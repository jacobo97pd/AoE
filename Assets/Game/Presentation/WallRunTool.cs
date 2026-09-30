using System;
using System.Collections.Generic;
using System.Text;
using Emberfield.Simulation;
using UnityEngine;
using UnityEngine.Rendering;

namespace Emberfield.Presentation
{
    /// <summary>
    /// Presentation only: a wall drawn as one run. With the wall chosen, a mouse drag lays a straight wall in one go,
    /// while clicks fix corners one after another and the run follows the pointer between them; a click beside the
    /// start closes the enclosure, and a right click, Enter, a double click or Confirm wall orders what is drawn. On a
    /// touchscreen a finger draws, every lift fixes a corner, and only Confirm wall orders.
    ///
    /// The run snaps to whole stretches along east-west or north-south legs (WallRunLayout). Each stretch is a pooled
    /// ghost: green where it can stand, orange where it could but the stock runs out before it, red where it cannot.
    /// The rules judge every stretch (World.PreviewBuildRun), the same planning the order itself runs, so green is
    /// exactly what gets laid; the label beside the run adds up what the green stretches cost against the stock. The
    /// order is one BuildRunCommand of the green stretches, nearest the builders first; each is then built in its
    /// ordinary time, one after another. Nothing is judged again while nothing changes, so a still pointer costs a frame
    /// nothing and allocates nothing.
    /// </summary>
    public sealed class WallRunTool : IDisposable
    {
        /// <summary>How often a standing run is judged again, so a unit walking into it or a delivery shows at once.</summary>
        public const float RevalidateSeconds = .25f;
        public const string Guide = "Drag to draw the wall; click to fix each corner. Right-click or Confirm wall builds it.";
        public static readonly Color Buildable = new Color(.3f, .75f, .43f), Unaffordable = new Color(.95f, .6f, .18f), Blocked = new Color(.8f, .26f, .22f);
        private const int MaximumCorners = BuildRunCommand.MaximumSites + 1;
        private const float GhostHeight = .36f, GhostGap = .12f;
        private readonly MatchController match;
        private readonly Transform root;
        private readonly Material buildable, unaffordable, blocked;
        private readonly List<MeshRenderer> ghosts = new List<MeshRenderer>();
        private readonly List<GridCell> corners = new List<GridCell>(), through = new List<GridCell>();
        private readonly List<BuildSite> sites = new List<BuildSite>(BuildRunCommand.MaximumSites), ordered = new List<BuildSite>(BuildRunCommand.MaximumSites);
        private readonly StringBuilder text = new StringBuilder(192);
        private BuildRunPreview preview;
        private BuildingDefinition definition;
        private GridCell pointer;
        private bool hasPointer, pressed, pressStarted, laidOut, judged, drawn;
        private float nextJudgement;
        private long judgedTick = -1;
        private int shownGhosts;

        public WallRunTool(MatchController match, Transform parent)
        {
            this.match = match;
            root = new GameObject("Wall run").transform; root.SetParent(parent, false);
            var template = Resources.Load<Material>("Materials/Greybox");
            buildable = Tinted(template, Buildable); unaffordable = Tinted(template, Unaffordable); blocked = Tinted(template, Blocked);
        }

        /// <summary>True while a wall is the building waiting to be placed (EconomyControls.BeginBuild).</summary>
        public bool Active => definition != null;
        public BuildingDefinition Definition => definition;
        /// <summary>The corners fixed so far, the start first. Zero until the run is started.</summary>
        public int CornerCount => corners.Count;
        public bool IsDrawing => corners.Count > 0;
        /// <summary>The run as drawn ends beside its start: the enclosure is closed.</summary>
        public bool Closed { get; private set; }
        /// <summary>The stretches as drawn, through the fixed corners and the pointer.</summary>
        public IReadOnlyList<BuildSite> Sites => sites;
        /// <summary>The rules' verdict on the stretches as last judged, one per site in order; null with nothing drawn.</summary>
        public BuildRunPreview Preview => judged && sites.Count > 0 ? preview : null;
        public int AcceptedCount { get; private set; }
        public int UnaffordableCount { get; private set; }
        public int BlockedCount { get; private set; }
        /// <summary>What the green stretches cost together.</summary>
        public ResourceAmount Cost { get; private set; }
        /// <summary>The label beside the run: how many stretches, their cost, the stock and what is wrong, in source text.</summary>
        public string Label { get; private set; }
        public Color Tone { get; private set; }
        public bool HasLabel => Active && sites.Count > 0 && Label != null;
        /// <summary>Where the label hangs: over the run's last stretch, where the pointer is.</summary>
        public Vector3 LabelAnchor => sites.Count == 0 ? Vector3.zero : DefinitionLoader.ToWorld(sites[sites.Count - 1].Position) + Vector3.up * 1.2f;
        /// <summary>The ghost drawn for each site, for tests and captures; only the first Sites.Count are shown.</summary>
        public IReadOnlyList<MeshRenderer> Ghosts => ghosts;

        public void Begin(BuildingDefinition wall) { Reset(); definition = wall; }

        public void Cancel() { Reset(); definition = null; Hide(); }

        private void Reset()
        {
            corners.Clear(); through.Clear(); sites.Clear();
            hasPointer = pressed = pressStarted = false; laidOut = judged = drawn = false; Closed = false;
            AcceptedCount = UnaffordableCount = BlockedCount = 0; Cost = default; Label = null;
        }

        /// <summary>The pointer is over this ground point, and the run follows it.</summary>
        public void Point(SimPoint ground)
        {
            if (!Active) return;
            var cell = WallRunLayout.Cell(ground, match.World.Map.CellSizeMillimetres);
            if (hasPointer && cell.X == pointer.X && cell.Z == pointer.Z) return;
            pointer = cell; hasPointer = true; laidOut = false;
        }

        /// <summary>The finger is off the glass: only the fixed corners are drawn.</summary>
        public void Lift() { if (hasPointer) { hasPointer = false; laidOut = false; } }

        /// <summary>A press on the ground. The first one starts the run where it lands.</summary>
        public void Press()
        {
            if (!Active || !hasPointer) return;
            pressed = true; pressStarted = corners.Count == 0;
            if (pressStarted) { corners.Add(pointer); laidOut = false; }
        }

        /// <summary>
        /// The press ends where the pointer is. A mouse drag that started the run orders it at once, a straight wall;
        /// a finger's fixes its far end instead. A tap that started the run leaves its start fixed, and any other
        /// release fixes a corner.
        /// </summary>
        public void Release(bool dragged, bool touch)
        {
            if (!pressed) return;
            pressed = false;
            if (pressStarted) { if (dragged) { if (touch) Corner(false); else Finish(); } return; }
            Corner(!touch);
        }

        /// <summary>A press interrupted by a second finger or a menu leaves the run as it was before it.</summary>
        public void Abort()
        {
            if (!pressed) return;
            pressed = false;
            if (pressStarted && corners.Count == 1) { corners.Clear(); laidOut = false; }
        }

        /// <summary>A point given from elsewhere (a tap on the terrain, a voice order): starts the run or fixes a corner.</summary>
        public void Click(SimPoint ground)
        {
            if (!Active) return;
            Point(ground);
            if (corners.Count == 0) { corners.Add(pointer); laidOut = false; }
            else Corner(false);
        }

        public void UndoCorner()
        {
            if (!Active || corners.Count == 0) return;
            corners.RemoveAt(corners.Count - 1); pressed = false; laidOut = false;
        }

        // Fixes a corner where the pointer is. With the mouse, fixing the same corner again (a double click) or closing
        // the enclosure orders the run.
        private void Corner(bool finish)
        {
            if (!hasPointer) return;
            if (corners.Count == 0) { corners.Add(pointer); laidOut = false; return; }
            var last = corners[corners.Count - 1];
            if (pointer.X == last.X && pointer.Z == last.Z) { if (finish) Finish(); return; }
            if (corners.Count >= MaximumCorners) return;
            corners.Add(pointer); laidOut = false;
            if (finish && Layout()) Finish();
        }

        // Ordering the run is what Confirm wall does, so the wall is put away the same way whichever finished it.
        private void Finish() => match.Economy.ConfirmBuild();

        /// <summary>
        /// Orders the green stretches of the run as drawn, nearest the builders first. Called by EconomyControls, which
        /// puts the wall away once the order stands.
        /// </summary>
        internal CommandResult Order()
        {
            Layout(); Judge();
            ordered.Clear();
            var verdicts = Preview;
            if (verdicts != null)
                for (int i = 0; i < sites.Count && i < verdicts.Sites.Count; i++) if (verdicts.Sites[i].Accepted) ordered.Add(sites[i]);
            if (ordered.Count == 0 && verdicts != null && verdicts.Sites.Count > 0)
            {
                var refusal = FirstRefusal(verdicts);
                match.SetFeedback(refusal.Message);
                return refusal;
            }
            if (ordered.Count > 1) WallRunLayout.NearestFirst(ordered, BuildersCentre(), Closed);
            int count = ordered.Count;
            var result = match.SubmitPlayerCommand(new BuildRunCommand(MatchController.LocalPlayer, match.Economy.SelectedWorkers(), definition.Id, ordered.ToArray()));
            match.SetFeedback(!result.Accepted ? result.Message : match.World.IsNetworkReplica ? "Wall order sent. Waiting for the server."
                : count == 1 ? "Wall ordered: 1 stretch. Workers are constructing." : "Wall ordered: " + count + " stretches. Workers build them one after another.");
            return result;
        }

        /// <summary>Called every frame by the HUD: lays the run out, has the rules judge it when something changed, and draws it.</summary>
        public void Sync()
        {
            if (!Active) return;
            if (!laidOut) Layout();
            if (!judged || Time.unscaledTime >= nextJudgement && match.World.TickIndex != judgedTick) Judge();
            if (!drawn) Draw();
        }

        // Through the fixed corners and the pointer. Returns whether the run closes.
        private bool Layout()
        {
            through.Clear();
            for (int i = 0; i < corners.Count; i++) through.Add(corners[i]);
            // The pointer resting on the corner just fixed adds nothing; laid out again it would start a leg of its own.
            var last = corners.Count > 0 ? corners[corners.Count - 1] : default;
            if (hasPointer && (corners.Count == 0 || pointer.X != last.X || pointer.Z != last.Z)) through.Add(pointer);
            Closed = through.Count > 0 && WallRunLayout.Layout(definition, match.World.Map.CellSizeMillimetres, through, sites);
            if (through.Count == 0) sites.Clear();
            laidOut = true; judged = false;
            return Closed;
        }

        private void Judge()
        {
            judged = true; drawn = false;
            nextJudgement = Time.unscaledTime + RevalidateSeconds; judgedTick = match.World.TickIndex;
            AcceptedCount = UnaffordableCount = BlockedCount = 0; Cost = default; Label = null; Tone = Blocked;
            if (sites.Count == 0) return;
            var command = new BuildRunCommand(MatchController.LocalPlayer, match.Economy.SelectedWorkers(), definition.Id, sites.ToArray());
            preview = match.World.PreviewBuildRun(command, preview);
            bool reported = false; CommandResult reason = default;
            for (int i = 0; i < preview.Sites.Count; i++)
            {
                var verdict = preview.Sites[i];
                if (verdict.Accepted) AcceptedCount++;
                else if (verdict.Reason == CommandRejection.InsufficientResources) UnaffordableCount++;
                else { BlockedCount++; if (!reported) { reported = true; reason = verdict; } }
            }
            // The stretches may each stand while the order as a whole cannot (a builder who reaches none of them).
            if (!reported && !preview.Result.Accepted) { reported = true; reason = preview.Result; }
            Cost = preview.Cost;
            Tone = AcceptedCount == 0 || reported ? Blocked : UnaffordableCount > 0 ? Unaffordable : Buildable;
            Label = Compose(reported ? reason.Message : null);
        }

        // "5 stretches · 50 wood 200 stone", then the stock with how many it does not cover, then what stands in the way.
        private string Compose(string reason)
        {
            text.Clear();
            if (AcceptedCount == 0) text.Append("No stretch can be built here");
            else text.Append(AcceptedCount).Append(AcceptedCount == 1 ? " stretch · " : " stretches · ").Append(EconomyControls.CostText(Cost));
            var each = definition.Cost;
            if (each.Food > 0 || each.Wood > 0 || each.Metal > 0 || each.Stone > 0)
            {
                ResourceAmount stock = default;
                if (match.World.TryGetPlayer(MatchController.LocalPlayer, out var player)) stock = player.Resources;
                // Only what a wall is paid in, down to zero: the rest of the stock says nothing about it.
                text.Append("\nStock:");
                if (each.Food > 0) text.Append(' ').Append(stock.Food).Append(" food");
                if (each.Wood > 0) text.Append(' ').Append(stock.Wood).Append(" wood");
                if (each.Metal > 0) text.Append(' ').Append(stock.Metal).Append(" metal");
                if (each.Stone > 0) text.Append(' ').Append(stock.Stone).Append(" stone");
                if (UnaffordableCount > 0) text.Append(" · ").Append(UnaffordableCount).Append(" need more resources");
            }
            int parts = 0;
            if (Closed) { text.Append("\nEnclosure closed"); parts++; }
            if (BlockedCount > 0) { text.Append(parts++ == 0 ? "\n" : " · ").Append(BlockedCount).Append(" blocked"); }
            if (reason != null) text.Append(parts == 0 ? "\n" : " · ").Append(reason);
            return text.ToString();
        }

        private static CommandResult FirstRefusal(BuildRunPreview verdicts)
        {
            for (int i = 0; i < verdicts.Sites.Count; i++)
                if (verdicts.Sites[i].Reason != CommandRejection.InsufficientResources) return verdicts.Sites[i];
            return verdicts.Sites[0];
        }

        // Where the selected builders stand on average: the run is walked from the stretch nearest them.
        private SimPoint BuildersCentre()
        {
            long x = 0, z = 0; int count = 0;
            foreach (int id in match.Selection)
                if (match.World.TryGetUnit(id, out var unit) && unit.OwnerId == MatchController.LocalPlayer && unit.IsWorker)
                { x += unit.Position.X; z += unit.Position.Z; count++; }
            return count == 0 ? ordered[0].Position : new SimPoint((int)(x / count), (int)(z / count));
        }

        private void Draw()
        {
            drawn = true;
            float cell = match.World.Map.CellSizeMillimetres * .001f;
            float along = definition.WidthCells * cell - GhostGap, across = definition.DepthCells * cell;
            for (int i = 0; i < sites.Count; i++)
            {
                if (i == ghosts.Count) ghosts.Add(NewGhost(i));
                var ghost = ghosts[i];
                var site = sites[i];
                ghost.transform.position = DefinitionLoader.ToWorld(site.Position) + Vector3.up * (.02f + GhostHeight / 2);
                // A little short of a whole stretch, so the stretches read as the separate pieces they are built as.
                ghost.transform.localScale = site.Turned ? new Vector3(across, GhostHeight, along) : new Vector3(along, GhostHeight, across);
                var verdict = preview != null && i < preview.Sites.Count ? preview.Sites[i] : default;
                var material = verdict.Accepted ? buildable : verdict.Reason == CommandRejection.InsufficientResources ? unaffordable : blocked;
                if (ghost.sharedMaterial != material) ghost.sharedMaterial = material;
                if (!ghost.gameObject.activeSelf) ghost.gameObject.SetActive(true);
            }
            for (int i = sites.Count; i < shownGhosts; i++) ghosts[i].gameObject.SetActive(false);
            shownGhosts = sites.Count;
        }

        private void Hide()
        {
            for (int i = 0; i < shownGhosts; i++) ghosts[i].gameObject.SetActive(false);
            shownGhosts = 0;
        }

        private MeshRenderer NewGhost(int index)
        {
            var ghost = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ghost.name = "Wall stretch " + index; ghost.transform.SetParent(root, true);
            UnityEngine.Object.Destroy(ghost.GetComponent<Collider>());
            var renderer = ghost.GetComponent<MeshRenderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            renderer.sharedMaterial = blocked;
            ghost.SetActive(false);
            return renderer;
        }

        private static Material Tinted(Material template, Color color) => new Material(template) { color = color, enableInstancing = true };

        public void Dispose()
        {
            if (root != null) UnityEngine.Object.Destroy(root.gameObject);
            UnityEngine.Object.Destroy(buildable); UnityEngine.Object.Destroy(unaffordable); UnityEngine.Object.Destroy(blocked);
        }
    }
}
