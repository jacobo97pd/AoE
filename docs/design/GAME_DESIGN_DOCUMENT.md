# Emberfield — game design document

Status: living design skeleton. Systems below are intended product rules unless explicitly recorded as delivered in [CURRENT_PHASE](../tasks/CURRENT_PHASE.md). This document does not expand the immediate implementation beyond the [MVP scope](MVP_SCOPE.md).

## Match structure

Start with a small settlement, Tenders, limited resources and unexplored territory. The repeating loop is explore → gather → build → expand → research → create army → scout → fight → adapt → victory. Information, travel time and attention connect those actions.

The implemented offline skirmish is 1v1 on Amber Crossing, a larger map in the Amber terrace biome; Amber Reach remains the economy practice fixture. Factions and Conquest/Dominion are chosen before the match, with the same rules and starting stock for both players. Strategic AI obeys fog and ordinary paid commands. Scenarios may use authored review/stress starts explicitly labeled as such. Online skirmish is future work.

## Economy and workers

| Resource | Intended role | Decision created |
| --- | --- | --- |
| Food | Worker and living-unit production | Grow income or fund immediate military presence. |
| Wood | Expansion, production, ranged equipment and utility | Add capacity or spend on the units that use it. |
| Metal | Advanced equipment and higher-tier forces | Contest exposed reserves or use cheaper compositions. |
| Stone | Durable defenses and advanced infrastructure | Fortify a location or retain resources for expansion. |

Tenders are selected entities with an explicit task: idle, move, approach resource, harvest, carry, return, construct, repair or reassign. A resource is credited to inventory at a valid owned drop-off, not when its presentation animation plays. Depleted nodes and invalid targets must produce a clear state transition; later automatic retargeting may reduce busywork only within an explicitly selected task.

Worker carrying capacity, gathering interval, node stock, drop-off eligibility, costs and work rates belong to configuration. Economy transactions must be atomic and validated by simulation. Invalid commands cannot create negative stock or partially charge a purchase. Cancellation/refund policy must be explicit per implemented queue or construction system and covered by its tests.

Population has a current value and a capacity; units consume it, housing increases capacity. Production must reserve or recheck capacity consistently so multiple queues cannot overfill the cap. Queued supply commitments must be visible to the player. No arbitrary premium population expansion exists.

## Construction and production

Preview a building footprint, show why a site is invalid, then confirm placement. Validate bounds, obstacles, occupancy, player ownership, prerequisites and affordability in simulation. A successful placement creates construction state; completing a structure enables its functions. Construction progress, health and destruction are separate concerns.

Production is a time-based queue controlled by simulation ticks. Queue entries reference stable definition IDs. The interface shows cost, remaining work and population, with a reason when an order is rejected. Training reserves population when queued and waits at completion if no legal exit exists; configured rally orders then move newly trained units. Phase 3 destruction cancels the producer's queue, releases its reservations and grants no resource refund. Manual cancellation/refunds remain future policy work. Completed housing destruction reduces capacity; existing units remain alive, while further training/spawning must obey the reduced cap.

## Movement and commands

Input expresses player intent through commands rather than changing authoritative transforms. Movement stays within map bounds; later navigation separates a shared strategic route from local avoidance and destination slots. Stop cancels current movement/task according to the task state machine. Invalid ownership and invalid target IDs are rejected.

Phase 1 requires one selected worker to move. Phase 4 adds robust group routes, destination spreading and line/box/loose formations. Formation requests provide target slots; they must not lock a unit out of sensible combat behavior. Performance success requires actual measurements, particularly congestion and order latency.

## Combat — Phase 3 prototype

The initial triangle is Reedguard (spear infantry) against Strider (cavalry), Strider against Stringwarden (ranged infantry), and Stringwarden against Reedguard. These are soft advantages. Force size, range, positioning, reinforcements, economic cost and upgrades still affect the outcome.

Reusable tags include Worker, Infantry, Cavalry, Ranged, Light, Heavy, Siege and Structure. Configured damage bonuses query tags; matching multiple tags must use one documented aggregation rule rather than accidental duplicate multiplication. Armor, minimum damage, range checks and cooldown timing need explicit tests when added.

Simulation owns target selection, attacks, integer projectile travel, impact and death. Presentation owns silhouettes, attack feedback, health bars and pooled projectile views. Dead units release population and stop acting once; carried resources are lost with a killed worker. Destroyed buildings clear their footprint and cease construction/drop-off/production. A destroyed Hearth does not end the current sandbox: victory rules remain Phase 7.

Idle armed units acquire nearby reachable enemies at a bounded cadence. Explicit attack orders pursue their target; moving and working units keep their explicit task. Stop cancels attacks and holds fire until another order. Tenders carry a light self-defence attack: their acquire range equals their own reach, so an idle worker answers what is already on top of it and never leaves to chase, and a worker that is gathering or building keeps working. The touch context filters a mixed selection to armed units when tapping an enemy; the simulation rejects a direct attack command containing unarmed units atomically.

Archers launch homing simulated projectiles with damage fixed at launch. Shots already airborne survive their shooter's death; missing/dead targets cause a miss, and flight expires after the configured lifetime. This prototype has no line-of-sight or terrain interception, friendly fire, kiting automation or attack-move command. Those are distinct future gameplay decisions rather than implied capabilities.

## Eras and technology — future Phase 5

Configurable initial labels are Settlement, Kingdom, Dominion and Empire. Advancement consumes a substantial investment, takes time, and unlocks options. It does not automatically purchase every upgrade. A player may remain at an earlier Era to exploit a military timing.

Research references prerequisite IDs, minimum Era, permitted faction and producer. Effects are composable modifiers scoped to this match. Distinct branches cover economy, military, defense and faction mechanics. Validate missing IDs, cycles, duplicate research and illegal transition attempts; see [technology tree](TECHNOLOGY_TREE.md).

## Information and territory — future Phase 7

Fog has Unexplored, Explored and Visible states. Players and normal AI receive the same information-limited observation contract. Explored terrain remains known; enemy state must not update live outside vision. Any last-seen building marker must look stale and reveal no hidden changes.

A later scout is fast, has broad vision and weak combat. Exposed production, expansion and Era activity are scouting clues. Map control offers additional deposits, shorter reinforcement paths and objective pressure. No global enemy technology panel undermines those decisions.

## Victory — future Phase 7

**Conquest:** destroy all enemy structures marked as central strategic structures by the selected ruleset. The base case is the Hearth. Only a completed Hearth keeps a player alive. A replacement counts once its construction finishes, but an unfinished foundation hidden in the fog does not prolong a decided match. Once the result is final, no command can reopen it.

**Dominion:** control configured strategic objectives for a stated interval. Capture, contest, hold-progress decay/reset and simultaneous win resolution require explicit tuning and tests before this mode ships. Show all clocks and contest states clearly to both players without revealing unrelated hidden units.

Surrender, match result, restart and event finalization must work for either mode. Ascendancy, a costly defended countdown structure, is future optional design beyond earliest MVP.

## Factions and AI

Aven Compact favors adaptable civic logistics; Serevin March favors deliberate redeployment and raid pressure. Both retain the common resource, unit-role and counter language. Their unique mechanics are [Phase 6 proposals](FACTION_BIBLE.md), not current bonuses.

The offline AI issues the same legal commands as a player. Its three difficulties are chosen on the skirmish page:
- **Easy** keeps the original pacing: it develops to Empire before any assault.
- **Normal**, the default, attacks with a formed army from minute seven once it has reached Kingdom.
- **Hard** runs a larger economy, reacts faster, guards a wider radius and attacks from minute six with a larger army.

The [expansion simulation notes](../technical/EXPANSION_SIMULATION.md) list the exact limits. No difficulty inspects hidden state or receives resource grants. A Master tier with stronger timing and multitasking remains future work. No in-match LLM calls.

## UX, learning and persistent systems

The [UI/UX guide](../art/UI_UX_GUIDE.md) defines touch/mouse equivalence, contextual selection and safe areas. UI must distinguish an unavailable action from an ignored tap. Tech, queue and construction feedback must state the missing resource, population or prerequisite.

Future post-game reports show result, duration, Era timings, economy/army curves, losses and objectives. Present a useful hypothesis such as a late expansion or production gap without claiming statistics prove one cause. Aggregated timeline events feed local reports first; remote analytics is a separate adapter and is not sent for every simulation event.

Account profile, faction mastery, cosmetics and achievements remain outside combat state. Hidden MMR estimates skill per queue; visible rank represents seasonal aspiration. Future authoritative match results update these backend-side. Tutorial → exercises → AI → casual → ranked is the eventual learning progression.

## Open design questions and validation

| Question | Evidence needed | Earliest phase |
| --- | --- | --- |
| How much economic assistance preserves decisions on touch? | Compare idle time and command errors in observed play. | 2, refine 8 |
| Is the counter triangle useful without determining fights? | Equal-value, unequal-size and terrain combat scenarios. | 3 |
| Can groups move through normal approaches comfortably? | Measured 100+ unit routes and congestion recovery. | 4 |
| Do Era choices have multiple viable timings? | Playtest openings and record timing/outcome traces. | 5–7 |
| Do faction mechanics change plans without adding chores? | Matched player trials with mechanic use and counterplay. | 6–7 |
| Are losses understandable and matches enjoyable? | Complete AI matches plus player interviews. | 7 |
