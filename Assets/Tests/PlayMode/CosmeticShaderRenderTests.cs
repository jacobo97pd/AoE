using System.Collections;
using Emberfield.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;

namespace Emberfield.Tests.PlayMode
{
    public sealed class CosmeticShaderRenderTests
    {
        [UnityTest]
        public IEnumerator SapphireActuallyRendersBlueWhileOwnershipPixelsStayUnchangedAndClearRestoresTheSource()
        {
            var holder = new GameObject("Cosmetic pixel verification"); Mesh mesh = null; RenderTexture target = null; Texture2D pixels = null;
            try
            {
                var surfaces = new GameObject("Body ownership ornament", typeof(MeshFilter), typeof(MeshRenderer)); surfaces.transform.SetParent(holder.transform, false); surfaces.layer = 29;
                var vertices = new Vector3[12]; var normals = new Vector3[12]; var colors = new Color[12]; var triangles = new int[18];
                for (int panel = 0; panel < 3; panel++)
                {
                    float left = -3 + panel * 2, right = left + 2; int vertex = panel * 4, index = panel * 6;
                    vertices[vertex] = new Vector3(left, -1, 0); vertices[vertex + 1] = new Vector3(left, 1, 0);
                    vertices[vertex + 2] = new Vector3(right, 1, 0); vertices[vertex + 3] = new Vector3(right, -1, 0);
                    var color = panel == 0 ? new Color(.71f, .29f, .17f, 0) : panel == 1 ? new Color(.92f, .89f, .76f, 1) : new Color(.92f, .64f, .23f, 2);
                    for (int i = 0; i < 4; i++) { normals[vertex + i] = Vector3.back; colors[vertex + i] = color; }
                    triangles[index] = vertex; triangles[index + 1] = vertex + 1; triangles[index + 2] = vertex + 2;
                    triangles[index + 3] = vertex; triangles[index + 4] = vertex + 2; triangles[index + 5] = vertex + 3;
                }
                mesh = new Mesh { name = "Actual shader three-mask render sample", vertices = vertices, normals = normals, colors = colors, triangles = triangles }; mesh.RecalculateBounds();
                surfaces.GetComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = surfaces.GetComponent<MeshRenderer>(); renderer.sharedMaterial = ArtKit.Catalog.Material; renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
                var block = new MaterialPropertyBlock(); block.SetColor("_TeamColor", AlphaWorldArt.OwnerColor(1)); renderer.SetPropertyBlock(block);
                var camera = new GameObject("Cosmetic render camera", typeof(Camera)).GetComponent<Camera>(); camera.transform.SetParent(holder.transform, false);
                camera.transform.position = new Vector3(0, 0, -10); camera.cullingMask = 1 << 29; camera.orthographic = true; camera.orthographicSize = 1; camera.aspect = 3;
                camera.nearClipPlane = .1f; camera.farClipPlane = 20; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
                camera.allowHDR = false; camera.enabled = false;
                target = new RenderTexture(192, 64, 24, RenderTextureFormat.ARGB32); target.Create(); camera.targetTexture = target;
                pixels = new Texture2D(192, 64, TextureFormat.RGB24, false);
                yield return null;
                var original = Read(camera, target, pixels);
                Assert.That(original[0].r, Is.GreaterThan(original[0].b * 1.20f), "The reference surface must really render as warm dragon membrane.");
                AlphaWorldArt.ApplyCosmetic(surfaces.transform, CosmeticLoadout.Style("sapphire_frost"));
                var sapphire = Read(camera, target, pixels);
                Assert.That(sapphire[0].b, Is.GreaterThan(sapphire[0].r * 1.50f), "Sapphire must be visibly blue, not a property block that still renders brown.");
                Assert.That(sapphire[2].b, Is.GreaterThan(sapphire[2].r), "Ornaments must use the pale sapphire accent.");
                Assert.That(Vector4.Distance(original[1], sapphire[1]), Is.LessThan(.015f), "Ownership cloth pixels must remain independent of the cosmetic.");
                AlphaWorldArt.ApplyCosmetic(surfaces.transform, default);
                var restored = Read(camera, target, pixels);
                for (int panel = 0; panel < 3; panel++) Assert.That(Vector4.Distance(original[panel], restored[panel]), Is.LessThan(.015f), "Clearing equipment must restore the rendered source.");
            }
            finally
            {
                if (target != null) { target.Release(); Object.DestroyImmediate(target); }
                if (pixels != null) Object.DestroyImmediate(pixels);
                Object.DestroyImmediate(holder); if (mesh != null) Object.DestroyImmediate(mesh);
            }
        }
        private static Color[] Read(Camera camera, RenderTexture target, Texture2D pixels)
        {
            var request = new RenderPipeline.StandardRequest { destination = target };
            Assert.IsTrue(RenderPipeline.SupportsRenderRequest(camera, request)); RenderPipeline.SubmitRenderRequest(camera, request);
            var active = RenderTexture.active;
            try { RenderTexture.active = target; pixels.ReadPixels(new Rect(0, 0, 192, 64), 0, 0); pixels.Apply(false, false); }
            finally { RenderTexture.active = active; }
            return new[] { pixels.GetPixel(32, 32), pixels.GetPixel(96, 32), pixels.GetPixel(160, 32) };
        }
    }
}
