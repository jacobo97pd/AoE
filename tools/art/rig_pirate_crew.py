"""Fit authored rigs to the user's three posed Meshy pirates, retaining their UV atlas.

Run in Blender with --character NAME --preview. Anatomical landmarks and prop
capsules are recorded in pirate_crew_rigs.json in metres, Blender Z up/front -Y.
The source is not a T-pose; these are fitted Generic rigs, not mocap retargets.
"""
import bpy, bmesh, json, math, sys, shutil, hashlib, importlib.util
from pathlib import Path
import numpy as np
from mathutils import Vector, Quaternion

ROOT=Path(__file__).resolve().parents[2]
WORK=Path('D:/CodexTooling/pirate-crew')
ASSET=ROOT/'Assets/Game/PirateCrew'
CONFIG=json.loads((Path(__file__).with_name('pirate_crew_rigs.json')).read_text(encoding='utf8'))

def segment_distance(points,a,b):
    a=np.asarray(a);d=np.asarray(b)-a
    t=np.clip(np.sum((points-a)*d,axis=1)/max(float(np.dot(d,d)),1e-9),0,1)
    return np.linalg.norm(points-(a+t[:,None]*d),axis=1)

def vertex_atlas(obj,filename):
    """Average one source map over every UV corner of each vertex."""
    me=obj.data;count=len(me.vertices)
    image=next(n.image for n in me.materials[0].node_tree.nodes if n.type=='TEX_IMAGE' and n.image and n.image.filepath.endswith(filename))
    width,height=image.size
    pixels=np.empty(width*height*4,dtype=np.float32);image.pixels.foreach_get(pixels);pixels=pixels.reshape(height,width,4)
    uv=np.empty(len(me.loops)*2,dtype=np.float32);me.uv_layers.active.data.foreach_get('uv',uv);uv=uv.reshape(-1,2)
    indices=np.empty(len(me.loops),dtype=np.int32);me.loops.foreach_get('vertex_index',indices)
    texels=pixels[np.clip((uv[:,1]*height).astype(int),0,height-1),np.clip((uv[:,0]*width).astype(int),0,width-1),:3]
    values=np.zeros((count,3),dtype=np.float32);np.add.at(values,indices,texels)
    return values/np.maximum(1,np.bincount(indices,minlength=count))[:,None]

def components(edge,mask):
    """Connected-component labels of the vertices in mask; -1 elsewhere."""
    labels=np.arange(len(mask));inside=edge[mask[edge[:,0]]&mask[edge[:,1]]]
    while len(inside):
        low=np.minimum(labels[inside[:,0]],labels[inside[:,1]]);joined=labels.copy()
        np.minimum.at(joined,inside[:,0],low);np.minimum.at(joined,inside[:,1],low);joined=joined[joined]
        if np.array_equal(joined,labels):break
        labels=joined
    return np.where(mask,labels,-1)

def split_contact(me,weights,protected,part,box):
    """Split the edges joining part faces to other faces inside box. A seam vertex
    copy whose weights belong to the other side takes the mean of its face's
    undisputed vertices. Returns the new weights, protection, seam size and the
    original index of every vertex after the split."""
    lo,hi=np.asarray(box[0]),np.asarray(box[1])
    bm=bmesh.new();bm.from_mesh(me);bm.verts.ensure_lookup_table()
    origin=bm.verts.layers.int.new('RigOrigin');kind=bm.faces.layers.int.new('RigPart')
    for v in bm.verts:v[origin]=v.index
    for face in bm.faces:face[kind]=int(sum(bool(part[v.index]) for v in face.verts)*2>len(face.verts))
    def inside(v):return all(lo[i]<=v.co[i]<=hi[i] for i in range(3))
    seam=[e for e in bm.edges if len({f[kind] for f in e.link_faces})>1 and inside(e.verts[0]) and inside(e.verts[1])]
    bmesh.ops.split_edges(bm,edges=seam)
    bm.verts.ensure_lookup_table();bm.verts.index_update()
    origins=np.array([v[origin] for v in bm.verts],dtype=np.int32)
    duplicated=np.bincount(origins,minlength=len(part))[origins]>1
    new=weights[origins].copy();fixed=protected[origins].copy()
    total=np.zeros_like(new);hits=np.zeros(len(origins),dtype=np.int32)
    for face in bm.faces:
        side=bool(face[kind]);donors=[weights[v[origin]] for v in face.verts if bool(part[v[origin]])==side]
        if not donors:continue
        average=np.mean(donors,axis=0)
        for v in face.verts:
            if duplicated[v.index] and bool(part[v[origin]])!=side:total[v.index]+=average;hits[v.index]+=1
    moved=hits>0;new[moved]=total[moved]/hits[moved,None];fixed[moved]=False
    bm.verts.layers.int.remove(origin);bm.faces.layers.int.remove(kind)
    bm.to_mesh(me);bm.free();me.update()
    return new,fixed,len(seam),origins

def ease(a,b,t):
    t=np.clip((t-a)/(b-a),0,1);return t*t*(3-2*t)

def curve(t,keys):
    for (a,v),(b,w) in zip(keys,keys[1:]):
        if t<=b:return float(v+(w-v)*ease(a,b,t))
    return keys[-1][1]

def material():
    texdir=ASSET/'Textures';texdir.mkdir(parents=True,exist_ok=True)
    source=WORK/'source'
    for suffix,name in [('_texture.png','BaseColor.png'),('_texture_normal.png','Normal.png'),('_texture_metallic.png','Metallic.png'),('_texture_roughness.png','Roughness.png')]:
        src=next(p for p in source.rglob('*.png') if p.name.endswith(suffix))
        if not (texdir/name).exists():shutil.copy2(src,texdir/name)
    mat=bpy.data.materials.new('PirateCrew_Surface');mat.use_nodes=True
    nodes=mat.node_tree.nodes;nodes.clear();links=mat.node_tree.links
    out=nodes.new('ShaderNodeOutputMaterial');bsdf=nodes.new('ShaderNodeBsdfPrincipled');links.new(bsdf.outputs['BSDF'],out.inputs['Surface'])
    for filename,socket in [('BaseColor.png','Base Color'),('Metallic.png','Metallic'),('Roughness.png','Roughness')]:
        tex=nodes.new('ShaderNodeTexImage');tex.image=bpy.data.images.load(str(texdir/filename),check_existing=True)
        if socket!='Base Color':tex.image.colorspace_settings.name='Non-Color'
        links.new(tex.outputs['Color'],bsdf.inputs[socket])
    tex=nodes.new('ShaderNodeTexImage');tex.image=bpy.data.images.load(str(texdir/'Normal.png'));tex.image.colorspace_settings.name='Non-Color'
    normal=nodes.new('ShaderNodeNormalMap');normal.inputs['Strength'].default_value=.55
    links.new(tex.outputs['Color'],normal.inputs['Color']);links.new(normal.outputs['Normal'],bsdf.inputs['Normal'])
    return mat

def skin(obj,arm,cfg):
    joints=cfg['joints'];names=list(joints);index={n:i for i,n in enumerate(names)}
    me=obj.data;count=len(me.vertices)
    points=np.empty(count*3,dtype=np.float32);me.vertices.foreach_get('co',points);points=points.reshape(-1,3);x,y,z=points.T
    edge=np.empty(len(me.edges)*2,dtype=np.int32);me.edges.foreach_get('vertices',edge);edge=edge.reshape(-1,2)
    weights=np.zeros((count,len(names)),dtype=np.float32);remaining=np.ones(count,dtype=bool)
    protected=np.zeros(count,dtype=bool)
    # Each vertex remembers which assignment claimed it (a prop, an arm, a named
    # cloth part, a leg), so contact seams are cut between regions, not weights.
    region=np.zeros(count,dtype=np.int32);regions=['']
    def tag(ids,name):
        if name not in regions:regions.append(name)
        region[ids]=regions.index(name)
    split_props=[]
    sampled={}
    def atlas(filename):
        if filename not in sampled:sampled[filename]=vertex_atlas(obj,filename)
        return sampled[filename]
    def main_island(mask):
        parents=np.arange(count,dtype=np.int32)
        def find(a):
            while parents[a]!=a:parents[a]=parents[parents[a]];a=int(parents[a])
            return a
        for a,b in edge[mask[edge[:,0]]&mask[edge[:,1]]]:
            ra,rb=find(int(a)),find(int(b))
            if ra!=rb:parents[ra]=rb
        ids=np.flatnonzero(mask)
        if not len(ids):return mask
        roots=np.array([find(int(i)) for i in ids]);keys,counts=np.unique(roots,return_counts=True)
        out=np.zeros(count,dtype=bool);out[ids[roots==keys[np.argmax(counts)]]]=True;return out
    def rigid(mask,name,protect=False):
        mask=mask&remaining;weights[mask,index[name]]=1;remaining[mask]=False;tag(mask,name)
        if protect:protected[mask]=True
    def capsule(mask,bones,sharpness=4,name='Body'):
        ids=np.flatnonzero(mask&remaining)
        if not len(ids):return
        d=np.stack([segment_distance(points[ids],joints[n][0],joints[n][1]) for n in bones],axis=1)
        v=1/np.maximum(.02,d)**sharpness
        keep=np.argsort(-v,axis=1)[:,:3];filtered=np.zeros_like(v)
        np.put_along_axis(filtered,keep,np.take_along_axis(v,keep,axis=1),axis=1)
        filtered/=filtered.sum(axis=1,keepdims=True)
        for col,bone in enumerate(bones):weights[ids,index[bone]]=filtered[:,col]
        remaining[ids]=False;tag(ids,name)
    # Weapons, map and telescope must follow the hand rather than nearby faces or cloth.
    for prop in cfg.get('props',[]):
        mask=np.zeros(count,dtype=bool)
        for a,b,radius in prop['capsules']:mask|=segment_distance(points,a,b)<radius
        if 'box' in prop:
            lo,hi=np.array(prop['box']);mask|=np.all((points>=lo)&(points<=hi),axis=1)
        for axis,lo,hi in prop.get('limits',[]):mask&=(points[:,axis]>=lo)&(points[:,axis]<=hi)
        reach=mask.copy();excluded=np.zeros(count,dtype=bool)
        for lo,hi in prop.get('exclude_boxes',[]):excluded|=np.all((points>=np.array(lo))&(points<=np.array(hi)),axis=1)
        for normal,lo,hi,xmin,xmax in prop.get('plane_limits',[]):
            signed=points@np.asarray(normal)
            excluded|=(x>=xmin)&(x<=xmax)&((signed<lo)|(signed>hi))
        if 'exclude_color' in prop:
            # The red bandana and steel blade overlap spatially in the Meshy
            # source. The original UV atlas distinguishes the touching surfaces.
            color_rule=prop['exclude_color'];rgb=atlas('BaseColor.png');ratio=color_rule['red_ratio']
            lo,hi=np.array(color_rule['box'])
            color_region=np.all((points>=lo)&(points<=hi),axis=1)
            color_mask=color_region&(rgb[:,0]>ratio*rgb[:,1])&(rgb[:,0]>ratio*rgb[:,2])
            if color_rule.get('grow',0)>0:
                distance=np.where(color_mask,0.0,np.inf)
                edge_length=np.linalg.norm(points[edge[:,0]]-points[edge[:,1]],axis=1)
                for _ in range(16):
                    next_distance=distance.copy()
                    np.minimum.at(next_distance,edge[:,0],distance[edge[:,1]]+edge_length)
                    np.minimum.at(next_distance,edge[:,1],distance[edge[:,0]]+edge_length)
                    distance=np.where(color_region,next_distance,np.inf)
                color_mask|=(distance<color_rule['grow'])&color_region
            excluded|=color_mask
        if 'exclude_metallic_below' in prop:
            # Beside the face the blade is welded to cloth, beard and skin. The
            # source metal map separates them where colour alone misses dark hair.
            rule=prop['exclude_metallic_below'];lo,hi=np.array(rule['box'])
            excluded|=np.all((points>=lo)&(points<=hi),axis=1)&(atlas('Metallic.png')[:,0]<rule['value'])
        mask&=~excluded
        if prop.get('main',True):mask=main_island(mask&remaining)
        if prop.get('grow',0)>0:
            eligible=remaining&~excluded
            if 'grow_box' in prop:
                lo,hi=np.array(prop['grow_box']);eligible&=np.all((points>=lo)&(points<=hi),axis=1)
            distance=np.where(mask,0.0,np.inf)
            edge_length=np.linalg.norm(points[edge[:,0]]-points[edge[:,1]],axis=1)
            for _ in range(16):
                next_distance=distance.copy()
                np.minimum.at(next_distance,edge[:,0],distance[edge[:,1]]+edge_length)
                np.minimum.at(next_distance,edge[:,1],distance[edge[:,0]]+edge_length)
                distance=np.where(eligible,next_distance,np.inf)
            mask|=(distance<prop['grow'])&eligible
        if 'islands' in prop:
            # Texture thresholds leave specks on both sides of that seam: dark blade
            # texels read as cloth, studs and rings read as steel. Fill small holes
            # enclosed by the prop and drop small loose pieces inside the box.
            rule=prop['islands'];lo,hi=np.array(rule['box']);inbox=np.all((points>=lo)&(points<=hi),axis=1)
            size=max(1,int(mask.sum()));gap=reach&~mask;labels=components(edge,gap);a,b=edge[:,0],edge[:,1]
            leaks=np.unique(np.concatenate((labels[a][gap[a]&~reach[b]],labels[b][gap[b]&~reach[a]])))
            ids,sizes=np.unique(labels[gap],return_counts=True)
            filled=np.isin(labels,ids[(sizes<=rule['fill']*size)&~np.isin(ids,leaks)]);mask|=filled
            labels=components(edge,mask);ids,sizes=np.unique(labels[mask],return_counts=True)
            dropped=np.isin(labels,ids[(sizes<rule['drop']*size)&~np.isin(ids,np.unique(labels[mask&~inbox]))]);mask&=~dropped
            print('RIG_ISLANDS '+obj.name+' '+prop['bone']+' filled '+str(int(filled.sum()))+' dropped '+str(int(dropped.sum())),flush=True)
        mask&=remaining
        if prop.get('split',False):split_props.append((mask.copy(),prop['bone']))
        rigid(mask,prop['bone'],True)
    # The cap/hat and face retain their sculpted silhouette. Raised hands are handled
    # by excluding the corresponding arm capsule before this central head region.
    head=cfg['head'];headmask=(z>head['z'])&(x>head['xmin'])&(x<head['xmax'])
    for exclusion in head.get('exclude',[]):
        a,b,r=exclusion;headmask&=segment_distance(points,a,b)>r
    rigid(headmask,'Head')
    for side in ['R','L']:
        chain=['Clavicle.'+side,'UpperArm.'+side,'Forearm.'+side,'Hand.'+side]
        acfg=cfg['arms'][side]
        mask=np.zeros(count,dtype=bool)
        # Optional per-bone radii keep a slim forearm and fist off the holster or
        # pouch they hang beside while the muscular upper arm keeps its volume.
        radii=acfg.get('radii',[acfg['radius']]*3)
        for bone,radius in zip(chain[1:],radii):mask|=segment_distance(points,joints[bone][0],joints[bone][1])<radius
        if 'sleeve' in acfg:
            # A bracer, cuff or puffed sleeve stands further off the forearm bone than the
            # slim radius, and pieces left on the body tear open once the arm swings. Take
            # the whole sleeve from above the elbow to just past the wrist ('t' along the
            # forearm), except on the side facing the body, where the belt, holster or
            # vest it hangs against must stay put.
            rule=acfg['sleeve'];a,b=np.asarray(joints[chain[2]][0]),np.asarray(joints[chain[2]][1]);d=b-a
            t=((points-a)@d)/(d@d);offset=points-(a+np.clip(t,0,1)[:,None]*d);first,last=rule.get('t',[0,1])
            inward=np.asarray(joints['Hips'][0])-(points-offset);inward[:,2]=0
            inward/=np.maximum(np.linalg.norm(inward,axis=1,keepdims=True),1e-6)
            mask|=(np.linalg.norm(offset,axis=1)<rule['radius'])&(t>=first)&(t<=last)&((offset*inward).sum(axis=1)<rule['inward'])
        mask&=z>acfg['zmin']
        if 'xrange' in acfg:mask&=(x>acfg['xrange'][0])&(x<acfg['xrange'][1])
        if 'yrange' in acfg:mask&=(y>acfg['yrange'][0])&(y<acfg['yrange'][1])
        # Belts, pouches and coils touching a hanging hand stay on the body.
        for lo,hi in acfg.get('exclude_boxes',[]):
            mask&=~np.all((points>=np.asarray(lo))&(points<=np.asarray(hi)),axis=1)
        mask=main_island(mask&remaining)
        # A prop can divide a gripping hand from the arm's remaining surface.
        # Include that anatomical core even when it is a smaller mask island.
        hand='Hand.'+side
        mask|=segment_distance(points,joints[hand][0],joints[hand][1])<acfg.get('hand_radius',.115)
        capsule(mask,chain,4.5,'Arm.'+side)
    # Coats, belts and loose sashes move with the pelvis instead of stretching onto
    # the knees. Explicit regions avoid borrowing weights from nearby arm props.
    for part in cfg.get('cloth',[]):
        lo,hi=np.array(part['box']);mask=np.all((points>=lo)&(points<=hi),axis=1)&remaining
        if 'red_ratio' in part:
            # A sash shares its region with the trousers; the atlas tells the red cloth apart.
            rgb=atlas('BaseColor.png');mask&=(rgb[:,0]>part['red_ratio']*rgb[:,1])&(rgb[:,0]>part['red_ratio']*rgb[:,2])
        capsule(mask,part['bones'],part.get('sharpness',3.5),part.get('name','+'.join(part['bones'])))
    hips=cfg['hips_z'];split=cfg.get('leg_split_x',0)
    capsule((z<hips)&(x<split),['Hips','Thigh.R','Shin.R','Foot.R','Toe.R'],name='Leg.R')
    capsule(z<hips,['Hips','Thigh.L','Shin.L','Foot.L','Toe.L'],name='Leg.L')
    capsule(remaining,['Hips','Spine','Chest','Neck','Head'],name='Torso')
    if split_props:
        # Generated meshes sometimes weld a sword to the scarf it touches. Split
        # only the boundary edges, retaining every triangle and its UV corners.
        # Both surfaces can then move without rubber triangles between them.
        old_weights=weights;old_protected=protected
        bm=bmesh.new();bm.from_mesh(me);bm.verts.ensure_lookup_table()
        origin=bm.verts.layers.int.new('RigOrigin');kind=bm.faces.layers.int.new('RigPart')
        for v in bm.verts:v[origin]=v.index
        for face in bm.faces:
            face[kind]=0
            for part,(mask,bone) in enumerate(split_props,1):
                if sum(bool(mask[v[origin]]) for v in face.verts)>len(face.verts)/2:
                    face[kind]=part;break
        seam=[e for e in bm.edges if len({f[kind] for f in e.link_faces})>1]
        bmesh.ops.split_edges(bm,edges=seam)
        bm.verts.ensure_lookup_table();bm.verts.index_update()
        origins=np.array([v[origin] for v in bm.verts],dtype=np.int32)
        weights=old_weights[origins].copy();protected=old_protected[origins].copy();region=region[origins]
        body_sum=np.zeros_like(weights);body_count=np.zeros(len(origins),dtype=np.int32)
        split_bones=[index[bone] for _,bone in split_props]
        for face in bm.faces:
            part=face[kind]
            if part:
                bone_index=index[split_props[part-1][1]]
                for v in face.verts:
                    weights[v.index]=0;weights[v.index,bone_index]=1;protected[v.index]=True
            else:
                donors=[old_weights[v[origin]] for v in face.verts if old_weights[v[origin],split_bones].sum()<.5]
                if donors:
                    average=np.mean(donors,axis=0);average[split_bones]=0
                    average/=max(float(average.sum()),1e-9)
                    for v in face.verts:
                        if old_weights[v[origin],split_bones].sum()>.5:
                            body_sum[v.index]+=average;body_count[v.index]+=1
        reassigned=body_count>0
        weights[reassigned]=body_sum[reassigned]/body_count[reassigned,None];protected[reassigned]=False
        bm.verts.layers.int.remove(origin);bm.faces.layers.int.remove(kind)
        bm.to_mesh(me);bm.free();me.update()
        count=len(me.vertices)
        edge=np.empty(len(me.edges)*2,dtype=np.int32);me.edges.foreach_get('vertices',edge);edge=edge.reshape(-1,2)
        print('RIG_SEAM_SPLIT '+obj.name+' '+str(len(seam))+' edges',flush=True)
    for contact in cfg.get('contact_splits',[]):
        # Decimation welds hanging hands to holsters and pouches, and coat panels to
        # trousers. Cut those seams inside the contact box only, so the shoulder or
        # waistband outside it stays joined, before smoothing can blend the sides.
        part=np.isin(region,[regions.index(r) for r in contact['regions'] if r in regions])
        weights,protected,cut,origins=split_contact(me,weights,protected,part,contact['box']);region=region[origins]
        count=len(me.vertices);edge=np.empty(len(me.edges)*2,dtype=np.int32);me.edges.foreach_get('vertices',edge);edge=edge.reshape(-1,2)
        print('RIG_CONTACT_SPLIT '+obj.name+' '+'+'.join(contact['regions'])+' '+str(cut)+' edges',flush=True)
    src=np.concatenate((edge[:,0],edge[:,1]));dst=np.concatenate((edge[:,1],edge[:,0]))
    degree=np.maximum(1,np.bincount(dst,minlength=count))[:,None]
    fixed=weights[protected].copy()
    for _ in range(8):
        total=np.zeros_like(weights);np.add.at(total,dst,weights[src])
        weights=weights*.5+total/degree*.5;weights[protected]=fixed
    keep=np.argsort(-weights,axis=1)[:,:4];filtered=np.zeros_like(weights)
    np.put_along_axis(filtered,keep,np.take_along_axis(weights,keep,axis=1),axis=1)
    weights=filtered/filtered.sum(axis=1,keepdims=True)
    if not np.isfinite(weights).all() or np.max(np.abs(weights.sum(axis=1)-1))>1e-4:raise ValueError('Invalid weights')
    for group in list(obj.vertex_groups):obj.vertex_groups.remove(group)
    for col,name in enumerate(names):
        group=obj.vertex_groups.new(name=name)
        for vi in np.flatnonzero(weights[:,col]>.00001):group.add([int(vi)],float(weights[vi,col]),'REPLACE')
    obj.parent=arm;mod=obj.modifiers.new('Fitted pirate skin','ARMATURE');mod.object=arm
    me.calc_loop_triangles()
    print('SKINNED '+obj.name+' '+str(count),flush=True)
    return {'name':obj.name,'vertices':count,'triangles':len(me.loop_triangles),'weightedVertices':count,'maxInfluences':int((weights>.00001).sum(axis=1).max())}

MOTION=importlib.util.spec_from_file_location('pirate_motion',str(Path(__file__).with_name('pirate_motion.py')))
motion=importlib.util.module_from_spec(MOTION);MOTION.loader.exec_module(motion)

def animate(arm,cfg,detailed=None):
    """Author every clip with the shared motion module. Returns frames per clip and
    the measured ground speed of Walk and Run."""
    return motion.animate(arm,cfg,detailed)

def preview(name,arm,cfg):
    scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.samples=20
    scene.render.resolution_x=1100;scene.render.resolution_y=1400;scene.render.resolution_percentage=100
    world=bpy.data.worlds.new('Review');world.use_nodes=True;world.node_tree.nodes['Background'].inputs['Color'].default_value=(.3,.35,.42,1);world.node_tree.nodes['Background'].inputs['Strength'].default_value=.5;scene.world=world
    center=Vector((0,0,1.17))
    for title,loc,power in [('Key',(-3,-4,5),650),('Fill',(4,-1,4),300),('Rim',(1,4,5),650)]:
        data=bpy.data.lights.new(title,'AREA');obj=bpy.data.objects.new(title,data);scene.collection.objects.link(obj);obj.location=loc;obj.rotation_euler=(center-obj.location).to_track_quat('-Z','Y').to_euler();data.energy=power;data.size=4
    cd=bpy.data.cameras.new('Review');cam=bpy.data.objects.new('Review',cd);scene.collection.objects.link(cam);scene.camera=cam;cam.location=(0,-5,2.0);cam.rotation_euler=(center-cam.location).to_track_quat('-Z','Y').to_euler();cd.type='ORTHO';cd.ortho_scale=2.95
    poses=[('Idle',1),('Walk',10),('Attack',17 if cfg['role']=='gunner' else 13),('Death',45)]
    if cfg['role']=='seeker':poses.append(('Work',28))
    for state,frame in poses:
        arm.animation_data.action=bpy.data.actions['Corsair_'+state];scene.frame_set(frame)
        scene.render.filepath=str(WORK/(name+'-'+state.lower()+'.png'));bpy.ops.render.render(write_still=True)
    if cfg['role']=='raider':
        for view,location in [('quarter',(-3,-5,2.5)),('back',(3,5,2.6))]:
            cam.location=location;cam.rotation_euler=(center-cam.location).to_track_quat('-Z','Y').to_euler()
            for state,frame in [('Attack',13),('Attack',19),('Death',48)]:
                arm.animation_data.action=bpy.data.actions['Corsair_'+state];scene.frame_set(frame)
                scene.render.filepath=str(WORK/(name+'-'+view+'-'+state.lower()+'-'+str(frame)+'.png'));bpy.ops.render.render(write_still=True)

def build(name):
    manifest=ASSET/name/'model-manifest.json'
    if manifest.exists() and json.loads(manifest.read_text(encoding='utf8')).get('pipeline')=='MeshyBiped':
        print('PRESERVE '+name+': use tools/art/import_meshy_raider.py for the supplied biped and animations.',flush=True)
        return
    cfg=CONFIG[name];bpy.ops.wm.open_mainfile(filepath=str(WORK/(name+'-prepared.blend')))
    scene=bpy.context.scene;scene.render.fps=30
    meshes=[o for o in scene.objects if o.type=='MESH' and '_LOD' in o.name]
    if len(meshes)!=3:raise ValueError('Expected three prepared LOD meshes')
    for obj in list(scene.objects):
        if obj not in meshes:bpy.data.objects.remove(obj,do_unlink=True)
    mat=material()
    for obj in meshes:
        obj.hide_set(False);obj.select_set(False);obj.data.materials.clear();obj.data.materials.append(mat)
    armdata=bpy.data.armatures.new(name+'_Skeleton');arm=bpy.data.objects.new(name+'Rig',armdata);scene.collection.objects.link(arm)
    bpy.context.view_layer.objects.active=arm;arm.select_set(True);bpy.ops.object.mode_set(mode='EDIT')
    for key,(head,tail,parent) in cfg['joints'].items():
        bone=armdata.edit_bones.new(key);bone.head=head;bone.tail=tail
        if parent:bone.parent=armdata.edit_bones[parent]
        bone.use_deform=key!='Root'
    bpy.ops.object.mode_set(mode='OBJECT');arm.show_in_front=True
    reports=[skin(obj,arm,cfg) for obj in meshes]
    extras=[]
    if 'muzzle' in cfg:
        muzzle=bpy.data.objects.new('Muzzle',None);scene.collection.objects.link(muzzle)
        muzzle.parent=arm;muzzle.parent_type='BONE';muzzle.parent_bone=cfg['muzzle']['bone']
        bpy.context.view_layer.update()
        from mathutils import Matrix
        direction=Vector(cfg['muzzle']['direction']).normalized()
        muzzle.matrix_world=Matrix.Translation(Vector(cfg['muzzle']['position']))@direction.to_track_quat('Z','Y').to_matrix().to_4x4()
        extras.append(muzzle)
    lengths,speeds=animate(arm,cfg,next(o for o in meshes if o.name.endswith('_LOD0')));scene.frame_start=1;scene.frame_end=91
    for obj in meshes:obj.hide_render=not obj.name.endswith('_LOD0')
    bpy.ops.file.pack_all();out=WORK/(name+'.blend');bpy.ops.wm.save_as_mainfile(filepath=str(out),compress=True)
    review=ROOT/'Artifacts/ArtReview/pirate-crew/source';review.mkdir(parents=True,exist_ok=True);shutil.copy2(out,review/out.name)
    bpy.ops.object.select_all(action='DESELECT');arm.select_set(True)
    for obj in meshes+extras:obj.hide_set(False);obj.select_set(True)
    bpy.context.view_layer.objects.active=arm
    folder=ASSET/name/'Model';folder.mkdir(parents=True,exist_ok=True);fbx=folder/(name+'.fbx')
    bpy.ops.export_scene.fbx(filepath=str(fbx),use_selection=True,object_types={'MESH','ARMATURE','EMPTY'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,bake_space_transform=False,add_leaf_bones=False,armature_nodetype='NULL',use_armature_deform_only=False,bake_anim=True,bake_anim_use_all_actions=True,bake_anim_use_nla_strips=False,bake_anim_simplify_factor=0,path_mode='STRIP',embed_textures=False,use_mesh_modifiers=True)
    record={'name':name,'sourceSha256':json.loads((WORK/'inspection.json').read_text())['sourceSha256'],'fbxSha256':hashlib.sha256(fbx.read_bytes()).hexdigest(),'bones':len(cfg['joints']),'heightMetres':2.35,'rig':'Fitted Generic rig on posed Meshy source; no facial rig','lods':reports,'animations':[{'name':'Corsair_'+label,'seconds':frames/30,'loop':label in motion.LOOPS} for label,frames in lengths.items()],'locomotion':{'walkMetresPerSecond':speeds['Walk'],'runMetresPerSecond':speeds['Run']}}
    (ASSET/name/'model-manifest.json').write_text(json.dumps(record,indent=2),encoding='utf8')
    print('CREW_RIG_OK '+json.dumps(record),flush=True)
    if '--preview' in sys.argv:preview(name,arm,cfg)

if __name__=='__main__':
    args=sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else []
    names=[args[args.index('--character')+1]] if '--character' in args else list(CONFIG)
    for name in names:build(name)
