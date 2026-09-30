using System.Collections.Generic;
using Emberfield.Simulation;
using UnityEngine;

namespace Emberfield.Presentation
{
    public sealed partial class WorldView
    {
        private readonly Dictionary<string, Renderer> objectiveMarkers = new Dictionary<string, Renderer>();
        private readonly Dictionary<string, TextMesh> objectiveLabels = new Dictionary<string, TextMesh>();
        private readonly Dictionary<string, int> observedObjectiveOwners = new Dictionary<string, int>();
        private readonly Dictionary<string, LabelState> objectiveLabelStates = new Dictionary<string, LabelState>();
        private readonly struct LabelState
        {
            public readonly int Owner, Capturer, Percentage;
            public readonly bool Contested;
            public LabelState(DominionObjectiveState point)
            { Owner = point.OwnerId; Capturer = point.CapturingPlayerId; Percentage = point.CaptureProgressTicks * 100 / point.CaptureRequiredTicks; Contested = point.IsContested; }
            public bool Same(LabelState other) => Owner == other.Owner && Capturer == other.Capturer && Percentage == other.Percentage && Contested == other.Contested;
        }
        private void CreateObjectives()
        {
            if (world.Match == null || world.Match.Mode != VictoryMode.Dominion) return;
            foreach (var objective in world.Match.Objectives)
            {
                var site = new GameObject("Beacon " + objective.Id).transform; site.SetParent(root, false); site.position = DefinitionLoader.ToWorld(objective.Position);
                if (alphaEnabled)
                {
                    AlphaWorldArt.Beacon(site);
                    objectiveMarkers.Add(objective.Id, Shape("Beacon ownership banner", PrimitiveType.Cube, site, new Vector3(0, 1.24f, -.24f), new Vector3(.38f, 1.14f, .04f), neutral).GetComponent<Renderer>());
                    Shape("Beacon gold sigil", PrimitiveType.Cube, site, new Vector3(0, 1.34f, -.27f), new Vector3(.11f, .27f, .025f), arrow);
                }
                else
                {
                    Shape("Objective plinth", PrimitiveType.Cylinder, site, new Vector3(0, .12f, 0), new Vector3(1.7f, .12f, 1.7f), neutral);
                    objectiveMarkers.Add(objective.Id, Shape("Beacon flag", PrimitiveType.Cube, site, new Vector3(0, 2, 0), new Vector3(.6f, 1.4f, .18f), neutral).GetComponent<Renderer>());
                    Shape("Beacon mast", PrimitiveType.Cylinder, site, new Vector3(0, 1.3f, 0), new Vector3(.12f, 1.3f, .12f), dark);
                }
                var circle = new GameObject("Public capture radius", typeof(LineRenderer)); circle.transform.SetParent(site, false);
                var line = circle.GetComponent<LineRenderer>(); line.useWorldSpace = false; line.positionCount = 48; line.loop = true; line.startWidth = line.endWidth = .07f; line.sharedMaterial = arrow;
                for (int index = 0; index < 48; index++) { float a = index * Mathf.PI * 2 / 48; line.SetPosition(index, new Vector3(Mathf.Cos(a) * objective.RadiusMillimetres * .001f, .08f, Mathf.Sin(a) * objective.RadiusMillimetres * .001f)); }
                var labelObject = new GameObject("Public objective status", typeof(TextMesh)); labelObject.transform.SetParent(site, false); labelObject.transform.localPosition = new Vector3(0, 3, 0);
                var label = labelObject.GetComponent<TextMesh>(); label.fontSize = 40; label.characterSize = .075f; label.anchor = TextAnchor.LowerCenter; label.alignment = TextAlignment.Center;
                label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); label.color = Color.white;
                // World labels precede camera-space UI as well as the normal overlay HUD.
                var textMaterial = new Material(label.font.material) { name = "Public beacon label", renderQueue = 2999 };
                materials.Add(textMaterial); labelObject.GetComponent<Renderer>().sharedMaterial = textMaterial;
                objectiveLabels.Add(objective.Id, label);
                observedObjectiveOwners.Add(objective.Id, objective.OwnerId);
            }
        }
        private void SyncObjectives()
        {
            if (world.Match == null) return;
            var objectives = world.Match.Objectives;
            for (int index = 0; index < objectives.Count; index++)
            {
                var point = objectives[index];
                if (!objectiveMarkers.TryGetValue(point.Id, out var marker)) continue;
                if (observedObjectiveOwners[point.Id] != point.OwnerId) Feedback?.Emit(FeedbackCue.Objective, DefinitionLoader.ToWorld(point.Position) + Vector3.up);
                observedObjectiveOwners[point.Id] = point.OwnerId;
                marker.sharedMaterial = point.OwnerId == 1 ? blue : point.OwnerId == 2 ? red : neutral;
                var label = objectiveLabels[point.Id]; label.transform.rotation = cameraRotation;
                var state = new LabelState(point);
                if (objectiveLabelStates.TryGetValue(point.Id, out var previous) && previous.Same(state)) continue;
                objectiveLabelStates[point.Id] = state;
                UiLocalization.SetText(label, (point.OwnerId == 1 ? "YOURS" : point.OwnerId == 2 ? "RIVAL" : "NEUTRAL")
                    + (point.IsContested ? "\nCONTESTED" : point.CapturingPlayerId != 0 ? "\nCapture " + state.Percentage + "%" : "\nBeacon"));
            }
        }
    }
}
