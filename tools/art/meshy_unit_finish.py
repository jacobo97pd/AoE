"""Turn a unit's raw Meshy downloads into what the game ships. Free: no Meshy call.

    python tools/art/meshy_unit_finish.py <unit-id> [<unit-id> ...] [--roster tools/art/meshy_units.json]

For each unit, from the raw folder that meshy_unit.py filled:

* <id>.fbx -- the rigged mesh with every clip as its own take (Idle, Walk, Run, Attack, Hit, Death, and Work or Aim
  where the role has one, and Climb for the wall-boarding infantry once meshy_climb.py has fetched it). Blender merges
  the rig's walking and running with the library clips; no textures embedded. A roster entry with "clipsFrom" (the
  raw folder of another figure rigged by Meshy) brings that figure's library clips and climb over onto this rig
  instead (tools/art/meshy_retarget_blender.py); the walking and running are always the rig's own.
* <id>_BaseColor.png -- 1024, with the owner-colour mask in alpha: cloth whose hue falls in the roster's teamHues,
  and/or the surface inside its teamBoxes (for a figure whose cloth shares its hue with the rest of it), narrowed by
  teamValue to the cloth's own brightness.
* <id>_Normal.png -- 1024.
* <id>_Mask.png -- 1024, metallic in red and smoothness (1 - roughness) in alpha, as the Meshy Unit shader reads it.

Run under plain Python; it calls Blender itself for the FBX.
"""
import argparse
import json
import os
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
BLENDER = os.environ.get('BLENDER', r'D:\CodexTooling\blender-portable\blender-4.5.13-windows-x64\blender.exe')
# Library names Meshy gives the takes, mapped to the states CorsairAnimationDriver plays.
STATES = {
    'Idle': 'Idle', 'Attack': 'Attack', 'Dead': 'Death', 'Hit_Reaction': 'Hit', 'Heavy_Hammer_Swing': 'Attack',
    'Thrust_Slash': 'Attack', 'Charged_Axe_Chop': 'Work', 'Charged_Spell_Cast': 'Attack',
    'Archery_Shot': 'Attack', 'Archery_Aim_with_Lateral_Scan': 'Aim', 'Hit_Reaction_with_Bow': 'Hit',
    'Shot_and_Fall_Forward': 'Death', 'Ladder_Climb_Loop': 'Climb',
}
SIZE = 1024


def reference_median(entry):
    """Median luminance (0..1, as stored) over the figure's pixels in the user's own pictures, front and back (the
    roster's "compare" list, or else the pictures Meshy was shown)."""
    import numpy as np
    from PIL import Image
    from scipy import ndimage
    from skin_reference_crops import figure_mask
    values = []
    for source in entry['multi'].get('compare') or entry['multi']['images']:
        picture = np.asarray(Image.open(Path(os.path.expandvars(os.path.expanduser(source)))).convert('RGB'))
        figure = ndimage.binary_erosion(figure_mask(picture), iterations=3)
        values.append(picture[figure].astype(np.float32) / 255 @ np.array([.2126, .7152, .0722], np.float32))
    return float(np.median(np.concatenate(values)))


def region_weights(path, size):
    """The roster's teamBoxes as a texture-space weight: the UV triangles the Blender step found inside the boxes,
    filled in white and softened a little. Not spread: Meshy packs the islands of its atlas a texel or two apart, and a
    wider mask would colour the neighbour's edge."""
    import cv2
    import numpy as np
    canvas = np.zeros((size, size), np.uint8)
    for triangle in json.loads(Path(path).read_text(encoding='utf-8'))['triangles']:
        points = np.array([[u * size, (1 - v) * size] for u, v in triangle]).round().astype(np.int32)
        cv2.fillConvexPoly(canvas, points, 255)
    return cv2.GaussianBlur(canvas, (0, 0), .8).astype(np.float32) / 255


def textures(entry, raw, out, size=None, region=None, report=None):
    """Writes the three maps; returns the share of the texture marked as owner colour. `report`, a dict, also gets the
    tone gamma applied to a figure modelled from the user's pictures."""
    import numpy as np
    from PIL import Image
    unit = entry['id']
    SIZE = size or globals()['SIZE']
    base = Image.open(raw / ('%s_base_color.png' % unit)).convert('RGB').resize((SIZE, SIZE), Image.LANCZOS)
    rgb = np.asarray(base).astype(np.float32) / 255
    gamma = None
    if entry.get('multi') and entry.get('matchTone', True):
        # A figure modelled from the user's pictures is brought to their brightness: Meshy's albedo comes out darker than
        # the lit pictures it was made from. One gamma on the luminance puts the medians together and leaves black,
        # white, hue and the painted contrast alone.
        luminance = rgb @ np.array([.2126, .7152, .0722], np.float32)
        wanted, have = reference_median(entry), float(np.median(luminance))
        gamma = float(np.clip(np.log(wanted) / np.log(have), .6, 1.4))
        ratio = np.power(np.maximum(luminance, 1e-4), gamma) / np.maximum(luminance, 1e-4)
        rgb = np.clip(rgb * ratio[..., None], 0, 1)
        base = Image.fromarray((rgb * 255 + .5).astype(np.uint8))
    high, low = rgb.max(axis=2), rgb.min(axis=2)
    chroma = high - low
    saturation = np.where(high > 0, chroma / np.maximum(high, 1e-6), 0)
    r, g, b = rgb[..., 0], rgb[..., 1], rgb[..., 2]
    safe = np.maximum(chroma, 1e-6)
    hue = np.where(high == r, ((g - b) / safe) % 6, np.where(high == g, (b - r) / safe + 2, (r - g) / safe + 4)) * 60
    mask = np.zeros(hue.shape, np.float32)
    # Most cloth is saturated; some cultures dye theirs dark and dull (the mountain clans' woad blue sits near 0.2),
    # so the roster can lower the gate for one figure.
    gate = entry.get('teamSaturation', .28)
    for start, end in entry.get('teamHues', []):
        inside = (hue >= start) & (hue <= end)
        # Soft at the edges of the saturation and brightness gates, so trim and shading do not band.
        weight = np.clip((saturation - gate) / max(.08, gate * .6), 0, 1) * np.clip((high - .08) / .08, 0, 1)
        mask = np.maximum(mask, np.where(inside, weight, 0))
    if entry.get('teamValue'):
        # Cloth that is darker (or lighter) than what shares its hue: a dark olive cloak against blonde hair and a pale
        # tunic, which no hue can tell apart. [lowest, highest] brightness, 0..1, soft over a few percent.
        low_value, high_value = entry['teamValue']
        mask = mask * np.clip((high - low_value) / .05, 0, 1) * np.clip((high_value - high) / .05, 0, 1)
    if entry.get('teamBoxes'):
        # A figure whose cloth shares its hue with everything else on it (brown on brown, olive on olive) is marked by
        # where the cloth is instead: the surface inside the roster's boxes (fractions of the figure's height, x across,
        # y front to back with the front at -y, z up from the soles). The hue gate above, when the entry has one, still
        # applies inside them.
        area = region_weights(region, SIZE)
        mask = area * (mask if entry.get('teamHues') else 1)
    # A partial weight tints a zone toward the owner's colour instead of replacing it (the mountain turf roofs).
    mask *= entry.get('teamWeight', 1)
    alpha = Image.fromarray((mask * 255).astype(np.uint8))
    rgba = base.copy()
    rgba.putalpha(alpha)
    rgba.save(out / ('%s_BaseColor.png' % unit))
    Image.open(raw / ('%s_normal.png' % unit)).convert('RGB').resize((SIZE, SIZE), Image.LANCZOS).save(out / ('%s_Normal.png' % unit))
    metallic = Image.open(raw / ('%s_metallic.png' % unit)).convert('L').resize((SIZE, SIZE), Image.LANCZOS)
    rough = np.asarray(Image.open(raw / ('%s_roughness.png' % unit)).convert('L').resize((SIZE, SIZE), Image.LANCZOS))
    smooth = Image.fromarray((255 - rough).astype(np.uint8))
    black = Image.new('L', (SIZE, SIZE), 0)
    Image.merge('RGBA', (metallic, black, black, smooth)).save(out / ('%s_Mask.png' % unit))
    if report is not None and gamma is not None:
        report['toneGamma'] = round(gamma, 3)
    return float(mask.mean())


BLENDER_SCRIPT = r'''
import bpy, json, sys
args = json.loads(sys.argv[sys.argv.index('--') + 1])
states = args['states']
bpy.ops.wm.read_factory_settings(use_empty=True)

def load(path):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=path)
    return [o for o in bpy.data.objects if o not in before]

model = load(args['rigged'])
arm = next(o for o in model if o.type == 'ARMATURE')
mesh = max((o for o in model if o.type == 'MESH' and o.data.materials), key=lambda o: len(o.data.polygons))
for o in model:
    if o not in (arm, mesh):
        bpy.data.objects.remove(o, do_unlink=True)
for action in list(bpy.data.actions):
    bpy.data.actions.remove(action)
mesh.name = args['id']
mesh.data.materials[0].name = args['id']

# Roster "teamBoxes": [[x0, x1, y0, y1, z0, z1], ...] in fractions of the figure's height at rest (z up from the soles, y
# negative at the front). The UV triangles whose centre lies in a box are written out for textures() to fill in.
if args.get('teamBoxes'):
    import numpy as np
    me = mesh.data
    me.calc_loop_triangles()
    world = np.asarray(mesh.matrix_world, np.float64)
    co = np.empty(len(me.vertices) * 3, np.float32); me.vertices.foreach_get('co', co)
    co = co.reshape(-1, 3).astype(np.float64) @ world[:3, :3].T + world[:3, 3]
    floor, tall = co[:, 2].min(), co[:, 2].max() - co[:, 2].min()
    count = len(me.loop_triangles)
    tri_vertices = np.empty(count * 3, np.int32); me.loop_triangles.foreach_get('vertices', tri_vertices)
    tri_loops = np.empty(count * 3, np.int32); me.loop_triangles.foreach_get('loops', tri_loops)
    uv = np.empty(len(me.loops) * 2, np.float32); me.uv_layers.active.data.foreach_get('uv', uv)
    uv = uv.reshape(-1, 2)
    centre = co[tri_vertices.reshape(-1, 3)].mean(axis=1)
    centre = np.c_[centre[:, 0] / tall, centre[:, 1] / tall, (centre[:, 2] - floor) / tall]
    chosen = np.zeros(count, bool)
    for x0, x1, y0, y1, z0, z1 in args['teamBoxes']:
        chosen |= (centre[:, 0] >= x0) & (centre[:, 0] <= x1) & (centre[:, 1] >= y0) & (centre[:, 1] <= y1) & (centre[:, 2] >= z0) & (centre[:, 2] <= z1)
    with open(args['region'], 'w') as handle:
        json.dump({'triangles': uv[tri_loops.reshape(-1, 3)[chosen]].tolist(), 'of': count}, handle)
    print('TEAM_REGION', int(chosen.sum()), 'of', count, 'triangles')

# Roster "rigidParts": {"bone": "Spine02", "minSize": 0.5}. Meshy's auto-rig spreads a spear, bow or shield slung on
# the back over the arms, neck and legs, so a walking leg drags the spear butt into a long sliver and the shield
# buckles. Every separate piece of surface other than the body whose longest side reaches minSize of the figure's
# height rides that one bone rigidly instead. The pieces are found on a welded copy: the GLB splits vertices along
# UV seams.
rigid_report = []
if args.get('rigid'):
    import bmesh
    from mathutils import kdtree
    bm = bmesh.new(); bm.from_mesh(mesh.data)
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-4); bm.verts.ensure_lookup_table()
    tree = kdtree.KDTree(len(bm.verts))
    for v in bm.verts: tree.insert(v.co, v.index)
    tree.balance()
    label, count = {}, 0
    for v in bm.verts:
        if v.index in label: continue
        stack = [v]; label[v.index] = count
        while stack:
            a = stack.pop()
            for e in a.link_edges:
                b = e.other_vert(a)
                if b.index not in label: label[b.index] = count; stack.append(b)
        count += 1
    pieces = {}
    for v in mesh.data.vertices: pieces.setdefault(label[tree.find(v.co)[1]], []).append(v.index)
    world = [mesh.matrix_world @ v.co for v in mesh.data.vertices]
    tall = max(c.z for c in world) - min(c.z for c in world)
    body = max(pieces, key=lambda k: len(pieces[k]))
    bone = mesh.vertex_groups.get(args['rigid'].get('bone', 'Spine02')) or mesh.vertex_groups.new(name=args['rigid'].get('bone', 'Spine02'))
    for k, members in pieces.items():
        if k == body: continue
        extent = max(max(world[i][a] for i in members) - min(world[i][a] for i in members) for a in range(3))
        if extent < args['rigid'].get('minSize', .5) * tall: continue
        for i in members:
            for g in list(mesh.data.vertices[i].groups): mesh.vertex_groups[g.group].remove([i])
            bone.add([i], 1.0, 'REPLACE')
        rigid_report.append({'vertices': len(members), 'extent': round(extent / tall, 2)})
    bm.free()
    # clearBelow: the auto-rig also hangs flaps of a long coat's hem on an arm, so the hand drags a sheet of cloth
    # out sideways. No vertex below that share of the height (the arms stand straight out in the T-pose, far above)
    # keeps an arm, neck or head weight; what is left is renormalised, or goes to the hips.
    below = args['rigid'].get('clearBelow')
    if below:
        floor = min(c.z for c in world)
        upper = [g.index for g in mesh.vertex_groups if any(k in g.name for k in ('Arm', 'Hand', 'Shoulder', 'neck', 'Head'))]
        hips = mesh.vertex_groups.get('Hips')
        cleared = 0
        for v, c in zip(mesh.data.vertices, world):
            if c.z - floor > below * tall: continue
            drop = [g.group for g in v.groups if g.group in upper and g.weight > 0]
            if not drop: continue
            for k in drop: mesh.vertex_groups[k].remove([v.index])
            total = sum(g.weight for g in v.groups)
            if total > 1e-6:
                for g in v.groups: g.weight = g.weight / total
            elif hips is not None:
                hips.add([v.index], 1.0, 'REPLACE')
            cleared += 1
        rigid_report.append({'clearedBelow': below, 'vertices': cleared})

found = {}
for path, forced, retarget in args['clips']:
    if retarget:
        # The clips of another rig, copied onto this one (meshy_retarget_blender.py, prepended to this script).
        objects = []
        print('RETARGETED', path, retarget_clip_file(path, arm))
    else:
        objects = load(path)
    for action in list(bpy.data.actions):
        if action.name in found.values() or action.get('emberfield_state'):
            continue
        name = forced or next((s for k, s in states.items() if action.name == k or action.name.endswith('|' + k)), None)
        if name is None:
            print('UNMAPPED_ACTION', action.name); continue
        action.name = name; action['emberfield_state'] = name; action.use_fake_user = True
        found[name] = name
    for o in objects:
        bpy.data.objects.remove(o, do_unlink=True)

# Every take on one slot name, the armature's. The FBX exporter hands each action to the armature through the slot it
# last used, so a take whose slot carries another name (a clip file imported beside the rig is "Armature.001") would
# bake as one still pose whenever the takes do not all share a name.
for action in bpy.data.actions:
    for slot in action.slots:
        slot.identifier = 'OB' + arm.name

arm.animation_data_create()

def curves(action):
    if len(action.slots) and len(action.layers) and len(action.layers[0].strips):
        return action.layers[0].strips[0].channelbag(action.slots[0]).fcurves
    return action.fcurves

def assign(action):
    arm.animation_data.action = action
    if len(action.slots): arm.animation_data.action_slot = action.slots[0]

scene = bpy.context.scene
idle = bpy.data.actions.get('Idle')

def standing_heights():
    """Height of every vertex of the body at the first frame of Idle, in metres: the pose MeshyUnitBaker measures too."""
    import numpy as np
    assign(idle)
    scene.frame_set(int(idle.frame_range[0]))
    evaluated = mesh.evaluated_get(bpy.context.evaluated_depsgraph_get())
    data = evaluated.to_mesh()
    co = np.empty(len(data.vertices) * 3, np.float32)
    data.vertices.foreach_get('co', co)
    evaluated.to_mesh_clear()
    world = np.asarray(evaluated.matrix_world, np.float64)
    return co.reshape(-1, 3).astype(np.float64) @ world[2, :3] + world[2, 3]

# Roster "groundIdle": a rig fitted by Meshy to a figure puts the soles a few centimetres below the toe joints the clips
# plant on the floor, and a figure whose legs differ from the clips' source more still. Every take rises by the one
# amount that stands Idle's soles on the ground, so the figure neither sinks into the floor nor floats above it.
lift = 0.0
if args.get('ground') and idle:
    lift = -float(standing_heights().min())
    from mathutils import Vector
    shift = arm.data.bones['Hips'].matrix_local.to_3x3().inverted() @ Vector((0, 0, lift / arm.matrix_world.to_scale().z))
    for action in bpy.data.actions:
        for curve in curves(action):
            if curve.data_path != 'pose.bones["Hips"].location': continue
            for key in curve.keyframe_points:
                key.co.y += shift[curve.array_index]; key.handle_left.y += shift[curve.array_index]; key.handle_right.y += shift[curve.array_index]
            curve.update()

# Meshy's gaits walk forward through the clip. The game moves the unit itself, so the travel comes out (a straight
# line from first to last key on each Hips location channel; the sway and bob, which return to where they started,
# stay). The travel is measured first: it is the stride speed at which the feet stay planted.
# The ladder climb goes up the rungs, and forward along the ladder's lean, the same way: WorldView carries the
# boarding soldier from the ladder's foot to the wall deck, so the clip must climb in place, and its rise per second
# is the speed at which the hands keep hold of the rungs.
fps = scene.render.fps / scene.render.fps_base
speeds = {}
for name in ('Climb', 'Walk', 'Run'):
    action = bpy.data.actions.get(name)
    if not action: continue
    assign(action)
    start, end = action.frame_range
    scene.frame_set(int(start)); a = (arm.matrix_world @ arm.pose.bones['Hips'].head).copy()
    scene.frame_set(int(end)); b = (arm.matrix_world @ arm.pose.bones['Hips'].head).copy()
    seconds = max(1e-3, (int(end) - int(start)) / fps)
    travel = ((b.x - a.x) ** 2 + (b.y - a.y) ** 2) ** .5
    if name == 'Climb':
        speeds[name] = (b.z - a.z) / seconds
    elif travel > .05:
        speeds[name] = travel / seconds
    else:
        # Already in place: a planted foot slides back under the body for the stance part of the cycle (about
        # 60% of a walk, 40% of a run), so its fore-and-aft range over that time is the ground speed.
        xs, ys = [], []
        for frame in range(int(start), int(end) + 1):
            scene.frame_set(frame)
            foot = arm.matrix_world @ arm.pose.bones['LeftFoot'].head
            xs.append(foot.x); ys.append(foot.y)
        reach = max(max(xs) - min(xs), max(ys) - min(ys))
        speeds[name] = reach / (seconds * (.6 if name == 'Walk' else .4))
    for curve in curves(action):
        if curve.data_path != 'pose.bones["Hips"].location' or len(curve.keyframe_points) < 2: continue
        keys = curve.keyframe_points
        t0, t1 = keys[0].co.x, keys[-1].co.x
        drift = keys[-1].co.y - keys[0].co.y
        for key in keys:
            shift = drift * (key.co.x - t0) / max(1e-6, t1 - t0)
            key.co.y -= shift; key.handle_left.y -= shift; key.handle_right.y -= shift
        curve.update()

# heightMetres is the body's height at the first frame of Idle, the pose MeshyUnitBaker measures to turn the stride speeds
# (metres here) into the game's units. Without an Idle it is the mesh's own height.
if idle:
    heights = standing_heights()
    height = float(heights.max() - heights.min())
else:
    height = mesh.dimensions.z
bpy.ops.object.select_all(action='DESELECT')
arm.select_set(True); mesh.select_set(True); bpy.context.view_layer.objects.active = arm
bpy.ops.export_scene.fbx(filepath=args['fbx'], use_selection=True, object_types={'MESH', 'ARMATURE'},
    axis_forward='-Z', axis_up='Y', apply_unit_scale=True, add_leaf_bones=False, armature_nodetype='NULL',
    use_armature_deform_only=False, bake_anim=True, bake_anim_use_all_actions=True, bake_anim_use_nla_strips=False,
    bake_anim_simplify_factor=0, path_mode='STRIP', embed_textures=False, mesh_smooth_type='FACE')
tris = sum(len(p.vertices) - 2 for p in mesh.data.polygons)
print('MESHY_FINISH ' + json.dumps({'states': sorted(found), 'triangles': tris, 'bones': len(arm.data.bones),
    'walkMetresPerSecond': round(speeds.get('Walk', 0), 3), 'runMetresPerSecond': round(speeds.get('Run', 0), 3),
    'climbMetresPerSecond': round(speeds.get('Climb', 0), 3), 'heightMetres': round(height, 3),
    **({'groundLiftMetres': round(lift, 4)} if args.get('ground') else {}),
    **({'rigidParts': rigid_report} if args.get('rigid') else {})}))
'''


def finish(entry, roster):
    unit = entry['id']
    raw = Path(os.path.expandvars(os.path.expanduser(roster['raw']))) / unit
    out = ROOT / roster['output'] / unit
    # Walking and running come with every rig. The library clips are bought for it (False), or, where the roster says
    # "clipsFrom", copied over from a figure that had them bought (True); the ladder climb likewise
    # (meshy_climb.py fetches it separately and only for the units that can board a wall).
    source = Path(os.path.expandvars(os.path.expanduser(entry['clipsFrom']))) if entry.get('clipsFrom') else raw
    borrowed = source != raw
    clips = [(str(source / ('%s_animation.glb' % unit)), None, borrowed), (str(raw / ('%s_walking.glb' % unit)), 'Walk', False),
             (str(raw / ('%s_running.glb' % unit)), 'Run', False)]
    climb = source / ('%s_climb.glb' % unit)
    if climb.exists():
        clips.append((str(climb), 'Climb', borrowed))
    args = {'id': unit, 'rigged': str(raw / ('%s_rigged_character.glb' % unit)), 'clips': clips,
            'fbx': str(out / ('%s.fbx' % unit)), 'states': STATES, 'rigid': entry.get('rigidParts'), 'ground': bool(entry.get('groundIdle')),
            'teamBoxes': entry.get('teamBoxes'), 'region': str(raw / 'team_region.json')}
    script = raw / 'finish_blender.py'
    retarget = Path(__file__).with_name('meshy_retarget_blender.py').read_text(encoding='utf-8')
    script.write_text(retarget + '\n' + BLENDER_SCRIPT, encoding='utf-8')
    result = subprocess.run([BLENDER, '-b', '--factory-startup', '-P', str(script), '--', json.dumps(args)],
                            capture_output=True, text=True, encoding='utf-8', errors='replace')
    lines = [l for l in result.stdout.splitlines() if l.startswith(('MESHY_FINISH', 'UNMAPPED_ACTION', 'RETARGETED', 'TEAM_REGION'))]
    if result.returncode != 0 or not any(l.startswith('MESHY_FINISH') for l in lines):
        sys.stderr.write(result.stdout[-3000:] + result.stderr[-3000:])
        raise SystemExit('Blender failed for %s' % unit)
    report = json.loads(next(l for l in lines if l.startswith('MESHY_FINISH'))[len('MESHY_FINISH '):])
    # The textures come after the Blender step: a roster's teamBoxes are found in the mesh there.
    coverage = textures(entry, raw, out, region=raw / 'team_region.json', report=report)
    report['teamMaskCoverage'] = round(coverage, 4)
    for line in lines:
        if line.startswith('UNMAPPED_ACTION'):
            print('  warning: ' + line)
        elif line.startswith(('RETARGETED', 'TEAM_REGION')):
            print('  ' + line)
    manifest_path = out / 'meshy-manifest.json'
    manifest = json.loads(manifest_path.read_text(encoding='utf-8'))
    manifest['finish'] = report
    manifest_path.write_text(json.dumps(manifest, indent=2, ensure_ascii=False) + '\n', encoding='utf-8')
    print('%s: %s' % (unit, json.dumps(report)))
    return report


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('units', nargs='+')
    parser.add_argument('--roster', default='tools/art/meshy_units.json')
    args = parser.parse_args()
    roster = json.loads((ROOT / args.roster).read_text(encoding='utf-8'))
    for unit in args.units:
        entry = next(e for e in roster['units'] if e['id'] == unit)
        finish(entry, roster)


if __name__ == '__main__':
    main()
