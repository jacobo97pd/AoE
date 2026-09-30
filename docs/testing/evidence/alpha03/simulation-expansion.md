# Alpha 0.3 simulation expansion evidence

Run completed: 2026-09-10T11:52:05.120119Z

**48/48 natural same-realm AI matches finished**, covering eight starting factions, three maps and both victory modes. No match reached the 1,800-second deadline. The same run completed eight paid progression/production scenarios, thirty population/resource-budget counter measurements, five DPS measurements and one dispersed-archer scenario: 92 scenarios, zero runtime failures.

Rules SHA-256: `B4FF84B570F0A18B9CFCE017669BC4F50EA31347C63CCAE282BBD865AD40BA5E`. Simulation source SHA-256: `82BF07214E7C3B55498E9C3E16845F0424BEB0CFAED1808E0623C18A93C37F75`. The collector checked these frozen inputs and all three map hashes against the current files.

Run `powershell -NoProfile -ExecutionPolicy Bypass -File tools/Verify-ExpansionSimulation.ps1` to reproduce. [Raw observations](simulation-expansion.json) preserve per-player stocks, spent resources, produced army types, research progress and rejected-command counts. This is deterministic headless rules/pacing evidence, not human competitive balance or rendered FPS.

## Natural match duration

| Map | Mode | Finished | Minimum seconds | Median seconds | Maximum seconds |
| --- | --- | ---: | ---: | ---: | ---: |
| amber_crossing | Conquest | 8/8 | 442.80 | 600.92 | 941.35 |
| amber_crossing | Dominion | 8/8 | 881.35 | 1189.12 | 1388.05 |
| sapphire_coast | Conquest | 8/8 | 484.10 | 707.05 | 945.20 |
| sapphire_coast | Dominion | 8/8 | 756.70 | 1201.90 | 1325.15 |
| sunscar_basin | Conquest | 8/8 | 463.75 | 615.42 | 956.95 |
| sunscar_basin | Dominion | 8/8 | 1094.35 | 1175.10 | 1483.20 |

The AI issues ordinary commands against visible targets; the runner fails any accepted attack on an entity outside that player’s vision. All eight authored progression fixtures reached Empire, paid their unique technology and actually trained their unique unit plus ram, ladder and siege tower. Natural AI produced every new unique military type across the matrix.

Two exploratory Conquest cases stalled when food ran out below the normal army buildup threshold. The final AI commits its surviving army after fifteen minutes with fewer than 100 food, using only its own state and preserving Aven/Serevin behavior. The final runs now finish at 945.20 seconds (Verdant/coast) and 956.95 seconds (Solar/desert). No resources, hidden targets or victory results were injected.

## Positioning and resource trade measurements

These fixtures start deployed armies. “Equal resource budget” uses the sum of food, wood, metal and stone and rounds the opposing unit count down to fit; it does not equate their strategic gathering value. Armies attack directly without human kiting. All five creatures lose against prepared equal-population spears. Each has a favorable trade against clustered archers.

| Creature | Equal-population spears | Equal-population archers | Equal-population cavalry | Equal-budget spears | Equal-budget archers | Equal-budget cavalry |
| --- | --- | --- | --- | --- | --- | --- |
| dune_elephant | 3 counters survive / 9.25s | Creature survives / 16.10s | Creature survives / 23.90s | 4 counters survive / 9.25s | Creature survives / 21.50s | 3 counters survive / 21.10s |
| sun_lion | 3 counters survive / 4.10s | Creature survives / 11.35s | 2 counters survive / 8.80s | 3 counters survive / 4.10s | Creature survives / 11.35s | 2 counters survive / 8.80s |
| grove_guardian | 3 counters survive / 10.50s | Creature survives / 28.80s | Creature survives / 27.50s | 5 counters survive / 8.35s | Creature survives / 36.00s | 1 counters survive / 28.40s |
| war_troll | 3 counters survive / 7.75s | Creature survives / 17.85s | 2 counters survive / 16.20s | 3 counters survive / 7.75s | Creature survives / 17.85s | 2 counters survive / 16.20s |
| ember_drake | 4 counters survive / 5.80s | Creature survives / 15.10s | 4 counters survive / 8.10s | 4 counters survive / 5.80s | Creature survives / 15.10s | 4 counters survive / 8.10s |

Five clustered archers lose to the Ember Drake in 15.10 seconds. The same five archers spread around it win in 13.45 seconds with three survivors: splash and spacing change the outcome. This is a bounded positioning check, not a claim that drakes cannot be countered by ranged armies.

## All natural results

| Map | Starting faction | Mode | Winning faction | Seconds | Deaths | Final eras |
| --- | --- | --- | --- | ---: | ---: | --- |
| amber_crossing | aven | Conquest | aven | 459.50 | 61 | 4 / 4 |
| amber_crossing | aven | Dominion | aven | 1160.85 | 82 | 4 / 4 |
| amber_crossing | serevin | Conquest | aven | 488.70 | 55 | 4 / 4 |
| amber_crossing | serevin | Dominion | aven | 881.35 | 86 | 3 / 4 |
| amber_crossing | miraj | Conquest | skeld | 646.15 | 96 | 4 / 4 |
| amber_crossing | miraj | Dominion | miraj | 1116.00 | 73 | 4 / 4 |
| amber_crossing | skeld | Conquest | aven | 442.80 | 36 | 3 / 4 |
| amber_crossing | skeld | Dominion | aven | 1087.95 | 68 | 4 / 4 |
| amber_crossing | solar | Conquest | verdant | 555.70 | 72 | 4 / 4 |
| amber_crossing | solar | Dominion | verdant | 1217.40 | 87 | 3 / 4 |
| amber_crossing | verdant | Conquest | verdant | 918.70 | 125 | 4 / 4 |
| amber_crossing | verdant | Dominion | verdant | 1258.80 | 90 | 4 / 4 |
| amber_crossing | ashen | Conquest | drakeforged | 941.35 | 70 | 4 / 4 |
| amber_crossing | ashen | Dominion | drakeforged | 1378.10 | 74 | 4 / 4 |
| amber_crossing | drakeforged | Conquest | drakeforged | 904.00 | 40 | 4 / 4 |
| amber_crossing | drakeforged | Dominion | solar | 1388.05 | 75 | 4 / 4 |
| sapphire_coast | aven | Conquest | aven | 484.10 | 50 | 4 / 4 |
| sapphire_coast | aven | Dominion | aven | 804.05 | 73 | 4 / 4 |
| sapphire_coast | serevin | Conquest | aven | 678.60 | 114 | 4 / 4 |
| sapphire_coast | serevin | Dominion | aven | 756.70 | 48 | 4 / 4 |
| sapphire_coast | miraj | Conquest | miraj | 657.25 | 61 | 4 / 4 |
| sapphire_coast | miraj | Dominion | miraj | 1197.40 | 64 | 4 / 2 |
| sapphire_coast | skeld | Conquest | aven | 791.20 | 114 | 4 / 4 |
| sapphire_coast | skeld | Dominion | aven | 1122.15 | 62 | 4 / 4 |
| sapphire_coast | solar | Conquest | solar | 696.50 | 71 | 4 / 4 |
| sapphire_coast | solar | Dominion | verdant | 1206.40 | 74 | 4 / 4 |
| sapphire_coast | verdant | Conquest | ashen | 945.20 | 117 | 4 / 4 |
| sapphire_coast | verdant | Dominion | verdant | 1309.55 | 74 | 4 / 4 |
| sapphire_coast | ashen | Conquest | drakeforged | 860.35 | 113 | 4 / 4 |
| sapphire_coast | ashen | Dominion | ashen | 1252.15 | 82 | 4 / 4 |
| sapphire_coast | drakeforged | Conquest | solar | 717.60 | 77 | 4 / 4 |
| sapphire_coast | drakeforged | Dominion | drakeforged | 1325.15 | 71 | 4 / 4 |
| sunscar_basin | aven | Conquest | aven | 463.75 | 60 | 4 / 4 |
| sunscar_basin | aven | Dominion | aven | 1096.75 | 103 | 4 / 3 |
| sunscar_basin | serevin | Conquest | aven | 483.80 | 65 | 4 / 4 |
| sunscar_basin | serevin | Dominion | aven | 1154.05 | 87 | 4 / 4 |
| sunscar_basin | miraj | Conquest | miraj | 561.80 | 61 | 4 / 4 |
| sunscar_basin | miraj | Dominion | miraj | 1094.35 | 92 | 4 / 4 |
| sunscar_basin | skeld | Conquest | aven | 467.10 | 57 | 3 / 4 |
| sunscar_basin | skeld | Dominion | aven | 1209.35 | 109 | 4 / 4 |
| sunscar_basin | solar | Conquest | solar | 956.95 | 130 | 4 / 4 |
| sunscar_basin | solar | Dominion | solar | 1254.50 | 77 | 4 / 4 |
| sunscar_basin | verdant | Conquest | verdant | 779.65 | 104 | 4 / 4 |
| sunscar_basin | verdant | Dominion | verdant | 1163.85 | 82 | 4 / 3 |
| sunscar_basin | ashen | Conquest | drakeforged | 669.05 | 63 | 4 / 4 |
| sunscar_basin | ashen | Dominion | ashen | 1186.35 | 98 | 4 / 3 |
| sunscar_basin | drakeforged | Conquest | drakeforged | 815.10 | 115 | 4 / 4 |
| sunscar_basin | drakeforged | Dominion | solar | 1483.20 | 119 | 4 / 4 |

[Rules, prices, siege behavior and known limits](../../../technical/EXPANSION_SIMULATION.md). Deck movement uses discrete slots, AI boarding planners remain future work, and creature navigation footprints remain smaller than their large visual silhouettes.
