"""Read the exported FBX back into Blender and validate the actual transfer.

This validates the FBX data, not Unity rendering, mobile memory or frame rate.
"""
import json,sys
from pathlib import Path
import bpy
from mathutils import Vector

ROOT=Path(__file__).resolve().parents[2]
CACHE=Path('D:/CodexTooling/kingdom-premium/environment')
OUTPUT=ROOT/'Assets/Game/KingdomPremium/Environment'
EXPORT=Path(sys.argv[sys.argv.index('--')+1]) if '--' in sys.argv and len(sys.argv)>sys.argv.index('--')+1 else OUTPUT
manifest=json.loads((EXPORT/'material-manifest.json').read_text(encoding='utf-8'))
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)
bpy.ops.import_scene.fbx(filepath=str(EXPORT/'Environment.fbx'),use_anim=False)
known={m['name'] for m in manifest['materials']}
errors=[];models=[];tree_rows=[]
for obj in bpy.context.scene.objects:
    if obj.type!='MESH':continue
    mesh=obj.data
    materials=[m.name if m else '' for m in mesh.materials]
    for name in materials:
        if name not in known:errors.append(obj.name+': unexpected material '+name)
    if not materials:errors.append(obj.name+': no material slots')
    bad=sum(p.material_index>=len(materials) for p in mesh.polygons)
    if bad:errors.append(obj.name+': invalid polygon material indices '+str(bad))
    corners=[obj.matrix_world@Vector(p) for p in obj.bound_box]
    bounds={'min':[min(v[i] for v in corners) for i in range(3)],
            'max':[max(v[i] for v in corners) for i in range(3)]}
    row={'name':obj.name,'triangles':sum(len(p.vertices)-2 for p in mesh.polygons),
         'materials':materials,'boundsBlenderZUp':bounds}
    models.append(row)
    if 'optimized living tree' in obj.name:
        tree_rows.append(row)
        if not 50000<=row['triangles']<=350100:errors.append(obj.name+': tree triangle budget')
        if set(materials)!={'ENV_TreeBark','ENV_TreeBranch','ENV_TreeLeaf'}:
            errors.append(obj.name+': missing tree material layer')
    if 'coopered' in obj.name and materials!=['ENV_Barrel']:
        errors.append(obj.name+': wrong barrel remapping')
    if 'meadow tuft' in obj.name and materials!=['ENV_Grass']:
        errors.append(obj.name+': wrong grass remapping')
for material in manifest['materials']:
    for key in ('baseColorTexture','normalTexture','roughnessTexture','metallicTexture','opacityTexture'):
        if material.get(key) and not (OUTPUT/material[key]).is_file():
            errors.append(material['name']+': missing '+key)
    if material.get('cutout') and not material.get('opacityTexture'):
        errors.append(material['name']+': alpha-tested surface missing opacity')
report={'passed':not errors,'scope':'FBX round trip, material slots, referenced texture files and per-tree mesh budgets only',
        'meshObjects':len(models),'uniqueMeshes':len({o.data for o in bpy.context.scene.objects if o.type=='MESH'}),
        'treeInstances':len(tree_rows),'totalEvaluatedTriangles':sum(m['triangles'] for m in models),
        'errors':errors,'objects':models}
(EXPORT/'fbx-validation.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
print('ENVIRONMENT_FBX_CHECK '+json.dumps({k:v for k,v in report.items() if k!='objects'}),flush=True)
if errors:raise RuntimeError('Exported FBX validation failed')
