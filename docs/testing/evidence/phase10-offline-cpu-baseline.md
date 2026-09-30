# Offline CPU workload: baseline

UTC 2026-09-09T22:31:22.9205682Z; Unity 6000.3.23f1; runtime 4.0.30319.42000; Windows 11  (10.0.26200); Intel(R) Core(TM) i5-10400F CPU @ 2.90GHz (12 logical processors); system memory 16281MB. Graphics device reported by runtime: Null Device / Null; no graphics measurement is performed.

World and AI timing/allocation windows are disjoint. AI includes its legal command submissions; fixed script batches are separate. Navigation counters include all three windows. Observation, hashing, report construction and GC.GetTotalMemory(false) sampling are outside timed windows. GC collection/heap fields cover the whole sample loop, including diagnostics; heap deltas are not allocation totals. No forced collection, sleep, rendering, GPU or mobile-device measurement.

Unmeasured separate12-mover synthetic world advances1200ticks before any sampled case. Every measured case starts with a new World and fresh navigation caches. Shipped mid samples first advance their own AI/world to the requested start tick; that progression is excluded from timing windows.

Fixture `offline-cpu-v1`; Simulation+Diagnostics source SHA256 `12E70131B7841F9FEF54937DDFCD66034030C8B3EEF60600B18EE030ECCC7A3A`; editor runner SHA256 `9D5F00EB9400A5486B402C1C8D373775BD85B4948B6B8A7342082F5BC04BB0F3`.

Shipped rules SHA256 `6F100B25035E54E4144761F51885F52E45051EBD6562EF6024AFC37601CB63C2`; Amber Crossing map SHA256 `3EF7E44A71AE3C76C74C3DC04BD0BA7E8EFE43AA0BDC500AD2450487D493D072`.

Synthetic rows use a128x128m map and author N moving Aven Tenders, four other gatherers per side, one Threadkeeper and completed infrastructure, with 5000 of each resource/player and base population 1000. An11-cell solid buffer across z70..80 encloses the Serevin AI annex beyond attack range, preventing combat deaths from changing N. This is a noncombat scaling fixture with a deliberately unreachable opponent, not a complete match. Serevin AI and paid scripted Aven actions run under shipped rules/timers. It is not a normal start or a population/progression recommendation. Unrestricted shipped samples cover combat. The exact schedule and rejected script actions are in source/JSON.

Shipped rows use ordinary starts with both factions and both victory modes. Early sampling starts at0s; mid sampling at300s after ordinary AI progression. Requested windows: stress120s, shipped60s, repetitions2. A result ends sampling; unavailable/truncated windows remain explicit. Full-match pacing evidence comes from OfflineMatchVerification, not this runner.

| Fixture / mode / P1 faction | N movers | Rep | Start / sampled ticks | World p50 / p95 / p99 / max ms | AI think p50 / p95 / max ms | Script batch total / max ms | Units initial / peak / final | Window available / match ended |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| SyntheticMovers / Conquest / aven | 50 | 1 | 0 / 2400 | 0.925 / 1.291 / 1.544 / 5.722 | 0.290 / 0.976 / 2.555 | 8.922 / 3.765 | 59 / 93 / 93 | True / False |
| SyntheticMovers / Conquest / aven | 50 | 2 | 0 / 2400 | 0.924 / 1.275 / 1.492 / 1.969 | 0.297 / 1.102 / 12.113 | 8.529 / 3.600 | 59 / 93 / 93 | True / False |
| SyntheticMovers / Conquest / aven | 100 | 1 | 0 / 2400 | 1.148 / 1.497 / 1.830 / 6.342 | 0.325 / 1.009 / 3.599 | 12.247 / 5.136 | 109 / 143 / 143 | True / False |
| SyntheticMovers / Conquest / aven | 100 | 2 | 0 / 2400 | 1.155 / 1.529 / 1.847 / 2.508 | 0.327 / 1.026 / 3.546 | 12.310 / 5.235 | 109 / 143 / 143 | True / False |
| SyntheticMovers / Conquest / aven | 200 | 1 | 0 / 2400 | 1.610 / 2.048 / 2.394 / 2.962 | 0.404 / 1.209 / 7.672 | 24.705 / 10.221 | 209 / 243 / 243 | True / False |
| SyntheticMovers / Conquest / aven | 200 | 2 | 0 / 2400 | 1.616 / 2.309 / 3.299 / 43.828 | 0.413 / 1.320 / 10.993 | 25.027 / 10.266 | 209 / 243 / 243 | True / False |
| SyntheticMovers / Conquest / aven | 300 | 1 | 0 / 2400 | 2.078 / 2.639 / 3.214 / 7.757 | 0.490 / 1.370 / 13.126 | 40.153 / 16.636 | 309 / 343 / 343 | True / False |
| SyntheticMovers / Conquest / aven | 300 | 2 | 0 / 2400 | 2.093 / 2.812 / 3.606 / 7.981 | 0.508 / 1.365 / 13.334 | 40.784 / 17.061 | 309 / 343 / 343 | True / False |
| SyntheticMovers / Conquest / aven | 500 | 1 | 0 / 2400 | 3.031 / 3.783 / 4.972 / 7.356 | 0.616 / 1.758 / 29.383 | 84.837 / 33.787 | 509 / 543 / 543 | True / False |
| SyntheticMovers / Conquest / aven | 500 | 2 | 0 / 2400 | 3.006 / 3.765 / 4.993 / 12.545 | 0.618 / 1.725 / 28.640 | 84.275 / 33.862 | 509 / 543 / 543 | True / False |
| ShippedEarly / Conquest / aven | 0 | 1 | 0 / 1200 | 0.324 / 0.528 / 0.605 / 1.159 | 0.040 / 0.311 / 1.371 | 0.000 / 0.000 | 8 / 24 / 24 | True / False |
| ShippedEarly / Conquest / aven | 0 | 2 | 0 / 1200 | 0.316 / 0.448 / 0.502 / 0.720 | 0.029 / 0.320 / 1.366 | 0.000 / 0.000 | 8 / 24 / 24 | True / False |
| ShippedMid / Conquest / aven | 0 | 1 | 6000 / 1200 | 0.632 / 0.910 / 1.085 / 1.527 | 0.463 / 0.881 / 2.064 | 0.000 / 0.000 | 55 / 66 / 66 | True / False |
| ShippedMid / Conquest / aven | 0 | 2 | 6000 / 1200 | 0.630 / 0.928 / 1.073 / 1.242 | 0.481 / 0.969 / 14.087 | 0.000 / 0.000 | 55 / 66 / 66 | True / False |
| ShippedEarly / Conquest / serevin | 0 | 1 | 0 / 1200 | 0.318 / 0.449 / 0.552 / 0.711 | 0.030 / 0.317 / 1.417 | 0.000 / 0.000 | 8 / 24 / 24 | True / False |
| ShippedEarly / Conquest / serevin | 0 | 2 | 0 / 1200 | 0.317 / 0.438 / 0.532 / 0.710 | 0.030 / 0.294 / 1.373 | 0.000 / 0.000 | 8 / 24 / 24 | True / False |
| ShippedMid / Conquest / serevin | 0 | 1 | 6000 / 1200 | 0.625 / 0.956 / 1.189 / 2.800 | 0.437 / 0.846 / 2.142 | 0.000 / 0.000 | 50 / 60 / 60 | True / False |
| ShippedMid / Conquest / serevin | 0 | 2 | 6000 / 1200 | 0.619 / 0.937 / 1.148 / 1.586 | 0.436 / 0.852 / 2.137 | 0.000 / 0.000 | 50 / 60 / 60 | True / False |
| ShippedEarly / Dominion / aven | 0 | 1 | 0 / 1200 | 0.322 / 0.528 / 0.682 / 1.509 | 0.036 / 0.364 / 1.463 | 0.000 / 0.000 | 8 / 24 / 24 | True / False |
| ShippedEarly / Dominion / aven | 0 | 2 | 0 / 1200 | 0.319 / 0.445 / 0.524 / 1.028 | 0.030 / 0.338 / 1.368 | 0.000 / 0.000 | 8 / 24 / 24 | True / False |
| ShippedMid / Dominion / aven | 0 | 1 | 6000 / 1200 | 0.605 / 1.062 / 1.274 / 7.995 | 0.271 / 0.928 / 3.341 | 0.000 / 0.000 | 51 / 55 / 55 | True / False |
| ShippedMid / Dominion / aven | 0 | 2 | 6000 / 1200 | 0.606 / 1.019 / 1.172 / 1.552 | 0.246 / 0.882 / 1.831 | 0.000 / 0.000 | 51 / 55 / 55 | True / False |
| ShippedEarly / Dominion / serevin | 0 | 1 | 0 / 1200 | 0.317 / 0.442 / 0.513 / 0.729 | 0.028 / 0.352 / 1.375 | 0.000 / 0.000 | 8 / 24 / 24 | True / False |
| ShippedEarly / Dominion / serevin | 0 | 2 | 0 / 1200 | 0.326 / 0.470 / 0.550 / 0.861 | 0.033 / 0.355 / 1.885 | 0.000 / 0.000 | 8 / 24 / 24 | True / False |
| ShippedMid / Dominion / serevin | 0 | 1 | 6000 / 1200 | 0.572 / 0.771 / 0.973 / 2.073 | 0.245 / 0.631 / 1.679 | 0.000 / 0.000 | 50 / 52 / 52 | True / False |
| ShippedMid / Dominion / serevin | 0 | 2 | 6000 / 1200 | 0.574 / 0.807 / 1.016 / 2.513 | 0.260 / 0.671 / 1.803 | 0.000 / 0.000 | 50 / 52 / 52 | True / False |

| Fixture / N / mode / P1 / start / rep | Moving / gatherers peak | Active research / charters / affected units / boosted producers peak | AI accepted / commands | Script accepted / commands | Navigation queries / ms | Allocation bytes World / AI / script | GC0 / GC1 / GC2 | Heap before / peak / after bytes |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| SyntheticMovers / 50 / Conquest / aven / 0 / 1 | 68 / 16 | 3 / 2 / 4 / 1 | 229/263 | 30/30 | 11321 / 82.068 | -1 / -1 / -1 | 0 / 0 / 0 | 564797440 / 584605696 / 584605696 |
| SyntheticMovers / 50 / Conquest / aven / 0 / 2 | 68 / 16 | 3 / 2 / 4 / 1 | 229/263 | 30/30 | 11321 / 84.498 | -1 / -1 / -1 | 1 / 1 / 1 | 586907648 / 599822336 / 544919552 |
| SyntheticMovers / 100 / Conquest / aven / 0 / 1 | 119 / 16 | 3 / 2 / 4 / 1 | 237/272 | 30/30 | 11393 / 82.662 | -1 / -1 / -1 | 0 / 0 / 0 | 547176448 / 566292480 / 566292480 |
| SyntheticMovers / 100 / Conquest / aven / 0 / 2 | 119 / 16 | 3 / 2 / 4 / 1 | 237/272 | 30/30 | 11393 / 83.257 | -1 / -1 / -1 | 0 / 0 / 0 | 568606720 / 588582912 / 588582912 |
| SyntheticMovers / 200 / Conquest / aven / 0 / 1 | 219 / 16 | 3 / 2 / 4 / 1 | 228/262 | 30/30 | 11929 / 98.043 | -1 / -1 / -1 | 0 / 0 / 0 | 590999552 / 612184064 / 612184064 |
| SyntheticMovers / 200 / Conquest / aven / 0 / 2 | 219 / 16 | 3 / 2 / 4 / 1 | 228/262 | 30/30 | 11929 / 103.195 | -1 / -1 / -1 | 1 / 1 / 1 | 614641664 / 627363840 / 548401152 |
| SyntheticMovers / 300 / Conquest / aven / 0 / 1 | 318 / 16 | 3 / 2 / 4 / 1 | 232/266 | 30/30 | 12471 / 120.265 | -1 / -1 / -1 | 0 / 0 / 0 | 550907904 / 572547072 / 572547072 |
| SyntheticMovers / 300 / Conquest / aven / 0 / 2 | 318 / 16 | 3 / 2 / 4 / 1 | 232/266 | 30/30 | 12471 / 122.345 | -1 / -1 / -1 | 0 / 0 / 0 | 575119360 / 598470656 / 598470656 |
| SyntheticMovers / 500 / Conquest / aven / 0 / 1 | 518 / 16 | 3 / 2 / 4 / 1 | 226/261 | 30/30 | 13465 / 177.376 | -1 / -1 / -1 | 0 / 0 / 0 | 601481216 / 628285440 / 628285440 |
| SyntheticMovers / 500 / Conquest / aven / 0 / 2 | 518 / 16 | 3 / 2 / 4 / 1 | 226/261 | 30/30 | 13465 / 177.165 | -1 / -1 / -1 | 0 / 0 / 0 | 545157120 / 567377920 / 567377920 |
| ShippedEarly / 0 / Conquest / aven / 0 / 1 | 17 / 24 | 0 / 0 / 0 / 0 | 51/51 | 0/0 | 3110 / 23.115 | -1 / -1 / -1 | 0 / 0 / 0 | 568926208 / 571260928 / 571260928 |
| ShippedEarly / 0 / Conquest / aven / 0 / 2 | 17 / 24 | 0 / 0 / 0 / 0 | 51/51 | 0/0 | 3110 / 21.790 | -1 / -1 / -1 | 0 / 0 / 0 | 572219392 / 574599168 / 574599168 |
| ShippedMid / 0 / Conquest / aven / 6000 / 1 | 44 / 24 | 1 / 1 / 0 / 0 | 271/271 | 0/0 | 7409 / 55.762 | -1 / -1 / -1 | 0 / 0 / 0 | 592224256 / 598765568 / 598765568 |
| ShippedMid / 0 / Conquest / aven / 6000 / 2 | 44 / 24 | 1 / 1 / 0 / 0 | 271/271 | 0/0 | 7409 / 58.408 | -1 / -1 / -1 | 1 / 1 / 1 | 618811392 / 624599040 / 546107392 |
| ShippedEarly / 0 / Conquest / serevin / 0 / 1 | 18 / 24 | 0 / 0 / 0 / 0 | 53/53 | 0/0 | 3172 / 20.346 | -1 / -1 / -1 | 0 / 0 / 0 | 547106816 / 548851712 / 548851712 |
| ShippedEarly / 0 / Conquest / serevin / 0 / 2 | 18 / 24 | 0 / 0 / 0 / 0 | 53/53 | 0/0 | 3172 / 20.031 | -1 / -1 / -1 | 0 / 0 / 0 | 549785600 / 551825408 / 551825408 |
| ShippedMid / 0 / Conquest / serevin / 6000 / 1 | 37 / 24 | 2 / 2 / 0 / 0 | 272/272 | 0/0 | 7846 / 64.523 | -1 / -1 / -1 | 0 / 0 / 0 | 566386688 / 571944960 / 571944960 |
| ShippedMid / 0 / Conquest / serevin / 6000 / 2 | 37 / 24 | 2 / 2 / 0 / 0 | 272/272 | 0/0 | 7846 / 63.892 | -1 / -1 / -1 | 0 / 0 / 0 | 588128256 / 595345408 / 595345408 |
| ShippedEarly / 0 / Dominion / aven / 0 / 1 | 17 / 24 | 0 / 0 / 0 / 0 | 51/51 | 0/0 | 3110 / 22.993 | -1 / -1 / -1 | 0 / 0 / 0 | 596475904 / 599379968 / 599379968 |
| ShippedEarly / 0 / Dominion / aven / 0 / 2 | 17 / 24 | 0 / 0 / 0 / 0 | 51/51 | 0/0 | 3110 / 21.832 | -1 / -1 / -1 | 0 / 0 / 0 | 600420352 / 603316224 / 603316224 |
| ShippedMid / 0 / Dominion / aven / 6000 / 1 | 39 / 24 | 2 / 1 / 0 / 1 | 170/170 | 0/0 | 9222 / 88.559 | -1 / -1 / -1 | 1 / 1 / 1 | 623529984 / 627097600 / 547049472 |
| ShippedMid / 0 / Dominion / aven / 6000 / 2 | 39 / 24 | 2 / 1 / 0 / 1 | 170/170 | 0/0 | 9222 / 72.357 | -1 / -1 / -1 | 0 / 0 / 0 | 560947200 / 565354496 / 565354496 |
| ShippedEarly / 0 / Dominion / serevin / 0 / 1 | 18 / 24 | 0 / 0 / 0 / 0 | 53/53 | 0/0 | 3172 / 20.005 | -1 / -1 / -1 | 0 / 0 / 0 | 566460416 / 568856576 / 568856576 |
| ShippedEarly / 0 / Dominion / serevin / 0 / 2 | 18 / 24 | 0 / 0 / 0 / 0 | 53/53 | 0/0 | 3172 / 20.970 | -1 / -1 / -1 | 0 / 0 / 0 | 569888768 / 572297216 / 572297216 |
| ShippedMid / 0 / Dominion / serevin / 6000 / 1 | 34 / 24 | 2 / 0 / 0 / 0 | 189/189 | 0/0 | 5405 / 41.581 | -1 / -1 / -1 | 0 / 0 / 0 | 583626752 / 586502144 / 586502144 |
| ShippedMid / 0 / Dominion / serevin / 6000 / 2 | 34 / 24 | 2 / 0 / 0 / 0 | 189/189 | 0/0 | 5405 / 42.081 | -1 / -1 / -1 | 0 / 0 / 0 | 599871488 / 603992064 / 603992064 |

Allocation capability: Unavailable: GC.GetAllocatedBytesForCurrentThread did not report at least4096 bytes for a retained 4096-byte array (or threw NotSupported/NotImplemented). Allocation fields are -1, never a zero-allocation claim. Probe bytes: 0.

| Synthetic movers | Rep | Group moves | Move p50 / max ms | Move path queries / visited cells |
| --- | --- | --- | --- | --- |
| 50 | 1 | 3 | 0.998 / 1.001 | 150 / 61050 |
| 50 | 2 | 3 | 1.061 / 1.062 | 150 / 61050 |
| 100 | 1 | 3 | 1.881 / 2.152 | 300 / 141880 |
| 100 | 2 | 3 | 1.834 / 2.226 | 300 / 141880 |
| 200 | 1 | 3 | 5.008 / 6.291 | 600 / 553023 |
| 200 | 2 | 3 | 5.047 / 6.347 | 600 / 553023 |
| 300 | 1 | 3 | 8.916 / 11.676 | 900 / 1124862 |
| 300 | 2 | 3 | 8.995 / 12.043 | 900 / 1124862 |
| 500 | 1 | 3 | 22.305 / 26.975 | 1500 / 2954145 |
| 500 | 2 | 3 | 21.958 / 27.103 | 1500 / 2954145 |

Navigation query count excludes connectivity rebuilds; navigation time includes them. Shared-field hits and recovery queries are subsets of path work, not extra independent queries. Script latency brackets Submit calls, excluding command object creation. AI timing includes its command object creation and submissions plus fixed command-event capture; trace serialization is excluded.

SHA256 of ordered public unit/building/player/resource/projectile/objective state and both visible/explored masks. This is an observable-state comparison, not a savegame or a proof that private path/work remainders match. Command traces include command tick, ordered payload and result, but exclude unmeasured natural warm-up commands. Start-state hashes detect a changed warm-up outcome. Compare before/after only with matching fixture parameters, shipped hashes, start/end hashes and trace hashes; timing differences alone do not demonstrate semantic equivalence.

Complete: True. Synthetic fixture valid (all requested movers survive, scripted commands accept, full window runs): True. Matching repetitions: True; verified pairs: 13. A single repetition verifies no pair. Runner wall seconds (all setup, warm-up and reporting included): 82.664. No performance budget or rendering/mobile pass is inferred from completing this CPU measurement.
