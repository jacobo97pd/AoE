"""Copy the user's previously approved 3D pirates without degrading their meshes."""
import bpy,shutil
from pathlib import Path
import reference_characters as R

def build(index,render=False):
 identifier='GunpowderCorsair' if index==1 else 'CrimsonCorsair'
 source=Path('D:/CodexTooling/pirate-crew/GunpowderCorsair.blend') if index==1 else Path('D:/CodexTooling/crimson-corsair/CrimsonCorsair.blend')
 R.reset();bpy.ops.wm.open_mainfile(filepath=str(source));bpy.context.preferences.filepaths.save_version=0
 rig=next(o for o in bpy.context.scene.objects if o.type=='ARMATURE')
 lods=sorted([o for o in bpy.context.scene.objects if o.type=='MESH' and any(m.type=='ARMATURE' for m in o.modifiers)],key=lambda o:-len(o.data.vertices))
 materials={m.name:m for ob in lods for m in ob.data.materials if m}
 # Retain the original complete actions; aliases share their original curves
 # through Blender copies, with no new pose synthesis or retargeting.
 actions=list(bpy.data.actions)
 for state,source_suffix in [('Idle','Idle'),('Run','Run'),('Fall','Death')]:
  act=next((a for a in actions if a.name.endswith('_'+source_suffix)),None)
  if act:
   cp=act.copy();cp.name=state;cp.use_fake_user=True;actions.append(cp)
 asset_dir='PirateCrew' if index==1 else 'CrimsonCorsair'
 source_tex=R.ROOT/f'Assets/Game/{asset_dir}/Textures'
 target=R.OUT/f'figure_{index:02d}'/'Textures';target.mkdir(parents=True,exist_ok=True)
 for name in ['BaseColor.png','Normal.png','MetallicSmoothness.png']:
  shutil.copyfile(source_tex/name,target/name)
 R.META.clear()
 for name,mat in materials.items():
  R.META[name]={'name':name,'baseColor':[1,1,1,1],'metallic':1,'smoothness':1,'baseMap':f'Assets/Game/ReferenceCharacters/SourceModels/figure_{index:02d}/Textures/BaseColor.png','normalMap':f'Assets/Game/ReferenceCharacters/SourceModels/figure_{index:02d}/Textures/Normal.png','metallicGlossMap':f'Assets/Game/ReferenceCharacters/SourceModels/figure_{index:02d}/Textures/MetallicSmoothness.png','normalScale':1,'tiling':[1,1]}
 rig.animation_data.action=next(a for a in actions if a.name=='Idle');bpy.context.scene.frame_set(1)
 # Preserve all parented empty weapon markers and authored rigs in editable
 # blend. FBX exporter only exports mesh + rig here; marker is declared below.
 return R.export_entry(index,rig,lods,actions,'pirate','ranged' if index==1 else 'hero',render,'Reuses the supplied Meshy pirate and its existing project rig, textures, mesh LODs and animations. Geometry was not simplified or replaced. Idle/Run/Fall are aliases of existing actions; this is not a newly reconstructed model from the PNG.')
