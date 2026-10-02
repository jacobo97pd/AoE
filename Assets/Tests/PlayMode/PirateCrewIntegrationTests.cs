using System;
using System.Collections;
using System.Linq;
using Emberfield.Presentation;
using Emberfield.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Emberfield.Tests.PlayMode
{
    public sealed class PirateCrewIntegrationTests
    {
        private GameObject root;
        private WorldView view;
        [UnityTearDown] public IEnumerator Cleanup()
        {
            view?.Dispose(); view = null;
            if (root != null) Object.Destroy(root);
            yield return null;
        }
        private static GameDefinition Rules()
        {
            var rules = JsonUtility.FromJson<GameDefinition>(Resources.Load<TextAsset>("Definitions/greybox").text);
            rules.StartingResources = new ResourceAmount(1000, 1000, 1000, 1000);
            rules.BasePopulationCapacity = 50;
            return rules;
        }
        private static MapDefinition Map(string unitId, int distance = 16000, string realm = ContentRealms.Naval) => new MapDefinition
        {
            Id = "pirate_crew_test", WidthCells = 48, HeightCells = 32,
            RealmId = realm,
            OfflineMatch = new OfflineMatchDefinition { Enabled = true },
            PlayerFactions = new[] {
                new PlayerFactionDefinition { PlayerId = 1, FactionId = realm == ContentRealms.Naval ? "pirates" : realm == ContentRealms.Fantasy ? "ashen" : "aven" },
                new PlayerFactionDefinition { PlayerId = 2, FactionId = realm == ContentRealms.Naval ? "pirates" : realm == ContentRealms.Fantasy ? "verdant" : "serevin" }
            },
            UnitSpawns = new[] {
                new UnitSpawnDefinition { Id = 1, OwnerId = 1, DefinitionId = unitId, Position = new SimPoint(10500, 10500) },
                new UnitSpawnDefinition { Id = 2, OwnerId = 2, DefinitionId = "reedguard", Position = new SimPoint(10500 + distance, 10500) }
            },
            BuildingSpawns = new[] {
                new BuildingSpawnDefinition { Id = 100, OwnerId = 1, DefinitionId = "hearth", Position = new SimPoint(6000, 6000) },
                new BuildingSpawnDefinition { Id = 101, OwnerId = 1, DefinitionId = "muster_hall", Position = new SimPoint(14500, 5500) },
                new BuildingSpawnDefinition { Id = 102, OwnerId = 2, DefinitionId = "hearth", Position = new SimPoint(34000, 24000) }
            },
            ResourceSpawns = new[] { new ResourceSpawnDefinition { Id = 200, DefinitionId = "wood_source", Position = new SimPoint(13500, 14500) } }
        };
        private static void Accepted(CommandResult result) => Assert.That(result.Accepted, Is.True, result.Message);
        private CorsairAnimationDriver CreateView(World world)
        {
            root = new GameObject("Crew integration");
            var camera = new GameObject("Camera", typeof(Camera)).GetComponent<Camera>(); camera.transform.SetParent(root.transform);
            view = new WorldView(world, root.transform, camera);
            return view.RootFor(1).GetComponentInChildren<CorsairAnimationDriver>();
        }

        [TestCase("boarding_raider", 101)] [TestCase("gunpowder_corsair", 101)] [TestCase("treasure_seeker", 100)]
        public void CrewBelongsToNavalPiratesAndCanTrainSeveralWithOrdinaryResources(string id, int producer)
        {
            var rules = Rules(); var definition = rules.Units.Single(u => u.Id == id);
            Assert.That(definition.RequiredRealmId, Is.EqualTo(ContentRealms.Naval));
            Assert.That(definition.RequiredFactionId, Is.EqualTo("pirates"));
            Assert.That(definition.MaxAlivePerPlayer, Is.Zero);
            var world = new World(rules, Map("reedguard")); world.TryGetPlayer(1, out var player);
            var initial = player.Resources;
            Accepted(world.Submit(new TrainCommand(1, producer, id)));
            Accepted(world.Submit(new TrainCommand(1, producer, id)));
            Assert.That(player.Resources.Food, Is.EqualTo(initial.Food - 2 * definition.Cost.Food));
            Assert.That(player.Resources.Metal, Is.EqualTo(initial.Metal - 2 * definition.Cost.Metal));
            Assert.That(player.PopulationReserved, Is.EqualTo(2 * definition.PopulationCost));
            for (int i = 0; i < definition.TrainTicks * 3; i++) world.Tick();
            Assert.That(world.Units.Count(u => u.OwnerId == 1 && u.DefinitionId == id), Is.EqualTo(2));
            Assert.That(ImportedCharacterVisuals.CanUse(world, world.Units.First(u => u.OwnerId == 1 && u.DefinitionId == id)), Is.True);
            foreach (string otherRealm in new[] { ContentRealms.Historical, ContentRealms.Fantasy })
            {
                var otherWorld = new World(Rules(), Map("tender", realm: otherRealm));
                Assert.That(otherWorld.Submit(new TrainCommand(1, producer, id)).Reason, Is.EqualTo(CommandRejection.WrongFaction), otherRealm);
                Assert.Throws<ArgumentException>(() => new World(Rules(), Map(id, realm: otherRealm)), otherRealm);
            }
            world.TryGetUnit(1, out var ordinaryInfantry);
            Assert.That(ImportedCharacterVisuals.CanUse(world, ordinaryInfantry), Is.False);
        }

        [TestCase("boarding_raider", ContentRealms.Historical)] [TestCase("gunpowder_corsair", ContentRealms.Historical)] [TestCase("treasure_seeker", ContentRealms.Historical)]
        [TestCase("boarding_raider", ContentRealms.Fantasy)] [TestCase("gunpowder_corsair", ContentRealms.Fantasy)] [TestCase("treasure_seeker", ContentRealms.Fantasy)]
        public void OtherRealmSnapshotCannotIntroducePirateCrew(string id, string realm)
        {
            var world = new World(Rules(), Map("tender", realm: realm));
            var replica = World.CreateNetworkReplica(world.Definition, world.Map, NetworkObservation.Export(world, 1, 1));
            var forged = NetworkObservation.Export(world, 1, 2);
            forged.Units.First(u => u.Id == 1).DefinitionId = id;
            Assert.Throws<ArgumentException>(() => replica.ApplyNetworkSnapshot(forged));
            Assert.That(replica.NetworkSnapshotSequence, Is.EqualTo(1));
        }

        [Test] public void PirateWorkerRosterRejectsForgedTenderAndKeepsAiRecruitmentOnSeekers()
        {
            Assert.Throws<ArgumentException>(() => new World(Rules(), Map("tender")), "Authored starts cannot bypass the pirate worker roster.");
            var world = new World(Rules(), Map(ImportedCharacterVisuals.TreasureSeekerId));
            Assert.That(world.Submit(new TrainCommand(1, 100, "tender")).Reason, Is.EqualTo(CommandRejection.WrongFaction));
            var replica = World.CreateNetworkReplica(world.Definition, world.Map, NetworkObservation.Export(world, 1, 1));
            var forged = NetworkObservation.Export(world, 1, 2);
            forged.Units.First(u => u.Id == 1).DefinitionId = "tender";
            Assert.Throws<ArgumentException>(() => replica.ApplyNetworkSnapshot(forged));
            Assert.That(replica.NetworkSnapshotSequence, Is.EqualTo(1));
            replica.TryGetUnit(1, out var retained);
            Assert.That(retained.DefinitionId, Is.EqualTo(ImportedCharacterVisuals.TreasureSeekerId));

            int workersQueued = 0;
            var ai = new OfflineAi(world, 1);
            ai.CommandIssued += (command, result) =>
            {
                if (!(command is TrainCommand train) || !world.Definition.Units.Single(unit => unit.Id == train.UnitDefinitionId).IsWorker) return;
                Assert.That(result.Accepted, Is.True, result.Message);
                Assert.That(train.UnitDefinitionId, Is.EqualTo(ImportedCharacterVisuals.TreasureSeekerId));
                workersQueued++;
            };
            for (int tick = 0; tick < 40; tick++) { ai.Tick(); world.Tick(); }
            Assert.That(workersQueued, Is.GreaterThan(0), "The AI must grow the pirate economy rather than stall on the forbidden cheaper worker.");
        }

        [Test] public void CrewReviewShowsFourDistinctCharactersAndKeepsNormalStartsUnchanged()
        {
            var world = PirateCrewScenario.CreateReviewWorld();
            Assert.That(world.Map.RealmId, Is.EqualTo(ContentRealms.Naval));
            // The review crew is the local pirates'; their offline rival is the next fleet.
            Assert.That(world.Map.PlayerFactions.Select(player => player.FactionId), Is.EqualTo(new[] { "pirates", ContentRealms.OpponentFaction("pirates") }));
            var expected = ImportedCharacterVisuals.Descriptors.Select(d => d.DefinitionId).ToArray();
            CollectionAssert.AreEquivalent(expected, world.Units.Where(u => u.OwnerId == 1).Select(u => u.DefinitionId));
            world.TryGetPlayer(1, out var player);
            Assert.That(player.PopulationUsed, Is.EqualTo(6));
            Assert.That(player.PopulationUsed, Is.LessThanOrEqualTo(player.PopulationCapacity));
            var normal = DefinitionLoader.CreateOfflineWorld("aven", VictoryMode.Conquest, "sapphire_coast");
            Assert.That(normal.Units.Any(u => expected.Contains(u.DefinitionId)), Is.False);
        }

        [TestCase("boarding_raider")] [TestCase("gunpowder_corsair")] [TestCase("treasure_seeker")]
        public void ImportedCrewUsesItsOwnSkinnedLodsAndMovingAnimation(string id)
        {
            Assert.That(ImportedCharacterVisuals.TryGetDescriptor(id, out var descriptor), Is.True);
            root = Object.Instantiate(Resources.Load<GameObject>(descriptor.PrefabResourcePath));
            var driver = root.GetComponent<CorsairAnimationDriver>() ?? root.AddComponent<CorsairAnimationDriver>();
            Assert.That(driver.HasValidRig, Is.True);
            Assert.That(root.GetComponentInChildren<LODGroup>().GetLODs().Length, Is.EqualTo(3));
            var renderer = root.GetComponentInChildren<SkinnedMeshRenderer>();
            var sampled = new Mesh();
            try
            {
                var states = id == ImportedCharacterVisuals.TreasureSeekerId ? new[] { "Idle", "Walk", "Run", "Attack", "Hit", "Death", "Work" } :
                    id == ImportedCharacterVisuals.GunpowderCorsairId ? new[] { "Idle", "Walk", "Run", "Attack", "Hit", "Death", "Aim" } :
                    new[] { "Idle", "Walk", "Run", "Attack", "Hit", "Death" };
                // Locomotion playback follows the ground speed measured on the clips.
                Assert.That(driver.WalkMetresPerSecond, Is.InRange(.8f, 2.2f), id + " walk ground speed");
                // The supplied Meshy biped is an authored sprint: measured at the
                // imported 2.35m scale its support foot travels about 7.26m/s.
                Assert.That(driver.RunMetresPerSecond, id == ImportedCharacterVisuals.BoardingRaiderId ? Is.InRange(6f, 8f) : Is.InRange(2.4f, 4.6f), id + " run ground speed");
                if (id == ImportedCharacterVisuals.BoardingRaiderId)
                {
                    var clips = driver.Animator.runtimeAnimatorController.animationClips;
                    var run = clips.Single(c => c.name == "Corsair_Run");
                    var death = clips.Single(c => c.name == "Corsair_Death");
                    Assert.That(run.length, Is.EqualTo(15f / 24f).Within(.002f), "Preserve the supplied Running timing");
                    Assert.That(death.length, Is.EqualTo(83f / 24f).Within(.002f), "Preserve the supplied abdominal fall timing");
                    Assert.That(run.isLooping, Is.True);
                    Assert.That(death.isLooping, Is.False);
                }
                foreach (string state in states)
                {
                    Assert.That(driver.PlayPreview(state), Is.True, id + " missing " + state);
                    driver.Animator.Update(.15f); renderer.BakeMesh(sampled); var before = sampled.vertices;
                    driver.Animator.Update(.25f); renderer.BakeMesh(sampled); var after = sampled.vertices;
                    float difference = 0;
                    for (int i = 0; i < before.Length; i += Math.Max(1, before.Length / 1000)) difference += (before[i] - after[i]).sqrMagnitude;
                    Assert.That(difference, Is.GreaterThan(.000001f), id + " " + state + " must deform the actual mesh.");
                }
            }
            finally { Object.Destroy(sampled); }
        }

        [Test] public void PistolUsesTravelingBulletAndVisualDischargeBeforeOneDamageApplication()
        {
            var world = new World(Rules(), Map(ImportedCharacterVisuals.GunpowderCorsairId, 4000));
            Accepted(world.Submit(new StopCommand(2, new[] { 2 })));
            var driver = CreateView(world); world.TryGetUnit(2, out var enemy);
            int before = enemy.Health;
            Accepted(world.Submit(new AttackCommand(1, new[] { 1 }, 2)));
            world.Tick(); view.Sync(1, new[] { 1 });
            Assert.That(driver.CurrentState, Is.EqualTo("Attack"));
            Assert.That(world.Projectiles.Count, Is.EqualTo(1));
            Assert.That(enemy.Health, Is.EqualTo(before), "A pistol projectile must travel before applying damage.");
            var bullet = view.ProjectileRootFor(world.Projectiles[0].Id);
            Assert.That(bullet.name, Is.EqualTo("Pistol shot"));
            Assert.That(root.GetComponentsInChildren<Transform>().Any(t => t.name == "Muzzle flash" && t.gameObject.activeInHierarchy), Is.True);
            for (int i = 0; i < 10; i++) { world.Tick(); view.Sync(1, new[] { 1 }); }
            Assert.That(world.Projectiles.Count, Is.Zero);
            Assert.That(enemy.Health, Is.EqualTo(before - (18 - enemy.Armor)));
            // Between shots the corsair keeps the pistol levelled at the target.
            for (int i = 0; i < 6; i++) { world.Tick(); view.Sync(1, new[] { 1 }); }
            Assert.That(driver.CurrentState, Is.EqualTo("Aim"));
            Assert.That(enemy.Health, Is.EqualTo(before - (18 - enemy.Armor)));
            var position = driver.Animator.transform.position;
            view.Sync(1, new[] { 1 }); view.Sync(1, new[] { 1 });
            Assert.That(driver.Animator.transform.position, Is.EqualTo(position));
            Assert.That(enemy.Health, Is.EqualTo(before - (18 - enemy.Armor)));
        }

        [Test] public void SeekerHarvestsCarriesAndDepositsThroughTheOrdinaryWorkerSystem()
        {
            var world = new World(Rules(), Map(ImportedCharacterVisuals.TreasureSeekerId));
            world.TryGetPlayer(1, out var player); world.TryGetUnit(1, out var seeker);
            int initial = player.Resources.Wood;
            var driver = CreateView(world);
            Accepted(world.Submit(new GatherCommand(1, new[] { 1 }, 200)));
            bool work = false, carried = false;
            for (int i = 0; i < 700 && player.Resources.Wood == initial; i++)
            {
                world.Tick(); view.Sync(1, new[] { 1 });
                carried |= seeker.CarriedAmount > 0;
                work |= driver.CurrentState == "Work";
                if (seeker.CarriedAmount > 0) Assert.That(seeker.CarriedAmount, Is.LessThanOrEqualTo(seeker.CarryCapacity));
            }
            Assert.That(work, Is.True); Assert.That(carried, Is.True);
            Assert.That(player.Resources.Wood, Is.GreaterThan(initial));
            var ordinary = new World(Rules(), Map("tender", realm: ContentRealms.Historical));
            var exploration = new World(Rules(), Map(ImportedCharacterVisuals.TreasureSeekerId));
            var extraGround = new SimPoint(10500, 21500);
            Assert.That(exploration.Vision.IsVisible(1, extraGround), Is.True);
            Assert.That(ordinary.Vision.IsVisible(1, extraGround), Is.False, "The Seeker must reveal ground beyond the ordinary worker's view.");
            Assert.That(world.Submit(new AttackCommand(1, new[] { 1 }, 2)).Accepted, Is.False, "This worker is unarmed.");
            Accepted(world.Submit(new BuildCommand(1, new[] { 1 }, "shelter", new SimPoint(12000, 8000))));
        }

        [Test] public void HiddenPistolShooterCannotCreateAnEnemyModelOrMuzzleFlash()
        {
            var rules = Rules(); rules.Units.Single(u => u.Id == ImportedCharacterVisuals.TreasureSeekerId).VisionCells = 1;
            var map = Map(ImportedCharacterVisuals.TreasureSeekerId, 4000);
            map.UnitSpawns[1].DefinitionId = ImportedCharacterVisuals.GunpowderCorsairId;
            map.BuildingSpawns = map.BuildingSpawns.Where(b => b.Id != 101).ToArray();
            map.BuildingSpawns[0].Position = new SimPoint(6000, 26000);
            var world = new World(rules, map);
            Assert.That(world.Vision.IsEntityVisible(1, 2), Is.False);
            CreateView(world);
            Accepted(world.Submit(new AttackCommand(2, new[] { 2 }, 1)));
            world.Tick(); view.Sync(1, new[] { 1 });
            Assert.That(world.Projectiles.Count, Is.GreaterThan(0));
            Assert.That(view.RootFor(2), Is.Null);
            Assert.That(root.GetComponentsInChildren<Transform>(true).Any(t => t.name == "Muzzle flash"), Is.False);
            Assert.That(view.ProjectileCount, Is.Zero, "The projectile is still beyond local vision on its first tick.");
        }
    }
}
