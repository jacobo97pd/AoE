using System;
using System.Collections.Generic;
using Emberfield.Simulation;
using NUnit.Framework;

namespace Emberfield.Tests.EditMode
{
    /// <summary>Walls and gates turned north-south, and walls ordered as a whole run of stretches.</summary>
    public sealed class WallRunTests
    {
        private static readonly ResourceAmount WallCost = new ResourceAmount(0, 10, 0, 40);

        private static GameDefinition Definitions(ResourceAmount stock) => new GameDefinition
        {
            BasePopulationCapacity = 50,
            StartingResources = stock,
            Units = new[]
            {
                new UnitDefinition { Id = "worker", IsWorker = true, RadiusMillimetres = 200, MoveSpeedMillimetresPerSecond = 5000 },
                new UnitDefinition { Id = "soldier", MaxHealth = 200, Tags = CombatTags.Infantry,
                    Attack = new AttackDefinition { Damage = 20, RangeMillimetres = 1000, AcquireRangeMillimetres = 1500, CooldownTicks = 20 } },
                new UnitDefinition { Id = "ladder", Tags = CombatTags.Siege, SiegeEquipment = SiegeEquipmentKind.Ladder }
            },
            Buildings = new[]
            {
                new BuildingDefinition { Id = "wall", WidthCells = 3, DepthCells = 1, IsWall = true, MaxHealth = 500, WallCapacity = 4, BuildTicks = 20, Cost = WallCost },
                new BuildingDefinition { Id = "gate", WidthCells = 2, DepthCells = 1, IsGate = true, BuildTicks = 20 },
                new BuildingDefinition { Id = "shed", WidthCells = 2, DepthCells = 1, BuildTicks = 20 },
                new BuildingDefinition { Id = "hearth", WidthCells = 1, DepthCells = 1 }
            },
            Resources = new[] { new ResourceDefinition { Id = "stone", Kind = ResourceKind.Stone } }
        };
        private static readonly ResourceAmount Plenty = new ResourceAmount(1000, 1000, 1000, 1000);

        private static UnitSpawnDefinition U(int id, int owner, string definition, int x, int z) =>
            new UnitSpawnDefinition { Id = id, OwnerId = owner, DefinitionId = definition, Position = new SimPoint(x, z) };
        private static BuildingSpawnDefinition B(int id, int owner, string definition, int x, int z, bool turned = false) =>
            new BuildingSpawnDefinition { Id = id, OwnerId = owner, DefinitionId = definition, Position = new SimPoint(x, z), Turned = turned };
        private static World Field(ResourceAmount stock, params UnitSpawnDefinition[] units) =>
            new World(Definitions(stock), new MapDefinition { Id = "wall-run-field", WidthCells = 32, HeightCells = 24, UnitSpawns = units });
        private static World Field(params UnitSpawnDefinition[] units) => Field(Plenty, units);
        private static void Accepted(CommandResult result) => Assert.That(result.Accepted, Is.True, result.Message);
        private static void Tick(World world, int ticks) { for (int i = 0; i < ticks; i++) world.Tick(); }
        private static UnitState Unit(World world, int id) { Assert.That(world.TryGetUnit(id, out var unit), Is.True); return unit; }
        private static SimPoint Cell(int x, int z) => new SimPoint(x * 1000 + 500, z * 1000 + 500);
        private static List<BuildSite> Run(World world, params GridCell[] corners)
        {
            var sites = new List<BuildSite>();
            WallRunLayout.Layout(world.Definition.Buildings[0], world.Map.CellSizeMillimetres, corners, sites);
            return sites;
        }

        // The cells a site covers, south-west first.
        private static IEnumerable<GridCell> Cells(BuildingDefinition definition, BuildSite site)
        {
            int width = BuildingFootprints.WidthCells(definition, site.Turned), depth = BuildingFootprints.DepthCells(definition, site.Turned);
            int left = (site.Position.X - width * 500) / 1000, bottom = (site.Position.Z - depth * 500) / 1000;
            for (int z = bottom; z < bottom + depth; z++) for (int x = left; x < left + width; x++) yield return new GridCell(x, z);
        }

        [Test]
        public void TurnedWallsAndGatesSwapTheirFootprintAndOtherBuildingsCannotTurn()
        {
            var world = Field(U(1, 1, "worker", 2500, 2500));
            // Cells (6, 5)-(6, 7): the turned wall's centre sits half a cell in from the west edge and a cell and a half up.
            var site = new SimPoint(6500, 6500);
            Accepted(world.ValidatePlacement(1, new[] { 1 }, "wall", site, true));
            var turnedHouse = world.ValidatePlacement(1, new[] { 1 }, "shed", new SimPoint(6500, 6000), true);
            Assert.That(turnedHouse.Accepted, Is.False); Assert.That(turnedHouse.Reason, Is.EqualTo(CommandRejection.InvalidPlacement));
            Assert.That(world.ValidatePlacement(1, new[] { 1 }, "wall", new SimPoint(6500, 6000), true).Reason, Is.EqualTo(CommandRejection.InvalidPlacement),
                "A turned footprint aligns its own sides to the grid.");
            var result = world.Submit(new BuildCommand(1, new[] { 1 }, "wall", site, true)); Accepted(result);
            Assert.That(world.TryGetBuilding(result.EntityId, out var wall), Is.True);
            Assert.That(wall.IsTurned, Is.True); Assert.That(wall.WidthCells, Is.EqualTo(1)); Assert.That(wall.DepthCells, Is.EqualTo(3));
            for (int z = 5; z <= 7; z++) Assert.That(world.IsWalkable(Cell(6, z)), Is.False);
            Assert.That(world.IsWalkable(Cell(5, 6)), Is.True); Assert.That(world.IsWalkable(Cell(7, 6)), Is.True);
            Assert.That(world.IsWalkable(Cell(6, 4)), Is.True); Assert.That(world.IsWalkable(Cell(6, 8)), Is.True);

            var gate = world.Submit(new BuildCommand(1, new[] { 1 }, "gate", new SimPoint(10500, 6000), true)); Accepted(gate);
            Assert.That(world.TryGetBuilding(gate.EntityId, out var turnedGate), Is.True);
            Assert.That(turnedGate.WidthCells, Is.EqualTo(1)); Assert.That(turnedGate.DepthCells, Is.EqualTo(2)); Assert.That(turnedGate.IsTurned, Is.True);
            var plain = world.Submit(new BuildCommand(1, new[] { 1 }, "wall", new SimPoint(15500, 10500))); Accepted(plain);
            Assert.That(world.TryGetBuilding(plain.EntityId, out var plainWall), Is.True);
            Assert.That(plainWall.IsTurned, Is.False); Assert.That(plainWall.WidthCells, Is.EqualTo(3)); Assert.That(plainWall.DepthCells, Is.EqualTo(1));
        }

        [Test]
        public void MapsCanStartWithTurnedWallsButNotTurnedHouses()
        {
            var world = new World(Definitions(Plenty), new MapDefinition { WidthCells = 16, HeightCells = 16, BuildingSpawns = new[] { B(100, 1, "wall", 4500, 6500, true) } });
            Assert.That(world.TryGetBuilding(100, out var wall), Is.True); Assert.That(wall.IsTurned, Is.True);
            for (int z = 5; z <= 7; z++) Assert.That(world.IsWalkable(Cell(4, z)), Is.False);
            Assert.That(world.IsWalkable(Cell(3, 6)), Is.True);
            Assert.Throws<ArgumentException>(() => new World(Definitions(Plenty), new MapDefinition { WidthCells = 16, HeightCells = 16,
                BuildingSpawns = new[] { B(100, 1, "shed", 4500, 6000, true) } }));
        }

        [Test]
        public void ARectangleDrawnCornerByCornerClosesWithoutGapsOrOverlapsAndEncloses()
        {
            var world = Field(U(1, 1, "worker", 2500, 2500), U(2, 1, "worker", 3500, 2500), U(3, 1, "soldier", 9500, 9500));
            var sites = new List<BuildSite>();
            var corners = new[] { new GridCell(6, 6), new GridCell(14, 6), new GridCell(14, 12), new GridCell(6, 12), new GridCell(6, 6) };
            bool closed = WallRunLayout.Layout(world.Definition.Buildings[0], 1000, corners, sites);
            Assert.That(closed, Is.True);
            Assert.That(sites.Count, Is.EqualTo(10), "Three stretches east, two north, three west and two south.");
            var covered = new HashSet<GridCell>();
            foreach (var site in sites) foreach (var cell in Cells(world.Definition.Buildings[0], site)) Assert.That(covered.Add(cell), Is.True, "Stretches overlap at " + cell.X + "," + cell.Z);
            Assert.That(covered.Count, Is.EqualTo(30));
            // A closed ring: every covered cell touches exactly two others.
            foreach (var cell in covered)
            {
                int neighbours = 0;
                foreach (var other in new[] { new GridCell(cell.X + 1, cell.Z), new GridCell(cell.X - 1, cell.Z), new GridCell(cell.X, cell.Z + 1), new GridCell(cell.X, cell.Z - 1) })
                    if (covered.Contains(other)) neighbours++;
                Assert.That(neighbours, Is.EqualTo(2), "Gap or spur at " + cell.X + "," + cell.Z);
            }
            int turned = 0; foreach (var site in sites) if (site.Turned) turned++;
            Assert.That(turned, Is.EqualTo(4), "The north-south legs are made of turned stretches.");

            var preview = world.PreviewBuildRun(new BuildRunCommand(1, new[] { 1, 2 }, "wall", sites.ToArray()));
            Accepted(preview.Result); Assert.That(preview.AcceptedCount, Is.EqualTo(10));
            Assert.That(preview.Cost, Is.EqualTo(new ResourceAmount(0, 100, 0, 400)));
            var run = world.Submit(new BuildRunCommand(1, new[] { 1, 2 }, "wall", sites.ToArray())); Accepted(run);
            Assert.That(world.Buildings.Count, Is.EqualTo(10));
            Assert.That(world.Players[0].Resources, Is.EqualTo(new ResourceAmount(1000, 900, 1000, 600)));
            foreach (var cell in covered) Assert.That(world.IsWalkable(Cell(cell.X, cell.Z)), Is.False);
            // Foundations already block: the soldier inside cannot walk out.
            world.Submit(new MoveCommand(1, new[] { 3 }, new SimPoint(20500, 9500)));
            Tick(world, 200);
            var inside = Unit(world, 3).Position;
            Assert.That(inside.X > 6000 && inside.X < 14000 && inside.Z > 7000 && inside.Z < 12000, Is.True, "Escaped the enclosure to " + inside);
        }

        [Test]
        public void TheWorkersLayEveryStretchInOrderAndNeverIdleWhileOneRemains()
        {
            var world = Field(U(1, 1, "worker", 2500, 2500), U(2, 1, "worker", 3500, 2500));
            var sites = Run(world, new GridCell(6, 4), new GridCell(17, 4));
            Assert.That(sites.Count, Is.EqualTo(4));
            WallRunLayout.NearestFirst(sites, Unit(world, 1).Position, false);
            Assert.That(sites[0].Position, Is.EqualTo(new SimPoint(7500, 4500)), "The nearer end comes first.");
            var run = world.Submit(new BuildRunCommand(1, new[] { 1, 2 }, "wall", sites.ToArray())); Accepted(run);
            var ids = new List<int>(); foreach (var building in world.Buildings) ids.Add(building.Id);
            Assert.That(ids.Count, Is.EqualTo(4)); Assert.That(ids[0], Is.EqualTo(run.EntityId));
            var firstPosition = sites[0].Position;
            Assert.That(world.TryGetBuilding(ids[0], out var first), Is.True); Assert.That(first.Position, Is.EqualTo(firstPosition));
            foreach (int id in new[] { 1, 2 }) Assert.That(Unit(world, id).TargetBuildingId, Is.EqualTo(ids[0]));
            int finishedAt = -1;
            for (int tick = 0; tick < 3000 && finishedAt < 0; tick++)
            {
                world.Tick();
                bool remaining = false;
                for (int k = 0; k < ids.Count; k++)
                {
                    Assert.That(world.TryGetBuilding(ids[k], out var stretch), Is.True);
                    if (!stretch.IsComplete) remaining = true;
                    if (k > 0 && stretch.BuildProgressTicks > 0)
                    {
                        Assert.That(world.TryGetBuilding(ids[k - 1], out var previous), Is.True);
                        Assert.That(previous.IsComplete, Is.True, "Stretch " + k + " started before stretch " + (k - 1) + " was finished.");
                    }
                }
                if (!remaining) { finishedAt = tick; break; }
                foreach (int id in new[] { 1, 2 })
                    Assert.That(Unit(world, id).WorkerTask, Is.EqualTo(WorkerTask.MovingToConstruction).Or.EqualTo(WorkerTask.Constructing),
                        "Worker " + id + " idled at tick " + tick + " with stretches left.");
            }
            Assert.That(finishedAt, Is.GreaterThan(0), "The run was never finished.");
            world.Tick();
            foreach (int id in new[] { 1, 2 }) Assert.That(Unit(world, id).WorkerTask, Is.EqualTo(WorkerTask.None));
        }

        [Test]
        public void ThePreviewJudgesEachStretchLikeTheOrderAndChangesNothing()
        {
            var world = new World(Definitions(new ResourceAmount(0, 1000, 0, 100)), new MapDefinition { Id = "wall-run-field", WidthCells = 32, HeightCells = 24,
                UnitSpawns = new[] { U(1, 1, "worker", 2500, 2500), U(2, 1, "worker", 3500, 2500), U(3, 1, "soldier", 19500, 4500) },
                ResourceSpawns = new[] { new ResourceSpawnDefinition { Id = 50, DefinitionId = "stone", Position = Cell(10, 4) } } });
            // Stretches over cells 6-8, 9-11 (a stone), 12-14, 15-17, 18-20 (the soldier) and one past the map's east edge.
            var sites = new[] { new BuildSite(new SimPoint(7500, 4500), false), new BuildSite(new SimPoint(10500, 4500), false),
                new BuildSite(new SimPoint(13500, 4500), false), new BuildSite(new SimPoint(16500, 4500), false),
                new BuildSite(new SimPoint(19500, 4500), false), new BuildSite(new SimPoint(31500, 4500), false) };
            var command = new BuildRunCommand(1, new[] { 1, 2 }, "wall", sites);
            int buildings = world.Buildings.Count; var stock = world.Players[0].Resources;
            var preview = world.PreviewBuildRun(command);
            Assert.That(world.Buildings.Count, Is.EqualTo(buildings)); Assert.That(world.Players[0].Resources, Is.EqualTo(stock));
            Assert.That(world.IsWalkable(Cell(7, 4)), Is.True); Assert.That(Unit(world, 1).WorkerTask, Is.EqualTo(WorkerTask.None));
            Accepted(preview.Result);
            Assert.That(preview.Sites.Count, Is.EqualTo(6));
            Assert.That(preview.Sites[0].Accepted, Is.True);
            Assert.That(preview.Sites[1].Reason, Is.EqualTo(CommandRejection.DestinationBlocked));
            Assert.That(preview.Sites[2].Accepted, Is.True);
            Assert.That(preview.Sites[3].Reason, Is.EqualTo(CommandRejection.InsufficientResources), "Two stretches exhaust 100 stone.");
            Assert.That(preview.Sites[4].Reason, Is.EqualTo(CommandRejection.UnitObstruction));
            Assert.That(preview.Sites[5].Reason, Is.EqualTo(CommandRejection.InvalidPlacement));
            Assert.That(preview.AcceptedCount, Is.EqualTo(2)); Assert.That(preview.Cost, Is.EqualTo(new ResourceAmount(0, 20, 0, 80)));
            var reused = world.PreviewBuildRun(command, preview);
            Assert.That(reused, Is.SameAs(preview)); Assert.That(reused.Sites.Count, Is.EqualTo(6));

            var result = world.Submit(command); Accepted(result);
            Assert.That(world.Buildings.Count, Is.EqualTo(2));
            Assert.That(world.Buildings[0].Position, Is.EqualTo(sites[0].Position)); Assert.That(world.Buildings[1].Position, Is.EqualTo(sites[2].Position));
            Assert.That(world.Players[0].Resources, Is.EqualTo(new ResourceAmount(0, 980, 0, 20)));
            var nothing = world.Submit(new BuildRunCommand(1, new[] { 1 }, "wall", new[] { sites[3] }));
            Assert.That(nothing.Accepted, Is.False); Assert.That(nothing.Reason, Is.EqualTo(CommandRejection.InsufficientResources));
            Assert.That(world.Submit(new BuildRunCommand(1, new[] { 1 }, "gate", new[] { sites[3] })).Reason, Is.EqualTo(CommandRejection.InvalidCommand),
                "Only walls are laid in runs.");
            Assert.That(world.Submit(new BuildRunCommand(1, new[] { 1 }, "wall", Array.Empty<BuildSite>())).Accepted, Is.False);
            Assert.That(world.Submit(new BuildRunCommand(1, new[] { 1 }, "wall", new BuildSite[BuildRunCommand.MaximumSites + 1])).Accepted, Is.False);
        }

        [Test]
        public void ARunThatCrossesItselfMarksTheCrossingRedAndNeverThrows()
        {
            var world = Field(U(1, 1, "worker", 2500, 2500));
            // East along row 4, north, back west, then south across the first leg.
            var sites = Run(world, new GridCell(6, 4), new GridCell(14, 4), new GridCell(14, 8), new GridCell(10, 8), new GridCell(10, 2));
            Assert.That(sites.Count, Is.EqualTo(7));
            sites.Add(sites[0]); sites.Add(sites[2]);
            var command = new BuildRunCommand(1, new[] { 1 }, "wall", sites.ToArray());
            BuildRunPreview preview = null;
            Assert.DoesNotThrow(() => preview = world.PreviewBuildRun(command));
            Accepted(preview.Result);
            for (int i = 0; i < 5; i++) Assert.That(preview.Sites[i].Accepted, Is.True, "Stretch " + i + ": " + preview.Sites[i].Message);
            Assert.That(preview.Sites[5].Reason, Is.EqualTo(CommandRejection.DestinationBlocked), "The southward leg crosses the first leg here.");
            Assert.That(preview.Sites[5].Message, Does.Contain("another stretch"));
            Assert.That(preview.Sites[6].Accepted, Is.True);
            Assert.That(preview.Sites[7].Reason, Is.EqualTo(CommandRejection.DestinationBlocked)); Assert.That(preview.Sites[8].Reason, Is.EqualTo(CommandRejection.DestinationBlocked));
            CommandResult result = default;
            Assert.DoesNotThrow(() => result = world.Submit(command));
            Accepted(result); Assert.That(world.Buildings.Count, Is.EqualTo(6));
        }

        [Test]
        public void AWorkerCutOffFromTheRestOfTheRunIdlesAndTheOtherFinishesIt()
        {
            // A river down column 12 splits the field; each worker reaches only the stretch on its own bank.
            var river = new List<GridCell>(); for (int z = 0; z < 16; z++) river.Add(new GridCell(12, z));
            var world = new World(Definitions(Plenty), new MapDefinition { Id = "wall-run-river", WidthCells = 24, HeightCells = 16, BlockedCells = river.ToArray(),
                UnitSpawns = new[] { U(1, 1, "worker", 5500, 2500), U(2, 1, "worker", 18500, 2500) } });
            var west = new BuildSite(new SimPoint(7500, 6500), false); var east = new BuildSite(new SimPoint(16500, 6500), false);
            var result = world.Submit(new BuildRunCommand(1, new[] { 1, 2 }, "wall", new[] { west, east })); Accepted(result);
            int westId = result.EntityId, eastId = westId + 1;
            Assert.That(Unit(world, 1).TargetBuildingId, Is.EqualTo(westId)); Assert.That(Unit(world, 2).TargetBuildingId, Is.EqualTo(eastId));
            Assert.That(world.TryGetBuilding(westId, out var westWall), Is.True); Assert.That(world.TryGetBuilding(eastId, out var eastWall), Is.True);
            for (int i = 0; i < 2000 && !westWall.IsComplete; i++) world.Tick();
            Assert.That(westWall.IsComplete, Is.True);
            for (int i = 0; i < 100; i++)
            {
                world.Tick();
                Assert.That(Unit(world, 1).WorkerTask, Is.EqualTo(WorkerTask.None), "The west worker must not wait on the far bank's stretch.");
                Assert.That(Unit(world, 1).TargetBuildingId, Is.Zero);
            }
            for (int i = 0; i < 2000 && !eastWall.IsComplete; i++) world.Tick();
            Assert.That(eastWall.IsComplete, Is.True);
            var stranded = world.Submit(new BuildRunCommand(1, new[] { 1, 2 }, "wall", new[] { new BuildSite(new SimPoint(7500, 10500), false) }));
            Assert.That(stranded.Accepted, Is.False, "Every builder must reach some stretch of the run.");
            Assert.That(stranded.Reason, Is.EqualTo(CommandRejection.NoPath));
        }

        [Test]
        public void StretchesInTheFogAreRejectedOneByOne()
        {
            var world = new World(Definitions(Plenty), new MapDefinition { Id = "wall-run-fog", WidthCells = 40, HeightCells = 24,
                UnitSpawns = new[] { U(1, 1, "worker", 2500, 4500), U(2, 2, "worker", 37500, 20500) },
                BuildingSpawns = new[] { B(100, 1, "hearth", 1500, 1500), B(101, 2, "hearth", 38500, 22500) },
                OfflineMatch = new OfflineMatchDefinition { Enabled = true, VisionUnitCells = 6, VisionBuildingCells = 3 } });
            world.Tick();
            var sites = Run(world, new GridCell(3, 6), new GridCell(20, 6));
            var preview = world.PreviewBuildRun(new BuildRunCommand(1, new[] { 1 }, "wall", sites.ToArray()));
            Accepted(preview.Result);
            Assert.That(preview.Sites[0].Accepted, Is.True);
            Assert.That(preview.Sites[sites.Count - 1].Reason, Is.EqualTo(CommandRejection.TargetNotVisible));
            var result = world.Submit(new BuildRunCommand(1, new[] { 1 }, "wall", sites.ToArray())); Accepted(result);
            Assert.That(world.Buildings.Count, Is.EqualTo(2 + preview.AcceptedCount));
        }

        [Test]
        public void TwoWorldsFedTheSameRunsStayIdentical()
        {
            World Play()
            {
                var world = new World(Definitions(new ResourceAmount(0, 1000, 0, 200)), new MapDefinition { Id = "wall-run-field", WidthCells = 32, HeightCells = 24,
                    UnitSpawns = new[] { U(1, 1, "worker", 2500, 2500), U(2, 1, "worker", 3500, 2500), U(3, 1, "worker", 2500, 3500) },
                    ResourceSpawns = new[] { new ResourceSpawnDefinition { Id = 50, DefinitionId = "stone", Position = Cell(10, 4) } } });
                var sites = Run(world, new GridCell(6, 4), new GridCell(20, 4), new GridCell(20, 12));
                Accepted(world.Submit(new BuildRunCommand(1, new[] { 1, 2 }, "wall", sites.ToArray())));
                Tick(world, 150);
                Accepted(world.Submit(new BuildCommand(1, new[] { 3 }, "gate", new SimPoint(4500, 10000), true)));
                Tick(world, 600);
                return world;
            }
            var a = Play(); var b = Play();
            Assert.That(a.Buildings.Count, Is.EqualTo(b.Buildings.Count)); Assert.That(a.Buildings.Count, Is.GreaterThan(3));
            for (int i = 0; i < a.Buildings.Count; i++)
            {
                Assert.That(a.Buildings[i].Id, Is.EqualTo(b.Buildings[i].Id)); Assert.That(a.Buildings[i].Position, Is.EqualTo(b.Buildings[i].Position));
                Assert.That(a.Buildings[i].IsTurned, Is.EqualTo(b.Buildings[i].IsTurned)); Assert.That(a.Buildings[i].BuildProgressTicks, Is.EqualTo(b.Buildings[i].BuildProgressTicks));
            }
            for (int i = 0; i < a.Units.Count; i++)
            {
                Assert.That(a.Units[i].Position, Is.EqualTo(b.Units[i].Position)); Assert.That(a.Units[i].WorkerTask, Is.EqualTo(b.Units[i].WorkerTask));
                Assert.That(a.Units[i].TargetBuildingId, Is.EqualTo(b.Units[i].TargetBuildingId));
            }
            Assert.That(a.Players[0].Resources, Is.EqualTo(b.Players[0].Resources));
            // The same journal replayed into a fresh start lands on the same field.
            var replay = new World(a.Definition, a.Map);
            int entry = 0;
            while (replay.TickIndex < a.TickIndex)
            {
                while (entry < a.Journal.Entries.Count && a.Journal.Entries[entry].Tick == replay.TickIndex) Accepted(replay.Submit(a.Journal.Entries[entry++].Command));
                replay.Tick();
            }
            Assert.That(replay.Buildings.Count, Is.EqualTo(a.Buildings.Count));
            for (int i = 0; i < a.Units.Count; i++) Assert.That(replay.Units[i].Position, Is.EqualTo(a.Units[i].Position));
        }

        [Test]
        public void SoldiersClimbANorthSouthWallFromItsSideAndStandAlongIt()
        {
            // An enemy wall turned north-south over cells (7, 5)-(7, 7).
            var world = new World(Definitions(Plenty), new MapDefinition { Id = "wall-run-siege", WidthCells = 20, HeightCells = 16,
                BuildingSpawns = new[] { B(100, 2, "wall", 7500, 6500, true) },
                UnitSpawns = new[] { U(1, 1, "soldier", 5500, 5500), U(2, 1, "soldier", 5500, 6500), U(3, 1, "soldier", 5500, 7500),
                    U(4, 1, "ladder", 6200, 4300), U(5, 1, "ladder", 3500, 6500) } });
            var far = world.Submit(new BoardWallCommand(1, new[] { 1 }, 100, 5));
            Assert.That(far.Reason, Is.EqualTo(CommandRejection.OutOfRange), "A ladder two cells off the west face is not against the wall.");
            Accepted(world.Submit(new BoardWallCommand(1, new[] { 1, 2, 3 }, 100, 4)));
            Tick(world, 100);
            Assert.That(world.TryGetBuilding(100, out var wall), Is.True);
            var decks = new HashSet<int>();
            foreach (int id in new[] { 1, 2, 3 })
            {
                var soldier = Unit(world, id);
                Assert.That(soldier.WallId, Is.EqualTo(100));
                Assert.That(soldier.Position.X, Is.EqualTo(wall.Position.X), "The deck runs along the wall's length, north-south.");
                Assert.That(soldier.Position.Z, Is.GreaterThan(5000).And.LessThan(8000));
                Assert.That(decks.Add(soldier.Position.Z), Is.True);
                Assert.That(soldier.Position, Is.EqualTo(BuildingFootprints.DeckSlot(wall, 4, decks.Count - 1, 1000)));
            }
            Accepted(world.Submit(new LeaveWallCommand(1, new[] { 1 }, new SimPoint(9000, 6500))));
            Assert.That(Unit(world, 1).WallId, Is.Zero); Assert.That(Unit(world, 1).Position.X, Is.GreaterThan(8000), "The descent lands on the east side.");
            Assert.That(world.IsWalkable(Unit(world, 1).Position), Is.True);
            Assert.That(world.Submit(new LeaveWallCommand(1, new[] { 2 }, new SimPoint(7500, 11500))).Accepted, Is.False, "Too far past the wall's north end.");
        }

        [Test]
        public void UnturnedDeckSlotsStayWhereTheyWere()
        {
            var world = new World(Definitions(Plenty), new MapDefinition { WidthCells = 16, HeightCells = 16, BuildingSpawns = new[] { B(100, 1, "wall", 7500, 6500) } });
            Assert.That(world.TryGetBuilding(100, out var wall), Is.True);
            for (int slot = 0; slot < 4; slot++)
                Assert.That(BuildingFootprints.DeckSlot(wall, 4, slot, 1000), Is.EqualTo(new SimPoint(6000 + (slot + 1) * 3000 / 5, 6500)));
        }

        [Test]
        public void ATurnedGateOpensAndClosesANorthSouthPassage()
        {
            // A cliff down column 5 with a two-cell gap at rows 6-7, filled by a gate turned north-south.
            var cliff = new List<GridCell>(); for (int z = 0; z < 14; z++) if (z != 6 && z != 7) cliff.Add(new GridCell(5, z));
            var world = new World(Definitions(Plenty), new MapDefinition { WidthCells = 14, HeightCells = 14, BlockedCells = cliff.ToArray(),
                BuildingSpawns = new[] { B(100, 1, "gate", 5500, 7000, true) }, UnitSpawns = new[] { U(1, 1, "soldier", 3500, 6500) } });
            Assert.That(world.IsWalkable(Cell(5, 6)), Is.False); Assert.That(world.IsWalkable(Cell(5, 7)), Is.False);
            Assert.That(world.Submit(new MoveCommand(1, new[] { 1 }, new SimPoint(8500, 6500))).Accepted, Is.False);
            Accepted(world.Submit(new SetGateCommand(1, 100, true)));
            Assert.That(world.IsWalkable(Cell(5, 6)), Is.True); Assert.That(world.IsWalkable(Cell(5, 7)), Is.True);
            Accepted(world.Submit(new MoveCommand(1, new[] { 1 }, new SimPoint(5500, 7500)))); Tick(world, 30);
            Assert.That(world.Submit(new SetGateCommand(1, 100, false)).Reason, Is.EqualTo(CommandRejection.UnitObstruction), "The doorway is occupied.");
            Accepted(world.Submit(new MoveCommand(1, new[] { 1 }, new SimPoint(8500, 6500)))); Tick(world, 60);
            Assert.That(Unit(world, 1).Position.X, Is.GreaterThan(6000));
            Accepted(world.Submit(new SetGateCommand(1, 100, false)));
            Assert.That(world.IsWalkable(Cell(5, 6)), Is.False); Assert.That(world.IsWalkable(Cell(5, 7)), Is.False);
        }

        [Test]
        public void ObservationsCarryTheTurnToTheReplica()
        {
            var world = new World(Definitions(Plenty), new MapDefinition { Id = "wall-run-network", WidthCells = 24, HeightCells = 16,
                UnitSpawns = new[] { U(1, 1, "worker", 5500, 4500) },
                BuildingSpawns = new[] { B(100, 1, "hearth", 1500, 1500), B(101, 2, "hearth", 22500, 14500) },
                OfflineMatch = new OfflineMatchDefinition { Enabled = true, VisionUnitCells = 4, VisionBuildingCells = 3 } });
            world.Tick();
            var built = world.Submit(new BuildCommand(1, new[] { 1 }, "wall", new SimPoint(7500, 6500), true)); Accepted(built);
            var snapshot = NetworkObservation.Export(world, 1, 1);
            var observed = Array.Find(snapshot.Buildings, b => b.Id == built.EntityId);
            Assert.That(observed.Turned, Is.True);
            Assert.That(Array.Find(snapshot.Buildings, b => b.Id == 100).Turned, Is.False);
            var replica = World.CreateNetworkReplica(world.Definition, world.Map, snapshot);
            Assert.That(replica.TryGetBuilding(built.EntityId, out var wall), Is.True);
            Assert.That(wall.IsTurned, Is.True); Assert.That(wall.WidthCells, Is.EqualTo(1)); Assert.That(wall.DepthCells, Is.EqualTo(3));
            for (int z = 5; z <= 7; z++) Assert.That(replica.IsWalkable(Cell(7, z)), Is.False);
            Assert.That(replica.IsWalkable(Cell(8, 6)), Is.True);
            var corrupt = NetworkObservation.Export(world, 1, 2);
            Array.Find(corrupt.Buildings, b => b.Id == 100).Turned = true;
            Assert.Throws<ArgumentException>(() => replica.ApplyNetworkSnapshot(corrupt), "A square hearth cannot be turned.");
            Assert.That(replica.NetworkSnapshotSequence, Is.EqualTo(1));
        }
    }
}
