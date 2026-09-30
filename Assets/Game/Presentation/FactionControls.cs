using System;
using Emberfield.Simulation;
using UnityEngine;

namespace Emberfield.Presentation
{
    // Local faction browsing and deployment preview; rules and costs remain in World.
    public sealed class FactionControls : IDisposable
    {
        private readonly MatchController match;
        private readonly GameObject preview;
        private readonly Material material;
        public bool IsOpen { get; private set; }
        public int PendingDeployUnitId { get; private set; }
        public bool HasPreview { get; private set; }
        public SimPoint PreviewPosition { get; private set; }
        public CommandResult PlacementResult { get; private set; }
        public bool Available => match.Stress == null && (match.World.Definition.Factions?.Length ?? 0) > 0;
        public FactionDefinition LocalDefinition => DefinitionFor(MatchController.LocalPlayer);

        public FactionControls(MatchController match, Transform parent)
        {
            this.match = match;
            preview = GameObject.CreatePrimitive(PrimitiveType.Cube);
            preview.name = "Outpost deployment preview"; preview.transform.SetParent(parent, false);
            UnityEngine.Object.Destroy(preview.GetComponent<Collider>());
            material = new Material(Resources.Load<Material>("Materials/Greybox"));
            preview.GetComponent<Renderer>().sharedMaterial = material; preview.SetActive(false);
        }

        public FactionDefinition DefinitionFor(int playerId)
        {
            if (!match.World.TryGetPlayer(playerId, out var player)) return null;
            foreach (var definition in match.World.Definition.Factions ?? Array.Empty<FactionDefinition>())
                if (definition.Id == player.FactionId) return definition;
            return null;
        }
        public bool Allows(string requiredFaction) => match.World.ValidateFactionRequirement(MatchController.LocalPlayer, requiredFaction).Accepted;
        public UnitState SelectedUnit() => match.Selection.Count == 1 && match.World.TryGetUnit(match.Selection[0], out var unit) && unit.OwnerId == MatchController.LocalPlayer ? unit : null;

        public void Open()
        {
            if (!Available) return;
            match.Research.Close(); match.Economy.CancelBuild(); CancelDeploy();
            IsOpen = true; match.Hud?.SetSelectionBox(null); match.Hud?.Invalidate();
        }
        public void Close() { IsOpen = false; match.Hud?.Invalidate(); }
        public void Choose(string id) => match.StartFactionDrill(id);

        public CommandResult Issue(IGameCommand command, string success)
        {
            var result = match.SubmitPlayerCommand(command);
            match.SetFeedback(result.Accepted ? match.World.IsNetworkReplica ? "Faction order sent. Waiting for the server." : success : result.Message);
            return result;
        }
        public void SetCharter(StoreyardCharter charter)
        {
            var site = match.Economy.SelectedBuilding();
            Issue(new SetCharterCommand(MatchController.LocalPlayer, site?.Id ?? 0, charter), charter + " charter change started. Its benefit pauses during preparation.");
        }
        public void Pack()
        {
            var site = match.Economy.SelectedBuilding();
            Issue(new PackOutpostCommand(MatchController.LocalPlayer, site?.Id ?? 0), "Packing supply outpost. Drop-off is offline; the transport can be attacked.");
        }
        public void Reposition()
        {
            Issue(new RepositionCommand(MatchController.LocalPlayer, match.SelectedUnits()), "Reposition active. Move to safety; Ashrunners cannot attack during the burst.");
        }
        public void DeployRelay()
        {
            var unit = SelectedUnit();
            if (unit == null) { match.SetFeedback("Select one Threadkeeper near an owned chartered Storeyard."); return; }
            BuildingState chosen = null;
            long nearest = long.MaxValue;
            foreach (var site in match.World.Buildings)
            {
                var command = new DeployThreadkeeperCommand(MatchController.LocalPlayer, unit.Id, site.Id);
                if (!match.World.ValidateFactionAction(command).Accepted) continue;
                long dx = (long)site.Position.X - unit.Position.X, dz = (long)site.Position.Z - unit.Position.Z;
                long distance = dx * dx + dz * dz;
                if (distance < nearest) { nearest = distance; chosen = site; }
            }
            if (chosen == null) { match.SetFeedback("Relay unavailable. Move near an active owned chartered Storeyard and wait for cooldown."); return; }
            Issue(new DeployThreadkeeperCommand(MatchController.LocalPlayer, unit.Id, chosen.Id), "Threadkeeper relay deployed. It stays exposed and stationary until the supply window ends.");
        }

        public void BeginDeploy()
        {
            var unit = SelectedUnit();
            if (unit == null || !unit.IsPackedOutpost) { match.SetFeedback("Select a packed supply outpost."); return; }
            match.TouchSelectionMode = false;
            match.Economy.CancelBuild(); PendingDeployUnitId = unit.Id; HasPreview = false;
            match.SetFeedback("Tap a clear footprint beside the stationary transport, then Confirm deploy. Move the transport there first.");
        }
        public void PreviewAt(SimPoint point)
        {
            if (!match.World.TryGetUnit(PendingDeployUnitId, out var unit) || !unit.IsPackedOutpost) { CancelDeploy(); return; }
            var building = match.Economy.BuildingDefinition(unit.PackedBuildingDefinitionId);
            int cell = match.World.Map.CellSizeMillimetres;
            int x = Mathf.RoundToInt((point.X - building.WidthCells * cell / 2f) / cell);
            int z = Mathf.RoundToInt((point.Z - building.DepthCells * cell / 2f) / cell);
            PreviewPosition = new SimPoint(x * cell + building.WidthCells * cell / 2, z * cell + building.DepthCells * cell / 2);
            HasPreview = true;
            preview.transform.position = DefinitionLoader.ToWorld(PreviewPosition) + Vector3.up * .08f;
            preview.transform.localScale = new Vector3(building.WidthCells * cell * .001f, .12f, building.DepthCells * cell * .001f);
            preview.SetActive(true); RefreshPreview();
            match.SetFeedback(PlacementResult.Accepted ? "Clear adjacent footprint. Confirm deploy to begin preparation." : PlacementResult.Message);
        }
        public void RefreshPreview()
        {
            if (!HasPreview || PendingDeployUnitId == 0) return;
            PlacementResult = match.World.ValidateFactionAction(new DeployOutpostCommand(MatchController.LocalPlayer, PendingDeployUnitId, PreviewPosition));
            material.color = PlacementResult.Accepted ? new Color(.3f, .75f, .43f) : new Color(.8f, .26f, .22f);
        }
        public CommandResult ConfirmDeploy()
        {
            var result = Issue(new DeployOutpostCommand(MatchController.LocalPlayer, HasPreview ? PendingDeployUnitId : 0, PreviewPosition), "Deploying. The transport remains vulnerable; obstructed completion waits for clearance.");
            if (result.Accepted) CancelDeploy();
            return result;
        }
        public void CancelDeploy() { PendingDeployUnitId = 0; HasPreview = false; preview.SetActive(false); match.Hud?.Invalidate(); }
        public void Dispose() { UnityEngine.Object.Destroy(preview); UnityEngine.Object.Destroy(material); }
    }
}
