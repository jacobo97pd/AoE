using System;

namespace Emberfield.Simulation
{
    public enum ResourceKind { Food, Wood, Metal, Stone }

    /// <summary>
    /// Which grid a unit moves on. Land units walk the cells nothing blocks; ships sail deep water, the map's water
    /// cells that are also blocked to feet. A ford or a bridge (water nothing blocks) stays land.
    /// </summary>
    public enum MovementDomain { Land = 0, Water = 1 }

    [Serializable]
    public sealed class GameDefinition
    {
        public UnitDefinition[] Units = Array.Empty<UnitDefinition>();
        public BuildingDefinition[] Buildings = Array.Empty<BuildingDefinition>();
        public ResourceDefinition[] Resources = Array.Empty<ResourceDefinition>();
        public EraDefinition[] Eras = Array.Empty<EraDefinition>();
        public TechnologyDefinition[] Technologies = Array.Empty<TechnologyDefinition>();
        public FactionDefinition[] Factions = Array.Empty<FactionDefinition>();
        public ResourceAmount StartingResources;
        public int BasePopulationCapacity = 10;
        public int MaxProductionQueue = 5;
    }

    [Serializable]
    public sealed class UnitDefinition
    {
        public string Id;
        public string DisplayName;
        public int MaxHealth = 100;
        public int MoveSpeedMillimetresPerSecond = 3000;
        public int RadiusMillimetres = 250;
        public bool IsWorker;
        public ResourceAmount Cost;
        public int PopulationCost = 1;
        public int TrainTicks = 100;
        public int CarryCapacity = 10;
        public int GatherIntervalTicks = 20;
        public int GatherAmount = 1;
        public CombatTags Tags;
        public int Armor;
        public AttackDefinition Attack = new AttackDefinition();
        public string RequiredEraId;
        public string[] RequiredTechnologyIds = Array.Empty<string>();
        public string RequiredFactionId;
        public string RequiredRealmId;
        // Zero is unlimited. A positive limit includes living units and every production queue.
        public int MaxAlivePerPlayer;
        public bool IsThreadkeeper;
        public bool CanReposition;
        public int VisionCells;
        public SiegeEquipmentKind SiegeEquipment;
        public int RegenerationPerSecond;
        // Ships: Water sails deep water and trains beside it; a positive CargoCapacity carries that many land units.
        public MovementDomain Domain;
        public int CargoCapacity;
    }

    [Serializable]
    public sealed class BuildingDefinition
    {
        public string Id;
        public string DisplayName;
        public int MaxHealth = 1000;
        public int WidthCells = 3;
        public int DepthCells = 3;
        public ResourceAmount Cost;
        public int BuildTicks = 200;
        public int PopulationCapacity;
        public bool CanDropOff;
        public string[] TrainableUnitIds = Array.Empty<string>();
        public CombatTags Tags = CombatTags.Structure;
        public int Armor;
        public string RequiredEraId;
        public string[] RequiredTechnologyIds = Array.Empty<string>();
        public string RequiredFactionId;
        public bool CanCharter;
        public string TransportUnitId;
        public int VisionCells;
        public bool IsWall;
        public bool IsGate;
        public int WallCapacity = 4;
        public int WallHeightMillimetres = 3000;
        public AttackDefinition Attack = new AttackDefinition();
        public int OilDamage;
        public int OilCooldownTicks = 100;
        // A dock: its footprint must touch deep water, where the ships it trains are launched.
        public bool RequiresShore;
    }

    [Serializable]
    public sealed class ResourceDefinition
    {
        public string Id;
        public string DisplayName;
        public ResourceKind Kind;
        public int InitialAmount = 1500;
    }

    [Serializable]
    public sealed class MapDefinition
    {
        public string Id;
        public string RealmId = "historical";
        public string BiomeId = "forest";
        public int WidthCells = 64;
        public int HeightCells = 48;
        public int CellSizeMillimetres = 1000;
        public GridCell[] BlockedCells = Array.Empty<GridCell>();
        // Scenery the map carries with it, so a generated map dresses itself instead of the presentation
        // hard-coding a shape per biome. Feet, picking and objectives read BlockedCells alone, and a map that omits
        // both still renders through the original biome dressing. Ships sail the water cells that are also blocked
        // (deep water); a water cell left open is a ford or a bridge, which stays land.
        public GridCell[] WaterCells = Array.Empty<GridCell>();
        public MapLandmarkDefinition[] Landmarks = Array.Empty<MapLandmarkDefinition>();
        // The lands a map is divided into, also scenery only: row-major "zone:count" runs, zone 0 the neutral ground
        // and 1-9 the starting lands. MapLands resolves each land's look from the hearth that starts in it; empty on
        // a map of one biome.
        public string ZoneRuns = "";
        public UnitSpawnDefinition[] UnitSpawns = Array.Empty<UnitSpawnDefinition>();
        public BuildingSpawnDefinition[] BuildingSpawns = Array.Empty<BuildingSpawnDefinition>();
        public ResourceSpawnDefinition[] ResourceSpawns = Array.Empty<ResourceSpawnDefinition>();
        public PlayerFactionDefinition[] PlayerFactions = Array.Empty<PlayerFactionDefinition>();
        public OfflineMatchDefinition OfflineMatch;
    }

    [Serializable]
    public sealed class MapLandmarkDefinition
    {
        public string Kind;
        public SimPoint Position;
        public int RotationDegrees;
        public int ScalePermille = 1000;
    }

    [Serializable]
    public struct GridCell
    {
        public int X;
        public int Z;
        public GridCell(int x, int z) { X = x; Z = z; }
    }

    [Serializable]
    public sealed class UnitSpawnDefinition
    {
        public int Id;
        public int OwnerId;
        public string DefinitionId;
        public SimPoint Position;
    }

    [Serializable]
    public sealed class BuildingSpawnDefinition
    {
        public int Id;
        public int OwnerId;
        public string DefinitionId;
        public SimPoint Position;
        // A wall or gate turned a quarter: its footprint swaps WidthCells and DepthCells so it runs north-south.
        public bool Turned;
    }

    [Serializable]
    public sealed class ResourceSpawnDefinition
    {
        public int Id;
        public string DefinitionId;
        public SimPoint Position;
    }
}
