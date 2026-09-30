using Emberfield.Voice;
using UnityEngine;
using UnityEngine.UI;

namespace Emberfield.Presentation
{
    // Voice status above the command bar: a tap-to-talk chip showing what was last heard, and the spoken help.
    public sealed class VoicePanel
    {
        private readonly MatchController match;
        private readonly RectTransform root, help;
        private readonly Image light;
        private readonly Text status, detail, helpTitle, helpBody;
        public RectTransform Root => root;
        public bool IsVisible => root.gameObject.activeSelf;
        public bool HelpVisible => help.gameObject.activeSelf;

        public VoicePanel(MatchController match, RectTransform commands)
        {
            this.match = match;
            root = Rect("Voice control", commands);
            root.anchorMin = root.anchorMax = new Vector2(0, 1); root.pivot = Vector2.zero;
            root.anchoredPosition = new Vector2(18, 10); root.sizeDelta = new Vector2(380, 58);
            var image = root.gameObject.AddComponent<Image>();
            var button = root.gameObject.AddComponent<Button>(); button.targetGraphic = image;
            AlphaTheme.StyleButton(button);
            button.onClick.AddListener(match.Voice.PressHudButton);
            var lamp = Rect("Listening light", root);
            lamp.anchorMin = lamp.anchorMax = new Vector2(0, .5f); lamp.anchoredPosition = new Vector2(22, 0); lamp.sizeDelta = new Vector2(14, 14);
            light = lamp.gameObject.AddComponent<Image>(); light.raycastTarget = false;
            status = Label("Voice status", root, AlphaTheme.Strong, 16, new Vector2(0, .5f), Vector2.one, new Vector2(40, 0), new Vector2(-12, -5));
            detail = Label("Voice heard", root, AlphaTheme.Body, 15, Vector2.zero, new Vector2(1, .5f), new Vector2(40, 5), new Vector2(-12, 0));

            help = Rect("Voice help", commands);
            help.anchorMin = help.anchorMax = new Vector2(0, 1); help.pivot = Vector2.zero;
            help.anchoredPosition = new Vector2(18, 78); help.sizeDelta = new Vector2(640, 250);
            AlphaTheme.StylePanel(help); help.GetComponent<Image>().raycastTarget = false;
            helpTitle = Label("Voice help title", help, AlphaTheme.Strong, 17, new Vector2(0, 1), Vector2.one, new Vector2(16, -38), new Vector2(-16, -10));
            helpBody = Label("Voice help examples", help, AlphaTheme.Body, 17, Vector2.zero, Vector2.one, new Vector2(16, 12), new Vector2(-16, -42));
            helpBody.alignment = TextAnchor.UpperLeft; helpBody.lineSpacing = 1.15f;
            help.gameObject.SetActive(false);
        }

        public void Refresh()
        {
            var voice = match.Voice;
            bool show = VoiceControls.PlatformSupported || voice.Mode != VoiceListenMode.Off;
            root.gameObject.SetActive(show);
            help.gameObject.SetActive(show && voice.HelpVisible);
            if (!show) return;
            status.text = voice.StatusText; detail.text = voice.DetailText;
            // High-contrast text owns label colours while it is on.
            if (match.Alpha?.Settings.Value.HighContrastText != true)
            {
                status.color = AlphaTheme.Gold; helpTitle.color = AlphaTheme.Gold; helpBody.color = AlphaTheme.Ink;
                detail.color = voice.ShowsRecentPhrase ? voice.LastIntent.IsNone ? AlphaTheme.Danger : AlphaTheme.Positive : AlphaTheme.Muted;
            }
            light.color = voice.IsListening ? Color.Lerp(AlphaTheme.Positive, Color.white, .5f + .5f * Mathf.Sin(Time.unscaledTime * 7))
                : voice.Mode == VoiceListenMode.Off ? AlphaTheme.Border : voice.IsAvailable ? AlphaTheme.Gold : AlphaTheme.Danger;
            if (!help.gameObject.activeSelf) return;
            helpTitle.text = voice.Language == VoiceLanguage.Spanish ? "PUEDES DECIR" : "YOU CAN SAY";
            helpBody.text = string.Join("\n", voice.Parser.Vocabulary.Help);
        }

        private static Text Label(string name, Transform parent, Font font, int size, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            var rect = Rect(name, parent);
            rect.anchorMin = anchorMin; rect.anchorMax = anchorMax; rect.offsetMin = offsetMin; rect.offsetMax = offsetMax;
            var label = rect.gameObject.AddComponent<Text>(); label.font = font; label.fontSize = size; label.color = AlphaTheme.Ink;
            label.alignment = TextAnchor.MiddleLeft; label.raycastTarget = false;
            label.resizeTextForBestFit = true; label.resizeTextMinSize = 12; label.resizeTextMaxSize = size;
            return label;
        }

        private static RectTransform Rect(string name, Transform parent)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); rect.SetParent(parent, false); return rect;
        }
    }
}
