# MVP scope and boundaries

Status: scope definition. The user authorized Phases 7–10 together after Phase 6: full offline match, mobile UX, bounded art slice and measured optimization. [CURRENT_PHASE](../tasks/CURRENT_PHASE.md) owns completion evidence and environmental limits. Multiplayer and backend are not included in this pass.

## Immediate execution

| Phase | Required outcome | Gate |
| --- | --- | --- |
| 0 — Foundation | Audited environment, original design, technical boundaries, art rules and roadmap | Reference read; existing content preserved; chosen editor documented. |
| 1 — Greybox core | Primitive units/buildings/resources, commands, touch/mouse camera, selection and movement on one map | Player can select a Tender and move it; compilation, suitable tests and build evidence are healthy. |
| 2 — Economy | Four-resource inventory, gather/return, population, placement, construction and training | Start only after Phase 1 gate. Player can gather, construct a production building and train a unit; logic tests pass. |
| 3 — Combat | Health, attacks, armor, range, target selection, projectiles, death and three counter roles | Three archetypes fight with nonabsolute advantages; economy and combat integration remain green. |

If environment or build failures prevent a phase gate, document the actual failure and repair it within scope. Do not label a phase complete from code inspection alone. Phase 3 was authorized and completed as a separate bounded pass; the first select-to-fight loop now has recorded verification. The complete MVP still requires the later gates below.

## Eventual offline MVP

- One readable biome and one initial map, Amber Reach, with configuration allowing future maps.
- Two original factions: Aven Compact and Serevin March.
- Food, Wood, Metal and Stone; workers, construction, queues and population.
- Four configurable Eras; a small meaningful technology tree.
- About 6–8 meaningful unit choices per faction maximum, including shared roles and a faction substitute.
- About 8–12 building roles maximum; only a subset is needed in early phases.
- Soft counters, scouting, fog, AI, Conquest and Dominion victory.
- Full 1v1 against AI, a basic learning path, surrender/restart and useful results.
- Touch controls tested on tablet and phone layouts; device performance recorded.

Private-room 1v1 multiplayer is a later milestone after offline play, UX and profiling. Real accounts, matchmaking, MMR, visible rank and seasons follow verified networking and authoritative results.

## Explicit exclusions

No naval combat, campaigns, 4v4, eight-player free-for-all, huge open world, guilds, clans, battle pass, mod editor, twenty factions or full production art library. No monetization implementation during early gameplay. No external paid service, licensed networking SDK or large optional installation without the user's required authorization.

No complete ECS conversion, speculative bot farm, ranked backend, replay service or content-management platform to satisfy a placeholder architecture. Add boundaries where meaningful; implement functionality when its phase needs it.

## Scope change rule

An added feature must identify the decision it creates, the current acceptance criterion it enables, and its verification cost. Prefer removing low-value complexity to adding more controls or content. A proposal outside the active phase stays in the roadmap until its prerequisites pass.
