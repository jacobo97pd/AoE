using System;
using System.Linq;
using Emberfield.Simulation;
using NUnit.Framework;

namespace Emberfield.Tests.EditMode
{
    public sealed class WorkerInteractionRecoveryTests
    {
        [TestCase(0, false)]
        [TestCase(8, false)]
        [TestCase(0, true)]
        [TestCase(8, true)]
        public void BothSeatsDeliverWhenARecruitOccupiesTheirAlreadyPlannedDropOff(int startDelay, bool explicitReturn)
        {
            // Preserve the HTTPS failure's geometry and timings independently of future balance data:
            // a full load leaves before tick120, then training occupies that planned interaction cell.
            var world = CreateRace();
            for (int tick = 0; tick < startDelay; tick++) world.Tick();
            foreach (int player in new[] { 1, 2 })
            {
                Accepted(world.Submit(new MoveCommand(player, new[] { player == 1 ? 1 : 11 },
                    player == 1 ? new SimPoint(19000, 24500) : new SimPoint(77000, 47500))));
                Accepted(world.Submit(new GatherCommand(player, new[] { player == 1 ? 2 : 12 }, player == 1 ? 203 : 223)));
                Accepted(world.Submit(new TrainCommand(player, player == 1 ? 100 : 101, "tender")));
            }
            var planned = new SimPoint[2];
            var returning = new bool[2];
            var delivered = new bool[2];
            var recruits = new UnitState[2];
            for (int tick = 0; tick < 400 && !delivered.All(value => value); tick++)
            {
                world.Tick();
                for (int index = 0; index < 2; index++)
                {
                    int player = index + 1;
                    var worker = Unit(world, player == 1 ? 2 : 12);
                    var inventory = world.Players.Single(value => value.Id == player).Resources;
                    var node = world.Resources.Single(value => value.Id == (player == 1 ? 203 : 223));
                    Assert.That(inventory.Wood - 80 + worker.CarriedAmount + node.RemainingAmount, Is.EqualTo(500),
                        "Rerouting must neither duplicate inventory nor lose undelivered cargo.");
                    if (!returning[index] && worker.WorkerTask == WorkerTask.ReturningResources)
                    {
                        returning[index] = true;
                        planned[index] = worker.Destination;
                        Assert.That(worker.CarriedAmount, Is.EqualTo(11));
                        Assert.That(inventory.Wood, Is.EqualTo(80), "Planning a return does not credit income.");
                        if (explicitReturn)
                            Accepted(world.Submit(new ReturnCargoCommand(player, new[] { worker.Id }, player == 1 ? 100 : 101)));
                    }
                    var recruit = world.Units.FirstOrDefault(value => value.OwnerId == player && value.Id > 223);
                    if (recruit != null && recruits[index] == null)
                    {
                        recruits[index] = recruit;
                        Assert.That(returning[index], Is.True, "The route must predate the recruit.");
                        Assert.That(recruit.Position, Is.EqualTo(planned[index]), "This fixture must actually obstruct the planned destination.");
                        Assert.That(inventory.Wood, Is.EqualTo(80), "The obstructed carrier has not arrived yet.");
                    }
                    if (inventory.Wood > 80 && !delivered[index])
                    {
                        delivered[index] = true;
                        Assert.That(inventory.Wood, Is.EqualTo(91));
                        Assert.That(worker.CarriedAmount, Is.Zero);
                        var hearth = world.Buildings.Single(value => value.OwnerId == player);
                        long dx = Math.Max(0, Math.Abs((long)worker.Position.X - hearth.Position.X) - 2000);
                        long dz = Math.Max(0, Math.Abs((long)worker.Position.Z - hearth.Position.Z) - 2000);
                        Assert.That(dx * dx + dz * dz, Is.LessThanOrEqualTo(500L * 500), "Credit requires actual arrival at the building edge.");
                        if (explicitReturn)
                        {
                            Assert.That(worker.WorkerTask, Is.EqualTo(WorkerTask.None));
                            Assert.That(worker.TargetResourceId, Is.Zero, "Explicit return must not restore a cancelled gathering assignment.");
                        }
                    }
                }
            }
            CollectionAssert.AreEqual(new[] { true, true }, delivered, "Both seats must recover without another player order.");
            for (int index = 0; index < recruits.Length; index++)
            {
                Assert.That(recruits[index], Is.Not.Null);
                Assert.That(recruits[index].Position, Is.EqualTo(planned[index]), "Recovery must not teleport the idle blocker away.");
                Assert.That(recruits[index].Order, Is.EqualTo(UnitOrder.Idle));
                Assert.That(world.Players.Single(value => value.Id == index + 1).Resources.Food, Is.EqualTo(70), "Training charges exactly once.");
            }
            Assert.That(Unit(world, 1).Position, Is.EqualTo(new SimPoint(19000, 24500)));
            Assert.That(Unit(world, 11).Position, Is.EqualTo(new SimPoint(77000, 47500)));
            Assert.That(world.Units.Count, Is.EqualTo(10));
        }

        [Test]
        public void FullyOccupiedDropOffRetainsCargoUntilAnAccessCellIsFreed()
        {
            var world = CreateRace(true);
            Accepted(world.Submit(new GatherCommand(1, new[] { 2 }, 203)));
            Accepted(world.Submit(new TrainCommand(1, 100, "tender")));
            for (int tick = 0; tick < 300; tick++) world.Tick();
            var worker = Unit(world, 2);
            var recruit = world.Units.Single(value => value.OwnerId == 1 && value.Id > 223);
            Assert.That(recruit.Position, Is.EqualTo(new SimPoint(20500, 19500)));
            Assert.That(worker.WorkerTask, Is.EqualTo(WorkerTask.ReturningResources));
            Assert.That(worker.Order, Is.EqualTo(UnitOrder.Idle), "A completely occupied destination waits for a retry instead of endlessly steering at the blocker.");
            for (int tick = 0; tick < 100; tick++)
            {
                world.Tick();
                Assert.That(worker.CarriedAmount, Is.EqualTo(11));
                Assert.That(world.Players[0].Resources.Wood, Is.EqualTo(80), "There is no free physical access for delivery.");
                Assert.That(world.Resources.Single(value => value.Id == 203).RemainingAmount, Is.EqualTo(489));
            }
            Accepted(world.Submit(new MoveCommand(1, new[] { recruit.Id }, new SimPoint(24500, 19500))));
            for (int tick = 0; tick < 200 && world.Players[0].Resources.Wood == 80; tick++) world.Tick();
            Assert.That(world.Players[0].Resources.Wood, Is.EqualTo(91), "The retained load must be delivered after an access cell becomes free.");
            Assert.That(worker.CarriedAmount, Is.Zero);
        }

        private static World CreateRace(bool surroundFirstDropOff = false)
        {
            var definitions = new GameDefinition
            {
                StartingResources = new ResourceAmount(120, 80, 0, 40), BasePopulationCapacity = surroundFirstDropOff ? 64 : 16,
                Units = new[] { new UnitDefinition { Id = "tender", IsWorker = true, MaxHealth = 60,
                    RadiusMillimetres = 300, MoveSpeedMillimetresPerSecond = 3200, Cost = new ResourceAmount(50, 0, 0, 0),
                    TrainTicks = 120, CarryCapacity = 11, GatherIntervalTicks = 10, GatherAmount = 2 } },
                Buildings = new[] { new BuildingDefinition { Id = "hearth", WidthCells = 4, DepthCells = 4,
                    CanDropOff = true, TrainableUnitIds = new[] { "tender" } } },
                Resources = new[] { new ResourceDefinition { Id = "wood", Kind = ResourceKind.Wood, InitialAmount = 500 } }
            };
            var map = new MapDefinition
            {
                Id = "dropoff-recruit-race", WidthCells = 96, HeightCells = 72,
                UnitSpawns = new[] { Spawn(1, 1, 16500, 22500), Spawn(2, 1, 18500, 22500), Spawn(3, 1, 20500, 22500), Spawn(4, 1, 22500, 22500),
                    Spawn(11, 2, 79500, 49500), Spawn(12, 2, 77500, 49500), Spawn(13, 2, 75500, 49500), Spawn(14, 2, 73500, 49500) },
                BuildingSpawns = new[] {
                    new BuildingSpawnDefinition { Id = 100, OwnerId = 1, DefinitionId = "hearth", Position = new SimPoint(18000, 18000) },
                    new BuildingSpawnDefinition { Id = 101, OwnerId = 2, DefinitionId = "hearth", Position = new SimPoint(78000, 54000) } },
                ResourceSpawns = new[] {
                    new ResourceSpawnDefinition { Id = 203, DefinitionId = "wood", Position = new SimPoint(25500, 21500) },
                    new ResourceSpawnDefinition { Id = 223, DefinitionId = "wood", Position = new SimPoint(70500, 50500) } }
            };
            if (surroundFirstDropOff)
            {
                var blockers = new System.Collections.Generic.List<UnitSpawnDefinition>();
                int id = 30;
                for (int cell = 16; cell < 20; cell++)
                {
                    blockers.Add(Spawn(id++, 1, cell * 1000 + 500, 15500));
                    blockers.Add(Spawn(id++, 1, cell * 1000 + 500, 20500));
                    blockers.Add(Spawn(id++, 1, 15500, cell * 1000 + 500));
                    if (cell != 19) blockers.Add(Spawn(id++, 1, 20500, cell * 1000 + 500));
                }
                map.UnitSpawns = map.UnitSpawns.Concat(blockers).ToArray();
            }
            return new World(definitions, map);
        }

        private static UnitSpawnDefinition Spawn(int id, int owner, int x, int z) =>
            new UnitSpawnDefinition { Id = id, OwnerId = owner, DefinitionId = "tender", Position = new SimPoint(x, z) };
        private static UnitState Unit(World world, int id)
        {
            Assert.That(world.TryGetUnit(id, out var unit), Is.True);
            return unit;
        }
        private static void Accepted(CommandResult result) => Assert.That(result.Accepted, Is.True, result.Message);
    }
}
