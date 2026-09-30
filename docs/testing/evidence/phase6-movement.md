# Movement CPU measurement: final

Captured UTC: 2026-09-09T21:33:10.8241723Z. Unity 6000.3.23f1; Windows 11  (10.0.26200); Intel(R) Core(TM) i5-10400F CPU @ 2.90GHz.

Fixture: `movement-v1`; simulation+diagnostics source SHA256: `CEB28A978BF89C869B884BF60C3B2427CDFF91FEA78D752A993166965B66460A`. Pre-optimization algorithm reference: `9d61486`; baseline adds opt-in timing instrumentation only.

Each case uses a fresh 128x96 m map, dense stable IDs, unarmed worker radius300 mm/speed3200 mm/s, and one or two simultaneous group orders. Crossing uses two owners. DynamicObstacle has N movers plus one builder and inserts a 3x9 m footprint at elapsed tick100. No shipped combat asset is changed.

120 simulated seconds/case at 20 Hz; 1 repetition(s); observer every tick. JIT warm-up uses separate worlds. Command latency includes the entire order batch and destination capture. Navigation totals include commands, scheduled construction and ticks, excluding world creation. Tick time/allocations bracket only World.Tick; observation and report allocations are excluded. Active ticks begin with at least one moving unit; full-window quantiles include settled idle ticks. Allocation counts are managed bytes on the current thread, not total heap/native/GPU usage. Timings are instrumented editor CPU measurements, not rendered FPS or mobile-device results.

Allocation capability: Unavailable: GC.GetAllocatedBytesForCurrentThread did not report at least4096 bytes for a retained 4096-byte array (or threw NotSupported/NotImplemented). Allocation fields are -1, never a zero-allocation claim. Probe observed bytes: 0.

Shared-field builds/cache hits and local recovery counts are reported separately below. Group route requests still increment path-query counts even when reusing a field; recovery queries are a subset of the path count. Their timing/visited cells stay in the path bucket. Do not add these counters as separate searches. These supplemental counters were introduced after the preserved baseline capture.

Arrival requires Idle within100 mm of the assigned goal. A stalled mover has made less than50 mm progress for200 ticks. Overlap means more than1 mm circle penetration; a pair persisting20 ticks fails the normal criterion. The independent radius/static-cell/map-edge checks run every tick. Peak overlap remains visible even when transient. Unreachable must reject atomically and remain still; its stall count is expected.

Final required gates: all sampled cases remain inside the map and outside static obstacles; Unreachable rejects/remains still; 100-unit OpenField, WideCorridor and CrossingGroups must all arrive by the deadline, with no final stalls/overlaps and no persistent overlap. Other sizes and constrained scenarios report the same acceptance outcome without silently treating congestion as a success. Baseline records failures without aborting.

| Scenario | Movers / total | Rep | Accepted orders | Command ms | Paths / ms | Active p50 / p95 / p99 / max ms | Tick allocation bytes | Arrived / seconds | Final stalls | Overlap peak / longest ticks / final | Invalid peak | Criterion | Required case |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| OpenField | 50 / 50 | 1 | 1/1 | 1.074 | 50 / 0.976 | 0.026 / 0.031 / 0.047 / 0.179 | -1 | 50/50 / 27.250 | 0 | 0 / 0 / 0 | 0 | True | True |
| OpenField | 100 / 100 | 1 | 1/1 | 2.432 | 100 / 2.268 | 0.051 / 0.060 / 0.062 / 0.092 | -1 | 100/100 / 27.450 | 0 | 0 / 0 / 0 | 0 | True | True |
| OpenField | 200 / 200 | 1 | 1/1 | 25.150 | 200 / 24.343 | 0.107 / 0.160 / 0.198 / 0.373 | -1 | 200/200 / 27.900 | 0 | 0 / 0 / 0 | 0 | True | True |
| OpenField | 300 / 300 | 1 | 1/1 | 31.347 | 300 / 29.996 | 0.161 / 0.206 / 0.252 / 0.607 | -1 | 300/300 / 28.400 | 0 | 0 / 0 / 0 | 0 | True | True |
| OpenField | 500 / 500 | 1 | 1/1 | 30.623 | 500 / 29.919 | 0.253 / 0.374 / 0.544 / 1.713 | -1 | 500/500 / 28.850 | 0 | 0 / 0 / 0 | 0 | True | True |
| WideCorridor | 50 / 50 | 1 | 1/1 | 0.928 | 50 / 0.846 | 0.026 / 0.031 / 0.038 / 0.045 | -1 | 50/50 / 27.250 | 0 | 0 / 0 / 0 | 0 | True | True |
| WideCorridor | 100 / 100 | 1 | 1/1 | 2.194 | 101 / 3.157 | 0.057 / 0.089 / 0.111 / 1.364 | -1 | 100/100 / 28.100 | 0 | 0 / 0 / 0 | 0 | True | True |
| WideCorridor | 200 / 200 | 1 | 1/1 | 5.093 | 223 / 5.001 | 0.135 / 0.191 / 0.256 / 2.234 | -1 | 200/200 / 33.900 | 0 | 0 / 0 / 0 | 0 | True | True |
| WideCorridor | 300 / 300 | 1 | 1/1 | 8.256 | 611 / 12.576 | 0.204 / 0.445 / 0.547 / 3.276 | -1 | 300/300 / 41.150 | 0 | 0 / 0 / 0 | 0 | True | True |
| WideCorridor | 500 / 500 | 1 | 1/1 | 18.516 | 729 / 22.155 | 0.313 / 0.486 / 0.581 / 4.113 | -1 | 500/500 / 45.550 | 0 | 0 / 0 / 0 | 0 | True | True |
| NarrowChoke | 50 / 50 | 1 | 1/1 | 1.116 | 298 / 5.128 | 0.039 / 0.164 / 0.224 / 0.281 | -1 | 50/50 / 44.800 | 0 | 0 / 0 / 0 | 0 | True | True |
| NarrowChoke | 100 / 100 | 1 | 1/1 | 2.789 | 1442 / 20.154 | 0.144 / 0.433 / 0.518 / 0.602 | -1 | 100/100 / 66.800 | 0 | 0 / 0 / 0 | 0 | True | True |
| NarrowChoke | 200 / 200 | 1 | 1/1 | 8.989 | 9321 / 73.673 | 0.989 / 1.112 / 1.205 / 1.525 | -1 | 31/200 / deadline | 0 | 0 / 0 / 0 | 0 | False | True |
| NarrowChoke | 300 / 300 | 1 | 1/1 | 18.549 | 9464 / 80.022 | 1.378 / 1.542 / 1.934 / 3.328 | -1 | 52/300 / deadline | 25 | 0 / 0 / 0 | 0 | False | True |
| NarrowChoke | 500 / 500 | 1 | 1/1 | 46.722 | 9767 / 110.503 | 2.737 / 2.956 / 3.335 / 4.423 | -1 | 37/500 / deadline | 98 | 0 / 0 / 0 | 0 | False | True |
| CrossingGroups | 50 / 50 | 1 | 2/2 | 1.166 | 60 / 1.412 | 0.027 / 0.046 / 0.070 / 0.268 | -1 | 50/50 / 31.050 | 0 | 0 / 0 / 0 | 0 | True | True |
| CrossingGroups | 100 / 100 | 1 | 2/2 | 2.111 | 163 / 3.695 | 0.056 / 0.174 / 0.262 / 0.308 | -1 | 100/100 / 32.200 | 0 | 0 / 0 / 0 | 0 | True | True |
| CrossingGroups | 200 / 200 | 1 | 2/2 | 5.564 | 367 / 9.957 | 0.123 / 0.523 / 0.694 / 0.837 | -1 | 200/200 / 36.450 | 0 | 0 / 0 / 0 | 0 | True | True |
| CrossingGroups | 300 / 300 | 1 | 2/2 | 9.374 | 690 / 16.219 | 0.164 / 0.621 / 0.717 / 0.797 | -1 | 300/300 / 37.250 | 0 | 0 / 0 / 0 | 0 | True | True |
| CrossingGroups | 500 / 500 | 1 | 2/2 | 22.715 | 1463 / 38.271 | 0.276 / 1.478 / 1.637 / 1.749 | -1 | 500/500 / 44.200 | 0 | 0 / 0 / 0 | 0 | True | True |
| DynamicObstacle | 50 / 51 | 1 | 1/1 | 0.933 | 174 / 3.286 | 0.030 / 0.105 / 0.170 / 0.303 | -1 | 50/50 / 32.600 | 0 | 0 / 0 / 0 | 0 | True | True |
| DynamicObstacle | 100 / 101 | 1 | 1/1 | 2.034 | 349 / 6.418 | 0.057 / 0.210 / 0.287 / 0.464 | -1 | 100/100 / 38.450 | 0 | 0 / 0 / 0 | 0 | True | True |
| DynamicObstacle | 200 / 201 | 1 | 1/1 | 7.308 | 677 / 16.720 | 0.113 / 0.354 / 0.422 / 0.751 | -1 | 200/200 / 40.550 | 0 | 0 / 0 / 0 | 0 | True | True |
| DynamicObstacle | 300 / 301 | 1 | 1/1 | 13.012 | 939 / 27.784 | 0.164 / 0.463 / 0.539 / 0.957 | -1 | 300/300 / 43.900 | 0 | 0 / 0 / 0 | 0 | True | True |
| DynamicObstacle | 500 / 501 | 1 | 1/1 | 31.883 | 1507 / 65.489 | 0.270 / 0.658 / 0.729 / 1.431 | -1 | 500/500 / 44.900 | 0 | 0 / 0 / 0 | 0 | True | True |
| Unreachable | 50 / 50 | 1 | 0/1 | 0.027 | 0 / 0.000 | 0.000 / 0.000 / 0.000 / 0.000 | -1 | 0/50 / deadline | 50 | 0 / 0 / 0 | 0 | True | True |
| Unreachable | 100 / 100 | 1 | 0/1 | 0.039 | 0 / 0.000 | 0.000 / 0.000 / 0.000 / 0.000 | -1 | 0/100 / deadline | 100 | 0 / 0 / 0 | 0 | True | True |
| Unreachable | 200 / 200 | 1 | 0/1 | 0.068 | 0 / 0.000 | 0.000 / 0.000 / 0.000 / 0.000 | -1 | 0/200 / deadline | 200 | 0 / 0 / 0 | 0 | True | True |
| Unreachable | 300 / 300 | 1 | 0/1 | 0.090 | 0 / 0.000 | 0.000 / 0.000 / 0.000 / 0.000 | -1 | 0/300 / deadline | 300 | 0 / 0 / 0 | 0 | True | True |
| Unreachable | 500 / 500 | 1 | 0/1 | 0.144 | 0 / 0.000 | 0.000 / 0.000 / 0.000 / 0.000 | -1 | 0/500 / deadline | 500 | 0 / 0 / 0 | 0 | True | True |

| Scenario | Movers | Rep | Shared-field builds | Shared-field cache hits | Local recovery queries | Path visited cells |
| --- | --- | --- | --- | --- | --- | --- |
| OpenField | 50 | 1 | 1 | 49 | 0 | 25946 |
| OpenField | 100 | 1 | 1 | 99 | 0 | 62288 |
| OpenField | 200 | 1 | 1 | 199 | 0 | 238730 |
| OpenField | 300 | 1 | 1 | 299 | 0 | 506151 |
| OpenField | 500 | 1 | 1 | 499 | 0 | 1349294 |
| WideCorridor | 50 | 1 | 1 | 49 | 0 | 24873 |
| WideCorridor | 100 | 1 | 1 | 99 | 1 | 51901 |
| WideCorridor | 200 | 1 | 1 | 205 | 17 | 148261 |
| WideCorridor | 300 | 1 | 1 | 326 | 284 | 321326 |
| WideCorridor | 500 | 1 | 1 | 528 | 200 | 756156 |
| NarrowChoke | 50 | 1 | 1 | 87 | 210 | 59669 |
| NarrowChoke | 100 | 1 | 1 | 257 | 1184 | 256576 |
| NarrowChoke | 200 | 1 | 1 | 686 | 8634 | 1222414 |
| NarrowChoke | 300 | 1 | 1 | 703 | 8760 | 1708644 |
| NarrowChoke | 500 | 1 | 1 | 842 | 8924 | 3386930 |
| CrossingGroups | 50 | 1 | 2 | 51 | 7 | 36624 |
| CrossingGroups | 100 | 1 | 2 | 110 | 51 | 72453 |
| CrossingGroups | 200 | 1 | 2 | 218 | 147 | 203203 |
| CrossingGroups | 300 | 1 | 2 | 346 | 342 | 400876 |
| CrossingGroups | 500 | 1 | 2 | 597 | 864 | 1074988 |
| DynamicObstacle | 50 | 1 | 2 | 111 | 37 | 67082 |
| DynamicObstacle | 100 | 1 | 2 | 224 | 99 | 143354 |
| DynamicObstacle | 200 | 1 | 2 | 435 | 216 | 497027 |
| DynamicObstacle | 300 | 1 | 2 | 637 | 276 | 1015521 |
| DynamicObstacle | 500 | 1 | 2 | 1053 | 428 | 2701092 |
| Unreachable | 50 | 1 | 0 | 0 | 0 | 0 |
| Unreachable | 100 | 1 | 0 | 0 | 0 | 0 |
| Unreachable | 200 | 1 | 0 | 0 | 0 | 0 |
| Unreachable | 300 | 1 | 0 | 0 | 0 | 0 |
| Unreachable | 500 | 1 | 0 | 0 | 0 | 0 |

Run complete: True. Required gates covered: True. Required acceptance passed: True. Runner wall seconds (including observation/warm-up): 20.756.

The adjacent JSON contains all-window quantiles, active sample counts, maximum tick/command/event allocation, complete navigation/connectivity counters and times, individual order rejection reasons, dynamic build outcome, maximum penetration, separate outside/static counts and gate reasons. Single-repetition results are a diagnostic sample, not a claim of stable cross-machine performance.
