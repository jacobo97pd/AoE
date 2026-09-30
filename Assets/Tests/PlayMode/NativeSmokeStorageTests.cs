using System;
using System.IO;
using System.Reflection;
using Emberfield.Presentation;
using NUnit.Framework;

namespace Emberfield.Tests.PlayMode
{
    public sealed class NativeSmokeStorageTests
    {
        private const BindingFlags Static = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static;
        private static readonly Type Session = typeof(AlphaControls).Assembly.GetType("Emberfield.Presentation.AlphaLocalSession", true);
        private static readonly FieldInfo RootOverride = typeof(NativeSmokeStorage).GetField("DirectoryOverride", Static);
        private static readonly FieldInfo AlphaOverride = Session.GetField("DirectoryOverride", Static);
        private string scratch, previousRoot, previousAlpha;

        [SetUp] public void SetUp()
        {
            scratch = Path.Combine(Path.GetTempPath(), "EmberfieldNativeStorage-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(scratch);
            previousRoot = (string)RootOverride.GetValue(null); previousAlpha = (string)AlphaOverride.GetValue(null);
            Shutdown();
        }
        [TearDown] public void TearDown()
        {
            Shutdown(); RootOverride.SetValue(null, previousRoot); AlphaOverride.SetValue(null, previousAlpha);
            if (Directory.Exists(scratch)) Directory.Delete(scratch, true);
        }
        private static void Shutdown() => Session.GetMethod("Shutdown", Static).Invoke(null, null);
        private static void Ensure() => Session.GetMethod("Ensure", Static).Invoke(null, null);
        private static T SessionValue<T>(string name) => (T)Session.GetProperty(name, Static).GetValue(null);
        private static string Resolve(string ordinary, string[] args, bool development = true, string overridden = null) =>
            (string)typeof(NativeSmokeStorage).GetMethod("ResolveRoot", Static).Invoke(null, new object[] { overridden, ordinary, args, development });
        private void Use(string root) { Shutdown(); RootOverride.SetValue(null, root); AlphaOverride.SetValue(null, null); }

        [TestCase(false)] [TestCase(true)]
        public void NativeDriversKeepSettingsDiagnosticsAndSavesAwayFromAnExistingProfile(bool online)
        {
            string player = Path.Combine(scratch, "existing-player"), alpha = Path.Combine(player, "Alpha"), saves = Path.Combine(player, "saves");
            Directory.CreateDirectory(alpha); Directory.CreateDirectory(saves);
            var originalSettings = new AlphaSettingsStore(alpha);
            originalSettings.Value.SoundVolume = .17f; originalSettings.Value.TutorialActive = true;
            Assert.That(originalSettings.Save(), Is.True);
            foreach (string file in new[] { "diagnostics.json", "session-active.json", "diagnostics-export.json" })
                File.WriteAllText(Path.Combine(alpha, file), "existing player data: " + file);
            File.WriteAllText(Path.Combine(saves, "match.json"), "existing saved game");
            string[] originals = Directory.GetFiles(player, "*", SearchOption.AllDirectories);
            var bytes = Array.ConvertAll(originals, File.ReadAllBytes);
            string evidence = Path.Combine(scratch, "evidence");
            var arguments = online ? new[] { "player", "-emberfieldOnlineSmoke", Path.Combine(evidence, "sync", "guest-config.json") } :
                new[] { "player", "-emberfieldProductSmoke", evidence };
            string isolated = Resolve(player, arguments);
            Assert.That(isolated.StartsWith(evidence + Path.DirectorySeparatorChar, StringComparison.Ordinal), Is.True);
            Use(isolated); Ensure();
            Assert.That(NativeSmokeStorage.IsIsolated, Is.True);
            var settings = SessionValue<AlphaSettingsStore>("Settings");
            Assert.That(settings.Value.TutorialActive, Is.False, "The smoke must not resume the player's guide.");
            settings.Value.SoundVolume = .42f; Assert.That(settings.Save(), Is.True);
            var diagnostics = SessionValue<AlphaDiagnostics>("Diagnostics");
            diagnostics.SetConsent(true); diagnostics.Record(AlphaDiagnosticKind.MatchStarted, 4);
            Assert.That(diagnostics.Export(), Is.True); Assert.That(diagnostics.Clear(), Is.True);
            Assert.That(File.Exists(Path.Combine(isolated, "Alpha", "settings.json")), Is.True);
            var save = new MatchArchive.Save { Version = MatchArchive.Version, MapId = "amber_crossing", FactionId = "aven" };
            MatchArchive.Write(save); Assert.That(MatchArchive.Read().MapId, Is.EqualTo("amber_crossing"));
            Assert.That(MatchArchive.Directory, Is.EqualTo(Path.Combine(isolated, "saves"))); MatchArchive.Delete();
            for (int i = 0; i < originals.Length; i++) Assert.That(File.ReadAllBytes(originals[i]), Is.EqualTo(bytes[i]), originals[i]);
        }

        [Test] public void BothSeatsHaveSeparatePreferencesAndTheGuestUsesTheSameFolderAfterRestart()
        {
            string sync = Path.Combine(scratch, "sync");
            string host = Resolve("unused", new[] { "-emberfieldOnlineSmoke", Path.Combine(sync, "host-config.json") });
            string guest = Resolve("unused", new[] { "-emberfieldOnlineSmoke", Path.Combine(sync, "guest-config.json") });
            Assert.That(host, Is.Not.EqualTo(guest));
            Assert.That(Resolve("unused", new[] { "-emberfieldOnlineSmoke", Path.Combine(sync, "guest-config.json") }), Is.EqualTo(guest));
            Use(host); NativeSmokeStorage.SetPreference("Emberfield.Online.ServerAddress", "http://127.0.0.1:12345");
            string cosmetic = CosmeticLoadout.Catalog[0].id;
            Assert.That(CosmeticLoadout.EquipPreview(cosmetic), Is.True);
            Use(guest);
            Assert.That(NativeSmokeStorage.GetPreference("Emberfield.Online.ServerAddress"), Is.Empty);
            Assert.That(CosmeticLoadout.IsPreviewEquipped(cosmetic), Is.False);
            NativeSmokeStorage.SetPreference("Emberfield.Online.ServerAddress", "http://127.0.0.1:54321");
            Use(host);
            Assert.That(NativeSmokeStorage.GetPreference("Emberfield.Online.ServerAddress"), Is.EqualTo("http://127.0.0.1:12345"));
            Assert.That(CosmeticLoadout.IsPreviewEquipped(cosmetic), Is.True);
        }

        [Test] public void ExplicitTestOverridesKeepPriorityAndOrdinaryLaunchesKeepTheirDirectory()
        {
            string ordinary = Path.Combine(scratch, "ordinary"), overridden = Path.Combine(scratch, "test-root");
            Assert.That(Resolve(ordinary, new[] { "player" }, false), Is.EqualTo(ordinary));
            Assert.That(Resolve(ordinary, new[] { "-emberfieldProductSmoke" }, false, overridden), Is.EqualTo(overridden));
            Use(overridden);
            string alpha = Path.Combine(scratch, "explicit-alpha"); AlphaOverride.SetValue(null, alpha); Ensure();
            Assert.That(SessionValue<AlphaSettingsStore>("Settings").Save(), Is.True);
            Assert.That(File.Exists(Path.Combine(alpha, "settings.json")), Is.True);
            Assert.That(Directory.Exists(Path.Combine(overridden, "Alpha")), Is.False);
            Assert.That(MatchArchive.Directory, Is.EqualTo(Path.Combine(overridden, "saves")));
        }

        [TestCase("missing")] [TestCase("next-option")] [TestCase("relative")]
        [TestCase("duplicate")] [TestCase("not-json")] [TestCase("release")]
        public void MalformedSmokeFlagsFailInsteadOfFallingBackToThePlayersFiles(string invalid)
        {
            string config = Path.Combine(scratch, "guest-config.json");
            string[] arguments = { "-emberfieldOnlineSmoke", config };
            switch (invalid)
            {
                case "missing": arguments = new[] { "-emberfieldProductSmoke" }; break;
                case "next-option": arguments = new[] { "-emberfieldProductSmoke", "-logFile" }; break;
                case "relative": arguments = new[] { "-emberfieldProductSmoke", "relative" }; break;
                case "duplicate": arguments = new[] { "-emberfieldProductSmoke", scratch, "-emberfieldOnlineSmoke", config }; break;
                case "not-json": arguments = new[] { "-emberfieldOnlineSmoke", scratch }; break;
            }
            var error = Assert.Throws<TargetInvocationException>(() => Resolve(Path.Combine(scratch, "player"), arguments, invalid != "release"));
            Assert.That(error.InnerException, Is.InstanceOf<ArgumentException>().Or.InstanceOf<InvalidOperationException>());
            Assert.That(Directory.GetFiles(scratch, "*", SearchOption.AllDirectories), Is.Empty);
        }
    }
}
