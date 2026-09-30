using Emberfield.Simulation;

namespace Emberfield.Tests.EditMode
{
    internal static class TestWorldFactory
    {
        internal static GameDefinition Definitions(int movementSpeed = 3000) => new GameDefinition
        {
            Units = new[]
            {
                new UnitDefinition { Id = "worker", DisplayName = "Hearth Tender", IsWorker = true,
                    MaxHealth = 100, MoveSpeedMillimetresPerSecond = movementSpeed, RadiusMillimetres = 250 }
            },
            Buildings = new[]
            {
                new BuildingDefinition { Id = "hall", DisplayName = "Gathering Hall", MaxHealth = 1500, WidthCells = 3, DepthCells = 3 }
            },
            Resources = new[]
            {
                new ResourceDefinition { Id = "timber", DisplayName = "Copperleaf Grove", Kind = ResourceKind.Wood, InitialAmount = 1500 }
            }
        };

        internal static MapDefinition Map() => new MapDefinition
        {
            Id = "test-field", WidthCells = 16, HeightCells = 12, CellSizeMillimetres = 1000,
            UnitSpawns = new[]
            {
                new UnitSpawnDefinition { Id = 1, OwnerId = 1, DefinitionId = "worker", Position = new SimPoint(1500, 1500) },
                new UnitSpawnDefinition { Id = 2, OwnerId = 1, DefinitionId = "worker", Position = new SimPoint(1500, 2500) },
                new UnitSpawnDefinition { Id = 3, OwnerId = 2, DefinitionId = "worker", Position = new SimPoint(3500, 2500) }
            },
            BuildingSpawns = new[]
            {
                new BuildingSpawnDefinition { Id = 101, OwnerId = 1, DefinitionId = "hall", Position = new SimPoint(11500, 8500) }
            },
            ResourceSpawns = new[]
            {
                new ResourceSpawnDefinition { Id = 201, DefinitionId = "timber", Position = new SimPoint(6500, 6500) }
            }
        };

        internal static World Create(int movementSpeed = 3000) => new World(Definitions(movementSpeed), Map());
        internal static UnitState Unit(World world, int id = 1)
        {
            if (!world.TryGetUnit(id, out var result)) throw new System.InvalidOperationException("Test unit missing.");
            return result;
        }
        internal static void Tick(World world, int count)
        {
            for (int i = 0; i < count; i++) world.Tick();
        }
    }
}
