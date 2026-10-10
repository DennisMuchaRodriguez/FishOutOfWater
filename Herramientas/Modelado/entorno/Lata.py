# -*- coding: utf-8 -*-
"""Lata de refresco (más grande que una real: 0,3 m), azul desteñida con franjas amarilla y blanca, abollada y con
óxido y limo abajo. Basura humana del fondo del lago."""
import os, sys, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from entorno_kit import *


def pintar(T):
    ang = np.arctan2(T.y, T.x)
    rgb = T.fill(C('#3F7FA3'))
    band = smoothstep(0.115, 0.12, T.z) * (1 - smoothstep(0.185, 0.19, T.z))
    rgb = mix(rgb, '#E8B83E', band)
    thin = smoothstep(0.2, 0.204, T.z) * (1 - smoothstep(0.214, 0.218, T.z))
    rgb = mix(rgb, '#EDE6DA', thin)
    dots = (1 - smoothstep(0.012, 0.016, np.hypot((np.mod(ang * 0.0827 / 0.07 + 0.5, 1.0) - 0.5) * 0.07, T.z - 0.152)))
    rgb = mix(rgb, '#EDE6DA', dots * band)
    metal = np.maximum(smoothstep(0.255, 0.265, T.z), 1 - smoothstep(0.022, 0.03, T.z))
    rgb = mix(rgb, '#B8BFC5', metal)
    rgb = spots(T, rgb, '#8A4B2A', freq=22.0, thresh=0.35, soft=0.15, seed=4, amount=0.7)
    rgb = film(T, rgb, '#6E7A4E', z0=0.0, z1=0.09, amount=0.55)
    return light(T, rgb, 0.1, 0.2)


def construir():
    b = prop('Lata', clay=0.01, seed=37)

    def rmod(th, t, r, z):
        d = np.angle(np.exp(1j * (th - 1.0)))
        dent = np.exp(-(d / 0.45) ** 2 - ((z - 0.16) / 0.05) ** 2)
        return 1 - 0.09 * dent * (r > 0.08)

    prof = [(0.0, 0.296), (0.064, 0.3), (0.071, 0.29), (0.082, 0.265), (0.082, 0.16), (0.082, 0.03), (0.074, 0.006),
            (0.05, 0.0), (0.0, 0.006)]
    b.add(revolve('Lata', prof, 16, rmod=rmod, clay=0.2, paint=pintar))
    b.add(ellipsoid('Anilla', (0.028, 0, 0.302), (0.022, 0.012, 0.003), rings=3, segs=6, paint=paint_flat('#AEB5BA')))
    return b


if __name__ == '__main__':
    run_prop('Lata', construir, res=256, smoothness=0.35, ao_distance=0.05, bake_range=1.0, grain=0.03)
