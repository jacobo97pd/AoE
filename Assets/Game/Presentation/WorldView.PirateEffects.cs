using UnityEngine;
using UnityEngine.Rendering;

namespace Emberfield.Presentation
{
    public sealed partial class WorldView
    {
        private sealed class Gunshot
        {
            public Transform Root, Flash, Smoke;
            public Renderer SmokeRenderer;
            public CorsairAnimationDriver Source;
            public Vector3 Origin, Forward;
            public double Started;
        }
        private readonly Gunshot[] gunshots = new Gunshot[16];
        private readonly MaterialPropertyBlock gunSmokeTint = new MaterialPropertyBlock();
        private Material flashMaterial, smokeMaterial;
        private int nextGunshot;

        // Triggered only from an observed firing cooldown. These effects never fire
        // animation events or gameplay commands, and never query hidden shooters.
        private void EmitGunshot(Visual source, float alpha)
        {
            int index = nextGunshot++ % gunshots.Length;
            var effect = gunshots[index];
            if (effect == null)
            {
                if (flashMaterial == null)
                {
                    flashMaterial = Material(new Color(1, .69f, .20f));
                    flashMaterial.SetColor("_EmissionColor", new Color(3, 1.3f, .20f)); flashMaterial.EnableKeyword("_EMISSION");
                    smokeMaterial = Material(new Color(.62f, .60f, .56f, .35f));
                    smokeMaterial.SetFloat("_Surface", 1); smokeMaterial.SetFloat("_ZWrite", 0);
                    smokeMaterial.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                    smokeMaterial.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                    smokeMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                    smokeMaterial.SetOverrideTag("RenderType", "Transparent"); smokeMaterial.renderQueue = (int)RenderQueue.Transparent;
                }
                effect = new Gunshot { Root = new GameObject("Visible pistol discharge " + index).transform };
                effect.Root.SetParent(root, false);
                effect.Flash = Shape("Muzzle flash", PrimitiveType.Sphere, effect.Root, Vector3.zero, Vector3.one, flashMaterial).transform;
                effect.Smoke = Shape("Powder smoke", PrimitiveType.Sphere, effect.Root, Vector3.zero, Vector3.one, smokeMaterial).transform;
                effect.SmokeRenderer = effect.Smoke.GetComponent<Renderer>();
                gunshots[index] = effect;
            }
            effect.Source = source.ImportedCharacter;
            effect.Origin = source.ImportedCharacter != null ? source.ImportedCharacter.MuzzleWorldPosition : source.Root.TransformPoint(new Vector3(.30f, 1.45f, .70f) * AlphaWorldArt.UnitScale);
            effect.Forward = source.Root.forward;
            effect.Started = (world.TickIndex + Mathf.Clamp01(alpha)) * (double)Emberfield.Simulation.World.TickSeconds;
            effect.Root.gameObject.SetActive(true);
        }

        private void SyncGunshots(float alpha)
        {
            double now = (world.TickIndex + Mathf.Clamp01(alpha)) * (double)Emberfield.Simulation.World.TickSeconds;
            foreach (var effect in gunshots)
            {
                if (effect == null || !effect.Root.gameObject.activeSelf) continue;
                float age = Mathf.Max(0, (float)(now - effect.Started));
                // The flash rides the barrel through the recoil at any display rate; the
                // smoke then drifts from wherever the muzzle last was.
                if (age < .12f && effect.Source != null && effect.Source.isActiveAndEnabled && !effect.Source.IsDying)
                    effect.Origin = effect.Source.MuzzleWorldPosition;
                if (age > .65f || world.Vision != null && !world.Vision.IsVisible(MatchController.LocalPlayer, DefinitionLoader.ToSimulation(effect.Origin)))
                { effect.Root.gameObject.SetActive(false); continue; }
                effect.Root.position = effect.Origin; effect.Root.rotation = Quaternion.LookRotation(effect.Forward);
                effect.Flash.gameObject.SetActive(age < .12f);
                effect.Flash.localScale = new Vector3(.13f, .12f, .48f) * (Mathf.Clamp01(1 - age / .14f) * AlphaWorldArt.UnitScale);
                effect.Flash.localPosition = Vector3.forward * (.13f * AlphaWorldArt.UnitScale);
                effect.Smoke.localPosition = (Vector3.forward * age * .6f + Vector3.up * age * .5f) * AlphaWorldArt.UnitScale;
                effect.Smoke.localScale = Vector3.one * ((.10f + age * .55f) * AlphaWorldArt.UnitScale);
                var tint = new Color(.62f, .60f, .56f, .35f * Mathf.Clamp01(1 - age / .65f));
                gunSmokeTint.SetColor("_BaseColor", tint); gunSmokeTint.SetColor("_Color", tint);
                effect.SmokeRenderer.SetPropertyBlock(gunSmokeTint);
            }
        }
    }
}
