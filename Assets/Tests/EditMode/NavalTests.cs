using System;
using System.Collections.Generic;
using System.Linq;
using Emberfield.Simulation;
using NUnit.Framework;

namespace Emberfield.Tests.EditMode
{
    /// <summary>
    /// Ships on a strait: land to the south and north, six rows of deep water between them with a rock in the middle.
    /// Ships sail the water only, troops walk the land only, a dock stands on the shore, and a transport carries troops
    /// across and lands them.
    /// </summary>
    public sealed class NavalTests
    {
        private const int Width = 40, Height = 24, SeaFrom = 9, SeaTo = 14;
        private const int Worker = 1, Spear = 2, Spear2 = 3, Spear3 = 4, Archer = 5, Ship = 10, EnemySpear = 20, EnemyShip = 30, EnemyInshore = 31;

        private static GameDefinition Rules() => new GameDefinition
        {
            StartingResources = new ResourceAmount(5000, 5000, 5000, 5000), BasePopulationCapacity = 100,
            Units = new[]
            {
                new UnitDefinition { Id = "worker", IsWorker = true, Tags = CombatTags.Worker, RadiusMillimetres = 300,
                    Attack = new AttackDefinition { Damage = 2, RangeMillimetres = 1000, CooldownTicks = 20 } },
                new UnitDefinition { Id = "spear", Tags = CombatTags.Infantry, RadiusMillimetres = 300, MaxHealth = 100,
                    Attack = new AttackDefinition { Damage = 10, RangeMillimetres = 1000, CooldownTicks = 20 } },
                new UnitDefinition { Id = "archer", Tags = CombatTags.Ranged, RadiusMillimetres = 300, MaxHealth = 80,
                    Attack = new AttackDefinition { Damage = 8, RangeMillimetres = 5000, AcquireRangeMillimetres = 6000, CooldownTicks = 20, ProjectileSpeedMillimetresPerSecond = 12000 } },
                new UnitDefinition { Id = "ship", Tags = CombatTags.Naval, Domain = MovementDomain.Water, CargoCapacity = 4, RadiusMillimetres = 500,
                    MaxHealth = 300, MoveSpeedMillimetresPerSecond = 4000, PopulationCost = 2, TrainTicks = 20,
                    Attack = new AttackDefinition { Damage = 20, RangeMillimetres = 5000, AcquireRangeMillimetres = 6000, CooldownTicks = 20, ProjectileSpeedMillimetresPerSecond = 14000 } }
            },
            Buildings = new[]
            {
                new BuildingDefinition { Id = "hall", WidthCells = 3, DepthCells = 3, PopulationCapacity = 50, CanDropOff = true },
                new BuildingDefinition { Id = "dock", WidthCells = 3, DepthCells = 3, BuildTicks = 10, RequiresShore = true, TrainableUnitIds = new[] { "ship" } }
            }
        };

        private static MapDefinition Strait(bool match = false)
        {
            var blocked = new List<GridCell>(); var water = new List<GridCell>();
            for (int z = SeaFrom; z <= SeaTo; z++)
            for (int x = 0; x < Width; x++)
            {
                blocked.Add(new GridCell(x, z));
                // The rock in mid-strait is blocked to everything: it is not water.
                if (!(x >= 18 && x <= 20 && z >= 11 && z <= 12)) water.Add(new GridCell(x, z));
            }
            return new MapDefinition
            {
                Id = "naval-strait", WidthCells = Width, HeightCells = Height, CellSizeMillimetres = 1000,
                BlockedCells = blocked.ToArray(), WaterCells = water.ToArray(),
                UnitSpawns = new[]
                {
                    Spawn(Worker, 1, "worker", 10500, 6500), Spawn(Spear, 1, "spear", 14500, 7500), Spawn(Spear2, 1, "spear", 15500, 7500),
                    Spawn(Spear3, 1, "spear", 16500, 7500), Spawn(Archer, 1, "archer", 22500, 7500), Spawn(Ship, 1, "ship", 15500, 9500),
                    Spawn(EnemySpear, 2, "spear", 25500, 17500), Spawn(EnemyShip, 2, "ship", 34500, 13500)
                },
                BuildingSpawns = new[]
                {
                    new BuildingSpawnDefinition { Id = 100, OwnerId = 1, DefinitionId = "hall", Position = new SimPoint(5500, 3500) },
                    new BuildingSpawnDefinition { Id = 101, OwnerId = 2, DefinitionId = "hall", Position = new SimPoint(34500, 20500) }
                },
                OfflineMatch = match ? new OfflineMatchDefinition { Enabled = true, Mode = VictoryMode.Conquest, PlayerIds = new[] { 1, 2 },
                    VisionUnitCells = 40, VisionBuildingCells = 40, CentralBuildingId = "hall" } : null
            };
        }

        private static World Create(bool match = false) => Hold(new World(Rules(), Strait(match)));
        // Nobody wanders off to fight on their own before the test gives its orders.
        private static World Hold(World world)
        {
            foreach (var player in world.Players)
            {
                var ids = world.Units.Where(u => u.OwnerId == player.Id).Select(u => u.Id).ToArray();
                if (ids.Length > 0) Accepted(world.Submit(new StopCommand(player.Id, ids)));
            }
            return world;
        }
        private static UnitSpawnDefinition Spawn(int id, int owner, string definition, int x, int z) =>
            new UnitSpawnDefinition { Id = id, OwnerId = owner, DefinitionId = definition, Position = new SimPoint(x, z) };
        private static UnitState Unit(World world, int id) { Assert.That(world.TryGetUnit(id, out var unit), Is.True, "unit " + id); return unit; }
        private static PlayerState Player(World world, int id) { Assert.That(world.TryGetPlayer(id, out var player), Is.True); return player; }
        private static void Accepted(CommandResult result) => Assert.That(result.Accepted, Is.True, result.Reason + ": " + result.Message);
        private static void Rejected(CommandResult result, CommandRejection reason) { Assert.That(result.Accepted, Is.False); Assert.That(result.Reason, Is.EqualTo(reason), result.Message); }
        private static void Until(World world, Func<bool> condition, int ticks, Action everyTick = null)
        {
            for (int i = 0; i < ticks && !condition(); i++) { world.Tick(); everyTick?.Invoke(); }
            Assert.That(condition(), Is.True, "Bounded simulation condition did not become true.");
        }
        private static bool Deep(World world, SimPoint point) => world.IsSailable(point);
        private static int Distance(SimPoint a, SimPoint b) => (int)Math.Ceiling(Math.Sqrt((long)(a.X - b.X) * (a.X - b.X) + (long)(a.Z - b.Z) * (a.Z - b.Z)));
        private static bool Rock(SimPoint point) => point.X >= 18000 && point.X < 21000 && point.Z >= 11000 && point.Z < 13000;

        [Test]
        public void ShipsSailOnlyDeepWaterAndTroopsOnlyLand()
        {
            var world = Create();
            Assert.That(world.HasDeepWater, Is.True);
            Assert.That(Deep(world, new SimPoint(15500, 11500)), Is.True);
            Assert.That(Deep(world, new SimPoint(15500, 7500)), Is.False, "Land is not water.");
            Assert.That(Deep(world, new SimPoint(19500, 11500)), Is.False, "The rock in the strait is not water.");
            Rejected(world.Submit(new MoveCommand(1, new[] { Ship }, new SimPoint(15500, 5500))), CommandRejection.DestinationBlocked);
            Rejected(world.Submit(new MoveCommand(1, new[] { Ship }, new SimPoint(19500, 11500))), CommandRejection.DestinationBlocked);
            Rejected(world.Submit(new MoveCommand(1, new[] { Spear }, new SimPoint(15500, 11500))), CommandRejection.DestinationBlocked);
            Rejected(world.Submit(new MoveCommand(1, new[] { Spear }, new SimPoint(15500, 18500))), CommandRejection.NoPath);
            // Round the rock and away down the strait, never touching land or rock on the way.
            var goal = new SimPoint(37500, 12500);
            Accepted(world.Submit(new MoveCommand(1, new[] { Ship }, goal)));
            var ship = Unit(world, Ship);
            Until(world, () => ship.Order == UnitOrder.Idle && Distance(ship.Position, goal) < 700, 400, () =>
            {
                Assert.That(Deep(world, ship.Position) && !Rock(ship.Position), Is.True, "Off the water at " + ship.Position);
                foreach (var dx in new[] { -499, 499 }) foreach (var dz in new[] { -499, 499 })
                    Assert.That(world.IsWalkable(new SimPoint(ship.Position.X + dx, ship.Position.Z + dz)), Is.False, "The hull reached over land at " + ship.Position);
            });
        }

        [Test]
        public void AMixedSelectionMovesEachOnItsOwnGrid()
        {
            var world = Create();
            Accepted(world.Submit(new MoveCommand(1, new[] { Spear, Ship }, new SimPoint(25500, 11500))));
            Assert.That(Unit(world, Ship).Order, Is.EqualTo(UnitOrder.Moving));
            Assert.That(Unit(world, Spear).Order, Is.EqualTo(UnitOrder.Idle), "Troops do not wade into the sea.");
            Accepted(world.Submit(new MoveCommand(1, new[] { Spear, Ship }, new SimPoint(12500, 5500))));
            Assert.That(Unit(world, Spear).Order, Is.EqualTo(UnitOrder.Moving));
        }

        [Test]
        public void ADockStandsOnlyOnTheShoreAndLaunchesItsShipsOntoTheWater()
        {
            var world = Create();
            Rejected(world.Submit(new BuildCommand(1, new[] { Worker }, "dock", new SimPoint(10500, 3500))), CommandRejection.NoShore);
            var built = world.Submit(new BuildCommand(1, new[] { Worker }, "dock", new SimPoint(25500, 7500)));
            Accepted(built);
            Assert.That(world.TryGetBuilding(built.EntityId, out var dock), Is.True);
            Until(world, () => dock.IsComplete, 600);
            Rejected(world.Submit(new SetRallyCommand(1, dock.Id, new SimPoint(25500, 4500))), CommandRejection.DestinationBlocked);
            Accepted(world.Submit(new SetRallyCommand(1, dock.Id, new SimPoint(28500, 12500))));
            var before = new HashSet<int>(world.Units.Select(u => u.Id));
            int population = Player(world, 1).PopulationUsed;
            Accepted(world.Submit(new TrainCommand(1, dock.Id, "ship")));
            Until(world, () => world.Units.Any(u => !before.Contains(u.Id)), 200);
            var launched = world.Units.First(u => !before.Contains(u.Id));
            Assert.That(launched.Domain, Is.EqualTo(MovementDomain.Water));
            Assert.That(Deep(world, launched.Position), Is.True, "Launched onto the water beside the dock.");
            Assert.That(launched.Position.Z, Is.EqualTo(9500), "The row of water touching the dock.");
            Assert.That(Player(world, 1).PopulationUsed, Is.EqualTo(population + 2));
            Until(world, () => launched.Order == UnitOrder.Idle && Distance(launched.Position, new SimPoint(28500, 12500)) < 700, 300);
        }

        [Test]
        public void ATransportCarriesTroopsAcrossTheStraitAndLandsThemOnTheFarShore()
        {
            var world = Create();
            var ship = Unit(world, Ship);
            int population = Player(world, 1).PopulationUsed;
            Accepted(world.Submit(new EmbarkCommand(1, new[] { Spear, Spear2, Spear3 }, Ship)));
            Rejected(world.Submit(new EmbarkCommand(1, new[] { Archer, Worker }, Ship)), CommandRejection.CargoFull);
            Until(world, () => ship.CargoCount == 3, 300);
            foreach (int id in new[] { Spear, Spear2, Spear3 })
            {
                Assert.That(world.TryGetUnit(id, out _), Is.False, "Aboard is out of the world.");
                Assert.That(world.TryGetPassenger(id, out var passenger), Is.True);
                Assert.That(passenger.CarrierId, Is.EqualTo(Ship));
            }
            Assert.That(Player(world, 1).PopulationUsed, Is.EqualTo(population), "Passengers still count.");
            Rejected(world.Submit(new MoveCommand(1, new[] { Spear }, new SimPoint(12500, 5500))), CommandRejection.UnknownUnit);
            var shore = new SimPoint(15500, 16500);
            Accepted(world.Submit(new DisembarkCommand(1, Ship, shore)));
            Assert.That(ship.IsUnloading, Is.True);
            Until(world, () => ship.CargoCount == 0, 600);
            Assert.That(ship.IsUnloading, Is.False);
            foreach (int id in new[] { Spear, Spear2, Spear3 })
            {
                var landed = Unit(world, id);
                Assert.That(world.TryGetPassenger(id, out _), Is.False);
                Assert.That(world.IsWalkable(landed.Position) && !Deep(world, landed.Position), Is.True, "Landed on land at " + landed.Position);
                Assert.That(landed.Position.Z, Is.GreaterThanOrEqualTo(15000), "Landed on the far shore.");
                Assert.That(landed.CarrierId, Is.Zero);
            }
            Assert.That(world.Units.Select(u => u.Id), Is.Ordered, "Landed units keep the id order the rules iterate in.");
            Accepted(world.Submit(new MoveCommand(1, new[] { Spear }, new SimPoint(15500, 21500))));
            Until(world, () => Unit(world, Spear).Order == UnitOrder.Idle, 300);
            Assert.That(Unit(world, Spear).Position.Z, Is.GreaterThan(20000));
        }

        [Test]
        public void BoardingNeedsYourOwnShipLyingAlongsideAShore()
        {
            var world = Create();
            Rejected(world.Submit(new EmbarkCommand(1, new[] { Spear }, EnemyShip)), CommandRejection.NotOwner);
            Rejected(world.Submit(new EmbarkCommand(1, new[] { Ship }, Ship)), CommandRejection.InvalidCommand);
            Rejected(world.Submit(new DisembarkCommand(1, Ship, new SimPoint(15500, 16500))), CommandRejection.InvalidCommand);
            // Mid-strait, out of a soldier's reach from either bank.
            Accepted(world.Submit(new MoveCommand(1, new[] { Ship }, new SimPoint(8500, 12000))));
            Until(world, () => Unit(world, Ship).Order == UnitOrder.Idle, 300);
            Rejected(world.Submit(new EmbarkCommand(1, new[] { Spear }, Ship)), CommandRejection.NoShore);
        }

        [Test]
        public void TroopsWalkToAShipAlongsideAndClimbAboardOverASecond()
        {
            var world = Create();
            Accepted(world.Submit(new EmbarkCommand(1, new[] { Worker }, Ship)));
            var worker = Unit(world, Worker);
            Assert.That(worker.EmbarkShipId, Is.EqualTo(Ship));
            Until(world, () => worker.EmbarkRemainingTicks > 0, 200);
            Assert.That(world.TryGetUnit(Worker, out _), Is.True, "Climbing takes a moment on the shore.");
            Until(world, () => !world.TryGetUnit(Worker, out _), World.TickRate + 2);
            Assert.That(Unit(world, Ship).CargoCount, Is.EqualTo(1));
        }

        [Test]
        public void AnotherOrderCallsTheBoardingOff()
        {
            var world = Create();
            Accepted(world.Submit(new EmbarkCommand(1, new[] { Archer }, Ship)));
            world.Tick();
            Accepted(world.Submit(new MoveCommand(1, new[] { Archer }, new SimPoint(30500, 4500))));
            var archer = Unit(world, Archer);
            Assert.That(archer.EmbarkShipId, Is.Zero);
            Assert.That(archer.AutoAttackEnabled, Is.True);
            Until(world, () => archer.Order == UnitOrder.Idle, 400);
            Assert.That(Unit(world, Ship).CargoCount, Is.Zero);
        }

        [Test]
        public void ASunkTransportDrownsEveryoneAboard()
        {
            var world = Create();
            Accepted(world.Submit(new EmbarkCommand(1, new[] { Spear, Spear2, Spear3 }, Ship)));
            var ship = Unit(world, Ship);
            Until(world, () => ship.CargoCount == 3, 300);
            int deaths = world.DeathCount, population = Player(world, 1).PopulationUsed;
            Accepted(world.Submit(new AttackCommand(2, new[] { EnemyShip }, Ship)));
            Until(world, () => !world.TryGetUnit(Ship, out _), 1200);
            Assert.That(world.DeathCount, Is.EqualTo(deaths + 4), "The hull and its three passengers.");
            Assert.That(Player(world, 1).PopulationUsed, Is.EqualTo(population - 2 - 3));
            foreach (int id in new[] { Spear, Spear2, Spear3 })
            {
                Assert.That(world.TryGetUnit(id, out _), Is.False);
                Assert.That(world.TryGetPassenger(id, out _), Is.False);
            }
            // Nothing refers to the drowned: the world ticks on.
            for (int i = 0; i < 40; i++) world.Tick();
        }

        [Test]
        public void ShipsTradeBroadsidesAndShellTheShoreButBladesCannotReachThem()
        {
            var definitions = Rules();
            var map = Strait();
            map.UnitSpawns = map.UnitSpawns.Append(Spawn(EnemyInshore, 2, "ship", 24500, 9500)).ToArray();
            var world = Hold(new World(definitions, map));
            // A spear on the beach cannot hack at a hull lying off it; an archer can shoot it.
            Rejected(world.Submit(new AttackCommand(1, new[] { Spear3 }, EnemyInshore)), CommandRejection.OutOfRange);
            Accepted(world.Submit(new AttackCommand(1, new[] { Archer }, EnemyInshore)));
            var inshore = Unit(world, EnemyInshore);
            Until(world, () => inshore.Health < inshore.MaxHealth, 200);
            // The hull fires back at the shore.
            Accepted(world.Submit(new AttackCommand(2, new[] { EnemyInshore }, Archer)));
            var archer = Unit(world, Archer);
            Until(world, () => archer.Health < archer.MaxHealth, 200);
            // Ship against ship: one of them goes down, and the rules keep their books.
            int deaths = world.DeathCount;
            int population = Player(world, 2).PopulationUsed;
            Accepted(world.Submit(new AttackCommand(1, new[] { Ship }, EnemyShip)));
            Accepted(world.Submit(new AttackCommand(2, new[] { EnemyShip }, Ship)));
            Until(world, () => !world.TryGetUnit(Ship, out _) || !world.TryGetUnit(EnemyShip, out _), 2000);
            Assert.That(world.DeathCount, Is.GreaterThan(deaths));
            if (!world.TryGetUnit(EnemyShip, out _)) Assert.That(Player(world, 2).PopulationUsed, Is.LessThanOrEqualTo(population - 2));
        }

        [Test]
        public void TheSameNavalOrdersReplayToTheSameState()
        {
            string Run()
            {
                var world = Create();
                var script = new Dictionary<long, IGameCommand>
                {
                    { 1, new EmbarkCommand(1, new[] { Spear, Spear2 }, Ship) },
                    { 5, new MoveCommand(2, new[] { EnemyShip }, new SimPoint(30500, 11500)) },
                    { 160, new DisembarkCommand(1, Ship, new SimPoint(22500, 16500)) },
                    { 500, new AttackCommand(1, new[] { Ship }, EnemyShip) }
                };
                for (int tick = 0; tick < 900; tick++)
                {
                    if (script.TryGetValue(world.TickIndex, out var command)) world.Submit(command);
                    world.Tick();
                }
                var units = world.Units.Select(u => u.Id + "@" + u.Position + ":" + u.Health + ":" + u.CargoCount);
                return string.Join(";", units) + "|" + world.DeathCount;
            }
            Assert.That(Run(), Is.EqualTo(Run()));
        }

        [Test]
        public void AShipsManifestReachesOnlyItsOwnersObservation()
        {
            var world = Create(true);
            var ship = Unit(world, Ship);
            Accepted(world.Submit(new EmbarkCommand(1, new[] { Spear, Spear2 }, Ship)));
            Until(world, () => ship.CargoCount == 2, 300);
            Accepted(world.Submit(new DisembarkCommand(1, Ship, new SimPoint(15500, 16500))));
            world.Tick();
            var own = NetworkObservation.Export(world, 1, 1);
            var hull = own.Units.Single(u => u.Id == Ship);
            Assert.That(hull.CargoIds, Is.EqualTo(new[] { Spear, Spear2 }));
            Assert.That(hull.CargoDefinitionIds, Is.EqualTo(new[] { "spear", "spear" }));
            Assert.That(hull.CargoHealths, Is.EqualTo(new[] { 100, 100 }));
            Assert.That(hull.IsUnloading, Is.True);
            Assert.That(own.Units.Any(u => u.Id == Spear), Is.False, "Passengers are not on the map.");
            var replica = World.CreateNetworkReplica(world.Definition, world.Map, own);
            Assert.That(replica.TryGetUnit(Ship, out var replicaShip), Is.True);
            Assert.That(replicaShip.CargoCount, Is.EqualTo(2));
            Assert.That(replicaShip.IsUnloading, Is.True);
            Assert.That(replica.TryGetPassenger(Spear, out var passenger), Is.True);
            Assert.That(passenger.CarrierId, Is.EqualTo(Ship));
            Assert.That(replica.TryGetUnit(Spear, out _), Is.False);
            // The rival sees the hull, never who is in it.
            var rival = NetworkObservation.Export(world, 2, 1);
            var seen = rival.Units.Single(u => u.Id == Ship);
            Assert.That(seen.CargoIds, Is.Empty); Assert.That(seen.CargoDefinitionIds, Is.Empty); Assert.That(seen.IsUnloading, Is.False);
            Assert.That(seen.CargoHealths, Is.Empty);
        }

        [TestCase("cargo-on-enemy-hull")] [TestCase("over-capacity")] [TestCase("ship-as-passenger")] [TestCase("mismatched-manifest")] [TestCase("duplicate-passenger")]
        public void AForgedManifestIsRejectedWithoutReplacingTheReplica(string forgery)
        {
            var world = Create(true);
            var replica = World.CreateNetworkReplica(world.Definition, world.Map, NetworkObservation.Export(world, 1, 1));
            var next = NetworkObservation.Export(world, 1, 2);
            var own = next.Units.Single(u => u.Id == Ship);
            var enemy = next.Units.Single(u => u.Id == EnemyShip);
            switch (forgery)
            {
                case "cargo-on-enemy-hull": enemy.CargoIds = new[] { 900 }; enemy.CargoDefinitionIds = new[] { "spear" }; break;
                case "over-capacity": own.CargoIds = new[] { 900, 901, 902, 903, 904 }; own.CargoDefinitionIds = Enumerable.Repeat("spear", 5).ToArray(); break;
                case "ship-as-passenger": own.CargoIds = new[] { 900 }; own.CargoDefinitionIds = new[] { "ship" }; break;
                case "mismatched-manifest": own.CargoIds = new[] { 900, 901 }; own.CargoDefinitionIds = new[] { "spear" }; break;
                case "duplicate-passenger": own.CargoIds = new[] { Spear }; own.CargoDefinitionIds = new[] { "spear" }; break;
            }
            own.CargoHealths = Enumerable.Repeat(100, own.CargoIds.Length).ToArray();
            enemy.CargoHealths = Enumerable.Repeat(100, enemy.CargoIds.Length).ToArray();
            Assert.Throws<ArgumentException>(() => replica.ApplyNetworkSnapshot(next));
            Assert.That(replica.NetworkSnapshotSequence, Is.EqualTo(1));
        }

        [TestCase("missing-ship")] [TestCase("enemy-ship")] [TestCase("boarding-ship")] [TestCase("timer-without-ship")]
        [TestCase("enemy-unload")] [TestCase("empty-unload")] [TestCase("ship-on-land")] [TestCase("over-reserved")]
        public void InvalidNavalReferencesAndDomainsCannotReplaceAReplica(string forgery)
        {
            var world = Create(true);
            var replica = World.CreateNetworkReplica(world.Definition, world.Map, NetworkObservation.Export(world, 1, 1));
            var next = NetworkObservation.Export(world, 1, 2);
            var spear = next.Units.Single(u => u.Id == Spear); var ship = next.Units.Single(u => u.Id == Ship);
            switch (forgery)
            {
                case "missing-ship": spear.EmbarkShipId = 999; break;
                case "enemy-ship": spear.EmbarkShipId = EnemyShip; break;
                case "boarding-ship": ship.EmbarkShipId = EnemyShip; break;
                case "timer-without-ship": spear.EmbarkRemainingTicks = 1; break;
                case "enemy-unload": next.Units.Single(u => u.Id == EnemyShip).IsUnloading = true; break;
                case "empty-unload": ship.IsUnloading = true; break;
                case "ship-on-land": ship.Position = new SimPoint(15500, 7500); break;
                case "over-reserved": foreach (var u in next.Units.Where(u => u.OwnerId == 1 && u.Id != Ship)) u.EmbarkShipId = Ship; break;
            }
            Assert.Throws<ArgumentException>(() => replica.ApplyNetworkSnapshot(next));
            Assert.That(replica.NetworkSnapshotSequence, Is.EqualTo(1));
            Assert.That(Unit(replica, Ship).Position, Is.EqualTo(Unit(world, Ship).Position));
        }

        [Test]
        public void WoundedPassengersFollowTheHullKeepTheirHealthAndSurviveReconnection()
        {
            var map = Strait(true);
            map.UnitSpawns = map.UnitSpawns.Append(Spawn(EnemyInshore, 2, "archer", 20500, 7500)).ToArray();
            var world = Hold(new World(Rules(), map)); var spear = Unit(world, Spear);
            Accepted(world.Submit(new AttackCommand(2, new[] { EnemyInshore }, Spear)));
            Until(world, () => spear.Health < spear.MaxHealth, 200);
            Accepted(world.Submit(new StopCommand(2, new[] { EnemyInshore })));
            Accepted(world.Submit(new EmbarkCommand(1, new[] { Spear }, Ship)));
            var ship = Unit(world, Ship);
            Until(world, () => ship.CargoCount == 1, 200);
            int health = ship.Cargo[0].Health;
            Assert.That(health, Is.LessThan(spear.MaxHealth));
            Accepted(world.Submit(new MoveCommand(1, new[] { Ship }, new SimPoint(28500, 13500))));
            Until(world, () => ship.Order == UnitOrder.Idle, 400, () => Assert.That(ship.Cargo[0].Position, Is.EqualTo(ship.Position)));
            var observation = NetworkObservation.Export(world, 1, 1);
            var replica = World.CreateNetworkReplica(world.Definition, world.Map, observation);
            Assert.That(replica.TryGetPassenger(Spear, out var passenger), Is.True);
            Assert.That(passenger.Health, Is.EqualTo(health)); Assert.That(passenger.Position, Is.EqualTo(ship.Position));
            Assert.That(world.Match.IsFinished, Is.False, "Boarding never changes conquest survival.");
            Accepted(world.Submit(new DisembarkCommand(1, Ship, new SimPoint(28500, 16500))));
            Until(world, () => ship.CargoCount == 0, 400);
            Assert.That(Unit(world, Spear).Health, Is.EqualTo(health));
            replica.ApplyNetworkSnapshot(NetworkObservation.Export(world, 1, 2));
            Assert.That(Unit(replica, Spear).Health, Is.EqualTo(health));
            Assert.That(replica.TryGetPassenger(Spear, out _), Is.False);
        }

        [Test]
        public void HeroCapsIncludePassengersAndRejectForgedDuplicateHeroes()
        {
            var rules = Rules(); rules.Units[1].MaxAlivePerPlayer = 1;
            var map = Strait(true); map.UnitSpawns = map.UnitSpawns.Where(s => s.Id != Spear2 && s.Id != Spear3).ToArray();
            var world = Hold(new World(rules, map));
            Accepted(world.Submit(new EmbarkCommand(1, new[] { Spear }, Ship)));
            Until(world, () => Unit(world, Ship).CargoCount == 1, 200);
            Rejected(world.ValidateUnitRecruitment(1, "spear"), CommandRejection.CannotTrain);
            var replica = World.CreateNetworkReplica(rules, map, NetworkObservation.Export(world, 1, 1));
            var next = NetworkObservation.Export(world, 1, 2); var hull = next.Units.Single(u => u.Id == Ship);
            hull.CargoIds = new[] { Spear, 900 }; hull.CargoDefinitionIds = new[] { "spear", "spear" }; hull.CargoHealths = new[] { 100, 100 };
            Assert.Throws<ArgumentException>(() => replica.ApplyNetworkSnapshot(next));
            Assert.That(replica.NetworkSnapshotSequence, Is.EqualTo(1));
        }

        [Test]
        public void ShallowsRemainWalkableWhileOnlyBlockedWaterIsSailable()
        {
            var map = Strait(); var shallow = new GridCell(0, SeaFrom);
            map.BlockedCells = map.BlockedCells.Where(c => c.X != shallow.X || c.Z != shallow.Z).ToArray();
            var world = Hold(new World(Rules(), map)); var point = new SimPoint(500, SeaFrom * 1000 + 500);
            Assert.That(world.IsWalkable(point), Is.True, "Existing fords and bridges retain land movement.");
            Assert.That(world.IsSailable(point), Is.False, "A hull cannot sail across a land bridge.");
        }

        [TestCase("sapphire_coast", "naval", "sapphire_coast_naval")]
        [TestCase("sapphire_coast", "historical", "sapphire_coast")]
        [TestCase("sapphire_coast", "fantasy", "sapphire_coast")]
        [TestCase("amber_crossing", "historical", "amber_crossing")]
        [TestCase("sunscar_basin", "historical", "sunscar_basin")]
        [TestCase("legend_lands", "fantasy", "legend_lands")]
        public void NavalResourceRoutingPreservesPublicBattlefieldIds(string map, string realm, string resource)
        {
            Assert.That(ContentRealms.MapResourceId(map, realm), Is.EqualTo(resource));
            Assert.That(ContentRealms.IsMapAllowedInRealm(map, realm), Is.True);
            foreach (string known in ContentRealms.All)
                Assert.That(ContentRealms.IsMapAllowedInRealm("sapphire_coast_naval", known), Is.False, "The storage variant is not a new selectable battlefield.");
        }

        [Test]
        public void ClimbingAWallCancelsEmbarkationAndShipsCannotClimbWalls()
        {
            const int wallId = 300;
            var rules = Rules(); rules.Units[3].Tags |= CombatTags.Ranged;
            rules.Buildings = rules.Buildings.Append(new BuildingDefinition { Id = "wall", WidthCells = 3, DepthCells = 1, IsWall = true }).ToArray();
            var map = Strait(true);
            map.BuildingSpawns = map.BuildingSpawns.Append(new BuildingSpawnDefinition
                { Id = wallId, OwnerId = 1, DefinitionId = "wall", Position = new SimPoint(15500, 6500) }).ToArray();
            var world = Hold(new World(rules, map));
            Accepted(world.Submit(new EmbarkCommand(1, new[] { Spear }, Ship)));
            Accepted(world.Submit(new BoardWallCommand(1, new[] { Spear }, wallId)));
            Assert.That(Unit(world, Spear).EmbarkShipId, Is.Zero, "The new wall order cancels the ship reservation immediately.");
            Assert.That(Unit(world, Spear).BoardingWallId, Is.EqualTo(wallId));
            Assert.DoesNotThrow(() => World.CreateNetworkReplica(rules, map, NetworkObservation.Export(world, 1, 1)));
            Until(world, () => Unit(world, Spear).WallId == wallId, 60);
            Assert.That(Unit(world, Ship).CargoCount, Is.Zero);
            Rejected(world.Submit(new EmbarkCommand(1, new[] { Spear }, Ship)), CommandRejection.FactionActionBusy);
            Rejected(world.Submit(new BoardWallCommand(1, new[] { Ship }, wallId)), CommandRejection.InvalidCommand);
        }

        [Test]
        public void NavalAiBuildsAPaidDockTrainsShipsAndAttacksThroughOrdinaryCommands()
        {
            var rules = Rules(); rules.Units[3].Cost = new ResourceAmount(0, 100, 20, 0);
            rules.Buildings[1].Cost = new ResourceAmount(0, 150, 0, 0);
            rules.Buildings = rules.Buildings.Append(new BuildingDefinition { Id = "muster_hall", WidthCells = 3, DepthCells = 3 }).ToArray();
            var map = Strait(true); map.RealmId = ContentRealms.Naval;
            map.UnitSpawns = new[] { Spawn(Worker, 1, "worker", 10500, 6500), Spawn(EnemyShip, 2, "ship", 34500, 13500) }
                .Concat(Enumerable.Range(0, 5).Select(i => Spawn(40 + i, 1, "worker", 2500 + 1500 * i, 6500))).ToArray();
            map.BuildingSpawns = map.BuildingSpawns.Append(new BuildingSpawnDefinition
                { Id = 102, OwnerId = 1, DefinitionId = "muster_hall", Position = new SimPoint(10500, 3500) }).ToArray();
            var world = Hold(new World(rules, map)); var ai = new OfflineAi(world, 1);
            bool built = false, trained = false, attacked = false;
            ai.CommandIssued += (command, result) =>
            {
                if (!result.Accepted) return;
                if (command is BuildCommand build && build.BuildingDefinitionId == "dock") built = true;
                if (command is TrainCommand train && train.UnitDefinitionId == "ship") trained = true;
                if (command is AttackCommand attack && attack.UnitIds.Any(id => world.TryGetUnit(id, out var u) && u.Domain == MovementDomain.Water)) attacked = true;
            };
            Until(world, () => built && trained && attacked, 1800, () => ai.Tick());
            Assert.That(world.Buildings.Any(b => b.OwnerId == 1 && b.DefinitionId == "dock" && b.IsComplete), Is.True);
            Assert.That(world.Units.Any(u => u.OwnerId == 1 && u.Domain == MovementDomain.Water), Is.True);
            Assert.That(ai.Statistics.Spent.Wood, Is.GreaterThanOrEqualTo(250));
            Assert.That(Player(world, 1).Resources.Wood + ai.Statistics.Spent.Wood, Is.EqualTo(rules.StartingResources.Wood), "No free resources fund the fleet.");
        }

        [Test]
        public void ADevelopedNavalAiEmbarksAndLandsItsArmyAcrossWater()
        {
            var rules = Rules();
            rules.Buildings = rules.Buildings.Append(new BuildingDefinition { Id = "muster_hall", WidthCells = 3, DepthCells = 3 }).ToArray();
            var map = Strait(true); map.RealmId = ContentRealms.Naval;
            var troops = Enumerable.Range(0, 26).Select(i => Spawn(50 + i, 1, "spear", 2500 + 1100 * i, 7500)).ToArray();
            map.UnitSpawns = new[] { Spawn(Worker, 1, "worker", 16500, 5500), Spawn(Ship, 1, "ship", 15500, 9500) }
                .Concat(Enumerable.Range(0, 9).Select(i => Spawn(40 + i, 1, "worker", 2500 + 1500 * i, 6500))).Concat(troops).ToArray();
            map.BuildingSpawns = map.BuildingSpawns.Append(new BuildingSpawnDefinition
                { Id = 102, OwnerId = 1, DefinitionId = "muster_hall", Position = new SimPoint(30500, 3500) }).ToArray();
            var world = Hold(new World(rules, map)); var ai = new OfflineAi(world, 1, OfflineAiDifficulty.Easy);
            var boarded = new HashSet<int>(); bool embarking = false, unloading = false;
            ai.CommandIssued += (command, result) =>
            {
                if (!result.Accepted) return;
                embarking |= command is EmbarkCommand; unloading |= command is DisembarkCommand;
            };
            Until(world, () => world.Units.Any(u => boarded.Contains(u.Id) && u.Position.Z >= 15000), 1800, () =>
            {
                foreach (var hull in world.Units) foreach (var passenger in hull.Cargo) boarded.Add(passenger.Id);
                ai.Tick();
            });
            Assert.That(embarking && unloading, Is.True, "Only normal embark and disembark commands ferry the army.");
            Assert.That(boarded.Count, Is.GreaterThan(0));
            Assert.That(world.Units.Where(u => boarded.Contains(u.Id)).All(u => u.Domain == MovementDomain.Land && world.IsWalkable(u.Position)), Is.True);
        }

        [Test]
        public void AShoreBuildingTrainsOnlyShipsAndOnlyAShipCarriesCargo()
        {
            var rules = Rules(); rules.Buildings[0].TrainableUnitIds = new[] { "ship" };
            Assert.Throws<ArgumentException>(() => new World(rules, Strait()));
            rules = Rules(); rules.Units[1].CargoCapacity = 2;
            Assert.Throws<ArgumentException>(() => new World(rules, Strait()));
            rules = Rules(); rules.Units[3].Tags = CombatTags.Heavy;
            Assert.Throws<ArgumentException>(() => new World(rules, Strait()), "A ship is tagged naval.");
            var map = Strait(); map.UnitSpawns[5] = Spawn(Ship, 1, "ship", 15500, 7500);
            Assert.Throws<ArgumentException>(() => new World(Rules(), map), "A ship cannot start on land.");
        }
    }
}
