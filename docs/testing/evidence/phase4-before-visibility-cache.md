# Movement CPU measurement: final

Captured UTC: 2026-09-09T19:37:03.1191321Z. Unity 6000.3.23f1; Windows 11  (10.0.26200); Intel(R) Core(TM) i5-10400F CPU @ 2.90GHz.

Fixture: `movement-v1`; simulation+diagnostics source SHA256: `4D01CE0C51E5B3773465143F5C5D7C446B7EC20CABDBD3646F76509B9B907EC4`. Pre-optimization algorithm reference: `9d61486`; baseline adds opt-in timing instrumentation only.

Each case uses a fresh 128x96 m map, dense stable IDs, unarmed worker radius300 mm/speed3200 mm/s, and one or two simultaneous group orders. Crossing uses two owners. DynamicObstacle has N movers plus one builder and inserts a 3x9 m footprint at elapsed tick100. No shipped combat asset is changed.

120 simulated seconds/case at 20 Hz; 1 repetition(s); observer every tick. JIT warm-up uses separate worlds. Command latency includes the entire order batch and destination capture. Navigation totals include commands, scheduled construction and ticks, excluding world creation. Tick time/allocations bracket only World.Tick; observation and report allocations are excluded. Active ticks begin with at least one moving unit; full-window quantiles include settled idle ticks. Allocation counts are managed bytes on the current thread, not total heap/native/GPU usage. Timings are instrumented editor CPU measurements, not rendered FPS or mobile-device results.

Allocation capability: Unavailable: GC.GetAllocatedBytesForCurrentThread did not report at least4096 bytes for a retained 4096-byte array (or threw NotSupported/NotImplemented). Allocation fields are -1, never a zero-allocation claim. Probe observed bytes: 0.

Shared-field builds/cache hits and local recovery counts are reported separately below. Group route requests still increment path-query counts even when reusing a field; recovery queries are a subset of the path count. Their timing/visited cells stay in the path bucket. Do not add these counters as separate searches. These supplemental counters were introduced after the preserved baseline capture.

Arrival requires Idle within100 mm of the assigned goal. A stalled mover has made less than50 mm progress for200 ticks. Overlap means more than1 mm circle penetration; a pair persisting20 ticks fails the normal criterion. The independent radius/static-cell/map-edge checks run every tick. Peak overlap remains visible even when transient. Unreachable must reject atomically and remain still; its stall count is expected.

Final required gates: all sampled cases remain inside the map and outside static obstacles; Unreachable rejects/remains still; 100-unit OpenField, WideCorridor and CrossingGroups must all arrive by the deadline, with no final stalls/overlaps and no persistent overlap. Other sizes and constrained scenarios report the same acceptance outcome without silently treating congestion as a success. Baseline records failures without aborting.

| Scenario | Movers / total | Rep | Accepted orders | Command ms | Paths / ms | Active p50 / p95 / p99 / max ms | Tick allocation bytes | Arrived / seconds | Final stalls | Overlap peak / longest ticks / final | Invalid peak | Criterion | Required case |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| OpenField | 50 / 50 | 1 | 1/1 | 0.901 | 50 / 0.834 | 0.028 / 0.030 / 0.031 / 0.192 | -1 | 50/50 / 26.900 | 0 | 0 / 0 / 0 | 0 | True | True |
| OpenField | 100 / 100 | 1 | 1/1 | 1.647 | 100 / 1.546 | 0.055 / 0.061 / 0.078 / 0.139 | -1 | 100/100 / 27.000 | 0 | 0 / 0 / 0 | 0 | True | True |
| OpenField | 200 / 200 | 1 | 1/1 | 5.375 | 200 / 5.119 | 0.121 / 0.180 / 0.188 / 0.214 | -1 | 200/200 / 27.200 | 0 | 0 / 0 / 0 | 0 | True | True |
| OpenField | 300 / 300 | 1 | 1/1 | 39.068 | 300 / 37.633 | 0.166 / 0.222 / 0.266 / 3.189 | -1 | 300/300 / 27.550 | 0 | 0 / 0 / 0 | 0 | True | True |
| OpenField | 500 / 500 | 1 | 1/1 | 19.943 | 500 / 19.523 | 0.268 / 0.319 / 0.372 / 0.450 | -1 | 500/500 / 27.750 | 0 | 0 / 0 / 0 | 0 | True | True |
| WideCorridor | 50 / 50 | 1 | 1/1 | 0.793 | 50 / 0.738 | 0.028 / 0.030 / 0.034 / 0.046 | -1 | 50/50 / 26.900 | 0 | 0 / 0 / 0 | 0 | True | True |
| WideCorridor | 100 / 100 | 1 | 1/1 | 1.452 | 100 / 1.363 | 0.056 / 0.073 / 0.076 / 0.697 | -1 | 100/100 / 27.000 | 0 | 0 / 0 / 0 | 0 | True | True |
| WideCorridor | 200 / 200 | 1 | 1/1 | 3.405 | 210 / 4.513 | 0.554 / 1.223 / 1.291 / 2.359 | -1 | 200/200 / 28.500 | 0 | 0 / 0 / 0 | 0 | True | True |
| WideCorridor | 300 / 300 | 1 | 1/1 | 6.453 | 803 / 8.166 | 0.537 / 1.856 / 1.980 / 2.415 | -1 | 300/300 / 44.000 | 0 | 0 / 0 / 0 | 0 | True | True |
| WideCorridor | 500 / 500 | 1 | 1/1 | 12.392 | 1247 / 14.130 | 0.109 / 1.905 / 2.230 / 2.990 | -1 | 494/500 / deadline | 0 | 0 / 0 / 0 | 0 | False | True |
| NarrowChoke | 50 / 50 | 1 | 1/1 | 0.918 | 289 / 2.123 | 0.303 / 0.789 / 0.971 / 1.047 | -1 | 50/50 / 41.750 | 0 | 0 / 0 / 0 | 0 | True | True |
| NarrowChoke | 100 / 100 | 1 | 1/1 | 2.069 | 1272 / 7.988 | 1.012 / 2.163 / 2.909 / 5.817 | -1 | 100/100 / 63.850 | 0 | 0 / 0 / 0 | 0 | True | True |
| NarrowChoke | 200 / 200 | 1 | 1/1 | 6.493 | 4127 / 26.455 | 2.629 / 4.499 / 7.724 / 58.401 | -1 | 200/200 / 94.650 | 0 | 0 / 0 / 0 | 0 | True | True |
| NarrowChoke | 300 / 300 | 1 | 1/1 | 13.006 | 9319 / 51.418 | 7.384 / 8.764 / 12.478 / 15.366 | -1 | 147/300 / deadline | 0 | 0 / 0 / 0 | 0 | False | True |
| NarrowChoke | 500 / 500 | 1 | 1/1 | 34.640 | 9625 / 71.040 | 17.570 / 20.286 / 28.660 / 35.625 | -1 | 45/500 / deadline | 55 | 0 / 0 / 0 | 0 | False | True |
| CrossingGroups | 50 / 50 | 1 | 2/2 | 1.071 | 74 / 1.292 | 0.028 / 0.071 / 0.112 / 0.160 | -1 | 50/50 / 31.000 | 0 | 0 / 0 / 0 | 0 | True | True |
| CrossingGroups | 100 / 100 | 1 | 2/2 | 1.795 | 176 / 2.484 | 0.055 / 0.181 / 0.232 / 0.286 | -1 | 100/100 / 35.650 | 0 | 0 / 0 / 0 | 0 | True | True |
| CrossingGroups | 200 / 200 | 1 | 2/2 | 3.785 | 690 / 7.005 | 0.115 / 0.691 / 0.772 / 0.849 | -1 | 200/200 / 39.250 | 0 | 0 / 0 / 0 | 0 | True | True |
| CrossingGroups | 300 / 300 | 1 | 2/2 | 7.830 | 957 / 12.018 | 0.170 / 0.975 / 1.088 / 1.215 | -1 | 300/300 / 39.250 | 0 | 0 / 0 / 0 | 0 | True | True |
| CrossingGroups | 500 / 500 | 1 | 2/2 | 17.800 | 1955 / 26.611 | 0.280 / 1.798 / 1.905 / 2.006 | -1 | 500/500 / 51.300 | 0 | 0 / 0 / 0 | 0 | True | True |
| DynamicObstacle | 50 / 51 | 1 | 1/1 | 0.798 | 197 / 2.487 | 0.032 / 1.748 / 1.934 / 2.016 | -1 | 50/50 / 34.300 | 0 | 0 / 0 / 0 | 0 | True | True |
| DynamicObstacle | 100 / 101 | 1 | 1/1 | 1.503 | 363 / 4.108 | 0.064 / 2.024 / 2.187 / 2.459 | -1 | 100/100 / 36.300 | 0 | 0 / 0 / 0 | 0 | True | True |
| DynamicObstacle | 200 / 201 | 1 | 1/1 | 4.456 | 727 / 10.262 | 0.118 / 2.771 / 2.885 / 3.948 | -1 | 200/200 / 40.100 | 0 | 0 / 0 / 0 | 0 | True | True |
| DynamicObstacle | 300 / 301 | 1 | 1/1 | 8.273 | 1020 / 18.313 | 0.184 / 4.083 / 4.254 / 4.369 | -1 | 300/300 / 41.600 | 0 | 0 / 0 / 0 | 0 | True | True |
| DynamicObstacle | 500 / 501 | 1 | 1/1 | 20.052 | 1663 / 42.681 | 0.299 / 4.364 / 4.567 / 4.963 | -1 | 500/500 / 46.250 | 0 | 0 / 0 / 0 | 0 | True | True |
| Unreachable | 50 / 50 | 1 | 0/1 | 0.023 | 0 / 0.000 | 0.000 / 0.000 / 0.000 / 0.000 | -1 | 0/50 / deadline | 50 | 0 / 0 / 0 | 0 | True | True |
| Unreachable | 100 / 100 | 1 | 0/1 | 0.041 | 0 / 0.000 | 0.000 / 0.000 / 0.000 / 0.000 | -1 | 0/100 / deadline | 100 | 0 / 0 / 0 | 0 | True | True |
| Unreachable | 200 / 200 | 1 | 0/1 | 0.077 | 0 / 0.000 | 0.000 / 0.000 / 0.000 / 0.000 | -1 | 0/200 / deadline | 200 | 0 / 0 / 0 | 0 | True | True |
| Unreachable | 300 / 300 | 1 | 0/1 | 0.096 | 0 / 0.000 | 0.000 / 0.000 / 0.000 / 0.000 | -1 | 0/300 / deadline | 300 | 0 / 0 / 0 | 0 | True | True |
| Unreachable | 500 / 500 | 1 | 0/1 | 0.152 | 0 / 0.000 | 0.000 / 0.000 / 0.000 / 0.000 | -1 | 0/500 / deadline | 500 | 0 / 0 / 0 | 0 | True | True |

| Scenario | Movers | Rep | Shared-field builds | Shared-field cache hits | Local recovery queries | Path visited cells |
| --- | --- | --- | --- | --- | --- | --- |
| OpenField | 50 | 1 | 1 | 49 | 0 | 20998 |
| OpenField | 100 | 1 | 1 | 99 | 0 | 40698 |
| OpenField | 200 | 1 | 1 | 199 | 0 | 153382 |
| OpenField | 300 | 1 | 1 | 299 | 0 | 310252 |
| OpenField | 500 | 1 | 1 | 499 | 0 | 851640 |
| WideCorridor | 50 | 1 | 1 | 49 | 0 | 20729 |
| WideCorridor | 100 | 1 | 1 | 99 | 0 | 38043 |
| WideCorridor | 200 | 1 | 1 | 204 | 5 | 107305 |
| WideCorridor | 300 | 1 | 1 | 335 | 467 | 229444 |
| WideCorridor | 500 | 1 | 1 | 521 | 725 | 477446 |
| NarrowChoke | 50 | 1 | 1 | 71 | 217 | 34799 |
| NarrowChoke | 100 | 1 | 1 | 193 | 1078 | 116086 |
| NarrowChoke | 200 | 1 | 1 | 406 | 3720 | 481215 |
| NarrowChoke | 300 | 1 | 1 | 579 | 8739 | 974784 |
| NarrowChoke | 500 | 1 | 1 | 682 | 8942 | 1957664 |
| CrossingGroups | 50 | 1 | 2 | 52 | 20 | 32272 |
| CrossingGroups | 100 | 1 | 2 | 112 | 62 | 55335 |
| CrossingGroups | 200 | 1 | 2 | 247 | 441 | 133783 |
| CrossingGroups | 300 | 1 | 2 | 362 | 593 | 275663 |
| CrossingGroups | 500 | 1 | 2 | 610 | 1343 | 841547 |
| DynamicObstacle | 50 | 1 | 2 | 115 | 56 | 55071 |
| DynamicObstacle | 100 | 1 | 2 | 223 | 114 | 95369 |
| DynamicObstacle | 200 | 1 | 2 | 426 | 275 | 324274 |
| DynamicObstacle | 300 | 1 | 2 | 638 | 356 | 638936 |
| DynamicObstacle | 500 | 1 | 2 | 1053 | 584 | 1713174 |
| Unreachable | 50 | 1 | 0 | 0 | 0 | 0 |
| Unreachable | 100 | 1 | 0 | 0 | 0 | 0 |
| Unreachable | 200 | 1 | 0 | 0 | 0 | 0 |
| Unreachable | 300 | 1 | 0 | 0 | 0 | 0 |
| Unreachable | 500 | 1 | 0 | 0 | 0 | 0 |

Run complete: True. Required gates covered: True. Required acceptance passed: True. Runner wall seconds (including observation/warm-up): 77.367.

The adjacent JSON contains all-window quantiles, active sample counts, maximum tick/command/event allocation, complete navigation/connectivity counters and times, individual order rejection reasons, dynamic build outcome, maximum penetration, separate outside/static counts and gate reasons. Single-repetition results are a diagnostic sample, not a claim of stable cross-machine performance.
