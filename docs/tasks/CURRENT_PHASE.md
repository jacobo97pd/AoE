# Current phase - Alpha 0.3: Realms and Frontiers

**Latest work — September 14, 2026: three separate faction realms.** The active roster is Historical 3 / Fantasy 4 / Naval 1, with three additional naval factions planned and locked. The [faction catalog](../design/FACTION_GROUPS.md) and [realm review](../technical/faction-realms-review.md) own the current scope and validation. The earlier two-unit ArtStyleLab [checkpoint report](../art/ART_STYLE_CHECKPOINT_2.md) and [review gallery](../../Artifacts/ArtReview/index.html) remain historical art evidence; their earlier pause does not describe the current authorized faction work.

The earlier Alpha 0.3 systems and Windows package passed their recorded acceptance. The current faction update builds on those results, Alpha 0.2 and the completed original Phases 0-13, without renumbering or restarting those milestones. New package evidence is recorded separately in the realm review. Production/device gates below remain open.

The [earlier Alpha 0.3 verification report](../testing/ALPHA_03_VERIFICATION.md) and [artifacts](../testing/evidence/alpha03/) retain their original source totals, build identity and packaged matrix. Current totals and limitations belong in the [realm review](../technical/faction-realms-review.md); an earlier build GUID, screenshot or timing does not establish the current package's result.

## Implemented scope

- **Twelve active factions:** Históricas — Franceses, Hispanos, Ingleses, Sultanato, Confederación del Sahel (the desert pair since 2026-09-28, [design](../design/DESERT_FACTIONS.md)); Fantasía — Orcos, Enanos, Hombres de las montañas, Elfos; Navales — Piratas, Marina inglesa, Marina española (the navies since 2026-10-02, [design](../design/NAVAL_SLICE.md)). The Flota esquelética remains planned and locked. Miraj/Solar remain legacy definitions and history only.
- **Three separate PvP realms:** private rooms, queues and authority creation reject cross-realm armies. MMR, visible rank and history are scoped by realm. Unknown or locked choices fail validation; migration preserves existing scoped ratings and historical results.
- **Four playable maps:** Amber Crossing (temperate forest), Sapphire Coast (Caribbean), Sunscar Basin (desert) and Tierras de Leyenda (`legend_lands`, cultural lands around a neutral highland centre), with distinct map layouts and resource placement. Historical and Fantasy admit the first three maps; Fantasy also has Tierras de Leyenda as its exclusive default. Naval admits only Sapphire Coast. Naval adds docks, ship navigation, naval combat and landings with three hulls: sloop, frigate and galleon.
- **Creature armies:** Grove Guardians, War Trolls and Ember Drakes retain resource/population costs, Era/faction requirements and counters. Dune Elephants and Sun Lions remain in legacy faction definitions. Drakes currently use ground navigation.
- **Functional siege:** rams, ladders and siege towers; constructed walls, gates, Watchtowers and Keeps; timed boarding, limited wall occupancy, wall-height combat, descent, breaches, gate navigation, defensive projectiles/oil and collapse consequences. HUD actions submit actual game commands and the online protocol carries observable siege state.
- **Product and art:** realm/faction/map setup, the retained Home/settings/guide, contextual HUD and measured session metrics, original procedural low-poly faction/creature/siege geometry, biome lighting and live store model previews.
- **Twelve cosmetic appearances:** local free offline previews plus a separate durable server wardrobe. Server entitlements control online equipment. Receipts have a trusted verification boundary, idempotency and replay rejection. Prices are provisional and real purchases remain unavailable without provider/checkout integration.

The [current faction catalog](../design/FACTION_GROUPS.md) explains the active roster and limits. The [earlier expansion implementation record](ALPHA_03_EXPANSION.md) preserves its original decisions and controls. Existing economy, four Eras, research, fog, Conquest/Dominion, strategic AI, reconnect, accounts and optional diagnostics remain part of the game.

## Verification checkpoint

Current source, service/authority and package-specific outcomes are recorded in the [realm review](../technical/faction-realms-review.md). The [earlier verification report](../testing/ALPHA_03_VERIFICATION.md) and [gallery](../testing/evidence/alpha03/gallery.html) preserve their combat/faction probes, natural AI matches, camera/input checks and packaged runs for that earlier build.

Natural matches finish through ordinary AI commands and game victory conditions. Packaged online validation uses two actual Windows players, a C# authority and HTTP/SQLite, including a killed/restarted guest, recovered commands, realm-scoped history and return to lobby. Those checks do not establish human balance, WAN reliability or production-scale capacity.

## Open and play

A normal Windows launch opens **Home**. **Play** selects a realm, faction, map and victory mode. Workers can open **More buildings** for fortifications and the Beast Lodge/Siege Workshop. Eligible infantry use **Board / assault wall** and **Descend wall**; select an owned gate to open or close its route.

**Store** previews appearances without payment. **Sync online wardrobe** requires sign-in through Multiplayer; local previews do not grant online ownership. Online games continue when menus are open. See the [main README](../../README.md) and [server guide](../../Server/README.md) for startup and validation commands.

## Remaining acceptance and production work

- Human playtests for balance, match pacing, readability and touch comfort.
- Physical mobile builds, input, thermal/battery behavior and device performance; no mobile FPS result is claimed.
- Expanded-army performance measurements, including dense chokes and cold orders. Historical desktop timings remain historical.
- Production art/animation and flying-unit navigation; current visuals are procedural low-poly and drakes follow ground paths.
- Durable production hosting, operations, account recovery and production load validation. A temporary public HTTPS tunnel currently exposes the local alpha; its address and availability can change.
- Payment provider, real checkout, refunds/revocation and platform store approval. No real purchase or paid service has been activated.

Original reports remain available in the [documentation index](../README.md), including [Alpha 0.2](ALPHA_02_REPORT.md) and [Phase 13](PHASE_13_REPORT.md). Supplied documents remain unchanged; this implementation preserves the project's original content and history.
