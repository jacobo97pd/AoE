"""Give a Meshy mount the skeleton and animations of an animated donor animal. Free: no Meshy call.

    python tools/art/transplant_rig.py <unit-id>

The donor comes from the Quaternius Animated Animal Pack (CC0), unpacked outside the repository at
D:/EmberfieldAssets/Monturas/animal-pack. Each animal there has a full rig and animations made by an animator (walk,
gallop, idle, headbutt and kick, hit reactions, death). The roster entry names the donor ("donor": "Horse") and may
remap its clips ("clips": {"Attack": "Attack_Kick"}).

The donor's clips keep their local rotations; the Meshy model keeps its shape, and the donor's skeleton is moved into it:

1. The feet of both are clustered from their lowest slice, and both bellies are the underside of the body on its centre
   line. The Meshy model is scaled per axis so its feet span the donor's and its belly sits at the donor's height, and
   a thin-plate spline then pins each foot and the muzzle onto the donor's.
2. In that warped shape each vertex copies the bone weights of the nearest point on the donor's surface, so legs follow
   legs and the neck follows the neck. The model then takes its own shape back, and the donor's bones move into it
   through the inverse warp; the donor's tail swings onto the Meshy tail.
3. The rider and its gear (the pieces of surface standing mostly above the back) ride the back rigidly.
4. Corrections where the shapes differ: hooves ride the lower legs, lower legs keep only what lies within a hoof's
   thickness, only the tail swings with the tail, ears ride the head, and a pair of legs Meshy fused into one block
   moves as one leg (or, with splitLegs, two separate legs are split by side).
5. The body is lifted or lowered in the standing and moving clips until the planted hooves touch the ground.
6. In the attack and the hit the tail, then the neck, turn up and then the body lifts, just enough to keep every
   vertex above the ground.

Roster options: donor, clips, tailDamp (tail motion kept, 0 to 1), tailMargin, saddle (the back's height as a share
of the model's, when the rider is not found), cloth (flanks ride the body), fit (["muzzle", "tail"] by default), yaw,
belly and donorBelly (heights as a share of the model's), saddleBone, splitLegs (["Front"] and/or ["Back"]),
tailClear (a tail hanging close behind the hocks drops the leg weights it copied and swings with the tail alone).
docs/art/meshy-pipeline.md explains them.

The donor's clips are renamed to the states CorsairAnimationDriver plays, and the FBX and manifest match the rest of
the units, so MeshyUnitBaker takes them unchanged.
"""
import argparse
import json
import os
import subprocess
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import meshy_unit as meshy  # noqa: E402
import meshy_unit_finish as finish  # noqa: E402

ROOT = meshy.ROOT
DONORS = Path(os.environ.get('EMBERFIELD_MOUNTS', 'D:/EmberfieldAssets/Monturas/animal-pack/glTF'))
# The pack's clip names; the wolf has one attack, the hoofed animals have two.
DEFAULT_CLIPS = {'Idle': 'Idle', 'Walk': 'Walk', 'Run': 'Gallop', 'Attack': 'Attack_Headbutt', 'Hit': 'Idle_HitReact1', 'Death': 'Death'}

BLENDER_SCRIPT = r'''
import bpy, json, sys, math
import numpy as np
from mathutils import Vector
args = json.loads(sys.argv[sys.argv.index('--') + 1])
bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene

# Donor: armature at rest, mesh in rest pose.
bpy.ops.import_scene.gltf(filepath=args['donor'])
arm = next(o for o in scene.objects if o.type == 'ARMATURE')
donor = max((o for o in scene.objects if o.type == 'MESH'), key=lambda o: len(o.data.vertices))
for extra in [o for o in scene.objects if o.type == 'MESH' and o is not donor]:
    bpy.data.objects.remove(extra, do_unlink=True)
arm.animation_data.action = None
for b in arm.pose.bones: b.matrix_basis.identity()
bpy.context.view_layer.update()
depsgraph = bpy.context.evaluated_depsgraph_get()
evaluated = donor.evaluated_get(depsgraph)
dp = np.array([(donor.matrix_world @ v.co)[:] for v in evaluated.data.vertices])

# Target: the Meshy model, joined, standing on the ground.
before = set(scene.objects)
bpy.ops.import_scene.gltf(filepath=args['target'], merge_vertices=True)
parts = [o for o in scene.objects if o not in before and o.type == 'MESH']
for o in scene.objects: o.select_set(False)
for o in parts: o.select_set(True)
bpy.context.view_layer.objects.active = parts[0]
bpy.ops.object.parent_clear(type='CLEAR_KEEP_TRANSFORM')
if len(parts) > 1: bpy.ops.object.join()
target = bpy.context.view_layer.objects.active
target.name = args['id']
if target.data.materials: target.data.materials[0].name = args['id']
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
tp = np.array([v.co[:] for v in target.data.vertices], np.float64)
if args.get('yaw'):
    a = math.radians(args['yaw']); c, s = math.cos(a), math.sin(a)
    tp = tp @ np.array([[c, s, 0], [-s, c, 0], [0, 0, 1]])
tp[:, 2] -= tp[:, 2].min()

def landmarks(p, override):
    H = p[:, 2].max()
    low = p[p[:, 2] < H * .06][:, :2]
    front = low[low[:, 1] <= np.median(low[:, 1])]; back = low[low[:, 1] > np.median(low[:, 1])]
    def pair(q):
        m = np.median(q[:, 0]); return q[q[:, 0] <= m].mean(0), q[q[:, 0] > m].mean(0)
    centres = np.array([*pair(front), *pair(back)])
    for _ in range(8):
        labels = np.argmin(((low[:, None, :] - centres[None]) ** 2).sum(2), 1)
        centres = np.array([low[labels == k].mean(0) if (labels == k).any() else centres[k] for k in range(4)])
    # The belly: the underside of the body on its centre line, in the middle third between the front and hind feet,
    # where neither a leg nor a cloth hanging down a flank reaches.
    f, h = centres[:2, 1].mean(), centres[2:, 1].mean()
    band = p[(np.abs(p[:, 0] - centres[:, 0].mean()) < (np.ptp(centres[:, 0]) + H * .05) * .15) &
             (p[:, 1] > f + (h - f) / 3) & (p[:, 1] < h - (h - f) / 3) & (p[:, 2] > H * .06)]
    belly = float(band[:, 2].min()) if len(band) else H * .3
    if override: belly = H * override
    return centres, belly

dfeet, dbelly = landmarks(dp, args.get('donorBelly'))
tfeet, tbelly = landmarks(tp, args.get('belly'))
dspan = dfeet.max(0) - dfeet.min(0); tspan = tfeet.max(0) - tfeet.min(0)
scale = np.array([dspan[0] / max(tspan[0], 1e-6), dspan[1] / max(tspan[1], 1e-6), dbelly / max(tbelly, 1e-6)])
# The Meshy model is warped onto the donor only long enough to read weights off the donor's surface. Afterwards it gets
# its own shape back and the skeleton moves to it instead (see below), so the mount keeps its proportions.
dcentre = np.array([*dfeet.mean(0), 0]); tcentre = np.array([*tfeet.mean(0), 0])
fitted = (tp - tcentre) * scale + dcentre
back = np.percentile(dp[(dp[:, 1] > dfeet[:, 1].min()) & (dp[:, 1] < dfeet[:, 1].max())][:, 2], 97)
between = (fitted[:, 1] > dfeet[:, 1].min() - (dspan[1] * .1)) & (fitted[:, 1] < dfeet[:, 1].max())
# The rider sits above the donor's back; but a bull's hump or a big stag's withers stand higher than a ram's or a
# slender stag's back, and would hide most of a short rider. When hardly anything clears the donor's back, the top of
# the model's own rump, just in front of the hind feet, sets the saddle instead.
saddleTop = back
rider = np.where((fitted[:, 2] > saddleTop * 1.02) & between)[0]
rump = ((fitted[:, 1] > dfeet[:, 1].max() - dspan[1] * .25) & (fitted[:, 1] < dfeet[:, 1].max() + dspan[1] * .05) &
        (np.abs(fitted[:, 0] - dfeet[:, 0].mean()) < dspan[0] * .35))
if len(rider) < len(fitted) * .05 and rump.any():
    saddleTop = min(back, float(np.percentile(fitted[rump][:, 2], 90)))
    rider = np.where((fitted[:, 2] > saddleTop * 1.02) & between)[0]
# "saddle" in the roster names the height of the animal's back, as a share of the model's height, when neither guess
# finds the rider (a ram's upright tail tops its rump).
if args.get('saddle'):
    saddleTop = args['saddle'] * tp[:, 2].max() * scale[2]
    rider = np.where((fitted[:, 2] > saddleTop) & between)[0]

# A scale per axis lines up the span of the feet and the belly, but not a foot set forward of its pair, nor a head held
# low and forward: the leg would then copy weights off the donor's belly, and the neck off its chest. A thin-plate
# spline therefore pins each foot and the muzzle onto the donor's, while the hips and shoulders, above the middle of
# each pair of feet, and the belly keep the plain fit. (The tail is fitted the other way round, further down: the
# donor's tail swings onto the Meshy tail.)
def extremes(p, feet, belly, skip):
    cx = feet[:, 0].mean(); wide = (feet[:, 0].max() - feet[:, 0].min()) * .35
    keep = np.ones(len(p), bool); keep[skip] = False
    core = keep & (np.abs(p[:, 0] - cx) < wide)
    head = p[core & (p[:, 1] < feet[:, 1].min()) & (p[:, 2] > belly * .9) & (p[:, 2] < back * 1.35)]
    tail = p[core & (p[:, 1] > feet[:, 1].max()) & (p[:, 2] > back * .04) & (p[:, 2] < back * 1.1)]
    reach = back * .03
    marks = {}
    if len(head): marks['muzzle'] = head[head[:, 1] < head[:, 1].min() + reach].mean(0)
    if len(tail): marks['tail'] = tail[tail[:, 1] > tail[:, 1].max() - reach].mean(0)
    return marks
def spline(src, dst):
    n = len(src)
    K = np.linalg.norm(src[:, None] - src[None], axis=2)
    P = np.hstack([np.ones((n, 1)), src])
    L = np.zeros((n + 4, n + 4)); L[:n, :n] = K; L[:n, n:] = P; L[n:, :n] = P.T
    W = np.linalg.solve(L, np.vstack([dst, np.zeros((4, 3))]))
    return lambda x: np.linalg.norm(x[:, None] - src[None], axis=2) @ W[:n] + np.hstack([np.ones((len(x), 1)), x]) @ W[n:]
fit = args.get('fit', ['muzzle', 'tail'])
dmarks = extremes(dp, dfeet, dbelly, [])
tmarks = extremes(fitted, dfeet, dbelly, rider)
pins = [m for m in ['muzzle'] if m in fit and m in dmarks and m in tmarks]
held = [np.array([f[0], dfeet[k // 2 * 2:k // 2 * 2 + 2, 1].mean(), dbelly]) for k, f in enumerate(dfeet)] + [np.array([*dfeet.mean(0), dbelly])]
tfeetFitted = (np.hstack([tfeet, np.zeros((4, 1))]) - tcentre) * scale + dcentre
src = np.array(list(tfeetFitted) + held + [tmarks[m] for m in pins])
dst = np.array([np.array([*f, 0]) for f in dfeet] + held + [dmarks[m] for m in pins])
bend, unbend = spline(src, dst), spline(dst, src)
warped = bend(fitted)
for v, q in zip(target.data.vertices, warped): v.co = Vector(q)
target.data.update()

# Weights: nearest point on the donor's surface.
for o in scene.objects: o.select_set(False)
target.select_set(True); bpy.context.view_layer.objects.active = target
for group in donor.vertex_groups: target.vertex_groups.new(name=group.name)
transfer = target.modifiers.new('weights', 'DATA_TRANSFER')
transfer.object = donor; transfer.use_vert_data = True; transfer.data_types_verts = {'VGROUP_WEIGHTS'}
transfer.vert_mapping = 'POLYINTERP_NEAREST'; transfer.layers_vgroup_select_src = 'ALL'; transfer.layers_vgroup_select_dst = 'NAME'
bpy.ops.object.modifier_apply(modifier=transfer.name)
# The rider and whatever it carries ride the back rigidly. Meshy models the animal as one piece of surface and the
# rider and its gear as others, so the pieces decide: a piece that stands mostly above the back is ridden rigidly,
# boots and stirrups hanging down the flanks included; the animal's own piece never is, so a head tossed up, antlers
# or horns stay the head's. Only when the rider is fused into the animal's piece does height alone decide there,
# and then what the head carries stays the head's.
saddle = args.get('saddleBone', 'Torso')
parent = list(range(len(tp)))
def find(a):
    while parent[a] != a:
        parent[a] = parent[parent[a]]; a = parent[a]
    return a
for e in target.data.edges:
    a, b = find(e.vertices[0]), find(e.vertices[1])
    if a != b: parent[a] = b
piece = np.array([find(i) for i in range(len(tp))])
pieces, sizes = np.unique(piece, return_counts=True)
animal = pieces[np.argmax(sizes)]
above = np.zeros(len(tp), bool); above[rider] = True
headGroups = {g.index for g in target.vertex_groups if g.name.startswith(('Head', 'Ear'))}
ownHead = np.array([sum(e.weight for e in v.groups if e.group in headGroups) >= .5 for v in target.data.vertices])
ridden = above & (piece == animal) & ~ownHead
for k in pieces:
    if k != animal and above[piece == k].mean() >= .5: ridden |= piece == k
rider = np.where(ridden)[0]
riderSet = set(int(i) for i in rider)
for group in target.vertex_groups:
    group.remove([int(i) for i in rider]) if len(rider) else None
if len(rider): target.vertex_groups[saddle].add([int(i) for i in rider], 1.0, 'REPLACE')
# A Meshy ear is a small nub where the donor's is a long chain of four bones; it rides the head rather than flick into
# a spike.
if 'Head' in target.vertex_groups:
    ears = {g.index for g in target.vertex_groups if g.name.startswith('Ear')}
    moved = {}
    for v in target.data.vertices:
        weight = sum(e.weight for e in v.groups if e.group in ears)
        if weight > 0: moved[v.index] = weight
    for g in target.vertex_groups:
        if g.index in ears: g.remove(list(moved))
    for index, weight in moved.items(): target.vertex_groups['Head'].add([index], weight, 'ADD')
# Give the model its own shape back, and move the skeleton onto it: bone heads and tails go through the inverse of the
# warp. Bones keep their roll and the clips their local rotations, so the donor's motion survives the move.
for v, q in zip(target.data.vertices, tp): v.co = Vector(q)
target.data.update()
bpy.data.objects.remove(donor, do_unlink=True)
for o in scene.objects: o.select_set(False)
arm.select_set(True); bpy.context.view_layer.objects.active = arm
bpy.ops.object.mode_set(mode='EDIT')
for bone in arm.data.edit_bones:
    ends = unbend(np.array([bone.head[:], bone.tail[:]], np.float64))
    bone.head = Vector((ends[0] - dcentre) / scale + tcentre)
    bone.tail = Vector((ends[1] - dcentre) / scale + tcentre)
# The donor's tail swings about its root, and stretches, until its tip is the Meshy tail's tip.
tailFit = {}
chain = sorted([b for b in arm.data.edit_bones if b.name.startswith('Tail') and b.name[4:].isdigit()], key=lambda b: int(b.name[4:]))
if 'tail' in fit and 'tail' in tmarks and chain:
    root = np.array(chain[0].head[:]); tip = (tmarks['tail'] - dcentre) / scale + tcentre
    v = np.array(chain[-1].tail[:]) - root; w = tip - root
    turn = math.atan2(w[2], w[1]) - math.atan2(v[2], v[1]); stretch = math.hypot(w[1], w[2]) / max(1e-6, math.hypot(v[1], v[2]))
    # A stub of a tail far from the donor's would stretch the chain into a lever; past half again its length it stops.
    stretch = min(stretch, 1.5)
    tailFit = {'turn': round(math.degrees(turn), 1), 'stretch': round(stretch, 2)}
    c, s = math.cos(turn), math.sin(turn)
    def swing(point):
        d = np.array(point[:]) - root
        return Vector((root[0] + d[0], root[1] + stretch * (c * d[1] - s * d[2]), root[2] + stretch * (s * d[1] + c * d[2])))
    for b in chain:
        b.head, b.tail = swing(b.head), swing(b.tail)
bpy.ops.object.mode_set(mode='OBJECT')
# Weights read off the donor's surface still go wrong where the Meshy model is shaped differently. Three corrections,
# made where the model has its own shape and the skeleton sits in it:
# - The hoof rides the lower leg. The donor's hooves follow its IK controls, whose paths were keyed for the donor's leg
#   lengths; on the Meshy legs they would drift off the leg and sink into the ground.
# - A lower leg carries only what lies within a hoof's thickness of it (read from the Meshy hooves), from the knee down
#   to the ground; the rest goes to the upper leg, or to the tail behind the tail's root. A tail or a cloth hanging
#   close behind a hock no longer stretches after every stride.
# - Only the tail swings with the tail: a vertex behind the tail's root, and nearer a tail bone than the body's, keeps
#   its leg weights and takes the rest from the two nearest tail bones. Every other vertex drops what it took from the
#   tail, so a caparison over the rump stays put. tailMargin keeps the first stretch behind the root still as well, as
#   a share of the feet's length: a bare tail hanging straight from the rump needs none, the knight's caparison 0.12.
#   With tailClear the tail keeps no leg weights either: a camel's tail hangs so close behind the hocks that it copies
#   theirs and tears into shards at every stride (sultanate_camel_rider: Walk stretched 102 -> 32, Run 167 -> 58).
bones = [b for b in arm.data.bones if not b.name.startswith('PoleTarget') and b.name in target.vertex_groups]
names = [b.name for b in bones]
heads = np.array([(arm.matrix_world @ b.head_local)[:] for b in bones]); ends = np.array([(arm.matrix_world @ b.tail_local)[:] for b in bones])
isTail = np.array([n.startswith('Tail') for n in names])
isLower = np.array(['LowerLeg' in n for n in names])
isLeg = np.array(['Leg' in n or 'Shoulder' in n or n.startswith(('IK', 'FF')) for n in names])
isCore = np.array([n in ('Body', 'Back') or n.startswith('Torso') for n in names])
ends[isLower, 2] = 0
span = ends - heads
t = np.clip(((tp[:, None] - heads[None]) * span[None]).sum(2) / np.maximum((span ** 2).sum(1), 1e-12)[None], 0, 1)
dist = np.linalg.norm(tp[:, None] - (heads[None] + t[..., None] * span[None]), axis=2)
low = tp[:, 2] < tp[:, 2].max() * .06
hoof = np.argmin(((tp[low][:, None, :2] - tfeet[None]) ** 2).sum(2), 1)
thick = float(np.median([np.percentile(np.linalg.norm(tp[low][hoof == k][:, :2] - tfeet[k], axis=1), 90) for k in range(4) if (hoof == k).any()]))
keepLower = np.clip((thick * 1.6 - np.where(isLower[None], dist, np.inf).min(1)) / (thick * .6), 0, 1)
keepLower[low] = 1
# "cloth" in the roster: a saddle cloth or a fleece hangs over the flanks. Above the belly it rides the body, not the
# shoulders and hips, which would tear it at every stride; the legs take over again below the belly.
flank = np.zeros(len(tp))
if args.get('cloth'):
    flank = np.clip((tp[:, 2] - tbelly) / (tbelly * .25), 0, 1)
    flank[(tp[:, 1] < tfeet[:, 1].min() - tspan[1] * .1) | (tp[:, 1] > tfeet[:, 1].max() + tspan[1] * .1)] = 0
tailDist = np.where(isTail[None], dist, np.inf)
root = arm.matrix_world @ arm.data.bones['Tail1'].head_local if 'Tail1' in arm.data.bones else None
free = isTail[np.where((isCore | isTail)[None], dist, np.inf).argmin(1)] & (tp[:, 1] > (root.y if root else np.inf) + tspan[1] * float(args.get('tailMargin', .12)))
free[rider] = False
hips = 'Back' if 'Back' in target.vertex_groups else saddle
byIndex = {g.index: g.name for g in target.vertex_groups}
tailVertices = int(free.sum()); thinned = 0
for v in target.data.vertices:
    i = v.index
    if i in riderSet: continue
    old = {byIndex[e.group]: e.weight for e in v.groups if e.weight > 0}
    new = {}; spare = {}
    def put(bag, name, weight): bag[name] = bag.get(name, 0) + weight
    for name, weight in old.items():
        if name.startswith(('IK', 'FF')):
            leg = ('Back' if name.startswith(('IKBack', 'FFB')) else 'Front') + 'LowerLeg.' + name[-1]
            if leg in target.vertex_groups: name = leg
        k = names.index(name) if name in names else -1
        if k >= 0 and isLeg[k] and free[i] and args.get('tailClear'):
            put(spare, 'tail', weight); continue
        if k >= 0 and isLeg[k] and flank[i] > 0:
            put(spare, 'body', weight * flank[i]); weight *= 1 - flank[i]
        if k >= 0 and isLower[k]:
            put(new, name, weight * keepLower[i])
            put(spare, 'tail' if free[i] else name.replace('Lower', 'Upper'), weight * (1 - keepLower[i]))
        elif k >= 0 and isTail[k] and not free[i]: put(new, hips, weight)
        elif free[i] and not (k >= 0 and isLeg[k]): put(spare, 'tail', weight)
        else: put(new, name, weight)
    for name, weight in spare.items():
        if weight < 1e-4: continue
        if keepLower[i] < 1 or flank[i] > 0: thinned += 1
        if name == 'tail':
            near = np.argsort(tailDist[i])[:2]; share = 1 / (tailDist[i][near] + 1e-4) ** 2; share /= share.sum()
            for k, part in zip(near, share): put(new, names[k], weight * float(part))
        else:
            put(new, name if name != 'body' and name in target.vertex_groups else names[int(np.argmin(np.where(isCore, dist[i], np.inf)))], weight)
    if new != old:
        for name in old: target.vertex_groups[name].remove([i])
        for name, weight in new.items():
            if weight > 1e-4: target.vertex_groups[name].add([i], float(weight), 'REPLACE')
# Meshy sometimes models a pair of legs as one block: the two lower legs fused together, or one of them hidden in the
# other. Stepping them apart would tear the block at every stride, so such a pair moves as one leg: each vertex takes
# the average of the pair's two sides.
# The same test also fires on two separate legs whose weights came out on one side, as when the model lifts a
# foot: the lifted leg is not where the donor's is, and loses its lower leg's weights. "splitLegs" in the roster
# (["Front"], ["Back"]) splits such a pair by side instead: every vertex below the belly gives the pair's weights, left
# plus right, to the side of the gap between the two legs it stands on, so each leg steps with its own donor leg. Only
# for legs that stand apart: a single column of a leg would tear lengthwise.
edges = np.array([e.vertices[:] for e in target.data.edges])
byIndex = {g.index: g.name for g in target.vertex_groups}
dominant = np.array([byIndex[max(v.groups, key=lambda e: e.weight).group] if len(v.groups) else '' for v in target.data.vertices])
fused = []; split = {}
for end in ('Front', 'Back'):
    a, b = dominant[edges[:, 0]], dominant[edges[:, 1]]
    left, right = end + 'LowerLeg.L', end + 'LowerLeg.R'
    bridges = int((((a == left) & (b == right)) | ((a == right) & (b == left))).sum())
    counts = sorted(int((dominant == side).sum()) for side in (left, right))
    pair = [n[:-2] for n in names if n.startswith(end) and n.endswith('.L') and n[:-2] + '.R' in target.vertex_groups]
    sides = [g.index for g in target.vertex_groups if g.name[:-2] in pair]
    share = np.zeros(len(tp))
    for v in target.data.vertices: share[v.index] = sum(e.weight for e in v.groups if e.group in sides)
    # The gap between the two legs: two means of the leg's x, from the ground to the belly.
    xs = tp[(share > .5) & (tp[:, 2] < tbelly), 0]
    if end in args.get('splitLegs', []) and len(xs) >= 8:
        cut = float(np.median(xs)); lo, hi = xs[xs <= cut], xs[xs > cut]
        for _ in range(20):
            lo, hi = xs[xs <= cut], xs[xs > cut]
            if not len(lo) or not len(hi): break
            cut = float((lo.mean() + hi.mean()) / 2)
        # Which side is the left: the donor's upper legs, now in the model.
        upper = end + 'UpperLeg'
        leftPlus = heads[names.index(upper + '.L'), 0] > heads[names.index(upper + '.R'), 0] if upper + '.L' in names else True
        blend = max(thick * .5, 1e-4)
        fade = np.clip((tbelly * 1.1 - tp[:, 2]) / (tbelly * .3), 0, 1)
        moved = 0
        for v in target.data.vertices:
            i = v.index
            if i in riderSet or fade[i] <= 0: continue
            w = {byIndex[e.group]: e.weight for e in v.groups}
            plus = float(np.clip((tp[i, 0] - cut) / blend + .5, 0, 1))
            for base in pair:
                l, r = w.get(base + '.L', 0), w.get(base + '.R', 0)
                if l + r <= 0: continue
                toLeft = (plus if leftPlus else 1 - plus) * (l + r)
                nl = fade[i] * toLeft + (1 - fade[i]) * l; nr = fade[i] * (l + r - toLeft) + (1 - fade[i]) * r
                if abs(nl - l) + abs(nr - r) < 1e-4: continue
                moved += 1
                for side, weight in ((base + '.L', nl), (base + '.R', nr)):
                    if weight > 1e-4: target.vertex_groups[side].add([i], float(weight), 'REPLACE')
                    else: target.vertex_groups[side].remove([i])
        split[end] = {'cut': round(cut, 4), 'gap': round(float(hi.min() - lo.max()) if len(lo) and len(hi) else 0, 4), 'moved': moved}
        continue
    if bridges < 12 and counts[0] >= counts[1] * .3: continue
    fused.append(end)
    for v in target.data.vertices:
        w = {byIndex[e.group]: e.weight for e in v.groups}
        for base in pair:
            l, r = w.get(base + '.L', 0), w.get(base + '.R', 0)
            if l + r <= 0 or l == r: continue
            target.vertex_groups[base + '.L'].add([v.index], (l + r) / 2, 'REPLACE')
            target.vertex_groups[base + '.R'].add([v.index], (l + r) / 2, 'REPLACE')
# tailDamp scales the tail's motion down: a caparison over the rump cannot follow a whole tail's flick, a long Meshy tail
# sweeps too wide, and a stub of a tail (a ram's, a stag's) is best held still with 0.
damp = float(args.get('tailDamp', 1))
if damp < 1:
    from mathutils import Quaternion
    for action in bpy.data.actions:
        bags = [action.layers[0].strips[0].channelbag(action.slots[0])] if len(action.slots) and len(action.layers) and len(action.layers[0].strips) else []
        curves = bags[0].fcurves if bags else action.fcurves
        byBone = {}
        for curve in curves:
            if curve.data_path.startswith('pose.bones["Tail') and curve.data_path.endswith('rotation_quaternion'):
                byBone.setdefault(curve.data_path, {})[curve.array_index] = curve
        for quad in byBone.values():
            if len(quad) < 4: continue
            for k in range(len(quad[0].keyframe_points)):
                q = Quaternion([quad[i].keyframe_points[k].co.y for i in range(4)])
                q = Quaternion().slerp(q, damp)
                for i in range(4):
                    key = quad[i].keyframe_points[k]; delta = q[i] - key.co.y
                    key.co.y += delta; key.handle_left.y += delta; key.handle_right.y += delta
            for curve in quad.values(): curve.update()
# Translation keys (the body's bob and drop) were authored at the donor's size.
shrink = 1 / float(np.mean(scale))
for action in bpy.data.actions:
    bags = []
    if len(action.slots) and len(action.layers) and len(action.layers[0].strips):
        bags = [action.layers[0].strips[0].channelbag(action.slots[0])]
    for curve in (bags[0].fcurves if bags else action.fcurves):
        if curve.data_path.endswith('.location'):
            for key in curve.keyframe_points:
                key.co.y *= shrink; key.handle_left.y *= shrink; key.handle_right.y *= shrink
            curve.update()
fitted = tp
modifier = target.modifiers.new('Armature', 'ARMATURE'); modifier.object = arm
target.parent = arm

# The donor's clips under our state names; the rest go.
keep = {}
for state, clip in args['clips'].items():
    action = bpy.data.actions.get(clip)
    if action is None: raise SystemExit('Donor has no clip ' + clip)
    keep[state] = action
for action in list(bpy.data.actions):
    if action not in keep.values(): bpy.data.actions.remove(action)
for state, action in keep.items():
    action.name = state; action.use_fake_user = True
scene.render.fps = 24
arm.animation_data.action = keep['Idle']
if len(keep['Idle'].slots): arm.animation_data.action_slot = keep['Idle'].slots[0]

# The donor's body height was keyed for its own legs; on the Meshy legs a gait can carry the hooves under the ground
# or over it. Standing and moving clips therefore lift or lower the body until the planted hooves (the lowest tenth of
# the clip's lowest points) touch the ground. An attack, a hit or a fall keeps its own drop.
def curvesOf(action):
    if len(action.slots) and len(action.layers) and len(action.layers[0].strips):
        return action.layers[0].strips[0].channelbag(action.slots[0]).fcurves
    return action.fcurves
settled = {}
body = arm.data.bones.get('Body')
for state in ('Idle', 'Walk', 'Run'):
    action = keep.get(state)
    if action is None or body is None: continue
    arm.animation_data.action = action
    if len(action.slots): arm.animation_data.action_slot = action.slots[0]
    s, e = action.frame_range
    lows = []
    for f in np.linspace(s, e, 25):
        scene.frame_set(int(f), subframe=f - int(f))
        mesh = target.evaluated_get(bpy.context.evaluated_depsgraph_get()).data
        lows.append(min((target.matrix_world @ v.co).z for v in mesh.vertices))
    drop = float(np.percentile(lows, 10))
    if abs(drop) < tp[:, 2].max() * .004: continue
    lift = (arm.matrix_world.to_3x3() @ body.matrix_local.to_3x3()).inverted() @ Vector((0, 0, -drop))
    curves = {c.array_index: c for c in curvesOf(action) if c.data_path == 'pose.bones["Body"].location'}
    for axis in range(3):
        curve = curves.get(axis)
        if curve is None:
            curve = curvesOf(action).new('pose.bones["Body"].location', index=axis)
            curve.keyframe_points.insert(s, 0); curve.keyframe_points.insert(e, 0)
        for key in curve.keyframe_points:
            key.co.y += lift[axis]; key.handle_left.y += lift[axis]; key.handle_right.y += lift[axis]
        curve.update()
    settled[state] = round(-drop / tp[:, 2].max(), 3)
arm.animation_data.action = keep['Idle']
if len(keep['Idle'].slots): arm.animation_data.action_slot = keep['Idle'].slots[0]

# An attack or a hit keeps its own motion, but nothing goes under the ground. A Meshy tail hangs lower than the donor's
# and a Meshy head is held lower, so the wolf's bite, which rears before it lunges, sank its tail a seventh of its height
# into the ground. Frame by frame the tail, then the neck, turns up about its root just enough, and whatever is still
# under (a hoof in a headbutt) lifts the body. Each correction eases in and out over a few frames. Death keeps its fall.
from mathutils import Quaternion
Htop = float(tp[:, 2].max()); tol = Htop * .004
vcount = len(target.data.vertices)
def lowest(mask=None):
    ev = target.evaluated_get(bpy.context.evaluated_depsgraph_get())
    co = np.zeros(vcount * 3); ev.data.vertices.foreach_get('co', co); co = co.reshape(-1, 3)
    M = np.array(target.matrix_world)
    z = co @ M[2, :3] + M[2, 3]
    return float(z[mask].min() if mask is not None else z.min())
def region(prefixes):
    groups = {g.index for g in target.vertex_groups if g.name.startswith(prefixes)}
    return np.array([sum(e.weight for e in v.groups if e.group in groups) >= .5 for v in target.data.vertices])
def ease(values):
    v = np.array(values, float)
    wide = np.array([v[max(0, k - 2):k + 3].max() for k in range(len(v))])
    return np.convolve(np.pad(wide, 1, mode='edge'), [.25, .5, .25], mode='valid')
def rekey(action, path, samples):
    curves = {c.array_index: c for c in curvesOf(action) if c.data_path == path}
    for axis in range(len(samples[0][1])):
        curve = curves.get(axis) or curvesOf(action).new(path, index=axis)
        curve.keyframe_points.clear()
        curve.keyframe_points.add(len(samples))
        for key, (frame, value) in zip(curve.keyframe_points, samples):
            key.co = (frame, value[axis]); key.interpolation = 'LINEAR'
        curve.update()
side = (arm.matrix_world.to_3x3().inverted() @ Vector((1, 0, 0))).normalized()
grounded = {}
for state in ('Attack', 'Hit'):
    action = keep.get(state)
    if action is None: continue
    arm.animation_data.action = action
    if len(action.slots): arm.animation_data.action_slot = action.slots[0]
    s, e = action.frame_range
    frames = sorted(set([float(s)] + list(range(int(math.ceil(s)), int(math.floor(e)) + 1)) + [float(e)]))
    report = {}
    for label, root, prefixes in (('tail', 'Tail1', ('Tail',)), ('neck', 'Neck1', ('Neck', 'Head', 'Ear', 'Jaw'))):
        pb = arm.pose.bones.get(root)
        mask = region(prefixes)
        if pb is None or not mask.any(): continue
        base, axes, need = [], [], []
        for f in frames:
            scene.frame_set(int(f), subframe=f - int(f))
            q0 = pb.rotation_quaternion.copy(); axis = (pb.matrix.to_3x3().inverted() @ side).normalized()
            base.append(q0); axes.append(axis)
            if lowest(mask) >= -tol: need.append(0.0); continue
            def at(angle):
                pb.rotation_quaternion = q0 @ Quaternion(axis, angle); bpy.context.view_layer.update()
                return lowest(mask)
            sign = 1 if at(.15) >= at(-.15) else -1
            lo, hi = 0.0, math.radians(75)
            if at(sign * hi) < 0: lo = hi
            for _ in range(14):
                mid = (lo + hi) / 2
                if at(sign * mid) >= 0: hi = mid
                else: lo = mid
            need.append(sign * hi)
            pb.rotation_quaternion = q0; bpy.context.view_layer.update()
        if not any(need): continue
        signs = np.sign([n for n in need if n]); sign = 1 if signs.sum() >= 0 else -1
        angles = ease(np.abs(need)) * sign
        samples, prev = [], None
        for f, q0, axis, angle in zip(frames, base, axes, angles):
            q = q0 @ Quaternion(axis, float(angle))
            if prev is not None and q.dot(prev) < 0: q.negate()
            prev = q; samples.append((f, tuple(q)))
        rekey(action, 'pose.bones["%s"].rotation_quaternion' % root, samples)
        report[label] = round(math.degrees(float(np.abs(angles).max())), 1)
    if body is not None:
        drops = []
        for f in frames:
            scene.frame_set(int(f), subframe=f - int(f)); low = lowest()
            drops.append(-low if low < -tol else 0.0)
        if any(drops):
            lift = ease(drops); up = (arm.matrix_world.to_3x3() @ body.matrix_local.to_3x3()).inverted() @ Vector((0, 0, 1))
            pb = arm.pose.bones['Body']; samples = []
            for f, amount in zip(frames, lift):
                scene.frame_set(int(f), subframe=f - int(f))
                samples.append((f, tuple(pb.location + up * float(amount))))
            rekey(action, 'pose.bones["Body"].location', samples)
            report['lift'] = round(float(lift.max()) / Htop, 4)
    if report: grounded[state] = report
arm.animation_data.action = keep['Idle']
if len(keep['Idle'].slots): arm.animation_data.action_slot = keep['Idle'].slots[0]

# Stride speed from the donor's own hoof travel over the stance part of each gait.
def reach(action, stance):
    arm.animation_data.action = action
    if len(action.slots): arm.animation_data.action_slot = action.slots[0]
    s, e = [int(x) for x in action.frame_range]
    ys = []
    for f in range(s, e + 1):
        scene.frame_set(f)
        bone = arm.pose.bones.get('FrontLowerLeg.L') or arm.pose.bones[0]
        ys.append((arm.matrix_world @ bone.tail).y)
    return (max(ys) - min(ys)) / max(1e-3, (e - s) / 24 * stance)
walk, run = reach(keep['Walk'], .6), reach(keep['Run'], .4)
arm.animation_data.action = keep['Idle']
if len(keep['Idle'].slots): arm.animation_data.action_slot = keep['Idle'].slots[0]
arm.name = 'Armature'

for o in scene.objects: o.select_set(False)
arm.select_set(True); target.select_set(True); bpy.context.view_layer.objects.active = arm
bpy.ops.export_scene.fbx(filepath=args['fbx'], use_selection=True, object_types={'MESH', 'ARMATURE'},
    axis_forward='-Z', axis_up='Y', apply_unit_scale=True, add_leaf_bones=False, armature_nodetype='NULL',
    use_armature_deform_only=True, bake_anim=True, bake_anim_use_all_actions=True, bake_anim_use_nla_strips=False,
    bake_anim_simplify_factor=0, path_mode='STRIP', embed_textures=False, mesh_smooth_type='FACE')
tris = sum(len(f.vertices) - 2 for f in target.data.polygons)
print('TRANSPLANT ' + json.dumps({'states': sorted(keep), 'triangles': tris, 'bones': len(arm.data.bones),
    'heightMetres': round(float(fitted[:, 2].max()), 3), 'walkMetresPerSecond': round(walk, 3), 'runMetresPerSecond': round(run, 3),
    'scale': [round(float(v), 3) for v in scale], 'riderVertices': int(len(rider)), 'donorBack': round(float(back), 3),
    'saddle': round(float(saddleTop / back), 3),
    'bellies': [round(float(tbelly / tp[:, 2].max()), 3), round(float(dbelly / dp[:, 2].max()), 3)],
    'pinned': {m: [round(float(v), 3) for v in dmarks[m] - tmarks[m]] for m in pins}, 'tailVertices': tailVertices,
    'thinnedVertices': thinned, 'legThickness': round(thick, 4), 'tailFit': tailFit, 'fusedLegs': fused, 'splitLegs': split,
    'settled': settled, 'grounded': grounded}))
'''


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('unit')
    parser.add_argument('--roster', default='tools/art/meshy_units.json')
    args = parser.parse_args()
    roster = json.loads((ROOT / args.roster).read_text(encoding='utf-8'))
    entry = next((e for e in roster['units'] if e['id'] == args.unit), None)
    if entry is None or not entry.get('donor'):
        raise SystemExit('%s names no donor in %s' % (args.unit, args.roster))
    unit = entry['id']
    raw = Path(os.path.expandvars(os.path.expanduser(roster['raw']))) / unit
    out = ROOT / roster['output'] / unit
    manifest_path = out / 'meshy-manifest.json'
    manifest = json.loads(manifest_path.read_text(encoding='utf-8'))
    coverage = finish.textures(entry, raw, out)
    clips = dict(DEFAULT_CLIPS); clips.update(entry.get('clips', {}))
    script = raw / 'transplant_blender.py'
    script.write_text(BLENDER_SCRIPT, encoding='utf-8')
    blender_args = {'id': unit, 'donor': str(DONORS / (entry['donor'] + '.gltf')), 'target': str(raw / (unit + '_model.glb')),
                    'fbx': str(out / (unit + '.fbx')), 'clips': clips, 'yaw': entry.get('yaw', 0), 'belly': entry.get('belly', 0),
                    'donorBelly': entry.get('donorBelly', 0), 'saddleBone': entry.get('saddleBone', 'Torso'),
                    'tailDamp': entry.get('tailDamp', 1), 'tailMargin': entry.get('tailMargin', .12),
                    'saddle': entry.get('saddle', 0), 'cloth': entry.get('cloth', False), 'fit': entry.get('fit', ['muzzle', 'tail']), 'tailClear': entry.get('tailClear', False),
                    'splitLegs': entry.get('splitLegs', [])}
    result = subprocess.run([finish.BLENDER, '-b', '--factory-startup', '-P', str(script), '--', json.dumps(blender_args)],
                            capture_output=True, text=True, encoding='utf-8', errors='replace')
    line = next((l for l in result.stdout.splitlines() if l.startswith('TRANSPLANT ')), None)
    if result.returncode != 0 or line is None:
        sys.stderr.write(result.stdout[-4000:] + result.stderr[-3000:])
        raise SystemExit('Blender failed for %s' % unit)
    report = json.loads(line[len('TRANSPLANT '):])
    report['teamMaskCoverage'] = round(coverage, 4)
    report['rig'] = 'transplanted from the Quaternius %s (CC0) by tools/art/transplant_rig.py' % entry['donor']
    manifest['finish'] = report
    meshy.save(manifest, manifest_path)
    print('TRANSPLANT_OK %s %s' % (unit, json.dumps(report)))


if __name__ == '__main__':
    main()
