# Phase 1 — greybox core

Status: complete and verified on the audited Windows workstation. Phase 2 may proceed.

## Files created / modified

Created `Assets/Game/Simulation/` (plain C# rules and navigation), `Presentation/` (camera, views, input, HUD and opt-in player smoke), `Editor/ProjectTools.cs`, `Resources/Definitions/greybox.json`, `Resources/Maps/amber_reach.json`, shared URP material, `Scenes/Greybox.unity`, `Scenes/MobileInputTest.unity`, EditMode/PlayMode test assemblies and tests, `Packages/manifest.json` and lock, native `ProjectSettings/`, `Assets/Settings/` URP assets, and verification/build/smoke scripts under `tools/`. Unity-generated .meta files are retained. See Git for the full file inventory.

## Implemented

- Select an owned Tender and order it to move, using touch or mouse.
- Fixed 20 Hz simulation, integer millimetres, stable IDs, atomic ownership/target validation, Move and Stop commands.
- Four-neighbour obstacle routing, reused search buffers and canonical destination spreading. Units, buildings and four resource node kinds are data-driven.
- Primitive 3D view proxies, shared material source, camera interpolation, constrained elevated camera with pan and pinch/wheel zoom.
- Tap, double-tap similar visible units, long-press drag/Shift-drag group selection, UI exclusion and pinch cancellation. Three contacts cancel until every finger lifts.
- Safe-area HUD, developer-only metrics, scene/setup validation and Windows/Android build entry points.

## Architecture decisions

Rules have no UnityEngine dependency. Presentation is centrally synchronized; no authoritative transforms or unit Update loops. Paths are generated at command boundaries; fixed ticks do not run global searches. Local submission is synchronous between ticks; network scheduling and cross-device determinism are future work. Windows development output is a test harness for a mobile product.

## Tests and results

- **33/33 EditMode passed:** definitions, ownership, command atomicity, boundaries, speed/remainders, stop, obstacle/unreachable routing, canonical groups and repeatable command playback.
- **10/10 PlayMode passed:** rendered proxy synchronization, selection/movement/stop, camera bounds, gesture arbitration, actual queued mouse/touch events, UI exclusion, pinch recovery and third-contact cancellation.
- Mouse tests explicitly configure hidden-editor focus routing and wait for a new UI canvas to register. The runtime focus policy is unchanged.
- Windows development player: **Succeeded, 0 errors, 0 warnings**. Approximately 163 MB before any release-size work. See local `TestResults/build-summary-StandaloneWindows64.txt`.
- Player smoke: **passed**, 15 entities, tick 26, 1440×1080, NVIDIA RTX 3060 Ti. Actual camera and HUD rendered to a texture and visually inspected. Batch windows have no usable visible backbuffer, so the smoke explicitly renders through URP rather than treating a black capture as evidence.
- Initial 6000.6 launch and the first import's transient source-refresh error were resolved/documented. The clean compile log has no compiler errors.

Tracked evidence: `docs/testing/evidence/phase1-editmode.xml`, `phase1-playmode.xml`, `phase1-smoke.txt`, `phase1-build.txt`, `phase1-greybox.png`. Full local logs remain ignored under `TestResults/`.

## Limits, performance and debt

This is a movement greybox. Resources are selectable sources, not harvested yet; buildings do not yet construct or train. Combat, fog, AI, technology, factions and online services remain absent. No claim of a complete match.

The small desktop smoke displayed the 60 FPS target with a tiny simulation cost, but it is not a benchmark and says nothing about tablet/phone performance. No large-army guarantee; moving units can overlap, routing turns at grid centres, and congestion/local steering remain Phase 4. The initial camera/HUD framing needs further refinement for base building. Android module absent; no Android APK or iOS build claimed. Full phone UX is Phase 8.

Next phase: economy, carrying/drop-off, inventory, placement/construction, population and training. Then repeat the complete verification gate.
