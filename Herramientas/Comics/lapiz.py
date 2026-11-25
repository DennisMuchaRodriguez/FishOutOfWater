# -*- coding: utf-8 -*-
"""Lienzo de dibujo a lápiz para las viñetas del modo Historia (solo numpy y Pillow).

Tres modos con la MISMA escena (escenas.py):
  - 'palito'    : el guion gráfico, con monigotes de palitos a marcador (ideas rápidas, con flechas y notas)
  - 'caricatura': el dibujo terminado, en el estilo 2D del protagonista (contornos negros gruesos y limpios, colores
                  planos, sombras planas un poco más oscuras y brillos de bisel). Es el que usan las viñetas del juego.
  - 'lapiz'     : la versión a lápiz (grafito con grano de papel, líneas dobles, sombreado a rayas, aguada suave)

Las coordenadas son las de la viñeta final (1380 x 660, y hacia abajo). Se dibuja al doble y se reduce al final.
"""
import os
import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont

W, H = 1380, 660
SS = 2
REPO = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..'))
FUENTES = os.path.join(REPO, 'Assets', 'Resources', 'Fonts')

PAPEL = np.array([0.958, 0.940, 0.893], np.float32)
GRAFITO = np.array([0.20, 0.20, 0.235], np.float32)
MARCADOR = np.array([0.10, 0.10, 0.12], np.float32)
TINTA = np.array([0.05, 0.05, 0.07], np.float32)           # contornos de la caricatura


# ======================= GEOMETRÍA =======================

def spline(pts, cerrado=False, n=10):
    """Curva suave (Catmull-Rom) que pasa por los puntos."""
    P = np.asarray(pts, np.float64)
    if len(P) < 3:
        return P.copy()
    if cerrado:
        P = np.vstack([P[-1:], P, P[:2]])
    else:
        P = np.vstack([2 * P[:1] - P[1:2], P, 2 * P[-1:] - P[-2:-1]])
    out = []
    t = np.linspace(0, 1, n, endpoint=False)[:, None]
    t2, t3 = t * t, t * t * t
    for i in range(1, len(P) - 2):
        p0, p1, p2, p3 = P[i - 1], P[i], P[i + 1], P[i + 2]
        out.append(0.5 * ((2 * p1) + (-p0 + p2) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t2 + (-p0 + 3 * p1 - 3 * p2 + p3) * t3))
    out = np.vstack(out)
    if not cerrado:
        out = np.vstack([out, P[-2:-1]])
    return out


def elipse(cx, cy, rx, ry, rot=0.0, n=48, a0=0.0, a1=360.0):
    a = np.radians(np.linspace(a0, a1, n, endpoint=(a1 - a0) < 360))
    x, y = np.cos(a) * rx, np.sin(a) * ry
    r = np.radians(rot)
    return np.stack([cx + x * np.cos(r) - y * np.sin(r), cy + x * np.sin(r) + y * np.cos(r)], 1)


def banda(eje, g0, g1=None, n=8):
    """Tubo alrededor de una curva (cuellos, patas, chorros): polígono cerrado, grosor de g0 a g1."""
    E = spline(eje, n=n) if len(eje) > 2 else np.asarray(eje, np.float64)
    if g1 is None:
        g1 = g0
    d = np.gradient(E, axis=0)
    d /= np.linalg.norm(d, axis=1, keepdims=True) + 1e-9
    nrm = np.stack([-d[:, 1], d[:, 0]], 1)
    g = np.linspace(g0, g1, len(E))[:, None] * 0.5
    return np.vstack([E + nrm * g, (E - nrm * g)[::-1]])


def remuestrear(P, paso):
    seg = np.linalg.norm(np.diff(P, axis=0), axis=1)
    L = np.concatenate([[0], np.cumsum(seg)])
    if L[-1] < 1e-6:
        return P[:1]
    n = max(2, int(L[-1] / paso) + 1)
    s = np.linspace(0, L[-1], n)
    return np.stack([np.interp(s, L, P[:, 0]), np.interp(s, L, P[:, 1])], 1)


class T:
    """Transformación local -> viñeta: escala, giro (grados), espejo (mira a la izquierda) y posición."""

    def __init__(self, x, y, s, rot=0.0, flip=False):
        self.x, self.y, self.s, self.rot, self.flip = x, y, s, rot, flip
        r = np.radians(rot)
        self.c, self.sn = np.cos(r), np.sin(r)

    def __call__(self, pts):
        P = np.asarray(pts, np.float64).reshape(-1, 2).copy()
        if self.flip:
            P[:, 0] = -P[:, 0]
        x = P[:, 0] * self.c - P[:, 1] * self.sn
        y = P[:, 0] * self.sn + P[:, 1] * self.c
        return np.stack([self.x + x * self.s, self.y + y * self.s], 1)

    def p(self, u, v):
        return self([(u, v)])[0]

    def hijo(self, u, v, s=1.0, rot=0.0):
        """Transformación de una parte (ala, aleta, cuello) con su propio giro, dentro de esta."""
        q = self.p(u, v)
        giro = self.rot + (-rot if self.flip else rot)
        t = T(q[0], q[1], self.s * s, giro, self.flip)
        return t


# ======================= LIENZO =======================

class Mascara:
    def __init__(self, a, x0, y0):
        self.a, self.x0, self.y0 = a, x0, y0

    def caja(self):
        h, w = self.a.shape
        return self.x0, self.y0, self.x0 + w, self.y0 + h


def _union(m1, m2):
    x0, y0 = min(m1.x0, m2.x0), min(m1.y0, m2.y0)
    x1 = max(m1.caja()[2], m2.caja()[2])
    y1 = max(m1.caja()[3], m2.caja()[3])
    out = []
    for m in (m1, m2):
        a = np.zeros((y1 - y0, x1 - x0), np.float32)
        h, w = m.a.shape
        a[m.y0 - y0:m.y0 - y0 + h, m.x0 - x0:m.x0 - x0 + w] = m.a
        out.append(a)
    return out[0], out[1], x0, y0


def _ampliar(m, k):
    a = np.pad(m.a, k)
    return Mascara(a, m.x0 - k, m.y0 - k)


def mas(m1, m2):
    a, b, x0, y0 = _union(m1, m2)
    return Mascara(np.maximum(a, b), x0, y0)


def menos(m1, m2):
    a, b, x0, y0 = _union(m1, m2)
    return Mascara(np.clip(a - b, 0, 1), x0, y0)


def dentro(m1, m2):
    a, b, x0, y0 = _union(m1, m2)
    return Mascara(a * b, x0, y0)


class Lienzo:
    def __init__(self, modo='lapiz', seed=1, w=W, h=H, ss=SS):
        self.modo = modo
        self.ss = ss
        self.W, self.H = w, h
        self.w, self.h = w * ss, h * ss
        self.rng = np.random.default_rng(seed)
        self.g = np.zeros((self.h, self.w), np.float32)
        self.tinte = np.ones((self.h, self.w, 3), np.float32)
        self.fade = 1.0          # presión general (lejos, niebla): 1 = normal
        if modo == 'lapiz':
            fino = (self.rng.random((self.h, self.w)) * 255).astype(np.uint8)
            fino = np.asarray(Image.fromarray(fino).filter(ImageFilter.GaussianBlur(0.7)), np.float32) / 255
            fibra = self.ruido(70) * 0.5 + self.ruido(9) * 0.5
            self.grano = np.clip(0.45 + 0.75 * np.clip((fino - 0.5) * 2.6 + 0.5, 0, 1) * (0.8 + 0.25 * fibra), 0.3, 1.0)
        else:
            self.grano = np.ones((self.h, self.w), np.float32)

    @property
    def palito(self):
        return self.modo == 'palito'

    @property
    def plano(self):
        """Estilo caricatura: colores planos y contornos negros limpios."""
        return self.modo == 'caricatura'

    # ---------- ruido ----------
    def ruido(self, escala, shape=None, rng=None):
        h, w = shape if shape is not None else (self.h, self.w)
        rng = rng if rng is not None else self.rng
        sh, sw = max(2, int(h / escala) + 3), max(2, int(w / escala) + 3)
        small = (rng.random((sh, sw)) * 255).astype(np.uint8)
        img = Image.fromarray(small).resize((w, h), Image.BICUBIC)
        return np.asarray(img, np.float32) / 255

    # ---------- acumular grafito ----------
    def _acumular(self, arr, x0, y0, borrar=False):
        h, w = arr.shape
        X0, Y0 = max(0, x0), max(0, y0)
        X1, Y1 = min(self.w, x0 + w), min(self.h, y0 + h)
        if X1 <= X0 or Y1 <= Y0:
            return
        a = arr[Y0 - y0:Y1 - y0, X0 - x0:X1 - x0]
        g = self.g[Y0:Y1, X0:X1]
        if borrar:
            g *= (1 - a)
        else:
            a = a * self.grano[Y0:Y1, X0:X1] * self.fade
            g[...] = 1 - (1 - g) * (1 - a)

    # ---------- máscaras ----------
    def mascara(self, forma, suave=0.0, margen=0):
        """Máscara de una forma (recortada a su caja). Con suave > 0 la caja crece para que el difuminado no se
        corte en un rectángulo."""
        if isinstance(forma, Mascara):
            return _ampliar(forma, margen) if margen else forma
        P = np.asarray(forma, np.float64) * self.ss
        m = 4 + margen + int(3 * suave * self.ss)
        x0, y0 = int(np.floor(P[:, 0].min())) - m, int(np.floor(P[:, 1].min())) - m
        x1, y1 = int(np.ceil(P[:, 0].max())) + m, int(np.ceil(P[:, 1].max())) + m
        img = Image.new('L', (max(1, x1 - x0), max(1, y1 - y0)), 0)
        ImageDraw.Draw(img).polygon([tuple(p) for p in (P - [x0, y0])], fill=255)
        if suave > 0:
            img = img.filter(ImageFilter.GaussianBlur(suave * self.ss))
        return Mascara(np.asarray(img, np.float32) / 255, x0, y0)

    def circulo_m(self, cx, cy, r):
        return self.mascara(elipse(cx, cy, r, r, n=40))

    # ---------- trazos ----------
    def trazo(self, pts, peso=1.0, presion=0.85, temblor=1.0, pasadas=2, cerrado=False, punta=True, suave=True,
              borrar=False):
        """Línea a lápiz (o a marcador en 'palito'). pts en coordenadas de la viñeta."""
        P = np.asarray(pts, np.float64)
        if len(P) < 2:
            return
        if suave and len(P) > 2:
            P = spline(P, cerrado=cerrado, n=6)
        if cerrado:
            P = np.vstack([P, P[:1]])
        P = P * self.ss
        if self.palito:
            pasadas, temblor, presion = 1, 0.35, 1.0
            peso = max(peso, 0.8) * 1.25
        elif self.plano and not borrar:
            # contorno limpio: una sola pasada, sin temblor, más grueso y negro
            pasadas, temblor = 1, 0.12
            presion = min(1.0, presion * 1.35)
            peso = peso * 1.45
        P = remuestrear(P, 2.2 * self.ss)
        n = len(P)
        if n < 2:
            return
        d = np.gradient(P, axis=0)
        d /= np.linalg.norm(d, axis=1, keepdims=True) + 1e-9
        nrm = np.stack([-d[:, 1], d[:, 0]], 1)
        L = np.concatenate([[0], np.cumsum(np.linalg.norm(np.diff(P, axis=0), axis=1))])
        Ltot = max(L[-1], 1e-6)
        s = L / Ltot
        margen = int(peso * 6 * self.ss + temblor * 6 * self.ss + 6)
        x0, y0 = int(P[:, 0].min()) - margen, int(P[:, 1].min()) - margen
        x1, y1 = int(P[:, 0].max()) + margen, int(P[:, 1].max()) + margen
        base_w = peso * 2.1 * self.ss
        for k in range(pasadas):
            img = Image.new('L', (x1 - x0, y1 - y0), 0)
            dr = ImageDraw.Draw(img)
            amp = temblor * self.ss * (0.55 if k == 0 else 1.1)
            ph = self.rng.random(3) * 6.283
            ciclos = max(1.0, Ltot / (90 * self.ss))
            wob = (np.sin(s * ciclos * 6.283 + ph[0]) * 0.6 + np.sin(s * ciclos * 15.7 + ph[1]) * 0.4) * amp
            shift = self.rng.normal(0, 0.7 * self.ss * temblor, 2) if k > 0 else np.zeros(2)
            Q = P + nrm * wob[:, None] + shift - [x0, y0]
            if punta and not cerrado:
                f = np.clip(np.minimum(s, 1 - s) * Ltot / (18 * self.ss), 0.25, 1.0)
            else:
                f = np.ones(n)
            pr = presion * (1.0 if k == 0 else 0.5) * (np.ones(n) if self.plano else (0.82 + 0.18 * np.sin(s * 9 + ph[2])))
            trozo = 6
            for i in range(0, n - 1, trozo):
                j = min(n, i + trozo + 1)
                fi = float(f[i:j].mean())
                v = int(255 * np.clip(pr[i:j].mean() * (0.55 + 0.45 * fi), 0, 1))
                wdt = max(1, int(round(base_w * (0.6 + 0.4 * fi) * (1.0 if k == 0 else 0.8))))
                dr.line([tuple(q) for q in Q[i:j]], fill=v, width=wdt, joint='curve')
            arr = np.asarray(img, np.float32) / 255
            self._acumular(arr, x0, y0, borrar=borrar)

    def linea(self, p0, p1, **kw):
        self.trazo([p0, p1], suave=False, **kw)

    def contorno(self, pts, **kw):
        self.trazo(pts, cerrado=True, punta=False, **kw)

    # ---------- rellenos ----------
    def tapar(self, forma, suave=0.0):
        """Tapa lo de atrás (papel limpio): para que cada cosa quede delante de la anterior."""
        m = self.mascara(forma, suave)
        self._acumular(m.a, m.x0, m.y0, borrar=True)
        x0, y0, x1, y1 = m.caja()
        X0, Y0, X1, Y1 = max(0, x0), max(0, y0), min(self.w, x1), min(self.h, y1)
        if X1 > X0 and Y1 > Y0:
            a = m.a[Y0 - y0:Y1 - y0, X0 - x0:X1 - x0][..., None]
            t = self.tinte[Y0:Y1, X0:X1]
            t[...] = t * (1 - a) + a * 1.0
        return m

    def rayado(self, forma, angulo=45.0, espacio=6.0, presion=0.45, cruzado=False, peso=0.7, irregular=1.0):
        """Sombreado a rayas dentro de una forma (y cruzado para lo más oscuro). En caricatura: sombra plana."""
        if self.palito:
            return
        m = self.mascara(forma)
        if self.plano:
            k = np.clip(presion * (0.62 if cruzado else 0.48), 0, 0.5)
            img = Image.fromarray((m.a * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(0.5 * self.ss))
            a = (np.asarray(img, np.float32) / 255)[..., None] * k
            self._mezclar_tinte(a, np.zeros(3, np.float32), m.x0, m.y0, multiplicar=True)
            return
        h, w = m.a.shape
        if h < 2 or w < 2:
            return
        angs = [angulo] + ([angulo + 75] if cruzado else [])
        for ang in angs:
            img = Image.new('L', (w, h), 0)
            dr = ImageDraw.Draw(img)
            a = np.radians(ang)
            dx, dy = np.cos(a), np.sin(a)
            nx, ny = -dy, dx
            diag = np.hypot(w, h)
            cx, cy = w / 2, h / 2
            esp = espacio * self.ss
            o = -diag / 2
            while o < diag / 2:
                oo = o + self.rng.normal(0, esp * 0.18 * irregular)
                # rayas cortas, como a mano: tramos con hueco y presión distinta
                t = -diag / 2
                while t < diag / 2:
                    largo = self.rng.uniform(25, 70) * self.ss
                    jit = self.rng.normal(0, 0.03 * irregular)
                    ddx, ddy = np.cos(a + jit), np.sin(a + jit)
                    px, py = cx + nx * oo + dx * t, cy + ny * oo + dy * t
                    qx, qy = px + ddx * largo, py + ddy * largo
                    v = int(255 * np.clip(presion * self.rng.uniform(0.7, 1.1), 0, 1))
                    dr.line([(px, py), (qx, qy)], fill=v, width=max(1, int(round(peso * 1.6 * self.ss))))
                    t += largo + self.rng.uniform(0, 6) * self.ss
                o += esp
            arr = np.asarray(img, np.float32) / 255 * m.a
            self._acumular(arr, m.x0, m.y0)

    def tono(self, forma, valor=0.25, suave=2.0, mancha=0.35):
        """Grafito difuminado (frotado con el dedo) dentro de una forma."""
        if self.palito:
            return
        m = self.mascara(forma, margen=int(3 * suave * self.ss))
        h, w = m.a.shape
        if h < 2 or w < 2:
            return
        img = Image.fromarray((m.a * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(suave * self.ss))
        a = np.asarray(img, np.float32) / 255
        if self.plano:
            # oscurece el color de forma plana (pupilas negras, sombras, interiores)
            k = np.clip(a * valor * (1.0 if valor > 0.6 else 0.9), 0, 1)[..., None]
            self._mezclar_tinte(k, np.zeros(3, np.float32), m.x0, m.y0, multiplicar=True)
            return
        nz = self.ruido(40 * self.ss, (h, w))
        self._acumular(a * valor * (1 - mancha + mancha * nz), m.x0, m.y0)

    def aguada(self, forma, rgb, fuerza=0.5, suave=1.5, borde=0.25):
        """Acuarela muy suave: tiñe el papel (no tapa el grafito)."""
        if self.palito:
            return
        m = self.mascara(forma, margen=int(3 * max(suave, 5) * self.ss))
        h, w = m.a.shape
        if h < 2 or w < 2:
            return
        if self.plano:
            # color plano (reemplaza el de abajo), con el borde apenas suavizado
            img = Image.fromarray((m.a * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(0.4 * self.ss))
            k = np.clip(np.asarray(img, np.float32) / 255 * min(1.0, fuerza * 1.3), 0, 1)[..., None]
            self._mezclar_tinte(k, np.asarray(rgb, np.float32), m.x0, m.y0, multiplicar=False)
            return
        img = Image.fromarray((m.a * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(suave * self.ss))
        a = np.asarray(img, np.float32) / 255
        # borde un poco más cargado (como se seca la acuarela)
        bl = np.asarray(Image.fromarray((a * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(5 * self.ss)), np.float32) / 255
        ed = np.clip(a - bl, 0, 1) * 2.0
        nz = self.ruido(60 * self.ss, (h, w))
        k = np.clip(a * fuerza * (0.82 + 0.3 * nz) * (1 + borde * ed), 0, 1)[..., None]
        self._mezclar_tinte(k, np.asarray(rgb, np.float32), m.x0, m.y0, multiplicar=True)

    def aclarar(self, forma, fuerza=0.5, suave=6.0):
        """Luz (rayos, brillos): devuelve el papel hacia blanco."""
        if self.palito:
            return
        m = self.mascara(forma, suave)
        self._mezclar_tinte(m.a[..., None] * fuerza, np.ones(3, np.float32), m.x0, m.y0, multiplicar=False)
        self._acumular(m.a * fuerza * 0.8, m.x0, m.y0, borrar=True)

    def _mezclar_tinte(self, k, rgb, x0, y0, multiplicar):
        h, w = k.shape[:2]
        X0, Y0 = max(0, x0), max(0, y0)
        X1, Y1 = min(self.w, x0 + w), min(self.h, y0 + h)
        if X1 <= X0 or Y1 <= Y0:
            return
        kk = k[Y0 - y0:Y1 - y0, X0 - x0:X1 - x0]
        t = self.tinte[Y0:Y1, X0:X1]
        if multiplicar:
            t[...] = t * (1 - kk + kk * rgb)
        else:
            t[...] = t * (1 - kk) + kk * rgb

    def degradado(self, y0, y1, rgb0, rgb1, x0=0, x1=None):
        """Aguada de fondo (cielo, agua) en franja horizontal, de rgb0 arriba a rgb1 abajo."""
        if self.palito:
            return
        x1 = self.W if x1 is None else x1
        Y0, Y1 = int(y0 * self.ss), int(y1 * self.ss)
        X0, X1 = int(x0 * self.ss), int(x1 * self.ss)
        Y0, Y1 = max(0, Y0), min(self.h, Y1)
        if Y1 <= Y0:
            return
        t = np.linspace(0, 1, Y1 - Y0)[:, None, None]
        col = np.asarray(rgb0, np.float32) * (1 - t) + np.asarray(rgb1, np.float32) * t
        if not self.plano:
            nz = self.ruido(80 * self.ss, (Y1 - Y0, X1 - X0))[..., None]
            col = 1 - (1 - col) * (0.85 + 0.3 * nz)
        self.tinte[Y0:Y1, X0:X1] *= col

    # ---------- texto (onomatopeyas y notas) ----------
    def texto(self, s, x, y, alto=40, fuente='LilitaOne-Regular.ttf', angulo=0.0, relleno=None, contorno=True,
              centro=True):
        f = ImageFont.truetype(os.path.join(FUENTES, fuente), int(alto * self.ss))
        bb = f.getbbox(s)
        tw, th = bb[2] - bb[0] + 20 * self.ss, bb[3] - bb[1] + 20 * self.ss
        img = Image.new('L', (int(tw), int(th)), 0)
        ImageDraw.Draw(img).text((10 * self.ss - bb[0], 10 * self.ss - bb[1]), s, font=f, fill=255)
        if angulo:
            img = img.rotate(angulo, resample=Image.BICUBIC, expand=True)
        a = np.asarray(img, np.float32) / 255
        h, w = a.shape
        x0 = int(x * self.ss - (w / 2 if centro else 0))
        y0 = int(y * self.ss - h / 2)
        if contorno:
            gr = np.asarray(img.filter(ImageFilter.MaxFilter(int(5 * self.ss) | 1)), np.float32) / 255
            self._acumular(gr, x0, y0, borrar=True)
            borde = np.clip(gr - a, 0, 1)
            self._acumular(borde * (1.0 if self.palito else 0.9), x0, y0)
            if relleno is not None and not self.palito:
                self._mezclar_tinte(a[..., None] * 0.85, np.asarray(relleno, np.float32), x0, y0, multiplicar=True)
            elif not self.palito:
                self._acumular(a * 0.12, x0, y0)
        else:
            self._acumular(a, x0, y0)

    # ---------- resultado ----------
    def imagen(self):
        if self.palito:
            base = np.ones((self.h, self.w, 3), np.float32)
            g = np.clip(self.g * 1.4, 0, 1)[..., None]
            out = base * (1 - g) + MARCADOR * g
        elif self.plano:
            base = np.array([0.99, 0.985, 0.97], np.float32) * self.tinte
            g = np.clip(self.g * 1.15, 0, 1)[..., None]
            out = base * (1 - g) + TINTA * g
        else:
            yy, xx = np.mgrid[0:self.h, 0:self.w].astype(np.float32)
            r = np.hypot((xx / self.w - 0.5) * 1.1, (yy / self.h - 0.5) * 1.3)
            vin = 1 - np.clip(r - 0.45, 0, 1) * 0.22
            papel = PAPEL * (0.97 + 0.03 * self.ruido(120)[..., None]) * vin[..., None]
            base = papel * self.tinte
            g = np.clip(self.g, 0, 1)[..., None]
            out = base * (1 - g * 0.92) + GRAFITO * g * 0.92
        img = Image.fromarray((np.clip(out, 0, 1) * 255 + 0.5).astype(np.uint8), 'RGB')
        return img.resize((self.W, self.H), Image.LANCZOS)
