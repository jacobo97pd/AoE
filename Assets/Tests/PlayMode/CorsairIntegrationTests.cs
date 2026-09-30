using System.Collections;
using System.Linq;
using Emberfield.Presentation;
using Emberfield.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Emberfield.Tests.PlayMode
{
    public sealed class CorsairIntegrationTests
    {
        private GameObject root;
        private WorldView view;
        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            view?.Dispose(); view = null;
            if (root != null) Object.Destroy(root);
            yield return null;
        }
        private static GameDefinition Rules() => JsonUtility.FromJson<GameDefinition>(Resources.Load<TextAsset>("Definitions/greybox").text);
        private static MapDefinition Map(int enemyX = 28500) => new MapDefinition
        {
            Id = "corsair_integration", RealmId = ContentRealms.Naval, WidthCells = 48, HeightCells = 48,
            PlayerFactions = new[] {
                new PlayerFactionDefinition { PlayerId = 1, FactionId = "pirates" },
                new PlayerFactionDefinition { PlayerId = 2, FactionId = "pirates" }
            },
            UnitSpawns = new[] {
                new UnitSpawnDefinition { Id = 1, OwnerId = 1, DefinitionId = "crimson_corsair", Position = new SimPoint(10500,10500) },
                new UnitSpawnDefinition { Id = 2, OwnerId = 2, DefinitionId = "reedguard", Position = new SimPoint(enemyX,10500) }
            }
        };
        private CorsairAnimationDriver CreateView(World world)
        {
            root = new GameObject("Corsair game integration");
            var camera = new GameObject("Camera", typeof(Camera)).GetComponent<Camera>(); camera.transform.SetParent(root.transform);
            view = new WorldView(world, root.transform, camera);
            var driver = view.RootFor(1).GetComponentInChildren<CorsairAnimationDriver>();
            Assert.That(driver, Is.Not.Null, "The real WorldView must instantiate the imported hero prefab.");
            return driver;
        }

        [Test]
        public void ShippedHeroBelongsToNavalPiratesAndKeepsItsHearthRecruitment()
        {
            var rules = Rules();
            var hero = rules.Units.Single(u => u.Id == "crimson_corsair");
            Assert.That(hero.RequiredRealmId, Is.EqualTo(ContentRealms.Naval));
            Assert.That(hero.RequiredFactionId, Is.EqualTo("pirates"));
            Assert.That(hero.MaxAlivePerPlayer, Is.EqualTo(1));
            Assert.That(hero.Cost.Food + hero.Cost.Metal, Is.GreaterThan(0));
            Assert.That(hero.IsWorker, Is.False);
            Assert.That(rules.Buildings.Single(b => b.Id == "hearth").TrainableUnitIds, Does.Contain(hero.Id));
            var world = new World(rules, Map());
            world.TryGetUnit(1, out var corsair); world.TryGetUnit(2, out var infantry);
            Assert.That(ImportedCharacterVisuals.CanUse(world, corsair), Is.True);
            Assert.That(ImportedCharacterVisuals.CanUse(world, infantry), Is.False);
            var demo = CorsairHeroScenario.CreateReviewWorld();
            Assert.That(demo.Map.RealmId, Is.EqualTo(ContentRealms.Naval));
            Assert.That(demo.Map.BiomeId, Is.EqualTo("caribbean"));
            Assert.That(demo.Units.Count(u => u.DefinitionId == hero.Id), Is.EqualTo(1));
            Assert.That(demo.TryGetPlayer(MatchController.LocalPlayer, out var localPlayer), Is.True);
            Assert.That(localPlayer.PopulationUsed + localPlayer.PopulationReserved, Is.LessThanOrEqualTo(localPlayer.PopulationCapacity),
                "The playable review must start within its population capacity.");
            Assert.That(DefinitionLoader.CreateOfflineWorld("aven", VictoryMode.Conquest, "sapphire_coast").Units.Any(u => u.DefinitionId == hero.Id), Is.False,
                "Review hero spawn must not change ordinary skirmish starts.");
        }

        [UnityTest]
        public IEnumerator ImportedPrefabHasSixAnimationsThatActuallyDeformItsSkinnedMesh()
        {
            var prefab = Resources.Load<GameObject>(ImportedCharacterVisuals.PrefabResourcePath);
            Assert.That(prefab, Is.Not.Null);
            root = Object.Instantiate(prefab);
            var driver = root.GetComponent<CorsairAnimationDriver>() ?? root.AddComponent<CorsairAnimationDriver>();
            yield return null;
            var renderer = root.GetComponentInChildren<SkinnedMeshRenderer>();
            Assert.That(renderer, Is.Not.Null); Assert.That(renderer.bones.Length, Is.GreaterThan(10));
            Assert.That(driver.Animator.applyRootMotion, Is.False);
            var sampled = new Mesh();
            try
            {
                foreach (string state in new[] { "Idle", "Walk", "Run", "Attack", "Hit", "Death" })
                {
                    Assert.That(driver.PlayPreview(state), Is.True, "Missing Animator state " + state);
                    driver.Animator.Update(.15f);
                    renderer.BakeMesh(sampled); var before = sampled.vertices;
                    driver.Animator.Update(Mathf.Max(.12f, driver.Duration(state) * .25f));
                    renderer.BakeMesh(sampled); var after = sampled.vertices;
                    Assert.That(after.Length, Is.EqualTo(before.Length));
                    float movement = 0;
                    for (int i = 0; i < before.Length; i++) movement = Mathf.Max(movement, (before[i] - after[i]).sqrMagnitude);
                    Assert.That(movement, Is.GreaterThan(.000001f), state + " must deform vertices, not just translate the GameObject.");
                }
            }
            finally { Object.Destroy(sampled); }
        }

        [UnityTest]
        public IEnumerator RealMovementAndAttacksDriveRigWhileRepeatedPresentationCannotAdvanceIt()
        {
            var world = new World(Rules(), Map());
            var driver = CreateView(world); yield return null;
            Assert.That(world.Submit(new StopCommand(2, new[] { 2 })).Accepted, Is.True);
            Assert.That(world.Submit(new MoveCommand(1, new[] { 1 }, new SimPoint(18500,10500))).Accepted, Is.True);
            for (int tick = 0; tick < 4; tick++) { world.Tick(); view.Sync(1, new[] { 1 }); }
            Assert.That(driver.CurrentState, Is.EqualTo("Run"));
            world.TryGetUnit(1, out var hero); world.TryGetPlayer(1, out var player);
            var position = hero.Position; var stock = player.Resources; int health = hero.Health; long time = world.TickIndex;
            var bones = driver.GetComponentInChildren<SkinnedMeshRenderer>().bones;
            var rotations = bones.Select(b => b.localRotation).ToArray();
            for (int i = 0; i < 25; i++) view.Sync(1, new[] { 1 });
            Assert.That(hero.Position, Is.EqualTo(position)); Assert.That(hero.Health, Is.EqualTo(health));
            Assert.That(player.Resources, Is.EqualTo(stock)); Assert.That(world.TickIndex, Is.EqualTo(time));
            for (int i = 0; i < bones.Length; i++) Assert.That(Quaternion.Angle(rotations[i], bones[i].localRotation), Is.LessThan(.001f));
            Assert.That(view.RootFor(1).Find("Model").localPosition, Is.EqualTo(Vector3.zero), "Old placeholder bobbing must not move the imported root.");
            Assert.That(world.Submit(new AttackCommand(1, new[] { 1 }, 2)).Accepted, Is.True);
            view.Sync(1, new[] { 1 });
            Assert.That(driver.CurrentState, Is.Not.EqualTo("Attack"), "Chasing a distant target must not swing yet.");
            bool attacked = false;
            for (int tick = 0; tick < 200; tick++)
            {
                world.Tick(); view.Sync(1, new[] { 1 });
                if (driver.CurrentState == "Attack") { attacked = true; break; }
            }
            Assert.That(attacked, Is.True, "A real cooldown reset must play Attack.");
            world.TryGetUnit(2, out var target); Assert.That(target.Health, Is.LessThan(target.MaxHealth));
        }

        [UnityTest]
        public IEnumerator LocalDeathDetachesOnlyVisualCorpseAndDisposingViewRemovesIt()
        {
            var rules = Rules(); rules.Units.Single(u => u.Id == "reedguard").Attack.Damage = 1000;
            var world = new World(rules, Map(enemyX:11600));
            var driver = CreateView(world); yield return null;
            Assert.That(world.Submit(new StopCommand(1, new[] { 1 })).Accepted, Is.True);
            Assert.That(world.Submit(new AttackCommand(2, new[] { 2 }, 1)).Accepted, Is.True);
            world.Tick(); view.Sync(1, System.Array.Empty<int>());
            Assert.That(world.TryGetUnit(1, out _), Is.False); Assert.That(view.RootFor(1), Is.Null);
            Assert.That(driver.IsDying, Is.True); Assert.That(driver.CurrentState, Is.EqualTo("Death"));
            Assert.That(world.ValidateUnitRecruitment(1, "crimson_corsair").Accepted, Is.True, "Cosmetic corpse cannot reserve a dead hero's gameplay slot.");
            view.Dispose(); view = null; yield return null;
            Assert.That(driver == null, Is.True);
        }
    }
}
