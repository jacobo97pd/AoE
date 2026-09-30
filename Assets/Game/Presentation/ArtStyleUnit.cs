using UnityEngine;

namespace Emberfield.Presentation
{
    [DisallowMultipleComponent, RequireComponent(typeof(Animator), typeof(LODGroup))]
    public sealed class ArtStyleUnit : MonoBehaviour
    {
        public UnitVisualDefinition Definition;
        public CosmeticSkinDefinition Skin;
        public bool Warrior;
        public Color Team = new Color(.19f, .37f, .62f);
        public bool Selected;
        public string CurrentPose { get; private set; } = "Idle";
        private Renderer[] renderers;
        private MaterialPropertyBlock block;
        private float hitUntil;

        private void OnEnable() { RefreshAppearance(); }
        public void SetTeam(Color color) { Team = color; RefreshAppearance(); }
        public void Select(bool value) { Selected = value; RefreshAppearance(); }
        public void FlashHit() { hitUntil = Time.unscaledTime + .16f; RefreshAppearance(); }
        private void Update()
        {
            if (hitUntil > 0 && Time.unscaledTime >= hitUntil) { hitUntil = 0; RefreshAppearance(); }
        }
        public void RefreshAppearance()
        {
            if (block == null) block = new MaterialPropertyBlock();
            if (renderers == null) renderers = GetComponentsInChildren<SkinnedMeshRenderer>(true);
            foreach (var renderer in renderers)
            {
                renderer.GetPropertyBlock(block);
                block.SetColor("_TeamColor", Team);
                block.SetColor("_FactionTint", Skin == null ? Color.white : Skin.SurfaceTint);
                block.SetFloat("_Selection", Selected ? 1 : 0);
                block.SetFloat("_DamageFlash", hitUntil > 0 ? .65f : 0);
                renderer.SetPropertyBlock(block);
            }
        }
        public void Pose(string state)
        {
            var animator = GetComponent<Animator>();
            if (animator == null || animator.runtimeAnimatorController == null) return;
            CurrentPose = state;
            if (Application.isPlaying) animator.CrossFadeInFixedTime(state, .13f);
        }
    }
}
