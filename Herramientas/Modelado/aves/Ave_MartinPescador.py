# -*- coding: utf-8 -*-
"""Martín pescador (Alcedo atthis) estilo plastilina: compacto, cabeza enorme, pico de daga negro,
dorso turquesa con raya celeste, pecho naranja, garganta y mancha del cuello blancas, cola corta.
Envergadura de juego ~1.7 m. Lleva el pez en el pico."""
import os, sys
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), '..'))
import numpy as np
import kit
from kit import C, mix, shade, feather_strokes, smoothstep, fbm

ASSET = 'Ave_MartinPescador'

AZUL = C('#2B86B0')
AZUL_OSC = C('#1E5F86')
CELESTE = C('#56D2E6')
NARANJA = C('#D9773A')
NARANJA_CL = C('#E89A5C')
BLANCO = C('#F1EEE4')
NEGRO = C('#1B1C21')
ROJO = C('#D5533A')


def mancha(T, cy, cz, ry, rz, lado=True):
    """Mancha elíptica suave (en el costado de la cabeza si lado=True)."""
    d = ((T.y - cy) / ry) ** 2 + ((T.z - cz) / rz) ** 2
    m = 1 - smoothstep(0.55, 1.0, d)
    if lado:
        m = m * smoothstep(0.05, 0.09, np.abs(T.x))
    return m


def pintar_cuerpo(T):
    rgb = T.fill(NARANJA)
    arriba = smoothstep(-0.10, 0.25, T.nz)
    rgb = mix(rgb, AZUL, arriba)
    # pintas claras en la corona
    corona = (T.y < -0.28) * smoothstep(0.3, 0.6, T.nz)
    pintas = smoothstep(0.45, 0.62, fbm(T.pos, 22.0, 1, seed=4))
    rgb = mix(rgb, CELESTE, corona * pintas * 0.9)
    # raya celeste en la espalda (suave)
    raya = (1 - smoothstep(0.015, 0.045, np.abs(T.x))) * smoothstep(0.6, 0.85, T.nz) * smoothstep(-0.20, -0.10, T.y) * (1 - smoothstep(0.06, 0.16, T.y))
    rgb = mix(rgb, CELESTE, raya * 0.75)
    # mejilla naranja detrás del ojo, bigotera azul, garganta y mancha del cuello blancas
    rgb = mix(rgb, NARANJA_CL, mancha(T, -0.385, 0.115, 0.06, 0.035))
    rgb = mix(rgb, AZUL_OSC, mancha(T, -0.41, 0.060, 0.05, 0.018))
    rgb = mix(rgb, BLANCO, mancha(T, -0.47, 0.035, 0.07, 0.05, lado=False) * smoothstep(-0.2, 0.2, -T.nz + 0.3))
    rgb = mix(rgb, BLANCO, mancha(T, -0.285, 0.095, 0.035, 0.035))
    return shade(T, rgb, top=0.06, bottom=0.12)


def pintar_ala(T):
    arriba = T.side > -0.5
    rgb = np.where(arriba[:, None], AZUL[None], NARANJA_CL[None] * 0.92)
    rgb = mix(rgb, AZUL_OSC, smoothstep(0.45, 0.65, T.u) * arriba)
    rgb = mix(rgb, AZUL_OSC * 0.85, smoothstep(0.55, 0.85, T.v) * arriba)
    pintas = smoothstep(0.5, 0.65, fbm(T.pos, 26.0, 1, seed=8)) * (1 - smoothstep(0.35, 0.5, T.u)) * (T.v < 0.5)
    rgb = mix(rgb, CELESTE, pintas * arriba * 0.8)
    rgb = shade(T, rgb, top=0.05, bottom=0.06)
    return feather_strokes(T, rgb, start=0.4, width=0.06, strength=0.32)


def pintar_cola(T):
    rgb = shade(T, T.fill(AZUL_OSC), top=0.08, bottom=0.08)
    return feather_strokes(T, rgb, start=0.5, width=0.07, strength=0.3, both=True)


def pintar_pico(T):
    rgb = T.fill(NEGRO)
    return shade(T, rgb + 0.06, top=0.10, bottom=0.05)


def pintar_pata(T):
    return shade(T, T.fill(ROJO), top=0.06, bottom=0.14)


def construir():
    b = kit.Bird(ASSET, wing_pivot=(0.11, -0.04, 0.08), feet=(0.012, -0.85, 0.095), clay=0.014, seed=21)

    b.add(kit.loft('cuerpo', [
        (0, -0.50, 0.105, 0.100, 0.090, 0.085),
        (0, -0.43, 0.115, 0.170, 0.160, 0.140),
        (0, -0.31, 0.100, 0.175, 0.155, 0.150),
        (0, -0.14, 0.050, 0.180, 0.130, 0.165),
        (0, 0.04, 0.040, 0.150, 0.110, 0.130),
        (0, 0.17, 0.055, 0.075, 0.050, 0.050),
    ], ring=22, samples=20, caps=('round', 'round'), paint=pintar_cuerpo, uv_weight=1.6))

    for sx in (1, -1):
        b.add(kit.eye('ojo%s' % ('I' if sx > 0 else 'D'), (0.098 * sx, -0.45, 0.165), 0.080,
                      look=(0.50 * sx, -0.82, 0.26), segs=20))

    b.add(kit.beak('pico', (0, -0.545, 0.100), 0.31, 0.046, 0.062, pitch=-2, hook=0.0, gonys=0.0,
                   lower=0.46, lower_len=0.97, paint_upper=pintar_pico, paint_lower=pintar_pico, tip_round=0.05))

    b.add(kit.tail_fan('cola', (0, 0.14, 0.060), 0.050, 0.13, 0.070, thick=0.018, feathers=5, depth=0.02,
                       roundness=0.025, paint=pintar_cola))

    for sx in (1, -1):
        b.add(kit.leg('pata%s' % sx, [(0.05 * sx, 0.00, -0.12), (0.05 * sx, 0.07, -0.14), (0.05 * sx, 0.12, -0.135)],
                      0.016, paint=pintar_pata))
        b.add(kit.foot_webbed('pie%s' % sx, (0.05 * sx, 0.12, -0.135), (0.0, 1.0, 0.0), 0.07, 0.06, toes=3,
                              thick=0.009, paint=pintar_pata))

    le = [(0.08, -0.13, 0.085), (0.34, -0.16, 0.115), (0.60, -0.09, 0.085), (0.82, 0.04, 0.05)]
    te = [(0.08, 0.11, 0.075), (0.34, 0.15, 0.10), (0.60, 0.14, 0.08), (0.82, 0.09, 0.05)]
    b.add(kit.blade('ala', le, te, [0.045, 0.036, 0.022, 0.010], n=22, chord=6,
                    feathers=[(0.0, 0.45, 6, 0.018), (0.45, 1.0, 6, 0.035)], camber=0.02, te_round=0.4,
                    paint=pintar_ala, uv_weight=1.2, tip_len=0.04), group='wing')
    return b


REFS = [os.path.join(os.environ.get('KIT_REFS', ''), f) for f in ('martin/2218248.jpg', 'martin/1366438.jpg')]

if __name__ == '__main__':
    kit.run_bird(ASSET, construir, previas='--sin-previas' not in sys.argv, refs=REFS)
