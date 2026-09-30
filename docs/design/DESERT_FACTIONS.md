# Desert factions — Sultanato and Confederación del Sahel

Status: **playable since 2026-09-28.** This document began as a proposal (2026-09-27) and now records what was built,
including where the build departed from the proposal and why (§8). Sultanato's unique unit is the ranged **Camel
Archer** (`camel_archer`) and Sahel's the melee **Quilted Lancer** (`quilted_lancer`), the pairing the art was
modelled for.

Both factions are **fictional polities inspired by desert trading and savanna cavalry cultures**, not depictions of a
real nation, ethnicity or religion: no real flags, no religious symbols or script used as decoration, no caricature.
Naming follows the game's convention of a generic polity noun (Compact, March, Clans, Dominion, Covenant, Kingdom,
Brotherhood — `FactionKind` in `Assets/Game/Simulation/FactionDefinitions.cs`) rather than a real country or people's
name.

## 0. Why this is a low-risk addition

Every simple faction — Miraj (legacy), Skeld, Solar, Verdant, Ashen, Drakeforged, English — is pure content: a
`FactionDefinition` row using only the generic bonus fields (`ArmorBonusTags`/`ArmorBonus`,
`BonusGatherResource`/`ResourceGatherBonus`, `WorkerCarryBonus`, `CavalrySpeedBonusPermille`, `MeleeLifeSteal`,
`HomeHealingPerSecond`), one `UniqueUnitId` trained at a shared building and one `UniqueTechnologyId` researched at the
shared `archive`. Only Aven and Serevin have bespoke engine mechanics (Storeyard charters and Threadkeeper relays;
mobile outposts and Ashrunner reposition), which `FactionCatalog` gates on their `FactionKind`.

Sultanato and Sahel follow the simple-faction pattern: no new commands, no new `UnitState`/`BuildingState` fields, no
new systems. The rules change is two `FactionKind` values, content rows in `greybox.json`, the historical roster in
`ContentRealms` and the server's `content-realms.mjs`, and one generalised line in the offline AI (§5).

## 1. Realm, rivals and battlefields

Both join `ContentRealms.Historical`, beside `aven`, `serevin` and `english`:

```csharp
// ContentRealms.cs
case "aven": case "serevin": case "english": case "sultanate": case "sahel": case "miraj": return Historical;
FactionsForRealm(Historical) => { "aven", "serevin", "english", "sultanate", "sahel" }
```

- **Offline rival.** `OpponentFaction` pairs the two desert factions with each other, as it pairs Aven and Serevin.
  Round-robin alone would have moved the English from their Aven rival to the Sultanato, so the English keep Aven
  explicitly. Fantasy and Naval rosters are unchanged.
- **Battlefields.** Like every historical faction they play Amber Crossing, Sapphire Coast and Sunscar Basin, never
  the fantasy realm's Tierras de Leyenda. **Sunscar Basin is their home**: `FrontierCodex.HomeMap` suggests it, and the
  skirmish screen and the secondary offline selector move to it when a desert faction is chosen, as long as the
  battlefield was still the previous faction's suggestion. A battlefield the player picked stays picked. The server
  keeps the realm's default map (`amber_crossing`) for requests that name none; online players choose the map.
- **Online.** `Server/content-realms.mjs` lists both under `historical`, so private rooms, casual and ranked queues,
  the C# authority, historical ratings and scoped history accept them and still reject them under Fantasy or Naval.
- **Maps are not dressed per faction.** Historical maps carry no `ZoneRuns`; the desert identity comes from the
  art (§6). A "Historical Lands of Legend" with a dune quarter and a savanna quarter remains a deferred idea (§7).

## 2. Faction definitions (as shipped in `greybox.json`)

```json
{
  "Id": "sultanate", "DisplayName": "Sultanato", "Kind": 13, "RealmId": "historical",
  "Description": "Caravanas y canteras del desierto. Los Aldeanos recolectan una piedra más por ciclo y la caballería se mueve un diez por ciento más rápido. El Arquero de camello hostiga a la infantería desde la silla; la caballería enemiga lo alcanza y los arqueros a pie lo superan en alcance.",
  "UniqueUnitId": "camel_archer", "UniqueTechnologyId": "sultanate_composite_bows",
  "BonusGatherResource": 3, "ResourceGatherBonus": 1, "CavalrySpeedBonusPermille": 100
},
{
  "Id": "sahel", "DisplayName": "Confederación del Sahel", "Kind": 14, "RealmId": "historical",
  "Description": "Pastores y jinetes de la sabana. Los Aldeanos recolectan una comida más por ciclo y la infantería gana un punto de armadura por sus escudos de mimbre. El Jinete acolchado, con armadura de algodón, arrolla a los arqueros a pie y a caballo; las lanzas lo detienen.",
  "UniqueUnitId": "quilted_lancer", "UniqueTechnologyId": "sahel_quilted_barding",
  "BonusGatherResource": 0, "ResourceGatherBonus": 1, "ArmorBonusTags": 2, "ArmorBonus": 1
}
```

- `Kind` 13 and 14 leave 10–12 free for the three planned naval factions (`FactionKind.DesertSultanate`,
  `FactionKind.SahelConfederation`).
- `BonusGatherResource: 3` is Stone: the Sultanato is Historical's first Stone economy. `0` is Food for the Sahel.
- `ArmorBonusTags: 2` is `Infantry` alone, so the Sahel's woven shields armour the Reedguard, not its own cavalry.
- Faction rows carry Spanish text directly, like the English and the Pirates; `EnglishCatalog` gives the English
  screen "Sultanate", "Sahel Confederation" and both descriptions.

## 3. Unique units

Both train at the shared `muster_hall` (its `TrainableUnitIds` now end with `camel_archer`, `quilted_lancer`) from
Settlement, beside Threadkeeper and Ashrunner. Unit names are English in `greybox.json` and translated by
`SpanishCatalog`: "Camel Archer" → "Arquero de camello", "Quilted Lancer" → "Jinete acolchado". The Sahel model's art
roster name was changed from "Lancero acolchado" to "Jinete acolchado" too, because in game Spanish "Lancero" is the
Reedguard, the unit that counters it.

```json
{
  "Id": "camel_archer", "DisplayName": "Camel Archer", "RequiredFactionId": "sultanate", "RequiredEraId": "settlement",
  "MaxHealth": 90, "MoveSpeedMillimetresPerSecond": 4400, "RadiusMillimetres": 350, "Armor": 0, "Tags": 28,
  "Cost": { "Food": 70, "Wood": 35, "Metal": 0, "Stone": 0 }, "PopulationCost": 1, "TrainTicks": 160,
  "Attack": { "Damage": 9, "RangeMillimetres": 4000, "AcquireRangeMillimetres": 6000, "CooldownTicks": 26,
    "ProjectileSpeedMillimetresPerSecond": 7000, "Bonuses": [ { "TargetTags": 2, "MultiplierPermille": 1300 } ] }
},
{
  "Id": "quilted_lancer", "DisplayName": "Quilted Lancer", "RequiredFactionId": "sahel", "RequiredEraId": "settlement",
  "MaxHealth": 120, "MoveSpeedMillimetresPerSecond": 4600, "RadiusMillimetres": 350, "Armor": 2, "Tags": 36,
  "Cost": { "Food": 90, "Wood": 0, "Metal": 30, "Stone": 0 }, "PopulationCost": 1, "TrainTicks": 180,
  "Attack": { "Damage": 12, "RangeMillimetres": 900, "AcquireRangeMillimetres": 6000, "CooldownTicks": 26,
    "ProjectileSpeedMillimetresPerSecond": 0, "Bonuses": [ { "TargetTags": 8, "MultiplierPermille": 1500 } ] }
}
```

(Abridged: the shipped rows also carry the soldiers' usual `IsWorker: false`, `CarryCapacity: 10`,
`GatherIntervalTicks: 10`, `GatherAmount: 2` and `RequiredTechnologyIds: []`.)

**Camel Archer.** `Tags: 28` is `Cavalry | Ranged | Light`: Historical's first ranged cavalry. ×1.3 against plain
`Infantry`. Its counters are existing units: Strider (5.0 m/s, faster even than the Sultanato's 4.84 m/s camel, ×1.3
against `Ranged`), Stringwarden (5.0 m range against 4.0 m) and Reedguard (×1.8 against `Cavalry`) if it reaches the
archer. What it beats is anything it can keep at range: slow infantry and workers, when it is kited (§7).

**Quilted Lancer.** `Tags: 36` is `Cavalry | Heavy`: heavier, slower and dearer than the Strider (120 health / 2 armor /
4.6 m/s / 120 resources against 110 / 1 / 5.0 / 80). ×1.5 against `Ranged`, so it rides down foot archers and the
Camel Archer. The Reedguard's ×1.8 against `Cavalry` stops it.

The HUD's role buttons count a mounted archer with the archers ("Keep your distance"), not with the cavalry ("Strong
against archers"); the unit panel still names it cavalry, which is what the counters see.

## 4. Unique technologies

Researched at the shared `archive` in Dominion, the template every simple faction uses, scoped to the unique unit with
`TargetTags: 511` and `TargetUnitId`:

| Id | Name (English / Spanish) | Cost | Effect |
| --- | --- | --- | --- |
| `sultanate_composite_bows` | Composite Bows / Arcos compuestos | 120 F, 120 M, 600 ticks | Camel Archer attack +2 (`Kind` 0). |
| `sahel_quilted_barding` | Quilted Barding / Bardas acolchadas | 120 F, 120 M, 600 ticks | Quilted Lancer armor +2 (`Kind` 1). |

Both pass `FactionCatalog`'s per-faction reachability closure (Settlement → Kingdom → Archive → Dominion → the
technology), which World creation runs on every load.

## 5. The offline AI

`OfflineAi` needs no knowledge of the new factions to play them: recruitment, the "own soldier until a third of the
army" preference, counter scoring and research planning are all generic. One line was generalised: a faction whose
own soldier trains at the Muster Hall no longer raises a Beast Lodge it has no creature for. That rule used to be a
hard-coded exception for Skeld; it now reads the faction's `UniqueUnitId` against the hall's roster, which gives the
same answer for every existing faction (Skeld skips the lodge; English, with no unique, and Pirates, whose hero trains
at the Hearth, still build one) and makes the desert pair skip it. The state hashes of every existing scenario are
unchanged (§7).

## 6. Art

The desert art pass (2026-09-27, about 806 credits across both cultures, none in this integration) is in the repo:

| Culture | Units (roster id → game unit) | Buildings | Fortifications and ladder | Owner colour |
| --- | --- | --- | --- | --- |
| sultanate | warrior → reedguard, worker → tender, archer → stringwarden, camel rider → strider, camel archer → camel_archer | nine, whitewashed mudbrick and sandstone, flat roofs, horseshoe arches, LOD1 at 40% | wall, gate, leaf built in Blender and painted as a group; palm-trunk ladder on a sled | turquoise cloth |
| sahel | worker → tender, spearman → reedguard, archer → stringwarden, horse rider → strider, quilted lancer → quilted_lancer | nine, sun-dried mud plaster, toron beam ends, thatch, LOD1 at 40% | wall, gate, leaf built and painted as a group; palm-trunk ladder on a log skid | indigo cloth |

`MeshyUnitVisuals.FactionId` maps the two kinds to `sultanate`/`sahel`, and every culture lookup (units, buildings,
walls and gates, the gate leaf, the siege ladder) goes through it. `MeshyUnitBaker`'s building-style map gained both
styles, and an unknown style now fails the bake instead of silently baking a building no faction draws. The riders use
the mount pipeline's donors (camels: Alpaca and Stag with `tailClear`; Sahel: Horse); the Sahel bipeds use the new
`rigidParts` finish option. `AlphaWorldArt` has procedural stand-ins for both riders for the art-free paths.

In-game review: `-emberfieldMeshyReview <folder> <faction> sunscar_basin` now arranges both rows, player 1's town
and a wall-gate-wall stretch on free ground around the Hearth and adds a walls still. Contact sheets from the
2026-09-28 player are `TestResults/Desert-20260928/{sultanate,sahel}_sunscar_sheet.jpg`.

Known art limits carried over from the art pass: the camel archer's reins stretch into a rod when the head drops in
Attack, and its forelegs fold flat at the walk; the Sahel ladder is narrow (0.48 m); the Sahel beast lodge came out as
dark timber; the Sahel shelter has no owner colour; the quilted barding is painted stripes, not bulk; faces read dark
at a distance. Cultural care in the art: no flags, crescents, script or mosque silhouettes; the Sahel watchtower ends
in a roofed lookout storey so it reads as secular.

## 7. Verification (2026-09-28)

**Tests.** `Assets/Tests/PlayMode/DesertFactionTests.cs` (realm and rivals; definitions and faction gates; the shared
counter tags; a scripted match on Sunscar Basin in which each faction builds every building open to it, trains every
unit its buildings offer and researches its technology; a natural Hard AI match; the counter probes below; each
culture's art). The realm selector, faction realm, fortification, ladder, prop, localisation and online recovery
suites gained desert cases, and `Server/tests/service.test.mjs` a desert queue, rating and history test.

**Determinism.** `tools/Verify-SimulationTicks.ps1 --scenario all` before and after the rules change: all six scenarios
(match-amber, match-legend, match-sunscar, battle, mass-order, siege) hash identically at every checkpoint.

**Counter probes.** Shipped definitions, open 1 m terrain, groups 7 m apart, each soldier ordered on its nearest enemy
and re-ordered every second; faction passives apply (the shared units fight as Aven), no research. Each case is run
from both seats; the numbers are identical from either seat unless shown.

| Probe | Predicted (proposal) | Result |
| --- | --- | --- |
| 1 Camel Archer vs 1 Quilted Lancer | Lancer | Lancer, 6.15 s, 85/120 health left |
| 1 Camel Archer vs 1 Strider | Strider | Strider, 6.95 s, 62/110 left |
| 1 Camel Archer vs 1 Reedguard | Reedguard, if it reaches the archer | Reedguard, 5.90 s, 50/100 left |
| 1 Camel Archer vs 1 Stringwarden | Stringwarden (range) | Stringwarden, 8.80 s, 7/70 left |
| 1 Quilted Lancer vs 1 Reedguard | Reedguard | Reedguard, 7.85 s, 34/100 left |
| 1 Quilted Lancer vs 1 Stringwarden | Lancer | Lancer, 4.95 s, 80/120 left |
| 1 Quilted Lancer vs 1 Strider | — | Lancer, 12.30 s, 20/120 left |

At equal nominal resources (1,680: 16 Camel Archers, 14 Quilted Lancers or 21 of an 80-resource soldier):

| Probe | Winner | Survivors (seat 1 / seat 2) |
| --- | --- | --- |
| 14 Quilted Lancers vs 16 Camel Archers | Lancers | 9 / 10 |
| 21 Striders vs 16 Camel Archers | Striders | 16 / 16 |
| 21 Reedguards vs 16 Camel Archers | Reedguards | 16 / 12 |
| 21 Stringwardens vs 16 Camel Archers | Stringwardens | 16 / 16 |
| 21 Reedguards vs 14 Quilted Lancers | Reedguards | 18 / 16 |
| 21 Striders vs 14 Quilted Lancers | Striders | 12 / 14 |
| 14 Quilted Lancers vs 21 Stringwardens | even: the side in seat 1 wins | Lancers 4 (216 health), Stringwardens 2 (140 health) |

Kiting (the Camel Archer falls back five metres, curving, whenever an enemy closes to three): 1 against 1 Reedguard, the
archer wins in 14.85 s untouched (90/90); 16 against 21 Reedguards, the archers win in 41.6 s with 15 left. Standing,
both lose. The Camel Archer is therefore a micro unit: in a standing fight it loses to every shared role at equal
cost, and kited it beats the spears that would catch it. The Quilted Lancer wins its duels against archers and the
Strider but, at equal cost, only draws against massed Stringwardens and loses to massed Striders and Reedguards. These
probes check that the counters hold; they are not full-match balance.

**Natural AI matches** (Hard against Hard, the offline render probe's alternating think order; recorded with the
shipped rules and the lodge rule of §5). Sultanato against Sahel, each map and mode from both seats — all 12 finish:

| Map | Mode | Sultanato in seat 1 | Sahel in seat 1 |
| --- | --- | --- | --- |
| sunscar_basin | Conquest | Sahel, 21.7 min | Sultanato, 10.8 min |
| sunscar_basin | Dominion | Sahel, 17.8 min | Sultanato, 20.6 min |
| amber_crossing | Conquest | Sultanato, 10.2 min | Sultanato, 9.9 min |
| amber_crossing | Dominion | Sultanato, 19.5 min | Sahel, 15.0 min |
| sapphire_coast | Conquest | Sultanato, 9.5 min | Sultanato, 8.5 min |
| sapphire_coast | Dominion | Sahel, 16.9 min | Sultanato, 17.4 min |

Sultanato 8, Sahel 4. Both fielded their unique unit in every match (10–51 each), and 20 of the 24 seats researched
their technology.
Against the other historical factions (Conquest, Sunscar Basin and Amber Crossing, both seats, 4 matches per pairing):
Sultanato 3–1 against Aven, 4–0 against Serevin, 2–2 against the English; Sahel 3–1 against Aven, 4–0 against Serevin,
2–2 against the English. Aven and Serevin lose most AI matches to any "expanded army" faction (the English beat Aven
3–1 in the same harness before the desert pair existed), so the useful comparison is the English: both desert
factions play level with them. Two Normal matches on Sunscar Basin also finished (both Sultanato). The PlayMode natural
match (Sahel in seat 1, Sunscar Basin, Conquest) ends by Conquest for player 2 after 10.8 minutes, the same result as
the .NET harness.

## 8. Where the build departs from the proposal

- **No carry bonus for the Sultanato.** The proposal's JSON gave it `WorkerCarryBonus: 1` on top of the Stone bonus and
  the cavalry speed, while its own description named only the latter two. It ships with those two, matching the
  Sahel's two bonuses.
- **The offline rival.** The proposal expected `OpponentFaction`'s round-robin to absorb the new factions; that would
  have changed the English's rival, so the desert pair is paired explicitly and the English keep Aven (§1).
- **Home battlefield.** The selectors suggest Sunscar Basin (§1); the proposal left the map choice as it was.
- **The AI's lodge rule** (§5), which the proposal did not anticipate.
- **Soldier gathering fields** follow the other soldiers' `10`/`2`, not the proposal's `20`/`1`; soldiers do not gather,
  so neither value has an effect.
- **The mounted archer's HUD role** (§3).

## 9. Deferred, and open

- **Sultanato — "Caravan toll"**: Camel Archers escorting a loaded worker grant it carry or speed. A new per-tick
  proximity check, real new code. Deferred.
- **Sahel — "Watering rights"**: home healing near any drop-off rather than only Hearth and Keep
  (`SiegeSystem.Regenerate`'s `hearth`/`keep` check). A behaviour change to a shared method; its own review. Deferred.
- **Historical cultural lands map** (a dune quarter and a savanna quarter on one map, like `legend_lands`). Deferred.
- **The AI does not kite.** Its Camel Archers fight standing, where they lose; the Sultanato still wins its share of
  AI matches through its economy and army mix. A human who kites gets far more out of the unit.
- **Cultural-care sign-off** is a judgment call: a reviewer with the relevant cultural expertise should look at the
  names, the flavour text and the art before a public release.
