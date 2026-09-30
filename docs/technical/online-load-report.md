# Online concurrency validation — 2026-09-13

This work validates the actual Node HTTP service, SQLite persistence, matchmaking, and C# authoritative simulation on one Windows machine. All concurrency traffic is IPv4 loopback. No Internet throughput, mobile device, or two-human network session is claimed by these load results; a separate short HTTPS delivery reproduction is identified below.

## Changes

- `Server/authority-pool.mjs` assigns each match to one bounded C# process. The default is four processes with eight match slots each: **32 simultaneous 1v1 matches / 64 players**. Configure `EMBERFIELD_AUTHORITY_WORKERS=8` with eight slots for a 64-match ceiling. Configurable ceilings require measurement on the deployment hardware.
- A worker process failure aborts only that process's matches without ranked awards. Other workers continue; the next admission starts a replacement process. An uncertain create/close acknowledgement discards that shard so an untracked World cannot leak capacity.
- Authenticated gameplay uses independent account quotas, fixing the former 1,200-requests/minute shared-IP limit that would block many players behind one router. Authentication and invalid unauthenticated traffic keep IP quotas. Connections, concurrent requests, password work, queue entries, IPC messages and request/snapshot sizes remain bounded.
- A burst of eight simultaneous re-logins exposed the former immediate `authentication_busy` response above four password operations. The service now keeps four active derivations and permits eight requests to wait for at most two seconds; the 12-authentication-requests/minute/IP quota remains unchanged.
- A dedicated tunnel origin may explicitly enable `EMBERFIELD_TRUST_CLOUDFLARE_LOOPBACK=1`. Only a loopback socket peer with a single IP validated by `net.isIP` can supply `CF-Connecting-IP`; other cases use the peer address. `X-Forwarded-For` is ignored. Default service behavior does not trust proxy headers.
- A completed World produces two separately filtered final observations and releases its worker slot. Completed viewers can still read their final result; a new match can start without waiting for both previous players to dismiss results. Terminal observation retention is bounded by time and total room count; durable history remains in SQLite.
- Capacity changes preserved the existing wire protocol and gameplay rules. A subsequent real HTTPS smoke exposed the worker drop-off race described below; that simulation correction requires a new shared content pin and matching authority/client builds.

The former limits were 16 service rooms and 16 Worlds in a single worker. Increasing only one of those limits would not provide actual extra capacity. Retaining every finished World for ten minutes also prevented immediate reuse at capacity.

## Repeatable workload

```powershell
./tools/Build-Authority.ps1
node --experimental-sqlite --test Server/tests/*.test.mjs
./tools/Test-OnlineLoad.ps1 -Matches 32 -Seconds 75
./tools/Test-OnlineLoad.ps1 -Matches 64 -Seconds 75
```

`tools/online-load.mjs` starts the same service/pool implementation on an ephemeral loopback port with a fresh scratch SQLite file. Its HTTP generator runs in a separate Node process. The harness provisions isolated accounts through the real password/session implementation before timing; it deliberately preserves the production HTTP authentication throttle rather than claiming a simultaneous sign-up burst. Synthetic usernames, random passwords and bearer tokens are not written into reports. Reports and scratch databases stay under `D:/CodexTooling/online-validation` unless an explicit output directory is supplied.

The workload mixes private rooms, casual queues and ranked queues, historical/fantasy realms, all three maps and both victory modes. Each connected player requests observations at 5 Hz and issues movement, gathering or production orders at about 1 Hz. Every eighth match has a participant explicitly log out, stop heartbeat/snapshots for 20 seconds, and log in again. The opponent observes the disconnected flag, while re-login must recover the same match, player slot and sequence. The harness also checks forged actor ownership, duplicate paid request IDs, sequence replay, absence of a public result-write endpoint, final observations, each participant's history, ranked updates and immediate creation of a replacement match.

These are early-game Worlds with initial workers and worker production, not large late-game armies or a long-duration soak. Captured latency includes setup/result requests; the maximum may therefore include a burst of synchronous durable result writes. Tick throughput is derived from authoritative ticks and observation arrival times, not an invented server FPS.

## Final simulation build: 64 matches / 128 clients

After the worker delivery correction, the authority and clients were repinned to `frontiers-83c0586b99532761c29bc07c4cd12fe605b5078c4511fc3b279aeeb5615bac0d` (protocol 2). The tested authority DLL SHA-256 is `7d5decbdba8268d5c97f32268684060de2801b8786023d230db03f6af1bac508`. The hash was checked before and after execution and is recorded in the JSON report.

| Measured item | Final authority, 8 workers × 8 slots |
| --- | ---: |
| Concurrent matches / clients | 64 / 128 |
| Timed gameplay duration | 45.173 s |
| HTTP requests, including setup/results | 34,671 |
| Recipient/match snapshots validated | 28,256 |
| Accepted gameplay orders | 5,761 |
| Unexpected errors in the passing run | 0 |
| Intentional rejected gameplay attacks | 2 |
| Disconnect / re-login checks | 8 |
| Participant histories verified | 128 |
| Replacement match after completions | PASS |
| Mean / slowest authoritative progression | 19.995 / 19.972 ticks/s |
| Snapshot response p95 / p99 | 60.84 / 124.22 ms |
| Command response p95 / p99 | 77.53 / 107.63 ms |
| Maximum snapshot response | 1,594.94 ms |
| Service event-loop p95 / maximum | 42.86 / 1,561.33 ms |
| Node service peak RSS, excluding workers | 181.0 MiB |
| Sampled peak in-flight HTTP requests | 118 |

[Final passing load evidence](D:/CodexTooling/online-validation/load-64-final-dropoff-diagnostic-20260913/report.json) includes the content pin, binary hash, completed phases and transport diagnostics. The harness used a separate ephemeral listener and scratch database; it did not stop or modify the public alpha service.

The [first final-build attempt](D:/CodexTooling/online-validation/load-64-final-dropoff-20260913/report.json) **failed** during the simultaneous completion/history phase with `fetch failed` on a history request and a 7.038-second maximum service event-loop stall. Its timed gameplay and reconnect assertions had completed, but that failed run does not count as a passing end-to-end result. The diagnostic rerun above passed with the same service/binary and unchanged quotas/timeouts; only failure reporting, phase evidence and binary hashing were added to the harness. The first attempt did not preserve the underlying socket cause, so this remains an unresolved intermittent completion/transport tail. A passing repeat is not evidence that the underlying transient failure was repaired. Durable storage stalls and sustained completion bursts need further investigation before a production reliability claim.

An [isolated two-process HTTP reproduction](D:/CodexTooling/online-validation/dropoff-probe/keepalive-stall.json) used the installed Node 22.12.0, the same five-second keep-alive setting and an explicitly injected seven-second event-loop stall after a snapshot response. The next `fetch` GET for history failed with `ECONNRESET` (`read ECONNRESET`); the server recorded an idle-socket timeout before handling that GET. This reproduces a mechanism consistent with the observed completion failure, without proving that it was the original failure's exact cause. No production service was fault-injected, no timeout was increased, and no mutating request was replayed. The Unity client continues state/snapshot polling after a transport error and now retries profile/history GETs **once after 250 ms** when a transport failure occurs. The retry preserves the requested realm and requires the same signed-in session; cancellation, authentication/version errors and a second transport failure do not trigger further attempts. Authentication and gameplay POSTs are never repeated by this recovery. This client-side recovery does not establish that the service's intermittent completion stall has been resolved.

## Initial capacity measurements

| Measured item | 32 matches / 64 clients | 64 matches / 128 clients |
| --- | ---: | ---: |
| Worker processes × slots | 4 × 8 | 8 × 8 |
| Timed gameplay duration | 45.106 s | 75.091 s |
| HTTP requests, including setup/results | 17,343 | 57,708 |
| Snapshots validated against recipient/match | 14,128 | 47,453 |
| Accepted gameplay orders | 2,881 | 9,601 |
| Unexpected errors | 0 | 0 |
| Intentional rejected gameplay attacks | 2 | 2 |
| Logout / missing-heartbeat / re-login checks | 4 | 8 |
| Participant histories verified | 64 | 128 |
| Replacement match after all completions | PASS | PASS |
| Mean authoritative tick progression | 19.995 ticks/s | 20.000 ticks/s |
| Slowest observed match tick progression | 19.977 ticks/s | 19.986 ticks/s |
| Snapshot response p95 / p99 | 36.13 / 69.35 ms | 49.85 / 71.52 ms |
| Command response p95 / p99 | 32.63 / 44.66 ms | 51.35 / 72.96 ms |
| Maximum snapshot response | 733.78 ms | 1,595.73 ms |
| Total HTTP response bytes | 138,718,340 | 467,946,551 |
| Node service peak RSS, excluding workers | 205.5 MiB | 192.5 MiB |
| Sampled peak in-flight HTTP requests | 64 | 106 |

The maximum response spikes coincided with the deliberately simultaneous end-of-match/result phase. SQLite commits are synchronous and preserve durable results; the eight-worker configuration is not a claim that all response tails are below a frame. Peak RSS depends on garbage-collection timing and is not a linear per-match memory estimate. Tick rates come from active observations only; quantization and arrival timing cause small deviations around 20.

Raw evidence remains in the isolated tool directory:

- [32-match final report](D:/CodexTooling/online-validation/load-32-2026-09-13T16-16-45-785Z/report.json).
- [64-match final report](D:/CodexTooling/online-validation/load-64-2026-09-13T16-14-23-461Z/report.json).
- [Final backend test transcript](D:/CodexTooling/online-validation/backend-tests-final.txt).

An earlier 32-match run also passed 75 seconds. The initial harness drafts failed their own field assumptions (`IsWorker` versus observable worker capabilities, and `gamesPlayed` versus win/loss/draw totals); those runs are not presented as accepted evidence. The first 64-match run correctly failed on four rejected simultaneous logins; the bounded authentication queue fixed that case before the successful rerun. No load failure was hidden by increasing authentication quotas or suppressing unexpected errors.

No external endpoint was exercised by this harness. Any public HTTPS smoke managed separately has its own evidence and does not turn this loopback throughput result into a WAN benchmark.

## Worker delivery regression found by the HTTPS smoke

The native online smoke issued Move, Gather and Train for each seat on Amber Crossing. All six orders were accepted, but both gatherers kept 11 wood in cargo indefinitely. Production completed after six seconds and placed an idle recruit on the Hearth interaction cell that its gatherer had selected earlier. Movement recovery continued trying to reach the same occupied destination. At 600 simulated ticks, both stocks were still 80 wood and the gatherers had accumulated 477 blocked ticks.

`EconomySystem.Tick` now reselects the resource or drop-off approach after 20 blocked movement ticks. It preserves the worker's cargo and assignment. If every access cell is occupied, it waits using the existing retry delay; only physical arrival credits inventory. The idle blocker is neither moved nor removed.

Five deterministic regression cases cover both mirrored seats, commands submitted at tick 0 or 8, automatic versus explicit cargo return, and a fully occupied drop-off subsequently freed by a move order. They assert conservation, physical arrival, one production charge and the recruit's unchanged position. A separate Roslyn/.NET 8 harness ran these five cases plus the existing 53 economy tests: **58/58 passed**. Compiling the previous guard into a scratch copy produced exactly five regression failures while the existing 53 tests still passed. These are standalone simulation test executions, not an additional Unity Test Runner result.

The final [Unity EditMode economy run](D:/CodexTooling/online-validation/unity-economy-final.xml) independently passed the same **58/58 cases in 0.3435 seconds**: 53 existing economy tests and five worker-interaction regressions, with zero failures, skips or inconclusive cases.

Using the shipped rules and map in the same scratch harness, both seats first delivered at tick 146, or tick 154 when commands were delayed by eight ticks; their maximum observed blocked counter was 19. The separately managed HTTPS reproduction then confirmed six accepted orders and both seats moving, training and delivering at its eight-second sample (tick 165, 91 wood, no cargo, no blocked ticks). That short two-account HTTPS check proves the reported delivery failure was repaired; it does not measure Internet concurrency.

Evidence:

- [Simulation before correction](D:/CodexTooling/online-validation/dropoff-probe/before.jsonl) and [after correction](D:/CodexTooling/online-validation/dropoff-probe/after.jsonl).
- [58 passing simulation cases](D:/CodexTooling/online-validation/dropoff-probe/tests-after.txt) and [five failing regressions against the previous guard](D:/CodexTooling/online-validation/dropoff-probe/tests-before.txt).
- [HTTPS delivery reproduction after correction](../../TestResults/OnlineClient-20260913/https-economy-after-fix.json).
- [Native Windows clients over public HTTPS](../../TestResults/Online-HTTPS-Historical-Fixed-20260913/summary.json): both standalone clients passed the full historical match flow, including actual guest-process termination, re-login and history, in 38.82 seconds. Both clients ran on this computer; independent physical networks were not tested.

## Regression evidence and limits

The final [Unity client networking run](D:/CodexTooling/online-validation/unity-network-final.xml) passed **41/41 PlayMode cases in 5.3592 seconds**, with zero failures, skips or inconclusive cases: eight frontier security, eight read-retry, seven session-recovery and 18 online-UI tests. Read-retry coverage includes a reset while streaming the history body, preservation of the history realm, one retry for the same account, cancellation/account-switch guards, failure after two resets, and no retry for authentication or command POSTs. These client tests complement the load and HTTPS evidence; they are not additional Internet-concurrency measurements.

The expanded backend suite passes **33/33 tests in 7.9156 seconds**, including an actual C# child process kill: only its match aborts, another shard's ticks continue, and a new child admits a replacement match. The suite also covers account/IP quota isolation, a real HTTP authentication burst, explicit proxy trust/distrust, and final snapshots after authority capacity is released. Existing tests retain authentication, fog, command ownership, request/sequence replay, realm/map queue partitions, AFK/disconnect timers, durable ratings/history, database owner locking and entitlement isolation. Runtime: Node 22.12.0 with built-in experimental SQLite and the compiled .NET 8 authority.

On this machine the OS reports 12 logical processors and 17,072,500,736 bytes physical memory. The harness reports Node service RSS only; it does not mislabel that number as the combined C# worker memory. Durable public hosting, independent-device/NAT reachability, packet loss/latency, real mobile lifecycle, competitive human testing, large armies and sustained production operations remain separate validation gates. A stopped service aborts in-progress matches; worker process replacement is not restoration of the lost World.
