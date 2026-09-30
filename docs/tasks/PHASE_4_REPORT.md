# Phase 4 — movement / pathfinding

Status: **complete against the measured normal-scenario acceptance gate**, 9 September 2026, Unity 6000.3.23f1. Human Checkpoint 2: group orders complete safely for 100–500 movers in the tested open-field, wide-corridor, crossing-group and dynamic-foundation scenarios. The one-cell choke still jams at 200+ movers; those failed cases remain visible in the evidence. Phase 5 has not started.

## Features and architecture

The authoritative simulation remains plain C# with `noEngineReferences`, integer millimetres, stable entity IDs and a central 20 Hz tick. Timing is optional instrumentation and never affects game decisions. `FormationPlanner` assigns destinations, `Navigation` supplies routes, and `MovementSystem` handles physical progress and local avoidance. Diagnostics live in a separate assembly and independently observe the simulation.

- **Loose / Line / Box:** the Formation control chooses the next terrain order. Loose preserves the cohort layout with sufficient aisle spacing; Line and Box prefer their named shapes, oriented to the dominant cardinal travel axis. Invalid slots near obstacles, idle bodies or map edges fall back to safe free positions. Shapes can adapt; they are not rigid formations during travel or combat. Repeated Loose commands do not keep expanding the group.
- **Physical clearance:** exact swept-circle checks test relative unit motion across a tick, with static rectangle and map clearance. A reusable spatial grid supplies nearby candidates. Tangency is legal; initial body penetration is rejected. Stops remain fixed obstacles. Movement uses an integer speed budget with fractional remainder; regression checks bound a step to `ceil(speed / 20) + 1 mm` for coordinate rounding.
- **Shared routes:** groups of at least four reuse reverse BFS fields, with at most four cached fields, personal destination tails and connectivity invalidation when footprints change. Cached straight segments avoid redoing clear-route visibility work on every tick; smoothing considers at most 12 candidates when a segment needs selection.
- **Congestion recovery:** deterministic side steering and persistent head-on yielding handle ordinary crowds. After eight blocked-forward attempts, recovery may search a half-cell lattice with 25×25 nodes, subject to a 20-tick per-unit cooldown and four recovery searches per tick. These are local limits, not a complete global command/path budget.
- **Economy/combat integration:** worker interactions use physically accessible adjacent positions and skip idle-blocked approach centers. Foundations update routes. Production checks spawn sweeps and resolves occupied rally destinations. A fresh move after autonomous pursuit returns to its destination instead of reusing stale completed group identity. Stop retains cargo and hold-fire behavior.
- **Stress tooling:** `RTSPerformanceStress` offers 50/100/200/300/500 movers, six scenarios, Run/Reset, frame/follow controls, arrival/immobility/overlap counters, frame/tick/navigation timing and an uncapped development driver. Normal economy/combat retain their ordinary driver.

No ECS, Jobs, Burst, physics navigation package or external service was introduced. Measured hot work justified shared routes, reusable neighbor lookup and visibility caching; the existing simulation boundary remains suitable for further targeted improvements. Detailed policies are in [pathfinding architecture](../technical/PATHFINDING_ARCHITECTURE.md) and [simulation architecture](../technical/SIMULATION_ARCHITECTURE.md).

## Measured baseline and optimization

The preserved [baseline table](../testing/evidence/phase4-baseline.md) and [JSON](../testing/evidence/phase4-baseline.json) measured the Phase 3 movement algorithm from commit `9d61486`, with the new observer/timing instrumentation before movement behavior changed. Eventual arrival alone concealed body penetration: 100-unit open and wide cases each reached 52 overlapping pairs, and the 100-unit crossing case held a pair overlapped for **531 ticks / 26.55 seconds**. The three normal 100-unit cases failed the persistent-overlap criterion.

Baseline OpenField command cost was **19.620 ms at 100** and **95.159 ms at 500**, including **19.140 / 85.228 ms** in path searches. The final corresponding command samples are **7.788 / 34.154 ms**, with zero observed overlaps. A final 500-unit open order builds one shared field and records 499 cache hits; personal tail work still scales with the number and spread of destinations.

A subsequent collision-safe [pre-visibility-cache checkpoint](../testing/evidence/phase4-before-visibility-cache.md) exposed expensive blocked-route visibility work: 500-unit NarrowChoke active-tick p95 was **20.286 ms**. The [final matrix](../testing/evidence/phase4-final.md) records **2.863 ms**, about 7.1 times lower. Other aisle/recovery changes occurred between those checkpoints, so this is a whole-checkpoint comparison, not an isolated attribution of every gain to caching. The final choke still fails completion; faster ticks do not resolve its gameplay failure.

These are single instrumented editor runs on one workstation, with variation and cold spikes retained. They are diagnostic measurements, not stable cross-machine guarantees. The Unity Mono allocation API returned zero for a retained 4096-byte allocation. Final allocation fields therefore report **-1 / unavailable**. The original baseline zero-allocation fields are invalid measurements, retained for provenance and explicitly excluded from allocation conclusions.

## CPU movement results

Fixture `movement-v1` uses a 128×96 m map, 1 m cells, unarmed mover definitions with 3200 mm/s speed and 300 mm radius, and 120 simulated seconds per case. The DynamicObstacle case adds one builder outside the mover count, then schedules a 3×9 m foundation at tick 100 and assigns its construction approach. The wide corridor permits routes around the wall ends; the narrow wall has one 1 m gap. CrossingGroups splits movers between opposing travel directions.

The observer checks every tick, outside the timed `World.Tick` window. Arrival requires Idle within 100 mm of the assigned destination. Immobility means less than 50 mm movement over 200 ticks; circling units can remain unarrived with zero immobility. More than 1 mm penetration counts as overlap, and a pair persisting 20 ticks fails the normal criterion. Analytic regression tests separately check continuous swept clearance between endpoints. Exact fixture geometry, counters and gates are documented in [movement stress methodology](../testing/MOVEMENT_STRESS.md).

**All 30 cases had zero observed overlaps and zero static/map violations.** The required gate enforces static/map validity for every sampled case and atomic rejection/stillness for Unreachable. It requires the full arrival and congestion criterion for the 100-unit OpenField/WideCorridor/CrossingGroups cases. The full run covers and passes those gates; it does not claim all 30 arrival criteria passed. Zero overlap across the other counts remains an observed result, independently reported from the required gate.

| 100-mover scenario | Command batch ms | Active tick p95 / max ms | Outcome / all-arrived seconds |
| --- | ---: | ---: | --- |
| OpenField | 7.788 | 0.059 / 0.145 | 100/100 in 27.45 s |
| WideCorridor | 1.753 | 0.080 / 1.374 | 100/100 in 28.10 s |
| CrossingGroups | 2.104 | 0.177 / 0.357 | 100/100 in 32.20 s |
| DynamicObstacle | 2.111 | 0.208 / 0.463 | 100/100 in 38.45 s |
| NarrowChoke | 2.754 | 0.428 / 0.649 | 100/100 in 66.80 s |
| Unreachable | 0.044 | No active movement | Rejected atomically; all remain still |

Completion across all configured sizes, in simulated seconds:

| Scenario | 50 | 100 | 200 | 300 | 500 |
| --- | ---: | ---: | ---: | ---: | ---: |
| OpenField | 27.25 | 27.45 | 27.90 | 28.40 | 28.85 |
| WideCorridor | 27.25 | 28.10 | 33.90 | 41.15 | 45.55 |
| CrossingGroups | 31.05 | 32.20 | 36.45 | 37.25 | 44.20 |
| DynamicObstacle | 32.60 | 38.45 | 40.55 | 43.90 | 44.90 |
| NarrowChoke | 44.80 | 66.80 | Failed: 31/200 | Failed: 52/300 | Failed: 37/500 |
| Unreachable | Rejected | Rejected | Rejected | Rejected | Rejected |

The failed narrow cases end at 120 s, with **0 / 25 / 98 immobile movers** respectively. Persistent circulation and blocked queues are unresolved congestion, not evidence that the crowd merely needs proportional extra travel time. At 500, active-tick p95 is **0.307 ms open**, **0.474 ms wide**, **1.468 ms crossing**, **0.662 ms dynamic**, and **2.863 ms narrow**. Large command costs remain a separate risk: the 300-unit open sample took **50.364 ms** and the 500-unit open sample **34.154 ms**.

The [full final JSON](../testing/evidence/phase4-final.json) includes all-window/active percentiles, route work, query maxima, shared-field/recovery counters, rejection results and source SHA-256 `76F7C85534B2A4BC1CAF500F878CADC4241058F15AF713067639E1D1E5ACF6CB`. That hash covers simulation/diagnostics paths and source text as measured, including line endings; a checkout's line-ending normalization can change it without a code change. The current working source matched it at final review. Observer/warmup overhead is excluded from tick timings and included in the 20.588 s total runner wall time.

## Rendered desktop measurements

Hardware: **Intel Core i5-10400F 2.9 GHz, 6 cores / 12 logical processors, 16,281 MB RAM, NVIDIA RTX 3060 Ti with 8,024 MB VRAM, Windows 11 10.0.26200**. Unity 6000.3.23f1 Windows development player, D3D11, `Mobile_RPAsset`, render scale **0.8**, MSAA **1**, vSync **0**, uncapped target. Output 1280×720 implies approximately 1024×576 3D rendering; 1440×1080 implies approximately 1152×864.

The hidden, non-batch player submits one explicit URP camera/HUD render per Unity loop to a reused output-sized texture. A synchronous one-pixel readback waits for preceding GPU work. Frame costs **include render submission, forced Canvas work and GPU synchronization overhead**: conservative serialized offscreen throughput, **not ordinary display FPS**, isolated GPU timing or mobile-device performance. Normal 20 Hz simulation continues; there is no smoke-style tick acceleration. Unity documents [render callbacks](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Rendering.RenderPipelineManager-endCameraRendering.html) and the synchronization behavior of [ReadPixels](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Texture2D.ReadPixels.html).

After render preparation, each probe excludes 3 s warmup from frame samples and records 20 s; tick/navigation samples include warmup. Each passed pixel validation, unique camera callbacks in the sampled window (one final lifecycle offset allowed), normal simulation/wall ratio and zero observed overlap/invalid positions. Observations run every two ticks. The final full PNG readback is outside timing. An earlier automatic-camera attempt produced no render callbacks and a black texture; its [rejected report](../testing/evidence/phase4-rejected-render-probe.json) is preserved and its loop timing is excluded.

| Movers / output | Frame p50 / p95 / p99 ms | Tick p95 ms | Initial order ms | Frame samples | Raw report |
| --- | ---: | ---: | ---: | ---: | --- |
| 50 / 1280×720 | 1.000 / 1.584 / 1.937 | 0.056 | 10.800 | 18,498 | [JSON](../testing/evidence/phase4-player-50-1280x720.json) |
| 100 / 1280×720 | 1.278 / 1.958 / 2.553 | 0.093 | 12.432 | 14,475 | [JSON](../testing/evidence/phase4-player-100-1280x720.json) |
| 200 / 1280×720 | 1.915 / 2.914 / 3.973 | 0.173 | 18.246 | 9,742 | [JSON](../testing/evidence/phase4-player-200-1280x720.json) |
| 300 / 1280×720 | 2.590 / 4.002 / 5.178 | 0.263 | 24.594 | 7,103 | [JSON](../testing/evidence/phase4-player-300-1280x720.json) |
| 500 / 1280×720 | 4.569 / 6.968 / 8.420 | 0.467 | 48.394 | 4,065 | [JSON](../testing/evidence/phase4-player-500-1280x720.json) |
| 100 / 1440×1080 | 1.349 / 2.148 / 2.687 | 0.097 | 12.255 | 13,527 | [JSON](../testing/evidence/phase4-player-100-1440x1080.json) |

The six actual sample windows lasted about 20.01–20.06 s, with simulation/wall ratios about 1.000–1.002. They deliberately sample moving armies before their 27–29 s arrivals; `Arrived: 0` at approximately 23 s is expected. The separate 120 s CPU matrix establishes completion. Initial order cost is recorded separately and excluded from the warmed frame distribution; the **48.394 ms** 500-unit order remains a potential input hitch.

Actual measured-texture captures were inspected at [100 phone](../testing/evidence/phase4-stress-100-1280x720.png), [500 phone](../testing/evidence/phase4-stress-500-1280x720.png) and [100 tablet](../testing/evidence/phase4-stress-100-1440x1080.png). All movers fit inside the usable view and the HUD is readable. The screenshot's instantaneous FPS label also describes this serialized offscreen loop, not normal displayed gameplay. These primitive unarmed workloads do not measure large armed battles, production art, thermals or touch usability.

## Tests and build status

- **160/160 EditMode**, **23/23 PlayMode**, zero failed or skipped: [EditMode XML](../testing/evidence/phase4-editmode.xml), [PlayMode XML](../testing/evidence/phase4-playmode.xml).
- **12/12 shipped balance counter probes** remain passing: [counter report](../testing/evidence/phase4-combat-balance.md).
- **30 CPU cases** ran with required coverage and acceptance passing; the three high-count narrow arrival failures remain explicit in the matrix.
- **Six rendered probes** passed the method and spatial gates above.
- **Final Windows development build:** succeeded, zero errors/warnings, **163,028,983 bytes**, **13.5675456 s**, build GUID `15c1bf3ca78343ffbcd39ba40ab38795`: [build summary](../testing/evidence/phase4-build.txt).

The six performance probes used the preceding build GUID `c025db1be65c48f1bbfcb08ef47692eb`, **163,028,968 bytes**, [profile-build summary](../testing/evidence/phase4-profile-build.txt). The final distribution change only fits the two-line Formation label in the non-stress HUD and corrects two diagnostic comments. Simulation, stress HUD, fixtures and rendered-measurement behavior are unchanged. Full rules/integration suites preceded the opt-in rendering-probe correction; actual six-player runs verified that correction. The final label adjustment was compiled, rebuilt and verified in fresh functional captures, without an unnecessary full suite or performance rerun.

The **19 added EditMode tests** cover formations and movement: continuous swept pair/static safety, bounded speed, deterministic selection/replay, atomic invalid orders, repeated group intents, hard Stop, obstacle changes, available worker approaches, produced units resolving rally slots, and returning after combat pursuit. **Four added PlayMode tests** cover stress composition, manually advanced tick/event and summary wiring, and the selected formation passing through terrain-tap orders. Actual cadence and camera framing are supported by the separate rendered probes and capture review. Two older fixtures were updated to test actual physical clearance and an unobstructed speed lane, preserving their behavioral assertions.

Final packaged functional checks, with runtime logs checked for errors:

| Smoke | Outcome | Evidence |
| --- | --- | --- |
| Economy / 1440×1080 | Select → move → gather → build → train → fight; tick 1312, 16 entities, population 5+0/6; trained soldier fought | [Report](../testing/evidence/phase4-economy-1440x1080.txt), [built/trained capture](../testing/evidence/phase4-economy-1440x1080.png), [fight finish](../testing/evidence/phase4-first-loop-finish.png) |
| Combat / 1280×720 | All three roles attacked; projectile damage, 3 deaths and view removal; tick 123, 25 entities | [Report](../testing/evidence/phase4-combat-1280x720.txt), [battle capture](../testing/evidence/phase4-combat-1280x720.png) |
| Combat / 1440×1080 | Same functional assertions passed; tick 123, 25 entities | [Report](../testing/evidence/phase4-combat-1440x1080.txt), [battle capture](../testing/evidence/phase4-combat-1440x1080.png) |

These smokes accelerate simulation and are functional checks, not performance or real-time balance claims. Their actual captures show battle framing and both Formation label lines. The user can launch `Builds/Windows/Emberfield.exe`, press **Stress**, choose a count/scenario and **Run orders**. [README](../../README.md) and the [stress guide](../testing/MOVEMENT_STRESS.md) contain exact repeatable commands.

## Current gameplay quality, limits and technical debt

The first-loop smoke still connects physical resource gathering to a completed production building, a trained soldier and a fight. Normal crowds preserve bodies, keep stopped units fixed, spread at their destinations and pass one another in the tested layouts. Formation selection creates a visible positioning choice. Camera framing exposes the whole measured army, and the current mode is readable at phone/tablet aspect ratios. These are meaningful greybox improvements; the automated fixtures do not establish comfortable real touch play or a fun complete match.

The remaining work is concrete:

1. **Large narrow chokes:** 200+ movers can circulate or jam indefinitely. Cooperative queueing/traffic policy needs separate design and regression coverage; local avoidance guarantees safety, not universal completion.
2. **Command spikes and allocation visibility:** group assignment and personal tail searches still run synchronously and allocate. Recovery is bounded locally, but command batches, full route replans and combat acquisition have no shared total work budget. Fix measured costs before claiming a frame budget, and replace the unsupported allocation counter with a verified profiler measurement.
3. **Target-device evidence:** Android and iOS editor support modules are absent; no APK or signed iOS build was produced. Windows cannot finish iOS signing. Real supported tablets/phones, sustained thermals, GPU timing, presentation latency and physical touch usability remain unmeasured. Desktop offscreen results do not prove the product's 60/30 FPS device targets.
4. **Presentation and scope:** 500 primitive units are visible but small. Selection rings/equipment can visually overlap independently of body collision radii. Formation cardinal orientation/fallback is basic. No final art/animation/audio, large mixed-army combat benchmark, terrain line-of-sight, fog, strategic AI, victory, Eras, technologies, factions or online services are implemented yet.

## Files created and modified

Paths below are relative to `Assets/Game/` unless stated otherwise; new Unity assets include `.meta` files.

| Area | Created | Modified |
| --- | --- | --- |
| Simulation | `FormationPlanner.cs`, `MovementGeometry.cs`, `NavigationMetrics.cs`, `UnitSpatialGrid.cs` | `MovementSystem.cs`, `Navigation.cs`, `Commands.cs`, `EntityStates.cs`, `World.cs`, `CombatSystem.cs`, `EconomySystem.cs`, `ConstructionSystem.cs`, `InteractionSearch.cs`, `ProductionSystem.cs` |
| Diagnostics | Assembly plus `MovementScenarioFactory.cs` (including scenario state), `MovementObserver.cs`, `MovementBenchmark.cs` | — |
| Presentation | `MovementStressSession.cs`, `MovementPlayerProbe.cs` | `MatchController.cs`, `MatchHud.cs`, `PlayerSmoke.cs`, presentation assembly references |
| Editor/scenes | `Editor/MovementVerification.cs`, `Scenes/RTSPerformanceStress.unity` | `Editor/ProjectTools.cs`, editor assembly references, `ProjectSettings/EditorBuildSettings.asset` |
| Tests | `Assets/Tests/EditMode/GroupMovementTests.cs`, `FormationTests.cs`; `Assets/Tests/PlayMode/MovementStressIntegrationTests.cs`, `FormationIntegrationTests.cs` | `MovementTests.cs`, `TestWorldFactory.cs`, PlayMode assembly references |
| Tools | `tools/Verify-Movement.ps1`, `tools/Profile-MovementPlayer.ps1` | `tools/Verify-Unity.ps1` |
| Documentation | This report, `docs/testing/MOVEMENT_STRESS.md`, tracked `phase4-*` evidence | Root/docs READMEs, current phase, technical design, simulation/pathfinding architecture, performance budget, test strategy |

The supplied reference PDF is unchanged: SHA-256 **778295A57495B1F67CC9E42A7F8F01932EF5841F6491FB72E84F55D77BCAEDF7**. No Unity reinstall, large optional application, paid service or remote push was performed. Build/cache/log directories remain ignored.

## Next recommended phase

**Phase 5 — Era / technology:** data-driven Settlement → Kingdom → Dominion → Empire, research prerequisites/costs/completion effects, unlocks and a small technology tree. Preserve movement stress and economy/combat regressions while adding atomic research validation and all-four-Eras acceptance. The known choke and command-budget debt remains visible for subsequent navigation/performance work.
