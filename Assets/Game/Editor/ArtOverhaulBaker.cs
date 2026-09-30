using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Emberfield.Presentation;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object=UnityEngine.Object;

namespace Emberfield.Editor
{
    /// <summary>Reproducible art-only scenes and separate review executable; no simulation data mutations.</summary>
    public static class ArtOverhaulBaker
    {
        public const string Root="Assets/Game/ArtOverhaul", Review="Artifacts/ArtReview/overhaul";
        private static readonly ArtUnitKind[][] Cast={
            new[]{ArtUnitKind.Worker,ArtUnitKind.Warrior,ArtUnitKind.Cavalry,ArtUnitKind.Hero,ArtUnitKind.MountainWarrior},
            new[]{ArtUnitKind.Worker,ArtUnitKind.Pirate,ArtUnitKind.Hero,ArtUnitKind.Warrior},
            new[]{ArtUnitKind.Worker,ArtUnitKind.Warrior,ArtUnitKind.Cavalry,ArtUnitKind.Hero},
            new[]{ArtUnitKind.Elf,ArtUnitKind.Dwarf,ArtUnitKind.Dragon,ArtUnitKind.Hero}};
        private static readonly string[] Labels={"Recolector","Guerrero","Jinete","Héroe","Pirata","Enano","Elfo","Guerrero de montaña","Dragón"};
        public static void Run()
        {
            if(!Application.isBatchMode)throw new InvalidOperationException("Use tools/Build-ArtOverhaul.ps1 to preserve open editor work.");
            var previousPipeline=QualitySettings.renderPipeline;var previousDefault=GraphicsSettings.defaultRenderPipeline;int previousAA=QualitySettings.antiAliasing;
            // BuildPipeline unloads unused native assets; managed locals alone do not retain them.
            string previousPipelinePath=AssetDatabase.GetAssetPath(previousPipeline),previousDefaultPath=AssetDatabase.GetAssetPath(previousDefault);
            int exitCode=0;
            try
            {
                foreach(string folder in new[]{Root,Root+"/Models",Root+"/Materials",Root+"/Environment",Root+"/Prefabs",Root+"/Animations",Root+"/Rendering",Review})Directory.CreateDirectory(folder);
                bool playerOnly=Environment.GetCommandLineArgs().Contains("-overhaulPlayerOnly");
                AssetDatabase.Refresh();var pipeline=playerOnly?AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(Root+"/Rendering/OverhaulPipeline.asset"):MakePipeline();
                if(pipeline==null)throw new InvalidOperationException("Bake the art assets before requesting a player-only rebuild.");
                QualitySettings.renderPipeline=pipeline;GraphicsSettings.defaultRenderPipeline=pipeline;QualitySettings.antiAliasing=2;
                var materials=playerOnly?null:MakeMaterials();var scenes=new List<string>();
                bool kingdomOnly=Environment.GetCommandLineArgs().Contains("-overhaulKingdomOnly");
                foreach(ArtBiome biome in Enum.GetValues(typeof(ArtBiome)))
                {
                    if(kingdomOnly&&biome!=ArtBiome.Kingdom)continue;
                    string path="Assets/Game/Scenes/ArtOverhaul_"+biome+".unity";scenes.Add(path);
                    if(playerOnly)
                    {
                        if(!File.Exists(path))throw new FileNotFoundException("Bake the art scenes before requesting a player-only rebuild.",path);
                        continue;
                    }
                    var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                    var prefabs=Cast[(int)biome].Select(kind=>BakeUnit(kind,biome,materials[0])).ToArray();
                    var controller=BuildScene(biome,prefabs,materials,pipeline);
                    EditorSceneManager.SaveScene(scene,path);AssetDatabase.SaveAssets();CaptureViews(controller);
                    WriteInventory(controller);EditorSceneManager.OpenScene(path,OpenSceneMode.Single);
                    Debug.Log("ART_OVERHAUL_BIOME_READY "+biome);
                }
                AssetDatabase.SaveAssets();
                ArtOverhaulValidator.ValidateOrThrow(Review+"/asset-validation.json",kingdomOnly);
                if(Environment.GetCommandLineArgs().Contains("-overhaulPlayer"))
                {
                    var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=scenes.ToArray(),locationPathName="Builds/ArtOverhaul/ArtOverhaul.exe",target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development});
                    File.WriteAllText(Review+"/build.txt","Result: "+report.summary.result+"\nErrors: "+report.summary.totalErrors+"\nWarnings: "+report.summary.totalWarnings+"\nBytes: "+report.summary.totalSize+"\nDuration: "+report.summary.totalTime+"\nScenes: "+string.Join(", ",scenes)+"\n");
                    if(report.summary.result!=BuildResult.Succeeded)throw new InvalidOperationException("Separate art review player failed.");
                }
                Debug.Log("ART_OVERHAUL_OK scenes="+scenes.Count);
            }
            catch(Exception error){Debug.LogException(error);exitCode=1;}
            finally
            {
                QualitySettings.renderPipeline=string.IsNullOrEmpty(previousPipelinePath)?previousPipeline:AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(previousPipelinePath);
                GraphicsSettings.defaultRenderPipeline=string.IsNullOrEmpty(previousDefaultPath)?previousDefault:AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(previousDefaultPath);
                QualitySettings.antiAliasing=previousAA;AssetDatabase.SaveAssets();
            }
            if(exitCode!=0)EditorApplication.Exit(exitCode);
        }
        private static UniversalRenderPipelineAsset MakePipeline()
        {
            string rendererPath=Root+"/Rendering/OverhaulRenderer.asset";
            var renderer=AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rendererPath);
            if(renderer==null){renderer=Object.Instantiate(AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Settings/Mobile_Renderer.asset"));renderer.name="Overhaul Renderer";renderer.rendererFeatures.Clear();AssetDatabase.CreateAsset(renderer,rendererPath);}
            var ao=renderer.rendererFeatures.OfType<ScreenSpaceAmbientOcclusion>().FirstOrDefault();
            if(ao==null){ao=ScriptableObject.CreateInstance<ScreenSpaceAmbientOcclusion>();ao.name="Contact occlusion";AssetDatabase.AddObjectToAsset(ao,renderer);renderer.rendererFeatures.Add(ao);}
            var settings=new SerializedObject(ao);
            void Int(string key,int value){var p=settings.FindProperty("m_Settings."+key);if(p!=null)p.intValue=value;}
            void Float(string key,float value){var p=settings.FindProperty("m_Settings."+key);if(p!=null)p.floatValue=value;}
            Int("AOMethod",1);Int("Source",0);Int("NormalSamples",1);Int("Samples",1);Int("BlurQuality",0);
            settings.FindProperty("m_Settings.Downsample").boolValue=true;settings.FindProperty("m_Settings.AfterOpaque").boolValue=false;
            Float("Intensity",.8f);Float("DirectLightingStrength",.12f);Float("Radius",.25f);Float("Falloff",140);
            settings.ApplyModifiedPropertiesWithoutUndo();ao.SetActive(true);ao.Create();renderer.SetDirty();EditorUtility.SetDirty(renderer);EditorUtility.SetDirty(ao);
            var pipeline=Object.Instantiate(AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/Mobile_RPAsset.asset"));pipeline.name="Overhaul URP";
            var serialized=new SerializedObject(pipeline);serialized.FindProperty("m_RendererDataList").GetArrayElementAtIndex(0).objectReferenceValue=renderer;
            serialized.FindProperty("m_RequireDepthTexture").boolValue=true;serialized.FindProperty("m_ShadowDistance").floatValue=140;serialized.FindProperty("m_MainLightShadowmapResolution").intValue=2048;
            serialized.ApplyModifiedPropertiesWithoutUndo();return Save(pipeline,Root+"/Rendering/OverhaulPipeline.asset");
        }
        private static Material[] MakeMaterials()
        {
            string texturePath=Root+"/Textures/SurfaceAtlas.png";AssetDatabase.ImportAsset(texturePath,ImportAssetOptions.ForceSynchronousImport);
            var importer=(TextureImporter)AssetImporter.GetAtPath(texturePath);importer.maxTextureSize=2048;importer.npotScale=TextureImporterNPOTScale.ToLarger;importer.mipmapEnabled=true;importer.sRGBTexture=true;importer.wrapMode=TextureWrapMode.Clamp;importer.filterMode=FilterMode.Trilinear;importer.anisoLevel=4;importer.textureCompression=TextureImporterCompression.Compressed;importer.SaveAndReimport();
            var shader=AssetDatabase.LoadAssetAtPath<Shader>(Root+"/OverhaulSurface.shader");var waterShader=AssetDatabase.LoadAssetAtPath<Shader>(Root+"/OverhaulWater.shader");
            if(shader==null||waterShader==null||ShaderUtil.ShaderHasError(shader)||ShaderUtil.ShaderHasError(waterShader))throw new InvalidOperationException("Overhaul shaders missing or contain errors.");
            var result=new Material[4];
            for(int i=0;i<3;i++)
            {
                var m=new Material(shader){name=new[]{"Shared painted surfaces","Living foliage","Luminous crystal"}[i],enableInstancing=true};m.SetTexture("_SurfaceMap",AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath));
                m.SetFloat("_TextureStrength",i==2?.4f:.62f);m.SetFloat("_Wind",i==1?.5f:0);m.SetFloat("_Emission",i==2?2.2f:0);
                result[i]=Save(m,Root+"/Materials/"+new[]{"PaintedSurface","Foliage","Crystal"}[i]+".mat");
            }
            result[3]=Save(new Material(waterShader){name="River water"},Root+"/Materials/Water.mat");return result;
        }
        private static GameObject BakeUnit(ArtUnitKind kind,ArtBiome biome,Material material)
        {
            string name=biome+"_"+kind;var root=new GameObject(name);
            var high=ArtOverhaulCharacters.Build(kind,biome,0);var bounds=high.Mesh.bounds;var bones=new Transform[high.BoneNames.Length];
            for(int i=0;i<bones.Length;i++){bones[i]=new GameObject(high.BoneNames[i]).transform;int parent=high.BoneParents[i];bones[i].SetParent(parent<0?root.transform:bones[parent],false);bones[i].localPosition=high.BonePositions[i]-(parent<0?Vector3.zero:high.BonePositions[parent]);}
            var lods=new LOD[3];
            for(int lod=0;lod<3;lod++)
            {
                var mesh=Save(lod==0?high.Mesh:ArtOverhaulCharacters.Build(kind,biome,lod).Mesh,Root+"/Models/"+name+"_LOD"+lod+".asset");
                var child=new GameObject("LOD"+lod,typeof(SkinnedMeshRenderer));child.transform.SetParent(root.transform,false);
                var r=child.GetComponent<SkinnedMeshRenderer>();r.sharedMesh=mesh;r.sharedMaterial=material;r.bones=bones;r.rootBone=bones[0];r.quality=SkinQuality.Bone2;r.localBounds=new Bounds(bounds.center,bounds.size*1.6f+Vector3.one);r.shadowCastingMode=ShadowCastingMode.On;r.receiveShadows=true;
                lods[lod]=new LOD(new[]{.16f,.055f,.006f}[lod],new Renderer[]{r});
            }
            var group=root.AddComponent<LODGroup>();group.SetLODs(lods);group.fadeMode=LODFadeMode.None;group.RecalculateBounds();group.localReferencePoint=bounds.center;group.size=bounds.size.y;
            var avatar=AvatarBuilder.BuildGenericAvatar(root,bones[0].name);avatar.name=name+" rig";avatar=Save(avatar,Root+"/Animations/"+name+".avatar.asset");
            var animator=root.AddComponent<Animator>();animator.avatar=avatar;animator.applyRootMotion=false;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;animator.runtimeAnimatorController=MakeController(name,kind,root.transform,bones);
            var unit=root.AddComponent<ArtOverhaulUnit>();unit.Kind=kind;unit.Biome=biome;unit.Label=Labels[(int)kind];unit.ApplyTeam();
            var collider=root.AddComponent<CapsuleCollider>();collider.center=bounds.center;collider.height=bounds.size.y;collider.radius=Mathf.Min(.72f,bounds.size.x*.35f);
            var prefab=PrefabUtility.SaveAsPrefabAsset(root,Root+"/Prefabs/"+name+".prefab");Object.DestroyImmediate(root);return prefab;
        }
        private static RuntimeAnimatorController MakeController(string name,ArtUnitKind kind,Transform root,Transform[] bones)
        {
            string path=Root+"/Animations/"+name+".controller";var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(path);if(controller==null)controller=AnimatorController.CreateAnimatorControllerAtPath(path);
            var machine=controller.layers[0].stateMachine;foreach(var state in machine.states)machine.RemoveState(state.state);
            foreach(string stateName in new[]{"Idle","Walk","Action"})
            {
                var clip=new AnimationClip{name=stateName,frameRate=30};float duration=stateName=="Idle"?2.4f:stateName=="Walk"?1f:1.4f;
                void R(string bone,Vector3 axis,params float[] values){var found=Array.Find(bones,b=>b.name==bone);if(found!=null)Rotation(clip,root,found,axis,values,duration);}
                foreach(var bone in bones)Rotation(clip,root,bone,Vector3.right,new[]{0f,0f},duration);
                bool dragon=kind==ArtUnitKind.Dragon,mount=kind==ArtUnitKind.Cavalry;
                R("Spine",Vector3.forward,0,1,0,-1,0);R("Head",Vector3.up,-2,0,2,0,-2);
                if(dragon)
                {
                    float wing=stateName=="Action"?24:8;R("WingL",Vector3.forward,0,wing,0,-wing*.4f,0);R("WingR",Vector3.forward,0,-wing,0,wing*.4f,0);
                    R("Tail1",Vector3.up,-4,0,4,0,-4);R("Tail2",Vector3.up,-7,0,7,0,-7);
                    if(stateName=="Action"){R("Jaw",Vector3.right,0,0,23,15,0);R("Neck",Vector3.right,0,-9,-12,4,0);}
                }
                if(mount)
                {
                    R("MountNeck",Vector3.right,0,-2,0,2,0);R("MountTail",Vector3.forward,-6,0,6,0,-6);
                    if(stateName=="Walk")foreach(string leg in new[]{"Front","Rear"})foreach(string side in new[]{"L","R"}){float phase=(side=="L"?1:-1)*(leg=="Front"?1:-1);R("Mount"+leg+"Upper"+side,Vector3.right,-23*phase,0,23*phase,0,-23*phase);R("Mount"+leg+"Lower"+side,Vector3.right,2,18,2,0,2);}
                }
                if(stateName=="Walk"&&!mount)
                {
                    float stride=dragon?14:24;R("UpperLegL",Vector3.right,-stride,0,stride,0,-stride);R("UpperLegR",Vector3.right,stride,0,-stride,0,stride);
                    R("LowerLegL",Vector3.right,2,24,2,0,2);R("LowerLegR",Vector3.right,2,0,2,24,2);R("UpperArmL",Vector3.right,12,0,-12,0,12);R("UpperArmR",Vector3.right,-12,0,12,0,-12);
                }
                if(stateName=="Action"&&!dragon)
                {
                    R("UpperArmR",Vector3.right,0,kind==ArtUnitKind.Worker?-64:-32,-52,12,0);R("LowerArmR",Vector3.right,0,-12,-24,0,0);R("Spine",Vector3.right,0,-5,-8,7,0);
                }
                var hips=Array.Find(bones,b=>b.name=="Hips");PositionY(clip,root,hips,new[]{0f,stateName=="Walk"?.025f:.01f,0f,stateName=="Walk"?.025f:.01f,0f},duration);
                var settings=AnimationUtility.GetAnimationClipSettings(clip);settings.loopTime=true;settings.loopBlend=true;AnimationUtility.SetAnimationClipSettings(clip,settings);clip.EnsureQuaternionContinuity();clip=Save(clip,Root+"/Animations/"+name+"_"+stateName+".anim");clip.name=stateName;EditorUtility.SetDirty(clip);
                var state=machine.AddState(stateName);state.motion=clip;state.writeDefaultValues=true;if(stateName=="Idle")machine.defaultState=state;
            }
            EditorUtility.SetDirty(controller);return controller;
        }
        private static void Rotation(AnimationClip clip,Transform root,Transform bone,Vector3 axis,float[] degrees,float duration)
        {
            string path=AnimationUtility.CalculateTransformPath(bone,root);var curves=new[]{new AnimationCurve(),new AnimationCurve(),new AnimationCurve(),new AnimationCurve()};
            for(int i=0;i<degrees.Length;i++){var q=Quaternion.AngleAxis(degrees[i],axis);for(int c=0;c<4;c++)curves[c].AddKey(duration*i/(degrees.Length-1),q[c]);}
            for(int c=0;c<4;c++){for(int k=0;k<curves[c].length;k++)curves[c].SmoothTangents(k,0);AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve(path,typeof(Transform),"m_LocalRotation."+"xyzw"[c]),curves[c]);}
        }
        private static void PositionY(AnimationClip clip,Transform root,Transform bone,float[] offsets,float duration)
        {
            var curve=new AnimationCurve();for(int i=0;i<offsets.Length;i++)curve.AddKey(duration*i/(offsets.Length-1),bone.localPosition.y+offsets[i]);for(int k=0;k<curve.length;k++)curve.SmoothTangents(k,0);AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve(AnimationUtility.CalculateTransformPath(bone,root),typeof(Transform),"m_LocalPosition.y"),curve);
        }
        private static ArtOverhaulController BuildScene(ArtBiome biome,GameObject[] prefabs,Material[] materials,UniversalRenderPipelineAsset pipeline)
        {
            var controller=new GameObject("Art direction review").AddComponent<ArtOverhaulController>();controller.Biome=biome;controller.Pipeline=pipeline;
            var world=new GameObject("World").transform;
            var water=new Material(materials[3]){name=biome+" water"};water.SetColor("_Shallow",biome==ArtBiome.Fantasy?new Color(.13f,.64f,.66f):new Color(.13f,.68f,.58f));water=Save(water,Root+"/Materials/"+biome+"Water.mat");
            var layout=ArtOverhaulEnvironment.Build(biome,world,materials[0],water,materials[1],materials[2]);PersistEnvironment(world,biome);
            controller.UnitFocus=layout.UnitFocus;controller.RTFocus=layout.RTFocus;controller.Units=new ArtOverhaulUnit[prefabs.Length];
            for(int i=0;i<prefabs.Length;i++)
            {
                var instance=(GameObject)PrefabUtility.InstantiatePrefab(prefabs[i]);instance.transform.position=layout.UnitPositions[i];instance.transform.rotation=layout.UnitRotations[i];PrefabUtility.RecordPrefabInstancePropertyModifications(instance.transform);
                var unit=instance.GetComponent<ArtOverhaulUnit>();unit.SetTeam(biome==ArtBiome.Desert?new Color(.67f,.16f,.10f):biome==ArtBiome.Caribbean?new Color(.08f,.41f,.45f):new Color(.17f,.35f,.66f));PrefabUtility.RecordPrefabInstancePropertyModifications(unit);controller.Units[i]=unit;
            }
            controller.CloseCamera=Camera("Close",layout.UnitPositions[1]+Vector3.up*1.3f,2.4f,22,20);
            controller.MidCamera=Camera("Medium",Vector3.Lerp(layout.UnitFocus,layout.RTFocus,.35f),10,55,35);
            controller.RTSCamera=Camera("RTS",layout.RTFocus+new Vector3(0,0,5),22,55,35);controller.RTSCamera.tag="MainCamera";controller.RTSCamera.gameObject.AddComponent<AudioListener>();
            FitLandmarkFrame(controller.RTSCamera,world,controller.Units);
            LightScene(biome);controller.SetView(2);return controller;
        }
        private static void FitLandmarkFrame(Camera camera,Transform world,ArtOverhaulUnit[] units)
        {
            var min=new Vector2(float.PositiveInfinity,float.PositiveInfinity);var max=new Vector2(float.NegativeInfinity,float.NegativeInfinity);
            void Include(Bounds bounds,Transform transform)
            {
                for(int i=0;i<8;i++)
                {
                    var p=transform.TransformPoint(bounds.center+Vector3.Scale(bounds.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1)));
                    var projected=new Vector2(Vector3.Dot(p,camera.transform.right),Vector3.Dot(p,camera.transform.up));min=Vector2.Min(min,projected);max=Vector2.Max(max,projected);
                }
            }
            foreach(var unit in units){var renderer=unit.GetComponentInChildren<SkinnedMeshRenderer>();Include(renderer.sharedMesh.bounds,renderer.transform);}
            bool landmark=false;
            foreach(var group in world.GetComponentsInChildren<LODGroup>())
            {
                string name=group.name.ToLowerInvariant();if(!name.Contains("castle")&&!name.Contains("citadel")&&!name.Contains("elven sanctuary"))continue;
                foreach(var renderer in group.GetLODs()[0].renderers){var filter=renderer.GetComponent<MeshFilter>();if(filter!=null){Include(filter.sharedMesh.bounds,filter.transform);landmark=true;}}
            }
            if(!landmark)return;
            camera.orthographicSize=Mathf.Max(22,(max.y-min.y)/1.76f,(max.x-min.x)/(1.8f*camera.aspect));
            var middle=(min+max)*.5f;var position=camera.transform.position;
            camera.transform.position+=camera.transform.right*(middle.x-Vector3.Dot(position,camera.transform.right))+camera.transform.up*(middle.y-Vector3.Dot(position,camera.transform.up));
        }
        private static void LightScene(ArtBiome biome)
        {
            var sun=new GameObject("Late morning sunlight",typeof(Light)).GetComponent<Light>();sun.type=LightType.Directional;sun.intensity=biome==ArtBiome.Fantasy?1.8f:2.2f;sun.color=new Color(1,.91f,.77f);sun.transform.rotation=Quaternion.Euler(48,-34,0);sun.shadows=LightShadows.Soft;sun.shadowBias=.025f;sun.shadowNormalBias=.16f;sun.shadowStrength=.86f;RenderSettings.sun=sun;
            RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=new Color(.48f,.59f,.73f);RenderSettings.ambientEquatorColor=new Color(.33f,.38f,.36f);RenderSettings.ambientGroundColor=new Color(.16f,.20f,.16f);RenderSettings.ambientIntensity=1;
            RenderSettings.defaultReflectionMode=DefaultReflectionMode.Custom;RenderSettings.customReflectionTexture=MakeReflection();RenderSettings.reflectionIntensity=.7f;
            RenderSettings.fog=true;RenderSettings.fogMode=FogMode.ExponentialSquared;RenderSettings.fogDensity=biome==ArtBiome.Fantasy?.005f:.004f;RenderSettings.fogColor=biome==ArtBiome.Desert?new Color(.70f,.66f,.52f):new Color(.46f,.60f,.63f);
            var profile=ScriptableObject.CreateInstance<VolumeProfile>();profile.name=biome+" atmosphere";
            profile.Add<Tonemapping>(true).mode.Override(TonemappingMode.ACES);var color=profile.Add<ColorAdjustments>(true);color.postExposure.Override(.4f);color.contrast.Override(5);color.saturation.Override(3);
            var bloom=profile.Add<Bloom>(true);bloom.intensity.Override(biome==ArtBiome.Fantasy?.18f:.06f);bloom.threshold.Override(1.3f);profile.Add<Vignette>(true).intensity.Override(.12f);
            string path=Root+"/Rendering/"+biome+"Atmosphere.asset";var existing=AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if(existing!=null){foreach(var sub in AssetDatabase.LoadAllAssetsAtPath(path))if(sub!=existing)Object.DestroyImmediate(sub,true);EditorUtility.CopySerialized(profile,existing);Object.DestroyImmediate(profile);profile=existing;}else AssetDatabase.CreateAsset(profile,path);
            foreach(var component in profile.components)AssetDatabase.AddObjectToAsset(component,profile);EditorUtility.SetDirty(profile);
            var volume=new GameObject("Atmosphere and restrained grading",typeof(Volume)).GetComponent<Volume>();volume.isGlobal=true;volume.priority=20;volume.sharedProfile=profile;
        }
        private static Camera Camera(string name,Vector3 focus,float size,float pitch,float yaw)
        {
            var c=new GameObject(name+" camera",typeof(Camera)).GetComponent<Camera>();c.orthographic=true;c.orthographicSize=size;c.nearClipPlane=.1f;c.farClipPlane=260;c.aspect=16f/9;c.transform.rotation=Quaternion.Euler(pitch,yaw,0);c.transform.position=focus-c.transform.forward*(name=="Close"?12:80);c.clearFlags=CameraClearFlags.SolidColor;c.backgroundColor=new Color(.48f,.61f,.66f);c.allowHDR=true;
            var data=c.GetUniversalAdditionalCameraData();data.renderPostProcessing=true;data.antialiasing=AntialiasingMode.FastApproximateAntialiasing;return c;
        }
        private static Cubemap MakeReflection()
        {
            string path=Root+"/Rendering/DaylightReflection.asset";var existing=AssetDatabase.LoadAssetAtPath<Cubemap>(path);if(existing!=null)return existing;
            const int size=64;var cube=new Cubemap(size,TextureFormat.RGBAHalf,true){name="Diffuse daylight environment",filterMode=FilterMode.Trilinear};
            for(int face=0;face<6;face++)
            {
                var colors=new Color[size*size];
                for(int y=0;y<size;y++)for(int x=0;x<size;x++)
                {
                    float u=2*(x+.5f)/size-1,v=2*(y+.5f)/size-1;
                    var direction=face==0?new Vector3(1,-v,-u):face==1?new Vector3(-1,-v,u):face==2?new Vector3(u,1,v):face==3?new Vector3(u,-1,-v):face==4?new Vector3(u,-v,1):new Vector3(-u,-v,-1);direction.Normalize();
                    var color=direction.y>=0?Color.Lerp(new Color(.55f,.62f,.65f),new Color(.20f,.34f,.52f),Mathf.Sqrt(direction.y)):Color.Lerp(new Color(.43f,.44f,.34f),new Color(.12f,.14f,.10f),-direction.y);
                    color+=new Color(.6f,.5f,.33f)*Mathf.Pow(Mathf.Max(0,Vector3.Dot(direction,new Vector3(-.3f,.8f,-.5f).normalized)),32);color.a=1;colors[y*size+x]=color;
                }
                cube.SetPixels(colors,(CubemapFace)face);
            }
            cube.Apply(true,false);AssetDatabase.CreateAsset(cube,path);return cube;
        }
        private static void PersistEnvironment(Transform root,ArtBiome biome)
        {
            var cache=new Dictionary<int,Mesh>();var transient=new List<Mesh>();int index=0;
            Mesh Store(Mesh mesh)
            {
                if(mesh==null||EditorUtility.IsPersistent(mesh))return mesh;int id=mesh.GetInstanceID();if(cache.TryGetValue(id,out var saved))return saved;
                // Keep native sources alive until every shared renderer/collider has been rebound.
                saved=Save(mesh,Root+"/Environment/"+biome+"_"+(index++).ToString("D3")+".asset",false);cache[id]=saved;if(mesh!=saved)transient.Add(mesh);return saved;
            }
            foreach(var filter in root.GetComponentsInChildren<MeshFilter>(true))filter.sharedMesh=Store(filter.sharedMesh);
            foreach(var collider in root.GetComponentsInChildren<MeshCollider>(true))collider.sharedMesh=Store(collider.sharedMesh);
            foreach(var mesh in transient)Object.DestroyImmediate(mesh);
        }
        private static void CaptureViews(ArtOverhaulController controller)
        {
            foreach(var unit in controller.Units){unit.Select(false);var animator=unit.GetComponent<Animator>();animator.enabled=false;animator.runtimeAnimatorController.animationClips.First(c=>c.name=="Idle").SampleAnimation(unit.gameObject,.4f);}
            for(int view=0;view<3;view++){controller.SetView(view);Capture(controller.ActiveCamera,1920,1080,Review+"/"+controller.Biome.ToString().ToLowerInvariant()+"-"+new[]{"close","medium","rts"}[view]+".png");}
            var camera=controller.CloseCamera;var position=camera.transform.position;float size=camera.orthographicSize;controller.SetView(0);
            foreach(var unit in controller.Units)
            {
                var bounds=unit.GetComponentInChildren<SkinnedMeshRenderer>().sharedMesh.bounds;
                camera.orthographicSize=unit.Kind==ArtUnitKind.Dragon?4.8f:unit.Kind==ArtUnitKind.Cavalry?2.5f:1.95f;
                camera.transform.position=unit.transform.TransformPoint(bounds.center)-camera.transform.forward*12;
                Capture(camera,1000,1200,Review+"/"+controller.Biome.ToString().ToLowerInvariant()+"-"+unit.Kind.ToString().ToLowerInvariant()+".png");
            }
            camera.transform.position=position;camera.orthographicSize=size;controller.SetView(2);
        }
        private static void Capture(Camera camera,int width,int height,string path)
        {
            using var snapshot=new PosedRenderSnapshot();var rt=new RenderTexture(width,height,24,RenderTextureFormat.ARGB32);rt.Create();var pixels=new Texture2D(width,height,TextureFormat.RGB24,false);var previous=RenderTexture.active;var target=camera.targetTexture;float aspect=camera.aspect;
            try{camera.aspect=width/(float)height;camera.targetTexture=rt;var request=new RenderPipeline.StandardRequest{destination=rt};if(!RenderPipeline.SupportsRenderRequest(camera,request))camera.Render();if(!RenderPipeline.SupportsRenderRequest(camera,request))throw new InvalidOperationException("URP capture unavailable.");for(int i=0;i<3;i++)RenderPipeline.SubmitRenderRequest(camera,request);RenderTexture.active=rt;pixels.ReadPixels(new Rect(0,0,width,height),0,0);pixels.Apply();File.WriteAllBytes(path,pixels.EncodeToPNG());}
            finally{camera.targetTexture=target;camera.aspect=aspect;RenderTexture.active=previous;rt.Release();Object.DestroyImmediate(rt);Object.DestroyImmediate(pixels);}
        }
        private sealed class PosedRenderSnapshot:IDisposable
        {
            private readonly List<(LODGroup group,LOD[] lods)> groups=new List<(LODGroup,LOD[])>();
            private readonly List<(SkinnedMeshRenderer source,bool enabled,GameObject display,Mesh mesh)> copies=new List<(SkinnedMeshRenderer,bool,GameObject,Mesh)>();
            public PosedRenderSnapshot()
            {
                foreach(var unit in Object.FindObjectsByType<ArtOverhaulUnit>(FindObjectsSortMode.None))
                {
                    var group=unit.GetComponent<LODGroup>();var original=group.GetLODs();groups.Add((group,original));var replacement=(LOD[])original.Clone();
                    for(int level=0;level<original.Length;level++)
                    {
                        var source=(SkinnedMeshRenderer)original[level].renderers[0];var mesh=new Mesh();source.BakeMesh(mesh);var display=new GameObject("Capture pose",typeof(MeshFilter),typeof(MeshRenderer));display.transform.SetParent(source.transform,false);display.GetComponent<MeshFilter>().sharedMesh=mesh;var renderer=display.GetComponent<MeshRenderer>();renderer.sharedMaterial=source.sharedMaterial;var block=new MaterialPropertyBlock();source.GetPropertyBlock(block);renderer.SetPropertyBlock(block);copies.Add((source,source.enabled,display,mesh));source.enabled=false;replacement[level].renderers=new Renderer[]{renderer};
                    }
                    group.SetLODs(replacement);
                }
            }
            public void Dispose(){foreach(var item in groups)item.group.SetLODs(item.lods);foreach(var item in copies){item.source.enabled=item.enabled;Object.DestroyImmediate(item.display);Object.DestroyImmediate(item.mesh);}}
        }
        [Serializable] private sealed class Inventory{public string biome;public string status="Baked; screenshots require visual review; desktop counts are not mobile timings.";public int sceneRenderers,environmentMeshes,environmentLODGroups,uniqueMaterials;public List<UnitInventory> units=new List<UnitInventory>();}
        [Serializable] private sealed class UnitInventory{public string kind;public int bones;public int[] triangles;public int[] vertices;public string[] clips;}
        private static void WriteInventory(ArtOverhaulController c)
        {
            var renderers=Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);var report=new Inventory{biome=c.Biome.ToString(),sceneRenderers=renderers.Length,environmentMeshes=Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None).Select(f=>f.sharedMesh).Distinct().Count(),environmentLODGroups=Object.FindObjectsByType<LODGroup>(FindObjectsSortMode.None).Length-c.Units.Length,uniqueMaterials=renderers.SelectMany(r=>r.sharedMaterials).Distinct().Count()};
            foreach(var unit in c.Units){var meshes=unit.GetComponent<LODGroup>().GetLODs().Select(l=>(SkinnedMeshRenderer)l.renderers[0]).ToArray();report.units.Add(new UnitInventory{kind=unit.Kind.ToString(),bones=meshes[0].bones.Length,triangles=meshes.Select(r=>r.sharedMesh.triangles.Length/3).ToArray(),vertices=meshes.Select(r=>r.sharedMesh.vertexCount).ToArray(),clips=unit.GetComponent<Animator>().runtimeAnimatorController.animationClips.Select(a=>a.name).ToArray()});}
            File.WriteAllText(Review+"/"+c.Biome.ToString().ToLowerInvariant()+"-assets.json",JsonUtility.ToJson(report,true));
        }
        private static T Save<T>(T value,string path,bool destroySource=true)where T:Object
        {
            var old=AssetDatabase.LoadAssetAtPath<T>(path);if(old==null){AssetDatabase.CreateAsset(value,path);return value;}
            if(value is Mesh source&&old is Mesh target)
            {
                target.Clear(false);target.indexFormat=source.indexFormat;target.name=source.name;target.vertices=source.vertices;target.normals=source.normals;target.colors=source.colors;
                for(int channel=0;channel<8;channel++){var coordinates=new List<Vector4>();source.GetUVs(channel,coordinates);if(coordinates.Count>0)target.SetUVs(channel,coordinates);}
                if(source.tangents.Length>0)target.tangents=source.tangents;target.bindposes=source.bindposes;target.boneWeights=source.boneWeights;target.subMeshCount=source.subMeshCount;for(int sub=0;sub<source.subMeshCount;sub++)target.SetIndices(source.GetIndices(sub),source.GetTopology(sub),sub,false);target.bounds=source.bounds;
            }
            else EditorUtility.CopySerialized(value,old);EditorUtility.SetDirty(old);if(destroySource)Object.DestroyImmediate(value);return old;
        }
    }
}
