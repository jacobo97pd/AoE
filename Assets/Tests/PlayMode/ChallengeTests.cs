using System.Collections;
using Emberfield.Presentation;
using Emberfield.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Emberfield.Tests.PlayMode
{
    public sealed class ChallengeTests
    {
        private GameObject root;

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            ChallengeRun.Queue((string)null);
            if (root != null) Object.Destroy(root);
            yield return null;
        }

        private MatchController Play(string challengeId)
        {
            ChallengeRun.Queue(challengeId);
            root = new GameObject("Challenge " + challengeId); root.SetActive(false);
            var match = root.AddComponent<MatchController>();
            root.SetActive(true); match.enabled = false;
            return match;
        }

        [Test]
        public void EveryChallengeNamesAMapItCanActuallyBeSetOn()
        {
            CollectionAssert.IsNotEmpty(ChallengeCatalog.All);
            foreach (var challenge in ChallengeCatalog.All)
            {
                Assert.IsNotEmpty(challenge.Id, "A challenge needs an identifier.");
                Assert.AreSame(challenge, ChallengeCatalog.Find(challenge.Id), challenge.Id);
                Assert.Greater(challenge.Seconds, 0, challenge.Id);
                Assert.Greater(challenge.Target, 0, challenge.Id);
                Assert.IsNotEmpty(challenge.Brief, challenge.Id);
                var world = DefinitionLoader.CreateWorld(challenge.MapPath);
                Assert.IsNotNull(world, challenge.Id);
                if (challenge.Goal == ChallengeGoal.Build)
                    Assert.IsNotEmpty(challenge.DefinitionId, challenge.Id + " must name the building it asks for.");
            }
            Assert.IsNull(ChallengeCatalog.Find("no_such_exercise"));
        }

        [UnityTest]
        public IEnumerator AQueuedChallengeBuildsItsOwnBattlefieldAndRunsAgainstTheClock()
        {
            var match = Play("first_harvest"); yield return null;
            Assert.IsTrue(match.IsChallenge, "Queueing an exercise must start it.");
            Assert.AreEqual("first_harvest", match.Challenge.Definition.Id);
            Assert.AreEqual("amber_reach", match.World.Map.Id, "The challenge chooses its own map.");
            Assert.IsNull(match.World.Match, "An exercise is not a two-player match and must not inherit its victory rules.");
            Assert.AreEqual(ChallengeState.Running, match.Challenge.State);
            Assert.AreEqual(180, match.Challenge.RemainingSeconds);
            // A queued challenge is consumed, so the next scene is an ordinary one.
            Assert.IsFalse(ChallengeRun.HasPending);
            for (int tick = 0; tick < World.TickRate * 3; tick++) match.AdvanceSimulationTick();
            Assert.AreEqual(177, match.Challenge.RemainingSeconds, "The clock follows simulated ticks, not frames.");
            Assert.AreEqual(ChallengeState.Running, match.Challenge.State);
        }

        [UnityTest]
        public IEnumerator ReachingTheTargetCompletesTheExerciseAndStopsTheClock()
        {
            // The watcher only ever reads the world, so the honest way to reach a target without a long
            // gathering run is to ask for something the ordinary starting stock already satisfies.
            root = new GameObject("Reachable challenge"); root.SetActive(false);
            ChallengeRun.Queue(new ChallengeDefinition
            {
                Id = "reachable", MapPath = "Maps/amber_reach", Goal = ChallengeGoal.Stock,
                Resource = ResourceKind.Food, Target = 1, Seconds = 120, Name = "Reachable", Brief = "Hold any food at all."
            });
            var match = root.AddComponent<MatchController>();
            root.SetActive(true); match.enabled = false;
            yield return null;
            Assert.IsTrue(match.World.TryGetPlayer(MatchController.LocalPlayer, out var player));
            Assert.Greater(player.Resources.Food, 0, "The ordinary start carries food, which this exercise asks for.");
            match.AdvanceSimulationTick();
            Assert.AreEqual(ChallengeState.Won, match.Challenge.State);
            Assert.IsTrue(match.Challenge.IsFinished);
            int frozen = match.Challenge.RemainingSeconds;
            for (int tick = 0; tick < World.TickRate * 5; tick++) match.AdvanceSimulationTick();
            Assert.AreEqual(ChallengeState.Won, match.Challenge.State, "A finished exercise stays finished.");
            Assert.AreEqual(frozen, match.Challenge.RemainingSeconds, "A finished exercise stops its clock.");
        }

        [UnityTest]
        public IEnumerator RunningOutOfTimeFailsTheExercise()
        {
            var match = Play("first_harvest"); yield return null;
            for (int tick = 0; tick <= World.TickRate * 181 && !match.Challenge.IsFinished; tick++) match.AdvanceSimulationTick();
            Assert.AreEqual(ChallengeState.Lost, match.Challenge.State);
            Assert.AreEqual(0, match.Challenge.RemainingSeconds);
            Assert.AreEqual(ChallengeState.Lost, ChallengeRun.LastResult);
            Assert.AreEqual("first_harvest", ChallengeRun.LastPlayedId);
        }

        [UnityTest]
        public IEnumerator TheCombatExerciseIsWonByClearingTheFieldOfEnemies()
        {
            var match = Play("the_counter_triangle"); yield return null;
            Assert.AreEqual("combat_sandbox", match.World.Map.Id);
            Assert.AreEqual(ChallengeState.Running, match.Challenge.State);
            match.AdvanceSimulationTick();
            // The sandbox parks an enemy Tender behind the line. The brief says army, so only the army counts.
            StringAssert.Contains("6 enemies left", match.Challenge.Counter());
            var armed = new System.Collections.Generic.List<int>();
            foreach (var unit in match.World.Units)
                if (unit.OwnerId != MatchController.LocalPlayer && !unit.IsWorker && unit.AttackDamage > 0) armed.Add(unit.Id);
            Assert.AreEqual(6, armed.Count);
            foreach (int enemy in armed)
            {
                // Attackers die as the fight goes on, so the order is given by whoever is still standing.
                var mine = new System.Collections.Generic.List<int>();
                foreach (var unit in match.World.Units)
                    if (unit.OwnerId == MatchController.LocalPlayer && unit.AttackDamage > 0) mine.Add(unit.Id);
                if (mine.Count == 0) break;
                var order = match.World.Submit(new AttackCommand(MatchController.LocalPlayer, mine.ToArray(), enemy));
                Assert.IsTrue(order.Accepted, order.Message);
                for (int tick = 0; tick < World.TickRate * 60 && match.World.TryGetUnit(enemy, out _); tick++) match.AdvanceSimulationTick();
            }
            Assert.AreEqual(ChallengeState.Won, match.Challenge.State, "Clearing the army wins the exercise its brief describes.");
            Assert.AreEqual("Field cleared", match.Challenge.Counter());
            Assert.IsTrue(match.World.TryGetUnit(2, out var survivor) && survivor.IsWorker,
                "The enemy Tender is still standing, which is exactly the point.");
        }

        [UnityTest]
        public IEnumerator TheMenuPausesAnExerciseInsteadOfLettingItsClockRunBehindTheMenu()
        {
            var match = Play("first_harvest"); yield return null;
            match.OfflineControls.Open();
            Assert.IsTrue(match.OfflineControls.IsPaused, "An exercise is played against a clock, so the menu must pause it.");
            match.OfflineControls.Close();
            Assert.IsFalse(match.OfflineControls.IsPaused);
        }

        [UnityTest]
        public IEnumerator RestartingAnExerciseQueuesTheSameExerciseAgain()
        {
            var match = Play("the_counter_triangle"); yield return null;
            Assert.IsFalse(ChallengeRun.HasPending, "Starting it consumed the queue.");
            // The queueing half of Restart, without the scene load: loading a scene inside a test leaves a
            // second controller and its front-door canvas behind for whatever runs next.
            Assert.IsTrue(match.QueueChallengeRestart(), "Restart re-queues the exercise rather than dropping into free practice.");
            Assert.IsTrue(ChallengeRun.HasPending);
        }

        [UnityTest]
        public IEnumerator TheTrainingGroundPageOffersEveryExerciseWithSomethingToPress()
        {
            root = new GameObject("Training ground"); root.SetActive(false);
            var match = root.AddComponent<MatchController>();
            root.SetActive(true); match.enabled = false;
            yield return null;
            Assert.IsNotNull(match.Shell, "The front door owns the training ground.");
            match.Shell.Open();
            match.Shell.Navigate("challenges");
            Assert.AreEqual("challenges", match.Shell.Page);
            var content = match.Shell.Root.GetComponentsInChildren<UnityEngine.UI.Button>(true);
            foreach (var challenge in ChallengeCatalog.All)
            {
                bool offered = false;
                foreach (var button in content) if (button.name == "Play " + challenge.Id) offered = true;
                Assert.IsTrue(offered, challenge.Id + " must be playable from the training ground.");
            }
            Assert.Throws<System.ArgumentException>(() => match.Shell.Navigate("nowhere"));
        }

        [UnityTest]
        public IEnumerator PassingAnExerciseIsRememberedAcrossTheWholeCourse()
        {
            root = new GameObject("Course memory"); root.SetActive(false);
            ChallengeRun.Queue(new ChallengeDefinition
            {
                Id = "first_harvest", MapPath = "Maps/amber_reach", Goal = ChallengeGoal.Stock,
                Resource = ResourceKind.Food, Target = 1, Seconds = 120, Name = "First harvest", Brief = "Hold any food."
            });
            var match = root.AddComponent<MatchController>();
            root.SetActive(true); match.enabled = false;
            yield return null;
            var record = match.Alpha.Settings.Value.ChallengesPassed;
            record.Clear();
            Assert.AreEqual(0, ChallengeRun.PassedCount(match));
            match.AdvanceSimulationTick();
            Assert.AreEqual(ChallengeState.Won, match.Challenge.State);
            Assert.IsTrue(ChallengeRun.WasPassed(match, "first_harvest"), "Passing one is remembered.");
            Assert.AreEqual(1, ChallengeRun.PassedCount(match));
            // Playing a different exercise afterwards must not erase the one already passed.
            ChallengeRun.Queue("the_metal_seam");
            Assert.IsTrue(ChallengeRun.WasPassed(match, "first_harvest"));
            record.Clear();
        }

        [Test]
        public void AHandEditedRecordOfPassedExercisesComesBackBoundedAndDeduplicated()
        {
            var settings = new AlphaSettings();
            settings.ChallengesPassed.Add("first_harvest");
            settings.ChallengesPassed.Add("first_harvest");
            settings.ChallengesPassed.Add("   ");
            settings.ChallengesPassed.Add(new string('x', 200));
            for (int i = 0; i < AlphaSettings.MaximumRecordedChallenges + 20; i++) settings.ChallengesPassed.Add("filler" + i);
            settings.Normalize();
            Assert.LessOrEqual(settings.ChallengesPassed.Count, AlphaSettings.MaximumRecordedChallenges);
            Assert.AreEqual(1, settings.ChallengesPassed.FindAll(id => id == "first_harvest").Count);
            CollectionAssert.DoesNotContain(settings.ChallengesPassed, "   ");
            foreach (string id in settings.ChallengesPassed) Assert.LessOrEqual(id.Length, 64);
        }

        [UnityTest]
        public IEnumerator AnOrdinaryMatchCarriesNoChallengeAndKeepsItsOwnVictoryRules()
        {
            root = new GameObject("Ordinary offline"); root.SetActive(false);
            var match = root.AddComponent<MatchController>();
            match.InitialOfflineFactionId = "aven"; match.InitialOfflineMode = VictoryMode.Conquest;
            root.SetActive(true); match.enabled = false;
            yield return null;
            Assert.IsFalse(match.IsChallenge);
            Assert.IsNull(match.Challenge);
            Assert.IsNotNull(match.World.Match, "A skirmish keeps Conquest and Dominion.");
        }
    }
}
