using System.Collections.Generic;
using Emberfield.Simulation;
using NUnit.Framework;
using UnityEngine;

namespace Emberfield.Tests.PlayMode
{
    // The shipped worker balance: a villager defends itself and its neighbours, still loses to a soldier,
    // and never wanders off to pick a fight. These read the authored definitions, not a mirror of them.
    public class WorkerDefenceTests
    {
        private const int WorkerId = 1, SoldierId = 100;

        [Test]
        public void ASoldierBeatsALoneWorkerThatWasOrderedToFightBack()
        {
            var world = Field(1, 900);
            Assert.IsTrue(world.TryGetUnit(WorkerId, out var worker));
            Assert.That(worker.AttackDamage, Is.GreaterThan(0), "A villager can fight at all.");
            var ordered = world.Submit(new AttackCommand(1, new[] { WorkerId }, SoldierId));
            Assert.IsTrue(ordered.Accepted, ordered.Message);
            for (int tick = 0; tick < 600 && world.TryGetUnit(WorkerId, out _); tick++) world.Tick();
            Assert.IsFalse(world.TryGetUnit(WorkerId, out _), "One villager loses that fight.");
            Assert.IsTrue(world.TryGetUnit(SoldierId, out var soldier));
            Assert.That(soldier.Health, Is.GreaterThan(soldier.MaxHealth * .7f), "And it barely scratches the soldier.");
        }

        [Test]
        public void ThreeWorkersTogetherBringASoldierDownAndPayForIt()
        {
            var world = Field(3, 900);
            var ordered = world.Submit(new AttackCommand(1, new[] { 1, 2, 3 }, SoldierId));
            Assert.IsTrue(ordered.Accepted, ordered.Message);
            for (int tick = 0; tick < 1600 && world.TryGetUnit(SoldierId, out _); tick++) world.Tick();
            Assert.IsFalse(world.TryGetUnit(SoldierId, out _), "Defending together has to be worth something.");
            int survivors = 0;
            foreach (int id in new[] { 1, 2, 3 }) if (world.TryGetUnit(id, out _)) survivors++;
            Assert.That(survivors, Is.InRange(1, 2), "The group wins, and loses at least one of its own doing it.");
        }

        [Test]
        public void AnIdleWorkerHoldsItsGroundInsteadOfChasingAnEnemyNearby()
        {
            var world = Field(1, 4000);
            Assert.IsTrue(world.Submit(new StopCommand(2, new[] { SoldierId })).Accepted);
            Assert.IsTrue(world.TryGetUnit(WorkerId, out var worker));
            var start = worker.Position;
            for (int tick = 0; tick < 200; tick++) world.Tick();
            Assert.IsTrue(world.TryGetUnit(WorkerId, out worker));
            Assert.AreEqual(0, worker.AttackTargetId, "A villager never goes looking for a fight.");
            Assert.AreEqual(start, worker.Position, "It keeps its ground rather than chasing.");
            Assert.AreEqual(UnitOrder.Idle, worker.Order);
        }

        private static World Field(int workers, int distanceMillimetres)
        {
            var spawns = new List<UnitSpawnDefinition>();
            for (int index = 0; index < workers; index++)
                spawns.Add(new UnitSpawnDefinition
                { Id = index + 1, OwnerId = 1, DefinitionId = "tender", Position = new SimPoint(10000, 10000 + index * 800) });
            spawns.Add(new UnitSpawnDefinition
            { Id = SoldierId, OwnerId = 2, DefinitionId = "reedguard", Position = new SimPoint(10000 + distanceMillimetres, 10000) });
            var rules = JsonUtility.FromJson<GameDefinition>(Resources.Load<TextAsset>("Definitions/greybox").text);
            return new World(rules, new MapDefinition
            {
                Id = "worker_defence",
                WidthCells = 32,
                HeightCells = 24,
                CellSizeMillimetres = 1000,
                RealmId = ContentRealms.Historical,
                PlayerFactions = new[]
                {
                    new PlayerFactionDefinition { PlayerId = 1, FactionId = "aven" },
                    new PlayerFactionDefinition { PlayerId = 2, FactionId = "serevin" }
                },
                UnitSpawns = spawns.ToArray()
            });
        }
    }
}
