"""Use the later authored Royal Soldier anatomy/tailoring as the human baseline.

Original body.blend and armor.blend remain untouched. Distinct meshes preserve
tailoring, stitching, fabricated plate profiles and sword/shield relief detail.
"""
import bpy,json,re,shutil
from mathutils import Vector,Matrix
import reference_characters as R
K=R.K;KC=R.KC
NORMALIZE=1.18

def surface_library(family,role):
 R.palette(family,role)
 original=R.RS.register_materials()
 manifest=json.loads((R.RS.WORK/'material-manifest.json').read_text(encoding='utf-8'))
 tintmap={'RS_BlueCloth':'BlueCloth','RS_ShieldBlue':'BlueCloth','RS_Skin':'Skin','RS_Hair':'Hair','RS_HairDetail':'HairLight'}
 for source in manifest['materials']:
  name=source['name'];mat=original[name]
  record={'name':name,'baseColor':source['baseColor'],'metallic':1.0,'smoothness':1.0,'fallbackMetallic':source['metallic'],'fallbackSmoothness':source['smoothness'],'normalScale':source['normalScale'],'tiling':source['tiling'],'metallicGlossMap':'','packedMapMode':'R=absolute metallic; A=absolute smoothness; shader multipliers are one'}
  for field,old in [('baseMap','baseColorTexture'),('normalMap','normalTexture'),('metallicGlossMap','metallicSmoothnessTexture')]:
   filename=source[old].split('/')[-1];destination=R.TEX/filename
   if not destination.exists():shutil.copyfile(R.RS.WORK/'Textures'/filename,destination)
   record[field]='Assets/Game/ReferenceCharacters/SourceModels/Textures/'+filename
  if name in tintmap:
   col=R.META[tintmap[name]]['baseColor'];record['baseColor']=col;mat.diffuse_color=tuple(R.RS.srgb_to_linear(c) for c in col[:3])+(1,)
   for node in mat.node_tree.nodes:
    if node.type=='MIX_RGB' and not node.inputs[1].is_linked:node.inputs[1].default_value=mat.diffuse_color
  for node in mat.node_tree.nodes:
   if node.type=='TEX_IMAGE' and node.image:
    dest=R.TEX/node.image.name.split('.')[0]
    filename=__import__('pathlib').Path(node.image.filepath).name
    if (R.TEX/filename).exists():node.image.filepath=str(R.TEX/filename)
  R.META[name]=record

def weapon_name(name):
 return any(s in name.lower() for s in ['sword','pommel'])
def shield_name(name):
 return any(s in name.lower() for s in ['shield','heater','fleur crossbar','fleur inset perimeter'])
def helmet_name(name):
 return any(s in name.lower() for s in ['helm','sallet','cheek guard','nape rolled edge'])

def build_parts(family,role,kind):
 R.reset();KC.KIND=kind
 armor_enabled=role not in {'worker','archer','mage'}
 for source in ['body.blend','armor.blend']:
  with bpy.data.libraries.load(str(R.RS.WORK/source),link=False) as (src,dst):dst.objects=src.objects
  for ob in dst.objects:
   keep=ob.type in {'MESH','CURVE'} and not ob.name.startswith('PREVIEW_ONLY')
   if source=='armor.blend':
    if not armor_enabled:keep=False
    if weapon_name(ob.name):keep=family=='kingdom' and role in {'warrior','hero'}
    if shield_name(ob.name):keep=family=='kingdom' and role=='warrior'
    if helmet_name(ob.name):keep=family=='kingdom' and role in {'warrior','rider'}
   if source=='body.blend':
    if family=='elf' and any(s in ob.name.lower() for s in ['beard','moustache']):keep=False
    if (family!='kingdom' or role=='hero') and any(s in ob.name.lower() for s in ['cape','yoke']):keep=False
   if keep:
    bpy.context.collection.objects.link(ob);K.PARTS.append(ob);ob['existingRoyalGeometry']=True
    if source=='armor.blend':ob['royalArmor']=True
   else:bpy.data.objects.remove(ob,do_unlink=True)
 surface_library(family,role)
 bpy.context.view_layer.update()
 for ob in K.PARTS:
  for slot in ob.material_slots:
   if slot.material:
    name=re.sub(r'\.\d{3}$','',slot.material.name)
    if name in R.META:slot.material=bpy.data.materials[name]
  if ob.type=='CURVE':
   bpy.ops.object.select_all(action='DESELECT');ob.select_set(True);bpy.context.view_layer.objects.active=ob;bpy.ops.object.convert(target='MESH')
  K.apply(ob);matrix=ob.matrix_world.copy();ob.data=ob.data.copy();ob.data.transform(matrix);ob.matrix_world=Matrix.Identity(4)
  for vertex in ob.data.vertices:vertex.co/=NORMALIZE
  name=ob.name.lower()
  if weapon_name(name):ob['bindBone']='Hand.R'
  elif shield_name(name):ob['bindBone']='Hand.L'
  elif helmet_name(name) or any(s in name for s in ['head','eye','iris','pupil','brow','beard','moustache','scalp','temple']):ob['bindBone']='Head'
  elif 'cape' in name or 'yoke' in name:ob['bindBone']='Cape'
  elif any(s in name for s in ['belt','pouch','breast','cuirass','plackart','collar','tasset']):ob['bindBone']='Chest' if 'collar' in name or 'breast' in name or 'cuirass' in name else 'Hips'
  elif 'royalArmor' in ob:
   # Rigid plates never bend across an elbow or knee. Their authored center is
   # sufficient to choose the adjacent joint; cloth receives blended weights.
   center=sum((v.co for v in ob.data.vertices),Vector())/max(1,len(ob.data.vertices));side='L' if center.x>0 else 'R'
   if any(s in name for s in ['pauldron','rerebrace']):ob['bindBone']='UpperArm.'+side
   elif any(s in name for s in ['forearm','vambrace','couter','elbow']):ob['bindBone']='Forearm.'+side
   elif any(s in name for s in ['sabaton','toe cap']):ob['bindBone']='Foot.'+side
   elif any(s in name for s in ['greave','knee','poleyn']):ob['bindBone']='Shin.'+side
   elif 'thigh' in name:ob['bindBone']='Thigh.'+side
 start=len(K.PARTS)
 if family=='elf':R.elf_details(role);R.cloak(role=='archer')
 if family in {'mountain','dwarf'}:R.long_beard(family,role);R.fur_collar();R.cloak(role in {'worker','archer'})
 if family=='desert':R.turban(role);R.cloak(role=='worker')
 if role=='hero' and family=='kingdom':
  marker=len(K.PARTS);KC.crown_and_cape();R.parts_added(marker,pose='torso')
  for ob in K.PARTS[marker:]:ob['bindBone']='Head' if any(s in ob.name.lower() for s in ['crown','jewel']) else 'Cape' if 'mantle' in ob.name.lower() or 'cape edge' in ob.name.lower() else 'Chest'
 elif role=='hero' and family=='dwarf':
  marker=len(K.PARTS);KC.helmet();R.parts_added(marker,'Head','head')
  for j in range(7):
   a=R.pi+j*R.pi/6;K.tube('Thane crown spike',(.1*R.cos(a),-.03+.11*R.sin(a),1.70),(.115*R.cos(a),-.03+.12*R.sin(a),1.82-abs(3-j)*.016),.013,'Gold',.001,16)['bindBone']='Head'
 elif family=='kingdom' and role=='worker':
  marker=len(K.PARTS);KC.straw_hat();R.parts_added(marker,'Head','head')
 elif family=='dwarf' and role=='worker':
  marker=len(K.PARTS);KC.helmet();R.disk('Miner headlamp',(0,-.17,1.665),.028,.009,'Gold');R.disk('Miner glass',(0,-.181,1.665),.020,.005,'Gem');R.parts_added(marker,'Head','head')
 if role=='worker':R.worker_pack(family)
 if role in {'pikeman','rider'}:R.weapon('pike')
 elif role=='worker':R.weapon('pitchfork' if family=='kingdom' else 'pick')
 elif role=='archer':R.weapon('bow')
 elif role=='mage':R.weapon('staff')
 elif family=='dwarf':R.weapon('hammer')
 elif family=='mountain':R.weapon('axe')
 elif family=='desert':R.weapon('scimitar')
 elif family=='elf':R.weapon('sword')
 if family!='kingdom' and (role in {'warrior','guard'} or (family=='dwarf' and role=='hero')):R.shield(family)
 for ob in K.PARTS[start:]:
  K.apply(ob);matrix=ob.matrix_world.copy();ob.data.transform(matrix);ob.matrix_world=Matrix.Identity(4)
  # The auxiliary sculpting functions use the earlier anatomical frame. Fit
  # their hand-bound equipment to the newer tailored body's actual grip.
  if ob.get('bindBone') in {'Hand.R','Hand.L'}:
   side=-1 if ob['bindBone']=='Hand.R' else 1
   prior=KC.limb_transform((side*.405,-.10,.818),side);prior.x+=.015 if side<0 else -.015;prior.y-=.014
   target=Vector((-.385,-.215,1.065) if side<0 else (.28,-.32,1.30))/NORMALIZE
   delta=target-prior
   for v in ob.data.vertices:v.co+=delta
 for ob in K.PARTS:
  bone=ob.get('bindBone')
  anchor=Vector((-.385,-.215,1.065) if bone=='Hand.R' else (.28,-.32,1.30))/NORMALIZE
  for v in ob.data.vertices:
   v.co=R.transform_point(anchor,family)+(v.co-anchor)*(R.SCALE*1.08) if family=='dwarf' and bone in {'Hand.R','Hand.L'} else R.transform_point(v.co,family)
 return K.PARTS[:]

def bones(family):
 raw=[('Root',(0,0,0),(0,0,.15),None),('Hips',(0,.015,1.07),(0,0,1.25),'Root'),('Spine',(0,0,1.25),(0,0,1.47),'Hips'),('Chest',(0,0,1.47),(0,0,1.66),'Spine'),('Head',(0,.01,1.66),(0,.0,1.99),'Chest'),('Cape',(0,.15,1.61),(0,.24,.50),'Chest')]
 for side,label in [(1,'L'),(-1,'R')]:
  shoulder=(side*.27,0,1.60);elbow=(-.36,-.015,1.30) if side<0 else (.37,0,1.34);wrist=(-.38,-.15,1.10) if side<0 else (.28,-.28,1.32);hand=(-.385,-.215,1.065) if side<0 else (.28,-.32,1.30);advance=-.10 if side<0 else .06;kneey=-.07 if side<0 else .045
  raw += [('UpperArm.'+label,shoulder,elbow,'Chest'),('Forearm.'+label,elbow,wrist,'UpperArm.'+label),('Hand.'+label,wrist,hand,'Forearm.'+label),('Thigh.'+label,(side*.12,.014,1.06),(side*.15,kneey,.59),'Hips'),('Shin.'+label,(side*.15,kneey,.59),(side*.16,advance,.15),'Thigh.'+label),('Foot.'+label,(side*.16,advance,.15),(side*.16,advance-.18,.06),'Shin.'+label)]
 return [(n,R.transform_point(Vector(h)/NORMALIZE,family),R.transform_point(Vector(t)/NORMALIZE,family),p) for n,h,t,p in raw]

def build(index,render=False):
 name,family,role,kind=R.ENTRIES[index-1];print('REFERENCE_BUILD_PARTS',index,flush=True);parts=build_parts(family,role,kind)
 print('REFERENCE_SKIN',index,len(parts),flush=True);rig=R.create_rig(bones(family));R.skin_parts(parts,rig,family)
 print('REFERENCE_LODS',index,flush=True);lods=R.join_model(parts,f'figure_{index:02d}')
 print('REFERENCE_ANIMATION',index,flush=True);actions=R.make_actions(rig);R.ground_actions(rig,lods[0],actions)
 return R.export_entry(index,rig,lods,actions,family,role,render,'Editable 3D interpretation using the later Royal Soldier anatomy, fabric folds, stitches, layered leather, fabricated plate profiles and relief equipment. Family-specific proportions, beards, cloth, headwear and tools are added as geometry. It remains below the illustration in likeness and microdetail; no identical reconstruction or commercial-quality animation is claimed.')
