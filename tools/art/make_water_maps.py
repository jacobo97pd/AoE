"""Write the two water maps the Meshy Water shader scrolls: a ripple normal map and a foam pattern. Free, local.

    python tools/art/make_water_maps.py

Both are built from periodic noise (random phases on an integer frequency grid, summed with an inverse FFT), so they
tile by construction instead of by a blended seam. Ripples are gentle and broad, which is what reads from an RTS
camera; foam is cellular (Worley-like) so the shore line breaks into lace rather than a flat stripe.
"""
from pathlib import Path

import numpy as np
from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'Assets/Game/Resources/Art/Water'
SIZE = 512


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


def normal_map():
    height = periodic_noise(11, 1.8, 2, 18) * .8 + periodic_noise(12, 1.3, 8, 36) * .2
    # Central differences that wrap, so the normals tile exactly as the height does.
    dx = (np.roll(height, -1, axis=1) - np.roll(height, 1, axis=1)) * 14
    dy = (np.roll(height, -1, axis=0) - np.roll(height, 1, axis=0)) * 14
    normal = np.dstack([-dx, -dy, np.ones_like(height)])
    normal /= np.linalg.norm(normal, axis=2, keepdims=True)
    return Image.fromarray(((normal * .5 + .5) * 255).astype(np.uint8), 'RGB')


def foam_map():
    rng = np.random.default_rng(21)
    points = rng.random((90, 2)) * SIZE
    y, x = np.mgrid[0:SIZE, 0:SIZE].astype(np.float32)
    nearest = np.full((SIZE, SIZE), 1e9, np.float32)
    second = np.full((SIZE, SIZE), 1e9, np.float32)
    for px, py in points:
        dx = np.abs(x - px); dx = np.minimum(dx, SIZE - dx)
        dy = np.abs(y - py); dy = np.minimum(dy, SIZE - dy)
        d = np.sqrt(dx * dx + dy * dy)
        second = np.where(d < nearest, nearest, np.minimum(second, d))
        nearest = np.minimum(nearest, d)
    # Bright along the cell borders, where two bubbles meet: lace, not blobs.
    lace = 1 - np.clip((second - nearest) / 4, 0, 1)
    lace = lace * (.6 + .4 * periodic_noise(22, 1.4, 3, 30))
    return Image.fromarray((np.clip(lace, 0, 1) * 255).astype(np.uint8), 'L')


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    normal_map().save(OUT / 'water_normal.png')
    foam_map().save(OUT / 'water_foam.png')
    print('WATER_MAPS_OK', OUT)


if __name__ == '__main__':
    main()
