"""Is a skin figure the one in the user's pictures? One contact sheet per figure, rendered in Blender's workbench. Free.

    python tools/art/review_faithful_sheet.py <id> [<id> ...] [--out D:/EmberfieldWorkingCache/cosmetics-review/faithful]

The top row sets the user's front and back pictures (the cleaned ones the model was made from, roster "multi"; its
"compare" list names them when Meshy was shown only some) beside the
model in its T-pose with the textures the game ships, drawn into the very same frame: the figure's soles on the same
line, its height the same number of pixels, so the two read as one picture to be compared or laid over each other. The
second row is the same model with the first and the second player's colour laid on the cloth the way MeshyUnit.shader
does it, so what the owner colour takes over is plain. Below, the animation sheet of review_unit_sheet.py (idle, walk,
run, attack, aim and climb at the size they have in a match). Run meshy_unit_finish.py first.
"""
import argparse
import json
import os
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import review_unit_sheet as sheet  # noqa: E402
from skin_reference_crops import figure_mask  # noqa: E402

ROOT = sheet.ROOT

BLENDER_ALIGNED = r'''
import bpy, json, sys, math
from mathutils import Vector
args = json.loads(sys.argv[sys.argv.index('--') + 1])
bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene
scene.render.engine = 'BLENDER_WORKBENCH'
scene.render.film_transparent = False
scene.render.image_settings.file_format = 'PNG'
scene.world = bpy.data.worlds.new('w'); scene.world.color = tuple(args['background'])
shading = scene.display.shading
shading.light = args.get('light', 'FLAT'); shading.color_type = 'TEXTURE'; shading.show_cavity = False
shading.show_shadows = False; shading.show_object_outline = False
scene.view_settings.view_transform = 'Standard'
bpy.ops.import_scene.gltf(filepath=args['model'])
mesh = max((o for o in bpy.data.objects if o.type == 'MESH'), key=lambda o: len(o.data.polygons))
camera = bpy.data.objects.new('cam', bpy.data.cameras.new('cam'))
scene.collection.objects.link(camera); scene.camera = camera
camera.data.type = 'ORTHO'; camera.data.clip_end = 100

def material(path):
    made = bpy.data.materials.new('review'); made.use_nodes = True
    image = made.node_tree.nodes.new('ShaderNodeTexImage'); image.image = bpy.data.images.load(path)
    image.image.colorspace_settings.name = 'sRGB'
    made.node_tree.links.new(image.outputs['Color'], made.node_tree.nodes['Principled BSDF'].inputs['Base Color'])
    return made

bounds = [mesh.matrix_world @ Vector(c) for c in mesh.bound_box]
low = Vector((min(p.x for p in bounds), min(p.y for p in bounds), min(p.z for p in bounds)))
high = Vector((max(p.x for p in bounds), max(p.y for p in bounds), max(p.z for p in bounds)))
tall = high.z - low.z
for shot in args['shots']:
    mesh.data.materials.clear(); mesh.data.materials.append(material(shot['texture']))
    width, height = shot['width'], shot['height']
    left, top, right, bottom = shot['figure']
    scene.render.resolution_x, scene.render.resolution_y = width, height
    ppm = (bottom - top) / tall                     # the figure's pixels per metre in the picture it is compared with
    camera.data.ortho_scale = max(width, height) / ppm
    toward = -1 if shot['yaw'] == 0 else 1          # the model faces -Y; yaw 180 looks at its back
    across = Vector((1, 0, 0)) if shot['yaw'] == 0 else Vector((-1, 0, 0))
    centre = Vector(((low.x + high.x) / 2, (low.y + high.y) / 2, low.z + (bottom - height / 2) / ppm))
    centre += across * ((width / 2 - (left + right) / 2) / ppm)
    camera.location = centre + Vector((0, toward * 20, 0))
    camera.rotation_euler = (centre - camera.location).to_track_quat('-Z', 'Y').to_euler()
    scene.render.filepath = args['out'] + '/' + shot['name'] + '.png'
    bpy.ops.render.render(write_still=True)
print('ALIGNED_DONE ' + json.dumps({'height': round(tall, 3)}))
'''


def figure_box(picture):
    import numpy as np
    ys, xs = np.where(figure_mask(np.asarray(picture.convert('RGB'))))
    return int(xs.min()), int(ys.min()), int(xs.max()) + 1, int(ys.max()) + 1


def review(unit, roster, out_dir):
    import subprocess
    from PIL import Image, ImageDraw
    entry = next(e for e in roster['units'] if e['id'] == unit)
    raw = Path(os.path.expandvars(os.path.expanduser(roster['raw']))) / unit
    base = ROOT / roster['output'] / unit
    work = out_dir / ('_work_' + unit + '_faithful')
    work.mkdir(parents=True, exist_ok=True)
    pictures = [Image.open(Path(os.path.expandvars(os.path.expanduser(p)))).convert('RGB') for p in entry['multi'].get('compare') or entry['multi']['images']]
    boxes = [figure_box(p) for p in pictures]
    textures = {'plain': work / 'plain.png'}
    Image.open(base / (unit + '_BaseColor.png')).convert('RGB').save(textures['plain'])
    for owner in (1, 2):
        textures['owner%d' % owner] = sheet.team_texture(base / (unit + '_BaseColor.png'), owner, work / ('owner%d.png' % owner))
    shots = []
    for side, (picture, box, yaw) in zip(('front', 'back'), zip(pictures, boxes, (0, 180))):
        for kind, texture in textures.items():
            shots.append({'name': '%s_%s' % (side, kind), 'yaw': yaw, 'texture': str(texture), 'width': picture.width,
                          'height': picture.height, 'figure': box})
    background = [c / 255 for c in pictures[0].getpixel((4, 4))]
    script = work / 'aligned_blender.py'
    script.write_text(BLENDER_ALIGNED, encoding='utf-8')
    arguments = {'model': str(raw / (unit + '_rigged_character.glb')), 'out': str(work), 'shots': shots, 'background': background}
    result = subprocess.run([sheet.BLENDER, '-b', '--factory-startup', '-P', str(script), '--', json.dumps(arguments)],
                            capture_output=True, text=True, encoding='utf-8', errors='replace')
    if result.returncode != 0 or 'ALIGNED_DONE' not in result.stdout:
        sys.stderr.write(result.stdout[-3000:] + result.stderr[-3000:])
        raise SystemExit('Blender failed')
    cell = 600

    def fit(picture):
        scale = cell / picture.height
        return picture.convert('RGB').resize((int(picture.width * scale), cell), Image.LANCZOS)

    def row(images, labels):
        shown = [fit(i) for i in images]
        out = Image.new('RGB', (sum(s.width for s in shown), cell + 24), (20, 20, 24))
        draw = ImageDraw.Draw(out)
        left = 0
        for s, label in zip(shown, labels):
            out.paste(s, (left, 24))
            draw.text((left + 8, 6), label, fill=(235, 235, 235))
            left += s.width
        return out

    first = row([pictures[0], Image.open(work / 'front_plain.png'), pictures[1], Image.open(work / 'back_plain.png')],
                ['the user\'s picture, front', 'model, front', 'the user\'s picture, back', 'model, back'])
    second = row([Image.open(work / ('%s_owner%d.png' % (side, owner))) for owner in (1, 2) for side in ('front', 'back')],
                 ['player 1, front', 'player 1, back', 'player 2, front', 'player 2, back'])
    animations = Image.open(sheet.review('final', unit, roster, out_dir)).convert('RGB')
    width = max(first.width, second.width, animations.width)
    result_image = Image.new('RGB', (width, first.height + second.height + animations.height), (20, 20, 24))
    y = 0
    for part in (first, second, animations):
        result_image.paste(part, ((width - part.width) // 2, y))
        y += part.height
    target = out_dir / (unit + '-faithful.png')
    result_image.save(target)
    return target


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('units', nargs='+')
    parser.add_argument('--roster', default='tools/art/meshy_units.json')
    parser.add_argument('--out', default='D:/EmberfieldWorkingCache/cosmetics-review/faithful')
    args = parser.parse_args()
    roster = json.loads((ROOT / args.roster).read_text(encoding='utf-8'))
    out_dir = Path(args.out)
    out_dir.mkdir(parents=True, exist_ok=True)
    for unit in args.units:
        print(review(unit, roster, out_dir))


if __name__ == '__main__':
    main()
