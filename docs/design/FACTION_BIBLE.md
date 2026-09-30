# Faction bible

**Current roster (14 September 2026):** [Faction groups](FACTION_GROUPS.md) supersedes the old faction names and membership below. Históricas: Franceses, Hispanos, Ingleses, Sultanato, Confederación del Sahel ([desert factions](DESERT_FACTIONS.md), 28 September 2026). Fantasía: Orcos, Enanos, Hombres de las montañas, Elfos. Navales: Piratas; Marina inglesa, Marina española and Flota esquelética remain locked, planned entries. The following Phase 6 document preserves the original mechanics and evidence under their stable internal IDs.

Status: Phase 6 faction rules and presentation passed simulation, PlayMode integration and packaged Windows player checks for both factions at 1280 x 720 and 1440 x 1080. The [current phase](../tasks/CURRENT_PHASE.md) owns checkpoint completion claims. [greybox.json](../../Assets/Game/Resources/Definitions/greybox.json) owns numeric tuning; these are prototype choices, not established competitive balance.

## Shared rules and scope

Both factions use Food, Wood, Metal and Stone; four Eras; shared worker and military roles; and the existing movement, damage and research rules. Faction assignment belongs to a player for the match. `aven` and `serevin` are stable content IDs. Legacy maps without an assignment retain their neutral prototype behavior.

Unique units, the Supply Outpost and the two unique technologies require the matching faction. Shared Tenders, Reedguards, Stringwardens and Striders retain their original base definitions and availability. Ashrunner is an additional cavalry choice in this slice; the earlier Strider replacement proposal is deferred. Bonuses affect owned state, not shared definitions or account statistics.

Times assume 20 simulation ticks per second. Training/construction times describe work; walking, blocked exits and interruptions can extend elapsed time. No faction effect creates resources, grants invulnerability or replaces physical cargo delivery.

## Aven Compact

**Fantasy:** terrace settlements cooperating through public stores and causeways. The Compact survives ash seasons by moving resources and expertise where shortages appear. Its identity is civic coordination, not a reproduction of a historical state.

**Strategic identity:** balanced and adaptable. Commit infrastructure to an economic route or reinforcement position, then weigh the cost and downtime of changing its purpose.

| Rule | Authored value | Decision and exposure |
| --- | --- | --- |
| Worker passive | +1 carry capacity; a base Tender carries 11. | Fewer return journeys; extraction and delivery remain necessary. |
| Storeyard charter | 40 Wood + 20 Metal; 100 ticks / 5 s to initialize or change. | A completed owned Storeyard chooses Logistics or Muster. Switching suspends charter benefits; normal drop-off remains available. |
| Coverage | 6 m local radius. | Commit infrastructure near the intended workers or producer. |
| Logistics | +4 nearby worker carry capacity; an Aven Tender reaches 15. | Improves carrying, not gather amount or free stock. Overlapping sources do not multiply the bonus. |
| Muster | +100 permille nearby training work rate. | 10% more work per tick, not 10% less duration: an uninterrupted 160-tick recruit needs 146 ticks. Research and construction are unaffected. |
| Reciprocal Stores | Kingdom Archive; 120 Food + 100 Wood; 600 ticks / 30 s. | Yard coverage/tether expands from 6 m to 8 m. Threadkeeper relay radius stays 4 m. |

Only an Aven owner can charter the shared Storeyard. Charters are mutually exclusive on a yard and overlapping coverage does not stack. Coverage and tether distances are centre-to-centre, including the radius boundary. Losing carrying capacity never deletes cargo already carried. Selecting the same completed charter must not repeatedly charge resources. Cost, duration, radius and modifier magnitude are configurable faction data.

### Threadkeeper

A Threadkeeper trains at a completed Muster Hall in Settlement for **60 Food + 40 Wood**, **160 work ticks / 8 s**, and **one population**. It has **70 health, no armor, 3.2 m/s speed and a 0.30 m radius**. It is unarmed, uses the Light tag, and cannot gather or build. Military equipment research does not match its tags.

Deployment requires a nearby owned, completed Storeyard with an active charter. The Threadkeeper immediately becomes immobile for **300 ticks / 15 s**, projecting that linked yard's charter within **4 m** of its own position. It cannot chain through other relays, choose an independent charter or become a physical drop-off. Workers still deliver to a real eligible structure.

An inactive source charter or lost tether supplies no relayed benefit. Switching the source yard leaves the deployment timer running; its new charter can resume the relay benefit while the timer and tether remain valid. Destroying the linked yard ends deployment and starts cooldown. A **400-tick / 20 s cooldown** follows deployment expiry. This substitutes a vulnerable, immobile support body for military presence; it grants no free income or extra vision system.

**Counterplay:** read the charter marker, pressure workers beyond its coverage, destroy the source yard or attack the relay. Test isolated network edges and overlapping yards before widening the system. Paid switching and deployment commitment should discourage constant toggling.

**Visual language:** offset terraced roofs, square braces, pale mineral plaster, warm timber and rectangular cloth tabs. Team color belongs on large tabs and roof bands. Charter markers and the support silhouette must read at gameplay zoom; final art remains future work.

## Serevin March

**Fantasy:** mobile frontier households escorting stored supplies along seasonal routes and establishing temporary footholds. Their power depends on choosing when to commit people and transport to one place.

**Strategic identity:** aggressive and mobile. Trade logistical uptime and combat commitment for a change of position, accepting interception risk along the way.

| Rule | Authored value | Decision and exposure |
| --- | --- | --- |
| Cavalry passive | +150 permille speed: Strider and Ashrunner move at 5.75 m/s before temporary effects. | Cavalry still triggers the Reedguard counter. The cart has no Cavalry tag. |
| Worker passive | +1 carry capacity; a base Tender carries 11. Added on 13 September 2026, when balance samples showed Serevin losing about 70% of its matches against Aven. | Supports the outposts' supply lines; extraction and delivery remain necessary. |
| Supply Outpost | Settlement; 80 Wood + 20 Stone; 160 construction ticks / 8 s; 2 x 2 cells. | Physical drop-off with 450 health and 1 armor; no housing, production or charter. |
| Pack | 100 ticks / 5 s. | Drop-off utility stops during relocation; packing provides no teleport. |
| Supply Cart | 450 maximum health, no armor, 2.2 m/s, 0.35 m radius, Light tag. | Unarmed, nonworker transport with no population cost, direct training option or drop-off. Damage persists across forms. |
| Deploy | 100 ticks / 5 s after legal local placement is accepted. | Normal movement must first bring the cart to the site. Terrain, bounds and occupied space constrain the structure footprint. |
| Prepared Encampments | Kingdom Archive; 100 Food + 100 Wood; 600 ticks / 30 s. | Subtracts 40 ticks from each transition: pack and deploy each take 60 ticks / 3 s. Travel and exposure remain. |

The cart is the packed outpost asset, not a free recruit. Its zero authored resource cost and one-tick placeholder training value satisfy the data contract; no building trains it. Both forms share maximum health and preserve exact current health, so conversion cannot heal damage. Relocation charges no second resource payment. Stop during deployment discards transition progress and returns to the packed state; movement requires leaving the deploying state first. Destruction removes the asset. Neither form attacks, gathers, trains units or provides population; the Hearth remains stationary.

### Ashrunner

An Ashrunner trains at a completed Muster Hall in Settlement for **80 Food + 20 Wood**, **160 work ticks / 8 s**, and **one population**. Base statistics are **95 health, no armor, 5.0 m/s speed, 0.35 m radius, 10 damage, 0.9 m edge range and a 24-tick / 1.2 s attack interval**. It retains **Cavalry | Light** tags and a **1.3x damage multiplier against Ranged**. The faction passive raises normal speed to 5.75 m/s.

Reposition adds **500 permille temporary speed for 60 ticks / 3 s**, followed by a **300-tick / 15 s cooldown starting at active expiry**. The modifiers add: 5000 x (1000 + 50 + 500) / 1000 gives **7.75 m/s** during reposition. Attacks are disabled during the active window; collision and incoming damage still apply. Movement orders determine the route. This is an escape or approach commitment, not a free attack buff.

The Ashrunner costs 20 Wood more than Strider while carrying less health, armor and damage. Reedguards retain their 1.8x Cavalry multiplier against it. Shared military research affects it normally. These tradeoffs require matchup testing; the authored numbers alone do not establish balance.

**Counterplay:** occupy retreat routes, intercept the slower cart, pressure transitions and defend against cavalry with spears. Research must leave a visible relocation delay. Avoid universal best cavalry, unavoidable raid loops and constant touch micro.

**Visual language:** low wedge roofs, split timber runners, diagonal braces, bundled textiles and narrow pennants. Broad runner shapes distinguish the outpost from a Storeyard; the packed cart must read as vulnerable transport. Ashrunner remains recognizable cavalry with distinct equipment. No real culture's regalia or another game's artwork is required.

## Faction proving ground

[faction_proving_ground.json](../../Assets/Game/Resources/Maps/faction_proving_ground.json) is a **64 x 56-cell** map with one-metre cells. Default assignment is owner 1 Aven and owner 2 Serevin; the chooser can swap assignments. Starts are exact 180-degree mirrors: **four Tenders, one completed Hearth, one completed Storeyard and one completed Muster Hall each**. Both begin in Settlement with **120 Food, 80 Wood, 0 Metal and 40 Stone**, **four population used and six capacity**.

Each side has equally placed Orchard, Copse, Metal seam and Stone outcrop nodes with matching stock. The open centre leaves room for coverage, routing and interception checks. Prebuilt shared structures make mechanics accessible; there are no initial unique units, Archive, free technologies or extra resources. This is fixture setup, not evidence for a normal build order's timing.

Train the faction unit through its Muster Hall. Gather Metal and Wood for Aven charters, or Wood and Stone for a Serevin outpost. Both unique technologies still require ordinary Kingdom advancement, Archive construction and paid research. Equal starts support comparisons; different faction actions still need cost and opportunity-cost comparisons.

## Phase 6 acceptance and limits

The current Unity run passed **290/290 EditMode and 32/32 PlayMode tests**, including **73 new simulation tests and six new faction integration tests**. The [simulation results](../testing/evidence/phase6-editmode.xml) and [integration results](../testing/evidence/phase6-playmode.xml) cover faction gates, equal starts, owned modifiers, local coverage, paid switches, relay lifecycle, relocation, research and presentation. The integration checks use authored resources, actual button handlers and real combat damage; they cover grouped Reposition and clearing deployment previews when the transport dies. Existing neutral economy, research and combat checks remain green.

The [eight faction counter probes](../testing/evidence/phase6-faction-counters.md) passed with actual shipped definitions and faction assignments: Reedguard defeats Ashrunner, Ashrunner defeats Stringwarden, and a two-to-one disadvantage reverses each result on both spawn sides. These fixed combat checks omit research, charters, relays and active Reposition, and compare units with different nominal costs. The 12 shared counter probes and 30-case movement run also retained their required gates; documented dense-choke limitations remain.

The final Windows build passed all four faction walkthroughs: Aven at [1280 x 720](../testing/evidence/phase6-aven-1280x720.txt) and [1440 x 1080](../testing/evidence/phase6-aven-1440x1080.txt), and Serevin at [1280 x 720](../testing/evidence/phase6-serevin-1280x720.txt) and [1440 x 1080](../testing/evidence/phase6-serevin-1440x1080.txt). Each verifies equal unmodified starting budgets, physical gathering, unique gates, mechanic, unit action, paid unique research and chooser dispatches that override the command-line faction. The Aven runs finish at tick 4035; Serevin at tick 3844, retaining a damaged outpost's 439/450 health through two relocations.

These accelerated desktop checks use synthetic UI handlers and scripted tick advancement. They establish integration behavior, not physical touch usability, real-time match pacing or mobile performance. The proving ground has no full-match AI, fog, victory flow or competitive faction balance evidence. Full matches, command burden, network raidability and economic pacing remain future evaluation. Extra factions, unit replacements, trade/siege identities, relay chains and a mobile Hearth are outside Phase 6.
