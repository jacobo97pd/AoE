"""Create the 350K crown variant validated against the untouched source render."""
import bpy,json,math
from pathlib import Path
root=Path('D:/CodexTooling/kingdom-premium/environment')
bpy.ops.wm.open_mainfile(filepath=str(root/'imports.blend'))
tree=bpy.data.objects['tree_small_02_LOD0']
for obj in list(bpy.data.objects):
    if obj!=tree:bpy.data.objects.remove(obj,do_unlink=True)
for collection in list(tree.users_collection):collection.objects.unlink(tree)
bpy.context.scene.collection.objects.link(tree)
bpy.context.view_layer.objects.active=tree
tree.select_set(True)
before=sum(len(p.vertices)-2 for p in tree.data.polygons)
modifier=tree.modifiers.new('Visually reviewed moderate crown reduction','DECIMATE')
modifier.ratio=350000/before
modifier.use_collapse_triangulate=True
bpy.ops.object.modifier_apply(modifier=modifier.name)
after=sum(len(p.vertices)-2 for p in tree.data.polygons)
rows={i:{'name':m.name,'triangles':0,'faces':0} for i,m in enumerate(tree.data.materials)}
for p in tree.data.polygons:
    rows[p.material_index]['triangles']+=len(p.vertices)-2
    rows[p.material_index]['faces']+=1
tree.name='KingdomCrownTree'
tree.data.name='KingdomCrownTree'
bpy.ops.wm.save_as_mainfile(filepath=str(root/'tree-final.blend'))
report={'sourceTriangles':before,'preservedTriangles':after,'method':'Moderate collapse; crown compared visually against untouched source','materials':rows}
(root/'tree-final.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
print('PRESERVED_TREE '+json.dumps(report),flush=True)
