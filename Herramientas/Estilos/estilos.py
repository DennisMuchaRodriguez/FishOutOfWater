# -*- coding: utf-8 -*-
"""Estilos (skins) del pez: recolorea las texturas del modelo (cuerpo del pez y traje) para cada estilo.

    python3 Herramientas/Estilos/estilos.py           (desde la raíz del proyecto; numpy y Pillow)

Escribe Assets/Resources/Skins/<Estilo>/Cuerpo.png y Armadura.png (1024 x 1024, con su .meta). Las vistas previas del
menú (Vista.png) las hace Herramientas/Logo/render_pez_3d.py con "estilos" (Blender).
Cada estilo cambia:
  - el CUERPO: la cara (naranja en el original) y las aletas (azules) cambian de tono; la capucha oscura queda igual;
  - el TRAJE: el gris azulado se pinta con un degradado nuevo (de un color oscuro a uno claro), con sus detalles.
Para agregar otro estilo, súmalo a ESTILOS (y a SkinCatalog.cs).
"""
import os
import uuid

import numpy as np
from PIL import Image

TEX = os.path.join('Assets', 'Models', 'Texturas')
OUT = os.path.join('Assets', 'Resources', 'Skins')
SIZE = 1024

# tono (grados) de la cara y de las aletas; degradado del traje (oscuro, claro)
ESTILOS = {
    'NeonAbisal': dict(cara=318, aletas=178, cara_sat=1.0, aletas_sat=1.05,
                       traje=((10, 8, 22), (150, 78, 210))),
    'SolDorado': dict(cara=272, aletas=44, cara_sat=0.85, aletas_sat=1.15,
                      traje=((70, 48, 18), (255, 226, 150))),
}


def recolor_cuerpo(img, cara, aletas, cara_sat, aletas_sat):
    hsv = np.asarray(img.convert('RGB').convert('HSV'), np.float32)
    h, s, v = hsv[..., 0] * 360 / 255, hsv[..., 1], hsv[..., 2]
    color = s > 60
    es_cara = color & ((h < 28) | (h > 340))
    es_aleta = color & (h > 185) & (h < 250)
    h = np.where(es_cara, cara, np.where(es_aleta, aletas, h))
    s = np.where(es_cara, np.clip(s * cara_sat, 0, 255), np.where(es_aleta, np.clip(s * aletas_sat, 0, 255), s))
    out = np.stack([h * 255 / 360, s, v], axis=-1).astype(np.uint8)
    return Image.fromarray(out, 'HSV').convert('RGB')


def recolor_traje(img, oscuro, claro):
    rgba = img.convert('RGBA')
    a = np.asarray(rgba, np.float32)
    rgb, alpha = a[..., :3], a[..., 3:]
    hsv = np.asarray(rgba.convert('RGB').convert('HSV'), np.float32)
    lum = rgb @ np.array([0.299, 0.587, 0.114], np.float32) / 255
    lum = np.clip((lum - 0.12) / 0.7, 0, 1)                      # un poco más de contraste
    o = np.array(oscuro, np.float32)
    c = np.array(claro, np.float32)
    nuevo = o + (c - o) * lum[..., None]
    # La luz roja del pecho (y lo muy saturado) queda como estaba
    deja = (hsv[..., 1] > 150)[..., None]
    rgb = np.where(deja, rgb, nuevo)
    return Image.fromarray(np.concatenate([rgb, alpha], axis=-1).clip(0, 255).astype(np.uint8), 'RGBA')


def meta(path, base):
    m = path + '.meta'
    if os.path.exists(m):
        return
    lineas = open(base + '.meta', encoding='utf-8').read().split('\n')
    lineas[1] = 'guid: ' + uuid.uuid4().hex
    with open(m, 'w', encoding='utf-8', newline='\n') as f:
        f.write('\n'.join(lineas))


def carpeta_meta(folder):
    m = folder + '.meta'
    if not os.path.exists(m):
        with open(m, 'w', encoding='utf-8', newline='\n') as f:
            f.write("fileFormatVersion: 2\nguid: %s\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n"
                    "  userData: \n  assetBundleName: \n  assetBundleVariant: \n" % uuid.uuid4().hex)


def main():
    cuerpo_src = os.path.join(TEX, 'Body', 'Pez_F_Model_Body_BaseColor.png')
    traje_src = os.path.join(TEX, 'Armor', 'Pez_F_Model_Armor_BaseColor.png')
    cuerpo = Image.open(cuerpo_src).resize((SIZE, SIZE), Image.LANCZOS)
    traje = Image.open(traje_src).resize((SIZE, SIZE), Image.LANCZOS)
    os.makedirs(OUT, exist_ok=True)
    carpeta_meta(OUT)
    for nombre, e in ESTILOS.items():
        d = os.path.join(OUT, nombre)
        os.makedirs(d, exist_ok=True)
        carpeta_meta(d)
        p = os.path.join(d, 'Cuerpo.png')
        recolor_cuerpo(cuerpo, e['cara'], e['aletas'], e['cara_sat'], e['aletas_sat']).save(p, optimize=True)
        meta(p, cuerpo_src)
        p = os.path.join(d, 'Armadura.png')
        recolor_traje(traje, *e['traje']).save(p, optimize=True)
        meta(p, traje_src)
        print('ESTILO', nombre)


if __name__ == '__main__':
    main()
