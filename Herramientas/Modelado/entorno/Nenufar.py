# -*- coding: utf-8 -*-
"""Nenúfares: cuatro hojas flotantes (rojizas por debajo, se ven desde el agua), una flor rosa y blanca y los
tallos que bajan. El pivote está en la superficie del agua. Unos 1,5 m de ancho."""
import os, sys, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from entorno_kit import *


def pintar_hoja(seed):
    def f(T):
        top = T.side > 0
        rgb = grad(T, '#2F6F33', '#5E9E45', smoothstep(0.0, 1.0, T.u))
        # nervios desde el centro
        rgb = strokes(T, rgb, count=11, coord='v', width=0.12, strength=0.22, wobble=0.004, seed=seed, color='#86BE62')
        rgb = mix(rgb, '#2A5A2C', smoothstep(0.86, 1.0, T.u) * 0.5)
        under = grad(T, '#8C4A4A', '#6F3B3E', smoothstep(0.0, 1.0, T.u))
        rgb = np.where(top[:, None], rgb, under)
        return light(T, rgb, 0.05, 0.1)
    return f


def pintar_petalo(T):
    rgb = grad(T, '#FFF6F2', '#F08DB3', smoothstep(0.35, 1.0, T.u))
    return light(T, rgb, 0.06, 0.15)


def pintar_tallo(T):
    return light(T, grad(T, '#5E6B3A', '#3E4E2E', smoothstep(0.0, 1.0, T.u)), 0.05, 0.1)


def construir():
    b = prop('Nenufar', clay=0.008, seed=21)
    hojas = [((0.0, 0.0), 0.36, 0.3), ((0.52, 0.32), 0.27, 2.1), ((-0.42, 0.42), 0.24, 4.0), ((0.12, -0.55), 0.2, 5.2)]
    for k, ((x, y), R, rot) in enumerate(hojas):
        b.add(pad('Hoja%d' % k, (x, y, 0.0), R, thick=0.026, segs=14, notch=26, rot=rot, wave=0.01, seed=k,
                  paint=pintar_hoja(30 + k)))
        if k < 2:
            b.add(tube('TalloHoja%d' % k, [(x, y, 0.005), (x * 0.8 + 0.05, y * 0.8, -0.5), (x * 0.4, y * 0.4 - 0.05, -1.1)],
                       0.012, ring=4, samples=3, caps=('flat', 'flat'), clay=0.2, paint=pintar_tallo))
    # flor sobre la hoja grande
    c = np.array([0.06, 0.05, 0.03])
    for k in range(8):
        inner = k >= 5
        n = 3 if inner else 5
        a = 2 * math.pi * (k - (5 if inner else 0)) / n + (0.6 if inner else 0.0)
        out = np.array([math.cos(a), math.sin(a), 0.0])
        L = 0.09 if inner else 0.12
        rise = 0.08 if inner else 0.045
        pts = [c, c + out * L * 0.45 + [0, 0, rise * 0.4], c + out * L + [0, 0, rise]]
        face = unit(np.array([0, 0, 1.0]) - out * (1.6 if inner else 0.7))
        b.add(strap('Petalo%d' % k, pts, [0.012, 0.03 if not inner else 0.025, 0.006], 0.004, face=face, ring=4,
                    samples=3, caps=('flat', 'point'), paint=pintar_petalo))
    b.add(ellipsoid('Centro', c + [0, 0, 0.02], (0.025, 0.025, 0.018), rings=3, segs=6, paint=paint_flat('#F2C53D')))
    return b


if __name__ == '__main__':
    run_prop('Nenufar', construir, res=512, smoothness=0.35, ao_distance=0.08, bake_range=1.5, grain=0.03)
