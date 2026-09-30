using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Emberfield.Networking;
using Emberfield.Presentation;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Emberfield.Tests.PlayMode
{
    public sealed class FrontierSecurityTests
    {
        private sealed class ControlledTransport : HttpMessageHandler
        {
            internal readonly List<TaskCompletionSource<HttpResponseMessage>> Pending = new List<TaskCompletionSource<HttpResponseMessage>>();
            internal readonly List<string> Bearers = new List<string>();
            internal readonly List<string> Paths = new List<string>();
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellation)
            {
                var completion = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
                Pending.Add(completion); Bearers.Add(request.Headers.Authorization?.Parameter); Paths.Add(request.RequestUri.AbsolutePath);
                cancellation.Register(() => completion.TrySetCanceled());
                return completion.Task;
            }
            internal void Reply(int index, string json, HttpStatusCode status = HttpStatusCode.OK)
            { Pending[index].SetResult(new HttpResponseMessage(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") }); }
            internal void Login(int index, string name) => Reply(index, "{\"ok\":true,\"token\":\"" + name + "-session\",\"profile\":{\"username\":\"" + name + "\",\"ratings\":[]}}");
        }
        private static IEnumerator Complete(Task task)
        {
            for (int i = 0; i < 300 && !task.IsCompleted; i++) yield return null;
            Assert.IsTrue(task.IsCompleted, "The controlled transport did not complete within 300 frames.");
        }

        [UnityTest]
        public IEnumerator ADelayedLoginCannotReplaceANewerAccountSession()
        {
            var transport = new ControlledTransport(); using (var service = new OnlineService(transport))
            {
                var older = service.SignIn("Older", "test-password", false);
                var newer = service.SignIn("Newer", "test-password", false);
                transport.Login(1, "Newer"); yield return Complete(newer);
                Assert.IsFalse(newer.IsFaulted); int revision = service.SessionRevision;
                transport.Login(0, "Older"); yield return Complete(older);
                Assert.IsTrue(older.IsFaulted); Assert.That(older.Exception.GetBaseException().Message, Does.Contain("superseded"));
                Assert.AreEqual("Newer", service.Profile.username); Assert.AreEqual(revision, service.SessionRevision);
            }
        }

        [UnityTest]
        public IEnumerator ADelayedLogoutRevokesTheOutgoingBearerWithoutErasingTheNextLogin()
        {
            var transport = new ControlledTransport(); using (var service = new OnlineService(transport))
            {
                var first = service.SignIn("First", "test-password", false); transport.Login(0, "First"); yield return Complete(first);
                var logout = service.SignOut(); Assert.IsFalse(service.SignedIn); Assert.AreEqual("First-session", transport.Bearers[1]);
                var next = service.SignIn("Next", "test-password", false); transport.Login(2, "Next"); yield return Complete(next);
                transport.Reply(1, "{\"ok\":true}"); yield return Complete(logout);
                Assert.IsFalse(logout.IsFaulted); Assert.IsTrue(service.SignedIn); Assert.AreEqual("Next", service.Profile.username);
            }
        }

        [UnityTest]
        public IEnumerator AnOldProfileResponseCannotRevealThePreviousAccountsProfile()
        {
            var transport = new ControlledTransport(); using (var service = new OnlineService(transport))
            {
                var first = service.SignIn("First", "test-password", false); transport.Login(0, "First"); yield return Complete(first);
                var oldProfile = service.RefreshProfile();
                var next = service.SignIn("Next", "test-password", false); transport.Login(2, "Next"); yield return Complete(next);
                transport.Reply(1, "{\"ok\":true,\"profile\":{\"username\":\"First\",\"ratings\":[]}}"); yield return Complete(oldProfile);
                Assert.IsFalse(oldProfile.IsFaulted); Assert.AreEqual("Next", service.Profile.username);
            }
        }

        [UnityTest]
        public IEnumerator SessionExpiryInvalidatesCachedWardrobeIdentity()
        {
            var transport = new ControlledTransport(); using (var service = new OnlineService(transport))
            {
                var first = service.SignIn("First", "test-password", false); transport.Login(0, "First"); yield return Complete(first);
                int revision = service.SessionRevision;
                var request = service.Send<OnlineReply>("/v1/cosmetics");
                transport.Reply(1, "{\"ok\":false,\"error\":\"session_expired\"}", HttpStatusCode.Unauthorized); yield return Complete(request);
                Assert.IsTrue(request.IsFaulted); Assert.IsNotNull(request.Exception);
                Assert.IsFalse(service.SignedIn); Assert.IsNull(service.Profile); Assert.Greater(service.SessionRevision, revision);
            }
        }

        [UnityTest]
        public IEnumerator ChangingServerRejectsALateLoginFromThePreviousEndpoint()
        {
            var transport = new ControlledTransport(); using (var service = new OnlineService(transport))
            {
                var previousServer = service.SignIn("SameName", "test-password", false);
                int revision = service.SessionRevision; service.Configure("https://another-server.example.test");
                Assert.Greater(service.SessionRevision, revision);
                transport.Login(0, "SameName"); yield return Complete(previousServer);
                Assert.IsTrue(previousServer.IsFaulted); Assert.That(previousServer.Exception.GetBaseException().Message, Does.Contain("superseded"));
                Assert.IsFalse(service.SignedIn); Assert.IsNull(service.Profile);
                Assert.AreEqual("https://another-server.example.test", service.Address);
            }
        }

        [UnityTest]
        public IEnumerator HealthCheckUsesThePublicRouteWithoutSendingTheAccountsBearer()
        {
            var transport = new ControlledTransport(); using (var service = new OnlineService(transport))
            {
                var login = service.SignIn("First", "test-password", false); transport.Login(0, "First"); yield return Complete(login);
                var check = service.CheckConnection();
                Assert.AreEqual("/health", transport.Paths[1]); Assert.IsNull(transport.Bearers[1]);
                transport.Reply(1, "{\"ok\":true,\"status\":\"ready\",\"protocolVersion\":" + NetworkBuild.ProtocolVersion + ",\"contentVersion\":\"" + NetworkBuild.ContentVersion + "\"}");
                yield return Complete(check); Assert.IsFalse(check.IsFaulted); Assert.IsTrue(service.SignedIn);
            }
        }

        [UnityTest]
        public IEnumerator AReachableServiceWithDifferentContentCannotPassTheConnectionCheck()
        {
            var transport = new ControlledTransport(); using (var service = new OnlineService(transport))
            {
                var check = service.CheckConnection();
                transport.Reply(0, "{\"ok\":true,\"status\":\"ready\",\"protocolVersion\":" + NetworkBuild.ProtocolVersion + ",\"contentVersion\":\"older-content\"}");
                yield return Complete(check); Assert.IsTrue(check.IsFaulted);
                Assert.That(check.Exception.GetBaseException().Message, Does.Contain("different versions"));
            }
        }

        [UnityTest]
        public IEnumerator LargeObservationsFitTheServerEnvelopeButOversizedResponsesRemainBounded()
        {
            var transport = new ControlledTransport(); using (var service = new OnlineService(transport))
            {
                var supported = service.Send<OnlineReply>("/v1/matches/current/snapshot");
                transport.Reply(0, "{\"ok\":true,\"padding\":\"" + new string('x', 3 * 1024 * 1024) + "\"}");
                yield return Complete(supported); Assert.IsFalse(supported.IsFaulted, supported.Exception?.ToString());
                var oversized = service.Send<OnlineReply>("/v1/matches/current/snapshot");
                transport.Reply(1, "{\"ok\":true,\"padding\":\"" + new string('x', OnlineService.MaximumResponseBytes) + "\"}");
                yield return Complete(oversized); Assert.IsTrue(oversized.IsFaulted);
                Assert.That(oversized.Exception.GetBaseException().Message, Does.Contain("exceeded the limit"));
            }
        }
    }
}
