# -*- coding: utf-8 -*-
"""Objetos, efectos y fondos de las viñetas (gotas, burbujas, salpicaduras, nenúfares, juncos, algas, rocas, sol,
nubes, orilla, superficie del agua, chatarra, cueva y muebles del Taller), en los dos modos del Lienzo (palito y lápiz).
Los personajes están en personajes.py.
"""
import numpy as np
from lapiz import T, spline, elipse, banda, menos, mas, dentro

AGUA = (0.78, 0.90, 1.0)
VIDRIO = (0.86, 0.93, 0.98)


def pw(s):
    """Grosor del contorno según el tamaño (lo grande, más marcado)."""
    return float(np.clip(0.45 + s / 300.0, 0.55, 1.6))


class lejos:
    """Con 'with lejos(c, prof):' todo lo de adentro sale más claro (perspectiva aérea)."""

    def __init__(self, c, prof):
        self.c, self.prof = c, prof

    def __enter__(self):
        self.old = self.c.fade
        if not self.c.palito:
            self.c.fade = self.old * (1 - 0.6 * self.prof)

    def __exit__(self, *a):
        self.c.fade = self.old


def pieza(c, t, pts, peso, presion=0.85, color=None, fuerza=0.45, sombra=0.0, k=0.10, oscuro=0.0, tapar=True,
          cerrado=True, rayas=5.0, ang=50.0, cruzado=False, n=8):
    """Una parte: tapa lo de atrás, aguada, tono, sombra a rayas (abajo a la derecha) y contorno."""
    P = spline(t(pts), cerrado=cerrado, n=n) if n > 1 else np.asarray(t(pts), float)   # n <= 1: esquinas rectas
    if tapar and cerrado:
        c.tapar(P)
    if c.palito:
        c.trazo(P, peso=peso, cerrado=cerrado, punta=not cerrado, suave=False)
        return P
    if color is not None and cerrado:
        c.aguada(P, color, fuerza)
    if oscuro > 0 and cerrado:
        c.tono(P, oscuro, suave=0.6, mancha=0.25)
    if sombra > 0 and cerrado:
        d = k * t.s
        sh = menos(c.mascara(P), c.mascara(P + [-d * 0.55, -d]))
        c.rayado(sh, angulo=ang, espacio=rayas, presion=sombra, cruzado=cruzado)
    c.trazo(P, peso=peso, presion=presion, cerrado=cerrado, punta=not cerrado, suave=False)
    return P


def llave(c, t, p):
    """Llave inglesa."""
    if c.palito:
        c.trazo(t([(0, 0), (0.45, 0)]), peso=p, suave=False)
        return
    mango = [(0.0, -0.03), (0.38, -0.035), (0.38, 0.035), (0.0, 0.03)]
    pieza(c, t, mango, p * 0.7, color=(0.75, 0.76, 0.80), fuerza=0.5, sombra=0.3, n=2)
    boca = [(0.36, -0.06), (0.50, -0.10), (0.56, -0.04), (0.48, -0.02), (0.48, 0.02), (0.56, 0.04), (0.50, 0.10),
            (0.36, 0.06)]
    pieza(c, t, boca, p * 0.7, color=(0.75, 0.76, 0.80), fuerza=0.5, sombra=0.3, n=3)


# ======================= OBJETOS Y EFECTOS =======================

def gota(c, x, y, r):
    P = np.array([(x, y - r * 1.8), (x + r, y), (x + r * 0.7, y + r * 0.75), (x, y + r), (x - r * 0.7, y + r * 0.75),
                  (x - r, y)])
    P = spline(P, cerrado=True, n=4)
    c.tapar(P)
    c.aguada(P, AGUA, 0.6)
    c.contorno(P, peso=0.55, presion=0.75)


def sudor(c, x, y, r=9):
    for k, (dx, dy) in enumerate(((0, 0), (r * 1.6, r * 1.2))):
        gota(c, x + dx, y + dy, r * (1 - 0.3 * k))


def burbujas(c, x, y, n=6, r=8, seed=0, alto=120, ancho=30):
    rng = np.random.default_rng(seed)
    for i in range(n):
        bx = x + rng.normal(0, ancho * 0.4)
        by = y - rng.uniform(0, alto)
        br = r * rng.uniform(0.5, 1.2) * (1 - 0.4 * (y - by) / max(alto, 1))
        P = elipse(bx, by, br, br, n=14)
        c.tapar(P)
        c.contorno(P, peso=0.55, presion=0.7)
        if not c.palito:
            c.trazo(elipse(bx, by, br * 0.6, br * 0.6, a0=200, a1=250, n=5), peso=0.5, presion=0.6)


def salpicadura(c, x, y, s, seed=0, alto=1.0):
    """Corona de agua y gotas (s = ancho)."""
    rng = np.random.default_rng(seed)
    p = pw(s * 0.6)
    pts = [(x - s * 0.5, y)]
    for k in range(7):
        fx = -0.42 + k * 0.14
        pts.append((x + fx * s, y - s * alto * (0.28 + 0.25 * rng.random()) * (1 - abs(fx))))
        pts.append((x + (fx + 0.07) * s, y - s * alto * 0.12))
    pts.append((x + s * 0.5, y))
    P = np.array(pts)
    if c.palito:
        c.trazo(P, peso=p, suave=False)
    else:
        c.tapar(P)
        c.aguada(P, AGUA, 0.45)
        c.trazo(P, peso=p * 0.8, presion=0.8)
    for k in range(10):
        a = np.radians(rng.uniform(200, 340))
        d = s * rng.uniform(0.35, 0.7)
        gx, gy = x + np.cos(a) * d, y + np.sin(a) * d * alto
        gota(c, gx, gy, s * rng.uniform(0.015, 0.03))


def lineas_vel(c, x0, y0, x1, y1, n=5, sep=14, seed=0, peso=0.7):
    """Líneas de movimiento (de dónde viene algo)."""
    rng = np.random.default_rng(seed)
    d = np.array([x1 - x0, y1 - y0], float)
    L = np.linalg.norm(d) + 1e-6
    nn = np.array([-d[1], d[0]]) / L
    for k in range(n):
        off = (k - (n - 1) / 2) * sep + rng.normal(0, sep * 0.2)
        a = rng.uniform(0, 0.25)
        b = rng.uniform(0.7, 1.0)
        p0 = np.array([x0, y0]) + d * a + nn * off
        p1 = np.array([x0, y0]) + d * b + nn * off
        c.linea(p0, p1, peso=peso, presion=0.7)


def flecha(c, pts, peso=1.2):
    """Flecha de movimiento: solo en el guion gráfico."""
    if not c.palito:
        return
    P = spline(pts, n=8) if len(pts) > 2 else np.asarray(pts, float)
    c.trazo(P, peso=peso, suave=False)
    d = P[-1] - P[-3 if len(P) > 2 else 0]
    d /= np.linalg.norm(d) + 1e-6
    n = np.array([-d[1], d[0]])
    tip = P[-1]
    c.trazo([tip - d * 18 + n * 10, tip, tip - d * 18 - n * 10], peso=peso, suave=False)


def nota(c, x, y, s, alto=38):
    """Nota escrita: solo en el guion gráfico."""
    if c.palito:
        c.texto(s, x, y, alto=alto, fuente='Rajdhani-Bold.ttf', contorno=False, centro=False)


def signo(c, s, x, y, alto=60, angulo=0):
    c.texto(s, x, y, alto=alto, angulo=angulo)


def estrellas(c, x, y, r, n=3, seed=0):
    """Estrellitas de mareo o brillo."""
    rng = np.random.default_rng(seed)
    for i in range(n):
        a = 6.283 * i / n + rng.uniform(0, 0.5)
        cx, cy = x + np.cos(a) * r, y + np.sin(a) * r * 0.4
        rr = r * 0.28
        pts = []
        for k in range(8):
            ang = np.radians(k * 45 - 90)
            rad = rr if k % 2 == 0 else rr * 0.35
            pts.append((cx + np.cos(ang) * rad, cy + np.sin(ang) * rad))
        c.tapar(pts)
        c.aguada(pts, (1.0, 0.9, 0.5), 0.6)
        c.trazo(pts, peso=0.6, cerrado=True, suave=False)


def brillo(c, x, y, r):
    """Destello de 4 puntas (algo nuevo, una idea)."""
    pts = []
    for k in range(8):
        ang = np.radians(k * 45)
        rad = r if k % 2 == 0 else r * 0.22
        pts.append((x + np.cos(ang) * rad, y + np.sin(ang) * rad))
    c.tapar(pts)
    c.trazo(pts, peso=0.7, cerrado=True, suave=False)


def corazon(c, x, y, r):
    pts = [(x, y + r * 0.9), (x - r, y), (x - r * 0.9, y - r * 0.6), (x - r * 0.45, y - r * 0.8), (x, y - r * 0.35),
           (x + r * 0.45, y - r * 0.8), (x + r * 0.9, y - r * 0.6), (x + r, y)]
    P = spline(pts, cerrado=True, n=5)
    c.tapar(P)
    c.aguada(P, (1.0, 0.6, 0.6), 0.6)
    c.contorno(P, peso=0.7)


def zzz(c, x, y, alto=30):
    for k in range(3):
        c.texto('z', x + k * alto * 0.8, y - k * alto * 0.8, alto=alto * (0.7 + 0.25 * k), contorno=not c.palito)


# ---------- Lago ----------

def nenufar(c, x, y, r, flor=False, seed=0):
    rng = np.random.default_rng(seed)
    P = elipse(x, y, r, r * 0.3, n=30, a0=12, a1=348)
    P = np.vstack([P, [[x, y]]])
    if c.palito:
        c.contorno(P, peso=0.8, suave=False)
        return
    c.tapar(P)
    c.aguada(P, (0.44, 0.70, 0.40), 0.9)
    c.rayado(menos(c.mascara(P), c.mascara(P + [0, -r * 0.12])), angulo=10, espacio=4, presion=0.35)
    c.contorno(P, peso=0.8, presion=0.8, suave=False)
    for a in (60, 120, 200, 300):
        q = np.radians(a)
        c.linea((x, y), (x + np.cos(q) * r * 0.8, y + np.sin(q) * r * 0.24), peso=0.4, presion=0.4)
    if flor:
        fx, fy = x + rng.uniform(-0.3, 0.3) * r, y - r * 0.08
        for k in range(7):
            a = np.radians(-150 + k * 20)
            pts = [(fx, fy), (fx + np.cos(a) * r * 0.22 - 4, fy + np.sin(a) * r * 0.22),
                   (fx + np.cos(a) * r * 0.34, fy + np.sin(a) * r * 0.34),
                   (fx + np.cos(a) * r * 0.22 + 4, fy + np.sin(a) * r * 0.22)]
            Q = spline(pts, cerrado=True, n=4)
            c.tapar(Q)
            c.aguada(Q, (0.98, 0.62, 0.78), 0.9)
            c.contorno(Q, peso=0.6)


def juncos(c, x, y, alto, n=5, seed=0, prof=0.0, ancho=60):
    rng = np.random.default_rng(seed)
    with lejos(c, prof):
        for i in range(n):
            bx = x + rng.uniform(-ancho, ancho)
            h = alto * rng.uniform(0.7, 1.1)
            curva = rng.uniform(-0.12, 0.12) * h
            tallo = [(bx, y), (bx + curva * 0.4, y - h * 0.5), (bx + curva, y - h)]
            if c.palito:
                c.trazo(tallo, peso=0.8)
                continue
            c.trazo(tallo, peso=0.7, presion=0.8)
            if rng.random() < 0.6:
                q = (bx + curva, y - h)
                P = elipse(q[0], q[1] + h * 0.08, h * 0.035, h * 0.1, rot=np.degrees(np.arctan2(curva, h)), n=16)
                c.tapar(P)
                c.aguada(P, (0.55, 0.36, 0.22), 0.9)
                c.rayado(P, angulo=80, espacio=2.5, presion=0.6)
                c.contorno(P, peso=0.6)
            # hoja larga
            lado = rng.choice([-1, 1])
            hoja = [(bx, y), (bx + lado * h * 0.15, y - h * 0.4), (bx + lado * h * 0.32, y - h * 0.65)]
            P = banda(hoja, h * 0.04, 0.5)
            c.tapar(P)
            c.aguada(P, (0.50, 0.70, 0.36), 0.9)
            c.contorno(P, peso=0.5, presion=0.7)


def algas(c, x, y, alto, n=4, seed=0, ancho=50):
    rng = np.random.default_rng(seed)
    for i in range(n):
        bx = x + rng.uniform(-ancho, ancho)
        h = alto * rng.uniform(0.6, 1.1)
        fase = rng.uniform(0, 6.28)
        eje = [(bx + np.sin(fase + k * 1.3) * 12, y - h * k / 5) for k in range(6)]
        P = banda(eje, 14, 6)
        if c.palito:
            c.trazo(eje, peso=0.8)
            continue
        c.tapar(P)
        c.aguada(P, (0.34, 0.62, 0.40), 0.9)
        c.rayado(P, angulo=70, espacio=5, presion=0.3)
        c.contorno(P, peso=0.6, presion=0.75)


def roca(c, x, y, r, seed=0, oscura=0.3):
    rng = np.random.default_rng(seed)
    pts = []
    for k in range(9):
        a = np.radians(180 + k * 22.5)
        rr = r * rng.uniform(0.75, 1.05)
        pts.append((x + np.cos(a) * rr * 1.3, y + np.sin(a) * rr * 0.8))
    pts += [(x + r * 1.3, y + r * 0.1), (x - r * 1.3, y + r * 0.1)]
    P = spline(pts, cerrado=True, n=4)
    c.tapar(P)
    if c.palito:
        c.contorno(P, peso=0.8)
        return
    c.aguada(P, (0.60, 0.62, 0.66), 0.9)
    c.rayado(menos(c.mascara(P), c.mascara(P + [-r * 0.3, -r * 0.4])), espacio=4, presion=oscura + 0.15,
             cruzado=True)
    c.contorno(P, peso=0.8)


def sol(c, x, y, r, rayos=True):
    P = elipse(x, y, r, r, n=40)
    c.tapar(P)
    if not c.palito:
        c.aguada(P, (1.0, 0.84, 0.38), 0.9)
    c.contorno(P, peso=0.7, presion=0.7)
    if rayos:
        for k in range(12):
            a = np.radians(k * 30 + 8)
            c.linea((x + np.cos(a) * r * 1.25, y + np.sin(a) * r * 1.25), (x + np.cos(a) * r * 1.6,
                    y + np.sin(a) * r * 1.6), peso=0.6, presion=0.6)


def nube(c, x, y, w, seed=0, oscura=0.0):
    rng = np.random.default_rng(seed)
    pts = []
    n = 7
    for k in range(n):
        u = -0.5 + k / (n - 1)
        pts.append((x + u * w, y - w * (0.12 + 0.12 * np.sin(np.pi * (u + 0.5))) - rng.uniform(0, w * 0.06)))
    pts += [(x + w * 0.55, y), (x - w * 0.55, y)]
    P = spline(pts, cerrado=True, n=6)
    c.tapar(P)
    if c.palito:
        c.contorno(P, peso=0.7)
        return
    if oscura > 0:
        c.tono(P, oscura, suave=4)
    c.rayado(menos(c.mascara(P), c.mascara(P + [0, -w * 0.1])), angulo=20, espacio=6, presion=0.25)
    c.contorno(P, peso=0.6, presion=0.5)


def orilla_lejana(c, y, seed=0, prof=0.6, x0=0, x1=1380):
    """Árboles y colinas del otro lado del lago, sobre la línea y."""
    rng = np.random.default_rng(seed)
    with lejos(c, prof):
        colina = [(x0 - 20, y)]
        for x in np.linspace(x0, x1, 9):
            colina.append((x, y - rng.uniform(20, 55)))
        colina.append((x1 + 20, y))
        P = spline(colina, n=6)
        F = np.vstack([P, [[x1 + 20, y + 4], [x0 - 20, y + 4]]])
        c.tapar(F)
        if not c.palito:
            c.aguada(F, (0.56, 0.74, 0.48), 0.8)
        c.trazo(P, peso=0.6, presion=0.6)
        x = x0 + rng.uniform(0, 40)
        while x < x1:
            h = rng.uniform(25, 60)
            base = y - rng.uniform(2, 14)
            if rng.random() < 0.5:
                pts = [(x - h * 0.22, base), (x, base - h), (x + h * 0.22, base)]
                c.tapar(pts)
                if not c.palito:
                    c.aguada(pts, (0.30, 0.52, 0.36), 0.9)
                    c.rayado(pts, angulo=60, espacio=4, presion=0.3)
                c.trazo(pts, peso=0.6, presion=0.6, suave=False)
            else:
                P = elipse(x, base - h * 0.55, h * 0.3, h * 0.38, n=16)
                c.tapar(P)
                if not c.palito:
                    c.aguada(P, (0.38, 0.62, 0.36), 0.9)
                c.contorno(P, peso=0.6, presion=0.6)
                c.linea((x, base - h * 0.2), (x, base), peso=0.5)
                if not c.palito:
                    c.rayado(menos(c.mascara(P), c.mascara(P + [-6, -8])), espacio=4, presion=0.3)
            x += rng.uniform(25, 70)


def superficie(c, y, x0=0, x1=1380, olas=True, seed=0, desde_abajo=False):
    """Línea del agua (vista de lado) con ondas."""
    rng = np.random.default_rng(seed)
    xs = np.linspace(x0, x1, 40)
    ys = y + np.sin(xs / 37 + rng.uniform(0, 6)) * 2.5
    c.trazo(np.stack([xs, ys], 1), peso=0.9, presion=0.85)
    if olas and not c.palito:
        for k in range(26):
            wx = rng.uniform(x0, x1)
            wy = y + (rng.uniform(-6, -2) if not desde_abajo else rng.uniform(4, 12))
            c.trazo(elipse(wx, wy, rng.uniform(8, 16), 3, a0=200, a1=340, n=6), peso=0.45, presion=0.5)


def agua_rayitas(c, y0, y1, x0=0, x1=1380, seed=0, densidad=1.0):
    """Superficie del lago vista de frente: rayitas horizontales (más juntas a lo lejos)."""
    if c.palito:
        return
    rng = np.random.default_rng(seed)
    n = int(70 * densidad)
    for k in range(n):
        f = rng.random() ** 1.5
        yy = y0 + (y1 - y0) * f
        xx = rng.uniform(x0, x1)
        L = rng.uniform(10, 40) * (0.4 + f)
        c.linea((xx, yy), (xx + L, yy), peso=0.45, presion=0.35 + 0.25 * f)


def rayos_luz(c, n=5, seed=0, y1=660):
    """Rayos de sol que entran al agua (bajo el agua)."""
    if c.palito:
        return
    rng = np.random.default_rng(seed)
    for k in range(n):
        x = rng.uniform(80, 1300)
        w0, w1 = rng.uniform(20, 50), rng.uniform(70, 140)
        dx = rng.uniform(-150, -60)
        P = [(x - w0, 0), (x + w0, 0), (x + dx + w1, y1), (x + dx - w1, y1)]
        c.aclarar(P, fuerza=0.22, suave=14)


def fondo_lago(c, y, seed=0, x0=0, x1=1380):
    """Fondo del lago (arena y piedritas)."""
    rng = np.random.default_rng(seed)
    xs = np.linspace(x0 - 10, x1 + 10, 30)
    ys = y + np.sin(xs / 90 + rng.uniform(0, 6)) * 8 + rng.normal(0, 1.5, len(xs))
    P = np.vstack([np.stack([xs, ys], 1), [[x1 + 10, 700], [x0 - 10, 700]]])
    c.tapar(P)
    if not c.palito:
        c.aguada(P, (0.92, 0.80, 0.56), 0.9)
        c.rayado(P, angulo=15, espacio=7, presion=0.25)
    c.trazo(np.stack([xs, ys], 1), peso=0.9)
    if not c.palito:
        for k in range(30):
            px, py = rng.uniform(x0, x1), y + rng.uniform(8, 60)
            r = rng.uniform(2, 6)
            c.contorno(elipse(px, py, r * 1.4, r, n=10), peso=0.45, presion=0.55)


# ---------- Chatarra ----------

def neumatico(c, x, y, r, rot=0):
    out = elipse(x, y, r, r * 0.55, rot=rot, n=36)
    inn = elipse(x, y, r * 0.5, r * 0.26, rot=rot, n=24)
    c.tapar(out)
    if not c.palito:
        c.tono(out, 0.45, suave=0.6)
        c.tapar(inn)
        c.tono(inn, 0.2, suave=0.6)
        for k in range(16):
            a = np.radians(k * 22.5)
            p0 = (x + np.cos(a) * r * 0.85, y + np.sin(a) * r * 0.47)
            p1 = (x + np.cos(a) * r * 0.98, y + np.sin(a) * r * 0.54)
            c.linea(p0, p1, peso=0.5, presion=0.8)
    c.contorno(out, peso=0.9)
    c.contorno(inn, peso=0.8)


def lata(c, x, y, r, h, rot=0):
    t = T(x, y, 1.0, rot)
    cuerpo = [(-r, -h / 2), (r, -h / 2), (r, h / 2), (-r, h / 2)]
    pieza(c, t, cuerpo, 0.8, color=(0.66, 0.70, 0.76), fuerza=0.9, sombra=0.35, k=r * 0.6, n=2)
    tapa = t(elipse(0, -h / 2, r, r * 0.3, n=20))
    c.tapar(tapa)
    c.contorno(tapa, peso=0.7)
    for v in (-h * 0.2, 0, h * 0.2):
        c.trazo(t([(-r, v), (r, v)]), peso=0.4, presion=0.5, suave=False)


def frasco(c, x, y, r, rot=0):
    t = T(x, y, r, rot)
    cuerpo = [(-0.8, -0.7), (-1.0, -0.3), (-1.0, 0.9), (1.0, 0.9), (1.0, -0.3), (0.8, -0.7)]
    P = spline(t(cuerpo), cerrado=True, n=4)
    c.tapar(P)
    if not c.palito:
        c.aguada(P, VIDRIO, 0.35)
    c.contorno(P, peso=0.8)
    pieza(c, t, [(-0.85, -0.7), (0.85, -0.7), (0.85, -1.0), (-0.85, -1.0)], 0.8, color=(0.8, 0.8, 0.8), fuerza=0.4,
          sombra=0.3, n=2)
    if not c.palito:
        c.trazo(t([(-0.6, -0.2), (-0.6, 0.6)]), peso=1.2, borrar=True, pasadas=1)


def motor_viejo(c, x, y, s, rot=0):
    """Motor fuera de borda viejo."""
    t = T(x, y, s, rot)
    p = pw(s)
    pieza(c, t, [(-0.5, -0.5), (0.4, -0.55), (0.5, -0.1), (-0.45, -0.05)], p, color=(0.78, 0.40, 0.30), fuerza=0.9,
          sombra=0.4, n=3)
    pieza(c, t, banda([(0.0, -0.05), (0.0, 0.55)], 0.18), p, color=(0.56, 0.58, 0.62), fuerza=0.9, sombra=0.3, n=2)
    for a in (0, 120, 240):
        tt = t.hijo(0.0, 0.62, 1.0, a)
        pieza(c, tt, [(0.0, 0.0), (0.12, -0.12), (0.25, -0.05), (0.2, 0.05)], p * 0.8, oscuro=0.2, n=3)
    for u in (-0.3, -0.1, 0.1):
        c.trazo(t([(u, -0.45), (u + 0.02, -0.15)]), peso=p * 0.5, suave=False)
    if not c.palito:
        c.contorno(t(elipse(0.25, -0.32, 0.06, 0.06, n=10)), peso=p * 0.6)


def engranaje(c, x, y, r, dientes=10, rot=0.0):
    pts = []
    for k in range(dientes * 2):
        a = np.radians(rot + k * 180 / dientes)
        rr = r if k % 2 == 0 else r * 0.8
        a2 = np.radians(rot + (k + 0.6) * 180 / dientes)
        pts.append((x + np.cos(a) * rr, y + np.sin(a) * rr))
        pts.append((x + np.cos(a2) * rr, y + np.sin(a2) * rr))
    c.tapar(pts)
    if not c.palito:
        c.aguada(pts, (0.72, 0.66, 0.52), 0.9)
        c.rayado(menos(c.mascara(pts), c.mascara(np.array(pts) + [-r * 0.2, -r * 0.25])), espacio=4, presion=0.35)
    c.trazo(pts, peso=0.8, cerrado=True, suave=False)
    c.contorno(elipse(x, y, r * 0.3, r * 0.3, n=14), peso=0.7)


# ---------- Taller ----------

def cueva(c, seed=0, techo=True, luz=(700, 120)):
    """Paredes de roca alrededor (marco oscuro) y luz cálida al centro."""
    rng = np.random.default_rng(seed)
    if c.palito:
        c.trazo([(0, 130), (120, 60), (300, 20), (700, 0)], peso=1.0)
        c.trazo([(1380, 140), (1260, 60), (1080, 10)], peso=1.0)
        return
    c.degradado(0, 660, (0.80, 0.74, 0.64), (0.64, 0.58, 0.50))
    c.aclarar(elipse(luz[0], luz[1] + 200, 520, 300, n=40), fuerza=0.45, suave=60)
    bordes = [
        [(-20, -20), (520, -20), (420, 30), (260, 50), (150, 120), (70, 260), (40, 420), (60, 560), (-20, 690)],
        [(1400, -20), (900, -20), (1000, 30), (1150, 60), (1260, 150), (1330, 300), (1340, 480), (1310, 680),
         (1400, 690)],
    ]
    for B in bordes:
        P = spline(B, cerrado=True, n=5)
        c.tapar(P)
        c.aguada(P, (0.42, 0.38, 0.36), 0.95)
        c.tono(P, 0.18, suave=8)
        c.rayado(P, angulo=55, espacio=6, presion=0.35, cruzado=True)
        c.trazo(spline(B[1:-1], n=5), peso=1.0)
    if techo:
        for k in range(7):
            x = rng.uniform(300, 1100)
            h = rng.uniform(25, 60)
            P = [(x - h * 0.25, 0), (x + h * 0.25, 0), (x + rng.uniform(-4, 4), h)]
            c.tapar(P)
            c.rayado(P, angulo=80, espacio=4, presion=0.4)
            c.trazo(P, peso=0.7, suave=False)


def lampara(c, x, y):
    c.linea((x, 0), (x, y - 18), peso=0.6)
    P = elipse(x, y, 16, 20, n=20)
    if not c.palito:
        c.aguada(elipse(x, y + 10, 140, 120, n=30), (1.0, 0.92, 0.7), 0.35)
    c.tapar(P)
    if not c.palito:
        c.aguada(P, (1.0, 0.92, 0.6), 0.7)
    c.contorno(P, peso=0.7)
    pieza(c, T(x, y, 1.0), [(-12, -16), (12, -16), (8, -28), (-8, -28)], 0.7, color=(0.7, 0.7, 0.7), fuerza=0.4,
          n=2)
    if not c.palito:
        for k in range(8):
            a = np.radians(k * 45 + 20)
            c.linea((x + np.cos(a) * 26, y + np.sin(a) * 28), (x + np.cos(a) * 36, y + np.sin(a) * 38), peso=0.5,
                    presion=0.5)


def banco(c, x, y, w, h=24):
    """Mesa de trabajo (x, y = esquina izquierda de la tabla)."""
    t = T(0, 0, 1.0)
    for xx in (x + 20, x + w - 34):
        pieza(c, t, [(xx, y + h), (xx + 14, y + h), (xx + 14, 660), (xx, 660)], 0.8, color=(0.64, 0.44, 0.28),
              fuerza=0.9, sombra=0.4, k=6, n=2)
    pieza(c, t, [(x, y), (x + w, y), (x + w, y + h), (x, y + h)], 0.9, color=(0.72, 0.50, 0.32), fuerza=0.9,
          sombra=0.3, k=8, n=2)
    if not c.palito:
        for k in range(4):
            xx = x + w * (0.15 + 0.22 * k)
            c.linea((xx, y + 4), (xx + 30, y + 4), peso=0.4, presion=0.4)


def estante(c, x, y, w, seed=0):
    rng = np.random.default_rng(seed)
    pieza(c, T(0, 0, 1.0), [(x, y), (x + w, y), (x + w, y + 10), (x, y + 10)], 0.8, color=(0.9, 0.82, 0.7),
          fuerza=0.4, n=2)
    xx = x + 10
    while xx < x + w - 30:
        k = rng.integers(0, 3)
        if k == 0:
            lata(c, xx + 14, y - 22, 13, 40)
            xx += 34
        elif k == 1:
            frasco(c, xx + 16, y - 20, 16)
            xx += 38
        else:
            engranaje(c, xx + 18, y - 20, 18, rot=rng.uniform(0, 30))
            xx += 42
