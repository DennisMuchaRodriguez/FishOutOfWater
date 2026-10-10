# -*- coding: utf-8 -*-
"""Garza real (Ardea cinerea) estilo plastilina, y su versión jefe (La Garza Gris Gigante).
En vuelo: cuello recogido en S, patas largas estiradas hacia atrás, alas anchas y arqueadas,
pico de daga amarillo. Gris con plumas de vuelo negras, cabeza y cuello blancos, lista negra
del ojo a la nuca con penacho, rayas negras en el frente del cuello.
Envergadura de juego ~3.6 m (el jefe se agranda desde los datos). Lleva el pez en el pico."""
import os, sys
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), '..'))
import numpy as np
import kit
from kit import C, mix, shade, feather_strokes, smoothstep, fbm

ASSET = 'Ave_Garza'

BLANCO = C('#EEEDE8')
NEGRO = C('#202227')
AMARILLO = C('#E8A53A')
PATA = C('#B79A55')


def paleta(jefe):
    if jefe:
        return dict(gris=C('#56616E'), gris_ala=C('#4E5864'), bajo=C('#737B84'), negro=C('#121316'))
    return dict(gris=C('#9AA2AB'), gris_ala=C('#8E97A2'), bajo=C('#D3D5D8'), negro=NEGRO)


def segmento(T, a, b, ancho):
    """Distancia (en el plano Y-Z) de cada texel al segmento a-b, como máscara suave."""
    a, b = np.array(a), np.array(b)
    p = np.stack([T.y, T.z], 1)
    ab = b - a
    t = np.clip(((p - a) @ ab) / (ab @ ab), 0, 1)
    d = np.linalg.norm(p - (a + t[:, None] * ab), axis=1)
    return 1 - smoothstep(ancho * 0.5, ancho, d)


def hacer_pintar_cuerpo(jefe):
    P = paleta(jefe)

    def pintar(T):
        rgb = mix(T.fill(P['bajo']), P['gris'], smoothstep(-0.25, 0.25, T.nz))
        # cabeza y cuello blancos
        cuello = 1 - smoothstep(-0.36, -0.28, T.y)
        rgb = mix(rgb, BLANCO, cuello)
        # rayas negras en el frente del cuello
        frente = cuello * smoothstep(-0.2, -0.5, T.nz) * smoothstep(-0.62, -0.56, T.y)
        rayas = smoothstep(0.55, 0.85, np.sin(T.y * 90.0 + np.abs(T.x) * 40.0)) * (1 - smoothstep(0.02, 0.05, np.abs(T.x)))
        rgb = mix(rgb, P['negro'], frente * rayas * 0.85)
        # lista negra del ojo a la nuca
        lado = smoothstep(0.03, 0.06, np.abs(T.x))
        lista = lado * segmento(T, (-0.77, 0.135), (-0.60, 0.150), 0.035)
        rgb = mix(rgb, P['negro'], lista)
        if jefe:
            # cicatrices claras sobre el ojo derecho
            der = smoothstep(0.03, 0.06, -T.x)
            cic = (segmento(T, (-0.80, 0.19), (-0.70, 0.07), 0.012) + segmento(T, (-0.76, 0.20), (-0.67, 0.09), 0.010)) * der
            rgb = mix(rgb, C('#E9C9C0'), np.clip(cic, 0, 1))
            # pecho moteado de batalla
            rgb = rgb * (1 - 0.10 * smoothstep(0.3, 0.7, fbm(T.pos, 14.0, 2, seed=12)))[:, None]
        return shade(T, rgb, top=0.05, bottom=0.10)
    return pintar


def hacer_pintar_ala(jefe):
    P = paleta(jefe)

    def pintar(T):
        arriba = T.side > -0.5
        rgb = np.where(arriba[:, None], P['gris_ala'][None], (P['gris_ala'] * 0.85)[None])
        # plumas de vuelo negras: la mano y el borde de salida
        vuelo = np.clip(smoothstep(0.55, 0.66, T.u) + smoothstep(0.58, 0.70, T.v), 0, 1)
        rgb = mix(rgb, P['negro'] * 1.3, vuelo)
        # hombro claro
        rgb = mix(rgb, BLANCO * 0.92, (1 - smoothstep(0.05, 0.14, T.u)) * (1 - smoothstep(0.2, 0.35, T.v)) * arriba * 0.6)
        rgb = shade(T, rgb, top=0.05, bottom=0.06)
        return feather_strokes(T, rgb, start=0.5, width=0.05, strength=0.30, color=C('#4B4F57'))
    return pintar


def pintar_pico(T):
    rgb = mix(T.fill(AMARILLO), C('#B9782A'), smoothstep(0.8, 1.0, T.u) * 0.6)
    return shade(T, rgb, top=0.08, bottom=0.10)


def hacer_pintar_cola(jefe):
    P = paleta(jefe)

    def pintar(T):
        rgb = shade(T, T.fill(P['gris_ala']), top=0.05, bottom=0.08)
        return feather_strokes(T, rgb, start=0.45, width=0.06, strength=0.25, both=True)
    return pintar


def pintar_penacho(T):
    return shade(T, T.fill(C('#16171A')), top=0.1, bottom=0.05)


def pintar_pata(T):
    return shade(T, T.fill(PATA), top=0.06, bottom=0.14)


def construir(jefe=False):
    nombre = 'Jefe_GarzaGris' if jefe else ASSET
    largo_pico = 0.52 if jefe else 0.46
    b = kit.Bird(nombre, wing_pivot=(0.16, -0.04, 0.10), feet=(0.012, -0.83 - largo_pico, 0.045),
                 clay=0.014 if jefe else 0.012, seed=51 if jefe else 52)

    b.add(kit.loft('cuerpo', [
        (0, -0.80, 0.060, 0.075, 0.070, 0.065),
        (0, -0.72, 0.075, 0.130, 0.120, 0.105),
        (0, -0.62, 0.055, 0.115, 0.100, 0.115),
        (0, -0.52, -0.030, 0.125, 0.110, 0.170),
        (0, -0.36, 0.000, 0.190, 0.150, 0.200),
        (0, -0.10, 0.040, 0.230, 0.160, 0.190),
        (0, 0.20, 0.060, 0.170, 0.115, 0.120),
        (0, 0.42, 0.080, 0.085, 0.055, 0.055),
    ], ring=22, samples=24, caps=('round', 'round'), paint=hacer_pintar_cuerpo(jefe), uv_weight=1.5))

    for sx in (1, -1):
        b.add(kit.eye('ojo%s' % ('I' if sx > 0 else 'D'), (0.085 * sx, -0.75, 0.128), 0.075,
                      look=(0.50 * sx, -0.82, 0.25), segs=18))
        if jefe:
            b.add(kit.brow('ceja%s' % ('I' if sx > 0 else 'D'), (0.035 * sx, -0.805, 0.180), (0.135 * sx, -0.715, 0.225),
                           0.026, paint=pintar_penacho))

    b.add(kit.beak('pico', (0, -0.835, 0.060), largo_pico, 0.046 if jefe else 0.042, 0.072, pitch=2, hook=0.0,
                   gonys=0.05, lower=0.45, paint_upper=pintar_pico, paint_lower=pintar_pico, tip_round=0.04))

    # penacho negro de la nuca
    n_plumas = 3 if jefe else 2
    for i in range(n_plumas):
        dx = (i - (n_plumas - 1) / 2) * 0.025
        largo = 1.25 if jefe else 1.0
        pts = [(dx, -0.64, 0.150), (dx * 1.5, -0.64 + 0.09 * largo, 0.172 - i * 0.004),
               (dx * 2.0, -0.64 + 0.20 * largo, 0.150 - i * 0.01), (dx * 2.4, -0.64 + 0.29 * largo, 0.105 - i * 0.012)]
        b.add(kit.plume('penacho%d' % i, pts, 0.020, paint=pintar_penacho))

    b.add(kit.tail_fan('cola', (0, 0.38, 0.080), 0.070, 0.18, 0.14, thick=0.02, feathers=6, depth=0.025,
                       roundness=0.03, paint=hacer_pintar_cola(jefe)))

    # patas largas estiradas hacia atrás
    for sx in (1, -1):
        b.add(kit.leg('pata%s' % sx, [(0.060 * sx, 0.12, -0.12), (0.055 * sx, 0.40, -0.07),
                                     (0.050 * sx, 0.70, -0.025), (0.045 * sx, 0.92, -0.005)],
                      [0.030, 0.022, 0.018, 0.016], paint=pintar_pata))
        b.add(kit.foot_talons('pie%s' % sx, (0.045 * sx, 0.93, -0.005), (0.0, 1.0, 0.05), 0.15, 0.013,
                              claws=True, spread=24, paint_toe=pintar_pata, paint_claw=pintar_penacho))

    # alas anchas, arqueadas hacia abajo en las puntas, con "dedos"
    le = [(0.12, -0.22, 0.11), (0.55, -0.30, 0.18), (1.00, -0.24, 0.13), (1.42, -0.06, 0.02), (1.78, 0.10, -0.10)]
    te = [(0.12, 0.25, 0.09), (0.55, 0.33, 0.15), (1.00, 0.32, 0.10), (1.42, 0.28, 0.00), (1.78, 0.22, -0.10)]
    b.add(kit.blade('ala', le, te, [0.060, 0.050, 0.038, 0.024, 0.010], n=30, chord=6,
                    feathers=[(0.0, 0.65, 12, 0.03), (0.65, 1.0, 7, 0.07)], camber=0.03, te_round=0.35,
                    paint=hacer_pintar_ala(jefe), uv_weight=1.2, tip_len=0.04), group='wing')
    return b


REFS = [os.path.join(os.environ.get('KIT_REFS', ''), f) for f in ('garza/336394.jpg', 'garza/520948.jpg', 'garza/1071398.jpg')]

if __name__ == '__main__':
    kit.run_bird(ASSET, construir, previas='--sin-previas' not in sys.argv, refs=REFS)
