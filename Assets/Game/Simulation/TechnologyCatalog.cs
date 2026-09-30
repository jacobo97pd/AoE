using System;
using System.Collections.Generic;

namespace Emberfield.Simulation
{
    /// <summary>Validates research references and reachability independently of authored sandbox spawns.</summary>
    internal sealed class TechnologyCatalog
    {
        internal readonly Dictionary<string, EraDefinition> Eras = new Dictionary<string, EraDefinition>(StringComparer.Ordinal);
        internal readonly Dictionary<string, TechnologyDefinition> Technologies = new Dictionary<string, TechnologyDefinition>(StringComparer.Ordinal);
        private readonly EraDefinition[] erasByTier = new EraDefinition[5];
        private readonly TechnologyDefinition[] advancesByTier = new TechnologyDefinition[5];
        internal string InitialEraId => erasByTier[1]?.Id;

        internal TechnologyCatalog(GameDefinition definition, Dictionary<string, UnitDefinition> units,
            Dictionary<string, BuildingDefinition> buildings)
        {
            foreach (var era in definition.Eras ?? Array.Empty<EraDefinition>())
            {
                if (era == null || string.IsNullOrWhiteSpace(era.Id) || Eras.ContainsKey(era.Id) ||
                    era.Tier < 1 || era.Tier > 4 || erasByTier[era.Tier] != null)
                    throw new ArgumentException("Eras require unique IDs and unique tiers from one through four.");
                Eras.Add(era.Id, era); erasByTier[era.Tier] = era;
            }
            if (Eras.Count != 0 && Eras.Count != 4)
                throw new ArgumentException("Configured eras must contain the complete four-tier chain.");
            foreach (var technology in definition.Technologies ?? Array.Empty<TechnologyDefinition>())
            {
                if (technology == null || string.IsNullOrWhiteSpace(technology.Id) || Technologies.ContainsKey(technology.Id))
                    throw new ArgumentException("Technologies require unique, nonempty IDs.");
                if (technology.ResearchBuildingId == null || !buildings.ContainsKey(technology.ResearchBuildingId) ||
                    !technology.Cost.IsNonnegative || technology.ResearchTicks < 1)
                    throw new ArgumentException("Technology producer, cost, or duration is invalid: " + technology.Id);
                Technologies.Add(technology.Id, technology);
            }
            foreach (var unit in units.Values) ValidateRequirements(unit.RequiredEraId, unit.RequiredTechnologyIds);
            foreach (var building in buildings.Values) ValidateRequirements(building.RequiredEraId, building.RequiredTechnologyIds);
            foreach (var technology in Technologies.Values)
            {
                ValidateRequirements(technology.RequiredEraId, technology.RequiredTechnologyIds);
                ValidateIds(technology.RequiredBuildingIds, buildings, "required building");
                ValidateEffects(technology);
                foreach (var effect in technology.Effects ?? Array.Empty<TechnologyEffect>())
                    if (!string.IsNullOrEmpty(effect.TargetUnitId) && !units.ContainsKey(effect.TargetUnitId))
                        throw new ArgumentException("Technology targets an unknown unit.");
                if (string.IsNullOrEmpty(technology.AdvancesToEraId)) continue;
                if (!Eras.TryGetValue(technology.AdvancesToEraId, out var target) ||
                    string.IsNullOrEmpty(technology.RequiredEraId) || !Eras.TryGetValue(technology.RequiredEraId, out var prior) ||
                    target.Tier != prior.Tier + 1 || advancesByTier[target.Tier] != null)
                    throw new ArgumentException("Each era advancement must uniquely link exactly the preceding era: " + technology.Id);
                advancesByTier[target.Tier] = technology;
            }
            if (Eras.Count != 0)
                for (int tier = 2; tier <= 4; tier++)
                    if (advancesByTier[tier] == null) throw new ArgumentException("Every configured era needs its advancement technology.");
            ValidateCycles();
            ValidateReachability(buildings);
            ValidateCumulativeStats(units);
        }

        internal int RequiredTier(string eraId) => string.IsNullOrEmpty(eraId) ? 1 : Eras[eraId].Tier;

        private void ValidateRequirements(string eraId, string[] technologies)
        {
            if (!string.IsNullOrEmpty(eraId) && !Eras.ContainsKey(eraId))
                throw new ArgumentException("A prerequisite references an unknown era: " + eraId);
            ValidateIds(technologies, Technologies, "required technology");
        }

        private static void ValidateIds<T>(string[] ids, Dictionary<string, T> definitions, string label)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var id in ids ?? Array.Empty<string>())
                if (string.IsNullOrWhiteSpace(id) || !definitions.ContainsKey(id) || !seen.Add(id))
                    throw new ArgumentException("Invalid or duplicate " + label + " reference.");
        }

        private static void ValidateEffects(TechnologyDefinition technology)
        {
            const CombatTags all = CombatTags.Worker | CombatTags.Infantry | CombatTags.Cavalry | CombatTags.Ranged |
                CombatTags.Light | CombatTags.Heavy | CombatTags.Structure | CombatTags.Creature | CombatTags.Siege;
            foreach (var effect in technology.Effects ?? Array.Empty<TechnologyEffect>())
                if (effect == null || effect.Kind < TechnologyEffectKind.AttackDamage || effect.Kind > TechnologyEffectKind.GatherAmount ||
                    effect.TargetTags == CombatTags.None || (effect.TargetTags & ~all) != 0 || effect.Amount < 1)
                    throw new ArgumentException("Technology effects require a supported kind, nonempty known tag mask, and positive amount.");
        }

        private void ValidateCycles()
        {
            var pending = new Dictionary<string, int>(StringComparer.Ordinal);
            var dependents = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            var ready = new Queue<string>();
            foreach (var technology in Technologies.Values)
            {
                int count = technology.RequiredTechnologyIds?.Length ?? 0;
                pending.Add(technology.Id, count);
                if (count == 0) ready.Enqueue(technology.Id);
                foreach (var dependency in technology.RequiredTechnologyIds ?? Array.Empty<string>())
                {
                    if (!dependents.TryGetValue(dependency, out var list))
                    { list = new List<string>(); dependents.Add(dependency, list); }
                    list.Add(technology.Id);
                }
            }
            int visited = 0;
            while (ready.Count > 0)
            {
                string id = ready.Dequeue(); visited++;
                if (!dependents.TryGetValue(id, out var list)) continue;
                foreach (var dependent in list)
                { pending[dependent]--; if (pending[dependent] == 0) ready.Enqueue(dependent); }
            }
            if (visited != Technologies.Count) throw new ArgumentException("Technology prerequisites contain a cycle.");
        }

        private void ValidateReachability(Dictionary<string, BuildingDefinition> buildings)
        {
            var completed = new HashSet<string>(StringComparer.Ordinal);
            var availableBuildings = new HashSet<string>(StringComparer.Ordinal);
            int lastTier = Eras.Count == 0 ? 1 : 4;
            for (int tier = 1; tier <= lastTier; tier++)
            {
                bool changed;
                do
                {
                    changed = false;
                    foreach (var building in buildings.Values)
                        if (!availableBuildings.Contains(building.Id) && RequiredTier(building.RequiredEraId) <= tier &&
                            HasAll(completed, building.RequiredTechnologyIds)) changed |= availableBuildings.Add(building.Id);
                    foreach (var technology in Technologies.Values)
                        if (string.IsNullOrEmpty(technology.AdvancesToEraId) && !completed.Contains(technology.Id) &&
                            CanReach(technology, tier, completed, availableBuildings)) changed |= completed.Add(technology.Id);
                } while (changed);
                if (tier == lastTier) break;
                var advance = advancesByTier[tier + 1];
                if (!CanReach(advance, tier, completed, availableBuildings))
                    throw new ArgumentException("Era advancement is locked behind an unreachable prerequisite: " + advance.Id);
                completed.Add(advance.Id);
            }
            if (completed.Count != Technologies.Count || availableBuildings.Count != buildings.Count)
                throw new ArgumentException("Technology or building prerequisites cannot be reached from the initial era.");
        }

        private bool CanReach(TechnologyDefinition technology, int tier, HashSet<string> completed, HashSet<string> buildings) =>
            RequiredTier(technology.RequiredEraId) <= tier && HasAll(completed, technology.RequiredTechnologyIds) &&
            buildings.Contains(technology.ResearchBuildingId) && HasAll(buildings, technology.RequiredBuildingIds);

        private static bool HasAll(HashSet<string> set, string[] ids)
        { foreach (string id in ids ?? Array.Empty<string>()) if (!set.Contains(id)) return false; return true; }

        private void ValidateCumulativeStats(Dictionary<string, UnitDefinition> units)
        {
            foreach (var unit in units.Values)
            {
                long damage = unit.Attack.Damage, armor = unit.Armor, gather = unit.GatherAmount;
                foreach (var technology in Technologies.Values)
                foreach (var effect in technology.Effects ?? Array.Empty<TechnologyEffect>())
                {
                    if ((unit.Tags & effect.TargetTags) == 0 || (!string.IsNullOrEmpty(effect.TargetUnitId) && effect.TargetUnitId != unit.Id)) continue;
                    switch (effect.Kind)
                    {
                        case TechnologyEffectKind.AttackDamage: damage += effect.Amount; break;
                        case TechnologyEffectKind.Armor: armor += effect.Amount; break;
                        case TechnologyEffectKind.GatherAmount: gather += effect.Amount; break;
                    }
                    if (damage > int.MaxValue || armor > int.MaxValue || gather > int.MaxValue)
                        throw new ArgumentException("Cumulative technology effects overflow unit stats: " + unit.Id);
                }
            }
        }
    }
}
