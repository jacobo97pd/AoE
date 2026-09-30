# Development plan

Status is maintained in [CURRENT_PHASE](CURRENT_PHASE.md). This roadmap defines gates. After Phases 7–10, the user authorized all remaining phases, 11–13. Their local Windows alpha implementation and verification are recorded in the current status and phase reports. Human fun, physical-device performance and production operation acceptance remain explicit where local automation cannot establish them.

## Phase roadmap

| Phase | Deliverable | Acceptance and gate |
| --- | --- | --- |
| 0 — Discovery/design | Environment/repository audit, reference synthesis, original vision, GDD, technical/art/MVP foundation | Existing work preserved, installed Unity used, assumptions and risks documented. Checkpoint 1 report. |
| 1 — Greybox core | Fixed-tick simulation, primitive unit/building/resource views, camera, selection, commands, one map | Select a Tender and move it. Compile, run relevant tests, inspect logs and record build evidence before Phase 2. |
| 2 — Economy | Four resources, gather/return, inventory, population, placement, construction, training | Gather resources, construct a Muster Hall and train a unit. Resource/cost/queue/population tests pass. |
| 3 — Combat | Health, armor, ranges, targets, projectiles, death; Reedguard/Stringwarden/Strider | Observable soft counter relationships across controlled contextual scenarios. No guaranteed victory from role alone. |
| 4 — Navigation | Group routes, local avoidance, destination spreading, line/box/loose formation, pathfinding stress scene | Measured 100+ group orders without pathological normal-scenario congestion. Checkpoint 2 report with traces. |
| 5 — Era/technology | Four Eras, prerequisites, research costs/effects, small tech tree | Progress through all four Eras; illegal/duplicate research rejected; timing tradeoffs visible. |
| 6 — Factions | Aven Compact and Serevin March, each with mechanic, unique unit and unique technology | Distinct choices, shared counter language, explicit counterplay and placeholder visual distinction. |
| 7 — Full offline match | Fog, scouting, legal AI, Conquest/Dominion, surrender, result/restart | Complete 1v1 vs AI. Tune toward 12–20 minutes. Checkpoint 3 assesses fun before expensive art/network work. |
| 8 — Mobile UX | Refined touch, safe-area HUD, responsive tablet/phone layouts, device build preparation | Full match controllable without mouse/keyboard; real device usability recorded where available. |
| 9 — Art vertical slice | One biome, small Aven unit/building/resource kit, team masks, animation, VFX/audio | Original direction is readable at gameplay zoom and fits measured budgets. |
| 10 — Optimization | Profile CPU/GPU, simulation, rendering, navigation, GC and memory; fix measured costs | Documented target-device performance; capable-tablet 60 FPS target, stable 30 FPS floor on supported lower hardware. |
| 11 — Multiplayer foundation | Evaluate available/authorized Quantum suitability; transport/commands, private two-player room, disconnect behavior | Synchronization and failure scenarios validated. Checkpoint 4. No ranked implementation yet. |
| 12 — Backend/matchmaking | Accounts/profile, authoritative results, hidden MMR, visible rank, history, then queueing | Client cannot award wins or rank. Paid services require explicit user authorization before activation. |
| 13 — Alpha productization | Reconnect/AFK, analytics/crash reporting, settings, tutorial, post-game statistics, build pipeline | Reproducible builds and recoverable sessions; assess wider content only after evidence. |

## Working method

For each phase: inspect current state, choose a bounded change, implement it, compile, run appropriate tests, fix actual failures, update documentation and report evidence. A failed test is not removed or commented out to manufacture a pass. A future interface is not a working implementation.

Checkpoints are reports under the user's present instructions and do not require repeated permission. Continue within authorized scope unless a destructive migration, credential/license requirement, material external cost or genuinely unresolved product direction makes user input necessary.

## Dependencies and restraint

Offline commands and simulation state come before transport. Correct small-group movement comes before advanced flow fields. Data definitions come before elaborate editors. Representative profiling comes before Jobs/Burst/ECS conversion. A complete enjoyable AI match comes before production art breadth, seasons and real matchmaking.

Do not reinstall Unity, change its version arbitrarily, install large optional tools automatically or push to a remote. Android/iOS build availability is constrained by installed modules and host tooling; record those limits honestly. Windows does not produce a complete signed iOS/Xcode release.

## Verification matrix

| Milestone | Evidence expected |
| --- | --- |
| Pure rules | Unity EditMode tests for active resource, command, queue, combat or prerequisite behavior. |
| Runtime integration | Suitable PlayMode tests, scene boot and view/command integration. |
| Important checkpoint | Unity compilation, logs checked for compiler errors, tests recorded, local build status with exact blockers. |
| Performance claim | Hardware/editor context, scenario/entity count, frame/simulation/path timing and measurement limitations. |
| Mobile claim | Target/device or clearly labeled editor aspect simulation; no inferred device FPS or usability. |
| Multiplayer claim | Two independent clients, authority/sync checks and documented disconnect/resync behavior. |

Use 50/100/200/300/500-entity configurable stress cases as the harness grows. A spawn count alone is not success: record route shape, active systems, frame timing and congestion. Dedicated PathfindingStressTestScene and RTSPerformanceStress are useful only when their measurements represent implemented behavior.

## Phase report template

Every phase report includes phase/status; files created/modified; features actually implemented; architecture decisions; tests added and results; build status; known limitations; performance observations; technical debt; and next recommended phase. Link raw logs/test artifacts where useful. Do not report future gameplay as completed because its documentation exists.
