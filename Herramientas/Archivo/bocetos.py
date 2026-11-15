# -*- coding: utf-8 -*-
"""Paso 2 de los bocetos del Archivo de aves: convierte los renders de render_aves.py en bocetos holográficos
(líneas azul eléctrico de trazo doble, sombreado a rayas, marcas de construcción y líneas de escaneo) para
Assets/Resources/Archivo/<Nombre>.png, con el mismo estilo que la interfaz del casco.
    python Herramientas/Archivo/bocetos.py <carpeta con los renders>
"""
import os, sys, glob, uuid
import numpy as np
from PIL import Image, ImageFilter

REPO = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..'))
OUT = os.path.join(REPO, 'Assets', 'Resources', 'Archivo')
SIZE = 512

ELECTRIC = np.array([64, 184, 255]) / 255.0
GLOW = np.array([190, 236, 255]) / 255.0
DEEP = np.array([30, 90, 255]) / 255.0

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
  maxTextureSize: 512
  textureSettings:
    serializedVersion: 2
    filterMode: 1
    aniso: 1
    mipBias: 0
    wrapU: 1
    wrapV: 1
    wrapW: 1
  nPOTScale: 1
  lightmap: 0
  compressionQuality: 50
  spriteMode: 0
  spriteExtrude: 1
  spriteMeshType: 1
  alignment: 0
  spritePivot: {{x: 0.5, y: 0.5}}
  spritePixelsToUnits: 100
  spriteBorder: {{x: 0, y: 0, z: 0, w: 0}}
  spriteGenerateFallbackPhysicsShape: 1
  alphaUsage: 1
  alphaIsTransparency: 1
  spriteTessellationDetail: -1
  textureType: 0
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
    maxTextureSize: 512
    resizeAlgorithm: 0
    textureFormat: -1
    textureCompression: 1
    compressionQuality: 50
    crunchedCompression: 0
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
    spriteID: 
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


def sobel(img):
    p = np.pad(img, 1, mode='edge')
    gx = (p[:-2, 2:] + 2 * p[1:-1, 2:] + p[2:, 2:]) - (p[:-2, :-2] + 2 * p[1:-1, :-2] + p[2:, :-2])
    gy = (p[2:, :-2] + 2 * p[2:, 1:-1] + p[2:, 2:]) - (p[:-2, :-2] + 2 * p[:-2, 1:-1] + p[:-2, 2:])
    return np.sqrt(gx * gx + gy * gy)


def noise(h, w, scale, seed):
    """Ruido suave (bilineal de una rejilla aleatoria)."""
    rng = np.random.default_rng(seed)
    gh, gw = h // scale + 2, w // scale + 2
    g = rng.random((gh, gw))
    yy = np.arange(h) / scale
    xx = np.arange(w) / scale
    y0 = np.floor(yy).astype(int); x0 = np.floor(xx).astype(int)
    fy = (yy - y0)[:, None]; fx = (xx - x0)[None, :]
    a = g[y0][:, x0]; b = g[y0][:, x0 + 1]; c = g[y0 + 1][:, x0]; d = g[y0 + 1][:, x0 + 1]
    return a * (1 - fx) * (1 - fy) + b * fx * (1 - fy) + c * (1 - fx) * fy + d * fx * fy


def shift(img, dx, dy):
    return np.roll(np.roll(img, dy, 0), dx, 1)


def blur(img, r):
    im = Image.fromarray((np.clip(img, 0, 1) * 255).astype(np.uint8))
    return np.asarray(im.filter(ImageFilter.GaussianBlur(r))).astype(float) / 255.0


def sketch(luz_path, normal_path, seed):
    luz = np.asarray(Image.open(luz_path).convert('RGBA')).astype(float) / 255.0
    nor = np.asarray(Image.open(normal_path).convert('RGBA')).astype(float) / 255.0
    A = luz[..., 3]
    L = luz[..., :3].mean(axis=2)
    H, W = A.shape

    # Contornos: silueta + cambios de normal (pliegues, ojos, plumas)
    e_sil = sobel(A)
    e_nor = sum(sobel(nor[..., c] * A) for c in range(3))
    e_lum = sobel(L * A)
    edges = np.clip(e_sil * 1.2 + np.clip(e_nor - 0.35, 0, None) * 0.9 + np.clip(e_lum - 0.25, 0, None) * 0.6, 0, 1)

    # Trazo de boceto: dos pasadas algo corridas y con cortes
    n1 = noise(H, W, 14, seed)
    n2 = noise(H, W, 9, seed + 1)
    stroke1 = edges * (n1 > 0.18)
    stroke2 = shift(edges, 1 + seed % 2, -1) * (n2 > 0.35) * 0.55
    lines = np.clip(np.maximum(stroke1, stroke2), 0, 1)

    # Sombreado a rayas en las sombras (diagonal; cruzado en lo más oscuro), un poco ondulado
    yy, xx = np.mgrid[0:H, 0:W].astype(float)
    wob = (noise(H, W, 20, seed + 2) - 0.5) * 3.0
    # oscuridad relativa a la luz del propio modelo (así siempre hay rayado en el lado en sombra)
    inside = L[A > 0.5]
    hi = np.percentile(inside, 92) if inside.size else 1.0
    lo_ = np.percentile(inside, 8) if inside.size else 0.0
    dark = np.clip((hi - L) / max(0.05, hi - lo_), 0, 1) * (A > 0.5)
    h1 = (np.mod(xx + yy + wob, 6.0) < 1.1).astype(float)
    h2 = (np.mod(xx - yy + wob, 6.0) < 1.1).astype(float)
    hatch = h1 * (dark > 0.35) * 0.55 + h2 * (dark > 0.7) * 0.5
    hatch *= (noise(H, W, 6, seed + 3) > 0.25)

    # Marcas de construcción: ejes y círculo punteados detrás del ave
    ys, xs = np.nonzero(A > 0.5)
    cx, cy = (xs.mean(), ys.mean()) if len(xs) else (W / 2, H / 2)
    r = 0.44 * W
    dist = np.hypot(xx - cx, yy - cy)
    ang = np.arctan2(yy - cy, xx - cx)
    circle = (np.abs(dist - r) < 0.9) * (np.mod(ang * 40, 2) < 1.2) * 0.35
    axes = ((np.abs(xx - cx) < 0.6) | (np.abs(yy - cy) < 0.6)) * (np.mod(xx + yy, 12) < 6) * 0.18
    ticks = np.zeros_like(A)
    for k in range(-4, 5):
        tx = int(cx + k * W * 0.1)
        if 0 <= tx < W:
            ticks[max(0, int(cy) - 4):int(cy) + 5, max(0, tx - 0):tx + 1] = 0.35
    construct = np.clip(circle + axes + ticks, 0, 1) * (1 - A * 0.85)

    # Relleno tenue (holograma) y brillo de las líneas
    fill = A * (0.08 + 0.06 * (1 - yy / H))
    core = np.clip(lines + hatch * 0.75, 0, 1)
    glow = blur(core, 3) * 0.7 + blur(core, 8) * 0.35
    scan = 0.82 + 0.18 * (np.mod(yy, 3) < 2)

    alpha = np.clip(np.maximum.reduce([core, glow * 0.75, fill, construct]) * scan, 0, 1)
    # Color: líneas azul eléctrico con el centro casi blanco; brillo y relleno más profundos
    w_core = np.clip(core * 1.2, 0, 1)[..., None]
    w_hot = np.clip(lines - 0.6, 0, 1)[..., None] * 2
    col = DEEP[None, None] * (1 - w_core) + ELECTRIC[None, None] * w_core
    col = col * (1 - np.clip(w_hot, 0, 1)) + GLOW[None, None] * np.clip(w_hot, 0, 1)
    rgba = np.concatenate([np.clip(col, 0, 1), alpha[..., None]], axis=2)
    im = Image.fromarray((rgba * 255).astype(np.uint8), 'RGBA')
    return im.resize((SIZE, SIZE), Image.LANCZOS)


def main():
    src = sys.argv[1]
    os.makedirs(OUT, exist_ok=True)
    folder_meta = OUT + '.meta'
    if not os.path.exists(folder_meta):
        with open(folder_meta, 'w', encoding='utf-8', newline='\n') as f:
            f.write("fileFormatVersion: 2\nguid: %s\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n"
                    "  userData: \n  assetBundleName: \n  assetBundleVariant: \n" % uuid.uuid4().hex)
    for i, luz in enumerate(sorted(glob.glob(os.path.join(src, '*_luz.png')))):
        name = os.path.basename(luz)[:-len('_luz.png')]
        nor = luz.replace('_luz.png', '_normal.png')
        out = os.path.join(OUT, name + '.png')
        sketch(luz, nor, 11 + i).save(out)
        if not os.path.exists(out + '.meta'):
            with open(out + '.meta', 'w', encoding='utf-8', newline='\n') as f:
                f.write(META.format(guid=uuid.uuid4().hex))
        print('BOCETO', name)


if __name__ == '__main__':
    main()
