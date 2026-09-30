using System;
using Emberfield.Simulation;
using NUnit.Framework;
using static Emberfield.Tests.EditMode.TechnologyTestWorldFactory;

namespace Emberfield.Tests.EditMode
{
    public sealed class TechnologyTests
    {
        [Test]
        public void ValidationIsReadOnlyAndResearchChargesOnceCompletingOnExactlyItsThirdTick()
        {
            var world = Create();
            var player = Player(world);
            var before = player.Resources;
            var command = new ResearchCommand(1, 100, "edge");
            Accepted(world.ValidateResearch(command));
            Accepted(world.ValidateResearch(command));
            Assert.That(player.Resources, Is.EqualTo(before));
            Assert.That(Building(world).ActiveResearch, Is.Null);
            Assert.That(player.EraId, Is.EqualTo("camp"));
            Assert.That(player.EraTier, Is.EqualTo(1));

            Accepted(world.Submit(command));
            Assert.That(player.Resources, Is.EqualTo(new ResourceAmount(980, 990, 1000, 1000)));
            var research = Building(world).ActiveResearch;
            Assert.That(research.TechnologyId, Is.EqualTo("edge"));
            Assert.That(research.TotalTicks, Is.EqualTo(3));
            Assert.That(research.RemainingTicks, Is.EqualTo(3));
            Assert.That(player.HasTechnology("edge"), Is.False);
            Tick(world, 2);
            Assert.That(research.RemainingTicks, Is.EqualTo(1));
            Assert.That(Unit(world, 2).AttackDamage, Is.EqualTo(10));
            Assert.That(player.HasTechnology("edge"), Is.False);
            world.Tick();
            Assert.That(Building(world).ActiveResearch, Is.Null);
            Assert.That(player.HasTechnology("edge"), Is.True);
            Assert.That(player.CompletedTechnologyIds, Is.EquivalentTo(new[] { "edge" }));
            Assert.That(Unit(world, 2).AttackDamage, Is.EqualTo(13));
            Tick(world, 10);
            Assert.That(player.Resources, Is.EqualTo(new ResourceAmount(980, 990, 1000, 1000)));
            Assert.That(player.CompletedTechnologyIds.Count, Is.EqualTo(1));
        }

        [TestCase(1, 102, "edge")]
        [TestCase(1, 103, "edge")]
        [TestCase(1, 999, "edge")]
        [TestCase(0, 100, "edge")]
        [TestCase(99, 100, "edge")]
        [TestCase(1, 100, "missing")]
        [TestCase(1, 100, null)]
        public void InvalidResearchIdentityOwnershipAndProducerRejectWithoutMutation(int owner, int building, string technology)
        {
            var world = Create();
            var first = Player(world).Resources;
            var second = Player(world, 2).Resources;
            var command = new ResearchCommand(owner, building, technology);
            Assert.That(world.ValidateResearch(command).Accepted, Is.False);
            Assert.That(world.Submit(command).Accepted, Is.False);
            Assert.That(Player(world).Resources, Is.EqualTo(first));
            Assert.That(Player(world, 2).Resources, Is.EqualTo(second));
            Assert.That(Player(world).CompletedTechnologyIds, Is.Empty);
            foreach (var producer in world.Buildings)
            {
                Assert.That(producer.ActiveResearch, Is.Null);
                Assert.That(producer.ProductionQueue, Is.Empty);
            }
        }

        [Test]
        public void IncompleteProducerCannotResearchAndRejectionDoesNotChangeItsConstruction()
        {
            var world = Create();
            var result = world.Submit(new BuildCommand(1, new[] { 1 }, "hall", new SimPoint(12500, 15500)));
            Accepted(result);
            var site = Building(world, result.EntityId);
            var resources = Player(world).Resources;
            int progress = site.BuildProgressTicks;
            Assert.That(site.IsComplete, Is.False);
            Assert.That(world.Submit(new ResearchCommand(1, site.Id, "edge")).Reason, Is.EqualTo(CommandRejection.BuildingIncomplete));
            Assert.That(site.ActiveResearch, Is.Null);
            Assert.That(site.BuildProgressTicks, Is.EqualTo(progress));
            Assert.That(Player(world).Resources, Is.EqualTo(resources));
            Assert.That(Unit(world).TargetBuildingId, Is.EqualTo(site.Id));
        }

        [Test]
        public void InsufficientFundsRejectResearchWithoutSpendingTheOtherResource()
        {
            var definitions = Definitions();
            definitions.StartingResources = new ResourceAmount(19, 100, 0, 0);
            var world = new World(definitions, Map());
            Assert.That(world.Submit(new ResearchCommand(1, 100, "edge")).Reason, Is.EqualTo(CommandRejection.InsufficientResources));
            Assert.That(Player(world).Resources, Is.EqualTo(definitions.StartingResources));
            Assert.That(Building(world).ActiveResearch, Is.Null);
        }

        [Test]
        public void TrainingAndResearchAreMutuallyExclusiveWithoutLosingReservationsOrSpending()
        {
            var world = Create();
            Accepted(world.Submit(new TrainCommand(1, 100, "striker")));
            var resources = Player(world).Resources;
            Assert.That(world.Submit(new ResearchCommand(1, 100, "edge")).Accepted, Is.False);
            Assert.That(Player(world).Resources, Is.EqualTo(resources));
            Assert.That(Player(world).PopulationReserved, Is.EqualTo(1));
            Assert.That(Building(world).ProductionQueue.Count, Is.EqualTo(1));
            Assert.That(Building(world).ProductionQueue[0].RemainingTicks, Is.EqualTo(4));
            Assert.That(Building(world).ActiveResearch, Is.Null);

            world = Create();
            Accepted(world.Submit(new ResearchCommand(1, 100, "edge")));
            resources = Player(world).Resources;
            Assert.That(world.Submit(new TrainCommand(1, 100, "striker")).Accepted, Is.False);
            Assert.That(world.Submit(new ResearchCommand(1, 100, "plate")).Accepted, Is.False);
            Assert.That(Player(world).Resources, Is.EqualTo(resources));
            Assert.That(Player(world).PopulationReserved, Is.Zero);
            Assert.That(Building(world).ProductionQueue, Is.Empty);
            Assert.That(Building(world).ActiveResearch.TechnologyId, Is.EqualTo("edge"));
            Assert.That(Building(world).ActiveResearch.RemainingTicks, Is.EqualTo(3));
        }

        [Test]
        public void DuplicateResearchAcrossOwnedBuildingsRejectsWithoutResetButOtherPlayerCanResearch()
        {
            var world = Create();
            Accepted(world.Submit(new ResearchCommand(1, 100, "edge")));
            world.Tick();
            var resources = Player(world).Resources;
            Assert.That(world.Submit(new ResearchCommand(1, 100, "edge")).Accepted, Is.False);
            Assert.That(world.Submit(new ResearchCommand(1, 101, "edge")).Accepted, Is.False);
            Assert.That(Building(world).ActiveResearch.RemainingTicks, Is.EqualTo(2));
            Assert.That(Building(world, 101).ActiveResearch, Is.Null);
            Assert.That(Player(world).Resources, Is.EqualTo(resources));
            Accepted(world.Submit(new ResearchCommand(2, 102, "edge")));
            Tick(world, 2);
            Assert.That(Player(world).HasTechnology("edge"), Is.True);
            Assert.That(Player(world, 2).HasTechnology("edge"), Is.False);
            world.Tick();
            Assert.That(Player(world, 2).HasTechnology("edge"), Is.True);
            Assert.That(world.Submit(new ResearchCommand(1, 101, "edge")).Reason, Is.EqualTo(CommandRejection.ResearchAlreadyCompleted));
            Assert.That(Player(world).Resources, Is.EqualTo(resources));
        }

        [Test]
        public void EraChainRequiresPriorResearchAndCompletedInfrastructureWithoutSkippingOrRepeating()
        {
            var world = Create();
            Assert.That(world.Submit(new ResearchCommand(1, 100, "advance-city")).Accepted, Is.False);
            Assert.That(world.Submit(new ResearchCommand(1, 100, "advance-citadel")).Accepted, Is.False);
            Research(world, "advance-village");
            Assert.That(Player(world).EraId, Is.EqualTo("village"));
            Assert.That(Player(world).EraTier, Is.EqualTo(2));
            Assert.That(world.Submit(new ResearchCommand(1, 100, "advance-city")).Accepted, Is.False);
            Research(world, "edge"); // A minimum-era upgrade remains available after advancing.
            var result = world.Submit(new BuildCommand(1, new[] { 1 }, "workshop", new SimPoint(9500, 12500)));
            Accepted(result);
            Assert.That(world.Submit(new ResearchCommand(1, 100, "advance-city")).Accepted, Is.False);
            Until(world, () => Building(world, result.EntityId).IsComplete, "Required workshop completes");
            Research(world, "advance-city");
            Assert.That(Player(world).EraId, Is.EqualTo("city"));
            Assert.That(Player(world).EraTier, Is.EqualTo(3));
            Research(world, "advance-citadel");
            Assert.That(Player(world).EraId, Is.EqualTo("citadel"));
            Assert.That(Player(world).EraTier, Is.EqualTo(4));
            var resources = Player(world).Resources;
            Assert.That(world.Submit(new ResearchCommand(1, 101, "advance-village")).Accepted, Is.False);
            Assert.That(Player(world).Resources, Is.EqualTo(resources));
            Assert.That(Player(world, 2).EraId, Is.EqualTo("camp"));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void BuildAndTrainRequireBothEraAndTechnologyRegardlessOfAcquisitionOrder(bool eraFirst)
        {
            var world = Create();
            var site = new SimPoint(9500, 12500);
            var move = new SimPoint(3500, 4500);
            Accepted(world.Submit(new MoveCommand(1, new[] { 1 }, move)));
            var before = Player(world).Resources;
            Assert.That(world.Submit(new BuildCommand(1, new[] { 1 }, "workshop", site)).Accepted, Is.False);
            Assert.That(world.Submit(new TrainCommand(1, 100, "guard")).Accepted, Is.False);
            Assert.That(Player(world).Resources, Is.EqualTo(before));
            Assert.That(Unit(world).Destination, Is.EqualTo(move), "A locked building must not cancel the worker's order.");
            Research(world, eraFirst ? "advance-village" : "edge");
            Assert.That(world.ValidatePlacement(1, new[] { 1 }, "workshop", site).Accepted, Is.False);
            Assert.That(world.Submit(new TrainCommand(1, 100, "guard")).Accepted, Is.False);
            Research(world, eraFirst ? "edge" : "advance-village");
            Accepted(world.Submit(new TrainCommand(1, 100, "guard")));
            Accepted(world.Submit(new BuildCommand(1, new[] { 1 }, "workshop", site)));
        }

        [Test]
        public void EnemyCompletedPrerequisiteBuildingDoesNotUnlockOwnedResearch()
        {
            var map = Map();
            var buildings = new BuildingSpawnDefinition[map.BuildingSpawns.Length + 1];
            Array.Copy(map.BuildingSpawns, buildings, map.BuildingSpawns.Length);
            buildings[buildings.Length - 1] = CombatTestWorldFactory.BuildingSpawn(104, 2, "workshop", 14500, 18500);
            map.BuildingSpawns = buildings;
            var world = new World(Definitions(), map);
            Assert.That(Building(world, 104).IsComplete, Is.True, "Authored setup bypasses ordinary unlock gates.");
            Research(world, "advance-village");
            var before = Player(world).Resources;
            Assert.That(world.Submit(new ResearchCommand(1, 100, "advance-city")).Accepted, Is.False);
            Assert.That(Player(world).Resources, Is.EqualTo(before));
            Assert.That(Player(world).EraTier, Is.EqualTo(2));
        }

        [TestCase("edge")]
        [TestCase("advance-village")]
        public void DestroyingProducerOnCompletionTickLosesCostAndProgressButAllowsFullRetry(string technology)
        {
            var world = WithHallKiller();
            Accepted(world.Submit(new ResearchCommand(1, 100, technology)));
            Tick(world, 2);
            Assert.That(Building(world).ActiveResearch.RemainingTicks, Is.EqualTo(1));
            DestroyHall(world); // Combat resolves before research on the would-be completion tick.
            Assert.That(Player(world).HasTechnology(technology), Is.False);
            Assert.That(Player(world).EraTier, Is.EqualTo(1));
            Assert.That(Unit(world, 2).AttackDamage, Is.EqualTo(10));
            Assert.That(Player(world).Resources, Is.EqualTo(new ResourceAmount(980, 990, 1000, 1000)));
            Accepted(world.Submit(new ResearchCommand(1, 101, technology)));
            Assert.That(Building(world, 101).ActiveResearch.RemainingTicks, Is.EqualTo(3));
            Assert.That(Player(world).Resources, Is.EqualTo(new ResourceAmount(960, 980, 1000, 1000)));
            Tick(world, 3);
            Assert.That(Player(world).HasTechnology(technology), Is.True);
            Assert.That(Player(world).EraTier, Is.EqualTo(technology == "advance-village" ? 2 : 1));
            Assert.That(Unit(world, 2).AttackDamage, Is.EqualTo(technology == "edge" ? 13 : 10));
        }

        [Test]
        public void CompletedTechnologyPersistsAfterProducerDestructionAndCannotChargeAgain()
        {
            var world = WithHallKiller();
            Research(world, "edge");
            DestroyHall(world);
            var resources = Player(world).Resources;
            Assert.That(Player(world).HasTechnology("edge"), Is.True);
            Assert.That(Unit(world, 2).AttackDamage, Is.EqualTo(13));
            Assert.That(world.Submit(new ResearchCommand(1, 101, "edge")).Reason, Is.EqualTo(CommandRejection.ResearchAlreadyCompleted));
            Assert.That(Player(world).Resources, Is.EqualTo(resources));
        }

        [Test]
        public void EffectsApplyToMatchingOwnedExistingAndNewUnitsWithoutMutatingSharedDefinitions()
        {
            var definitions = Definitions();
            var map = Map();
            map.UnitSpawns[2].DefinitionId = "striker";
            var world = new World(definitions, map);
            Research(world, "edge");
            Research(world, "plate");
            Assert.That(Unit(world, 2).AttackDamage, Is.EqualTo(13));
            Assert.That(Unit(world, 2).Armor, Is.EqualTo(3));
            Assert.That(Unit(world).AttackDamage, Is.Zero, "Infantry upgrades do not arm the worker.");
            Assert.That(Unit(world).Armor, Is.Zero);
            Assert.That(Unit(world, 3).AttackDamage, Is.EqualTo(10));
            Assert.That(Unit(world, 3).Armor, Is.EqualTo(1));
            Assert.That(definitions.Units[0].Attack.Damage, Is.EqualTo(10));
            Assert.That(definitions.Units[0].Armor, Is.EqualTo(1));
            int oldCount = world.Units.Count;
            Accepted(world.Submit(new TrainCommand(1, 100, "striker")));
            Until(world, () => world.Units.Count == oldCount + 1, "Post-upgrade training spawns");
            foreach (var unit in world.Units)
                if (unit.Id > 201)
                {
                    Assert.That(unit.OwnerId, Is.EqualTo(1));
                    Assert.That(unit.AttackDamage, Is.EqualTo(13));
                    Assert.That(unit.Armor, Is.EqualTo(3));
                }
        }

        [Test]
        public void DistinctUpgradesStackOnceAndMultipleMatchingTagsDoNotMultiplyOneEffect()
        {
            var definitions = Definitions();
            Tech(definitions, "edge").Effects[0].TargetTags = CombatTags.Infantry | CombatTags.Light;
            var world = new World(definitions, Map());
            Assert.That(world.Submit(new ResearchCommand(1, 100, "edge2")).Accepted, Is.False);
            Research(world, "edge");
            Assert.That(Unit(world, 2).AttackDamage, Is.EqualTo(13));
            Research(world, "edge2");
            Assert.That(Unit(world, 2).AttackDamage, Is.EqualTo(17));
            Assert.That(world.Submit(new ResearchCommand(1, 101, "edge2")).Accepted, Is.False);
            Tick(world, 15);
            Assert.That(Unit(world, 2).AttackDamage, Is.EqualTo(17));
            Assert.That(Player(world).CompletedTechnologyIds.Count, Is.EqualTo(2));
        }

        [Test]
        public void CombatUsesEffectiveAttackerDamageAndDefenderArmor()
        {
            var map = Map();
            map.UnitSpawns[2] = CombatTestWorldFactory.Spawn(3, 2, "striker", 9500, 4500);
            var world = new World(Definitions(), map);
            Accepted(world.Submit(new StopCommand(1, new[] { 2 })));
            Accepted(world.Submit(new StopCommand(2, new[] { 3 })));
            Research(world, "edge");
            Research(world, "plate", 102, 2);
            Accepted(world.Submit(new AttackCommand(1, new[] { 2 }, 3)));
            world.Tick();
            Assert.That(Unit(world, 3).Health, Is.EqualTo(90), "13 upgraded damage minus 3 upgraded armor.");
            Assert.That(Unit(world, 2).Health, Is.EqualTo(100), "The defender remains on explicit hold fire.");
        }

        [Test]
        public void ResearchFinishesAfterCombatSoNewDamageStartsOnFollowingTick()
        {
            var definitions = Definitions();
            definitions.Units[0].Attack.CooldownTicks = 1;
            var map = Map();
            map.UnitSpawns[2] = CombatTestWorldFactory.Spawn(3, 2, "dummy", 9500, 4500);
            var world = new World(definitions, map);
            Accepted(world.Submit(new StopCommand(1, new[] { 2 })));
            Accepted(world.Submit(new ResearchCommand(1, 100, "edge")));
            Tick(world, 2);
            Accepted(world.Submit(new AttackCommand(1, new[] { 2 }, 3)));
            world.Tick();
            Assert.That(Player(world).HasTechnology("edge"), Is.True);
            Assert.That(Unit(world, 2).AttackDamage, Is.EqualTo(13));
            Assert.That(Unit(world, 3).Health, Is.EqualTo(190), "Attack resolves before the technology completes.");
            world.Tick();
            Assert.That(Unit(world, 3).Health, Is.EqualTo(177), "The next tick uses the completed upgrade.");
        }

        [Test]
        public void UnitSpawnedByProductionOnResearchCompletionTickReceivesCompletedEffects()
        {
            var definitions = Definitions();
            definitions.Units[0].TrainTicks = 3;
            var world = new World(definitions, Map());
            Accepted(world.Submit(new ResearchCommand(1, 100, "edge")));
            Accepted(world.Submit(new TrainCommand(1, 101, "striker")));
            Tick(world, 2);
            Assert.That(world.Units.Count, Is.EqualTo(3));
            world.Tick();
            Assert.That(world.Units.Count, Is.EqualTo(4));
            Assert.That(Player(world).HasTechnology("edge"), Is.True);
            Assert.That(Unit(world, 202).AttackDamage, Is.EqualTo(13), "Research's completion pass includes this tick's newborn.");
            Assert.That(Unit(world, 202).OwnerId, Is.EqualTo(1));
            Assert.That(Player(world).PopulationReserved, Is.Zero);
        }

        [Test]
        public void ResearchCompletingAfterProjectileLaunchDoesNotRewriteFlyingDamage()
        {
            var definitions = Definitions();
            definitions.Units[0].Attack = CombatTestWorldFactory.Attack(10, 5000, 20, 1000);
            var map = Map();
            map.UnitSpawns[2] = CombatTestWorldFactory.Spawn(3, 2, "dummy", 12500, 4500);
            var world = new World(definitions, map);
            Accepted(world.Submit(new StopCommand(1, new[] { 2 })));
            Accepted(world.Submit(new ResearchCommand(1, 100, "edge")));
            Accepted(world.Submit(new AttackCommand(1, new[] { 2 }, 3)));
            world.Tick();
            Assert.That(world.Projectiles.Count, Is.EqualTo(1));
            Accepted(world.Submit(new StopCommand(1, new[] { 2 })));
            Tick(world, 2);
            Assert.That(Player(world).HasTechnology("edge"), Is.True);
            Assert.That(Unit(world, 2).AttackDamage, Is.EqualTo(13));
            Assert.That(world.Projectiles.Count, Is.EqualTo(1));
            Until(world, () => world.Projectiles.Count == 0, "Pre-upgrade projectile impacts");
            Assert.That(Unit(world, 3).Health, Is.EqualTo(190));
            Accepted(world.Submit(new AttackCommand(1, new[] { 2 }, 3)));
            world.Tick();
            Assert.That(world.Projectiles.Count, Is.EqualTo(1));
            Accepted(world.Submit(new StopCommand(1, new[] { 2 })));
            Until(world, () => world.Projectiles.Count == 0, "Post-upgrade projectile impacts");
            Assert.That(Unit(world, 3).Health, Is.EqualTo(177));
        }

        [Test]
        public void EffectiveGatherAmountPreservesCargoAndClampsToRemainingCarrySpace()
        {
            var world = Create();
            Accepted(world.Submit(new GatherCommand(1, new[] { 1 }, 201)));
            Until(world, () => Unit(world).CarriedAmount > 0, "Initial base-rate harvest");
            Assert.That(Unit(world).CarriedAmount, Is.EqualTo(2));
            Accepted(world.Submit(new StopCommand(1, new[] { 1 })));
            Research(world, "harvest");
            Assert.That(Unit(world).GatherAmount, Is.EqualTo(11));
            Assert.That(Unit(world, 3).GatherAmount, Is.EqualTo(2));
            Assert.That(Unit(world).CarriedAmount, Is.EqualTo(2));
            int inventory = Player(world).Resources.Wood;
            Accepted(world.Submit(new GatherCommand(1, new[] { 1 }, 201)));
            Until(world, () => Unit(world).WorkerTask == WorkerTask.ReturningResources, "Upgraded harvest fills remaining carry space");
            Assert.That(Unit(world).CarriedAmount, Is.EqualTo(5));
            Assert.That(Player(world).Resources.Wood, Is.EqualTo(inventory), "Cargo still credits only at arrival.");
            Assert.That(world.TryGetResource(201, out var node), Is.True);
            Assert.That(node.RemainingAmount, Is.EqualTo(95));
            Until(world, () => Player(world).Resources.Wood > inventory, "Full load physically delivered");
            Assert.That(Player(world).Resources.Wood, Is.EqualTo(inventory + 5));
            Assert.That(Unit(world).CarriedAmount, Is.Zero);
        }

        [Test]
        public void MaximumEffectiveGatherAmountIsClampedBeforeCargoAddition()
        {
            var definitions = Definitions();
            Tech(definitions, "harvest").Effects[0].Amount = int.MaxValue - 2;
            var world = new World(definitions, Map());
            Research(world, "harvest");
            Assert.That(Unit(world).GatherAmount, Is.EqualTo(int.MaxValue));
            Accepted(world.Submit(new GatherCommand(1, new[] { 1 }, 201)));
            Until(world, () => Unit(world).WorkerTask == WorkerTask.ReturningResources, "Bounded maximum-rate harvest");
            Assert.That(Unit(world).CarriedAmount, Is.EqualTo(5));
            Assert.That(world.TryGetResource(201, out var node), Is.True);
            Assert.That(node.RemainingAmount, Is.EqualTo(95));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void LegacyDefinitionsWithoutErasRemainPlayable(bool nullArrays)
        {
            var definitions = CombatTestWorldFactory.Definitions();
            if (nullArrays) { definitions.Eras = null; definitions.Technologies = null; }
            var world = new World(definitions, CombatTestWorldFactory.Map());
            Assert.That(Player(world).EraTier, Is.EqualTo(1));
            Assert.That(Player(world).EraId, Is.Null);
            Assert.That(Player(world).CompletedTechnologyIds, Is.Empty);
            Accepted(world.Submit(new TrainCommand(1, 100, "worker")));
            Assert.That(world.Submit(new ResearchCommand(1, 101, "edge")).Accepted, Is.False);
        }

        [TestCase("null-era")]
        [TestCase("duplicate-era-id")]
        [TestCase("duplicate-tier")]
        [TestCase("missing-tier")]
        [TestCase("null-technology")]
        [TestCase("duplicate-technology")]
        [TestCase("zero-duration")]
        [TestCase("negative-cost")]
        [TestCase("unknown-producer")]
        [TestCase("unknown-era")]
        [TestCase("unknown-technology-prerequisite")]
        [TestCase("duplicate-prerequisite")]
        [TestCase("unknown-building-prerequisite")]
        [TestCase("unknown-unit-unlock")]
        [TestCase("unknown-building-era")]
        [TestCase("self-cycle")]
        [TestCase("mutual-cycle")]
        [TestCase("building-unlock-cycle")]
        [TestCase("null-effect")]
        [TestCase("unknown-effect-kind")]
        [TestCase("empty-effect-tags")]
        [TestCase("unknown-effect-tags")]
        [TestCase("zero-effect-amount")]
        [TestCase("negative-effect-amount")]
        [TestCase("missing-era-advance")]
        public void MalformedOrImpossibleTechnologyGraphRejectsBeforeWorldCreation(string fault)
        {
            var definitions = Definitions();
            var edge = Tech(definitions, "edge");
            switch (fault)
            {
                case "null-era": definitions.Eras[0] = null; break;
                case "duplicate-era-id": definitions.Eras[1].Id = "camp"; break;
                case "duplicate-tier": definitions.Eras[1].Tier = 1; break;
                case "missing-tier": definitions.Eras = new[] { definitions.Eras[0], definitions.Eras[1], definitions.Eras[2] }; break;
                case "null-technology": definitions.Technologies[3] = null; break;
                case "duplicate-technology": definitions.Technologies[4].Id = "edge"; break;
                case "zero-duration": edge.ResearchTicks = 0; break;
                case "negative-cost": edge.Cost = new ResourceAmount(-1, 0, 0, 0); break;
                case "unknown-producer": edge.ResearchBuildingId = "missing"; break;
                case "unknown-era": edge.RequiredEraId = "missing"; break;
                case "unknown-technology-prerequisite": edge.RequiredTechnologyIds = new[] { "missing" }; break;
                case "duplicate-prerequisite": Tech(definitions, "edge2").RequiredTechnologyIds = new[] { "edge", "edge" }; break;
                case "unknown-building-prerequisite": edge.RequiredBuildingIds = new[] { "missing" }; break;
                case "unknown-unit-unlock": definitions.Units[3].RequiredTechnologyIds = new[] { "missing" }; break;
                case "unknown-building-era": definitions.Buildings[2].RequiredEraId = "missing"; break;
                case "self-cycle": edge.RequiredTechnologyIds = new[] { "edge" }; break;
                case "mutual-cycle": edge.RequiredTechnologyIds = new[] { "edge2" }; break;
                case "building-unlock-cycle": edge.ResearchBuildingId = "workshop"; break;
                case "null-effect": edge.Effects[0] = null; break;
                case "unknown-effect-kind": edge.Effects[0].Kind = (TechnologyEffectKind)999; break;
                case "empty-effect-tags": edge.Effects[0].TargetTags = CombatTags.None; break;
                case "unknown-effect-tags": edge.Effects[0].TargetTags = (CombatTags)1024; break;
                case "zero-effect-amount": edge.Effects[0].Amount = 0; break;
                case "negative-effect-amount": edge.Effects[0].Amount = -1; break;
                case "missing-era-advance": Tech(definitions, "advance-citadel").AdvancesToEraId = null; break;
                default: throw new ArgumentException("Unknown test fault " + fault);
            }
            Assert.Throws<ArgumentException>(() => new World(definitions, Map()), fault);
        }

        [TestCase(TechnologyEffectKind.AttackDamage, CombatTags.Infantry)]
        [TestCase(TechnologyEffectKind.Armor, CombatTags.Infantry)]
        [TestCase(TechnologyEffectKind.GatherAmount, CombatTags.Worker)]
        public void CumulativeMatchingEffectsCannotOverflowEffectiveUnitStats(TechnologyEffectKind kind, CombatTags tags)
        {
            var definitions = Definitions();
            var edge = Tech(definitions, "edge");
            edge.Effects = new[] { new TechnologyEffect { Kind = kind, TargetTags = tags, Amount = int.MaxValue - 100 } };
            Tech(definitions, "edge2").Effects = new[] { new TechnologyEffect { Kind = kind, TargetTags = tags, Amount = 101 } };
            Assert.Throws<ArgumentException>(() => new World(definitions, Map()));
        }
    }
}
