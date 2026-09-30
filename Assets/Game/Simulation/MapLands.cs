using System;
using System.Collections.Generic;
using System.Text;

namespace Emberfield.Simulation
{
    /// <summary>
    /// The lands of a map, resolved once for a match. A map that carries <see cref="MapDefinition.ZoneRuns"/> splits
    /// its cells into a neutral zone (0) and starting lands (1-9); a land wears the culture of the player whose starting
    /// hearth stands in it. The owner is found by position, never by player number, because an online replica
    /// renumbers its seats so that the local player is always 1. Presentation only: navigation, vision and the rules
    /// never read it, and a map without zones is a single land of its own biome.
    /// </summary>
    public sealed class MapLands
    {
        public const int Neutral = 0;
        public const int MaxZone = 9;
        public const string Elven = "elven", Highland = "highland", Volcanic = "volcanic";
        // Landmark slots a map with lands places; each becomes the monument of the land it stands in.
        public const string MonumentSlot = "land_monument", PeakSlot = "land_peak";

        private readonly byte[] zones;
        private readonly string[] biomes = new string[MaxZone + 1];
        private readonly int[] owners = new int[MaxZone + 1];
        public int Width { get; }
        public int Height { get; }
        public int CellSizeMillimetres { get; }
        public string MapBiome { get; }
        public bool HasLands => zones != null;
        /// <summary>Every biome the map shows, the map's own first; one entry on a map without lands.</summary>
        public IReadOnlyList<string> Biomes { get; }

        private MapLands(MapDefinition map, byte[] zones)
        {
            Width = map.WidthCells; Height = map.HeightCells; CellSizeMillimetres = map.CellSizeMillimetres;
            MapBiome = string.IsNullOrEmpty(map.BiomeId) ? "forest" : map.BiomeId;
            this.zones = zones;
            for (int zone = 0; zone <= MaxZone; zone++) biomes[zone] = MapBiome;
            var shown = new List<string> { MapBiome };
            if (zones != null)
            {
                string central = map.OfflineMatch?.CentralBuildingId;
                if (string.IsNullOrEmpty(central)) central = "hearth";
                // The first starting hearth found in a land claims it; a baked map puts exactly one in each.
                foreach (var spawn in map.BuildingSpawns ?? Array.Empty<BuildingSpawnDefinition>())
                {
                    if (spawn == null || spawn.DefinitionId != central) continue;
                    int zone = ZoneAt(spawn.Position);
                    if (zone != Neutral && owners[zone] == 0) owners[zone] = spawn.OwnerId;
                }
                var present = new bool[MaxZone + 1];
                foreach (byte zone in zones) present[zone] = true;
                for (int zone = 1; zone <= MaxZone; zone++)
                {
                    if (owners[zone] == 0) continue;
                    string faction = null;
                    foreach (var player in map.PlayerFactions ?? Array.Empty<PlayerFactionDefinition>())
                        if (player != null && player.PlayerId == owners[zone]) { faction = player.FactionId; break; }
                    biomes[zone] = CultureBiome(faction);
                    if (present[zone] && !shown.Contains(biomes[zone])) shown.Add(biomes[zone]);
                }
            }
            Biomes = shown.AsReadOnly();
        }

        /// <summary>Resolves the map's lands for the factions and starts it carries now.</summary>
        public static MapLands Resolve(MapDefinition map)
        {
            if (map == null) throw new ArgumentNullException(nameof(map));
            return new MapLands(map, string.IsNullOrEmpty(map.ZoneRuns) ? null : DecodeRuns(map.ZoneRuns, map.WidthCells, map.HeightCells));
        }

        /// <summary>The land a culture's players start in: the elves' forest, the orcs' volcanic waste, everyone else's highland.</summary>
        public static string CultureBiome(string factionId) =>
            factionId == "verdant" ? Elven : factionId == "ashen" ? Volcanic : Highland;

        public int ZoneAt(int x, int z) => zones == null || x < 0 || z < 0 || x >= Width || z >= Height ? Neutral : zones[z * Width + x];
        public int ZoneAt(SimPoint point) => ZoneAt(Cell(point.X), Cell(point.Z));
        public string BiomeAt(int x, int z) => biomes[ZoneAt(x, z)];
        public string BiomeAt(SimPoint point) => biomes[ZoneAt(point)];
        public string BiomeOf(int zone) => zone < 0 || zone > MaxZone ? MapBiome : biomes[zone];
        /// <summary>The player whose starting hearth stands in this land, or 0 for the neutral zone and an unclaimed land.</summary>
        public int OwnerOf(int zone) => zone <= Neutral || zone > MaxZone ? 0 : owners[zone];
        /// <summary>The land a player starts in, or the neutral zone for a player with none.</summary>
        public int ZoneOf(int playerId)
        {
            for (int zone = 1; zone <= MaxZone; zone++) if (playerId != 0 && owners[zone] == playerId) return zone;
            return Neutral;
        }
        private int Cell(int millimetres) => millimetres < 0 ? -1 : millimetres / Math.Max(1, CellSizeMillimetres);

        /// <summary>The landmark to draw for a map landmark: a land slot becomes the monument of the land it stands in.</summary>
        public string LandmarkKind(MapLandmarkDefinition landmark) =>
            landmark == null ? null : LandmarkKind(landmark.Kind, BiomeAt(landmark.Position));

        public static string LandmarkKind(string kind, string biome)
        {
            if (kind == MonumentSlot) return biome == Elven ? "moonwell" : biome == Volcanic ? "spiked_tower" : "standing_stones";
            if (kind == PeakSlot) return biome == Volcanic ? "volcano" : "mountain";
            return kind;
        }

        /// <summary>The biome whose scenery holds a land monument: a peak in the elven forest is the highland's massif.</summary>
        public static string LandmarkBiome(string kind, string biome) =>
            kind == "moonwell" ? Elven : kind == "spiked_tower" || kind == "volcano" ? Volcanic :
            kind == "standing_stones" || kind == "mountain" ? Highland : biome;

        /// <summary>Row-major zone runs, "zone:count" separated by spaces, e.g. "1:40 0:30 2:74".</summary>
        public static string EncodeRuns(byte[] cells)
        {
            if (cells == null || cells.Length == 0) return "";
            var text = new StringBuilder();
            int start = 0;
            for (int i = 1; i <= cells.Length; i++)
            {
                if (i < cells.Length && cells[i] == cells[start]) continue;
                if (cells[start] > MaxZone) throw new ArgumentException("Zones run from 0 to " + MaxZone + ".");
                if (text.Length > 0) text.Append(' ');
                text.Append(cells[start]).Append(':').Append(i - start);
                start = i;
            }
            return text.ToString();
        }

        public static byte[] DecodeRuns(string runs, int width, int height)
        {
            if (width < 1 || height < 1) throw new ArgumentException("Map zones need a map with cells.");
            var cells = new byte[width * height];
            int filled = 0;
            foreach (string run in runs.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
            {
                int colon = run.IndexOf(':');
                if (colon != 1 || run[0] < '0' || run[0] > '0' + MaxZone ||
                    !int.TryParse(run.Substring(2), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out int count) ||
                    count < 1 || count > cells.Length - filled)
                    throw new ArgumentException("Map zones must be runs of zone:count covering every cell once.");
                byte zone = (byte)(run[0] - '0');
                for (int i = 0; i < count; i++) cells[filled++] = zone;
            }
            if (filled != cells.Length) throw new ArgumentException("Map zones must be runs of zone:count covering every cell once.");
            return cells;
        }
    }
}
