using UnityEngine;

namespace Emberfield.Presentation
{
    /// <summary>
    /// Turns a windmill's sails. Cosmetic only, and driven by unscaled time so the mill keeps working while
    /// the match is paused behind a menu — a still frame of a paused game should still look alive.
    /// </summary>
    public sealed class TurningSails : MonoBehaviour
    {
        private float degreesPerSecond;

        private void Awake()
        {
            // Each mill on the map keeps its own pace, from its own place in the world.
            float seed = Mathf.Abs(transform.position.x * 7.31f + transform.position.z * 3.17f);
            degreesPerSecond = 17 + Mathf.Repeat(seed, 9);
        }

        private void Update() => transform.localRotation *= Quaternion.Euler(0, 0, degreesPerSecond * Time.unscaledDeltaTime);
    }
}
