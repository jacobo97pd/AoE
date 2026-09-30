"""Portable PBR surfaces for the single Royal Soldier study.

Run with normal Python (NumPy + Pillow) to generate the maps/manifest, or import
in Blender and call register_materials(). Textures are original mathematical
surfaces, not photographs, concept art or replacement character renders.

Color tint is sRGB. Base maps are sRGB neutral multipliers. Normal maps are
tangent-space +Y. Packed maps are linear: R=metallic, A=absolute smoothness.
Unity must set its smoothness multiplier to 1. All Blender shaders use exactly
the exported maps, the same UV0 and the same tiling; no Blender-only pointiness,
procedural noise, or displacement is used for the final appearance.
"""
from pathlib import Path
import argparse
import json
import math

WORK = Path('D:/CodexTooling/royal-soldier')
TEXTURES = WORK / 'Textures'
UNITY_TEXTURES = 'Assets/Game/RoyalSoldier/Textures'
VERSION = 2

# Tint sRGB, metallic, perceptual roughness, surface family, UV tiling.
# Large folds, seams, embossed decoration and plate borders must be geometry.
DEFINITIONS = {
    'RS_Steel':       ((.57, .63, .68), .92, .52, 'steel',   (2.0, 2.0)),
    'RS_SteelDark':   ((.255, .29, .33), .90, .55, 'steel',  (2.0, 2.0)),
    'RS_SteelEdge':   ((.61, .67, .71), .92, .48, 'steel',   (2.0, 2.0)),
    'RS_Gold':        ((.71, .52, .265), .88, .39, 'gold',   (2.0, 2.0)),
    'RS_GoldEdge':    ((.75, .56, .30), .88, .37, 'gold',    (2.0, 2.0)),
    'RS_BlueCloth':   ((.105, .235, .425), 0, .86, 'cloth', (3.0, 3.0)),
    'RS_IvoryCloth':  ((.56, .57, .545), 0, .91, 'cloth',   (3.0, 3.0)),
    'RS_Leather':     ((.31, .185, .10), 0, .64, 'leather', (2.5, 2.5)),
    'RS_LeatherLight':((.405, .26, .145), 0, .67, 'leather',(2.5, 2.5)),
    'RS_Skin':        ((.69, .465, .345), 0, .64, 'skin',   (5.0, 5.0)),
    'RS_Lips':        ((.55, .29, .25), 0, .61, 'skin',     (5.0, 5.0)),
    'RS_Hair':        ((.225, .14, .085), 0, .86, 'hair',   (2.0, 2.0)),
    'RS_HairDetail':  ((.28, .18, .11), 0, .86, 'hair',     (2.0, 2.0)),
    'RS_EyeIvory':    ((.65, .62, .55), 0, .32, 'plain',    (1.0, 1.0)),
    'RS_Iris':        ((.15, .095, .038), 0, .34, 'plain',  (1.0, 1.0)),
    'RS_Pupil':       ((.006, .008, .009), 0, .22, 'plain', (1.0, 1.0)),
    'RS_Wood':        ((.335, .23, .12), 0, .79, 'wood',    (2.0, 2.0)),
    'RS_ThreadGold':  ((.66, .46, .19), .22, .68, 'cloth',  (2.0, 2.0)),
    'RS_ThreadIvory': ((.62, .57, .46), 0, .88, 'cloth',    (2.0, 2.0)),
    'RS_Sole':        ((.085, .071, .055), 0, .89, 'leather',(2.5, 2.5)),
    'RS_ShieldBlue':  ((.095, .215, .385), 0, .48, 'paint', (1.5, 1.5)),
}


def srgb_to_linear(c):
    return c / 12.92 if c <= .04045 else ((c + .055) / 1.055) ** 2.4


def _arrays(size, seed=82763):
    """Periodic noise on a torus: filtering across the texture edge is valid."""
    import numpy as np
    rng = np.random.default_rng(seed)
    y, x = np.mgrid[0:size, 0:size].astype(np.float64) / size

    def noise(grid):
        values = rng.uniform(-1, 1, (grid, grid))
        px, py = x * grid, y * grid
        ix, iy = px.astype(int), py.astype(int)
        fx, fy = px - ix, py - iy
        fx = fx * fx * fx * (fx * (fx * 6 - 15) + 10)
        fy = fy * fy * fy * (fy * (fy * 6 - 15) + 10)
        a = values[iy % grid, ix % grid] * (1 - fx) + values[iy % grid, (ix + 1) % grid] * fx
        b = values[(iy + 1) % grid, ix % grid] * (1 - fx) + values[(iy + 1) % grid, (ix + 1) % grid] * fx
        return a * (1 - fy) + b * fy

    return np, x, y, noise


def _surface(family, size):
    np, x, y, noise = _arrays(size)
    broad, medium, fine = noise(8), noise(31), noise(113)
    tau = 2 * math.pi
    # Low-frequency color changes stay subtle. A typical tile is only a few
    # centimetres on the object. Large light/dark bands would read as damage.
    if family in ('steel', 'gold'):
        brush = np.sin(tau * (91 * x + .055 * np.sin(tau * y * 5)))
        height = .58 * fine + .12 * medium + .08 * brush
        value = .93 + .018 * medium + .009 * broad + .005 * brush
        rough = .012 * medium + .006 * fine
        strength = .035 if family == 'steel' else .025
    elif family == 'cloth':
        # Alternating warp/weft lobes: only the surface weave, not fake folds.
        warp, weft = np.cos(tau * 40 * x), np.cos(tau * 40 * y)
        over = np.sin(tau * 20 * x) * np.sin(tau * 20 * y)
        height = .35 * warp + .35 * weft + .22 * over + .04 * fine
        value = .93 + .007 * warp + .007 * weft + .014 * medium
        rough = .015 * medium + .01 * fine
        strength = .075
    elif family == 'leather':
        pores = np.maximum(fine - .27, 0) ** .7
        height = .50 * medium + .22 * fine - .42 * pores
        value = .93 + .026 * medium + .012 * fine + .012 * broad
        rough = .035 * medium + .015 * fine
        strength = .075
    elif family == 'skin':
        pores = np.maximum(fine - .24, 0)
        height = .1 * medium - .6 * pores + .12 * fine
        value = .965 + .006 * medium + .005 * fine
        rough = .018 * medium + .013 * fine
        strength = .026
    elif family == 'hair':
        strands = np.cos(tau * (73 * x + .15 * np.sin(tau * y * 2)))
        height = .5 * strands + .1 * fine
        value = .93 + .021 * strands + .016 * medium
        rough = .02 * medium
        strength = .07
    elif family == 'wood':
        grain = np.cos(tau * (18 * x + .12 * np.sin(tau * 2 * y) + .025 * medium))
        height = .35 * grain + .2 * fine + .14 * medium
        value = .93 + .024 * grain + .02 * medium
        rough = .03 * medium + .018 * grain
        strength = .075
    elif family == 'paint':
        height = .35 * medium + .1 * fine
        value = .94 + .017 * medium + .008 * broad
        rough = .025 * medium + .014 * fine
        strength = .035
    else:
        height, rough = np.zeros_like(x), np.zeros_like(x)
        value, strength = np.ones_like(x), 0

    # Tangent-space derivatives, bounded explicitly to prevent pebbled metal.
    dx = (np.roll(height, -1, axis=1) - np.roll(height, 1, axis=1)) * .5
    dy = (np.roll(height, -1, axis=0) - np.roll(height, 1, axis=0)) * .5
    max_slope = max(float(np.max(np.abs(dx))), float(np.max(np.abs(dy))), .0001)
    dx, dy = dx / max_slope * strength, dy / max_slope * strength
    nz = np.ones_like(x)
    normal = np.stack((-dx, -dy, nz), axis=-1)
    normal /= np.linalg.norm(normal, axis=-1, keepdims=True)
    return np.clip(value, 0, 1), rough, normal


def generate_textures(output_dir=TEXTURES, size=512):
    """Generate reusable base/normal maps and per-material response maps."""
    import numpy as np
    from PIL import Image
    out = Path(output_dir)
    out.mkdir(parents=True, exist_ok=True)
    family_data = {}
    for family in sorted({d[3] for d in DEFINITIONS.values()}):
        value, rough_variation, normal = _surface(family, size)
        rgb = np.repeat(value[:, :, None], 3, axis=-1)
        Image.fromarray(np.rint(rgb * 255).astype(np.uint8)).save(out / f'{family}_BaseColor.png')
        Image.fromarray(np.rint((normal * .5 + .5) * 255).astype(np.uint8)).save(out / f'{family}_Normal.png')
        family_data[family] = rough_variation
    records = []
    for name, (tint, metal, roughness, family, tiling) in DEFINITIONS.items():
        rough = np.clip(roughness + family_data[family], .06, .97)
        packed = np.zeros((size, size, 4), dtype=np.uint8)
        packed[:, :, 0] = round(metal * 255)
        packed[:, :, 1] = 255  # unused, deliberately not interpreted as AO
        packed[:, :, 3] = np.rint((1 - rough) * 255).astype(np.uint8)
        Image.fromarray(packed).save(out / f'{name}_MetalSmooth.png')
        Image.fromarray(np.rint(rough * 255).astype(np.uint8)).save(out / f'{name}_Roughness.png')
        records.append({
            'name': name, 'tint': list(tint) + [1], 'colorSpace': 'sRGB',
            'baseColor': list(tint) + [1], 'textureMultipliesBaseColor': True,
            'metallic': metal, 'roughness': roughness, 'smoothness': 1 - roughness,
            'smoothnessMapMode': 'absolute', 'smoothnessMultiplier': 1.0,
            'normalConvention': 'tangent +Y', 'normalScale': 1.0,
            'tiling': list(tiling), 'vertexColorStrength': 1.0 if name in ('RS_Skin', 'RS_Lips') else 0.0,
            'baseColorTexture': f'{UNITY_TEXTURES}/{family}_BaseColor.png',
            'normalTexture': f'{UNITY_TEXTURES}/{family}_Normal.png',
            'metallicSmoothnessTexture': f'{UNITY_TEXTURES}/{name}_MetalSmooth.png',
            'roughnessTexture': f'{UNITY_TEXTURES}/{name}_Roughness.png',
            'sourceDirectory': str(out), 'family': family,
        })
    manifest = {
        'schemaVersion': VERSION, 'asset': 'Royal Soldier material set',
        'createdBy': 'tools/art/royal_soldier_materials.py',
        'source': 'Original deterministic mathematical textures. No external image source.',
        'resolution': [size, size], 'materials': records,
        'packing': {'R': 'metallic', 'G': 'unused', 'B': 'unused', 'A': 'absolute smoothness'},
        'edgeWear': 'RS_SteelEdge and RS_GoldEdge are optional assignments to real exposed bevels. No curvature is assumed or faked by a tile.',
        'limits': 'Microdetail changes surface response only. Silhouette, plate thickness, cloth folds, seams and emblems require authored geometry.',
        'blenderOnlyPreviewDifferences': 'Skin uses mild subsurface scattering and cloth uses 0.10 sheen; Unity shader must implement these separately or omit them. Shared texture, tint and vertex color inputs otherwise match.',
    }
    (out.parent / 'material-manifest.json').write_text(json.dumps(manifest, indent=2), encoding='utf-8')
    return manifest


def register_materials(output_dir=TEXTURES):
    """Create Blender materials from already generated, Unity-compatible maps.

    Generate maps once with normal Python before launching Blender: Blender's
    bundled Python is not required to have Pillow installed.
    """
    import bpy
    out = Path(output_dir)
    if not (out / 'steel_BaseColor.png').exists():
        raise FileNotFoundError(f'Generate textures with Python before Blender: {out}')
    result = {}
    for name, (tint, metallic, roughness, family, tiling) in DEFINITIONS.items():
        mat = bpy.data.materials.get(name) or bpy.data.materials.new(name)
        mat.use_nodes = True
        mat.diffuse_color = tuple(srgb_to_linear(c) for c in tint) + (1,)
        nt = mat.node_tree
        nt.nodes.clear()
        bs = nt.nodes.new('ShaderNodeBsdfPrincipled')
        output = nt.nodes.new('ShaderNodeOutputMaterial')
        nt.links.new(bs.outputs['BSDF'], output.inputs['Surface'])
        bs.inputs['Metallic'].default_value = metallic
        bs.inputs['Roughness'].default_value = roughness
        if name in ('RS_Skin', 'RS_Lips'):
            bs.inputs['Subsurface Weight'].default_value = .035
            bs.inputs['Subsurface Radius'].default_value = (.8, .36, .18)
            bs.inputs['Subsurface Scale'].default_value = .012
        if family == 'cloth':
            bs.inputs['Sheen Weight'].default_value = .10
            bs.inputs['Sheen Roughness'].default_value = .8
        uv = nt.nodes.new('ShaderNodeUVMap'); uv.uv_map = 'UVMap'
        scale = nt.nodes.new('ShaderNodeVectorMath'); scale.operation = 'MULTIPLY'
        scale.inputs[1].default_value = (*tiling, 1)
        nt.links.new(uv.outputs['UV'], scale.inputs[0])

        def image_node(filename, color_space):
            node = nt.nodes.new('ShaderNodeTexImage')
            node.image = bpy.data.images.load(str(out / filename), check_existing=True)
            node.image.colorspace_settings.name = color_space
            node.extension = 'REPEAT'; node.interpolation = 'Linear'
            nt.links.new(scale.outputs['Vector'], node.inputs['Vector'])
            return node

        base = image_node(f'{family}_BaseColor.png', 'sRGB')
        mix = nt.nodes.new('ShaderNodeMixRGB'); mix.blend_type = 'MULTIPLY'
        mix.inputs[0].default_value = 1
        mix.inputs[1].default_value = mat.diffuse_color
        nt.links.new(base.outputs['Color'], mix.inputs[2])
        if name in ('RS_Skin', 'RS_Lips'):
            attribute = nt.nodes.new('ShaderNodeVertexColor'); attribute.layer_name = 'Color'
            skin = nt.nodes.new('ShaderNodeMixRGB'); skin.blend_type = 'MULTIPLY'; skin.inputs[0].default_value = 1
            nt.links.new(mix.outputs['Color'], skin.inputs[1]); nt.links.new(attribute.outputs['Color'], skin.inputs[2])
            nt.links.new(skin.outputs['Color'], bs.inputs['Base Color'])
        else:
            nt.links.new(mix.outputs['Color'], bs.inputs['Base Color'])
        normal_image = image_node(f'{family}_Normal.png', 'Non-Color')
        normal = nt.nodes.new('ShaderNodeNormalMap')
        normal.space = 'TANGENT'; normal.uv_map = 'UVMap'; normal.inputs['Strength'].default_value = 1
        nt.links.new(normal_image.outputs['Color'], normal.inputs['Color'])
        nt.links.new(normal.outputs['Normal'], bs.inputs['Normal'])
        rough = image_node(f'{name}_Roughness.png', 'Non-Color')
        nt.links.new(rough.outputs['Color'], bs.inputs['Roughness'])
        mat['royal_soldier_surface_version'] = VERSION
        mat['unity_tint_srgb'] = list(tint)
        mat['unity_metallic'] = metallic
        mat['unity_smoothness'] = 1 - roughness
        result[name] = mat
    return result


def material(name):
    import bpy
    if name not in DEFINITIONS:
        raise KeyError(f'Unknown royal soldier material: {name}')
    found = bpy.data.materials.get(name)
    if found is None:
        return register_materials()[name]
    return found


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output', type=Path, default=TEXTURES)
    parser.add_argument('--size', type=int, default=512)
    args = parser.parse_args()
    manifest = generate_textures(args.output, args.size)
    print(f'Generated {len(manifest["materials"])} surfaces at {args.output}; manifest={args.output.parent / "material-manifest.json"}')
