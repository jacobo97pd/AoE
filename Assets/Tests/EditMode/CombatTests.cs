using System;
using System.Collections.Generic;
using Emberfield.Simulation;
using NUnit.Framework;
using static Emberfield.Tests.EditMode.CombatTestWorldFactory;

namespace Emberfield.Tests.EditMode
{
    public sealed class CombatTests
    {
        [TestCase("unit-armor")]
        [TestCase("building-armor")]
        [TestCase("damage")]
        [TestCase("range")]
        [TestCase("acquisition")]
        [TestCase("cooldown")]
        [TestCase("projectile-speed")]
        [TestCase("projectile-lifetime")]
        [TestCase("unit-tags")]
        [TestCase("bonus-tags")]
        [TestCase("bonus-multiplier")]
        public void InvalidCombatDefinitionsRejectWorldCreation(string invalid)
        {
            var definitions = Definitions();
            var fighter = definitions.Units[0];
            switch (invalid)
            {
                case "unit-armor": fighter.Armor = -1; break;
                case "building-armor": definitions.Buildings[0].Armor = -1; break;
                case "damage": fighter.Attack.Damage = -1; break;
                case "range": fighter.Attack.RangeMillimetres = -1; break;
                case "acquisition": fighter.Attack.AcquireRangeMillimetres = -1; break;
                case "cooldown": fighter.Attack.CooldownTicks = 0; break;
                case "projectile-speed": fighter.Attack.ProjectileSpeedMillimetresPerSecond = -1; break;
                case "projectile-lifetime": fighter.Attack.ProjectileLifetimeTicks = 0; break;
                case "unit-tags": fighter.Tags = (CombatTags)1024; break;
                case "bonus-tags": fighter.Attack.Bonuses = new[] { new DamageBonus { TargetTags = (CombatTags)1024, MultiplierPermille = 1200 } }; break;
                case "bonus-multiplier": fighter.Attack.Bonuses = new[] { new DamageBonus { TargetTags = CombatTags.Infantry, MultiplierPermille = -1 } }; break;
            }
            Assert.Throws<ArgumentException>(() => new World(definitions, Map()));
        }

        [TestCase("foreign-selection")]
        [TestCase("duplicate-selection")]
        [TestCase("unarmed-selection")]
        [TestCase("unknown-unit")]
        [TestCase("own-target")]
        [TestCase("resource-target")]
        [TestCase("unknown-target")]
        public void InvalidAttackIsAtomicAndPreservesExistingMove(string invalid)
        {
            var definitions = Definitions();
            var map = Map();
            map.UnitSpawns = new[] { Spawn(1, 1, "striker", 4500, 4500), Spawn(2, 2, "dummy", 8500, 4500), Spawn(3, 1, "dummy", 1500, 1500) };
            var world = new World(definitions, map);
            var destination = new SimPoint(4500, 7500);
            Accepted(world.Submit(new MoveCommand(1, new[] { 1 }, destination)));
            var unit = Unit(world);
            var position = unit.Position;
            int[] selection = { 1 };
            int target = 2;
            switch (invalid)
            {
                case "foreign-selection": selection = new[] { 1, 2 }; break;
                case "duplicate-selection": selection = new[] { 1, 1 }; break;
                case "unarmed-selection": selection = new[] { 1, 3 }; break;
                case "unknown-unit": selection = new[] { 1, 999 }; break;
                case "own-target": target = 3; break;
                case "resource-target": target = 201; break;
                case "unknown-target": target = 999; break;
            }
            Assert.That(world.Submit(new AttackCommand(1, selection, target)).Accepted, Is.False);
            Assert.That(unit.Position, Is.EqualTo(position));
            Assert.That(unit.Destination, Is.EqualTo(destination));
            Assert.That(unit.Order, Is.EqualTo(UnitOrder.Moving));
            Assert.That(unit.AttackTargetId, Is.Zero);
            Assert.That(Unit(world, 2).Health, Is.EqualTo(200));
            Assert.That(world.TickIndex, Is.Zero);
        }

        [Test]
        public void AttackCommandDoesNotDealDamageBeforeTheNextTick()
        {
            var world = Create();
            Accepted(world.Submit(new AttackCommand(1, new[] { 1 }, 2)));
            Assert.That(Unit(world, 2).Health, Is.EqualTo(200));
            Assert.That(world.TickIndex, Is.Zero);
            world.Tick();
            Assert.That(Unit(world, 2).Health, Is.EqualTo(190));
        }

        [Test]
        public void RepeatedAttackOrdersCannotResetCooldownOrAccelerateDamage()
        {
            var a = Create(); var b = Create();
            Accepted(a.Submit(new AttackCommand(1, new[] { 1 }, 2)));
            for (int tick = 0; tick < 30; tick++)
            {
                Accepted(b.Submit(new AttackCommand(1, new[] { 1 }, 2)));
                a.Tick(); b.Tick();
                Assert.That(Unit(b, 2).Health, Is.EqualTo(Unit(a, 2).Health), "Damage differs at tick " + tick);
                Assert.That(Unit(b).AttackCooldownTicks, Is.EqualTo(Unit(a).AttackCooldownTicks));
            }
            Assert.That(Unit(a, 2).Health, Is.LessThan(190), "Cooldown must permit later attacks.");
        }

        [TestCase(1500, true)]
        [TestCase(1501, false)]
        public void UnitRangeIncludesBothRadiiAndUsesAnExactBoundary(int centerDistance, bool inRange)
        {
            var definitions = Definitions();
            definitions.Units[0].MoveSpeedMillimetresPerSecond = 1;
            var map = Map(); map.UnitSpawns[1].Position = new SimPoint(4500 + centerDistance, 4500);
            var world = new World(definitions, map);
            Accepted(world.Submit(new AttackCommand(1, new[] { 1 }, 2)));
            world.Tick();
            Assert.That(Unit(world, 2).Health, Is.EqualTo(inRange ? 190 : 200));
        }

        [TestCase(1250, true)]
        [TestCase(1249, false)]
        public void BuildingRangeUsesItsFootprintRatherThanItsCenter(int range, bool inRange)
        {
            var definitions = Definitions(); definitions.Units[0].Attack.RangeMillimetres = range;
            definitions.Units[0].MoveSpeedMillimetresPerSecond = 1;
            var map = Map(); map.UnitSpawns = new[] { Spawn(1, 1, "striker", 4500, 4500) };
            map.BuildingSpawns[1].Position = new SimPoint(7500, 4500);
            var world = new World(definitions, map);
            Accepted(world.Submit(new AttackCommand(1, new[] { 1 }, 101)));
            world.Tick();
            Assert.That(Building(world, 101).Health, Is.EqualTo(inRange ? 490 : 500));
        }

        [TestCase(0, 10)]
        [TestCase(4, 6)]
        [TestCase(100, 1)]
        public void ArmorReducesDamageButCannotReduceAHitBelowOne(int armor, int expectedDamage)
        {
            var definitions = Definitions(); definitions.Units[1].Armor = armor;
            var world = new World(definitions, Map());
            Accepted(world.Submit(new AttackCommand(1, new[] { 1 }, 2))); world.Tick();
            Assert.That(Unit(world, 2).Health, Is.EqualTo(200 - expectedDamage));
        }

        [Test]
        public void HighestMatchingTagMultiplierAppliesOnceBeforeArmor()
        {
            var definitions = Definitions();
            definitions.Units[0].Attack.Bonuses = new[]
            {
                new DamageBonus { TargetTags = CombatTags.Infantry, MultiplierPermille = 1500 },
                new DamageBonus { TargetTags = CombatTags.Cavalry | CombatTags.Ranged, MultiplierPermille = 2000 },
                new DamageBonus { TargetTags = CombatTags.Structure, MultiplierPermille = 9000 }
            };
            definitions.Units[1].Tags = CombatTags.Infantry | CombatTags.Ranged;
            definitions.Units[1].Armor = 3;
            var world = new World(definitions, Map());
            Accepted(world.Submit(new AttackCommand(1, new[] { 1 }, 2))); world.Tick();
            Assert.That(Unit(world, 2).Health, Is.EqualTo(183), "10 * 2 - 3, without stacking or applying the unrelated structure bonus.");
        }

        [Test]
        public void ProjectileTravelsBeforeApplyingItsSingleDamageSnapshot()
        {
            var definitions = Definitions(); definitions.Units[0].Attack = Attack(13, 6000, 500, 1000);
            var map = Map(); map.UnitSpawns[1].Position = new SimPoint(9500, 4500);
            var world = new World(definitions, map);
            var target = Unit(world, 2);
            Accepted(world.Submit(new AttackCommand(1, new[] { 1 }, 2))); world.Tick();
            Assert.That(world.Projectiles.Count, Is.EqualTo(1));
            Assert.That(target.Health, Is.EqualTo(200));
            var projectile = world.Projectiles[0]; var start = projectile.Position;
            Accepted(world.Submit(new StopCommand(1, new[] { 1 })));
            world.Tick();
            Assert.That(projectile.Position, Is.Not.EqualTo(start));
            Assert.That(target.Health, Is.EqualTo(200));
            Until(world, () => world.Projectiles.Count == 0, "Projectile reaches target");
            Assert.That(target.Health, Is.EqualTo(187));
            Tick(world, 30); Assert.That(target.Health, Is.EqualTo(187), "Resolved projectiles cannot damage twice.");
        }

        [Test]
        public void ProjectileSurvivesItsSourceDeathAndStillHits()
        {
            var definitions = Definitions(); definitions.Units[0].Attack = Attack(13, 6000, 500, 1000);
            var killer = UnitDefinition("killer", 500); definitions.Units = new[] { definitions.Units[0], definitions.Units[1], definitions.Units[2], killer };
            var map = Map(); map.UnitSpawns = new[] { Spawn(1, 1, "striker", 4500, 4500), Spawn(2, 2, "dummy", 9500, 4500), Spawn(3, 2, "killer", 5000, 4500) };
            var world = new World(definitions, map); var target = Unit(world, 2);
            Accepted(world.Submit(new AttackCommand(1, new[] { 1 }, 2)));
            Accepted(world.Submit(new AttackCommand(2, new[] { 3 }, 1))); world.Tick();
            Assert.That(world.TryGetUnit(1, out _), Is.False);
            Assert.That(world.Projectiles.Count, Is.EqualTo(1));
            Until(world, () => world.Projectiles.Count == 0, "Released projectile survives source removal");
            Assert.That(target.Health, Is.EqualTo(187));
        }

        [Test]
        public void ProjectileWhoseTargetDiesDoesNotHitAnotherEntityOrCountAnotherDeath()
        {
            var definitions = Definitions(); definitions.Units[0].Attack = Attack(13, 6000, 500, 1000);
            var killer = UnitDefinition("killer", 500); definitions.Units = new[] { definitions.Units[0], definitions.Units[1], definitions.Units[2], killer };
            var map = Map(); map.UnitSpawns = new[] { Spawn(1, 1, "striker", 4500, 4500), Spawn(2, 2, "dummy", 9500, 4500), Spawn(3, 1, "killer", 9500, 5000) };
            var world = new World(definitions, map);
            Accepted(world.Submit(new AttackCommand(1, new[] { 1 }, 2)));
            Accepted(world.Submit(new AttackCommand(1, new[] { 3 }, 2))); world.Tick();
            Assert.That(world.TryGetUnit(2, out _), Is.False);
            Until(world, () => world.Projectiles.Count == 0, "Projectile expires after its target dies");
            Tick(world, 30);
            Assert.That(world.DeathCount, Is.EqualTo(1));
            Assert.That(Unit(world, 3).Health, Is.EqualTo(200));
        }

        [Test]
        public void ProjectileTracksAMovingTargetWithoutInstantDamage()
        {
            var definitions = Definitions(); definitions.Units[0].Attack = Attack(13, 6000, 500, 5000);
            definitions.Units[1].MoveSpeedMillimetresPerSecond = 1000;
            var map = Map(); map.UnitSpawns[1].Position = new SimPoint(9500, 4500);
            var world = new World(definitions, map); var target = Unit(world, 2);
            Accepted(world.Submit(new AttackCommand(1, new[] { 1 }, 2))); world.Tick();
            Accepted(world.Submit(new StopCommand(1, new[] { 1 })));
            var start = target.Position;
            Accepted(world.Submit(new MoveCommand(2, new[] { 2 }, new SimPoint(9500, 9500))));
            Until(world, () => target.Health < 200, "Homing projectile intercepts the moving target");
            Assert.That(target.Position, Is.Not.EqualTo(start));
            Assert.That(target.Health, Is.EqualTo(187));
        }

        [Test]
        public void ProjectileExpiresWithoutDamageWhenATargetOutrunsIt()
        {
            var definitions = Definitions(); definitions.Units[0].Attack = Attack(13, 6000, 500, 1000);
            definitions.Units[0].Attack.ProjectileLifetimeTicks = 8;
            definitions.Units[1].MoveSpeedMillimetresPerSecond = 5000;
            var map = Map(); map.UnitSpawns[1].Position = new SimPoint(9500, 4500);
            var world = new World(definitions, map); var target = Unit(world, 2);
            Accepted(world.Submit(new AttackCommand(1, new[] { 1 }, 2))); world.Tick();
            Assert.That(world.Projectiles.Count, Is.EqualTo(1));
            Assert.That(world.Projectiles[0].RemainingLifetimeTicks, Is.EqualTo(8));
            Accepted(world.Submit(new StopCommand(1, new[] { 1 })));
            var start = target.Position;
            Accepted(world.Submit(new MoveCommand(2, new[] { 2 }, new SimPoint(20500, 4500))));
            Tick(world, 7);
            Assert.That(world.Projectiles.Count, Is.EqualTo(1), "A still-airborne shot retains its configured lifetime.");
            Assert.That(target.Health, Is.EqualTo(200));
            world.Tick();
            Assert.That(world.Projectiles.Count, Is.Zero, "Runaway targets cannot retain projectiles indefinitely.");
            Assert.That(target.Position.X, Is.GreaterThan(start.X));
            Assert.That(target.Health, Is.EqualTo(200));
            Assert.That(world.DeathCount, Is.Zero);
        }

        [Test]
        public void IdleAcquisitionChoosesNearestEnemyWithStableIdTieBreak()
        {
            var map = Map();
            map.UnitSpawns = new[] { Spawn(1, 1, "striker", 4500, 4500), Spawn(9, 2, "dummy", 5500, 4500), Spawn(2, 2, "dummy", 3500, 4500) };
            var world = new World(Definitions(), map); world.Tick();
            Assert.That(Unit(world).AttackTargetId, Is.EqualTo(2));
            Assert.That(Unit(world, 2).Health, Is.LessThan(200));
            Assert.That(Unit(world, 9).Health, Is.EqualTo(200));
        }

        [Test]
        public void IdleAcquisitionSkipsCloserEnemyWithoutAReachableFiringPosition()
        {
            var map = Map();
            map.UnitSpawns = new[] { Spawn(1, 1, "striker", 4500, 4500), Spawn(2, 2, "dummy", 9500, 4500), Spawn(9, 2, "dummy", 5500, 10500) };
            map.BlockedCells = new GridCell[map.HeightCells];
            for (int z = 0; z < map.HeightCells; z++) map.BlockedCells[z] = new GridCell(7, z);
            var world = new World(Definitions(), map); world.Tick();
            Assert.That(Unit(world).AttackTargetId, Is.EqualTo(9), "The nearer enemy across the sealed wall cannot be engaged by this melee unit.");
            Assert.That(Unit(world, 2).Health, Is.EqualTo(200));
        }

        [Test]
        public void ExplicitAttackChasesTargetThatMovesBeyondItsOriginalPosition()
        {
            var definitions = Definitions(); definitions.Units[1].MoveSpeedMillimetresPerSecond = 1000;
            var map = Map(); map.UnitSpawns[1].Position = new SimPoint(8500, 4500);
            var world = new World(definitions, map); var victim = Unit(world, 2);
            Accepted(world.Submit(new AttackCommand(1, new[] { 1 }, 2)));
            Accepted(world.Submit(new MoveCommand(2, new[] { 2 }, new SimPoint(14500, 4500))));
            Until(world, () => victim.Health < 200, "Attacker catches moving target");
            Assert.That(victim.Position.X, Is.GreaterThan(8500));
            Assert.That(Unit(world).Position.X, Is.GreaterThan(4500));
        }

        [Test]
        public void UnreachableTargetCannotBeDamagedThroughASealedWall()
        {
            var map = Map(); map.UnitSpawns[1].Position = new SimPoint(9500, 4500);
            map.BlockedCells = new GridCell[map.HeightCells];
            for (int z = 0; z < map.HeightCells; z++) map.BlockedCells[z] = new GridCell(7, z);
            var world = new World(Definitions(), map);
            world.Submit(new AttackCommand(1, new[] { 1 }, 2));
            Tick(world, 200);
            Assert.That(Unit(world, 2).Health, Is.EqualTo(200));
            Assert.That(Unit(world).Position.X, Is.LessThan(7000));
            Assert.That(world.IsWalkable(Unit(world).Position), Is.True);
        }

        [Test]
        public void ExplicitMoveCancelsAttackAndDoesNotFireWhileTravelling()
        {
            var definitions = Definitions(); definitions.Units[0].MoveSpeedMillimetresPerSecond = 1000;
            var world = new World(definitions, Map()); var target = Unit(world, 2);
            Accepted(world.Submit(new AttackCommand(1, new[] { 1 }, 2)));
            Accepted(world.Submit(new MoveCommand(1, new[] { 1 }, new SimPoint(4500, 14500))));
            Tick(world, 10);
            Assert.That(Unit(world).AttackTargetId, Is.Zero);
            Assert.That(Unit(world).Order, Is.EqualTo(UnitOrder.Moving));
            Assert.That(target.Health, Is.EqualTo(200));
        }

        [Test]
        public void StopHoldsFireUntilAnExplicitAttackOrder()
        {
            var world = Create();
            Accepted(world.Submit(new AttackCommand(1, new[] { 1 }, 2)));
            Accepted(world.Submit(new StopCommand(1, new[] { 1 })));
            Tick(world, 40);
            Assert.That(Unit(world).AutoAttackEnabled, Is.False);
            Assert.That(Unit(world, 2).Health, Is.EqualTo(200));
            Accepted(world.Submit(new AttackCommand(1, new[] { 1 }, 2))); world.Tick();
            Assert.That(Unit(world, 2).Health, Is.EqualTo(190));
        }

        [Test]
        public void GatherOrderCancelsWorkersAttackWithoutDiscardingCargo()
        {
            var definitions = Definitions(); definitions.Units[2].Attack.Damage = 3;
            var map = Map(); map.UnitSpawns = new[] { Spawn(1, 1, "worker", 4500, 11500), Spawn(2, 2, "dummy", 5500, 11500) };
            var world = new World(definitions, map); var worker = Unit(world);
            Accepted(world.Submit(new GatherCommand(1, new[] { 1 }, 201)));
            Until(world, () => worker.CarriedAmount > 0, "Worker gathers physical cargo");
            int cargo = worker.CarriedAmount;
            Accepted(world.Submit(new AttackCommand(1, new[] { 1 }, 2)));
            Assert.That(worker.WorkerTask, Is.EqualTo(WorkerTask.None));
            Assert.That(worker.CarriedAmount, Is.EqualTo(cargo));
            Accepted(world.Submit(new GatherCommand(1, new[] { 1 }, 201)));
            Assert.That(worker.AttackTargetId, Is.Zero);
            Assert.That(worker.CarriedAmount, Is.EqualTo(cargo));
            Assert.That(worker.WorkerTask, Is.Not.EqualTo(WorkerTask.None));
        }

        [TestCase("return")]
        [TestCase("build")]
        [TestCase("construct")]
        public void ExplicitWorkerJobsCancelCombatWhilePreservingTheirCarriedLoad(string job)
        {
            var definitions = Definitions(); definitions.Units[2].Attack.Damage = 3;
            var map = Map();
            map.UnitSpawns = new[] { Spawn(1, 1, "worker", 4500, 11500), Spawn(2, 2, "dummy", 5500, 11500), Spawn(3, 1, "worker", 8500, 15500) };
            var world = new World(definitions, map); var worker = Unit(world);
            Accepted(world.Submit(new GatherCommand(1, new[] { 1 }, 201)));
            Until(world, () => worker.CarriedAmount > 0, "Worker carries resources before its combat order");
            int cargo = worker.CarriedAmount;
            int constructionId = 0;
            if (job == "construct")
            {
                var foundation = world.Submit(new BuildCommand(1, new[] { 3 }, "house", new SimPoint(6500, 13500)));
                Accepted(foundation); constructionId = foundation.EntityId;
            }
            Accepted(world.Submit(new AttackCommand(1, new[] { 1 }, 2)));
            Assert.That(worker.AttackTargetId, Is.EqualTo(2));
            CommandResult result;
            if (job == "return") result = world.Submit(new ReturnCargoCommand(1, new[] { 1 }));
            else if (job == "build") result = world.Submit(new BuildCommand(1, new[] { 1 }, "house", new SimPoint(6500, 13500)));
            else result = world.Submit(new ConstructCommand(1, new[] { 1 }, constructionId));
            Accepted(result);
            Assert.That(worker.AttackTargetId, Is.Zero);
            Assert.That(worker.WorkerTask, Is.Not.EqualTo(WorkerTask.None));
            Assert.That(worker.CarriedAmount, Is.EqualTo(cargo));
            Assert.That(worker.CarriedKind, Is.EqualTo(ResourceKind.Wood));
        }

        [Test]
        public void WorkerDeathLosesCargoWithoutCreditingOrDuplicatingIt()
        {
            var definitions = Definitions(); definitions.Units[0].Attack.Damage = 500;
            var map = Map(); map.UnitSpawns = new[] { Spawn(1, 1, "worker", 4500, 11500), Spawn(2, 2, "striker", 5500, 11500) };
            var world = new World(definitions, map); var worker = Unit(world);
            Accepted(world.Submit(new StopCommand(2, new[] { 2 })));
            Accepted(world.Submit(new GatherCommand(1, new[] { 1 }, 201)));
            Until(world, () => worker.CarriedAmount > 0, "Worker carries stock before death");
            var inventory = Player(world).Resources;
            Assert.That(world.TryGetResource(201, out var resource), Is.True);
            int remaining = resource.RemainingAmount;
            Assert.That(remaining, Is.LessThan(100));
            Accepted(world.Submit(new AttackCommand(2, new[] { 2 }, 1))); world.Tick();
            Assert.That(world.TryGetUnit(1, out _), Is.False);
            Tick(world, 100);
            Assert.That(Player(world).Resources, Is.EqualTo(inventory));
            Assert.That(resource.RemainingAmount, Is.EqualTo(remaining));
            Assert.That(Player(world).PopulationUsed, Is.Zero);
        }

        [Test]
        public void UnitDeathRemovesIdentityAndReleasesPopulationExactlyOnce()
        {
            var definitions = Definitions(); definitions.Units[0].Attack.Damage = 500;
            definitions.Units[1].PopulationCost = 3;
            var world = new World(definitions, Map()); var victim = Unit(world, 2);
            int population = Player(world, 2).PopulationUsed;
            Accepted(world.Submit(new AttackCommand(1, new[] { 1 }, 2))); world.Tick();
            Assert.That(victim.Health, Is.Zero);
            Assert.That(world.TryGetUnit(2, out _), Is.False);
            Assert.That(Player(world, 2).PopulationUsed, Is.EqualTo(population - 3));
            Assert.That(world.DeathCount, Is.EqualTo(1));
            Assert.That(world.Submit(new AttackCommand(1, new[] { 1 }, 2)).Accepted, Is.False);
            Tick(world, 30);
            Assert.That(Player(world, 2).PopulationUsed, Is.EqualTo(population - 3));
            Assert.That(world.DeathCount, Is.EqualTo(1));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void SimultaneousLethalMeleeAttacksKillBothUnitsRegardlessOfOwnerIdOrder(bool swapOwners)
        {
            var definitions = Definitions(); definitions.Units[0].Attack.Damage = 500;
            int firstOwner = swapOwners ? 2 : 1;
            int secondOwner = swapOwners ? 1 : 2;
            var map = Map();
            map.UnitSpawns = new[] { Spawn(1, firstOwner, "striker", 4500, 4500), Spawn(2, secondOwner, "striker", 5500, 4500) };
            var world = new World(definitions, map);
            Assert.That(Player(world, 1).PopulationUsed, Is.EqualTo(1));
            Assert.That(Player(world, 2).PopulationUsed, Is.EqualTo(1));
            Accepted(world.Submit(new AttackCommand(firstOwner, new[] { 1 }, 2)));
            Accepted(world.Submit(new AttackCommand(secondOwner, new[] { 2 }, 1)));
            world.Tick();
            Assert.That(world.TryGetUnit(1, out _), Is.False);
            Assert.That(world.TryGetUnit(2, out _), Is.False, "Both actors alive for this tick's melee decisions must release their due attacks.");
            Assert.That(world.DeathCount, Is.EqualTo(2));
            Assert.That(Player(world, 1).PopulationUsed, Is.Zero);
            Assert.That(Player(world, 2).PopulationUsed, Is.Zero);
            Tick(world, 30);
            Assert.That(world.DeathCount, Is.EqualTo(2));
            Assert.That(Player(world, 1).PopulationUsed + Player(world, 2).PopulationUsed, Is.Zero);
        }

        [Test]
        public void DestroyedProducerReleasesQueueReservationsCapacityAndNavigationWithoutRefunds()
        {
            var definitions = Definitions(); definitions.Units[0].Attack = Attack(500, 2000);
            var map = Map(); map.UnitSpawns = new[] { Spawn(1, 1, "striker", 4500, 4500) };
            map.BuildingSpawns[1].Position = new SimPoint(7500, 4500);
            var world = new World(definitions, map); var hall = Building(world, 101); var player = Player(world, 2);
            Accepted(world.Submit(new TrainCommand(2, 101, "worker")));
            Accepted(world.Submit(new TrainCommand(2, 101, "striker")));
            var paidInventory = player.Resources; int originalCapacity = player.PopulationCapacity;
            Assert.That(player.PopulationReserved, Is.EqualTo(2));
            Accepted(world.Submit(new AttackCommand(1, new[] { 1 }, 101))); world.Tick();
            Assert.That(world.TryGetBuilding(101, out _), Is.False);
            Assert.That(hall.Health, Is.Zero);
            Assert.That(player.PopulationReserved, Is.Zero);
            Assert.That(player.PopulationCapacity, Is.EqualTo(originalCapacity - 4));
            Assert.That(player.Resources, Is.EqualTo(paidInventory), "Destroyed queues do not refund in Phase 3.");
            Assert.That(world.IsWalkable(hall.Position), Is.True);
            int entities = world.Units.Count;
            Tick(world, 150);
            Assert.That(world.Units.Count, Is.EqualTo(entities));
            Assert.That(player.PopulationReserved, Is.Zero);
            Assert.That(world.DeathCount, Is.EqualTo(1));
        }

        [Test]
        public void ConstructionProgressDoesNotEraseDamageAlreadyDealtToAnIncompleteSite()
        {
            var definitions = Definitions(); definitions.Units[0].Attack = Attack(7, 1000, 1000);
            var map = Map();
            map.UnitSpawns = new[] { Spawn(1, 1, "worker", 14500, 10500), Spawn(2, 2, "striker", 14500, 12500) };
            var world = new World(definitions, map);
            Accepted(world.Submit(new StopCommand(2, new[] { 2 })));
            var placed = world.Submit(new BuildCommand(1, new[] { 1 }, "house", new SimPoint(14500, 11500)));
            Accepted(placed); var site = Building(world, placed.EntityId);
            Until(world, () => site.Health >= 30, "Foundation gains enough health to survive a hit");
            int healthBefore = site.Health; int progressBefore = site.BuildProgressTicks;
            Accepted(world.Submit(new AttackCommand(2, new[] { 2 }, site.Id))); world.Tick();
            Assert.That(site.Health, Is.LessThan(healthBefore + 3), "Construction cannot obscure a real damage event.");
            Assert.That(site.BuildProgressTicks, Is.GreaterThan(progressBefore));
            Accepted(world.Submit(new StopCommand(2, new[] { 2 })));
            Until(world, () => site.IsComplete, "Damaged site completes construction");
            Assert.That(site.Health, Is.EqualTo(site.MaxHealth - 7), "Completion preserves the prior combat damage deficit.");
        }

        [TestCase("reedguard", "strider")]
        [TestCase("stringwarden", "reedguard")]
        [TestCase("strider", "stringwarden")]
        public void ShippedSoftCounterWinsEqualHeadcountAndEqualTotalResourceValue(string favored, string disadvantaged)
        {
            var definitions = ShippedCombatDefinitions();
            foreach (var definition in definitions.Units)
            {
                var cost = definition.Cost;
                Assert.That(cost.Food + cost.Wood + cost.Metal + cost.Stone, Is.EqualTo(80));
                Assert.That(definition.PopulationCost, Is.EqualTo(1));
            }
            var world = CreateCounterBattle(favored, 1, disadvantaged, 1);
            Until(world, () => (SurvivingUnits(world, 1) == 0 || SurvivingUnits(world, 2) == 0) && world.Projectiles.Count == 0, "Equal-value counter duel and outstanding projectiles resolve", 4000);
            Assert.That(SurvivingUnits(world, 1), Is.EqualTo(1), favored + " should beat " + disadvantaged + " in this open approach fixture.");
            Assert.That(SurvivingUnits(world, 2), Is.Zero);
        }

        [TestCase("reedguard", "strider")]
        [TestCase("stringwarden", "reedguard")]
        [TestCase("strider", "stringwarden")]
        public void TwiceAsManyDisadvantagedUnitsOvercomeTheShippedSoftCounter(string favored, string disadvantaged)
        {
            var world = CreateCounterBattle(favored, 1, disadvantaged, 2);
            Until(world, () => (SurvivingUnits(world, 1) == 0 || SurvivingUnits(world, 2) == 0) && world.Projectiles.Count == 0, "Numerical advantage and outstanding projectiles resolve", 4000);
            Assert.That(SurvivingUnits(world, 1), Is.Zero, "Counter bonuses must not guarantee victory against two opponents.");
            Assert.That(SurvivingUnits(world, 2), Is.GreaterThan(0));
        }
    }
}
