# -*- coding: utf-8 -*-
"""Totora (espadaña): hojas largas y planas que se arquean y tres tallos con su "salchicha" marrón.
Entre 1,4 y 2,1 m."""
import os, sys, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from entorno_kit import *


def pintar_hoja(T):
    rgb = grad(T, '#3B6430', '#5F9040', smoothstep(0.0, 0.5, T.u))
    rgb = mix(rgb, '#B3BF6B', smoothstep(0.72, 1.0, T.u))
    return light(T, rgb, 0.08, 0.18)


def pintar_tallo(T):
    return light(T, grad(T, '#4A6A33', '#7F9A4E', smoothstep(0.0, 1.0, T.u)), 0.08, 0.15)


def pintar_espiga(T):
    rgb = grad(T, '#4F2E1A', '#7C4E2C', smoothstep(-0.5, 0.7, T.nz))
    rgb = spots(T, rgb, '#9A6B42', freq=30.0, thresh=0.3, soft=0.2, seed=3, amount=0.35)
    return light(T, rgb, 0.1, 0.2)


def construir():
    b = prop('Totora', clay=0.01, seed=19)
    rng = np.random.RandomState(20)
    for k in range(6):
        a = 2 * math.pi * k / 6 + rng.uniform(-0.35, 0.35)
        out = np.array([math.cos(a), math.sin(a), 0.0])
        base = out * 0.05 + np.array([0, 0, -0.1])
        H = rng.uniform(1.35, 1.95)
        lean = rng.uniform(0.15, 0.4) * H
        droop = rng.uniform(0.0, 0.3)
        pts = [base, base + out * lean * 0.12 + [0, 0, H * 0.35], base + out * lean * 0.45 + [0, 0, H * 0.7],
               base + out * lean * 0.85 + [0, 0, H * (0.95 - droop * 0.3)], base + out * lean * (1.1 + droop) + [0, 0, H * (0.98 - droop)]]
        b.add(strap('Hoja%d' % k, pts, [0.04, 0.042, 0.034, 0.022, 0.008], 0.007, face=out, ring=4, samples=5,
                    twist=rng.uniform(-30, 30), paint=pintar_hoja))
    for k in range(3):
        a = 2 * math.pi * k / 3 + 0.5
        out = np.array([math.cos(a), math.sin(a), 0.0])
        base = out * 0.07 + np.array([0, 0, -0.1])
        H = 1.6 + 0.25 * k
        top = base + out * 0.12 + [0, 0, H]
        b.add(tube('Tallo%d' % k, [base, base + out * 0.04 + [0, 0, H * 0.5], top], [0.012, 0.01, 0.006], ring=5,
                   samples=3, caps=('flat', 'point'), clay=0.2, paint=pintar_tallo))
        c = base + out * 0.1 + [0, 0, H * 0.8]
        b.add(ellipsoid('Espiga%d' % k, c, (0.042, 0.042, 0.14), axes_from(unit(top - base)), rings=4, segs=6,
                        clay=0.3, paint=pintar_espiga))
    return b


if __name__ == '__main__':
    run_prop('Totora', construir, res=256, smoothness=0.15, ao_distance=0.15, bake_range=2.5, grain=0.03)
