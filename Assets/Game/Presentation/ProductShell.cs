using System;
using System.Collections.Generic;
using Emberfield.Simulation;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace Emberfield.Presentation
{
    // The product front door is a native uGUI layer. It never owns game rules or a store wallet.
    public sealed partial class ProductShell : IDisposable
    {
        private readonly MatchController match;
        private readonly GameObject root;
        private readonly RectTransform safe, content;
        private readonly RawImage artwork;
        private readonly Text footer;
        private Rect screenSafe;
        private int width, height;
        private string page = "home", faction = "aven";
        private VictoryMode mode = VictoryMode.Conquest;
        private bool resumePanel;
        private static bool sessionEntered;
        private static bool returnHome;
        private static bool startGuide;
        public bool IsOpen => root != null && root.activeSelf;
        public bool BlocksLocalSimulation => IsOpen || resumePanel;
        public string Page => page;
        public bool HasCatalog => CosmeticLoadout.Catalog.Count > 0;
        public bool PurchasesAvailable => cosmeticsReply?.purchasesAvailable == true;
        public RectTransform Root => (RectTransform)root.transform;

        public ProductShell(MatchController match, Transform parent)
        {
            this.match = match;
            root = new GameObject("Emberfield Frontend", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            root.transform.SetParent(parent, false);
            var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 120;
            var scaler = root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900); scaler.matchWidthOrHeight = 1;
            var background = Rect("Key art", root.transform); Stretch(background);
            artwork = background.gameObject.AddComponent<RawImage>(); artwork.color = Color.white;
            artwork.texture = Resources.Load<Texture2D>("Interface/EmberfieldKeyArt");
            if (artwork.texture == null) artwork.color = AlphaTheme.Background;
            var shadow = Rect("Readability gradient", root.transform); Stretch(shadow);
            var gradient = shadow.gameObject.AddComponent<AlphaGradient>(); gradient.Left = new Color(.025f, .055f, .06f, .83f); gradient.Right = new Color(.025f, .055f, .06f, .08f); gradient.raycastTarget = false;
            var shade = Rect("Footer gradient", root.transform); Place(shade, new Vector2(0, 0), new Vector2(1, .22f));
            var bottomGradient = shade.gameObject.AddComponent<AlphaGradient>(); bottomGradient.Vertical = true; bottomGradient.Left = new Color(.02f, .045f, .05f, .94f); bottomGradient.Right = Color.clear; bottomGradient.raycastTarget = false;
            safe = Rect("Safe frontend", root.transform); Stretch(safe);
            var masthead = Label("Brand", safe, "EMBERFIELD", 32, AlphaTheme.Ink, true);
            Place(masthead.rectTransform, new Vector2(.045f, .91f), new Vector2(.33f, .97f));
            var alpha = Label("Build channel", safe, "A L P H A  /  0.3", 13, AlphaTheme.Gold);
            Place(alpha.rectTransform, new Vector2(.045f, .875f), new Vector2(.28f, .91f));
            // Five destinations share the masthead, so the row is stepped to keep the last one inside it.
            Nav("Home", .50f, () => Navigate("home"));
            Nav("Play", .60f, () => Navigate("skirmish"));
            Nav("Train", .70f, () => Navigate("challenges"));
            Nav("Store", .80f, () => Navigate("store"));
            Nav("Settings", .90f, () => Navigate("settings"));
            LanguageSwitch();
            var rule = Rect("Masthead rule", safe); Place(rule, new Vector2(.045f, .861f), new Vector2(.955f, .862f));
            rule.gameObject.AddComponent<Image>().color = new Color(AlphaTheme.Gold.r, AlphaTheme.Gold.g, AlphaTheme.Gold.b, .4f);
            content = Rect("Frontend content", safe); Place(content, new Vector2(.045f, .13f), new Vector2(.955f, .83f));
            footer = Label("Footer", safe, "REALMS & FRONTIERS     /     TEN FACTIONS. FOUR BATTLEFIELDS.", 14, AlphaTheme.Muted);
            Place(footer.rectTransform, new Vector2(.045f, .035f), new Vector2(.76f, .078f));
            var quit = Action(safe, "Quit game", "EXIT GAME", () => SafeQuit.Request(0));
            Place((RectTransform)quit.transform, new Vector2(.835f, .03f), new Vector2(.955f, .09f));
            root.SetActive(false);
            bool explicitDemo = false;
            foreach (string argument in Environment.GetCommandLineArgs()) if (argument.StartsWith("-emberfield", StringComparison.OrdinalIgnoreCase) && !argument.Equals("-emberfieldServer", StringComparison.OrdinalIgnoreCase)) explicitDemo = true;
            if (returnHome || !Application.isEditor && !sessionEntered && !explicitDemo && !match.World.IsNetworkReplica && match.Stress == null)
            { returnHome = false; Open(); }
            if (startGuide) { startGuide = false; match.Alpha.StartTutorial(); }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() { sessionEntered = false; returnHome = false; startGuide = false; }

        public void Open()
        {
            // An ongoing online match can only be resumed or explicitly surrendered through its own panel.
            if (match.World.IsNetworkReplica && !match.World.Match.IsFinished) { match.Online.Open(); return; }
            root.SetActive(true); sessionEntered = true; resumePanel = false;
            match.Research.Close(); match.Factions.Close(); match.Alpha?.Close(); match.Online.Close();
            match.ClearSelection(); Navigate("home"); Update();
        }
        public void Close() { root.SetActive(false); resumePanel = false; }
        public void Navigate(string destination)
        {
            if (destination != "home" && destination != "skirmish" && destination != "challenges" && destination != "store" && destination != "settings") throw new ArgumentException("Unknown frontend page.");
            page = destination;
            for (int i = content.childCount - 1; i >= 0; i--) { var child = content.GetChild(i).gameObject; child.SetActive(false); UnityEngine.Object.Destroy(child); }
            footer.text = page == "store" ? "THE QUARTERMASTER     /     APPEARANCE ONLY"
                : page == "challenges" ? "THE TRAINING GROUND     /     FIVE SHORT EXERCISES"
                : "REALMS & FRONTIERS     /     TEN FACTIONS. FOUR BATTLEFIELDS.";
            if (page == "home") Home(); else if (page == "skirmish") Skirmish(); else if (page == "challenges") Challenges(); else if (page == "store") Store(); else Settings();
        }
        public void ReturnFromMatch()
        {
            if (match.World.IsNetworkReplica) { match.Online.Open(); return; }
            returnHome = true; SceneManager.LoadScene("Greybox");
        }
        public void Update()
        {
            ObserveStoreSession();
            ObserveChallenge();
            if (resumePanel && !match.Online.IsOpen && !match.Alpha.IsOpen && !match.World.IsNetworkReplica) Open();
            if (!IsOpen) return;
            if (width != Screen.width || height != Screen.height || screenSafe != Screen.safeArea)
            {
                width = Screen.width; height = Screen.height; screenSafe = Screen.safeArea;
                safe.anchorMin = new Vector2(screenSafe.xMin / Math.Max(1, width), screenSafe.yMin / Math.Max(1, height));
                safe.anchorMax = new Vector2(screenSafe.xMax / Math.Max(1, width), screenSafe.yMax / Math.Max(1, height));
                safe.offsetMin = safe.offsetMax = Vector2.zero;
                if (artwork.texture != null)
                {
                    float screenAspect = width / (float)Math.Max(1, height), textureAspect = artwork.texture.width / (float)artwork.texture.height;
                    float x = Mathf.Min(1, screenAspect / textureAspect), y = Mathf.Min(1, textureAspect / screenAspect);
                    artwork.uvRect = new Rect((1 - x) * .5f, (1 - y) * .5f, x, y);
                }
            }
        }

        private void Home()
        {
            var eyebrow = Label("Home eyebrow", content, "A NEW AGE OF STRATEGY", 16, AlphaTheme.Gold);
            Place(eyebrow.rectTransform, new Vector2(0, .84f), new Vector2(.57f, .92f));
            var headline = Label("Home headline", content, "Every empire\nstarts with you.", 65, AlphaTheme.Ink, true);
            Place(headline.rectTransform, new Vector2(0, .49f), new Vector2(.58f, .85f));
            headline.resizeTextForBestFit = true; headline.resizeTextMinSize = 42; headline.resizeTextMaxSize = 65;
            var intro = Label("Home introduction", content, "Raise a kingdom. Command creatures and siege.\nChoose your world. Claim your frontier.", 23, AlphaTheme.Ink);
            Place(intro.rectTransform, new Vector2(0, .31f), new Vector2(.55f, .48f));
            bool resumable = match.World.Match != null && !match.World.Match.IsFinished && !match.World.IsNetworkReplica;
            var play = Action(content, "Play skirmish", resumable ? "RESUME YOUR SKIRMISH    ›" : "PLAY A SKIRMISH    ›", () => { if (resumable) { match.OfflineControls.Close(); Close(); } else Navigate("skirmish"); }, true);
            Place((RectTransform)play.transform, new Vector2(0, .145f), new Vector2(.35f, .27f));
            // The second row carries whatever this session actually offers, shared out evenly: a saved match
            // has to be reachable from the front door, not only from inside a match already running.
            var secondary = new List<(string Name, string Label, Action Act)>();
            if (match.HasSavedMatch && !match.World.IsNetworkReplica)
                secondary.Add(("Continue saved match", "CONTINUE SAVED MATCH", () => { Close(); match.LoadMatch(); }));
            secondary.Add(("Play online", "MULTIPLAYER", () => { Close(); match.Online.Open(); resumePanel = true; }));
            secondary.Add(("Learn to play", "LEARN TO PLAY", PracticeGuide));
            secondary.Add(("Challenges", "CHALLENGES", () => Navigate("challenges")));
            if (ReferenceCharacterVisuals.Entries.Length > 0 && ReferenceCharacterReview.CanOpen(match.Online.State, match.Online.Busy))
                secondary.Add(("Character collection", "PERSONAJES", ReferenceCharacterReview.OpenFromGame));
            const float rowStart = 0f, rowEnd = .66f, gap = .015f;
            float slot = (rowEnd - rowStart + gap) / secondary.Count;
            for (int index = 0; index < secondary.Count; index++)
            {
                var entry = secondary[index];
                var button = Action(content, entry.Name, entry.Label, entry.Act);
                float left = rowStart + index * slot;
                Place((RectTransform)button.transform, new Vector2(left, -.005f), new Vector2(left + slot - gap, .105f));
            }
            var news = Panel("Frontier note", content);
            Place(news, new Vector2(.67f, -.005f), new Vector2(1, .26f));
            var tag = Label("Frontier tag", news, "EXPLORE THE FRONTIER", 13, AlphaTheme.Gold); Box(tag.rectTransform, 22, 20, -22, 45, true);
            var name = Label("Frontier title", news, "Realms & Frontiers", 28, AlphaTheme.Ink, true); Box(name.rectTransform, 22, 49, -22, 90, true);
            var body = Label("Frontier description", news, "Históricas. Fantasía. Navales.\nTres ámbitos PvP separados.", 17, AlphaTheme.Muted); Box(body.rectTransform, 22, 96, -22, 150, true);
        }

        private void Settings()
        {
            var title = Label("Settings heading", content, "Make yourself at home", 46, AlphaTheme.Ink, true); Place(title.rectTransform, new Vector2(0, .85f), new Vector2(1, 1));
            var subtitle = Label("Settings subtitle", content, "Sound, controls, voice and accessibility. Saved on this device.", 21, AlphaTheme.Muted); Place(subtitle.rectTransform, new Vector2(0, .75f), new Vector2(1, .85f));
            var settings = match.Alpha.Settings.Value;
            // Audio shares the top row in three narrower tiles so the score can be silenced on its own.
            Setting("SOUND", settings.SoundEnabled ? "On" : "Off", 0, .58f, match.Alpha.ToggleSound, .31f);
            Setting("VOLUME", Mathf.RoundToInt(settings.SoundVolume * 100) + "%", .345f, .58f, match.Alpha.CycleVolume, .31f);
            Setting("MUSIC", Mathf.RoundToInt(settings.MusicVolume * 100) + "%", .69f, .58f, match.Alpha.CycleMusic, .31f);
            Setting("CAMERA PAN", settings.PanSpeed.ToString("0.##") + "×", 0, .41f, match.Alpha.CyclePan);
            Setting("CAMERA ZOOM", settings.ZoomSpeed.ToString("0.##") + "×", .52f, .41f, match.Alpha.CycleZoom);
            Setting("DRAG TOLERANCE", settings.DragTolerance.ToString("0.##") + "×", 0, .24f, match.Alpha.CycleTolerance);
            Setting("TEXT CONTRAST", settings.HighContrastText ? "High" : "Standard", .52f, .24f, match.Alpha.ToggleContrast);
            // Voice shares its row with the frame rate, in the audio row's narrower tiles.
            Setting("VOICE CONTROL", VoiceControls.ModeName((VoiceListenMode)settings.VoiceMode), 0, .07f, match.Alpha.CycleVoiceMode, .31f);
            Setting("VOICE LANGUAGE", VoiceControls.LanguageName((VoiceLanguageChoice)settings.VoiceLanguage), .345f, .07f, match.Alpha.CycleVoiceLanguage, .31f);
            Setting("FRAME RATE", FramePacing.Name(settings.FrameRate), .69f, .07f, match.Alpha.CycleFrameRate, .31f);
            var reports = Action(content, "More settings", "GUIDE & LOCAL REPORTS", () => { Close(); match.Alpha.Open(); resumePanel = true; });
            Place((RectTransform)reports.transform, new Vector2(0, -.06f), new Vector2(.32f, .045f));
            var performance = Action(content, "Toggle performance overlay", "PERFORMANCE " + (settings.ShowPerformanceMetrics ? "ON" : "OFF"), () => { match.Alpha.TogglePerformanceMetrics(); Navigate("settings"); });
            Place((RectTransform)performance.transform, new Vector2(.35f, -.06f), new Vector2(.65f, .045f));
            var notice = Label("Settings save notice", content, match.Alpha.Notice, 14, AlphaTheme.Muted); Place(notice.rectTransform, new Vector2(.69f, -.075f), new Vector2(1, .07f));
        }
        private void Setting(string name, string value, float x, float y, Action callback, float width = .48f)
        {
            var button = Action(content, "Setting " + name, name + "\n" + value, () => { callback(); Navigate("settings"); });
            Place((RectTransform)button.transform, new Vector2(x, y), new Vector2(x + width, y + .15f));
        }
        private void PracticeGuide()
        {
            // A challenge world is frozen the moment it ends, so the guide has to be given a fresh one
            // rather than started inside a battlefield where nothing will ever move again.
            if (match.World.Match != null || match.IsChallenge || match.IsCombatSandbox || match.IsFactionDrill)
            { Close(); returnHome = false; startGuide = true; SceneManager.LoadScene("Greybox"); return; }
            Close(); match.OfflineControls.Close(); match.Alpha.StartTutorial();
        }
        private void Nav(string label, float x, Action action)
        {
            var button = Action(safe, "Navigation " + label, label.ToUpperInvariant(), action);
            Place((RectTransform)button.transform, new Vector2(x, .896f), new Vector2(x + .085f, .958f));
            button.GetComponent<Image>().color = new Color(.03f, .065f, .07f, .28f);
        }
        private static RectTransform Panel(string name, Transform parent)
        { var r = Rect(name, parent); AlphaTheme.StylePanel(r); return r; }
        private static Button Action(Transform parent, string name, string label, Action action, bool primary = false)
        {
            var r = Rect(name, parent); var button = r.gameObject.AddComponent<Button>(); r.gameObject.AddComponent<Image>();
            var text = Label("Text", r, label, 19, AlphaTheme.Ink); Stretch(text.rectTransform, 14, 6);
            text.alignment = TextAnchor.MiddleCenter; text.resizeTextForBestFit = true; text.resizeTextMinSize = 14; text.resizeTextMaxSize = 19;
            button.onClick.AddListener(() => action()); AlphaTheme.StyleButton(button, primary); return button;
        }
        private static Text Label(string name, Transform parent, string value, int size, Color color, bool display = false)
        {
            var r = Rect(name, parent); var text = r.gameObject.AddComponent<Text>(); text.text = value; text.font = display ? AlphaTheme.Display : AlphaTheme.Body;
            text.fontSize = size; text.color = color; text.alignment = TextAnchor.MiddleLeft; text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap; text.verticalOverflow = VerticalWrapMode.Truncate;
            return text;
        }
        private static RectTransform Rect(string name, Transform parent)
        { var r = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); r.SetParent(parent, false); return r; }
        private static void Stretch(RectTransform r, float x = 0, float y = 0)
        { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = new Vector2(x, y); r.offsetMax = new Vector2(-x, -y); }
        private static void Place(RectTransform r, Vector2 min, Vector2 max)
        { r.anchorMin = min; r.anchorMax = max; r.offsetMin = r.offsetMax = Vector2.zero; }
        private static void Box(RectTransform r, float left, float top, float right, float bottom, bool fromTop)
        { r.anchorMin = new Vector2(0, 1); r.anchorMax = Vector2.one; r.offsetMin = new Vector2(left, -bottom); r.offsetMax = new Vector2(right, -top); }
        public void Dispose() { disposed = true; if (root != null) UnityEngine.Object.Destroy(root); }
    }
}
