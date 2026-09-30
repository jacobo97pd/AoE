using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Emberfield.Presentation
{
    // Isolated, interactive art review. Movement and poses here never submit simulation commands.
    public sealed class ArtStyleLabController : MonoBehaviour
    {
        public Camera CloseCamera, MidCamera, RTSCamera;
        public ArtStyleUnit Worker, Warrior;
        public ArtStyleUnit[] ColorSamples;
        public Transform LegacySamples;
        public Color[] TeamColors = { new Color(.19f,.37f,.62f), new Color(.62f,.18f,.12f), new Color(.19f,.43f,.25f), new Color(.79f,.54f,.17f) };
        public Camera ActiveCamera { get; private set; }
        public ArtStyleUnit Selected { get; private set; }
        private Text caption;
        private RtsCamera rts;
        private Vector3 destination;
        private bool moving;
        private int viewIndex=2;
        private Canvas controls;
        private RectTransform safeArea;
        private Rect lastSafeArea;
        private Vector2Int lastScreen;
        private bool reviewing;
        private readonly List<Button> buttons = new List<Button>();
        private readonly List<RaycastResult> pointerHits = new List<RaycastResult>();

        private void Start()
        {
            Application.targetFrameRate=60;
            rts=new RtsCamera(RTSCamera,new Vector2(48,36));
            rts.SetHome(new Vector3(24,0,19),8);
            for(int i=0;i<ColorSamples.Length;i++)ColorSamples[i].SetTeam(TeamColors[i%TeamColors.Length]);
            SelectUnit(Warrior); SetView(2); BuildControls();
            var args=Environment.GetCommandLineArgs(); int capture=Array.IndexOf(args,"-artStyleReview");
            if(capture>=0 && capture+1<args.Length) { reviewing=true; Application.runInBackground=true; StartCoroutine(CaptureReview(args[capture+1])); }
        }
        public void SetView(int index)
        {
            viewIndex=index;
            CloseCamera.enabled=index==0; MidCamera.enabled=index==1; RTSCamera.enabled=index==2;
            ActiveCamera=index==0?CloseCamera:index==1?MidCamera:RTSCamera;
            RefreshCaption();
        }
        public void SelectUnit(ArtStyleUnit unit)
        {
            if(Selected!=null) Selected.Select(false);
            moving=false; Selected=unit;
            if(Selected!=null) { Selected.Select(true); Selected.Pose("Idle"); }
            RefreshCaption();
        }
        public void SetPose(string pose)
        {
            moving=false;
            if(Selected!=null) Selected.Pose(pose=="Action"?(Selected.Warrior?"Attack01":"Gather"):pose);
            RefreshCaption();
        }
        public void SetTeam(int index) { if(Selected!=null) Selected.SetTeam(TeamColors[index]); }
        private void RefreshCaption()
        {
            if(caption==null)return;
            caption.text=(viewIndex==0?"CERCA":viewIndex==1?"DISTANCIA MEDIA":"CÁMARA RTS · REFERENCIA DE ACEPTACIÓN")
                +"  /  "+(Selected==null?"Selecciona una unidad":Selected.Warrior?"Guerrero":"Recolector")
                +"  /  "+(Selected?.CurrentPose??"Idle");
        }
        private void Update()
        {
            ApplySafeArea();
            if(moving && Selected!=null)
            {
                Vector3 direction=destination-Selected.transform.position;
                if(direction.sqrMagnitude<.01f) { moving=false; Selected.Pose("Idle"); RefreshCaption(); }
                else { Selected.transform.rotation=Quaternion.RotateTowards(Selected.transform.rotation,Quaternion.LookRotation(direction),450*Time.deltaTime); Selected.transform.position=Vector3.MoveTowards(Selected.transform.position,destination,2*Time.deltaTime); }
            }
            if(reviewing)return;
            if(Keyboard.current!=null)
            {
                if(Keyboard.current.digit1Key.wasPressedThisFrame)SetView(0);
                if(Keyboard.current.digit2Key.wasPressedThisFrame)SetView(1);
                if(Keyboard.current.digit3Key.wasPressedThisFrame)SetView(2);
            }
            if(ActiveCamera==null)return;
            // Raycast the UI at the actual contact position. The input module's cached
            // pointer-over state may still describe the preceding frame during Update.
            bool touchActive=false, touchHandled=false;
            var touchscreen=Touchscreen.current;
            if(touchscreen!=null) foreach(var touch in touchscreen.touches)
            {
                touchActive|=touch.press.isPressed || touch.press.wasReleasedThisFrame;
                if(!touch.press.wasPressedThisFrame || touchHandled)continue;
                touchHandled=true; HandlePointer(touch.position.ReadValue(),touch.touchId.ReadValue());
            }
            var mouse=Mouse.current;
            if(!touchActive && !touchHandled && mouse!=null && mouse.leftButton.wasPressedThisFrame)
                HandlePointer(mouse.position.ReadValue(),-1);
        }
        private void HandlePointer(Vector2 point,int pointerId)
        {
            if(ActiveCamera==null || IsUiPoint(point,pointerId))return;
            Ray ray=ActiveCamera.ScreenPointToRay(point);
            if(!Physics.Raycast(ray,out var hit,150))return;
            var unit=hit.collider.GetComponentInParent<ArtStyleUnit>();
            if(unit!=null)SelectUnit(unit);
            else if(Selected!=null)
            {
                destination=new Vector3(Mathf.Clamp(hit.point.x,15,33),0,Mathf.Clamp(hit.point.z,12,26));
                moving=true; Selected.Pose("Walk"); RefreshCaption();
            }
        }
        private bool IsUiPoint(Vector2 point,int pointerId=-1)
        {
            if(EventSystem.current==null)return false;
            var pointer=new PointerEventData(EventSystem.current) { position=point,pointerId=pointerId };
            pointerHits.Clear(); EventSystem.current.RaycastAll(pointer,pointerHits);
            foreach(var hit in pointerHits)if(hit.module is GraphicRaycaster)return true;
            return false;
        }
        public void BuildControls()
        {
            if(controls!=null)return;
            var canvasObject=new GameObject("Art review controls",typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
            controls=canvasObject.GetComponent<Canvas>(); controls.renderMode=RenderMode.ScreenSpaceOverlay;
            var scaler=canvasObject.GetComponent<CanvasScaler>(); scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution=new Vector2(1600,900); scaler.matchWidthOrHeight=.5f;
            if(EventSystem.current==null) { var events=new GameObject("Art review input",typeof(EventSystem),typeof(InputSystemUIInputModule)); events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions(); }
            safeArea=new GameObject("Safe area",typeof(RectTransform)).GetComponent<RectTransform>(); safeArea.SetParent(canvasObject.transform,false); ApplySafeArea();
            var top=Panel(safeArea,"Header",new Vector2(0,1),new Vector2(1,1),new Vector2(0,-76),Vector2.zero);
            StretchLabel(top,"EMBERFIELD   /   ESTUDIO DE ARTE",22,12,29,new Color(.95f,.83f,.58f));
            caption=StretchLabel(top,"",14,43,25,new Color(.69f,.79f,.82f));
            var bottom=Panel(safeArea,"Controls",Vector2.zero,new Vector2(1,0),Vector2.zero,new Vector2(0,164));
            StretchLabel(bottom,"Toca una unidad y después el suelo para moverla.  ·  Teclas 1 / 2 / 3: cámara.",14,9,25,new Color(.69f,.79f,.82f));
            var actions=ButtonRow(bottom,"Units, cameras and actions",44);
            AddButton(actions,"Recolector",()=>SelectUnit(Worker)); AddButton(actions,"Guerrero",()=>SelectUnit(Warrior));
            AddButton(actions,"Cerca",()=>SetView(0)); AddButton(actions,"Media",()=>SetView(1)); AddButton(actions,"RTS",()=>SetView(2));
            AddButton(actions,"Reposo",()=>SetPose("Idle")); AddButton(actions,"Caminar",()=>SetPose("Walk")); AddButton(actions,"Acción",()=>SetPose("Action"));
            var teams=ButtonRow(bottom,"Player colors",104);
            for(int i=0;i<4;i++) { int index=i; var b=AddButton(teams,new[]{"Equipo azul","Equipo rojo","Equipo verde","Equipo oro"}[i],()=>SetTeam(index)); b.GetComponent<Image>().color=TeamColors[i]*.62f+new Color(.04f,.04f,.04f,.38f); }
            RefreshCaption();
        }
        private void ApplySafeArea()
        {
            if(safeArea==null || Screen.width<=0 || Screen.height<=0)return;
            var size=new Vector2Int(Screen.width,Screen.height); var area=Screen.safeArea;
            if(lastScreen==size && lastSafeArea==area)return;
            lastScreen=size; lastSafeArea=area;
            safeArea.anchorMin=new Vector2(area.xMin/size.x,area.yMin/size.y); safeArea.anchorMax=new Vector2(area.xMax/size.x,area.yMax/size.y);
            safeArea.offsetMin=safeArea.offsetMax=Vector2.zero;
        }
        private static Transform Panel(Transform parent,string name,Vector2 min,Vector2 max,Vector2 offsetMin,Vector2 offsetMax)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(Image));go.transform.SetParent(parent,false);
            var r=(RectTransform)go.transform;r.anchorMin=min;r.anchorMax=max;r.offsetMin=offsetMin;r.offsetMax=offsetMax;
            go.GetComponent<Image>().color=new Color(.035f,.065f,.077f,.96f);return go.transform;
        }
        private static Text Label(Transform parent,string text,int size,Rect box,Color color)
        {
            var go=new GameObject(text,typeof(RectTransform),typeof(Text));go.transform.SetParent(parent,false);
            var label=go.GetComponent<Text>();label.text=text;label.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");label.fontSize=size;label.color=color;label.raycastTarget=false;
            Place(label.rectTransform,box);return label;
        }
        private static Text StretchLabel(Transform parent,string text,int size,float y,float height,Color color)
        {
            var label=Label(parent,text,size,new Rect(0,0,1,height),color); var rect=label.rectTransform;
            rect.anchorMin=new Vector2(0,1); rect.anchorMax=Vector2.one; rect.offsetMin=new Vector2(24,-y-height);rect.offsetMax=new Vector2(-24,-y);
            label.resizeTextForBestFit=true;label.resizeTextMinSize=11;label.resizeTextMaxSize=size;return label;
        }
        private static Transform ButtonRow(Transform parent,string name,float y)
        {
            var row=new GameObject(name,typeof(RectTransform),typeof(HorizontalLayoutGroup));row.transform.SetParent(parent,false);
            var rect=(RectTransform)row.transform;rect.anchorMin=new Vector2(0,1);rect.anchorMax=Vector2.one;rect.offsetMin=new Vector2(24,-y-48);rect.offsetMax=new Vector2(-24,-y);
            var layout=row.GetComponent<HorizontalLayoutGroup>();layout.spacing=8;layout.childControlWidth=true;layout.childControlHeight=true;
            layout.childForceExpandWidth=true;layout.childForceExpandHeight=false;layout.childAlignment=TextAnchor.MiddleCenter;return row.transform;
        }
        private Button AddButton(Transform parent,string title,UnityEngine.Events.UnityAction action)
        {
            var go=new GameObject(title,typeof(RectTransform),typeof(Image),typeof(Button),typeof(LayoutElement));go.transform.SetParent(parent,false);
            var layout=go.GetComponent<LayoutElement>();layout.minWidth=64;layout.minHeight=48;layout.preferredHeight=48;layout.flexibleWidth=1;
            go.GetComponent<Image>().color=new Color(.12f,.19f,.22f);var button=go.GetComponent<Button>();button.onClick.AddListener(action);
            var text=Label(go.transform,title,16,new Rect(0,0,1,48),new Color(.95f,.94f,.86f));text.alignment=TextAnchor.MiddleCenter;
            text.rectTransform.anchorMin=Vector2.zero;text.rectTransform.anchorMax=Vector2.one;text.rectTransform.offsetMin=new Vector2(4,0);text.rectTransform.offsetMax=new Vector2(-4,0);
            text.resizeTextForBestFit=true;text.resizeTextMinSize=11;text.resizeTextMaxSize=16;buttons.Add(button);return button;
        }
        private static void Place(RectTransform rect,Rect box) { rect.anchorMin=rect.anchorMax=new Vector2(0,1);rect.pivot=new Vector2(0,1);rect.anchoredPosition=new Vector2(box.x,-box.y);rect.sizeDelta=box.size; }

        private IEnumerator CaptureReview(string directory)
        {
            Directory.CreateDirectory(directory);
            var report=new PlayerReviewReport { buildGuid=Application.buildGUID,unity=Application.unityVersion,width=Screen.width,height=Screen.height };
            yield return null; yield return new WaitForSecondsRealtime(.5f);
            Canvas.ForceUpdateCanvases(); CheckControls(report); CheckTeams(report);
            yield return CheckAnimations(Worker,report); yield return CheckAnimations(Warrior,report);
            Worker.Pose("Idle"); Warrior.Pose("Idle"); moving=false;
            Worker.Select(false); Warrior.Select(false);
            foreach(var unit in ColorSamples)unit.Select(false);
            controls.gameObject.SetActive(false);
            for(int view=0;view<3;view++)
            {
                SetView(view); yield return new WaitForSecondsRealtime(.20f);
                yield return Capture(directory,new[]{"player-close.png","player-mid.png","player-rts.png"}[view],report);
            }
            SetView(1); Worker.Pose("Walk"); Warrior.Pose("Walk"); yield return new WaitForSecondsRealtime(.45f);
            yield return Capture(directory,"player-walk.png",report);
            SetView(0); Worker.Pose("Idle"); Warrior.Pose("Attack01");yield return new WaitForSecondsRealtime(.42f);
            yield return Capture(directory,"player-attack.png",report);
            Worker.Pose("Gather"); Warrior.Pose("Idle");yield return new WaitForSecondsRealtime(.42f);
            yield return Capture(directory,"player-gather.png",report);
            controls.gameObject.SetActive(true); SetView(2); SelectUnit(Warrior); SetPose("Action");
            yield return new WaitForSecondsRealtime(.35f); Canvas.ForceUpdateCanvases();
            yield return Capture(directory,"player-controls.png",report);
            report.passed=report.problems.Count==0 && report.animations.Count==6 && report.teams.Count==4;
            report.status=report.passed?"completed":"failed";
            File.WriteAllText(Path.Combine(directory,"player-review.json"),JsonUtility.ToJson(report,true));
            Application.Quit(report.passed?0:1);
        }

        [Serializable] private sealed class AnimationReview
        {
            public string unit,pose; public bool passed,stateFound; public int bones,samples;
            public float maxBonePositionChange,maxBoneRotationDegrees,maxRootPositionDrift,maxRootRotationDegrees,maxRootScaleDrift;
        }
        [Serializable] private sealed class TeamReview
        {
            public int sample,renderers; public Color expected,observed; public bool passed,sharedAssetsPreserved;
        }
        [Serializable] private sealed class PlayerReviewReport
        {
            public string status,buildGuid,unity;
            public string note="Native standalone ArtStyleLab: controlled Animator bone sampling, root isolation, shared mesh/material team masks and UI hit-region checks. Animator culling is temporarily AlwaysAnimate during sampling. Captures explicitly render the running camera and camera-space HUD into a RenderTexture, including live skinning; hidden Windows players have no readable backbuffer. Desktop inspection; no physical touch-device, mobile performance or visual-parity claim.";
            public int width,height,visibleControls; public bool passed,controlsFit,controlsBlockWorldInput;
            public List<AnimationReview> animations=new List<AnimationReview>(); public List<TeamReview> teams=new List<TeamReview>();
            public List<string> screenshots=new List<string>(),problems=new List<string>();
        }
        private void CheckControls(PlayerReviewReport report)
        {
            report.controlsFit=true;report.controlsBlockWorldInput=true;
            var corners=new Vector3[4];
            foreach(var button in buttons)
            {
                if(button==null || !button.gameObject.activeInHierarchy){report.controlsFit=false;continue;}
                report.visibleControls++;var rect=(RectTransform)button.transform;rect.GetWorldCorners(corners);
                if(rect.rect.width<44 || rect.rect.height<44)report.controlsFit=false;
                foreach(var corner in corners)
                {
                    var point=RectTransformUtility.WorldToScreenPoint(null,corner);
                    if(point.x<Screen.safeArea.xMin-.5f || point.x>Screen.safeArea.xMax+.5f || point.y<Screen.safeArea.yMin-.5f || point.y>Screen.safeArea.yMax+.5f)report.controlsFit=false;
                }
                var center=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center));
                if(!IsUiPoint(center,-1) || !IsUiPoint(center,1))report.controlsBlockWorldInput=false;
            }
            if(!report.controlsFit || report.visibleControls!=12)report.problems.Add("Controls do not all fit the safe area with at least 44 logical units per touch target.");
            if(!report.controlsBlockWorldInput)report.problems.Add("A UI button contact is not excluded from world input.");
        }
        private void CheckTeams(PlayerReviewReport report)
        {
            if(ColorSamples==null || ColorSamples.Length<4 || TeamColors.Length<4){report.problems.Add("Four team-color samples are required.");return;}
            var block=new MaterialPropertyBlock();
            for(int index=0;index<4;index++)
            {
                var sample=ColorSamples[index];var check=new TeamReview { sample=index,expected=TeamColors[index],passed=true,sharedAssetsPreserved=true };report.teams.Add(check);
                if(sample==null){check.passed=false;report.problems.Add("Missing team sample "+index);continue;}
                var renderers=sample.GetComponentsInChildren<SkinnedMeshRenderer>(true);check.renderers=renderers.Length;
                var reference=(sample.Warrior?Warrior:Worker).GetComponentsInChildren<SkinnedMeshRenderer>(true);
                if(renderers.Length==0 || renderers.Length!=reference.Length)check.passed=check.sharedAssetsPreserved=false;
                for(int rendererIndex=0;rendererIndex<renderers.Length;rendererIndex++)
                {
                    renderers[rendererIndex].GetPropertyBlock(block);check.observed=block.GetColor("_TeamColor");
                    if(!SameColor(check.observed,check.expected))check.passed=false;
                    if(rendererIndex>=reference.Length || renderers[rendererIndex].sharedMesh!=reference[rendererIndex].sharedMesh || renderers[rendererIndex].sharedMaterial!=reference[rendererIndex].sharedMaterial)
                        check.passed=check.sharedAssetsPreserved=false;
                }
                if(!check.passed)report.problems.Add("Team "+index+" does not preserve its MPB color and shared assets at every LOD.");
            }
        }
        private IEnumerator CheckAnimations(ArtStyleUnit unit,PlayerReviewReport report)
        {
            if(unit==null){report.problems.Add("Missing animation review unit.");yield break;}
            var animator=unit.GetComponent<Animator>();var renderer=unit.GetComponentInChildren<SkinnedMeshRenderer>(true);
            if(animator==null || animator.runtimeAnimatorController==null || renderer==null || renderer.bones.Length==0)
            {report.problems.Add("Missing Animator, controller or skeleton on "+unit.name);yield break;}
            var originalCulling=animator.cullingMode;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
            var bones=renderer.bones;var positions=new Vector3[bones.Length];var rotations=new Quaternion[bones.Length];
            foreach(string state in new[]{"Idle","Walk",unit.Warrior?"Attack01":"Gather"})
            {
                var check=new AnimationReview { unit=unit.Warrior?"reedguard":"tender",pose=state,bones=bones.Length };report.animations.Add(check);
                string fullState=animator.GetLayerName(0)+"."+state;
                check.stateFound=animator.HasState(0,Animator.StringToHash(fullState)) || animator.HasState(0,Animator.StringToHash(state));
                if(!check.stateFound){report.problems.Add(check.unit+" is missing Animator state "+state);continue;}
                moving=false;unit.Pose(state);animator.Play(state,0,0);animator.Update(0);
                var rootPosition=unit.transform.position;var rootRotation=unit.transform.rotation;var rootScale=unit.transform.localScale;
                bool validBones=true;
                for(int i=0;i<bones.Length;i++)
                {
                    if(bones[i]==null){validBones=false;continue;}
                    positions[i]=bones[i].localPosition;rotations[i]=bones[i].localRotation;
                }
                for(int sample=0;sample<5;sample++)
                {
                    yield return new WaitForSecondsRealtime(.16f);check.samples++;
                    for(int i=0;i<bones.Length;i++)if(bones[i]!=null)
                    {
                        check.maxBonePositionChange=Mathf.Max(check.maxBonePositionChange,Vector3.Distance(positions[i],bones[i].localPosition));
                        check.maxBoneRotationDegrees=Mathf.Max(check.maxBoneRotationDegrees,Quaternion.Angle(rotations[i],bones[i].localRotation));
                    }
                    check.maxRootPositionDrift=Mathf.Max(check.maxRootPositionDrift,Vector3.Distance(rootPosition,unit.transform.position));
                    check.maxRootRotationDegrees=Mathf.Max(check.maxRootRotationDegrees,Quaternion.Angle(rootRotation,unit.transform.rotation));
                    check.maxRootScaleDrift=Mathf.Max(check.maxRootScaleDrift,Vector3.Distance(rootScale,unit.transform.localScale));
                }
                check.passed=validBones && (check.maxBonePositionChange>.00005f || check.maxBoneRotationDegrees>.03f)
                    && check.maxRootPositionDrift<.0001f && check.maxRootRotationDegrees<.01f && check.maxRootScaleDrift<.0001f && !animator.applyRootMotion;
                if(!check.passed)report.problems.Add(check.unit+"/"+state+" failed bone motion or root-isolation checks.");
            }
            animator.cullingMode=originalCulling;unit.Pose("Idle");
        }
        private static bool SameColor(Color a,Color b)=>Mathf.Abs(a.r-b.r)<.0001f && Mathf.Abs(a.g-b.g)<.0001f && Mathf.Abs(a.b-b.b)<.0001f && Mathf.Abs(a.a-b.a)<.0001f;
        private IEnumerator Capture(string directory,string name,PlayerReviewReport report)
        {
            string path=Path.Combine(directory,name);if(File.Exists(path))File.Delete(path);
            var oldMode=controls.renderMode;var oldCamera=controls.worldCamera;float oldDistance=controls.planeDistance;
            controls.renderMode=RenderMode.ScreenSpaceCamera;controls.worldCamera=ActiveCamera;controls.planeDistance=1;
            Canvas.ForceUpdateCanvases();
            yield return new WaitForEndOfFrame();
            var target=new RenderTexture(Screen.width,Screen.height,24);target.Create();
            try
            {
                UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(ActiveCamera,
                    new UnityEngine.Rendering.RenderPipeline.StandardRequest{destination=target});
                if(PlayerSmoke.CaptureTarget(target,path))report.screenshots.Add(name);
                else report.problems.Add("Native camera render contains no readable pixels: "+name);
            }
            finally
            {
                target.Release();Destroy(target);controls.renderMode=oldMode;controls.worldCamera=oldCamera;controls.planeDistance=oldDistance;
            }
            yield return new WaitForSecondsRealtime(.12f);
        }
    }
}
