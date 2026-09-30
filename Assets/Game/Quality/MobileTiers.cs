using System;

namespace Emberfield.Quality
{
    public enum MobileTier { Low, Mid, High }

    /// <summary>What a device says about itself, copied from Unity's SystemInfo on the device.</summary>
    public readonly struct DeviceProfile
    {
        public readonly int MemoryMegabytes, ProcessorCount, GraphicsMemoryMegabytes, MaximumTextureSize;
        /// <summary>The graphics API as UnityEngine.Rendering.GraphicsDeviceType names it: Vulkan, Metal, OpenGLES3.</summary>
        public readonly string GraphicsApi;

        public DeviceProfile(int memoryMegabytes, int processorCount, int graphicsMemoryMegabytes, int maximumTextureSize, string graphicsApi)
        {
            MemoryMegabytes = memoryMegabytes; ProcessorCount = processorCount; GraphicsMemoryMegabytes = graphicsMemoryMegabytes;
            MaximumTextureSize = maximumTextureSize; GraphicsApi = graphicsApi ?? "";
        }
    }

    /// <summary>
    /// What one phone tier asks of the renderer and the engine. Plain numbers, so the table can be checked without a
    /// device; Emberfield.Presentation.MobileQuality turns them into a pipeline asset and quality settings.
    /// </summary>
    public sealed class MobileTierSettings
    {
        public MobileTier Tier { get; private set; }
        /// <summary>Upper bound of the 3D render scale; the HUD is drawn at the screen's own resolution.</summary>
        public float RenderScale { get; private set; }
        /// <summary>The 3D image stops growing at this many rows on the screen's short side, whatever the panel.</summary>
        public int RenderHeight { get; private set; }
        /// <summary>How far dynamic resolution may lower the render scale when frames run over budget.</summary>
        public float MinimumRenderScale { get; private set; }
        public int MsaaSamples { get; private set; }
        public bool Hdr { get; private set; }
        public int ShadowResolution { get; private set; }
        public float ShadowDistance { get; private set; }
        public int ShadowCascades { get; private set; }
        public bool SoftShadows { get; private set; }
        public int AdditionalLights { get; private set; }
        public float LodBias { get; private set; }
        public int MaximumLodLevel { get; private set; }
        public float MeshLodThreshold { get; private set; }
        /// <summary>Bones per vertex: 1, 2 or 4.</summary>
        public int SkinWeights { get; private set; }
        public int TargetFrameRate { get; private set; }
        /// <summary>
        /// The share of the screen's height below which scenery (the trees, palms and rocks on blocked cells) draws its
        /// lighter level; gatherable nodes switch at the zoom a 3 m tree does (<see cref="MobileTiers.NodeDetailZoom"/>).
        /// The battlefield camera is orthographic, so the share depends on the zoom alone: a 3 m tree fills
        /// 3 / (2 x orthographic size) of the screen, 0.07 at the widest zoom and 0.3 at the closest. The fillers are
        /// measured at their model's size, not at each copy's scale, so all copies of a model switch at one zoom. See
        /// <see cref="MobileTiers.SceneryDetailZoom"/>.
        /// </summary>
        public float SceneryDetailHeight { get; private set; }
        /// <summary>Whether the trees, palms and rocks on blocked cells cast shadows.</summary>
        public bool SceneryShadows { get; private set; }
        /// <summary>
        /// The share of the screen's height a unit must fill to draw its whole mesh; each halving below it draws the
        /// next lighter Mesh LOD level. See <see cref="MobileTiers.UnitMeshLevel"/>.
        /// </summary>
        public float UnitDetailHeight { get; private set; }
        /// <summary>
        /// The share of the embers, glimmers and smoke over a map's lands (LandMotes) that the tier draws: none of the
        /// embers or glimmers on low, which keeps half the smoke, half of everything on mid and all of it on high.
        /// </summary>
        public float AmbientParticleShare { get; private set; }

        /// <summary>
        /// How deep the widest battlefield view reaches, in metres along the camera: RtsCamera stands 55 m back at a
        /// 55 degree pitch and zooms out to an orthographic half-height of 22 m, so the top edge meets the ground
        /// 55 + 22 / tan 55 = 70.4 m away. A shadow distance short of this leaves the far side of the screen unshadowed.
        /// </summary>
        public const float DeepestView = 70.4f;

        private MobileTierSettings() { }

        // One cascade on every tier. The battlefield camera is orthographic and 55 m back, so every visible pixel lies
        // 39-71 m deep: the near cascade of the shipped 2-cascade split (0-20 m) never holds a visible pixel, yet its
        // casters are drawn and its tile halves the atlas. A single cascade spends the whole map on what is seen.
        // Low renders without HDR: bloom only starts above 1.2, and on the measured battlefield the picture with and
        // without it differs in 0.1% of pixels. The other tiers keep it for the brighter fantasy effects.
        // Scenery: low and mid keep the detailed trees to a 12.5 m zoom (a 3 m broadleaf; a 3.3 m pine to 13.7 m),
        // well out past the play zoom (8 m), and draw the lighter ones beyond it; high keeps them to 19-21 m, short of
        // the widest zoom (22 m). At 0.23 low drew every tree as its far shell from a 7 m zoom out, the play zoom
        // included, where the closed shells read as rounded blobs; what that saved there was 240 triangles a tree. Low
        // still casts no scenery shadows (1,700 of the widest forest view's casters on a desktop). A desktop keeps the
        // detailed level at every zoom. Units (1.3-1.9 m) fill 0.08-0.12 of the screen at the play zoom and 0.03-0.04
        // at the widest: low draws at most half a unit's mesh (about 100 pixels tall zoomed right in), a third of it at
        // the play zoom and a sixth at the widest; mid halves the foot soldiers from the play zoom out; high halves them
        // past about 12 m. The grass and reed tufts stay: on the widest forest view they cost 3 of 72 draw calls and 0.5%
        // of the triangles.
        private static readonly MobileTierSettings[] table =
        {
            new MobileTierSettings
            {
                Tier = MobileTier.Low, RenderScale = .8f, RenderHeight = 720, MinimumRenderScale = .6f, MsaaSamples = 1, Hdr = false,
                ShadowResolution = 1024, ShadowDistance = 75, ShadowCascades = 1, SoftShadows = false, AdditionalLights = 1,
                LodBias = .7f, MaximumLodLevel = 0, MeshLodThreshold = 2, SkinWeights = 2, TargetFrameRate = 30,
                SceneryDetailHeight = .12f, SceneryShadows = false, UnitDetailHeight = .2f, AmbientParticleShare = 0
            },
            new MobileTierSettings
            {
                Tier = MobileTier.Mid, RenderScale = .9f, RenderHeight = 900, MinimumRenderScale = .6f, MsaaSamples = 2, Hdr = true,
                ShadowResolution = 2048, ShadowDistance = 80, ShadowCascades = 1, SoftShadows = true, AdditionalLights = 2,
                LodBias = 1, MaximumLodLevel = 0, MeshLodThreshold = 1.5f, SkinWeights = 2, TargetFrameRate = 60,
                SceneryDetailHeight = .12f, SceneryShadows = true, UnitDetailHeight = .1f, AmbientParticleShare = .5f
            },
            new MobileTierSettings
            {
                Tier = MobileTier.High, RenderScale = 1, RenderHeight = 1080, MinimumRenderScale = .6f, MsaaSamples = 2, Hdr = true,
                ShadowResolution = 2048, ShadowDistance = 80, ShadowCascades = 1, SoftShadows = true, AdditionalLights = 4,
                LodBias = 1.5f, MaximumLodLevel = 0, MeshLodThreshold = 1, SkinWeights = 4, TargetFrameRate = 60,
                SceneryDetailHeight = .08f, SceneryShadows = true, UnitDetailHeight = .06f, AmbientParticleShare = 1
            }
        };

        public static MobileTierSettings For(MobileTier tier)
        {
            if ((int)tier < 0 || (int)tier >= table.Length) throw new ArgumentOutOfRangeException(nameof(tier));
            return table[(int)tier];
        }
    }

    public static class MobileTiers
    {
        /// <summary>
        /// A desktop's scenery share, which the prop baker gives the fillers' prefab groups: under every zoom the game
        /// allows, the smallest scenery (a rock about 0.8 m across at the widest zoom) still fills 0.019 of the screen, so
        /// a desktop never draws the lighter level. At runtime a desktop keeps each filler's one detailed mesh.
        /// </summary>
        public const float DesktopSceneryDetailHeight = .012f;
        /// <summary>A prop prefab's group culls scenery below this share; no zoom the game allows comes near it.</summary>
        public const float SceneryCullHeight = .004f;

        /// <summary>
        /// The LODGroup threshold at which a model leaves its detailed level where a LOD group decides it (the prefab
        /// bakers). Unity multiplies a group's share of the screen by the quality level's LOD bias before comparing it,
        /// orthographic camera included, so the threshold carries the bias in force and the switch lands at
        /// <paramref name="detailHeight"/> itself. Never at or under the cull.
        /// </summary>
        public static float SceneryLodThreshold(float detailHeight, float lodBias) => Math.Max(SceneryCullHeight * 2, detailHeight * lodBias);

        /// <summary>The trees a tier's scenery share is set for: the forest's broadleaf stands 3 m, its pine 3.3 m.</summary>
        public const float SceneryTreeHeight = 3;

        /// <summary>
        /// The orthographic size (half the view's height in metres) beyond which a model <paramref name="size"/> metres
        /// across draws its lighter level on a tier whose scenery share is <paramref name="detailHeight"/>: the zoom at
        /// which the model fills that share of the screen. A 3 m tree at 0.12 switches at 12.5 m.
        /// </summary>
        public static float SceneryDetailZoom(float detailHeight, float size) => detailHeight > 0 ? Math.Max(0, size) / (2 * detailHeight) : float.PositiveInfinity;

        /// <summary>
        /// The zoom beyond which a gatherable node draws its lighter level: where the tier's 3 m trees switch, whatever the
        /// node's own size. Measured at its own size, a 1 m berry bush would draw its far level, which loses the berries,
        /// from a zoom a third as wide as the trees' (4.2 m at 0.12, closer than the game ever zooms).
        /// </summary>
        public static float NodeDetailZoom(float detailHeight) => SceneryDetailZoom(detailHeight, SceneryTreeHeight);

        /// <summary>
        /// How far back inside its switch the zoom must come before a model that has gone light draws its detailed level
        /// again: a tenth of the zoom, about one notch of the mouse wheel. A pinch resting on a switch never flickers.
        /// </summary>
        public const float SceneryHysteresis = 1.1f;

        /// <summary>
        /// Whether a model whose switch is at <paramref name="switchZoom"/> draws its lighter level at
        /// <paramref name="zoom"/>, given whether it did (<paramref name="lighter"/>): it goes light past the switch and
        /// comes back only once the zoom is back inside it by <see cref="SceneryHysteresis"/>.
        /// </summary>
        public static bool DrawsLighterLevel(bool lighter, float zoom, float switchZoom) => lighter ? zoom * SceneryHysteresis > switchZoom : zoom > switchZoom;

        /// <summary>
        /// The Mesh LOD level a unit filling <paramref name="share"/> of the screen's height draws: the whole mesh from
        /// <paramref name="detailHeight"/> up, one lighter level for each halving below it, never past the mesh's last
        /// level. Unity's generated levels each keep about half the triangles, so the triangles per pixel stay about even.
        /// </summary>
        public static int UnitMeshLevel(float share, float detailHeight, int levels)
        {
            int level = 0;
            if (detailHeight <= 0) return 0;
            for (float step = detailHeight; share < step && level < levels - 1; step *= .5f) level++;
            return level;
        }

        /// <summary>
        /// Picks a tier from what the device reports. Unity reports a phone's memory less what the system keeps, so a
        /// 4 GB phone shows about 3,700 MB and a 6 GB one about 5,500 MB; the thresholds sit just under those.
        /// </summary>
        public static MobileTier Choose(DeviceProfile device)
        {
            // A phone runs OpenGL ES only when its Vulkan driver is missing or blocked: an old or weak GPU.
            bool legacyApi = device.GraphicsApi.StartsWith("OpenGLES", StringComparison.Ordinal);
            if (legacyApi || device.MemoryMegabytes < 3300 || device.ProcessorCount < 4 || device.MaximumTextureSize < 4096)
                return MobileTier.Low;
            // Unified memory makes the reported graphics memory a guess on most phones, so it can only hold a
            // device back from the top tier, never push it down to the bottom one.
            bool graphicsMemory = device.GraphicsMemoryMegabytes <= 0 || device.GraphicsMemoryMegabytes >= 1024;
            if (device.MemoryMegabytes >= 5300 && device.ProcessorCount >= 6 && device.MaximumTextureSize >= 8192 && graphicsMemory)
                return MobileTier.High;
            return MobileTier.Mid;
        }

        public static bool TryParse(string text, out MobileTier tier)
        {
            tier = MobileTier.Low;
            switch ((text ?? "").Trim().ToLowerInvariant())
            {
                case "low": tier = MobileTier.Low; return true;
                case "mid": tier = MobileTier.Mid; return true;
                case "high": tier = MobileTier.High; return true;
                default: return false;
            }
        }

        /// <summary>
        /// The render scale a tier starts at on a given screen: its own scale, lowered further so the 3D image is no
        /// taller than the tier's render height. Never below one half, so a 1440p panel still gets a usable picture.
        /// </summary>
        public static float StartingRenderScale(MobileTierSettings tier, int screenWidth, int screenHeight)
        {
            if (tier == null) throw new ArgumentNullException(nameof(tier));
            float scale = tier.RenderScale;
            int shortSide = Math.Min(screenWidth, screenHeight);
            if (shortSide > 0) scale = Math.Min(scale, tier.RenderHeight / (float)shortSide);
            return Math.Max(.5f, Math.Min(1, scale));
        }
    }
}
