"""Turn a unit's raw Meshy downloads into what the game ships. Free: no Meshy call.

    python tools/art/meshy_unit_finish.py <unit-id> [<unit-id> ...] [--roster tools/art/meshy_units.json]

For each unit, from the raw folder that meshy_unit.py filled:

* <id>.fbx -- the rigged mesh with every clip as its own take (Idle, Walk, Run, Attack, Hit, Death, and Work or Aim
  where the role has one, and Climb for the wall-boarding infantry once meshy_climb.py has fetched it). Blender merges
  the rig's walking and running with the library clips; no textures embedded.
* <id>_BaseColor.png -- 1024, with the owner-colour mask in alpha: cloth whose hue falls in the roster's teamHues.
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


def textures(entry, raw, out, size=None):
    import numpy as np
    from PIL import Image
    unit = entry['id']
    SIZE = size or globals()['SIZE']
    base = Image.open(raw / ('%s_base_color.png' % unit)).convert('RGB').resize((SIZE, SIZE), Image.LANCZOS)
    rgb = np.asarray(base).astype(np.float32) / 255
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
for path, forced in args['clips']:
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

arm.animation_data_create()

def curves(action):
    if len(action.slots) and len(action.layers) and len(action.layers[0].strips):
        return action.layers[0].strips[0].channelbag(action.slots[0]).fcurves
    return action.fcurves

def assign(action):
    arm.animation_data.action = action
    if len(action.slots): arm.animation_data.action_slot = action.slots[0]

# Meshy's gaits walk forward through the clip. The game moves the unit itself, so the travel comes out (a straight
# line from first to last key on each Hips location channel; the sway and bob, which return to where they started,
# stay). The travel is measured first: it is the stride speed at which the feet stay planted.
# The ladder climb goes up the rungs, and forward along the ladder's lean, the same way: WorldView carries the
# boarding soldier from the ladder's foot to the wall deck, so the clip must climb in place, and its rise per second
# is the speed at which the hands keep hold of the rungs. It goes first: heightMetres below reads the pose last
# evaluated, the end of Run, and MeshyUnitBaker scales the stride speed by it.
scene = bpy.context.scene
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

idle = bpy.data.actions.get('Idle')
if idle:
    arm.animation_data.action = idle
    if len(idle.slots): arm.animation_data.action_slot = idle.slots[0]
bpy.ops.object.select_all(action='DESELECT')
arm.select_set(True); mesh.select_set(True); bpy.context.view_layer.objects.active = arm
bpy.ops.export_scene.fbx(filepath=args['fbx'], use_selection=True, object_types={'MESH', 'ARMATURE'},
    axis_forward='-Z', axis_up='Y', apply_unit_scale=True, add_leaf_bones=False, armature_nodetype='NULL',
    use_armature_deform_only=False, bake_anim=True, bake_anim_use_all_actions=True, bake_anim_use_nla_strips=False,
    bake_anim_simplify_factor=0, path_mode='STRIP', embed_textures=False, mesh_smooth_type='FACE')
tris = sum(len(p.vertices) - 2 for p in mesh.data.polygons)
print('MESHY_FINISH ' + json.dumps({'states': sorted(found), 'triangles': tris, 'bones': len(arm.data.bones),
    'walkMetresPerSecond': round(speeds.get('Walk', 0), 3), 'runMetresPerSecond': round(speeds.get('Run', 0), 3),
    'climbMetresPerSecond': round(speeds.get('Climb', 0), 3), 'heightMetres': round(mesh.dimensions.z, 3),
    **({'rigidParts': rigid_report} if args.get('rigid') else {})}))
'''


def finish(entry, roster):
    unit = entry['id']
    raw = Path(os.path.expandvars(os.path.expanduser(roster['raw']))) / unit
    out = ROOT / roster['output'] / unit
    coverage = textures(entry, raw, out)
    clips = [(str(raw / ('%s_animation.glb' % unit)), None), (str(raw / ('%s_walking.glb' % unit)), 'Walk'),
             (str(raw / ('%s_running.glb' % unit)), 'Run')]
    # The ladder climb (meshy_climb.py) is fetched separately and only for the units that can board a wall.
    climb = raw / ('%s_climb.glb' % unit)
    if climb.exists():
        clips.append((str(climb), 'Climb'))
    args = {'id': unit, 'rigged': str(raw / ('%s_rigged_character.glb' % unit)), 'clips': clips,
            'fbx': str(out / ('%s.fbx' % unit)), 'states': STATES, 'rigid': entry.get('rigidParts')}
    script = raw / 'finish_blender.py'
    script.write_text(BLENDER_SCRIPT, encoding='utf-8')
    result = subprocess.run([BLENDER, '-b', '--factory-startup', '-P', str(script), '--', json.dumps(args)],
                            capture_output=True, text=True, encoding='utf-8', errors='replace')
    lines = [l for l in result.stdout.splitlines() if l.startswith(('MESHY_FINISH', 'UNMAPPED_ACTION'))]
    if result.returncode != 0 or not any(l.startswith('MESHY_FINISH') for l in lines):
        sys.stderr.write(result.stdout[-3000:] + result.stderr[-3000:])
        raise SystemExit('Blender failed for %s' % unit)
    report = json.loads(next(l for l in lines if l.startswith('MESHY_FINISH'))[len('MESHY_FINISH '):])
    report['teamMaskCoverage'] = round(coverage, 4)
    for line in lines:
        if line.startswith('UNMAPPED_ACTION'):
            print('  warning: ' + line)
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
