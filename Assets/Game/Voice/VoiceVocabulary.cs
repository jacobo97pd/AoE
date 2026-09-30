using System;
using System.Collections.Generic;
using System.Linq;
using Emberfield.Simulation;

namespace Emberfield.Voice
{
    /// <summary>One spoken name for a unit, building or research family, with its plural.</summary>
    public sealed class VoiceNoun
    {
        public string Singular { get; }
        public string Plural { get; }
        public bool Feminine { get; }
        /// <summary>False for names taken from the rules' display names: understood when typed, not offered to the recognizer.</summary>
        public bool Offered { get; }
        internal string[] SingularWords { get; }
        internal string[] PluralWords { get; }

        public VoiceNoun(string singular, string plural = null, bool feminine = false, bool offered = true)
        {
            Singular = singular; Plural = plural ?? singular; Feminine = feminine; Offered = offered;
            SingularWords = VoiceText.Words(Singular); PluralWords = VoiceText.Words(Plural);
        }
    }

    /// <summary>A unit, building or research family and the names it can be called by. Feedback uses the first name.</summary>
    public sealed class VoiceTerm
    {
        public string Id { get; }
        public IReadOnlyList<VoiceNoun> Nouns { get; }
        public VoiceNoun Primary => Nouns[0];
        internal VoiceTerm(string id, List<VoiceNoun> nouns) { Id = id; Nouns = nouns.AsReadOnly(); }
    }

    internal enum VoiceVerbKind { Train, Build, Select, Research, Gather }

    internal sealed class VoiceVerb
    {
        internal readonly string[] Words;
        internal readonly VoiceVerbKind Kind;
        internal VoiceVerb(string words, VoiceVerbKind kind) { Words = VoiceText.Words(words); Kind = kind; }
    }

    /// <summary>
    /// The words for one match and one language: the units, buildings and research this faction can use,
    /// the verbs and fixed commands, and the exact phrases offered to a keyword recognizer.
    /// </summary>
    public sealed class VoiceVocabulary
    {
        public const string Weapons = "weapons", Armor = "armor", Gathering = "gather", Unique = "unique";

        public VoiceLanguage Language { get; }
        public IReadOnlyList<VoiceTerm> Units { get; }
        public IReadOnlyList<VoiceTerm> Buildings { get; }
        public IReadOnlyList<VoiceTerm> ResearchFamilies { get; }
        public IReadOnlyList<string> Help => pack.Help;
        internal IReadOnlyList<VoiceVerb> Verbs { get; }
        private readonly VoiceLanguagePack pack;
        private readonly Dictionary<string, string[]> technologies;
        private readonly Dictionary<string, VoiceAction> fixedPhrases = new Dictionary<string, VoiceAction>(StringComparer.Ordinal);
        private IReadOnlyList<VoicePhrase> phrases;

        private VoiceVocabulary(VoiceLanguage language, List<VoiceTerm> units, List<VoiceTerm> buildings, List<VoiceTerm> research, Dictionary<string, string[]> technologies)
        {
            Language = language; pack = VoiceLanguagePack.For(language);
            Units = units.AsReadOnly(); Buildings = buildings.AsReadOnly(); ResearchFamilies = research.AsReadOnly();
            this.technologies = technologies;
            foreach (var pair in pack.Fixed)
                foreach (string phrase in pair.Value)
                {
                    var words = VoiceText.Words(phrase);
                    string said = string.Join(" ", words);
                    if (!fixedPhrases.ContainsKey(said)) fixedPhrases.Add(said, pair.Key);
                    // Also without articles: "cámara base" and "select army" still count.
                    string bare = string.Join(" ", words.Where(word => !pack.Articles.Contains(word)));
                    if (bare.Length > 0 && !fixedPhrases.ContainsKey(bare)) fixedPhrases.Add(bare, pair.Key);
                }
            var verbs = new List<VoiceVerb>();
            foreach (string verb in pack.TrainVerbs) verbs.Add(new VoiceVerb(verb, VoiceVerbKind.Train));
            foreach (string verb in pack.BuildVerbs) verbs.Add(new VoiceVerb(verb, VoiceVerbKind.Build));
            foreach (string verb in pack.SelectVerbs) verbs.Add(new VoiceVerb(verb, VoiceVerbKind.Select));
            foreach (string verb in pack.ResearchVerbs) verbs.Add(new VoiceVerb(verb, VoiceVerbKind.Research));
            foreach (string verb in pack.GatherVerbs) verbs.Add(new VoiceVerb(verb, VoiceVerbKind.Gather));
            // Longer verbs first, so "a por" is tried before any one-word verb.
            Verbs = verbs.OrderByDescending(verb => verb.Words.Length).ToList().AsReadOnly();
        }

        /// <summary>The vocabulary of one match: only units, buildings and research the faction can use in its realm.</summary>
        public static VoiceVocabulary Build(GameDefinition definition, string factionId, string realmId, VoiceLanguage language)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            var pack = VoiceLanguagePack.For(language);
            string realm = string.IsNullOrEmpty(realmId) ? ContentRealms.Historical : realmId;
            var trainable = new HashSet<string>(StringComparer.Ordinal);
            foreach (var building in definition.Buildings ?? Array.Empty<BuildingDefinition>())
                if (Eligible(building.RequiredFactionId, factionId))
                    foreach (string id in building.TrainableUnitIds ?? Array.Empty<string>()) trainable.Add(id);
            var units = new List<VoiceTerm>();
            foreach (var unit in definition.Units ?? Array.Empty<UnitDefinition>())
            {
                if (!trainable.Contains(unit.Id) || !Eligible(unit.RequiredFactionId, factionId)) continue;
                if (!string.IsNullOrEmpty(unit.RequiredRealmId) && unit.RequiredRealmId != realm) continue;
                units.Add(Term(unit.Id, unit.DisplayName, pack.Nouns));
            }
            var buildings = new List<VoiceTerm>();
            foreach (var building in definition.Buildings ?? Array.Empty<BuildingDefinition>())
                if (Eligible(building.RequiredFactionId, factionId)) buildings.Add(Term(building.Id, building.DisplayName, pack.Nouns));
            var families = new Dictionary<string, string[]>(StringComparer.Ordinal);
            var techs = (definition.Technologies ?? Array.Empty<TechnologyDefinition>()).Where(tech => Eligible(tech.RequiredFactionId, factionId)).ToList();
            void Family(string key, IEnumerable<TechnologyDefinition> members)
            {
                var ids = members.Select(tech => tech.Id).OrderBy(id => id, StringComparer.Ordinal).ToArray();
                if (ids.Length > 0) families[key] = ids;
            }
            Family(Weapons, techs.Where(tech => tech.Id.StartsWith("weapons_", StringComparison.Ordinal)));
            Family(Armor, techs.Where(tech => tech.Id.StartsWith("armor_", StringComparison.Ordinal)));
            Family(Gathering, techs.Where(tech => tech.Id.StartsWith("gather_", StringComparison.Ordinal)));
            if (!string.IsNullOrEmpty(factionId)) Family(Unique, techs.Where(tech => tech.RequiredFactionId == factionId));
            var research = new List<VoiceTerm>();
            foreach (string key in new[] { Weapons, Armor, Gathering, Unique })
                if (families.ContainsKey(key) && pack.ResearchNouns.TryGetValue(key, out var nouns)) research.Add(new VoiceTerm(key, nouns.ToList()));
            return new VoiceVocabulary(language, units, buildings, research, families);
        }

        private static bool Eligible(string required, string factionId) => string.IsNullOrEmpty(required) || required == factionId;

        private static VoiceTerm Term(string id, string displayName, Dictionary<string, VoiceNoun[]> authored)
        {
            var nouns = new List<VoiceNoun>();
            if (authored.TryGetValue(id, out var names)) nouns.AddRange(names);
            // The rules' own name is always understood when typed, even when nobody would say it aloud.
            if (!string.IsNullOrEmpty(displayName) && nouns.All(noun => VoiceText.Normalize(noun.Singular) != VoiceText.Normalize(displayName)))
                nouns.Add(new VoiceNoun(displayName, offered: false));
            if (nouns.Count == 0) nouns.Add(new VoiceNoun(id.Replace('_', ' '), offered: false));
            return new VoiceTerm(id, nouns);
        }

        /// <summary>The unit, building or research family with this id, if this match can use it.</summary>
        public VoiceTerm Find(string id) => Units.Concat(Buildings).Concat(ResearchFamilies).FirstOrDefault(term => term.Id == id);

        /// <summary>The everyday word for a resource: "madera", "wood".</summary>
        public string ResourceName(ResourceKind kind) => pack.Resources[kind][0];

        /// <summary>The technologies of a research family, in the order they unlock.</summary>
        public IReadOnlyList<string> TechnologiesIn(string family) =>
            family != null && technologies.TryGetValue(family, out var ids) ? ids : (IReadOnlyList<string>)Array.Empty<string>();

        internal bool TryFixed(List<string> words, out VoiceAction action)
        {
            if (fixedPhrases.TryGetValue(string.Join(" ", words), out action)) return true;
            return fixedPhrases.TryGetValue(string.Join(" ", words.Where(word => !pack.Articles.Contains(word))), out action);
        }
        internal bool IsArticle(string word) => pack.Articles.Contains(word);
        internal bool IsFiller(string word) => pack.Fillers.Contains(word);
        internal bool TryResource(List<string> words, out ResourceKind resource)
        {
            if (words.Count == 1)
                foreach (var pair in pack.Resources)
                    foreach (string name in pair.Value)
                        if (VoiceText.Forms(words[0]).Contains(VoiceText.Normalize(name))) { resource = pair.Key; return true; }
            resource = ResourceKind.Food; return false;
        }

        /// <summary>
        /// Every phrase a keyword recognizer should listen for, with the command each one means. Phrases
        /// keep their accents; duplicates that differ only by accents or case are dropped.
        /// </summary>
        public IReadOnlyList<VoicePhrase> Phrases()
        {
            if (phrases != null) return phrases;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var list = new List<VoicePhrase>();
            void Add(string text, VoiceIntent intent) { if (seen.Add(VoiceText.Normalize(text))) list.Add(new VoicePhrase(text, intent)); }
            foreach (var pair in pack.Fixed)
                foreach (string phrase in pair.Value) Add(phrase, new VoiceIntent(pair.Key));
            foreach (var term in Units)
                foreach (var noun in term.Nouns.Where(noun => noun.Offered))
                {
                    foreach (string verb in pack.TrainVerbs.Take(2))
                        for (int count = 1; count <= 5; count++) Add(pack.Train(verb, count, noun), new VoiceIntent(VoiceAction.Train, term.Id, count));
                    Add(pack.SelectUnits(noun), new VoiceIntent(VoiceAction.SelectUnits, term.Id));
                }
            foreach (var term in Buildings)
                foreach (var noun in term.Nouns.Where(noun => noun.Offered))
                {
                    foreach (string verb in pack.BuildVerbs.Take(2)) Add(pack.Build(verb, noun), new VoiceIntent(VoiceAction.Build, term.Id));
                    Add(pack.SelectBuilding(noun), new VoiceIntent(VoiceAction.SelectBuilding, term.Id));
                }
            foreach (var term in ResearchFamilies)
            {
                foreach (var noun in term.Nouns.Where(noun => noun.Offered)) Add(pack.ResearchVerbs[0] + " " + noun.Singular, new VoiceIntent(VoiceAction.Research, term.Id));
                Add(pack.ResearchVerbs[1] + " " + term.Primary.Singular, new VoiceIntent(VoiceAction.Research, term.Id));
            }
            foreach (var pair in pack.Resources)
                foreach (string verb in pack.GatherVerbs.Take(3)) Add(verb + " " + pair.Value[0], new VoiceIntent(VoiceAction.Gather, resource: pair.Key));
            return phrases = list.AsReadOnly();
        }
    }

    /// <summary>Everything that differs between languages: verbs, articles, fixed commands, names and phrase shapes.</summary>
    internal sealed class VoiceLanguagePack
    {
        internal string[] TrainVerbs, BuildVerbs, SelectVerbs, ResearchVerbs, GatherVerbs, Help;
        internal HashSet<string> Articles, Fillers;
        internal Dictionary<VoiceAction, string[]> Fixed;
        internal Dictionary<string, VoiceNoun[]> Nouns, ResearchNouns;
        internal Dictionary<ResourceKind, string[]> Resources;
        internal Func<string, int, VoiceNoun, string> Train;
        internal Func<string, VoiceNoun, string> Build;
        internal Func<VoiceNoun, string> SelectUnits, SelectBuilding;

        internal static VoiceLanguagePack For(VoiceLanguage language) => language == VoiceLanguage.Spanish ? Spanish : English;

        private static VoiceNoun N(string singular, string plural = null) => new VoiceNoun(singular, plural);
        private static VoiceNoun F(string singular, string plural = null) => new VoiceNoun(singular, plural, true);
        private static readonly string[] SpanishCounts = { "", "un", "dos", "tres", "cuatro", "cinco" };
        private static readonly string[] EnglishCounts = { "", "a", "two", "three", "four", "five" };
        private static string EnglishArticle(string noun) => "aeiou".IndexOf(char.ToLowerInvariant(noun[0])) >= 0 ? "an" : "a";

        internal static readonly VoiceLanguagePack Spanish = new VoiceLanguagePack
        {
            TrainVerbs = new[] { "entrena", "recluta", "entrenar", "reclutar", "crea", "produce" },
            BuildVerbs = new[] { "construye", "levanta", "construir", "edifica" },
            SelectVerbs = new[] { "selecciona", "seleccionar", "elige" },
            ResearchVerbs = new[] { "investiga", "mejora", "investigar", "desarrolla" },
            GatherVerbs = new[] { "recolecta", "recoge", "a por", "recolectar", "corta", "mina" },
            Articles = new HashSet<string>(StringComparer.Ordinal) { "el", "la", "los", "las", "lo", "al", "a", "unos", "unas", "todos", "todas", "mis", "nuestros", "nuestras" },
            Fillers = new HashSet<string>(StringComparer.Ordinal) { "porfa", "ahora", "vale", "venga", "oye", "ya", "aqui", "mas" },
            Fixed = new Dictionary<VoiceAction, string[]>
            {
                { VoiceAction.Help, new[] { "ayuda", "qué puedo decir", "comandos de voz" } },
                { VoiceAction.CloseHelp, new[] { "cierra la ayuda", "oculta la ayuda" } },
                { VoiceAction.SelectArmy, new[] { "selecciona el ejército", "todo el ejército", "ejército" } },
                { VoiceAction.SelectWorkers, new[] { "selecciona los trabajadores", "todos los trabajadores", "trabajadores" } },
                { VoiceAction.SelectIdleWorkers, new[] { "trabajadores ociosos", "selecciona los ociosos", "aldeanos ociosos" } },
                { VoiceAction.ClearSelection, new[] { "deselecciona", "borra la selección", "quita la selección" } },
                { VoiceAction.Attack, new[] { "ataca aquí", "ataca", "al ataque", "atacad" } },
                { VoiceAction.Move, new[] { "muévete aquí", "ve aquí", "moveos aquí", "id aquí" } },
                { VoiceAction.Stop, new[] { "alto", "detente", "para", "quietos" } },
                { VoiceAction.Retreat, new[] { "retirada", "retiraos", "vuelve a casa", "volved a casa" } },
                { VoiceAction.ReturnCargo, new[] { "entrega los recursos", "descarga", "deja la carga" } },
                { VoiceAction.CycleFormation, new[] { "cambia la formación", "siguiente formación" } },
                { VoiceAction.Confirm, new[] { "confirma", "confirmar", "colócalo aquí" } },
                { VoiceAction.Cancel, new[] { "cancela", "cancelar", "anula" } },
                { VoiceAction.AdvanceEra, new[] { "avanza de era", "siguiente era", "sube de era" } },
                { VoiceAction.CameraHome, new[] { "cámara a la base", "cámara a casa", "centra la base" } },
                { VoiceAction.CameraSelection, new[] { "cámara al ejército", "cámara a la selección", "sigue la selección" } },
                { VoiceAction.CameraRotate, new[] { "gira la cámara", "rota la cámara" } },
                { VoiceAction.ZoomIn, new[] { "acerca la cámara", "acércate", "más cerca" } },
                { VoiceAction.ZoomOut, new[] { "aleja la cámara", "aléjate", "más lejos" } },
                { VoiceAction.Pause, new[] { "pausa", "pausa la partida", "abre el menú" } },
                { VoiceAction.Resume, new[] { "reanuda", "continúa", "reanuda la partida" } },
            },
            Nouns = new Dictionary<string, VoiceNoun[]>(StringComparer.Ordinal)
            {
                { "tender", new[] { N("trabajador", "trabajadores"), N("aldeano", "aldeanos") } },
                { "treasure_seeker", new[] { F("buscadora", "buscadoras"), F("buscadora de tesoros", "buscadoras de tesoros") } },
                { "reedguard", new[] { N("lancero", "lanceros"), N("piquero", "piqueros") } },
                { "stringwarden", new[] { N("arquero", "arqueros") } },
                { "strider", new[] { N("jinete", "jinetes"), N("caballero", "caballeros") } },
                { "threadkeeper", new[] { N("enlace", "enlaces") } },
                { "ashrunner", new[] { N("corredor", "corredores") } },
                { "frostguard", new[] { F("guardia de escarcha", "guardias de escarcha") } },
                { "camel_archer", new[] { N("arquero de camello", "arqueros de camello"), N("camello", "camellos") } },
                { "quilted_lancer", new[] { N("jinete acolchado", "jinetes acolchados"), N("acolchado", "acolchados") } },
                { "dune_elephant", new[] { N("elefante", "elefantes") } },
                { "sun_lion", new[] { N("león", "leones") } },
                { "grove_guardian", new[] { N("guardián", "guardianes") } },
                { "war_troll", new[] { N("trol", "troles") } },
                { "ember_drake", new[] { N("draco", "dracos"), N("dragón", "dragones") } },
                { "siege_ram", new[] { N("ariete", "arietes") } },
                { "siege_ladder", new[] { F("escalera", "escaleras") } },
                { "siege_tower", new[] { F("torre de asedio", "torres de asedio") } },
                { "crimson_corsair", new[] { N("capitán", "capitanes"), N("héroe", "héroes") } },
                { "boarding_raider", new[] { N("saqueador", "saqueadores"), N("pirata", "piratas") } },
                { "gunpowder_corsair", new[] { N("pistolero", "pistoleros"), N("tirador", "tiradores"), N("corsario de pólvora", "corsarios de pólvora") } },
                { "war_galley", new[] { F("galera", "galeras") } },
                { "pirate_sloop", new[] { F("balandra", "balandras") } },
                { "hearth", new[] { N("hogar", "hogares"), N("centro urbano", "centros urbanos") } },
                { "shelter", new[] { F("casa", "casas"), N("refugio", "refugios") } },
                { "storeyard", new[] { N("almacén", "almacenes") } },
                { "muster_hall", new[] { N("cuartel", "cuarteles") } },
                { "archive", new[] { N("archivo", "archivos"), F("biblioteca", "bibliotecas") } },
                { "supply_outpost", new[] { N("puesto de suministros", "puestos de suministros") } },
                { "wall", new[] { F("muralla", "murallas") } },
                { "gate", new[] { F("puerta", "puertas") } },
                { "watchtower", new[] { F("torre", "torres"), F("atalaya", "atalayas") } },
                { "keep", new[] { F("fortaleza", "fortalezas"), N("castillo", "castillos") } },
                { "beast_lodge", new[] { N("establo", "establos"), N("cubil", "cubiles") } },
                { "siege_workshop", new[] { N("taller de asedio", "talleres de asedio"), N("taller", "talleres") } },
                { "dock", new[] { N("muelle", "muelles") } },
            },
            ResearchNouns = new Dictionary<string, VoiceNoun[]>(StringComparer.Ordinal)
            {
                { VoiceVocabulary.Weapons, new[] { F("armas"), F("mejora de armas") } },
                { VoiceVocabulary.Armor, new[] { F("armaduras"), F("mejora de armadura") } },
                { VoiceVocabulary.Gathering, new[] { F("herramientas"), F("mejora de recolección") } },
                { VoiceVocabulary.Unique, new[] { F("tecnología de facción"), F("tecnología única") } },
            },
            Resources = new Dictionary<ResourceKind, string[]>
            {
                { ResourceKind.Food, new[] { "comida", "alimento" } },
                { ResourceKind.Wood, new[] { "madera" } },
                { ResourceKind.Metal, new[] { "metal", "hierro" } },
                { ResourceKind.Stone, new[] { "piedra" } },
            },
            Train = (verb, count, noun) => count == 1 ? verb + " " + (noun.Feminine ? "una " : "un ") + noun.Singular : verb + " " + SpanishCounts[count] + " " + noun.Plural,
            Build = (verb, noun) => verb + " " + (noun.Feminine ? "una " : "un ") + noun.Singular,
            SelectUnits = noun => "selecciona " + (noun.Feminine ? "las " : "los ") + noun.Plural,
            SelectBuilding = noun => "selecciona " + (noun.Feminine ? "la " : "el ") + noun.Singular,
            Help = new[]
            {
                "«selecciona el ejército» · «trabajadores ociosos» · «selecciona los lanceros»",
                "«entrena dos lanceros» · «recluta un jinete» · «entrena un trabajador»",
                "«construye un cuartel» apuntando con el ratón, y luego «confirma» o «cancela»",
                "«recolecta madera» · «a por comida» · «entrega los recursos»",
                "«ataca aquí» · «muévete aquí» · «alto» · «retirada»",
                "«investiga armas» · «avanza de era» · «cambia la formación»",
                "«cámara a la base» · «gira la cámara» · «acércate» · «pausa» · «reanuda»",
            },
        };

        internal static readonly VoiceLanguagePack English = new VoiceLanguagePack
        {
            TrainVerbs = new[] { "train", "recruit", "make", "produce", "create" },
            BuildVerbs = new[] { "build", "construct" },
            SelectVerbs = new[] { "select", "choose", "pick" },
            ResearchVerbs = new[] { "research", "upgrade", "study" },
            GatherVerbs = new[] { "gather", "collect", "harvest", "mine", "chop" },
            Articles = new HashSet<string>(StringComparer.Ordinal) { "the", "a", "an", "some", "all", "my", "our" },
            Fillers = new HashSet<string>(StringComparer.Ordinal) { "please", "now", "okay", "ok", "hey", "here", "more" },
            Fixed = new Dictionary<VoiceAction, string[]>
            {
                { VoiceAction.Help, new[] { "help", "what can I say", "voice commands" } },
                { VoiceAction.CloseHelp, new[] { "close help", "hide help" } },
                { VoiceAction.SelectArmy, new[] { "select army", "select the army", "army" } },
                { VoiceAction.SelectWorkers, new[] { "select workers", "all workers", "workers" } },
                { VoiceAction.SelectIdleWorkers, new[] { "idle workers", "select idle workers" } },
                { VoiceAction.ClearSelection, new[] { "deselect", "clear selection" } },
                { VoiceAction.Attack, new[] { "attack here", "attack", "charge" } },
                { VoiceAction.Move, new[] { "move here", "go here", "move" } },
                { VoiceAction.Stop, new[] { "stop", "halt", "hold position" } },
                { VoiceAction.Retreat, new[] { "retreat", "fall back", "go home" } },
                { VoiceAction.ReturnCargo, new[] { "return cargo", "drop off", "deliver resources" } },
                { VoiceAction.CycleFormation, new[] { "change formation", "next formation" } },
                { VoiceAction.Confirm, new[] { "confirm", "place it", "build it here" } },
                { VoiceAction.Cancel, new[] { "cancel", "never mind" } },
                { VoiceAction.AdvanceEra, new[] { "advance age", "next age", "advance era" } },
                { VoiceAction.CameraHome, new[] { "camera home", "camera to base", "go to base" } },
                { VoiceAction.CameraSelection, new[] { "camera to army", "focus selection", "follow army" } },
                { VoiceAction.CameraRotate, new[] { "rotate camera", "turn camera" } },
                { VoiceAction.ZoomIn, new[] { "zoom in", "closer" } },
                { VoiceAction.ZoomOut, new[] { "zoom out", "further" } },
                { VoiceAction.Pause, new[] { "pause", "pause game", "open menu" } },
                { VoiceAction.Resume, new[] { "resume", "continue", "resume game" } },
            },
            Nouns = new Dictionary<string, VoiceNoun[]>(StringComparer.Ordinal)
            {
                { "tender", new[] { N("worker", "workers"), N("villager", "villagers") } },
                { "treasure_seeker", new[] { N("seeker", "seekers"), N("treasure seeker", "treasure seekers") } },
                { "reedguard", new[] { N("spearman", "spearmen"), N("reedguard", "reedguards") } },
                { "stringwarden", new[] { N("archer", "archers"), N("stringwarden", "stringwardens") } },
                { "strider", new[] { N("rider", "riders"), N("strider", "striders") } },
                { "threadkeeper", new[] { N("threadkeeper", "threadkeepers") } },
                { "ashrunner", new[] { N("ashrunner", "ashrunners"), N("runner", "runners") } },
                { "frostguard", new[] { N("frostguard", "frostguards") } },
                { "camel_archer", new[] { N("camel archer", "camel archers"), N("camel", "camels") } },
                { "quilted_lancer", new[] { N("quilted lancer", "quilted lancers"), N("lancer", "lancers") } },
                { "dune_elephant", new[] { N("elephant", "elephants") } },
                { "sun_lion", new[] { N("lion", "lions") } },
                { "grove_guardian", new[] { N("guardian", "guardians") } },
                { "war_troll", new[] { N("troll", "trolls") } },
                { "ember_drake", new[] { N("drake", "drakes"), N("dragon", "dragons") } },
                { "siege_ram", new[] { N("ram", "rams") } },
                { "siege_ladder", new[] { N("ladder", "ladders") } },
                { "siege_tower", new[] { N("siege tower", "siege towers") } },
                { "crimson_corsair", new[] { N("captain", "captains"), N("hero", "heroes") } },
                { "boarding_raider", new[] { N("raider", "raiders"), N("pirate", "pirates") } },
                { "gunpowder_corsair", new[] { N("gunner", "gunners"), N("musketeer", "musketeers") } },
                { "war_galley", new[] { N("galley", "galleys"), N("warship", "warships") } },
                { "pirate_sloop", new[] { N("sloop", "sloops") } },
                { "hearth", new[] { N("hearth", "hearths"), N("town center", "town centers") } },
                { "shelter", new[] { N("house", "houses"), N("shelter", "shelters") } },
                { "storeyard", new[] { N("storeyard", "storeyards"), N("storehouse", "storehouses") } },
                { "muster_hall", new[] { N("barracks", "barracks"), N("muster hall", "muster halls") } },
                { "archive", new[] { N("archive", "archives"), N("library", "libraries") } },
                { "supply_outpost", new[] { N("outpost", "outposts") } },
                { "wall", new[] { N("wall", "walls") } },
                { "gate", new[] { N("gate", "gates") } },
                { "watchtower", new[] { N("watchtower", "watchtowers"), N("tower", "towers") } },
                { "keep", new[] { N("keep", "keeps"), N("castle", "castles") } },
                { "beast_lodge", new[] { N("beast lodge", "beast lodges"), N("stable", "stables") } },
                { "siege_workshop", new[] { N("siege workshop", "siege workshops"), N("workshop", "workshops") } },
                { "dock", new[] { N("dock", "docks"), N("harbor", "harbors") } },
            },
            ResearchNouns = new Dictionary<string, VoiceNoun[]>(StringComparer.Ordinal)
            {
                { VoiceVocabulary.Weapons, new[] { N("weapons"), N("weapon upgrade") } },
                { VoiceVocabulary.Armor, new[] { N("armor"), N("armor upgrade") } },
                { VoiceVocabulary.Gathering, new[] { N("tools"), N("gathering") } },
                { VoiceVocabulary.Unique, new[] { N("faction technology"), N("unique technology") } },
            },
            Resources = new Dictionary<ResourceKind, string[]>
            {
                { ResourceKind.Food, new[] { "food" } },
                { ResourceKind.Wood, new[] { "wood", "lumber" } },
                { ResourceKind.Metal, new[] { "metal", "iron", "ore" } },
                { ResourceKind.Stone, new[] { "stone", "rock" } },
            },
            Train = (verb, count, noun) => count == 1 ? verb + " " + EnglishArticle(noun.Singular) + " " + noun.Singular : verb + " " + EnglishCounts[count] + " " + noun.Plural,
            Build = (verb, noun) => verb + " " + EnglishArticle(noun.Singular) + " " + noun.Singular,
            SelectUnits = noun => "select " + noun.Plural,
            SelectBuilding = noun => "select the " + noun.Singular,
            Help = new[]
            {
                "“select army” · “idle workers” · “select spearmen”",
                "“train two spearmen” · “recruit a rider” · “train a worker”",
                "“build a barracks” while pointing with the mouse, then “confirm” or “cancel”",
                "“gather wood” · “collect food” · “return cargo”",
                "“attack here” · “move here” · “stop” · “retreat”",
                "“research weapons” · “advance age” · “change formation”",
                "“camera home” · “rotate camera” · “zoom in” · “pause” · “resume”",
            },
        };
    }
}
