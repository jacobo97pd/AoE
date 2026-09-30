"""Stage the visually reviewed crown and verdant courtyard on D: during Unity import."""
import bpy,json
from pathlib import Path
from mathutils import Matrix
ROOT=Path(__file__).resolve().parents[2]
CACHE=Path('D:/CodexTooling/kingdom-premium/environment')
OUTPUT=ROOT/'Assets/Game/KingdomPremium/Environment'
STAGE=CACHE/'staged';STAGE.mkdir(exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=str(CACHE/'kingdom_environment.blend'))
collection=bpy.data.collections['KingdomPremiumEnvironment']
with bpy.data.libraries.load(str(CACHE/'tree-comparison.blend'),link=False) as (source,target):
    target.meshes=['TreeModerate350k']
mesh=target.meshes[0]
mesh.transform(Matrix.Translation((0,0,-min(v.co.z for v in mesh.vertices))))
for i,mat in enumerate(mesh.materials):
    mesh.materials[i]=bpy.data.materials['ENV_TreeLeaf' if 'TreeLeaf' in mat.name else 'ENV_TreeBranch' if 'TreeBranch' in mat.name else 'ENV_TreeBark']
mesh.name='CC0_tree_small_02_crown350k'
for obj in collection.objects:
    if 'optimized living tree' in obj.name:obj.data=mesh
manifest=json.loads((OUTPUT/'material-manifest.json').read_text(encoding='utf-8'))
color=(.32,.8,.25,1)
mat=bpy.data.materials['ENV_WoodlandSoil']
for node in mat.node_tree.nodes:
    if node.type=='MIX_RGB':node.inputs[2].default_value=color
next(m for m in manifest['materials'] if m['name']==mat.name)['color']=list(color)
objects=[o for o in collection.objects if o.type=='MESH']
bpy.ops.object.select_all(action='DESELECT')
for obj in objects:obj.select_set(True)
bpy.context.view_layer.objects.active=objects[0]
bpy.ops.export_scene.fbx(filepath=str(STAGE/'Environment.fbx'),use_selection=True,object_types={'MESH'},
    apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',axis_forward='-Z',axis_up='Y',
    use_space_transform=True,bake_space_transform=False,use_mesh_modifiers=True,mesh_smooth_type='FACE',
    use_tspace=True,add_leaf_bones=False,bake_anim=False,path_mode='STRIP',embed_textures=False)
rows={i:{'name':m.name,'triangles':0} for i,m in enumerate(mesh.materials)}
for p in mesh.polygons:rows[p.material_index]['triangles']+=len(p.vertices)-2
manifest['statistics']['tree']={'sourceTriangles':2062487,'exportTrianglesPerTree':sum(r['triangles'] for r in rows.values()),
    'method':'Moderate collapse reduction; crown compared visually with untouched source in native Blender render',
    'materials':rows,'visualComparison':str(CACHE/'tree-crown-comparison.png')}
manifest['statistics'].update(meshObjects=len(objects),uniqueMeshes=len({o.data for o in objects}),
    evaluatedTriangles=sum(sum(len(p.vertices)-2 for p in o.data.polygons) for o in objects),
    fbxBytes=(STAGE/'Environment.fbx').stat().st_size)
manifest['authoring']['nativePreview']=str(CACHE/'kingdom-courtyard-final.png')
manifest['authoring']['blend']=str(CACHE/'kingdom_environment_final.blend')
(STAGE/'material-manifest.json').write_text(json.dumps(manifest,indent=2),encoding='utf-8')
bpy.context.scene.render.filepath=str(CACHE/'kingdom-courtyard-final.png')
bpy.ops.wm.save_as_mainfile(filepath=str(CACHE/'kingdom_environment_final.blend'))
bpy.ops.render.render(write_still=True)
print('STAGED_ENVIRONMENT '+json.dumps(manifest['statistics']),flush=True)
