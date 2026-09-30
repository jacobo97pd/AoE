# Meshy art pipeline

In September 2026 the Meshy test barracks was approved, and the game's units and buildings are being redrawn in that
style. This page is how a model gets from a reference sheet into a match. The plan and the decisions behind it are
in the session notes of 24 September 2026.

## What the user decided

- The European factions (Franceses, Hispanos, Ingleses) share the kingdom set: the fleur-de-lis sheets.
- The desert sheets are **not** theirs. They went to the two desert factions, the Sultanato (`sultanate`) and the
  Confederación del Sahel (`sahel`), both fictional (docs/design/DESERT_FACTIONS.md).
- Humanoid creatures are done now: orcs, elves, dwarves.
- Four-legged beasts, mounts and the drake wait for a different tool. Meshy's animation library is biped only.
- Spending is checked in stages. When a batch would spend heavily, work stops and reports first.

## Units

```
python tools/art/meshy_unit.py <id> --max-credits 45     # paid: model 15 + rig 5 + 3 per clip
python tools/art/meshy_unit_finish.py <id> [<id> ...]     # free: Blender + textures
Unity -batchmode -executeMethod Emberfield.Editor.MeshyUnitBaker.Run -quit
```

`tools/art/meshy_units.json` lists each figure. Each entry gives:

- which sheet in the user's Downloads the figure comes from, and its crop box;
- the game unit and the factions that field it;
- a role, which picks the library clips;
- the height it stands in the game;
- `teamHues`, the cloth hues that take the owner's colour;
- optionally `rigidParts` (`{"bone": "Spine02", "minSize": 0.5, "clearBelow": 0.5}`), for a spear, bow or shield
  slung on the back that Meshy's auto-rig spread over the arms, neck and legs. Every separate piece of surface other
  than the body whose longest side reaches `minSize` of the height rides that bone rigidly, and `clearBelow` strips
  arm, neck and head weights from the coat hem below that share of the height. The three sahel bipeds carry it.

The paid script records each step in `meshy-manifest.json` before starting the next, so a rerun resumes instead of
paying twice. Meshy's own downloads stay outside the repository, in `D:/EmberfieldWorkingCache/meshy-units`. They are
about 25 MB a unit, because every file carries its own textured copy of the mesh.

The finish writes `Assets/Models/Units/<id>/`:

- `<id>.fbx`: the rigged mesh, with every clip as its own take. The walking and running clips have their forward
  drift removed, and the stride speed is measured from the foot so the feet do not skate.
- `<id>_BaseColor.png`: 1024 pixels, with the owner-colour mask in its alpha channel.
- `<id>_Normal.png`: 1024 pixels.
- `<id>_Mask.png`: 1024 pixels, metallic in red and smoothness in alpha.

The baker then turns these into a prefab under `Resources/MeshyUnits` and writes the catalogue. Every prefab carries
a `CorsairAnimationDriver`, with Idle, Walk, Run, Attack, Hit and Death states, plus Work or Aim where the role has
them.

### Ladder climb

```
python tools/art/meshy_climb.py [<id> ...] --max-credits 40   # paid: 3 credits a unit, on the rig it already has
python tools/art/meshy_unit_finish.py <id> [<id> ...]          # free: adds the Climb take
```

Only reedguard, stringwarden and frostguard may board a wall (`SiegeSystem`), so only their fifteen Meshy figures get
Meshy's `Ladder_Climb_Loop` (library action 438). With no ids the script picks them from the roster. The GLB lands in
the raw folder as `<id>_climb.glb`, and the manifest records a `climb` step.

The finish takes the climb's rise and its lean toward the ladder out of the hips, as it does a gait's travel, because
`WorldView` lifts the boarding soldier itself. The rise per second is reported as `climbMetresPerSecond`, about
0.25–0.46 m/s at Meshy's size, before the game's scale. The kingdom warrior's hands grip about half a metre ahead
of its hips. `MeshyUnitBaker` gives these controllers a looping Climb state; other units have none. How the game
uses it is under [Siege ladders](#siege-ladders).

A Meshy rig task expires three days after it was made. Past that, the unit has to be rigged again (5 credits) before
it can be given new clips.

## Mounts, riders and beasts

```
python tools/art/rig_quadruped.py <id> --generate    # paid: 15 credits for the model; the rig and clips are free
python tools/art/rig_quadruped.py <id>               # re-rig an existing model, free
```

Meshy rigs bipeds only. The API does list `animation_type: quadruped`, but on 2026-09-25 it refused both a horse and
a mounted knight ("Pose estimation failed"). So four-legged models are rigged here in Blender, from a template.

The script fits the template to the model:

- The four feet are clustered from the lowest slice of the mesh. The front pair is the one toward -Y, the way every
  Meshy model faces.
- The belly is where the cross-section jumps from four legs to one body. A roster `belly` (a fraction of the height)
  overrides this when wide paws fool it; the wolf needs 0.25.
- Hips, chest, neck, head and tail run along the body, and each leg gets thigh, shin and hoof.
- Each vertex takes its nearest two bone segments, and leg vertices may only take their own leg. A rider, a lance or a
  saddle therefore moves rigidly with the back, so rider and mount are one model.

The clips are keyed from the leg length: a diagonal walk, a paired gallop, an idle, a charge that dips onto the
forehand, a flinch and a fall onto the side. The FBX and the manifest match the bipeds', so `MeshyUnitBaker` imports
them unchanged. They go through the same `CorsairAnimationDriver` states.

Roster entries set `"rig": "quadruped"`, plus a prompt and a texture. The mounts are:

| Model | Unit | Factions |
|---|---|---|
| kingdom knight | strider | aven, serevin, english |
| kingdom ashrunner | ashrunner | serevin |
| mountain pony rider | strider | skeld |
| dwarf war-ram rider | strider | drakeforged |
| elf stag rider | strider | verdant |
| orc wolf rider | strider | ashen |
| sultanate camel rider | strider | sultanate |
| sultanate camel archer | camel_archer | sultanate |
| sahel horse rider | strider | sahel |
| sahel quilted lancer | quilted_lancer | sahel |
| ember drake | ember_drake | drakeforged |

The drake's wings are rigid; it walks on four legs.

### Real animal animation from a donor pack

```
python tools/art/transplant_rig.py <id>    # free: needs the donor pack on disk
```

The template rig above gives a readable walk and gallop, but a mount moves better with an animator's clips. The
Quaternius Animated Animal Pack (CC0 public domain) is unpacked outside the repository, at
`D:/EmberfieldAssets/Monturas/animal-pack`; `EMBERFIELD_MOUNTS` points elsewhere. It has twelve rigged animals, each
with walk, gallop, three idles, headbutt and kick, two hit reactions, death, eating and jumps.

A roster entry with `"donor": "Horse"` (or `Stag`, `Wolf`, `Bull`, `Donkey`...) gets that animal's skeleton and clips:

1. The Meshy model is warped onto the donor only long enough to copy the weights of the nearest point on the donor's
   surface. A scale per axis puts its feet on the donor's and its belly (the underside of the body, on the centre
   line, midway between the feet) at the donor's height. A thin-plate spline then pins each foot and the muzzle
   onto the donor's, so a foot set forward or a head held low still copies a leg's or a head's weights.
2. It then takes back its own shape, and the donor's skeleton moves onto it. Bone heads and tails go through the
   inverse warp, the tail swings onto the Meshy tail, and translation keys are scaled.
3. The mount therefore keeps its proportions and plays the donor's motion.
4. The rider and its gear ride the saddle bone rigidly. Meshy builds the animal as one piece of surface and the rider
   as others: a piece standing mostly above the back is the rider's, boots included; the animal's own antlers,
   horns or raised head stay the head's.
5. Corrections where the two shapes differ: the hooves ride the lower legs (the donor's hooves follow IK controls
   keyed for its own leg lengths, and would drift off the leg and into the ground). A lower leg keeps only what lies
   within a hoof's thickness of it. Ears ride the head. A pair of legs that Meshy fused into one block, or with one
   leg hidden in the other, moves as one leg.
6. Only the tail swings with the tail: what lies behind the tail's root and nearer a tail bone than the body's.
7. Idle, Walk and Run lift or lower the body until the planted hooves touch the ground.
8. Attack and Hit keep their motion, but nothing goes under the ground: frame by frame the tail, then the neck, turns
   up about its root just enough, and whatever is still under (a hoof in a headbutt) lifts the body, easing in and
   out over a few frames. The wolf's bite rears before it lunges and sank its tail 14% of its height into the ground;
   its tail now turns up by at most 36 degrees and the lunge is unchanged. Death keeps its fall.

`clips` remaps the donor's names to our states. The default is Idle, Walk, Gallop as Run, Attack_Headbutt as Attack,
Idle_HitReact1 as Hit, and Death; the wolf has one attack, `"clips": {"Attack": "Attack"}`. Other options:

| Option | Effect |
|---|---|
| `tailDamp` | The share of the tail's motion kept. 0.2–0.5 for long Meshy tails, 0 holds a stub still (ram, stag) |
| `tailMargin` | How far behind the tail's root, as a share of the feet's length, the tail starts to pull; 0.12 by default for a caparison over the rump, 0 for a bare tail |
| `saddle` | The animal's back as a share of the model's height, when the rider is not found above the donor's back (the dwarf's ram: 0.5) |
| `cloth` | A saddle cloth or fleece over the flanks: above the belly they ride the body, not the shoulders and hips |
| `fit` | `["muzzle", "tail"]` by default: which ends are fitted onto the donor's |
| `belly`, `donorBelly` | Override the measured belly heights, as a share of the height |
| `tailClear` | A tail hanging close behind the hocks drops the leg weights it copied and swings with the tail bones alone. The camels need it: their tails tore into shards at every stride (camel rider Walk stretched 102 -> 32, Run 167 -> 58) |
| `splitLegs` | `["Front"]` or `["Back"]`: split that pair by side instead of averaging it. For two separate legs whose weights came out on one side because the model lifts or bends a foot (the stag and the pony, front pair); never for a single column of leg (the wolf's front pair, the ashrunner's hind pair), which tears lengthwise |

Donors: the ashrunner rides the Horse, the mountain pony the Donkey, the dwarf's war ram the Bull, the elf's stag the
Stag, the orc's wolf the Wolf, the sultanate camel rider the Alpaca and its camel archer the Stag (its long thin legs fit that camel's better than the Alpaca's: stretched Walk 50 -> 22, Run 96 -> 39), and both Sahel riders the Horse. The drake keeps the template rig. Each run writes what it measured into the
manifest's `finish` (rider vertices, bellies, fused legs, tail fit, how far each clip was settled).

Meshy geometry the transplant cannot undo: the ashrunner's tail is welded to its hind legs, which stand in one
column, so at a gallop a strip between the tail and the hocks still stretches, and they keep stepping as one leg. The
wolf's two front legs are also one column and step together. The pony's hind hooves touch, and one hind hoof takes a
few of the other leg's weights, so it drags into a spike along the ground at the walk.

A fused pair is detected when many edges join the two lower legs or one side holds under 30% of the pair's lower-leg
vertices. The second test also fires on two separate legs when the model holds a foreleg lifted or bent (the stag,
the pony): that leg's lower weights are thinned away, and averaging then makes both forelegs step together. Such a
pair takes `splitLegs`: every vertex below the belly gives the pair's weights to its own side of the gap between the
legs (two means of x), fading back to the transferred weights at the chest. Measured on the stag and the pony, the
forelegs alternate like the hind legs (left/right correlation of the forward travel at the walk +0.82 and +0.93
before, -0.88 and -0.82 after) and stretched edges barely move (12 to 14 and 23 to 26 at the walk, 62 to 65 and 50 to 55
at the gallop). Split, the wolf's front column went from 14 to 158 and the ashrunner's hind column from 39 to 151.
The stag's and the pony's roster entries do not name `splitLegs` yet; their committed models still average the pair.

The kingdom knight was transplanted again with the spline, the measured belly and the corrections above (belly 0.257
of its height instead of 0.30; 833 rigid rider vertices instead of 285): edges stretched past 2.5 times their rest
length went from 153 to 38 at the walk and from 271 to 92 at the gallop, and its lowest point at the walk from -3.2%
of its height to -0.2%.

To bake only some units into Unity, leaving every other prefab, controller and catalogue entry as it is:

```
Unity -batchmode -executeMethod Emberfield.Editor.MeshyUnitBaker.RunUnitsFromCommandLine -meshyUnits kingdom_ashrunner,orc_rider -quit
```

## Roles with no sheet

Some roles have no sheet of their own: the kingdom archer, the orc worker and archer, the elf worker and the dwarf
crossbowman. For these, Meshy image-to-image draws one first:

```
python tools/art/meshy_concept.py <id>    # paid: 6 credits with nano-banana-2
```

The roster entry carries a `concept` block with the prompt and the units whose crops set the style. The picture lands
at `Assets/Models/Units/<id>/concept.png`, and the entry's `sheet` points there. Nothing is modelled until someone has
looked at the picture. After that, `meshy_unit.py` models it exactly as it models a figure cut from the user's sheets.

## Buildings

```
python tools/art/meshy_building.py <id> --max-credits 20   # paid: preview 5 + refine 10, then the free finish
```

`tools/art/meshy_buildings.json` holds one style per culture (palette, texture prompt, owner-colour hues) and one
prompt per building. Prompts lead with mass and silhouette and name few ornaments. The barracks' first prompt listed
every ornament and came back as an open pavilion. Meshy cuts prompts at 600 characters.

Seven building cultures share the file:

| Culture | Factions | Look | Owner colour |
|---|---|---|---|
| kingdom | aven, serevin, english | cream plaster and slate | royal blue cloth |
| mountain | skeld | turf-roofed timber longhouses | woad blue banners |
| dwarf | drakeforged | carved granite with bronze and runes | crimson banners |
| elf | verdant | pale stone and living wood with leaf roofs | royal blue banners |
| orc | ashen | crude logs, hides, black iron and bone | blood red rags |
| sultanate | sultanate | whitewashed mudbrick, sandstone, flat roofs, horseshoe arches | turquoise cloth (hue 155-200) |
| sahel | sahel | sun-dried mud plaster with toron beam ends, buttresses and thatch | indigo cloth (hue 185-252) |

Each culture has its own nine buildings. The serevin supply outpost is kingdom only.

### Walls and gates

Each culture also has a wall segment, a gate and a gate leaf: `<culture>_wall`, `<culture>_gate` and
`<culture>_gate_leaf`, 21 pieces of 250–1,300 triangles. Siege ladders, boarding and the opening gate depend on their
geometry, so they are built to the game's numbers:

| Piece | Size (x along the wall, depth, height) | What the game needs of it |
|---|---|---|
| wall | 3.0 × 0.9 × 3.7 m | walk at 3.0 m, faces 0.45 m off the centre line, ends cut flat so segments tile |
| gate | 2.0 × 0.9 × 3.7 m | the same walk and ends, and a clear passage 1.3 × 2.4 m open to the ground |
| leaf | 1.3 × 0.15 × 2.4 m | fills the passage |

Text-to-3D never gave a usable wall or gate in 25 tries: towers, blocks a cube deep, a door filling the passage. So
these entries name a `base` group instead of a prompt. `meshy_building.py` builds the culture's pieces in Blender at
their exact size, and one Meshy retexture (10 credits) paints the whole group from the group's texture prompt. Run the
script for each member; the first pays and the others reuse the painted maps. A group's `cloth` colour dyes the
banners and shields the builder made where Meshy painted them something else. The kingdom, mountain, elf and orc
leaves are ordinary text-to-3D pieces; the sultanate's and the sahel's, like the dwarf's, are built and painted with their group. The kingdom, mountain and orc leaves carry a texture prompt of their own,
because the style's prompt describes houses. The dwarf leaf is built and painted with its group.

```
python tools/art/meshy_building.py orc_wall --max-credits 10    # builds the orc group, pays its retexture, finishes the wall
python tools/art/meshy_building.py orc_gate                     # reuses the painted maps
```

These entries use `fit: stretch`: the finish scales each axis on its own to fill the box, and refuses a model that
would stretch more than 40% against its other axes. A leaf gives its `size` in metres and is `thin`, so its plate
thickness is left out of that check.

`MeshyUnitBaker.RunBuildingsOnly` (menu Emberfield/Art/Import Meshy buildings) bakes the buildings alone and leaves
every unit's prefab and controller as it is. The leaves enter the catalogue as building `gate_leaf`, which no
definition uses. An entry's `grade` overrides its style's. The orc wall and gate carry 1.8 against the style's 1.2:
Meshy painted the palisade darker than the orc houses, and from outside, in its own shadow, it read as a black band.

In the game, `MeshyBuildingVisuals` draws a wall or gate with its culture's model and fits it with no margin, one
scale per axis: the whole width along X, so neighbouring segments touch, and the full 3.7 m height, so the walk stays
at the simulation's 3 m where ladders reach and boarders stand. The depth scales with the length, so the model keeps
its own 0.9 m. A gate gets the owner culture's leaf as a direct child named `Portcullis`, centred in the passage with
its pivot on the ground and recoloured for the owner. WorldView opens it as it opens the procedural gate: it lifts
the Portcullis to 2.45 m and squashes it into the lintel. The half turn every Meshy building gets and the rise
during construction act on the gate and its leaf alike. Miraj, Solar and the pirates have no fortification entries
and keep the procedural wall and gate.

## Scenery: trees, rocks, resource nodes, landmarks and siege

```
python tools/art/meshy_prop.py <id> [...] --max-credits 60    # paid: preview 5 + refine 10, then the free finish
python tools/art/meshy_prop.py <id> --preview-only            # 5-credit probe of the shape before paying for texture
```

`tools/art/meshy_props.json` lists every prop by biome (forest, desert, caribbean, or `all` for siege). Each prop has
a role:

- **obstacle**: the fillers on blocked cells. There are thousands of them, so they get a tight triangle budget of
  200–500 and a far level at 40% of it that phones switch to early (see Levels of detail). `kind` names the
  procedural filler they replace.
- **resource**: a gatherable node, with two LODs.
- **landmark**: a map landmark.
- **siege**: the ram, ladder or tower. These are sized in authored units under the unit scale, and their cloth takes
  the owner's colour. A siege entry may list `factions`. The ladder has one per culture:

  | Model | Factions |
  |---|---|
  | kingdom ladder on a wheeled sled | aven, serevin, english, pirates |
  | mountain split-log ladder | skeld |
  | dwarf iron-banded ladder on a wheeled frame | drakeforged |
  | elf living-wood ladder | verdant |
  | orc spiked log ladder | ashen |
  | sultanate palm-trunk ladder on a sled | sultanate |
  | sahel palm-trunk ladder on a log skid | sahel |

  `MeshyPropVisuals.TrySiege` picks the entry that lists the owner's faction. Without one, it takes an entry that lists
  no factions, then the kingdom's. The ram, tower and cart list none, so every faction shares them. The Miraj and Solar
  armies, which have no ladder of their own, climb the kingdom's.

The finish writes the model's size into its vertices, in metres, standing on y = 0. Two reasons:

- The game scales each obstacle unevenly per cell.
- The felling sway bends by object-space height.

With `fit: box`, height and footprint are fitted separately. A broad crown on a one-metre cell then grows taller and
narrower instead of shrinking or spilling onto walkable ground.

`MeshyPropBaker` bakes each level of detail into a mesh asset and builds prefabs with no colliders. Scenery uses the
`Emberfield/Meshy Prop` shader. It is GPU-instanced (no per-material CBUFFER, by design), and it carries the felling
`_Sway` as an instanced property, so only the tree being cut bends. `MeshyPropVisuals` hands obstacles back as shared
meshes (near and far) and a shared material, so copies instance. Resources, landmarks and siege come back as prefabs.

Until 2026-09-25 every filler FBX held a single mesh, which Unity puts on the file's root. The baker measured the mesh
against that root, so it dropped the root's axis turn and baked the trees, palms and rocks lying on their sides: a
3 m tree stood 1.7 m tall with its footprint turned up. With a far level in the file the mesh has its own node again,
and the baker now also applies the root's turn when a file holds one mesh
([before and after](../testing/evidence/scenery-fillers-upright-1280x720.jpg)).

Bridges, piers, dunes, the windmill and the grass, reed and dry-grass scatter stay procedural.

## Siege ladders

Each culture climbs its own ladder (the table above), and its wall-boarding infantry carry Meshy's climb clip
([Ladder climb](#ladder-climb)). `SiegeLadderVisuals` puts the two together in a match. It is presentation only: the
simulation (`SiegeSystem`) still decides when a ladder is equipment and when a soldier reaches the deck.

- **Raise.** A ladder that is idle within the simulation's 1300 mm of a visible enemy wall swings up against it over
  0.8 s of game time. The model turns about the front edge of its base, so nothing sinks into the ground. It also
  stretches until its top stands half a metre above the wall walk: 3.8 authored units under the 0.6 unit scale is
  2.3 m, and a wall is 3 m. The lean and the line of the rails are measured once from each model's own vertices, so
  any ladder model works. It lowers again when it moves off or the wall goes.
- **Climb.** A soldier boarding that wall walks to the ladder's foot and goes up its rungs, laid along them and
  facing the wall. Where its model has Climb, the clip plays at a phase tied to the height gained. The soldier then
  steps onto the deck slot the simulation is about to give it. Soldiers sent up one ladder together climb one behind
  the other. The owner ring and the impact disc, both drawn flat at the feet, stay hidden from the ladder's foot until
  the soldier stands on the deck.
- **Straight lift.** Towers, a player's own walls and a ladder out of sight keep the old lift from the ground.

`tools/Record-SiegeShowcase.ps1` films it: the Drakeforged assault on the Ashen wall. The east ladder is followed in
close from the north-west as it pulls up and swings against the wall, then its two climbers go up, then a wide shot
from outside shows the rest.

Known limits:

- The ladders are single rigid meshes. Raised, they stretch 1.4–1.6 times along their height, so the rungs space
  out.
- The climb clip plays about twice its authored speed, because the simulation fixes a climb at 100 ticks. The feet
  slide a little on the rails.
- Online, boarding progress is sent for a player's own units only, so enemy climbers keep the straight lift. Their
  ladders still swing up.

## Ground and water

```
python tools/art/meshy_ground.py forest desert caribbean      # paid: 6 credits a layer with nano-banana-2
python tools/art/make_water_maps.py                           # free
```

**Ground.** Each biome has four painted layers (`tools/art/meshy_ground.py`), in the style of the user's own ground
strip from the Crowns & Horizons poster. The script then does three things:

- It makes each layer seamless by blending half-offset copies.
- It pulls each layer toward the mean colour in the roster.
- It packs them 2×2 into `Resources/Art/Ground/<biome>_layers.png`.

`MeshyGroundImport` imports that file as a four-slice Texture2DArray. `AmberBiome` builds the terrain as an indexed
one-metre grid and writes each corner's weights for layers 1–3:

| Biome | Layer 1 | Layer 2 | Layer 3 |
|---|---|---|---|
| Forest | forest floor under the tree masses | path | river mud |
| Desert | hardpan path | oasis grass | gravel near rock |
| Caribbean | beach sand | wet sand | path |

Layer 0 takes whatever weight is left. The `Emberfield/Meshy Ground` shader blends the layers by height. It keeps the
procedural terrain's brightness, so the lighting grade still holds, and pulls saturation back so the ground sits under
the units.

**Water.** `Emberfield/Meshy Water` keeps the existing sheet and its shore depth. It adds two ripple layers, sky
reflection, sun glint, a wet bank and foam lace that rolls toward the shore, with colours per biome
(`AlphaEnvironment.WaterMaterial`). The flat Alpha water and AmberSurface stay as fallbacks.

## Levels of detail

The battlefield camera is orthographic, so how large a model is on screen depends on the zoom alone: a model fills its
size over twice the orthographic size of the screen (5 m zoomed right in, 8 m at the play zoom, 22 m at the widest).
Unity's LOD groups compare that share, multiplied by the quality level's LOD bias, with each level's threshold. A
desktop (bias 2) keeps every detailed level at every zoom the game allows; the phone tiers switch sooner (see the
Mobile tier section of [PERFORMANCE_BUDGET.md](../technical/PERFORMANCE_BUDGET.md)).

Because the share depends on the zoom alone, every copy of a model switches at the same zoom. The fillers and the
gatherable nodes use that instead of a LOD group: on a phone tier each keeps one renderer, and `SceneryDetail` hands
every copy of a model its far mesh at once when the zoom crosses the model's switch, and the near one back once the
zoom is 10% inside it again (`MobileTiers.SceneryHysteresis`, so a pinch resting on a switch never flickers). Nothing
runs until the zoom changes, and the swap allocates nothing. Both meshes share the instanced Meshy Prop material, so
the copies keep batching, and the renderer keeps its property block, so a tree being felled sways at either level.

| Art | Far level | Made by | Switch |
|---|---|---|---|
| Rocks and palms on blocked cells | 40%: rocks 400 → 160, palms 500 → 199 | `meshy_prop.py` finish (`lod1`), collapse of the merged source | on a phone tier `SceneryDetail` swaps each filler's one mesh past the zoom where the model fills the tier's share of the screen, measured at the model's size so every copy switches at one zoom (`AlphaEnvironment.CreateObstacle` registers it). A 1 m rock is light at every zoom on low and mid, past 6.3 m on high; a palm past 13.8 m on low and mid. A desktop never swaps: a LOD group per filler cost it 0.4 ms a frame |
| Broadleaf and pine trees on blocked cells | 40%: 400 → 160, a closed shell | the same with `farShell` | the same: past 12.4–13.7 m on low and mid, 19–21 m on high |
| Berry bushes, forest and caribbean ore | 300 and 250, a closed shell | `farShell` on the resource entry | a desktop keeps the prefab's 0.06; on a phone the node's group gives way to one renderer that swaps at the zoom where the tier's 3 m trees do, whatever the node's size (measured at its own size, a 1 m bush lost its berries at every zoom on mid) |
| Other resource nodes | 23–40%, unchanged | collapse | the same as the berry bushes |
| Landmarks, siege | 23–40%, unchanged | collapse | the prefab's 0.06 |
| Culture buildings (46) | 40% | `tools/art/meshy_lod.py`, collapse of the game's own FBX | the prefab's 0.08; never culled |
| Walls, gates, gate leaves | none: 250–1,300 triangles already | – | – |
| Units (26) | Unity Mesh LOD levels in the unit's own mesh: about 51%, 29% and 15% | `ModelImporter.generateMeshLods` in `MeshyUnitBaker` | forced from the zoom by `UnitMeshDetail` on a phone tier; never on a desktop |

**Why a shell.** The forest trees and berry bushes are a hundred or more loose shards of three to eight triangles
each. Collapse cannot take a shard below itself, so a 40% far level flattened them into dark slivers and the crowns
drew as shreds with holes. A roster entry with `"farShell": true` gets a far level that is a new closed skin around
the model instead: Blender thickens every shard (solidify), voxel-remeshes the result at 4% of the model's height,
collapses it to `lod1`, and copies the source's texture coordinates and normals onto it from the nearest face. The
crown keeps its outline and colours without holes; up close it reads as rounded lobes, which is why every tier keeps
the detailed trees at the play zoom (low drew the shells there until 2026-09-25 and its forest read as blobs). Palms
(22–45 parts) and rocks collapse cleanly and keep the plain decimation. `meshy_prop.py --finish-only <ids>` remakes both levels for free; the near level comes out identical.

**Buildings.** `python tools/art/meshy_lod.py [<id> ...] [--ratio 0.4]` reads each culture building's game FBX, welds
the vertices the export split along UV seams (collapsing an unwelded mesh opens cracks), collapses it to 40%, keeps
every vertex inside the near level's box (a collapse can push a merged vertex out: the kingdom siege workshop's went
0.28 m into the ground), copies the near level's normals back and writes `<id>_LOD1.fbx` beside it. `MeshyUnitBaker`
imports it with the near level's settings, hangs it under the building as `LOD1` and gives the prefab a LOD group; it
refuses a far level whose box is more than 5% off the near one's. `MeshyBuildingVisuals` fits and grounds a building
by its near level only.

**Units.** Unity 6000.3's Mesh LOD keeps the levels in the mesh itself and works on skinned meshes: the Animator, the
bones and `CorsairAnimationDriver` are untouched and the animation plays at every level (measured). Its automatic
selection, though, never leaves the first level under an orthographic camera (the triangles did not change from zoom 5
to 22 at thresholds 1–4), and the GPU-instanced Meshy Prop shader ignores it even when a level is forced, which is why
the scenery swaps whole meshes. A LOD group for a unit would need a second skinned renderer in every unit prefab and a
rebake of the prefabs and controllers. So the importer generates the levels (`MeshyUnitBaker.MeshLevels`;
**Emberfield/Art/Generate Meshy unit mesh levels** applies them alone and changes only the FBX .meta files), and on a
phone tier `UnitMeshDetail` sets `Renderer.forceMeshLod` from the zoom whenever it changes: one lighter level for each
halving of the unit's share of the screen below the tier's `UnitDetailHeight`. Skinning still runs on every vertex;
only the drawn triangles drop.

`-emberfieldSceneryStills <folder> [map]` (development players) opens the offline match on a map with the whole map
explored and writes stills of the thickest stand of blocked cells at the widest, play and closest zooms, the closest
again with every LOD group, filler and node held at its far level, the review's town on amber_crossing as drawn and
held at its far level, and each still's render statistics and the time the zoom's mesh swap took (`stills.json`). Add
`-emberfieldMobileTier low` to see a tier.

## In the game

- `MeshyUnitVisuals` and `MeshyBuildingVisuals` are presentation only. They draw the catalogue model for a unit or
  building when its owner's faction is listed on it; anything else keeps its current art.
- Owner colour comes from the Meshy Unit shader (`Resources/Shaders/MeshyUnit.shader`). Where the base map's alpha
  marks cloth, the painted colour is replaced by the owner's, and the painted shading is kept. There is one material
  per model and owner, so a hundred spears still cost one material.
- `-emberfieldProceduralUnits` turns every Meshy model off, to compare with the art it replaces. This covers units,
  buildings, scenery, ground and water.
- `-emberfieldMeshyReview <folder> [faction]` lines both armies up on amber_crossing. It writes close, medium and
  play-distance stills, and one still of the fight.

## Budget

| Piece | Triangles | Textures | Credits per try |
|---|---|---|---|
| Unit | 4,000 target, 6,000 at most | 1024, three maps | about 32–35 |
| Building | 5,000 target, 10,000 at most / LOD1 40% | 1024, three maps | 15 |
| Wall, gate (built in Blender) | 3,000 at most; 500–1,300 as built | 1024, three maps | 10 per culture, one retexture |
| Gate leaf | 800 at most | 1024, three maps | 15 (the dwarf's comes with its group) |
| Obstacle filler | 200–500 / LOD1 40% | 512, three maps | 15 |
| Resource node | 1,000–1,500 / LOD1 250–350 | 512, three maps | 15 |
| Landmark, siege | 3,500 / LOD1 800–900 | 1024, three maps | 15 |
| Ground layer | – | 1024 slice of a 2×2 array | 6 |
