using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Emberfield.Presentation
{
    // Attached once to the HUD canvas. High contrast is opt-in and leaves geometry/team colours alone.
    public sealed class AlphaTextContrast : MonoBehaviour
    {
        private MatchController match;
        private readonly List<Text> labels = new List<Text>();
        private readonly Dictionary<Text, Color> originals = new Dictionary<Text, Color>();
        private readonly Dictionary<Text, Shadow> shadows = new Dictionary<Text, Shadow>();
        private bool applied;
        private float nextScan;
        public void Initialize(MatchController owner) => match = owner;
        private void LateUpdate()
        {
            bool requested = match != null && match.Alpha != null && match.Alpha.Settings.Value.HighContrastText;
            if (!requested && !applied) return;
            if (!requested) { Restore(); return; }
            if (Time.unscaledTime < nextScan) return;
            nextScan = Time.unscaledTime + .1f;
            labels.Clear(); GetComponentsInChildren(true, labels);
            foreach (var label in labels)
            {
                if (!originals.ContainsKey(label))
                {
                    originals.Add(label, label.color);
                    // Only own this shadow, so pre-existing UI effects retain their configuration.
                    var shadow = label.gameObject.AddComponent<Shadow>(); shadow.effectColor = Color.black; shadow.effectDistance = new Vector2(1, -1); shadows.Add(label, shadow);
                }
                label.color = Color.white;
                if (shadows.TryGetValue(label, out var effect) && effect != null) effect.enabled = true;
            }
            applied = true;
        }
        private void Restore()
        {
            foreach (var pair in originals) if (pair.Key != null) pair.Key.color = pair.Value;
            foreach (var pair in shadows) if (pair.Value != null) Destroy(pair.Value);
            originals.Clear(); shadows.Clear(); applied = false; nextScan = 0;
        }
        private void OnDisable() { if (applied) Restore(); }
    }
}
