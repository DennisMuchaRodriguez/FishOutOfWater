# -*- coding: utf-8 -*-
"""Alga B: mata redonda y tupida (tres tallos con penachos de hojitas), verde oliva con puntas doradas.
Unos 1,1 m."""
import os, sys, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from entorno_kit import *


def pintar_penacho(seed):
    def f(T):
        up = np.clip(T.nz, -1, 1)
        rgb = grad(T, '#33491F', '#5F7E2E', smoothstep(-0.6, 0.2, up))
        rgb = mix(rgb, '#A3BE58', smoothstep(0.0, 0.95, up) * 0.55)
        rgb = spots(T, rgb, '#C4964A', freq=7.0, thresh=0.3, soft=0.15, seed=seed, amount=0.45)
        return light(T, rgb, 0.08, 0.2)
    return f


def pintar_tallo(T):
    return light(T, grad(T, '#3A4A24', '#62773A', smoothstep(0.0, 1.0, T.u)), 0.08, 0.15)


def construir():
    b = prop('Alga_B', clay=0.012, seed=25)
    tallos = [[(0, 0, -0.02), (0.08, 0.02, 0.45), (0.2, 0.05, 0.95)],
              [(0.02, 0.02, -0.02), (-0.12, 0.08, 0.35), (-0.3, 0.12, 0.7)],
              [(-0.02, 0.0, -0.02), (0.02, -0.14, 0.3), (0.05, -0.32, 0.55)]]
    for k, pts in enumerate(tallos):
        b.add(tube('Tallo%d' % k, pts, [0.03, 0.022, 0.012], ring=4, samples=4, caps=('flat', 'point'), clay=0.3,
                   paint=pintar_tallo))
    penachos = [((0.2, 0.05, 0.95), 0.26), ((0.1, 0.03, 0.55), 0.22), ((-0.3, 0.12, 0.72), 0.24),
                ((-0.15, 0.08, 0.38), 0.2), ((0.05, -0.32, 0.56), 0.23), ((0.02, -0.15, 0.25), 0.18),
                ((0.25, 0.25, 0.25), 0.17)]
    for k, (c, r) in enumerate(penachos):
        b.add(blob('Penacho%d' % k, c, (r, r * 0.92, r * 0.72), rings=4, segs=8, lump=0.2, freq=2.2, seed=40 + k,
                   clay=0.5, paint=pintar_penacho(50 + k)))
    return b


if __name__ == '__main__':
    run_prop('Alga_B', construir, res=512, smoothness=0.3, ao_distance=0.15, bake_range=1.5, grain=0.03)
