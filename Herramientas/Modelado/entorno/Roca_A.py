# -*- coding: utf-8 -*-
"""Roca A: canto rodado grande y redondeado, gris azulado con musgo arriba. Unos 2,4 m de ancho y 1,4 m de alto.
Sirve en la orilla y en el fondo del lago (bajo el agua el musgo parece alga)."""
import os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from entorno_kit import *


def pintar(T):
    rgb = grad(T, '#4E5864', '#7E8996', smoothstep(-0.7, 0.2, T.nz))
    rgb = mix(rgb, '#A7B0B9', smoothstep(0.2, 0.95, T.nz) * 0.45)
    rgb = spots(T, rgb, '#5F6A76', freq=2.2, thresh=0.15, soft=0.25, seed=4, amount=0.35)
    rgb = spots(T, rgb, '#C9C6A8', freq=9.0, thresh=0.45, soft=0.06, seed=7, amount=0.5)
    rgb = moss(T, rgb, '#6B8A40', amount=0.9, up=0.5, freq=0.9, seed=5)
    return light(T, rgb, 0.08, 0.2)


def construir():
    b = prop('Roca_A', clay=0.015, seed=31)
    b.add(blob('Roca', (0, 0, 0.5), (1.2, 1.05, 0.92), rings=10, segs=18, lump=0.13, freq=1.1, seed=12,
               floor=-0.3, clay=0.8, paint=pintar))
    return b


if __name__ == '__main__':
    run_prop('Roca_A', construir, res=512, smoothness=0.25, readable=True, ao_distance=0.5, bake_range=2.0)
