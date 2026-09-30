using System;
using System.Collections;
using Emberfield.Networking;
using Emberfield.Simulation;
using NUnit.Framework;
using UnityEngine;

namespace Emberfield.Tests.Networking
{
    public sealed class NetworkCommandTests
    {
        private static IEnumerable Commands()
        {
            yield return new MoveCommand(1, new[] { 5, 8 }, new SimPoint(12500, 25500), MovementFormation.Loose);
            yield return new StopCommand(1, new[] { 5, 8 });
            yield return new GatherCommand(1, new[] { 5, 8 }, 100);
            yield return new ReturnCargoCommand(1, new[] { 5 }, 100);
            yield return new BuildCommand(1, new[] { 5 }, "shelter", new SimPoint(12500, 25500));
            yield return new BuildCommand(1, new[] { 5 }, "gate", new SimPoint(12500, 25000), true);
            yield return new BuildRunCommand(1, new[] { 5, 8 }, "wall", new[] { new BuildSite(new SimPoint(12500, 25500), false), new BuildSite(new SimPoint(13500, 27500), true) });
            yield return new ConstructCommand(1, new[] { 5 }, 100);
            yield return new TrainCommand(1, 100, "tender");
            yield return new SetRallyCommand(1, 100, new SimPoint(12500, 25500));
            yield return new AttackCommand(1, new[] { 5, 8 }, 100);
            yield return new ResearchCommand(1, 100, "advance_camp");
            yield return new SetCharterCommand(1, 100, StoreyardCharter.Logistics);
            yield return new DeployThreadkeeperCommand(1, 5, 100);
            yield return new PackOutpostCommand(1, 100);
            yield return new DeployOutpostCommand(1, 5, new SimPoint(12500, 25500));
            yield return new RepositionCommand(1, new[] { 5, 8 });
            yield return new SurrenderCommand(1);
            yield return new BoardWallCommand(1, new[] { 5, 8 }, 100, 9);
            yield return new BoardWallCommand(1, new[] { 5, 8 }, 100);
            yield return new LeaveWallCommand(1, new[] { 5, 8 }, new SimPoint(12500, 25500));
            yield return new SetGateCommand(1, 100, true);
            yield return new SetGateCommand(1, 100, false);
            yield return new EmbarkCommand(1, new[] { 5, 8 }, 100);
            yield return new DisembarkCommand(1, 100, new SimPoint(12500, 25500));
        }

        [TestCaseSource(nameof(Commands))]
        public void EveryCommandSurvivesJsonWireRoundTripAndUsesAuthenticatedPlayer(IGameCommand command)
        {
            var envelope = NetworkCommandCodec.Encode(command, 12, 300, "request_12");
            string json = JsonUtility.ToJson(envelope);
            Assert.That(json, Does.Not.Contain("PlayerId"), "The client cannot choose a player identity.");
            var wire = JsonUtility.FromJson<NetworkCommandEnvelope>(json);
            Assert.That(NetworkCommandCodec.TryDecode(wire, 2, 302, out var decoded, out var error), Is.True, error);
            Assert.That(decoded.GetType(), Is.EqualTo(command.GetType())); Assert.That(decoded.PlayerId, Is.EqualTo(2));
            Assert.That(JsonUtility.ToJson(NetworkCommandCodec.Encode(decoded, 12, 300, "request_12")), Is.EqualTo(json));
        }

        [TestCase("version")] [TestCase("zero-sequence")] [TestCase("empty-request")] [TestCase("invalid-request")]
        [TestCase("negative-tick")] [TestCase("future-tick")] [TestCase("stale-tick")] [TestCase("kind")]
        [TestCase("oversized-selection")] [TestCase("duplicate-selection")] [TestCase("negative-selection")] [TestCase("empty-selection")]
        [TestCase("negative-point")] [TestCase("oversized-point")] [TestCase("unexpected-definition")] [TestCase("unexpected-entity")]
        [TestCase("unexpected-target")] [TestCase("invalid-option")]
        public void MalformedEnvelopesAreRejectedBeforeWorldSubmission(string corruption)
        {
            var e = NetworkCommandCodec.Encode(new MoveCommand(1, new[] { 5, 8 }, new SimPoint(1000, 2000)), 1, 1300, "valid");
            switch (corruption)
            {
                case "version": e.Version++; break;
                case "zero-sequence": e.Sequence = 0; break;
                case "empty-request": e.RequestId = ""; break;
                case "invalid-request": e.RequestId = "../file"; break;
                case "negative-tick": e.IssuedTick = -1; break;
                case "future-tick": e.IssuedTick = 1341; break;
                case "stale-tick": e.IssuedTick = 99; break;
                case "kind": e.Kind = (NetworkCommandKind)99; break;
                case "oversized-selection": e.UnitIds = new int[1025]; break;
                case "duplicate-selection": e.UnitIds = new[] { 5, 5 }; break;
                case "negative-selection": e.UnitIds = new[] { -5 }; break;
                case "empty-selection": e.UnitIds = Array.Empty<int>(); break;
                case "negative-point": e.X = -1; break;
                case "oversized-point": e.Z = 1000001; break;
                case "unexpected-definition": e.DefinitionId = "hearth"; break;
                case "unexpected-entity": e.EntityId = 100; break;
                case "unexpected-target": e.TargetId = 100; break;
                case "invalid-option": e.Option = 999; break;
            }
            Assert.That(NetworkCommandCodec.TryDecode(e, 1, 1300, out var command, out var error), Is.False);
            Assert.That(command, Is.Null); Assert.That(error, Is.Not.Empty);
        }

        [Test]
        public void CursorRejectsReplayGapAndDuplicateRequestWithoutConsumingNextSequence()
        {
            var cursor = new NetworkCommandCursor(); var command = new SurrenderCommand(1);
            var first = NetworkCommandCodec.Encode(command, 1, 100, "first");
            Assert.That(cursor.TryAccept(first, 2, 100, out _, out _), Is.True);
            Assert.That(cursor.TryAccept(first, 2, 100, out _, out _), Is.False);
            Assert.That(cursor.TryAccept(NetworkCommandCodec.Encode(command, 3, 100, "third"), 2, 100, out _, out _), Is.False);
            Assert.That(cursor.TryAccept(NetworkCommandCodec.Encode(command, 2, 100, "first"), 2, 100, out _, out _), Is.False);
            Assert.That(cursor.LastSequence, Is.EqualTo(1));
            Assert.That(cursor.TryAccept(NetworkCommandCodec.Encode(command, 2, 100, "second"), 2, 100, out _, out _), Is.True);
            Assert.That(cursor.LastSequence, Is.EqualTo(2));
        }

        [Test]
        public void InvalidEnvelopeDoesNotConsumeASeatSequence()
        {
            var cursor = new NetworkCommandCursor(); var e = NetworkCommandCodec.Encode(new StopCommand(1, new[] { 1 }), 1, 0, "first");
            e.Version = 999; Assert.That(cursor.TryAccept(e, 1, 0, out _, out _), Is.False); Assert.That(cursor.LastSequence, Is.Zero);
            e.Version = NetworkCommandCodec.Version; Assert.That(cursor.TryAccept(e, 1, 0, out _, out _), Is.True);
        }

        [Test]
        public void SelectionIsCopiedAndExactBoundsRemainSupported()
        {
            var ids = new int[1024]; for (int i = 0; i < ids.Length; i++) ids[i] = i + 1;
            var e = NetworkCommandCodec.Encode(new MoveCommand(1, ids, new SimPoint(1000000, 1000000)), 1, 1200, "max");
            Assert.That(NetworkCommandCodec.TryDecode(e, 1, 2400, out var command, out _), Is.True);
            e.UnitIds[0] = 99999; Assert.That(((MoveCommand)command).UnitIds[0], Is.EqualTo(1));
            e.IssuedTick = 2440; Assert.That(NetworkCommandCodec.TryDecode(e, 1, 2400, out _, out _), Is.True);
        }

        [TestCase("move")] [TestCase("train")] [TestCase("research")] [TestCase("construct")]
        [TestCase("return")] [TestCase("threadkeeper")]
        public void GuessedEnemyAndNonexistentActorsHaveIdenticalAuthorizationFailure(string kind)
        {
            var world = GuardWorld();
            IGameCommand existing, missing;
            switch (kind)
            {
                case "move": existing = new MoveCommand(1, new[] { 2 }, new SimPoint(3000, 3000)); missing = new MoveCommand(1, new[] { 999 }, new SimPoint(3000, 3000)); break;
                case "train": existing = new TrainCommand(1, 102, "worker"); missing = new TrainCommand(1, 999, "worker"); break;
                case "research": existing = new ResearchCommand(1, 102, "technology"); missing = new ResearchCommand(1, 999, "technology"); break;
                case "construct": existing = new ConstructCommand(1, new[] { 1 }, 102); missing = new ConstructCommand(1, new[] { 1 }, 999); break;
                case "return": existing = new ReturnCargoCommand(1, new[] { 1 }, 102); missing = new ReturnCargoCommand(1, new[] { 1 }, 999); break;
                default: existing = new DeployThreadkeeperCommand(1, 1, 102); missing = new DeployThreadkeeperCommand(1, 1, 999); break;
            }
            Assert.That(NetworkCommandCodec.TryAuthorizeObservation(world, existing, out string first), Is.False);
            Assert.That(NetworkCommandCodec.TryAuthorizeObservation(world, missing, out string second), Is.False);
            Assert.That(first, Is.EqualTo(second));
        }

        [TestCase(false)] [TestCase(true)]
        public void HiddenAndNonexistentTargetsHaveIdenticalAuthorizationFailure(bool gather)
        {
            var world = GuardWorld();
            IGameCommand existing = gather ? (IGameCommand)new GatherCommand(1, new[] { 1 }, 201) : new AttackCommand(1, new[] { 1 }, 2);
            IGameCommand missing = gather ? (IGameCommand)new GatherCommand(1, new[] { 1 }, 999) : new AttackCommand(1, new[] { 1 }, 999);
            Assert.That(NetworkCommandCodec.TryAuthorizeObservation(world, existing, out string first), Is.False);
            Assert.That(NetworkCommandCodec.TryAuthorizeObservation(world, missing, out string second), Is.False);
            Assert.That(first, Is.EqualTo(second));
        }

        [Test]
        public void ObservationAuthorizationPreservesOwnedOrdersIntoUnknownTerrain()
        {
            var world = GuardWorld();
            Assert.That(NetworkCommandCodec.TryAuthorizeObservation(world, new MoveCommand(1, new[] { 1 }, new SimPoint(18000, 18000)), out _), Is.True);
            Assert.That(NetworkCommandCodec.TryAuthorizeObservation(world, new TrainCommand(1, 101, "worker"), out _), Is.True);
            Assert.That(NetworkCommandCodec.TryAuthorizeObservation(world, new SurrenderCommand(1), out _), Is.True);
        }

        [TestCase(1)] [TestCase(2)]
        public void ActualUnitySnapshotJsonRoundTripPreservesSeatLongSequenceAndOrdinaryEntities(int recipient)
        {
            var world = GuardWorld(); var original = NetworkObservation.Export(world, recipient, 9876543210L);
            string json = JsonUtility.ToJson(original);
            var decoded = JsonUtility.FromJson<NetworkSnapshot>(json);
            var replica = World.CreateNetworkReplica(world.Definition, world.Map, decoded);
            Assert.That(replica.NetworkSnapshotSequence, Is.EqualTo(9876543210L));
            Assert.That(replica.NetworkServerPlayerId, Is.EqualTo(recipient));
            Assert.That(replica.Units.Count, Is.EqualTo(1)); Assert.That(replica.Units[0].OwnerId, Is.EqualTo(1));
            Assert.That(replica.Units[0].Position, Is.EqualTo(original.Units[0].Position));
            Assert.That(replica.Units[0].IsPackedOutpost, Is.False);
            Assert.That(replica.Buildings[0].ActiveResearch, Is.Null); Assert.That(replica.Buildings[0].ProductionQueue, Is.Empty);
            Assert.That(replica.Vision.IsVisible(1, original.Units[0].Position), Is.True);
        }

        [TestCase(true)] [TestCase(false)]
        public void ServerStyleExplicitNullAndOmittedNestedFieldsDoNotInventPackedOutposts(bool explicitNull)
        {
            var world = GuardWorld(); var original = NetworkObservation.Export(world, 1, 1);
            // System.Text.Json IncludeFields emits PascalCase SimPoint members and JSON null for absent nested objects.
            // This fixture deliberately avoids JsonUtility generating the payload it is supposed to read.
            string json = @"{
                ""Version"":2,""RealmId"":""historical"",""BiomeId"":""forest"",""Sequence"":9876543210,""Tick"":0,""MapId"":""network-json-fixture"",""ServerPlayerId"":1,
                ""WidthCells"":20,""HeightCells"":20,""CellSizeMillimetres"":1000,
                ""LocalPlayer"":{""FactionId"":null,""Resources"":{""Food"":0,""Wood"":0,""Metal"":0,""Stone"":0},
                    ""PopulationUsed"":1,""PopulationReserved"":0,""PopulationCapacity"":10,""EraId"":null,""EraTier"":1,
                    ""CompletedTechnologies"":[],""PendingTechnologies"":[],""PendingEraAdvance"":false},
                ""OpponentFactionId"":null,
                ""Units"":[{""Id"":1,""OwnerId"":1,""DefinitionId"":""worker"",""Position"":{""X"":3500,""Z"":3500},
                    ""PreviousPosition"":{""X"":3500,""Z"":3500},""Destination"":{""X"":3500,""Z"":3500},
                    ""Health"":100,""CarryCapacity"":10,""GatherAmount"":1,""MoveSpeedMillimetresPerSecond"":3000,$PACKED$
                    ""PackedBuildingDefinitionId"":null}],
                ""Buildings"":[{""Id"":101,""OwnerId"":1,""DefinitionId"":""hearth"",""Position"":{""X"":1500,""Z"":1500},
                    ""Health"":1000,""BuildProgressTicks"":200,""ProductionWorkPermille"":1000,""ProductionQueue"":[],""ResearchId"":null}],
                ""Resources"":[],""Projectiles"":[],
                ""Match"":{""Mode"":0,""IsFinished"":false,""WinnerId"":0,""Reason"":0,""ElapsedTicks"":0,
                    ""LocalHoldTicks"":0,""OpponentHoldTicks"":0,""Objectives"":[]},""Vision"":""$VISION$""}";
            json = json.Replace("$PACKED$", explicitNull ? "\"PackedBuilding\":null," : "").Replace("$VISION$", original.Vision);
            var snapshot = JsonUtility.FromJson<NetworkSnapshot>(json);
            var replica = World.CreateNetworkReplica(world.Definition, world.Map, snapshot);
            Assert.That(replica.NetworkSnapshotSequence, Is.EqualTo(9876543210L)); Assert.That(replica.Units[0].IsPackedOutpost, Is.False);
            Assert.That(replica.Units[0].Position, Is.EqualTo(new SimPoint(3500, 3500)));
            Assert.That(replica.Buildings[0].ActiveResearch, Is.Null); Assert.That(replica.Buildings[0].ProductionQueue, Is.Empty);
        }

        [TestCase("gate")] [TestCase("wall")] [TestCase("siege")]
        public void SiegeAuthorizationDoesNotRevealHiddenEnemiesByGuessedId(string kind)
        {
            var world = GuardWorld();
            IGameCommand hidden = kind == "gate" ? (IGameCommand)new SetGateCommand(1, 102, true) :
                kind == "wall" ? new BoardWallCommand(1, new[] { 1 }, 102) : new BoardWallCommand(1, new[] { 1 }, 101, 2);
            IGameCommand missing = kind == "gate" ? (IGameCommand)new SetGateCommand(1, 999, true) :
                kind == "wall" ? new BoardWallCommand(1, new[] { 1 }, 999) : new BoardWallCommand(1, new[] { 1 }, 101, 999);
            Assert.That(NetworkCommandCodec.TryAuthorizeObservation(world, hidden, out string first), Is.False);
            Assert.That(NetworkCommandCodec.TryAuthorizeObservation(world, missing, out string second), Is.False);
            Assert.That(first, Is.EqualTo(second));
        }

        [Test]
        public void SiegeWireRejectsNegativeBridgeAndNonBooleanGateOptions()
        {
            var board = NetworkCommandCodec.Encode(new BoardWallCommand(1, new[] { 1 }, 101, 2), 1, 0, "board");
            board.Option = -1; Assert.That(NetworkCommandCodec.TryDecode(board, 1, 0, out _, out _), Is.False);
            var gate = NetworkCommandCodec.Encode(new SetGateCommand(1, 101, true), 1, 0, "gate");
            gate.Option = 2; Assert.That(NetworkCommandCodec.TryDecode(gate, 1, 0, out _, out _), Is.False);
        }

        [Test]
        public void TurnedBuildsAndWallRunsKeepEveryStretchAcrossTheWire()
        {
            var sites = new[] { new BuildSite(new SimPoint(7500, 4500), false), new BuildSite(new SimPoint(9000, 6500), true), new BuildSite(new SimPoint(1000000, 0), true) };
            var wire = JsonUtility.FromJson<NetworkCommandEnvelope>(JsonUtility.ToJson(NetworkCommandCodec.Encode(new BuildRunCommand(1, new[] { 3, 4 }, "wall", sites), 1, 0, "run")));
            Assert.That(NetworkCommandCodec.TryDecode(wire, 2, 0, out var decoded, out var error), Is.True, error);
            var run = (BuildRunCommand)decoded;
            Assert.That(run.PlayerId, Is.EqualTo(2)); Assert.That(run.BuildingDefinitionId, Is.EqualTo("wall")); Assert.That(run.WorkerIds, Is.EqualTo(new[] { 3, 4 }));
            Assert.That(run.Sites, Is.EqualTo(sites));
            var gate = JsonUtility.FromJson<NetworkCommandEnvelope>(JsonUtility.ToJson(NetworkCommandCodec.Encode(new BuildCommand(1, new[] { 3 }, "gate", new SimPoint(5500, 7000), true), 2, 0, "gate")));
            Assert.That(NetworkCommandCodec.TryDecode(gate, 1, 0, out var turned, out error), Is.True, error);
            Assert.That(((BuildCommand)turned).Turned, Is.True);
            var plain = NetworkCommandCodec.Encode(new BuildCommand(1, new[] { 3 }, "gate", new SimPoint(5500, 7000)), 3, 0, "plain");
            Assert.That(plain.Option, Is.Zero); Assert.That(plain.Sites, Is.Empty);
            Assert.That(() => NetworkCommandCodec.Encode(new BuildRunCommand(1, new[] { 3 }, "wall", new BuildSite[BuildRunCommand.MaximumSites + 1]), 4, 0, "long"), Throws.ArgumentException);
        }

        [TestCase("no-sites")] [TestCase("partial-site")] [TestCase("too-many-sites")] [TestCase("turn-flag")] [TestCase("negative-site")]
        [TestCase("oversized-site")] [TestCase("run-position")] [TestCase("run-option")] [TestCase("no-builders")] [TestCase("sites-on-build")] [TestCase("build-option")]
        public void MalformedWallRunsAndTurnsAreRejectedBeforeWorldSubmission(string corruption)
        {
            var e = NetworkCommandCodec.Encode(new BuildRunCommand(1, new[] { 5 }, "wall", new[] { new BuildSite(new SimPoint(7500, 4500), false) }), 1, 0, "run");
            switch (corruption)
            {
                case "no-sites": e.Sites = Array.Empty<int>(); break;
                case "partial-site": e.Sites = new[] { 7500, 4500 }; break;
                case "too-many-sites": e.Sites = new int[(BuildRunCommand.MaximumSites + 1) * 3]; break;
                case "turn-flag": e.Sites[2] = 2; break;
                case "negative-site": e.Sites[0] = -1; break;
                case "oversized-site": e.Sites[1] = 1000001; break;
                case "run-position": e.X = 7500; break;
                case "run-option": e.Option = 1; break;
                case "no-builders": e.UnitIds = Array.Empty<int>(); break;
                case "sites-on-build": e = NetworkCommandCodec.Encode(new BuildCommand(1, new[] { 5 }, "wall", new SimPoint(7500, 4500)), 1, 0, "build"); e.Sites = new[] { 7500, 4500, 0 }; break;
                case "build-option": e = NetworkCommandCodec.Encode(new BuildCommand(1, new[] { 5 }, "wall", new SimPoint(7500, 4500)), 1, 0, "build"); e.Option = 2; break;
            }
            Assert.That(NetworkCommandCodec.TryDecode(e, 1, 0, out var command, out var error), Is.False);
            Assert.That(command, Is.Null); Assert.That(error, Is.Not.Empty);
        }

        [Test]
        public void WallRunAuthorizationRequiresOwningEveryBuilder()
        {
            var world = GuardWorld(); var site = new[] { new BuildSite(new SimPoint(5500, 5500), false) };
            Assert.That(NetworkCommandCodec.TryAuthorizeObservation(world, new BuildRunCommand(1, new[] { 1 }, "wall", site), out _), Is.True);
            Assert.That(NetworkCommandCodec.TryAuthorizeObservation(world, new BuildRunCommand(1, new[] { 1, 2 }, "wall", site), out string enemy), Is.False);
            Assert.That(NetworkCommandCodec.TryAuthorizeObservation(world, new BuildRunCommand(1, new[] { 1, 999 }, "wall", site), out string missing), Is.False);
            Assert.That(enemy, Is.EqualTo(missing));
        }

        [TestCase(false)] [TestCase(true)]
        public void NavalOrdersCannotGuessAnotherPlayersHull(bool unload)
        {
            var world = GuardWorld();
            IGameCommand hidden = unload ? (IGameCommand)new DisembarkCommand(1, 2, new SimPoint(3500, 3500)) : new EmbarkCommand(1, new[] { 1 }, 2);
            IGameCommand missing = unload ? (IGameCommand)new DisembarkCommand(1, 999, new SimPoint(3500, 3500)) : new EmbarkCommand(1, new[] { 1 }, 999);
            Assert.That(NetworkCommandCodec.TryAuthorizeObservation(world, hidden, out var first), Is.False);
            Assert.That(NetworkCommandCodec.TryAuthorizeObservation(world, missing, out var second), Is.False);
            Assert.That(first, Is.EqualTo(second));
            Assert.That(NetworkCommandCodec.TryAuthorizeObservation(world, new EmbarkCommand(1, new[] { 2 }, 1), out _), Is.False);
        }

        [TestCase("missing-hull")] [TestCase("position-on-embark")] [TestCase("embark-option")]
        [TestCase("unload-selection")] [TestCase("unload-target")] [TestCase("unload-option")]
        public void MalformedNavalEnvelopesAreRejectedBeforeWorldSubmission(string corruption)
        {
            var e = NetworkCommandCodec.Encode(new EmbarkCommand(1, new[] { 5 }, 100), 1, 0, "embark");
            if (corruption.StartsWith("unload", StringComparison.Ordinal))
                e = NetworkCommandCodec.Encode(new DisembarkCommand(1, 100, new SimPoint(5000, 5000)), 1, 0, "unload");
            switch (corruption)
            {
                case "missing-hull": e.TargetId = 0; break;
                case "position-on-embark": e.X = 1; break;
                case "embark-option": case "unload-option": e.Option = 1; break;
                case "unload-selection": e.UnitIds = new[] { 5 }; break;
                case "unload-target": e.TargetId = 101; break;
            }
            Assert.That(NetworkCommandCodec.TryDecode(e, 1, 0, out var command, out var error), Is.False);
            Assert.That(command, Is.Null); Assert.That(error, Is.Not.Empty);
        }

        private static World GuardWorld() => new World(new GameDefinition
        {
            Units = new[] { new UnitDefinition { Id = "worker", IsWorker = true } },
            Buildings = new[] { new BuildingDefinition { Id = "hearth", WidthCells = 1, DepthCells = 1 } },
            Resources = new[] { new ResourceDefinition { Id = "wood" } }
        }, new MapDefinition
        {
            Id = "network-json-fixture", WidthCells = 20, HeightCells = 20,
            UnitSpawns = new[] {
                new UnitSpawnDefinition { Id = 1, OwnerId = 1, DefinitionId = "worker", Position = new SimPoint(3500, 3500) },
                new UnitSpawnDefinition { Id = 2, OwnerId = 2, DefinitionId = "worker", Position = new SimPoint(16500, 16500) } },
            BuildingSpawns = new[] {
                new BuildingSpawnDefinition { Id = 101, OwnerId = 1, DefinitionId = "hearth", Position = new SimPoint(1500, 1500) },
                new BuildingSpawnDefinition { Id = 102, OwnerId = 2, DefinitionId = "hearth", Position = new SimPoint(18500, 18500) } },
            ResourceSpawns = new[] { new ResourceSpawnDefinition { Id = 201, DefinitionId = "wood", Position = new SimPoint(17500, 16500) } },
            OfflineMatch = new OfflineMatchDefinition { Enabled = true, VisionUnitCells = 2, VisionBuildingCells = 2 }
        });
    }
}
