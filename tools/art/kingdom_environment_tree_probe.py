"""Inspect source versus reduced tree material topology before choosing a crown budget."""
import bpy,json
from pathlib import Path
root=Path('D:/CodexTooling/kingdom-premium/environment')
result={}
for filename in ('imports.blend','kingdom_environment.blend'):
    bpy.ops.wm.open_mainfile(filepath=str(root/filename))
    candidates=[o for o in bpy.data.objects if o.type=='MESH' and (o.name=='tree_small_02_LOD0' if filename=='imports.blend' else 'optimized living tree' in o.name)]
    obj=candidates[0]
    rows={i:{'name':mat.name,'triangles':0,'faces':0} for i,mat in enumerate(obj.data.materials)}
    for p in obj.data.polygons:
        rows[p.material_index]['triangles']+=len(p.vertices)-2
        rows[p.material_index]['faces']+=1
    result[filename]=rows
print('TREE_MATERIAL_COUNTS '+json.dumps(result),flush=True)
(root/'tree-material-topology.json').write_text(json.dumps(result,indent=2),encoding='utf-8')
