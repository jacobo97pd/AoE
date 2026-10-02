using System;
using System.Collections.Generic;

namespace Emberfield.Simulation
{
    public sealed partial class World
    {
        /// <summary>Shared recruitment restrictions, including reservations at every building.</summary>
        public CommandResult ValidateUnitRecruitment(int playerId, string definitionId)
        {
            if (!TryGetPlayer(playerId, out var player)) return CommandResult.Reject(CommandRejection.InvalidPlayer, "Player does not exist.");
            if (definitionId == null || !unitDefinitions.TryGetValue(definitionId, out var definition))
                return CommandResult.Reject(CommandRejection.UnknownDefinition, "Unit definition does not exist.");
            if (!ContentRealms.IsUnitInFactionRoster(definitionId, player.FactionId))
                return CommandResult.Reject(CommandRejection.WrongFaction, definition.Domain != MovementDomain.Water ? "Pirates recruit treasure seekers as their workers."
                    : player.FactionId == "pirates" ? "Pirates sail their own sloops instead of the galley." : "This navy sails its own warship instead of the galley.");
            if (!string.IsNullOrEmpty(definition.RequiredRealmId) && definition.RequiredRealmId != Map.RealmId)
                return CommandResult.Reject(CommandRejection.WrongFaction, "This unit belongs to the " + definition.RequiredRealmId + " realm.");
            var faction = ValidateFactionRequirement(playerId, definition.RequiredFactionId);
            if (!faction.Accepted) return faction;
            if (definition.MaxAlivePerPlayer > 0)
            {
                int count = 0;
                foreach (var unit in units) if (unit.OwnerId == playerId && unit.DefinitionId == definitionId) count++;
                // Aboard a ship is still alive.
                foreach (var unit in passengers.Values) if (unit.OwnerId == playerId && unit.DefinitionId == definitionId) count++;
                foreach (var building in buildings)
                    if (building.OwnerId == playerId)
                        foreach (var entry in building.Queue) if (entry.UnitDefinitionId == definitionId) count++;
                if (count >= definition.MaxAlivePerPlayer)
                    return CommandResult.Reject(CommandRejection.CannotTrain, definition.DisplayName + ": limit " +
                        definition.MaxAlivePerPlayer + " alive or in training per player.");
            }
            return CommandResult.Success();
        }

        // Used before committing authored spawns or an incoming replica, so a malformed
        // initial state or snapshot cannot bypass the same realm/hero cap as recruitment.
        private void ValidateUnitCollectionRestrictions(IEnumerable<UnitState> observedUnits, IEnumerable<BuildingState> observedBuildings)
        {
            var counts = new Dictionary<(int owner, string definition), int>();
            void Count(int owner, string id)
            {
                var definition = unitDefinitions[id];
                factionCatalog.Assignments.TryGetValue(owner, out var ownerFaction);
                if (!ContentRealms.IsUnitInFactionRoster(id, ownerFaction))
                    throw new ArgumentException("Unit does not belong to this faction's roster: " + id);
                if (!string.IsNullOrEmpty(definition.RequiredRealmId) && definition.RequiredRealmId != Map.RealmId)
                    throw new ArgumentException("A unit cannot enter another competitive realm: " + id);
                if (definition.MaxAlivePerPlayer == 0) return;
                var key = (owner, id);
                counts.TryGetValue(key, out int count);
                if (++count > definition.MaxAlivePerPlayer)
                    throw new ArgumentException("Unit count exceeds the alive/training limit for player " + owner + ": " + id);
                counts[key] = count;
            }
            foreach (var unit in observedUnits)
            {
                Count(unit.OwnerId, unit.DefinitionId);
                foreach (var passenger in unit.Cargo)
                {
                    factionCatalog.ValidateSpawn(passenger.OwnerId, unitDefinitions[passenger.DefinitionId].RequiredFactionId, passenger.DefinitionId);
                    Count(passenger.OwnerId, passenger.DefinitionId);
                }
            }
            foreach (var building in observedBuildings)
                foreach (var entry in building.Queue) Count(building.OwnerId, entry.UnitDefinitionId);
        }
    }
}
