"""Measure how one rigged pirate deforms: skin its LOD0 with rig_pirate_crew.py,
count stretched edges in every clip and render review close-ups into one sheet.

Run in Blender:
  blender --background --python audit_pirate_crew.py -- LABEL [--character NAME]
      [--patch FILE] [--motion] [--diagnose] [--norender] [--report PATH]

A welded seam that is cut by the rig never registers as stretch; only rubber
triangles do. --patch executes a Python file with `cfg` bound to the character's
configuration, so candidate masks can be compared without editing the JSON.
"""
import bpy,sys,json,time,importlib.util
import numpy as np
from pathlib import Path
from mathutils import Vector

start=time.time()
args=sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else []
label=args[0] if args and not args[0].startswith('--') else 'audit'
name=args[args.index('--character')+1] if '--character' in args else 'BoardingRaider'
spec=importlib.util.spec_from_file_location('pirate_rig',str(Path(__file__).with_name('rig_pirate_crew.py')))
rig=importlib.util.module_from_spec(spec);spec.loader.exec_module(rig)
OUT=rig.WORK/'audit'/label;OUT.mkdir(parents=True,exist_ok=True)
cfg=rig.CONFIG[name]
if '--patch' in args:exec(Path(args[args.index('--patch')+1]).read_text(encoding='utf8'),{'cfg':cfg,'np':np})

bpy.ops.wm.open_mainfile(filepath=str(rig.WORK/(name+'-prepared.blend')))
scene=bpy.context.scene;scene.render.fps=30
lod0=next(o for o in scene.objects if o.type=='MESH' and o.name.endswith('_LOD0'))
for o in list(scene.objects):
    if o is not lod0:bpy.data.objects.remove(o,do_unlink=True)
lod0.hide_set(False);lod0.data.materials.clear();lod0.data.materials.append(rig.material())
ad=bpy.data.armatures.new(name+'_Skeleton');arm=bpy.data.objects.new(name+'Rig',ad);scene.collection.objects.link(arm)
bpy.context.view_layer.objects.active=arm;arm.select_set(True);bpy.ops.object.mode_set(mode='EDIT')
for key,(head,tail,parent) in cfg['joints'].items():
    bone=ad.edit_bones.new(key);bone.head=head;bone.tail=tail
    if parent:bone.parent=ad.edit_bones[parent]
    bone.use_deform=key!='Root'
bpy.ops.object.mode_set(mode='OBJECT')
rig.skin(lod0,arm,cfg);lengths,_=rig.animate(arm,cfg,lod0)

# Rubber triangles: edges much longer than at rest, sampled every other frame.
me=lod0.data;count=len(me.vertices)
rest=np.empty(count*3,dtype=np.float32);me.vertices.foreach_get('co',rest);rest=rest.reshape(-1,3)
edge=np.empty(len(me.edges)*2,dtype=np.int32);me.edges.foreach_get('vertices',edge);edge=edge.reshape(-1,2)
rest_length=np.linalg.norm(rest[edge[:,0]]-rest[edge[:,1]],axis=1);valid=rest_length>2e-4
def pose(clip,frame):
    arm.animation_data.action=bpy.data.actions['Corsair_'+clip];scene.frame_set(frame);bpy.context.view_layer.update()
def deformed():
    ev=lod0.evaluated_get(bpy.context.evaluated_depsgraph_get());m=ev.to_mesh()
    p=np.empty(len(m.vertices)*3,dtype=np.float32);m.vertices.foreach_get('co',p);ev.to_mesh_clear()
    world=np.array(lod0.matrix_world);return p.reshape(-1,3)@world[:3,:3].T+world[:3,3]
def stretch(clip,frame):
    pose(clip,frame);p=deformed()
    return np.where(valid,np.linalg.norm(p[edge[:,0]]-p[edge[:,1]],axis=1)/np.maximum(rest_length,1e-6),1.0)
report={'label':label,'character':name,'lod':lod0.name,'vertices':count,'edges':len(edge),
        'measure':'Worst sampled frame per clip: edges stretched beyond 1.5x and 2.5x their rest length, the largest ratio, and the densest 6 cm cells of edges beyond 1.8x (rest-pose metres).','clips':{}}
for clip,frames in lengths.items():
    worst=None
    for frame in range(1,frames+2,2):
        ratio=stretch(clip,frame);over=int((ratio>1.5).sum())
        if worst is None or over>worst['over1.5']:
            bad=ratio>1.8;clusters=[]
            if bad.any():
                middle=(rest[edge[bad,0]]+rest[edge[bad,1]])/2
                cells,counts=np.unique(np.floor(middle/.06).astype(int),axis=0,return_counts=True)
                clusters=[[*[round(float(c),2) for c in (cells[i]+.5)*.06],int(counts[i])] for i in np.argsort(-counts)[:6]]
            worst={'frame':frame,'over1.5':over,'over2.5':int((ratio>2.5).sum()),'max':round(float(ratio.max()),2),'clusters':clusters}
    report['clips'][clip]=worst;print('STRETCH',clip,json.dumps(worst),flush=True)

if '--diagnose' in args:
    # Which bone regions do the rubber edges join and how long were they at rest?
    groups=[g.name for g in lod0.vertex_groups];weights=np.zeros((count,len(groups)),dtype=np.float32)
    for v in me.vertices:
        for g in v.groups:weights[v.index,g.group]=g.weight
    dominant=np.argmax(weights,axis=1)
    for clip in lengths:
        frame=report['clips'][clip]['frame'];bad=np.flatnonzero(stretch(clip,frame)>3);pairs={}
        for i in bad:
            key=' / '.join(sorted((groups[dominant[edge[i,0]]],groups[dominant[edge[i,1]]])));pairs[key]=pairs.get(key,0)+1
        print('DIAG',clip,frame,len(bad),sorted(pairs.items(),key=lambda kv:-kv[1])[:8],
              'restmm',(np.percentile(rest_length[bad],[5,50,95])*1000).round(1).tolist() if len(bad) else [],flush=True)
def finish():
    text=json.dumps(report,indent=1);(OUT/'report.json').write_text(text,encoding='utf8')
    if '--report' in args:Path(args[args.index('--report')+1]).write_text(text,encoding='utf8')
    print('AUDIT_OK',label,round(time.time()-start,1),flush=True)
if '--norender' in args:
    finish();sys.exit(0)

scene.render.engine='BLENDER_EEVEE_NEXT';T=480
scene.render.resolution_x=T;scene.render.resolution_y=T;scene.render.resolution_percentage=100
world=bpy.data.worlds.new('Audit');world.use_nodes=True;bg=world.node_tree.nodes['Background'];bg.inputs[0].default_value=(.3,.35,.42,1);bg.inputs[1].default_value=.5;scene.world=world
centre=Vector((0,0,1.17))
for title,loc,power in [('Key',(-3,-4,5),650),('Fill',(4,-1,4),300),('Rim',(1,4,5),650)]:
    data=bpy.data.lights.new(title,'AREA');light=bpy.data.objects.new(title,data);scene.collection.objects.link(light)
    light.location=loc;light.rotation_euler=(centre-light.location).to_track_quat('-Z','Y').to_euler();data.energy=power;data.size=4
cd=bpy.data.cameras.new('Audit');cam=bpy.data.objects.new('Audit',cd);scene.collection.objects.link(cam);scene.camera=cam;cd.type='ORTHO';cd.clip_end=40
views={'q':(-3,-5,1.33),'b':(3,5,1.43),'bl':(-3,5,1.43),'f':(0,-1,.12),'top':(-.35,-.55,1.1),'r':(-1,-.25,.15),'l':(1,-.25,.15)}
def shot(tag,clip,frame,view,target=None,scale=None):
    pose(clip,frame)
    if target:focus=arm.matrix_world@arm.pose.bones[target].head
    else:
        p=deformed();lo,hi=p.min(axis=0),p.max(axis=0);focus=Vector(((lo+hi)/2).tolist());scale=float((hi-lo).max())*1.1
    d=Vector(views[view]).normalized();cam.location=focus+d*8;cam.rotation_euler=(-d).to_track_quat('-Z','Y').to_euler();cd.ortho_scale=scale
    path=OUT/(tag+'.png');scene.render.filepath=str(path);bpy.ops.render.render(write_still=True);return path
if '--motion' in args:
    # Locomotion, reactions and the corpse from every side: cloth, legs and hands.
    shots=[('b-run7','Run',7,'b'),('b-run19','Run',19,'b'),('l-run19','Run',19,'l'),('r-run7','Run',7,'r'),
           ('q-walk10','Walk',10,'q'),('q-walk27','Walk',27,'q'),('q-hit5','Hit',5,'q'),('top-death','Death',lengths['Death']+1,'top')]
elif cfg['role']=='raider':
    shots=[('q-atk13','Attack',13,'q'),('q-atk19','Attack',19,'q'),('b-atk13','Attack',13,'b'),('b-atk19','Attack',19,'b'),
           ('hb-atk7','Attack',7,'b','Head',.8),('hb-atk13','Attack',13,'b','Head',.8),('hb-atk19','Attack',19,'b','Head',.8),('hbl-atk19','Attack',19,'bl','Head',.8),
           ('hq-atk19','Attack',19,'q','Head',.8),('hq-atk24','Attack',24,'q','Head',.8),('hb-hit8','Hit',8,'b','Head',.8),('hq-run7','Run',7,'q','Head',.8),
           ('top-death49','Death',49,'top'),('hd-death49','Death',49,'top','Head',.9),('rq-atk16','Attack',16,'q','Hand.R',.7),('rr-death40','Death',40,'r','Hand.R',.9)]
else:
    shots=[('f-idle1','Idle',1,'f'),('q-atk','Attack',lengths['Attack']//2,'q'),('b-walk10','Walk',10,'b'),('top-death','Death',lengths['Death']+1,'top'),
           ('hq-idle1','Idle',1,'q','Head',.8),('hq-hit8','Hit',8,'q','Head',.8),('hb-run7','Run',7,'b','Head',.8)]
    if 'Work' in lengths:shots.append(('q-work30','Work',30,'q'))
paths=[shot(*s) for s in shots]
cols=4;rows=(len(paths)+cols-1)//cols;sheet=np.ones((rows*T,cols*T,4),dtype=np.float32)
for i,path in enumerate(paths):
    image=bpy.data.images.load(str(path));pixels=np.empty(T*T*4,dtype=np.float32);image.pixels.foreach_get(pixels)
    r=rows-1-i//cols;c=i%cols;sheet[r*T:(r+1)*T,c*T:(c+1)*T]=pixels.reshape(T,T,4);bpy.data.images.remove(image)
image=bpy.data.images.new('Audit sheet',cols*T,rows*T,alpha=True);image.pixels.foreach_set(sheet.ravel())
image.filepath_raw=str(OUT/'sheet.png');image.file_format='PNG';image.save()
report['shots']=[s[0] for s in shots];finish()
