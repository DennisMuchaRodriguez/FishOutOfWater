# -*- coding: utf-8 -*-
"""Tronco hundido: tronco empapado de 5 m tumbado en el fondo, con un extremo roto en astillas, dos muñones
de rama y manchas de algas y limo."""
import os, sys, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from entorno_kit import *

R0 = 0.4
ZC = 0.36


def pintar_corteza(T):
    rgb = grad(T, '#2E261C', '#4D3F2D', smoothstep(-0.8, 0.4, T.nz))
    rgb = strokes(T, rgb, count=14, coord='v', width=0.2, strength=0.45, wobble=0.01, seed=3)
    rgb = spots(T, rgb, '#3F6E50', freq=1.6, thresh=0.1, soft=0.3, seed=8, amount=0.55)
    rgb = mix(rgb, '#8C8770', smoothstep(0.35, 0.9, T.nz) * smoothstep(-0.2, 0.3, nse(T, 1.2, 9)) * 0.6)
    # extremo cortado (-X): anillos de la madera
    end = smoothstep(0.7, 0.9, -T.nrm[:, 0]) * (T.x < -2.3)
    rad = np.hypot(T.y, T.z - ZC)
    rings = 0.5 + 0.5 * np.sin(rad * 55 + 2 * nse(T, 6, 2))
    wood = mix(grad(T, '#8A7350', '#A68B60', rings), '#5A4632', smoothstep(R0 * 0.8, R0 * 0.95, rad))
    rgb = rgb * (1 - end[:, None]) + wood * end[:, None]
    return light(T, rgb, 0.06, 0.2)


def pintar_astilla(T):
    rgb = grad(T, '#5A4A35', '#9C8862', smoothstep(0.2, 1.0, T.u))
    return light(T, rgb, 0.06, 0.2)


def construir():
    b = prop('Tronco_Hundido', clay=0.012, seed=35)
    L = 5.0

    def rmod(th, t, r, z):
        return 1 + 0.05 * np.sin(5 * th + 3 * z) + 0.03 * np.sin(11 * th + 1.3 * z)

    prof = [(0.33, L), (0.38, L - 0.07), (0.41, 4.2), (0.4, 2.6), (0.42, 1.2), (0.39, 0.08), (0.34, 0.0)]
    b.add(revolve('Tronco', prof, 12, origin=(-L / 2, 0, ZC), axes=axes_from((1, 0, 0)), rmod=rmod, clay=0.6,
                  paint=pintar_corteza))
    rng = np.random.RandomState(36)
    for k in range(5):
        a = 2 * math.pi * k / 5 + rng.uniform(-0.3, 0.3)
        off = np.array([0.0, math.cos(a), math.sin(a)]) * R0 * 0.55
        base = np.array([L / 2 - 0.08, 0, ZC]) + off
        tip = base + np.array([rng.uniform(0.2, 0.42), 0, 0]) + off * 0.25
        b.add(tube('Astilla%d' % k, [base, (base + tip) / 2, tip], [0.1, 0.07, 0.02], ring=4, samples=3,
                   caps=('flat', 'point'), clay=0.3, paint=pintar_astilla))
    for k, (x, ang) in enumerate(((-0.9, 60), (1.1, -35))):
        d = np.array([0.0, math.cos(math.radians(ang)), math.sin(math.radians(ang)) + 0.4])
        d = unit(d)
        base = np.array([x, 0, ZC]) + d * R0 * 0.5
        b.add(tube('Rama%d' % k, [base, base + d * 0.5 + [0.08, 0, 0], base + d * 0.95 + [0.18, 0, 0]],
                   [0.13, 0.1, 0.075], ring=8, samples=3, caps=('flat', 'flat'), clay=0.4, paint=pintar_corteza))
    return b


if __name__ == '__main__':
    run_prop('Tronco_Hundido', construir, res=512, smoothness=0.3, ao_distance=0.4, bake_range=3.0)
