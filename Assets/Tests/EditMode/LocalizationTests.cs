using System;
using Emberfield.Localization;
using NUnit.Framework;

namespace Emberfield.Tests.EditMode
{
    public class LocalizationTests
    {
        private static readonly TextTranslator Spanish = SpanishCatalog.Create();
        private static readonly TextTranslator English = EnglishCatalog.Create();

        [Test]
        public void CatalogsBuildWithoutDuplicatesOrBrokenTemplates()
        {
            Assert.AreEqual(GameLanguage.Spanish, Spanish.Language);
            Assert.AreEqual(GameLanguage.English, English.Language);
            Assert.Greater(Spanish.EntryCount, 500);
            Assert.Greater(Spanish.TemplateCount, 80);
            Assert.Greater(English.EntryCount, 50);
        }

        [Test]
        public void TranslatorMatchesWholeTextLinesSegmentsAndCase()
        {
            var t = new TextTranslator(GameLanguage.Spanish).Add("Food", "Comida").Add("Wood", "Madera").Add("Home", "Base").Add("HOME", "INICIO")
                .Template("Gathering {r}.", "Recolectando {r:lower}.").Template("{n:int} selected", "{n} seleccionados").Template("Train {u}", "Entrenar {u}")
                .Template("Player: {p:raw}", "Jugador: {p:raw}");
            Assert.AreEqual("Comida", t.Translate("Food"));
            Assert.AreEqual("COMIDA", t.Translate("FOOD"));
            Assert.AreEqual("comida", t.Translate("food"));
            Assert.AreEqual("INICIO", t.Translate("HOME"), "An exact entry wins over the case-folded one.");
            Assert.AreEqual("Base", t.Translate("Home"));
            Assert.AreEqual("Recolectando madera.", t.Translate("Gathering wood."));
            Assert.AreEqual("12 seleccionados · Base", t.Translate("12 selected · Home"));
            Assert.AreEqual("Entrenar Comida\n  Madera  ", t.Translate("Train Food\n  Wood  "), "Lines translate separately and keep their spacing.");
            Assert.AreEqual("Jugador: Food", t.Translate("Player: Food"), "Raw placeholders keep names such as usernames untouched.");
            Assert.AreEqual("Zorblax 7", t.Translate("Zorblax 7"));
            Assert.AreEqual("", t.Translate(""));
            Assert.IsNull(t.Translate(null));
        }

        [Test]
        public void MistakesInACatalogAreRejectedWhenItIsBuilt()
        {
            var t = new TextTranslator(GameLanguage.Spanish).Add("Stop", "Detener");
            Assert.Throws<ArgumentException>(() => t.Add("Stop", "Alto"));
            Assert.Throws<ArgumentException>(() => t.Template("{a} {b}", "{a}"));
            Assert.Throws<ArgumentException>(() => t.Template("Train {u}", "Entrenar {v}"));
            Assert.Throws<ArgumentException>(() => t.Template("{u} and {u}", "{u}"));
        }

        [TestCase("Workers", "Aldeanos")]
        [TestCase("7 IDLE WORKERS", "7 OCIOSOS")]
        [TestCase("Train Reedguard\n50 food 20 wood", "Entrenar Lancero\n50 comida 20 madera")]
        [TestCase("Gathering wood. Resources count when delivered.", "Recolectando madera. Los recursos cuentan al entregarlos.")]
        [TestCase("HOME", "INICIO")]
        [TestCase("Home", "Base")]
        [TestCase("SOUND\nOn", "SONIDO\nActivado")]
        [TestCase("WORKERS  12  ·  3 idle       ARMY  4       PRODUCING  1/2       ORDERS/MIN  7",
            "TRABAJADORES  12  ·  3 ociosos       EJÉRCITO  4       PRODUCIENDO  1/2       ÓRDENES/MIN  7")]
        [TestCase("Tender / Worker  ·  60/60 health  ·  Idle", "Aldeano / Trabajador  ·  60/60 de salud  ·  Inactivo")]
        [TestCase("✓  Conquest / destroy the Hearths", "✓  Conquista / destruye los Hogares")]
        [TestCase("BEGIN Históricas BATTLE  ›", "COMENZAR BATALLA: HISTÓRICAS  ›")]
        [TestCase("Result: Surrender", "Resultado: Rendición")]
        [TestCase("Rival settlement: Era 3 · 6 Tenders · 0 military units remaining · 13 buildings",
            "Asentamiento rival: era 3 · 6 aldeanos · 0 unidades militares restantes · 13 edificios")]
        [TestCase("Moving in loose formation.", "En marcha en formación abierta.")]
        [TestCase("Hearth · Requires Kingdom", "Hogar · Requiere Reino")]
        [TestCase("Stored events: 3 / 256", "Eventos guardados: 3 / 256")]
        [TestCase("Better Handles, Tempered Edges complete. Era 2 · Kingdom.", "Investigación completada: Mangos mejorados, Filos templados. Era 2 · Reino.")]
        [TestCase("RESEARCH  /  Era 2 · Kingdom", "INVESTIGACIÓN  /  Era 2 · Reino")]
        [TestCase("PlayerOne / Hispanos / ready", "PlayerOne / Hispanos / listo")]
        [TestCase("Player: Gold", "Jugador: Gold")]
        [TestCase("Miraj Sultanate (legado)", "Sultanato de Miraj (legado)")]
        [TestCase("Not enough resources to train.", "No hay recursos suficientes para entrenar.")]
        [TestCase("Requires era: Kingdom.", "Requiere la era: Reino.")]
        [TestCase("1  Settlement", "1  Asentamiento")]
        [TestCase("Conquest: destruye los Hearths. Dominion: controla los faros.", "Conquista: destruye los Hogares. Dominio: controla los faros.")]
        [TestCase("Camel Archers", "Arqueros de camello")]
        [TestCase("Train Quilted Lancer", "Entrenar Jinete acolchado")]
        [TestCase("Quilted Lancers gain two armor.", "Los Jinetes acolchados ganan dos puntos de armadura.")]
        [TestCase("Zorblax", "Zorblax")]
        public void SpanishReadsComposedScreens(string source, string expected) => Assert.AreEqual(expected, Spanish.Translate(source));

        [TestCase("Franceses", "French")]
        [TestCase("Miraj Sultanate (legado)", "Miraj Sultanate (legacy)")]
        [TestCase("Play Hispanos", "Play Spanish")]
        [TestCase("BEGIN Navales BATTLE  ›", "BEGIN NAVAL BATTLE  ›")]
        [TestCase("Corsario Carmesí / Héroe pirata  ·  380/380 health  ·  Idle", "Crimson Corsair / Pirate hero  ·  380/380 health  ·  Idle")]
        [TestCase("Pistola · recarga entre disparos", "Pistol · reloads between shots")]
        [TestCase("Workers", "Workers")]
        [TestCase("Play Confederación del Sahel", "Play Sahel Confederation")]
        [TestCase("SABANA & CABALLERÍA PESADA", "SAVANNA & HEAVY CAVALRY")]
        public void EnglishCoversTextWrittenInSpanish(string source, string expected) => Assert.AreEqual(expected, English.Translate(source));
    }
}
