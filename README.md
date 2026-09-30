# Emberfield - Alpha 0.3: Realms and Frontiers

## iPhone / Codemagic

Este repositorio incluye el proyecto Unity completo y el export Xcode en `ios/`.
Conecta esta rama a Codemagic y ejecuta primero **ios-compile-check**.
Para instalar mediante TestFlight, sigue [LEEME.md](LEEME.md): configura el Bundle ID,
la cuenta Apple y la firma, y ejecuta **ios-testflight**. La prueba inicial es offline.
La exportación Unity pasó; la compilación nativa en Codemagic y la prueba en iPhone
siguen pendientes. Los archivos grandes usan Git LFS: `git lfs install` y `git lfs pull`.

La copia de publicación conserva el estado local `ba3ae3f` y las herramientas iOS.
El historial de desarrollo original permanece en el repositorio local. Las secciones
de arte y alpha más abajo son documentación histórica; el tramo naval terminado está
descrito en [su informe](docs/technical/naval-slice-20260928.md).


The current art review focuses on **one Kingdom infantry soldier**, rebuilt for visual inspection in a compact courtyard. Open [the soldier gallery](Artifacts/ArtReview/royal-soldier/index.html), launch `Builds/RoyalSoldier/RoyalSoldier.exe`, or open `Assets/Game/Scenes/RoyalSoldier.unity` and press Play. [The review](docs/art/royal-soldier-review.md) and [asset guide](Assets/Game/RoyalSoldier/README.md) record its scope and reproduction. The earlier [three-character slice](Artifacts/ArtReview/kingdom-premium/index.html) remains available for comparison; these art reviews do not replace the playable alpha below.

An original tablet-first RTS in Unity with ten active factions, four playable maps, trainable creature armies, functional siege, a native frontend and HUD, and authoritative online 1v1. **Historical, Fantasy and Naval are separate PvP realms with their own opponents, rankings and history.** The Quartermaster contains 12 cosmetic appearances, with free offline previews and a separate server-owned wardrobe.

The September 14 faction update is recorded in the [current roster](docs/design/FACTION_GROUPS.md) and [realm verification report](docs/technical/faction-realms-review.md). Alpha 0.3 extends the completed Alpha 0.2 and original Phases 0-13. Its earlier [verification report](docs/testing/ALPHA_03_VERIFICATION.md), [evidence](docs/testing/evidence/alpha03/) and [visual gallery](docs/testing/evidence/alpha03/gallery.html) retain results for their own source and package versions; they do not establish acceptance of a newer build.

## Open and play

Launch `Builds/Windows/Emberfield.exe`. A normal launch opens **Home**. Choose **Play**, select **Históricas**, **Fantasía** or **Navales**, choose an available faction and battlefield, select **Conquest** or **Dominion**, then begin. Historical and Fantasy can use Amber Crossing, Sapphire Coast and Sunscar Basin; Fantasy also has its exclusive default map, Tierras de Leyenda (`legend_lands`). Naval uses Sapphire Coast only. Opponents always belong to the same realm.

| Realm | Active factions |
| --- | --- |
| Históricas | Franceses, Hispanos, Ingleses, Sultanato, Confederación del Sahel |
| Fantasía | Orcos, Enanos, Hombres de las montañas, Elfos |
| Navales | Piratas |

Marina inglesa, Marina española and Flota esquelética are planned, locked entries. Naval currently means land battles on the Caribbean coast, including Pirates versus Pirates. Miraj and Solar remain legacy definitions/history only. This classification adds no ships and does not claim finished national or orc character collections.

**Amber Crossing** is temperate forest with river crossings and wooded approaches. **Sapphire Coast** is Caribbean terrain with palms, coves and coastal passages. **Sunscar Basin** has desert approaches, oases and sandstone ruins. **Tierras de Leyenda** dresses each fantasy army's starting land for its culture around a neutral highland centre. Each map has authored navigation/resource placement and its own environment, resource presentation, lighting and ambience. Decorative ships do not introduce naval warfare.

**Conquest** destroys the rival's Hearths. **Dominion** holds two of three beacons with military units for eight uninterrupted minutes; contested beacons interrupt the hold. Both sides start with a Hearth, four workers and equal resources. Pirates use Treasure Seekers and cannot recruit Tenders. Offline AI uses ordinary gathering, construction, research, scouting and combat commands.

**Menu** pauses a local match and offers resume, surrender and restart. **Main menu** preserves an unfinished local skirmish for **Resume your skirmish**. Online matches continue under server authority while menus are open. **Learn to play** retains the optional practice guide. **Settings** offers saved sound and camera controls, text contrast and an optional performance overlay. Local diagnostics are opt-in, bounded and manually exported; nothing uploads automatically.

To open the project, use **Unity 6000.3.23f1**, run **Emberfield > Verify project and prepare scenes** if generated scenes are missing, then open `Assets/Game/Scenes/Greybox.unity`. Editor and diagnostic launches retain access to their fixtures.

## Command your economy and army

Tap a worker, then a resource to gather or open ground to move. Drag to pan; use two fingers to pan and zoom. **Select** enables group selection; double-tap selects similar visible units. Mouse wheel, Shift-drag and hold-and-drag controls are also available. The minimap respects fog of war.

Resources become spendable when workers deliver cargo. Select workers to preview and confirm a building; **More buildings** exposes additional construction choices, including fortifications, the Beast Lodge and Siege Workshop. Accepted construction, training and research spend the same resource stocks. Shelters add population, Storeyards shorten delivery routes, and the research tree shows prerequisites and costs.

The shared spear/cavalry/archer counter triangle remains. Eligible creatures such as **Grove Guardian**, **War Troll** and **Ember Drake** have costs, population use and combat rules, and train at a Beast Lodge after their Era requirements. The mountain faction's **Frostguard** trains at the Muster Hall; the French Threadkeeper and Hispanic Ashrunner retain the former Aven/Serevin mechanics. Miraj's Dune Elephant and Solar's Sun Lion remain in legacy definitions, with those factions unavailable for new matches.

Build Curtain Walls, Fortress Gates, Watchtowers and Keeps. The Siege Workshop trains rams, ladders and siege towers. Rams damage fortifications and breaches open ground routes. Nearby infantry and archers use **Board / assault wall**; attacking an enemy wall requires a ready ladder or siege tower beside it. Boarding takes time, wall decks have limited capacity, and **Descend wall** lands on nearby clear ground. Gates open actual routes and cannot close on an occupied doorway. Defenses fire projectiles and use close-range oil attacks; wall collapse evacuates or kills occupants through simulation rules.

**Ember Drakes currently use ground navigation.** Wings and fire provide their visual identity; they do not fly over terrain or walls. Cavalry, creatures and siege equipment remain outside infantry wall decks.

## Read the HUD

The HUD shows stocks, resource rates, population, selection health, contextual construction/training/siege actions and the minimap. The tactical summary tracks own workers, idle workers, army, busy producers and confirmed orders per minute.

- **Offline income/min** counts delivered resources, excluding starting stock and undelivered cargo.
- **Online NET/min** is signed stock change including spending; the protocol does not expose gross delivery totals.
- **Orders/min** counts accepted commands, once per order. Online submissions count after the server acknowledges acceptance.
- The optional performance overlay describes recent local frame intervals and locally measured World ticks. It does not measure server CPU, GPU timing or display-present latency.

See [metric definitions and limits](docs/technical/ALPHA_METRICS.md). Reconnect observations cover the current client session, rather than reconstructing disconnected activity.

## Graphics and cosmetics

The **art overhaul vertical slice** contains Kingdom, Caribbean, Desert and Fantasy environments, nine character archetypes across 17 prefabs, three LODs per character, and close, medium and RTS cameras. Open the [new review gallery](Artifacts/ArtReview/overhaul/index.html) or `Builds/ArtOverhaul/ArtOverhaul.exe` to inspect the native 3D scenes, animation and team colors. The [art review](docs/art/art-overhaul-review.md) records visual improvements, remaining gaps against the references and validation limits. This is a separate art viewer; the playable alpha still uses its existing art. Mobile performance and final production quality remain unverified.

Rebuild the scenes and viewer with `./tools/Build-ArtOverhaul.ps1 -Player`; use `-PlayerOnly` to rebuild the viewer from existing validated assets after runtime changes. Run `./tools/Review-ArtOverhaul.ps1` for native player captures and checks. The earlier two-unit [ArtStyleLab checkpoint](docs/art/ART_STYLE_CHECKPOINT_2.md) and [gallery](Artifacts/ArtReview/index.html) remain available as the visual baseline.

Alpha 0.3 uses original procedural low-poly meshes with shared near/far detail, faction palettes, creature and siege silhouettes, biome dressing, shadows and post-processing. Creature motion, dragon wings and gate movement are driven by the game presentation. This is an editable alpha art system; it is not a finished production character/animation library. The illustrated menu background is decorative key art, separate from actual gameplay captures. [Asset provenance](docs/art/ALPHA_ASSET_PROVENANCE.md) documents the retained assets and licenses.

**Store > The Quartermaster** offers 12 architecture and army appearances with a live model preview. Local **free alpha previews** persist for offline play and never grant online ownership. A signed-in account can sync its server wardrobe and equip appearances it owns for its next room. Own and rival equipment remain separate in online matches; local previews never substitute for either participant's server equipment.

Skins change appearance while preserving team recognition, geometry, collision, damage, health, speed, cooldowns and economic rules. Prices are **provisional**. **Real purchases are unconfigured and unavailable**: no payment is taken. The backend includes durable entitlements, an idempotent purchase boundary and rejection of unverified/replayed receipts. Explicit server sandbox grants are labeled as tests. Selecting a provider, implementing checkout/refunds and completing platform store integration remain production work. See the [server guide](Server/README.md).

## Online play

Build and start the local authority from this folder:

```powershell
./tools/Build-Authority.ps1
node --experimental-sqlite Server/server.mjs
```

Choose **Multiplayer**, sign in with separate accounts in two player instances, select the same realm/map, host a private room, join its code and ready both players. Casual/ranked searches require another account in the same realm, map, queue and victory mode. Historical, Fantasy and Naval membership is enforced in private rooms, matchmaking and the C# authority. Rankings and history are scoped by realm. Only recognized pre-realm rating keys migrate into historical play; existing scoped ratings and past results remain unchanged.

Clients receive only their own private state and visible enemies. The server owns commands and results, while SQLite stores accounts, sessions, history, ratings and cosmetic entitlements. Re-login restores an active seat within the reconnect grace period; a server restart aborts active matches without rank awards. A temporary public HTTPS tunnel exposes the local alpha; its address and availability can change. Durable production hosting is still pending. [Server setup](Server/README.md) covers HTTPS, operation and service limits.

## Verify and build

Close this project's editor before Unity batch commands:

```powershell
./tools/Verify-Unity.ps1
./tools/Verify-ExpansionSimulation.ps1
./tools/Build-Unity.ps1 -Target Windows
./tools/Build-Authority.ps1
node --experimental-sqlite --test Server/tests/*.test.mjs
./tools/Smoke-Expansion.ps1 -Width 1280 -Height 720
./tools/Smoke-Online.ps1 -Width 1280 -Height 720
./tools/Smoke-Online.ps1 -Realm fantasy -MapId sapphire_coast -Width 1440 -Height 1080 -TimeoutSeconds 240
```

After an intentional rules/network/map source change, run `node Server/update-content-version.mjs` before rebuilding both client and authority. The server rejects a content mismatch. Generated builds, diagnostics and raw runs stay in ignored `Builds/` and `TestResults/`; reviewed evidence belongs in [Alpha 0.3 evidence](docs/testing/evidence/alpha03/).

The [current realm review](docs/technical/faction-realms-review.md) records this roster's validation. The [earlier Alpha 0.3 verification report](docs/testing/ALPHA_03_VERIFICATION.md) retains its source suites, combat/faction probes, naturally completed AI matches, packaged walkthroughs, two-client reconnect checks and visual captures. Automated completion is engineering evidence, not human balance or mobile performance qualification.

## Remaining limits and history

Human balance, readability and touch comfort need playtesting. No physical Android/iOS, battery, thermal or target-device FPS qualification is claimed; the audited workstation has no installed Android SDK/NDK/JDK support. Previous dense-choke/cold-order findings remain relevant until measured again for the expanded armies. Old desktop/offscreen timings must not be presented as Alpha 0.3 or mobile FPS.

There is no campaign, full naval warfare, large-team PvP, offline save/load or live payment provider. The temporary HTTPS tunnel is not a durable production deployment. Finished faction art, production hosting operations and platform store integration remain open.

Start with [current status](docs/tasks/CURRENT_PHASE.md), the [faction roster](docs/design/FACTION_GROUPS.md), the [realm verification report](docs/technical/faction-realms-review.md), and the [documentation index](docs/README.md). The [Alpha 0.3 expansion](docs/tasks/ALPHA_03_EXPANSION.md), [Alpha 0.2 report](docs/tasks/ALPHA_02_REPORT.md) and original Phase 0-13 reports retain their own evidence. The September biome/fantasy plan expands that history; its phase numbering does not restart the project. Supplied references remain unchanged. No paid service has been activated.
