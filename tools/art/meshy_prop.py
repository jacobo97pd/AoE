"""Generate environment props from tools/art/meshy_props.json with Meshy text-to-3D, then finish them for the game.

    python tools/art/meshy_prop.py <prop-id> [<prop-id> ...] [--max-credits 60] [--preview-only] [--finish-only]
    python tools/art/meshy_prop.py --priority 1 --max-credits 160
    python tools/art/meshy_prop.py --priority 6 --dry-run      # free: budgets, prompt lengths and the credits a run needs

Two paid steps per prop, as for the buildings: a Smart Topology preview (5 credits) and a refine that paints PBR
textures on it (10). --preview-only stops after the first, so a cheap probe can be judged before paying for texture.
Each step is recorded before the next starts, so a rerun resumes instead of paying twice. A step that was paid for but
whose download broke (an SSL drop) is fetched again from its task on the next run, for free. Credits are counted at the
list price, not by reading the balance, so two batches can run side by side without confusing each other.

An entry with "source": "<other id>" draws that prop's model in another biome (the highland reuses the forest pine and
resource nodes): the source's paid files are linked into its raw folder and it is finished under its own id, with no
Meshy call. The source must have been generated first.

The finish is free. Blender joins the parts and centres them on the ground. It fits the prop to its height and footprint
in the vertices themselves, because the game scales instances unevenly and the felling sway reads object-space height
in metres. It then decimates to the roster's lod0 (and lod1 where there is one) and writes one FBX holding <id>_LOD0 and
<id>_LOD1; a farShell entry's LOD1 is a closed skin around the model instead (see shell below). Textures are cut to the
roster size; siege props get the owner-colour mask like the units. An entry's "recolour" bands pull hue ranges of the
base colour toward roster colours where Meshy painted them off (see recolour), and a "crater" entry gets its summit bowl
painted molten where Meshy left it rock-coloured (see paint_crater).
"""
import argparse
import json
import os
import shutil
import subprocess
import sys
from datetime import datetime, timezone
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import meshy_unit as meshy  # noqa: E402
import meshy_unit_finish as finish  # noqa: E402

ROOT = meshy.ROOT
COST = {'preview': 5, 'refine': 10}

BLENDER_SCRIPT = r'''
import bpy, bmesh, json, sys
import numpy as np
args = json.loads(sys.argv[sys.argv.index('--') + 1])
bpy.ops.wm.read_factory_settings(use_empty=True)
# Merged, the mesh stays closed along its UV seams, so decimating it cannot open cracks between the pieces.
bpy.ops.import_scene.gltf(filepath=args['glb'], merge_vertices=True)
meshes = [o for o in bpy.context.scene.objects if o.type == 'MESH']
bpy.ops.object.select_all(action='DESELECT')
for mesh in meshes: mesh.select_set(True)
bpy.context.view_layer.objects.active = meshes[0]
bpy.ops.object.parent_clear(type='CLEAR_KEEP_TRANSFORM')
if len(meshes) > 1: bpy.ops.object.join()
obj = bpy.context.view_layer.objects.active
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
if args.get('trim'):
    # Meshy built the highland massif on a square terrain tile with cut sides, which read from the game camera as a
    # square slab. trim cuts that share of the height off the bottom before the fit: the ground then meets the slopes
    # along their own ragged contour. The baker grounds by the lowest vertex, so sinking the model would not survive.
    cut_mesh = bmesh.new(); cut_mesh.from_mesh(obj.data)
    heights = [v.co.z for v in cut_mesh.verts]
    level = min(heights) + (max(heights) - min(heights)) * args['trim']
    bmesh.ops.bisect_plane(cut_mesh, geom=cut_mesh.verts[:] + cut_mesh.edges[:] + cut_mesh.faces[:], plane_co=(0, 0, level),
                           plane_no=(0, 0, 1), clear_inner=True)
    # The cut leaves pebbles of the old skirt standing apart, which drew as dark specks around the foot.
    seen, parts = set(), []
    for face in cut_mesh.faces:
        if face in seen: continue
        seen.add(face); stack, part = [face], []
        while stack:
            current = stack.pop(); part.append(current)
            for edge in current.edges:
                for other in edge.link_faces:
                    if other not in seen: seen.add(other); stack.append(other)
        parts.append(part)
    largest = max(len(part) for part in parts)
    bmesh.ops.delete(cut_mesh, geom=[f for part in parts if len(part) < largest * .02 for f in part], context='FACES')
    cut_mesh.to_mesh(obj.data); cut_mesh.free(); obj.data.update()
points = np.empty(len(obj.data.vertices) * 3, dtype=np.float32)
obj.data.vertices.foreach_get('co', points); points = points.reshape(-1, 3)
if args.get('yaw'):
    a = np.radians(args['yaw']); c, s = np.cos(a), np.sin(a)
    points = points @ np.array([[c, s, 0], [-s, c, 0], [0, 0, 1]], np.float32)
low, high = points.min(0), points.max(0); size = high - low
# Blender is Z-up. Height is the target; the footprint only caps the wider side.
scale = min(args['height'] / size[2], args['footprint'] / max(size[0], size[1]))
# 'box' fits height and footprint separately: a broad crown on a one-metre cell grows taller and narrower instead of
# shrinking to a bush or spilling over the walkable cells around it.
scale = np.array([args['footprint'] / max(size[0], size[1])] * 2 + [args['height'] / size[2]], np.float32) if args.get('fit') == 'box' else scale
points = (points - np.array([(low[0] + high[0]) / 2, (low[1] + high[1]) / 2, low[2]], np.float32)) * scale
obj.data.vertices.foreach_set('co', points.ravel()); obj.data.update()
source = sum(len(p.vertices) - 2 for p in obj.data.polygons)
crater = []
if args.get('crater'):
    # Meshy painted the volcano's crater bowl the colour of its rock, so the summit read as a dark pit. The crater is
    # found by shape: the rim is the innermost face in the top share of the height, and the faces inside that radius
    # (with some reach) and above the depth share are the bowl. Their texture islands are painted molten after the
    # export (paint_crater), hotter toward the floor.
    c = args['crater']
    centres = np.array([p.center[:] for p in obj.data.polygons], np.float32).reshape(-1, 3)
    level = centres[:, 2] / float(points[:, 2].max())
    radius = np.hypot(centres[:, 0], centres[:, 1]) / float(points[:, 2].max())
    rim = radius[level > 1 - c.get('rim', .05)].min()
    inside = np.nonzero((radius < rim * c.get('reach', 1.15)) & (level > 1 - c.get('depth', .25)))[0]
    if len(inside):
        floor, lip = level[inside].min(), level[inside].max()
        loops = obj.data.uv_layers.active.data
        crater = [{'uv': [[round(v, 5) for v in loops[l].uv] for l in obj.data.polygons[i].loop_indices],
                   'heat': round(float((lip - level[i]) / max(lip - floor, 1e-6)), 3)} for i in inside]

def collapse(copy, budget):
    triangles = sum(len(p.vertices) - 2 for p in copy.data.polygons)
    # Later passes ask again for what is still over when the first one stops more than 5% short.
    for attempt in range(4):
        if not budget or triangles <= (budget if attempt == 0 else budget * 1.05): break
        bpy.ops.object.select_all(action='DESELECT'); copy.select_set(True); bpy.context.view_layer.objects.active = copy
        modifier = copy.modifiers.new('budget', 'DECIMATE'); modifier.decimate_type = 'COLLAPSE'
        modifier.ratio = budget / triangles; modifier.use_collapse_triangulate = True
        bpy.ops.object.modifier_apply(modifier=modifier.name)
        triangles = sum(len(p.vertices) - 2 for p in copy.data.polygons)
    return triangles

def shell(copy, budget):
    # A Meshy tree's crown is a hundred loose leaf shards of three to eight triangles (a berry bush's, 150). Collapse
    # cannot take a shard below itself, so a 40% far level flattened them into slivers and the crown drew as shreds
    # with holes. For the entries the roster marks farShell the far level is a new closed skin around the shards:
    # thickened so every shard has volume, voxel-remeshed at 4% of the height, collapsed to the budget, then textured
    # and shaded from the source at each point.
    bpy.ops.object.select_all(action='DESELECT'); copy.select_set(True); bpy.context.view_layer.objects.active = copy
    voxel = args['height'] * .04
    solid = copy.modifiers.new('volume', 'SOLIDIFY'); solid.thickness = voxel * .66; solid.offset = 0
    bpy.ops.object.modifier_apply(modifier=solid.name)
    remesh = copy.modifiers.new('skin', 'REMESH'); remesh.mode = 'VOXEL'; remesh.voxel_size = voxel
    bpy.ops.object.modifier_apply(modifier=remesh.name)
    triangles = collapse(copy, budget)
    if not copy.data.uv_layers: copy.data.uv_layers.new(name=obj.data.uv_layers[0].name)
    for kind in ('UV', 'CUSTOM_NORMAL'):
        transfer = copy.modifiers.new('near ' + kind, 'DATA_TRANSFER'); transfer.object = obj
        transfer.use_loop_data = True; transfer.data_types_loops = {kind}; transfer.loop_mapping = 'POLYINTERP_NEAREST'
        bpy.ops.object.modifier_apply(modifier=transfer.name)
    if args.get('shellUv') == 'face':
        # Each corner above takes its own nearest point, so a shell face spanning rocks painted in different atlas
        # islands interpolates across the atlas and draws stripes (the volcanic ore came out striped red). shellUv
        # "face" gives each shell face the texture of the one source triangle nearest its centre instead.
        from mathutils.bvhtree import BVHTree
        from mathutils.geometry import barycentric_transform, closest_point_on_tri
        from mathutils import Vector
        src = bmesh.new(); src.from_mesh(obj.data); bmesh.ops.triangulate(src, faces=src.faces[:])
        src.faces.ensure_lookup_table(); tree = BVHTree.FromBMesh(src); suv = src.loops.layers.uv.active
        dst = bmesh.new(); dst.from_mesh(copy.data); duv = dst.loops.layers.uv.active
        for face in dst.faces:
            tri = src.faces[tree.find_nearest(face.calc_center_median())[2]]
            corners = [l.vert.co for l in tri.loops]
            uvs = [Vector((l[suv].uv.x, l[suv].uv.y, 0)) for l in tri.loops]
            for loop in face.loops:
                uv = barycentric_transform(closest_point_on_tri(loop.vert.co, *corners), *corners, *uvs)
                loop[duv].uv = (uv.x, uv.y)
        dst.to_mesh(copy.data); src.free(); dst.free()
    return triangles

def decimated(name, budget, far=False):
    copy = obj.copy(); copy.data = obj.data.copy(); copy.name = name
    bpy.context.collection.objects.link(copy)
    shards = far and args.get('shell')
    return copy, shell(copy, budget) if shards else collapse(copy, budget), shards

lod0, t0, _ = decimated(args['id'] + '_LOD0', args['lod0'])
parts = [lod0]; report = {'sourceTriangles': source, 'lod0': t0}
if crater: report['crater'] = crater
if args.get('lod1'):
    lod1, t1, shards = decimated(args['id'] + '_LOD1', args['lod1'], True); parts.append(lod1); report['lod1'] = t1
    if shards: report['lod1Shell'] = True
bpy.data.objects.remove(obj, do_unlink=True)
bpy.ops.object.select_all(action='DESELECT')
for part in parts: part.select_set(True)
bpy.context.view_layer.objects.active = parts[0]
bpy.ops.export_scene.fbx(filepath=args['fbx'], use_selection=True, object_types={'MESH'}, apply_unit_scale=True,
    apply_scale_options='FBX_SCALE_UNITS', axis_forward='-Z', axis_up='Y', use_space_transform=True,
    bake_space_transform=False, use_mesh_modifiers=True, mesh_smooth_type='FACE', use_tspace=False,
    add_leaf_bones=False, bake_anim=False, path_mode='STRIP', embed_textures=False)
report['fittedMetres'] = [round(float(v), 3) for v in size * scale]
print('MESHY_PROP ' + json.dumps(report))
'''


def paths(roster, entry):
    out = ROOT / roster['output'] / entry['biome'] / entry['id']
    raw = Path(os.path.expandvars(os.path.expanduser(roster['raw']))) / entry['id']
    out.mkdir(parents=True, exist_ok=True)
    raw.mkdir(parents=True, exist_ok=True)
    return out, raw


def generate(entry, roster, key, budget, preview_only):
    out, raw = paths(roster, entry)
    manifest_path = out / 'meshy-manifest.json'
    manifest = json.loads(manifest_path.read_text(encoding='utf-8')) if manifest_path.exists() else {
        'id': entry['id'], 'entry': entry, 'steps': {}, 'files': {}, 'credits': 0}

    def run(step, body):
        if budget['spent'] + COST[step] > budget['cap']:
            raise SystemExit('Stopping before %s %s: the batch would pass its cap of %d credits.' % (entry['id'], step, budget['cap']))
        task_id = meshy.call('POST', '/v2/text-to-3d', key, body)['result']
        print('%s %s task %s' % (entry['id'], step, task_id), flush=True)
        task = meshy.wait(key, '/v2/text-to-3d/' + task_id, entry['id'] + ' ' + step)
        budget['spent'] += COST[step]
        manifest['credits'] += COST[step]
        manifest['steps'][step] = {'task': meshy.strip(task), 'credits': COST[step],
                                   'finished_utc': datetime.now(timezone.utc).isoformat()}
        meshy.save(manifest, manifest_path)
        return task

    def fetch_preview(task):
        for where, url in meshy.urls(task):
            if where.startswith('thumbnail'):
                manifest['files']['preview_thumbnail'] = meshy.download(url, raw / 'preview.png')
        meshy.save(manifest, manifest_path)

    def fetch_refine(task):
        for where, url in meshy.urls(task):
            name = where.split('.')[-1]
            if where.startswith('model_urls.') and url.split('?', 1)[0].endswith('.glb'):
                manifest['files']['model_glb'] = meshy.download(url, raw / (entry['id'] + '_model.glb'))
            elif where.startswith('texture_urls.'):
                manifest['files']['texture_' + name] = meshy.download(url, raw / ('%s_%s.png' % (entry['id'], name)))
            elif where.startswith('thumbnail'):
                manifest['files']['thumbnail'] = meshy.download(url, raw / 'thumbnail.png')
        meshy.save(manifest, manifest_path)

    def again(step):
        # The step was paid and recorded, but its download broke: the finished task is fetched again, which is free.
        print('%s %s fetched again from task %s (free)' % (entry['id'], step, manifest['steps'][step]['task']['id']), flush=True)
        return meshy.call('GET', '/v2/text-to-3d/' + manifest['steps'][step]['task']['id'], key)

    if 'preview' not in manifest['steps']:
        fetch_preview(run('preview', {'mode': 'preview', 'prompt': entry['prompt'][:600], 'model_type': 'smart-topology',
                                      'ai_model': 'meshy-t2', 'target_polycount': entry['polycount'], 'target_formats': ['glb']}))
    elif not (raw / 'preview.png').exists():
        # Only a thumbnail for judging a probe: an expired task must not stop the model from being finished.
        try:
            fetch_preview(again('preview'))
        except (RuntimeError, OSError) as error:
            print('%s preview thumbnail not fetched again: %s' % (entry['id'], error), flush=True)
    if preview_only:
        return manifest
    if 'refine' not in manifest['steps']:
        fetch_refine(run('refine', {'mode': 'refine', 'preview_task_id': manifest['steps']['preview']['task']['id'],
                                    'enable_pbr': True, 'texture_prompt': entry['texture'][:600], 'target_formats': ['glb']}))
    elif not all((raw / ('%s_%s' % (entry['id'], name))).exists() for name in REFINED):
        fetch_refine(again('refine'))
    return manifest


# What a refine leaves in the raw folder, and what the finish reads from it.
REFINED = ('model.glb', 'base_color.png', 'normal.png', 'metallic.png', 'roughness.png')


def reuse(entry, roster):
    """Link the source prop's paid files into this entry's raw folder under this entry's id. Free: no Meshy call."""
    source = next((p for p in roster['props'] if p['id'] == entry['source']), None)
    if source is None or source.get('source'):
        raise SystemExit('%s: source %r is not a generated prop in the roster' % (entry['id'], entry['source']))
    out, raw = paths(roster, entry)
    source_raw = Path(os.path.expandvars(os.path.expanduser(roster['raw']))) / source['id']
    for name in REFINED:
        have, want = source_raw / ('%s_%s' % (source['id'], name)), raw / ('%s_%s' % (entry['id'], name))
        if not have.exists():
            raise RuntimeError('%s has no %s yet: generate %s first' % (source['id'], name, source['id']))
        if want.exists() and want.stat().st_size == have.stat().st_size:
            continue
        if want.exists():
            want.unlink()
        try:
            os.link(have, want)
        except OSError:
            shutil.copyfile(have, want)
    manifest_path = out / 'meshy-manifest.json'
    if not manifest_path.exists():
        meshy.save({'id': entry['id'], 'entry': entry, 'source': source['id'], 'steps': {}, 'files': {}, 'credits': 0}, manifest_path)


def recolour(entry, out):
    """Pull each "recolour" band of the base colour toward the roster's mean colour, keeping the painted shading.

    Meshy paints to the texture prompt's hues only loosely: the elven gold crowns came out pumpkin orange, the teal
    crowns a near-white mint that read as frost, the silver trunks dark slate. A band is {"hues": [from, to] in
    degrees, "saturation": the grey gate (default 0.08), "mean": "#rrggbb"}; its pixels are scaled so their mean
    lands on the target, like the ground layers are pulled to theirs. Free, and redone by every finish.
    """
    if not entry.get('recolour'):
        return []
    import numpy as np
    from PIL import Image
    path = out / ('%s_BaseColor.png' % entry['id'])
    rgba = np.asarray(Image.open(path).convert('RGBA')).astype(np.float32) / 255
    rgb = rgba[..., :3]
    high, low = rgb.max(axis=2), rgb.min(axis=2)
    chroma = high - low
    saturation = np.where(high > 0, chroma / np.maximum(high, 1e-6), 0)
    r, g, b = rgb[..., 0], rgb[..., 1], rgb[..., 2]
    safe = np.maximum(chroma, 1e-6)
    hue = np.where(high == r, ((g - b) / safe) % 6, np.where(high == g, (b - r) / safe + 2, (r - g) / safe + 4)) * 60
    result, done = rgb.copy(), []
    for band in entry['recolour']:
        inside = (hue >= band['hues'][0]) & (hue <= band['hues'][1]) & (saturation >= band.get('saturation', .08))
        if not inside.any():
            continue
        target = np.array([int(band['mean'][i:i + 2], 16) for i in (1, 3, 5)], np.float32) / 255
        mean = rgb[inside].mean(axis=0)
        result[inside] = np.clip(rgb[inside] * (target / np.maximum(mean, 1e-3)), 0, 1)
        done.append({'hues': band['hues'], 'pixels': round(float(inside.mean()), 3),
                     'from': '#%02x%02x%02x' % tuple(int(v * 255 + .5) for v in mean), 'to': band['mean']})
    rgba[..., :3] = result
    Image.fromarray((rgba * 255 + .5).astype(np.uint8), 'RGBA').save(path)
    return done


def paint_crater(entry, out, faces):
    """Paint the crater faces the Blender finish found (their UV polygons and a heat, 0 at the lip, 1 on the floor).

    "crater": {"rim": top share searched for the rim (.05), "reach": radius over the rim's (1.15), "depth": share of
    the height below the top that still counts as bowl (.25), "hot"/"warm"/"edge": floor, middle and lip colours}. The
    painted detail is kept as brightness, and the colours are bright saturated orange, so a colour-keyed emissive shader
    lights the pool like the rest of the land's glow. Free, and redone by every finish. Returns the painted share.
    """
    import numpy as np
    from PIL import Image, ImageDraw, ImageFilter
    c = entry['crater']
    path = out / ('%s_BaseColor.png' % entry['id'])
    image = Image.open(path).convert('RGBA')
    width, height = image.size
    heat = Image.new('F', (width, height), -1.0)
    draw = ImageDraw.Draw(heat)
    for face in faces:
        draw.polygon([(u * width, (1 - v) * height) for u, v in face['uv']], fill=face['heat'])
    heat = np.asarray(heat)
    painted = heat >= 0
    # One texel of margin so filtering does not pull the rock colour in along the islands' edges. Two bled orange
    # specks into the flank islands packed next to them.
    grown = np.asarray(Image.fromarray((painted * 255).astype(np.uint8)).filter(ImageFilter.MaxFilter(3))) > 0
    spread = np.asarray(Image.fromarray(np.where(painted, heat, 0).astype(np.float32)).filter(ImageFilter.MaxFilter(3)))
    heat = np.clip(np.where(painted, heat, spread), 0, 1)[..., None]
    rgba = np.asarray(image).astype(np.float32) / 255
    rgb = rgba[..., :3]
    luminance = rgb @ np.array([.2126, .7152, .0722], np.float32)
    detail = (luminance - luminance[grown].min()) / max(1e-3, float(np.ptp(luminance[grown])))
    hot, warm, edge = (np.array([int(c.get(k, d)[i:i + 2], 16) for i in (1, 3, 5)], np.float32) / 255
                       for k, d in (('hot', '#FFC24A'), ('warm', '#F07A2E'), ('edge', '#A8321E')))
    lava = np.where(heat > .5, warm + (hot - warm) * (heat - .5) * 2, edge + (warm - edge) * heat * 2) * (.8 + .4 * detail[..., None])
    rgba[..., :3] = np.where(grown[..., None], np.clip(lava, 0, 1), rgb)
    Image.fromarray((rgba * 255 + .5).astype(np.uint8), 'RGBA').save(path)
    return round(float(grown.mean()), 4)


def finish_prop(entry, roster):
    out, raw = paths(roster, entry)
    manifest_path = out / 'meshy-manifest.json'
    manifest = json.loads(manifest_path.read_text(encoding='utf-8'))
    # The culture ladders dye their cloth like the culture's units, so the owner-colour gate travels with the hues
    # (the mountain clans' dull woad blue needs a lower saturation gate than the kingdom's royal blue).
    coverage = finish.textures({'id': entry['id'], 'teamHues': entry.get('teamHues', []), 'teamSaturation': entry.get('teamSaturation', .28),
                                'teamWeight': entry.get('teamWeight', 1)}, raw, out, entry.get('size', 512))
    recoloured = recolour(entry, out)
    script = raw / 'finish_prop.py'
    script.write_text(BLENDER_SCRIPT, encoding='utf-8')
    args = {'id': entry['id'], 'glb': str(raw / (entry['id'] + '_model.glb')), 'fbx': str(out / (entry['id'] + '.fbx')),
            'height': entry['height'], 'footprint': entry['footprint'], 'lod0': entry['lod0'], 'lod1': entry.get('lod1', 0),
            'yaw': entry.get('yaw', 0), 'fit': entry.get('fit', 'uniform'), 'shell': entry.get('farShell', False),
            'shellUv': entry.get('shellUv'), 'trim': entry.get('trim', 0), 'crater': entry.get('crater')}
    result = subprocess.run([finish.BLENDER, '-b', '--factory-startup', '-P', str(script), '--', json.dumps(args)],
                            capture_output=True, text=True, encoding='utf-8', errors='replace')
    line = next((l for l in result.stdout.splitlines() if l.startswith('MESHY_PROP ')), None)
    if result.returncode != 0 or line is None:
        sys.stderr.write(result.stdout[-3000:] + result.stderr[-3000:])
        raise SystemExit('Blender failed for %s' % entry['id'])
    report = json.loads(line[len('MESHY_PROP '):])
    report['teamMaskCoverage'] = round(coverage, 4)
    if recoloured:
        report['recoloured'] = recoloured
    crater = report.pop('crater', None)
    if crater:
        report['crater'] = {'faces': len(crater), 'painted': paint_crater(entry, out, crater)}
    manifest['finish'] = report
    meshy.save(manifest, manifest_path)
    print('MESHY_PROP_OK %s %s credits=%s' % (entry['id'], json.dumps(report), manifest['credits']), flush=True)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('props', nargs='*')
    parser.add_argument('--priority', type=int)
    parser.add_argument('--roster', default='tools/art/meshy_props.json')
    parser.add_argument('--max-credits', type=int, default=30)
    parser.add_argument('--preview-only', action='store_true')
    parser.add_argument('--finish-only', action='store_true')
    parser.add_argument('--dry-run', action='store_true')
    args = parser.parse_args()
    roster = json.loads((ROOT / args.roster).read_text(encoding='utf-8'))
    entries = [p for p in roster['props'] if p['id'] in args.props or (args.priority and p['priority'] == args.priority)]
    missing = set(args.props) - {p['id'] for p in entries}
    if missing:
        raise SystemExit('Not in the roster: %s' % sorted(missing))
    print('%d props, cap %d credits' % (len(entries), args.max_credits))
    if args.dry_run:
        dry_run(entries, roster, args)
        return
    paid = [e for e in entries if not e.get('source')]
    key = None if args.finish_only or not paid else meshy.api_key()
    budget = {'spent': 0, 'cap': args.max_credits}
    for entry in entries:
        try:
            if entry.get('source'):
                reuse(entry, roster)
            elif not args.finish_only:
                generate(entry, roster, key, budget, args.preview_only)
            if not args.preview_only:
                finish_prop(entry, roster)
        # OSError covers a download that broke after paying: the step is recorded, and the next run fetches it free.
        except (RuntimeError, OSError) as error:
            print('MESHY_PROP_FAILED %s %s' % (entry['id'], error), flush=True)
    print('MESHY_PROPS_DONE spent=%d' % budget['spent'])


def dry_run(entries, roster, args):
    """Check each entry against the game's rules (MeshyPropVisualsTests, the phone LODs) and price the run."""
    total, problems = 0, []
    for entry in entries:
        role, kind, lod0, lod1 = entry['role'], entry['kind'], entry['lod0'], entry.get('lod1', 0)
        out = ROOT / roster['output'] / entry['biome'] / entry['id'] / 'meshy-manifest.json'
        manifest = json.loads(out.read_text(encoding='utf-8')) if out.exists() else {}
        steps, fitted = manifest.get('steps', {}), manifest.get('finish', {}).get('fittedMetres')
        issues = []
        if role == 'obstacle':
            if lod0 > 700:
                issues.append('an obstacle is drawn by the thousand: lod0 %d is over 700' % lod0)
            if not lod1 or not .35 <= lod1 / lod0 <= .55:
                issues.append('the far level must be 35-55%% of lod0 (lod1 %s)' % lod1)
            if kind in ('thicket', 'palm'):
                # fit box makes the ratio height/footprint exactly; a uniform fit keeps the model's own, known after the finish.
                ratio = entry['height'] / entry['footprint'] if entry.get('fit') == 'box' else fitted[2] / max(fitted[:2]) if fitted else None
                if ratio is not None and ratio <= 1.55:
                    issues.append('height/width %.2f: a %s must stand taller than 1.5x its width' % (ratio, kind))
            elif entry['height'] >= 1.1:
                issues.append('a %s obstacle must stay under 1.1 m tall' % kind)
        elif lod0 > 4500:
            issues.append('lod0 %d is over the 4,500 the tests allow' % lod0)
        if entry.get('source'):
            source = next((p for p in roster['props'] if p['id'] == entry['source']), None)
            glb = Path(os.path.expandvars(os.path.expanduser(roster['raw']))) / entry['source'] / (entry['source'] + '_model.glb')
            if source is None:
                issues.append('source %s is not in the roster' % entry['source'])
            state, cost = 'reuses %s%s' % (entry['source'], '' if glb.exists() else ' (not generated yet)'), 0
        else:
            for field in ('prompt', 'texture'):
                if len(entry[field]) > 600:
                    issues.append('%s is %d characters; Meshy cuts at 600' % (field, len(entry[field])))
            cost = sum(COST[s] for s in (('preview',) if args.preview_only else ('preview', 'refine')) if s not in steps)
            state = 'paid' if not cost else '%d credits' % cost
        total += cost
        print('  %-26s %-8s %-15s %-9s lod %4d/%-4d %4.1f m x %4.1f m%s  %s' % (
            entry['id'], role, kind, entry['biome'], lod0, lod1, entry['height'], entry['footprint'],
            ' shell' if entry.get('farShell') else '      ', state))
        problems += ['%s: %s' % (entry['id'], issue) for issue in issues]
    for problem in problems:
        print('  PROBLEM ' + problem)
    print('MESHY_PROPS_DRY_RUN credits=%d cap=%d problems=%d%s' % (total, args.max_credits, len(problems),
                                                                   '' if total <= args.max_credits else ' OVER THE CAP'))


if __name__ == '__main__':
    main()
