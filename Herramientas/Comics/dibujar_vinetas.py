# -*- coding: utf-8 -*-
"""Dibuja las viñetas del modo Historia (escenas.py) en dos pasos:

  1. Guion gráfico con monigotes de palitos (ideas: quién está, qué hace, flechas y notas), una hoja por cómic:
         python3 Herramientas/Comics/dibujar_vinetas.py guion [carpeta]   (por defecto Herramientas/Comics/guion_grafico)
     (y las mismas hojas con los dibujos terminados: 'hojas_lapiz [carpeta]')
  2. Las viñetas terminadas, en el estilo 2D del protagonista (contornos negros y colores planos), en
     Assets/Art/Comics/<id>.jpg (con su .meta de Sprite):
         python3 Herramientas/Comics/dibujar_vinetas.py vinetas [id ...]
     o para revisar sin tocar el juego:
         python3 Herramientas/Comics/dibujar_vinetas.py prueba <carpeta> [id ...]

Después, asignar_vinetas.py pone dibujos y textos en los cómics (solo hace falta si cambian los ids o los textos).
Solo numpy y Pillow.
"""
import os, sys, uuid
from PIL import Image, ImageDraw, ImageFont

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from guion import COMICS, VINETAS
from lapiz import Lienzo, REPO, FUENTES
from escenas import ESCENAS

OUT = os.path.join(REPO, 'Assets', 'Art', 'Comics')
GUION = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'guion_grafico')

META = """fileFormatVersion: 2
guid: {guid}
TextureImporter:
  internalIDToNameTable: []
  externalObjects: {{}}
  serializedVersion: 13
  mipmaps:
    mipMapMode: 0
    enableMipMap: 0
    sRGBTexture: 1
    linearTexture: 0
    fadeOut: 0
    borderMipMap: 0
    mipMapsPreserveCoverage: 0
    alphaTestReferenceValue: 0.5
    mipMapFadeDistanceStart: 1
    mipMapFadeDistanceEnd: 3
  bumpmap:
    convertToNormalMap: 0
    externalNormalMap: 0
    heightScale: 0.25
    normalMapFilter: 0
    flipGreenChannel: 0
  isReadable: 0
  streamingMipmaps: 0
  streamingMipmapsPriority: 0
  vTOnly: 0
  ignoreMipmapLimit: 0
  grayScaleToAlpha: 0
  generateCubemap: 6
  cubemapConvolution: 0
  seamlessCubemap: 0
  textureFormat: 1
  maxTextureSize: 2048
  textureSettings:
    serializedVersion: 2
    filterMode: 1
    aniso: 1
    mipBias: 0
    wrapU: 1
    wrapV: 1
    wrapW: 1
  nPOTScale: 0
  lightmap: 0
  compressionQuality: 50
  spriteMode: 1
  spriteExtrude: 1
  spriteMeshType: 0
  alignment: 0
  spritePivot: {{x: 0.5, y: 0.5}}
  spritePixelsToUnits: 100
  spriteBorder: {{x: 0, y: 0, z: 0, w: 0}}
  spriteGenerateFallbackPhysicsShape: 0
  alphaUsage: 0
  alphaIsTransparency: 0
  spriteTessellationDetail: -1
  textureType: 8
  textureShape: 1
  singleChannelComponent: 0
  flipbookRows: 1
  flipbookColumns: 1
  maxTextureSizeSet: 0
  compressionQualitySet: 0
  textureFormatSet: 0
  ignorePngGamma: 0
  applyGammaDecoding: 0
  swizzle: 50462976
  cookieLightType: 0
  platformSettings:
  - serializedVersion: 4
    buildTarget: DefaultTexturePlatform
    maxTextureSize: 2048
    resizeAlgorithm: 0
    textureFormat: -1
    textureCompression: 1
    compressionQuality: 50
    crunchedCompression: 1
    allowsAlphaSplitting: 0
    overridden: 0
    ignorePlatformSupport: 0
    androidETC2FallbackOverride: 0
    forceMaximumCompressionQuality_BC6H_BC7: 0
  - serializedVersion: 4
    buildTarget: Standalone
    maxTextureSize: 2048
    resizeAlgorithm: 0
    textureFormat: -1
    textureCompression: 1
    compressionQuality: 50
    crunchedCompression: 1
    allowsAlphaSplitting: 0
    overridden: 0
    ignorePlatformSupport: 0
    androidETC2FallbackOverride: 0
    forceMaximumCompressionQuality_BC6H_BC7: 0
  spriteSheet:
    serializedVersion: 2
    sprites: []
    outline: []
    customData:
    physicsShape: []
    bones: []
    spriteID: 5e97eb03825dee720800000000000000
    internalID: 0
    vertices: []
    indices:
    edges: []
    weights: []
    secondaryTextures: []
    spriteCustomMetadata:
      entries: []
    nameFileIdTable: {{}}
  mipmapLimitGroupName:
  pSDRemoveMatte: 0
  userData:
  assetBundleName:
  assetBundleVariant:
"""


def dibujar(vid, modo):
    i = [v[0] for v in VINETAS].index(vid)
    c = Lienzo(modo, seed=1000 + i)
    ESCENAS[vid](c)
    return c.imagen()


def guardar(img, path):
    # JPG de buena calidad (sin submuestreo de color): pesa mucho menos que un PNG con el grano del papel
    img.save(path, 'JPEG', quality=92, optimize=True, subsampling=0)
    meta = path + '.meta'
    if not os.path.exists(meta):
        with open(meta, 'w', encoding='utf-8', newline='\n') as f:
            f.write(META.format(guid=uuid.uuid4().hex))


def _envolver(draw, texto, fuente, ancho):
    lineas, actual = [], ''
    for palabra in texto.split():
        prueba = (actual + ' ' + palabra).strip()
        if draw.textlength(prueba, font=fuente) <= ancho:
            actual = prueba
        else:
            lineas.append(actual)
            actual = palabra
    if actual:
        lineas.append(actual)
    return lineas


def hoja(comic, vinetas, carpeta, modo='palito'):
    """Una hoja con las viñetas de un cómic en miniatura (2 columnas), con el número y el texto de cada una."""
    tw, th = 620, 297
    cols = 2 if len(vinetas) > 1 else 1
    filas = (len(vinetas) + cols - 1) // cols
    margen, alto_txt, titulo = 30, 92, 70
    W = margen + cols * (tw + margen)
    H = titulo + filas * (th + alto_txt + margen) + margen
    img_hoja = Image.new('RGB', (W, H), (250, 249, 245))
    d = ImageDraw.Draw(img_hoja)
    f_tit = ImageFont.truetype(os.path.join(FUENTES, 'LilitaOne-Regular.ttf'), 38)
    f_txt = ImageFont.truetype(os.path.join(FUENTES, 'Rajdhani-Bold.ttf'), 24)
    d.text((margen, 18), comic.replace('_', ' ') + ('  -  guion gráfico' if modo == 'palito' else '  -  viñetas'),
           font=f_tit, fill=(30, 30, 36))
    for k, (vid, amb, texto) in enumerate(vinetas):
        fila, col = divmod(k, cols)
        x = margen + col * (tw + margen)
        y = titulo + fila * (th + alto_txt + margen)
        img = dibujar(vid, modo).resize((tw, th), Image.LANCZOS)
        img_hoja.paste(img, (x, y))
        d.rectangle([x - 1, y - 1, x + tw, y + th], outline=(40, 40, 44), width=3)
        d.text((x, y + th + 6), '%d. %s' % (k + 1, vid), font=f_txt, fill=(150, 60, 40))
        for j, linea in enumerate(_envolver(d, texto, f_txt, tw)[:2]):
            d.text((x, y + th + 34 + j * 26), linea, font=f_txt, fill=(40, 40, 44))
    os.makedirs(carpeta, exist_ok=True)
    ruta = os.path.join(carpeta, comic + ('' if modo == 'palito' else '_lapiz') + '.png')
    img_hoja.save(ruta, optimize=True)
    print('HOJA', ruta, flush=True)
    return ruta


def main():
    if len(sys.argv) < 2:
        print(__doc__)
        return
    accion = sys.argv[1]
    if accion in ('guion', 'hojas_lapiz'):
        carpeta = sys.argv[2] if len(sys.argv) > 2 else GUION
        for comic, vs in COMICS:
            hoja(comic, vs, carpeta, 'palito' if accion == 'guion' else 'caricatura')
    elif accion == 'vinetas':
        solo = set(sys.argv[2:])
        os.makedirs(OUT, exist_ok=True)
        for vid, comic, amb, texto in VINETAS:
            if solo and vid not in solo:
                continue
            guardar(dibujar(vid, 'caricatura'), os.path.join(OUT, vid + '.jpg'))
            print('VIÑETA', vid, flush=True)
    elif accion == 'prueba':
        carpeta = sys.argv[2]
        solo = set(sys.argv[3:])
        os.makedirs(carpeta, exist_ok=True)
        for vid, comic, amb, texto in VINETAS:
            if solo and vid not in solo:
                continue
            for modo in ('palito', 'caricatura'):
                dibujar(vid, modo).save(os.path.join(carpeta, '%s_%s.png' % (vid, modo)))
            print('PRUEBA', vid, flush=True)
    else:
        print(__doc__)


if __name__ == '__main__':
    main()
