using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Emberfield.Presentation
{
    // Shared native UI palette. Font licenses are shipped beside the font files.
    public static class AlphaTheme
    {
        public static readonly Color Background = new Color32(12, 23, 27, 255);
        public static readonly Color Surface = new Color32(20, 35, 39, 247);
        public static readonly Color SurfaceRaised = new Color32(30, 49, 52, 255);
        public static readonly Color Border = new Color32(78, 90, 83, 255);
        public static readonly Color Gold = new Color32(216, 179, 103, 255);
        public static readonly Color Ink = new Color32(242, 236, 220, 255);
        public static readonly Color Muted = new Color32(163, 182, 178, 255);
        public static readonly Color Teal = new Color32(87, 161, 157, 255);
        public static readonly Color Positive = new Color32(135, 201, 143, 255);
        public static readonly Color Danger = new Color32(226, 122, 103, 255);
        private static Font body, strong, display;
        public static Font Body => body != null ? body : body = Resources.Load<Font>("Fonts/Lato-Regular") ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        public static Font Strong => strong != null ? strong : strong = Resources.Load<Font>("Fonts/Lato-Bold") ?? Body;
        public static Font Display => display != null ? display : display = Resources.Load<Font>("Fonts/Marcellus-Regular") ?? Body;

        public static void StylePanel(RectTransform panel, bool raised = false)
        {
            var image = panel.GetComponent<Image>() ?? panel.gameObject.AddComponent<Image>();
            image.color = raised ? SurfaceRaised : Surface;
            var outline = panel.GetComponent<Outline>() ?? panel.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(Border.r, Border.g, Border.b, .55f);
            outline.effectDistance = new Vector2(1, -1); outline.useGraphicAlpha = false;
        }

        public static void StyleButton(Button button, bool primary = false)
        {
            var image = button.GetComponent<Image>() ?? button.gameObject.AddComponent<Image>();
            image.color = primary ? Gold : SurfaceRaised; button.targetGraphic = image;
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.13f, 1.13f, 1.1f);
            colors.selectedColor = new Color(1.1f, 1.1f, 1.05f);
            colors.pressedColor = new Color(.75f, .82f, .8f);
            colors.disabledColor = new Color(.55f, .6f, .6f, .5f);
            colors.fadeDuration = .12f; button.colors = colors;
            var text = button.GetComponentInChildren<Text>();
            if (text != null) { text.font = Strong; text.color = primary ? Background : Ink; }
            foreach (var icon in button.GetComponentsInChildren<AlphaIcon>()) icon.color = primary ? Background : Gold;
            var outline = button.GetComponent<Outline>() ?? button.gameObject.AddComponent<Outline>();
            outline.effectColor = primary ? new Color32(248, 222, 159, 255) : new Color32(73, 93, 88, 255);
            outline.effectDistance = new Vector2(1, -1);
        }
    }

    // Decorative gradient built as UI geometry, avoiding a full-screen bitmap allocation.
    public sealed class AlphaGradient : MaskableGraphic
    {
        public Color Left = new Color(0, 0, 0, .88f), Right = Color.clear;
        public bool Vertical;
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); var r = rectTransform.rect;
            Add(vh, new Vector2(r.xMin, r.yMin), Left);
            Add(vh, new Vector2(r.xMin, r.yMax), Vertical ? Right : Left);
            Add(vh, new Vector2(r.xMax, r.yMax), Right);
            Add(vh, new Vector2(r.xMax, r.yMin), Vertical ? Left : Right);
            vh.AddTriangle(0, 1, 2); vh.AddTriangle(2, 3, 0);
        }
        private static void Add(VertexHelper vh, Vector2 p, Color c)
        { var v = UIVertex.simpleVert; v.position = p; v.color = c; vh.AddVert(v); }
    }
}
