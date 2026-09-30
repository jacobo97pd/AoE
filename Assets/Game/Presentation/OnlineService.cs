using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Emberfield.Networking;
using Emberfield.Simulation;
using UnityEngine;

namespace Emberfield.Presentation
{
    /// <summary>In-memory credentials, bounded HTTP messages and platform TLS validation.</summary>
    public sealed class OnlineService : IDisposable, IMatchTransport, IMatchResultService
    {
        public const string DefaultAddress = "http://127.0.0.1:8787";
        // The service permits a 4 MiB observation; allow its bounded state/seat wrapper too.
        public const int MaximumResponseBytes = 5 * 1024 * 1024;
        public string Address { get; private set; } = DefaultAddress;
        public bool SignedIn => !string.IsNullOrEmpty(token);
        public bool IsConnected => SignedIn;
        public OnlineProfile Profile { get; private set; }
        public int SessionRevision { get; private set; }
        private readonly HttpClient http;
        private string token;
        public OnlineService() : this(new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false }) { }
        public OnlineService(HttpMessageHandler handler)
        { http = new HttpClient(handler ?? throw new ArgumentNullException(nameof(handler))); http.Timeout = TimeSpan.FromSeconds(6); }
        public void Configure(string address)
        {
            if (!TryAddress(address, out var uri)) throw new ArgumentException("Use HTTPS, or HTTP on this computer (127.0.0.1).");
            string normalized = uri.GetLeftPart(UriPartial.Authority);
            if (normalized != Address && SignedIn) throw new InvalidOperationException("Sign out before changing server.");
            if (normalized != Address) SessionRevision++;
            Address = normalized;
        }
        public static bool TryAddress(string address, out Uri uri)
        {
            if (!Uri.TryCreate(address?.Trim(), UriKind.Absolute, out uri) || !string.IsNullOrEmpty(uri.UserInfo) ||
                !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment) || uri.AbsolutePath != "/") return false;
            return uri.Scheme == "https" || uri.Scheme == "http" &&
                (uri.Host == "localhost" || IPAddress.TryParse(uri.DnsSafeHost, out var ip) && IPAddress.IsLoopback(ip));
        }
        public static string InitialAddress(string commandLine, string saved, string packaged)
        {
            if (!string.IsNullOrWhiteSpace(commandLine))
            {
                if (!TryAddress(commandLine, out var explicitUri)) throw new ArgumentException("The configured server address requires HTTPS outside this computer.");
                return explicitUri.GetLeftPart(UriPartial.Authority);
            }
            if (TryAddress(saved, out var savedUri)) return savedUri.GetLeftPart(UriPartial.Authority);
            return TryAddress(packaged, out var packagedUri) ? packagedUri.GetLeftPart(UriPartial.Authority) : DefaultAddress;
        }
        public async Task CheckConnection()
        {
            string json = await ExchangeResourceAsync("/health", "GET", null, CancellationToken.None, false);
            var health = JsonUtility.FromJson<OnlineHealth>(json);
            if (health.protocolVersion != NetworkBuild.ProtocolVersion || health.contentVersion != NetworkBuild.ContentVersion)
                throw new InvalidOperationException(FriendlyError("client_version_mismatch"));
            if (health.status != "ready") throw new InvalidOperationException(FriendlyError("authority_unavailable"));
        }
        public async Task SignIn(string username, string password, bool register)
        {
            int revision = ++SessionRevision;
            var reply = await Send<AuthReply>(register ? "/v1/register" : "/v1/login", "POST", new Credentials { username = username.Trim(), password = password });
            if (revision != SessionRevision) throw new InvalidOperationException("This sign-in was superseded by a newer account action.");
            if (string.IsNullOrEmpty(reply.token)) throw new InvalidOperationException("The server did not return a session.");
            token = reply.token; Profile = reply.profile; SessionRevision++;
        }
        public async Task SignOut()
        {
            int revision = ++SessionRevision;
            // Send captures the outgoing bearer before local credentials are removed. A delayed logout cannot erase a newer login.
            var pending = Send<OnlineReply>("/v1/logout", "POST"); token = null; Profile = null;
            try { await pending; } finally { if (revision == SessionRevision) { token = null; Profile = null; } }
        }
        public async Task RefreshProfile()
        {
            int revision = SessionRevision; var reply = await Send<ProfileReply>("/v1/profile");
            if (revision == SessionRevision && SignedIn) Profile = reply.profile;
        }
        public async Task<T> Send<T>(string path, string method = "GET", object body = null) where T : OnlineReply
        {
            string json = body == null ? null : JsonUtility.ToJson(body);
            var reply = await ExchangeAsync(path, method, json, CancellationToken.None);
            return JsonUtility.FromJson<T>(reply);
        }
        public async Task<NetworkResultReceipt> ReadResultAsync()
        {
            var state = await Send<OnlineState>("/v1/state");
            var result = state.Result;
            return result == null ? null : new NetworkResultReceipt { WinnerPlayerId = result.winnerPlayerId,
                Reason = result.reason, Tick = result.tick, DurationSeconds = result.durationSeconds };
        }
        public async Task<string> ExchangeAsync(string path, string method, string requestJson, CancellationToken cancellation)
        {
            if (string.IsNullOrEmpty(path) || !path.StartsWith("/v1/", StringComparison.Ordinal) || path.Contains("?") || path.Contains("#"))
                throw new ArgumentException("Invalid service resource.");
            int revision = SessionRevision;
            bool retryRead = method == "GET" && (path == "/v1/profile" || path == "/v1/history" ||
                path == "/v1/history/historical" || path == "/v1/history/fantasy" || path == "/v1/history/naval");
            try { return await ExchangeResourceAsync(path, method, requestJson, cancellation, true); }
            catch (Exception error) when (retryRead && SignedIn && revision == SessionRevision && !cancellation.IsCancellationRequested &&
                (error is HttpRequestException || error is IOException || error is TaskCanceledException))
            {
                // A reused connection may be reset while a batch of results is committed. Retry only these idempotent reads,
                // once, with the original per-request deadline and without carrying the request into another account session.
                await Task.Delay(250, cancellation);
                if (!SignedIn || revision != SessionRevision) throw new OperationCanceledException("The account changed before the profile request could retry.");
                return await ExchangeResourceAsync(path, method, requestJson, cancellation, true);
            }
        }
        private async Task<string> ExchangeResourceAsync(string path, string method, string requestJson, CancellationToken cancellation, bool authenticated)
        {
            using (var request = new HttpRequestMessage(new HttpMethod(method), Address + path))
            {
                string requestToken = authenticated ? token : null; int revision = SessionRevision;
                if (!string.IsNullOrEmpty(requestToken)) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", requestToken);
                request.Headers.Add("X-Emberfield-Protocol", NetworkBuild.ProtocolVersion.ToString());
                request.Headers.Add("X-Emberfield-Content", NetworkBuild.ContentVersion);
                if (requestJson != null || method == "POST") request.Content = new StringContent(requestJson ?? "{}", Encoding.UTF8, "application/json");
                using (var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation))
                {
                deadline.CancelAfter(TimeSpan.FromSeconds(6));
                using (var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, deadline.Token))
                {
                    const int maximum = MaximumResponseBytes;
                    if (response.Content.Headers.ContentLength > maximum) throw new InvalidOperationException("Server response exceeded the limit.");
                    using (var stream = await response.Content.ReadAsStreamAsync())
                    using (var data = new MemoryStream())
                    {
                        var buffer = new byte[8192]; int count;
                        while ((count = await stream.ReadAsync(buffer, 0, buffer.Length, deadline.Token)) > 0)
                        { if (data.Length + count > maximum) throw new InvalidOperationException("Server response exceeded the limit."); data.Write(buffer, 0, count); }
                        string json = Encoding.UTF8.GetString(data.ToArray());
                        OnlineReply reply;
                        try { reply = JsonUtility.FromJson<OnlineReply>(json); }
                        catch (ArgumentException) { throw new InvalidOperationException("This address did not return an Emberfield service response. Check the server address."); }
                        if (authenticated && response.StatusCode == HttpStatusCode.Unauthorized && token == requestToken && revision == SessionRevision)
                        { token = null; Profile = null; SessionRevision++; }
                        if (reply == null || !reply.ok || !response.IsSuccessStatusCode)
                            throw new InvalidOperationException(FriendlyError(reply?.error));
                        return json;
                    }
                }
                }
            }
        }
        public void Dispose() { token = null; Profile = null; SessionRevision++; http.Dispose(); }
        private static string FriendlyError(string error)
        {
            switch (error)
            {
                case "invalid_credentials": return "Username or password is incorrect.";
                case "username_unavailable": return "That username is already registered. Sign in or choose another.";
                case "username_3_to_24_letters_digits_underscore_password_10_to_128_bytes": return "Use a username of 3-24 letters, digits or underscores and a password of 10-128 bytes.";
                case "authentication_required": case "session_expired": return "Sign in again to resume your session.";
                case "client_version_mismatch": return "The game and server use different versions. Update them to the same build.";
                case "faction_realm_mismatch": return "Históricas, fantasía y navales tienen PvP separados. Elige el ámbito de la sala antes de entrar.";
                case "unsupported_realm_or_map": return "Choose a supported PvP realm and battlefield.";
                case "payment_provider_unconfigured": return "Real purchases are not available in this alpha. No payment was taken.";
                case "cosmetic_not_owned": return "This account does not own that appearance.";
                case "sandbox_disabled": return "The server has not enabled free alpha cosmetic claims.";
                case "invalid_purchase_receipt": case "purchase_receipt_already_used": return "The purchase receipt could not be verified for this account and item.";
                case "unknown_cosmetic": return "That appearance is not available in this catalog.";
                case "room_unavailable": case "invalid_room_or_faction": return "That room is unavailable. Check its code or create another.";
                case "already_in_room": case "already_queued": return "Leave the current room or cancel matchmaking first.";
                case "surrender_before_leaving_active_match": return "Surrender before leaving an active match.";
                case "no_current_match": return "There is no active server match for this account.";
                case "service_unavailable": case "authority_unavailable": return "The server is temporarily unavailable. Try again shortly.";
                case "room_capacity_reached": case "queue_capacity_reached": return "The server is full. Try again shortly.";
            }
            if (error != null && (error.EndsWith("rate_limited", StringComparison.Ordinal) || error == "authentication_busy")) return "Too many requests. Wait a moment before trying again.";
            return "The server could not complete this request. Refresh the lobby and try again.";
        }
        [Serializable] private sealed class Credentials { public string username; public string password; }
        [Serializable] private sealed class AuthReply : OnlineReply { public string token; public OnlineProfile profile; }
        [Serializable] private sealed class ProfileReply : OnlineReply { public OnlineProfile profile; }
        [Serializable] private sealed class OnlineHealth : OnlineReply { public int protocolVersion; public string contentVersion; public string status; }
    }

    [Serializable] public sealed class OnlineCosmeticsReply : OnlineReply { public CosmeticCatalogItem[] items; public string[] ownedIds; public CosmeticEquippedItem[] equipped; public CosmeticEntitlement[] entitlements; public bool sandboxEnabled; public bool purchasesAvailable; }
    [Serializable] public sealed class CosmeticEntitlement { public string itemId; public string source; }
    [Serializable] public sealed class OnlineCosmeticRequest { public string itemId; public string idempotencyKey; public string receipt; }
    [Serializable] public class OnlineReply { public bool ok; public string error; }
    [Serializable] public sealed class OnlineProfile { public string username; public OnlineRating[] ratings; public int gamesPlayed; }
    [Serializable] public sealed class OnlineRating { public string queue; public string realmId; public string visibleRank; public int rankPoints; public int gamesPlayed; public int wins; public int losses; }
    [Serializable] public sealed class OnlineSeat { public int playerId; public string username; public string factionId; public CosmeticEquippedItem[] cosmetics; public bool ready; public bool connected; }
    [Serializable] public sealed class OnlineResult { public int winnerPlayerId; public string reason; public long tick; public double durationSeconds; }
    [Serializable] public sealed class OnlineState : OnlineReply
    {
        public string realmId; public string mapId; public string status; public string roomCode; public string matchId; public string queue; public string mode;
        public int playerId; public long lastAcceptedSequence; public OnlineSeat[] players;
        public int queueSeconds; public int reconnectGraceSeconds; public int afkTimeoutSeconds; public int afkSecondsRemaining; public OnlineResult result;
        // JsonUtility may materialize an empty inline class for JSON null. Only terminal receipts are results.
        public OnlineResult Result => (status == "finished" || status == "aborted") && result != null && !string.IsNullOrEmpty(result.reason) ? result : null;
    }
    [Serializable] public sealed class OnlineSnapshotReply : OnlineReply { public NetworkSnapshot observation; public long lastAcceptedSequence; public OnlineState state; }
    [Serializable] public sealed class OnlineCommandReply : OnlineReply { public bool accepted; public long lastAcceptedSequence; public int entityId; }
    [Serializable] public sealed class OnlineRoomRequest { public string realmId; public string mapId; public string factionId; public string mode; public string code; public string queue; public bool ready; }
    [Serializable] public sealed class OnlineCommandRequest { public long sequence; public NetworkCommandEnvelope command; }
    [Serializable] public sealed class OnlineHistoryReply : OnlineReply { public OnlineHistoryEntry[] matches; }
    [Serializable] public sealed class OnlineHistoryEntry { public string realmId; public string mapId; public string matchId; public string queue; public string mode; public string outcome; public string opponent; public string reason; public double durationSeconds; public string playedAt; public OnlineStatistics statistics; }
    [Serializable] public sealed class OnlineStatistics { public int eraTier; public int workersRemaining; public int armyRemaining; public int buildingsRemaining; public int food; public int wood; public int metal; public int stone; public int technologiesCompleted; }
}
