"""Paint the ground layers of a biome with Meshy image-to-image, then make them tile and pack them for Unity.

    python tools/art/meshy_ground.py forest [desert caribbean] [--model nano-banana-2] [--max-credits 30] [--pack-only]
    python tools/art/meshy_ground.py elven highland volcanic --dry-run      # free: prints each prompt and the cost

One image per layer (tools/art/meshy_ground.json), in the style of the user's own painted ground strip. The paid step
is 6 credits a layer with nano-banana-2. A layer's optional "style" replaces the default "same style and palette as the
grass in the reference" clause, for a biome whose palette is nothing like grass (the volcanic ash). The rest is free
and local:

* the picture is cut square and made seamless: half-offset copies are blended across the seams, first across x and
  then across y, so the result wraps in both directions;
* a layer with "flatten": [amount, blur px] loses that share of its variation above the blur's scale (a wrap-around
  blur, so it still tiles): for a base layer whose drifts or blotches repeat visibly at 5 m a tile (the volcanic
  ash). Fine grain and pebbles stay; the terrain tint and the shader's patch noise give the large-scale variation;
* its colour is pulled gently toward the layer's mean, so a whole biome sits in one palette;
* blurred luminance goes into alpha as height, which the ground shader uses to blend layers with crisp edges;
* the four layers are packed 2x2 into <biome>_layers.png, which Unity imports as a 4-slice Texture2DArray
  (MeshyGroundBaker), plus a small JSON of their means.
"""
import argparse
import base64
import json
import os
import sys
from datetime import datetime, timezone
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import meshy_unit as meshy  # noqa: E402

COST = {'nano-banana': 3, 'nano-banana-2': 6, 'nano-banana-pro': 9}
SIZE = 1024
STYLE = 'in the same painted game-art style and palette as the grass in the reference picture'


def layer_prompt(layer):
    return ('Seamless tileable top-down orthographic ground texture of %s, %s. Seen straight from above, flat even lighting, '
            'no shadows, no people, no objects, no tiles or grid lines, no border, fills the whole frame edge to edge.'
            % (layer['look'], layer.get('style', STYLE)))


def hex_rgb(value):
    value = value.lstrip('#')
    return [int(value[i:i + 2], 16) / 255 for i in (0, 2, 4)]


def seamless(image):
    import numpy as np
    from PIL import Image
    width, height = image.size
    side = min(width, height)
    square = image.crop(((width - side) // 2, (height - side) // 2, (width + side) // 2, (height + side) // 2)).resize((SIZE, SIZE), Image.LANCZOS)
    pixels = np.asarray(square).astype(np.float32)
    ramp = np.sin(np.linspace(0, np.pi, SIZE, endpoint=False)) ** 2
    across = pixels * ramp[None, :, None] + np.roll(pixels, SIZE // 2, axis=1) * (1 - ramp[None, :, None])
    return across * ramp[:, None, None] + np.roll(across, SIZE // 2, axis=0) * (1 - ramp[:, None, None])


def flatten(pixels, amount, sigma):
    """Remove `amount` of the variation broader than a Gaussian of `sigma` pixels, keeping the tile's mean."""
    import numpy as np
    frequency = np.fft.fftfreq(SIZE)
    kernel = np.exp(-2 * (np.pi * sigma) ** 2 * (frequency[:, None] ** 2 + frequency[None, :] ** 2))
    low = np.real(np.fft.ifft2(np.fft.fft2(pixels, axes=(0, 1)) * kernel[..., None], axes=(0, 1)))
    return np.clip(pixels - amount * (low - low.mean(axis=(0, 1))), 0, 255)


def finish_layer(pixels, mean):
    import numpy as np
    from PIL import Image, ImageFilter
    rgb = pixels / 255
    # Keep the painted detail, move the average: a whole biome then reads as one palette instead of five pictures.
    current = rgb.reshape(-1, 3).mean(axis=0)
    rgb = np.clip(rgb * (np.array(mean) / np.maximum(current, 1e-3)) ** .7, 0, 1)
    luminance = Image.fromarray((rgb @ np.array([.2126, .7152, .0722]) * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(3))
    height = np.asarray(luminance).astype(np.float32)
    height = (height - height.min()) / max(1, height.max() - height.min())
    return np.dstack([rgb * 255, height * 255]).astype(np.uint8)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('biomes', nargs='+')
    parser.add_argument('--roster', default='tools/art/meshy_ground.json')
    parser.add_argument('--model', default='nano-banana-2', choices=sorted(COST))
    parser.add_argument('--max-credits', type=int, default=30)
    parser.add_argument('--pack-only', action='store_true')
    parser.add_argument('--redo', type=int, nargs='*', default=[], help='Layer indexes to paint again.')
    parser.add_argument('--dry-run', action='store_true', help='Print each prompt and what a run would cost; no key, no spending.')
    args = parser.parse_args()
    import numpy as np
    from PIL import Image
    roster = json.loads((meshy.ROOT / args.roster).read_text(encoding='utf-8'))
    if args.dry_run:
        dry_run(roster, args)
        return
    raw = Path(roster['raw']); raw.mkdir(parents=True, exist_ok=True)
    out = meshy.ROOT / roster['output']; out.mkdir(parents=True, exist_ok=True)
    reference = 'data:image/png;base64,' + base64.b64encode((meshy.ROOT / roster['reference']).read_bytes()).decode()
    key = None if args.pack_only else meshy.api_key()
    spent = 0
    for biome in args.biomes:
        layers = roster['biomes'][biome]
        manifest_path = out / ('%s_layers.json' % biome)
        manifest = json.loads(manifest_path.read_text(encoding='utf-8')) if manifest_path.exists() else {'biome': biome, 'layers': []}
        while len(manifest['layers']) < len(layers):
            manifest['layers'].append({})
        for index, layer in enumerate(layers):
            source = raw / ('%s_%d.png' % (biome, index))
            if args.pack_only or (source.exists() and index not in args.redo):
                continue
            paid = manifest['layers'][index]
            if not source.exists() and index not in args.redo and paid.get('name') == layer['name'] and paid.get('task', {}).get('id'):
                # Paid for, but the download broke (an SSL drop): fetch the finished task again, which is free.
                task = meshy.call('GET', '/v1/image-to-image/' + paid['task']['id'], key)
                url = next(u for where, u in meshy.urls(task) if where.startswith('image_urls'))
                paid['file'] = meshy.download(url, source)
                manifest_path.write_text(json.dumps(manifest, indent=2, ensure_ascii=False) + '\n', encoding='utf-8')
                print('  %s %s fetched again from task %s (free)' % (biome, layer['name'], paid['task']['id']), flush=True)
                continue
            if spent + COST[args.model] > args.max_credits:
                raise SystemExit('Stopping before %s layer %d: over the cap of %d credits.' % (biome, index, args.max_credits))
            prompt = layer_prompt(layer)
            task_id = meshy.call('POST', '/v1/image-to-image', key, {'ai_model': args.model, 'prompt': prompt, 'aspect_ratio': '1:1',
                                                                      'reference_image_urls': [reference]})['result']
            task = meshy.wait(key, '/v1/image-to-image/' + task_id, '%s %s' % (biome, layer['name']))
            spent += COST[args.model]
            # Recorded before the download, so a broken download is fetched again on the next run instead of paid twice.
            manifest['layers'][index] = {'name': layer['name'], 'task': meshy.strip(task), 'credits': COST[args.model],
                                         'model': args.model, 'prompt': prompt, 'mean': layer['mean'],
                                         'finished_utc': datetime.now(timezone.utc).isoformat()}
            manifest_path.write_text(json.dumps(manifest, indent=2, ensure_ascii=False) + '\n', encoding='utf-8')
            url = next(u for where, u in meshy.urls(task) if where.startswith('image_urls'))
            manifest['layers'][index]['file'] = meshy.download(url, source)
            manifest_path.write_text(json.dumps(manifest, indent=2, ensure_ascii=False) + '\n', encoding='utf-8')
            print('  %s %s painted (%d credits so far)' % (biome, layer['name'], spent), flush=True)
        atlas = np.zeros((SIZE * 2, SIZE * 2, 4), np.uint8)
        for index, layer in enumerate(layers):
            source = raw / ('%s_%d.png' % (biome, index))
            if not source.exists():
                raise SystemExit('%s layer %d has not been painted yet.' % (biome, index))
            pixels = seamless(Image.open(source).convert('RGB'))
            if layer.get('flatten'):
                pixels = flatten(pixels, *layer['flatten'])
            tile = finish_layer(pixels, hex_rgb(layer['mean']))
            # Flipbook order for Unity's 2D-array import: left to right, top to bottom.
            row, column = divmod(index, 2)
            atlas[row * SIZE:(row + 1) * SIZE, column * SIZE:(column + 1) * SIZE] = tile
            manifest['layers'][index]['mean'] = layer['mean']
        Image.fromarray(atlas, 'RGBA').save(out / ('%s_layers.png' % biome))
        manifest_path.write_text(json.dumps(manifest, indent=2, ensure_ascii=False) + '\n', encoding='utf-8')
        print('MESHY_GROUND_OK %s spent=%d' % (biome, spent), flush=True)


def dry_run(roster, args):
    raw = Path(roster['raw'])
    reference = meshy.ROOT / roster['reference']
    print('reference %s %s' % (roster['reference'], 'ok' if reference.exists() else 'MISSING'))
    total = 0
    for biome in args.biomes:
        layers = roster['biomes'].get(biome)
        if not layers:
            raise SystemExit('No biome %r in %s' % (biome, args.roster))
        if len(layers) != 4:
            raise SystemExit('%s has %d layers; the ground shader reads exactly 4.' % (biome, len(layers)))
        for index, layer in enumerate(layers):
            r, g, b = hex_rgb(layer['mean'])
            painted = (raw / ('%s_%d.png' % (biome, index))).exists() and index not in args.redo
            cost = 0 if painted else COST[args.model]
            total += cost
            prompt = layer_prompt(layer)
            print('%s %d %-22s mean %s luma %.2f %3d chars %s' % (biome, index, layer['name'], layer['mean'], .2126 * r + .7152 * g + .0722 * b,
                                                            len(prompt), 'painted' if painted else '%d credits' % cost))
            print('    ' + prompt)
    print('MESHY_GROUND_DRY_RUN credits=%d cap=%d%s' % (total, args.max_credits, '' if total <= args.max_credits else ' OVER THE CAP'))


if __name__ == '__main__':
    main()
