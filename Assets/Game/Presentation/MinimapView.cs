using System;
using Emberfield.Simulation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Emberfield.Presentation
{
    // A local observation of the map. Static terrain never consults live navigation occupancy.
    public sealed class MinimapView : IDisposable
    {
        private static readonly Color32 Unknown = new Color32(10, 19, 24, 255);
        private static readonly Color32 Ground = new Color32(105, 119, 88, 255);
        private static readonly Color32 RememberedGround = new Color32(44, 58, 48, 255);
        private static readonly Color32 Rock = new Color32(150, 144, 120, 255);
        private static readonly Color32 RememberedRock = new Color32(68, 69, 58, 255);
        private static readonly Color32 Sea = new Color32(46, 104, 150, 255);
        private static readonly Color32 RememberedSea = new Color32(26, 52, 72, 255);
        private static readonly Color32 Own = new Color32(65, 174, 233, 255);
        private static readonly Color32 Enemy = new Color32(219, 81, 64, 255);
        private static readonly Color32 Beacon = new Color32(244, 196, 90, 255);
        private static readonly Color32 Viewport = new Color32(240, 242, 228, 255);
        private readonly MatchController match;
        private readonly Color32[] pixels;
        private readonly bool[] blockedTerrain, deepWater;
        private readonly int[] pixelCells;
        private readonly int worldWidth, worldHeight;
        private readonly Vector2[] footprint = new Vector2[4];
        private readonly Plane groundPlane = new Plane(Vector3.up, Vector3.zero);
        private readonly MinimapPointer pointerInput;
        private readonly Text caption;
        private float nextPaint;
        private bool disposed;
        public RectTransform Root { get; }
        public RawImage Image { get; }
        public Texture2D Texture { get; }

        public MinimapView(MatchController match, Transform parent, Font font)
        {
            this.match = match;
            var map = match.World.Map;
            worldWidth = map.WidthCells * map.CellSizeMillimetres;
            worldHeight = map.HeightCells * map.CellSizeMillimetres;
            float textureScale = 192f / Mathf.Max(map.WidthCells, map.HeightCells);
            int width = Mathf.Max(1, Mathf.RoundToInt(map.WidthCells * textureScale));
            int height = Mathf.Max(1, Mathf.RoundToInt(map.HeightCells * textureScale));
            Texture = new Texture2D(width, height, TextureFormat.RGBA32, false, true)
            { name = "Local minimap", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            pixels = new Color32[width * height];
            blockedTerrain = new bool[map.WidthCells * map.HeightCells];
            foreach (var cell in map.BlockedCells) blockedTerrain[cell.Z * map.WidthCells + cell.X] = true;
            // The sea the ships sail reads as water, not as rock.
            deepWater = new bool[map.WidthCells * map.HeightCells];
            foreach (var cell in map.WaterCells ?? Array.Empty<GridCell>())
                if (cell.X >= 0 && cell.Z >= 0 && cell.X < map.WidthCells && cell.Z < map.HeightCells && blockedTerrain[cell.Z * map.WidthCells + cell.X]) deepWater[cell.Z * map.WidthCells + cell.X] = true;
            // The cell under each pixel's centre, worked out once: the map does not move.
            pixelCells = new int[width * height];
            for (int y = 0, index = 0; y < height; y++) for (int x = 0; x < width; x++, index++)
            {
                var point = new SimPoint((int)((x + .5f) * worldWidth / width), (int)((y + .5f) * worldHeight / height));
                pixelCells[index] = point.Z / map.CellSizeMillimetres * map.WidthCells + point.X / map.CellSizeMillimetres;
            }
            Root = Rect("Minimap", parent);
            Root.anchorMin = Root.anchorMax = Root.pivot = Vector2.one;
            Root.anchoredPosition = new Vector2(-18, -158);
            AlphaTheme.StylePanel(Root);
            var imageRect = Rect("Map", Root);
            imageRect.anchorMin = imageRect.anchorMax = imageRect.pivot = Vector2.zero;
            imageRect.anchoredPosition = new Vector2(6, 6);
            Image = imageRect.gameObject.AddComponent<RawImage>(); Image.texture = Texture;
            pointerInput = imageRect.gameObject.AddComponent<MinimapPointer>(); pointerInput.View = this;
            var captionRect = Rect("Caption", Root);
            captionRect.anchorMin = new Vector2(0, 1); captionRect.anchorMax = Vector2.one;
            captionRect.offsetMin = new Vector2(6, -28); captionRect.offsetMax = new Vector2(-6, -4);
            caption = captionRect.gameObject.AddComponent<Text>(); caption.font = AlphaTheme.Strong; caption.fontSize = 16;
            caption.color = AlphaTheme.Gold;
            caption.alignment = TextAnchor.MiddleCenter; caption.raycastTarget = false;
            caption.resizeTextForBestFit = true; caption.resizeTextMinSize = 10; caption.resizeTextMaxSize = 16;
            // Whether this session behaves as a phone (the device, or -emberfieldMobileTier) does not change
            // while it runs, so the hint is written once instead of read back every frame.
            caption.text = HintText(MobileQuality.Requested);
            Refresh();
        }

        public Vector2 MapUv(SimPoint point) => new Vector2(point.X / (float)worldWidth, point.Z / (float)worldHeight);
        public SimPoint PointAtUv(Vector2 uv) => new SimPoint(
            Mathf.RoundToInt(Mathf.Clamp01(uv.x) * worldWidth), Mathf.RoundToInt(Mathf.Clamp01(uv.y) * worldHeight));

        public void Refresh()
        {
            if (disposed) return;
            if (Blocked()) pointerInput.Cancel();
            var parent = (RectTransform)Root.parent;
            bool phone = parent.rect.width / Mathf.Max(1, parent.rect.height) >= 1.6f;
            var size = phone ? new Vector2(180, 135) : new Vector2(220, 165);
            if (Image.rectTransform.sizeDelta != size)
            { Image.rectTransform.sizeDelta = size; Root.sizeDelta = size + new Vector2(12, 36); }
            if (Time.unscaledTime < nextPaint) return;
            nextPaint = Time.unscaledTime + .2f;
            using (PresentationMarkers.Minimap.Auto()) Paint();
        }

        // The desktop hint names both actions; a phone's hint only has room for the touch one.
        private static string HintText(bool phone) => phone ? "MAP · hold to move" : "MAP · tap to look · right-click to move";

        private void Paint()
        {
            var world = match.World; var vision = world.Vision; var map = world.Map;
            int width = Texture.width, height = Texture.height;
            // The fog's own pass over the map (the local player's sight), read per pixel instead of asking twice a pixel.
            var cells = match.View.Fog?.Cells;
            for (int y = 0, index = 0; y < height; y++) for (int x = 0; x < width; x++, index++)
            {
                int cell = pixelCells[index];
                bool explored, visible;
                // No Vision system (practice/sandbox worlds) means nothing is hidden: the whole map counts as
                // explored and in sight, same as a scout standing everywhere at once.
                if (vision == null) { explored = true; visible = true; }
                else if (cells != null) { explored = (cells[cell] & 1) != 0; visible = (cells[cell] & 2) != 0; }
                else
                {
                    var point = new SimPoint((int)((x + .5f) * worldWidth / width), (int)((y + .5f) * worldHeight / height));
                    explored = vision.IsExplored(MatchController.LocalPlayer, point); visible = explored && vision.IsVisible(MatchController.LocalPlayer, point);
                }
                if (!explored) { pixels[index] = Unknown; continue; }
                bool rock = blockedTerrain[cell];
                pixels[index] = deepWater[cell] ? (visible ? Sea : RememberedSea) : rock ? (visible ? Rock : RememberedRock) : (visible ? Ground : RememberedGround);
            }
            // By index: a foreach through the world's read-only lists boxes an enumerator on every paint.
            var resources = world.Resources;
            for (int i = 0; i < resources.Count; i++)
            {
                var resource = resources[i];
                if (resource.RemainingAmount <= 0) continue;
                // A seam you have already walked past stays on the map, dimmed. Terrain does not move, and
                // forgetting where the ore was is what made metal impossible to find a second time.
                bool live = vision == null || vision.IsEntityVisible(MatchController.LocalPlayer, resource.Id);
                if (!live && !vision.IsExplored(MatchController.LocalPlayer, resource.Position)) continue;
                var color = resource.Kind == ResourceKind.Food ? new Color32(231, 157, 75, 255)
                    : resource.Kind == ResourceKind.Wood ? new Color32(101, 191, 98, 255)
                    : resource.Kind == ResourceKind.Metal ? new Color32(153, 201, 205, 255) : new Color32(198, 192, 176, 255);
                if (!live) color = new Color32((byte)(color.r * .62f), (byte)(color.g * .62f), (byte)(color.b * .62f), 255);
                Marker(MapUv(resource.Position), resource.Kind == ResourceKind.Metal ? 2 : 1, color, true);
            }
            var buildings = world.Buildings;
            for (int i = 0; i < buildings.Count; i++)
                if (vision == null || vision.IsEntityVisible(MatchController.LocalPlayer, buildings[i].Id))
                    Marker(MapUv(buildings[i].Position), 3, buildings[i].OwnerId == MatchController.LocalPlayer ? Own : Enemy, false);
            var units = world.Units;
            for (int i = 0; i < units.Count; i++)
                if (vision == null || vision.IsEntityVisible(MatchController.LocalPlayer, units[i].Id))
                    Marker(MapUv(units[i].Position), units[i].Domain == MovementDomain.Water ? 2 : 1, units[i].OwnerId == MatchController.LocalPlayer ? Own : Enemy, units[i].Domain == MovementDomain.Water);
            // A practice/sandbox world has no Match, so it has no Dominion beacons to paint.
            if (world.Match != null && world.Match.Mode == VictoryMode.Dominion)
            {
                var objectives = world.Match.Objectives;
                for (int i = 0; i < objectives.Count; i++)
                {
                    var objective = objectives[i];
                    Marker(MapUv(objective.Position), 4, Beacon, true);
                    Marker(MapUv(objective.Position), 2, objective.OwnerId == 0 ? Unknown
                        : objective.OwnerId == MatchController.LocalPlayer ? Own : Enemy, true);
                }
            }
            PaintFootprint();
            Texture.SetPixels32(pixels); Texture.Apply(false, false);
        }

        private void PaintFootprint()
        {
            for (int corner = 0; corner < 4; corner++)
            {
                var screen = new Vector3(corner == 1 || corner == 2 ? 1 : 0, corner >= 2 ? 1 : 0, 0);
                var ray = match.Rig.Camera.ViewportPointToRay(screen);
                if (!groundPlane.Raycast(ray, out float distance)) return;
                footprint[corner] = MapUv(DefinitionLoader.ToSimulation(ray.GetPoint(distance)));
            }
            for (int corner = 0; corner < 4; corner++) Line(footprint[corner], footprint[(corner + 1) % 4]);
        }

        private void Marker(Vector2 uv, int radius, Color32 color, bool diamond)
        {
            int centreX = PixelX(uv.x), centreY = PixelY(uv.y);
            for (int y = -radius; y <= radius; y++) for (int x = -radius; x <= radius; x++)
                if (!diamond || Math.Abs(x) + Math.Abs(y) <= radius) SetPixel(centreX + x, centreY + y, color);
        }
        private void Line(Vector2 from, Vector2 to)
        {
            int x = PixelX(from.x), y = PixelY(from.y), targetX = PixelX(to.x), targetY = PixelY(to.y);
            int dx = Math.Abs(targetX - x), dy = Math.Abs(targetY - y), sx = x < targetX ? 1 : -1, sy = y < targetY ? 1 : -1;
            int error = dx - dy;
            while (true)
            {
                SetPixel(x, y, Viewport);
                if (x == targetX && y == targetY) break;
                int twice = error * 2;
                if (twice > -dy) { error -= dy; x += sx; }
                if (twice < dx) { error += dx; y += sy; }
            }
        }
        private int PixelX(float u) => Mathf.Clamp(Mathf.FloorToInt(u * Texture.width), 0, Texture.width - 1);
        private int PixelY(float v) => Mathf.Clamp(Mathf.FloorToInt(v * Texture.height), 0, Texture.height - 1);
        private void SetPixel(int x, int y, Color32 color)
        { if (x >= 0 && x < Texture.width && y >= 0 && y < Texture.height) pixels[y * Texture.width + x] = color; }
        private static RectTransform Rect(string name, Transform parent)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false); return rect;
        }

        private bool Blocked() => match.OfflineControls.BlocksWorldInput || match.Research.IsOpen || match.Factions.IsOpen ||
            match.Online?.BlocksWorldInput == true || match.Alpha?.IsOpen == true;

        private bool TryPointAt(Vector2 screenPosition, Camera camera, out SimPoint point)
        {
            point = default;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(Image.rectTransform, screenPosition, camera, out var local)) return false;
            var rect = Image.rectTransform.rect;
            point = PointAtUv(new Vector2((local.x - rect.xMin) / rect.width, (local.y - rect.yMin) / rect.height));
            return true;
        }

        internal void Look(PointerEventData pointer)
        {
            if (disposed || pointer.button != PointerEventData.InputButton.Left || Blocked()) return;
            if (TryPointAt(pointer.position, pointer.pressEventCamera, out var point)) match.Rig.Focus(DefinitionLoader.ToWorld(point));
        }

        // The same order a right-click on the ground issues: the selected units of the local player walk to the
        // point under the cursor, in whatever formation is current. A touch long-press reaches this too.
        internal void Order(Vector2 screenPosition, Camera camera)
        {
            if (disposed || Blocked()) return;
            var units = match.SelectedUnits();
            if (units.Length == 0) return;
            if (!TryPointAt(screenPosition, camera, out var point)) return;
            match.OrderGround(point);
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true; UnityEngine.Object.Destroy(Texture); UnityEngine.Object.Destroy(Root.gameObject);
        }
    }

    // Left press/drag only ever looks. A right-click orders the current selection there at once, the desktop way;
    // a touch instead has to hold still past the long-press threshold, since a tap or a drag must keep looking.
    internal sealed class MinimapPointer : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler, IDragHandler, IInitializePotentialDragHandler
    {
        internal MinimapView View;
        private int pointerId = int.MinValue;
        private bool longPressArmed, longPressFired;
        private float pressStartTime;
        private Vector2 pressStartPosition;
        private Camera pressCamera;
        public void OnInitializePotentialDrag(PointerEventData pointer) { pointer.useDragThreshold = false; }
        public void OnPointerClick(PointerEventData pointer)
        {
            if (pointer.button == PointerEventData.InputButton.Right) View.Order(pointer.position, pointer.pressEventCamera);
        }
        public void OnPointerDown(PointerEventData pointer)
        {
            if (pointerId != int.MinValue || pointer.button != PointerEventData.InputButton.Left) return;
            pointerId = pointer.pointerId; View.Look(pointer);
            // Only a touch needs the hold: a mouse already has its right button for an immediate order.
            longPressArmed = pointer is ExtendedPointerEventData extended && extended.pointerType == UIPointerType.Touch;
            longPressFired = false; pressStartTime = Time.unscaledTime; pressStartPosition = pointer.position; pressCamera = pointer.pressEventCamera;
        }
        public void OnDrag(PointerEventData pointer)
        {
            if (pointer.pointerId != pointerId) return;
            if (longPressArmed && !longPressFired && Vector2.Distance(pointer.position, pressStartPosition) >
                MobileInputSettings.ScreenPixels(MobileInputSettings.DragThreshold, Screen.height))
                longPressArmed = false; // Moving before the hold fires makes it an ordinary drag, which only looks.
            View.Look(pointer);
        }
        public void OnPointerUp(PointerEventData pointer) { if (pointer.pointerId == pointerId) Reset(); }
        // OnDrag only runs when the finger moves; a still hold needs its own clock to notice the threshold passing.
        private void Update()
        {
            if (!longPressArmed || longPressFired || pointerId == int.MinValue) return;
            if (Time.unscaledTime - pressStartTime < MobileInputSettings.LongPressSeconds) return;
            longPressFired = true;
            View.Order(pressStartPosition, pressCamera);
        }
        internal void Cancel() => Reset();
        private void Reset() { pointerId = int.MinValue; longPressArmed = longPressFired = false; }
        private void OnDisable() { Reset(); }
        private void OnApplicationFocus(bool focused) { if (!focused) Reset(); }
        private void OnApplicationPause(bool paused) { if (paused) Reset(); }
    }
}
