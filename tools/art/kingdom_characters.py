"""Reproducible Blender 4.5 character authoring for the Kingdom visual slice.

Run with Blender --background --factory-startup --python this_file -- --kind Worker
Requires the CC0 Blender Studio Human Base Meshes v1.4.1, downloaded separately.
All intermediates go to D:/CodexTooling/kingdom-premium; only final FBX + manifests
are emitted into the repository. The FBX is a STATIC POSED visual study, not a rig.
"""
import bpy, bmesh, math, json, sys, random, argparse
from pathlib import Path
from mathutils import Vector, Matrix
from mathutils.bvhtree import BVHTree
from math import sin, cos, pi
sys.path.insert(0,str(Path(__file__).parent))
import kingdom_character_meshes as K

ROOT=Path(__file__).resolve().parents[2]
WORK=Path('D:/CodexTooling/kingdom-premium')
BASE=WORK/'human-base-meshes/human-base-meshes-bundle-v1.4.1/human_base_meshes_bundle.blend'
OUT=ROOT/'Assets/Game/KingdomPremium/Characters'
SCALE=1.235
KIND='Worker'
RNG=random.Random(17641)

def clamp(v,a=0,b=1):return max(a,min(b,v))
def smooth(a,b,v):
    t=clamp((v-a)/(b-a));return t*t*(3-2*t)

def mats():
    defs=[('Skin',(.38,.205,.125),0,.64),('SkinLips',(.32,.145,.10),0,.6),
          ('EyeIvory',(.57,.53,.43),0,.3),('EyeIris',(.09,.048,.017),0,.34),('EyePupil',(.004,.004,.003),0,.21),
          ('Steel',(.22,.275,.31),.82,.42),('SteelDark',(.095,.12,.15),.72,.48),('Gold',(.52,.30,.075),.78,.34),
          ('BlueCloth',(.025,.095,.28),0,.88),('RoyalEmbroidery',(.032,.10,.29),0,.8),
          ('IvoryCloth',(.62,.56,.43),0,.95),('Leather',(.13,.062,.024),0,.72),('LeatherLight',(.26,.135,.054),0,.73),
          ('Hair',(.038,.023,.015),0,.87),('HairLight',(.07,.038,.022),0,.86),('Wood',(.24,.115,.035),0,.86),
          ('HairRoyal',(.066,.052,.041),0,.9),('HairSilver',(.20,.185,.16),0,.91),
          ('Straw',(.53,.32,.09),0,.94),('StrawLight',(.68,.47,.16),0,.94),('Fur',(.71,.67,.56),0,.95),
          ('FurSpot',(.026,.023,.022),0,.96),('CapeLining',(.21,.028,.03),0,.87)]
    for d in defs:K.material(*d)
    # Reusable colour modulation for matte barbered hair. This is a procedural
    # surface texture, not a photograph, sprite, or painted replacement for a face.
    texdir=OUT/'Textures';texdir.mkdir(parents=True,exist_ok=True)
    hairpath=texdir/'HairGrain.png'
    if not hairpath.exists():
        im=bpy.data.images.new('HairGrain',width=512,height=512,alpha=False)
        px=[]
        for y in range(512):
            for x in range(512):
                wave=x*.91+1.5*sin(y*.024+x*.039)+.6*sin(y*.072)
                value=.72+.15*sin(wave*2*pi)+.055*sin(wave*6.1+y*.018)
                px.extend((value,value,value,1))
        im.pixels.foreach_set(px);im.filepath_raw=str(hairpath);im.file_format='PNG';im.save();bpy.data.images.remove(im)
    for name in ['Hair','HairLight','HairRoyal','HairSilver']:
        m=bpy.data.materials[name];nodes=m.node_tree.nodes;bs=nodes.get('Principled BSDF');col=tuple(bs.inputs['Base Color'].default_value)
        t=nodes.new('ShaderNodeTexImage');t.image=bpy.data.images.load(str(hairpath),check_existing=True)
        mix=nodes.new('ShaderNodeMixRGB');mix.blend_type='MULTIPLY';mix.inputs[0].default_value=1;mix.inputs[1].default_value=col
        m.node_tree.links.new(t.outputs['Color'],mix.inputs[2]);m.node_tree.links.new(mix.outputs[0],bs.inputs['Base Color'])
        K.MATERIALS[name]['texture']='Assets/Game/KingdomPremium/Characters/Textures/HairGrain.png'
        K.MATERIALS[name]['textureMultipliesBaseColor']=True
    path=ROOT/'Assets/Game/KingdomPremium/Textures/RoyalEmbroidery.png'
    if path.exists():
        m=bpy.data.materials['RoyalEmbroidery'];tex=m.node_tree.nodes.new('ShaderNodeTexImage');tex.image=bpy.data.images.load(str(path),check_existing=True)
        m.node_tree.links.new(tex.outputs['Color'],m.node_tree.nodes.get('Principled BSDF').inputs['Base Color'])
        K.MATERIALS['RoyalEmbroidery']['texture']='Assets/Game/KingdomPremium/Textures/RoyalEmbroidery.png'
        K.MATERIALS['RoyalEmbroidery']['baseColor']=[1,1,1,1]
        K.MATERIALS['RoyalEmbroidery']['textureMultipliesBaseColor']=False

def base_body():
    with bpy.data.libraries.load(str(BASE),link=False) as (src,dst):dst.objects=['GEO-body_male_realistic']
    obj=dst.objects[0];bpy.context.collection.objects.link(obj);obj.location=(0,0,0);obj.parent=None;obj.animation_data_clear()
    for m in list(obj.modifiers):
        if m.type=='MULTIRES':m.levels=1;m.render_levels=1
        else:obj.modifiers.remove(m)
    K.apply(obj)
    # Distinct adult builds without replacing or flattening the anatomical face.
    for v in obj.data.vertices:
        x,y,z=v.co
        torso=smooth(.90,1.1,z)*(1-smooth(1.32,1.43,z))
        bulk={'Worker':1.05,'Warrior':1.12,'Hero':1.16}[KIND]
        v.co.x=x*(1+(bulk-1)*torso)
        v.co.y=y*(1+(.08 if KIND=='Worker' else .06)*torso)
        if z>1.44:
            jaw=(1-smooth(1.48,1.59,z))*smooth(1.43,1.48,z)
            v.co.x*=1+({'Worker':.09,'Warrior':.06,'Hero':.28}[KIND])*jaw
            if KIND=='Hero' and y<-.09:
                brow=math.exp(-((z-1.602)/.016)**2)*math.exp(-((abs(x)-.039)/.032)**2)
                v.co.y-=.0045*brow
                v.co.y-=.006*math.exp(-((z-1.551)/.03)**2)*math.exp(-(x/.027)**2)
    obj.data.update()
    return obj

def limb_transform(point,side):
    p=Vector(point);s=side
    shoulder=Vector((s*.175,.015,1.335));elbow=Vector((s*.295,.005,1.09));wrist=Vector((s*.375,-.04,.895))
    targets={
      'Worker': {1:((.34,-.015,1.17),(.33,-.225,1.04)), -1:((-.285,.015,1.105),(-.29,-.08,.90))},
      'Warrior': {1:((.31,.015,1.12),(.32,-.19,1.00)), -1:((-.315,.015,1.14),(-.345,-.20,1.005))},
      'Hero': {1:((.315,.015,1.13),(.33,-.09,.93)), -1:((-.325,.015,1.17),(-.34,-.24,1.075))}}
    e,w=[Vector(v) for v in targets[KIND][side]]
    sh=shoulder+Vector((s*(.015 if KIND!='Worker' else 0),0,0))
    q1=(elbow-shoulder).rotation_difference(e-sh)
    q2=(wrist-elbow).rotation_difference(w-e)
    upper=sh+q1@(p-shoulder)
    lower=e+q2@(p-elbow)
    mix=1-smooth(1.045,1.14,p.z)
    return upper.lerp(lower,mix)

def pose(p,tag='auto'):
    p=Vector(p);x,y,z=p
    if tag=='fixed':return p
    if tag=='head':return p
    side=1 if x>0 else -1
    # Smooth arm ownership at the actual shoulder. Below the armpit, only the arms.
    weight=smooth(.14,.25,abs(x)) * smooth(.85,1.02,z)
    if abs(x)>.32 and z>.69:weight=1
    weight*=1-smooth(1.37,1.43,z)
    if tag.startswith('arm'):weight=1
    if tag in ('torso','leg'):weight=0
    return p.lerp(limb_transform(p,side),weight)

def finalize_pose(obj,tag='auto'):
    K.apply(obj)
    # Bake object transforms before world-space analytic limb deformation.
    mat=obj.matrix_world.copy()
    for v in obj.data.vertices:v.co=pose(mat@v.co,tag)
    obj.matrix_world=Matrix.Identity(4)

def skin_and_face(source):
    bvh=BVHTree.FromPolygons([v.co.copy() for v in source.data.vertices],[list(p.vertices) for p in source.data.polygons])
    def front(x,z,offset=.003):
        hit=bvh.ray_cast(Vector((x,-1,z)),Vector((0,1,0)),2)
        return hit[0].y-offset if hit[0] is not None else -.13
    if KIND=='Worker': pred=lambda p:p.z>1.37 or (abs(p.x)>.29 and p.z<1.095 and p.z>.69)
    else: pred=lambda p:p.z>1.37 or (abs(p.x)>.34 and p.z<.905 and p.z>.69)
    skin=K.shell(source,'Anatomical skin',pred,'Skin',0,0,0)
    skin.data.update()
    if KIND!='Worker':
        skin.data.materials.append(bpy.data.materials['Leather'])
        for poly in skin.data.polygons:
            if sum(skin.data.vertices[i].co.z for i in poly.vertices)/len(poly.vertices)<1.0:poly.material_index=1
    # Anatomical fingertips follow the same continuous mesh; curl around the grip.
    for v in skin.data.vertices:
        x,y,z=v.co
        side=1 if x>0 else -1
        gripping=(KIND!='Worker' or side==1)
        if gripping and abs(x)>.39 and .70<z<.805:
            length=.805-z;angle=clamp(length/.032,0,2.65)
            v.co.z=.805-.032*sin(angle)
            v.co.x=x-side*.032*(1-cos(angle))
    # Use the actual eyeball size and centres of the CC0 scan-based anatomy.
    for side in [-1,1]:
        eye=K.sphere('Anatomical eyeball',(side*.0329,-.1205,1.5737),(.0137,.0137,.0137),'EyeIvory',32,20)
        K.sphere('Curved brown iris',(side*.0329,-.1339,1.5737),(.0056,.00085,.0056),'EyeIris',32,16)
        K.sphere('Round pupil',(side*.0329,-.1348,1.5737),(.0026,.0003,.0026),'EyePupil',24,12)
        # Brow follows the supraorbital ridge, rather than a flat sticker.
        points=[]
        browbase=1.592 if KIND=='Hero' else 1.596
        for t in [0,.32,.66,1]:
            x=side*(.014+t*.045);z=browbase+.006*sin(t*pi)-.008*t
            points.append((x,front(x,z,.0028),z))
        K.curve('Anatomically fitted brow',points,.0035 if KIND=='Hero' else .0027,'HairRoyal' if KIND=='Hero' else 'Hair',4,radii=[.45,1,.8,.1])
        for k in range(18):
            t=k/17;x=side*(.014+t*.045);z=browbase+.006*sin(t*pi)-.008*t
            pts=[(x-side*.002,front(x-side*.002,z-.001,.0037),z-.001),(x,front(x,z+.0015,.004),z+.0015),(x+side*.002,front(x+side*.002,z+.002,.0037),z+.002)]
            K.curve('Fine eyebrow hairs',pts,.0007,'HairLight' if k%6==0 else 'Hair',2,radii=[.2,1,.1])
    # Lip colour remains on a thin anatomical region, not a separate protruding mouth.
    lip=K.shell(source,'Natural lip vermilion',lambda p:abs(p.x)<.028 and p.y<-.139 and 1.496<p.z<1.516,'SkinLips',.00035,0,0)
    scalp=K.shell(source,'Tailored scalp hair',lambda p:p.z>1.594+.066*clamp((-p.y-.005)/.14) and abs(p.x)<.095,'Hair',.007,0,.001)
    beard_start=len(K.PARTS)
    beard=K.shell(source,'Anatomical beard',lambda p:1.451<p.z<1.49+.69*abs(p.x) and p.y<-.047,'HairRoyal' if KIND=='Hero' else 'Hair',.0045,0,.002)
    for v in beard.data.vertices:
        x,y,z=v.co;v.co.y-=.0014*(sin(x*192+z*17)+.35*sin(x*439-z*30))
    for j in range(30):
        x=-.069+j*.138/29;top=1.489+.63*abs(x)
        for row in range(2):
            z=top-row*.022
            if z-.028<1.449:continue
            pts=[(x,front(x,z,.006),z),(x*.98,front(x*.98,z-.012,.007),z-.012),(x*.95,front(x*.95,z-.027,.0055),z-.027)]
            mat=('HairSilver' if j%5==0 else 'HairRoyal') if KIND=='Hero' else ('HairLight' if j%7==0 else 'Hair')
            K.curve('Surface following beard fibres',pts,.0014,mat,3,radii=[.4,1,.05])
    # Medium-scale groom strands are curved tapered surfaces, not triangular spikes.
    for side in [-1,1]:
        for k in range(16):
            t=k/15;x=side*(.015+.061*t);y=-.137+.075*t*t;z=1.514+.014*t
            K.curve('Combed beard locks',[(x,y-.006,z),(x*.99,y-.01,z-.027),(x*.87,y-.006,z-.048)],.0033,'HairLight' if k%4==0 else 'Hair',3,radii=[.65,1,.07])
        for k in range(12):
            t=k/11;x=side*(.007+.025*t)
            K.curve('Tapered moustache',[(side*.004,front(side*.004,1.522,.0035),1.522),(x,front(x,1.52,.0045),1.52),(side*(.025+.009*t),front(side*(.025+.009*t),1.508,.0038),1.508)],.0014,'Hair',3,radii=[.35,1,.1])
    if KIND=='Hero':
        for o in K.PARTS[beard_start:]:
            if o.type!='MESH':continue
            for v in o.data.vertices:
                t=1-smooth(1.462,1.493,v.co.z)
                v.co.z-=.025*t;v.co.y-=.008*t;v.co.x*=1+.09*t
    for k in range(32):
        a=2*pi*k/32
        if sin(a)<-.45:continue
        K.curve('Combed scalp locks',[(.06*cos(a),.003+.05*sin(a),1.664),(.083*cos(a),.004+.065*sin(a),1.625),(.082*cos(a),.003+.062*sin(a),1.58)],.0032,'HairLight' if k%6==0 else 'Hair',3,radii=[.7,1,.15])

def underclothes(source):
    K.shell(source,'Gathered linen shirt',lambda p:.96<p.z<(1.393 if abs(p.x)>.10 else 1.375) and (abs(p.x)<.29 or p.z>1.075),'IvoryCloth' if KIND=='Worker' else 'SteelDark',.015,0,.004,.002 if KIND=='Worker' else .001)
    if KIND=='Worker':
        collar=K.lathe('Clean linen neck band',[(1.362,.098,.085,-.018),(1.38,.083,.078,-.013),(1.40,.07,.07,-.008),(1.409,.068,.069,-.008)],'IvoryCloth',64)
        K.subd(collar,1,.003);collar['pose']='torso'
    K.shell(source,'Tailored wool breeches',lambda p:.265<p.z<.98 and abs(p.x)<.235,'LeatherLight' if KIND=='Worker' else 'IvoryCloth',.018,0,.004,.0014)
    for side in [-1,1]:
        # Seam and rolled collar details fitted to the existing lower-leg shape.
        cx=side*.143
        # A shoemaker's continuous last encloses the toes; no painted bare feet.
        verts=[];uv=[];segments=36
        for i,(yy,xx,rx,rz,zc) in enumerate([(.105,.146,.013,.015,.047),(.083,.146,.048,.045,.047),(.03,.154,.055,.056,.057),(-.035,.18,.065,.046,.048),(-.096,.205,.062,.036,.037),(-.137,.216,.045,.029,.030),(-.148,.216,.008,.014,.025)]):
            for j in range(segments):
                a=j*2*pi/segments;verts.append((side*xx+rx*cos(a),yy,zc+rz*sin(a)));uv.append((j/segments,i/6))
        faces=[(i*segments+j,i*segments+(j+1)%segments,(i+1)*segments+(j+1)%segments,(i+1)*segments+j) for i in range(6) for j in range(segments)]
        faces.extend([tuple(reversed(range(segments))),tuple(6*segments+j for j in range(segments))])
        boot=K.subd(K.mesh('Sculpted leather boot last',verts,faces,'Leather',uv),2,.005)
        if KIND!='Worker':
            K.apply(boot)
            K.shell(boot,'Forged sabaton toe',lambda p:p.z>.035 and p.y<.025,'Steel',.006,0,.004)
            if KIND=='Hero':
                # Preserve the full-resolution forged outer plate. The leather
                # last beneath it only needs one subdivision for its exposed
                # heel and sole; this is authored tessellation, not decimation.
                K.PARTS.remove(boot)
                bpy.data.objects.remove(boot,do_unlink=True)
                boot=K.subd(K.mesh('Sculpted leather boot last',verts,faces,'Leather',uv),1,.005)
        shaft=K.lathe('Fitted boot shaft',[(.07,.058,.063,.037),(.10,.052,.059,.037),(.16,.05,.056,.039),(.22,.055,.06,.036),(.295,.057,.064,.033)],'Leather',48,.0007)
        for v in shaft.data.vertices:v.co.x+=cx
        K.subd(shaft,1,.005)
        if KIND!='Worker':
            K.apply(shaft)
            K.shell(shaft,'Fitted lower greave plate',lambda p:p.y<.039 and .09<p.z<.294,'Steel',.007,0,.004)
        K.ring('Boot rolled top',(cx,.035,.29),.059,.067,.026,'LeatherLight',36)
        K.buckle('Boot side fastening',(cx+side*.044,-.029,.25),.026,.031,'SteelDark')
        if KIND=='Worker':
            # Sleeves rolled back just above the elbows.
            c=(side*.289,.009,1.093)
            sleeve=K.ring('Rolled linen sleeve',c,.058,.064,.051,'IvoryCloth',40,.002)
            sleeve['pose']='arm'
        else:
            # Gauntlet cuff over the forearm; fingers retain anatomy.
            K.shell(source,'Curved vambraces',lambda p,s=side:s*p.x>.295 and .88<p.z<1.062,'Steel',.022,0,.006)
            K.shell(source,'Anatomical gauntlet backplate',lambda p,s=side:s*p.x>.402 and .823<p.z<.89,'Gold' if KIND=='Hero' else 'Steel',.006,0,.004)
            K.shell(source,'Articulated cuisses',lambda p,s=side:.055<s*p.x<.23 and .47<p.z<.76 and p.y<-.01,'SteelDark',.027,0,.006)
            K.shell(source,'Curved greaves',lambda p,s=side:s*p.x>.065 and .085<p.z<.405 and p.y<.042,'Steel',.024,0,.006)
            knee=K.sphere('Rounded poleyn',(side*.135,-.077,.452),(.078,.037,.055),'Steel',24,14)
            K.curve('Poleyn rolled edge',[(side*.193,-.075,.46),(side*.137,-.11,.419),(side*.076,-.065,.454)],.004,'Gold' if KIND=='Hero' else 'SteelDark',4)

def robe():
    start=len(K.PARTS)
    mat='BlueCloth' if KIND=='Worker' else 'RoyalEmbroidery'
    front=K.panel('Fitted blue surcoat',[(1.354,.116,-.151,-.122),(1.31,.179,-.218,-.187),(1.22,.169,-.211,-.181),(1.12,.157,-.197,-.168),(1.035,.151,-.190,-.144),(.94,.176,-.206,-.176),(.81,.208,-.217,-.189),(.665,.232,-.224,-.197)],mat,28,.007)
    front['pose']='torso'
    back=K.panel('Surcoat back',[(1.353,.12,.097,.061),(1.27,.167,.154,.115),(1.13,.147,.144,.106),(1.035,.151,.15,.103),(.91,.183,.179,.133),(.78,.217,.189,.14),(.665,.232,.193,.15)],mat,24,.009)
    back['pose']='torso'
    # Narrow contrasting woven borders track the actual drape.
    for side in [-1,1]:
        K.ribbon('Surcoat woven edge',[(side*.114,-.124,1.35),(side*.174,-.189,1.28),(side*.163,-.174,1.18),(side*.15,-.148,1.037),(side*.19,-.187,.88),(side*.229,-.200,.67)],.015,'IvoryCloth' if KIND=='Worker' else 'Gold')['pose']='torso'
        if KIND=='Warrior':
            bridge=K.ribbon('Shoulder tabard bridge',[(side*.135,-.177,1.338),(side*.165,-.100,1.376),(side*.175,.015,1.407),(side*.147,.12,1.349)],.059,mat,subdivision=1);bridge['fitBodyMargin']=.021
        if KIND=='Worker':
            K.ribbon('Folded linen V collar',[(side*.054,-.054,1.417),(side*.072,-.098,1.397),(side*.044,-.158,1.365),(side*.008,-.173,1.34)],.042,'IvoryCloth',.003,subdivision=1)['pose']='torso'
    if KIND=='Worker':
        K.fleur('Worker woven lily',(0,-.238,.8),.11,'StrawLight')
    K.ring('Supple waist belt',(0,-.009,1.031),.187,.199,.053,'Leather',72)['pose']='torso'
    K.buckle('Waist buckle',(-.015,-.216,1.035),.061,.047,'Gold' if KIND=='Hero' else 'Steel')
    K.ribbon('Long belt tongue',[(.042,-.204,1.04),(.043,-.219,.976),(.027,-.22,.913)],.028,'Leather')
    for z in [.98,.955,.93]:K.sphere('Belt punched hole',(.035,-.198,z),(.002,.001,.002),'SteelDark',8,4)
    # Saddler-cut pouch with overlapped curved flap and a real fastening.
    pouch=K.sphere('Leather belt pouch',(-.195,-.113,.955),(.065,.052,.085),'LeatherLight',24,16)
    K.leaf_plate('Pouch folded flap',(-.195,-.160,.998),.11,.062,'Leather',.007)
    K.buckle('Pouch buckle',(-.195,-.175,.967),.022,.025,'SteelDark')
    for o in K.PARTS[start:]:o['pose']='torso'

def armour(source):
    for side in [-1,1]:
        # A fitted cap is extracted from the anatomical deltoid; layered lames
        # follow the arm below it, with no spherical shoulder primitives.
        K.shell(source,'Forged shoulder cap',lambda p,s=side:s*p.x>.15 and 1.252<p.z<1.40,'Steel',.029,0,.009)
        for j in range(3):
            z=1.258-j*.046
            K.shell(source,'Overlapping shoulder lame',lambda p,s=side,z=z:s*p.x>.207 and z-.036<p.z<z+.025,'Steel' if j%2==0 else 'SteelDark',.025+j*.001,0,.006)
        K.curve('Pauldron border',[(side*.153,-.065,1.38),(side*.22,-.084,1.332),(side*.269,-.065,1.266)],.004,'Gold' if KIND=='Hero' else 'SteelDark')
    K.shell(source,'Anatomical cuirass',lambda p:1.045<p.z<1.352 and abs(p.x)<.183,'SteelDark' if KIND=='Warrior' else 'Steel',.023,0,.006)
    # Visible breastplate bevels on either side of the cloth and collar.
    K.curve('Raised gorget',[( -.085,-.069,1.404),(-.07,-.11,1.371),(0,-.123,1.356),(.07,-.11,1.371),(.085,-.069,1.404)],.009,'Gold' if KIND=='Hero' else 'Steel',4)
    for side in ([] if KIND=='Hero' else [-1]):
        K.ribbon('Crossbody sword baldric',[(side*.134,-.15,1.338),(side*.075,-.232,1.251),(-side*.02,-.214,1.14),(-side*.127,-.184,1.051)],.036,'Leather')['pose']='torso'
    if KIND=='Hero':
        for side in [-1,1]:
            for j in range(3):
                x=side*(.195+j*.014);z=1.334-j*.047
                K.curve('Engraved shoulder volute',[(x-side*.011,-.098,z-.008),(x,-.113,z+.006),(x+side*.009,-.11,z-.005),(x,-.11,z-.013)],.0018,'Gold',4)
        for side in [-1,1]:
            K.curve('Gilt breast rim',[(side*.12,-.127,1.33),(side*.177,-.12,1.24),(side*.16,-.122,1.1)],.005,'Gold',4)

def straw_hat():
    # Continuous broad brim with slight handwoven waviness and turned outer lip.
    rings=[]
    for r,z in [(.088,1.663),(.12,1.664),(.17,1.662),(.205,1.669),(.216,1.677),(.213,1.68)]:rings.append((z,r,r*.83,-.005))
    hat=K.lathe('Continuous woven straw brim',rings,'Straw',96)
    for v in hat.data.vertices:
        a=math.atan2(v.co.y+.005,v.co.x);v.co.z+=.008*sin(a*2+.7)*smooth(.09,.21,abs(v.co.x))
    K.subd(hat,1,.004)
    K.subd(K.lathe('Shaped straw crown',[(1.666,.097,.085,-.006),(1.697,.092,.081,-.006),(1.755,.082,.071,-.006),(1.777,.06,.05,-.006),(1.781,.003,.003,-.006)],'Straw',64),1,.003)
    K.ring('Hat leather sweat band',(0,-.006,1.691),.095,.084,.031,'BlueCloth',72)
    for r in [.112,.128,.145,.162,.179,.195,.211]:
        pts=[(r*cos(a),-.005+r*.83*sin(a),1.668+.01*(r/.21)**2) for a in [j*2*pi/64 for j in range(64)]]
        K.curve('Coiled straw weave',pts,.0009,'StrawLight',1,True)
    for j in range(48):
        a=2*pi*j/48
        K.curve('Radial straw weave',[(r*cos(a),-.005+r*.83*sin(a),1.671+.01*(r/.21)**2) for r in [.108,.142,.18,.21]],.00065,'StrawLight',1)

def helmet():
    # Open-face sallet with a curved steel dome and cheek coverage.
    h=K.lathe('Forged open-faced helm',[(1.605,.092,.102,-.057),(1.638,.094,.101,-.052),(1.675,.084,.089,-.047),(1.717,.057,.056,-.042),(1.735,.004,.004,-.040)],'Steel',64)
    K.subd(h,1,.004)
    # Brim sits above brow and avoids covering the face.
    pts=[(.105*cos(a),-.054+.118*sin(a),1.605+.006*cos(a*2)) for a in [j*2*pi/64 for j in range(64)]]
    K.curve('Rolled helmet brim',pts,.006,'SteelDark',2,True)
    for side in [-1,1]:
        K.ribbon('Helmet cheek guard',[(side*.081,-.028,1.612),(side*.087,-.039,1.57),(side*.084,-.063,1.525)],.031,'Steel',.006)
        K.sphere('Helmet hinge',(side*.091,-.035,1.591),(.004,.004,.005),'Gold',12,8)
    K.curve('Helm raised median seam',[(0,-.164,1.609),(0,-.126,1.686),(0,-.040,1.741),(0,.030,1.682),(0,.049,1.612)],.004,'SteelDark',5)

def crown_and_cape():
    start=len(K.PARTS)
    K.ring('Gold crown band',(0,-.030,1.66),.096,.103,.035,'Gold',80)
    for j in range(10):
        a=2*pi*j/10;xx=.094*cos(a);yy=-.030+.102*sin(a)
        begin=len(K.PARTS);K.fleur('Raised crown fleur',(0,0,0),.062,'Gold')
        rot=Matrix.Rotation(a+pi/2,4,'Z')
        for o in K.PARTS[begin:]:
            for v in o.data.vertices:v.co=rot@v.co+Vector((xx,yy,1.702))
        K.sphere('Crown jewel',(xx,yy-.003 if sin(a)<0 else yy,1.663),(.007,.005,.008),'BlueCloth',12,8)
    # Heavy multi-fold mantle, flared at ground and pulled forward over shoulders.
    rows=[(1.373,.215,.102,.012),(1.322,.29,.147,.02),(1.20,.303,.19,.047),(1.035,.319,.223,.058),(.84,.353,.263,.087),(.63,.387,.291,.088),(.415,.414,.306,.101),(.18,.431,.307,.095),(.112,.431,.3,.089)]
    cape=K.panel('Heavy royal mantle',rows,'BlueCloth',48,.031,.010);cape['pose']='torso'
    # Lining has its own real surface, inset behind the outer cloth.
    inner=K.panel('Crimson mantle lining',[(z,w*.988,y-.011,ey-.01) for z,w,y,ey in rows],'CapeLining',48,.029,.003);inner['pose']='torso'
    for side in [-1,1]:
        # Ermine trim follows cape silhouette. Fine curved tufts prevent a bead chain.
        path=[(side*w,ey-.005,z) for z,w,y,ey in rows]
        K.curve('Continuous ermine cape edge',path,.025,'Fur',4)
        for j in range(30):
            t=j/29;z=1.32-t*1.2;w=.28+.15*t;y=.015+.09*t
            K.curve('Soft ermine edge fibres',[(side*(w+.018),y-.019,z+.014),(side*(w+.032),y-.029,z),(side*(w+.028),y-.03,z-.022)],.003,'Fur',3,radii=[.9,1,.1])
            if j%3==0:K.curve('Ermine black tail',[(side*w,y-.034,z),(side*(w+.002),y-.037,z-.025)],.002,'FurSpot',2,radii=[1,.1])
    # A single draped U-shaped fur capelet wraps the nape and both shoulders.
    # Its four radial rows describe a broad surface, not separate tubular bars.
    vv=[];uv=[];n=84
    profiles=[(.087,.078,1.431),(.135,.115,1.433),(.218,.165,1.407),(.289,.205,1.354)]
    for i,(rx,ry,z) in enumerate(profiles):
        for j in range(n+1):
            a=-pi/2+.28+j/n*(2*pi-.56)
            ripple=(.0015*sin(a*19)+.002*sin(a*9+.4))*(i/3)
            vv.append(((rx+ripple)*cos(a),.018+(ry+ripple)*sin(a),z+.018*max(0,sin(a))+.004*sin(a*7)*(i/3)))
            uv.append((j/n,i/3))
    ff=[(i*(n+1)+j,i*(n+1)+j+1,(i+1)*(n+1)+j+1,(i+1)*(n+1)+j) for i in range(3) for j in range(n)]
    # Eighty-four perimeter samples already retain the curved capelet outline;
    # one subdivision suffices beneath the independently modeled fur locks.
    collar=K.subd(K.mesh('Continuous draped ermine collar',vv,ff,'Fur',uv),1,.008)
    K.apply(collar)
    collar_tree=BVHTree.FromPolygons([v.co.copy() for v in collar.data.vertices],[list(p.vertices) for p in collar.data.polygons])
    fur_rng=random.Random(90147)
    # Irregular short locks cover the whole capelet, with volume and dark ermine
    # tufts. There is no evenly spaced curtain of pointed edge fibres.
    for j in range(580):
        a=-pi/2+.30+fur_rng.random()*(2*pi-.60)
        radial=.12+.86*fur_rng.random()**.68
        section=min(2,int(radial*3));t=radial*3-section
        p0,p1=profiles[section],profiles[section+1]
        rx=p0[0]*(1-t)+p1[0]*t;ry=p0[1]*(1-t)+p1[1]*t
        hit=collar_tree.ray_cast(Vector((rx*cos(a),.018+ry*sin(a),1.8)),Vector((0,0,-1)),.7)
        if hit[0] is None:continue
        p=hit[0]+hit[1]*.0008;normal=hit[1].normalized()
        if any((p-Vector((s*.13,-.184,1.382))).length<.04 for s in [-1,1]):continue
        drift=a+fur_rng.uniform(-.7,.7);direction=Vector((cos(drift),sin(drift),-.25))
        direction=(direction-normal*direction.dot(normal)).normalized()
        length=fur_rng.uniform(.007,.023);radius=fur_rng.uniform(.0013,.0031)
        centers=[p,p+direction*length*.35+normal*length*.24,p+direction*length*.78+normal*length*.17,p+direction*length+normal*length*.04]
        verts=[];tex=[];segments=6
        for r,(center,weight) in enumerate(zip(centers,[.55,1,.62,.018])):
            tangent=(centers[min(r+1,3)]-centers[max(0,r-1)]).normalized()
            across=normal.cross(tangent).normalized();up=tangent.cross(across).normalized()
            for k in range(segments):
                angle=k*2*pi/segments
                verts.append(center+across*cos(angle)*radius*weight+up*sin(angle)*radius*weight*.5);tex.append((k/segments,r/3))
        faces=[(r*segments+k,r*segments+(k+1)%segments,(r+1)*segments+(k+1)%segments,(r+1)*segments+k) for r in range(3) for k in range(segments)]
        faces.extend([tuple(reversed(range(segments))),tuple(3*segments+k for k in range(segments))])
        K.mesh('Irregular volumetric ermine lock',verts,faces,'FurSpot' if fur_rng.random()<.055 and radial>.3 else 'Fur',tex)
    for side in [-1,1]:
        K.sphere('Royal mantle clasp',(side*.13,-.184,1.382),(.028,.007,.028),'Gold',24,12)
        K.sphere('Clasp sapphire',(side*.13,-.192,1.382),(.015,.004,.015),'BlueCloth',16,10)
    for o in K.PARTS[start:]:o['pose']='torso'

def scabbard():
    # Empty leather scabbard remains on the belt when the sword is drawn.
    start=len(K.PARTS)
    obj=K.lathe('Saddler-cut sword scabbard',[(1.042,.032,.014,.032),(.98,.031,.014,.035),(.73,.028,.013,.057),(.49,.023,.012,.075),(.435,.006,.008,.083)],'Leather',32)
    for v in obj.data.vertices:v.co.x+=.205+(1.042-v.co.z)*.22
    K.subd(obj,1,.004)
    for z in [1.031,.982,.463]:
        r=K.ring('Scabbard metal mount',(.205+(1.042-z)*.22,.032+(1.042-z)*.085,z),.034 if z>.5 else .023,.017,.022,'Gold' if KIND=='Hero' else 'SteelDark',32)
    K.ribbon('Scabbard suspension strap',[(.173,-.032,1.05),(.227,-.009,.993),(.245,.043,.957)],.026,'LeatherLight')
    for o in K.PARTS[start:]:o['pose']='torso'

def pitchfork():
    # Final-space shaft passes through the posed fingers.
    p=limb_transform((.405,-.098,.815),1);x,y=p.x-.018,p.y
    K.tube('Ash pitchfork shaft',(x,y,.045),(x+.022,y,1.681),.014,'Wood',.011,24)['pose']='fixed'
    K.tube('Pitchfork iron socket',(x+.019,y,1.56),(x+.023,y,1.714),.018,'SteelDark',vertices=20)['pose']='fixed'
    K.curve('Forged fork shoulder',[(x-.082,y,1.723),(x-.055,y,1.685),(x+.023,y,1.69),(x+.088,y,1.72)],.014,'SteelDark',4)['pose']='fixed'
    for i in range(4):
        xx=x-.08+i*.055
        K.curve('Four sharpened iron tines',[(xx,y,1.713),(xx,y-.008,1.82),(xx+.012,y-.022,1.922)],.009,'SteelDark',4,radii=[1, .72,.05])['pose']='fixed'

def sword(downward=False):
    side=-1;p=limb_transform((-.405,-.10,.818),side)
    x,y,z=p.x+.015,p.y-.014,p.z
    sign=-1 if downward else 1
    gripbottom=z-.057;guardz=z+sign*.076
    K.tube('Leather wrapped sword grip',(x,y,z-.061),(x,y,z+.065),.015,'Leather',vertices=20)['pose']='fixed'
    for j in range(7):
        obj=K.ring('Twisted sword grip wrap',(x,y,z-.052+j*.017),.0155,.0155,.004,'Gold' if KIND=='Hero' else 'LeatherLight',24);obj['pose']='fixed'
    K.curve('Swept sword crossguard',[(x-.105,y,guardz+sign*.025),(x-.08,y,guardz+.006*sign),(x,y,guardz),(x+.08,y,guardz+.006*sign),(x+.105,y,guardz+sign*.025)],.009,'Gold' if KIND=='Hero' else 'Steel',4)['pose']='fixed'
    pomz=z-sign*.083
    K.sphere('Sword pommel',(x,y,pomz),(.023,.02,.025),'Gold',20,12)['pose']='fixed'
    length=.90 if KIND=='Hero' else .78
    # Lenticular diamond section with a real fuller ridge, guard to sharp tip.
    verts=[];uv=[]
    for i,(t,w) in enumerate([(0,.027),(.07,.027),(.76,.022),(.94,.012),(1,0.0008)]):
        zz=guardz+sign*(.022+t*length)
        for j,(xx,yy) in enumerate([(-w,0),(0,-.006),(w,0),(0,.006)]):verts.append((x+xx,y+yy,zz));uv.append((j/3,t))
    faces=[(i*4+j,i*4+(j+1)%4,(i+1)*4+(j+1)%4,(i+1)*4+j) for i in range(4) for j in range(4)]
    faces.extend([(0,3,2,1),(16,17,18,19)])
    blade=K.mesh('Tempered straight sword blade',verts,faces,'Steel',uv);blade['pose']='fixed'
    for p in blade.data.polygons:p.use_smooth=False
    if KIND=='Hero':
        K.curve('Sword gilt central inlay',[(x,y-.0065,guardz-.05),(x,y-.0065,guardz-.23),(x,y-.0065,guardz-.30)],.0015,'Gold',2)['pose']='fixed'

def shield():
    # Convex heater shield, continuous gridded surface and rolled steel rim.
    cx=.39;cz=1.007;cy=-.333;rows=24;cols=28;verts=[];uv=[]
    for i in range(rows+1):
        t=i/rows;z=cz+.31-t*.65;w=.225*(1-.94*max(0,(t-.35)/.65)**1.1)
        for j in range(cols+1):
            u=j/cols;xx=(u*2-1)*w;y=cy-.06*(1-(u*2-1)**2)
            verts.append((cx+xx,y,z));uv.append((u,t))
    faces=[(i*(cols+1)+j,i*(cols+1)+j+1,(i+1)*(cols+1)+j+1,(i+1)*(cols+1)+j) for i in range(rows) for j in range(cols)]
    obj=K.subd(K.mesh('Convex blue heater shield',verts,faces,'BlueCloth',uv),1,.022);obj['pose']='fixed'
    edge=[]
    for i in range(rows+1):
        t=i/rows;w=.225*(1-.94*max(0,(t-.35)/.65)**1.1);edge.append((cx-w,cy-.003,cz+.31-t*.65))
    for i in reversed(range(rows+1)):
        t=i/rows;w=.225*(1-.94*max(0,(t-.35)/.65)**1.1);edge.append((cx+w,cy-.003,cz+.31-t*.65))
    K.curve('Heater shield rolled rim',edge,.010,'Steel',2,True)['pose']='fixed'
    for i in range(0,len(edge),5):
        x,y,z=edge[i];K.sphere('Shield rivet',(cx+(x-cx)*.94,y-.012,z),(.004,.003,.004),'Gold',10,6)['pose']='fixed'
    start=len(K.PARTS);K.fleur('Raised gold shield lily',(cx,cy-.086,cz+.02),.39,'Gold')
    for o in K.PARTS[start:]:o['pose']='fixed'
    for z in [cz-.08,cz+.13]:K.ribbon('Shield leather arm strap',[(cx-.12,cy+.05,z),(cx,cy+.11,z+.014),(cx+.12,cy+.05,z)],.035,'Leather')['pose']='fixed'

def worker_pack():
    K.sphere('Canvas grain sack',(0,.196,1.132),(.15,.106,.196),'IvoryCloth',32,20)['pose']='torso'
    K.curve('Gathered sack closure',[(-.06,.215,1.3),(0,.21,1.325),(.06,.215,1.3)],.013,'LeatherLight',3)['pose']='torso'
    for j in range(12):
        x=-.105+j*.018;z=1.47+RNG.uniform(-.065,.065);y=.19+RNG.uniform(-.035,.035)
        K.curve('Wheat stalk',[(x*.5,.20,1.17),(x,y,1.41),(x*1.2,y+.015,z+.075)],.0017,'Straw',2)['pose']='torso'
        for k in range(6):
            zz=z+k*.011;xx=x*1.2
            for s in [-1,1]:
                grain=K.sphere('Wheat grain',(xx+s*.006,y+.012,zz),(.0045,.003,.009),'StrawLight',8,6);grain.rotation_euler.y=s*.5;grain['pose']='torso'

def worker_harness():
    """Two continuous saddler's bands fitted to the FINAL posed garment mesh."""
    prefixes=('Fitted blue surcoat','Gathered linen shirt','Surcoat back','Supple waist belt','Canvas grain sack','Continuous leather harness')
    def garments_tree():
        verts=[];faces=[]
        for obj in K.PARTS:
            if not obj.name.startswith(prefixes):continue
            K.apply(obj);offset=len(verts)
            verts.extend(pose(obj.matrix_world@v.co,obj.get('pose','auto')) for v in obj.data.vertices)
            faces.extend(tuple(offset+i for i in p.vertices) for p in obj.data.polygons)
        return BVHTree.FromPolygons(verts,faces)
    def project(tree,p,stage,clearance=.0035):
        p=Vector(p)
        if stage=='front':origin=Vector((p.x,-1,p.z));direction=Vector((0,1,0))
        elif stage=='back':origin=Vector((p.x,1,p.z));direction=Vector((0,-1,0))
        else:origin=Vector((p.x,p.y,1.9));direction=Vector((0,0,-1))
        hit=tree.ray_cast(origin,direction,2)
        if hit[0] is None:return p,(-direction)
        return hit[0]-direction*clearance,hit[1].normalized()
    for side in [-1,1]:
        tree=garments_tree()
        controls=[Vector(p) for p in [(side*.10,.29,1.22),(side*.155,.145,1.357),(side*.188,.065,1.411),(side*.182,-.043,1.405),(side*.142,-.235,1.316),(side*.073,-.248,1.228),(-side*.034,-.248,1.131),(-side*.124,-.224,1.027)]]
        samples=[];stages=[]
        for i in range(len(controls)-1):
            p0=controls[max(0,i-1)];p1=controls[i];p2=controls[i+1];p3=controls[min(len(controls)-1,i+2)]
            for k in range(9):
                t=k/9;p=.5*((2*p1)+(-p0+p2)*t+(2*p0-5*p1+4*p2-p3)*t*t+(-p0+3*p1-3*p2+p3)*t*t*t)
                stage='back' if i<1 else ('top' if i<3 else 'front')
                q,n=project(tree,p,stage);samples.append((q,n));stages.append(stage)
        q,n=project(tree,controls[-1],'front');samples.append((q,n));stages.append('front')
        verts=[];uv=[];previous=None;width=.033
        for i,(p,normal) in enumerate(samples):
            tangent=(samples[min(i+1,len(samples)-1)][0]-samples[max(0,i-1)][0]).normalized()
            across=tangent.cross(normal).normalized()
            if previous is not None and across.dot(previous)<0:across=-across
            previous=across
            for j in range(5):
                s=j/4-.5;v,n=project(tree,p+across*width*s,stages[i])
                verts.append(v);uv.append((j/4,i/(len(samples)-1)))
        faces=[(i*5+j,i*5+j+1,(i+1)*5+j+1,(i+1)*5+j) for i in range(len(samples)-1) for j in range(4)]
        band=K.subd(K.mesh('Continuous leather harness',verts,faces,'Leather',uv),0,.004);band['pose']='fixed'
        # Buckled attachment is on the belt, with a short riveted tongue.
        end=samples[-1][0];start=len(K.PARTS)
        K.buckle('Harness belt anchor',(end.x,end.y-.004,1.046),.035,.040,'SteelDark')
        K.sphere('Harness anchor rivet',(end.x,end.y-.006,1.026),(.0025,.0015,.0025),'Steel',12,8)
        for o in K.PARTS[start:]:o['pose']='fixed'

def prepare_scene():
    bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
    for data in list(bpy.data.materials):bpy.data.materials.remove(data)
    K.PARTS.clear();K.MATERIALS.clear();mats()

def join_parts(name):
    for obj in K.PARTS:finalize_pose(obj,obj.get('pose','auto'))
    inventory=[]
    for part in K.PARTS:
        part.data.calc_loop_triangles();inventory.append({'name':part.name,'triangles':len(part.data.loop_triangles)})
    bpy.ops.object.select_all(action='DESELECT')
    for obj in K.PARTS:obj.select_set(True)
    bpy.context.view_layer.objects.active=K.PARTS[0];bpy.ops.object.join();obj=bpy.context.object;obj.name=name+'_LOD0'
    # Normalize ground and height. Anatomical body target ~2.1 m; equipment may exceed.
    ground=min(v.co.z for v in obj.data.vertices)
    for v in obj.data.vertices:v.co=Vector((v.co.x*SCALE,v.co.y*SCALE,(v.co.z-ground)*SCALE))
    obj.data.update();obj.data.calc_loop_triangles()
    obj['authoredTriangleCount']=len(obj.data.loop_triangles)
    OUT.mkdir(parents=True,exist_ok=True)
    (OUT/(name+'-surface-inventory.json')).write_text(json.dumps(sorted(inventory,key=lambda x:-x['triangles']),indent=2))
    print('AUTHORED_TRIANGLES '+name+' '+str(len(obj.data.loop_triangles)),flush=True)
    if len(obj.data.loop_triangles)>300000:
        raise ValueError('Authored LOD0 exceeds 300k; reduce authored detail selectively, never globally decimate armour: '+str(len(obj.data.loop_triangles)))
    return obj

def fit_straps(source):
    tree=BVHTree.FromPolygons([v.co.copy() for v in source.data.vertices],[list(p.vertices) for p in source.data.polygons])
    for o in K.PARTS:
        margin=o.get('fitBodyMargin',0)
        if not margin:continue
        K.apply(o)
        for v in o.data.vertices:
            hit=tree.find_nearest(v.co)
            if hit[0] is None or hit[3]>.14:continue
            loc,normal=hit[0],hit[1]
            signed=(v.co-loc).dot(normal)
            if signed<margin or v.co.z>1.365:v.co+=normal*(margin-signed)
        o.data.update()

def render_previews(obj):
    scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.samples=32;scene.cycles.use_denoising=True
    scene.render.threads_mode='FIXED';scene.render.threads=6
    scene.world.use_nodes=True;scene.world.node_tree.nodes['Background'].inputs['Color'].default_value=(.18,.205,.23,1);scene.world.node_tree.nodes['Background'].inputs['Strength'].default_value=.45
    for name,loc,power,size,color in [('Key',(-3.5,-4.2,5),800,4,(1,.87,.72)),('Fill',(3,-2,3.5),550,3,(.73,.84,1)),('Rim',(1,3,4),1000,3,(1,.94,.82))]:
        d=bpy.data.lights.new(name,'AREA');d.energy=power;d.shape='DISK';d.size=size;d.color=color;o=bpy.data.objects.new(name,d);bpy.context.collection.objects.link(o);o.location=loc;o.rotation_euler=(Vector((0,0,1.2))-o.location).to_track_quat('-Z','Y').to_euler()
    bpy.ops.mesh.primitive_plane_add(size=200,location=(0,0,-.013));floor=bpy.context.object;floor.name='PreviewFloor';floor.data.materials.append(K.material('PreviewFloor',(.10,.115,.125),0,.88))
    d=bpy.data.cameras.new('PreviewCamera');cam=bpy.data.objects.new('PreviewCamera',d);bpy.context.collection.objects.link(cam);scene.camera=cam;d.type='ORTHO';d.ortho_scale=2.65
    scene.render.resolution_x=800;scene.render.resolution_y=1000;scene.render.resolution_percentage=100;scene.render.image_settings.file_format='PNG';scene.view_settings.view_transform='AgX';scene.view_settings.look='AgX - Medium High Contrast'
    folder=WORK/'previews';folder.mkdir(exist_ok=True)
    for label,loc in [('front',(2.5,-6,2.9)),('side',(5,-.8,2.7)),('back',(-3,5,2.8))]:
        cam.location=loc;cam.rotation_euler=(Vector((0,0,1.19))-cam.location).to_track_quat('-Z','Y').to_euler();scene.render.filepath=str(folder/(KIND+'-'+label+'.png'));bpy.ops.render.render(write_still=True)
    # A genuine head-and-shoulders render to judge eyes, mouth, materials and fit.
    cam.location=(.8,-4,2.35);cam.rotation_euler=(Vector((0,-.03,1.95))-cam.location).to_track_quat('-Z','Y').to_euler();d.ortho_scale=.84;scene.render.resolution_x=900;scene.render.resolution_y=900;scene.render.filepath=str(folder/(KIND+'-face.png'));bpy.ops.render.render(write_still=True)

def export(obj):
    OUT.mkdir(parents=True,exist_ok=True);lods=[obj];counts=[]
    def clean_mesh(o):
        bm=bmesh.new();bm.from_mesh(o.data)
        bmesh.ops.dissolve_degenerate(bm,dist=0.0000001,edges=list(bm.edges))
        tiny=[f for f in bm.faces if f.calc_area()<1e-11]
        if tiny:bmesh.ops.delete(bm,geom=tiny,context='FACES_ONLY')
        loose=[v for v in bm.verts if not v.link_faces]
        if loose:bmesh.ops.delete(bm,geom=loose,context='VERTS')
        bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(o.data);bm.free();o.data.update()
    clean_mesh(obj)
    obj.data.calc_loop_triangles();lod0_triangles=len(obj.data.loop_triangles)
    for i,target in [(1,100000),(2,35000)]:
        ratio=min(1.0,target/lod0_triangles)
        child=obj.copy();child.data=obj.data.copy();bpy.context.collection.objects.link(child);child.name=KIND+'_LOD'+str(i)
        mod=child.modifiers.new('Generated lower-detail topology','DECIMATE');mod.ratio=ratio;mod.use_collapse_triangulate=True;K.apply(child);clean_mesh(child);lods.append(child)
    for o in lods:
        o.data.calc_loop_triangles();counts.append({'name':o.name,'vertices':len(o.data.vertices),'triangles':len(o.data.loop_triangles),'uvLayers':[uv.name for uv in o.data.uv_layers],'materials':len(o.data.materials)})
    bpy.ops.object.select_all(action='DESELECT')
    for o in lods:o.select_set(True)
    bpy.context.view_layer.objects.active=obj
    # Blender -Y -> Unity -Z, Z -> Y. Root nodes carry importer axis conversion.
    path=OUT/(KIND+'.fbx')
    bpy.ops.export_scene.fbx(filepath=str(path),use_selection=True,object_types={'MESH'},axis_forward='Z',axis_up='Y',apply_unit_scale=True,apply_scale_options='FBX_SCALE_ALL',bake_space_transform=True,use_mesh_modifiers=True,mesh_smooth_type='FACE',add_leaf_bones=False,bake_anim=False,path_mode='STRIP',embed_textures=False)
    manifest={'character':KIND,'source':'Blender Studio Human Base Meshes v1.4.1 / Body Male - Realistic / Dan Ulrich','license':'CC0','sourceUrl':'https://download.blender.org/demo/asset-bundles/human-base-meshes/human-base-meshes-bundle-v1.4.1.zip','sourceSha256':'811f43accbb31a88266d932f8f5563b2d13586fca0ba2693aad1f5fe582b3515','authoring':'Original tailored wardrobe, equipment and static pose; anatomically complete CC0 base mesh retained for face and hands. No rig or animation is claimed.','coordinates':'Blender Z-up / front -Y; FBX export axis_forward Z, axis_up Y, baked conversion intended Unity Y-up / front -Z, ground Y0','unitScaleMetres':1,'lods':counts,'materials':list(K.MATERIALS.values()),'fbxBytes':path.stat().st_size}
    manifest['lodPolicy']={'lod0GlobalDecimation':False,'authoredTrianglesBeforeCleanup':obj['authoredTriangleCount'],'visibleLod0AfterCleanup':counts[0]['triangles'],'lod0Maximum':300000,'lod1Target':100000,'lod2Target':35000}
    manifest['coordinates']='Blender Z-up / front -Y; existing FBX axis_forward Z, axis_up Y. Native Unity integration requires root yaw 180 degrees for final Y-up / front -Z / feet Y0.'
    manifest['unityImport']={'rootYawDegrees':180,'finalSceneUp':'+Y','finalSceneForward':'-Z','groundY':0,'verification':'Native Unity inspection found the imported characters facing backwards; retain the exporter and apply yaw 180 degrees to each Unity root, as for the environment. Blender axisFront alone does not validate Unity import orientation.'}
    (OUT/(KIND+'.json')).write_text(json.dumps(manifest,indent=2),encoding='utf-8')
    for o in lods[1:]:o.hide_render=True;o.hide_set(True)
    return manifest

def main():
    global KIND, OUT
    args=sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else []
    parser=argparse.ArgumentParser();parser.add_argument('--kind',choices=['Worker','Warrior','Hero'],default='Worker');parser.add_argument('--skip-render',action='store_true');parser.add_argument('--output',default=str(OUT));a=parser.parse_args(args);KIND=a.kind;OUT=Path(a.output)
    WORK.mkdir(exist_ok=True);prepare_scene();source=base_body();skin_and_face(source);underclothes(source)
    if KIND!='Worker':armour(source)
    robe()
    if KIND=='Worker':straw_hat();worker_pack();pitchfork()
    elif KIND=='Warrior':helmet();sword();shield();scabbard()
    else:crown_and_cape();sword(True);scabbard()
    fit_straps(source)
    if KIND=='Worker':worker_harness()
    bpy.data.objects.remove(source,do_unlink=True)
    obj=join_parts(KIND);manifest=export(obj)
    if not a.skip_render:render_previews(obj)
    bpy.ops.wm.save_as_mainfile(filepath=str(WORK/(KIND+'.blend')),compress=True)
    print('KINGDOM_CHARACTER_RESULT '+json.dumps(manifest))

if __name__=='__main__':main()
