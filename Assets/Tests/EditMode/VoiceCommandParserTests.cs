using System.Linq;
using Emberfield.Simulation;
using Emberfield.Voice;
using NUnit.Framework;

namespace Emberfield.Tests.EditMode
{
    public class VoiceCommandParserTests
    {
        private static GameDefinition Rules() => new GameDefinition
        {
            Units = new[]
            {
                new UnitDefinition { Id = "tender", DisplayName = "Tender", IsWorker = true },
                new UnitDefinition { Id = "reedguard", DisplayName = "Reedguard" },
                new UnitDefinition { Id = "stringwarden", DisplayName = "Stringwarden" },
                new UnitDefinition { Id = "strider", DisplayName = "Strider" },
                new UnitDefinition { Id = "threadkeeper", DisplayName = "Threadkeeper", RequiredFactionId = "aven" },
                new UnitDefinition { Id = "ashrunner", DisplayName = "Ashrunner", RequiredFactionId = "serevin" },
                new UnitDefinition { Id = "ember_drake", DisplayName = "Ember Drake", RequiredRealmId = ContentRealms.Fantasy },
                new UnitDefinition { Id = "siege_tower", DisplayName = "Siege Tower" },
            },
            Buildings = new[]
            {
                new BuildingDefinition { Id = "hearth", DisplayName = "Hearth", CanDropOff = true, TrainableUnitIds = new[] { "tender" } },
                new BuildingDefinition { Id = "shelter", DisplayName = "Shelter" },
                new BuildingDefinition { Id = "muster_hall", DisplayName = "Muster Hall", TrainableUnitIds = new[] { "reedguard", "stringwarden", "strider", "threadkeeper", "ashrunner" } },
                new BuildingDefinition { Id = "watchtower", DisplayName = "Watchtower" },
                new BuildingDefinition { Id = "beast_lodge", DisplayName = "Beast Lodge", TrainableUnitIds = new[] { "ember_drake" } },
                new BuildingDefinition { Id = "siege_workshop", DisplayName = "Siege Workshop", TrainableUnitIds = new[] { "siege_tower" } },
            },
            Technologies = new[]
            {
                new TechnologyDefinition { Id = "weapons_2", DisplayName = "Weapons II" },
                new TechnologyDefinition { Id = "weapons_1", DisplayName = "Weapons I" },
                new TechnologyDefinition { Id = "armor_1", DisplayName = "Armor I" },
                new TechnologyDefinition { Id = "gather_1", DisplayName = "Gathering I" },
                new TechnologyDefinition { Id = "aven_lore", DisplayName = "Aven lore", RequiredFactionId = "aven" },
                new TechnologyDefinition { Id = "serevin_lore", DisplayName = "Serevin lore", RequiredFactionId = "serevin" },
            },
        };

        private static VoiceCommandParser Parser(VoiceLanguage language, string faction = "aven", string realm = ContentRealms.Historical) =>
            new VoiceCommandParser(VoiceVocabulary.Build(Rules(), faction, realm, language));

        [Test]
        public void TextIsComparedWithoutAccentsCasePunctuationOrExtraSpaces()
        {
            Assert.AreEqual("camara a la base", VoiceText.Normalize("¡Cámara  a la BASE!"));
            Assert.AreEqual("entrena 2 lanceros", VoiceText.Normalize("  Entrena, 2 lanceros... "));
            Assert.AreEqual("", VoiceText.Normalize("¿?"));
            Assert.AreEqual("", VoiceText.Normalize(null));
        }

        [TestCase("ataca aquí", VoiceAction.Attack)]
        [TestCase("Ataca", VoiceAction.Attack)]
        [TestCase("muévete aquí", VoiceAction.Move)]
        [TestCase("muevete aqui", VoiceAction.Move)]
        [TestCase("¡Alto!", VoiceAction.Stop)]
        [TestCase("detente", VoiceAction.Stop)]
        [TestCase("retirada", VoiceAction.Retreat)]
        [TestCase("cámara a la base", VoiceAction.CameraHome)]
        [TestCase("camara base", VoiceAction.CameraHome)]
        [TestCase("gira la cámara", VoiceAction.CameraRotate)]
        [TestCase("acércate más", VoiceAction.ZoomIn)]
        [TestCase("pausa, por favor", VoiceAction.Pause)]
        [TestCase("reanuda", VoiceAction.Resume)]
        [TestCase("ayuda", VoiceAction.Help)]
        [TestCase("cierra la ayuda", VoiceAction.CloseHelp)]
        [TestCase("trabajadores ociosos", VoiceAction.SelectIdleWorkers)]
        [TestCase("selecciona el ejército", VoiceAction.SelectArmy)]
        [TestCase("selecciona los trabajadores", VoiceAction.SelectWorkers)]
        [TestCase("confirma", VoiceAction.Confirm)]
        [TestCase("cancela", VoiceAction.Cancel)]
        [TestCase("avanza de era", VoiceAction.AdvanceEra)]
        [TestCase("cambia la formación", VoiceAction.CycleFormation)]
        [TestCase("entrega los recursos", VoiceAction.ReturnCargo)]
        public void SpanishFixedCommands(string phrase, VoiceAction action) =>
            Assert.AreEqual(new VoiceIntent(action), Parser(VoiceLanguage.Spanish).Parse(phrase));

        [TestCase("attack here", VoiceAction.Attack)]
        [TestCase("Move here", VoiceAction.Move)]
        [TestCase("STOP", VoiceAction.Stop)]
        [TestCase("fall back", VoiceAction.Retreat)]
        [TestCase("camera home", VoiceAction.CameraHome)]
        [TestCase("zoom out", VoiceAction.ZoomOut)]
        [TestCase("pause game", VoiceAction.Pause)]
        [TestCase("resume", VoiceAction.Resume)]
        [TestCase("help", VoiceAction.Help)]
        [TestCase("idle workers", VoiceAction.SelectIdleWorkers)]
        [TestCase("select the army", VoiceAction.SelectArmy)]
        [TestCase("confirm", VoiceAction.Confirm)]
        [TestCase("next age", VoiceAction.AdvanceEra)]
        public void EnglishFixedCommands(string phrase, VoiceAction action) =>
            Assert.AreEqual(new VoiceIntent(action), Parser(VoiceLanguage.English).Parse(phrase));

        [TestCase("entrena dos lanceros", "reedguard", 2)]
        [TestCase("recluta un jinete", "strider", 1)]
        [TestCase("entrena 3 arqueros", "stringwarden", 3)]
        [TestCase("crea cinco aldeanos", "tender", 5)]
        [TestCase("entrena a dos lanceros", "reedguard", 2)]
        [TestCase("entrena trabajadores", "tender", 1)]
        [TestCase("entrena más lanceros por favor", "reedguard", 1)]
        [TestCase("entrena una torre de asedio", "siege_tower", 1)]
        [TestCase("entrena dos torres de asedio", "siege_tower", 2)]
        [TestCase("entrena 12 lanceros", "reedguard", VoiceText.MaximumCount)]
        [TestCase("entrena dos reedguards", "reedguard", 2)]
        public void SpanishTrainingReadsCountsAndPlurals(string phrase, string unit, int count) =>
            Assert.AreEqual(new VoiceIntent(VoiceAction.Train, unit, count), Parser(VoiceLanguage.Spanish).Parse(phrase));

        [TestCase("train two spearmen", "reedguard", 2)]
        [TestCase("recruit a rider", "strider", 1)]
        [TestCase("train an archer", "stringwarden", 1)]
        [TestCase("make 4 workers", "tender", 4)]
        [TestCase("train 12 archers", "stringwarden", VoiceText.MaximumCount)]
        [TestCase("train riders", "strider", 1)]
        [TestCase("Train 3 workers, please!", "tender", 3)]
        public void EnglishTrainingReadsCountsAndPlurals(string phrase, string unit, int count) =>
            Assert.AreEqual(new VoiceIntent(VoiceAction.Train, unit, count), Parser(VoiceLanguage.English).Parse(phrase));

        [TestCase(VoiceLanguage.Spanish, "construye una casa", "shelter")]
        [TestCase(VoiceLanguage.Spanish, "construye un cuartel", "muster_hall")]
        [TestCase(VoiceLanguage.Spanish, "levanta una torre", "watchtower")]
        [TestCase(VoiceLanguage.Spanish, "construye la atalaya", "watchtower")]
        [TestCase(VoiceLanguage.English, "build a house", "shelter")]
        [TestCase(VoiceLanguage.English, "build a barracks", "muster_hall")]
        [TestCase(VoiceLanguage.English, "construct a tower", "watchtower")]
        public void BuildingsAreNamedInBothLanguages(VoiceLanguage language, string phrase, string building) =>
            Assert.AreEqual(new VoiceIntent(VoiceAction.Build, building), Parser(language).Parse(phrase));

        [TestCase(VoiceLanguage.Spanish, "selecciona los arqueros", VoiceAction.SelectUnits, "stringwarden")]
        [TestCase(VoiceLanguage.Spanish, "selecciona las torres de asedio", VoiceAction.SelectUnits, "siege_tower")]
        [TestCase(VoiceLanguage.Spanish, "selecciona el cuartel", VoiceAction.SelectBuilding, "muster_hall")]
        [TestCase(VoiceLanguage.English, "select archers", VoiceAction.SelectUnits, "stringwarden")]
        [TestCase(VoiceLanguage.English, "select the barracks", VoiceAction.SelectBuilding, "muster_hall")]
        public void SelectionTriesUnitsBeforeBuildings(VoiceLanguage language, string phrase, VoiceAction action, string target) =>
            Assert.AreEqual(new VoiceIntent(action, target), Parser(language).Parse(phrase));

        [TestCase(VoiceLanguage.Spanish, "recolecta madera", ResourceKind.Wood)]
        [TestCase(VoiceLanguage.Spanish, "a por comida", ResourceKind.Food)]
        [TestCase(VoiceLanguage.Spanish, "mina piedra", ResourceKind.Stone)]
        [TestCase(VoiceLanguage.Spanish, "recoge el hierro", ResourceKind.Metal)]
        [TestCase(VoiceLanguage.English, "gather wood", ResourceKind.Wood)]
        [TestCase(VoiceLanguage.English, "collect food", ResourceKind.Food)]
        [TestCase(VoiceLanguage.English, "mine iron", ResourceKind.Metal)]
        public void GatheringNamesAResource(VoiceLanguage language, string phrase, ResourceKind resource) =>
            Assert.AreEqual(new VoiceIntent(VoiceAction.Gather, resource: resource), Parser(language).Parse(phrase));

        [TestCase(VoiceLanguage.Spanish, "investiga armas", VoiceVocabulary.Weapons)]
        [TestCase(VoiceLanguage.Spanish, "mejora armaduras", VoiceVocabulary.Armor)]
        [TestCase(VoiceLanguage.Spanish, "investiga herramientas", VoiceVocabulary.Gathering)]
        [TestCase(VoiceLanguage.Spanish, "investiga tecnología de facción", VoiceVocabulary.Unique)]
        [TestCase(VoiceLanguage.English, "research weapons", VoiceVocabulary.Weapons)]
        [TestCase(VoiceLanguage.English, "upgrade armor", VoiceVocabulary.Armor)]
        [TestCase(VoiceLanguage.English, "research faction technology", VoiceVocabulary.Unique)]
        public void ResearchNamesAFamily(VoiceLanguage language, string phrase, string family) =>
            Assert.AreEqual(new VoiceIntent(VoiceAction.Research, family), Parser(language).Parse(phrase));

        [TestCase(VoiceLanguage.Spanish, "")]
        [TestCase(VoiceLanguage.Spanish, "   ")]
        [TestCase(VoiceLanguage.Spanish, "baila la conga")]
        [TestCase(VoiceLanguage.Spanish, "entrena")]
        [TestCase(VoiceLanguage.Spanish, "selecciona")]
        [TestCase(VoiceLanguage.Spanish, "construye un dragón")]
        [TestCase(VoiceLanguage.Spanish, "entrena dos casas")]
        [TestCase(VoiceLanguage.English, "dance the conga")]
        [TestCase(VoiceLanguage.English, "build a spearman")]
        public void UnknownOrIncompletePhrasesAreIgnored(VoiceLanguage language, string phrase) =>
            Assert.IsTrue(Parser(language).Parse(phrase).IsNone);

        [Test]
        public void OnlyTheLocalFactionAndRealmAreUnderstood()
        {
            var aven = Parser(VoiceLanguage.Spanish, "aven");
            var serevin = Parser(VoiceLanguage.Spanish, "serevin");
            Assert.AreEqual(new VoiceIntent(VoiceAction.Train, "threadkeeper"), aven.Parse("entrena un enlace"));
            Assert.IsTrue(aven.Parse("entrena un corredor").IsNone);
            Assert.AreEqual(new VoiceIntent(VoiceAction.Train, "ashrunner"), serevin.Parse("entrena un corredor"));
            Assert.IsTrue(serevin.Parse("entrena un enlace").IsNone);
            Assert.IsTrue(aven.Parse("entrena un draco").IsNone, "Fantasy creatures are not part of a historical match.");
            Assert.AreEqual(new VoiceIntent(VoiceAction.Train, "ember_drake"), Parser(VoiceLanguage.Spanish, null, ContentRealms.Fantasy).Parse("entrena un draco"));
            Assert.IsTrue(Parser(VoiceLanguage.Spanish, null).Parse("investiga tecnología de facción").IsNone, "No faction, no faction technology.");
        }

        [Test]
        public void ResearchFamiliesListTechnologiesInUnlockOrder()
        {
            var vocabulary = VoiceVocabulary.Build(Rules(), "aven", ContentRealms.Historical, VoiceLanguage.English);
            CollectionAssert.AreEqual(new[] { "weapons_1", "weapons_2" }, vocabulary.TechnologiesIn(VoiceVocabulary.Weapons));
            CollectionAssert.AreEqual(new[] { "aven_lore" }, vocabulary.TechnologiesIn(VoiceVocabulary.Unique));
            CollectionAssert.IsEmpty(vocabulary.TechnologiesIn("unknown"));
            CollectionAssert.IsEmpty(vocabulary.TechnologiesIn(null));
        }

        [Test]
        public void EveryOfferedPhraseMeansWhatItWasGeneratedFor()
        {
            foreach (var language in new[] { VoiceLanguage.Spanish, VoiceLanguage.English })
                foreach (var (faction, realm) in new[] { ("aven", ContentRealms.Historical), ("serevin", ContentRealms.Historical), ((string)null, ContentRealms.Fantasy) })
                {
                    var vocabulary = VoiceVocabulary.Build(Rules(), faction, realm, language);
                    var parser = new VoiceCommandParser(vocabulary);
                    var phrases = vocabulary.Phrases();
                    Assert.Greater(phrases.Count, 100, language + " " + faction);
                    Assert.AreEqual(phrases.Count, phrases.Select(phrase => VoiceText.Normalize(phrase.Text)).Distinct().Count(), "Phrases must differ after normalization.");
                    foreach (var phrase in phrases)
                        Assert.AreEqual(phrase.Intent, parser.Parse(phrase.Text), language + " · " + faction + " · " + phrase.Text);
                }
        }

        [Test]
        public void RecognizerPhrasesKeepAccentsAndLeaveOutDisplayNameAliases()
        {
            var spanish = VoiceVocabulary.Build(Rules(), "aven", ContentRealms.Historical, VoiceLanguage.Spanish).Phrases().Select(phrase => phrase.Text).ToList();
            CollectionAssert.Contains(spanish, "cámara a la base");
            CollectionAssert.Contains(spanish, "entrena dos lanceros");
            CollectionAssert.Contains(spanish, "entrena una torre de asedio");
            CollectionAssert.Contains(spanish, "construye un cuartel");
            CollectionAssert.Contains(spanish, "selecciona los arqueros");
            CollectionAssert.Contains(spanish, "a por madera");
            CollectionAssert.DoesNotContain(spanish, "entrena un reedguard", "English rule names are understood when typed, not offered to a Spanish recognizer.");
            var english = VoiceVocabulary.Build(Rules(), "aven", ContentRealms.Historical, VoiceLanguage.English).Phrases().Select(phrase => phrase.Text).ToList();
            CollectionAssert.Contains(english, "train two spearmen");
            CollectionAssert.Contains(english, "train an archer");
            CollectionAssert.Contains(english, "build a barracks");
            CollectionAssert.Contains(english, "gather wood");
        }

        [Test]
        public void CountsAreBoundedAndHelpExistsInBothLanguages()
        {
            Assert.AreEqual(VoiceText.MaximumCount, new VoiceIntent(VoiceAction.Train, "tender", 99).Count);
            Assert.AreEqual(1, new VoiceIntent(VoiceAction.Train, "tender", 0).Count);
            foreach (var language in new[] { VoiceLanguage.Spanish, VoiceLanguage.English })
                Assert.GreaterOrEqual(VoiceVocabulary.Build(Rules(), "aven", ContentRealms.Historical, language).Help.Count, 5);
        }
    }
}
