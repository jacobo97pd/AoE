using UnityEngine;
using UnityEngine.UI;

namespace Emberfield.Presentation
{
    internal static class AlphaPanelDecor
    {
        public static void Apply(RectTransform root)
        {
            AlphaTheme.StylePanel(root);
            root.GetComponent<Image>().color = AlphaTheme.Background;
            var band = new GameObject("Panel header atmosphere", typeof(RectTransform)).GetComponent<RectTransform>();
            band.SetParent(root, false); band.anchorMin = new Vector2(0, 1); band.anchorMax = Vector2.one;
            band.offsetMin = new Vector2(0, -172); band.offsetMax = Vector2.zero;
            var gradient = band.gameObject.AddComponent<AlphaGradient>(); gradient.Vertical = true;
            gradient.Left = new Color(AlphaTheme.SurfaceRaised.r, AlphaTheme.SurfaceRaised.g, AlphaTheme.SurfaceRaised.b, 0);
            gradient.Right = AlphaTheme.SurfaceRaised; gradient.raycastTarget = false;
            var rule = new GameObject("Panel gold rule", typeof(RectTransform)).GetComponent<RectTransform>();
            rule.SetParent(root, false); rule.anchorMin = new Vector2(0, 1); rule.anchorMax = Vector2.one;
            rule.offsetMin = new Vector2(24, -3); rule.offsetMax = new Vector2(-24, -1);
            var line = rule.gameObject.AddComponent<Image>(); line.color = AlphaTheme.Gold; line.raycastTarget = false;
        }
    }
}
