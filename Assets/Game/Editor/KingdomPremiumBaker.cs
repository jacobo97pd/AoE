using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Emberfield.Presentation;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace Emberfield.Editor
{
    /// <summary>Imports authored Blender assets into an isolated PC art scene and captures actual URP output.</summary>
    public static class KingdomPremiumBaker
    {
        public const string Root = "Assets/Game/KingdomPremium";
        public const string Review = "Artifacts/ArtReview/kingdom-premium";
        public const string ScenePath = "Assets/Game/Scenes/KingdomPremium.unity";

        [Serializable] public class SurfaceSpec
        {
            public string name,baseColorTexture,normalTexture,roughnessTexture,metallicTexture,opacityTexture,occlusionTexture,texture,colorSpace;
            public float[] color,baseColor,tiling;
            public float metallic,smoothness=.35f;
            public bool cutout,doubleSided,textureMultipliesBaseColor;
        }
        [Serializable] public class SurfaceManifest { public SurfaceSpec[] materials; }
        [Serializable] private class MeshEvidence
        {
            public string name,path; public int vertices,triangles,materials; public bool normals,uv;
        }
        [Serializable] private class Evidence
        {
            public string generatedUtc,unity,scene;
            public string scope="Native static 3D character and environment inspection. Asset counts are not frame timings; this report does not certify reference identity or artistic acceptance.";
            public int units,renderers,materials; public bool passed;
            public List<string> errors=new List<string>();
            public List<MeshEvidence> meshes=new List<MeshEvidence>();
        }

        public static void Run()
        {
            if(!Application.isBatchMode)throw new InvalidOperationException("Use tools/Build-KingdomPremium.ps1 to preserve an open editor session.");
            var previousQuality=QualitySettings.renderPipeline;var previousDefault=GraphicsSettings.defaultRenderPipeline;
            string qualityPath=AssetDatabase.GetAssetPath(previousQuality),defaultPath=AssetDatabase.GetAssetPath(previousDefault);
            int previousAA=QualitySettings.antiAliasing,exit=0;
            try
            {
                foreach(string folder in new[]{Root+"/Materials",Root+"/Rendering",Root+"/Prefabs",Review})Directory.CreateDirectory(folder);
                AssetDatabase.Refresh();
                bool playerOnly=Environment.GetCommandLineArgs().Contains("-premiumPlayerOnly");
                bool sceneOnly=Environment.GetCommandLineArgs().Contains("-premiumSceneOnly");
                var pipeline=playerOnly?AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(Root+"/Rendering/KingdomPipeline.asset"):MakePipeline();
                if(pipeline==null)throw new InvalidOperationException("Bake the scene before using -PlayerOnly.");
                QualitySettings.renderPipeline=pipeline;GraphicsSettings.defaultRenderPipeline=pipeline;QualitySettings.antiAliasing=4;
                if(!playerOnly)
                {
                    if(!sceneOnly)ImportModels();
                    var surfaces=sceneOnly?Directory.GetFiles(Root+"/Materials","*.mat").ToDictionary(p=>Path.GetFileNameWithoutExtension(p),p=>AssetDatabase.LoadAssetAtPath<Material>(p.Replace('\\','/')),StringComparer.OrdinalIgnoreCase):LoadSurfaces();
                    BuildScene(pipeline,surfaces);
                    AssetDatabase.SaveAssets();
                }
                if(!File.Exists(ScenePath))throw new FileNotFoundException("The premium Kingdom scene has not been generated.");
                EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
                var controller=Object.FindFirstObjectByType<KingdomPremiumReview>();
                Validate(controller);
                if(!playerOnly)CaptureViews(controller);
                if(Environment.GetCommandLineArgs().Contains("-premiumPlayer"))
                {
                    var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{ScenePath},locationPathName="Builds/KingdomPremium/KingdomPremium.exe",target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development});
                    File.WriteAllText(Review+"/build.txt",$"Result: {report.summary.result}\nErrors: {report.summary.totalErrors}\nWarnings: {report.summary.totalWarnings}\nBytes: {report.summary.totalSize}\nDuration: {report.summary.totalTime}\nScene: {ScenePath}\n");
                    if(report.summary.result!=BuildResult.Succeeded)throw new InvalidOperationException("Kingdom premium player build failed.");
                }
                Debug.Log("KINGDOM_PREMIUM_OK");
            }
            catch(Exception error){Debug.LogException(error);exit=1;}
            finally
            {
                // A player build may unload the old native assets. Reload their paths instead of restoring fake-null references.
                QualitySettings.renderPipeline=string.IsNullOrEmpty(qualityPath)?previousQuality:AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(qualityPath);
                GraphicsSettings.defaultRenderPipeline=string.IsNullOrEmpty(defaultPath)?previousDefault:AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(defaultPath);
                QualitySettings.antiAliasing=previousAA;AssetDatabase.SaveAssets();
            }
            if(exit!=0)EditorApplication.Exit(exit);
        }

        private static UniversalRenderPipelineAsset MakePipeline()
        {
            string rendererPath=Root+"/Rendering/KingdomRenderer.asset";
            var renderer=AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rendererPath);
            if(renderer==null)
            {
                renderer=Object.Instantiate(AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Settings/PC_Renderer.asset"));
                renderer.name="Kingdom PC renderer";renderer.rendererFeatures.Clear();AssetDatabase.CreateAsset(renderer,rendererPath);
            }
            var ao=renderer.rendererFeatures.OfType<ScreenSpaceAmbientOcclusion>().FirstOrDefault();
            if(ao==null){ao=ScriptableObject.CreateInstance<ScreenSpaceAmbientOcclusion>();ao.name="Contact shadows";AssetDatabase.AddObjectToAsset(ao,renderer);renderer.rendererFeatures.Add(ao);}
            var settings=new SerializedObject(ao);
            settings.FindProperty("m_Settings.AOMethod").intValue=1;
            settings.FindProperty("m_Settings.Source").intValue=1;
            settings.FindProperty("m_Settings.Downsample").boolValue=false;
            settings.FindProperty("m_Settings.Samples").intValue=0;
            settings.FindProperty("m_Settings.Intensity").floatValue=.55f;
            settings.FindProperty("m_Settings.DirectLightingStrength").floatValue=.08f;
            settings.FindProperty("m_Settings.Radius").floatValue=.12f;
            settings.FindProperty("m_Settings.Falloff").floatValue=80;
            settings.ApplyModifiedPropertiesWithoutUndo();ao.SetActive(true);ao.Create();renderer.SetDirty();EditorUtility.SetDirty(renderer);EditorUtility.SetDirty(ao);
            var pipeline=Object.Instantiate(AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/PC_RPAsset.asset"));pipeline.name="Kingdom PC art pipeline";
            var serialized=new SerializedObject(pipeline);
            serialized.FindProperty("m_RendererDataList").GetArrayElementAtIndex(0).objectReferenceValue=renderer;
            serialized.FindProperty("m_MSAA").intValue=4;
            serialized.FindProperty("m_MainLightShadowmapResolution").intValue=4096;
            serialized.FindProperty("m_ShadowDistance").floatValue=80;
            serialized.FindProperty("m_ShadowCascadeCount").intValue=4;
            serialized.FindProperty("m_UseSRPBatcher").boolValue=true;
            serialized.ApplyModifiedPropertiesWithoutUndo();return Save(pipeline,Root+"/Rendering/KingdomPipeline.asset");
        }

        private static void ImportModels()
        {
            foreach(string path in Directory.GetFiles(Root,"*.fbx",SearchOption.AllDirectories))
            {
                string asset=path.Replace('\\','/');AssetDatabase.ImportAsset(asset,ImportAssetOptions.ForceSynchronousImport);
                var importer=(ModelImporter)AssetImporter.GetAtPath(asset);
                importer.globalScale=1;importer.useFileScale=true;importer.importAnimation=false;
                importer.importCameras=false;importer.importLights=false;importer.importNormals=ModelImporterNormals.Import;
                importer.importTangents=ModelImporterTangents.CalculateMikk;importer.meshCompression=ModelImporterMeshCompression.Off;
                importer.isReadable=true;importer.materialImportMode=ModelImporterMaterialImportMode.ImportStandard;
                importer.SaveAndReimport();
            }
        }

        private static Dictionary<string,Material> LoadSurfaces()
        {
            var result=new Dictionary<string,Material>(StringComparer.OrdinalIgnoreCase);
            foreach(string path in Directory.GetFiles(Root,"*.json",SearchOption.AllDirectories))
            {
                SurfaceManifest manifest;
                try{manifest=JsonUtility.FromJson<SurfaceManifest>(File.ReadAllText(path));}catch{continue;}
                if(manifest?.materials==null)continue;
                string folder=Path.GetDirectoryName(path).Replace('\\','/');
                string scope=folder.EndsWith("/Characters",StringComparison.OrdinalIgnoreCase)?Path.GetFileNameWithoutExtension(path)+"_":"";
                foreach(var item in manifest.materials)
                {
                    if(string.IsNullOrWhiteSpace(item.name))continue;
                    result[scope+item.name]=MakeMaterial(item,folder,scope);
                }
            }
            return result;
        }

        private static Material MakeMaterial(SurfaceSpec item,string folder,string scope="")
        {
            var material=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=item.name,enableInstancing=true};
            var values=item.color??item.baseColor;
            Color tint=values!=null&&values.Length>=3?new Color(values[0],values[1],values[2],values.Length>3?values[3]:1):Color.white;
            // Blender Principled values are linear. Unity's Lit color property accepts an sRGB color.
            if(item.baseColor!=null||string.Equals(item.colorSpace,"linear",StringComparison.OrdinalIgnoreCase))tint=tint.gamma;
            if(string.IsNullOrEmpty(item.baseColorTexture)&&!string.IsNullOrEmpty(item.texture)){item.baseColorTexture=item.texture;if(!item.textureMultipliesBaseColor)tint=Color.white;}
            material.SetColor("_BaseColor",tint);material.SetFloat("_Metallic",item.metallic);material.SetFloat("_Smoothness",item.smoothness);
            var diffuse=Texture(Resolve(folder,item.baseColorTexture),false,true);
            if(diffuse!=null)material.SetTexture("_BaseMap",diffuse);
            var normal=Texture(Resolve(folder,item.normalTexture),true,false);
            if(normal!=null){material.SetTexture("_BumpMap",normal);material.SetFloat("_BumpScale",.75f);material.EnableKeyword("_NORMALMAP");}
            var roughness=Texture(Resolve(folder,item.roughnessTexture),false,false,true);
            var metallic=Texture(Resolve(folder,item.metallicTexture),false,false,true);
            if(roughness!=null||metallic!=null)
            {
                material.SetTexture("_MetallicGlossMap",PackMetalSmooth(item.name,roughness,metallic,item.metallic,item.smoothness));
                material.SetFloat("_Smoothness",1);material.EnableKeyword("_METALLICSPECGLOSSMAP");
            }
            var occlusion=Texture(Resolve(folder,item.occlusionTexture),false,false);
            if(occlusion!=null){material.SetTexture("_OcclusionMap",occlusion);material.EnableKeyword("_OCCLUSIONMAP");}
            if(item.tiling!=null&&item.tiling.Length>=2)material.SetTextureScale("_BaseMap",new Vector2(item.tiling[0],item.tiling[1]));
            if(item.cutout)
            {
                material.SetFloat("_AlphaClip",1);material.SetFloat("_Cutoff",.4f);material.EnableKeyword("_ALPHATEST_ON");
                material.SetOverrideTag("RenderType","TransparentCutout");material.renderQueue=(int)RenderQueue.AlphaTest;
                var opacity=Texture(Resolve(folder,item.opacityTexture),false,false,true);
                if(opacity!=null&&diffuse!=null)material.SetTexture("_BaseMap",PackAlpha(item.name,Texture(Resolve(folder,item.baseColorTexture),false,true,true),opacity));
            }
            material.SetFloat("_Cull",item.doubleSided?0:2);
            KingdomPremiumSurfaceDetail.Apply(material,item.name,Root+"/Textures/SurfaceDetail");
            return Save(material,Root+"/Materials/"+SafeName(scope+item.name)+".mat");
        }

        private static string Resolve(string folder,string path)
        {
            if(string.IsNullOrWhiteSpace(path))return null;
            return (path.StartsWith("Assets/")?path:folder+"/"+path).Replace('\\','/');
        }
        private static string SafeName(string name)=>string.Concat(name.Select(c=>char.IsLetterOrDigit(c)||c=='_'||c=='-'?c:'_'));

        private static Texture2D Texture(string path,bool normal,bool srgb,bool readable=false)
        {
            if(string.IsNullOrEmpty(path)||!File.Exists(path))return null;
            var importer=AssetImporter.GetAtPath(path) as TextureImporter;
            if(importer==null){AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);importer=AssetImporter.GetAtPath(path) as TextureImporter;}
            if(importer==null)return null;
            bool dirty=importer.textureType!=(normal?TextureImporterType.NormalMap:TextureImporterType.Default)||importer.sRGBTexture!=srgb||importer.isReadable!=readable||importer.anisoLevel!=8;
            if(dirty)
            {
                importer.textureType=normal?TextureImporterType.NormalMap:TextureImporterType.Default;importer.sRGBTexture=srgb;importer.isReadable=readable;
                importer.maxTextureSize=2048;importer.mipmapEnabled=true;importer.filterMode=FilterMode.Trilinear;importer.anisoLevel=8;importer.wrapMode=TextureWrapMode.Repeat;
                importer.textureCompression=TextureImporterCompression.CompressedHQ;importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static Texture2D PackMetalSmooth(string name,Texture2D roughness,Texture2D metallic,float metal,float smooth)
        {
            const int size=1024;var texture=new Texture2D(size,size,TextureFormat.RGBA32,true,true);var pixels=new Color32[size*size];
            for(int y=0;y<size;y++)for(int x=0;x<size;x++)
            {
                float u=(x+.5f)/size,v=(y+.5f)/size;
                float m=metallic!=null?metallic.GetPixelBilinear(u,v).r:metal;
                float s=roughness!=null?1-roughness.GetPixelBilinear(u,v).r:smooth;
                pixels[y*size+x]=new Color(m,0,0,Mathf.Clamp(s,.03f,.94f));
            }
            texture.SetPixels32(pixels);texture.Apply();string path=Root+"/Textures/"+SafeName(name)+"_MetalSmooth.png";
            File.WriteAllBytes(path,texture.EncodeToPNG());Object.DestroyImmediate(texture);AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);return Texture(path,false,false);
        }
        private static Texture2D PackAlpha(string name,Texture2D diffuse,Texture2D alpha)
        {
            int width=diffuse.width,height=diffuse.height;var texture=new Texture2D(width,height,TextureFormat.RGBA32,true,false);var pixels=new Color[width*height];
            for(int y=0;y<height;y++)for(int x=0;x<width;x++){float u=(x+.5f)/width,v=(y+.5f)/height;var c=diffuse.GetPixelBilinear(u,v);c.a=alpha.GetPixelBilinear(u,v).r;pixels[y*width+x]=c;}
            texture.SetPixels(pixels);texture.Apply();string path=Root+"/Textures/"+SafeName(name)+"_Cutout.png";File.WriteAllBytes(path,texture.EncodeToPNG());Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);return Texture(path,false,true);
        }

        private static Material FallbackMaterial(Material source,Dictionary<string,Material> materials)
        {
            string name=source==null?"Unassigned":source.name;
            if(materials.TryGetValue(name,out var existing))return existing;
            Color color=source!=null&&source.HasProperty("_Color")?source.color:Color.white;
            string key=name.ToLowerInvariant();float metal=0,smooth=.32f;
            if(key.Contains("steel")||key.Contains("iron")||key.Contains("chain")){metal=.92f;smooth=.53f;}
            if(key.Contains("gold")||key.Contains("brass")){metal=.9f;smooth=.57f;}
            if(key.Contains("skin")){smooth=.38f;}
            if(key.Contains("eye")||key.Contains("iris")){smooth=.65f;}
            if(key.Contains("leather")){smooth=.3f;}
            var spec=new SurfaceSpec{name=name,color=new[]{color.r,color.g,color.b,color.a},metallic=metal,smoothness=smooth};
            if(key.Contains("royalembroidery")){spec.baseColorTexture="Textures/RoyalEmbroidery.png";spec.color=new[]{1f,1f,1f,1f};spec.smoothness=.25f;}
            var material=MakeMaterial(spec,Root);materials[name]=material;return material;
        }

        private static GameObject InstantiateModel(string path,string label,Dictionary<string,Material> materials)
        {
            var asset=AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if(asset==null)throw new FileNotFoundException("Authored model is missing: "+path);
            var instance=(GameObject)PrefabUtility.InstantiatePrefab(asset);instance.name=label;
            string scope=path.Contains("/Characters/")?Path.GetFileNameWithoutExtension(path)+"_":"";
            foreach(var renderer in instance.GetComponentsInChildren<Renderer>(true))
            {
                renderer.sharedMaterials=renderer.sharedMaterials.Select(m=>m!=null&&materials.TryGetValue(scope+m.name,out var local)?local:FallbackMaterial(m,materials)).ToArray();
                renderer.shadowCastingMode=ShadowCastingMode.On;renderer.receiveShadows=true;
            }
            return instance;
        }

        private static void BuildScene(UniversalRenderPipelineAsset pipeline,Dictionary<string,Material> materials)
        {
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var environment=InstantiateModel(Root+"/Environment/Environment.fbx","Kingdom gatehouse and courtyard",materials);
            environment.transform.position=Vector3.zero;
            // The environment's Blender FBX convention maps its front to +Z; turn the complete authored layout once.
            environment.transform.rotation=Quaternion.Euler(0,180,0);
            FinishWoodland(environment,materials);
            var controller=new GameObject("Kingdom art review").AddComponent<KingdomPremiumReview>();controller.Pipeline=pipeline;
            controller.Units=new GameObject[3];
            string[] names={"Worker","Warrior","Hero"};
            Vector3[] positions={new Vector3(-2.2f,0,-3),new Vector3(0,0,-3),new Vector3(2.4f,0,-3)};
            for(int i=0;i<3;i++)
            {
                var unit=InstantiateModel(Root+"/Characters/"+names[i]+".fbx",names[i],materials);unit.transform.position=positions[i];unit.transform.rotation=Quaternion.Euler(0,180,0);controller.Units[i]=unit;
                SetupLods(unit);var bounds=BoundsOf(unit);
                var collider=unit.AddComponent<BoxCollider>();collider.center=unit.transform.InverseTransformPoint(bounds.center);collider.size=bounds.size;
                PrefabUtility.SaveAsPrefabAssetAndConnect(unit,Root+"/Prefabs/"+names[i]+".prefab",InteractionMode.AutomatedAction);
            }
            controller.CloseCamera=MakeCamera("Close",new Vector3(.8f,1.58f,-8.8f),new Vector3(0,1.15f,-3),false,36,0);
            controller.MidCamera=MakeCamera("Medium",new Vector3(2.8f,2.9f,-9.7f),new Vector3(0,1.2f,-3),false,43,0);
            controller.RTSCamera=MakeCamera("RTS",new Vector3(12,18,-20),new Vector3(0,1.75f,2),true,40,9.5f);
            controller.RTSCamera.tag="MainCamera";controller.gameObject.AddComponent<AudioListener>();
            Lighting();controller.SetView(2);
            EditorSceneManager.SaveScene(scene,ScenePath);
        }

        private static void FinishWoodland(GameObject environment,Dictionary<string,Material> materials)
        {
            foreach(var pair in materials)
            {
                if(pair.Key.EndsWith("_Steel",StringComparison.Ordinal))pair.Value.SetFloat("_Smoothness",.30f);
                else if(pair.Key.EndsWith("_SteelDark",StringComparison.Ordinal))pair.Value.SetFloat("_Smoothness",.24f);
                else if(pair.Key.EndsWith("_Gold",StringComparison.Ordinal))pair.Value.SetFloat("_Smoothness",.42f);
                else continue;
                EditorUtility.SetDirty(pair.Value);
            }
            foreach(string name in new[]{"ENV_TreeLeaf","ENV_Fern","ENV_Grass"})
            {
                if(!materials.TryGetValue(name,out var foliage))continue;
                foliage.SetFloat("_Smoothness",.28f);foliage.SetFloat("_BumpScale",.45f);EditorUtility.SetDirty(foliage);
                string texturePath=AssetDatabase.GetAssetPath(foliage.GetTexture("_BaseMap"));
                if(AssetImporter.GetAtPath(texturePath) is TextureImporter importer&&(!importer.alphaIsTransparency||!importer.mipMapsPreserveCoverage))
                {
                    importer.alphaIsTransparency=true;importer.mipMapsPreserveCoverage=true;importer.alphaTestReferenceValue=.4f;importer.SaveAndReimport();
                }
            }
            var ground=environment.GetComponentsInChildren<MeshFilter>().FirstOrDefault(m=>m.sharedMesh.name=="Courtyard_WoodlandSoil"||m.name.StartsWith("Ground woodland terrain",StringComparison.Ordinal));
            if(ground!=null)
            {
                var parent=new GameObject("Distant meadow continuation").transform;parent.SetParent(environment.transform,false);
                var extension=Object.Instantiate(ground.gameObject,parent);extension.name="Continuous horizon meadow";
                extension.transform.SetPositionAndRotation(ground.transform.position,ground.transform.rotation);extension.transform.localScale=ground.transform.lossyScale;
                var original=ground.GetComponent<Renderer>().sharedMaterial;var material=new Material(original){name="Distant meadow"};
                material.SetTextureScale("_BaseMap",original.GetTextureScale("_BaseMap")*12);extension.GetComponent<Renderer>().sharedMaterial=Save(material,Root+"/Materials/DistantMeadow.mat");
                extension.GetComponent<Renderer>().shadowCastingMode=ShadowCastingMode.Off;
                parent.localScale=new Vector3(12,1,12);parent.localPosition=new Vector3(0,-.2f,0);
            }
            var tree=environment.GetComponentsInChildren<MeshFilter>().FirstOrDefault(m=>m.name.Contains("optimized living tree"));
            if(tree==null)return;
            Vector3[] positions={new Vector3(-24,0,28),new Vector3(-18,0,23),new Vector3(-12,0,31),new Vector3(-6,0,25),new Vector3(7,0,29),new Vector3(14,0,24),new Vector3(21,0,32),new Vector3(28,0,26)};
            for(int i=0;i<positions.Length;i++)
            {
                var distant=Object.Instantiate(tree.gameObject,environment.transform);distant.name="Woodland background "+i;
                distant.transform.SetPositionAndRotation(positions[i],Quaternion.Euler(0,i*73,0)*tree.transform.rotation);distant.transform.localScale=tree.transform.lossyScale*(1.35f+.12f*(i%3));
                distant.GetComponent<Renderer>().shadowCastingMode=ShadowCastingMode.Off;
            }
        }

        private static void SetupLods(GameObject unit)
        {
            var levels=new List<LOD>();
            for(int level=0;level<3;level++)
            {
                string marker="_LOD"+level;var roots=unit.GetComponentsInChildren<Transform>(true).Where(t=>t.name.EndsWith(marker,StringComparison.OrdinalIgnoreCase)).ToArray();
                var renderers=roots.SelectMany(t=>t.GetComponentsInChildren<Renderer>(true)).Distinct().ToArray();
                if(renderers.Length>0)levels.Add(new LOD(new[]{.065f,.025f,.006f}[level],renderers));
            }
            if(levels.Count==0)return;
            var group=unit.GetComponent<LODGroup>()??unit.AddComponent<LODGroup>();group.SetLODs(levels.ToArray());group.RecalculateBounds();
        }
        private static Bounds BoundsOf(GameObject root)
        {
            var renderers=root.GetComponentsInChildren<Renderer>(true);var bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);return bounds;
        }
        private static Camera MakeCamera(string name,Vector3 position,Vector3 target,bool ortho,float fov,float size)
        {
            var camera=new GameObject(name+" camera",typeof(Camera)).GetComponent<Camera>();camera.transform.position=position;camera.transform.LookAt(target);
            camera.orthographic=ortho;camera.fieldOfView=fov;camera.orthographicSize=size;camera.nearClipPlane=.05f;camera.farClipPlane=250;camera.aspect=16f/9;camera.allowHDR=true;
            camera.clearFlags=CameraClearFlags.Skybox;camera.backgroundColor=new Color(.5f,.61f,.69f);
            var data=camera.GetUniversalAdditionalCameraData();data.renderPostProcessing=true;data.antialiasing=AntialiasingMode.SubpixelMorphologicalAntiAliasing;data.antialiasingQuality=AntialiasingQuality.High;
            return camera;
        }

        private static void Lighting()
        {
            string skyPath=Root+"/Textures/Daylight.hdr";
            var importer=(TextureImporter)AssetImporter.GetAtPath(skyPath);importer.textureShape=TextureImporterShape.TextureCube;importer.generateCubemap=TextureImporterGenerateCubemap.Spheremap;importer.sRGBTexture=false;importer.maxTextureSize=1024;importer.mipmapEnabled=true;importer.SaveAndReimport();
            var cube=AssetDatabase.LoadAssetAtPath<Cubemap>(skyPath);
            var sky=new Material(Shader.Find("Skybox/Cubemap")){name="Kingdom daylight sky"};sky.SetTexture("_Tex",cube);sky.SetFloat("_Exposure",1);sky.SetFloat("_Rotation",30);RenderSettings.skybox=Save(sky,Root+"/Materials/DaylightSky.mat");
            RenderSettings.defaultReflectionMode=DefaultReflectionMode.Custom;RenderSettings.customReflectionTexture=cube;RenderSettings.reflectionIntensity=1;
            RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=new Color(.55f,.64f,.76f);RenderSettings.ambientEquatorColor=new Color(.34f,.36f,.34f);RenderSettings.ambientGroundColor=new Color(.18f,.16f,.13f);
            RenderSettings.fog=true;RenderSettings.fogMode=FogMode.ExponentialSquared;RenderSettings.fogColor=new Color(.64f,.71f,.75f);RenderSettings.fogDensity=.003f;
            var sun=new GameObject("Warm courtyard sunlight",typeof(Light)).GetComponent<Light>();sun.type=LightType.Directional;sun.intensity=2.5f;sun.color=new Color(1,.90f,.77f);sun.transform.rotation=Quaternion.Euler(43,-32,0);sun.shadows=LightShadows.Soft;sun.shadowBias=.025f;sun.shadowNormalBias=.15f;sun.shadowStrength=.9f;RenderSettings.sun=sun;
            var fill=new GameObject("Soft sky fill",typeof(Light)).GetComponent<Light>();fill.type=LightType.Directional;fill.intensity=.25f;fill.color=new Color(.65f,.78f,1);fill.transform.rotation=Quaternion.Euler(22,135,0);fill.shadows=LightShadows.None;
            var profile=ScriptableObject.CreateInstance<VolumeProfile>();profile.name="Kingdom daylight grade";profile.Add<Tonemapping>(true).mode.Override(TonemappingMode.ACES);
            var adjustments=profile.Add<ColorAdjustments>(true);adjustments.postExposure.Override(.2f);adjustments.contrast.Override(6);adjustments.saturation.Override(3);
            var bloom=profile.Add<Bloom>(true);bloom.intensity.Override(.055f);bloom.threshold.Override(1.6f);profile.Add<Vignette>(true).intensity.Override(.10f);
            string path=Root+"/Rendering/KingdomAtmosphere.asset";var previous=AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if(previous!=null){foreach(var sub in AssetDatabase.LoadAllAssetsAtPath(path))if(sub!=previous)Object.DestroyImmediate(sub,true);EditorUtility.CopySerialized(profile,previous);Object.DestroyImmediate(profile);profile=previous;}else AssetDatabase.CreateAsset(profile,path);
            foreach(var component in profile.components)AssetDatabase.AddObjectToAsset(component,profile);EditorUtility.SetDirty(profile);
            var volume=new GameObject("Kingdom atmosphere",typeof(Volume)).GetComponent<Volume>();volume.isGlobal=true;volume.sharedProfile=profile;
        }

        private static void Validate(KingdomPremiumReview controller)
        {
            var evidence=new Evidence{generatedUtc=DateTime.UtcNow.ToString("O"),unity=Application.unityVersion,scene=ScenePath};
            if(controller==null||controller.Units==null||controller.Units.Length!=3)evidence.errors.Add("Three authored character instances are required.");
            evidence.units=controller?.Units?.Length??0;
            var renderers=Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);evidence.renderers=renderers.Length;evidence.materials=renderers.SelectMany(r=>r.sharedMaterials).Distinct().Count();
            foreach(var filter in Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None))
            {
                var mesh=filter.sharedMesh;if(mesh==null){evidence.errors.Add(filter.name+" has no mesh.");continue;}
                string path=AssetDatabase.GetAssetPath(mesh);
                if(evidence.meshes.Any(m=>m.path==path&&m.name==mesh.name))continue;
                var renderer=filter.GetComponent<Renderer>();
                evidence.meshes.Add(new MeshEvidence{name=mesh.name,path=path,vertices=mesh.vertexCount,triangles=mesh.triangles.Length/3,materials=renderer?.sharedMaterials.Length??0,normals=mesh.normals.Length==mesh.vertexCount,uv=mesh.uv.Length==mesh.vertexCount});
                if(string.IsNullOrEmpty(path))evidence.errors.Add(mesh.name+" is not persistent.");
                if(mesh.normals.Length!=mesh.vertexCount)evidence.errors.Add(mesh.name+" is missing authored normals.");
            }
            foreach(var material in renderers.SelectMany(r=>r.sharedMaterials).Distinct())
                if(material==null||material.shader==null||!material.shader.isSupported)evidence.errors.Add("A material or shader is unavailable.");
            evidence.passed=evidence.errors.Count==0;File.WriteAllText(Review+"/asset-validation.json",JsonUtility.ToJson(evidence,true));
            if(!evidence.passed)throw new InvalidOperationException(string.Join("; ",evidence.errors));
        }

        private static void CaptureViews(KingdomPremiumReview controller)
        {
            foreach(var group in Object.FindObjectsByType<LODGroup>(FindObjectsSortMode.None))group.ForceLOD(0);
            for(int view=0;view<3;view++){controller.SetView(view);Capture(controller.ActiveCamera,2560,1440,Review+"/"+new[]{"close","medium","rts"}[view]+".png");}
            for(int unit=0;unit<3;unit++){controller.CloseCamera.aspect=.75f;controller.SelectUnit(unit);controller.SetView(0);Capture(controller.ActiveCamera,1200,1600,Review+"/"+new[]{"worker","warrior","hero"}[unit]+".png");}
            foreach(var group in Object.FindObjectsByType<LODGroup>(FindObjectsSortMode.None))group.ForceLOD(-1);
            EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
        }
        private static void Capture(Camera camera,int width,int height,string path)
        {
            var target=new RenderTexture(width,height,24,RenderTextureFormat.ARGB32);target.Create();var pixels=new Texture2D(width,height,TextureFormat.RGB24,false);var previous=RenderTexture.active;var previousTarget=camera.targetTexture;float aspect=camera.aspect;
            try
            {
                camera.aspect=width/(float)height;camera.targetTexture=target;var request=new RenderPipeline.StandardRequest{destination=target};
                if(!RenderPipeline.SupportsRenderRequest(camera,request))throw new InvalidOperationException("URP render capture unavailable.");
                for(int i=0;i<3;i++)RenderPipeline.SubmitRenderRequest(camera,request);
                RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,width,height),0,0);pixels.Apply();File.WriteAllBytes(path,pixels.EncodeToPNG());
            }
            finally{camera.targetTexture=previousTarget;camera.aspect=aspect;RenderTexture.active=previous;target.Release();Object.DestroyImmediate(target);Object.DestroyImmediate(pixels);}
        }
        private static T Save<T>(T asset,string path)where T:Object
        {
            var existing=AssetDatabase.LoadAssetAtPath<T>(path);if(existing==null){AssetDatabase.CreateAsset(asset,path);return asset;}
            EditorUtility.CopySerialized(asset,existing);EditorUtility.SetDirty(existing);Object.DestroyImmediate(asset);return existing;
        }
    }
}
