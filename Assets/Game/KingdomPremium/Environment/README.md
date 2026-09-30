# Kingdom premium courtyard source

This is actual static FBX geometry for the standalone Kingdom art review: a bespoke two-storey gatehouse, a paved forecourt, garden masonry, dressed openings, individual roof slates, cloth banners, lanterns, and scanned/modelled vegetation and props. It does not integrate or alter gameplay statistics.

The editable authoring script is `tools/art/kingdom_environment.py`. Raw downloads, Blender files, logs and native preview remain under `D:/CodexTooling/kingdom-premium/environment`; the `.blend` is intentionally outside Unity's asset directory. From a clean source cache, run `kingdom_environment_assets.py` with Python, then `kingdom_environment_probe.py`, `kingdom_environment_tree.py` and `kingdom_environment.py` with Blender. Finish with `kingdom_environment_transition.py -- --from-base` in Blender, then copy the FBX, manifest and three added trail JPEGs from the D: `staged` directory. The final editable scene is `kingdom_environment_final.blend`. The separate `refine`, `stage` and comparison scripts record the visual iteration and are not needed for this rebuild sequence.

`kingdom_environment_textures.py` runs from the main authoring script. It retains the original 2K architectural / 1K prop resolution and bounds JPEG transfer size. The secondary path surface is downsampled to 1K. Alpha maps remain lossless PNGs. The delivered source texture set is 52,389,227 bytes, below the 55 MiB transfer budget.

`material-manifest.json` is the importer contract. Names match the exact FBX material names, paths are relative to this directory, and material `color` values are linear Blender values. Texture base colours use sRGB; normals, roughness, metallic and opacity are data maps. Foliage uses textured leaf/frond/blade geometry with alpha testing; it is not a rendered image of a tree or environment. The limewash uses a solid albedo over a scanned normal/roughness surface. The source's heavy black staining is intentionally omitted.

The facade faces Blender -Y, the building is centred on Blender Y=6, and ground height is Blender Z=0. FBX exports with forward -Z and up Y in metres. The desired Unity arrangement is a facade toward -Z and characters at (-2.2,0,-3), (0,0,-3), (2.4,0,-3). The root agent's Unity import confirmed inverted depth; place the environment root at yaw 180 degrees as specified in the manifest.

All downloaded assets and surface maps are [Poly Haven CC0 assets](https://polyhaven.com/license). Source asset URLs and author names are preserved individually in the manifest. Public download URLs and source metadata are retained in the D: download index. The building, ground topology, layout, roof tile geometry, banners, architectural joinery and assembled composition are authored by the Python script.

The manifest's `meshSources` records each of the seven source FBX assets with its author, CC0 license and direct download URL. `textureSources` does the same for every one of the 54 delivered texture files, including the three renamed/downsampled path maps and their original mud-surface source. The procedural building and composition are distinguished from these third-party source meshes.

To reproduce from the repository root in PowerShell, with Blender and Python/Pillow installed:

```powershell
$kingdomBlender = 'D:/CodexTooling/blender-portable/blender-4.5.13-windows-x64/blender.exe'
$kingdomCache = 'D:/CodexTooling/kingdom-premium/environment'
$kingdomAssets = 'Assets/Game/KingdomPremium/Environment'
$env:TEMP = "$kingdomCache/temp"
$env:TMP = $env:TEMP
New-Item -ItemType Directory -Force -Path $env:TEMP | Out-Null
python tools/art/kingdom_environment_assets.py
& $kingdomBlender --background --factory-startup --python tools/art/kingdom_environment_probe.py
& $kingdomBlender --background --factory-startup --python tools/art/kingdom_environment_tree.py
& $kingdomBlender --background --factory-startup --python tools/art/kingdom_environment.py
& $kingdomBlender --background --factory-startup --python tools/art/kingdom_environment_transition.py -- --from-base
Copy-Item -LiteralPath "$kingdomCache/staged/Environment.fbx" -Destination "$kingdomAssets/Environment.fbx" -Force
Copy-Item -LiteralPath "$kingdomCache/staged/material-manifest.json" -Destination "$kingdomAssets/material-manifest.json" -Force
Get-ChildItem -LiteralPath "$kingdomCache/staged/Textures" -Filter '*.jpg' | Copy-Item -Destination "$kingdomAssets/Textures" -Force
& $kingdomBlender --background --factory-startup --python tools/art/kingdom_environment_validate.py
```

`kingdom_environment_validate.py` reimports the FBX in Blender and emits `fbx-validation.json`, checking material slots, referenced files and the 350K triangle limit per tree. The final tree has 349,999 triangles, reduced from 2,062,487. A native comparison against the untouched source confirmed retained crown coverage; the earlier 110K and planar-dissolve trials were visually rejected. The environment has 36 unique meshes, 961 mesh objects and 5,590,279 triangles when all instances are counted.

This is a data-transfer check. It does not establish Unity visual parity, performance on a device, finished production LODs, or final user acceptance. Inspect leaf silhouettes and shadow quality in the actual Unity player. The continuous terrain and worn approach extend beyond the paved courtyard, and all foreground dressing preserves the three character reservations.
