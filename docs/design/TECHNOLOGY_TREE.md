# Technologies and Eras

Status: the shared four-Era tree and Phase 6 faction research are implemented and verified in simulation, UI integration and packaged Windows walkthroughs at 1280 x 720 and 1440 x 1080. [greybox.json](../../Assets/Game/Resources/Definitions/greybox.json) owns active content; the [Phase 5 report](../tasks/PHASE_5_REPORT.md) records the shared-tree checkpoint and the [faction bible](FACTION_BIBLE.md) links current faction evidence. Values below are prototype tuning, not established competitive balance.

Phase 6 adds faction-only Reciprocal Stores and Prepared Encampments below. Simulation checks verify their requirements and effects; PlayMode checks verify faction-specific UI availability. Both factions' packaged walkthroughs gather and pay for unique research at both resolutions. The nine shared Phase 5 research definitions and costs are unchanged.

## Four-Era progression

| Tier / ID | Display name | Available choices in this slice |
| --- | --- | --- |
| 1 / `settlement` | Settlement | Shared worker/military roster and early buildings; Phase 6 adds the eligible faction recruit and Serevin Supply Outpost. |
| 2 / `kingdom` | Kingdom | Archive; Better Handles, Tempered Edges and Layered Gear; Phase 6 adds the eligible faction's unique research. |
| 3 / `dominion` | Dominion | Work Rotation, which also becomes a prerequisite for Empire. |
| 4 / `empire` | Empire | Measured Strikes and Interlocked Plates. |

Every player starts in Settlement. Advancement follows Settlement → Kingdom → Dominion → Empire through paid Hearth research. The existing military roster remains available in Settlement so earlier economy/combat drills keep their starting choices. This slice unlocks a research building and upgrade tiers; it does not introduce heavy units, siege or additional unit production structures. Optional Era/technology requirements on unit and building definitions support later content gates.

## Configured research tree

Costs use F = Food, W = Wood, M = Metal and S = Stone. Time is fixed research ticks at 20 ticks/second. The listed Era is the required tier; Era advancement proceeds from its immediate predecessor. The selected research building must be owned, complete and available.

| ID / display name | Building | Era and additional prerequisites | Cost | Work / nominal time | Completion |
| --- | --- | --- | --- | --- | --- |
| `advance_kingdom` / Advance to Kingdom | Hearth | Settlement | 200 F, 100 W | 600 ticks / 30 s | Enter Kingdom; unlock Archive and first research tier. |
| `advance_dominion` / Advance to Dominion | Hearth | Kingdom; own a completed Archive | 400 F, 200 W, 100 M | 900 ticks / 45 s | Enter Dominion. |
| `advance_empire` / Advance to Empire | Hearth | Dominion; Work Rotation complete | 600 F, 300 M, 200 S | 1200 ticks / 60 s | Enter Empire. |
| `gather_1` / Better Handles | Archive | Kingdom | 60 F, 80 W | 400 ticks / 20 s | +1 worker gather amount per operation. |
| `gather_2` / Work Rotation | Archive | Dominion; Better Handles complete | 120 F, 120 W | 600 ticks / 30 s | A further +1 worker gather amount per operation. |
| `weapons_1` / Tempered Edges | Muster Hall | Kingdom | 80 F, 60 M | 400 ticks / 20 s | +2 attack damage for soldiers and creatures; siege engines excluded. |
| `weapons_2` / Measured Strikes | Muster Hall | Empire; Tempered Edges complete | 180 F, 180 M | 600 ticks / 30 s | A further +2 attack damage for soldiers and creatures; siege engines excluded. |
| `armor_1` / Layered Gear | Muster Hall | Kingdom | 60 W, 80 M | 400 ticks / 20 s | +1 armor for soldiers, creatures and siege equipment. |
| `armor_2` / Interlocked Plates | Muster Hall | Empire; Layered Gear complete | 120 W, 200 M | 600 ticks / 30 s | A further +1 armor for soldiers, creatures and siege equipment. |
| `aven_stores` / Reciprocal Stores | Archive | Kingdom; Aven Compact | 120 F, 100 W | 600 ticks / 30 s | Chartered Storeyard coverage and Threadkeeper tether radius +2 m; relay radius unchanged. |
| `serevin_camps` / Prepared Encampments | Archive | Kingdom; Serevin March | 100 F, 100 W | 600 ticks / 30 s | Supply Outpost packing and redeployment each require 40 fewer ticks: 5 s becomes 3 s. |
| `miraj_howdahs` / Reinforced Howdahs | Archive | Dominion; Miraj Sultanate | 120 F, 120 M | 600 ticks / 30 s | Dune Elephant armor +2. |
| `skeld_oathsteel` / Oathsteel | Archive | Dominion; Skeld Clans | 120 F, 120 M | 600 ticks / 30 s | Frostguard attack +3. |
| `solar_radiance` / Solar Radiance | Archive | Dominion; Solar Kingdom | 120 F, 120 M | 600 ticks / 30 s | Sun Lion armor +2. |
| `verdant_heartwood` / Living Heartwood | Archive | Dominion; Verdant Covenant | 120 F, 120 M | 600 ticks / 30 s | Grove Guardian armor +2. |
| `ashen_bloodpact` / Blood Pact | Archive | Dominion; Ashen Dominion | 120 F, 120 M | 600 ticks / 30 s | War Troll attack +4. |
| `drakeforged_scales` / Runeforged Scales | Archive | Dominion; Drakeforged Clans | 120 F, 120 M | 600 ticks / 30 s | Ember Drake armor +2. |
| `sultanate_composite_bows` / Composite Bows | Archive | Dominion; Sultanato | 120 F, 120 M | 600 ticks / 30 s | Camel Archer attack +2. |
| `sahel_quilted_barding` / Quilted Barding | Archive | Dominion; Confederación del Sahel | 120 F, 120 M | 600 ticks / 30 s | Quilted Lancer armor +2. |

The configured tree has 19 research items: three Era advancements, six shared upgrades, eight faction items from the original roster, including the legacy Miraj and Solar definitions, and the two desert factions' items. These are not one item for each of the ten currently selectable factions: English and Pirates have no unique research item. The economic branch is Better Handles → Work Rotation → Empire eligibility. The weapon and armor branches each have one Kingdom tier and one Empire tier. Advancing an Era does not complete any equipment or economic upgrade automatically. An Archive costs a separate 120 W and 40 S, so Dominion requires a physical investment as well as its Hearth research payment.

The eight faction entries use `RequiredFactionId`. Aven and Serevin reference faction definition values; the six Frontiers entries target only their named unit. Each faction can research only its own item, and none is available in the neutral maps. All require a completed Archive, compete with its other research, and follow the same cost, duplicate and producer-loss rules. They neither advance an Era nor unlock another faction's content. See the [faction bible](FACTION_BIBLE.md) for coverage, relocation and counterplay.

## Production, interruption and effects

Research spends its entire cost when accepted. Each building can run one research item and cannot queue more research. A producer's training queue must be empty before research starts; training is blocked while that building researches. Hearth advancement therefore competes with Tender production, and military upgrades compete with the Muster Hall's army output. Different buildings can research different eligible technologies in parallel, but a player cannot start a second copy of an active or completed technology.

Destroying the research producer loses that item's paid cost and unfinished progress. The pending technology is released so the player can retry at an eligible completed building, paying the full cost again. Completed research remains owned after a building is destroyed. Manual research cancellation and refund options are outside this slice.

Prerequisites are checked when research starts. Losing a separate required building afterward does not cancel accepted work. At most one Era advancement may be pending for a player; advancing skips no tier.

Completed effects apply once to the owner's existing and subsequently trained units. They are additive match modifiers, not changes to shared base definitions or account statistics. Gather upgrades match the Worker tag (`1`). Weapon upgrades match any Infantry, Cavalry, Ranged or Creature tag (`142`). They deliberately exclude Siege, because the ram's 10× structure multiplier would otherwise turn +4 base damage into +40 per hit against buildings. Armor upgrades also cover siege equipment (`398`). Buildings receive no armor bonus from these military technologies.

The Tender's base gather amount is 2, becoming 3 and then 4 per operation; cargo capacity, operation interval and physical drop-off remain separate rules. Attack bonuses add to base damage before the existing highest matching counter multiplier and target armor, retaining minimum-one damage. A projectile snapshots its calculated damage when launched. Both weapon tiers total +4 damage, and both armor tiers total +2 armor.

## Validation and limits

Verified checks cover stable IDs, prerequisite references and cycles, reachable Era/building/technology chains, producer eligibility, completed-building requirements, Era order, resources and duplicate/pending research. Invalid requests reject atomically without consuming stock or disturbing production. Existing and future units resolve the same completed modifiers. Tests cover all four Eras, early-research rejection, queue contention, producer destruction/retry, completion ordering, projectile snapshots, cargo bounds and additive overflow. Both packaged-player walkthroughs physically gathered resources and completed all nine technologies without changing starting stock.

These purchases establish an economy-versus-production choice, but full-match pacing, counter balance after upgrades and strategic research schedules still need playtesting. Research is per match. Phase 6 checks verify faction requirements, unique effects and own-faction research card gates; four final packaged faction walkthroughs also complete the eligible unique research. Tick advancement and UI dispatch are scripted, so these desktop checks do not establish physical touch usability or real-time research pacing. No account upgrades, saved progression or hidden-enemy research disclosure rules are implemented by this slice.

## Deferred research proposals

Route Discipline (`movement_1`, movement specialization), Braced Foundations (`defense_1`, defensive structures), and Frame Engineering (`siege_1`, siege) remain future design only and are absent from the configured technology list. Their costs, prerequisites and effects are undecided; their appearance here does not unlock gameplay.
