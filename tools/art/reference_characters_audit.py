"""Validate delivered editable meshes, export completeness and sampled actions.

Run in Blender after generation. No Unity or gameplay mutation. Records include
actual weighted-vertex deformation, ground penetration and static bounds per pose.
"""
import bpy,json,sys,argparse,math
from pathlib import Path
from mathutils import Vector
sys.path.insert(0,str(Path(__file__).resolve().parent))
import reference_characters as R
p=argparse.ArgumentParser();p.add_argument('--ids',default='all');p.add_argument('--motion-renders',default='18,12,6');p.add_argument('--static-renders',default='0');a=p.parse_args(sys.argv[sys.argv.index('--')+1:])
entries=json.loads((R.OUT/'manifest.json').read_text(encoding='utf-8'))['entries']
ids={int(e['id'].split('_')[1]) for e in entries} if a.ids=='all' else {int(x) for x in a.ids.split(',')}
render_ids={int(x) for x in a.motion_renders.split(',') if x};results=[]
static_ids={int(x) for x in a.static_renders.split(',') if x}

def sampled_vertices(ob):
 deps=bpy.context.evaluated_depsgraph_get();bpy.context.view_layer.update();evaluated=ob.evaluated_get(deps);mesh=evaluated.to_mesh();coords=[evaluated.matrix_world@v.co for v in mesh.vertices];evaluated.to_mesh_clear();return coords

for entry in entries:
 index=int(entry['id'].split('_')[1])
 if index not in ids:continue
 bpy.ops.wm.open_mainfile(filepath=str(R.ROOT/entry['sourceBlendPath']))
 rig=next(ob for ob in bpy.context.scene.objects if ob.type=='ARMATURE')
 lods=sorted([ob for ob in bpy.context.scene.objects if ob.type=='MESH' and '_LOD' in ob.name and not ob.name.startswith('SOURCE_')],key=lambda ob:-len(ob.data.vertices))
 result={'id':entry['id'],'unweighted':sum(1 for ob in lods for v in ob.data.vertices if not v.groups),'nonNormalizedWeights':sum(1 for ob in lods for v in ob.data.vertices if abs(sum(g.weight for g in v.groups)-1)>.015),'lodCount':len(lods),'lodTriangles':entry['lodTriangles'],'bones':len(rig.data.bones),'animations':[],'texturesPresent':all((R.ROOT/m[key]).exists() for m in entry['materials'] for key in ['baseMap','normalMap'] if m.get(key)),'fbxPresent':(R.OUT/entry['modelPath']).exists()}
 if index in static_ids:R.render_views(lods[0],R.REVIEW/entry['id'],entry['family']=='dragon')
 elif index in render_ids:R.setup_stage(lods[0],entry['family']=='dragon')
 for state in ['Idle','Run','Fall']:
  action=bpy.data.actions.get(state)
  if not action:result['animations'].append({'state':state,'passed':False,'reason':'missing action'});continue
  rig.animation_data.action=action;start,end=action.frame_range;frames=[int(start),int(start+(end-start)*.27),int(start+(end-start)*.57),int(end)]
  minimum=100;maximum=-100;poses=[]
  for frame in frames:
   bpy.context.scene.frame_set(frame);coords=sampled_vertices(lods[0]);poses.append(coords);minimum=min(minimum,min(v.z for v in coords));maximum=max(maximum,max(v.z for v in coords))
  motion=max((poses[1][i]-poses[0][i]).length for i in range(0,len(poses[0]),max(1,len(poses[0])//3000)))
  final_height=max(v.z for v in poses[-1])-min(v.z for v in poses[-1]);initial_height=max(v.z for v in poses[0])-min(v.z for v in poses[0])
  collapsed=state!='Fall' or final_height<initial_height*.78
  result['animations'].append({'state':state,'firstFrame':float(start),'lastFrame':float(end),'sampleFrames':frames,'minimumZ':minimum,'maximumZ':maximum,'initialPoseHeight':initial_height,'finalPoseHeight':final_height,'collapsed':collapsed,'sampledVertexMotionMetres':motion,'passed':motion>.0001 and minimum>-.08 and collapsed})
  if index in render_ids and state in ['Run','Fall']:
   frame=frames[1] if state=='Run' else frames[-1];bpy.context.scene.frame_set(frame)
   coords=sampled_vertices(lods[0]);lo=Vector(tuple(min(v[i] for v in coords) for i in range(3)));hi=Vector(tuple(max(v[i] for v in coords) for i in range(3)));center=(lo+hi)*.5
   cam=bpy.context.scene.camera;cam.location=center+Vector((3,-7,3));cam.rotation_euler=(center-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.ortho_scale=max((hi-lo).z*1.25,(hi-lo).x*1.55,2.5)
   bpy.context.scene.render.filepath=str(R.REVIEW/entry['id']/('animation-'+state.lower()+'.png'));bpy.ops.render.render(write_still=True)
 result['passed']=result['unweighted']==0 and result['nonNormalizedWeights']==0 and result['lodCount']==3 and result['texturesPresent'] and result['fbxPresent'] and all(sample['passed'] for sample in result['animations'])
 results.append(result);(R.REVIEW/entry['id']/'source-validation.json').write_text(json.dumps(result,indent=2),encoding='utf-8')
 print('REFERENCE_AUDIT '+json.dumps(result),flush=True)
summary={'passed':len(results)==len(ids) and all(r['passed'] for r in results),'requested':len(ids),'inspected':len(results),'entries':results,'scope':'Blender mesh/skin/action evidence; does not certify visual likeness, physical animation quality, gameplay balance, or mobile GPU performance.'}
(R.REVIEW/'source-validation.json').write_text(json.dumps(summary,indent=2),encoding='utf-8')
if not summary['passed']:raise RuntimeError('Reference character audit failed; inspect source-validation.json')
