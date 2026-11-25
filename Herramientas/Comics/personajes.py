# -*- coding: utf-8 -*-
"""Personajes de las viñetas, dibujados a lápiz pero PARECIDOS A LOS MODELOS 3D DEL JUEGO (no iguales: se guían por
sus formas y colores). Cada uno en los dos modos del Lienzo: 'palito' (guion gráfico) y 'lapiz' (dibujo terminado).

  - Peces (Assets/Models/PezNormal): parados como una gota (redondos arriba, en punta abajo), ojos saltones, una
    sonrisita y aletas AZULES acanaladas: dos a los lados y dos abajo. Rojos, dorados, rosas, verdes, turquesas, violeta.
  - El protagonista (Assets/Models/Animations/PESCAO.fbx): la misma gota con capucha gris carbón, la cara roja y los
    ojos enojados. Con el TRAJE: armadura gris azulada en placas, casco de vidrio redondo, "auriculares" redondos a
    los lados, luz roja en el pecho, mochila propulsora atrás (de ahí sale el fuego).
  - Defensores (Traje_Defensor): traje blanco de cúpula con visor oscuro y un cañón arriba.
  - Aves (Assets/Models/Aves): "de peluche": cuerpo redondo, casi sin cuello, ojos saltones grandes, alas largas y
    rectas con las puntas oscuras. La Garza Gris (blanca y gris, cejas negras, pico largo), el Pelícano (blanco, bolsa
    naranja con manchas), el Cormorán Rey (morado oscuro, alas verde azulado con escamas, collar rojo, corona) y la
    Reina Águila (café, cabeza blanca, corona, garras amarillas).
  - El bagre mecánico (Mecanico_Bagre): gris y parado, lentes dorados redondos, delantal café y bigotes largos.

Todos miran a la derecha por defecto (flip=True: a la izquierda). x, y = centro; s = tamaño en píxeles (alto en los
peces, largo del cuerpo en las aves). prof (0 cerca .. 1 lejos) aclara el trazo.
"""
import numpy as np
from lapiz import T, spline, elipse, banda, menos, mas, dentro
from objetos import *  # noqa: F401,F403  (los objetos del lago y del Taller, para escenas.py)
from objetos import pieza, pw, lejos, gota, brillo, llave, signo, AGUA, VIDRIO

# ======================= COLORES (de las texturas de los modelos) =======================

ALETA = (0.24, 0.42, 0.82)
CAPUCHA = (0.27, 0.28, 0.33)
CARA_HEROE = (0.80, 0.35, 0.22)      # naranja rojizo (del dibujo 2D de referencia)
OJO_HEROE = (0.97, 0.91, 0.78)       # el blanco de los ojos, crema
NAVY = (0.16, 0.23, 0.45)            # la armadura del traje
NAVY_OSC = (0.11, 0.16, 0.34)
NAVY_CLARO = (0.22, 0.33, 0.61)      # biseles, borde del casco
ALETA_HEROE = (0.18, 0.37, 0.80)
ALETA_HEROE_OSC = (0.13, 0.27, 0.62)
LUZ_PECHO = (1.0, 0.44, 0.24)
LUZ_MARCO_COL = (0.80, 0.20, 0.12)
ARMADURA = (0.66, 0.71, 0.82)
ARMADURA_OSC = (0.50, 0.55, 0.67)
LUZ_ROJA = (1.0, 0.36, 0.30)
VIDRIO_CASCO = (0.80, 0.92, 1.0)
FUEGO = (1.0, 0.72, 0.35)
BLANCO_TRAJE = (0.95, 0.96, 0.97)
VISOR = (0.30, 0.34, 0.44)
AZUL_LUZ = (0.55, 0.86, 1.0)
# Los peces del lago (PezNormal, PezDorado, PezRosa, PezVerde, PezTurquesa, PezVioleta)
COLORES_PEZ = [(0.90, 0.42, 0.32), (0.95, 0.72, 0.34), (0.88, 0.50, 0.78), (0.48, 0.76, 0.44), (0.36, 0.76, 0.82),
               (0.64, 0.50, 0.88)]

# ======================= PECES =======================

_DER = [(0.0, -0.50), (0.20, -0.47), (0.33, -0.38), (0.40, -0.22), (0.41, -0.04), (0.37, 0.13), (0.28, 0.29),
        (0.15, 0.42), (0.0, 0.50)]
HUEVO = _DER + [(-x, y) for x, y in reversed(_DER[1:-1])]          # la gota del cuerpo (alto 1)
ALETA_ABANICO = [(0.0, -0.05), (0.12, -0.10), (0.24, -0.13), (0.31, -0.10), (0.34, -0.03), (0.32, 0.05), (0.26, 0.10),
                 (0.13, 0.10), (0.0, 0.05)]
MOCHILA = [(-0.16, -0.42), (-0.42, -0.36), (-0.53, -0.14), (-0.50, 0.12), (-0.34, 0.25), (-0.20, 0.05)]
ALETA_GRIS = [(0, -0.05), (0.2, -0.08), (0.24, 0.02), (0.0, 0.05)]


def ancho_huevo(y):
    """Medio ancho de la gota a la altura y (para las líneas de las placas)."""
    return float(np.interp(y, [q[1] for q in _DER], [q[0] for q in _DER]))


def aleta_azul(c, t, p, color=ALETA):
    """Aleta azul acanalada (como las del modelo): un abanico con nervios."""
    pieza(c, t, ALETA_ABANICO, p * 0.8, color=color, fuerza=0.8, sombra=0.25, n=6)
    if not c.palito:
        for q in ((0.27, -0.11), (0.33, -0.035), (0.30, 0.06)):
            c.trazo(t([(0.04, 0.0), q]), peso=p * 0.45, presion=0.75, suave=False)


def ojo_saltón(c, cx, cy, R, mirada=(0.3, 0.0), expr='normal', peso=1.0, esclera=(1.0, 1.0, 1.0), flip=False):
    """Ojo grande y redondo de los peces y las aves del juego (blanco, pupila negra con brillo)."""
    dx = mirada[0] * (-1 if flip else 1)
    dy = mirada[1]
    if c.palito:
        c.contorno(elipse(cx, cy, R, R, n=16), peso=peso * 0.8)
        if expr not in ('feliz', 'dormido'):
            c.trazo(elipse(cx + dx * R * 0.4, cy + dy * R * 0.4, R * 0.25, R * 0.25, n=8), peso=peso * 1.3, cerrado=True,
                    suave=False)
        return
    if expr == 'feliz':
        c.trazo(elipse(cx, cy + R * 0.25, R * 0.8, R * 0.7, a0=200, a1=340, n=14), peso=peso * 1.1, presion=0.95)
        return
    if expr == 'dormido':
        c.trazo(elipse(cx, cy - R * 0.1, R * 0.8, R * 0.55, a0=20, a1=160, n=14), peso=peso * 1.1, presion=0.95)
        return
    circ = elipse(cx, cy, R, R, n=30)
    c.tapar(circ)
    c.aguada(circ, esclera, 0.6)
    c.contorno(circ, peso=peso * 0.85, presion=0.85)
    if expr == 'mareado':
        esp = [(cx + np.cos(a) * R * 0.08 * a, cy + np.sin(a) * R * 0.08 * a) for a in np.linspace(0, 10, 40)]
        c.trazo(esp, peso=peso * 0.7, suave=False)
        return
    pr = R * (0.3 if expr in ('asustado', 'sorpresa') else 0.48)
    px, py = cx + dx * R * 0.42, cy + dy * R * 0.42
    pup = elipse(px, py, pr, pr, n=18)
    c.tono(pup, 1.0, suave=0.15, mancha=0.02)
    c.contorno(pup, peso=peso * 0.6)
    c.tapar(elipse(px - pr * 0.35, py - pr * 0.4, pr * 0.32, pr * 0.32, n=10))


def parpado_enojado(c, cx, cy, R, interior, color, peso):
    """Párpado caído en diagonal (más bajo hacia el centro de la cara): la mirada enojada del protagonista."""
    a = (cx - interior * R * 1.15, cy - R * 0.02)
    b = (cx + interior * R * 1.15, cy - R * 0.75)
    if c.palito:
        c.trazo([a, b], peso=peso * 1.5, suave=False)
        return
    # solo la parte del ojo que queda arriba de la línea del párpado
    tapa = [a, b, (b[0], cy - R * 1.4), (a[0], cy - R * 1.4)]
    m = dentro(c.mascara(tapa), c.mascara(elipse(cx, cy, R * 1.06, R * 1.06, n=30)))
    c.tapar(m)
    c.aguada(m, color, 0.85, suave=0.3)
    c.trazo([a, b], peso=peso * 1.6, presion=1.0, suave=False)


ENOJADO = ('enojado', 'decidido', 'normal', 'malo')


def _ojos_boca_heroe(c, t, cx, cy, R, expr, mirada, p, vista):
    """Ojos y boca del protagonista (como el modelo): ojos grandes con el blanco oliva, pupila negra grande con un
    brillo, el párpado grueso en diagonal (enojado) y la boca fruncida. (cx, cy, R) = centro y radio de la cara."""
    s = t.s
    k = 1 - 0.3 * vista
    pts_cara = t.p(cx, cy)
    for lado in (-1, 1):
        lejano = (lado < 0)
        u = cx + lado * 0.475 * R * k
        v = cy - 0.14 * R
        q = t.p(u, v)
        rx = 0.29 * R * s * (1 - 0.25 * vista if lejano else 1)
        ry = 0.25 * R * s
        if expr in ('feliz',):
            c.trazo(elipse(q[0], q[1] + ry * 0.3, rx * 0.85, ry * 0.8, a0=200, a1=340, n=14), peso=p * 1.2, presion=1.0)
            continue
        if expr == 'dormido':
            c.trazo(elipse(q[0], q[1] - ry * 0.1, rx * 0.85, ry * 0.6, a0=20, a1=160, n=14), peso=p * 1.2, presion=1.0)
            continue
        ojo = elipse(q[0], q[1], rx, ry, n=32)
        c.tapar(ojo)
        if not c.palito:
            c.aguada(ojo, OJO_HEROE, 0.85)
        c.contorno(ojo, peso=p * 0.9, presion=0.9)
        asustado = expr in ('sorpresa', 'asustado')
        pr = ry * (0.42 if asustado else 0.64)
        hacia = 1 if q[0] < pts_cara[0] else -1          # las pupilas miran un poco hacia el centro (ceño)
        dx = (mirada[0] * (-1 if t.flip else 1)) * rx * 0.25 + (0 if asustado else hacia * rx * 0.18)
        px, py = q[0] + dx, q[1] + ry * (0.05 if not asustado else 0.0) + mirada[1] * ry * 0.3
        pup = elipse(px, py, pr, pr, n=20)
        c.tono(pup, 1.0, suave=0.15, mancha=0.02)
        c.contorno(pup, peso=p * 0.6)
        c.tapar(elipse(px + pr * 0.38, py - pr * 0.38, pr * 0.26, pr * 0.26, n=10))
        if not asustado:
            # párpado grueso: más bajo hacia el centro de la cara
            a, b = (q[0] + hacia * rx * 1.12, q[1] - ry * 0.02), (q[0] - hacia * rx * 1.12, q[1] - ry * 0.80)
            if c.palito:
                c.trazo([a, b], peso=p * 1.5, suave=False)
            else:
                tapa = [a, b, (b[0], q[1] - ry * 1.5), (a[0], q[1] - ry * 1.5)]
                m = dentro(c.mascara(tapa), c.mascara(elipse(q[0], q[1], rx * 1.07, ry * 1.07, n=30)))
                c.tapar(m)
                c.aguada(m, CARA_HEROE, 0.9, suave=0.3)
                c.trazo([a, b], peso=p * 1.9, presion=1.0, suave=False)
                # el pliegue del ceño encima
                c.trazo([(a[0] + hacia * rx * 0.1, a[1] - ry * 0.45), (b[0] - hacia * rx * 0.1, b[1] - ry * 0.4)],
                        peso=p * 0.6, presion=0.5, suave=False)
    m = t.p(cx - 0.02 * R, cy + 0.36 * R)
    w = 0.36 * R * s * k
    if expr == 'feliz':
        c.trazo(elipse(m[0], m[1] - w * 0.3, w * 0.7, w * 0.45, a0=20, a1=160, n=12), peso=p * 0.9)
    elif expr in ('sorpresa', 'asustado'):
        P = elipse(m[0], m[1], w * 0.25, w * 0.32, n=12)
        c.tapar(P)
        c.tono(P, 0.75, suave=0.3)
        c.contorno(P, peso=p * 0.6)
    elif expr == 'dormido':
        c.trazo([(m[0] - w * 0.3, m[1]), (m[0] + w * 0.3, m[1])], peso=p * 0.8, suave=False)
    else:
        c.trazo(spline([(m[0] - w, m[1] + w * 0.16), (m[0] - w * 0.1, m[1] - w * 0.14), (m[0] + w, m[1] + w * 0.16)],
                       n=8), peso=p * 1.0, presion=1.0)


def _cara_heroe(c, t, fx, s, expr, mirada, p, vista):
    """Sin traje: la cara naranja en la abertura de la capucha gris carbón."""
    cara = elipse(*t.p(fx, -0.17), 0.27 * (1 - 0.25 * vista) * s, 0.25 * s, n=36)
    c.tapar(cara)
    if not c.palito:
        c.aguada(cara, CARA_HEROE, 0.88)
        c.rayado(menos(c.mascara(cara), c.mascara(cara + [-0.03 * s, -0.05 * s])), espacio=4, presion=0.3)
    c.contorno(cara, peso=p * 0.8, presion=0.8)
    _ojos_boca_heroe(c, t, fx, -0.17, 0.26, expr, mirada, p, vista)


def llama(c, t, largo=0.8):
    """Fuego del propulsor (sale hacia +u de la transformación)."""
    p = pw(t.s)
    if c.palito:
        pts = [(0, 0)] + [(largo * k / 6, (0.08 if k % 2 else -0.08)) for k in range(1, 7)]
        c.trazo(t(pts), peso=p, suave=False)
        return
    ext = [(0.0, -0.10), (largo * 0.35, -0.14), (largo * 0.7, -0.07), (largo, 0.0), (largo * 0.7, 0.07),
           (largo * 0.35, 0.14), (0.0, 0.10)]
    P = spline(t(ext), cerrado=True, n=6)
    c.tapar(P)
    c.aguada(P, FUEGO, 0.8)
    c.contorno(P, peso=p * 0.7, presion=0.7)
    inn = [(0.0, -0.05), (largo * 0.3, -0.06), (largo * 0.55, 0.0), (largo * 0.3, 0.06), (0.0, 0.05)]
    P2 = spline(t(inn), cerrado=True, n=6)
    c.aguada(P2, (1.0, 0.95, 0.65), 0.7)
    c.contorno(P2, peso=p * 0.45, presion=0.5)


def pistola_agua(c, t, chorro=0.0):
    """Pistola de agua del traje (gris azulada, con su tanque) y el chorro."""
    p = pw(t.s)
    if c.palito:
        c.trazo(t([(0, 0), (0.32, 0)]), peso=p * 1.3, suave=False)
        for k in range(int(chorro * 6)):
            c.trazo(t([(0.4 + k * 0.35, 0), (0.6 + k * 0.35, 0)]), peso=p, suave=False)
        return
    if chorro > 0:
        L = chorro
        eje = [(0.36, 0.0), (0.36 + L * 0.35, -0.03), (0.36 + L * 0.7, 0.02), (0.36 + L, 0.0)]
        P = t(banda(eje, 0.07, 0.13))
        c.tapar(P)
        c.aguada(P, AGUA, 0.6)
        c.contorno(P, peso=p * 0.6, presion=0.6)
        for v in (-0.02, 0.02):
            c.trazo(t([(0.4, v), (0.36 + L * 0.5, v + 0.01), (0.36 + L * 0.9, v)]), peso=p * 0.35, presion=0.4)
        for k in range(9):
            q = t.p(0.36 + L * c.rng.uniform(0.5, 1.15), c.rng.normal(0, 0.09))
            r = c.rng.uniform(0.015, 0.035) * t.s
            P = elipse(q[0], q[1], r, r, n=10)
            c.aguada(P, AGUA, 0.6)
            c.contorno(P, peso=p * 0.4)
    pieza(c, t, [(0.03, 0.03), (0.11, 0.03), (0.07, 0.16), (-0.02, 0.15)], p * 0.7, color=ARMADURA_OSC, fuerza=0.7)
    pieza(c, t, [(-0.04, -0.05), (0.30, -0.045), (0.30, 0.045), (-0.04, 0.05)], p * 0.75, color=ARMADURA, fuerza=0.75,
          sombra=0.3, n=3)
    pieza(c, t, [(0.30, -0.03), (0.38, -0.025), (0.38, 0.025), (0.30, 0.03)], p * 0.6, color=AZUL_LUZ, fuerza=0.7, n=2)
    q = t.p(0.10, -0.10)
    tanque = elipse(q[0], q[1], 0.07 * t.s, 0.06 * t.s, n=20)
    c.tapar(tanque)
    c.aguada(tanque, AGUA, 0.7)
    c.contorno(tanque, peso=p * 0.6)


def pez(c, x, y, s, rot=0.0, flip=False, heroe=False, traje=False, color=None, expr=None, mirada=(0.35, 0.0),
        aleta=0.0, pistola=False, chorro=0.0, fuego=False, prof=0.0, boca=None, escamas=True, fuerza=None,
        defensor=False, vista=0.4):
    """Un pez del juego: parado como una gota (x, y = centro; s = alto). rot = cuánto se inclina (grados).
    heroe: el protagonista (capucha gris y cara roja); traje: con la armadura; defensor: con el traje blanco.
    vista: 0 de frente, 0,4 de tres cuartos (la cara se corre hacia donde mira)."""
    t = T(x, y, s, rot, flip)
    p = pw(s * 0.9)
    expr = expr or ('enojado' if heroe else 'normal')
    fx = 0.30 * vista
    with lejos(c, prof):
        if traje:
            _heroe_traje(c, t, s, p, expr, mirada, vista, fuego, pistola, chorro, aleta)
            return t
        if c.palito:
            _pez_palito(c, t, s, p, heroe, traje, defensor, expr, mirada, fx, pistola, chorro, fuego, aleta)
            return t
        # fuego de la mochila (sale para abajo, detrás)
        if fuego and (traje or defensor):
            base = (-0.34, 0.28) if vista > 0.1 else (0.0, 0.52)
            llama(c, t.hijo(base[0], base[1], 1.0, 90), largo=0.75)
        # aleta de atrás y las de la cola
        abajo = 0.26 if defensor else 0.44
        aleta_azul(c, t.hijo(-0.36 if not defensor else -0.46, 0.05 if not defensor else 0.16, 0.85, 160), p)
        aleta_azul(c, t.hijo(-0.05, abajo, 0.8, 112), p)
        aleta_azul(c, t.hijo(0.05, abajo, 0.8, 68), p)
        if traje and vista > 0.1:
            pieza(c, t, MOCHILA, p, color=ARMADURA_OSC, fuerza=0.8, sombra=0.4)
            pieza(c, t, [(-0.42, 0.18), (-0.28, 0.20), (-0.30, 0.30), (-0.42, 0.28)], p * 0.8, color=(0.4, 0.42, 0.5),
                  fuerza=0.8, n=2)
        # cuerpo
        if traje:
            col, fz = ARMADURA, 0.8
        elif defensor:
            col, fz = BLANCO_TRAJE, 0.6
        elif heroe:
            col, fz = CAPUCHA, 0.8
        else:
            col, fz = (color or COLORES_PEZ[0]), (fuerza if fuerza is not None else 0.75)
        if not defensor:
            pieza(c, t, HUEVO, p * 1.05, color=col, fuerza=fz, sombra=0.42, k=0.07)
        if defensor:
            _traje_defensor(c, t, s, p, fx, color)
        elif heroe:
            _cara_heroe(c, t, fx, s, expr, mirada, p, vista)
        else:
            # los ojos saltones arriba, la sonrisita y unas líneas suaves (como la textura del modelo)
            for v in (0.06, 0.16):
                w = ancho_huevo(v) * 0.85
                c.trazo(spline(t([(-w, v - 0.02), (0.0, v + 0.02), (w, v - 0.02)]), n=6), peso=p * 0.4, presion=0.4)
            for lado, sep, esc in ((-1, 0.12 * (1 - 0.4 * vista), 0.9), (1, 0.12, 1.0)):
                q = t.p(fx + lado * sep, -0.26)
                ojo_saltón(c, q[0], q[1], 0.10 * esc * s, mirada, expr, p, flip=t.flip)
            m = t.p(fx + 0.02, -0.10)
            b = boca or ('o' if expr in ('asustado', 'sorpresa') else 'sonrisa')
            if b == 'o':
                P = elipse(m[0], m[1], 0.025 * s, 0.032 * s, n=12)
                c.tapar(P)
                c.tono(P, 0.7, suave=0.3)
                c.contorno(P, peso=p * 0.5)
            elif b == 'plana':
                c.trazo([(m[0] - 0.04 * s, m[1]), (m[0] + 0.04 * s, m[1])], peso=p * 0.6, suave=False)
            else:
                c.trazo(elipse(m[0], m[1] - 0.03 * s, 0.06 * s, 0.035 * s, a0=20, a1=160, n=10), peso=p * 0.6)
        # aleta de adelante (el "brazo"); con la pistola, la sostiene
        ta = t.hijo(0.36, 0.05, 0.85, 20 + aleta) if not defensor else t.hijo(0.46, 0.16, 0.85, 20 + aleta)
        if pistola:
            pistola_agua(c, ta.hijo(0.24, 0.0, 1.0, -20 - aleta), chorro=chorro)
        aleta_azul(c, ta, p)
    return t


# ---------- El protagonista con el traje (como la imagen del modelo 3D) ----------
# Coordenadas: alto total 1 (del borde del casco a la punta de las aletas de abajo), centro en 0, mirando al frente.
# Medidas tomadas del dibujo 2D de referencia (alto total 1, centro en 0).
H_CABEZA = (0.0, -0.25, 0.225, 0.21)                 # centro x, y y radios de la cabeza (cúpula)
_CAB_DER = [(0.0, -0.457), (0.13, -0.445), (0.20, -0.395), (0.225, -0.32), (0.228, -0.20), (0.22, -0.13), (0.0, -0.11)]
H_CABEZA_PTS = _CAB_DER + [(-x, y) for x, y in reversed(_CAB_DER[1:-1])]
H_CASCO = [(-0.258, -0.29), (-0.25, -0.40), (-0.175, -0.478), (0.0, -0.500), (0.175, -0.478), (0.25, -0.40),
           (0.258, -0.29), (0.226, -0.29), (0.205, -0.385), (0.135, -0.445), (0.0, -0.459), (-0.135, -0.445),
           (-0.205, -0.385), (-0.226, -0.29)]
H_CASCO_LINEA = [(-0.243, -0.30), (-0.232, -0.392), (-0.16, -0.462), (0.0, -0.481), (0.16, -0.462), (0.232, -0.392),
                 (0.243, -0.30)]
H_CASCO_EXT = (0.255, 0.25)
H_OREJERA = (0.293, -0.25, 0.081, 0.20)              # auricular: centro (x a cada lado), ancho y alto
_ARM_DER = [(0.0, -0.10), (0.24, -0.10), (0.28, -0.02), (0.285, 0.10), (0.26, 0.19), (0.22, 0.26), (0.16, 0.32),
            (0.08, 0.36), (0.0, 0.372)]
H_ARMADURA = _ARM_DER + [(-x, y) for x, y in reversed(_ARM_DER[1:-1])]
H_CUELLO = [(-0.262, -0.158), (-0.13, -0.142), (0.0, -0.137), (0.13, -0.142), (0.262, -0.158), (0.272, -0.086),
            (0.13, -0.076), (0.0, -0.073), (-0.13, -0.076), (-0.272, -0.086)]
H_CUELLO_SEP = [(0.10, -0.140), (0.105, -0.076)], [(0.20, -0.150), (0.21, -0.080)]
H_HOMBRERA = [(0.215, -0.118), (0.31, -0.126), (0.338, -0.10), (0.338, 0.03), (0.30, 0.046), (0.228, 0.03)]
H_HOMBRERA_TAPA = [(0.215, -0.118), (0.31, -0.126), (0.338, -0.10), (0.24, -0.09)]
H_ALETA = [(0.0, 0.0), (0.251, 0.06), (0.242, 0.102), (0.134, 0.27), (0.109, 0.26), (0.028, 0.149), (-0.013, 0.084)]
H_ALETA_TAPA = [(0.0, -0.005), (0.252, 0.056), (0.245, 0.098), (0.0, 0.042)]
H_ALETA_NERVIOS = [((0.01, 0.06), (0.225, 0.14)), ((0.0, 0.10), (0.18, 0.21))]
H_COLA = [(0.12, 0.28), (0.17, 0.30), (0.21, 0.39), (0.235, 0.48), (0.17, 0.50), (0.10, 0.50), (0.07, 0.42), (0.06, 0.34)]
H_COLA_NERVIOS = [((0.10, 0.31), (0.20, 0.47)), ((0.08, 0.35), (0.14, 0.49))]
H_COLA_INT = [(0.0, 0.36), (0.05, 0.395), (0.065, 0.47), (0.02, 0.478), (0.0, 0.42)]
H_LUZ_MARCO = [(-0.10, -0.05), (0.10, -0.05), (0.088, 0.045), (-0.088, 0.045)]
H_LUZ = [(-0.078, -0.036), (0.078, -0.036), (0.07, 0.032), (-0.07, 0.032)]
H_PLACAS = [[(0.10, -0.05), (0.15, -0.075), (0.22, -0.065)], [(0.15, -0.075), (0.17, 0.03), (0.088, 0.06)],
            [(0.17, 0.03), (0.27, 0.06)], [(0.0, 0.06), (0.10, 0.06), (0.13, 0.15), (0.10, 0.19), (0.0, 0.19)],
            [(0.13, 0.15), (0.255, 0.165)], [(0.0, 0.215), (0.12, 0.21), (0.235, 0.20)],
            [(0.0, 0.275), (0.10, 0.272), (0.19, 0.262)], [(0.05, 0.215), (0.05, 0.275)], [(0.12, 0.275), (0.12, 0.325)]]
H_BISELES = [[(0.0, 0.07), (0.098, 0.07), (0.12, 0.14)], [(0.0, 0.225), (0.12, 0.22), (0.23, 0.21)],
             [(0.0, 0.285), (0.10, 0.282), (0.18, 0.272)]]
H_RECTANGULO = [(0.04, 0.335), (0.09, 0.335), (0.09, 0.352), (0.04, 0.352)]
H_MOCHILA = [(-0.18, -0.12), (-0.36, -0.10), (-0.40, 0.04), (-0.37, 0.17), (-0.26, 0.22), (-0.18, 0.10)]


def _espejo(pts, m=-1):
    return [(m * x, y) for x, y in pts]


def _brillo_luz(c, forma, rgb, fuerza, suave):
    """Resplandor suave de color (la luz del pecho, el fuego)."""
    if c.palito:
        return
    m = c.mascara(forma, suave)
    c._mezclar_tinte(np.clip(m.a * fuerza, 0, 1)[..., None], np.asarray(rgb, np.float32), m.x0, m.y0, multiplicar=False)


def _heroe_traje(c, t, s, p, expr, mirada, vista, fuego, pistola, chorro, aleta, vacio=False):
    """El protagonista con el traje, como su dibujo 2D de referencia: cabeza en cúpula naranja con el borde del casco
    en dos tonos y los auriculares de tres láminas, cuello en segmentos, hombreras, armadura azul marino en placas con
    biseles claros, la luz naranja en trapecio del pecho y las aletas azules (con su tapa azul marino y tres bandas a
    los costados, dos abajo y otras dos más chicas detrás). vacio: el traje sin el pez (en la vitrina).
    En tres cuartos (vista > 0) la cara y el pecho se corren hacia donde mira y se esconde el auricular de ese lado."""
    fx = 0.20 * vista
    fc = fx * 0.55
    ce = 1 - 0.25 * vista                     # la cara se angosta un poco en tres cuartos
    hx, hy, hr, hry = H_CABEZA

    def cab_pts(pts):
        return [(hx + fx + x * ce, y) for x, y in pts]

    if c.palito:
        c.contorno(t(cab_pts(H_CABEZA_PTS)), peso=p)
        c.contorno(t(H_ARMADURA), peso=p)
        c.contorno(t([(x + fc, y) for x, y in H_LUZ_MARCO]), peso=p, suave=False)
        if not vacio:
            for m in (-1, 1):
                tf = T(*t.p(m * 0.276, -0.03), s, t.rot, t.flip if m > 0 else not t.flip)
                c.contorno(tf(H_ALETA), peso=p * 0.8)
                c.contorno(t(_espejo(H_COLA, m)), peso=p * 0.8)
            _ojos_boca_heroe(c, t, hx + fx, hy, hr, expr, mirada, p, vista)
        if fuego:
            llama(c, t.hijo(0.0, 0.40, 1.0, 90), largo=0.5)
        if pistola:
            pistola_agua(c, t.hijo(0.46, 0.14, 0.8, 0), chorro=chorro)
        return
    # fuego (abajo, entre las aletas; en tres cuartos sale de la mochila)
    if fuego:
        base = (-0.32, 0.20, 105) if vista > 0.15 else (0.0, 0.38, 90)
        tl = t.hijo(base[0], base[1], 1.0, base[2])
        _brillo_luz(c, tl(elipse(0.35, 0, 0.45, 0.22, n=24)), (1.0, 0.75, 0.40), 0.5, 10)
        llama(c, tl, largo=0.62)
    if vista > 0.15:
        pieza(c, t, H_MOCHILA, p, color=NAVY_OSC, fuerza=0.95, sombra=0.4)
        pieza(c, t, [(-0.36, 0.15), (-0.27, 0.17), (-0.28, 0.24), (-0.36, 0.22)], p * 0.8, color=NAVY, fuerza=0.95, n=2)
    if not vacio:
        # aletas de abajo (las de atrás, más chicas y oscuras) y las de los costados
        for m in (-1, 1):
            pieza(c, t, _espejo(H_COLA_INT, m), p * 0.9, color=ALETA_HEROE_OSC, fuerza=0.95, n=4)
        for m in (-1, 1):
            pieza(c, t, _espejo(H_COLA, m), p, color=ALETA_HEROE, fuerza=0.95, sombra=0.25, n=5)
            for (a0, a1) in H_COLA_NERVIOS:
                c.trazo(t([(m * a0[0], a0[1]), (m * a1[0], a1[1])]), peso=p * 0.55, presion=0.9, suave=False)
        for m in (-1, 1):
            giro = (aleta if m > 0 else 0.0) * (-1 if t.flip else 1)
            tf = T(*t.p(m * 0.276, -0.03), s * 1.15, t.rot + giro, t.flip if m > 0 else not t.flip)
            pieza(c, tf, H_ALETA, p, color=ALETA_HEROE, fuerza=0.95, sombra=0.25, n=4)
            for (a0, a1) in H_ALETA_NERVIOS:
                c.trazo(tf([a0, a1]), peso=p * 0.55, presion=0.9, suave=False)
            pieza(c, tf, H_ALETA_TAPA, p * 0.9, color=NAVY, fuerza=0.95, n=3)
    # la armadura con sus placas y biseles
    pieza(c, t, H_ARMADURA, p * 1.1, color=NAVY, fuerza=0.95, sombra=0.4, k=0.05)
    for linea in H_BISELES:
        for m in (-1, 1):
            c.aclarar(t(banda([(m * x + fc, y) for x, y in linea], 0.012)), 0.22, suave=0.3)
    for linea in H_PLACAS:
        for m in (-1, 1):
            c.trazo(t([(m * x + fc, y) for x, y in linea]), peso=p * 0.6, presion=0.9, suave=False)
    for m in (-1, 1):
        pieza(c, t, [(m * x + fc, y) for x, y in H_RECTANGULO], p * 0.5, color=NAVY_OSC, fuerza=0.95, n=1)
    # la luz naranja del pecho (brilla)
    q = t.p(fc, -0.003)
    if not vacio:
        _brillo_luz(c, elipse(q[0], q[1], 0.15 * s, 0.085 * s, n=30), (1.0, 0.42, 0.25), 0.45, 6)
    pieza(c, t, [(x + fc, y) for x, y in H_LUZ_MARCO], p * 0.8, color=LUZ_MARCO_COL if not vacio else NAVY_OSC,
          fuerza=0.95, n=1)
    luz = np.asarray(t([(x + fc, y) for x, y in H_LUZ]), float)
    c.tapar(luz)
    c.aguada(luz, LUZ_PECHO if not vacio else (0.45, 0.30, 0.30), 1.0)
    if not vacio:
        _brillo_luz(c, elipse(q[0], q[1], 0.05 * s, 0.016 * s, n=16), (1.0, 0.72, 0.45), 0.9, 1.5)
    c.contorno(luz, peso=p * 0.5)
    # hombreras (con su cara de arriba más clara)
    for m in (-1, 1):
        pieza(c, t, _espejo(H_HOMBRERA, m), p, color=NAVY, fuerza=0.95, sombra=0.3, k=0.04)
        pieza(c, t, _espejo(H_HOMBRERA_TAPA, m), p * 0.6, color=NAVY_CLARO, fuerza=0.95, n=3)
    # la cabeza (o el casco vacío)
    cab = spline(t(cab_pts(H_CABEZA_PTS)), cerrado=True, n=8)
    c.tapar(cab)
    if vacio:
        c.aguada(cab, (0.10, 0.12, 0.20), 1.0)
    else:
        c.aguada(cab, CARA_HEROE, 1.0)
    c.contorno(cab, peso=p)
    if not vacio:
        _ojos_boca_heroe(c, t, hx + fx, hy, hr, expr, mirada, p, vista)
    # el cuello en segmentos
    pieza(c, t, [(x * (1 - 0.1 * vista) + fc * 0.7, y) for x, y in H_CUELLO], p, color=NAVY, fuerza=0.95, sombra=0.25,
          k=0.03, n=5)
    for sep in H_CUELLO_SEP:
        for m in (-1, 1):
            c.trazo(t([(m * x + fc * 0.7, y) for x, y in sep]), peso=p * 0.6, presion=0.9, suave=False)
    # el borde del casco (dos tonos) por arriba de la cabeza
    casco = spline(t(cab_pts(H_CASCO)), cerrado=True, n=4)
    c.tapar(casco)
    c.aguada(casco, NAVY_CLARO, 1.0)
    c.contorno(casco, peso=p * 0.9)
    c.trazo(spline(t(cab_pts(H_CASCO_LINEA)), n=6), peso=p * 0.55, presion=0.9)
    # los auriculares de tres láminas (el del lado que mira se esconde en tres cuartos)
    ox, oy, ow, oh = H_OREJERA
    for m in (-1, 1):
        if m > 0 and vista > 0.3:
            continue
        cx = hx + fx + m * ox * ce
        w = ow * (1 - 0.5 * vista if m > 0 else 1.0)
        def caja(x0, x1, y0, y1, r=0.015):
            return [(x0 + r, y0), (x1 - r, y0), (x1, y0 + r), (x1, y1 - r), (x1 - r, y1), (x0 + r, y1), (x0, y1 - r),
                    (x0, y0 + r)]
        pieza(c, t, caja(cx - w / 2, cx + w / 2, oy - oh / 2, oy + oh / 2), p, color=NAVY_CLARO, fuerza=0.95, n=2)
        afuera = cx + m * w / 2
        pieza(c, t, caja(min(afuera, afuera - m * w * 0.28), max(afuera, afuera - m * w * 0.28), oy - oh * 0.42,
                         oy + oh * 0.42, 0.01), p * 0.7, color=NAVY, fuerza=0.95, n=2)
        c.trazo(t([(cx - m * w * 0.12, oy - oh * 0.45), (cx - m * w * 0.12, oy + oh * 0.45)]), peso=p * 0.55,
                presion=0.9, suave=False)
    if pistola and not vacio:
        pistola_agua(c, t.hijo(0.46, 0.14, 0.85, -12), chorro=chorro)


CUPULA = [(-0.52, 0.14), (-0.50, -0.06), (-0.40, -0.26), (-0.20, -0.38), (0.0, -0.41), (0.20, -0.38), (0.40, -0.26),
          (0.50, -0.06), (0.52, 0.14), (0.30, 0.22), (0.0, 0.24), (-0.30, 0.22)]


def _traje_defensor(c, t, s, p, fx, color):
    """Traje del defensor como el modelo: cúpula blanca ancha con la franja oscura del visor abajo (y su filo de
    luz), el cañón arriba y una manguera en aro al costado."""
    luz = color or AZUL_LUZ
    if not c.palito:
        c.trazo(spline(t([(0.30, -0.30), (0.62, -0.18), (0.66, 0.10), (0.48, 0.30), (0.30, 0.22)]), n=10), peso=p * 2.2,
                presion=0.9)
    pieza(c, t, CUPULA, p * 1.05, color=BLANCO_TRAJE, fuerza=0.6, sombra=0.35, k=0.07)
    c.trazo(t([(fx * 0.5, -0.40), (fx * 0.5, -0.02)]), peso=p * 0.5, presion=0.5, suave=False)
    visor = [(-0.51, 0.0), (0.51, 0.0), (0.50, 0.12), (-0.50, 0.12)]
    pieza(c, t, visor, p * 0.8, color=VISOR, fuerza=0.85, oscuro=0.2, n=3)
    if not c.palito:
        c.aguada(spline(t([(-0.50, 0.105), (0.50, 0.105), (0.50, 0.125), (-0.50, 0.125)]), cerrado=True, n=2), luz, 0.9)
        for lado in (-1, 1):
            q = t.p(fx + lado * 0.12, 0.05)
            c.aclarar(elipse(q[0], q[1], 0.035 * s, 0.018 * s, n=10), 0.8, suave=1)
    # el cañón de arriba (blanco, con anillos de luz)
    pieza(c, t, [(-0.14, -0.50), (0.14, -0.50), (0.14, -0.40), (-0.14, -0.40)], p * 0.7, color=BLANCO_TRAJE,
          fuerza=0.6, sombra=0.3, n=2)
    pieza(c, t, [(-0.04, -0.60), (0.34, -0.60), (0.34, -0.50), (-0.04, -0.50)], p * 0.7, color=BLANCO_TRAJE,
          fuerza=0.6, sombra=0.3, n=2)
    pieza(c, t, [(0.30, -0.61), (0.37, -0.61), (0.37, -0.49), (0.30, -0.49)], p * 0.6, color=luz, fuerza=0.85, n=2)
    pieza(c, t, [(0.08, -0.60), (0.12, -0.60), (0.12, -0.50), (0.08, -0.50)], p * 0.5, color=luz, fuerza=0.85, n=2)


def _pez_palito(c, t, s, p, heroe, traje, defensor, expr, mirada, fx, pistola, chorro, fuego, aleta):
    if fuego and (traje or defensor):
        llama(c, t.hijo(-0.30, 0.30, 1.0, 90), largo=0.6)
    c.contorno(t(HUEVO), peso=p)
    for (u, v, a) in ((-0.36, 0.05, 160), (0.36, 0.05, 20 + aleta), (-0.05, 0.44, 112), (0.05, 0.44, 68)):
        tf = t.hijo(u, v, 0.85, a)
        c.trazo(tf([(0, 0), (0.3, -0.08)]), peso=p, suave=False)
        c.trazo(tf([(0, 0), (0.3, 0.08)]), peso=p, suave=False)
    cxs = t.p(fx, -0.2)[0]
    for lado in (-1, 1):
        q = t.p(fx + lado * 0.11, -0.24)
        ojo_saltón(c, q[0], q[1], 0.08 * s, mirada, expr, p)
        if (heroe or traje) and expr in ENOJADO:
            parpado_enojado(c, q[0], q[1], 0.08 * s, 1 if q[0] > cxs else -1, None, p)
    if traje:
        q = t.p(fx * 0.9, -0.18)
        c.contorno(elipse(q[0], q[1], 0.31 * s, 0.31 * s, n=24), peso=p * 0.7)
        c.contorno(t(MOCHILA), peso=p * 0.8)
    if defensor:
        c.trazo(t([(-0.38, -0.13), (0.40, -0.13)]), peso=p, suave=False)
        c.contorno(t([(0.02, -0.63), (0.30, -0.63), (0.30, -0.55), (0.02, -0.55)]), peso=p, suave=False)
    if pistola:
        pistola_agua(c, t.hijo(0.55, 0.10, 0.9), chorro=chorro)


def cardumen(c, cx, cy, n, s, seed=0, dispersion=(160, 70), flip=None, expr='normal', prof=0.0, colores=True,
             rot_var=10, mirada=(0.35, 0.0), evitar=None):
    """Grupo de peces del lago (de colores, como en el juego)."""
    rng = np.random.default_rng(seed)
    pts = []
    for i in range(n):
        for _ in range(30):
            x = cx + rng.uniform(-1, 1) * dispersion[0]
            y = cy + rng.uniform(-1, 1) * dispersion[1]
            if all(np.hypot(x - a, y - b) > s * 0.85 for a, b in pts) and \
                    (evitar is None or np.hypot(x - evitar[0], y - evitar[1]) > evitar[2]):
                break
        pts.append((x, y))
    pts.sort(key=lambda q: q[1])
    for i, (x, y) in enumerate(pts):
        f = flip if flip is not None else bool(rng.random() < 0.5)
        pez(c, x, y, s * rng.uniform(0.85, 1.15), rot=rng.normal(0, rot_var), flip=f,
            color=COLORES_PEZ[i % len(COLORES_PEZ)], expr=expr if isinstance(expr, str) else expr[i % len(expr)],
            prof=prof, mirada=mirada)
    return pts


def pez_sombra(c, x, y, s, rot=0.0, flip=False):
    """Silueta de pez (dentro del saco del pelícano)."""
    t = T(x, y, s, rot, flip)
    P = spline(t(HUEVO), cerrado=True, n=4)
    if c.palito:
        c.contorno(P, peso=0.6)
        return
    c.tono(P, 0.35, suave=0.5, mancha=0.1)
    c.contorno(P, peso=0.5, presion=0.5)
    for a in (112, 68):
        c.tono(spline(t.hijo(0, 0.44, 0.8, a)(ALETA_ABANICO), cerrado=True, n=3), 0.35, suave=0.5)


# ======================= AVES (de peluche, como los modelos) =======================

CUERPO_AVE = [(0.40, -0.04), (0.36, -0.22), (0.20, -0.32), (-0.05, -0.28), (-0.30, -0.18), (-0.50, -0.06),
              (-0.52, 0.04), (-0.30, 0.14), (0.00, 0.22), (0.26, 0.20), (0.40, 0.10)]
COLA_AVE = [(-0.46, -0.07), (-0.74, -0.13), (-0.80, -0.04), (-0.76, 0.05), (-0.46, 0.05)]
ALA_LARGA = [(0.0, 0.0), (0.30, -0.07), (0.70, -0.08), (1.00, -0.06), (1.10, 0.0), (1.06, 0.08)] + \
            [(1.0 - 0.1 * k, 0.12 + 0.05 * (k % 2)) for k in range(11)] + [(0.0, 0.10)]

POSES_ALA = {
    # (ángulo ala cercana, espejo, largo), (ángulo ala lejana, espejo, largo)
    'arriba': ((-108, True, 1.25), (-72, True, 1.05)),
    'extendidas': ((-150, True, 1.3), (-32, True, 1.0)),
    'abajo': ((150, False, 1.15), (166, False, 0.8)),
    'atras': ((174, True, 1.15), (-174, True, 0.95)),
}

ESPECIES = {
    'gaviota': dict(cuerpo=(0.98, 0.98, 0.99), ala=(0.80, 0.85, 0.93), punta=(0.30, 0.31, 0.36), pico='corto',
                    pico_col=(1.0, 0.82, 0.40), patas=(1.0, 0.80, 0.40), cejas=False),
    'garza': dict(cuerpo=(0.98, 0.98, 0.99), lomo=(0.64, 0.70, 0.80), ala=(0.64, 0.70, 0.80), punta=(0.32, 0.34, 0.42),
                  pico='daga', pico_col=(0.95, 0.78, 0.50), patas=(0.98, 0.82, 0.45), cejas=True, cresta=True),
    'pelicano': dict(cuerpo=(0.97, 0.96, 0.93), ala=(0.94, 0.94, 0.93), punta=(0.28, 0.28, 0.32), pico='bolsa',
                     pico_col=(1.0, 0.78, 0.50), patas=(0.98, 0.68, 0.60), cejas=True),
    'cormoran': dict(cuerpo=(0.44, 0.40, 0.58), ala=(0.40, 0.64, 0.72), punta=(0.22, 0.24, 0.32), pico='gancho',
                     pico_col=(0.38, 0.38, 0.44), patas=(0.25, 0.25, 0.3), cejas=True, collar=(1.0, 0.38, 0.44),
                     corona=True, escamas=True),
    'aguila': dict(cuerpo=(0.66, 0.50, 0.38), cabeza=(0.99, 0.99, 0.98), ala=(0.64, 0.49, 0.37),
                   punta=(0.45, 0.33, 0.25), pico='aguila', pico_col=(1.0, 0.84, 0.42), patas=(1.0, 0.84, 0.42),
                   cejas=True, corona=True, cola=(0.99, 0.99, 0.98)),
}
SIN_CEJAS = ('feliz', 'dormido', 'sorpresa', 'asustado', 'mareado', 'triste', 'cansado')


def _ala_larga(c, t, hombro, ang, espejo, largo, p, sp, lejana=False):
    m = -1 if espejo else 1
    tw = t.hijo(hombro[0], hombro[1], largo, ang)
    if c.palito:
        c.trazo(tw([(0, 0), (0.55, -0.06 * m), (1.1, 0)]), peso=p, suave=False)
        return
    with lejos(c, 0.3 if lejana else 0.0):
        P = pieza(c, tw, [(u, v * m) for u, v in ALA_LARGA], p * (0.8 if lejana else 1.0), color=sp['ala'],
                  fuerza=0.75, sombra=0.2, k=0.06, n=3)
        tip = spline(tw([(0.74, -0.09 * m), (1.10, 0.0), (1.06, 0.08 * m), (0.74, 0.17 * m)]), cerrado=True, n=3)
        zona = dentro(c.mascara(tip), c.mascara(P))
        c.aguada(zona, sp['punta'], 0.85)
        c.rayado(zona, angulo=30, espacio=4, presion=0.4)
        if sp.get('escamas'):
            for k in range(6):
                for vv in (0.0, 0.06):
                    q = tw.p(0.15 + 0.1 * k, vv * m)
                    c.trazo(elipse(q[0], q[1], 0.025 * tw.s, 0.02 * tw.s, a0=0, a1=180, n=6), peso=p * 0.35,
                            presion=0.5)


def ave(c, x, y, s, especie='gaviota', rot=0.0, flip=False, alas='extendidas', expr='malo', patas='colgando',
        presa=False, prof=0.0, silueta=False, pico_abierto=False, bolsa=None, ladeada=0.0):
    """Ave del juego en estilo de peluche (vista de lado, mirando a la derecha). s = largo del cuerpo.
    alas: extendidas, arriba, abajo, atras, plegadas. patas: colgando (en vuelo), paradas, largas, ninguna."""
    sp = ESPECIES[especie]
    t = T(x, y, s, rot, flip)
    p = pw(s * 0.8)
    with lejos(c, prof):
        if silueta:
            _ave_silueta(c, t, p, alas)
            return t
        pose_alas = POSES_ALA.get(alas)
        if pose_alas and not c.palito:
            _ala_larga(c, t, (0.02, -0.16), pose_alas[1][0], pose_alas[1][1], pose_alas[1][2], p, sp, lejana=True)
        pc = sp['patas']
        if patas == 'colgando':
            for dv in (0.0, 0.05):
                pieza(c, t, banda([(-0.18, 0.16 + dv), (-0.52, 0.20 + dv), (-0.66, 0.22 + dv)], 0.035, 0.03), p * 0.6,
                      color=pc, fuerza=0.8, n=3)
        elif patas in ('paradas', 'largas'):
            alto = 0.95 if patas == 'largas' else 0.34
            for du, lej in ((-0.10, True), (0.06, False)):
                with lejos(c, 0.25 if lej else 0.0):
                    pata = banda([(du, 0.16), (du - 0.04, 0.16 + alto * 0.5), (du + 0.02, 0.16 + alto)], 0.045, 0.035)
                    pieza(c, t, pata, p * 0.7, color=pc, fuerza=0.8, n=3)
                    pieza(c, t, [(du - 0.06, 0.16 + alto), (du + 0.14, 0.16 + alto), (du + 0.04, 0.12 + alto)], p * 0.6,
                          color=pc, fuerza=0.8, n=2)
        pieza(c, t, COLA_AVE, p, color=sp.get('cola', sp['ala']), fuerza=0.75, sombra=0.25)
        if c.palito:
            c.contorno(t(elipse(0, -0.04, 0.46, 0.24, n=20)), peso=p)
        else:
            P = pieza(c, t, CUERPO_AVE, p * 1.05, color=sp['cuerpo'], fuerza=0.75, sombra=0.35, k=0.07)
            if 'lomo' in sp:
                lomo = spline(t([(0.30, -0.18), (0.0, -0.34), (-0.6, -0.2), (-0.5, -0.02), (0.0, -0.08), (0.25, -0.06)]),
                              cerrado=True, n=4)
                c.aguada(dentro(c.mascara(lomo), c.mascara(P)), sp['lomo'], 0.8)
        if alas == 'plegadas':
            pieza(c, t, [(0.20, -0.12), (-0.05, -0.18), (-0.40, -0.10), (-0.70, -0.02), (-0.34, 0.06), (0.10, 0.04)], p,
                  color=sp['ala'], fuerza=0.8, sombra=0.3)
            if not c.palito:
                c.aguada(spline(t([(-0.40, -0.09), (-0.70, -0.02), (-0.38, 0.05)]), cerrado=True, n=4), sp['punta'], 0.85)
        if sp.get('collar'):
            pieza(c, t, [(0.10, -0.30), (0.18, -0.34), (0.24, -0.02), (0.15, 0.03)], p * 0.7, color=sp['collar'],
                  fuerza=0.9, n=4)
        # cabeza (grande, casi sin cuello)
        q = t.p(0.36, -0.18)
        cab = elipse(q[0], q[1], 0.21 * s, 0.20 * s, n=28)
        c.tapar(cab)
        if not c.palito:
            c.aguada(cab, sp.get('cabeza', sp['cuerpo']), 0.75)
            c.rayado(menos(c.mascara(cab), c.mascara(cab + [-0.04 * s, -0.05 * s])), espacio=5, presion=0.3)
        c.contorno(cab, peso=p, presion=0.85)
        _pico(c, t, sp, p, pico_abierto, bolsa)
        if sp.get('cresta'):
            for dv in (-0.33, -0.27):
                c.trazo(spline(t([(0.30, dv), (0.18, dv - 0.03), (0.06, dv + 0.02)]), n=6), peso=p * 1.6, presion=1.0)
        # ojos saltones (el de atrás asoma por arriba) y las cejas
        ojos = ((0.30, -0.36, 0.085), (0.44, -0.31, 0.10))
        for (u, v, r) in ojos:
            ojo_saltón(c, *t.p(u, v), r * s, (0.6, 0.2), expr, p, flip=t.flip)
        if (sp.get('cejas') or expr == 'malo') and expr not in SIN_CEJAS:
            for (u, v, r) in ojos:
                c.trazo(t([(u - r * 1.0, v - r * 1.55), (u + r * 0.9, v - r * 0.95)]), peso=p * 1.7, presion=1.0,
                        suave=False, punta=False)
        elif expr in ('triste', 'cansado'):
            for (u, v, r) in ojos:
                c.trazo(t([(u - r * 0.9, v - r * 1.0), (u + r * 0.9, v - r * 1.5)]), peso=p * 1.2, suave=False)
                if expr == 'cansado' and not c.palito:
                    q = t.p(u, v)
                    R = r * s
                    tapa = np.vstack([elipse(q[0], q[1], R * 1.05, R * 1.05, a0=180, a1=360, n=14),
                                      [[q[0] + R, q[1] + R * 0.1], [q[0] - R, q[1] + R * 0.1]]])
                    c.tapar(tapa)
                    c.aguada(tapa, sp.get('cabeza', sp['cuerpo']), 0.75)
                    c.trazo([(q[0] - R, q[1] + R * 0.1), (q[0] + R, q[1] + R * 0.1)], peso=p)
        if sp.get('corona'):
            corona(c, t, 0.36, -0.42, 1.3, ladeada, p)
        if pose_alas:
            _ala_larga(c, t, (0.04, -0.15), pose_alas[0][0], pose_alas[0][1], pose_alas[0][2], p, sp)
        if presa:
            pez(c, *t.p(0.95, -0.02), s * 0.42, rot=t.rot + (-80 if not flip else 80), flip=flip, expr='asustado',
                color=COLORES_PEZ[1], vista=0.2)
    return t


def _pico(c, t, sp, p, abierto, bolsa):
    col = sp['pico_col']
    tipo = sp['pico']
    if tipo == 'corto':
        if abierto:
            pieza(c, t, [(0.50, -0.22), (0.74, -0.26), (0.78, -0.22), (0.52, -0.16)], p * 0.8, color=col, fuerza=0.8)
            pieza(c, t, [(0.50, -0.14), (0.70, -0.08), (0.66, -0.05), (0.50, -0.10)], p * 0.8, color=col, fuerza=0.8)
        else:
            pieza(c, t, [(0.50, -0.22), (0.72, -0.19), (0.77, -0.14), (0.71, -0.11), (0.67, -0.14), (0.52, -0.12)],
                  p * 0.8, color=col, fuerza=0.8, n=4)
            if not c.palito:
                q = t.p(0.64, -0.125)
                c.aguada(elipse(q[0], q[1], 0.018 * t.s, 0.014 * t.s, n=8), (1.0, 0.4, 0.35), 0.8)
    elif tipo == 'daga':
        abre = 0.05 if abierto else 0.0
        pieza(c, t, [(0.50, -0.22), (1.18, -0.15 - abre), (0.52, -0.14)], p * 0.9, color=col, fuerza=0.8, n=2,
              sombra=0.2)
        pieza(c, t, [(0.52, -0.14), (1.14, -0.13 + abre), (0.50, -0.10)], p * 0.8, color=col, fuerza=0.8, n=2)
        if abierto and not c.palito:
            pieza(c, t, [(0.55, -0.12), (0.72, -0.06), (0.75, 0.04), (0.68, 0.02), (0.55, -0.08)], p * 0.6,
                  color=(1.0, 0.6, 0.65), fuerza=0.8)
    elif tipo == 'gancho':
        pieza(c, t, [(0.50, -0.21), (0.84, -0.19), (0.89, -0.12), (0.83, -0.13), (0.50, -0.12)], p * 0.85, color=col,
              fuerza=0.85, n=3)
        if not c.palito:
            q = t.p(0.85, -0.15)
            c.aguada(elipse(q[0], q[1], 0.035 * t.s, 0.03 * t.s, n=10), (0.95, 0.92, 0.85), 0.9)
    elif tipo == 'aguila':
        pieza(c, t, [(0.48, -0.25), (0.70, -0.24), (0.80, -0.13), (0.74, -0.05), (0.71, -0.12), (0.50, -0.10)],
              p * 0.9, color=col, fuerza=0.85, sombra=0.2, n=4)
    elif tipo == 'bolsa':
        b = bolsa or 'normal'
        pico = [(0.50, -0.24), (1.12, -0.19), (1.20, -0.13), (1.12, -0.11), (0.52, -0.15)]
        if b == 'lleno':
            saco = [(0.52, -0.15), (1.12, -0.11), (1.12, 0.12), (0.92, 0.32), (0.66, 0.28), (0.50, 0.04)]
        elif b == 'recogiendo':
            pico = [(0.50, -0.26), (1.12, -0.30), (1.20, -0.24), (1.12, -0.21), (0.52, -0.17)]
            saco = [(0.52, -0.17), (0.60, 0.40), (0.92, 0.66), (1.20, 0.44), (1.18, -0.22)]
        elif b == 'vacio':
            saco = [(0.52, -0.15), (1.12, -0.11), (0.98, 0.00), (0.80, 0.10), (0.60, 0.06), (0.50, -0.06)]
        else:
            saco = [(0.52, -0.15), (1.12, -0.11), (1.04, 0.04), (0.84, 0.14), (0.62, 0.10), (0.50, -0.04)]
        Ps = pieza(c, t, saco, p, color=col, fuerza=0.85, sombra=0.3, k=0.10)
        if not c.palito:
            rng = np.random.default_rng(7)
            for k in range(6):
                q = t.p(0.62 + rng.uniform(0, 0.4), -0.06 + rng.uniform(0, 0.16))
                mancha = elipse(q[0], q[1], 0.035 * t.s, 0.025 * t.s, rot=rng.uniform(0, 90), n=10)
                c.aguada(dentro(c.mascara(mancha), c.mascara(Ps)), (0.72, 0.46, 0.30), 0.8)
        if b == 'lleno':
            rng = np.random.default_rng(3)
            for k in range(5):
                q = t.p(0.66 + rng.uniform(0, 0.36), -0.02 + rng.uniform(0, 0.22))
                pez_sombra(c, q[0], q[1], t.s * 0.16, rot=rng.uniform(-40, 40), flip=bool(rng.random() < 0.5))
        elif b == 'recogiendo' and not c.palito:
            c.tono(spline(t([(0.56, -0.17), (0.64, 0.34), (0.92, 0.58), (1.14, 0.40), (1.14, -0.21)]), cerrado=True, n=6),
                   0.14, suave=2.0)
        elif b == 'vacio' and not c.palito:
            for k in range(4):
                u = 0.62 + 0.1 * k
                c.trazo(t([(u, -0.10), (u - 0.03, 0.04)]), peso=p * 0.5, presion=0.6, suave=False)
        pieza(c, t, pico, p, color=col, fuerza=0.85, sombra=0.25, n=4)
        c.trazo(t([(0.54, -0.18), (1.10, -0.15)]), peso=p * 0.5, presion=0.6)


def _ave_silueta(c, t, p, alas='extendidas'):
    """Ave lejana o en sombra: una silueta en V (o posada)."""
    if alas == 'plegadas':
        P = spline(t([(0.35, -0.05), (0.38, -0.30), (0.55, -0.28), (0.30, 0.02), (0.10, 0.18), (-0.45, 0.22),
                      (-0.15, 0.0)]), cerrado=True, n=5)
    else:
        P = spline(t([(-1.1, -0.40), (-0.45, -0.16), (0.0, 0.0), (0.45, -0.16), (1.1, -0.40), (0.5, -0.05),
                      (0.1, 0.12), (-0.1, 0.12), (-0.5, -0.05)]), cerrado=True, n=4)
    if c.palito:
        c.contorno(P, peso=p)
        return
    c.tapar(P)
    c.tono(P, 0.55, suave=0.4, mancha=0.1)
    c.contorno(P, peso=p * 0.8, presion=0.8)


def gaviota(c, x, y, s, rot=0.0, flip=False, alas='extendidas', expr='malo', patas='colgando', presa=False,
            prof=0.0, silueta=False, pico_abierto=False):
    return ave(c, x, y, s, 'gaviota', rot, flip, alas, expr, patas, presa, prof, silueta, pico_abierto)


def bandada(c, cx, cy, n, s, seed=0, dispersion=(300, 120), prof=0.3):
    rng = np.random.default_rng(seed)
    for i in range(n):
        x = cx + rng.uniform(-1, 1) * dispersion[0]
        y = cy + rng.uniform(-1, 1) * dispersion[1]
        gaviota(c, x, y, s * rng.uniform(0.6, 1.2), rot=rng.normal(0, 10), flip=bool(rng.random() < 0.5), silueta=True,
                prof=prof + rng.uniform(0, 0.25))


def corona(c, t, u, v, s=1.0, ladeada=0.0, p=1.0):
    tc = t.hijo(u, v, s, ladeada)
    P = tc([(-0.12, 0.0), (-0.14, -0.14), (-0.07, -0.06), (0.0, -0.17), (0.07, -0.06), (0.14, -0.14), (0.12, 0.0)])
    if c.palito:
        c.contorno(P, peso=p, suave=False)
        return
    c.tapar(P)
    c.aguada(P, (1.0, 0.84, 0.35), 0.85)
    c.rayado(menos(c.mascara(P), c.mascara(P + [-2, -3])), espacio=3, presion=0.4)
    c.contorno(P, peso=p * 0.8, suave=False)
    for uu in (-0.14, 0.0, 0.14):
        q = tc.p(uu, -0.15 if uu else -0.18)
        c.contorno(elipse(q[0], q[1], 0.02 * tc.s, 0.02 * tc.s, n=8), peso=p * 0.4)


# ---------- Jefes ----------

def garza(c, x, y, s, flip=False, pose='parada', prof=0.0, sombra_niebla=False, rot=0.0):
    """La Garza Gris Gigante: blanca y gris, cejas y cresta negras, pico largo como una daga, patas largas.
    Poses: parada, estocada (se lanza con el pico al agua), cansada, volando, huyendo."""
    if sombra_niebla:
        t = T(x, y, s, rot, flip)
        with lejos(c, prof):
            _ave_silueta(c, T(x, y, s * 1.5, rot, flip), pw(s))
        return t
    k = -1 if flip else 1                      # inclinación: positivo = la cabeza baja
    if pose == 'parada':
        return ave(c, x, y, s, 'garza', rot - 12 * k, flip, alas='plegadas', patas='largas', prof=prof)
    if pose == 'estocada':
        # se lanza desde el aire con el pico por delante (como el modelo, que siempre vuela)
        return ave(c, x, y, s, 'garza', rot + 45 * k, flip, alas='arriba', patas='colgando', prof=prof)
    if pose == 'cansada':
        return ave(c, x, y, s, 'garza', rot + 18 * k, flip, alas='abajo', patas='largas', expr='cansado', prof=prof,
                   pico_abierto=True)
    if pose == 'huyendo':
        return ave(c, x, y, s, 'garza', rot - 10 * k, flip, alas='arriba', patas='colgando', expr='mareado', prof=prof)
    return ave(c, x, y, s, 'garza', rot, flip, alas='extendidas', patas='colgando', prof=prof)


def pelicano(c, x, y, s, flip=False, pose='volando', prof=0.0, rot=0.0, expr='malo', alas=True):
    """El Pelícano Saco Sin Fondo: blanco, cejas gruesas, la bolsa naranja con manchas cafés.
    Poses: volando, lleno (bolsa llena de peces), vacio (bolsa colgando), recogiendo (bolsa abierta en el agua)."""
    bolsa = {'lleno': 'lleno', 'vacio': 'vacio', 'recogiendo': 'recogiendo'}.get(pose, 'normal')
    if pose == 'recogiendo':
        return ave(c, x, y, s, 'pelicano', rot + (10 if not flip else -10), flip, alas='plegadas', patas='ninguna', expr=expr, prof=prof,
                   bolsa=bolsa)
    return ave(c, x, y, s, 'pelicano', rot, flip, alas='extendidas' if alas else 'plegadas',
               patas='colgando' if alas else 'ninguna', expr=expr, prof=prof, bolsa=bolsa)


def cormoran(c, x, y, s, flip=False, pose='buceando', prof=0.0, rot=0.0):
    """El Cormorán Rey: morado oscuro, alas verde azulado con escamas, collar rojo, corona y pico ganchudo.
    Poses: buceando (alas pegadas, como un torpedo), volando, empapado (se va goteando, la corona chueca)."""
    if pose == 'buceando':
        return ave(c, x, y, s, 'cormoran', rot, flip, alas='plegadas', patas='colgando', prof=prof)
    t = ave(c, x, y, s, 'cormoran', rot, flip, alas='arriba' if pose == 'empapado' else 'extendidas', patas='colgando',
            prof=prof, expr='malo', ladeada=12 if pose == 'empapado' else 0.0)
    if pose == 'empapado':
        rng = np.random.default_rng(5)
        for k in range(14):
            gota(c, *t.p(rng.uniform(-0.6, 0.6), rng.uniform(0.2, 0.6)), s * rng.uniform(0.025, 0.045))
    return t


def aguila(c, x, y, s, pose='garras', prof=0.0, flip=False, rot=0.0):
    """La Reina Águila Marina DE FRENTE: café, cabeza y cola blancas, corona, pico amarillo ganchudo, ojos saltones
    con cejas y las garras amarillas por delante. Poses: garras, grito, lejos."""
    t = T(x, y, s, rot, flip)
    p = pw(s)
    sp = ESPECIES['aguila']
    with lejos(c, prof):
        if pose == 'lejos':
            _ave_silueta(c, T(x, y, s * 0.9, rot, flip), p)
            corona(c, t, 0.0, -0.10, 0.6, 0, p * 0.6)
            return t
        if c.palito:
            c.contorno(t(elipse(0, 0, 0.26, 0.36, n=20)), peso=p)
            q = t.p(0, -0.5)
            c.contorno(elipse(q[0], q[1], 0.2 * s, 0.2 * s, n=16), peso=p)
            for m in (-1, 1):
                c.trazo(t([(0.2 * m, -0.2), (0.8 * m, -0.7), (1.6 * m, -0.6)]), peso=p * 1.2, suave=False)
                c.trazo(t([(0.15 * m, 0.3), (0.25 * m, 0.6)]), peso=p, suave=False)
                ojo_saltón(c, *t.p(0.09 * m, -0.56), 0.07 * s, (0, 0.3), 'normal', p)
            corona(c, t, 0, -0.72, 1.6, 0, p)
            return t
        for m in (-1, 1):
            ala = [(0.18, -0.30), (0.60, -0.58), (1.10, -0.66), (1.62, -0.62)] + \
                  [(1.62 - 0.12 * k, -0.48 + 0.04 * (k % 2) + 0.025 * k) for k in range(12)] + [(0.25, 0.05)]
            P = pieza(c, t, [(u * m, v) for u, v in ala], p * 1.1, color=sp['ala'], fuerza=0.8, sombra=0.4, k=0.07, n=3)
            tip = spline(t([(1.20 * m, -0.70), (1.70 * m, -0.66), (1.66 * m, -0.40), (1.20 * m, -0.36)]), cerrado=True,
                         n=2)
            c.aguada(dentro(c.mascara(tip), c.mascara(P)), sp['punta'], 0.85)
            for u in (0.5, 0.75, 1.0, 1.25):
                c.trazo(t([(u * m, -0.55), ((u + 0.05) * m, -0.30)]), peso=p * 0.45, presion=0.55, suave=False)
        pieza(c, t, [(-0.16, 0.25), (-0.30, 0.66), (0.0, 0.74), (0.30, 0.66), (0.16, 0.25)], p, color=sp['cola'],
              fuerza=0.5, sombra=0.3)
        pieza(c, t, elipse(0, 0, 0.29, 0.38, n=30), p * 1.15, color=sp['cuerpo'], fuerza=0.8, sombra=0.45, k=0.08)
        for m in (-1, 1):
            pieza(c, t, banda([(0.12 * m, 0.25), (0.18 * m, 0.45), (0.24 * m, 0.58)], 0.12, 0.09), p, color=sp['patas'],
                  fuerza=0.8, sombra=0.3, n=3)
            for a in (-50, -15, 20, 55):
                r = np.radians(a + 90)
                base = (0.24 * m + np.cos(r) * 0.05 * m, 0.60 + np.sin(r) * 0.03)
                dirx, diry = np.cos(r) * m, np.sin(r)
                pieza(c, t, banda([base, (base[0] + dirx * 0.12, base[1] + diry * 0.10)], 0.07, 0.05), p * 0.8,
                      color=sp['patas'], fuerza=0.8, n=3)
                pieza(c, t, [(base[0] + dirx * 0.11, base[1] + diry * 0.09 - 0.02),
                             (base[0] + dirx * 0.20, base[1] + diry * 0.16 + 0.03),
                             (base[0] + dirx * 0.12, base[1] + diry * 0.11 + 0.02)], p * 0.8, oscuro=0.7, n=3)
        pieza(c, t, elipse(0, -0.50, 0.23, 0.22, n=28), p * 1.1, color=sp['cabeza'], fuerza=0.4, sombra=0.25)
        if pose == 'grito':
            pieza(c, t, [(-0.09, -0.47), (0.09, -0.47), (0.06, -0.38), (-0.06, -0.38)], p, color=sp['pico_col'],
                  fuerza=0.85, n=3)
            boca = t(elipse(0, -0.31, 0.07, 0.07, n=16))
            c.tapar(boca)
            c.tono(boca, 0.8, suave=0.3)
            c.contorno(boca, peso=p * 0.8)
            pieza(c, t, [(-0.06, -0.26), (0.06, -0.26), (0.0, -0.18)], p * 0.8, color=sp['pico_col'], fuerza=0.85, n=3)
        else:
            pieza(c, t, [(-0.09, -0.47), (0.09, -0.47), (0.07, -0.36), (0.0, -0.24), (-0.07, -0.36)], p,
                  color=sp['pico_col'], fuerza=0.85, sombra=0.2, n=3)
        for m in (-1, 1):
            q = t.p(0.10 * m, -0.57)
            ojo_saltón(c, q[0], q[1], 0.075 * s, (0, 0.3), 'normal', p)
            c.trazo([(q[0] - m * 0.09 * s, q[1] - 0.12 * s), (q[0] + m * 0.08 * s, q[1] - 0.07 * s)], peso=p * 1.7,
                    presion=1.0, suave=False, punta=False)
        corona(c, t, 0.0, -0.72, 1.7, 0, p)
    return t


# ======================= BAGRE MECÁNICO =======================

_BAGRE_DER = [(0.0, -0.44), (0.24, -0.43), (0.38, -0.34), (0.40, -0.22), (0.30, -0.08), (0.24, 0.10), (0.14, 0.28),
              (0.05, 0.40), (0.0, 0.44)]
BAGRE = _BAGRE_DER + [(-x, y) for x, y in reversed(_BAGRE_DER[1:-1])]


def bagre(c, x, y, s, flip=False, pose='normal', prof=0.0, rot=0.0, expr='normal'):
    """El mecánico del Taller, parado como en el juego: bagre gris con manchas, lentes dorados redondos, delantal
    café y bigotes largos. Poses: normal (con su llave), martillo, lentes, pieza, lupa, guardando.  s = alto."""
    t = T(x, y, s, rot, flip)
    p = pw(s * 0.8)
    piel = (0.66, 0.70, 0.74)
    with lejos(c, prof):
        pieza(c, t.hijo(-0.30, -0.05, 1.0, 160), ALETA_GRIS, p * 0.8, color=piel, fuerza=0.75)
        pieza(c, t, [(0.0, 0.42), (-0.20, 0.62), (-0.06, 0.58), (0.0, 0.52), (0.06, 0.58), (0.20, 0.62)], p,
              color=piel, fuerza=0.75, sombra=0.25)
        pieza(c, t, BAGRE, p * 1.1, color=piel, fuerza=0.75, sombra=0.4, k=0.07)
        if not c.palito:
            for u, v, r in ((-0.22, -0.30, 0.03), (0.26, -0.34, 0.025), (-0.10, -0.38, 0.02), (0.30, -0.18, 0.025),
                            (-0.28, -0.12, 0.03)):
                q = t.p(u, v)
                c.tono(elipse(q[0], q[1], r * s, r * s * 0.8, n=10), 0.3, suave=0.4)
        # delantal café con bolsillo y la correa del cuello
        pieza(c, t, [(-0.24, -0.08), (0.24, -0.08), (0.20, 0.16), (0.10, 0.32), (-0.10, 0.32), (-0.20, 0.16)], p * 0.8,
              color=(0.66, 0.46, 0.32), fuerza=0.85, sombra=0.3)
        c.trazo(spline(t([(-0.24, -0.08), (0.0, -0.17), (0.24, -0.08)]), n=6), peso=p * 0.8)
        pieza(c, t, [(-0.10, 0.02), (0.10, 0.02), (0.10, 0.14), (-0.10, 0.14)], p * 0.6, color=(0.56, 0.38, 0.26),
              fuerza=0.8, n=2)
        c.trazo(t([(0.04, 0.03), (0.02, -0.06)]), peso=p * 0.9, suave=False)
        # boca ancha y bigotes: dos largos hacia los lados y cuatro cortos colgando
        c.trazo(spline(t([(-0.18, -0.20), (0.0, -0.16), (0.18, -0.20)]), n=6), peso=p * 0.9)
        for m in (-1, 1):
            pieza(c, t, banda([(0.16 * m, -0.21), (0.40 * m, -0.20), (0.56 * m, -0.04), (0.60 * m, 0.20)], 0.035, 0.018),
                  p * 0.7, color=(0.36, 0.38, 0.42), fuerza=0.85, n=4)
            for k in (0.05, 0.12):
                pieza(c, t, banda([(k * m, -0.17), ((k + 0.03) * m, -0.05), ((k + 0.01) * m, 0.08)], 0.028, 0.015),
                      p * 0.6, color=(0.36, 0.38, 0.42), fuerza=0.85, n=4)
        # lentes dorados redondos con los ojos atrás y las cejas
        for m in (-1, 1):
            q = t.p(0.12 * m, -0.31)
            ojo_saltón(c, q[0], q[1], 0.055 * s, (0.2, 0.2), 'feliz' if expr == 'feliz' else 'normal', p)
            lente = elipse(q[0], q[1], 0.085 * s, 0.085 * s, n=24)
            if not c.palito:
                c.aguada(lente, VIDRIO, 0.3)
            c.contorno(lente, peso=p * 1.0, presion=0.95)
            if not c.palito:
                aro = np.vstack([elipse(q[0], q[1], 0.095 * s, 0.095 * s, n=24),
                                 elipse(q[0], q[1], 0.08 * s, 0.08 * s, n=24)[::-1]])
                c.aguada(aro, (1.0, 0.78, 0.35), 0.9)
            if expr == 'triste':
                c.trazo(t([(0.04 * m, -0.43), (0.20 * m, -0.40)]), peso=p * 1.4, suave=False)
        c.trazo(t([(-0.03, -0.31), (0.03, -0.31)]), peso=p * 0.9, suave=False)
        _herramienta(c, t, pose, p, piel)
    return t


def _herramienta(c, t, pose, p, piel):
    """La aleta de adelante con su herramienta."""
    if pose == 'martillo':
        ta = t.hijo(0.32, -0.05, 1.0, -60)
        pieza(c, ta, banda([(0.0, 0.0), (0.50, 0.0)], 0.05), p * 0.8, color=(0.85, 0.70, 0.52), fuerza=0.8, n=2)
        pieza(c, ta, [(0.46, -0.12), (0.60, -0.12), (0.60, 0.14), (0.46, 0.14)], p, color=(0.72, 0.74, 0.78),
              fuerza=0.8, sombra=0.4, n=2)
        pieza(c, ta, ALETA_GRIS, p * 0.9, color=piel, fuerza=0.75)
    elif pose == 'lentes':
        pieza(c, t.hijo(0.26, -0.08, 0.9, -110), ALETA_GRIS, p * 0.9, color=piel, fuerza=0.75)
    elif pose == 'pieza':
        pieza(c, t.hijo(0.30, -0.08, 1.0, -85), ALETA_GRIS, p * 0.9, color=piel, fuerza=0.75)
    elif pose == 'lupa':
        ta = t.hijo(0.32, -0.02, 1.0, -10)
        pieza(c, ta, banda([(0.1, 0.0), (0.42, 0.0)], 0.05), p * 0.8, color=(0.85, 0.70, 0.52), fuerza=0.8, n=2)
        q = ta.p(0.56, 0.0)
        lente = elipse(q[0], q[1], 0.14 * ta.s, 0.14 * ta.s, n=24)
        c.tapar(lente)
        if not c.palito:
            c.aguada(lente, VIDRIO, 0.4)
        c.contorno(lente, peso=p)
        c.contorno(elipse(q[0], q[1], 0.12 * ta.s, 0.12 * ta.s, n=24), peso=p * 0.5)
        pieza(c, ta, ALETA_GRIS, p * 0.9, color=piel, fuerza=0.75)
    elif pose == 'guardando':
        for dv in (-0.08, 0.04):
            pieza(c, t.hijo(0.32, dv, 1.0, -5), ALETA_GRIS, p * 0.9, color=piel, fuerza=0.75)
    else:
        ta = t.hijo(0.32, -0.04, 1.0, 30)
        pieza(c, ta, ALETA_GRIS, p * 0.9, color=piel, fuerza=0.75)
        llave(c, ta.hijo(0.20, 0.02, 0.9, 20), p)


# ======================= EL TRAJE, LA VITRINA Y EL PLANO =======================

def traje_vacio(c, x, y, s, flip=False):
    """El traje sin el pez (en la vitrina o en el soporte): la armadura con el casco vacío y la luz apagada."""
    t = T(x, y, s, 0, flip)
    _heroe_traje(c, t, s, pw(s * 0.9), 'normal', (0, 0), 0.0, False, False, 0.0, 0.0, vacio=True)


def vitrina(c, x, y, w, h, traje=True, luz=True):
    """Vitrina del Taller como en el juego: columnas gris oscuro con filos de luz azul, aro naranja arriba y un aro de
    luz en la base. x, y = arriba a la izquierda."""
    t = T(0, 0, 1.0)
    gris = (0.40, 0.43, 0.48)
    fondo = [(x, y), (x + w, y), (x + w, y + h), (x, y + h)]
    c.tapar(fondo)
    if not c.palito:
        c.aguada(fondo, (0.52, 0.56, 0.62), 0.6)
        for k in range(1, 7):
            yy = y + h * k / 7
            c.linea((x + 6, yy), (x + w - 6, yy), peso=0.4, presion=0.4)
        if luz:
            c.aclarar(elipse(x + w / 2, y + h * 0.5, w * 0.42, h * 0.42, n=30), fuerza=0.3, suave=20)
    base = elipse(x + w / 2, y + h - 4, w * 0.42, 16, n=30)
    c.tapar(base)
    if not c.palito:
        c.aguada(base, AZUL_LUZ, 0.8)
    c.contorno(base, peso=0.9)
    if traje:
        traje_vacio(c, x + w / 2, y + h * 0.56, min(w, h) * 0.62)
    for xx in (x - 16, x + w - 8):
        pieza(c, t, [(xx, y - 6), (xx + 24, y - 6), (xx + 24, y + h + 10), (xx, y + h + 10)], 1.0, color=gris,
              fuerza=0.8, sombra=0.4, k=8, n=2)
        if not c.palito:
            x0 = xx + (18 if xx < x else 0)
            c.aguada([(x0, y), (x0 + 6, y), (x0 + 6, y + h), (x0, y + h)], AZUL_LUZ, 0.9)
    pieza(c, t, [(x - 26, y - 34), (x + w + 26, y - 34), (x + w + 22, y - 6), (x - 22, y - 6)], 1.0, color=gris,
          fuerza=0.8, sombra=0.3, k=6, n=2)
    c.contorno(elipse(x + w / 2, y + 24, 18, 18, n=24), peso=1.4, presion=0.9)
    if not c.palito:
        c.aguada(np.vstack([elipse(x + w / 2, y + 24, 21, 21, n=24), elipse(x + w / 2, y + 24, 15, 15, n=24)[::-1]]),
                 (1.0, 0.6, 0.3), 0.9)
        for k in range(2):
            xx = x + w * (0.18 + 0.14 * k)
            c.trazo([(xx, y + 14), (xx + w * 0.16, y + h * 0.4)], peso=1.3, borrar=True, pasadas=1)


def plano(c, x, y, w, h, dibujo='traje'):
    """Plano pegado en la pared (cuadrícula azul y un dibujo: el traje o la sombra de un águila)."""
    P = [(x, y), (x + w, y + 6), (x + w - 4, y + h), (x - 4, y + h - 6)]
    c.tapar(P)
    if not c.palito:
        c.aguada(P, (0.80, 0.88, 1.0), 0.55)
        for k in range(1, 6):
            c.linea((x + w * k / 6, y + 2), (x + w * k / 6 - 3, y + h - 2), peso=0.35, presion=0.35)
            c.linea((x, y + h * k / 6), (x + w, y + h * k / 6 + 4), peso=0.35, presion=0.35)
    c.trazo(P, peso=0.8, cerrado=True, suave=False)
    if dibujo == 'traje':
        traje_vacio(c, x + w * 0.5, y + h * 0.55, h * 0.7)
    else:
        aguila(c, x + w * 0.5, y + h * 0.55, w * 0.28, pose='lejos', prof=0.1)
        signo(c, '?', x + w * 0.85, y + h * 0.25, alto=40)
