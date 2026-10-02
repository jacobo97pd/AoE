using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Emberfield.Simulation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Emberfield.Presentation
{
    // Explicit development-player automation. Uses the shipped UI, HTTP client and read-only replica.
    // Credentials exist only in the disposable runner config and normal in-memory sign-in flow.
    public sealed partial class OnlinePlayerSmoke
    {
        [Serializable] private sealed class Config
        {
            public string Role, Address, Username, Password, SyncDirectory, OutputDirectory;
            public string RealmId = "historical", MapId;
            public bool Resume, NavalSlice;
            public int TimeoutSeconds = 240;
        }
        [Serializable] private sealed class RoomFile { public string Code; }
        [Serializable] private sealed class Report
        {
            public string Role, BuildGuid, UnityVersion, MatchId, Failure = "", HistoryOutcome = "";
            public string RealmId, MapId, BiomeId, LocalFactionId, OpponentFactionId;
            public bool IsolatedStorage;
            public string LocalStorageRoot;
            public bool RealmAndMapVerified, HistoryRealmAndMapVerified;
            public bool StarterRosterVerified, PlannedFactionsLocked;
            public string StartingWorkerDefinitionId, TrainedDefinitionId;
            public int TrainingWoodSpent;
            public int DockId, ShipId, PassengerId, DockWoodSpent, ShipWoodSpent, LandingX, LandingZ;
            public string ShipDefinitionId;
            public bool ShipTrained, ShipSailed, PassengerLanded, NavalAssetsPersisted;
            public bool KingdomResearched, WallRunVerified, TurnedBuildVerified, FortificationsPersisted;
            public bool StoneVisibleBeforeScouting, StoneScouted;
            public int StoneScoutOrders, DiscoveredStoneId;
            public int[] WallRunIds = Array.Empty<int>();
            public BuildSite[] WallRunSites = Array.Empty<BuildSite>();
            public int WallRunWidthCells, WallRunDepthCells, WallRunLengthCells, WallRunStoneSpent;
            public int TurnedBuildId, TurnedBuildX, TurnedBuildZ, TurnedBuildWidthCells, TurnedBuildDepthCells, TurnedBuildStoneSpent;
            public string FailureOperation = "", FailureExceptionType = "", FailureMethods = "";
            public int Width, Height, ServerPlayerId, NativeButtonClicks, MovedUnitId, FailureHResult, EvidenceWriteRetries;
            public long InitialTick, ActionsTick, FinalTick, AcceptedSequence, BeforeRestartTick, ReconnectedTick, CurrentTick;
            public bool Passed, Replica, ReplicaTickDoesNotAdvance, HiddenOpponentAtStart, Moved, Trained, ResourcesDelivered;
            public bool Reconnected, PartnerDisconnectedObserved, AuthorityAdvancedWhilePeerAbsent, PersistedHistory, StatisticsPresent;
            public bool SettingsModalPausesLocal, ReturnedToLobby, ConnectionChecked, ReturnedLobbySelectionPreserved;
            public string OnlineStatus = "", OrderFeedback = "";
            public int PendingOrders;
            public long SubmittedSequence;
            public bool Recovering, SendingOrder;
            public int GatheringWorkerId, GathererX, GathererZ, GathererTask, GathererCargo, GathererBlockedTicks, WoodStock, OwnedUnits;
        }
        private static Report memory;
        private static bool checkingReturnToLobby;
        private readonly MatchController match;
        private readonly Config config;
        private readonly Report report;
        private readonly float deadline;
        private bool failed, finished;
        private string evidenceOperation = "driver";
        private OnlinePlayerSmoke(MatchController match, Config config)
        {
            this.match = match; this.config = config;
            Directory.CreateDirectory(config.SyncDirectory); Directory.CreateDirectory(config.OutputDirectory);
            if (memory == null && config.Resume)
            {
                string prior = Path.Combine(config.SyncDirectory, config.Role + "-progress.json");
                if (File.Exists(prior) && new FileInfo(prior).Length <= 16384) memory = JsonUtility.FromJson<Report>(File.ReadAllText(prior));
            }
            report = memory ?? (memory = new Report { Role = config.Role, BuildGuid = Application.buildGUID,
                UnityVersion = Application.unityVersion, Width = Screen.width, Height = Screen.height, RealmId = config.RealmId, MapId = config.MapId });
            report.IsolatedStorage = NativeSmokeStorage.IsIsolated; report.LocalStorageRoot = NativeSmokeStorage.Root;
            if (!report.IsolatedStorage) throw new InvalidOperationException("Online smoke requires isolated local storage.");
            deadline = Time.realtimeSinceStartup + Mathf.Clamp(config.TimeoutSeconds, 30, config.NavalSlice ? 360 : 240);
        }
        public static void TryStart(MatchController match)
        {
            if (!Debug.isDebugBuild) return;
            var arguments = Environment.GetCommandLineArgs(); int option = Array.IndexOf(arguments, "-emberfieldOnlineSmoke");
            if (option < 0 || option + 1 >= arguments.Length) return;
            try
            {
                var file = new FileInfo(arguments[option + 1]);
                if (!file.Exists || file.Length > 16384) throw new ArgumentException("Invalid smoke config.");
                var config = JsonUtility.FromJson<Config>(File.ReadAllText(file.FullName));
                if (config == null || config.Role != "host" && config.Role != "guest" || string.IsNullOrEmpty(config.OutputDirectory)
                    || string.IsNullOrEmpty(config.SyncDirectory) || string.IsNullOrEmpty(config.Username) || string.IsNullOrEmpty(config.Password))
                    throw new ArgumentException("Incomplete smoke config.");
                if (string.IsNullOrEmpty(config.RealmId)) config.RealmId = "historical";
                if (string.IsNullOrEmpty(config.MapId)) config.MapId = ContentRealms.DefaultMapForRealm(config.RealmId);
                if (!ContentRealms.IsMapAllowedInRealm(config.MapId, config.RealmId))
                    throw new ArgumentException("Unsupported smoke realm or battlefield.");
                Application.runInBackground = true;
                var smoke = new OnlinePlayerSmoke(match, config);
                match.StartCoroutine(smoke.Guard(checkingReturnToLobby ? smoke.ReturnedLobby() : match.World.IsNetworkReplica ? smoke.Play() : smoke.Lobby()));
            }
            catch { Debug.LogError("EMBERFIELD_ONLINE_SMOKE invalid configuration"); Application.Quit(1); }
        }
        private IEnumerator Guard(IEnumerator entry)
        {
            var stack = new Stack<IEnumerator>(); stack.Push(entry);
            while (!failed && stack.Count > 0)
            {
                object current = null; bool moved = false;
                try { moved = stack.Peek().MoveNext(); if (moved) current = stack.Peek().Current; }
                catch (Exception error) { RememberError(error); Fail("Driver exception: " + error.GetType().Name); }
                if (failed) yield break;
                if (!moved) { stack.Pop(); continue; }
                if (current is IEnumerator nested) stack.Push(nested);
                else yield return current;
            }
        }
        private IEnumerator Lobby()
        {
            if (!config.Resume)
            {
                match.Alpha.Open(); long pausedAt = match.World.TickIndex;
                yield return new WaitForSecondsRealtime(.15f);
                report.SettingsModalPausesLocal = match.World.TickIndex == pausedAt;
                Check(report.SettingsModalPausesLocal, "Settings did not pause the local practice simulation.");
                yield return Capture("settings.png"); match.Alpha.Close();
            }
            match.Online.Open();
            yield return Capture(config.Resume ? "relogin.png" : "sign-in.png");
            SetField("Server address", config.Address);
            yield return Click("Check server connection");
            yield return Wait(() => !match.Online.Busy && match.Online.Status.StartsWith("Server ready", StringComparison.Ordinal), "Server readiness/version check did not pass.", 10);
            report.ConnectionChecked = true;
            SetField("Username (3-24 letters, numbers or underscore)", config.Username);
            SetField("Password (at least 10 characters)", config.Password);
            yield return Click(config.Resume ? "Sign in" : "Create account");
            yield return Wait(() => OnlineControls.Service.SignedIn && !match.Online.Busy, "Authentication did not complete.", 18);
            if (config.Resume)
            {
                // The normal lobby poll restores the current match and changes scenes.
                yield return Wait(() => match.World.IsNetworkReplica, "The signed-in client did not restore its match.", 22);
                yield break;
            }
            yield return Wait(() => match.Online.State != null && match.Online.State.status == "idle", "The online lobby did not become ready.", 12);
            for (int i = 0; i < ContentRealms.All.Length && match.Online.Realm != config.RealmId; i++) yield return Click("PvP realm");
            for (int i = 0; i < FrontierCodex.MapIds.Length && match.Online.MapId != config.MapId; i++) yield return Click("Battlefield");
            Check(match.Online.Realm == config.RealmId && match.Online.MapId == config.MapId, "Native realm or battlefield selection did not match configuration.");
            for (int i = 0; i < ContentRealms.FactionsForRealm(config.RealmId).Length && match.Online.Faction != ExpectedFaction(config.Role); i++)
                yield return Click("Faction");
            Check(match.Online.Faction == ExpectedFaction(config.Role), "Native faction selection did not match the requested participants.");
            if (config.RealmId == "naval")
            {
                match.Hud.Invalidate(); match.Hud.Refresh(); yield return null;
                int locked = 0;
                foreach (var button in match.Hud.OnlinePanel.Root.GetComponentsInChildren<Button>())
                    foreach (string id in FrontierCodex.PlannedNavalFactions)
                        if (button.name == "Planned faction " + id && !button.interactable) locked++;
                report.PlannedFactionsLocked = locked == FrontierCodex.PlannedNavalFactions.Length;
                Check(report.PlannedFactionsLocked, "Planned fleets must be visibly locked in the naval lobby.");
                yield return Capture("naval-options.png");
            }
            if (config.Role == "host")
            {
                yield return Click("Host private 1v1");
                yield return Wait(() => match.Online.State?.status == "lobby" && !string.IsNullOrEmpty(match.Online.State.roomCode), "Private room was not created.", 12);
                Write("room.json", JsonUtility.ToJson(new RoomFile { Code = match.Online.State.roomCode }));
            }
            else
            {
                yield return Wait(() => File.Exists(Sync("room.json")), "Host did not publish a room code.", 25);
                var room = JsonUtility.FromJson<RoomFile>(File.ReadAllText(Sync("room.json")));
                SetField("Private room code", room.Code); yield return Click("Join private room");
                yield return Wait(() => match.Online.State?.status == "lobby", "Guest did not join the private room.", 12);
            }
            Check(match.Online.State.realmId == config.RealmId && match.Online.State.mapId == config.MapId, "Private room used the wrong realm or battlefield.");
            yield return Capture("private-lobby.png");
            yield return Click("Ready to start");
            // The shipped poll, snapshot bootstrap and SceneManager replace this controller.
            yield return Wait(() => match.World.IsNetworkReplica, "The private match did not start.", 24);
        }
        private IEnumerator Play()
        {
            report.Replica = match.World.IsNetworkReplica; report.ServerPlayerId = match.World.NetworkServerPlayerId;
            report.BiomeId = match.World.Map.BiomeId;
            Check(match.World.TryGetPlayer(1, out var local) && match.World.TryGetPlayer(2, out _), "Replica participants missing.");
            match.World.TryGetPlayer(2, out var opponent);
            report.LocalFactionId = local.FactionId; report.OpponentFactionId = opponent.FactionId;
            report.RealmAndMapVerified = match.World.Map.RealmId == config.RealmId && match.World.Map.Id == config.MapId &&
                match.Online.State.realmId == config.RealmId && match.Online.State.mapId == config.MapId &&
                report.RealmId == config.RealmId && report.MapId == config.MapId &&
                ContentRealms.IsPlayableFactionInRealm(local.FactionId, config.RealmId) && ContentRealms.IsPlayableFactionInRealm(opponent.FactionId, config.RealmId);
            Check(report.RealmAndMapVerified, "Replica realm, battlefield or faction membership differed from configuration.");
            Check(report.LocalFactionId == ExpectedFaction(config.Role) && report.OpponentFactionId == ExpectedFaction(config.Role == "host" ? "guest" : "host"),
                "Native faction selections did not reach both authoritative seats.");
            Check(report.Replica && match.OfflineControls.Ai == null, "Online client must display a replica without an offline AI.");
            long tick = match.World.TickIndex; match.World.Tick();
            report.ReplicaTickDoesNotAdvance = match.World.TickIndex == tick;
            Check(report.ReplicaTickDoesNotAdvance, "The client advanced authoritative simulation.");
            if (!config.Resume)
            {
                report.MatchId = match.Online.State?.matchId; report.InitialTick = tick;
                Check(!string.IsNullOrEmpty(report.MatchId), "Online match identity missing.");
                int enemyHearth = report.ServerPlayerId == 1 ? 101 : 100;
                report.HiddenOpponentAtStart = !match.World.TryGetBuilding(enemyHearth, out _) && match.View.RootFor(enemyHearth) == null;
                Check(report.HiddenOpponentAtStart, "Starting observation included the hidden rival Hearth.");
            }
            else
            {
                Check(report.Role == "guest" && report.MatchId == match.Online.State?.matchId, "Process restart restored a different match.");
                Check(match.World.TickIndex > report.BeforeRestartTick, "Server state did not advance across process restart.");
                Check(OwnedCount() >= 5, "Previously trained unit did not survive reconnection.");
                VerifyPersistentAssets();
                report.ReconnectedTick = match.World.TickIndex;
            }
            match.Online.Close(); match.OfflineControls.Close(); match.Rig.Home();
            yield return Capture(config.Resume ? "reconnected-start.png" : "match-start.png");
            if (config.Resume) yield return ResumeActions();
            else yield return InitialActions();
            if (config.Role == "guest" && !config.Resume)
            {
                report.BeforeRestartTick = match.World.TickIndex; SaveProgress(); Write("guest-restart-ready.flag", "ready");
                // The runner kills this actual process and starts a new one with the same disposable account.
                yield return Wait(() => false, "Runner did not restart the guest process.", 55); yield break;
            }
            if (config.Role == "host")
            {
                yield return Wait(() => PeerDisconnected(), "Host never observed the absent peer.", 45);
                report.PartnerDisconnectedObserved = true;
                long absentTick = match.World.TickIndex;
                // The runner cannot relaunch the guest until we publish the flag below.
                yield return Wait(() => match.World.TickIndex >= absentTick + 20, "Authority stopped advancing while the guest process was absent.", 6);
                report.AuthorityAdvancedWhilePeerAbsent = true;
                match.Online.Open(); yield return Capture("peer-disconnected.png"); SaveProgress(); Write("host-disconnected.flag", "observed");
                yield return Wait(() => File.Exists(Sync("guest-reconnected.flag")), "Guest did not reconnect to the same match.", 35);
                yield return Wait(() => !PeerDisconnected(), "The restored peer did not appear connected.", 8);
                VerifyPersistentAssets();
                if (config.NavalSlice) yield return LandSmokePassenger();
                yield return Capture("peer-reconnected.png");
                yield return Click("Surrender..."); yield return Click("Confirm surrender");
            }
            yield return Wait(() => match.World.Match.IsFinished, "Authoritative surrender result did not arrive.", 18);
            int expected = config.Role == "host" ? 2 : 1;
            Check(match.World.Match.WinnerId == expected, "Winner was not normalized to the local perspective.");
            Check(match.Online.State?.result != null && match.Online.State.result.winnerPlayerId == 2, "Server result did not identify the guest winner.");
            yield return Wait(() => !match.Online.Busy, "Result profile refresh did not finish.", 8);
            match.Online.Profile();
            yield return Wait(() => FindHistory() != null && !match.Online.Busy, "The server result was not present in match history.", 12);
            var history = FindHistory();
            report.HistoryRealmAndMapVerified = history.realmId == config.RealmId && history.mapId == config.MapId;
            Check(report.HistoryRealmAndMapVerified, "The stored result belongs to another realm or battlefield.");
            report.HistoryOutcome = history.outcome; report.PersistedHistory = history.outcome == (config.Role == "host" ? "loss" : "win");
            report.StatisticsPresent = history.statistics != null && history.statistics.workersRemaining >= 5 && history.statistics.buildingsRemaining >= 1;
            Check(report.PersistedHistory && report.StatisticsPresent, "Persisted result or authoritative match statistics were incorrect.");
            report.FinalTick = match.World.TickIndex; match.Online.Open(); yield return Capture("result.png");
            var scroll = match.Hud.OnlinePanel.Root.GetComponentInChildren<ScrollRect>(); scroll.verticalNormalizedPosition = 0;
            yield return Capture("history.png");
            checkingReturnToLobby = true;
            yield return Click("Return to lobby");
            yield return Wait(() => false, "Return to lobby did not change scenes.", 12);
        }
        private IEnumerator ReturnedLobby()
        {
            yield return null;
            Check(!match.World.IsNetworkReplica && match.Online.IsOpen && !match.Shell.IsOpen, "Returning from the finished match did not reopen the lobby.");
            yield return Wait(() => match.Online.State?.status == "idle" && !match.Online.Busy, "The returned lobby did not become ready.", 12);
            report.ReturnedLobbySelectionPreserved = match.Online.Realm == config.RealmId && match.Online.MapId == config.MapId;
            Check(report.ReturnedLobbySelectionPreserved, "Returning to the lobby reset the selected PvP realm or battlefield.");
            report.ReturnedToLobby = true;
            yield return Capture("returned-lobby.png");
            report.Passed = true; Finish();
        }
        private IEnumerator InitialActions()
        {
            var workers = OwnedWorkers(); Check(workers.Count >= 4, "Expected the ordinary four-worker opening.");
            report.StartingWorkerDefinitionId = ContentRealms.StartingWorkerForFaction(report.LocalFactionId);
            report.StarterRosterVerified = true;
            foreach (int id in workers) if (!match.World.TryGetUnit(id, out var starter) || starter.DefinitionId != report.StartingWorkerDefinitionId) report.StarterRosterVerified = false;
            Check(report.StarterRosterVerified, "Authority opening workers do not match the selected faction.");
            int worker = workers[0], gatherer = workers[1]; report.MovedUnitId = worker;
            report.GatheringWorkerId = gatherer;
            match.World.TryGetUnit(worker, out var unit); var before = unit.Position;
            int direction = report.ServerPlayerId == 1 ? 1 : -1;
            var destination = new SimPoint(before.X + direction * 2500, before.Z + direction * 2000);
            match.Select(new[] { worker }); match.Issue(new MoveCommand(1, new[] { worker }, destination), "Moving.");
            match.World.TryGetUnit(worker, out var unchanged); Check(unchanged.Position.Equals(before), "Queued command mutated client gameplay before a snapshot.");
            int woodBefore = LocalResources().Wood;
            ResourceNodeState source = null; long nearest = long.MaxValue;
            match.World.TryGetUnit(gatherer, out var gatheringUnit);
            foreach (var item in match.World.Resources) if (item.Kind == ResourceKind.Wood && item.RemainingAmount > 0)
            { long x = (long)item.Position.X - gatheringUnit.Position.X, z = (long)item.Position.Z - gatheringUnit.Position.Z; long distance = x * x + z * z; if (distance < nearest) { nearest = distance; source = item; } }
            Check(source != null, "Opening observation has no visible Wood source.");
            var gather = match.World.Submit(new GatherCommand(1, new[] { gatherer }, source.Id)); Check(gather.Accepted, "Gather command could not be queued.");
            BuildingState hearth = null; foreach (var building in match.World.Buildings) if (building.OwnerId == 1 && building.DefinitionId == "hearth") { hearth = building; break; }
            Check(hearth != null, "Owned Hearth missing."); int beforeCount = OwnedCount();
            report.TrainedDefinitionId = report.StartingWorkerDefinitionId;
            foreach (var definition in match.World.Definition.Units) if (definition.Id == report.TrainedDefinitionId) report.TrainingWoodSpent = definition.Cost.Wood;
            // Worker costs differ by faction. A trained unit confirms spending; stock above this net baseline proves a physical deposit.
            int woodAfterTraining = woodBefore - report.TrainingWoodSpent;
            match.Select(new[] { hearth.Id }); var train = match.Economy.Train(report.TrainedDefinitionId); Check(train.Accepted, "Train command could not be queued.");
            yield return Wait(() => {
                report.Moved = Moved(worker, before); report.Trained = OwnedCount() > beforeCount; report.ResourcesDelivered = report.Trained && LocalResources().Wood > woodAfterTraining;
                return report.Moved && report.Trained && report.ResourcesDelivered;
            }, "Real server movement, training or delivered resources did not reach the client.", 30);
            report.Moved = Moved(worker, before); report.Trained = OwnedCount() > beforeCount; report.ResourcesDelivered = report.Trained && LocalResources().Wood > woodAfterTraining;
            report.AcceptedSequence = match.Online.State?.lastAcceptedSequence ?? 0;
            Check(report.AcceptedSequence >= 3, "The authority did not acknowledge all three command sequences.");
            report.ActionsTick = match.World.TickIndex; SaveProgress(); match.Select(new[] { gatherer }); yield return Capture("gameplay.png");
            if (config.NavalSlice) yield return NavalActions(hearth.Id);
            else yield return FortificationActions(hearth.Id);
        }
        private string ExpectedFaction(string role)
        {
            if (config.RealmId == ContentRealms.Fantasy) return role == "host" ? "verdant" : "ashen";
            var factions = ContentRealms.FactionsForRealm(config.RealmId);
            return factions[(role == "host" ? 0 : 1) % factions.Length];
        }
        private IEnumerator FortificationActions(int hearthId)
        {
            // Exercise ordinary paid gameplay: gather the shortage and research Kingdom on the C# authority.
            // Neither the replica nor the server gets free resources, forced era changes or accelerated ticks.
            evidenceOperation = "fortifications:gather";
            var workers = OwnedWorkers(); Check(workers.Count >= 5, "Fortification check needs the recruited fifth worker.");
            var wall = match.Economy.BuildingDefinition("wall");
            TechnologyDefinition kingdom = null;
            foreach (var technology in match.World.Definition.Technologies) if (technology.Id == "advance_kingdom") kingdom = technology;
            Check(wall != null && BuildingFootprints.CanTurn(wall) && kingdom != null, "Fortification definitions are unavailable.");
            var required = new ResourceAmount(kingdom.Cost.Food + wall.Cost.Food * 3, kingdom.Cost.Wood + wall.Cost.Wood * 3,
                kingdom.Cost.Metal + wall.Cost.Metal * 3, kingdom.Cost.Stone + wall.Cost.Stone * 3);
            AssignGather(workers[0], ResourceKind.Food); AssignGather(workers[2], ResourceKind.Food);
            AssignGather(workers[1], ResourceKind.Wood);
            Check(match.World.TryGetBuilding(hearthId, out var openingHearth), "Hearth missing while scouting construction resources.");
            yield return ScoutStone(new[] { workers[3], workers[4] }, openingHearth.Position);
            AssignGather(workers[3], ResourceKind.Stone); AssignGather(workers[4], ResourceKind.Stone);
            yield return Wait(() => {
                var stock = LocalResources();
                return stock.Food >= required.Food && stock.Wood >= required.Wood && stock.Metal >= required.Metal && stock.Stone >= required.Stone;
            }, "Ordinary gathering did not fund Kingdom and three wall stretches.", 130);
            Check(match.World.Submit(new StopCommand(1, workers.ToArray())).Accepted, "Builders could not stop gathering.");
            long stopSequence = match.Online.SubmittedSequence;
            yield return Wait(() => (match.Online.State?.lastAcceptedSequence ?? 0) >= stopSequence && match.Online.PendingOrderCount == 0 && !match.Online.SendingOrder,
                "The gathering stop was not acknowledged before paid construction.", 8);
            evidenceOperation = "fortifications:research";
            Check(match.World.ValidateResearch(new ResearchCommand(1, hearthId, kingdom.Id)).Accepted, "Kingdom research was not legally available.");
            Check(match.World.Submit(new ResearchCommand(1, hearthId, kingdom.Id)).Accepted, "Kingdom research could not be queued.");
            yield return Wait(() => match.World.TryGetPlayer(1, out var player) && player.EraId == "kingdom", "Paid Kingdom research did not complete on the authority.", 40);
            report.KingdomResearched = true;
            Check(match.World.TryGetBuilding(hearthId, out var hearth), "Hearth missing while planning fortifications.");
            int[] builders = { workers[0], workers[2] };
            report.WallRunSites = FindWallRun(builders, wall, hearth.Position);
            Check(report.WallRunSites.Length == 2, "No visible, reachable two-stretch north-south run could be placed.");
            evidenceOperation = "fortifications:run";
            int stoneBefore = LocalResources().Stone;
            Check(match.World.Submit(new BuildRunCommand(1, builders, wall.Id, report.WallRunSites)).Accepted, "BuildRunCommand could not be queued.");
            yield return Wait(() => FindWall(report.WallRunSites[0].Position) != null && FindWall(report.WallRunSites[1].Position) != null,
                "The authority did not create both north-south BuildRun foundations.", 12);
            report.WallRunIds = new[] { FindWall(report.WallRunSites[0].Position).Id, FindWall(report.WallRunSites[1].Position).Id };
            report.WallRunStoneSpent = stoneBefore - LocalResources().Stone;
            Check(report.WallRunStoneSpent == wall.Cost.Stone * 2, "BuildRun did not spend exactly two ordinary wall costs.");
            var first = FindWall(report.WallRunSites[0].Position);
            report.WallRunWidthCells = first.WidthCells; report.WallRunDepthCells = first.DepthCells;
            report.WallRunLengthCells = (Math.Abs(report.WallRunSites[1].Position.Z - report.WallRunSites[0].Position.Z) /
                match.World.Map.CellSizeMillimetres) + first.DepthCells;
            report.WallRunVerified = VerifyWallRun(false);
            Check(report.WallRunVerified, "BuildRun footprints were not rotated into one contiguous north-south wall.");
            evidenceOperation = "fortifications:single";
            int[] singleBuilder = { workers[3] };
            var single = FindTurnedWall(singleBuilder, wall, hearth.Position);
            report.TurnedBuildX = single.X; report.TurnedBuildZ = single.Z;
            stoneBefore = LocalResources().Stone;
            Check(match.World.Submit(new BuildCommand(1, singleBuilder, wall.Id, single, true)).Accepted, "Turned BuildCommand could not be queued.");
            yield return Wait(() => FindWall(single) != null, "The authority did not create the separately rotated BuildCommand wall.", 12);
            var built = FindWall(single); report.TurnedBuildId = built.Id;
            report.TurnedBuildWidthCells = built.WidthCells; report.TurnedBuildDepthCells = built.DepthCells;
            report.TurnedBuildStoneSpent = stoneBefore - LocalResources().Stone;
            report.TurnedBuildVerified = built.IsTurned && built.WidthCells == wall.DepthCells && built.DepthCells == wall.WidthCells && report.TurnedBuildStoneSpent == wall.Cost.Stone;
            Check(report.TurnedBuildVerified, "BuildCommand rotation or its paid stone cost was lost on the authority.");
            evidenceOperation = "fortifications:completion";
            yield return Wait(() => VerifyFortifications(true), "Workers did not complete the queued wall run and separate rotated wall.", 40);
            SaveProgress(); match.Select(new[] { report.WallRunIds[0], report.WallRunIds[1], report.TurnedBuildId });
            yield return Capture("north-south-fortifications.png");
            evidenceOperation = "driver";
        }
        private void AssignGather(int worker, ResourceKind kind)
        {
            Check(match.World.TryGetUnit(worker, out var unit), "Gathering worker missing.");
            ResourceNodeState nearest = null; long distance = long.MaxValue;
            foreach (var node in match.World.Resources) if (node.Kind == kind && node.RemainingAmount > 0)
            {
                long dx = (long)node.Position.X - unit.Position.X, dz = (long)node.Position.Z - unit.Position.Z;
                long candidate = dx * dx + dz * dz;
                if (candidate < distance) { distance = candidate; nearest = node; }
            }
            Check(nearest != null, "No currently visible " + kind + " node for construction gathering.");
            Check(match.World.Submit(new GatherCommand(1, new[] { worker }, nearest.Id)).Accepted,
                "The ordinary " + kind + " gathering order could not be queued.");
        }
        private ResourceNodeState VisibleStone()
        {
            foreach (var node in match.World.Resources) if (node.Kind == ResourceKind.Stone && node.RemainingAmount > 0) return node;
            return null;
        }
        private IEnumerator ScoutStone(int[] scouts, SimPoint hearth)
        {
            // Resources outside the received observation are unknown. Explore relative to our own base;
            // never consult authored resource spawns or inject a hidden node into the replica.
            report.StoneVisibleBeforeScouting = VisibleStone() != null;
            if (!report.StoneVisibleBeforeScouting)
            {
                evidenceOperation = "fortifications:scout-stone";
                int cell = match.World.Map.CellSizeMillimetres;
                int towardX = hearth.X < match.World.Map.WidthCells * cell / 2 ? 1 : -1;
                int towardZ = hearth.Z < match.World.Map.HeightCells * cell / 2 ? 1 : -1;
                var offsets = new[] { new GridCell(towardX * 8, 0), new GridCell(0, towardZ * 8), new GridCell(0, -towardZ * 8) };
                foreach (var offset in offsets)
                {
                    if (VisibleStone() != null) break;
                    if (!TryScoutWaypoint(scouts, hearth, offset, out var target)) continue;
                    Check(match.World.Submit(new MoveCommand(1, scouts, target)).Accepted, "Stone scouting movement could not be queued.");
                    report.StoneScoutOrders++;
                    float until = Mathf.Min(deadline, Time.realtimeSinceStartup + 8);
                    while (VisibleStone() == null && Time.realtimeSinceStartup < until) yield return null;
                }
                report.StoneScouted = VisibleStone() != null;
                Check(report.StoneScouted, "Ordinary scouting did not reveal a Stone node within three nearby routes (24 seconds).");
            }
            report.DiscoveredStoneId = VisibleStone().Id;
            SaveProgress(); evidenceOperation = "fortifications:gather";
        }
        private bool TryScoutWaypoint(int[] scouts, SimPoint hearth, GridCell offset, out SimPoint target)
        {
            target = default;
            int cell = match.World.Map.CellSizeMillimetres, width = match.World.Map.WidthCells, height = match.World.Map.HeightCells;
            if (!match.World.TryGetUnit(scouts[0], out var scout)) return false;
            int cx = hearth.X / cell, cz = hearth.Z / cell;
            var reachable = new bool[width * height]; var pending = new Queue<GridCell>();
            var start = new GridCell(scout.Position.X / cell, scout.Position.Z / cell);
            reachable[start.Z * width + start.X] = true; pending.Enqueue(start);
            var directions = new[] { new GridCell(-1, 0), new GridCell(1, 0), new GridCell(0, -1), new GridCell(0, 1) };
            // Bounded flood fill of publicly walkable cells near the base selects a reachable waypoint.
            // The ordinary authority movement command still validates routes and unit clearance itself.
            while (pending.Count > 0)
            {
                var current = pending.Dequeue();
                foreach (var direction in directions)
                {
                    int x = current.X + direction.X, z = current.Z + direction.Z;
                    if (x < 0 || z < 0 || x >= width || z >= height || Math.Abs(x - cx) > 14 || Math.Abs(z - cz) > 14 || reachable[z * width + x]) continue;
                    var point = new SimPoint(x * cell + cell / 2, z * cell + cell / 2);
                    if (!match.World.IsWalkable(point)) continue;
                    reachable[z * width + x] = true; pending.Enqueue(new GridCell(x, z));
                }
            }
            foreach (int id in scouts)
                if (!match.World.TryGetUnit(id, out var unit) || !reachable[(unit.Position.Z / cell) * width + unit.Position.X / cell]) return false;
            for (int radius = 0; radius <= 2; radius++)
            for (int dz = -radius; dz <= radius; dz++)
            for (int dx = -radius; dx <= radius; dx++)
            {
                if (Math.Max(Math.Abs(dx), Math.Abs(dz)) != radius) continue;
                int x = cx + offset.X + dx, z = cz + offset.Z + dz;
                if (x < 0 || z < 0 || x >= width || z >= height || !reachable[z * width + x]) continue;
                target = new SimPoint(x * cell + cell / 2, z * cell + cell / 2); return true;
            }
            return false;
        }
        private BuildSite[] FindWallRun(int[] workers, BuildingDefinition wall, SimPoint around)
        {
            int cell = match.World.Map.CellSizeMillimetres;
            int cx = around.X / cell, cz = around.Z / cell;
            for (int radius = 3; radius <= 11; radius++)
            for (int z = cz - radius; z <= cz + radius; z++)
            for (int x = cx - radius; x <= cx + radius; x++)
            {
                if (Math.Max(Math.Abs(x - cx), Math.Abs(z - cz)) != radius) continue;
                var sites = new List<BuildSite>();
                WallRunLayout.Layout(wall, cell, new[] { new GridCell(x, z), new GridCell(x, z + wall.WidthCells * 2 - 1) }, sites);
                var command = new BuildRunCommand(1, workers, wall.Id, sites.ToArray());
                var preview = match.World.PreviewBuildRun(command);
                if (sites.Count == 2 && preview.Result.Accepted && preview.AcceptedCount == 2) return sites.ToArray();
            }
            return Array.Empty<BuildSite>();
        }
        private SimPoint FindTurnedWall(int[] workers, BuildingDefinition wall, SimPoint around)
        {
            int cell = match.World.Map.CellSizeMillimetres;
            int cx = around.X / cell, cz = around.Z / cell;
            for (int radius = 3; radius <= 11; radius++)
            for (int z = cz - radius; z <= cz + radius; z++)
            for (int x = cx - radius; x <= cx + radius; x++)
            {
                if (Math.Max(Math.Abs(x - cx), Math.Abs(z - cz)) != radius) continue;
                var position = new SimPoint(x * cell + wall.DepthCells * cell / 2, z * cell + wall.WidthCells * cell / 2);
                if (match.World.ValidatePlacement(1, workers, wall.Id, position, true).Accepted) return position;
            }
            Check(false, "No visible, reachable placement for a separate north-south BuildCommand wall."); return default;
        }
        private BuildingState FindWall(SimPoint position)
        {
            foreach (var building in match.World.Buildings)
                if (building.OwnerId == 1 && building.DefinitionId == "wall" && building.Position == position) return building;
            return null;
        }
        private bool VerifyWallRun(bool complete)
        {
            if (report.WallRunIds.Length != 2 || report.WallRunSites.Length != 2) return false;
            var definition = match.Economy.BuildingDefinition("wall");
            for (int i = 0; i < 2; i++)
                if (!match.World.TryGetBuilding(report.WallRunIds[i], out var wall) || wall.OwnerId != 1 || wall.DefinitionId != "wall" || !wall.IsTurned ||
                    wall.WidthCells != definition.DepthCells || wall.DepthCells != definition.WidthCells || wall.Position != report.WallRunSites[i].Position || complete && !wall.IsComplete) return false;
            return report.WallRunSites[0].Position.X == report.WallRunSites[1].Position.X &&
                Math.Abs(report.WallRunSites[0].Position.Z - report.WallRunSites[1].Position.Z) == definition.WidthCells * match.World.Map.CellSizeMillimetres;
        }
        private bool VerifyFortifications(bool complete)
        {
            var definition = match.Economy.BuildingDefinition("wall");
            return VerifyWallRun(complete) && match.World.TryGetBuilding(report.TurnedBuildId, out var wall) && wall.OwnerId == 1 && wall.DefinitionId == "wall" &&
                wall.IsTurned && wall.WidthCells == definition.DepthCells && wall.DepthCells == definition.WidthCells &&
                wall.Position == new SimPoint(report.TurnedBuildX, report.TurnedBuildZ) && (!complete || wall.IsComplete);
        }
        private IEnumerator ResumeActions()
        {
            Check(report.Moved && report.Trained && report.ResourcesDelivered, "Prior process checkpoints are missing.");
            Check(match.Online.State.lastAcceptedSequence >= report.AcceptedSequence, "Server command sequence regressed after sign-in.");
            int worker = report.MovedUnitId; Check(match.World.TryGetUnit(worker, out var unit), "The moved worker disappeared during restart.");
            var before = unit.Position; long oldSequence = match.Online.State.lastAcceptedSequence;
            // The worker now stands beside a wall; do not aim the recovery order through that newly built footprint.
            SimPoint destination = before;
            foreach (var offset in new[] { new SimPoint(-1500, 0), new SimPoint(1500, 0), new SimPoint(0, -1500), new SimPoint(0, 1500) })
            {
                var candidate = new SimPoint(before.X + offset.X, before.Z + offset.Z);
                var middle = new SimPoint(before.X + offset.X / 2, before.Z + offset.Z / 2);
                int radius = unit.RadiusMillimetres;
                if (!match.World.IsWalkable(middle) || !match.World.IsWalkable(candidate) ||
                    !match.World.IsWalkable(new SimPoint(candidate.X - radius, candidate.Z - radius)) ||
                    !match.World.IsWalkable(new SimPoint(candidate.X + radius, candidate.Z + radius))) continue;
                destination = candidate; break;
            }
            Check(destination != before, "No nearby ground for the post-reconnect movement check.");
            match.Select(new[] { worker }); match.Issue(new MoveCommand(1, new[] { worker }, destination), "Moving after reconnect.");
            yield return Wait(() => Moved(worker, before) && match.Online.State.lastAcceptedSequence > oldSequence, "Reconnected client could not issue the next valid command.", 14);
            if (config.NavalSlice) yield return LandSmokePassenger();
            report.Reconnected = true; report.AcceptedSequence = match.Online.State.lastAcceptedSequence;
            SaveProgress(); yield return Capture("reconnected-gameplay.png"); Write("guest-reconnected.flag", "confirmed");
        }
        private IEnumerator Wait(Func<bool> condition, string failure, float seconds)
        {
            float until = Mathf.Min(deadline, Time.realtimeSinceStartup + seconds); float nextWrite = 0;
            while (!failed && !condition())
            {
                if (Time.realtimeSinceStartup >= until) { Fail(failure); yield break; }
                if (match.World.IsNetworkReplica && Time.realtimeSinceStartup >= nextWrite) { SaveProgress(); nextWrite = Time.realtimeSinceStartup + .5f; }
                yield return null;
            }
        }
        private IEnumerator Click(string name)
        {
            match.Hud.Invalidate(); match.Hud.Refresh(); yield return null; Canvas.ForceUpdateCanvases();
            Button button = null;
            foreach (var item in match.Hud.OnlinePanel.Root.GetComponentsInChildren<Button>()) if (item.name == name) { button = item; break; }
            Check(button != null && button.interactable, "Unavailable online button: " + name);
            var scroll = button.GetComponentInParent<ScrollRect>();
            if (scroll != null)
            {
                var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(scroll.viewport, button.transform);
                float delta = scroll.viewport.rect.center.y - bounds.center.y;
                var position = scroll.content.anchoredPosition;
                position.y = Mathf.Clamp(position.y + delta, 0, Mathf.Max(0, scroll.content.rect.height - scroll.viewport.rect.height)); scroll.content.anchoredPosition = position;
            }
            yield return null; Canvas.ForceUpdateCanvases();
            var canvas = button.GetComponentInParent<Canvas>(); var rect = button.GetComponent<RectTransform>();
            var screen = RectTransformUtility.WorldToScreenPoint(canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera, rect.TransformPoint(rect.rect.center));
            var pointer = new PointerEventData(EventSystem.current) { position = screen, button = PointerEventData.InputButton.Left };
            var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(pointer, hits);
            Check(hits.Count > 0 && (hits[0].gameObject == button.gameObject || hits[0].gameObject.transform.IsChildOf(button.transform)), "Online button was covered: " + name);
            report.NativeButtonClicks++; ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerClickHandler);
        }
        private void SetField(string name, string value)
        {
            foreach (var field in match.Hud.OnlinePanel.Root.GetComponentsInChildren<InputField>(true)) if (field.name == name) { field.text = value; return; }
            throw new InvalidOperationException("Missing online input.");
        }
        private IEnumerator Capture(string name)
        {
            match.Hud.Invalidate(); match.SyncPresentation(1); match.Hud.PrepareOffscreenCapture(match.Rig.Camera); Canvas.ForceUpdateCanvases();
            yield return new WaitForEndOfFrame();
            evidenceOperation = "capture:" + name;
            Check(PlayerSmoke.Capture(match, Path.Combine(config.OutputDirectory, name)), "Online screenshot was empty.");
            evidenceOperation = "driver";
        }
        private List<int> OwnedWorkers() { var result = new List<int>(); foreach (var unit in match.World.Units) if (unit.OwnerId == 1 && unit.IsWorker) result.Add(unit.Id); return result; }
        private int OwnedCount() { int count = 0; foreach (var unit in match.World.Units) if (unit.OwnerId == 1) count++; return count; }
        private ResourceAmount LocalResources() { match.World.TryGetPlayer(1, out var player); return player.Resources; }
        private bool Moved(int id, SimPoint before) { if (!match.World.TryGetUnit(id, out var unit)) return false; long x = (long)unit.Position.X - before.X, z = (long)unit.Position.Z - before.Z; return x * x + z * z >= 250000; }
        private bool PeerDisconnected() { var seats = match.Online.State?.players; if (seats == null) return false; foreach (var seat in seats) if (seat.playerId != report.ServerPlayerId) return !seat.connected; return false; }
        private OnlineHistoryEntry FindHistory() { foreach (var entry in match.Online.History) if (entry.matchId == report.MatchId) return entry; return null; }
        private string Sync(string name) => Path.Combine(config.SyncDirectory, name);
        private void Write(string name, string contents) => WriteEvidence(Sync(name), contents);
        private void WriteEvidence(string path, string contents)
        {
            // Atomic checkpoints can briefly race a Windows reader that has not enabled delete sharing.
            // Use a per-write temporary name and bounded retries; never publish a partial JSON document.
            evidenceOperation = "write:" + Path.GetFileName(path);
            string temporary = path + ".tmp-" + Guid.NewGuid().ToString("N");
            try
            {
                var bytes = new System.Text.UTF8Encoding(false).GetBytes(contents);
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                { stream.Write(bytes, 0, bytes.Length); stream.Flush(true); }
                for (int attempt = 0; ; attempt++)
                {
                    try
                    {
                        if (File.Exists(path)) File.Replace(temporary, path, null);
                        else File.Move(temporary, path);
                        break;
                    }
                    catch (IOException)
                    { if (attempt >= 7) throw; report.EvidenceWriteRetries++; System.Threading.Thread.Sleep(10); }
                }
                evidenceOperation = "driver";
            }
            finally
            {
                try { if (File.Exists(temporary)) File.Delete(temporary); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }
        private void RememberError(Exception error)
        {
            if (!string.IsNullOrEmpty(report.FailureExceptionType)) return;
            report.FailureOperation = evidenceOperation; report.FailureExceptionType = error.GetType().Name; report.FailureHResult = error.HResult;
            var methods = new List<string>(); var frames = new System.Diagnostics.StackTrace(error, false).GetFrames();
            if (frames != null) foreach (var frame in frames)
            {
                var method = frame.GetMethod(); if (method != null) methods.Add((method.DeclaringType?.FullName ?? "") + "." + method.Name);
                if (methods.Count == 6) break;
            }
            // Method names only: no exception message, argument values, source paths or config fields.
            report.FailureMethods = string.Join(" | ", methods);
        }
        private void SaveProgress()
        {
            report.CurrentTick = match.World.TickIndex;
            report.AcceptedSequence = Math.Max(report.AcceptedSequence, match.Online.State?.lastAcceptedSequence ?? 0);
            report.OnlineStatus = match.Online.Status; report.OrderFeedback = match.Feedback;
            report.PendingOrders = match.Online.PendingOrderCount; report.SubmittedSequence = match.Online.SubmittedSequence;
            report.Recovering = match.Online.Recovering; report.SendingOrder = match.Online.SendingOrder;
            report.WoodStock = LocalResources().Wood; report.OwnedUnits = OwnedCount();
            if (match.World.TryGetUnit(report.GatheringWorkerId, out var gatherer) && gatherer.OwnerId == 1)
            {
                report.GathererX = gatherer.Position.X; report.GathererZ = gatherer.Position.Z;
                report.GathererTask = (int)gatherer.WorkerTask; report.GathererCargo = gatherer.CarriedAmount; report.GathererBlockedTicks = gatherer.MovementBlockedTicks;
            }
            Write(config.Role + "-progress.json", JsonUtility.ToJson(report, true));
        }
        private void Check(bool okay, string message) { if (!okay) { Fail(message); throw new InvalidOperationException("Smoke assertion failed."); } }
        private void Fail(string message) { if (failed || finished) return; failed = true; report.Failure = message; report.Passed = false; Finish(); }
        private void Finish()
        {
            if (finished) return; finished = true;
            try { SaveProgress(); }
            catch (Exception error) { RememberError(error); report.Passed = false; if (string.IsNullOrEmpty(report.Failure)) report.Failure = "Could not write final progress evidence."; }
            try { WriteEvidence(Path.Combine(config.OutputDirectory, "smoke.json"), JsonUtility.ToJson(report, true)); }
            catch (Exception error) { RememberError(error); report.Passed = false; Debug.Log("EMBERFIELD_ONLINE_SMOKE evidence write failed: " + report.FailureOperation + " / " + error.HResult); }
            Debug.Log("EMBERFIELD_ONLINE_SMOKE " + config.Role + " " + report.Passed); Application.Quit(report.Passed ? 0 : 1);
        }
    }
}
