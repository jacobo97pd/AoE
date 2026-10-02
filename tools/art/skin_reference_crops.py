"""Clean front and back views of the three store-skin characters, ready for Meshy's Multi-Image to 3D. Free.

    python tools/art/skin_reference_crops.py [dwarf|elf|ranger ...] [--sheet <png>]

The user's own pictures (outside the repository, in D:/EmberfieldWorkingCache/cosmetics-refs) are never restyled. The
only edits are the ones a model sheet needs before a modeller sees it:

* the dwarf's front carries a title and its back a "VISTA FRONTAL" / "VISTA TRASERA" label; the rows holding the
  text are cut off, and the cut is padded back out with the picture's own background (the nearest edge of a lightly blurred
  copy of the flat grey, so no seam shows). Not one pixel of the figure is touched;
* front and back share one canvas, the figure's soles on the same line and its centre on the same column, with the
  margin Meshy reads a single object best with (6% of the figure's longest side);
* the dwarf's 512-pixel pictures are doubled with Lanczos so that all six pictures are about the same size.

The result is written next to the sources, in crops/<name>_front_clean.png and crops/<name>_back_clean.png.
"""
import argparse
import sys
from pathlib import Path

import numpy as np
from PIL import Image
from scipy import ndimage

BASE = Path('D:/EmberfieldWorkingCache/cosmetics-refs')
# keep = the rectangle (left, top, right, bottom) of the source that is free of text; None keeps everything.
SOURCES = {
    'dwarf': {'front': 'dwarf_front_titled.png', 'back': 'dwarf_back_labeled.png', 'scale': 2,
              'keep_front': (0, 70, 512, 512), 'keep_back': (0, 24, 512, 474)},
    'elf': {'front': 'elf_archer_front.png', 'back': 'elf_archer_back.png', 'scale': 1},
    'ranger': {'front': 'ranger_front.png', 'back': 'ranger_back.png', 'scale': 1},
}


def figure_mask(pixels, backdrop=None):
    """Boolean mask of the biggest solid shape in a picture on a plain backdrop: the figure and what it carries."""
    if backdrop is None:
        border = np.concatenate([pixels[:4].reshape(-1, 3), pixels[-4:].reshape(-1, 3), pixels[:, :4].reshape(-1, 3), pixels[:, -4:].reshape(-1, 3)])
        backdrop = np.median(border, axis=0)
    solid = ndimage.binary_opening(np.abs(pixels.astype(np.int16) - backdrop).max(axis=2) > 24, iterations=1)
    labels, count = ndimage.label(solid)
    sizes = ndimage.sum(solid, labels, range(1, count + 1))
    return labels == int(np.argmax(sizes)) + 1


def figure_box(pixels, backdrop):
    """Bounding box (left, top, right, bottom, inclusive) of the figure."""
    ys, xs = np.where(figure_mask(pixels, backdrop))
    return xs.min(), ys.min(), xs.max(), ys.max()


def prepare(name):
    entry = SOURCES[name]
    views = {}
    for side in ('front', 'back'):
        full = np.asarray(Image.open(BASE / entry[side]).convert('RGB'))
        left, top, right, bottom = entry.get('keep_' + side) or (0, 0, full.shape[1], full.shape[0])
        kept = full[top:bottom, left:right]
        border = np.concatenate([kept[:4].reshape(-1, 3), kept[-4:].reshape(-1, 3), kept[:, :4].reshape(-1, 3), kept[:, -4:].reshape(-1, 3)])
        backdrop = np.median(border, axis=0)
        box = figure_box(kept, backdrop)
        views[side] = {'pixels': kept, 'box': box}
    widths = [v['box'][2] - v['box'][0] + 1 for v in views.values()]
    heights = [v['box'][3] - v['box'][1] + 1 for v in views.values()]
    pad = int(max(max(widths), max(heights)) * .06)
    canvas_w, canvas_h = max(widths) + pad * 2, max(heights) + pad * 2
    result = {}
    for side, view in views.items():
        left, top, right, bottom = view['box']
        # The figure's soles on one line and its centre on one column, the same in both views.
        shift_x = int(round(canvas_w / 2 - (left + right) / 2))
        shift_y = int(round((canvas_h - pad) - (bottom + 1)))
        ys, xs = np.mgrid[0:canvas_h, 0:canvas_w]
        sx, sy = xs - shift_x, ys - shift_y
        # Past the source's edge the backdrop is the nearest edge pixel of a lightly blurred copy (the flat grey is smooth,
        # so this leaves no seam); inside the source its own pixels stay as they are, blending out over a few pixels.
        kept = view['pixels']
        soft = ndimage.gaussian_filter(kept.astype(np.float64), (3, 3, 0))
        image = soft[np.clip(sy, 0, kept.shape[0] - 1), np.clip(sx, 0, kept.shape[1] - 1)]
        inside = (sx >= 0) & (sx < kept.shape[1]) & (sy >= 0) & (sy < kept.shape[0])
        weight = ndimage.gaussian_filter(ndimage.binary_erosion(inside, iterations=6).astype(np.float64), 3)[..., None]
        source = np.zeros((canvas_h, canvas_w, 3), np.float64)
        source[inside] = kept[sy[inside], sx[inside]]
        image = image * (1 - weight) + source * weight
        picture = Image.fromarray(np.clip(image + .5, 0, 255).astype(np.uint8))
        if entry['scale'] != 1:
            picture = picture.resize((picture.width * entry['scale'], picture.height * entry['scale']), Image.LANCZOS)
        result[side] = picture
    return result


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('names', nargs='*', default=list(SOURCES))
    parser.add_argument('--sheet', help='also write a contact sheet of the results to this path')
    args = parser.parse_args()
    out = BASE / 'crops'
    out.mkdir(exist_ok=True)
    tiles = []
    for name in args.names:
        views = prepare(name)
        for side, picture in views.items():
            target = out / ('%s_%s_clean.png' % (name, side))
            picture.save(target)
            print('%s -> %s %s' % (name, target, picture.size))
        tiles.append((views['front'], views['back']))
    if args.sheet:
        cell = 560
        sheet = Image.new('RGB', (cell * 2, cell * len(tiles)), (40, 40, 40))
        for row, (front, back) in enumerate(tiles):
            for column, picture in enumerate((front, back)):
                scale = min(cell / picture.width, cell / picture.height)
                shown = picture.resize((int(picture.width * scale), int(picture.height * scale)), Image.LANCZOS)
                sheet.paste(shown, (column * cell + (cell - shown.width) // 2, row * cell + (cell - shown.height) // 2))
        sheet.save(args.sheet)
        print('sheet ->', args.sheet)


if __name__ == '__main__':
    sys.exit(main())
