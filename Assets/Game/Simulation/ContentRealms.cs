using System;

namespace Emberfield.Simulation
{
    /// <summary>Canonical competitive content membership. Cosmetics never participate in these rules.</summary>
    public static class ContentRealms
    {
        public const string Historical = "historical";
        public const string Fantasy = "fantasy";
        public const string Naval = "naval";
        public static string[] All => new[] { Historical, Fantasy, Naval };
        public static bool IsValidRealm(string id) => id == Historical || id == Fantasy || id == Naval;
        public static string DisplayName(string realmId) => realmId == Historical ? "Históricas" :
            realmId == Fantasy ? "Fantasía" : realmId == Naval ? "Navales" : "Desconocido";
        public static string RealmForFaction(string id)
        {
            switch (id)
            {
                case "aven": case "serevin": case "english": case "sultanate": case "sahel": case "miraj": return Historical;
                case "solar": case "verdant": case "ashen": case "drakeforged": case "skeld": return Fantasy;
                case "pirates": case "english_navy": case "spanish_navy": return Naval;
                default: return null;
            }
        }
        public static bool IsFactionInRealm(string factionId, string realmId) => IsValidRealm(realmId) && RealmForFaction(factionId) == realmId;
        // Legacy faction definitions remain readable; only these rosters are offered for new matches.
        // The Skeleton Fleet stays out of the naval roster until its crews exist (docs/design/NAVAL_SLICE.md).
        public static string[] FactionsForRealm(string realmId) => realmId == Historical ? new[] { "aven", "serevin", "english", "sultanate", "sahel" } :
            realmId == Fantasy ? new[] { "ashen", "drakeforged", "skeld", "verdant" } :
            realmId == Naval ? new[] { "pirates", "english_navy", "spanish_navy" } : Array.Empty<string>();
        public static bool IsPlayableFactionInRealm(string factionId, string realmId) =>
            Array.IndexOf(FactionsForRealm(realmId), factionId) >= 0;
        // The Lands of Legend dress each start in its culture's land, so only the fantasy realm plays them.
        public static bool IsMapAllowedInRealm(string mapId, string realmId) => IsValidRealm(realmId) &&
            (mapId == "sapphire_coast" || (realmId != Naval && (mapId == "amber_crossing" || mapId == "sunscar_basin")) ||
            (realmId == Fantasy && mapId == "legend_lands"));
        public static string DefaultMapForRealm(string realmId) => realmId == Naval ? "sapphire_coast" : realmId == Fantasy ? "legend_lands" : "amber_crossing";
        /// <summary>Storage variant only: the connected naval coast keeps the same public battlefield ID.</summary>
        public static string MapResourceId(string mapId, string realmId) =>
            mapId == "sapphire_coast" && realmId == Naval ? "sapphire_coast_naval" : mapId;
        public static string StartingWorkerForFaction(string factionId) => factionId == "pirates" ? "treasure_seeker" : "tender";
        // The pirates put their own worker and their own sloop in place of the shared tender and galley; each navy
        // sails its own hull (the frigate, the galleon) instead of the galley and keeps the kingdom's tender.
        public static bool IsUnitInFactionRoster(string unitId, string factionId) =>
            factionId == "pirates" ? unitId != "tender" && unitId != "war_galley" :
            factionId == "english_navy" || factionId == "spanish_navy" ? unitId != "war_galley" : true;

        /// <summary>Resolve the shared map's starter workers after player factions have been assigned.</summary>
        public static void PrepareStartingUnits(MapDefinition map)
        {
            if (map == null) throw new ArgumentNullException(nameof(map));
            foreach (var spawn in map.UnitSpawns ?? Array.Empty<UnitSpawnDefinition>())
            {
                if (spawn.DefinitionId != "tender") continue;
                foreach (var player in map.PlayerFactions ?? Array.Empty<PlayerFactionDefinition>())
                    if (player.PlayerId == spawn.OwnerId)
                    { spawn.DefinitionId = StartingWorkerForFaction(player.FactionId); break; }
            }
        }
        // The offline rival. Aven and Serevin meet each other, as do the two desert factions; the English keep the
        // Aven rival they had before the desert pair joined the historical roster. The rest take the next faction.
        public static string OpponentFaction(string factionId)
        {
            switch (factionId)
            {
                case "aven": return "serevin"; case "serevin": return "aven"; case "english": return "aven";
                case "sultanate": return "sahel"; case "sahel": return "sultanate";
            }
            string realm = RealmForFaction(factionId);
            if (realm == null) return null;
            var ids = FactionsForRealm(realm);
            int index = Array.IndexOf(ids, factionId);
            return index < 0 ? null : ids[(index + 1) % ids.Length];
        }
    }
}
