# -*- coding: utf-8 -*-
"""Cormorán grande (Phalacrocorax carbo) estilo plastilina, y su versión jefe (El Cormorán Rey).
Cuello estirado hacia delante, pico largo con gancho, piel amarilla en la base del pico, mejilla blanca,
negro con escamas bronce en la espalda y las alas, cola larga y rígida, patas palmeadas negras.
Envergadura de juego ~3.0 m. Lleva el pez en el pico."""
import os, sys
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), '..'))
import numpy as np
import kit
from kit import C, mix, shade, feather_strokes, smoothstep, fbm

ASSET = 'Ave_Cormoran'

NEGRO = C('#1C1D1F')
BRONCE = C('#4C3B2B')
BLANCO = C('#E9E5DA')
AMARILLO = C('#E5A436')
PICO = C('#3A3936')
ORO = C('#E6B43A')
ROJO = C('#B2283A')
GEMA = C('#C8283A')


def escamas_uv(a, b):
    """Bordes de plumas en escama (arcos en filas alternadas) para coordenadas a (a lo largo) y b."""
    r = np.floor(a)
    b2 = b + 0.5 * (r % 2)
    fa = a - r
    fb = b2 - np.floor(b2) - 0.5
    d = np.sqrt(fb ** 2 + fa ** 2)
    return smoothstep(0.50, 0.62, d) * (1 - smoothstep(0.70, 0.82, d)) * (fa > 0.05)


def escamas(T, escala=22.0):
    return escamas_uv(T.y * escala, np.abs(T.x) * escala)


def mancha(T, cy, cz, ry, rz):
    d = ((T.y - cy) / ry) ** 2 + ((T.z - cz) / rz) ** 2
    return (1 - smoothstep(0.55, 1.0, d)) * smoothstep(0.03, 0.06, np.abs(T.x))


def hacer_pintar_cuerpo(jefe):
    def pintar(T):
        rgb = T.fill(NEGRO)
        dorso = smoothstep(0.3, 0.6, T.nz) * smoothstep(-0.44, -0.32, T.y) * (1 - smoothstep(0.15, 0.25, T.y))
        if jefe:
            # brillo verde azulado arriba y morado a los lados
            rgb = mix(rgb, C('#1F4B55'), smoothstep(0.2, 0.8, T.nz) * 0.7)
            rgb = mix(rgb, C('#3A2850'), (1 - smoothstep(0.1, 0.5, np.abs(T.nz))) * 0.5)
        else:
            rgb = mix(rgb, BRONCE, dorso)
        rgb = rgb * (1 - 0.30 * (escamas(T) * dorso)[:, None])
        # mejilla blanca y piel amarilla en la base del pico
        rgb = mix(rgb, BLANCO, mancha(T, -0.925, 0.070, 0.05, 0.035))
        rgb = mix(rgb, AMARILLO, mancha(T, -0.975, 0.062, 0.03, 0.022))
        return shade(T, rgb + 0.03, top=0.08, bottom=0.10)
    return pintar


def hacer_pintar_ala(jefe):
    def pintar(T):
        arriba = T.side > -0.5
        cubiertas = (1 - smoothstep(0.50, 0.62, T.u)) * (1 - smoothstep(0.45, 0.6, T.v)) * arriba
        base = C('#2A2F3A') if jefe else NEGRO
        rgb = T.fill(base * 1.2)
        rgb = mix(rgb, C('#2A5560') if jefe else BRONCE, cubiertas)
        rgb = rgb * (1 - 0.30 * (escamas_uv(T.u * 16.0, T.v * 5.0) * cubiertas)[:, None])
        rgb = shade(T, rgb, top=0.08, bottom=0.05)
        return feather_strokes(T, rgb, start=0.45, width=0.05, strength=0.30, color=C('#4E4A46'))
    return pintar


def pintar_cola(T):
    rgb = shade(T, T.fill(NEGRO * 1.3), top=0.08, bottom=0.06)
    return feather_strokes(T, rgb, start=0.4, width=0.06, strength=0.28, both=True, color=C('#4A4A4A'))


def pintar_pico_sup(T):
    return shade(T, mix(T.fill(PICO), C('#5A5852'), smoothstep(0.6, 1.0, T.u) * 0.4), top=0.10, bottom=0.08)


def pintar_pico_inf(T):
    rgb = mix(T.fill(AMARILLO), PICO, smoothstep(0.30, 0.45, T.u))
    return shade(T, rgb, top=0.08, bottom=0.10)


def pintar_pata(T):
    return shade(T, T.fill(C('#222326')), top=0.08, bottom=0.10)


def pintar_oro(T):
    return shade(T, mix(T.fill(ORO), C('#FFF0B0'), smoothstep(0.5, 1.0, T.nz) * 0.4), top=0.12, bottom=0.12)


def pintar_gema(T):
    return shade(T, T.fill(GEMA), top=0.2, bottom=0.1)


def pintar_collar(T):
    return shade(T, T.fill(ROJO), top=0.1, bottom=0.1)


def construir(jefe=False):
    nombre = 'Jefe_CormoranRey' if jefe else ASSET
    b = kit.Bird(nombre, wing_pivot=(0.14, -0.06, 0.08), feet=(0.012, -1.20, 0.072), clay=0.012, seed=61 if jefe else 62)

    b.add(kit.loft('cuerpo', [
        (0, -0.96, 0.095, 0.065, 0.058, 0.055),
        (0, -0.88, 0.100, 0.110, 0.100, 0.090),
        (0, -0.77, 0.080, 0.075, 0.072, 0.075),
        (0, -0.60, 0.050, 0.085, 0.082, 0.090),
        (0, -0.40, 0.020, 0.170, 0.130, 0.160),
        (0, -0.12, 0.020, 0.210, 0.140, 0.170),
        (0, 0.18, 0.030, 0.150, 0.100, 0.110),
        (0, 0.36, 0.050, 0.070, 0.045, 0.045),
    ], ring=22, samples=24, caps=('round', 'round'), paint=hacer_pintar_cuerpo(jefe), uv_weight=1.4))

    for sx in (1, -1):
        b.add(kit.eye('ojo%s' % ('I' if sx > 0 else 'D'), (0.072 * sx, -0.905, 0.135), 0.064,
                      look=(0.48 * sx, -0.83, 0.26), segs=18))
        if jefe:
            b.add(kit.brow('ceja%s' % ('I' if sx > 0 else 'D'), (0.030 * sx, -0.955, 0.180), (0.115 * sx, -0.875, 0.215),
                           0.022, paint=pintar_cola))

    b.add(kit.beak('pico', (0, -1.005, 0.090), 0.21, 0.034, 0.050, pitch=-2, hook=0.18, gonys=0.0, lower=0.42,
                   paint_upper=pintar_pico_sup, paint_lower=pintar_pico_inf, tip_round=0.06))

    b.add(kit.tail_fan('cola', (0, 0.33, 0.050), 0.060, 0.36, 0.11, thick=0.016, feathers=6, depth=0.03,
                       roundness=0.04, paint=pintar_cola))

    for sx in (1, -1):
        b.add(kit.leg('pata%s' % sx, [(0.06 * sx, 0.08, -0.12), (0.065 * sx, 0.22, -0.125), (0.065 * sx, 0.33, -0.10)],
                      0.02, paint=pintar_pata))
        b.add(kit.foot_webbed('pie%s' % sx, (0.065 * sx, 0.33, -0.10), (0.0, 1.0, 0.06), 0.17, 0.16, toes=4,
                              thick=0.011, paint=pintar_pata))

    if jefe:
        b.add(kit.crown('corona', (0, -0.875, 0.188), 0.062, 0.075, points=5, axis=(0, 0.12, 1.0),
                        paint=pintar_oro, paint_gem=pintar_gema))
        b.add(kit.torus('collar', (0, -0.70, 0.065), 0.088, 0.024, axis=(0, 1.0, 0.25), segs=24, sides=8,
                        paint=pintar_collar))

    le = [(0.10, -0.18, 0.08), (0.45, -0.22, 0.12), (0.85, -0.14, 0.10), (1.20, 0.02, 0.05), (1.48, 0.14, 0.02)]
    te = [(0.10, 0.18, 0.07), (0.45, 0.22, 0.10), (0.85, 0.20, 0.09), (1.20, 0.18, 0.05), (1.48, 0.18, 0.02)]
    b.add(kit.blade('ala', le, te, [0.045, 0.038, 0.028, 0.016, 0.007], n=28, chord=6,
                    feathers=[(0.0, 0.6, 10, 0.022), (0.6, 1.0, 7, 0.05)], camber=0.025, te_round=0.35,
                    paint=hacer_pintar_ala(jefe), uv_weight=1.2, tip_len=0.03), group='wing')
    return b


REFS = [os.path.join(os.environ.get('KIT_REFS', ''), f) for f in ('cormoran/1462091.jpg', 'cormoran/209269.jpg', 'cormoran/1413629.jpg')]

if __name__ == '__main__':
    kit.run_bird(ASSET, construir, previas='--sin-previas' not in sys.argv, refs=REFS)
