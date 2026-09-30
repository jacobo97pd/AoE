using UnityEngine;

namespace Emberfield.Presentation
{
    // Logical screen distances stay consistent at phone/tablet resolutions without relying on reported DPI.
    public static class MobileInputSettings
    {
        public const float ReferenceHeight = 720;
        public const float DragThreshold = 12;
        public const float DoubleTapRadius = 30;
        public const float DoubleTapSeconds = .32f;
        // How long a still finger must rest on the minimap before it sends the selection instead of just looking.
        public const float LongPressSeconds = .45f;
        public static float ScreenPixels(float referencePixels, int screenHeight)
            => referencePixels * Mathf.Max(1, screenHeight) / ReferenceHeight;
    }
}
