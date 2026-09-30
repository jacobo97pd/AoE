using System;
using System.Collections;
using Emberfield.Presentation;
using Emberfield.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Emberfield.Tests.PlayMode
{
    public sealed class FogPresentationRegressionTests
    {
        private GameObject root;
        private World world;
        private WorldView view;
        private Camera camera;

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            view?.Dispose();
            if (root != null) UnityEngine.Object.Destroy(root);
            yield return null;
        }

        [UnityTest]
        public IEnumerator RevealingAHiddenCompletionDoesNotEmitANewCompletionEffect()
        {
            Create(false);
            yield return null;
            MoveScout(new SimPoint(12500, 9500));
            var build = world.Submit(new BuildCommand(2, new[] { 2 }, "hut", new SimPoint(14500, 8500)));
            Assert.IsTrue(build.Accepted, build.Message);
            view.Sync(1, Array.Empty<int>());
            Assert.IsTrue(world.TryGetBuilding(build.EntityId, out var building));
            Assert.IsFalse(building.IsComplete);
            Assert.IsTrue(view.RootFor(building.Id).gameObject.activeSelf);

            MoveScout(new SimPoint(4500, 8500));
            Assert.IsFalse(world.Vision.IsEntityVisible(1, building.Id));
            Assert.IsFalse(view.RootFor(building.Id).gameObject.activeSelf);
            Assert.IsFalse(building.IsComplete, "The building must still be unfinished when vision is lost.");
            Until(() => building.IsComplete, 80);
            Assert.IsFalse(view.RootFor(building.Id).gameObject.activeSelf);
            int particlesBeforeReveal = view.Feedback.EmittedParticles;

            MoveScout(new SimPoint(12500, 9500));
            Assert.IsTrue(world.Vision.IsEntityVisible(1, building.Id));
            Assert.IsTrue(view.RootFor(building.Id).gameObject.activeSelf);
            Assert.AreEqual(DefinitionLoader.ToWorld(building.Position), view.RootFor(building.Id).position);
            Assert.AreEqual(particlesBeforeReveal, view.Feedback.EmittedParticles,
                "Re-observing an already completed building must not replay its hidden completion as a fresh event.");
        }

        [UnityTest]
        public IEnumerator FirstRevealOfADepletedResourceNeverCreatesAnActiveModelOrPickTarget()
        {
            Create(true);
            yield return null;
            Assert.IsFalse(world.Vision.IsEntityVisible(1, 201));
            Assert.IsNull(view.RootFor(201), "The local player has never seen this source.");
            var gather = world.Submit(new GatherCommand(2, new[] { 2 }, 201));
            Assert.IsTrue(gather.Accepted, gather.Message);
            Assert.IsTrue(world.TryGetResource(201, out var resource));
            Until(() => resource.RemainingAmount == 0, 80);
            Assert.IsFalse(world.Vision.IsEntityVisible(1, resource.Id));
            Assert.IsTrue(world.TryGetUnit(2, out var gatherer));
            Assert.Greater(gatherer.CarriedAmount, 0, "Depletion must result from real harvesting.");

            var move = world.Submit(new MoveCommand(1, new[] { 1 }, new SimPoint(12500, 9500)));
            Assert.IsTrue(move.Accepted, move.Message);
            Until(() => world.Vision.IsEntityVisible(1, resource.Id), 80);
            // Until synchronizes on the first revealed tick, so a one-frame ghost would fail here.
            var resourceView = view.RootFor(resource.Id);
            Assert.IsTrue(resourceView == null || !resourceView.gameObject.activeSelf,
                "An exhausted source must be absent on its first visible presentation update.");
            var screen = camera.WorldToScreenPoint(DefinitionLoader.ToWorld(resource.Position) + Vector3.up * .6f);
            Assert.AreNotEqual(resource.Id, view.Pick(camera, screen, 1));
        }

        [UnityTest]
        public IEnumerator EnemyLeavingSightAndDyingBetweenSyncsDoesNotEmitAStaleDefeatCue()
        {
            Create(false, true);
            yield return null;
            Assert.IsTrue(world.Submit(new StopCommand(2, new[] { 2 })).Accepted);
            MoveScout(new SimPoint(12500, 9500));
            Assert.IsTrue(world.TryGetUnit(2, out var enemy));
            Assert.IsTrue(world.Submit(new StopCommand(2, new[] { 2 })).Accepted);
            Assert.IsTrue(world.Submit(new AttackCommand(1, new[] { 1 }, 2)).Accepted);
            world.Tick();
            view.Sync(1, Array.Empty<int>());
            Assert.Greater(world.Projectiles.Count, 0, "A real projectile must already be in flight.");
            Assert.IsTrue(view.RootFor(2).gameObject.activeSelf);
            var lastObservedPoint = DefinitionLoader.ToSimulation(view.RootFor(2).position);
            int particles = view.Feedback.EmittedParticles;
            int sounds = view.Feedback.PlayedSounds;
            Assert.IsTrue(world.Submit(new MoveCommand(2, new[] { 2 }, new SimPoint(20500, 7500))).Accepted);
            bool hiddenBeforeDeath = false;
            for (int tick = 0; tick < 80 && world.TryGetUnit(2, out _); tick++)
            {
                world.Tick();
                if (world.TryGetUnit(2, out _) && !world.Vision.IsEntityVisible(1, 2)) hiddenBeforeDeath = true;
            }
            Assert.IsTrue(hiddenBeforeDeath);
            Assert.IsFalse(world.TryGetUnit(2, out _), "The launched projectile must cause the hidden death.");
            Assert.IsFalse(world.Vision.IsVisible(1, enemy.Position));
            Assert.IsTrue(world.Vision.IsVisible(1, lastObservedPoint), "The stale render position is misleadingly still visible.");
            view.Sync(1, Array.Empty<int>());
            Assert.IsNull(view.RootFor(2));
            Assert.AreEqual(particles, view.Feedback.EmittedParticles);
            Assert.AreEqual(sounds, view.Feedback.PlayedSounds);
        }

        private void Create(bool includeResource, bool projectileCombat = false)
        {
            var rules = new GameDefinition
            {
                StartingResources = new ResourceAmount(100, 100, 100, 100),
                Units = new[] { new UnitDefinition
                {
                    Id = "worker", IsWorker = true, Tags = CombatTags.Worker, VisionCells = 3,
                    MoveSpeedMillimetresPerSecond = 20000, CarryCapacity = 10, GatherAmount = 3, GatherIntervalTicks = 1,
                    Attack = projectileCombat ? new AttackDefinition { Damage = 1000, RangeMillimetres = 5000,
                        AcquireRangeMillimetres = 5000, CooldownTicks = 1000, ProjectileSpeedMillimetresPerSecond = 10000,
                        ProjectileLifetimeTicks = 100 } : new AttackDefinition()
                } },
                Buildings = new[]
                {
                    new BuildingDefinition { Id = "hearth", WidthCells = 1, DepthCells = 1, CanDropOff = true, VisionCells = 2 },
                    new BuildingDefinition { Id = "hut", WidthCells = 1, DepthCells = 1, MaxHealth = 100,
                        BuildTicks = 40, Cost = new ResourceAmount(0, 10, 0, 0), VisionCells = 3 }
                },
                Resources = new[] { new ResourceDefinition { Id = "wood", Kind = ResourceKind.Wood, InitialAmount = 3 } }
            };
            var map = new MapDefinition
            {
                // This enables the real slice terrain and feedback without altering shipped assets.
                Id = "amber_crossing", WidthCells = 24, HeightCells = 16,
                OfflineMatch = new OfflineMatchDefinition { Enabled = true, VisionUnitCells = 3, VisionBuildingCells = 2 },
                UnitSpawns = new[]
                {
                    new UnitSpawnDefinition { Id = 1, OwnerId = 1, DefinitionId = "worker", Position = new SimPoint(4500, 8500) },
                    new UnitSpawnDefinition { Id = 2, OwnerId = 2, DefinitionId = "worker", Position = new SimPoint(13500, 7500) }
                },
                BuildingSpawns = new[]
                {
                    new BuildingSpawnDefinition { Id = 100, OwnerId = 1, DefinitionId = "hearth", Position = new SimPoint(2500, 2500) },
                    new BuildingSpawnDefinition { Id = 101, OwnerId = 2, DefinitionId = "hearth", Position = new SimPoint(20500, 2500) }
                },
                ResourceSpawns = includeResource ? new[] { new ResourceSpawnDefinition
                    { Id = 201, DefinitionId = "wood", Position = new SimPoint(14500, 8500) } } : Array.Empty<ResourceSpawnDefinition>()
            };
            world = new World(rules, map);
            root = new GameObject("Fog presentation regression");
            var cameraObject = new GameObject("Fixture camera"); cameraObject.transform.SetParent(root.transform, false);
            camera = cameraObject.AddComponent<Camera>(); camera.enabled = false; camera.orthographic = true; camera.orthographicSize = 12;
            camera.transform.position = new Vector3(12, 25, 8); camera.transform.rotation = Quaternion.Euler(90, 0, 0);
            view = new WorldView(world, root.transform, camera);
            Assert.IsNotNull(view.Feedback);
        }

        private void MoveScout(SimPoint point)
        {
            var result = world.Submit(new MoveCommand(1, new[] { 1 }, point)); Assert.IsTrue(result.Accepted, result.Message);
            Assert.IsTrue(world.TryGetUnit(1, out var scout));
            Until(() => scout.Order == UnitOrder.Idle, 80);
            Assert.AreEqual(point, scout.Position);
        }

        private void Until(Func<bool> predicate, int maximumTicks)
        {
            for (int tick = 0; tick < maximumTicks && !predicate(); tick++)
            { world.Tick(); view.Sync(1, Array.Empty<int>()); }
            Assert.IsTrue(predicate(), "Fixture condition did not complete through legal fixed ticks.");
        }
    }
}
