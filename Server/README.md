# Emberfield alpha service

This Node service runs separate historical, fantasy and naval authoritative Emberfield matches through a pool of C# worker processes, stores accounts/results in SQLite, and exposes the private, casual, and ranked 1v1 flows used by the Unity client. It requires Node 22.12+ and the .NET 8 runtime. There are no npm dependencies, cloud accounts, or paid service activations.

From the repository root:

```powershell
node Server/update-content-version.mjs
./tools/Build-Authority.ps1
node --experimental-sqlite Server/server.mjs
```

The default endpoint is `http://127.0.0.1:8787`. Leave the console running while playing. A second client on this computer can register a different account, join the displayed room code, and ready up. A private room starts only after both players are ready. Queue entries match with other live accounts in the same realm, battlefield, queue and victory mode. Realm membership and allowed maps are checked again inside the C# authority, including private rooms. The three realms cannot play one another; mirror matches are supported.

| Realm | Active faction IDs and labels | Allowed maps |
| --- | --- | --- |
| `historical` | `aven` (Franceses), `serevin` (Hispanos), `english` (Ingleses), `sultanate` (Sultanato), `sahel` (Confederación del Sahel) | `amber_crossing`, `sapphire_coast`, `sunscar_basin` |
| `fantasy` | `ashen` (Orcos), `drakeforged` (Enanos), `skeld` (Hombres de las montañas), `verdant` (Elfos) | The three historical maps plus fantasy-exclusive `legend_lands` |
| `naval` | `pirates` (Piratas), including Pirates versus Pirates | `sapphire_coast` only |

The first naval slice lets pirate treasure seekers build coastal docks, train pirate sloops, fight at sea and transport troops. Naval matches load the connected `sapphire_coast_naval.json` resource through the shared realm resolver while retaining the public map ID `sapphire_coast`; historical and fantasy matches retain the original coastal layout. Embarkation, landing and own cargo restoration on client reconnection use the C# authority. The English navy, Spanish navy and skeleton fleet remain planned cards, with no selectable API faction. This describes the local build's functionality, not deployment or verification of a public endpoint. `miraj` and `solar` remain legacy definitions/history only and cannot enter new rooms or queues. Historical records retain their original faction, realm and content version, including older historical `skeld` results.

The built-in SQLite API in the installed Node 22.12 runtime requires the experimental flag. Password derivation uses asynchronous built-in scrypt with a random per-account salt. See the versioned official [SQLite](https://nodejs.org/download/release/v22.12.0/docs/api/sqlite.html) and [crypto](https://nodejs.org/download/release/v22.12.0/docs/api/crypto.html) documentation.

## Configuration

| Environment variable | Default | Purpose |
| --- | --- | --- |
| `EMBERFIELD_HOST` | `127.0.0.1` | Bind address. External listening requires TLS files. |
| `EMBERFIELD_PORT` | `8787` | Listening port. |
| `EMBERFIELD_DATABASE` | `Server/data/emberfield.sqlite` | Durable account, hashed session, rating, and match database. |
| `EMBERFIELD_AUTHORITY_WORKERS` | `4` | Bounded C# worker count, 1–16. Each match stays on one worker. |
| `EMBERFIELD_MATCHES_PER_WORKER` | `8` | Slots per worker, 1–16. Default total capacity is 32 simultaneous 1v1 matches / 64 players. |
| `EMBERFIELD_TRUST_CLOUDFLARE_LOOPBACK` | unset | Set to `1` only for a dedicated local Cloudflare tunnel origin. Accepts valid `CF-Connecting-IP` only when the actual socket peer is loopback. |
| `EMBERFIELD_COSMETIC_SANDBOX` | unset | Set to `1` only for explicit free alpha cosmetic entitlements; these are labeled `sandbox`, never purchases. |
| `EMBERFIELD_TLS_CERT` | unset | PEM certificate chain for HTTPS. |
| `EMBERFIELD_TLS_KEY` | unset | PEM private key; keep in ignored `Server/certs` or outside the repository. |
| `AUTHORITY_COMMAND` | `dotnet` | Worker runtime executable. |
| `AUTHORITY_ARGS_JSON` | absolute path to `Server/AuthorityWorker/out/Emberfield.Authority.dll` as JSON array | Worker arguments. No shell evaluation is used. |

Public deployment and certificate issuance are operator steps. External clients need a valid HTTPS endpoint; the client does not disable certificate validation. Run one Node service instance per database; it owns the configured worker pool. A process lock prevents a second instance from modifying the first instance's live matches; stale locks from a dead process are recovered on restart. Multiple machines, multi-region matchmaking, e-mail recovery, and production abuse moderation are outside this alpha. Back up the database using SQLite backup tooling or copy it after gracefully stopping the service; an active WAL database consists of more than its main `.sqlite` file.

Proxy trust is off by default. A dedicated Cloudflare tunnel may opt into `EMBERFIELD_TRUST_CLOUDFLARE_LOOPBACK=1` so authentication quotas use the visitor IP rather than sharing one loopback quota. The service trusts only a single syntactically valid `CF-Connecting-IP` from a loopback socket; missing/malformed headers and non-loopback peers use the actual socket address. `X-Forwarded-For` is never used. This setting trusts the local proxy process and should be enabled only for that dedicated origin. Cloudflare documents this origin header in its [HTTP header reference](https://developers.cloudflare.com/fundamentals/reference/http-headers/).

## Protocol and lifecycle

`GET /health` returns protocol/content versions and worker availability. Every `/v1/` request requires `X-Emberfield-Protocol: 2` and `X-Emberfield-Content` equal to the pinned value in [content-version.mjs](content-version.mjs). The server verifies all shipped simulation/network C# source, rules JSON and map hashes on startup. Authentication returns an opaque bearer token; all other routes require `Authorization: Bearer <token>`. Request/response field names are case-sensitive. All responses contain `ok`; failures contain a short `error` code/message. POST bodies must be JSON objects, including `{}` for empty actions.

| Method and route | Body / response |
| --- | --- |
| `POST /v1/register`, `POST /v1/login` | `{username,password}` → `{token,profile}`. Names: 3–24 letters/digits/underscore. Passwords: 10–128 UTF-8 bytes. |
| `POST /v1/logout` | `{}` revokes that token. An active match continues and the reconnect clock applies. |
| `GET /v1/profile` | Own public profile, visible ranked points/tier and win/loss/draw records. Hidden MMR is never returned. |
| `GET /v1/history`, `GET /v1/history/historical`, `GET /v1/history/fantasy`, `GET /v1/history/naval` | Up to 25 recent completed/aborted matches and own authoritative post-match statistics. |
| `POST /v1/rooms` | `{realmId:'historical'|'fantasy'|'naval',mapId,factionId,mode:'Conquest'|'Dominion'}` → current state with room code. |
| `POST /v1/rooms/join` | `{code,realmId,factionId}` joins a private lobby. |
| `POST /v1/rooms/ready` | `{ready:true|false}`. Both ready starts the C# World. |
| `POST /v1/rooms/leave` | `{}` leaves lobby or dismisses finished match. An active match requires surrender first. |
| `POST /v1/queue` | `{queue:'casual'|'ranked',realmId,mapId,mode,factionId}`. Matchmaking starts automatically. |
| `DELETE /v1/queue` | Cancels own queue entry. |
| `GET /v1/state`, `POST /v1/heartbeat` | Current state and heartbeat; POST body `{}`. |
| `GET /v1/matches/current/snapshot` | Own filtered observation, sequence cursor, and state; also heartbeat. Client polls at 5 Hz. |
| `POST /v1/matches/current/commands` | `{sequence,command:<NetworkCommandEnvelope>}` → `{accepted,error,lastAcceptedSequence}`. |
| `POST /v1/surrender` | `{}` requests an authoritative forfeit for the authenticated seat. |

`lastAcceptedSequence` is the consumed command cursor: every valid next sequence is consumed once, even if game validation rejects it or a reply is lost. The nested command `Sequence` must match the wrapper. Re-login or reconnect reads the cursor from state/snapshot before issuing another command. The server injects the player ID and match ID from account membership, ignoring forged client identity fields. Command rules, unit ownership, economy, hidden targets, and results are validated by the C# World. There is no public result-write endpoint.

Each seat also retains its latest 2048 valid request IDs across re-login. IDs must contain 1–64 ASCII letters/digits/underscore/hyphen. Reusing a retained `RequestId` with a new sequence consumes that sequence but rejects the action before reaching the worker, so a paid action is not charged twice. Malformed IDs are rejected without retaining them. This bounded recent-ID window complements the strict sequence cursor; IDs older than the window are no longer retained.

Snapshots include only own private state and visible enemies/resources, plus public objectives. Server player 2 receives its own army normalized to presentation owner 1. Entity IDs remain globally stable. This is observation polling at 5 Hz over a 20 Hz authoritative simulation, with no per-render-frame transform broadcast. C# worker JSON serialization enables `System.Text.Json.JsonSerializerOptions.IncludeFields`; simulation/network DTOs use public fields and preserve their Pascal-case names. The service's outer HTTP/IPC envelopes use their documented camel-case names.

The default allows 32 concurrent lobbies/starting/active matches and 128 queue entries. Final results retain immutable, separately filtered observations for each player, while their Worlds immediately release worker capacity. Terminal views expire after ten minutes or dismissal; total retained rooms are additionally capped at twice configured live capacity, evicting older terminal views first. Durable history survives that eviction. A participant is shown disconnected after 15 seconds without heartbeat; after another 60 seconds the server forfeits the absent participant. Re-login to the same account restores its seat. Three minutes without an accepted command triggers AFK forfeit; `afkSecondsRemaining` lets the client display a warning. If both participants exceed disconnect/AFK limits, the match aborts without a rating award. Worker failure aborts only that worker's matches without awards; healthy workers continue, and admission starts a replacement child when needed. Result-persistence failure and service restart also abort without awards. Client reconnect is supported; an interrupted server process does not restore an in-progress World.

Authenticated traffic is limited to 900 requests per account per minute and 20 commands per account per second, so players sharing a public IP do not consume one another's gameplay quota. Registration/login remains limited to 12 requests per IP per minute with at most four concurrent password derivations. Up to eight authentication requests can wait for two seconds to absorb reconnect bursts; excess work is rejected. Invalid unauthenticated/version requests are limited to 120 per IP per minute. Public traffic is limited to 1200 per IP per minute. Connections and in-flight HTTP requests are globally bounded to `max(128, capacity * 8)`, and each worker bridge allows at most 256 pending messages. These are protective bounds, not a claim that any configured capacity is fast enough on arbitrary hardware.

Casual/ranked matchmaking uses internal Elo independently for each realm, queue and victory mode, for example `historical:ranked:Conquest`, `fantasy:ranked:Conquest` and `naval:ranked:Conquest`. Map preferences partition the search but do not split ratings. The initial value is 1000, K is 32, and search tolerance starts at 100 MMR, expands 50 each ten seconds, and caps at 1200. Ranked visible points use a separate +25 win / −15 loss rule, clamped at zero, with Bronze/Silver/Gold/Platinum/Diamond/Master/Legend thresholds defined in `database.mjs`. Private rooms never modify ratings. These are configurable alpha rules; competitive balance, placements, seasons, and population tuning need subsequent player testing.

Schema migration 3 only prefixes the four recognized pre-realm `casual/ranked:Conquest/Dominion` keys with `historical:`. Already scoped ratings, unknown future keys, accounts, sessions, ownership and recorded results remain unchanged. If an imported legacy key collides with an existing scoped rating, both original rows are retained for operator review instead of overwriting either. Truly old matches lacking realm/map columns receive historical/Amber Crossing defaults. The migration is transactional and safe to reopen; it never reclassifies existing records using today's faction roster.

## Cosmetic catalog and ownership

`Assets/Game/Resources/Cosmetics/catalog.json` contains appearance metadata only. The service validates fields, supported targets and styles at startup; combat/movement/hitbox fields and unknown appearances reject the catalog. Cosmetic metadata is excluded from the competitive content hash and never sent into the authoritative World. A room freezes each participant's entitled equipment at join, filters it by realm (and a character skin by the faction played), and publishes it in `state.players[].cosmetics`; the client applies its own and opponent appearances separately.

A `character` item swaps one faction's unit model for a model of its own (appearance only, like every slot). Besides the common fields it carries `factionId`, which must be a playable faction of the item's realm, and `modelId`, the client's skin model; its `targetId` must be a soldier, rider or worker unit, and `styleId` only names the accent the store draws beside it. The other slots reject both extra fields. An equipped character skin is stored, and listed in `equipped`, under the target `<factionId>:<targetId>` (for example `drakeforged:reedguard`), so one account wears a skin for each faction and unit at once; equipping another skin for the same faction and unit replaces the first.

| Method and route | Contract |
| --- | --- |
| `GET /v1/cosmetics` | `{items,ownedIds,entitlements:[{itemId,source}],equipped:[{slot,targetId,itemId}],sandboxEnabled,purchasesAvailable}`. |
| `POST /v1/cosmetics/equip` | `{itemId}`. Requires this account's durable entitlement; price/ownership/stat claims from clients are ignored. |
| `POST /v1/cosmetics/sandbox-claim` | `{itemId,idempotencyKey}`. Disabled by default. Explicit sandbox grants have no real money or paid entitlement meaning. |
| `POST /v1/cosmetics/purchase` | `{itemId,idempotencyKey,receipt}`. Returns `payment_provider_unconfigured` until an operator supplies a trusted provider adapter. No payment is initiated by this alpha service. |

Receipt verification is an injected `verifyPurchase({accountId,item,receipt})` boundary. A production adapter must validate the receipt with the payment provider and return `{verified:true,accountId,itemId,provider,transactionId}` bound to the authenticated account and catalog product. No client-supplied `verified`, amount, transaction identifier or account identifier is trusted. Keys are idempotent per account; provider transaction IDs are unique across accounts, preventing replay into another account or another key. Real provider configuration, checkout UI, refund/revocation integration and platform store review are not activated by this repository.

Local wardrobe previews belong to the presentation layer. They never confer server ownership and never carry into PvP. Paid/free entitlements and equipped appearance persist in SQLite; skins do not alter damage, armor, speed, radius, target tags, resources or cooldowns.

The `frostguard_bronze` item follows Skeld/Frostguard into fantasy while retaining its existing item ID and ownership/equipment records. Catalog metadata is loaded once when the service starts; restart the service after a catalog correction so new rooms receive the updated eligibility. No account or entitlement migration is needed for this realm correction.

Supported map IDs are `amber_crossing` (forest), `sapphire_coast` (Caribbean), `sunscar_basin` (desert) and fantasy-exclusive `legend_lands` (highland, Tierras de Leyenda). Omitted realm defaults to historical; omitted map defaults to Amber Crossing for historical, Lands of Legend for fantasy, and Sapphire Coast for naval. Explicit unknown or incompatible values fail. Protocol 2 includes board-wall, leave-wall and gate commands, plus observable wall occupancy/elevation, boarding and defensive building state. Roster changes require a coordinated content pin and rebuilt client/authority even when the JSON field shape remains unchanged.

## Verification

```powershell
node Server/update-content-version.mjs
./tools/Build-Authority.ps1
node --experimental-sqlite --test Server/tests/*.test.mjs
```

`service.test.mjs` exercises actual HTTP, SQLite, authentication and lifecycle with an explicit fake worker so timers and failures are controlled. `authority.test.mjs` runs the compiled real C# simulation, two authenticated clients, private/ranked flows, fog/ownership/cost validation, reconnect, and authoritative results. Tests use temporary or in-memory databases, ephemeral ports, and synthetic test credentials; no stored player data is read or modified.

For a repeatable local concurrency run with a separate HTTP load-generator process and scratch database:

```powershell
node --experimental-sqlite tools/online-load.mjs --matches 32 --seconds 75
# Optional larger capacity, measured independently:
node --experimental-sqlite tools/online-load.mjs --matches 64 --seconds 75
```

The harness creates an isolated loopback service using the same service/pool implementation, provisions disposable accounts before timing, and exercises 5 Hz observations, accepted orders, queues, re-login, filtered final views and history. Accounts are provisioned before the timed run to preserve the real HTTP authentication throttle. Reports and SQLite data go under `D:/CodexTooling/online-validation` by default; credentials remain only in process memory. See [measured load and limits](../docs/technical/online-load-report.md). A configured limit is not a guarantee of Internet performance or large-army performance.

The packaged two-client check defaults to historical armies on Amber Crossing. To exercise elves (`verdant`) against orcs (`ashen`) on fantasy's default Lands of Legend, after building the Windows player:

```powershell
./tools/Smoke-Online.ps1 -Realm fantasy -Width 1440 -Height 1080 -TimeoutSeconds 240
# Explicitly selecting -MapId legend_lands runs the same battlefield.
```

Both players select the realm, map and faction through the native lobby controls. Each client gathers ordinary resources, pays for Kingdom research, lays two contiguous north-south wall stretches with `BuildRunCommand`, and places a separate rotated wall with `BuildCommand`. The checks require authoritative snapshots to show the completed 1-by-3 footprints, a six-cell run, and the exact stone cost. The runner kills and restarts the guest, then checks the same fortification IDs and orientations survive reconnection, verifies scoped history, and uses the native return-to-lobby actions. Each player verifies its own walls without bypassing enemy fog of war. Summary and player reports contain realm/map/biome, faction IDs and construction evidence, without account names, passwords or tokens; disposable sign-in configs are removed. This describes the verification procedure, not an already-passing result, and remains an automated local Windows check.
