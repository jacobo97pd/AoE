using System;
using System.Collections.Generic;

namespace Emberfield.Simulation
{
    internal static class DefinitionValidation
    {
        internal static Dictionary<string, UnitDefinition> Units(UnitDefinition[] values, int cellSize)
        {
            var result = new Dictionary<string, UnitDefinition>(StringComparer.Ordinal);
            foreach (var value in values ?? Array.Empty<UnitDefinition>())
            {
                if (value == null || string.IsNullOrWhiteSpace(value.Id) || result.ContainsKey(value.Id))
                    throw new ArgumentException("Unit definitions require unique, nonempty IDs.");
                if (value.MaxHealth < 1 || value.MoveSpeedMillimetresPerSecond < 1 || value.MoveSpeedMillimetresPerSecond > 1000000 ||
                    value.RadiusMillimetres < 1 || value.RadiusMillimetres > cellSize / 2 || !value.Cost.IsNonnegative ||
                    value.PopulationCost < 0 || value.TrainTicks < 1 || value.CarryCapacity < 1 ||
                    value.GatherIntervalTicks < 1 || value.GatherAmount < 1)
                    throw new ArgumentException("Unit health, speed, or radius is outside supported bounds: " + value.Id);
                result.Add(value.Id, value);
                if (!string.IsNullOrEmpty(value.RequiredRealmId) && !ContentRealms.IsValidRealm(value.RequiredRealmId) ||
                    value.MaxAlivePerPlayer < 0 || value.MaxAlivePerPlayer > 1024)
                    throw new ArgumentException("Unit realm requirement or per-player limit is invalid: " + value.Id);
                string factionRealm = ContentRealms.RealmForFaction(value.RequiredFactionId);
                if (!string.IsNullOrEmpty(value.RequiredRealmId) && factionRealm != null && factionRealm != value.RequiredRealmId)
                    throw new ArgumentException("Unit realm and faction requirements conflict: " + value.Id);
                if (!Enum.IsDefined(typeof(SiegeEquipmentKind), value.SiegeEquipment) || value.RegenerationPerSecond < 0 || value.RegenerationPerSecond > 20)
                    throw new ArgumentException("Creature regeneration or siege equipment is invalid.");
                // A ship is a naval hull that never works a resource or climbs a wall; only a ship carries troops.
                bool ship = value.Domain == MovementDomain.Water;
                if (!Enum.IsDefined(typeof(MovementDomain), value.Domain) || value.CargoCapacity < 0 || value.CargoCapacity > 32 ||
                    value.CargoCapacity > 0 && !ship || ship != ((value.Tags & CombatTags.Naval) != 0) ||
                    ship && (value.IsWorker || value.SiegeEquipment != SiegeEquipmentKind.None || value.IsThreadkeeper || value.CanReposition))
                    throw new ArgumentException("A ship must be a naval, water-domain hull, and only a ship carries cargo: " + value.Id);
                ValidateCombat(value.Tags, value.Armor, value.Attack);
            }
            return result;
        }

        internal static Dictionary<string, BuildingDefinition> Buildings(BuildingDefinition[] values, int width, int height)
        {
            var result = new Dictionary<string, BuildingDefinition>(StringComparer.Ordinal);
            foreach (var value in values ?? Array.Empty<BuildingDefinition>())
            {
                if (value == null || string.IsNullOrWhiteSpace(value.Id) || result.ContainsKey(value.Id))
                    throw new ArgumentException("Building definitions require unique, nonempty IDs.");
                if (value.MaxHealth < 1 || value.WidthCells < 1 || value.DepthCells < 1 || value.WidthCells > width || value.DepthCells > height ||
                    !value.Cost.IsNonnegative || value.BuildTicks < 1 || value.PopulationCapacity < 0)
                    throw new ArgumentException("Building health or footprint is outside supported bounds: " + value.Id);
                result.Add(value.Id, value);
                ValidateCombat(value.Tags, value.Armor, value.Attack ?? new AttackDefinition());
                if ((value.IsWall && value.IsGate) || value.WallCapacity < 1 || value.WallCapacity > 32 || value.WallHeightMillimetres < 1 ||
                    value.WallHeightMillimetres > 10000 || value.OilDamage < 0 || value.OilDamage > 1000 || value.OilCooldownTicks < 1)
                    throw new ArgumentException("Wall, gate or defensive oil settings are invalid.");
                if (value.RequiresShore && (value.IsWall || value.IsGate || !string.IsNullOrEmpty(value.TransportUnitId)))
                    throw new ArgumentException("A shore building cannot be a wall, a gate or a relocatable outpost: " + value.Id);
            }
            return result;
        }

        internal static Dictionary<string, ResourceDefinition> Resources(ResourceDefinition[] values)
        {
            var result = new Dictionary<string, ResourceDefinition>(StringComparer.Ordinal);
            foreach (var value in values ?? Array.Empty<ResourceDefinition>())
            {
                if (value == null || string.IsNullOrWhiteSpace(value.Id) || result.ContainsKey(value.Id))
                    throw new ArgumentException("Resource definitions require unique, nonempty IDs.");
                if (!Enum.IsDefined(typeof(ResourceKind), value.Kind) || value.InitialAmount < 1)
                    throw new ArgumentException("Resource kind or amount is invalid: " + value.Id);
                result.Add(value.Id, value);
            }
            return result;
        }

        private static void ValidateCombat(CombatTags tags, int armor, AttackDefinition attack, bool requireAttack = true)
        {
            const CombatTags allTags = CombatTags.Worker | CombatTags.Infantry | CombatTags.Cavalry | CombatTags.Ranged |
                CombatTags.Light | CombatTags.Heavy | CombatTags.Structure | CombatTags.Creature | CombatTags.Siege | CombatTags.Naval;
            if ((tags & ~allTags) != 0 || armor < 0 || armor > 1000000)
                throw new ArgumentException("Combat tags or armor are outside supported bounds.");
            if (!requireAttack) return;
            if (attack == null || attack.Damage < 0 || attack.Damage > 1000000 || attack.RangeMillimetres < 0 ||
                attack.RangeMillimetres > 2000000 || attack.AcquireRangeMillimetres < attack.RangeMillimetres ||
                attack.AcquireRangeMillimetres > 2000000 || attack.CooldownTicks < 1 ||
                attack.ProjectileSpeedMillimetresPerSecond < 0 || attack.ProjectileSpeedMillimetresPerSecond > 1000000 ||
                attack.ProjectileLifetimeTicks < 1 || attack.ProjectileLifetimeTicks > 36000)
                throw new ArgumentException("Attack configuration is outside supported bounds.");
            if (attack.SplashRadiusMillimetres < 0 || attack.SplashRadiusMillimetres > 3000 || attack.SplashDamagePermille < 0 || attack.SplashDamagePermille > 1000 ||
                (attack.SplashRadiusMillimetres == 0) != (attack.SplashDamagePermille == 0))
                throw new ArgumentException("Splash attacks require a bounded radius and damage percentage together.");
            foreach (var bonus in attack.Bonuses ?? Array.Empty<DamageBonus>())
                if (bonus == null || bonus.TargetTags == CombatTags.None || (bonus.TargetTags & ~allTags) != 0 ||
                    bonus.MultiplierPermille < 1 || bonus.MultiplierPermille > 100000)
                    throw new ArgumentException("Damage bonus tags or multiplier are outside supported bounds.");
        }
    }
}
