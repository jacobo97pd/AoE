# Frontiers simulation and competitive content

This expansion keeps the existing fixed 20 Hz rules engine and stable historical IDs. Cosmetic selection is absent from `GameDefinition`, commands, combat calculations, economy, navigation and research. Purchases cannot supply an alternative unit definition or combat footprint.

## Competitive realms

`ContentRealms` defines two independent faction sets. Historical: Aven Compact (`aven`), Serevin March (`serevin`), Miraj Sultanate (`miraj`), Skeld Clans (`skeld`). Fantasy: Solar Kingdom (`solar`), Verdant Covenant (`verdant`), Ashen Dominion (`ashen`), Drakeforged Clans (`drakeforged`). `MapDefinition.RealmId` and each faction's `RealmId` must agree when `World` is constructed. Canonical IDs cannot be relabeled into another realm. Legacy maps with an omitted realm normalize to historical; neutral drill players remain supported.

The shared infantry, archers, cavalry, workers, buildings and siege equipment are available in both realms. Faction requirements still restrict every unique unit and technology. Creatures are ordinary paid, population-consuming army entities. The historical Dune Elephant belongs to Miraj; it cannot bring its faction into fantasy PvP.

## Faction mechanics

| Faction | Economic / military rule | Unique army or support | Unique research |
| --- | --- | --- | --- |
| Aven | Workers carry +1; paid local Logistics/Muster charter and Threadkeeper relay | Threadkeeper | Reciprocal Stores extends charter radius |
| Serevin | Cavalry speed +15% and workers carry +1; relocatable supply outposts and timed reposition | Ashrunner | Prepared Encampments shortens packing/deployment |
| Miraj | Workers carry +1 | Dune Elephant, structure damage and nearby-enemy stomp | Reinforced Howdahs: elephant armor +2 |
| Skeld | Infantry and cavalry armor +1 | Frostguard, heavy-infantry counter | Oathsteel: Frostguard attack +3 |
| Solar | After six seconds without damage or an attack target, units recover 2 HP/s within 5 m of a completed Hearth/Keep | Sun Lion, fast anti-archer creature | Solar Radiance: lion armor +2 |
| Verdant | Workers gather +1 wood per operation | Grove Guardian, slow armored frontline; 3 HP/s out of combat | Living Heartwood: guardian armor +2 |
| Ashen | Successful melee hits on enemy units restore 2 HP to their surviving attacker; hits on buildings do not | War Troll, anti-infantry attacks; 6 HP/s out of combat | Blood Pact: troll attack +4 |
| Drakeforged | Workers gather +1 metal and carry +1; creatures gain +1 armor | Ember Drake, ranged area fire and structure pressure | Runeforged Scales: drake armor +2 |

Faction passives were retuned on 13 September 2026 after the [rules audit](../audits/auditoria-reglas-2026-09-12.md), where Aven had won all 18 of its natural matches. The values come from 288-match samples in which each map is played from both starting bases:
- Aven's carry bonus fell from +2 to +1, and its Muster charter from +20% to +10%.
- Serevin's cavalry speed rose from +5% to +15%, and its workers now carry one extra resource.
- Miraj's carry bonus fell from +4 to +1.
- Skeld's armor now also covers cavalry.
- Drakeforged workers now carry one extra resource.

All new unique research costs 120 food and 120 metal, requires Dominion, takes 600 ticks and targets only its named unit. Effects retain shared paid weapon/armor upgrades and never stack from per-tick refreshes. Resource bonuses apply only to the named resource and owning player. Regeneration waits six seconds after damage, requires no attack target and cannot exceed maximum health.

| Army unit | Health | Population | Food / wood / metal | Speed m/s | Initial training era |
| --- | ---: | ---: | --- | ---: | --- |
| Dune Elephant | 560 | 5 | 320 / 80 / 100 | 2.2 | Dominion |
| Frostguard | 145 | 2 | 85 / 0 / 35 | 3.2 | Kingdom |
| Sun Lion | 220 | 3 | 170 / 0 / 70 | 4.6 | Kingdom |
| Grove Guardian | 560 | 4 | 190 / 180 / 40 | 2.0 | Kingdom |
| War Troll | 420 | 4 | 240 / 40 / 80 | 2.8 | Kingdom |
| Ember Drake | 380 | 5 | 280 / 0 / 180 | 3.3 | Dominion |

Spears retain a 1.8× soft counter against creatures. Guardians and trolls use Creature/Heavy tags, avoiding an unintended second anti-infantry weakness against archers. Drake fire deals 65% secondary damage within 1.6 m; elephant stomp does the same within 1.1 m. Secondary victims use their own armor and counter tags. The primary target receives one hit, friendly entities take no splash, and elevated deck occupants are excluded from ground splash. Separating an army materially changes these trades.

Creatures are army in Dominion too. Any armed unit tagged Infantry, Cavalry, Ranged or Creature captures and contests a beacon, so Grove Guardians and War Trolls hold ground like elephants, lions and drakes. Workers, packed outposts and siege equipment do not.

## Functional siege

The Siege Workshop trains ram, ladder and tower. Rams are armored slow attackers with a 10× structure multiplier, weak direct damage to troops and ordinary pathfinding. Destroyed buildings remove their navigation footprint and replan affected movement. The Beast Lodge trains faction-eligible creatures; Frostguards train at the Muster Hall.

`BoardWallCommand(playerId, unitIds, wallId, siegeUnitId)` admits infantry and archers. Soldiers must stand within 1.8 m of the wall edge. An enemy wall requires a friendly stationary ladder/tower within 1.3 m; defenders can climb their own wall without equipment. Own-wall climbing takes 40 ticks, an enemy ladder 100, and an enemy tower 40. Capacity includes soldiers already on the deck and pending reservations. Moving or losing the supporting equipment cancels unfinished boarding. Stop cancels a soldier's reservation.

A completed climb places the soldier at a deterministic deck position, 3 m above the ground, with +2 armor. Ranged units gain 1.5 m range. Ground melee cannot strike deck units; infantry occupying the same wall can fight each other, and projectiles can reach either layer. Ground movement ignores elevated bodies. `LeaveWallCommand` descends onto nearby clear ground on either side of the wall, cannot cross another obstacle, and releases the deck slot. If a wall collapses, occupants suffer 40% maximum-health fall damage and land at a nearby unoccupied legal position; units with no viable landing die.

`SetGateCommand` changes an owned completed gate. Opening updates navigation for **all** armies; closing rejects any ground unit obstructing the doorway. This makes an open gate a real defensive choice. A Watchtower fires authoritative ranged projectiles; the Keep uses heavier ranged damage. Walls, gates, watchtowers and keeps apply their configured close-range defensive oil damage and cooldown. Presentation observes projectiles and visible oil cooldowns; it never applies damage.

Walls and gates can stand turned a quarter so a fortification runs north-south: `BuildCommand` and map spawns take `Turned`, the building state swaps `WidthCells` and `DepthCells`, and navigation, fog, range, ladders, gates and descents all read that footprint. `BuildingState.IsTurned` is derived from it rather than stored. A deck spreads its slots along the wall's long side (`BuildingFootprints.DeckSlot`), so soldiers on a north-south wall stand north-south. A square footprint never turns.

`BuildRunCommand(playerId, workerIds, buildingDefinitionId, sites)` lays up to 64 wall stretches in one order. `WallRunLayout` turns the corners the player drags through into whole stretches: each leg runs east-west or north-south, and a later leg starts beside the previous leg's last cell, so corners meet without gaps or overlaps and a rectangle with equal opposite sides closes. `World.PreviewBuildRun` runs the command's own planning without changing the match and judges every stretch: accepted, `InsufficientResources` once the stock runs out, or why it cannot stand (fog, map edge, obstacle, unit, an earlier stretch of the same run, or no builder can reach it with the whole run standing). Submitting lays and pays for every accepted stretch at once. The workers build them one after another in list order, and a worker that can reach none of the remaining foundations leaves the run and idles rather than wait beside one. `WallRunTests` covers turned footprints, a closed enclosure, the building order, crossings, unreachable stretches, fog, replay, north-south decks and descents, a turned gate and the replica.

The deck system is a discrete simulation layer with fixed segment slots. It does not implement free movement across connected ramparts, multi-storey interiors or a full 3D navigation mesh. Drakes use ground navigation, with flight-like animation as presentation. Large creature models currently retain the engine's maximum 450 mm navigation radius; this is not a simulation of their full visual body volume. AI currently attacks fortifications with rams; ladder/tower boarding is available through player commands and verified scenarios, without a dedicated AI boarding planner.

New-faction AI reserves resources and population for a small creature contingent once its producer and era are available. In Conquest, after fifteen minutes with fewer than 100 food, any AI commits its surviving army and stops repeating low-health retreats. This prevents an exhausted economy from waiting indefinitely for the normal attack buildup. The decision reads the AI's own army, own food, match mode and elapsed time, and its attack orders still require visible enemy targets. It grants no resources and sets no result.

Every faction uses the same assault rule. It measures the army by military population, so creature and infantry armies compare fairly, and its thresholds come from the chosen `OfflineAiDifficulty`. Recruitment rotates through every soldier the producer can train and pay for now, including the naval realm's pirate company, so a hall never waits for metal it is not gathering. The enemy troops in view still favour their counters. The faction's own soldier is favoured until it makes up a third of the army. The AI's placement, regroup and scouting searches run in a frame mirrored through the map centre, so both seats of a mirrored map build and scout as mirror images. Once six workers are gathering, the AI also trains one longer-sighted worker, such as the treasure seeker.

The skirmish page chooses the difficulty. `MatchController.StartOfflineMatch` carries it across the scene load and **Play again** keeps it. The limits come from `OfflineAiTuning`:

| Limit | Easy | Normal (default) | Hard |
| --- | --- | --- | --- |
| Thinking cadence | 10 ticks, 4 orders | 10 ticks, 4 orders | 6 ticks, 6 orders |
| Workers | 12 | 16 | 20 |
| Muster Halls | 2; the second at 8 soldiers | 2; the second at 6 soldiers | 3; at 6 and 12 soldiers |
| Research | From 4 soldiers | From 4 soldiers | From 4 soldiers, plus 3 more per technology started |
| Other producers keep training while one of their kind researches | No | Yes | Yes |
| Assault | Empire, 8 technologies, 26 military population, 10 workers | Kingdom, 2 technologies, 18 population, 12 workers, from minute 7 | Kingdom, 2 technologies, 24 population, 14 workers, from minute 6 |
| Home guard radius | 18 m | 24 m | 28 m |
| Focused engagement: targets from the army's centre, soldiers already fighting keep their target, no retreats while defending at home | No | Yes | Yes |

Easy keeps the thresholds of the opponent that existed before the rules audit. The recruitment rules, the treasure seeker and the mirrored searches described above apply to every difficulty. Development players also accept `-emberfieldDifficulty Easy|Normal|Hard` for a scene started without the skirmish page.

## Verification

`Assets/Tests/EditMode/ExpansionSimulationTests.cs` covers realm rejection, paid unique technology isolation, stable armor, resource-specific harvesting, lifesteal against units but not structures, delayed regeneration, boarding timing/capacity, equipment cancellation, actual enemy-wall crossing, elevated attacks, gate obstruction, ram breaches, collapse evacuation, oil damage and projectile/melee splash. Network siege observation tests separately cover the authority-to-replica view.

Run `tools/Verify-ExpansionSimulation.ps1` for an independent .NET verification executable compiled with Unity's existing Roslyn compiler. It does not launch Unity or install another SDK. The runner freezes and hashes the rules and three map files before testing. Its default matrix contains:

- Eight production scenarios advancing through all four eras, paying unique research, and training the faction unit plus all three siege engines.
- Thirty same-realm creature trades against spears, archers and cavalry, at equal population and at equal total raw-resource budget.
- Five actual 20-second damage measurements and a dispersed-archer focus scenario.
- Forty-eight natural AI matches: eight starting factions × three maps × Conquest/Dominion. Natural matches use authored resources and ordinary commands, with fog-restricted observations; no result is forced.

Production/combat fixtures deliberately begin with test infrastructure or deployed armies. Their reports state this distinction. Equal-resource budget sums the four raw resource amounts without claiming that different resources have identical strategic value. Natural matches record time limits, progress, army types, resource stocks and rejection reasons; an unfinished case is reported explicitly. These automated exercises test rules and pacing and do not establish ranked human balance or rendered/mobile frame rate.
