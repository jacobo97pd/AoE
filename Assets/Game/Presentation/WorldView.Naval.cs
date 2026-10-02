using Emberfield.Simulation;
using UnityEngine;

namespace Emberfield.Presentation
{
    public sealed partial class WorldView
    {
        // Bow to stern on screen, whichever culture's hull it is: long enough to read as a ship beside a 1.3 m soldier,
        // short enough that a harbour of them stays legible. The rules' hull is a one-metre disc (World: half a cell).
        private const float ShipLength = 3.3f;
        // The hull classes read apart at a glance: the light sloop shorter, the galleon longer. The rules' discs are equal.
        private static float HullLength(string id) => id == "pirate_sloop" ? 3.0f : id == "english_frigate" ? 3.5f : id == "spanish_galleon" ? 3.8f : ShipLength;
        // The water sheet sits a little under the ground plane (AlphaEnvironment.BuildWaterSheet) and a hull sits in it.
        private const float WaterLine = -.02f, Draft = .09f;
        private const float ShipTurnDegreesPerSecond = 150;

        private void NewShipVisual(UnitState u)
        {
            var v = NewVisual(u.Id, u.DefinitionId, AlphaWorldArt.UnitSelectionRadius(u.DefinitionId), true);
            v.IsUnit = true; v.IsShip = true; v.OwnerId = u.OwnerId;
            var faction = FactionFor(u.OwnerId);
            v.FactionKind = AlphaWorldArt.Culture(faction?.Kind ?? FactionKind.AvenCompact);
            float height = 2.4f;
            var hull = sliceEnabled ? MeshyPropVisuals.TryShip(u.DefinitionId, u.OwnerId, v.FactionKind, v.Model) : null;
            if (hull != null && MeshBounds(hull, out var bounds))
            {
                // Fitted by its own bounds to its class's length; it floats with its keel under water.
                float scale = HullLength(u.DefinitionId) / Mathf.Max(.1f, Mathf.Max(bounds.size.z, bounds.size.x));
                hull.localScale = Vector3.one * scale;
                hull.localPosition = new Vector3(-bounds.center.x * scale, -(bounds.min.y + bounds.size.y * Draft) * scale, -bounds.center.z * scale);
                height = bounds.size.y * scale;
                v.IsArt = true;
            }
            else BuildHull(v, u);
            AddOwnerMarker(v);
            AddHealthBar(v, height * .82f + .25f, 1.6f);
            v.LastHealth = u.Health; v.LastAttackCooldown = u.AttackCooldownTicks;
        }

        private static bool MeshBounds(Transform model, out Bounds bounds)
        {
            bounds = default; bool found = false;
            foreach (var filter in model.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null || filter.name == "LOD1") continue;
                var mesh = filter.sharedMesh.bounds;
                var toRoot = model.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                // Child meshes can have their own transforms; mesh.bounds alone is in the child's coordinates.
                for (int corner = 0; corner < 8; corner++)
                {
                    var point = toRoot.MultiplyPoint3x4(mesh.center + Vector3.Scale(mesh.extents,
                        new Vector3((corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1)));
                    if (!found) { bounds = new Bounds(point, Vector3.zero); found = true; } else bounds.Encapsulate(point);
                }
            }
            return found;
        }

        // Without the art catalogue (-emberfieldProceduralUnits): a hull, a mast and a sail in the owner's colour.
        private void BuildHull(Visual v, UnitState u)
        {
            var team = u.OwnerId == 1 ? blue : red;
            Shape("Hull", PrimitiveType.Cube, v.Model, new Vector3(0, .18f, 0), new Vector3(.95f, .45f, ShipLength * .82f), neutral);
            Shape("Bow", PrimitiveType.Cube, v.Model, new Vector3(0, .22f, ShipLength * .43f), new Vector3(.55f, .4f, .5f), neutral).transform.localRotation = Quaternion.Euler(0, 45, 0);
            Shape("Mast", PrimitiveType.Cylinder, v.Model, new Vector3(0, 1.2f, .1f), new Vector3(.09f, 1.05f, .09f), dark);
            Shape("Sail", PrimitiveType.Cube, v.Model, new Vector3(0, 1.35f, .1f), new Vector3(1.1f, 1.1f, .05f), team);
        }

        // Afloat: a slow bob, roll and pitch of its own, and a kick to the side when the guns fire.
        private void PoseShip(Visual v, UnitState u, float alpha, float pulse)
        {
            float time = (world.TickIndex + Mathf.Clamp01(alpha)) * Simulation.World.TickSeconds, phase = u.Id * 1.37f;
            float bob = Mathf.Sin(time * 1.6f + phase) * .035f;
            float roll = Mathf.Sin(time * 1.1f + phase) * 2.2f + pulse * 6;
            float pitch = Mathf.Sin(time * 1.35f + phase * .7f) * 1.2f;
            v.Model.localPosition = new Vector3(0, WaterLine + bob, 0);
            v.Model.localRotation = Quaternion.Euler(pitch, 0, roll);
        }

        // A ship brings its side to bear on what it shoots at, turning the short way round.
        private static Quaternion Broadside(Quaternion heading, Vector3 toTarget)
        {
            var side = Vector3.Cross(Vector3.up, toTarget).normalized;
            if (side.sqrMagnitude < .0001f) return heading;
            var port = Quaternion.LookRotation(side); var starboard = Quaternion.LookRotation(-side);
            return Quaternion.Angle(heading, port) <= Quaternion.Angle(heading, starboard) ? port : starboard;
        }

        // A sunk ship settles and heels over where it went down, then is gone; its visual is already out of the view.
        private void Sink(Visual visual)
        {
            var wreck = visual.Model;
            wreck.SetParent(root, true);
            wreck.gameObject.AddComponent<SinkingHull>();
        }

        /// <summary>The harbour quay stood on a dock's footprint, its seaward side turned to the water it touches.</summary>
        private Transform DockModel(BuildingState building, Transform parent, float width, float depth)
        {
            var model = MeshyPropVisuals.TryDock(parent);
            if (model == null || !MeshBounds(model, out var bounds)) return model;
            float scale = Mathf.Min(width, depth) * 1.1f / Mathf.Max(.1f, Mathf.Max(bounds.size.x, bounds.size.z));
            model.localScale = Vector3.one * scale;
            model.localRotation = Quaternion.Euler(0, SeawardAngle(building), 0);
            model.localPosition = model.localRotation * new Vector3(-bounds.center.x * scale, -bounds.min.y * scale, -bounds.center.z * scale);
            return model;
        }

        private float SeawardAngle(BuildingState building)
        {
            int cell = world.Map.CellSizeMillimetres;
            int left = (building.Position.X - building.WidthCells * cell / 2) / cell, bottom = (building.Position.Z - building.DepthCells * cell / 2) / cell;
            int north = 0, south = 0, east = 0, west = 0;
            bool Sea(int x, int z) => world.IsSailable(new SimPoint(x * cell + cell / 2, z * cell + cell / 2));
            for (int x = left; x < left + building.WidthCells; x++) { if (Sea(x, bottom + building.DepthCells)) north++; if (Sea(x, bottom - 1)) south++; }
            for (int z = bottom; z < bottom + building.DepthCells; z++) { if (Sea(left + building.WidthCells, z)) east++; if (Sea(left - 1, z)) west++; }
            int best = Mathf.Max(Mathf.Max(north, south), Mathf.Max(east, west));
            return best == north ? 0 : best == east ? 90 : best == south ? 180 : 270;
        }
    }

    /// <summary>A sunk hull going down: it heels over and slips under the water in a few seconds, then is destroyed.</summary>
    internal sealed class SinkingHull : MonoBehaviour
    {
        private const float Seconds = 3.2f;
        private float elapsed;
        private Vector3 start;
        private Quaternion upright;

        private void Start() { start = transform.position; upright = transform.rotation; }

        private void Update()
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / Seconds);
            transform.position = start + Vector3.down * (t * t * 2.4f);
            transform.rotation = upright * Quaternion.Euler(t * 8, 0, t * 32);
            if (t >= 1) Destroy(gameObject);
        }
    }
}
