using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Emberfield.Simulation;
using UnityEngine;

namespace Emberfield.Presentation
{
    /// <summary>Opt-in native-player proof of the three imported roles in the real coastal match.</summary>
    public static class PirateCrewGameSmoke
    {
        private const string Flag = "-pirateCrewGameReview";
        private const string RecordFlag = "-pirateCrewRecordFrames";
        private static int fixtureEnemyId;
        private static bool started;
        private static FrameRecorder recorder;
        public static bool Requested => Array.IndexOf(Environment.GetCommandLineArgs(), Flag) >= 0;

        // The only fixture mutation is an extra ordinary enemy spawn. Normal JUGAR,
        // shipped map files, fog, statistics, resources and economy remain unchanged.
        public static void AddFixtureSpawns(World baseline, List<UnitSpawnDefinition> spawns)
        {
            if (!Requested) return;
            var raider = spawns.Single(s => s.OwnerId == MatchController.LocalPlayer && s.DefinitionId == ImportedCharacterVisuals.BoardingRaiderId);
            var gunner = spawns.Single(s => s.OwnerId == MatchController.LocalPlayer && s.DefinitionId == ImportedCharacterVisuals.GunpowderCorsairId);
            for (int radius = 5500; radius <= 7000; radius += 500)
            for (int angle = 0; angle < 24; angle++)
            {
                double radians = angle * Math.PI / 12;
                var point = new SimPoint(gunner.Position.X + (int)(Math.Cos(radians) * radius), gunner.Position.Z + (int)(Math.Sin(radians) * radius));
                if (!Free(baseline, spawns, point) || !ClearLine(baseline, raider.Position, point) || !ClearLine(baseline, gunner.Position, point)) continue;
                if (!spawns.Any(s => s.OwnerId == MatchController.LocalPlayer && Distance(s.Position, point) < 6000)) continue;
                fixtureEnemyId = 1;
                foreach (var spawn in spawns) fixtureEnemyId = Math.Max(fixtureEnemyId, spawn.Id + 1);
                foreach (var building in baseline.Map.BuildingSpawns) fixtureEnemyId = Math.Max(fixtureEnemyId, building.Id + 1);
                foreach (var resource in baseline.Map.ResourceSpawns) fixtureEnemyId = Math.Max(fixtureEnemyId, resource.Id + 1);
                spawns.Add(new UnitSpawnDefinition { Id = fixtureEnemyId, OwnerId = 2, DefinitionId = "reedguard", Position = point });
                return;
            }
            throw new InvalidOperationException("No clear, normally visible coastal location exists for the crew combat fixture.");
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

        public static void TryStart(MatchController match)
        {
            if (started || !Debug.isDebugBuild) return;
            var args = Environment.GetCommandLineArgs(); int flag = Array.IndexOf(args, Flag);
            if (flag < 0 || flag + 1 >= args.Length) return;
            started = true; Application.runInBackground = true;
            int record = Array.IndexOf(args, RecordFlag);
            if (record >= 0 && record + 1 < args.Length) recorder = new FrameRecorder(args[record + 1]);
            match.StartCoroutine(RunSafely(match, args[flag + 1]));
        }

        [Serializable] private sealed class Role
        {
            public string definitionId, state;
            public int id, skinnedRenderers, lods, clips;
            public bool validRig, isWorker;
        }

        [Serializable] private sealed class Report
        {
            public string buildGuid, unity, graphicsDevice, map, realm, resourceKind;
            public int width, height, population, populationCapacity, captainCount, enemyId, enemyInitialHealth, enemyAfterRaider, enemyAfterGunner;
            public int resourceId, resourceInitialAmount, resourceFinalAmount, cargo, stockBefore, stockAfter;
            public long ticks, firstShotTick;
            public bool passed, sharedCrewMaterial, movementAccepted, locomotionObserved, raiderAttackAccepted, raiderAttackObserved, raiderDamage;
            public bool raiderOwnMaterial, raiderOwnPbrMaps;
            public bool gunnerAttackAccepted, gunnerAttackObserved, pistolProjectileObserved, projectileMeshVerified, projectileDirectionVerified, projectileOnScreen;
            public bool muzzlePresent, flashObserved, smokeObserved, followsMuzzleMarker, barrelAimed, gunnerDamage, gatherAccepted, workObserved, workDeformed, returnAccepted, deposited;
            public bool pausePreserved, pausedVfxPreserved, hudPixels, seekerClearanceAccepted, seekerClearanceCompleted;
            public double raiderMovementMetres, seekerMovementMetres, seekerClearanceMetres, workMaxVertexDisplacement;
            public float muzzleMarkerErrorMetres, firstShotBarrelAngleDegrees = 180;
            public string fixture = "Explicit -pirateCrewGameReview fixture: coastal offline match with the real captain and three distinct crew definitions; one additional ordinary Reedguard enemy held by Stop orders. The seeker receives an ordinary MoveCommand roughly 2.5 metres to the side, then Stop, to clear the visible firing line. Uses original resource 205, unit statistics, fog, commands, projectile system and HUD. World.Tick stepping is accelerated and is not a performance benchmark. No animation preview triggers or gameplay-state writes are used.";
            public string projectilePresentation = "Native 3D pistol ball and short tracer mesh aligned with simulated travel; not a camera-facing illustration.";
            public string materialValidation = "sharedCrewMaterial checks GunpowderCorsair and TreasureSeeker against the original PirateCrew atlas material. BoardingRaider uses MeshyRaider on every LOD, with its own base color, normal and metallic/smoothness texture references.";
            public List<Role> roles = new List<Role>();
            public List<string> failures = new List<string>();
            public List<string> screenshots = new List<string>();
        }

        private static IEnumerator RunSafely(MatchController match, string folder)
        {
            Directory.CreateDirectory(folder);
            var report = new Report { buildGuid = Application.buildGUID, unity = Application.unityVersion, graphicsDevice = SystemInfo.graphicsDeviceName, width = Screen.width, height = Screen.height };
            var stack = new Stack<IEnumerator>(); stack.Push(Run(match, folder, report));
            while (stack.Count > 0)
            {
                bool more; object yielded = null; var current = stack.Peek();
                try { more = current.MoveNext(); if (more) yielded = current.Current; }
                catch (Exception error) { report.failures.Add(error.ToString()); break; }
                if (!more) { stack.Pop(); (current as IDisposable)?.Dispose(); continue; }
                if (yielded is IEnumerator nested) { stack.Push(nested); continue; }
                yield return yielded;
            }
            while (stack.Count > 0) (stack.Pop() as IDisposable)?.Dispose();
            report.passed = report.failures.Count == 0 && report.sharedCrewMaterial && report.raiderOwnMaterial && report.raiderOwnPbrMaps;
            if (match.World != null) report.ticks = match.World.TickIndex;
            File.WriteAllText(Path.Combine(folder, "crew-gameplay-review.json"), JsonUtility.ToJson(report, true));
            if (recorder != null) Debug.Log("PIRATE_CREW_RECORDED " + recorder.Count + " frames at " + Mathf.RoundToInt(FrameRecorder.FramesPerTick / (float)World.TickSeconds) + " fps");
            Debug.Log("PIRATE_CREW_GAME_REVIEW " + report.passed);
            Application.Quit(report.passed ? 0 : 1);
        }

        private static IEnumerator Run(MatchController match, string folder, Report report)
        {
            match.enabled = false;
            match.Shell?.Close();
            yield return null;
            while (!Application.isBatchMode && !UnityEngine.Rendering.SplashScreen.isFinished) yield return null;
            var world = match.World;
            report.map = world.Map.Id; report.realm = world.Map.RealmId;
            Require(report.realm == ContentRealms.Naval && world.Match != null && !world.IsNetworkReplica, "The probe must run in the real naval offline coastal match.");
            var ids = new[] { ImportedCharacterVisuals.GameplayDefinitionId, ImportedCharacterVisuals.BoardingRaiderId, ImportedCharacterVisuals.GunpowderCorsairId, ImportedCharacterVisuals.TreasureSeekerId };
            var units = ids.Select(id => world.Units.Single(u => u.OwnerId == 1 && u.DefinitionId == id)).ToArray();
            var captain = units[0]; var raider = units[1]; var gunner = units[2]; var seeker = units[3];
            report.captainCount = world.Units.Count(u => u.OwnerId == 1 && u.DefinitionId == ids[0]);
            Require(report.captainCount == 1 && !raider.IsWorker && !gunner.IsWorker && seeker.IsWorker, "The crew must contain one captain and the three distinct gameplay roles.");
            Require(world.TryGetPlayer(1, out var player), "Local player is missing.");
            report.population = player.PopulationUsed; report.populationCapacity = player.PopulationCapacity;
            Require(report.population <= report.populationCapacity, "The crew scenario starts over population capacity.");
            Require(world.TryGetUnit(fixtureEnemyId, out var enemy), "The controlled Reedguard fixture is missing.");
            report.enemyId = enemy.Id; report.enemyInitialHealth = enemy.Health;
            foreach (int owner in new[] { 1, 2 })
                Require(world.Submit(new StopCommand(owner, world.Units.Where(u => u.OwnerId == owner).Select(u => u.Id).ToArray())).Accepted, "Initial hold-fire command failed.");
            match.Select(units.Select(u => u.Id).ToArray()); match.SyncPresentation(1);
            var drivers = new CorsairAnimationDriver[4];
            Material sharedCrew = null, raiderMaterial = null;
            for (int i = 0; i < units.Length; i++)
            {
                var root = match.View.RootFor(units[i].Id);
                drivers[i] = root ? root.GetComponentInChildren<CorsairAnimationDriver>(true) : null;
                var driver = drivers[i];
                Require(driver && driver.HasValidRig, "Missing imported animated model: " + ids[i]);
                var skins = driver.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                var lod = driver.GetComponent<LODGroup>();
                var role = new Role { definitionId = ids[i], id = units[i].Id, isWorker = units[i].IsWorker, validRig = driver.HasValidRig, skinnedRenderers = skins.Length, lods = lod ? lod.lodCount : 0, clips = driver.Animator.runtimeAnimatorController.animationClips.Distinct().Count(), state = driver.CurrentState };
                report.roles.Add(role);
                Require(role.skinnedRenderers == 3 && role.lods == 3 && role.clips >= (i == 3 ? 7 : 6), "Missing rig, animation states or LODs for " + ids[i]);
                foreach (string state in new[] { "Idle", "Walk", "Run", "Attack", "Hit", "Death" }) Require(driver.HasState(state), ids[i] + " lacks " + state);
                if (i == 1)
                {
                    raiderMaterial = skins[0].sharedMaterial;
                    report.raiderOwnMaterial = raiderMaterial && raiderMaterial.name == "MeshyRaider" &&
                        skins.All(s => s.sharedMaterials.Length > 0 && s.sharedMaterials.All(m => m == raiderMaterial));
                    Require(report.raiderOwnMaterial, "The replacement raider must use its own MeshyRaider material consistently across all LODs.");
                }
                else if (i > 1)
                {
                    if (sharedCrew == null) sharedCrew = skins[0].sharedMaterial;
                    Require(skins.All(s => s.sharedMaterials.Length > 0 && s.sharedMaterials.All(m => m == sharedCrew)),
                        "The gunner and seeker must share the original texture atlas material across all LODs.");
                }
            }
            report.sharedCrewMaterial = sharedCrew && sharedCrew.name == "PirateCrew";
            string[] properties = { "_BaseMap", "_BumpMap", "_MetallicGlossMap" };
            string[] textureNames = { "BaseColor", "Normal", "MetallicSmoothness" };
            report.raiderOwnPbrMaps = raiderMaterial && sharedCrew && raiderMaterial != sharedCrew &&
                raiderMaterial.shader && raiderMaterial.shader.name == "Universal Render Pipeline/Lit";
            for (int i = 0; i < properties.Length; i++)
            {
                var own = raiderMaterial && raiderMaterial.HasProperty(properties[i]) ? raiderMaterial.GetTexture(properties[i]) : null;
                var shared = sharedCrew && sharedCrew.HasProperty(properties[i]) ? sharedCrew.GetTexture(properties[i]) : null;
                report.raiderOwnPbrMaps &= own && shared && own != shared && own.name == textureNames[i];
            }
            Require(report.sharedCrewMaterial && report.raiderOwnMaterial && report.raiderOwnPbrMaps,
                "The gunner/seeker must retain PirateCrew, while the raider must use a separate complete MeshyRaider PBR texture set.");
            Require(drivers[3].HasState("Work"), "The seeker must have a dedicated Work animation.");
            var center = new SimPoint((int)units.Average(u => u.Position.X), (int)units.Average(u => u.Position.Z));
            Frame(match, center, 6.5f);
            match.SetFeedback("Tripulación real: capitán, sable, pistola y buscadora. Población " + report.population + "/" + report.populationCapacity + ".");
            yield return SaveFrame(match, folder, "gameplay-crew-spawn.png", report);

            // An actual movement order clears the gun barrel's physical space. The
            // posed mesh extends beyond its navigation radius; moving the worker
            // avoids intersecting her body without teleporting or hiding an ally.
            SimPoint seekerBeforeClearance = seeker.Position;
            SimPoint seekerClearance = SeekerClearanceDestination(world, seeker, gunner, enemy);
            report.seekerClearanceAccepted = match.SubmitPlayerCommand(new MoveCommand(1, new[] { seeker.Id }, seekerClearance)).Accepted;
            Require(report.seekerClearanceAccepted, "The seeker's firing-line clearance MoveCommand was rejected.");
            for (int tick = 0; tick < 240 && Distance(seeker.Position, seekerClearance) > 250; tick++)
            { Step(match); yield return null; }
            report.seekerClearanceMetres = Distance(seekerBeforeClearance, seeker.Position) * .001;
            report.seekerClearanceCompleted = Distance(seeker.Position, seekerClearance) <= 350 && report.seekerClearanceMetres >= 2;
            Require(report.seekerClearanceCompleted, "The seeker did not walk clear of the firing line.");
            Require(match.SubmitPlayerCommand(new StopCommand(1, new[] { seeker.Id })).Accepted, "The seeker's clearance StopCommand was rejected.");
            Step(match);

            var raiderStart = raider.Position;
            double startDistance = Distance(raiderStart, enemy.Position);
            float fraction = (float)Math.Max(.2, (startDistance - 2200) / startDistance);
            var destination = Lerp(raiderStart, enemy.Position, fraction);
            report.movementAccepted = match.SubmitPlayerCommand(new MoveCommand(1, new[] { raider.Id }, destination)).Accepted;
            Require(report.movementAccepted, "The raider's actual MoveCommand was rejected.");
            for (int tick = 0; tick < 500 && Distance(raider.Position, destination) > 450; tick++)
            {
                Step(match); report.locomotionObserved |= drivers[1].CurrentState == "Walk" || drivers[1].CurrentState == "Run";
                yield return null;
            }
            report.raiderMovementMetres = Distance(raiderStart, raider.Position) * .001;
            Require(report.raiderMovementMetres > 1 && report.locomotionObserved && Distance(raider.Position, destination) <= 600, "The raider failed the real movement and skeletal locomotion check.");
            Require(world.Vision == null || world.Vision.IsEntityVisible(1, enemy.Id), "The enemy is not visible through ordinary fog before attacking.");
            report.raiderAttackAccepted = match.SubmitPlayerCommand(new AttackCommand(1, new[] { raider.Id }, enemy.Id)).Accepted;
            Require(report.raiderAttackAccepted, "The raider's actual AttackCommand was rejected.");
            match.Select(new[] { raider.Id });
            for (int tick = 0; tick < 500 && enemy.Health == report.enemyInitialHealth; tick++)
            {
                Step(match); report.raiderAttackObserved |= drivers[1].CurrentState == "Attack";
                Frame(match, Midpoint(raider.Position, enemy.Position), 4.5f);
                yield return null;
            }
            report.enemyAfterRaider = enemy.Health; report.raiderDamage = enemy.Health < report.enemyInitialHealth;
            Require(report.raiderDamage && report.raiderAttackObserved && enemy.Health > 0, "The raider must deal real melee damage and animate Attack while the target survives.");
            Require(match.SubmitPlayerCommand(new StopCommand(1, new[] { raider.Id })).Accepted, "The raider's hold-fire command was rejected.");
            for (int tick = 0; tick < 4; tick++) { Step(match); yield return null; }
            match.SetFeedback("Saqueador de abordaje · Ataque cuerpo a cuerpo. Daño real: " + (report.enemyInitialHealth - enemy.Health) + ".");
            yield return SaveFrame(match, folder, "gameplay-raider-attack.png", report);

            report.gunnerAttackAccepted = match.SubmitPlayerCommand(new AttackCommand(1, new[] { gunner.Id }, enemy.Id)).Accepted;
            Require(report.gunnerAttackAccepted, "The gunner's actual AttackCommand was rejected.");
            match.Select(new[] { gunner.Id });
            // View the firing line from the side opposite the Hearth, so its roof
            // cannot hide the shooter's lower body. Actors retain their command-
            // driven positions; the previous RTS camera is restored afterwards.
            Quaternion previousShotCameraRotation = match.Rig.Camera.transform.rotation;
            Vector3 firingLine = DefinitionLoader.ToWorld(enemy.Position) - DefinitionLoader.ToWorld(gunner.Position);
            float firingHeading = Mathf.Atan2(firingLine.x, firingLine.z) * Mathf.Rad2Deg;
            match.Rig.Camera.transform.rotation = Quaternion.Euler(40, firingHeading + 90, 0);
            bool firingImage = false, firstShotSampled = false;
            int previousGunCooldown = gunner.AttackCooldownTicks;
            for (int tick = 0; tick < 500 && (!report.gunnerDamage || !firingImage); tick++)
            {
                Step(match); report.gunnerAttackObserved |= drivers[2].CurrentState == "Attack";
                if (!firstShotSampled && gunner.AttackCooldownTicks > previousGunCooldown)
                {
                    firstShotSampled = true; report.firstShotTick = world.TickIndex;
                    var muzzle = drivers[2].GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Muzzle");
                    report.muzzlePresent = muzzle != null;
                    if (muzzle != null)
                    {
                        // The FBX marker's local +Z is authored along the barrel.
                        // Measure the first real firing pose, including its first tick
                        // of recoil, independently of the tracer's assigned heading.
                        Vector3 direction = DefinitionLoader.ToWorld(enemy.Position) - muzzle.position;
                        direction.y = 0;
                        if (direction.sqrMagnitude > .00001f)
                        {
                            report.firstShotBarrelAngleDegrees = Vector3.Angle(muzzle.forward, direction);
                            report.barrelAimed = report.firstShotBarrelAngleDegrees <= 30;
                        }
                    }
                }
                previousGunCooldown = gunner.AttackCooldownTicks;
                Frame(match, Midpoint(gunner.Position, enemy.Position), 4);
                var projectile = world.Projectiles.FirstOrDefault(p => p.SourceUnitId == gunner.Id);
                if (projectile != null)
                {
                    var visual = match.View.ProjectileRootFor(projectile.Id);
                    report.pistolProjectileObserved |= visual && visual.gameObject.activeInHierarchy && visual.name == "Pistol shot";
                    if (visual)
                    {
                        var filter = visual.GetComponent<MeshFilter>();
                        report.projectileMeshVerified |= filter && filter.sharedMesh == AlphaSiegeVisuals.Projectile(3);
                        var delta = DefinitionLoader.ToWorld(projectile.Position) - DefinitionLoader.ToWorld(projectile.PreviousPosition);
                        if (delta.sqrMagnitude > .00001f) report.projectileDirectionVerified |= Vector3.Dot(visual.forward, delta.normalized) > .995f;
                        report.projectileOnScreen |= InFrame(match.Rig.Camera, visual.position);
                    }
                }
                var effect = match.GetComponentsInChildren<Transform>(true)
                    .Where(t => t.name.StartsWith("Visible pistol discharge ", StringComparison.Ordinal) && t.gameObject.activeInHierarchy)
                    .FirstOrDefault(t => t.Find("Muzzle flash") && t.Find("Muzzle flash").gameObject.activeInHierarchy);
                if (effect)
                {
                    report.flashObserved = true;
                    var smoke = effect.Find("Powder smoke");
                    report.smokeObserved |= smoke && smoke.gameObject.activeInHierarchy;
                    report.muzzleMarkerErrorMetres = Vector3.Distance(effect.position, drivers[2].MuzzleWorldPosition);
                    report.followsMuzzleMarker |= report.muzzleMarkerErrorMetres < .05f;
                    if (!firingImage && report.pistolProjectileObserved && report.projectileDirectionVerified && report.followsMuzzleMarker)
                    {
                        var beforePosition = effect.position; var beforeScale = effect.Find("Muzzle flash").localScale;
                        long paused = world.TickIndex;
                        for (int frame = 0; frame < 4; frame++) { match.SyncPresentation(1); yield return null; }
                        report.pausedVfxPreserved = paused == world.TickIndex && Vector3.Distance(effect.position, beforePosition) < .00001f &&
                            Vector3.Distance(effect.Find("Muzzle flash").localScale, beforeScale) < .00001f;
                        match.SetFeedback("Corsario de pólvora · Proyectil real, fogonazo y humo desde la boca del arma.");
                        yield return SaveFrame(match, folder, "gameplay-gunner-fire.png", report);
                        firingImage = true;
                        Require(match.SubmitPlayerCommand(new StopCommand(1, new[] { gunner.Id })).Accepted, "The gunner's hold-fire command was rejected.");
                    }
                }
                report.gunnerDamage |= enemy.Health < report.enemyAfterRaider;
                yield return null;
            }
            match.Rig.Camera.transform.rotation = previousShotCameraRotation;
            match.Rig.Constrain();
            report.enemyAfterGunner = enemy.Health;
            Require(report.gunnerAttackObserved && report.pistolProjectileObserved && report.projectileMeshVerified && report.projectileDirectionVerified && report.projectileOnScreen,
                "The native gun attack must show the real correctly oriented pistol projectile in the gameplay camera.");
            Require(firstShotSampled && report.muzzlePresent && report.barrelAimed,
                "The barrel must aim within 30 degrees of the target on its first actual firing tick; measured " + report.firstShotBarrelAngleDegrees.ToString("0.0") + " degrees.");
            Require(report.flashObserved && report.smokeObserved && report.followsMuzzleMarker && report.pausedVfxPreserved && report.gunnerDamage && firingImage,
                "The gun attack must follow its authored muzzle marker, preserve paused flash/smoke and deal real projectile damage.");
            match.SubmitPlayerCommand(new StopCommand(1, new[] { raider.Id, gunner.Id, captain.Id }));

            var seekerStart = seeker.Position;
            Require(world.TryGetResource(205, out var resource) && resource.RemainingAmount > 20 &&
                (world.Vision == null || world.Vision.IsEntityVisible(1, resource.Id)),
                "Original coastal resource 205 must remain visible and available after clearing the firing line.");
            report.resourceId = resource.Id; report.resourceKind = resource.Kind.ToString(); report.resourceInitialAmount = resource.RemainingAmount;
            report.stockBefore = player.Resources.Get(resource.Kind);
            report.gatherAccepted = match.SubmitPlayerCommand(new GatherCommand(1, new[] { seeker.Id }, resource.Id)).Accepted;
            Require(report.gatherAccepted, "The seeker's actual GatherCommand was rejected.");
            match.Select(new[] { seeker.Id });
            Vector3[] workBefore = null;
            var workSkin = drivers[3].GetComponentsInChildren<SkinnedMeshRenderer>(true).OrderByDescending(s => s.sharedMesh.vertexCount).First();
            for (int tick = 0; tick < 700 && !(report.workDeformed && seeker.CarriedAmount > 0); tick++)
            {
                Step(match); Frame(match, Midpoint(seeker.Position, resource.Position), 4.7f);
                if (drivers[3].CurrentState == "Work" && seeker.WorkerTask == WorkerTask.Gathering)
                {
                    report.workObserved = true;
                    var vertices = BakedVertices(workSkin);
                    if (workBefore == null) workBefore = vertices;
                    else
                    {
                        report.workMaxVertexDisplacement = Math.Max(report.workMaxVertexDisplacement, MaxDisplacement(workBefore, vertices));
                        report.workDeformed |= report.workMaxVertexDisplacement > .0001;
                    }
                }
                yield return null;
            }
            report.seekerMovementMetres = Distance(seekerStart, seeker.Position) * .001;
            report.cargo = seeker.CarriedAmount; report.resourceFinalAmount = resource.RemainingAmount;
            Require(report.workObserved && report.workDeformed && report.cargo > 0 && report.resourceFinalAmount < report.resourceInitialAmount,
                "The seeker must reach an original resource, deform in Work and harvest real cargo.");
            var allBones = drivers.SelectMany(d => d.GetComponentInChildren<SkinnedMeshRenderer>(true).bones).Distinct().ToArray();
            var pausedPose = allBones.Select(b => b.localRotation).ToArray(); long pausedTick = world.TickIndex;
            for (int frame = 0; frame < 8; frame++) { match.SyncPresentation(1); yield return null; }
            report.pausePreserved = world.TickIndex == pausedTick && allBones.Select((b, i) => Quaternion.Angle(b.localRotation, pausedPose[i])).All(angle => angle < .001f);
            Require(report.pausePreserved, "Repeated paused presentation advanced a crew pose or the world tick.");
            match.SetFeedback("Buscadora de tesoros · Recolección real de " + report.resourceKind + ": carga " + report.cargo + "/" + seeker.CarryCapacity + ".");
            yield return SaveFrame(match, folder, "gameplay-seeker-work.png", report);

            var hearth = world.Buildings.First(b => b.OwnerId == 1 && b.DefinitionId == "hearth" && b.IsOperational);
            report.returnAccepted = match.SubmitPlayerCommand(new ReturnCargoCommand(1, new[] { seeker.Id }, hearth.Id)).Accepted;
            Require(report.returnAccepted, "The seeker's real ReturnCargoCommand was rejected.");
            for (int tick = 0; tick < 700 && player.Resources.Get(resource.Kind) <= report.stockBefore; tick++)
            {
                Step(match); Frame(match, seeker.Position, 5.5f);
                yield return null;
            }
            report.stockAfter = player.Resources.Get(resource.Kind);
            report.deposited = report.stockAfter - report.stockBefore == report.cargo && seeker.CarriedAmount == 0;
            Require(report.deposited, "The original Hearth did not receive the exact harvested cargo in the real resource stock.");
            match.SubmitPlayerCommand(new StopCommand(1, new[] { seeker.Id })); Step(match);
            Frame(match, Midpoint(seeker.Position, hearth.Position), 6);
            match.SetFeedback("Depósito confirmado · +" + (report.stockAfter - report.stockBefore) + " " + report.resourceKind + " en el almacén. Total: " + report.stockAfter + ".");
            yield return SaveFrame(match, folder, "gameplay-seeker-deposit.png", report);
            for (int i = 0; i < drivers.Length; i++) report.roles[i].state = drivers[i].CurrentState;
            Require(report.hudPixels && report.screenshots.Count == 5, "The five native HUD screenshots are incomplete.");
            if (recorder != null) yield return Showcase(match, captain, raider, gunner, seeker, enemy, resource.Id, hearth.Id);
        }

        // Recording only: the same real commands left running, so every role's gait,
        // attacks, hit reactions and work can be watched in one continuous take.
        private static IEnumerator Showcase(MatchController match, UnitState captain, UnitState raider, UnitState gunner, UnitState seeker, UnitState enemy, int resourceId, int hearthId)
        {
            var world = match.World;
            var fight = Midpoint(raider.Position, enemy.Position);
            Require(match.SubmitPlayerCommand(new AttackCommand(1, new[] { captain.Id, raider.Id, gunner.Id }, enemy.Id)).Accepted, "The showcase AttackCommand was rejected.");
            match.Select(new[] { captain.Id, raider.Id, gunner.Id });
            match.SetFeedback("Toda la tripulación contra el Reedguard.");
            for (int tick = 0, after = 0; tick < 400 && after < 40; tick++)
            {
                Step(match); Frame(match, fight, 4.5f);
                if (!world.TryGetUnit(enemy.Id, out _)) after++;
                yield return null;
            }
            Require(match.SubmitPlayerCommand(new GatherCommand(1, new[] { seeker.Id }, resourceId)).Accepted, "The showcase GatherCommand was rejected.");
            match.Select(new[] { seeker.Id });
            match.SetFeedback("La buscadora vuelve a picar hasta llenar la mochila.");
            for (int tick = 0; tick < 600 && seeker.CarriedAmount < seeker.CarryCapacity; tick++)
            { Step(match); Frame(match, seeker.Position, 4.5f); yield return null; }
            match.SubmitPlayerCommand(new ReturnCargoCommand(1, new[] { seeker.Id }, hearthId));
            match.SetFeedback("Y lleva la carga al Hearth.");
            for (int tick = 0; tick < 600 && seeker.CarriedAmount > 0; tick++)
            { Step(match); Frame(match, seeker.Position, 4.5f); yield return null; }
            for (int tick = 0; tick < 30; tick++) { Step(match); Frame(match, seeker.Position, 4.5f); yield return null; }
        }

        private static SimPoint SeekerClearanceDestination(World world, UnitState seeker, UnitState gunner, UnitState enemy)
        {
            double length = Math.Max(1, Distance(gunner.Position, enemy.Position));
            int offsetX = (int)(-(enemy.Position.Z - gunner.Position.Z) * 2500 / length);
            int offsetZ = (int)((enemy.Position.X - gunner.Position.X) * 2500 / length);
            foreach (int side in new[] { 1, -1 })
            {
                var point = new SimPoint(seeker.Position.X + offsetX * side, seeker.Position.Z + offsetZ * side);
                bool clear = ClearLine(world, seeker.Position, point);
                foreach (int x in new[] { -400, 0, 400 }) foreach (int z in new[] { -400, 0, 400 })
                    clear &= world.IsWalkable(new SimPoint(point.X + x, point.Z + z));
                if (clear && !world.Units.Any(unit => unit.Id != seeker.Id && Distance(unit.Position, point) < 1500)) return point;
            }
            throw new InvalidOperationException("No reachable free point is available beside the original firing line.");
        }

        private static Vector3[] BakedVertices(SkinnedMeshRenderer skin)
        {
            var mesh = new Mesh();
            try { skin.BakeMesh(mesh, false); return mesh.vertices; }
            finally { UnityEngine.Object.Destroy(mesh); }
        }
        private static double MaxDisplacement(Vector3[] a, Vector3[] b)
        {
            Require(a.Length == b.Length, "The animation sample changed topology.");
            float maxSquared = 0;
            for (int i = 0; i < a.Length; i++) maxSquared = Mathf.Max(maxSquared, (a[i] - b[i]).sqrMagnitude);
            return Math.Sqrt(maxSquared);
        }
        private static bool InFrame(Camera camera, Vector3 point)
        {
            Vector3 p = camera.WorldToViewportPoint(point);
            return p.z > 0 && p.x > .05f && p.x < .95f && p.y > .08f && p.y < .90f;
        }
        private static double Distance(SimPoint a, SimPoint b)
        { double x = a.X - b.X, z = a.Z - b.Z; return Math.Sqrt(x * x + z * z); }
        private static SimPoint Lerp(SimPoint a, SimPoint b, float fraction)
        { return new SimPoint(a.X + (int)((b.X - a.X) * fraction), a.Z + (int)((b.Z - a.Z) * fraction)); }
        private static SimPoint Midpoint(SimPoint a, SimPoint b) => new SimPoint((a.X + b.X) / 2, (a.Z + b.Z) / 2);
        private static void Step(MatchController match)
        {
            match.World.Tick();
            if (recorder != null) recorder.Record(match); else match.SyncPresentation(1);
        }
        private static void Frame(MatchController match, SimPoint point, float zoom)
        { match.Rig.MinimumZoom = 4; match.Rig.SetHome(DefinitionLoader.ToWorld(point), zoom); }
        private static IEnumerator SaveFrame(MatchController match, string folder, string file, Report report)
        {
            match.Hud.Refresh(); match.Hud.PrepareOffscreenCapture(match.Rig.Camera);
            yield return new WaitForEndOfFrame();
            bool pixels = PlayerSmoke.Capture(match, Path.Combine(folder, file));
            Require(pixels, "The native gameplay capture is empty: " + file);
            report.hudPixels = true; report.screenshots.Add(file);
            recorder?.Hold(match, 1);
        }
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }

        /// <summary>
        /// Opt-in frame dump for a gameplay video. Each simulation tick is presented at three
        /// interpolation steps, as a 60 Hz display shows it, and the recorded view eases toward
        /// the review's framing instead of jumping with it every tick. The review's own camera,
        /// checks and captures are restored after every recorded frame.
        /// </summary>
        private sealed class FrameRecorder
        {
            public const int FramesPerTick = 3;
            private readonly string folder;
            private RenderTexture target;
            private Texture2D pixels;
            private byte[] last;
            private bool placed;
            private Vector3 position;
            private Quaternion rotation;
            private float size;
            public int Count { get; private set; }

            public FrameRecorder(string folder) { this.folder = folder; Directory.CreateDirectory(folder); }

            public void Record(MatchController match)
            {
                for (int step = 1; step <= FramesPerTick; step++)
                {
                    match.SyncPresentation(step / (float)FramesPerTick);
                    Render(match, (float)World.TickSeconds / FramesPerTick);
                }
            }

            public void Hold(MatchController match, float seconds)
            {
                Render(match, 0);
                for (int i = 1; i < Mathf.RoundToInt(seconds / (float)World.TickSeconds * FramesPerTick); i++) Write(last);
            }

            private void Render(MatchController match, float seconds)
            {
                var camera = match.Rig.Camera; var view = camera.transform;
                Vector3 goal = view.position; float goalSize = camera.orthographicSize;
                // Cut on a deliberate change of view; otherwise ease with a 0.35 s time constant.
                if (!placed || Quaternion.Angle(rotation, view.rotation) > .5f || Vector3.Distance(position, goal) > 8)
                { position = goal; size = goalSize; rotation = view.rotation; placed = true; }
                else
                {
                    float ease = 1 - Mathf.Exp(-seconds / .35f);
                    position = Vector3.Lerp(position, goal, ease); size = Mathf.Lerp(size, goalSize, ease);
                }
                view.position = position; camera.orthographicSize = size;
                if (target == null)
                {
                    target = new RenderTexture(Screen.width, Screen.height, 24); target.Create();
                    pixels = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
                }
                Canvas.ForceUpdateCanvases();
                UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera, new UnityEngine.Rendering.RenderPipeline.StandardRequest { destination = target });
                var previous = RenderTexture.active; RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0, false);
                RenderTexture.active = previous;
                view.position = goal; camera.orthographicSize = goalSize;
                last = pixels.EncodeToJPG(90);
                Write(last);
            }

            private void Write(byte[] frame) => File.WriteAllBytes(Path.Combine(folder, "frame-" + (Count++).ToString("D5") + ".jpg"), frame);
        }
    }
}
