using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Emberfield.Presentation
{
    // Presentation-only interaction. This controller never submits simulation commands.
    public sealed class ArtOverhaulController : MonoBehaviour
    {
        public ArtBiome Biome;
        public Camera CloseCamera, MidCamera, RTSCamera;
        public ArtOverhaulUnit[] Units;
        public UniversalRenderPipelineAsset Pipeline;
        public Vector3 UnitFocus, RTFocus;
        public Camera ActiveCamera { get; private set; }
        public ArtOverhaulUnit Selected { get; private set; }

        private static readonly Color[] TeamColors = { new Color(.22f,.39f,.62f),new Color(.62f,.18f,.12f),new Color(.79f,.54f,.17f) };
        private static readonly string[] PoseNames = { "Idle", "Walk", "Action" };
        private static bool commandLineBiomeRouted;
        private readonly List<Button> buttons = new List<Button>();
        private readonly List<RaycastResult> uiHits = new List<RaycastResult>();
        private Canvas controls;
        private RectTransform safeArea;
        private RectTransform headerPanel, footerPanel;
        private Camera paddedCamera;
        private float cameraSizeBeforePadding;
        private Vector3 cameraPositionBeforePadding;
        private Rect controlsFreeArea;
        private Rect controlsHeaderArea, controlsFooterArea;
        private bool controlsMeasurementValid;
        private GameObject chrome;
        private Text caption, toggleLabel;
        private Rect lastSafeArea;
        private Vector2Int lastScreen;
        private int viewIndex = 2;
        private bool reviewing, moving;
        private Vector3 destination;

        private void Awake()
        {
            if (Pipeline != null) QualitySettings.renderPipeline = Pipeline;
        }

        private void Start()
        {
            var args = Environment.GetCommandLineArgs();
            string reviewFolder = Argument(args,"-overhaulReview");
            // Hidden review windows can lose focus during the initial scene switch.
            // Enable background updates before routing so the next scene can reach Start.
            if(!string.IsNullOrEmpty(reviewFolder))Application.runInBackground=true;
            string requestedBiome = Argument(args,"-artBiome");
            if (!commandLineBiomeRouted && !string.IsNullOrEmpty(requestedBiome))
            {
                commandLineBiomeRouted = true;
                if (!Enum.TryParse(requestedBiome,true,out ArtBiome wanted) || !Enum.IsDefined(typeof(ArtBiome),wanted))
                {
                    FailStartup(reviewFolder,"Unknown -artBiome value: "+requestedBiome); return;
                }
                if (wanted != Biome)
                {
                    string sceneName = "ArtOverhaul_"+wanted;
                    if (!Application.CanStreamedLevelBeLoaded(sceneName))
                    { FailStartup(reviewFolder,"Scene is absent from this player: "+sceneName); return; }
                    SceneManager.LoadScene(sceneName); return;
                }
            }
            if (Units == null) Units = GetComponentsInChildren<ArtOverhaulUnit>(true);
            foreach (var unit in Units) if (unit != null) unit.ApplyTeam();
            SetView(2);
            if(Units.Length>0)SelectUnit(Units[Mathf.Min(1,Units.Length-1)]);
            BuildControls();
            if (!string.IsNullOrEmpty(reviewFolder))
            {
                reviewing = true; Application.runInBackground = true;
                StartCoroutine(RunReview(reviewFolder));
            }
        }

        private static string Argument(string[] args,string name)
        {
            int index = Array.IndexOf(args,name);
            return index >= 0 && index+1 < args.Length ? args[index+1] : null;
        }

        private void FailStartup(string directory,string problem)
        {
            Debug.LogError("ART_OVERHAUL_REVIEW "+problem);
            if (!string.IsNullOrEmpty(directory))
            {
                var report = NewReport(); report.problems.Add(problem);
                FinishReview(directory,report);
            }
            else enabled = false;
        }

        // The baker calls this before Start; UI and selection may not exist yet.
        public void SetView(int index)
        {
            RemoveControlsFraming();
            viewIndex = Mathf.Clamp(index,0,2);
            if (CloseCamera != null) CloseCamera.enabled = viewIndex == 0;
            if (MidCamera != null) MidCamera.enabled = viewIndex == 1;
            if (RTSCamera != null) RTSCamera.enabled = viewIndex == 2;
            ActiveCamera = viewIndex == 0 ? CloseCamera : viewIndex == 1 ? MidCamera : RTSCamera;
            if(viewIndex==0)FrameSelected();
            ApplyControlsFraming();
            RefreshCaption();
        }

        public void SelectUnit(ArtOverhaulUnit unit)
        {
            if (Selected != null) { Selected.Select(false); if (moving) Selected.SetPose("Idle"); }
            moving = false; Selected = unit;
            if (Selected != null) Selected.Select(true);
            if(viewIndex==0)FrameSelected();
            RefreshCaption();
        }

        private void FrameSelected()
        {
            if(Selected==null||CloseCamera==null)return;
            RemoveControlsFraming();
            var bounds=Selected.GetComponentInChildren<SkinnedMeshRenderer>().sharedMesh.bounds;
            CloseCamera.orthographicSize=Selected.Kind==ArtUnitKind.Dragon?4.2f:Selected.Kind==ArtUnitKind.Cavalry?2.7f:2.1f;
            var focus=Selected.transform.TransformPoint(bounds.center);
            CloseCamera.transform.position=focus-CloseCamera.transform.forward*12;
            ApplyControlsFraming();
        }

        public void SetPose(string pose)
        {
            moving = false;
            if (Selected != null) Selected.SetPose(pose);
            RefreshCaption();
        }

        public void SetTeam(int index)
        {
            if (Selected != null && index >= 0 && index < TeamColors.Length) Selected.SetTeam(TeamColors[index]);
        }

        public void SetBiome(ArtBiome biome)
        {
            if (reviewing || biome == Biome) return;
            string sceneName = "ArtOverhaul_"+biome;
            if (Application.CanStreamedLevelBeLoaded(sceneName)) SceneManager.LoadScene(sceneName);
            else if (caption != null) caption.text = "Este paisaje no está disponible en esta versión.";
        }

        public void ToggleControls()
        {
            if (chrome == null) return;
            chrome.SetActive(!chrome.activeSelf);
            if (toggleLabel != null) toggleLabel.text = chrome.activeSelf ? "Ocultar" : "Mostrar";
            ApplyControlsFraming();
        }

        // Preserve each authored camera exactly; padding must never accumulate across views or movement.
        private void RemoveControlsFraming()
        {
            if(paddedCamera==null)return;
            paddedCamera.orthographicSize=cameraSizeBeforePadding;
            paddedCamera.transform.position=cameraPositionBeforePadding;
            paddedCamera=null;
        }

        private void ApplyControlsFraming()
        {
            RemoveControlsFraming();
            controlsMeasurementValid=false;controlsFreeArea=default;
            if(ActiveCamera==null||!ActiveCamera.orthographic||controls==null||!controls.gameObject.activeInHierarchy||chrome==null||!chrome.activeInHierarchy||headerPanel==null||footerPanel==null)return;
            Canvas.ForceUpdateCanvases();
            var pixels=ActiveCamera.pixelRect;
            if(pixels.width<=0||pixels.height<=0)return;
            var area=Screen.safeArea;
            var header=PanelScreenRect(headerPanel);var footer=PanelScreenRect(footerPanel);
            controlsHeaderArea=header;controlsFooterArea=footer;
            float bottom=Mathf.Max(pixels.yMin,Mathf.Max(area.yMin,footer.yMax))+8;
            float top=Mathf.Min(pixels.yMax,Mathf.Min(area.yMax,header.yMin))-8;
            float left=Mathf.Max(pixels.xMin,area.xMin)+8,right=Mathf.Min(pixels.xMax,area.xMax)-8;
            if(top-bottom<32||right-left<32)return;
            controlsFreeArea=Rect.MinMaxRect(left,bottom,right,top);
            float scale=ControlsPixelScale();
            float headerHeight=Mathf.Abs(headerPanel.offsetMax.y-headerPanel.offsetMin.y)*scale;
            float footerHeight=Mathf.Abs(footerPanel.offsetMax.y-footerPanel.offsetMin.y)*scale;
            controlsMeasurementValid=headerHeight>1&&footerHeight>1
                &&header.height>=headerHeight-.5f&&footer.height>=footerHeight-.5f
                &&controlsFreeArea.yMin>=footer.yMax+7.5f&&controlsFreeArea.yMax<=header.yMin-7.5f
                &&controlsFreeArea.height<=pixels.height-headerHeight-footerHeight-15;
            if(!controlsMeasurementValid)return;
            float fraction=Mathf.Min(controlsFreeArea.height/pixels.height,controlsFreeArea.width/pixels.width);
            paddedCamera=ActiveCamera;cameraSizeBeforePadding=ActiveCamera.orthographicSize;cameraPositionBeforePadding=ActiveCamera.transform.position;
            float size=cameraSizeBeforePadding/Mathf.Max(.05f,fraction);
            Vector2 center=new Vector2((controlsFreeArea.center.x-pixels.xMin)/pixels.width,(controlsFreeArea.center.y-pixels.yMin)/pixels.height);
            ActiveCamera.orthographicSize=size;
            ActiveCamera.transform.position=cameraPositionBeforePadding
                -ActiveCamera.transform.up*((center.y-.5f)*2*size)
                -ActiveCamera.transform.right*((center.x-.5f)*2*size*ActiveCamera.aspect);
            // Camera-space HUD geometry follows the resized frustum while retaining its screen layout.
            Canvas.ForceUpdateCanvases();
        }

        private Rect PanelScreenRect(RectTransform panel)
        {
            // Screen-space canvases keep these authored anchors/offsets when their world transforms
            // lag an Overlay/Camera switch. Never project those transient world corners to measure UI.
            var chain=new Stack<RectTransform>();
            for(Transform node=panel;node!=null&&node!=controls.transform;node=node.parent)
                if(node is RectTransform rect)chain.Push(rect);
            Rect pixels=controls.renderMode==RenderMode.ScreenSpaceCamera&&controls.worldCamera!=null
                ?controls.worldCamera.pixelRect:new Rect(0,0,Screen.width,Screen.height);
            float scale=ControlsPixelScale();
            while(chain.Count>0)
            {
                var rect=chain.Pop();
                pixels=Rect.MinMaxRect(
                    pixels.xMin+rect.anchorMin.x*pixels.width+rect.offsetMin.x*scale,
                    pixels.yMin+rect.anchorMin.y*pixels.height+rect.offsetMin.y*scale,
                    pixels.xMin+rect.anchorMax.x*pixels.width+rect.offsetMax.x*scale,
                    pixels.yMin+rect.anchorMax.y*pixels.height+rect.offsetMax.y*scale);
            }
            return pixels;
        }

        private float ControlsPixelScale()
        {
            var scaler=controls.GetComponent<CanvasScaler>();
            if(scaler==null)return controls.scaleFactor;
            if(scaler.uiScaleMode==CanvasScaler.ScaleMode.ConstantPixelSize)return scaler.scaleFactor;
            if(scaler.uiScaleMode!=CanvasScaler.ScaleMode.ScaleWithScreenSize)return controls.scaleFactor;
            float width=Screen.width/Mathf.Max(1,scaler.referenceResolution.x),height=Screen.height/Mathf.Max(1,scaler.referenceResolution.y);
            if(scaler.screenMatchMode==CanvasScaler.ScreenMatchMode.Expand)return Mathf.Min(width,height);
            if(scaler.screenMatchMode==CanvasScaler.ScreenMatchMode.Shrink)return Mathf.Max(width,height);
            return Mathf.Pow(2,Mathf.Lerp(Mathf.Log(width,2),Mathf.Log(height,2),scaler.matchWidthOrHeight));
        }

        private void Update()
        {
            ApplySafeArea();
            if (reviewing) return;
            if (moving && Selected != null)
            {
                Vector3 current = Selected.transform.position;
                var direction = new Vector3(destination.x-current.x,0,destination.z-current.z);
                if (direction.sqrMagnitude < .01f)
                { moving = false; Selected.SetPose("Idle"); RefreshCaption(); }
                else
                {
                    var next = Vector3.MoveTowards(current,new Vector3(destination.x,current.y,destination.z),2*Time.deltaTime);
                    next.y = GroundHeight(next,current.y);
                    Selected.transform.position = next;
                    Selected.transform.rotation = Quaternion.RotateTowards(Selected.transform.rotation,Quaternion.LookRotation(direction),360*Time.deltaTime);
                    if(viewIndex==0)FrameSelected();
                }
            }
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.digit1Key.wasPressedThisFrame) SetView(0);
                if (keyboard.digit2Key.wasPressedThisFrame) SetView(1);
                if (keyboard.digit3Key.wasPressedThisFrame) SetView(2);
                if (keyboard.hKey.wasPressedThisFrame) ToggleControls();
            }
            bool touchActive = false, handled = false;
            if (Touchscreen.current != null) foreach (var touch in Touchscreen.current.touches)
            {
                touchActive |= touch.press.isPressed || touch.press.wasReleasedThisFrame;
                if (handled || !touch.press.wasPressedThisFrame) continue;
                handled = true; HandlePointer(touch.position.ReadValue(),touch.touchId.ReadValue());
            }
            if (!touchActive && !handled && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
                HandlePointer(Mouse.current.position.ReadValue(),-1);
        }

        private void HandlePointer(Vector2 position,int pointerId)
        {
            if (ActiveCamera == null || IsUiPoint(position,pointerId)) return;
            Ray ray = ActiveCamera.ScreenPointToRay(position);
            bool hitWorld = Physics.Raycast(ray,out var hit,250,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore);
            var clicked = hitWorld ? hit.collider.GetComponentInParent<ArtOverhaulUnit>() : null;
            float nearest = hitWorld ? hit.distance : float.PositiveInfinity;
            if (clicked == null && Units != null) foreach (var unit in Units)
            {
                if (unit == null || !unit.gameObject.activeInHierarchy || !TryBounds(unit,out var bounds)) continue;
                if (bounds.IntersectRay(ray,out float distance) && distance < nearest) { nearest = distance; clicked = unit; }
            }
            if (clicked != null) { SelectUnit(clicked); return; }
            if (Selected == null) return;
            Vector3 point;
            if (hitWorld) point = hit.point;
            else
            {
                var plane = new Plane(Vector3.up,new Vector3(UnitFocus.x,UnitFocus.y-.9f,UnitFocus.z));
                if (!plane.Raycast(ray,out float distance)) return;
                point = ray.GetPoint(distance);
            }
            // A small, local presentation area; this is not map navigation or combat movement.
            point.x = Mathf.Clamp(point.x,UnitFocus.x-8.5f,UnitFocus.x+8.5f);
            point.z = Mathf.Clamp(point.z,UnitFocus.z-4.5f,UnitFocus.z+4.5f);
            point.y = GroundHeight(point,Selected.transform.position.y);
            destination = point; moving = true; Selected.SetPose("Walk"); RefreshCaption();
        }

        private static float GroundHeight(Vector3 point,float fallback)
        {
            var hits = Physics.RaycastAll(point+Vector3.up*30,Vector3.down,60,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore);
            float nearest = float.PositiveInfinity, result = fallback;
            foreach (var hit in hits)
            {
                if (!(hit.collider is MeshCollider) || hit.collider.GetComponentInParent<ArtOverhaulUnit>() != null || hit.distance >= nearest) continue;
                nearest = hit.distance; result = hit.point.y;
            }
            return result;
        }

        private bool IsUiPoint(Vector2 position,int pointerId)
        {
            if (EventSystem.current == null) return false;
            var pointer = new PointerEventData(EventSystem.current) { position = position, pointerId = pointerId };
            uiHits.Clear(); EventSystem.current.RaycastAll(pointer,uiHits);
            foreach (var hit in uiHits) if (hit.module is GraphicRaycaster) return true;
            return false;
        }

        private static string BiomeName(ArtBiome biome)
        {
            switch (biome)
            {
                case ArtBiome.Caribbean: return "Costa caribeña";
                case ArtBiome.Desert: return "Oasis del desierto";
                case ArtBiome.Fantasy: return "Bosque encantado";
                default: return "Valle del reino";
            }
        }

        private static string UnitName(ArtOverhaulUnit unit)
        {
            if (unit == null) return "Selecciona una unidad";
            switch (unit.Kind)
            {
                case ArtUnitKind.Worker: return "Recolector";
                case ArtUnitKind.Warrior: return "Guerrero";
                case ArtUnitKind.Cavalry: return "Jinete";
                case ArtUnitKind.Hero: return "Héroe";
                case ArtUnitKind.Pirate: return "Pirata";
                case ArtUnitKind.Dwarf: return "Enano";
                case ArtUnitKind.Elf: return "Elfo";
                case ArtUnitKind.MountainWarrior: return "Montañés";
                default: return "Dragón";
            }
        }

        private void RefreshCaption()
        {
            if (caption == null) return;
            caption.text = BiomeName(Biome)+"   ·   "+UnitName(Selected)+"   ·   "+new[]{"Vista cercana","Vista media","Vista táctica"}[viewIndex];
        }

        private void BuildControls()
        {
            if (controls != null) return;
            var canvas = new GameObject("Overhaul controls",typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
            canvas.transform.SetParent(transform,false); controls = canvas.GetComponent<Canvas>(); controls.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvas.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600,900); scaler.matchWidthOrHeight = .5f;
            if (EventSystem.current == null)
            {
                var events = new GameObject("Overhaul input",typeof(EventSystem),typeof(InputSystemUIInputModule));
                events.transform.SetParent(transform,false); events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            }
            safeArea = new GameObject("Safe area",typeof(RectTransform)).GetComponent<RectTransform>(); safeArea.SetParent(canvas.transform,false); ApplySafeArea();
            chrome = new GameObject("Panels",typeof(RectTransform)); chrome.transform.SetParent(safeArea,false); Stretch((RectTransform)chrome.transform);
            var header = Panel(chrome.transform,"Header",true,102);
            headerPanel=(RectTransform)header;
            var brand = TextLabel(header,"EMBERFIELD",22,new Color(.95f,.83f,.60f)); Anchor(brand.rectTransform,24,10,220,30);
            caption = TextLabel(header,"",15,new Color(.78f,.84f,.81f));
            caption.rectTransform.anchorMin = new Vector2(0,1); caption.rectTransform.anchorMax = Vector2.one;
            caption.rectTransform.offsetMin = new Vector2(246,-37); caption.rectTransform.offsetMax = new Vector2(-130,-12);
            var biomes = Row(header,"Biomes",50);
            foreach (ArtBiome biome in Enum.GetValues(typeof(ArtBiome)))
            { var value = biome; var button = AddButton(biomes,BiomeName(biome),()=>SetBiome(value)); button.interactable = value != Biome; }
            var footer = Panel(chrome.transform,"Selection",false,161);
            footerPanel=(RectTransform)footer;
            var hint = TextLabel(footer,"Toca una unidad y el suelo para moverla.   ·   1 / 2 / 3: vistas   ·   H: controles",13,new Color(.69f,.76f,.72f));
            hint.rectTransform.anchorMin = new Vector2(0,1); hint.rectTransform.anchorMax = Vector2.one;
            hint.rectTransform.offsetMin = new Vector2(24,-26); hint.rectTransform.offsetMax = new Vector2(-24,-8);
            var selection = Row(footer,"Units",33);
            foreach (var unit in Units) if (unit != null) { var choice = unit; AddButton(selection,UnitName(unit),()=>SelectUnit(choice)); }
            var actions = Row(footer,"Views and actions",96);
            for (int i=0;i<3;i++) { int view = i; AddButton(actions,new[]{"Cerca","Media","RTS"}[i],()=>SetView(view)); }
            for (int i=0;i<3;i++) { string pose = PoseNames[i]; AddButton(actions,new[]{"Reposo","Caminar","Acción"}[i],()=>SetPose(pose)); }
            for (int i=0;i<TeamColors.Length;i++)
            { int team = i; var button = AddButton(actions,new[]{"Azul","Rojo","Oro"}[i],()=>SetTeam(team)); button.GetComponent<Image>().color = TeamColors[i]*.67f+new Color(.025f,.035f,.025f,.33f); }
            var toggle = AddButton(safeArea,"Ocultar",ToggleControls);
            var toggleRect = (RectTransform)toggle.transform; toggleRect.anchorMin = toggleRect.anchorMax = Vector2.one;
            toggleRect.pivot = Vector2.one; toggleRect.anchoredPosition = new Vector2(-24,-5); toggleRect.sizeDelta = new Vector2(98,44);
            toggleLabel = toggle.GetComponentInChildren<Text>(); RefreshCaption();ApplyControlsFraming();
        }

        private void ApplySafeArea()
        {
            if (safeArea == null || Screen.width <= 0 || Screen.height <= 0) return;
            var size = new Vector2Int(Screen.width,Screen.height); var area = Screen.safeArea;
            if (size == lastScreen && area == lastSafeArea) return;
            lastScreen = size; lastSafeArea = area;
            safeArea.anchorMin = new Vector2(area.xMin/size.x,area.yMin/size.y);
            safeArea.anchorMax = new Vector2(area.xMax/size.x,area.yMax/size.y);
            safeArea.offsetMin = safeArea.offsetMax = Vector2.zero;
            ApplyControlsFraming();
        }

        private static Transform Panel(Transform parent,string name,bool top,float height)
        {
            var panel = new GameObject(name,typeof(RectTransform),typeof(Image)); panel.transform.SetParent(parent,false);
            var rect = (RectTransform)panel.transform; rect.anchorMin = new Vector2(0,top?1:0); rect.anchorMax = new Vector2(1,top?1:0);
            rect.offsetMin = new Vector2(0,top?-height:0); rect.offsetMax = new Vector2(0,top?0:height);
            panel.GetComponent<Image>().color = new Color(.025f,.044f,.042f,.93f); return panel.transform;
        }

        private static Transform Row(Transform parent,string name,float y)
        {
            var row = new GameObject(name,typeof(RectTransform),typeof(HorizontalLayoutGroup)); row.transform.SetParent(parent,false);
            var rect = (RectTransform)row.transform; rect.anchorMin = new Vector2(0,1); rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(24,-y-48); rect.offsetMax = new Vector2(-24,-y);
            var layout = row.GetComponent<HorizontalLayoutGroup>(); layout.spacing = 8; layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true; layout.childForceExpandHeight = false; layout.childAlignment = TextAnchor.MiddleCenter;
            return row.transform;
        }

        private Button AddButton(Transform parent,string title,UnityEngine.Events.UnityAction action)
        {
            var go = new GameObject(title,typeof(RectTransform),typeof(Image),typeof(Button),typeof(LayoutElement)); go.transform.SetParent(parent,false);
            var layout = go.GetComponent<LayoutElement>(); layout.minWidth = 54; layout.minHeight = 44; layout.preferredHeight = 48; layout.flexibleWidth = 1;
            go.GetComponent<Image>().color = new Color(.13f,.19f,.17f);
            var button = go.GetComponent<Button>(); button.onClick.AddListener(action);
            var label = TextLabel(go.transform,title,16,new Color(.94f,.94f,.86f)); label.alignment = TextAnchor.MiddleCenter; Stretch(label.rectTransform,4);
            buttons.Add(button); return button;
        }

        private static Text TextLabel(Transform parent,string text,int size,Color color)
        {
            var go = new GameObject(text,typeof(RectTransform),typeof(Text)); go.transform.SetParent(parent,false);
            var label = go.GetComponent<Text>(); label.text = text; label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = size; label.color = color; label.raycastTarget = false; label.resizeTextForBestFit = true; label.resizeTextMinSize = 11; label.resizeTextMaxSize = size;
            return label;
        }

        private static void Stretch(RectTransform rect,float inset=0)
        { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = Vector2.one*inset; rect.offsetMax = Vector2.one*-inset; }
        private static void Anchor(RectTransform rect,float x,float y,float width,float height)
        { rect.anchorMin = rect.anchorMax = new Vector2(0,1); rect.pivot = new Vector2(0,1); rect.anchoredPosition = new Vector2(x,-y); rect.sizeDelta = new Vector2(width,height); }

        [Serializable] private sealed class StateReview
        { public string state; public bool found,animated,passed; public int frames; public float bonePositionChange,boneRotationDegrees,rootDrift,rootRotationDegrees,rootScaleDrift; }
        [Serializable] private sealed class UnitReview
        { public string kind,label; public int bones; public string[] clips; public bool animated,passed; public float rootDrift; public List<StateReview> states = new List<StateReview>(); }
        [Serializable] private sealed class MetricReview
        { public string name,unit="Count",reason="Unavailable: no completed-frame recorder samples."; public bool available; public int samples; public double mean=-1; public long minimum=-1,maximum=-1; }
        [Serializable] private sealed class ViewReview
        {
            public string view,image; public int countOnScreen,fullyInFrame; public bool hasPixels,hudVisible;
            public float orthographicSize; public Vector3 cameraPosition,cameraEuler;
            public List<string> unitsOnScreen = new List<string>();
            public bool hudFramingApplied,hudPanelMeasurementValid,selectedCenterInFreeArea,selectedBoundsInFreeArea;
            public int centersInFreeArea,boundsInFreeArea;public Rect hudFreeAreaPixels,hudHeaderPixels,hudFooterPixels;
            public List<string> unitsInFreeArea = new List<string>();
            public MetricReview metricsDrawCalls,metricsTriangles;
        }
        [Serializable] private sealed class ReviewReport
        {
            public string status="running",buildGuid,unity,generatedUtc,biome,scene,graphicsDevice;
            public string note="Native Windows art-scene inspection. Screenshots submit the running URP camera into a RenderTexture with live skinning; HUD is camera-space only for its own capture. On-screen counts test transformed LOD0 mesh bounds/frustum and center. HUD captures additionally project unit centers and all eight bounds corners into the measured free screen area between panels; this tests panel overlap, not environment occlusion. Animation checks sample 30 live frames per state with culling temporarily AlwaysAnimate. Render counters sample three regular frames after two drain frames following explicit screenshot requests; HUD visibility is recorded per view. This is not calibrated frame timing, mobile FPS, physical-device testing or visual-parity evidence.";
            public int width,height,visibleControls,teamChecks; public bool passed,controlsFit,controlsBlockWorldInput;
            public List<UnitReview> units = new List<UnitReview>(); public List<ViewReview> views = new List<ViewReview>();
            public List<string> screenshots = new List<string>(),problems = new List<string>();
            public MetricReview metricsDrawCalls = new MetricReview { name="Draw Calls Count" }, metricsTriangles = new MetricReview { name="Triangles Count" };
        }

        private ReviewReport NewReport() => new ReviewReport
        {
            buildGuid = Application.buildGUID, unity = Application.unityVersion, generatedUtc = DateTime.UtcNow.ToString("O"),
            biome = Biome.ToString().ToLowerInvariant(), scene = SceneManager.GetActiveScene().name,
            width = Screen.width, height = Screen.height, graphicsDevice = SystemInfo.graphicsDeviceName
        };

        // Flatten nested review enumerators so a failed readback/animation check still writes a failed report.
        private IEnumerator RunReview(string directory)
        {
            var report = NewReport(); var stack = new Stack<IEnumerator>(); stack.Push(CaptureReview(directory,report));
            while (stack.Count > 0)
            {
                object next = null; bool advanced = false, failed = false;
                try { advanced = stack.Peek().MoveNext(); if (advanced) next = stack.Peek().Current; }
                catch (Exception error) { report.problems.Add(error.GetType().Name+": "+error.Message); Debug.LogException(error); failed = true; }
                if (failed) break;
                if (!advanced) { (stack.Pop() as IDisposable)?.Dispose(); continue; }
                if (next is IEnumerator nested) stack.Push(nested); else yield return next;
            }
            while (stack.Count > 0) (stack.Pop() as IDisposable)?.Dispose();
            FinishReview(directory,report);
        }

        private void FinishReview(string directory,ReviewReport report)
        {
            int expected = Units == null ? 0 : Units.Length;
            report.passed = report.problems.Count == 0 && report.units.Count == expected && expected >= 3 && report.views.Count >= 4 && report.screenshots.Count >= 4;
            report.status = report.passed ? "completed" : "failed";
            try
            {
                Directory.CreateDirectory(directory);
                File.WriteAllText(Path.Combine(directory,report.biome+"-review.json"),JsonUtility.ToJson(report,true));
            }
            catch (Exception error) { report.passed = false; Debug.LogError("Cannot write art review: "+error.Message); }
            Debug.Log("ART_OVERHAUL_PLAYER "+report.biome+" "+(report.passed?"PASSED":"FAILED"));
            Application.Quit(report.passed?0:1);
        }

        private IEnumerator CaptureReview(string directory,ReviewReport report)
        {
            Directory.CreateDirectory(directory);
            yield return null; yield return new WaitForSecondsRealtime(.35f);
            if (Pipeline == null) report.problems.Add("The scene has no dedicated URP pipeline reference.");
            if (Units == null || Units.Length < 3) { report.problems.Add("The scene requires at least three representative units."); yield break; }
            if (CloseCamera == null || MidCamera == null || RTSCamera == null) { report.problems.Add("Three presentation cameras are required."); yield break; }
            Canvas.ForceUpdateCanvases(); CheckControls(report); CheckTeams(report);
            foreach (var unit in Units) if (unit != null) { unit.SetPose("Idle"); unit.Select(false); }
            moving = false; controls.gameObject.SetActive(false);
            for (int view=0;view<3;view++)
            {
                SetView(view); yield return new WaitForSecondsRealtime(.18f);
                yield return Capture(directory,new[]{"close","medium","rts"}[view],report,false);
            }
            foreach (var unit in Units) yield return CheckAnimation(unit,report);
            if (Selected != null)
            {
                SetView(0); Selected.SetPose("Action"); yield return new WaitForSecondsRealtime(3);
                yield return Capture(directory,"action",report,false); Selected.SetPose("Idle");
            }
            controls.gameObject.SetActive(true); chrome.SetActive(true); SetView(2);
            if (Selected != null) Selected.Select(true);
            yield return null; Canvas.ForceUpdateCanvases();
            yield return Capture(directory,"controls",report,true);
        }

        private void CheckControls(ReviewReport report)
        {
            report.controlsFit = report.controlsBlockWorldInput = true; var corners = new Vector3[4];
            foreach (var button in buttons)
            {
                if (button == null || !button.gameObject.activeInHierarchy) { report.controlsFit = false; continue; }
                report.visibleControls++; var rect = (RectTransform)button.transform; rect.GetWorldCorners(corners);
                if (rect.rect.width < 43.9f || rect.rect.height < 43.9f) report.controlsFit = false;
                foreach (var corner in corners)
                {
                    Vector2 point = RectTransformUtility.WorldToScreenPoint(null,corner);
                    if (point.x < Screen.safeArea.xMin-.5f || point.x > Screen.safeArea.xMax+.5f || point.y < Screen.safeArea.yMin-.5f || point.y > Screen.safeArea.yMax+.5f) report.controlsFit = false;
                }
                var center = RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center));
                if (!IsUiPoint(center,-1) || !IsUiPoint(center,1)) report.controlsBlockWorldInput = false;
            }
            if (!report.controlsFit || report.visibleControls != Units.Length+14) report.problems.Add("Controls do not all fit inside the safe area with 44 logical units per target.");
            if (!report.controlsBlockWorldInput) report.problems.Add("A control's mouse or touch hit region is not excluded from world movement.");
        }

        private void CheckTeams(ReviewReport report)
        {
            var properties = new MaterialPropertyBlock();
            foreach (var unit in Units)
            {
                if (unit == null) { report.problems.Add("A unit reference is missing."); continue; }
                var renderers = unit.GetComponentsInChildren<SkinnedMeshRenderer>(true); var original = unit.Team;
                var meshes = new Mesh[renderers.Length]; var materials = new Material[renderers.Length];
                for (int i=0;i<renderers.Length;i++) { meshes[i] = renderers[i].sharedMesh; materials[i] = renderers[i].sharedMaterial; }
                if (renderers.Length == 0) report.problems.Add(unit.name+" has no skinned renderers.");
                foreach (var color in TeamColors)
                {
                    unit.SetTeam(color); bool valid = renderers.Length > 0;
                    for (int i=0;i<renderers.Length;i++)
                    {
                        renderers[i].GetPropertyBlock(properties); var observed = properties.GetColor("_TeamColor");
                        valid &= SameColor(observed,color) && renderers[i].sharedMesh == meshes[i] && renderers[i].sharedMaterial == materials[i];
                    }
                    report.teamChecks++; if (!valid) report.problems.Add(unit.name+" changes shared assets or loses its team color.");
                }
                unit.SetTeam(original);
            }
        }

        private IEnumerator CheckAnimation(ArtOverhaulUnit unit,ReviewReport report)
        {
            if (unit == null) { report.problems.Add("Missing animation unit."); yield break; }
            var check = new UnitReview { kind=unit.Kind.ToString(),label=unit.Label,animated=true,passed=true }; report.units.Add(check);
            var animator = unit.GetComponent<Animator>(); var renderer = unit.GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (animator == null || animator.runtimeAnimatorController == null || renderer == null || renderer.bones.Length == 0)
            { check.animated = check.passed = false; report.problems.Add(unit.name+" lacks its Animator, controller or rig."); yield break; }
            var clips = animator.runtimeAnimatorController.animationClips; check.clips = new string[clips.Length];
            for (int i=0;i<clips.Length;i++) check.clips[i] = clips[i] == null ? "missing" : clips[i].name;
            var bones = renderer.bones; check.bones = bones.Length;
            var positions = new Vector3[bones.Length]; var rotations = new Quaternion[bones.Length];
            var oldCulling = animator.cullingMode; animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            try
            {
                foreach (string state in PoseNames)
                {
                    var sample = new StateReview { state=state }; check.states.Add(sample);
                    sample.found = animator.HasState(0,Animator.StringToHash(state)) || animator.HasState(0,Animator.StringToHash(animator.GetLayerName(0)+"."+state));
                    if (!sample.found) { check.animated = check.passed = false; report.problems.Add(unit.name+" is missing state "+state); continue; }
                    unit.SetPose(state); animator.Play(state,0,0); animator.Update(0);
                    Vector3 root = unit.transform.position, scale = unit.transform.localScale; Quaternion rootRotation = unit.transform.rotation;
                    bool validBones = true;
                    for (int i=0;i<bones.Length;i++)
                    {
                        if (bones[i] == null) { validBones = false; continue; }
                        positions[i] = bones[i].localPosition; rotations[i] = bones[i].localRotation;
                    }
                    for (int frame=0;frame<30;frame++)
                    {
                        yield return null; sample.frames++;
                        for (int i=0;i<bones.Length;i++) if (bones[i] != null)
                        {
                            sample.bonePositionChange = Mathf.Max(sample.bonePositionChange,Vector3.Distance(positions[i],bones[i].localPosition));
                            sample.boneRotationDegrees = Mathf.Max(sample.boneRotationDegrees,Quaternion.Angle(rotations[i],bones[i].localRotation));
                        }
                        sample.rootDrift = Mathf.Max(sample.rootDrift,Vector3.Distance(root,unit.transform.position));
                        sample.rootRotationDegrees = Mathf.Max(sample.rootRotationDegrees,Quaternion.Angle(rootRotation,unit.transform.rotation));
                        sample.rootScaleDrift = Mathf.Max(sample.rootScaleDrift,Vector3.Distance(scale,unit.transform.localScale));
                    }
                    sample.animated = validBones && (sample.bonePositionChange > .00005f || sample.boneRotationDegrees > .03f);
                    sample.passed = sample.animated && sample.rootDrift < .0001f && sample.rootRotationDegrees < .01f && sample.rootScaleDrift < .0001f && !animator.applyRootMotion;
                    check.rootDrift = Mathf.Max(check.rootDrift,sample.rootDrift); check.animated &= sample.animated; check.passed &= sample.passed;
                    if (!sample.passed) report.problems.Add(unit.name+"/"+state+" failed bone motion or root-isolation checks.");
                }
            }
            finally { animator.cullingMode = oldCulling; unit.SetPose("Idle"); }
        }

        private static bool SameColor(Color a,Color b) => Mathf.Abs(a.r-b.r)<.0001f && Mathf.Abs(a.g-b.g)<.0001f && Mathf.Abs(a.b-b.b)<.0001f && Mathf.Abs(a.a-b.a)<.0001f;

        private IEnumerator Capture(string directory,string name,ReviewReport report,bool withHud)
        {
            string imageName = report.biome+"-"+name+".png", path = Path.Combine(directory,imageName);
            var oldMode = controls.renderMode; var oldCamera = controls.worldCamera; float oldDistance = controls.planeDistance;
            if (withHud)
            {
                controls.renderMode = RenderMode.ScreenSpaceCamera; controls.worldCamera = ActiveCamera; controls.planeDistance = 1;
                ApplyControlsFraming();
            }
            else RemoveControlsFraming();
            var view = new ViewReview { view=name,image=imageName,hudVisible=withHud,orthographicSize=ActiveCamera.orthographicSize,cameraPosition=ActiveCamera.transform.position,cameraEuler=ActiveCamera.transform.eulerAngles };
            report.views.Add(view); CheckFrustum(view);
            if (name == "rts" && view.countOnScreen < 3) report.problems.Add("RTS camera contains fewer than three representative unit centers and mesh bounds.");
            if(withHud)
            {
                CheckControlsFraming(view);
                if(!view.hudFramingApplied||!view.hudPanelMeasurementValid||view.centersInFreeArea<3||!view.selectedCenterInFreeArea||!view.selectedBoundsInFreeArea)
                    report.problems.Add("RTS controls obscure representative units: require three centers and all selected-unit bounds inside the measured free area between panels.");
            }
            if (File.Exists(path)) File.Delete(path);
            var target = new RenderTexture(Screen.width,Screen.height,24); target.Create();
            using (var draws = new RenderCounter("Draw Calls Count"))
            using (var triangles = new RenderCounter("Triangles Count"))
            {
                try
                {
                    for (int frame=0;frame<3;frame++)
                    {
                        yield return new WaitForEndOfFrame();
                        RenderPipeline.SubmitRenderRequest(ActiveCamera,new RenderPipeline.StandardRequest { destination=target });
                        yield return null;
                    }
                    // Drain the explicit render submissions before recording ordinary camera frames.
                    yield return null;yield return null;
                    for(int frame=0;frame<3;frame++){yield return null;draws.Sample();triangles.Sample();}
                    view.hasPixels = PlayerSmoke.CaptureTarget(target,path);
                    if (view.hasPixels) report.screenshots.Add(imageName); else report.problems.Add("Native render contains no readable pixels: "+imageName);
                    view.metricsDrawCalls = draws.Report(); view.metricsTriangles = triangles.Report();
                    if (name == "rts") { report.metricsDrawCalls = view.metricsDrawCalls; report.metricsTriangles = view.metricsTriangles; }
                }
                finally
                {
                    target.Release(); Destroy(target);
                    controls.renderMode = oldMode; controls.worldCamera = oldCamera; controls.planeDistance = oldDistance;
                    ApplyControlsFraming();
                }
            }
        }

        private void CheckControlsFraming(ViewReview review)
        {
            review.hudFramingApplied=paddedCamera==ActiveCamera;
            review.hudPanelMeasurementValid=controlsMeasurementValid;
            review.hudFreeAreaPixels=controlsFreeArea;
            review.hudHeaderPixels=controlsHeaderArea;review.hudFooterPixels=controlsFooterArea;
            foreach(var unit in Units)
            {
                if(unit==null||!unit.gameObject.activeInHierarchy||!TryBounds(unit,out var bounds))continue;
                var center=ActiveCamera.WorldToScreenPoint(bounds.center);
                bool centerInside=center.z>0&&controlsFreeArea.Contains(new Vector2(center.x,center.y)),complete=true;
                if(centerInside){review.centersInFreeArea++;review.unitsInFreeArea.Add(unit.Kind.ToString());}
                for(int i=0;i<8;i++)
                {
                    var corner=bounds.center+Vector3.Scale(bounds.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));
                    var point=ActiveCamera.WorldToScreenPoint(corner);
                    if(point.z<=0||!controlsFreeArea.Contains(new Vector2(point.x,point.y)))complete=false;
                }
                if(complete)review.boundsInFreeArea++;
                if(unit==Selected){review.selectedCenterInFreeArea=centerInside;review.selectedBoundsInFreeArea=complete;}
            }
        }

        private void CheckFrustum(ViewReview review)
        {
            var planes = GeometryUtility.CalculateFrustumPlanes(ActiveCamera);
            foreach (var unit in Units)
            {
                if (unit == null || !unit.gameObject.activeInHierarchy || !TryBounds(unit,out var bounds)) continue;
                Vector3 center = ActiveCamera.WorldToViewportPoint(bounds.center);
                if (center.z <= 0 || center.x < 0 || center.x > 1 || center.y < 0 || center.y > 1 || !GeometryUtility.TestPlanesAABB(planes,bounds)) continue;
                review.countOnScreen++; review.unitsOnScreen.Add(unit.Kind.ToString()); bool complete = true;
                for (int i=0;i<8;i++)
                {
                    var point = ActiveCamera.WorldToViewportPoint(bounds.center+Vector3.Scale(bounds.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1)));
                    if (point.z <= 0 || point.x < 0 || point.x > 1 || point.y < 0 || point.y > 1) complete = false;
                }
                if (complete) review.fullyInFrame++;
            }
        }

        private static bool TryBounds(ArtOverhaulUnit unit,out Bounds bounds)
        {
            bounds = default; var group = unit.GetComponent<LODGroup>();
            var lods = group == null ? Array.Empty<LOD>() : group.GetLODs();
            if (lods.Length == 0 || lods[0].renderers.Length == 0 || !(lods[0].renderers[0] is SkinnedMeshRenderer renderer) || renderer.sharedMesh == null) return false;
            var local = renderer.sharedMesh.bounds; bool first = true;
            for (int i=0;i<8;i++)
            {
                Vector3 point = renderer.transform.TransformPoint(local.center+Vector3.Scale(local.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1)));
                if (first) { bounds = new Bounds(point,Vector3.zero); first = false; } else bounds.Encapsulate(point);
            }
            return !first;
        }

        private sealed class RenderCounter : IDisposable
        {
            private ProfilerRecorder recorder;
            private readonly MetricReview result;
            private double total;
            public RenderCounter(string name)
            {
                result = new MetricReview { name=name };
                try
                {
                    recorder = ProfilerRecorder.StartNew(ProfilerCategory.Render,name,1);
                    if (!recorder.Valid) result.reason = "Unavailable: this player/platform does not expose a valid recorder.";
                }
                catch (Exception error) { result.reason = "Unavailable: "+error.Message; }
            }
            public void Sample()
            {
                if (!recorder.Valid || recorder.Count == 0) return;
                long value = recorder.LastValue; if (value < 0) return;
                result.samples++; total += value;
                result.minimum = result.minimum < 0 ? value : Math.Min(result.minimum,value); result.maximum = Math.Max(result.maximum,value);
            }
            public MetricReview Report()
            {
                result.available = recorder.Valid && result.samples > 0 && result.maximum > 0;
                if (result.available) { result.mean = total/result.samples; result.reason = "Available regular-frame render counter after capture requests were drained; not GPU timing."; }
                else { result.mean = result.minimum = result.maximum = -1; if (result.samples > 0) result.reason = "Unavailable: all observed render samples were zero."; }
                return result;
            }
            public void Dispose() { if (recorder.Valid) recorder.Dispose(); }
        }
    }
}
