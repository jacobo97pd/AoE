using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Emberfield.Presentation;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Emberfield.Editor
{
    /// <summary>Measured persistent-asset and sampled-animation checks; no art approval or mobile performance claim.</summary>
    public static class ArtOverhaulValidator
    {
        private const string Root = "Assets/Game/ArtOverhaul";
        private const string SurfacePath = Root+"/Materials/PaintedSurface.mat";
        private const string AtlasPath = Root+"/Textures/SurfaceAtlas.png";
        // Mirrors the authored scene casts, rather than validating nonexistent cross-biome combinations.
        private static readonly ArtUnitKind[][] Cast = {
            new[] { ArtUnitKind.Worker,ArtUnitKind.Warrior,ArtUnitKind.Cavalry,ArtUnitKind.Hero,ArtUnitKind.MountainWarrior },
            new[] { ArtUnitKind.Worker,ArtUnitKind.Pirate,ArtUnitKind.Hero,ArtUnitKind.Warrior },
            new[] { ArtUnitKind.Worker,ArtUnitKind.Warrior,ArtUnitKind.Cavalry,ArtUnitKind.Hero },
            new[] { ArtUnitKind.Elf,ArtUnitKind.Dwarf,ArtUnitKind.Dragon,ArtUnitKind.Hero }
        };
        private static readonly string[] States = { "Idle","Walk","Action" };

        [Serializable] public sealed class LodReport
        {
            public int level,triangles,vertices,bones,bindposes,teamVertices,neutralVertices,degenerateTriangles;
            public string mesh,material;
            public float transitionHeight,maxWeightError,maxBindposeError,maxBakedRestError;
        }
        [Serializable] public sealed class AnimationReport
        {
            public string name,path; public float length,maxVertexMovement,maxWeightedBonePositionChange,maxWeightedBoneRotationDegrees,maxRootDrift,maxRootRotationDegrees,maxSkeletonRootDrift;
            public bool loop,geometryMoves,weightedBonesMove,rootStable,passed; public int sampledPoses;
        }
        [Serializable] public sealed class PrefabReport
        {
            public string path,biome,kind; public bool passed; public int materialCount,teamChecks;
            public List<LodReport> lods = new List<LodReport>(); public List<AnimationReport> animations = new List<AnimationReport>();
            public List<string> errors = new List<string>(),warnings = new List<string>();
        }
        [Serializable] public sealed class SceneReport
        {
            public string path,biome,pipeline; public bool passed; public int units,cameras,renderers,uniqueMaterials,uniqueMeshes;
            public long referencedMeshTriangles;
            public List<string> errors = new List<string>();
        }
        [Serializable] public sealed class Report
        {
            public string generatedUtc,unityVersion;
            public string scope = "Persistent native 3D art assets, LOD/UV/skin integrity, prefab-scene references and CPU-sampled weighted geometry. This validates neither visual parity nor mobile FPS/GPU time. Mesh triangle sums describe referenced assets, not actual frame workload.";
            public bool passed,kingdomOnly; public int expectedPrefabs,prefabCount,archetypeCount,sceneCount,sharedUnitMaterials,atlasWidth,atlasHeight;
            public List<PrefabReport> prefabs = new List<PrefabReport>(); public List<SceneReport> scenes = new List<SceneReport>();
            public List<string> errors = new List<string>();
        }

        public static Report ValidateOrThrow(string path,bool kingdomOnly=false)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("An output report path is required.",nameof(path));
            var report = Validate(kingdomOnly);
            string folder = Path.GetDirectoryName(Path.GetFullPath(path)); Directory.CreateDirectory(folder);
            File.WriteAllText(path,JsonUtility.ToJson(report,true));
            if (!report.passed) throw new InvalidOperationException("Art overhaul asset validation failed: "+string.Join(" | ",report.errors));
            return report;
        }

        public static Report Validate(bool kingdomOnly=false)
        {
            var report = new Report { generatedUtc=DateTime.UtcNow.ToString("O"),unityVersion=Application.unityVersion,kingdomOnly=kingdomOnly,expectedPrefabs=kingdomOnly?5:17 };
            var material = AssetDatabase.LoadAssetAtPath<Material>(SurfacePath);
            ValidateSurface(material,report);
            ValidateVisualData(report.errors);
            var kinds = new HashSet<ArtUnitKind>(); var materials = new HashSet<Material>();
            foreach (ArtBiome biome in Enum.GetValues(typeof(ArtBiome)))
            {
                if (kingdomOnly && biome != ArtBiome.Kingdom) continue;
                foreach (var kind in Cast[(int)biome])
                {
                    string path = Root+"/Prefabs/"+biome+"_"+kind+".prefab";
                    var measured = new PrefabReport { path=path,biome=biome.ToString(),kind=kind.ToString() }; report.prefabs.Add(measured);
                    try
                    {
                        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                        if (prefab == null) measured.errors.Add("Missing persistent prefab.");
                        else
                        {
                            report.prefabCount++; kinds.Add(kind);
                            ValidatePrefab(prefab,biome,kind,material,measured,materials);
                            if (measured.errors.Count == 0) SamplePrefab(path,measured);
                        }
                    }
                    catch (Exception error) { measured.errors.Add(error.GetType().Name+": "+error.Message); }
                    measured.passed = measured.errors.Count == 0;
                    foreach (string error in measured.errors) report.errors.Add(biome+"/"+kind+": "+error);
                }
                var scene = ValidateScene(biome,material); report.scenes.Add(scene);
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scene.path) != null) report.sceneCount++;
                foreach (string error in scene.errors) report.errors.Add(biome+" scene: "+error);
            }
            report.archetypeCount = kinds.Count; report.sharedUnitMaterials = materials.Count;
            if (report.prefabCount != report.expectedPrefabs) report.errors.Add("Expected "+report.expectedPrefabs+" persistent prefabs, found "+report.prefabCount+".");
            if (report.archetypeCount != (kingdomOnly?5:9)) report.errors.Add("The required visual archetypes are incomplete.");
            if (report.sceneCount != (kingdomOnly?1:4)) report.errors.Add("The required art scenes are incomplete.");
            if (materials.Count != 1 || !materials.Contains(material)) report.errors.Add("All unit LODs must reuse the one persistent PaintedSurface material.");
            report.passed = report.errors.Count == 0; return report;
        }

        private static void ValidateSurface(Material material,Report report)
        {
            if (material == null) { report.errors.Add("Missing shared unit material: "+SurfacePath); return; }
            if (material.shader == null || material.shader.name != "Emberfield/Overhaul Painted Surface" || ShaderUtil.ShaderHasError(material.shader))
                report.errors.Add("Shared painted-surface shader is missing, incorrect or has compile errors.");
            foreach (string property in new[] { "_SurfaceMap","_TeamColor","_Metallic","_Smoothness","_Selection" })
                if (!material.HasProperty(property)) report.errors.Add("Shared shader property missing: "+property);
            var atlas = AssetDatabase.LoadAssetAtPath<Texture2D>(AtlasPath);
            if (atlas == null || !material.HasProperty("_SurfaceMap") || material.GetTexture("_SurfaceMap") != atlas)
            { report.errors.Add("The shared material does not reference its persistent surface atlas."); return; }
            report.atlasWidth = atlas.width; report.atlasHeight = atlas.height;
            if (atlas.width > 2048 || atlas.height > 2048 || !Mathf.IsPowerOfTwo(atlas.width) || !Mathf.IsPowerOfTwo(atlas.height))
                report.errors.Add("Imported atlas dimensions must be powers of two no larger than 2048.");
            if (atlas.width != atlas.height) report.errors.Add("The sixteen-cell atlas must be square.");
            var importer = AssetImporter.GetAtPath(AtlasPath) as TextureImporter;
            if (importer == null || !importer.mipmapEnabled) report.errors.Add("Surface atlas requires a texture importer with mipmaps.");
        }

        private static void ValidateVisualData(List<string> errors)
        {
            var prohibited = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "Stats","Health","MaxHealth","Damage","Armor","Speed","MoveSpeed","Cost","Population","PopulationCost","Attack","Range","GatherAmount","TrainTicks" };
            foreach (var type in new[] { typeof(ArtOverhaulUnit),typeof(UnitVisualDefinition),typeof(CosmeticSkinDefinition),typeof(FactionVisualDefinition) })
                foreach (var field in type.GetFields(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.DeclaredOnly))
                    if (prohibited.Contains(field.Name) || IncludesSimulation(field.FieldType)) errors.Add(type.Name+"."+field.Name+" contains gameplay data in a visual definition.");
        }

        private static bool IncludesSimulation(Type type)
        {
            if (type.Namespace != null && type.Namespace.StartsWith("Emberfield.Simulation",StringComparison.Ordinal)) return true;
            if (type.HasElementType && IncludesSimulation(type.GetElementType())) return true;
            if (type.IsGenericType) foreach (var argument in type.GetGenericArguments()) if (IncludesSimulation(argument)) return true;
            return false;
        }

        private static void ValidatePrefab(GameObject prefab,ArtBiome biome,ArtUnitKind kind,Material expectedMaterial,PrefabReport report,HashSet<Material> allMaterials)
        {
            var unit = prefab.GetComponent<ArtOverhaulUnit>();
            if (!PrefabUtility.IsPartOfPrefabAsset(prefab) || !EditorUtility.IsPersistent(prefab)) report.errors.Add("Unit is not a persistent prefab asset.");
            if (unit == null || unit.Kind != kind || unit.Biome != biome || string.IsNullOrWhiteSpace(unit.Label)) report.errors.Add("Prefab visual identity/label does not match its authored cast.");
            if (prefab.transform.localPosition.sqrMagnitude > .000001f || Quaternion.Angle(prefab.transform.localRotation,Quaternion.identity) > .01f || (prefab.transform.localScale-Vector3.one).sqrMagnitude > .000001f)
                report.errors.Add("Prefab root must have identity position, rotation and scale.");
            foreach (var component in prefab.GetComponentsInChildren<Component>(true))
            {
                if (component == null) { report.errors.Add("Missing component script."); continue; }
                if (IncludesSimulation(component.GetType())) report.errors.Add("Art prefab contains a simulation component.");
            }
            var animator = prefab.GetComponent<Animator>();
            if (animator == null || animator.runtimeAnimatorController == null || animator.avatar == null || !animator.avatar.isValid || animator.applyRootMotion)
                report.errors.Add("Valid in-place Animator/controller/avatar required.");
            else ValidateClips(animator,report);
            var group = prefab.GetComponent<LODGroup>(); var lods = group == null ? Array.Empty<LOD>() : group.GetLODs();
            if (lods.Length != 3 || prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length != 3)
                report.errors.Add("Expected three LODs with one SkinnedMeshRenderer each.");
            int previousTriangles = int.MaxValue; float previousHeight = 1; var materials = new HashSet<Material>();
            Transform[] referenceBones = null;
            for (int level=0;level<lods.Length;level++)
            {
                var lod = lods[level]; var measured = new LodReport { level=level,transitionHeight=lod.screenRelativeTransitionHeight }; report.lods.Add(measured);
                if (!Finite(lod.screenRelativeTransitionHeight) || lod.screenRelativeTransitionHeight <= 0 || lod.screenRelativeTransitionHeight >= previousHeight) report.errors.Add("LOD transitions must be finite, positive and strictly decreasing.");
                previousHeight = lod.screenRelativeTransitionHeight;
                if (lod.renderers.Length != 1 || !(lod.renderers[0] is SkinnedMeshRenderer renderer)) { report.errors.Add("LOD "+level+" has no unique skinned renderer."); continue; }
                if (!renderer.transform.IsChildOf(prefab.transform)) report.errors.Add("LOD renderer is outside its prefab.");
                var mesh = renderer.sharedMesh;
                if (mesh == null) { report.errors.Add("LOD "+level+" mesh is missing."); continue; }
                measured.mesh = AssetDatabase.GetAssetPath(mesh); measured.material = AssetDatabase.GetAssetPath(renderer.sharedMaterial);
                if (!EditorUtility.IsPersistent(mesh) || !measured.mesh.StartsWith(Root+"/Models/",StringComparison.Ordinal)) report.errors.Add("LOD mesh is not persisted in the art model directory.");
                measured.vertices = mesh.vertexCount; measured.triangles = mesh.triangles.Length/3;
                if (measured.vertices == 0 || measured.triangles == 0 || mesh.subMeshCount != 1) report.errors.Add("Every LOD needs nonempty geometry in one submesh.");
                if (measured.triangles >= previousTriangles) report.errors.Add("Triangle counts must strictly decrease across LODs."); previousTriangles = measured.triangles;
                if (renderer.sharedMaterials.Length != 1 || renderer.sharedMaterial == null || renderer.sharedMaterial != expectedMaterial) report.errors.Add("LOD does not reuse the shared PaintedSurface material.");
                if (renderer.sharedMaterial != null) { materials.Add(renderer.sharedMaterial); allMaterials.Add(renderer.sharedMaterial); }
                ValidateMesh(renderer,mesh,prefab,kind,measured,report.errors);
                if (measured.degenerateTriangles > 0) report.warnings.Add("LOD "+level+" includes "+measured.degenerateTriangles+" degenerate/tiny triangles; review wasted geometry.");
                if (referenceBones == null) referenceBones = renderer.bones;
                else
                {
                    if (referenceBones.Length != renderer.bones.Length) report.errors.Add("LOD skeleton sizes differ.");
                    else for (int i=0;i<referenceBones.Length;i++) if (referenceBones[i] != renderer.bones[i]) { report.errors.Add("LOD bone references differ."); break; }
                }
            }
            report.materialCount = materials.Count;
        }

        private static void ValidateMesh(SkinnedMeshRenderer renderer,Mesh mesh,GameObject prefab,ArtUnitKind kind,LodReport measured,List<string> errors)
        {
            var vertices = mesh.vertices; var normals = mesh.normals; var colors = mesh.colors;
            var uv = new List<Vector2>(); var surface = new List<Vector2>(); var atlas = new List<Vector2>();
            mesh.GetUVs(0,uv); mesh.GetUVs(1,surface); mesh.GetUVs(2,atlas);
            if (normals.Length != vertices.Length || colors.Length != vertices.Length || uv.Count != vertices.Length || surface.Count != vertices.Length || atlas.Count != vertices.Length)
            { errors.Add("Vertex normals, colors and all three UV channels must cover every vertex."); return; }
            for (int i=0;i<vertices.Length;i++)
            {
                if (!Finite(vertices[i]) || !Finite(normals[i]) || normals[i].sqrMagnitude < .5f || normals[i].sqrMagnitude > 1.5f)
                { errors.Add("Mesh contains non-finite vertices or invalid normals."); break; }
                var c = colors[i];
                if (!Finite(c.r)||!Finite(c.g)||!Finite(c.b)||!Finite(c.a)||c.a<-.0001f||c.a>1.0001f)
                { errors.Add("Mesh contains invalid vertex color/team-mask data."); break; }
                if (c.a > .1f) measured.teamVertices++; else measured.neutralVertices++;
                if (!Finite(uv[i]) || !Finite(surface[i]) || surface[i].x < -.0001f || surface[i].x > 1.0001f || surface[i].y < -.0001f || surface[i].y > 1.0001f)
                { errors.Add("UV0 or UV1 metallic/smoothness data is invalid."); break; }
                if (!Finite(atlas[i]) || atlas[i].x < -.0001f || atlas[i].x > 15.0001f || Mathf.Abs(atlas[i].x-Mathf.Round(atlas[i].x)) > .0001f)
                { errors.Add("UV2 atlas tile must be a finite integer from 0 to 15."); break; }
            }
            if (measured.teamVertices == 0 || measured.neutralVertices == 0) errors.Add("Every LOD requires both team-masked and neutral surfaces.");
            var indices = mesh.triangles; var used = new HashSet<int>();
            if (indices.Length%3 != 0) errors.Add("Triangle index count is not divisible by three.");
            for (int i=0;i<indices.Length;i++)
            {
                if (indices[i] < 0 || indices[i] >= vertices.Length) { errors.Add("Triangle references a nonexistent vertex."); return; }
                used.Add(indices[i]);
                if (i%3 == 2 && Vector3.Cross(vertices[indices[i-1]]-vertices[indices[i-2]],vertices[indices[i]]-vertices[indices[i-2]]).sqrMagnitude <= 1e-12f) measured.degenerateTriangles++;
            }
            if (used.Count != vertices.Length) errors.Add("Mesh has unreferenced or stale vertices.");
            if (!Finite(mesh.bounds.center) || !Finite(mesh.bounds.extents) || !Finite(renderer.localBounds.center) || !Finite(renderer.localBounds.extents)) errors.Add("Mesh or renderer bounds are non-finite.");
            var bones = renderer.bones; var bindposes = mesh.bindposes; var weights = mesh.boneWeights;
            measured.bones = bones.Length; measured.bindposes = bindposes.Length;
            int expectedBones = kind == ArtUnitKind.Cavalry ? 28 : kind == ArtUnitKind.Dragon ? 24 : 16;
            if (bones.Length != expectedBones || bindposes.Length != bones.Length || renderer.rootBone == null || bones.Length == 0 || renderer.rootBone != bones[0]) errors.Add("Rig bone count/root/bindposes do not match the authored body family.");
            for (int i=0;i<bindposes.Length;i++)
            {
                if (i >= bones.Length || bones[i] == null || !bones[i].IsChildOf(prefab.transform)) { errors.Add("Skeleton bone is null or outside its prefab."); continue; }
                var rest = renderer.transform.worldToLocalMatrix*bones[i].localToWorldMatrix*bindposes[i];
                for (int row=0;row<4;row++) for (int column=0;column<4;column++)
                {
                    if (!Finite(bindposes[i][row,column]) || !Finite(rest[row,column])) { errors.Add("Non-finite bindpose or skeleton matrix."); return; }
                    measured.maxBindposeError = Mathf.Max(measured.maxBindposeError,Mathf.Abs(rest[row,column]-(row==column?1:0)));
                }
            }
            if (measured.maxBindposeError > .005f) errors.Add("Bindposes do not reproduce the authored rest skeleton.");
            if (weights.Length != vertices.Length) { errors.Add("Every vertex requires normalized bone weights."); return; }
            foreach (var weight in weights)
            {
                float total = 0;
                for (int slot=0;slot<4;slot++)
                {
                    float value = Weight(weight,slot); int index = Bone(weight,slot);
                    if (!Finite(value) || value < 0 || value > 1.0001f || value > 0 && (index < 0 || index >= bones.Length)) { errors.Add("Skin weights contain invalid values or bone indices."); return; }
                    total += value;
                }
                measured.maxWeightError = Mathf.Max(measured.maxWeightError,Mathf.Abs(total-1));
            }
            if (measured.maxWeightError > .001f) errors.Add("Skin weights are not normalized.");
        }

        private static void ValidateClips(Animator animator,PrefabReport report)
        {
            if (!EditorUtility.IsPersistent(animator.runtimeAnimatorController) || !EditorUtility.IsPersistent(animator.avatar)) report.errors.Add("Animator controller/avatar must be persistent assets.");
            var clips = animator.runtimeAnimatorController.animationClips; var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var clip in clips)
            {
                if (clip == null || !EditorUtility.IsPersistent(clip)) { report.errors.Add("Missing or nonpersistent animation clip."); continue; }
                if (!names.Add(clip.name)) report.errors.Add("Duplicate animation clip name: "+clip.name);
                if (Array.IndexOf(States,clip.name) < 0 || clip.length <= 0 || !AnimationUtility.GetAnimationClipSettings(clip).loopTime) report.errors.Add("Expected nonempty looping Idle/Walk/Action clips only.");
                foreach (var binding in AnimationUtility.GetCurveBindings(clip))
                    if (binding.type == typeof(Transform) && string.IsNullOrEmpty(binding.path)) report.errors.Add(clip.name+" animates the prefab root transform.");
            }
            if (clips.Length != 3 || names.Count != 3) report.errors.Add("Exactly three animation clips are required.");
            foreach (string required in States) if (!names.Contains(required)) report.errors.Add("Missing animation clip: "+required);
            var controller = animator.runtimeAnimatorController as AnimatorController;
            if (controller == null || controller.layers.Length != 1) { report.errors.Add("Expected one-layer AnimatorController."); return; }
            var machine = controller.layers[0].stateMachine; var stateNames = new HashSet<string>(StringComparer.Ordinal);
            foreach (var state in machine.states)
            {
                stateNames.Add(state.state.name);
                if (!(state.state.motion is AnimationClip clip) || clip.name != state.state.name) report.errors.Add("Animator state does not reference its matching clip.");
            }
            if (machine.states.Length != 3 || machine.defaultState == null || machine.defaultState.name != "Idle") report.errors.Add("Animator must have three states and start in Idle.");
            foreach (string name in States) if (!stateNames.Contains(name)) report.errors.Add("Missing Animator state: "+name);
        }

        private static void SamplePrefab(string path,PrefabReport report)
        {
            GameObject instance = null; var baked = new Mesh();
            try
            {
                instance = PrefabUtility.LoadPrefabContents(path);
                var animator = instance.GetComponent<Animator>(); animator.enabled = false;
                var group = instance.GetComponent<LODGroup>(); group.ForceLOD(0);
                var transforms = instance.GetComponentsInChildren<Transform>(true);
                var positions = new Vector3[transforms.Length]; var rotations = new Quaternion[transforms.Length]; var scales = new Vector3[transforms.Length];
                for (int i=0;i<transforms.Length;i++) { positions[i] = transforms[i].localPosition; rotations[i] = transforms[i].localRotation; scales[i] = transforms[i].localScale; }
                var lods = group.GetLODs();
                for (int level=0;level<lods.Length;level++)
                {
                    var renderer = (SkinnedMeshRenderer)lods[level].renderers[0]; renderer.BakeMesh(baked);
                    var actual = baked.vertices; var authored = renderer.sharedMesh.vertices;
                    if (actual.Length != authored.Length) { report.errors.Add("Baked rest vertex count differs at LOD "+level); continue; }
                    for (int i=0;i<actual.Length;i++)
                    {
                        if (!Finite(actual[i])) { report.errors.Add("Non-finite baked rest geometry."); break; }
                        report.lods[level].maxBakedRestError = Mathf.Max(report.lods[level].maxBakedRestError,Vector3.Distance(actual[i],authored[i]));
                    }
                    if (report.lods[level].maxBakedRestError > .005f) report.errors.Add("BakeMesh rest pose changes authored vertices at LOD "+level);
                }
                var high = (SkinnedMeshRenderer)lods[0].renderers[0]; var bones = high.bones; var weighted = new HashSet<int>();
                foreach (var weight in high.sharedMesh.boneWeights) for (int slot=0;slot<4;slot++) if (Weight(weight,slot) > .00001f) weighted.Add(Bone(weight,slot));
                foreach (var clip in animator.runtimeAnimatorController.animationClips)
                {
                    Restore(); clip.SampleAnimation(instance,0); high.BakeMesh(baked); var initial = baked.vertices;
                    var initialBonePositions = new Vector3[bones.Length]; var initialBoneRotations = new Quaternion[bones.Length];
                    for (int i=0;i<bones.Length;i++) { initialBonePositions[i] = bones[i].localPosition; initialBoneRotations[i] = bones[i].localRotation; }
                    var measured = new AnimationReport { name=clip.name,path=AssetDatabase.GetAssetPath(clip),length=clip.length,loop=AnimationUtility.GetAnimationClipSettings(clip).loopTime }; report.animations.Add(measured);
                    foreach (float fraction in new[] { .17f,.39f,.67f,.83f })
                    {
                        Restore(); clip.SampleAnimation(instance,clip.length*fraction); high.BakeMesh(baked); var moved = baked.vertices; measured.sampledPoses++;
                        if (moved.Length != initial.Length) { report.errors.Add(clip.name+" changed vertex count during sampling."); continue; }
                        for (int i=0;i<moved.Length;i++)
                        {
                            if (!Finite(moved[i])) { report.errors.Add(clip.name+" produces non-finite geometry."); break; }
                            measured.maxVertexMovement = Mathf.Max(measured.maxVertexMovement,Vector3.Distance(initial[i],moved[i]));
                        }
                        for (int i=1;i<bones.Length;i++) if (weighted.Contains(i))
                        {
                            measured.maxWeightedBonePositionChange = Mathf.Max(measured.maxWeightedBonePositionChange,Vector3.Distance(initialBonePositions[i],bones[i].localPosition));
                            measured.maxWeightedBoneRotationDegrees = Mathf.Max(measured.maxWeightedBoneRotationDegrees,Quaternion.Angle(initialBoneRotations[i],bones[i].localRotation));
                        }
                        measured.maxRootDrift = Mathf.Max(measured.maxRootDrift,Vector3.Distance(positions[0],instance.transform.localPosition));
                        measured.maxRootRotationDegrees = Mathf.Max(measured.maxRootRotationDegrees,Quaternion.Angle(rotations[0],instance.transform.localRotation));
                        measured.maxSkeletonRootDrift = Mathf.Max(measured.maxSkeletonRootDrift,Vector3.Distance(initialBonePositions[0],bones[0].localPosition));
                        if ((instance.transform.localScale-scales[0]).sqrMagnitude > .000001f || Quaternion.Angle(initialBoneRotations[0],bones[0].localRotation) > .01f)
                            report.errors.Add(clip.name+" changes root scale or rotates the entire skeleton root.");
                    }
                    measured.geometryMoves = measured.maxVertexMovement > .0001f;
                    measured.weightedBonesMove = measured.maxWeightedBonePositionChange > .00005f || measured.maxWeightedBoneRotationDegrees > .03f;
                    measured.rootStable = measured.maxRootDrift < .0001f && measured.maxRootRotationDegrees < .01f && measured.maxSkeletonRootDrift < .0001f;
                    measured.passed = measured.geometryMoves && measured.weightedBonesMove && measured.rootStable;
                    if (!measured.passed) report.errors.Add(clip.name+" does not deform weighted geometry independently of stable roots.");
                }
                Restore(); CheckAppearance(instance,report);
                void Restore()
                {
                    for (int i=0;i<transforms.Length;i++) { transforms[i].localPosition = positions[i]; transforms[i].localRotation = rotations[i]; transforms[i].localScale = scales[i]; }
                }
            }
            finally { Object.DestroyImmediate(baked); if (instance != null) PrefabUtility.UnloadPrefabContents(instance); }
        }

        private static void CheckAppearance(GameObject instance,PrefabReport report)
        {
            var unit = instance.GetComponent<ArtOverhaulUnit>(); var kind = unit.Kind; var biome = unit.Biome; var original = unit.Team;
            var renderers = instance.GetComponentsInChildren<SkinnedMeshRenderer>(true); var meshes = new Mesh[renderers.Length]; var materials = new Material[renderers.Length];
            for (int i=0;i<renderers.Length;i++) { meshes[i] = renderers[i].sharedMesh; materials[i] = renderers[i].sharedMaterial; }
            var properties = new MaterialPropertyBlock();
            foreach (var color in new[] { new Color(.22f,.39f,.62f),new Color(.62f,.18f,.12f),new Color(.19f,.43f,.25f),new Color(.79f,.54f,.17f) })
            {
                unit.SetTeam(color); report.teamChecks++;
                for (int i=0;i<renderers.Length;i++)
                {
                    renderers[i].GetPropertyBlock(properties); var actual = properties.GetColor("_TeamColor");
                    if (Mathf.Abs(actual.r-color.r)>.0001f || Mathf.Abs(actual.g-color.g)>.0001f || Mathf.Abs(actual.b-color.b)>.0001f || Mathf.Abs(actual.a-color.a)>.0001f || renderers[i].sharedMesh != meshes[i] || renderers[i].sharedMaterial != materials[i])
                    { report.errors.Add("Team appearance changes shared assets or loses its MPB color."); break; }
                }
                if (unit.Kind != kind || unit.Biome != biome) report.errors.Add("Changing appearance changes visual identity.");
            }
            unit.SetTeam(original);
        }

        private static SceneReport ValidateScene(ArtBiome biome,Material unitMaterial)
        {
            var report = new SceneReport { biome=biome.ToString(),path="Assets/Game/Scenes/ArtOverhaul_"+biome+".unity" };
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(report.path) == null) { report.errors.Add("Missing persistent scene."); return report; }
            Scene scene = SceneManager.GetSceneByPath(report.path); bool opened = false;
            try
            {
                if (!scene.IsValid() || !scene.isLoaded) { scene = EditorSceneManager.OpenScene(report.path,OpenSceneMode.Additive); opened = true; }
                var controllers = new List<ArtOverhaulController>(); var units = new List<ArtOverhaulUnit>(); var renderers = new List<Renderer>();
                foreach (var root in scene.GetRootGameObjects())
                { controllers.AddRange(root.GetComponentsInChildren<ArtOverhaulController>(true)); units.AddRange(root.GetComponentsInChildren<ArtOverhaulUnit>(true)); renderers.AddRange(root.GetComponentsInChildren<Renderer>(true)); }
                report.units = units.Count; report.renderers = renderers.Count;
                if (controllers.Count != 1) report.errors.Add("Expected one art-scene controller.");
                if (units.Count < 3 || units.Count != Cast[(int)biome].Length) report.errors.Add("Representative unit count does not match the authored cast.");
                if (controllers.Count == 1)
                {
                    var controller = controllers[0]; report.pipeline = AssetDatabase.GetAssetPath(controller.Pipeline);
                    if (controller.Biome != biome || controller.Units == null || controller.Units.Length != units.Count) report.errors.Add("Controller biome/unit references are invalid.");
                    if (controller.Pipeline == null || !EditorUtility.IsPersistent(controller.Pipeline) || !report.pipeline.StartsWith(Root+"/Rendering/",StringComparison.Ordinal)) report.errors.Add("Scene must reference its isolated persistent URP pipeline.");
                    var cameras = new HashSet<Camera>();
                    foreach (var camera in new[] { controller.CloseCamera,controller.MidCamera,controller.RTSCamera })
                    {
                        if (camera == null || camera.gameObject.scene != scene || !camera.orthographic || !Finite(camera.orthographicSize) || camera.orthographicSize <= 0 || !Finite(camera.transform.position)) report.errors.Add("Missing or invalid scene presentation camera.");
                        else cameras.Add(camera);
                    }
                    report.cameras = cameras.Count; if (cameras.Count != 3) report.errors.Add("Three distinct presentation cameras are required.");
                    var referenced = new HashSet<ArtOverhaulUnit>();
                    if (controller.Units != null) foreach (var unit in controller.Units)
                        if (unit == null || unit.gameObject.scene != scene || !referenced.Add(unit)) report.errors.Add("Controller has missing, external or duplicate unit references.");
                }
                var kinds = new HashSet<ArtUnitKind>();
                foreach (var unit in units)
                {
                    kinds.Add(unit.Kind);
                    string expectedPath = Root+"/Prefabs/"+biome+"_"+unit.Kind+".prefab";
                    var source = PrefabUtility.GetCorrespondingObjectFromSource(unit.gameObject);
                    if (unit.Biome != biome || source == null || AssetDatabase.GetAssetPath(source) != expectedPath) report.errors.Add("Scene unit does not reference its matching persistent prefab.");
                    foreach (var renderer in unit.GetComponentsInChildren<SkinnedMeshRenderer>(true)) if (renderer.sharedMaterial != unitMaterial) report.errors.Add("Scene unit overrides the shared material.");
                }
                foreach (var kind in Cast[(int)biome]) if (!kinds.Contains(kind)) report.errors.Add("Scene is missing its "+kind+" representative.");
                var materials = new HashSet<Material>(); var meshes = new HashSet<Mesh>();
                foreach (var renderer in renderers)
                {
                    foreach (var material in renderer.sharedMaterials)
                    {
                        if (material == null) { report.errors.Add("Scene renderer has a missing material."); continue; }
                        if (materials.Add(material) && (!EditorUtility.IsPersistent(material) || material.shader == null || ShaderUtil.ShaderHasError(material.shader))) report.errors.Add("Scene material/shader is missing, transient or has compile errors: "+material.name);
                    }
                    var mesh = renderer is SkinnedMeshRenderer skin ? skin.sharedMesh : renderer.GetComponent<MeshFilter>()?.sharedMesh;
                    if (mesh == null && (renderer is SkinnedMeshRenderer || renderer is MeshRenderer)) report.errors.Add("Scene renderer has no mesh: "+renderer.name);
                    if (mesh != null && meshes.Add(mesh))
                    {
                        report.referencedMeshTriangles += (long)mesh.triangles.Length/3;
                        if (!EditorUtility.IsPersistent(mesh)) report.errors.Add("Scene contains a transient mesh: "+mesh.name);
                    }
                }
                report.uniqueMaterials = materials.Count; report.uniqueMeshes = meshes.Count;
            }
            catch (Exception error) { report.errors.Add(error.GetType().Name+": "+error.Message); }
            finally { if (opened && scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene,true); }
            report.passed = report.errors.Count == 0; return report;
        }

        private static float Weight(BoneWeight weight,int index) => index==0?weight.weight0:index==1?weight.weight1:index==2?weight.weight2:weight.weight3;
        private static int Bone(BoneWeight weight,int index) => index==0?weight.boneIndex0:index==1?weight.boneIndex1:index==2?weight.boneIndex2:weight.boneIndex3;
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static bool Finite(Vector2 value) => Finite(value.x) && Finite(value.y);
        private static bool Finite(Vector3 value) => Finite(value.x) && Finite(value.y) && Finite(value.z);
    }
}
