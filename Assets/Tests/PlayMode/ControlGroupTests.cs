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
    public sealed class ControlGroupTests
    {
        private GameObject root;
        private MatchController match;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            root = new GameObject("Control group match"); root.SetActive(false);
            match = root.AddComponent<MatchController>();
            match.MapResourcePath = "Maps/combat_sandbox";
            match.enabled = false; root.SetActive(true);
            yield return null;
            match.SyncPresentation(1);
        }
        [UnityTearDown] public IEnumerator TearDown() { if (root != null) Object.Destroy(root); yield return null; }

        private List<int> Mine(int count)
        {
            var ids = new List<int>();
            foreach (var unit in match.World.Units) if (unit.OwnerId == MatchController.LocalPlayer && ids.Count < count) ids.Add(unit.Id);
            Assert.AreEqual(count, ids.Count, "The sandbox must give the local player enough units to group.");
            return ids;
        }
        private void AdvanceUntil(Func<bool> condition, string failure)
        {
            for (int tick = 0; tick < World.TickRate * 300 && !condition(); tick++) { match.World.Tick(); match.SyncPresentation(1); }
            Assert.IsTrue(condition(), failure);
        }

        [Test]
        public void AGroupRemembersASelectionAndBringsItBackWithoutTouchingTheRest()
        {
            var mine = Mine(3);
            match.Select(mine);
            match.Groups.Assign(4);
            Assert.AreEqual(3, match.Groups.Size(4));
            match.ClearSelection();
            Assert.IsEmpty(match.Selection);
            Assert.IsTrue(match.Groups.Recall(4));
            CollectionAssert.AreEquivalent(mine, match.Selection);
            Assert.AreEqual(0, match.Groups.Size(5), "Recalling one slot must not disturb the others.");
        }

        [Test]
        public void AddingKeepsWhatYouHadAndAnEmptySelectionClearsTheSlot()
        {
            var mine = Mine(3);
            match.Select(new[] { mine[0] }); match.Groups.Assign(1);
            match.Select(new[] { mine[1] }); match.Groups.Assign(2);
            Assert.IsTrue(match.Groups.Recall(1));
            Assert.IsTrue(match.Groups.Recall(2, true));
            CollectionAssert.AreEquivalent(new[] { mine[0], mine[1] }, match.Selection);
            match.ClearSelection(); match.Groups.Assign(1);
            Assert.AreEqual(0, match.Groups.Size(1));
            Assert.IsFalse(match.Groups.Recall(1), "An empty group must report that it has nothing to give.");
        }

        [Test]
        public void DeadMembersLeaveTheGroupAndTheSurvivorsStillAnswer()
        {
            var mine = Mine(3);
            match.Select(mine); match.Groups.Assign(7);
            int doomed = mine[0];
            var enemies = new List<int>();
            foreach (var unit in match.World.Units) if (unit.OwnerId == 2 && unit.AttackDamage > 0) enemies.Add(unit.Id);
            CollectionAssert.IsNotEmpty(enemies, "The sandbox must field an enemy able to make the point.");
            var attack = match.World.Submit(new AttackCommand(2, enemies.ToArray(), doomed));
            Assert.IsTrue(attack.Accepted, attack.Message);
            AdvanceUntil(() => !match.World.TryGetUnit(doomed, out _), "A legal enemy attack must remove the defeated unit.");
            Assert.AreEqual(2, match.Groups.Size(7), "A unit that dies leaves its group behind it.");
            Assert.IsTrue(match.Groups.Recall(7));
            CollectionAssert.AreEquivalent(new[] { mine[1], mine[2] }, match.Selection);
            CollectionAssert.DoesNotContain(match.Selection, doomed);
        }

        [Test]
        public void PressingTheGroupAgainMovesTheCameraAndLeavesTheSelectionAlone()
        {
            var mine = Mine(2);
            match.Select(mine); match.Groups.Assign(3);
            Assert.IsTrue(match.World.TryGetUnit(mine[0], out var first));
            var home = DefinitionLoader.ToWorld(first.Position);
            var map = match.World.Map;
            // Park the camera at the far corner of the map so the jump has somewhere to come back from.
            match.Rig.Focus(new Vector3(map.WidthCells * map.CellSizeMillimetres / 1000f, 0, map.HeightCells * map.CellSizeMillimetres / 1000f));
            // The camera sits back and above what it looks at, so the test asks what is on screen rather
            // than where the camera stands.
            var parked = match.Rig.Camera.WorldToViewportPoint(home);
            Assert.IsFalse(parked.z > 0 && parked.x > .25f && parked.x < .75f && parked.y > .25f && parked.y < .75f,
                "The parked camera must not already be looking at the group.");
            Assert.IsTrue(match.Groups.Jump(3));
            var shown = match.Rig.Camera.WorldToViewportPoint(home);
            Assert.That(shown.z, Is.GreaterThan(0), "Jumping to a group must put it in front of the camera.");
            Assert.That(shown.x, Is.InRange(.25f, .75f), "Jumping to a group must bring it to the middle of the screen.");
            Assert.That(shown.y, Is.InRange(.25f, .75f), "Jumping to a group must bring it to the middle of the screen.");
            CollectionAssert.AreEquivalent(mine, match.Selection, "Jumping is a camera move, not a selection change.");
            Assert.IsFalse(match.Groups.Jump(8), "An empty group has nowhere to take the camera.");
        }
    }
}
