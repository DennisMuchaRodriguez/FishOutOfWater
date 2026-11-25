# -*- coding: utf-8 -*-
"""Logo de Fish Out Of Water (solo numpy y Pillow).

El protagonista igual a su dibujo 2D de referencia (el mismo estilo de las viñetas: contornos negros gruesos y colores
planos): la cabeza en cúpula naranja con los ojos enojados, el borde del casco en dos tonos y los auriculares, el
cuello en segmentos, la armadura azul marino en placas con la luz naranja del pecho, las hombreras y las aletas azules.
Sale volando de una ola
(el fuego del propulsor abajo). A su lado el título FISH OUT (naranja) / OF WATER (azul) con la fuente y los colores
del menú (Oxanium, naranja, azul y azul marino) y el brillo azul eléctrico de la interfaz holográfica.
Usa las mismas formas que los cómics (Herramientas/Comics/personajes.py, las constantes H_*).

    python3 Herramientas/Logo/logo.py

Escribe:
  - Assets/Resources/UI/Logo.png    el logo completo (el menú principal lo usa como título)
  - Assets/Resources/UI/Icono.png   ícono cuadrado 1024 (el protagonista de frente sobre azul marino): ícono de la app
  - Itch/logo.png                   el logo para la página de itch.io
"""
import os, sys, uuid
import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont

AQUI = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(AQUI, '..', '..'))
sys.path.insert(0, os.path.join(REPO, 'Herramientas', 'Comics'))
from lapiz import spline, elipse, banda, T  # noqa: E402
from personajes import (H_CABEZA, H_CABEZA_PTS, H_CASCO, H_CASCO_LINEA, H_OREJERA, H_ARMADURA, H_CUELLO,  # noqa: E402
                        H_CUELLO_SEP, H_HOMBRERA, H_HOMBRERA_TAPA, H_ALETA, H_ALETA_TAPA, H_ALETA_NERVIOS, H_COLA,
                        H_COLA_NERVIOS, H_COLA_INT, H_LUZ_MARCO, H_LUZ, H_PLACAS, H_BISELES, H_RECTANGULO)

FUENTE = os.path.join(REPO, 'Assets', 'Resources', 'Fonts', 'Oxanium-ExtraBold.ttf')
SS = 2                                    # se dibuja al doble y se reduce (bordes suaves)

# Colores de la interfaz del juego (MenuUI): azul marino del casco, naranja de acción y azul holograma
MARINO = (10, 18, 40)
MARINO_HONDO = (4, 9, 24)
GLOW_AZUL = (64, 156, 255)
NARANJA_ARRIBA, NARANJA_ABAJO = (255, 190, 96), (255, 106, 36)
AZUL_ARRIBA, AZUL_ABAJO = (160, 228, 255), (52, 140, 255)
AGUA_ARRIBA, AGUA_ABAJO = (120, 214, 255), (26, 108, 220)
# Colores del protagonista (del dibujo 2D de referencia): contornos negros, colores planos
TINTA = (14, 14, 20)
NAVY = ((44, 64, 122), (30, 44, 92))
NAVY_CLARO = ((60, 90, 164), (50, 76, 142))
NAVY_OSC = ((30, 44, 92), (24, 34, 74))
CARA = ((210, 94, 60), (198, 84, 52))
OJO = (248, 234, 202)
ALETA = ((50, 102, 214), (38, 82, 186))
ALETA_OSC = ((34, 70, 160), (28, 58, 136))
LUZ_MARCO = (206, 52, 32)
LUZ = ((255, 132, 76), (255, 96, 52))


# ======================= AYUDANTES =======================

class Lienzo:
    def __init__(self, w, h):
        self.w, self.h = w * SS, h * SS
        self.img = Image.new('RGBA', (self.w, self.h), (0, 0, 0, 0))

    def mascara(self, pts):
        m = Image.new('L', (self.w, self.h), 0)
        ImageDraw.Draw(m).polygon([(float(x) * SS, float(y) * SS) for x, y in pts], fill=255)
        return m

    def pintar(self, m, arriba, abajo=None, alpha=255, y0=None, y1=None):
        """Pinta la máscara con un color o un degradado vertical (entre y0 e y1, o la caja de la máscara)."""
        bb = m.getbbox()
        if bb is None:
            return
        abajo = arriba if abajo is None else abajo
        a = np.asarray(m, np.float32) / 255 * (alpha / 255)
        top = (y0 * SS) if y0 is not None else bb[1]
        bot = (y1 * SS) if y1 is not None else bb[3]
        ys = np.clip((np.arange(self.h, dtype=np.float32) - top) / max(1.0, bot - top), 0, 1)[:, None]
        col = np.zeros((self.h, self.w, 4), np.float32)
        for k in range(3):
            col[..., k] = arriba[k] * (1 - ys) + abajo[k] * ys
        col[..., 3] = a * 255
        self.img = Image.alpha_composite(self.img, Image.fromarray(col.astype(np.uint8), 'RGBA'))

    def forma(self, pts, arriba, abajo=None, borde=MARINO, grosor=7, alpha=255, suave=True):
        P = spline(pts, cerrado=True, n=10) if (len(pts) > 3 and suave) else np.asarray(pts, float)
        m = self.mascara(P)
        self.pintar(m, arriba, abajo, alpha)
        if grosor > 0:
            self.linea(P, borde, grosor, cerrada=True)
        return m

    def linea(self, pts, color, grosor, cerrada=False, alpha=255):
        d = ImageDraw.Draw(self.img)
        P = [(float(x) * SS, float(y) * SS) for x, y in pts]
        if cerrada:
            P = P + [P[0]]
        d.line(P, fill=color + (alpha,), width=max(1, int(grosor * SS)), joint='curve')
        r = grosor * SS / 2
        for (x, y) in (P[0], P[-1]):
            d.ellipse([x - r, y - r, x + r, y + r], fill=color + (alpha,))

    def circulo(self, cx, cy, r, arriba, abajo=None, borde=MARINO, grosor=7, alpha=255, ry=None):
        return self.forma(elipse(cx, cy, r, ry if ry is not None else r, n=64), arriba, abajo, borde, grosor, alpha)

    def brillo(self, m, color, radio, fuerza=1.0):
        """Brillo suave (luz) con la forma de la máscara."""
        g = m.filter(ImageFilter.GaussianBlur(radio * SS))
        capa = Image.new('RGBA', self.img.size, color + (0,))
        capa.putalpha(g.point(lambda v: int(min(255, v * fuerza))))
        self.img = Image.alpha_composite(self.img, capa)

    def encima(self, otro, glow=None, radio=10, ancho=6):
        """Pone otro lienzo encima; con glow, primero un borde de luz alrededor de su silueta."""
        if glow is not None:
            a = otro.img.split()[3]
            k = int(ancho * SS) * 2 + 1
            gr = a.filter(ImageFilter.MaxFilter(k)).filter(ImageFilter.GaussianBlur(radio * SS))
            capa = Image.new('RGBA', self.img.size, glow + (0,))
            capa.putalpha(gr)
            self.img = Image.alpha_composite(self.img, capa)
        self.img = Image.alpha_composite(self.img, otro.img)

    def resultado(self, glow=0):
        img = self.img
        if glow > 0:
            # Brillo azul eléctrico alrededor de todo (como la interfaz holográfica del juego)
            a = img.split()[3]
            k = int(glow * SS) * 2 + 1
            gr = a.filter(ImageFilter.MaxFilter(k)).filter(ImageFilter.GaussianBlur(glow * SS * 1.2))
            base = Image.new('RGBA', img.size, GLOW_AZUL + (0,))
            base.putalpha(gr.point(lambda v: int(v * 0.85)))
            borde = a.filter(ImageFilter.MaxFilter(int(glow * 0.35 * SS) * 2 + 1)).filter(ImageFilter.GaussianBlur(SS))
            fino = Image.new('RGBA', img.size, (190, 228, 255, 0))
            fino.putalpha(borde)
            img = Image.alpha_composite(Image.alpha_composite(base, fino), img)
        return img.resize((self.w // SS, self.h // SS), Image.LANCZOS)


# ======================= LA OLA =======================

def ola(c, x, y, s):
    """Ola que se levanta y rompe (x, y = base al centro; s = ancho)."""
    cuerpo = [(x - s * 0.55, y), (x - s * 0.42, y - s * 0.16), (x - s * 0.20, y - s * 0.30), (x + s * 0.02, y - s * 0.42),
              (x + s * 0.20, y - s * 0.40), (x + s * 0.30, y - s * 0.30), (x + s * 0.22, y - s * 0.24),
              (x + s * 0.12, y - s * 0.26), (x + s * 0.18, y - s * 0.14), (x + s * 0.40, y - s * 0.06),
              (x + s * 0.55, y)]
    c.forma(cuerpo, AGUA_ARRIBA, AGUA_ABAJO, grosor=7)
    espuma = [(x - s * 0.18, y - s * 0.29), (x + s * 0.02, y - s * 0.41), (x + s * 0.20, y - s * 0.39),
              (x + s * 0.29, y - s * 0.30), (x + s * 0.20, y - s * 0.33), (x + s * 0.05, y - s * 0.33),
              (x - s * 0.08, y - s * 0.27)]
    c.forma(espuma, (255, 255, 255), (214, 244, 255), grosor=0)
    for f in (0.25, 0.5):
        P = spline([(x - s * (0.45 - f * 0.2), y - s * 0.05), (x - s * (0.2 - f * 0.3), y - s * (0.16 + f * 0.05)),
                    (x + s * (f * 0.15), y - s * (0.2 + f * 0.06))], n=8)
        c.linea(P, (210, 242, 255), 4)
    base = [(x - s * 0.62, y - s * 0.02), (x + s * 0.62, y - s * 0.02), (x + s * 0.56, y + s * 0.07),
            (x - s * 0.56, y + s * 0.07)]
    c.forma(base, AGUA_ABAJO, (18, 70, 170), grosor=7)


def gotas(c, lista):
    for (x, y, r) in lista:
        P = [(x, y - r * 1.9), (x + r, y), (x + r * 0.7, y + r * 0.75), (x, y + r), (x - r * 0.7, y + r * 0.75),
             (x - r, y)]
        c.forma(P, (200, 240, 255), AGUA_ABAJO, grosor=4)


# ======================= EL PROTAGONISTA (como el modelo) =======================

def _esp(pts, m):
    return [(m * x, y) for x, y in pts]


def heroe(c, x, y, s, rot=0.0, fuego=True):
    """El protagonista de frente, igual a su dibujo 2D de referencia (x, y = centro; s = alto)."""
    t = T(x, y, s, rot)
    g = 0.0105 * s                       # contorno negro grueso
    gi = g * 0.55                        # líneas de adentro
    hx, hy, hr, hry = H_CABEZA
    if fuego:
        tf = t.hijo(0.0, 0.36, 1.0, 90)
        llama = [(0.0, -0.10), (0.25, -0.14), (0.5, -0.07), (0.72, 0.0), (0.5, 0.07), (0.25, 0.14), (0.0, 0.10)]
        c.brillo(c.mascara(tf(llama)), (255, 150, 60), 16, 1.3)
        c.forma(tf(llama), (255, 222, 110), (255, 120, 36), borde=TINTA, grosor=g)
        c.forma(tf([(0.0, -0.06), (0.2, -0.07), (0.42, 0.0), (0.2, 0.07), (0.0, 0.06)]), (255, 250, 214),
                (255, 214, 110), grosor=0)
    # aletas de abajo (las de atrás más oscuras) y de los costados
    for m in (-1, 1):
        c.forma(t(_esp(H_COLA_INT, m)), *ALETA_OSC, borde=TINTA, grosor=g)
    for m in (-1, 1):
        c.forma(t(_esp(H_COLA, m)), *ALETA, borde=TINTA, grosor=g)
        for (a0, a1) in H_COLA_NERVIOS:
            c.linea(t([(m * a0[0], a0[1]), (m * a1[0], a1[1])]), TINTA, gi)
    for m in (-1, 1):
        tf = T(*t.p(m * 0.276, -0.03), s * 1.15, t.rot, m < 0)
        c.forma(tf(H_ALETA), *ALETA, borde=TINTA, grosor=g)
        for (a0, a1) in H_ALETA_NERVIOS:
            c.linea(tf([a0, a1]), TINTA, gi)
        c.forma(tf(H_ALETA_TAPA), *NAVY, borde=TINTA, grosor=g, suave=False)
    # armadura con biseles, placas y rectangulitos
    c.forma(t(H_ARMADURA), *NAVY, borde=TINTA, grosor=g * 1.1)
    for linea in H_BISELES:
        for m in (-1, 1):
            c.linea(t([(m * px, py) for px, py in linea]), NAVY_CLARO[0], g * 1.1)
    for linea in H_PLACAS:
        for m in (-1, 1):
            c.linea(t([(m * px, py) for px, py in linea]), TINTA, gi)
    for m in (-1, 1):
        c.forma(t(_esp(H_RECTANGULO, m)), *NAVY_OSC, borde=TINTA, grosor=gi, suave=False)
    # la luz del pecho
    q = t.p(0.0, -0.003)
    c.brillo(c.mascara(elipse(q[0], q[1], 0.15 * s, 0.085 * s, n=40)), (255, 90, 40), 12, 1.1)
    c.forma(t(H_LUZ_MARCO), LUZ_MARCO, borde=TINTA, grosor=g * 0.9, suave=False)
    c.forma(t(H_LUZ), *LUZ, borde=TINTA, grosor=gi, suave=False)
    c.brillo(c.mascara(elipse(q[0], q[1], 0.05 * s, 0.016 * s, n=30)), (255, 210, 160), 3, 1.4)
    # hombreras
    for m in (-1, 1):
        c.forma(t(_esp(H_HOMBRERA, m)), *NAVY, borde=TINTA, grosor=g)
        c.forma(t(_esp(H_HOMBRERA_TAPA, m)), *NAVY_CLARO, borde=TINTA, grosor=gi)
    # la cabeza naranja, los ojos (crema, pupila negra, párpado negro y ceño) y la boca fruncida
    c.forma(t(H_CABEZA_PTS), *CARA, borde=TINTA, grosor=g)
    R = hr
    for lado in (-1, 1):
        qq = t.p(hx + lado * 0.475 * R, hy - 0.14 * R)
        rx, ry = 0.29 * R * s, 0.25 * R * s
        c.circulo(qq[0], qq[1], rx, OJO, borde=TINTA, grosor=gi * 1.2, ry=ry)
        hacia = -lado
        pr = ry * 0.64
        px, py = qq[0] + hacia * rx * 0.18, qq[1] + ry * 0.05
        c.circulo(px, py, pr, (10, 10, 14), grosor=0)
        c.circulo(px + pr * 0.38, py - pr * 0.38, pr * 0.26, (255, 255, 255), grosor=0)
        a = (qq[0] + hacia * rx * 1.12, qq[1] - ry * 0.02)
        b = (qq[0] - hacia * rx * 1.12, qq[1] - ry * 0.80)
        tapa = c.mascara([a, b, (b[0], qq[1] - ry * 1.5), (a[0], qq[1] - ry * 1.5)])
        ojo = c.mascara(elipse(qq[0], qq[1], rx * 1.07, ry * 1.07, n=40))
        mm = Image.fromarray((np.asarray(tapa, np.float32) * np.asarray(ojo, np.float32) / 255).astype(np.uint8))
        c.pintar(mm, CARA[0])
        c.linea([a, b], TINTA, g * 1.25)
        c.linea([(a[0] + hacia * rx * 0.1, a[1] - ry * 0.45), (b[0] - hacia * rx * 0.1, b[1] - ry * 0.4)], TINTA,
                gi * 0.8)
    mq = t.p(hx - 0.02 * R, hy + 0.36 * R)
    w = 0.36 * R * s
    c.linea(spline([(mq[0] - w, mq[1] + w * 0.16), (mq[0] - w * 0.1, mq[1] - w * 0.14), (mq[0] + w, mq[1] + w * 0.16)],
                   n=10), TINTA, g)
    # el cuello en segmentos
    c.forma(t(H_CUELLO), *NAVY, borde=TINTA, grosor=g)
    for sep in H_CUELLO_SEP:
        for m in (-1, 1):
            c.linea(t([(m * px, py) for px, py in sep]), TINTA, gi)
    # el borde del casco (dos tonos) y los auriculares de tres láminas
    c.forma(t(H_CASCO), *NAVY_CLARO, borde=TINTA, grosor=g, suave=False)
    c.linea(spline(t(H_CASCO_LINEA), n=8), TINTA, gi)
    ox, oy, ow, oh = H_OREJERA

    def caja(x0, x1, y0, y1, r=0.015):
        return [(x0 + r, y0), (x1 - r, y0), (x1, y0 + r), (x1, y1 - r), (x1 - r, y1), (x0 + r, y1), (x0, y1 - r),
                (x0, y0 + r)]
    for m in (-1, 1):
        cx = hx + m * ox
        c.forma(t(caja(cx - ow / 2, cx + ow / 2, oy - oh / 2, oy + oh / 2)), *NAVY_CLARO, borde=TINTA, grosor=g,
                suave=False)
        afuera = cx + m * ow / 2
        x0, x1 = sorted((afuera, afuera - m * ow * 0.28))
        c.forma(t(caja(x0, x1, oy - oh * 0.42, oy + oh * 0.42, 0.01)), *NAVY, borde=TINTA, grosor=gi, suave=False)
        c.linea(t([(cx - m * ow * 0.12, oy - oh * 0.45), (cx - m * ow * 0.12, oy + oh * 0.45)]), TINTA, gi)
    return t


def emblema(c, x, y, s):
    """La ola, el protagonista saliendo del agua (con su borde de luz azul) y las gotas (x, y = base de la ola)."""
    ola(c, x, y, s * 1.25)
    h = Lienzo(c.w // SS, c.h // SS)
    heroe(h, x + s * 0.02, y - s * 0.78, s * 1.08, rot=8)
    c.encima(h, glow=GLOW_AZUL, radio=9, ancho=5)
    gotas(c, [(x - s * 0.62, y - s * 0.50, s * 0.035), (x - s * 0.46, y - s * 0.78, s * 0.026),
              (x + s * 0.66, y - s * 0.56, s * 0.032), (x + s * 0.78, y - s * 0.32, s * 0.022),
              (x - s * 0.78, y - s * 0.26, s * 0.024)])


# ======================= EL TÍTULO =======================

def titulo(c, x, y, texto, alto, arriba, abajo, relieve=14):
    """Una línea del título: relieve azul marino, contorno y relleno en degradado (x, y = arriba a la izquierda)."""
    f = ImageFont.truetype(FUENTE, int(alto * SS))
    d = ImageDraw.Draw(c.img)
    X, Y = x * SS, y * SS
    contorno = int(alto * 0.075 * SS)
    for k in range(int(relieve * SS), 0, -SS):
        d.text((X + k * 0.25, Y + k), texto, font=f, fill=MARINO_HONDO + (255,), stroke_width=contorno,
               stroke_fill=MARINO_HONDO + (255,))
    d.text((X, Y), texto, font=f, fill=MARINO + (255,), stroke_width=contorno, stroke_fill=MARINO + (255,))
    m = Image.new('L', c.img.size, 0)
    ImageDraw.Draw(m).text((X, Y), texto, font=f, fill=255)
    bb = m.getbbox()
    c.pintar(m, arriba, abajo, y0=bb[1] / SS, y1=bb[3] / SS)
    franja = Image.new('L', c.img.size, 0)
    ImageDraw.Draw(franja).rectangle([0, bb[1], c.img.size[0], bb[1] + (bb[3] - bb[1]) * 0.28], fill=110)
    brillo = Image.fromarray((np.asarray(m, np.float32) * np.asarray(franja, np.float32) / 255).astype(np.uint8))
    c.pintar(brillo, (255, 255, 255))
    return bb[2] / SS, bb[3] / SS


# ======================= SALIDAS =======================

def meta_sprite(path):
    meta = path + '.meta'
    if os.path.exists(meta):
        return
    base = open(os.path.join(REPO, 'Assets', 'Art', 'Comics', 'origen_1.jpg.meta'), encoding='utf-8').read()
    lineas = base.split('\n')
    lineas[1] = 'guid: ' + uuid.uuid4().hex
    texto = '\n'.join(lineas).replace('alphaIsTransparency: 0', 'alphaIsTransparency: 1')
    with open(meta, 'w', encoding='utf-8', newline='\n') as fh:
        fh.write(texto)


def carpeta_meta(folder):
    meta = folder + '.meta'
    if not os.path.exists(meta):
        with open(meta, 'w', encoding='utf-8', newline='\n') as fh:
            fh.write("fileFormatVersion: 2\nguid: %s\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n"
                     "  userData: \n  assetBundleName: \n  assetBundleVariant: \n" % uuid.uuid4().hex)


def logo_completo():
    alto = 270
    f = ImageFont.truetype(FUENTE, alto)
    ancho = max(f.getbbox('FISH OUT')[2], f.getbbox('OF WATER')[2] + 60)
    x_texto = 1160
    c = Lienzo(x_texto + ancho + 120, 1020)
    emblema(c, 620, 930, 560)
    x1, y1 = titulo(c, x_texto, 170, 'FISH OUT', alto, NARANJA_ARRIBA, NARANJA_ABAJO)
    titulo(c, x_texto + 60, y1 + 36, 'OF WATER', alto, AZUL_ARRIBA, AZUL_ABAJO)
    img = c.resultado(glow=12)
    bb = img.getbbox()
    m = 24
    return img.crop((max(0, bb[0] - m), max(0, bb[1] - m), min(img.width, bb[2] + m), min(img.height, bb[3] + m)))


def icono():
    """El protagonista de frente sobre azul marino, como su dibujo 2D de referencia."""
    c = Lienzo(1024, 1024)
    fondo = [(90, 0), (934, 0), (1024, 90), (1024, 934), (934, 1024), (90, 1024), (0, 934), (0, 90)]
    c.forma(fondo, (20, 30, 58), (14, 22, 44), grosor=0, suave=False)
    heroe(c, 512, 522, 900, fuego=False)
    return c.resultado(glow=0)


def main():
    ui = os.path.join(REPO, 'Assets', 'Resources', 'UI')
    os.makedirs(ui, exist_ok=True)
    carpeta_meta(ui)
    logo = logo_completo()
    ruta = os.path.join(ui, 'Logo.png')
    logo.save(ruta, optimize=True)
    meta_sprite(ruta)
    print('LOGO', ruta, logo.size)
    ic = icono()
    ruta = os.path.join(ui, 'Icono.png')
    ic.save(ruta, optimize=True)
    meta_sprite(ruta)
    print('ICONO', ruta, ic.size)
    itch = os.path.join(REPO, 'Itch', 'logo.png')
    logo.save(itch, optimize=True)
    print('ITCH', itch)


if __name__ == '__main__':
    main()
