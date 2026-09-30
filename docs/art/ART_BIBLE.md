# Emberfield art bible

Status: the bounded Phase 9 native art slice is baked and verified by PC tests and packaged phone/tablet-resolution galleries. Physical-device art/performance validation remains unverified. The broader production direction below remains a target. See the [Phase 9 report](../tasks/PHASE_9_REPORT.md) for evidence and [original provenance](ORIGINAL_PROVENANCE.md) for editable sources.

## Current native slice

The original Aven kit covers Tender, Reedguard, Stringwarden and Strider; Hearth, Shelter and Muster Hall; and four resources. Each identity has two merged, flat-shaded mesh levels sharing one vertex-color material. Vertex alpha marks team cloth, with an independent owner tint. The procedural recipes are editable source, and the editor baker produces ordinary Unity mesh assets plus a measured triangle/bounds manifest. There are no downloaded models or painted/AI-generated textures in this kit.

Match-capture review led to broad team-colored roof cloth and a Tender rear pack band at both LODs, plus 180° building facade orientation in presentation. These expose ownership to the fixed tactical camera. The 16-instance galleries at 1280×720 and 1440×1080 passed their composition checks with 22 shared meshes and one kit material. The full verification passed 347 EditMode and 66 PlayMode tests, including asset validity, shared tints/LODs and cosmetic-motion isolation.

Presentation applies the Aven forms in offline matches, faction drills and the explicit art gallery; unauthored models retain their earlier fallback. The Amber terrain variation is visual and flat: it does not change navigation. Whole-model movement bob, work tilt, carry pose and attack lunge provide bounded cosmetic motion. Pooled sparks and original synthesized feedback accompany observed orders, gathering, impacts, construction completion, defeat and objective ownership changes. This is not a skeletal animation or complete sound-production library.

## Visual goal

Stylized 3D settlements and armies in a landscape shaped by recurrent ash seasons. Use broad forms, restrained surface detail and distinct tool/weapon silhouettes. At normal play distance the reading order is silhouette → team → role → animation → detail.

Amber Reach combines warm pale ground, muted vegetation, dark exposed rock and restrained amber mineral accents. Separate units and traversable routes by value and shape; avoid relying on hue alone. Terrain decoration cannot hide footprints, resources, selection rings or projectiles.

## Faction identity

| Identity | Aven Compact | Serevin March |
| --- | --- | --- |
| Large form | Terraced masses and offset square frames | Low wedges and elongated runner bases |
| Structural rhythm | Repeated braces and civic courtyards | Diagonal supports and compact transport bundles |
| Cloth | Broad rectangular tabs and roof bands | Narrow pennants and folded panels |
| Material emphasis | Pale mineral plaster and warm timber | Dark timber, woven cloth and restrained metal |
| Gameplay recognition | Connected stable infrastructure | Obvious packing/deployment states later |

Faction shape and team color must remain independent. An Aven red team is recognizable against a Serevin red test proxy even if shared team colors are prohibited in a normal match.

## Unit and structure readability

- Tender: small broad bundle/tool mass, neutral nonmilitary pose.
- Reedguard: tall narrow spear above a clear infantry body.
- Stringwarden: broad bow silhouette with a different stance from spear infantry.
- Strider: long mounted body profile and clear forward direction.
- Hearth: largest early roof volume with a readable central entrance.
- Shelter: compact single domestic mass.
- Storeyard: open-sided racks and distinct material piles.
- Muster Hall: long training frontage and obvious exit area.

Selectability is not constrained to the mesh. Presentation should provide stable ground footprints and readable selection/team markers. Avoid decorative weapon spikes that resemble a different role.

## Scale and camera

Use meters in source files and Unity. Adopt approximately human scale for workers/infantry, then allow careful weapon and team-panel exaggeration for overhead reading. Buildings and terrain respect simulation footprints even when art is compressed for mobile readability. Confirm with the actual camera zoom limits before producing a whole kit.

Legacy proxies and unauthored entries use primitive capsules/cubes and distinguishing attachments. The Aven slice substitutes merged faceted geometry at the same simulation identity and footprint. No final texture, illustration, character portrait or marketing image is required to use it. Do not replace a missing mesh with unreviewed third-party/reference imagery.

## Team colors and materials

Initial team palette: blue, red, green, yellow. The native slice uses vertex RGB for its base palette and vertex alpha for the team mask, with one shared material and per-owner tint. A future textured kit should use a shared base texture plus mask and player color; do not duplicate textures for each team. Complement color with selection outline/marker shapes where necessary for accessibility.

Shared materials and atlases are preferred. Avoid one material per piece of equipment. Shader keywords, transparency, emissive effects and variants require restraint. Any temporary primitive material setup is a prototype stage, not proof the final masked shader system is delivered.

## Geometry and lighting targets

Initial standard-unit guidance: LOD0 3k–5k triangles, LOD1 1.5k–2.5k, LOD2 500–1k, and a very low LOD/billboard only when the camera makes it useful. These are authoring guidelines, not device performance guarantees. Buildings can have more silhouette detail; measure screen coverage, draw calls, skinning and overdraw as well as triangles.

The native slice uses lower, measured budgets recorded by its [baked manifest](../../Assets/Game/Resources/Art/ART_KIT_MANIFEST.json): two LODs, one merged renderer per level, and Unity LOD/frustum culling alongside match visibility filtering. This does not establish a device-performance result. Use simplified shadows for ordinary troops when needed and prioritize major structures and large forms. More detailed LODs, billboards and final shadow policy require device profiling.

## Animation, VFX and audio

Eventual unit set: idle, walk/run, gather, carry, construct, attack, react and death as relevant. Animation follows simulation state; events may trigger cosmetic feedback but cannot grant damage or resources. Preserve silhouette and avoid violent motion that makes selection hard.

Small pooled VFX communicate gathering, construction, attack, projectile/impact, death, destruction, research, Era advancement and objective capture. Effects must not conceal army composition. Audio groups include music, ambience, combat, units, buildings and UI; prioritize feedback and cap overlapping acknowledgements. Use original or appropriately licensed assets with provenance; placeholder silence/simple local feedback is acceptable initially.

## Vertical-slice acceptance and remaining production work

The verified PC slice supplies one biome, the eleven Aven/resource identities, shared material/team masks, two LODs, basic whole-model motion and selected VFX/audio. The Phase 9 report records the successful bake, validity tests and gallery results. Tactical-zoom screenshots at phone/tablet resolutions are desktop presentation checks. Physical-device play, color-vision usability, thermal/frame-time profiling, skeletal clips, full faction rosters, a texture-atlas pipeline, layered ambience/music and a larger production library remain separate acceptance work.
