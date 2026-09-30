# Phase 11 — private authoritative multiplayer

**Status: implemented and verified with two Windows clients against the local authoritative service.** The final Phase 11–13 delivery passes 511 Unity tests and both packaged online scenarios. Android, WAN, public hosting and human playtests remain separate qualification gates.

## Delivered behavior

Two authenticated accounts create/join a private room, choose faction/victory mode, ready up and enter one authoritative Amber Crossing match. The .NET worker advances the existing C# World at 20 Hz. Unity submits the same sixteen gameplay commands used offline and polls filtered observations at approximately 5 Hz. Gathering, construction, production, technology, faction actions, combat, objectives and results come from server state. Online menus do not pause the match. Reconnection retrieves current visible state and the consumed command cursor.

The same authority supports Phase 12 profiles/queues/results and Phase 13 reconnect/AFK flows, documented separately. An Internet service has not been deployed by implementing this local server.

## Architecture and dependency decision

Node 22 and built-in HTTP/HTTPS/SQLite address a dedicated .NET 8 worker. No npm dependency, Unity networking package, external account, paid service or SDK was added. The worker compiles existing Simulation sources, not a second economy/combat ruleset. `IMatchTransport`, `IMatchClock`, `IMatchResultService` and `IPlayerCommandQueue` isolate exchange, time, results and bounded outgoing intent. [Architecture/protocol](../technical/MULTIPLAYER_ARCHITECTURE.md), [server operation](../../Server/README.md).

Photon Quantum was evaluated; SDK/AppID prerequisites and the simulation-port implications are documented in the architecture reference with official sources. The selected server worker preserves the existing rule implementation and exports only visible observations. Quantum integration and cloud licensing are not claimed.

## Authority and privacy

- Protocol/content headers and bounded envelope shape/range/enum/sequence/tick checks precede rules execution. Authenticated room membership sets the player and match.
- Each valid next command sequence is consumed once even if gameplay validation rejects it or a reply is lost. Reconnection reads that cursor before sending another order. The service also rejects duplicate request IDs using a per-seat set retaining the latest 2,048 IDs.
- Unknown/unowned actors share one preflight error; hidden/nonexistent attack/gather targets share one visibility error. Ordinary server rules then validate costs, ownership and prerequisites.
- Each client receives full own economy/technology/faction data, currently visible entities/projectiles, exact own fog and public objectives/results. Enemy private orders, queues, research and hidden entities are absent.
- Server seat 2 becomes client-local player 1 without changing entity IDs. Winner, objective ownership and hold clocks follow that mapping.
- Replicas never tick or execute rules. A transport sink queues intent; snapshot validation completes before replacing visible state.
- Visible carts retain their public type/progress without private packed-building state or previous hidden pack position. Cargo is preserved when a capacity bonus disappears.
- Results originate from the authoritative match/forfeit path and are recorded by the service. Client-authored winners cannot update rank.

## Local run

From the repository root:

```powershell
./tools/Build-Authority.ps1
node --experimental-sqlite Server/server.mjs
```

Open two Windows players, use **Online** with `http://127.0.0.1:8787`, register distinct accounts, create a room in one client, join its code in the other, and ready both. Enter credentials in the UI, not command-line arguments or evidence files. Keep the service running. External clients require an HTTPS endpoint with valid certificate configuration. [Complete service reference](../../Server/README.md).

## Verification ledger

| Check | Result/status |
| --- | --- |
| Pure Roslyn compile of Simulation/Networking | Passed before engine integration. |
| Observation/replica tests | **26/26 passed** in an independent Mono run; the Unity suite also includes them. |
| Command/wire/authorization suite | **50/50 Unity EditMode cases passed** within the 423-case engine run: sixteen command roundtrips, malformed bounds/options, ordering, nine privacy guards and four snapshot JSON cases. |
| Own economy/faction continuity | Inventory, queue/rally, effective stats, pending/completed research, packed outpost and retained cargo after capacity loss covered. |
| Fog/seat consistency | Every map cell compared for both recipients; winner/objective/hold ownership and reveal/loss covered. |
| Atomic malformed snapshot | Header/replay/mask/ID/definition/bounds/overlap/invalid timer cases retain previous objects and tick. |
| JSON compatibility | Unity roundtrip and handwritten server-style explicit-null/omitted packed fields, null research, PascalCase positions and 64-bit sequences. |
| Unity compile/EditMode | Compile passed; **423/423 EditMode cases passed**, including all 76 Phase 11 additions. [EditMode evidence](../testing/evidence/phase13-editmode.xml). |
| Final PlayMode integration | **88/88 passed**, giving **511/511 combined Unity tests**. [PlayMode evidence](../testing/evidence/phase13-playmode.xml). |
| Existing simulation regression gates | All **12 combat probes, 8 faction counters, 4 natural offline matches and 30 required movement-case gates** passed. Dense-choke arrival limitations remain documented rather than being represented as resolved. |
| Two rendered clients at 1280×720 | **Passed**, 30.04 seconds; guest observed tick 151 → 520, authoritative result at tick 534. [Online scenario evidence](../testing/evidence/phase13-online-1280.json). |
| Two rendered clients at 1440×1080 | **Passed**, 30.30 seconds; guest observed tick 150 → 518, authoritative result at tick 533. [Online scenario evidence](../testing/evidence/phase13-online-1440.json). |
| Final Windows build | GUID `cdf8183365404135975644fd3c935a58`, **164,137,326 bytes**, **14.086756 seconds**, **zero errors and warnings**. [Build summary](../testing/evidence/phase13-build.txt). |

Each packaged online scenario uses two real standalone Unity clients and the live loopback Node/.NET service, records **19 native PNG captures and 9 actual UI button clicks**, and verifies movement, training and resource delivery for both players. The guest process is stopped and the host continues for at least 20 authoritative ticks before the guest is restarted and resumes the same match. Surrender then produces the authoritative result and the expected history entry. These are automated functional scenarios, not human-played matches, natural full-length PvP balance tests or measured WAN/mobile sessions. The [packaged verification matrix](../testing/evidence/phase13-player-matrix.md) records the delivery checks separately from unit tests.

The 76 new observation/command cases are Phase 11's contribution within the 511-test combined Phase 11–13 suite. Service unit tests, real-worker HTTP integration and rendered standalone checks remain separately identified.

## Qualification still required

Local two-client checks do not establish handset frame rate, touch comfort, Wi-Fi/cellular latency, Internet/NAT reachability, public TLS deployment or prolonged host load. Full 5 Hz observations have no delta compression and require online army-count bandwidth/allocation measurements. One service/database/worker hosts the initial alpha; process restart aborts in-progress matches rather than restoring a save. Existing short Conquest pacing, Aven-skewed AI outcomes and dense-choke movement limits still require human testing and gameplay work. Authority prevents client-authored rules state; production anti-cheat and complete removal of terrain inference through normal pathfinding are not claimed.
