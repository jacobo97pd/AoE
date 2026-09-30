using UnityEngine;

namespace Emberfield.Presentation
{
    public enum GestureKind { None, Tap, Pan, BoxSelection }

    // Exactly one gesture wins. UI-origin and multi-touch contacts are cancelled by the adapter.
    public sealed class GestureTracker
    {
        public float HoldSeconds { get; set; } = .38f;
        public float DragThreshold { get; set; } = 12;
        public Vector2 Start { get; private set; }
        public Vector2 Last { get; private set; }
        public bool Active { get; private set; }
        public GestureKind Kind { get; private set; }
        private float began;
        private bool boxOnDrag;

        public void Begin(Vector2 point, float time, bool forceBox = false)
        {
            Active = true; Start = Last = point; began = time;
            boxOnDrag = forceBox;
            Kind = GestureKind.None;
        }

        public GestureKind Move(Vector2 point, float time)
        {
            if (!Active) return GestureKind.None;
            if (Kind == GestureKind.None && (point - Start).sqrMagnitude >= DragThreshold * DragThreshold)
                Kind = boxOnDrag || time - began >= HoldSeconds ? GestureKind.BoxSelection : GestureKind.Pan;
            Last = point;
            return Kind;
        }

        public GestureKind End(Vector2 point, float time)
        {
            if (!Active) return GestureKind.None;
            Move(point, time);
            var result = Kind == GestureKind.None ? GestureKind.Tap : Kind;
            Active = false; Kind = GestureKind.None;
            return result;
        }

        public void Cancel() { Active = false; Kind = GestureKind.None; }
    }
}
