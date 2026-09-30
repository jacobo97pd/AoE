# Offline performance comparison

Compare 26 keyed CPU cases: five synthetic sizes plus both factions/modes at early and mid checkpoints, each repeated twice. Rules/map/fixture/runner/method/platform, full windows, observed start/end state INCLUDING both visibility/exploration masks, and ordered command/result traces must match before timing is compared. Fingerprints are observable-state evidence, not full private-state or cross-platform determinism proof. CPU p95 values are per-run quantiles; repetition summaries are means/ranges of two quantiles, never pooled p95. A 5% and non-overlapping-range screen is descriptive, not statistical significance. Navigation has only total/max timing, no p95. GC/heap cover the whole diagnostic loop; heap change is not allocated bytes. No ordinary FPS or mobile performance claim.

Validation: **matched workload**. Performance-budget pass: not evaluated.

Baseline: `C:\Users\jacob\Documents\AoE\TestResults\offline-performance-baseline.json`; optimized: `C:\Users\jacob\Documents\AoE\TestResults\offline-performance-optimized.json`.

Source SHA256: `12E70131B7841F9FEF54937DDFCD66034030C8B3EEF60600B18EE030ECCC7A3A` -> `F2C16585009A5A0FC3E4EB06C2993D795D0406C1CF98A4D224CF73FE905001B5`. All 26 start/end hashes (including fog) and ordered command traces match. Both runs independently repeat all 13 workloads.

| Key: fixture / N / mode / P1 / warmup tick / repetition | World p95 ms | AI think p95 ms | Move command p95 ms | Navigation total ms |
| --- | --- | --- | --- | --- |
| ShippedEarly/0/Conquest/aven/0/1 | 0.528 -> 0.212 (-59.9%) | 0.311 -> 0.165 (-47.0%) | unavailable | 23.115 -> 21.929 (-5.1%) |
| ShippedEarly/0/Conquest/aven/0/2 | 0.448 -> 0.212 (-52.7%) | 0.320 -> 0.190 (-40.5%) | unavailable | 21.790 -> 21.622 (-0.8%) |
| ShippedEarly/0/Conquest/serevin/0/1 | 0.449 -> 0.204 (-54.6%) | 0.317 -> 0.162 (-48.8%) | unavailable | 20.346 -> 19.765 (-2.9%) |
| ShippedEarly/0/Conquest/serevin/0/2 | 0.438 -> 0.205 (-53.1%) | 0.294 -> 0.165 (-43.8%) | unavailable | 20.031 -> 19.693 (-1.7%) |
| ShippedEarly/0/Dominion/aven/0/1 | 0.528 -> 0.215 (-59.2%) | 0.364 -> 0.177 (-51.4%) | unavailable | 22.993 -> 21.986 (-4.4%) |
| ShippedEarly/0/Dominion/aven/0/2 | 0.445 -> 0.226 (-49.3%) | 0.338 -> 0.237 (-29.9%) | unavailable | 21.832 -> 23.357 (7.0%) |
| ShippedEarly/0/Dominion/serevin/0/1 | 0.442 -> 0.208 (-53.0%) | 0.352 -> 0.162 (-53.9%) | unavailable | 20.005 -> 19.746 (-1.3%) |
| ShippedEarly/0/Dominion/serevin/0/2 | 0.470 -> 0.207 (-56.1%) | 0.355 -> 0.174 (-50.9%) | unavailable | 20.970 -> 19.698 (-6.1%) |
| ShippedMid/0/Conquest/aven/6000/1 | 0.910 -> 0.536 (-41.1%) | 0.881 -> 0.517 (-41.3%) | unavailable | 55.762 -> 55.918 (0.3%) |
| ShippedMid/0/Conquest/aven/6000/2 | 0.928 -> 0.557 (-40.0%) | 0.969 -> 0.571 (-41.1%) | unavailable | 58.408 -> 57.087 (-2.3%) |
| ShippedMid/0/Conquest/serevin/6000/1 | 0.956 -> 0.602 (-37.0%) | 0.846 -> 0.647 (-23.5%) | unavailable | 64.523 -> 73.383 (13.7%) |
| ShippedMid/0/Conquest/serevin/6000/2 | 0.937 -> 0.574 (-38.7%) | 0.852 -> 0.513 (-39.8%) | unavailable | 63.892 -> 63.172 (-1.1%) |
| ShippedMid/0/Dominion/aven/6000/1 | 1.062 -> 0.672 (-36.7%) | 0.928 -> 0.561 (-39.6%) | unavailable | 88.559 -> 72.530 (-18.1%) |
| ShippedMid/0/Dominion/aven/6000/2 | 1.019 -> 0.657 (-35.5%) | 0.882 -> 0.649 (-26.5%) | unavailable | 72.357 -> 72.498 (0.2%) |
| ShippedMid/0/Dominion/serevin/6000/1 | 0.771 -> 0.393 (-49.0%) | 0.631 -> 0.339 (-46.3%) | unavailable | 41.581 -> 40.764 (-2.0%) |
| ShippedMid/0/Dominion/serevin/6000/2 | 0.807 -> 0.421 (-47.8%) | 0.671 -> 0.340 (-49.3%) | unavailable | 42.081 -> 45.850 (9.0%) |
| SyntheticMovers/100/Conquest/aven/0/1 | 1.497 -> 0.799 (-46.6%) | 1.009 -> 1.204 (19.4%) | 2.152 -> 2.131 (-1.0%) | 82.662 -> 85.195 (3.1%) |
| SyntheticMovers/100/Conquest/aven/0/2 | 1.529 -> 0.792 (-48.2%) | 1.026 -> 0.645 (-37.1%) | 2.226 -> 1.975 (-11.3%) | 83.257 -> 82.385 (-1.0%) |
| SyntheticMovers/200/Conquest/aven/0/1 | 2.048 -> 1.133 (-44.7%) | 1.209 -> 0.777 (-35.7%) | 6.291 -> 5.887 (-6.4%) | 98.043 -> 98.248 (0.2%) |
| SyntheticMovers/200/Conquest/aven/0/2 | 2.309 -> 1.115 (-51.7%) | 1.320 -> 0.719 (-45.6%) | 6.347 -> 6.050 (-4.7%) | 103.195 -> 97.072 (-5.9%) |
| SyntheticMovers/300/Conquest/aven/0/1 | 2.639 -> 1.484 (-43.8%) | 1.370 -> 0.772 (-43.6%) | 11.676 -> 21.288 (82.3%) | 120.265 -> 130.638 (8.6%) |
| SyntheticMovers/300/Conquest/aven/0/2 | 2.812 -> 1.441 (-48.8%) | 1.365 -> 0.723 (-47.0%) | 12.043 -> 11.390 (-5.4%) | 122.345 -> 117.032 (-4.3%) |
| SyntheticMovers/50/Conquest/aven/0/1 | 1.291 -> 0.640 (-50.4%) | 0.976 -> 1.100 (12.7%) | 1.001 -> 11.502 (1049.1%) | 82.068 -> 109.602 (33.5%) |
| SyntheticMovers/50/Conquest/aven/0/2 | 1.275 -> 0.622 (-51.2%) | 1.102 -> 0.679 (-38.4%) | 1.062 -> 0.936 (-11.8%) | 84.498 -> 80.136 (-5.2%) |
| SyntheticMovers/500/Conquest/aven/0/1 | 3.783 -> 2.133 (-43.6%) | 1.758 -> 0.881 (-49.9%) | 26.975 -> 26.481 (-1.8%) | 177.376 -> 174.375 (-1.7%) |
| SyntheticMovers/500/Conquest/aven/0/2 | 3.765 -> 2.149 (-42.9%) | 1.725 -> 0.901 (-47.8%) | 27.103 -> 26.661 (-1.6%) | 177.165 -> 180.276 (1.8%) |

Positive change means slower/more; negative means faster/less. Synthetic move p95 has only three command samples per repetition. Navigation includes path, attack and connectivity work across World/AI/script windows; no navigation p95 was collected.

| Workload | Mean of two World p95 values, baseline -> optimized ms | World interpretation |
| --- | --- | --- |
| ShippedEarly/0/Conquest/aven/0 | 0.488 -> 0.212 | Consistent reduction in these two repetitions; not a significance test. |
| ShippedEarly/0/Conquest/serevin/0 | 0.444 -> 0.205 | Consistent reduction in these two repetitions; not a significance test. |
| ShippedEarly/0/Dominion/aven/0 | 0.486 -> 0.220 | Consistent reduction in these two repetitions; not a significance test. |
| ShippedEarly/0/Dominion/serevin/0 | 0.456 -> 0.207 | Consistent reduction in these two repetitions; not a significance test. |
| ShippedMid/0/Conquest/aven/6000 | 0.919 -> 0.547 | Consistent reduction in these two repetitions; not a significance test. |
| ShippedMid/0/Conquest/serevin/6000 | 0.946 -> 0.588 | Consistent reduction in these two repetitions; not a significance test. |
| ShippedMid/0/Dominion/aven/6000 | 1.040 -> 0.664 | Consistent reduction in these two repetitions; not a significance test. |
| ShippedMid/0/Dominion/serevin/6000 | 0.789 -> 0.407 | Consistent reduction in these two repetitions; not a significance test. |
| SyntheticMovers/100/Conquest/aven/0 | 1.513 -> 0.795 | Consistent reduction in these two repetitions; not a significance test. |
| SyntheticMovers/200/Conquest/aven/0 | 2.179 -> 1.124 | Consistent reduction in these two repetitions; not a significance test. |
| SyntheticMovers/300/Conquest/aven/0 | 2.725 -> 1.462 | Consistent reduction in these two repetitions; not a significance test. |
| SyntheticMovers/50/Conquest/aven/0 | 1.283 -> 0.631 | Consistent reduction in these two repetitions; not a significance test. |
| SyntheticMovers/500/Conquest/aven/0 | 3.774 -> 2.141 | Consistent reduction in these two repetitions; not a significance test. |

| Case | GC collections 0/1/2 baseline -> optimized | Allocation World / AI / script bytes | Heap after baseline -> optimized bytes |
| --- | --- | --- | --- |
| ShippedEarly/0/Conquest/aven/0/1 | 0/0/0 -> 0/0/0 | unavailable (-1), not zero | 571260928 -> 575578112 |
| ShippedEarly/0/Conquest/aven/0/2 | 0/0/0 -> 0/0/0 | unavailable (-1), not zero | 574599168 -> 578215936 |
| ShippedEarly/0/Conquest/serevin/0/1 | 0/0/0 -> 0/0/0 | unavailable (-1), not zero | 548851712 -> 633921536 |
| ShippedEarly/0/Conquest/serevin/0/2 | 0/0/0 -> 0/0/0 | unavailable (-1), not zero | 551825408 -> 637902848 |
| ShippedEarly/0/Dominion/aven/0/1 | 0/0/0 -> 0/0/0 | unavailable (-1), not zero | 599379968 -> 594644992 |
| ShippedEarly/0/Dominion/aven/0/2 | 0/0/0 -> 0/0/0 | unavailable (-1), not zero | 603316224 -> 598048768 |
| ShippedEarly/0/Dominion/serevin/0/1 | 0/0/0 -> 0/0/0 | unavailable (-1), not zero | 568856576 -> 653869056 |
| ShippedEarly/0/Dominion/serevin/0/2 | 0/0/0 -> 0/0/0 | unavailable (-1), not zero | 572297216 -> 657944576 |
| ShippedMid/0/Conquest/aven/6000/1 | 0/0/0 -> 0/0/0 | unavailable (-1), not zero | 598765568 -> 598835200 |
| ShippedMid/0/Conquest/aven/6000/2 | 1/1/1 -> 0/0/0 | unavailable (-1), not zero | 546107392 -> 629891072 |
| ShippedMid/0/Conquest/serevin/6000/1 | 0/0/0 -> 1/1/1 | unavailable (-1), not zero | 571944960 -> 573968384 |
| ShippedMid/0/Conquest/serevin/6000/2 | 0/0/0 -> 0/0/0 | unavailable (-1), not zero | 595345408 -> 591147008 |
| ShippedMid/0/Dominion/aven/6000/1 | 1/1/1 -> 0/0/0 | unavailable (-1), not zero | 547049472 -> 623484928 |
| ShippedMid/0/Dominion/aven/6000/2 | 0/0/0 -> 0/0/0 | unavailable (-1), not zero | 565354496 -> 649719808 |
| ShippedMid/0/Dominion/serevin/6000/1 | 0/0/0 -> 0/0/0 | unavailable (-1), not zero | 586502144 -> 581664768 |
| ShippedMid/0/Dominion/serevin/6000/2 | 0/0/0 -> 0/0/0 | unavailable (-1), not zero | 603992064 -> 596250624 |
| SyntheticMovers/100/Conquest/aven/0/1 | 0/0/0 -> 1/1/1 | unavailable (-1), not zero | 566292480 -> 577818624 |
| SyntheticMovers/100/Conquest/aven/0/2 | 0/0/0 -> 0/0/0 | unavailable (-1), not zero | 588582912 -> 599908352 |
| SyntheticMovers/200/Conquest/aven/0/1 | 0/0/0 -> 0/0/0 | unavailable (-1), not zero | 612184064 -> 623534080 |
| SyntheticMovers/200/Conquest/aven/0/2 | 1/1/1 -> 0/0/0 | unavailable (-1), not zero | 548401152 -> 647639040 |
| SyntheticMovers/300/Conquest/aven/0/1 | 0/0/0 -> 1/1/1 | unavailable (-1), not zero | 572547072 -> 578027520 |
| SyntheticMovers/300/Conquest/aven/0/2 | 0/0/0 -> 0/0/0 | unavailable (-1), not zero | 598470656 -> 602865664 |
| SyntheticMovers/50/Conquest/aven/0/1 | 0/0/0 -> 1/1/1 | unavailable (-1), not zero | 584605696 -> 602279936 |
| SyntheticMovers/50/Conquest/aven/0/2 | 1/1/1 -> 0/0/0 | unavailable (-1), not zero | 544919552 -> 624906240 |
| SyntheticMovers/500/Conquest/aven/0/1 | 0/0/0 -> 0/0/0 | unavailable (-1), not zero | 628285440 -> 632733696 |
| SyntheticMovers/500/Conquest/aven/0/2 | 0/0/0 -> 1/1/1 | unavailable (-1), not zero | 567377920 -> 572465152 |

Heap values include runtime state, caches and diagnostics, with no forced collection; compare allocations only when both capability probes passed. JSON includes per-repetition query counters, GC/heap values and repetition ranges for World, AI, move commands and navigation.

## Rendered pair

One before/after rendered pair. Descriptive serialized URP/Canvas/readback throughput only; no significance, ordinary display FPS, isolated-camera GPU, mobile or performance-budget pass. Frame counts vary with throughput, but the exact400-tick simulation workload and camera/state hashes match. Frame-level profiler counters and delayed GPU telemetry are advisory.

Builds: `a39b23a98990479a8ff2e3d99692e50e` -> `d1b7b1cbc28740e994945476895e1295`. Output 1280x720, URP render scale 0.800000011920929.

| Metric | p50 ms | p95 ms |
| --- | --- | --- |
| FrameMilliseconds | 0.918 -> 1.116 (21.6%) | 1.849 -> 2.236 (20.9%) |
| WorldTickMilliseconds | 0.872 -> 0.332 (-62.0%) | 1.307 -> 0.739 (-43.5%) |
| AiPairPerTickMilliseconds | 0.000 -> 0.000 (-25.0%) | 0.341 -> 0.200 (-41.5%) |
| PresentationMilliseconds | 0.122 -> 0.119 (-2.3%) | 0.221 -> 0.235 (6.1%) |
| RenderAndReadbackMilliseconds | 0.684 -> 0.854 (24.8%) | 1.207 -> 1.624 (34.6%) |

Delayed platform GPU p95: unavailable.

| Completed-frame recorder | Units | Mean before -> after | p95 before -> after |
| --- | --- | --- | --- |
| Draw Calls Count | Count | 80.493 -> 79.848 (-0.8%) | 91.000 -> 90.000 (-1.1%) |
| SetPass Calls Count | Count | 9.000 -> 9.000 (0.0%) | 9.000 -> 9.000 (0.0%) |
| Triangles Count | Count | 14241.937 -> 14217.162 (-0.2%) | 14968.000 -> 14940.000 (-0.2%) |
| GC Allocated In Frame | Bytes | 4447.248 -> 663.515 (-85.1%) | 4614.000 -> 516.000 (-88.8%) |
| Main Thread | TimeNanoseconds | 1038994.826 -> 1281882.427 (23.4%) | 1821900.000 -> 2205400.000 (21.0%) |
| GPU Frame Time | TimeNanoseconds | unavailable | unavailable |
