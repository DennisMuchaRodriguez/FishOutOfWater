# -*- coding: utf-8 -*-
"""Batería de coche tirada en el fondo (0,37 m): caja oscura con etiqueta amarilla, bornes rojo y negro,
asa, sulfato blanco junto a los bornes y limo abajo."""
import os, sys, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from entorno_kit import *


def pintar_caja(T):
    rgb = T.fill(C('#393D43'))
    face = smoothstep(0.75, 0.9, np.abs(T.nrm[:, 1]))
    label = face * smoothstep(0.06, 0.07, T.z) * (1 - smoothstep(0.16, 0.17, T.z)) * (1 - smoothstep(0.14, 0.15, np.abs(T.x)))
    rgb = mix(rgb, '#E1A83A', label)
    stripe = label * (1 - smoothstep(0.008, 0.014, np.abs(T.z - 0.115 - 0.25 * T.x)))
    rgb = mix(rgb, '#2A2C30', stripe)
    rgb = film(T, rgb, '#5E6E48', z0=0.0, z1=0.07, amount=0.6)
    return light(T, rgb, 0.1, 0.2)


def pintar_tapa(T):
    rgb = T.fill(C('#2B2E33'))
    rgb = spots(T, rgb, '#D8D1B6', freq=25.0, thresh=0.35, soft=0.12, seed=6, amount=0.8 * smoothstep(0.2, 0.9, T.nz))
    return light(T, rgb, 0.1, 0.2)


def construir():
    b = prop('Bateria', clay=0.008, seed=39)
    b.add(rounded_box('Caja', (0, 0, 0.105), (0.36, 0.2, 0.21), e=0.22, rings=6, segs=12, paint=pintar_caja))
    b.add(rounded_box('Tapa', (0, 0, 0.212), (0.37, 0.21, 0.035), e=0.25, rings=4, segs=12, paint=pintar_tapa))
    for k, (x, col) in enumerate(((0.12, '#C8423A'), (-0.12, '#26282C'))):
        b.add(cyl('Borne%d' % k, (x, 0.05, 0.22), (x, 0.05, 0.262), 0.018, segs=8, paint=paint_flat(col)))
    b.add(tube('Asa', [(-0.08, -0.03, 0.225), (-0.065, -0.03, 0.27), (0.065, -0.03, 0.27), (0.08, -0.03, 0.225)], 0.01,
               ring=4, samples=5, caps=('flat', 'flat'), clay=0.1, paint=paint_flat('#1F2124')))
    return b


if __name__ == '__main__':
    run_prop('Bateria', construir, res=256, smoothness=0.3, ao_distance=0.05, bake_range=1.0, grain=0.03)
