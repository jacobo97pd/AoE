using Emberfield.Simulation;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace Emberfield.Presentation
{
    // A camera renders the same shared army mesh used on the battlefield. It has no simulation entity.
    public sealed class CosmeticModelPreview : MonoBehaviour
    {
        private GameObject stage;
        private Transform model;
        private Camera previewCamera;
        private RenderTexture texture;
        private Vector3 center;
        private RawImage picture;
        private MeshFilter[] meshFilters;
        private float nextRender;
        public bool HasRendered { get; private set; }
        public void Initialize(RawImage image, CosmeticCatalogItem item)
        {
            picture = image;
            stage = new GameObject("Wardrobe preview stage"); stage.transform.position = new Vector3(10000,0,10000);
            string id = item.targetId == "*" ? "keep" : item.targetId;
            FactionKind faction = item.realmId == "naval" ? FactionKind.PirateBrotherhood : item.realmId == "fantasy" ? FactionKind.DrakeforgedClans : FactionKind.AvenCompact;
            if (id == "dune_elephant") faction = FactionKind.MirajSultanate;
            if (id == "war_troll") faction = FactionKind.AshenDominion;
            if (id == "grove_guardian") faction = FactionKind.VerdantCovenant;
            if (id == "sun_lion" || item.styleId == "luminous_ward") faction = FactionKind.SolarKingdom;
            model = CosmeticLoadout.IsBuilding(id)
                ? AlphaWorldArt.Building(id,faction,stage.transform,1,4,4)
                : AlphaWorldArt.Unit(id,faction,stage.transform,1);
            if (model == null) return;
            foreach (var child in stage.GetComponentsInChildren<Transform>()) child.gameObject.layer = 30;
            foreach (var lod in stage.GetComponentsInChildren<LODGroup>()) lod.ForceLOD(0);
            AlphaWorldArt.ApplyCosmetic(model, CosmeticLoadout.Style(item.styleId));
            // This is a studio display, isolated from the active map's shadows and atmosphere.
            var lightBlock = new MaterialPropertyBlock();
            foreach (var renderer in model.GetComponentsInChildren<MeshRenderer>(true))
            {
                lightBlock.Clear(); renderer.GetPropertyBlock(lightBlock); lightBlock.SetFloat("_PreviewLighting", 1); renderer.SetPropertyBlock(lightBlock);
                renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            }
            meshFilters = model.GetComponentsInChildren<MeshFilter>(true);
            var bounds = new Bounds(model.position,Vector3.zero);
            foreach (var renderer in model.GetComponentsInChildren<Renderer>()) bounds.Encapsulate(renderer.bounds);
            center = bounds.center;
            var cameraObject = new GameObject("Wardrobe camera"); cameraObject.transform.SetParent(stage.transform,false);
            previewCamera = cameraObject.AddComponent<Camera>(); previewCamera.cullingMask = 1 << 30;
            previewCamera.clearFlags = CameraClearFlags.SolidColor; previewCamera.backgroundColor = new Color(.14f,.21f,.24f);
            previewCamera.orthographic = true;
            previewCamera.transform.position = center + new Vector3(7,5,-9); previewCamera.transform.LookAt(center);
            previewCamera.nearClipPlane = .1f; previewCamera.farClipPlane = 40; previewCamera.allowHDR = false;
            var data = previewCamera.GetUniversalAdditionalCameraData(); data.renderPostProcessing = false; data.renderShadows = false;
            previewCamera.enabled = false;
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
            RenderPipeline.SubmitRenderRequest(previewCamera,request); HasRendered = true;
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
            // Use each mesh's oriented local bounds. A world-space AABB or sphere would
            // make wide wings needlessly tiny and lose the vertical creature silhouette.
            float minX = float.PositiveInfinity, minY = float.PositiveInfinity, maxX = float.NegativeInfinity, maxY = float.NegativeInfinity;
            foreach (var filter in meshFilters)
            {
                var bounds = filter.sharedMesh.bounds;
                var matrix = previewCamera.worldToCameraMatrix * filter.transform.localToWorldMatrix;
                for (int i = 0; i < 8; i++)
                {
                    var point = bounds.center + Vector3.Scale(bounds.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    var projected = matrix.MultiplyPoint3x4(point);
                    minX = Mathf.Min(minX, projected.x); maxX = Mathf.Max(maxX, projected.x);
                    minY = Mathf.Min(minY, projected.y); maxY = Mathf.Max(maxY, projected.y);
                }
            }
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
