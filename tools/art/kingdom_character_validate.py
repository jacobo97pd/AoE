"""Reimport the three Kingdom FBXs in Blender and record actual exported topology.

Usage: blender --background --factory-startup --python this_file -- --folder PATH
Run after kingdom_characters.py, before publishing its files. No mesh is changed.
"""
import argparse
import hashlib
import json
import math
import sys
from pathlib import Path

import bpy


args = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
parser = argparse.ArgumentParser()
parser.add_argument('--folder', required=True)
folder = Path(parser.parse_args(args).folder)
report = {
    'passed': False,
    'scope': 'Blender FBX reimport: meshes, finite vertices, normals, UV, material slots and actual triangle totals. Does not validate native Unity visuals or mobile performance.',
    'unityImport': {'rootYawDegrees': 180, 'finalSceneUp': '+Y', 'finalSceneForward': '-Z', 'groundY': 0},
    'characters': [],
}
manifests = []
for kind in ['Worker', 'Warrior', 'Hero']:
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete(use_global=False)
    for material in list(bpy.data.materials):
        bpy.data.materials.remove(material)
    fbx = folder / (kind + '.fbx')
    manifest = json.loads((folder / (kind + '.json')).read_text(encoding='utf-8'))
    bpy.ops.import_scene.fbx(filepath=str(fbx), use_anim=False)
    entry = {'name': kind, 'fbxBytes': fbx.stat().st_size,
             'fbxSha256': hashlib.sha256(fbx.read_bytes()).hexdigest(), 'meshes': []}
    imported = [o for o in bpy.context.scene.objects if o.type == 'MESH']
    assert len(imported) == 3, (kind, 'Expected three LOD meshes', len(imported))
    for obj in sorted(imported, key=lambda o: o.name):
        mesh = obj.data
        mesh.update()
        mesh.calc_loop_triangles()
        lod = int(obj.name.rsplit('_LOD', 1)[1])
        assert 0 <= lod <= 2
        bad = sum(1 for v in mesh.vertices
                  if not all(math.isfinite(float(x)) for x in v.co) or v.normal.length < .5)
        uv = mesh.uv_layers.active
        assert bad == 0, (kind, obj.name, 'Invalid vertices or normals', bad)
        assert uv and len(uv.data) > 0, (kind, obj.name, 'Missing UV')
        assert all(math.isfinite(float(x)) for item in uv.data for x in item.uv)
        triangles = len(mesh.loop_triangles)
        assert 0 < triangles <= [300000, 100050, 35050][lod], (kind, lod, triangles)
        row = {'name': obj.name, 'vertices': len(mesh.vertices), 'triangles': triangles,
               'invalidVerticesOrNormals': bad, 'materialCount': len(mesh.materials),
               'materialNames': [m.name for m in mesh.materials], 'uvCount': len(uv.data),
               'worldMin': [min((obj.matrix_world @ v.co)[i] for v in mesh.vertices) for i in range(3)],
               'worldMax': [max((obj.matrix_world @ v.co)[i] for v in mesh.vertices) for i in range(3)]}
        source = manifest['lods'][lod]
        if 'beforeFbxReimport' not in source:
            source['beforeFbxReimport'] = {key: source[key] for key in ['vertices', 'triangles', 'materials']}
        source.update(vertices=row['vertices'], triangles=triangles, materials=row['materialCount'])
        source['countScope'] = 'Actual FBX mesh reimported in Blender; beforeFbxReimport records authoring topology.'
        entry['meshes'].append(row)
        print('VALIDATED', kind, lod, triangles, row['materialCount'], flush=True)
    manifest['fbxBytes'] = entry['fbxBytes']
    manifest['fbxSha256'] = entry['fbxSha256']
    manifest['fbxReimportValidated'] = True
    manifests.append((kind, manifest))
    report['characters'].append(entry)

# Only write results once every mesh passes. These are metadata updates; the
# FBX files are never modified by this validator.
report['passed'] = True
for kind, manifest in manifests:
    (folder / (kind + '.json')).write_text(json.dumps(manifest, indent=2), encoding='utf-8')
(folder / 'blender-export-validation.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
print(json.dumps(report, indent=2))
