# -*- coding: utf-8 -*-
"""Piedras del fondo: siete cantos chatos de colores distintos, medio enterrados. Un grupo de 1,5 m."""
import os, sys, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from entorno_kit import *

COLORES = ['#9A9488', '#6F7A86', '#B5A688', '#5F6662', '#8A7462', '#A3A9A6', '#7C8570', '#8E8F96']


def pintar(col, seed):
    def f(T):
        rgb = grad(T, '#3F4448', col, smoothstep(-0.8, 0.3, T.nz))
        rgb = mix(rgb, '#FFFFFF', smoothstep(0.3, 1.0, T.nz) * 0.15)
        rgb = spots(T, rgb, '#4E5A50', freq=6.0, thresh=0.3, soft=0.2, seed=seed, amount=0.3)
        return light(T, rgb, 0.08, 0.2)
    return f


def construir():
    b = prop('Piedras_Fondo', clay=0.015, seed=33)
    rng = np.random.RandomState(34)
    puestos = []
    for k in range(7):
        for _ in range(60):
            r = rng.uniform(0.1, 0.3)
            a = rng.uniform(0, 2 * math.pi)
            d = 0.62 * math.sqrt(rng.uniform(0, 1))
            x, y = d * math.cos(a), d * math.sin(a)
            if all(math.hypot(x - px, y - py) > (r + pr) * 0.8 for px, py, pr in puestos):
                break
        puestos.append((x, y, r))
        hz = r * rng.uniform(0.45, 0.7)
        ax = rot_matrix((0, 0, 1), rng.uniform(0, 360)) @ rot_matrix((1, 0, 0), rng.uniform(-12, 12))
        b.add(blob('Piedra%d' % k, (x, y, hz * 0.35), (r, r * rng.uniform(0.7, 0.95), hz), rings=4, segs=8,
                   lump=0.12, freq=1.5, seed=k, axes=ax, floor=-0.02, clay=0.5, paint=pintar(COLORES[k], 40 + k)))
    return b


if __name__ == '__main__':
    run_prop('Piedras_Fondo', construir, res=256, smoothness=0.35, ao_distance=0.12, bake_range=1.0)
