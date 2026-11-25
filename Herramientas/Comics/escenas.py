# -*- coding: utf-8 -*-
"""Las 34 viñetas del modo Historia: qué se ve en cada una (una función por viñeta, mismo id que guion.py).

Cada escena se dibuja igual en los dos modos del Lienzo: 'palito' (guion gráfico: monigotes, flechas y notas) y
'lapiz' (el dibujo terminado). Regla de cada viñeta: UNA acción clara, el protagonista grande y al frente, el fondo
simple. La esquina de arriba a la izquierda queda libre (ahí el juego pone el recuadro del texto).
"""
import numpy as np
from lapiz import T, spline, elipse, banda, menos
from personajes import *

# ======================= FONDOS =======================

CIELOS = {
    'dia': ((0.50, 0.75, 0.96), (0.86, 0.94, 1.0)),
    'amanecer': ((0.62, 0.62, 0.90), (1.0, 0.82, 0.62)),
    'atardecer': ((0.56, 0.50, 0.80), (1.0, 0.72, 0.46)),
    'cielo_rojo': ((0.78, 0.36, 0.30), (1.0, 0.66, 0.42)),
    'niebla': ((0.80, 0.83, 0.86), (0.92, 0.93, 0.94)),
}
AGUAS = {
    'dia': ((0.46, 0.74, 0.88), (0.24, 0.50, 0.72)),
    'amanecer': ((0.66, 0.68, 0.86), (0.36, 0.48, 0.72)),
    'atardecer': ((0.92, 0.64, 0.54), (0.40, 0.40, 0.62)),
    'cielo_rojo': ((0.90, 0.52, 0.42), (0.48, 0.30, 0.36)),
    'niebla': ((0.76, 0.80, 0.84), (0.58, 0.64, 0.70)),
}
AGUA_ARRIBA = (0.52, 0.80, 0.90)       # bajo el agua: cerca de la superficie
AGUA_ABAJO = (0.22, 0.48, 0.64)        # y en lo hondo


def cielo(c, amb, y1=660, nubes=((1000, 120, 220), (380, 170, 170)), seed=0):
    top, bot = CIELOS[amb]
    c.degradado(0, y1, top, bot)
    for i, (x, y, w) in enumerate(nubes):
        nube(c, x, y, w, seed=seed + i, oscura=0.05 if amb == 'cielo_rojo' else 0.0)


def agua_frente(c, amb, y0, seed=0, x0=0, x1=1380):
    """El lago visto de frente, desde el horizonte y0 hasta abajo."""
    P = [(x0, y0), (x1, y0), (x1, 700), (x0, 700)]
    c.tapar(P)
    a, b = AGUAS[amb]
    c.degradado(y0, 660, a, b, x0=x0, x1=x1)
    agua_rayitas(c, y0 + 4, 660, x0, x1, seed=seed)
    c.trazo([(x0, y0), (x1, y0)], peso=0.6, presion=0.55, suave=False)


def bajo_agua(c, y0=0, fondo=600, seed=0, rayos=5, oscuro_abajo=True):
    """Todo bajo el agua (desde y0): aguada azul verdosa, rayos de luz y el fondo."""
    c.degradado(y0, 660, AGUA_ARRIBA, AGUA_ABAJO if oscuro_abajo else (0.34, 0.64, 0.78))
    rayos_luz(c, rayos, seed=seed)
    if fondo is not None:
        fondo_lago(c, fondo, seed=seed)


def corte(c, amb, yw, seed=0, fondo=610, orilla=True, nubes=((1080, 110, 200), (420, 150, 150)), rayos=4):
    """Viñeta partida: arriba el cielo (y la orilla lejana), abajo el agua vista de lado."""
    cielo(c, amb, yw, nubes=nubes, seed=seed)
    if orilla:
        orilla_lejana(c, yw, seed=seed, prof=0.55)
    c.degradado(yw, 660, AGUA_ARRIBA, AGUA_ABAJO)
    if rayos:
        for k in range(rayos):
            x = 150 + k * 300 + seed * 37 % 90
            c.aclarar([(x - 30, yw), (x + 30, yw), (x - 80, 660), (x - 200, 660)], fuerza=0.2, suave=14)
    if fondo is not None:
        fondo_lago(c, fondo, seed=seed)
    superficie(c, yw, seed=seed)


def niebla(c, y0, y1, fuerza=0.5):
    """Velo de niebla: aclara lo que ya está dibujado."""
    if c.palito:
        return
    for k in range(3):
        yy = y0 + (y1 - y0) * k / 3
        c.aclarar([(-50, yy), (1430, yy - 20), (1430, yy + (y1 - y0) / 2), (-50, yy + (y1 - y0) / 2 + 20)],
                  fuerza=fuerza * 0.5, suave=40)


# ======================= ELEMENTOS DE ESCENA =======================

def globo(c, x, y, w, h, texto=None, cola=None, pensamiento=False, alto=34):
    """Globo de diálogo (o de pensamiento) con su texto."""
    P = elipse(x, y, w / 2, h / 2, n=40)
    if cola is not None and not pensamiento:
        tx, ty = cola
        d = np.array([tx - x, ty - y], float)
        d /= np.linalg.norm(d) + 1e-6
        n = np.array([-d[1], d[0]])
        base = np.array([x, y]) + d * min(w, h) * 0.38
        cola_pts = [base + n * 16, (tx, ty), base - n * 16]
        c.tapar(cola_pts)
        c.trazo(cola_pts, peso=0.9, suave=False)
    c.tapar(P)
    c.contorno(P, peso=0.9)
    if cola is not None and pensamiento:
        tx, ty = cola
        for k, f in enumerate((0.25, 0.5, 0.75)):
            px, py = x + (tx - x) * (0.55 + f * 0.45), y + (ty - y) * (0.55 + f * 0.45)
            r = 14 - k * 4
            Q = elipse(px, py, r, r * 0.8, n=14)
            c.tapar(Q)
            c.contorno(Q, peso=0.7)
    if texto:
        c.texto(texto, x, y, alto=alto, contorno=False)


def bombilla(c, x, y, r=26):
    """Foco de idea."""
    P = elipse(x, y, r, r * 1.1, n=30)
    if not c.palito:
        c.aguada(elipse(x, y, r * 2.2, r * 2.2, n=30), (1.0, 0.95, 0.6), 0.5)
    c.tapar(P)
    if not c.palito:
        c.aguada(P, (1.0, 0.92, 0.55), 0.7)
    c.contorno(P, peso=0.9)
    base = [(x - r * 0.45, y + r * 0.95), (x + r * 0.45, y + r * 0.95), (x + r * 0.4, y + r * 1.4),
            (x - r * 0.4, y + r * 1.4)]
    c.tapar(base)
    c.trazo(base, peso=0.8, cerrado=True, suave=False)
    c.trazo([(x - r * 0.3, y + r * 0.6), (x - r * 0.15, y), (x, y + r * 0.4), (x + r * 0.15, y), (x + r * 0.3, y + r * 0.6)],
            peso=0.6, suave=False)
    for k in range(7):
        a = np.radians(-150 + k * 20)
        c.linea((x + np.cos(a) * r * 1.4, y + np.sin(a) * r * 1.4), (x + np.cos(a) * r * 1.9, y + np.sin(a) * r * 1.9),
                peso=0.7)


def arbol_seco(c, x, y, h, seed=0):
    """Árbol sin hojas (ramas para que se posen las aves). Devuelve puntos de ramas."""
    rng = np.random.default_rng(seed)
    tronco = banda([(x, y), (x + 6, y - h * 0.5), (x - 4, y - h)], h * 0.08, h * 0.03)
    c.tapar(tronco)
    if not c.palito:
        c.tono(tronco, 0.45, suave=0.5)
    c.contorno(tronco, peso=0.9)
    puntas = []
    for k, (f, lado) in enumerate(((0.45, -1), (0.6, 1), (0.75, -1), (0.88, 1))):
        bx, by = x + (6 if f < 0.5 else 0), y - h * f
        L = h * rng.uniform(0.3, 0.45)
        ex, ey = bx + lado * L, by - L * 0.35
        rama = banda([(bx, by), (bx + lado * L * 0.5, by - L * 0.1), (ex, ey)], h * 0.03, h * 0.01)
        c.tapar(rama)
        if not c.palito:
            c.tono(rama, 0.45, suave=0.5)
        c.contorno(rama, peso=0.7)
        puntas.append((bx + lado * L * 0.55, by - L * 0.12))
    return puntas


def pluma(c, x, y, s, rot=0.0):
    t = T(x, y, s, rot)
    P = spline(t([(-0.5, 0.0), (-0.1, -0.16), (0.5, 0.0), (-0.1, 0.14)]), cerrado=True, n=6)
    c.tapar(P)
    if not c.palito:
        c.aguada(P, (0.85, 0.88, 0.92), 0.5)
    c.contorno(P, peso=0.6)
    c.trazo(t([(-0.6, 0.02), (0.5, 0.0)]), peso=0.5, suave=False)
    if not c.palito:
        for u in (-0.2, 0.0, 0.2):
            c.trazo(t([(u, 0.0), (u + 0.1, -0.1)]), peso=0.35, suave=False)


def turbina(c, x, y, s, rot=0.0):
    """La pieza nueva del bagre: un propulsor más grande y brillante."""
    t = T(x, y, s, rot)
    p = pw(s)
    pieza(c, t, [(-0.5, -0.22), (0.3, -0.26), (0.45, -0.12), (0.45, 0.12), (0.3, 0.26), (-0.5, 0.22)], p,
          color=(0.82, 0.84, 0.88), fuerza=0.5, sombra=0.4)
    pieza(c, t, [(0.45, -0.18), (0.62, -0.26), (0.62, 0.26), (0.45, 0.18)], p * 0.9, color=(0.7, 0.72, 0.75),
          fuerza=0.5, oscuro=0.2)
    for u in (-0.3, -0.1, 0.1):
        c.trazo(t([(u, -0.24), (u, 0.24)]), peso=p * 0.5, suave=False)
    for u in (-0.35, 0.2):
        q = t.p(u, -0.12)
        c.contorno(elipse(q[0], q[1], 0.03 * s, 0.03 * s, n=10), peso=p * 0.4)
    if not c.palito:
        c.trazo(t([(-0.4, -0.16), (0.25, -0.18)]), peso=p * 1.2, borrar=True, pasadas=1)
    for (u, v, r) in ((0.75, -0.45, 0.12), (-0.6, -0.5, 0.09), (0.2, -0.55, 0.07)):
        q = t.p(u, v)
        brillo(c, q[0], q[1], r * s)


def soporte(c, x, y, h):
    """Pedestal del Taller (x, y = arriba al centro)."""
    t = T(0, 0, 1.0)
    pieza(c, t, [(x - 12, y), (x + 12, y), (x + 12, y + h), (x - 12, y + h)], 0.8, color=(0.85, 0.85, 0.86),
          fuerza=0.4, sombra=0.4, k=8, n=2)
    pieza(c, t, [(x - 70, y + h), (x + 70, y + h), (x + 80, y + h + 22), (x - 80, y + h + 22)], 0.9,
          color=(0.82, 0.82, 0.84), fuerza=0.4, sombra=0.4, k=8, n=2)


def cartel(c, x, y, w, texto, rot=-4.0):
    """Tablón con letras (como 'TALLER')."""
    t = T(x, y, 1.0, rot)
    h = w * 0.32
    for dx in (-w * 0.3, w * 0.3):
        c.linea(t.p(dx, -h * 0.5), t.p(dx * 0.6, -h * 1.6), peso=0.6)
    pieza(c, t, [(-w / 2, -h / 2), (w / 2, -h / 2), (w / 2, h / 2), (-w / 2, h / 2)], 1.0, color=(0.92, 0.82, 0.65),
          fuerza=0.55, sombra=0.3, k=6, n=2)
    if not c.palito:
        for v in (-h * 0.2, h * 0.15):
            c.trazo(t([(-w / 2 + 6, v), (w / 2 - 6, v + 2)]), peso=0.35, presion=0.35, suave=False)
    c.texto(texto, x, y, alto=h * 0.62, angulo=-rot, contorno=False)


def ola(c, x, y, s, seed=0, flip=False):
    """Ola grande que se levanta (x, y = base)."""
    m = -1 if flip else 1
    pts = [(x - s * 0.6 * m, y), (x - s * 0.3 * m, y - s * 0.25), (x, y - s * 0.65), (x + s * 0.25 * m, y - s * 0.8),
           (x + s * 0.45 * m, y - s * 0.62), (x + s * 0.32 * m, y - s * 0.55), (x + s * 0.22 * m, y - s * 0.4),
           (x + s * 0.4 * m, y - s * 0.2), (x + s * 0.7 * m, y)]
    P = spline(pts, cerrado=True, n=6)
    c.tapar(P)
    if not c.palito:
        c.aguada(P, AGUA, 0.6)
        for k in range(4):
            f = 0.2 + 0.18 * k
            c.trazo(spline([(x - s * (0.5 - f) * m, y - s * 0.05), (x + s * (f - 0.2) * m, y - s * (0.2 + f * 0.6)),
                            (x + s * (f * 0.6) * m, y - s * (0.3 + f * 0.5))], n=6), peso=0.6, presion=0.5)
    c.contorno(P, peso=1.0)
    rng = np.random.default_rng(seed)
    for k in range(8):
        gota(c, x + rng.uniform(-0.2, 0.7) * s * m, y - s * rng.uniform(0.7, 1.05), s * rng.uniform(0.02, 0.035))


def chispas(c, x, y, r, seed=0):
    rng = np.random.default_rng(seed)
    for k in range(9):
        a = np.radians(rng.uniform(180, 360))
        d0, d1 = r * rng.uniform(0.2, 0.4), r * rng.uniform(0.7, 1.1)
        c.linea((x + np.cos(a) * d0, y + np.sin(a) * d0), (x + np.cos(a) * d1, y + np.sin(a) * d1), peso=0.8)
    if not c.palito:
        c.aguada(elipse(x, y, r * 0.8, r * 0.8, n=20), (1.0, 0.9, 0.55), 0.6)


def etiqueta(c, texto, x, y, hacia, alto=28):
    """Rótulo escrito a mano con una flecha hacia una parte del dibujo."""
    c.texto(texto, x, y, alto=alto, fuente='Rajdhani-Bold.ttf', contorno=False)
    d = np.array(hacia, float) - [x, y]
    L = np.linalg.norm(d)
    d /= L + 1e-6
    p0 = np.array([x, y]) + d * (alto * 0.9)
    p1 = np.array(hacia) - d * 6
    mid = (p0 + p1) / 2 + np.array([-d[1], d[0]]) * L * 0.08
    c.trazo(spline([p0, mid, p1], n=8), peso=0.7, suave=False)
    n = np.array([-d[1], d[0]])
    c.trazo([p1 - d * 12 + n * 7, p1, p1 - d * 12 - n * 7], peso=0.7, suave=False)


def cabeza_asomada(c, x, y, s, heroe=True, expr='sorpresa', flip=False, mirada=(0.2, -0.6), color=None, traje=False,
                   amb='dia', horizonte=420):
    """Un pez asomado: medio cuerpo fuera del agua (y = la línea del agua donde asoma). El agua tapa lo de abajo."""
    pez(c, x, y + s * 0.08, s, flip=flip, heroe=heroe, expr=expr, mirada=mirada, color=color, traje=traje, vista=0.2)
    a, b = AGUAS.get(amb, AGUAS['dia'])
    k = float(np.clip((y - horizonte) / max(1.0, 660 - horizonte), 0, 1))
    col = tuple(a[i] * (1 - k) + b[i] * k for i in range(3))
    x0, x1 = x - s * 0.66, x + s * 0.66
    c.tapar([(x0, y), (x1, y), (x1, 700), (x0, 700)])
    c.degradado(y, 660, col, b, x0=x0, x1=x1)
    agua_rayitas(c, y + 4, 660, x0, x1, seed=int(x), densidad=0.25)
    for k2 in range(2):
        c.trazo(elipse(x, y + 2 + k2 * 6, s * (0.45 + 0.2 * k2), s * 0.06, a0=0, a1=180, n=16), peso=0.6, presion=0.6)


# ======================= LAS VIÑETAS =======================

# ---------- Cómic 0: el origen ----------

def origen_1(c):
    """Amanecer en el lago: arriba los nenúfares, abajo el cardumen nadando feliz."""
    corte(c, 'amanecer', 250, seed=1, nubes=((420, 120, 160),))
    sol(c, 1120, 205, 60)
    orilla_lejana(c, 250, seed=11, prof=0.5, x0=900, x1=1380)
    for i, (x, r, fl) in enumerate(((260, 95, True), (700, 70, False), (1120, 110, True))):
        c.trazo([(x, 252), (x - 20, 420), (x + 10, 610)], peso=0.6, presion=0.6)
        nenufar(c, x, 250, r, flor=fl, seed=i)
    algas(c, 120, 615, 230, n=5, seed=1)
    algas(c, 1260, 615, 260, n=5, seed=2)
    cardumen(c, 640, 430, 6, 95, seed=4, dispersion=(330, 90), expr='feliz', evitar=(650, 440, 110))
    pez(c, 650, 440, 130, heroe=True, expr='feliz')
    burbujas(c, 760, 400, 5, 7, seed=1, alto=100)
    burbujas(c, 420, 380, 4, 6, seed=2, alto=80)
    nota(c, 860, 600, 'todo en calma: peces felices')


def origen_2(c):
    """Llegan las gaviotas: una sube con un pez en el pico, otras bajan en picada; el cardumen huye."""
    corte(c, 'dia', 400, seed=2, nubes=((360, 130, 170),))
    bandada(c, 520, 80, 6, 40, seed=3, dispersion=(160, 40), prof=0.5)
    lineas_vel(c, 1000, 40, 1060, 120, n=4, sep=16, seed=1)
    gaviota(c, 1100, 160, 170, rot=-42, flip=True, alas='atras')
    lineas_vel(c, 1240, 120, 1290, 200, n=3, sep=16, seed=2)
    gaviota(c, 1290, 250, 130, rot=-48, flip=True, alas='atras', prof=0.2)
    salpicadura(c, 640, 400, 170, seed=1)
    gaviota(c, 720, 250, 270, rot=-18, alas='arriba', presa=True)
    lineas_vel(c, 600, 380, 660, 300, n=3, sep=18, seed=3)
    cardumen(c, 450, 520, 6, 85, seed=7, dispersion=(260, 70), flip=True, expr='asustado')
    for k in range(4):
        lineas_vel(c, 560 + k * 120, 470 + k * 20, 620 + k * 120, 470 + k * 20, n=2, sep=10, seed=10 + k)
    sudor(c, 300, 470)
    flecha(c, [(1150, 60), (1010, 200), (930, 360)])
    flecha(c, [(640, 380), (700, 260), (780, 140)])
    nota(c, 900, 600, 'gaviotas se llevan peces')


def origen_3(c):
    """Los peces se esconden en las algas; el pequeño sale, mira las sombras de las aves y dice ¡BASTA!"""
    bajo_agua(c, fondo=610, seed=3)
    superficie(c, 70, seed=3, desde_abajo=True)
    for x, rr in ((520, 0), (900, -10), (1200, 8)):
        gaviota(c, x, 40, 90, rot=rr, flip=True, silueta=True, prof=0.3)
    # peces escondidos (solo se les ve la cara entre las algas)
    for (x, y, f) in ((150, 380, False), (260, 470, False), (110, 540, False), (330, 330, False)):
        pez(c, x, y, 90, flip=f, color=COLORES_PEZ[4], expr='asustado', boca='plana', escamas=False)
    algas(c, 70, 615, 420, n=8, seed=5, ancho=110)
    algas(c, 260, 615, 360, n=6, seed=6, ancho=80)
    pez(c, 820, 370, 230, heroe=True, expr='enojado', mirada=(0.3, -0.6), rot=-12, boca='decidida', aleta=60)
    globo(c, 1110, 210, 250, 110, '¡BASTA!', cola=(930, 290), alto=46)
    burbujas(c, 960, 330, 4, 7, seed=4, alto=80)
    flecha(c, [(500, 470), (650, 420)])
    nota(c, 60, 640 - 28, 'los demás se esconden')


def origen_4(c):
    """En el fondo: la chatarra (llanta, latas, frasco, motor y una pistola de agua). Al pez se le ocurre una idea."""
    bajo_agua(c, fondo=530, seed=4)
    roca(c, 1250, 540, 70, seed=2)
    neumatico(c, 600, 560, 95, rot=-8)
    lata(c, 460, 560, 22, 60, rot=80)
    lata(c, 760, 590, 18, 48, rot=-20)
    frasco(c, 820, 470, 62, rot=8)
    motor_viejo(c, 1060, 440, 190, rot=6)
    pistola_agua(c, T(330, 580, 160, -10))
    engranaje(c, 940, 600, 34, rot=10)
    pez(c, 330, 300, 190, heroe=True, expr='sorpresa', mirada=(0.5, 0.7), rot=12, boca='o')
    bombilla(c, 420, 150, 34)
    for (x, y) in ((820, 380), (1080, 320), (400, 520)):
        brillo(c, x, y, 18)
    nota(c, 900, 640 - 28, 'chatarra = idea!')


def origen_5(c):
    """El pez con su traje recién armado, orgulloso. Rótulos: escafandra, propulsor, pistola de agua."""
    bajo_agua(c, fondo=600, seed=5)
    lata(c, 200, 610, 20, 52, rot=85)
    llave(c, T(1150, 610, 150, -15), 1.0)
    engranaje(c, 1260, 600, 30)
    pez(c, 680, 340, 380, heroe=True, traje=True, pistola=True, expr='feliz', aleta=10, boca='sonrisa')
    for (x, y, r) in ((360, 170, 26), (1020, 160, 30), (1040, 470, 20), (330, 470, 18)):
        brillo(c, x, y, r)
    etiqueta(c, 'escafandra', 1110, 200, (820, 230))
    etiqueta(c, 'propulsor', 330, 230, (530, 290))
    etiqueta(c, 'pistola de agua', 1100, 560, (965, 405))
    burbujas(c, 600, 120, 5, 8, seed=6, alto=60)


def origen_6(c):
    """¡Sale volando del agua! Las gaviotas no lo pueden creer."""
    corte(c, 'dia', 480, seed=6, nubes=((300, 110, 200), (1180, 330, 160)))
    salpicadura(c, 470, 480, 230, seed=2, alto=1.2)
    lineas_vel(c, 360, 520, 520, 380, n=5, sep=22, seed=4, peso=0.8)
    pez(c, 690, 290, 270, heroe=True, traje=True, fuego=True, pistola=True, expr='feliz', rot=26, aleta=-30)
    gaviota(c, 1150, 140, 150, flip=True, alas='arriba', expr='sorpresa')
    gaviota(c, 1260, 260, 120, flip=True, alas='extendidas', expr='sorpresa', prof=0.15)
    signo(c, '!?', 1100, 50, alto=56)
    flecha(c, [(500, 470), (600, 360), (760, 220)])
    nota(c, 860, 620, 'sale del agua volando')


# ---------- El Taller (primera visita) ----------

def taller_1(c):
    """La entrada de una cueva en el fondo del lago, con luz y un cartel: TALLER. El pez llega curioso."""
    bajo_agua(c, fondo=None, seed=7)
    ladera = [(-20, 520), (300, 470), (620, 330), (900, 250), (1200, 230), (1400, 260), (1400, 700), (-20, 700)]
    P = spline(ladera, cerrado=True, n=6)
    c.tapar(P)
    if not c.palito:
        c.aguada(P, (0.88, 0.84, 0.76), 0.5)
        c.rayado(P, angulo=30, espacio=7, presion=0.3)
    c.trazo(spline(ladera[:6], n=6), peso=1.0)
    boca = elipse(1000, 470, 190, 170, n=40, a0=180, a1=360)
    boca = np.vstack([boca, [[1190, 660], [810, 660]]])
    c.tapar(boca)
    if not c.palito:
        c.aguada(boca, (1.0, 0.88, 0.62), 0.55)
        c.aclarar(elipse(1000, 520, 140, 110, n=30), fuerza=0.4, suave=30)
    c.trazo(boca, peso=1.1, cerrado=True)
    for k in range(5):
        x = 860 + k * 70
        P2 = [(x - 12, 320 + abs(k - 2) * 20), (x + 12, 320 + abs(k - 2) * 20), (x, 360 + abs(k - 2) * 20)]
        c.tapar(P2)
        c.trazo(P2, peso=0.7, cerrado=True, suave=False)
    engranaje(c, 930, 600, 40, rot=12)
    engranaje(c, 1080, 580, 30, rot=4)
    lampara(c, 1000, 420)
    cartel(c, 1000, 220, 220, 'TALLER')
    neumatico(c, 700, 470, 60, rot=-20)
    lata(c, 1250, 300, 18, 50, rot=70)
    pez(c, 360, 300, 140, heroe=True, expr='sorpresa', mirada=(0.6, 0.2), boca='o')
    signo(c, '?', 430, 200, alto=56)
    burbujas(c, 470, 290, 4, 6, seed=7, alto=70)
    flecha(c, [(450, 330), (650, 380), (820, 430)])


def taller_2(c):
    """El viejo bagre mecánico martillando un motor en su mesa, con chispas: ¡TAC!"""
    cueva(c, seed=8)
    estante(c, 330, 190, 300, seed=1)
    estante(c, 860, 160, 280, seed=2)
    lampara(c, 700, 120)
    banco(c, 700, 470, 520)
    motor_viejo(c, 900, 400, 150, rot=-8)
    lata(c, 1100, 445, 18, 48)
    llave(c, T(760, 462, 150, 4), 1.0)
    bagre(c, 470, 400, 400, pose='martillo', expr='feliz')
    chispas(c, 850, 400, 70, seed=1)
    c.texto('¡TAC!', 1020, 300, alto=60, angulo=8)
    flecha(c, [(640, 170), (760, 260), (820, 360)])
    nota(c, 60, 610, 'bagre: lentes + bigotes + delantal')


def taller_3(c):
    """El bagre se ajusta los lentes y mira el traje: piensa en un traje mejor (con un propulsor más grande)."""
    cueva(c, seed=9)
    lampara(c, 640, 110)
    pez(c, 360, 420, 260, heroe=True, traje=True, pistola=True, expr='normal', mirada=(0.6, -0.3), aleta=40)
    bagre(c, 990, 420, 420, flip=True, pose='lentes', expr='normal')
    globo(c, 1160, 170, 250, 160, None, cola=(1060, 300), pensamiento=True)
    pez(c, 1150, 160, 120, heroe=True, traje=True, fuego=True, prof=0.1, expr='feliz')
    c.texto('+', 1245, 120, alto=50, contorno=False)
    nota(c, 60, 610, 'se puede mejorar')


# ---------- Jefe 1: la Garza Gris Gigante ----------

def garza_antes_1(c):
    """Niebla: una sombra enorme con las alas abiertas sobre los juncos; el pez asoma la cabeza, chiquito."""
    cielo(c, 'niebla', 420, nubes=())
    orilla_lejana(c, 420, seed=12, prof=0.75)
    agua_frente(c, 'niebla', 420, seed=12)
    # la Garza Gris de verdad (pico largo, patas colgando) borroneada por la niebla: una sombra enorme
    garza(c, 700, 230, 300, pose='volando', prof=0.35)
    niebla(c, 60, 470, 0.75)
    juncos(c, 80, 660, 330, n=7, seed=3, ancho=90)
    juncos(c, 1300, 660, 300, n=6, seed=4, ancho=90)
    cabeza_asomada(c, 690, 600, 120, expr='asustado', mirada=(0.1, -0.8), amb='niebla', horizonte=420)
    signo(c, '!', 760, 470, alto=56)
    flecha(c, [(400, 260), (560, 230), (700, 230)])
    nota(c, 860, 640, 'la sombra es ENORME')


def garza_antes_2(c):
    """La garza clava el pico en el agua y levanta una ola que lanza peces por el aire."""
    cielo(c, 'niebla', 400, nubes=((1100, 110, 180),))
    orilla_lejana(c, 400, seed=13, prof=0.7)
    agua_frente(c, 'niebla', 400, seed=13)
    niebla(c, 100, 420, 0.4)
    garza(c, 590, 395, 280, pose='estocada')
    ola(c, 860, 600, 330, seed=3)
    salpicadura(c, 820, 590, 200, seed=6)
    for (x, y, r) in ((990, 290, -60), (1080, 360, 40), (900, 250, 120)):
        pez(c, x, y, 70, rot=r, color=COLORES_PEZ[3], expr='asustado', boca='o', escamas=False)
    pez(c, 1200, 210, 150, heroe=True, traje=True, fuego=True, expr='sorpresa', rot=-15, flip=True)
    lineas_vel(c, 560, 380, 700, 470, n=4, sep=18, seed=6, peso=0.9)
    flecha(c, [(600, 380), (720, 470), (800, 540)])
    nota(c, 60, 612, 'pico abajo -> OLA')


def garza_antes_3(c):
    """La garza, cansada (lengua afuera, sudando). El pez le dispara un chorro enorme."""
    cielo(c, 'dia', 420, nubes=((1100, 120, 200), (300, 140, 160)))
    orilla_lejana(c, 420, seed=14)
    agua_frente(c, 'dia', 420, seed=14)
    garza(c, 980, 250, 260, pose='cansada', flip=True)
    sudor(c, 760, 150, 12)
    sudor(c, 1060, 120, 10)
    c.texto('¡uf!', 820, 90, alto=44, angulo=6)
    pez(c, 270, 330, 240, heroe=True, traje=True, fuego=True, pistola=True, chorro=2.5, expr='enojado', aleta=-20,
        boca='decidida')
    salpicadura(c, 800, 330, 150, seed=7)
    c.texto('¡SPLASH!', 780, 470, alto=56, relleno=(0.7, 0.88, 1.0), angulo=-6)
    flecha(c, [(400, 330), (600, 320), (760, 320)])
    nota(c, 60, 612, 'cansada = ¡dispara!')


def garza_despues_1(c):
    """La garza huye mareada perdiendo plumas; los peces saltan felices y el pez celebra."""
    corte(c, 'dia', 470, seed=15, nubes=((300, 120, 180),))
    garza(c, 1080, 170, 180, pose='huyendo')
    estrellas(c, 1260, 110, 50, seed=2)
    for (x, y, r) in ((940, 260, 30), (900, 330, -20), (1000, 330, 60), (860, 230, 10)):
        pluma(c, x, y, 40, r)
    for k, (x, y, r, f) in enumerate(((330, 420, -40, False), (180, 440, 30, True), (760, 430, -30, False))):
        pez(c, x, y, 90, rot=r, flip=f, color=[COLORES_PEZ[4], COLORES_PEZ[2], COLORES_PEZ[1]][k],
            expr='feliz', escamas=False)
        salpicadura(c, x - 10, 470, 90, seed=10 + k, alto=0.6)
    pez(c, 540, 280, 210, heroe=True, traje=True, fuego=True, pistola=True, expr='feliz', aleta=-70, rot=10)
    for (x, y) in ((440, 200), (650, 170)):
        corazon(c, x, y, 16)
    nota(c, 60, 612, 'victoria: los peces saltan')


def garza_despues_2(c):
    """En el Taller, el bagre levanta una pieza nueva y brillante. El pez la mira emocionado."""
    cueva(c, seed=16)
    lampara(c, 700, 110)
    estante(c, 900, 200, 260, seed=3)
    pez(c, 360, 430, 240, heroe=True, traje=True, expr='feliz', mirada=(0.6, -0.4), aleta=50)
    bagre(c, 900, 450, 360, flip=True, pose='pieza', expr='feliz')
    turbina(c, 860, 205, 170, rot=-6)
    signo(c, '!', 470, 260, alto=50)
    nota(c, 60, 610, 'pieza nueva')


# ---------- Jefe 2: el Pelícano Saco Sin Fondo ----------

def pelicano_antes_1(c):
    """Un pico enorme asoma detrás del horizonte, como si saliera el sol. Los peces miran asustados."""
    cielo(c, 'dia', 400, nubes=((300, 120, 170),))
    pelicano(c, 600, 430, 400, pose='vacio', expr='malo', alas=False)
    orilla_lejana(c, 400, seed=17, prof=0.5)
    agua_frente(c, 'dia', 400, seed=17)
    cabeza_asomada(c, 300, 600, 140, expr='asustado', mirada=(0.6, -0.5), horizonte=400)
    cabeza_asomada(c, 150, 620, 100, heroe=False, color=COLORES_PEZ[4], expr='asustado', mirada=(0.6, -0.5), horizonte=400)
    cabeza_asomada(c, 450, 630, 90, heroe=False, color=COLORES_PEZ[3], expr='asustado', mirada=(0.5, -0.6), horizonte=400)
    signo(c, '!!', 340, 450, alto=56)
    nota(c, 860, 612, 'sale detrás del horizonte')


def pelicano_antes_2(c):
    """El pelícano mete el saco abierto en el agua y chupa el cardumen entero."""
    corte(c, 'dia', 330, seed=18, nubes=((1150, 100, 180),))
    pelicano(c, 300, 310, 300, pose='recogiendo', rot=12)
    superficie(c, 330, x0=520, x1=760, seed=8)
    for k, (x, y, r) in enumerate(((820, 430, 165), (960, 470, 170), (900, 560, 160), (1100, 520, 175),
                                   (1060, 410, 170))):
        pez(c, x, y, 85, rot=r, color=[COLORES_PEZ[4], COLORES_PEZ[3], COLORES_PEZ[2],
                                       COLORES_PEZ[1], None][k] and COLORES_PEZ[k % 6], expr='asustado', boca='o', escamas=False)
    for k in range(5):
        lineas_vel(c, 1320 - k * 20, 400 + k * 45, 1150 - k * 10, 420 + k * 30, n=1, seed=20 + k, peso=0.8)
    c.texto('¡SLURP!', 1000, 250, alto=60, angulo=-8)
    flecha(c, [(1250, 520), (1000, 470), (760, 400)])
    nota(c, 60, 612, 'el saco se traga el cardumen')


def pelicano_antes_3(c):
    """El pez dispara al saco lleno de peces: el saco se abre y los peces saltan afuera."""
    cielo(c, 'dia', 660, nubes=((1180, 560, 200), (260, 560, 180)))
    agua_frente(c, 'dia', 600, seed=19)
    pelicano(c, 820, 370, 300, pose='lleno', expr='sorpresa')
    pez(c, 230, 400, 230, heroe=True, traje=True, fuego=True, pistola=True, chorro=3.3, expr='enojado', aleta=-20,
        rot=-12, boca='decidida')
    salpicadura(c, 1120, 330, 130, seed=9)
    for (x, y, r) in ((1180, 170, -50), (1270, 230, 40)):
        pez(c, x, y, 70, rot=r, color=COLORES_PEZ[2], expr='feliz', escamas=False)
    c.texto('¡POP!', 1240, 90, alto=56, angulo=10, relleno=(0.7, 0.88, 1.0))
    flecha(c, [(400, 330), (700, 330), (1020, 330)])
    nota(c, 60, 612, 'disparar al saco')


def pelicano_despues_1(c):
    """El pelícano se va triste con el saco vacío; los peces vuelven al agua."""
    corte(c, 'dia', 470, seed=20, nubes=((380, 120, 170),))
    pelicano(c, 1030, 190, 230, pose='vacio', expr='triste')
    sudor(c, 980, 70, 10)
    for k, (x, y, r) in enumerate(((330, 420, 60), (470, 440, 80), (620, 420, 70))):
        pez(c, x, y, 80, rot=r, color=[COLORES_PEZ[4], COLORES_PEZ[3], COLORES_PEZ[1]][k], expr='feliz',
            escamas=False)
        salpicadura(c, x + 10, 470, 80, seed=30 + k, alto=0.6)
    pez(c, 200, 300, 170, heroe=True, traje=True, fuego=True, expr='feliz', aleta=-70)
    flecha(c, [(1100, 160), (1250, 110), (1350, 80)])
    nota(c, 60, 612, 'se va con el pico vacío')


def pelicano_despues_2(c):
    """El equipo: el pez y otros peces con traje (los defensores), listos para pelear juntos."""
    corte(c, 'dia', 520, seed=21, nubes=((1150, 110, 200), (300, 120, 160)), fondo=None)
    equipo = ((250, 380, 180, (0.62, 0.78, 1.0)), (1130, 380, 180, (0.66, 0.90, 0.60)),
              (450, 210, 140, (1.0, 0.72, 0.82)), (940, 210, 140, (1.0, 0.86, 0.5)))
    for (x, y, s, col) in equipo[2:]:
        pez(c, x, y, s, defensor=True, fuego=True, color=col, prof=0.15)
    for (x, y, s, col) in equipo[:2]:
        pez(c, x, y, s, defensor=True, fuego=True, pistola=True, color=col, flip=x > 690, aleta=-30)
    pez(c, 690, 370, 260, heroe=True, traje=True, fuego=True, pistola=True, expr='feliz', aleta=-50)
    for (x, y) in ((690, 150), (300, 160), (1100, 160)):
        brillo(c, x, y, 22)
    nota(c, 60, 612, 'el equipo: defensores')


# ---------- Jefe 3: el Cormorán Rey ----------

def cormoran_antes_1(c):
    """Bajo el agua: el cardumen mira hacia arriba sin ver que, por abajo, se acerca el Cormorán Rey."""
    bajo_agua(c, fondo=620, seed=22)
    superficie(c, 60, seed=22, desde_abajo=True)
    c.tono([(0, 450), (1380, 450), (1380, 660), (0, 660)], 0.15, suave=40)
    cardumen(c, 950, 230, 6, 90, seed=8, dispersion=(220, 70), expr='normal', mirada=(0.1, -0.9))
    burbujas(c, 180, 560, 8, 9, seed=9, alto=160, ancho=60)
    cormoran(c, 430, 470, 360, rot=-14, pose='buceando')
    lineas_vel(c, 30, 560, 200, 520, n=4, sep=18, seed=7)
    signo(c, '!', 640, 330, alto=60)
    flecha(c, [(250, 540), (500, 450), (760, 330)])
    nota(c, 60, 612, 'el peligro viene por abajo')


def cormoran_antes_2(c):
    """El rey vuela arriba, se zambulle y bucea abajo: una línea punteada muestra su camino."""
    corte(c, 'dia', 300, seed=23, nubes=((1150, 100, 160),), fondo=620)
    cormoran(c, 300, 200, 190, pose='volando', rot=12)
    salpicadura(c, 640, 300, 180, seed=11)
    cormoran(c, 1000, 470, 280, pose='buceando', rot=18)
    camino = spline([(400, 170), (560, 210), (640, 300), (740, 400), (860, 450)], n=10)
    for k in range(0, len(camino) - 1, 3):
        c.trazo(camino[k:k + 2], peso=0.9, suave=False)
    burbujas(c, 760, 430, 6, 8, seed=12, alto=60, ancho=60)
    c.texto('ARRIBA', 220, 280, alto=40, contorno=not c.palito)
    c.texto('ABAJO', 1000, 600, alto=40, contorno=not c.palito)


def cormoran_antes_3(c):
    """Cerca del fondo: el pez se pone delante del cardumen y apunta al Cormorán Rey que llega."""
    bajo_agua(c, fondo=580, seed=24)
    roca(c, 1180, 590, 110, seed=3)
    roca(c, 1000, 600, 70, seed=4)
    cardumen(c, 1120, 450, 6, 80, seed=12, dispersion=(170, 70), flip=True, expr='asustado')
    cormoran(c, 300, 330, 330, pose='buceando', rot=6)
    pez(c, 760, 360, 230, heroe=True, traje=True, pistola=True, chorro=1.4, flip=True, expr='enojado', aleta=-20,
        boca='decidida')
    burbujas(c, 60, 360, 6, 8, seed=13, alto=60, ancho=60)
    flecha(c, [(520, 330), (600, 340)])
    nota(c, 60, 612, 'el pez protege al cardumen')


def cormoran_despues_1(c):
    """El rey se va volando empapado (gotea, corona chueca). El pez le dice adiós."""
    cielo(c, 'dia', 470, nubes=((300, 140, 180),))
    orilla_lejana(c, 470, seed=25)
    agua_frente(c, 'dia', 470, seed=25)
    cormoran(c, 880, 220, 330, pose='empapado', rot=-8)
    c.texto('¡bah!', 1170, 110, alto=44, angulo=8)
    pez(c, 260, 420, 180, heroe=True, traje=True, fuego=True, expr='feliz', aleta=-80)
    flecha(c, [(1000, 200), (1200, 150), (1350, 120)])
    nota(c, 60, 612, 'se va mojado y enojado')


def cormoran_despues_2(c):
    """El bagre revisa el traje con una lupa. En la pared, un plano con la sombra de un águila y un '?'."""
    cueva(c, seed=26)
    lampara(c, 560, 110)
    plano(c, 860, 60, 270, 180, dibujo='aguila')
    soporte(c, 470, 470, 120)
    traje_vacio(c, 470, 380, 240)
    bagre(c, 870, 450, 370, flip=True, pose='lupa', expr='triste')
    pez(c, 200, 520, 140, heroe=True, expr='asustado', mirada=(0.6, -0.4))
    nota(c, 60, 610, 'lo que viene es más grande')


# ---------- Jefe 4: la Reina Águila Marina ----------

def reina_antes_1(c):
    """Cielo rojo. Las aves posadas en un árbol seco, calladas ('...'). El pez asoma preocupado."""
    cielo(c, 'cielo_rojo', 430, nubes=((1000, 120, 260), (450, 160, 200)))
    sol(c, 700, 400, 70, rayos=False)
    orilla_lejana(c, 430, seed=27, prof=0.4)
    agua_frente(c, 'cielo_rojo', 430, seed=27)
    puntas = arbol_seco(c, 1120, 620, 420, seed=1)
    for i, (x, y) in enumerate(puntas):
        gaviota(c, x, y - 36, 95, flip=i % 2 == 0, alas='plegadas', patas='paradas', expr='triste')
    for (x, y) in ((900, 250), (1290, 200)):
        globo(c, x, y, 90, 56, '...', cola=(x + (40 if x < 1000 else -40), y + 70), alto=30)
    juncos(c, 260, 660, 200, n=5, seed=8)
    cabeza_asomada(c, 640, 610, 120, expr='asustado', mirada=(0.6, -0.5), amb='cielo_rojo', horizonte=430)
    nota(c, 60, 612, 'silencio...')


def reina_antes_2(c):
    """La Reina Águila Marina baja de las nubes, de frente, con las garras por delante."""
    cielo(c, 'cielo_rojo', 660, nubes=((250, 140, 300), (1150, 130, 320)))
    for k in range(12):
        a = np.radians(k * 30 + 5)
        r0, r1 = 300, 520
        c.linea((760 + np.cos(a) * r0, 300 + np.sin(a) * r0 * 0.7), (760 + np.cos(a) * r1, 300 + np.sin(a) * r1 * 0.7),
                peso=0.7, presion=0.5)
    aguila(c, 760, 300, 230, pose='garras')
    agua_frente(c, 'cielo_rojo', 600, seed=28)
    cabeza_asomada(c, 150, 630, 100, expr='asustado', mirada=(0.7, -0.6), amb='cielo_rojo', horizonte=600)
    nota(c, 60, 612, 'de frente, garras primero')


def reina_antes_3(c):
    """La reina grita (¡KRIII!) y el cielo se llena de gaviotas."""
    cielo(c, 'cielo_rojo', 660, nubes=((300, 100, 180),))
    bandada(c, 1050, 220, 26, 50, seed=5, dispersion=(300, 170), prof=0.3)
    for (x, y, r) in ((900, 470, 10), (1180, 420, -10)):
        gaviota(c, x, y, 140, rot=r, flip=True, alas='extendidas', pico_abierto=True)
    aguila(c, 380, 330, 190, pose='grito')
    for k in range(4):
        r = 260 + k * 40
        c.trazo(elipse(380, 260, r, r * 0.8, a0=-40, a1=40, n=10), peso=0.9, presion=0.8)
    c.texto('¡KRIIII!', 660, 110, alto=64, angulo=-6, relleno=(1.0, 0.75, 0.6))
    nota(c, 60, 612, 'llama a la bandada')


def reina_antes_4(c):
    """La última batalla: el pez y los defensores suben volando hacia la reina y su bandada."""
    cielo(c, 'cielo_rojo', 660, nubes=((420, 140, 220),))
    aguila(c, 1110, 140, 100, pose='garras', prof=0.25)
    bandada(c, 1050, 160, 14, 34, seed=6, dispersion=(260, 90), prof=0.45)
    agua_frente(c, 'cielo_rojo', 580, seed=29)
    salpicadura(c, 330, 590, 200, seed=13)
    for (x, y, s, col) in ((200, 360, 140, (0.62, 0.78, 1.0)), (720, 470, 150, (0.66, 0.90, 0.60)),
                           (320, 220, 120, (1.0, 0.72, 0.82))):
        pez(c, x, y, s, defensor=True, fuego=True, pistola=True, color=col, rot=30, prof=0.1, aleta=-30)
    pez(c, 520, 360, 300, heroe=True, traje=True, fuego=True, pistola=True, expr='enojado', rot=30, aleta=-30)
    lineas_vel(c, 320, 600, 440, 470, n=5, sep=24, seed=8, peso=0.9)
    flecha(c, [(500, 360), (800, 230), (1000, 160)])
    nota(c, 60, 612, '¡todos al ataque!')


# ---------- Final ----------

def final_1(c):
    """Atardecer: la reina y su bandada, chiquitas, se van hacia el sol."""
    cielo(c, 'atardecer', 420, nubes=((300, 130, 200),))
    sol(c, 760, 420, 110, rayos=False)
    aguila(c, 860, 300, 40, pose='lejos')
    bandada(c, 900, 290, 12, 18, seed=7, dispersion=(120, 40), prof=0.3)
    orilla_lejana(c, 420, seed=30, prof=0.5)
    agua_frente(c, 'atardecer', 420, seed=30)
    if not c.palito:
        for k in range(10):
            y = 440 + k * 18
            w = 120 - k * 6
            c.aclarar([(760 - w, y), (760 + w, y), (760 + w, y + 5), (760 - w, y + 5)], fuerza=0.35, suave=3)
    juncos(c, 70, 660, 280, n=6, seed=9)
    juncos(c, 1320, 660, 260, n=6, seed=10)
    cabeza_asomada(c, 360, 610, 130, expr='feliz', mirada=(0.6, -0.4), traje=True, amb='atardecer', horizonte=420)
    flecha(c, [(950, 280), (1100, 300)])
    nota(c, 860, 600, 'se van lejos, muy lejos')


def final_2(c):
    """El lago en calma: nenúfares con flores, juncos, una libélula y ondas suaves."""
    cielo(c, 'atardecer', 360, nubes=((350, 120, 200), (1100, 150, 160)))
    sol(c, 1040, 360, 70, rayos=False)
    orilla_lejana(c, 360, seed=31, prof=0.5)
    agua_frente(c, 'atardecer', 360, seed=31)
    for i, (x, y, r, fl) in enumerate(((330, 460, 120, True), (760, 420, 90, False), (1040, 520, 140, True),
                                       (560, 590, 110, True), (1250, 430, 70, False))):
        nenufar(c, x, y, r, flor=fl, seed=20 + i)
    for (x, y, r) in ((660, 480, 40), (900, 600, 55)):
        for k in range(3):
            c.contorno(elipse(x, y, r * (1 + k * 0.6), r * 0.25 * (1 + k * 0.6), n=30), peso=0.5, presion=0.5)
    juncos(c, 90, 660, 300, n=7, seed=11)
    juncos(c, 1300, 660, 260, n=5, seed=12)
    lx, ly = 700, 260
    c.trazo([(lx - 40, ly), (lx + 40, ly)], peso=1.0, suave=False)
    for dx in (-10, 8):
        for dy in (-1, 1):
            P = elipse(lx + dx, ly + dy * 16, 10, 22, rot=dy * 20, n=14)
            c.tapar(P)
            c.contorno(P, peso=0.6)
    nota(c, 60, 612, 'paz')


def final_3(c):
    """Bajo el agua, todo el cardumen nada en ronda alrededor del pez del traje, feliz (corazones)."""
    bajo_agua(c, fondo=620, seed=32)
    algas(c, 100, 625, 260, n=5, seed=13)
    algas(c, 1280, 625, 240, n=5, seed=14)
    for k in range(10):
        a = np.radians(k * 36)
        x, y = 690 + np.cos(a) * 400, 340 + np.sin(a) * 200
        pez(c, x, y, 95, rot=18 * np.cos(a), flip=np.cos(a) > 0, color=COLORES_PEZ[k % len(COLORES_PEZ)], expr='feliz')
    pez(c, 690, 340, 260, heroe=True, traje=True, pistola=True, expr='feliz', aleta=20)
    for (x, y) in ((540, 140), (850, 130), (690, 560), (420, 470), (980, 470)):
        corazon(c, x, y, 18)
    nota(c, 60, 612, 'ronda de peces')


def final_4(c):
    """El bagre guarda el traje en la vitrina con mucho cuidado; el pez (ya sin traje) mira contento."""
    cueva(c, seed=33)
    lampara(c, 850, 100)
    vitrina(c, 700, 170, 300, 320)
    bagre(c, 1140, 430, 360, flip=True, pose='guardando', expr='feliz')
    pez(c, 300, 420, 170, heroe=True, expr='feliz', mirada=(0.6, -0.2))
    corazon(c, 380, 300, 16)
    nota(c, 60, 610, 'el traje a la vitrina')


def final_5(c):
    """El pez duerme bajo un nenúfar, entre las algas, y suelta burbujas 'z'."""
    corte(c, 'atardecer', 230, seed=34, nubes=((400, 100, 160),), rayos=3)
    sol(c, 1120, 230, 70, rayos=False)
    nenufar(c, 700, 230, 230, flor=True, seed=40)
    c.trazo([(700, 235), (690, 420), (705, 610)], peso=0.7)
    algas(c, 380, 615, 260, n=5, seed=15)
    algas(c, 1000, 615, 240, n=5, seed=16)
    pez(c, 690, 440, 240, heroe=True, expr='dormido', boca='plana', rot=-4)
    zzz(c, 860, 330, alto=40)
    burbujas(c, 840, 380, 3, 9, seed=17, alto=40)
    nota(c, 60, 612, 'a dormir')


def final_6(c):
    """De noche, el Taller oscuro: el traje brilla en su vitrina y en el vidrio se refleja la sombra de un ave."""
    cueva(c, seed=35, luz=(690, 60))
    if not c.palito:
        c.degradado(0, 660, (0.70, 0.72, 0.82), (0.62, 0.62, 0.70))
        c.aclarar([(600, 0), (780, 0), (900, 660), (480, 660)], fuerza=0.35, suave=30)
    vitrina(c, 540, 150, 300, 340)
    gaviota(c, 790, 205, 80, flip=True, silueta=True, prof=0.55)
    brillo(c, 760, 300, 40)
    brillo(c, 610, 210, 18)
    signo(c, '...', 1080, 520, alto=60)
    nota(c, 60, 612, 'el traje espera, listo')


ESCENAS = {k: v for k, v in list(globals().items()) if callable(v) and k.split('_')[0] in
           ('origen', 'taller', 'garza', 'pelicano', 'cormoran', 'reina', 'final') and k[-1].isdigit()}
