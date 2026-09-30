using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Emberfield.Networking;
using Emberfield.Simulation;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Emberfield.Presentation
{
    public sealed class OnlineControls : IMatchClock
    {
        internal static OnlineService Service = new OnlineService();
        internal static NetworkSnapshot PendingSnapshot;
        internal static OnlineState RememberedState;
        private static bool reopenAfterScene;
        private const string AddressPreference = "Emberfield.Online.ServerAddress";
        private static string selectedRealm = "historical", selectedMap = "amber_crossing", selectedFaction = "aven", selectedMode = "Conquest";
        private readonly MatchController match;
        private readonly IPlayerCommandQueue commands = new PlayerCommandQueue();
        private Task operation, poll, sending;
        private float nextPoll, lastSnapshotAt, lastContactAt;
        private long sequence;
        private int stateRevision, observedSessionRevision;
        private OnlineService observedService;
        private bool disposed, recovering, resultShown;
        public bool IsOpen { get; private set; }
        public bool Busy => operation != null && !operation.IsCompleted;
        public bool IsMatch => match.World.IsNetworkReplica;
        public long Tick => match.World.TickIndex;
        public int TickRate => World.TickRate;
        public bool BlocksWorldInput => IsOpen || IsMatch && (recovering || !Service.SignedIn || match.World.Match.IsFinished);
        public float Interpolation => match.World.Match.IsFinished ? 1 : Mathf.Clamp01((Time.unscaledTime - lastSnapshotAt) / .2f);
        public string Status { get; private set; } = "Sign in to host a private match or find an opponent.";
        public string Faction { get; private set; } = "aven";
        public string Realm { get; private set; } = "historical";
        public string MapId { get; private set; } = "amber_crossing";
        public string FactionName => FrontierCodex.Name(Faction);
        public string Mode { get; private set; } = "Conquest";
        public OnlineState State { get; private set; }
        public OnlineHistoryEntry[] History { get; private set; } = Array.Empty<OnlineHistoryEntry>();
        internal int PendingOrderCount => commands.Count;
        internal bool Recovering => recovering;
        internal bool SendingOrder => sending != null && !sending.IsCompleted;
        internal long SubmittedSequence => sequence;
        public OnlineControls(MatchController match)
        {
            this.match = match;
            if (!Service.SignedIn && Service.Address == OnlineService.DefaultAddress)
            {
                try
                {
                    var arguments = Environment.GetCommandLineArgs(); int option = Array.IndexOf(arguments, "-emberfieldServer");
                    string explicitAddress = option >= 0 && option + 1 < arguments.Length ? arguments[option + 1] : null;
                    var configuration = Resources.Load<TextAsset>("Online/server");
                    string packaged = configuration == null ? null : JsonUtility.FromJson<ServerConfiguration>(configuration.text)?.address;
                    Service.Configure(OnlineService.InitialAddress(explicitAddress, NativeSmokeStorage.GetPreference(AddressPreference), packaged));
                }
                catch (ArgumentException error) { Status = error.Message; }
            }
            observedService = Service; observedSessionRevision = Service.SessionRevision;
            Realm = selectedRealm; MapId = selectedMap; Faction = selectedFaction; Mode = selectedMode;
            if (!ContentRealms.IsValidRealm(Realm)) Realm = ContentRealms.All[0];
            if (!ContentRealms.IsPlayableFactionInRealm(Faction, Realm)) Faction = ContentRealms.FactionsForRealm(Realm)[0];
            if (!ContentRealms.IsMapAllowedInRealm(MapId, Realm)) MapId = ContentRealms.DefaultMapForRealm(Realm);
            RememberChoices();
            State = RememberedState;
            if (State != null)
            {
                AdoptState(State);
            }
            IsOpen = reopenAfterScene; reopenAfterScene = false;
            if (IsMatch)
            {
                sequence = State?.lastAcceptedSequence ?? 0; RefreshCosmetics();
                match.World.NetworkCommandSink = Submit;
                lastSnapshotAt = lastContactAt = Time.unscaledTime;
                Status = "Connected. The match continues while menus are open.";
                Application.runInBackground = true;
            }
        }
        public void Open() { Application.runInBackground = true; IsOpen = true; match.Alpha?.Close(); match.Research.Close(); match.Factions.Close(); match.OfflineControls.Close(); match.ClearSelection(); nextPoll = 0; }
        public void Close() { IsOpen = false; }
        private bool CanChoose => !disposed && !Busy && !IsMatch && (State == null || State.status == "idle");
        public void ChooseRealm()
        {
            if (!CanChoose) return;
            Realm = FrontierCodex.NextRealm(Realm); Faction = ContentRealms.FactionsForRealm(Realm)[0];
            MapId = ContentRealms.DefaultMapForRealm(Realm);
            RememberChoices();
        }
        public void ChooseMap() { if (!CanChoose) return; MapId = FrontierCodex.NextMap(MapId, Realm); RememberChoices(); }
        public void ChooseFaction()
        {
            if (!CanChoose) return;
            var factions = ContentRealms.FactionsForRealm(Realm);
            Faction = factions[(Array.IndexOf(factions, Faction) + 1) % factions.Length]; RememberChoices();
        }
        public void ChooseMode() { if (!CanChoose) return; Mode = Mode == "Conquest" ? "Dominion" : "Conquest"; RememberChoices(); }
        public void CheckConnection(string address) => Run(async () => {
            var service = Service; service.Configure(address); ObserveSession(); var context = Capture();
            await service.CheckConnection(); if (!Current(context)) return;
            SaveAddress(); Status = "Server ready and game version compatible. Sign in or create an account to play.";
        }, false);
        public void Authenticate(string address, string username, string password, bool register) => Run(async () => {
            var service = Service; int revision = stateRevision;
            service.Configure(address); await service.SignIn(username, password, register);
            if (disposed || revision != stateRevision || !ReferenceEquals(service, Service)) return;
            ResetSessionData(); ObserveSession(); SaveAddress();
            // Re-read the current membership after every login, even when another endpoint uses the same username.
            if (IsMatch) { ReloadLobby(); return; }
            Status = "Signed in. Choose a private room or matchmaking."; nextPoll = 0;
        });
        public void Host() => Act("/v1/rooms", "POST", new OnlineRoomRequest { factionId = Faction, mode = Mode, realmId = Realm, mapId = MapId });
        public void Join(string code) => Act("/v1/rooms/join", "POST", new OnlineRoomRequest { code = code.Trim().ToUpperInvariant(), factionId = Faction, realmId = Realm, mapId = MapId });
        public void Ready() => Act("/v1/rooms/ready", "POST", new OnlineRoomRequest { ready = true });
        public void Queue(bool ranked) => Act("/v1/queue", "POST", new OnlineRoomRequest { queue = ranked ? "ranked" : "casual", factionId = Faction, mode = Mode, realmId = Realm, mapId = MapId });
        public void CancelQueue() => Act("/v1/queue", "DELETE");
        public void Leave() => Run(async () => {
            var context = Capture(); await context.Service.Send<OnlineReply>("/v1/rooms/leave", "POST"); if (!Current(context)) return;
            ResetSessionData();
            if (IsMatch) ReloadLobby(); else nextPoll = 0;
        });
        public void Surrender() => Act("/v1/surrender", "POST");
        public void SignOut() => Run(async () => {
            var service = Service; var pending = service.SignOut(); var context = Capture();
            // Local sign-out must clear private data even if the outgoing logout cannot reach the service.
            ResetSessionData(); ObserveSession();
            try { await pending; if (Current(context)) Status = "Signed out. An active match continues until reconnection or forfeit."; }
            finally { if (Current(context) && IsMatch) ReloadLobby(); }
        });
        public void Profile() => Run(async () => {
            var context = Capture(); string realm = Realm;
            await context.Service.RefreshProfile(); if (!Current(context)) return;
            var history = await context.Service.Send<OnlineHistoryReply>("/v1/history/" + realm); if (!Current(context)) return;
            History = history.matches ?? Array.Empty<OnlineHistoryEntry>();
            var receipt = IsMatch ? await context.Service.ReadResultAsync() : null; if (!Current(context)) return;
            Status = receipt == null ? "Profile and match history refreshed." : "Server result recorded: " + receipt.Reason + ". Profile and history refreshed.";
        }, false);
        private void Act(string path, string method, object body = null) => Run(async () => {
            if (body is OnlineRoomRequest request && request.factionId != null &&
                (!ContentRealms.IsPlayableFactionInRealm(request.factionId, request.realmId) || !ContentRealms.IsMapAllowedInRealm(request.mapId, request.realmId)))
                throw new InvalidOperationException("Elige una facción disponible y un mapa de su ámbito.");
            var context = Capture(); var state = await context.Service.Send<OnlineState>(path, method, body);
            if (!Current(context)) return; AdoptState(state); nextPoll = 0;
        });
        private void Run(Func<Task> action, bool changesState = true)
        {
            if (disposed || Busy) return;
            if (changesState) stateRevision++;
            operation = SafeAction(action, stateRevision);
        }
        private async Task SafeAction(Func<Task> action, int revision)
        {
            try { await action(); }
            catch (Exception error) { if (!disposed && revision == stateRevision) Status = Message(error); }
        }
        public void Update()
        {
            if (disposed) return;
            if (!ReferenceEquals(observedService, Service) || observedSessionRevision != Service.SessionRevision)
            {
                ResetSessionData(); ObserveSession(); nextPoll = 0;
                if (!Service.SignedIn) { IsOpen = true; Status = "Sign in again to resume your session."; }
            }
            if (IsMatch && Service.SignedIn && Time.unscaledTime - lastContactAt > 3 && !match.World.Match.IsFinished)
            { recovering = true; Status = "Connection interrupted. Reconnecting; the server match continues."; }
            if (IsMatch && Service.SignedIn && match.World.Match.IsFinished && !resultShown && !Busy)
            { resultShown = true; IsOpen = true; Status = "Match finished. The server has recorded the result."; Profile(); }
            bool waiting = State?.status == "queued" || State?.status == "lobby" || State?.status == "starting";
            if (Service.SignedIn && (IsOpen || IsMatch || waiting) && !Busy && (poll == null || poll.IsCompleted) && Time.unscaledTime >= nextPoll)
            { nextPoll = Time.unscaledTime + (IsMatch ? .2f : 1); poll = Poll(); }
            if (!recovering && Service.SignedIn && commands.Count > 0 && (sending == null || sending.IsCompleted)) sending = SendNext();
        }
        private async Task Poll()
        {
            var context = Capture();
            try
            {
                if (IsMatch)
                {
                    var reply = await context.Service.Send<OnlineSnapshotReply>("/v1/matches/current/snapshot");
                    if (!Current(context)) return;
                    if (reply.observation == null) throw new InvalidOperationException("Snapshot missing.");
                    // The service can return the same cached observation when a menu requests an early poll.
                    if (reply.observation.Sequence != match.World.NetworkSnapshotSequence)
                    { match.World.ApplyNetworkSnapshot(reply.observation); match.Metrics?.ObserveTick(match.World); lastSnapshotAt = Time.unscaledTime; }
                    AdoptState(reply.state); RefreshCosmetics(); sequence = Math.Max(sequence, reply.lastAcceptedSequence);
                    lastContactAt = Time.unscaledTime;
                    if (recovering) Status = "Reconnected to the current server state.";
                    recovering = false;
                }
                else
                {
                    var state = await context.Service.Send<OnlineState>("/v1/state");
                    if (!Current(context)) return;
                    AdoptState(state);
                    if (State.status == "active" || State.status == "finished")
                    {
                        var reply = await context.Service.Send<OnlineSnapshotReply>("/v1/matches/current/snapshot");
                        if (!Current(context)) return;
                        PendingSnapshot = reply.observation ?? throw new InvalidOperationException("Snapshot missing.");
                        AdoptState(reply.state); RefreshCosmetics(); SceneManager.LoadScene("Greybox");
                    }
                }
            }
            catch (Exception error)
            {
                if (!Current(context)) return;
                Status = Message(error); nextPoll = Time.unscaledTime + 1;
                if (IsMatch && Service.SignedIn)
                {
                    try
                    {
                        var state = await context.Service.Send<OnlineState>("/v1/state");
                        if (!Current(context)) return;
                        AdoptState(state);
                        if (state.status == "aborted" || state.status == "idle")
                        { IsOpen = true; recovering = true; Status = "The server match is no longer active. Return to the lobby; history shows recorded results."; }
                    }
                    catch { } // Keep the last valid observation while the connection is unavailable.
                }
            }
        }
        private void RefreshCosmetics()
        {
            CosmeticEquippedItem[] own = Array.Empty<CosmeticEquippedItem>(), rival = Array.Empty<CosmeticEquippedItem>();
            foreach (var seat in State?.players ?? Array.Empty<OnlineSeat>())
                if (seat.playerId == State.playerId) own = seat.cosmetics ?? Array.Empty<CosmeticEquippedItem>();
                else rival = seat.cosmetics ?? Array.Empty<CosmeticEquippedItem>();
            CosmeticLoadout.SetOnlineEquipment(own); CosmeticLoadout.SetOpponentEquipment(rival);
        }
        private void AdoptState(OnlineState state)
        {
            if (state == null || string.IsNullOrEmpty(state.status)) throw new InvalidOperationException("Lobby state missing. Refresh the connection.");
            // An idle response carries server defaults, not the player's next-match choices.
            if (state.status == "idle") { State = RememberedState = state; return; }
            if (!ContentRealms.IsValidRealm(state.realmId)) throw new InvalidOperationException("Unsupported PvP realm in server state.");
            if (!ContentRealms.IsMapAllowedInRealm(state.mapId, state.realmId) || state.mode != "Conquest" && state.mode != "Dominion")
                throw new InvalidOperationException("Unsupported battlefield or victory mode in server state.");
            foreach (var seat in state.players ?? Array.Empty<OnlineSeat>())
                if (!ContentRealms.IsPlayableFactionInRealm(seat.factionId, state.realmId)) throw new InvalidOperationException("Faction is not available in the server's PvP realm.");
            State = RememberedState = state;
            Realm = state.realmId; MapId = state.mapId; Mode = state.mode;
            if (!ContentRealms.IsPlayableFactionInRealm(Faction, Realm)) Faction = ContentRealms.FactionsForRealm(Realm)[0];
            foreach (var seat in state.players ?? Array.Empty<OnlineSeat>()) if (seat.playerId == state.playerId) Faction = seat.factionId;
            RememberChoices();
        }
        private void RememberChoices() { selectedRealm = Realm; selectedMap = MapId; selectedFaction = Faction; selectedMode = Mode; }
        private static void SaveAddress()
        {
            NativeSmokeStorage.SetPreference(AddressPreference, Service.Address);
        }
        [Serializable] private sealed class ServerConfiguration { public string address; }
        private void ObserveSession() { observedService = Service; observedSessionRevision = Service.SessionRevision; }
        private void ResetSessionData()
        {
            State = RememberedState = null; PendingSnapshot = null; History = Array.Empty<OnlineHistoryEntry>();
            commands.Clear(); sequence = 0; recovering = IsMatch;
            RefreshCosmetics();
        }
        private static void ReloadLobby() { reopenAfterScene = true; SceneManager.LoadScene("Greybox"); }
        private RequestContext Capture() => new RequestContext(Service, Service.SessionRevision, stateRevision);
        private bool Current(RequestContext context) => !disposed && ReferenceEquals(context.Service, Service) &&
            context.Session == Service.SessionRevision && context.State == stateRevision;
        private readonly struct RequestContext
        {
            internal readonly OnlineService Service;
            internal readonly int Session, State;
            internal RequestContext(OnlineService service, int session, int state) { Service = service; Session = session; State = state; }
        }
        private CommandResult Submit(IGameCommand command)
        {
            if (!Service.SignedIn || recovering || commands.Count >= 32 || match.World.Match.IsFinished)
                return NetworkCommandResult.Unavailable("Wait for the server connection before issuing orders.");
            try
            {
                var envelope = NetworkCommandCodec.Encode(command, ++sequence, match.World.TickIndex, Guid.NewGuid().ToString("N"));
                if (!commands.TryEnqueue(envelope)) { sequence--; return NetworkCommandResult.Unavailable("Order queue full. Wait for the server."); }
                return NetworkCommandResult.Queued();
            }
            catch (ArgumentException error) { sequence--; return NetworkCommandResult.Unavailable(error.Message); }
        }
        private async Task SendNext()
        {
            if (!commands.TryDequeue(out var envelope)) return;
            var context = Capture();
            var request = new OnlineCommandRequest { sequence = envelope.Sequence, command = envelope };
            try
            {
                var reply = await context.Service.Send<OnlineCommandReply>("/v1/matches/current/commands", "POST", request);
                if (!Current(context)) return;
                match.SetFeedback(reply.accepted ? "Order accepted by the server." : reply.error ?? "The server rejected the order.");
                if (reply.accepted) match.Metrics?.ObserveAcceptedOnlineOrder();
            }
            catch (Exception error)
            {
                if (!Current(context)) return;
                // A snapshot already in flight may predate the failed command's consumed cursor.
                stateRevision++; commands.Clear(); recovering = true; sequence = 0; nextPoll = 0;
                Status = "Order delivery could not be confirmed. Reconnecting."; match.SetFeedback(Message(error));
            }
        }
        public void Dispose() { disposed = true; commands.Clear(); if (IsMatch) match.World.NetworkCommandSink = null; }
        private static string Message(Exception error) => error is TaskCanceledException || error is System.Net.Http.HttpRequestException ?
            Service.Address.StartsWith("http:", StringComparison.Ordinal) ? "Cannot reach the local server. Start the Emberfield service on this computer, or enter your shared HTTPS server address." :
            "Cannot reach this HTTPS server. Check the address, connection and server availability." : error.Message;
    }

    public sealed partial class MatchController
    {
        public OnlineControls Online { get; private set; }
        private bool TryInitializeOnlineWorld()
        {
            var snapshot = OnlineControls.PendingSnapshot;
            if (snapshot == null) return false;
            OnlineControls.PendingSnapshot = null;
            World = World.CreateNetworkReplica(JsonUtility.FromJson<GameDefinition>(Resources.Load<TextAsset>("Definitions/greybox").text),
                JsonUtility.FromJson<MapDefinition>(Resources.Load<TextAsset>("Maps/" + ContentRealms.MapResourceId(snapshot.MapId, snapshot.RealmId)).text), snapshot);
            return true;
        }
    }
}
