# Phase 5 — Eras and technology

**Status: complete.** Players can progress from Settlement through Kingdom and Dominion to Empire using gathered resources, completed buildings and paid, timed research. Nine research items, the Archive, prerequisite gates and a responsive research tree are implemented. Verification passed **217 EditMode tests, 26 PlayMode tests, 12 combat counter probes, the required gates of the 30-case movement matrix, a Windows build and five packaged-player functional checks**. Phase 6 has not started.

## Baseline and environment

Work continues from Phase 4 commit `5678253` on the existing project. Unity **6000.3.23f1** at `D:\Unity\Editors\6000.3.23f1\Editor\Unity.exe` remains the working editor, with URP 17.3.0, Input System 1.20.0, Test Framework 1.6.0 and uGUI 2.0.0. No editor, package or platform module was installed or changed. The original incomplete 6000.6 installation remains documented in the [environment audit](../audits/initial-project-audit.md).

Verification used Windows 11 (10.0.26200), an Intel i5-10400F at 2.90 GHz (6 cores / 12 threads), 16,281 MB system memory and an NVIDIA GeForce RTX 3060 Ti reporting 8,024 MB graphics memory. Android/iOS modules are absent. No physical mobile session, APK or signed iOS application was produced.

The supplied reference PDF remains unchanged: SHA256 `778295A57495B1F67CC9E42A7F8F01932EF5841F6491FB72E84F55D77BCAEDF7`. Its [design lessons](../design/REFERENCE_DESIGN_LESSONS.md) inform the resource-versus-timing choices; game content and primitive visuals are original.

## Implemented behavior

All players begin in **Settlement**. The existing three military roles remain available there. Research unlocks the Archive and successive economic/equipment tiers; no additional military roster was introduced. All existing unit/building balance, resource nodes, starting inventory, population and queue values are preserved. The active [technology tree](../design/TECHNOLOGY_TREE.md) records all numeric tuning.

| Advance at the Hearth | Prerequisites | Cost | Research time at 20 Hz |
| --- | --- | --- | --- |
| Kingdom | Settlement | 200 Food, 100 Wood | 600 ticks / 30 seconds |
| Dominion | Kingdom; own a completed Archive | 400 Food, 200 Wood, 100 Metal | 900 ticks / 45 seconds |
| Empire | Dominion; Work Rotation complete | 600 Food, 300 Metal, 200 Stone | 1,200 ticks / 60 seconds |

The **Archive** requires Kingdom and costs 120 Wood / 40 Stone. Its 3×3 footprint, 700 health, 2 armor and 240 construction-work ticks use the existing placement and construction systems. It has no housing, drop-off or training function. Better Handles and Work Rotation raise a Tender's gather amount from **2 → 3 → 4** per operation, preserving cargo capacity, harvesting interval, finite stock and physical delivery.

The Muster Hall researches two weapon and two armor upgrades. Each weapon tier adds 2 damage and each armor tier adds 1 armor to the owner's military units. A Reedguard progresses from **12 damage / 1 armor** to **16 damage / 3 armor**. Bonuses affect existing and subsequently trained units; enemy stats and shared definitions remain unchanged. Kingdom permits first-tier upgrades; Empire permits the second tiers after their corresponding first tier.

Research spends its complete cost at acceptance. Each producer handles one research item, and its training queue must first empty. It accepts no training while research runs. Separate buildings can research different eligible technologies in parallel. A player cannot research a duplicate active/completed item or run two Era advancements concurrently. A destroyed producer loses its unfinished investment; a retry costs the full amount again. Completed upgrades last for the match.

The top-right **Research** button shows the current Era and opens a safe-area panel with four Era steps, costs, durations, producer/prerequisite names, completion states and progress bars. It uses three columns at 1440×1080 and two at 1280×720, with scrolling for later cards. It respects a selected compatible producer, otherwise finding an eligible owned site. The world continues simulating while the panel blocks terrain commands. Selected buildings show research progress, and selected troops show effective upgraded stats.

## Architecture and rule decisions

`TechnologyCatalog` validates four unique Era tiers, exactly one adjacent advancement per later tier, stable references, nonnegative costs, positive durations, supported effects, direct cycles and inaccessible Era/building/technology chains. It rejects cumulative stat overflow. Empty Era/technology arrays retain the legacy stress/test fixtures. Unit and building definitions support optional Era/technology gates; authored sandbox spawns intentionally bypass those purchase gates.

`ResearchCommand` enters the same authoritative command boundary as training and construction. Read-only `World.ValidateResearch` and `ValidateRequirements` drive feedback without spending or mutating orders. Research validation checks a living, owned, completed producer, resources, unlocks, current Era, completed prerequisites and producer contention before any payment.

Tick order is **Movement → Economy → Construction → Combat → Production → Research → TickIndex increment**. Combat can destroy a producer on its would-be completion tick, preventing completion and clearing pending identity so retry remains possible. Prerequisites are checked at acceptance: losing a separate prerequisite building later does not cancel accepted research. A unit produced earlier in the completion tick receives the newly completed modifiers; combat and harvesting use those modifiers starting on the following tick.

Completion recomputes effective stats per player and unit definition, updates that player's existing units and caches values for future spawns. Combat and gathering read effective values without scanning the technology tree per action. Attack additions precede the existing highest matching counter multiplier and target armor; successful armed hits retain minimum-one damage. A launched projectile preserves its final damage snapshot. Gather amount remains bounded by cargo capacity and source stock. Completion does not alter shared balance data.

Presentation owns panel state, selection and feedback. The Unity-free simulation owns legality, inventory, progress and effects. No new scenes, services or dependencies are needed. The detailed [simulation contract](../technical/SIMULATION_ARCHITECTURE.md) and [technical design](../technical/TECHNICAL_DESIGN_DOCUMENT.md) describe these boundaries.

## Tests and results

`tools/Verify-Unity.ps1 -Stage All` completed compilation and all required stages. Existing tests were retained.

| Check | Result | Evidence |
| --- | --- | --- |
| Unity EditMode | **217/217 passed**, 57 added | [NUnit XML](../testing/evidence/phase5-editmode.xml) |
| Unity PlayMode | **26/26 passed**, 3 added | [NUnit XML](../testing/evidence/phase5-playmode.xml) |
| Shipped-JSON combat probes | **12/12 passed** | [Counter scenarios](../testing/evidence/phase5-combat-balance.md) |
| Movement regression | **30 cases; required gates passed**, known large narrow-choke failures retained | [Readable report](../testing/evidence/phase5-movement.md), [raw JSON](../testing/evidence/phase5-movement.json) |

The 57 new EditMode cases cover atomic payment and read-only validation; timing; unknown/foreign/unfinished/busy producers; duplicate research across producers; the full chain; unit/building unlocks; producer destruction, retry and pending-state release; owned, completed prerequisite buildings; owner-isolated current/future effects; stacking; projectile snapshots; cargo/source bounds; same-tick combat/production ordering; legacy data; and invalid, cyclic, unreachable or overflowing definitions.

The three PlayMode tests use the shipped JSON and normal Amber Reach map. They verify prices and readable prerequisites, research panel input interception, close behavior, exact Kingdom timing, duplicate button dispatch before a HUD refresh charging only once, Archive unlock, physical gathering/construction and progression through all four Eras. A viewport-width assertion prevents the discovered horizontal clipping regression.

Review found and fixed completion feedback that incorrectly treated sorted completed IDs as completion order. The integration chain now checks the latest completion message. Captures also exposed a default content-rectangle width offset clipping the first research column and a translucent background showing old HUD text. The content width was corrected and the panel made opaque; **all 26 PlayMode tests were rerun successfully afterward**. The subsequent source adjustment only framed selected objects in the opt-in smoke capture; the final build and packaged-player checks cover that adjustment.

## Build and packaged-player checks

The final Windows development build **succeeded with zero errors and zero warnings**, totaling **163,094,640 bytes** in **13.3849585 seconds**. Build GUID: `48c0b4723fb54362a3697c9027469896`. Launch `Builds/Windows/Emberfield.exe`; binaries remain ignored by Git. [Build summary](../testing/evidence/phase5-build.txt).

| Final-build functional check | Outcome |
| --- | --- |
| Technology, [1440×1080](../testing/evidence/phase5-technology-1440x1080.txt) | Empire, all nine research items, tick 16,338; population 6 used + 0 reserved / 6 capacity |
| Technology, [1280×720](../testing/evidence/phase5-technology-1280x720.txt) | Same result; nine visible research button dispatches |
| Economy, [1440×1080](../testing/evidence/phase5-economy-1440x1080.txt) | Gathered, constructed, trained and fought; tick 1,312, 16 entities, population 5 + 0 / 6 |
| Combat, [1440×1080](../testing/evidence/phase5-combat-1440x1080.txt) | All three roles attacked, projectile damage, three deaths and dead-view cleanup; tick 123, 25 entities |
| Combat, [1280×720](../testing/evidence/phase5-combat-1280x720.txt) | Same combat checks passed; peak two projectiles |

Both technology walkthroughs use unmodified starting inventory and physically gather Food, Wood, Metal and Stone. They construct Archive and Muster Hall, reject early Archive/Empire requests, buy every research item, check each exact four-resource deduction and reject duplicates. A Reedguard trained before equipment research and one trained after it both finish at 16 damage / 3 armor; enemy stats remain unchanged.

Each purchase scrolls its card into view programmatically, checks the top EventSystem raycast hit at the button center and dispatches a synthetic pointer-click handler. All nine buttons pass at each aspect ratio. This verifies visible button geometry and command integration; it does not claim physical touch gestures, manual scrolling or real-device safe-area usability.

The functional runner advances up to 100 ticks per yielded frame while waiting. Its 16,338 simulated ticks represent 816.9 simulation seconds with 167 yielded frames; this is a scripted accelerated scenario, not measured human match pacing or real-time FPS. All final player logs were checked for errors.

Inspected captures show the [tablet research tree](../testing/evidence/phase5-research-top-1440x1080.png), [phone lower research cards](../testing/evidence/phase5-research-bottom-1280x720.png), [active research](../testing/evidence/phase5-research-progress-1280x720.png), [Archive and world progress bar](../testing/evidence/phase5-archive-1440x1080.png), [upgraded troop stats](../testing/evidence/phase5-upgraded-unit-1280x720.png), [phone combat](../testing/evidence/phase5-combat-1280x720.png), [economy production](../testing/evidence/phase5-economy-1440x1080.png) and [the completed first loop](../testing/evidence/phase5-first-loop-finish.png). Matching companion captures are in the evidence directory. Screenshot FPS labels from these accelerated checks are not performance measurements.

## Performance observations and continuing debt

The movement rerun used the same unarmed 50/100/200/300/500-mover fixtures and a 120-second simulated deadline. Simulation plus diagnostics source SHA256 is `6FAC51FE80789FFACF7082CBCAE67C82282327FC9BF929EA04EB38E465AACEB8`. It checks the Phase 5 simulation without benchmarking active research completion or the research panel.

| 100-mover scenario | Active-tick p95 CPU | All arrived by |
| --- | --- | --- |
| OpenField | 0.089 ms | 27.45 simulated seconds |
| WideCorridor | 0.069 ms | 28.10 seconds |
| CrossingGroups | 0.173 ms | 32.20 seconds |
| DynamicObstacle | 0.209 ms | 38.45 seconds |
| NarrowChoke | 0.435 ms | 66.80 seconds |

Every OpenField, WideCorridor, CrossingGroups and DynamicObstacle case through 500 movers completed with the same arrival results as Phase 4. All 30 cases reported zero observed overlaps and zero static/map violations; unreachable commands rejected atomically. NarrowChoke passed at 50/100 but **200/300/500 still reached only 31/52/37 arrivals at the deadline**. Circulating units can avoid the immobility counter without reaching their destination. These failures remain visible in the raw evidence.

Large cold orders still hitch: this rerun's 500-unit OpenField command took 33.996 ms and NarrowChoke command 46.424 ms. This phase made no navigation optimization. Its editor CPU measurements are not rendered FPS or mobile performance. The [Phase 4 render measurements](PHASE_4_REPORT.md) remain historical serialized offscreen URP/Canvas/GPU-readback evidence; they were not rerun or relabeled for this build. The allocation capability probe still reports unavailable (`-1` measurements), not zero allocations.

Research caches avoid per-action modifier scans, but synchronous completion updates, temporary completion collections and panel refresh work have not been profiled at scale. Future measured work includes a total route-work budget, dense choke cooperation, target-device CPU/GPU/memory profiling and presentation scalability.

## Files created and modified

Paths below are relative to the repository. All nine new C# files include Unity `.meta` files.

| Area | Created | Modified |
| --- | --- | --- |
| `Assets/Game/Simulation/` | `TechnologyDefinitions.cs`, `TechnologyCatalog.cs`, `ResearchSystem.cs` | `Definitions.cs`, `PlayerState.cs`, `EntityStates.cs`, `World.cs`, `Commands.cs`, `ConstructionSystem.cs`, `ProductionSystem.cs`, `EconomySystem.cs`, `CombatSystem.cs` |
| `Assets/Game/Presentation/` | `ResearchControls.cs`, `ResearchPanel.cs`, `TechnologyPlayerSmoke.cs` | `EconomyControls.cs`, `MatchController.cs`, `MatchHud.cs`, `RtsInput.cs`, `WorldView.cs`, `PlayerSmoke.cs` |
| Content | — | `Assets/Game/Resources/Definitions/greybox.json` |
| Tests and tools | `Assets/Tests/EditMode/TechnologyTests.cs`, `TechnologyTestWorldFactory.cs`; `Assets/Tests/PlayMode/TechnologyIntegrationTests.cs` | `tools/Smoke-Player.ps1` |
| Documentation | This report; `docs/testing/evidence/phase5-*` results and captures | Root/docs READMEs; `CURRENT_PHASE.md`; technology tree and building roster; simulation, technical and pathfinding architecture; performance budget; test strategy and movement methodology |

## Known limits and next phase

This remains an engineering greybox. The research choices are functional, but full-match pacing, post-upgrade combat balance and strategic schedules need playtesting. There is no voluntary research cancellation/refund, research queue, save/load, persistent progression or faction-specific research. Effects currently support additive attack damage, armor and gather amount. No health, speed, siege or structural upgrade system is implied. Required-building ownership is checked at research start only.

Factions, fog, scouting rules, strategic AI, victory, surrender/result flow, networking and account services remain future work. No complete match or mobile performance target is claimed. Current visual checks cover desktop windows at phone/tablet proportions with original primitive assets.

**Next recommended phase: Phase 6 — two original factions.** Implement Aven Compact and Serevin March with distinct identities, bonuses and mechanics, at least one unique unit and technology each, clear counterplay and placeholder visual distinction. Preserve the existing shared counter language and research validation. That phase has not been implemented here.
