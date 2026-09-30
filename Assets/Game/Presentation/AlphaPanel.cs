using System;
using UnityEngine;
using UnityEngine.UI;

namespace Emberfield.Presentation
{
    public sealed class AlphaPanel
    {
        private readonly MatchController match;
        private readonly Font font;
        private readonly RectTransform root;
        private readonly RectTransform[] pages = new RectTransform[3];
        private readonly Text notice, guide, reportInfo;
        private readonly Text[] settings = new Text[11], tabs = new Text[3];
        private readonly Text consent, tutorialState;
        private readonly Button startGuide, resumeGuide;
        private int page;
        public RectTransform Root => root;
        public bool IsVisible => root.gameObject.activeSelf;
        public AlphaPanel(MatchController match, Transform parent, Font font)
        {
            this.match = match; this.font = AlphaTheme.Body;
            root = Rect("Settings and guide", parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            AlphaPanelDecor.Apply(root);
            var title = Label("SETTINGS & GUIDE", root, 30, new Vector2(26, -74), new Vector2(-230, -20));
            title.font = AlphaTheme.Display; title.color = AlphaTheme.Gold;
            var close = Button("Return", root, match.Alpha.Close);
            Place(close, 1, 1, new Vector2(-202, -74), new Vector2(-26, -20));
            string[] names = { "Controls", "Guide", "Local reports" };
            for (int i = 0; i < 3; i++)
            {
                int captured = i;
                var tab = Button(names[i], root, () => { page = captured; match.Hud.Invalidate(); });
                Place(tab, i / 3f, (i + 1) / 3f, new Vector2(i == 0 ? 26 : 7, -146), new Vector2(i == 2 ? -26 : -7, -90));
                tabs[i] = tab.GetComponentInChildren<Text>();
                pages[i] = Rect(names[i], root, Vector2.zero, Vector2.one, new Vector2(26, 100), new Vector2(-26, -162));
                AlphaTheme.StylePanel(pages[i]);
            }
            Label("Tune the camera, touch and voice controls to your preference. Control and a digit stores the selected group, the digit alone brings it back, and pressing it twice takes the camera there; Home returns to your base. Voice starts off and uses Windows speech recognition on this device. Changes apply immediately; gameplay rules stay the same.", pages[0], 23, new Vector2(18, -100), new Vector2(-18, -10));
            UnityEngine.Events.UnityAction[] actions = { match.Alpha.ToggleSound, match.Alpha.CycleVolume, match.Alpha.CycleMusic, match.Alpha.CyclePan, match.Alpha.CycleZoom, match.Alpha.CycleTolerance, match.Alpha.ToggleContrast, match.Alpha.TogglePerformanceMetrics, match.Alpha.CycleVoiceMode, match.Alpha.CycleVoiceLanguage, match.Alpha.CycleLanguage };
            string[] settingNames = { "Setting 0", "Setting 1", "Setting 6", "Setting 2", "Setting 3", "Setting 4", "Setting 5", "Performance overlay", "Voice control", "Voice language", "Language" };
            // Three columns keep all thirteen controls inside the 720-pixel landscape layout.
            for (int i = 0; i < actions.Length; i++) settings[i] = Cell(pages[0], settingNames[i], actions[i], i, 114, 3).GetComponentInChildren<Text>();
            Cell(pages[0], "Restore defaults", match.Alpha.ResetSettings, 11, 114, 3);
            Cell(pages[0], "Open guide", () => { page = 1; match.Hud.Invalidate(); }, 12, 114, 3);
            tutorialState = Label("", pages[1], 25, new Vector2(18, -55), new Vector2(-18, -8));
            guide = Label("", pages[1], 22, new Vector2(18, -204), new Vector2(-18, -62));
            startGuide = Cell(pages[1], "Start again", match.Alpha.StartTutorial, 0, 220);
            resumeGuide = Cell(pages[1], "Resume guide", () => { match.Alpha.ResumeTutorial(); match.Alpha.Close(); match.OfflineControls.Close(); }, 1, 220);
            Cell(pages[1], "Pause guide", match.Alpha.StopTutorial, 2, 220);
            Cell(pages[1], "Back to controls", () => { page = 0; match.Hud.Invalidate(); }, 3, 220);
            Label("Optional reports stay on this device. They contain event categories, dates, match ticks and counts. They exclude account details, identifiers, log messages and stack traces. Nothing is uploaded. Disabling deletes local reports and exports.", pages[2], 22, new Vector2(18, -132), new Vector2(-18, -10));
            consent = Cell(pages[2], "Local reports", match.Alpha.ToggleDiagnostics, 0, 148).GetComponentInChildren<Text>();
            Cell(pages[2], "Delete reports", match.Alpha.ClearDiagnostics, 1, 148);
            Cell(pages[2], "Export report", match.Alpha.ExportDiagnostics, 2, 148);
            Cell(pages[2], "Open report folder", match.Alpha.OpenReportFolder, 3, 148);
            reportInfo = Label("", pages[2], 21, new Vector2(18, -382), new Vector2(-18, -298));
            notice = Label("", root, 20, new Vector2(26, 16), new Vector2(-26, 84));
            notice.rectTransform.anchorMin = Vector2.zero; notice.rectTransform.anchorMax = new Vector2(1, 0);
            root.gameObject.SetActive(false);
        }
        public void Refresh()
        {
            root.gameObject.SetActive(match.Alpha.IsOpen);
            if (!IsVisible) return;
            root.SetAsLastSibling();
            for (int i = 0; i < pages.Length; i++)
            {
                bool selected = i == page; pages[i].gameObject.SetActive(selected);
                tabs[i].color = selected ? AlphaTheme.Background : AlphaTheme.Ink;
                tabs[i].transform.parent.GetComponent<Image>().color = selected ? AlphaTheme.Gold : AlphaTheme.SurfaceRaised;
            }
            var value = match.Alpha.Settings.Value;
            settings[0].text = value.SoundEnabled ? "Sound: ON" : "Sound: OFF";
            settings[1].text = "Volume: " + Mathf.RoundToInt(value.SoundVolume * 100) + "%";
            settings[2].text = "Music: " + (value.MusicVolume <= 0 ? "OFF" : Mathf.RoundToInt(value.MusicVolume * 100) + "%");
            settings[3].text = "Camera pan: " + Speed(value.PanSpeed);
            settings[4].text = "Camera zoom: " + Speed(value.ZoomSpeed);
            settings[5].text = "Drag tolerance: " + (value.DragTolerance < .99f ? "Low" : value.DragTolerance < 1.49f ? "Normal" : "High");
            settings[6].text = value.HighContrastText ? "Text contrast: HIGH" : "Text contrast: STANDARD";
            settings[7].text = value.ShowPerformanceMetrics ? "Performance overlay: ON" : "Performance overlay: OFF";
            settings[8].text = "Voice: " + VoiceControls.ModeName((VoiceListenMode)value.VoiceMode);
            settings[9].text = "Voice language: " + VoiceControls.LanguageName((VoiceLanguageChoice)value.VoiceLanguage);
            settings[10].text = value.Language == 1 ? "Language: English" : "Language: Spanish (Spain)";
            tutorialState.text = value.TutorialCompleted ? "GUIDE COMPLETE / 9 OF 9" : "LEARN BY PLAYING / " + CountSteps(value.TutorialProgress) + " OF 9 COMPLETE";
            startGuide.interactable = resumeGuide.interactable = !match.World.IsNetworkReplica;
            guide.text = match.World.IsNetworkReplica ? "The guide is available in Practice and offline AI matches. Your completed steps are saved. Leave this match and return to Practice to continue learning; online play continues while Settings is open."
                : match.Alpha.Tutorial != null ? match.Alpha.Tutorial.Instruction
                : "Start in Practice to learn camera movement, selection, gathering, building, training, combat and Era progression. Then win a real AI match. The guide remembers completed steps across matches and can be paused any time.";
            consent.text = value.DiagnosticsConsent ? "Local reports: ON" : "Local reports: OFF";
            reportInfo.text = "Stored events: " + match.Alpha.Diagnostics.Records.Count + " / " + AlphaDiagnostics.Capacity
                + "\nAn interrupted session means the game did not close normally. It is not proof of a crash. You choose whether to share an exported file.";
            notice.text = match.Alpha.Notice;
        }
        private static int CountSteps(int mask) { int count = 0; for (int i = 0; i < 9; i++) if ((mask & (1 << i)) != 0) count++; return count; }
        private static string Speed(float value) => value < .99f ? "Slow" : value < 1.49f ? "Normal" : "Fast";
        private Button Cell(Transform parent, string title, UnityEngine.Events.UnityAction action, int index, float top, int columns = 2)
        {
            var button = Button(title, parent, action); int row = index / columns, column = index % columns;
            Place(button, column / (float)columns, (column + 1) / (float)columns, new Vector2(column == 0 ? 18 : 7, -top - row * 66 - 54), new Vector2(column == columns - 1 ? -18 : -7, -top - row * 66)); return button;
        }
        private Text Label(string text, Transform parent, int size, Vector2 min, Vector2 max)
        {
            var rect = Rect("Label", parent, new Vector2(0, 1), Vector2.one, min, max);
            var label = rect.gameObject.AddComponent<Text>(); label.font = font; label.text = text; label.fontSize = size;
            label.color = AlphaTheme.Ink; label.alignment = TextAnchor.MiddleLeft; label.raycastTarget = false;
            label.resizeTextForBestFit = true; label.resizeTextMinSize = 17; label.resizeTextMaxSize = size; return label;
        }
        private Button Button(string text, Transform parent, UnityEngine.Events.UnityAction action)
        {
            var rect = Rect(text, parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var image = rect.gameObject.AddComponent<Image>(); image.color = new Color(.18f, .32f, .34f);
            var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = image; button.onClick.AddListener(action);
            bool primary = text == "Start again" || text == "Resume guide";
            button.onClick.AddListener(() => UiSound.Play(primary ? UiCue.Confirm : UiCue.Click));
            var label = Label(text, rect, 22, new Vector2(8, 4), new Vector2(-8, -4)); label.alignment = TextAnchor.MiddleCenter;
            label.rectTransform.anchorMin = Vector2.zero; label.rectTransform.anchorMax = Vector2.one;
            AlphaTheme.StyleButton(button, primary); return button;
        }
        private static void Place(Button button, float left, float right, Vector2 min, Vector2 max)
        { var rect = button.GetComponent<RectTransform>(); rect.anchorMin = new Vector2(left, 1); rect.anchorMax = new Vector2(right, 1); rect.offsetMin = min; rect.offsetMax = max; }
        private static RectTransform Rect(string name, Transform parent, Vector2 a, Vector2 b, Vector2 min, Vector2 max)
        { var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); rect.SetParent(parent, false); rect.anchorMin = a; rect.anchorMax = b; rect.offsetMin = min; rect.offsetMax = max; return rect; }
    }

    // The guide used to hold a two-line board over the battlefield at every step. It now keeps one line,
    // opens on a tap for the full instruction, and folds itself away again when the step is done.
    public sealed class AlphaTutorialView
    {
        private const float CollapsedHeight = 36, ExpandedHeight = 116;
        private readonly MatchController match;
        private readonly RectTransform root;
        private readonly Text hint, instruction, chevron;
        private AlphaTutorialStep shownStep = (AlphaTutorialStep)(-1);
        private bool expanded;
        public RectTransform Root => root;
        public bool IsExpanded => expanded;
        public AlphaTutorialView(MatchController match, Transform parent, Font font)
        {
            this.match = match;
            root = new GameObject("Guide hint", typeof(RectTransform)).GetComponent<RectTransform>(); root.SetParent(parent, false);
            root.anchorMin = new Vector2(0, 1); root.anchorMax = Vector2.one;
            AlphaTheme.StylePanel(root);
            var open = root.gameObject.AddComponent<Button>(); open.targetGraphic = root.GetComponent<Image>();
            open.onClick.AddListener(Toggle); AlphaTheme.StyleButton(open);
            hint = Line("Next step", new Vector2(0, 1), Vector2.one, new Vector2(12, -30), new Vector2(-44, -6), 20);
            hint.color = AlphaTheme.Gold;
            instruction = Line("Instruction", Vector2.zero, Vector2.one, new Vector2(12, 10), new Vector2(-12, -32), 18);
            instruction.alignment = TextAnchor.UpperLeft; instruction.color = AlphaTheme.Ink;
            chevron = Line("Open guide", Vector2.one, Vector2.one, new Vector2(-36, -30), new Vector2(-12, -6), 20);
            chevron.text = "›"; chevron.color = AlphaTheme.Gold; chevron.alignment = TextAnchor.MiddleCenter;
            Place();
            root.gameObject.SetActive(false);
        }
        public void Toggle() { expanded = !expanded; Place(); }
        private void Place()
        {
            // Below the objective strip whenever one is on screen; a match and an exercise both draw it.
            float top = match.World.Match == null && !match.IsChallenge ? -98 : -158;
            root.offsetMax = new Vector2(-268, top);
            root.offsetMin = new Vector2(18, top - (expanded ? ExpandedHeight : CollapsedHeight));
            instruction.gameObject.SetActive(expanded);
            chevron.rectTransform.localEulerAngles = new Vector3(0, 0, expanded ? -90 : 0);
        }
        public void Refresh()
        {
            var tutorial = match.Alpha.Tutorial;
            root.gameObject.SetActive(tutorial != null && !match.Alpha.IsOpen && !match.OfflineControls.IsOpen && !match.Research.IsOpen && !match.Factions.IsOpen);
            if (!root.gameObject.activeSelf) return;
            if (shownStep != tutorial.Step)
            {
                // Someone who has not even moved the camera yet gets the first instruction opened for them.
                // Everyone else gets the quiet line and opens it when they want it.
                bool firstShown = (int)shownStep < 0;
                shownStep = tutorial.Step;
                expanded = firstShown && tutorial.Step == AlphaTutorialStep.Camera;
                Place();
            }
            hint.text = "GUIDE  " + tutorial.CompletedCount + "/9  ·  " + tutorial.Step.ToString().ToUpperInvariant();
            instruction.text = tutorial.Instruction;
        }
        private Text Line(string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax, int size)
        {
            var label = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); label.SetParent(root, false);
            label.anchorMin = anchorMin; label.anchorMax = anchorMax; label.offsetMin = offsetMin; label.offsetMax = offsetMax;
            var text = label.gameObject.AddComponent<Text>(); text.font = AlphaTheme.Strong; text.fontSize = size;
            text.alignment = TextAnchor.MiddleLeft; text.raycastTarget = false;
            text.resizeTextForBestFit = true; text.resizeTextMinSize = 15; text.resizeTextMaxSize = size;
            return text;
        }
    }
}
