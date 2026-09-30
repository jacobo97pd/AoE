"""Read a generated character, render real geometry, and check rig/action data."""
import bpy,sys,json,argparse
from pathlib import Path
sys.path.insert(0,str(Path(__file__).resolve().parent))
import reference_characters as R
p=argparse.ArgumentParser();p.add_argument('--id',type=int,default=18);p.add_argument('--render',action='store_true');p.add_argument('--source');a=p.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [])
path=Path(a.source) if a.source else R.REVIEW/f'figure_{a.id:02d}'/f'figure_{a.id:02d}.blend'
bpy.ops.wm.open_mainfile(filepath=str(path))
meshes=[ob for ob in bpy.context.scene.objects if ob.type=='MESH' and any(m.type=='ARMATURE' for m in ob.modifiers)]
rigs=[ob for ob in bpy.context.scene.objects if ob.type=='ARMATURE']
report={'source':str(path),'meshes':[],'rigs':[],'actions':[{'name':act.name,'range':list(act.frame_range)} for act in bpy.data.actions]}
for ob in meshes:
 ob.data.calc_loop_triangles();report['meshes'].append({'name':ob.name,'triangles':len(ob.data.loop_triangles),'vertices':len(ob.data.vertices),'unweighted':sum(1 for v in ob.data.vertices if not v.groups),'hidden':ob.hide_render,'materials':[m.name for m in ob.data.materials if m]})
for rig in rigs:report['rigs'].append({'name':rig.name,'bones':len(rig.data.bones),'names':[b.name for b in rig.data.bones]})
print('REFERENCE_VALIDATION '+json.dumps(report),flush=True)
if a.render:
 lod=next((ob for ob in meshes if ob.name.endswith('_LOD0')),max(meshes,key=lambda ob:len(ob.data.vertices)));R.render_views(lod,R.REVIEW/f'figure_{a.id:02d}',a.id in [6,7,8])
