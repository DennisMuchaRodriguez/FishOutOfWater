# -*- coding: utf-8 -*-
"""Objetos del Taller (la cueva del mecánico bajo el lago), con referencias del taller de Tony Stark en las
películas de Iron Man y de cuevas submarinas estilizadas:
  - Mesa_Chatarra: mesa de trabajo de tablones y caños, con un tablero de herramientas colgadas, una morsa,
    un flexo y un cajonero (el banco de trabajo del taller).
  - Vitrina_Traje: nicho para exhibir un traje (la "sala de armaduras"): panel de fondo con tiras de luz, base
    redonda con aro de luz, columnas y una luz cenital.
  - Mesa_Holo: mesa holográfica redonda (a la altura de la cintura) con aro de luz y proyector en el centro; el
    holograma lo dibuja el juego.
  - BrazoRobot_Base / BrazoRobot_Tramo / BrazoRobot_Pinza: brazo robótico de taller (como el "Dum-E" de Stark) en
    piezas separadas para que el juego lo articule. Tramo: del origen hacia +Z, 1 m. Pinza: muñeca en el origen.
  - Estalactitas: racimo de estalactitas colgando de una roca (origen arriba, apuntan hacia -Z); dado vuelta
    sirve de estalagmitas.
  - Cristales_Cueva: racimo de cristales hexagonales que brillan, sobre una roca.
  - Pantallas_Taller: tres monitores en un soporte, con interfaces azules que brillan.
Uso: python Taller_Props.py Mesa_Holo [otros...] [--sin-previas]   (build_all.py también los conoce)
"""
import os, sys, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from entorno_kit import *

TODOS = ['Mesa_Chatarra', 'Vitrina_Traje', 'Mesa_Holo', 'BrazoRobot_Base', 'BrazoRobot_Tramo', 'BrazoRobot_Pinza',
         'Estalactitas', 'Cristales_Cueva', 'Pantallas_Taller']

CIAN = '#5FE3FF'
METAL = '#5B646C'
METAL_OSC = '#30363C'
NARANJA = '#E07B2E'


def metal(col=METAL, scratches=0.3):
    def p(T):
        rgb = T.fill(C(col))
        rgb = spots(T, rgb, '#8D969D', freq=28.0, thresh=0.45, soft=0.1, seed=3, amount=scratches)
        return light(T, rgb, 0.16, 0.28)
    return p


def madera(T):
    rgb = T.fill(C('#7A5636'))
    rgb = strokes(T, rgb, count=22, coord='u', width=0.12, strength=0.3, color=C('#5C3E24'))
    rgb = spots(T, rgb, '#4E3A27', freq=8.0, thresh=0.5, soft=0.1, seed=5, amount=0.4)
    return light(T, rgb, 0.12, 0.25)


def brillo(col=CIAN, k=1.0):
    def e(T):
        return np.ones((len(T.pos), 1)) * C(col) * k
    return e


def roca(T):
    rgb = grad(T, '#2C3A3E', C('#56676A'), smoothstep(-0.6, 0.8, T.nz))
    rgb = spots(T, rgb, '#22302F', freq=6.0, thresh=0.4, soft=0.12, seed=2, amount=0.6)
    rgb = moss(T, rgb, '#3F6A55', amount=0.5, up=0.3)
    return light(T, rgb, 0.12, 0.3)


# =====================================================================================
# Mesa de chatarra con tablero de herramientas
# =====================================================================================

def tablero(T):
    rgb = T.fill(C('#8E7A5B'))
    holes = 1 - smoothstep(0.006, 0.01, np.hypot(np.mod(T.x + 0.02, 0.06) - 0.03, np.mod(T.z, 0.06) - 0.03))
    rgb = mix(rgb, '#3A3127', holes * smoothstep(0.6, 0.9, np.abs(T.nrm[:, 1])))
    return light(T, rgb, 0.1, 0.2)


def cajonero(T):
    rgb = T.fill(C('#B04A30'))
    seam = 1 - smoothstep(0.006, 0.012, np.abs(np.mod(T.z - 0.36, 0.18) - 0.09) - 0.08)
    return light(T, mix(rgb, '#7A2F1E', seam), 0.14, 0.25)


def mesa_chatarra():
    b = prop('Mesa_Chatarra', clay=0.006, seed=101)
    # Tablones de la cubierta y caños de las patas
    for k in range(4):
        b.add(rounded_box('Tablon%d' % k, (0, -0.33 + k * 0.22, 0.95), (2.1, 0.21, 0.07), e=0.25, rings=4, segs=10,
                          paint=madera))
    for sx in (-1, 1):
        for sy in (-1, 1):
            b.add(cyl('Pata%d%d' % (sx, sy), (sx * 0.95, sy * 0.38, 0.0), (sx * 0.95, sy * 0.38, 0.92), 0.04, segs=8,
                      paint=metal(METAL_OSC)))
        b.add(cyl('Travesano%d' % sx, (sx * 0.95, -0.38, 0.3), (sx * 0.95, 0.38, 0.3), 0.025, segs=6, paint=metal(METAL_OSC)))
    b.add(rounded_box('Estante', (0, 0, 0.3), (1.9, 0.72, 0.04), e=0.2, rings=3, segs=8, paint=metal()))
    # Cajonero a la izquierda (con manijas)
    b.add(rounded_box('Cajonero', (-0.6, 0.0, 0.62), (0.55, 0.62, 0.56), e=0.18, rings=4, segs=10, paint=cajonero))
    for k in range(3):
        b.add(cyl('Manija%d' % k, (-0.7, -0.315, 0.43 + k * 0.18), (-0.5, -0.315, 0.43 + k * 0.18), 0.012, segs=6,
                  paint=metal('#C9CED2')))
    # Tablero perforado atrás con herramientas colgadas
    b.add(rounded_box('Tablero', (0, 0.42, 1.55), (2.0, 0.05, 1.1), e=0.12, rings=4, segs=10, paint=tablero))
    for sx in (-0.95, 0.95):
        b.add(cyl('Poste%d' % (sx > 0), (sx, 0.42, 0.95), (sx, 0.42, 2.1), 0.035, segs=8, paint=metal(METAL_OSC)))
    # Llaves, martillo y destornilladores colgados del tablero
    for k in range(4):
        x = -0.75 + k * 0.13
        L = 0.28 + k * 0.05
        b.add(cyl('Llave%d' % k, (x, 0.39, 1.75), (x, 0.39, 1.75 - L), 0.014, segs=6, paint=metal('#B9C0C6')))
        b.add(torus('LlaveOjo%d' % k, (x, 0.39, 1.77), 0.03, 0.01, axis=(0, 1, 0), segs=10, sides=4, paint=metal('#B9C0C6')))
    b.add(cyl('MartilloMango', (0.0, 0.39, 1.95), (0.0, 0.39, 1.45), 0.02, segs=8, paint=madera))
    b.add(rounded_box('MartilloCabeza', (0.0, 0.385, 1.97), (0.2, 0.06, 0.07), e=0.3, rings=3, segs=8, paint=metal(METAL_OSC)))
    for k in range(3):
        x = 0.3 + k * 0.1
        b.add(cyl('DestMango%d' % k, (x, 0.39, 1.8), (x, 0.39, 1.68), 0.02, segs=8,
                  paint=paint_flat(['#D24A3A', '#E2B23A', '#3A7FD2'][k])))
        b.add(cyl('DestPunta%d' % k, (x, 0.39, 1.68), (x, 0.39, 1.5), 0.007, segs=5, paint=metal('#C9CED2')))
    b.add(torus('RollCable', (0.75, 0.38, 1.7), 0.14, 0.03, axis=(0, 1, 0), segs=16, sides=6, paint=paint_flat('#2E2A26')))
    # Morsa en la esquina derecha
    b.add(rounded_box('Morsa', (0.85, -0.3, 1.06), (0.18, 0.22, 0.14), e=0.25, rings=3, segs=8, paint=metal('#3F6E8C')))
    b.add(cyl('MorsaTornillo', (0.85, -0.42, 1.08), (0.85, -0.55, 1.08), 0.015, segs=6, paint=metal('#C9CED2')))
    # Flexo
    b.add(cyl('FlexoBase', (0.6, 0.25, 0.985), (0.6, 0.25, 1.01), 0.09, segs=12, paint=metal(METAL_OSC)))
    b.add(tube('FlexoBrazo', [(0.6, 0.25, 1.0), (0.55, 0.2, 1.45), (0.35, 0.05, 1.55)], 0.02, ring=6, samples=6,
               caps=('flat', 'flat'), clay=0.1, paint=metal(METAL_OSC)))
    b.add(revolve('FlexoPantalla', [(0.0, 0.06), (0.05, 0.05), (0.11, -0.04), (0.12, -0.06)], 14,
                  origin=(0.33, 0.03, 1.55), axes=axes_from(unit(V((-0.3, -0.25, 1)))), clay=0.1, paint=metal(NARANJA)))
    return b


# =====================================================================================
# Vitrina para exhibir un traje
# =====================================================================================

def panel_vitrina(T):
    rgb = T.fill(C('#262C33'))
    lines = 1 - smoothstep(0.004, 0.008, np.abs(np.mod(T.z, 0.3) - 0.15) - 0.14)
    rgb = mix(rgb, '#3A424B', lines)
    return light(T, rgb, 0.1, 0.2)


def vitrina():
    b = prop('Vitrina_Traje', clay=0.004, seed=111)
    b.add(cyl('Base', (0, 0, 0), (0, 0, 0.16), 0.62, segs=28, bevel=0.15, paint=metal(METAL_OSC, 0.15)))
    aro = torus('AroLuz', (0, 0, 0.165), 0.5, 0.025, segs=36, sides=6, paint=paint_flat(CIAN))
    aro.emit = brillo(CIAN)
    b.add(aro)
    b.add(rounded_box('Fondo', (0, 0.55, 1.3), (1.5, 0.12, 2.5), e=0.15, rings=4, segs=10, paint=panel_vitrina))
    for sx in (-1, 1):
        b.add(rounded_box('Columna%d' % sx, (sx * 0.78, 0.4, 1.3), (0.14, 0.3, 2.6), e=0.2, rings=4, segs=8,
                          paint=metal('#4A535B', 0.2)))
        tira = rounded_box('Tira%d' % sx, (sx * 0.7, 0.49, 1.3), (0.035, 0.02, 2.2), e=0.3, rings=3, segs=6,
                           paint=paint_flat(CIAN))
        tira.emit = brillo(CIAN, 0.9)
        b.add(tira)
    b.add(rounded_box('Techo', (0, 0.3, 2.62), (1.75, 0.75, 0.12), e=0.2, rings=3, segs=10, paint=metal(METAL_OSC, 0.15)))
    luz = cyl('LuzCenital', (0, 0.25, 2.55), (0, 0.25, 2.57), 0.3, segs=20, bevel=0.1, paint=paint_flat('#E8F6FF'))
    luz.emit = brillo('#CFEFFF', 1.0)
    b.add(luz)
    # Emblema redondo naranja en el panel
    emb = torus('Emblema', (0, 0.485, 2.15), 0.14, 0.02, axis=(0, 1, 0), segs=24, sides=5, paint=paint_flat(NARANJA))
    emb.emit = brillo(NARANJA, 0.7)
    b.add(emb)
    return b


# =====================================================================================
# Mesa holográfica
# =====================================================================================

def _grid(T, w0, w1):
    gx = 1 - smoothstep(w0, w1, np.abs(np.mod(T.x, 0.1) - 0.05) - 0.045)
    gy = 1 - smoothstep(w0, w1, np.abs(np.mod(T.y, 0.1) - 0.05) - 0.045)
    return np.clip(gx + gy, 0, 1)


def tapa_holo(T):
    rgb = T.fill(C('#1E3442'))
    r = np.hypot(T.x, T.y)
    rgb = mix(rgb, '#4FB8D8', _grid(T, 0.004, 0.008) * smoothstep(0.1, 0.4, T.nz) * (1 - smoothstep(0.5, 0.55, r)))
    return light(T, rgb, 0.1, 0.2)


def brillo_tapa(T):
    r = np.hypot(T.x, T.y)
    m = _grid(T, 0.003, 0.007) * smoothstep(0.1, 0.4, T.nz) * (1 - smoothstep(0.5, 0.55, r)) * 0.35
    ring = (1 - smoothstep(0.015, 0.03, np.abs(r - 0.56))) * smoothstep(0.1, 0.4, T.nz)
    return np.maximum(m, ring)[:, None] * C(CIAN)


def mesa_holo():
    b = prop('Mesa_Holo', clay=0.004, seed=121)
    b.add(revolve('Pie', [(0.0, 0.85), (0.16, 0.85), (0.12, 0.6), (0.1, 0.25), (0.34, 0.06), (0.38, 0.0)], 24,
                  clay=0.1, paint=metal(METAL_OSC, 0.15)))
    tapa = cyl('Tapa', (0, 0, 0.85), (0, 0, 0.95), 0.62, segs=32, bevel=0.2, paint=tapa_holo)
    tapa.emit = brillo_tapa
    b.add(tapa)
    lente = ellipsoid('Proyector', (0, 0, 0.955), (0.09, 0.09, 0.03), rings=4, segs=14, paint=paint_flat('#BFF4FF'))
    lente.emit = brillo('#BFF4FF', 1.0)
    b.add(lente)
    for k in range(6):
        a = k * math.pi / 3
        b.add(rounded_box('Panel%d' % k, (0.5 * math.cos(a), 0.5 * math.sin(a), 0.9), (0.12, 0.05, 0.02), e=0.3,
                          rings=2, segs=6, axes=axes_from((0, 0, 1), (math.cos(a), math.sin(a), 0)),
                          paint=paint_flat(NARANJA)))
    return b


# =====================================================================================
# Brazo robótico (3 piezas)
# =====================================================================================

def amarillo(T):
    rgb = T.fill(C('#D9A33A'))
    rgb = spots(T, rgb, '#8C6A2E', freq=20.0, thresh=0.47, soft=0.1, seed=8, amount=0.5)
    return light(T, rgb, 0.16, 0.28)


def brazo_base():
    b = prop('BrazoRobot_Base', clay=0.004, seed=131)
    b.add(cyl('Placa', (0, 0, 0), (0, 0, 0.08), 0.45, segs=24, bevel=0.2, paint=metal(METAL_OSC, 0.2)))
    for k in range(6):
        a = k * math.pi / 3 + 0.3
        p = (0.38 * math.cos(a), 0.38 * math.sin(a))
        b.add(cyl('Perno%d' % k, (p[0], p[1], 0.08), (p[0], p[1], 0.11), 0.025, segs=6, paint=metal('#B9C0C6')))
    b.add(revolve('Torre', [(0.0, 0.5), (0.16, 0.5), (0.2, 0.42), (0.24, 0.12), (0.28, 0.08)], 20, clay=0.1,
                  paint=amarillo))
    b.add(cyl('EjeHombro', (-0.2, 0, 0.5), (0.2, 0, 0.5), 0.12, segs=18, bevel=0.25, paint=metal(METAL_OSC, 0.15)))
    return b


def brazo_tramo():
    b = prop('BrazoRobot_Tramo', clay=0.004, seed=141)
    b.add(rounded_box('Viga', (0, 0, 0.5), (0.16, 0.2, 0.86), e=0.3, rings=6, segs=10, paint=amarillo))
    for z in (0.0, 1.0):
        b.add(cyl('Codo%d' % int(z), (-0.13, 0, z), (0.13, 0, z), 0.11, segs=18, bevel=0.25, paint=metal(METAL_OSC, 0.15)))
    b.add(tube('Cable', [(0.09, 0.08, 0.05), (0.12, 0.13, 0.5), (0.09, 0.08, 0.95)], 0.018, ring=5, samples=6,
               caps=('flat', 'flat'), clay=0.1, paint=paint_flat('#1E1F22')))
    return b


def brazo_pinza():
    b = prop('BrazoRobot_Pinza', clay=0.004, seed=151)
    b.add(cyl('Muneca', (0, 0, 0), (0, 0, 0.14), 0.09, segs=16, bevel=0.25, paint=metal(METAL_OSC, 0.15)))
    b.add(rounded_box('Mano', (0, 0, 0.2), (0.2, 0.12, 0.1), e=0.3, rings=3, segs=8, paint=amarillo))
    for sx in (-1, 1):
        b.add(tube('Dedo%d' % sx, [(sx * 0.07, 0, 0.24), (sx * 0.1, 0, 0.33), (sx * 0.05, 0, 0.42)], [0.025, 0.02, 0.012],
                   ring=6, samples=6, caps=('round', 'round'), clay=0.1, paint=metal(METAL_OSC, 0.2)))
    return b


# =====================================================================================
# Cueva: estalactitas y cristales
# =====================================================================================

def estalactitas():
    b = prop('Estalactitas', clay=0.02, seed=161)
    rng = np.random.RandomState(162)
    b.add(blob('Techo', (0, 0, 0.0), (0.8, 0.6, 0.22), rings=6, segs=12, lump=0.25, seed=3, paint=roca))
    for k in range(7):
        a = rng.uniform(0, 2 * math.pi)
        r = rng.uniform(0.0, 0.5)
        x, y = r * math.cos(a), r * 0.75 * math.sin(a)
        L = rng.uniform(0.45, 1.25)
        w = rng.uniform(0.08, 0.16)
        rmod = (lambda s: (lambda th, t, rr, z: 1 + 0.12 * np.sin(th * 3 + s) * (1 - t)))(k)
        b.add(revolve('Punta%d' % k, [(w, -0.05), (w * 0.85, -L * 0.3), (w * 0.5, -L * 0.7), (w * 0.12, -L), (0.0, -L - 0.03)],
                      9, origin=(x, y, 0), rmod=rmod, clay=0.4, paint=roca))
    return b


def cristal(T):
    rgb = grad(T, '#2C6E8E', C('#9EEBFF'), np.clip(T.nz * 0.5 + (1 - T.u) * 0.5, 0, 1))
    return light(T, rgb, 0.2, 0.2)


def brillo_cristal(T):
    k = 0.35 + 0.65 * (1 - T.u)
    return k[:, None] * C('#6FE6FF')


def cristales():
    b = prop('Cristales_Cueva', clay=0.0, seed=171)
    rng = np.random.RandomState(172)
    b.add(blob('Roca', (0, 0, 0.04), (0.42, 0.36, 0.14), rings=5, segs=10, lump=0.25, seed=7, floor=0.0, paint=roca))
    for k in range(7):
        d = unit(V((rng.uniform(-0.6, 0.6), rng.uniform(-0.6, 0.6), 1.0)))
        base = V((rng.uniform(-0.2, 0.2), rng.uniform(-0.15, 0.15), 0.06))
        L = rng.uniform(0.3, 0.75)
        r = rng.uniform(0.05, 0.1)
        c = revolve('Cristal%d' % k, [(0.0, L + r * 1.6), (r * 0.98, L), (r, r * 0.5), (r * 0.9, 0.0)], 6,
                    origin=base, axes=axes_from(d), clay=0.0, paint=cristal)
        c.emit = brillo_cristal
        b.add(c)
    return b


# =====================================================================================
# Monitores
# =====================================================================================

def pantalla(T):
    return T.fill(C('#0E2230'))


def brillo_pantalla(seed, cz):
    def e(T):
        rng = np.random.RandomState(seed)
        u, w = T.x, T.z - cz
        front = smoothstep(0.5, 0.9, -T.nrm[:, 1])
        out = np.full(len(T.pos), 0.22)
        # Barras de texto, una gráfica que ondula y un marco
        for k in range(6):
            yy = rng.uniform(-0.22, 0.02)
            x0, x1 = rng.uniform(-0.45, -0.1), rng.uniform(0.0, 0.45)
            out += 0.6 * (1 - smoothstep(0.006, 0.012, np.abs(w - yy))) * smoothstep(x0, x0 + 0.01, u) * (1 - smoothstep(x1, x1 + 0.01, u))
        wave = 0.06 * np.sin(u * 18.0 + seed) + 0.03 * np.sin(u * 47.0)
        out += 0.9 * (1 - smoothstep(0.004, 0.012, np.abs(w - 0.14 - wave)))
        return (np.clip(out, 0, 1) * front)[:, None] * C('#58D8FF')
    return e


def pantallas():
    b = prop('Pantallas_Taller', clay=0.003, seed=181)
    b.add(cyl('Pie', (0, 0.1, 0), (0, 0.1, 0.06), 0.35, segs=20, bevel=0.2, paint=metal(METAL_OSC, 0.15)))
    b.add(cyl('Poste', (0, 0.1, 0.06), (0, 0.1, 1.5), 0.05, segs=10, paint=metal(METAL_OSC, 0.15)))
    specs = [('Central', (0, 0, 1.55), (1.1, 0.06, 0.65), 0.0, 7), ('Izq', (-0.92, 0.12, 1.55), (0.7, 0.06, 0.5), 25.0, 13),
             ('Der', (0.92, 0.12, 1.55), (0.7, 0.06, 0.5), -25.0, 21)]
    for name, c, size, yaw, seed in specs:
        a = math.radians(yaw)
        axes = axes_from((0, 0, 1), (-math.sin(a), math.cos(a), 0))
        b.add(rounded_box('Marco' + name, c, (size[0] + 0.06, size[1], size[2] + 0.06), e=0.12, rings=8, segs=12, axes=axes,
                          paint=metal(METAL_OSC, 0.1)))
        fwd = V((math.sin(a), -math.cos(a), 0)) * 0.032
        s = rounded_box('Pantalla' + name, V(c) + fwd, (size[0], 0.012, size[2]), e=0.06, rings=8, segs=12, axes=axes,
                        paint=pantalla)
        s.emit = brillo_pantalla(seed, c[2])
        b.add(s)
    return b


CONSTRUIR = {'Mesa_Chatarra': mesa_chatarra, 'Vitrina_Traje': vitrina, 'Mesa_Holo': mesa_holo,
             'BrazoRobot_Base': brazo_base, 'BrazoRobot_Tramo': brazo_tramo, 'BrazoRobot_Pinza': brazo_pinza,
             'Estalactitas': estalactitas, 'Cristales_Cueva': cristales, 'Pantallas_Taller': pantallas}
EMISION = {'Vitrina_Traje': (0.4, 1.7, 2.0), 'Mesa_Holo': (0.3, 1.3, 1.6), 'Cristales_Cueva': (0.4, 1.6, 2.0),
           'Pantallas_Taller': (0.35, 1.5, 2.0)}


if __name__ == '__main__':
    nombres = [a for a in sys.argv[1:] if not a.startswith('--')] or TODOS
    for n in nombres:
        run_prop(n, CONSTRUIR[n], res=512, smoothness=0.45, ao_distance=0.15, bake_range=3.0, grain=0.025, blotch=0.04,
                 emission=EMISION.get(n))
