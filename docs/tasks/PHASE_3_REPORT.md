# Phase 3 — combat

Status: **complete and verified** on 9 September 2026 in Unity 6000.3.23f1. Reedguard spear infantry, Stringwarden archers and Strider cavalry can fight with observable soft counters. The packaged player also completes select → move → gather → construct → train → fight using the soldier produced by that economy loop.

## Files created

- Simulation: `AttackCommand.cs`, `CombatDefinitions.cs`, `CombatSystem.cs`, `ProjectileState.cs`, `TargetGeometry.cs` and Unity metadata.
- Presentation/tooling: `CombatPlayerSmoke.cs`, `Editor/CombatVerification.cs` and metadata.
- Sandbox: `Resources/Maps/combat_sandbox.json`, `Scenes/CombatSandbox.unity` and metadata.
- Tests: `EditMode/CombatTests.cs`, `CombatTestWorldFactory.cs`, `PlayMode/CombatIntegrationTests.cs` and metadata.
- This report and the `docs/testing/evidence/phase3-*` artifacts linked below.

## Files modified

Simulation definitions/validation, entity states, commands, World composition, navigation and movement/economy/construction integration. Presentation definition loading, controller, HUD, views, camera and player smoke. Shared balance JSON, editor scene/build setup and `EditorBuildSettings.asset`; verification/smoke PowerShell wrappers; README, unit roster/GDD, current phase and technical/testing documents. Existing economy/input scenes and the supplied PDF remain preserved. Git records the exact inventory.

## Features implemented

- Atomic attack commands validate live owned armed units, hostile live targets and reachable firing positions. Repeated orders cannot reset cooldowns. A mixed touch selection attacks with its armed members; workers remain unarmed.
- Configurable health, armor, edge-to-edge range, cooldown, acquisition range and tag bonuses. The highest matching bonus is applied once, then armor; damage has a minimum of one. Numeric validation and integer arithmetic prevent invalid rule states.
- Idle military units acquire the nearest reachable enemy at a bounded cadence with stable target ties. Explicit attacks pursue moving targets; move/work orders retain priority. Stop cancels attacks and holds fire until a new order.
- Stringwarden projectiles travel in simulation, home toward a living target, carry damage determined at launch and expire after the configured lifetime. A dead shooter does not erase an airborne shot. A dead target causes a miss.
- Same-tick lethal melee attacks can cause mutual kills. Existing projectile impacts resolve before living actors issue that tick's attacks. Melee damage then resolves after all those actors have attacked, removing the former lower-ID advantage in a lethal melee tie.
- Unit death removes active state/view/selection and releases population once; carried resources are lost. Building destruction clears its footprint and queue, releases queued population, removes completed housing capacity and gives no refund. Remaining units may exceed reduced capacity, but further training/spawning obeys the cap. Construction grants health incrementally so work does not erase prior combat damage.
- Distinct primitive spear, bow/quiver and mounted silhouettes; health bars, attack pulses, impact feedback and pooled projectile views. HUD shows roles, health, attack/armor/range and counter guidance.
- The Battle/Economy switch opens the ready combat sandbox or economy fixture. The sandbox has two of each military role per side, workers, resources and completed production. Its camera and map margins were corrected after a phone capture showed units obscured by the header.

## Architecture decisions

Rules remain in the plain C# assembly without UnityEngine. The 20 Hz order is movement → economy → construction → combat → production. Combat resolves projectile arrivals, actor attack decisions and staged melee hits in defined stages. Presentation centrally synchronizes views; no unit/arrow physics bodies or per-entity Update loops were introduced.

Acquisition runs no more frequently than every ten ticks and pursuit route searches every five ticks. Routes seek a legal point within attack range of a unit radius or building footprint. Projectiles default to a 400-tick lifetime. The view retains at most 128 inactive arrow objects for reuse. These bounds improve predictability, but do not establish large-army performance or cross-device lockstep determinism.

For the prototype counter tags, Reedguard is `Infantry | Light`, Stringwarden `Ranged | Light`, and Strider `Cavalry | Light`. The archer's Infantry bonus therefore does not also increase damage against other archers. All three cost 80 total resources, use one population and train in 160 ticks; Food and Wood retain different economic costs, so equal totals are only a nominal comparison.

## Tests added and results

**160/160 tests passed: 141 EditMode and 19 PlayMode; zero failures or skipped cases.** This retains all 86 rules and 14 integration tests from Phase 2 and adds 55 combat rules cases plus five combat integration cases.

Rules cover definition validation, ownership/atomic rejection, target legality, range/footprints, pursuit, reachable acquisition/ties, cooldown idempotence, armor/minimum damage/bonus aggregation, projectile travel/expiry/dead source/dead target, explicit order interruption, unit/building death and population/queue/navigation cleanup, lost worker cargo, damaged construction, mirrored counters and simultaneous melee deaths under swapped owner/ID assignments.

Integration covers authored training/presentation of all three roles, enemy taps issuing attacks while preserving selection, visible projectile travel/impact/health-bar updates and view release, dead selected entity removal, and Stop holding fire until a fresh attack. Existing synthetic mouse/touch and economy integration tests also pass. PlayMode was repeated successfully after the final sandbox camera/map adjustment.

The editor probe also passed **12/12 scenarios using the actual shipped JSON**, with the rules hash recorded. Each matchup swaps player/spawn sides and waits for remaining projectiles before recording its outcome:

| Favored unit | Opponent | One versus one, both sides | Two opposing units versus one favored unit |
| --- | --- | --- | --- |
| Reedguard | Strider | Reedguard survives with 34 health | Striders win |
| Stringwarden | Reedguard | Stringwarden survives with 22 health | Reedguards win |
| Strider | Stringwarden | Strider survives with 56 health | Stringwardens win |

The probes use open ground with seven metres between front units and attack orders at tick zero. They demonstrate useful, nonabsolute advantages in this controlled fixture; they do not prove competitive balance across terrain, army compositions, reinforcements or human control. See the [complete matchup report](../testing/evidence/phase3-combat-balance.md).

## Build and packaged player verification

Windows development build: **Succeeded, 0 errors, 0 warnings**, 162,933,015 bytes (about 163 MB). Artifact: `Builds/Windows/Emberfield.exe`. Open it normally and press **Battle** to play combat.

| Packaged scenario | Result | Evidence |
| --- | --- | --- |
| Combat, 1440×1080 | Passed: all three roles attacked; damage/projectiles observed; three deaths; dead views removed. Tick 107, 25 entities. | [Report](../testing/evidence/phase3-combat-smoke-1440x1080.txt), [frame](../testing/evidence/phase3-combat-1440x1080.png) |
| Combat, 1280×720 | Passed with the same combat outcomes; final framing keeps the fighting group clear of the HUD. | [Report](../testing/evidence/phase3-combat-smoke-1280x720.txt), [frame](../testing/evidence/phase3-combat-1280x720.png) |
| Economy through combat, 1440×1080 | Passed: moved a Tender, gathered/delivered, constructed a Muster Hall, trained a Reedguard, then used that soldier to defeat the enemy worker. Tick 1413, 16 entities, population 5 + 0 queued / 6. | [Report](../testing/evidence/phase3-first-loop-smoke.txt), [economy frame](../testing/evidence/phase3-economy.png), [combat finish](../testing/evidence/phase3-first-loop.png) |

Combat screenshots were captured during battle at tick 35, before the final three deaths, so they show 28 entities. The report records the later final state. Both resolutions were visually inspected for silhouettes, health bars, arrows and readable HUD controls. Camera/HUD captures use an explicit URP render request because hidden batch players do not have a usable visible backbuffer.

All smoke scenarios disclose accelerated simulation ticks and ran on an NVIDIA RTX 3060 Ti. The combat smoke advances ticks itself, so its ordinary driver tick-time overlay reads zero; it must not be interpreted as a simulation measurement. The economy finish displayed an isolated 55 FPS / 0.010 ms tick reading, also not a benchmark. No device thermal, battery, frame-percentile or touch-latency measurements were collected.

The final build and player logs contain no compiler/shader/runtime failures. Editor logs retain the previously audited nonfatal licensing access-token message followed by successful entitlement resolution. No licensing or platform installation was changed. Android Build Support remains absent; no APK or iOS build is claimed.

Additional tracked evidence: [EditMode XML](../testing/evidence/phase3-editmode.xml), [PlayMode XML](../testing/evidence/phase3-playmode.xml), [build summary](../testing/evidence/phase3-build.txt). Full logs remain under ignored `TestResults/`. All asset files have Unity metadata. The supplied reference PDF retains SHA256 `778295A57495B1F67CC9E42A7F8F01932EF5841F6491FB72E84F55D77BCAEDF7`.

## Reproduce

Close this project's editor before batch verification:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools/Verify-Unity.ps1 -Stage All
powershell -NoProfile -ExecutionPolicy Bypass -File tools/Build-Unity.ps1 -Target Windows
powershell -NoProfile -ExecutionPolicy Bypass -File tools/Smoke-Player.ps1 -Scenario Combat -Width 1440 -Height 1080
powershell -NoProfile -ExecutionPolicy Bypass -File tools/Smoke-Player.ps1 -Scenario Combat -Width 1280 -Height 720
powershell -NoProfile -ExecutionPolicy Bypass -File tools/Smoke-Player.ps1 -Scenario Economy -Width 1440 -Height 1080
```

## Known limitations, performance and technical debt

Moving units can overlap and their meshes can visibly intersect during melee. This is documented behavior awaiting Phase 4 collision/congestion work; visual captures deliberately retain that evidence. The initial targeting scan is quadratic at scale and attack-range BFS may be expensive in dense battles. Profile acquisition, pursuit, route allocation, command latency and congestion with representative 50/100/200/300/500-entity fixtures before optimizing or claiming 100+ units work well.

There is no line-of-sight occlusion, projectile terrain interception, friendly fire, attack-move, formation steering, strategic AI, fog, victory, save/replay file, technology, faction mechanics, networking or account service. Destroying a Hearth does not produce a match result. A new/reused arrow may keep its previous/default visual orientation until its first movement tick. Final art, animation, sound and physical touch-device UX remain later work.

The counter tests cover one controlled opening rather than the full balance space. Future tests should include protected ranged lines, reinforcement timings, terrain and larger mixed forces after movement is reliable. The nominal 80-resource total does not make Food and Wood economically interchangeable.

Next recommended phase: **Phase 4 — movement/pathfinding**. Add measured group movement and congestion handling, the configurable stress scene, and the 100+ unit acceptance scenarios. Record actual performance and gameplay limitations at Human Checkpoint 2. No Phase 4 implementation was started in this checkpoint.
