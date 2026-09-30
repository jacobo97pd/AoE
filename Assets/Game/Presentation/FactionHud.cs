using System;
using System.Collections.Generic;
using System.Text;
using Emberfield.Simulation;
using UnityEngine.UI;

namespace Emberfield.Presentation
{
    public sealed partial class MatchHud
    {
        private sealed class FactionAction
        {
            public Button Button;
            public Func<IGameCommand> Command;
        }
        private readonly List<FactionAction> factionButtons = new List<FactionAction>();

        private void AppendFactionContextKey(StringBuilder key)
        {
            var unit = match.Factions.SelectedUnit();
            var building = match.Economy.SelectedBuilding();
            key.Append(":faction:"); AppendNumber(key, match.Factions.PendingDeployUnitId).Append(':');
            if (unit != null) { AppendNumber(key, unit.Id).Append(':').Append(unit.DefinitionId).Append(':'); AppendNumber(key, (int)unit.RelocationStage); }
            else key.Append("::");
            key.Append(':');
            if (building != null) AppendNumber(key, (int)building.RelocationStage);
            key.Append(":group:").Append(AllSelectedCanReposition());
        }
        private bool AllSelectedCanReposition()
        {
            var ids = match.SelectedUnits();
            if (ids.Length == 0) return false;
            foreach (int id in ids)
                if (!match.World.TryGetUnit(id, out var unit) || !match.Economy.UnitDefinition(unit.DefinitionId).CanReposition) return false;
            return true;
        }
        private void AppendFactionUnitDetails(UnitState unit)
        {
            var definition = match.Economy.UnitDefinition(unit.DefinitionId);
            if (unit.IsPackedOutpost)
            {
                detailText = definition.DisplayName + $" · {unit.Health}/{unit.MaxHealth} health · {unit.RelocationStage}";
                detailText += unit.RelocationStage == RelocationStage.Deploying ? $"\nDeploying {unit.RelocationRemainingTicks / (float)World.TickRate:0.0}s · blocked completion waits · drop-off offline" : "\nMove to clear terrain, then Deploy beside the transport. Drop-off is offline.";
            }
            else if (definition.IsThreadkeeper)
            {
                detailText = definition.DisplayName + $" / Support · {unit.Health}/{unit.MaxHealth} health";
                detailText += unit.ThreadkeeperRemainingTicks > 0 ? $"\nStationary relay · {unit.ThreadkeeperRemainingTicks / (float)World.TickRate:0.0}s left · linked Storeyard #{unit.LinkedStoreyardId}" : unit.ThreadkeeperCooldownTicks > 0 ? $"\nRelay cooldown {unit.ThreadkeeperCooldownTicks / (float)World.TickRate:0.0}s" : "\nDeploy near an active owned Storeyard to extend its charter. Unarmed and exposed.";
            }
            else if (definition.CanReposition)
                detailText += unit.RepositionRemainingTicks > 0 ? $" · Reposition {unit.RepositionRemainingTicks / (float)World.TickRate:0.0}s · no attacks" : $" · Reposition cooldown {unit.RepositionCooldownTicks / (float)World.TickRate:0.0}s";
        }
        private void AppendFactionBuildingDetails(BuildingState building)
        {
            var definition = match.Economy.BuildingDefinition(building.DefinitionId);
            if (building.RelocationStage == RelocationStage.Packing)
                detailText += $"\nPacking {building.RelocationRemainingTicks / (float)World.TickRate:0.0}s · drop-off offline";
            else if (definition.CanCharter && match.Factions.LocalDefinition?.Kind == FactionKind.AvenCompact)
                detailText += building.CharterRemainingTicks > 0 ? $"\nChanging to {building.PendingCharter} · {building.CharterRemainingTicks / (float)World.TickRate:0.0}s · charter benefit offline" : "\nCharter: " + building.ActiveCharter + " · benefits do not stack";
            else if (!string.IsNullOrEmpty(definition.TransportUnitId))
                detailText += "\nSupply Outpost · packing disables drop-off · damage persists through relocation";
        }
        private void ActionButton(string name, string text, Func<IGameCommand> command, Action action)
        {
            var label = Button(contextRow, text, () => action());
            var button = label.GetComponentInParent<Button>(); button.name = name;
            factionButtons.Add(new FactionAction { Button = button, Command = command });
        }
        private void AddFactionBuildingActions(BuildingState building)
        {
            var definition = match.Economy.BuildingDefinition(building.DefinitionId);
            var faction = match.Factions.LocalDefinition;
            if (faction == null) return;
            if (definition.CanCharter && faction.Kind == FactionKind.AvenCompact)
            {
                string cost = EconomyControls.CostText(faction.CharterCost);
                ActionButton("Set Logistics charter", "Logistics charter\n" + cost, () => new SetCharterCommand(MatchController.LocalPlayer, building.Id, StoreyardCharter.Logistics), () => match.Factions.SetCharter(StoreyardCharter.Logistics));
                ActionButton("Set Muster charter", "Muster charter\n" + cost, () => new SetCharterCommand(MatchController.LocalPlayer, building.Id, StoreyardCharter.Muster), () => match.Factions.SetCharter(StoreyardCharter.Muster));
            }
            if (!string.IsNullOrEmpty(definition.TransportUnitId))
                ActionButton("Pack outpost", "Pack outpost\nDrop-off pauses", () => new PackOutpostCommand(MatchController.LocalPlayer, building.Id), match.Factions.Pack);
        }
        private bool AddFactionUnitActions()
        {
            var unit = match.Factions.SelectedUnit();
            if (unit == null)
            {
                if (!AllSelectedCanReposition()) return false;
                ActionButton("Reposition", "Reposition Ashrunners\nMove faster; no attacks", () => new RepositionCommand(MatchController.LocalPlayer, match.SelectedUnits()), match.Factions.Reposition);
                return true;
            }
            var definition = match.Economy.UnitDefinition(unit.DefinitionId);
            if (unit.IsPackedOutpost)
            {
                if (unit.RelocationStage == RelocationStage.Packed) Button(contextRow, "Deploy outpost\nBeside transport", match.Factions.BeginDeploy);
                return true;
            }
            if (definition.IsThreadkeeper)
            {
                var label = Button(contextRow, "Deploy relay\nNear chartered Storeyard", match.Factions.DeployRelay);
                label.transform.parent.name = "Deploy relay";
                return true;
            }
            if (definition.CanReposition)
            {
                ActionButton("Reposition", "Reposition\nMove faster; no attacks", () => new RepositionCommand(MatchController.LocalPlayer, new[] { unit.Id }), match.Factions.Reposition);
                return true;
            }
            return false;
        }
        private void RefreshFactionButtons()
        {
            foreach (var action in factionButtons)
                action.Button.interactable = match.World.ValidateFactionAction(action.Command()).Accepted;
        }
    }
}
