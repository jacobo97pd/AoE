# Reference design lessons

Source: supplied [Age_of_Empires_IV_Estudio_Integral.pdf](../references/Age_of_Empires_IV_Estudio_Integral.pdf), 20 pages, dated 9 September 2026. Read in full before this design foundation. Page references below identify the supplied study, not independent verification of the reference game's current product state. We use its systems analysis; we do not rely on its dated release, platform, roster-count or season claims for implementation.

The following is original synthesis for Emberfield. No reference artwork, map, civilization, interface, campaign, logo, music, iconography or distinctive wording is a production asset source.

## Transferable principles and resulting requirements

| Topic | Lesson from the study | Emberfield requirement | Phase / validation |
| --- | --- | --- | --- |
| Core loop (pp. 3–5) | Simple interconnected systems produce depth because each investment changes later choices. | Keep economy, production, scouting and combat in one match state with observable consequences. | 1–7; complete the small loop before expanding content. |
| Economy (pp. 5–6) | Income is time and territorial access converted into future options. | Four resources with distinct uses; workers travel, carry and drop off; budget cannot simultaneously fund every plan. | 2; resource/queue tests, allocation playtests. |
| Era design (pp. 7–8) | Advancement exchanges immediate capacity for future options. | Paid, timed, prerequisite-driven advancement; retain viable earlier-Era pressure. | 5–7; record investment windows and timings. |
| Soft counters (p. 8) | Composition matters alongside cost, position, force size and execution. | Tag-based advantages for spear/ranged/cavalry roles; avoid binary immunity and automatic wins. | 3; equal-value and contextual matchup tests. |
| Scouting (p. 8) | Information earns value when it arrives before a commitment is punished. | Three-state fog and useful production/expansion clues; AI observes player-limited information. | 7; hidden-state and decision scenarios. |
| Map variability (pp. 8–9) | Constrained variation preserves identity while preventing coordinate memorization. | Start with authored Amber Reach; isolate map/resource/spawn rules for later seeded variation. | 1–2 authored layout; later reachability/fairness tests. |
| Territory and endings (pp. 8–9) | Objectives create incentives to act and reduce indefinite stalling. | Conquest and later Dominion create distinct strategic closure; include surrender. | 7; progress/contest/result tests. |
| Asymmetry (pp. 9–10) | Factions create value through different decisions within shared readable rules. | Exactly two initial original factions with counterplay, a unique mechanic, unit and technology. | 6–7; paired full-match trials. |
| Persistent progress (pp. 10–11) | A reset match can coexist with mastery, identity and goals outside it. | Account data cannot increase standard PvP damage, armor, speed, income or starting wealth. | All phases; future profile boundary tests. |
| Matchmaking (pp. 11–12) | Skill estimation and visible seasonal aspiration serve different jobs. | Hidden MMR per queue; visible rank model separately; retain historical skill across placements. | 12; authoritative result and rating tests. |
| Input adaptation (p. 12) | Preserve strategy when adapting a control device; do not preserve every desktop gesture. | Native pan/pinch, forgiving selection, contextual controls and responsive tablet/phone layouts. | 1, 8; observed touch usability. |
| Post-game (p. 13) | A readable loss creates a hypothesis for improvement. | Record economical timeline statistics; explain result, timing and tradeoffs with clear graphs later. | 7 basic result; 13 analysis. |
| Onboarding (pp. 15–16) | Teach separate skills before combining them under competitive pressure. | Interactive small tutorial, short challenges, AI practice, then casual/ranked later. | 7–8/13; unassisted task completion. |
| Retention (pp. 13, 16–18) | Learning works across actions, matches and longer goals. | Make retry convenient and losses intelligible before introducing seasonal systems. | 7 first; 12–13 future meta. |
| Product flywheel (pp. 14, 18) | Meaningful new interactions and improving opponents create renewed problems to solve. | Expand factions/maps only after the small roster supports adaptive play; separate content from core rules. | Future after validated slice. |
| Modding/monetization (pp. 13–14, 18) | Competitive integrity and experimentation have different constraints. | Keep versioned standard rules distinct from future custom presets; no early payments or mod editor. | Architecture only, outside immediate scope. |

## Strengths to retain

The study's strongest transferable properties are a fair fresh match, economic opportunity cost, incomplete information, legible counters, multiple skill dimensions and the feedback loop from outcome to a revised plan (pp. 3–8, 17–18). Emberfield should let macro, micro, scouting, timing, map awareness and adaptation each contribute; raw production volume alone must not solve every problem.

These properties reinforce each other. A richer exposed deposit matters because it can fund an Era; scouting matters because it reveals that commitment; a raid matters because it delays the payoff. Build those connections before adding unrelated feature counts.

## Weaknesses to mitigate

The study identifies onboarding burden, mechanical overload, snowballing, long loss duration, balance complexity and rating confusion (pp. 17–19). On a phone, these risks intensify because usable screen area and precise simultaneous input are limited.

Emberfield responses: small rosters, role subtitles under original names, context-sensitive commands, staged teaching, shorter target sessions, early surrender, clear objective pressure, and post-game explanations. Reducing input friction should preserve resource and strategic choices. Do not hide a required decision behind silent automation.

Two rating values may be architecturally useful but must not create two competing unexplained progress bars. Future UI should explain visible season progress and that opponents are selected using skill history, without exposing an opaque formula as gameplay homework.

## Features and expression not to copy literally

- The reference's civilization roster, identities, unique advancement structures, unit art, map layouts, interface placement, campaigns, text, audio or iconography.
- Full feature breadth, enormous content libraries, many queues or large team modes at launch (pp. 10, 19).
- Desktop hotkey/APM expectations or input automation transplanted without touch usability tests (pp. 12, 19).
- Long default match duration, unbounded late-game stalemates or obscure resign/result flows (pp. 5, 17–19).
- Seasonal calendars, commercial offerings and announced content described by the study as if they were requirements for this new project (pp. 12–14, 19–20).
- Hidden/visible rating terminology or league branding lifted from the reference; Emberfield's labels remain configurable and its explanations original.

## Immediate consequence

Phase 0 creates the foundation. Phase 1 proves selection and movement with primitive presentation. Phase 2 follows only after healthy compilation, tests and build evidence and proves resource → construction → training. Combat, progression, factions, complete AI matches, art and networking follow their gates. A small working decision loop is stronger evidence than a list of untested systems.
