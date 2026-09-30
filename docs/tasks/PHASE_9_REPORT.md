# Phase 9 — bounded native art slice

**PC engineering verified for the bounded Phase 9 art slice.** The native asset bake succeeded, the complete Unity run passed **347 EditMode and 66 PlayMode tests**, and the packaged gallery passed at **1280×720 and 1440×1080**. The scope is one original Aven/Amber visual slice. Physical-device performance, final faction-wide artwork and skeletal animation remain unverified or future work. [Current phase](CURRENT_PHASE.md) owns the combined delivery status.

## Scope and provenance

The kit contains four Aven units—Tender, Reedguard, Stringwarden and Strider—three buildings—Hearth, Shelter and Muster Hall—and Food, Wood, Metal and Stone. Eleven identities produce 22 shared LOD meshes and one shared art material. An explicit staged gallery places 16 instances across both team colors; those gallery spawns are not a normal match start.

The editable recipes are original native C# mesh authoring. No downloaded models, traced reference artwork, external model application, texture packs or generated bitmap assets were used. The supplied RTS reference informed general silhouette/readability constraints without supplying copied artwork. [Original provenance](../art/ORIGINAL_PROVENANCE.md) identifies the sources and caveats.

The Aven forms use faceted tunics, cloth tabs, square frames and terraced roof masses. Tool pack, tall spear, wide angular bow and long mounted body separate the roles. Owner tint remains independent of faction shape. Resources have a low harvest patch, faceted tree, amber mineral inclusions and pale rock groups. The Amber biome uses restrained sage/sand variation and a pale traversable route; it remains flat and does not alter navigation or occupancy.

Actual match captures exposed a fixed-camera readability issue: back-facing Tender packs and roof views hid the original front-facing team panels. Both LODs now include a broad team-colored roof cloth strip and a Tender pack wrap/rear band. Building presentation rotates the authored facade 180° around Y toward the tactical camera, without changing the mesh footprint or simulation orientation.

## Implementation and architecture

`ArtKitGeometry` builds editable triangle meshes with flat face normals, vertex palette colors and an alpha-channel team mask. The explicit `ArtKitBaker.Bake` editor action writes mesh assets, the shared instanced material, catalog and a manifest containing the recipe hash, actual triangle/vertex counts, bounds and declared budgets. Rebaking preserves asset identities. There is no import-time automatic rewrite or runtime per-instance geometry generation.

`ArtKit.CreateUnit`, `CreateBuilding` and `CreateResource` return a model root using shared assets, two renderer children and an LOD Group. One renderer is assigned to each detail level. Unit pivots are at ground level and face +Z. Building geometry stays within a normalized 1×1 footprint, scaled to the simulation dimensions; heights stay below 2.3 m. The spear stays below 2.15 m, the mounted figure below 2.3 m, resource widths/depths below 1.4 m and the tree below 2.5 m. The native shader applies vertex palettes, per-owner masked tints and URP lighting/shadow/depth passes.

World presentation enables the bounded slice in offline matches, faction drills and the gallery. Its Aven units/buildings use the new forms; unauthored entries and neutral legacy practice keep their fallbacks. Resource models and the Amber terrain serve the enabled slice maps. Costs, health, selection ownership, movement radii, building footprints and all economic/combat outcomes remain simulation-owned.

Visible presentation state drives movement bob/roll, work tilt, a carry posture and attack lunge. These are whole-model cosmetic motions, with no skeleton or gameplay animation event. `SliceFeedback` reuses a 16-spark pool and six audio voices with per-cue throttles. It synthesizes original short mono cues locally and exposes a sound toggle in the offline menu. Orders, gathering, visible impacts, construction completion, owned losses (all on-screen losses in fog-free practice) and public objective ownership changes provide feedback; hidden enemy events must not emit revealing effects. In fogged matches, enemy-removal cues are conservatively suppressed because a stale rendered position cannot prove where an enemy died between presentation updates. The biome's procedural vertex colors, the authored spark mesh and synthesized waveforms have editable local sources and no external art/audio input. Terrain, feedback and existing HUD materials are separate from the kit's one shared mesh material. This is a bounded feedback set, not a complete soundtrack or every production animation/VFX event.

## Files

- Kit: [ArtKit.cs](../../Assets/Game/Presentation/ArtKit.cs), [ArtKitCatalog.cs](../../Assets/Game/Presentation/ArtKitCatalog.cs), [ArtKitGeometry.cs](../../Assets/Game/Presentation/ArtKitGeometry.cs), [ArtKitBaker.cs](../../Assets/Game/Editor/ArtKitBaker.cs), [FacetedTeam.shader](../../Assets/Game/Resources/Shaders/FacetedTeam.shader).
- Generated assets and real counts: [ART_KIT_MANIFEST.json](../../Assets/Game/Resources/Art/ART_KIT_MANIFEST.json); catalog and 22 mesh assets in the same folder.
- Integration: `WorldView`, `AmberBiome`, `SliceFeedback`, `ObjectiveView`, `MatchController`, `OfflinePanel`, `ArtReviewSmoke`, `art_review.json` and the Amber terrain shader.
- Validation: [ArtKitTests.cs](../../Assets/Tests/PlayMode/ArtKitTests.cs), [SlicePresentationTests.cs](../../Assets/Tests/PlayMode/SlicePresentationTests.cs); packaged gallery and match runners maintained with the main verification tools.
- Art guidance: [art bible](../art/ART_BIBLE.md), [asset pipeline](../art/ASSET_PIPELINE.md), [original provenance](../art/ORIGINAL_PROVENANCE.md).

## Budgets and verification

| Entry type | LOD0 triangle ceiling | LOD1 triangle ceiling |
| --- | ---: | ---: |
| Unit | 1,500 | 900 |
| Building | 2,000 | 1,000 |
| Resource | 1,000 | 500 |

The successful bake produced the following actual triangle counts, all below their ceilings. The [manifest](../../Assets/Game/Resources/Art/ART_KIT_MANIFEST.json) records vertex counts, bounds and paths as well. Its recipe SHA-256 is `e6c0ad5dd5451c847d4ed2a1a4fdd4a53bc991275a8e6cc04d85bcea7f5dbe5a`, matching the final geometry source.

| Identity | LOD0 triangles | LOD1 triangles |
| --- | ---: | ---: |
| Tender | 252 | 188 |
| Reedguard | 248 | 172 |
| Stringwarden | 300 | 176 |
| Strider | 360 | 280 |
| Hearth | 176 | 104 |
| Shelter | 100 | 64 |
| Muster Hall | 196 | 112 |
| Food | 336 | 124 |
| Wood | 84 | 48 |
| Metal | 192 | 80 |
| Stone | 144 | 64 |
| One of every identity | **2,388** | **1,412** |

Both art validity tests passed in the complete **347/347 EditMode + 66/66 PlayMode** run, with no failures or skipped tests. They validate all 11 pairs, finite coordinates, nondegenerate triangles/flat normals, masks, bounds, role extents, lower LOD counts, two merged renderer children, shared meshes/material and independent blue/red tints. The material-property round-trip assertion uses a per-channel `1e-6` float tolerance; the distinct-team and shared-asset checks remain intact. The additional motion/feedback integration passed: repeated presentation sync leaves positions, health, stock and simulation time unchanged, offscreen feedback stays silent, mute suppresses new audio while retaining visual feedback, and the particle pool remains bounded.

Both packaged galleries passed with **16 model instances, 22 unique shared LOD meshes and one kit material**. The verified asset baseline used build GUID `a39b23a98990479a8ff2e3d99692e50e`; the final packaging rerun retains the same art assets and enlarges gallery labels. Composition and nonempty capture checks are separate from visual inspection and do not measure ordinary frame rate.

| Gallery | Capture | Packaged check |
| --- | --- | --- |
| 1280×720 | [Phone gallery](../testing/evidence/phase9-gallery-1280x720.png) | [Art smoke](../testing/evidence/phase9-art-1280x720.txt) |
| 1440×1080 | [Tablet gallery](../testing/evidence/phase9-gallery-1440x1080.png) | [Art smoke](../testing/evidence/phase9-art-1440x1080.txt) |

Final packaged build GUID: **`d1b7b1cbc28740e994945476895e1295`**.

## Limits and follow-up

This slice supplies two hard LOD transitions, no billboard level, no texture atlas, no skeletal clips and no complete Serevin/common-building replacement library. The small meshes remain readable for validation. The material supports real-time shadows; the final device shadow budget remains a profiling decision. Selection and health bars retain their existing visual conventions. Basic audio is short synthesized feedback with a mute toggle, without a full mixer/music/ambience production system.

Desktop phone/tablet-resolution captures do not prove physical touch comfort, visual accessibility, thermal stability or mobile FPS. Color-vision usability, subjective audio quality and a full match on target hardware remain acceptance work. Build/toolchain limitations and measured rendering performance belong to the Phase 8/10 reports. Extend the library only after reviewing the actual tactical-zoom slice and its device cost.
