"""Turn one figure from a reference sheet into a rigged, animated Meshy unit.

    python tools/art/meshy_unit.py <unit-id> [--roster tools/art/meshy_units.json] [--max-credits 60] [--dry-run]

Three paid steps, each recorded before the next starts, so an interrupted run resumes where it stopped instead of
paying again:

1. image-to-3d  -- Smart Topology (meshy-t2), T-pose, PBR textures, about 4,000 triangles.     15 credits
2. rigging      -- Meshy's biped auto-rig. Walking and running clips come back with it.         5 credits
3. animations   -- the role's library clips merged into one file (idle, attack, hit, death...). 3 credits each

The roster names the sheet, the crop box around one figure, the unit's height and its clips. The crop is saved beside
the model as reference.png, so what Meshy was shown stays with what it made.

The key is read ONLY from MESHY_API_KEY (process first, then the Windows user environment) and is never printed or
written to disk. Signed download URLs are stored without their query string.
"""
import argparse
import base64
import hashlib
import io
import json
import os
import sys
import time
import urllib.error
import urllib.request
from datetime import datetime, timezone
from pathlib import Path

API = 'https://api.meshy.ai/openapi'
ROOT = Path(__file__).resolve().parents[2]

# Library ids (GET /v1/animations/library). Walk and run come free with the rig; these fill the other states.
ROLE_CLIPS = {
    'melee': [('Idle', 0), ('Attack', 4), ('Hit', 178), ('Death', 8)],
    'heavy': [('Idle', 0), ('Attack', 128), ('Hit', 178), ('Death', 8)],
    'spear': [('Idle', 0), ('Attack', 240), ('Hit', 178), ('Death', 8)],
    'ranged': [('Idle', 0), ('Attack', 224), ('Aim', 231), ('Hit', 150), ('Death', 184)],
    'caster': [('Idle', 0), ('Attack', 125), ('Hit', 178), ('Death', 8)],
    'worker': [('Idle', 0), ('Work', 237), ('Attack', 4), ('Hit', 178), ('Death', 8)],
}
COST = {'model': 15, 'rig': 5, 'clip': 3}


def api_key():
    key = os.environ.get('MESHY_API_KEY')
    if not key and sys.platform == 'win32':
        try:
            import winreg
            with winreg.OpenKey(winreg.HKEY_CURRENT_USER, 'Environment') as handle:
                key = winreg.QueryValueEx(handle, 'MESHY_API_KEY')[0]
        except OSError:
            key = None
    if not key:
        raise SystemExit('MESHY_API_KEY is not set. Set it as a user environment variable; never pass it on the command line.')
    return key


def call(method, path, key, body=None):
    data = json.dumps(body).encode() if body is not None else None
    request = urllib.request.Request(API + path, data=data, method=method, headers={
        'Authorization': 'Bearer ' + key, 'Content-Type': 'application/json'})
    try:
        with urllib.request.urlopen(request, timeout=120) as response:
            return json.loads(response.read().decode() or '{}')
    except urllib.error.HTTPError as error:
        # The body explains the refusal; the request headers, which carry the key, are never echoed.
        raise RuntimeError('%s %s -> HTTP %d: %s' % (method, path, error.code, error.read().decode()[:600])) from None


def balance(key):
    return call('GET', '/v1/balance', key).get('balance')


def wait(key, path, label):
    last = None
    started = time.time()
    while True:
        task = call('GET', path, key)
        status, progress = task.get('status'), task.get('progress')
        if (status, progress) != last:
            print('  %s: %s %s%% (%ds)' % (label, status, progress, time.time() - started), flush=True)
            last = (status, progress)
        if status == 'SUCCEEDED':
            return task
        if status in ('FAILED', 'CANCELED', 'EXPIRED'):
            raise RuntimeError('%s %s: %s' % (label, status, json.dumps(task.get('task_error'))))
        if time.time() - started > 2400:
            raise RuntimeError('%s did not finish within 40 minutes.' % label)
        time.sleep(8)


def urls(node, prefix=''):
    """Every download link in a task, keyed by where it sits. The schema differs between endpoints and is only
    partly documented, so the links are found rather than named."""
    if isinstance(node, dict):
        for name, value in node.items():
            yield from urls(value, prefix + name + '.')
    elif isinstance(node, list):
        for index, value in enumerate(node):
            yield from urls(value, prefix + str(index) + '.')
    elif isinstance(node, str) and node.startswith('https://'):
        yield prefix.rstrip('.'), node


def strip(node):
    if isinstance(node, dict):
        return {k: strip(v) for k, v in node.items()}
    if isinstance(node, list):
        return [strip(v) for v in node]
    if isinstance(node, str) and node.startswith('https://'):
        return node.split('?', 1)[0]
    return node


def download(url, target):
    with urllib.request.urlopen(url, timeout=600) as response, open(target, 'wb') as handle:
        handle.write(response.read())
    return {'file': target.name, 'bytes': target.stat().st_size,
            'sha256': hashlib.sha256(target.read_bytes()).hexdigest()}


def crop(entry, out):
    import numpy as np
    from PIL import Image
    from scipy import ndimage
    sheet = Path(os.path.expandvars(os.path.expanduser(entry['sheet'])))
    if not sheet.is_absolute():
        sheet = ROOT / sheet
    image = Image.open(sheet).convert('RGB')
    box = entry.get('box')
    pixels = np.asarray(image.crop(tuple(box)) if box else image).astype(np.int16)
    # The sheets sit figures shoulder to shoulder, so a box also catches a neighbour's fingertips or spear tip. Those
    # arrive as small shapes cut by the left or right edge; paint them out with the backdrop. The figure itself is
    # left untouched: dark boots and armour are barely lighter than the backdrop and would not survive a stricter cut.
    backdrop = np.median(np.concatenate([pixels[:8].reshape(-1, 3), pixels[-8:].reshape(-1, 3)]), axis=0)
    solid = np.abs(pixels - backdrop).max(axis=2) > 16
    labels, count = ndimage.label(solid)
    if count:
        sizes = ndimage.sum(solid, labels, range(1, count + 1))
        edge = set(np.unique(labels[:, :6])) | set(np.unique(labels[:, -6:]))
        for label in edge - {0}:
            if sizes[label - 1] < sizes.max() * .03:
                pixels[ndimage.binary_dilation(labels == label, iterations=3)] = backdrop
    figure = Image.fromarray(pixels.clip(0, 255).astype(np.uint8))
    # Meshy reads a single object best when it fills the frame with a little margin around it.
    pad = int(max(figure.size) * .06)
    canvas = Image.new('RGB', (figure.width + pad * 2, figure.height + pad * 2), tuple(int(c) for c in backdrop))
    canvas.paste(figure, (pad, pad))
    target = out / 'reference.png'
    canvas.save(target)
    return target


def save(manifest, path):
    path.write_text(json.dumps(manifest, indent=2, ensure_ascii=False) + '\n', encoding='utf-8')


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('unit')
    parser.add_argument('--roster', default='tools/art/meshy_units.json')
    parser.add_argument('--max-credits', type=int, default=60, help='Refuse any step that would take this run past it.')
    parser.add_argument('--dry-run', action='store_true', help='Crop and show the plan without spending anything.')
    parser.add_argument('--stop-after', choices=['model', 'rig', 'animations'], default='animations')
    args = parser.parse_args()

    roster = json.loads((ROOT / args.roster).read_text(encoding='utf-8'))
    entry = next((e for e in roster['units'] if e['id'] == args.unit), None)
    if entry is None:
        raise SystemExit('No unit %r in %s' % (args.unit, args.roster))
    out = ROOT / roster['output'] / entry['id']
    out.mkdir(parents=True, exist_ok=True)
    # Meshy's downloads (a textured copy of the mesh in every file, 25 MB a unit) stay outside the repository;
    # meshy_unit_finish.py turns them into the light FBX and 1024 textures the game ships.
    raw = Path(os.path.expandvars(os.path.expanduser(roster['raw']))) / entry['id']
    raw.mkdir(parents=True, exist_ok=True)
    manifest_path = out / 'meshy-manifest.json'
    manifest = json.loads(manifest_path.read_text(encoding='utf-8')) if manifest_path.exists() else {
        'id': entry['id'], 'entry': entry, 'steps': {}, 'files': {}, 'credits': 0}
    clips = ROLE_CLIPS[entry['role']]
    planned = (0 if 'model' in manifest['steps'] else COST['model']) + (0 if 'rig' in manifest['steps'] else COST['rig']) + \
        (0 if 'animations' in manifest['steps'] else COST['clip'] * len(clips))
    reference = out / 'reference.png'
    if not reference.exists():
        reference = crop(entry, out)
    print('%s: %s, clips %s, still to spend about %d credits (cap %d)'
          % (entry['id'], entry['role'], ', '.join(name for name, _ in clips), planned, args.max_credits))
    if args.dry_run:
        return
    key = api_key()
    spent = 0

    def charge(step, estimate):
        nonlocal spent
        if spent + estimate > args.max_credits:
            raise SystemExit('Stopping before %s: it would take this run to %d credits, over the cap of %d.'
                             % (step, spent + estimate, args.max_credits))

    def record(step, task, before, price):
        nonlocal spent
        after = balance(key)
        # List price, not the balance difference: other batches may be spending at the same time.
        used = price
        spent += used
        manifest['credits'] += used or 0
        manifest['steps'][step] = {'task': strip(task), 'credits': used, 'balance_after': after,
                                   'finished_utc': datetime.now(timezone.utc).isoformat()}
        save(manifest, manifest_path)
        print('  %s cost %s credits; balance %s' % (step, used, after), flush=True)

    if 'model' not in manifest['steps']:
        charge('model', COST['model'])
        data = base64.b64encode(reference.read_bytes()).decode()
        body = {'image_url': 'data:image/png;base64,' + data, 'model_type': 'smart-topology', 'ai_model': 'meshy-t2',
                'target_polycount': entry.get('polycount', 4000), 'pose_mode': 't-pose', 'should_texture': True,
                'enable_pbr': True, 'target_formats': ['glb', 'fbx']}
        if entry.get('texture_prompt'):
            body['texture_prompt'] = entry['texture_prompt']
        before = balance(key)
        task_id = call('POST', '/v1/image-to-3d', key, body)['result']
        print('Model task %s' % task_id, flush=True)
        task = wait(key, '/v1/image-to-3d/' + task_id, 'model')
        for where, url in urls(task):
            name = url.split('?', 1)[0].rsplit('/', 1)[-1]
            if where.startswith('model_urls.') and name.endswith('.glb'):
                manifest['files']['model_glb'] = download(url, raw / (entry['id'] + '_model.glb'))
            elif where.startswith('texture_urls.'):
                kind = where.split('.')[-1]
                manifest['files']['texture_' + kind] = download(url, raw / ('%s_%s.png' % (entry['id'], kind)))
            elif where.startswith('thumbnail'):
                manifest['files'].setdefault('thumbnail', download(url, raw / 'thumbnail.png'))
        record('model', task, before, COST['model'])
    if args.stop_after == 'model':
        return

    if 'rig' not in manifest['steps']:
        charge('rig', COST['rig'])
        before = balance(key)
        task_id = call('POST', '/v1/rigging', key, {'input_task_id': manifest['steps']['model']['task']['id'],
                                                   'height_meters': entry.get('height', 1.75)})['result']
        print('Rig task %s' % task_id, flush=True)
        task = wait(key, '/v1/rigging/' + task_id, 'rig')
        for where, url in urls(task):
            name = where.split('.')[-1]
            if name.endswith('_fbx_url') or name.endswith('_glb_url'):
                ext = 'fbx' if name.endswith('_fbx_url') else 'glb'
                label = name[:-len('_fbx_url')]
                manifest['files'][label + '_' + ext] = download(url, raw / ('%s_%s.%s' % (entry['id'], label, ext)))
        record('rig', task, before, COST['rig'])
    if args.stop_after == 'rig':
        return

    if 'animations' not in manifest['steps']:
        charge('animations', COST['clip'] * len(clips))
        before = balance(key)
        task_id = call('POST', '/v1/animations', key, {'rig_task_id': manifest['steps']['rig']['task']['id'],
                                                      'action_ids': [clip for _, clip in clips]})['result']
        print('Animation task %s' % task_id, flush=True)
        task = wait(key, '/v1/animations/' + task_id, 'animations')
        for where, url in urls(task):
            name = where.split('.')[-1]
            if name.endswith('_fbx_url') or name.endswith('_glb_url'):
                ext = 'fbx' if name.endswith('_fbx_url') else 'glb'
                label = name[:-len('_fbx_url')]
                manifest['files'][label + '_' + ext] = download(url, raw / ('%s_%s.%s' % (entry['id'], label, ext)))
        manifest['clips'] = [{'state': state, 'action_id': clip} for state, clip in clips]
        record('animations', task, before, COST['clip'] * len(clips))
    print('MESHY_UNIT_OK %s credits=%s' % (entry['id'], manifest['credits']))


if __name__ == '__main__':
    main()
