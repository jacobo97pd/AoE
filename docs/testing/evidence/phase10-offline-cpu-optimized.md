# Offline CPU workload: optimized

UTC 2026-09-09T22:57:56.7169612Z; Unity 6000.3.23f1; runtime 4.0.30319.42000; Windows 11  (10.0.26200); Intel(R) Core(TM) i5-10400F CPU @ 2.90GHz (12 logical processors); system memory 16281MB. Graphics device reported by runtime: Null Device / Null; no graphics measurement is performed.

World and AI timing/allocation windows are disjoint. AI includes its legal command submissions; fixed script batches are separate. Navigation counters include all three windows. Observation, hashing, report construction and GC.GetTotalMemory(false) sampling are outside timed windows. GC collection/heap fields cover the whole sample loop, including diagnostics; heap deltas are not allocation totals. No forced collection, sleep, rendering, GPU or mobile-device measurement.

Unmeasured separate12-mover synthetic world advances1200ticks before any sampled case. Every measured case starts with a new World and fresh navigation caches. Shipped mid samples first advance their own AI/world to the requested start tick; that progression is excluded from timing windows.

Fixture `offline-cpu-v1`; Simulation+Diagnostics source SHA256 `F2C16585009A5A0FC3E4EB06C2993D795D0406C1CF98A4D224CF73FE905001B5`; editor runner SHA256 `9D5F00EB9400A5486B402C1C8D373775BD85B4948B6B8A7342082F5BC04BB0F3`.

Shipped rules SHA256 `6F100B25035E54E4144761F51885F52E45051EBD6562EF6024AFC37601CB63C2`; Amber Crossing map SHA256 `3EF7E44A71AE3C76C74C3DC04BD0BA7E8EFE43AA0BDC500AD2450487D493D072`.

Synthetic rows use a128x128m map and author N moving Aven Tenders, four other gatherers per side, one Threadkeeper and completed infrastructure, with 5000 of each resource/player and base population 1000. An11-cell solid buffer across z70..80 encloses the Serevin AI annex beyond attack range, preventing combat deaths from changing N. This is a noncombat scaling fixture with a deliberately unreachable opponent, not a complete match. Serevin AI and paid scripted Aven actions run under shipped rules/timers. It is not a normal start or a population/progression recommendation. Unrestricted shipped samples cover combat. The exact schedule and rejected script actions are in source/JSON.

Shipped rows use ordinary starts with both factions and both victory modes. Early sampling starts at0s; mid sampling at300s after ordinary AI progression. Requested windows: stress120s, shipped60s, repetitions2. A result ends sampling; unavailable/truncated windows remain explicit. Full-match pacing evidence comes from OfflineMatchVerification, not this runner.

| Fixture / mode / P1 faction | N movers | Rep | Start / sampled ticks | World p50 / p95 / p99 / max ms | AI think p50 / p95 / max ms | Script batch total / max ms | Units initial / peak / final | Window available / match ended |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| SyntheticMovers / Conquest / aven | 50 | 1 | 0 / 2400 | 0.347 / 0.640 / 0.798 / 3.214 | 0.124 / 1.100 / 8.800 | 25.956 / 23.966 | 59 / 93 / 93 | True / False |
| SyntheticMovers / Conquest / aven | 50 | 2 | 0 / 2400 | 0.340 / 0.622 / 0.765 / 1.955 | 0.125 / 0.679 / 2.599 | 4.216 / 2.260 | 59 / 93 / 93 | True / False |
| SyntheticMovers / Conquest / aven | 100 | 1 | 0 / 2400 | 0.465 / 0.799 / 0.942 / 1.490 | 0.150 / 1.204 / 6.775 | 7.675 / 4.014 | 109 / 143 / 143 | True / False |
| SyntheticMovers / Conquest / aven | 100 | 2 | 0 / 2400 | 0.464 / 0.792 / 0.976 / 1.495 | 0.145 / 0.645 / 3.406 | 7.351 / 3.747 | 109 / 143 / 143 | True / False |
| SyntheticMovers / Conquest / aven | 200 | 1 | 0 / 2400 | 0.718 / 1.133 / 1.466 / 2.219 | 0.162 / 0.777 / 7.649 | 17.822 / 7.945 | 209 / 243 / 243 | True / False |
| SyntheticMovers / Conquest / aven | 200 | 2 | 0 / 2400 | 0.715 / 1.115 / 1.342 / 1.735 | 0.164 / 0.719 / 7.497 | 18.006 / 8.078 | 209 / 243 / 243 | True / False |
| SyntheticMovers / Conquest / aven | 300 | 1 | 0 / 2400 | 0.970 / 1.484 / 1.832 / 2.778 | 0.193 / 0.772 / 13.328 | 44.534 / 21.326 | 309 / 343 / 343 | True / False |
| SyntheticMovers / Conquest / aven | 300 | 2 | 0 / 2400 | 0.966 / 1.441 / 1.736 / 2.337 | 0.186 / 0.723 / 12.936 | 31.625 / 13.841 | 309 / 343 / 343 | True / False |
| SyntheticMovers / Conquest / aven | 500 | 1 | 0 / 2400 | 1.493 / 2.133 / 2.502 / 3.337 | 0.219 / 0.881 / 28.329 | 72.801 / 29.845 | 509 / 543 / 543 | True / False |
| SyntheticMovers / Conquest / aven | 500 | 2 | 0 / 2400 | 1.494 / 2.149 / 2.576 / 5.259 | 0.225 / 0.901 / 28.658 | 77.536 / 30.014 | 509 / 543 / 543 | True / False |
| ShippedEarly / Conquest / aven | 0 | 1 | 0 / 1200 | 0.101 / 0.212 / 0.274 / 0.572 | 0.027 / 0.165 / 1.175 | 0.000 / 0.000 | 8 / 24 / 24 | True / False |
| ShippedEarly / Conquest / aven | 0 | 2 | 0 / 1200 | 0.101 / 0.212 / 0.269 / 0.499 | 0.027 / 0.190 / 1.183 | 0.000 / 0.000 | 8 / 24 / 24 | True / False |
| ShippedMid / Conquest / aven | 0 | 1 | 6000 / 1200 | 0.259 / 0.536 / 0.660 / 0.822 | 0.275 / 0.517 / 1.544 | 0.000 / 0.000 | 55 / 66 / 66 | True / False |
| ShippedMid / Conquest / aven | 0 | 2 | 6000 / 1200 | 0.261 / 0.557 / 0.747 / 2.190 | 0.263 / 0.571 / 1.535 | 0.000 / 0.000 | 55 / 66 / 66 | True / False |
| ShippedEarly / Conquest / serevin | 0 | 1 | 0 / 1200 | 0.102 / 0.204 / 0.257 / 0.472 | 0.026 / 0.162 / 1.178 | 0.000 / 0.000 | 8 / 24 / 24 | True / False |
| ShippedEarly / Conquest / serevin | 0 | 2 | 0 / 1200 | 0.101 / 0.205 / 0.261 / 0.475 | 0.026 / 0.165 / 1.209 | 0.000 / 0.000 | 8 / 24 / 24 | True / False |
| ShippedMid / Conquest / serevin | 0 | 1 | 6000 / 1200 | 0.267 / 0.602 / 0.852 / 3.577 | 0.261 / 0.647 / 4.386 | 0.000 / 0.000 | 50 / 60 / 60 | True / False |
| ShippedMid / Conquest / serevin | 0 | 2 | 6000 / 1200 | 0.265 / 0.574 / 0.754 / 0.980 | 0.237 / 0.513 / 1.656 | 0.000 / 0.000 | 50 / 60 / 60 | True / False |
| ShippedEarly / Dominion / aven | 0 | 1 | 0 / 1200 | 0.103 / 0.215 / 0.287 / 0.618 | 0.027 / 0.177 / 1.274 | 0.000 / 0.000 | 8 / 24 / 24 | True / False |
| ShippedEarly / Dominion / aven | 0 | 2 | 0 / 1200 | 0.112 / 0.226 / 0.293 / 0.510 | 0.034 / 0.237 / 1.190 | 0.000 / 0.000 | 8 / 24 / 24 | True / False |
| ShippedMid / Dominion / aven | 0 | 1 | 6000 / 1200 | 0.248 / 0.672 / 0.804 / 1.215 | 0.140 / 0.561 / 1.545 | 0.000 / 0.000 | 51 / 55 / 55 | True / False |
| ShippedMid / Dominion / aven | 0 | 2 | 6000 / 1200 | 0.245 / 0.657 / 0.801 / 1.213 | 0.137 / 0.649 / 1.532 | 0.000 / 0.000 | 51 / 55 / 55 | True / False |
| ShippedEarly / Dominion / serevin | 0 | 1 | 0 / 1200 | 0.103 / 0.208 / 0.270 / 0.477 | 0.027 / 0.162 / 1.221 | 0.000 / 0.000 | 8 / 24 / 24 | True / False |
| ShippedEarly / Dominion / serevin | 0 | 2 | 0 / 1200 | 0.102 / 0.207 / 0.257 / 0.478 | 0.027 / 0.174 / 1.197 | 0.000 / 0.000 | 8 / 24 / 24 | True / False |
| ShippedMid / Dominion / serevin | 0 | 1 | 6000 / 1200 | 0.232 / 0.393 / 0.522 / 0.808 | 0.139 / 0.339 / 1.454 | 0.000 / 0.000 | 50 / 52 / 52 | True / False |
| ShippedMid / Dominion / serevin | 0 | 2 | 6000 / 1200 | 0.230 / 0.421 / 0.558 / 0.802 | 0.142 / 0.340 / 1.458 | 0.000 / 0.000 | 50 / 52 / 52 | True / False |

| Fixture / N / mode / P1 / start / rep | Moving / gatherers peak | Active research / charters / affected units / boosted producers peak | AI accepted / commands | Script accepted / commands | Navigation queries / ms | Allocation bytes World / AI / script | GC0 / GC1 / GC2 | Heap before / peak / after bytes |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| SyntheticMovers / 50 / Conquest / aven / 0 / 1 | 68 / 16 | 3 / 2 / 4 / 1 | 229/263 | 30/30 | 11321 / 109.602 | -1 / -1 / -1 | 1 / 1 / 1 | 574251008 / 602279936 / 602279936 |
| SyntheticMovers / 50 / Conquest / aven / 0 / 2 | 68 / 16 | 3 / 2 / 4 / 1 | 229/263 | 30/30 | 11321 / 80.136 | -1 / -1 / -1 | 0 / 0 / 0 | 604590080 / 624906240 / 624906240 |
| SyntheticMovers / 100 / Conquest / aven / 0 / 1 | 119 / 16 | 3 / 2 / 4 / 1 | 237/272 | 30/30 | 11393 / 85.195 | -1 / -1 / -1 | 1 / 1 / 1 | 627290112 / 633200640 / 577818624 |
| SyntheticMovers / 100 / Conquest / aven / 0 / 2 | 119 / 16 | 3 / 2 / 4 / 1 | 237/272 | 30/30 | 11393 / 82.385 | -1 / -1 / -1 | 0 / 0 / 0 | 580108288 / 599908352 / 599908352 |
| SyntheticMovers / 200 / Conquest / aven / 0 / 1 | 219 / 16 | 3 / 2 / 4 / 1 | 228/262 | 30/30 | 11929 / 98.248 | -1 / -1 / -1 | 0 / 0 / 0 | 602324992 / 623534080 / 623534080 |
| SyntheticMovers / 200 / Conquest / aven / 0 / 2 | 219 / 16 | 3 / 2 / 4 / 1 | 228/262 | 30/30 | 11929 / 97.072 | -1 / -1 / -1 | 0 / 0 / 0 | 625995776 / 647639040 / 647639040 |
| SyntheticMovers / 300 / Conquest / aven / 0 / 1 | 318 / 16 | 3 / 2 / 4 / 1 | 232/266 | 30/30 | 12471 / 130.638 | -1 / -1 / -1 | 1 / 1 / 1 | 650215424 / 659963904 / 578027520 |
| SyntheticMovers / 300 / Conquest / aven / 0 / 2 | 318 / 16 | 3 / 2 / 4 / 1 | 232/266 | 30/30 | 12471 / 117.032 | -1 / -1 / -1 | 0 / 0 / 0 | 580603904 / 602865664 / 602865664 |
| SyntheticMovers / 500 / Conquest / aven / 0 / 1 | 518 / 16 | 3 / 2 / 4 / 1 | 226/261 | 30/30 | 13465 / 174.375 | -1 / -1 / -1 | 0 / 0 / 0 | 605896704 / 632733696 / 632733696 |
| SyntheticMovers / 500 / Conquest / aven / 0 / 2 | 518 / 16 | 3 / 2 / 4 / 1 | 226/261 | 30/30 | 13465 / 180.276 | -1 / -1 / -1 | 1 / 1 / 1 | 636084224 / 662208512 / 572465152 |
| ShippedEarly / 0 / Conquest / aven / 0 / 1 | 17 / 24 | 0 / 0 / 0 / 0 | 51/51 | 0/0 | 3110 / 21.929 | -1 / -1 / -1 | 0 / 0 / 0 | 573939712 / 575578112 / 575578112 |
| ShippedEarly / 0 / Conquest / aven / 0 / 2 | 17 / 24 | 0 / 0 / 0 / 0 | 51/51 | 0/0 | 3110 / 21.622 | -1 / -1 / -1 | 0 / 0 / 0 | 576454656 / 578215936 / 578215936 |
| ShippedMid / 0 / Conquest / aven / 6000 / 1 | 44 / 24 | 1 / 1 / 0 / 0 | 271/271 | 0/0 | 7409 / 55.918 | -1 / -1 / -1 | 0 / 0 / 0 | 592314368 / 598835200 / 598835200 |
| ShippedMid / 0 / Conquest / aven / 6000 / 2 | 44 / 24 | 1 / 1 / 0 / 0 | 271/271 | 0/0 | 7409 / 57.087 | -1 / -1 / -1 | 0 / 0 / 0 | 621670400 / 629891072 / 629891072 |
| ShippedEarly / 0 / Conquest / serevin / 0 / 1 | 18 / 24 | 0 / 0 / 0 / 0 | 53/53 | 0/0 | 3172 / 19.765 | -1 / -1 / -1 | 0 / 0 / 0 | 631005184 / 633921536 / 633921536 |
| ShippedEarly / 0 / Conquest / serevin / 0 / 2 | 18 / 24 | 0 / 0 / 0 / 0 | 53/53 | 0/0 | 3172 / 19.693 | -1 / -1 / -1 | 0 / 0 / 0 | 634970112 / 637902848 / 637902848 |
| ShippedMid / 0 / Conquest / serevin / 6000 / 1 | 37 / 24 | 2 / 2 / 0 / 0 | 272/272 | 0/0 | 7846 / 73.383 | -1 / -1 / -1 | 1 / 1 / 1 | 659542016 / 664657920 / 573968384 |
| ShippedMid / 0 / Conquest / serevin / 6000 / 2 | 37 / 24 | 2 / 2 / 0 / 0 | 272/272 | 0/0 | 7846 / 63.172 | -1 / -1 / -1 | 0 / 0 / 0 | 585572352 / 591147008 / 591147008 |
| ShippedEarly / 0 / Dominion / aven / 0 / 1 | 17 / 24 | 0 / 0 / 0 / 0 | 51/51 | 0/0 | 3110 / 21.986 | -1 / -1 / -1 | 0 / 0 / 0 | 592252928 / 594644992 / 594644992 |
| ShippedEarly / 0 / Dominion / aven / 0 / 2 | 17 / 24 | 0 / 0 / 0 / 0 | 51/51 | 0/0 | 3110 / 23.357 | -1 / -1 / -1 | 0 / 0 / 0 | 595660800 / 598048768 / 598048768 |
| ShippedMid / 0 / Dominion / aven / 6000 / 1 | 39 / 24 | 2 / 1 / 0 / 1 | 170/170 | 0/0 | 9222 / 72.530 | -1 / -1 / -1 | 0 / 0 / 0 | 617598976 / 623484928 / 623484928 |
| ShippedMid / 0 / Dominion / aven / 6000 / 2 | 39 / 24 | 2 / 1 / 0 / 1 | 170/170 | 0/0 | 9222 / 72.498 | -1 / -1 / -1 | 0 / 0 / 0 | 643837952 / 649719808 / 649719808 |
| ShippedEarly / 0 / Dominion / serevin / 0 / 1 | 18 / 24 | 0 / 0 / 0 / 0 | 53/53 | 0/0 | 3172 / 19.746 | -1 / -1 / -1 | 0 / 0 / 0 | 650940416 / 653869056 / 653869056 |
| ShippedEarly / 0 / Dominion / serevin / 0 / 2 | 18 / 24 | 0 / 0 / 0 / 0 | 53/53 | 0/0 | 3172 / 19.698 | -1 / -1 / -1 | 0 / 0 / 0 | 655020032 / 657944576 / 657944576 |
| ShippedMid / 0 / Dominion / serevin / 6000 / 1 | 34 / 24 | 2 / 0 / 0 / 0 | 189/189 | 0/0 | 5405 / 40.764 | -1 / -1 / -1 | 0 / 0 / 0 | 578932736 / 581664768 / 581664768 |
| ShippedMid / 0 / Dominion / serevin / 6000 / 2 | 34 / 24 | 2 / 0 / 0 / 0 | 189/189 | 0/0 | 5405 / 45.850 | -1 / -1 / -1 | 0 / 0 / 0 | 593055744 / 596250624 / 596250624 |

Allocation capability: Unavailable: GC.GetAllocatedBytesForCurrentThread did not report at least4096 bytes for a retained 4096-byte array (or threw NotSupported/NotImplemented). Allocation fields are -1, never a zero-allocation claim. Probe bytes: 0.

| Synthetic movers | Rep | Group moves | Move p50 / max ms | Move path queries / visited cells |
| --- | --- | --- | --- | --- |
| 50 | 1 | 3 | 0.860 / 11.502 | 150 / 61050 |
| 50 | 2 | 3 | 0.850 / 0.936 | 150 / 61050 |
| 100 | 1 | 3 | 1.645 / 2.131 | 300 / 141880 |
| 100 | 2 | 3 | 1.655 / 1.975 | 300 / 141880 |
| 200 | 1 | 3 | 4.709 / 5.887 | 600 / 553023 |
| 200 | 2 | 3 | 4.713 / 6.050 | 600 / 553023 |
| 300 | 1 | 3 | 11.509 / 21.288 | 900 / 1124862 |
| 300 | 2 | 3 | 8.605 / 11.390 | 900 / 1124862 |
| 500 | 1 | 3 | 21.826 / 26.481 | 1500 / 2954145 |
| 500 | 2 | 3 | 24.616 / 26.661 | 1500 / 2954145 |

Navigation query count excludes connectivity rebuilds; navigation time includes them. Shared-field hits and recovery queries are subsets of path work, not extra independent queries. Script latency brackets Submit calls, excluding command object creation. AI timing includes its command object creation and submissions plus fixed command-event capture; trace serialization is excluded.

SHA256 of ordered public unit/building/player/resource/projectile/objective state and both visible/explored masks. This is an observable-state comparison, not a savegame or a proof that private path/work remainders match. Command traces include command tick, ordered payload and result, but exclude unmeasured natural warm-up commands. Start-state hashes detect a changed warm-up outcome. Compare before/after only with matching fixture parameters, shipped hashes, start/end hashes and trace hashes; timing differences alone do not demonstrate semantic equivalence.

Complete: True. Synthetic fixture valid (all requested movers survive, scripted commands accept, full window runs): True. Matching repetitions: True; verified pairs: 13. A single repetition verifies no pair. Runner wall seconds (all setup, warm-up and reporting included): 37.249. No performance budget or rendering/mobile pass is inferred from completing this CPU measurement.
