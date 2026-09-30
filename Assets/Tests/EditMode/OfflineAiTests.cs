using System;
using System.Collections.Generic;
using Emberfield.Simulation;
using NUnit.Framework;

namespace Emberfield.Tests.EditMode
{
    public sealed class OfflineAiTests
    {
        [Test]
        public void RequiresAnExistingPlayerAndAnOptInVisionMatch()
        {
            var map = OfflineAiTestWorldFactory.Map(); map.OfflineMatch = null;
            Assert.Throws<ArgumentException>(() => new OfflineAi(new World(OfflineAiTestWorldFactory.Definitions(), map), 1));
            Assert.Throws<ArgumentException>(() => new OfflineAi(OfflineAiTestWorldFactory.World(), 3));
        }

        [Test]
        public void RepeatedCallsAtSameTickThinkOnceAndRespectOrderBudget()
        {
            var world = OfflineAiTestWorldFactory.World(); var ai = new OfflineAi(world, 1);
            for (int i = 0; i < 100; i++) ai.Tick();
            Assert.That(ai.Statistics.Thinks, Is.EqualTo(1));
            Assert.That(ai.Statistics.Commands, Is.InRange(1, OfflineAi.MaximumCommandsPerThink));
            for (int i = 0; i < OfflineAi.ThinkIntervalTicks - 1; i++) { world.Tick(); ai.Tick(); }
            Assert.That(ai.Statistics.Thinks, Is.EqualTo(1));
            world.Tick(); ai.Tick();
            Assert.That(ai.Statistics.Thinks, Is.EqualTo(2));
        }

        [Test]
        public void PlanningNeverGrantsResourcesAndRecordsExactAcceptedPurchaseCost()
        {
            var world = OfflineAiTestWorldFactory.World(); world.TryGetPlayer(1, out var player);
            var before = player.Resources; var ai = new OfflineAi(world, 1); ai.Tick(); var after = player.Resources;
            Assert.That(after.Food, Is.LessThanOrEqualTo(before.Food)); Assert.That(after.Wood, Is.LessThanOrEqualTo(before.Wood));
            Assert.That(after.Metal, Is.LessThanOrEqualTo(before.Metal)); Assert.That(after.Stone, Is.LessThanOrEqualTo(before.Stone));
            Assert.That(ai.Statistics.Spent, Is.EqualTo(new ResourceAmount(before.Food - after.Food, before.Wood - after.Wood, before.Metal - after.Metal, before.Stone - after.Stone)));
            Assert.That(ai.Statistics.UnitsQueued, Is.GreaterThan(0));
        }

        [Test]
        public void EmptyStockIsFundedByPhysicalHarvestAndDepositBeforePurchases()
        {
            var definitions = OfflineAiTestWorldFactory.Definitions(); definitions.StartingResources = default;
            var world = new World(definitions, OfflineAiTestWorldFactory.Map()); var ai = new OfflineAi(world, 1);
            world.TryGetPlayer(1, out var player); world.TryGetResource(200, out var food); int initial = food.RemainingAmount;
            ai.Tick();
            Assert.That(player.Resources, Is.EqualTo(default(ResourceAmount)));
            Assert.That(ai.Statistics.UnitsQueued, Is.Zero);
            for (int i = 0; i < 700; i++) { world.Tick(); ai.Tick(); }
            Assert.That(food.RemainingAmount, Is.LessThan(initial));
            Assert.That(ai.Statistics.GatherOrders, Is.GreaterThan(0));
            Assert.That(ai.Statistics.Spent.Food + player.Resources.Food, Is.GreaterThan(0));
            Assert.That(ai.Statistics.UnitsQueued, Is.GreaterThan(0));
            Assert.That(ai.Statistics.Spent.Food + player.Resources.Food, Is.LessThanOrEqualTo(initial - food.RemainingAmount));
        }

        [Test]
        public void HiddenEnemyAndResourceRelocationDoesNotChangeDecisions()
        {
            var definitions = OfflineAiTestWorldFactory.Definitions(); definitions.StartingResources = default;
            var firstMap = OfflineAiTestWorldFactory.Map(); var secondMap = OfflineAiTestWorldFactory.Map();
            secondMap.UnitSpawns[4].Position = new SimPoint(45500, 41500);
            secondMap.ResourceSpawns[4].Position = new SimPoint(59500, 41500);
            var first = new World(definitions, firstMap); var second = new World(OfflineAiTestWorldFactory.WithStock(default), secondMap);
            var a = new OfflineAi(first, 1); var b = new OfflineAi(second, 1);
            var ordersA = new List<string>(); var ordersB = new List<string>();
            a.CommandIssued += (command, result) => ordersA.Add(Describe(command) + ":" + result.Reason);
            b.CommandIssued += (command, result) => ordersB.Add(Describe(command) + ":" + result.Reason);
            for (int i = 0; i < 20; i++) { a.Tick(); b.Tick(); first.Tick(); second.Tick(); }
            CollectionAssert.AreEqual(ordersA, ordersB);
            Assert.That(a.Observation.KnownEnemies.Count, Is.Zero);
            var observedIds = new List<int>(); foreach (var node in a.Observation.KnownResources) observedIds.Add(node.Id);
            CollectionAssert.AreEqual(new[] { 200, 201 }, observedIds);
        }

        [Test]
        public void LastSeenEnemyMemoryDoesNotFollowAHiddenMovingUnit()
        {
            var map = OfflineAiTestWorldFactory.Map();
            map.UnitSpawns[0].Position = new SimPoint(24500, 22500); map.UnitSpawns[4].Position = new SimPoint(27500, 24500);
            var world = new World(OfflineAiTestWorldFactory.Definitions(), map); var ai = new OfflineAi(world, 1);
            Assert.That(ai.Observation.KnownEnemies.Count, Is.EqualTo(1));
            var known = ai.Observation.KnownEnemies[0]; var last = known.Position;
            Assert.That(world.Submit(new MoveCommand(1, new[] { 1 }, new SimPoint(14500, 8500))).Accepted, Is.True);
            Assert.That(world.Submit(new MoveCommand(2, new[] { 11 }, new SimPoint(45500, 32500))).Accepted, Is.True);
            for (int i = 0; i < 240; i++) world.Tick();
            Assert.That(world.Vision.IsEntityVisible(1, 11), Is.False);
            Assert.That(world.Vision.IsVisible(1, last), Is.False);
            ai.Tick();
            Assert.That(known.Visible, Is.False); Assert.That(known.Position, Is.EqualTo(last)); Assert.That(known.LastSeenTick, Is.Zero);
        }

        [Test]
        public void EveryIssuedOrderBelongsToItsPlayerAndOnlyUsesOwnedActors()
        {
            var world = OfflineAiTestWorldFactory.World(); var ai = new OfflineAi(world, 1);
            int checkedOrders = 0;
            ai.CommandIssued += (command, result) =>
            {
                Assert.That(command.PlayerId, Is.EqualTo(1)); checkedOrders++;
                foreach (int id in Actors(command))
                {
                    if (world.TryGetUnit(id, out var unit)) Assert.That(unit.OwnerId, Is.EqualTo(1));
                    else { Assert.That(world.TryGetBuilding(id, out var building), Is.True); Assert.That(building.OwnerId, Is.EqualTo(1)); }
                }
                if (command is AttackCommand attack) Assert.That(world.Vision.IsEntityVisible(1, attack.TargetEntityId), Is.True);
                if (command is GatherCommand gather) Assert.That(world.Vision.IsEntityVisible(1, gather.ResourceId), Is.True);
            };
            for (int i = 0; i < 1500; i++) { ai.Tick(); world.Tick(); }
            Assert.That(checkedOrders, Is.GreaterThan(10));
            Assert.That(ai.Statistics.BuildingsStarted, Is.GreaterThan(0));
            Assert.That(ai.Statistics.UnitsQueued, Is.GreaterThan(0));
        }

        [Test]
        public void CompletedMatchStopsAiWithoutAdditionalCommands()
        {
            var world = OfflineAiTestWorldFactory.World(); var ai = new OfflineAi(world, 1); ai.Tick();
            Assert.That(world.Submit(new SurrenderCommand(2)).Accepted, Is.True);
            int commands = ai.Statistics.Commands;
            for (int i = 0; i < 100; i++) { ai.Tick(); world.Tick(); }
            Assert.That(ai.Statistics.Commands, Is.EqualTo(commands));
        }

        [Test]
        public void EstablishedArmyStartsAffordableResearchThroughProducer()
        {
            var definitions = OfflineAiTestWorldFactory.WithStock(new ResourceAmount(500, 500, 500, 500));
            definitions.Technologies = new[] { new TechnologyDefinition { Id = "work", ResearchBuildingId = "hearth", ResearchTicks = 3,
                Cost = new ResourceAmount(5, 0, 0, 0), Effects = new[] { new TechnologyEffect { Kind = TechnologyEffectKind.GatherAmount, TargetTags = CombatTags.Worker, Amount = 1 } } } };
            var map = OfflineAiTestWorldFactory.WithArmy(4);
            var world = new World(definitions, map); var ai = new OfflineAi(world, 1);
            for (int i = 0; i < 100; i++) { ai.Tick(); world.Tick(); }
            world.TryGetPlayer(1, out var player);
            Assert.That(ai.Statistics.ResearchStarted, Is.EqualTo(1)); Assert.That(player.HasTechnology("work"), Is.True);
        }

        [Test]
        public void PlannedResearchReservesFoodInsteadOfRecruitingEveryDepositAway()
        {
            var definitions = OfflineAiTestWorldFactory.WithStock(new ResourceAmount(15, 100, 0, 40));
            definitions.Technologies = new[] { new TechnologyDefinition { Id = "work", ResearchBuildingId = "hearth", ResearchTicks = 3,
                Cost = new ResourceAmount(50, 0, 0, 0), Effects = new[] { new TechnologyEffect { Kind = TechnologyEffectKind.GatherAmount, TargetTags = CombatTags.Worker, Amount = 1 } } } };
            var map = OfflineAiTestWorldFactory.WithArmy(6);
            var sites = new List<BuildingSpawnDefinition>(map.BuildingSpawns) { new BuildingSpawnDefinition { Id = 102, OwnerId = 1, DefinitionId = "muster_hall", Position = new SimPoint(18500, 13500) } };
            map.BuildingSpawns = sites.ToArray();
            var world = new World(definitions, map); var ai = new OfflineAi(world, 1); world.TryGetPlayer(1, out var player);
            ai.Tick();
            Assert.That(ai.Statistics.UnitsQueued, Is.Zero);
            Assert.That(player.Resources.Food, Is.EqualTo(15));
            for (int i = 0; i < 600; i++) { world.Tick(); ai.Tick(); }
            Assert.That(player.HasTechnology("work"), Is.True);
            Assert.That(ai.Statistics.ResearchStarted, Is.EqualTo(1));
            Assert.That(ai.Statistics.GatherOrders, Is.GreaterThan(0));
        }

        [Test]
        public void DominionCapturesAndHoldsBeaconsInsteadOfAttackingAVisibleHearth()
        {
            var map = OfflineAiTestWorldFactory.WithArmy(6);
            map.OfflineMatch.Mode = VictoryMode.Dominion; map.OfflineMatch.CaptureTicks = 10; map.OfflineMatch.ContinuousHoldTicks = 30;
            map.OfflineMatch.Objectives = new[]
            {
                new DominionObjectiveDefinition { Id = "near", Position = new SimPoint(24500, 22500), RadiusMillimetres = 4000 },
                new DominionObjectiveDefinition { Id = "middle", Position = new SimPoint(32500, 32500), RadiusMillimetres = 4000 },
                new DominionObjectiveDefinition { Id = "far", Position = new SimPoint(50500, 12500), RadiusMillimetres = 4000 }
            };
            map.BuildingSpawns[1].Position = new SimPoint(38500, 15500);
            for (int i = 5; i < map.UnitSpawns.Length; i++) map.UnitSpawns[i].Position = new SimPoint(40500 + (i - 5) * 1000, 12500);
            var world = new World(OfflineAiTestWorldFactory.WithStock(default), map); var ai = new OfflineAi(world, 1);
            Assert.That(world.Vision.IsEntityVisible(1, 101), Is.True);
            ai.CommandIssued += (command, result) => { if (command is AttackCommand attack) Assert.That(attack.TargetEntityId, Is.Not.EqualTo(101)); };
            for (int i = 0; i < 1000 && !world.Match.IsFinished; i++) { ai.Tick(); world.Tick(); }
            Assert.That(world.Match.IsFinished, Is.True);
            Assert.That(world.Match.Reason, Is.EqualTo(MatchEndReason.Dominion));
            Assert.That(world.Match.WinnerId, Is.EqualTo(1));
            Assert.That(world.TryGetBuilding(101, out _), Is.True);
        }

        private static IEnumerable<int> Actors(IGameCommand command)
        {
            if (command is MoveCommand move) return move.UnitIds;
            if (command is StopCommand stop) return stop.UnitIds;
            if (command is AttackCommand attack) return attack.UnitIds;
            if (command is GatherCommand gather) return gather.WorkerIds;
            if (command is ReturnCargoCommand cargo) return cargo.WorkerIds;
            if (command is BuildCommand build) return build.WorkerIds;
            if (command is ConstructCommand construct) return construct.WorkerIds;
            if (command is TrainCommand train) return new[] { train.BuildingId };
            if (command is ResearchCommand research) return new[] { research.BuildingId };
            if (command is SetCharterCommand charter) return new[] { charter.BuildingId };
            if (command is RepositionCommand reposition) return reposition.UnitIds;
            throw new AssertionException("Unreviewed AI command type: " + command.GetType().Name);
        }
        private static string Describe(IGameCommand command)
        {
            string text = command.GetType().Name + ":" + command.PlayerId + ":" + string.Join(",", Actors(command));
            if (command is MoveCommand move) text += ":" + move.Destination.X + ":" + move.Destination.Z;
            if (command is GatherCommand gather) text += ":" + gather.ResourceId;
            if (command is BuildCommand build) text += ":" + build.BuildingDefinitionId + ":" + build.Position;
            return text;
        }
    }
}
