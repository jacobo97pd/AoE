using UnityEngine;

namespace Emberfield.Presentation
{
    // Visual identity only; never supplies simulation rules or purchased power.
    [CreateAssetMenu(menuName = "Emberfield/Art/Faction visual identity")]
    public sealed class FactionVisualDefinition : ScriptableObject
    {
        public string FactionId;
        public Color Cloth = new Color(.055f, .20f, .53f);
        public Color Trim = new Color(.78f, .55f, .22f);
        public Color NeutralCloth = new Color(.86f, .80f, .64f);
        [TextArea] public string BodyLanguage, Silhouettes, ArmorStyle, ClothStyle;
        [TextArea] public string TrimMaterial, FactionSymbol, Architecture, Weapons, VisualEffects, EnvironmentProps, BannerStyle;
    }
}
