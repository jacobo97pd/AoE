"""Download only selected public-domain geometry/PBR maps to the D: authoring cache."""
from __future__ import annotations
import concurrent.futures
import json
import pathlib
import urllib.request

CACHE = pathlib.Path('D:/CodexTooling/kingdom-premium/environment')
MODEL_IDS = ['large_castle_door', 'wine_barrel_01', 'wooden_crate_01', 'fern_02',
             'grass_medium_01', 'rock_moss_set_01', 'tree_small_02']
SURFACE_IDS = ['medieval_blocks_05', 'cobblestone_floor_08', 'roof_slates_02',
               'white_rough_plaster', 'wood_planks_dirt', 'brown_mud_leaves_01','leafy_grass']
MAP_KEYS = ['Diffuse', 'nor_gl', 'Rough', 'Metal', 'Alpha',
            'branch_diff', 'branch_nor_gl', 'branch_rough',
            'leaves_diff', 'leaves_nor_gl', 'leaves_rough', 'leaves_alpha']

def json_get(url):
    req = urllib.request.Request(url, headers={'User-Agent': 'Emberfield-Art-Authoring/1.0'})
    with urllib.request.urlopen(req, timeout=40) as response:
        return json.load(response)

def download(url, path, size=0):
    path = pathlib.Path(path)
    if path.exists() and (not size or path.stat().st_size == size):
        return str(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_suffix(path.suffix + '.part')
    req = urllib.request.Request(url, headers={'User-Agent': 'Emberfield-Art-Authoring/1.0'})
    with urllib.request.urlopen(req, timeout=120) as response, temporary.open('wb') as output:
        while chunk := response.read(1024 * 1024):
            output.write(chunk)
    temporary.replace(path)
    return str(path)

def prepare(asset_id):
    folder = CACHE / 'downloads' / asset_id
    files = json_get('https://api.polyhaven.com/files/' + asset_id)
    info = json_get('https://api.polyhaven.com/info/' + asset_id)
    model = asset_id in MODEL_IDS
    resolution = '1k' if model else '2k'
    result = {'id': asset_id, 'name': info['name'], 'url': 'https://polyhaven.com/a/' + asset_id,
              'license': 'CC0-1.0', 'licenseUrl': 'https://polyhaven.com/license',
              'author': ', '.join(info.get('authors', {}).keys()), 'polycountSource': info.get('polycount'),
              'dimensionsMillimetres': info.get('dimensions'), 'maps': {}, 'mapUrls': {}}
    if model:
        fbx = files['fbx']['1k']['fbx']
        result['fbx'] = download(fbx['url'], folder / (asset_id + '.fbx'), fbx['size'])
        result['fbxUrl'] = fbx['url']
    for key in MAP_KEYS:
        candidates = files.get(key, {}).get(resolution, {})
        if not candidates:
            continue
        fmt = 'png' if 'alpha' in key.lower() and 'png' in candidates else 'jpg' if 'jpg' in candidates else 'png'
        if fmt not in candidates:
            continue
        item = candidates[fmt]
        result['maps'][key] = download(item['url'], folder / (asset_id + '_' + key.lower() + '.' + fmt), item['size'])
        result['mapUrls'][key] = item['url']
    return result

if __name__ == '__main__':
    CACHE.mkdir(parents=True, exist_ok=True)
    assets = {}
    with concurrent.futures.ThreadPoolExecutor(max_workers=4) as pool:
        pending = {pool.submit(prepare, asset_id): asset_id for asset_id in MODEL_IDS + SURFACE_IDS}
        for future in concurrent.futures.as_completed(pending):
            item = future.result()
            assets[item['id']] = item
            print(json.dumps({'ready': item['id'], 'maps': len(item['maps'])}), flush=True)
    (CACHE / 'download-index.json').write_text(json.dumps(assets, indent=2), encoding='utf-8')
    print(json.dumps({'assets': len(assets), 'cache': str(CACHE)}), flush=True)
