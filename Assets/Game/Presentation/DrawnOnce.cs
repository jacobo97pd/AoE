using UnityEngine;

namespace Emberfield.Presentation
{
    /// <summary>
    /// Keeps its object for the frame it was made in, which draws it, and takes it away at the first Update of a later
    /// frame, before anything else can see it. WorldView's prewarm hangs on one; it needs no match to be running.
    /// </summary>
    public sealed class DrawnOnce : MonoBehaviour
    {
        private int frame;

        private void Awake() => frame = Time.frameCount;

        private void Update()
        {
            if (Time.frameCount <= frame) return;
            gameObject.SetActive(false);
            Destroy(gameObject);
        }
    }
}
