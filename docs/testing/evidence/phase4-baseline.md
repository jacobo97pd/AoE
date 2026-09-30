# Movement CPU measurement: baseline

Captured UTC: 2026-09-09T18:56:48.0222856Z. Unity 6000.3.23f1; Windows 11  (10.0.26200); Intel(R) Core(TM) i5-10400F CPU @ 2.90GHz.

Fixture: `movement-v1`; simulation+diagnostics source SHA256: `705203E3F808F0F4191E5DEF18EF7EAC1AC237B7B19A18853543ADAE097D2632`. Pre-optimization algorithm reference: `9d61486`; baseline adds opt-in timing instrumentation only.

Each case uses a fresh 128x96 m map, dense stable IDs, unarmed worker radius300 mm/speed3200 mm/s, and one or two simultaneous group orders. Crossing uses two owners. DynamicObstacle has N movers plus one builder and inserts a 3x9 m footprint at elapsed tick100. No shipped combat asset is changed.

120 simulated seconds/case at 20 Hz; 1 repetition(s); observer every tick. JIT warm-up uses separate worlds. Command latency includes the entire order batch and destination capture. Navigation totals include commands, scheduled construction and ticks, excluding world creation. Tick time/allocations bracket only World.Tick; observation and report allocations are excluded. Active ticks begin with at least one moving unit; full-window quantiles include settled idle ticks. Allocation counts are managed bytes on the current thread, not total heap/native/GPU usage. Timings are instrumented editor CPU measurements, not rendered FPS or mobile-device results.

Arrival requires Idle within100 mm of the assigned goal. A stalled mover has made less than50 mm progress for200 ticks. Overlap means more than1 mm circle penetration; a pair persisting20 ticks fails the normal criterion. The independent radius/static-cell/map-edge checks run every tick. Peak overlap remains visible even when transient. Unreachable must reject atomically and remain still; its stall count is expected.

Final required gates: all sampled cases remain inside the map and outside static obstacles; Unreachable rejects/remains still; 100-unit OpenField, WideCorridor and CrossingGroups must all arrive by the deadline, with no final stalls/overlaps and no persistent overlap. Other sizes and constrained scenarios report the same acceptance outcome without silently treating congestion as a success. Baseline records failures without aborting.

| Scenario | Movers / total | Rep | Accepted orders | Command ms | Paths / ms | Active p50 / p95 / p99 / max ms | Tick allocation bytes | Arrived / seconds | Final stalls | Overlap peak / longest ticks / final | Invalid peak | Criterion | Required case |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| OpenField | 50 / 50 | 1 | 1/1 | 8.299 | 50 / 8.119 | 0.008 / 0.013 / 0.016 / 0.178 | 0 | 50/50 / 29.700 | 0 | 27 / 13 / 0 | 0 | True | True |
| OpenField | 100 / 100 | 1 | 1/1 | 19.620 | 100 / 19.140 | 0.016 / 0.023 / 0.027 / 0.075 | 0 | 100/100 / 30.350 | 0 | 52 / 32 / 0 | 0 | False | False |
| OpenField | 200 / 200 | 1 | 1/1 | 52.198 | 200 / 49.833 | 0.034 / 0.045 / 0.052 / 0.106 | 0 | 200/200 / 34.400 | 0 | 111 / 44 / 0 | 0 | False | True |
| OpenField | 300 / 300 | 1 | 1/1 | 81.049 | 300 / 76.833 | 0.062 / 0.086 / 0.107 / 0.185 | 0 | 300/300 / 35.000 | 0 | 173 / 62 / 0 | 0 | False | True |
| OpenField | 500 / 500 | 1 | 1/1 | 95.159 | 500 / 85.228 | 0.083 / 0.101 / 0.103 / 0.159 | 0 | 500/500 / 39.400 | 0 | 289 / 112 / 0 | 0 | False | True |
| WideCorridor | 50 / 50 | 1 | 1/1 | 7.745 | 50 / 7.608 | 0.008 / 0.010 / 0.010 / 0.135 | 0 | 50/50 / 29.700 | 0 | 27 / 13 / 0 | 0 | True | True |
| WideCorridor | 100 / 100 | 1 | 1/1 | 16.079 | 100 / 15.591 | 0.016 / 0.021 / 0.029 / 0.034 | 0 | 100/100 / 30.350 | 0 | 52 / 32 / 0 | 0 | False | False |
| WideCorridor | 200 / 200 | 1 | 1/1 | 34.876 | 200 / 33.192 | 0.035 / 0.061 / 0.156 / 0.892 | 0 | 200/200 / 34.400 | 0 | 118 / 450 / 0 | 0 | False | True |
| WideCorridor | 300 / 300 | 1 | 1/1 | 52.344 | 300 / 48.709 | 0.047 / 0.065 / 0.088 / 0.122 | 0 | 300/300 / 35.000 | 0 | 177 / 518 / 0 | 0 | False | True |
| WideCorridor | 500 / 500 | 1 | 1/1 | 92.520 | 500 / 82.626 | 0.082 / 0.099 / 0.103 / 0.151 | 0 | 500/500 / 39.400 | 0 | 307 / 519 / 0 | 0 | False | True |
| NarrowChoke | 50 / 50 | 1 | 1/1 | 7.740 | 50 / 7.596 | 0.008 / 0.010 / 0.010 / 0.016 | 0 | 50/50 / 30.000 | 0 | 105 / 286 / 0 | 0 | False | True |
| NarrowChoke | 100 / 100 | 1 | 1/1 | 16.292 | 100 / 15.845 | 0.016 / 0.020 / 0.021 / 0.035 | 0 | 100/100 / 30.950 | 0 | 365 / 300 / 0 | 0 | False | True |
| NarrowChoke | 200 / 200 | 1 | 1/1 | 34.254 | 200 / 32.537 | 0.032 / 0.054 / 0.063 / 0.124 | 0 | 200/200 / 34.400 | 0 | 1006 / 325 / 0 | 0 | False | True |
| NarrowChoke | 300 / 300 | 1 | 1/1 | 51.706 | 300 / 48.077 | 0.049 / 0.060 / 0.077 / 0.095 | 0 | 300/300 / 36.250 | 0 | 1944 / 350 / 0 | 0 | False | True |
| NarrowChoke | 500 / 500 | 1 | 1/1 | 90.173 | 500 / 80.395 | 0.081 / 0.099 / 0.109 / 0.135 | 0 | 500/500 / 39.400 | 0 | 4319 / 375 / 0 | 0 | False | True |
| CrossingGroups | 50 / 50 | 1 | 2/2 | 7.947 | 50 / 7.856 | 0.008 / 0.010 / 0.010 / 0.012 | 0 | 50/50 / 28.450 | 0 | 50 / 538 / 0 | 0 | False | True |
| CrossingGroups | 100 / 100 | 1 | 2/2 | 17.435 | 100 / 17.135 | 0.016 / 0.019 / 0.026 / 0.070 | 0 | 100/100 / 29.700 | 0 | 82 / 531 / 0 | 0 | False | False |
| CrossingGroups | 200 / 200 | 1 | 2/2 | 32.509 | 200 / 31.599 | 0.032 / 0.039 / 0.045 / 0.058 | 0 | 200/200 / 30.350 | 0 | 180 / 550 / 0 | 0 | False | True |
| CrossingGroups | 300 / 300 | 1 | 2/2 | 50.865 | 300 / 48.882 | 0.048 / 0.058 / 0.059 / 0.083 | 0 | 300/300 / 33.150 | 0 | 249 / 582 / 0 | 0 | False | True |
| CrossingGroups | 500 / 500 | 1 | 2/2 | 88.621 | 500 / 83.579 | 0.079 / 0.097 / 0.107 / 0.135 | 0 | 500/500 / 33.450 | 0 | 457 / 569 / 0 | 0 | False | True |
| DynamicObstacle | 50 / 51 | 1 | 1/1 | 7.871 | 124 / 16.044 | 0.008 / 0.010 / 0.011 / 0.752 | 0 | 50/50 / 31.600 | 0 | 53 / 319 / 0 | 0 | False | True |
| DynamicObstacle | 100 / 101 | 1 | 1/1 | 16.069 | 224 / 32.037 | 0.016 / 0.019 / 0.026 / 0.156 | 0 | 100/100 / 32.200 | 0 | 180 / 318 / 0 | 0 | False | True |
| DynamicObstacle | 200 / 201 | 1 | 1/1 | 34.141 | 424 / 64.671 | 0.032 / 0.050 / 0.057 / 0.066 | 0 | 200/200 / 34.400 | 0 | 412 / 344 / 0 | 0 | False | True |
| DynamicObstacle | 300 / 301 | 1 | 1/1 | 51.671 | 624 / 95.160 | 0.048 / 0.058 / 0.060 / 0.083 | 0 | 300/300 / 35.000 | 0 | 410 / 337 / 0 | 0 | False | True |
| DynamicObstacle | 500 / 501 | 1 | 1/1 | 92.359 | 1024 / 161.949 | 0.079 / 0.097 / 0.117 / 0.146 | 0 | 500/500 / 39.400 | 0 | 641 / 354 / 0 | 0 | False | True |
| Unreachable | 50 / 50 | 1 | 0/1 | 0.018 | 0 / 0.000 | 0.000 / 0.000 / 0.000 / 0.000 | 0 | 0/50 / deadline | 50 | 0 / 0 / 0 | 0 | True | True |
| Unreachable | 100 / 100 | 1 | 0/1 | 0.041 | 0 / 0.000 | 0.000 / 0.000 / 0.000 / 0.000 | 0 | 0/100 / deadline | 100 | 0 / 0 / 0 | 0 | True | True |
| Unreachable | 200 / 200 | 1 | 0/1 | 0.066 | 0 / 0.000 | 0.000 / 0.000 / 0.000 / 0.000 | 0 | 0/200 / deadline | 200 | 0 / 0 / 0 | 0 | True | True |
| Unreachable | 300 / 300 | 1 | 0/1 | 0.075 | 0 / 0.000 | 0.000 / 0.000 / 0.000 / 0.000 | 0 | 0/300 / deadline | 300 | 0 / 0 / 0 | 0 | True | True |
| Unreachable | 500 / 500 | 1 | 0/1 | 0.170 | 0 / 0.000 | 0.000 / 0.000 / 0.000 / 0.000 | 0 | 0/500 / deadline | 500 | 0 / 0 / 0 | 0 | True | True |

Run complete: True. Required acceptance passed: False. Runner wall seconds (including observation/warm-up): 7.964.

The adjacent JSON contains all-window quantiles, active sample counts, maximum tick/command/event allocation, complete navigation/connectivity counters and times, individual order rejection reasons, dynamic build outcome, maximum penetration, separate outside/static counts and gate reasons. Single-repetition results are a diagnostic sample, not a claim of stable cross-machine performance.
