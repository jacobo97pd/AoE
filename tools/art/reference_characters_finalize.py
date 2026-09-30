"""Apply final LOD budget to early probes without resculpting or retargeting."""
import bpy,json,sys,argparse
from pathlib import Path
sys.path.insert(0,str(Path(__file__).resolve().parent))
import reference_characters as R
p=argparse.ArgumentParser();p.add_argument('--ids',default='18,9,21');p.add_argument('--render',action='store_true');a=p.parse_args(sys.argv[sys.argv.index('--')+1:])
ids=[int(v['id'].split('_')[1]) for v in json.loads((R.OUT/'manifest.json').read_text(encoding='utf-8'))['entries']] if a.ids=='all' else [int(v) for v in a.ids.split(',')]
for index in ids:
 if index<3:continue
 identifier=f'figure_{index:02d}';entry=json.loads((R.OUT/identifier/'model-manifest.json').read_text(encoding='utf-8'))
 bpy.ops.wm.open_mainfile(filepath=str(R.ROOT/entry['sourceBlendPath']));bpy.context.preferences.filepaths.save_version=0
 rig=next(ob for ob in bpy.context.scene.objects if ob.type=='ARMATURE');lod=next(ob for ob in bpy.context.scene.objects if ob.type=='MESH' and ob.name.endswith('_LOD0'))
 for ob in list(bpy.context.scene.objects):
  if ob.type=='MESH' and ob!=lod and not ob.name.startswith('SOURCE_EDITABLE_'):bpy.data.objects.remove(ob,do_unlink=True)
 R.META.clear()
 for material in entry['materials']:
  if material['name'].startswith('RS_') and material.get('metallicGlossMap'):
   material['fallbackSmoothness']=material['smoothness'];material['fallbackMetallic']=material['metallic'];material['smoothness']=1;material['metallic']=1
  R.META[material['name']]=material
 rig.animation_data.action=None;bpy.context.scene.frame_set(1)
 for action in list(bpy.data.actions):bpy.data.actions.remove(action)
 # Root local Z must coincide with world Z for authored vertical offsets and
 # its Y axis must be horizontal for the Fall roll. Preserve all child rest
 # joints while correcting early probe skeletons built with a vertical root.
 bpy.context.view_layer.objects.active=rig;rig.select_set(True);bpy.ops.object.mode_set(mode='EDIT')
 root=rig.data.edit_bones['Root'];root.tail=root.head+__import__('mathutils').Vector((0,.15,0));bpy.ops.object.mode_set(mode='OBJECT')
 lods=R.join_model([lod],identifier)
 actions=R.make_actions(rig,entry['family']=='dragon')
 R.ground_actions(rig,lods[0],actions)
 R.export_entry(index,rig,lods,actions,entry['family'],entry['role'],a.render,entry['interpretationNotes'])
