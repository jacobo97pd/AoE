# Multiplayer architecture

## Implemented authority boundary

Phase 11 implements private 1v1 using the existing C# simulation in a dedicated .NET 8 worker. The Node 22 HTTP service owns authenticated room membership and addresses that worker over private JSON lines. Unity sends intent and displays recipient-specific observations. It does not run a second authoritative simulation, grant resources or decide online results.

The worker advances `World.Tick` at 20 Hz. Unity requests observations approximately every 200 ms; the service may reuse a recipient's observation for 100 ms. Every observation has its simulation tick and increasing snapshot sequence. HTTP commands are sent serially; the authority validates identity, order, protocol, payload, ownership, visibility, prerequisites and costs. Menus do not pause an online match. [Service routes and operation](../../Server/README.md).

| Boundary | Implementation |
| --- | --- |
| `IMatchTransport` | Request/response exchange through the configured HTTP/HTTPS endpoint with ordinary certificate validation. |
| `IMatchClock` | Tick and tick rate, distinct from transport wall time. |
| `IPlayerCommandQueue` | FIFO with capacity 32; queued intent is not a successful server transaction. |
| `IMatchResultService` | Reads the server-issued result. Client claims never update ranking. |
| `NetworkCommandCodec` | Pure envelope encoding, bounds checks and observation authorization. |
| `NetworkObservation` | Exports one seat's own private state, visible entities and public objectives/results. |
| `World.CreateNetworkReplica` / `ApplyNetworkSnapshot` | Validates observations for the presentation API. Replica `Tick` does nothing; `Submit` only calls its transport sink. |

[MatchContracts.cs](../../Assets/Game/Networking/MatchContracts.cs) defines the interfaces. Simulation has no Unity or transport dependency; observation DTOs live there to avoid circular assembly references. Networking references Simulation; Presentation references both.

## Commands and ordering

Protocol-1 `NetworkCommandEnvelope` contains `Version`, `Sequence`, `IssuedTick`, `RequestId`, `Kind`, `UnitIds`, `EntityId`, `TargetId`, `DefinitionId`, `X`, `Z` and `Option`. It has **no player/match identity field**. The service obtains both from authenticated membership. Field names are case-sensitive, enum values are numeric, and positions use integer millimetres.

All sixteen existing commands are supported: move, stop, gather, return cargo, build, construct, train, rally, attack, research, charter, Threadkeeper deployment, outpost packing/deployment, reposition and surrender. Selections allow at most 1,024 positive unique IDs. Coordinates fit 0–1,000,000 mm. Definition/request IDs are ASCII tokens of at most 80/64 characters. Invalid enums and fields incompatible with a command kind are rejected. Request age allows 1,200 ticks behind and 40 ticks ahead of the server; this does not schedule execution at a client-selected tick.

The service consumes each valid next sequence once, including requests later rejected by gameplay rules. A lost reply cannot cause a replayed purchase. Reconnect reads `lastAcceptedSequence`; despite its name, it is the **last consumed sequence**, not the last successful order. The HTTP path also validates request-ID tokens and rejects duplicates through a per-seat insertion-ordered set retaining the latest **2,048 request IDs**. A duplicate request ID consumes its new valid sequence but never reaches the worker. Older IDs leave that bounded cache; monotonic sequence enforcement remains in effect. `NetworkCommandCursor` supplies strict ordering and similarly bounded request-ID deduplication for other adapters; HTTP uses its service-owned seat cursor and cache.

`TryAuthorizeObservation` runs before normal rules submission. Unknown and unowned acting IDs produce the same error, including secondary infrastructure for construction, return and relay. Hidden/nonexistent attack and gather targets receive the same visibility error. Thus rules messages cannot serve as an enemy-ID lookup. Move orders into unexplored terrain remain legal. Navigation success and observed movement can imply an obstruction; complete elimination of inference through ordinary orders is not claimed.

## Observation privacy and replica state

`NetworkSnapshot` carries public map dimensions/identity, recipient seat, sequence/tick, local player, opponent faction, visible entities/projectiles, public match state and own fog. Server seat 1 or 2 becomes presentation player **1**; its opponent becomes **2**. Entity IDs remain stable. Winner, capturer and hold clocks use that same ownership mapping.

Only the local player receives inventory, population, era and completed/pending technologies. Own units retain effective stats, cargo, orders, faction timers and packed outpost state. Own buildings retain queues, research, charter work, production rate and rally points. Retained cargo may legitimately exceed a reduced carrying capacity after losing a bonus.

Enemy entities and resources appear only while currently visible. Enemy destinations, targets, queues, research, economic totals, cooldowns and private packed-building contents are cleared or absent. Visible health, construction/relocation progress and active faction cues remain available. Enemy carts export their public definition and receive a visual shell at their current position, never their hidden previous pack location or rally state. Projectile references are cleared when the referenced entity is not visible. Global deaths are omitted because a hidden enemy death is not observable.

Each cell uses two bits: `0` unknown, `1` explored, `3` visible. Four cells fit in a byte, encoded as base64. The replica copies the recipient's exact mask and empties the opponent's mask; it does not recompute vision from a reduced entity set. Dominion objectives and their ownership/progress/hold state are public beyond sight.

The installed public map supplies terrain/objective definitions. Replica construction normalizes authored starts, immediately replaces their entities with the observation, and clears spawn arrays before returning. Hidden dynamic entities never enter public lists/indexes. Payloads are validated before replacing current state: stale sequence/tick, incompatible map/seat/version, malformed collections/masks, unknown definitions, bad health/timers, duplicate IDs, invalid positions and obstacle overlaps reject atomically. Unity may materialize a null inline nested object as an empty shell; an explicit packed definition identifies a cart, so an empty object never creates one.

## Disconnect and results

The service owns heartbeat, reconnect/AFK policy, authentication, queues and SQLite result transactions. Reconnection obtains a fresh filtered observation and command cursor, not a client savegame. The worker emits its finished `World.Match`, or the service requests an authoritative forfeit. Results are recorded once. Process failure aborts matches rather than inventing competitive wins. Exact timeouts and operation are maintained in the [service lifecycle policy](../../Server/README.md) and Phase 12/13 reports.

## Evaluated alternative: Photon Quantum

Quantum 3 was evaluated on 2026-09-10. Its SDK imports Photon/Quantum assemblies, user simulation/view directories and generated integration code. Online setup requires a Photon account, SDK import and Quantum AppID. No SDK or configured AppID is present here. [Quantum project structure](https://doc.photonengine.com/quantum/v3/manual/quantum-project), [official setup tutorial](https://doc.photonengine.com/quantum/v3/tutorials/asteroids/2-project-setup).

**Engineering assessment:** adopting Quantum would require adapting this managed object/list model to its simulation/frame/component workflow and revalidating rules and visibility. It is not a transport replacement for the current `World`. The selected Node/.NET worker preserves existing rules and allows filtered observations using installed tools. No SDK, cloud service, account or paid plan was activated. Vendor licensing and production hosting prices were not quoted or assumed.

## Validation and limits

Phase 11 adds 26 observation/replica cases and 50 command/wire/authorization cases, including four actual Unity JSON snapshot cases. Independent Mono execution passed all 26 observation cases before engine integration. The [Phase 11 report](../tasks/PHASE_11_REPORT.md) records consolidated engine/player evidence.

This alpha uses full observations at 5 Hz, with bandwidth/allocation costs requiring target-device measurement. It has no delta compression, combat prediction/rollback, server migration, persisted in-progress save, recovery after host restart or cross-device lockstep claim. Local impairment fixtures and two local players do not establish Internet/NAT, Android radio, mobile bandwidth/latency or production anti-abuse reliability. External listening requires HTTPS; public deployment and sustained multi-match load qualification remain separate gates. Existing navigation, pacing and faction balance debts are unchanged by transport.
