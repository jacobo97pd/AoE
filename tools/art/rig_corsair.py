"""Create a fitted, weighted generic game rig and six in-place clips for the supplied corsair.

The source is a posed scan-like Meshy mesh, not an anatomical T-pose. Joint locations
are fitted to that pose. The decorative barrel is removed by connected island in
prepare_corsair_mesh.py before this script. Source surface UVs are retained.
"""
import bpy, math, json, sys, shutil, hashlib, importlib.util
import numpy as np
from pathlib import Path
from mathutils import Vector, Quaternion

ROOT=Path(__file__).resolve().parents[2]
WORK=Path('D:/CodexTooling/crimson-corsair')
ASSET=ROOT/'Assets/Game/CrimsonCorsair'
OUT=ASSET/'Model';OUT.mkdir(parents=True,exist_ok=True)
TEX=ASSET/'Textures';TEX.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=str(WORK/'prepared.blend'))
scene=bpy.context.scene
scene.render.fps=30
meshes=[o for o in scene.objects if o.type=='MESH' and 'LOD' in o.name]
if len(meshes)!=3: raise ValueError('Expected precisely three source-preserving LOD meshes')
for o in list(scene.objects):
    if o not in meshes: bpy.data.objects.remove(o,do_unlink=True)

# Use the external lossless maps supplied in the archive, not the FBX embedded JPG previews.
source=next((WORK/'source').glob('*'))
for suffix,name in [('_texture.png','BaseColor.png'),('_texture_normal.png','Normal.png'),('_texture_metallic.png','Metallic.png'),('_texture_roughness.png','Roughness.png')]:
    path=next(p for p in source.glob('*.png') if p.name.endswith(suffix))
    shutil.copy2(path,TEX/name)
mat=bpy.data.materials.new('Corsair_Surface');mat.use_nodes=True
nodes=mat.node_tree.nodes;nodes.clear();links=mat.node_tree.links
out=nodes.new('ShaderNodeOutputMaterial');bsdf=nodes.new('ShaderNodeBsdfPrincipled');links.new(bsdf.outputs['BSDF'],out.inputs['Surface'])
for filename,socket in [('BaseColor.png','Base Color'),('Metallic.png','Metallic'),('Roughness.png','Roughness')]:
    tex=nodes.new('ShaderNodeTexImage');tex.image=bpy.data.images.load(str(TEX/filename),check_existing=True)
    if socket!='Base Color':tex.image.colorspace_settings.name='Non-Color'
    links.new(tex.outputs['Color'],bsdf.inputs[socket])
normal=nodes.new('ShaderNodeTexImage');normal.image=bpy.data.images.load(str(TEX/'Normal.png'));normal.image.colorspace_settings.name='Non-Color'
normalmap=nodes.new('ShaderNodeNormalMap');normalmap.inputs['Strength'].default_value=.55;links.new(normal.outputs['Color'],normalmap.inputs['Color']);links.new(normalmap.outputs['Normal'],bsdf.inputs['Normal'])
for obj in meshes:
    obj.data.materials.clear();obj.data.materials.append(mat)

# Blender coordinates: metres, Z up, front -Y; height including hat is 2.5 m.
joints={
 'Root':((-.15,0,0),(-.15,0,.22),None),
 'Hips':((-.15,.015,1.12),(-.15,.015,1.36),'Root'),
 'Spine':((-.15,.015,1.36),(-.15,.015,1.62),'Hips'),
 'Chest':((-.15,.015,1.62),(-.11,.055,1.91),'Spine'),
 'Neck':((-.11,.055,1.91),(-.075,-.035,2.08),'Chest'),
 'Head':((-.075,-.035,2.08),(-.055,-.04,2.38),'Neck'),
 'Clavicle.R':((-.12,.035,1.83),(-.43,.10,1.81),'Chest'),
 'UpperArm.R':((-.43,.10,1.81),(-.59,.00,1.58),'Clavicle.R'),
 'Forearm.R':((-.59,.00,1.58),(-.64,-.23,1.29),'UpperArm.R'),
 'Hand.R':((-.64,-.23,1.29),(-.66,-.25,1.17),'Forearm.R'),
 'Clavicle.L':((-.12,.035,1.83),(.22,.22,1.82),'Chest'),
 'UpperArm.L':((.22,.22,1.82),(.34,.21,1.47),'Clavicle.L'),
 'Forearm.L':((.34,.21,1.47),(.50,.14,1.14),'UpperArm.L'),
 'Hand.L':((.50,.14,1.14),(.50,.12,1.02),'Forearm.L'),
 'Thigh.R':((-.34,.015,1.08),(-.435,-.055,.48),'Hips'),
 'Shin.R':((-.435,-.055,.48),(-.59,-.09,.13),'Thigh.R'),
 'Foot.R':((-.59,-.09,.13),(-.565,-.28,.075),'Shin.R'),
 'Toe.R':((-.565,-.28,.075),(-.575,-.365,.07),'Foot.R'),
 'Thigh.L':((.045,.08,1.08),(.105,.16,.51),'Hips'),
 'Shin.L':((.105,.16,.51),(.16,.22,.13),'Thigh.L'),
 'Foot.L':((.16,.22,.13),(.24,-.04,.075),'Shin.L'),
 'Toe.L':((.24,-.04,.075),(.29,-.14,.07),'Foot.L'),
 'CoatBack':((-.15,.20,1.20),(-.12,.45,.47),'Hips'),
 'Coat.R':((-.34,.02,1.18),(-.53,.03,.49),'Hips'),
 'Coat.L':((.08,.13,1.18),(.275,.28,.49),'Hips'),
 'Parrot':((.23,.15,1.85),(.26,.10,2.17),'Chest'),
 'Sword':((-.637,-.24,1.135),(.15,-.86,.72),'Hand.R'),
}
armdata=bpy.data.armatures.new('Corsair_Skeleton');arm=bpy.data.objects.new('CorsairRig',armdata);scene.collection.objects.link(arm)
bpy.context.view_layer.objects.active=arm;arm.select_set(True);bpy.ops.object.mode_set(mode='EDIT')
for name,(head,tail,parent) in joints.items():
    b=armdata.edit_bones.new(name);b.head=head;b.tail=tail
    if parent:b.parent=armdata.edit_bones[parent]
    b.use_deform=name!='Root'
bpy.ops.object.mode_set(mode='OBJECT');arm.show_in_front=True
names=list(joints);index={n:i for i,n in enumerate(names)}

def distance_segment(points,a,b):
    a=np.array(a);d=np.array(b)-a;t=np.clip(np.sum((points-a)*d,axis=1)/np.dot(d,d),0,1)
    return np.linalg.norm(points-(a+t[:,None]*d),axis=1)

def smoothstep(lo,hi,x):
    t=np.clip((x-lo)/(hi-lo),0,1);return t*t*(3-2*t)

base_image=bpy.data.images.get('BaseColor.png')
image_size=tuple(base_image.size);pixels=np.empty(image_size[0]*image_size[1]*4,dtype=np.float32);base_image.pixels.foreach_get(pixels);pixels=pixels.reshape(image_size[1],image_size[0],4)
reports=[]
for obj in meshes:
    bpy.context.view_layer.objects.active=obj
    obj.select_set(True);bpy.ops.object.transform_apply(location=True,rotation=True,scale=True);obj.select_set(False)
    me=obj.data;count=len(me.vertices);points=np.empty(count*3,dtype=np.float32);me.vertices.foreach_get('co',points);points=points.reshape(-1,3)
    x,y,z=points.T
    edge=np.empty(len(me.edges)*2,dtype=np.int32);me.edges.foreach_get('vertices',edge);edge=edge.reshape(-1,2)
    def main_island(mask):
        """A spatial band can include a nearby coat flap. Keep its anatomical island."""
        parents=np.arange(count,dtype=np.int32)
        def find(a):
            while parents[a]!=a:parents[a]=parents[parents[a]];a=int(parents[a])
            return a
        for a,b in edge[mask[edge[:,0]]&mask[edge[:,1]]]:
            ra,rb=find(int(a)),find(int(b))
            if ra!=rb:parents[ra]=rb
        ids=np.flatnonzero(mask)
        if not len(ids):return mask
        roots=np.array([find(int(i)) for i in ids]);keys,counts=np.unique(roots,return_counts=True);keep=keys[np.argmax(counts)]
        result=np.zeros(count,dtype=bool);result[ids[roots==keep]]=True;return result
    colors=np.zeros((count,3),dtype=np.float32)
    if me.uv_layers:
        uv=np.empty(len(me.loops)*2,dtype=np.float32);me.uv_layers.active.data.foreach_get('uv',uv);uv=uv.reshape(-1,2)
        vi=np.empty(len(me.loops),dtype=np.int32);me.loops.foreach_get('vertex_index',vi)
        px=np.clip((uv[:,0]*image_size[0]).astype(np.int32),0,image_size[0]-1);py=np.clip((uv[:,1]*image_size[1]).astype(np.int32),0,image_size[1]-1)
        colors[vi]=pixels[py,px,:3]
    weights=np.zeros((count,len(names)),dtype=np.float32)
    remaining=np.ones(count,dtype=bool)
    def rigid(mask,name):
        mask=mask&remaining;weights[mask,index[name]]=1;remaining[mask]=False
    def capsule(mask,bones,sharpness=3.5):
        mask=mask&remaining;ids=np.flatnonzero(mask)
        if not len(ids):return
        distances=np.stack([distance_segment(points[ids],joints[n][0],joints[n][1]) for n in bones],axis=1)
        values=1/np.maximum(.022,distances)**sharpness
        order=np.argsort(-values,axis=1);keep=np.zeros_like(values,dtype=bool);np.put_along_axis(keep,order[:,:min(3,len(bones))],True,axis=1)
        values=np.where(keep,values,0);values/=values.sum(axis=1,keepdims=True)
        for col,name in enumerate(bones):weights[ids,index[name]]=values[:,col]
        remaining[ids]=False
    # The curved saber is rigidly held by the hand; it never receives leg/coat weights.
    blade=(z>.63)&(z<1.34)&(y<-.28)&(distance_segment(points,(-.64,-.24,1.16),(.17,-.87,.71))<.18)
    guard=(x<-.48)&(x>-.85)&(y<-.12)&(z>1.06)&(z<1.27)
    rigid(main_island(blade|guard),'Sword')
    parrot=(x>.145)&(z>1.83)&(y>-.055)
    rigid(parrot,'Parrot')
    # Hat, plume, earrings and hair follow the head as a rigid sculpted assembly.
    rigid((z>2.075)|((z>1.88)&(y>.18)&(x<.18)&(x>-.47)),'Head')
    capsule((z>1.86)&(x>-.4)&(x<.22),['Chest','Neck','Head'],4)
    hand_r=distance_segment(points,joints['Hand.R'][0],joints['Hand.R'][1])<.115
    hand_l=distance_segment(points,joints['Hand.L'][0],joints['Hand.L'][1])<.125
    right_arm=main_island((x<-.44)&(z>1.09)&(z<1.91)&~((y>.25)&(z<1.43)))|hand_r
    left_arm=main_island((x>.215)&(z>.94)&(z<1.92)&((y>.035)|(z>1.55)))|hand_l
    capsule(right_arm,['Clavicle.R','UpperArm.R','Forearm.R','Hand.R'],4.5)
    capsule(left_arm,['Clavicle.L','UpperArm.L','Forearm.L','Hand.L'],4.5)
    red=(colors[:,0]>colors[:,1]*1.12)&(colors[:,0]>colors[:,2]*1.2)&(colors[:,0]>.055)
    coat=(z>.31)&(z<1.26)&(red|((y>.18)&(z>.42))|((np.abs(x+.15)>.33)&(z>.53)&(y>-.14)))&remaining
    # Include cream embroidery and hems connected to red fabric using a short
    # geodesic expansion, avoiding the nearby pants on another surface.
    distance=np.where(coat,0.0,np.inf)
    edge_length=np.linalg.norm(points[edge[:,0]]-points[edge[:,1]],axis=1)
    eligible=(z>.31)&(z<1.26)&remaining
    for iteration in range(8):
        candidate=distance.copy();np.minimum.at(candidate,edge[:,0],distance[edge[:,1]]+edge_length);np.minimum.at(candidate,edge[:,1],distance[edge[:,0]]+edge_length);distance=np.where(eligible,candidate,np.inf)
    coat=(distance<.035)&eligible
    ids=np.flatnonzero(coat)
    for vi in ids:
        name='CoatBack' if y[vi]>.24 and abs(x[vi]+.15)<.29 else ('Coat.R' if x[vi]<-.15 else 'Coat.L')
        w=float(smoothstep(1.22,.66,z[vi]))
        weights[vi,index[name]]=w;weights[vi,index['Hips']]=1-w
    remaining[ids]=False
    capsule((z<1.13)&(x<-.15),['Hips','Thigh.R','Shin.R','Foot.R','Toe.R'],4.2)
    capsule(z<1.13,['Hips','Thigh.L','Shin.L','Foot.L','Toe.L'],4.2)
    capsule(remaining,['Hips','Spine','Chest','Neck'],4)
    # Smooth weight transitions along actual mesh edges (not Euclidean nearby
    # surfaces), avoiding hard regional seams in the authored scan-like topology.
    source_ids=np.concatenate((edge[:,0],edge[:,1]));target_ids=np.concatenate((edge[:,1],edge[:,0]))
    degree=np.maximum(1,np.bincount(target_ids,minlength=count))[:,None]
    fixed_blade=(y<-.48)&blade
    for iteration in range(9):
        accumulated=np.zeros_like(weights);np.add.at(accumulated,target_ids,weights[source_ids]);weights=weights*.45+accumulated/degree*.55
        weights[fixed_blade]=0;weights[fixed_blade,index['Sword']]=1
    keep=np.argsort(-weights,axis=1)[:,:4];filtered=np.zeros_like(weights);np.put_along_axis(filtered,keep,np.take_along_axis(weights,keep,axis=1),axis=1);weights=filtered/filtered.sum(axis=1,keepdims=True)
    if np.max(np.abs(weights.sum(axis=1)-1))>1e-4:raise ValueError('Unweighted vertices')
    for g in list(obj.vertex_groups):obj.vertex_groups.remove(g)
    for col,name in enumerate(names):
        group=obj.vertex_groups.new(name=name)
        ids=np.flatnonzero(weights[:,col]>.00001)
        for vi in ids:group.add([int(vi)],float(weights[vi,col]),'REPLACE')
    obj.parent=arm;modifier=obj.modifiers.new('Corsair skin','ARMATURE');modifier.object=arm;modifier.use_deform_preserve_volume=False
    me.calc_loop_triangles();reports.append({'name':obj.name,'vertices':count,'triangles':len(me.loop_triangles),'weightedVertices':int(np.count_nonzero(weights.sum(axis=1)>0)),'maxInfluences':int((weights>.00001).sum(axis=1).max())})

# In-place clips from the shared pirate motion module; the simulation owns movement.
MOTION=importlib.util.spec_from_file_location('pirate_motion',str(Path(__file__).with_name('pirate_motion.py')))
motion=importlib.util.module_from_spec(MOTION);MOTION.loader.exec_module(motion)
captain={'role':'captain','sword_hand':'R','slash':{'arm_wind':35,'arm_cut':-45,'arm_open':0,'elbow_wind':-10,'elbow_cut':-10,'wrist_wind':25,'wrist_cut':5}}
lengths,speeds=motion.animate(arm,captain,next(o for o in meshes if 'LOD0' in o.name))
for label in lengths:bpy.data.actions['Corsair_'+label]['MotionType']='In place, simulation-owned movement'
scene.frame_start=1;scene.frame_end=91;scene.frame_set(1)
for obj in meshes:obj.hide_render='LOD0' not in obj.name
bpy.ops.file.pack_all();bpy.ops.wm.save_as_mainfile(filepath=str(WORK/'CrimsonCorsair.blend'),compress=True)
bpy.ops.object.select_all(action='DESELECT');arm.select_set(True)
for obj in meshes:obj.hide_set(False);obj.select_set(True)
bpy.context.view_layer.objects.active=arm
fbx=OUT/'CrimsonCorsair.fbx'
bpy.ops.export_scene.fbx(filepath=str(fbx),use_selection=True,object_types={'MESH','ARMATURE'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,bake_space_transform=False,add_leaf_bones=False,armature_nodetype='NULL',use_armature_deform_only=False,bake_anim=True,bake_anim_use_all_actions=True,bake_anim_use_nla_strips=False,bake_anim_simplify_factor=.0,path_mode='STRIP',embed_textures=False,use_mesh_modifiers=True)
manifest={'source':'Meshy user-supplied Crimson Corsair ZIP','sourceSha256':json.loads((WORK/'inspection.json').read_text())['sourceSha256'],'rig':'27-bone fitted generic skeleton, weighted posed source, no facial rig','heightMetres':2.5,'forward':'Blender -Y; FBX export -Z forward/Y up; Unity +Z front verified in the review scene','lods':reports,'animations':[{'name':'Corsair_'+name,'seconds':f/30,'loop':name in motion.LOOPS} for name,f in lengths.items()],'locomotion':{'walkMetresPerSecond':speeds['Walk'],'runMetresPerSecond':speeds['Run']},'fbxSha256':hashlib.sha256(fbx.read_bytes()).hexdigest()}
(ASSET/'model-manifest.json').write_text(json.dumps(manifest,indent=2),encoding='utf8')
review=ROOT/'Artifacts/ArtReview/crimson-corsair/source';review.mkdir(parents=True,exist_ok=True);shutil.copy2(WORK/'CrimsonCorsair.blend',review/'CrimsonCorsair.blend')
print('CORSAIR_RIG_COMPLETE '+json.dumps(manifest))

# Diagnostic real Blender renders of deformation, independent of Unity integration.
if '--preview' in sys.argv:
    scene.render.engine='CYCLES';scene.cycles.samples=16;scene.render.resolution_x=1200;scene.render.resolution_y=1500;scene.render.resolution_percentage=100
    world=bpy.data.worlds.new('Corsair preview');world.use_nodes=True;world.node_tree.nodes['Background'].inputs['Color'].default_value=(.3,.35,.42,1);world.node_tree.nodes['Background'].inputs['Strength'].default_value=.5;scene.world=world
    center=Vector((-.15,0,1.25))
    for name,loc,power,size in [('Key',(-3,-4,6),650,4),('Fill',(4,-1,4),350,4),('Rim',(1,4,5),750,3)]:
        data=bpy.data.lights.new(name,'AREA');obj=bpy.data.objects.new(name,data);scene.collection.objects.link(obj);obj.location=loc;obj.rotation_euler=(center-obj.location).to_track_quat('-Z','Y').to_euler();data.energy=power;data.size=size
    cd=bpy.data.cameras.new('Preview');cam=bpy.data.objects.new('Preview',cd);scene.collection.objects.link(cam);scene.camera=cam;cam.location=(-.15,-5,2.1);cam.rotation_euler=(center-cam.location).to_track_quat('-Z','Y').to_euler();cd.type='ORTHO';cd.ortho_scale=2.9
    for label,frame in [('Idle',1),('Walk',10),('Attack',11),('Death',45)]:
        arm.animation_data.action=bpy.data.actions['Corsair_'+label];scene.frame_set(frame);scene.render.filepath=str(WORK/('rig-'+label.lower()+'.png'));bpy.ops.render.render(write_still=True)
