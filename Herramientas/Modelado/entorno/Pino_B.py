# -*- coding: utf-8 -*-
"""Pino B: cono de bultos apilados (como un pino de caricatura hecho de bolitas de plastilina), verde frío.
Más alto y delgado que el A: unos 12 m."""
import os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from entorno_kit import *

VERDE = '#2D5F4C'
VERDE_CLARO = '#5F9677'
VERDE_OSCURO = '#183830'
PUNTA = '#86B58F'


def pintar_bulto(seed):
    def f(T):
        up = np.clip(T.nz, -1, 1)
        rgb = grad(T, VERDE_OSCURO, VERDE, smoothstep(-0.6, 0.2, up))
        rgb = mix(rgb, VERDE_CLARO, smoothstep(0.1, 0.9, up) * 0.6)
        rgb = spots(T, rgb, PUNTA, freq=1.6, thresh=0.18, soft=0.3, seed=seed, amount=0.25)
        rgb = strokes(T, rgb, count=18, coord='v', width=0.12, strength=0.1, wobble=0.006, seed=seed)
        return light(T, rgb, 0.08, 0.22)
    return f


def pintar_tronco(T):
    rgb = grad(T, '#3F2A1E', '#6E4C35', smoothstep(-0.3, 2.0, T.z))
    rgb = strokes(T, rgb, count=8, coord='v', width=0.22, strength=0.4, wobble=0.01, seed=4)
    return light(T, rgb, 0.1, 0.2)


def construir():
    b = prop('Pino_B', clay=0.012, seed=5)
    b.add(tube('Tronco', [(0, 0, -0.45), (0, 0, 0.25), (-0.03, 0.02, 1.8), (-0.05, 0.03, 4.0)],
               [0.46, 0.34, 0.27, 0.18], ring=8, samples=5, caps=('flat', 'round'), paint=pintar_tronco))
    rng = np.random.RandomState(8)
    capas = [(2.9, 2.2, 1.0), (4.25, 1.95, 1.0), (5.55, 1.72, 0.95), (6.8, 1.48, 0.9), (7.95, 1.24, 0.86),
             (9.0, 1.0, 0.8), (9.95, 0.78, 0.74), (10.8, 0.56, 0.68), (11.55, 0.34, 0.55)]
    for i, (z, r, hz) in enumerate(capas):
        dx, dy = rng.uniform(-0.08, 0.08, 2) * r
        p = blob('Bulto%d' % i, (dx, dy, z), (r, r * 0.96, hz), rings=6, segs=14, lump=0.1, freq=1.7,
                 seed=20 + i, clay=0.6, paint=pintar_bulto(30 + i))
        # la mitad de abajo más plana (cada bulto parece una falda que cae sobre el de abajo)
        below = p.verts[:, 2] < z
        p.verts[below, 2] = z + (p.verts[below, 2] - z) * 0.55
        b.add(p)
    return b


if __name__ == '__main__':
    run_prop('Pino_B', construir, res=1024, smoothness=0.12, ao_distance=1.2, bake_range=13.0)
