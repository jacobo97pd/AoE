using System;
using System.Collections.Generic;
using Emberfield.Simulation;

namespace Emberfield.Presentation
{
    public static class FrontierCodex
    {
        public static readonly string[] MapIds = { "amber_crossing", "sapphire_coast", "sunscar_basin", "legend_lands" };
        public static readonly string[] PlannedNavalFactions = { "english_navy", "spanish_navy", "skeleton_fleet" };
        public static string[] VisibleFactions(string realm)
        {
            var ids = new List<string>(ContentRealms.FactionsForRealm(realm));
            if (realm == "naval") ids.AddRange(PlannedNavalFactions);
            return ids.ToArray();
        }
        public static string[] MapsForRealm(string realm) => Array.FindAll(MapIds, id => ContentRealms.IsMapAllowedInRealm(id, realm));
        public static string NextRealm(string realm)
        {
            int index = Array.IndexOf(ContentRealms.All, realm);
            return ContentRealms.All[(index + 1) % ContentRealms.All.Length];
        }
        public static string NextMap(string map, string realm)
        {
            var maps = MapsForRealm(realm);
            return maps.Length == 0 ? null : maps[(Array.IndexOf(maps, map) + 1) % maps.Length];
        }
        public static string Availability(string id) => Array.IndexOf(PlannedNavalFactions, id) >= 0
            ? "No disponible: faltan ejércitos y reglas propias de estas flotas."
            : id == "pirates" ? "Disponible en Navales: muelles, balandras y desembarcos en Sapphire Coast." : "";
        public static string MapName(string id) => id == "sapphire_coast" ? "Sapphire Coast" : id == "sunscar_basin" ? "Sunscar Basin" : id == "legend_lands" ? "Lands of Legend" : "Amber Crossing";
        public static string BiomeName(string id) => id == "sapphire_coast" ? "CARIBBEAN" : id == "sunscar_basin" ? "DESERT" : id == "legend_lands" ? "HOMELANDS" : "TEMPERATE FOREST";
        public static string MapDescription(string id) => id == "sapphire_coast" ? "Palm groves, sheltered coves and contested coastal passages." : id == "sunscar_basin" ? "Oasis routes, sandstone ruins and open dune approaches." :
            id == "legend_lands" ? "Each army starts in its own land: elven forest, highland plains or volcanic wastes." : "River crossings, wooded approaches and fertile frontier valleys.";
        public static string RealmName(string realm) => ContentRealms.DisplayName(realm).ToUpperInvariant();
        /// <summary>The battlefield a faction is suggested first: the desert pair's Sunscar Basin, else its realm's own.</summary>
        public static string HomeMap(string factionId) => factionId == "sultanate" || factionId == "sahel" ? "sunscar_basin"
            : ContentRealms.DefaultMapForRealm(ContentRealms.RealmForFaction(factionId));
        public static string Name(string id)
        {
            switch (id)
            {
                case "aven": return "Franceses"; case "serevin": return "Hispanos"; case "english": return "Ingleses";
                case "sultanate": return "Sultanato"; case "sahel": return "Confederación del Sahel";
                case "miraj": return "Miraj Sultanate (legado)"; case "skeld": return "Hombres de las montañas";
                case "solar": return "Solar Kingdom (legado)"; case "verdant": return "Elfos";
                case "ashen": return "Orcos"; case "drakeforged": return "Enanos";
                case "pirates": return "Piratas"; case "english_navy": return "Marina inglesa";
                case "spanish_navy": return "Marina española"; case "skeleton_fleet": return "Flota esquelética";
                default: return id;
            }
        }
        public static string Identity(string id)
        {
            switch (id)
            {
                case "aven": return "NETWORKS & LOGISTICS"; case "serevin": return "MOBILITY & PRESSURE";
                case "english": return "ARQUERÍA & DEFENSA"; case "pirates": return "SABLES, PÓLVORA & EXPLORACIÓN";
                case "sultanate": return "CARAVANAS & ARQUEROS MONTADOS"; case "sahel": return "SABANA & CABALLERÍA PESADA";
                case "english_navy": case "spanish_navy": case "skeleton_fleet": return "PLANEADA / NO DISPONIBLE";
                case "miraj": return "OASIS & WAR ELEPHANTS"; case "skeld": return "ENDURANCE & INFANTRY";
                case "solar": return "DISCIPLINE & SUN LIONS"; case "verdant": return "ARCHERY & LIVING GROVES";
                case "ashen": return "ATTRITION & WAR TROLLS"; case "drakeforged": return "FORGES & DRAGONS";
                default: return "";
            }
        }
        public static string Description(string id)
        {
            string note = id == "aven" || id == "serevin" || id == "english" ? "Modelos del reino compartidos.\n" :
                id == "ashen" ? "Modelos propios pendientes.\n" : "";
            return note + FactionDescription(id);
        }
        private static string FactionDescription(string id)
        {
            switch (id)
            {
                case "aven": return "Link Storeyard charters and Threadkeeper relays to supply a growing kingdom.";
                case "serevin": return "Relocate Supply Outposts and strike exposed positions with swift Ashrunners.";
                case "english": return "Organiza tu economía y protege a tus arqueros con una línea de infantería.";
                case "sultanate": return "Canteras de piedra y caballería veloz. Hostiga a la infantería con Arqueros de camello.";
                case "sahel": return "Graneros y escudos de mimbre. Arrolla a los arqueros con Jinetes acolchados.";
                case "pirates": return "Capitán, saqueadores, pistoleros y buscadoras de tesoros. Construye un muelle, entrena balandras y transporta tropas por la costa.";
                case "english_navy": case "spanish_navy": case "skeleton_fleet": return Availability(id);
                case "miraj": return "Build an oasis economy and support powerful, costly giant war elephants.";
                case "skeld": return "Hold the frontier with resilient Frostguards and disciplined northern infantry.";
                case "solar": return "Lead a disciplined host alongside armored sun lions and luminous citadels.";
                case "verdant": return "Support woodland archers with living Grove Guardians. Protect your ranged line.";
                case "ashen": return "Break the enemy line with brutal war trolls and relentless close-range pressure.";
                case "drakeforged": return "Invest in mighty Ember Drakes. Their strength demands an expensive economy.";
                default: return "";
            }
        }
        public static string SignatureUnit(string id)
        {
            switch (id)
            {
                case "aven": return "threadkeeper"; case "serevin": return "ashrunner"; case "miraj": return "dune_elephant";
                case "english": return "stringwarden"; case "pirates": return "crimson_corsair";
                case "sultanate": return "camel_archer"; case "sahel": return "quilted_lancer";
                case "skeld": return "frostguard"; case "solar": return "sun_lion"; case "verdant": return "grove_guardian";
                case "ashen": return "war_troll"; case "drakeforged": return "ember_drake"; default: return "reedguard";
            }
        }
    }
}
