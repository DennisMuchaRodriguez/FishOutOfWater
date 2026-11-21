# -*- coding: utf-8 -*-
"""
Imágenes de la página de itch.io (paso 2 de 2).

Toma los renders de render_arte_itch.py (el pez con su traje saliendo del lago y las aves, hechos con los modelos del
juego) y les pone el título con el estilo del menú del juego: fuentes Oxanium y Rajdhani (las del juego), "FISH OUT"
en naranja y "OF WATER" en azul eléctrico, contorno oscuro, halo y la separación de color de los hologramas.

    python3 Herramientas/Itch/componer_imagenes.py <carpeta de los renders>      (desde la raíz del proyecto)

Escribe en Itch/:
    portada_630x500.png        Portada (la que sale en las listas de itch.io). También portada_1260x1000.png (2x)
    banner_960x300.png         Banner de la página (tema). También banner_1920x600.png (2x)
    fondo_1920x1080.jpg        Fondo de la página (oscuro y desenfocado, para que el texto se lea)
    arte_principal_1920x1080.png  Arte con el título (redes, devlogs, prensa)
    tarjeta_redes_1200x630.png    Para compartir el enlace en redes
Requisitos: pip install pillow numpy
"""
import os
import sys

import numpy as np
from PIL import Image, ImageDraw, ImageEnhance, ImageFilter, ImageFont

ROOT = os.getcwd()
FONTS = os.path.join(ROOT, 'Assets', 'Resources', 'Fonts')
TITLE_FONT = os.path.join(FONTS, 'Oxanium-ExtraBold.ttf')
BODY_FONT = os.path.join(FONTS, 'Rajdhani-Bold.ttf')
OUT = os.path.join(ROOT, 'Itch')

# Los colores de la interfaz del juego (MenuUI)
INK = (2, 8, 22)
CREAM = (232, 244, 255)
ACCENT = (64, 184, 255)
ORANGE_TOP, ORANGE_BOTTOM = (255, 190, 92), (255, 104, 34)
BLUE_TOP, BLUE_BOTTOM = (160, 230, 255), (46, 140, 255)

TAGLINE = 'UN PEZ. UN TRAJE. NADA DE AGUA.'
STUDIO = 'KAI CODEX'


# ---------------------------------------------------------------- acabado de los renders

def finish(img):
    """Brillo suave en lo más claro (sol, agua, llamas), viñeta y un poco más de color."""
    a = np.asarray(img.convert('RGB')).astype(np.float32) / 255.0
    lum = a @ np.array([0.2126, 0.7152, 0.0722], dtype=np.float32)
    bright = (np.clip((lum - 0.72) / 0.28, 0, 1)[..., None] * a * 255).astype(np.uint8)
    bloom = np.asarray(Image.fromarray(bright).filter(ImageFilter.GaussianBlur(img.width / 90.0))).astype(np.float32) / 255.0
    a = 1.0 - (1.0 - a) * (1.0 - bloom * 0.45)
    h, w = lum.shape
    yy, xx = np.mgrid[0:h, 0:w]
    d = np.sqrt(((xx - w / 2) / (w / 2)) ** 2 + ((yy - h / 2) / (h / 2)) ** 2)
    a *= (1.0 - 0.22 * np.clip(d - 0.55, 0, 1) ** 1.5)[..., None]
    out = Image.fromarray((np.clip(a, 0, 1) * 255).astype(np.uint8))
    return ImageEnhance.Color(out).enhance(1.08)


def shade(img, side, strength, reach):
    """Oscurece un borde con un degradado (el texto se lee sobre el cielo y el agua)."""
    w, h = img.size
    if side in ('left', 'right'):
        ramp = np.linspace(1, 0, int(w * reach)) ** 1.6
        line = np.zeros(w); line[:len(ramp)] = ramp
        if side == 'right': line = line[::-1]
        alpha = np.tile(line, (h, 1))
    else:
        ramp = np.linspace(1, 0, int(h * reach)) ** 1.6
        col = np.zeros(h); col[:len(ramp)] = ramp
        if side == 'bottom': col = col[::-1]
        alpha = np.tile(col[:, None], (1, w))
    layer = Image.new('RGBA', (w, h), INK + (0,))
    layer.putalpha(Image.fromarray((alpha * 255 * strength).astype(np.uint8)))
    return Image.alpha_composite(img.convert('RGBA'), layer)


# ---------------------------------------------------------------- texto

def text_width(font, text, spacing):
    return sum(font.getlength(c) for c in text) + spacing * max(0, len(text) - 1)


def draw_text(draw, xy, text, font, fill, spacing=0.0, stroke=0, stroke_fill=None):
    x, y = xy
    for c in text:
        draw.text((x, y), c, font=font, fill=fill, stroke_width=stroke, stroke_fill=stroke_fill if stroke_fill is not None else fill)
        x += font.getlength(c) + spacing


def gradient(size, top, bottom):
    w, h = size
    t = np.linspace(0, 1, h)[:, None, None]
    g = np.array(top, dtype=np.float32) * (1 - t) + np.array(bottom, dtype=np.float32) * t
    return Image.fromarray(np.repeat(g, w, axis=1).astype(np.uint8))


def title_run(canvas, xy, text, size, top, bottom, glow, spacing):
    """Una palabra del título: halo, sombra, contorno, separación de color y relleno en degradado."""
    W, H = canvas.size
    font = ImageFont.truetype(TITLE_FONT, int(size))
    stroke = max(2, int(size * 0.075))
    mask = Image.new('L', (W, H), 0)
    draw_text(ImageDraw.Draw(mask), xy, text, font, 255, spacing)
    outline = Image.new('L', (W, H), 0)
    draw_text(ImageDraw.Draw(outline), xy, text, font, 255, spacing, stroke=stroke)

    def paint(color, alpha_mask, opacity):
        layer = Image.new('RGBA', (W, H), color + (0,))
        layer.putalpha(Image.fromarray((np.asarray(alpha_mask).astype(np.float32) * opacity).astype(np.uint8)))
        return layer

    halo = outline.filter(ImageFilter.MaxFilter(5)).filter(ImageFilter.GaussianBlur(size * 0.22))
    canvas.alpha_composite(paint(glow, halo, 0.65))
    shadow = Image.new('L', (W, H), 0)
    shadow.paste(outline, (0, int(size * 0.06)))
    canvas.alpha_composite(paint(INK, shadow.filter(ImageFilter.GaussianBlur(size * 0.04)), 0.8))
    canvas.alpha_composite(paint(INK, outline, 0.96))
    # Separación de color de los hologramas (como los títulos del menú)
    off = max(1, int(size * 0.022))
    cyan = Image.new('L', (W, H), 0); cyan.paste(mask, (-off, 0))
    red = Image.new('L', (W, H), 0); red.paste(mask, (off, 0))
    canvas.alpha_composite(paint(ACCENT, cyan, 0.35))
    canvas.alpha_composite(paint((255, 64, 90), red, 0.25))
    fill = gradient((W, H), top, bottom).convert('RGBA')
    fill.putalpha(mask)
    canvas.alpha_composite(fill)
    return text_width(font, text, spacing)


def logo(canvas, x, y, size, one_line=False, center=False):
    """FISH OUT (naranja) / OF WATER (azul). Devuelve el alto que ocupa."""
    spacing = size * 0.03
    f = ImageFont.truetype(TITLE_FONT, int(size))
    if one_line:
        w1 = text_width(f, 'FISH OUT ', spacing)
        total = w1 + text_width(f, 'OF WATER', spacing)
        x0 = x - total / 2 if center else x
        title_run(canvas, (x0, y), 'FISH OUT', size, ORANGE_TOP, ORANGE_BOTTOM, (255, 120, 40), spacing)
        title_run(canvas, (x0 + w1, y), 'OF WATER', size, BLUE_TOP, BLUE_BOTTOM, (40, 150, 255), spacing)
        return size * 1.15
    title_run(canvas, (x, y), 'FISH OUT', size, ORANGE_TOP, ORANGE_BOTTOM, (255, 120, 40), spacing)
    title_run(canvas, (x + size * 0.28, y + size * 0.98), 'OF WATER', size, BLUE_TOP, BLUE_BOTTOM, (40, 150, 255), spacing)
    return size * 2.1


def pill(canvas, x, y, text, size, center=False, color=CREAM, edge=ACCENT):
    """El subtítulo en su cápsula de cristal (como en el menú del juego)."""
    font = ImageFont.truetype(BODY_FONT, int(size))
    spacing = size * 0.22
    tw = text_width(font, text, spacing)
    pad_x, pad_y = size * 0.9, size * 0.38
    w, h = tw + pad_x * 2, size + pad_y * 2
    x0 = x - w / 2 if center else x
    layer = Image.new('RGBA', canvas.size, (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)
    r = h * 0.5
    d.rounded_rectangle((x0, y, x0 + w, y + h), r, fill=INK + (170,), outline=edge + (220,), width=max(1, int(size * 0.07)))
    canvas.alpha_composite(layer)
    d = ImageDraw.Draw(canvas)
    draw_text(d, (x0 + pad_x, y + pad_y - size * 0.12), text, font, color + (255,), spacing)
    return w, h


def small(canvas, xy, text, size, color=CREAM, spacing_k=0.18, anchor_right=False, alpha=230):
    font = ImageFont.truetype(BODY_FONT, int(size))
    spacing = size * spacing_k
    x, y = xy
    if anchor_right: x -= text_width(font, text, spacing)
    layer = Image.new('RGBA', canvas.size, (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)
    draw_text(d, (x + 2, y + 2), text, font, INK + (200,), spacing)
    draw_text(d, (x, y), text, font, color + (alpha,), spacing)
    canvas.alpha_composite(layer)


# ---------------------------------------------------------------- piezas

def cover(src):
    img = finish(Image.open(src))
    w, h = img.size
    img = img.crop((0, 0, w, int(w * 500 / 630))).resize((1260, 1000), Image.LANCZOS)
    c = shade(img, 'top', 0.55, 0.36)
    logo(c, 630, 46, 104, one_line=True, center=True)
    pill(c, 630, 178, TAGLINE, 30, center=True)
    c = c.convert('RGB')
    c.save(os.path.join(OUT, 'portada_1260x1000.png'))
    c.resize((630, 500), Image.LANCZOS).save(os.path.join(OUT, 'portada_630x500.png'))


def banner(src):
    img = finish(Image.open(src)).resize((1920, 600), Image.LANCZOS)
    c = shade(img, 'left', 0.62, 0.6)
    logo(c, 96, 60, 150)
    pill(c, 102, 400, TAGLINE, 34)
    small(c, (108, 520), 'UN JUEGO DE ' + STUDIO, 30, ACCENT, 0.3)
    c = c.convert('RGB')
    c.save(os.path.join(OUT, 'banner_1920x600.png'))
    c.resize((960, 300), Image.LANCZOS).save(os.path.join(OUT, 'banner_960x300.png'))


def key_art(src):
    img = finish(Image.open(src)).resize((1920, 1080), Image.LANCZOS)
    plain = img.copy()
    c = shade(img, 'bottom', 0.6, 0.5)
    c = shade(c.convert('RGB'), 'left', 0.35, 0.5)
    logo(c, 86, 640, 136)
    pill(c, 92, 940, TAGLINE, 32)
    small(c, (1840, 1010), 'UN JUEGO DE ' + STUDIO, 30, ACCENT, 0.3, anchor_right=True)
    c.convert('RGB').save(os.path.join(OUT, 'arte_principal_1920x1080.png'))

    # Tarjeta para redes (1200x630): el mismo arte, recortado
    t = plain.crop((0, 36, 1920, 36 + 1008)).resize((1200, 630), Image.LANCZOS)
    t = shade(t, 'bottom', 0.62, 0.55)
    t = shade(t.convert('RGB'), 'left', 0.35, 0.55)
    logo(t, 54, 352, 84)
    pill(t, 58, 538, TAGLINE, 21)
    small(t, (1150, 584), 'DISPONIBLE EN ITCH.IO  ·  ' + STUDIO, 21, ACCENT, 0.25, anchor_right=True)
    t.convert('RGB').save(os.path.join(OUT, 'tarjeta_redes_1200x630.png'))

    # Fondo de la página: desenfocado y oscuro (el texto de la página va encima)
    f = plain.filter(ImageFilter.GaussianBlur(14))
    dark = Image.new('RGB', f.size, INK)
    f = Image.blend(f, dark, 0.66)
    f.save(os.path.join(OUT, 'fondo_1920x1080.jpg'), quality=88)


def main():
    renders = sys.argv[1] if len(sys.argv) > 1 else 'render'
    os.makedirs(OUT, exist_ok=True)
    cover(os.path.join(renders, 'arte_portada.png'))
    banner(os.path.join(renders, 'arte_banner.png'))
    key_art(os.path.join(renders, 'arte_ancho.png'))
    for f in sorted(os.listdir(OUT)):
        p = os.path.join(OUT, f)
        if f.lower().endswith(('.png', '.jpg')):
            with Image.open(p) as im:
                print('%-32s %4dx%-4d %5d KB' % (f, im.width, im.height, os.path.getsize(p) // 1024))


if __name__ == '__main__':
    main()
