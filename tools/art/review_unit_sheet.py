"""Contact sheets of a Meshy unit, rendered in Blender's workbench: free, headless, no Unity.

    python tools/art/review_unit_sheet.py model <id> [<id> ...]   # the raw image-to-3D model, T-pose, before the rig is paid for
    python tools/art/review_unit_sheet.py final <id> [<id> ...]   # the shipped FBX and textures, in the game's clips, front and back

``model`` reads <raw>/<id>/<id>_model.glb and writes <out>/<id>-model.png: front, three-quarter, side and back.
``final`` reads Assets/Models/Units/<id>/<id>.fbx with its BaseColor map and writes <out>/<id>-sheet.png: one column per
clip (Idle, Walk, Run, Attack, plus Aim, Work or Climb where the unit has them) and a last column of the idle in the
second player's colour, with the front on the top row and the back below. The owner's colour is laid on the cloth the
way MeshyUnit.shader does it (the painted brightness kept, the hue replaced where the mask is set), so a missing or
leaking team-colour band shows up here, before the unit is baked.

Each picture is drawn at the size the figure has on a 1080p screen at the play zoom, doubled, so an animation that reads
badly in the sheet will read badly in the match. The default folder is D:/EmberfieldWorkingCache/cosmetics-review.
"""
import argparse
import json
import os
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
BLENDER = os.environ.get('BLENDER', r'D:\CodexTooling\blender-portable\blender-4.5.13-windows-x64\blender.exe')
# AlphaWorldArt.OwnerColor: player 1 teal, player 2 red.
OWNERS = {1: (.12, .56, .56), 2: (.83, .25, .17)}
STATES = ['Idle', 'Walk', 'Run', 'Attack', 'Aim', 'Work', 'Climb']
# Where in each clip the picture is taken (share of its length): the walk and run mid-stride, the attack at the strike.
MOMENT = {'Idle': 0.0, 'Walk': 0.2, 'Run': 0.2, 'Attack': 0.45, 'Aim': 0.5, 'Work': 0.4, 'Climb': 0.3}

BLENDER_SCRIPT = r'''
import bpy, json, sys, math
from mathutils import Vector
args = json.loads(sys.argv[sys.argv.index('--') + 1])
bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene
scene.render.engine = 'BLENDER_WORKBENCH'
scene.render.film_transparent = True
scene.render.resolution_x, scene.render.resolution_y = args['size']
scene.render.image_settings.file_format = 'PNG'
scene.render.image_settings.color_mode = 'RGBA'
shading = scene.display.shading
shading.light = args.get('light', 'STUDIO'); shading.color_type = 'TEXTURE'; shading.show_cavity = False
shading.show_shadows = False; shading.show_object_outline = False
scene.view_settings.view_transform = 'Standard'

before = set(bpy.data.objects)
if args['model'].lower().endswith('.glb'):
    bpy.ops.import_scene.gltf(filepath=args['model'])
else:
    bpy.ops.import_scene.fbx(filepath=args['model'])
new = [o for o in bpy.data.objects if o not in before]
arm = next((o for o in new if o.type == 'ARMATURE'), None)
meshes = [o for o in new if o.type == 'MESH']
mesh = max(meshes, key=lambda o: len(o.data.polygons))
for o in meshes:
    if o is not mesh: bpy.data.objects.remove(o, do_unlink=True)

def texture_material(path):
    material = bpy.data.materials.new('review'); material.use_nodes = True
    nodes = material.node_tree.nodes
    image = nodes.new('ShaderNodeTexImage'); image.image = bpy.data.images.load(path); image.image.colorspace_settings.name = 'sRGB'
    principled = nodes['Principled BSDF']
    material.node_tree.links.new(image.outputs['Color'], principled.inputs['Base Color'])
    return material

def apply_texture(path):
    material = texture_material(path)
    mesh.data.materials.clear()
    mesh.data.materials.append(material)

if args.get('textures'):
    apply_texture(args['textures'][0])

camera = bpy.data.objects.new('cam', bpy.data.cameras.new('cam'))
scene.collection.objects.link(camera); scene.camera = camera
camera.data.type = 'ORTHO'; camera.data.clip_end = 100

def frame_of(action, share):
    start, end = action.frame_range
    return start + (end - start) * share

def find_action(state):
    for action in bpy.data.actions:
        if action.name == state or action.name.endswith('|' + state): return action
    return None

def bounds():
    deps = bpy.context.evaluated_depsgraph_get()
    obj = mesh.evaluated_get(deps)
    points = [obj.matrix_world @ Vector(c) for c in obj.bound_box]
    return (Vector((min(p.x for p in points), min(p.y for p in points), min(p.z for p in points))),
            Vector((max(p.x for p in points), max(p.y for p in points), max(p.z for p in points))))

def pose(state):
    action = find_action(state) if arm is not None else None
    if action is None: return False
    arm.animation_data_create(); arm.animation_data.action = action
    if len(action.slots): arm.animation_data.action_slot = action.slots[0]
    scene.frame_set(int(round(frame_of(action, args['moments'].get(state, 0)))))
    return True

# One camera for every picture of a unit, so the clips and the views compare at the same scale.
if arm is not None: pose('Idle')
low, high = bounds()
height = high.z - low.z
width = max(high.x - low.x, high.y - low.y)
middle = Vector(((low.x + high.x) / 2, (low.y + high.y) / 2, (low.z + high.z) / 2))
if args.get('fit') == 'tpose':
    camera.data.ortho_scale = max(height, width) * 1.08
else:
    camera.data.ortho_scale = max(height * 1.12, width * .9)
    middle.z = low.z + height * .5 + height * .02
distance = 20

def shoot(name, yaw):
    # yaw 0 looks at the front (the model faces -Y); 180 at the back.
    rad = math.radians(yaw)
    camera.location = middle + Vector((math.sin(rad) * distance, -math.cos(rad) * distance, 0))
    direction = middle - camera.location
    camera.rotation_euler = direction.to_track_quat('-Z', 'Y').to_euler()
    scene.render.filepath = args['out'] + '/' + name + '.png'
    bpy.ops.render.render(write_still=True)

done = []
for shot in args['shots']:
    if shot.get('texture'): apply_texture(shot['texture'])
    if shot.get('state') and not pose(shot['state']): continue
    shoot(shot['name'], shot['yaw'])
    done.append(shot['name'])
print('REVIEW_DONE ' + json.dumps({'shots': done, 'height': round(height, 3), 'width': round(width, 3)}))
'''


def team_texture(base_color, owner, target):
    """BaseColor with the owner's colour on the cloth the mask marks, as MeshyUnit.shader lays it."""
    import numpy as np
    from PIL import Image
    image = np.asarray(Image.open(base_color).convert('RGBA')).astype(np.float32) / 255
    rgb, alpha = image[..., :3], image[..., 3:4]
    luminance = rgb @ np.array([.2126, .7152, .0722], np.float32)
    tint = np.array(OWNERS[owner], np.float32) * np.clip(.35 + luminance * 1.9, 0, 1)[..., None]
    out = rgb * (1 - alpha) + tint * alpha
    Image.fromarray((np.clip(out, 0, 1) * 255).astype(np.uint8)).save(target)
    return target


def run_blender(args, work):
    script = work / 'review_blender.py'
    script.write_text(BLENDER_SCRIPT, encoding='utf-8')
    result = subprocess.run([BLENDER, '-b', '--factory-startup', '-P', str(script), '--', json.dumps(args)],
                            capture_output=True, text=True, encoding='utf-8', errors='replace')
    line = next((l for l in result.stdout.splitlines() if l.startswith('REVIEW_DONE')), None)
    if result.returncode != 0 or line is None:
        sys.stderr.write(result.stdout[-3000:] + result.stderr[-3000:])
        raise SystemExit('Blender failed')
    return json.loads(line[len('REVIEW_DONE '):])


def sheet(columns, rows, work, target, labels, size, background=(26, 26, 30)):
    from PIL import Image, ImageDraw
    cell_w, cell_h = size
    out = Image.new('RGB', (cell_w * len(columns), cell_h * len(rows) + 22), background)
    draw = ImageDraw.Draw(out)
    for c, column in enumerate(columns):
        draw.text((c * cell_w + 6, 4), labels[c], fill=(235, 235, 235))
        for r, row in enumerate(rows):
            path = work / ('%s_%s.png' % (column, row))
            if path.exists():
                picture = Image.open(path).convert('RGBA')
                out.paste(picture, (c * cell_w, 22 + r * cell_h), picture)
    out.save(target)
    return target


def review(mode, unit, roster, out_dir):
    raw = Path(os.path.expandvars(os.path.expanduser(roster['raw']))) / unit
    work = out_dir / ('_work_' + unit + '_' + mode)
    work.mkdir(parents=True, exist_ok=True)
    for old in work.glob('*.png'):
        old.unlink()
    entry = next((e for e in roster['units'] if e['id'] == unit), {})
    if mode == 'model':
        model = raw / (unit + '_model.glb')
        size = [360, 480]
        shots = [{'name': 'front_a', 'yaw': 0}, {'name': 'threeq_a', 'yaw': 40}, {'name': 'side_a', 'yaw': 90}, {'name': 'back_a', 'yaw': 180}]
        run_blender({'model': str(model), 'out': str(work), 'size': size, 'shots': shots, 'moments': MOMENT, 'fit': 'tpose'}, work)
        return sheet(['front', 'threeq', 'side', 'back'], ['a'], work, out_dir / (unit + '-model.png'),
                     ['front', 'three-quarter', 'side', 'back'], size)
    base = ROOT / roster['output'] / unit
    fbx = base / (unit + '.fbx')
    textures = [team_texture(base / (unit + '_BaseColor.png'), owner, work / ('owner%d.png' % owner)) for owner in (1, 2)]
    # The unit's own 1080p play-zoom height (8 m ortho half-height: 135 px for 2 m) doubled for reading.
    height_px = 300
    size = [int(height_px * .8), height_px]
    manifest = json.loads((base / 'meshy-manifest.json').read_text(encoding='utf-8'))
    have = [s for s in STATES if s in manifest.get('finish', {}).get('states', [])]
    shots = []
    for state in have:
        for view, yaw in (('front', 0), ('back', 180)):
            shots.append({'name': '%s_%s' % (state, view), 'yaw': yaw, 'state': state, 'texture': str(textures[0])})
    for view, yaw in (('front', 0), ('back', 180)):
        shots.append({'name': 'Owner2_%s' % view, 'yaw': yaw, 'state': 'Idle', 'texture': str(textures[1])})
    run_blender({'model': str(fbx), 'out': str(work), 'size': size, 'shots': shots, 'moments': MOMENT, 'textures': [str(textures[0])]}, work)
    columns = have + ['Owner2']
    labels = [c + (' (player 2)' if c == 'Owner2' else '') for c in columns]
    return sheet(columns, ['front', 'back'], work, out_dir / (unit + '-sheet.png'), labels, size)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('mode', choices=['model', 'final'])
    parser.add_argument('units', nargs='+')
    parser.add_argument('--roster', default='tools/art/meshy_units.json')
    parser.add_argument('--out', default='D:/EmberfieldWorkingCache/cosmetics-review')
    args = parser.parse_args()
    roster = json.loads((ROOT / args.roster).read_text(encoding='utf-8'))
    out_dir = Path(args.out)
    out_dir.mkdir(parents=True, exist_ok=True)
    for unit in args.units:
        print(review(args.mode, unit, roster, out_dir))


if __name__ == '__main__':
    main()
