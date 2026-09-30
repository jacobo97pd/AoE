using System.Collections;
using Emberfield.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Emberfield.Tests.PlayMode
{
    public sealed class OnlineUiTests
    {
        [TestCase("{\"ok\":true,\"status\":\"active\",\"result\":null}", false)]
        [TestCase("{\"ok\":true,\"status\":\"active\",\"result\":{\"winnerPlayerId\":2,\"reason\":\"Surrender\"}}", false)]
        [TestCase("{\"ok\":true,\"status\":\"finished\",\"result\":{\"winnerPlayerId\":2,\"reason\":\"Surrender\"}}", true)]
        public void OnlyATerminalNonemptyReceiptIsPresentedAsAResult(string json, bool terminal)
        { Assert.AreEqual(terminal, JsonUtility.FromJson<OnlineState>(json).Result != null); }

        [TestCase("http://127.0.0.1:8787", true)]
        [TestCase("http://[::1]:8787", true)]
        [TestCase("  https://rts.example.test/  ", true)]
        [TestCase("https://rts.example.test", true)]
        [TestCase("http://192.168.1.25:8787", false)]
        [TestCase("https://username:secret@rts.example.test", false)]
        [TestCase("https://rts.example.test?token=secret", false)]
        [TestCase("file:///C:/private", false)]
        public void ServerAddressRequiresTlsOutsideLoopbackAndRejectsEmbeddedCredentials(string address, bool accepted)
        { Assert.AreEqual(accepted, OnlineService.TryAddress(address, out _)); }

        [TestCase("https://explicit.example.test", "https://saved.example.test", "https://packaged.example.test", "https://explicit.example.test")]
        [TestCase(null, "https://saved.example.test", "https://packaged.example.test", "https://saved.example.test")]
        [TestCase(null, "http://remote.example.test", "https://packaged.example.test", "https://packaged.example.test")]
        [TestCase(null, null, "https://packaged.example.test", "https://packaged.example.test")]
        [TestCase(null, null, "http://remote.example.test", OnlineService.DefaultAddress)]
        public void StartupEndpointUsesExplicitThenSavedThenPackagedWithValidatedFallbacks(string commandLine, string saved, string packaged, string expected)
        { Assert.AreEqual(expected, OnlineService.InitialAddress(commandLine, saved, packaged)); }

        [Test]
        public void InvalidExplicitEndpointIsReportedRatherThanSilentlySelectingAnotherServer()
        { Assert.Throws<System.ArgumentException>(() => OnlineService.InitialAddress("http://remote.example.test", "https://saved.example.test", null)); }

        [UnityTest]
        public IEnumerator SettingsPauseLocalWorldAndAreExclusiveWithOnlineMenu()
        {
            var root = new GameObject("Alpha UI integration");
            try
            {
                var match = root.AddComponent<MatchController>();
                yield return null;
                match.Alpha.Open();
                long before = match.World.TickIndex;
                yield return new WaitForSecondsRealtime(.2f);
                Assert.AreEqual(before, match.World.TickIndex, "Settings pause local gameplay.");
                Assert.IsTrue(match.Hud.AlphaPanel.IsVisible);
                match.Online.Open(); yield return null;
                Assert.IsFalse(match.Alpha.IsOpen); Assert.IsTrue(match.Online.IsOpen);
                Assert.IsTrue(match.Online.BlocksWorldInput);
                match.Alpha.Open(); yield return null;
                Assert.IsFalse(match.Online.IsOpen); Assert.IsTrue(match.Alpha.IsOpen);
                match.Alpha.Close();
                yield return new WaitForSecondsRealtime(.2f);
                Assert.Greater(match.World.TickIndex, before);
            }
            finally { Object.Destroy(root); }
            yield return null;
        }
    }
}
