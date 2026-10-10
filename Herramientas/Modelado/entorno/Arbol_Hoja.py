# -*- coding: utf-8 -*-
"""Árbol de hoja ancha: tronco grueso con tres ramas y una copa redonda de bultos (verde cálido). Unos 8,6 m."""
import os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from entorno_kit import *

VERDE = '#4E8836'
VERDE_CLARO = '#9CC65B'
VERDE_OSCURO = '#2A5126'
AMARILLO = '#B9CF62'


def pintar_copa(seed):
    def f(T):
        up = np.clip(T.nz, -1, 1)
        rgb = grad(T, VERDE_OSCURO, VERDE, smoothstep(-0.65, 0.15, up))
        rgb = mix(rgb, VERDE_CLARO, smoothstep(0.05, 0.95, up) * 0.55)
        # matas de hojas: manchas claras redondas
        rgb = spots(T, rgb, AMARILLO, freq=1.5, thresh=0.22, soft=0.18, seed=seed, amount=0.35)
        rgb = spots(T, rgb, VERDE_OSCURO, freq=2.2, thresh=0.3, soft=0.2, seed=seed + 3, amount=0.25)
        return light(T, rgb, 0.08, 0.22)
    return f


def pintar_madera(T):
    rgb = grad(T, '#4A3324', '#7B5A40', smoothstep(-0.3, 3.0, T.z))
    rgb = strokes(T, rgb, count=10, coord='v', width=0.22, strength=0.38, wobble=0.012, seed=8)
    rgb = spots(T, rgb, '#9A7A58', freq=2.0, thresh=0.3, soft=0.2, amount=0.3, seed=12)
    return light(T, rgb, 0.1, 0.2)


def construir():
    b = prop('Arbol_Hoja', clay=0.012, seed=9)
    b.add(tube('Tronco', [(0, 0, -0.45), (0, 0, 0.3), (0.1, 0.0, 1.9), (0.2, 0.05, 3.5)],
               [0.62, 0.47, 0.37, 0.3], ring=10, samples=5, caps=('flat', 'round'), paint=pintar_madera))
    ramas = [[(0.18, 0.03, 3.0), (1.1, 0.35, 4.5), (1.8, 0.6, 5.3)],
             [(0.12, 0.0, 3.2), (-0.85, -0.45, 4.7), (-1.5, -0.85, 5.5)],
             [(0.2, 0.05, 3.3), (0.28, 0.2, 5.0), (0.32, 0.3, 6.3)]]
    for i, pts in enumerate(ramas):
        b.add(tube('Rama%d' % i, pts, [0.21, 0.14, 0.08], ring=6, samples=4, caps=('flat', 'round'),
                   paint=pintar_madera))
    copas = [((0.2, 0.1, 6.0), (2.4, 2.3, 1.9)), ((1.9, 0.6, 5.5), (1.7, 1.6, 1.4)),
             ((-1.6, -0.8, 5.5), (1.7, 1.6, 1.4)), ((-0.6, 1.5, 5.8), (1.6, 1.5, 1.35)),
             ((0.9, -1.5, 5.7), (1.6, 1.5, 1.35)), ((0.3, 0.2, 7.3), (1.7, 1.6, 1.3)),
             ((-1.2, 0.4, 6.8), (1.2, 1.1, 1.0))]
    for i, (c, r) in enumerate(copas):
        b.add(blob('Copa%d' % i, c, r, rings=7, segs=14, lump=0.14, freq=1.4, seed=60 + i, clay=0.6,
                   paint=pintar_copa(70 + i)))
    return b


if __name__ == '__main__':
    run_prop('Arbol_Hoja', construir, res=1024, smoothness=0.12, ao_distance=1.0, bake_range=10.0)
