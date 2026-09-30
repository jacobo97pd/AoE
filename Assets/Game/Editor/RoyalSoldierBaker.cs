using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Emberfield.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object=UnityEngine.Object;

namespace Emberfield.Editor
{
    /// <summary>One authored soldier, one compact presentation scene, real camera captures.</summary>
    public static class RoyalSoldierBaker
    {
        private const string Root="Assets/Game/RoyalSoldier", Review="Artifacts/ArtReview/royal-soldier", ScenePath="Assets/Game/Scenes/RoyalSoldier.unity";
        [Serializable] private class Surface { public string name,baseColorTexture,normalTexture,metallicSmoothnessTexture;public float[] tint,tiling;public float metallic,smoothness,normalScale=1,vertexColorStrength,smoothnessMultiplier=1; }
        [Serializable] private class Manifest { public Surface[] materials; }
        [Serializable] private class MeshInfo { public string name,path;public int vertices,triangles;public bool normals,uv,colors; }
        [Serializable] private class Evidence { public string generatedUtc,scene=ScenePath,unity;public bool passed;public int units=1;public List<MeshInfo> meshes=new List<MeshInfo>();public List<string> errors=new List<string>();public string scope="Persistent meshes/materials and shader checks. Visual quality is judged from the unretouched camera captures; this is one static posed soldier."; }
        public static void Run()
        {
            if(!Application.isBatchMode)throw new InvalidOperationException("Use tools/Build-RoyalSoldier.ps1.");
            string oldQuality=AssetDatabase.GetAssetPath(QualitySettings.renderPipeline),oldDefault=AssetDatabase.GetAssetPath(GraphicsSettings.defaultRenderPipeline);int oldAA=QualitySettings.antiAliasing,exit=0;
            try
            {
                foreach(string folder in new[]{Root+"/Materials",Root+"/Rendering",Root+"/Prefabs",Review})Directory.CreateDirectory(folder);
                AssetDatabase.Refresh();bool playerOnly=Environment.GetCommandLineArgs().Contains("-soldierPlayerOnly"),sceneOnly=Environment.GetCommandLineArgs().Contains("-soldierSceneOnly");
                var pipeline=playerOnly?AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(Root+"/Rendering/SoldierPipeline.asset"):MakePipeline();
                if(pipeline==null)throw new InvalidOperationException("Generate the soldier scene first.");
                QualitySettings.renderPipeline=pipeline;GraphicsSettings.defaultRenderPipeline=pipeline;QualitySettings.antiAliasing=4;
                if(!playerOnly)
                {
                    if(!sceneOnly)ImportModels();
                    var materials=sceneOnly?Directory.GetFiles(Root+"/Materials","*.mat").Select(p=>AssetDatabase.LoadAssetAtPath<Material>(p.Replace('\\','/'))).ToDictionary(m=>m.name):MakeMaterials();
                    BuildScene(materials,pipeline);AssetDatabase.SaveAssets();
                }
                EditorSceneManager.OpenScene(ScenePath);var viewer=Object.FindFirstObjectByType<RoyalSoldierReview>();Validate(viewer);
                if(!playerOnly)Capture(viewer);
                if(Environment.GetCommandLineArgs().Contains("-soldierPlayer"))
                {
                    var build=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{ScenePath},target=BuildTarget.StandaloneWindows64,locationPathName="Builds/RoyalSoldier/RoyalSoldier.exe",options=BuildOptions.Development});
                    File.WriteAllText(Review+"/build.txt",$"Result: {build.summary.result}\nErrors: {build.summary.totalErrors}\nWarnings: {build.summary.totalWarnings}\nBytes: {build.summary.totalSize}\nDuration: {build.summary.totalTime}\n");
                    File.AppendAllLines(Review+"/build.txt",build.steps.SelectMany(step=>step.messages).Where(message=>message.type==LogType.Warning||message.type==LogType.Error).Select(message=>message.type+": "+message.content));
                    if(build.summary.result!=BuildResult.Succeeded)throw new InvalidOperationException("Soldier player build failed.");
                }
                Debug.Log("ROYAL_SOLDIER_OK");
            }
            catch(Exception e){Debug.LogException(e);exit=1;}
            finally
            {
                QualitySettings.renderPipeline=string.IsNullOrEmpty(oldQuality)?null:AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(oldQuality);
                GraphicsSettings.defaultRenderPipeline=string.IsNullOrEmpty(oldDefault)?null:AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(oldDefault);
                QualitySettings.antiAliasing=oldAA;AssetDatabase.SaveAssets();
            }
            if(exit!=0)EditorApplication.Exit(exit);
        }
        private static T Save<T>(T value,string path) where T:Object
        {
            var old=AssetDatabase.LoadAssetAtPath<T>(path);if(old==null){AssetDatabase.CreateAsset(value,path);return value;}
            EditorUtility.CopySerialized(value,old);EditorUtility.SetDirty(old);Object.DestroyImmediate(value);return old;
        }
        private static UniversalRenderPipelineAsset MakePipeline()
        {
            string path=Root+"/Rendering/SoldierRenderer.asset";var renderer=AssetDatabase.LoadAssetAtPath<UniversalRendererData>(path);
            if(renderer==null){renderer=Object.Instantiate(AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Settings/PC_Renderer.asset"));renderer.name="Soldier forward renderer";renderer.rendererFeatures.Clear();AssetDatabase.CreateAsset(renderer,path);}
            var serializedRenderer=new SerializedObject(renderer);serializedRenderer.FindProperty("m_RenderingMode").intValue=0;serializedRenderer.ApplyModifiedPropertiesWithoutUndo();
            var ao=renderer.rendererFeatures.OfType<ScreenSpaceAmbientOcclusion>().FirstOrDefault();
            if(ao==null){ao=ScriptableObject.CreateInstance<ScreenSpaceAmbientOcclusion>();ao.name="Small scale contact";AssetDatabase.AddObjectToAsset(ao,renderer);renderer.rendererFeatures.Add(ao);}
            var settings=new SerializedObject(ao);settings.FindProperty("m_Settings.AOMethod").intValue=1;settings.FindProperty("m_Settings.Source").intValue=1;settings.FindProperty("m_Settings.Downsample").boolValue=false;settings.FindProperty("m_Settings.Samples").intValue=0;settings.FindProperty("m_Settings.Intensity").floatValue=.40f;settings.FindProperty("m_Settings.Radius").floatValue=.10f;settings.FindProperty("m_Settings.DirectLightingStrength").floatValue=.04f;settings.FindProperty("m_Settings.Falloff").floatValue=30;settings.ApplyModifiedPropertiesWithoutUndo();ao.SetActive(true);ao.Create();renderer.SetDirty();EditorUtility.SetDirty(renderer);EditorUtility.SetDirty(ao);
            var pipeline=Object.Instantiate(AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/PC_RPAsset.asset"));pipeline.name="SoldierPipeline";
            var data=new SerializedObject(pipeline);data.FindProperty("m_RendererDataList").GetArrayElementAtIndex(0).objectReferenceValue=renderer;data.FindProperty("m_MSAA").intValue=4;data.FindProperty("m_MainLightShadowmapResolution").intValue=4096;data.FindProperty("m_ShadowDistance").floatValue=25;data.FindProperty("m_ShadowCascadeCount").intValue=2;data.FindProperty("m_UseSRPBatcher").boolValue=true;data.ApplyModifiedPropertiesWithoutUndo();return Save(pipeline,Root+"/Rendering/SoldierPipeline.asset");
        }
        private static void ImportModels()
        {
            foreach(string file in Directory.GetFiles(Root,"*.fbx",SearchOption.AllDirectories))
            {
                string path=file.Replace('\\','/');AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);var importer=(ModelImporter)AssetImporter.GetAtPath(path);
                importer.importAnimation=false;importer.importCameras=false;importer.importLights=false;importer.globalScale=1;importer.useFileScale=true;importer.importNormals=ModelImporterNormals.Import;importer.importTangents=ModelImporterTangents.CalculateMikk;importer.meshCompression=ModelImporterMeshCompression.Off;importer.isReadable=true;importer.materialImportMode=ModelImporterMaterialImportMode.ImportStandard;importer.SaveAndReimport();
            }
        }
        private static Texture2D Map(string path,bool normal,bool srgb)
        {
            if(string.IsNullOrEmpty(path))return null;
            if(!File.Exists(path))throw new FileNotFoundException("Soldier material map missing",path);
            var importer=AssetImporter.GetAtPath(path) as TextureImporter;if(importer==null){AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);importer=(TextureImporter)AssetImporter.GetAtPath(path);}
            if(importer.textureType!=(normal?TextureImporterType.NormalMap:TextureImporterType.Default)||importer.sRGBTexture!=srgb||importer.anisoLevel!=8)
            {importer.textureType=normal?TextureImporterType.NormalMap:TextureImporterType.Default;importer.sRGBTexture=srgb;importer.convertToNormalmap=false;importer.maxTextureSize=2048;importer.mipmapEnabled=true;importer.filterMode=FilterMode.Trilinear;importer.wrapMode=TextureWrapMode.Repeat;importer.anisoLevel=8;importer.textureCompression=TextureImporterCompression.CompressedHQ;importer.SaveAndReimport();}
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
        private static Dictionary<string,Material> MakeMaterials()
        {
            var materials=new Dictionary<string,Material>();var manifest=JsonUtility.FromJson<Manifest>(File.ReadAllText(Root+"/material-manifest.json"));var shader=Shader.Find("RoyalSoldier/Surface");
            if(shader==null)throw new InvalidOperationException("Soldier surface shader not imported.");
            foreach(var spec in manifest.materials)
            {
                var mat=new Material(shader){name=spec.name,enableInstancing=true};var c=spec.tint;
                mat.SetColor("_BaseColor",new Color(c[0],c[1],c[2],c.Length>3?c[3]:1));mat.SetFloat("_VertexColorStrength",spec.vertexColorStrength);mat.SetFloat("_WorkflowMode",1);mat.SetFloat("_Surface",0);mat.SetFloat("_Cull",2);mat.SetFloat("_ZWrite",1);mat.SetFloat("_SrcBlend",1);mat.SetFloat("_DstBlend",0);mat.SetFloat("_Metallic",spec.metallic);mat.SetFloat("_Smoothness",spec.smoothness);
                mat.SetTexture("_BaseMap",Map(spec.baseColorTexture,false,true));
                if(!string.IsNullOrEmpty(spec.normalTexture)){mat.SetTexture("_BumpMap",Map(spec.normalTexture,true,false));mat.SetFloat("_BumpScale",spec.normalScale);mat.EnableKeyword("_NORMALMAP");}
                if(!string.IsNullOrEmpty(spec.metallicSmoothnessTexture)){mat.SetTexture("_MetallicGlossMap",Map(spec.metallicSmoothnessTexture,false,false));mat.SetFloat("_Smoothness",spec.smoothnessMultiplier);mat.EnableKeyword("_METALLICSPECGLOSSMAP");}
                if(spec.tiling!=null)mat.SetTextureScale("_BaseMap",new Vector2(spec.tiling[0],spec.tiling[1]));
                materials[spec.name]=Save(mat,Root+"/Materials/"+spec.name+".mat");
            }
            foreach(string key in new[]{"ENV_CutLimestone","ENV_CourtyardCobble","ENV_Oak","ENV_ForgedIron"})
            {
                var src=AssetDatabase.LoadAssetAtPath<Material>("Assets/Game/KingdomPremium/Materials/"+key+".mat");if(src==null)throw new FileNotFoundException(key);
                var mat=Object.Instantiate(src);mat.name=key;
                if(key=="ENV_CutLimestone"||key=="ENV_CourtyardCobble"){var color=mat.GetColor("_BaseColor");mat.SetColor("_BaseColor",new Color(color.r*.82f,color.g*.82f,color.b*.82f,color.a));}
                materials[key]=Save(mat,Root+"/Materials/"+key+".mat");
            }
            return materials;
        }
        private static GameObject Model(string path,string name,Dictionary<string,Material> materials)
        {
            var source=AssetDatabase.LoadAssetAtPath<GameObject>(path);if(source==null)throw new FileNotFoundException(path);
            var go=(GameObject)PrefabUtility.InstantiatePrefab(source);go.name=name;go.transform.rotation=Quaternion.Euler(0,180,0);
            foreach(var renderer in go.GetComponentsInChildren<Renderer>(true))
            {
                renderer.sharedMaterials=renderer.sharedMaterials.Select(m=>{string key=System.Text.RegularExpressions.Regex.Replace(m.name,@"\.\d{3}$","");if(!materials.TryGetValue(key,out var mat))throw new InvalidOperationException("Unmapped authored surface: "+key);return mat;}).ToArray();renderer.shadowCastingMode=ShadowCastingMode.On;renderer.receiveShadows=true;
            }
            return go;
        }
        private static void BuildScene(Dictionary<string,Material> materials,UniversalRenderPipelineAsset pipeline)
        {
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var soldier=Model(Root+"/Model/RoyalSoldier.fbx","Soldado del reino",materials);PrefabUtility.SaveAsPrefabAssetAndConnect(soldier,Root+"/Prefabs/RoyalSoldier.prefab",InteractionMode.AutomatedAction);
            Model(Root+"/Stage/Stage.fbx","Patio de guardia",materials);
            var viewer=new GameObject("Inspección del soldado").AddComponent<RoyalSoldierReview>();viewer.Soldier=soldier;viewer.Pipeline=pipeline;
            viewer.Cameras=new[]{Camera("Cercana",new Vector3(-2.0f,1.90f,-4.6f),new Vector3(0,1.05f,0),false,30,0),Camera("Media",new Vector3(-4.0f,2.7f,-6.2f),new Vector3(0,1.0f,0),false,30,0),Camera("RTS",new Vector3(-5,7,-8),new Vector3(0,.6f,0),true,40,3.0f)};
            viewer.Cameras[0].tag="MainCamera";viewer.gameObject.AddComponent<AudioListener>();Lighting();viewer.SetView(0);EditorSceneManager.SaveScene(scene,ScenePath);
        }
        private static Camera Camera(string name,Vector3 pos,Vector3 target,bool ortho,float fov,float size)
        {
            var c=new GameObject(name,typeof(Camera)).GetComponent<Camera>();c.transform.position=pos;c.transform.LookAt(target);c.orthographic=ortho;c.orthographicSize=size;c.fieldOfView=fov;c.nearClipPlane=.03f;c.farClipPlane=100;c.allowHDR=true;c.clearFlags=CameraClearFlags.SolidColor;c.backgroundColor=new Color(.32f,.35f,.38f);c.aspect=16f/9;
            var extra=c.GetUniversalAdditionalCameraData();extra.renderPostProcessing=true;extra.antialiasing=AntialiasingMode.SubpixelMorphologicalAntiAliasing;extra.antialiasingQuality=AntialiasingQuality.High;return c;
        }
        private static void Lighting()
        {
            const string original="Assets/Game/KingdomPremium/Textures/Daylight.hdr";string reflection=Root+"/Textures/SoldierReflection.hdr";
            if(!File.Exists(reflection)){File.Copy(original,reflection);AssetDatabase.ImportAsset(reflection,ImportAssetOptions.ForceSynchronousImport);}
            var hdrImporter=(TextureImporter)AssetImporter.GetAtPath(reflection);var hdrSettings=new TextureImporterSettings();hdrImporter.ReadTextureSettings(hdrSettings);
            if(hdrSettings.textureShape!=TextureImporterShape.TextureCube||hdrSettings.cubemapConvolution!=TextureImporterCubemapConvolution.Specular)
            {((TextureImporter)AssetImporter.GetAtPath(original)).ReadTextureSettings(hdrSettings);hdrSettings.textureShape=TextureImporterShape.TextureCube;hdrSettings.cubemapConvolution=TextureImporterCubemapConvolution.Specular;hdrSettings.mipmapEnabled=true;hdrImporter.SetTextureSettings(hdrSettings);hdrImporter.SaveAndReimport();}
            var hdr=AssetDatabase.LoadAssetAtPath<Cubemap>(reflection);RenderSettings.defaultReflectionMode=DefaultReflectionMode.Custom;RenderSettings.customReflectionTexture=hdr;RenderSettings.reflectionIntensity=.40f;
            RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=new Color(.48f,.56f,.66f);RenderSettings.ambientEquatorColor=new Color(.34f,.37f,.41f);RenderSettings.ambientGroundColor=new Color(.14f,.125f,.10f);RenderSettings.fog=false;
            var sun=new GameObject("Key - soft warm daylight",typeof(Light)).GetComponent<Light>();sun.type=LightType.Directional;sun.color=new Color(1,.92f,.82f);sun.intensity=1.55f;sun.transform.rotation=Quaternion.Euler(38,-35,0);sun.shadows=LightShadows.Soft;sun.shadowBias=.012f;sun.shadowNormalBias=.12f;sun.shadowStrength=.68f;RenderSettings.sun=sun;
            var fill=new GameObject("Fill - cool reflected daylight",typeof(Light)).GetComponent<Light>();fill.type=LightType.Directional;fill.color=new Color(.72f,.82f,1);fill.intensity=.55f;fill.transform.rotation=Quaternion.Euler(20,35,0);
            var rim=new GameObject("Rim - shield and cape edge",typeof(Light)).GetComponent<Light>();rim.type=LightType.Directional;rim.color=new Color(1,.86f,.67f);rim.intensity=.35f;rim.transform.rotation=Quaternion.Euler(35,160,0);
            var profile=ScriptableObject.CreateInstance<VolumeProfile>();profile.name="Soldier neutral presentation";profile.Add<Tonemapping>(true).mode.Override(TonemappingMode.ACES);var grade=profile.Add<ColorAdjustments>(true);grade.postExposure.Override(.15f);grade.contrast.Override(3);grade.saturation.Override(-2);profile.Add<Vignette>(true).intensity.Override(.07f);
            string path=Root+"/Rendering/SoldierGrade.asset";var existing=AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);if(existing!=null){foreach(var o in AssetDatabase.LoadAllAssetsAtPath(path))if(o!=existing)Object.DestroyImmediate(o,true);EditorUtility.CopySerialized(profile,existing);Object.DestroyImmediate(profile);profile=existing;}else AssetDatabase.CreateAsset(profile,path);
            foreach(var component in profile.components)AssetDatabase.AddObjectToAsset(component,profile);EditorUtility.SetDirty(profile);var volume=new GameObject("Presentation colour grade",typeof(Volume)).GetComponent<Volume>();volume.isGlobal=true;volume.sharedProfile=profile;
        }
        private static void Validate(RoyalSoldierReview viewer)
        {
            var e=new Evidence{generatedUtc=DateTime.UtcNow.ToString("O"),unity=Application.unityVersion};if(viewer==null||viewer.Soldier==null)throw new InvalidOperationException("Missing the single soldier.");
            foreach(var filter in viewer.Soldier.GetComponentsInChildren<MeshFilter>(true))
            {
                var mesh=filter.sharedMesh;string path=AssetDatabase.GetAssetPath(mesh);var info=new MeshInfo{name=mesh.name,path=path,vertices=mesh.vertexCount,triangles=mesh.triangles.Length/3,normals=mesh.normals.Length==mesh.vertexCount,uv=mesh.uv.Length==mesh.vertexCount,colors=mesh.colors.Length==mesh.vertexCount};e.meshes.Add(info);
                if(string.IsNullOrEmpty(path)||!info.normals||!info.uv)e.errors.Add("Incomplete mesh "+info.name);
            }
            foreach(var mat in viewer.Soldier.GetComponentsInChildren<Renderer>(true).SelectMany(r=>r.sharedMaterials).Distinct())
            {
                if(mat==null||mat.shader==null||!mat.shader.isSupported||string.IsNullOrEmpty(AssetDatabase.GetAssetPath(mat)))e.errors.Add("Invalid soldier material");
                else foreach(var msg in ShaderUtil.GetShaderMessages(mat.shader))if(msg.severity==UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error)e.errors.Add(msg.message);
            }
            e.passed=e.errors.Count==0;File.WriteAllText(Review+"/asset-validation.json",JsonUtility.ToJson(e,true));if(!e.passed)throw new InvalidOperationException(string.Join("; ",e.errors.Distinct()));
        }
        private static void Capture(RoyalSoldierReview viewer)
        {
            viewer.Controls=false;string[] names={"close","medium","rts"};for(int i=0;i<3;i++){viewer.SetView(i);RoyalSoldierReview.CaptureCamera(viewer.Active,i==0?1500:2560,i==0?1900:1440,Review+"/"+names[i]+".png");}
            var close=viewer.Cameras[0];close.transform.position=new Vector3(-.6f,1.9f,-1.9f);close.transform.LookAt(new Vector3(0,1.76f,0));close.fieldOfView=29;RoyalSoldierReview.CaptureCamera(close,1500,1500,Review+"/face.png");
            close.transform.position=new Vector3(-2.6f,3.0f,4.8f);close.transform.LookAt(new Vector3(0,1.05f,0));close.fieldOfView=30;RoyalSoldierReview.CaptureCamera(close,1500,1900,Review+"/back.png");
            EditorSceneManager.OpenScene(ScenePath);
        }
    }
}
