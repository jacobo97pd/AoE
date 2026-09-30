"""Native comparison of the untouched crown and moderate geometry reduction (D: only)."""
import bpy,json,math
from pathlib import Path
from mathutils import Vector,Matrix
CACHE=Path('D:/CodexTooling/kingdom-premium/environment')
bpy.ops.wm.open_mainfile(filepath=str(CACHE/'kingdom_environment.blend'))
bpy.data.collections['KingdomPremiumEnvironment'].hide_render=True
with bpy.data.libraries.load(str(CACHE/'imports.blend'),link=False) as (source,target):
    target.objects=['tree_small_02_LOD0']
source=target.objects[0]
source.data.transform(source.matrix_world);source.matrix_world=Matrix.Identity(4)
bpy.context.scene.collection.objects.link(source)
for i,mat in enumerate(source.data.materials):
    source.data.materials[i]=bpy.data.materials['ENV_TreeLeaf' if 'leaves' in mat.name else 'ENV_TreeBranch' if 'branches' in mat.name else 'ENV_TreeBark']
source.name='FULL SOURCE';source.location.x=-5
moderate=source.copy();moderate.data=source.data.copy();bpy.context.scene.collection.objects.link(moderate)
moderate.name='350K COLLAPSE';moderate.location.x=0
bpy.ops.object.select_all(action='DESELECT');moderate.select_set(True);bpy.context.view_layer.objects.active=moderate
modifier=moderate.modifiers.new('Moderate crown reduction','DECIMATE')
modifier.ratio=350000/sum(len(p.vertices)-2 for p in moderate.data.polygons)
modifier.use_collapse_triangulate=True
bpy.ops.object.modifier_apply(modifier=modifier.name)
moderate.data.name='TreeModerate350k'
with bpy.data.libraries.load(str(CACHE/'tree-preserved-compact.blend'),link=False) as (library,target):
    target.objects=['KingdomBoundaryPreservedTree']
compact=target.objects[0];compact.data.transform(compact.matrix_world);compact.matrix_world=Matrix.Identity(4)
bpy.context.scene.collection.objects.link(compact);compact.name='180K DISSOLVE';compact.location.x=5
for i,mat in enumerate(compact.data.materials):
    compact.data.materials[i]=bpy.data.materials['ENV_TreeLeaf' if 'leaves' in mat.name else 'ENV_TreeBranch' if 'branches' in mat.name else 'ENV_TreeBark']
scene=bpy.context.scene
camera=scene.camera;camera.location=(7,-16,8)
camera.rotation_euler=(Vector((0,0,2))-camera.location).to_track_quat('-Z','Y').to_euler()
camera.data.ortho_scale=17
scene.render.resolution_x=1500;scene.render.resolution_y=800;scene.cycles.samples=16
scene.render.filepath=str(CACHE/'tree-crown-comparison.png')
bpy.ops.wm.save_as_mainfile(filepath=str(CACHE/'tree-comparison.blend'))
bpy.ops.render.render(write_still=True)
