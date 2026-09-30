"""Caption and encode the frames ArmyFilm writes. Free, local.

    python tools/art/encode_film.py <out.mp4> <film-folder> [<film-folder> ...] [--fps 40]

Each folder holds frames/frame-NNNNNN.jpg and frames/frames.csv (index,tick,alpha,mode,caption). The caption of each
frame is drawn into a band along the bottom (once: a marker file records it), then Blender's sequencer cuts every
folder in order into one H.264 video. There is no ffmpeg on this machine; Blender carries its own.
"""
import argparse
import csv
import json
import os
import subprocess
import sys
from pathlib import Path

BLENDER = os.environ.get('BLENDER', r'D:\CodexTooling\blender-portable\blender-4.5.13-windows-x64\blender.exe')

ENCODER = r'''
import bpy, json, sys
args = json.load(open(sys.argv[sys.argv.index('--') + 1], encoding='utf-8'))
scene = bpy.data.scenes.new('film')
r = scene.render; r.fps = args['fps']; r.fps_base = 1
r.resolution_x = args['width']; r.resolution_y = args['height']; r.resolution_percentage = 100
scene.view_settings.view_transform = 'Standard'; scene.view_settings.look = 'None'
editor = scene.sequence_editor_create(); strips = getattr(editor, 'strips', None) or editor.sequences
start = 1
for folder, files in args['folders']:
    strip = strips.new_image(name=folder[-40:], filepath=folder + '/' + files[0], channel=1, frame_start=start, fit_method='FIT')
    for f in files[1:]: strip.elements.append(f)
    start += len(files)
scene.frame_start = 1; scene.frame_end = start - 1
r.use_sequencer = True; r.image_settings.file_format = 'FFMPEG'; ff = r.ffmpeg
ff.format = 'MPEG4'; ff.codec = 'H264'; ff.constant_rate_factor = 'HIGH'; ff.ffmpeg_preset = 'GOOD'; ff.gopsize = args['fps'] * 2; ff.audio_codec = 'NONE'
r.filepath = args['out']
with bpy.context.temp_override(scene=scene): bpy.ops.render.render(animation=True, scene=scene.name)
print('ENCODED', args['out'], start - 1)
'''


def caption(folder):
    from PIL import Image, ImageDraw, ImageFont
    frames = Path(folder) / 'frames'
    marker = frames / 'captioned.txt'
    rows = list(csv.DictReader(open(frames / 'frames.csv', encoding='utf-8')))
    if marker.exists():
        return rows
    try:
        font = ImageFont.truetype('C:/Windows/Fonts/segoeuib.ttf', 34)
    except OSError:
        font = ImageFont.load_default()
    for row in rows:
        path = frames / ('frame-%06d.jpg' % int(row['index']))
        image = Image.open(path).convert('RGB')
        draw = ImageDraw.Draw(image, 'RGBA')
        w, h = image.size
        draw.rectangle([0, h - 70, w, h], fill=(18, 20, 24, 170))
        draw.text((32, h - 58), row['caption'], fill=(240, 232, 210), font=font)
        image.save(path, quality=90)
    marker.write_text('captioned\n', encoding='utf-8')
    return rows


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('out')
    parser.add_argument('folders', nargs='+')
    parser.add_argument('--fps', type=int, default=40)
    args = parser.parse_args()
    from PIL import Image
    folders = []
    for folder in args.folders:
        rows = caption(folder)
        frames = (Path(folder) / 'frames').resolve()
        folders.append([str(frames).replace('\\', '/'), ['frame-%06d.jpg' % int(r['index']) for r in rows]])
    width, height = Image.open(Path(folders[0][0]) / folders[0][1][0]).size
    script = Path(os.environ.get('TEMP', '.')) / 'encode_film_blender.py'
    script.write_text(ENCODER, encoding='utf-8')
    payload = {'folders': folders, 'fps': args.fps, 'width': width, 'height': height, 'out': str(Path(args.out).resolve()).replace('\\', '/')}
    # Thousands of frame names overflow Windows' command line, so the list travels in a file.
    listing = script.with_suffix('.json')
    listing.write_text(json.dumps(payload), encoding='utf-8')
    result = subprocess.run([BLENDER, '-b', '--factory-startup', '-P', str(script), '--', str(listing)],
                            capture_output=True, text=True, encoding='utf-8', errors='replace')
    line = next((l for l in result.stdout.splitlines() if l.startswith('ENCODED')), None)
    if result.returncode != 0 or line is None:
        sys.stderr.write(result.stdout[-3000:] + result.stderr[-2000:])
        raise SystemExit('Blender failed to encode')
    print(line)


if __name__ == '__main__':
    main()
