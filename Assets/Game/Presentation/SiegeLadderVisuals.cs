using System;
using System.Collections.Generic;
using Emberfield.Simulation;
using UnityEngine;

namespace Emberfield.Presentation
{
    /// <summary>
    /// Presentation only: a siege ladder raised against an enemy wall, and the soldiers who go up it.
    ///
    /// The simulation already decides everything (SiegeSystem). A ladder is equipment while it is idle within 1300 mm
    /// of an enemy wall, soldiers within 1800 mm may board, and a ladder climb takes 100 ticks. The screen used to
    /// show none of it: the ladder stood where it had stopped, shorter than the wall, and the boarding soldier rose
    /// in a straight line from the ground to the deck, through the stone. Here the ladder swings up against the wall
    /// until its top stands among the merlons, and the soldier walks to its foot, goes up its rungs playing the
    /// Meshy Ladder_Climb_Loop where the model has one, and steps onto the deck slot the simulation is about to give
    /// him. Nothing is written back, and nothing here reads anything the local player could not see.
    ///
    /// The ladder models are rigid meshes (a ladder on its sled or cart, in each culture's style), so raising one is
    /// a turn about the front edge of its base, which keeps every part above the ground, plus a stretch along its
    /// height: 3.8 authored units under the 0.6 unit scale is 2.3 m, and a wall is 3 m.
    /// </summary>
    public static class SiegeLadderVisuals
    {
        /// <summary>How long a ladder takes to swing up against a wall, or back down onto its base, in simulated seconds.</summary>
        public const float RaiseSeconds = .8f;
        /// <summary>The ladder's reach as equipment, from SiegeSystem.EquipmentReady; the unit's radius comes on top.</summary>
        private const int EquipmentRangeMillimetres = 1300;
        // The top stands this far above the wall walk, among the merlons, as assault ladders were cut.
        private const float TopAboveDeck = .5f;
        // Room left between the front of the base and the wall face, so the two never interpenetrate.
        private const float FaceClearance = .05f;
        // The Meshy climb grips about a quarter of the body's height ahead of the hips (0.45-0.63 m on the 2 m
        // kingdom warrior), so the figure hangs that far out from the rungs.
        private const float Standoff = .26f;
        // Height gained per loop of the climb clip, as a share of the body's height. The clip itself rises about 0.3
        // of it per loop; a little more keeps the loop from racing, at the cost of a slight slide along the rails.
        private const float StepRise = .4f;
        private const float WalkMetresPerSecond = 2;
        // A second soldier sent up the same ladder at the same moment waits this share of the climb before starting
        // up, so the file reads as two figures a body apart rather than one on top of the other.
        private const float Stagger = .25f;

        /// <summary>One ladder's raise, kept on its WorldView visual.</summary>
        public sealed class Ladder
        {
            /// <summary>The enemy wall it is braced against, 0 while it is stowed or on the move.</summary>
            public int WallId;
            /// <summary>0 stowed on its base, 1 against the wall.</summary>
            public float Raise;
            /// <summary>The line its rungs run along, in the world, as last drawn: the climbers follow it.</summary>
            public Vector3 Foot, Top;
            /// <summary>Horizontal, from the wall face out toward the ladder.</summary>
            public Vector3 Outward;
            internal Profile Profile;
            internal bool Measured;
            internal Vector3 RestScale, Position, Scale;
            internal Quaternion Rotation = Quaternion.identity;
            internal double Time = -1;
        }

        /// <summary>One boarding soldier's way up a ladder, kept on its WorldView visual.</summary>
        public sealed class Climb
        {
            /// <summary>The ladder this soldier is going up, or null when it climbs without one (a tower, its own wall).</summary>
            public Ladder Ladder;
            /// <summary>True from the ladder's foot until the soldier stands on the deck: on the rungs and stepping over.</summary>
            public bool Aloft;
            internal bool Placed, Posed, Animated, MarkerSought;
            internal Transform Marker;
            internal Quaternion Facing = Quaternion.identity;
            internal float Tilt, Phase = -1;
        }

        /// <summary>A ladder model measured once, in the space of the WorldView model it hangs under.</summary>
        internal sealed class Profile
        {
            // Foot and Top lie on the line the rails run along, at the bottom and the top of the model. Front is the
            // base's front edge on the ground, the pivot of the raise. Lean is +1 when the model leans toward its +Z.
            public Vector3 Foot, Top, Front;
            public float Lean;
        }

        private static readonly Dictionary<int, Profile> profiles = new Dictionary<int, Profile>();
        private static readonly List<Vector3> points = new List<Vector3>(), vertices = new List<Vector3>();

        /// <summary>
        /// Raises or lowers a ladder model against the nearest enemy wall it serves. Runs after WorldView's own
        /// siege sway has posed the model this frame and blends from that pose, so a ladder that drives off lowers
        /// itself while it moves.
        /// </summary>
        public static void Pose(World world, UnitState unit, Transform root, Transform model, Ladder ladder, float alpha, Func<int, bool> visible)
        {
            if (!ladder.Measured) { ladder.Measured = true; ladder.Profile = Measure(model); ladder.RestScale = model.localScale; }
            if (ladder.Profile == null) return;
            // Simulated time drives the raise, so a paused match holds the ladder where it is.
            double now = (world.TickIndex + Mathf.Clamp01(alpha)) * (double)World.TickSeconds;
            float delta = ladder.Time < 0 ? 0 : (float)Math.Max(0, Math.Min(.25, now - ladder.Time));
            ladder.Time = now;
            ladder.WallId = unit.Order == UnitOrder.Idle ? Brace(world, unit, root, ladder, visible) : 0;
            ladder.Raise = Mathf.MoveTowards(ladder.Raise, ladder.WallId != 0 ? 1 : 0, delta / RaiseSeconds);
            model.localScale = ladder.RestScale;
            if (ladder.Raise > 0)
            {
                float eased = Mathf.SmoothStep(0, 1, ladder.Raise);
                model.localPosition = Vector3.Lerp(model.localPosition, ladder.Position, eased);
                model.localRotation = Quaternion.Slerp(model.localRotation, ladder.Rotation, eased);
                model.localScale = Vector3.Lerp(ladder.RestScale, ladder.Scale, eased);
            }
            ladder.Foot = model.TransformPoint(ladder.Profile.Foot);
            ladder.Top = model.TransformPoint(ladder.Profile.Top);
        }

        /// <summary>
        /// Finds the enemy wall this idle ladder serves by the simulation's own test, and works out the pose that
        /// leans it there: turned to face the wall, its base's front edge kept where it stands (or drawn back off the
        /// face), tipped about that edge and lengthened until its top is at the parapet. Returns the wall, or 0.
        /// </summary>
        private static int Brace(World world, UnitState unit, Transform root, Ladder ladder, Func<int, bool> visible)
        {
            float cell = world.Map.CellSizeMillimetres * .001f;
            float reach = (unit.RadiusMillimetres + EquipmentRangeMillimetres) * .001f + .05f;
            var at = DefinitionLoader.ToWorld(unit.Position) + Vector3.up * (root.position.y);
            BuildingState wall = null; float distance = float.MaxValue, height = 0; Vector3 contact = default;
            foreach (var building in world.Buildings)
            {
                if (building.OwnerId == unit.OwnerId || !building.IsComplete || !visible(building.Id)) continue;
                var definition = Definition(world, building.DefinitionId);
                if (definition == null || !definition.IsWall) continue;
                // Walls are axis-aligned footprints (TargetGeometry), so the nearest point of the face is a clamp.
                var centre = DefinitionLoader.ToWorld(building.Position);
                float halfWidth = building.WidthCells * cell * .5f, halfDepth = building.DepthCells * cell * .5f;
                var point = new Vector3(Mathf.Clamp(at.x, centre.x - halfWidth, centre.x + halfWidth), at.y, Mathf.Clamp(at.z, centre.z - halfDepth, centre.z + halfDepth));
                float gapToWall = Vector3.Distance(point, at);
                if (gapToWall > reach || gapToWall >= distance) continue;
                wall = building; distance = gapToWall; contact = point; height = definition.WallHeightMillimetres * .001f;
            }
            if (wall == null || distance < FaceClearance) return 0;

            var profile = ladder.Profile;
            var outward = (at - contact) / distance;
            float scale = ladder.RestScale.x, lean = profile.Lean;
            // Measured in the vertical plane through the wall, forward toward it, from the base's front edge.
            float front = lean * profile.Front.z * scale;
            float setBack = Mathf.Min(front, distance - FaceClearance);
            float gap = distance - setBack;
            float along = lean * profile.Top.z * scale - front;
            float rise = (profile.Top.y - profile.Front.y) * scale;
            float climb = height + TopAboveDeck;
            // The stretch makes the edge-to-top distance the edge-to-parapet distance; the tip then turns it onto it.
            float stretch = Mathf.Clamp(Mathf.Sqrt(Mathf.Max(.01f, gap * gap + climb * climb - along * along)) / Mathf.Max(.01f, rise), .75f, 2.2f);
            float tip = Mathf.Atan2(gap, climb) - Mathf.Atan2(along, rise * stretch);
            // Turned so the side it leans toward faces the wall, then tipped toward it about the model's own X axis.
            var rotation = Quaternion.LookRotation(-outward * lean) * Quaternion.Euler(lean * tip * Mathf.Rad2Deg, 0, 0);
            var size = new Vector3(scale, scale * stretch, scale);
            var origin = at - outward * setBack - rotation * Vector3.Scale(size, profile.Front);
            var inverse = Quaternion.Inverse(root.rotation);
            ladder.Position = inverse * (origin - root.position);
            ladder.Rotation = inverse * rotation;
            ladder.Scale = size;
            ladder.Outward = outward;
            return wall.Id;
        }

        /// <summary>
        /// Reads a ladder model's shape from its near-level vertices: the line its rails run along (a least-squares
        /// fit between a third and most of its height, above the sled or cart and below the pennant at the top), which
        /// way that line leans, and the front edge of the base. Cached per mesh; every copy hangs the same way.
        /// </summary>
        private static Profile Measure(Transform model)
        {
            var filters = model.GetComponentsInChildren<MeshFilter>(true);
            bool levels = Array.Exists(filters, filter => filter.name == "LOD0");
            int key = 0;
            foreach (var filter in filters)
                if (filter.sharedMesh != null && (!levels || filter.name == "LOD0")) { key = filter.sharedMesh.GetInstanceID(); break; }
            if (key == 0) return null;
            if (profiles.TryGetValue(key, out var known)) return known;

            points.Clear();
            var toModel = model.worldToLocalMatrix;
            foreach (var filter in filters)
            {
                var mesh = filter.sharedMesh;
                if (mesh == null || (levels && filter.name != "LOD0")) continue;
                var matrix = toModel * filter.transform.localToWorldMatrix;
                if (mesh.isReadable) { mesh.GetVertices(vertices); foreach (var vertex in vertices) points.Add(matrix.MultiplyPoint3x4(vertex)); }
                else
                {
                    var bounds = mesh.bounds;
                    for (int corner = 0; corner < 8; corner++)
                        points.Add(matrix.MultiplyPoint3x4(bounds.center + Vector3.Scale(bounds.extents, new Vector3((corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1))));
                }
            }
            float bottom = float.MaxValue, top = float.MinValue;
            foreach (var point in points) { bottom = Mathf.Min(bottom, point.y); top = Mathf.Max(top, point.y); }
            float height = top - bottom;
            if (points.Count == 0 || height < .1f) return profiles[key] = null;

            double count = 0, sumX = 0, sumY = 0, sumZ = 0, sumYY = 0, sumYZ = 0;
            foreach (var point in points)
            {
                float y = point.y - bottom;
                if (y < .3f * height || y > .85f * height) continue;
                count++; sumX += point.x; sumY += y; sumZ += point.z; sumYY += y * y; sumYZ += y * point.z;
            }
            float slope = 0, intercept = 0, x = 0;
            if (count >= 8)
            {
                double variance = sumYY - sumY * sumY / count;
                slope = variance > 1e-6 ? (float)((sumYZ - sumY * sumZ / count) / variance) : 0;
                intercept = (float)((sumZ - slope * sumY) / count); x = (float)(sumX / count);
            }
            else { foreach (var point in points) intercept += point.z; intercept /= points.Count; }
            // A near-upright ladder (the elves') leans the model's own way.
            float lean = slope < -.03f ? -1 : 1;
            float front = lean > 0 ? float.MinValue : float.MaxValue;
            foreach (var point in points)
                if (point.y - bottom <= .5f * height) front = lean > 0 ? Mathf.Max(front, point.z) : Mathf.Min(front, point.z);
            return profiles[key] = new Profile
            {
                Foot = new Vector3(x, bottom, intercept), Top = new Vector3(x, top, intercept + slope * height),
                Front = new Vector3(0, bottom, front), Lean = lean,
            };
        }

        /// <summary>
        /// Where a boarding soldier is at this point of its climb: it walks to the ladder's foot, goes up the rungs,
        /// then steps over onto <paramref name="deck"/>. Also sets the facing, the lean onto the rungs and the clip
        /// phase for <see cref="Pose(Transform, Transform, CorsairAnimationDriver, Climb)"/>. False when the ladder
        /// is not against this wall, and the caller keeps its straight lift.
        /// </summary>
        public static bool Place(UnitState unit, Climb climb, Vector3 start, int duration, int rank, Vector3 deck, float alpha, out Vector3 position)
        {
            position = start;
            var ladder = climb.Ladder;
            if (ladder == null || ladder.WallId != unit.BoardingWallId) return false;
            var axis = ladder.Top - ladder.Foot;
            if (axis.y < .5f) return false;
            axis.Normalize();
            var outward = ladder.Outward;
            var side = Vector3.Cross(Vector3.up, outward);
            var normal = Vector3.Cross(axis, side).normalized;
            if (Vector3.Dot(normal, outward) < 0) normal = -normal;
            float tall = AlphaWorldArt.UnitHeight(unit.DefinitionId);
            // Everyone on one ladder hangs off the same rungs; a trailing soldier also stands a little to one side.
            var line = ladder.Foot + normal * (tall * Standoff) + side * (rank == 0 ? 0 : rank % 2 == 1 ? .16f : -.16f);
            var bottom = line + axis * ((start.y - line.y) / axis.y);
            var summit = line + axis * ((deck.y - line.y) / axis.y);

            float progress = Mathf.Clamp01(1 - (unit.BoardingRemainingTicks - Mathf.Clamp01(alpha)) / Mathf.Max(1, duration));
            float seconds = Mathf.Max(1, duration) * World.TickSeconds;
            var approach = bottom - start; approach.y = 0;
            float mount = Mathf.Min(Mathf.Clamp(approach.magnitude / (WalkMetresPerSecond * seconds), .05f, .3f) + rank * Stagger, .6f);
            float dismount = Mathf.Min(.86f + rank * Stagger * .4f, .96f);
            var wallward = Quaternion.LookRotation(-outward);
            float leanDegrees = Vector3.Angle(Vector3.up, axis);
            climb.Aloft = progress >= mount;
            if (progress < mount)
            {
                float t = progress / mount;
                position = Vector3.Lerp(start, bottom, Mathf.SmoothStep(0, 1, t));
                var walking = approach.sqrMagnitude > .0025f ? Quaternion.LookRotation(approach) : wallward;
                climb.Facing = Quaternion.Slerp(walking, wallward, Mathf.SmoothStep(.6f, 1, t));
                climb.Tilt = 0; climb.Phase = -1;
            }
            else if (progress < dismount)
            {
                float t = (progress - mount) / (dismount - mount);
                position = Vector3.Lerp(bottom, summit, t);
                climb.Facing = wallward;
                // The clip was made for an upright ladder, so the figure is laid along the rails: hands and feet then
                // meet the rungs of a ladder at any lean.
                climb.Tilt = leanDegrees * Mathf.SmoothStep(0, 1, t / .12f);
                climb.Phase = Mathf.Max(1, Mathf.Round((deck.y - start.y) / (tall * StepRise))) * t;
            }
            else
            {
                float t = Mathf.SmoothStep(0, 1, (progress - dismount) / (1 - dismount));
                position = Vector3.Lerp(summit, deck, t);
                climb.Facing = wallward;
                climb.Tilt = leanDegrees * (1 - t); climb.Phase = -1;
            }
            climb.Placed = true;
            return true;
        }

        /// <summary>
        /// After the character's own animation driver has run this frame: faces the soldier to the wall, lays it
        /// along the rails and, where its controller has a Climb state, shows the climb at the phase the ascent has
        /// reached. The driver keeps its own state (Walk, while boarding) and is handed back to it afterwards.
        /// </summary>
        public static void Pose(Transform root, Transform model, CorsairAnimationDriver character, Climb climb)
        {
            if (!climb.Placed) { Release(model, character, climb); return; }
            climb.Placed = false;
            root.rotation = climb.Facing;
            model.localRotation = Quaternion.Euler(climb.Tilt, 0, 0) * model.localRotation;
            climb.Posed = true;
            if (climb.Phase >= 0 && character != null && !character.IsDying && State(character.Animator, "Climb", out int hash))
            {
                // The driver may have left this frame's step for the engine; take it first so the climb phase wins.
                character.ApplyPendingAdvance();
                var animator = character.Animator;
                animator.Play(hash, 0, Mathf.Repeat(climb.Phase, 1));
                animator.Update(0);
                climb.Animated = true;
            }
            else HandBack(character, climb);
        }

        /// <summary>Undoes a climb's pose once the soldier is off the ladder, or climbing without one.</summary>
        public static void Release(Transform model, CorsairAnimationDriver character, Climb climb)
        {
            climb.Ladder = null; climb.Placed = climb.Aloft = false;
            // Imported and art models are re-posed every frame anyway; a plain greybox figure is not.
            if (climb.Posed) { climb.Posed = false; if (character == null) model.localRotation = Quaternion.identity; }
            HandBack(character, climb);
        }

        private static void HandBack(CorsairAnimationDriver character, Climb climb)
        {
            if (!climb.Animated) return;
            climb.Animated = false;
            if (character == null || character.IsDying) return;
            // The driver still believes it is playing its own state and would never switch back to it by itself.
            character.ApplyPendingAdvance();
            var animator = character.Animator;
            if (State(animator, character.CurrentState, out int hash)) { animator.CrossFadeInFixedTime(hash, .15f, 0, 0); animator.Update(0); }
        }

        private static bool State(Animator animator, string name, out int hash)
        {
            hash = Animator.StringToHash(name);
            if (animator == null || animator.runtimeAnimatorController == null) return false;
            if (animator.HasState(0, hash)) return true;
            hash = Animator.StringToHash("Base Layer." + name);
            return animator.HasState(0, hash);
        }

        /// <summary>
        /// Where the simulation will put this soldier on the deck: the first free slot, handed out in the order the
        /// climbs finish (SiegeSystem.Tick walks the units in list order and fills the slots from the west end, or the
        /// south end of a wall turned north-south; BuildingFootprints.DeckSlot places them for both).
        /// </summary>
        public static Vector3 DeckSlot(World world, UnitState climber, BuildingState wall)
        {
            var definition = Definition(world, wall.DefinitionId);
            float height = definition != null ? definition.WallHeightMillimetres * .001f : 3;
            int capacity = definition != null ? definition.WallCapacity : 4;
            int ahead = 0; bool passed = false;
            foreach (var other in world.Units)
            {
                if (other == climber) { passed = true; continue; }
                if (other.BoardingWallId != wall.Id) continue;
                if (other.BoardingRemainingTicks < climber.BoardingRemainingTicks || (other.BoardingRemainingTicks == climber.BoardingRemainingTicks && !passed)) ahead++;
            }
            for (int slot = 0; slot < capacity; slot++)
            {
                var point = BuildingFootprints.DeckSlot(wall, capacity, slot, world.Map.CellSizeMillimetres);
                bool taken = false;
                foreach (var other in world.Units) if (other.WallId == wall.Id && other.Position == point) { taken = true; break; }
                if (!taken && ahead-- == 0) return DefinitionLoader.ToWorld(point) + Vector3.up * height;
            }
            return DefinitionLoader.ToWorld(wall.Position) + Vector3.up * height;
        }

        private static BuildingDefinition Definition(World world, string id)
        {
            foreach (var definition in world.Definition.Buildings) if (definition.Id == id) return definition;
            return null;
        }
    }

    // The WorldView side: which visuals are ladders, which ladder a soldier is on, and the hooks Sync calls.
    public sealed partial class WorldView
    {
        private HashSet<string> ladderDefinitions;
        private Func<int, bool> visibleToLocal;

        private bool IsLadder(string definitionId)
        {
            if (ladderDefinitions == null)
            {
                ladderDefinitions = new HashSet<string>();
                foreach (var definition in world.Definition.Units)
                    if (definition.SiegeEquipment == SiegeEquipmentKind.Ladder) ladderDefinitions.Add(definition.Id);
            }
            return ladderDefinitions.Contains(definitionId);
        }

        /// <summary>Hook: a ladder's raise against the wall it serves, after the stock siege sway has posed its model.</summary>
        private void SyncLadder(Visual v, UnitState u, float alpha)
        {
            if (!v.IsArt || v.ImportedCharacter != null || !IsLadder(u.DefinitionId)) return;
            v.Ladder ??= new SiegeLadderVisuals.Ladder();
            visibleToLocal ??= new Func<int, bool>(Visible);
            SiegeLadderVisuals.Pose(world, u, v.Root, v.Model, v.Ladder, alpha, visibleToLocal);
        }

        /// <summary>
        /// Hook: puts a boarding soldier on its side's ladder at this wall. False keeps the straight lift: a tower, a
        /// soldier's own wall, or no ladder of its side raised there in sight. The simulation does not say which ladder
        /// was used (BoardingSiegeUnitId is internal), so the nearest raised one stands in for it.
        /// </summary>
        private bool ClimbLadder(Visual v, UnitState u, BuildingState wall, float alpha)
        {
            v.Climb ??= new SiegeLadderVisuals.Climb();
            v.Climb.Ladder = wall.OwnerId != u.OwnerId ? LadderAt(u, wall.Id, v.ClimbStart) : null;
            if (v.Climb.Ladder == null) return false;
            var deck = SiegeLadderVisuals.DeckSlot(world, u, wall);
            if (!SiegeLadderVisuals.Place(u, v.Climb, v.ClimbStart, v.BoardingDuration, Rank(u, v.Climb.Ladder), deck, alpha, out var position))
            { v.Climb.Ladder = null; return false; }
            v.Root.position = position;
            return true;
        }

        /// <summary>Hook: after the character's own animation, faces the climber to the wall and plays the climb.</summary>
        private void PoseClimber(Visual v)
        {
            if (v.Climb == null) return;
            // The impact flash is a disc on the ground at the unit's feet; boiling oil keeps it lit under a climber,
            // where it read as a platform in mid-air, and the same flat disc reads as a lit puddle under a soldier
            // still waiting or walking at the foot of the ladder. Placed covers the whole boarding walk and climb,
            // so it hides through both. The health bar and the impact sound still report every hit.
            if (v.Climb.Placed && v.Hit != null) v.Hit.SetActive(false);
            // The owner ring is drawn flat at the feet for the ground and the deck. On the rungs it hung in the air
            // around the shins and cut through the ladder, and on the step over it floated outside the parapet. It
            // comes back once the soldier stands on the deck; the health bar says whose soldier it is meanwhile.
            if (!v.Climb.MarkerSought) { v.Climb.MarkerSought = true; v.Climb.Marker = v.Root.Find("Owner marker"); }
            if (v.Climb.Marker != null) v.Climb.Marker.gameObject.SetActive(!(v.Climb.Placed && v.Climb.Aloft));
            SiegeLadderVisuals.Pose(v.Root, v.Model, v.ImportedCharacter, v.Climb);
        }

        private SiegeLadderVisuals.Ladder LadderAt(UnitState climber, int wallId, Vector3 near)
        {
            SiegeLadderVisuals.Ladder best = null; float closest = float.MaxValue;
            foreach (var unit in world.Units)
            {
                if (unit.OwnerId != climber.OwnerId || !visuals.TryGetValue(unit.Id, out var visual) || visual.Ladder == null) continue;
                if (visual.Ladder.WallId != wallId || !visual.Root.gameObject.activeSelf) continue;
                float distance = (visual.Root.position - near).sqrMagnitude;
                if (distance < closest) { best = visual.Ladder; closest = distance; }
            }
            return best;
        }

        // Soldiers sent up one ladder together go up it one behind the other, lowest id first.
        private int Rank(UnitState climber, SiegeLadderVisuals.Ladder ladder)
        {
            int rank = 0;
            foreach (var other in world.Units)
            {
                if (other.Id >= climber.Id || other.OwnerId != climber.OwnerId || other.BoardingWallId != climber.BoardingWallId) continue;
                if (Math.Abs(other.BoardingRemainingTicks - climber.BoardingRemainingTicks) > 10) continue;
                if (visuals.TryGetValue(other.Id, out var visual) && visual.Climb != null && visual.Climb.Ladder == ladder) rank++;
            }
            return rank;
        }
    }
}
