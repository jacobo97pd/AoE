using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Emberfield.Presentation;
using Emberfield.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Emberfield.Tests.PlayMode
{
    public sealed class OnlineRecoveryTests
    {
        private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.NonPublic;
        private const BindingFlags Static = BindingFlags.Static | BindingFlags.NonPublic;
        private readonly Dictionary<string, object> savedFields = new Dictionary<string, object>();
        private ControlledTransport transport;
        private OnlineService service;
        private OnlineControls online;
        private MatchController match;
        private GameObject actor;

        private sealed class ControlledTransport : HttpMessageHandler
        {
            internal readonly List<TaskCompletionSource<HttpResponseMessage>> Pending = new List<TaskCompletionSource<HttpResponseMessage>>();
            internal readonly List<string> Bodies = new List<string>();
            internal readonly List<string> Paths = new List<string>();
            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellation)
            {
                var completion = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
                Pending.Add(completion); Paths.Add(request.RequestUri.AbsolutePath); Bodies.Add(request.Content == null ? "" : await request.Content.ReadAsStringAsync());
                using (cancellation.Register(() => completion.TrySetCanceled())) return await completion.Task;
            }
            internal void Reply(int index, object value) => Json(index, JsonUtility.ToJson(value));
            internal void Json(int index, string json, HttpStatusCode status = HttpStatusCode.OK) => Pending[index].SetResult(
                new HttpResponseMessage(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") });
            internal void Login(int index, string name) => Json(index, "{\"ok\":true,\"token\":\"" + name + "-session\",\"profile\":{\"username\":\"" + name + "\",\"ratings\":[]}}");
            internal void Fail(int index) => Pending[index].SetException(new HttpRequestException("Controlled unavailable service"));
        }

        [SetUp]
        public void SetUp()
        {
            foreach (string name in new[] { "Service", "RememberedState", "PendingSnapshot", "reopenAfterScene", "selectedRealm", "selectedMap", "selectedFaction", "selectedMode" })
                savedFields[name] = typeof(OnlineControls).GetField(name, Static).GetValue(null);
            transport = new ControlledTransport(); service = new OnlineService(transport);
            SetStatic("Service", service); SetStatic("RememberedState", null); SetStatic("PendingSnapshot", null); SetStatic("reopenAfterScene", false);
            SetStatic("selectedRealm", "historical"); SetStatic("selectedMap", "amber_crossing"); SetStatic("selectedFaction", "aven"); SetStatic("selectedMode", "Conquest");
        }
        [TearDown]
        public void TearDown()
        {
            online?.Dispose(); service.Dispose(); if (actor != null) Object.DestroyImmediate(actor);
            foreach (var pair in savedFields) SetStatic(pair.Key, pair.Value);
            savedFields.Clear(); online = null; actor = null;
        }
        private static void SetStatic(string name, object value) => typeof(OnlineControls).GetField(name, Static).SetValue(null, value);
        private static IEnumerator Complete(Task task)
        {
            for (int i = 0; i < 300 && !task.IsCompleted; i++) yield return null;
            Assert.IsTrue(task.IsCompleted, "Controlled HTTP task did not finish.");
            Assert.IsFalse(task.IsFaulted, task.Exception?.ToString());
        }
        private IEnumerator Login(string name = "First")
        {
            int index = transport.Pending.Count; var login = service.SignIn(name, "controlled-test-password", false);
            transport.Login(index, name); yield return Complete(login);
        }
        private void CreateControls(World world = null, OnlineState state = null)
        {
            // Inactive component supplies the real World and feedback sink without starting a scene/camera or its Update loop.
            actor = new GameObject("Controlled online recovery"); actor.SetActive(false); match = actor.AddComponent<MatchController>();
            typeof(MatchController).GetField("<World>k__BackingField", Instance).SetValue(match, world ?? DefinitionLoader.CreateOfflineWorld("aven", VictoryMode.Conquest));
            SetStatic("RememberedState", state); online = new OnlineControls(match);
        }
        private Task Poll() => (Task)typeof(OnlineControls).GetMethod("Poll", Instance).Invoke(online, null);
        private Task SendNext() => (Task)typeof(OnlineControls).GetMethod("SendNext", Instance).Invoke(online, null);
        private Task Operation => (Task)typeof(OnlineControls).GetField("operation", Instance).GetValue(online);
        private static OnlineState State(string status = "lobby", long cursor = 0, string realm = "historical", string map = "amber_crossing") => new OnlineState {
            ok = true, status = status, realmId = realm, mapId = map, mode = "Conquest", playerId = 1, lastAcceptedSequence = cursor,
            roomCode = "OLDROOM", matchId = "controlled-match", players = new[] { new OnlineSeat { playerId = 1, username = "First", factionId = ContentRealms.FactionsForRealm(realm)[0] } }
        };

        [UnityTest]
        public IEnumerator ADelayedLobbyPollCannotRestoreARoomAfterLeaving()
        {
            yield return Login(); CreateControls(state: State());
            var oldPoll = Poll(); online.Leave();
            transport.Json(2, "{\"ok\":true}"); yield return Complete(Operation);
            transport.Reply(1, State()); yield return Complete(oldPoll);
            Assert.IsNull(online.State); Assert.IsNull(typeof(OnlineControls).GetField("RememberedState", Static).GetValue(null));
        }

        [UnityTest]
        public IEnumerator APreviousAccountsDelayedLobbyResponseCannotReplaceTheNewSession()
        {
            yield return Login(); CreateControls(); var oldPoll = Poll();
            yield return Login("Next"); online.Update();
            transport.Reply(1, State()); yield return Complete(oldPoll);
            Assert.AreEqual("Next", service.Profile.username); Assert.IsNull(online.State);
        }

        [UnityTest]
        public IEnumerator FailedLogoutStillClearsTheRoomAndPrivateHistoryImmediately()
        {
            yield return Login(); CreateControls(state: State()); online.Profile();
            transport.Json(1, "{\"ok\":true,\"profile\":{\"username\":\"First\",\"ratings\":[]}}");
            for (int i = 0; i < 300 && transport.Pending.Count < 3; i++) yield return null;
            Assert.AreEqual(3, transport.Pending.Count);
            transport.Json(2, "{\"ok\":true,\"matches\":[{\"realmId\":\"historical\",\"opponent\":\"PreviousPrivateOpponent\"}]}");
            yield return Complete(Operation); Assert.AreEqual(1, online.History.Length);
            online.SignOut();
            Assert.IsFalse(service.SignedIn); Assert.IsNull(online.State); Assert.IsEmpty(online.History);
            transport.Fail(3); yield return Complete(Operation);
            Assert.IsNull(online.State); Assert.IsEmpty(online.History);
        }

        [UnityTest]
        public IEnumerator SessionExpiryWhileHistoryIsPendingCannotPublishTheOldHistory()
        {
            yield return Login(); CreateControls(); online.Profile();
            transport.Json(1, "{\"ok\":true,\"profile\":{\"username\":\"First\",\"ratings\":[]}}");
            for (int i = 0; i < 300 && transport.Pending.Count < 3; i++) yield return null;
            Assert.AreEqual(3, transport.Pending.Count);
            var expiry = service.Send<OnlineReply>("/v1/cosmetics");
            transport.Json(3, "{\"ok\":false,\"error\":\"session_expired\"}", HttpStatusCode.Unauthorized);
            for (int i = 0; i < 300 && !expiry.IsCompleted; i++) yield return null;
            Assert.IsTrue(expiry.IsFaulted); _ = expiry.Exception; online.Update();
            transport.Json(2, "{\"ok\":true,\"matches\":[{\"realmId\":\"historical\",\"opponent\":\"PreviousPrivateOpponent\"}]}");
            yield return Complete(Operation);
            Assert.IsFalse(service.SignedIn); Assert.IsEmpty(online.History); Assert.IsNull(online.State);
        }

        [UnityTest]
        public IEnumerator JoinUsesTheRoomsActualBattlefieldAndKeepsItWhenIdle()
        {
            yield return Login(); CreateControls(); online.ChooseRealm(); online.Join("oldroom");
            transport.Reply(1, State(realm: "fantasy", map: "sapphire_coast")); yield return Complete(Operation);
            Assert.AreEqual("fantasy", online.Realm); Assert.AreEqual("sapphire_coast", online.MapId);
            online.Leave(); transport.Json(2, "{\"ok\":true}"); yield return Complete(Operation);
            var idle = Poll(); transport.Reply(3, State("idle")); yield return Complete(idle);
            Assert.AreEqual("fantasy", online.Realm); Assert.AreEqual("sapphire_coast", online.MapId);
        }

        [Test]
        public void RealmAndFactionCyclesOfferOnlyActiveFactionsAndKeepNavalOnTheCoast()
        {
            CreateControls();
            foreach (string realm in ContentRealms.All)
            {
                Assert.AreEqual(realm, online.Realm);
                var ids = ContentRealms.FactionsForRealm(realm);
                for (int index = 0; index < ids.Length; index++)
                {
                    Assert.AreEqual(ids[index], online.Faction);
                    Assert.IsTrue(ContentRealms.IsPlayableFactionInRealm(online.Faction, realm));
                    online.ChooseFaction();
                }
                Assert.AreEqual(ids[0], online.Faction);
                for (int index = 0; index < 4; index++)
                {
                    online.ChooseMap(); Assert.IsTrue(ContentRealms.IsMapAllowedInRealm(online.MapId, realm));
                    if (realm == "naval") Assert.AreEqual("sapphire_coast", online.MapId);
                }
                online.ChooseRealm();
            }
            Assert.AreEqual("historical", online.Realm);
        }

        [UnityTest]
        public IEnumerator NavalHostAndIdleReturnPreserveThePirateCoastalSelection()
        {
            yield return Login(); CreateControls(); online.ChooseRealm(); online.ChooseRealm(); online.Host();
            var request = JsonUtility.FromJson<OnlineRoomRequest>(transport.Bodies[1]);
            Assert.AreEqual("naval", request.realmId); Assert.AreEqual("pirates", request.factionId); Assert.AreEqual("sapphire_coast", request.mapId);
            transport.Reply(1, State(realm: "naval", map: "sapphire_coast")); yield return Complete(Operation);
            online.ChooseRealm(); Assert.AreEqual("naval", online.Realm, "A private lobby cannot locally change its competitive realm.");
            online.Leave(); transport.Json(2, "{\"ok\":true}"); yield return Complete(Operation);
            var idle = Poll(); transport.Reply(3, State("idle")); yield return Complete(idle);
            Assert.AreEqual("naval", online.Realm); Assert.AreEqual("pirates", online.Faction); Assert.AreEqual("sapphire_coast", online.MapId);
        }

        [UnityTest]
        public IEnumerator TheSultanatoQueuesForHistoricalRankedOnItsDesert()
        {
            yield return Login(); CreateControls();
            while (online.Faction != "sultanate") online.ChooseFaction();
            while (online.MapId != "sunscar_basin") online.ChooseMap();
            Assert.AreEqual("historical", online.Realm);
            online.Queue(true);
            var request = JsonUtility.FromJson<OnlineRoomRequest>(transport.Bodies[1]);
            Assert.AreEqual("/v1/queue", transport.Paths[1]);
            Assert.AreEqual("historical", request.realmId); Assert.AreEqual("sultanate", request.factionId);
            Assert.AreEqual("sunscar_basin", request.mapId); Assert.AreEqual("ranked", request.queue);
            transport.Reply(1, new OnlineState { ok = true, status = "queued", realmId = "historical", mapId = "sunscar_basin", mode = "Conquest" });
            yield return Complete(Operation);
            Assert.AreEqual("sultanate", online.Faction); Assert.AreEqual("sunscar_basin", online.MapId);
        }

        [UnityTest]
        public IEnumerator PlannedAndLegacyFactionRequestsAreRejectedBeforeAnyHttpMutation()
        {
            yield return Login(); CreateControls();
            foreach (string id in new[] { "miraj", "solar", "english_navy", "spanish_navy", "skeleton_fleet" })
            {
                typeof(OnlineControls).GetField("<Faction>k__BackingField", Instance).SetValue(online, id);
                online.Host(); yield return Complete(Operation);
                Assert.AreEqual(1, transport.Pending.Count, id + " must not reach /v1/rooms.");
                Assert.IsNull(online.State);
            }
        }

        [UnityTest]
        public IEnumerator AnInvalidNavalServerMapCannotReplaceTheChosenRoom()
        {
            yield return Login(); CreateControls(); online.ChooseRealm(); online.ChooseRealm();
            var reply = Poll(); transport.Reply(1, State(realm: "naval", map: "amber_crossing")); yield return Complete(reply);
            Assert.IsNull(online.State); Assert.AreEqual("sapphire_coast", online.MapId);
            Assert.AreEqual("pirates", online.Faction);
        }

        [UnityTest]
        public IEnumerator RecoveryIgnoresAPollOlderThanTheUnconfirmedCommandAndReadsAFreshCursor()
        {
            yield return Login(); var authority = DefinitionLoader.CreateOfflineWorld("aven", VictoryMode.Conquest);
            var first = NetworkObservation.Export(authority, 1, 1);
            var replica = World.CreateNetworkReplica(authority.Definition, authority.Map, first);
            CreateControls(replica, State("active", 7));
            int worker = 0; foreach (var unit in replica.Units) if (unit.OwnerId == 1) { worker = unit.Id; break; }
            Assert.Greater(worker, 0);
            var oldPoll = Poll(); Assert.IsTrue(replica.Submit(new StopCommand(1, new[] { worker })).Accepted);
            var sending = SendNext(); transport.Fail(2); yield return Complete(sending);
            transport.Reply(1, new OnlineSnapshotReply { ok = true, observation = NetworkObservation.Export(authority, 1, 2), lastAcceptedSequence = 7, state = State("active", 7) });
            yield return Complete(oldPoll);
            Assert.AreEqual(1, replica.NetworkSnapshotSequence, "An earlier request cannot end recovery using a stale command cursor.");
            Assert.IsTrue(online.BlocksWorldInput);
            var freshPoll = Poll(); transport.Reply(3, new OnlineSnapshotReply { ok = true, observation = NetworkObservation.Export(authority, 1, 3), lastAcceptedSequence = 8, state = State("active", 8) });
            yield return Complete(freshPoll);
            Assert.IsFalse(online.BlocksWorldInput);
            Assert.IsTrue(replica.Submit(new StopCommand(1, new[] { worker })).Accepted);
            var next = SendNext(); Assert.AreEqual(9, JsonUtility.FromJson<OnlineCommandRequest>(transport.Bodies[4]).sequence);
            transport.Reply(4, new OnlineCommandReply { ok = true, accepted = true, lastAcceptedSequence = 9 }); yield return Complete(next);
            Assert.AreEqual("Order accepted by the server.", match.Feedback);
        }

        [UnityTest]
        public IEnumerator AResultArrivingDuringAnotherProfileReadStillSchedulesItsOwnRefresh()
        {
            yield return Login(); var authority = DefinitionLoader.CreateOfflineWorld("aven", VictoryMode.Conquest);
            var replica = World.CreateNetworkReplica(authority.Definition, authority.Map, NetworkObservation.Export(authority, 1, 1));
            CreateControls(replica, State("active"));
            var pendingSnapshot = Poll(); online.Profile();
            Assert.IsTrue(authority.Submit(new SurrenderCommand(1)).Accepted);
            var terminal = State("finished"); terminal.result = new OnlineResult { winnerPlayerId = 2, reason = "Surrender" };
            transport.Reply(1, new OnlineSnapshotReply { ok = true, observation = NetworkObservation.Export(authority, 1, 2), state = terminal });
            yield return Complete(pendingSnapshot); Assert.IsTrue(replica.Match.IsFinished); Assert.IsTrue(online.Busy);
            transport.Json(2, "{\"ok\":true,\"profile\":{\"username\":\"First\",\"ratings\":[]}}");
            for (int i = 0; i < 300 && transport.Pending.Count < 4; i++) yield return null;
            Assert.AreEqual(4, transport.Pending.Count); transport.Json(3, "{\"ok\":true,\"matches\":[]}");
            for (int i = 0; i < 300 && transport.Pending.Count < 5; i++) yield return null;
            Assert.AreEqual(5, transport.Pending.Count); transport.Reply(4, terminal); yield return Complete(Operation);
            online.Update();
            Assert.AreEqual(6, transport.Pending.Count); Assert.AreEqual("/v1/profile", transport.Paths[5]);
            Assert.IsTrue(online.IsOpen); Assert.IsTrue(online.Busy);
        }
    }
}
