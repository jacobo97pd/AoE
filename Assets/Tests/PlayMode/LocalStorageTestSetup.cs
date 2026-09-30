using System;
using System.IO;
using System.Reflection;
using Emberfield.Presentation;

// Every match the suite opens starts an Alpha session, which reads and saves settings and reports in the player's own
// data folder: a test that muted the sound once left the real game silent. The whole run keeps them in a folder of its
// own instead, starting from the defaults, and deletes it at the end. Called from the run's one SetUpFixture
// (LocalizationTestSetup): this NUnit gives every test to a single fixture of a namespace, so a second would starve it.
internal static class LocalStorageTestSetup
{
    private static readonly Type Session = typeof(AlphaControls).Assembly.GetType("Emberfield.Presentation.AlphaLocalSession", true);
    private static readonly FieldInfo StorageOverride = typeof(NativeSmokeStorage).GetField("DirectoryOverride", BindingFlags.NonPublic | BindingFlags.Static);
    private static string directory;

    internal static void KeepTheRunsSettingsApart()
    {
        directory = Path.Combine(Path.GetTempPath(), "EmberfieldPlayModeAlpha-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        Restart(directory);
    }

    internal static void RestoreThePlayersFolder()
    {
        Restart(null);
        try { if (directory != null && Directory.Exists(directory)) Directory.Delete(directory, true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    // A session already open keeps its folder, so it is closed and the next match opens one where this says.
    private static void Restart(string folder)
    {
        Session.GetMethod("Shutdown", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
        StorageOverride.SetValue(null, folder);
        Session.GetField("DirectoryOverride", BindingFlags.NonPublic | BindingFlags.Static).SetValue(null, folder);
    }
}
