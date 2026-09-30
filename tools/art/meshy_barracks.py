"""Generate the Emberfield barracks (muster_hall) with the Meshy text-to-3D API.

    python tools/art/meshy_barracks.py [--out Assets/Models/Buildings/Barracks] [--standard]

Two paid steps: a geometry preview, then a refine that paints PBR textures onto it. The key is read ONLY from the
MESHY_API_KEY environment variable (process first, then the Windows user environment) and is never printed or
written to disk. Everything else -- task ids, prompts, the credits each step cost, the files and their hashes -- goes
into meshy-manifest.json beside the model, so the result can be traced back to exactly what produced it.

The prompt is written against the live muster_hall: its real footprint and heights, and its palette converted from
the project's linear vertex colours to the sRGB a texture needs. See docs/art/meshy-barracks.md.
"""
import argparse
import hashlib
import json
import os
import sys
import time
import urllib.error
import urllib.request
from datetime import datetime, timezone
from pathlib import Path

API = 'https://api.meshy.ai/openapi'

PROMPT = (
    "Low-poly stylized medieval barracks for an isometric RTS game, single building on a square pale grey stone "
    "plinth. Wide one-storey hall: pale cream plaster walls, dark brown timber corner posts and eave beam, "
    "low-pitched gable roof of grey-teal slate with thin batten strips and a gold ridge cap, gable end facing the "
    "entrance. Off-white cloth band draped across the middle of the roof. Open forecourt in front: two square pale "
    "stone pillars with small gold pyramid caps, joined by a thick timber crossbeam with a gold plaque; two plain "
    "off-white banners hang beside the pillars; spear racks against the front wall. Dark doorway, two small windows. "
    "Chunky simple shapes, flat-shaded, flat colour blocks, game-ready. No text, no terrain, no characters."
)

TEXTURE_PROMPT = (
    "Stylized matte flat colours, even neutral lighting, no baked shadows or ambient occlusion. Walls: pale cream "
    "plaster (#EDE7D0), very faint variation. Roof: muted grey-teal slate (#7E9597), batten strips slightly lighter "
    "(#879FA1). Timber: warm mid-brown wood (#8E765F), faint grain. Plinth, pillars and sills: pale grey-beige stone "
    "(#D4D4C4), large simple blocks. Ridge cap, pillar caps, plaque and small trims: warm gold (#F9D389), the only "
    "metal. Door and window openings: very dark blue-grey (#4D5F61). Banners and roof band: plain off-white cloth "
    "(#F9F5E3), no pattern or emblem. No dirt, moss, cracks, rust, text or logos."
)


# Second attempt. The first prompt listed every ornament of the procedural hall -- banners, a cloth band, pillars,
# a plaque -- and Meshy built an open pavilion on stilts around them. This one leads with the mass and silhouette,
# says "enclosed" and "wider than tall" outright, and names almost no decoration.
PROMPT_ENCLOSED = (
    "Low-poly game asset: a medieval military barracks for a top-down strategy game. One wide, low, fully enclosed "
    "rectangular hall, clearly wider than it is tall, with solid walls on all four sides. Pale cream plaster walls "
    "framed with dark timber beams, a thick grey stone base around the bottom. A shallow gable roof of grey-teal "
    "slate covering the whole hall, with straight edges and no curves. One wooden double door in the middle of the "
    "long front wall and two small square windows either side. A spear rack leaning beside the door. Simple chunky "
    "geometry, hard edges, flat faceted surfaces, few large polygons. Isolated building, no ground, no text, no "
    "characters, no fabric."
)

TEXTURE_PROMPT_ENCLOSED = (
    "Stylized matte flat colours, even neutral lighting, no baked shadows or ambient occlusion. Walls: pale cream "
    "plaster (#EDE7D0), very faint variation. Timber beams and door: warm mid-brown wood (#8E765F), faint grain. "
    "Stone base: pale grey-beige stone (#D4D4C4), large simple blocks. Roof: muted grey-teal slate (#7E9597), flat "
    "and uniform. Windows: very dark blue-grey (#4D5F61). Small iron fittings only. No dirt, moss, cracks, rust, "
    "text, logos, cloth or banners."
)

VARIANTS = {'first': (PROMPT, TEXTURE_PROMPT), 'enclosed': (PROMPT_ENCLOSED, TEXTURE_PROMPT_ENCLOSED)}


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
        with urllib.request.urlopen(request, timeout=60) as response:
            return json.loads(response.read().decode() or '{}')
    except urllib.error.HTTPError as error:
        # The body explains the refusal; the request headers, which carry the key, are never echoed.
        raise RuntimeError('%s %s -> HTTP %d: %s' % (method, path, error.code, error.read().decode()[:400])) from None


def balance(key):
    return call('GET', '/v1/balance', key).get('balance')


def wait(key, task_id, label):
    last = None
    started = time.time()
    while True:
        task = call('GET', '/v2/text-to-3d/' + task_id, key)
        status, progress = task.get('status'), task.get('progress')
        if (status, progress) != last:
            print('  %s: %s %s%% (%ds)' % (label, status, progress, time.time() - started), flush=True)
            last = (status, progress)
        if status == 'SUCCEEDED':
            return task
        if status in ('FAILED', 'CANCELED'):
            raise RuntimeError('%s %s: %s' % (label, status, json.dumps(task.get('task_error'))))
        if time.time() - started > 1800:
            raise RuntimeError('%s did not finish within 30 minutes.' % label)
        time.sleep(8)


def download(url, target):
    with urllib.request.urlopen(url, timeout=300) as response, open(target, 'wb') as handle:
        handle.write(response.read())
    return {'file': target.name, 'bytes': target.stat().st_size,
            'sha256': hashlib.sha256(target.read_bytes()).hexdigest()}


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--out', default='Assets/Models/Buildings/Barracks')
    parser.add_argument('--polycount', type=int, default=5000)
    parser.add_argument('--variant', choices=sorted(VARIANTS), default='first')
    parser.add_argument('--standard', action='store_true',
                        help='Use the standard model with a remesh instead of Smart Topology.')
    args = parser.parse_args()
    out = Path(args.out)
    out.mkdir(parents=True, exist_ok=True)
    prompt, texture_prompt = VARIANTS[args.variant]
    key = api_key()

    before = balance(key)
    print('Balance before: %s credits' % before)
    # Smart Topology generates at the target directly (100-15,000 faces, triangles only). Standard remeshes down.
    preview_body = {'mode': 'preview', 'prompt': prompt, 'target_polycount': args.polycount,
                    'pose_mode': '', 'target_formats': ['glb', 'fbx']}
    if args.standard:
        preview_body.update({'model_type': 'standard', 'should_remesh': True, 'topology': 'triangle'})
    else:
        preview_body.update({'model_type': 'smart-topology', 'ai_model': 'meshy-t2'})

    try:
        preview_id = call('POST', '/v2/text-to-3d', key, preview_body)['result']
    except RuntimeError as error:
        if args.standard:
            raise
        # meshy-t2 needs an account entitlement. Fall back rather than stop, and record that it happened.
        print('Smart Topology refused (%s); falling back to the standard model with a remesh.' % str(error)[:160])
        preview_body.update({'model_type': 'standard', 'should_remesh': True, 'topology': 'triangle'})
        preview_body.pop('ai_model', None)
        preview_id = call('POST', '/v2/text-to-3d', key, preview_body)['result']
    print('Preview task %s' % preview_id)
    preview = wait(key, preview_id, 'preview')

    refine_body = {'mode': 'refine', 'preview_task_id': preview_id, 'enable_pbr': True,
                   'texture_resolution': '2k', 'texture_prompt': texture_prompt, 'target_formats': ['glb', 'fbx']}
    refine_id = call('POST', '/v2/text-to-3d', key, refine_body)['result']
    print('Refine task %s' % refine_id)
    refine = wait(key, refine_id, 'refine')

    files = []
    urls = refine.get('model_urls') or {}
    for kind in ('glb', 'fbx'):
        if urls.get(kind):
            files.append(download(urls[kind], out / ('Barracks.' + kind)))
    textures = refine.get('texture_urls') or []
    if textures:
        for channel, url in textures[0].items():
            if url:
                files.append(download(url, out / ('Barracks_%s.png' % channel)))
    thumbnail = refine.get('thumbnail_url')
    if thumbnail:
        files.append(download(thumbnail, out / 'Barracks_thumbnail.png'))

    after = balance(key)
    manifest = {
        'generated_utc': datetime.now(timezone.utc).isoformat(),
        'service': 'Meshy text-to-3D API v2',
        'preview_task_id': preview_id, 'refine_task_id': refine_id,
        'preview_request': {k: v for k, v in preview_body.items() if k != 'prompt'},
        'refine_request': {k: v for k, v in refine_body.items() if k != 'texture_prompt'},
        'variant': args.variant, 'prompt': prompt, 'texture_prompt': texture_prompt,
        'credits_before': before, 'credits_after': after,
        'credits_spent': (before - after) if isinstance(before, int) and isinstance(after, int) else None,
        'files': files,
        'note': 'Generated for a side-by-side test against the procedural muster_hall. Not yet a shipped asset.',
    }
    (out / 'meshy-manifest.json').write_text(json.dumps(manifest, indent=2), encoding='utf-8')
    print('Spent %s credits. %d files in %s' % (manifest['credits_spent'], len(files), out))
    for entry in files:
        print('  %-28s %8d bytes' % (entry['file'], entry['bytes']))


if __name__ == '__main__':
    main()
