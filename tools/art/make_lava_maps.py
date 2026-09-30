"""Write the two lava maps for the volcanic land's rivers (its water cells render as lava). Free, local.

    python tools/art/make_lava_maps.py [--preview <png>]

Both tile by construction, like the water maps (tools/art/make_water_maps.py): periodic noise summed with an inverse
FFT, and cells whose distances wrap around the tile, so nothing is blended across a seam.

lava_color.png (RGBA, sRGB colour): the molten flow. RGB runs from dark red through orange to a hot yellow along
swirled, stretched streaks; alpha is the heat (0-1) the colour was taken from, for the shader's emission weight.

lava_crust.png (RGB, linear data, not colour):
  R  distance from the nearest crack, 0 on the crack and 1 in the middle of a plate. A shader thresholds it to lay
     cooled crust over the flow: crust = smoothstep(t, t + .08, R). A high t near the middle of the river leaves open
     lava; a low t near the banks crusts it over. The value just under t is the glowing rim of each plate.
  G  one random value per plate, so plates can differ in tone, or melt one by one (G < melt leaves the plate open).
  B  fine grain for the crust's surface (bumps, pits), tileable.

How the Emberfield water shader would use them (the lava variant is still to be written): scroll lava_color slowly
along the flow at two scales, scroll lava_crust about half as fast so the crust drifts over the melt, then
  colour = lerp(lava, crust colour * (.75 + .5 * G) * (.8 + .4 * B), crust), emission = lava * heat * (1 - crust)
                                                                             + rim glow.
Suggested crust and bank colours to match the volcanic ground (tools/art/meshy_ground.json): crust #2E2624, rim glow
#FF6A1E, bank (wet band) #5C3B31. Both files belong in Resources/Art/Water with the water maps; in Unity lava_color
imports as a default sRGB texture and lava_crust as linear (sRGB off), both with wrap Repeat.
"""
import argparse
from pathlib import Path

import numpy as np
from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'Assets/Game/Resources/Art/Water'
SIZE = 512
# Heat 0..1 to colour: cooled dark red, red, orange, hot yellow, a little white-yellow at the hottest.
RAMP = [(0.0, '#3A0804'), (0.22, '#7A1407'), (0.45, '#C2300E'), (0.66, '#EE6A18'), (0.84, '#FFAE34'), (1.0, '#FFE58C')]


def periodic_noise(seed, falloff, low, high):
    rng = np.random.default_rng(seed)
    fx = np.fft.fftfreq(SIZE) * SIZE
    kx, ky = np.meshgrid(fx, fx)
    radius = np.sqrt(kx ** 2 + ky ** 2)
    band = (radius >= low) & (radius <= high)
    amplitude = np.where(band, 1 / np.maximum(radius, 1) ** falloff, 0)
    spectrum = amplitude * np.exp(2j * np.pi * rng.random((SIZE, SIZE)))
    field = np.real(np.fft.ifft2(spectrum))
    return (field - field.min()) / (field.max() - field.min())


def sample(field, x, y):
    """Bilinear lookup that wraps, so a warped field still tiles."""
    x0, y0 = np.floor(x).astype(int), np.floor(y).astype(int)
    fx, fy = x - x0, y - y0
    x0, y0 = x0 % SIZE, y0 % SIZE
    x1, y1 = (x0 + 1) % SIZE, (y0 + 1) % SIZE
    top = field[y0, x0] * (1 - fx) + field[y0, x1] * fx
    bottom = field[y1, x0] * (1 - fx) + field[y1, x1] * fx
    return top * (1 - fy) + bottom * fy


def hex_rgb(value):
    return np.array([int(value[i:i + 2], 16) for i in (1, 3, 5)], np.float32) / 255


def ramp(heat):
    stops = np.array([s for s, _ in RAMP], np.float32)
    colours = np.array([hex_rgb(c) for _, c in RAMP])
    return np.stack([np.interp(heat, stops, colours[:, channel]) for channel in range(3)], axis=-1)


def lava_color():
    y, x = np.mgrid[0:SIZE, 0:SIZE].astype(np.float32)
    # Two passes of domain warping turn round blobs into the swirled, stretched streaks of a slow melt. The flow runs
    # along x, so the warp pushes further along it than across it.
    warp_a, warp_b = periodic_noise(31, 1.6, 1, 8) - .5, periodic_noise(32, 1.6, 1, 8) - .5
    x1, y1 = x + warp_a * 150, y + warp_b * 60
    warp_c, warp_d = periodic_noise(33, 1.4, 2, 14) - .5, periodic_noise(34, 1.4, 2, 14) - .5
    x2, y2 = x1 + sample(warp_c, x1, y1) * 70, y1 + sample(warp_d, x1, y1) * 34
    base = sample(periodic_noise(35, 1.5, 2, 24), x2, y2)
    detail = sample(periodic_noise(36, 1.1, 10, 60), x2 * 1.0, y2)
    heat = np.clip((base * .8 + detail * .2 - .2) / .62, 0, 1)
    # Hot seams: soft bright bands where the warped field crosses its middle. Kept wide and faint; thin ones drew as
    # contour lines.
    veins = np.clip(1 - np.abs(base - .55) / .09, 0, 1) ** 2
    heat = np.clip(heat * .9 + veins * .16, 0, 1)
    rgb = ramp(heat)
    return Image.fromarray(np.dstack([rgb * 255, heat * 255]).clip(0, 255).astype(np.uint8), 'RGBA'), heat


def lava_crust():
    rng = np.random.default_rng(41)
    points = rng.random((46, 2)) * SIZE
    tone = rng.random(len(points))
    y, x = np.mgrid[0:SIZE, 0:SIZE].astype(np.float32)
    # Warped before measuring, so the cracks wander instead of running in straight Voronoi lines.
    wx = (periodic_noise(42, 1.5, 2, 16) - .5) * 38
    wy = (periodic_noise(43, 1.5, 2, 16) - .5) * 38
    px, py = x + wx, y + wy
    nearest = np.full((SIZE, SIZE), 1e9, np.float32)
    second = np.full((SIZE, SIZE), 1e9, np.float32)
    owner = np.zeros((SIZE, SIZE), np.int32)
    for index, (cx, cy) in enumerate(points):
        dx = np.abs(px - cx) % SIZE; dx = np.minimum(dx, SIZE - dx)
        dy = np.abs(py - cy) % SIZE; dy = np.minimum(dy, SIZE - dy)
        d = np.sqrt(dx * dx + dy * dy)
        closer = d < nearest
        second = np.where(closer, nearest, np.minimum(second, d))
        owner = np.where(closer, index, owner)
        nearest = np.minimum(nearest, d)
    # F2 - F1 is the distance to the border between two plates: zero on the crack.
    edge = second - nearest
    distance = np.clip(edge / 26, 0, 1) ** .8
    # Crack width wobbles along its length, so the threshold opens some cracks wider than others.
    distance = np.clip(distance * (.8 + .4 * periodic_noise(44, 1.3, 4, 30)), 0, 1)
    grain = periodic_noise(45, .9, 16, 120) * .7 + periodic_noise(46, 1.2, 6, 40) * .3
    grain = (grain - grain.min()) / (grain.max() - grain.min())
    rgb = np.dstack([distance, tone[owner], grain])
    return Image.fromarray((rgb * 255).clip(0, 255).astype(np.uint8), 'RGB'), distance, tone[owner], grain


def composite(heat, distance, tone, grain, threshold, shift):
    """What the shader would draw: crust over the flow, glowing rims, at one crack threshold and flow offset."""
    rgb = ramp(np.roll(heat, shift, axis=1))
    lava = rgb * (.55 + .45 * np.roll(heat, shift, axis=1)[..., None])
    crust = np.clip((distance - threshold) / .08, 0, 1)[..., None]
    rim = np.clip(1 - np.abs(distance - threshold) / .07, 0, 1)[..., None] * (1 - crust)
    crust_rgb = hex_rgb('#2E2624') * (.75 + .5 * tone[..., None]) * (.8 + .4 * grain[..., None])
    out = lava * (1 - crust) + crust_rgb * crust + hex_rgb('#FF6A1E') * rim * .6
    return np.clip(out, 0, 1)


def preview(path, color, heat, crust, distance, tone, grain):
    tile = lambda image: np.tile(image, (2, 2, 1)) if image.ndim == 3 else np.tile(image, (2, 2))  # noqa: E731
    panels = [np.asarray(color.convert('RGB')).astype(np.float32) / 255,
              np.asarray(crust).astype(np.float32) / 255]
    rows = [np.concatenate([tile(p) for p in panels], axis=1)]
    heat2, dist2, tone2, grain2 = tile(heat), tile(distance), tile(tone), tile(grain)
    bank = composite(heat2, dist2, tone2, grain2, .12, 0)
    middle = composite(heat2, dist2, tone2, grain2, .42, 180)
    rows.append(np.concatenate([bank, middle], axis=1))
    sheet = np.concatenate(rows, axis=0)
    Image.fromarray((sheet * 255).astype(np.uint8)).resize((1024, 1024), Image.LANCZOS).save(path)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--preview', type=Path, help='Also write a 2x2-tiled contact sheet here: colour, crust mask, '
                                                     'and the composite near a bank (thin cracks) and mid-river (open lava).')
    args = parser.parse_args()
    OUT.mkdir(parents=True, exist_ok=True)
    color, heat = lava_color()
    crust, distance, tone, grain = lava_crust()
    color.save(OUT / 'lava_color.png')
    crust.save(OUT / 'lava_crust.png')
    if args.preview:
        args.preview.parent.mkdir(parents=True, exist_ok=True)
        preview(args.preview, color, heat, crust, distance, tone, grain)
    print('LAVA_MAPS_OK', OUT)


if __name__ == '__main__':
    main()
