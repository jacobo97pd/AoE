# Unit roster

Status: the four initial roles and Phase 6 Threadkeeper, Ashrunner and relocation-only Supply Cart have passed simulation, PlayMode integration and packaged Windows player checks. Both factions' unique recruits/actions were verified at 1280 x 720 and 1440 x 1080. These accelerated checks do not establish full-match balance or physical touch usability. Other later entries remain proposals. Runtime JSON owns numeric values. See the [faction bible](FACTION_BIBLE.md) and [current phase](../tasks/CURRENT_PHASE.md) for evidence.

## Initial roles

| Working name | Common role | Primary decision | Planned phase |
| --- | --- | --- | --- |
| Tender | Worker | Build long-term income or expose workers on an expansion. | 1 movement; 2 economy |
| Reedguard | Spear infantry | Hold an approach against cavalry despite ranged vulnerability. | 2 training target; 3 combat |
| Stringwarden | Archer | Sustain ranged pressure while protecting a fragile formation. | 3 |
| Strider | Cavalry | Raid and flank while avoiding prepared spear lines. | 3 |

These are original display names for familiar mechanical roles. The tutorial and selection panel should include the role subtitle so new names do not obscure counters.

The primitive proxies distinguish a Tender's pack, Reedguard's tall spear, Stringwarden's bow/quiver and Strider's mount/lance. Military attacks show a small movement pulse; projectiles travel visibly and health bars show damage. These are prototype feedback, not final models or authored animation.

## Initial combat tuning

| Unit | Health / armor | Damage / interval | Edge range | Speed | Bonus | Cost |
| --- | --- | --- | --- | --- | --- | --- |
| Tender | 60 / 0 | 5 / 1.2 s | 1.0 m | 3.2 m/s | — | 50 Food |
| Reedguard | 100 / 1 | 12 / 1.2 s | 1.1 m | 3.6 m/s | ×1.8 against Cavalry | 60 Food + 20 Wood |
| Stringwarden | 70 / 0 | 10 / 1.0 s | 5.0 m | 3.2 m/s | ×1.8 against Infantry | 30 Food + 50 Wood |
| Strider | 110 / 1 | 12 / 1.2 s | 0.9 m | 5.0 m/s | ×1.3 against Ranged | 80 Food |

A Tender defends itself and nothing more. Its 5 damage lands as 4 on a Reedguard's armor, about two fifths of a soldier's output, on 60 health with no armor: a soldier beats a lone worker while keeping most of its health, two workers still lose, and three together bring the soldier down and lose one of their own. Its acquire range equals its 1.0 m reach, so an idle worker answers only what is already on top of it and never walks off to fight, and a worker on a gathering or building task ignores the fight entirely. Workers are excluded from Army selection and from the army metric, and they still cannot capture beacons.

All three military roles use one population and train in eight seconds at the prototype Muster Hall. Their nominal resource totals match; Food and Wood still have different gathering/opportunity costs. The hall's combined training roster is a small prototype choice, not the final production-building roster.

For this roster, `Infantry` is the melee-infantry counter tag on Reedguard. Stringwarden uses `Ranged | Light`, so its Infantry bonus does not also amplify archer-versus-archer attacks. Bonuses use the highest matching multiplier, integer truncation, then target armor, with at least one damage. No immunity makes a counter absolute.

The battle sandbox starts both sides with two of each military role, one Tender and ready production. Opponents acquire nearby targets; this is local combat behavior, not an economic or strategic AI opponent. Shipped-data counter checks compare both spawn sides and a two-to-one numerical disadvantage before recording results in the phase report.

## Phase 6 faction roster

The two unique recruits supplement the shared roles in this slice; the earlier Ashrunner-for-Strider replacement proposal is deferred. Both require their matching player faction, one population and 160 training work ticks at a completed Muster Hall in Settlement. Their costs are deliberately different from the common 80-resource military drill roster.

| Unit / faction | Health / armor | Base combat and movement | Cost | Function |
| --- | --- | --- | --- | --- |
| Threadkeeper / Aven | 70 / 0 | Unarmed; 3.2 m/s; radius 0.30 m; Light tag. | 60 Food + 40 Wood | Deploy for 15 s as an immobile relay of a linked chartered Storeyard, then 20 s cooldown. No gathering, building or physical drop-off. |
| Ashrunner / Serevin | 95 / 0 | 10 damage / 1.2 s; 0.9 m range; base 5.0 m/s; radius 0.35 m; Cavalry and Light tags; 1.3x against Ranged. | 80 Food + 20 Wood | Three-second reposition increases speed to 7.75 m/s and disables attacks, followed by 15 s cooldown. |
| Supply Cart / Serevin | 450 / 0 | Unarmed; 2.2 m/s; radius 0.35 m; Light tag. | Conversion only | Packed Supply Outpost with zero population; cannot be trained, gather or drop off cargo. Damage survives conversion. |

Serevin's cavalry passive raises Strider and Ashrunner normal speed to 5.75 m/s; it does not affect the cart. Ashrunner remains subject to the Reedguard's Cavalry counter and normal military research. The Light-only Threadkeeper and cart do not receive equipment upgrades intended for Infantry, Cavalry or Ranged. The [faction bible](FACTION_BIBLE.md) specifies paid charters, relay coverage, temporary speed and relocation timing.

The [eight shipped-data faction counter probes](../testing/evidence/phase6-faction-counters.md) passed on both spawn sides. A Reedguard defeated an Ashrunner in 5.40 s with 64 health remaining; an Ashrunner defeated a Stringwarden in 6.95 s with 25 health remaining. Two opposing units reversed both favored one-on-one outcomes. Those drills use faction passives but no research or active abilities, and Ashrunner costs 100 nominal resources against the shared units' 80. They verify retained counter behavior; full-match and economic balance remain unvalidated.

## Desert cavalry (28 September 2026)

The Sultanato and the Confederación del Sahel each add one cavalry recruit to the Muster Hall in Settlement, using only
the shared counter tags ([design and probe results](DESERT_FACTIONS.md)).

| Unit / faction | Health / armor | Base combat and movement | Cost | Function |
| --- | --- | --- | --- | --- |
| Camel Archer / Sultanato | 90 / 0 | 9 damage / 1.3 s; 4.0 m range, arrows at 7 m/s; base 4.4 m/s (4.84 with the Sultanato's cavalry passive); Cavalry, Ranged and Light tags; 1.3x against Infantry. | 70 Food + 35 Wood | Harries infantry and workers from the saddle. Strider, Reedguard and Stringwarden each beat it standing; kiting is what keeps it alive. |
| Quilted Lancer / Sahel | 120 / 2 | 12 damage / 1.3 s; 0.9 m range; 4.6 m/s; Cavalry and Heavy tags; 1.5x against Ranged. | 90 Food + 30 Metal | Rides down archers on foot and the Camel Archer. The Reedguard's 1.8x against Cavalry stops it. |

## Historical pirate company

The pirates are a shared company of the historical realm, not a fifth faction. Their definitions require only `RequiredRealmId: historical`, so Aven, Serevin, Miraj and Skeld can all recruit them, and fantasy maps reject them. On 13 September 2026 the rules audit confirmed this as the intended design, rather than tying the company to one faction or capping it as mercenaries. It takes each historical faction above the 6–8 choice guideline below; the company adds roles instead of duplicating the core triangle.

| Unit | Health / armor | Damage / interval | Edge range | Speed | Bonus | Cost / population | Producer |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Crimson Corsair (hero, one alive or queued) | 380 / 3 | 24 / 1.2 s | 1.2 m | 3.4 m/s | — | 200 Food + 100 Metal / 3 | Hearth |
| Boarding Raider | 110 / 2 | 13 / 1.3 s | 1.05 m | 3.7 m/s | ×1.6 against Ranged, ×1.5 against structures | 60 Food + 20 Metal / 1 | Muster Hall |
| Gunpowder Corsair | 80 / 0 | 18 / 1.8 s | 4.5 m | 3.2 m/s | ×1.5 against Heavy | 40 Food + 40 Metal / 1 | Muster Hall |
| Treasure Seeker | 65 / 0 | 5 / 1.2 s | 1.0 m | 3.5 m/s | 12-cell vision, carries 10 | 70 Food + 20 Wood / 1 | Hearth |

At an equal 720-resource budget, the raiders narrowly beat Reedguards and beat gunners, but lose to Striders and Stringwardens. Gunners beat Reedguards and lose to Stringwardens and Striders. The Captain defeats three Reedguards, Striders or Stringwardens. The offline AI rotates through every trainable soldier, so the company appears in AI armies; it trains one Treasure Seeker once six workers are gathering. The [pirate integration notes](../art/pirate-crew-integration.md) record the measurements.

## Later bounded roster

| Working name | Role | Roster rule / decision | Earliest phase |
| --- | --- | --- | --- |
| Waylight | Scout | Weak in combat; movement and vision earn actionable information. | 7, may be introduced earlier for testing |
| Slatebound | Heavy infantry | Durable commitment that sacrifices mobility and resource flexibility. | 5–7 after core triangle |
| Rookframe | Siege | Threatens structures while requiring escort and preparation. | 5–7 |

Limit a faction to roughly 6–8 meaningful unit choices in the complete slice. A separate swordsman, heavy cavalry and additional support branch remain candidate expansions, not automatic additions to this roster. Upgraded tiers should remain one recognizable role rather than padding the unit count.

## Data contract

Definitions should express stable ID, display name, role/category, permitted factions, tags, health, armor, speed, vision, attack definition, resource cost, training time, population cost, Era, producer, prerequisites, visual reference and audio reference. Implement only fields needed by the active phase; validate new references as their systems arrive.

Attack data contains range, cooldown, base damage, projectile definition and tag bonuses. Worker task data contains carry capacity, work cadence and accepted resource/drop-off interactions. Entity state contains current mutable values; definitions are not modified to represent an individual unit.

## Counter and balance checks — Phase 3 onward

Reedguard should efficiently threaten Strider, Strider should reach exposed Stringwarden, and Stringwarden should punish unsupported Reedguard. Test equal resource value as well as equal headcount; those are different comparisons. Include larger opposing forces, protected ranged lines, retreats, reinforcement arrival and narrow/open terrain. A counter advantage must survive sensible testing without becoming an automatic win independent of context.

Add effects through composable tags/modifiers. Avoid bespoke inheritance per faction, universal best units and permanent account bonuses. Track total cost, time to field, population, effective reach and replacement distance when tuning; a damage number alone is not a unit's value.
