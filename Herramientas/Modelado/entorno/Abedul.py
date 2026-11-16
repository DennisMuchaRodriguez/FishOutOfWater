# -*- coding: utf-8 -*-
"""Abedul: dos troncos finos y blancos con marcas oscuras, y una copa ligera de bultos verde amarillo.
Da color al borde del bosque (entre los pinos oscuros y la pradera). Unos 9 m."""
import os, sys, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from entorno_kit import *

VERDE = '#8DB447'
VERDE_CLARO = '#D6E27C'
VERDE_OSCURO = '#4E7A2E'


def pintar_corteza(T):
    rgb = grad(T, '#B9B2A2', '#EEEAE0', smoothstep(-0.2, 1.6, T.z))
    # Marcas horizontales oscuras (lenticelas) en tramos cortos
    band = np.abs(np.sin(T.z * 7.0 + 2.0 * nse(T, 1.5, 3)))
    dash = smoothstep(0.82, 0.95, band) * smoothstep(0.0, 0.25, nse(T, 3.0, 8))
    rgb = mix(rgb, '#2E2A27', dash * 0.85)
    # Base más oscura y rugosa
    rgb = mix(rgb, '#4F4538', (1 - smoothstep(0.0, 0.7, T.z)) * 0.7)
    return light(T, rgb, 0.08, 0.18)


def pintar_copa(seed):
    def f(T):
        up = np.clip(T.nz, -1, 1)
        rgb = grad(T, VERDE_OSCURO, VERDE, smoothstep(-0.6, 0.2, up))
        rgb = mix(rgb, VERDE_CLARO, smoothstep(0.1, 0.95, up) * 0.65)
        rgb = spots(T, rgb, '#E8D46A', freq=1.8, thresh=0.3, soft=0.2, seed=seed, amount=0.25)
        rgb = spots(T, rgb, VERDE_OSCURO, freq=2.4, thresh=0.32, soft=0.18, seed=seed + 4, amount=0.2)
        return light(T, rgb, 0.08, 0.2)
    return f


def construir():
    b = prop('Abedul', clay=0.01, seed=31)
    troncos = [[(0, 0, -0.4), (0, 0, 0.3), (0.15, 0.05, 3.0), (0.35, 0.1, 6.0), (0.45, 0.12, 7.6)],
               [(0.25, 0.15, -0.4), (0.3, 0.2, 0.3), (-0.3, 0.55, 2.8), (-0.75, 0.9, 5.4), (-0.95, 1.05, 6.6)]]
    radios = [[0.3, 0.24, 0.19, 0.13, 0.08], [0.24, 0.2, 0.15, 0.1, 0.06]]
    for i, (pts, rr) in enumerate(zip(troncos, radios)):
        b.add(tube('Tronco%d' % i, pts, rr, ring=8, samples=6, caps=('flat', 'round'), paint=pintar_corteza))
    copas = [((0.45, 0.15, 7.9), (1.5, 1.4, 1.5)), ((-0.9, 1.0, 6.9), (1.3, 1.2, 1.2)),
             ((1.3, -0.3, 6.9), (1.2, 1.1, 1.1)), ((-0.1, -0.8, 7.0), (1.15, 1.1, 1.05)),
             ((0.2, 0.6, 8.8), (1.0, 0.95, 0.85)), ((-0.6, 1.6, 5.9), (0.9, 0.85, 0.8)),
             ((0.9, 0.9, 6.3), (0.95, 0.9, 0.85))]
    for i, (c, r) in enumerate(copas):
        b.add(blob('Copa%d' % i, c, r, rings=7, segs=13, lump=0.16, freq=1.7, seed=40 + i, clay=0.6,
                   paint=pintar_copa(50 + i)))
    return b


if __name__ == '__main__':
    run_prop('Abedul', construir, res=1024, smoothness=0.12, ao_distance=0.9, bake_range=10.0)
