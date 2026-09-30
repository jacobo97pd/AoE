# Phase 10 — profiling and measured optimization

**Status: desktop profiling and bounded optimization verified; mobile qualification remains open.** All 26 optimized CPU cases match baseline state, fog and command traces, and all three corrected-build rendered comparisons pass their workload checks. CPU tick cost and recorded per-frame allocations decrease. Whole-frame timing does not show a consistent improvement; no ordinary displayed FPS or mobile performance pass is claimed.

## Environment and retained baseline

Measurements use Unity **6000.3.23f1**, Windows 11 (10.0.26200), Intel i5-10400F 2.90 GHz / 12 logical processors, 16,281 MB RAM and NVIDIA GeForce RTX 3060 Ti / 8,024 MB graphics memory. The CPU runner uses no graphics. The packaged runner uses D3D11, the Mobile URP pipeline, render scale **0.8**, MSAA **1**, no VSync and an uncapped frame target. No platform module or profiling package was installed.

The rendered baseline build **succeeded with zero errors and warnings**, size **163,950,464 bytes**, duration **18.0436048 seconds**, GUID `a39b23a98990479a8ff2e3d99692e50e`. Its complete runnable directory is preserved under `Builds/Phase10Baseline/`, including the executable and data, so later builds do not overwrite the comparison artifact. [Baseline build summary](../testing/evidence/phase10-baseline-build.txt).

The corrected final Windows build is GUID `d1b7b1cbc28740e994945476895e1295`, **163,954,032 bytes**, **13.820391 seconds**, with **zero errors and warnings**. All **[19 fresh standalone checks](../testing/evidence/phase10-player-matrix.md)** passed, including corrected result-screen presentation. All three final rendering reports and comparisons carry this GUID. [Final build summary](../testing/evidence/phase10-build.txt).

Rules SHA256 is `6F100B25035E54E4144761F51885F52E45051EBD6562EF6024AFC37601CB63C2`; Amber Crossing map SHA256 is `3EF7E44A71AE3C76C74C3DC04BD0BA7E8EFE43AA0BDC500AD2450487D493D072`. The CPU baseline records simulation/diagnostics source SHA256 `12E70131B7841F9FEF54937DDFCD66034030C8B3EEF60600B18EE030ECCC7A3A` and its own runner hash. These identify the measured inputs; they do not prove cross-platform determinism.

## CPU method and baseline

The full matrix contains **26 cases**: five synthetic army sizes, plus both factions in both victory modes at early and 300-second shipped checkpoints, all repeated twice. Each synthetic case advances **120 simulated seconds**; each shipped case samples **60 seconds**. A separate small world warms the runtime before measurement; each case still starts with a fresh World and fresh navigation caches.

Synthetic cases author 50/100/200/300/500 Aven Tender movers, eight additional gatherers, one Threadkeeper and completed infrastructure on a 128×128 map, with 5,000 of each resource and high starting capacity. An eleven-cell buffer isolates the opposing AI annex beyond attack range. Ordinary costs, timers and faction rules still apply; paid scripted charter, relay, research and training actions coexist with the AI. These are explicit noncombat scaling fixtures, not normal starting armies or complete matches. Shipped early/mid cases use ordinary 120 Food / 80 Wood / 0 Metal / 40 Stone starts and natural AI development, including combat.

`World.Tick`, AI calls and fixed scripted command submissions have separate timing/allocation windows. Navigation totals span those windows and include connectivity. Observation, state/trace serialization and reporting are excluded from the timed sections. GC/heap fields cover the wider diagnostic loop and include diagnostics overhead; heap deltas are not allocation totals. No forced collection is performed.

| Baseline workload | World tick p95, repetitions 1 / 2 | AI thinking-tick p95, repetitions 1 / 2 | Other observation |
| --- | --- | --- | --- |
| Synthetic 100 movers | 1.4970 / 1.5293 ms | 1.0085 / 1.0255 ms | 109 initial, 143 final total units |
| Synthetic 500 movers | 3.7829 / 3.7648 ms | 1.7579 / 1.7245 ms | 509 initial, 543 final total units |
| Shipped Aven Dominion, tick 6,000 onward | 1.0622 / 1.0185 ms | 0.9276 / 0.8823 ms | 51 initial, 55 final total units |

Synthetic 500-mover command p95 is **26.9745 / 27.1025 ms**, from only three measured group moves per repetition. Steady tick performance does not remove cold-order hitches. Navigation has aggregate time and maximum query time, without a query p95 histogram. The direct per-thread managed allocation API reports zero for a retained known 4,096-byte allocation, so its allocation fields remain **unavailable (-1)**.

All baseline and optimized repetition pairs agree on start/end observable state, command/result trace and sampled ticks. Fingerprints include public unit/building/player/resource/projectile/objective state and both players' visible/explored masks. They do not include every private path/work remainder and are not savegames. Full results: [baseline report](../testing/evidence/phase10-offline-cpu-baseline.md) / [JSON](../testing/evidence/phase10-offline-cpu-baseline.json), [optimized report](../testing/evidence/phase10-offline-cpu-optimized.md) / [JSON](../testing/evidence/phase10-offline-cpu-optimized.json).

## Verified CPU change

The fog cache keeps every existing refresh boundary. It compares ordered source identity, owner, cell position, effective vision radius and building footprint, and skips mask reconstruction only when those inputs are unchanged. Unit movement within one cell does not change the circular cell mask; entering another cell, creation, death or a relevant footprint/radius change invalidates the cached inputs. Five independent oracle tests compare the resulting masks with fresh recomputation. This changes repeated mask work without reducing fog accuracy or refresh opportunities.

The latest Unity gate passed **[347 EditMode tests](../testing/evidence/phase10-editmode.xml) and [66 PlayMode tests](../testing/evidence/phase10-playmode.xml), 413 total**, with the existing counter, natural-match and movement checks retained. The final presentation corrections received the 66-test PlayMode rerun; simulation, AI and CPU measurement code did not change. The optimized CPU matrix completed 26 cases and 13 matching repetition pairs. `Compare-OfflinePerformance.ps1` then verified all 26 start/end observable-state hashes, fog masks and ordered command/result traces against the baseline. The measurement runner and rules/map hashes are unchanged; optimized simulation/diagnostics source SHA256 is `F2C16585009A5A0FC3E4EB06C2993D795D0406C1CF98A4D224CF73FE905001B5`.

| Workload | Mean of two World p95 values, baseline → optimized | Change | Optimized World p95 range |
| --- | --- | --- | --- |
| Synthetic 50 movers | 1.2828 → 0.6307 ms | −50.8% | 0.6217–0.6396 ms |
| Synthetic 100 movers | 1.5132 → 0.7953 ms | −47.4% | 0.7916–0.7989 ms |
| Synthetic 200 movers | 2.1789 → 1.1239 ms | −48.4% | 1.1145–1.1332 ms |
| Synthetic 300 movers | 2.7254 → 1.4624 ms | −46.3% | 1.4406–1.4842 ms |
| Synthetic 500 movers | 3.7739 → 2.1410 ms | −43.3% | 2.1331–2.1489 ms |
| Shipped Aven Dominion, tick 6,000 onward | 1.0404 → 0.6645 ms | −36.1% | 0.6568–0.6721 ms |

Each value above is a mean of two independently reported p95 values, **not a pooled p95**. All thirteen workload groups have lower World tick p95 ranges in this pair of optimized repetitions. This supports a repeatable reduction under the declared desktop workload, without claiming statistical significance or a target-device frame budget.

AI thinking-tick p95 also falls from a mean 1.7412 to 0.8909 ms in the 500-mover fixture, and 0.9050 to 0.6046 ms in shipped Aven Dominion. AI timing includes its legal command submissions and their fog refreshes. The 100-mover AI results overlap the baseline range, so their mean change is noise-sensitive. The report keeps all per-run values.

**No navigation optimization is claimed.** The 500-mover navigation total is essentially unchanged, 177.2709 → 177.3251 ms on average, and its movement-command p95 remains 27.0385 → 26.5709 ms from three calls per repetition. Optimized first repetitions also retain **11.5024 ms** at 50 movers and **21.2877 ms** at 300 movers as move-command outliers. They are not removed from the report or assigned an unverified cause. Per-thread allocation measurements remain unavailable; GC/heap observations do not establish an allocation improvement.

The [primary comparison](../testing/evidence/phase10-offline-comparison-1280x720-r1.md) and [raw JSON](../testing/evidence/phase10-offline-comparison-1280x720-r1.json) retain all CPU cases as well as the corresponding rendered pair. `ComparisonValid = true` means workload identity checks passed, not that all latency or device targets were met.

## Packaged rendering method and baseline

`OfflineRenderProbe` drives both ordinary AIs from the unmodified shipped Aven/Dominion start to **tick 6,000**, without resource grants or scripted deaths. It then advances exactly **60 real-time warmup ticks** over three seconds and samples **400 ticks, from 6,060 to 6,460**, over twenty seconds. A fixed central camera views the crossing with normal local fog. The sample starts with 52 units, 28 military units and 23 active military views; it observes five deaths and up to four simultaneous projectiles. Two 1280×720 runs and one 1440×1080 run use the same gameplay checkpoint and final-state fingerprints.

Hidden Windows players can skip automatic rendering. The measurement therefore disables the automatic camera and submits **one explicit URP render request with Canvas updates into a persistent render texture, followed by synchronous one-pixel `ReadPixels`, per Unity loop**. Every sampled loop must have a matching unique `endCameraRendering` callback. This serializes rendering and GPU completion. Whole-frame values include engine scheduling, presentation, instrumentation, render submission and readback/synchronization overhead; they measure **conservative serialized offscreen throughput**, not ordinary display FPS or isolated GPU execution.

AI-pair and World tick timings are collected separately per tick. Presentation includes world views, fog, HUD and camera constraints. Navigation reports route-search/connectivity work only. Startup, known-allocation probing, checkpoint hashing, full PNG readback/encoding and JSON serialization are outside the sample. The normal 20 Hz simulation must remain within 5% of elapsed wall time. The measured baseline ratios are 0.99980–0.99983, with exact tick counts and matching rendered-frame counts.

| Baseline viewport / repetition | Rendered frames | Frame p50 / p95 | World p95 | Presentation p95 | Render/readback p95 |
| --- | --- | --- | --- | --- | --- |
| 1280×720 / 1 | 16,771 | 1.0462 / 2.1652 ms | 1.2667 ms | 0.2198 ms | 1.4877 ms |
| 1280×720 / 2 | 19,155 | 0.9179 / 1.8486 ms | 1.3066 ms | 0.2212 ms | 1.2067 ms |
| 1440×1080 / 1 | 16,804 | 1.0392 / 1.9885 ms | 1.3082 ms | 0.2397 ms | 1.3391 ms |

The baseline repeat spread is visible; small single-pair timing differences will not justify an improvement claim. Render scale 0.8 means approximately 1024×576 and 1152×864 3D rendering before upscale at the two output sizes. These viewports on a desktop GPU are not device benchmarks.

Completed-frame recorder p95 is **90 / 91 / 111 draw calls**, **9 / 9 / 9 SetPass calls**, and **14,944 / 14,968 / 17,072 triangles** for the three runs above. The exact counter inventory and units are retained. Frame timing statistics are enabled, but `FrameTimingManager` and the exact unit-checked `GPU Frame Time` recorder both return no valid positive GPU samples; GPU timing remains **unavailable (-1)**. No estimate is reconstructed from the CPU or readback times.

The completed-frame allocation recorder passes an independent **8,192-byte capability probe**, observing **8,328 bytes**. Mean whole-frame allocation is **4,469.38 / 4,447.25 / 4,466.61 bytes**, with p50 **4,294 bytes** and p95 **4,614 bytes** in each run. This covers the instrumented Unity frame, including gameplay and diagnostics. It does not validate the unsupported CPU per-thread allocation API.

Reported GC generation tuples are **(93,93,93), (106,106,106), (93,93,93)**. Unity Mono can expose the same non-generational collection count under each generation index; the entries must not be summed. Heap before/after values are retained without forced collection and are not an allocated-byte total.

Tracked baseline rendering reports are [1280×720 run 1](../testing/evidence/phase10-offline-render-baseline-1280x720-r1.json), [1280×720 run 2](../testing/evidence/phase10-offline-render-baseline-1280x720-r2.json) and [1440×1080](../testing/evidence/phase10-offline-render-baseline-1440x1080.json). Their original `TestResults/OfflinePlayer-baseline-*` folders also retain captures and logs. The earlier `OfflinePlayer-probe-1280x720` smoke uses an earlier art build and is explicitly excluded from final before/after comparisons.

## Final rendered comparisons

All three final reports pass with build `d1b7b1cbc28740e994945476895e1295`, exact ticks 6,060–6,460, matching checkpoint/start/end state fingerprints and matching unique render/frame counts. Simulation/wall ratios are **0.99975–0.99985**. The comparison also verifies identical resolution, camera, quality, rules/map and workload statistics. Final PNGs were captured outside timing from the completed render target: [1280×720](../testing/evidence/phase10-offline-profile-1280x720.png), [1440×1080](../testing/evidence/phase10-offline-profile-1440x1080.png).

| Final viewport / repetition | Rendered samples | Frame p50 / p95 | World p95 | AI pair p95 | Presentation p95 | Render/readback p95 |
| --- | --- | --- | --- | --- | --- | --- |
| 1280×720 / 1 | 16,373 | 1.0664 / 2.1752 ms | 0.7346 ms | 0.1954 ms | 0.2420 ms | 1.5281 ms |
| 1280×720 / 2 | 15,535 | 1.1161 / 2.2358 ms | 0.7386 ms | 0.1996 ms | 0.2347 ms | 1.6240 ms |
| 1440×1080 / 1 | 16,485 | 1.0675 / 1.9343 ms | 0.7068 ms | 0.2033 ms | 0.2082 ms | 1.3832 ms |

**There is no consistent whole-frame improvement.** Paired phone frame p95 increases **2.1652 → 2.1752 ms** and **1.8486 → 2.2358 ms**; tablet p95 decreases **1.9885 → 1.9343 ms**. These limited repetitions do not isolate the reason for those differences. The corrected presentation set and serialized render/readback workload must remain part of the interpretation. Lower World/AI cost is supported in all pairs, but it does not establish higher displayed FPS or faster GPU execution.

| Viewport / repetition | Mean recorded allocation/frame, baseline → final | Allocation p95, baseline → final | GC tuple, baseline → final |
| --- | --- | --- | --- |
| 1280×720 / 1 | 4,469.38 → **655.96 B** | 4,614 → **516 B** | (93,93,93) → (12,12,12) |
| 1280×720 / 2 | 4,447.25 → **663.52 B** | 4,614 → **516 B** | (106,106,106) → (12,12,12) |
| 1440×1080 / 1 | 4,466.61 → **654.99 B** | 4,614 → **516 B** | (93,93,93) → (11,11,11) |

Whole-frame mean allocation decreases by approximately **85%**, with p95 decreasing **88.8%** across all three pairs. These are capability-verified recorder values for the instrumented frame, not per-system allocation attribution or a zero-allocation claim. Different throughput produces different numbers of sampled frames, so per-frame values are compared. GC tuples are kept separate rather than summed. No retained-memory improvement is inferred from the live heap readings.

Final draw-call p95 is **91 / 90 / 111**, SetPass p95 remains **9 / 9 / 9**, and triangle p95 is **14,944 / 14,940 / 17,072**. These are effectively unchanged; no draw-cost reduction is claimed. Both GPU timing sources still provide no valid positive values, so GPU time remains unavailable.

| Final evidence | Paired comparison | Optimized raw report |
| --- | --- | --- |
| 1280×720 run 1 | [Markdown](../testing/evidence/phase10-offline-comparison-1280x720-r1.md) / [JSON](../testing/evidence/phase10-offline-comparison-1280x720-r1.json) | [Player JSON](../testing/evidence/phase10-offline-render-optimized-1280x720-r1.json) |
| 1280×720 run 2 | [Markdown](../testing/evidence/phase10-offline-comparison-1280x720-r2.md) / [JSON](../testing/evidence/phase10-offline-comparison-1280x720-r2.json) | [Player JSON](../testing/evidence/phase10-offline-render-optimized-1280x720-r2.json) |
| 1440×1080 | [Markdown](../testing/evidence/phase10-offline-comparison-1440x1080.md) / [JSON](../testing/evidence/phase10-offline-comparison-1440x1080.json) | [Player JSON](../testing/evidence/phase10-offline-render-optimized-1440x1080.json) |

The 20 copied CPU/comparison/render/build/capture artifacts were SHA256-checked against their originals. All three comparison JSON files explicitly identify the final build; superseded optimized output cannot satisfy this evidence set.

## Presentation changes and comparison gates

The captured baseline motivates a reusable selected-ID set in world view synchronization and objective-label state caching in addition to the verified fog change. These avoid repeated interface enumeration/boxing and unchanged label strings. Later visual and integration reviews corrected fog/public-label/UI draw ordering to queues 2998/2999/3000, suppressed stale unseen-enemy death cues while retaining owned-unit loss feedback, and corrected selection army-tag counts/filters including Ashrunners. These are presentation corrections; simulation, AI and the CPU runner are unchanged.

Final rendered comparisons cover the complete delivered presentation change set, including those correctness fixes. They therefore do not isolate the cache changes' causal contribution. No ECS/Jobs migration, unit balance change, vision approximation or general backend is introduced, and reduced CPU cost alone cannot establish improved whole-frame throughput.

The initial optimized build `9a4ae3d9d9c843739c429c8de0c61504` completed all three rendering checks, but is superseded by these presentation corrections. Its reports, logs, captures, comparisons and build summary are preserved in `TestResults/phase10-before-label-order/`. Only corrected-build results appear in the final comparison tables; CPU source and CPU comparison are unchanged.

Optimized full tests, CPU repetitions, packaged rendering and all three comparisons have passed their declared correctness/workload gates. `tools/Compare-OfflinePerformance.ps1` rejects missing/duplicate cases, incomplete sample windows, differing rules/map/fixture/runner/protocol/hardware, or changed observed state including fog and ordered command traces. It recomputes within-run repetition agreement instead of trusting flags alone. Optional rendered comparisons additionally require identical camera/quality, checkpoint/start/end fingerprints and exact sample ticks. GPU p95 is compared only when both reports provide the same valid timing source.

The script's own checks accepted baseline self-comparison and reordered case arrays, and rejected altered command traces and missing cases. Those checks validate the comparison wrapper; the actual optimized run and comparison above supply performance evidence. Per-repetition metrics and ranges remain visible, without pooled p95 values or implied statistical significance from two runs. No automatic budget pass follows from `ComparisonValid = true`.

## Limits and remaining qualification

Desktop results, final build metadata and reproducible comparison evidence are recorded above. Phase 4–6 performance data remains historical in the [performance budget](../technical/PERFORMANCE_BUDGET.md). Dense NarrowChoke congestion at 200/300/500 movers remains a behavioural limitation; this profiling work does not establish a fix.

The provisional mobile 60/30 FPS targets still need supported-device selection and ordinary on-screen CPU/GPU/memory measurements. Physical touch, thermal throttling, battery behaviour, sustained long matches and larger rendered armies have not been measured. A natural AI match finishing does not establish human fun or competitive faction balance.
