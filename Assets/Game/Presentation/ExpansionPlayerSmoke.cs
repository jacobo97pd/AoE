using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Emberfield.Simulation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Emberfield.Presentation
{
    // Opt-in acceptance against the actual packaged frontend and ordinary AI commands.
    public static class ExpansionPlayerSmoke
    {
        [Serializable] private sealed class Report
        {
            public bool Passed, CatalogPreviewPreservesWorld;
            public string BuildGuid, Failure = "", Method = "Native pointer clicks, original starting economies and ordinary AI commands; accelerated development is not an FPS measurement.";
            public int Width,Height,VisibleButtonClicks;
            public List<Case> Cases = new List<Case>();
        }
        [Serializable] private sealed class Case
        {
            public string Faction,Realm,Map;
            public long Tick;
            public bool SignatureObserved;
            public int Units,Buildings,Era,Workers,Army;
        }
        private static readonly string[] Factions = { "aven","serevin","english","ashen","drakeforged","skeld","verdant","pirates","sultanate","sahel" };
        private static readonly string[] Maps = { "amber_crossing","sapphire_coast","sunscar_basin","amber_crossing","sapphire_coast","amber_crossing","sunscar_basin","sapphire_coast","sunscar_basin","sunscar_basin" };
        private static Report report;
        private static int caseIndex;
        private static string folder,previousPreviews;
        private static bool awaitingMatch;
        public static void TryStart(MatchController match)
        {
            if (!Debug.isDebugBuild) return;
            var args = Environment.GetCommandLineArgs(); int option = Array.IndexOf(args,"-emberfieldExpansionSmoke");
            if (option < 0 || option+1 >= args.Length) return;
            folder = Path.GetFullPath(args[option+1]); Directory.CreateDirectory(folder); Application.runInBackground = true;
            if (report == null)
            {
                report = new Report { BuildGuid = Application.buildGUID, Width = Screen.width,Height = Screen.height };
                previousPreviews = PlayerPrefs.GetString("Emberfield.Alpha03.CosmeticPreviews","");
            }
            match.StartCoroutine(Guard(awaitingMatch ? Gameplay(match) : Intro(match)));
        }
        private static IEnumerator Guard(IEnumerator routine)
        {
            var stack = new Stack<IEnumerator>(); stack.Push(routine);
            while (stack.Count > 0)
            {
                bool more = false; object current = null;
                try { more = stack.Peek().MoveNext(); if (more) current = stack.Peek().Current; }
                catch (Exception error) { report.Failure = error.ToString(); Finish(false); yield break; }
                if (!more) { stack.Pop(); continue; }
                if (current is IEnumerator nested) stack.Push(nested); else yield return current;
            }
        }
        private static IEnumerator Intro(MatchController match)
        {
            while (!UnityEngine.Rendering.SplashScreen.isFinished) yield return null;
            match.Shell.Open(); yield return Capture(match,"frontiers-home.png");
            yield return Click(match,"Navigation Store"); yield return null;
            string rules = JsonUtility.ToJson(match.World.Definition); long tick = match.World.TickIndex;
            yield return Click(match,"Inspect drake_frost"); yield return null;
            yield return Click(match,"Preview drake_frost"); yield return null;
            report.CatalogPreviewPreservesWorld = match.Shell.HasCatalog && !match.Shell.PurchasesAvailable && match.World.TickIndex == tick && rules == JsonUtility.ToJson(match.World.Definition);
            Require(report.CatalogPreviewPreservesWorld,"Cosmetic preview changed competitive state or checkout was enabled.");
            yield return Capture(match,"store-drake.png");
            yield return Click(match,"Store filter historical"); yield return null;
            yield return Click(match,"Inspect elephant_imperial"); yield return null;
            yield return Click(match,"Preview elephant_imperial"); yield return null;
            yield return Capture(match,"store-elephant.png");
            yield return Click(match,"Store filter all");
            yield return Click(match,"Next collection page");
            foreach (string item in new[] { "troll_frost", "guardian_bloom", "lion_luminous", "frostguard_bronze" })
            {
                yield return Click(match,"Inspect " + item);
                yield return Capture(match,"store-" + item + ".png");
            }
            yield return LoadCase(match);
        }
        private static IEnumerator LoadCase(MatchController match)
        {
            match.enabled = true; match.Shell.Open();
            yield return Click(match,"Navigation Play");
            yield return Click(match,"Choose realm " + ContentRealms.RealmForFaction(Factions[caseIndex]));
            yield return Click(match,"Select " + Factions[caseIndex]);
            yield return Click(match,"Choose map " + Maps[caseIndex]);
            yield return Click(match,"Choose Conquest");
            yield return Capture(match,"setup-" + Factions[caseIndex] + ".png");
            awaitingMatch = true;
            yield return Click(match,"Begin skirmish");
        }
        private static IEnumerator Gameplay(MatchController match)
        {
            yield return null; match.enabled = false; match.OfflineControls.Close();
            string faction = Factions[caseIndex],realm = ContentRealms.RealmForFaction(faction);
            Require(match.Factions.LocalDefinition.Id == faction && match.World.Map.Id == Maps[caseIndex] && match.World.Map.RealmId == realm,"Frontend started the wrong realm, faction or biome.");
            foreach (var seat in match.World.Map.PlayerFactions) Require(ContentRealms.IsPlayableFactionInRealm(seat.FactionId,realm),"Cross-realm or unavailable opponent escaped selection validation.");
            foreach (var unit in match.World.Units) if (unit.OwnerId == 1 && unit.IsWorker)
                Require(unit.DefinitionId == ContentRealms.StartingWorkerForFaction(faction), "The faction started with the wrong worker roster.");
            var local = new OfflineAi(match.World,1); string signature = FrontierCodex.SignatureUnit(faction); bool sawSignature = false;
            int signatureId = 0;
            while (match.World.TickIndex < 18000 && !match.World.Match.IsFinished)
            {
                for (int i = 0; i < 100 && !match.World.Match.IsFinished; i++) { local.Tick(); match.AdvanceSimulationTick(); }
                foreach (var unit in match.World.Units) if (unit.OwnerId == 1 && unit.DefinitionId == signature) { sawSignature = true; signatureId = unit.Id; }
                // Aven's unarmed relay specialist is covered by the faction drill, not recruited
                // by combat AI. Capture its developed economy before the result overlay opens.
                if ((sawSignature || faction == "aven") && match.World.TickIndex > 3000) break;
                match.SyncPresentation(1); yield return null;
            }
            match.World.TryGetPlayer(1,out var player);
            var entry = new Case { Faction = faction,Realm = realm,Map = match.World.Map.Id,Tick = match.World.TickIndex,SignatureObserved = sawSignature,Units = match.World.Units.Count,Buildings = match.World.Buildings.Count,Era = player.EraTier,Workers = match.Metrics.Workers,Army = match.Metrics.Army };
            report.Cases.Add(entry);
            Require(entry.Tick >= 1000 && entry.Buildings >= 3,"The original economy did not develop.");
            if (caseIndex >= 2) Require(sawSignature,"The new faction never deployed its signature army: " + faction);
            match.Rig.Home(); match.ClearSelection();
            yield return Click(match,"Rotate view");
            yield return Click(match,"Rotate view");
            if (signatureId > 0 && match.World.TryGetUnit(signatureId,out var special))
            { match.Select(new[] { signatureId }); match.Rig.SetHome(DefinitionLoader.ToWorld(special.Position),6); }
            match.SyncPresentation(1);
            yield return Capture(match,"battle-" + faction + ".png");
            File.WriteAllText(Path.Combine(folder,"expansion-smoke.json"),JsonUtility.ToJson(report,true));
            caseIndex++;
            if (caseIndex == Factions.Length) { Finish(true); yield break; }
            yield return LoadCase(match);
        }
        private static IEnumerator Click(MatchController match,string name)
        {
            Canvas.ForceUpdateCanvases(); yield return new WaitForEndOfFrame(); Button target = null;
            foreach (var button in match.GetComponentsInChildren<Button>()) if (button.name == name && button.isActiveAndEnabled) { target = button; break; }
            Require(target != null && target.interactable,"Missing active control: " + name);
            var rect = (RectTransform)target.transform;
            var pointer = new PointerEventData(EventSystem.current) { position = RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center)) };
            var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(pointer,hits);
            Require(hits.Count > 0 && hits[0].gameObject.GetComponentInParent<Button>() == target,"Control covered: " + name);
            report.VisibleButtonClicks++; ExecuteEvents.Execute(target.gameObject,pointer,ExecuteEvents.pointerClickHandler); yield return null;
        }
        private static IEnumerator Capture(MatchController match,string name)
        {
            Canvas.ForceUpdateCanvases(); yield return new WaitForEndOfFrame();
            foreach (var preview in match.GetComponentsInChildren<CosmeticModelPreview>())
            { preview.RenderPreview(); Require(preview.HasRendered,"The wardrobe model was not rendered."); }
            var canvases = match.GetComponentsInChildren<Canvas>(); var modes = new RenderMode[canvases.Length];
            var cameras = new Camera[canvases.Length]; var distances = new float[canvases.Length]; var orders = new int[canvases.Length];
            for (int i = 0; i < canvases.Length; i++)
            {
                modes[i] = canvases[i].renderMode; cameras[i] = canvases[i].worldCamera; distances[i] = canvases[i].planeDistance; orders[i] = canvases[i].sortingOrder;
                if (modes[i] == RenderMode.ScreenSpaceOverlay) { canvases[i].renderMode = RenderMode.ScreenSpaceCamera; canvases[i].worldCamera = match.Rig.Camera; canvases[i].planeDistance = 1; }
            }
            try { Canvas.ForceUpdateCanvases(); Require(PlayerSmoke.Capture(match,Path.Combine(folder,name)),"Empty gameplay image: " + name); }
            finally
            {
                for (int i = 0; i < canvases.Length; i++) { canvases[i].renderMode = modes[i]; canvases[i].worldCamera = cameras[i]; canvases[i].planeDistance = distances[i]; canvases[i].sortingOrder = orders[i]; }
                Canvas.ForceUpdateCanvases();
            }
        }
        private static void Require(bool condition,string message) { if (!condition) throw new InvalidOperationException(message); }
        private static void Finish(bool passed)
        {
            report.Passed = passed; CosmeticLoadout.ClearPreviews(); foreach (string id in (previousPreviews ?? "").Split('|')) CosmeticLoadout.EquipPreview(id);
            File.WriteAllText(Path.Combine(folder,"expansion-smoke.json"),JsonUtility.ToJson(report,true));
            if (!passed) Debug.LogError("EMBERFIELD_EXPANSION_SMOKE " + report.Failure);
            Application.Quit(passed ? 0 : 1);
        }
    }
}
