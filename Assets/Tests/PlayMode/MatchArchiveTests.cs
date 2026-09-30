using System.Collections.Generic;
using Emberfield.Presentation;
using Emberfield.Simulation;
using NUnit.Framework;

namespace Emberfield.Tests.PlayMode
{
    // A saved game here is the start a match was dealt plus every order given since. These cases hold the
    // two properties that makes that honest: the orders survive a round trip through the file, and replaying
    // them into the same start rebuilds the same battlefield rather than one that merely looks similar.
    public sealed class MatchArchiveTests
    {
        [Test]
        public void EveryKindOfOrderSurvivesTheRoundTripThroughTheFile()
        {
            var commands = new List<IGameCommand>
            {
                new MoveCommand(1, new[] { 1, 2 }, new SimPoint(12000, 9000), MovementFormation.Line),
                new StopCommand(1, new[] { 3 }),
                new AttackCommand(2, new[] { 11 }, 101),
                new GatherCommand(1, new[] { 4 }, 205),
                new ReturnCargoCommand(1, new[] { 4 }, 100),
                new BuildCommand(1, new[] { 2 }, "shelter", new SimPoint(21000, 17000)),
                new BuildCommand(1, new[] { 2 }, "gate", new SimPoint(21500, 17000), true),
                new BuildRunCommand(1, new[] { 2, 4 }, "wall", new[] { new BuildSite(new SimPoint(7500, 4500), false), new BuildSite(new SimPoint(9500, 6500), true) }),
                new ConstructCommand(1, new[] { 2 }, 140),
                new TrainCommand(1, 100, "reedguard"),
                new SetRallyCommand(1, 100, new SimPoint(30000, 31000)),
                new ResearchCommand(1, 100, "weapons_1"),
                new SurrenderCommand(2),
                new BoardWallCommand(1, new[] { 5 }, 160, 7),
                new LeaveWallCommand(1, new[] { 5 }, new SimPoint(4000, 5000)),
                new EmbarkCommand(1, new[] { 2, 5 }, 180),
                new DisembarkCommand(1, 180, new SimPoint(12500, 18500)),
                new SetGateCommand(1, 161, true),
                new SetCharterCommand(1, 120, StoreyardCharter.Logistics),
                new DeployThreadkeeperCommand(1, 9, 120),
                new PackOutpostCommand(1, 121),
                new DeployOutpostCommand(1, 9, new SimPoint(7000, 8000)),
                new RepositionCommand(2, new[] { 12, 13 })
            };
            foreach (var command in commands)
            {
                var order = System.Array.Find(MatchArchive.Encode(Journal(command)), item => true);
                Assert.IsNotNull(order, command.GetType().Name + " must be recordable.");
                var restored = MatchArchive.Decode(order);
                Assert.IsNotNull(restored, order.Kind);
                Assert.AreEqual(command.GetType(), restored.GetType(), order.Kind);
                Assert.AreEqual(command.PlayerId, restored.PlayerId, order.Kind);
                Assert.AreEqual(Describe(command), Describe(restored), order.Kind + " must come back with every field intact.");
            }
        }

        [Test]
        public void ReplayingASavedMatchRebuildsTheSameBattlefield()
        {
            var original = DefinitionLoader.CreateOfflineWorld("aven", VictoryMode.Conquest);
            var worker = Owned(original, true);
            var soldierless = Owned(original, true);
            Assert.IsTrue(original.Submit(new MoveCommand(1, new[] { worker }, new SimPoint(30000, 30000))).Accepted);
            Tick(original, 40);
            Assert.IsTrue(original.Submit(new TrainCommand(1, Hearth(original), "tender")).Accepted);
            Tick(original, 60);
            Assert.IsTrue(original.Submit(new MoveCommand(1, new[] { soldierless }, new SimPoint(24000, 34000))).Accepted);
            Tick(original, 150);

            var save = MatchArchive.Capture(original, original.Map.Id, "aven", OfflineAiDifficulty.Normal);
            Assert.That(save.Orders.Length, Is.EqualTo(3));
            Assert.That(save.Tick, Is.EqualTo(original.TickIndex));

            var restored = DefinitionLoader.CreateOfflineWorld("aven", VictoryMode.Conquest);
            int cursor = 0; bool finished = false; int guard = 0;
            while (!finished && guard++ < 50) cursor = MatchArchive.Replay(restored, save, cursor, 200, out finished);
            Assert.IsTrue(finished, "The replay must reach the tick the match was saved at.");

            Assert.AreEqual(original.TickIndex, restored.TickIndex, "Same tick.");
            Assert.AreEqual(Fingerprint(original), Fingerprint(restored), "Every unit, building and stock must land where it was left.");
        }

        [Test]
        public void ASaveWrittenToDiskReadsBackIdentically()
        {
            const string slot = "test_match";
            var world = DefinitionLoader.CreateOfflineWorld("aven", VictoryMode.Dominion);
            Assert.IsTrue(world.Submit(new MoveCommand(1, new[] { Owned(world, true) }, new SimPoint(34000, 30000))).Accepted,
                "The case needs an order on the journal to round trip.");
            Tick(world, 25);
            var save = MatchArchive.Capture(world, world.Map.Id, "aven", OfflineAiDifficulty.Hard);
            try
            {
                MatchArchive.Write(save, slot);
                Assert.IsTrue(MatchArchive.Exists(slot));
                var read = MatchArchive.Read(slot);
                Assert.IsNotNull(read);
                Assert.AreEqual(save.MapId, read.MapId);
                Assert.AreEqual(save.FactionId, read.FactionId);
                Assert.AreEqual(save.Mode, read.Mode);
                Assert.AreEqual(save.Difficulty, read.Difficulty);
                Assert.AreEqual(save.Tick, read.Tick);
                Assert.AreEqual(save.Orders.Length, read.Orders.Length);
                Assert.AreEqual(Describe(MatchArchive.Decode(save.Orders[0])), Describe(MatchArchive.Decode(read.Orders[0])));
            }
            finally { MatchArchive.Delete(slot); }
            Assert.IsFalse(MatchArchive.Exists(slot));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void NavalSaveReplaysPassengersAndTheirLanding(bool land)
        {
            var original = TransportWorld();
            Assert.IsTrue(original.Submit(new EmbarkCommand(1, new[] { 1 }, 10)).Accepted);
            Tick(original, 60);
            Assert.IsTrue(original.TryGetPassenger(1, out var passenger), "The save must contain a real passenger.");
            int passengerHealth = passenger.Health;
            if (land)
            {
                Assert.IsTrue(original.Submit(new DisembarkCommand(1, 10, new SimPoint(9500, 12500))).Accepted);
                Tick(original, 180);
                Assert.IsTrue(original.TryGetUnit(1, out var landed));
                Assert.That(landed.Position.Z, Is.GreaterThanOrEqualTo(10000), "The passenger reached the opposite bank.");
            }
            var save = UnityEngine.JsonUtility.FromJson<MatchArchive.Save>(UnityEngine.JsonUtility.ToJson(
                MatchArchive.Capture(original, original.Map.Id, "pirates", OfflineAiDifficulty.Normal)));
            Assert.That(save.Orders.Length, Is.EqualTo(land ? 2 : 1));
            Assert.That(save.Version, Is.EqualTo(3), "Old clients must refuse saves whose transport orders they cannot replay.");
            var restored = TransportWorld();
            int cursor = 0, guard = 0; bool finished = false;
            while (!finished && guard++ < 50) cursor = MatchArchive.Replay(restored, save, cursor, 20, out finished);
            Assert.IsTrue(finished);
            Assert.AreEqual(original.TickIndex, restored.TickIndex);
            Assert.AreEqual(Fingerprint(original), Fingerprint(restored));
            Assert.IsTrue(restored.TryGetUnit(10, out var ship));
            Assert.That(ship.CargoCount, Is.EqualTo(land ? 0 : 1));
            Assert.AreEqual(!land, restored.TryGetPassenger(1, out var restoredPassenger));
            if (!land)
            {
                Assert.AreEqual(passengerHealth, restoredPassenger.Health);
                Assert.AreEqual(ship.Position, restoredPassenger.Position);
            }
        }

        private static World TransportWorld()
        {
            var water = new List<GridCell>();
            for (int z = 6; z < 10; z++) for (int x = 0; x < 24; x++) water.Add(new GridCell(x, z));
            return new World(new GameDefinition
            {
                BasePopulationCapacity = 20,
                Units = new[]
                {
                    new UnitDefinition { Id = "worker", IsWorker = true, Tags = CombatTags.Worker, RadiusMillimetres = 300, MaxHealth = 85 },
                    new UnitDefinition { Id = "ship", Tags = CombatTags.Naval, Domain = MovementDomain.Water,
                        CargoCapacity = 6, RadiusMillimetres = 500, MoveSpeedMillimetresPerSecond = 4000, MaxHealth = 300 }
                }
            }, new MapDefinition
            {
                Id = "archive-strait", RealmId = ContentRealms.Naval, WidthCells = 24, HeightCells = 18, CellSizeMillimetres = 1000,
                WaterCells = water.ToArray(), BlockedCells = water.ToArray(),
                UnitSpawns = new[]
                {
                    new UnitSpawnDefinition { Id = 1, OwnerId = 1, DefinitionId = "worker", Position = new SimPoint(9500, 5500) },
                    new UnitSpawnDefinition { Id = 10, OwnerId = 1, DefinitionId = "ship", Position = new SimPoint(9500, 6500) }
                }
            });
        }

        private static MatchJournal Journal(IGameCommand command)
        {
            var journal = new MatchJournal();
            typeof(MatchJournal).GetMethod("Record", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .Invoke(journal, new object[] { 7L, command });
            return journal;
        }

        private static void Tick(World world, int count) { for (int i = 0; i < count; i++) world.Tick(); }

        private static int Owned(World world, bool worker)
        {
            foreach (var unit in world.Units) if (unit.OwnerId == 1 && unit.IsWorker == worker) return unit.Id;
            Assert.Fail("The start has no such unit."); return 0;
        }

        private static int Hearth(World world)
        {
            foreach (var building in world.Buildings) if (building.OwnerId == 1 && building.DefinitionId == "hearth") return building.Id;
            Assert.Fail("The start has no Hearth."); return 0;
        }

        /// <summary>Everything a player could notice, in one string: positions, health, orders and stock.</summary>
        private static string Fingerprint(World world)
        {
            var text = new System.Text.StringBuilder();
            foreach (var unit in world.Units)
                text.Append(unit.Id).Append(':').Append(unit.DefinitionId).Append(':').Append(unit.Position.X).Append(',').Append(unit.Position.Z)
                    .Append(':').Append(unit.Health).Append(':').Append(unit.Order).Append(':').Append(unit.CarriedAmount).Append('|');
            foreach (var building in world.Buildings)
                text.Append(building.Id).Append(':').Append(building.Health).Append(':').Append(building.ConstructionProgress).Append('|');
            foreach (var resource in world.Resources) text.Append(resource.Id).Append(':').Append(resource.RemainingAmount).Append('|');
            foreach (int player in new[] { 1, 2 })
                if (world.TryGetPlayer(player, out var state))
                    text.Append(player).Append(':').Append(state.Resources.Food).Append(',').Append(state.Resources.Wood)
                        .Append(',').Append(state.Resources.Metal).Append(',').Append(state.Resources.Stone)
                        .Append(':').Append(state.PopulationUsed).Append('|');
            return text.ToString();
        }

        private static string Describe(IGameCommand command)
        {
            var order = MatchArchive.Encode(Journal(command))[0];
            return order.Kind + "|" + order.Player + "|" + string.Join(",", order.Units ?? System.Array.Empty<int>()) + "|" +
                order.Entity + "|" + order.Text + "|" + order.X + "," + order.Z + "|" + order.Flag + "|" + string.Join(",", order.Sites ?? System.Array.Empty<int>());
        }
    }
}
