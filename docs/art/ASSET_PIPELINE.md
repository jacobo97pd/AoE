# Asset pipeline

Status: the bounded Phase 9 native mesh pipeline is baked and PC-verified; the broader external-art production pipeline below remains guidance. Successful bake, test and packaged gallery evidence are recorded in the [Phase 9 report](../tasks/PHASE_9_REPORT.md). Physical mobile validation is still unverified. No optional modeling or subscription application is required to compile or play the prototype.

## Delivered native workflow

[ArtKitGeometry.cs](../../Assets/Game/Presentation/ArtKitGeometry.cs) is the editable source for eleven original unit, building and resource identities. [ArtKitBaker.cs](../../Assets/Game/Editor/ArtKitBaker.cs) generates 22 reusable LOD meshes, one shared material, an `ArtKitCatalog` and [ART_KIT_MANIFEST.json](../../Assets/Game/Resources/Art/ART_KIT_MANIFEST.json) under `Assets/Game/Resources/Art`. Run **Emberfield → Art → Bake original Aven slice** or the explicit editor entry point `Emberfield.Editor.ArtKitBaker.Bake`. The baker preserves existing asset identities and records the recipe hash, actual triangles/vertices, bounds and budgets; it performs no automatic import-time rewrite.

`ArtKit.CreateUnit`, `CreateBuilding` and `CreateResource` attach two shared meshes and a Unity LOD Group to a view. Building X/Z dimensions are scaled from a normalized footprint to the simulation definition. Each LOD has one renderer, uses the same material, and receives its team tint through a material property block. Vertex RGB contains the palette and alpha is the team mask. Unsupported entries return `null` for the existing presentation fallback. No gameplay value comes from mesh geometry or animation.

This kit needs no FBX exporter, image generation, painting application, rig importer or source texture atlas. [Original provenance](ORIGINAL_PROVENANCE.md) records the scope and source ownership. The small baked meshes remain readable for geometry validation; any later stripping/compression change must preserve validated geometry and be measured. Both `ArtKitTests` cases passed, checking finite geometry, topology/normals, declared dimensions and budgets, mask coverage, distinct role extents, shared assets and LOD configuration. Motion/feedback isolation also passed within the 347 EditMode + 66 PlayMode combined run. Both gallery resolutions verified 16 instances, 22 shared meshes and one kit material. Gallery and packaged evidence supplement these checks; they do not replace a device profile.

## Tools and ownership

Unity is the integration and runtime authority. Blender is a suitable optional source-modeling tool when available. Texture painting, rigging, animation and UI tools can vary with the artist's existing licensed workflow. Do not install large applications or acquire subscriptions for the greybox phase.

Each external asset needs source path, creator/license provenance, intended use, source revision and export settings. Reference PDF art is not a production source. Store editable source assets separately from runtime imports; keep the runtime repository lean and introduce Git LFS only with a concrete asset-size need and repository decision.

## Paths and naming

Current native runtime assets live in `Assets/Game/Resources/Art`, their shader in `Assets/Game/Resources/Shaders`, and editable geometry recipes in `Assets/Game/Presentation`. Future imported-production areas may use `Assets/Art/Models`, `Materials`, `Textures`, `Animations`, `VFX`; game definitions continue to reference stable data identity rather than importing a file name into game rules. Keep external source art in an explicitly chosen non-runtime authoring directory when that workflow begins.

Suggested files: `aven_tender_lod0.fbx`, `aven_tender_body_basecolor.png`, `aven_tender_body_mask.png`, `aven_tender_walk.anim`, `common_team_surface.mat`. Use lowercase ASCII and underscores consistently. Name clips by action, and include faction only when the asset actually differs. A renamed art file must not change an entity definition ID.

## Modeling and export standards

| Area | Production rule |
| --- | --- |
| Scale | Model in meters; confirm Unity import scale 1 with a reference human and known footprint. |
| Transforms | Apply intended object scale/rotation before export; no negative scales in the delivered hierarchy. |
| Orientation | Establish Unity-facing +Z and +Y-up in an import test, then reuse the verified exporter preset. |
| Pivot | Units at ground center; buildings at documented footprint anchor; projectiles at intended travel/impact origin. |
| Topology | Preserve silhouette; remove hidden unnecessary geometry; avoid duplicate coplanar faces. |
| UVs | One deliberate atlas allocation; enough margin for mip levels; no accidental overlaps in unique-lightmap UVs. |
| Rig | Minimal useful hierarchy; consistent bone names; budget skin influences and verify deformation on device. |
| Colliders | Use simple separate proxies; do not use detailed mesh collision for ordinary soldiers. |
| Export | Export only intended meshes/armatures/clips; exclude cameras, lights and authoring helpers; retain editable source. |

FBX is a conventional interchange option, not a requirement to add an exporter dependency now. Validate a single unit from source to Unity before mass export. Any conversion that changes handedness, scale or bone orientation must be captured in the preset rather than manually repaired per asset.

## Animation

Current native models use whole-model cosmetic motion driven by visible simulation state; there are no bones, skin weights or authored skeletal clips. For a later rigged kit, use generic rigs for stylized nonhuman or strongly proportioned assets and Humanoid only when its retargeting benefit is established. Prefer in-place locomotion driven by simulation presentation so root motion cannot change authoritative position. Bake only required channels and choose compression after comparing visible error.

Clips loop deliberately for idle, movement and work. Attack/death clips have clear nonlooping boundaries. Do not put resource credit, hit damage or unit spawning in animation events. Separate carried objects or role equipment may share rigs/materials when their silhouettes stay readable.

## Textures and materials

Start small: typical unit texture sets should explore shared 512–1024 atlases; building/terrain sheets may need 1024–2048 according to actual camera coverage. These are starting budgets, not mandatory per-asset allocations. Prefer sharing over assigning every unit a unique maximum-size texture.

Use sRGB for color data and linear import for masks; mark normal maps correctly. Team masks use a documented channel and must not be baked into duplicate blue/red/green/yellow base textures. Disable read/write on imported runtime textures/meshes unless a proven runtime operation requires it. Use mipmaps for world textures and deliberate settings for UI.

Choose platform compression only after quality checks on supported targets. Record Android/iOS override formats and alpha artifacts when device builds exist. Avoid uncompressed large runtime textures or transparent particle layers merely to hide import errors.

## Unity integration

1. Import one representative asset using reviewed settings and no unexpected scale conversion.
2. Assign shared materials and team-mask properties through the presentation layer.
3. Configure LOD Group transitions using normal camera distances; inspect popping and role readability.
4. Add simple selection proxies distinct from art collision and simulation occupancy.
5. Bind state/animation through a view component; gameplay stays in simulation.
6. Check missing references, material count, texture memory, animation cost and culling behavior.
7. Record on-device frame-time impact in a representative stress scene before batch integration.

Keep stable Unity `.meta` files in Git. Do not move assets outside Unity without preserving their metadata. Generated caches and build outputs remain ignored.

## Asset acceptance

Every integrated production asset has correct name/scale/pivot, reliable silhouette at play zoom, bounded material count, valid team coloring, no missing references, configured LOD where useful, provenance, and a measured effect on representative device performance. Review proxy-to-final substitution without changing costs, selection ownership, damage or navigation footprints accidentally.
