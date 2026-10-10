# -*- coding: utf-8 -*-
"""Serreta mediana (Mergus serrator, macho) estilo plastilina: pato buceador de cuello estirado,
cabeza verde oscura con cresta despeinada, collar blanco, pecho rojizo moteado, flancos grises,
espalda negra, pico rojo fino con gancho y parche blanco en el ala.
Envergadura de juego ~2.3 m. Lleva el pez en el pico."""
import os, sys
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), '..'))
import numpy as np
import kit
from kit import C, mix, shade, feather_strokes, smoothstep, fbm

ASSET = 'Ave_Serreta'

VERDE = C('#1F3A2E')
VERDE_BRILLO = C('#2F5D46')
BLANCO = C('#EFEEE8')
OXIDO = C('#B26B42')
GRIS = C('#A4A7AA')
NEGRO = C('#1C1D20')
ROJO = C('#C53B2C')
PATA = C('#DA6A3C')


def pintar_cuerpo(T):
    rgb = T.fill(GRIS)
    # vientre blanco
    rgb = mix(rgb, BLANCO, smoothstep(-0.35, -0.75, T.nz))
    # espalda negra
    rgb = mix(rgb, NEGRO, smoothstep(0.35, 0.65, T.nz) * smoothstep(-0.42, -0.30, T.y))
    # pecho rojizo moteado
    pecho = smoothstep(-0.56, -0.50, T.y) * (1 - smoothstep(-0.28, -0.20, T.y)) * (1 - smoothstep(0.25, 0.55, T.nz))
    motas = smoothstep(0.15, 0.45, fbm(T.pos, 30.0, 1, seed=6))
    rgb = mix(rgb, OXIDO * (1 - 0.35 * motas[:, None]), pecho)
    # collar blanco
    collar = smoothstep(-0.63, -0.60, T.y) * (1 - smoothstep(-0.57, -0.54, T.y)) * (1 - smoothstep(0.55, 0.8, T.nz))
    rgb = mix(rgb, BLANCO, collar)
    # cabeza verde oscura con brillo
    cabeza = 1 - smoothstep(-0.64, -0.60, T.y)
    verde = mix(T.fill(VERDE), VERDE_BRILLO, smoothstep(0.2, 0.8, T.nz) * 0.6)
    rgb = rgb * (1 - cabeza[:, None]) + verde * cabeza[:, None]
    # flancos con finas líneas onduladas
    flanco = (1 - smoothstep(0.1, 0.4, np.abs(T.nz))) * smoothstep(-0.25, -0.15, T.y)
    lineas = 0.5 + 0.5 * np.sin(T.z * 160 + 6 * fbm(T.pos, 6.0, 1, seed=2))
    rgb = rgb * (1 - 0.10 * (flanco * lineas)[:, None])
    return shade(T, rgb, top=0.05, bottom=0.10)


def pintar_cresta(T):
    return shade(T, mix(T.fill(VERDE), VERDE_BRILLO, T.u * 0.5), top=0.08, bottom=0.08)


def pintar_ala(T):
    arriba = T.side > -0.5
    rgb = np.where(arriba[:, None], NEGRO[None] * 1.4, C('#D9DADB')[None])
    # parche blanco de las secundarias con dos barras negras
    parche = (1 - smoothstep(0.38, 0.46, T.u)) * smoothstep(0.25, 0.35, T.v)
    barras = smoothstep(0.035, 0.0, np.abs(T.v - 0.55)) + smoothstep(0.035, 0.0, np.abs(T.v - 0.78))
    rgb = mix(rgb, BLANCO, parche * arriba)
    rgb = mix(rgb, NEGRO, parche * np.clip(barras, 0, 1) * arriba)
    rgb = mix(rgb, NEGRO * 1.2, smoothstep(0.55, 0.7, T.u) * arriba)
    rgb = shade(T, rgb, top=0.06, bottom=0.05)
    return feather_strokes(T, rgb, start=0.45, width=0.05, strength=0.3, color=C('#4A4D52'))


def pintar_cola(T):
    rgb = shade(T, T.fill(C('#7D8186')), top=0.06, bottom=0.08)
    return feather_strokes(T, rgb, start=0.45, width=0.06, strength=0.25, both=True)


def pintar_pico(T):
    rgb = mix(T.fill(ROJO), NEGRO, smoothstep(0.6, 1.0, T.nz) * 0.45)
    return shade(T, rgb, top=0.08, bottom=0.10)


def pintar_pata(T):
    return shade(T, T.fill(PATA), top=0.06, bottom=0.14)


def construir():
    b = kit.Bird(ASSET, wing_pivot=(0.13, -0.06, 0.07), feet=(0.010, -1.00, 0.06), clay=0.012, seed=41)

    b.add(kit.loft('cuerpo', [
        (0, -0.79, 0.098, 0.068, 0.062, 0.058),
        (0, -0.71, 0.108, 0.112, 0.105, 0.092),
        (0, -0.61, 0.088, 0.088, 0.082, 0.082),
        (0, -0.47, 0.040, 0.120, 0.098, 0.118),
        (0, -0.29, 0.005, 0.195, 0.128, 0.165),
        (0, -0.02, 0.010, 0.215, 0.128, 0.155),
        (0, 0.24, 0.030, 0.150, 0.088, 0.098),
        (0, 0.38, 0.050, 0.070, 0.040, 0.040),
    ], ring=22, samples=22, caps=('round', 'round'), paint=pintar_cuerpo, uv_weight=1.4))

    # cresta despeinada en la nuca
    bases = [(0.0, -0.69, 0.195), (0.03, -0.665, 0.19), (-0.03, -0.665, 0.19), (0.0, -0.645, 0.185), (0.045, -0.64, 0.165), (-0.045, -0.64, 0.165)]
    puntas = [(0.0, -0.60, 0.27), (0.06, -0.57, 0.25), (-0.06, -0.57, 0.25), (0.0, -0.55, 0.23), (0.09, -0.56, 0.20), (-0.09, -0.56, 0.20)]
    b.add(kit.crest('cresta', bases, puntas, 0.032, paint=pintar_cresta))

    for sx in (1, -1):
        b.add(kit.eye('ojo%s' % ('I' if sx > 0 else 'D'), (0.068 * sx, -0.735, 0.150), 0.068,
                      look=(0.48 * sx, -0.83, 0.26), segs=18))

    b.add(kit.beak('pico', (0, -0.805, 0.092), 0.20, 0.030, 0.040, pitch=-2, hook=0.08, gonys=0.0,
                   lower=0.42, paint_upper=pintar_pico, paint_lower=pintar_pico, tip_round=0.08))

    b.add(kit.tail_fan('cola', (0, 0.35, 0.050), 0.055, 0.17, 0.10, thick=0.016, feathers=5, depth=0.02,
                       roundness=0.02, paint=pintar_cola))

    for sx in (1, -1):
        b.add(kit.leg('pata%s' % sx, [(0.06 * sx, 0.10, -0.12), (0.065 * sx, 0.22, -0.13), (0.065 * sx, 0.32, -0.12)],
                      0.017, paint=pintar_pata))
        b.add(kit.foot_webbed('pie%s' % sx, (0.065 * sx, 0.32, -0.12), (0.0, 1.0, 0.05), 0.13, 0.12, toes=3,
                              thick=0.01, paint=pintar_pata))

    # alas de pato: puntiagudas y algo cortas
    le = [(0.09, -0.16, 0.075), (0.40, -0.19, 0.105), (0.72, -0.10, 0.085), (1.06, 0.10, 0.045)]
    te = [(0.09, 0.15, 0.065), (0.40, 0.17, 0.095), (0.72, 0.15, 0.075), (1.06, 0.14, 0.045)]
    b.add(kit.blade('ala', le, te, [0.040, 0.032, 0.020, 0.008], n=24, chord=6,
                    feathers=[(0.0, 0.5, 8, 0.016), (0.5, 1.0, 6, 0.03)], camber=0.02, te_round=0.35,
                    paint=pintar_ala, uv_weight=1.2, tip_len=0.03), group='wing')
    return b


REFS = [os.path.join(os.environ.get('KIT_REFS', ''), f) for f in ('serreta_mediana/261383.jpg', 'serreta_mediana/317086.jpg')]

if __name__ == '__main__':
    kit.run_bird(ASSET, construir, previas='--sin-previas' not in sys.argv, refs=REFS)
