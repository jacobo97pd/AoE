using UnityEngine;

namespace Emberfield.Presentation
{
    // Camera owns view state only. Its projected ground footprint stays inside the map unless naval edge focus is enabled.
    public sealed class RtsCamera
    {
        private readonly Camera camera;
        private readonly Vector2 mapSize;
        private readonly bool allowMapEdgeFocus;
        private Vector3 focus;
        private Vector3? homeFocus;
        private float homeZoom;
        private readonly Plane ground = new Plane(Vector3.up, Vector3.zero);
        public Camera Camera => camera;
        public float MinimumZoom { get; set; } = 5;
        public float MaximumZoom { get; set; } = 22;
        public float PanSensitivity { get; set; } = 1;
        public float ZoomSensitivity { get; set; } = 1;

        public RtsCamera(Camera camera, Vector2 mapSize, bool allowMapEdgeFocus = false)
        {
            this.camera = camera;
            this.mapSize = mapSize;
            this.allowMapEdgeFocus = allowMapEdgeFocus;
            camera.orthographic = true;
            camera.orthographicSize = 8;
            camera.nearClipPlane = .1f;
            camera.farClipPlane = 150;
            camera.transform.rotation = Quaternion.Euler(55, 35, 0);
            Home();
        }

        public void Home()
        {
            zoomTarget = 0;
            if (homeFocus.HasValue)
            {
                camera.orthographicSize = homeZoom;
                Focus(homeFocus.Value);
                return;
            }
            bool wide = camera.aspect > 1.7f;
            camera.orthographicSize = wide ? 6 : 8;
            Focus(wide ? new Vector3(13.5f, 0, 13.5f) : new Vector3(15, 0, 16));
        }

        public bool GroundPoint(Vector2 screen, out Vector3 point)
        {
            var ray = camera.ScreenPointToRay(screen);
            if (ground.Raycast(ray, out float distance)) { point = ray.GetPoint(distance); return true; }
            point = default;
            return false;
        }

        public void SetHome(Vector3 point, float zoom) { homeFocus = point; homeZoom = Mathf.Clamp(zoom, MinimumZoom, MaximumZoom); Home(); }

        public void Pan(Vector2 from, Vector2 to)
        {
            if (GroundPoint(from, out var a) && GroundPoint(to, out var b)) Focus(focus + (a - b) * PanSensitivity);
        }

        // Travel without a grabbed point, for the keyboard and the screen edge. The direction is screen
        // relative so it follows a rotated view, and the distance is measured in view heights: the map
        // slides at the same visible speed however far the camera is zoomed out.
        public void PanBy(Vector2 screenDirection, float viewHeights)
        {
            var right = camera.transform.right; right.y = 0;
            var forward = camera.transform.up; forward.y = 0;
            if (right.sqrMagnitude < .000001f || forward.sqrMagnitude < .000001f) return;
            if (screenDirection.sqrMagnitude < .000001f || float.IsNaN(viewHeights) || float.IsInfinity(viewHeights)) return;
            float metres = camera.orthographicSize * 2 * viewHeights * PanSensitivity;
            Focus(focus + (right.normalized * screenDirection.x + forward.normalized * screenDirection.y) * metres);
        }

        public void Zoom(float scale)
        {
            zoomTarget = 0;
            camera.orthographicSize = Mathf.Clamp(camera.orthographicSize * Mathf.Pow(Mathf.Max(.01f, scale), ZoomSensitivity), MinimumZoom, MaximumZoom);
            Constrain();
        }

        // A wheel notch moves where the zoom is heading and Ease glides there, about 90% of the way in an eighth of a
        // second whatever the frame rate, instead of the view jumping a notch at a time. Pinches and voice stay direct.
        private const float ZoomEasing = 18;
        private float zoomTarget;

        public void ZoomSmoothly(float scale)
        {
            float from = zoomTarget > 0 ? zoomTarget : camera.orthographicSize;
            zoomTarget = Mathf.Clamp(from * Mathf.Pow(Mathf.Max(.01f, scale), ZoomSensitivity), MinimumZoom, MaximumZoom);
        }

        public void Ease(float seconds)
        {
            if (zoomTarget <= 0 || float.IsNaN(seconds) || seconds <= 0) return;
            float size = Mathf.Lerp(camera.orthographicSize, zoomTarget, 1 - Mathf.Exp(-seconds * ZoomEasing));
            if (Mathf.Abs(size - zoomTarget) < .002f) size = zoomTarget;
            camera.orthographicSize = size;
            Constrain();
            // Arrived, or held short by the map's edge: either way there is nowhere further to go.
            if (camera.orthographicSize == zoomTarget || camera.orthographicSize < size - .0001f) zoomTarget = 0;
        }

        public void Rotate(float degrees)
        {
            if (float.IsNaN(degrees) || float.IsInfinity(degrees)) return;
            float yaw = Mathf.Repeat(camera.transform.eulerAngles.y + degrees, 360);
            camera.transform.rotation = Quaternion.Euler(55, yaw, 0);
            Constrain();
        }

        // Keep the ground under the fingers anchored while their centre translates and their distance changes.
        public void PanAndZoom(Vector2 previousCentre, Vector2 currentCentre, float scale)
        {
            bool anchored = GroundPoint(previousCentre, out var before);
            Zoom(scale);
            if (anchored && GroundPoint(currentCentre, out var after)) Focus(focus + before - after);
        }

        public void Focus(Vector3 point) { focus = new Vector3(point.x, 0, point.z); Constrain(); }

        public void Constrain()
        {
            Place();
            var extent = FootprintExtents();
            // Two-argument Min: the three-argument form takes a params array, allocated every frame.
            float fit = Mathf.Min(1, Mathf.Min(mapSize.x / Mathf.Max(.01f, extent.x * 2), mapSize.y / Mathf.Max(.01f, extent.y * 2)));
            if (fit < 1) { camera.orthographicSize *= fit; Place(); extent = FootprintExtents(); }
            // Ships use the outer coastline. They must remain focusable in the middle of the screen, away from the HUD.
            // Other matches retain the original whole-footprint constraint; zoom still fits the map in both modes.
            focus.x = Mathf.Clamp(focus.x, allowMapEdgeFocus ? 0 : extent.x, allowMapEdgeFocus ? mapSize.x : mapSize.x - extent.x);
            focus.z = Mathf.Clamp(focus.z, allowMapEdgeFocus ? 0 : extent.y, allowMapEdgeFocus ? mapSize.y : mapSize.y - extent.y);
            Place();
        }

        private void Place() => camera.transform.position = focus - camera.transform.forward * 55;

        private Vector2 FootprintExtents()
        {
            Vector2 extent = Vector2.zero;
            for (int x = 0; x < 2; x++) for (int y = 0; y < 2; y++)
            {
                var ray = camera.ViewportPointToRay(new Vector3(x, y));
                if (!ground.Raycast(ray, out float d)) continue;
                var delta = ray.GetPoint(d) - focus;
                extent.x = Mathf.Max(extent.x, Mathf.Abs(delta.x));
                extent.y = Mathf.Max(extent.y, Mathf.Abs(delta.z));
            }
            return extent;
        }
    }
}
