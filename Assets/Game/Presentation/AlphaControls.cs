using System;
using System.IO;
using Emberfield.Simulation;
using UnityEngine;

namespace Emberfield.Presentation
{
    public sealed class AlphaControls
    {
        private readonly MatchController match;
        private int lastTutorialProgress;
        private bool resultRecorded;
        public AlphaSettingsStore Settings { get; }
        public AlphaDiagnostics Diagnostics { get; }
        public AlphaTutorial Tutorial { get; private set; }
        public bool IsOpen { get; private set; }
        public string Notice { get; private set; } = "Changes are saved on this device.";
        public AlphaControls(MatchController match)
        {
            this.match = match;
            AlphaLocalSession.Ensure(); Settings = AlphaLocalSession.Settings; Diagnostics = AlphaLocalSession.Diagnostics;
            Apply();
            if (Settings.Value.TutorialActive && match.Stress == null && !match.World.IsNetworkReplica) ResumeTutorial();
            if (match.World.Match != null) Diagnostics.Record(AlphaDiagnosticKind.MatchStarted, match.World.TickIndex, (int)match.World.Match.Mode);
        }
        public void Open()
        {
            match.Research.Close(); match.Factions.Close(); match.Online?.Close(); match.ClearSelection();
            IsOpen = true; Notice = "Changes are saved on this device."; match.Hud?.Invalidate();
        }
        public void Close() { IsOpen = false; match.Hud?.Invalidate(); }
        public void ToggleSound() { Settings.Value.SoundEnabled = !Settings.Value.SoundEnabled; Save(); }
        public void CycleVolume() { Settings.Value.SoundVolume = Settings.Value.SoundVolume >= .99f ? .25f : Mathf.Min(1, Settings.Value.SoundVolume + .25f); Save(); }
        // Music alone is allowed to reach zero: turning the score off should not cost you the game's other sounds.
        public void CycleMusic() { Settings.Value.MusicVolume = Settings.Value.MusicVolume >= .99f ? 0 : Mathf.Min(1, Settings.Value.MusicVolume + .25f); Save(); }
        public void CyclePan() { Settings.Value.PanSpeed = CycleSpeed(Settings.Value.PanSpeed); Save(); }
        public void CycleZoom() { Settings.Value.ZoomSpeed = CycleSpeed(Settings.Value.ZoomSpeed); Save(); }
        public void CycleTolerance() { Settings.Value.DragTolerance = CycleSpeed(Settings.Value.DragTolerance); Save(); }
        public void ToggleContrast() { Settings.Value.HighContrastText = !Settings.Value.HighContrastText; Save(); }
        public void TogglePerformanceMetrics() { Settings.Value.ShowPerformanceMetrics = !Settings.Value.ShowPerformanceMetrics; Save(); }
        public void CycleVoiceMode() { Settings.Value.VoiceMode = (Settings.Value.VoiceMode + 1) % 3; Save(); }
        public void CycleVoiceLanguage() { Settings.Value.VoiceLanguage = (Settings.Value.VoiceLanguage + 1) % 3; Save(); }
        public void SetVoiceMode(VoiceListenMode mode) { Settings.Value.VoiceMode = (int)mode; Save(); }
        public void CycleLanguage() { Settings.Value.Language = (Settings.Value.Language + 1) % 2; Save(); }
        public void CycleFrameRate() { Settings.Value.FrameRate = (Settings.Value.FrameRate + 1) % FramePacing.Choices; Save(); }
        public void ToggleDiagnostics()
        {
            bool enabled = !Settings.Value.DiagnosticsConsent;
            Settings.Value.DiagnosticsConsent = enabled;
            // Store the opt-in first. A failed write must not start collecting with an unpersisted preference.
            bool saved = Settings.Save();
            if (!saved && enabled) Settings.Value.DiagnosticsConsent = false;
            Diagnostics.SetConsent(Settings.Value.DiagnosticsConsent);
            Notice = saved && Diagnostics.StorageAvailable ? enabled ? "Local reports enabled. Nothing is sent automatically." : "Local reports disabled; stored reports and the export were deleted."
                : "The device could not save this change. Local collection stays off if consent could not be saved.";
            match.Hud?.Invalidate();
        }
        public void ClearDiagnostics()
        {
            Notice = Diagnostics.Clear() ? "Local reports and the previous export were deleted." : "Some local report files could not be deleted.";
            match.Hud?.Invalidate();
        }
        public void ExportDiagnostics()
        {
            Notice = Diagnostics.Export() ? "Report saved on this device. Use Open report folder to find diagnostics-export.json." : Diagnostics.Consented ? "The report could not be saved on this device." : "Enable local reports before exporting.";
            match.Hud?.Invalidate();
        }
        public void OpenReportFolder()
        {
            if (!Diagnostics.Consented || !File.Exists(Diagnostics.ExportPath)) { Notice = "Export a local report first."; match.Hud?.Invalidate(); return; }
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            var start = new System.Diagnostics.ProcessStartInfo("explorer.exe", "/select,\"" + Diagnostics.ExportPath + "\"") { UseShellExecute = true };
            try { System.Diagnostics.Process.Start(start); Notice = "The exported report is selected in the file browser."; }
            catch (Exception error) when (error is System.ComponentModel.Win32Exception || error is InvalidOperationException) { Notice = "The file browser could not be opened. The local export is still saved."; }
#else
            Notice = "The report is saved in the game's local data folder. Sharing from this device is not available in this build.";
#endif
            match.Hud?.Invalidate();
        }
        public void ResetSettings()
        {
            Diagnostics.SetConsent(false); Settings.Reset(); Tutorial = null; lastTutorialProgress = 0; Apply();
            Notice = Settings.LastWriteSucceeded && Diagnostics.StorageAvailable ? "Defaults restored. Tutorial progress and local reports were cleared." : "Defaults restored for this session; some local files could not be updated.";
            match.Hud?.Invalidate();
        }
        public void StartTutorial()
        {
            if (match.World.IsNetworkReplica) { Notice = "The guide is available in Practice and offline AI matches."; match.Hud?.Invalidate(); return; }
            Settings.Value.TutorialProgress = 0; Settings.Value.TutorialCompleted = false; ResumeTutorial();
            IsOpen = false; match.OfflineControls.Close(); match.SetFeedback("The guide follows your actions. You can stop or restart it from Settings.");
        }
        public void ResumeTutorial()
        {
            if (match.Stress != null || match.World.IsNetworkReplica) { Notice = "The guide is available in Practice and offline AI matches."; return; }
            match.Shell?.Close();
            Tutorial = new AlphaTutorial(match.World, MatchController.LocalPlayer, Settings.Value.TutorialProgress);
            lastTutorialProgress = Tutorial.Progress; Settings.Value.TutorialActive = true;
            Tutorial.ObserveCamera(match.Rig.Camera.transform.position, match.Rig.Camera.orthographicSize);
            Save();
        }
        public void StopTutorial()
        {
            Settings.Value.TutorialActive = false; Tutorial = null; Save();
            Notice = "Guide paused. Resume keeps completed steps; Start again clears them.";
        }
        public void ObserveCommand(IGameCommand command, CommandResult result) => Tutorial?.ObserveCommand(command, result);
        public void Observe()
        {
            if (Tutorial != null)
            {
                Tutorial.ObserveCamera(match.Rig.Camera.transform.position, match.Rig.Camera.orthographicSize);
                Tutorial.ObserveSelection(match.Selection); Tutorial.ObserveWorld();
                if (Tutorial.Progress != lastTutorialProgress)
                {
                    lastTutorialProgress = Settings.Value.TutorialProgress = Tutorial.Progress;
                    Settings.Value.TutorialCompleted = Tutorial.IsComplete;
                    Diagnostics.Record(AlphaDiagnosticKind.TutorialStep, match.World.TickIndex, Tutorial.CompletedCount);
                    Save();
                }
            }
            if (!resultRecorded && match.World.Match != null && match.World.Match.IsFinished)
            { resultRecorded = true; Diagnostics.Record(AlphaDiagnosticKind.MatchFinished, match.World.TickIndex, match.World.Match.WinnerId); }
        }
        private void Save()
        {
            Settings.Save(); Apply();
            Notice = Settings.LastWriteSucceeded ? "Changes are saved on this device." : "Changes apply now, but could not be saved on this device.";
            match.Hud?.Invalidate();
        }
        private void Apply()
        {
            Settings.Value.Normalize(); AudioListener.volume = Settings.Value.SoundVolume;
            match.Rig.PanSensitivity = Settings.Value.PanSpeed; match.Rig.ZoomSensitivity = Settings.Value.ZoomSpeed;
            if (match.View.Feedback != null && SliceFeedback.Muted == Settings.Value.SoundEnabled) match.View.Feedback.ToggleMute();
            match.Voice?.ApplySettings();
            FramePacing.Choose(Settings.Value.FrameRate);
            UiLocalization.SetLanguage(Settings.Value.Language == 1 ? Emberfield.Localization.GameLanguage.English : Emberfield.Localization.GameLanguage.Spanish);
        }
        private static float CycleSpeed(float current) => current >= 1.49f ? .75f : current < .99f ? 1 : 1.5f;
    }

    // One session survives scene changes; scene transitions must not be mistaken for crashed sessions.
    internal static class AlphaLocalSession
    {
        internal static AlphaSettingsStore Settings { get; private set; }
        internal static AlphaDiagnostics Diagnostics { get; private set; }
        // Where the next session keeps its files, instead of the player's own data folder. The PlayMode suite sets it for
        // its whole run (LocalStorageTestSetup), so no test reads or saves the player's settings and reports.
        internal static string DirectoryOverride;
        private static DateTime lastError = DateTime.MinValue;
        internal static void Ensure()
        {
            if (Settings != null) return;
            string directory = DirectoryOverride ?? Path.Combine(NativeSmokeStorage.Root, "Alpha");
            Settings = new AlphaSettingsStore(directory); Diagnostics = new AlphaDiagnostics(directory, Settings.Value.DiagnosticsConsent);
            Application.logMessageReceived += OnLog; Application.quitting += Shutdown;
        }
        private static void OnLog(string ignoredMessage, string ignoredStack, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert || Diagnostics == null || !Diagnostics.Consented) return;
            var now = DateTime.UtcNow; if ((now - lastError).TotalSeconds < 2) return; lastError = now;
            Diagnostics.Record(type == LogType.Exception ? AlphaDiagnosticKind.ExceptionObserved : type == LogType.Assert ? AlphaDiagnosticKind.AssertionObserved : AlphaDiagnosticKind.ErrorObserved);
        }
        private static void Shutdown()
        {
            Application.logMessageReceived -= OnLog; Application.quitting -= Shutdown;
            Diagnostics?.Dispose(); Diagnostics = null; Settings = null;
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetDomain() { Shutdown(); lastError = DateTime.MinValue; }
    }

    public sealed partial class MatchController
    {
        public AlphaControls Alpha { get; private set; }
        private void InitializeAlpha() { if (Stress == null) Alpha = new AlphaControls(this); }
        private void ObserveAlpha() => Alpha?.Observe();
        private void ObserveAlphaCommand(IGameCommand command, CommandResult result) => Alpha?.ObserveCommand(command, result);
    }
}
