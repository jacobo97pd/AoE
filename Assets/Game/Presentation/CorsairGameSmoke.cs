using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Emberfield.Simulation;
using UnityEngine;

namespace Emberfield.Presentation
{
    /// <summary>Explicit development-player probe using the real coastal match, command path and HUD.</summary>
    public static class CorsairGameSmoke
    {
        private const string Flag = "-corsairGameReview";
        private static int fixtureEnemyId;
        private static bool started;

        public static bool Requested => Array.IndexOf(Environment.GetCommandLineArgs(), Flag) >= 0;

        // Called only while constructing the opt-in review world. The shipped map and
        // ordinary JUGAR scenario remain unchanged, as do every unit's actual stats.
        public static void AddFixtureSpawns(World baseline, List<UnitSpawnDefinition> spawns, UnitSpawnDefinition heroSpawn)
        {
            if (!Requested) return;
            for (int radius = 10000; radius <= 18000; radius += 1000)
            for (int angle = 0; angle < 24; angle++)
            {
                double radians = angle * Math.PI / 12;
                var point = new SimPoint(heroSpawn.Position.X + (int)(Math.Cos(radians) * radius), heroSpawn.Position.Z + (int)(Math.Sin(radians) * radius));
                if (!ClearLine(baseline, heroSpawn.Position, point) || !Free(baseline, spawns, point)) continue;
                // A friendly worker also sees the duel, so removing the hero does not
                // artificially require clearing fog to observe its visual death.
                if (!spawns.Any(s => s.OwnerId == MatchController.LocalPlayer && s.Id != heroSpawn.Id && Distance(s.Position, point) < 6500)) continue;
                fixtureEnemyId = heroSpawn.Id + 1;
                spawns.Add(new UnitSpawnDefinition { Id = fixtureEnemyId, OwnerId = 2, DefinitionId = "reedguard", Position = point });
                return;
            }
            throw new InvalidOperationException("Could not place the opt-in corsair combat fixture on the actual coastal map.");
        }

        private static bool Free(World world, List<UnitSpawnDefinition> spawns, SimPoint point)
        {
            foreach (int x in new[] { -400, 0, 400 }) foreach (int z in new[] { -400, 0, 400 })
                if (!world.IsWalkable(new SimPoint(point.X + x, point.Z + z))) return false;
            return !spawns.Any(s => Distance(s.Position, point) < 1500);
        }

        private static bool ClearLine(World world, SimPoint from, SimPoint to)
        {
            int samples = Math.Max(1, (int)(Distance(from, to) / 250));
            for (int i = 1; i <= samples; i++)
            {
                var point = new SimPoint(from.X + (to.X - from.X) * i / samples, from.Z + (to.Z - from.Z) * i / samples);
                foreach (int x in new[] { -400, 400 }) foreach (int z in new[] { -400, 400 })
                    if (!world.IsWalkable(new SimPoint(point.X + x, point.Z + z))) return false;
            }
            return true;
        }

        private static double Distance(SimPoint a, SimPoint b)
        {
            double dx = a.X - b.X, dz = a.Z - b.Z;
            return Math.Sqrt(dx * dx + dz * dz);
        }

        public static void TryStart(MatchController match)
        {
            if (started || !Debug.isDebugBuild) return;
            var args = Environment.GetCommandLineArgs(); int flag = Array.IndexOf(args, Flag);
            if (flag < 0 || flag + 1 >= args.Length) return;
            started = true; Application.runInBackground = true;
            match.StartCoroutine(RunSafely(match, args[flag + 1]));
        }

        [Serializable] private sealed class Report
        {
            public string buildGuid, unity, graphicsDevice, map, realm;
            public int width, height, heroCount, skinnedRenderers, lods, heroId, enemyId, enemyInitialHealth, enemyFinalHealth, heroInitialHealth, heroFinalHealth;
            public long ticks;
            public bool passed, movementAccepted, locomotionObserved, attackAccepted, attackObserved, enemyDamaged, enemyAttackAccepted, heroDamaged, hitObserved, heroDied, corpseObserved, corpseAnimated, pausePreserved, hudPixels;
            public double movementMetres;
            public string fixture = "Explicit -corsairGameReview coastal fixture: one additional enemy Reedguard; original unit statistics, fog and gameplay command rules. Tick stepping is accelerated; this is not a frame-rate benchmark. Enemy receives attack orders directly as the test opponent.";
            public List<string> failures = new List<string>();
            public List<string> screenshots = new List<string>();
        }

        private static IEnumerator RunSafely(MatchController match, string folder)
        {
            Directory.CreateDirectory(folder);
            var report = new Report { buildGuid = Application.buildGUID, unity = Application.unityVersion, graphicsDevice = SystemInfo.graphicsDeviceName, width = Screen.width, height = Screen.height };
            var stack = new Stack<IEnumerator>();
            stack.Push(Run(match, folder, report));
            while (stack.Count > 0)
            {
                bool more; object yielded = null;
                var run = stack.Peek();
                try { more = run.MoveNext(); if (more) yielded = run.Current; }
                catch (Exception error) { report.failures.Add(error.ToString()); break; }
                if (!more) { stack.Pop(); (run as IDisposable)?.Dispose(); continue; }
                if (yielded is IEnumerator nested) { stack.Push(nested); continue; }
                yield return yielded;
            }
            report.passed = report.failures.Count == 0;
            if (match.World != null) report.ticks = match.World.TickIndex;
            File.WriteAllText(Path.Combine(folder, "gameplay-review.json"), JsonUtility.ToJson(report, true));
            Debug.Log("CORSAIR_GAME_REVIEW " + report.passed);
            Application.Quit(report.passed ? 0 : 1);
        }

        private static IEnumerator Run(MatchController match, string folder, Report report)
        {
            match.enabled = false; // The coroutine advances real World.Tick exactly once per sample.
            match.Shell?.Close();
            yield return null;
            while (!Application.isBatchMode && !UnityEngine.Rendering.SplashScreen.isFinished) yield return null;
            report.map = match.World.Map.Id; report.realm = match.World.Map.RealmId;
            Require(report.realm == ContentRealms.Naval, "The pirate must be tested in the naval realm.");
            var heroes = match.World.Units.Where(u => u.DefinitionId == CorsairHeroScenario.HeroId).ToArray();
            report.heroCount = heroes.Length;
            Require(report.heroCount == 1, "Exactly one real pirate hero must exist.");
            var hero = heroes[0]; report.heroId = hero.Id; report.heroInitialHealth = hero.Health;
            Require(match.World.TryGetUnit(fixtureEnemyId, out var enemy), "The controlled enemy fixture is missing.");
            report.enemyId = enemy.Id; report.enemyInitialHealth = enemy.Health;
            foreach (int owner in new[] { 1, 2 })
                match.World.Submit(new StopCommand(owner, match.World.Units.Where(u => u.OwnerId == owner).Select(u => u.Id).ToArray()));
            match.Select(new[] { hero.Id }); match.SyncPresentation(1);
            var root = match.View.RootFor(hero.Id);
            var driver = root ? root.GetComponentInChildren<CorsairAnimationDriver>(true) : null;
            Require(driver && driver.HasValidRig, "WorldView did not instantiate the imported animated hero.");
            report.skinnedRenderers = driver.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length;
            var lod = driver.GetComponent<LODGroup>(); report.lods = lod ? lod.lodCount : 0;
            Require(report.skinnedRenderers == 3 && report.lods == 3, "The real hero must have exactly three skinned LODs.");
            Frame(match, hero.Position, 5);
            match.SetFeedback("Corsario Carmesí · Héroe pirata seleccionado. Prueba real de movimiento y combate.");
            yield return SaveFrame(match, folder, "gameplay-spawn.png", report);

            var start = hero.Position;
            double distance = Distance(start, enemy.Position);
            double fraction = Math.Max(.2, (distance - 6500) / distance);
            var destination = new SimPoint(start.X + (int)((enemy.Position.X - start.X) * fraction), start.Z + (int)((enemy.Position.Z - start.Z) * fraction));
            report.movementAccepted = match.SubmitPlayerCommand(new MoveCommand(1, new[] { hero.Id }, destination)).Accepted;
            Require(report.movementAccepted, "The real movement order was rejected.");
            bool movementImage = false;
            for (int tick = 0; tick < 400 && Distance(hero.Position, destination) > 400; tick++)
            {
                Step(match);
                report.locomotionObserved |= driver.CurrentState == "Walk" || driver.CurrentState == "Run";
                Frame(match, hero.Position, 5);
                if (!movementImage && Distance(start, hero.Position) > 1200 && report.locomotionObserved)
                {
                    match.SetFeedback("Orden de movimiento aceptada · El héroe recorre el terreno con animación esquelética.");
                    yield return SaveFrame(match, folder, "gameplay-moving.png", report); movementImage = true;
                }
                yield return null;
            }
            report.movementMetres = Distance(start, hero.Position) * .001;
            Require(report.movementMetres > 1 && report.locomotionObserved && Distance(hero.Position, destination) <= 600, "The hero did not complete a visible animated movement.");
            match.SubmitPlayerCommand(new StopCommand(1, new[] { hero.Id })); Step(match);
            var bones = driver.GetComponentInChildren<SkinnedMeshRenderer>(true).bones;
            var rotations = bones.Select(b => b.localRotation).ToArray();
            long pausedTick = match.World.TickIndex;
            for (int i = 0; i < 20; i++) match.SyncPresentation(1);
            yield return null;
            report.pausePreserved = pausedTick == match.World.TickIndex && bones.Select((b, i) => Quaternion.Angle(b.localRotation, rotations[i])).All(a => a < .001f);
            Require(report.pausePreserved, "Repeated rendering changed the paused animation or world time.");

            Require(match.World.Vision == null || match.World.Vision.IsEntityVisible(1, enemy.Id), "The enemy must be visible through normal fog rules before the attack.");
            report.attackAccepted = match.SubmitPlayerCommand(new AttackCommand(1, new[] { hero.Id }, enemy.Id)).Accepted;
            Require(report.attackAccepted, "The real attack order was rejected.");
            for (int tick = 0; tick < 400 && enemy.Health == report.enemyInitialHealth; tick++)
            {
                Step(match); report.attackObserved |= driver.CurrentState == "Attack";
                Frame(match, Midpoint(hero.Position, enemy.Position), 4.5f);
                yield return null;
            }
            report.enemyFinalHealth = enemy.Health; report.enemyDamaged = enemy.Health < report.enemyInitialHealth;
            Require(report.enemyDamaged && report.attackObserved, "The hero must damage the enemy and enter Attack from real combat.");
            for (int i = 0; i < 5; i++) { Step(match); yield return null; }
            match.SetFeedback("Ataque confirmado · Daño real infligido al enemigo: " + (report.enemyInitialHealth - enemy.Health));
            yield return SaveFrame(match, folder, "gameplay-attack.png", report);

            // Stop is the game's own hold-fire command. The surviving enemy now attacks;
            // no health mutation, boosted damage or fake animation trigger is used.
            match.SubmitPlayerCommand(new StopCommand(1, new[] { hero.Id }));
            report.enemyAttackAccepted = match.World.Submit(new AttackCommand(2, new[] { enemy.Id }, hero.Id)).Accepted;
            Require(report.enemyAttackAccepted, "The enemy's real counterattack was rejected.");
            bool hitImage = false;
            for (int tick = 0; tick < 1400 && match.World.TryGetUnit(hero.Id, out _); tick++)
            {
                Step(match);
                report.heroDamaged |= hero.Health < report.heroInitialHealth;
                report.hitObserved |= driver && driver.CurrentState == "Hit";
                if (!hitImage && report.hitObserved)
                {
                    for (int i = 0; i < 3; i++) Step(match);
                    match.SetFeedback("Impacto recibido · Salud real del héroe: " + hero.Health + "/" + hero.MaxHealth);
                    yield return SaveFrame(match, folder, "gameplay-hit.png", report); hitImage = true;
                }
                yield return null;
            }
            report.heroFinalHealth = hero.Health;
            report.heroDied = !match.World.TryGetUnit(hero.Id, out _);
            report.corpseObserved = driver && driver.IsDying && driver.CurrentState == "Death" && !match.View.RootFor(hero.Id);
            Require(report.heroDamaged && report.hitObserved && report.heroDied && report.corpseObserved, "Real damage, hit response and visual death must all be observed.");
            var corpseBones = driver.GetComponentInChildren<SkinnedMeshRenderer>(true).bones;
            var deathBefore = corpseBones.Select(b => b.localRotation).ToArray();
            yield return new WaitForSeconds(Mathf.Min(.8f, driver.Duration("Death") * .4f));
            report.corpseAnimated = corpseBones.Select((b, i) => Quaternion.Angle(b.localRotation, deathBefore[i])).Any(a => a > .05f);
            Require(report.corpseAnimated, "The detached death corpse did not animate.");
            match.SetFeedback("Caída confirmada · La unidad ha salido de la simulación; la animación final es visual.");
            yield return SaveFrame(match, folder, "gameplay-death.png", report);
            Require(report.hudPixels && report.screenshots.Count == 5, "The five native gameplay screenshots are incomplete.");
        }

        private static SimPoint Midpoint(SimPoint a, SimPoint b) => new SimPoint((a.X + b.X) / 2, (a.Z + b.Z) / 2);
        private static void Step(MatchController match) { match.World.Tick(); match.SyncPresentation(1); }
        private static void Frame(MatchController match, SimPoint point, float zoom)
        {
            match.Rig.MinimumZoom = 4;
            match.Rig.SetHome(DefinitionLoader.ToWorld(point), zoom);
        }
        private static IEnumerator SaveFrame(MatchController match, string folder, string file, Report report)
        {
            match.Hud.Refresh(); match.Hud.PrepareOffscreenCapture(match.Rig.Camera);
            yield return new WaitForEndOfFrame();
            bool pixels = PlayerSmoke.Capture(match, Path.Combine(folder, file));
            Require(pixels, "The gameplay render was empty: " + file);
            report.hudPixels = true; report.screenshots.Add(file);
        }
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    }
}
