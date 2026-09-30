using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace Emberfield.Presentation
{
    // A static art viewer. Selection and camera controls do not issue gameplay commands.
    public sealed class KingdomPremiumReview : MonoBehaviour
    {
        public Camera CloseCamera, MidCamera, RTSCamera;
        public GameObject[] Units;
        public RenderPipelineAsset Pipeline;
        public Camera ActiveCamera { get; private set; }
        public GameObject Selected => Units != null && selectedIndex >= 0 && selectedIndex < Units.Length ? Units[selectedIndex] : null;

        private const float HeaderHeight = 36, FooterHeight = 56;
        private static readonly string[] Names = { "Recolector", "Guerrero", "Héroe" };
        private static readonly string[] Keys = { "worker", "warrior", "hero" };
        private static readonly string[] Views = { "Cerca", "Media", "RTS" };
        private readonly CameraState[] authored = new CameraState[3];
        private readonly List<Button> buttons = new List<Button>();
        private readonly List<RaycastResult> hits = new List<RaycastResult>();
        private Canvas canvas;
        private RectTransform safeArea;
        private GameObject panels, restoreButton;
        private Text caption;
        private Camera paddedCamera;
        private CameraState beforePadding;
        private Rect freeArea, lastSafeArea;
        private Vector2Int lastResolution;
        private int selectedIndex, viewIndex = 2;
        private bool reviewing, dragging;
        private string reviewDirectory;
        private ReviewReport report;
        private RenderPipelineAsset previousPipeline;

        private sealed class CameraState
        {
            public Vector3 Position;
            public Quaternion Rotation;
            public float Size, FieldOfView;
            public bool Orthographic;
            public CameraState(Camera camera) { Position = camera.transform.position; Rotation = camera.transform.rotation; Size = camera.orthographicSize; FieldOfView = camera.fieldOfView; Orthographic = camera.orthographic; }
            public void Apply(Camera camera) { camera.transform.SetPositionAndRotation(Position, Rotation); camera.orthographic = Orthographic; camera.orthographicSize = Size; camera.fieldOfView = FieldOfView; }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ConfigureReviewStartup()
        {
            if (!string.IsNullOrEmpty(Argument(Environment.GetCommandLineArgs(), "-kingdomPremiumReview"))) Application.runInBackground = true;
        }

        private void Awake()
        {
            reviewDirectory = Argument(Environment.GetCommandLineArgs(), "-kingdomPremiumReview");
            reviewing = !string.IsNullOrEmpty(reviewDirectory);
            if (reviewing) Application.runInBackground = true;
            previousPipeline = QualitySettings.renderPipeline;
            if (Pipeline != null) QualitySettings.renderPipeline = Pipeline;
        }

        private void Start()
        {
            SetView(2);
            SelectUnit(0);
            BuildControls();
            if (reviewing)
            {
                report = new ReviewReport { buildGuid = Application.buildGUID, unityVersion = Application.unityVersion, generatedUtc = DateTime.UtcNow.ToString("o"), width = Screen.width, height = Screen.height, graphicsDevice = SystemInfo.graphicsDeviceName };
                StartCoroutine(GuardReview());
            }
        }

        private void OnDestroy()
        {
            if (Application.isPlaying && QualitySettings.renderPipeline == Pipeline) QualitySettings.renderPipeline = previousPipeline;
        }

        private static string Argument(string[] args, string key)
        {
            int i = Array.IndexOf(args, key);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }

        // Both entry points can be called by the editor baker before Start builds a Canvas.
        public void SetView(int index)
        {
            RemovePadding();
            var cameras = new[] { CloseCamera, MidCamera, RTSCamera };
            for (int i = 0; i < cameras.Length; i++) if (cameras[i] != null && authored[i] == null) authored[i] = new CameraState(cameras[i]);
            viewIndex = Mathf.Clamp(index, 0, 2);
            for (int i = 0; i < cameras.Length; i++) if (cameras[i] != null) cameras[i].enabled = i == viewIndex;
            ActiveCamera = cameras[viewIndex];
            if (ActiveCamera != null && authored[viewIndex] != null) authored[viewIndex].Apply(ActiveCamera);
            if (viewIndex == 0) FrameSelected();
            ApplyPadding();
            RefreshCaption();
        }

        public void SelectUnit(int index)
        {
            if (Units == null || index < 0 || index >= Units.Length || Units[index] == null) return;
            selectedIndex = index;
            if (viewIndex == 0)
            {
                RemovePadding();
                if (authored[0] != null) authored[0].Apply(CloseCamera);
                FrameSelected();
                ApplyPadding();
            }
            RefreshCaption();
        }

        private void FrameSelected()
        {
            if (CloseCamera == null || !TryBounds(Selected, out var bounds)) return;
            Quaternion rotation = CloseCamera.transform.rotation;
            Vector3 extents = Vector3.zero;
            for (int i = 0; i < 8; i++)
            {
                Vector3 local = Quaternion.Inverse(rotation) * (Corner(bounds, i) - bounds.center);
                extents = Vector3.Max(extents, new Vector3(Mathf.Abs(local.x), Mathf.Abs(local.y), Mathf.Abs(local.z)));
            }
            float extent = Mathf.Max(extents.y, extents.x / Mathf.Max(.1f, CloseCamera.aspect));
            float distance;
            if (CloseCamera.orthographic)
            {
                CloseCamera.orthographicSize = Mathf.Max(.1f, extent * 1.22f);
                distance = Mathf.Max(12, extents.z * 3 + 2);
            }
            else
            {
                float tangent = Mathf.Tan(CloseCamera.fieldOfView * Mathf.Deg2Rad * .5f);
                distance = Mathf.Max(CloseCamera.nearClipPlane + extents.z + .1f, extent / Mathf.Max(.01f, tangent) * 1.18f + extents.z);
            }
            CloseCamera.transform.position = bounds.center - CloseCamera.transform.forward * distance;
        }

        private static bool TryBounds(GameObject unit, out Bounds bounds)
        {
            bounds = default;
            if (unit == null) return false;
            bool found = false;
            foreach (var renderer in unit.GetComponentsInChildren<Renderer>(true))
            {
                if (!renderer.enabled || !renderer.gameObject.activeInHierarchy || !(renderer is MeshRenderer || renderer is SkinnedMeshRenderer)) continue;
                if (!found) { bounds = renderer.bounds; found = true; } else bounds.Encapsulate(renderer.bounds);
            }
            return found;
        }

        private static Vector3 Corner(Bounds bounds, int i) => bounds.center + Vector3.Scale(bounds.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));

        private void Update()
        {
            UpdateSafeArea();
            if (reviewing || ActiveCamera == null) return;
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.hKey.wasPressedThisFrame) ToggleControls();
                if (keyboard.digit1Key.wasPressedThisFrame) SetView(0);
                if (keyboard.digit2Key.wasPressedThisFrame) SetView(1);
                if (keyboard.digit3Key.wasPressedThisFrame) SetView(2);
            }
            var mouse = Mouse.current;
            if (mouse != null)
            {
                Vector2 position = mouse.position.ReadValue();
                if (mouse.leftButton.wasPressedThisFrame)
                {
                    dragging = !OverUI(position);
                    if (dragging) PickUnit(position);
                }
                if (mouse.leftButton.wasReleasedThisFrame) dragging = false;
                if (!OverUI(position))
                {
                    float wheel = mouse.scroll.ReadValue().y;
                    if (Mathf.Abs(wheel) > .01f)
                    {
                        RemovePadding();
                        if (ActiveCamera.orthographic) ActiveCamera.orthographicSize = Mathf.Clamp(ActiveCamera.orthographicSize * Mathf.Exp(-wheel * .001f), .35f, 80);
                        else ActiveCamera.fieldOfView = Mathf.Clamp(ActiveCamera.fieldOfView * Mathf.Exp(-wheel * .0007f), 25, 65);
                        ApplyPadding();
                    }
                }
                if (dragging && mouse.leftButton.isPressed && viewIndex == 0 && TryBounds(Selected, out var bounds))
                {
                    Vector2 delta = mouse.delta.ReadValue();
                    if (delta.sqrMagnitude > .02f)
                    {
                        RemovePadding();
                        float orbitDistance = Vector3.Distance(CloseCamera.transform.position, bounds.center);
                        Vector3 angles = CloseCamera.transform.eulerAngles;
                        float pitch = angles.x > 180 ? angles.x - 360 : angles.x;
                        CloseCamera.transform.rotation = Quaternion.Euler(Mathf.Clamp(pitch - delta.y * .18f, -10, 70), angles.y + delta.x * .18f, 0);
                        CloseCamera.transform.position = bounds.center - CloseCamera.transform.forward * Mathf.Max(.1f, orbitDistance);
                        ApplyPadding();
                    }
                }
            }
            var touch = Touchscreen.current;
            if (touch != null && touch.primaryTouch.press.wasPressedThisFrame)
            {
                Vector2 position = touch.primaryTouch.position.ReadValue();
                if (!OverUI(position)) PickUnit(position);
            }
        }

        private bool OverUI(Vector2 position)
        {
            if (EventSystem.current == null) return false;
            hits.Clear();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = position }, hits);
            return hits.Count > 0;
        }

        private void PickUnit(Vector2 position)
        {
            if (Units == null || ActiveCamera == null) return;
            Ray ray = ActiveCamera.ScreenPointToRay(position);
            float nearest = float.PositiveInfinity;
            int picked = -1;
            for (int i = 0; i < Units.Length; i++) if (TryBounds(Units[i], out var bounds) && bounds.IntersectRay(ray, out float distance) && distance < nearest) { nearest = distance; picked = i; }
            if (picked >= 0) SelectUnit(picked);
        }

        public void ToggleControls()
        {
            if (panels == null) return;
            panels.SetActive(!panels.activeSelf);
            restoreButton.SetActive(!panels.activeSelf);
            ApplyPadding();
        }

        private void RemovePadding()
        {
            if (paddedCamera != null && beforePadding != null) beforePadding.Apply(paddedCamera);
            paddedCamera = null; beforePadding = null;
        }

        private float PixelScale() => Mathf.Sqrt((Screen.width / 1600f) * (Screen.height / 900f));

        private void ApplyPadding()
        {
            RemovePadding();
            if (ActiveCamera == null || canvas == null || !canvas.gameObject.activeInHierarchy || panels == null || !panels.activeInHierarchy) return;
            Rect safe = Screen.safeArea, pixels = ActiveCamera.pixelRect;
            float scale = PixelScale();
            freeArea = Rect.MinMaxRect(Mathf.Max(pixels.xMin, safe.xMin) + 8, Mathf.Max(pixels.yMin, safe.yMin + FooterHeight * scale) + 6,
                Mathf.Min(pixels.xMax, safe.xMax) - 8, Mathf.Min(pixels.yMax, safe.yMax - HeaderHeight * scale) - 6);
            if (freeArea.width < 32 || freeArea.height < 32) return;
            paddedCamera = ActiveCamera; beforePadding = new CameraState(ActiveCamera);
            float fraction = Mathf.Min(freeArea.width / pixels.width, freeArea.height / pixels.height);
            float size;
            Vector3 position = beforePadding.Position;
            if (ActiveCamera.orthographic) { size = beforePadding.Size / fraction; ActiveCamera.orthographicSize = size; }
            else
            {
                Vector3 focus = position + ActiveCamera.transform.forward * 10;
                if (viewIndex == 0 && TryBounds(Selected, out var selectedBounds)) focus = selectedBounds.center;
                else if (TryGroupBounds(out var groupBounds)) focus = groupBounds.center;
                float depth = Mathf.Max(1, Vector3.Dot(focus - position, ActiveCamera.transform.forward));
                float paddedDepth = depth / fraction;
                size = paddedDepth * Mathf.Tan(beforePadding.FieldOfView * Mathf.Deg2Rad * .5f);
                position -= ActiveCamera.transform.forward * (paddedDepth - depth);
            }
            Vector2 center = new Vector2((freeArea.center.x - pixels.xMin) / pixels.width, (freeArea.center.y - pixels.yMin) / pixels.height);
            ActiveCamera.transform.position = position - ActiveCamera.transform.up * ((center.y - .5f) * 2 * size)
                - ActiveCamera.transform.right * ((center.x - .5f) * 2 * size * ActiveCamera.aspect);
            Canvas.ForceUpdateCanvases();
        }

        private bool TryGroupBounds(out Bounds bounds)
        {
            bounds = default; bool found = false;
            if (Units == null) return false;
            foreach (var unit in Units) if (TryBounds(unit, out var item)) { if (!found) { bounds = item; found = true; } else bounds.Encapsulate(item); }
            return found;
        }

        private void BuildControls()
        {
            var go = new GameObject("Kingdom viewer controls", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.transform.SetParent(transform, false); canvas = go.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = go.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900); scaler.matchWidthOrHeight = .5f;
            if (EventSystem.current == null)
            {
                var input = new GameObject("Kingdom viewer input", typeof(EventSystem), typeof(InputSystemUIInputModule));
                input.transform.SetParent(transform, false); input.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            }
            safeArea = new GameObject("Safe area", typeof(RectTransform)).GetComponent<RectTransform>(); safeArea.SetParent(go.transform, false);
            panels = new GameObject("Chrome", typeof(RectTransform)); panels.transform.SetParent(safeArea, false); Stretch((RectTransform)panels.transform);
            var header = Panel(panels.transform, "Title", true, HeaderHeight);
            var title = Label(header, "EMBERFIELD   /   KINGDOM", 18, new Color(.89f, .78f, .56f));
            title.rectTransform.anchorMin = new Vector2(0, 0); title.rectTransform.anchorMax = new Vector2(.5f, 1); title.rectTransform.offsetMin = new Vector2(18, 0); title.rectTransform.offsetMax = Vector2.zero;
            caption = Label(header, "", 13, new Color(.77f, .80f, .78f)); caption.alignment = TextAnchor.MiddleRight;
            caption.rectTransform.anchorMin = new Vector2(.5f, 0); caption.rectTransform.anchorMax = Vector2.one; caption.rectTransform.offsetMin = Vector2.zero; caption.rectTransform.offsetMax = new Vector2(-18, 0);
            var footer = Panel(panels.transform, "Selection and cameras", false, FooterHeight);
            var row = new GameObject("Buttons", typeof(RectTransform), typeof(HorizontalLayoutGroup)); row.transform.SetParent(footer, false); Stretch((RectTransform)row.transform, 6);
            var layout = row.GetComponent<HorizontalLayoutGroup>(); layout.spacing = 6; layout.padding = new RectOffset(10, 10, 0, 0); layout.childControlWidth = layout.childControlHeight = true; layout.childForceExpandWidth = true;
            for (int i = 0; i < 3; i++) { int choice = i; AddButton(row.transform, Names[i], () => SelectUnit(choice)); }
            for (int i = 0; i < 3; i++) { int choice = i; AddButton(row.transform, Views[i], () => SetView(choice)); }
            AddButton(row.transform, "Ocultar · H", ToggleControls);
            restoreButton = AddButton(safeArea, "Mostrar · H", ToggleControls).gameObject;
            var restore = (RectTransform)restoreButton.transform; restore.anchorMin = restore.anchorMax = Vector2.one; restore.pivot = Vector2.one; restore.anchoredPosition = new Vector2(-10, -6); restore.sizeDelta = new Vector2(116, 44); restoreButton.SetActive(false);
            UpdateSafeArea(); RefreshCaption(); ApplyPadding();
        }

        private static Transform Panel(Transform parent, string name, bool top, float height)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image)); go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform; rect.anchorMin = new Vector2(0, top ? 1 : 0); rect.anchorMax = new Vector2(1, top ? 1 : 0);
            rect.offsetMin = new Vector2(0, top ? -height : 0); rect.offsetMax = new Vector2(0, top ? 0 : height);
            go.GetComponent<Image>().color = new Color(.035f, .05f, .05f, .92f); return go.transform;
        }

        private Button AddButton(Transform parent, string text, UnityEngine.Events.UnityAction action)
        {
            var go = new GameObject(text, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement)); go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = new Color(.11f, .145f, .15f, .98f);
            var layout = go.GetComponent<LayoutElement>(); layout.minHeight = 44; layout.minWidth = 74; layout.flexibleWidth = 1;
            var button = go.GetComponent<Button>(); button.onClick.AddListener(action); buttons.Add(button);
            var label = Label(go.transform, text, 15, new Color(.94f, .93f, .88f)); label.alignment = TextAnchor.MiddleCenter; Stretch(label.rectTransform, 3); return button;
        }

        private static Text Label(Transform parent, string text, int size, Color color)
        {
            var go = new GameObject(text, typeof(RectTransform), typeof(Text)); go.transform.SetParent(parent, false);
            var label = go.GetComponent<Text>(); label.text = text; label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); label.fontSize = size; label.color = color;
            label.raycastTarget = false; label.alignment = TextAnchor.MiddleLeft; label.resizeTextForBestFit = true; label.resizeTextMinSize = 10; label.resizeTextMaxSize = size; return label;
        }

        private static void Stretch(RectTransform rect, float inset = 0) { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = Vector2.one * inset; rect.offsetMax = Vector2.one * -inset; }
        private void RefreshCaption() { if (caption != null) caption.text = Names[Mathf.Clamp(selectedIndex, 0, 2)] + "   ·   " + Views[viewIndex] + "   ·   Rueda: zoom" + (viewIndex == 0 ? "   /   Arrastrar: girar" : ""); }

        private void UpdateSafeArea()
        {
            if (safeArea == null || Screen.width < 1 || Screen.height < 1) return;
            var resolution = new Vector2Int(Screen.width, Screen.height); var safe = Screen.safeArea;
            if (resolution == lastResolution && safe == lastSafeArea) return;
            lastResolution = resolution; lastSafeArea = safe;
            safeArea.anchorMin = new Vector2(safe.xMin / Screen.width, safe.yMin / Screen.height); safeArea.anchorMax = new Vector2(safe.xMax / Screen.width, safe.yMax / Screen.height);
            safeArea.offsetMin = safeArea.offsetMax = Vector2.zero; ApplyPadding();
        }

        [Serializable] private sealed class MeshInfo { public string name, renderer, referenceKind; public int vertices, submeshes, triangles; }
        [Serializable] private sealed class MaterialInfo { public string name, shader; public bool supported; public List<string> textures = new List<string>(); }
        [Serializable] private sealed class UnitInfo { public string key, name; public Bounds bounds; public bool hasGeometry; public List<MeshInfo> meshes = new List<MeshInfo>(); public List<MaterialInfo> materials = new List<MaterialInfo>(); }
        [Serializable] private sealed class ViewInfo
        {
            public string id, image; public bool hasPixels, hudVisible, hudFramingApplied, selectedBoundsInFreeArea;
            public int width, height, unitsOnScreen, completeUnits, centersInFreeArea, boundsInFreeArea;
            public bool orthographic; public float orthographicSize, fieldOfView; public Vector3 cameraPosition, cameraEuler; public Rect hudFreeAreaPixels;
        }
        [Serializable] private sealed class ReviewReport
        {
            public string status = "running", buildGuid, unityVersion, generatedUtc, graphicsDevice;
            public string note = "Native Windows inspection of three static posed 3D characters. No animation, gameplay, frame-rate or mobile-performance claim. Runtime reports shared mesh/material names and geometry; asset GUID/path persistence requires editor validation. Screenshots use the active URP camera and RenderTexture, not the hidden window backbuffer.";
            public bool passed, assetPersistenceVerifiedInPlayer, controlsFit, controlsBlockWorldInput; public int width, height;
            public float hudReservedLogicalPixels = HeaderHeight + FooterHeight;
            public List<UnitInfo> units = new List<UnitInfo>(); public List<ViewInfo> views = new List<ViewInfo>(); public List<string> problems = new List<string>();
        }

        private IEnumerator GuardReview()
        {
            var stack = new Stack<IEnumerator>(); stack.Push(RunReview());
            while (stack.Count > 0)
            {
                object yielded = null; bool next = false; Exception failure = null;
                try { next = stack.Peek().MoveNext(); if (next) yielded = stack.Peek().Current; }
                catch (Exception ex) { failure = ex; }
                if (failure != null)
                {
                    report.problems.Add(failure.ToString());
                    while (stack.Count > 0) { try { (stack.Pop() as IDisposable)?.Dispose(); } catch (Exception ex) { report.problems.Add(ex.Message); } }
                    break;
                }
                if (!next) { (stack.Pop() as IDisposable)?.Dispose(); continue; }
                if (yielded is IEnumerator child) stack.Push(child); else yield return yielded;
            }
            report.passed = report.problems.Count == 0; report.status = report.passed ? "completed" : "failed";
            try { Directory.CreateDirectory(reviewDirectory); File.WriteAllText(Path.Combine(reviewDirectory, "review.json"), JsonUtility.ToJson(report, true)); }
            catch (Exception ex) { Debug.LogError(ex); report.passed = false; }
            Debug.Log("KINGDOM_PREMIUM_REVIEW " + (report.passed ? "PASSED" : "FAILED"));
            Application.Quit(report.passed ? 0 : 1);
        }

        private IEnumerator RunReview()
        {
            Directory.CreateDirectory(reviewDirectory);
            if (Units == null || Units.Length != 3) throw new InvalidOperationException("Expected Worker, Warrior and Hero in that order.");
            if (CloseCamera == null || MidCamera == null || RTSCamera == null || Pipeline == null) throw new InvalidOperationException("Missing camera or render pipeline.");
            for (int i = 0; i < Units.Length; i++) report.units.Add(InspectUnit(i));
            yield return null; yield return null;
            panels.SetActive(true); restoreButton.SetActive(false); canvas.gameObject.SetActive(false); RemovePadding();
            SelectUnit(0);
            for (int i = 0; i < 3; i++) { SetView(i); yield return Capture(new[] { "close", "medium", "rts" }[i], false); }
            for (int i = 0; i < 3; i++) { SelectUnit(i); SetView(0); yield return Capture(Keys[i], false); }
            SelectUnit(1); SetView(2); canvas.gameObject.SetActive(true); ApplyPadding();
            yield return null;
            yield return Capture("controls", true);
            yield return null; yield return null;
            report.controlsFit = true; report.controlsBlockWorldInput = true;
            Canvas.ForceUpdateCanvases();
            foreach (var button in buttons)
            {
                if (!button.gameObject.activeInHierarchy) continue;
                var corners = new Vector3[4]; ((RectTransform)button.transform).GetWorldCorners(corners);
                foreach (var corner in corners) if (!Screen.safeArea.Contains(RectTransformUtility.WorldToScreenPoint(null, corner))) report.controlsFit = false;
                Vector2 center = RectTransformUtility.WorldToScreenPoint(null, button.transform.position);
                if (!OverUI(center)) report.controlsBlockWorldInput = false;
            }
            if (!report.controlsFit || !report.controlsBlockWorldInput) report.problems.Add("Controls do not fit the safe area or do not block world selection.");
        }

        private UnitInfo InspectUnit(int index)
        {
            var unit = Units[index]; var info = new UnitInfo { key = Keys[index], name = unit == null ? "MISSING" : unit.name };
            info.hasGeometry = TryBounds(unit, out info.bounds);
            if (!info.hasGeometry) { report.problems.Add(Keys[index] + " has no visible geometry."); return info; }
            var seen = new HashSet<Material>();
            foreach (var renderer in unit.GetComponentsInChildren<Renderer>(true))
            {
                Mesh mesh = renderer is SkinnedMeshRenderer skin ? skin.sharedMesh : renderer.GetComponent<MeshFilter>()?.sharedMesh;
                if (mesh == null) continue;
                long indices = 0; for (int s = 0; s < mesh.subMeshCount; s++) indices += mesh.GetIndexCount(s);
                info.meshes.Add(new MeshInfo { name = mesh.name, renderer = renderer.name, referenceKind = "sharedMesh", vertices = mesh.vertexCount, submeshes = mesh.subMeshCount, triangles = (int)(indices / 3) });
                foreach (var material in renderer.sharedMaterials)
                {
                    if (material == null) { report.problems.Add(unit.name + " has a missing material."); continue; }
                    if (!seen.Add(material)) continue;
                    var m = new MaterialInfo { name = material.name, shader = material.shader == null ? "MISSING" : material.shader.name, supported = material.shader != null && material.shader.isSupported };
                    foreach (string property in material.GetTexturePropertyNames()) { Texture texture = material.GetTexture(property); if (texture != null) m.textures.Add(property + ": " + texture.name + " " + texture.width + "x" + texture.height); }
                    info.materials.Add(m); if (!m.supported) report.problems.Add(unit.name + " has an unsupported shader.");
                }
            }
            return info;
        }

        private IEnumerator Capture(string id, bool withHud)
        {
            var oldMode = canvas.renderMode; var oldCamera = canvas.worldCamera; float oldDistance = canvas.planeDistance;
            if (withHud) { canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = ActiveCamera; canvas.planeDistance = Mathf.Max(1, ActiveCamera.nearClipPlane + .1f); ApplyPadding(); }
            else RemovePadding();
            var view = new ViewInfo { id = id, image = id + ".png", width = Screen.width, height = Screen.height, hudVisible = withHud, hudFramingApplied = withHud && paddedCamera == ActiveCamera, hudFreeAreaPixels = withHud ? freeArea : default,
                orthographic = ActiveCamera.orthographic, orthographicSize = ActiveCamera.orthographicSize, fieldOfView = ActiveCamera.fieldOfView, cameraPosition = ActiveCamera.transform.position, cameraEuler = ActiveCamera.transform.eulerAngles };
            foreach (var unit in Units)
            {
                if (!TryBounds(unit, out var bounds)) continue;
                Vector3 center = ActiveCamera.WorldToViewportPoint(bounds.center); bool centerVisible = center.z > 0 && center.x >= 0 && center.x <= 1 && center.y >= 0 && center.y <= 1;
                if (centerVisible) view.unitsOnScreen++;
                bool complete = true, inFree = true;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 point = ActiveCamera.WorldToViewportPoint(Corner(bounds, i));
                    if (point.z <= 0 || point.x < 0 || point.x > 1 || point.y < 0 || point.y > 1) complete = false;
                    Vector3 screen = ActiveCamera.WorldToScreenPoint(Corner(bounds, i)); if (screen.z <= 0 || !freeArea.Contains(new Vector2(screen.x, screen.y))) inFree = false;
                }
                if (complete) view.completeUnits++;
                if (withHud)
                {
                    Vector3 screen = ActiveCamera.WorldToScreenPoint(bounds.center); if (screen.z > 0 && freeArea.Contains(new Vector2(screen.x, screen.y))) view.centersInFreeArea++;
                    if (inFree) view.boundsInFreeArea++; if (unit == Selected) view.selectedBoundsInFreeArea = inFree;
                }
            }
            if (id == "rts" && (view.unitsOnScreen != 3 || view.completeUnits != 3)) report.problems.Add("RTS must include all three complete characters.");
            if (withHud && (!view.hudFramingApplied || view.boundsInFreeArea != 3 || !view.selectedBoundsInFreeArea)) report.problems.Add("Controls overlap one or more characters.");
            var target = new RenderTexture(Screen.width, Screen.height, 24); target.Create();
            try
            {
                for (int i = 0; i < 3; i++) { yield return new WaitForEndOfFrame(); RenderPipeline.SubmitRenderRequest(ActiveCamera, new RenderPipeline.StandardRequest { destination = target }); yield return null; }
                view.hasPixels = PlayerSmoke.CaptureTarget(target, Path.Combine(reviewDirectory, view.image));
                if (!view.hasPixels) report.problems.Add("Empty native capture: " + view.image);
                report.views.Add(view);
            }
            finally
            {
                target.Release(); Destroy(target); canvas.renderMode = oldMode; canvas.worldCamera = oldCamera; canvas.planeDistance = oldDistance; ApplyPadding();
            }
        }
    }
}
