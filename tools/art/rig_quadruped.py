"""Rig a four-legged Meshy model -- a mount with its rider, or a beast -- and write its clips. Free after the model.

    python tools/art/rig_quadruped.py <unit-id> [--generate] [--max-credits 15]

Meshy's own rig is biped only: its quadruped option exists in the API but refuses a horse and a mounted knight alike
("Pose estimation failed", 2026-09-25). So four legs get a template skeleton here, fitted to the model:

* the hooves are the lowest slice of the mesh, clustered into four; the front pair is the one toward -Y, the way
  every Meshy model faces;
* the belly is where the cross-section jumps from four thin legs to one wide body;
* the neck and head are what stands above the belly ahead of the front legs, the tail what hangs behind the hind ones.

Hips and chest run along the body; each leg gets thigh, shin and hoof; neck, head and tail follow. Weights go to
the nearest bone segments (two per vertex), with leg vertices allowed only onto their own leg, so a rider, a lance or a
saddle moves rigidly with the back. The clips are keyed from the leg lengths: a diagonal walk, a gallop with paired
legs, an idle, a charge that dips onto the forehand, a flinch and a fall onto the side. The FBX and the manifest match what
meshy_unit_finish.py writes for bipeds, so MeshyUnitBaker takes it unchanged.

--generate first makes the model with Meshy text-to-3D (preview 5 + refine 10) from the roster's prompt.
"""
import argparse
import json
import os
import subprocess
import sys
from datetime import datetime, timezone
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import meshy_unit as meshy  # noqa: E402
import meshy_unit_finish as finish  # noqa: E402

ROOT = meshy.ROOT

BLENDER_SCRIPT = r'''
import bpy, json, sys, math
import numpy as np
from mathutils import Vector
args = json.loads(sys.argv[sys.argv.index('--') + 1])
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=args['glb'], merge_vertices=True)
meshes = [o for o in bpy.context.scene.objects if o.type == 'MESH']
bpy.ops.object.select_all(action='DESELECT')
for m in meshes: m.select_set(True)
bpy.context.view_layer.objects.active = meshes[0]
bpy.ops.object.parent_clear(type='CLEAR_KEEP_TRANSFORM')
if len(meshes) > 1: bpy.ops.object.join()
mesh = bpy.context.view_layer.objects.active
mesh.name = args['id']
if mesh.data.materials: mesh.data.materials[0].name = args['id']
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
# Stand it on the ground, centred, at its real height in metres; turn it if the roster says it faces elsewhere.
p = np.array([v.co[:] for v in mesh.data.vertices], np.float64)
if args.get('yaw'):
    a = math.radians(args['yaw']); c, s = math.cos(a), math.sin(a)
    p = p @ np.array([[c, s, 0], [-s, c, 0], [0, 0, 1]])
lo, hi = p.min(0), p.max(0)
p = (p - np.array([(lo[0] + hi[0]) / 2, (lo[1] + hi[1]) / 2, lo[2]])) * (args['height'] / (hi[2] - lo[2]))
for v, q in zip(mesh.data.vertices, p): v.co = Vector(q)
mesh.data.update()
H = p[:, 2].max()

# Hooves: the lowest slice, four clusters.
low = p[p[:, 2] < H * .07][:, :2]
order = np.argsort(low[:, 1]); front, back = low[order[: len(order) // 2]], low[order[len(order) // 2:]]
def pair(points):
    split = np.median(points[:, 0])
    left, right = points[points[:, 0] <= split], points[points[:, 0] > split]
    return left.mean(0), right.mean(0)
feet = {'FrontLeft': pair(front)[0], 'FrontRight': pair(front)[1], 'HindLeft': pair(back)[0], 'HindRight': pair(back)[1]}
# Refine with a few k-means passes so a tail or a lance butt near the ground cannot drag a foot.
centres = np.array(list(feet.values()))
for _ in range(8):
    labels = np.argmin(((low[:, None, :] - centres[None]) ** 2).sum(2), 1)
    centres = np.array([low[labels == k].mean(0) if (labels == k).any() else centres[k] for k in range(4)])
feet = dict(zip(feet.keys(), centres))
frontY = (feet['FrontLeft'][1] + feet['FrontRight'][1]) / 2
hindY = (feet['HindLeft'][1] + feet['HindRight'][1]) / 2

# Belly: the first slice whose footprint jumps well past the four legs'.
def cells(z0, z1):
    s = p[(p[:, 2] >= z0) & (p[:, 2] < z1)]
    return len(set(map(tuple, np.floor(s[:, :2] / (H * .03)).astype(int)))) if len(s) else 0
step = H * .02
base = np.mean([cells(z, z + step) for z in np.arange(H * .02, H * .08, step)])
belly = H * .3
for z in np.arange(H * .08, H * .6, step):
    if cells(z, z + step) >= max(3, base * 2.5): belly = z; break
# Wide paws (a wolf's) can fool the footprint test; the roster may name the belly as a fraction of the height.
if args.get('belly'): belly = H * args['belly']
body = p[(p[:, 2] > belly) & (p[:, 1] > frontY - .15 * H) & (p[:, 1] < hindY + .15 * H)]
back = np.percentile(body[:, 2], 60) if len(body) else belly * 1.4
spineZ = (belly + back) / 2
ahead = p[(p[:, 1] < frontY - .05 * H) & (p[:, 2] > belly)]
head = ahead[np.argmin(ahead[:, 1])] if len(ahead) else np.array([0, frontY - .2 * H, back + .2 * H])
headTop = ahead[ahead[:, 1] < head[1] + .12 * H] if len(ahead) else ahead
headCentre = headTop.mean(0) if len(headTop) else head
behind = p[(p[:, 1] > hindY + .05 * H) & (p[:, 2] > belly * .3)]
tail = behind[np.argmax(behind[:, 1])] if len(behind) else None

# Skeleton.
bpy.ops.object.armature_add(enter_editmode=True, location=(0, 0, 0))
arm = bpy.context.object; arm.name = 'Armature'; arm.data.name = 'Armature'
bones = arm.data.edit_bones
root = bones[0]; root.name = 'Root'; root.head = (0, 0, 0); root.tail = (0, 0, belly * .5)
def bone(name, head, tail, parent, connect=False):
    b = bones.new(name); b.head = Vector(head); b.tail = Vector(tail); b.parent = bones[parent]; b.use_connect = connect; return b
mid = (frontY + hindY) / 2
bone('Hips', (0, hindY, spineZ), (0, mid, spineZ), 'Root')
bone('Chest', (0, mid, spineZ), (0, frontY, spineZ), 'Hips', True)
neckBase = (0, frontY - .02 * H, back)
bone('Neck', neckBase, (0, (frontY + headCentre[1]) / 2, (back + headCentre[2]) / 2), 'Chest')
bone('Head', bones['Neck'].tail[:], (0, head[1], headCentre[2]), 'Neck', True)
if tail is not None:
    bone('Tail', (0, hindY + .03 * H, back * .95), (0, tail[1], tail[2]), 'Hips')
for name, (x, y) in feet.items():
    parent = 'Chest' if name.startswith('Front') else 'Hips'
    bone(name + 'Thigh', (x, y, belly), (x, y, belly * .55), parent)
    bone(name + 'Shin', (x, y, belly * .55), (x, y, belly * .12), name + 'Thigh', True)
    bone(name + 'Hoof', (x, y, belly * .12), (x, y, 0), name + 'Shin', True)
bpy.ops.object.mode_set(mode='OBJECT')

# Weights: nearest two bone segments; a leg vertex may only take its own leg (or the body it hangs from).
segments = {b.name: (np.array(b.head_local[:]), np.array(b.tail_local[:])) for b in arm.data.bones if b.name != 'Root'}
names = list(segments)
A = np.array([segments[n][0] for n in names]); B = np.array([segments[n][1] for n in names])
AB = B - A; lengths = np.maximum((AB ** 2).sum(1), 1e-9)
def distances(q):
    t = np.clip(((q - A) * AB).sum(1) / lengths, 0, 1)
    return np.sqrt(((A + AB * t[:, None] - q) ** 2).sum(1))
legOf = {n: n.replace('Thigh', '').replace('Shin', '').replace('Hoof', '') for n in names if n.endswith(('Thigh', 'Shin', 'Hoof'))}
footXY = np.array(list(feet.values())); footNames = list(feet.keys())
spread = min(np.linalg.norm(footXY[i] - footXY[j]) for i in range(4) for j in range(i + 1, 4))
groups = {n: mesh.vertex_groups.new(name=n) for n in names}
for index, q in enumerate(p):
    d = distances(q)
    if q[2] < belly * 1.02:
        near = int(np.argmin(((footXY - q[:2]) ** 2).sum(1)))
        if np.linalg.norm(footXY[near] - q[:2]) < spread * .55:
            leg = footNames[near]
            allowed = [i for i, n in enumerate(names) if legOf.get(n) == leg or (q[2] > belly * .85 and n in ('Hips', 'Chest'))]
        else:
            allowed = [i for i, n in enumerate(names) if n not in legOf]
    else:
        allowed = [i for i, n in enumerate(names) if n not in legOf]
    best = sorted(allowed, key=lambda i: d[i])[:2]
    w = np.array([1 / (d[i] + H * .01) ** 4 for i in best]); w /= w.sum()
    for i, weight in zip(best, w):
        if weight > .02: groups[names[i]].add([index], float(weight), 'REPLACE')
modifier = mesh.modifiers.new('Armature', 'ARMATURE'); modifier.object = arm
mesh.parent = arm

# Clips, keyed on the pose bones. Leg bones point down, so a rotation about their local X swings them fore and aft.
scene = bpy.context.scene; fps = 30; scene.render.fps = fps
arm.animation_data_create()
pose = arm.pose.bones
for b in pose: b.rotation_mode = 'XYZ'
legs = list(feet.keys())
def clip(name, seconds, key):
    action = bpy.data.actions.new(name); action.use_fake_user = True
    arm.animation_data.action = action
    if len(action.slots) == 0 and hasattr(action.slots, 'new'): pass
    frames = int(round(seconds * fps))
    for f in range(frames + 1):
        t = f / frames
        for b in pose: b.rotation_euler = (0, 0, 0); b.location = (0, 0, 0)
        key(t)
        for b in pose:
            b.keyframe_insert('rotation_euler', frame=f + 1); b.keyframe_insert('location', frame=f + 1)
    return action
def gait(t, period, swing, bend, phases, bob, pitch):
    for leg in legs:
        a = 2 * math.pi * (t + phases[leg])
        pose[leg + 'Thigh'].rotation_euler.x = math.radians(swing) * math.sin(a)
        pose[leg + 'Shin'].rotation_euler.x = -math.radians(bend) * max(0, math.sin(a + math.pi / 2))
        pose[leg + 'Hoof'].rotation_euler.x = math.radians(bend * .4) * max(0, math.sin(a + math.pi / 2))
    pose['Hips'].location.z = bob * H * abs(math.sin(2 * math.pi * t * 2))
    pose['Hips'].rotation_euler.x = math.radians(pitch) * math.sin(2 * math.pi * t)
    pose['Neck'].rotation_euler.x = math.radians(pitch * .8) * math.sin(2 * math.pi * t + .6)
    if 'Tail' in pose: pose['Tail'].rotation_euler.z = math.radians(8) * math.sin(2 * math.pi * t)
walk = {'FrontLeft': 0, 'HindRight': 0, 'FrontRight': .5, 'HindLeft': .5}
gallop = {'FrontLeft': 0, 'FrontRight': .1, 'HindLeft': .5, 'HindRight': .6}
clip('Walk', 1.0, lambda t: gait(t, 1.0, 20, 32, walk, .006, 1.5))
clip('Run', .6, lambda t: gait(t, .6, 38, 55, gallop, .02, 5))
def idle(t):
    pose['Neck'].rotation_euler.x = math.radians(3) * math.sin(2 * math.pi * t)
    pose['Head'].rotation_euler.x = math.radians(4) * math.sin(2 * math.pi * t * 2 + 1)
    if 'Tail' in pose: pose['Tail'].rotation_euler.z = math.radians(10) * math.sin(2 * math.pi * t)
clip('Idle', 2.0, idle)
def attack(t):
    # A charge: the body dips forward onto the forehand, head and lance down into the blow, then recovers.
    rise = math.sin(math.pi * min(1, t / .75)) if t < .75 else 0
    pose['Hips'].rotation_euler.x = -math.radians(22) * rise
    for leg in ('FrontLeft', 'FrontRight'):
        pose[leg + 'Thigh'].rotation_euler.x = math.radians(55) * rise
        pose[leg + 'Shin'].rotation_euler.x = -math.radians(70) * rise
    pose['Neck'].rotation_euler.x = math.radians(18) * rise
clip('Attack', 1.0, attack)
def hit(t):
    k = math.sin(math.pi * t)
    pose['Chest'].rotation_euler.z = math.radians(7) * k
    pose['Neck'].rotation_euler.x = -math.radians(10) * k
clip('Hit', .4, hit)
def death(t):
    k = min(1, t / .7); k = k * k * (3 - 2 * k)
    # Root stands upright, so its local Z is the body's long axis: turning about it rolls the animal onto its side.
    pose['Root'].rotation_euler.z = math.radians(84) * k
    pose['Root'].location.x = -belly * .55 * k
    for leg in legs:
        pose[leg + 'Thigh'].rotation_euler.x = math.radians(18 if leg.startswith('Front') else -14) * k
    pose['Neck'].rotation_euler.x = -math.radians(25) * k
clip('Death', 1.6, death)
arm.animation_data.action = bpy.data.actions['Idle']

bpy.ops.object.select_all(action='DESELECT')
arm.select_set(True); mesh.select_set(True); bpy.context.view_layer.objects.active = arm
bpy.ops.export_scene.fbx(filepath=args['fbx'], use_selection=True, object_types={'MESH', 'ARMATURE'},
    axis_forward='-Z', axis_up='Y', apply_unit_scale=True, add_leaf_bones=False, armature_nodetype='NULL',
    use_armature_deform_only=False, bake_anim=True, bake_anim_use_all_actions=True, bake_anim_use_nla_strips=False,
    bake_anim_simplify_factor=0, path_mode='STRIP', embed_textures=False, mesh_smooth_type='FACE')
tris = sum(len(f.vertices) - 2 for f in mesh.data.polygons)
leg = belly
print('RIG_QUADRUPED ' + json.dumps({'states': ['Attack', 'Death', 'Hit', 'Idle', 'Run', 'Walk'], 'triangles': tris,
    'bones': len(arm.data.bones), 'heightMetres': round(float(H), 3), 'bellyMetres': round(float(belly), 3),
    'walkMetresPerSecond': round(2 * leg * math.sin(math.radians(20)) / 1.0 * 1.6, 3),
    'runMetresPerSecond': round(2 * leg * math.sin(math.radians(38)) / .6 * 2.2, 3),
    'feet': {k: [round(float(c), 3) for c in v] for k, v in feet.items()}, 'tail': tail is not None}))
'''


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('unit')
    parser.add_argument('--roster', default='tools/art/meshy_units.json')
    parser.add_argument('--generate', action='store_true')
    parser.add_argument('--max-credits', type=int, default=15)
    args = parser.parse_args()
    roster = json.loads((ROOT / args.roster).read_text(encoding='utf-8'))
    entry = next((e for e in roster['units'] if e['id'] == args.unit), None)
    if entry is None or entry.get('rig') != 'quadruped':
        raise SystemExit('%s is not a quadruped entry in %s' % (args.unit, args.roster))
    unit = entry['id']
    raw = Path(os.path.expandvars(os.path.expanduser(roster['raw']))) / unit
    out = ROOT / roster['output'] / unit
    raw.mkdir(parents=True, exist_ok=True); out.mkdir(parents=True, exist_ok=True)
    manifest_path = out / 'meshy-manifest.json'
    manifest = json.loads(manifest_path.read_text(encoding='utf-8')) if manifest_path.exists() else {
        'id': unit, 'entry': entry, 'steps': {}, 'files': {}, 'credits': 0}
    if args.generate and 'refine' not in manifest['steps']:
        key = meshy.api_key()
        if 15 > args.max_credits:
            raise SystemExit('A model costs 15 credits, over the cap.')
        pid = meshy.call('POST', '/v2/text-to-3d', key, {'mode': 'preview', 'prompt': entry['prompt'][:600], 'model_type': 'smart-topology',
                                                          'ai_model': 'meshy-t2', 'target_polycount': 5000, 'target_formats': ['glb']})['result']
        manifest['steps']['preview'] = {'task': meshy.strip(meshy.wait(key, '/v2/text-to-3d/' + pid, unit + ' preview')), 'credits': 5}
        rid = meshy.call('POST', '/v2/text-to-3d', key, {'mode': 'refine', 'preview_task_id': pid, 'enable_pbr': True,
                                                          'texture_prompt': entry['texture'][:600], 'target_formats': ['glb']})['result']
        task = meshy.wait(key, '/v2/text-to-3d/' + rid, unit + ' refine')
        manifest['steps']['refine'] = {'task': meshy.strip(task), 'credits': 10, 'finished_utc': datetime.now(timezone.utc).isoformat()}
        manifest['credits'] += 15
        for where, url in meshy.urls(task):
            name = where.split('.')[-1]
            if where.startswith('model_urls.') and url.split('?', 1)[0].endswith('.glb'):
                manifest['files']['model_glb'] = meshy.download(url, raw / (unit + '_model.glb'))
            elif where.startswith('texture_urls.'):
                manifest['files']['texture_' + name] = meshy.download(url, raw / ('%s_%s.png' % (unit, name)))
            elif where.startswith('thumbnail'):
                manifest['files']['thumbnail'] = meshy.download(url, raw / 'thumbnail.png')
        meshy.save(manifest, manifest_path)
    coverage = finish.textures(entry, raw, out)
    script = raw / 'rig_quadruped_blender.py'
    script.write_text(BLENDER_SCRIPT, encoding='utf-8')
    blender_args = {'id': unit, 'glb': str(raw / (unit + '_model.glb')), 'fbx': str(out / (unit + '.fbx')),
                    'height': entry.get('height', 2.4), 'yaw': entry.get('yaw', 0), 'belly': entry.get('belly', 0)}
    result = subprocess.run([finish.BLENDER, '-b', '--factory-startup', '-P', str(script), '--', json.dumps(blender_args)],
                            capture_output=True, text=True, encoding='utf-8', errors='replace')
    line = next((l for l in result.stdout.splitlines() if l.startswith('RIG_QUADRUPED ')), None)
    if result.returncode != 0 or line is None:
        sys.stderr.write(result.stdout[-4000:] + result.stderr[-3000:])
        raise SystemExit('Blender failed for %s' % unit)
    report = json.loads(line[len('RIG_QUADRUPED '):])
    report['teamMaskCoverage'] = round(coverage, 4)
    report['rig'] = 'quadruped template (tools/art/rig_quadruped.py)'
    manifest['finish'] = report
    meshy.save(manifest, manifest_path)
    print('RIG_QUADRUPED_OK %s %s' % (unit, json.dumps(report)))


if __name__ == '__main__':
    main()
