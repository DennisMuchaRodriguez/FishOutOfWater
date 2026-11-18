# -*- coding: utf-8 -*-
"""Traje de los peces defensores (gris y blanco, distinto del traje del protagonista): casco con visor oscuro,
collar, cinturón, mochila con dos tanques y un cañón de agua sobre la espalda con luces cian.

Se hace alrededor de un pez de referencia de 1,3 m (el PezNormal del juego, que nada acostado): en Blender la
cabeza mira a -Y, la espalda a +Z y la cara hacia abajo. El origen es el centro del cuerpo. En el juego
DefenderFish lo ajusta al tamaño real de cada pez.
"""
import os, sys, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from entorno_kit import *

BLANCO = '#E4E7EA'
GRIS = '#9BA3AB'
OSCURO = '#353B43'
CIAN = '#5FE3FF'


def pintar_blanco(T):
    rgb = T.fill(C(BLANCO))
    # Líneas de panel cada tanto (a lo largo de la pieza) y un leve gris en lo que mira abajo
    rgb = strokes(T, rgb, count=6, coord='v', width=0.05, strength=0.5, color=C(GRIS))
    rgb = mix(rgb, GRIS, 0.35 * smoothstep(0.1, -0.6, T.nz))
    return light(T, rgb, 0.08, 0.22)


def pintar_casco(T):
    rgb = T.fill(C(BLANCO))
    # Visor: banda oscura al frente (hacia -Y), con un brillo arriba
    front = smoothstep(0.25, 0.55, -T.nrm[:, 1])
    band = front * smoothstep(-0.25, -0.05, T.nz) * (1 - smoothstep(0.35, 0.55, T.nz))
    rgb = mix(rgb, '#16232E', band)
    shine = band * smoothstep(0.22, 0.32, T.nz) * (1 - smoothstep(0.36, 0.42, T.nz))
    rgb = mix(rgb, '#7FA6BC', shine * 0.8)
    # Raya gris por el centro del casco
    ridge = (1 - smoothstep(0.02, 0.035, np.abs(T.x))) * smoothstep(0.4, 0.7, T.nz)
    rgb = mix(rgb, GRIS, ridge)
    return light(T, rgb, 0.1, 0.2)


def brillo_casco(T):
    out = np.zeros((len(T.pos), 3))
    # Dos pilotos cian a los lados del visor
    for sx in (-1, 1):
        d = np.hypot(T.x - sx * 0.27, T.z - 0.12) + 0.6 * np.clip(T.y + 0.42, 0, None)
        m = 1 - smoothstep(0.025, 0.05, d)
        out += m[:, None] * C(CIAN)
    return np.clip(out, 0, 1)


def pintar_gris(T):
    return light(T, T.fill(C(GRIS)), 0.1, 0.25)


def pintar_oscuro(T):
    return light(T, T.fill(C(OSCURO)), 0.1, 0.2)


def pintar_tanque(T):
    rgb = T.fill(C('#D6DADF'))
    stripe = (1 - smoothstep(0.08, 0.12, np.abs(T.u - 0.5)))
    rgb = mix(rgb, '#5BBBD6', stripe)
    caps = np.maximum(1 - smoothstep(0.1, 0.16, T.u), smoothstep(0.84, 0.9, T.u))
    rgb = mix(rgb, GRIS, caps)
    return light(T, rgb, 0.12, 0.2)


def brillo_tanque(T):
    stripe = (1 - smoothstep(0.06, 0.1, np.abs(T.u - 0.5)))
    return stripe[:, None] * C(CIAN) * 0.8


def brillo_boca(T):
    return np.ones((len(T.pos), 1)) * C(CIAN)


def construir():
    b = prop('Traje_Defensor', clay=0.006, seed=71)

    # Casco: una cúpula sobre la parte de arriba y adelante de la cabeza (la cara, abajo, queda libre)
    eje = unit(V((0, -0.55, 1.0)))
    casco = revolve('Casco', [(0.0, 0.33), (0.2, 0.31), (0.33, 0.24), (0.42, 0.12), (0.45, 0.02), (0.44, -0.02)], 20,
                    origin=(0, -0.36, -0.02), axes=axes_from(eje), clay=0.3, paint=pintar_casco)
    casco.emit = brillo_casco
    b.add(casco)
    b.add(torus('BordeCasco', V((0, -0.36, -0.02)) + eje * (-0.02), 0.445, 0.03, axis=eje, segs=24, sides=6,
                paint=pintar_gris))

    # Collar y cinturón (aros alrededor del cuerpo)
    b.add(torus('Collar', (0, -0.1, 0.0), 0.41, 0.045, axis=(0, 1, 0), segs=24, sides=8, paint=pintar_gris))
    b.add(torus('Cinturon', (0, 0.22, -0.02), 0.34, 0.035, axis=(0, 1, 0), segs=22, sides=6, paint=pintar_oscuro))

    # Mochila con dos tanques
    b.add(rounded_box('Mochila', (0, 0.06, 0.36), (0.44, 0.46, 0.16), e=0.3, rings=6, segs=14, paint=pintar_blanco))
    for k, x in enumerate((-0.13, 0.13)):
        t = cyl('Tanque%d' % k, (x, -0.15, 0.47), (x, 0.28, 0.47), 0.075, segs=12, bevel=0.35, paint=pintar_tanque)
        t.emit = brillo_tanque
        b.add(t)

    # Cañón de agua centrado sobre la mochila, apuntando hacia adelante
    b.add(rounded_box('Montura', (0, 0.0, 0.58), (0.16, 0.24, 0.1), e=0.3, rings=4, segs=10, paint=pintar_gris))
    b.add(cyl('Canon', (0, 0.08, 0.6), (0, -0.52, 0.6), 0.055, segs=12, bevel=0.25, paint=pintar_oscuro))
    b.add(cyl('Camisa', (0, -0.08, 0.6), (0, -0.36, 0.6), 0.07, segs=12, bevel=0.3, paint=pintar_blanco))
    boca = torus('Boca', (0, -0.52, 0.6), 0.05, 0.016, axis=(0, 1, 0), segs=14, sides=5, paint=paint_flat(CIAN))
    boca.emit = brillo_boca
    b.add(boca)
    return b


if __name__ == '__main__':
    run_prop('Traje_Defensor', construir, res=512, smoothness=0.55, ao_distance=0.12, bake_range=2.0, grain=0.02,
             blotch=0.03, emission=(0.4, 1.6, 1.9),
             views=[('tres_cuartos', (-0.85, -1, 0.55)), ('lado', (1, 0, 0.15)), ('arriba', (0, 0, 1)),
                    ('frente', (0, -1, 0.1))])
