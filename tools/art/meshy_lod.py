"""Write a far level for the Meshy buildings: <id>_LOD1.fbx beside each building's own FBX.

    python tools/art/meshy_lod.py [<building-id> ...] [--ratio 0.4]

The finish in meshy_building.py leaves one FBX of about 5,000 triangles per building. Zoomed out on a phone a house is
some 100 pixels tall, and 40% of those triangles draw the same picture. The far level is made from the game's own FBX,
so it stands exactly where the near one does whatever fit the finish applied. Blender welds the vertices the export
split along UV seams first (collapsing an unwelded mesh opens cracks between its pieces), collapses the mesh to the
ratio, and copies the near level's normals onto what is left, so the light does not jump when the level changes.
Walls, gates and gate leaves are left alone: at 250-1,300 triangles they are light already.

Free: no Meshy call. MeshyUnitBaker picks the file up and gives the building's prefab a LOD group.
"""
import argparse
import json
import subprocess
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import meshy_unit as meshy  # noqa: E402
import meshy_unit_finish as finish  # noqa: E402

ROOT = meshy.ROOT
FORTIFICATIONS = {'wall', 'gate', 'gate_leaf'}

BLENDER_SCRIPT = r'''
import bpy, bmesh, json, sys
jobs = json.loads(open(sys.argv[sys.argv.index('--') + 1], encoding='utf-8').read())
for job in jobs:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=job['fbx'])
    meshes = [o for o in bpy.context.scene.objects if o.type == 'MESH']
    bpy.ops.object.select_all(action='DESELECT')
    for mesh in meshes: mesh.select_set(True)
    bpy.context.view_layer.objects.active = meshes[0]
    bpy.ops.object.parent_clear(type='CLEAR_KEEP_TRANSFORM')
    if len(meshes) > 1: bpy.ops.object.join()
    near = bpy.context.view_layer.objects.active
    # Back to the Z-up metres the finish exported from, so the export below writes the same node the near FBX has.
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    for other in [o for o in bpy.context.scene.objects if o is not near]: bpy.data.objects.remove(other, do_unlink=True)
    far = near.copy(); far.data = near.data.copy(); far.name = job['id'] + '_LOD1'
    bpy.context.collection.objects.link(far)
    source = sum(len(p.vertices) - 2 for p in far.data.polygons)
    bm = bmesh.new(); bm.from_mesh(far.data)
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-4)
    bm.to_mesh(far.data); bm.free(); far.data.update()
    target = round(source * job['ratio']); triangles = source
    bpy.ops.object.select_all(action='DESELECT'); far.select_set(True); bpy.context.view_layer.objects.active = far
    for attempt in range(4):
        if triangles <= target * 1.05: break
        modifier = far.modifiers.new('far level', 'DECIMATE'); modifier.decimate_type = 'COLLAPSE'
        modifier.ratio = target / triangles; modifier.use_collapse_triangulate = True
        bpy.ops.object.modifier_apply(modifier=modifier.name)
        triangles = sum(len(p.vertices) - 2 for p in far.data.polygons)
    # A collapse puts each merged vertex where it best fits the faces around it, which can be outside the model (the
    # kingdom siege workshop's reached 0.28 m into the ground); no far vertex leaves the near level's box.
    low = [min(v.co[i] for v in near.data.vertices) for i in range(3)]; high = [max(v.co[i] for v in near.data.vertices) for i in range(3)]
    for v in far.data.vertices: v.co = [min(max(v.co[i], low[i]), high[i]) for i in range(3)]
    far.data.update()
    # Welding and collapsing leave the corners with Blender's own smooth normals; the near level's are copied back.
    transfer = far.modifiers.new('near normals', 'DATA_TRANSFER'); transfer.object = near
    transfer.use_loop_data = True; transfer.data_types_loops = {'CUSTOM_NORMAL'}; transfer.loop_mapping = 'POLYINTERP_NEAREST'
    bpy.ops.object.modifier_apply(modifier=transfer.name)
    bpy.data.objects.remove(near, do_unlink=True)
    far.select_set(True); bpy.context.view_layer.objects.active = far
    bpy.ops.export_scene.fbx(filepath=job['out'], use_selection=True, object_types={'MESH'}, apply_unit_scale=True,
        apply_scale_options='FBX_SCALE_UNITS', axis_forward='-Z', axis_up='Y', use_space_transform=True,
        bake_space_transform=False, use_mesh_modifiers=True, mesh_smooth_type='FACE', use_tspace=False,
        add_leaf_bones=False, bake_anim=False, path_mode='STRIP', embed_textures=False)
    print('MESHY_LOD ' + json.dumps({'id': job['id'], 'triangles': source, 'far': triangles}), flush=True)
'''


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('buildings', nargs='*')
    parser.add_argument('--roster', default='tools/art/meshy_buildings.json')
    parser.add_argument('--ratio', type=float, default=.4)
    args = parser.parse_args()
    roster = json.loads((ROOT / args.roster).read_text(encoding='utf-8'))
    jobs = []
    for entry in roster['buildings']:
        if args.buildings and entry['id'] not in args.buildings:
            continue
        if entry.get('base') or entry['building'] in FORTIFICATIONS:
            continue
        folder = ROOT / roster['output'] / entry['style'].capitalize() / entry['id']
        fbx = folder / (entry['id'] + '.fbx')
        if not fbx.exists():
            continue
        jobs.append({'id': entry['id'], 'fbx': str(fbx), 'out': str(folder / (entry['id'] + '_LOD1.fbx')), 'ratio': args.ratio})
    missing = set(args.buildings) - {job['id'] for job in jobs}
    if missing:
        raise SystemExit('Not a finished building with a far level to make: %s' % sorted(missing))
    raw = Path(roster['raw'])
    raw.mkdir(parents=True, exist_ok=True)
    script, listing = raw / 'far_level.py', raw / 'far_level_jobs.json'
    script.write_text(BLENDER_SCRIPT, encoding='utf-8')
    listing.write_text(json.dumps(jobs), encoding='utf-8')
    result = subprocess.run([finish.BLENDER, '-b', '--factory-startup', '-P', str(script), '--', str(listing)],
                            capture_output=True, text=True, encoding='utf-8', errors='replace')
    done = [json.loads(line[len('MESHY_LOD '):]) for line in result.stdout.splitlines() if line.startswith('MESHY_LOD ')]
    for report in done:
        print('MESHY_LOD_OK %s %d -> %d (%.0f%%)' % (report['id'], report['triangles'], report['far'], 100 * report['far'] / report['triangles']))
    if result.returncode != 0 or len(done) != len(jobs):
        sys.stderr.write(result.stdout[-3000:] + result.stderr[-3000:])
        raise SystemExit('Blender made %d of %d far levels' % (len(done), len(jobs)))


if __name__ == '__main__':
    main()
