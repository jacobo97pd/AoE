using System;
using Emberfield.Simulation;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Emberfield.Presentation
{
    public sealed class OfflineControls : IDisposable
    {
        private readonly MatchController match;
        private bool resultShown;
        public OfflineAi Ai { get; }
        public bool IsOpen { get; private set; }
        public bool IsPaused { get; private set; }
        public bool ConfirmSurrender { get; private set; }
        public bool BlocksWorldInput => IsOpen || match.World.Match != null && match.World.Match.IsFinished;
        public string ChosenFaction { get; private set; } = "aven";
        public string ChosenRealm => ContentRealms.RealmForFaction(ChosenFaction);
        public string ChosenMap { get; private set; } = "amber_crossing";
        public VictoryMode ChosenMode { get; private set; } = VictoryMode.Conquest;
        public OfflineAiDifficulty ChosenDifficulty { get; private set; } = OfflineAiDifficulty.Normal;
        public bool IsMatch => match.World.Match != null;

        public OfflineControls(MatchController match)
        {
            this.match = match;
            ChosenDifficulty = match.OfflineDifficulty;
            if (IsMatch)
            {
                if (!match.World.IsNetworkReplica) Ai = new OfflineAi(match.World, 2, match.OfflineDifficulty);
                ChosenFaction = match.Factions.LocalDefinition.Id;
                ChosenMap = match.World.Map.Id;
                ChosenMode = match.World.Match.Mode;
            }
        }
        public void Open()
        {
            if (match.World.IsNetworkReplica) { match.Online.Open(); return; }
            match.Research.Close(); match.Factions.Close(); match.ClearSelection();
            // An exercise is played against a clock, so the menu pauses it for the same reason a match pauses.
            IsOpen = true; IsPaused = IsMatch || match.IsChallenge; ConfirmSurrender = false;
            match.Hud?.Invalidate();
        }
        public void Close()
        {
            if (IsMatch && match.World.Match.IsFinished) return;
            IsOpen = IsPaused = ConfirmSurrender = false; match.Hud?.Invalidate();
        }
        public void ChooseFaction(string id)
        {
            string realm = ContentRealms.RealmForFaction(id);
            if (!ContentRealms.IsPlayableFactionInRealm(id, realm)) return;
            // A battlefield still on the last faction's suggestion follows the new one (the desert pair's Sunscar Basin).
            if (ChosenMap == FrontierCodex.HomeMap(ChosenFaction)) ChosenMap = FrontierCodex.HomeMap(id);
            ChosenFaction = id;
            if (!ContentRealms.IsMapAllowedInRealm(ChosenMap, realm)) ChosenMap = ContentRealms.DefaultMapForRealm(realm);
            match.Hud?.Invalidate();
        }
        public void ChooseRealm()
        {
            ChooseFaction(ContentRealms.FactionsForRealm(FrontierCodex.NextRealm(ChosenRealm))[0]);
            // A realm opens on its own battlefield: the fantasy realm on its Lands of Legend.
            ChosenMap = ContentRealms.DefaultMapForRealm(ChosenRealm); match.Hud?.Invalidate();
        }
        public void NextFaction()
        {
            var ids = ContentRealms.FactionsForRealm(ChosenRealm);
            ChooseFaction(ids[(Array.IndexOf(ids, ChosenFaction) + 1) % ids.Length]);
        }
        public void ChooseMap() { ChosenMap = FrontierCodex.NextMap(ChosenMap, ChosenRealm); match.Hud?.Invalidate(); }
        public void ChooseMode(VictoryMode mode) { ChosenMode = mode; match.Hud?.Invalidate(); }
        public void ChooseDifficulty(OfflineAiDifficulty difficulty) { ChosenDifficulty = difficulty; match.Hud?.Invalidate(); }
        public void Start() => match.StartOfflineMatch(ChosenFaction, ChosenMode, ChosenMap, ChosenDifficulty);
        public void RequestSurrender() { ConfirmSurrender = true; match.Hud?.Invalidate(); }
        public void Surrender()
        {
            if (!ConfirmSurrender) return;
            var result = match.SubmitPlayerCommand(new SurrenderCommand(MatchController.LocalPlayer));
            if (!result.Accepted) match.SetFeedback(result.Message);
            Observe();
        }
        public void Observe()
        {
            if (match.World.IsNetworkReplica) return;
            if (!IsMatch || !match.World.Match.IsFinished || resultShown) return;
            resultShown = true;
            match.Research.Close(); match.Factions.Close(); match.ClearSelection();
            IsOpen = IsPaused = true; ConfirmSurrender = false;
            match.Hud?.Invalidate();
        }
        public void PauseForFocus() { if (IsMatch && !match.World.IsNetworkReplica && !match.World.Match.IsFinished) Open(); }
        public void Sandbox() => SceneManager.LoadScene("Greybox");
        public void Dispose() { }
    }

    public sealed partial class MatchController
    {
        private static string nextOfflineFaction;
        private static string nextOfflineMap;
        private static VictoryMode nextOfflineMode;
        private static OfflineAiDifficulty? nextOfflineDifficulty;
        public string InitialOfflineFactionId;
        public VictoryMode InitialOfflineMode;
        /// <summary>The rival AI's difficulty for this scene's offline match.</summary>
        public OfflineAiDifficulty OfflineDifficulty { get; private set; } = OfflineAiDifficulty.Normal;
        public void StartOfflineMatch(string factionId, VictoryMode mode, string mapId = "amber_crossing", OfflineAiDifficulty difficulty = OfflineAiDifficulty.Normal)
        {
            string realm = ContentRealms.RealmForFaction(factionId);
            if (!ContentRealms.IsPlayableFactionInRealm(factionId, realm)) throw new ArgumentException("Choose an available faction.", nameof(factionId));
            if (!ContentRealms.IsMapAllowedInRealm(mapId, realm)) mapId = ContentRealms.DefaultMapForRealm(realm);
            nextFactionId = null;
            nextOfflineFaction = factionId; nextOfflineMode = mode;
            nextOfflineMap = mapId; nextOfflineDifficulty = difficulty;
            SceneManager.LoadScene("Greybox");
        }
        // A chosen difficulty survives the scene load; debug players also accept -emberfieldDifficulty.
        private void ConsumeOfflineDifficulty()
        {
            var chosen = nextOfflineDifficulty; nextOfflineDifficulty = null;
            if (chosen.HasValue) { OfflineDifficulty = chosen.Value; return; }
            if (!UnityEngine.Debug.isDebugBuild && !Application.isEditor) return;
            var args = Environment.GetCommandLineArgs(); int option = Array.IndexOf(args, "-emberfieldDifficulty");
            if (option >= 0 && option + 1 < args.Length && Enum.TryParse(args[option + 1], true, out OfflineAiDifficulty parsed) &&
                Enum.IsDefined(typeof(OfflineAiDifficulty), parsed)) OfflineDifficulty = parsed;
        }
        // ---------------------------------------------------------------- saved matches

        private static MatchArchive.Save pendingRestore;
        private MatchArchive.Save restoring;
        private int restoreCursor;
        /// <summary>True while a saved match is being replayed back into place.</summary>
        public bool IsRestoring => restoring != null;
        public float RestoreProgress => restoring == null || restoring.Tick <= 0 ? 1 : Mathf.Clamp01(World.TickIndex / (float)restoring.Tick);
        public bool HasSavedMatch => MatchArchive.Exists();

        public bool SaveMatch()
        {
            if (World.Match == null || World.IsNetworkReplica) { SetFeedback("Only a match in progress can be saved."); return false; }
            if (World.Journal.Overflowed) { SetFeedback("This match ran too long to save."); return false; }
            try
            {
                MatchArchive.Write(MatchArchive.Capture(World, World.Map.Id, Factions.LocalDefinition.Id, OfflineDifficulty));
            }
            catch (Exception error)
            {
                UnityEngine.Debug.LogWarning("Saving the match failed: " + error.Message);
                SetFeedback("The match could not be saved.");
                return false;
            }
            SetFeedback("Match saved.");
            return true;
        }

        /// <summary>
        /// Deals the saved start again and replays every order recorded into it. The scene reloads first, so
        /// the restored match begins from exactly the world the original one did.
        /// </summary>
        public bool LoadMatch()
        {
            MatchArchive.Save save;
            try { save = MatchArchive.Read(); }
            catch (Exception error) { UnityEngine.Debug.LogWarning("Reading the saved match failed: " + error.Message); save = null; }
            if (save == null) { SetFeedback("There is no saved match to restore."); return false; }
            pendingRestore = save;
            OfflineControls.Close();
            StartOfflineMatch(save.FactionId, (VictoryMode)save.Mode, save.MapId, (OfflineAiDifficulty)save.Difficulty);
            return true;
        }

        private void ConsumePendingRestore()
        {
            var save = pendingRestore; pendingRestore = null;
            if (save == null || World.Match == null) return;
            restoring = save; restoreCursor = 0;
            Feedback = "Restoring the saved match…";
        }

        /// <summary>
        /// Replays a slice of the saved orders each frame rather than the whole match at once, so the window
        /// keeps answering and the player can watch the restore land.
        /// </summary>
        private void AdvanceRestore()
        {
            restoreCursor = MatchArchive.Replay(World, restoring, restoreCursor, 400, out bool finished);
            if (!finished)
            {
                Feedback = "Restoring the saved match… " + Mathf.RoundToInt(RestoreProgress * 100) + "%";
                Hud?.Invalidate();
                SyncPresentation(1);
                return;
            }
            restoring = null;
            SetFeedback("Saved match restored.");
            SyncPresentation(1);
        }

        private void InitializeOfflineMatch()
        {
            if (World.Match == null) return;
            foreach (var building in World.Buildings)
                if (building.OwnerId == LocalPlayer && building.DefinitionId == "hearth")
                { Rig.SetHome(DefinitionLoader.ToWorld(building.Position) + new Vector3(0, 0, -1.5f), Rig.Camera.aspect > 1.7f ? 9 : 12); break; }
            Feedback = World.Match.Mode == VictoryMode.Conquest
                ? "Scout the crossing, build an army and destroy every enemy Hearth. Gather Food and Wood to begin."
                : "Capture two of three beacons with military units. Keep a majority for eight minutes; contesting interrupts the clock.";
        }
        public void ToggleTouchSelection()
        {
            input.Cancel(); TouchSelectionMode = !TouchSelectionMode;
            if (TouchSelectionMode) { Economy.CancelBuild(); Factions.CancelDeploy(); }
            SetFeedback(TouchSelectionMode ? "Select mode: tap your units or drag a group. Turn Select off to give orders." : "Command mode: tap to order; drag with a finger to pan. Two fingers pan and zoom.");
        }
    }
}
