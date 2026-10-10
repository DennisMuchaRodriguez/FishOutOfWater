# -*- coding: utf-8 -*-
"""Mata de hierba: siete hojas que se abren desde la base. Unos 0,5 m."""
import os, sys, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from entorno_kit import *


def pintar(T):
    rgb = grad(T, '#3D6E2C', '#6FA041', smoothstep(0.0, 0.55, T.u))
    rgb = mix(rgb, '#BFCF6D', smoothstep(0.6, 1.0, T.u))
    return light(T, rgb, 0.1, 0.15)


def construir():
    b = prop('Hierba', clay=0.01, seed=13)
    rng = np.random.RandomState(14)
    n = 7
    for k in range(n):
        a = 2 * math.pi * k / n + rng.uniform(-0.3, 0.3)
        out = np.array([math.cos(a), math.sin(a), 0.0])
        H = rng.uniform(0.32, 0.55)
        lean = rng.uniform(0.12, 0.24) * H
        base = out * 0.035 + np.array([0, 0, -0.03])
        pts = [base, base + out * lean * 0.12 + [0, 0, 0.35 * H], base + out * lean * 0.5 + [0, 0, 0.72 * H],
               base + out * lean + [0, 0, H]]
        b.add(strap('Hoja%d' % k, pts, [0.03, 0.027, 0.019, 0.008], 0.006, face=out, ring=4, samples=4,
                    paint=pintar))
    return b


if __name__ == '__main__':
    run_prop('Hierba', construir, res=256, smoothness=0.1, ao_distance=0.1, bake_range=1.0, grain=0.03)
