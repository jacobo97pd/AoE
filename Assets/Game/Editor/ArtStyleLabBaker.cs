using System;
using System.Collections.Generic;
using System.IO;
using Emberfield.Presentation;
using Emberfield.Simulation;
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
    public static class ArtStyleLabBaker
    {
        public const string Root="Assets/Game/ArtStyleLab";
        public const string ScenePath="Assets/Game/Scenes/ArtStyleLab.unity";
        public const string Review="Artifacts/ArtReview";
        private static readonly List<GameObject> temporary=new List<GameObject>();
        private static int contextMesh;

        public static void Run()
        {
            if(!Application.isBatchMode)throw new InvalidOperationException("Use tools/Build-ArtStyleLab.ps1 to preserve the editor's open scene.");
            int previousAA=QualitySettings.antiAliasing;
            try
            {
                foreach(string folder in new[]{Root,Root+"/Models",Root+"/Materials",Root+"/Prefabs",Root+"/Visuals",Root+"/Animations",Review})Directory.CreateDirectory(folder);
                AssetDatabase.Refresh();
                var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                var material=MakeMaterial();
                var faction=MakeFaction();
                GameObject worker=BakeUnit(false,material,faction),warrior=BakeUnit(true,material,faction);
                var lab=BuildScene(worker,warrior);
                EditorSceneManager.SaveScene(scene,ScenePath);
                AssetDatabase.SaveAssets();
                CaptureViews(lab);
                // Discard capture-only poses, disabled animators and temporary renderer state.
                EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
                ArtAssetValidator.ValidateOrThrow(Review+"/asset-validation.json");
                if(Array.IndexOf(Environment.GetCommandLineArgs(),"-artStylePlayer")>=0)
                {
                    // This dedicated review executable does not replace the shipped alpha.
                    var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{ScenePath},locationPathName="Builds/ArtStyleLab/ArtStyleLab.exe",target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development});
                    File.WriteAllText(Review+"/build.txt","Result: "+report.summary.result+"\nErrors: "+report.summary.totalErrors+"\nWarnings: "+report.summary.totalWarnings+"\nBytes: "+report.summary.totalSize+"\nDuration: "+report.summary.totalTime+"\nScene: "+ScenePath+"\n");
                    if(report.summary.result!=BuildResult.Succeeded)throw new InvalidOperationException("The separate laboratory player failed to build.");
                }
                Debug.Log("ART_STYLE_LAB_OK two prototype identities, six skinned LOD meshes; review at "+Path.GetFullPath(Review));
            }
            catch(Exception error){QualitySettings.antiAliasing=previousAA;Debug.LogException(error);EditorApplication.Exit(1);}
            finally{QualitySettings.antiAliasing=previousAA;}
        }
        private static Material MakeMaterial()
        {
            var shader=AssetDatabase.LoadAssetAtPath<Shader>(Root+"/StylizedArmy.shader");
            if(shader==null || ShaderUtil.ShaderHasError(shader))throw new InvalidOperationException("Stylized Army shader is missing or has errors.");
            // Original, quiet broad weave. One shared 1024 source; geometry supplies the palette
            // and per-surface metallic/smoothness, so teams never duplicate textures/materials.
            const int size=1024;
            var texture=new Texture2D(size,size,TextureFormat.RGB24,false);
            var pixels=new Color32[size*size];
            for(int y=0;y<size;y++)for(int x=0;x<size;x++)
            {
                float weave=Mathf.Sin(x*Mathf.PI/16)*Mathf.Sin(y*Mathf.PI/16);
                byte value=(byte)Mathf.RoundToInt(249+weave*3+Mathf.Sin((x+y)*.014f)*2);
                pixels[y*size+x]=new Color32(value,value,value,255);
            }
            texture.SetPixels32(pixels);texture.Apply();string texturePath=Root+"/Materials/SharedSurface.png";
            File.WriteAllBytes(texturePath,texture.EncodeToPNG());Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(texturePath,ImportAssetOptions.ForceSynchronousImport);
            var importer=(TextureImporter)AssetImporter.GetAtPath(texturePath);importer.maxTextureSize=1024;importer.mipmapEnabled=true;importer.sRGBTexture=true;importer.wrapMode=TextureWrapMode.Repeat;importer.filterMode=FilterMode.Trilinear;importer.textureCompression=TextureImporterCompression.Compressed;importer.SaveAndReimport();
            var material=new Material(shader){name="Aven shared stylized PBR",enableInstancing=false};
            material.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath));
            material.SetFloat("_RimStrength",.045f);material.SetFloat("_NormalStrength",0);
            material.SetFloat("_Smoothness",.72f);
            return Save(material,Root+"/Materials/StylizedArmy.mat");
        }
        private static FactionVisualDefinition MakeFaction()
        {
            var f=ScriptableObject.CreateInstance<FactionVisualDefinition>();f.name="Aven Kingdom visual study";f.FactionId="aven";
            f.BodyLanguage="Steady weight, broad shoulders, planted boots; six-head heroic proportions.";
            f.Silhouettes="Worker: wide straw brim and work pack. Reedguard: long spear, layered shoulders, kite shield.";
            f.ArmorStyle="Rounded steel shells, broad bevels and protected joints with warm brass edges.";
            f.ClothStyle="Royal cloth over ivory sleeves; split front panels and restrained large folds.";
            f.TrimMaterial="Warm brass and aged gold, separate from cool steel.";
            f.FactionSymbol="Original forked horizon: two rising branches above a horizontal bridge. No copied heraldic mark.";
            f.Architecture="Retain Aven civic structure and building footprints; this checkpoint reuses the alpha context.";
            f.Weapons="Readable long spear and large shield; broad practical harvesting tool.";
            f.VisualEffects="Small cool edge highlight, brief warm hit flash, no constant magic clouds.";
            f.EnvironmentProps="Farm bundles, timber, pale stone. Existing context is retained in the lab.";
            f.BannerStyle="Broad player-color panels, pale geometric emblem, brass edge.";
            return Save(f,Root+"/Visuals/AvenVisual.asset");
        }
        private static GameObject BakeUnit(bool warrior,Material material,FactionVisualDefinition faction)
        {
            string name=warrior?"KingdomWarrior":"KingdomWorker";
            var root=new GameObject(name);temporary.Add(root);
            var high=ArtStylePrototypeGeometry.Build(warrior,0);
            var visualBounds=high.Mesh.bounds;
            var bones=new Transform[high.BoneNames.Length];
            for(int i=0;i<bones.Length;i++)
            {
                bones[i]=new GameObject(high.BoneNames[i]).transform;
                int parent=high.BoneParents[i];bones[i].SetParent(parent<0?root.transform:bones[parent],false);
                bones[i].localPosition=high.BonePositions[i]-(parent<0?Vector3.zero:high.BonePositions[parent]);
            }
            var lods=new LOD[3];
            for(int lod=0;lod<3;lod++)
            {
                var mesh=Save(lod==0?high.Mesh:ArtStylePrototypeGeometry.Build(warrior,lod).Mesh,Root+"/Models/"+name+"_LOD"+lod+".asset");
                var child=new GameObject("LOD"+lod,typeof(SkinnedMeshRenderer));child.transform.SetParent(root.transform,false);
                var renderer=child.GetComponent<SkinnedMeshRenderer>();renderer.sharedMesh=mesh;renderer.sharedMaterial=material;renderer.bones=bones;renderer.rootBone=bones[0];renderer.quality=SkinQuality.Bone2;
                renderer.localBounds=new Bounds(new Vector3(0,1.5f,0),new Vector3(3.8f,4.1f,3.8f));renderer.updateWhenOffscreen=false;renderer.shadowCastingMode=ShadowCastingMode.On;renderer.receiveShadows=true;
                lods[lod]=new LOD(new[]{.14f,.06f,.008f}[lod],new Renderer[]{renderer});
            }
            var group=root.AddComponent<LODGroup>();group.SetLODs(lods);group.fadeMode=LODFadeMode.None;group.RecalculateBounds();
            // Use actual mesh silhouette for screen-height LOD selection, not generous animation bounds.
            group.localReferencePoint=visualBounds.center;group.size=visualBounds.size.y;
            var avatar=AvatarBuilder.BuildGenericAvatar(root,high.BoneNames[0]);avatar.name=name+" generic skeleton";avatar=Save(avatar,Root+"/Animations/"+name+".avatar.asset");
            var animator=root.AddComponent<Animator>();animator.avatar=avatar;animator.applyRootMotion=false;animator.cullingMode=AnimatorCullingMode.CullUpdateTransforms;
            animator.runtimeAnimatorController=MakeController(name,warrior,root.transform,bones);
            var visual=ScriptableObject.CreateInstance<UnitVisualDefinition>();visual.name=name+" visual";visual.GameplayDefinitionId=warrior?"reedguard":"tender";visual.Faction=faction;visual.ProductionApproved=false;visual.ReviewNotes="Checkpoint 2 candidate, isolated art lab. Pending human approval and mobile profiling; no gameplay stats are stored here.";
            visual=Save(visual,Root+"/Visuals/"+name+".asset");
            var skin=ScriptableObject.CreateInstance<CosmeticSkinDefinition>();skin.name=name+" default";skin.SkinId=name+"_Default";skin.UnitVisual=visual;skin.Rarity=CosmeticVisualRarity.Standard;skin.SurfaceTint=Color.white;
            skin=Save(skin,Root+"/Visuals/"+name+"_Default.asset");
            var unit=root.AddComponent<ArtStyleUnit>();unit.Definition=visual;unit.Skin=skin;unit.Warrior=warrior;unit.RefreshAppearance();
            var collider=root.AddComponent<CapsuleCollider>();collider.center=new Vector3(0,1.05f,0);collider.height=2.1f;collider.radius=.45f;
            var prefab=PrefabUtility.SaveAsPrefabAsset(root,Root+"/Prefabs/"+name+".prefab");
            visual.Prefab=prefab;EditorUtility.SetDirty(visual);
            Object.DestroyImmediate(root);temporary.Remove(root);return prefab;
        }
        private static RuntimeAnimatorController MakeController(string name,bool warrior,Transform root,Transform[] bones)
        {
            string path=Root+"/Animations/"+name+".controller";
            var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if(controller==null)controller=AnimatorController.CreateAnimatorControllerAtPath(path);
            var machine=controller.layers[0].stateMachine;
            foreach(var state in machine.states)machine.RemoveState(state.state);
            foreach(string stateName in new[]{"Idle","Walk",warrior?"Attack01":"Gather"})
            {
                var clip=new AnimationClip{name=stateName,frameRate=30};
                float duration=stateName=="Idle"?2:stateName=="Walk"?.9f:1.3f;
                foreach(var bone in bones)Rotation(clip,root,bone,Vector3.right,new[]{0f,0f},duration);
                Transform Find(string boneName)=>Array.Find(bones,b=>b.name==boneName);
                if(stateName=="Idle")
                {
                    Rotation(clip,root,Find("Spine"),Vector3.forward,new[]{0f,.9f,0f,-.9f,0f},duration);
                    Rotation(clip,root,Find("Head"),Vector3.up,new[]{-2f,1f,2f,0f,-2f},duration);
                    PositionY(clip,root,Find("Hips"),new[]{0f,.014f,0f,.014f,0f},duration);
                }
                else if(stateName=="Walk")
                {
                    Rotation(clip,root,Find("UpperLegL"),Vector3.right,new[]{-27f,0f,27f,0f,-27f},duration);
                    Rotation(clip,root,Find("UpperLegR"),Vector3.right,new[]{27f,0f,-27f,0f,27f},duration);
                    Rotation(clip,root,Find("LowerLegL"),Vector3.right,new[]{8f,42f,8f,0f,8f},duration);
                    Rotation(clip,root,Find("LowerLegR"),Vector3.right,new[]{8f,0f,8f,42f,8f},duration);
                    Rotation(clip,root,Find("UpperArmL"),Vector3.right,new[]{15f,0f,-15f,0f,15f},duration);
                    Rotation(clip,root,Find("UpperArmR"),Vector3.right,new[]{-12f,0f,12f,0f,-12f},duration);
                    Rotation(clip,root,Find("Spine"),Vector3.forward,new[]{-2f,0f,2f,0f,-2f},duration);
                    PositionY(clip,root,Find("Hips"),new[]{0f,.045f,0f,.045f,0f},duration);
                }
                else
                {
                    Rotation(clip,root,Find("UpperArmR"),Vector3.right,warrior?new[]{0f,-22f,-58f,-40f,0f}:new[]{0f,-75f,-92f,18f,0f},duration);
                    Rotation(clip,root,Find("LowerArmR"),Vector3.right,new[]{0f,-12f,-26f,0f,0f},duration);
                    Rotation(clip,root,Find("Spine"),Vector3.right,new[]{0f,-6f,-9f,14f,0f},duration);
                    Rotation(clip,root,Find("Head"),Vector3.right,new[]{0f,-5f,0f,8f,0f},duration);
                }
                var settings=AnimationUtility.GetAnimationClipSettings(clip);settings.loopTime=true;settings.loopBlend=true;AnimationUtility.SetAnimationClipSettings(clip,settings);
                clip.EnsureQuaternionContinuity();clip=Save(clip,Root+"/Animations/"+name+"_"+stateName+".anim");
                clip.name=stateName;EditorUtility.SetDirty(clip);
                var state=machine.AddState(stateName);state.motion=clip;state.writeDefaultValues=true;
                if(stateName=="Idle")machine.defaultState=state;
            }
            EditorUtility.SetDirty(controller);return controller;
        }
        private static void Rotation(AnimationClip clip,Transform root,Transform bone,Vector3 axis,float[] degrees,float duration)
        {
            if(bone==null)throw new InvalidOperationException("Animation bone missing.");
            string path=AnimationUtility.CalculateTransformPath(bone,root);
            var curves=new[]{new AnimationCurve(),new AnimationCurve(),new AnimationCurve(),new AnimationCurve()};
            for(int i=0;i<degrees.Length;i++){Quaternion q=Quaternion.AngleAxis(degrees[i],axis);for(int c=0;c<4;c++)curves[c].AddKey(duration*i/(degrees.Length-1),q[c]);}
            for(int c=0;c<4;c++){for(int k=0;k<curves[c].length;k++)curves[c].SmoothTangents(k,0);AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve(path,typeof(Transform),"m_LocalRotation."+"xyzw"[c]),curves[c]);}
        }
        private static void PositionY(AnimationClip clip,Transform root,Transform bone,float[] offset,float duration)
        {
            var curve=new AnimationCurve();for(int i=0;i<offset.Length;i++)curve.AddKey(duration*i/(offset.Length-1),bone.localPosition.y+offset[i]);for(int k=0;k<curve.length;k++)curve.SmoothTangents(k,0);
            AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve(AnimationUtility.CalculateTransformPath(bone,root),typeof(Transform),"m_LocalPosition.y"),curve);
        }
        private static ArtStyleLabController BuildScene(GameObject workerPrefab,GameObject warriorPrefab)
        {
            var lab=new GameObject("ArtStyleLab").AddComponent<ArtStyleLabController>();
            lab.Worker=Instance(workerPrefab,new Vector3(22.5f,0,18));lab.Warrior=Instance(warriorPrefab,new Vector3(25.5f,0,18));
            lab.ColorSamples=new ArtStyleUnit[4];
            for(int i=0;i<4;i++){lab.ColorSamples[i]=Instance(i%2==0?workerPrefab:warriorPrefab,new Vector3(20.5f+i*2.7f,0,23));lab.ColorSamples[i].SetTeam(lab.TeamColors[i]);PrefabUtility.RecordPrefabInstancePropertyModifications(lab.ColorSamples[i]);ReviewLayer(lab.ColorSamples[i].transform,31);}
            lab.LegacySamples=new GameObject("Previous alpha geometry, comparison only").transform;
            var oldWorker=AlphaWorldArt.Unit("tender",FactionKind.AvenCompact,lab.LegacySamples,1);oldWorker.position=new Vector3(17,0,18);oldWorker.rotation=Quaternion.Euler(0,180,0);
            var oldWarrior=AlphaWorldArt.Unit("reedguard",FactionKind.AvenCompact,lab.LegacySamples,1);oldWarrior.position=new Vector3(18.8f,0,18);oldWarrior.rotation=Quaternion.Euler(0,180,0);
            BakeContext(lab.LegacySamples);
            ReviewLayer(lab.LegacySamples,30);
            var ground=Shape("Neutral ground",PrimitiveType.Cube,new Vector3(24,-.11f,18),new Vector3(48,.2f,36),ContextMaterial("Ground",new Color(.33f,.37f,.27f)));
            Shape("Review plaza",PrimitiveType.Cube,new Vector3(24,-.025f,19),new Vector3(21,.04f,12),ContextMaterial("Plaza",new Color(.49f,.46f,.38f)));
            var plinth=ContextMaterial("Plinth",new Color(.24f,.27f,.27f));
            foreach(var unit in new[]{lab.Worker,lab.Warrior})Shape("Silhouette base",PrimitiveType.Cylinder,unit.transform.position+Vector3.down*.01f,new Vector3(2.2f,.016f,2.2f),plinth);
            var context=new GameObject("Retained alpha building and vegetation").transform;
            var building=AlphaWorldArt.Building("hearth",FactionKind.AvenCompact,context,1,4,4);building.position=new Vector3(16,0,27);building.rotation=Quaternion.Euler(0,180,0);
            foreach(var position in new[]{new Vector3(13,0,24),new Vector3(32,0,25),new Vector3(34,0,28)}){var tree=AlphaWorldArt.Resource(ResourceKind.Wood,context);tree.position=position;}
            BakeContext(context);
            lab.CloseCamera=Camera("CloseCamera",new Vector3(24,1.15f,18),2.4f,24,24);
            lab.CloseCamera.cullingMask&=~((1<<31)|(1<<30));
            lab.MidCamera=Camera("MidCamera",new Vector3(24,0,19),4.5f,55,35);
            lab.RTSCamera=Camera("RTSCamera",new Vector3(24,0,19),8,55,35);
            var rts=new RtsCamera(lab.RTSCamera,new Vector2(48,36));rts.SetHome(new Vector3(24,0,19),8);
            lab.RTSCamera.gameObject.tag="MainCamera";lab.RTSCamera.gameObject.AddComponent<AudioListener>();
            var sun=new GameObject("Warm daylight",typeof(Light)).GetComponent<Light>();sun.type=LightType.Directional;sun.intensity=1.65f;sun.color=new Color(1,.91f,.78f);sun.transform.rotation=Quaternion.Euler(47,-35,0);sun.shadows=LightShadows.Soft;sun.shadowBias=.035f;sun.shadowNormalBias=.20f;sun.shadowStrength=.80f;RenderSettings.sun=sun;
            RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=new Color(.43f,.55f,.68f);RenderSettings.ambientEquatorColor=new Color(.32f,.35f,.36f);RenderSettings.ambientGroundColor=new Color(.17f,.19f,.19f);RenderSettings.ambientIntensity=1;
            var profile=ScriptableObject.CreateInstance<VolumeProfile>();profile.name="Daylight art review";
            var tone=profile.Add<Tonemapping>(true);tone.mode.Override(TonemappingMode.ACES);
            var color=profile.Add<ColorAdjustments>(true);color.postExposure.Override(.15f);color.contrast.Override(6);color.saturation.Override(5);
            var bloom=profile.Add<Bloom>(true);bloom.intensity.Override(.07f);bloom.threshold.Override(1.35f);
            // Persist volume components as subassets so the saved lab and player reproduce the capture.
            string volumePath=Root+"/Materials/Daylight.asset";var existing=AssetDatabase.LoadAssetAtPath<VolumeProfile>(volumePath);
            if(existing!=null){foreach(var old in AssetDatabase.LoadAllAssetsAtPath(volumePath))if(old!=existing)Object.DestroyImmediate(old,true);EditorUtility.CopySerialized(profile,existing);Object.DestroyImmediate(profile);profile=existing;}else AssetDatabase.CreateAsset(profile,volumePath);
            foreach(var component in profile.components)AssetDatabase.AddObjectToAsset(component,profile);
            EditorUtility.SetDirty(profile);
            var volume=new GameObject("Restrained daylight grade",typeof(Volume)).GetComponent<Volume>();volume.isGlobal=true;volume.sharedProfile=profile;volume.priority=10;
            lab.SetView(2);return lab;
        }
        private static ArtStyleUnit Instance(GameObject prefab,Vector3 position)
        {var root=(GameObject)PrefabUtility.InstantiatePrefab(prefab);root.transform.position=position;root.transform.rotation=Quaternion.Euler(0,180,0);PrefabUtility.RecordPrefabInstancePropertyModifications(root.transform);return root.GetComponent<ArtStyleUnit>();}
        private static void ReviewLayer(Transform root,int layer)
        {foreach(var child in root.GetComponentsInChildren<Transform>(true)){child.gameObject.layer=layer;PrefabUtility.RecordPrefabInstancePropertyModifications(child.gameObject);}}
        private static Camera Camera(string name,Vector3 focus,float size,float pitch,float yaw)
        {
            var camera=new GameObject(name,typeof(Camera)).GetComponent<Camera>();camera.orthographic=true;camera.orthographicSize=size;camera.nearClipPlane=.1f;camera.farClipPlane=150;camera.aspect=16f/9;
            camera.transform.rotation=Quaternion.Euler(pitch,yaw,0);camera.transform.position=focus-camera.transform.forward*55;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.36f,.45f,.50f);camera.allowHDR=true;
            var data=camera.GetUniversalAdditionalCameraData();data.renderPostProcessing=true;data.antialiasing=AntialiasingMode.FastApproximateAntialiasing;return camera;
        }
        private static Material ContextMaterial(string name,Color color)
        {var mat=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=name};mat.SetColor("_BaseColor",color);mat.SetFloat("_Smoothness",.15f);return Save(mat,Root+"/Materials/"+name+".mat");}
        private static GameObject Shape(string name,PrimitiveType type,Vector3 position,Vector3 scale,Material material)
        {var go=GameObject.CreatePrimitive(type);go.name=name;go.transform.position=position;go.transform.localScale=scale;go.GetComponent<Renderer>().sharedMaterial=material;return go;}
        private static void BakeContext(Transform root)
        {
            foreach(var filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if(EditorUtility.IsPersistent(filter.sharedMesh))continue;
                filter.sharedMesh=Save(Object.Instantiate(filter.sharedMesh),Root+"/Models/Context_"+(contextMesh++)+".asset");
            }
        }
        private static void CaptureViews(ArtStyleLabController lab)
        {
            foreach(var renderer in Object.FindObjectsByType<SkinnedMeshRenderer>(FindObjectsSortMode.None))renderer.updateWhenOffscreen=true;
            foreach(var unit in Object.FindObjectsByType<ArtStyleUnit>(FindObjectsSortMode.None))
            {unit.Select(false);var animator=unit.GetComponent<Animator>();animator.enabled=false;var clip=Array.Find(animator.runtimeAnimatorController.animationClips,c=>c.name=="Idle");clip.SampleAnimation(unit.gameObject,0);}
            WriteReadability(lab);
            for(int i=0;i<3;i++){lab.SetView(i);Capture(lab.ActiveCamera,1920,1080,Review+"/"+new[]{"close.png","mid.png","rts.png"}[i]);}
            Capture(lab.RTSCamera,1280,720,Review+"/rts-phone.png");
            Capture(lab.RTSCamera,1440,1080,Review+"/rts-tablet.png");
            var previous=lab.CloseCamera.transform.position;float oldSize=lab.CloseCamera.orthographicSize;int oldMask=lab.CloseCamera.cullingMask;
            lab.CloseCamera.cullingMask|=1<<30;
            lab.CloseCamera.transform.position=new Vector3(21.8f,1.15f,18)-lab.CloseCamera.transform.forward*55;lab.CloseCamera.orthographicSize=3.8f;
            lab.SetView(0);Capture(lab.CloseCamera,1920,1080,Review+"/comparison.png");
            lab.CloseCamera.transform.position=previous;lab.CloseCamera.orthographicSize=oldSize;lab.CloseCamera.cullingMask=oldMask;
            for(int i=0;i<2;i++)
            {
                var unit=i==0?lab.Worker:lab.Warrior;
                var clip=Array.Find(unit.GetComponent<Animator>().runtimeAnimatorController.animationClips,c=>c.name==(i==0?"Gather":"Attack01"));
                clip.SampleAnimation(unit.gameObject,.65f);Capture(lab.CloseCamera,1920,1080,Review+"/"+(i==0?"gather":"attack")+".png");
                Array.Find(unit.GetComponent<Animator>().runtimeAnimatorController.animationClips,c=>c.name=="Idle").SampleAnimation(unit.gameObject,0);
            }
            lab.SetView(2);
        }
        [Serializable] private sealed class ReadabilityReport
        {
            public string method="Actual posed LOD0 vertices projected by RTSCamera; full silhouette including equipment. Offscreen resolution, not physical device validation.";
            public float pitch=55,yaw=35,orthographicSize=8;
            public List<ProjectedUnit> measurements=new List<ProjectedUnit>();
        }
        [Serializable] private sealed class ProjectedUnit
        {public string unit;public int width,height;public float silhouetteWidthPixels,silhouetteHeightPixels;}
        private static void WriteReadability(ArtStyleLabController lab)
        {
            var report=new ReadabilityReport();var camera=lab.RTSCamera;float oldAspect=camera.aspect;
            try
            {
                foreach(var size in new[]{new Vector2Int(1920,1080),new Vector2Int(1280,720),new Vector2Int(1440,1080)})
                foreach(var unit in new[]{lab.Worker,lab.Warrior})
                {
                    camera.aspect=size.x/(float)size.y;
                    var renderer=(SkinnedMeshRenderer)unit.GetComponent<LODGroup>().GetLODs()[0].renderers[0];
                    var mesh=new Mesh();renderer.BakeMesh(mesh);var min=Vector2.one*float.PositiveInfinity;var max=Vector2.one*float.NegativeInfinity;
                    foreach(var vertex in mesh.vertices)
                    {
                        Vector2 projected=camera.WorldToViewportPoint(renderer.transform.TransformPoint(vertex));
                        min=Vector2.Min(min,projected);max=Vector2.Max(max,projected);
                    }
                    Object.DestroyImmediate(mesh);
                    report.measurements.Add(new ProjectedUnit{unit=unit.Definition.GameplayDefinitionId,width=size.x,height=size.y,silhouetteWidthPixels=(max.x-min.x)*size.x,silhouetteHeightPixels=(max.y-min.y)*size.y});
                }
                File.WriteAllText(Review+"/readability.json",JsonUtility.ToJson(report,true));
            }
            finally{camera.aspect=oldAspect;}
        }
        private static void Capture(Camera camera,int width,int height,string path)
        {
            // A synchronous editor render does not advance Unity's skinning player loop.
            // Bake the actual posed skinned mesh for this single capture, retaining its LOD,
            // material and property block. The standalone review separately verifies live animation.
            using var snapshot=new PosedRenderSnapshot();
            var rt=new RenderTexture(width,height,24,RenderTextureFormat.ARGB32);rt.Create();
            var pixels=new Texture2D(width,height,TextureFormat.RGB24,false);var previous=RenderTexture.active;var target=camera.targetTexture;float previousAspect=camera.aspect;
            try
            {
                camera.aspect=width/(float)height;camera.targetTexture=rt;var request=new RenderPipeline.StandardRequest{destination=rt};
                if(!RenderPipeline.SupportsRenderRequest(camera,request))camera.Render();
                if(!RenderPipeline.SupportsRenderRequest(camera,request))throw new InvalidOperationException("URP capture unavailable.");
                for(int i=0;i<3;i++)RenderPipeline.SubmitRenderRequest(camera,request);
                RenderTexture.active=rt;pixels.ReadPixels(new Rect(0,0,width,height),0,0);pixels.Apply();
                File.WriteAllBytes(path,pixels.EncodeToPNG());
            }
            finally{camera.targetTexture=target;camera.aspect=previousAspect;RenderTexture.active=previous;rt.Release();Object.DestroyImmediate(rt);Object.DestroyImmediate(pixels);}
        }
        private sealed class PosedRenderSnapshot:IDisposable
        {
            private readonly List<(LODGroup group,LOD[] lods)> groups=new List<(LODGroup,LOD[])>();
            private readonly List<(SkinnedMeshRenderer source,bool enabled,GameObject display,Mesh mesh)> copies=new List<(SkinnedMeshRenderer,bool,GameObject,Mesh)>();
            public PosedRenderSnapshot()
            {
                foreach(var unit in Object.FindObjectsByType<ArtStyleUnit>(FindObjectsSortMode.None))
                {
                    var group=unit.GetComponent<LODGroup>();var original=group.GetLODs();groups.Add((group,original));
                    var replacement=(LOD[])original.Clone();
                    for(int level=0;level<original.Length;level++)
                    {
                        var source=(SkinnedMeshRenderer)original[level].renderers[0];
                        var mesh=new Mesh();source.BakeMesh(mesh);
                        var display=new GameObject("Temporary posed capture",typeof(MeshFilter),typeof(MeshRenderer));
                        display.layer=source.gameObject.layer;display.transform.SetParent(source.transform,false);
                        display.GetComponent<MeshFilter>().sharedMesh=mesh;
                        var renderer=display.GetComponent<MeshRenderer>();renderer.sharedMaterial=source.sharedMaterial;
                        var block=new MaterialPropertyBlock();source.GetPropertyBlock(block);renderer.SetPropertyBlock(block);
                        copies.Add((source,source.enabled,display,mesh));source.enabled=false;
                        replacement[level].renderers=new Renderer[]{renderer};
                    }
                    group.SetLODs(replacement);
                }
            }
            public void Dispose()
            {
                foreach(var item in groups)item.group.SetLODs(item.lods);
                foreach(var item in copies){item.source.enabled=item.enabled;Object.DestroyImmediate(item.display);Object.DestroyImmediate(item.mesh);}
            }
        }
        private static T Save<T>(T value,string path)where T:Object
        {
            var old=AssetDatabase.LoadAssetAtPath<T>(path);
            if(old==null){AssetDatabase.CreateAsset(value,path);return value;}
            if(value is Mesh source && old is Mesh target)
            {
                // CopySerialized leaves native vertex buffers stale when the mesh layout/count
                // changes. Rebuild every channel while retaining the asset GUID and references.
                target.Clear(false);target.indexFormat=source.indexFormat;target.name=source.name;
                target.vertices=source.vertices;target.normals=source.normals;target.colors=source.colors;
                for(int channel=0;channel<8;channel++)
                {
                    var coordinates=new List<Vector4>();source.GetUVs(channel,coordinates);
                    if(coordinates.Count>0)target.SetUVs(channel,coordinates);
                }
                if(source.tangents.Length>0)target.tangents=source.tangents;
                target.bindposes=source.bindposes;target.boneWeights=source.boneWeights;
                target.subMeshCount=source.subMeshCount;
                for(int sub=0;sub<source.subMeshCount;sub++)target.SetIndices(source.GetIndices(sub),source.GetTopology(sub),sub,false);
                target.bounds=source.bounds;
            }
            else EditorUtility.CopySerialized(value,old);
            EditorUtility.SetDirty(old);Object.DestroyImmediate(value);return old;
        }
    }
}
