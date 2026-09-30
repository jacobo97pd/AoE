"""In-place clips for the posed Meshy pirates, shared by rig_pirate_crew.py and
rig_corsair.py.

The simulation moves every unit and applies damage on the tick an attack starts,
so clips stay in place and land their impact within the first 0.25 s. Gait cycles
use a physiological knee phase (flexed in swing, nearly straight at contact) and
are grounded so the lowest point of either foot rests on the floor every frame.
Idle, reactions, attacks, aiming and work plant both ankles with a two-bone leg
solver, so the hips can lunge, recoil or crouch without the feet skating.

Rotations are degrees about armature axes through each bone's head. The models
face -Y: a negative X rotation swings a hanging limb forward, while a positive X
rotation leans an upright bone (spine, chest, head) forward.
"""
import bpy, math
from mathutils import Matrix, Quaternion, Vector

FPS=30
LOOPS={'Idle','Walk','Run','Work','Aim'}

def ease(a,b,t):
    t=min(1.0,max(0.0,(t-a)/(b-a)));return t*t*(3-2*t)

def curve(t,keys):
    for (a,v),(b,w) in zip(keys,keys[1:]):
        if t<=b:return v+(w-v)*ease(a,b,t)
    return keys[-1][1]

def bump(phase,centre,width):
    """Periodic bell centred on a gait phase, in radians."""
    d=(phase-centre+math.pi)%math.tau-math.pi
    return math.exp(-(d/width)**2)

def cross(a,b):return a.x*b.y-a.y*b.x

class Poser:
    def __init__(self,arm):
        self.arm=arm;self.bones=arm.pose.bones;self.data=arm.data.bones;self.held=set()
        # A heel point rides rigidly on each foot, behind the ankle on the floor.
        self.heel={}
        for side in 'RL':
            foot,toe=self.data['Foot.'+side],self.data['Toe.'+side]
            ahead=toe.tail_local-foot.head_local;ahead.z=0;ahead.normalize()
            heel=foot.head_local-ahead*.07;heel.z=min(toe.head_local.z,toe.tail_local.z)
            self.heel[side]=heel
        # Sculpted stances put one leg ahead; gait angles are measured from vertical
        # (degrees, positive ahead) so both legs swing about the same neutral line.
        self.pitch={}
        for side in 'RL':
            angles=[]
            for bone in ('Thigh.','Shin.'):
                d=self.data[bone+side].tail_local-self.data[bone+side].head_local;angles.append(math.degrees(math.atan2(-d.y,-d.z)))
            self.pitch[side]=angles
        self.lift=None;self.need=0.0
        self.reset();self.update();self.floor=self.lowest()

    def reset(self):
        for pb in self.bones:
            pb.rotation_mode='QUATERNION';pb.rotation_quaternion=(1,0,0,0);pb.location=(0,0,0);pb.scale=(1,1,1)

    def update(self):bpy.context.view_layer.update()

    def rotate(self,name,x=0,y=0,z=0):
        if name not in self.bones or name in self.held:return
        q=Quaternion((1,0,0),math.radians(x))@Quaternion((0,1,0),math.radians(y))@Quaternion((0,0,1),math.radians(z))
        rest=self.data[name].matrix_local.to_quaternion()
        self.bones[name].rotation_quaternion=rest.inverted()@q@rest

    def move(self,name,offset):
        if name not in self.bones or name in self.held:return
        rest=self.data[name].matrix_local.to_quaternion()
        self.bones[name].location=rest.inverted()@Vector(offset)

    def lowest(self):
        points=[]
        for side in 'RL':
            toe,foot=self.bones['Toe.'+side],self.bones['Foot.'+side]
            heel=foot.matrix@self.data['Foot.'+side].matrix_local.inverted()@self.heel[side]
            points+=[toe.tail.z,toe.head.z,heel.z]
        return min(points)

    def ground(self):
        """Lift or lower the whole skeleton so the lowest foot point rests on the floor.
        With a lift floor set, the body never drops below it: both feet leave the
        ground there, which is a run's flight phase."""
        self.update();self.need=self.floor-self.lowest()
        self.move('Root',(0,0,self.need if self.lift is None else max(self.need,self.lift)))

    def plant(self):
        """Keep both ankles where they stand at rest, feet flat, knees forward."""
        self.update()
        for side in 'RL':self.leg(side)

    def leg(self,side):
        data,pose=self.data,self.bones
        thigh,shin,foot=data['Thigh.'+side],data['Shin.'+side],data['Foot.'+side]
        h0,k0,a0=thigh.head_local,shin.head_local,shin.tail_local
        def plane(v):return Vector((v.y,v.z))
        hip=pose['Thigh.'+side].head.copy();h,a=plane(hip),plane(a0)
        l1,l2=(plane(k0)-plane(h0)).length,(plane(a0)-plane(k0)).length
        reach=a-h;d=min(l1+l2-1e-4,max(abs(l1-l2)+1e-4,reach.length))
        phi=math.atan2(reach.y,reach.x)
        beta=math.acos(max(-1.0,min(1.0,(l1*l1+d*d-l2*l2)/(2*l1*d))))
        bend=cross(plane(a0)-plane(h0),plane(k0)-plane(h0))
        theta=phi+beta
        for candidate in (phi+beta,phi-beta):
            knee=h+l1*Vector((math.cos(candidate),math.sin(candidate)))
            if cross(reach,knee-h)*bend>0:theta=candidate
        knee=h+l1*Vector((math.cos(theta),math.sin(theta)))
        rest_thigh=math.atan2(*reversed(plane(k0)-plane(h0)));rest_shin=math.atan2(*reversed(plane(a0)-plane(k0)))
        shin_angle=math.atan2(*reversed(a-knee))
        pose['Thigh.'+side].matrix=Matrix.Translation(hip)@Matrix.Rotation(theta-rest_thigh,4,'X')@Matrix.Translation(-h0)@thigh.matrix_local
        self.update()
        joint=pose['Shin.'+side].head.copy()
        pose['Shin.'+side].matrix=Matrix.Translation(joint)@Matrix.Rotation(shin_angle-rest_shin,4,'X')@Matrix.Translation(-k0)@shin.matrix_local
        self.update()
        ankle=pose['Foot.'+side].head.copy()
        pose['Foot.'+side].matrix=Matrix.Translation(ankle-a0)@foot.matrix_local
        self.update()

    def key(self,frame):
        for pb in self.bones:
            pb.keyframe_insert(data_path='rotation_quaternion',frame=frame,group=pb.name)
            pb.keyframe_insert(data_path='location',frame=frame,group=pb.name)

def idle(p,t,cfg):
    ph=t*math.tau;b=math.sin(ph)
    p.move('Hips',(.006*b,0,-.002*(1-math.cos(2*ph))))
    p.rotate('Spine',x=.5*b);p.rotate('Chest',x=.8*b,z=.6*math.sin(ph+.8))
    p.rotate('Head',z=2*math.sin(ph+1.3),x=-.6*math.sin(2*ph))
    p.rotate('UpperArm.R',x=.8*b);p.rotate('UpperArm.L',x=-.8*b)
    p.rotate('Parrot',x=2*math.sin(2*ph),z=3*math.sin(ph))
    p.plant()

def gait(p,t,cfg,run):
    ph=t*math.tau
    swing=cfg.get('run_swing',30) if run else cfg.get('walk_swing',18)
    lean=8 if run else 2.5;yaw=(5 if run else 3.5)*math.sin(ph)
    p.move('Hips',((.012 if run else .018)*math.cos(ph),0,0))
    p.rotate('Hips',z=yaw,y=(2 if run else 1.4)*math.cos(ph))
    p.rotate('Spine',x=lean,z=-.5*yaw)
    p.rotate('Chest',x=.35*lean+(1.4 if run else .6)*math.sin(2*ph),z=-1.1*yaw)
    p.rotate('Head',x=-.8*lean,z=.35*yaw)
    behind=[]
    for side,offset in (('R',0),('L',math.pi)):
        s=ph+offset;ahead=swing*math.sin(s)
        # Swing begins at toe-off (s=-pi/2): the knee folds early in swing and is
        # nearly straight at heel strike (s=pi/2); the stance knee yields slightly.
        knee=92*bump(s,-.3,.95)+28*bump(s,math.pi,.55) if run else 55*bump(s,-.35,.8)+9*bump(s,.62*math.pi,.5)
        roll=-(14 if run else 11)*bump(s,.5*math.pi,.35)+(26 if run else 18)*bump(s,-.5*math.pi-.3,.45)
        thigh_rest,shin_rest=p.pitch[side]
        thigh=thigh_rest-ahead;shin=(shin_rest-(ahead-knee))-thigh
        p.rotate('Thigh.'+side,x=thigh);p.rotate('Shin.'+side,x=shin);p.rotate('Foot.'+side,x=roll-(thigh+shin))
        arms=cfg.get('arm_swing',1)*(16 if run else 9)*math.sin(s)
        p.rotate('UpperArm.'+side,x=arms);p.rotate('Forearm.'+side,x=-(30 if run else 6)-(6 if run else 2)*math.sin(s))
        p.rotate('Coat.'+side,x=-.5*ahead);behind.append(-ahead)
    p.rotate('CoatBack',x=.3*max(behind)+(4 if run else 2))
    p.rotate('Parrot',x=3*math.sin(2*ph))
    p.ground()

def hit(p,t,cfg):
    h=curve(t,[(0,0),(.16,1),(.42,.55),(1,0)])
    p.move('Hips',(0,.045*h,-.03*h))
    p.rotate('Spine',x=-9*h);p.rotate('Chest',x=-6*h,z=4*h);p.rotate('Head',x=-7*h,z=-3*h)
    for side in 'RL':p.rotate('UpperArm.'+side,x=-9*h);p.rotate('Forearm.'+side,x=-8*h)
    p.rotate('Parrot',x=-8*h)
    p.plant()

def death(p,t,cfg):
    fall=curve(t,[(0,0),(.18,.1),(.33,.35),(.67,1),(1,1)])
    hips=p.data['Hips'].head_local.z
    p.rotate('Hips',x=-fall*83,y=fall*10,z=-fall*8);p.move('Hips',(.10*fall,.13*fall,-(hips-.16)*fall))
    p.rotate('Chest',x=fall*8);p.rotate('Head',x=-fall*10)
    p.rotate('UpperArm.R',y=-fall*25,x=-fall*12);p.rotate('UpperArm.L',y=fall*25,x=-fall*12)
    p.rotate('Thigh.R',x=fall*15);p.rotate('Shin.R',x=fall*24);p.rotate('CoatBack',x=fall*8)

def slash(p,t,cfg,side):
    """Anticipate, cut through by 0.24 of the clip (0.2 s), follow through, recover."""
    wind=curve(t,[(0,0),(.14,1),(.24,0),(1,0)])
    cut=curve(t,[(0,0),(.12,0),(.24,1),(.40,1.1),(.62,.5),(1,0)])
    sign=1 if side=='L' else -1;twist=sign*(15*wind-24*cut);other='R' if side=='L' else 'L'
    a=cfg.get('slash',{})
    p.move('Hips',(0,-.06*cut,-.035*cut))
    p.rotate('Hips',z=.3*twist)
    p.rotate('Spine',x=-5*wind+10*cut,z=.35*twist);p.rotate('Chest',x=-3*wind+6*cut,z=.55*twist)
    p.rotate('Head',x=3*wind-8*cut,z=-.75*twist)
    p.rotate('UpperArm.'+side,x=a.get('arm_wind',-60)*wind+a.get('arm_cut',-20)*cut,y=sign*a.get('arm_open',-10)*wind)
    p.rotate('Forearm.'+side,x=a.get('elbow_wind',-25)*wind+a.get('elbow_cut',15)*cut)
    p.rotate('Hand.'+side,x=a.get('wrist_wind',30)*wind+a.get('wrist_cut',-100)*cut)
    p.rotate('UpperArm.'+other,x=12*wind-14*cut)
    p.rotate('Parrot',x=6*cut)
    p.plant()

def aim_pose(p,cfg,recoil=0):
    p.rotate('Forearm.L',x=cfg.get('aim_angle',78)-12*recoil);p.rotate('UpperArm.L',x=-12);p.rotate('Hand.L',x=5*recoil)

def aim(p,t,cfg):
    ph=t*math.tau;aim_pose(p,cfg)
    p.move('Hips',(.004*math.sin(ph),0,0))
    p.rotate('Spine',x=.4*math.sin(ph));p.rotate('Chest',z=-7+.6*math.sin(ph),x=.5*math.sin(ph));p.rotate('Head',z=5)
    p.plant()

def fire(p,t,cfg):
    """The shot leaves on the first frame, from the held aim; recoil, then settle."""
    r=curve(t,[(0,0),(.08,1),(.35,.2),(.7,0),(1,0)]);aim_pose(p,cfg,r)
    p.move('Hips',(0,.02*r,0))
    p.rotate('Spine',x=-3*r);p.rotate('Chest',z=-7,x=-2*r);p.rotate('Head',z=5,x=-2*r)
    p.plant()

def consult(p,t,cfg):
    k=math.sin(math.pi*t)
    p.rotate('Spine',x=6*k);p.rotate('Chest',x=4*k)
    p.rotate('UpperArm.R',x=-12*k);p.rotate('Forearm.R',x=-18*k)
    p.plant()

def dig(p,t,cfg):
    """Crouch over the find and scrape it free with the map hand, once a second."""
    d=curve(t,[(0,0),(.3,1),(.5,.8),(1,0)])
    p.move('Hips',(0,.05+.025*d,-(.11+.05*d)))
    p.rotate('Spine',x=20+10*d);p.rotate('Chest',x=8+6*d)
    p.rotate('UpperArm.R',x=-12-10*d);p.rotate('Forearm.R',x=15+40*d)
    p.plant()

def clips(cfg):
    role=cfg['role']
    table={'Idle':(90,idle),'Walk':(32,lambda p,t,c:gait(p,t,c,False)),'Run':(20,lambda p,t,c:gait(p,t,c,True)),
           'Hit':(15,hit),'Death':(48,death)}
    if role=='gunner':table['Attack']=(18,fire);table['Aim']=(48,aim)
    elif role=='seeker':table['Attack']=(27,consult);table['Work']=(30,dig)
    else:table['Attack']=(27,lambda p,t,c:slash(p,t,c,c.get('sword_hand','L')))
    return table

def ground_death(arm,poser,frames,detailed):
    """Rest the corpse's torso and legs on the floor; a long weapon may dip below it."""
    scene=bpy.context.scene;arm.animation_data.action=bpy.data.actions['Corsair_Death']
    names={'Hips','Spine','Chest','Neck','Head','Coat.R','Coat.L','CoatBack'}
    groups={g.index for g in detailed.vertex_groups if g.name in names or g.name.startswith(('Thigh.','Shin.','Foot.','Toe.'))}
    supports=[v.index for v in detailed.data.vertices if sum(g.weight for g in v.groups if g.group in groups)>.7]
    for frame in range(frames+1):
        scene.frame_set(frame+1);bpy.context.view_layer.update()
        evaluated=detailed.evaluated_get(bpy.context.evaluated_depsgraph_get());mesh=evaluated.to_mesh()
        floor=min((evaluated.matrix_world@mesh.vertices[i].co).z for i in supports);evaluated.to_mesh_clear()
        poser.move('Root',(0,0,max(0,.008-floor)))
        arm.pose.bones['Root'].keyframe_insert(data_path='location',frame=frame+1,group='Root')

def ground_speed(arm,label):
    """Backward speed of the planted foot relative to the body: metres per second at 1x."""
    scene=bpy.context.scene;action=bpy.data.actions['Corsair_'+label];arm.animation_data.action=action
    first,last=[int(round(v)) for v in action.frame_range];samples=[]
    for frame in range(first,last+1):
        scene.frame_set(frame);samples.append([(arm.pose.bones['Toe.'+s].head.y,arm.pose.bones['Toe.'+s].head.z) for s in 'RL'])
    speeds=[]
    for a,b in zip(samples,samples[1:]):
        side=0 if a[0][1]<=a[1][1] else 1;speeds.append((b[side][0]-a[side][0])*FPS)
    speeds=sorted(v for v in speeds if v>0)
    return round(speeds[len(speeds)//2],3) if speeds else 0.0

def animate(arm,cfg,detailed=None):
    """Author every clip for one character. Returns clip lengths in frames and the
    measured ground speed of Walk and Run."""
    poser=Poser(arm)
    steady=cfg.get('steady',[]);steady=[steady] if isinstance(steady,dict) else steady
    # Evaluating skins is unnecessary while posing bones; the leg solver updates often.
    modifiers=[m for o in bpy.context.scene.objects if o.type=='MESH' for m in o.modifiers if m.type=='ARMATURE' and m.object==arm]
    for m in modifiers:m.show_viewport=False
    if not arm.animation_data:arm.animation_data_create()
    table=clips(cfg);lengths={}
    try:
        for label,(frames,author) in table.items():
            action=bpy.data.actions.new('Corsair_'+label);action.use_fake_user=True;arm.animation_data.action=action
            poser.held={b for g in steady if label not in g.get('except',[]) for b in g['bones']}
            if label in ('Walk','Run'):
                # First pass: how high each frame must sit to keep a foot on the floor.
                # The highest need is mid-stance; cap the drop below it at the bob.
                needs=[]
                for frame in range(frames+1):
                    poser.reset();author(poser,frame/frames,cfg);needs.append(poser.need)
                poser.lift=max(needs)-(cfg.get('run_bob',.07) if label=='Run' else cfg.get('walk_bob',.07))
            for frame in range(frames+1):
                poser.reset();author(poser,frame/frames,cfg);poser.key(frame+1)
            poser.lift=None
            action['Loop']=label in LOOPS;lengths[label]=frames
        poser.held=set()
    finally:
        for m in modifiers:m.show_viewport=True
    if detailed is not None:ground_death(arm,poser,lengths['Death'],detailed)
    speeds={'Walk':ground_speed(arm,'Walk'),'Run':ground_speed(arm,'Run')}
    poser.reset();arm.animation_data.action=bpy.data.actions['Corsair_Idle'];bpy.context.scene.frame_set(1)
    return lengths,speeds
