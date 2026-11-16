# -*- coding: utf-8 -*-
"""Helecho: siete frondas en arco que se abren desde el centro, cada una con su tallo y folíolos a los lados.
Cubre el suelo del bosque y la sombra de los árboles. Unos 0,6 m de alto y 1,2 m de ancho."""
import os, sys, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from entorno_kit import *


def pintar(T):
    rgb = grad(T, '#2C5222', '#4F8A35', smoothstep(0.0, 0.5, T.u))
    rgb = mix(rgb, '#93BE55', smoothstep(0.55, 1.0, T.u) * 0.7)
    # Nervio central más claro
    rgb = strokes(T, rgb, count=1, coord='v', width=0.12, strength=0.3, wobble=0.0, seed=2, color=C('#A8C866'))
    return light(T, rgb, 0.12, 0.2)


def construir():
    b = prop('Helecho', clay=0.008, seed=41)
    rng = np.random.RandomState(42)
    n = 7
    for k in range(n):
        a = 2 * math.pi * k / n + rng.uniform(-0.2, 0.2)
        out = np.array([math.cos(a), math.sin(a), 0.0])
        side = np.array([-out[1], out[0], 0.0])
        L = rng.uniform(0.55, 0.7)
        rise = rng.uniform(0.42, 0.6)

        def arc(t):
            z = rise * math.sin(math.pi * t * 0.85) * (1 - 0.3 * t) - 0.02
            return out * (0.02 + L * t) + np.array([0, 0, z])

        # Tallo (raquis) de la fronda
        b.add(strap('Tallo%d' % k, [arc(t) for t in (0, 0.33, 0.66, 1.0)], [0.012, 0.01, 0.007, 0.003], 0.006,
                    face=(0, 0, 1), ring=4, samples=6, paint=pintar))
        # Folíolos a los dos lados: más largos en el medio, chicos en la base y la punta
        m = 7
        for i in range(m):
            t = 0.18 + 0.78 * i / (m - 1)
            p0 = arc(t)
            tang = arc(min(1.0, t + 0.02)) - arc(max(0.0, t - 0.02))
            tang = tang / max(1e-6, np.linalg.norm(tang))
            size = 0.2 * math.sin(math.pi * min(1.0, t * 1.05)) ** 0.8 + 0.03
            for sgn in (-1, 1):
                d = side * sgn * 0.9 + tang * 0.45 + np.array([0, 0, -0.12])
                d = d / np.linalg.norm(d)
                pts = [p0, p0 + d * size * 0.5 + np.array([0, 0, 0.012]), p0 + d * size]
                b.add(strap('Foliolo%d_%d_%d' % (k, i, sgn + 1), pts, [0.016, 0.022, 0.004], 0.003,
                            face=(0, 0, 1), ring=4, samples=3, caps=('flat', 'point'), paint=pintar))
    return b


if __name__ == '__main__':
    run_prop('Helecho', construir, res=256, smoothness=0.1, ao_distance=0.12, bake_range=1.2, grain=0.03)
