# -*- coding: utf-8 -*-
"""Abeto: siete faldas caídas y estrechas, verde azulado oscuro. Unos 12 m, más esbelto que los pinos."""
import os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from entorno_kit import *

VERDE = '#25503F'
VERDE_CLARO = '#4F8869'
VERDE_OSCURO = '#14302A'
ESCARCHA = '#86AE9B'


def lobulos(R, h, lobes, depth, droop, seed):
    rng = np.random.RandomState(seed)
    ph = rng.uniform(0, 2 * np.pi)
    amp = rng.uniform(0.75, 1.25, lobes)

    def wave(th):
        x = lobes * th + ph
        k = np.floor((x + np.pi) / (2 * np.pi)).astype(int) % lobes
        return ((np.cos(x) + 1) / 2) ** 0.8 * amp[k]

    def rmod(th, t, r, z):
        return 1 + depth * (wave(th) - 0.5) * (r / R) ** 3

    def zmod(th, t, r, z):
        return -droop * h * wave(th) * (r / R) ** 4

    return rmod, zmod


def pintar_falda(R, seed):
    def f(T):
        rad = np.hypot(T.x, T.y) / R
        up = np.clip(T.nz, -1, 1)
        rgb = grad(T, VERDE_OSCURO, VERDE, smoothstep(-0.6, 0.2, up))
        rgb = mix(rgb, VERDE_CLARO, smoothstep(0.2, 0.95, up) * (0.25 + 0.5 * smoothstep(0.5, 1.0, rad)))
        rgb = mix(rgb, ESCARCHA, smoothstep(0.82, 1.0, rad) * smoothstep(0.0, 0.6, up) * 0.35)
        rgb = strokes(T, rgb, count=24, coord='v', width=0.13, strength=0.12, wobble=0.006, seed=seed)
        return light(T, rgb, 0.08, 0.22)
    return f


def pintar_tronco(T):
    rgb = grad(T, '#3A2A20', '#62483A', smoothstep(-0.3, 1.8, T.z))
    rgb = strokes(T, rgb, count=8, coord='v', width=0.22, strength=0.4, wobble=0.01, seed=6)
    return light(T, rgb, 0.1, 0.2)


def falda(name, z0, R, h, lobes, seed):
    prof = [(0, h), (0.25 * R, 0.72 * h), (0.56 * R, 0.4 * h), (0.86 * R, 0.08 * h), (R, -0.14 * h),
            (0.84 * R, -0.22 * h), (0.38 * R, -0.08 * h), (0, 0.0)]
    rmod, zmod = lobulos(R, h, lobes, 0.18, 0.1, seed)
    return revolve(name, prof, lobes * 4, origin=(0, 0, z0), rmod=rmod, zmod=zmod, clay=0.8,
                   paint=pintar_falda(R, seed))


def construir():
    b = prop('Abeto', clay=0.012, seed=7)
    b.add(tube('Tronco', [(0, 0, -0.45), (0, 0, 0.25), (0.02, 0.02, 1.6), (0.03, 0.03, 3.4)],
               [0.4, 0.3, 0.24, 0.16], ring=8, samples=5, caps=('flat', 'round'), paint=pintar_tronco))
    capas = [(1.7, 2.5, 2.3, 7), (3.15, 2.2, 2.2, 7), (4.5, 1.9, 2.1, 6), (5.75, 1.6, 2.0, 6), (6.9, 1.3, 1.9, 5),
             (7.95, 1.0, 1.85, 5), (8.9, 0.68, 2.7, 4)]
    for i, (z0, R, h, lobes) in enumerate(capas):
        b.add(falda('Falda%d' % i, z0, R, h, lobes, seed=40 + i * 5))
    return b


if __name__ == '__main__':
    run_prop('Abeto', construir, res=1024, smoothness=0.12, ao_distance=1.2, bake_range=13.0)
