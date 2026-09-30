"""Inspect the user-supplied Meshy FBX without altering the source archive."""
import bpy, json, zipfile, hashlib, math
from pathlib import Path
from mathutils import Vector

WORK=Path('D:/CodexTooling/crimson-corsair')
ZIP=Path('C:/Users/jacob/Downloads/Meshy_AI_Crimson_Corsair_Comma_0911070912_texture_fbx (9).zip')
SOURCE=WORK/'source'
with zipfile.ZipFile(ZIP) as archive:
    for entry in archive.infolist():
        target=(SOURCE/entry.filename).resolve()
        if not target.is_relative_to(SOURCE.resolve()): raise ValueError('Archive path leaves source directory')
        if not entry.is_dir():
            target.parent.mkdir(parents=True,exist_ok=True)
            if not target.exists(): target.write_bytes(archive.read(entry))
bpy.ops.wm.read_factory_settings(use_empty=True)
fbx=next(SOURCE.rglob('*.fbx'))
bpy.ops.import_scene.fbx(filepath=str(fbx))
objects=[]
for obj in bpy.context.scene.objects:
    item={'name':obj.name,'type':obj.type,'location':list(obj.location),'rotation':list(obj.rotation_euler),'scale':list(obj.scale)}
    if obj.type=='MESH':
        obj.data.calc_loop_triangles()
        item.update(vertices=len(obj.data.vertices),triangles=len(obj.data.loop_triangles),materials=[m.name for m in obj.data.materials],groups=[g.name for g in obj.vertex_groups],bounds=[list(obj.matrix_world@Vector(c)) for c in obj.bound_box])
    if obj.type=='ARMATURE': item['bones']=[{'name':b.name,'head':list(b.head_local),'tail':list(b.tail_local)} for b in obj.data.bones]
    objects.append(item)
manifest={'sourceZip':str(ZIP),'sourceSha256':hashlib.sha256(ZIP.read_bytes()).hexdigest(),'objects':objects,'actions':[a.name for a in bpy.data.actions],'images':[{'name':i.name,'size':list(i.size),'path':i.filepath} for i in bpy.data.images]}
(WORK/'inspection.json').write_text(json.dumps(manifest,indent=2),encoding='utf8')
bpy.ops.wm.save_as_mainfile(filepath=str(WORK/'imported.blend'),compress=True)
meshes=[o for o in bpy.context.scene.objects if o.type=='MESH']
points=[o.matrix_world@Vector(c) for o in meshes for c in o.bound_box]
low=Vector(tuple(min(v[i] for v in points) for i in range(3)))
high=Vector(tuple(max(v[i] for v in points) for i in range(3)))
center=(low+high)*.5
size=max(high-low)
scene=bpy.context.scene
scene.render.engine='CYCLES';scene.cycles.samples=16
scene.render.resolution_x=1100;scene.render.resolution_y=1400;scene.render.resolution_percentage=100
scene.world=bpy.data.worlds.new('Inspection world');scene.world.use_nodes=True
scene.world.node_tree.nodes['Background'].inputs['Color'].default_value=(.35,.38,.42,1)
scene.world.node_tree.nodes['Background'].inputs['Strength'].default_value=.5
for name,offset,power,scale in [('Key',(-2,-3,4),1000,3),('Fill',(3,-1,2),600,4),('Rim',(1,3,3),800,3)]:
    ld=bpy.data.lights.new(name,'AREA');lo=bpy.data.objects.new(name,ld);scene.collection.objects.link(lo)
    lo.location=center+Vector(offset)*size*.6;lo.rotation_euler=(center-lo.location).to_track_quat('-Z','Y').to_euler();ld.energy=power*size*size;ld.shape='DISK';ld.size=scale*size
camd=bpy.data.cameras.new('Inspect');cam=bpy.data.objects.new('Inspect',camd);scene.collection.objects.link(cam);scene.camera=cam
camd.type='ORTHO';camd.ortho_scale=size*1.25
for name,offset in [('front',(0,-3,.08)),('back',(0,3,.08)),('side',(3,0,.08))]:
    cam.location=center+Vector(offset)*size;cam.rotation_euler=(center-cam.location).to_track_quat('-Z','Y').to_euler();scene.render.filepath=str(WORK/(name+'.png'));bpy.ops.render.render(write_still=True)
print('CORSAIR_INSPECTION_COMPLETE '+json.dumps({'meshes':len(meshes),'actions':len(bpy.data.actions),'bounds':[list(low),list(high)]}))
