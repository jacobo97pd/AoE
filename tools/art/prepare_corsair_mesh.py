"""Prepare the user-supplied Meshy corsair without changing its authored appearance.

Run with Blender --background --python this_file -- --analyse for topology audit.
The original FBX / imported blend is never overwritten.
"""
import bpy
import json
import sys
import time
from pathlib import Path
from mathutils import Vector

import numpy as np

WORK = Path('D:/CodexTooling/crimson-corsair')


def emit(message):
    print(message, flush=True)


def connected_islands(obj):
    mesh = obj.data
    coords = np.empty(len(mesh.vertices) * 3, dtype=np.float32)
    mesh.vertices.foreach_get('co', coords)
    coords = coords.reshape(-1, 3)
    edges = np.empty(len(mesh.edges) * 2, dtype=np.int32)
    mesh.edges.foreach_get('vertices', edges)
    edges = edges.reshape(-1, 2)
    parent = list(range(len(coords)))
    sizes = [1] * len(coords)
    for ai, bi in edges:
        a, b = int(ai), int(bi)
        while parent[a] != a:
            parent[a] = parent[parent[a]]
            a = parent[a]
        while parent[b] != b:
            parent[b] = parent[parent[b]]
            b = parent[b]
        if a == b:
            continue
        if sizes[a] < sizes[b]:
            a, b = b, a
        parent[b] = a
        sizes[a] += sizes[b]
    for i in range(len(parent)):
        a = i
        while parent[a] != a:
            parent[a] = parent[parent[a]]
            a = parent[a]
        parent[i] = a
    roots, labels, counts = np.unique(parent, return_inverse=True, return_counts=True)
    minimum = np.full((len(roots), 3), np.inf, np.float32)
    maximum = np.full((len(roots), 3), -np.inf, np.float32)
    for axis in range(3):
        np.minimum.at(minimum[:, axis], labels, coords[:, axis])
        np.maximum.at(maximum[:, axis], labels, coords[:, axis])
    result = []
    for i in np.argsort(-counts):
        result.append({'island': int(i), 'vertices': int(counts[i]),
                       'min': minimum[i].tolist(), 'max': maximum[i].tolist()})
    return coords, labels, result


def open_source():
    bpy.ops.wm.open_mainfile(filepath=str(WORK / 'imported.blend'))
    obj = next(o for o in bpy.context.scene.objects if o.type == 'MESH')
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True)
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    return obj


def audit(obj):
    started = time.time()
    coords, labels, islands = connected_islands(obj)
    (WORK / 'island-analysis.json').write_text(json.dumps(islands, indent=2), encoding='utf-8')
    emit(json.dumps({'seconds': time.time()-started, 'islands': islands[:30]}, indent=2))
    np.savez_compressed(WORK / 'topology-labels.npz', labels=labels)


def tri_count(obj):
    return sum(len(poly.vertices) - 2 for poly in obj.data.polygons)


def prepare():
    obj = open_source()
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.mesh.separate(type='LOOSE')
    bpy.ops.object.mode_set(mode='OBJECT')
    pieces = [o for o in bpy.context.scene.objects if o.type == 'MESH']
    assert len(pieces) == 2, 'Do not classify unreviewed topology automatically.'
    pieces.sort(key=lambda o: len(o.data.vertices), reverse=True)
    source, decor = pieces
    assert len(source.data.vertices) == 1045555
    assert len(decor.data.vertices) == 147202
    minimum_z = min(v.co.z for v in source.data.vertices)
    maximum_z = max(v.co.z for v in source.data.vertices)
    scale = 2.5 / (maximum_z - minimum_z)
    for mesh_obj in pieces:
        coordinates = np.empty(len(mesh_obj.data.vertices)*3, np.float32)
        mesh_obj.data.vertices.foreach_get('co', coordinates)
        coordinates = coordinates.reshape(-1, 3)
        coordinates[:, 2] -= minimum_z
        coordinates *= scale
        mesh_obj.data.vertices.foreach_set('co', coordinates.ravel())
        mesh_obj.data.update()
    decor.name = 'Corsair_RemovedDisplayProp'
    decor.data.name = 'Corsair_RemovedDisplayProp_Mesh'
    bpy.data.libraries.write(str(WORK / 'decor.blend'), {decor}, compress=True)
    emit('Saved separated decorative prop. No geometric cutting used.')
    for other in list(bpy.context.scene.objects):
        if other != source:
            bpy.data.objects.remove(other, do_unlink=True)
    original_triangles = tri_count(source)
    manifest = {'sourceBodyTriangles': original_triangles,
                'sourceDecorVertices': 147202,
                'removal': 'Separate disconnected topology island, no region cuts',
                'scale': scale, 'sourceGroundZ': minimum_z,
                'centering': 'Original X/Y retained; Z moved to ground then uniform scale',
                'faceAxis': '-Y', 'height': 2.5, 'lods': []}
    lods = []
    for level, target in enumerate([140000, 45000, 12000]):
        started = time.time()
        lod = source.copy()
        lod.data = source.data.copy()
        bpy.context.collection.objects.link(lod)
        lod.name = f'Corsair_LOD{level}'
        lod.data.name = f'Corsair_LOD{level}_Mesh'
        bpy.ops.object.select_all(action='DESELECT')
        lod.select_set(True)
        bpy.context.view_layer.objects.active = lod
        modifier = lod.modifiers.new(name='Reviewed mesh simplification', type='DECIMATE')
        modifier.decimate_type = 'COLLAPSE'
        modifier.ratio = target / original_triangles
        modifier.use_collapse_triangulate = True
        bpy.ops.object.modifier_apply(modifier=modifier.name)
        for polygon in lod.data.polygons:
            polygon.use_smooth = True
        lod.hide_render = level > 0
        lod.hide_set(level > 0)
        lods.append(lod)
        record = {'name': lod.name, 'triangles': tri_count(lod),
                  'vertices': len(lod.data.vertices),
                  'uvLayers': [layer.name for layer in lod.data.uv_layers],
                  'materials': [m.name for m in lod.data.materials],
                  'seconds': round(time.time()-started, 2)}
        manifest['lods'].append(record)
        emit(json.dumps(record))
    bpy.data.objects.remove(source, do_unlink=True)
    bpy.context.view_layer.objects.active = lods[0]
    bpy.ops.object.select_all(action='DESELECT')
    lods[0].select_set(True)
    bpy.ops.wm.save_as_mainfile(filepath=str(WORK / 'prepared.blend'), compress=True)
    (WORK / 'preparation-manifest.json').write_text(json.dumps(manifest, indent=2), encoding='utf-8')
    emit('PREPARED_CORSAIR_OK')


def grid_review():
    bpy.ops.wm.open_mainfile(filepath=str(WORK / 'prepared.blend'))
    for obj in bpy.context.scene.objects:
        obj.hide_render = obj.name != 'Corsair_LOD0'
        obj.hide_set(obj.name != 'Corsair_LOD0')
    scene = bpy.context.scene
    scene.render.engine = 'BLENDER_EEVEE_NEXT'
    scene.render.resolution_x = 1400
    scene.render.resolution_y = 1600
    scene.render.resolution_percentage = 100
    if scene.world is None:
        scene.world = bpy.data.worlds.new('Coordinate review background')
    scene.world.color = (0.32, 0.32, 0.32)
    scene.view_settings.view_transform = 'AgX'
    for name, loc, energy, size in [('Key',(-3,-4,5),700,4),('Fill',(3,-2,3),350,3)]:
        lamp = bpy.data.lights.new(name, 'AREA')
        lamp.energy = energy
        lamp.shape = 'DISK'
        lamp.size = size
        lamp_obj = bpy.data.objects.new(name, lamp)
        scene.collection.objects.link(lamp_obj)
        lamp_obj.location = loc
        lamp_obj.rotation_euler = (Vector((0,0,1.2)) - lamp_obj.location).to_track_quat('-Z','Y').to_euler()
    camera = bpy.data.cameras.new('Coordinate camera')
    camera.type = 'ORTHO'
    camera.ortho_scale = 2.95
    camera_obj = bpy.data.objects.new('Coordinate camera', camera)
    scene.collection.objects.link(camera_obj)
    scene.camera = camera_obj
    mat = bpy.data.materials.new('Grid ink')
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get('Principled BSDF')
    bsdf.inputs['Base Color'].default_value = (.1,.12,.14,1)
    bsdf.inputs['Emission Color'].default_value = (.08,.1,.12,1)
    bsdf.inputs['Emission Strength'].default_value = 1
    def line(name, points, radius=.001):
        curve = bpy.data.curves.new(name, 'CURVE')
        curve.dimensions = '3D'
        curve.bevel_depth = radius
        curve.bevel_resolution = 0
        spline = curve.splines.new('POLY')
        spline.points.add(len(points)-1)
        for p, co in zip(spline.points, points): p.co = (*co,1)
        o = bpy.data.objects.new(name, curve)
        scene.collection.objects.link(o)
        o.data.materials.append(mat)
        return o
    def label(text, pos, side=False):
        font = bpy.data.curves.new('Label', 'FONT')
        font.body = text
        font.size = .039
        o = bpy.data.objects.new('Label',font)
        scene.collection.objects.link(o)
        o.data.materials.append(mat)
        o.location = pos
        o.rotation_euler = (1.5707963,0,1.5707963 if side else 0)
        return o
    for view in ['front','side']:
        grid = []
        if view == 'front':
            camera_obj.location = (0,-7,1.3)
            target = Vector((0,0,1.3))
            for z in np.arange(0,2.76,.25):
                grid.append(line('Z grid',[(-1.15,.95,float(z)),(1.15,.95,float(z))]))
                grid.append(label(f'z {z:.2f}',(-1.2,.94,float(z)+.01)))
            for x in np.arange(-1,1.01,.25):
                grid.append(line('X grid',[(float(x),.95,-.05),(float(x),.95,2.75)]))
                grid.append(label(f'{x:+.2f}',(float(x)-.03,.94,-.13)))
        else:
            camera_obj.location = (7,0,1.3)
            target = Vector((0,0,1.3))
            for z in np.arange(0,2.76,.25):
                grid.append(line('Z grid',[(-1,-1.15,float(z)),(-1,1.15,float(z))]))
                grid.append(label(f'z {z:.2f}',(-.99,-1.2,float(z)+.01),True))
            for y in np.arange(-1,1.01,.25):
                grid.append(line('Y grid',[(-1,float(y),-.05),(-1,float(y),2.75)]))
                grid.append(label(f'{y:+.2f}',(-.99,float(y)-.03,-.13),True))
        camera_obj.rotation_euler = (target-camera_obj.location).to_track_quat('-Z','Y').to_euler()
        scene.render.filepath = str(WORK / f'prepared-{view}-grid.png')
        bpy.ops.render.render(write_still=True)
        for obj in grid: bpy.data.objects.remove(obj,do_unlink=True)


def main():
    if '--analyse' in sys.argv:
        audit(open_source())
    elif '--review' in sys.argv:
        grid_review()
    else:
        prepare()


if __name__ == '__main__':
    main()
