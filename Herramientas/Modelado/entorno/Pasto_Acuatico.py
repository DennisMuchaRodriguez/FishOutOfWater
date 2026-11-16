# -*- coding: utf-8 -*-
"""Pasto acuático (tipo Vallisneria): mata de cintas largas y onduladas que crecen desde el fondo y se tuercen
con la corriente. Forma praderas en el fondo del lago (la vegetación sumergida de la zonación). Unos 1,5 m."""
import os, sys, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from entorno_kit import *


def pintar(T):
    # De la base (oscura, verde azulado) a la punta (clara, verde amarillento), con nervios finos
    rgb = grad(T, '#1F4A3A', '#3E7A4C', smoothstep(0.0, 0.5, T.u))
    rgb = mix(rgb, '#9CC46A', smoothstep(0.55, 1.0, T.u) * 0.75)
    rgb = strokes(T, rgb, count=2, coord='v', width=0.25, strength=0.2, wobble=0.03, seed=6)
    rgb = spots(T, rgb, '#C8B95E', freq=6.0, thresh=0.35, soft=0.15, seed=9, amount=0.25)
    return light(T, rgb, 0.1, 0.15)


def construir():
    b = prop('Pasto_Acuatico', clay=0.008, seed=61)
    rng = np.random.RandomState(62)
    n = 11
    for k in range(n):
        a = 2 * math.pi * k / n + rng.uniform(-0.3, 0.3)
        out = np.array([math.cos(a), math.sin(a), 0.0])
        side = np.array([-out[1], out[0], 0.0])
        H = rng.uniform(0.9, 1.6)
        lean = rng.uniform(0.15, 0.4)
        ph = rng.uniform(0, 2 * math.pi)
        amp = rng.uniform(0.05, 0.12)
        base = out * rng.uniform(0.02, 0.08) + np.array([0, 0, -0.03])
        m = 7
        pts, widths = [], []
        for i in range(m):
            t = i / (m - 1)
            # Sube ondulando hacia los lados (como mecida por el agua)
            wave = side * amp * math.sin(ph + t * 5.0) * t
            pts.append(base + out * lean * t * t + wave + np.array([0, 0, H * t]))
            widths.append(0.03 * (1 - 0.55 * t) + 0.004)
        b.add(strap('Cinta%d' % k, pts, widths, 0.004, face=out, ring=4, samples=m, paint=pintar,
                    twist=rng.uniform(-0.6, 0.6)))
    return b


if __name__ == '__main__':
    run_prop('Pasto_Acuatico', construir, res=256, smoothness=0.3, ao_distance=0.15, bake_range=1.8, grain=0.03)
