using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Emberfield.Presentation
{
    // Resolve before any scene creates settings, diagnostics, save files or local preferences.
    // Each native smoke keeps its data under its own evidence folder, including after a guest restart.
    public static class NativeSmokeStorage
    {
        internal static string DirectoryOverride;
        private static bool resolved;
        private static string smokeRoot, ordinaryRoot;
        private static readonly Dictionary<string, Dictionary<string, string>> preferences =
            new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);

        public static string Root
        {
            get
            {
                if (DirectoryOverride != null) return Path.GetFullPath(DirectoryOverride);
                ResolveRuntime();
                return smokeRoot ?? ordinaryRoot;
            }
        }
        public static bool IsIsolated
        {
            get { if (DirectoryOverride != null) return true; ResolveRuntime(); return smokeRoot != null; }
        }
        private static void ResolveRuntime()
        {
            if (resolved) return;
            smokeRoot = ResolveRoot(null, null, Environment.GetCommandLineArgs(), Debug.isDebugBuild);
            ordinaryRoot = Application.persistentDataPath;
            resolved = true;
        }

        internal static string ResolveRoot(string directoryOverride, string ordinaryRoot, string[] arguments, bool development)
        {
            if (directoryOverride != null) return Path.GetFullPath(directoryOverride);
            string option = null, value = null;
            for (int i = 0; i < arguments.Length; i++)
            {
                if (arguments[i] != "-emberfieldOnlineSmoke" && arguments[i] != "-emberfieldProductSmoke") continue;
                if (option != null || i + 1 >= arguments.Length || string.IsNullOrWhiteSpace(arguments[i + 1]) ||
                    arguments[i + 1].StartsWith("-", StringComparison.Ordinal))
                    throw new ArgumentException("Native smoke storage requires one valid output or config path.");
                option = arguments[i]; value = arguments[++i];
            }
            if (option == null) return ordinaryRoot;
            if (!development) throw new InvalidOperationException("Native smoke storage requires a development player.");
            if (!Path.IsPathRooted(value)) throw new ArgumentException("Native smoke storage requires an absolute path.");
            string path = Path.GetFullPath(value);
            if (option == "-emberfieldProductSmoke") return Path.Combine(path, "local-state");
            if (!string.Equals(Path.GetExtension(path), ".json", StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrEmpty(Path.GetFileNameWithoutExtension(path)))
                throw new ArgumentException("Native online smoke storage requires a JSON config path.");
            return Path.Combine(Path.GetDirectoryName(path), "local-state", Path.GetFileNameWithoutExtension(path));
        }

        public static string GetPreference(string key, string fallback = "")
        {
            if (!IsIsolated) return PlayerPrefs.GetString(key, fallback);
            return preferences.TryGetValue(Root, out var values) && values.TryGetValue(key, out string value) ? value : fallback;
        }
        public static void SetPreference(string key, string value)
        {
            if (!IsIsolated) { PlayerPrefs.SetString(key, value); PlayerPrefs.Save(); return; }
            string root = Root;
            if (!preferences.TryGetValue(root, out var values)) preferences.Add(root, values = new Dictionary<string, string>(StringComparer.Ordinal));
            values[key] = value;
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetDomain() { resolved = false; smokeRoot = ordinaryRoot = null; preferences.Clear(); }
    }
}
