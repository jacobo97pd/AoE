using System;
using System.Collections.Generic;

namespace Emberfield.Simulation
{
    /// <summary>Paid research transactions and completion-time, player-local stat caches.</summary>
    internal sealed class ResearchSystem
    {
        private readonly World world;
        // Keyed by the rules entry itself, which every unit holds, rather than by its name.
        private readonly Dictionary<int, Dictionary<UnitDefinition, EffectiveStats>> playerStats = new Dictionary<int, Dictionary<UnitDefinition, EffectiveStats>>();
        private readonly struct EffectiveStats
        {
            internal readonly int Damage, Armor, Gather;
            internal EffectiveStats(int damage, int armor, int gather) { Damage = damage; Armor = armor; Gather = gather; }
        }
        internal ResearchSystem(World world) { this.world = world; }

        internal CommandResult Validate(ResearchCommand command)
        {
            if (command == null) return CommandResult.Reject(CommandRejection.InvalidCommand, "No research command was supplied.");
            if (!world.TryGetPlayer(command.PlayerId, out var player))
                return CommandResult.Reject(CommandRejection.InvalidPlayer, "Player does not exist.");
            if (!world.TryGetBuilding(command.BuildingId, out var building))
                return CommandResult.Reject(CommandRejection.UnknownBuilding, "Research building does not exist.");
            if (building.OwnerId != command.PlayerId) return CommandResult.Reject(CommandRejection.NotOwner, "You do not own this research building.");
            if (!building.IsComplete) return CommandResult.Reject(CommandRejection.BuildingIncomplete, "Construction must finish before research.");
            if (!building.IsOperational || building.CharterRemainingTicks != 0)
                return CommandResult.Reject(CommandRejection.FactionActionBusy, "Research must wait until relocation or charter work finishes.");
            if (command.TechnologyId == null || !world.technologyCatalog.Technologies.TryGetValue(command.TechnologyId, out var technology))
                return CommandResult.Reject(CommandRejection.UnknownDefinition, "Technology does not exist.");
            if (technology.ResearchBuildingId != building.DefinitionId)
                return CommandResult.Reject(CommandRejection.CannotResearch, "This building cannot research that technology.");
            var faction = world.ValidateFactionRequirement(command.PlayerId, technology.RequiredFactionId);
            if (!faction.Accepted) return faction;
            if (player.HasTechnology(technology.Id))
                return CommandResult.Reject(CommandRejection.ResearchAlreadyCompleted, "Technology is already completed.");
            bool advances = !string.IsNullOrEmpty(technology.AdvancesToEraId);
            if (player.PendingTechnologyIds.Contains(technology.Id) || (advances && player.PendingEraAdvance))
                return CommandResult.Reject(CommandRejection.ResearchInProgress, "This technology or another era advancement is already in progress.");
            if (building.ActiveResearch != null || building.Queue.Count != 0)
                return CommandResult.Reject(CommandRejection.ResearchBusy, "Research requires a building with no research or training in progress.");
            var requirements = ValidateRequirements(command.PlayerId, technology.RequiredEraId, technology.RequiredTechnologyIds);
            if (!requirements.Accepted) return requirements;
            if (advances && world.technologyCatalog.Eras[technology.AdvancesToEraId].Tier != player.EraTier + 1)
                return CommandResult.Reject(CommandRejection.PrerequisiteMissing, "Only the immediately following era can be researched.");
            foreach (string required in technology.RequiredBuildingIds ?? Array.Empty<string>())
            {
                bool found = false;
                foreach (var candidate in world.buildings)
                    if (candidate.OwnerId == command.PlayerId && candidate.IsOperational && candidate.DefinitionId == required)
                    { found = true; break; }
                if (!found) return CommandResult.Reject(CommandRejection.PrerequisiteMissing,
                    "A completed " + Label(world.buildingDefinitions[required].DisplayName, required) + " belonging to you is required.");
            }
            if (!player.CanAfford(technology.Cost))
                return CommandResult.Reject(CommandRejection.InsufficientResources, "Not enough resources to research.");
            return CommandResult.Success();
        }

        internal CommandResult ValidateRequirements(int playerId, string requiredEraId, string[] requiredTechnologyIds)
        {
            if (!world.TryGetPlayer(playerId, out var player))
                return CommandResult.Reject(CommandRejection.InvalidPlayer, "Player does not exist.");
            if (!string.IsNullOrEmpty(requiredEraId))
            {
                bool known = world.technologyCatalog.Eras.TryGetValue(requiredEraId, out var era);
                if (!known || player.EraTier < era.Tier)
                    return CommandResult.Reject(CommandRejection.PrerequisiteMissing,
                        "Requires era: " + Label(known ? era.DisplayName : null, requiredEraId) + ".");
            }
            foreach (string technology in requiredTechnologyIds ?? Array.Empty<string>())
                if (!player.HasTechnology(technology))
                {
                    TechnologyDefinition definition = null;
                    if (technology != null) world.technologyCatalog.Technologies.TryGetValue(technology, out definition);
                    return CommandResult.Reject(CommandRejection.PrerequisiteMissing,
                        "Requires technology: " + Label(definition?.DisplayName, technology) + ".");
                }
            return CommandResult.Success();
        }

        private static string Label(string displayName, string id) => string.IsNullOrWhiteSpace(displayName) ? id ?? "unknown" : displayName;

        internal CommandResult Submit(ResearchCommand command)
        {
            var result = Validate(command); if (!result.Accepted) return result;
            var technology = world.technologyCatalog.Technologies[command.TechnologyId];
            var player = world.Player(command.PlayerId);
            player.TrySpend(technology.Cost);
            player.PendingTechnologyIds.Add(technology.Id);
            if (!string.IsNullOrEmpty(technology.AdvancesToEraId)) player.PendingEraAdvance = true;
            world.TryGetBuilding(command.BuildingId, out var building);
            building.ActiveResearch = new ResearchState(technology);
            return CommandResult.Success();
        }

        internal void Tick()
        {
            foreach (var building in world.buildings)
            {
                var active = building.ActiveResearch;
                if (active == null || !building.IsOperational) continue;
                active.RemainingTicks--;
                if (active.RemainingTicks != 0) continue;
                var technology = world.technologyCatalog.Technologies[active.TechnologyId];
                var player = world.Player(building.OwnerId);
                ClearPending(building);
                player.CompleteTechnology(technology.Id);
                if (!string.IsNullOrEmpty(technology.AdvancesToEraId))
                {
                    var era = world.technologyCatalog.Eras[technology.AdvancesToEraId];
                    player.EraId = era.Id; player.EraTier = era.Tier;
                }
                RebuildStats(player);
            }
        }

        internal void ClearPending(BuildingState building)
        {
            if (building.ActiveResearch == null) return;
            var player = world.Player(building.OwnerId);
            string id = building.ActiveResearch.TechnologyId;
            player.PendingTechnologyIds.Remove(id);
            if (!string.IsNullOrEmpty(world.technologyCatalog.Technologies[id].AdvancesToEraId)) player.PendingEraAdvance = false;
            building.ActiveResearch = null;
        }

        private void RebuildStats(PlayerState player)
        {
            var stats = new Dictionary<UnitDefinition, EffectiveStats>();
            foreach (var definition in world.unitDefinitions.Values)
            {
                int damage = definition.Attack.Damage, armor = definition.Armor, gather = definition.GatherAmount;
                foreach (string id in player.CompletedTechnologyIds)
                foreach (var effect in world.technologyCatalog.Technologies[id].Effects ?? Array.Empty<TechnologyEffect>())
                {
                    if ((definition.Tags & effect.TargetTags) == 0 || (!string.IsNullOrEmpty(effect.TargetUnitId) && effect.TargetUnitId != definition.Id)) continue;
                    switch (effect.Kind)
                    {
                        case TechnologyEffectKind.AttackDamage: damage += effect.Amount; break;
                        case TechnologyEffectKind.Armor: armor += effect.Amount; break;
                        case TechnologyEffectKind.GatherAmount: gather += effect.Amount; break;
                    }
                }
                stats.Add(definition, new EffectiveStats(damage, armor, gather));
            }
            playerStats[player.Id] = stats;
            foreach (var unit in world.units) if (unit.OwnerId == player.Id) ApplyStats(unit);
        }

        internal void ApplyStats(UnitState unit)
        {
            var definition = unit.Definition;
            if (playerStats.TryGetValue(unit.OwnerId, out var stats))
            {
                var effective = stats[definition];
                unit.AttackDamage = effective.Damage; unit.Armor = effective.Armor; unit.GatherAmount = effective.Gather;
            }
            else { unit.AttackDamage = definition.Attack.Damage; unit.Armor = definition.Armor; unit.GatherAmount = definition.GatherAmount; }
            var faction = world.factions?.DefinitionFor(unit.OwnerId);
            if (faction != null && (unit.Tags & faction.ArmorBonusTags) != 0) unit.Armor += faction.ArmorBonus;
            if (unit.WallId != 0) unit.Armor += 2;
        }
    }
}
