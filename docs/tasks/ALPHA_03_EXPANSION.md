# Alpha 0.3 - Realms and Frontiers

The September 10 biome/fantasy plan has been implemented as an expansion of Emberfield Alpha 0.2. This record describes shipped-source behavior and its limits. Final Windows package acceptance and captures are complete; the [verification report](../testing/ALPHA_03_VERIFICATION.md) records exact totals, build identity and outcomes, supported by [Alpha 0.3 evidence](../testing/evidence/alpha03/).

The original master prompt and Phases 0-13 remain the foundation. The later PDF's phase labels describe an expanded scope and do not start a new project or replace historical evidence.

## Product decisions implemented

The user's clarification establishes two independent PvP ecosystems: historical and fantasy armies cannot face one another, including in private rooms. Dragons, trolls, giant elephants and other creatures are army units. Purchasable appearances may change their look, but cannot change a competitive rule.

The existing Aven and Serevin identities are retained. The roster now reaches four historical and four fantasy factions, with stable IDs, faction mechanics, a signature unit and a faction technology.

| Realm | Faction | Signature unit | Implemented identity |
| --- | --- | --- | --- |
| Historical | Aven Compact | Threadkeeper | Storeyard charters, logistics and temporary supply relays |
| Historical | Serevin March | Ashrunner | Cavalry mobility, repositioning and relocatable Supply Outposts |
| Historical | Miraj Sultanate | Dune Elephant | Increased carrying capacity and costly elephant armies |
| Historical | Skeld Clans | Frostguard | Armored infantry and resilient northern formations |
| Fantasy | Solar Kingdom | Sun Lion | Recovery near friendly Hearths/Keeps after peaceful time and Sun Lion armies |
| Fantasy | Verdant Covenant | Grove Guardian | Wood gathering, woodland armies and recovering guardians |
| Fantasy | Ashen Dominion | War Troll | Melee sustain and regenerating trolls, vulnerable to range and concentrated attacks |
| Fantasy | Drakeforged Clans | Ember Drake | Metal gathering, creature armor and costly ranged fire |

The shared food/wood/metal/stone economy, four Eras, population, production/research opportunity costs and counter system still constrain these armies. Runtime definitions own numerical balance; a faction or skin name does not imply an additional unimplemented ability.

## Three playable biomes

| Battlefield | Stable map ID | Biome and presentation |
| --- | --- | --- |
| Amber Crossing | `amber_crossing` | Temperate forest, wooded approaches, river crossings and frontier settlements |
| Sapphire Coast | `sapphire_coast` | Caribbean palms, coves, coastal passages and tropical resource presentation |
| Sunscar Basin | `sunscar_basin` | Desert approaches, oasis vegetation, sandstone ruins and arid resource presentation |

The maps have authored blocked cells, starts and resources, alongside distinct terrain, environmental props, water treatment, lighting and ambience. They are available to either realm without allowing mixed armies. Decorative ships remain props; there is no naval combat system. Original public map data and observed entities remain separate from private enemy state.

## Creatures and siege

Dune Elephants, Sun Lions, Grove Guardians, War Trolls and Ember Drakes are trained at the Beast Lodge subject to their faction and Era prerequisites. They consume resources and population and obey authoritative combat rules, including regeneration or area attacks where their definitions provide them. The Frostguard remains an infantry unit trained at the Muster Hall.

Ember Drakes use ground pathfinding and cannot bypass walls or terrain by flying. Wing motion and fire effects do not grant a flight mechanic. Creature silhouettes and population costs communicate scale, while counterplay still requires human validation.

The Siege Workshop trains rams, ladders and siege towers. Players construct Curtain Walls, Fortress Gates, Watchtowers and Keeps. The implemented interaction includes:

- Rams damage fortifications; destruction removes the obstacle and opens a ground route.
- Infantry and archers board nearby friendly walls. Enemy boarding needs a ready ladder or siege tower beside the target and takes time; moving or losing required equipment interrupts the attempt.
- Wall decks have finite slots and logical elevation. Elevated defenders can attack with the wall combat rules, while ground melee cannot freely strike the deck.
- **Descend wall** selects clear ground immediately beside the wall. Creatures, cavalry and siege equipment do not occupy infantry decks.
- Opening a gate changes navigation. Closing it rejects an occupied doorway.
- Defensive buildings fire projectiles and trigger close-range oil attacks. Wall destruction evacuates, damages or kills occupants according to available landing space.

The HUD exposes construction pages, training choices and siege orders. Commands, ownership, visibility and costs are validated by the shared simulation, with corresponding online command and observation fields. Geometry alone does not decide whether a wall, breach or gate is passable.

## Frontend, HUD and presentation

Home and skirmish setup now offer realm, faction, biome and victory-mode choices. The tactical HUD retains delivered-income versus online-net-rate distinctions, population, own-army metrics, selection health, minimap and confirmed-order measurements, and adds reachable contextual siege controls. Local menus pause local simulation; online authority continues while a client browses menus.

World art uses original procedural low-poly geometry, shared detail meshes, readable team markings, creature/siege silhouettes and biome-specific lighting and dressing. Dragon wings, gate movement, attacks and oil effects are presentation of game state. These assets are an alpha implementation, with production modeling, skeletal animation and final art direction still open. The main-menu illustration is decorative key art and must never be labeled as a gameplay capture. [Provenance](../art/ALPHA_ASSET_PROVENANCE.md) documents retained asset origins and licenses.

## Cosmetics, ownership and payments

The Quartermaster contains **12 appearances**: architecture collections and army/creature skins, including dragon variants, elephant armor, trolls, guardians, lions and Frostguards. The selected appearance has a live model preview. Listed prices are provisional.

Local **free alpha previews** persist for offline use and do not create purchases or online entitlements. Online wardrobe sync reads authenticated server ownership. An owned item can be equipped for the next room; the room records eligible equipment and publishes cosmetic metadata separately from simulation inputs. Own and opponent appearances use separate loadouts, and local previews never substitute for server-owned equipment.

Cosmetic metadata cannot change health, damage, armor, cooldowns, speed, population, navigation, visibility, collision or economy. The catalog validates supported appearance fields/targets and rejects gameplay fields. Authentication/session guards prevent late wardrobe responses or old account data from replacing another session's appearance.

SQLite stores entitlements, equipped items and idempotent claims. The purchase boundary requires a trusted provider verification result bound to the account/product and prevents transaction replay. Explicit server sandbox claims are labeled as tests. **Real payments remain unconfigured and unavailable**: no payment is taken and no paid service is enabled. Provider selection, real checkout, refunds/revocation, credentials and platform store integration remain production work. The [server guide](../../Server/README.md) defines the boundary and operator configuration.

## Separate competitive realms

Historical/fantasy membership is checked when creating or joining a private room, matching a queue and creating the authoritative C# World. Unknown content fails closed. Queue matching also respects map, victory mode and casual/ranked choice. Ratings and history use a realm namespace; existing account results/ratings migrate to historical play.

Protocol 2 carries realm/biome identity, creature state through ordinary unit observations, and observable wall/gate/defense state. Each participant receives their own private state plus visible enemies; equipment metadata never enters the authoritative World or competitive content hash. Client restart restores the seat and accepted-command cursor within the reconnect window. Changing server identity replaces an incompatible replica even when the username is the same.

Public deployment, recovery e-mail, multi-region operation, sustained population testing and production payments are not activated by these local systems.

## Verification and remaining acceptance

Source verification covers existing economy/combat/input/network behavior and new realm, creature, siege, snapshot, cosmetic and session-isolation cases. The expanded verifier checks combat/faction cases and naturally completed AI matches across the roster, maps and victory modes. These checks, camera/input regressions and final Windows package acceptance all passed their recorded runs.

Packaged acceptance includes frontend/store/setup walkthroughs, actual game captures, and two-player online tests for historical/Amber Crossing and fantasy/Sapphire Coast. The online driver uses native lobby controls, kills and restarts the guest, checks recovered commands and realm-scoped history, and returns both clients to their lobbies. A prepared test or previous build is not a pass for the final executable; consult the [verification report](../testing/ALPHA_03_VERIFICATION.md) and [evidence directory](../testing/evidence/alpha03/) for completed runs.

No physical Android/iOS input, thermal, battery or FPS qualification is claimed. Existing dense-choke/cold-order findings need renewed measurements for expanded armies. Human balance, fun and readability, production art/animation, flying navigation, public hosting and real payments remain open. Full naval warfare, campaigns, large-team modes and offline save/load are outside this implemented alpha.
