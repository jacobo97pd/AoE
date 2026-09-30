"""Build editable 3D interpretations of the supplied 26 reference figures.

Blender 4.5, metres, Z up / front -Y. No reference image is placed on geometry.
Sources: project-authored Kingdom tailoring and CC0 Blender Studio anatomy;
the two previously supplied Meshy pirates retain their original mesh and rig.
Heavy output is under the project's D: junctions. Existing art is never edited.
Example: blender -b -t 6 -P tools/art/reference_characters.py -- --ids 18
"""
import bpy, math, json, sys, shutil, argparse, random, hashlib, os
from pathlib import Path
from math import sin, cos, pi
from mathutils import Vector, Matrix, Quaternion
HERE=Path(__file__).resolve().parent
sys.path.insert(0,str(HERE))
import kingdom_characters as KC
import kingdom_character_meshes as K
import royal_soldier_materials as RS
ROOT=HERE.parents[1]
WORK=Path('D:/CodexTooling/reference-characters')
OUT=ROOT/'Assets/Game/ReferenceCharacters/SourceModels'
REVIEW=ROOT/'Artifacts/ArtReview/reference-characters'
CATALOG=ROOT/'Artifacts/ArtReview/character-animation-intake/reference-catalog.json'
TEX=OUT/'Textures'
META={}
SCALE=1.30
ENTRIES=[
 ('Corsario de pólvora','pirate','ranged','GunpowderCorsair'),
 ('Capitán pirata','pirate','hero','CrimsonCorsair'),
 ('Héroe del reino','kingdom','hero','Hero'),
 ('Héroe del desierto','desert','hero','Hero'),
 ('Héroe de montaña','mountain','hero','Hero'),
 ('Dragón metálico','dragon','metallic','Dragon'),
 ('Dragón glacial','dragon','glacial','Dragon'),
 ('Dragón ígneo','dragon','igneous','Dragon'),
 ('Arquero del bosque','elf','archer','Worker'),
 ('Centinela élfico','elf','warrior','Warrior'),
 ('Mago élfico','elf','mage','Worker'),
 ('Guerrero enano','dwarf','warrior','Warrior'),
 ('Minero enano','dwarf','worker','Worker'),
 ('Thane enano','dwarf','hero','Hero'),
 ('Piquero de montaña','mountain','pikeman','Warrior'),
 ('Guerrero de montaña — veterano','mountain','warrior','Warrior'),
 ('Cazador de montaña','mountain','archer','Worker'),
 ('Guerrero del reino','kingdom','warrior','Warrior'),
 ('Guerrero del desierto','desert','warrior','Warrior'),
 ('Guerrero de montaña — guardia','mountain','guard','Warrior'),
 ('Recolector del reino','kingdom','worker','Worker'),
 ('Recolector del desierto','desert','worker','Worker'),
 ('Recolector de montaña','mountain','worker','Worker'),
 ('Jinete del reino a pie','kingdom','rider','Warrior'),
 ('Jinete del desierto a pie','desert','rider','Warrior'),
 ('Jinete de montaña a pie','mountain','rider','Warrior')]

def clamp(v,a=0,b=1):return max(a,min(b,v))
def realm_for_family(family):
 return 'naval' if family=='pirate' else 'fantasy' if family in {'elf','dwarf','dragon','mountain'} else 'historical'
def smooth(a,b,v):t=clamp((v-a)/(b-a));return t*t*(3-2*t)
def reset():
 bpy.ops.wm.read_factory_settings(use_empty=True)
 bpy.context.scene.unit_settings.system='METRIC'
 bpy.context.preferences.filepaths.save_version=0
 K.PARTS.clear();K.MATERIALS.clear();META.clear()

def palette(family,role):
 """Portable bitmap microstructure, explicit material response and neutral UVs."""
 TEX.mkdir(parents=True,exist_ok=True)
 for p in (RS.WORK/'Textures').glob('*_BaseColor.png'):
  if not (TEX/p.name).exists():shutil.copyfile(p,TEX/p.name)
 for p in (RS.WORK/'Textures').glob('*_Normal.png'):
  if not (TEX/p.name).exists():shutil.copyfile(p,TEX/p.name)
 cloth={'kingdom':(.08,.24,.48),'desert':(.50,.06,.045),'mountain':(.16,.23,.29),'elf':(.055,.23,.10),'dwarf':(.33,.075,.035)}.get(family,(.1,.2,.4))
 if family=='elf' and role=='warrior':cloth=(.07,.16,.36)
 if family=='elf' and role=='mage':cloth=(.65,.60,.40)
 if family=='dwarf' and role=='hero':cloth=(.06,.17,.36)
 skin={'desert':(.58,.35,.23),'mountain':(.65,.42,.31),'elf':(.78,.61,.46),'dwarf':(.68,.43,.3)}.get(family,(.69,.465,.345))
 hair={'elf':(.70,.62,.39),'dwarf':(.40,.17,.055),'mountain':(.30,.17,.09)}.get(family,(.15,.085,.045))
 if family=='dwarf' and role=='hero':hair=(.70,.65,.52)
 defs={
  'Skin':(skin,0,.64,'skin'),'SkinLips':((.53,.29,.22),0,.6,'skin'),
  'EyeIvory':((.69,.66,.56),0,.27,'plain'),'EyeIris':((.14,.18,.13),0,.3,'plain'),'EyePupil':((.006,.009,.007),0,.2,'plain'),
  'Steel':((.49,.56,.60),.87,.42,'steel'),'SteelDark':((.20,.24,.27),.82,.52,'steel'),
  'Gold':((.68,.46,.19),.85,.38,'gold'),'BlueCloth':(cloth,0,.85,'cloth'),'RoyalEmbroidery':(cloth,0,.83,'cloth'),
  'IvoryCloth':((.66,.61,.48),0,.9,'cloth'),'Leather':((.23,.12,.065),0,.69,'leather'),
  'LeatherLight':((.38,.24,.12),0,.70,'leather'),'Hair':(hair,0,.82,'hair'),
  'HairLight':(tuple(min(1,c*1.2) for c in hair),0,.85,'hair'),'HairRoyal':(hair,0,.84,'hair'),
  'HairSilver':((.57,.54,.47),0,.88,'hair'),'Wood':((.35,.22,.105),0,.78,'wood'),
  'Straw':((.55,.35,.13),0,.91,'wood'),'StrawLight':((.76,.55,.23),0,.9,'cloth'),
  'Fur':((.66,.60,.48),0,.92,'hair'),'FurSpot':((.12,.09,.07),0,.9,'hair'),
  'CapeLining':((.30,.035,.035),0,.87,'cloth'),'Gem':((.07,.40,.72),.15,.17,'plain')}
 for name,(tint,metal,rough,surface) in defs.items():
  mat=K.material(name,tint,metal,rough);mat.diffuse_color=tuple(RS.srgb_to_linear(c) for c in tint)+(1,)
  nt=mat.node_tree;bs=nt.nodes.get('Principled BSDF')
  bs.inputs['Base Color'].default_value=mat.diffuse_color
  uv=nt.nodes.new('ShaderNodeTexCoord');mult=nt.nodes.new('ShaderNodeVectorMath');mult.operation='MULTIPLY';mult.inputs[1].default_value=(2,2,2);nt.links.new(uv.outputs['UV'],mult.inputs[0])
  base=nt.nodes.new('ShaderNodeTexImage');base.image=bpy.data.images.load(str(TEX/(surface+'_BaseColor.png')),check_existing=True)
  mix=nt.nodes.new('ShaderNodeMixRGB');mix.blend_type='MULTIPLY';mix.inputs[0].default_value=1;mix.inputs[1].default_value=mat.diffuse_color
  nt.links.new(mult.outputs[0],base.inputs['Vector']);nt.links.new(base.outputs['Color'],mix.inputs[2]);nt.links.new(mix.outputs[0],bs.inputs['Base Color'])
  normal=nt.nodes.new('ShaderNodeTexImage');normal.image=bpy.data.images.load(str(TEX/(surface+'_Normal.png')),check_existing=True);normal.image.colorspace_settings.name='Non-Color'
  nm=nt.nodes.new('ShaderNodeNormalMap');nm.inputs['Strength'].default_value=.45
  nt.links.new(mult.outputs[0],normal.inputs['Vector']);nt.links.new(normal.outputs['Color'],nm.inputs['Color']);nt.links.new(nm.outputs['Normal'],bs.inputs['Normal'])
  if surface=='cloth':bs.inputs['Sheen Weight'].default_value=.12
  META[name]={'name':name,'baseColor':list(tint)+[1],'metallic':metal,'smoothness':1-rough,'baseMap':f'Assets/Game/ReferenceCharacters/SourceModels/Textures/{surface}_BaseColor.png','normalMap':f'Assets/Game/ReferenceCharacters/SourceModels/Textures/{surface}_Normal.png','metallicGlossMap':'','tiling':[2,2],'normalScale':.45}

def base_cache(kind):
 path=WORK/(kind+'-parts.blend')
 if path.exists():return path
 reset();palette('kingdom','worker');KC.KIND=kind
 src=KC.base_body();KC.skin_and_face(src);KC.underclothes(src)
 if kind!='Worker':KC.armour(src)
 KC.robe();KC.fit_straps(src)
 bpy.data.objects.remove(src,do_unlink=True)
 for ob in list(K.PARTS):
  if ob.type in {'MESH','CURVE'}:K.apply(ob)
 bpy.ops.file.pack_all();bpy.ops.wm.save_as_mainfile(filepath=str(path),compress=True)
 return path

def parts_added(start,bone=None,pose='auto'):
 for ob in K.PARTS[start:]:
  ob['pose']=pose
  if bone:ob['bindBone']=bone

def disk(name,center,radius,depth,mat,bone=None):
 ob=K.sphere(name,center,(radius,depth,radius),mat,32,16)
 if bone:ob['bindBone']=bone
 return ob

def braid(name,center,length,mat='Hair',width=.035):
 start=len(K.PARTS);x,y,z=center
 for strand in range(3):
  points=[(x+width*.44*cos(t*9*pi+strand*2*pi/3),y+.012*sin(t*9*pi+strand*2*pi/3),z-t*length) for t in [j/24 for j in range(25)]]
  K.curve(name+' woven strand',points,width*.26,mat,2,radii=[1-j/32 for j in range(25)])
 K.ring(name+' clasp',(x,y,z-length*.90),width*.53,.022,.028,'Gold',28)
 parts_added(start,'Head','head')

def long_beard(family,role):
 start=len(K.PARTS)
 width=.105 if family=='dwarf' else .09;length=.34 if family=='dwarf' else .26
 rng=random.Random(871)
 for j in range(62):
  t=j/61;x=(t*2-1)*width;z=1.51+.35*abs(x);y=-.147+.063*(abs(x)/width)**2
  strand_length=length*(1-.52*(abs(x)/width)**1.4)
  pts=[(x,y,z),(x*.93,y-.027,z-strand_length*.25),(x*.65,y-.052,z-strand_length*.66),(x*.30,y-.040,z-strand_length)]
  K.curve('Combed long beard lock',pts,rng.uniform(.0035,.007),'HairLight' if j%7==0 else 'Hair',3,radii=[.8,1,.65,.02])
 parts_added(start,'Head','head')
 for side in [-1,1]:braid('Beard braid',(side*.063,-.185,1.44),length*.78,width=.026)

def fur_collar():
 start=len(K.PARTS);rng=random.Random(822)
 profiles=[(.086,.078,1.425),(.17,.12,1.411),(.275,.187,1.344)];vs=[];uv=[];n=64
 for row,(rx,ry,z) in enumerate(profiles):
  for j in range(n+1):
   a=-pi/2+.30+j/n*(2*pi-.60);vs.append((rx*cos(a),.025+ry*sin(a),z+.012*sin(a)));uv.append((j/n,row/2))
 ob=K.mesh('Draped fur shoulder mantle',vs,[(r*(n+1)+j,r*(n+1)+j+1,(r+1)*(n+1)+j+1,(r+1)*(n+1)+j) for r in range(2) for j in range(n)],'Fur',uv);K.subd(ob,1,.006)
 for j in range(235):
  a=-pi/2+.30+rng.random()*(2*pi-.60);r=rng.random();rx=.09+r*.18;ry=.08+r*.104;z=1.425-.081*r
  p=Vector((rx*cos(a),.025+ry*sin(a),z+.012*sin(a)));d=Vector((cos(a)*.023,sin(a)*.023,-.027))
  K.curve('Fur tapered lock',[p,p+d*.45+Vector((0,0,.008)),p+d],rng.uniform(.0018,.0032),'FurSpot' if j%15==0 else 'Fur',2,radii=[1,.8,.01])
 parts_added(start,'Chest','torso')

def cloak(short=False):
 start=len(K.PARTS)
 end=.58 if short else .21
 rows=[(1.39,.2,.11,.04),(1.30,.28,.16,.045),(1.10,.30,.20,.08),(.90,.33,.245,.10),(.65,.36,.28,.13),(end,.40,.30,.15)]
 K.panel('Draped travelling cloak',rows,'BlueCloth',36,.024,.006)
 K.panel('Contrasting cloak lining',[(z,w*.99,y-.009,ey-.009) for z,w,y,ey in rows],'CapeLining',36,.022,.004)
 parts_added(start,'Cape','torso')

def turban(role):
 start=len(K.PARTS)
 K.sphere('Wrapped linen turban foundation',(0,-.025,1.665),(.104,.115,.085),'IvoryCloth',40,24)
 for j in range(9):
  pts=[(.103*cos(a),-.025+.112*sin(a),1.635+j*.010+.013*sin(a+.5*j)) for a in [k*2*pi/64 for k in range(65)]]
  K.curve('Folded turban cloth course',pts,.007,'IvoryCloth' if j%3 else 'BlueCloth',2)
 if role!='hero':
  K.panel('Desert face veil',[(1.558,.065,-.145,-.106),(1.52,.079,-.16,-.09),(1.46,.065,-.145,-.08),(1.42,.04,-.12,-.07)],'IvoryCloth',28,.004,.003)
 else:
  disk('Turban brooch',(0,-.143,1.68),.027,.009,'Gold');disk('Turban sapphire',(0,-.154,1.68),.015,.005,'Gem')
 parts_added(start,'Head','head')
 start=len(K.PARTS)
 K.panel('Desert scarf tail',[(1.59,.095,.065,.04),(1.38,.105,.19,.08),(1.13,.12,.22,.1),(.9,.13,.23,.12)],'BlueCloth',24,.016,.003)
 parts_added(start,'Cape','torso')

def elf_details(role):
 start=len(K.PARTS)
 for side in [-1,1]:
  ob=K.mesh('Long pointed elven ear',[(side*.075,.0,1.595),(side*.20,.055,1.665),(side*.115,.022,1.545),(side*.105,-.028,1.58)],[(0,1,2),(0,3,1)],'Skin');K.subd(ob,1,.007)
 for j in range(24):
  a=j*2*pi/24
  if sin(a)<-.45:continue
  K.curve('Elven flowing hair',[(.075*cos(a),.01+.071*sin(a),1.657),(.095*cos(a),.025+.10*sin(a),1.49),(.09*cos(a),.07+.105*sin(a),1.32)],.009,'HairLight' if j%4==0 else 'Hair',3,radii=[.8,1,.035])
 if role!='archer':
  K.curve('Elven tiara',[(-.083,-.061,1.651),(-.043,-.129,1.657),(0,-.145,1.703),(.043,-.129,1.657),(.083,-.061,1.651)],.007,'Gold',3)
  disk('Tiara jewel',(0,-.151,1.684),.014,.006,'Gem')
 parts_added(start,'Head','head')
 start=len(K.PARTS)
 for side in [-1,1]:
  for j in range(4):
   x=side*(.18+j*.024);y=-.05;z=1.36-j*.04
   vs=[(x,y,z+.09),(x-.05,y,z+.02),(x-.045,y,z-.045),(x,y-.02,z-.12),(x+.045,y,z-.045),(x+.05,y,z+.02),(x,y-.028,z)]
   ob=K.mesh('Layered pointed elven shoulder leaf',vs,[(q,(q+1)%6,6) for q in range(6)],'BlueCloth' if role=='archer' else 'Gold');K.subd(ob,0,.004,.002)
   ob['pose']='arm';ob['bindBone']='UpperArm.L' if side>0 else 'UpperArm.R'
 parts_added(start,pose='arm')

def worker_pack(family):
 if family=='kingdom':KC.worker_pack();return
 start=len(K.PARTS)
 K.sphere('Travel resource backpack',(0,.19,1.16),(.16,.11,.22),'LeatherLight',32,20)
 for side in [-1,1]:
  K.ribbon('Backpack leather strap',[(side*.10,.28,1.20),(side*.16,.12,1.37),(side*.13,-.18,1.32),(side*.12,-.215,1.07)],.035,'Leather',.005,1)
  K.buckle('Backpack strap buckle',(side*.12,-.223,1.15),.035,.045,'Steel')
 for z in [1.06,1.27]:K.ribbon('Backpack horizontal binding',[(-.16,.265,z),(0,.303,z),(.16,.265,z)],.037,'Leather',.004,1)
 parts_added(start,'Chest','torso')

def weapon(kind,side=-1):
 """All weapon geometry is attached to the gripping hand before animation."""
 start=len(K.PARTS);p=KC.limb_transform((side*.405,-.10,.818),side);x,y,z=p.x+( .015 if side<0 else -.015),p.y-.014,p.z
 bone='Hand.R' if side<0 else 'Hand.L'
 if kind in {'sword','scimitar'}:
  K.tube('Wrapped weapon grip',(x,y,z-.06),(x,y,z+.055),.014,'Leather',vertices=24)
  for j in range(7):K.ring('Grip binding',(x,y,z-.055+j*.016),.0145,.0145,.005,'LeatherLight',24)
  K.curve('Forged swept crossguard',[(x-.095,y,z+.10),(x-.065,y,z+.077),(x,y,z+.07),(x+.065,y,z+.077),(x+.095,y,z+.1)],.008,'Gold',3)
  K.sphere('Weapon pommel',(x,y,z-.085),(.023,.018,.022),'Gold',24,16)
  vs=[];uv=[]
  for i in range(14):
   t=i/13;offset=.20*t*t if kind=='scimitar' else 0;w=.025*(1-.95*t**7)
   for j,(dx,dy) in enumerate([(-w,0),(0,-.006),(w,0),(0,.006)]):vs.append((x+offset+dx,y+dy,z+.09+t*.76));uv.append((j/3,t))
  fs=[(i*4+j,i*4+(j+1)%4,(i+1)*4+(j+1)%4,(i+1)*4+j) for i in range(13) for j in range(4)]
  ob=K.mesh('Tempered curved blade' if kind=='scimitar' else 'Tempered steel blade',vs,fs,'Steel',uv)
  for poly in ob.data.polygons:poly.use_smooth=False
 elif kind in {'pike','staff','pitchfork','pick','axe','hammer'}:
  length={'pike':1.95,'staff':1.6,'pitchfork':1.42,'pick':.85,'axe':.85,'hammer':.95}[kind]
  bottom=z-.65;top=bottom+length
  K.tube('Tool hardwood shaft',(x,y,bottom),(x,y,top),.017 if kind!='hammer' else .024,'Wood',vertices=24)
  for zz in [z-.11,z+.12,top-.07]:K.ring('Shaft binding',(x,y,zz),.020,.020,.034,'Gold',28)
  if kind=='pike':
   ob=K.mesh('Leaf spear tip',[(x,y-.005,top+.31),(x-.045,y,top+.075),(x,y-.009,top-.015),(x+.045,y,top+.075),(x,y+.009,top+.075)],[(0,1,2),(0,2,3),(0,4,1),(0,3,4),(1,4,2),(2,4,3)],'Steel');K.subd(ob,0,.003)
   K.panel('Noble lance pennant',[(top-.03,.17,y, y),(top-.16,.15,y,y),(top-.30,.10,y,y)],'BlueCloth',24,.008,.003)
   for ob in K.PARTS[-1:]:
    for v in ob.data.vertices:v.co.x+=x+.18
  elif kind=='staff':
   for s in [-1,1]:K.curve('Curved staff prong',[(x+s*.02,y,top-.08),(x+s*.10,y,top+.12),(x+s*.05,y,top+.22)],.012,'Gold',4,radii=[1,.8,.1])
   K.sphere('Staff crystal',(x,y,top+.12),(.055,.04,.095),'Gem',16,10)
  elif kind=='pitchfork':
   K.curve('Fork shoulder',[(x-.10,y,top-.03),(x-.09,y,top-.13),(x,y,top-.15),(x+.09,y,top-.13),(x+.10,y,top-.03)],.014,'SteelDark',3)
   for dx in [-.1,-.033,.033,.1]:K.tube('Fork tapered tine',(x+dx,y,top-.11),(x+dx,y-.045,top+.24),.012,'Steel',.002,20)
  elif kind=='pick':K.curve('Forged pick head',[(x-.28,y,top-.12),(x-.16,y,top+.015),(x,y,top+.055),(x+.14,y,top+.025),(x+.24,y,top-.025)],.032,'Steel',4,radii=[.02,.6,1,.8,.1])
  elif kind=='hammer':
   vs=[(x+dx,y+dy,top+dz) for dz in [-.105,.105] for dy in [-.067,.067] for dx in [-.17,.17]]
   ob=K.mesh('Hammer forged striking block',vs,[(0,1,3,2),(4,6,7,5),(0,4,5,1),(2,3,7,6),(0,2,6,4),(1,5,7,3)],'SteelDark');K.subd(ob,0,0,.008)
   for dx in [-.14,.14]:K.curve('Hammer gilt forged rim',[(x+dx,y-.07,top-.09),(x+dx,y-.07,top+.09),(x+dx,y+.07,top+.09),(x+dx,y+.07,top-.09)],.006,'Gold',1,True)
   K.curve('Hammer inset rune',[(x-.055,y-.075,top),(x,y-.075,top+.06),(x+.055,y-.075,top),(x,y-.075,top-.06),(x-.055,y-.075,top)],.004,'Gold',1)
  else:
   ob=K.mesh('Broad crescent axe head',[(x-.02,y,top+.12),(x+.23,y,top+.19),(x+.28,y,top-.19),(x+.17,y,top-.08),(x-.02,y,top-.07)],[(0,1,2,3,4)],'Steel');K.subd(ob,1,.024)
   K.curve('Axe sharpened edge',[(x+.23,y-.015,top+.19),(x+.28,y-.015,top),(x+.28,y-.015,top-.19)],.004,'Gold',3)
 elif kind=='bow':
  pts=[(x+.10*sin(t*pi),y,z-.49+t*.98) for t in [j/16 for j in range(17)]]
  K.curve('Recurved hardwood longbow',pts,.018,'Wood',4,radii=[.45+.5*sin(j*pi/16) for j in range(17)])
  K.curve('Taut bowstring',[(x,y,z-.49),(x-.07,y,z),(x,y,z+.49)],.0016,'IvoryCloth',1)
  for zz in [z-.38,z+.38]:K.ring('Bow engraved binding',(x+.035,y,zz),.018,.018,.026,'Gold',24)
 parts_added(start,bone,'fixed')

def shield(family):
 if family=='kingdom':
  start=len(K.PARTS);KC.shield();parts_added(start,'Hand.L','fixed');return
 start=len(K.PARTS);cx=.37;cy=-.35;cz=1.06;radius=.255
 disk('Curved circular shield',(cx,cy,cz),radius,.045,'BlueCloth' if family=='elf' else 'Wood')
 pts=[(cx+radius*cos(a),cy-.01,cz+radius*sin(a)) for a in [j*2*pi/64 for j in range(65)]]
 K.curve('Shield rolled rim',pts,.015,'Gold' if family in {'desert','elf'} else 'Steel',2)
 disk('Shield boss',(cx,cy-.055,cz),.055,.022,'Gold')
 for j in range(16):
  a=j*2*pi/16;disk('Shield rivet',(cx+.232*cos(a),cy-.036,cz+.232*sin(a)),.005,.004,'Gold')
 if family=='dwarf':
  for s in [-1,1]:K.curve('Dwarven angular shield inlay',[(cx+s*.15,cy-.052,cz-.13),(cx,cy-.065,cz+.15),(cx+s*.12,cy-.055,cz-.01)],.008,'Gold',1)
 elif family=='elf':K.curve('Lunar shield crest',[(cx-.065,cy-.067,cz+.17),(cx+.06,cy-.069,cz+.10),(cx+.04,cy-.069,cz-.08),(cx-.08,cy-.06,cz-.13)],.008,'Gold',4)
 parts_added(start,'Hand.L','fixed')

def transform_point(p,family):
 p=Vector(p)
 if family=='dwarf':p=Vector((p.x*1.23,p.y*1.13,.73*p.z if p.z<1.35 else .9855+(p.z-1.35)*1.08))
 elif family=='elf':p=Vector((p.x*.92,p.y*.95,p.z*1.05))
 elif family=='mountain':p.x*=1.09;p.y*=1.04
 return p*SCALE

def human_parts(family,role,kind):
 cache=base_cache(kind);reset()
 with bpy.data.libraries.load(str(cache),link=False) as (src,dst):dst.objects=src.objects
 for ob in dst.objects:
  if ob.type=='MESH':bpy.context.collection.objects.link(ob);K.PARTS.append(ob)
  else:bpy.data.objects.remove(ob,do_unlink=True)
 palette(family,role);KC.KIND=kind
 for ob in K.PARTS:
  for slot in ob.material_slots:
   if slot.material:
    name=slot.material.name.split('.')[0]
    if name in META:slot.material=bpy.data.materials[name]
 if family=='elf':
  for ob in list(K.PARTS):
   if any(key in ob.name.lower() for key in ['beard','moustache']):K.PARTS.remove(ob);bpy.data.objects.remove(ob,do_unlink=True)
  elf_details(role);cloak(role=='archer')
 if family in {'mountain','dwarf'}:long_beard(family,role);fur_collar();cloak(role in {'worker','archer'})
 if family=='desert':turban(role)
 if role=='hero' and family=='kingdom':
  start=len(K.PARTS);KC.crown_and_cape();parts_added(start,pose='torso')
  for ob in K.PARTS[start:]:ob['bindBone']='Head' if any(x in ob.name.lower() for x in ['crown','jewel']) else 'Cape' if 'mantle' in ob.name.lower() or 'cape edge' in ob.name.lower() else 'Chest'
 elif role=='hero' and family=='dwarf':
  start=len(K.PARTS);KC.helmet();parts_added(start,'Head','head')
  start=len(K.PARTS)
  for j in range(7):
   a=pi+j*pi/6;K.tube('Thane crown spike',(.10*cos(a),-.03+.11*sin(a),1.70),(.115*cos(a),-.03+.12*sin(a),1.82-abs(3-j)*.016),.013,'Gold',.001,16)
  parts_added(start,'Head','head')
 elif family=='kingdom' and role in {'warrior','rider'}:
  start=len(K.PARTS);KC.helmet();parts_added(start,'Head','head')
 elif family=='kingdom' and role=='worker':
  start=len(K.PARTS);KC.straw_hat();parts_added(start,'Head','head')
 elif family=='dwarf' and role=='worker':
  start=len(K.PARTS);KC.helmet();disk('Miner headlamp rim',(0,-.17,1.665),.026,.011,'Gold');disk('Miner lamp glass',(0,-.185,1.665),.019,.006,'Gem');parts_added(start,'Head','head')
 if role=='worker':worker_pack(family)
 if role=='hero' and family=='desert':cloak()
 if role in {'pikeman','rider'}:weapon('pike')
 elif role=='worker':weapon('pitchfork' if family=='kingdom' else 'pick')
 elif role=='archer':weapon('bow')
 elif role=='mage':weapon('staff')
 elif family=='dwarf':weapon('hammer')
 elif family=='mountain':weapon('axe')
 else:weapon('scimitar' if family=='desert' else 'sword')
 if role in {'warrior','guard'} or (family=='dwarf' and role=='hero'):shield(family)
 for ob in K.PARTS:
  K.apply(ob)
  KC.finalize_pose(ob,ob.get('pose','auto'))
  for v in ob.data.vertices:v.co=transform_point(v.co,family)
 return K.PARTS[:]

def humanoid_bones(family):
 def pt(p):return transform_point(p,family)
 b=[('Root',(0,0,0),(0,0,.15),None),('Hips',(0,0,.92),(0,0,1.06),'Root'),('Spine',(0,0,1.06),(0,0,1.24),'Hips'),('Chest',(0,0,1.24),(0,0,1.39),'Spine'),('Head',(0,0,1.39),(0,-.02,1.70),'Chest'),('Cape',(0,.15,1.36),(0,.29,.40),'Chest')]
 for side,label in [(1,'L'),(-1,'R')]:
  shoulder=(side*.19,.015,1.345);elbow=KC.limb_transform((side*.295,.005,1.09),side);wrist=KC.limb_transform((side*.375,-.04,.895),side);hand=KC.limb_transform((side*.415,-.07,.80),side)
  b += [('UpperArm.'+label,shoulder,elbow,'Chest'),('Forearm.'+label,elbow,wrist,'UpperArm.'+label),('Hand.'+label,wrist,hand,'Forearm.'+label),('Thigh.'+label,(side*.13,0,.94),(side*.14,-.015,.46),'Hips'),('Shin.'+label,(side*.14,-.015,.46),(side*.146,.04,.12),'Thigh.'+label),('Foot.'+label,(side*.146,.04,.12),(side*.21,-.13,.045),'Shin.'+label)]
 return [(n,pt(h),pt(t),parent) for n,h,t,parent in b]

def create_rig(bones):
 arm=bpy.data.armatures.new('Reference skeleton');rig=bpy.data.objects.new('Rig',arm);bpy.context.collection.objects.link(rig)
 bpy.context.view_layer.objects.active=rig;rig.select_set(True);bpy.ops.object.mode_set(mode='EDIT')
 for name,head,tail,parent in bones:
  b=arm.edit_bones.new(name);b.head=head;b.tail=head+Vector((0,.15,0)) if name=='Root' else tail
  if parent:b.parent=arm.edit_bones[parent]
 bpy.ops.object.mode_set(mode='OBJECT');rig.show_in_front=True
 for p in rig.pose.bones:p.rotation_mode='XYZ'
 return rig

def point_segment_distance(p,a,b):
 ab=b-a;u=clamp((p-a).dot(ab)/max(1e-9,ab.length_squared));return (p-(a+ab*u)).length

def skin_parts(parts,rig,family):
 bones={b.name:(b.head_local.copy(),b.tail_local.copy()) for b in rig.data.bones}
 for ob in parts:
  for vg in list(ob.vertex_groups):ob.vertex_groups.remove(vg)
  groups={name:ob.vertex_groups.new(name=name) for name in bones}
  fixed=ob.get('bindBone');name=ob.name.lower()
  # Explicit attachments prevent sword, ornaments, beard, and cape stretching.
  if not fixed and any(k in name for k in ['eye','iris','pupil','brow','scalp','beard','moustache','lip','ear','helm','crown','turban']):fixed='Head'
  if fixed:
   groups[fixed].add(list(range(len(ob.data.vertices))),1,'REPLACE')
  else:
   for v in ob.data.vertices:
    p=v.co;z=p.z/SCALE;x=p.x/SCALE
    if family=='dwarf':z=z/.73 if z<.9855 else 1.35+(z-.9855)/1.08;x/=1.23
    elif family=='elf':z/=1.05;x/=.92
    elif family=='mountain':x/=1.09
    side='L' if x>0 else 'R'
    if z>1.42:allowed=['Head']
    elif abs(x)>.225 and z>.78:allowed=['UpperArm.'+side,'Forearm.'+side,'Hand.'+side]
    elif z<.94:allowed=['Hips','Thigh.'+side,'Shin.'+side,'Foot.'+side]
    else:allowed=['Hips','Spine','Chest']
    closest=sorted(((point_segment_distance(p,*bones[n]),n) for n in allowed))[:2]
    weights=[1/max(.025,d)**4 for d,n in closest];total=sum(weights)
    for (d,n),w in zip(closest,weights):groups[n].add([v.index],w/total,'REPLACE')
  mod=ob.modifiers.new('Deform with reference skeleton','ARMATURE');mod.object=rig;ob.parent=rig

def join_model(parts,name):
 bpy.ops.object.select_all(action='DESELECT')
 for ob in parts:ob.select_set(True)
 bpy.context.view_layer.objects.active=parts[0]
 bpy.ops.object.join();ob=bpy.context.object;ob.name=name+'_LOD0'
 # Preserve the full editable mesh in .blend only. The imported top LOD has a
 # finite budget, rather than exporting hundreds of invisible stitch segments.
 ob.data.calc_loop_triangles();source_triangles=len(ob.data.loop_triangles)
 if source_triangles>250000:
  high=ob.copy();high.data=ob.data.copy();high.name='SOURCE_EDITABLE_'+name;bpy.context.collection.objects.link(high);high.hide_render=True;high.hide_set(True)
  deform=next((m for m in ob.modifiers if m.type=='ARMATURE'),None);rig=deform.object if deform else ob.parent
  if deform:ob.modifiers.remove(deform)
  dec=ob.modifiers.new('Top LOD budget preserving original in source','DECIMATE');dec.ratio=220000/source_triangles;dec.use_collapse_triangulate=True
  bpy.context.view_layer.objects.active=ob;bpy.ops.object.modifier_apply(modifier=dec.name)
  mod=ob.modifiers.new('Deform with reference skeleton','ARMATURE');mod.object=rig
 lods=[ob]
 ob.data.calc_loop_triangles();base_triangles=len(ob.data.loop_triangles)
 for i,target in [(1,40000),(2,11000)]:
  ratio=min(.80 if i==1 else .45,target/max(1,base_triangles))
  cp=ob.copy();cp.data=ob.data.copy();bpy.context.collection.objects.link(cp);cp.name=name+'_LOD'+str(i)
  deform=cp.modifiers.get('Deform with reference skeleton')
  if deform:cp.modifiers.remove(deform)
  dec=cp.modifiers.new('Derived LOD reduction','DECIMATE');dec.ratio=ratio;dec.use_collapse_triangulate=True
  bpy.context.view_layer.objects.active=cp;bpy.ops.object.modifier_apply(modifier=dec.name)
  mod=cp.modifiers.new('Deform with reference skeleton','ARMATURE');mod.object=ob.parent
  cp.hide_render=True;cp.hide_set(True);lods.append(cp)
 return lods

def keyframe(rig,frame):
 for b in rig.pose.bones:
  b.keyframe_insert(data_path='location',frame=frame);b.keyframe_insert(data_path='rotation_euler',frame=frame)

def clear_pose(rig):
 for b in rig.pose.bones:b.location=(0,0,0);b.rotation_euler=(0,0,0)

def make_actions(rig,dragon=False):
 rig.animation_data_create();actions=[]
 for state,length in [('Idle',61),('Run',25),('Fall',49)]:
  action=bpy.data.actions.new(state);action.use_fake_user=True;rig.animation_data.action=action
  for frame in range(1,length+1,2):
   clear_pose(rig);t=(frame-1)/(length-1);phase=t*2*pi
   if state=='Idle':
    rig.pose.bones['Chest'].rotation_euler.x=.014*sin(phase)
    rig.pose.bones['Head'].rotation_euler.z=.017*sin(phase)
   elif state=='Run':
    rig.pose.bones['Root'].location.z=.055*(1-cos(phase*2))
    rig.pose.bones['Hips'].rotation_euler.x=.06
    rig.pose.bones['Chest'].rotation_euler.x=.055
    for side,offset in [('L',0),('R',pi)]:
     angle=phase+offset
     rig.pose.bones['Thigh.'+side].rotation_euler.x=.64*sin(angle)
     rig.pose.bones['Shin.'+side].rotation_euler.x=-.72*max(0,sin(angle-.6))
     rig.pose.bones['Foot.'+side].rotation_euler.x=.17*sin(angle+.9)
     rig.pose.bones['UpperArm.'+side].rotation_euler.x=-.33*sin(angle)
     rig.pose.bones['Forearm.'+side].rotation_euler.x=.10+.18*max(0,sin(angle))
    if 'Cape' in rig.pose.bones:rig.pose.bones['Cape'].rotation_euler.x=.05+.045*sin(phase*2)
   else:
    # One-shot stumble and collapse; Root is adjusted to ground below afterwards.
    u=smooth(.08,.78,t);settle=sin((t-.7)*pi*3)*.035*(1-u)
    # Collapse onto the back: the shield stays above the torso instead of
    # propping the entire body above the floor on its long lower edge.
    rig.pose.bones['Root'].rotation_euler.x=-1.48*u
    rig.pose.bones['Root'].rotation_euler.y=.10*u
    rig.pose.bones['Hips'].rotation_euler.x=.15*sin(t*pi)
    rig.pose.bones['Chest'].rotation_euler.x=.18*sin(t*pi)
    rig.pose.bones['Head'].rotation_euler.z=-.14*u
    for side,sign in [('L',1),('R',-1)]:
     rig.pose.bones['Thigh.'+side].rotation_euler.x=sign*.28*sin(t*pi)+.15*u
     rig.pose.bones['Shin.'+side].rotation_euler.x=-.35*u
     rig.pose.bones['UpperArm.'+side].rotation_euler.z=sign*.30*u
     rig.pose.bones['Forearm.'+side].rotation_euler.x=-.22*u
    if 'Cape' in rig.pose.bones:rig.pose.bones['Cape'].rotation_euler.x=-.18*u
   if dragon:
    for side,sign in [('L',1),('R',-1)]:
     wing=rig.pose.bones['Wing.'+side];basis=wing.bone.matrix_local.to_quaternion()
     # The falling dragon lands on its back with the wing membranes splayed
     # near the floor. Folding them behind its back would prop up the corpse.
     fold=sign*(.055*sin(phase)+(1.08 if state=='Run' else .10*smooth(.1,.8,t) if state=='Fall' else 0))
     wing.rotation_euler=(basis.inverted()@Quaternion(Vector((0,0,1)),fold)@basis).to_euler('XYZ')
     rig.pose.bones['WingTip.'+side].rotation_euler.z=sign*.08*sin(phase+.6)
    for j in range(1,5):rig.pose.bones['Tail'+str(j)].rotation_euler.x=.07*sin(phase+j*.65) if state!='Fall' else 0
    if state=='Fall':
     tail=rig.pose.bones['Tail1'];basis=tail.bone.matrix_local.to_quaternion()
     tail.rotation_euler=(basis.inverted()@Quaternion(Vector((1,0,0)),2.2*smooth(.1,.8,t))@basis).to_euler('XYZ')
   keyframe(rig,frame)
  actions.append(action)
 rig.animation_data.action=actions[0];bpy.context.scene.frame_set(1);clear_pose(rig)
 return actions

def ground_actions(rig,lod,actions):
 """Sample the deformed mesh to keep each authored pose above the ground plane."""
 scene=bpy.context.scene;deps=bpy.context.evaluated_depsgraph_get()
 for action in actions:
  rig.animation_data.action=action
  for frame in range(int(action.frame_range[0]),int(action.frame_range[1])+1,2):
   scene.frame_set(frame);root=rig.pose.bones.get('Root')
   if root is None:continue
   root.location.z=0;bpy.context.view_layer.update()
   evalob=lod.evaluated_get(deps);mesh=evalob.to_mesh()
   minimum=min((evalob.matrix_world@v.co).z for v in mesh.vertices);evalob.to_mesh_clear()
   root.location.z=-minimum+.005;root.keyframe_insert(data_path='location',frame=frame)
 rig.animation_data.action=actions[0];scene.frame_set(1)

def bounds(objects):
 pts=[ob.matrix_world@Vector(c) for ob in objects for c in ob.bound_box]
 return [min(p[i] for p in pts) for i in range(3)],[max(p[i] for p in pts) for i in range(3)]

def setup_stage(lod,dragon=False):
 scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.samples=16;scene.cycles.use_denoising=True
 scene.render.resolution_x=900;scene.render.resolution_y=1100;scene.render.resolution_percentage=100
 scene.world=bpy.data.worlds.new('Review world');scene.world.use_nodes=True;scene.world.node_tree.nodes['Background'].inputs[0].default_value=(.18,.22,.27,1);scene.world.node_tree.nodes['Background'].inputs[1].default_value=.3
 scene.view_settings.view_transform='AgX'
 for name,location,power,size,color in [('Key',(-3,-4,6),750,4,(1,.87,.71)),('Fill',(4,-2,3),470,3,(.63,.77,1)),('Rim',(0,3,4),950,3,(.77,.87,1))]:
  ld=bpy.data.lights.new('REVIEW_'+name,'AREA');ld.energy=power;ld.shape='DISK';ld.size=size;ld.color=color;ob=bpy.data.objects.new(ld.name,ld);bpy.context.collection.objects.link(ob);ob.location=location;ob.rotation_euler=(Vector((0,0,1.3))-ob.location).to_track_quat('-Z','Y').to_euler()
 bpy.ops.mesh.primitive_plane_add(size=200);floor=bpy.context.object;floor.name='REVIEW_Floor';m=bpy.data.materials.new('REVIEW_Ground');m.use_nodes=True;m.node_tree.nodes.get('Principled BSDF').inputs['Base Color'].default_value=(.055,.07,.08,1);m.node_tree.nodes.get('Principled BSDF').inputs['Roughness'].default_value=.88;floor.data.materials.append(m)
 camera=bpy.data.cameras.new('REVIEW_Camera');ob=bpy.data.objects.new('REVIEW_Camera',camera);bpy.context.collection.objects.link(ob);scene.camera=ob;camera.type='ORTHO';camera.lens=55
 return ob

def render_views(lod,folder,dragon=False):
 cam=setup_stage(lod,dragon);scene=bpy.context.scene
 lo,hi=bounds([lod]);height=hi[2]-lo[2];width=hi[0]-lo[0];target=Vector(((hi[0]+lo[0])/2,0,height*.51))
 for view,offset in [('front',( .35,-8,3)),('rts',(5,-8,8))]:
  cam.location=target+Vector(offset);cam.rotation_euler=(target-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.ortho_scale=max(height*1.22,width*1.40)
  scene.render.filepath=str(folder/(view+'.png'));bpy.ops.render.render(write_still=True)

def export_entry(index,rig,lods,actions,family,role,render=False,source_note=''):
 identifier=f'figure_{index:02d}';folder=OUT/identifier;folder.mkdir(parents=True,exist_ok=True);review=REVIEW/identifier;review.mkdir(parents=True,exist_ok=True)
 scene=bpy.context.scene;scene.render.fps=30;scene.frame_start=1;scene.frame_end=61
 rig.animation_data.action=next((a for a in actions if a.name=='Idle'),actions[0]);scene.frame_set(1)
 for ob in lods:ob.hide_set(False);ob.hide_render=ob!=lods[0]
 bpy.ops.object.select_all(action='DESELECT');rig.select_set(True)
 for ob in lods:ob.select_set(True)
 for ob in rig.children_recursive:
  if ob.type=='EMPTY':ob.select_set(True)
 bpy.context.view_layer.objects.active=rig
 model=folder/(identifier+'.fbx')
 staged_model=WORK/(identifier+'-export.fbx')
 bpy.ops.export_scene.fbx(filepath=str(staged_model),use_selection=True,object_types={'ARMATURE','MESH','EMPTY'},axis_forward='Z',axis_up='Y',apply_unit_scale=True,apply_scale_options='FBX_SCALE_ALL',use_mesh_modifiers=True,mesh_smooth_type='FACE',add_leaf_bones=False,bake_anim=True,bake_anim_use_all_actions=True,bake_anim_use_nla_strips=False,bake_anim_force_startend_keying=True,bake_anim_simplify_factor=0,path_mode='STRIP',embed_textures=False)
 os.replace(staged_model,model)
 for ob in lods[1:]:ob.hide_set(True)
 bpy.ops.file.pack_all();blend=review/(identifier+'.blend');bpy.ops.wm.save_as_mainfile(filepath=str(blend),compress=True)
 lo,hi=bounds([lods[0]]);lodcounts=[]
 for ob in lods:ob.data.calc_loop_triangles();lodcounts.append(len(ob.data.loop_triangles))
 reference=next(r for r in json.loads(CATALOG.read_text(encoding='utf-8-sig'))['references'] if any(f['id']==identifier for f in r['figures']))
 entry={'id':identifier,'name':ENTRIES[index-1][0],'family':family,'role':role,'suggestedFaction':family,'realm':realm_for_family(family),'modelPath':f'{identifier}/{identifier}.fbx','sourceBlendPath':str(blend.relative_to(ROOT)).replace('\\','/'),'heightMetres':hi[2]-lo[2],'boundsMin':lo,'boundsMax':hi,'locomotion':{'walkMetresPerSecond':1.7,'runMetresPerSecond':3.4},'clips':[{'name':a.name,'loop':'run' in a.name.lower() or 'idle' in a.name.lower() or 'walk' in a.name.lower(),'firstFrame':float(a.frame_range[0]),'lastFrame':float(a.frame_range[1])} for a in actions],'materials':list(META.values()),'triangles':lodcounts[0],'lodTriangles':lodcounts,'bones':len(rig.data.bones),'referenceImage':reference['image'],'fbxSha256':hashlib.sha256(model.read_bytes()).hexdigest(),'interpretationNotes':source_note or 'New editable 3D interpretation. Proportions, garments and equipment are modeled geometry; source PNG is reference only. Facial likeness, fine embroidery and animation polish do not reproduce the illustration exactly.'}
 used_materials={m.name for ob in lods for m in ob.data.materials if m}
 entry['materials']=[m for m in entry['materials'] if m['name'] in used_materials]
 atomic_json(folder/'model-manifest.json',entry)
 update_manifest()
 if render:render_views(lods[0],review,family=='dragon')
 print('REFERENCE_CHARACTER_EXPORTED '+json.dumps({'id':identifier,'triangles':lodcounts,'bones':entry['bones'],'height':entry['heightMetres']}),flush=True)
 return entry

def atomic_json(path,value):
 staged=path.with_name(path.name+f'.{os.getpid()}.tmp')
 staged.write_text(json.dumps(value,ensure_ascii=False,indent=2),encoding='utf-8')
 os.replace(staged,path)

def update_manifest():
 entries=[json.loads(p.read_text(encoding='utf-8')) for p in sorted(OUT.glob('figure_*/model-manifest.json'))]
 atomic_json(OUT/'manifest.json',{'schemaVersion':1,'coordinateSystem':'metres; Blender Z up/front -Y; FBX Unity Y up/front +Z','entries':entries,'requestedFigures':26,'generatedFigures':len(entries),'scope':'Editable interpreted models and reused user-supplied pirates. Not identical reconstructions or a claim of commercial art quality.'})

def build_human(index,render):
 import reference_characters_royal as royal
 return royal.build(index,render)

def main():
 WORK.mkdir(parents=True,exist_ok=True);OUT.mkdir(parents=True,exist_ok=True);REVIEW.mkdir(parents=True,exist_ok=True)
 parser=argparse.ArgumentParser();parser.add_argument('--ids',default='18');parser.add_argument('--render',action='store_true');a=parser.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [])
 ids=list(range(1,27)) if a.ids=='all' else [int(v) for v in a.ids.split(',')]
 for index in ids:
  if ENTRIES[index-1][1]=='dragon':
   import reference_characters_dragons as D;D.build(index,a.render)
  elif ENTRIES[index-1][1]=='pirate':
   import reference_characters_pirates as P;P.build(index,a.render)
  else:build_human(index,a.render)

if __name__=='__main__':main()
