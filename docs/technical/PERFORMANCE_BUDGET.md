# Performance budget

## Targets, not measurements

Aim for **60 FPS** on capable tablets and a stable **30 FPS** floor on lower supported hardware. This means whole-frame budgets of approximately **16.7 ms** and **33.3 ms** respectively. No supported mobile device list or physical mobile measurements exist; the [phone quality tiers](#mobile-tier-2026-09-25) are measured only on a desktop told to render as a phone. Phase 10 verifies desktop offline CPU and whole-frame allocation reductions with matching observable behaviour. Final rendering pairs show no consistent whole-frame throughput improvement. The [Phase 10 report](../tasks/PHASE_10_REPORT.md) records this workload, while the [phase ledger](../tasks/CURRENT_PHASE.md) owns delivery status. Phase 4–6 measurements below remain historical and cannot establish current full-match performance.

Simulation runs at **20 Hz**: 50 ms between ticks is a scheduling interval, not an acceptable 50 ms main-thread spike. Work must fit within the target rendered frame budget. Profile both individual tick spikes and average cost; report catch-up behavior after pauses.

The [original baseline](../testing/evidence/phase4-baseline.md) and [pre-visibility checkpoint](../testing/evidence/phase4-before-visibility-cache.md) are preserved intermediate measurements. They justify specific changes and must retain their labels. Neither is the final optimized artifact. Earlier Phase 1–3 player smokes used accelerated ticks and establish functional history, not real-time performance.

## Phase 10 verified desktop measurements

CPU profiling uses 26 cases: 50/100/200/300/500 synthetic movers plus both faction assignments and victory modes at early and 300-second shipped checkpoints, each repeated twice. Synthetic cases run for 120 simulated seconds with active faction/research/economy work and an isolated opponent; their 5,000-each-resource authored budget and extra units are explicitly synthetic. Shipped cases use ordinary starts and natural AI progression for 60-second samples. World, AI and scripted-command windows are separate. Navigation totals span these windows; no per-query p95 is available.

The desktop CPU baseline records synthetic 100-mover World tick p95 **1.4970 / 1.5293 ms**, and 500-mover **3.7829 / 3.7648 ms**. These become 143 and 543 total units as the workload develops. The 500-mover scripted movement command p95 is **26.9745 / 27.1025 ms**, with only three command samples per repetition. Cold command spikes remain relevant even when steady tick cost is low. Direct managed allocation measurement fails its known 4,096-byte capability test and stays **unavailable (-1)**.

The exact-input fog cache retains refresh boundaries and matches five fresh-recomputation oracle tests. The current verification is **347 EditMode / 66 PlayMode tests, 413 total**; the 66-test PlayMode rerun covers final presentation corrections with no further simulation/AI/CPU-runner changes. The optimized CPU run completed all 26 cases with 13 matching repetition pairs. Baseline/optimized observable start/end state, both fog masks and command/result traces match in all cases. Mean-of-two World tick p95 values fall **1.5132 → 0.7953 ms (−47.4%)** at 100 synthetic movers, **3.7739 → 2.1410 ms (−43.3%)** at 500, and **1.0404 → 0.6645 ms (−36.1%)** in shipped Aven Dominion at the 300-second checkpoint. These are means of run-level quantiles, not pooled percentiles or statistical significance.

Navigation was not optimized: the 500-mover total remains approximately **177.3 ms** across the whole window, and move-command p95 averages **26.5709 ms** after the change. The optimized first repetitions retain **11.5024 ms at 50 movers** and **21.2877 ms at 300 movers** as command outliers. Unsupported per-thread allocation remains −1. CPU source hashes and all per-case ranges/counters are retained in the [comparison JSON](../testing/evidence/phase10-offline-comparison-1280x720-r1.json); a valid comparison does not establish a frame-budget pass.

The rendered baseline uses build `a39b23a98990479a8ff2e3d99692e50e`, preserved at `Builds/Phase10Baseline/Emberfield.exe`: 163,950,464 bytes, zero build errors/warnings. Both honest AIs advance the shipped Aven/Dominion match to tick 6,000, followed by 60 real-time warmup ticks. The measured window is **exactly ticks 6,060–6,460** over 20 seconds. It begins with 52 units, 28 military units and 23 active military views, and observes five deaths and up to four simultaneous projectiles. No stock or army is granted.

| Baseline viewport / repetition | Rendered frame samples | Serialized frame p95 | World tick p95 | Presentation p95 | Mean recorded allocation/frame |
| --- | --- | --- | --- | --- | --- |
| 1280×720 / 1 | 16,771 | 2.1652 ms | 1.2667 ms | 0.2198 ms | 4,469.38 B |
| 1280×720 / 2 | 19,155 | 1.8486 ms | 1.3066 ms | 0.2212 ms | 4,447.25 B |
| 1440×1080 / 1 | 16,804 | 1.9885 ms | 1.3082 ms | 0.2397 ms | 4,466.61 B |

`tools/Profile-OfflinePlayer.ps1` runs the hidden non-batch D3D11 player with one explicit URP/Canvas render and synchronous one-pixel readback per Unity loop. The camera is fixed over the crossing. Every timed frame must have a matching `endCameraRendering` callback; simulation must stay within 5% of real time. These numbers are **serialized offscreen throughput**, including render submission, Canvas, GPU synchronization, profiler collection and engine overhead. They are not ordinary displayed FPS. These Phase 10 runs used the Mobile URP asset as it stood then, with render scale 0.8 and MSAA off, so output resolution was not native 3D render resolution. Since Alpha 0.2 the shipped asset renders at scale 1 with 2x MSAA, a desktop renders through `AlphaLighting`'s runtime copy, and phones through a tier's copy (see [Mobile tier](#mobile-tier-2026-09-25)). Full PNG capture is outside timing.

The separate completed-frame `ProfilerRecorder` allocation counter passes its 8,192-byte capability test, observing 8,328 bytes. Its allocation values are valid for the whole instrumented frame, including probe/AI/presentation work; they do not make the unsupported per-thread CPU counter valid. Recorded GC tuples are **(93,93,93), (106,106,106), (93,93,93)**. Mono may expose the same non-generational collection through each index; **do not sum those entries**. Heap before/after values are live runtime measurements without forced collection, not allocation totals.

GPU frame statistics are enabled, but neither `FrameTimingManager` nor the exact `GPU Frame Time` recorder produces positive timing in any final run. GPU timing remains **unavailable (-1)**. The metric inventory and source provenance are retained; no GPU estimate is inferred from CPU/readback timing. Selected-unit membership and objective-label reuse reduce recorded allocations in the final complete presentation change set; the fog cache's CPU reduction and observed behavioural equivalence are verified above.

The final presentation change set also corrects fog/public-label/UI draw ordering (2998/2999/3000), suppresses stale hidden-enemy death cues and fixes army-tag selection counts/filters including Ashrunners. Final rendered comparisons include those corrections and cannot isolate cache-only effects or infer a whole-frame FPS improvement from lower tick cost. The prior optimized build and its runs are preserved as superseded diagnostics in `TestResults/phase10-before-label-order/`.

The corrected [distribution build](../testing/evidence/phase10-build.txt) `d1b7b1cbc28740e994945476895e1295` succeeded with **163,954,032 bytes**, **13.820391 seconds**, zero errors/warnings and **[19 passing standalone checks](../testing/evidence/phase10-player-matrix.md)**. All three final rendered reports and comparison JSONs identify that GUID, exact ticks 6,060–6,460, matching checkpoint/state/camera/quality and matching unique render counts. Simulation/wall ratios remain within 0.99975–0.99985.

| Final viewport / repetition | Final rendered samples | Frame p95, baseline → final | World p95, baseline → final | Mean recorded allocation/frame, baseline → final |
| --- | --- | --- | --- | --- |
| [1280×720 / 1](../testing/evidence/phase10-offline-comparison-1280x720-r1.md) | 16,373 | 2.1652 → 2.1752 ms | 1.2667 → 0.7346 ms | 4,469.38 → 655.96 B |
| [1280×720 / 2](../testing/evidence/phase10-offline-comparison-1280x720-r2.md) | 15,535 | 1.8486 → 2.2358 ms | 1.3066 → 0.7386 ms | 4,447.25 → 663.52 B |
| [1440×1080](../testing/evidence/phase10-offline-comparison-1440x1080.md) | 16,485 | 1.9885 → 1.9343 ms | 1.3082 → 0.7068 ms | 4,466.61 → 654.99 B |

Mean whole-frame allocation falls approximately **85%** and allocation p95 falls **4,614 → 516 B (88.8%)** in all three pairs. Final GC tuples are **(12,12,12), (12,12,12), (11,11,11)**, reported without summing generation indices. This is neither zero allocation nor a retained-heap improvement claim. Draw/SetPass/triangle counts remain effectively unchanged. Frame p95 is higher in both phone pairs and slightly lower in the tablet pair: **no consistent rendering-throughput improvement is established**, and the cause of that variation is not isolated. CPU/readback timing cannot supply missing GPU measurements.

## Measurement after the larger maps (2026-09-16)

Rebaked battlefields at 144×112, roughly 2.3 times the ground of the maps above, with denser scenery, a
sharper desktop shadow atlas, atmospheric fog and a command journal recording every accepted order. Same
protocol and the same 6,060–6,460 tick window, measured with `tools/Profile-OfflinePlayer.ps1 -Label tonight`:

| Viewport | Frame p50 | Frame p95 | World tick p95 | AI pair p95 | Presentation p95 | Rendered samples |
| --- | --- | --- | --- | --- | --- | --- |
| 1280×720 | 6.25 ms | 8.54 ms | 1.319 ms | 0.326 ms | 2.740 ms | 3,068 |

In absolute terms this is comfortable: the frame sits at about 117 fps at p95 and a tick spends 2.6% of the
50 ms scheduling interval. Against the Phase 10 final column it is a regression — world tick 0.73 → 1.32 ms
and frame p95 2.18 → 8.54 ms — and the cause is the deliberate change of workload, not a code defect found
by the comparison. `Compare-OfflinePerformance.ps1` cannot judge it: the map and fixture changed, which that
tool rejects by design.

An earlier version of this note proposed static batching for the landscape props as the cheapest headroom
left. That was wrong and is withdrawn: `FacetedTeam.mat` already has `m_EnableInstancingVariants: 1`, the
props share that one material and a small set of meshes, and the `_Sway` they differ by is declared as an
instanced property. They already draw as instanced batches. Static batching would *disable* instancing for
them and inflate memory with a combined mesh, so it is a regression dressed as an optimization.

Re-measured the same way after chimney smoke and the offline-AI retreat fix, `-Label smoke`:

| Viewport | Frame p50 | Frame p95 | World tick p95 | AI pair p95 | Presentation p95 | Rendered samples |
| --- | --- | --- | --- | --- | --- | --- |
| 1280×720 | 6.39 ms | 8.86 ms | 1.709 ms | 0.365 ms | 2.910 ms | 3,017 |

Frame p95 pays **0.32 ms** for the smoke, which is what a few translucent draws over the settlement should
cost. The World tick moving 1.32 → 1.71 ms is not paid by the smoke, which never touches the simulation:
the likeliest cause is that the repaired AI keeps more units alive by tick 6,060, so the fixed measurement
window simulates a busier battlefield than it used to. These are single runs, not means of repetitions, so
treat both deltas as indicative rather than settled.

The [phone](../testing/evidence/phase10-offline-profile-1280x720.png) and [tablet](../testing/evidence/phase10-offline-profile-1440x1080.png) captures show the actual completed target outside the sample window. The [Phase 10 report](../tasks/PHASE_10_REPORT.md) links all six raw player reports, three paired comparisons and both CPU matrices; copied artifacts were hash-checked against the measured originals.

`tools/Compare-OfflinePerformance.ps1` requires all 26 keyed cases and 13 verified repetition pairs. It rejects changed rules/map/fixture/measurement protocol, start/end observable state including both fog masks, command traces or truncated windows. Optional rendered comparisons also require identical checkpoint/ticks/camera/quality/state. Two-repetition ranges and changes are descriptive rather than statistical significance. No performance-budget pass follows from completing the benchmark.

## Meshy art against the procedural art (2026-09-25)

Same fixture and protocol, same build. One run used the Meshy units, buildings, scenery, ground and water. The other
passed `-emberfieldProceduralUnits`, which turns all of them off. The command was
`tools/Profile-OfflinePlayer.ps1 -Label meshy`, and the same with `-ExtraArguments -emberfieldProceduralUnits`.

| Art | Frame p50 | Frame p95 | World tick p95 | Presentation p95 | Rendered samples |
| --- | --- | --- | --- | --- | --- |
| Meshy | 6.05 ms | 8.59 ms | 1.709 ms | 2.917 ms | 3,137 |
| Procedural | 6.16 ms | 8.67 ms | 1.715 ms | 2.778 ms | 3,119 |

The captured frames confirm each run drew its own art. On this desktop the textured art costs nothing measurable. The
instanced scenery and one material per model and owner keep the draw count flat, and presentation p95 moves by about
0.14 ms. These are single runs at 1280×720 on a desktop GPU, so they say nothing about phones or tablets.

## Mobile tier (2026-09-25)

**No phone has run this.** The Android Build Support module is not installed for 6000.3.23f1
(`Editor/Data/PlaybackEngines` holds only `windowsstandalonesupport`), so there is no SDK, NDK, OpenJDK, APK or device.
Everything below was measured on the Windows development player told to render as a phone, on an i5-10400F and an
RTX 3060 Ti. Its milliseconds are a desktop's; the render statistics, the settings and the pictures are what carries
over. What a real measurement needs is listed at the end of this section.

On a phone (`Application.isMobilePlatform`) every match picks a tier from what the device reports, and `MobileQuality`
renders it through a runtime copy of `Mobile_RPAsset` set for that tier; neither that asset nor `QualitySettings.asset`
is edited. A desktop is left exactly as it was. Development players and the editor take
`-emberfieldMobileTier low|mid|high|auto` to behave as a phone of that tier, and `-emberfieldMobileTier baseline` to
behave as a phone did before the tiers: the Mobile quality level's values with the shipped asset untouched. Both skip
the desktop shadow upgrade. A desktop player does not contain the Mobile quality level (it excludes Standalone), so
`MobileQuality` sets that level's values itself; a PlayMode test keeps them in step with the settings file.
`tools/Profile-OfflinePlayer.ps1 -MobileTier low` measures a tier, and the probe now also records batches, vertices,
shadow casters, visible skinned meshes, texture, render-target and mesh memory, `FrameTimingManager` CPU times, the
pipeline's shadow and LOD settings, and a close still without the HUD next to the usual far one.

### Choosing the tier

`MobileTiers.Choose` is a pure function with EditMode tests. Unity reports a phone's memory less what the system keeps,
so a 4 GB phone shows about 3,700 MB and a 6 GB one about 5,500 MB.

| Tier | Rule |
| --- | --- |
| Low | OpenGL ES (a phone falls back to it when its Vulkan driver is missing or blocked), under 3,300 MB, under 4 cores, or a texture limit under 4096 |
| High | At least 5,300 MB, 6 cores and an 8192 texture limit, and reported graphics memory unknown or at least 1 GB |
| Mid | Everything else |

These thresholds come from spec sheets, not from devices that ran the game.

### What each tier asks for

| Setting | Low | Mid | High | Phone before the tiers | Desktop (unchanged) |
| --- | --- | --- | --- | --- | --- |
| Render scale, at most | 0.8 | 0.9 | 1.0 | 1.0 | 1.0 |
| 3D image height, at most | 720 rows | 900 rows | 1,080 rows | native | native |
| Dynamic resolution floor | 0.6 | 0.6 | 0.6 | none | none |
| MSAA | off | 2x | 2x | 2x | 4x |
| HDR | off | on | on | on | on |
| Main shadow map | 1024 | 2048 | 2048 | 2048 | 2048 |
| Shadow cascades / distance | 1 / 75 m | 1 / 80 m | 1 / 80 m | 2 / 80 m | 3 / 110 m |
| Soft shadows | no (hard) | yes | yes | yes | yes |
| Additional lights per object | 1 | 2 | 4 | 4 | 4 |
| LOD bias / mesh LOD threshold / maximum LOD level | 0.7 / 2 / 0 | 1 / 1.5 / 0 | 1.5 / 1 / 0 | 1 / 1 / 0 | 2 / 1 / 0 |
| Skin weights | 2 bones | 2 bones | 4 bones | 2 bones | 4 bones |
| Frame rate cap / vSync count | 30 / 0 | 60 / 0 | 60 / 0 | 60 / 0 | none / 1 (the display's own rate; Settings → Frame rate offers 60, 30 or unlimited, see [Smoothness fixes](#smoothness-fixes-2026-09-26-frame-pacing-and-hitches)) |

The render scale is the lower of the tier's own scale and what keeps the 3D image within its height, never below 0.5,
so a 2400×1080 phone on low renders the battlefield at 0.67 while the HUD stays at the panel's own resolution. On the
1280×720 fixture the three tiers start at 0.80, 0.90 and 1.00; at 1920×1080 at 0.67, 0.83 and 1.00.

**One cascade on every tier.** The battlefield camera is orthographic and stands 55 m back at a 55° pitch, so every
visible pixel lies 39–71 m deep, even at the widest zoom. The shipped two-cascade split puts its near cascade over the
first 20 m, which never holds a visible pixel, yet its casters are drawn and its tile halves the atlas. One cascade
spends the whole map on what is seen: mid and high get sharper shadows than before from half the caster draws, and low
gets about the old sharpness from a quarter of the map. Every tier's distance covers the 70.4 m far edge.

**HDR only off on low.** Bloom only starts above 1.2, and on the fixture the low tier's picture with and without HDR
differs in 0.11% of pixels, none by more than 12 levels. Mid and high keep it for the brighter fantasy effects.
Soft shadows cannot be switched off on a pipeline copy (the setting is internal), so low asks the sun for hard ones.
Main-light shadows and per-pixel additional lights stay on in every tier: `Mobile_RPAsset` prefilters the shader
variants without them, so switching them off at runtime would fall back to missing variants.

**Dynamic resolution** (`DynamicResolution`, pure and tested; phones only). Each frame it reads the work time, the
longest of CPU main thread, render thread and GPU from `FrameTimingManager`, and the wall-clock frame. Two seconds of
smoothed work above 92% of the budget, or of frames 15% over it, take the render scale down 0.1, never below the
floor. Six seconds under 70% of the budget with the cap held take it back up 0.1. A step up that is taken back within
ten seconds doubles the next wait, up to a minute. A single hitch counts as at most a quarter second at twice the
budget. Without work times the scale only goes down, because a phone that holds its cap cannot show spare time on the
wall clock. Changes log `EMBERFIELD_RENDER_SCALE`; the chosen tier logs `EMBERFIELD_MOBILE_TIER` with the device.
The offline probe freezes it at the tier's starting scale. `Application.lowMemory` unloads unused assets.

### Textures on Android

`MobileTextureImport` gives the game's art its own Android import settings; Windows keeps its own, and the .meta
diff adds only an Android block. The Meshy bakers (`MeshyUnitBaker`, `MeshyPropBaker`, `MeshyGroundImport`) call it on
the art textures they import, and **Emberfield/Art/Apply Android texture settings** wrote it into the 390 textures already
there. Unity stores the override without the Android module installed; it could not compress for Android here, so
the ASTC output itself is untested.

| Art | Android | Estimated memory, Windows → Android |
| --- | --- | --- |
| Units (78 maps) | 512, ASTC 6x6 colour and mask, 5x5 normal | 104.0 → 13.5 MB |
| Buildings (186) | 512, same blocks | 248.0 → 32.2 MB |
| Props (123) | 512; rocks under 1.5 m 256 | 92.0 → 18.6 MB |
| Painted ground (3 arrays) | 2048 atlas kept (four 1024 slices fill the screen), ASTC 6x6 | 16.0 → 7.1 MB |
| **Total** | | **460.0 → 71.4 MB** |

The estimate covers every faction and biome with full mip chains; a match loads a subset. UI textures are untouched.

### Budget

Whole frame: 33.3 ms on low, 16.7 ms on mid and high, which dynamic resolution defends on the GPU side only. On this
desktop presentation alone takes about 3 ms a frame at p95 and a simulation tick 1.7 ms; a phone's CPU is several
times slower, so CPU time is the first thing to measure on a device. The render budgets below are for the fixture's widest battle
view, shadow passes included. They are provisional until a device confirms them.

| Per frame | Low | Mid | High | With the tiers alone: low / mid / high | Now, with the lighter art: low / mid / high |
| --- | --- | --- | --- | --- | --- |
| Triangles | 400,000 | 550,000 | 700,000 | 591,318 / 612,623 / 623,120 | 166,638 / 263,610 / 318,281 |
| Draw calls | 200 | 200 | 200 | 155 / 155 / 154 | 149 / 155 / 154 |
| SetPass calls | 60 | 60 | 60 | 56 / 56 / 55 | 55 / 56 / 55 |
| Shadow casters | 300 | 450 | 450 | 395 / 405 / 405 | 53 / 406 / 406 |
| Render targets at 1920×1080 | 80 MB | 150 MB | 150 MB | 77 / 142 / 148 MB | 77 / 142 / 147 MB |
| Art textures on Android | 100 MB | 100 MB | 100 MB | 71 MB estimated, all art | the same |

With the tiers alone low was over its triangle and shadow-caster budgets: about 2,500 blocked-cell fillers with no
lower level cast shadows on this map, and units and buildings kept every triangle at any size. The lighter art below
puts every tier inside its budgets on the fixture, low by more than half. On the widest view of a forest, where the
fixture's fog hides most of the trees, low draws 166,000 triangles and 11 shadow casters (see the next section).

### Before and after

Same fixture and protocol as above (Amber Crossing, Aven against Dominion, ticks 6,060–6,460). The baseline is the
tier flag in its before form, recorded before any tier setting existed; the final runs use the finished build.
Texture memory was not recorded in the first baseline, so the table gives the final build's baseline run too.
Render statistics are medians of completed frames; texture memory includes render targets.

| Viewport | Run | Scale | Frame p50 / p95 | Batches | Draw calls | SetPass | Triangles | Vertices | Shadow casters | Render targets | Texture memory |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 1280×720 | Desktop, first baseline | 1.00 | 6.32 / 8.66 ms | 186 | 209 | 64 | 999,665 | 1,773,778 | 920 | 98 MB | — |
| 1280×720 | Phone before, first baseline | 1.00 | 6.23 / 8.82 ms | 157 | 180 | 59 | 800,125 | 1,412,893 | 671 | 66 MB | — |
| 1280×720 | Desktop, final | 1.00 | 6.43 / 8.87 ms | 186 | 209 | 64 | 999,665 | 1,773,778 | 920 | 98 MB | 170 MB |
| 1280×720 | Phone before, final build | 1.00 | 6.24 / 8.60 ms | 158 | 181 | 59 | 802,040 | 1,415,585 | 671 | 66 MB | 138 MB |
| 1280×720 | Low | 0.80 | 5.60 / 8.10 ms | 132 | 155 | 56 | 591,318 | 1,037,010 | 395 | 39 MB | 111 MB |
| 1280×720 | Mid | 0.90 | 5.97 / 8.44 ms | 132 | 155 | 56 | 612,623 | 1,063,720 | 405 | 70 MB | 142 MB |
| 1280×720 | High | 1.00 | 6.03 / 8.44 ms | 131 | 154 | 55 | 623,120 | 1,078,483 | 405 | 70 MB | 142 MB |
| 1920×1080 | Desktop, first baseline | 1.00 | 6.58 / 9.08 ms | 185 | 207 | 64 | 997,701 | 1,769,681 | 919 | 211 MB | — |
| 1920×1080 | Phone before, first baseline | 1.00 | 6.40 / 9.02 ms | 157 | 180 | 59 | 800,125 | 1,412,893 | 671 | 144 MB | — |
| 1920×1080 | Desktop, final | 1.00 | 6.42 / 8.90 ms | 186 | 209 | 64 | 999,769 | 1,773,778 | 920 | 211 MB | 283 MB |
| 1920×1080 | Phone before, final build | 1.00 | 6.46 / 8.87 ms | 158 | 181 | 59 | 802,040 | 1,415,585 | 671 | 144 MB | 216 MB |
| 1920×1080 | Low | 0.67 | 6.01 / 8.42 ms | 132 | 155 | 56 | 591,318 | 1,037,010 | 395 | 77 MB | 149 MB |
| 1920×1080 | Mid | 0.83 | 6.44 / 8.90 ms | 132 | 155 | 56 | 612,623 | 1,063,720 | 406 | 142 MB | 214 MB |
| 1920×1080 | High | 1.00 | 6.37 / 8.72 ms | 131 | 154 | 55 | 623,128 | 1,079,185 | 405 | 148 MB | 220 MB |

Against the phone as it was, every tier makes 26 or 27 fewer draw calls, about a quarter fewer triangles and vertices and 40%
fewer shadow casters, almost all from the single cascade; low also halves the render-target memory at 1080p. On this
desktop the frame barely moves (low saves about 0.5 ms at p95), because the serialized probe is bound by CPU work and
GPU synchronization, not by the RTX 3060 Ti; a phone's GPU is where the statistics above turn into time. World tick
p95 stays at 1.67–1.74 ms and presentation p95 at 3.0–3.1 ms in every run. GPU frame time and the render thread's
time remain unavailable on this D3D11 player. The desktop is unchanged: its render statistics match the first baseline within two draw
calls, and its far still differs from one taken with the build before this change in 0.7% of pixels, none by more than
12 levels (small moving details), against 1.0% between the two desktop runs of this work.

The stills put the desktop, the phone as it was and the three tiers side by side from the same moment:
[far, 1280×720](../testing/evidence/mobile-tiers-far-1280x720.jpg),
[close, 1280×720](../testing/evidence/mobile-tiers-close-1280x720.jpg) and
[close detail, 1920×1080](../testing/evidence/mobile-tiers-close-detail-1920x1080.jpg). Mid and high are hard to tell
from the desktop; their single-cascade shadows are a little darker and crisper than before. Low is visibly softer,
most at 1080p where it renders 1280×720, and its hard shadows show a faint stair on long edges; units and objective
labels stay readable. The offscreen probe draws the HUD through the camera, so the far stills scale the HUD with the
3D image; in the game the HUD is an overlay at the panel's resolution.

### Lighter art for the tiers (2026-09-25)

The low tier was over its triangle and shadow-caster budgets because of the art: about 2,500 blocked-cell fillers on
Amber Crossing (1,900 of them trees) with no lower level, all casting shadows, and units and buildings of 4,000–5,300
triangles drawn a few dozen pixels tall. Each now has a lighter level that the phone tiers draw sooner; a desktop keeps
the detailed level at every zoom the game allows. How each level is made is in
[meshy-pipeline.md](../art/meshy-pipeline.md#levels-of-detail).

| | Low | Mid | High | Phone before the tiers | Desktop |
| --- | --- | --- | --- | --- | --- |
| Fillers leave the detailed level below this share of the screen | 0.12: trees past 12.4–13.7 m (0.23 until the swap below: every tree from a 7 m zoom out) | 0.12: the same | 0.08: trees past 19–21 m | none: one mesh | none: one mesh |
| Filler shadows | off | on | on | on | on |
| Gatherable nodes (prefab switch 0.06) | one renderer, swapped at the zoom where the tier's 3 m trees switch, whatever the node's size: 12.5 m | the same: 12.5 m | the same: 18.8 m | 0.06 | 0.06 |
| Culture buildings (switch 0.08 over the LOD bias) | a 1.9 m shelter past 8.4 m (just beyond the play zoom), a 4.6 m keep past 20 m | shelter past 12 m | shelter past 18 m | shelter past 12 m | never |
| Units draw their whole mesh down to this share, one Mesh LOD level lighter per halving | 0.2 | 0.1 | 0.06 | whole mesh | whole mesh |

A model fills its size over twice the orthographic size of the screen: 5 m zoomed right in, 8 m at the play zoom, 22 m
at the widest. Unity's LOD groups compare that share multiplied by the LOD bias, orthographic camera included (checked:
a group with a 0.1 switch drew its far level at bias 1 and its near one at bias 2 at the same zoom); the buildings use
them. The fillers and gatherable nodes do not: every copy of a model switches at one zoom whatever its own scale (on low
and mid a broadleaf at 12.4 m, a pine at 13.7 m; a 1 m rock at 4.2 m, so at every zoom), so on a phone tier each keeps
one renderer and `SceneryDetail` swaps the shared mesh of every copy of a model when the zoom crosses the model's
switch, and back once the zoom is 10% inside it again, so a pinch resting on a switch never flickers. A stand at rest
never mixes the two levels, and no filler is ever culled.

**Unity Mesh LOD.** Checked in a PlayMode probe on this project before choosing, with the Meshy Prop material on 576
fillers and 30 skinned trolls:

- The GPU-instanced Meshy Prop shader ignores it: the triangles did not change at any zoom or threshold, nor with
  `Renderer.forceMeshLod` set to 1. Instancing kept its batches, but only because the level never changed. A LOD
  group of two renderers on separate meshes kept the same 10 batches and cut the triangles, so the fillers carry two
  separate meshes (now swapped per model with the zoom rather than chosen by a group per filler, see below).
- On the SRP-batched Meshy Unit shader (units, buildings) a forced level works, skinned meshes included, and the
  animation keeps playing, but Unity's own selection never leaves the first level under an orthographic camera
  (the same triangles from zoom 5 to 22 at thresholds 1, 2 and 4). So the units carry the levels in their own mesh and
  `UnitMeshDetail` forces one from the zoom on a phone; buildings use a LOD group.

**The fillers were lying on their sides.** Each filler FBX held one mesh, which Unity puts on the file's root, and the
baker measured the mesh against that root, dropping its axis turn: every Meshy tree, palm and rock on a blocked cell
stood on its side (a 3 m tree 1.7 m tall, its footprint turned up). A far level gives the mesh its own node, and the
baker now applies the root's turn for a one-mesh file too. This is the one change a desktop sees: its forests now
stand as authored ([before and after](../testing/evidence/scenery-fillers-upright-1280x720.jpg)); the triangles on
the fixture are unchanged (999,433 against 999,665) and its shadow casters rise from 920 to 938.

**The fixture.** Same fixture and protocol as above, one build with this work; the 1280×720 frame times are the
median of three runs, the rest single runs. Render statistics are medians of completed frames.

| Viewport | Run | Scale | Frame p50 / p95 | Batches | Draw calls | SetPass | Triangles | Vertices | Shadow casters | Render targets |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 1280×720 | Desktop | 1.00 | 6.85 / 9.34 ms | 186 | 209 | 64 | 999,433 | 1,792,176 | 938 | 98 MB |
| 1280×720 | Phone before the tiers | 1.00 | 6.46 / 9.00 ms | 158 | 181 | 59 | 740,421 | 1,352,967 | 675 | 66 MB |
| 1280×720 | Low | 0.80 | 6.44 / 9.04 ms | 126 | 149 | 55 | 166,638 | 358,693 | 53 | 39 MB |
| 1280×720 | Mid | 0.90 | 6.00 / 8.79 ms | 132 | 155 | 56 | 263,610 | 613,593 | 406 | 69 MB |
| 1280×720 | High | 1.00 | 6.12 / 8.76 ms | 131 | 154 | 55 | 318,281 | 649,064 | 406 | 70 MB |
| 1920×1080 | Desktop | 1.00 | 6.91 / 9.44 ms | 186 | 209 | 64 | 1,006,155 | 1,792,176 | 938 | 211 MB |
| 1920×1080 | Phone before the tiers | 1.00 | 6.81 / 9.28 ms | 158 | 181 | 59 | 745,335 | 1,352,961 | 675 | 143 MB |
| 1920×1080 | Low | 0.67 | 5.77 / 8.51 ms | 126 | 149 | 55 | 166,638 | 358,693 | 53 | 77 MB |
| 1920×1080 | Mid | 0.83 | 6.64 / 9.18 ms | 132 | 155 | 56 | 263,654 | 613,687 | 407 | 142 MB |
| 1920×1080 | High | 1.00 | 6.71 / 9.11 ms | 131 | 154 | 55 | 318,289 | 649,070 | 407 | 147 MB |

Against the tiers as they were (the table above), triangles fall 72% on low (591,318 to 166,638), 57% on mid and 49%
on high, and low's shadow casters fall from 395 to 53; draw and SetPass calls hold or drop by a few. The desktop's
render statistics are unchanged but for the upright forest (938 shadow casters for 920). "Phone before the tiers" is
the Mobile quality level (LOD bias 1), which now also draws the buildings' far levels when zoomed out, so it no longer
reproduces the phone before this change: 740,421 triangles against 802,040.

Which change buys what, on low at 1280×720, from a build with a switch for each (not shipped):

| Low, one change taken back | Triangles | Shadow casters | Batches |
| --- | --- | --- | --- |
| Everything | 166,638 | 53 | 126 |
| Fillers keep their shadows | 221,678 | 398 | 132 |
| Fillers keep their detailed level at every zoom | 246,632 | 53 | 126 |
| Units keep their whole mesh | 271,326 | 53 | 126 |

On mid, units keeping their whole mesh draw 373,742 triangles instead of 264,238. The same runs show why the frame
times above say little: taking back any one change moves low's frame by at most 0.15 ms at p50, well inside the run to
run spread. The serialized offscreen probe waits for the desktop's CPU and GPU synchronization, not for the RTX 3060 Ti
to draw triangles. Its desktop ran 0.4 ms slower than the previous build's with unchanged render statistics; that
was not other work in the tree but the fillers' LOD groups, which the desktop then built too (next paragraph).
Compare milliseconds only within one build; the statistics are what carries over to a phone.

**A desktop keeps one renderer per filler.** As first committed, every filler on a desktop also carried a LOD group
and a far renderer it never switched to. Two builds that differ only in that, profiled in turn four times each at
1280×720: with the group, frame p50 6.79–6.87 ms, p95 9.23–9.61 ms, presentation p95 3.26–3.49 ms; with one renderer,
p50 6.42–6.46 ms, p95 8.77–8.87 ms, presentation p95 3.05–3.06 ms, back to the desktop numbers before this work.
The render statistics are identical (186 batches, 209 draw calls, 999,433 triangles, 938 shadow casters), so the
fillers' far level and group are now built on the phone tiers only. The table above was measured before that change;
the tiers are unaffected by it.

**The phones swap meshes instead of a LOD group per filler.** The phone tiers still paid for a LOD group and a second
renderer on every filler, although every copy of a model switches at the same zoom. Now each filler keeps one
renderer and `SceneryDetail` swaps the shared mesh of every copy of a model when the zoom crosses the model's switch,
and back once the zoom is 10% inside it again. Nothing runs between zoom changes and nothing is allocated (a PlayMode
test checks both); both meshes keep the instanced material, so the batches do not change. A gatherable node gives up
its prefab's group the same way on a phone and swaps at the zoom where the tier's 3 m trees do, whatever its size;
its one renderer keeps the felling sway through a swap. What the swap draws is exactly what the groups drew: the
scenery stills of the desktop and mid are pixel-identical before and after, and so are low's at every shot the
retune below does not touch. Crossing a switch swaps up to 2,500 meshes in one frame, 0.74–0.84 ms on this desktop,
once per crossing.

**Low draws mid's trees.** At 0.23 low drew every tree as its far shell from a 7 m zoom out, the play zoom included,
where the closed shells read as rounded blobs, to save 240 triangles a tree. Low now switches at mid's 0.12: every
tree is detailed to a 12.4–13.7 m zoom and comes back inside 11.3–12.5 m. Low still casts no filler shadows. The
fixture's widest view draws the far trees either way, so its statistics do not move (166,638 triangles and 53 shadow
casters against 400,000 and 300); on the forest stills low now draws 91,610 triangles at the play zoom (60,022 before,
the desktop 173,745) and 201,239 in the town (152,166). [Desktop, low before and low now](../testing/evidence/scenery-swap-desktop-low-1280x720.jpg)
at the play zoom, the widest zoom and in the town: at the play zoom low now looks like the desktop without shadows,
berry bushes red; at the widest zoom nothing changed.

Two builds that differ only in this work, profiled in turn on the fixture (rounds alternate which build goes first;
medians of the rounds, the range in brackets):

| Viewport | Tier | Frame p50, before → after | Frame p95, before → after | Presentation p95, before → after | Triangles | Shadow casters |
| --- | --- | --- | --- | --- | --- | --- |
| 1280×720, 4 rounds | Desktop | 6.41 (6.09–6.49) → 6.43 (6.29–6.48) ms | 8.80 (8.60–9.37) → 8.88 (8.70–8.97) ms | 3.06 → 3.08 ms | 999,433 → 999,433 | 938 → 938 |
| 1280×720, 4 rounds | Low | 6.51 (6.46–6.62) → 6.09 (6.06–6.12) ms | 9.09 (8.96–9.14) → 8.64 (8.49–8.72) ms | 3.27 → 3.05 ms | 166,638 → 166,638 | 54 → 53 |
| 1280×720, 4 rounds | Mid | 6.21 (5.99–6.40) → 5.64 (5.63–5.72) ms | 8.97 (8.73–9.17) → 8.24 (8.19–8.36) ms | 3.29 → 3.03 ms | 263,654 → 263,654 | 407 → 407 |
| 1920×1080, 3 rounds | Desktop | 6.89 (6.30–7.58) → 6.93 (6.67–7.33) ms | 9.96 (8.83–10.95) → 9.94 (9.12–10.44) ms | 3.17 → 3.25 ms | 1,005,543 → 1,005,543 | 938 → 938 |
| 1920×1080, 3 rounds | Low | 5.60 (5.22–5.94) → 5.54 (5.49–5.63) ms | 8.21 (8.21–8.69) → 8.05 (8.03–8.37) ms | 3.21 → 3.01 ms | 165,174 → 166,646 | 52 → 54 |
| 1920×1080, 3 rounds | Mid | 6.63 (6.34–6.72) → 6.29 (5.69–6.40) ms | 9.10 (8.89–9.33) → 8.79 (8.07–8.90) ms | 3.24 → 3.06 ms | 263,654 → 263,654 | 407 → 407 |

At 1280×720 low and mid drop 0.4–0.6 ms at frame p50 and 0.5–0.7 ms at p95, and presentation p95 by a quarter of a
millisecond, with the before and after ranges apart: the groups' per-frame cost that the desktop shed earlier. At
1920×1080, where this desktop's runs spread wider, presentation p95 drops by 0.18–0.2 ms again and frame p95 by
0.15–0.3 ms. The desktop is unchanged, as it should be: it built no groups. The render statistics are the same before
and after on every tier; low's 1080p medians differ by one visible unit, and their maxima are identical (179,834
triangles, 62 shadow casters). Low stays inside its budgets at both sizes.

**Forest and coast with nothing hidden.** The fixture's fog hides most of Amber Crossing's forest.
`-emberfieldSceneryStills` explores the whole map and aims at the thickest stand of blocked cells, at the widest (22 m),
play (8 m) and closest (5 m) zooms, plus the review's town at the play zoom. Triangles / batches / shadow casters at
1280×720:

| Map | Run | Widest | Play | Closest | Town |
| --- | --- | --- | --- | --- | --- |
| Amber Crossing | Desktop | 1,044,217 / 73 / 1,706 | 173,745 / 32 / 184 | 126,051 / 32 / 117 | 354,296 / 70 / 334 |
| Amber Crossing | Phone before the tiers | 792,264 / 61 / 1,113 | 173,346 / 32 / 183 | 126,051 / 32 / 117 | 336,899 / 69 / 293 |
| Amber Crossing | Low | 165,950 / 45 / 11 | 91,610 / 28 / 0 (60,022 at 0.23) | 74,758 / 28 / 0 | 201,239 / 64 / 30 (152,166 at 0.23) |
| Amber Crossing | Mid | 299,510 / 53 / 812 | 155,347 / 33 / 183 | 116,932 / 33 / 117 | 285,437 / 70 / 293 |
| Amber Crossing | High | 299,509 / 52 / 812 | 155,346 / 32 / 183 | 126,051 / 32 / 117 | 306,382 / 69 / 293 |
| Sapphire Coast | Desktop | 929,459 / 62 / 1,345 | 242,733 / 32 / 282 | 188,747 / 31 / 215 | – |
| Sapphire Coast | Phone before the tiers | 737,966 / 52 / 965 | 228,633 / 31 / 250 | 175,347 / 31 / 185 | – |
| Sapphire Coast | Low | 153,249 / 37 / 10 | 88,854 / 28 / 0 (69,891 at 0.23) | 74,328 / 28 / 0 | – |
| Sapphire Coast | Mid | 281,802 / 44 / 729 | 160,474 / 32 / 250 | 128,068 / 32 / 185 | – |
| Sapphire Coast | High | 285,230 / 43 / 729 | 160,473 / 31 / 250 | 175,347 / 31 / 185 | – |

The widest forest view drops from 792,264 triangles on the phone as it was to 165,950 on low and 299,510 on mid.
Low's play and town shots were retaken with the swap and mid's share. Its other shots came out the same, pixel for
pixel, and so did every shot of the desktop and mid on Amber Crossing, retaken too. Low draws fewer triangles than mid at the same trees because its units draw a lighter
level, its buildings switch sooner (LOD bias 0.7) and its fillers cast no shadows.

**The grass stays.** The same stills taken again without the scattered grass and reed tufts show what they cost: 3 of
73 batches and 4,968 of 1,044,217 triangles on the widest forest view (2 batches and 4,518 triangles on the coast),
because the tufts share one instanced material. Thinning them on low would save nothing worth the bare ground.

**Tierras de Leyenda (2026-09-25).** The fantasy map draws each land in its own biome: a ground shader that samples
only the biomes a pixel has a share of, lava with its own shader, per-land stands, nodes, tufts and water, embers,
glimmers and smoke (`LandMotes`, a few hundred quads flown by their shader, none of the embers or glimmers on low and
half the smoke) and a grade per land that follows the camera (`LandAtmosphere`, two extra global volumes). Measured
with `-emberfieldSceneryStills <folder> legend_lands verdant ashen` (elves against orcs, so all three biomes show),
1600×900, triangles / batches / shadow casters:

| Shot | Desktop | Low | Mid | High |
| --- | --- | --- | --- | --- |
| Widest (22 m) on the thickest stand | 959,950 / 122 / 1,420 | 163,048 / 66 / 25 | 267,055 / 84 / 605 | 363,276 / 82 / 605 |
| Play (8 m) on the thickest stand | 193,582 / 51 / 234 | 91,756 / 39 / 4 | 154,330 / 51 / 188 | 154,371 / 49 / 188 |
| Elven settlement, play zoom | 304,728 / 61 / 357 | 138,836 / 51 / 22 | 226,416 / 60 / 242 | 241,969 / 57 / 242 |
| Orc settlement, play zoom | 218,580 / 53 / 172 | 119,233 / 50 / 16 | 169,686 / 56 / 172 | 185,634 / 54 / 172 |
| Volcano and lava river (10 m) | 277,446 / 50 / 366 | 84,784 / 36 / 5 | – | 157,813 / 47 / 224 |

Measured the same way, same build (`D:/EmberfieldWorkingCache/AoE/MobileTierBuildProject`, `-emberfieldSceneryStills <folder> legend_lands verdant ashen -emberfieldMobileTier mid`, 1600×900): mid's shadow caster counts already match high's on every shot above, because only low thins the caster set (fewer casters, buildings switching sooner); mid and high differ from each other by triangle detail (the mesh LOD and scenery-swap zoom shares), not by which objects cast. Mid's triangles sit between low's and high's on every shot, as they should, and stay under high's 700,000 ceiling; its shadow casters follow high's rather than low's stricter 300, which is expected since low is the one tier that thins them. The lava river shot was not retaken for mid; the four shots above are the ones a phone actually reaches at the play zoom and widest view.

Every game zoom stays inside low's 400,000 triangles and 300 shadow casters and high's 700,000 triangles. The tufts
cost 5 batches and 6,504 triangles of the widest view. A whole-map still (zoom 62, which no player reaches) draws
429,794 triangles on low and 718,931 on high.

**Pictures.** [The widest forest view](../testing/evidence/scenery-lod-forest-far-1280x720.jpg) on the desktop, the
phone as it was, mid and low: the forest keeps its outline and density on every tier; low loses the trees' shadows and
its crowns read as rounded lobes. [Play and closest zoom](../testing/evidence/scenery-lod-forest-play-close-1280x720.jpg),
desktop against low at 0.23: at the play zoom every tree on low was the far shell, zoomed right in every tree is
detailed again. Since the retune low is detailed at the play zoom too
([desktop, low before and low now](../testing/evidence/scenery-swap-desktop-low-1280x720.jpg)).
[The far levels held up close](../testing/evidence/scenery-lod-far-levels-1280x720.jpg), nearer than any tier ever
draws them: tree crowns as closed shells with no holes, the town's buildings at 40% nearly indistinguishable, units at
their third level. [The coast](../testing/evidence/scenery-lod-coast-1280x720.jpg): palms and rocks keep their shape
at 40%. A tree being cut sways at either level and through a swap (PlayMode test on the low tier).

### What a device measurement needs

1. The Android Build Support module for Unity 6000.3.23f1 with its SDK, NDK and OpenJDK
   (`tools/Test-AndroidReadiness.ps1` lists what is missing), then `tools/Build-Unity.ps1 -Target Android`.
2. One phone per tier with USB debugging: a 3–4 GB phone with a Mali-G52 or Adreno 610 class GPU, a 6 GB phone with
   an Adreno 6xx or Mali-G7x, and a recent 8 GB or larger phone.
3. On each: the tier it chooses (`EMBERFIELD_MOBILE_TIER` in logcat), a 20-minute match with a battle, frame times from
   `FrameTimingManager` (CPU and GPU), every `EMBERFIELD_RENDER_SCALE` change, memory (`adb shell dumpsys meminfo`),
   and whether the device throttles when warm. Check that the ASTC textures import and look right.
4. Then set the thresholds of `MobileTiers.Choose`, the 30/60 caps and the budgets above from what the phones do,
   and look at the far levels on each phone's own screen: the switch shares (0.12 / 0.12 / 0.08 for scenery,
   0.2 / 0.1 / 0.06 for units) were set on a desktop monitor.

These builds were made from a project shell on D: (junctions to this project's folders, like
`tools/Build-PirateCrew.ps1` does): with about 1 GB free on C:, `tools/Build-Unity.ps1` run in place filled
`resources.assets` with zeros past 44 MB without reporting an error, then on the next attempt failed with
"Failed to write file: resources.assets".

## Smoothness (2026-09-26)

A player asked for the game to feel **extremely smooth**. Five scouting passes (pacing, hitches, presentation,
simulation) had already read the source and proposed suspects without running anything. This section extends the
measurement harness to see tail behaviour, not just p50/p95, and records a baseline against the current build,
before changing any of the suspects. Nothing under `Assets/Game/Simulation` was touched, so the sacred
determinism guarantee is not at risk here and no state-hash replay was required for this work; every change below
is presentation/tooling only.

### Harness extension

`OfflineRenderProbe` (`tools/Profile-OfflinePlayer.ps1`), `AlphaMatchMetrics` (`MatchController.Metrics`) and
`MovementStressSession` (`tools/Profile-MovementPlayer.ps1`) now report, beside the existing p50/p95:

- **p99, p99.9 and max** frame time (`OfflineDistribution.P999`/`StdDev` are new fields; every existing
  `OfflineDistribution` — frame, world tick, AI, presentation, render, GPU, and every `ProfilerRecorder` counter
  including `GC Allocated In Frame` — gained them for free).
- **Hitch count**: a sampled frame counts as a hitch when it is both over twice the sampled median **and** over
  33 ms (one 30 fps frame). `HitchThresholdMilliseconds` records the exact cutoff used, since it adapts to the
  run's own median rather than being a fixed number.
- **Frame-time standard deviation** (pacing), alongside the mean/percentiles already present.
- **GC bytes/frame and collection count** were already available (`Counters[3]`, `GcCollections`); they now also
  carry p99/p99.9/stddev like every other counter.
- **World tick p99/max** (`WorldTickMilliseconds.P99`/`.Maximum`) were already computed; nothing new was needed
  beyond printing them.
- **The worst 10 sampled frames**, each split into `WorldTickMilliseconds`/`AiMilliseconds`/
  `PresentationMilliseconds`/`RenderMilliseconds`/`UnaccountedMilliseconds` (engine/GC/driver overhead the probe
  does not time explicitly) with a `DominantMarker` naming whichever was largest.
- **`InitialPresentationSyncMilliseconds`**: a new, separate, one-off measurement. The existing fixture already
  ran the match headless for 6,000 ticks before ever calling `SyncPresentation`; that first call — where every
  unit/building type reached by tick 6,000 pays its first `Resources.Load`/`Instantiate`/shader-variant compile,
  all at once — was previously outside every timed window. It is now timed on its own as a cheap, targeted proxy
  for a cold first-use hitch (see below); it is not a claim about any single in-match first production.

`AlphaMatchMetrics` gained a second, independent sample set (`SessionSamples`, bounded at 20,000 entries, never
overwritten) beside its existing ~5 s rolling `SampleWindow` used for the live HUD reading. The rolling window is
untouched — the HUD's instantaneous FPS/frame-p95 reading behaves exactly as before — but `Session*` properties
(`SessionFrameP99/P999/Max/StdDevMilliseconds`, `SessionFrameHitchCount`, `SessionTickP99/MaxMilliseconds`, …) now
give a whole-session view for offline reporting. `ProductShellSmoke`'s report picks these up. `MovementStressSession`
got the matching p99.9/max/stddev/hitch fields for `Profile-MovementPlayer.ps1`'s Stress-mode scenarios, without
its own GC/allocation instrumentation (Stress mode never carried `ProfilerRecorder` counters; adding that
capability-probe machinery was judged out of scope for this pass and is listed under what remains, below).

All of the above is additive: existing fields, existing behaviour and the `AlphaMatchMetricsTests` PlayMode suite
are unchanged. `tools/Verify-Unity.ps1 -Stage Compile` passed (`EMBERFIELD_VERIFY_OK`) before building.

### Scenario (a): the existing fixture (Amber Crossing, Aven/Dominion, ticks 6,060–6,460)

Same protocol as every prior entry in this document. Build GUID `779be87f7b1649dbbebd8ecb8f8f923b`
(`D:/EmberfieldWorkingCache/AoE/MobileTierBuildProject`, zero build errors/warnings).

| Viewport | Frame p50/p95/p99/p99.9/max | Frame stddev | Hitches (>threshold) | World tick p95/p99/max | GC alloc/frame median/p95/max | GC collections (0/1/2) | Cold first-use proxy |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 1280×720, desktop | 6.34 / 8.79 / 9.95 / 11.81 / 13.12 ms | 1.02 ms | 0 (>33.0 ms) | 1.723 / 2.118 / 3.868 ms | 9,544 / 21,746 / 958,044 B | 19/19/19 | 447.39 ms |
| 1920×1080, desktop | 6.59 / 9.16 / 10.51 / 35.87 / 39.10 ms | 1.61 ms | 3 (>33.0 ms) | 1.683 / 2.173 / 2.527 ms | 9,544 / 22,132 / 958,044 B | 19/19/19 | 50.29 ms |
| 1280×720, low tier | 6.06 / 8.45 / 9.61 / 11.34 / 12.51 ms | 1.01 ms | 0 (>33.0 ms) | 1.721 / 2.196 / 2.669 ms | 9,544 / 21,126 / 958,044 B | 20/20/20 | 51.14 ms |

Commands: `tools/Profile-OfflinePlayer.ps1 -Label smoothness -Width 1280 -Height 720`; the same with
`-Width 1920 -Height 1080`; the same at 1280×720 with `-MobileTier low`. Reports:
`TestResults/OfflinePlayer-smoothness-1280x720`, `-1920x1080`, and `TestResults/OfflinePlayer-smoothness-low-1280x720`.

Steady-state pacing on this i5-10400F/RTX 3060 Ti is excellent regardless of viewport or tier: **zero hitch frames
at 1280×720** on both desktop and low tier, stddev around 1 ms, p99.9 under 12 ms. The worst sampled frames at
1280×720 are dominated by `RenderAndReadback`/`Presentation` in roughly equal measure (11–13 ms, e.g. the desktop's
worst frame: 13.12 ms with 5.10 ms presentation, 5.73 ms render, 0.92 ms world tick, 1.36 ms unaccounted), never by
GC or world tick. At 1920×1080 three frames (of 2,996) cross the hitch threshold at 35.87–39.10 ms, each dominated
by `RenderAndReadback` or `Presentation` (31–37 ms in that one component alone) against a texture-memory footprint
of 283 MB versus 170 MB at 720p; this is a real, reproducible-looking but not yet root-caused signal, not
explained by this pass, and is listed under what remains. The maximum single-frame GC allocation (958,044 bytes)
is identical to the byte in all three runs, which points to one deterministic, specific event in the fixture
rather than random collector noise, also unexplained here.

The **cold first-use proxy** dropped from 447.39 ms on the first run of the session to ~50 ms on the second and
third runs immediately after it. Each run is a fresh player process and a fresh match, so this is not caused by
anything in-process; the likely explanation is the Direct3D driver's on-disk shader cache being cold only for the
very first launch after the new build was written, and already warm for the following two launches in the same
few minutes. Treat 447 ms as closer to what a genuinely first-ever launch on this machine pays, and ~50 ms as a
floor once the driver has compiled these shaders once; a real player's very first match after installing or after
a driver/build update is the closer analogue to the 447 ms figure.

### Scenario (b): heavy fight, via Stress mode (150+ units)

The task allowed Stress mode as a stand-in for a scripted big battle, since it already exists, and it does: 500
synthetic, unarmed `Diagnostic Tender` movers (`MovementScenarioFactory`), not real combat with projectiles,
deaths or army composition, so this measures rendering/movement at scale, not combat CPU cost. Reported alongside
the existing 100-unit case for scale:

| Units | Frame p50/p95/p99/p99.9/max | Frame stddev | Hitches (>threshold) | Tick p50/p95/p99/max | Samples |
| --- | --- | --- | --- | --- | --- |
| 100 | 7.22 / 8.36 / 9.63 / 11.03 / 11.38 ms | 0.55 ms | 0 (>33.0 ms) | – / 0.097 / 0.150 / 1.895 ms | 2,723 |
| 500 | 29.42 / 31.36 / 32.59 / 36.27 / 36.27 ms | 3.08 ms | 0 (>58.8 ms) | – / 0.449 / 0.548 / 2.686 ms | 714 |

Commands: `tools/Profile-MovementPlayer.ps1 -Counts 100 -Scenario OpenField -Width 1280 -Height 720 -Seconds 20`;
the same with `-Counts 500`. Reports: `TestResults/MovementPlayer-OpenField-100-1280x720`,
`TestResults/MovementPlayer-OpenField-500-1280x720`.

This is the sharpest confirmed result in this pass. World tick cost stays trivial at 500 units (p95 0.449 ms — the
simulation is not the problem), but frame time rises to a p50/p95 of 29.4/31.4 ms, past the 60 fps budget and
within a few milliseconds of the 33 ms/30 fps floor, **uniformly**, not as occasional spikes: the hitch counter
correctly reports zero, because its threshold adapts to the (already elevated) median — 500 units is not "hitchy",
it is consistently slow. That gap between a trivial world tick and a heavy frame is rendering/presentation cost
scaling with army size, consistent in direction with (though not isolated down to) the presentation-dimension
scout findings about per-unit, per-frame work (`SiegeLadderVisuals`, `FactionWorldView`, the per-unit cooldown
lookup, `CorsairAnimationDriver`). This fixture cannot separate which of those costs dominates, or how much of the
500-unit cost is genuinely the diagnostic Tenders' simple visuals versus what a real 500-unit army with full Meshy
art/animation would cost; a follow-up with Profiler markers around those specific call sites, on this same
fixture, is the direct next step (listed below).

### Scenario (c): the first real seconds of a fresh match, at the real 60 fps cap

`tools/Smoke-AlphaProduct.ps1 -Width 1280 -Height 720` (report:
`TestResults/AlphaProduct-1280x720-<timestamp>/product-smoke.json`) drives the real menu → skirmish → match flow
through `MatchController`'s ordinary `Application.targetFrameRate = 60`/`vSyncCount = 0` path, not the offline
probe's uncapped one, so this is the only number in this section that shows pacing **under the shipped cap** the
way a player actually gets it.

`FramesPerSecond` 59.96, rolling `FrameP95Milliseconds` 16.68 ms (essentially exactly the 60 fps budget, as
expected under a hard cap), local tick p95 1.93 ms. The whole-session fields
(`SessionFrame*`, 482 samples) tell a sharper story: **`SessionFrameHitchCount` is 1, and that one frame is
259.04 ms** — both `SessionFrameP999Milliseconds` and `SessionFrameMaxMilliseconds`, since with 482 samples the
999th‑per‑mille and the maximum are the same sample. A quarter of a second, once, is exactly the kind of thing a
player would notice and remember as "the game froze for a second there."

This product-smoke path fast-forwards the first 2,400 ticks (`MatchController.enabled = false` while a helper AI
and `AdvanceSimulationTick()` are called directly, `SyncPresentation` and rendering happening only once per outer
iteration) precisely so a CI run doesn't wait two real minutes; `Metrics.ObserveFrame` is only ever called from
`MatchController.Update()`, so **none of that fast-forward is observed** — `SessionFrameSamples` is 482, almost
exactly 8 real seconds at 60 fps, matching only the `WaitForSecondsRealtime(8)` tail after `match.enabled = true`
is restored. The 259 ms frame therefore lands in the first real seconds after the match becomes interactive and
rendered at ordinary pace — independent confirmation, through the ordinary game loop rather than a special probe,
of the same family of cold first-use cost the offline fixture's `InitialPresentationSyncMilliseconds` measures.
Two different measurement paths now agree that a large, one-off stall exists somewhere around a fresh match's
first real frames; this pass did not instrument which specific unit/building type or shader variant caused the
259 ms frame, only that it exists and roughly when.

### Scenarios not measured this session: legend_lands pan, and low tier on it

`OfflineRenderProbe`'s fixture is deliberately locked to Amber Crossing/Aven/Dominion/tick 0 for reproducibility
(`Run()` rejects any other map/faction/mode outright). Giving it a second, fresh-start, real-time, pannable mode
for `legend_lands` — needed to measure (d) and (e) with the same frame-time percentiles as above — is a genuine
new capability, not a small extension of the existing one, so it was not built in this pass; doing it without
risking the existing fixture's reproducibility deserves its own change and review. What exists today is
non-timed: the triangle/batch/shadow-caster stills already in the ["Tierras de Leyenda"](#lighter-art-for-the-tiers-2026-09-25)
section above, from `-emberfieldSceneryStills <folder> legend_lands verdant ashen [-emberfieldMobileTier <tier>]`.
Recommended follow-up: a `-emberfieldStartupProfile <folder> [-emberfieldMatchMap legend_lands] [-emberfieldMatchFaction verdant]`
mode that skips the 6,000-tick prewarm, drives a fresh match in real time for ~60 s with the same hidden-D3D11
render-and-readback technique, and optionally pans the camera, reusing the `Samples`/hitch/worst-frame machinery
added in this pass.

### Confirmed/refuted, in order of how strong the evidence is

1. **Confirmed, large: a cold first-use stall exists and is severe.** Two independent measurements agree: the
   offline fixture's untimed post-prewarm sync cost 447 ms on a cold shader cache (~50 ms once warm), and the
   product smoke's ordinary game loop hit a 259 ms frame in the first real seconds of a fresh match. This is the
   single most "unfluid" moment measured in this pass and the strongest confirmation of the hitches-dimension
   scouting finding about `WorldView`'s lazy `Resources.Load`/`Instantiate`/shader-variant compilation on first
   use. Neither measurement isolates which specific type or variant dominates.
2. **Confirmed, large: rendering/presentation cost, not simulation cost, is what a big army spends.** 500 units:
   world tick p95 0.449 ms, frame p95 31.36 ms. This is new, quantified evidence for the general direction of the
   presentation-dimension per-unit/per-frame scouting findings, though this pass does not isolate which of them
   (or of simple draw/skinning cost) accounts for how much.
3. **Refuted at this scale, on this hardware: steady-state play is not full of micro-hitches.** Zero hitch frames
   at 1280×720 on both desktop and low tier across ~3,000-3,150 sampled frames each, stddev ~1 ms, p99.9 under
   12 ms. The problem measured here is concentrated at specific moments (cold start, large army count), not spread
   through ordinary frame-to-frame cost.
4. **Unexplained, flagged for follow-up:** three hitch frames at 1920×1080 only (35.87–39.10 ms, `RenderAndReadback`/
   `Presentation`-dominated) against a larger texture-memory footprint; and an identical 958,044-byte maximum
   single-frame GC allocation across all three 1280×720/1920×1080 runs, which looks deterministic rather than
   noisy. Neither was root-caused here.
5. **Not measurable with this harness, unconfirmed either way:** the pacing-dimension finding that the desktop is
   hard-capped at 60 fps/vSync 0 with no refresh-rate awareness. `OfflineRenderProbe` always forces
   `vSyncCount = 0`/`targetFrameRate = -1` to measure uncapped throughput, and the product smoke's 60 fps-capped
   run has no way to see a real display's refresh cadence or frame-pacing jitter; that needs an actual high-refresh
   monitor and a tool like PresentMon, which this harness does not provide.
6. **Not measured this session:** the remaining presentation-dimension findings this pass had no matching scenario
   for (`SiegeLadderVisuals`'/boarding's per-frame linear rescans need an active siege; `FogView`'s unthrottled
   rebuild and `HearthSmoke`'s unconditional per-puff update need a busy/hearth-heavy map; `CorsairAnimationDriver`
   is explicitly off-limits to edit and was not instrumented read-only either); the mouse-wheel zoom stepping
   (cosmetic, explicitly lowest priority in the scouting pass); and every simulation-dimension finding
   (`MovementSystem.ReplanAll` cascades on structure death, `FactionSystem`'s 4× redundant refresh,
   `CombatSystem.UpdateDefenses`'s unthrottled per-building unit scan, `SiegeSystem`'s wall-destruction scans,
   `InteractionSearch`'s exhaustive drop-off search, `Navigation`'s per-call list allocation), since those need
   scripted siege/economy/AI-burst scenarios this pass did not build, and — being under `Assets/Game/Simulation` —
   would need the state-hash determinism proof before any change, not just a measurement.

### Exact commands to re-run each scenario

```
tools/Profile-OfflinePlayer.ps1 -Label smoothness -Width 1280 -Height 720
tools/Profile-OfflinePlayer.ps1 -Label smoothness -Width 1920 -Height 1080
tools/Profile-OfflinePlayer.ps1 -Label smoothness-low -Width 1280 -Height 720 -MobileTier low
tools/Profile-MovementPlayer.ps1 -Counts 100 -Scenario OpenField -Width 1280 -Height 720 -Seconds 20
tools/Profile-MovementPlayer.ps1 -Counts 500 -Scenario OpenField -Width 1280 -Height 720 -Seconds 20
tools/Smoke-AlphaProduct.ps1 -Width 1280 -Height 720
```

All six ran against the same build (GUID `779be87f7b1649dbbebd8ecb8f8f923b`), built with
`tools/Build-Unity.ps1 -Target Windows -ProjectPath D:\EmberfieldWorkingCache\AoE\MobileTierBuildProject`, on the
same i5-10400F/RTX 3060 Ti desktop, back to back in one session (run-to-run noise on this machine is documented
elsewhere in this file at ±0.5 ms for frame p50/p95; the tail statistics added here have not had their own
noise floor established — treat single-run p99.9/max/hitch counts as indicative until repeated).

### Final result, after the whole pass (2026-09-26 review)

The same four commands, once each, against a fresh build (GUID `6196bf848779440cbbf5e924635597d4`, same
`MobileTierBuildProject` shell, same desktop), after every commit of the pass (baseline recording, frame pacing/
prewarm, per-frame presentation CPU/GC, the mass-order simulation shortcut, and the siege film polish). Single runs,
not interleaved pairs, so treat this table the same as the baseline's own tail statistics — indicative, not a noise
floor.

| Scenario | Metric | Baseline (`779be87f`) | Final (`6196bf84`) |
| --- | --- | --- | --- |
| (a) Fixture, 1280×720 desktop | Frame p50/p95/p99/p99.9/max | 6.34 / 8.79 / 9.95 / 11.81 / 13.12 ms | 5.15 / 6.37 / 7.07 / 7.97 / 8.35 ms |
| (a) Fixture, 1280×720 desktop | Frame stddev / hitches | 1.02 ms / 0 | 0.59 ms / 0 |
| (a) Fixture, 1280×720 desktop | World tick p95/p99/max | 1.723 / 2.118 / 3.868 ms | 0.495 / 0.558 / 0.606 ms |
| (a) Fixture, 1280×720 desktop | GC alloc/frame median/p95/max | 9,544 / 21,746 / 958,044 B | 7,944 / 9,812 / 88,988 B |
| (a) Fixture, 1280×720 desktop | Cold first-use proxy | 447.39 ms (cold) / ~50 ms (warm) | 34.93 ms |
| Low tier, 1280×720 | Frame p50/p95/p99/p99.9/max | 6.06 / 8.45 / 9.61 / 11.34 / 12.51 ms | 4.53 / 6.14 / 6.91 / 7.94 / 9.43 ms |
| Low tier, 1280×720 | Frame stddev / hitches | 1.01 ms / 0 | 0.78 ms / 0 |
| Low tier, 1280×720 | World tick p95/p99/max | 1.721 / 2.196 / 2.669 ms | 0.501 / 0.574 / 0.662 ms |
| Low tier, 1280×720 | GC alloc/frame median/p95/max | 9,544 / 21,126 / 958,044 B | 7,768 / 9,762 / 89,120 B |
| Low tier, 1280×720 | Cold first-use proxy | 51.14 ms | 24.50 ms |
| (b) Heavy fight, 500 units | Frame p50/p95/p99/p99.9/max | 29.42 / 31.36 / 32.59 / 36.27 / 36.27 ms | 29.38 / 31.33 / 33.57 / 39.47 / 39.47 ms |
| (b) Heavy fight, 500 units | Frame stddev / hitches (>58.8 ms) | 3.08 ms / 0 | 3.15 ms / 0 |
| (b) Heavy fight, 500 units | World tick p95/p99/max | 0.449 / 0.548 / 2.686 ms | 0.447 / 0.544 / 2.676 ms |
| (c) First real seconds, product smoke, 60 fps cap | Samples | 482 | 661 |
| (c) First real seconds, product smoke, 60 fps cap | Hitches (>33 ms) | 1 | 0 |
| (c) First real seconds, product smoke, 60 fps cap | Max | 259.04 ms | 17.32 ms |
| (c) First real seconds, product smoke, 60 fps cap | Stddev | 11.03 ms | 0.050 ms |
| (c) First real seconds, product smoke, 60 fps cap | p99 | 17.10 ms | 16.84 ms |

(a) and the low tier improved on every figure: frame tail roughly halved, world tick roughly a third, peak
single-frame GC allocation cut by about 10×, and the cold first-use proxy fell by an order of magnitude (or more,
against the genuinely-cold 447 ms baseline sample). (c) confirms the frame-pacing/prewarm fixes directly: the
259 ms cold-start hitch is gone (0 hitch frames in 661 samples) and stddev fell about 220×. (b) is unchanged within
run-to-run noise, exactly as this pass's own "what remains" notes predict: 500 synthetic movers' frame cost is
dominated by `CorsairAnimationDriver`'s per-unit `Animator.Update` and bone-array allocation, which stayed
untouched (explicitly off-limits to edit) through every commit of this pass — the mass-order commit's simulation
work does not touch presentation/rendering cost, and no other commit targeted this animation-driver-bound scenario.
Reports: `TestResults/OfflinePlayer-smoothness-final-1280x720`, `TestResults/OfflinePlayer-smoothness-final-low-1280x720`,
`TestResults/MovementPlayer-OpenField-500-1280x720-smoothness-final`, `TestResults/AlphaProduct-1280x720-20260926-171627`.

## Smoothness fixes (2026-09-26, frame pacing and hitches)

Acting on the baseline above. Presentation only: nothing under `Assets/Game/Simulation` changed, so every tick's
content is what it was; what moved is which rendered frame runs a tick, and what a frame builds or loads.

### What changed

1. **Frame pacing** (`FramePacing`). A desktop now waits for the display (vSync 1, no software cap) instead of a 60 fps
   timer with vSync off: each frame is shown for exactly one refresh at whatever rate the monitor runs, with no
   tearing. Settings → **LÍMITE DE FPS** cycles *Frecuencia de pantalla* (default), *60 FPS*, *30 FPS* and *Sin límite*
   (`AlphaSettings.FrameRate`). Phones never take the choice: `MobileQuality`'s tier caps pace them as before. Stress
   and the probes still run uncapped. Where the game keeps running out of focus (online sets `runInBackground`) the
   display choice holds 30 fps while unfocused, since a minimised window has no refresh to wait for. Development
   players take `-emberfieldFrameRate display|60|30|unlimited`.
2. **The load stays out of the first playable frames** (`MatchController`). The frame a match is made in loads the
   scene and draws it first. Its interval is no longer sampled by `Metrics`, and the tick accumulator no longer
   catches it up as five ticks crowded into the next frame (the 39–44 ms first playable frame the new start window
   exposed). Under vSync that interval reaches `unscaledDeltaTime` two to four frames late, where Unity times frames by
   when they are shown. So a frame counts as loading until the intervals reported since the load cover the wall-clock
   time it took, give or take 0.1 s, and never for more than 30 frames. The loading frame now also runs the match's
   first tick: Mono compiles the rules and the AI makes its opening plan there (35–37 ms measured: `World.Tick` 10.8 ms
   plus about 24 ms of the first think), behind the load instead of in the first frame the player sees move.
3. **Prewarm** (`WorldView.Prewarm`, retired by `DrawnOnce`). While a match loads, every Meshy unit, building and
   siege model and every reference character its players can field is read from disk and recoloured for its owner.
   Each is drawn once, standing 20 m past the ground on the camera's line of sight, where it is inside the view but
   behind the terrain (`-emberfieldCaptureLoadingFrame` saves that frame as `loading-frame.png`; nothing shows).
   A selection ring, an owner ring, health and research bars, a hit marker and each projectile mesh are drawn with
   them, and the projectile pool starts with 32. For Aven against the Ashen Dominion that is 42 models. The load pays
   63–90 ms for it with a warm file cache and 320–1,513 ms on the first launch after a build, which is the same cold
   read that otherwise landed mid-match (below). Not covered: pirate and cosmetic characters
   (`ImportedCharacterVisuals`, which this pass does not edit). Cost: the rest of both rosters stays resident, +55 MB of
   textures and +5 MB of meshes on this desktop (178 → 233 MB); on Android that stays inside the 100 MB art-texture
   budget, which already counts all the art. Development players take `-emberfieldNoPrewarm`.
4. **Cached cue audio.** `SliceFeedback`'s eight synthesised cues are made once per process, as `FrontierAmbience`
   already kept its beds, instead of once per match.
5. **Interpolation that never snaps** (`WorldView.Turn`, `RtsCamera.Ease`). Every unit's facing now eases between
   ticks (540°/s along its path, 1,440°/s toward an attack target). Before, units without an imported model snapped
   to each new heading once per 20 Hz tick, and every unit snapped to face its target. The attack lunge is eased by
   the tick fraction like the position. A mouse-wheel notch now glides the zoom (90% of the way in about an eighth of a
   second at any frame rate) instead of jumping; pinch and voice zoom stay direct. Keyboard and edge panning were
   already scaled by frame time.

### Scenario (c), the product smoke

The smoke now also samples the first 3 real seconds of the match, before its fast-forward, as well as the last 8.
It pairs each slow frame with what its `Update` did (ticks, their cost, `World.Tick` alone, the presentation, the
view's share, visuals built, GC collections) and times each fast-forward frame's cost beyond its 20 ticks. That cost
is where a new unit or building type's first use lands. The capture before the fast-forward gets a frame of its own.
A hidden window has no display refresh to wait on, so `Smoke-AlphaProduct.ps1` runs at the 60 fps cap unless
`-Visible` (the shipped vSync default, in a visible window) or `-ExtraArguments -emberfieldFrameRate …` is given.

| Build / run | Samples | Hitches (>33 ms) | Max | Stddev | p99 |
| --- | --- | --- | --- | --- | --- |
| Baseline `779be87f`, 60 cap | 482 | 1 | 259.04 ms | 11.03 ms | 17.10 ms |
| Instrumented, before the fixes (`1fd8ad58`), 60 cap, 2 runs | 482 / 482 | 1 / 1 | 286.44 / 260.08 ms | 12.27 / 11.08 ms | 17.15 / 17.25 ms |
| Final `69944541`, hidden, 60 cap, first run after the build | 661 | 0 | 20.16 ms | 0.19 ms | 16.90 ms |
| Final, hidden, 60 cap, 2 runs | 660 / 660 | 0 / 0 | 20.86 / 21.48 ms | 0.22 / 0.21 ms | 17.33 / 17.21 ms |
| Final, hidden, 60 cap, no prewarm, 3 runs | 660 each | 0 / 0 / 0 | 17.55 / 17.56 / 20.73 ms | 0.08 / 0.08 / 0.20 ms | 17.16 / 17.15 / 17.11 ms |
| Final, **visible, display sync (default)**, 2 runs | 657 / 658 | 0 / 0 | 16.82 / 16.83 ms | **0.014 / 0.014 ms** | 16.70 / 16.70 ms |
| Final, visible, 60 cap, 2 runs | 660 / 660 | 0 / 0 | 17.46 / 18.94 ms | 0.058 / 0.100 ms | 16.99 / 16.98 ms |

The 259 ms "hitch" of the baseline was the loading frame itself. The pre-fix instrumentation showed that no real frame
of the 8-second window was over 17.3 ms; the 39–44 ms first playable frame that the new 3-second window then exposed
was the catch-up and the first tick, and neither is there now. Under display sync the frame
rate locks to the display (60.01 fps reported against 59.95–59.99 for the timer), the spread of frame times drops by
four to seven times against the 60 cap, and the few 20 ms frames that a 60 cap shows are absorbed by the frames queued
ahead. What display sync costs is at the load: from the Begin click to a steady match took 383/385 ms against 321/323 ms
at the 60 cap, because the loading frame waits to be shown. That wait arrives as one 283 ms interval at the start
window's frame 1, which the smoke marks `Loading` and `Metrics` does not sample.

First use, over the fast-forward's 120 frames of 20 ticks, at the 60 cap (hidden or visible), 30 runs with the
prewarm against 16 without. Without the prewarm, the first run after each of two builds had 3 and 2 frames over 33 ms.
A frame building a new model there cost 87.6 and 70.5 ms, and 57.4 ms on the first run of the instrumented pre-fix
build; warm runs cost 6.8–13.6 ms. With the prewarm no fast-forward frame passed 33 ms in any run. A frame building new
visuals cost 3.0–10.2 ms warm and 6.1–8.6 ms on first runs after a build, with one exception: 17.5 ms on the final
build's first run, whose prewarm took 1,513 ms at load.

### Scenarios (a) and (b)

`Profile-OfflinePlayer.ps1` at 1280×720, interleaved on the same builds. `InitialPresentationSyncMilliseconds`, the
cold first-use proxy, is 49.5–56.8 ms without the prewarm (6 runs) and 26.2–42.3 ms with it (8 runs). Sampled frame
p95 is 8.87–9.99 ms without and 8.89–13.12 ms with it. The two worst prewarmed runs (13.12 and 10.51 ms, with
`RenderAndReadback` spikes early in the window) came in the first A/B pair of the session. Six later prewarmed runs
sat at 8.89–10.13 ms, and a later pair without the prewarm showed tails of the same size (max 22.1 and 29.7 ms). So
this pass does not attribute a steady-state cost to the prewarm, but the probe's tail has more run-to-run noise than
the ±0.5 ms documented for its p50/p95. Stress, 500 units (`Profile-MovementPlayer.ps1`, one run): frame p50/p95
29.69/32.13 ms against 29.42/31.36 ms before, within noise. Stress takes neither the prewarm nor the loading tick.

### What remains

- A few frames in a match's first seconds spend about 16 ms in `SyncPresentation` (view about 10 ms, the HUD about 6).
  They fall at the same ticks from run to run (5, 37), so they come from what happens in the match at those moments.
  At a 60 cap they make a 20–21 ms frame; under display sync the queue absorbs them. Not attributed further.
- Only a 60 Hz monitor was available (the RTX 3060 Ti machine's panel runs 60 Hz, 75 at most). Display sync follows
  any refresh rate by construction, but a 144 Hz run is unmeasured. At 144 Hz the 6.9 ms refresh sits below this
  desktop's p95 frame, so frames would alternate between one and two refreshes unless the panel has variable refresh.
  *60 FPS* in Settings is the fallback.
- Under display sync the smoke's own captures (`ReadPixels`) cost one fast-forward frame of about 63 ms. That is
  harness overhead, not gameplay.
- The prewarm reads the full rosters of the factions in play, not what the first minute will need. That is enough for
  Aven against the Dominion. A mid-match first use of a pirate or cosmetic character still reads from disk.

### Commands

```
tools/Smoke-AlphaProduct.ps1 -Width 1280 -Height 720                         # hidden, 60 cap
tools/Smoke-AlphaProduct.ps1 -Width 1280 -Height 720 -Visible                # shipped default: display sync
tools/Smoke-AlphaProduct.ps1 -Width 1280 -Height 720 -Visible -ExtraArguments -emberfieldFrameRate,60
tools/Smoke-AlphaProduct.ps1 -Width 1280 -Height 720 -ExtraArguments -emberfieldNoPrewarm
tools/Smoke-AlphaProduct.ps1 -Width 1280 -Height 720 -ExtraArguments -emberfieldCaptureLoadingFrame
tools/Profile-OfflinePlayer.ps1 -Label pacing-P -Width 1280 -Height 720
tools/Profile-OfflinePlayer.ps1 -Label pacing-A -Width 1280 -Height 720 -ExtraArguments -emberfieldNoPrewarm
tools/Profile-MovementPlayer.ps1 -Counts 500 -Scenario OpenField -Width 1280 -Height 720 -Seconds 20
```

`-Visible` opens a window. Keep it in front: when it loses focus, the offline match pauses and its frames stop being
sampled.

## Smoothness fixes (2026-09-26, per-frame presentation CPU and GC)

The third pass on the same request: what a frame's presentation costs on the CPU and what it leaves for the garbage
collector. Presentation only, apart from one read-only accessor on the fog of war (below), whose determinism is proven
by the fixture's state hashes. Nothing a desktop shows changed.

### Finding where the allocations come from

`GC.GetAllocatedBytesForCurrentThread` does not work in this Mono (see Phase 4 above), so a development player now
records the Unity profiler with a managed callstack on every allocation. `-emberfieldAllocationCapture <frames>` turns
it on in the offline probe (the first frames of its sampled window), the Stress probe (its first sampled frames) and
the product smoke (its last real-time window), and writes `allocations.raw` beside the report. Then
`tools/Report-Allocations.ps1 -Capture <allocations.raw>` reads it in the editor and writes `allocations.txt`: bytes and
allocations per frame by the first game method on the callstack, by profiler marker and by whole callstack, and the
main thread's markers by self and inclusive time. A capturing run's timings are not comparable, since the profiler slows
every allocation it records. `PresentationMarkers` names the parts of a presentation frame for that table
(`WorldView.Units`, `.Buildings`, `.Resources`, `.Removed`, `.Effects`, `.Fog`, `.Scenery`, `MatchHud.Refresh`,
`MatchHud.Text`, `Minimap.Paint`, `UiLocalization.Apply`). The Stress report now carries presentation and view p50/p95/p99
per sampled frame, and `Profile-MovementPlayer.ps1` takes `-PlayerPath`, `-Label` and `-ExtraArguments` like the
offline script, so two builds can be interleaved.

What allocated per frame before this pass, from 300 frames of the offline fixture (build `666b5560`, the previous pass
plus the capture) and 200 Stress frames at 500 units, which also run the real `Update` loop:

| Source | Fixture | Stress, 500 | Kind |
| --- | --- | --- | --- |
| `CorsairAnimationDriver.HasValidRig` (`SkinnedMeshRenderer.bones`, one new array per unit per frame) | 7,059 B, 29 | 111,443 B, 498 | presentation, file not changed here |
| Navigation route lists and the AI | about 1,700 B, 16 | – | simulation |
| `UiLocalization.Apply`: `FindObjectsByType` for canvases and meshes, and every `TextMesh.text` read back | 945 B, 21 (the probe draws the HUD twice a frame) | 430 B, 12 | presentation |
| `RtsInput.OverUi`: a new `PointerEventData` for each UI hit test | – (the probe runs no input) | 486 B, 2 | presentation |
| The HUD's ten refreshes a second: every label composed again | about 300 B | 446 B (the Stress HUD) | presentation |
| `WorldView.Sync` enumerating the world's read-only lists and the selection through their interfaces | 240 B, 6 | 199 B, 5 | presentation |
| `SyncPresentation`'s selection clean-up lambda | 128 B, 1 | 127 B, 1 | presentation |
| `RtsCamera.Constrain`'s three-argument `Mathf.Min` (a `params` array) | 44 B, 1 | 50 B, 1 | presentation |

In all: 10,581 B and 85 allocations a frame in the fixture, 116,265 B and 584 at 500 units. The fixture still ran 19
collections in its 20-second window.

### What changed

1. **The world view** (`WorldView`, `FactionWorldView`, `ObjectiveView`). The world's lists and the selection are walked
   by index; each visual keeps its owner's faction and its attack cooldown instead of scanning the definitions for them
   every frame; the camera's rotation is read once per sync for every bar that faces it; the boarding wall's height comes
   from a dictionary. The thousands of fillers on blocked cells are shown or hidden only when the fog's revision moves,
   and then only the ones whose explored state changed reach the engine (Amber Crossing has about 2,500; they were
   walked, converted and `SetActive` every frame). `AlphaEnvironment`'s decorations do the same.
2. **Input and camera.** `RtsInput` keeps one pointer for its UI hit tests; `RtsCamera.Constrain` takes two-argument
   minima; `SyncPresentation` keeps one selection predicate.
3. **The HUD** (`MatchHud`, `HudChrome`, `OfflineHud`, `ChallengeHud`, `FactionHud`). A label is written only when its
   source text changes. Writing the same source again put the untranslated text back until `UiLocalization`
   translated it, so every translated label and its canvas were rebuilt twice a refresh for nothing. The stock pills,
   rates, match clock, beacon line, formation and research buttons and the settlement summary are composed again only
   when a number in them changes. The context row's key is written into one reused builder with an allocation-free
   number writer and compared in place; the row's button lambdas no longer capture a method-wide local (that made
   every refresh allocate a closure); the selection helpers return a shared empty array for an empty selection.
   `Invalidate` still writes every label again.
4. **Translation** (`UiLocalization`). The canvases are found again only when the number of UI raycasters changes (a
   canvas made, shown, hidden or destroyed), when a scene loads or unloads, when a known canvas is destroyed, on
   `ApplyNow`, and in a sweep every 60 frames for a canvas without a raycaster. A world `TextMesh` is translated when it
   is written through `UiLocalization.SetText` (the beacon labels and the art review captions), since Unity can only hand
   its text back as a new string; it follows language changes from the source it was given.
5. **Fog and minimap** (`FogView`, `MinimapView`, `FogOfWarSystem.CopyCells`). The fog texture was redrawn with two
   lookups per cell on every vision revision (up to 20 a second), and the minimap asked the fog twice for each of its
   28,600 pixels five times a second. `FogOfWarSystem.CopyCells` hands presentation the local player's sight of the whole
   map in one pass, one byte per cell (bit 1 in sight, bit 0 explored: exactly `IsVisible` and `IsExplored` at each
   cell's centre); the fog draws from it, and the minimap reads the fog's copy through a pixel-to-cell table built once.
   `CopyCells` reads and never writes, and nothing in the simulation calls it.

**Determinism.** The fixture's SHA-256 state hashes at ticks 6,000, 6,060 and 6,460 of the AI-against-AI match
(`A1718019…`, `EDFD5040…`, `2EC6E7DB…`) are identical in every run of the builds before and after this pass, as they
were in the baseline. `FogOracleTests` now also checks `CopyCells` against `IsVisible` and `IsExplored` for every cell,
for both players, on every tick of its four scripted scenarios, plus its refusals (an unknown player, a short array).

### After

Offline fixture (`Profile-OfflinePlayer.ps1`, 1280×720), three interleaved pairs, before (`666b5560`) against after
(`bd513e91`):

| | Frame p50 / p95 / p99 | `SyncPresentation` p50 / p95 / p99 | World tick p95 | GC per frame median / p95 | Samples |
| --- | --- | --- | --- | --- | --- |
| Before | 6.45–6.61 / 8.87–9.43 / 10.02–11.11 ms | 1.551–1.564 / 3.082–3.168 / 3.438–3.775 ms | 1.72–1.76 ms | 9,544 / 22,132–23,426 B | 2,879–2,980 |
| After | 5.23–5.32 / 6.75–7.02 / 7.52–8.32 ms | 0.863–0.890 / 1.251–1.303 / 1.470–1.712 ms | 1.68–1.74 ms | 7,944 / 10,646–10,714 B | 3,714–3,759 |

`SyncPresentation` p95 fell by 59% and its p99 by 55%: the periodic fog, minimap and filler work was most of its tail.
The window still ran 19 collections: what is left to collect is the animation driver's and the simulation's.
The fixture's stills (with the HUD, and the close shot with a beacon label) differ from before by 51 and 20 pixels out of
921,600, against 35 between two runs of the same build: the animation phase at the sampled instant.

Captured again on the final build, the fixture allocates 8,711 B and 49 allocations a frame (median 6,912 B), of which
6,832 B are the animation driver's bone arrays, about 1,500 B the simulation and AI, and 120 B the probe itself. What
remains in presentation code is composed only when a displayed value changes: the stock pills, the clock, the beacon
line and new text passing through the translator, about 100 B a frame on average in the fixture. In the product smoke's
real 60 fps window the presentation code outside the driver comes to about 60 B a frame the same way.

Stress, 500 units, four interleaved pairs (two against an intermediate build, two against the final one):
`SyncPresentation` p50 11.41–11.59 ms before and 11.25–11.80 ms after, p95 12.45–15.98 against 12.53–13.48 ms and p99
14.88–18.84 against 14.74–16.63 ms. The p99 was lower in all four pairs, but the median did not move: at this size the
frame belongs to the animation driver. Its allocations are 111 KB of the 115 KB a frame, and each unit's manual
`Animator.Update` costs 9.3 ms of about 11.3 ms of `WorldView.Units` (captured build, where the capture's own callstack
collection inflates the rest).

Product smoke, hidden, 60 fps cap: after, three runs, 660 samples each, max 17.31–17.53 ms (one run 23.99 ms, a
frame at tick 4 whose `SyncPresentation` took 0.35 ms, so not presentation), stddev 0.064–0.074 ms (0.384 in that run),
p99 16.98–17.19 ms. Before, two runs: max 19.09 and 17.33 ms. Among each run's eight slowest frames of the first three
real seconds, the costliest `SyncPresentation` was 15.62 ms (tick 5, view 9.90 ms) and 3.28 ms before, and 3.20, 0.76
and 0.77 ms after. The 16 ms presentation frame the previous pass left at tick 5 showed in one of the two runs before
and in none of the three after.

### What remains

- **`CorsairAnimationDriver`** (not changed in this pass). `HasValidRig` runs `GetComponentInChildren` and reads
  `SkinnedMeshRenderer.bones`, which builds a new array, for every animated unit on every frame: about 7 KB a frame in
  a match and 111 KB at 500 units, with a collection about once a second in the fixture. Resolving the renderer and
  the answer once in `Initialize` (or testing `sharedMesh.bindposeCount` instead of `bones.Length`) removes it. Its
  per-unit `animator.Update(delta)` runs the animation pipeline once per unit on the main thread (9.3 ms at 500 units);
  letting the engine update the animators together, with their speed following simulated time, is the change that
  would move a large battle. `HasState("Aim")` also concatenates `"Base Layer." + state` for an idle unit with a target
  and no Aim state.
- **Simulation allocations**: route lists in `Navigation.TryFindPathCore`, `OfflineAi.NearKnownPoint` (60 allocations
  a frame in the product smoke), and a whole `Navigation` built in some ticks (the 676 KB and 958 KB single-frame
  allocations in the smoke and the fixture). They need the state-hash proof like any simulation change.
- `UiLocalization.Apply` still walks every active `Text` each frame (about 0.12 ms in the fixture); a translated text
  that changes costs the translator's allocations once, and a selected unit's or building's details are composed on
  every refresh while it stays selected.
- Siege presentation (`SiegeLadderVisuals.Brace` scanning the building definitions for every building and idle ladder)
  has no fixture to measure it and was left as it was.

### Commands

```
tools/Profile-OfflinePlayer.ps1 -Label <label> -Width 1280 -Height 720 [-PlayerPath <exe>]
tools/Profile-MovementPlayer.ps1 -Counts 500 -Scenario OpenField -Width 1280 -Height 720 -Seconds 20 [-PlayerPath <exe>] [-Label <label>]
tools/Smoke-AlphaProduct.ps1 -Width 1280 -Height 720
tools/Profile-OfflinePlayer.ps1 -Label gccap -Width 1280 -Height 720 -ExtraArguments -emberfieldAllocationCapture,300
tools/Profile-MovementPlayer.ps1 -Counts 500 -Label gccap -ExtraArguments -emberfieldAllocationCapture,120
tools/Smoke-AlphaProduct.ps1 -Width 1280 -Height 720 -ExtraArguments -emberfieldAllocationCapture,300
tools/Report-Allocations.ps1 -Capture <folder>/allocations.raw [-ProjectPath D:\EmberfieldWorkingCache\AoE\MobileTierBuildProject]
```

## Big battles: the animation driver (2026-09-28)

The 500-unit Stress fight above spent its frame in `CorsairAnimationDriver`. Presentation only: nothing under
`Assets/Game/Simulation` changed, and the offline fixture's state hashes are the same in both builds (`A1718019…`,
`EDFD5040…`, `2EC6E7DB…`).

### What changed

1. **The match's own frame leaves the animators to Unity.** Before, each unit's `Sync` ran `Animator.Update(delta)` on
   the main thread, one rig after another, and then set the speed to 0. Unity's own animator update then evaluated every
   animator a second time at speed 0, because the driver keeps `AlwaysAnimate`. Now `MatchController.Update` wraps its
   `SyncPresentation` in `CorsairAnimationDriver.BeginEngineFrame(camera)` / `EndEngineFrame()` (`SyncFrame`).
   - Inside that frame, `Sync` only sets the animator's speed. Unity's animator update comes later in the same frame,
     after every `Update` and before `LateUpdate` and rendering. There it advances the rig by exactly the simulated time
     times the gait's playback rate. It evaluates all the animators together on the worker threads.
   - `LateUpdate` sets the speed back to 0, so a paused match still freezes every unit.
   - Every other caller keeps the immediate evaluation, which is the old code path. That covers the probes, films, smokes
     and tests that render or read bones straight after a sync, and the restore replay.
   - Three cases first play what the engine was left (`ApplyPendingAdvance`), so their order is the old one: a second
     sync in one frame, a death or preview, and the siege ladder posing a climber.
2. **Units off the screen are not animated.** In the match's frame, the driver tests an 8 m box around each unit's
   position against the camera's frustum.
   - If the box is outside, the unit's animator does not advance, and its culling mode becomes `CullCompletely`. Unity then
     skips it while its renderers are unseen too, so a camera test that errs never freezes a unit in view.
   - The time the unit missed, up to 2 s, is played in one step when it comes back into view.
   - Its gait, attack and hit states keep following the simulation the whole time.
3. **No per-frame allocation.** `HasValidRig` read `SkinnedMeshRenderer.bones`, which builds a new array, for every unit
   on every frame. It now keeps a valid answer until the controller changes. State hashes are resolved once.

### After

Stress, 500 units, `Profile-MovementPlayer.ps1 -Counts 500 -Scenario OpenField -Width 1280 -Height 720 -Seconds 20`, run
before, after, before, after on two builds from the same tree and the same `MobileTierBuildProject` shell. The before build
is HEAD `548f66c` plus the re-baked knight and wolf (GUID `237fb6f9…`); the after build adds this change (`73d80cd2…`).
The low tier adds `-ExtraArguments -emberfieldMobileTier,low`.

| Run | Frame p50 / p95 / p99 / max | Stddev | View p50 | Samples |
| --- | --- | --- | --- | --- |
| Desktop, before (3 runs) | 29.56–29.81 / 31.37–31.65 / 32.54–33.04 / 34.97–38.77 ms | 3.03–3.12 ms | 11.15–11.38 ms | 703–710 |
| Desktop, after (3 runs) | 14.14–14.29 / 15.18–16.26 / 16.11–18.74 / 19.00–23.23 ms | 0.61–1.01 ms | 1.56–1.57 ms | 1,379–1,408 |
| Low tier, before (2 runs) | 27.53–27.64 / 29.18–29.37 / 30.11–30.43 / 30.89–36.72 ms | 0.82–0.98 ms | 11.16–11.25 ms | 722–724 |
| Low tier, after (2 runs) | 12.92–12.97 / 15.25–16.38 / 18.03–18.07 / 19.92–20.59 ms | 1.10–1.11 ms | 1.54–1.57 ms | 1,508–1,516 |

That is about 34 to 70 fps on the desktop and 36 to 77 fps on the low tier. No run had a hitch.

The allocation capture (`-emberfieldAllocationCapture,120`, 121 frames each, same builds) shows where the time went. The
main thread's profiler markers give each figure's inclusive time per frame:

| | Before | After |
| --- | --- | --- |
| GC allocated per frame, mean | 114,581 B (111,074 B from `HasValidRig`) | 1,578 B (none from the driver) |
| `WorldView.Units` | 14.68 ms | 1.64 ms |
| `Animators.Update` (Unity's own pass) | 9.32 ms | 2.55 ms |
| `Semaphore.WaitForSignal` (waiting on the render thread) | 12.76 ms | 6.14 ms |

What is left of the frame is mostly rendering 500 skinned units.

The offline fixture (Amber Crossing, one pair) takes the immediate path, so its frame barely moved: p50/p95 went from
5.19/6.42 ms to 4.96/6.24 ms. Its GC median went from 7,954 B to 224 B a frame, and its collections in the 20-second window
from 16 to 2.

Nothing a player sees changed:
- `AnimationDriverTests.TheEngineFramePosesTheRigAsTheImmediateSyncDid` walks two knights through 12 frames of a 5 m/s
  march, one on each path. Every bone agrees within 0.05°, and the state and its normalized time agree within 0.001.
- `ARigOffTheScreenWaitsAndCatchesUpWhenSeen` checks that a rig behind the camera stops. Back in view, it is in the same
  state and phase as a rig that never left.
- The product smoke's match-start still at play zoom, before against after, differs in 1 pixel of 921,600 (by more than
  24 of 255).
- The settlement and Stress stills differ only where units stand at different moments of capture: the two settlement
  clocks read 2:11 and 2:10.
- Sheet: `TestResults/AnimationDriver-20260928/visual_identity.jpg`.

### Gaits

Each rig still switches gait at its own clip speeds, but three things changed:
- **The switch speed.** It was the geometric mean of the Walk and Run clip speeds, `sqrt(W·R)`. It is now
  `min(max(sqrt(W·R), 0.6·R), 2.5·W)`.
- **Hysteresis.** A running rig walks again only below `max(0.85 × switch, 0.35·R)`.
- **Playback ceilings.** The walk may play up to 2.5 times its authored speed (was 1.6) and the run up to 2 times (was 1.6).

For a biped (R/W under 2.78) the switch stays where it was. For a mount, the donors' walks are authored at a fifth of their
gallop (0.35–0.61 m/s), so the switch moves up a little. For example, the knight's goes from 1.20 to 1.32 m/s and the
wolf's from 1.13 to 1.36 m/s. Across the whole walk the hooves now stay planted: before, the walk skated by up to 30%
between 1.6·W and the switch.

At a mount's own 5 m/s the gallop skates less:
- the knight, 13% before, now not at all;
- the wolf, 25% before, now 6%;
- the dwarf's ram, 36% before, now 20%;
- the pony, 0.6% before, now not at all.

The simulation moves every unit at its own top speed. `MovementSystem.Advance` has no formation pace and no
acceleration. So a mount walks only when the simulation holds it below about 1.3 m/s: in a crowd or a jam, arriving, or
boarding. At its own pace it gallops. A mount could not walk at the infantry's 3.2–3.6 m/s without playing its walk
six times over or skating.

### What remains

- **Far units.** A far or small unit still on the screen is updated every frame. The engine offers no rate between
  "every frame" and "skipped", except switching its `Animator` off and on, which was not tried. After this change,
  Unity's animator pass is 2.6 ms of the 500-unit frame.
- **Shared poses.** Identical units in lockstep are not sharing a pose.
- **Off-screen saving.** The Stress fixture frames its whole army, so it does not measure what skipping off-screen units
  saves. A battle at play zoom, with most of the army off the screen, has no timed fixture yet.

### Commands

```
tools/Profile-MovementPlayer.ps1 -Counts 500 -Scenario OpenField -Width 1280 -Height 720 -Seconds 20 -PlayerPath <exe> -Label <label>
tools/Profile-MovementPlayer.ps1 ... -ExtraArguments -emberfieldMobileTier,low
tools/Profile-MovementPlayer.ps1 -Counts 500 -Label <label> -PlayerPath <exe> -ExtraArguments -emberfieldAllocationCapture,120
tools/Report-Allocations.ps1 -Capture <folder>/allocations.raw
tools/Profile-OfflinePlayer.ps1 -Label <label> -Width 1280 -Height 720 -PlayerPath <exe>
```

After a build from the D: shell has run next to a compile of the main project, a player may log "The referenced script
(Unknown) on this Behaviour is missing" and stop at its first match. Deleting `Library/Bee` and building again fixed it
here. `Build-Unity.ps1` also left the shell's `TypeDb-All.json` in the shared Library, as the other build scripts once
did. It now removes that file when it finishes (`Remove-ForeignEditorTypeDb`, `tools/Unity-Batch.ps1`).

## Historical Phase 6 verification and measurement limits

Phase 6 added 73 pure faction cases and six PlayMode tests, reaching 290/32 passing tests. Its neutral CPU matrix retained every earlier arrival outcome: OpenField/WideCorridor/CrossingGroups/DynamicObstacle complete at 50–500 movers, while NarrowChoke 200/300/500 reach only 31/52/37 by 120 seconds. All 30 cases recorded zero observed overlaps/static/map violations. The [JSON](../testing/evidence/phase6-movement.json) records source SHA256 `CEB28A978BF89C869B884BF60C3B2427CDFF91FEA78D752A993166965B66460A`, timings and unsupported allocation fields. This historical matrix is a neutral navigation workload, with no active faction influence or research completion. Phase 6 supplied no rendered FPS, faction-renderer, allocation or mobile-device profiling evidence.

The final [Windows build](../testing/evidence/phase6-build.txt), GUID `3a4d191876ae44fba8e0e681bfbdeaa0`, succeeded with zero errors/warnings, 163,206,372 bytes and a 13.7569182-second build duration. The 32 PlayMode tests were rerun after final description-layout and opt-in smoke movement corrections; core rules remained unchanged after the complete gate. All nine packaged checks passed: both factions, technology and combat at 1280×720/1440×1080, plus economy at 1440×1080. Aven runs reached tick 4,035 over 44 yielded frames with nine visible synthetic button dispatches; Serevin reached tick 3,844 over 43 frames with twelve. Those totals include two chooser dispatches checking that explicit choice overrides CLI configuration. Physical gathering, paid mechanics and unique research passed; Serevin retained 439/450 health through two relocations. These are accelerated functional runs on an RTX 3060 Ti, not wall-clock match durations, rendered-throughput measurements or physical touch sessions. The [test strategy](../testing/TEST_STRATEGY.md) links the individual reports.

The eight shipped-JSON faction counter cases preserve authored stats and faction passives, with no research or activated abilities. Mirrored Reedguards beat Ashrunners in 5.40 s with 64 health; Ashrunners beat Stringwardens in 6.95 s with 25 health. Two disadvantaged units reverse each result. The Ashrunner costs 100 nominal resources versus 80 for either common unit; these functional counter checks neither prove competitive balance nor measure CPU/rendering performance.

## Historical Phase 5 verification

Phase 5 added 57 EditMode cases and three PlayMode tests. The 26-test PlayMode suite passed again after research viewport-width and opaque-panel corrections. The final [Windows build](../testing/evidence/phase5-build.txt), GUID `48c0b4723fb54362a3697c9027469896`, succeeded with zero errors/warnings. The later opt-in smoke-only camera focus adjustment was checked in that build and player evidence.

The movement rerun retained all Phase 4 arrival outcomes and zero observed overlaps/static/map violations across 30 cases. NarrowChoke 200/300/500 still reached only 31/52/37 movers by 120 seconds: persistent congestion is unresolved. This matrix isolates navigation with no research workload. It therefore does not measure completion-time rebuilding of technology stat caches. Allocation measurement remains unavailable; no new allocation or research-completion performance claim is supported.

The [1280 by 720](../testing/evidence/phase5-technology-1280x720.txt) and [1440 by 1080](../testing/evidence/phase5-technology-1440x1080.txt) technology smokes reached Empire with all nine technologies at tick 16,338. Each used nine visible, raycast-checked synthetic button dispatches and programmatic scrolling, paid from unmodified starting stock plus physically gathered resources. They use accelerated 100-tick wait-loop batches, not wall-clock play. Their inspected renders establish functional presentation only; there is no Phase 5 rendered FPS, physical touch or mobile-performance result.

## Historical Phase 4 CPU results and congestion limits

The [Phase 4 final CPU matrix](../testing/evidence/phase4-final.md), captured on 9 September 2026 using Unity 6000.3.23f1 and an Intel i5-10400F, completed all required gates. All OpenField, WideCorridor, CrossingGroups and DynamicObstacle cases from 50 through 500 movers arrived fully. Every one of the 30 cases reported zero observed overlaps/static/map violations. These are historical instrumented editor measurements with one repetition per case, not rendered FPS.

| Final case | Order batch | Active tick p95 | Active tick maximum | Arrival |
| --- | --- | --- | --- | --- |
| OpenField 100 | 7.788 ms | 0.059 ms | 0.145 ms | 100/100 in 27.45 s |
| WideCorridor 100 | 1.753 ms | 0.080 ms | 1.374 ms | 100/100 in 28.10 s |
| CrossingGroups 100 | 2.104 ms | 0.177 ms | 0.357 ms | 100/100 in 32.20 s |
| NarrowChoke 100 | 2.754 ms | 0.428 ms | 0.649 ms | 100/100 in 66.80 s |
| NarrowChoke 500 | 45.890 ms | 2.863 ms | 5.025 ms | 37/500 at 120 s |

For NarrowChoke 500, active tick p95 fell from 20.286 ms before the visibility changes to 2.863 ms in the final run; maximum tick time fell from 35.624 to 5.025 ms. These checkpoint differences include intervening visibility, recovery and formation changes, so they do not isolate one change's causal contribution. The full [final JSON](../testing/evidence/phase4-final.json) retains the source hash, timing distributions and counters.

CPU improvement does not solve the larger choke's behavior: at 120 seconds, NarrowChoke 200/300/500 had only 31/52/37 arrivals. This is unresolved persistent congestion, not merely a demonstrated throughput ceiling. These cases remain outside the required normal-case completion gate and must stay visible in reporting. Their safe positions do not establish convergence. Command spikes such as 45.890 ms also remain relevant to a rendered frame budget even when tick p95 is much lower.

## Implemented controls and remaining work

- Simulation scheduling is centralized. A fixed tick drives movement/economy/construction/combat/faction timers/production/research and offline match state; presentation interpolates independently. Faction influence refreshes before/after movement, after faction completions and after research. Research completes after production. Phase 10 now samples active faction/research/economy workloads and separate AI calls; physical target-device profiling remains required.
- Movement uses a reusable uniform-grid broad phase and at most four cached static distance fields. Connectivity changes synchronously invalidate those fields. Route lists and field storage still allocate.
- Local recovery is limited to four searches per tick, a 25 by 25 sample lattice at half-cell spacing and a 20-tick per-unit cooldown. Chosen straight segments are reused, with at most 12 visibility checks for a new segment. Global commands, construction replanning, connectivity rebuilds and combat searches have separate costs; no universal per-tick search scheduler exists.
- Visuals share materials and use one directional light with real-time shadows; the desktop copy and each phone tier set the shadow map, distance and cascades. Arrow views have a working pool. Phase 10's current-art baseline includes per-frame view/fog/HUD synchronization, draw/SetPass/triangle counters and whole-frame allocations; wider armies and physical-device rendering still need measurement.
- Navigation and visibility improvements follow captured CPU evidence. Additional Jobs/Burst/ECS, hierarchy, pooling or rendering changes require a measured problem and a comparable follow-up run.

Initial art guidance is 3–5k triangles at unit LOD0, 1.5–2.5k at LOD1 and 500–1,000 at LOD2. The current small offline viewport does not validate a future full roster or wider army rendering cost. GPU time, overdraw, draw calls, shadows and shader variants matter more than a single triangle count.

## Phase 4 CPU matrix

`RTSPerformanceStress` and the pure C# fixture factory provide **50, 100, 200, 300 and 500 movers**. The six scenarios are OpenField, WideCorridor, NarrowChoke, CrossingGroups, DynamicObstacle and Unreachable. The dynamic scenario includes one additional builder; report total units and movers separately. Fixtures isolate navigation with unarmed Tenders rather than combining pathfinding and combat workloads.

The API additionally supports Loose, Line and Box through the optional `MoveCommand` formation argument. The comparison matrix uses its default Loose mode. Explicit formation tests cover shape, repeat-order behavior and obstructed fallback separately; a default-mode scale result does not establish throughput for every formation. Loose's nearest-pair scaling avoids repeated fixed-scale expansion. Formation allocation remains a synchronous part of command latency.

`tools/Verify-Movement.ps1` runs the instrumented editor without graphics. A full matrix contains 30 cases, each advancing 120 simulated seconds at 20 Hz, with fresh worlds and separate warm-up. Reports retain source hash, fixture version, UTC timestamp, editor/runtime/CPU/OS, repetitions and duration. `Verify-Unity.ps1` with All or Movement requires the full 30-case Final report in addition to its declared checks. Exact commands and output locations are in [movement stress methodology](../testing/MOVEMENT_STRESS.md).

Measure order-batch latency, scheduled construction latency, per-tick p50/p95/p99/max, full-window versus active-tick distributions, path/attack/connectivity totals and maximum query times, visited cells, field builds/cache hits and recovery requests. The current navigation instrumentation reports totals and maxima, not a per-query percentile histogram. A group request still increments the path count on a shared-field hit; recovery is a subset of that count. Compare times and visited cells alongside query counts.

The HUD labels the instrumented subset **route searches**. It includes BFS/shared-route work, local recovery and connectivity rebuilds. Ordinary per-step clearance, spatial-grid neighbour checks, steering and segment-visibility scans contribute to total `World.Tick` cost outside those query timers. Recovery-query timing does include checks performed inside that query. A small route-search number therefore cannot establish that total movement is cheap; the visibility optimization must be assessed against tick time too.

Tick timing and allocation windows bracket `World.Tick()` only. The every-tick observer, report writing and scheduled construction commands sit outside that window; construction has separate timing and its queries remain in navigation totals. Observer allocations can still create process-level GC pressure. Active-tick samples begin with a moving unit; full-window percentiles also include settled idle time. Neither distribution is rendered FPS.

Managed allocation counters must pass a retained 4,096-byte capability probe. This Unity Mono runtime returned zero for the known allocation in the preserved pre-visibility run; allocation results are therefore **unavailable** and reported as `-1`. The original baseline's zero-allocation fields are invalid and remain preserved with a documented correction. Do not claim zero allocation or a measured memory improvement from them. Native allocations, total heap use, GPU memory, GC pauses, draw calls and batches require separate profiling.

## Historical Phase 4 packaged rendering method and results

`tools/Profile-MovementPlayer.ps1` launches the Windows development player hidden, without batch mode, using D3D11 and uncapped frame settings. The default matrix samples the five OpenField counts at 1280 by 720 output pixels. It requests 20 seconds of frame samples after three seconds of warm-up while simulation advances at ordinary 20 Hz. The probe records actual accepted frame duration and early completion; requested duration alone is insufficient evidence of a full sample.

The initial hidden automatic-camera probe rendered zero camera frames and produced a black target. Its [rejected report](../testing/evidence/phase4-rejected-render-probe.json) is retained with `Passed: false`; its fast loop timings cannot support rendering-performance or FPS claims. The verified replacement disables automatic camera rendering, calls `Canvas.ForceUpdateCanvases()`, submits one explicit URP `StandardRequest` per Unity loop to a persistent texture and reads one pixel into a reused CPU texture to wait for GPU completion. Unity documents the synchronization cost of [`ReadPixels`](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Texture2D.ReadPixels.html).

Unique camera-render callbacks are counted in the same accepted frame window, permitting one final Update-before-render offset. The post-window full capture reads that existing target rather than issuing a separate render request. The script validates actual count, scenario, resolution, non-batch mode, v-sync disabled and target frame rate unrestricted, in addition to process/report success and runtime logs.

Frame deltas include simulation, view/HUD updates, camera following, the observer at 10 Hz, forced canvas updates, explicit render submission and one-pixel readback/GPU synchronization. Tick/navigation samples include warm-up; steady frame samples exclude its first three seconds. Record frame and tick p50/p95/p99 and maximum tick time, sample counts, rendered camera frames and the simulated-time/wall-time ratio. The player gate requires a ratio between 0.95 and 1.05 because the ordinary driver drops catch-up time after long frames. Full screenshot readback and PNG encoding occur after timing; the per-frame completion readback remains included.

Record the actual pipeline asset, render scale, MSAA, quality, graphics API, resolution, hardware, editor/player version and development-build flag. The Phase 4 Mobile URP asset used a render scale of 0.8: 1280 by 720 output corresponded to approximately 1024 by 576 3D rendering before upscale. The shipped asset now renders at scale 1; the phone tiers choose their own scale. Pixel output dimensions are not equivalent to native-resolution 3D work. The camera frames the mover bounds, so image inspection is required to confirm that the intended scene and HUD are visible.

All five OpenField counts passed on the i5-10400F / RTX 3060 Ti development player, with approximately 20 seconds of accepted samples, matching camera-render counts within the declared one-frame offset, simulation/wall ratios within 0.2% of unity and zero observed overlap/invalid positions. Frame p95 was 1.584 / 1.958 / 2.914 / 4.002 / 6.968 ms for 50 / 100 / 200 / 300 / 500 movers. The additional 100-unit 1440 by 1080 desktop viewport passed with 2.148 ms frame p95. The [measurement guide](../testing/MOVEMENT_STRESS.md) links each JSON report and inspected captures. These single-run samples characterize this greybox OpenField workload, not every scenario or future art load.

The historical [Phase 4 measured build](../testing/evidence/phase4-profile-build.txt) GUID is `c025db1be65c48f1bbfcb08ef47692eb`, size 163,028,968 bytes, with zero build errors/warnings. The [Phase 4 distribution build](../testing/evidence/phase4-build.txt) is `15c1bf3ca78343ffbcd39ba40ab38795`, 163,028,983 bytes, completed in 13.5675456 seconds with zero errors/warnings. Its only differences from that measured build were non-stress Formation-label fitting and comments; simulation, stress code and the render path were unchanged between those two Phase 4 artifacts. Phase 4 economy/combat functional smokes passed on the distribution build. These profile timings belong to the recorded Phase 4 measurement build and do not profile later Phase 5/6 artifacts.

These measurements establish **conservatively serialized offscreen throughput** under the recorded desktop conditions, including explicit render submission and GPU synchronization overhead. They do not establish ordinary displayed gameplay FPS, display-present latency, isolated GPU execution time, physical touch use, thermal/battery behavior or mobile-device performance. The captured HUD's instantaneous FPS label must retain this qualifier. Future sustained-device profiling must record thermal state, memory pressure and match workload.

## Acceptance gates

Required Phase 4 matrix coverage includes 100-unit OpenField, WideCorridor, CrossingGroups and Unreachable. The first three must accept their orders and finish within the declared deadline without final stalls/overlaps or persistent pair overlap. Every sampled case must retain static/map validity; Unreachable rejects atomically and remains still. All other count/scenario arrival and congestion outcomes remain visible even when outside the normal-case gate. The final report must disclose constrained or overloaded cases that do not finish.

Zero immobility stalls can hide circling, so pair that metric with arrival fraction and the deadline. The headless geometry observer samples every tick; the packaged observer samples every other tick and is not a complete continuous-collision proof. Independent rule tests check swept circles, obstacle clearance, idempotent commands and economy integration. The active player sample does not replace the full 120-second arrival matrix.

A measured improvement in one CPU hotspot is accepted only with retained safety/progress behavior and a comparable run. Mobile qualification still requires selected target hardware and sustained matches including combat, fog, construction and thermal throttling. Select supported devices and quality tiers from that evidence, then revise the provisional 60/30 FPS targets.
