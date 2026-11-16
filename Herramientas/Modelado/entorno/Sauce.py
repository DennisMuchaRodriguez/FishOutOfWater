# -*- coding: utf-8 -*-
"""Sauce llorón de orilla: tronco grueso inclinado, ramas en arco y cortinas de hojas que caen hasta cerca
del suelo (verde amarillento). Es el árbol de la franja leñosa de la orilla. Unos 7,5 m."""
import os, sys, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from entorno_kit import *

VERDE = '#7FA442'
VERDE_CLARO = '#C3D46A'
VERDE_OSCURO = '#41662B'


def pintar_madera(T):
    rgb = grad(T, '#4C3A2A', '#80684C', smoothstep(-0.3, 3.0, T.z))
    rgb = strokes(T, rgb, count=12, coord='v', width=0.24, strength=0.45, wobble=0.02, seed=4)
    rgb = spots(T, rgb, '#9C8A66', freq=2.2, thresh=0.32, soft=0.2, amount=0.3, seed=7)
    return light(T, rgb, 0.1, 0.2)


def pintar_copa(seed):
    def f(T):
        up = np.clip(T.nz, -1, 1)
        rgb = grad(T, VERDE_OSCURO, VERDE, smoothstep(-0.6, 0.2, up))
        rgb = mix(rgb, VERDE_CLARO, smoothstep(0.1, 0.95, up) * 0.6)
        rgb = spots(T, rgb, VERDE_CLARO, freq=1.6, thresh=0.25, soft=0.2, seed=seed, amount=0.3)
        return light(T, rgb, 0.08, 0.22)
    return f


def pintar_cortina(T):
    # De la rama (arriba, más oscuro) a la punta (abajo, más claro y amarillo)
    rgb = grad(T, VERDE_OSCURO, VERDE, smoothstep(0.0, 0.45, T.u))
    rgb = mix(rgb, VERDE_CLARO, smoothstep(0.55, 1.0, T.u) * 0.8)
    rgb = strokes(T, rgb, count=3, coord='v', width=0.3, strength=0.18, wobble=0.05, seed=5)
    return light(T, rgb, 0.08, 0.12)


def construir():
    b = prop('Sauce', clay=0.012, seed=21)
    # Tronco que se inclina un poco
    b.add(tube('Tronco', [(0, 0, -0.45), (0, 0, 0.3), (0.35, 0.05, 1.8), (0.75, 0.1, 3.2), (0.95, 0.12, 4.2)],
               [0.62, 0.5, 0.4, 0.33, 0.26], ring=10, samples=6, caps=('flat', 'round'), paint=pintar_madera))
    rng = np.random.RandomState(22)
    top = np.array([0.95, 0.12, 4.2])
    # Ramas en arco que se abren (terminan dentro de la copa: no asoman)
    for k in range(5):
        a = 2 * math.pi * k / 5 + rng.uniform(-0.25, 0.25)
        out = np.array([math.cos(a), math.sin(a), 0.0])
        L = rng.uniform(1.4, 1.8)
        p1 = top + out * L * 0.45 + [0, 0, 0.9]
        p2 = top + out * L + [0, 0, 1.3]
        b.add(tube('Rama%d' % k, [top, p1, p2], [0.2, 0.13, 0.08], ring=6, samples=4,
                   caps=('flat', 'round'), paint=pintar_madera))
    # Copa: cúpula redonda de bultos (más alta que ancha en el centro)
    copas = [((0.95, 0.12, 6.1), (2.1, 2.1, 1.6)), ((2.2, 0.5, 5.6), (1.35, 1.25, 1.05)),
             ((-0.3, -0.3, 5.6), (1.35, 1.25, 1.05)), ((0.7, 1.5, 5.7), (1.3, 1.2, 1.0)),
             ((1.3, -1.3, 5.7), (1.3, 1.2, 1.0)), ((0.95, 0.12, 7.2), (1.3, 1.25, 0.9))]
    for i, (c, r) in enumerate(copas):
        b.add(blob('Copa%d' % i, c, r, rings=7, segs=14, lump=0.12, freq=1.5, seed=90 + i, clay=0.6,
                   paint=pintar_copa(95 + i)))
    # Cortinas: tiras de hojas que cuelgan desde la copa. Anillo exterior largo y anillo interior más corto
    # (así se ve tupido y con profundidad)
    center = np.array([0.95, 0.12, 0.0])
    for ring, (n, rmin, rmax, dmin, dmax, z_lo, z_hi) in enumerate([(30, 2.0, 2.5, 3.4, 4.6, 5.0, 5.6),
                                                                     (18, 1.2, 1.6, 2.2, 3.0, 4.9, 5.3)]):
        for k in range(n):
            a = 2 * math.pi * (k + 0.5 * ring) / n + rng.uniform(-0.08, 0.08)
            out = np.array([math.cos(a), math.sin(a), 0.0])
            R0 = rng.uniform(rmin, rmax)
            z0 = rng.uniform(z_lo, z_hi)
            drop = rng.uniform(dmin, dmax)
            start = center + out * R0 + [0, 0, z0]
            pts = [start, start + out * 0.3 + [0, 0, -0.25 * drop], start + out * 0.48 + [0, 0, -0.6 * drop],
                   start + out * 0.52 + [0, 0, -drop]]
            w = rng.uniform(0.11, 0.17)
            b.add(strap('Cortina%d_%d' % (ring, k), pts, [w * 0.8, w, w * 0.85, w * 0.25], 0.012, face=out,
                        ring=4, samples=5, paint=pintar_cortina))
    return b


if __name__ == '__main__':
    run_prop('Sauce', construir, res=1024, smoothness=0.12, ao_distance=1.0, bake_range=10.0)
