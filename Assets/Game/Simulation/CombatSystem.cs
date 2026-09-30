using System;
using System.Collections.Generic;

namespace Emberfield.Simulation
{
    /// <summary>Deterministic attack decisions, integer projectile travel, and single-application damage.</summary>
    internal sealed class CombatSystem
    {
        private const int AcquireCadenceTicks = 10;
        private const int ChaseCadenceTicks = 5;
        private readonly World world;
        private readonly List<int> actors = new List<int>();
        private readonly List<Candidate> candidates = new List<Candidate>();
        private readonly List<MeleeHit> meleeHits = new List<MeleeHit>();
        private readonly List<int> defensiveActors = new List<int>();
        private readonly List<int> oilTargets = new List<int>();
        private readonly List<int> splashTargets = new List<int>();
        private static readonly IComparer<Candidate> Nearest = new NearestFirst();

        private readonly struct Candidate
        {
            internal readonly int Id, Distance;
            internal Candidate(int id, int distance) { Id = id; Distance = distance; }
        }
        // A shared comparer: sorting with a lambda wraps it in a new comparer on every call under Mono.
        private sealed class NearestFirst : IComparer<Candidate>
        {
            public int Compare(Candidate a, Candidate b) => a.Distance != b.Distance ? a.Distance.CompareTo(b.Distance) : a.Id.CompareTo(b.Id);
        }

        private readonly struct MeleeHit
        {
            internal readonly int TargetId, Damage, SourceId;
            internal readonly int SourceOwner, BaseDamage;
            internal readonly AttackDefinition Attack;
            internal MeleeHit(int targetId, int damage, int sourceId = 0, int sourceOwner = 0, int baseDamage = 0, AttackDefinition attack = null)
            { TargetId = targetId; Damage = damage; SourceId = sourceId; SourceOwner = sourceOwner; BaseDamage = baseDamage; Attack = attack; }
        }

        internal CombatSystem(World world) { this.world = world; }

        internal CommandResult Submit(AttackCommand command)
        {
            if (!world.TryGetPlayer(command.PlayerId, out _))
                return CommandResult.Reject(CommandRejection.InvalidPlayer, "Player does not exist.");
            if (command.UnitIds.Count == 0) return CommandResult.Reject(CommandRejection.EmptySelection, "Select at least one armed unit.");
            if (command.UnitIds.Count > 1024) return CommandResult.Reject(CommandRejection.TooManyUnits, "Selection exceeds the command limit.");
            var selected = new List<UnitState>(command.UnitIds.Count);
            var seen = new HashSet<int>();
            foreach (int id in command.UnitIds)
            {
                if (!seen.Add(id)) return CommandResult.Reject(CommandRejection.DuplicateUnit, "Selection contains a duplicate unit.");
                if (!world.TryGetUnit(id, out var unit)) return CommandResult.Reject(CommandRejection.UnknownUnit, "A selected unit is no longer alive.");
                if (unit.OwnerId != command.PlayerId) return CommandResult.Reject(CommandRejection.NotOwner, "You do not own every selected unit.");
                if (FactionSystem.AttackSuppressed(unit))
                    return CommandResult.Reject(CommandRejection.FactionActionBusy, "A selected unit cannot attack during its faction action.");
                if (unit.AttackDamage == 0)
                    return CommandResult.Reject(CommandRejection.Unarmed, "Every selected unit must have an attack.");
                selected.Add(unit);
            }
            if (world.Vision != null && !world.Vision.IsEntityVisible(command.PlayerId, command.TargetEntityId))
                return CommandResult.Reject(CommandRejection.TargetNotVisible, "The attack target is not currently visible.");
            if (!TryTarget(command.TargetEntityId, out var target))
                return CommandResult.Reject(CommandRejection.UnknownTarget, "Attack target is no longer alive or cannot be attacked.");
            if (target.OwnerId == command.PlayerId) return CommandResult.Reject(CommandRejection.FriendlyTarget, "Friendly entities cannot be attacked.");
            selected.Sort((a, b) => a.Id.CompareTo(b.Id));
            var routes = new RoutePlan[selected.Count];
            // Ships sent with troops stay out of a fight they cannot sail to; ships alone, like troops, must all reach it.
            bool withTroops = false;
            foreach (var unit in selected) withTroops |= unit.Domain == MovementDomain.Land;
            bool[] ashore = null;
            for (int i = 0; i < selected.Count; i++)
            {
                var unit = selected[i];
                if (unit.AttackTargetId == target.Id) continue;
                var attack = world.unitDefinitions[unit.DefinitionId].Attack;
                bool reachable = CanReachElevation(unit, target, attack);
                int range = EffectiveRange(unit, attack);
                if (reachable && !target.Geometry.InRange(unit.Position, unit.RadiusMillimetres, range) &&
                    (unit.WallId != 0 || !world.NavigationFor(unit).TryFindAttackRoute(unit.Position, unit.RadiusMillimetres, target.Geometry, range, out routes[i])))
                {
                    if (unit.Domain == MovementDomain.Water && withTroops) { (ashore ??= new bool[selected.Count])[i] = true; continue; }
                    return CommandResult.Reject(CommandRejection.NoPath, "Every attacker needs a reachable firing position.");
                }
                if (reachable) continue;
                if (unit.Domain == MovementDomain.Water && withTroops) { (ashore ??= new bool[selected.Count])[i] = true; continue; }
                return CommandResult.Reject(CommandRejection.OutOfRange, (target.Tags & CombatTags.Naval) != 0
                    ? "Only ranged attacks reach a ship." : "Archers or soldiers on the same wall deck can attack elevated defenders.");
            }
            for (int i = 0; i < selected.Count; i++)
            {
                var unit = selected[i];
                if (ashore != null && ashore[i]) continue;
                NavalSystem.CancelOrders(unit);
                if (unit.AttackTargetId == target.Id)
                { unit.AutoAttackEnabled = true; unit.AttackIsExplicit = true; continue; }
                CancelAttack(unit, true);
                MovementSystem.Halt(unit);
                MovementSystem.CancelWorkerTask(unit);
                unit.AttackTargetId = target.Id; unit.AttackIsExplicit = true;
                unit.NextChaseTick = world.TickIndex + ChaseCadenceTicks;
                if (routes[i].Path != null)
                { MovementSystem.SetRoute(unit, routes[i]); unit.IsAttackChasing = true; }
            }
            return CommandResult.Success();
        }

        internal static void CancelAttack(UnitState unit, bool enableAutoAttack)
        {
            if (unit.IsAttackChasing) MovementSystem.Halt(unit);
            unit.AttackTargetId = 0; unit.IsAttackChasing = false; unit.AttackIsExplicit = false;
            unit.AutoAttackEnabled = enableAutoAttack;
            unit.NextAcquireTick = 0; unit.NextChaseTick = 0;
        }

        internal void Tick()
        {
            foreach (var unit in world.units)
                if (unit.AttackCooldownTicks > 0) unit.AttackCooldownTicks--;
            AdvanceProjectiles();
            UpdateDefenses();
            world.Vision?.Refresh();
            actors.Clear();
            meleeHits.Clear();
            foreach (var unit in world.units) actors.Add(unit.Id);
            foreach (int id in actors)
                if (world.TryGetUnit(id, out var unit)) UpdateUnit(unit);
            // All actors alive at the decision boundary may strike, including mutually lethal opponents.
            foreach (var hit in meleeHits)
                if (TryTarget(hit.TargetId, out var target))
                {
                    SimPoint impact = target.Geometry.Position;
                    Splash(impact, target.Id, hit.SourceOwner, hit.Attack, hit.BaseDamage);
                    ApplyDamage(target, hit.Damage);
                    // Life steal feeds on fighting troops, not on demolishing buildings.
                    if (target.Unit != null && hit.SourceId != 0 && world.TryGetUnit(hit.SourceId, out var source))
                    {
                        var faction = world.factions.DefinitionFor(source.OwnerId);
                        if (faction != null && faction.MeleeLifeSteal > 0) source.Health = (int)Math.Min(source.MaxHealth, (long)source.Health + faction.MeleeLifeSteal);
                    }
                }
        }

        private void UpdateUnit(UnitState unit)
        {
            var attack = unit.Definition.Attack;
            if (unit.AttackDamage == 0 || FactionSystem.AttackSuppressed(unit)) return;
            if (unit.AttackTargetId == 0)
            {
                if (!unit.AutoAttackEnabled || unit.Order != UnitOrder.Idle || unit.WorkerTask != WorkerTask.None ||
                    world.TickIndex < unit.NextAcquireTick) return;
                unit.NextAcquireTick = world.TickIndex + AcquireCadenceTicks;
                if (!Acquire(unit, attack)) return;
            }
            if ((world.Vision != null && !world.Vision.IsEntityVisible(unit.OwnerId, unit.AttackTargetId)) ||
                !TryTarget(unit.AttackTargetId, out var target) || target.OwnerId == unit.OwnerId || !CanReachElevation(unit, target, attack))
            {
                CancelAttack(unit, unit.AutoAttackEnabled);
                unit.NextAcquireTick = world.TickIndex + AcquireCadenceTicks;
                return;
            }
            if (!unit.AttackIsExplicit && !target.Geometry.InRange(unit.Position, unit.RadiusMillimetres, Math.Max(attack.AcquireRangeMillimetres, EffectiveRange(unit, attack))))
            {
                CancelAttack(unit, true);
                unit.NextAcquireTick = world.TickIndex + AcquireCadenceTicks;
                return;
            }
            int range = EffectiveRange(unit, attack);
            if (!target.Geometry.InRange(unit.Position, unit.RadiusMillimetres, range))
            {
                if (unit.WallId != 0) { CancelAttack(unit, true); return; }
                // Across the shore a unit fights only what it was sent against or what is already in reach: it does not
                // go hunting a firing position on its own grid for a target on the other one.
                if (!unit.AttackIsExplicit && AcrossShore(unit, target))
                { CancelAttack(unit, true); unit.NextAcquireTick = world.TickIndex + AcquireCadenceTicks; return; }
                if (world.TickIndex < unit.NextChaseTick) return;
                unit.NextChaseTick = world.TickIndex + ChaseCadenceTicks;
                if (world.NavigationFor(unit).TryFindAttackRoute(unit.Position, unit.RadiusMillimetres, target.Geometry, range, out var route))
                { MovementSystem.SetRoute(unit, route); unit.IsAttackChasing = true; }
                else { MovementSystem.Halt(unit); unit.IsAttackChasing = true; }
                return;
            }
            if (unit.IsAttackChasing) { MovementSystem.Halt(unit); unit.IsAttackChasing = false; }
            if (unit.AttackCooldownTicks != 0) return;
            int damage = DamageFor(attack, unit.AttackDamage, target.Tags, target.Armor);
            if (attack.ProjectileSpeedMillimetresPerSecond == 0) meleeHits.Add(new MeleeHit(target.Id, damage, unit.Id, unit.OwnerId, unit.AttackDamage, attack));
            else
            {
                if (!world.HasEntityId) return;
                world.projectiles.Add(new ProjectileState(world.AllocateEntityId(), unit, target.Id, damage,
                    attack.ProjectileSpeedMillimetresPerSecond, attack.ProjectileLifetimeTicks, attack));
            }
            unit.AttackCooldownTicks = attack.CooldownTicks;
        }

        private bool Acquire(UnitState unit, AttackDefinition attack)
        {
            candidates.Clear();
            foreach (var enemy in world.units)
            {
                if (enemy.OwnerId == unit.OwnerId) continue;
                if (world.Vision != null && !world.Vision.IsUnitVisible(unit.OwnerId, enemy)) continue;
                var geometry = new TargetGeometry(enemy);
                if (geometry.InRange(unit.Position, unit.RadiusMillimetres, Math.Max(attack.AcquireRangeMillimetres, EffectiveRange(unit, attack))))
                    candidates.Add(new Candidate(enemy.Id, geometry.EdgeDistance(unit.Position, unit.RadiusMillimetres)));
            }
            foreach (var enemy in world.buildings)
            {
                if (enemy.OwnerId == unit.OwnerId) continue;
                if (world.Vision != null && !world.Vision.IsBuildingVisible(unit.OwnerId, enemy)) continue;
                var geometry = new TargetGeometry(enemy, world.cellSize);
                if (geometry.InRange(unit.Position, unit.RadiusMillimetres, Math.Max(attack.AcquireRangeMillimetres, EffectiveRange(unit, attack))))
                    candidates.Add(new Candidate(enemy.Id, geometry.EdgeDistance(unit.Position, unit.RadiusMillimetres)));
            }
            candidates.Sort(Nearest);
            foreach (var candidate in candidates)
            {
                if (!TryTarget(candidate.Id, out var target)) continue;
                if (!CanReachElevation(unit, target, attack)) continue;
                var route = default(RoutePlan);
                int range = EffectiveRange(unit, attack);
                if (!target.Geometry.InRange(unit.Position, unit.RadiusMillimetres, range) &&
                    (unit.WallId != 0 || AcrossShore(unit, target) || !world.NavigationFor(unit).TryFindAttackRoute(unit.Position, unit.RadiusMillimetres, target.Geometry, range, out route))) continue;
                unit.AttackTargetId = candidate.Id; unit.AttackIsExplicit = false;
                unit.MoveGroupId = 0; unit.MoveGroupSize = 0;
                unit.NextChaseTick = world.TickIndex + ChaseCadenceTicks;
                if (route.Path != null) { MovementSystem.SetRoute(unit, route); unit.IsAttackChasing = true; }
                return true;
            }
            return false;
        }

        private void AdvanceProjectiles()
        {
            for (int i = 0; i < world.projectiles.Count;)
            {
                var projectile = world.projectiles[i];
                projectile.PreviousPosition = projectile.Position;
                projectile.RemainingLifetimeTicks--;
                if (!TryTarget(projectile.TargetEntityId, out var target))
                { world.projectiles.RemoveAt(i); continue; }
                int numerator = projectile.Speed + projectile.SpeedRemainder;
                int budget = numerator / World.TickRate;
                projectile.SpeedRemainder = numerator % World.TickRate;
                projectile.Position = StepTowards(projectile.Position, target.Geometry.Position, budget);
                if (target.Geometry.InRange(projectile.Position, 0, 0))
                {
                    Splash(target.Geometry.Position, target.Id, projectile.OwnerId, projectile.SplashAttack, projectile.SplashBaseDamage);
                    ApplyDamage(target, projectile.Damage);
                    world.projectiles.RemoveAt(i);
                }
                else if (projectile.RemainingLifetimeTicks == 0) world.projectiles.RemoveAt(i);
                else i++;
            }
        }

        private void Splash(SimPoint impact, int primaryId, int ownerId, AttackDefinition attack, int baseDamage)
        {
            if (attack == null || attack.SplashRadiusMillimetres == 0) return;
            splashTargets.Clear();
            foreach (var unit in world.units)
                if (unit.Id != primaryId && unit.OwnerId != ownerId && unit.WallId == 0 &&
                    SimPoint.DistanceCeiling(impact, unit.Position) <= attack.SplashRadiusMillimetres) splashTargets.Add(unit.Id);
            foreach (int id in splashTargets)
                if (world.TryGetUnit(id, out var unit))
                    ApplyDamage(new CombatTarget(unit), Math.Max(1, (int)((long)DamageFor(attack, baseDamage, unit.Tags, unit.Armor) * attack.SplashDamagePermille / 1000)));
        }

        // A ship and anything ashore (troops, buildings) move on different grids.
        private static bool AcrossShore(UnitState unit, CombatTarget target) =>
            unit.Domain != (target.Unit != null ? target.Unit.Domain : MovementDomain.Land);

        private static int EffectiveRange(UnitState unit, AttackDefinition attack) =>
            attack.RangeMillimetres + (unit.WallId != 0 && attack.ProjectileSpeedMillimetresPerSecond > 0 ? 1500 : 0);

        // Also the reach of a blade across the shore: only shot and shell reach a ship, so troops on the beach
        // cannot hack at a hull lying off it (a ship's own attack is always a projectile).
        private static bool CanReachElevation(UnitState attacker, CombatTarget target, AttackDefinition attack)
        {
            if (attack.ProjectileSpeedMillimetresPerSecond > 0) return true;
            if ((target.Tags & CombatTags.Naval) != 0) return false;
            if (target.Unit == null) return attacker.WallId == 0;
            return attacker.WallId == target.Unit.WallId;
        }

        private void UpdateDefenses()
        {
            defensiveActors.Clear();
            foreach (var building in world.buildings) if (building.IsOperational) defensiveActors.Add(building.Id);
            foreach (int id in defensiveActors)
            {
                if (!world.TryGetBuilding(id, out var building)) continue;
                var definition = building.Definition;
                var attack = definition.Attack;
                if (building.AttackCooldownTicks > 0) building.AttackCooldownTicks--;
                if (building.OilCooldownTicks > 0) building.OilCooldownTicks--;
                if (attack != null && attack.Damage > 0 && building.AttackCooldownTicks == 0)
                {
                    UnitState selected = null; int nearest = int.MaxValue;
                    var reach = new TargetGeometry(building, world.cellSize);
                    foreach (var enemy in world.units)
                    {
                        if (enemy.OwnerId == building.OwnerId || (world.Vision != null && !world.Vision.IsUnitVisible(building.OwnerId, enemy))) continue;
                        int distance = reach.EdgeDistance(enemy.Position, enemy.RadiusMillimetres);
                        if (distance > attack.RangeMillimetres || (distance == nearest && selected != null && enemy.Id >= selected.Id) || distance > nearest) continue;
                        nearest = distance; selected = enemy;
                    }
                    if (selected != null && world.HasEntityId)
                    {
                        int damage = DamageFor(attack, attack.Damage, selected.Tags, selected.Armor);
                        if (attack.ProjectileSpeedMillimetresPerSecond > 0)
                            world.projectiles.Add(new ProjectileState(world.AllocateEntityId(), building, selected.Id, damage, attack.ProjectileSpeedMillimetresPerSecond, attack.ProjectileLifetimeTicks));
                        else ApplyDamage(new CombatTarget(selected), damage);
                        building.AttackCooldownTicks = attack.CooldownTicks;
                    }
                }
                if (definition.OilDamage == 0 || building.OilCooldownTicks != 0) continue;
                oilTargets.Clear();
                var geometry = new TargetGeometry(building, world.cellSize);
                foreach (var enemy in world.units)
                    if (enemy.OwnerId != building.OwnerId && enemy.WallId == 0 && geometry.InRange(enemy.Position, enemy.RadiusMillimetres, 1000) &&
                        (world.Vision == null || world.Vision.IsUnitVisible(building.OwnerId, enemy))) oilTargets.Add(enemy.Id);
                foreach (int targetId in oilTargets)
                    if (world.TryGetUnit(targetId, out var enemy)) ApplyDamage(new CombatTarget(enemy), Math.Max(1, definition.OilDamage - enemy.Armor));
                if (oilTargets.Count > 0) building.OilCooldownTicks = definition.OilCooldownTicks;
            }
        }

        private static SimPoint StepTowards(SimPoint from, SimPoint destination, int budget)
        {
            if (budget == 0) return from;
            int distance = SimPoint.DistanceCeiling(from, destination);
            if (distance <= budget) return destination;
            long dx = (long)destination.X - from.X, dz = (long)destination.Z - from.Z;
            int moveX = (int)(dx * budget / distance), moveZ = (int)(dz * budget / distance);
            if (moveX == 0 && moveZ == 0)
            {
                if (Math.Abs(dx) >= Math.Abs(dz)) moveX = Math.Sign(dx);
                else moveZ = Math.Sign(dz);
            }
            return new SimPoint(from.X + moveX, from.Z + moveZ);
        }

        private bool TryTarget(int id, out CombatTarget target)
        {
            if (world.TryGetUnit(id, out var unit)) { target = new CombatTarget(unit); return true; }
            if (world.TryGetBuilding(id, out var building)) { target = new CombatTarget(building, world.cellSize); return true; }
            target = default;
            return false;
        }

        private static int DamageFor(AttackDefinition attack, int effectiveDamage, CombatTags tags, int armor)
        {
            int multiplier = 0;
            foreach (var bonus in attack.Bonuses ?? Array.Empty<DamageBonus>())
                if ((bonus.TargetTags & tags) != 0) multiplier = Math.Max(multiplier, bonus.MultiplierPermille);
            if (multiplier == 0) multiplier = 1000;
            long scaled = (long)effectiveDamage * multiplier / 1000;
            return (int)Math.Max(1, Math.Min(int.MaxValue, scaled - armor));
        }

        private void ApplyDamage(CombatTarget target, int damage)
        {
            if (target.Unit != null)
            {
                target.Unit.LastDamageTick = world.TickIndex;
                target.Unit.Health = Math.Max(0, target.Unit.Health - damage);
                if (target.Unit.Health == 0) world.DestroyUnit(target.Unit);
            }
            else
            {
                target.Building.Health = Math.Max(0, target.Building.Health - damage);
                if (target.Building.Health == 0) world.DestroyBuilding(target.Building);
            }
        }
    }
}
