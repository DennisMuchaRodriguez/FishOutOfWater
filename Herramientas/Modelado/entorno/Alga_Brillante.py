# -*- coding: utf-8 -*-
"""Alga brillante (fondo profundo): cinco tallos curvos con bulbos que brillan cian. Unos 1 m.
El material lleva emisión (mapa _Emission): solo brillan los bulbos y la punta de los tallos."""
import os, sys, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from entorno_kit import *

CIAN = np.array([0.55, 1.0, 0.92])


def pintar_tallo(T):
    rgb = grad(T, '#123437', '#2C7774', smoothstep(0.1, 1.0, T.u))
    return light(T, rgb, 0.08, 0.15)


def brillo_tallo(T):
    return CIAN[None] * (smoothstep(0.72, 1.0, T.u) * 0.55)[:, None]


def pintar_bulbo(T):
    rgb = grad(T, '#39CDBD', '#C9FFF5', smoothstep(-0.3, 0.9, T.nz))
    return rgb


def brillo_bulbo(c):
    def f(T):
        d = np.linalg.norm(T.pos - c[None], axis=1)
        core = 1 - smoothstep(0.0, 0.09, d)
        return CIAN[None] * (0.75 + 0.25 * core)[:, None] + np.array([0.3, 0.1, 0.1])[None] * core[:, None]
    return f


def construir():
    b = prop('Alga_Brillante', clay=0.01, seed=27)
    rng = np.random.RandomState(28)
    b.add(blob('Base', (0, 0, 0.02), (0.12, 0.11, 0.06), rings=3, segs=6, lump=0.15, seed=5, floor=-0.03,
               paint=lambda T: light(T, grad(T, '#14262A', '#24464A', smoothstep(-0.5, 0.8, T.nz)), 0.1, 0.2)))
    for k in range(5):
        a = 2 * math.pi * k / 5 + rng.uniform(-0.3, 0.3)
        out = np.array([math.cos(a), math.sin(a), 0.0])
        H = rng.uniform(0.65, 1.0)
        bend = rng.uniform(0.15, 0.35)
        base = out * 0.03 + np.array([0, 0, 0.01])
        top = base + out * bend * H + [0, 0, H]
        pts = [base, base + out * bend * 0.15 * H + [0, 0, H * 0.45], base + out * bend * 0.6 * H + [0, 0, H * 0.85], top]
        st = tube('Tallo%d' % k, pts, [0.022, 0.017, 0.012, 0.01], ring=4, samples=4, caps=('flat', 'point'), clay=0.2,
                  paint=pintar_tallo)
        st.emit = brillo_tallo
        b.add(st)
        r = rng.uniform(0.055, 0.08)
        c = top + out * 0.02 + [0, 0, r * 0.6]
        bu = ellipsoid('Bulbo%d' % k, c, (r, r, r * 1.2), axes_from(unit(out * 0.4 + [0, 0, 1])), rings=5, segs=8,
                       clay=0.0, paint=pintar_bulbo)
        bu.emit = brillo_bulbo(np.array(c))
        b.add(bu)
    return b


if __name__ == '__main__':
    run_prop('Alga_Brillante', construir, res=512, smoothness=0.4, ao_distance=0.1, bake_range=1.5, grain=0.02,
             emission=(0.35, 1.45, 1.3))
