# Emberfield project documentation

**Actualización del 14 de septiembre de 2026:** el [catálogo de facciones](design/FACTION_GROUPS.md) separa Históricas (Franceses, Hispanos, Ingleses), Fantasía (Orcos, Enanos, Hombres de las montañas, Elfos) y Navales (Piratas; tres flotas pendientes). El catálogo y las [pruebas de ámbitos](technical/faction-realms-review.md) sustituyen los nombres y grupos de los informes históricos que siguen. Navales ofrece desembarcos terrestres; el combate entre barcos sigue pendiente.

**Alpha 0.3: Realms and Frontiers**, with the September 14 faction update, has eight active factions across Historical, Fantasy and Naval, three playable biomes, creature armies, functional siege and a 12-appearance store. The three realms have separate PvP opponents, rankings and history. Free offline cosmetic previews are separate from authenticated server ownership; real payments remain unconfigured and listed prices provisional.

Start with [current status](tasks/CURRENT_PHASE.md), the [faction catalog](design/FACTION_GROUPS.md) and [realm verification](technical/faction-realms-review.md). The earlier [expansion implementation record](tasks/ALPHA_03_EXPANSION.md), [Alpha 0.3 verification report](testing/ALPHA_03_VERIFICATION.md), [evidence](testing/evidence/alpha03/) and [visual gallery](testing/evidence/alpha03/gallery.html) retain the acceptance results and build identity of their original versions.

## Alpha 0.3 implementation and operation

| Document | Purpose |
| --- | --- |
| [ArtStyleLab checkpoint 2](art/ART_STYLE_CHECKPOINT_2.md) | Two new rigged Aven candidates, 13 reference analyses, native captures, measured asset budgets and pending human art approval; [review gallery](../Artifacts/ArtReview/index.html) |
| [Current status](tasks/CURRENT_PHASE.md) | Implemented systems, current acceptance checkpoint and open production work |
| [Faction catalog](design/FACTION_GROUPS.md) | Current Historical 3 / Fantasy 4 / Naval 1 roster, locked choices and map limits |
| [Realm verification](technical/faction-realms-review.md) | Current faction migration tests, load evidence, build identities and limitations |
| [Realms and Frontiers](tasks/ALPHA_03_EXPANSION.md) | Earlier expansion record: original factions, biome maps, creatures, siege and cosmetics |
| [Earlier verification report](testing/ALPHA_03_VERIFICATION.md) | Source and packaged outcomes, build identity and screenshots for that Alpha 0.3 release |
| [Alpha 0.3 artifacts](testing/evidence/alpha03/) | Reviewed test reports, packaged walkthroughs and actual gameplay captures |
| [Complete visual atlas](art/visual-atlas/index.html) | 211 native Unity inspection views: units, faction appearances, buildings, resources, scenery, cosmetics and all three playable maps; [usage and provenance](art/visual-atlas/README.md) |
| [Gameplay metrics](technical/ALPHA_METRICS.md) | Delivered income versus online net stock, accepted orders and timing limitations |
| [Asset provenance](art/ALPHA_ASSET_PROVENANCE.md) | Retained native geometry/vector UI, decorative illustration and font sources/licenses |
| [Server operator guide](../Server/README.md) | Realm isolation, accounts, queues, durable wardrobe, payment boundary, local authority and HTTPS configuration |
| [Multiplayer architecture](technical/MULTIPLAYER_ARCHITECTURE.md) | The retained command/observation and authority boundaries; protocol 2 details are in the server guide |
| [Backend architecture](technical/BACKEND_ARCHITECTURE.md) | Persistence/session foundation; expansion realm and cosmetic behavior is recorded above |

The active factions are **Franceses, Hispanos and Ingleses** in Historical; **Orcos, Enanos, Hombres de las montañas and Elfos** in Fantasy; and **Piratas** in Naval. Historical and Fantasy can use **Amber Crossing**, **Sapphire Coast** and **Sunscar Basin**; Naval uses **Sapphire Coast** only and currently supports land battles with pirate mirrors. Miraj/Solar are legacy entries; the three additional naval fleets remain planned and locked. This organization does not imply new ships or finished national/orc model collections.

Ember Drakes use ground navigation. Human balance, finished faction art/animation, physical mobile testing, target-device performance, durable production hosting and live payments remain open; no desktop/offscreen timing is a mobile FPS claim. A temporary public HTTPS tunnel exposes the local alpha, with an address and availability that can change.

## Historical checkpoints

Earlier reports retain evidence for their own source/build versions. The original Phase 0-13 sequence remains intact:

- [Alpha 0.2 - frontend, HUD and first alpha world presentation](tasks/ALPHA_02_REPORT.md).
- [Phase 0 — foundation](tasks/PHASE_0_REPORT.md), [Phase 1 — greybox](tasks/PHASE_1_REPORT.md), [Phase 2 — economy](tasks/PHASE_2_REPORT.md), [Phase 3 — combat](tasks/PHASE_3_REPORT.md).
- [Phase 4 — movement](tasks/PHASE_4_REPORT.md), [Phase 5 — Eras and technology](tasks/PHASE_5_REPORT.md), [Phase 6 — factions](tasks/PHASE_6_REPORT.md).
- [Phase 7 — full offline match](tasks/PHASE_7_REPORT.md), [Phase 8 — mobile UX](tasks/PHASE_8_REPORT.md), [Phase 9 — first art slice](tasks/PHASE_9_REPORT.md), [Phase 10 — measured optimization](tasks/PHASE_10_REPORT.md).
- [Phase 11 — private multiplayer](tasks/PHASE_11_REPORT.md), [Phase 12 — accounts and matchmaking](tasks/PHASE_12_REPORT.md), [Phase 13 — alpha productization](tasks/PHASE_13_REPORT.md).

The [initial audit](audits/initial-project-audit.md) records the detected environment. The [development plan](tasks/DEVELOPMENT_PLAN.md) retains phase requirements and acceptance gates. Historical screenshots, test totals and timings are not current Alpha 0.3 measurements.

## Design

| Document | Purpose |
| --- | --- |
| [Game vision](design/GAME_VISION.md) | Product, audience and fairness |
| [Game design document](design/GAME_DESIGN_DOCUMENT.md) | Rules and system relationships |
| [MVP scope](design/MVP_SCOPE.md) | Scope decisions and exclusions |
| [Faction bible](design/FACTION_BIBLE.md) | Faction mechanics and counterplay |
| [Unit roster](design/UNIT_ROSTER.md) | Roles and counter language |
| [Building roster](design/BUILDING_ROSTER.md) | Economic, military and territorial infrastructure |
| [Technology tree](design/TECHNOLOGY_TREE.md) | Eras, research and prerequisites |
| [Map design](design/MAP_DESIGN.md) | Map structure and intended variation |
| [Reference design lessons](design/REFERENCE_DESIGN_LESSONS.md) | Systems lessons from the supplied study |

## Engineering, art and validation

| Document | Purpose |
| --- | --- |
| [Technical design](technical/TECHNICAL_DESIGN_DOCUMENT.md) | Runtime boundaries and implementation decisions |
| [Simulation architecture](technical/SIMULATION_ARCHITECTURE.md) | State, commands and fixed ticks |
| [Pathfinding architecture](technical/PATHFINDING_ARCHITECTURE.md) | Navigation scope and benchmarks |
| [Performance budget](technical/PERFORMANCE_BUDGET.md) | Targets and measured limitations |
| [Art bible](art/ART_BIBLE.md) | Original visual identity and readability |
| [Asset pipeline](art/ASSET_PIPELINE.md) | Modeling, import and delivery |
| [UI/UX guide](art/UI_UX_GUIDE.md) | Touch controls and layout principles |
| [Voice controls](technical/VOICE_CONTROLS.md) | Optional Spanish/English voice commands, recognizer boundary and privacy |
| [Languages](technical/LOCALIZATION.md) | Spanish (Spain) by default, English second; display-time translation catalogs and how to add text |
| [Test strategy](testing/TEST_STRATEGY.md) | Verification scope and evidence |
| [Movement stress](testing/MOVEMENT_STRESS.md) | Reproducible fixtures and desktop measurement limits |

Design documents describe intent unless implementation and verification are explicitly recorded. Some roster/scope documents retain the earlier MVP; use the Alpha 0.3 expansion record for the current implemented roster and the verification report for accepted behavior. Runtime configuration owns numeric balance. Preserve the supplied [reference PDF](references/Age_of_Empires_IV_Estudio_Integral.pdf) unchanged; its names, assets and wording are not a content source for Emberfield.
