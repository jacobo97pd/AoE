"""Turn a Meshy GLB into an FBX plus PNG maps that this Unity project can import.

    blender --background --factory-startup --python tools/art/convert_meshy_building.py -- SOURCE.glb OUT_DIR NAME

Unity 6 has no native glTF importer and this project deliberately carries no glTF package, so every external
model enters as FBX, the way the Meshy characters did. The GLB stays where it is as the source of record.

The script fits the building to the 3x3-cell footprint (the procedural plinth covers 96% of it) and keeps it under
3.4 m so it clears the 3.95 m health bar, holds it near the triangle budget, and unpacks the glTF material into
Unity's URP/Lit layout: glTF packs metal in blue and roughness in green; URP wants metal in red and smoothness,
which is one minus roughness, in alpha.
"""
import sys
from pathlib import Path

import bpy
import numpy as np

args = sys.argv[sys.argv.index('--') + 1:]
SOURCE, OUT, NAME = Path(args[0]).resolve(), Path(args[1]).resolve(), args[2]
FOOTPRINT, MAX_HEIGHT, TARGET = 3.0 * .96, 3.4, 5000
TEXTURES = OUT / 'Textures'
TEXTURES.mkdir(parents=True, exist_ok=True)

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=str(SOURCE))
meshes = [o for o in bpy.context.scene.objects if o.type == 'MESH']
if not meshes:
    raise SystemExit('BARRACKS_FAIL the GLB contains no mesh')
bpy.ops.object.select_all(action='DESELECT')
for mesh in meshes:
    mesh.select_set(True)
bpy.context.view_layer.objects.active = meshes[0]
bpy.ops.object.parent_clear(type='CLEAR_KEEP_TRANSFORM')
if len(meshes) > 1:
    bpy.ops.object.join()
obj = bpy.context.view_layer.objects.active
obj.name = NAME
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)

points = np.empty(len(obj.data.vertices) * 3, dtype=np.float32)
obj.data.vertices.foreach_get('co', points)
points = points.reshape(-1, 3)
low, high = points.min(0), points.max(0)
size = high - low
# Blender is Z-up: X and Y are the ground, Z is height.
scale = min(FOOTPRINT / max(size[0], size[1]), MAX_HEIGHT / size[2])
points = (points - np.array([(low[0] + high[0]) / 2, (low[1] + high[1]) / 2, low[2]], np.float32)) * scale
obj.data.vertices.foreach_set('co', points.ravel())
obj.data.update()

source_triangles = sum(len(p.vertices) - 2 for p in obj.data.polygons)
triangles = source_triangles
if triangles > TARGET * 1.2:
    modifier = obj.modifiers.new('RTS triangle budget', 'DECIMATE')
    modifier.decimate_type = 'COLLAPSE'
    modifier.ratio = TARGET / triangles
    modifier.use_collapse_triangulate = True
    bpy.ops.object.modifier_apply(modifier=modifier.name)
    triangles = sum(len(p.vertices) - 2 for p in obj.data.polygons)


def upstream_image(socket):
    for link in socket.links:
        node = link.from_node
        if node.type == 'TEX_IMAGE' and node.image:
            return node.image
        for entry in node.inputs:
            found = upstream_image(entry)
            if found:
                return found
    return None


def read(image):
    width, height = image.size
    pixels = np.empty(width * height * 4, np.float32)
    image.pixels.foreach_get(pixels)
    return width, height, pixels.reshape(-1, 4)


def write(name, width, height, pixels):
    image = bpy.data.images.new(name, width, height, alpha=True)
    image.pixels.foreach_set(pixels.astype(np.float32).ravel())
    image.filepath_raw = str(TEXTURES / (name + '.png'))
    image.file_format = 'PNG'
    image.save()


materials = {slot.material for slot in obj.material_slots if slot.material}
principled = next((n for m in materials if m.use_nodes for n in m.node_tree.nodes if n.type == 'BSDF_PRINCIPLED'), None)
base = normal = packed = None
if principled:
    base = upstream_image(principled.inputs['Base Color'])
    normal = upstream_image(principled.inputs['Normal'])
    packed = upstream_image(principled.inputs['Metallic']) or upstream_image(principled.inputs['Roughness'])
if base:
    write(NAME + '_BaseColor', *read(base))
if normal:
    write(NAME + '_Normal', *read(normal))
if packed:
    width, height, pixels = read(packed)
    unity = np.zeros_like(pixels)
    unity[:, 0] = pixels[:, 2]          # glTF blue = metallic -> Unity red
    unity[:, 3] = 1 - pixels[:, 1]      # glTF green = roughness -> Unity alpha = smoothness
    write(NAME + '_MetallicSmoothness', width, height, unity)

bpy.ops.object.select_all(action='DESELECT')
obj.select_set(True)
bpy.context.view_layer.objects.active = obj
bpy.ops.export_scene.fbx(
    filepath=str(OUT / (NAME + '.fbx')), use_selection=True, object_types={'MESH'},
    apply_unit_scale=True, apply_scale_options='FBX_SCALE_UNITS', axis_forward='-Z', axis_up='Y',
    use_space_transform=True, bake_space_transform=False, use_mesh_modifiers=True, mesh_smooth_type='FACE',
    use_tspace=False, add_leaf_bones=False, bake_anim=False, path_mode='STRIP', embed_textures=False)

fitted = (size * scale).round(3).tolist()
print('BARRACKS_OK source_triangles=%d triangles=%d fitted_xyz_m=%s materials=%d base=%s normal=%s metal_rough=%s'
      % (source_triangles, triangles, fitted, len(materials), bool(base), bool(normal), bool(packed)), flush=True)
