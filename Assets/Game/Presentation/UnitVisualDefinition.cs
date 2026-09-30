using UnityEngine;

namespace Emberfield.Presentation
{
    [CreateAssetMenu(menuName = "Emberfield/Art/Unit visual")]
    public sealed class UnitVisualDefinition : ScriptableObject
    {
        public string GameplayDefinitionId;
        public FactionVisualDefinition Faction;
        public GameObject Prefab;
        public bool ProductionApproved;
        [TextArea] public string ReviewNotes;
    }
}
