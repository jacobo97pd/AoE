using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Emberfield.Presentation;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Emberfield.Editor
{
    /// <summary>One import path for crew rigs, their shared atlas, and the replacement raider's own PBR maps.</summary>
    internal static class PirateCharacterAssetImport
    {
        internal const string Root = "Assets/Game/PirateCrew";
        internal static readonly string[] BaseStates = { "Idle", "Walk", "Run", "Attack", "Hit", "Death" };
        internal static string CharacterRoot(string name) => Root + "/" + name;
        internal static string ModelPath(string name) => CharacterRoot(name) + "/Model/" + name + ".fbx";
        internal static string PrefabPath(string name) => Root + "/Resources/ImportedUnits/" + name + ".prefab";
        internal static string ControllerPath(string name) => CharacterRoot(name) + "/Animations/" + name + ".controller";
        // The worker adds its gathering loop; the pistol corsair holds an aim between shots.
        internal static string[] States(string name) => name == "TreasureSeeker" ? BaseStates.Concat(new[] { "Work" }).ToArray() :
            name == "GunpowderCorsair" ? BaseStates.Concat(new[] { "Aim" }).ToArray() : BaseStates;

        internal static T Save<T>(T value, string path) where T : Object
        {
            var old = AssetDatabase.LoadAssetAtPath<T>(path);
            if (!old) { AssetDatabase.CreateAsset(value, path); return value; }
            EditorUtility.CopySerialized(value, old); EditorUtility.SetDirty(old); Object.DestroyImmediate(value);
            return old;
        }

        internal static Material BuildSharedMaterial() => BuildMaterial(Root, "PirateCrew");

        internal static Material MaterialForCharacter(string name, Material shared)
        {
            string root = CharacterRoot(name);
            // Only the replacement raider owns a different atlas. Do not silently
            // apply the original three-character atlas to its new UV layout.
            if (name != "BoardingRaider" || !Directory.Exists(root + "/Textures")) return shared;
            var material = AssetDatabase.LoadAssetAtPath<Material>(root + "/Materials/MeshyRaider.mat");
            return material ? material : BuildMeshyRaiderMaterial();
        }

        internal static Material BuildMeshyRaiderMaterial() => BuildMaterial(CharacterRoot("BoardingRaider"), "MeshyRaider");

        private static Material BuildMaterial(string root, string name)
        {
            Directory.CreateDirectory(root + "/Materials");
            // Preserve supplied base color and normals. Pack linear metallic R
            // and inverse roughness A for the URP metallic workflow.
            var metallic = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
            var roughness = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
            try
            {
                if (!metallic.LoadImage(File.ReadAllBytes(root + "/Textures/Metallic.png")) || !roughness.LoadImage(File.ReadAllBytes(root + "/Textures/Roughness.png"))) throw new InvalidOperationException("Cannot decode the supplied crew PBR data maps.");
                if (metallic.width != roughness.width || metallic.height != roughness.height) throw new InvalidOperationException("Crew metallic and roughness texture dimensions differ.");
                var metal = metallic.GetPixels32(); var rough = roughness.GetPixels32();
                for (int i = 0; i < metal.Length; i++) metal[i] = new Color32(metal[i].r, 0, 0, (byte)(255 - rough[i].r));
                metallic.SetPixels32(metal); metallic.Apply();
                File.WriteAllBytes(root + "/Textures/MetallicSmoothness.png", metallic.EncodeToPNG());
            }
            finally { Object.DestroyImmediate(metallic); Object.DestroyImmediate(roughness); }
            AssetDatabase.Refresh();
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (!shader) throw new InvalidOperationException("URP Lit is unavailable.");
            var material = new Material(shader) { name = name, enableInstancing = true };
            material.SetColor("_BaseColor", Color.white);
            material.SetTexture("_BaseMap", Map(root, "BaseColor.png", false, true));
            material.SetTexture("_BumpMap", Map(root, "Normal.png", true, false));
            material.SetTexture("_MetallicGlossMap", Map(root, "MetallicSmoothness.png", false, false));
            Map(root, "Metallic.png", false, false); Map(root, "Roughness.png", false, false);
            material.SetFloat("_WorkflowMode", 1); material.SetFloat("_Metallic", 1); material.SetFloat("_Smoothness", 1); material.SetFloat("_BumpScale", 1);
            material.SetFloat("_Surface", 0); material.SetFloat("_Cull", 2); material.SetFloat("_SrcBlend", 1); material.SetFloat("_DstBlend", 0); material.SetFloat("_ZWrite", 1);
            material.EnableKeyword("_NORMALMAP"); material.EnableKeyword("_METALLICSPECGLOSSMAP");
            return Save(material, root + "/Materials/" + name + ".mat");
        }

        private static Texture2D Map(string root, string name, bool normal, bool srgb)
        {
            string path = root + "/Textures/" + name;
            if (!File.Exists(path)) throw new FileNotFoundException("Missing crew atlas texture.", path);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = srgb; importer.convertToNormalmap = false; importer.maxTextureSize = 4096;
            importer.mipmapEnabled = true; importer.filterMode = FilterMode.Trilinear; importer.wrapMode = TextureWrapMode.Repeat;
            importer.anisoLevel = 8; importer.textureCompression = TextureImporterCompression.CompressedHQ; importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        internal static void ImportModel(string name)
        {
            string path = ModelPath(name);
            if (!File.Exists(path)) throw new FileNotFoundException("The rigged crew FBX is missing.", path);
            Directory.CreateDirectory(CharacterRoot(name) + "/Animations");
            Directory.CreateDirectory(Root + "/Resources/ImportedUnits");
            AssetDatabase.Refresh(); AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            importer.importAnimation = true; importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel; importer.optimizeGameObjects = false;
            importer.animationCompression = ModelImporterAnimationCompression.Off;
            importer.importCameras = false; importer.importLights = false; importer.globalScale = 1; importer.useFileScale = true;
            importer.importNormals = ModelImporterNormals.Import; importer.importTangents = ModelImporterTangents.CalculateMikk;
            importer.meshCompression = ModelImporterMeshCompression.Off; importer.isReadable = true;
            importer.materialImportMode = ModelImporterMaterialImportMode.None; importer.SaveAndReimport();
            var available = importer.defaultClipAnimations;
            var selected = new List<ModelImporterClipAnimation>();
            foreach (string state in States(name))
            {
                var clip = available.FirstOrDefault(c => MatchesState(c.name, state) || MatchesState(c.takeName, state));
                if (clip == null) throw new InvalidOperationException(name + " is missing " + state + "; FBX takes: " + string.Join(", ", available.Select(c => c.name)));
                clip.name = "Corsair_" + state;
                clip.loopTime = state == "Idle" || state == "Walk" || state == "Run" || state == "Work" || state == "Aim"; clip.loopPose = clip.loopTime;
                clip.lockRootRotation = true; clip.lockRootHeightY = true; clip.lockRootPositionXZ = true;
                clip.keepOriginalOrientation = true; clip.keepOriginalPositionY = true; clip.keepOriginalPositionXZ = true;
                selected.Add(clip);
            }
            importer.clipAnimations = selected.ToArray(); importer.SaveAndReimport();
        }

        private static bool MatchesState(string name, string state) => !string.IsNullOrEmpty(name) &&
            (name.Equals(state, StringComparison.OrdinalIgnoreCase) || name.EndsWith("_" + state, StringComparison.OrdinalIgnoreCase) || name.EndsWith("|" + state, StringComparison.OrdinalIgnoreCase));

        [Serializable] private sealed class Manifest { public Locomotion locomotion; }
        [Serializable] private sealed class Locomotion { public float walkMetresPerSecond, runMetresPerSecond; }

        /// <summary>Copy the ground speed measured on the Walk and Run clips onto the driver.</summary>
        internal static void ApplyLocomotion(CorsairAnimationDriver driver, string manifestPath)
        {
            var speeds = JsonUtility.FromJson<Manifest>(File.ReadAllText(manifestPath))?.locomotion;
            if (speeds == null || speeds.walkMetresPerSecond <= 0 || speeds.runMetresPerSecond <= speeds.walkMetresPerSecond)
                throw new InvalidOperationException("The rig manifest must record the Walk and Run ground speeds: " + manifestPath);
            driver.WalkMetresPerSecond = speeds.walkMetresPerSecond;
            driver.RunMetresPerSecond = speeds.runMetresPerSecond;
        }

        internal static AnimatorController BuildController(string name)
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath(name));
            if (!controller) controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath(name));
            var machine = controller.layers[0].stateMachine;
            foreach (var state in machine.states) machine.RemoveState(state.state);
            foreach (var transition in machine.anyStateTransitions) machine.RemoveAnyStateTransition(transition);
            var clips = AssetDatabase.LoadAllAssetsAtPath(ModelPath(name)).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__", StringComparison.Ordinal)).ToArray();
            foreach (string state in States(name))
            {
                var clip = clips.FirstOrDefault(c => c.name == "Corsair_" + state);
                if (!clip) throw new InvalidOperationException("Missing imported animation: " + name + "/" + state);
                var node = machine.AddState(state); node.motion = clip; node.writeDefaultValues = true;
                if (state == "Idle") machine.defaultState = node;
            }
            EditorUtility.SetDirty(controller); return controller;
        }

        internal static GameObject BuildPrefab(string name, Material material, AnimatorController controller)
        {
            material = MaterialForCharacter(name, material);
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath(name));
            if (!source) throw new InvalidOperationException("Missing imported character " + name);
            var character = (GameObject)PrefabUtility.InstantiatePrefab(source); character.name = name;
            var skins = character.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            foreach (var skin in skins)
            {
                skin.enabled = true; skin.updateWhenOffscreen = true; skin.quality = SkinQuality.Bone4;
                for (var node = skin.transform; node != null && node != character.transform; node = node.parent) node.gameObject.SetActive(true);
                skin.sharedMaterials = Enumerable.Repeat(material, Math.Max(1, skin.sharedMaterials.Length)).ToArray();
                skin.shadowCastingMode = ShadowCastingMode.On; skin.receiveShadows = true;
            }
            var animator = character.GetComponent<Animator>() ?? character.AddComponent<Animator>();
            if (!animator.avatar) animator.avatar = AssetDatabase.LoadAllAssetsAtPath(ModelPath(name)).OfType<Avatar>().FirstOrDefault();
            animator.runtimeAnimatorController = controller; animator.applyRootMotion = false; animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            var driver = character.GetComponent<CorsairAnimationDriver>();
            if (!driver) driver = character.AddComponent<CorsairAnimationDriver>();
            ApplyLocomotion(driver, CharacterRoot(name) + "/model-manifest.json");
            var group = character.GetComponent<LODGroup>() ?? character.AddComponent<LODGroup>();
            var lods = new List<LOD>(); float[] heights = { .32f, .12f, .012f };
            for (int i = 0; i < 3; i++)
            {
                string key = "LOD" + i;
                var selected = skins.Where(s => s.name.IndexOf(key, StringComparison.OrdinalIgnoreCase) >= 0).Cast<Renderer>().ToArray();
                if (selected.Length != 1) throw new InvalidOperationException(name + " must have exactly one skinned renderer for " + key);
                lods.Add(new LOD(heights[i], selected));
            }
            group.SetLODs(lods.ToArray()); group.fadeMode = LODFadeMode.None; group.RecalculateBounds();
            if (name == "GunpowderCorsair" && !character.GetComponentsInChildren<Transform>(true).Any(t => t.name == "Muzzle"))
                throw new InvalidOperationException("The pistol's authored Muzzle transform is missing.");
            var prefab = PrefabUtility.SaveAsPrefabAsset(character, PrefabPath(name));
            Object.DestroyImmediate(character);
            return prefab;
        }
    }
}
