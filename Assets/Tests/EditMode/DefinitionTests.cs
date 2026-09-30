using System;
using Emberfield.Simulation;
using NUnit.Framework;

namespace Emberfield.Tests.EditMode
{
    public sealed class DefinitionTests
    {
        [Test]
        public void EntityLookupsReturnStableConfiguredState()
        {
            var world = TestWorldFactory.Create();
            Assert.That(world.TryGetBuilding(101, out var building), Is.True);
            Assert.That(building.OwnerId, Is.EqualTo(1));
            Assert.That(building.Health, Is.EqualTo(1500));
            Assert.That(world.TryGetResource(201, out var resource), Is.True);
            Assert.That(resource.Kind, Is.EqualTo(ResourceKind.Wood));
            Assert.That(resource.RemainingAmount, Is.EqualTo(1500));
            Assert.That(world.TryGetUnit(999, out _), Is.False);
            Assert.That(world.Units.Count, Is.EqualTo(3));
        }

        [Test]
        public void DuplicateIdsAcrossEntityTypesAreRejected()
        {
            var map = TestWorldFactory.Map();
            map.UnitSpawns[0].Id = map.ResourceSpawns[0].Id;
            Assert.Throws<ArgumentException>(() => new World(TestWorldFactory.Definitions(), map));
        }

        [Test]
        public void UnknownDefinitionAndInvalidOwnerAreRejected()
        {
            var map = TestWorldFactory.Map();
            map.UnitSpawns[0].DefinitionId = "missing";
            Assert.Throws<ArgumentException>(() => new World(TestWorldFactory.Definitions(), map));
            map.UnitSpawns[0].DefinitionId = "worker";
            map.UnitSpawns[0].OwnerId = 0;
            Assert.Throws<ArgumentException>(() => new World(TestWorldFactory.Definitions(), map));
        }

        [Test]
        public void InvalidAndDuplicateDefinitionsAreRejectedBeforeSimulationStarts()
        {
            var definitions = TestWorldFactory.Definitions();
            definitions.Units[0].MoveSpeedMillimetresPerSecond = 0;
            Assert.Throws<ArgumentException>(() => new World(definitions, TestWorldFactory.Map()));
            definitions = TestWorldFactory.Definitions();
            definitions.Units = new[] { definitions.Units[0], definitions.Units[0] };
            Assert.Throws<ArgumentException>(() => new World(definitions, TestWorldFactory.Map()));
        }

        [Test]
        public void UnitInsideStaticObstacleIsRejected()
        {
            var map = TestWorldFactory.Map();
            map.UnitSpawns[0].Position = map.ResourceSpawns[0].Position;
            Assert.Throws<ArgumentException>(() => new World(TestWorldFactory.Definitions(), map));
        }

        [Test]
        public void OverlappingOrMisalignedBuildingFootprintsAreRejected()
        {
            var map = TestWorldFactory.Map();
            map.BuildingSpawns[0].Position = new SimPoint(11000, 8500);
            Assert.Throws<ArgumentException>(() => new World(TestWorldFactory.Definitions(), map));
            map = TestWorldFactory.Map();
            map.ResourceSpawns[0].Position = map.BuildingSpawns[0].Position;
            Assert.Throws<ArgumentException>(() => new World(TestWorldFactory.Definitions(), map));
        }

        [TestCase(0, 12, 1000)]
        [TestCase(16, -1, 1000)]
        [TestCase(16, 12, 0)]
        [TestCase(int.MaxValue, int.MaxValue, 1000)]
        public void InvalidMapDimensionsAreRejectedWithoutOverflow(int width, int height, int cellSize)
        {
            var map = TestWorldFactory.Map();
            map.WidthCells = width; map.HeightCells = height; map.CellSizeMillimetres = cellSize;
            Assert.Throws<ArgumentException>(() => new World(TestWorldFactory.Definitions(), map));
        }
    }
}
