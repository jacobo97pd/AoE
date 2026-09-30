# Movement stress measurements

## Phase 6 verification

Phase 6 passed [290 EditMode tests](evidence/phase6-editmode.xml), [32 PlayMode tests](evidence/phase6-playmode.xml), [12 common authored-JSON counter checks](evidence/phase6-combat-balance.md), [eight faction counter checks](evidence/phase6-faction-counters.md) and the required gates in the [30-case neutral movement matrix](evidence/phase6-movement.md). The additions are 73 pure faction cases and six integration tests. Faction timers now run after combat and before production; research remains after production. These fixtures contain no assigned factions, active influence or research workload. The [Phase 6 report](../tasks/PHASE_6_REPORT.md) and [phase ledger](../tasks/CURRENT_PHASE.md) record the completed build and player gates.

Every neutral arrival outcome matches Phase 4/5: OpenField, WideCorridor, CrossingGroups and DynamicObstacle finish at 50/100/200/300/500 movers, and all 30 cases report zero observed overlaps/static/map violations. NarrowChoke 100 finishes at 66.8 simulated seconds; 200/300/500 reach only 31/52/37 by 120 seconds. These unresolved persistent-congestion failures remain separate from the passing required normal-case gates. The [Phase 6 JSON](evidence/phase6-movement.json) records source SHA256 `CEB28A978BF89C869B884BF60C3B2427CDFF91FEA78D752A993166965B66460A` and the measured neutral CPU distributions. Allocation measurements remain unavailable. No active faction influence, renderer, research-completion or mobile performance has been profiled.

The separate faction counter probe uses unmodified shipped data and explicit owner assignments. Reedguards beat Ashrunners in 5.40 s with 64 health; Ashrunners beat Stringwardens in 6.95 s with 25 health, mirrored on both sides. Two disadvantaged units reverse both matchups. Its eight cases flush projectiles and check both orders, but use accelerated ticks and unequal nominal unit costs (100 versus 80); they are functional regressions, not competitive balance or performance measurements.

The final [Phase 6 build](evidence/phase6-build.txt), GUID `3a4d191876ae44fba8e0e681bfbdeaa0`, succeeded with zero errors/warnings, 163,206,372 bytes and a 13.7569182-second build duration. The 32 PlayMode tests passed again after final description-layout and opt-in smoke movement corrections; no core rules changed after the full gate. All nine packaged functional checks passed. Aven at [1280×720](evidence/phase6-aven-1280x720.txt)/[1440×1080](evidence/phase6-aven-1440x1080.txt) finished at tick 4,035 with 44 yielded frames and nine visible synthetic dispatches. Serevin at [1280×720](evidence/phase6-serevin-1280x720.txt)/[1440×1080](evidence/phase6-serevin-1440x1080.txt) finished at tick 3,844 with 43 frames and twelve dispatches, preserving 439/450 outpost health through two relocations. Each exercised two chooser selections and CLI override, equal stock, physical gathering, faction gates, mechanics, actions and unique research. The [test strategy](TEST_STRATEGY.md) links the two technology, two combat and one economy reports. These accelerated checks measure no active-faction or rendering performance; the Phase 4 serialized rendering results below remain historical.

## Historical Phase 5 checkpoint

The verified Phase 5 checkpoint passed [217 EditMode tests](evidence/phase5-editmode.xml), [26 PlayMode tests](evidence/phase5-playmode.xml), [12 authored-JSON counter checks](evidence/phase5-combat-balance.md) and the required gates in the [30-case movement rerun](evidence/phase5-movement.md). This adds 57 pure technology cases and three integration tests. Research now ticks after production; these navigation fixtures contain no research workload. All Phase 4 render/performance results below remain historical, and no Phase 5 rendered FPS, allocation or research-completion profiling claim is implied. The [Phase 5 report](../tasks/PHASE_5_REPORT.md) and [phase ledger](../tasks/CURRENT_PHASE.md) own delivery status.

Phase 5 reproduced all Phase 4 arrival outcomes: OpenField, WideCorridor, CrossingGroups and DynamicObstacle completed at all five counts, and all 30 cases had zero observed overlap/static/map violations. NarrowChoke 100 finished in 66.8 simulated seconds; the 200/300/500 cases still reached only 31/52/37 movers by 120 seconds. That unresolved persistent congestion remains a limitation. See the [Phase 5 JSON](evidence/phase5-movement.json) for the new source hash and measured CPU distributions; its known-allocation probe remains unavailable.

The final [Phase 5 Windows build](evidence/phase5-build.txt), GUID `48c0b4723fb54362a3697c9027469896`, succeeded with zero errors/warnings. The 26 PlayMode tests were rerun after research viewport-width and opaque-panel fixes; the later opt-in smoke-camera focus adjustment was covered by the final build and players. Technology smokes at [1280 by 720](evidence/phase5-technology-1280x720.txt) and [1440 by 1080](evidence/phase5-technology-1440x1080.txt) reached Empire with all nine technologies at tick 16,338. Each records nine visible, raycast-checked synthetic button dispatches with programmatic scrolling, unmodified starting stock and physical resource gathering. Accelerated 100-tick wait-loop batches establish functional integration, not wall-clock playtime or rendered performance.

The [baseline JSON](evidence/phase4-baseline.json) and [baseline table](evidence/phase4-baseline.md) were captured on 9 September 2026 at 18:56:48 UTC, using Unity 6000.3.23f1 on Windows 11, an Intel i5-10400F and 12 logical processors. The movement algorithm was unchanged from commit `9d61486`; opt-in instrumentation and diagnostics were added before capture. The report records source SHA256 `705203E3F808F0F4191E5DEF18EF7EAC1AC237B7B19A18853543ADAE097D2632`. All 30 cases completed in 7.964 seconds of runner wall time, including warm-up and observation. Each independently advanced 120 simulated seconds, so that wall time is not a frame-rate measurement.

## Historical Phase 4 baseline

All 25 reachable cases accepted their orders and reached their destinations by the deadline. All five unreachable cases rejected without movement. No static-obstacle or map-boundary violations were observed. However, all three required normal 100-unit scenarios failed the persistent-overlap criterion:

| 100-unit scenario | Order batch | Path queries / total time | Active tick p95 | Peak overlapping pairs | Longest pair overlap | All arrived |
| --- | --- | --- | --- | --- | --- | --- |
| OpenField | 19.620 ms | 100 / 19.140 ms | 0.023 ms | 52 | 32 ticks / 1.60 s | 30.35 s |
| WideCorridor | 16.079 ms | 100 / 15.591 ms | 0.021 ms | 52 | 32 ticks / 1.60 s | 30.35 s |
| CrossingGroups | 17.435 ms | 100 / 17.135 ms | 0.019 ms | 82 | 531 ticks / 26.55 s | 29.70 s |

At 500 units, OpenField order submission took 95.159 ms, including 85.228 ms in 500 path queries. NarrowChoke reached 4,319 overlapping pairs. DynamicObstacle's 500-unit scheduled construction command took 80.076 ms and triggered another set of routes; its separate event cost is absent from the tick quantiles but present in the JSON and navigation totals. These results direct attention to repeated route searches and physical congestion. Fast movement ticks alone would conceal both problems. They do not establish a rendered frame budget or a universal worst case.

**Allocation correction:** the original baseline reports zero allocated bytes and `AllocationMeasurementAvailable: true`, including command paths which visibly allocate arrays and lists. Treat those allocation values as unavailable, not as evidence of zero allocation. The original artifacts are preserved. The runner now verifies the API using a retained 4,096-byte allocation before measuring anything; a working counter must increase by at least 4,096 bytes. Reports include `AllocationMeasurementMethod` and `AllocationProbeObservedBytes`; an unsupported, nonworking or throwing counter produces `-1` allocation values. This correction does not alter the recorded baseline timing, geometry or arrival observations. The final Unity run observed zero for the known allocation, so its allocation measurements are unavailable and reported as `-1`.

## Reproducible fixtures

[MovementScenarioFactory](../../Assets/Game/Diagnostics/MovementScenarioFactory.cs) defines `movement-v1`. Every map has 128 by 96 one-metre cells. Counts are 50, 100, 200, 300 and 500 movers; IDs and spawn order are stable. Isolated diagnostic Tenders are unarmed workers with 300 mm radius, 3,200 mm/s speed and one population. The authored combat JSON is unchanged. A single group starts near cell (20,48), spaced one metre apart, and receives a goal at (104.5,48.5) m. Each mover's accepted assigned destination is captured for later arrival checks.

| Scenario | Geometry and order |
| --- | --- |
| OpenField | Empty map; one group travels east. |
| WideCorridor | Wall cells at z=41 and z=55, x=36 through 92 inclusive; 13 m interior. Walls have open ends, so this tests routing into and around a corridor, not a sealed lane. |
| NarrowChoke | Wall at x=64 across the entire map except cell (64,48); one 1 m gap, sufficient for one 600 mm diameter unit at a time. |
| CrossingGroups | Split total headcount between owners 1 and 2 near (20,48) and (104,48); both groups receive opposing goals before the first tick. Odd counts place the extra mover in group 2. |
| DynamicObstacle | N movers plus one explicitly reported builder at (64.5,60.5) m. At elapsed tick 100, submit a free 3 by 9 m foundation centered at (64.5,48.5) m. Record build acceptance and replan cost. |
| Unreachable | Complete wall at x=64; the group order must reject atomically, leaving every mover still. |

`IssueOrders()` sets `StartTick` once. Scheduled events use elapsed ticks since that call, allowing the interactive scene to idle before Run. Repeating `IssueOrders()` does not restart schedules or submit duplicate commands. `UnitIds` and arrival denominators include movers only; overlap/static checks include the dynamic builder too. Keep counts, fixture version, duration, source hash and measurement mode attached to every comparison.

## Observation and gates

[MovementObserver](../../Assets/Game/Diagnostics/MovementObserver.cs) reuses its sample object. It samples every tick in the CPU matrix and every two ticks (10 Hz) in the interactive/player scene.

- Arrived: Idle and within 100 mm of the captured assigned destination. The report records first all-arrived tick and final arrival fraction.
- Stalled: a nonarrived mover has displaced less than 50 mm from its last movement anchor for 200 ticks (10 seconds). This measures immobility, not progress toward the destination: a unit circling its goal can report zero stalls while never arriving. Always pair stall counts with final arrival fraction and the deadline gate. Unreachable-case stalls are expected and are not a failed rejection test.
- Overlap: center separation penetrates the combined radii by more than 1 mm. Record current/maximum pair counts, maximum penetration and longest continuous overlap of a particular pair. A pair persisting 20 ticks (one second) fails the normal criterion.
- Invalid position: a unit circle extends outside map bounds or intersects a blocked terrain/building/resource cell. Record separate outside/static peaks and their union.

The observer checks positions at sample times. It cannot alone prove the absence of a collision between samples, exact swept-circle safety, or absence of an overlap during the skipped player tick. Small rule tests cover those movement invariants separately. Its spatial buckets assume the diagnostic radius is at most half a cell; changing fixtures requires reviewing that assumption.

Required final coverage includes 100-unit OpenField, WideCorridor, CrossingGroups and Unreachable. The first three must accept all orders, finish within the declared duration, have no final stalls/overlaps, and have no pair persistently overlapping. All sampled cases must avoid static/map violations; Unreachable must reject and stay still. `RequiredGatesCovered` is false when a filtered matrix omits required cases, and Final mode fails incomplete coverage. The default final comparison uses the full 30-case, 120-second matrix.

Every other count and constrained scenario still reports `AcceptancePassed` using the same complete-arrival/congestion criteria. `RequiredCasePassed: true` on a nonrequired-size case is not a claim that it solved congestion: inspect `AcceptancePassed`, final stalls, arrival fraction and overlap history. The larger final NarrowChoke cases show unresolved persistent congestion; safe geometry and a finite-width passage do not establish that remaining units would eventually pass. Persistent jams in ordinary open/wide/crossing movement fail their required completion criteria.

## CPU matrix and reruns

Run these commands in PowerShell from the repository root, with no Unity Editor already using this project:

```powershell
# Candidate capture: preserves the tracked pre-change baseline and records failed criteria.
powershell -NoProfile -File .\tools\Verify-Movement.ps1 -Mode Baseline -Label candidate

# Complete final acceptance matrix. Nonzero exit means a failure or incomplete required coverage.
powershell -NoProfile -File .\tools\Verify-Movement.ps1 -Mode Final

# Repository gate: compile, EditMode, PlayMode, common/faction counter probes and the complete movement matrix.
powershell -NoProfile -File .\tools\Verify-Unity.ps1

# Run only the complete final movement matrix through the repository wrapper.
powershell -NoProfile -File .\tools\Verify-Unity.ps1 -Stage Movement

# Separate shipped-JSON faction counter regression; no movement performance measurement.
powershell -NoProfile -File .\tools\Verify-Unity.ps1 -Stage Factions

# Optional repeated focused diagnosis; not a complete final gate.
powershell -NoProfile -File .\tools\Verify-Movement.ps1 -Mode Baseline -Label open-repeat -Counts '100,200' -Scenarios OpenField -Repetitions 3
```

The wrapper defaults to `D:\Unity\Editors\6000.3.23f1\Editor\Unity.exe`, accepts `-UnityPath`/`-ProjectPath`, and calls `Emberfield.Editor.MovementVerification.Run` in batch mode without graphics. JSON, Markdown and logs are written to `TestResults/movement-<label>.*`. `-Seconds` defaults to 120; `-Repetitions` defaults to 1. Preserve the full run metadata when changing either. A single repetition yields tick-distribution samples, not independent run-to-run confidence intervals. `Verify-Unity.ps1` with All or Movement additionally requires a complete 30-case report, `RunComplete` and `RequiredGatesPassed`.

Separate small worlds warm the JIT and diagnostic code. Measured worlds are freshly constructed, so their navigation state is cold; world creation/connectivity initialization is outside the command/tick window. Order-batch time includes submission and capture of assigned goals. The scheduled build has its own event timing. Enabled navigation counters include path, attack-route and connectivity work after reset, including commands and construction previews. They count dequeued search cells; an early rejection or direct path may visit zero cells.

The final runner additionally exports `SharedFieldBuildCount`, `SharedFieldCacheHitCount` and `RecoveryQueryCount` to JSON and a separate Markdown table. A per-unit group-route request still increments the path count when its shared field is reused. Shared-field builds/hits describe those field lookups, while recovery is a subset of path requests; do not sum them as independent searches. Their times and visited cells stay in the path bucket. Compare path time and visited cells alongside builds/hits, rather than expecting the path-request count alone to fall. The preserved baseline predates these supplemental counters.

Tick p50/p95/p99/max bracket only `World.Tick()`. Observer work and its allocations are outside those measured windows, although their memory/GC pressure can still affect the process. `ActiveTicks` begin with at least one unit marked Moving; `AllTicks` include settled idle time through the full duration. Compare active sample counts as well as quantiles. Stopwatch instrumentation itself has overhead. Managed allocation bytes, when available, cover only the current thread and cannot replace native/GPU/heap measurements.

## Packaged rendering method and historical Phase 4 results

Build and run the separate development-player probe:

```powershell
powershell -NoProfile -File .\tools\Build-Unity.ps1 -Target Windows
powershell -NoProfile -File .\tools\Profile-MovementPlayer.ps1

# In an existing PowerShell session, a smaller explicit count set:
.\tools\Profile-MovementPlayer.ps1 -Counts @(100,200) -Scenario CrossingGroups -Width 1440 -Height 1080 -Seconds 20
```

The default player matrix uses OpenField, counts 50/100/200/300/500 and 1280 by 720 output pixels, requesting 20 seconds of active frame samples after a three-second warm-up. It starts a hidden **non-batch** D3D11 development player with ordinary 20 Hz simulation. The initial automatic-camera probe produced zero camera callbacks and a black render target despite a fast player loop. Its preserved [rejected report](evidence/phase4-rejected-render-probe.json) has `Passed: false`; exclude its frame deltas from rendering-performance comparisons or FPS claims. Omitting batch mode alone did not guarantee rendering in this environment.

The verified replacement method disables automatic camera rendering and submits one explicit URP `StandardRequest` per Unity loop to a persistent screen-sized texture, after `Canvas.ForceUpdateCanvases()`. It then reads one pixel into a reused 1 by 1 CPU texture to wait for GPU completion. Unique `endCameraRendering` callbacks are counted in the same accepted frame window; the gate allows one final Update-before-render lifecycle offset. Unity documents [player batch mode](https://docs.unity3d.com/6000.3/Documentation/Manual/PlayerCommandLineArguments.html) as headless, the [camera callback](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Rendering.RenderPipelineManager-endCameraRendering.html) as occurring after a camera renders, and [`ReadPixels`](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Texture2D.ReadPixels.html) as waiting for preceding GPU work.

Frame deltas include simulation, view updates, camera framing, HUD, the 10 Hz observer, explicit render submission, forced canvas updates and the synchronous one-pixel readback/GPU wait. This deliberately serializes the work and differs from the CPU tick timings, which exclude observer work. Tick/navigation samples include the warm-up; frame quantiles exclude its first three seconds. The camera follows the full mover bounds. Full screenshot readback/PNG encoding occurs after timing and reads the existing rendered target; that final capture does not submit a fresh camera render. Only this full capture is excluded: per-frame one-pixel readback remains in every measured frame. Reports record requested/actual duration, ticks, sample counts, rendered camera frames, quality, resolution, v-sync/target-frame-rate settings, hardware and method. `Sample.SampledFrameSeconds` records accepted frame duration and `CompletedBeforeRequestedWindow` discloses early completion. Inspect those values rather than assuming every request produced a full 20-second sample.

`SimulationSeconds` and `SimulationToWallRatio` expose dropped simulation time: the player gate requires simulated time to remain within 5% of elapsed wall time. This matters because the ordinary frame loop caps catch-up after long frames. Reports also record the active URP asset, `RenderScale` and `MsaaSamples`. The existing Mobile URP asset uses a 0.8 render scale: a 1280 by 720 output corresponds to approximately 1024 by 576 3D rendering before upscale, so output resolution is not a claim of native-resolution 3D work. Use the actual recorded settings in any comparison.

Outputs are `TestResults/MovementPlayer-<scenario>-<count>-<width>x<height>/movement-player.json`, `stress.png` and `player.log`. The script checks process exit, report success, requested count/scenario/resolution, uncapped non-batch settings and runtime errors. The probe requires enough frame/tick samples, verified camera rendering, an existing render target with captured pixels and no observed overlap/static violations. It is an active rendering sample, not the full 120-second arrival gate. Unreachable has no accepted active run and is verified by the CPU matrix instead.

The historical Phase 4 1280 by 720 runs used the Intel i5-10400F, RTX 3060 Ti and D3D11 development build `c025db1be65c48f1bbfcb08ef47692eb` (163,028,968 bytes, zero build errors/warnings; [measurement-build summary](evidence/phase4-profile-build.txt)). The Phase 4 distribution build was `15c1bf3ca78343ffbcd39ba40ab38795`, 163,028,983 bytes, completed in 13.5675456 seconds with zero errors/warnings ([distribution-build summary](evidence/phase4-build.txt)). Its only differences from the measured Phase 4 build were non-stress Formation-label fitting and comments. Phase 4 economy/combat functional smokes passed on that distribution build; these profile timings remain attached to the earlier measurement build and do not characterize Phase 5. The [Phase 4 report](../tasks/PHASE_4_REPORT.md) records that historical evidence. All five measured counts captured pixels, recorded zero observed overlap/invalid positions, accepted about 20.01–20.06 seconds of frame samples and advanced simulation within 0.2% of wall time. Sampled camera callbacks equaled frame samples minus one in every report, matching the declared final lifecycle offset. The [100-unit](evidence/phase4-stress-100-1280x720.png) and [500-unit](evidence/phase4-stress-500-1280x720.png) captures show the full army and readable HUD. The HUD's instantaneous FPS label belongs to this serialized probe and must not be presented as ordinary gameplay FPS.

| Movers / report | Frame samples | Sampled camera renders | Frame p50 | Frame p95 | Frame p99 |
| --- | --- | --- | --- | --- | --- |
| [50](evidence/phase4-player-50-1280x720.json) | 18,498 | 18,497 | 1.000 ms | 1.584 ms | 1.937 ms |
| [100](evidence/phase4-player-100-1280x720.json) | 14,475 | 14,474 | 1.278 ms | 1.958 ms | 2.553 ms |
| [200](evidence/phase4-player-200-1280x720.json) | 9,742 | 9,741 | 1.915 ms | 2.914 ms | 3.973 ms |
| [300](evidence/phase4-player-300-1280x720.json) | 7,103 | 7,102 | 2.590 ms | 4.002 ms | 5.178 ms |
| [500](evidence/phase4-player-500-1280x720.json) | 4,065 | 4,064 | 4.569 ms | 6.968 ms | 8.420 ms |

The additional [100-unit 1440 by 1080 probe](evidence/phase4-player-100-1440x1080.json) also passed: 13,527 frame samples, 13,526 sampled renders, 20.016 accepted seconds and frame p50/p95/p99 of 1.349 / 2.148 / 2.687 ms. Its simulation/wall ratio was 0.999937, with zero observed overlap/invalid positions. The [capture](evidence/phase4-stress-100-1440x1080.png) was inspected. This is a second desktop viewport, not a physical tablet result; the recorded 0.8 scale implies approximately 1152 by 864 3D rendering before upscale.

These are **conservatively serialized offscreen throughput** measurements, including render-request, forced-canvas and GPU synchronization overhead. They do not establish ordinary displayed gameplay FPS, display-present latency, isolated GPU duration, physical input comfort, battery/thermal behavior or mobile performance. No Android/iOS device results are implied. Preserve the method and exact reports with any performance comparison.
