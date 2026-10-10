# -*- coding: utf-8 -*-
"""Flores del prado: tres flores (amarilla, blanca y rosa) con dos hojas. Unos 0,4 m."""
import os, sys, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from entorno_kit import *


def pintar_flor(petalo, centro):
    def f(T):
        rgb = grad(T, centro, petalo, smoothstep(0.18, 0.32, T.u))
        rgb = mix(rgb, '#FFFFFF', smoothstep(0.7, 1.0, T.u) * 0.18)
        rgb = mix(rgb, centro, (1 - smoothstep(0.0, 0.12, T.u)) * 0.5)
        return light(T, rgb, 0.06, 0.25)
    return f


def pintar_tallo(T):
    return light(T, grad(T, '#3E6B2C', '#6D9B45', smoothstep(0.0, 1.0, T.u)), 0.1, 0.15)


def construir():
    b = prop('Flores', clay=0.01, seed=15)
    flores = [(0.0, 0.36, '#F2C64A', '#E0832E'), (2.2, 0.28, '#F5F1E6', '#F0C33E'), (4.1, 0.22, '#D98CC2', '#F2C64A')]
    for k, (a, H, pet, cen) in enumerate(flores):
        out = np.array([math.cos(a), math.sin(a), 0.0])
        base = out * 0.03 + np.array([0, 0, -0.02])
        head = out * 0.085 + np.array([0, 0, H])
        mid = out * 0.03 + np.array([0, 0, H * 0.55])
        b.add(tube('Tallo%d' % k, [base, mid, head - [0, 0, 0.01]], 0.008, ring=4, samples=3, caps=('flat', 'flat'),
                   clay=0.2, paint=pintar_tallo))
        nrm = unit(np.array([0, 0, 1.0]) + out * 0.45)
        b.add(star_disc('Flor%d' % k, head, nrm, 0.062 - k * 0.004, petals=5, inner=0.5, thick=0.008, dome=0.35,
                        spin=a, cup=0.18, paint=pintar_flor(pet, cen)))
    for k, a in enumerate((1.1, 3.3)):
        out = np.array([math.cos(a), math.sin(a), 0.0])
        base = np.array([0, 0, -0.01])
        pts = [base, base + out * 0.08 + [0, 0, 0.06], base + out * 0.17 + [0, 0, 0.05]]
        b.add(strap('Hoja%d' % k, pts, [0.012, 0.028, 0.006], 0.005, face=unit(np.array([0, 0, 1.0]) - out * 0.4),
                    ring=4, samples=3, paint=pintar_tallo))
    return b


if __name__ == '__main__':
    run_prop('Flores', construir, res=256, smoothness=0.12, ao_distance=0.06, bake_range=1.0, grain=0.03)
