# Movement CPU measurement: final

Captured UTC: 2026-09-09T22:57:19.8792373Z. Unity 6000.3.23f1; Windows 11  (10.0.26200); Intel(R) Core(TM) i5-10400F CPU @ 2.90GHz.

Fixture: `movement-v1`; simulation+diagnostics source SHA256: `F2C16585009A5A0FC3E4EB06C2993D795D0406C1CF98A4D224CF73FE905001B5`. Pre-optimization algorithm reference: `9d61486`; baseline adds opt-in timing instrumentation only.

Each case uses a fresh 128x96 m map, dense stable IDs, unarmed worker radius300 mm/speed3200 mm/s, and one or two simultaneous group orders. Crossing uses two owners. DynamicObstacle has N movers plus one builder and inserts a 3x9 m footprint at elapsed tick100. No shipped combat asset is changed.

120 simulated seconds/case at 20 Hz; 1 repetition(s); observer every tick. JIT warm-up uses separate worlds. Command latency includes the entire order batch and destination capture. Navigation totals include commands, scheduled construction and ticks, excluding world creation. Tick time/allocations bracket only World.Tick; observation and report allocations are excluded. Active ticks begin with at least one moving unit; full-window quantiles include settled idle ticks. Allocation counts are managed bytes on the current thread, not total heap/native/GPU usage. Timings are instrumented editor CPU measurements, not rendered FPS or mobile-device results.

Allocation capability: Unavailable: GC.GetAllocatedBytesForCurrentThread did not report at least4096 bytes for a retained 4096-byte array (or threw NotSupported/NotImplemented). Allocation fields are -1, never a zero-allocation claim. Probe observed bytes: 0.

Shared-field builds/cache hits and local recovery counts are reported separately below. Group route requests still increment path-query counts even when reusing a field; recovery queries are a subset of the path count. Their timing/visited cells stay in the path bucket. Do not add these counters as separate searches. These supplemental counters were introduced after the preserved baseline capture.

Arrival requires Idle within100 mm of the assigned goal. A stalled mover has made less than50 mm progress for200 ticks. Overlap means more than1 mm circle penetration; a pair persisting20 ticks fails the normal criterion. The independent radius/static-cell/map-edge checks run every tick. Peak overlap remains visible even when transient. Unreachable must reject atomically and remain still; its stall count is expected.

Final required gates: all sampled cases remain inside the map and outside static obstacles; Unreachable rejects/remains still; 100-unit OpenField, WideCorridor and CrossingGroups must all arrive by the deadline, with no final stalls/overlaps and no persistent overlap. Other sizes and constrained scenarios report the same acceptance outcome without silently treating congestion as a success. Baseline records failures without aborting.

| Scenario | Movers / total | Rep | Accepted orders | Command ms | Paths / ms | Active p50 / p95 / p99 / max ms | Tick allocation bytes | Arrived / seconds | Final stalls | Overlap peak / longest ticks / final | Invalid peak | Criterion | Required case |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| OpenField | 50 / 50 | 1 | 1/1 | 1.066 | 50 / 0.962 | 0.027 / 0.041 / 0.049 / 2.817 | -1 | 50/50 / 27.250 | 0 | 0 / 0 / 0 | 0 | True | True |
| OpenField | 100 / 100 | 1 | 1/1 | 13.691 | 100 / 13.234 | 0.052 / 0.066 / 0.089 / 0.119 | -1 | 100/100 / 27.450 | 0 | 0 / 0 / 0 | 0 | True | True |
| OpenField | 200 / 200 | 1 | 1/1 | 6.537 | 200 / 6.231 | 0.112 / 0.149 / 0.166 / 0.198 | -1 | 200/200 / 27.900 | 0 | 0 / 0 / 0 | 0 | True | True |
| OpenField | 300 / 300 | 1 | 1/1 | 14.117 | 300 / 13.515 | 0.171 / 0.270 / 0.300 / 0.805 | -1 | 300/300 / 28.400 | 0 | 0 / 0 / 0 | 0 | True | True |
| OpenField | 500 / 500 | 1 | 1/1 | 29.983 | 500 / 29.266 | 0.250 / 0.292 / 0.315 / 0.500 | -1 | 500/500 / 28.850 | 0 | 0 / 0 / 0 | 0 | True | True |
| WideCorridor | 50 / 50 | 1 | 1/1 | 0.965 | 50 / 0.871 | 0.027 / 0.031 / 0.056 / 0.169 | -1 | 50/50 / 27.250 | 0 | 0 / 0 / 0 | 0 | True | True |
| WideCorridor | 100 / 100 | 1 | 1/1 | 1.850 | 101 / 2.867 | 0.058 / 0.070 / 0.107 / 1.438 | -1 | 100/100 / 28.100 | 0 | 0 / 0 / 0 | 0 | True | True |
| WideCorridor | 200 / 200 | 1 | 1/1 | 4.802 | 223 / 4.930 | 0.131 / 0.184 / 0.233 / 2.272 | -1 | 200/200 / 33.900 | 0 | 0 / 0 / 0 | 0 | True | True |
| WideCorridor | 300 / 300 | 1 | 1/1 | 8.113 | 611 / 12.263 | 0.205 / 0.432 / 0.515 / 3.236 | -1 | 300/300 / 41.150 | 0 | 0 / 0 / 0 | 0 | True | True |
| WideCorridor | 500 / 500 | 1 | 1/1 | 18.252 | 729 / 21.624 | 0.309 / 0.488 / 0.598 / 4.074 | -1 | 500/500 / 45.550 | 0 | 0 / 0 / 0 | 0 | True | True |
| NarrowChoke | 50 / 50 | 1 | 1/1 | 1.132 | 298 / 4.795 | 0.038 / 0.161 / 0.227 / 0.278 | -1 | 50/50 / 44.800 | 0 | 0 / 0 / 0 | 0 | True | True |
| NarrowChoke | 100 / 100 | 1 | 1/1 | 2.763 | 1442 / 20.215 | 0.145 / 0.428 / 0.508 / 0.598 | -1 | 100/100 / 66.800 | 0 | 0 / 0 / 0 | 0 | True | True |
| NarrowChoke | 200 / 200 | 1 | 1/1 | 8.894 | 9321 / 75.002 | 0.986 / 1.123 / 1.225 / 1.546 | -1 | 31/200 / deadline | 0 | 0 / 0 / 0 | 0 | False | True |
| NarrowChoke | 300 / 300 | 1 | 1/1 | 26.928 | 9464 / 88.530 | 1.370 / 1.509 / 1.628 / 2.328 | -1 | 52/300 / deadline | 25 | 0 / 0 / 0 | 0 | False | True |
| NarrowChoke | 500 / 500 | 1 | 1/1 | 45.847 | 9767 / 110.405 | 2.705 / 2.907 / 3.108 / 7.666 | -1 | 37/500 / deadline | 98 | 0 / 0 / 0 | 0 | False | True |
| CrossingGroups | 50 / 50 | 1 | 2/2 | 1.221 | 60 / 1.475 | 0.027 / 0.048 / 0.073 / 0.272 | -1 | 50/50 / 31.050 | 0 | 0 / 0 / 0 | 0 | True | True |
| CrossingGroups | 100 / 100 | 1 | 2/2 | 2.235 | 163 / 3.832 | 0.056 / 0.173 / 0.267 / 0.308 | -1 | 100/100 / 32.200 | 0 | 0 / 0 / 0 | 0 | True | True |
| CrossingGroups | 200 / 200 | 1 | 2/2 | 15.056 | 367 / 13.157 | 0.111 / 0.381 / 0.477 / 0.624 | -1 | 200/200 / 36.450 | 0 | 0 / 0 / 0 | 0 | True | True |
| CrossingGroups | 300 / 300 | 1 | 2/2 | 18.325 | 690 / 25.849 | 0.164 / 0.645 / 0.744 / 1.118 | -1 | 300/300 / 37.250 | 0 | 0 / 0 / 0 | 0 | True | True |
| CrossingGroups | 500 / 500 | 1 | 2/2 | 38.295 | 1463 / 134.050 | 0.279 / 1.543 / 2.753 / 40.866 | -1 | 500/500 / 44.200 | 0 | 0 / 0 / 0 | 0 | True | True |
| DynamicObstacle | 50 / 51 | 1 | 1/1 | 0.961 | 174 / 3.336 | 0.030 / 0.105 / 0.169 / 0.302 | -1 | 50/50 / 32.600 | 0 | 0 / 0 / 0 | 0 | True | True |
| DynamicObstacle | 100 / 101 | 1 | 1/1 | 2.172 | 349 / 6.637 | 0.058 / 0.211 / 0.289 / 0.468 | -1 | 100/100 / 38.450 | 0 | 0 / 0 / 0 | 0 | True | True |
| DynamicObstacle | 200 / 201 | 1 | 1/1 | 6.141 | 677 / 15.678 | 0.114 / 0.351 / 0.422 / 0.744 | -1 | 200/200 / 40.550 | 0 | 0 / 0 / 0 | 0 | True | True |
| DynamicObstacle | 300 / 301 | 1 | 1/1 | 12.056 | 939 / 26.731 | 0.162 / 0.462 / 0.539 / 0.950 | -1 | 300/300 / 43.900 | 0 | 0 / 0 / 0 | 0 | True | True |
| DynamicObstacle | 500 / 501 | 1 | 1/1 | 29.742 | 1507 / 63.990 | 0.272 / 0.663 / 0.743 / 1.494 | -1 | 500/500 / 44.900 | 0 | 0 / 0 / 0 | 0 | True | True |
| Unreachable | 50 / 50 | 1 | 0/1 | 0.030 | 0 / 0.000 | 0.000 / 0.000 / 0.000 / 0.000 | -1 | 0/50 / deadline | 50 | 0 / 0 / 0 | 0 | True | True |
| Unreachable | 100 / 100 | 1 | 0/1 | 0.043 | 0 / 0.000 | 0.000 / 0.000 / 0.000 / 0.000 | -1 | 0/100 / deadline | 100 | 0 / 0 / 0 | 0 | True | True |
| Unreachable | 200 / 200 | 1 | 0/1 | 0.071 | 0 / 0.000 | 0.000 / 0.000 / 0.000 / 0.000 | -1 | 0/200 / deadline | 200 | 0 / 0 / 0 | 0 | True | True |
| Unreachable | 300 / 300 | 1 | 0/1 | 0.094 | 0 / 0.000 | 0.000 / 0.000 / 0.000 / 0.000 | -1 | 0/300 / deadline | 300 | 0 / 0 / 0 | 0 | True | True |
| Unreachable | 500 / 500 | 1 | 0/1 | 0.146 | 0 / 0.000 | 0.000 / 0.000 / 0.000 / 0.000 | -1 | 0/500 / deadline | 500 | 0 / 0 / 0 | 0 | True | True |

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

Run complete: True. Required gates covered: True. Required acceptance passed: True. Runner wall seconds (including observation/warm-up): 20.738.

The adjacent JSON contains all-window quantiles, active sample counts, maximum tick/command/event allocation, complete navigation/connectivity counters and times, individual order rejection reasons, dynamic build outcome, maximum penetration, separate outside/static counts and gate reasons. Single-repetition results are a diagnostic sample, not a claim of stable cross-machine performance.
