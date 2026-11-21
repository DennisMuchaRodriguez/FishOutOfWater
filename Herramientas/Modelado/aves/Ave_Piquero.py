# -*- coding: utf-8 -*-
"""Piquero de patas azules (Sula nebouxii) estilo plastilina: el ave KAMIKAZE del juego. En la vida real pesca
lanzándose al agua en picada como una flecha; aquí se lanza así contra el pez del traje.
Cuerpo de torpedo blanco, cabeza y cuello con rayitas pardas, dorso y alas largas y puntiagudas pardo chocolate,
pico largo y recto gris azulado, ojos mirando al frente con cejas de enfado, gafas de aviador en la frente y las
famosas patas azules. Envergadura de juego ~2 m."""
import os, sys
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), '..'))
import numpy as np
import kit
from kit import C, mix, shade, feather_strokes, smoothstep

ASSET = 'Ave_Piquero'

BLANCO = C('#F4F1EA')
CREMA = C('#E6D9C6')
PARDO = C('#6E4C34')
PARDO_OSC = C('#4A3223')
PICO = C('#7F9CB0')
PICO_OSC = C('#4F6878')
AZUL = C('#36B6E8')
AZUL_OSC = C('#1F86B8')
CUERO = C('#8A5A2B')
LENTE = C('#A8DDF2')


def pintar_cuerpo(T):
    rgb = T.fill(BLANCO)
    # cabeza y cuello: crema con rayitas pardas finas
    cabeza = 1 - smoothstep(-0.36, -0.26, T.y)
    rayas = (0.5 + 0.5 * np.sin(T.x * 140.0 + T.z * 60.0 + T.y * 25.0)) ** 6
    rgb = mix(rgb, CREMA, cabeza * 0.85)
    rgb = mix(rgb, PARDO, cabeza * rayas * 0.45)
    # dorso pardo (de la nuca a la cola), el vientre queda blanco
    dorso = smoothstep(0.05, 0.45, T.nz) * smoothstep(-0.32, -0.20, T.y)
    rgb = mix(rgb, PARDO, dorso)
    return shade(T, rgb, top=0.05, bottom=0.10)


def pintar_ala(T):
    arriba = T.side > -0.5
    rgb = np.where(arriba[:, None], PARDO[None], C('#C9B8A2')[None])
    # puntas más oscuras
    rgb = mix(rgb, PARDO_OSC, smoothstep(0.60, 0.85, T.u) * 0.7)
    rgb = shade(T, rgb, top=0.05, bottom=0.05)
    return feather_strokes(T, rgb, start=0.45, width=0.05, strength=0.28, color=C('#3A281C'))


def pintar_cola(T):
    rgb = shade(T, T.fill(PARDO), top=0.05, bottom=0.08)
    return feather_strokes(T, rgb, start=0.4, width=0.06, strength=0.25, both=True, color=C('#3A281C'))


def pintar_pico(T):
    rgb = mix(T.fill(PICO), PICO_OSC, smoothstep(0.65, 0.95, T.u) * 0.6)
    return shade(T, rgb, top=0.10, bottom=0.10)


def pintar_pata(T):
    rgb = mix(T.fill(AZUL), AZUL_OSC, smoothstep(-0.2, -0.8, T.nz) * 0.4)
    return shade(T, rgb, top=0.12, bottom=0.12)


def pintar_ceja(T):
    return shade(T, T.fill(PARDO_OSC), top=0.08, bottom=0.08)


def pintar_marco(T):
    return shade(T, mix(T.fill(CUERO), C('#C9944E'), smoothstep(0.4, 1.0, T.nz) * 0.4), top=0.12, bottom=0.10)


def pintar_lente(T):
    return shade(T, mix(T.fill(LENTE), C('#FFFFFF'), smoothstep(0.6, 1.0, T.nz) * 0.6), top=0.2, bottom=0.05)


def construir():
    # feet: punto de agarre en la punta del pico (no caza peces, pero el juego lo espera)
    b = kit.Bird(ASSET, wing_pivot=(0.10, -0.05, 0.09), feet=(0.0, -0.84, 0.085), clay=0.012, seed=73)

    # cuerpo de torpedo: cabeza y cuello gruesos, sin "cuello de garza"
    b.add(kit.loft('cuerpo', [
        (0, -0.58, 0.092, 0.072, 0.064, 0.058),
        (0, -0.51, 0.104, 0.122, 0.112, 0.100),
        (0, -0.40, 0.084, 0.114, 0.098, 0.102),
        (0, -0.26, 0.050, 0.134, 0.100, 0.118),
        (0, -0.04, 0.032, 0.168, 0.116, 0.142),
        (0, 0.18, 0.040, 0.120, 0.082, 0.092),
        (0, 0.33, 0.056, 0.056, 0.036, 0.036),
    ], ring=20, samples=22, caps=('round', 'round'), paint=pintar_cuerpo, uv_weight=1.4))

    # ojos al frente (los piqueros miran hacia adelante, como un piloto) y cejas de enfado
    for sx in (1, -1):
        lado = 'I' if sx > 0 else 'D'
        b.add(kit.eye('ojo%s' % lado, (0.058 * sx, -0.540, 0.140), 0.064, look=(0.30 * sx, -0.93, 0.20), segs=18))
        b.add(kit.brow('ceja%s' % lado, (0.022 * sx, -0.585, 0.184), (0.112 * sx, -0.515, 0.206), 0.020,
                       paint=pintar_ceja))

    # gafas de aviador subidas en la frente
    b.add(kit.glasses('gafas', (0.050, -0.505, 0.212), (-0.050, -0.505, 0.212), 0.036, rim=0.16,
                      look=(0.0, -0.55, 0.84), paint_frame=pintar_marco, paint_lens=pintar_lente))

    # pico largo, recto y en punta (como un dardo)
    b.add(kit.beak('pico', (0, -0.615, 0.086), 0.24, 0.036, 0.046, pitch=-3, hook=0.0, gonys=0.02,
                   lower=0.45, paint_upper=pintar_pico, paint_lower=pintar_pico, tip_round=0.03))

    # cola corta en cuña
    b.add(kit.tail_fan('cola', (0, 0.30, 0.056), 0.050, 0.26, 0.10, thick=0.014, feathers=5, depth=0.02,
                       roundness=0.0, paint=pintar_cola))

    # patas azules con pies palmeados grandes (lo más famoso del piquero)
    for sx in (1, -1):
        b.add(kit.leg('pata%s' % sx, [(0.045 * sx, 0.06, -0.11), (0.05 * sx, 0.14, -0.125), (0.05 * sx, 0.20, -0.115)],
                      0.017, paint=pintar_pata))
        b.add(kit.foot_webbed('pie%s' % sx, (0.05 * sx, 0.20, -0.115), (0.0, 1.0, 0.03), 0.10, 0.095, toes=3,
                              thick=0.010, paint=pintar_pata))

    # alas largas, estrechas y puntiagudas (vuelo rápido, pliega las alas para la picada)
    le = [(0.07, -0.13, 0.090), (0.36, -0.16, 0.120), (0.64, -0.10, 0.110), (0.86, 0.05, 0.080), (1.00, 0.20, 0.060)]
    te = [(0.07, 0.10, 0.080), (0.36, 0.10, 0.110), (0.64, 0.10, 0.100), (0.86, 0.16, 0.075), (1.00, 0.215, 0.060)]
    b.add(kit.blade('ala', le, te, [0.036, 0.030, 0.022, 0.012, 0.005], n=26, chord=6,
                    feathers=[(0.0, 0.55, 8, 0.016), (0.55, 1.0, 6, 0.03)], camber=0.02, te_round=0.35,
                    paint=pintar_ala, uv_weight=1.2, tip_len=0.03), group='wing')
    return b


if __name__ == '__main__':
    kit.run_bird(ASSET, construir, previas='--sin-previas' not in sys.argv)
