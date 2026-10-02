using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;

namespace Emberfield.Editor
{
    /// <summary>Release Xcode export for a materialized, disposable CI copy. Never rebakes content.</summary>
    public static class IosBuildTools
    {
        public const string PackageMarker = ".emberfield-ios-package";
        private const string Generated = "Assets/Generated/iOS";
        private static readonly string[] Scenes = {
            "Assets/Game/Scenes/Greybox.unity", "Assets/Game/Scenes/MobileInputTest.unity",
            "Assets/Game/Scenes/CombatSandbox.unity", "Assets/Game/Scenes/RTSPerformanceStress.unity",
            "Assets/Game/Scenes/CharacterCollection.unity"
        };

        public sealed class Configuration
        {
            public string Output, BundleId, BuildNumber, Version, ServerUrl;
        }

        // Public pure validation also lets EditMode tests reject unsafe CI input without changing PlayerSettings.
        public static Configuration ParseConfiguration(string output, string bundleId, string buildNumber, string version, string serverUrl)
        {
            if (string.IsNullOrWhiteSpace(output) || !Path.IsPathFullyQualified(output))
                throw new BuildFailedException("EMBERFIELD_IOS_OUTPUT must be an absolute, new or empty Xcode output directory.");
            output = Path.GetFullPath(output);
            if (SamePath(output, Path.GetPathRoot(output))) throw new BuildFailedException("Xcode output cannot be a filesystem root.");
            bundleId = Default(bundleId, "com.emberfield.jpedrero");
            if (!Regex.IsMatch(bundleId, @"^[A-Za-z0-9][A-Za-z0-9-]*(\.[A-Za-z0-9][A-Za-z0-9-]*)+$"))
                throw new BuildFailedException("EMBERFIELD_BUNDLE_ID must be an explicit reverse-domain identifier without wildcards.");
            buildNumber = Default(buildNumber, "3");
            if (!int.TryParse(buildNumber, NumberStyles.None, CultureInfo.InvariantCulture, out int number) || number < 1)
                throw new BuildFailedException("EMBERFIELD_IOS_BUILD_NUMBER must be a positive integer; increment it for each TestFlight upload.");
            version = Default(version, "0.3.2");
            if (!Regex.IsMatch(version, @"^[0-9]+\.[0-9]+\.[0-9]+$"))
                throw new BuildFailedException("EMBERFIELD_IOS_VERSION must contain three numeric components, for example 0.3.0.");
            serverUrl = string.IsNullOrWhiteSpace(serverUrl) ? "" : serverUrl.Trim();
            if (serverUrl.Length > 0)
            {
                if (!Uri.TryCreate(serverUrl, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps ||
                    uri.IsLoopback || string.IsNullOrEmpty(uri.Host) || !string.IsNullOrEmpty(uri.UserInfo) ||
                    !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment) || uri.AbsolutePath != "/")
                    throw new BuildFailedException("EMBERFIELD_SERVER_URL must be an HTTPS server origin, without credentials, path, query, fragment or loopback host.");
                serverUrl = uri.GetLeftPart(UriPartial.Authority);
            }
            return new Configuration { Output = output, BundleId = bundleId, BuildNumber = number.ToString(CultureInfo.InvariantCulture), Version = version, ServerUrl = serverUrl };
        }

        // Invoke with -batchmode -quit -buildTarget iOS -executeMethod Emberfield.Editor.IosBuildTools.Export.
        public static void Export()
        {
            var config = ParseConfiguration(Environment.GetEnvironmentVariable("EMBERFIELD_IOS_OUTPUT"),
                Environment.GetEnvironmentVariable("EMBERFIELD_BUNDLE_ID"), Environment.GetEnvironmentVariable("EMBERFIELD_IOS_BUILD_NUMBER"),
                Environment.GetEnvironmentVariable("EMBERFIELD_IOS_VERSION"), Environment.GetEnvironmentVariable("EMBERFIELD_SERVER_URL"));
            string project = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            ValidateCopy(project, config.Output);
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.iOS, BuildTarget.iOS))
                throw new BuildFailedException("iOS Build Support is missing from Unity " + Application.unityVersion + ". Install the matching module before exporting.");
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.iOS)
                throw new BuildFailedException("Start Unity with -buildTarget iOS so import and script compilation finish for iOS before Export runs.");
            foreach (string scene in Scenes)
                if (!File.Exists(scene) || AssetDatabase.LoadAssetAtPath<SceneAsset>(scene) == null)
                    throw new BuildFailedException("Required authored player scene is missing or not imported: " + scene);
            Configure(config);
            Directory.CreateDirectory(Generated);
            File.WriteAllText(Generated + "/link.xml", PreservationXml(AppDomain.CurrentDomain.GetAssemblies()), new UTF8Encoding(false));
            PrepareIcon();
            if (config.ServerUrl.Length > 0)
            {
                const string serverPath = "Assets/Game/Resources/Online/server.json";
                if (!File.Exists(serverPath)) throw new BuildFailedException("The packaged Online/server.json resource is missing.");
                File.WriteAllText(serverPath, JsonUtility.ToJson(new ServerAddress { address = config.ServerUrl }) + "\n", new UTF8Encoding(false));
                AssetDatabase.ImportAsset(serverPath, ImportAssetOptions.ForceSynchronousImport);
            }
            else Debug.LogWarning("EMBERFIELD_IOS_OFFLINE_DEFAULT: no public HTTPS endpoint supplied; the shipped loopback default cannot connect from an iPhone. Offline play remains available.");
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.SaveAssets();
            EditorUserBuildSettings.development = false;
            EditorUserBuildSettings.allowDebugging = false;
            EditorUserBuildSettings.connectProfiler = false;
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = Scenes, target = BuildTarget.iOS, locationPathName = config.Output, options = BuildOptions.None
            });
            Directory.CreateDirectory(config.Output);
            File.WriteAllText(Path.Combine(config.Output, "emberfield-export-summary.txt"),
                "Result: " + report.summary.result + "\nUnity: " + Application.unityVersion + "\nBundle: " + config.BundleId +
                "\nVersion: " + config.Version + "\nBuild: " + config.BuildNumber + "\nErrors: " + report.summary.totalErrors +
                "\nWarnings: " + report.summary.totalWarnings + "\nDuration: " + report.summary.totalTime +
                "\nSigning: deferred to Codemagic/Xcode\nOnline endpoint: " + (config.ServerUrl.Length > 0 ? config.ServerUrl : "unchanged; offline default") + "\n");
            if (report.summary.result != BuildResult.Succeeded)
                throw new BuildFailedException("iOS Xcode export failed: " + report.summary.result + "; see Unity build log.");
            if (!Directory.Exists(Path.Combine(config.Output, "Unity-iPhone.xcodeproj")))
                throw new BuildFailedException("Unity reported success but Unity-iPhone.xcodeproj is missing.");
            Debug.Log("EMBERFIELD_IOS_EXPORT_OK " + config.Output + " (unsigned Xcode project, not an IPA or a TestFlight upload)");
        }

        public static void ValidateCopy(string project, string output)
        {
            project = Path.GetFullPath(project);
            output = Path.GetFullPath(output);
            if (!File.Exists(Path.Combine(project, PackageMarker)))
                throw new BuildFailedException("Run iOS export only in the materialized package copy containing " + PackageMarker + ". The working Unity project must not be reconfigured.");
            if (SamePath(project, output) || IsBelow(project, output))
                throw new BuildFailedException("Xcode output cannot contain or replace the Unity project.");
            foreach (string folder in new[] { "Assets", "Packages", "ProjectSettings", "Library" })
                if (SamePath(output, Path.Combine(project, folder)) || IsBelow(output, Path.Combine(project, folder)))
                    throw new BuildFailedException("Xcode output cannot be inside a Unity source or cache directory.");
            if (File.Exists(output) || (Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any()))
                throw new BuildFailedException("Xcode output already contains files. Choose a fresh output directory; Export never deletes existing files.");
            foreach (string folder in new[] { "Assets", "Packages", "ProjectSettings" })
            {
                string path = Path.Combine(project, folder);
                if (!Directory.Exists(path)) throw new BuildFailedException("Package is missing " + folder + ".");
                RequireMaterialized(path);
            }
        }

        private static void Configure(Configuration config)
        {
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, config.BundleId);
            PlayerSettings.bundleVersion = config.Version;
            PlayerSettings.iOS.buildNumber = config.BuildNumber;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.iOS, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.iOS, ManagedStrippingLevel.Minimal);
            PlayerSettings.SetArchitecture(NamedBuildTarget.iOS, (int)AppleMobileArchitecture.ARM64);
            PlayerSettings.iOS.sdkVersion = iOSSdkVersion.DeviceSDK;
            PlayerSettings.iOS.targetDevice = iOSTargetDevice.iPhoneAndiPad;
            PlayerSettings.iOS.targetOSVersionString = "15.0";
            PlayerSettings.iOS.requiresFullScreen = true;
            PlayerSettings.iOS.appleDeveloperTeamID = "";
            PlayerSettings.iOS.appleEnableAutomaticSigning = false;
            PlayerSettings.iOS.iOSManualProvisioningProfileID = "";
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.iOS, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.iOS, new[] { GraphicsDeviceType.Metal });
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
        }

        public static string PreservationXml(IEnumerable<Assembly> assemblies)
        {
            var text = new StringBuilder();
            using (var writer = XmlWriter.Create(text, new XmlWriterSettings { Indent = true, OmitXmlDeclaration = true }))
            {
                writer.WriteStartElement("linker");
                foreach (var assembly in assemblies.OrderBy(a => a.GetName().Name, StringComparer.Ordinal))
                {
                    string name = assembly.GetName().Name;
                    if (!name.StartsWith("Emberfield.", StringComparison.Ordinal) || name == "Emberfield.Editor" || name.StartsWith("Emberfield.Tests.", StringComparison.Ordinal)) continue;
                    var types = assembly.GetTypes().Where(t => !t.IsEnum && !t.IsGenericType &&
                        t.IsDefined(typeof(SerializableAttribute), false) && !typeof(UnityEngine.Object).IsAssignableFrom(t)).OrderBy(t => t.FullName, StringComparer.Ordinal).ToArray();
                    if (types.Length == 0) continue;
                    writer.WriteStartElement("assembly"); writer.WriteAttributeString("fullname", name);
                    // JsonUtility creates DTOs without explicit constructor calls. Keep those data types, including nested/private DTOs, not entire assemblies.
                    foreach (var type in types)
                    {
                        writer.WriteStartElement("type"); writer.WriteAttributeString("fullname", type.FullName.Replace('+', '/'));
                        writer.WriteAttributeString("preserve", "all"); writer.WriteEndElement();
                    }
                    writer.WriteEndElement();
                }
                writer.WriteEndElement();
            }
            return text.ToString();
        }

        private static void PrepareIcon()
        {
            // Original code-drawn provisional mark in the game's navy/gold palette. RGB24 produces an opaque PNG.
            const int size = 1024;
            var texture = new Texture2D(size, size, TextureFormat.RGB24, false);
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++) pixels[y * size + x] = IconPixel((x + .5f) / size, (y + .5f) / size);
            texture.SetPixels32(pixels); texture.Apply();
            string path = Generated + "/Emberfield-TestFlight-Icon.png";
            File.WriteAllBytes(path, texture.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Default; importer.alphaSource = TextureImporterAlphaSource.None;
            importer.mipmapEnabled = false; importer.maxTextureSize = size; importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            int count = 0;
            foreach (var kind in PlayerSettings.GetSupportedIconKinds(NamedBuildTarget.iOS))
            {
                var icons = PlayerSettings.GetPlatformIcons(NamedBuildTarget.iOS, kind);
                foreach (var icon in icons) { icon.SetTextures(Enumerable.Repeat(texture, icon.maxLayerCount).ToArray()); count++; }
                PlayerSettings.SetPlatformIcons(NamedBuildTarget.iOS, kind, icons);
            }
            if (count == 0) throw new BuildFailedException("The iOS module exposed no application icon slots.");
        }

        public static Color32 IconPixel(float x, float y)
        {
            float radius = Mathf.Sqrt((x - .5f) * (x - .5f) + (y - .5f) * (y - .5f));
            Color32 navy = new Color32(20, 32, 51, 255), gold = new Color32(224, 183, 103, 255);
            if (radius > .387f && radius < .396f) return gold;
            bool baseBand = x > .275f && x < .725f && y > .30f && y < .357f;
            bool body = x > .28f && x < .72f && y > .375f && y < .48f;
            bool left = InTriangle(x, y, new Vector2(.28f,.43f), new Vector2(.24f,.68f), new Vector2(.45f,.43f));
            bool middle = InTriangle(x, y, new Vector2(.37f,.43f), new Vector2(.5f,.73f), new Vector2(.63f,.43f));
            bool right = InTriangle(x, y, new Vector2(.55f,.43f), new Vector2(.76f,.68f), new Vector2(.72f,.43f));
            return baseBand || body || left || middle || right ? gold : navy;
        }

        private static bool InTriangle(float x, float y, Vector2 a, Vector2 b, Vector2 c)
        {
            var p = new Vector2(x, y);
            float one = Cross(p - b, a - b), two = Cross(p - c, b - c), three = Cross(p - a, c - a);
            return !((one < 0 || two < 0 || three < 0) && (one > 0 || two > 0 || three > 0));
        }
        private static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;
        private static void RequireMaterialized(string path)
        {
            if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                throw new BuildFailedException("Package contains a symbolic link/junction instead of copied content: " + path);
            if (!Directory.Exists(path)) return;
            foreach (string child in Directory.EnumerateFileSystemEntries(path)) RequireMaterialized(child);
        }
        private static string Default(string value, string fallback) => string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        private static bool SamePath(string left, string right) => string.Equals(left.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), right.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), PathComparison);
        private static bool IsBelow(string child, string parent) => child.StartsWith(parent.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar, PathComparison);
        private static StringComparison PathComparison => Application.platform == RuntimePlatform.WindowsEditor ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        [Serializable] private sealed class ServerAddress { public string address; }
    }
}
