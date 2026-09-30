using System;
using System.Collections.Generic;
using System.Text;
using Emberfield.Simulation;

namespace Emberfield.Presentation
{
    // Local browsing state and command adaptation; World owns eligibility, payment and effects.
    public sealed class ResearchControls
    {
        private readonly MatchController match;
        public bool IsOpen { get; private set; }
        public string Notice { get; private set; } = "Research occupies its building. Training there must finish first.";
        private int seenCompletions;
        private readonly HashSet<string> seenTechnologyIds = new HashSet<string>(StringComparer.Ordinal);

        public ResearchControls(MatchController match) { this.match = match; }
        public bool Available => match.Stress == null && (match.World.Definition.Technologies?.Length ?? 0) > 0;
        public void Open()
        {
            if (!Available) return;
            match.Factions?.Close(); match.Factions?.CancelDeploy();
            if (match.Economy.PendingBuildingId != null || match.Economy.IsChoosingRally) match.Economy.CancelBuild();
            IsOpen = true; match.Hud?.SetSelectionBox(null); match.Hud?.Invalidate();
        }
        public void Close() { IsOpen = false; match.Hud?.Invalidate(); }

        public TechnologyDefinition Technology(string id)
        {
            foreach (var item in match.World.Definition.Technologies ?? Array.Empty<TechnologyDefinition>())
                if (item.Id == id) return item;
            return null;
        }
        public string TechnologyName(string id) => Technology(id)?.DisplayName ?? id;
        public string EraName(string id)
        {
            foreach (var era in match.World.Definition.Eras ?? Array.Empty<EraDefinition>())
                if (era.Id == id) return era.DisplayName;
            return id ?? "Settlement";
        }
        public string EraLabel
        {
            get
            {
                if (!match.World.TryGetPlayer(MatchController.LocalPlayer, out var player)) return "";
                return "Era " + player.EraTier + " · " + EraName(player.EraId);
            }
        }

        public BuildingState ProducerFor(TechnologyDefinition technology)
        {
            if (technology == null) return null;
            // An explicitly selected eligible site is respected, including its busy state.
            var selected = match.Economy.SelectedBuilding();
            if (selected != null && selected.DefinitionId == technology.ResearchBuildingId) return selected;
            BuildingState fallback = null;
            foreach (var building in match.World.Buildings)
            {
                if (building.OwnerId != MatchController.LocalPlayer || building.DefinitionId != technology.ResearchBuildingId) continue;
                if (fallback == null || (!fallback.IsComplete && building.IsComplete)) fallback = building;
                if (building.IsComplete && building.ActiveResearch == null && building.ProductionQueue.Count == 0) return building;
            }
            return fallback;
        }

        public BuildingState ActiveSite(string technologyId)
        {
            foreach (var building in match.World.Buildings)
                if (building.OwnerId == MatchController.LocalPlayer && building.ActiveResearch?.TechnologyId == technologyId) return building;
            return null;
        }

        public CommandResult Validate(string technologyId)
        {
            var site = ProducerFor(Technology(technologyId));
            return match.World.ValidateResearch(new ResearchCommand(MatchController.LocalPlayer, site?.Id ?? 0, technologyId));
        }

        public CommandResult Start(string technologyId)
        {
            var definition = Technology(technologyId);
            var site = ProducerFor(definition);
            var result = match.SubmitPlayerCommand(new ResearchCommand(MatchController.LocalPlayer, site?.Id ?? 0, technologyId));
            Notice = result.Accepted ? match.World.IsNetworkReplica ? "Research order sent. Waiting for the server." : definition.DisplayName + " started at " +
                match.Economy.BuildingDefinition(site.DefinitionId).DisplayName + " #" + site.Id + ". Training there is occupied." : result.Message;
            match.SetFeedback(Notice);
            return result;
        }

        public void ObserveCompletions()
        {
            if (!Available || !match.World.TryGetPlayer(MatchController.LocalPlayer, out var player)) return;
            if (player.CompletedTechnologyIds.Count <= seenCompletions) return;
            var newlyCompleted = new StringBuilder();
            foreach (string id in player.CompletedTechnologyIds)
                if (seenTechnologyIds.Add(id))
                {
                    if (newlyCompleted.Length > 0) newlyCompleted.Append(", ");
                    newlyCompleted.Append(TechnologyName(id));
                }
            seenCompletions = player.CompletedTechnologyIds.Count;
            Notice = newlyCompleted + " complete. " + EraLabel + ".";
            match.SetFeedback(Notice);
        }

        public string Requirements(TechnologyDefinition item)
        {
            var text = new StringBuilder((match.Economy.BuildingDefinition(item.ResearchBuildingId)?.DisplayName ?? item.ResearchBuildingId) + " · Requires " + EraName(item.RequiredEraId));
            foreach (string id in item.RequiredTechnologyIds ?? Array.Empty<string>()) text.Append(" · ").Append(TechnologyName(id));
            foreach (string id in item.RequiredBuildingIds ?? Array.Empty<string>()) text.Append(" · completed ").Append(match.Economy.BuildingDefinition(id)?.DisplayName ?? id);
            if (!string.IsNullOrEmpty(item.RequiredFactionId))
                foreach (var faction in match.World.Definition.Factions)
                    if (faction.Id == item.RequiredFactionId) { text.Append(" · ").Append(faction.DisplayName); break; }
            return text.ToString();
        }

        public string Status(TechnologyDefinition item)
        {
            match.World.TryGetPlayer(MatchController.LocalPlayer, out var player);
            if (player != null && player.HasTechnology(item.Id)) return "Completed";
            var active = ActiveSite(item.Id);
            if (active != null) return "Researching · " + (int)Math.Ceiling(active.ActiveResearch.RemainingTicks / (double)World.TickRate) + " s left";
            var site = ProducerFor(item);
            if (site == null) return "Build a " + (match.Economy.BuildingDefinition(item.ResearchBuildingId)?.DisplayName ?? item.ResearchBuildingId);
            var result = Validate(item.Id);
            return result.Accepted ? "Ready at " + match.Economy.BuildingDefinition(site.DefinitionId).DisplayName + " #" + site.Id : result.Message;
        }
    }
}
