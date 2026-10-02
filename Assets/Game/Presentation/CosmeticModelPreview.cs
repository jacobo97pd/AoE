using System.Collections.Generic;
using Emberfield.Simulation;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace Emberfield.Presentation
{
    // A camera renders the same shared army mesh used on the battlefield. It has no simulation entity.
    // A character skin is shown as its own Meshy model, standing in its idle.
    public sealed class CosmeticModelPreview : MonoBehaviour
    {
        private GameObject stage;
        private Transform model;
        private Camera previewCamera;
        private RenderTexture texture;
        private Vector3 center;
        private RawImage picture;
        private MeshFilter[] meshFilters;
        // A skinned character turns about its middle and moves through its idle, so it is fitted once by the view that
        // holds it in every pose at every turn: the camera-space extents (left, right, bottom, top) seen from where
        // the camera first stood, and that spot.
        private Vector4 characterView;
        private Vector3 cameraOrigin;
        private bool hasCharacterView;
        private float nextRender;
        private static readonly int StudioLight = Shader.PropertyToID("_EmberfieldStudio");
        public bool HasRendered { get; private set; }
        /// <summary>The animated character in the preview, for a character skin; null for every other item.</summary>
        public CorsairAnimationDriver Character { get; private set; }
        public void Initialize(RawImage image, CosmeticCatalogItem item)
        {
            picture = image;
            stage = new GameObject("Wardrobe preview stage"); stage.transform.position = new Vector3(10000,0,10000);
            if (item.slot == CosmeticLoadout.CharacterSlot) model = CreateCharacter(item);
            else
            {
                string id = item.targetId == "*" ? "keep" : item.targetId;
                FactionKind faction = item.realmId == "naval" ? FactionKind.PirateBrotherhood : item.realmId == "fantasy" ? FactionKind.DrakeforgedClans : FactionKind.AvenCompact;
                if (id == "dune_elephant") faction = FactionKind.MirajSultanate;
                if (id == "war_troll") faction = FactionKind.AshenDominion;
                if (id == "grove_guardian") faction = FactionKind.VerdantCovenant;
                if (id == "sun_lion" || item.styleId == "luminous_ward") faction = FactionKind.SolarKingdom;
                model = CosmeticLoadout.IsBuilding(id)
                    ? AlphaWorldArt.Building(id,faction,stage.transform,1,4,4)
                    : AlphaWorldArt.Unit(id,faction,stage.transform,1);
            }
            if (model == null) return;
            foreach (var child in stage.GetComponentsInChildren<Transform>()) child.gameObject.layer = 30;
            foreach (var lod in stage.GetComponentsInChildren<LODGroup>()) lod.ForceLOD(0);
            if (Character == null) AlphaWorldArt.ApplyCosmetic(model, CosmeticLoadout.Style(item.styleId));
            // This is a studio display, isolated from the active map's shadows and atmosphere.
            var lightBlock = new MaterialPropertyBlock();
            foreach (var renderer in model.GetComponentsInChildren<MeshRenderer>(true))
            {
                lightBlock.Clear(); renderer.GetPropertyBlock(lightBlock); lightBlock.SetFloat("_PreviewLighting", 1); renderer.SetPropertyBlock(lightBlock);
                renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            }
            foreach (var skin in model.GetComponentsInChildren<SkinnedMeshRenderer>(true)) { skin.shadowCastingMode = ShadowCastingMode.Off; skin.receiveShadows = false; }
            meshFilters = model.GetComponentsInChildren<MeshFilter>(true);
            var bounds = new Bounds(model.position,Vector3.zero);
            List<Vector3[]> poses = null;
            if (Character != null)
            {
                poses = IdlePoses();
                bool first = true;
                foreach (var pose in poses) foreach (var vertex in pose) { if (first) { bounds = new Bounds(vertex, Vector3.zero); first = false; } else bounds.Encapsulate(vertex); }
            }
            else foreach (var renderer in model.GetComponentsInChildren<Renderer>()) bounds.Encapsulate(renderer.bounds);
            center = bounds.center;
            var cameraObject = new GameObject("Wardrobe camera"); cameraObject.transform.SetParent(stage.transform,false);
            previewCamera = cameraObject.AddComponent<Camera>(); previewCamera.cullingMask = 1 << 30;
            previewCamera.clearFlags = CameraClearFlags.SolidColor; previewCamera.backgroundColor = new Color(.14f,.21f,.24f);
            previewCamera.orthographic = true;
            previewCamera.transform.position = center + new Vector3(7,5,-9); previewCamera.transform.LookAt(center);
            previewCamera.nearClipPlane = .1f; previewCamera.farClipPlane = 40; previewCamera.allowHDR = false;
            var data = previewCamera.GetUniversalAdditionalCameraData(); data.renderPostProcessing = false; data.renderShadows = false;
            previewCamera.enabled = false;
            if (poses != null) { cameraOrigin = previewCamera.transform.position; characterView = ViewOfEveryTurn(poses); hasCharacterView = true; }
        }
        /// <summary>
        /// The skin's own model in the idle the match plays, with owner 1's colour on its cloth. The animator runs on
        /// unscaled time so the idle plays while a paused match sits behind the store.
        /// </summary>
        private Transform CreateCharacter(CosmeticCatalogItem item)
        {
            var driver = MeshyUnitVisuals.Create(MeshyUnitVisuals.ResolveSkin(item.modelId, item.targetId, item.factionId), 1, stage.transform);
            if (driver == null) return null;
            Character = driver;
            driver.Animator.updateMode = AnimatorUpdateMode.UnscaledTime;
            driver.Animator.Update(0);
            return driver.transform;
        }

        /// <summary>
        /// Every vertex of the figure in world space at eight moments of its idle loop (a bow arm or a cloak moves out of
        /// the first frame). The animator takes the pose back on its next update.
        /// </summary>
        private List<Vector3[]> IdlePoses()
        {
            var poses = new List<Vector3[]>();
            var baked = new Mesh();
            AnimationClip idle = null;
            foreach (var clip in Character.Animator.runtimeAnimatorController.animationClips) if (clip != null && clip.name == "Idle") idle = clip;
            try
            {
                for (int step = 0; step < (idle != null ? 8 : 1); step++)
                {
                    if (idle != null) idle.SampleAnimation(model.gameObject, idle.length * step / 8f);
                    var vertices = new List<Vector3>();
                    foreach (var skin in model.GetComponentsInChildren<SkinnedMeshRenderer>())
                    {
                        // Baked without scale, the vertices are already in world units (the prefab is scaled to the unit's
                        // height), relative to the renderer's position and turn.
                        skin.BakeMesh(baked, false);
                        foreach (var vertex in baked.vertices) vertices.Add(skin.transform.position + skin.transform.rotation * vertex);
                    }
                    poses.Add(vertices.ToArray());
                }
                if (idle != null) idle.SampleAnimation(model.gameObject, 0);
            }
            finally { Destroy(baked); }
            return poses;
        }

        /// <summary>
        /// The camera-space extents (left, right, bottom, top) of the figure over all its poses with the stage turned
        /// through twelve angles about the vertical axis through <see cref="center"/>, which is how the preview turns it.
        /// </summary>
        private Vector4 ViewOfEveryTurn(List<Vector3[]> poses)
        {
            float minX = float.PositiveInfinity, minY = float.PositiveInfinity, maxX = float.NegativeInfinity, maxY = float.NegativeInfinity;
            var view = previewCamera.worldToCameraMatrix;
            for (int turn = 0; turn < 12; turn++)
            {
                var about = Matrix4x4.Translate(center) * Matrix4x4.Rotate(Quaternion.Euler(0, turn * 30, 0)) * Matrix4x4.Translate(-center);
                var matrix = view * about;
                foreach (var pose in poses)
                    foreach (var vertex in pose)
                    {
                        var projected = matrix.MultiplyPoint3x4(vertex);
                        minX = Mathf.Min(minX, projected.x); maxX = Mathf.Max(maxX, projected.x);
                        minY = Mathf.Min(minY, projected.y); maxY = Mathf.Max(maxY, projected.y);
                    }
            }
            return minX > maxX ? new Vector4(-1, 1, -1, 1) : new Vector4(minX, maxX, minY, maxY);
        }

        private void Update()
        {
            if (model != null) model.RotateAround(center,Vector3.up,Time.unscaledDeltaTime * 16);
            if (Time.unscaledTime >= nextRender) { nextRender = Time.unscaledTime + 1f/30; RenderPreview(); }
        }
        public void RenderPreview()
        {
            if (previewCamera == null || picture == null || !isActiveAndEnabled) return;
            FitDisplay();
            if (texture == null) return;
            var request = new RenderPipeline.StandardRequest { destination = texture };
            if (!RenderPipeline.SupportsRenderRequest(previewCamera,request)) return;
            // The Meshy unit shader lights a character with the studio's own key and fill while this is set: only for this render.
            Shader.SetGlobalFloat(StudioLight, 1);
            try { RenderPipeline.SubmitRenderRequest(previewCamera,request); HasRendered = true; }
            finally { Shader.SetGlobalFloat(StudioLight, 0); }
        }
        private void FitDisplay()
        {
            var size = picture.rectTransform.rect.size;
            if (size.x <= 1 || size.y <= 1) return;
            float aspect = size.x / size.y;
            int width = aspect >= 1 ? 512 : Mathf.Max(8, Mathf.RoundToInt(512 * aspect));
            int height = aspect >= 1 ? Mathf.Max(8, Mathf.RoundToInt(512 / aspect)) : 512;
            if (texture == null || texture.width != width || texture.height != height)
            {
                if (texture != null) { previewCamera.targetTexture = null; texture.Release(); Destroy(texture); }
                texture = new RenderTexture(width,height,24,RenderTextureFormat.ARGB32) { name = "Live wardrobe preview", antiAliasing = 2, filterMode = FilterMode.Bilinear, useMipMap = false };
                texture.Create(); previewCamera.targetTexture = texture; picture.texture = texture; HasRendered = false;
            }
            previewCamera.aspect = width / (float)height;
            if (hasCharacterView)
            {
                // The same view every frame, from where the camera first stood: the character never leaves it.
                previewCamera.orthographicSize = Mathf.Max(.6f, Mathf.Max((characterView.w - characterView.z) * .5f, (characterView.y - characterView.x) * .5f / previewCamera.aspect) * 1.08f);
                previewCamera.transform.position = cameraOrigin + previewCamera.transform.right * ((characterView.x + characterView.y) * .5f) +
                    previewCamera.transform.up * ((characterView.z + characterView.w) * .5f);
                return;
            }
            // Use each mesh's oriented local bounds. A world-space AABB or sphere would
            // make wide wings needlessly tiny and lose the vertical creature silhouette.
            float minX = float.PositiveInfinity, minY = float.PositiveInfinity, maxX = float.NegativeInfinity, maxY = float.NegativeInfinity;
            void Include(Bounds bounds, Matrix4x4 matrix)
            {
                for (int i = 0; i < 8; i++)
                {
                    var point = bounds.center + Vector3.Scale(bounds.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    var projected = matrix.MultiplyPoint3x4(point);
                    minX = Mathf.Min(minX, projected.x); maxX = Mathf.Max(maxX, projected.x);
                    minY = Mathf.Min(minY, projected.y); maxY = Mathf.Max(maxY, projected.y);
                }
            }
            foreach (var filter in meshFilters) Include(filter.sharedMesh.bounds, previewCamera.worldToCameraMatrix * filter.transform.localToWorldMatrix);
            previewCamera.orthographicSize = Mathf.Max(.6f, Mathf.Max((maxY - minY) * .5f, (maxX - minX) * .5f / previewCamera.aspect) * 1.10f);
            previewCamera.transform.position += previewCamera.transform.right * ((minX + maxX) * .5f) + previewCamera.transform.up * ((minY + maxY) * .5f);
        }
        private void OnDisable() { if (previewCamera != null) previewCamera.enabled = false; }
        private void OnDestroy()
        {
            if (stage != null) Destroy(stage);
            if (texture != null) { texture.Release(); Destroy(texture); }
        }
    }
}
