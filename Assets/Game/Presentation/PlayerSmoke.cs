using System;
using System.Collections;
using System.IO;
using Emberfield.Simulation;
using UnityEngine;

namespace Emberfield.Presentation
{
    // Opt-in development-player smoke; never runs for an ordinary game launch.
    public static class PlayerSmoke
    {
        public static void TryStart(MatchController match)
        {
            if (!Debug.isDebugBuild) return;
            string[] args = Environment.GetCommandLineArgs();
            if (MovementPlayerProbe.TryStart(match, args)) return;
            if (OfflineRenderProbe.TryStart(match, args)) return;
            for (int i = 0; i + 1 < args.Length; i++)
                if (args[i] == "-emberfieldArtReview")
                {
                    Application.runInBackground = true;
                    match.StartCoroutine(ArtReviewSmoke.Run(match, args[i + 1]));
                    return;
                }
            for (int i = 0; i + 1 < args.Length; i++)
                if (args[i] == "-emberfieldOfflineSmoke")
                {
                    Application.runInBackground = true;
                    match.StartCoroutine(OfflinePlayerSmoke.Run(match, args[i + 1]));
                    return;
                }
            for (int i = 0; i + 1 < args.Length; i++)
                if (args[i] == "-emberfieldFactionSmoke")
                {
                    Application.runInBackground = true;
                    match.StartCoroutine(FactionPlayerSmoke.Run(match, args[i + 1]));
                    return;
                }
            for (int i = 0; i + 1 < args.Length; i++)
                if (args[i] == "-emberfieldTechnologySmoke")
                {
                    Application.runInBackground = true;
                    match.StartCoroutine(TechnologyPlayerSmoke.Run(match, args[i + 1]));
                    return;
                }
            for (int i = 0; i + 1 < args.Length; i++)
                if (args[i] == "-emberfieldCombatSmoke")
                {
                    Application.runInBackground = true;
                    if (!match.IsCombatSandbox) UnityEngine.SceneManagement.SceneManager.LoadScene("CombatSandbox");
                    else match.StartCoroutine(CombatPlayerSmoke.Run(match, args[i + 1]));
                    return;
                }
            for (int i = 0; i + 1 < args.Length; i++)
                if (args[i] == "-emberfieldPracticeMinimapCapture")
                {
                    Application.runInBackground = true;
                    match.StartCoroutine(PracticeMinimapSmoke.Run(match, args[i + 1]));
                    return;
                }
            for (int i = 0; i + 1 < args.Length; i++)
                if (args[i] == "-emberfieldSmoke")
                {
                    Application.runInBackground = true;
                    Debug.Log("EMBERFIELD_PLAYER_SMOKE_START");
                    match.StartCoroutine(Run(match, args[i + 1]));
                    return;
                }
        }

        private static IEnumerator Run(MatchController match, string folder)
        {
            Directory.CreateDirectory(folder);
            yield return null;
            while (!Application.isBatchMode && !UnityEngine.Rendering.SplashScreen.isFinished) yield return null;
            match.Select(new[] { 1 });
            match.World.TryGetUnit(1, out var worker);
            var start = worker.Position;
            var result = match.World.Submit(new MoveCommand(MatchController.LocalPlayer, new[] { 1 }, new SimPoint(11500, 19500)));
            yield return new WaitForSeconds(1);
            bool passed = result.Accepted && worker.Position != start && match.View.Count == match.World.Units.Count + match.World.Buildings.Count + match.World.Resources.Count;
            match.StopSelected();
            match.World.TryGetPlayer(MatchController.LocalPlayer, out var player);
            var startingStock = player.Resources;
            var hallDefinition = match.Economy.BuildingDefinition("muster_hall");
            var soldierDefinition = match.Economy.UnitDefinition("reedguard");
            int neededWood = hallDefinition.Cost.Wood + soldierDefinition.Cost.Wood;
            match.World.TryGetResource(201, out var wood);
            match.Tap(match.Rig.Camera.WorldToScreenPoint(DefinitionLoader.ToWorld(wood.Position) + Vector3.up * .6f), false);
            int budget = 20000;
            while (player.Resources.Wood < neededWood && budget > 0)
            {
                for (int step = 0; step < 20; step++) { match.World.Tick(); budget--; }
                match.View.Sync(1, match.Selection);
                yield return null;
            }
            bool gathered = player.Resources.Wood >= neededWood && player.Resources.Wood > startingStock.Wood;
            passed &= gathered;
            match.Economy.BeginBuild("muster_hall");
            match.Economy.PreviewAt(new SimPoint(16500, 21500));
            var build = match.Economy.ConfirmBuild();
            bool hasHall = build.Accepted && match.World.TryGetBuilding(build.EntityId, out _);
            passed &= hasHall;
            BuildingState hall = null;
            if (hasHall)
            {
                match.World.TryGetBuilding(build.EntityId, out hall);
                budget = 10000;
                while (!hall.IsComplete && budget > 0)
                {
                    for (int step = 0; step < 20; step++) { match.World.Tick(); budget--; }
                    match.View.Sync(1, match.Selection);
                    yield return null;
                }
                passed &= hall.IsComplete;
                match.Select(new[] { hall.Id });
            }
            int unitsBefore = match.World.Units.Count;
            var train = match.Economy.Train("reedguard");
            passed &= train.Accepted;
            budget = 10000;
            while (train.Accepted && match.World.Units.Count == unitsBefore && budget > 0)
            {
                for (int step = 0; step < 20; step++) { match.World.Tick(); budget--; }
                match.View.Sync(1, match.Selection);
                yield return null;
            }
            bool trained = match.World.Units.Count == unitsBefore + 1;
            passed &= trained;
            match.View.Sync(1, match.Selection);
            match.SetFeedback(passed ? "Resources delivered. Muster Hall complete. Reedguard trained." : "Economy smoke failed; inspect report.");
            match.Rig.Focus(new Vector3(15, 0, 17));
            match.Hud.Refresh();
            match.Hud.PrepareOffscreenCapture(match.Rig.Camera);
            yield return new WaitForEndOfFrame();
            passed &= Capture(match, Path.Combine(folder, "greybox.png"));
            // Continue with this newly trained soldier to verify the entire first gameplay loop.
            UnitState soldier = null;
            foreach (var unit in match.World.Units) if (unit.OwnerId == 1 && unit.DefinitionId == "reedguard") { soldier = unit; break; }
            bool fought = false;
            if (soldier != null && match.World.TryGetUnit(5, out var opponent))
            {
                match.Select(new[] { soldier.Id });
                match.Rig.Focus(DefinitionLoader.ToWorld(opponent.Position));
                match.SyncPresentation(1);
                match.Tap(match.Rig.Camera.WorldToScreenPoint(DefinitionLoader.ToWorld(opponent.Position) + Vector3.up * .6f), false);
                budget = World.TickRate * 90;
                while (match.World.TryGetUnit(opponent.Id, out _) && budget > 0)
                {
                    for (int step = 0; step < 20; step++) { match.World.Tick(); budget--; }
                    match.SyncPresentation(1);
                    yield return null;
                }
                fought = !match.World.TryGetUnit(opponent.Id, out _) && match.World.TryGetUnit(soldier.Id, out _) && match.World.DeathCount > 0;
                match.StopSelected(); match.SyncPresentation(1);
                match.SetFeedback(fought ? "Gathered, built, trained and fought. The first gameplay loop is verified." : "Trained soldier combat check failed.");
                match.Hud.Refresh();
                yield return new WaitForEndOfFrame();
                passed &= Capture(match, Path.Combine(folder, "combat-finish.png"));
            }
            passed &= fought;
            File.WriteAllText(Path.Combine(folder, "smoke.txt"), $"Passed: {passed}\nUnity: {Application.unityVersion}\nTick: {match.World.TickIndex}\nEntities: {match.View.Count}\nResolution: {Screen.width}x{Screen.height}\nGraphics: {SystemInfo.graphicsDeviceName}\nGathered: {gathered}\nBuilt: {hall != null && hall.IsComplete}\nTrained: {trained}\nTrainedSoldierFought: {fought}\nEconomyTicksAccelerated: True\nPopulation: {player.PopulationUsed}+{player.PopulationReserved}/{player.PopulationCapacity}\n");
            Debug.Log("EMBERFIELD_PLAYER_SMOKE " + passed);
            Application.Quit(passed ? 0 : 1);
        }

        internal static bool Capture(MatchController match, string path)
        {
            // Batch players have no visible backbuffer. Render the actual camera and HUD explicitly.
            var target = new RenderTexture(Screen.width, Screen.height, 24);
            target.Create();
            UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(match.Rig.Camera,
                new UnityEngine.Rendering.RenderPipeline.StandardRequest { destination = target });
            bool hasPixels = CaptureTarget(target, path);
            target.Release(); UnityEngine.Object.Destroy(target);
            return hasPixels;
        }

        internal static bool CaptureTarget(RenderTexture target, string path)
        {
            if (target == null || !target.IsCreated()) return false;
            var previous = RenderTexture.active;
            RenderTexture.active = target;
            var screenshot = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            screenshot.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            screenshot.Apply();
            RenderTexture.active = previous;
            bool hasPixels = false;
            // A valid dark modal can have an empty centre row. Sample the whole image so
            // headings, buttons and map pixels all contribute to the render proof.
            for (int y = 20; y < screenshot.height && !hasPixels; y += 40)
                for (int x = 20; x < screenshot.width; x += 40)
                    if (screenshot.GetPixel(x, y).maxColorComponent > .05f) { hasPixels = true; break; }
            File.WriteAllBytes(path, screenshot.EncodeToPNG());
            UnityEngine.Object.Destroy(screenshot);
            return hasPixels;
        }
    }
}
