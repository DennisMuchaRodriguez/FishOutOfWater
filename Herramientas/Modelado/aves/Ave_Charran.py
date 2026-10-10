# -*- coding: utf-8 -*-
"""Charrán común (Sterna hirundo) estilo plastilina: esbelto y blanco, capirote negro, alas largas y
puntiagudas gris perla, pico rojo con punta negra, cola en horquilla y patas rojas cortas.
Envergadura de juego ~2.2 m. Lleva el pez en el pico."""
import os, sys
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), '..'))
import numpy as np
import kit
from kit import C, mix, shade, feather_strokes, smoothstep

ASSET = 'Ave_Charran'

BLANCO = C('#F3F2EE')
PERLA = C('#A7B1BD')
GRIS_OSC = C('#6C7581')
NEGRO = C('#17181C')
ROJO = C('#D9432F')


def pintar_cuerpo(T):
    rgb = T.fill(BLANCO)
    # dorso gris perla
    dorso = smoothstep(0.15, 0.55, T.nz) * smoothstep(-0.26, -0.14, T.y) * (1 - smoothstep(0.20, 0.30, T.y))
    rgb = mix(rgb, PERLA, dorso)
    # capirote negro: de la frente a la nuca, hasta la altura del ojo
    cap = smoothstep(-0.54, -0.50, T.y) * (1 - smoothstep(-0.30, -0.26, T.y)) * smoothstep(0.115, 0.135, T.z)
    rgb = mix(rgb, NEGRO, cap)
    return shade(T, rgb, top=0.05, bottom=0.10)


def pintar_ala(T):
    arriba = T.side > -0.5
    rgb = np.where(arriba[:, None], PERLA[None], C('#EEF0F2')[None])
    # primarias exteriores más oscuras en el borde de ataque de la mano
    oscuro = smoothstep(0.62, 0.80, T.u) * (1 - smoothstep(0.30, 0.55, T.v))
    rgb = mix(rgb, GRIS_OSC, oscuro * 0.8)
    rgb = mix(rgb, BLANCO, smoothstep(0.82, 0.95, T.v) * (1 - smoothstep(0.6, 0.7, T.u)) * arriba)
    rgb = shade(T, rgb, top=0.05, bottom=0.05)
    return feather_strokes(T, rgb, start=0.45, width=0.05, strength=0.25)


def pintar_cola(T):
    rgb = mix(T.fill(BLANCO), PERLA, smoothstep(0.0, 0.4, np.abs(T.x) * 4) * 0.5)
    rgb = shade(T, rgb, top=0.04, bottom=0.08)
    return feather_strokes(T, rgb, start=0.4, width=0.06, strength=0.2, both=True)


def pintar_pico(T):
    rgb = mix(T.fill(ROJO), NEGRO, smoothstep(0.72, 0.82, T.u))
    return shade(T, rgb, top=0.08, bottom=0.10)


def pintar_pata(T):
    return shade(T, T.fill(ROJO), top=0.06, bottom=0.14)


def construir():
    b = kit.Bird(ASSET, wing_pivot=(0.10, -0.05, 0.09), feet=(0.010, -0.75, 0.08), clay=0.012, seed=31)

    b.add(kit.loft('cuerpo', [
        (0, -0.53, 0.100, 0.070, 0.062, 0.058),
        (0, -0.46, 0.112, 0.120, 0.112, 0.098),
        (0, -0.36, 0.092, 0.112, 0.098, 0.098),
        (0, -0.23, 0.050, 0.118, 0.090, 0.108),
        (0, -0.02, 0.030, 0.155, 0.108, 0.135),
        (0, 0.18, 0.040, 0.115, 0.078, 0.088),
        (0, 0.32, 0.058, 0.058, 0.038, 0.038),
    ], ring=20, samples=20, caps=('round', 'round'), paint=pintar_cuerpo, uv_weight=1.4))

    for sx in (1, -1):
        b.add(kit.eye('ojo%s' % ('I' if sx > 0 else 'D'), (0.072 * sx, -0.475, 0.150), 0.070,
                      look=(0.48 * sx, -0.83, 0.26), segs=18))

    b.add(kit.beak('pico', (0, -0.565, 0.098), 0.20, 0.033, 0.046, pitch=-4, hook=0.02, gonys=0.06,
                   lower=0.44, paint_upper=pintar_pico, paint_lower=pintar_pico, tip_round=0.06))

    # cola en horquilla con dos "serpentinas"
    b.add(kit.tail_fan('cola', (0, 0.29, 0.058), 0.050, 0.30, 0.12, thick=0.014, feathers=4, depth=0.018,
                       roundness=0.02, fork=0.21, paint=pintar_cola))

    for sx in (1, -1):
        b.add(kit.leg('pata%s' % sx, [(0.04 * sx, 0.06, -0.10), (0.04 * sx, 0.13, -0.115), (0.04 * sx, 0.18, -0.11)],
                      0.013, paint=pintar_pata))
        b.add(kit.foot_webbed('pie%s' % sx, (0.04 * sx, 0.18, -0.11), (0.0, 1.0, 0.03), 0.07, 0.065, toes=3,
                              thick=0.008, paint=pintar_pata))

    # alas largas, estrechas y puntiagudas, con un leve quiebre en la muñeca
    le = [(0.07, -0.14, 0.095), (0.38, -0.18, 0.135), (0.68, -0.11, 0.125), (0.92, 0.06, 0.085), (1.10, 0.22, 0.06)]
    te = [(0.07, 0.10, 0.085), (0.38, 0.11, 0.12), (0.68, 0.11, 0.11), (0.92, 0.17, 0.08), (1.10, 0.235, 0.06)]
    b.add(kit.blade('ala', le, te, [0.036, 0.030, 0.022, 0.012, 0.005], n=26, chord=6,
                    feathers=[(0.0, 0.55, 8, 0.016), (0.55, 1.0, 6, 0.03)], camber=0.02, te_round=0.35,
                    paint=pintar_ala, uv_weight=1.2, tip_len=0.03), group='wing')
    return b


REFS = [os.path.join(os.environ.get('KIT_REFS', ''), f) for f in ('charran/1122638.jpg', 'charran/2009566.jpg')]

if __name__ == '__main__':
    kit.run_bird(ASSET, construir, previas='--sin-previas' not in sys.argv, refs=REFS)
