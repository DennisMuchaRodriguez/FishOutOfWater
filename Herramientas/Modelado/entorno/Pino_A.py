# -*- coding: utf-8 -*-
"""Pino A: cinco copas en capas redondeadas (como faldas festoneadas) sobre un tronco grueso. Unos 10,8 m."""
import os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from entorno_kit import *

VERDE = '#2F6A3A'
VERDE_CLARO = '#5E9C50'
VERDE_OSCURO = '#1C3F2A'
CALIDO = '#7FA352'


def lobulos(R, h, lobes, depth, droop, seed):
    """Festón del borde: lóbulos redondeados de distinto tamaño que caen un poco."""
    rng = np.random.RandomState(seed)
    ph = rng.uniform(0, 2 * np.pi)
    amp = rng.uniform(0.7, 1.3, lobes)

    def wave(th):
        x = lobes * th + ph
        k = np.floor((x + np.pi) / (2 * np.pi)).astype(int) % lobes
        return ((np.cos(x) + 1) / 2) ** 0.7 * amp[k]

    def rmod(th, t, r, z):
        return 1 + depth * (wave(th) - 0.5) * (r / R) ** 3

    def zmod(th, t, r, z):
        return -droop * h * wave(th) * (r / R) ** 4

    return rmod, zmod


def pintar_copa(R, lobes, seed):
    def f(T):
        rad = np.hypot(T.x, T.y) / R
        up = np.clip(T.nz, -1, 1)
        rgb = grad(T, VERDE_OSCURO, VERDE, smoothstep(-0.55, 0.25, up))
        rgb = mix(rgb, VERDE_CLARO, smoothstep(0.15, 0.95, up) * (0.25 + 0.55 * smoothstep(0.45, 1.0, rad)))
        rgb = strokes(T, rgb, count=lobes * 3, coord='v', width=0.14, strength=0.14, wobble=0.006, seed=seed)
        rgb = spots(T, rgb, CALIDO, freq=0.9, thresh=0.1, soft=0.45, seed=seed + 5, amount=0.22)
        return light(T, rgb, 0.08, 0.2)
    return f


def pintar_tronco(T):
    rgb = grad(T, '#45291B', '#7A5236', smoothstep(-0.3, 2.2, T.z))
    rgb = strokes(T, rgb, count=9, coord='v', width=0.22, strength=0.4, wobble=0.01, seed=2)
    rgb = spots(T, rgb, '#94704C', freq=2.5, thresh=0.3, soft=0.2, amount=0.35, seed=9)
    return light(T, rgb, 0.1, 0.2)


def copa(name, z0, R, h, lobes, seed, segs=None):
    prof = [(0, h), (0.3 * R, 0.75 * h), (0.65 * R, 0.38 * h), (0.92 * R, 0.08 * h), (R, -0.04 * h),
            (0.8 * R, -0.13 * h), (0.35 * R, -0.06 * h), (0, 0.0)]
    rmod, zmod = lobulos(R, h, lobes, 0.3, 0.14, seed)
    return revolve(name, prof, segs or lobes * 4, origin=(0, 0, z0), rmod=rmod, zmod=zmod, clay=0.8,
                   paint=pintar_copa(R, lobes, seed))


def construir():
    b = prop('Pino_A', clay=0.012, seed=3)
    b.add(tube('Tronco', [(0, 0, -0.45), (0, 0, 0.25), (0.03, 0.0, 1.8), (0.06, 0.02, 4.0)],
               [0.52, 0.38, 0.3, 0.2], ring=8, samples=5, caps=('flat', 'round'), paint=pintar_tronco))
    capas = [(2.0, 3.0, 2.9, 6), (3.7, 2.5, 2.7, 6), (5.3, 2.0, 2.5, 5), (6.8, 1.5, 2.3, 4), (8.2, 0.95, 2.4, 4)]
    for i, (z0, R, h, lobes) in enumerate(capas):
        b.add(copa('Copa%d' % i, z0, R, h, lobes, seed=11 + i * 7, segs=lobes * 5))
    return b


if __name__ == '__main__':
    run_prop('Pino_A', construir, res=1024, smoothness=0.12, ao_distance=1.2, bake_range=12.0)
