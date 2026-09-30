using System;
using System.Collections.Generic;

namespace Emberfield.Simulation
{
    /// <summary>Faction configuration and progression closure; assignments are copied, never read from mutable map data again.</summary>
    internal sealed class FactionCatalog
    {
        internal readonly Dictionary<string, FactionDefinition> Definitions = new Dictionary<string, FactionDefinition>(StringComparer.Ordinal);
        internal readonly Dictionary<int, string> Assignments = new Dictionary<int, string>();
        internal readonly HashSet<string> TransportIds = new HashSet<string>(StringComparer.Ordinal);

        internal FactionCatalog(GameDefinition game, MapDefinition map, Dictionary<string, UnitDefinition> units,
            Dictionary<string, BuildingDefinition> buildings, TechnologyCatalog technologies)
        {
            if (string.IsNullOrEmpty(map.RealmId)) map.RealmId = ContentRealms.Historical;
            if (string.IsNullOrEmpty(map.BiomeId)) map.BiomeId = "forest";
            if (!ContentRealms.IsValidRealm(map.RealmId)) throw new ArgumentException("Map realm is invalid.");
            var kinds = new HashSet<FactionKind>();
            foreach (var faction in game.Factions ?? Array.Empty<FactionDefinition>())
            {
                if (faction == null || string.IsNullOrWhiteSpace(faction.Id) || Definitions.ContainsKey(faction.Id) ||
                    !Enum.IsDefined(typeof(FactionKind), faction.Kind) || !kinds.Add(faction.Kind))
                    throw new ArgumentException("Faction definitions require unique IDs and supported, unique kinds.");
                if (string.IsNullOrEmpty(faction.RealmId)) faction.RealmId = ContentRealms.Historical;
                if (!ContentRealms.IsValidRealm(faction.RealmId) ||
                    (ContentRealms.RealmForFaction(faction.Id) != null && ContentRealms.RealmForFaction(faction.Id) != faction.RealmId))
                    throw new ArgumentException("Faction realm does not match canonical content membership: " + faction.Id);
                if (faction.ArmorBonus < 0 || faction.ArmorBonus > 10 || faction.ResourceGatherBonus < 0 || faction.ResourceGatherBonus > 10 ||
                    faction.HomeHealingPerSecond < 0 || faction.HomeHealingPerSecond > 10 || faction.MeleeLifeSteal < 0 || faction.MeleeLifeSteal > 10 ||
                    !Enum.IsDefined(typeof(ResourceKind), faction.BonusGatherResource))
                    throw new ArgumentException("Expansion faction bonuses exceed supported bounds.");
                if (faction.WorkerCarryBonus < 0 || faction.CavalrySpeedBonusPermille < 0 || faction.CavalrySpeedBonusPermille > 10000 ||
                    !faction.CharterCost.IsNonnegative || !Duration(faction.CharterTicks) || !Radius(faction.CharterRadiusMillimetres) ||
                    faction.LogisticsCarryBonus < 0 || faction.MusterWorkBonusPermille < 0 || faction.MusterWorkBonusPermille > 10000 ||
                    faction.ReciprocalRadiusBonusMillimetres < 0 || (long)faction.CharterRadiusMillimetres + faction.ReciprocalRadiusBonusMillimetres > 2000000 ||
                    !Duration(faction.ThreadkeeperDeployTicks) || !Duration(faction.ThreadkeeperCooldownTicks) || !Radius(faction.RelayRadiusMillimetres) ||
                    !Duration(faction.PackTicks) || !Duration(faction.DeployTicks) || faction.PreparationReductionTicks < 0 ||
                    faction.PreparationReductionTicks >= faction.PackTicks || faction.PreparationReductionTicks >= faction.DeployTicks ||
                    !Duration(faction.RepositionTicks) || !Duration(faction.RepositionCooldownTicks) ||
                    faction.RepositionSpeedBonusPermille < 0 || faction.RepositionSpeedBonusPermille > 10000)
                    throw new ArgumentException("Faction tuning is outside supported bounds: " + faction.Id);
                Definitions.Add(faction.Id, faction);
            }
            foreach (var assignment in map.PlayerFactions ?? Array.Empty<PlayerFactionDefinition>())
            {
                if (assignment == null || assignment.PlayerId < 1 || string.IsNullOrWhiteSpace(assignment.FactionId) ||
                    !Definitions.ContainsKey(assignment.FactionId) || Assignments.ContainsKey(assignment.PlayerId))
                    throw new ArgumentException("Player faction assignments require existing factions and unique positive players.");
                Assignments.Add(assignment.PlayerId, assignment.FactionId);
                if (Definitions[assignment.FactionId].RealmId != map.RealmId)
                    throw new ArgumentException("Factions from different content realms cannot share a match.");
            }
            foreach (var unit in units.Values)
            {
                ValidateReference(unit.RequiredFactionId);
                if (unit.IsThreadkeeper && (KindOf(unit.RequiredFactionId) != FactionKind.AvenCompact || unit.IsWorker || unit.Attack.Damage != 0 || unit.CanReposition))
                    throw new ArgumentException("Threadkeepers must be Aven, unarmed nonworkers.");
                if (unit.CanReposition && (KindOf(unit.RequiredFactionId) != FactionKind.SerevinMarch || (unit.Tags & CombatTags.Cavalry) == 0 || unit.IsWorker))
                    throw new ArgumentException("Reposition requires a Serevin cavalry unit.");
                foreach (var faction in Definitions.Values)
                {
                    if (!Eligible(unit.RequiredFactionId, faction.Id)) continue;
                    long carry = (long)unit.CarryCapacity + (unit.IsWorker ? (long)faction.WorkerCarryBonus + faction.LogisticsCarryBonus : 0);
                    long speedBonus = ((unit.Tags & CombatTags.Cavalry) != 0 ? faction.CavalrySpeedBonusPermille : 0L) +
                        (unit.CanReposition ? faction.RepositionSpeedBonusPermille : 0L);
                    if (carry > int.MaxValue || (long)unit.MoveSpeedMillimetresPerSecond * (1000L + speedBonus) / 1000 > int.MaxValue - World.TickRate)
                        throw new ArgumentException("Faction bonuses overflow effective unit stats: " + unit.Id);
                }
            }
            foreach (var building in buildings.Values)
            {
                ValidateReference(building.RequiredFactionId);
                if (building.CanCharter && ((!string.IsNullOrEmpty(building.RequiredFactionId) && KindOf(building.RequiredFactionId) != FactionKind.AvenCompact) || !string.IsNullOrEmpty(building.TransportUnitId)))
                    throw new ArgumentException("Only Aven stationary buildings can have charters.");
                if (string.IsNullOrEmpty(building.TransportUnitId)) continue;
                if (KindOf(building.RequiredFactionId) != FactionKind.SerevinMarch || building.PopulationCapacity != 0 ||
                    !units.TryGetValue(building.TransportUnitId, out var transport) || transport.RequiredFactionId != building.RequiredFactionId ||
                    transport.IsWorker || transport.Attack.Damage != 0 || transport.IsThreadkeeper || transport.CanReposition ||
                    transport.PopulationCost != 0 || transport.MaxHealth != building.MaxHealth || !TransportIds.Add(transport.Id))
                    throw new ArgumentException("A relocatable outpost requires a unique, matching-health, unarmed, population-free Serevin transport and no housing.");
            }
            foreach (var building in buildings.Values)
                foreach (string id in building.TrainableUnitIds ?? Array.Empty<string>())
                    if (TransportIds.Contains(id)) throw new ArgumentException("Outpost transports cannot be directly trained.");
            foreach (var technology in technologies.Technologies.Values) ValidateReference(technology.RequiredFactionId);
            foreach (var faction in Definitions.Values)
            {
                ValidateMechanicTechnology(faction.ReciprocalStoresTechnologyId, faction, FactionKind.AvenCompact, technologies);
                ValidateMechanicTechnology(faction.PreparedEncampmentsTechnologyId, faction, FactionKind.SerevinMarch, technologies);
            }
            // A global closure can accidentally borrow the other faction's infrastructure or research.
            // Verify the common and each faction's reachable content independently.
            if (Definitions.Count != 0)
            {
                ValidateReachability(null, units, buildings, technologies);
                foreach (string id in Definitions.Keys) ValidateReachability(id, units, buildings, technologies);
            }
        }

        private static bool Duration(int value) => value > 0 && value <= 36000;
        private static bool Radius(int value) => value > 0 && value <= 2000000;
        internal static bool Eligible(string required, string faction) => string.IsNullOrEmpty(required) || required == faction;
        private FactionKind? KindOf(string id) => !string.IsNullOrEmpty(id) && Definitions.TryGetValue(id, out var faction) ? faction.Kind : (FactionKind?)null;
        private void ValidateReference(string id)
        {
            if (!string.IsNullOrEmpty(id) && !Definitions.ContainsKey(id)) throw new ArgumentException("Unknown faction requirement: " + id);
        }

        private static void ValidateMechanicTechnology(string id, FactionDefinition faction, FactionKind expected, TechnologyCatalog technologies)
        {
            if (string.IsNullOrEmpty(id)) return;
            if (faction.Kind != expected || !technologies.Technologies.TryGetValue(id, out var technology) || technology.RequiredFactionId != faction.Id)
                throw new ArgumentException("Faction mechanic technology must exist and require its owning faction: " + id);
        }

        internal void ValidateSpawn(int owner, string required, string unitId = null)
        {
            Assignments.TryGetValue(owner, out var actual);
            if (!Eligible(required, actual)) throw new ArgumentException("A faction-specific spawn requires the matching player assignment.");
            if (unitId != null && !ContentRealms.IsUnitInFactionRoster(unitId, actual))
                throw new ArgumentException("The faction uses its own replacement for this unit: " + unitId);
            if (unitId != null && TransportIds.Contains(unitId)) throw new ArgumentException("Transports must originate from a packed outpost, not a standalone spawn.");
        }

        private static void ValidateReachability(string faction, Dictionary<string, UnitDefinition> units,
            Dictionary<string, BuildingDefinition> buildings, TechnologyCatalog technologies)
        {
            var completed = new HashSet<string>(StringComparer.Ordinal);
            var available = new HashSet<string>(StringComparer.Ordinal);
            int lastTier = technologies.Eras.Count == 0 ? 1 : 4;
            for (int tier = 1; tier <= lastTier; tier++)
            {
                bool changed;
                do
                {
                    changed = false;
                    foreach (var building in buildings.Values)
                        if (Eligible(building.RequiredFactionId, faction) && technologies.RequiredTier(building.RequiredEraId) <= tier && HasAll(completed, building.RequiredTechnologyIds))
                            changed |= available.Add(building.Id);
                    foreach (var technology in technologies.Technologies.Values)
                        if (Eligible(technology.RequiredFactionId, faction) && string.IsNullOrEmpty(technology.AdvancesToEraId) &&
                            technologies.RequiredTier(technology.RequiredEraId) <= tier && available.Contains(technology.ResearchBuildingId) &&
                            HasAll(available, technology.RequiredBuildingIds) && HasAll(completed, technology.RequiredTechnologyIds))
                            changed |= completed.Add(technology.Id);
                } while (changed);
                if (tier == lastTier) break;
                TechnologyDefinition advance = null;
                foreach (var technology in technologies.Technologies.Values)
                    if (!string.IsNullOrEmpty(technology.AdvancesToEraId) && technologies.Eras[technology.AdvancesToEraId].Tier == tier + 1) advance = technology;
                if (advance == null || !Eligible(advance.RequiredFactionId, faction) || !available.Contains(advance.ResearchBuildingId) ||
                    !HasAll(available, advance.RequiredBuildingIds) || !HasAll(completed, advance.RequiredTechnologyIds))
                    throw new ArgumentException("Era advancement cannot be reached by faction: " + (faction ?? "neutral"));
                completed.Add(advance.Id);
            }
            foreach (var building in buildings.Values)
                if (Eligible(building.RequiredFactionId, faction) && !available.Contains(building.Id)) throw new ArgumentException("Faction building prerequisites are unreachable: " + building.Id);
            foreach (var technology in technologies.Technologies.Values)
                if (Eligible(technology.RequiredFactionId, faction) && !completed.Contains(technology.Id)) throw new ArgumentException("Faction technology prerequisites are unreachable: " + technology.Id);
            foreach (var unit in units.Values)
                if (Eligible(unit.RequiredFactionId, faction) && !HasAll(completed, unit.RequiredTechnologyIds)) throw new ArgumentException("Faction unit prerequisites are unreachable: " + unit.Id);
        }

        private static bool HasAll(HashSet<string> values, string[] required)
        { foreach (string id in required ?? Array.Empty<string>()) if (!values.Contains(id)) return false; return true; }
    }
}
