"""Three real biped dragon interpretations with skinned wings, claws and tails."""
import bpy,math,random
from math import sin,cos,pi
from mathutils import Vector,Matrix
import reference_characters as R
K=R.K

def tag(start,bone):
 for ob in K.PARTS[start:]:ob['bindBone']=bone

def ellipsoid(name,center,radii,mat,bone,segments=32,rings=20):
 ob=K.sphere(name,center,radii,mat,segments,rings);ob['bindBone']=bone;return ob

def tube(name,points,radius,mat,bone,radii=None):
 start=len(K.PARTS);ob=K.curve(name,points,radius,mat,3,radii=radii);tag(start,bone);return ob

def scale_field(name,center,radii,rows,columns,bone,material='Steel',start_angle=0,end_angle=2*pi):
 """Overlapping curved shields follow an ellipsoid instead of painted scales."""
 cx,cy,cz=center;rx,ry,rz=radii
 rng=random.Random(rows*31+columns)
 for row in range(rows):
  phi=.18+(pi-.36)*(row+.5)/rows
  for col in range(columns):
   theta=start_angle+(end_angle-start_angle)*(col+.5*(row%2))/columns
   p=Vector((cx+rx*sin(phi)*cos(theta),cy+ry*sin(phi)*sin(theta),cz+rz*cos(phi)))
   normal=Vector(((p.x-cx)/rx**2,(p.y-cy)/ry**2,(p.z-cz)/rz**2)).normalized()
   w=pi*rx/columns*1.55*max(.4,sin(phi));h=pi*rz/rows*.85
   verts=[(-w,0,h*.55),(0,-.018,h*.85),(w,0,h*.55),(w*.83,0,-h*.25),(0,-.012,-h*.72),(-w*.83,0,-h*.25),(0,-.028,0)]
   rot=Vector((0,-1,0)).rotation_difference(normal).to_matrix()
   vs=[rot@Vector(v)+p for v in verts];fs=[(j,(j+1)%6,6) for j in range(6)]
   ob=K.mesh(name,vs,fs,material if rng.random()>.12 else 'Gold');ob['bindBone']=bone
   for polygon in ob.data.polygons:polygon.use_smooth=True
   K.subd(ob,0,.003,.0007)

def horns(center,side,material='Gold',bone='Head'):
 x,y,z=center
 tube('Swept crown horn',[(x,y,z),(x+side*.17,y+.12,z+.22),(x+side*.22,y+.13,z+.42),(x+side*.13,y+.08,z+.53)],.055,material,bone,[1,.78,.42,.01])

def palette(role):
 R.palette('dragon',role)
 colors={'metallic':{'Steel':(.56,.55,.45),'SteelDark':(.21,.23,.24),'Gold':(.76,.52,.21),'BlueCloth':(.40,.34,.21),'IvoryCloth':(.62,.58,.45),'Gem':(.95,.65,.13)},
 'glacial':{'Steel':(.59,.77,.91),'SteelDark':(.20,.36,.53),'Gold':(.19,.51,.86),'BlueCloth':(.14,.34,.63),'IvoryCloth':(.78,.87,.94),'Gem':(.08,.68,1)},
 'igneous':{'Steel':(.15,.125,.12),'SteelDark':(.08,.065,.06),'Gold':(.66,.115,.02),'BlueCloth':(.36,.045,.017),'IvoryCloth':(.75,.23,.045),'Gem':(1,.26,.012)}}[role]
 for name,col in colors.items():
  mat=bpy.data.materials[name];mat.diffuse_color=tuple(R.RS.srgb_to_linear(c) for c in col)+(1,)
  for node in mat.node_tree.nodes:
   if node.type=='MIX_RGB':node.inputs[1].default_value=mat.diffuse_color
  R.META[name]['baseColor']=list(col)+[1]
 if role!='metallic':
  for name in ['Steel','SteelDark','Gold']:bpy.data.materials[name].node_tree.nodes.get('Principled BSDF').inputs['Metallic'].default_value=.24;R.META[name]['metallic']=.24
 gem=bpy.data.materials['Gem'];bs=gem.node_tree.nodes.get('Principled BSDF');bs.inputs['Emission Color'].default_value=gem.diffuse_color;bs.inputs['Emission Strength'].default_value=1.4
 R.META['Gem']['emissionColor']=list(colors['Gem'])+[1];R.META['Gem']['emissionStrength']=1.4

def make_dragon(role):
 palette(role)
 ellipsoid('Powerful dragon pelvis',(0,.07,1.04),(.39,.30,.46),'SteelDark','Hips')
 ellipsoid('Muscular dragon trunk',(0,0,1.70),(.46,.34,.66),'SteelDark','Chest',40,28)
 scale_field('Torso overlapping scale',(0,0,1.70),(.465,.345,.66),13,22,'Chest')
 ellipsoid('Long armored neck',(0,-.075,2.33),(.22,.23,.49),'SteelDark','Head',40,24)
 scale_field('Neck overlapping scale',(0,-.075,2.34),(.225,.235,.49),10,15,'Head')
 # A segmented ventral plate series articulates with its supporting body region.
 for j in range(15):
  z=1.03+j*.104;width=.30-.11*R.smooth(1.7,2.5,z);y=-.27-.07*sin((z-1)*1.7)
  bone='Hips' if z<1.25 else 'Chest' if z<2.06 else 'Head'
  vs=[(-width,y+.045,z+.065),(0,y-.027,z+.08),(width,y+.045,z+.065),(width*.8,y+.025,z-.035),(0,y-.027,z-.115),(-width*.8,y+.025,z-.035),(0,y-.056,z)]
  ob=K.mesh('Overlapping chevron ventral plate',vs,[(k,(k+1)%6,6) for k in range(6)],'IvoryCloth');ob['bindBone']=bone;K.subd(ob,0,.008,.002)
 # Profile-built angular skull and wedge muzzle avoid a rounded toy face.
 vs=[]
 sections=[(.04,.13,2.64),(.21,.08,2.73),(.25,.07,2.89),(.21,.02,3.035),(.05,-.04,3.13)]
 for width,back,z in sections:
  for x,y in [(-width,-.13),(-width*.72,-.37),(0,-.415),(width*.72,-.37),(width,-.13),(width*.7,back),(0,back+.025),(-width*.7,back)]:vs.append((x,y,z))
 faces=[(r*8+k,r*8+(k+1)%8,(r+1)*8+(k+1)%8,(r+1)*8+k) for r in range(4) for k in range(8)];faces.extend([tuple(reversed(range(8))),tuple(32+k for k in range(8))])
 ob=K.mesh('Profile sculpted dragon cranium',vs,faces,'Steel');ob['bindBone']='Head';K.subd(ob,1,.008)
 vs=[(-.13,-.37,2.86),(.13,-.37,2.86),(-.115,-.76,2.81),(.115,-.76,2.81),(-.14,-.39,2.70),(.14,-.39,2.70),(-.09,-.76,2.73),(.09,-.76,2.73),(0,-.77,2.84),(0,-.36,2.91)]
 ob=K.mesh('Long ridged dragon muzzle',vs,[(0,2,8,9),(9,8,3,1),(0,4,6,2),(1,3,7,5),(2,6,7,3,8),(4,5,7,6),(0,9,1,5,4)],'Steel');ob['bindBone']='Head';K.subd(ob,0,.008,.006)
 vs=[(-.12,-.32,2.66),(.12,-.32,2.66),(-.095,-.71,2.64),(.095,-.71,2.64),(-.08,-.34,2.57),(.08,-.34,2.57),(-.07,-.65,2.585),(.07,-.65,2.585)]
 ob=K.mesh('Angular lower jaw',vs,[(0,1,3,2),(0,2,6,4),(1,5,7,3),(2,3,7,6),(4,6,7,5)],'SteelDark');ob['bindBone']='Head';K.subd(ob,1,.005)
 for side in [-1,1]:
  ellipsoid('Recessed dragon eye',(side*.168,-.365,2.895),(.046,.019,.023),'Gem','Head',24,16)
  ellipsoid('Vertical dragon pupil',(side*.17,-.384,2.895),(.006,.005,.019),'EyePupil','Head',20,12)
  tube('Heavy brow ridge',[(side*.085,-.40,2.93),(side*.18,-.385,2.934),(side*.27,-.25,3.03)],.026,'Steel','Head',[.45,1,.01])
  ellipsoid('Dragon nostril',(side*.083,-.754,2.79),(.017,.009,.012),'EyePupil','Head',16,8)
  horns((side*.16,-.07,3.0),side)
  for j in range(5):
   z=2.76+j*.06;x=side*(.21+.009*j);y=-.15+.036*j
   tube('Cheek crown spine',[(x,y,z),(x+side*.14,y+.06,z+.09),(x+side*.20,y+.11,z+.085)],.026,'Gold','Head',[1,.65,.01])
  for j in range(7):
   y=-.27-j*.052;x=side*(.14-.035*j/6)
   tube('Upper ivory fang',[(x,y,2.727),(x*.98,y-.009,2.665)],.013,'IvoryCloth','Head',[1,.01])
  for j in range(4):
   y=-.31-j*.065;x=side*.11
   tube('Lower ivory fang',[(x,y,2.69),(x,y-.005,2.731)],.01,'IvoryCloth','Head',[1,.01])
 for j in range(13):
  z=1.20+j*.135;y=.30 if z<2.1 else .10
  bone='Chest' if z<2.1 else 'Head'
  tube('Dorsal crest spine',[(0,y,z),(0,y+.19,z+.14),(0,y+.23,z+.20)],.046,'Gold',bone,[1,.55,.01])
 for side,label in [(1,'L'),(-1,'R')]:
  ellipsoid('Dragon shoulder',(side*.45,.02,1.98),(.27,.24,.29),'SteelDark','UpperArm.'+label)
  scale_field('Shoulder scale',(side*.46,.02,1.98),(.275,.245,.30),6,12,'UpperArm.'+label)
  tube('Dragon upper arm',[(side*.48,0,1.97),(side*.72,-.055,1.82),(side*.87,-.12,1.64)],.15,'SteelDark','UpperArm.'+label,[1.2,1,.76])
  tube('Dragon forearm',[(side*.87,-.12,1.64),(side*1.0,-.20,1.53),(side*1.07,-.32,1.43)],.11,'Steel','Forearm.'+label,[1.13,.91,.8])
  ellipsoid('Dragon palm',(side*1.09,-.32,1.4),(.125,.12,.11),'SteelDark','Hand.'+label)
  for j in range(4):
   x=side*(1.015+j*.047);y=-.36
   tube('Articulated dragon finger',[(x,y,1.40),(x+side*.02,y-.065,1.30),(x+side*.025,y-.10,1.25)],.022,'Steel','Hand.'+label,[1,.85,.55])
   tube('Hand talon',[(x+side*.025,y-.10,1.25),(x+side*.04,y-.15,1.22),(x+side*.05,y-.16,1.28)],.019,'IvoryCloth','Hand.'+label,[1,.65,.01])
  ellipsoid('Heavy dragon thigh',(side*.36,.08,.92),(.24,.24,.38),'SteelDark','Thigh.'+label)
  scale_field('Thigh armor scale',(side*.36,.08,.92),(.246,.246,.38),8,13,'Thigh.'+label)
  ellipsoid('Dragon knee',(side*.43,-.13,.60),(.15,.15,.16),'SteelDark','Shin.'+label)
  scale_field('Knee armored scute',(side*.43,-.13,.60),(.154,.154,.17),5,11,'Shin.'+label)
  tube('Digitigrade shank',[(side*.43,-.08,.64),(side*.47,.10,.37),(side*.49,.035,.18)],.103,'SteelDark','Shin.'+label,[1.25,.82,.7])
  ellipsoid('Splayed dragon foot',(side*.49,-.16,.11),(.20,.25,.105),'Steel','Foot.'+label)
  for j in range(3):
   x=side*.49+(j-1)*.105
   tube('Dragon articulated toe',[(x,-.22,.10),(x,-.37,.065),(x,-.44,.07)],.045,'Steel','Foot.'+label,[1,.8,.4])
   tube('Foot talon',[(x,-.4,.07),(x,-.49,.09),(x,-.48,.02)],.04,'IvoryCloth','Foot.'+label,[1,.5,.01])
  # Separate membrane lobes have rows of geometry; the upper and outer sections
  # follow Wing and WingTip bones, while ribs retain their actual thickness.
  root=Vector((side*.39,.10,1.94));elbow=Vector((side*1.28,.16,2.80))
  tube('Wing upper spar',[root,(side*.84,.13,2.38),elbow],.066,'Gold','Wing.'+label,[1,.85,.6])
  tips=[Vector((side*2.23,.07,2.50)),Vector((side*2.03,.12,1.87)),Vector((side*1.72,.20,1.40)),Vector((side*1.18,.24,1.13)),Vector((side*.50,.23,1.36))]
  for j,tip in enumerate(tips):
   mid=elbow.lerp(tip,.50)+Vector((0,-.035,.10))
   tube('Wing finger spar '+str(j),[elbow,mid,tip],.029,'Gold','WingTip.'+label,[1,.72,.12])
   tube('Wing tip claw',[tip,tip+Vector((side*.065,0,-.08))],.024,'IvoryCloth','WingTip.'+label,[1,.01])
  for j in range(len(tips)-1):
   verts=[];uv=[];n=16
   for row in range(n+1):
    v=row/n
    for col in range(n+1):
     u=col/n;edge=tips[j].lerp(tips[j+1],u)
     edge.z+=.12*sin(u*pi)
     point=elbow.lerp(edge,v);point.y+=.055*sin(u*pi)*sin(v*pi)
     verts.append(point);uv.append((u,v))
   faces=[(r*(n+1)+c,r*(n+1)+c+1,(r+1)*(n+1)+c+1,(r+1)*(n+1)+c) for r in range(n) for c in range(n)]
   ob=K.mesh('Thick sculpted wing membrane',verts,faces,'BlueCloth',uv);K.subd(ob,1,.007);ob['bindBone']='WingTip.'+label
  for j in range(5):
   p=root.lerp(elbow,j/5);tube('Wing leading edge scale',[p,p+Vector((side*.08,0,.13))],.039,'Steel','Wing.'+label,[1,.01])
 tail=[Vector(p) for p in [(0,.25,1.15),(0,.80,.84),(.24,1.25,.54),(.65,1.48,.30),(1.06,1.34,.20)]]
 for j in range(4):
  a,b=tail[j],tail[j+1];rad=.20*(1-j*.19);bone='Tail'+str(j+1)
  tube('Segmented dragon tail',[a,a.lerp(b,.5),b],rad,'SteelDark',bone,[1,.87,.72])
  for k in range(6):
   p=a.lerp(b,k/6);tube('Tail ridge spine',[p+Vector((0,0,rad)),p+Vector((0,.04,rad+.12))],.030*(1-j*.12),'Gold',bone,[1,.01])
   for side in [-1,1]:ellipsoid('Tail overlapping side plate',p+Vector((side*rad*.8,0,.02)),(.07,.08,.042),'Steel',bone,16,10)
 for side in [-1,1]:tube('Tail end crescent',[tail[-1],tail[-1]+Vector((side*.13,.03,.16)),tail[-1]+Vector((side*.18,-.04,.25))],.07,'Gold','Tail4',[1,.7,.01])

def bones():
 b=[('Root',(0,0,0),(0,0,.15),None),('Hips',(0,.05,.94),(0,.02,1.34),'Root'),('Spine',(0,.02,1.34),(0,0,1.72),'Hips'),('Chest',(0,0,1.72),(0,0,2.11),'Spine'),('Head',(0,0,2.11),(0,-.15,2.86),'Chest')]
 for side,label in [(1,'L'),(-1,'R')]:
  b.extend([('UpperArm.'+label,(side*.38,0,2.0),(side*.87,-.12,1.64),'Chest'),('Forearm.'+label,(side*.87,-.12,1.64),(side*1.07,-.32,1.43),'UpperArm.'+label),('Hand.'+label,(side*1.07,-.32,1.43),(side*1.10,-.40,1.29),'Forearm.'+label),('Thigh.'+label,(side*.33,.08,1.15),(side*.43,-.10,.63),'Hips'),('Shin.'+label,(side*.43,-.10,.63),(side*.49,.035,.18),'Thigh.'+label),('Foot.'+label,(side*.49,.035,.18),(side*.49,-.40,.07),'Shin.'+label),('Wing.'+label,(side*.39,.10,1.94),(side*1.28,.16,2.8),'Chest'),('WingTip.'+label,(side*1.28,.16,2.8),(side*2.23,.07,2.5),'Wing.'+label)])
 tail=[(0,.25,1.15),(0,.80,.84),(.24,1.25,.54),(.65,1.48,.30),(1.06,1.34,.20)]
 for j in range(4):b.append(('Tail'+str(j+1),tail[j],tail[j+1],'Hips' if j==0 else 'Tail'+str(j)))
 return [(n,Vector(h),Vector(t),p) for n,h,t,p in b]

def build(index,render=False):
 R.reset();role=R.ENTRIES[index-1][2];make_dragon(role)
 for ob in K.PARTS:K.apply(ob);matrix=ob.matrix_world.copy();ob.data.transform(matrix);ob.matrix_world=Matrix.Identity(4)
 rig=R.create_rig(bones());R.skin_parts(K.PARTS,rig,'dragon');lods=R.join_model(K.PARTS[:],f'figure_{index:02d}');actions=R.make_actions(rig,True);R.ground_actions(rig,lods[0],actions)
 return R.export_entry(index,rig,lods,actions,'dragon',role,render,'New biped dragon interpretation with modeled scales, skull, teeth, articulated wings, claws and four tail joints. Generic Run and Fall poses are authored for this skeleton; no flight system, exact likeness or production animation polish is claimed.')
