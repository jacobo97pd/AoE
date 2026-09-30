# Alpha 0.3 — biome and world art

This document describes source implementation. The current phase report owns final build, test and screenshot acceptance; the recipes below are not a claim of finished production assets or device performance.

## Original geometry and shared rendering

The expansion uses original C# mesh recipes through `AlphaMeshBuilder`, `AlphaExpandedGeometry` and `AlphaBiomeGeometry`. No external model, texture, animation library or paid service is used for these world assets. The existing Alpha 0.2 menu illustration and fonts retain their separate [provenance](ALPHA_ASSET_PROVENANCE.md).

`AlphaWorldArt` supports 16 unit/transport/equipment IDs and 12 building IDs for eight faction identities. These are **rendering combinations**, not 16 trainable units for every faction: simulation definitions control faction, Era and technology availability. Native geometry provides meaningful silhouettes, team-color masks, shadows and two shared LOD meshes per archetype/faction. All instances of the same combination share the material and meshes.

Faction visual vocabulary:

| Faction | Visible vocabulary |
| --- | --- |
| Aven Compact | Terraces, pale plaster, offset towers, square braces and broad cloth tabs. |
| Serevin March | Low wedge roofs, timber runners, pointed shields, packed supplies and narrow pennants. |
| Miraj Sultanate | Sandstone, turquoise domes, finials, wrapped headgear and cloth tails. |
| Skeld Clans | Dark longhouse roofs, timber roof ends, fur shoulders and horned equipment. |
| Solar Kingdom | Ivory stone, gold crests, sun disks, bright shoulder plates and vertical spires. |
| Verdant Covenant | Branch supports, leaf canopies, antlers, bark and green shoulder foliage. |
| Ashen Dominion | Dark stone, angular spines, heavy shoulders and ember accents. |
| Drakeforged Clans | Metal roofwork, forge chimneys, scale-like armor, horned helms and brass details. |

The six creatures have different body recipes: `sun_lion` has a mane and four legs; `grove_guardian` has a trunk body, branch arms, roots and antlers; `war_troll` has a broad humanoid body, tusks and a club; `ember_drake` has horns, four feet, a segmented tail and articulated wings; `dune_elephant` has ears, trunk, tusks and a howdah; `frostguard` has a large armored body and distinctive headgear. `siege_ram`, `siege_ladder` and `siege_tower` use separate timber, wheel, roof and access-platform recipes.

New buildings are `wall`, `gate`, `watchtower`, `keep`, `beast_lodge` and `siege_workshop`. Fortification height and deck occupancy come from simulation; graphics do not create another collision or navigation system.

The mesh cache is bounded by public identifiers: at most 224 faction/archetype pairs, 12 biome/resource pairs, one beacon pair and three animated-part pairs. That is at most **480 shared meshes**, created lazily. Mesh sharing is independent of owner and cosmetic. This is a cache bound, not a count of meshes visible at once or a measured GPU budget. The small projectile meshes use a separate three-entry shared cache.

## Animation and tactical reading

Movement/working/attack motion is driven centrally by observed simulation state in `WorldView`; individual soldiers do not run their own `Update`. Dragon wings have independent transforms, infantry climbing interpolates from a visible boarding countdown to a wall deck, and the gate portcullis retracts when the actual gate state is open. Shared LODs remain active on the articulated parts.

Defensive arrows originate at tower or wall elevation. The keep uses a stone silhouette; the drake uses an ember silhouette. These render the actual gameplay projectile trajectory with a cosmetic vertical arc. Source archetypes are resolved only while the source is visible, so graphics do not disclose hidden attackers. A fixed pool of 24 falling oil/fire elements responds to observed increases in the keep's oil cooldown and hides effects when the source leaves visibility. Reappearing buildings reset the observation baseline instead of replaying a hidden discharge.

Selection uses a projected capsule based on the canonical archetype body height and width. This permits tapping the upper body of a creature or siege tower. Cosmetic ornaments and wings do not enlarge that target; health and selection also follow actual wall elevation.

These are procedural, faceted models and transform animations. They do **not** constitute a skeletal animation pipeline, motion-captured performance or a complete production asset library. The drake still uses ground navigation, and large creatures retain the current simulation's compact navigation radius; their visual bulk is not a new large-footprint movement implementation.

## Three playable biomes

| Map | Topology and economy | Environment |
| --- | --- | --- |
| `amber_crossing` / forest | Existing 96×72 crossing layout and resource starts retained. The public river ridge and legal fords remain unchanged. | Mixed woodland resources, grass, shallow river/stone fords, boundary windmills and village silhouettes, warm meadow light. |
| `sapphire_coast` / caribbean | 104×80. Mirrored coastal water inlets, ruin islands and offset rocky barriers create different resource expansion routes and three public objectives. | Animated turquoise water, beaches, palms, fruit shrubs, old stone ruins, piers and original sailship props; cooler sky fill. Ships are scenery and do not imply naval combat. |
| `sunscar_basin` / desert | 104×80. Mirrored oasis ponds, ridge blocks and monument islands leave open routes around the central court, with separate resource expansion positions. | Warm sand, dunes beyond the playable boundary, sandstone pyramids and obelisks, date palms, oasis water and camel caravans; warmer directional light. |

Each new map starts with four workers and a Hearth per player, 24 resource nodes arranged as identical mirrored pairs, and three Dominion objectives. Terrain blockers retain exact 180-degree symmetry. Connectivity and symmetric resource placement are engineering fairness checks, not proof of human competitive balance.

Terrain colors, resource meshes and lighting follow `MapDefinition.BiomeId`. Realm affects the atmosphere without mixing simulation rules. Public landmarks are placed on existing blocked terrain or outside the playable boundary. Water surfaces follow the authored blocked coast/pond cells; unblocked routes are not secretly changed by a visual. Low grass adds no collider. Off-map landmarks use a nearby in-bounds exploration anchor; normal decorations and landmarks are revealed through exploration and do not consult enemy economic state.

## Cosmetic isolation

`AlphaWorldArt.ApplyCosmetic` changes only a `MaterialPropertyBlock`: `_CosmeticPrimary`, `_CosmeticAccent`, `_CosmeticBlend` and `_CosmeticEmission`. An equipped palette replaces the surface hue while retaining the recipe's light/dark variation; it does not leave the original red RGB dominant beneath a blue cosmetic. Vertex alpha distinguishes ownership cloth from ornamental accents, preserving the readable team color. Clearing equipment restores the original material appearance. This creates no new gameplay state, modifies no radius, tags, health or damage, and does not duplicate a mesh for each cosmetic.

`CosmeticLoadout.Resolve(definitionId, realmId, ownerId)` supplies the appearance. A revision counter refreshes existing instances after equipment changes. Offline local previews and online authoritative equipment are separate sources; opponent online equipment is applied only from the supplied equipment metadata. This document describes the rendering boundary, not purchase entitlement validation.

The Store uses the same shared models in `CosmeticModelPreview`, isolated on its own rendering layer and rendered explicitly through `RenderPipeline.SubmitRenderRequest`. Its texture follows the actual UI card aspect ratio, with each dimension bounded at 512 pixels; projected, oriented mesh bounds frame the model with a ten-percent margin. This avoids stretching a fixed 4:3 texture into a wider card or shrinking a model according to its entire three-dimensional diagonal.

The preview camera disables its own shadows and postprocessing. Its model renderers enable `_PreviewLighting`, an instance-only studio-lighting branch with key/fill illumination and a subtle rim. That property defaults to zero for battlefield renderers. The preview changes neither global `RenderSettings` nor the active match's lights, atmosphere or material instances, and its blue-gray background stays independent of the world's illumination.

## Validation scope

The expanded PlayMode checks cover all eight visual identities, shared LOD meshes, normal/vertex validity, geometry and footprint budgets, owner masks, contextual selection, fog privacy, independent biome resource silhouettes, mirrored/connectable map data, cosmetic isolation and upper-body selection. A render test exercises the actual `FacetedTeam` shader and reads pixels: the original orange surface must become visibly blue with Sapphire equipped, the accent must change, ownership cloth pixels must remain unchanged, and clearing the style must restore the source. This verifies more than the presence of material-property values.

Camera rotation checks cover legal ground footprints after successive quarter turns at map corners, central projection/zoom after a complete turn and reversible ground-anchored panning following an oblique rotation. These checks should be read together with the final Unity and packaged-player reports. Native screenshots, preview composition, desktop rendering measurements and eventual physical device validation remain separate evidence.
