"""Prepare the supplied Meshy biped for BoardingRaider only.

Default: scratch blend, six-clip previews and manifest. `--export` additionally
writes this pirate's FBX and own PBR textures, retaining existing Unity .meta GUIDs.
Input: D:/CodexTooling/pirate-animation-replacement/prepared/BoardingRaider-Meshy.blend.
Run/Death are supplied animation curves; Idle/Walk/Hit/Attack are authored fallback
motion on the supplied rig. The fallback attack is a short body/hand strike;
the supplied holstered sword stays on its belt and is not drawn.
"""
import bpy, json, math, hashlib, shutil, sys
from pathlib import Path
from mathutils import Matrix, Quaternion, Vector

ROOT=Path(__file__).resolve().parents[2]
WORK=Path('D:/CodexTooling/pirate-animation-replacement')
INPUT=WORK/'prepared/BoardingRaider-Meshy.blend'
OUT=WORK/'ready';OUT.mkdir(exist_ok=True)
ASSET=ROOT/'Assets/Game/PirateCrew/BoardingRaider'
FPS=24

def digest(path):return hashlib.sha256(Path(path).read_bytes()).hexdigest()
def set_action(arm,action):
    arm.animation_data.action=action
    if action and len(action.slots):arm.animation_data.action_slot=action.slots[0]
def update():bpy.context.view_layer.update()
def rotation_at(arm,name,axis,angle):
    bone=arm.pose.bones[name];q=Quaternion(Vector(axis),angle)@bone.matrix.to_quaternion()
    bone.matrix=Matrix.Translation(bone.head)@q.to_matrix().to_4x4();update()
def aim(arm,name,target):
    bone=arm.pose.bones[name];old=bone.tail-bone.head;new=Vector(target)-bone.head
    if old.length<1e-6 or new.length<1e-6:return
    q=old.normalized().rotation_difference(new.normalized())@bone.matrix.to_quaternion()
    bone.matrix=Matrix.Translation(bone.head)@q.to_matrix().to_4x4();update()
def ik(arm,upper,lower,target,pole):
    a=arm.pose.bones[upper].head.copy();target=Vector(target);axis=target-a
    l1=arm.data.bones[upper].length;l2=arm.data.bones[lower].length
    distance=max(abs(l1-l2)+.002,min(axis.length,l1+l2-.002));direction=axis.normalized()
    target=a+direction*distance;v=Vector(pole)-a;v-=direction*v.dot(direction)
    if v.length<1e-6:v=Vector((0,-1,0))
    v.normalize();c=max(-1,min(1,(l1*l1+distance*distance-l2*l2)/(2*l1*distance)))
    elbow=a+direction*l1*c+v*l1*math.sqrt(max(0,1-c*c))
    aim(arm,upper,elbow);aim(arm,lower,target)
def evaluated_bounds(obj):
    ob=obj.evaluated_get(bpy.context.evaluated_depsgraph_get());m=ob.to_mesh()
    points=[ob.matrix_world@v.co for v in m.vertices];ob.to_mesh_clear()
    return [min(v[i] for v in points) for i in range(3)],[max(v[i] for v in points) for i in range(3)]
def feet_floor(obj):
    ids={g.index for g in obj.vertex_groups if 'Foot' in g.name or 'Toe' in g.name}
    indices=[v.index for v in obj.data.vertices if sum(g.weight for g in v.groups if g.group in ids)>.65]
    ob=obj.evaluated_get(bpy.context.evaluated_depsgraph_get());m=ob.to_mesh()
    floor=min((ob.matrix_world@m.vertices[i].co).z for i in indices);ob.to_mesh_clear();return floor
def curve_digest(action):
    record=[(fc.data_path,fc.array_index,[(tuple(k.co),tuple(k.handle_left),tuple(k.handle_right),k.interpolation) for k in fc.keyframe_points]) for fc in action.fcurves]
    return hashlib.sha256(repr(record).encode()).hexdigest()

def measure_locomotion(arm,mesh,action):
    """Median backwards sole speed during low, near-horizontal stance samples."""
    scene=bpy.context.scene;set_action(arm,action);arm.pose.bones['Root'].matrix_basis=Matrix.Identity(4)
    soles={}
    for side in ['Left','Right']:
        groups={g.index for g in mesh.vertex_groups if g.name.startswith(side) and ('Foot' in g.name or 'Toe' in g.name)}
        candidates=[v for v in mesh.data.vertices if sum(g.weight for g in v.groups if g.group in groups)>.65]
        low=min(v.co.z for v in candidates);soles[side]=[v.index for v in candidates if v.co.z<low+.035]
    first,last=action.frame_range;intervals=round((last-first)/FPS*120);records=[]
    for sample in range(intervals+1):
        f=first+(last-first)*sample/intervals;scene.frame_set(int(f),subframe=f-int(f));update()
        ob=mesh.evaluated_get(bpy.context.evaluated_depsgraph_get());m=ob.to_mesh()
        entry={'time':(f-first)/FPS}
        for side,indices in soles.items():entry[side]=sum((ob.matrix_world@m.vertices[i].co for i in indices),Vector())/len(indices)
        records.append(entry);ob.to_mesh_clear()
    floor=min(row[side].z for row in records for side in soles);velocities=[];contacts=[]
    for i in range(1,len(records)-1):
        previous,current,following=records[i-1:i+2];dt=following['time']-previous['time']
        for side in soles:
            velocity=(following[side]-previous[side])/dt
            if current[side].z<floor+.06 and abs(velocity.z)<.65 and velocity.y>.2:
                velocities.append(float(velocity.y));contacts.append({'time':current['time'],'side':side,'backwardSpeed':float(velocity.y),'soleHeight':float(current[side].z),'verticalSpeed':float(velocity.z)})
    if not velocities:raise RuntimeError('No reliable stance samples for '+action.name)
    values=sorted(velocities);median=values[len(values)//2]
    return {'metresPerSecond':median,'method':'Median positive Blender-Y sole velocity; sole <= minimum+.06m and abs(verticalVelocity)<.65m/s, sampled at120Hz','stanceSamples':len(contacts),'min':min(values),'max':max(values),'contactSamples':contacts}

def main():
    bpy.ops.wm.open_mainfile(filepath=str(INPUT));scene=bpy.context.scene
    arm=next(o for o in scene.objects if o.type=='ARMATURE');arm.name='BoardingRaider_Rig'
    meshes=sorted([o for o in scene.objects if o.type=='MESH'],key=lambda o:o.name)
    for i,obj in enumerate(meshes):obj.name='BoardingRaider_LOD'+str(i);obj.hide_set(i!=0);obj.hide_render=i!=0
    for obj in meshes[1:]:
        for vertex in obj.data.vertices:
            weights=sorted([(g.group,g.weight) for g in vertex.groups],key=lambda x:x[1],reverse=True)
            for group,weight in weights[4:]:obj.vertex_groups[group].remove([vertex.index])
            total=sum(weight for group,weight in weights[:4])
            for group,weight in weights[:4]:obj.vertex_groups[group].add([vertex.index],weight/total,'REPLACE')
    mesh=meshes[0];mesh.data.materials[0].name='BoardingRaider_Meshy_PBR'
    run=bpy.data.actions['Meshy_Running'];death=bpy.data.actions['Meshy_Death'];run.name='Corsair_Run';death.name='Corsair_Death'
    original={a.name:curve_digest(a) for a in [run,death]}
    for action in [run,death]:
        for slot in action.slots:slot.name_display=arm.name
    set_action(arm,death);scene.frame_set(1);update()
    # The first injury frame is a useful lowered-arm stance, but its gaze is down.
    # Restore the bind-pose gaze only for the four authored fallback clips.
    for name in ['neck','Head']:
        bone=arm.pose.bones[name];bone.matrix=Matrix.Translation(bone.head)@bone.bone.matrix_local.to_quaternion().to_matrix().to_4x4();update()
    neutral={b.name:b.matrix_basis.copy() for b in arm.pose.bones}
    foot_positions={s:arm.pose.bones[s+'Foot'].head.copy() for s in ['Right','Left']}
    foot_rotations={s:arm.pose.bones[s+'Foot'].matrix.to_quaternion().copy() for s in ['Right','Left']}
    hand_positions={s:arm.pose.bones[s+'Hand'].head.copy() for s in ['Right','Left']}
    actions={'Run':run,'Death':death};scene.render.fps=FPS;scene.render.fps_base=1
    lengths={'Idle':72,'Walk':24,'Hit':12,'Attack':24}
    fallback_floors={}
    for state,intervals in lengths.items():
        action=bpy.data.actions.new('Corsair_'+state);action.use_fake_user=True;set_action(arm,action);floors=[]
        for frame in range(intervals+1):
            scene.frame_set(frame+1)
            for bone in arm.pose.bones:bone.matrix_basis=neutral[bone.name]
            update();t=frame/intervals;wave=math.sin(math.tau*t)
            if state=='Idle':
                rotation_at(arm,'Spine01',(1,0,0),.012*wave)
                rotation_at(arm,'Spine',(0,1,0),.008*math.sin(math.tau*t))
                rotation_at(arm,'Head',(0,0,1),.025*math.sin(math.tau*t))
            elif state=='Walk':
                hips=arm.pose.bones['Hips'];m=hips.matrix.copy();m.translation.z+=.015*(1-math.cos(2*math.tau*t));hips.matrix=m;update()
                rotation_at(arm,'Spine01',(0,0,1),.045*wave)
                for side,offset,sign in [('Right',0,-1),('Left',math.pi,1)]:
                    p=math.tau*t+offset;target=foot_positions[side].copy();target.y-=.27*math.cos(p);target.z+=.09*max(0,-math.sin(p))
                    hip=arm.pose.bones[side+'UpLeg'].head
                    ik(arm,side+'UpLeg',side+'Leg',target,(hip.x,hip.y-.7,hip.z-.3))
                    foot=arm.pose.bones[side+'Foot'];foot.matrix=Matrix.Translation(foot.head)@foot_rotations[side].to_matrix().to_4x4();update()
                    rotation_at(arm,side+'Arm',(1,0,0),sign*.16*wave)
            elif state=='Hit':
                amount=math.sin(math.pi*t)**2
                rotation_at(arm,'Spine01',(1,0,0),.18*amount)
                rotation_at(arm,'Head',(1,0,0),.14*amount)
                rotation_at(arm,'RightArm',(1,0,0),-.12*amount)
                rotation_at(arm,'LeftArm',(1,0,0),-.12*amount)
            elif state=='Attack':
                wind=math.sin(math.pi*min(1,t/.35)) if t<.35 else 0
                strike=max(0,1-abs(t-.46)/.25);strike=strike*strike*(3-2*strike)
                rotation_at(arm,'Spine01',(0,0,1),-.07*wind+.16*strike)
                rotation_at(arm,'Spine',(1,0,0),-.04*strike)
                shoulder=arm.pose.bones['RightArm'].head.copy()
                wrist=hand_positions['Right'].lerp(shoulder+Vector((-.035,-.51,-.18)),strike)
                wrist.y+=.08*wind;wrist.z+=.10*wind
                ik(arm,'RightArm','RightForeArm',wrist,shoulder+Vector((-.65,-.08,-.22)))
                rotation_at(arm,'LeftArm',(1,0,0),-.08*strike)
            floor=feet_floor(mesh);arm.pose.bones['Root'].location.z+=.006-floor;update();floors.append(feet_floor(mesh))
            for bone in arm.pose.bones:
                bone.rotation_mode='QUATERNION'
                for path in ['location','rotation_quaternion','scale']:bone.keyframe_insert(data_path=path,frame=frame+1,group=bone.name)
        for fc in action.fcurves:
            for key in fc.keyframe_points:key.interpolation='LINEAR'
        for slot in action.slots:slot.name_display=arm.name
        actions[state]=action;fallback_floors[state]={'min':min(floors),'max':max(floors)}
    assert all(curve_digest(a)==original[a.name] for a in [run,death]),'Supplied animation curves changed'
    # Key only the NEW identity Root so FBX's all-action exporter cannot inherit
    # a fallback contact lift into the otherwise unchanged supplied curves.
    for action in [run,death]:
        set_action(arm,action);root_bone=arm.pose.bones['Root'];root_bone.matrix_basis=Matrix.Identity(4)
        for frame in action.frame_range:
            for path in ['location','rotation_quaternion','scale']:root_bone.keyframe_insert(data_path=path,frame=frame,group='Root')
    texture_files={}
    for node in mesh.data.materials[0].node_tree.nodes:
        if node.type=='TEX_IMAGE':
            label={'Base Color':'BaseColor','Normal':'Normal','Metallic':'Metallic','Roughness':'Roughness'}[node.label]
            texture_files[label]=Path(bpy.path.abspath(node.image.filepath))
    lods=[]
    for obj in meshes:
        obj.data.calc_loop_triangles();lods.append({'name':obj.name,'vertices':len(obj.data.vertices),'triangles':len(obj.data.loop_triangles),'weightedVertices':sum(bool(v.groups) for v in obj.data.vertices),'maxInfluences':max(len(v.groups) for v in obj.data.vertices)})
    motion={state:measure_locomotion(arm,mesh,actions[state]) for state in ['Walk','Run']}
    report={'name':'BoardingRaider','pipeline':'MeshyBiped','preparedSourceSha256':digest(INPUT),'bones':len(arm.data.bones),'heightMetres':2.35,'rig':'Supplied Meshy biped with identity Root; no facial rig','lods':lods,'animations':[],'locomotion':{'walkMetresPerSecond':motion['Walk']['metresPerSecond'],'runMetresPerSecond':motion['Run']['metresPerSecond']},'locomotionMeasurement':motion,'textures':{k:{'file':'Textures/'+k+'.png','sha256':digest(v)} for k,v in texture_files.items()},'fallbackContactValidation':fallback_floors,'attackPresentation':'Short body/hand strike; the supplied sword remains holstered. No supplied attack clip.'}
    for state in ['Idle','Walk','Run','Attack','Hit','Death']:
        action=actions[state];first,last=action.frame_range
        report['animations'].append({'name':action.name,'firstFrame':float(first),'lastFrame':float(last),'fps':FPS,'seconds':float(last-first)/FPS,'loop':state in ['Idle','Walk','Run'],'source':'Meshy supplied' if state in ['Run','Death'] else 'Adapted fallback authored on supplied rig','curvesPreserved':state in ['Run','Death']})
    set_action(arm,actions['Idle']);scene.frame_set(1);scene.frame_start=1;scene.frame_end=73
    bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'BoardingRaider-ready.blend'),compress=True)
    (OUT/'model-manifest.json').write_text(json.dumps(report,indent=2),encoding='utf8')
    if '--export' in sys.argv:
        export(arm,meshes,actions,texture_files,report)
    previews(arm,mesh,actions)
    print('MESHY_RAIDER_READY',json.dumps(report),flush=True)

def export(arm,meshes,actions,texture_files,report):
    (ASSET/'Textures').mkdir(exist_ok=True)
    for label,path in texture_files.items():shutil.copy2(path,ASSET/'Textures'/f'{label}.png')
    for node in meshes[0].data.materials[0].node_tree.nodes:
        if node.type=='TEX_IMAGE':
            label={'Base Color':'BaseColor','Normal':'Normal','Metallic':'Metallic','Roughness':'Roughness'}[node.label]
            node.image.filepath=str(ASSET/'Textures'/f'{label}.png')
    bpy.ops.object.select_all(action='DESELECT');arm.select_set(True)
    for obj in meshes:obj.hide_set(False);obj.select_set(True)
    bpy.context.view_layer.objects.active=arm;set_action(arm,actions['Idle'])
    path=ASSET/'Model/BoardingRaider.fbx'
    bpy.ops.export_scene.fbx(filepath=str(path),use_selection=True,object_types={'MESH','ARMATURE'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,add_leaf_bones=False,armature_nodetype='NULL',use_armature_deform_only=False,bake_anim=True,bake_anim_use_all_actions=True,bake_anim_use_nla_strips=False,bake_anim_simplify_factor=0,path_mode='RELATIVE')
    report['fbxSha256']=digest(path)
    (ASSET/'model-manifest.json').write_text(json.dumps(report,indent=2),encoding='utf8')
    for i,obj in enumerate(meshes):obj.hide_set(i!=0);obj.hide_render=i!=0
    set_action(arm,actions['Idle']);bpy.context.scene.frame_set(1)
    bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/'Artifacts/ArtReview/pirate-crew/source/BoardingRaider.blend'),compress=True)

def previews(arm,mesh,actions):
    scene=bpy.context.scene;scene.render.engine='BLENDER_EEVEE_NEXT';scene.render.resolution_x=1300;scene.render.resolution_y=1500;scene.render.resolution_percentage=100
    cam=scene.camera;cam.data.ortho_scale=3.15
    for state in ['Idle','Walk','Run','Attack','Hit','Death']:
        action=actions[state];set_action(arm,action)
        for bone in arm.pose.bones:
            if bone.name=='Root':bone.matrix_basis=Matrix.Identity(4)
        frame={'Idle':1,'Walk':8,'Run':8,'Attack':12,'Hit':7,'Death':84}[state];scene.frame_set(frame);update()
        lo,hi=evaluated_bounds(mesh);target=Vector([(a+b)/2 for a,b in zip(lo,hi)])
        cam.location=target+Vector((-3,-5,2.1));cam.rotation_euler=(target-cam.location).to_track_quat('-Z','Y').to_euler()
        scene.render.filepath=str(OUT/('clip-'+state.lower()+'.png'));bpy.ops.render.render(write_still=True)
    set_action(arm,actions['Idle']);scene.frame_set(1)

if __name__=='__main__':main()
