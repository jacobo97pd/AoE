using System;
using Emberfield.Simulation;
using NUnit.Framework;
using static Emberfield.Tests.EditMode.OfflineMatchTestWorldFactory;

namespace Emberfield.Tests.EditMode
{
    public sealed class OfflineMatchTests
    {
        [Test]
        public void LegacyDrillHasNoVisionOrResultAndDoesNotAcceptSurrender()
        {
            var map = Map(); map.OfflineMatch = null;
            var world = Create(map: map);
            Assert.That(world.Match, Is.Null); Assert.That(world.Vision, Is.Null);
            Assert.That(world.Submit(new SurrenderCommand(1)).Reason, Is.EqualTo(CommandRejection.MatchNotRunning));
            Tick(world, 5); Assert.That(world.TickIndex, Is.EqualTo(5));
        }

        [Test]
        public void InitialVisionIsPlayerLocalAndUnexploredCellsRevealNoEntity()
        {
            var world = Create();
            Assert.That(world.Vision.IsEntityVisible(1, 1), Is.True);
            Assert.That(world.Vision.IsEntityVisible(2, 4), Is.True);
            Assert.That(world.Vision.IsEntityVisible(1, 4), Is.False);
            Assert.That(world.Vision.IsEntityVisible(1, 101), Is.False);
            Assert.That(world.Vision.IsExplored(1, Unit(world, 4).Position), Is.False);
            Assert.That(world.Vision.IsEntityVisible(1, 201), Is.True);
            Assert.That(world.Vision.IsEntityVisible(1, 202), Is.False);
            Assert.That(world.Vision.IsVisible(99, Unit(world, 1).Position), Is.False);
            Assert.That(world.Vision.IsVisible(1, new SimPoint(-1, 0)), Is.False);
            Assert.That(world.Vision.IsExplored(1, new SimPoint(40000, 0)), Is.False);
            Assert.That(world.Vision.IsEntityVisible(1, 999999), Is.False);
        }

        [Test]
        public void ScoutingRevealsThenLeavesExploredTerrainWithoutLiveEnemyVisibility()
        {
            var world = Create(); var enemy = Unit(world, 4).Position;
            Accepted(world.Submit(new MoveCommand(1, new[] { 2 }, new SimPoint(16500, 12500))));
            Until(world, () => world.Vision.IsEntityVisible(1, 4));
            Assert.That(world.Vision.IsExplored(1, enemy), Is.True);
            Accepted(world.Submit(new MoveCommand(1, new[] { 2 }, new SimPoint(6500, 12500))));
            Until(world, () => Unit(world, 2).Order == UnitOrder.Idle);
            Assert.That(world.Vision.IsEntityVisible(1, 4), Is.False);
            Assert.That(world.Vision.IsVisible(1, enemy), Is.False);
            Assert.That(world.Vision.IsExplored(1, enemy), Is.True);
            Assert.That(world.Vision.IsExplored(2, new SimPoint(6500, 12500)), Is.False);
        }

        [Test]
        public void VisionUsesCircularCellsAndDoesNotClaimTerrainOcclusion()
        {
            var map = Map(); map.BlockedCells = new[] { new GridCell(7, 12) };
            var world = Create(map: map);
            Assert.That(world.Vision.IsVisible(1, new SimPoint(8500, 12500)), Is.True, "A wall does not occlude this prototype's vision.");
            Assert.That(world.Vision.IsVisible(1, new SimPoint(11500, 12500)), Is.True, "Scout override reaches five cells.");
            Assert.That(world.Vision.IsVisible(1, new SimPoint(11500, 17500)), Is.False, "A radius is a disc, not its bounding square.");
            long revision = world.Vision.Revision;
            Tick(world, 3); Assert.That(world.Vision.Revision, Is.EqualTo(revision), "Unchanged masks do not dirty the fog view.");
        }

        [Test]
        public void AnyVisibleBuildingFootprintCellRevealsItsEntity()
        {
            var definitions = Definitions(); definitions.Buildings[1].WidthCells = 3;
            var map = Map();
            var buildings = new BuildingSpawnDefinition[3]; Array.Copy(map.BuildingSpawns, buildings, 2);
            buildings[2] = new BuildingSpawnDefinition { Id = 102, OwnerId = 2, DefinitionId = "house", Position = new SimPoint(12500, 12500) };
            map.BuildingSpawns = buildings;
            var world = Create(definitions, map);
            Assert.That(world.Vision.IsVisible(1, buildings[2].Position), Is.False);
            Assert.That(world.Vision.IsEntityVisible(1, 102), Is.True, "The footprint's west cell is inside scout vision.");
        }

        [Test]
        public void HiddenAttackAndResourceCommandsRejectWithoutChangingOrdersOrStock()
        {
            var world = Create();
            Accepted(world.Submit(new MoveCommand(1, new[] { 3 }, new SimPoint(8500, 8500))));
            var destination = Unit(world, 3).Destination; var stock = Player(world).Resources;
            Assert.That(world.Submit(new AttackCommand(1, new[] { 3 }, 4)).Reason, Is.EqualTo(CommandRejection.TargetNotVisible));
            Assert.That(world.Submit(new AttackCommand(1, new[] { 3 }, 999999)).Reason, Is.EqualTo(CommandRejection.TargetNotVisible));
            Assert.That(Unit(world, 3).Destination, Is.EqualTo(destination));
            Assert.That(Unit(world, 3).AttackTargetId, Is.Zero);
            Assert.That(world.Submit(new GatherCommand(1, new[] { 1 }, 202)).Reason, Is.EqualTo(CommandRejection.TargetNotVisible));
            Assert.That(Player(world).Resources, Is.EqualTo(stock));
            Assert.That(Unit(world, 1).WorkerTask, Is.EqualTo(WorkerTask.None));
            Accepted(world.Submit(new GatherCommand(1, new[] { 1 }, 201)));
        }

        [Test]
        public void HiddenPlacementCannotProbeOccupancyOrSpendResources()
        {
            var world = Create(); var before = Player(world).Resources;
            var position = new SimPoint(30500, 8500);
            Assert.That(world.ValidatePlacement(1, new[] { 1 }, "house", position).Reason, Is.EqualTo(CommandRejection.TargetNotVisible));
            Assert.That(world.Submit(new BuildCommand(1, new[] { 1 }, "house", position)).Reason, Is.EqualTo(CommandRejection.TargetNotVisible));
            Assert.That(Player(world).Resources, Is.EqualTo(before)); Assert.That(world.Buildings.Count, Is.EqualTo(2));
            Accepted(world.Submit(new BuildCommand(1, new[] { 1 }, "house", new SimPoint(5500, 8500))));
        }

        [Test]
        public void AutomaticAcquisitionWaitsForVisionAndReleasesTheHiddenTarget()
        {
            var definitions = Definitions(); definitions.Units[2].Attack.RangeMillimetres = 15000;
            var world = Create(definitions);
            Accepted(world.Submit(new MoveCommand(1, new[] { 3 }, Unit(world, 3).Position)));
            Tick(world, 12); Assert.That(Unit(world, 3).AttackTargetId, Is.Zero);
            Assert.That(Unit(world, 4).Health, Is.EqualTo(500));
            Accepted(world.Submit(new MoveCommand(1, new[] { 2 }, new SimPoint(16500, 12500))));
            Until(world, () => Unit(world, 3).AttackTargetId == 4);
            Assert.That(Unit(world, 4).Health, Is.LessThan(500));
            Accepted(world.Submit(new MoveCommand(1, new[] { 2 }, new SimPoint(6500, 12500))));
            Until(world, () => !world.Vision.IsEntityVisible(1, 4));
            Assert.That(Unit(world, 3).AttackTargetId, Is.Zero);
        }

        [Test]
        public void ExplicitChaseStopsAndForgetsIdentityWhenTheTargetLeavesVision()
        {
            var map = Map(); map.UnitSpawns[1].Position = new SimPoint(15500, 12500);
            var world = Create(map: map);
            Accepted(world.Submit(new AttackCommand(1, new[] { 3 }, 4)));
            Accepted(world.Submit(new MoveCommand(2, new[] { 4 }, new SimPoint(32500, 9500))));
            Until(world, () => !world.Vision.IsEntityVisible(1, 4));
            Assert.That(Unit(world, 3).AttackTargetId, Is.Zero);
            Assert.That(Unit(world, 3).Order, Is.EqualTo(UnitOrder.Idle));
            var stopped = Unit(world, 3).Position; Tick(world, 10);
            Assert.That(Unit(world, 3).Position, Is.EqualTo(stopped));
        }

        [Test]
        public void AlreadyLaunchedProjectileKeepsItsDamageContractAfterVisionIsLost()
        {
            var definitions = Definitions(); var map = Map();
            definitions.Units[2].VisionCells = 1;
            definitions.Units[2].Attack = new AttackDefinition { Damage = 50, RangeMillimetres = 5000, AcquireRangeMillimetres = 5000, CooldownTicks = 1000, ProjectileSpeedMillimetresPerSecond = 2000 };
            map.UnitSpawns[2].Position = new SimPoint(17500, 8500);
            var world = Create(definitions, map);
            Accepted(world.Submit(new AttackCommand(1, new[] { 3 }, 4))); world.Tick();
            Assert.That(world.Projectiles.Count, Is.EqualTo(1));
            Accepted(world.Submit(new StopCommand(1, new[] { 3 })));
            Accepted(world.Submit(new MoveCommand(2, new[] { 4 }, new SimPoint(32500, 9500))));
            Until(world, () => !world.Vision.IsEntityVisible(1, 4));
            Assert.That(world.Projectiles.Count, Is.EqualTo(1));
            Until(world, () => world.Projectiles.Count == 0);
            Assert.That(Unit(world, 4).Health, Is.EqualTo(450));
            Assert.That(Unit(world, 3).AttackTargetId, Is.Zero);
        }

        [Test]
        public void ProjectileKilledScoutCannotGrantVisionToLaterAttackDecisions()
        {
            var definitions = Definitions(); var map = Map();
            map.UnitSpawns[1].Position = new SimPoint(15500, 12500);
            definitions.Units[2].Attack.RangeMillimetres = 15000;
            definitions.Units[3].VisionCells = 10;
            definitions.Units[3].Attack = new AttackDefinition { Damage = 200, RangeMillimetres = 15000, AcquireRangeMillimetres = 15000, CooldownTicks = 1000, ProjectileSpeedMillimetresPerSecond = 1000000 };
            var world = Create(definitions, map);
            Accepted(world.Submit(new AttackCommand(2, new[] { 4 }, 2))); world.Tick();
            Accepted(world.Submit(new MoveCommand(1, new[] { 3 }, Unit(world, 3).Position)));
            world.Tick();
            Assert.That(world.TryGetUnit(2, out _), Is.False);
            Assert.That(world.Vision.IsEntityVisible(1, 4), Is.False);
            Assert.That(Unit(world, 3).AttackTargetId, Is.Zero);
            Assert.That(Unit(world, 4).Health, Is.EqualTo(500));
        }

        [TestCase(1, 2)]
        [TestCase(2, 1)]
        public void SurrenderIsImmediateAndFreezesCommandsTicksAndTimers(int loser, int winner)
        {
            var world = Create(); Tick(world, 2);
            Accepted(world.Submit(new TrainCommand(1, 100, "guard")));
            int remaining = Building(world, 100).ProductionQueue[0].RemainingTicks;
            var stock = Player(world).Resources; var position = Unit(world, 1).Position;
            Accepted(world.Submit(new SurrenderCommand(loser)));
            Assert.That(world.Match.IsFinished, Is.True); Assert.That(world.Match.WinnerId, Is.EqualTo(winner));
            Assert.That(world.Match.Reason, Is.EqualTo(MatchEndReason.Surrender));
            Assert.That(world.Match.ElapsedTicks, Is.EqualTo(2));
            Assert.That(world.Submit(new MoveCommand(1, new[] { 1 }, new SimPoint(5500, 8500))).Reason, Is.EqualTo(CommandRejection.MatchFinished));
            Assert.That(world.Submit(new SurrenderCommand(winner)).Reason, Is.EqualTo(CommandRejection.MatchFinished));
            Tick(world, 50);
            Assert.That(world.TickIndex, Is.EqualTo(2)); Assert.That(world.Match.ElapsedTicks, Is.EqualTo(2));
            Assert.That(Building(world, 100).ProductionQueue[0].RemainingTicks, Is.EqualTo(remaining));
            Assert.That(Player(world).Resources, Is.EqualTo(stock)); Assert.That(Unit(world, 1).Position, Is.EqualTo(position));
        }

        [Test]
        public void UnknownPlayerCannotSurrenderOrFinishTheMatch()
        {
            var world = Create();
            Assert.That(world.Submit(new SurrenderCommand(3)).Reason, Is.EqualTo(CommandRejection.InvalidPlayer));
            Assert.That(world.Match.IsFinished, Is.False); Tick(world, 3);
            Assert.That(world.Match.ElapsedTicks, Is.EqualTo(3));
        }

        [Test]
        public void ConquestEndsAfterTheFinalCentralBuildingAndFreezesAtThatTick()
        {
            var world = LethalBattle();
            Accepted(world.Submit(new AttackCommand(2, new[] { 4 }, 100))); world.Tick();
            Assert.That(world.Match.IsFinished, Is.True); Assert.That(world.Match.WinnerId, Is.EqualTo(2));
            Assert.That(world.Match.Reason, Is.EqualTo(MatchEndReason.Conquest)); Assert.That(world.Match.ElapsedTicks, Is.EqualTo(1));
            Assert.That(world.Vision.IsExplored(1, new SimPoint(4500, 4500)), Is.True);
            Assert.That(world.Vision.IsVisible(1, new SimPoint(4500, 4500)), Is.False);
            Tick(world, 20); Assert.That(world.TickIndex, Is.EqualTo(1));
            Assert.That(world.Submit(new BuildCommand(1, new[] { 1 }, "hearth", new SimPoint(5500, 8500))).Reason, Is.EqualTo(CommandRejection.MatchFinished));
        }

        [Test]
        public void AnUnfinishedReplacementCentralBuildingDoesNotPreventElimination()
        {
            var world = LethalBattle();
            var build = world.Submit(new BuildCommand(1, new[] { 1 }, "hearth", new SimPoint(5500, 8500))); Accepted(build);
            Assert.That(Building(world, build.EntityId).IsComplete, Is.False);
            Accepted(world.Submit(new AttackCommand(2, new[] { 4 }, 100))); world.Tick();
            Assert.That(world.Match.IsFinished, Is.True, "A foundation cannot keep a defeated participant alive.");
            Assert.That(world.Match.WinnerId, Is.EqualTo(2)); Assert.That(world.Match.Reason, Is.EqualTo(MatchEndReason.Conquest));
        }

        [Test]
        public void ACompletedReplacementCentralBuildingPreventsElimination()
        {
            var definitions = Definitions(); definitions.Buildings[0].BuildTicks = 5;
            definitions.Units[3].Attack = new AttackDefinition { Damage = 200, RangeMillimetres = 50000, AcquireRangeMillimetres = 50000 };
            definitions.Units[3].VisionCells = 128;
            var world = Create(definitions);
            var build = world.Submit(new BuildCommand(1, new[] { 1 }, "hearth", new SimPoint(5500, 8500))); Accepted(build);
            Until(world, () => Building(world, build.EntityId).IsComplete);
            Accepted(world.Submit(new AttackCommand(2, new[] { 4 }, 100))); world.Tick();
            Assert.That(world.Match.IsFinished, Is.False);
            Accepted(world.Submit(new AttackCommand(2, new[] { 4 }, build.EntityId)));
            Until(world, () => world.Match.IsFinished);
            Assert.That(world.Match.WinnerId, Is.EqualTo(2));
        }

        [TestCase(CombatTags.Creature | CombatTags.Heavy, true)]
        [TestCase(CombatTags.Siege | CombatTags.Heavy, false)]
        public void CreatureArmiesCaptureObjectivesButSiegeEquipmentDoesNot(CombatTags tags, bool captures)
        {
            var definitions = Definitions(); definitions.Units[2].Tags = tags;
            var world = Create(definitions, DominionMap(5)); var objective = world.Match.Objectives[0];
            Tick(world, 3);
            Assert.That(objective.OwnerId, Is.EqualTo(captures ? 1 : 0));
        }

        [Test]
        public void SimultaneousCentralLossIsADrawAfterBothMeleeHitsResolve()
        {
            var world = LethalBattle(true);
            Accepted(world.Submit(new AttackCommand(1, new[] { 3 }, 101)));
            Accepted(world.Submit(new AttackCommand(2, new[] { 4 }, 100))); world.Tick();
            Assert.That(world.Match.IsFinished, Is.True); Assert.That(world.Match.WinnerId, Is.Zero);
            Assert.That(world.Match.Reason, Is.EqualTo(MatchEndReason.SimultaneousElimination));
            Assert.That(world.DeathCount, Is.EqualTo(2));
            Assert.That(world.Match.ElapsedTicks, Is.EqualTo(1));
        }

        [Test]
        public void MatchConfigurationIsCapturedAndCannotBeChangedByMutatingTheMap()
        {
            var map = DominionMap(5); var world = Create(map: map);
            map.OfflineMatch.Mode = VictoryMode.Conquest; map.OfflineMatch.PlayerIds[0] = 99;
            map.OfflineMatch.CaptureTicks = 1; map.OfflineMatch.ContinuousHoldTicks = 1;
            map.OfflineMatch.Objectives[0].Position = new SimPoint(500, 500);
            Tick(world, 7);
            Assert.That(world.Match.Mode, Is.EqualTo(VictoryMode.Dominion)); Assert.That(world.Match.PlayerIds[0], Is.EqualTo(1));
            Assert.That(world.Match.Objectives[0].Position, Is.EqualTo(new SimPoint(16500, 8500)));
            Assert.That(world.Match.IsFinished, Is.False); world.Tick();
            Assert.That(world.Match.WinnerId, Is.EqualTo(1)); Assert.That(world.Match.ElapsedTicks, Is.EqualTo(8));
        }

        [Test]
        public void DominionCapturesConsecutivelyThenRequiresTheFullContinuousMajorityHold()
        {
            var world = Create(map: DominionMap(5)); var objective = world.Match.Objectives[0];
            Tick(world, 2); Assert.That(objective.OwnerId, Is.Zero); Assert.That(objective.CaptureProgressTicks, Is.EqualTo(2));
            world.Tick(); Assert.That(objective.OwnerId, Is.EqualTo(1)); Assert.That(objective.HoldTicks, Is.Zero);
            Assert.That(world.Match.GetHoldTicks(1), Is.Zero); Assert.That(world.Match.IsFinished, Is.False);
            Tick(world, 4); Assert.That(world.Match.GetHoldTicks(1), Is.EqualTo(4)); Assert.That(world.Match.IsFinished, Is.False);
            world.Tick(); Assert.That(world.Match.WinnerId, Is.EqualTo(1)); Assert.That(world.Match.Reason, Is.EqualTo(MatchEndReason.Dominion));
        }

        [Test]
        public void ContestedObjectiveResetsCaptureAndDoesNotAccumulateHold()
        {
            var map = DominionMap(); map.UnitSpawns[3].Position = new SimPoint(17000, 8500);
            var definitions = Definitions(); definitions.Units[3].Attack.Damage = 1;
            var world = Create(definitions, map); var objective = world.Match.Objectives[0];
            Tick(world, 5); Assert.That(objective.IsContested, Is.True); Assert.That(objective.OwnerId, Is.Zero);
            Assert.That(objective.CaptureProgressTicks, Is.Zero); Assert.That(world.Match.GetHoldTicks(1), Is.Zero);
            Accepted(world.Submit(new MoveCommand(2, new[] { 4 }, new SimPoint(20500, 8500))));
            Until(world, () => !objective.IsContested);
            Assert.That(objective.CaptureProgressTicks, Is.EqualTo(1));
            Tick(world, 2); Assert.That(objective.OwnerId, Is.EqualTo(1));
        }

        [Test]
        public void EmptyCaptureResetsButCapturedEmptyObjectivesRetainOwnershipAndHold()
        {
            var definitions = Definitions(); definitions.Units[2].MoveSpeedMillimetresPerSecond = 100000;
            var world = Create(definitions, DominionMap()); var objective = world.Match.Objectives[0];
            world.Tick(); Assert.That(objective.CaptureProgressTicks, Is.EqualTo(1));
            Accepted(world.Submit(new MoveCommand(1, new[] { 3 }, new SimPoint(18500, 8500))));
            Until(world, () => Unit(world, 3).Order == UnitOrder.Idle);
            Assert.That(objective.OwnerId, Is.Zero); Assert.That(objective.CaptureProgressTicks, Is.Zero);
            Accepted(world.Submit(new MoveCommand(1, new[] { 3 }, objective.Position)));
            Until(world, () => objective.OwnerId == 1);
            Accepted(world.Submit(new MoveCommand(1, new[] { 3 }, new SimPoint(18500, 8500))));
            Until(world, () => Unit(world, 3).Order == UnitOrder.Idle);
            Assert.That(objective.OwnerId, Is.EqualTo(1)); Assert.That(objective.HoldTicks, Is.GreaterThan(0));
            Assert.That(objective.CapturingPlayerId, Is.Zero);
        }

        [Test]
        public void LosingAnOwnedObjectiveToHostilePresenceResetsTheMajorityClockImmediately()
        {
            var definitions = Definitions(); definitions.Units[3].Attack.Damage = 1;
            var world = Create(definitions, DominionMap()); Tick(world, 5);
            Assert.That(world.Match.GetHoldTicks(1), Is.EqualTo(2));
            Accepted(world.Submit(new MoveCommand(2, new[] { 4 }, new SimPoint(17000, 8500))));
            Until(world, () => world.Match.Objectives[0].IsContested);
            Assert.That(world.Match.GetHoldTicks(1), Is.Zero); Assert.That(world.Match.Objectives[0].HoldTicks, Is.Zero);
            Assert.That(world.Match.Objectives[0].OwnerId, Is.EqualTo(1));
        }

        [Test]
        public void WorkersAndUnarmedTaggedUnitsDoNotCaptureObjectives()
        {
            var map = Map(VictoryMode.Dominion);
            map.UnitSpawns[0].Position = map.OfflineMatch.Objectives[0].Position;
            map.UnitSpawns[3].Position = map.OfflineMatch.Objectives[1].Position;
            var world = Create(map: map); Tick(world, 6);
            foreach (var objective in world.Match.Objectives) Assert.That(objective.OwnerId, Is.Zero);
        }

        [Test]
        public void SimultaneousDominionThresholdsResolveAsADraw()
        {
            var definitions = Definitions(); definitions.Units[3].Attack.Damage = 1;
            var map = DominionMap(3); map.OfflineMatch.ObjectivesRequired = 1;
            var world = Create(definitions, map); Tick(world, 6);
            Assert.That(world.Match.IsFinished, Is.True); Assert.That(world.Match.WinnerId, Is.Zero);
            Assert.That(world.Match.Reason, Is.EqualTo(MatchEndReason.SimultaneousDominion));
            Assert.That(world.Match.GetHoldTicks(1), Is.EqualTo(3)); Assert.That(world.Match.GetHoldTicks(2), Is.EqualTo(3));
        }

        [TestCase("mode")]
        [TestCase("missing-player")]
        [TestCase("duplicate-player")]
        [TestCase("extra-owner")]
        [TestCase("unknown-central")]
        [TestCase("missing-central")]
        [TestCase("zero-vision")]
        [TestCase("huge-vision")]
        [TestCase("negative-override")]
        [TestCase("zero-capture")]
        [TestCase("zero-hold")]
        [TestCase("missing-objective")]
        [TestCase("duplicate-objective")]
        [TestCase("outside-objective")]
        [TestCase("blocked-objective")]
        [TestCase("zero-radius")]
        [TestCase("unreachable-threshold")]
        public void InvalidOfflineConfigurationRejectsBeforePlay(string fault)
        {
            var definitions = Definitions(); var map = Map(VictoryMode.Dominion);
            switch (fault)
            {
                case "mode": map.OfflineMatch.Mode = (VictoryMode)7; break;
                case "missing-player": map.OfflineMatch.PlayerIds = new[] { 1 }; break;
                case "duplicate-player": map.OfflineMatch.PlayerIds = new[] { 1, 1 }; break;
                case "extra-owner": map.UnitSpawns[0].OwnerId = 3; break;
                case "unknown-central": map.OfflineMatch.CentralBuildingId = "absent"; break;
                case "missing-central": map.BuildingSpawns = new[] { map.BuildingSpawns[0] }; break;
                case "zero-vision": map.OfflineMatch.VisionUnitCells = 0; break;
                case "huge-vision": map.OfflineMatch.VisionBuildingCells = 129; break;
                case "negative-override": definitions.Units[0].VisionCells = -1; break;
                case "zero-capture": map.OfflineMatch.CaptureTicks = 0; break;
                case "zero-hold": map.OfflineMatch.ContinuousHoldTicks = 0; break;
                case "missing-objective": map.OfflineMatch.Objectives = Array.Empty<DominionObjectiveDefinition>(); break;
                case "duplicate-objective": map.OfflineMatch.Objectives[1].Id = map.OfflineMatch.Objectives[0].Id; break;
                case "outside-objective": map.OfflineMatch.Objectives[0].Position = new SimPoint(45000, 8500); break;
                case "blocked-objective": map.OfflineMatch.Objectives[0].Position = map.ResourceSpawns[0].Position; break;
                case "zero-radius": map.OfflineMatch.Objectives[0].RadiusMillimetres = 0; break;
                case "unreachable-threshold": map.OfflineMatch.ObjectivesRequired = 4; break;
            }
            Assert.Throws<ArgumentException>(() => new World(definitions, map));
        }
    }
}
