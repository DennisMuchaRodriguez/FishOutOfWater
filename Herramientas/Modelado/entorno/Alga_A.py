# -*- coding: utf-8 -*-
"""Alga A: seis cintas largas y onduladas (como hierba de agua), turquesa. Entre 1,4 y 2,3 m."""
import os, sys, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from entorno_kit import *


def pintar_cinta(T):
    rgb = grad(T, '#1D4A45', '#2E7E6D', smoothstep(0.0, 0.45, T.u))
    rgb = mix(rgb, '#7FCDB2', smoothstep(0.55, 1.0, T.u) * 0.8)
    # nervio central más claro
    mid = 1 - smoothstep(0.05, 0.16, np.minimum(np.abs(T.v - 0.0), np.minimum(np.abs(T.v - 0.5), np.abs(T.v - 1.0))))
    rgb = mix(rgb, '#9BE0C6', mid * 0.25)
    return light(T, rgb, 0.06, 0.12)


def pintar_base(T):
    return light(T, grad(T, '#2B3B2E', '#3E5A44', smoothstep(-0.5, 0.8, T.nz)), 0.1, 0.2)


def construir():
    b = prop('Alga_A', clay=0.01, seed=23)
    rng = np.random.RandomState(24)
    for k in range(6):
        a = 2 * math.pi * k / 6 + rng.uniform(-0.4, 0.4)
        out = np.array([math.cos(a), math.sin(a), 0.0])
        side = np.array([-out[1], out[0], 0.0])
        base = out * 0.06 + np.array([0, 0, 0.02])
        H = rng.uniform(1.4, 2.3)
        ph = rng.uniform(0, 6.28)
        pts = []
        for i in range(6):
            t = i / 5
            wig = math.sin(t * 5.5 + ph) * 0.1 * t
            pts.append(base + out * (0.18 * t * H * 0.4) + side * wig + [0, 0, H * t])
        b.add(strap('Cinta%d' % k, pts, [0.03, 0.055, 0.065, 0.06, 0.045, 0.012], 0.006, face=out, ring=4, samples=6,
                    twist=rng.uniform(60, 160) * (1 if k % 2 else -1), paint=pintar_cinta))
    b.add(blob('Base', (0, 0, 0.02), (0.14, 0.13, 0.07), rings=4, segs=8, lump=0.15, seed=3, floor=-0.03,
               paint=pintar_base))
    return b


if __name__ == '__main__':
    run_prop('Alga_A', construir, res=512, smoothness=0.3, ao_distance=0.15, bake_range=2.5, grain=0.03)
