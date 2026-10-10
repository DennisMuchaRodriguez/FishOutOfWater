# -*- coding: utf-8 -*-
"""Juncos: diez tallos redondos y finos que salen del agua baja, algunos con su mata de semillas marrón.
Entre 1,3 y 2,1 m."""
import os, sys, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from entorno_kit import *


def pintar_tallo(T):
    rgb = grad(T, '#33502A', '#6C9546', smoothstep(0.0, 0.45, T.u))
    rgb = mix(rgb, '#BDC27C', smoothstep(0.7, 1.0, T.u))
    return light(T, rgb, 0.08, 0.15)


def pintar_semillas(T):
    rgb = grad(T, '#6A4429', '#9C7046', smoothstep(-0.4, 0.6, T.nz))
    return light(T, rgb, 0.1, 0.2)


def construir():
    b = prop('Juncos', clay=0.01, seed=17)
    rng = np.random.RandomState(18)
    n = 10
    for k in range(n):
        a = rng.uniform(0, 2 * math.pi)
        r = 0.22 * math.sqrt(rng.uniform(0.05, 1.0))
        out = np.array([math.cos(a), math.sin(a), 0.0])
        base = out * r + np.array([0, 0, -0.08])
        H = rng.uniform(1.3, 2.1)
        lean = rng.uniform(0.05, 0.22) * H
        side = np.array([-out[1], out[0], 0.0]) * rng.uniform(-0.08, 0.08)
        pts = [base, base + out * lean * 0.15 + side * 0.5 + [0, 0, H * 0.4], base + out * lean * 0.55 + side + [0, 0, H * 0.78],
               base + out * lean + side * 0.6 + [0, 0, H]]
        b.add(tube('Tallo%d' % k, pts, [0.014, 0.012, 0.009, 0.004], ring=5, samples=4, caps=('flat', 'point'),
                   clay=0.2, paint=pintar_tallo))
        if k % 3 == 0:
            p = spline(np.array(pts), 30)[int(30 * 0.82)]
            b.add(ellipsoid('Semillas%d' % k, p + out * 0.02, (0.022, 0.022, 0.06), axes_from(unit(out * 0.6 + [0, 0, 1])),
                            rings=3, segs=5, clay=0.2, paint=pintar_semillas))
    return b


if __name__ == '__main__':
    run_prop('Juncos', construir, res=256, smoothness=0.18, ao_distance=0.15, bake_range=2.5, grain=0.03)
