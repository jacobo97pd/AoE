using Emberfield.Presentation;
using NUnit.Framework;

// Existing tests assert the text screens compose in their source language, so display translation stays off
// during the test run. LocalizationIntegrationTests turns it on for its own cases. The run's settings and reports
// also stay out of the player's own folder (LocalStorageTestSetup); this is the assembly's only SetUpFixture,
// because this NUnit hands every test to one fixture of a namespace and a second one would run for none.
[SetUpFixture]
public sealed class LocalizationTestSetup
{
    [OneTimeSetUp] public void ShowSourceText() { UiLocalization.Enabled = false; LocalStorageTestSetup.KeepTheRunsSettingsApart(); }
    [OneTimeTearDown] public void RestoreTranslation() { UiLocalization.Enabled = true; LocalStorageTestSetup.RestoreThePlayersFolder(); }
}
