# -*- coding: utf-8 -*-
"""Rueda de bicicleta (1 m) tirada en el fondo, un poco doblada: cubierta, llanta oxidada, radios y buje."""
import os, sys, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from entorno_kit import *

ZC = 0.045


def pintar_cubierta(T):
    rgb = T.fill(C('#2C2D30'))
    tread = 0.5 + 0.5 * np.sin(np.arctan2(T.y, T.x) * 60)
    rgb = mix(rgb, '#45464A', tread * smoothstep(0.3, 0.8, np.hypot(T.x, T.y) - 0.42 + 0.5) * 0.5)
    rgb = film(T, rgb, '#5C6B47', z0=0.0, z1=0.05, amount=0.5)
    return light(T, rgb, 0.12, 0.2)


def pintar_metal(T):
    rgb = T.fill(C('#A9B1B7'))
    rgb = spots(T, rgb, '#8E5A36', freq=9.0, thresh=0.15, soft=0.2, seed=3, amount=0.7)
    return light(T, rgb, 0.12, 0.2)


def construir():
    b = prop('Rueda_Bici', clay=0.004, seed=41)
    b.add(torus('Cubierta', (0, 0, ZC), 0.46, 0.04, axis=(0, 0, 1), segs=24, sides=6, paint=pintar_cubierta))
    b.add(torus('Llanta', (0, 0, ZC), 0.415, 0.017, axis=(0, 0, 1), segs=24, sides=4, paint=pintar_metal))
    b.add(cyl('Buje', (0, 0, ZC - 0.035), (0, 0, ZC + 0.035), 0.03, segs=8, paint=paint_flat('#5A6168')))
    for k in range(12):
        a = 2 * math.pi * k / 12
        side = 0.014 if k % 2 else -0.014
        p0 = (0.028 * math.cos(a), 0.028 * math.sin(a), ZC + side)
        a1 = a + (0.25 if k % 2 else -0.25)
        p1 = (0.41 * math.cos(a1), 0.41 * math.sin(a1), ZC)
        b.add(tube('Radio%d' % k, [p0, p1], 0.0045, ring=3, samples=2, caps=('flat', 'flat'), clay=0.0,
                   paint=paint_flat('#8E979E')))
    # doblada como un taco
    for p in b.parts:
        p.verts[:, 2] += 0.06 * (p.verts[:, 0] / 0.46) ** 2
    return b


if __name__ == '__main__':
    run_prop('Rueda_Bici', construir, res=512, smoothness=0.3, ao_distance=0.06, bake_range=1.0, grain=0.03)
