# -*- coding: utf-8 -*-
"""Logo de Fish Out Of Water (numpy y Pillow) con el protagonista en 3D.

El protagonista es un render del modelo del juego (Assets/Models/Animations/PESCAO.fbx con sus texturas), hecho con
render_pez_3d.py: sale volando de una salpicadura de agua con el fuego del propulsor encendido. Aquí se le pone el
resplandor de lo que brilla (la luz del pecho y el fuego) y un halo azul eléctrico detrás, y a su lado el título
FISH OUT (naranja) / OF WATER (azul) con la fuente y los colores del menú (Oxanium, naranja, azul y azul marino) y el
brillo azul de la interfaz holográfica.

    python Herramientas/Logo/render_pez_3d.py      (solo si cambia el modelo o la pose: necesita bpy)
    python3 Herramientas/Logo/logo.py              (desde cualquier carpeta)

Lee Herramientas/Logo/renders/pez_logo.png, pez_icono.png y sus *_brillo.png. Escribe:
  - Assets/Resources/UI/Logo.png    el logo completo (el menú principal lo usa como título)
  - Assets/Resources/UI/Icono.png   ícono cuadrado 1024 (el protagonista 3D de frente sobre azul marino): ícono de la app
  - Itch/logo.png                   el logo para la página de itch.io
"""
import os
import uuid

import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont

AQUI = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(AQUI, '..', '..'))
RENDERS = os.path.join(AQUI, 'renders')
FUENTE = os.path.join(REPO, 'Assets', 'Resources', 'Fonts', 'Oxanium-ExtraBold.ttf')
SS = 2                                    # se dibuja al doble y se reduce (bordes suaves)

# Colores de la interfaz del juego (MenuUI): azul marino del casco, naranja de acción y azul holograma
MARINO = (10, 18, 40)
MARINO_HONDO = (4, 9, 24)
GLOW_AZUL = (64, 156, 255)
NARANJA_ARRIBA, NARANJA_ABAJO = (255, 190, 96), (255, 106, 36)
AZUL_ARRIBA, AZUL_ABAJO = (160, 228, 255), (52, 140, 255)
FONDO_ICONO = ((22, 34, 66), (10, 16, 36))


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

    def halo(self, m, color, radio, fuerza=1.0, ancho=0):
        """Luz suave con la forma de la máscara (ancho > 0 la engorda antes de difuminarla)."""
        if ancho > 0:
            m = m.filter(ImageFilter.MaxFilter(int(ancho * SS) * 2 + 1))
        g = m.filter(ImageFilter.GaussianBlur(radio * SS))
        capa = Image.new('RGBA', self.img.size, color + (0,))
        capa.putalpha(g.point(lambda v: int(min(255, v * fuerza))))
        self.img = Image.alpha_composite(self.img, capa)

    def pegar(self, img, x, y):
        """Pega una imagen RGBA (ya a la escala del lienzo final) con su esquina de arriba a la izquierda en x, y."""
        img = img.resize((img.width * SS, img.height * SS), Image.LANCZOS)
        capa = Image.new('RGBA', self.img.size, (0, 0, 0, 0))
        capa.paste(img, (int(x * SS), int(y * SS)))
        self.img = Image.alpha_composite(self.img, capa)
        return capa.split()[3]

    def resultado(self, glow=0, solo=None):
        """Con glow: el brillo azul eléctrico de la interfaz holográfica alrededor de 'solo' (una máscara) o de todo."""
        img = self.img
        if glow > 0:
            a = solo if solo is not None else img.split()[3]
            k = int(glow * SS) * 2 + 1
            gr = a.filter(ImageFilter.MaxFilter(k)).filter(ImageFilter.GaussianBlur(glow * SS * 1.2))
            base = Image.new('RGBA', img.size, GLOW_AZUL + (0,))
            base.putalpha(gr.point(lambda v: int(v * 0.85)))
            borde = a.filter(ImageFilter.MaxFilter(int(glow * 0.35 * SS) * 2 + 1)).filter(ImageFilter.GaussianBlur(SS))
            fino = Image.new('RGBA', img.size, (190, 228, 255, 0))
            fino.putalpha(borde)
            img = Image.alpha_composite(Image.alpha_composite(base, fino), img)
        return img.resize((self.w // SS, self.h // SS), Image.LANCZOS)


# ======================= EL PROTAGONISTA 3D =======================

def _desenfocar(rgb, radio):
    return np.asarray(Image.fromarray((np.clip(rgb, 0, 1) * 255).astype(np.uint8)).filter(
        ImageFilter.GaussianBlur(radio)), np.float32) / 255


def pez_3d(nombre, resplandor=1.0):
    """El render del pez con el resplandor de lo que brilla (la luz del pecho y el fuego), recortado a su contenido."""
    img = Image.open(os.path.join(RENDERS, nombre + '.png')).convert('RGBA')
    v = np.asarray(img, np.float32) / 255
    a = v[..., 3:4]
    p = v[..., :3] * a                                            # color premultiplicado
    ruta_brillo = os.path.join(RENDERS, nombre + '_brillo.png')
    if os.path.exists(ruta_brillo):
        b = np.asarray(Image.open(ruta_brillo).convert('RGB'), np.float32) / 255
        r = img.width / 140.0
        luz = (_desenfocar(b, r) * 0.9 + _desenfocar(b, r * 3.5) * 0.8) * resplandor
        p = 1 - (1 - p) * (1 - luz)
        a = 1 - (1 - a) * (1 - luz.max(axis=2, keepdims=True))
    rgb = np.where(a > 1e-4, p / np.maximum(a, 1e-4), 0)
    out = Image.fromarray((np.clip(np.concatenate([rgb, a], axis=2), 0, 1) * 255).astype(np.uint8), 'RGBA')
    bb = out.split()[3].point(lambda x: 255 if x > 6 else 0).getbbox()
    return out.crop(bb)


def a_alto(img, alto):
    return img.resize((max(1, round(img.width * alto / img.height)), alto), Image.LANCZOS)


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
    pez = a_alto(pez_3d('pez_logo'), 870)
    x_pez, y_pez = 40, 30
    x_texto = x_pez + pez.width + 40
    c = Lienzo(x_texto + ancho + 120, 940)
    # halo azul detrás del pez (como la luz de la interfaz) y el pez encima
    mascara = Image.new('L', (c.w, c.h), 0)
    mascara.paste(a_alto(pez.split()[3], pez.height * SS), (x_pez * SS, y_pez * SS))
    c.halo(mascara.point(lambda v: 255 if v > 90 else 0), GLOW_AZUL, 26, 0.55, ancho=8)
    c.pegar(pez, x_pez, y_pez)
    # el título, con el mismo estilo de siempre y su borde de luz azul
    t = Lienzo(c.w // SS, c.h // SS)
    x1, y1 = titulo(t, x_texto, 150, 'FISH OUT', alto, NARANJA_ARRIBA, NARANJA_ABAJO)
    titulo(t, x_texto + 60, y1 + 36, 'OF WATER', alto, AZUL_ARRIBA, AZUL_ABAJO)
    titulo_img = t.resultado(glow=12)
    img = c.resultado()
    img.alpha_composite(titulo_img)
    bb = img.split()[3].point(lambda v: 255 if v > 4 else 0).getbbox()
    m = 24
    return img.crop((max(0, bb[0] - m), max(0, bb[1] - m), min(img.width, bb[2] + m), min(img.height, bb[3] + m)))


def icono():
    """El protagonista 3D de frente sobre azul marino, con un halo azul detrás de la cabeza."""
    c = Lienzo(1024, 1024)
    fondo = [(90, 0), (934, 0), (1024, 90), (1024, 934), (934, 1024), (90, 1024), (0, 934), (0, 90)]
    forma = c.mascara(fondo)
    c.pintar(forma, *FONDO_ICONO)
    luz = Image.new('L', (c.w, c.h), 0)
    ImageDraw.Draw(luz).ellipse([270 * SS, 150 * SS, 754 * SS, 640 * SS], fill=255)
    c.halo(luz, GLOW_AZUL, 90, 0.75)
    pez = a_alto(pez_3d('pez_icono', 0.8), 930)
    if pez.width > 1000:
        pez = pez.resize((1000, round(pez.height * 1000 / pez.width)), Image.LANCZOS)
    c.pegar(pez, (1024 - pez.width) / 2, 1024 - pez.height)
    # recorta todo a la forma del ícono y le pone un borde fino azul
    v = np.asarray(c.img, np.float32)
    v[..., 3] *= np.asarray(forma, np.float32) / 255
    c.img = Image.fromarray(v.astype(np.uint8), 'RGBA')
    d = ImageDraw.Draw(c.img)
    d.line([(x * SS, y * SS) for x, y in fondo + fondo[:1]], fill=GLOW_AZUL + (200,), width=6 * SS, joint='curve')
    return c.resultado()


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
