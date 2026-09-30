"""Combine the two authored parts into one real, static FBX and editable Blender file."""
import bpy, json, sys, re, math, hashlib, shutil
from pathlib import Path
from mathutils import Vector

ROOT=Path(__file__).resolve().parents[2]
WORK=Path('D:/CodexTooling/royal-soldier')
OUT=ROOT/'Assets/Game/RoyalSoldier/Model'
OUT.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.context.scene.unit_settings.system='METRIC'
bpy.context.scene.unit_settings.scale_length=1
parts=[]
for source in ['body.blend','armor.blend']:
    with bpy.data.libraries.load(str(WORK/source),link=False) as (src,dst):
        dst.objects=src.objects
    for obj in dst.objects:
        if obj.type in {'MESH','CURVE','EMPTY'} and not obj.name.startswith('PREVIEW_ONLY'):
            bpy.context.scene.collection.objects.link(obj)
            parts.append(obj)
        else:
            bpy.data.objects.remove(obj,do_unlink=True)

# Keep separate authored meshes so rigid plates never receive a body deformation.
# Collapse modifiers into the exported meshes, retaining source geometry and normals.
bpy.ops.object.select_all(action='DESELECT')
for obj in parts:
    if obj.type in {'MESH','CURVE'}: obj.select_set(True)
bpy.context.view_layer.objects.active=next(o for o in parts if o.type=='MESH')
bpy.ops.object.convert(target='MESH')
meshes=[o for o in bpy.context.selected_objects if o.type=='MESH']
for obj in meshes:
    for slot in obj.material_slots:
        if slot.material:
            name=re.sub(r'\.\d{3}$','',slot.material.name)
            existing=bpy.data.materials.get(name)
            if existing is not None:slot.material=existing
    if not obj.data.color_attributes:
        color=obj.data.color_attributes.new(name='Color',type='FLOAT_COLOR',domain='POINT')
        color.data.foreach_set('color',[1.0]*(len(color.data)*4))
    obj.data.calc_loop_triangles()

# Refresh surface parameters from the current shared definitions, even when a
# source .blend was saved before the last material adjustment.
sys.path.insert(0,str(Path(__file__).resolve().parent))
import royal_soldier_materials
royal_soldier_materials.register_materials()

# Thousands of stitches/hair tufts remain real geometry, but do not need their own
# draw call or Transform. Joining equal material sets changes no authored position.
groups={}
for obj in meshes:
    key=tuple(m.name for m in obj.data.materials if m)
    groups.setdefault(key,[]).append(obj)
for key,objects in groups.items():
    bpy.ops.object.select_all(action='DESELECT')
    for obj in objects:obj.select_set(True)
    bpy.context.view_layer.objects.active=objects[0]
    if len(objects)>1:bpy.ops.object.join()
    bpy.context.view_layer.objects.active.name='Soldier_'+('_'.join(key) or 'Surface')
meshes=[o for o in bpy.context.scene.objects if o.type=='MESH']
bpy.ops.object.select_all(action='DESELECT')
for obj in meshes:obj.select_set(True);obj.data.calc_loop_triangles()

stats=[]
for obj in meshes:
    stats.append({'name':obj.name,'triangles':len(obj.data.loop_triangles),'vertices':len(obj.data.vertices),'uv':len(obj.data.uv_layers)>0,'vertexColors':len(obj.data.color_attributes)>0,'materials':[m.name for m in obj.data.materials if m]})
total=sum(o['triangles'] for o in stats)
mins=[float('inf')]*3;maxs=[float('-inf')]*3
for obj in meshes:
    for corner in obj.bound_box:
        p=obj.matrix_world@Vector(corner)
        for k in range(3):mins[k]=min(mins[k],p[k]);maxs[k]=max(maxs[k],p[k])
manifest={'scope':'One static posed Kingdom infantry soldier. No rig or gameplay integration.','coordinates':'Blender metres, Z up, front -Y. Inspect Unity root orientation after import.','meshes':stats,'triangles':total,'boundsMin':mins,'boundsMax':maxs,'sources':['body.blend','armor.blend']}
bpy.ops.file.pack_all()
bpy.ops.wm.save_as_mainfile(filepath=str(WORK/'RoyalSoldier.blend'),compress=True)
bpy.ops.export_scene.fbx(filepath=str(OUT/'RoyalSoldier.fbx'),use_selection=True,object_types={'MESH'},axis_forward='Z',axis_up='Y',apply_unit_scale=True,apply_scale_options='FBX_SCALE_ALL',bake_space_transform=True,use_mesh_modifiers=True,mesh_smooth_type='FACE',add_leaf_bones=False,bake_anim=False,path_mode='STRIP',embed_textures=False,colors_type='LINEAR')
manifest['fbxSha256']=hashlib.sha256((OUT/'RoyalSoldier.fbx').read_bytes()).hexdigest()
(OUT/'model-manifest.json').write_text(json.dumps(manifest,indent=2),encoding='utf-8')
source=ROOT/'Artifacts/ArtReview/royal-soldier/source'
source.mkdir(parents=True,exist_ok=True)
shutil.copyfile(WORK/'RoyalSoldier.blend',source/'RoyalSoldier.blend')
print('ROYAL_SOLDIER_EXPORTED',total,'triangles',len(meshes),'meshes',mins,maxs,flush=True)
