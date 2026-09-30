# Backend architecture

## Implemented alpha boundary

The current backend provides a Node HTTP/HTTPS service, a bounded pool of private C# authority workers running the existing World, and SQLite account/result persistence. Native Unity clients authenticate, join private 1v1 rooms or casual/ranked queues, send commands, and receive recipient-filtered observations. Offline practice and AI matches remain available without an account. The [2026-09-13 concurrency report](online-load-report.md) records 32- and 64-match loopback runs; it does not claim Internet capacity or large-army performance. Historical phase reports remain evidence of their original releases.

| Responsibility | Implemented owner |
| --- | --- |
| Registration, password verification, session ownership | Server/service.mjs, built-in asynchronous scrypt and opaque random tokens |
| Accounts, hashed sessions, match history, ratings | Server/database.mjs, SQLite prepared statements and transactions |
| Single database process ownership | Server/database-lock.mjs, exclusive process lock and dead-process recovery |
| Private lobbies, queue matching, reconnect/AFK timers | Server/service.mjs |
| Commands, simulation, fog, economy, final winner/statistics | Server/AuthorityWorker/Program.cs using the actual C# World |
| Private worker IPC | Server/authority-bridge.mjs, bounded JSON lines over child stdin/stdout |
| Match-to-process ownership, capacity and isolated worker failure | Server/authority-pool.mjs; four workers × eight slots by default, configurable within hard bounds |
| Versioned commands and filtered snapshot/replica | Assets/Game/Networking and simulation NetworkObservation/NetworkReplica |
| Local settings/tutorial/consented local diagnostics | Unity presentation alpha components; no remote upload |

## Trust and transport

The client never chooses its authoritative player slot or submits a result. The service resolves account membership to match and player before dispatching commands to the worker. The nested NetworkCommandEnvelope.Sequence must equal the wrapper sequence and follow the consumed cursor exactly. Rejected gameplay commands still consume their sequence, making retry/reconnect behavior unambiguous. Re-login and snapshots return this cursor under the API field lastAcceptedSequence. Each seat additionally retains its latest 2048 valid RequestIds across re-login. Reusing a retained ID with a new sequence consumes the sequence but rejects the action before worker execution. IDs require 1–64 ASCII letters/digits/underscore/hyphen; invalid IDs are never retained. IDs evicted from the bounded window are no longer compared.

The C# World advances at 20 Hz. Clients poll filtered observations at 5 Hz and render a read-only replica; they do not simulate an authoritative opponent or broadcast each render frame's transforms. A snapshot contains own private state, currently visible entities, exploration/vision and public objectives. The second server seat is normalized to presentation owner 1, while entity IDs remain stable. Enemy economy, hidden armies, hidden targets and private research are excluded. Acting-entity ownership and hidden targets are revalidated before a World command executes.

Public GET /health exposes worker availability and pinned protocol/content versions. Every /v1/ request requires matching X-Emberfield-Protocol and X-Emberfield-Content headers. The content suffix hashes normalized shipped rule and map files; startup verifies the hash. Wire/semantic changes require the coordinated alpha prefix/protocol version to change. The worker's System.Text.Json serializer uses IncludeFields=true because simulation and network DTOs use public fields.

Local HTTP binds 127.0.0.1:8787 by default. A non-loopback listening address requires configured PEM certificate/key and HTTPS; Unity does not disable certificate verification. The service spawns its worker without shell evaluation and consumes results only from this trusted child's output. There is no HTTP endpoint for result submission.

A dedicated Cloudflare tunnel origin can explicitly set `EMBERFIELD_TRUST_CLOUDFLARE_LOOPBACK=1`. The service then uses a valid `CF-Connecting-IP` only if the actual socket peer is loopback; absent/malformed values and non-loopback peers fall back to the actual peer. `X-Forwarded-For` is never used. This preserves per-visitor authentication quotas behind a local tunnel while keeping proxy trust off for ordinary deployments.

## Persistence and identity

SQLite tables cover accounts, hashed sessions, queue/mode ratings, matches and participants. Usernames allow 3–24 ASCII letters/digits/underscore and are unique case-insensitively. Passwords accept 10–128 UTF-8 bytes, are derived with scrypt (N=32768, r=8, p=1, random per-account salt), and are compared with the built-in constant-time comparison. Sessions are 256-bit opaque random tokens; only their SHA-256 hashes are stored. Tokens expire after twelve hours, support explicit revocation, and have a bounded concurrent-session count.

Database state uses prepared statements, foreign keys and WAL. A terminal result writes match state, both participants' statistics and rating changes in one transaction. A second delivery sees a completed record and does nothing. An exclusive process lock prevents another instance from running startup recovery against an active first instance. Normal stop releases the lock; restart recovers a stale lock whose owner process no longer exists. An unreadable lock or interrupted lock-recovery marker requires operator inspection rather than overriding a possibly live process.

Starting after a stopped service marks interrupted active records aborted without rating changes. Completed accounts, histories and ratings survive restart. An in-progress World is not persisted or replayed on server restart. Backups must use SQLite backup tooling or a cleanly stopped database; copying only the main file during WAL writes is insufficient.

## Matchmaking and progression

Private room creation selects content realm, battlefield, faction and Conquest/Dominion. A second distinct account joins using the room code; both accounts ready before creation of the authoritative World. Private results write history but never alter ratings.

Playable rosters are historical `aven/serevin/english`, fantasy `ashen/drakeforged/skeld/verdant`, and naval `pirates`. Naval admits only Sapphire Coast and currently uses the existing land battle with pirate workers, including mirror matches. English/Spanish navy and skeleton fleet entries are planned and cannot enter the API; `miraj/solar` are legacy definitions and records only. Node admission and the C# authority both reject incompatible rosters/maps. Content membership never changes ownership of old account records or reclassifies historical results.

SQLite migration 3 converts only recognized unscoped legacy rating keys. Historical, fantasy, naval and unknown future namespaces survive repeated restarts. An existing scoped rating is never overwritten by a colliding imported legacy row. Match records retain original faction/realm/map/content-version values; only old schemas missing realm/map columns receive historical/Amber Crossing defaults.

Casual and ranked queues partition by realm, battlefield and victory mode; ratings partition by realm, queue and mode. Each rating starts at 1000 with K=32. Matching starts with a 100-point tolerance, expands by 50 each ten seconds waited, and caps at 1200. The default pool allows 32 concurrent rooms/matches and 128 queued accounts. Eight workers with eight slots provide a configurable 64-match ceiling, exercised by the linked load report. Finished Worlds release their worker slots after producing two recipient-filtered terminal observations. These immutable views remain until dismissal, ten minutes, or bounded terminal-cache eviction; durable history survives eviction. Total retained rooms are capped at twice live capacity.

Visible ranked points are stored separately from hidden MMR: wins add 25, losses subtract 15 with a zero floor, and draws leave points unchanged. Rank thresholds in database.mjs are Bronze 0, Silver 100, Gold 250, Platinum 450, Diamond 700, Master 1000 and Legend 1400. Profile/history responses expose visible points/tier and outcomes, while hidden MMR remains server-only. These alpha constants need real population and balance testing. Cosmetic catalog/entitlement boundaries are documented in the operator guide and do not affect gameplay; placements, seasons, faction mastery and achievements are separate product work.

## Reconnect, inactivity and failure

Lobby/queue state requests and active snapshots renew the account heartbeat. A seat appears disconnected after 15 seconds without a heartbeat. Another 60 seconds of absence causes an authoritative forfeit; signing in again to the same account restores its existing seat and command cursor during that grace period. Explicit logout revokes the token but leaves the active match running.

Three minutes without an accepted command causes AFK forfeit; the API returns afkSecondsRemaining for a visible warning. If both players exceed their disconnect or AFK limits, the service aborts without awarding MMR or rank. Worker failure aborts only that process's matches, while other workers continue. Admission can replace a failed child, but never reconstructs its lost Worlds. Terminal persistence failure and service restart likewise abort affected matches without awards. Surrender sends a private authority request for the authenticated player; the worker supplies winner/statistics.

HTTP bodies are limited to 64 KiB; snapshots are bounded to 4 MiB; worker pending requests, connections, headers, authentication concurrency, per-IP requests and per-account commands are limited. Error responses and worker-stderr handling omit raw credentials and request payloads. Local diagnostics remain explicit-consent device files with bounded retention and manual export, separate from competitive truth.

Authenticated traffic allows 900 requests/account/minute and 20 commands/account/second, avoiding a shared gameplay quota for players behind one router. Authentication allows 12 requests/IP/minute, four active password derivations and eight bounded waiters with a two-second timeout. Invalid unauthenticated/version requests have a 120/IP/minute quota. Public requests allow 1,200/IP/minute. Connections and active HTTP requests are capped at `max(128, configured capacity × 8)`; each child bridge caps pending IPC at 256.

## Running, verification and remaining production gates

Exact commands, environment variables and API routes are in the [Server operator guide](../../Server/README.md). Build the authority with tools/Build-Authority.ps1, run node --experimental-sqlite Server/server.mjs, and verify with node --experimental-sqlite --test Server/tests/*.test.mjs. Tests cover worker process failure/isolation, concurrency quotas, proxy trust, paid-command deduplication, persistence, three realms and entitlements. The [concurrency report](online-load-report.md) retains measured runs with their original content hashes; those measurements are not silently reassigned to a changed roster. The [Phase 12 report](../tasks/PHASE_12_REPORT.md) preserves historical evidence.

Production hosting, durable public endpoint/TLS operations, operational backups, e-mail/password recovery, abuse moderation, WAN latency/loss testing, sustained and large-army load, actual two-human acceptance, physical mobile testing and competitive balance remain deployment/product gates. Local 64-match concurrency is measured; multi-machine failover, server-process match recovery and production capacity are not claimed. A temporary tunnel's connectivity check has separate evidence and does not establish those operating guarantees.

Runtime APIs were verified using official versioned Node 22.12 [SQLite](https://nodejs.org/download/release/v22.12.0/docs/api/sqlite.html) and [crypto](https://nodejs.org/download/release/v22.12.0/docs/api/crypto.html) documentation. The installed Node build requires --experimental-sqlite; no npm installation is needed.
