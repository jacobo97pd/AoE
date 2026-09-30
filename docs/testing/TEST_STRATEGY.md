# Test strategy

## Phases 7–10 current verification

The final optimized source passes **347 EditMode + 66 PlayMode = 413 tests**, retaining the 322 Phase 6 cases and adding 91. Evidence: [EditMode](evidence/phase10-editmode.xml), [PlayMode](evidence/phase10-playmode.xml). New cases cover offline opt-in/definition validation, fog information boundaries, legal visibility-gated orders, capture/contest/hold timing, all result types/frozen state, honest AI economy/research/objective priorities, JSON compatibility, same-ID hidden relocation, gestures/lifecycle cancellation, safe-area geometry, minimap observations, shared art LOD/team data, cosmetic authority separation and hidden-period feedback. Final regressions exercise current-selection role counts/filtering (including Ashrunner actions) with frontmost UI raycasts, and a real projectile killing an enemy after it leaves sight between presentation updates. Five independent cell-union oracle tests exercise the optimized fog against movement, construction, production, death, relocation and radius changes.

The [12 common](evidence/phase10-combat-balance.md) and [eight faction](evidence/phase10-faction-counters.md) probes pass. All [30 movement cases](evidence/phase10-movement.md) ran and required gates pass; the known 200/300/500 narrow-choke completion failures remain reported. No assertion was removed to conceal a gameplay failure. A UI fixture now rebuilds the actual grid controllers; team-mask roundtrips use a 1e-6 per-channel float tolerance while retaining distinct ownership and shared-asset assertions.

[Four natural offline matches](evidence/phase7-offline-matches.md) start from authored stock and physical gathering. They exercise both faction assignments and both modes; Dominion must actually win through beacons. AI traces/progression are useful pacing proxies, not human fun or competitive balance. The [pre-tuning outcomes](evidence/phase7-offline-before-tuning.md) remain labeled as earlier results.

`Smoke-Player.ps1 -Scenario Offline` tests the packaged chooser/pause/result/restart path with a local ordinary-command AI driver and actual raycast-checked buttons. It accelerates ticks and captures intermediate views. `-Scenario ArtReview` stages all eleven art identities in both team colors; gallery population is not a match start. `Profile-OfflinePlayer.ps1` is separate: a fixed natural checkpoint, exactly 400 ticks over 20 seconds, explicit URP/Canvas rendering plus one-pixel GPU synchronization, recorder counters and capability-checked GPU/allocation telemetry. `Compare-OfflinePerformance.ps1` refuses timing comparisons if protocol, observed states, fog masks or command traces differ.

The [Android readiness report](evidence/phase8-android-readiness.json) records missing module/SDK/NDK/JDK and no device. Queued desktop touch events and safe-area layouts do not establish physical comfort, thermal behaviour or mobile FPS. Final packaged and performance evidence is indexed by [current status](../tasks/CURRENT_PHASE.md) and the four phase reports.

## Evidence policy

Tests are required for authoritative game rules and meaningful Unity integration. The plan below is not a list of tests already passing. `../tasks/CURRENT_PHASE.md` and phase reports record actual test counts, failures, build artifacts and limitations. Preserve failures; never delete or disable a failing test merely to obtain green output.

Use Unity Test Framework **1.6.0** with separate EditMode and PlayMode assemblies in the working Unity **6000.3.23f1** installation. The initial 6000.6 installation could not start Package Manager; the audit documents the fallback to an already installed complete editor. Simulation tests reference the plain C# assembly. Presentation integration tests may reference Unity and Input System. No external service is required.

## Phase 1 acceptance

EditMode tests should validate definition/world bounds, stable entity IDs, illegal-order rejection, ownership checks, movement progress, arrival and bounded coordinates. Verify rejected commands do not partially mutate state. Include extreme/invalid numeric input where the public API permits it.

PlayMode tests should load or compose the greybox scene, advance real frames, submit an order and assert presentation reflects authoritative movement. Verify camera bounds/zoom and entity-selection integration where practical. Automated pointer/gesture tests should exercise the gesture state machine and HUD exclusion, rather than claiming actual device usability.

A manual/editor smoke check must prove the player can select a worker and move it. `MobileInputTest` should make pan, pinch/scroll zoom, single/multiple selection, orders and building interaction easy to inspect. Test phone/tablet aspect ratios and safe areas separately from mouse behavior. A passing integration test does not establish comfortable touch control on hardware.

## Phase 2 verified checkpoint and continuing coverage

Phase 1 recorded 33 passing EditMode tests and 10 passing PlayMode tests. Phase 2 received a fresh clean compilation, **86/86 passing EditMode tests**, **14/14 passing PlayMode tests**, and a rebuilt Windows development player with **zero errors and zero warnings**, sized **162,884,454 bytes**. The [Phase 2 report](../tasks/PHASE_2_REPORT.md) records evidence and the [current phase ledger](../tasks/CURRENT_PHASE.md) owns the latest results. These counts describe the executed suites, not every future coverage requirement below.

The verified economy acceptance path is: select a Tender, gather and deliver resources, preview and confirm a Muster Hall, complete construction, select the finished hall, queue a Reedguard, and see the unit spawn. Checks exercise presentation command adapters as well as the simulation API. Maintain coverage that rejected/overlapping placements spend nothing, a successful confirmation charges once, cancelling a preview changes no rules state, and repeated confirmation cannot create duplicate foundations.

EditMode coverage should include all four resource types, carry limits, partial final harvests, deposit accounting, exhausted sources, reassignment while carrying, invalid ownership, interrupted construction, population reservation/release, multiple queued units, insufficient resources, capped population and blocked production exits. Adding a foundation must update navigation safely for workers and existing moving units. Document the intended depleted-node footprint behavior so presentation and routing agree.

PlayMode coverage should include resource tapping with workers selected, foundation visual creation/progress, selecting an unfinished building to resume construction, placement-mode cancellation, complete-building contextual training, rally commands, population/queue HUD refresh and visual creation for trained units. Include invalid rally/placement feedback and context changes rather than testing only direct method success.

The final run includes the manual-cargo addition. Its three EditMode regressions cover stopped final cargo after all sources deplete, rejecting a foreign drop-off without changing assignments/inventory, and preserving empty selected workers' movement during a carrier's delivery. The added PlayMode test taps a completed own drop-off with a stopped carrier selected and verifies physical delivery, inventory credit on arrival, preserved selection and an idle finish. The tests establish command/tap-adapter behavior; directly clicking the HUD's Deliver button through pointer event routing remains a separate usability check.

The opt-in development-player economy smokes passed at **1440×1080, tick 1043**, and **1280×720, tick 1042**, on an **RTX 3060 Ti**. Both recorded **17 entities**, `Gathered/Built/Trained: True`, and population **5 used + 0 reserved / 6 capacity**. Both used accelerated economy ticks. Rendered screenshots were inspected and the HUD was readable; runtime player logs had no errors. This verifies functional integration, not real-time economy balance, match duration or mobile performance. The phone viewport remains tight, physical touch usability is unvalidated, and no direct HUD-button click coverage is implied.

## Verified Phase 3 checkpoint

Final verification passed **141/141 EditMode tests**, **19/19 PlayMode tests** and **12/12 separate counter scenarios against the actual authored JSON**. This includes **55 new pure combat cases** and **5 new PlayMode integration tests** beyond Phase 2. The final PlayMode suite was rerun successfully after camera/map-margin adjustments. The [Phase 3 report](../tasks/PHASE_3_REPORT.md) records the XML, build summary, counter report and rendered-player evidence; the [current phase ledger](../tasks/CURRENT_PHASE.md) owns the latest status.

Pure combat cases cover definition bounds, atomic attack ownership/target validation, repeated-order cooldowns, edge-to-edge unit/building range, armor/minimum damage, highest matching bonus, projectile delay/homing/dead source/dead target/expiry, nearest reachable acquisition and ID ties, movement/chase/stop/worker interruption, population and cargo on death, destroyed producer reservations/capacity/navigation, and construction preserving prior damage. Identical lethal melee units must both die when their attacks are due in the same tick, including swapped owners/IDs. Previously flying projectiles still resolve before new actor decisions.

Controlled rule fixtures deliberately use simple values and passive targets. Separate shipped-balance fixtures mirror the three authored military definitions: 80 total resources, one population and 160 training ticks each. The editor counter probe loaded the actual JSON, hashed its content, exercised each favored pairing at 1:1 and 1:2 headcounts, and swapped player/spawn sides; **all 12 scenarios passed**. It waited for outstanding projectiles before choosing a winner. Equal summed resources are a nominal comparison; different resource types retain different economic value. These bounded results do not prove balance across maps, formations, upgrades or sustained play.

The final Windows development build succeeded with **zero errors/warnings**, **162,933,015 bytes** and a build duration of **12.9132761 seconds**. Both rebuilt-player combat smokes (**1440×1080**, **1280×720**) passed all three role attacks, actual projectile damage, three deaths and visual removal. They finished at **tick 107 with 25 entities**; their inspected tick-35 captures showed 28 entities with readable HUDs and clear combat views.

The **1440×1080** first-loop smoke passed select–move–gather–build–train–fight, including a trained Reedguard killing an enemy worker. It finished at **tick 1413 with 16 entities**, population **5+0/6**, and `TrainedSoldierFought: True`. Before-fight and after-fight screenshots were inspected; runtime player logs had no errors. Tests and smokes ran on an **RTX 3060 Ti** with accelerated ticks. These results are functional integration evidence, not FPS benchmarks, real-time combat tuning, a physical touch session or proof of clicking every contextual HUD button.

## Verified Phase 4 checkpoint

Final rules/integration verification passed **160/160 EditMode tests**, **23/23 PlayMode tests**, and **12/12 shipped-JSON counter probes**. The 19 added EditMode cases cover group movement and formations; four added PlayMode cases cover stress composition and formation selection flowing through a terrain tap. Coverage includes continuous swept pair/static clearance, speed limits, stable replay/selection ordering, hard Stop, dynamic footprints, worker access, repeated moves after combat pursuit, occupied rally destinations and legal formation fallback. Earlier movement fixtures were corrected for physical circle clearance and unobstructed speed lanes; behavioral assertions remain active.

The **30-case, 120-simulated-second** matrix passed its required coverage and normal 100-unit arrival gates. All 50–500 OpenField/WideCorridor/CrossingGroups/DynamicObstacle cases completed without observed overlap or static/map violations. NarrowChoke 200/300/500 failed arrival acceptance and remain reported failures, although the required normal-scenario gate passed. Unreachable orders rejected atomically. Arrival requires Idle within 100 mm of the assigned goal; immobility means less than 50 mm progress over 200 ticks and does not detect circulation. The observer samples endpoints every tick; separate analytic tests cover between-tick swept collisions.

Actual baseline, pre-cache checkpoint and final artifacts are preserved. This Unity Mono allocation counter returned zero for a retained 4096-byte allocation, so final fields use **-1 / unavailable** and the historical baseline's zero values are invalid measurements. Do not interpret them as allocation-free ticks.

The Windows player passed the economy-to-combat loop and combat smokes at 1280×720 and 1440×1080. Those accelerated functional checks are separate from six normal-time rendered probes: 50/100/200/300/500 at 1280×720 and 100 at 1440×1080. The latter include explicit URP offscreen rendering, Canvas work and a synchronous one-pixel readback every loop. Unique sampled camera callbacks, real pixels and simulation/wall-time ratio are required, preventing CPU-only loop speed from being reported as rendered performance. They measure serialized desktop throughput, with 3 s frame warmup and 20 s sampling, not display presentation latency, isolated GPU time or mobile performance. An initial black-output probe is retained as rejected evidence.

See the [Phase 4 report](../tasks/PHASE_4_REPORT.md) for exact historical results/build provenance and [movement stress guide](MOVEMENT_STRESS.md) for fixtures, scripts and acceptance semantics. Phase 4 source-test results preceded an opt-in rendering-probe correction, which the six actual player measurements verified. The Phase 4 distribution build differed from that measured build only in non-stress formation-label fitting and diagnostic comments; fresh functional captures verified the label. Authoritative simulation and the measured stress/render path were unchanged between those two Phase 4 builds. This does not establish performance of subsequent Phase 5 changes.

## Verified Phase 5 checkpoint

Final Phase 5 verification passed [217/217 EditMode tests](evidence/phase5-editmode.xml), [26/26 PlayMode tests](evidence/phase5-playmode.xml), [12/12 authored-JSON counter checks](evidence/phase5-combat-balance.md) and the required gates in the [30-case movement matrix](evidence/phase5-movement.md). The additions are 57 pure technology cases and three PlayMode tests. The 26-test PlayMode suite passed again after viewport-width and opaque research-panel fixes. A later opt-in smoke-only focus adjustment was covered by the final Windows build and rendered players. The [Phase 5 report](../tasks/PHASE_5_REPORT.md) records delivery and provenance.

Technology rules cover atomic ownership/producer/funds checks, training/research exclusion, duplicate research across buildings, one-time costs, exact completion ticks, all four eras, prerequisite and unlock gates, malformed/cyclic/overflowing definitions, producer destruction and retry, completed-effect persistence, player isolation, effects on existing/new units, gathering carry limits and projectile launch snapshots. Phase 5's tick order was movement, economy, construction, combat, production, research, then tick increment. Due combat uses pre-completion stats; a unit produced that tick receives completed effects by the tick's end. PlayMode coverage checks the actual configuration, modal/producer/progress state, disabled research actions and completion notices independent of sorted technology IDs.

Movement arrival outcomes matched Phase 4: all OpenField/WideCorridor/CrossingGroups/DynamicObstacle cases at 50–500 movers finished, with zero observed overlap/static/map violations across all 30 cases. NarrowChoke 200/300/500 still reached only 31/52/37 movers at the 120-second deadline. Those persistent-congestion failures remain visible despite passing the required normal-case gates. The [Phase 5 movement JSON](evidence/phase5-movement.json) retains CPU timing and unavailable allocation fields; the fixture does not exercise research completion.

The final [Windows development build](evidence/phase5-build.txt), GUID `48c0b4723fb54362a3697c9027469896`, succeeded with zero errors/warnings and 163,094,640 bytes. Technology smokes at [1280×720](evidence/phase5-technology-1280x720.txt) and [1440×1080](evidence/phase5-technology-1440x1080.txt) reached Empire with all nine technologies at tick 16,338. Each records nine synthetic pointer-click dispatches only after programmatic scrolling and a top-hit raycast confirm the button is visible and reachable. Stock starts unmodified; workers physically harvest and deliver the funds. Paid research, early locks, existing/new troop upgrades and enemy isolation all passed. The script advances accelerated 100-tick wait-loop batches, so its ticks/yielded frames are not wall-clock playtime, measured FPS or evidence of physical touch/scroll comfort.

All Phase 4 desktop frame measurements in this document and the [movement guide](MOVEMENT_STRESS.md) remain historical. Phase 5 has no rendered FPS result, allocation measurement or profile of research-completion stat-cache rebuilding. Inspect the retained [phone research capture](evidence/phase5-research-top-1280x720.png), [tablet research capture](evidence/phase5-research-top-1440x1080.png) and phase report for the functional presentation evidence.

## Verified Phase 6 checkpoint

The current suite passed [290/290 EditMode tests](evidence/phase6-editmode.xml), [32/32 PlayMode tests](evidence/phase6-playmode.xml), [12/12 common shipped-JSON counter cases](evidence/phase6-combat-balance.md), [8/8 faction counter cases](evidence/phase6-faction-counters.md) and the required gates in the [30-case neutral movement matrix](evidence/phase6-movement.md). This adds 73 pure faction cases and six integration tests, including regressions for grouped Ashrunner actions and a transport dying during deployment preview. The final [Windows build](evidence/phase6-build.txt), GUID `3a4d191876ae44fba8e0e681bfbdeaa0`, succeeded with zero errors/warnings, 163,206,372 bytes and a 13.7569182-second build duration. All nine packaged functional checks passed; the [Phase 6 report](../tasks/PHASE_6_REPORT.md) owns final artifact evidence.

Faction rule coverage includes immutable player assignment, equal starting budgets, faction locks and invalid definitions, owner-specific current/future unit passives, paid exact-time charter changes, training/research exclusion, downtime and nonstacking influence, retained over-capacity cargo, timed immobile Threadkeeper relays and lost-yard cooldowns. Relocation tests verify the already-paid outpost becomes a vulnerable normal-moving population-zero transport under the same entity ID, preserves damage and population, disables utility, waits on blocked deployment, cancels through Stop and cannot resurrect after destruction. Reposition has a bounded speed window, suppresses attacks, preserves spear counters and starts its full cooldown after expiry. Unique technology tests cover owner gates, effects, increased charter reach and captured packing/deployment durations; research cannot retroactively shorten an accepted action.

At the Phase 6 checkpoint, tick order was movement, economy, construction, combat, faction timers/completions, production, research, then tick increment. Influence refreshes before and after movement, after faction completions and after research. Due combat resolves before a relocation completes. PlayMode checks use authored data for both faction choices, modal pointer exclusion, visible charter/relay controls, same-ID visual replacement and selection, relocation of an already-paid outpost with no second payment, mixed-group rejection and preview cleanup after death. The 32-test PlayMode suite passed again after the final description-layout and opt-in smoke movement corrections. Core rules remained unchanged after the complete gate; the final build and all nine packaged checks cover those last presentation/probe changes.

The eight faction counter cases assign Aven to Reedguards/Stringwardens and Serevin to Ashrunners, with ownership/spawn sides mirrored, 7 m fronts and a 90-second deadline including projectile flight. With faction passives but no research or activated abilities, a Reedguard wins one-on-one in 5.40 s with 64 health; an Ashrunner beats a Stringwarden in 6.95 s with 25 health. Two Ashrunners beat one Reedguard in 9.10 s; two Stringwardens beat one Ashrunner in 4.35 s. All initial orders are accepted and all projectiles finish. The Ashrunner costs 100 nominal resources versus 80 for either common role: these outcomes establish bounded counter behavior, not competitive faction balance.

The neutral movement rerun preserves earlier arrival outcomes and zero observed overlap/static/map violations across all 30 cases. OpenField/WideCorridor/CrossingGroups/DynamicObstacle finish at every 50–500 count; NarrowChoke 200/300/500 still reach only 31/52/37 movers by 120 seconds. Required normal-case gates pass while those persistent-congestion failures remain unresolved. The [JSON](evidence/phase6-movement.json) records source SHA256 `CEB28A978BF89C869B884BF60C3B2427CDFF91FEA78D752A993166965B66460A` and unavailable allocation fields. Only neutral CPU work was measured: no active faction influence, faction renderer, research-completion, allocations or mobile performance was profiled. Phase 4 rendered timings remain historical.

| Final packaged check | Evidence | Result |
| --- | --- | --- |
| Aven faction drill | [1280×720](evidence/phase6-aven-1280x720.txt), [1440×1080](evidence/phase6-aven-1440x1080.txt) | Both passed at tick 4,035, 44 yielded frames and nine visible synthetic button dispatches. |
| Serevin faction drill | [1280×720](evidence/phase6-serevin-1280x720.txt), [1440×1080](evidence/phase6-serevin-1440x1080.txt) | Both passed at tick 3,844, 43 yielded frames and twelve dispatches; damaged outpost retained 439/450 health through two relocations. |
| Technology | [1280×720](evidence/phase6-technology-1280x720.txt), [1440×1080](evidence/phase6-technology-1440x1080.txt) | Both reached Empire with all nine researches at tick 16,338, 167 yielded frames and nine visible raycast-checked research dispatches. |
| Common combat | [1280×720](evidence/phase6-combat-1280x720.txt), [1440×1080](evidence/phase6-combat-1440x1080.txt) | Both passed all three role attacks, damage/projectiles, three deaths and view removal at tick 123 with 25 entities. |
| Economy-to-combat loop | [1440×1080](evidence/phase6-economy-1440x1080.txt) | Gather/build/train/fight passed at tick 1,312 with 16 entities and population 5+0/6. |

Both faction drills start with equal unmodified budgets and physically gather their funds. Each performs two scene-reloading chooser dispatches, proving explicit choice overrides the CLI faction, then verifies foreign-unique rejection, faction mechanics, unique actions and unique research. The listed dispatch totals include those two chooser actions; button centers must pass the UI raycast check before synthetic click dispatch. Research scrolling and simulation advancement are programmatic. The faction and technology wait loops advance up to 100 ticks between yielded frames; these RTX 3060 Ti functional runs establish no wall-clock match duration, rendered FPS, physical touch usability or mobile performance.

## Continuing and later phase gates

| Phase/system | Required meaningful checks |
| --- | --- |
| Economy | Four-resource conservation, purchase atomicity, insufficient funds, costs deducted once |
| Workers | Harvest stock, carry capacity, deposit, reassignment and missing-node/drop-off behavior |
| Construction | Invalid placement, charged cost, completion once, destruction/cancellation policy |
| Production | Queue ordering, population reservations/cap, spawn validity, interruption/refunds |
| Combat | Range/cooldown, armor, soft-counter tags, projectiles, death/removal once |
| Movement | Swept clearance, bounded speed, stable orders/formations, Stop, dynamic footprints, normal-crowd completion and explicitly reported choke failures |
| Technology | Era/prerequisite checks, costs, no duplicate research, all four eras reachable |
| Factions | Owner locks/passives, paid charter timing and exclusion, nonstacking relays, same-ID damaged relocation, blocked completion/destruction, temporary ability/cooldown gates |
| Fog/victory | Unexplored/explored/visible transitions, hidden-enemy presentation, conquest/objective result once |
| AI | Only legal commands, bounded decisions, completion of a seeded match |
| Replay/network | Serialization, duplicate/out-of-order rejection, hashes across runtimes, reconnect, authoritative result idempotency |

All systems must reject or avoid invalid numerical states. Integer positions do not remove the need for overflow/range tests. Future floating-point presentation/interpolation must avoid NaN/Infinity.

## Running verification

From the repository root:

```powershell
powershell -NoProfile -File .\tools\Verify-Unity.ps1
powershell -NoProfile -File .\tools\Verify-Unity.ps1 -Stage EditMode
powershell -NoProfile -File .\tools\Verify-Unity.ps1 -Stage PlayMode
powershell -NoProfile -File .\tools\Verify-Unity.ps1 -Stage Factions
powershell -NoProfile -File .\tools\Verify-Unity.ps1 -Stage Movement
powershell -NoProfile -File .\tools\Build-Unity.ps1 -Target Windows
powershell -NoProfile -File .\tools\Profile-MovementPlayer.ps1
```

The scripts accept `-UnityPath` and `-ProjectPath` overrides, create ignored `TestResults/` logs and XML, run editor processes hidden, and return nonzero on failures/timeouts. Tests omit `-quit`, letting the test runner finish and exit. A missing/empty XML file, failed result, zero executed tests or compiler errors fails verification even if the process exits zero. Close an editor already using the same project before batch execution.

Use `-Target Android` only once this exact editor has Android Build Support and a compatible SDK/JDK/NDK configuration. Initial Android preparation is not APK verification. iOS needs macOS/Xcode for final build/signing.

## Performance and review

Run the benchmark matrix in `../technical/PATHFINDING_ARCHITECTURE.md` and `../technical/PERFORMANCE_BUDGET.md`. Do not treat performance measurements as deterministic unit tests; preserve hardware/build/scenario metadata. Inspect editor logs at checkpoints, summarize actual outcomes in the phase ledger, and broaden testing only when changes or unresolved failures warrant it.
