# -*- coding: utf-8 -*-
"""Gaviota patiamarilla (Larus michahellis) estilo plastilina.
Ejecutar:  <python con bpy> Herramientas/Modelado/aves/Ave_Gaviota.py [--sin-previas]
Ejes Blender: pico hacia -Y, arriba +Z, ala izquierda en +X. Envergadura ~2.6 m."""
import os, sys
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), '..'))
import numpy as np
import kit
from kit import C, mix, shade, feather_strokes, smoothstep

ASSET = 'Ave_Gaviota'

BLANCO = C('#F2F1EC')
GRIS = C('#919CA9')
GRIS_OSC = C('#7F8A97')
NEGRO = C('#1D1F24')
AMARILLO = C('#EFC23C')
ROJO = C('#D23A2C')
PATA = C('#E6BF45')


def pintar_cuerpo(T):
    rgb = T.fill(BLANCO)
    # manto gris en la espalda (entre el cuello y la cola)
    espalda = smoothstep(0.05, 0.45, T.nz) * smoothstep(-0.34, -0.16, T.y) * (1 - smoothstep(0.30, 0.44, T.y))
    rgb = mix(rgb, GRIS, espalda)
    return shade(T, rgb, top=0.05, bottom=0.10)


def pintar_cola(T):
    rgb = shade(T, T.fill(BLANCO), top=0.04, bottom=0.08)
    return feather_strokes(T, rgb, start=0.55, width=0.06, strength=0.18, both=True)


def pintar_ala(T):
    arriba = T.side > -0.5
    rgb = np.where(arriba[:, None], GRIS[None], C('#E9ECEF')[None])
    # borde de salida blanco (arriba)
    borde = smoothstep(0.80, 0.93, T.v) * (1 - smoothstep(0.74, 0.80, T.u))
    rgb = mix(rgb, BLANCO, borde * arriba)
    # puntas negras con "espejos" blancos
    punta = smoothstep(0.70, 0.78, T.u)
    rgb = mix(rgb, NEGRO, punta)
    espejo = np.exp(-(((T.u - 0.93) / 0.035) ** 2 + ((T.v - 0.55) / 0.22) ** 2))
    espejo2 = np.exp(-(((T.u - 0.84) / 0.025) ** 2 + ((T.v - 0.70) / 0.14) ** 2))
    rgb = mix(rgb, BLANCO, np.clip(espejo * 1.4, 0, 1) * 0.95)
    rgb = mix(rgb, BLANCO, np.clip(espejo2 * 1.3, 0, 1) * 0.85)
    rgb = shade(T, rgb, top=0.05, bottom=0.06)
    # trazos de pluma (como los radios de las aletas del pez)
    rgb = feather_strokes(T, rgb, start=0.45, width=0.055, strength=0.30)
    rgb = feather_strokes(T, rgb, start=0.55, width=0.05, strength=0.12, both=True)
    return rgb


def pintar_pico_sup(T):
    rgb = T.fill(AMARILLO)
    return shade(T, rgb, top=0.08, bottom=0.10)


def pintar_pico_inf(T):
    rgb = T.fill(AMARILLO)
    mancha = smoothstep(0.55, 0.68, T.u) * (1 - smoothstep(0.88, 0.97, T.u))
    rgb = mix(rgb, ROJO, mancha)
    return shade(T, rgb, top=0.06, bottom=0.12)


def pintar_pata(T):
    return shade(T, T.fill(PATA), top=0.06, bottom=0.14)


def construir():
    # feet = punto de captura: la gaviota lleva el pez en el pico (LeftFoot/RightFoot en la punta)
    b = kit.Bird(ASSET, wing_pivot=(0.15, -0.04, 0.10), feet=(0.014, -0.99, 0.095), clay=0.012, seed=11)

    # Cuerpo: cabeza grande y redonda, cuello corto, pecho lleno y cola fina (una sola pieza suave)
    cuerpo = kit.loft('cuerpo', [
        (0, -0.70, 0.13, 0.090, 0.085, 0.080),
        (0, -0.62, 0.15, 0.165, 0.150, 0.130),
        (0, -0.50, 0.14, 0.165, 0.150, 0.135),
        (0, -0.38, 0.09, 0.150, 0.120, 0.150),
        (0, -0.18, 0.03, 0.235, 0.165, 0.215),
        (0, 0.08, 0.02, 0.235, 0.160, 0.205),
        (0, 0.30, 0.04, 0.170, 0.115, 0.125),
        (0, 0.46, 0.07, 0.085, 0.050, 0.050),
    ], ring=22, samples=22, caps=('round', 'round'), paint=pintar_cuerpo, uv_weight=1.4)
    b.add(cuerpo)

    # Ojos saltones (como los del pez)
    for sx in (1, -1):
        b.add(kit.eye('ojo%s' % ('I' if sx > 0 else 'D'), (0.092 * sx, -0.672, 0.215), 0.088,
                      look=(0.48 * sx, -0.84, 0.26), segs=20), group='body')

    # Pico amarillo con gancho y mancha roja
    b.add(kit.beak('pico', (0, -0.775, 0.125), 0.23, 0.050, 0.072, pitch=-6, hook=0.10, gonys=0.30,
                   lower=0.42, paint_upper=pintar_pico_sup, paint_lower=pintar_pico_inf))

    # Cola en abanico
    b.add(kit.tail_fan('cola', (0, 0.40, 0.065), 0.075, 0.24, 0.150, thick=0.022, feathers=6, depth=0.03,
                       roundness=0.035, paint=pintar_cola))

    # Patas recogidas hacia atrás con pies palmeados
    for sx in (1, -1):
        b.add(kit.leg('pata%s' % sx, [(0.07 * sx, 0.12, -0.13), (0.075 * sx, 0.26, -0.15), (0.075 * sx, 0.38, -0.14)],
                      0.022, paint=pintar_pata))
        b.add(kit.foot_webbed('pie%s' % sx, (0.075 * sx, 0.38, -0.14), (0.0, 1.0, 0.05), 0.15, 0.14, toes=3,
                              thick=0.012, paint=pintar_pata))

    # Ala izquierda (+X): brazo algo levantado y mano hacia atrás y abajo (la "M" de la gaviota)
    le = [(0.10, -0.16, 0.11), (0.42, -0.21, 0.18), (0.74, -0.15, 0.17), (1.02, 0.02, 0.10), (1.30, 0.22, 0.04)]
    te = [(0.10, 0.20, 0.09), (0.42, 0.22, 0.15), (0.74, 0.22, 0.14), (1.00, 0.22, 0.08), (1.30, 0.25, 0.04)]
    ala = kit.blade('ala', le, te, [0.050, 0.040, 0.030, 0.018, 0.008], n=26, chord=6,
                    feathers=[(0.0, 0.55, 9, 0.022), (0.55, 1.0, 7, 0.045)], camber=0.025, te_round=0.35,
                    paint=pintar_ala, uv_weight=1.2, tip_len=0.03)
    b.add(ala, group='wing')
    return b


REFS = [os.path.join(os.environ.get('KIT_REFS', ''), f) for f in ('gaviota_patiamarilla/12831.jpg', 'gaviota_argentea/22063.jpg')]

if __name__ == '__main__':
    kit.run_bird(ASSET, construir, previas='--sin-previas' not in sys.argv, refs=REFS)
