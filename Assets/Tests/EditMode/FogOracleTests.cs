using System;
using System.Collections.Generic;
using Emberfield.Simulation;
using NUnit.Framework;
using static Emberfield.Tests.EditMode.OfflineMatchTestWorldFactory;

namespace Emberfield.Tests.EditMode
{
    public sealed class FogOracleTests
    {
        [Test]
        public void CellUnionMatchesEveryMovementTickAndStationaryCommandBoundary()
        {
            var world = Create(); var oracle = new CellOracle(world); oracle.Check();
            long revision = world.Vision.Revision;
            for (int i = 0; i < 20; i++)
            { Accepted(world.Submit(new StopCommand(1, new[] { 2 }))); oracle.Check(); }
            Assert.That(world.Vision.Revision, Is.EqualTo(revision));
            Accepted(world.Submit(new MoveCommand(1, new[] { 3 }, new SimPoint(12500, 8500)))); oracle.Check();
            for (int tick = 0; tick < 80; tick++) { world.Tick(); oracle.Check(); }
            Assert.That(world.Vision.Revision, Is.GreaterThan(revision));
            Accepted(world.Submit(new MoveCommand(1, new[] { 2 }, new SimPoint(16500, 12500)))); oracle.Check();
            for (int tick = 0; tick < 80; tick++) { world.Tick(); oracle.Check(); }
        }

        [Test]
        public void AcceptedFoundationAndTrainedUnitsContributeVisionImmediately()
        {
            var definitions = Definitions(); definitions.Buildings[1].VisionCells = 7;
            definitions.Units[2].VisionCells = 9;
            var world = Create(definitions); var oracle = new CellOracle(world); oracle.Check();
            var result = world.Submit(new BuildCommand(1, new[] { 1 }, "house", new SimPoint(5500, 8500)));
            Accepted(result); oracle.Check();
            Assert.That(world.Vision.IsVisible(1, new SimPoint(12500, 8500)), Is.True);
            Accepted(world.Submit(new TrainCommand(1, 100, "guard"))); oracle.Check();
            for (int tick = 0; tick < 60; tick++) { world.Tick(); oracle.Check(); }
            Assert.That(Building(world, result.EntityId).IsComplete, Is.True);
            Assert.That(world.Units.Count, Is.EqualTo(6));
        }

        [Test]
        public void UnitAndFoundationDeathsRemoveVisionWhileExplorationPersists()
        {
            var definitions = Definitions(); definitions.Buildings[1].VisionCells = 9; definitions.Buildings[1].MaxHealth = 10;
            definitions.Units[3].VisionCells = 128;
            definitions.Units[3].Attack = new AttackDefinition { Damage = 200, RangeMillimetres = 50000, AcquireRangeMillimetres = 50000, CooldownTicks = 3 };
            var world = Create(definitions); var oracle = new CellOracle(world); oracle.Check();
            var scouted = new SimPoint(11500, 12500);
            Assert.That(world.Vision.IsVisible(1, scouted), Is.True);
            Accepted(world.Submit(new AttackCommand(2, new[] { 4 }, 2))); oracle.Check();
            world.Tick(); oracle.Check();
            Assert.That(world.TryGetUnit(2, out _), Is.False);
            Assert.That(world.Vision.IsVisible(1, scouted), Is.False);
            Assert.That(world.Vision.IsExplored(1, scouted), Is.True);
            var site = world.Submit(new BuildCommand(1, new[] { 1 }, "house", new SimPoint(5500, 8500)));
            Accepted(site); oracle.Check();
            Accepted(world.Submit(new StopCommand(1, new[] { 1 }))); oracle.Check();
            Accepted(world.Submit(new AttackCommand(2, new[] { 4 }, site.EntityId))); oracle.Check();
            for (int tick = 0; tick < 5; tick++) { world.Tick(); oracle.Check(); }
            Assert.That(world.TryGetBuilding(site.EntityId, out _), Is.False);
            Assert.That(world.Vision.IsVisible(1, scouted), Is.False);
            Assert.That(world.Vision.IsExplored(1, scouted), Is.True);
        }

        [Test]
        public void SameStableIdPackMoveAndRedeployMatchesBothSourceKinds()
        {
            var definitions = FactionTestWorldFactory.Definitions(); var map = FactionTestWorldFactory.Map();
            var units = new List<UnitSpawnDefinition>(); foreach (var unit in map.UnitSpawns) if (unit.OwnerId <= 2) units.Add(unit);
            var buildings = new List<BuildingSpawnDefinition>(); foreach (var building in map.BuildingSpawns) if (building.OwnerId <= 2) buildings.Add(building);
            map.UnitSpawns = units.ToArray(); map.BuildingSpawns = buildings.ToArray();
            map.PlayerFactions = new[] { map.PlayerFactions[0], map.PlayerFactions[1] };
            map.OfflineMatch = new OfflineMatchDefinition { Enabled = true, CentralBuildingId = "hall", VisionUnitCells = 2, VisionBuildingCells = 3 };
            foreach (var definition in definitions.Buildings) if (definition.Id == "outpost") definition.VisionCells = 9;
            foreach (var definition in definitions.Units) if (definition.Id == "wagon") definition.VisionCells = 1;
            var world = new World(definitions, map);
            foreach (var unit in world.Units) Accepted(world.Submit(new StopCommand(unit.OwnerId, new[] { unit.Id })));
            var oracle = new CellOracle(world); oracle.Check();
            var far = new SimPoint(30500, 12500); Assert.That(world.Vision.IsVisible(2, far), Is.True);
            Accepted(world.Submit(new PackOutpostCommand(2, 120))); oracle.Check();
            for (int tick = 0; tick < 3; tick++) { world.Tick(); oracle.Check(); }
            Assert.That(world.TryGetUnit(120, out var cart), Is.True);
            Assert.That(world.Vision.IsVisible(2, far), Is.False);
            Assert.That(world.Vision.IsExplored(2, far), Is.True);
            Accepted(world.Submit(new MoveCommand(2, new[] { 120 }, new SimPoint(18500, 12500)))); oracle.Check();
            for (int tick = 0; tick < 100 && cart.Order != UnitOrder.Idle; tick++) { world.Tick(); oracle.Check(); }
            Assert.That(cart.Position, Is.EqualTo(new SimPoint(18500, 12500)));
            Accepted(world.Submit(new DeployOutpostCommand(2, 120, new SimPoint(18500, 13500)))); oracle.Check();
            for (int tick = 0; tick < 3; tick++) { world.Tick(); oracle.Check(); }
            Assert.That(world.TryGetBuilding(120, out var relocated), Is.True);
            Assert.That(relocated.Position, Is.EqualTo(new SimPoint(18500, 13500)));
            Assert.That(world.DeathCount, Is.Zero);
        }

        [Test]
        public void ResolvedRadiusChangesAreNotMistakenForStationarySources()
        {
            var definitions = Definitions(); var world = Create(definitions); var oracle = new CellOracle(world); oracle.Check();
            var edge = new SimPoint(13500, 12500); Assert.That(world.Vision.IsVisible(1, edge), Is.False);
            definitions.Units[1].VisionCells = 7;
            Accepted(world.Submit(new StopCommand(1, new[] { 2 }))); oracle.Check();
            Assert.That(world.Vision.IsVisible(1, edge), Is.True);
            definitions.Units[1].VisionCells = 0;
            world.Tick(); oracle.Check();
            Assert.That(world.Vision.IsVisible(1, edge), Is.False);
            Assert.That(world.Vision.IsExplored(1, edge), Is.True);
        }

        // Independent cell-first union predicate; never reads cached stamps, private masks or diagnostic hooks.
        private sealed class CellOracle
        {
            private readonly World world;
            private readonly Dictionary<string, UnitDefinition> unitDefinitions = new Dictionary<string, UnitDefinition>();
            private readonly Dictionary<string, BuildingDefinition> buildingDefinitions = new Dictionary<string, BuildingDefinition>();
            private readonly Dictionary<int, bool[]> explored = new Dictionary<int, bool[]>();
            internal CellOracle(World world)
            {
                this.world = world;
                foreach (var definition in world.Definition.Units) unitDefinitions.Add(definition.Id, definition);
                foreach (var definition in world.Definition.Buildings) buildingDefinitions.Add(definition.Id, definition);
                foreach (int player in world.Match.PlayerIds) explored.Add(player, new bool[world.Map.WidthCells * world.Map.HeightCells]);
            }
            internal void Check()
            {
                int cell = world.Map.CellSizeMillimetres;
                foreach (int player in world.Match.PlayerIds)
                for (int z = 0; z < world.Map.HeightCells; z++) for (int x = 0; x < world.Map.WidthCells; x++)
                {
                    var point = new SimPoint(x * cell + cell / 2, z * cell + cell / 2);
                    bool expected = Expected(player, x, z, point), actual = world.Vision.IsVisible(player, point);
                    if (actual != expected) Assert.Fail("Visibility mismatch for player " + player + " cell " + x + "," + z + " at tick " + world.TickIndex);
                    bool known = world.Vision.IsExplored(player, point); int index = z * world.Map.WidthCells + x;
                    if (!known && (expected || explored[player][index])) Assert.Fail("Exploration was lost at " + x + "," + z);
                    explored[player][index] = known;
                }
                CheckCopy();
            }
            // The bulk read presentation draws the fog and minimap from must give the per-point answers for every cell.
            private void CheckCopy()
            {
                int cell = world.Map.CellSizeMillimetres, count = world.Map.WidthCells * world.Map.HeightCells;
                var copied = new byte[count];
                foreach (int player in world.Match.PlayerIds)
                {
                    Assert.That(world.Vision.CopyCells(player, copied), Is.True);
                    for (int z = 0, index = 0; z < world.Map.HeightCells; z++) for (int x = 0; x < world.Map.WidthCells; x++, index++)
                    {
                        var point = new SimPoint(x * cell + cell / 2, z * cell + cell / 2);
                        int expected = (world.Vision.IsVisible(player, point) ? 2 : 0) | (world.Vision.IsExplored(player, point) ? 1 : 0);
                        if (copied[index] != expected) Assert.Fail("CopyCells disagrees for player " + player + " cell " + x + "," + z + " at tick " + world.TickIndex);
                    }
                }
                Assert.That(world.Vision.CopyCells(99, copied), Is.False, "A player the fog does not track has no cells.");
                Assert.That(world.Vision.CopyCells(1, new byte[count - 1]), Is.False, "Only a whole map is copied.");
            }
            private bool Expected(int player, int x, int z, SimPoint point)
            {
                int cell = world.Map.CellSizeMillimetres;
                foreach (var unit in world.Units) if (unit.OwnerId == player)
                {
                    int radius = unitDefinitions[unit.DefinitionId].VisionCells;
                    if (Circle(x, z, unit.Position.X / cell, unit.Position.Z / cell, radius == 0 ? world.Map.OfflineMatch.VisionUnitCells : radius)) return true;
                }
                foreach (var building in world.Buildings) if (building.OwnerId == player)
                {
                    int radius = buildingDefinitions[building.DefinitionId].VisionCells;
                    if (Circle(x, z, building.Position.X / cell, building.Position.Z / cell, radius == 0 ? world.Map.OfflineMatch.VisionBuildingCells : radius)) return true;
                    long left = building.Position.X - (long)building.WidthCells * cell / 2;
                    long bottom = building.Position.Z - (long)building.DepthCells * cell / 2;
                    if (point.X >= left && point.X < left + building.WidthCells * (long)cell && point.Z >= bottom && point.Z < bottom + building.DepthCells * (long)cell) return true;
                }
                return false;
            }
            private static bool Circle(int x, int z, int centerX, int centerZ, int radius)
            { long dx = x - centerX, dz = z - centerZ; return dx * dx + dz * dz <= (long)radius * radius; }
        }
    }
}
