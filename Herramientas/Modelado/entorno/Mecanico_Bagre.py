# -*- coding: utf-8 -*-
"""El Mecánico del Taller: un bagre (bagre de canal, Ictalurus punctatus) estilo plastilina, erguido como los
personajes del juego, con gafas redondas, delantal de cuero y una llave inglesa en la aleta.

Referencias del bagre de canal: cuerpo robusto y cilíndrico, cabeza ancha y aplanada, boca ancha con la
mandíbula de arriba un poco más larga, ocho barbillas (dos largas en las comisuras, cuatro en el mentón y dos
junto a las narinas), aleta adiposa chica antes de la cola, aletas dorsal y pectorales con espina, cola en
horquilla; lomo gris azulado con puntos oscuros y panza clara.

Ejes (Blender): arriba +Z, la cara mira a -Y (en Unity queda mirando a +Z). Mide ~1,5 m de alto; el Taller lo
escala por la altura.
"""
import os, sys, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from entorno_kit import *
from kit import eye, glasses, blade

ASSET = 'Mecanico_Bagre'
LOMO = '#5C6A70'
PANZA = '#DCD5C3'
OSCURO = '#2E383C'
ALETA = '#6A757A'
CUERO = '#7A4E2D'


def pintar_cuerpo(T):
    # Lomo gris azulado arriba/atrás y panza clara al frente
    front = smoothstep(-0.1, -0.55, T.nrm[:, 1])
    rgb = grad(T, LOMO, C(PANZA), front)
    rgb = spots(T, rgb, OSCURO, freq=16.0, thresh=0.43, soft=0.06, seed=9, amount=0.85 * (1 - front))
    # Delantal de cuero sobre la panza, con un bolsillo y las tiras
    apron = smoothstep(0.35, 0.6, -T.nrm[:, 1]) * smoothstep(0.3, 0.36, T.z) * (1 - smoothstep(0.86, 0.9, T.z))
    rgb = mix(rgb, CUERO, apron)
    pocket = apron * smoothstep(0.48, 0.5, T.z) * (1 - smoothstep(0.64, 0.66, T.z)) * (1 - smoothstep(0.1, 0.115, np.abs(T.x)))
    rgb = mix(rgb, '#5E3A20', pocket * 0.8)
    stitch = apron * (1 - smoothstep(0.004, 0.009, np.abs(np.abs(T.x) - 0.1))) * smoothstep(0.48, 0.5, T.z) * (1 - smoothstep(0.64, 0.66, T.z))
    rgb = mix(rgb, '#C9A46A', stitch)
    strap = (1 - smoothstep(0.018, 0.026, np.abs(T.z - 0.88 - 0.25 * np.abs(T.x)))) * smoothstep(-0.2, 0.3, np.abs(T.x) - 0.05)
    rgb = mix(rgb, '#5E3A20', strap * (1 - front * 0.3))
    return light(T, rgb, 0.12, 0.25)


def pintar_cabeza(T):
    rgb = grad(T, LOMO, C(PANZA), smoothstep(-0.2, -0.75, T.nz))
    rgb = spots(T, rgb, OSCURO, freq=20.0, thresh=0.45, soft=0.06, seed=4, amount=0.7 * smoothstep(-0.2, 0.4, T.nz))
    # Boca ancha: una línea oscura al frente, con el labio de arriba un poco más adelante
    front = smoothstep(-0.27, -0.31, T.y) * (1 - smoothstep(0.2, 0.24, np.abs(T.x)))
    mouth = front * (1 - smoothstep(0.012, 0.022, np.abs(T.z - 0.985 - 0.35 * T.x ** 2)))
    rgb = mix(rgb, '#241C1F', mouth)
    lip = front * (1 - smoothstep(0.006, 0.012, np.abs(T.z - 1.005 - 0.35 * T.x ** 2)))
    rgb = mix(rgb, '#8C7E78', lip * 0.6)
    # Mejillas un poco rosadas
    cheek = (1 - smoothstep(0.03, 0.08, np.hypot(np.abs(T.x) - 0.21, T.z - 1.04))) * smoothstep(-0.1, -0.3, T.y)
    rgb = mix(rgb, '#B7848A', cheek * 0.45)
    return light(T, rgb, 0.12, 0.22)


def pintar_barbilla(T):
    rgb = grad(T, OSCURO, C('#55605F'), T.u)
    return light(T, rgb, 0.1, 0.2)


def pintar_aleta(T):
    rgb = T.fill(C(ALETA))
    rgb = strokes(T, rgb, count=9, coord='v', width=0.12, strength=0.35)
    rgb = mix(rgb, OSCURO, 0.5 * smoothstep(0.75, 1.0, T.u))
    return light(T, rgb, 0.1, 0.2)


def pintar_metal(T):
    rgb = T.fill(C('#A9B1B7'))
    rgb = spots(T, rgb, '#7D6A55', freq=30.0, thresh=0.45, soft=0.1, seed=2, amount=0.5)
    return light(T, rgb, 0.2, 0.3)


def construir():
    b = Bird(ASSET, wing_pivot=(0, 0, 0), feet=None, clay=0.012, seed=91)

    # Cuerpo robusto, erguido (la cola abajo y algo atrás). Sección: ancho en X, grosor en Y (panza a -Y)
    body = [(0, 0.30, 0.10, 0.03, 0.03, 0.03), (0, 0.24, 0.25, 0.08, 0.07, 0.07), (0, 0.14, 0.45, 0.17, 0.15, 0.16),
            (0, 0.05, 0.68, 0.22, 0.19, 0.21), (0, 0.00, 0.88, 0.25, 0.20, 0.22), (0, -0.02, 1.02, 0.26, 0.18, 0.2)]
    b.add(loft('Cuerpo', body, ring=20, samples=18, caps=('point', 'round'), up=(0, 1, 0), paint=pintar_cuerpo))

    # Cabeza ancha y aplanada, mirando al frente (-Y)
    head = [(0, 0.12, 1.06, 0.26, 0.17, 0.14), (0, -0.08, 1.08, 0.31, 0.17, 0.15), (0, -0.24, 1.06, 0.29, 0.13, 0.13),
            (0, -0.31, 1.03, 0.22, 0.08, 0.09)]
    b.add(loft('Cabeza', head, ring=20, samples=14, caps=('round', 'round'), up=(0, 0, 1), paint=pintar_cabeza))

    # Ojos chicos detrás de unas gafas redondas grandes
    for sx in (-1, 1):
        b.add(eye('Ojo%s' % ('I' if sx < 0 else 'D'), (sx * 0.12, -0.3, 1.15), 0.05, (sx * 0.15, -1, 0.05), pupil=0.5))
    b.add(glasses('Gafas', (-0.12, -0.355, 1.15), (0.12, -0.355, 1.15), 0.078, rim=0.11, look=(0, -1, 0),
                  paint_frame=paint_flat('#B8862E'), paint_lens=paint_flat('#BFE3EE')))

    # Ocho barbillas: dos largas en las comisuras, cuatro en el mentón y dos junto a las narinas
    for sx in (-1, 1):
        b.add(tube('BarbillaLarga%d' % sx, [(sx * 0.25, -0.25, 0.99), (sx * 0.43, -0.33, 0.96), (sx * 0.58, -0.36, 0.84),
                                            (sx * 0.64, -0.31, 0.68)], [0.026, 0.02, 0.013, 0.007], ring=6, samples=10,
                   caps=('round', 'point'), clay=0.2, paint=pintar_barbilla))
        b.add(tube('BarbillaNariz%d' % sx, [(sx * 0.08, -0.32, 1.12), (sx * 0.1, -0.39, 1.19), (sx * 0.13, -0.41, 1.26)],
                   [0.014, 0.01, 0.006], ring=5, samples=6, caps=('round', 'point'), clay=0.1, paint=pintar_barbilla))
        for k, xx in enumerate((0.06, 0.15)):
            b.add(tube('BarbillaMenton%d_%d' % (sx, k), [(sx * xx, -0.25, 0.95), (sx * (xx + 0.02), -0.29, 0.86),
                                                         (sx * (xx + 0.03), -0.28, 0.76 + 0.03 * k)],
                       [0.014, 0.01, 0.005], ring=5, samples=6, caps=('round', 'point'), clay=0.1, paint=pintar_barbilla))

    # Aletas: dorsal con espina en el lomo (+Y), pectorales como brazos, adiposa chica, anal y cola en horquilla
    b.add(blade('Dorsal', [(0, 0.19, 0.98), (0, 0.23, 0.86), (0, 0.24, 0.74)],
                [(0, 0.44, 1.04), (0, 0.37, 0.86), (0, 0.27, 0.71)], [0.014, 0.01, 0.006], n=12, chord=4,
                paint=pintar_aleta))
    for sx in (-1, 1):
        b.add(blade('Pectoral%d' % sx, [(sx * 0.23, -0.07, 0.95), (sx * 0.4, -0.15, 0.85), (sx * 0.56, -0.2, 0.74)],
                    [(sx * 0.23, 0.07, 0.9), (sx * 0.38, 0.0, 0.79), (sx * 0.5, -0.06, 0.68)], [0.018, 0.012, 0.006],
                    n=12, chord=4, paint=pintar_aleta))
    b.add(ellipsoid('Adiposa', (0, 0.25, 0.36), (0.014, 0.06, 0.05), rings=5, segs=8, paint=pintar_aleta))
    b.add(blade('Anal', [(0, 0.04, 0.42), (0, 0.16, 0.24)], [(0, -0.1, 0.36), (0, 0.06, 0.18)], [0.01, 0.006], n=8,
                chord=3, paint=pintar_aleta))
    b.add(blade('Cola', [(0, 0.22, 0.13), (0, 0.30, 0.11), (0, 0.38, 0.13)],
                [(0, 0.08, -0.12), (0, 0.31, 0.02), (0, 0.56, -0.08)], [0.012, 0.008, 0.012], n=14, chord=4,
                paint=pintar_aleta))

    # Llave inglesa en la aleta derecha
    b.add(cyl('LlaveMango', (0.55, -0.2, 0.72), (0.6, -0.36, 0.96), 0.022, segs=8, bevel=0.3, paint=pintar_metal))
    b.add(torus('LlaveBoca', (0.605, -0.375, 0.99), 0.045, 0.016, axis=(0.3, -0.6, 0.75), segs=12, sides=5,
                paint=pintar_metal))
    return b


if __name__ == '__main__':
    import kit
    kit.run_bird(ASSET, construir, folder=('Assets', 'Models', 'Taller'), previas='--sin-previas' not in sys.argv,
                 res=1024, grain_amt=0.04, blotch_amt=0.06)
