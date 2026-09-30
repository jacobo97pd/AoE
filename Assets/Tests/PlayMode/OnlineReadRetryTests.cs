using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Emberfield.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Emberfield.Tests.PlayMode
{
    public sealed class OnlineReadRetryTests
    {
        private sealed class ControlledTransport : HttpMessageHandler
        {
            internal readonly List<TaskCompletionSource<HttpResponseMessage>> Pending = new List<TaskCompletionSource<HttpResponseMessage>>();
            internal readonly List<string> Paths = new List<string>(), Bearers = new List<string>();
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellation)
            {
                var completion = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
                Pending.Add(completion); Paths.Add(request.RequestUri.AbsolutePath); Bearers.Add(request.Headers.Authorization?.Parameter);
                cancellation.Register(() => completion.TrySetCanceled()); return completion.Task;
            }
            internal void Reply(int index, string json, HttpStatusCode status = HttpStatusCode.OK) => Pending[index].SetResult(
                new HttpResponseMessage(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") });
            internal void Reset(int index) => Pending[index].SetException(new HttpRequestException("Controlled connection reset"));
            internal void Login(int index, string name) => Reply(index, "{\"ok\":true,\"token\":\"" + name + "-session\",\"profile\":{\"username\":\"" + name + "\",\"ratings\":[]}}");
        }
        private sealed class ResetResponseBody : HttpContent
        {
            protected override bool TryComputeLength(out long length) { length = 0; return false; }
            protected override Task SerializeToStreamAsync(Stream stream, TransportContext context) => Task.FromException(new IOException("Controlled body reset"));
            protected override Task<Stream> CreateContentReadStreamAsync() => Task.FromResult<Stream>(new ResetReadStream());
            private sealed class ResetReadStream : Stream
            {
                public override bool CanRead => true;
                public override bool CanSeek => false;
                public override bool CanWrite => false;
                public override long Length => throw new NotSupportedException();
                public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
                public override int Read(byte[] buffer, int offset, int count) => throw new IOException("Controlled body reset");
                public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken token) => Task.FromException<int>(new IOException("Controlled body reset"));
                public override void Flush() { }
                public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
                public override void SetLength(long value) => throw new NotSupportedException();
                public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
            }
        }
        private static IEnumerator Until(Func<bool> complete)
        {
            float until = Time.realtimeSinceStartup + 3;
            while (!complete() && Time.realtimeSinceStartup < until) yield return null;
            Assert.IsTrue(complete(), "The bounded controlled read did not complete.");
        }
        private static IEnumerator SignIn(OnlineService service, ControlledTransport transport, string name = "First")
        {
            int index = transport.Pending.Count; var task = service.SignIn(name, "controlled-password", false);
            transport.Login(index, name); yield return Until(() => task.IsCompleted); Assert.IsFalse(task.IsFaulted);
        }

        [UnityTest]
        public IEnumerator ProfileConnectionResetRetriesOnceAndRefreshesTheSameAccount()
        {
            var transport = new ControlledTransport(); using (var service = new OnlineService(transport))
            {
                yield return SignIn(service, transport); var read = service.RefreshProfile(); transport.Reset(1);
                yield return Until(() => transport.Pending.Count == 3);
                Assert.AreEqual("/v1/profile", transport.Paths[2]); Assert.AreEqual("First-session", transport.Bearers[2]);
                transport.Reply(2, "{\"ok\":true,\"profile\":{\"username\":\"First\",\"gamesPlayed\":7,\"ratings\":[]}}");
                yield return Until(() => read.IsCompleted);
                Assert.IsFalse(read.IsFaulted); Assert.AreEqual(7, service.Profile.gamesPlayed); Assert.AreEqual(3, transport.Pending.Count);
            }
        }

        [UnityTest]
        public IEnumerator AResetWhileStreamingTheHistoryBodyRetriesTheIdempotentRead()
        {
            var transport = new ControlledTransport(); using (var service = new OnlineService(transport))
            {
                yield return SignIn(service, transport); var read = service.Send<OnlineHistoryReply>("/v1/history/historical");
                transport.Pending[1].SetResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ResetResponseBody() });
                yield return Until(() => transport.Pending.Count == 3);
                transport.Reply(2, "{\"ok\":true,\"matches\":[{\"matchId\":\"confirmed-result\"}]}"); yield return Until(() => read.IsCompleted);
                Assert.IsFalse(read.IsFaulted); Assert.AreEqual("confirmed-result", read.Result.matches[0].matchId);
            }
        }

        [UnityTest]
        public IEnumerator HistoryReadsRecoverFromOneResetAndPreserveTheRequestedRealm()
        {
            foreach (string path in new[] { "/v1/history", "/v1/history/historical", "/v1/history/fantasy", "/v1/history/naval" })
            {
                var transport = new ControlledTransport(); using (var service = new OnlineService(transport))
                {
                    yield return SignIn(service, transport); var read = service.Send<OnlineHistoryReply>(path); transport.Reset(1);
                    yield return Until(() => transport.Pending.Count == 3); Assert.AreEqual(path, transport.Paths[2]);
                    transport.Reply(2, "{\"ok\":true,\"matches\":[{\"matchId\":\"confirmed-result\"}]}"); yield return Until(() => read.IsCompleted);
                    Assert.IsFalse(read.IsFaulted); Assert.AreEqual("confirmed-result", read.Result.matches[0].matchId);
                }
            }
        }

        [UnityTest]
        public IEnumerator TwoConnectionResetsSurfaceTheFailureWithoutAThirdAttempt()
        {
            var transport = new ControlledTransport(); using (var service = new OnlineService(transport))
            {
                yield return SignIn(service, transport); var read = service.Send<OnlineHistoryReply>("/v1/history/fantasy"); transport.Reset(1);
                yield return Until(() => transport.Pending.Count == 3); transport.Reset(2); yield return Until(() => read.IsCompleted);
                Assert.IsTrue(read.IsFaulted); Assert.IsInstanceOf<HttpRequestException>(read.Exception.GetBaseException());
                Assert.AreEqual(3, transport.Pending.Count); Assert.IsTrue(service.SignedIn);
            }
        }

        [UnityTest]
        public IEnumerator AuthenticationAndCommandPostsAreNotRetriedAfterAConnectionReset()
        {
            var transport = new ControlledTransport(); using (var service = new OnlineService(transport))
            {
                var login = service.SignIn("First", "controlled-password", false); transport.Reset(0); yield return Until(() => login.IsCompleted);
                Assert.IsTrue(login.IsFaulted); _ = login.Exception; Assert.AreEqual(1, transport.Pending.Count);
                yield return SignIn(service, transport);
                var command = service.Send<OnlineCommandReply>("/v1/matches/current/commands", "POST", new OnlineCommandRequest());
                transport.Reset(2); yield return Until(() => command.IsCompleted);
                Assert.IsTrue(command.IsFaulted); _ = command.Exception; Assert.AreEqual(3, transport.Pending.Count);
            }
        }

        [UnityTest]
        public IEnumerator AuthenticationAndVersionFailuresDoNotTriggerReadRetries()
        {
            foreach (var status in new[] { HttpStatusCode.Unauthorized, (HttpStatusCode)426 })
            {
                var transport = new ControlledTransport(); using (var service = new OnlineService(transport))
                {
                    yield return SignIn(service, transport); var read = service.Send<OnlineHistoryReply>("/v1/history/historical");
                    transport.Reply(1, "{\"ok\":false,\"error\":\"" + (status == HttpStatusCode.Unauthorized ? "session_expired" : "client_version_mismatch") + "\"}", status);
                    yield return Until(() => read.IsCompleted);
                    Assert.IsTrue(read.IsFaulted); _ = read.Exception; Assert.AreEqual(2, transport.Pending.Count);
                    Assert.AreEqual(status != HttpStatusCode.Unauthorized, service.SignedIn);
                }
            }
        }

        [UnityTest]
        public IEnumerator AConnectionFailureCannotRetryIntoANewerAccount()
        {
            var transport = new ControlledTransport(); using (var service = new OnlineService(transport))
            {
                yield return SignIn(service, transport); var read = service.Send<OnlineHistoryReply>("/v1/history/fantasy");
                transport.Reset(1); yield return null; // Allow the failure to schedule its delay before changing the session.
                yield return SignIn(service, transport, "Next"); yield return Until(() => read.IsCompleted);
                Assert.IsTrue(read.IsFaulted || read.IsCanceled); _ = read.Exception;
                Assert.AreEqual("Next", service.Profile.username); Assert.AreEqual(3, transport.Pending.Count);
                Assert.AreEqual("/v1/login", transport.Paths[2]);
            }
        }

        [UnityTest]
        public IEnumerator ExplicitCancellationDoesNotRetryOrRevokeTheAccount()
        {
            var transport = new ControlledTransport(); using (var service = new OnlineService(transport))
            using (var cancellation = new CancellationTokenSource())
            {
                yield return SignIn(service, transport);
                var read = service.ExchangeAsync("/v1/history", "GET", null, cancellation.Token);
                transport.Reset(1); cancellation.Cancel(); yield return Until(() => read.IsCompleted);
                Assert.IsTrue(read.IsFaulted || read.IsCanceled); _ = read.Exception;
                Assert.AreEqual(2, transport.Pending.Count); Assert.AreEqual("First", service.Profile.username); Assert.IsTrue(service.SignedIn);
            }
        }
    }
}
