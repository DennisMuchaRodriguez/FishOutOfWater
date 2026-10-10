# -*- coding: utf-8 -*-
"""Pelícano común (Pelecanus onocrotalus) estilo plastilina, y su versión jefe (El Pelícano Saco Sin Fondo).
Cuerpo pesado blanco rosado, cabeza recogida sobre los hombros, pico enorme apoyado en el pecho con bolsa
amarilla, piel rosada alrededor del ojo, plumas de vuelo negras, patas cortas rosadas.
Envergadura de juego ~4.2 m. Lleva los peces en la bolsa."""
import os, sys, math
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), '..'))
import numpy as np
import kit
from kit import C, mix, shade, feather_strokes, smoothstep, fbm

ASSET = 'Ave_Pelicano'

BLANCO = C('#F5E9E3')
SUCIO = C('#E0D5C3')
NEGRO = C('#232427')
PICO = C('#E9A06A')
BOLSA = C('#F2B33D')
ROSA = C('#EFAE9A')
PECHO = C('#F1D88E')
PATA = C('#E9A58A')

PITCH = 30.0
L_PICO = 0.58
BASE_PICO = np.array([0.0, -0.74, 0.170])


def ejes_pico():
    p = math.radians(PITCH)
    f = np.array([0.0, -math.cos(p), -math.sin(p)])
    u = np.array([0.0, -math.sin(p), math.cos(p)])
    return f, u


def mancha(T, cy, cz, ry, rz, lado=True):
    d = ((T.y - cy) / ry) ** 2 + ((T.z - cz) / rz) ** 2
    m = 1 - smoothstep(0.55, 1.0, d)
    return m * smoothstep(0.04, 0.08, np.abs(T.x)) if lado else m


def hacer_pintar_cuerpo(jefe):
    def pintar(T):
        rgb = T.fill(SUCIO if jefe else BLANCO)
        rgb = mix(rgb, PECHO, mancha(T, -0.40, -0.02, 0.10, 0.10, lado=False) * smoothstep(0.1, -0.4, T.nz) * 0.8)
        rgb = mix(rgb, ROSA, mancha(T, -0.655, 0.215, 0.06, 0.05))
        if jefe:
            sucio = smoothstep(0.35, 0.75, fbm(T.pos, 7.0, 3, seed=14))
            rgb = rgb * (1 - 0.18 * sucio[:, None])
        return shade(T, rgb, top=0.05, bottom=0.12)
    return pintar


def hacer_pintar_ala(jefe):
    def pintar(T):
        arriba = T.side > -0.5
        rgb = T.fill(SUCIO if jefe else BLANCO)
        vuelo = np.clip(smoothstep(0.58, 0.66, T.u) + smoothstep(0.52, 0.62, T.v) * (1 - smoothstep(0.62, 0.7, T.u)), 0, 1)
        rgb = mix(rgb, NEGRO, vuelo)
        rgb = shade(T, rgb, top=0.05, bottom=0.08)
        rgb = feather_strokes(T, rgb, start=0.5, width=0.05, strength=0.3, color=C('#5A5A5E'))
        return rgb * np.where(arriba, 1.0, 0.92)[:, None]
    return pintar


def pintar_pico(T):
    rgb = mix(T.fill(PICO), C('#F3C35A'), smoothstep(0.3, 0.9, np.abs(T.v - 0.5) * 2) * 0.5)
    rgb = mix(rgb, C('#D3543A'), smoothstep(0.9, 1.0, T.u))
    return shade(T, rgb, top=0.10, bottom=0.08)


def hacer_pintar_bolsa(jefe):
    def pintar(T):
        rgb = T.fill(BOLSA)
        venas = smoothstep(0.85, 1.0, np.sin(T.v * 60.0 + T.u * 4.0))
        rgb = rgb * (1 - 0.08 * venas[:, None])
        if jefe:
            # siluetas de peces tragados
            # v = 0 es la parte de abajo de la bolsa; 0.25 y 0.75 son los costados
            for (cu, cv, s) in ((0.38, 0.22, 1.3), (0.60, 0.30, 1.1), (0.40, 0.78, 1.3), (0.62, 0.70, 1.1),
                                (0.50, 0.02, 1.2), (0.50, 0.98, 1.2)):
                d = ((T.u - cu) / (0.10 * s)) ** 2 + ((T.v - cv) / (0.045 * s)) ** 2
                cola = ((T.u - (cu + 0.11 * s)) / (0.035 * s)) ** 2 + ((T.v - cv) / (0.04 * s)) ** 2
                pez = (1 - smoothstep(0.7, 1.0, d)) + (1 - smoothstep(0.7, 1.0, cola))
                rgb = mix(rgb, C('#A55A1C'), np.clip(pez, 0, 1) * 0.9)
        return shade(T, rgb, top=0.06, bottom=0.10)
    return pintar


def hacer_pintar_cola(jefe):
    def pintar(T):
        rgb = shade(T, T.fill(SUCIO if jefe else BLANCO), top=0.05, bottom=0.08)
        return feather_strokes(T, rgb, start=0.45, width=0.06, strength=0.22, both=True)
    return pintar


def pintar_pata(T):
    return shade(T, T.fill(PATA), top=0.06, bottom=0.14)


def pintar_ceja(T):
    return shade(T, T.fill(C('#3B3330')), top=0.1, bottom=0.05)


def construir(jefe=False):
    nombre = 'Jefe_PelicanoSaco' if jefe else ASSET
    f, u = ejes_pico()
    centro_bolsa = BASE_PICO + f * L_PICO * 0.5 - u * (0.20 if jefe else 0.09)
    b = kit.Bird(nombre, wing_pivot=(0.18, -0.02, 0.12), feet=(0.015, centro_bolsa[1], centro_bolsa[2]),
                 clay=0.014 if jefe else 0.012, seed=71 if jefe else 72)

    b.add(kit.loft('cuerpo', [
        (0, -0.70, 0.170, 0.100, 0.090, 0.085),
        (0, -0.62, 0.180, 0.155, 0.135, 0.120),
        (0, -0.50, 0.130, 0.150, 0.110, 0.160),
        (0, -0.32, 0.050, 0.260, 0.180, 0.240),
        (0, -0.02, 0.050, 0.300, 0.190, 0.240),
        (0, 0.30, 0.070, 0.220, 0.140, 0.150),
        (0, 0.50, 0.090, 0.100, 0.060, 0.060),
    ], ring=24, samples=22, caps=('round', 'round'), paint=hacer_pintar_cuerpo(jefe), uv_weight=1.4))

    for sx in (1, -1):
        b.add(kit.eye('ojo%s' % ('I' if sx > 0 else 'D'), (0.105 * sx, -0.645, 0.228), 0.074,
                      look=(0.50 * sx, -0.80, 0.30), segs=18))
        if jefe:
            b.add(kit.brow('ceja%s' % ('I' if sx > 0 else 'D'), (0.05 * sx, -0.70, 0.30), (0.15 * sx, -0.62, 0.30),
                           0.024, paint=pintar_ceja))

    # mandíbula superior (una pieza) apoyada en el pecho
    b.add(kit.beak('pico', tuple(BASE_PICO), L_PICO, 0.078, 0.050, pitch=PITCH, hook=0.035, split=False,
                   paint_upper=pintar_pico, tip_round=0.12))

    # bolsa amarilla colgando bajo el pico
    prof = [0.05, 0.13, 0.14, 0.09, 0.03]
    if jefe:
        prof = [0.08, 0.26, 0.30, 0.22, 0.06]
    est = []
    for t, w, hb in zip((0.0, 0.25, 0.5, 0.75, 0.95), (0.070, 0.078 if not jefe else 0.12, 0.072 if not jefe else 0.13, 0.060 if not jefe else 0.10, 0.040), prof):
        c = BASE_PICO - u * 0.018 + f * L_PICO * t
        est.append(tuple(c) + (w, 0.018, hb))
    b.add(kit.loft('bolsa', est, ring=18, samples=14, caps=('round', 'round'), up=tuple(u),
                   paint=hacer_pintar_bolsa(jefe), uv_weight=3.5 if jefe else 1.2))

    b.add(kit.tail_fan('cola', (0, 0.46, 0.090), 0.090, 0.16, 0.15, thick=0.024, feathers=6, depth=0.03,
                       roundness=0.035, paint=hacer_pintar_cola(jefe)))

    for sx in (1, -1):
        b.add(kit.leg('pata%s' % sx, [(0.08 * sx, 0.20, -0.17), (0.08 * sx, 0.28, -0.185), (0.08 * sx, 0.34, -0.175)],
                      0.024, paint=pintar_pata))
        b.add(kit.foot_webbed('pie%s' % sx, (0.08 * sx, 0.34, -0.175), (0.0, 1.0, 0.04), 0.17, 0.16, toes=4,
                              thick=0.012, paint=pintar_pata))

    le = [(0.16, -0.30, 0.14), (0.65, -0.34, 0.20), (1.20, -0.26, 0.16), (1.70, -0.08, 0.08), (2.10, 0.06, 0.04)]
    te = [(0.16, 0.30, 0.12), (0.65, 0.36, 0.17), (1.20, 0.34, 0.14), (1.70, 0.28, 0.07), (2.10, 0.16, 0.04)]
    b.add(kit.blade('ala', le, te, [0.070, 0.060, 0.045, 0.030, 0.012], n=32, chord=6,
                    feathers=[(0.0, 0.62, 12, 0.035), (0.62, 1.0, 7, 0.08)], camber=0.035, te_round=0.35,
                    paint=hacer_pintar_ala(jefe), uv_weight=1.2, tip_len=0.05), group='wing')
    return b


REFS = [os.path.join(os.environ.get('KIT_REFS', ''), f) for f in ('pelicano/10883219.jpeg', 'pelicano/12554953.jpeg', 'pelicano/11183246.jpeg')]

if __name__ == '__main__':
    kit.run_bird(ASSET, construir, previas='--sin-previas' not in sys.argv, refs=REFS)
