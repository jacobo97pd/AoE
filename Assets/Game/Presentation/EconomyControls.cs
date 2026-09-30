using System;
using System.Collections.Generic;
using Emberfield.Simulation;
using UnityEngine;

namespace Emberfield.Presentation
{
    // Presentation mode and placement preview. All validation and costs belong to World.
    public sealed class EconomyControls : IDisposable
    {
        private readonly MatchController match;
        private readonly GameObject footprint;
        private readonly Material previewMaterial;
        public string PendingBuildingId { get; private set; }
        public SimPoint PreviewPosition { get; private set; }
        public CommandResult PlacementResult { get; private set; }
        public bool HasPreview { get; private set; }
        public bool IsChoosingRally { get; private set; }
        /// <summary>The wall or gate waiting to be placed stands turned a quarter, north-south (BuildingFootprints.CanTurn).</summary>
        public bool Turned { get; private set; }
        /// <summary>The wall being drawn as one run while a wall is the building waiting to be placed.</summary>
        public WallRunTool WallRun { get; }
        private bool turnedByHand;

        public EconomyControls(MatchController match, Transform parent)
        {
            this.match = match;
            footprint = GameObject.CreatePrimitive(PrimitiveType.Cube);
            footprint.name = "Placement footprint"; footprint.transform.SetParent(parent, false);
            UnityEngine.Object.Destroy(footprint.GetComponent<Collider>());
            previewMaterial = new Material(Resources.Load<Material>("Materials/Greybox"));
            footprint.GetComponent<Renderer>().sharedMaterial = previewMaterial;
            footprint.SetActive(false);
            WallRun = new WallRunTool(match, parent);
        }

        public int[] SelectedWorkers()
        {
            var ids = new List<int>();
            foreach (int id in match.Selection)
                if (match.World.TryGetUnit(id, out var unit) && unit.OwnerId == MatchController.LocalPlayer && unit.IsWorker) ids.Add(id);
            return ids.ToArray();
        }

        public BuildingState SelectedBuilding()
        {
            if (match.Selection.Count == 1 && match.World.TryGetBuilding(match.Selection[0], out var building) && building.OwnerId == MatchController.LocalPlayer) return building;
            return null;
        }

        public bool HasSelectedCargo()
        {
            foreach (int id in match.Selection)
                if (match.World.TryGetUnit(id, out var unit) && unit.OwnerId == MatchController.LocalPlayer && unit.IsWorker && unit.CarriedAmount > 0) return true;
            return false;
        }

        public CommandResult ReturnCargo(int dropOffBuildingId = 0)
        {
            var result = match.SubmitPlayerCommand(new ReturnCargoCommand(MatchController.LocalPlayer, SelectedWorkers(), dropOffBuildingId));
            match.SetFeedback(result.Accepted ? match.World.IsNetworkReplica ? "Delivery order sent. Waiting for the server." : "Delivering carried resources, then waiting for orders." : result.Message);
            return result;
        }

        public BuildingDefinition BuildingDefinition(string id)
        {
            foreach (var definition in match.World.Definition.Buildings) if (definition.Id == id) return definition;
            return null;
        }

        public UnitDefinition UnitDefinition(string id)
        {
            foreach (var definition in match.World.Definition.Units) if (definition.Id == id) return definition;
            return null;
        }

        public void BeginBuild(string id)
        {
            if (SelectedWorkers().Length == 0) { match.SetFeedback("Select a Tender to construct a building."); return; }
            if (BuildingDefinition(id) == null) { match.SetFeedback("Unknown building."); return; }
            var definition = BuildingDefinition(id);
            var faction = match.World.ValidateFactionRequirement(MatchController.LocalPlayer, definition.RequiredFactionId);
            if (!faction.Accepted) { match.SetFeedback(faction.Message); return; }
            var requirements = match.World.ValidateRequirements(MatchController.LocalPlayer, definition.RequiredEraId, definition.RequiredTechnologyIds);
            if (!requirements.Accepted) { match.SetFeedback(requirements.Message); return; }
            match.TouchSelectionMode = false;
            match.Factions.CancelDeploy();
            IsChoosingRally = false; PendingBuildingId = id; HasPreview = false; footprint.SetActive(false);
            Turned = turnedByHand = false; WallRun.Cancel();
            // A wall is drawn as a whole run rather than placed a stretch at a time.
            if (definition.IsWall) { WallRun.Begin(definition); match.SetFeedback(WallRunTool.Guide); return; }
            match.SetFeedback("Tap a location to preview " + BuildingDefinition(id).DisplayName + ", then Confirm.");
        }

        public void PreviewAt(SimPoint point)
        {
            if (PendingBuildingId == null) return;
            // A point given for a wall starts its run or fixes the run's next corner.
            if (WallRun.Active) { WallRun.Click(point); return; }
            var definition = BuildingDefinition(PendingBuildingId);
            if (!turnedByHand && BuildingFootprints.CanTurn(definition)) Turned = TurnFor(definition, point);
            int cell = match.World.Map.CellSizeMillimetres;
            int width = BuildingFootprints.WidthCells(definition, Turned), depth = BuildingFootprints.DepthCells(definition, Turned);
            PreviewPosition = Snap(point, width, depth, cell);
            HasPreview = true;
            footprint.transform.position = DefinitionLoader.ToWorld(PreviewPosition) + Vector3.up * .08f;
            footprint.transform.localScale = new Vector3(width * cell * .001f, .12f, depth * cell * .001f);
            footprint.SetActive(true);
            RefreshPreview();
            match.SetFeedback(PlacementResult.Accepted ? "Valid location. Confirm to spend resources and start construction." : PlacementResult.Message);
        }

        public void RefreshPreview()
        {
            if (!HasPreview || PendingBuildingId == null) return;
            PlacementResult = match.World.ValidatePlacement(MatchController.LocalPlayer, SelectedWorkers(), PendingBuildingId, PreviewPosition, Turned);
            previewMaterial.color = PlacementResult.Accepted ? new Color(.3f, .75f, .43f) : new Color(.8f, .26f, .22f);
        }

        public CommandResult ConfirmBuild()
        {
            if (WallRun.Active) return FinishWallRun();
            var result = match.SubmitPlayerCommand(new BuildCommand(MatchController.LocalPlayer, SelectedWorkers(), HasPreview ? PendingBuildingId : null, PreviewPosition, Turned));
            match.SetFeedback(result.Accepted ? match.World.IsNetworkReplica ? "Build order sent. Waiting for the server." : "Foundation placed. Workers are constructing." : result.Message);
            if (result.Accepted) CancelBuild(false);
            return result;
        }

        // The run as drawn: its green stretches are ordered, and the wall is put away once the order stands.
        private CommandResult FinishWallRun()
        {
            var result = WallRun.Order();
            if (result.Accepted) CancelBuild(false);
            return result;
        }

        /// <summary>Turns the gate waiting to be placed a quarter by hand; it then keeps that way wherever it is dropped.</summary>
        public void RotatePending()
        {
            if (PendingBuildingId == null || WallRun.Active || !BuildingFootprints.CanTurn(BuildingDefinition(PendingBuildingId))) return;
            Turned = !Turned; turnedByHand = true;
            if (HasPreview) PreviewAt(PreviewPosition);
            else match.SetFeedback(Turned ? "Gate turned north-south." : "Gate turned east-west.");
        }

        // A gate dropped against the end of a wall turns to carry it on: north-south between turned walls, east-west
        // between plain ones. Where the walls say nothing either way it keeps the way it stood.
        private bool TurnFor(BuildingDefinition definition, SimPoint point)
        {
            int plain = WallEnds(definition, point, false), turned = WallEnds(definition, point, true);
            return turned != plain ? turned > plain : Turned;
        }

        // How many of the two ends of this footprint, snapped here standing this way, meet a wall or gate of the local
        // player's standing the same way.
        private int WallEnds(BuildingDefinition definition, SimPoint point, bool turned)
        {
            int cell = match.World.Map.CellSizeMillimetres;
            int width = BuildingFootprints.WidthCells(definition, turned), depth = BuildingFootprints.DepthCells(definition, turned);
            var centre = Snap(point, width, depth, cell);
            int left = (centre.X - width * cell / 2) / cell, bottom = (centre.Z - depth * cell / 2) / cell;
            return turned ? WallAt(left + width / 2, bottom - 1, true) + WallAt(left + width / 2, bottom + depth, true)
                : WallAt(left - 1, bottom + depth / 2, false) + WallAt(left + width, bottom + depth / 2, false);
        }

        private int WallAt(int x, int z, bool turned)
        {
            int cell = match.World.Map.CellSizeMillimetres;
            foreach (var building in match.World.Buildings)
            {
                if (building.OwnerId != MatchController.LocalPlayer || building.IsTurned != turned) continue;
                var definition = BuildingDefinition(building.DefinitionId);
                if (definition == null || !definition.IsWall && !definition.IsGate) continue;
                int left = (building.Position.X - building.WidthCells * cell / 2) / cell, bottom = (building.Position.Z - building.DepthCells * cell / 2) / cell;
                if (x >= left && x < left + building.WidthCells && z >= bottom && z < bottom + building.DepthCells) return 1;
            }
            return 0;
        }

        // The centre of the footprint of this size whose cells lie nearest the point.
        private static SimPoint Snap(SimPoint point, int width, int depth, int cell)
        {
            int x = Mathf.RoundToInt((point.X - width * cell / 2f) / cell);
            int z = Mathf.RoundToInt((point.Z - depth * cell / 2f) / cell);
            return new SimPoint(x * cell + width * cell / 2, z * cell + depth * cell / 2);
        }

        public void CancelBuild() => CancelBuild(true);
        private void CancelBuild(bool feedback)
        {
            PendingBuildingId = null; HasPreview = false; IsChoosingRally = false; footprint.SetActive(false);
            Turned = turnedByHand = false; WallRun.Cancel();
            if (feedback) match.SetFeedback("Placement cancelled.");
        }

        public CommandResult Train(string unitId)
        {
            var building = SelectedBuilding();
            var result = match.SubmitPlayerCommand(new TrainCommand(MatchController.LocalPlayer, building?.Id ?? 0, unitId));
            match.SetFeedback(result.Accepted ? match.World.IsNetworkReplica ? "Training order sent. Waiting for the server." : UnitDefinition(unitId).DisplayName + " queued." : result.Message);
            return result;
        }

        public void SelectRally()
        {
            if (SelectedBuilding() == null) return;
            match.TouchSelectionMode = false;
            CancelBuild(false); IsChoosingRally = true;
            match.SetFeedback("Tap open ground for this building's rally point.");
        }

        public bool HandleTerrain(Vector3 point)
        {
            if (PendingBuildingId != null) { PreviewAt(DefinitionLoader.ToSimulation(point)); return true; }
            if (!IsChoosingRally) return false;
            var building = SelectedBuilding();
            match.Issue(new SetRallyCommand(MatchController.LocalPlayer, building?.Id ?? 0, DefinitionLoader.ToSimulation(point)), "Rally point set.");
            IsChoosingRally = false;
            return true;
        }

        public static string CostText(ResourceAmount cost)
        {
            string text = "";
            if (cost.Food > 0) text += cost.Food + " food ";
            if (cost.Wood > 0) text += cost.Wood + " wood ";
            if (cost.Metal > 0) text += cost.Metal + " metal ";
            if (cost.Stone > 0) text += cost.Stone + " stone ";
            return text.Length > 0 ? text.TrimEnd() : "Free";
        }

        public void Dispose() { if (footprint != null) UnityEngine.Object.Destroy(footprint); UnityEngine.Object.Destroy(previewMaterial); WallRun.Dispose(); }
    }
}
