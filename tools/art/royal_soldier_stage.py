"""A small guard courtyard for presenting one soldier; no gameplay content.

Blender Z up, soldier at (0,0,0), front -Y. Keeps the central x +/-1,
y +/- .8 clear. Existing Kingdom materials are reused by exact name in Unity.
Run: Blender --background --factory-startup --python tools/art/royal_soldier_stage.py
"""
import bpy
import math
import json
import random
import sys
from pathlib import Path
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[2]
WORK = Path('D:/CodexTooling/royal-soldier')
sys.path.insert(0, str(Path(__file__).parent))
import royal_soldier_materials as R

RNG = random.Random(9242)
PARTS = []


def environment_materials():
    env = ROOT / 'Assets/Game/KingdomPremium/Environment'
    source = json.loads((env / 'material-manifest.json').read_text())
    required = {'ENV_CutLimestone', 'ENV_CourtyardCobble', 'ENV_Oak', 'ENV_ForgedIron'}
    for entry in source['materials']:
        if entry['name'] not in required:
            continue
        mat = bpy.data.materials.get(entry['name']) or bpy.data.materials.new(entry['name'])
        mat.use_nodes = True
        nodes, links = mat.node_tree.nodes, mat.node_tree.links
        bs = nodes.get('Principled BSDF')
        bs.inputs['Base Color'].default_value = entry.get('color', [.5, .5, .5, 1])
        bs.inputs['Metallic'].default_value = entry.get('metallic', 0)
        bs.inputs['Roughness'].default_value = 1 - entry.get('smoothness', .3)
        if entry.get('baseColorTexture'):
            tex = nodes.new('ShaderNodeTexImage')
            tex.image = bpy.data.images.load(str(env / entry['baseColorTexture']), check_existing=True)
            mix = nodes.new('ShaderNodeMixRGB'); mix.blend_type = 'MULTIPLY'; mix.inputs[0].default_value = 1
            mix.inputs[1].default_value = entry.get('color', [1, 1, 1, 1])
            links.new(tex.outputs['Color'], mix.inputs[2]); links.new(mix.outputs['Color'], bs.inputs['Base Color'])
        if entry.get('normalTexture'):
            tex = nodes.new('ShaderNodeTexImage')
            tex.image = bpy.data.images.load(str(env / entry['normalTexture']), check_existing=True)
            tex.image.colorspace_settings.name = 'Non-Color'
            normal = nodes.new('ShaderNodeNormalMap'); normal.inputs['Strength'].default_value = .45
            links.new(tex.outputs['Color'], normal.inputs['Color']); links.new(normal.outputs['Normal'], bs.inputs['Normal'])
        if entry.get('roughnessTexture'):
            tex = nodes.new('ShaderNodeTexImage')
            tex.image = bpy.data.images.load(str(env / entry['roughnessTexture']), check_existing=True)
            tex.image.colorspace_settings.name = 'Non-Color'
            links.new(tex.outputs['Color'], bs.inputs['Roughness'])


def finish(obj, name, material):
    obj.name = name
    obj.data.materials.append(bpy.data.materials[material])
    PARTS.append(obj)
    return obj


def cube(name, center, scale, material, bevel=.014):
    bpy.ops.mesh.primitive_cube_add(size=1, location=center)
    obj = bpy.context.object
    obj.scale = scale
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    finish(obj, name, material)
    if bevel:
        mod = obj.modifiers.new('Softly worn carved edges', 'BEVEL'); mod.width = bevel; mod.segments = 3
        mod = obj.modifiers.new('Weighted face normals', 'WEIGHTED_NORMAL'); mod.keep_sharp = True; mod.weight = 50
    # Consistent world-sized planar UVs: no entire cobble texture per block.
    uv = obj.data.uv_layers.active or obj.data.uv_layers.new(name='UVMap')
    uv.name = 'UVMap'
    for poly in obj.data.polygons:
        dominant = max(range(3), key=lambda a: abs(poly.normal[a]))
        axes = [a for a in range(3) if a != dominant]
        for li in poly.loop_indices:
            co = obj.matrix_world @ obj.data.vertices[obj.data.loops[li].vertex_index].co
            uv.data[li].uv = (co[axes[0]] * .32, co[axes[1]] * .32)
    return obj


def bar(name, a, b, radius, material):
    a, b = Vector(a), Vector(b)
    bpy.ops.mesh.primitive_cylinder_add(vertices=32, radius=radius, depth=(b-a).length, location=(a+b)*.5)
    obj = bpy.context.object; obj.rotation_euler = (b-a).to_track_quat('Z', 'Y').to_euler()
    return finish(obj, name, material)


def banner():
    vertices, uvs, faces = [], [], []
    rows, cols = 20, 18
    for row in range(rows + 1):
        v = row / rows
        for col in range(cols + 1):
            u = col / cols
            x = -1.73 + .61 * u
            y = 1.44 - .033 * math.sin(u * math.pi * 4) * (.25 + .75 * v)
            z = 1.08 - .58 * v - .035 * math.sin(u * math.pi) * v
            vertices.append((x, y, z)); uvs.append((u, 1-v))
    for row in range(rows):
        for col in range(cols):
            a = row * (cols+1) + col
            faces.append((a, a+cols+1, a+cols+2, a+1))
    mesh = bpy.data.meshes.new('Guard banner tailored surface'); mesh.from_pydata(vertices, [], faces); mesh.update()
    uv = mesh.uv_layers.new(name='UVMap')
    for p in mesh.polygons:
        p.use_smooth = True
        for li in p.loop_indices: uv.data[li].uv = uvs[mesh.loops[li].vertex_index]
    obj = bpy.data.objects.new('Small blue guard cloth', mesh); bpy.context.collection.objects.link(obj)
    finish(obj, obj.name, 'RS_BlueCloth')
    mod = obj.modifiers.new('Real cloth thickness', 'SOLIDIFY'); mod.thickness = .004
    bar('Cloth iron crossbar', (-1.80,1.455,1.105), (-1.06,1.455,1.105), .014, 'ENV_ForgedIron')
    for x in (-1.69,-1.16):
        cube('Cloth bracket', (x,1.55,1.105), (.025,.21,.035), 'ENV_ForgedIron', .004)


def build():
    bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)
    environment_materials(); R.register_materials()
    cube('Courtyard mortar foundation', (0,.2,-.105), (4.5,4,.16), 'ENV_CourtyardCobble', .012)
    # 8 courses of large pavers. A few millimetres of irregularity keep the
    # surface tactile without tipping or floating the soldier's boots.
    for row in range(8):
        y = -1.8 + (row+.5)*.5
        x = -2.25
        widths = ([.28] + [.56]*7 + [.30]) if row%2 else [.5625]*8
        for col,w in enumerate(widths):
            height=.046 + RNG.uniform(-.0015,.0015)
            obj=cube(f'Ashlar paving {row:02d}-{col:02d}', (x+w*.5,y,-height*.5), (w-.016,.484,height), 'ENV_CourtyardCobble', .008)
            x += w
    # Back wall, architectural rather than a display plinth.
    for row in range(3):
        for col in range(7):
            x=-2.13+(col+.5)*.608
            cube(f'Guard parapet ashlar {row:02d}-{col:02d}', (x,1.89,.135+row*.235), (.594,.30,.225), 'ENV_CutLimestone', .013)
    cube('Parapet coping front bevel', (0,1.89,.792), (4.35,.43,.14), 'ENV_CutLimestone', .025)
    for side in (-1,1):
        x=side*2.00
        cube('Guard pillar foot', (x,1.82,.065), (.46,.48,.13), 'ENV_CutLimestone', .018)
        for row in range(4):
            cube(f'Guard pillar {side} course {row}', (x,1.82,.27+row*.255), (.355,.375,.247), 'ENV_CutLimestone', .015)
        cube('Guard pillar neck', (x,1.82,1.22), (.395,.415,.10), 'ENV_CutLimestone', .013)
        cube('Guard pillar cap', (x,1.82,1.30), (.46,.48,.085), 'ENV_CutLimestone', .024)
    banner()
    for obj in PARTS:
        bpy.context.view_layer.objects.active=obj
        for mod in list(obj.modifiers): bpy.ops.object.modifier_apply(modifier=mod.name)
    bpy.ops.object.select_all(action='DESELECT')
    for obj in PARTS: obj.select_set(True)
    bpy.context.view_layer.objects.active=PARTS[0]
    WORK.mkdir(parents=True,exist_ok=True)
    bpy.ops.export_scene.fbx(filepath=str(WORK/'Stage.fbx'),use_selection=True,object_types={'MESH'},
        axis_forward='Z',axis_up='Y',bake_space_transform=True,use_mesh_modifiers=True,
        add_leaf_bones=False,bake_anim=False,path_mode='STRIP',mesh_smooth_type='FACE')
    report={'meshObjects':len(PARTS),'triangles':sum(sum(len(p.vertices)-2 for p in o.data.polygons) for o in PARTS),
        'materials':sorted({m.name for o in PARTS for m in o.data.materials if m}),
        'units':0,'footprintMeters':[4.5,4.0],'soldierOrigin':[0,0,0],
        'coordinateConvention':'Blender Z up/front -Y; FBX Z forward/Y up; inspect Unity imported root yaw',
        'reusedSource':'Assets/Game/KingdomPremium/Environment/material-manifest.json'}
    (WORK/'stage-validation.json').write_text(json.dumps(report,indent=2))
    # Non-exported inspection camera and neutral daylight. Root lights the
    # native Unity scene separately; this preview is not delivery evidence.
    for loc,power,size in [((-3,-4,6),650,4),((3,2,5),450,3)]:
        bpy.ops.object.light_add(type='AREA',location=loc);o=bpy.context.object;o.data.energy=power;o.data.size=size;o.rotation_euler=(Vector((0,0,.5))-o.location).to_track_quat('-Z','Y').to_euler()
    bpy.ops.object.camera_add(location=(4,-6,4.5));o=bpy.context.object;o.rotation_euler=(Vector((0,.2,.4))-o.location).to_track_quat('-Z','Y').to_euler();o.data.type='ORTHO';o.data.ortho_scale=6.0
    scene=bpy.context.scene;scene.camera=o;scene.world.use_nodes=True;scene.world.node_tree.nodes['Background'].inputs[1].default_value=.3
    scene.render.engine='CYCLES';scene.cycles.samples=16;scene.cycles.use_denoising=True
    scene.render.resolution_x=1200;scene.render.resolution_y=950;scene.render.resolution_percentage=100
    scene.view_settings.view_transform='AgX';scene.render.image_settings.file_format='PNG';scene.render.filepath=str(WORK/'stage-preview.png')
    bpy.ops.wm.save_as_mainfile(filepath=str(WORK/'stage.blend'))
    bpy.ops.render.render(write_still=True)
    print('ROYAL_SOLDIER_STAGE_OK',json.dumps(report))


if __name__ == '__main__': build()
