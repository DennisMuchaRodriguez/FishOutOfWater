# -*- coding: utf-8 -*-
"""Arbusto: cinco bultos redondos con bayas pintadas. Unos 2 m de ancho y 1,3 m de alto."""
import os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from entorno_kit import *

VERDE = '#457E39'
VERDE_CLARO = '#87BA5A'
VERDE_OSCURO = '#23462A'
BAYA = '#C9465C'


def pintar(seed):
    def f(T):
        up = np.clip(T.nz, -1, 1)
        rgb = grad(T, VERDE_OSCURO, VERDE, smoothstep(-0.6, 0.15, up))
        rgb = mix(rgb, VERDE_CLARO, smoothstep(0.1, 0.95, up) * 0.55)
        rgb = spots(T, rgb, '#A9C766', freq=4.0, thresh=0.25, soft=0.15, seed=seed, amount=0.3)
        # bayas: puntitos rojos en lo alto
        rgb = spots(T, rgb, BAYA, freq=9.0, thresh=0.42, soft=0.04, seed=seed + 7, amount=0.95 * smoothstep(-0.1, 0.4, up))
        return light(T, rgb, 0.08, 0.22)
    return f


def construir():
    b = prop('Arbusto', clay=0.012, seed=11)
    bultos = [((0, 0, 0.55), (0.85, 0.8, 0.62)), ((0.62, 0.35, 0.42), (0.62, 0.56, 0.5)),
              ((-0.62, 0.2, 0.4), (0.6, 0.55, 0.48)), ((0.12, -0.62, 0.4), (0.6, 0.5, 0.46)),
              ((-0.2, 0.48, 0.86), (0.5, 0.48, 0.42))]
    for i, (c, r) in enumerate(bultos):
        b.add(blob('Bulto%d' % i, c, r, rings=5, segs=10, lump=0.12, freq=1.6, seed=80 + i, floor=-0.08,
                   clay=0.6, paint=pintar(90 + i)))
    return b


if __name__ == '__main__':
    run_prop('Arbusto', construir, res=512, smoothness=0.12, ao_distance=0.4, bake_range=3.0)
