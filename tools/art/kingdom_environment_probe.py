"""Cache imported CC0 FBX assets and inspect mesh/LOD structure before authoring."""
import bpy
import json
from pathlib import Path
from mathutils import Vector

cache = Path('D:/CodexTooling/kingdom-premium/environment')
assets = json.loads((cache / 'download-index.json').read_text(encoding='utf-8'))
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)
report = {}
for key, asset in assets.items():
    if 'fbx' not in asset:
        continue
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=asset['fbx'], use_anim=False)
    created = list(set(bpy.data.objects) - before)
    collection = bpy.data.collections.new('SOURCE_' + key)
    bpy.context.scene.collection.children.link(collection)
    for obj in created:
        for old in list(obj.users_collection):
            old.objects.unlink(obj)
        collection.objects.link(obj)
    collection.hide_render = True
    rows = []
    for obj in created:
        if obj.type != 'MESH':
            continue
        corners = [obj.matrix_world @ Vector(p) for p in obj.bound_box]
        rows.append({'name': obj.name, 'data': obj.data.name,
                     'vertices': len(obj.data.vertices), 'faces': len(obj.data.polygons),
                     'min': [min(p[i] for p in corners) for i in range(3)],
                     'max': [max(p[i] for p in corners) for i in range(3)],
                     'materials': [m.name if m else None for m in obj.data.materials]})
    report[key] = rows
    print('ASSET_PROBE ' + key + ' ' + json.dumps(rows), flush=True)
bpy.ops.wm.save_as_mainfile(filepath=str(cache / 'imports.blend'))
(cache / 'import-structure.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
