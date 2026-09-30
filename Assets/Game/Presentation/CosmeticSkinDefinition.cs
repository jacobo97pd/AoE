using UnityEngine;

namespace Emberfield.Presentation
{
    public enum CosmeticVisualRarity { Standard, Rare, Epic, Legendary }

    [CreateAssetMenu(menuName = "Emberfield/Art/Cosmetic visual skin")]
    public sealed class CosmeticSkinDefinition : ScriptableObject
    {
        public string SkinId;
        public UnitVisualDefinition UnitVisual;
        public CosmeticVisualRarity Rarity;
        public Color SurfaceTint = Color.white;
        // Optional future authored variant; no health, damage, speed, economy or price fields.
        public GameObject VisualOverride;
    }
}
