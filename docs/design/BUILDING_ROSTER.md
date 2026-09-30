# Building roster

Status: Hearth, Shelter, Storeyard, Muster Hall and Archive are verified through Phase 5 simulation/integration checks and desktop packaged-player walkthroughs at phone/tablet dimensions. Phase 6 Storeyard charters, faction recruits and the Supply Outpost also pass simulation, PlayMode integration and packaged Windows checks for both factions at 1280 x 720 and 1440 x 1080. The [faction bible](FACTION_BIBLE.md) records current evidence and limits; the [Phase 5 report](../tasks/PHASE_5_REPORT.md) records the earlier checkpoint. Display names are original working terms; common roles remain visible in help text.

## Bounded roster

| Working name | Common role | Decision and function | Availability / scope |
| --- | --- | --- | --- |
| Hearth | Headquarters | Train Tenders and accept delivered cargo; Phase 5 advancement research competes with its worker queue. In Conquest, a player without a completed Hearth is eliminated; a foundation does not count until finished. | Settlement; existing economy, Phase 5 research |
| Shelter | Housing | Spend construction resources before production reaches capacity. | Settlement; existing |
| Storeyard | Resource drop-off | Reduce travel; an Aven owner can pay for a local Logistics or Muster charter. | Settlement; drop-off and charter rules verified |
| Muster Hall | Military production | Train the three shared military roles plus the eligible faction recruit; equipment research competes with training. | Settlement production; Kingdom/Empire research |
| Archive | Research | Invest in economic upgrades and satisfy the completed-building prerequisite for Dominion. | Kingdom; implemented |
| Supply Outpost | Serevin mobile drop-off | Commit to a supply site or lose service while packing, travelling and redeploying. | Settlement; simulation/UI integration verified |
| Cultivation Court | Food infrastructure | Invest in stable food access as finite safe sources diminish. | Future; outside Phase 5 slice |
| Bow Court | Ranged production | Possible separate Stringwarden queue if specialization earns its added complexity. | Future; Muster Hall serves this role now |
| Stride Yard | Cavalry production | Possible separate Strider queue if specialization earns its added complexity. | Future; Muster Hall serves this role now |
| Frameworks | Siege workshop | Invest in a structure-breaking force that needs protection. | Future; siege deferred |
| Signal Tower | Local defense/vision | Secure one route at a cost to army or expansion. | Future; outside Phase 5 slice |
| Bracewall | Wall segment | Shape movement while retaining readable entrances and counterplay. | Future; no player-built walls yet |
| Highward | Advanced fortress | Commit stone to holding a valuable site. | Future; after siege viability |

Twelve roles remain the upper MVP bound per faction, not an instruction to create every placeholder now. The faction-specific Supply Outpost is an alternative logistical investment, not a second complete building roster. Archive is the active name for the research role previously proposed as Inquiry House. Research shares the Hearth and Muster Hall where queue contention adds a useful decision.

## Phase 5 Archive and research roles

The `archive` definition is a 3×3-cell structure requiring Kingdom. It costs **120 Wood and 40 Stone**, has **700 maximum health and 2 armor**, and takes **240 construction work ticks** (12 seconds with one continuously adjacent Tender at 20 Hz). Additional builders contribute construction work under the existing rules. It provides no population capacity, resource drop-off or unit training.

A completed Archive researches Better Handles in Kingdom and Work Rotation in Dominion. The player must own a completed Archive when starting Dominion advancement at a Hearth. Work Rotation is an Empire prerequisite; weapon/armor research stays in the Muster Hall. Phase 6 also assigns the eligible faction's unique Kingdom research to the Archive. The [technology tree](TECHNOLOGY_TREE.md) lists the nine shared and two faction research items.

An owned, completed producer can run one research item only when its training queue is empty. It accepts no training while research is active, and research cannot be queued. Different buildings can research different technologies in parallel. Destruction loses the producer's paid, unfinished research; retry is allowed at full cost, while completed player bonuses remain. Prerequisites are checked when research starts: losing a separate required building afterward does not cancel accepted work. Archive placement, construction, research and rejection feedback are verified in Phase 5.

## Phase 6 logistical structures

The shared Storeyard's base cost, footprint, health and drop-off behavior are unchanged. `CanCharter` permits an Aven owner to pay **40 Wood + 20 Metal** and **100 ticks / 5 s** for either Logistics or Muster. Its **6 m** coverage grants **+4 worker carry capacity** or **+20% training work rate**, respectively. Only one charter is active per yard; switching suspends benefits and overlapping coverage does not stack. Reciprocal Stores expands the yard/tether radius to **8 m**. Neither research nor construction receives the training bonus.

The Serevin-only `supply_outpost` costs **80 Wood + 20 Stone**, takes **160 construction work ticks / 8 s**, occupies **2 x 2 cells**, and has **450 health and 1 armor**. It provides drop-off only: no training, population or charter. It packs in **100 ticks / 5 s** into a slow, unarmed Supply Cart and redeploys after another **100 ticks / 5 s** at a legal nearby site. It supplies no drop-off during relocation. The cart moves at **2.2 m/s**, has **zero armor**, preserves damage and cannot be trained directly. Prepared Encampments reduces each transition to **60 ticks / 3 s**. The [faction bible](FACTION_BIBLE.md) links simulation/UI evidence for paid construction, conversion, placement and removal of a dead transport's preview. Both final Serevin desktop walkthroughs preserved 439/450 health through two relocations, including the researched timing reduction. These checks do not establish full-match logistical balance.

## Placement and construction rules

Definitions own footprint, cost, construction work, maximum health, produced units, population contribution and drop-off eligibility. Phase 5 adds Era and technology prerequisites; Phase 6 adds optional faction gates. Terrain specialization and strategic vision remain future work. Cosmetic mesh extents do not determine authoritative occupancy.

Preview placement without charging resources. On confirm, validate prerequisites, bounds and occupancy and charge once. A valid foundation becomes selectable, shows progress and obstructs navigation immediately; it supplies its completed functions only after construction finishes. Foundations update routes under the verified movement rules. Merely displaying a model does not enable drop-off, housing, training or research.

Construction can be stopped and resumed by eligible workers without charging for the foundation again. Destruction removes the building and its ongoing work. Player-driven construction cancellation/refunds and repairs remain future policies; repair behavior must not exceed maximum health or revive destroyed structures. Research interruption follows the explicit loss/retry rule above.

## Production UX and later behavior

Show queue contents, remaining work, costs and population requirements. A disabled action states its reason, including missing research prerequisites or an occupied producer. Existing rally orders direct new units toward safe nearby destinations; blocked exits wait rather than deleting paid units, and an earlier rally arrival must not cage subsequent recruits. Phase 5 research presentation must distinguish the building's active research from its training queue.

Selection must distinguish an owned operational building, an owned construction site and an observed enemy structure. Enemy production information is limited by the information rules; a hidden queue is not exposed by selecting a stale marker.
