"""Generate one building from tools/art/meshy_buildings.json with Meshy text-to-3D, then finish it for the game.

    python tools/art/meshy_building.py <building-id> [--max-credits 20] [--dry-run] [--finish-only]

Two paid steps, as for the test barracks: a Smart Topology preview (about 5,000 triangles, 5 credits) and a refine
that paints PBR textures onto it (10 credits). Each is recorded before the next starts, so an interrupted run resumes
instead of paying again. The finish is free: Blender fits the mesh to the building's footprint and height and writes
an FBX, and the textures are cut to 1024 with the owner-colour mask in the base map's alpha (see meshy_unit_finish).

Fortifications add five optional entry fields:

* "fit": "stretch" -- the model fills its box exactly, one scale per axis. Walls and gates stand side by side along X,
  so they take the whole footprint width (no margin, no gap between segments), 90% of its depth and the full height.
  A model whose axes would need to stretch more than 40% against the other two came back in the wrong proportions;
  it is refused, to be generated again, rather than distorted.
* "size": [x, y, z] in metres, for a piece smaller than a cell, such as a gate leaf.
* "thin": true -- the depth is a plate's thickness (a gate leaf): it is squashed to fit but left out of the 40% check,
  because Meshy gives flat pieces two or three times the thickness asked and face-on they read the same.
* "triangles": the triangle budget, asked of Meshy and enforced by decimation (5,000 by default, at most 6,000).
* "suffix": replaces the style's shape suffix, whose roofs and plaster suit houses but not a curtain wall.

Two more change where the look comes from:

* "texture": replaces the style's texture prompt for this entry's refine. The style's prompt describes houses (plaster,
  timber, blue only on banners), so a gate leaf painted with it lost its iron and its owner colour.
* "base": the name of a group in the roster's "groups". Text-to-3D never gave a wall or gate the game can use (25 tries:
  towers, cube-deep blocks, filled passages), so these pieces are built here in Blender at their exact size, per
  culture, and one Meshy retexture (10 credits) paints the whole group from the group's texture prompt. Run the script
  for each member; the first one pays and the others reuse the painted maps. A group's "cloth" colour dyes the
  banners, shields and stripes the builder made, where Meshy painted them as something else (the dwarf banners came
  back as bronze plaques), keeping Meshy's light and dark as detail.
* "teamHues", "teamSaturation", "teamWeight": override the style's owner-colour mask for one entry, as for a leaf whose
  iron came out a dull slate blue.

The key is read only from MESHY_API_KEY and never printed or written to disk.
"""
import argparse
import base64
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
COST = {'preview': 5, 'refine': 10, 'retexture': 10}

BLENDER_SCRIPT = r'''
import bpy, json, sys
import numpy as np
args = json.loads(sys.argv[sys.argv.index('--') + 1])
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=args['glb'])
meshes = [o for o in bpy.context.scene.objects if o.type == 'MESH']
bpy.ops.object.select_all(action='DESELECT')
for mesh in meshes: mesh.select_set(True)
bpy.context.view_layer.objects.active = meshes[0]
bpy.ops.object.parent_clear(type='CLEAR_KEEP_TRANSFORM')
if len(meshes) > 1: bpy.ops.object.join()
obj = bpy.context.view_layer.objects.active
obj.name = args['id']
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
points = np.empty(len(obj.data.vertices) * 3, dtype=np.float32)
obj.data.vertices.foreach_get('co', points); points = points.reshape(-1, 3)
low, high = points.min(0), points.max(0); size = high - low
# Blender is Z-up. The model fills 96% of the footprint's shorter side and stays under its height.
width, depth = args['footprint'] or args['size'][:2]
stretch = None
if args['fit'] == 'stretch':
    # Walls and gates tile along X: the whole width, 90% of the depth, the full height. A leaf fills its own size.
    target = np.array(args['size'] or [width, depth * .9, args['height']], np.float32)
    normals = np.empty(len(obj.data.loops) * 3, dtype=np.float32)
    obj.data.corner_normals.foreach_get('vector', normals); normals = normals.reshape(-1, 3)
    if (target[0] > target[1]) != (size[0] > size[1]):
        # Meshy laid the piece out along Y: a quarter turn about Z puts its length on X.
        points = (points[:, [1, 0, 2]] * np.array([-1, 1, 1], np.float32)).astype(np.float32)
        normals = (normals[:, [1, 0, 2]] * np.array([-1, 1, 1], np.float32)).astype(np.float32)
        low, high = points.min(0), points.max(0); size = high - low
    scale = (target / size).astype(np.float32)
    axes = [0, 2] if args['thin'] else [0, 1, 2]
    stretch = [round(float(scale[i] / np.mean([scale[j] for j in axes if j != i])), 3) for i in axes]
    if max(max(s, 1 / s) for s in stretch) > 1.4:
        print('MESHY_BUILDING ' + json.dumps({'refused': 'an axis would stretch over 40%% against the others: %s' % stretch,
            'stretch': stretch, 'sourceProportions': [round(float(v / size.max()), 3) for v in size]}))
        sys.exit(0)
else:
    scale = min(width * .96 / size[0], depth * .96 / size[1], args['height'] / size[2]) if width != depth else \
        min(width * .96 / max(size[0], size[1]), args['height'] / size[2])
points = (points - np.array([(low[0] + high[0]) / 2, (low[1] + high[1]) / 2, low[2]], np.float32)) * scale
obj.data.vertices.foreach_set('co', points.ravel()); obj.data.update()
if stretch:
    # Normals take the inverse scale, so faces that were stretched still shade as they are now tilted.
    normals = normals / scale; normals /= np.maximum(np.linalg.norm(normals, axis=1, keepdims=True), 1e-8)
    obj.data.normals_split_custom_set(normals.tolist())
source = sum(len(p.vertices) - 2 for p in obj.data.polygons)
triangles = source
for attempt in range(4):
    if triangles <= (args['triangles'] or 6000): break
    modifier = obj.modifiers.new('RTS triangle budget', 'DECIMATE')
    modifier.decimate_type = 'COLLAPSE'; modifier.ratio = (args['triangles'] or 5000) / triangles; modifier.use_collapse_triangulate = True
    bpy.ops.object.modifier_apply(modifier=modifier.name)
    triangles = sum(len(p.vertices) - 2 for p in obj.data.polygons)
bpy.ops.object.select_all(action='DESELECT'); obj.select_set(True); bpy.context.view_layer.objects.active = obj
bpy.ops.export_scene.fbx(filepath=args['fbx'], use_selection=True, object_types={'MESH'}, apply_unit_scale=True,
    apply_scale_options='FBX_SCALE_UNITS', axis_forward='-Z', axis_up='Y', use_space_transform=True,
    bake_space_transform=False, use_mesh_modifiers=True, mesh_smooth_type='FACE', use_tspace=False,
    add_leaf_bones=False, bake_anim=False, path_mode='STRIP', embed_textures=False)
print('MESHY_BUILDING ' + json.dumps({'sourceTriangles': source, 'triangles': triangles,
    'fittedMetres': [round(float(v), 3) for v in size * scale], **({'fit': 'stretch', 'stretch': stretch} if stretch else {}),
    **({'thicknessScale': round(float(scale[1] / np.mean(scale[[0, 2]])), 3)} if args['thin'] else {})}))
'''

# Walls, gates and a leaf are built here rather than asked of Meshy: 25 text-to-3D tries never gave a thin segment with
# flat ends, a walk at 3 m or an open passage. Blender is Z-up; a piece runs along X, faces -Y and +Y and stands on
# z = 0. Every culture keeps the game's numbers: the walk at 3.0 m, the teeth up to 3.7 m, the faces within 0.45 m of
# the centre line where ladders lean, ends cut flat at +-length/2 with half a tooth each so a run of segments tiles, and
# the gate's clear passage 1.3 x 2.4 m, which the 1.3 x 0.15 x 2.4 m leaf fills. Each piece is unwrapped on its own; the
# group model Meshy paints lays the pieces side by side and squeezes each one's UVs into its own strip of the maps.
BASE_SCRIPT = r'''
import bpy, json, math, random, sys
from mathutils import Vector
args = json.loads(sys.argv[sys.argv.index('--') + 1])
DECK, TOP, FACE, OPEN_W, OPEN_H = 3.0, 3.7, .45, 1.3, 2.4


def newell(points):
    n = Vector((0, 0, 0))
    for i, a in enumerate(points):
        b = points[(i + 1) % len(points)]
        n += Vector(((a[1] - b[1]) * (a[2] + b[2]), (a[2] - b[2]) * (a[0] + b[0]), (a[0] - b[0]) * (a[1] + b[1])))
    return n


class Part:
    """The faces of one piece, wound outward. Solids overlap freely; faces nothing can see are left out."""

    def __init__(self, length):
        self.L, self.v, self.f, self.smooth, self.cloth, self.dyed = length, [], [], [], [], False

    def add(self, points, smooth=False):
        i = len(self.v); self.v += [tuple(p) for p in points]
        self.f.append(tuple(range(i, i + len(points)))); self.smooth.append(smooth); self.cloth.append(self.dyed)

    def box(self, x0, x1, y0, y1, z0, z1, skip=''):
        """skip leaves out covered faces: b(ottom), t(op), l (-x), r (+x), f (-y), k (+y)."""
        if min(x1 - x0, y1 - y0, z1 - z0) < 1e-4: return
        c = lambda i, j, k: ((x0, x1)[i], (y0, y1)[j], (z0, z1)[k])
        for name, quad in (('b', (c(0, 0, 0), c(0, 1, 0), c(1, 1, 0), c(1, 0, 0))), ('t', (c(0, 0, 1), c(1, 0, 1), c(1, 1, 1), c(0, 1, 1))),
                           ('f', (c(0, 0, 0), c(1, 0, 0), c(1, 0, 1), c(0, 0, 1))), ('k', (c(0, 1, 0), c(0, 1, 1), c(1, 1, 1), c(1, 1, 0))),
                           ('l', (c(0, 0, 0), c(0, 0, 1), c(0, 1, 1), c(0, 1, 0))), ('r', (c(1, 0, 0), c(1, 1, 0), c(1, 1, 1), c(1, 0, 1)))):
            if name not in skip: self.add(quad)

    def pair(self, x0, x1, y0, y1, z0, z1, skip=''):
        """One box on the front face and its mirror on the back: y0 < y1 measured out from the centre line, and 'i'
        in skip leaves out the side facing the centre line."""
        self.box(x0, x1, -y1, -y0, z0, z1, skip.replace('i', 'k'))
        self.box(x0, x1, y0, y1, z0, z1, skip.replace('i', 'f'))

    def pierced(self, x0, x1, y0, y1, z0, z1, holes):
        """A face slab on both sides with rectangular holes (x0, x1, z0, z1) through it, such as arrow slits."""
        edges = sorted({x0, x1} | {h[0] for h in holes} | {h[1] for h in holes})
        for a, b in zip(edges, edges[1:]):
            cut = [h for h in holes if h[0] <= a and b <= h[1]]
            bands = [(z0, cut[0][2]), (cut[0][3], z1)] if cut else [(z0, z1)]
            for lo, hi in bands: self.pair(a, b, y0, y1, lo, hi, 'i')

    def prism(self, poly, d0, d1, axis='y', smooth=False):
        """A polygon in (x, z) pushed along Y from d0 to d1 (d0 < d1), or one in (y, z) pushed along X."""
        P = (lambda a, b, d: (a, d, b)) if axis == 'y' else (lambda a, b, d: (d, a, b))
        if newell([P(a, b, d0) for a, b in poly]).dot(Vector((0, 1, 0)) if axis == 'y' else Vector((1, 0, 0))) > 0: poly = poly[::-1]
        self.add([P(a, b, d0) for a, b in poly]); self.add([P(a, b, d1) for a, b in poly][::-1])
        for i, a in enumerate(poly):
            b = poly[(i + 1) % len(poly)]
            self.add([P(*a, d0), P(*a, d1), P(*b, d1), P(*b, d0)], smooth)

    def both(self, poly, d0, d1):
        """A flat shape on both faces, d0 < d1 measured out from the centre line."""
        self.prism(poly, -d1, -d0); self.prism(poly, d0, d1)

    def tube(self, p0, p1, r0, r1, n, caps=(True, True), smooth=True, phase=0.0):
        """A log, rod or disc from p0 to p1; r1 = 0 makes a point."""
        p0, p1 = Vector(p0), Vector(p1); axis = (p1 - p0).normalized()
        a = Vector((0, -1, 0)) if abs(axis.y) < .9 else Vector((0, 0, 1)); a = (a - axis * a.dot(axis)).normalized(); b = axis.cross(a)
        ring = lambda p, r: [p + r * (math.cos(2 * math.pi * i / n + phase) * a + math.sin(2 * math.pi * i / n + phase) * b) for i in range(n)]
        R0, R1 = ring(p0, r0), ring(p1, r1)
        for i in range(n):
            j = (i + 1) % n
            self.add([R0[i], R0[j], p1] if r1 == 0 else [R0[i], R0[j], R1[j], R1[i]], smooth)
        if caps[0]: self.add(R0[::-1])
        if caps[1] and r1 > 0: self.add(R1)

    def sweep(self, path, radii, n):
        """A round rib or horn along a path of points."""
        rings = []
        for i, p in enumerate(path):
            t = (Vector(path[min(i + 1, len(path) - 1)]) - Vector(path[max(i - 1, 0)])).normalized()
            a = Vector((0, -1, 0)); a = (a - t * a.dot(t)).normalized(); b = t.cross(a)
            rings.append([Vector(p) + radii[i] * (math.cos(2 * math.pi * k / n) * a + math.sin(2 * math.pi * k / n) * b) for k in range(n)])
        for r0, r1 in zip(rings, rings[1:]):
            for k in range(n): self.add([r0[k], r0[(k + 1) % n], r1[(k + 1) % n], r1[k]], True)
        self.add(rings[0][::-1]); self.add(rings[-1])

    def blob(self, c, radii, segments, rings):
        """A low-poly ellipsoid, such as a skull."""
        c = Vector(c); lat = []
        for i in range(1, rings):
            t = math.pi * i / rings
            lat.append([c + Vector((radii[0] * math.sin(t) * math.cos(2 * math.pi * k / segments), radii[1] * math.sin(t) * math.sin(2 * math.pi * k / segments),
                                    radii[2] * math.cos(t))) for k in range(segments)])
        top, bottom = c + Vector((0, 0, radii[2])), c - Vector((0, 0, radii[2]))
        faces = [[top, lat[0][k], lat[0][(k + 1) % segments]] for k in range(segments)]
        faces += [[bottom, lat[-1][(k + 1) % segments], lat[-1][k]] for k in range(segments)]
        faces += [[r0[k], r1[k], r1[(k + 1) % segments], r0[(k + 1) % segments]] for r0, r1 in zip(lat, lat[1:]) for k in range(segments)]
        for f in faces:
            self.add(f if newell(f).dot(sum(f, Vector()) / len(f) - c) > 0 else f[::-1], True)

    def rod(self, x0, x1, z, r=.016, y=.432):
        """A hanging rod on both faces, kept inside the piece's ends."""
        x0, x1 = max(x0, -self.L / 2 + r), min(x1, self.L / 2 - r)
        for s in (-1, 1): self.tube((x0, s * y, z), (x1, s * y, z), r, r, 6)

    def banner(self, x, hw, top, bottom, notch, d=(.425, .44), rod=True):
        """A cloth on both faces: notch > 0 cuts a swallowtail, notch < 0 ends in a point."""
        side, tip = (bottom, bottom + notch) if notch > 0 else (bottom - notch, bottom)
        self.dyed = True
        self.both([(x - hw, top), (x + hw, top), (x + hw, side), (x, tip), (x - hw, side)], *d)
        self.dyed = False
        if rod: self.rod(x - hw - .04, x + hw + .04, top + .02)


def rotated(cx, cz, length, width, angle):
    """A plank's outline in (x, z), centred on (cx, cz), its length turned `angle` degrees from upright."""
    d = Vector((math.sin(math.radians(angle)), math.cos(math.radians(angle)))); s = Vector((d.y, -d.x)); c = Vector((cx, cz))
    return [tuple(c + u * length / 2 * d + v * width / 2 * s) for u, v in ((-1, -1), (-1, 1), (1, 1), (1, -1))]


def teeth(L, width, phase=0.0):
    """Teeth every half metre from one end, cut at the ends: a run of segments and gates keeps one rhythm, and the
    halves at every joint make whole teeth."""
    out, k = [], 0
    while -L / 2 + phase + k * .5 - width / 2 < L / 2:
        c = -L / 2 + phase + k * .5; x0, x1 = max(-L / 2, c - width / 2), min(L / 2, c + width / 2)
        if x1 - x0 > .01: out.append((c, x0, x1))
        k += 1
    return out


def spans(L, gate):
    """The solid runs along X: the whole length, or the two pillars either side of the passage."""
    return [(-L / 2, -OPEN_W / 2), (OPEN_W / 2, L / 2)] if gate else [(-L / 2, L / 2)]


def ogive(c, hw, z0, z1, L):
    """A pointed leaf-shaped tooth, halved at the piece's ends."""
    h = lambda f: z0 + (z1 - z0) * f
    pts = [(c - hw, z0), (c + hw, z0), (c + hw, h(.3)), (c + hw * .86, h(.56)), (c + hw * .55, h(.8)), (c, z1),
           (c - hw * .55, h(.8)), (c - hw * .86, h(.56)), (c - hw, h(.3))]
    if c - hw < -L / 2: pts = [(c, z0), (c + hw, z0)] + pts[2:6]
    if c + hw > L / 2: pts = [(c - hw, z0), (c, z0), (c, z1)] + pts[6:]
    return pts


def kingdom(p, gate):
    """Pale dressed ashlar: a moulded plinth, corbels under the walk, square merlons, blue banners, arrow slits."""
    L = p.L
    for x0, x1 in spans(L, gate):
        p.box(x0, x1, -FACE, FACE, 0, .26, 'b')
        p.prism([(-FACE, .26), (FACE, .26), (.42, .36), (-.42, .36)], x0, x1, axis='x')
        p.box(x0, x1, -.36, .36, .36, 2.84, 'b')
        p.pierced(x0, x1, .36, .42, .36, 2.84, [] if gate else [(-.99, -.92, 1.35, 2.05), (.92, .99, 1.35, 2.05)])
    if gate:
        p.box(-OPEN_W / 2, OPEN_W / 2, -.42, .42, OPEN_H, 2.84)
        for x0, x1 in ((-.72, -.65), (.65, .72)): p.pair(x0, x1, .42, FACE, .36, OPEN_H, 'bi')   # a moulded surround
        p.pair(-.72, .72, .42, FACE, OPEN_H, 2.56, 'i')
    p.box(-L / 2, L / 2, -FACE, FACE, 2.84, DECK)
    for c, x0, x1 in teeth(L, .12, phase=.25): p.pair(x0, x1, .42, FACE, 2.66, 2.84, 'it')          # corbels
    p.pair(-L / 2, L / 2, .28, .44, DECK, 3.36, 'b')
    for c, x0, x1 in teeth(L, .3): p.pair(x0, x1, .28, .44, 3.36, TOP, 'b')
    for x, hw, bottom in ([(-.85, .11, 1.55), (.85, .11, 1.55)] if gate else [(0, .28, 1.25)]):
        p.banner(x, hw, 2.62, bottom, .2)


def mountain(p, gate):
    """A fieldstone footing under upright timber logs, a heavy lintel beam, carved crossed beam ends as the teeth and
    round painted shields."""
    L = p.L
    for x0, x1 in spans(L, gate):
        p.prism([(-FACE, 0), (FACE, 0), (.43, 1.1), (-.43, 1.1)], x0, x1, axis='x')
    if gate:
        for s in (-1, 1):
            p.box(*sorted((s * .65, s * .8)), -.42, .42, 1.1, OPEN_H, 'b')                  # squared door posts
            p.box(*sorted((s * .8, s * 1.0)), -.3, .3, 1.1, OPEN_H, 'b')
            for y in (-.31, .31): p.tube((s * .9, y, 1.08), (s * .9, y, OPEN_H), .1, .1, 8, caps=(False, False))
        p.box(-L / 2, L / 2, -.44, .44, OPEN_H, 2.9)                                           # the lintel beam
        p.box(-L / 2, L / 2, -.3, .3, 2.9, DECK, 'b')
    else:
        p.box(-L / 2, L / 2, -.3, .3, 1.1, DECK, 'b')
        for z0, z1 in ((1.95, 2.07), (2.86, 2.98)): p.pair(-L / 2, L / 2, .40, .43, z0, z1)    # binding rails
    for i in range(int(round(L / .2))):
        x = -L / 2 + .1 + i * .2
        for y in (-.31, .31): p.tube((x, y, 2.9 if gate else 1.08), (x, y, 3.4), .1, .1, 8, caps=(False, True))
    for c, _, _ in teeth(L, .01, phase=.25):
        if abs(c) < L / 2 - .1:
            for angle in (-32, 32): p.both(rotated(c, 3.44, .56, .09, angle), .30, .40)
    for x in ([0] if gate else [-1.0, 0, 1.0]):
        z = 2.65 if gate else 2.48
        for s in (-1, 1):
            p.dyed = True
            p.tube((x, s * .425, z), (x, s * .442, z), .19, .19, 12, smooth=False)
            p.dyed = False
            p.tube((x, s * .442, z), (x, s * .45, z), .05, .05, 6, caps=(False, True), smooth=False)


def dwarf(p, gate):
    """Huge granite blocks in staggered courses on a stepped plinth, a bronze rune band, a corbelled cornice, squat
    teeth with bronze caps, bronze jambs and crimson banners."""
    L = p.L
    for x0, x1 in spans(L, gate):
        p.box(x0, x1, -FACE, FACE, 0, .4, 'b')
        p.box(x0, x1, -.44, .44, .4, .55, 'b')
        p.box(x0, x1, -.41, .41, .55, 2.76, 'b')
        for k, (z0, z1) in enumerate(((.55, 1.25), (1.25, 1.95), (1.95, 2.42))):
            x = x0 - (0 if gate else (k % 2) * .5)
            while x < x1 - .01:
                a, b = max(x0, x) + .015, min(x1, x + 1.0) - .015
                if b - a > .05: p.pair(a, b, .41, .435, z0 + .015, z1 - .015, 'i')
                x += 1.0
    if gate:
        p.box(-OPEN_W / 2, OPEN_W / 2, -.41, .41, OPEN_H, 2.76)
        for x0, x1 in ((-.72, -.65), (.65, .72)): p.pair(x0, x1, .41, FACE, .55, OPEN_H, 'i')
        p.pair(-.72, .72, .41, FACE, OPEN_H - .02, OPEN_H + .05, 'i')
    p.pair(-L / 2, L / 2, .41, FACE, 2.47, 2.66, 'i')                                          # the rune band
    p.box(-L / 2, L / 2, -.44, .44, 2.76, 2.88)
    p.box(-L / 2, L / 2, -FACE, FACE, 2.88, DECK)
    p.pair(-L / 2, L / 2, .26, .44, DECK, 3.36, 'b')
    for c, x0, x1 in teeth(L, .36): p.pair(x0, x1, .26, .44, 3.36, 3.6, 'b')
    for c, x0, x1 in teeth(L, .40): p.pair(x0, x1, .25, FACE, 3.6, TOP)
    for x, hw, bottom in ([(-.85, .11, 1.3), (.85, .11, 1.3)] if gate else [(0, .27, .95)]):
        p.banner(x, hw, 2.4, bottom, -.18, d=(.437, .447))


def dwarf_leaf(p):
    """A square-cornered bronze door slab: iron edge bands and rivets, a rune plaque and a crimson stripe."""
    W, H, T = OPEN_W, OPEN_H, .075
    p.box(-W / 2 + .06, W / 2 - .06, -.045, .045, .06, H - .06)
    for s in (-1, 1): p.box(*sorted((s * W / 2, s * (W / 2 - .09))), -T, T, 0, H)
    for z0, z1 in ((0, .1), (.72, .8), (2.3, H)): p.box(-W / 2 + .09, W / 2 - .09, -T, T, z0, z1)
    p.dyed = True
    p.box(-W / 2 + .09, W / 2 - .09, -.068, .068, 1.46, 1.76)
    p.dyed = False
    p.box(-.3, .3, -.062, .062, .86, 1.4); p.box(-.24, .24, -.07, .07, .92, 1.34)
    for z in (.05, .76, 2.35):
        for x in (-.45, -.15, .15, .45): p.box(x - .025, x + .025, -T, T, z - .025, z + .025)


def elf(p, gate):
    """Smooth ivory stone on a flared foot, a gold cornice, pointed leaf-shaped teeth, living-wood ribs with leaves
    (an arch round the passage) and blue banners."""
    L = p.L
    flare = [(-FACE, 0), (FACE, 0), (FACE, .08), (.435, .2), (.425, .32), (.42, .42), (-.42, .42), (-.425, .32), (-.435, .2), (-FACE, .08)]
    for x0, x1 in spans(L, gate):
        p.prism(flare, x0, x1, axis='x')
        p.box(x0, x1, -.42, .42, .42, 2.86, 'b')
    if gate: p.box(-OPEN_W / 2, OPEN_W / 2, -.42, .42, OPEN_H, 2.86)
    p.box(-L / 2, L / 2, -FACE, FACE, 2.86, 2.94)
    p.box(-L / 2, L / 2, -.44, .44, 2.94, DECK)
    p.pair(-L / 2, L / 2, .29, .43, DECK, 3.3, 'b')
    for c, x0, x1 in teeth(L, .26): p.both(ogive(c, .13, 3.3, TOP, L), .29, .43)
    leaves = []
    for s in (-1, 1):
        y = s * .405
        if gate:
            half = [(-.71, .42), (-.71, 1.4), (-.71, 2.3), (-.68, 2.5), (-.58, 2.64), (-.41, 2.74), (-.21, 2.8), (0, 2.83)]
            path = half + [(-x, z) for x, z in half[::-1][1:]]
            p.sweep([(x, y, z) for x, z in path], [.045] * len(path), 6)
            leaves += [(s, x * k, z, a * k) for k in (-1, 1) for x, z, a in ((-.82, 1.0, -50), (-.82, 1.75, 50), (-.5, 2.76, 70))]
        else:
            for x0 in (-1.0, 1.0):
                zs = (.42, .8, 1.2, 1.6, 2.0, 2.4, 2.84)
                p.sweep([(x0 + .07 * math.sin(z * 2.3), y, z) for z in zs], [.045, .044, .042, .04, .038, .036, .034], 6)
                leaves += [(s, x0 + .07 * math.sin(z * 2.3) + (.1 if k % 2 else -.1), z, 50 if k % 2 else -50) for k, z in enumerate((1.0, 1.7, 2.35))]
    for s, x, z, angle in leaves:
        poly = rotated(x, z, .18, .075, angle)
        p.prism(poly, -.447, -.435) if s < 0 else p.prism(poly, .435, .447)
    for x, hw, bottom in ([(-.87, .1, 1.7), (.87, .1, 1.7)] if gate else [(0, .22, 1.2)]):
        p.banner(x, hw, 2.78, bottom, -.22)


def logs(p, x0, x1, y, rng, base, top, tip, end=.13):
    """Upright sharpened logs side by side from x0 to x1; the two end logs are always the same so every joint matches."""
    radii = [end]
    while 2 * sum(radii) + 2 * end < x1 - x0 - .2: radii.append(rng.uniform(.105, .135))
    radii.append(end)
    scale, x = (x1 - x0) / (2 * sum(radii)), x0
    for i, r in enumerate(radii):
        c, x = x + r * scale, x + 2 * r * scale
        fixed, phase = i in (0, len(radii) - 1), rng.uniform(0, 1)
        t = top if fixed else top + rng.uniform(-.06, .05)
        q = tip if fixed else min(TOP, tip + rng.uniform(-.08, 0))
        p.tube((c, y, base), (c, y, t), r, r, 6, caps=(False, False), phase=phase)
        p.tube((c, y, t), (c, y, q), r, 0, 6, caps=(False, False), smooth=False, phase=phase)


def orc(p, gate):
    """Two rows of scorched sharpened logs round an earth core, rope lashings, bolted iron plates, blood-red rags and
    horned skulls; the gate hangs a log lintel between two great posts."""
    L = p.L
    rng = random.Random('orc gate' if gate else 'orc wall')
    if gate:
        for s in (-1, 1):
            p.box(*sorted((s * .70, s * .98)), -.2, .2, 0, DECK, 'b')
            for y in (-.27, .27):
                phase = rng.uniform(0, 1)
                p.tube((s * .825, y, 0), (s * .825, y, 3.36), .165, .165, 6, caps=(False, False), phase=phase)
                p.tube((s * .825, y, 3.36), (s * .825, y, TOP), .165, 0, 6, caps=(False, False), smooth=False, phase=phase)
        p.box(-.7, .7, -.2, .2, OPEN_H, DECK)
        for y in (-.25, .25):
            p.tube((-L / 2, y, 2.62), (L / 2, y, 2.62), .19, .19, 7)
            logs(p, -.66, .66, y * 1.2, rng, 2.75, 3.42, 3.66, end=.12)
    else:
        p.box(-L / 2, L / 2, -.2, .2, 0, DECK, 'b')
        for y in (-.3, .3): logs(p, -L / 2, L / 2, y, rng, 0, 3.42, TOP)
        for z in (.55, 2.5): p.pair(-L / 2, L / 2, .43, FACE, z, z + .07)                          # rope lashings
        for x, angle in ((-.95, 6), (.95, -5)): p.both(rotated(x, 1.45, .36, .52, 90 + angle), .43, FACE)   # iron plates
    for x, hw, bottom in ([(-.82, .1, 1.5), (.82, .1, 1.5)] if gate else [(0, .25, 1.05)]):
        rag = [(x + hw, bottom + .12), (x + hw * .5, bottom), (x, bottom + .16), (x - hw * .5, bottom + .02), (x - hw, bottom + .14)]
        p.both([(x - hw, 2.38), (x + hw, 2.38)] + rag, .438, .448)
        if not gate: p.rod(x - hw - .05, x + hw + .05, 2.41, r=.016)
    z = 2.95 if gate else 2.66
    for s in (-1, 1):
        p.blob((0, s * .40, z), (.1, .045, .11), 7, 4)
        for side in (-1, 1):
            p.sweep([(side * .07, s * .405, z + .05), (side * .15, s * .41, z + .08), (side * .21, s * .41, z + .16), (side * .2, s * .41, z + .25)],
                    [.035, .03, .02, .006], 5)


def sultanate_clip(poly, lo, hi):
    """A polygon in (x, z) cut to lo <= x <= hi, so a merlon at a piece's end is halved and the joint makes it whole."""
    def cut(points, inside, cross):
        out = []
        for i, a in enumerate(points):
            b = points[(i + 1) % len(points)]
            if inside(a):
                out.append(a)
                if not inside(b): out.append(cross(a, b))
            elif inside(b): out.append(cross(a, b))
        return out
    at = lambda x: (lambda a, b: (x, a[1] + (b[1] - a[1]) * (x - a[0]) / (b[0] - a[0])))
    poly = cut(poly, lambda q: q[0] >= lo - 1e-6, at(lo))
    return cut(poly, lambda q: q[0] <= hi + 1e-6, at(hi)) if poly else []


def sultanate_arch(c, hw, spring, n=9):
    """A horseshoe arch over an opening hw either side of c: an arc of 250 degrees from (c + hw, spring) over the top
    to (c - hw, spring), its centre above the spring line so it bulges wider than the opening."""
    r = hw / math.cos(math.radians(35))
    z0 = spring + r * math.sin(math.radians(35))
    return [(c + r * math.cos(math.radians(a)), z0 + r * math.sin(math.radians(a))) for a in [-35 + 250 * i / (n - 1) for i in range(n)]]


def sultanate_arcade(p, x0, x1, z0, z1, niches, d0, d1):
    """A raised band on both faces with horseshoe-arched niches cut up into it from its lower edge: a blind arcade."""
    poly = [(x0, z0)]
    for c, hw, spring in niches:
        poly += [(c - hw, z0), (c - hw, spring)] + sultanate_arch(c, hw, spring)[::-1][1:-1] + [(c + hw, spring), (c + hw, z0)]
    poly += [(x1, z0), (x1, z1), (x0, z1)]
    p.both(poly, d0, d1)


def sultanate_merlon(c, hw, z0, z1, L):
    """A stepped merlon with a pointed cap, halved at the piece's ends."""
    h = z1 - z0
    pts = [(c - hw, z0), (c + hw, z0), (c + hw, z0 + h * .45), (c + hw * .62, z0 + h * .45), (c + hw * .62, z0 + h * .78),
           (c, z1), (c - hw * .62, z0 + h * .78), (c - hw * .62, z0 + h * .45), (c - hw, z0 + h * .45)]
    return sultanate_clip(pts, -L / 2, L / 2)


def sultanate(p, gate):
    """A sandstone plinth under a whitewashed mudbrick wall, a blind arcade of horseshoe arches, timber beam ends under
    a projecting cornice, stepped merlons with pointed caps and turquoise banners. The gate frames its passage with
    pilasters and a lintel band, and hangs a banner either side."""
    L = p.L
    for x0, x1 in spans(L, gate):
        p.box(x0, x1, -FACE, FACE, 0, .3, 'b')                                              # sandstone plinth
        p.prism([(-FACE, .3), (FACE, .3), (.4, .42), (-.4, .42)], x0, x1, axis='x')         # its chamfer
        p.box(x0, x1, -.4, .4, .42, 2.66, 'b')
    if gate:
        p.box(-OPEN_W / 2, OPEN_W / 2, -.4, .4, OPEN_H, 2.66)
        for s in (-1, 1): p.pair(*sorted((s * .65, s * .8)), .4, .44, .3, 2.44, 'i')        # pilasters
        p.pair(-.8, .8, .4, .44, 2.44, 2.62, 'i')                                           # the lintel band
    else:
        sultanate_arcade(p, -L / 2, L / 2, 1.55, 2.62, [(c, .19, 2.15) for c in (-1.15, -.55, .55, 1.15)], .4, .44)
    p.box(-L / 2, L / 2, -.43, .43, 2.66, 2.74)                                             # string course
    for c, x0, x1 in teeth(L, .1, phase=.25): p.pair(x0, x1, .43, FACE, 2.74, 2.84, 'i')   # timber beam ends
    p.box(-L / 2, L / 2, -FACE, FACE, 2.84, DECK)                                           # cornice under the walk
    p.pair(-L / 2, L / 2, .28, .44, DECK, 3.3, 'b')                                         # parapet
    for c, x0, x1 in teeth(L, .32): p.both(sultanate_merlon(c, .16, 3.3, TOP, L), .28, .44)
    if gate:
        for x in (-.9, .9): p.banner(x, .08, 2.4, 1.45, -.14, d=(.44, .45))
    else:
        p.banner(0, .24, 2.6, 1.3, .2, d=(.44, .45))


def sultanate_leaf(p):
    """A carved timber door leaf: a plank slab in a stout frame, a raised horseshoe-arch panel with a round turquoise
    boss, and rows of bronze studs."""
    W, H, T = OPEN_W, OPEN_H, .075
    p.box(-W / 2 + .07, W / 2 - .07, -.05, .05, .07, H - .07)                              # the plank slab
    for s in (-1, 1): p.box(*sorted((s * W / 2, s * (W / 2 - .08))), -T, T, 0, H)         # stiles
    for z0, z1 in ((0, .1), (1.02, 1.1), (H - .09, H)): p.box(-W / 2 + .08, W / 2 - .08, -T, T, z0, z1)   # rails
    p.prism([(-.3, 1.16), (.3, 1.16), (.3, 1.65)] + sultanate_arch(0, .3, 1.65, 11)[1:-1] + [(-.3, 1.65)], -.066, .066)
    p.dyed = True
    p.tube((0, -.084, 1.86), (0, .084, 1.86), .12, .12, 12, smooth=False)                 # the boss, through both faces
    p.dyed = False
    for z in (.05, 1.06, H - .045):
        for x in (-.5, -.25, 0, .25, .5): p.box(x - .025, x + .025, -.082, .082, z - .025, z + .025)
    for z in (.3, .55, .8):
        for x in (-.4, -.13, .13, .4): p.box(x - .02, x + .02, -.08, .08, z - .02, z + .02)


CULTURES = {'kingdom': kingdom, 'mountain': mountain, 'dwarf': dwarf, 'elf': elf, 'orc': orc, 'sultanate': sultanate}
LEAVES = {'dwarf': dwarf_leaf, 'sultanate': sultanate_leaf}
LENGTH = {'wall': 3.0, 'gate': 2.0, 'gate_leaf': OPEN_W}

# --- sahel fortifications (staged 2026-09-27) ---
def sahel_clip(poly, lo, hi):
    """A polygon in (x, z) cut to lo <= x <= hi, so a merlon at a piece's end is halved and the joint makes it whole."""
    def cut(points, keep, cross):
        out = []
        for i, a in enumerate(points):
            b = points[(i + 1) % len(points)]
            if keep(a):
                out.append(a)
                if not keep(b): out.append(cross(a, b))
            elif keep(b): out.append(cross(a, b))
        return out
    at = lambda x: (lambda a, b: (x, a[1] + (b[1] - a[1]) * (x - a[0]) / (b[0] - a[0])))
    poly = cut(poly, lambda q: q[0] >= lo - 1e-6, at(lo))
    return cut(poly, lambda q: q[0] <= hi + 1e-6, at(hi)) if poly else []


def sahel_bullet(c, hw, z0, z1, n=6):
    """A merlon with a rounded top: straight sides, then a half circle."""
    zc = z1 - hw
    return [(c - hw, z0), (c + hw, z0)] + [(c + hw * math.cos(math.pi * i / n), zc + hw * math.sin(math.pi * i / n)) for i in range(n + 1)]


def sahel(p, gate):
    """Sun-dried mud on a battered footing: battered buttresses that rise into sugar-loaf pinnacles,
    staggered rows of projecting toron beam ends, a walk behind a parapet of round-topped merlons, a timber lintel over
    the gate and indigo banners."""
    L = p.L
    for x0, x1 in spans(L, gate):
        p.prism([(-FACE, 0), (FACE, 0), (.36, .45), (-.36, .45)], x0, x1, axis='x')                 # battered footing
        p.prism([(-.36, .45), (.36, .45), (.29, 2.86), (-.29, 2.86)], x0, x1, axis='x')             # the earthen wall
    if gate:
        p.box(-OPEN_W / 2, OPEN_W / 2, -.29, .29, OPEN_H, 2.86)
        p.pair(-.8, .8, .29, .4, OPEN_H, OPEN_H + .15, 'i')                                        # timber lintel
    p.pair(-L / 2, L / 2, .29, .38, 2.68, 2.86, 'i')                                               # a band under the walk
    p.box(-L / 2, L / 2, -FACE, FACE, 2.86, DECK)                                                  # the walk
    p.pair(-L / 2, L / 2, .26, .42, DECK, 3.3, 'b')                                                # parapet
    for c, _, _ in teeth(L, .01):
        poly = sahel_clip(sahel_bullet(c, .12, 3.3, 3.6), -L / 2, L / 2)
        if len(poly) > 2: p.both(poly, .26, .42)
    # Battered buttresses up both faces, each rising through the parapet into a sugar-loaf pinnacle.
    posts = [-.83, .83] if gate else [-.75, .75]
    for x in posts:
        for s in (-1, 1):
            p.prism([(s * .28, 0), (s * FACE, 0), (s * .39, 3.34), (s * .28, 3.34)], x - .14, x + .14, axis='x')
            p.tube((x, s * .335, 3.34), (x, s * .335, TOP), .115, .0, 8, caps=(True, False), smooth=False)
    # Toron: palm-wood beam ends in three staggered rows between the buttresses.
    for z, phase in ((1.05, .25), (1.8, 0.0), (2.5, .25)):
        for c, _, _ in teeth(L, .01, phase=phase):
            if gate and abs(c) < OPEN_W / 2 + .08 and z < OPEN_H + .3: continue
            if any(abs(c - x) < .24 for x in posts) or abs(c) > L / 2 - .08: continue
            for s in (-1, 1): p.tube((c, s * .26, z), (c, s * .45, z), .045, .045, 6, caps=(False, True), smooth=False)
    banners = [(-.36, .1, 3.22, 2.64, (.425, .44)), (.36, .1, 3.22, 2.64, (.425, .44))] if gate else [(0, .24, 2.62, 1.3, (.315, .33))]
    for x, hw, top, bottom, d in banners:
        p.banner(x, hw, top, bottom, .14, d=d, rod=False)
        for s in (-1, 1): p.tube((x - hw - .05, s * (d[0] + d[1]) / 2, top + .02), (x + hw + .05, s * (d[0] + d[1]) / 2, top + .02), .016, .016, 6)


def sahel_leaf(p):
    """A door of heavy vertical planks on two battens, rows of bronze studs, and an indigo band across its middle."""
    W, H = OPEN_W, OPEN_H
    n, gap = 5, .012
    pw = (W - (n - 1) * gap) / n
    for i in range(n):
        x0 = -W / 2 + i * (pw + gap)
        p.box(x0, x0 + pw, -.045, .045, 0, H)
    for z0, z1 in ((.3, .44), (1.96, 2.1)): p.pair(-W / 2 + .05, W / 2 - .05, .045, .07, z0, z1, 'i')
    p.dyed = True
    p.pair(-W / 2 + .05, W / 2 - .05, .045, .065, 1.08, 1.32, 'i')
    p.dyed = False
    for z in (.37, 2.03):
        for x in (-.5, -.25, 0, .25, .5): p.pair(x - .03, x + .03, .07, .085, z - .03, z + .03)


CULTURES['sahel'] = sahel
LEAVES['sahel'] = sahel_leaf
# --- end sahel fortifications ---

bpy.ops.wm.read_factory_settings(use_empty=True)
material = bpy.data.materials.new('base'); material.use_nodes = True
pieces, copies, layout, offset = [], [], [], 0.0
for piece in args['pieces']:
    p = Part(LENGTH[piece['building']])
    if piece['building'] == 'gate_leaf': LEAVES[args['culture']](p)
    else: CULTURES[args['culture']](p, piece['building'] == 'gate')
    mesh = bpy.data.meshes.new(piece['id']); mesh.from_pydata(p.v, [], p.f); mesh.update()
    for poly, smooth in zip(mesh.polygons, p.smooth): poly.use_smooth = smooth
    mesh.attributes.new('cloth', 'BOOLEAN', 'FACE').data.foreach_set('value', p.cloth)
    mesh.materials.append(material)
    obj = bpy.data.objects.new(piece['id'], mesh); bpy.context.scene.collection.objects.link(obj)
    bpy.ops.object.select_all(action='DESELECT'); obj.select_set(True); bpy.context.view_layer.objects.active = obj
    bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.mesh.remove_doubles(threshold=1e-5)
    bpy.ops.uv.smart_project(angle_limit=math.radians(66), island_margin=.012, area_weight=1, correct_aspect=True, scale_to_bounds=False)
    bpy.ops.object.mode_set(mode='OBJECT')
    # The piece as the game gets it: its own UVs over the whole of its maps.
    bpy.ops.export_scene.gltf(filepath=piece['glb'], export_format='GLB', use_selection=True, export_apply=True, export_yup=True)
    pieces.append(obj)
area = [sum(f.area for f in o.data.polygons) for o in pieces]
u0 = 0.0
for obj, a, piece in zip(pieces, area, args['pieces']):
    width = a / sum(area)
    copy = obj.copy(); copy.data = obj.data.copy(); bpy.context.scene.collection.objects.link(copy)
    uv = copy.data.uv_layers.active.data
    coords = [0.0] * (len(uv) * 2); uv.foreach_get('uv', coords)
    uv.foreach_set('uv', [u0 + c * width if i % 2 == 0 else c for i, c in enumerate(coords)])
    L = LENGTH[piece['building']]
    offset += L / 2
    copy.location.x = offset; offset += L / 2 + .8
    points = [v.co for v in obj.data.vertices]
    # Where the cloth lies in the piece's own UVs, so a group can dye it the owner colour Meshy did not paint.
    cloth, own = obj.data.attributes['cloth'].data, obj.data.uv_layers.active.data
    layout.append({'id': piece['id'], 'strip': [round(u0, 6), round(width, 6)], 'triangles': sum(len(f.vertices) - 2 for f in obj.data.polygons),
                   'bounds': [round(max(q[i] for q in points) - min(q[i] for q in points), 3) for i in range(3)],
                   'cloth': [[[round(own[l].uv[0], 5), round(own[l].uv[1], 5)] for l in f.loop_indices] for f in obj.data.polygons if cloth[f.index].value]})
    copies.append(copy); u0 += width
# The group Meshy paints: the pieces side by side, each one's UVs squeezed into its own strip.
bpy.ops.object.select_all(action='DESELECT')
for c in copies: c.select_set(True)
bpy.context.view_layer.objects.active = copies[0]
bpy.ops.object.join()
bpy.ops.export_scene.gltf(filepath=args['glb'], export_format='GLB', use_selection=True, export_apply=True, export_yup=True)
print('MESHY_BASE ' + json.dumps({'pieces': layout}))
'''


def paint(roster, entry, manifest, manifest_path, args):
    """A piece with a "base" is built here with the other pieces of its group (free), and one Meshy retexture paints the
    group together (10 credits), so a culture's wall, gate and leaf share one look. Each piece then keeps its own strip
    of the painted maps. The retexture is recorded in every member's manifest; the one that ran it carries the cost."""
    name = entry['base']
    members = [b for b in roster['buildings'] if b.get('base') == name]
    raw_root = Path(os.path.expandvars(os.path.expanduser(roster['raw'])))
    work = raw_root / name
    work.mkdir(parents=True, exist_ok=True)
    for member in members:
        (raw_root / member['id']).mkdir(parents=True, exist_ok=True)
    script = work / 'build_base.py'
    script.write_text(BASE_SCRIPT, encoding='utf-8')
    base_args = {'culture': entry['style'], 'glb': str(work / (name + '_base.glb')),
                 'pieces': [{'id': m['id'], 'building': m['building'], 'glb': str(raw_root / m['id'] / (m['id'] + '_model.glb'))} for m in members]}
    result = subprocess.run([finish.BLENDER, '-b', '--factory-startup', '-P', str(script), '--', json.dumps(base_args)],
                            capture_output=True, text=True, encoding='utf-8', errors='replace')
    line = next((l for l in result.stdout.splitlines() if l.startswith('MESHY_BASE ')), None)
    if result.returncode != 0 or line is None:
        sys.stderr.write(result.stdout[-3000:] + result.stderr[-3000:])
        raise SystemExit('Blender failed to build %s' % name)
    layout = json.loads(line[len('MESHY_BASE '):])['pieces']
    manifest['entry'], manifest['base'] = entry, next(p for p in layout if p['id'] == entry['id'])
    others = {}
    for member in members:
        if member['id'] != entry['id']:
            path = ROOT / roster['output'] / member['style'].capitalize() / member['id'] / 'meshy-manifest.json'
            others[member['id']] = (path, json.loads(path.read_text(encoding='utf-8')) if path.exists() else {
                'id': member['id'], 'entry': member, 'style': roster['styles'][member['style']], 'steps': {}, 'files': {}, 'credits': 0})
    step = manifest['steps'].get('retexture') or next((o['steps']['retexture'] for _, o in others.values() if 'retexture' in o['steps']), None)
    if step is None:
        if args.finish_only:
            raise SystemExit('%s has not been painted yet: run without --finish-only.' % name)
        if COST['retexture'] > args.max_credits:
            raise SystemExit('Stopping before the retexture: over the cap of %d credits.' % args.max_credits)
        key = meshy.api_key()
        glb = (work / (name + '_base.glb')).read_bytes()
        task_id = meshy.call('POST', '/v1/retexture', key, {
            'model_url': 'data:application/octet-stream;base64,' + base64.b64encode(glb).decode(), 'text_style_prompt': roster['groups'][name]['texture'][:600],
            'enable_original_uv': True, 'enable_pbr': True, 'ai_model': 'latest'})['result']
        print('%s retexture task %s' % (name, task_id), flush=True)
        task = meshy.wait(key, '/v1/retexture/' + task_id, name + ' retexture')
        # The request's model travels back in the task as a data URI; it is not worth keeping.
        step = {'task': {k: v for k, v in meshy.strip(task).items() if not (isinstance(v, str) and v.startswith('data:'))},
                'group': name, 'paidBy': entry['id'], 'finished_utc': datetime.now(timezone.utc).isoformat()}
        manifest['credits'] += COST['retexture']
        print('  %s retexture cost %s credits' % (name, COST['retexture']), flush=True)
    manifest['steps']['retexture'] = dict(step, credits=COST['retexture'] if step['paidBy'] == entry['id'] else 0)
    meshy.save(manifest, manifest_path)
    for path, other in others.values():
        if 'retexture' not in other['steps']:
            other['steps']['retexture'] = dict(step, credits=0)
            path.parent.mkdir(parents=True, exist_ok=True)
            meshy.save(other, path)
    maps = ('base_color', 'normal', 'metallic', 'roughness')
    if not all((work / ('%s_%s.png' % (name, k))).exists() for k in maps):
        if args.finish_only:
            raise SystemExit('The painted maps of %s are missing: run without --finish-only to fetch them.' % name)
        # Free: the task is read again for fresh signed links.
        task = meshy.call('GET', '/v1/retexture/' + step['task']['id'], meshy.api_key())
        for where, url in meshy.urls(task):
            if where.startswith('texture_urls.') and where.split('.')[-1] in maps:
                meshy.download(url, work / ('%s_%s.png' % (name, where.split('.')[-1])))
            elif where.startswith('model_urls.') and url.split('?', 1)[0].endswith('.glb'):
                meshy.download(url, work / (name + '_painted.glb'))
    import numpy as np
    from PIL import Image, ImageDraw, ImageFilter
    dye = roster['groups'][name].get('cloth')
    for piece in layout:
        u0, width = piece['strip']
        for k in maps:
            image = Image.open(work / ('%s_%s.png' % (name, k)))
            image = image.crop((round(u0 * image.width), 0, round((u0 + width) * image.width), image.height))
            if k == 'base_color' and dye and piece['cloth']:
                # Meshy may paint a banner as a carved plaque or a shield as bare wood; the builder knows where the cloth
                # is, so it takes the group's owner colour here, keeping Meshy's light and dark as detail.
                mask = Image.new('L', image.size, 0)
                for poly in piece['cloth']:
                    ImageDraw.Draw(mask).polygon([(u * image.width, (1 - v) * image.height) for u, v in poly], fill=255)
                inside = np.asarray(mask.filter(ImageFilter.MaxFilter(5))) > 0
                rgb = np.asarray(image.convert('RGB'), np.float32) / 255
                light = rgb[inside] @ np.array([.299, .587, .114], np.float32)
                colour = np.array([int(dye[i:i + 2], 16) for i in (1, 3, 5)], np.float32) / 255
                rgb[inside] = colour * np.clip(light / max(float(light.mean()), 1e-3), .6, 1.3)[:, None]
                image = Image.fromarray((np.clip(rgb, 0, 1) * 255).astype(np.uint8))
            image.save(raw_root / piece['id'] / ('%s_%s.png' % (piece['id'], k)))


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('building')
    parser.add_argument('--roster', default='tools/art/meshy_buildings.json')
    parser.add_argument('--max-credits', type=int, default=20)
    parser.add_argument('--dry-run', action='store_true')
    parser.add_argument('--finish-only', action='store_true')
    args = parser.parse_args()
    roster = json.loads((ROOT / args.roster).read_text(encoding='utf-8'))
    entry = next((b for b in roster['buildings'] if b['id'] == args.building), None)
    if entry is None:
        raise SystemExit('No building %r in %s' % (args.building, args.roster))
    style = roster['styles'][entry['style']]
    out = ROOT / roster['output'] / entry['style'].capitalize() / entry['id']
    out.mkdir(parents=True, exist_ok=True)
    raw = Path(os.path.expandvars(os.path.expanduser(roster['raw']))) / entry['id']
    raw.mkdir(parents=True, exist_ok=True)
    manifest_path = out / 'meshy-manifest.json'
    manifest = json.loads(manifest_path.read_text(encoding='utf-8')) if manifest_path.exists() else {
        'id': entry['id'], 'entry': entry, 'style': style, 'steps': {}, 'files': {}, 'credits': 0}
    if entry.get('base'):
        texture = roster['groups'][entry['base']]['texture']
        print('%s: built here, painted with %s (%d characters of texture prompt), %s' % (
            entry['id'], entry['base'], len(texture), 'resume' if manifest['steps'] else 'new'))
        if args.dry_run:
            print(texture)
            return
        paint(roster, entry, manifest, manifest_path, args)
        prompt = None
    else:
        prompt = entry['prompt'] + ' ' + entry.get('suffix', style['suffix'])
        print('%s: %d characters of prompt, %s' % (entry['id'], len(prompt), 'resume' if manifest['steps'] else 'new'))
        if args.dry_run:
            print(prompt)
            return

    if prompt and not args.finish_only:
        key = meshy.api_key()
        spent = 0

        def run(step, body):
            nonlocal spent
            if spent + COST[step] > args.max_credits:
                raise SystemExit('Stopping before %s: over the cap of %d credits.' % (step, args.max_credits))
            task_id = meshy.call('POST', '/v2/text-to-3d', key, body)['result']
            print('%s %s task %s' % (entry['id'], step, task_id), flush=True)
            task = meshy.wait(key, '/v2/text-to-3d/' + task_id, entry['id'] + ' ' + step)
            # List price, not the balance difference: other batches may be spending at the same time.
            used = COST[step]
            spent += used
            manifest['credits'] += used
            manifest['steps'][step] = {'task': meshy.strip(task), 'credits': used,
                                       'finished_utc': datetime.now(timezone.utc).isoformat()}
            meshy.save(manifest, manifest_path)
            print('  %s %s cost %s credits' % (entry['id'], step, used), flush=True)
            return task

        if 'preview' not in manifest['steps']:
            run('preview', {'mode': 'preview', 'prompt': prompt[:600], 'model_type': 'smart-topology', 'ai_model': 'meshy-t2',
                            'target_polycount': entry.get('triangles', 5000), 'target_formats': ['glb']})
        if 'refine' not in manifest['steps']:
            task = run('refine', {'mode': 'refine', 'preview_task_id': manifest['steps']['preview']['task']['id'],
                                  'enable_pbr': True, 'texture_prompt': entry.get('texture', style['texture'])[:600], 'target_formats': ['glb']})
            for where, url in meshy.urls(task):
                name = where.split('.')[-1]
                if where.startswith('model_urls.') and url.split('?', 1)[0].endswith('.glb'):
                    manifest['files']['model_glb'] = meshy.download(url, raw / (entry['id'] + '_model.glb'))
                elif where.startswith('texture_urls.'):
                    manifest['files']['texture_' + name] = meshy.download(url, raw / ('%s_%s.png' % (entry['id'], name)))
                elif where.startswith('thumbnail'):
                    manifest['files'].setdefault('thumbnail', meshy.download(url, raw / 'thumbnail.png'))
            meshy.save(manifest, manifest_path)

    # A download cut off after the refine was recorded: ask Meshy for the task again (free) for fresh signed links.
    if prompt and not args.finish_only and 'refine' in manifest['steps'] and not all((raw / ('%s_%s.png' % (entry['id'], k))).exists() for k in ('base_color', 'normal', 'metallic', 'roughness')):
        task = meshy.call('GET', '/v2/text-to-3d/' + manifest['steps']['refine']['task']['id'], meshy.api_key())
        for where, url in meshy.urls(task):
            name = where.split('.')[-1]
            if where.startswith('model_urls.') and url.split('?', 1)[0].endswith('.glb'):
                manifest['files']['model_glb'] = meshy.download(url, raw / (entry['id'] + '_model.glb'))
            elif where.startswith('texture_urls.'):
                manifest['files']['texture_' + name] = meshy.download(url, raw / ('%s_%s.png' % (entry['id'], name)))
        meshy.save(manifest, manifest_path)
    # An entry may override its style's owner-colour gate, as a leaf whose iron came out a dull slate blue does.
    coverage = finish.textures({'id': entry['id'], **{k: entry.get(k, style.get(k, default)) for k, default in (
        ('teamHues', []), ('teamSaturation', .28), ('teamWeight', 1))}}, raw, out)
    script = raw / 'finish_building.py'
    script.write_text(BLENDER_SCRIPT, encoding='utf-8')
    blender_args = {'id': entry['id'], 'glb': str(raw / (entry['id'] + '_model.glb')), 'fbx': str(out / (entry['id'] + '.fbx')),
                    'footprint': entry.get('footprint'), 'height': entry['height'], 'fit': entry.get('fit', 'uniform'),
                    'size': entry.get('size'), 'triangles': entry.get('triangles'), 'thin': entry.get('thin', False)}
    result = subprocess.run([finish.BLENDER, '-b', '--factory-startup', '-P', str(script), '--', json.dumps(blender_args)],
                            capture_output=True, text=True, encoding='utf-8', errors='replace')
    line = next((l for l in result.stdout.splitlines() if l.startswith('MESHY_BUILDING ')), None)
    if result.returncode != 0 or line is None:
        sys.stderr.write(result.stdout[-3000:] + result.stderr[-3000:])
        raise SystemExit('Blender failed for %s' % entry['id'])
    report = json.loads(line[len('MESHY_BUILDING '):])
    report['teamMaskCoverage'] = round(coverage, 4)
    if 'refused' in report:
        manifest['finish'] = report
        meshy.save(manifest, manifest_path)
        raise SystemExit('Refused %s: %s. Generate it again with a prompt that fixes its proportions.' % (entry['id'], report['refused']))
    manifest['finish'] = report
    meshy.save(manifest, manifest_path)
    print('MESHY_BUILDING_OK %s %s credits=%s' % (entry['id'], json.dumps(report), manifest['credits']))


if __name__ == '__main__':
    main()
