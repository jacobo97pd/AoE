"""Bound the transferred PBR set without downsampling source normal-map resolution.

Re-encode public-domain source JPEGs once at quality 94 with full 4:4:4 chroma.
Keep alpha PNGs lossless. Source downloads remain unchanged on D:.
"""
from pathlib import Path
from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
CACHE = Path('D:/CodexTooling/kingdom-premium/environment/downloads')
OUTPUT = ROOT / 'Assets/Game/KingdomPremium/Environment/Textures'

def prepare():
    OUTPUT.mkdir(parents=True, exist_ok=True)
    count = 0
    for target in OUTPUT.glob('*.jpg'):
        sources = list(CACHE.glob('*/' + target.name))
        if len(sources) != 1:
            raise RuntimeError('Ambiguous source: ' + target.name)
        with Image.open(sources[0]) as source:
            source.convert('RGB').save(target, quality=94, subsampling=0, optimize=True)
        count += 1
    print({'reencodedJpegMaps': count, 'bytes':sum(p.stat().st_size for p in OUTPUT.iterdir() if p.is_file() and p.suffix!='.meta')})

if __name__ == '__main__':
    prepare()
