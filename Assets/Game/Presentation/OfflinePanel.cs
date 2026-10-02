using System.Collections.Generic;
using Emberfield.Simulation;
using UnityEngine;
using UnityEngine.UI;

namespace Emberfield.Presentation
{
    public sealed class OfflinePanel
    {
        private readonly MatchController match;
        private readonly RectTransform root;
        private readonly Font font;
        private readonly Text heading, subtitle, description, realmChoice, factionChoice, mapChoice, difficultyChoice, conquest, dominion;
        private readonly Button start, close, resume, restart, surrender, confirm, sandbox, sound, mainMenu;
        private readonly Button online, factions, combat, restartPractice, save, load;
        private readonly List<Button> bottomRow = new List<Button>();
        private readonly GameObject choices;
        public RectTransform Root => root;
        public bool IsVisible => root.gameObject.activeSelf;

        public OfflinePanel(MatchController match, Transform parent, Font font)
        {
            this.match = match; this.font = AlphaTheme.Body;
            root = Rect("Offline match menu", parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            AlphaPanelDecor.Apply(root);
            heading = Label("EMBERFIELD / AMBER CROSSING", root, 30, new Vector2(26, -68), new Vector2(-210, -20));
            heading.font = AlphaTheme.Display; heading.color = AlphaTheme.Gold;
            close = Button("Return to map", root, match.OfflineControls.Close);
            Place(close, 1, 1, new Vector2(-192, -72), new Vector2(-26, -20));
            sound = Button("Sound: ON", root, () => { match.Alpha?.ToggleSound(); match.Hud.Invalidate(); });
            Place(sound, 1, 1, new Vector2(-365, -72), new Vector2(-202, -20));
            heading.rectTransform.offsetMax = new Vector2(-382, -20);
            subtitle = Label("A full offline 1v1", root, 24, new Vector2(26, -124), new Vector2(-26, -80));
            var body = Rect("Briefing", root, Vector2.zero, Vector2.one, new Vector2(26, 200), new Vector2(-26, -138));
            description = Label("", body, 22, new Vector2(18, -150), new Vector2(-18, -8));
            description.alignment = TextAnchor.UpperLeft; description.resizeTextForBestFit = true; description.resizeTextMinSize = 18; description.resizeTextMaxSize = 22;
            AlphaTheme.StylePanel(body);
            choices = Rect("Match choices", body, Vector2.zero, new Vector2(1, 0), new Vector2(18, 16), new Vector2(-18, 170)).gameObject;
            var choiceGrid = choices.AddComponent<GridLayoutGroup>(); choiceGrid.constraint = GridLayoutGroup.Constraint.FixedColumnCount; choiceGrid.constraintCount = 2; choiceGrid.spacing = new Vector2(14, 12);
            realmChoice = Choice("Offline realm", match.OfflineControls.ChooseRealm);
            factionChoice = Choice("Offline faction", match.OfflineControls.NextFaction);
            mapChoice = Choice("Offline battlefield", match.OfflineControls.ChooseMap);
            difficultyChoice = Choice("Offline difficulty", () => match.OfflineControls.ChooseDifficulty((OfflineAiDifficulty)(((int)match.OfflineControls.ChosenDifficulty + 1) % 3)));
            conquest = Choice("Conquest", () => match.OfflineControls.ChooseMode(VictoryMode.Conquest));
            dominion = Choice("Dominion", () => match.OfflineControls.ChooseMode(VictoryMode.Dominion));
            start = Button("Start match", root, match.OfflineControls.Start); Place(start, 0, 1, new Vector2(26, 116), new Vector2(-26, 182), true);
            resume = Button("Resume match", root, match.OfflineControls.Close); Place(resume, 0, .49f, new Vector2(26, 116), new Vector2(-8, 182), true);
            restart = Button("Play again", root, match.Restart); Place(restart, .51f, 1, new Vector2(8, 116), new Vector2(-26, 182), true);
            surrender = Button("Surrender...", root, match.OfflineControls.RequestSurrender); Place(surrender, 0, .32f, new Vector2(26, 34), new Vector2(-7, 100), true);
            confirm = Button("Confirm surrender", root, match.OfflineControls.Surrender); Place(confirm, 0, .32f, new Vector2(26, 34), new Vector2(-7, 100), true);
            sandbox = Button("Practice sandbox", root, match.OfflineControls.Sandbox); Place(sandbox, .34f, .66f, new Vector2(7, 34), new Vector2(-7, 100), true);
            mainMenu = Button("Main menu", root, () => {
                if (match.World.IsNetworkReplica) return;
                if (match.World.Match != null && match.World.Match.IsFinished) match.Shell?.ReturnFromMatch();
                else { match.OfflineControls.Close(); match.Shell?.Open(); }
            });
            // These left the command bar so that only orders remain there. Each one leaves the current
            // session behind, which is what this menu is for.
            save = Button("Save match", root, () => match.SaveMatch());
            load = Button("Load match", root, () => match.LoadMatch());
            restartPractice = Button("Restart", root, match.Restart);
            combat = Button("Battle", root, match.SwitchSandbox);
            online = Button("Online", root, () => { match.OfflineControls.Close(); match.Online.Open(); });
            factions = Button("Factions", root, () => { match.OfflineControls.Close(); match.Factions.Open(); });
            root.gameObject.SetActive(false);
        }
        public void Refresh()
        {
            var controls = match.OfflineControls;
            root.gameObject.SetActive(controls.IsOpen);
            if (!IsVisible) return;
            root.SetAsLastSibling();
            bool playing = controls.IsMatch;
            sound.gameObject.SetActive(match.View.Feedback != null);
            sound.GetComponentInChildren<Text>().text = SliceFeedback.Muted ? "Sound: OFF" : "Sound: ON";
            bool finished = playing && match.World.Match.IsFinished;
            description.rectTransform.anchorMin = playing ? Vector2.zero : new Vector2(0, 1);
            description.rectTransform.offsetMin = playing ? new Vector2(18, 18) : new Vector2(18, -150);
            heading.text = finished ? match.World.Match.WinnerId == 0 ? "MATCH DRAWN" : match.World.Match.WinnerId == 1 ? "VICTORY" : "DEFEAT" : playing ? "MATCH PAUSED" : "EMBERFIELD / PARTIDA LOCAL";
            subtitle.text = playing ? FrontierCodex.Name(controls.ChosenFaction) + "  /  " + FrontierCodex.MapName(match.World.Map.Id) + "  /  " + match.World.Match.Mode + "  /  " + controls.ChosenDifficulty + " rival  /  " + Clock(match.World.Match.ElapsedTicks) : "Choose your faction and victory condition";
            description.text = playing
                ? finished ? ResultText() : "Your offline match is paused.\n\nScout with a Tender or mounted unit. Build a Muster Hall, train an army and protect your Hearth. Research spends the same resources as production.\n\nDrag the left button to select a group, and hold shift to add to it. Pan with the arrow keys, the screen edge, the middle button or two fingers."
                : "Equal starts. Unexplored territory. A rival that gathers, builds, scouts and fights using the same rules.\n\nConquest: destroy every enemy Hearth.\nDominion: hold two of three beacons for eight uninterrupted minutes. An enemy presence contests a beacon and can reset the victory clock.";
            choices.SetActive(!playing);
            var grid = choices.GetComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(Mathf.Max(90, (((RectTransform)choices.transform).rect.width - grid.spacing.x) / 2), 48);
            realmChoice.text = FrontierCodex.RealmName(controls.ChosenRealm);
            factionChoice.text = FrontierCodex.Name(controls.ChosenFaction);
            mapChoice.text = FrontierCodex.MapName(controls.ChosenMap);
            difficultyChoice.text = "Rival: " + controls.ChosenDifficulty;
            if (!playing && controls.ChosenRealm == "naval") description.text = FrontierCodex.NavalBriefing + "\n" + FrontierCodex.NavalCounters + "\nConquest: destruye los Hearths. Dominion: controla los faros.";
            conquest.text = (controls.ChosenMode == VictoryMode.Conquest ? "✓  " : "") + "Conquest / destroy the Hearths";
            dominion.text = (controls.ChosenMode == VictoryMode.Dominion ? "✓  " : "") + "Dominion / hold the beacons";
            start.gameObject.SetActive(!playing); close.gameObject.SetActive(!finished);
            resume.gameObject.SetActive(playing && !finished); restart.gameObject.SetActive(playing);
            surrender.gameObject.SetActive(playing && !finished && !controls.ConfirmSurrender);
            confirm.gameObject.SetActive(playing && !finished && controls.ConfirmSurrender);
            sandbox.gameObject.SetActive(playing);
            mainMenu.gameObject.SetActive(!match.World.IsNetworkReplica && match.Shell != null);
            // A match in progress can be put away; a saved one can be taken up from either side of the menu.
            save.gameObject.SetActive(playing && !finished && !match.World.IsNetworkReplica);
            load.gameObject.SetActive(!match.World.IsNetworkReplica && match.HasSavedMatch);
            bool practice = !playing && !match.World.IsNetworkReplica;
            restartPractice.gameObject.SetActive(practice);
            combat.gameObject.SetActive(practice);
            combat.GetComponentInChildren<Text>().text = match.IsCombatSandbox ? "Economy" : "Battle";
            online.gameObject.SetActive(practice && match.Online != null);
            factions.gameObject.SetActive(practice && match.Factions.Available);
            LayoutBottomRow();
        }
        // The row holds a different set in a match than in practice, so it is shared out when it is shown.
        private void LayoutBottomRow()
        {
            bottomRow.Clear();
            foreach (var button in new[] { save, load, surrender, confirm, restartPractice, combat, online, factions, sandbox, mainMenu })
                if (button.gameObject.activeSelf) bottomRow.Add(button);
            for (int index = 0; index < bottomRow.Count; index++)
            {
                float left = index / (float)bottomRow.Count, right = (index + 1) / (float)bottomRow.Count;
                Place(bottomRow[index], left, right, new Vector2(index == 0 ? 26 : 7, 34),
                    new Vector2(index == bottomRow.Count - 1 ? -26 : -7, 100), true);
            }
        }
        private string ResultText()
        {
            match.World.TryGetPlayer(MatchController.LocalPlayer, out var player);
            // The match is over, so the rival's column is no longer hidden: seeing what beat you, or what you
            // beat, is most of what an end screen is for.
            return "Result: " + match.World.Match.Reason + "\nDuration: " + Clock(match.World.Match.ElapsedTicks)
                + "\nYour settlement: " + Tally(MatchController.LocalPlayer)
                + "\nRival settlement: " + Tally(MatchController.LocalPlayer == 1 ? 2 : 1)
                + "\nStock: " + EconomyControls.CostText(player.Resources)
                + "\n\nFor your next match: scout before choosing your army, keep workers gathering and check the objective clock. The result is final; restarting creates a fresh world.";
        }
        private string Tally(int playerId)
        {
            match.World.TryGetPlayer(playerId, out var player);
            int army = 0, workers = 0, buildings = 0;
            foreach (var unit in match.World.Units) if (unit.OwnerId == playerId) { if (unit.IsWorker) workers++; else if (unit.AttackDamage > 0) army++; }
            foreach (var building in match.World.Buildings) if (building.OwnerId == playerId) buildings++;
            return "Era " + player.EraTier + " · " + workers + " Tenders · " + army + " military units remaining · " + buildings + " buildings";
        }
        public static string Clock(long ticks) { long seconds = ticks / World.TickRate; return seconds / 60 + ":" + (seconds % 60).ToString("00"); }
        private Text Choice(string text, UnityEngine.Events.UnityAction action) => Button(text, choices.transform, action).GetComponentInChildren<Text>();
        private Text Label(string text, Transform parent, int size, Vector2 min, Vector2 max)
        {
            var rect = Rect("Label", parent, new Vector2(0, 1), Vector2.one, min, max);
            var label = rect.gameObject.AddComponent<Text>(); label.font = font; label.text = text; label.fontSize = size;
            label.color = AlphaTheme.Ink; label.alignment = TextAnchor.MiddleLeft; label.raycastTarget = false; return label;
        }
        private Button Button(string text, Transform parent, UnityEngine.Events.UnityAction action)
        {
            var rect = Rect(text, parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var image = rect.gameObject.AddComponent<Image>(); image.color = new Color(.18f, .32f, .34f);
            var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = image; button.onClick.AddListener(action);
            bool primary = text == "Start match" || text == "Resume match" || text == "Play again";
            button.onClick.AddListener(() => UiSound.Play(primary ? UiCue.Confirm : UiCue.Click));
            var label = Label(text, rect, 22, Vector2.zero, Vector2.zero); label.alignment = TextAnchor.MiddleCenter;
            label.resizeTextForBestFit = true; label.resizeTextMinSize = 17; label.resizeTextMaxSize = 22;
            label.rectTransform.anchorMin = Vector2.zero; label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = new Vector2(10, 5); label.rectTransform.offsetMax = new Vector2(-10, -5);
            AlphaTheme.StyleButton(button, primary); return button;
        }
        private static void Place(Button button, float left, float right, Vector2 min, Vector2 max, bool bottom = false)
        { var rect = button.GetComponent<RectTransform>(); rect.anchorMin = new Vector2(left, bottom ? 0 : 1); rect.anchorMax = new Vector2(right, bottom ? 0 : 1); rect.offsetMin = min; rect.offsetMax = max; }
        private static RectTransform Rect(string name, Transform parent, Vector2 a, Vector2 b, Vector2 min, Vector2 max)
        { var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); rect.SetParent(parent, false); rect.anchorMin = a; rect.anchorMax = b; rect.offsetMin = min; rect.offsetMax = max; return rect; }
    }
}
