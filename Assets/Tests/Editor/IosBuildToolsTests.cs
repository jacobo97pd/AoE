using System;
using System.IO;
using System.Linq;
using System.Xml;
using Emberfield.Editor;
using Emberfield.Simulation;
using NUnit.Framework;
using UnityEditor.Build;

namespace Emberfield.Tests.Editor
{
    public sealed class IosBuildToolsTests
    {
        private string temporary;
        [SetUp] public void SetUp()
        {
            temporary = Path.Combine(Path.GetTempPath(), "emberfield-ios-export-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temporary);
        }
        [TearDown] public void TearDown()
        {
            if (temporary != null && Path.GetFileName(temporary).StartsWith("emberfield-ios-export-test-", StringComparison.Ordinal) && Directory.Exists(temporary))
                Directory.Delete(temporary, true);
        }

        [Test] public void DefaultsAreUnsignedPrototypeConfigurationWithOfflineEndpointUnchanged()
        {
            var config = IosBuildTools.ParseConfiguration(Path.Combine(temporary, "xcode"), null, null, null, null);
            Assert.That(config.BundleId, Is.EqualTo("com.emberfield.jpedrero"));
            Assert.That(config.BuildNumber, Is.EqualTo("3")); Assert.That(config.Version, Is.EqualTo("0.3.2"));
            Assert.That(config.ServerUrl, Is.Empty);
        }
        [Test] public void ExplicitReleaseConfigurationNormalizesOnlyTheServerOrigin()
        {
            var config = IosBuildTools.ParseConfiguration(Path.Combine(temporary, "xcode"), "com.example.emberfield", "27", "1.2.3", "https://game.example.com/");
            Assert.That(config.BundleId, Is.EqualTo("com.example.emberfield"));
            Assert.That(config.BuildNumber, Is.EqualTo("27")); Assert.That(config.Version, Is.EqualTo("1.2.3"));
            Assert.That(config.ServerUrl, Is.EqualTo("https://game.example.com"));
        }
        [TestCase(null)] [TestCase("")] [TestCase("relative/Xcode")]
        public void MissingOrRelativeOutputIsRejected(string path)
            => Assert.Throws<BuildFailedException>(() => IosBuildTools.ParseConfiguration(path, null, null, null, null));

        [TestCase("http://game.example.com")] [TestCase("https://localhost")] [TestCase("https://127.0.0.1")]
        [TestCase("https://example.com/path")] [TestCase("https://user:secret@example.com")]
        [TestCase("https://example.com?token=value")] [TestCase("https://example.com/#fragment")]
        public void UnsafeOrUnsupportedOnlineOriginsAreRejected(string url)
            => Assert.Throws<BuildFailedException>(() => IosBuildTools.ParseConfiguration(Path.Combine(temporary, "xcode"), null, null, null, url));

        [TestCase("com.example.*", "1", "0.3.0")] [TestCase("example", "1", "0.3.0")]
        [TestCase("com.example.app", "0", "0.3.0")] [TestCase("com.example.app", "-1", "0.3.0")]
        [TestCase("com.example.app", "1.2", "0.3.0")] [TestCase("com.example.app", "1", "1.2-beta")]
        public void InvalidAppleIdentifiersAndVersionsAreRejected(string bundle, string build, string version)
            => Assert.Throws<BuildFailedException>(() => IosBuildTools.ParseConfiguration(Path.Combine(temporary, "xcode"), bundle, build, version, null));

        [Test] public void MissingIsolationMarkerAndNonemptyOutputAreRejectedWithoutChangingSentinels()
        {
            string project = Path.Combine(temporary, "project"), output = Path.Combine(temporary, "xcode");
            foreach (string folder in new[] { "Assets", "Packages", "ProjectSettings" }) Directory.CreateDirectory(Path.Combine(project, folder));
            string source = Path.Combine(project, "ProjectSettings", "sentinel.txt"); File.WriteAllText(source, "source stays unchanged");
            Assert.Throws<BuildFailedException>(() => IosBuildTools.ValidateCopy(project, output));
            File.WriteAllText(Path.Combine(project, IosBuildTools.PackageMarker), "isolated test copy");
            Assert.DoesNotThrow(() => IosBuildTools.ValidateCopy(project, output));
            Directory.CreateDirectory(output); string existing = Path.Combine(output, "sentinel.txt"); File.WriteAllText(existing, "do not overwrite");
            Assert.Throws<BuildFailedException>(() => IosBuildTools.ValidateCopy(project, output));
            Assert.That(File.ReadAllText(source), Is.EqualTo("source stays unchanged"));
            Assert.That(File.ReadAllText(existing), Is.EqualTo("do not overwrite"));
            Assert.Throws<BuildFailedException>(() => IosBuildTools.ValidateCopy(project, Path.Combine(project, "Assets", "Xcode")));
            Assert.Throws<BuildFailedException>(() => IosBuildTools.ValidateCopy(project, temporary));
        }

        [Test] public void LinkerPreservesSerializableGameDataIncludingPrivateNestedDtosButNotEditorOrTestCode()
        {
            // Force the runtime assembly containing its private JSON credentials/config DTOs to load.
            var runtime = typeof(Emberfield.Presentation.MatchController).Assembly;
            string xml = IosBuildTools.PreservationXml(new[] { typeof(World).Assembly, runtime, typeof(IosBuildTools).Assembly, GetType().Assembly });
            var document = new XmlDocument(); document.LoadXml(xml);
            Assert.That(document.SelectSingleNode("/linker/assembly[@fullname='Emberfield.Simulation']"), Is.Not.Null);
            Assert.That(document.SelectSingleNode("/linker/assembly[@fullname='Emberfield.Presentation']"), Is.Not.Null);
            Assert.That(document.SelectSingleNode("/linker/assembly[@fullname='Emberfield.Editor']"), Is.Null);
            Assert.That(document.SelectSingleNode("/linker/assembly[@fullname='Emberfield.Tests.Editor']"), Is.Null);
            var nested = runtime.GetTypes().First(t => t.IsNestedPrivate && t.IsDefined(typeof(SerializableAttribute), false) && !t.IsGenericType);
            Assert.That(document.SelectSingleNode("/linker/assembly/type[@fullname='" + nested.FullName.Replace('+', '/') + "'][@preserve='all']"), Is.Not.Null);
            Assert.That(xml, Does.Not.Contain("fullname=\"Emberfield.Presentation.MatchController\""));
        }

        [Test] public void ProvisionalIconIsOpaqueAndContainsVisibleGoldMarkOnDarkField()
        {
            var background = IosBuildTools.IconPixel(.02f, .02f); var crown = IosBuildTools.IconPixel(.5f, .5f);
            Assert.That(background.a, Is.EqualTo(255)); Assert.That(crown.a, Is.EqualTo(255));
            Assert.That(crown.r, Is.GreaterThan(background.r + 100));
            Assert.That(crown.g, Is.GreaterThan(background.g + 100));
        }
    }
}
