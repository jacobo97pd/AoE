# Phase 2 — economy

Status: **complete and verified** on 9 September 2026 using the audited Windows workstation and Unity 6000.3.23f1. The player can select a Tender, gather and deliver resources, construct a Muster Hall and train a Reedguard. Combat is Phase 3.

## Files created

- `Assets/Game/Simulation/`: `PlayerState.cs`, `EconomyCommands.cs`, `MovementSystem.cs`, `EconomySystem.cs`, `ConstructionSystem.cs`, `ProductionSystem.cs`, `InteractionSearch.cs` and their Unity metadata.
- `Assets/Game/Presentation/EconomyControls.cs` and metadata.
- `Assets/Tests/EditMode/EconomyTests.cs`, `EconomyTestWorldFactory.cs`, `Assets/Tests/PlayMode/EconomyIntegrationTests.cs` and metadata.
- This report and the `docs/testing/evidence/phase2-*` artifacts listed below.

## Files modified

Simulation definitions, validation, commands, entity states, navigation and World composition; presentation controller, HUD, views, camera and player smoke; `Resources/Definitions/greybox.json`; README, game vision/GDD, current phase and technical/testing documents. `.gitattributes` standardizes source line endings and permits Unity-generated trailing whitespace without rewriting generated assets; `.gitignore` received whitespace cleanup. Existing scenes, reference PDF and rendering assets were preserved.

## Features implemented

- Separate Food, Wood, Metal and Stone inventories with validated costs. Harvested stock travels in finite worker cargo and becomes spendable only after arrival at an owned completed drop-off.
- Automatic gather/return cycles, partial final loads, reassignment without losing cargo, and explicit delivery after Stop. Selected empty workers retain their orders during manual cargo delivery. Exhausted source IDs remain valid, while their visuals and blocked navigation cells clear.
- Population used, queued reservations and capacity are visible. Completed Hearths and Shelters add configured capacity.
- Grid-aligned placement previews with rejection reasons. Accepted foundations spend once, block their footprint immediately and update existing navigation. Assigned workers travel to the site and construct it; an unfinished building can receive new builders.
- Hearth, Shelter, Storeyard and Muster Hall configuration. Hearth trains Tenders; completed Muster Halls train Reedguards. FIFO queues charge and reserve population on acceptance, wait when exits are occupied, and dispatch produced units to an optional rally point.
- Contextual touch/mouse commands, costs, worker cargo/task details, construction progress and queue time. Safe-area layouts support the verified tablet/phone aspect ratios. All gameplay costs and rule validation remain in the simulation.

## Architecture decisions

The plain C# simulation still has no UnityEngine reference. World composes movement → economy → construction → production at 20 Hz. The presentation submits commands and synchronizes primitive views centrally; it does not own inventories, build completion or spawning. Integer coordinates and repeatable same-runtime tests are useful foundations, but do not establish cross-device lockstep determinism.

Construction validation checks affordability, footprint, units and worker approach paths before any mutation. Accepted sites update connectivity and replan routes. Training reserves population when queued, so parallel producers cannot overbook capacity. Full inventories retain undeposited cargo rather than overflow or discard it. Queue cancellation, refunds and destruction remain explicit future policy work.

## Tests added and results

**86/86 EditMode and 14/14 PlayMode passed; 100 total, no failures or skipped tests.** The final run includes the original 33 EditMode/10 PlayMode cases plus 53 economy rules cases and four economy integration cases.

- Rules: four-resource conservation, costs/ownership, partial and exhausted harvests, cargo reassignment/stop/manual return, integer overflow protection, placement atomicity, construction completion and interruption, capacity, FIFO training, blocked exits, rally orders, navigation updates and repeatable command playback.
- Integration: resource tapping and credit only on return; gather → preview → construct → train → selectable new view; invalid/cancelled placement without spending; stopped carrier tapping its own drop-off, physically delivering and remaining idle with selection preserved.
- Existing queued mouse/touch tests still pass, including UI exclusion, pan/pinch arbitration and multi-contact cancellation. These exercise synthetic input in Unity; they are not physical touch-device usability evidence.

Reproduction commands:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools/Verify-Unity.ps1 -Stage All
powershell -NoProfile -ExecutionPolicy Bypass -File tools/Build-Unity.ps1 -Target Windows
powershell -NoProfile -ExecutionPolicy Bypass -File tools/Smoke-Player.ps1 -Width 1440 -Height 1080
powershell -NoProfile -ExecutionPolicy Bypass -File tools/Smoke-Player.ps1 -Width 1280 -Height 720
```

## Build and rendered player verification

Windows development build: **Succeeded, 0 errors, 0 warnings**, 162,884,454 bytes; approximately 163 MB before release-size work. Local artifact: `Builds/Windows/Emberfield.exe`. Launch normally for interactive play.

| Player smoke | Outcome | Final state |
| --- | --- | --- |
| 1440×1080 | Passed; gathered, built and trained | Tick 1043; 17 entities; population 5 + 0 queued / 6 |
| 1280×720 | Passed; gathered, built and trained | Tick 1042; 17 entities; population 5 + 0 queued / 6 |

Both ran with NVIDIA GeForce RTX 3060 Ti. The opt-in smoke accelerates economy ticks and explicitly renders the real URP camera and HUD into a texture; it does not use the hidden batch player's unusable backbuffer. Screenshots were visually inspected: costs, population, context buttons, constructed building and trained unit are rendered. The phone view has limited battlefield space and still needs hands-on UX refinement. Different ending ticks reflect frame scheduling in this accelerated harness, not a deterministic replay comparison.

Compile/build/test logs were inspected. No C# compiler or shader errors were found, and player logs contain no runtime errors. Editor logs retain a nonfatal licensing-client signature/access-token message, followed by successful Unity Personal entitlement resolution and license update; all invoked operations completed successfully. The original incomplete 6000.6 installation remains documented in the audit and was not repaired.

Tracked evidence:

- [EditMode XML](../testing/evidence/phase2-editmode.xml) and [PlayMode XML](../testing/evidence/phase2-playmode.xml).
- [Build summary](../testing/evidence/phase2-build.txt).
- [Tablet smoke](../testing/evidence/phase2-smoke-1440x1080.txt) and [rendered frame](../testing/evidence/phase2-greybox-1440x1080.png).
- [Phone smoke](../testing/evidence/phase2-smoke-1280x720.txt) and [rendered frame](../testing/evidence/phase2-greybox-1280x720.png).

Full local logs remain under ignored `TestResults/`. The supplied PDF hash remains `778295A57495B1F67CC9E42A7F8F01932EF5841F6491FB72E84F55D77BCAEDF7`.

## Known limitations and performance observations

This is an economy greybox. Reedguards can be trained and moved; attacks, armor, projectiles, death and soft counters do not exist yet. Eras, technologies, faction mechanics, fog, AI, victory, saves, networking and account services are later phases. No full start-to-finish match is claimed.

Moving units can overlap. Destination spreading and static obstacle routing do not yet solve crowd congestion. Large-army stress scenarios and measured group movement belong to Phase 4. No dedicated performance stress scene or mobile performance result is claimed at this checkpoint.

The final desktop screenshots displayed the 60 FPS target and single-tick readings of 0.003 ms / 0.008 ms. Those isolated readings are **not benchmarks**: the scene has only 17 entities and the smoke accelerates rules ticks. No CPU/GPU percentile, thermal, battery or touch-latency measurements were collected. Placement preview validation clones navigation and searches approaches at the 10 Hz HUD refresh cadence; profile its allocation/time cost before scaling or caching it.

Android build preparation exists, but Unity Android Build Support is absent, so no APK was produced. iOS export/signing was not performed; Windows is not a complete iOS build environment. Final art and audio remain intentionally deferred.

## Technical debt and next phase

Measure crowd steering and route/preview allocation costs with representative entity counts. Add cancellation/refund and destruction rules when gameplay requires them. Improve compact-screen context layout and test physical touch hardware during the mobile UX gate. A shared rules boundary does not replace a future deterministic scheduler or network validation.

Next recommended phase: **Phase 3 — combat**, implemented and verified as its own bounded pass. Add configurable attack/armor/range, target validation, projectiles, death and the Reedguard/Stringwarden/Strider soft-counter relationships before scaling content. Complete the select → move → gather → construct → train → fight milestone there.
