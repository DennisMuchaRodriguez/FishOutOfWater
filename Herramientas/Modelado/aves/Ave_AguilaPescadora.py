# -*- coding: utf-8 -*-
"""Águila pescadora (Pandion haliaetus) estilo plastilina + constructor de rapaces (lo usa la Reina Águila).
Alas largas acodadas, dorso pardo oscuro, vientre y cabeza blancos con antifaz pardo, pico ganchudo negro,
cola barrada y garras grandes colgando para pescar. Envergadura de juego ~3.4 m. Lleva el pez en las garras."""
import os, sys
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), '..'))
import numpy as np
import kit
from kit import C, mix, shade, feather_strokes, smoothstep, fbm

ASSET = 'Ave_AguilaPescadora'

OSPREY = dict(
    escala=1.0, cabeza=1.0, pico=(0.15, 0.045, 0.085), pico_color='#1E1E22', cera='#8A98A6',
    pardo='#4B3626', pardo_claro='#6B5340', blanco='#F0ECE3', cabeza_color='#F0ECE3', cuello_hasta=-0.40,
    antifaz=True, collar=True, cola_color='#E9E2D4', cola_barras=True, pata='#B7C0C7', una='#17171A',
    alas_le=[(0.12, -0.16, 0.10), (0.55, -0.22, 0.17), (0.95, -0.22, 0.15), (1.35, 0.04, 0.07), (1.70, 0.20, 0.02)],
    alas_te=[(0.12, 0.22, 0.08), (0.55, 0.26, 0.14), (0.95, 0.22, 0.12), (1.35, 0.24, 0.06), (1.70, 0.24, 0.02)],
    dedos=5, ala_abajo_clara=True, corona=False, cejas=False, cresta=True,
)


def segmento(T, a, b, ancho):
    a, b = np.array(a), np.array(b)
    p = np.stack([T.y, T.z], 1)
    ab = b - a
    t = np.clip(((p - a) @ ab) / (ab @ ab), 0, 1)
    d = np.linalg.norm(p - (a + t[:, None] * ab), axis=1)
    return 1 - smoothstep(ancho * 0.5, ancho, d)


def rapaz(nombre, P):
    s = P['escala']
    hc = P['cabeza']
    PARDO, PARDO_CL, BLANCO = C(P['pardo']), C(P['pardo_claro']), C(P['blanco'])
    CABEZA = C(P['cabeza_color'])

    def pintar_cuerpo(T):
        rgb = mix(T.fill(BLANCO), PARDO, smoothstep(0.15, 0.45, T.nz) * smoothstep(-0.46, -0.36, T.y))
        cabeza = 1 - smoothstep(P['cuello_hasta'] - 0.04, P['cuello_hasta'] + 0.04, T.y)
        rgb = mix(rgb, CABEZA, cabeza)
        if P['antifaz']:
            lado = smoothstep(0.03, 0.07, np.abs(T.x))
            rgb = mix(rgb, PARDO, lado * segmento(T, (-0.585, 0.150), (-0.40, 0.12), 0.05))
            rgb = mix(rgb, PARDO, smoothstep(0.75, 0.95, T.nz) * cabeza * smoothstep(0.3, 0.6, fbm(T.pos, 30.0, 1, seed=5)) * 0.7)
        if P['collar']:
            collar = smoothstep(-0.36, -0.32, T.y) * (1 - smoothstep(-0.26, -0.22, T.y)) * smoothstep(0.1, -0.4, T.nz)
            rgb = mix(rgb, PARDO_CL, collar * smoothstep(0.2, 0.6, fbm(T.pos, 25.0, 1, seed=3)))
        return shade(T, rgb, top=0.06, bottom=0.10)

    def pintar_ala(T):
        arriba = T.side > -0.5
        rgb = np.where(arriba[:, None], PARDO[None], (BLANCO if P['ala_abajo_clara'] else PARDO * 0.9)[None])
        if P['ala_abajo_clara']:
            abajo = ~arriba
            muneca = smoothstep(0.46, 0.50, T.u) * (1 - smoothstep(0.60, 0.64, T.u)) * (1 - smoothstep(0.38, 0.46, T.v))
            rgb = mix(rgb, C('#2B211A'), muneca * abajo)
            barras = smoothstep(0.55, 0.9, np.sin(T.v * 40.0 + T.u * 6.0)) * smoothstep(0.42, 0.5, T.v)
            rgb = mix(rgb, C('#5B4A3C'), barras * abajo * 0.7)
            rgb = mix(rgb, C('#2B211A'), smoothstep(0.82, 0.9, T.u) * abajo)
        else:
            rgb = mix(rgb, PARDO_CL, (1 - smoothstep(0.35, 0.5, T.u)) * (1 - smoothstep(0.4, 0.55, T.v)) * arriba)
        rgb = shade(T, rgb, top=0.06, bottom=0.05)
        return feather_strokes(T, rgb, start=0.45, width=0.05, strength=0.35, color=PARDO_CL * 1.15)

    def pintar_cola(T):
        rgb = T.fill(C(P['cola_color']))
        if P['cola_barras']:
            barras = smoothstep(0.4, 0.75, np.sin(T.u * 34.0))
            rgb = mix(rgb, PARDO, barras * 0.85)
        rgb = shade(T, rgb, top=0.05, bottom=0.08)
        return feather_strokes(T, rgb, start=0.4, width=0.06, strength=0.25, both=True)

    def pintar_pico(T):
        rgb = mix(T.fill(C(P['pico_color'])), C(P['cera']), 1 - smoothstep(0.16, 0.26, T.u))
        return shade(T, rgb + 0.04, top=0.10, bottom=0.08)

    def pintar_pata(T):
        return shade(T, T.fill(C(P['pata'])), top=0.06, bottom=0.12)

    def pintar_una(T):
        return shade(T, T.fill(C(P['una'])) + 0.05, top=0.12, bottom=0.05)

    def pintar_pardo(T):
        return shade(T, T.fill(PARDO), top=0.08, bottom=0.06)

    def pintar_oro(T):
        return shade(T, mix(T.fill(C('#E6B43A')), C('#FFF0B0'), smoothstep(0.5, 1.0, T.nz) * 0.4), top=0.12, bottom=0.12)

    def pintar_gema(T):
        return shade(T, T.fill(C('#2E7FD0')), top=0.2, bottom=0.1)

    garra = np.array([0.07, -0.09, -0.34]) * s
    b = kit.Bird(nombre, wing_pivot=(0.14 * s, -0.04 * s, 0.10 * s), feet=tuple(garra), clay=0.013, seed=81 + len(nombre))

    def st(y, z, w, ht, hb):
        return (0, y * s, z * s, w * s, ht * s, hb * s)

    b.add(kit.loft('cuerpo', [
        st(-0.60, 0.100, 0.080 * hc, 0.072 * hc, 0.068 * hc),
        st(-0.53, 0.110, 0.140 * hc, 0.130 * hc, 0.115 * hc),
        st(-0.42, 0.080, 0.130, 0.110, 0.120),
        st(-0.24, 0.030, 0.210, 0.150, 0.190),
        st(0.02, 0.030, 0.220, 0.150, 0.180),
        st(0.28, 0.050, 0.150, 0.100, 0.110),
        st(0.44, 0.070, 0.070, 0.050, 0.050),
    ], ring=22, samples=22, caps=('round', 'round'), paint=pintar_cuerpo, uv_weight=1.4))

    for sx in (1, -1):
        b.add(kit.eye('ojo%s' % ('I' if sx > 0 else 'D'), (0.090 * sx * s * hc, -0.555 * s, 0.152 * s), 0.072 * s,
                      look=(0.48 * sx, -0.83, 0.26), segs=18))
        if P['cejas']:
            b.add(kit.brow('ceja%s' % ('I' if sx > 0 else 'D'), (0.03 * sx * s, -0.61 * s, 0.205 * s),
                           (0.13 * sx * s, -0.53 * s, 0.235 * s), 0.024 * s, paint=pintar_pardo))

    L, W, H = P['pico']
    b.add(kit.beak('pico', (0, -0.615 * s, 0.112 * s), L * s, W * s, H * s, pitch=-5, hook=0.38, droop=0.25,
                   lower=0.40, lower_len=0.75, paint_upper=pintar_pico, paint_lower=pintar_pico, tip_round=0.08))

    if P['cresta']:
        b.add(kit.crest('cresta', [(0.0, -0.47 * s, 0.215 * s), (0.03 * s, -0.46 * s, 0.205 * s), (-0.03 * s, -0.46 * s, 0.205 * s)],
                        [(0.0, -0.37 * s, 0.24 * s), (0.05 * s, -0.38 * s, 0.22 * s), (-0.05 * s, -0.38 * s, 0.22 * s)],
                        0.025 * s, paint=pintar_pardo))
    if P['corona']:
        b.add(kit.crown('tiara', (0, -0.55 * s, 0.232 * s), 0.07 * s, 0.065 * s, points=5, axis=(0, 0.1, 1.0),
                        paint=pintar_oro, paint_gem=pintar_gema))

    b.add(kit.tail_fan('cola', (0, 0.40 * s, 0.070 * s), 0.070 * s, 0.30 * s, 0.17 * s, thick=0.02 * s, feathers=7,
                       depth=0.025 * s, roundness=(0.0 if P['corona'] else 0.035) * s, paint=pintar_cola))

    for sx in (1, -1):
        b.add(kit.leg('pata%s' % sx, [(0.07 * sx * s, 0.00, -0.12 * s), (0.072 * sx * s, -0.03 * s, -0.22 * s),
                                     (0.072 * sx * s, -0.05 * s, -0.28 * s)], [0.04 * s, 0.026 * s, 0.022 * s], paint=pintar_pata))
        b.add(kit.foot_talons('garra%s' % sx, (0.072 * sx * s, -0.05 * s, -0.29 * s), (0.0, -1.0, -0.25), 0.11 * s,
                              0.018 * s, claws=True, spread=32, paint_toe=pintar_pata, paint_claw=pintar_una))

    le = [tuple(np.array(p) * s) for p in P['alas_le']]
    te = [tuple(np.array(p) * s) for p in P['alas_te']]
    b.add(kit.blade('ala', le, te, [0.055 * s, 0.046 * s, 0.035 * s, 0.020 * s, 0.008 * s], n=30, chord=6,
                    feathers=[(0.0, 0.7, 11, 0.025 * s), (0.7, 1.0, P['dedos'], 0.08 * s)], camber=0.03 * s,
                    te_round=0.35, paint=pintar_ala, uv_weight=1.2, tip_len=0.04 * s), group='wing')
    return b


def construir():
    return rapaz(ASSET, OSPREY)


REFS = [os.path.join(os.environ.get('KIT_REFS', ''), f) for f in ('aguila_pescadora/37619.jpg', 'aguila_pescadora/207486.jpg', 'aguila_pescadora/202877.jpg')]

if __name__ == '__main__':
    kit.run_bird(ASSET, construir, previas='--sin-previas' not in sys.argv, refs=REFS)
