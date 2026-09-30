# Alpha 0.3 — verification and playable scope

September 10, 2026. This expansion continues the existing Emberfield alpha. The original phase 0–13 evidence remains historical; the new PDF's numbering does not restart the project.

The Windows development player is `Builds/Windows/Emberfield.exe`. Its home screen opens skirmish setup with separate Historical/Fantasy choices, four factions per realm, three biomes and Conquest/Dominion. Drag to pan, pinch/scroll to zoom, and use **Rotate view** for quarter turns. The Store offers twelve appearance previews; select a product, inspect its actual game model and apply the free offline preview. Online equipment comes from authenticated server entitlements. Real checkout is disabled until a payment provider is integrated; shown prices are provisional.

## Implemented rules

Historical: Aven Compact, Serevin March, Miraj Sultanate and Skeld Clans. Fantasy: Solar Kingdom, Verdant Covenant, Ashen Dominion and Drakeforged Clans. Realm validation applies to local setup, private rooms, queues and the C# authority; ratings and history are separated by realm. A historical army cannot enter a fantasy match.

Elephants, sun lions, living guardians, war trolls and drakes are paid-for military units in the match economy, with population costs, progression requirements and counters. Cosmetic purchases do not buy those units or change their rules. The simulation contains no cosmetic fields; local preview, own online equipment and rival online equipment are separate presentation paths. Receipt binding, replay protection and persistence are exercised by server tests, without charging money.

Walls, gates, watchtowers and keeps have real simulation behavior. Rams damage fortifications; ladders and siege towers support infantry boarding. Wall decks grant elevation, armor and ranged reach, and support descent to clear ground on either side. Closing an occupied gate is rejected. Defenses fire projectiles and oil according to simulation cooldowns. The construction HUD paginates the expanded catalog, while selection exposes training and wall actions.

## Evidence

Raw evidence is retained under [evidence/alpha03](evidence/alpha03/).

| Source check | Result |
| --- | ---: |
| Unity EditMode | 460/460 passed |
| Unity PlayMode | 135/135 passed |
| Combined Unity tests | 595/595 passed |
| Node service tests, including real C# authority | 25/25 passed |
| Expanded deterministic fixtures | 92/92 passed |
| Existing combat / faction probes | 12/12 and 8/8 passed |
| Existing natural AI match gate | 4/4 completed |
| Movement matrix | Required gates passed across 30 cases |

PlayMode includes pixel readback through the actual cosmetic shader, preservation of team pixels and restoration of source colors, camera rotation/ground-footprint checks, and real touch events reaching wall/descent commands after Select mode. Store models render through their own proportional texture and per-instance studio lighting; those presentation settings do not enter the World.

The [92-scenario simulation report](evidence/alpha03/simulation-expansion.md) includes eight ordinary paid progression fixtures, thirty counter measurements, five DPS cases, one dispersed ranged-army case and **48/48 completed natural matches** across eight factions, three maps and two victory modes. No hidden attack target or injected victory is accepted. The final Conquest range is 442.80–956.95 seconds; Dominion is 756.70–1483.20 seconds. These are deterministic AI pacing observations, not proof of human competitive balance.

The authoritative content pin is `frontiers-68de7f60639e55961b927d91c7555cd4a54dcd9e8ea10322af0cd20f285a7b73`. The [simulation specification](../technical/EXPANSION_SIMULATION.md) records creature stats, counters, production, siege rules and known restrictions. [Art provenance](../art/ALPHA_03_WORLD_ART.md) identifies the editable procedural geometry and biome work. [Metrics](../technical/ALPHA_METRICS.md) defines exactly what HUD and profiling numbers measure.

## Final Windows package

Unity 6000.3.23f1 built version **0.3.0**, application GUID **`35abc41770ce473f8606780a352eccaf`**, with **zero errors and zero warnings**, total build size **170,609,266 bytes**. The [build summary](evidence/alpha03/build.txt), [packaged matrix](evidence/alpha03/player-matrix.md) and [run inventory](evidence/alpha03/player-runs.json) describe this package. Intermediate builds and failed preflight runs remain in ignored TestResults and are not presented as final evidence.

All **12 functional packaged checks passed**: eight-faction expansion walkthrough, two frontend aspect ratios, two retained faction drills, research, two natural offline matches, economy, combat and two independent online sessions. The expansion dispatches **76 visible native controls**, develops ordinary economies, observes all six new signature units and inspects six actual wardrobe models. Aven's unarmed Threadkeeper is exercised by its dedicated paid faction drill rather than claimed as a natural combat-AI recruit.

Historical/Amber Crossing at 1280×720 and Fantasy/Sapphire Coast at 1440×1080 each use two real Windows processes plus fresh HTTP/SQLite and the C# authority. Both kill/restart the guest, recover orders and state, verify realm-scoped persistent history, and return both participants to an open lobby. Temporary credential configs are removed and all runner-owned processes are stopped. Server tests additionally exercise all six realm/biome combinations and reject mixed realms.

The [Spanish visual gallery](evidence/alpha03/gallery.html) contains final-player captures, not mockups. Cosmetic review caught and fixed a palette blend that left sapphire wings brown, then verified real rendered pixels. Preview framing and studio light expose the same geometry used in game. Touch review fixed Select mode intercepting wall targeting. The legacy faction driver was updated to select the visible Aven/Serevin pair in the expanded chooser while retaining its actual clicks, costs and mechanics assertions. The final wide HUD keeps Formation readable alongside Rotate view.

## Measured local performance

The two frontend runs each observe eight seconds of ordinary capped gameplay after two simulated minutes of accelerated development. Their final 300-frame HUD windows show **59.70 FPS / 16.70 ms frame p95** at 1280×720 and **59.98 FPS / 16.67 ms frame p95** at 1440×1080, with a 60 FPS cap. This is a short workstation observation, not sustained device qualification.

The separate [render probe](evidence/alpha03/render.json) and [capture](evidence/alpha03/render.png) use the fixed Aven/Dominion fixture: 6,000 ordinary AI ticks, 60 warmup ticks, then ticks **6060–6460** at 20 Hz. It produced **10,053 verified rendered samples** with a simulation/wall-time ratio of **0.99977**. Its hidden non-batch D3D11 camera renders a persistent offscreen target and synchronously reads a pixel each loop; the figures include that serialization and are **not display FPS**.

| Measurement | p95 |
| --- | ---: |
| Serialized full frame | 2.876 ms |
| World.Tick, excluding AI/observer/presentation | 0.782 ms |
| AI pair per tick | 0.211 ms |
| Presentation update | 0.265 ms |

GPU timing was unavailable. No mobile GPU, display-present latency, thermal or battery result is inferred. This is an absolute current fixture result; changed simulation content and corrected metric observation prevent an isolated rendering-speed comparison with Alpha 0.2.

## Remaining alpha limits

- Artwork is original stylized procedural geometry with shared meshes and simple animation, not the cinematic home illustration. Production character rigs, authored animation sets and final commercial art remain future work.
- Drakes animate in the air but use the ground navigation graph. Large creatures currently retain a 450 mm simulation radius; skins preserve the same geometry, selection volume and rules.
- Wall movement uses fixed deck slots and explicit ascent/descent; continuous traversal along connected ramparts is not implemented. The AI uses rams; ladder/tower boarding is under player control.
- Forest, Caribbean and desert contain gameplay terrain, resources and decorative landmarks. Ships are scenery; naval combat is outside this alpha.
- Payment-provider checkout, refunds/revocation integration, platform store review and live hosted operations are not activated. Sandbox claims, when an operator explicitly enables them, are test entitlements only.
- This run validates a Windows development player and local services. Android/iOS builds, physical-device memory/thermal/touch testing, WAN conditions, human balance and accessibility playtesting remain open. Serialized offscreen PC render measurements must not be presented as mobile or display FPS.

## Reproduction

Run from the project root with Unity 6000.3.23f1, Node 22.12+ and the .NET 8 runtime installed:

```powershell
./tools/Verify-Unity.ps1 -Stage All
./tools/Verify-ExpansionSimulation.ps1
./tools/Verify-Alpha.ps1 -Stage Services
./tools/Verify-Alpha.ps1 -Stage Build
./tools/Smoke-Expansion.ps1 -Width 1280 -Height 720
./tools/Smoke-AlphaProduct.ps1 -Width 1280 -Height 720
./tools/Smoke-AlphaProduct.ps1 -Width 1440 -Height 1080
./tools/Smoke-Online.ps1 -Realm historical -MapId amber_crossing -Width 1280 -Height 720 -TimeoutSeconds 240
./tools/Smoke-Online.ps1 -Realm fantasy -MapId sapphire_coast -Width 1440 -Height 1080 -TimeoutSeconds 240
./tools/Profile-OfflinePlayer.ps1 -Label alpha03 -Width 1280 -Height 720
```

Do not run Unity editors/builds concurrently against the same project. The online runner starts an isolated local service and two real native clients, interrupts the guest, reconnects it and verifies authoritative results/history and return to lobby. Its test-account credentials are temporary and are removed by the runner. Local service setup for human play is documented in [Server/README](../../Server/README.md).
