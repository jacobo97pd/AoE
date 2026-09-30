using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Emberfield.Presentation
{
    [Serializable]
    public sealed class AlphaSettings
    {
        public int Version = 1;
        public bool SoundEnabled = true;
        public float SoundVolume = .8f;
        // The score is the one sound a player may want gone while keeping the game audible, so it
        // carries its own level underneath the master volume rather than sharing it.
        public float MusicVolume = .7f;
        public float PanSpeed = 1;
        public float ZoomSpeed = 1;
        public float DragTolerance = 1;
        public bool HighContrastText;
        public bool ShowPerformanceMetrics;
        public bool DiagnosticsConsent;
        public bool TutorialCompleted;
        public bool TutorialActive;
        public int TutorialProgress;
        // Which short exercises have been passed. Identifiers rather than a bitmask: the course is meant to
        // grow and be reordered, and a saved record must not start naming a different exercise when it does.
        public List<string> ChallengesPassed = new List<string>();
        // Voice commands stay off until the player turns them on: 0 off, 1 push-to-talk, 2 always listening.
        public int VoiceMode;
        // 0 follows the system language, 1 Spanish, 2 English.
        public int VoiceLanguage;
        // Display language: 0 Spanish (Spain), the default; 1 English.
        public int Language;
        // Desktop frame pacing (FramePacing): 0 waits for the display, 1 caps at 60, 2 at 30, 3 unlimited.
        public int FrameRate;

        public const int MaximumRecordedChallenges = 64;

        public void Normalize()
        {
            Version = 1;
            SoundVolume = FiniteClamp(SoundVolume, 0, 1, .8f);
            MusicVolume = FiniteClamp(MusicVolume, 0, 1, .7f);
            PanSpeed = FiniteClamp(PanSpeed, .5f, 2, 1);
            ZoomSpeed = FiniteClamp(ZoomSpeed, .5f, 2, 1);
            DragTolerance = FiniteClamp(DragTolerance, .5f, 2, 1);
            TutorialProgress &= AlphaTutorial.AllSteps;
            if (ChallengesPassed == null) ChallengesPassed = new List<string>();
            // A settings file is editable by hand, so what comes back is bounded and deduplicated.
            var kept = new List<string>();
            foreach (string id in ChallengesPassed)
                if (!string.IsNullOrWhiteSpace(id) && id.Length <= 64 && !kept.Contains(id) && kept.Count < MaximumRecordedChallenges) kept.Add(id);
            ChallengesPassed = kept;
            TutorialCompleted = TutorialProgress == AlphaTutorial.AllSteps;
            VoiceMode = Mathf.Clamp(VoiceMode, 0, 2);
            VoiceLanguage = Mathf.Clamp(VoiceLanguage, 0, 2);
            Language = Mathf.Clamp(Language, 0, 1);
            FrameRate = Mathf.Clamp(FrameRate, 0, FramePacing.Choices - 1);
        }
        private static float FiniteClamp(float value, float min, float max, float fallback)
            => float.IsNaN(value) || float.IsInfinity(value) ? fallback : Mathf.Clamp(value, min, max);
    }

    // No account data or identifiers are stored here. A version mismatch falls back to defaults.
    public sealed class AlphaSettingsStore
    {
        public const int MaximumFileBytes = 8192;
        private readonly string path;
        public AlphaSettings Value { get; private set; }
        public bool LastWriteSucceeded { get; private set; } = true;
        public AlphaSettingsStore(string directory)
        {
            if (string.IsNullOrEmpty(directory)) throw new ArgumentException("Settings require a directory.", nameof(directory));
            path = Path.Combine(directory, "settings.json"); Value = Read(path);
        }
        public bool Save()
        {
            Value.Normalize();
            return LastWriteSucceeded = AlphaLocalFiles.Write(path, JsonUtility.ToJson(Value, true));
        }
        public bool Reset()
        {
            Value = new AlphaSettings(); return Save();
        }
        private static AlphaSettings Read(string file)
        {
            try
            {
                if (!File.Exists(file) || new FileInfo(file).Length > MaximumFileBytes) return new AlphaSettings();
                string json = File.ReadAllText(file);
                // Require the schema field: JsonUtility may otherwise materialize missing fields as zeroes.
                if (json.IndexOf("\"Version\"", StringComparison.Ordinal) < 0) return new AlphaSettings();
                var value = new AlphaSettings();
                JsonUtility.FromJsonOverwrite(json, value);
                if (value == null || value.Version != 1) return new AlphaSettings();
                value.Normalize(); return value;
            }
            catch (Exception exception) when (AlphaLocalFiles.IsStorageError(exception) || exception is ArgumentException)
            { return new AlphaSettings(); }
        }
    }

    internal static class AlphaLocalFiles
    {
        internal static bool IsStorageError(Exception error)
            => error is IOException || error is UnauthorizedAccessException || error is System.Security.SecurityException;
        internal static bool Write(string path, string contents)
        {
            string temporary = path + ".tmp";
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(temporary, contents, new System.Text.UTF8Encoding(false));
                if (File.Exists(path)) File.Replace(temporary, path, null);
                else File.Move(temporary, path);
                return true;
            }
            catch (Exception error) when (IsStorageError(error)) { return false; }
            finally { try { if (File.Exists(temporary)) File.Delete(temporary); } catch (Exception error) when (IsStorageError(error)) { } }
        }
        internal static bool Delete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); return true; }
            catch (Exception error) when (IsStorageError(error)) { return false; }
        }
    }
}
