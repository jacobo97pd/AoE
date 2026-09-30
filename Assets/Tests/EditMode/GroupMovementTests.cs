using System;
using System.Collections.Generic;
using Emberfield.Simulation;
using NUnit.Framework;

namespace Emberfield.Tests.EditMode
{
    public class GroupMovementTests
    {
        [Test]
        public void InitialCirclePenetrationIsRejectedButExactTangencyIsLegal()
        {
            Assert.Throws<ArgumentException>(() => Create(new[] { Spawn(1, 5500, 8500), Spawn(2, 6099, 8500) }));
            var world = Create(new[] { Spawn(1, 5500, 8500), Spawn(2, 6100, 8500) });
            Accepted(world.Submit(new MoveCommand(1, new[] { 1 }, new SimPoint(1500, 8500))));
            Accepted(world.Submit(new MoveCommand(1, new[] { 2 }, new SimPoint(12500, 8500))));
            Finish(world, Goals(world, 1, 2));
        }

        [TestCase(3200)]
        [TestCase(100000)]
        public void HeadOnMoversDoNotTunnelAndBothReachTheirGoals(int speed)
        {
            var world = Create(new[] { Spawn(1, 5500, 8500), Spawn(2, 10500, 8500) }, speed: speed);
            Accepted(world.Submit(new MoveCommand(1, new[] { 1 }, new SimPoint(26500, 8500))));
            Accepted(world.Submit(new MoveCommand(1, new[] { 2 }, new SimPoint(1500, 8500))));
            Finish(world, Goals(world, 1, 2));
        }

        [Test]
        public void CrossingGroupsMakeProgressWithoutCircleOverlap()
        {
            var spawns = new List<UnitSpawnDefinition>();
            for (int i = 0; i < 4; i++)
            {
                spawns.Add(Spawn(i + 1, 3500, 8500 + i * 1000));
                spawns.Add(Spawn(i + 11, 11500 + i * 1000, 3500));
            }
            var world = Create(spawns.ToArray());
            Accepted(world.Submit(new MoveCommand(1, new[] { 1, 2, 3, 4 }, new SimPoint(26500, 10500))));
            Accepted(world.Submit(new MoveCommand(1, new[] { 11, 12, 13, 14 }, new SimPoint(13500, 20500))));
            Finish(world, Goals(world, 1, 2, 3, 4, 11, 12, 13, 14));
        }

        [Test]
        public void OpposingGroupsUseNarrowChokeWithOpenStagingAreas()
        {
            var blocked = new List<GridCell>();
            for (int z = 0; z < 24; z++) if (z != 11) blocked.Add(new GridCell(15, z));
            var world = Create(new[]
            {
                Spawn(1, 5500, 10500), Spawn(2, 5500, 12500),
                Spawn(11, 25500, 10500, owner: 2), Spawn(12, 25500, 12500, owner: 2)
            }, blocked: blocked.ToArray());
            Accepted(world.Submit(new MoveCommand(1, new[] { 1, 2 }, new SimPoint(27500, 11500))));
            Accepted(world.Submit(new MoveCommand(2, new[] { 11, 12 }, new SimPoint(3500, 11500))));
            Finish(world, Goals(world, 1, 2, 11, 12));
        }

        [Test]
        public void MixedRadiiAndSpeedsReserveSafeSlotsAndSettleExactly()
        {
            var spawns = new UnitSpawnDefinition[8];
            for (int i = 0; i < spawns.Length; i++)
                spawns[i] = Spawn(i + 1, 3500 + i % 2 * 1000, 8500 + i / 2 * 1000, i % 2 == 0 ? "small" : "large");
            var world = Create(spawns);
            Accepted(world.Submit(new MoveCommand(1, new[] { 8, 7, 6, 5, 4, 3, 2, 1 }, new SimPoint(25500, 12500))));
            var goals = Goals(world, 1, 2, 3, 4, 5, 6, 7, 8);
            foreach (var a in world.Units)
            foreach (var b in world.Units)
                if (a.Id < b.Id)
                    Assert.That(DistanceSquared(goals[a.Id], goals[b.Id]),
                        Is.GreaterThanOrEqualTo(Square(a.RadiusMillimetres + b.RadiusMillimetres)), "Assigned circles must fit together.");
            Finish(world, goals);
        }

        [Test]
        public void CircleAndSweptPathClearStaticObstacleCorners()
        {
            var blocked = new List<GridCell>();
            for (int z = 0; z <= 14; z++) blocked.Add(new GridCell(10, z));
            for (int x = 11; x <= 18; x++) blocked.Add(new GridCell(x, 14));
            var world = Create(new[] { Spawn(1, 4500, 9500, "large") }, blocked: blocked.ToArray());
            Accepted(world.Submit(new MoveCommand(1, new[] { 1 }, new SimPoint(24500, 10500))));
            Finish(world, Goals(world, 1));
        }

        [Test]
        public void ClickOnIdleBodyFindsNearbyGoalWithoutPushingIt()
        {
            var world = Create(new[] { Spawn(1, 3500, 10500), Spawn(2, 18500, 10500) });
            var idlePosition = Unit(world, 2).Position;
            Accepted(world.Submit(new StopCommand(1, new[] { 2 })));
            Accepted(world.Submit(new MoveCommand(1, new[] { 1 }, idlePosition)));
            var goal = Unit(world, 1).Destination;
            Assert.AreNotEqual(idlePosition, goal);
            Assert.That(DistanceSquared(goal, idlePosition), Is.LessThanOrEqualTo(Square(2000)), "An isolated occupied click should resolve locally.");
            Finish(world, Goals(world, 1), () => Assert.AreEqual(idlePosition, Unit(world, 2).Position));
        }

        [Test]
        public void StopHoldsOneCrowdedBodyWhileRedirectedMoversClearOldGoals()
        {
            var spawns = new UnitSpawnDefinition[6];
            for (int i = 0; i < spawns.Length; i++)
                spawns[i] = Spawn(i + 1, 3500 + i % 2 * 1000, 8500 + i / 2 * 1000);
            var world = Create(spawns);
            Accepted(world.Submit(new MoveCommand(1, new[] { 1, 2, 3, 4, 5, 6 }, new SimPoint(26500, 10500))));
            for (int tick = 0; tick < 45; tick++) Step(world);
            Accepted(world.Submit(new StopCommand(1, new[] { 1 })));
            var stopped = Unit(world, 1).Position;
            var oldGoal = Unit(world, 2).Destination;
            Accepted(world.Submit(new MoveCommand(1, new[] { 6, 5, 4, 3, 2 }, new SimPoint(6500, 19500))));
            Assert.AreNotEqual(oldGoal, Unit(world, 2).Destination);
            Finish(world, Goals(world, 2, 3, 4, 5, 6), () =>
            {
                Assert.AreEqual(stopped, Unit(world, 1).Position, "Stop is a fixed body, not permission to push it.");
                Assert.AreEqual(UnitOrder.Idle, Unit(world, 1).Order);
            });
        }

        [Test]
        public void StableIdsAndRepeatedOrdersReplayAcrossSpawnAndSelectionOrder()
        {
            var spawns = new[]
            {
                Spawn(1, 3500, 8500), Spawn(2, 3500, 9500), Spawn(3, 3500, 10500),
                Spawn(4, 3500, 11500), Spawn(20, 12500, 10500)
            };
            var reversed = (UnitSpawnDefinition[])spawns.Clone();
            Array.Reverse(reversed);
            var a = Create(spawns); var b = Create(reversed);
            var target = new SimPoint(25500, 10500);
            Accepted(a.Submit(new MoveCommand(1, new[] { 1, 2, 3, 4 }, target)));
            Accepted(b.Submit(new MoveCommand(1, new[] { 4, 3, 2, 1 }, target)));
            var goals = Goals(a, 1, 2, 3, 4);
            for (int tick = 0; tick < World.TickRate * 120 && !Settled(a, goals); tick++)
            {
                if (tick % 7 == 0)
                {
                    Accepted(a.Submit(new MoveCommand(1, new[] { 1, 2, 3, 4 }, target)));
                    Accepted(b.Submit(new MoveCommand(1, new[] { 4, 3, 2, 1 }, target)));
                    foreach (var goal in goals) Assert.AreEqual(goal.Value, Unit(a, goal.Key).Destination, "Repeating the order must preserve slots.");
                }
                Step(a); Step(b);
                foreach (var unit in a.Units)
                {
                    var other = Unit(b, unit.Id);
                    Assert.AreEqual(unit.Position, other.Position, "Stable IDs must determine replay position.");
                    Assert.AreEqual(unit.Destination, other.Destination);
                    Assert.AreEqual(unit.Order, other.Order);
                }
            }
            Assert.IsTrue(Settled(a, goals), "Repeated commands must not prevent arrival.");
            Assert.IsTrue(Settled(b, goals));
        }

        [Test]
        public void NewFoundationIsAvoidedAndDestroyedFootprintCanBeUsedAgain()
        {
            var world = Create(new[]
            {
                Spawn(1, 3500, 9500), Spawn(2, 3500, 11500),
                Spawn(3, 12500, 2500, owner: 2), Spawn(4, 22500, 2500, "demolisher")
            });
            Accepted(world.Submit(new StopCommand(1, new[] { 4 })));
            Accepted(world.Submit(new MoveCommand(1, new[] { 1, 2 }, new SimPoint(26500, 9500))));
            for (int tick = 0; tick < 10; tick++) Step(world);
            var foundationPosition = new SimPoint(12000, 10000);
            var built = world.Submit(new BuildCommand(2, new[] { 3 }, "barrier", foundationPosition));
            Accepted(built);
            Assert.IsFalse(world.IsWalkable(foundationPosition));
            for (int tick = 0; tick < 40; tick++) Step(world);
            Accepted(world.Submit(new AttackCommand(1, new[] { 4 }, built.EntityId)));
            for (int tick = 0; tick < 100 && world.TryGetBuilding(built.EntityId, out _); tick++) Step(world);
            Assert.IsFalse(world.TryGetBuilding(built.EntityId, out _), "Destroy the obstacle through a legal combat order.");
            Accepted(world.Submit(new StopCommand(1, new[] { 4 })));
            Assert.IsTrue(world.IsWalkable(foundationPosition));
            Accepted(world.Submit(new MoveCommand(1, new[] { 1, 2 }, foundationPosition)));
            Finish(world, Goals(world, 1, 2));
        }

        [Test]
        public void QueuedUnitsShareRallyAreaWithoutBeingCagedByFirstArrival()
        {
            var recruit = Definition("recruit", 300, 3200, false);
            recruit.TrainTicks = 100; // The first arrival settles before the next recruit is produced.
            var world = new World(new GameDefinition
            {
                BasePopulationCapacity = 10,
                Units = new[] { recruit },
                Buildings = new[] { new BuildingDefinition
                {
                    Id = "producer", WidthCells = 1, DepthCells = 1, TrainableUnitIds = new[] { "recruit" }
                } }
            }, new MapDefinition
            {
                Id = "shared-rally-test", WidthCells = 24, HeightCells = 16,
                BuildingSpawns = new[] { new BuildingSpawnDefinition
                {
                    Id = 100, OwnerId = 1, DefinitionId = "producer", Position = new SimPoint(6500, 6500)
                } }
            });
            var rally = new SimPoint(14500, 6500);
            Accepted(world.Submit(new SetRallyCommand(1, 100, rally)));
            for (int i = 0; i < 3; i++) Accepted(world.Submit(new TrainCommand(1, 100, "recruit")));
            UnitState first = null;
            SimPoint firstArrival = default;
            bool settledBeforeSecondSpawn = false;
            for (int tick = 0; tick < World.TickRate * 120; tick++)
            {
                Step(world);
                if (world.Units.Count == 1)
                {
                    first = world.Units[0];
                    if (first.Order == UnitOrder.Idle && DistanceSquared(first.Position, rally) <= Square(2000))
                    { settledBeforeSecondSpawn = true; firstArrival = first.Position; }
                }
                bool allIdle = world.Units.Count == 3;
                foreach (var unit in world.Units) allIdle &= unit.Order == UnitOrder.Idle;
                if (allIdle) break;
            }
            Assert.IsTrue(settledBeforeSecondSpawn, "Exercise an already idle rally occupant before producing later recruits.");
            Assert.AreEqual(3, world.Units.Count);
            Assert.AreEqual(firstArrival, first.Position, "Later recruits must find space without pushing the first arrival.");
            var finalPositions = new HashSet<SimPoint>();
            foreach (var unit in world.Units)
            {
                Assert.AreEqual(UnitOrder.Idle, unit.Order, "Every queued recruit must settle at a reachable rally slot.");
                Assert.AreEqual(unit.Position, unit.Destination);
                Assert.That(DistanceSquared(unit.Position, rally), Is.LessThanOrEqualTo(Square(2000)), "The rally area must stay local.");
                Assert.IsTrue(finalPositions.Add(unit.Position), "Each recruit needs a distinct final position.");
            }
            Assert.IsTrue(world.TryGetBuilding(100, out var producer));
            Assert.AreEqual(0, producer.ProductionQueue.Count);
            Assert.IsTrue(world.TryGetPlayer(1, out var player));
            Assert.AreEqual(0, player.PopulationReserved);
        }

        [Test]
        public void FormerMoveTargetIsReissuedAfterAutomaticChaseAndTargetDeath()
        {
            var hunter = Definition("hunter", 300, 3200, false);
            hunter.Attack = new AttackDefinition { Damage = 100, RangeMillimetres = 500, AcquireRangeMillimetres = 6000, CooldownTicks = 1 };
            var world = new World(new GameDefinition
            {
                Units = new[] { hunter, Definition("passive", 300, 3200, false) }
            }, new MapDefinition
            {
                Id = "move-after-auto-chase-test", WidthCells = 32, HeightCells = 18,
                UnitSpawns = new[] { Spawn(1, 3500, 8500, "hunter"), Spawn(2, 26500, 8500, "passive", 2) }
            });
            var formerTarget = new SimPoint(12500, 8500);
            Accepted(world.Submit(new MoveCommand(1, new[] { 1 }, formerTarget)));
            Finish(world, Goals(world, 1));
            var attacker = Unit(world, 1);
            Assert.AreEqual(formerTarget, attacker.Position);
            Assert.AreEqual(0, attacker.AttackTargetId, "The enemy starts outside auto-acquisition range.");

            // Bring a passive enemy into acquisition range through an actual order; issue no Attack command.
            Accepted(world.Submit(new MoveCommand(2, new[] { 2 }, new SimPoint(17500, 8500))));
            for (int tick = 0; tick < 600 && world.TryGetUnit(2, out _); tick++) Step(world);
            Assert.IsFalse(world.TryGetUnit(2, out _), "Automatic pursuit must end by defeating the approaching enemy.");
            for (int tick = 0; tick < 20 && attacker.AttackTargetId != 0; tick++) Step(world);
            Assert.AreEqual(0, attacker.AttackTargetId);
            Assert.That(DistanceSquared(attacker.Position, formerTarget), Is.GreaterThan(Square(1000)), "The chase must actually leave the former movement goal.");

            Accepted(world.Submit(new MoveCommand(1, new[] { 1 }, formerTarget)));
            Assert.AreEqual(UnitOrder.Moving, attacker.Order, "A stale group identity must not turn this order into a successful no-op.");
            Assert.AreEqual(formerTarget, attacker.Destination);
            Finish(world, Goals(world, 1));
            Assert.AreEqual(formerTarget, attacker.Position);
        }

        private static World Create(UnitSpawnDefinition[] spawns, int speed = 3200, GridCell[] blocked = null)
        {
            var definition = new GameDefinition
            {
                BasePopulationCapacity = 100,
                Units = new[]
                {
                    Definition("mover", 300, speed, true),
                    Definition("small", 200, 1600, true),
                    Definition("large", 450, 4800, true),
                    new UnitDefinition
                    {
                        Id = "demolisher", MaxHealth = 100, RadiusMillimetres = 300, MoveSpeedMillimetresPerSecond = speed,
                        Attack = new AttackDefinition { Damage = 1000, RangeMillimetres = 30000, AcquireRangeMillimetres = 30000, CooldownTicks = 10 }
                    }
                },
                Buildings = new[] { new BuildingDefinition { Id = "barrier", MaxHealth = 100, WidthCells = 2, DepthCells = 2, BuildTicks = 20 } }
            };
            return new World(definition, new MapDefinition
            {
                Id = "group-movement-test", WidthCells = 32, HeightCells = 24, CellSizeMillimetres = 1000,
                UnitSpawns = spawns, BlockedCells = blocked ?? Array.Empty<GridCell>()
            });
        }

        private static UnitDefinition Definition(string id, int radius, int speed, bool worker)
            => new UnitDefinition { Id = id, RadiusMillimetres = radius, MoveSpeedMillimetresPerSecond = speed, IsWorker = worker };

        private static UnitSpawnDefinition Spawn(int id, int x, int z, string definition = "mover", int owner = 1)
            => new UnitSpawnDefinition { Id = id, OwnerId = owner, DefinitionId = definition, Position = new SimPoint(x, z) };

        private static UnitState Unit(World world, int id)
        {
            Assert.IsTrue(world.TryGetUnit(id, out var unit), "Expected unit " + id);
            return unit;
        }

        private static void Accepted(CommandResult result) => Assert.IsTrue(result.Accepted, result.Message);

        private static Dictionary<int, SimPoint> Goals(World world, params int[] ids)
        {
            var goals = new Dictionary<int, SimPoint>();
            foreach (int id in ids) goals.Add(id, Unit(world, id).Destination);
            return goals;
        }

        private static bool Settled(World world, Dictionary<int, SimPoint> goals)
        {
            foreach (var goal in goals)
                if (!world.TryGetUnit(goal.Key, out var unit) || unit.Position != goal.Value || unit.Order != UnitOrder.Idle) return false;
            return true;
        }

        private static void Finish(World world, Dictionary<int, SimPoint> goals, Action afterTick = null)
        {
            AssertSafe(world);
            for (int tick = 0; tick < World.TickRate * 120 && !Settled(world, goals); tick++)
            {
                Step(world);
                afterTick?.Invoke();
            }
            foreach (var goal in goals)
            {
                Assert.AreEqual(goal.Value, Unit(world, goal.Key).Position, "Unit " + goal.Key + " must reach its originally assigned goal.");
                Assert.AreEqual(UnitOrder.Idle, Unit(world, goal.Key).Order, "Arrival must settle the movement order.");
            }
        }

        private static void Step(World world) { world.Tick(); AssertSafe(world); }

        // Independent analytic checks: radius tests use continuous circles and segments, not navigation cells.
        // Tangency is legal; epsilon is only floating-point squared-distance error, not a millimetre overlap allowance.
        internal static void AssertSafe(World world)
        {
            const double epsilon = .000001;
            int cell = world.Map.CellSizeMillimetres;
            foreach (var unit in world.Units)
            {
                int r = unit.RadiusMillimetres;
                var p = unit.Position;
                if (p.X < r || p.Z < r || p.X > world.Map.WidthCells * cell - r || p.Z > world.Map.HeightCells * cell - r)
                    Assert.Fail("Circle outside map at tick " + world.TickIndex + ", unit " + unit.Id);
                foreach (var definition in world.Definition.Units)
                    if (definition.Id == unit.DefinitionId)
                    {
                        // Whole-millimetre coordinates and fractional speed accumulation need at most one extra rounding millimetre.
                        double limit = Math.Ceiling(definition.MoveSpeedMillimetresPerSecond / (double)World.TickRate) + 1;
                        if (DistanceSquared(unit.PreviousPosition, p) > limit * limit + epsilon)
                            Assert.Fail("Speed bound exceeded at tick " + world.TickIndex + ", unit " + unit.Id);
                        break;
                    }
                foreach (var obstacle in world.Map.BlockedCells)
                    ClearRectangle(world, unit, obstacle.X * cell, obstacle.Z * cell, (obstacle.X + 1) * cell, (obstacle.Z + 1) * cell);
                foreach (var building in world.Buildings)
                {
                    double halfWidth = building.WidthCells * cell / 2.0, halfDepth = building.DepthCells * cell / 2.0;
                    ClearRectangle(world, unit, building.Position.X - halfWidth, building.Position.Z - halfDepth,
                        building.Position.X + halfWidth, building.Position.Z + halfDepth);
                }
                foreach (var resource in world.Resources)
                    if (resource.RemainingAmount > 0)
                    {
                        int x = resource.Position.X / cell, z = resource.Position.Z / cell;
                        ClearRectangle(world, unit, x * cell, z * cell, (x + 1) * cell, (z + 1) * cell);
                    }
            }
            for (int i = 0; i < world.Units.Count; i++)
            for (int j = i + 1; j < world.Units.Count; j++)
            {
                var a = world.Units[i]; var b = world.Units[j];
                double x = (double)a.PreviousPosition.X - b.PreviousPosition.X;
                double z = (double)a.PreviousPosition.Z - b.PreviousPosition.Z;
                double dx = (double)a.Position.X - a.PreviousPosition.X - b.Position.X + b.PreviousPosition.X;
                double dz = (double)a.Position.Z - a.PreviousPosition.Z - b.Position.Z + b.PreviousPosition.Z;
                double denominator = dx * dx + dz * dz;
                double t = denominator == 0 ? 0 : Math.Max(0, Math.Min(1, -(x * dx + z * dz) / denominator));
                double separationSquared = Square(x + dx * t) + Square(z + dz * t);
                if (separationSquared + epsilon < Square(a.RadiusMillimetres + b.RadiusMillimetres))
                    Assert.Fail("Swept circles overlap at tick " + world.TickIndex + ": units " + a.Id + " and " + b.Id +
                        ", closest distance " + Math.Sqrt(separationSquared).ToString("F6") + " mm.");
            }
        }

        private static void ClearRectangle(World world, UnitState unit, double left, double bottom, double right, double top)
        {
            double distance = SegmentRectangleDistanceSquared(unit.PreviousPosition, unit.Position, left, bottom, right, top);
            if (distance + .000001 < Square(unit.RadiusMillimetres))
                Assert.Fail("Swept circle cuts static footprint at tick " + world.TickIndex + ", unit " + unit.Id +
                    ", rectangle (" + left + "," + bottom + ")-(" + right + "," + top + ").");
        }

        private static double SegmentRectangleDistanceSquared(SimPoint a, SimPoint b, double left, double bottom, double right, double top)
        {
            double t0 = 0, t1 = 1;
            if (ClipAxis(a.X, b.X - a.X, left, right, ref t0, ref t1) &&
                ClipAxis(a.Z, b.Z - a.Z, bottom, top, ref t0, ref t1)) return 0;
            double distance = Math.Min(PointRectangleDistanceSquared(a, left, bottom, right, top),
                PointRectangleDistanceSquared(b, left, bottom, right, top));
            distance = Math.Min(distance, PointSegmentDistanceSquared(left, bottom, a, b));
            distance = Math.Min(distance, PointSegmentDistanceSquared(left, top, a, b));
            distance = Math.Min(distance, PointSegmentDistanceSquared(right, bottom, a, b));
            return Math.Min(distance, PointSegmentDistanceSquared(right, top, a, b));
        }

        private static bool ClipAxis(double origin, double direction, double minimum, double maximum, ref double t0, ref double t1)
        {
            if (direction == 0) return origin >= minimum && origin <= maximum;
            double a = (minimum - origin) / direction, b = (maximum - origin) / direction;
            if (a > b) { double swap = a; a = b; b = swap; }
            t0 = Math.Max(t0, a); t1 = Math.Min(t1, b);
            return t0 <= t1;
        }

        private static double PointRectangleDistanceSquared(SimPoint p, double left, double bottom, double right, double top)
            => Square(p.X - Math.Max(left, Math.Min(right, p.X))) + Square(p.Z - Math.Max(bottom, Math.Min(top, p.Z)));

        private static double PointSegmentDistanceSquared(double x, double z, SimPoint a, SimPoint b)
        {
            double dx = (double)b.X - a.X, dz = (double)b.Z - a.Z, length = dx * dx + dz * dz;
            double t = length == 0 ? 0 : Math.Max(0, Math.Min(1, ((x - a.X) * dx + (z - a.Z) * dz) / length));
            return Square(x - a.X - t * dx) + Square(z - a.Z - t * dz);
        }

        private static double DistanceSquared(SimPoint a, SimPoint b) => Square((double)a.X - b.X) + Square((double)a.Z - b.Z);
        private static double Square(double value) => value * value;
    }
}
