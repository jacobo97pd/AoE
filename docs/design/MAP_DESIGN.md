# Map design

Status: initial map direction plus future requirements. A greybox layout is a navigation/economy test space, not a validated competitive map. See [current phase](../tasks/CURRENT_PHASE.md) for scene status.

## Amber Reach

Amber Reach is an original dry river-terrace borderland: pale soil, dark ridges, broad timber stands and warm mineral deposits. Its first implementation uses simple geometry and resource proxies. No reference map geometry, landmark placement or decorative composition is copied.

**Target identity:** a readable two-player land map with safe opening work, exposed expansion resources, a broad central route and flank options. The initial prototype can be considerably simpler while Phase 1/2 controls and economy are validated. Add terrain obstacles only with clear occupancy and routing behavior.

| Area | Intended decision | Design constraint |
| --- | --- | --- |
| Starting terrace | Allocate early workers and choose first production timing. | Equal starting opportunities and reachable basic resources. |
| Near expansion | Increase income while lengthening defense coverage. | Travel exposure is visible; no accidental inaccessible node. |
| Central reach | Contest reinforcement access and future objectives. | A central hold must have counterplay through other routes. |
| Outer approaches | Scout and raid instead of fighting the main army. | Routes wide enough for intended groups; no hidden trap geometry. |
| Forward deposits | Trade safety for advanced-resource access. | Avoid giving one spawn a decisive unearned travel advantage. |

There is no naval layer. Water-like decoration must not suggest a navigable system that does not exist. Elevation initially improves visual composition only unless terrain bonuses and movement rules are explicitly implemented and tested.

## Data and later generation

The intended map model includes stable MapDefinition ID, bounds, MapSeed, BiomeDefinition, ResourceRules, SpawnRules and ObjectiveRules. A seed is reserved for future reproducible variation; storing it does not mean the current layout is procedurally generated.

Begin with authored spawn/resource positions. Later seeded generation varies deposit offsets, forest edges and optional lane details inside authored identity constraints. Generation must validate reachability, spawn clearance, resource budgets, travel distances, building space and objective fairness. The same versioned input must reproduce the same generated map before it is used for competitive synchronization.

## Amber Crossing — implemented offline map

`amber_crossing.json` authors a 96×72-metre map with 180-degree mirrored Hearths at (18,18) and (78,54), four Tenders per side and 24 finite resource nodes. Four-resource starting stock remains the global 120/80/0/40. Each side has nearby essentials and more exposed expansion deposits. Short ridges leave wide alternative approaches; no random map generation is implied.

Conquest destroys every completed enemy Hearth; an unfinished foundation does not keep a player alive. Dominion exposes beacons at (48,36), (30,44) and (66,28), each with a four-metre capture radius. Military units, creatures included, capture in 15 continuous seconds; workers and siege equipment do not; two controlled uncontested points held for eight continuous minutes win. Hostile presence interrupts majority progress. Conquest is also a backup victory in Dominion.

Natural mirrored AI runs finish all four mode/faction assignments. Dominion currently takes 15.9–18.8 simulated minutes; Conquest 7.0–10.2. Aven wins all four samples, so map symmetry does not establish faction balance. See the [Phase 7 report](../tasks/PHASE_7_REPORT.md).

## Lands of Legend — the fantasy realm's map

`legend_lands.json` (baked by `WorldMapBaker.Legend`; 144×112 cells, Hearths at (26,24) and (118,88), four Tenders per side and the same resource template as the other skirmish maps) is the fantasy realm's default map and is offered to no other realm. In Spanish it is *Tierras de Leyenda*.

Each start sits in a land of roughly a third of the map, with a neutral highland band through the centre. The lands are scenery only. `MapDefinition.ZoneRuns` stores a zone per cell as row-major `zone:count` runs (0 the neutral band, 1 and 2 the starting lands), and `MapLands`, resolved once per match as `World.Lands`, gives each land the look of the culture whose starting Hearth stands in it: an elven forest for the elves (`verdant`), a volcanic waste whose water is lava for the orcs (`ashen`), highland plains for the mountain men (`skeld`) and the dwarves (`drakeforged`); the neutral band is highland. The owner is found by the Hearth's position, never by player id, because an online replica renumbers its seats. Elves against dwarves therefore shows no volcanic land, and a mirror match shows the same land twice.

Terrain, water, resources and beacons are 180-degree mirrored exactly as on the other maps, and the zone grid is mirrored with the two lands swapped, so every pairing plays the same map. Besides reachability, clear Hearths and terrain symmetry, the bake refuses the map if a start is not in a land of its own, if the lands are not each other's reflection, or if a pool or stream crosses a land's edge (it would be half lava and half water).

| Feature | Placement |
| --- | --- |
| Beacons | Highland Crown (72,56), Western Cairn (44.5,74.5), Eastern Cairn (99.5,37.5), all in the neutral band. |
| Land streams | Each land has two edge peaks, each feeding a stream into a pool inside the land. The back road from each Hearth to the far cairn fords one of them. |
| Neutral band | Sparse rock outcrops and open ground; a peak and a mountain tarn where the band meets the north and south edges. |
| Woods | Stands of blocked cells are denser in the lands than in the neutral band. |

Landmarks in this map are slots that `MapLands.LandmarkKind` resolves per land: `land_monument` becomes a moonwell in the elven forest, a spiked tower in the volcanic waste and standing stones on the highland and in the neutral band; `land_peak` becomes a volcano in the volcanic waste and a mountain massif anywhere else. `MapLands.LandmarkBiome` names the biome whose scenery holds each monument.

## Information and objectives

Configure playable bounds separately from camera bounds. Fog starts unexplored outside starting vision. Keep visual resource silhouettes legible in visible terrain; explored terrain can preserve known terrain while dynamic enemy information becomes stale.

Fog uses configured cell-centred vision discs. It does not implement terrain occlusion or elevated vision. Explored static terrain remains known; live enemies/resources need current visibility. Placement requires the full footprint in current vision. Mode configuration is explicitly opt-in so old practice maps retain their original behaviour. Capture radius, visibility announcements and hold timers belong to mode data, not decorative meshes.

## Validation ladder

1. One worker can reach valid destinations and cannot receive out-of-bounds orders.
2. Workers can repeatedly gather and return; building footprints fit and invalid sites give a reason.
3. Combat scenarios expose open-ground and protected-range tradeoffs.
4. Measured group tests cover wide routes, narrow approaches, crossings and blocked destinations.
5. Mirrored full matches compare access, scouting timings, expansion value and objective pressure.
6. Real tablet/phone play checks terrain readability, accidental taps, selection occlusion and pinch boundaries.

Do not declare the map balanced from geometric symmetry alone. Starting resource travel, usable construction space and faction movement differences can still create unequal opportunities.
