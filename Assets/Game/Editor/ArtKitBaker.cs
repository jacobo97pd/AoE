using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using Emberfield.Presentation;
using Emberfield.Simulation;
using UnityEditor;
using UnityEngine;

namespace Emberfield.Editor
{
    public static class ArtKitBaker
    {
        public const string OutputDirectory = "Assets/Game/Resources/Art";
        public const string SourceVersion = "emberfield-original-faceted-v1";
        [Serializable] private sealed class Manifest
        {
            public string SourceVersion;
            public string Provenance;
            public string RecipeSha256;
            public string Shader;
            public int SharedMaterials;
            public int SharedMeshes;
            public Entry[] Assets;
        }
        [Serializable] private sealed class Entry
        {
            public string Category, Id, Lod0Path, Lod1Path;
            public int Lod0Triangles, Lod1Triangles, Lod0VertexCount, Lod1VertexCount;
            public int Lod0TriangleBudget, Lod1TriangleBudget;
            public Vector3 BoundsMin, BoundsMax;
        }

        [MenuItem("Emberfield/Art/Bake original Aven slice")]
        public static void Bake()
        {
            if (!AssetDatabase.IsValidFolder(OutputDirectory)) AssetDatabase.CreateFolder("Assets/Game/Resources", "Art");
            var shader = Resources.Load<Shader>("Shaders/FacetedTeam");
            if (shader == null) throw new InvalidOperationException("Import FacetedTeam.shader before baking the kit.");
            var material = new Material(shader) { name = "Emberfield shared faceted team", enableInstancing = true };
            material.SetColor("_TeamColor", Color.white);
            var sharedMaterial = Save(material, OutputDirectory + "/FacetedTeam.mat");
            var manifest = new List<Entry>();
            var catalog = ScriptableObject.CreateInstance<ArtKitCatalog>();
            catalog.SourceVersion = SourceVersion; catalog.Material = sharedMaterial;
            var units = new[] { "tender", "reedguard", "stringwarden", "strider" };
            var buildings = new[] { "hearth", "shelter", "muster_hall" };
            catalog.Units = new ArtKitEntry[units.Length]; catalog.Buildings = new ArtKitEntry[buildings.Length];
            catalog.Resources = new ArtKitEntry[4];
            for (int i = 0; i < units.Length; i++) catalog.Units[i] = Pair("unit", units[i], ArtKitGeometry.Unit(units[i], 0), ArtKitGeometry.Unit(units[i], 1), 1500, 900, manifest);
            for (int i = 0; i < buildings.Length; i++) catalog.Buildings[i] = Pair("building", buildings[i], ArtKitGeometry.Building(buildings[i], 0), ArtKitGeometry.Building(buildings[i], 1), 2000, 1000, manifest);
            for (int i = 0; i < 4; i++)
            {
                var kind = (ResourceKind)i;
                catalog.Resources[i] = Pair("resource", kind.ToString(), ArtKitGeometry.Resource(kind, 0), ArtKitGeometry.Resource(kind, 1), 1000, 500, manifest);
            }
            Save(catalog, OutputDirectory + "/EmberfieldKit.asset");
            var report = new Manifest {
                SourceVersion = SourceVersion,
                Provenance = "Original geometry and palettes authored in ArtKitGeometry.cs for Emberfield; no external meshes, textures, reference-image tracing or generative bitmap assets.",
                RecipeSha256 = Hash("Assets/Game/Presentation/ArtKitGeometry.cs"),
                Shader = "Emberfield/Faceted Team", SharedMaterials = 1, SharedMeshes = manifest.Count * 2, Assets = manifest.ToArray()
            };
            File.WriteAllText(OutputDirectory + "/ART_KIT_MANIFEST.json", JsonUtility.ToJson(report, true) + Environment.NewLine);
            AssetDatabase.ImportAsset(OutputDirectory + "/ART_KIT_MANIFEST.json");
            AssetDatabase.SaveAssets();
            Debug.Log("EMBERFIELD_ART_BAKE " + SourceVersion + ": " + report.SharedMeshes + " shared meshes, one shared material; measured triangle counts and bounds in ART_KIT_MANIFEST.json.");
        }

        private static ArtKitEntry Pair(string category, string id, Mesh high, Mesh low, int highBudget, int lowBudget, List<Entry> manifest)
        {
            string highPath = OutputDirectory + "/" + category + "_" + id + "_lod0.asset";
            string lowPath = OutputDirectory + "/" + category + "_" + id + "_lod1.asset";
            int highTriangles = high.triangles.Length / 3, lowTriangles = low.triangles.Length / 3;
            if (highTriangles > highBudget || lowTriangles > lowBudget || lowTriangles >= highTriangles)
                throw new InvalidOperationException(category + " " + id + " violates its declared LOD budgets.");
            manifest.Add(new Entry {
                Category = category, Id = id, Lod0Path = highPath, Lod1Path = lowPath,
                Lod0Triangles = highTriangles, Lod1Triangles = lowTriangles,
                Lod0VertexCount = high.vertexCount, Lod1VertexCount = low.vertexCount,
                Lod0TriangleBudget = highBudget, Lod1TriangleBudget = lowBudget,
                BoundsMin = high.bounds.min, BoundsMax = high.bounds.max
            });
            return new ArtKitEntry { Id = id, Lod0 = Save(high, highPath), Lod1 = Save(low, lowPath) };
        }

        private static T Save<T>(T asset, string path) where T : UnityEngine.Object
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing == null) { AssetDatabase.CreateAsset(asset, path); return asset; }
            EditorUtility.CopySerialized(asset, existing); EditorUtility.SetDirty(existing);
            UnityEngine.Object.DestroyImmediate(asset); return existing;
        }
        private static string Hash(string path)
        {
            using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-", "").ToLowerInvariant();
        }
    }
}
