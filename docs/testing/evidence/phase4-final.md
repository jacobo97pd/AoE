# Movement CPU measurement: final

Captured UTC: 2026-09-09T19:49:02.0662329Z. Unity 6000.3.23f1; Windows 11  (10.0.26200); Intel(R) Core(TM) i5-10400F CPU @ 2.90GHz.

Fixture: `movement-v1`; simulation+diagnostics source SHA256: `76F7C85534B2A4BC1CAF500F878CADC4241058F15AF713067639E1D1E5ACF6CB`. Pre-optimization algorithm reference: `9d61486`; baseline adds opt-in timing instrumentation only.

Each case uses a fresh 128x96 m map, dense stable IDs, unarmed worker radius300 mm/speed3200 mm/s, and one or two simultaneous group orders. Crossing uses two owners. DynamicObstacle has N movers plus one builder and inserts a 3x9 m footprint at elapsed tick100. No shipped combat asset is changed.

120 simulated seconds/case at 20 Hz; 1 repetition(s); observer every tick. JIT warm-up uses separate worlds. Command latency includes the entire order batch and destination capture. Navigation totals include commands, scheduled construction and ticks, excluding world creation. Tick time/allocations bracket only World.Tick; observation and report allocations are excluded. Active ticks begin with at least one moving unit; full-window quantiles include settled idle ticks. Allocation counts are managed bytes on the current thread, not total heap/native/GPU usage. Timings are instrumented editor CPU measurements, not rendered FPS or mobile-device results.

Allocation capability: Unavailable: GC.GetAllocatedBytesForCurrentThread did not report at least4096 bytes for a retained 4096-byte array (or threw NotSupported/NotImplemented). Allocation fields are -1, never a zero-allocation claim. Probe observed bytes: 0.

Shared-field builds/cache hits and local recovery counts are reported separately below. Group route requests still increment path-query counts even when reusing a field; recovery queries are a subset of the path count. Their timing/visited cells stay in the path bucket. Do not add these counters as separate searches. These supplemental counters were introduced after the preserved baseline capture.

Arrival requires Idle within100 mm of the assigned goal. A stalled mover has made less than50 mm progress for200 ticks. Overlap means more than1 mm circle penetration; a pair persisting20 ticks fails the normal criterion. The independent radius/static-cell/map-edge checks run every tick. Peak overlap remains visible even when transient. Unreachable must reject atomically and remain still; its stall count is expected.

Final required gates: all sampled cases remain inside the map and outside static obstacles; Unreachable rejects/remains still; 100-unit OpenField, WideCorridor and CrossingGroups must all arrive by the deadline, with no final stalls/overlaps and no persistent overlap. Other sizes and constrained scenarios report the same acceptance outcome without silently treating congestion as a success. Baseline records failures without aborting.

| Scenario | Movers / total | Rep | Accepted orders | Command ms | Paths / ms | Active p50 / p95 / p99 / max ms | Tick allocation bytes | Arrived / seconds | Final stalls | Overlap peak / longest ticks / final | Invalid peak | Criterion | Required case |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| OpenField | 50 / 50 | 1 | 1/1 | 1.049 | 50 / 0.951 | 0.027 / 0.042 / 0.048 / 0.197 | -1 | 50/50 / 27.250 | 0 | 0 / 0 / 0 | 0 | True | True |
| OpenField | 100 / 100 | 1 | 1/1 | 7.788 | 100 / 6.743 | 0.051 / 0.059 / 0.074 / 0.145 | -1 | 100/100 / 27.450 | 0 | 0 / 0 / 0 | 0 | True | True |
| OpenField | 200 / 200 | 1 | 1/1 | 6.804 | 200 / 6.497 | 0.104 / 0.150 / 0.167 / 0.194 | -1 | 200/200 / 27.900 | 0 | 0 / 0 / 0 | 0 | True | True |
| OpenField | 300 / 300 | 1 | 1/1 | 50.364 | 300 / 48.817 | 0.167 / 0.246 / 0.267 / 3.029 | -1 | 300/300 / 28.400 | 0 | 0 / 0 / 0 | 0 | True | True |
| OpenField | 500 / 500 | 1 | 1/1 | 34.154 | 500 / 33.419 | 0.249 / 0.307 / 0.369 / 0.515 | -1 | 500/500 / 28.850 | 0 | 0 / 0 / 0 | 0 | True | True |
| WideCorridor | 50 / 50 | 1 | 1/1 | 0.896 | 50 / 0.814 | 0.026 / 0.032 / 0.047 / 0.131 | -1 | 50/50 / 27.250 | 0 | 0 / 0 / 0 | 0 | True | True |
| WideCorridor | 100 / 100 | 1 | 1/1 | 1.753 | 101 / 2.736 | 0.057 / 0.080 / 0.175 / 1.374 | -1 | 100/100 / 28.100 | 0 | 0 / 0 / 0 | 0 | True | True |
| WideCorridor | 200 / 200 | 1 | 1/1 | 4.735 | 223 / 4.694 | 0.129 / 0.180 / 0.213 / 2.194 | -1 | 200/200 / 33.900 | 0 | 0 / 0 / 0 | 0 | True | True |
| WideCorridor | 300 / 300 | 1 | 1/1 | 7.910 | 611 / 12.073 | 0.203 / 0.434 / 0.513 / 3.236 | -1 | 300/300 / 41.150 | 0 | 0 / 0 / 0 | 0 | True | True |
| WideCorridor | 500 / 500 | 1 | 1/1 | 17.918 | 729 / 21.261 | 0.307 / 0.474 / 0.574 / 4.085 | -1 | 500/500 / 45.550 | 0 | 0 / 0 / 0 | 0 | True | True |
| NarrowChoke | 50 / 50 | 1 | 1/1 | 1.080 | 298 / 4.805 | 0.039 / 0.162 / 0.233 / 0.300 | -1 | 50/50 / 44.800 | 0 | 0 / 0 / 0 | 0 | True | True |
| NarrowChoke | 100 / 100 | 1 | 1/1 | 2.754 | 1442 / 19.944 | 0.145 / 0.428 / 0.512 / 0.649 | -1 | 100/100 / 66.800 | 0 | 0 / 0 / 0 | 0 | True | True |
| NarrowChoke | 200 / 200 | 1 | 1/1 | 8.895 | 9321 / 73.225 | 0.984 / 1.115 / 1.243 / 1.817 | -1 | 31/200 / deadline | 0 | 0 / 0 / 0 | 0 | False | True |
| NarrowChoke | 300 / 300 | 1 | 1/1 | 18.420 | 9464 / 79.293 | 1.368 / 1.513 / 1.619 / 4.587 | -1 | 52/300 / deadline | 25 | 0 / 0 / 0 | 0 | False | True |
| NarrowChoke | 500 / 500 | 1 | 1/1 | 45.890 | 9767 / 109.303 | 2.693 / 2.863 / 3.030 / 5.025 | -1 | 37/500 / deadline | 98 | 0 / 0 / 0 | 0 | False | True |
| CrossingGroups | 50 / 50 | 1 | 2/2 | 1.148 | 60 / 1.401 | 0.026 / 0.048 / 0.077 / 0.266 | -1 | 50/50 / 31.050 | 0 | 0 / 0 / 0 | 0 | True | True |
| CrossingGroups | 100 / 100 | 1 | 2/2 | 2.104 | 163 / 3.713 | 0.057 / 0.177 / 0.264 / 0.357 | -1 | 100/100 / 32.200 | 0 | 0 / 0 / 0 | 0 | True | True |
| CrossingGroups | 200 / 200 | 1 | 2/2 | 5.307 | 367 / 8.402 | 0.111 / 0.375 / 0.551 / 0.765 | -1 | 200/200 / 36.450 | 0 | 0 / 0 / 0 | 0 | True | True |
| CrossingGroups | 300 / 300 | 1 | 2/2 | 11.693 | 690 / 18.601 | 0.165 / 0.620 / 0.720 / 0.793 | -1 | 300/300 / 37.250 | 0 | 0 / 0 / 0 | 0 | True | True |
| CrossingGroups | 500 / 500 | 1 | 2/2 | 22.310 | 1463 / 37.909 | 0.278 / 1.468 / 1.617 / 1.707 | -1 | 500/500 / 44.200 | 0 | 0 / 0 / 0 | 0 | True | True |
| DynamicObstacle | 50 / 51 | 1 | 1/1 | 0.965 | 174 / 3.394 | 0.030 / 0.104 / 0.168 / 0.297 | -1 | 50/50 / 32.600 | 0 | 0 / 0 / 0 | 0 | True | True |
| DynamicObstacle | 100 / 101 | 1 | 1/1 | 2.111 | 349 / 6.590 | 0.057 / 0.208 / 0.288 / 0.463 | -1 | 100/100 / 38.450 | 0 | 0 / 0 / 0 | 0 | True | True |
| DynamicObstacle | 200 / 201 | 1 | 1/1 | 6.388 | 677 / 16.070 | 0.113 / 0.364 / 0.419 / 0.792 | -1 | 200/200 / 40.550 | 0 | 0 / 0 / 0 | 0 | True | True |
| DynamicObstacle | 300 / 301 | 1 | 1/1 | 12.584 | 939 / 27.498 | 0.164 / 0.464 / 0.541 / 0.974 | -1 | 300/300 / 43.900 | 0 | 0 / 0 / 0 | 0 | True | True |
| DynamicObstacle | 500 / 501 | 1 | 1/1 | 30.470 | 1507 / 64.773 | 0.270 / 0.662 / 0.776 / 1.403 | -1 | 500/500 / 44.900 | 0 | 0 / 0 / 0 | 0 | True | True |
| Unreachable | 50 / 50 | 1 | 0/1 | 0.029 | 0 / 0.000 | 0.000 / 0.000 / 0.000 / 0.000 | -1 | 0/50 / deadline | 50 | 0 / 0 / 0 | 0 | True | True |
| Unreachable | 100 / 100 | 1 | 0/1 | 0.044 | 0 / 0.000 | 0.000 / 0.000 / 0.000 / 0.000 | -1 | 0/100 / deadline | 100 | 0 / 0 / 0 | 0 | True | True |
| Unreachable | 200 / 200 | 1 | 0/1 | 0.079 | 0 / 0.000 | 0.000 / 0.000 / 0.000 / 0.000 | -1 | 0/200 / deadline | 200 | 0 / 0 / 0 | 0 | True | True |
| Unreachable | 300 / 300 | 1 | 0/1 | 0.095 | 0 / 0.000 | 0.000 / 0.000 / 0.000 / 0.000 | -1 | 0/300 / deadline | 300 | 0 / 0 / 0 | 0 | True | True |
| Unreachable | 500 / 500 | 1 | 0/1 | 0.156 | 0 / 0.000 | 0.000 / 0.000 / 0.000 / 0.000 | -1 | 0/500 / deadline | 500 | 0 / 0 / 0 | 0 | True | True |

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

Run complete: True. Required gates covered: True. Required acceptance passed: True. Runner wall seconds (including observation/warm-up): 20.588.

The adjacent JSON contains all-window quantiles, active sample counts, maximum tick/command/event allocation, complete navigation/connectivity counters and times, individual order rejection reasons, dynamic build outcome, maximum penetration, separate outside/static counts and gate reasons. Single-repetition results are a diagnostic sample, not a claim of stable cross-machine performance.
