namespace Emberfield.Localization
{
    /// <summary>
    /// The game in English. Most screens already compose English; this catalog covers what was written in
    /// Spanish: realm and faction names, pirate content and newer lines.
    /// </summary>
    public static class EnglishCatalog
    {
        public static TextTranslator Create()
        {
            var t = new TextTranslator(GameLanguage.English);
            T(t,
                "{x} (legado)", "{x} (legacy)",
                "BEGIN {r} BATTLE  ›", "BEGIN {r:upper} BATTLE  ›",
                "{r} REALM", "{r:upper} REALM",
                "Train {u}", "Train {u}",
                "{u} queued.", "{u} queued.",
                "Play {f}", "Play {f}",
                "{f} drill. Train your unique unit at the Muster Hall; select infrastructure for faction actions.", "{f} drill. Train your unique unit at the Muster Hall; select infrastructure for faction actions.",
                "{u}: limit {n:int} alive or in training per player.", "{u}: limit {n} alive or in training per player.",
                "PvP: {r}", "PvP: {r}",
                "Faction: {f}", "Faction: {f}");
            P(t,
                "Históricas", "Historical", "Fantasía", "Fantasy", "Navales", "Naval", "Desconocido", "Unknown",
                "Franceses", "French", "Hispanos", "Spanish", "Ingleses", "English", "Hombres de las montañas", "Mountain Men",
                "Elfos", "Elves", "Orcos", "Orcs", "Enanos", "Dwarves", "Piratas", "Pirates",
                "Sultanato", "Sultanate", "Confederación del Sahel", "Sahel Confederation",
                "CARAVANAS & ARQUEROS MONTADOS", "CARAVANS & HORSE ARCHERS", "SABANA & CABALLERÍA PESADA", "SAVANNA & HEAVY CAVALRY",
                "Canteras de piedra y caballería veloz. Hostiga a la infantería con Arqueros de camello.",
                "Stone quarries and swift cavalry. Harry infantry with Camel Archers.",
                "Graneros y escudos de mimbre. Arrolla a los arqueros con Jinetes acolchados.",
                "Granaries and woven shields. Ride down archers with Quilted Lancers.",
                "Caravanas y canteras del desierto. Los Aldeanos recolectan una piedra más por ciclo y la caballería se mueve un diez por ciento más rápido. El Arquero de camello hostiga a la infantería desde la silla; la caballería enemiga lo alcanza y los arqueros a pie lo superan en alcance.",
                "Desert caravans and quarries. Tenders gather one extra stone per operation and cavalry move ten percent faster. The Camel Archer harries infantry from the saddle; enemy cavalry catches it and foot archers outrange it.",
                "Pastores y jinetes de la sabana. Los Aldeanos recolectan una comida más por ciclo y la infantería gana un punto de armadura por sus escudos de mimbre. El Jinete acolchado, con armadura de algodón, arrolla a los arqueros a pie y a caballo; las lanzas lo detienen.",
                "Savanna herders and riders. Tenders gather one extra food per operation and infantry gain one armor from their woven shields. The Quilted Lancer, in cotton armour, rides down archers on foot and on horseback; spears stop it.",
                "Marina inglesa", "English Navy", "Marina española", "Spanish Navy", "Flota esquelética", "Skeleton Fleet",
                "Corsario Carmesí", "Crimson Corsair", "Saqueador de abordaje", "Boarding Raider", "Corsario de pólvora", "Gunpowder Corsair",
                "Buscadora de tesoros", "Treasure Seeker",
                "ARQUERÍA & DEFENSA", "ARCHERY & DEFENSE", "SABLES, PÓLVORA & EXPLORACIÓN", "SABRES, GUNPOWDER & EXPLORATION",
                "PLANEADA / NO DISPONIBLE", "PLANNED / UNAVAILABLE",
                "No disponible: faltan ejércitos y reglas propias de estas flotas.", "Unavailable: these fleets still need their own armies and rules.",
                "Disponible en Navales: muelles, balandras y desembarcos en Sapphire Coast.", "Available in Naval: docks, sloops and landings on Sapphire Coast.",
                "Modelos del reino compartidos.", "Shared kingdom models.", "Modelos propios pendientes.", "Own models pending.",
                "Organiza tu economía y protege a tus arqueros con una línea de infantería.", "Organise your economy and shield your archers with an infantry line.",
                "Capitán, saqueadores, pistoleros y buscadoras de tesoros. Construye un muelle, entrena balandras y transporta tropas por la costa.",
                "Captain, raiders, gunners and treasure seekers. Build a dock, train sloops and carry troops along the coast.",
                "Ejército del reino con infantería, arqueros y caballería. Sus tropas a distancia reciben un punto de armadura. La identidad visual nacional está pendiente.",
                "A kingdom army of infantry, archers and cavalry. Its ranged troops gain one armor. The national visual identity is pending.",
                "Capitán caribeño, saqueadores de abordaje, corsarios de pólvora y buscadoras de tesoros. Construye muelles para entrenar balandras, combatir en el mar y transportar tropas entre orillas.",
                "A Caribbean captain, boarding raiders, gunpowder corsairs and treasure seekers. Build docks to train sloops, fight at sea and carry troops between shores.",
                "Elige tu facción", "Choose your faction",
                "Históricas, fantasía y navales: tres PvP separados.", "Historical, fantasy and naval: three separate PvP ladders.",
                "NO DISPONIBLE", "UNAVAILABLE", "SELECCIONADA", "SELECTED", "ELEGIR FACCIÓN", "CHOOSE FACTION",
                "Práctica con recursos iniciales iguales. Cada ámbito tiene sus propios oponentes.", "Practice with equal starting resources. Each realm has its own opponents.",
                "Históricas, fantasía y navales nunca se enfrentan entre sí.", "Historical, fantasy and naval armies never face one another.",
                "TODAS", "ALL", "Todavía no hay apariencias de este ámbito.", "This realm has no appearances yet.",
                "Límite de héroe alcanzado", "Hero limit reached", "Héroe pirata", "Pirate hero", "Combate con sable", "Sabre combat",
                "Corsario Carmesí seleccionado.", "Crimson Corsair selected.", "Infantería de abordaje", "Boarding infantry", "Pistolero", "Gunner",
                "Exploradora y recolectora", "Scout and gatherer", "Pistola", "Pistol", "recarga entre disparos", "reloads between shots",
                "PARTIDA LOCAL", "LOCAL MATCH",
                "Piratas contra piratas en Sapphire Coast: muelles, balandras y desembarcos.", "Pirates against pirates on Sapphire Coast: docks, sloops and landings.",
                "Marina inglesa, Marina española y Flota esquelética siguen bloqueadas; faltan ejércitos y reglas propias.",
                "The English Navy, Spanish Navy and Skeleton Fleet remain locked; their own armies and rules are still missing.",
                "Conquest: destruye los Hearths. Dominion: controla los faros.", "Conquest: destroy the Hearths. Dominion: hold the beacons.",
                "Faltan ejércitos y reglas propias de estas flotas.", "These fleets still need their own armies and rules.",
                "Históricas, fantasía y navales tienen oponentes, rangos e historial separados.", "Historical, fantasy and naval play have separate opponents, ranks and history.",
                "PARTIDAS RECIENTES", "RECENT MATCHES",
                "Históricas, fantasía y navales tienen PvP separados. Elige el ámbito de la sala antes de entrar.", "Historical, fantasy and naval PvP are separate. Choose the room's realm before joining.",
                "PERSONAJES", "CHARACTERS", "Históricas. Fantasía. Navales.", "Historical. Fantasy. Naval.", "Tres ámbitos PvP separados.", "Three separate PvP realms.",
                "Corsario Carmesí: selecciona terreno para moverte o un enemigo para atacar. Héroe de los Piratas; reclutable en su Hearth en Navales.",
                "Crimson Corsair: choose ground to move or an enemy to attack. The Pirates' hero; recruit it at their Hearth in naval play.",
                "Tripulación pirata: sable, pistola y exploración. La buscadora recolecta y construye; recluta más en el Hearth y el Muster Hall de los Piratas, en Navales.",
                "Pirate crew: sabre, pistol and scouting. The seeker gathers and builds; recruit more at the Pirates' Hearth and Muster Hall in naval play.",
                "Español", "Spanish");
            return t;
        }

        private static void P(TextTranslator t, params string[] pairs) { for (int i = 0; i + 1 < pairs.Length; i += 2) t.Add(pairs[i], pairs[i + 1]); }
        private static void T(TextTranslator t, params string[] pairs) { for (int i = 0; i + 1 < pairs.Length; i += 2) t.Template(pairs[i], pairs[i + 1]); }
    }
}
