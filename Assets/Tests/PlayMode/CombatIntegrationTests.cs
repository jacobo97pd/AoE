using System;
using System.Collections;
using System.Collections.Generic;
using Emberfield.Presentation;
using Emberfield.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Emberfield.Tests.PlayMode
{
    public class CombatIntegrationTests
    {
        private GameObject root;
        private MatchController match;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            root = new GameObject("Combat integration match");
            root.SetActive(false);
            match = root.AddComponent<MatchController>();
            match.MapResourcePath = "Maps/combat_sandbox";
            match.enabled = false;
            root.SetActive(true);
            yield return null;
            Canvas.ForceUpdateCanvases();
            // Keep each scenario isolated from incidental auto-acquisition by other armies.
            foreach (int owner in new[] { 1, 2 })
            {
                var ids = new List<int>();
                foreach (var unit in match.World.Units) if (unit.OwnerId == owner) ids.Add(unit.Id);
                var stopped = match.World.Submit(new StopCommand(owner, ids.ToArray()));
                Assert.IsTrue(stopped.Accepted, stopped.Message);
            }
            match.SyncPresentation(1);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (root != null) Object.Destroy(root);
            yield return null;
        }

        [Test]
        public void AuthoredSandboxTrainsAndPresentsAllThreeMilitaryRoles()
        {
            Assert.IsTrue(match.IsCombatSandbox);
            var roles = new[] { "reedguard", "stringwarden", "strider" };
            var required = new ResourceAmount();
            foreach (string role in roles)
            {
                Assert.AreEqual(2, CountUnits(1, role), "The authored blue army must include each role.");
                Assert.AreEqual(2, CountUnits(2, role), "The authored red army must include each role.");
                var definition = match.Economy.UnitDefinition(role);
                Assert.IsNotNull(definition);
                required.Food += definition.Cost.Food;
                required.Wood += definition.Cost.Wood;
                required.Metal += definition.Cost.Metal;
                required.Stone += definition.Cost.Stone;
            }
            Assert.IsTrue(match.World.TryGetPlayer(1, out var player));
            Assert.IsTrue(match.World.TryGetUnit(1, out var worker));
            foreach (ResourceKind kind in Enum.GetValues(typeof(ResourceKind)))
            {
                int requiredAmount = required.Get(kind);
                if (player.Resources.Get(kind) >= requiredAmount) continue;
                var resource = ClosestResource(kind, worker.Position);
                Assert.IsNotNull(resource, "The sandbox must provide resources required by its training roster.");
                match.Select(new[] { worker.Id });
                match.Tap(ScreenPoint(resource.Position), false);
                AdvanceUntil(() => player.Resources.Get(kind) >= requiredAmount, "Gather the shipped training costs before queueing all roles.");
            }
            match.StopSelected();
            match.Select(new[] { 102 });
            foreach (string role in roles)
            {
                var queued = match.Economy.Train(role);
                Assert.IsTrue(queued.Accepted, role + ": " + queued.Message);
            }
            AdvanceUntil(() => CountUnits(1, "reedguard") == 3 && CountUnits(1, "stringwarden") == 3 && CountUnits(1, "strider") == 3,
                "All three production entries must become actual military units.");
            foreach (var unit in match.World.Units)
                if (unit.OwnerId == 1 && !unit.IsWorker) Assert.IsNotNull(match.View.RootFor(unit.Id));
            Assert.AreEqual(0, player.PopulationReserved);
        }

        [Test]
        public void EnemyTapRoutesAnAttackWithoutReplacingOwnedSelection()
        {
            Assert.IsTrue(match.World.TryGetUnit(10, out var attacker));
            Assert.IsTrue(match.World.TryGetUnit(20, out var target));
            match.Tap(ScreenPoint(attacker.Position), false);
            CollectionAssert.AreEqual(new[] { attacker.Id }, match.Selection);
            match.Tap(ScreenPoint(target.Position), false);
            CollectionAssert.AreEqual(new[] { attacker.Id }, match.Selection);
            Assert.AreEqual(target.Id, attacker.AttackTargetId);
            Assert.IsTrue(attacker.AutoAttackEnabled);
            var start = attacker.Position;
            int healthBefore = target.Health;
            AdvanceUntil(() => target.Health < healthBefore, "The enemy tap must lead to a reachable melee attack.");
            Assert.AreNotEqual(start, attacker.Position, "The attacker must close the authored opening distance.");
        }

        [Test]
        public void RangedProjectileViewTravelsImpactsAndUpdatesHealthBar()
        {
            Assert.IsTrue(match.World.TryGetUnit(12, out var archer));
            Assert.IsTrue(match.World.TryGetUnit(20, out var target));
            match.Select(new[] { archer.Id });
            int healthBefore = target.Health;
            var fill = match.View.RootFor(target.Id).Find("Health bar/Fill");
            Assert.IsNotNull(fill);
            Assert.That(fill.localScale.x, Is.EqualTo(1).Within(.001f));
            match.Tap(ScreenPoint(target.Position), false);
            AdvanceUntil(() => match.World.Projectiles.Count > 0, "A Stringwarden attack must launch a simulated projectile.");
            int projectileId = match.World.Projectiles[0].Id;
            Assert.That(match.View.ProjectileCount, Is.GreaterThan(0));
            var projectileView = match.View.ProjectileRootFor(projectileId);
            Assert.IsNotNull(projectileView);
            Vector3 firstPosition = projectileView.position;
            Assert.AreEqual(healthBefore, target.Health, "Launching a projectile must not apply its damage immediately.");
            match.StopSelected(); // The launched projectile must survive while further shots stop.
            match.World.Tick();
            match.SyncPresentation(1);
            projectileView = match.View.ProjectileRootFor(projectileId);
            Assert.IsNotNull(projectileView, "The authored ranged shot should travel for more than one tick.");
            Assert.That(Vector3.Distance(firstPosition, projectileView.position), Is.GreaterThan(.001f));
            AdvanceUntil(() => target.Health < healthBefore, "The in-flight projectile must apply damage on impact.");
            Assert.That(target.Health, Is.GreaterThan(0), "The representative first hit must leave a readable health bar.");
            Assert.That(fill.localScale.x, Is.EqualTo((float)target.Health / target.MaxHealth).Within(.001f));
            Assert.That(fill.localScale.x, Is.LessThan(1));
            Assert.AreEqual(0, match.View.ProjectileCount);
            Assert.IsNull(match.View.ProjectileRootFor(projectileId), "The impact must release the projectile's active view.");
        }

        [Test]
        public void DeathRemovesSelectedEntityAndItsPresentationView()
        {
            Assert.IsTrue(match.World.TryGetUnit(1, out var worker));
            match.Select(new[] { worker.Id });
            var viewBefore = match.View.RootFor(worker.Id);
            Assert.IsNotNull(viewBefore);
            int deathsBefore = match.World.DeathCount;
            var attack = match.World.Submit(new AttackCommand(2, new[] { 24 }, worker.Id));
            Assert.IsTrue(attack.Accepted, attack.Message);
            AdvanceUntil(() => !match.World.TryGetUnit(worker.Id, out _), "A legal enemy attack must remove the defeated worker from active state.");
            Assert.That(match.World.DeathCount, Is.GreaterThan(deathsBefore));
            Assert.IsNull(match.View.RootFor(worker.Id));
            CollectionAssert.DoesNotContain(match.Selection, worker.Id);
            Assert.AreEqual(match.World.Units.Count + match.World.Buildings.Count + match.World.Resources.Count, match.View.Count);
        }

        [Test]
        public void StopHoldsMeleeFireUntilAPlayerIssuesAnotherAttack()
        {
            Assert.IsTrue(match.World.TryGetUnit(14, out var cavalry));
            Assert.IsTrue(match.World.TryGetUnit(20, out var target));
            match.Select(new[] { cavalry.Id });
            int healthBefore = target.Health;
            match.Tap(ScreenPoint(target.Position), false);
            AdvanceUntil(() => target.Health < healthBefore, "The cavalry must first demonstrate an active melee attack.");
            match.StopSelected();
            Assert.IsFalse(cavalry.AutoAttackEnabled);
            Assert.AreEqual(0, cavalry.AttackTargetId);
            int stoppedHealth = target.Health;
            var stoppedPosition = cavalry.Position;
            int holdTicks = match.Economy.UnitDefinition("strider").Attack.CooldownTicks * 3;
            for (int tick = 0; tick < holdTicks; tick++) match.World.Tick();
            match.SyncPresentation(1);
            Assert.AreEqual(stoppedHealth, target.Health, "A nearby enemy must not override an explicit Stop.");
            Assert.AreEqual(stoppedPosition, cavalry.Position);
            match.Tap(ScreenPoint(target.Position), false);
            Assert.IsTrue(cavalry.AutoAttackEnabled);
            AdvanceUntil(() => target.Health < stoppedHealth, "A fresh attack order must resume combat after Stop.");
        }

        private int CountUnits(int owner, string role)
        {
            int count = 0;
            foreach (var unit in match.World.Units) if (unit.OwnerId == owner && unit.DefinitionId == role) count++;
            return count;
        }

        private ResourceNodeState ClosestResource(ResourceKind kind, SimPoint origin)
        {
            ResourceNodeState best = null;
            long bestDistance = long.MaxValue;
            foreach (var resource in match.World.Resources)
            {
                if (resource.Kind != kind || resource.RemainingAmount == 0) continue;
                long distance = Math.Abs((long)resource.Position.X - origin.X) + Math.Abs((long)resource.Position.Z - origin.Z);
                if (distance >= bestDistance) continue;
                best = resource; bestDistance = distance;
            }
            return best;
        }

        private Vector2 ScreenPoint(SimPoint position)
            => match.Rig.Camera.WorldToScreenPoint(DefinitionLoader.ToWorld(position) + Vector3.up * .6f);

        private void AdvanceUntil(Func<bool> condition, string failure)
        {
            const int maximumTicks = World.TickRate * 300;
            for (int tick = 0; tick < maximumTicks && !condition(); tick++)
            {
                match.World.Tick();
                match.SyncPresentation(1);
            }
            Assert.IsTrue(condition(), failure);
        }
    }
}
