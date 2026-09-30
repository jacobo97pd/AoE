# Movement CPU measurement: final

Captured UTC: 2026-09-09T20:37:45.4824257Z. Unity 6000.3.23f1; Windows 11  (10.0.26200); Intel(R) Core(TM) i5-10400F CPU @ 2.90GHz.

Fixture: `movement-v1`; simulation+diagnostics source SHA256: `6FAC51FE80789FFACF7082CBCAE67C82282327FC9BF929EA04EB38E465AACEB8`. Pre-optimization algorithm reference: `9d61486`; baseline adds opt-in timing instrumentation only.

Each case uses a fresh 128x96 m map, dense stable IDs, unarmed worker radius300 mm/speed3200 mm/s, and one or two simultaneous group orders. Crossing uses two owners. DynamicObstacle has N movers plus one builder and inserts a 3x9 m footprint at elapsed tick100. No shipped combat asset is changed.

120 simulated seconds/case at 20 Hz; 1 repetition(s); observer every tick. JIT warm-up uses separate worlds. Command latency includes the entire order batch and destination capture. Navigation totals include commands, scheduled construction and ticks, excluding world creation. Tick time/allocations bracket only World.Tick; observation and report allocations are excluded. Active ticks begin with at least one moving unit; full-window quantiles include settled idle ticks. Allocation counts are managed bytes on the current thread, not total heap/native/GPU usage. Timings are instrumented editor CPU measurements, not rendered FPS or mobile-device results.

Allocation capability: Unavailable: GC.GetAllocatedBytesForCurrentThread did not report at least4096 bytes for a retained 4096-byte array (or threw NotSupported/NotImplemented). Allocation fields are -1, never a zero-allocation claim. Probe observed bytes: 0.

Shared-field builds/cache hits and local recovery counts are reported separately below. Group route requests still increment path-query counts even when reusing a field; recovery queries are a subset of the path count. Their timing/visited cells stay in the path bucket. Do not add these counters as separate searches. These supplemental counters were introduced after the preserved baseline capture.

Arrival requires Idle within100 mm of the assigned goal. A stalled mover has made less than50 mm progress for200 ticks. Overlap means more than1 mm circle penetration; a pair persisting20 ticks fails the normal criterion. The independent radius/static-cell/map-edge checks run every tick. Peak overlap remains visible even when transient. Unreachable must reject atomically and remain still; its stall count is expected.

Final required gates: all sampled cases remain inside the map and outside static obstacles; Unreachable rejects/remains still; 100-unit OpenField, WideCorridor and CrossingGroups must all arrive by the deadline, with no final stalls/overlaps and no persistent overlap. Other sizes and constrained scenarios report the same acceptance outcome without silently treating congestion as a success. Baseline records failures without aborting.

| Scenario | Movers / total | Rep | Accepted orders | Command ms | Paths / ms | Active p50 / p95 / p99 / max ms | Tick allocation bytes | Arrived / seconds | Final stalls | Overlap peak / longest ticks / final | Invalid peak | Criterion | Required case |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| OpenField | 50 / 50 | 1 | 1/1 | 1.104 | 50 / 1.001 | 0.027 / 0.045 / 0.049 / 2.690 | -1 | 50/50 / 27.250 | 0 | 0 / 0 / 0 | 0 | True | True |
| OpenField | 100 / 100 | 1 | 1/1 | 7.428 | 100 / 6.960 | 0.054 / 0.089 / 0.102 / 6.279 | -1 | 100/100 / 27.450 | 0 | 0 / 0 / 0 | 0 | True | True |
| OpenField | 200 / 200 | 1 | 1/1 | 6.600 | 200 / 6.283 | 0.111 / 0.164 / 0.182 / 0.289 | -1 | 200/200 / 27.900 | 0 | 0 / 0 / 0 | 0 | True | True |
| OpenField | 300 / 300 | 1 | 1/1 | 27.610 | 300 / 26.410 | 0.170 / 0.251 / 0.381 / 0.657 | -1 | 300/300 / 28.400 | 0 | 0 / 0 / 0 | 0 | True | True |
| OpenField | 500 / 500 | 1 | 1/1 | 33.996 | 500 / 33.227 | 0.258 / 0.387 / 0.454 / 1.126 | -1 | 500/500 / 28.850 | 0 | 0 / 0 / 0 | 0 | True | True |
| WideCorridor | 50 / 50 | 1 | 1/1 | 0.913 | 50 / 0.825 | 0.026 / 0.031 / 0.035 / 0.044 | -1 | 50/50 / 27.250 | 0 | 0 / 0 / 0 | 0 | True | True |
| WideCorridor | 100 / 100 | 1 | 1/1 | 1.793 | 101 / 2.792 | 0.058 / 0.069 / 0.106 / 1.398 | -1 | 100/100 / 28.100 | 0 | 0 / 0 / 0 | 0 | True | True |
| WideCorridor | 200 / 200 | 1 | 1/1 | 4.652 | 223 / 4.683 | 0.130 / 0.181 / 0.210 / 2.208 | -1 | 200/200 / 33.900 | 0 | 0 / 0 / 0 | 0 | True | True |
| WideCorridor | 300 / 300 | 1 | 1/1 | 7.925 | 611 / 12.072 | 0.207 / 0.434 / 0.519 / 3.224 | -1 | 300/300 / 41.150 | 0 | 0 / 0 / 0 | 0 | True | True |
| WideCorridor | 500 / 500 | 1 | 1/1 | 17.819 | 729 / 21.230 | 0.310 / 0.475 / 0.579 / 4.064 | -1 | 500/500 / 45.550 | 0 | 0 / 0 / 0 | 0 | True | True |
| NarrowChoke | 50 / 50 | 1 | 1/1 | 1.101 | 298 / 4.716 | 0.038 / 0.160 / 0.229 / 0.273 | -1 | 50/50 / 44.800 | 0 | 0 / 0 / 0 | 0 | True | True |
| NarrowChoke | 100 / 100 | 1 | 1/1 | 2.804 | 1442 / 20.032 | 0.144 / 0.435 / 0.522 / 1.412 | -1 | 100/100 / 66.800 | 0 | 0 / 0 / 0 | 0 | True | True |
| NarrowChoke | 200 / 200 | 1 | 1/1 | 8.798 | 9321 / 75.037 | 0.997 / 1.155 / 1.465 / 2.384 | -1 | 31/200 / deadline | 0 | 0 / 0 / 0 | 0 | False | True |
| NarrowChoke | 300 / 300 | 1 | 1/1 | 18.158 | 9464 / 78.894 | 1.377 / 1.521 / 1.892 / 9.281 | -1 | 52/300 / deadline | 25 | 0 / 0 / 0 | 0 | False | True |
| NarrowChoke | 500 / 500 | 1 | 1/1 | 46.424 | 9767 / 109.518 | 2.718 / 2.919 / 3.374 / 5.834 | -1 | 37/500 / deadline | 98 | 0 / 0 / 0 | 0 | False | True |
| CrossingGroups | 50 / 50 | 1 | 2/2 | 1.213 | 60 / 1.467 | 0.027 / 0.047 / 0.073 / 0.267 | -1 | 50/50 / 31.050 | 0 | 0 / 0 / 0 | 0 | True | True |
| CrossingGroups | 100 / 100 | 1 | 2/2 | 2.160 | 163 / 3.779 | 0.057 / 0.173 / 0.264 / 0.307 | -1 | 100/100 / 32.200 | 0 | 0 / 0 / 0 | 0 | True | True |
| CrossingGroups | 200 / 200 | 1 | 2/2 | 5.310 | 367 / 8.229 | 0.111 / 0.375 / 0.466 / 0.656 | -1 | 200/200 / 36.450 | 0 | 0 / 0 / 0 | 0 | True | True |
| CrossingGroups | 300 / 300 | 1 | 2/2 | 9.372 | 690 / 16.300 | 0.162 / 0.626 / 0.727 / 0.801 | -1 | 300/300 / 37.250 | 0 | 0 / 0 / 0 | 0 | True | True |
| CrossingGroups | 500 / 500 | 1 | 2/2 | 23.162 | 1463 / 39.235 | 0.276 / 1.493 / 1.647 / 1.794 | -1 | 500/500 / 44.200 | 0 | 0 / 0 / 0 | 0 | True | True |
| DynamicObstacle | 50 / 51 | 1 | 1/1 | 1.006 | 174 / 3.418 | 0.030 / 0.109 / 0.167 / 0.306 | -1 | 50/50 / 32.600 | 0 | 0 / 0 / 0 | 0 | True | True |
| DynamicObstacle | 100 / 101 | 1 | 1/1 | 2.048 | 349 / 6.509 | 0.057 / 0.209 / 0.288 / 0.464 | -1 | 100/100 / 38.450 | 0 | 0 / 0 / 0 | 0 | True | True |
| DynamicObstacle | 200 / 201 | 1 | 1/1 | 6.404 | 677 / 16.128 | 0.114 / 0.356 / 0.425 / 0.755 | -1 | 200/200 / 40.550 | 0 | 0 / 0 / 0 | 0 | True | True |
| DynamicObstacle | 300 / 301 | 1 | 1/1 | 12.271 | 939 / 27.252 | 0.162 / 0.466 / 0.546 / 0.968 | -1 | 300/300 / 43.900 | 0 | 0 / 0 / 0 | 0 | True | True |
| DynamicObstacle | 500 / 501 | 1 | 1/1 | 30.117 | 1507 / 63.628 | 0.269 / 0.658 / 0.729 / 1.400 | -1 | 500/500 / 44.900 | 0 | 0 / 0 / 0 | 0 | True | True |
| Unreachable | 50 / 50 | 1 | 0/1 | 0.025 | 0 / 0.000 | 0.000 / 0.000 / 0.000 / 0.000 | -1 | 0/50 / deadline | 50 | 0 / 0 / 0 | 0 | True | True |
| Unreachable | 100 / 100 | 1 | 0/1 | 0.046 | 0 / 0.000 | 0.000 / 0.000 / 0.000 / 0.000 | -1 | 0/100 / deadline | 100 | 0 / 0 / 0 | 0 | True | True |
| Unreachable | 200 / 200 | 1 | 0/1 | 0.071 | 0 / 0.000 | 0.000 / 0.000 / 0.000 / 0.000 | -1 | 0/200 / deadline | 200 | 0 / 0 / 0 | 0 | True | True |
| Unreachable | 300 / 300 | 1 | 0/1 | 0.100 | 0 / 0.000 | 0.000 / 0.000 / 0.000 / 0.000 | -1 | 0/300 / deadline | 300 | 0 / 0 / 0 | 0 | True | True |
| Unreachable | 500 / 500 | 1 | 0/1 | 0.150 | 0 / 0.000 | 0.000 / 0.000 / 0.000 / 0.000 | -1 | 0/500 / deadline | 500 | 0 / 0 / 0 | 0 | True | True |

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

Run complete: True. Required gates covered: True. Required acceptance passed: True. Runner wall seconds (including observation/warm-up): 20.639.

The adjacent JSON contains all-window quantiles, active sample counts, maximum tick/command/event allocation, complete navigation/connectivity counters and times, individual order rejection reasons, dynamic build outcome, maximum penetration, separate outside/static counts and gate reasons. Single-repetition results are a diagnostic sample, not a claim of stable cross-machine performance.
