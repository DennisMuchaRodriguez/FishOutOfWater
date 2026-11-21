# -*- coding: utf-8 -*-
"""Viñetas del modo Historia, paso 2 de 2: convierte las 4 pasadas de render_vinetas.py en bocetos a lápiz con una
aguada apagada (poco color: el pez protagonista conserva un poco de su naranja) sobre papel, y los deja en
Assets/Art/Comics/<id>.jpg (con su .meta de Sprite, comprimido y sin mipmaps).

    python3 Herramientas/Comics/vinetas.py <carpeta con las pasadas> [id ...]

Solo numpy y Pillow. El estilo:
  - líneas de grafito donde cambia el objeto, la orientación de la superficie o la distancia (más gruesas y oscuras
    cerca; finas y claras lejos), dibujadas dos veces con un pequeño corrimiento, como a mano
  - sombreado a rayas (diagonal, cruzado y denso) según la luz de la pasada de arcilla
  - aguada: el color de cada cosa casi sin saturación, aclarado, con el borde un poco más oscuro
  - cielo de papel con nubes a lápiz; bajo el agua, aguada azul grisácea con rayos de luz y burbujas
  - papel cálido con grano
"""
import os, sys, uuid
import numpy as np
from PIL import Image, ImageFilter, ImageDraw

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from guion import VINETAS, AMBIENTE

REPO = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..'))
OUT = os.path.join(REPO, 'Assets', 'Art', 'Comics')

# Clases de render_vinetas.py (R de la pasada de clases)
CLASES = {'entorno': 0.10, 'agua': 0.25, 'pez': 0.50, 'ave': 0.62, 'mecanico': 0.75, 'brillo': 0.85, 'heroe': 1.0}

PAPEL = np.array([0.955, 0.935, 0.885])
TINTA = np.array([0.16, 0.16, 0.19])

# Ambientes: (tinte de la aguada, tinte del cielo arriba, tinte del cielo en el horizonte, nubes)
AMB = {
    'dia':        ((1.00, 1.00, 1.00), (0.80, 0.86, 0.92), (0.97, 0.96, 0.93), 0.55),
    'amanecer':   ((1.00, 0.97, 0.92), (0.86, 0.85, 0.90), (1.00, 0.93, 0.84), 0.45),
    'atardecer':  ((1.00, 0.95, 0.88), (0.78, 0.78, 0.86), (1.00, 0.88, 0.76), 0.50),
    'cielo_rojo': ((1.00, 0.90, 0.84), (0.72, 0.60, 0.60), (0.98, 0.78, 0.66), 0.65),
    'niebla':     ((0.95, 0.96, 0.97), (0.86, 0.88, 0.90), (0.93, 0.93, 0.92), 0.20),
    'bajo_agua':  ((0.88, 0.95, 0.97), (0.70, 0.80, 0.84), (0.83, 0.89, 0.89), 0.0),
    'cueva':      ((0.93, 0.92, 0.90), (0.55, 0.57, 0.60), (0.70, 0.70, 0.70), 0.0),
}

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


# ======================================================================= utilidades
def cargar(path):
    im = Image.open(path)
    im = im.convert('RGBA')
    return np.asarray(im).astype(np.float32) / 255.0


def blur(a, r):
    if r <= 0:
        return a
    if a.ndim == 2:
        im = Image.fromarray(np.clip(a * 255, 0, 255).astype(np.uint8))
        return np.asarray(im.filter(ImageFilter.GaussianBlur(r))).astype(np.float32) / 255.0
    return np.stack([blur(a[..., i], r) for i in range(a.shape[2])], -1)


def dilatar(mask, size):
    if size <= 1:
        return mask
    im = Image.fromarray((np.clip(mask, 0, 1) * 255).astype(np.uint8))
    return np.asarray(im.filter(ImageFilter.MaxFilter(size))).astype(np.float32) / 255.0


def ruido(h, w, escala, rng, octavas=3):
    out = np.zeros((h, w), np.float32)
    amp = 1.0
    tot = 0.0
    for o in range(octavas):
        sh = max(2, int(h / escala) + 2)
        sw = max(2, int(w / escala) + 2)
        r = rng.random((sh, sw)).astype(np.float32)
        im = Image.fromarray(r, 'F').resize((w, h), Image.BICUBIC)
        out += np.asarray(im) * amp
        tot += amp
        amp *= 0.5
        escala = max(1.0, escala / 2.0)
    return out / tot


def desplazar(a, dx, dy):
    return np.roll(np.roll(a, dy, 0), dx, 1)


# ======================================================================= el boceto
def boceto(base, amb, seed):
    rng = np.random.default_rng(seed)
    L = cargar(base + '_luz.png')
    C = cargar(base + '_color.png')
    N = cargar(base + '_normal.png')
    K = cargar(base + '_clase.png')
    h, w = L.shape[:2]
    escala = w / 1380.0                     # 1 en la versión final, ~0.34 en borrador
    tinte, cielo_arriba, cielo_horiz, nubes = AMB[amb]
    yy, xx = np.mgrid[0:h, 0:w].astype(np.float32)

    geo = (K[..., 3] > 0.5).astype(np.float32)
    clase = K[..., 0] * geo
    obj = K[..., 1]
    cerca = K[..., 2] * geo
    ids = np.array(list(CLASES.values()))
    cid = np.argmin(np.abs(clase[..., None] - ids[None, None, :]), axis=-1)
    nombres = list(CLASES.keys())

    def es(nombre):
        return ((cid == nombres.index(nombre)) & (geo > 0)).astype(np.float32)

    agua = es('agua')
    heroe = es('heroe')
    seres = np.maximum.reduce([es('pez'), es('ave'), es('mecanico'), heroe])
    brillo = es('brillo')

    # ---------------- líneas
    n = N[..., :3] * 2 - 1
    na = N[..., 3:4]
    n = n * (na > 0.5)

    def dif_normal(dx, dy):
        q = desplazar(n, dx, dy)
        return 1.0 - np.clip(np.sum(n * q, -1), -1, 1)

    e_n = np.maximum.reduce([dif_normal(1, 0), dif_normal(0, 1), dif_normal(1, 1), dif_normal(-1, 1)])
    e_n = np.clip((e_n - 0.22) / 0.35, 0, 1)

    def dif(a, dx, dy):
        return np.abs(a - desplazar(a, dx, dy))

    e_obj = np.maximum.reduce([dif(obj, 1, 0), dif(obj, 0, 1), dif(geo, 1, 0), dif(geo, 0, 1),
                               dif(clase, 1, 0), dif(clase, 0, 1)])
    e_obj = (e_obj > 0.003).astype(np.float32)
    cmax = np.maximum(cerca, desplazar(cerca, 1, 0))
    e_d = np.maximum(dif(cerca, 1, 0), dif(cerca, 0, 1)) / np.maximum(0.05, cmax)
    e_d = np.clip((e_d - 0.06) / 0.10, 0, 1)
    # El interior del agua no lleva líneas (solo su borde)
    e_n = e_n * (1 - agua * 0.85)
    lineas = np.maximum.reduce([e_obj, e_n * 0.85, e_d * 0.9])
    # Peso según la cercanía: cerca, línea gruesa y oscura; lejos, fina y clara
    cerca_l = np.maximum(cerca, desplazar(cerca, 1, 0))
    peso = np.clip(0.25 + cerca_l * 1.6, 0.25, 1.0)
    peso = np.maximum(peso, seres * 0.9)
    # Gotas y salpicaduras: trazo fino y claro (si no, una salpicadura es un montón de manchas); lo que brilla
    # (fuego del propulsor, cristales) no lleva contorno: es luz
    agua_l = dilatar(agua, 3)
    brillo_l = dilatar(brillo, 3)
    peso = peso * (1 - agua_l * 0.6) * (1 - brillo_l)
    lineas = lineas * peso
    gruesa = dilatar((lineas > 0.55).astype(np.float32) * (np.maximum(cerca_l, seres * 0.6) > 0.35) * (1 - agua_l), max(1, int(round(3 * escala))) | 1)
    lineas = np.maximum(lineas, gruesa * 0.9)
    lineas = blur(lineas, 0.6 * max(escala, 0.5))
    # Trazo doble y presión del lápiz
    presion = 0.75 + 0.35 * ruido(h, w, 60 * escala + 8, rng)
    corr = max(1, int(round(1.6 * escala)))
    lineas2 = desplazar(lineas, corr, -corr) * 0.45
    tinta = np.clip(np.maximum(lineas, lineas2) * presion, 0, 1)

    # ---------------- sombreado: grafito frotado + rayas a mano (trazos cortos) solo en lo oscuro
    lum = (L[..., 0] * 0.3 + L[..., 1] * 0.59 + L[..., 2] * 0.11)
    g = geo > 0
    hi = np.percentile(lum[g], 97) if g.any() else 1.0
    s = np.clip(lum / max(0.05, hi), 0, 1)
    oscuro = (1 - s) * geo * (1 - brillo) * (1 - agua)
    oscuro = blur(oscuro, 1.0 * escala + 0.4)
    cerca_s = np.clip(cerca * 4, 0.25, 1.0)
    img = Image.new('L', (w, h), 0)
    d = ImageDraw.Draw(img)
    for capa_r, (ang, umbral, dens) in enumerate(((-55, 0.42, 1.0), (35, 0.62, 0.8), (-15, 0.80, 0.6))):
        c, si = np.cos(np.radians(ang)), np.sin(np.radians(ang))
        prob = np.clip((oscuro - umbral) / 0.25, 0, 1) * cerca_s
        ys, xs = np.nonzero(prob > 0.02)
        if len(ys) == 0:
            continue
        k = int(len(ys) / max(1.0, (9.0 * escala + 1.5) ** 2) * 2.2 * dens)
        if k <= 0:
            continue
        idx = rng.choice(len(ys), min(k, len(ys)), replace=False)
        keep = rng.random(len(idx)) < prob[ys[idx], xs[idx]]
        for i in idx[keep]:
            y, x = float(ys[i]), float(xs[i])
            largo = rng.uniform(5, 13) * escala + 1.5
            jx, jy = rng.uniform(-0.8, 0.8), rng.uniform(-0.8, 0.8)
            d.line([(x - c * largo, y - si * largo), (x + c * largo + jx, y + si * largo + jy)],
                   fill=int(rng.uniform(120, 230)), width=max(1, int(round(escala * 1.1))))
    rayado = np.asarray(img).astype(np.float32) / 255.0 * geo * (1 - agua)
    rayado = blur(rayado, 0.35) * 0.75
    difumino = np.clip(oscuro - 0.15, 0, 1) * 0.30          # el grafito frotado de las sombras

    # ---------------- aguada
    col = C[..., :3]
    ca = C[..., 3:4]
    gris = (col[..., 0] * 0.3 + col[..., 1] * 0.59 + col[..., 2] * 0.11)[..., None]
    sat = 0.24 + 0.18 * seres + 0.30 * heroe + 0.25 * brillo
    col = gris + (col - gris) * sat[..., None]
    lav = 1 - (1 - col) * (0.55 + 0.1 * seres[..., None])
    lav = lav * np.array(tinte)
    # Lejos: se pierde en el papel (perspectiva aérea)
    lejos = np.clip(1 - cerca / 0.18, 0, 1) * geo * 0.65
    lav = lav * (1 - lejos[..., None]) + PAPEL * lejos[..., None]
    borde_lav = np.clip((lav - blur(lav, 2.5 * escala + 0.6)), -1, 0)
    lav = np.clip(lav + borde_lav * 1.4, 0, 1)
    lav = lav * ca + PAPEL * (1 - ca)

    # ---------------- cielo / fondo
    t = (yy / h)[..., None]
    if amb == 'bajo_agua':
        fondo = np.array(cielo_arriba) * (1 - t) + np.array(cielo_horiz) * t
        fondo = 1 - (1 - fondo) * 0.85
        # rayos de luz desde arriba
        ang = np.radians(-18)
        u = (xx * np.cos(ang) + yy * np.sin(ang)) / (90 * escala + 10)
        rayos = np.clip(np.sin(u * 3.1 + 2 * ruido(h, w, 200 * escala + 20, rng)) * 0.5 + 0.2, 0, 1) * np.clip(1 - yy / (h * 0.9), 0, 1)
        fondo = fondo + rayos[..., None] * 0.08
    elif amb == 'cueva':
        fondo = np.array(cielo_arriba) * (1 - t) + np.array(cielo_horiz) * t
    else:
        fondo = np.array(cielo_arriba) * (1 - t) ** 1.5 + np.array(cielo_horiz) * (1 - (1 - t) ** 1.5)
        fondo = 1 - (1 - fondo) * 0.55
    fondo = np.clip(fondo, 0, 1)
    nube_borde = np.zeros((h, w), np.float32)
    if nubes > 0:
        nn = ruido(h, w, 260 * escala + 30, rng, 4)
        nn = nn * (1 - 0.6 * (yy / h))
        umbral = 0.62 - nubes * 0.12
        nube = np.clip((nn - umbral) / 0.05, 0, 1) * (1 - geo)
        # contorno solo por arriba (el borde de abajo de una nube a lápiz casi no se marca), cortado a trozos
        arriba = np.clip(desplazar(nn, 0, int(4 * escala) + 1) - nn, 0, 1) > 0
        trozos = ruido(h, w, 40 * escala + 6, rng) > 0.45
        nube_borde = np.clip(1 - np.abs(nn - umbral) / 0.010, 0, 1) * (1 - geo) * arriba * trozos * (yy < h * 0.7)
        fondo = fondo * (1 - nube[..., None] * 0.7) + PAPEL * nube[..., None] * 0.7
        sombra = np.clip((nn - umbral - 0.06) / 0.1, 0, 1) * nube
        fondo = fondo * (1 - (sombra * 0.06)[..., None])

    # ---------------- agua desde arriba: rayitas horizontales; desde abajo: ondas claras
    rizos = np.zeros((h, w), np.float32)
    if amb not in ('bajo_agua', 'cueva') and agua.any():
        img = Image.new('L', (w, h), 0)
        d = ImageDraw.Draw(img)
        ys, xs = np.nonzero(agua > 0.5)
        k = min(len(ys), int(260 * (w / 1380.0) ** 2) + 30)
        if k > 0:
            idx = rng.choice(len(ys), k, replace=False)
            for i in idx:
                y, x = int(ys[i]), int(xs[i])
                cc = min(1.0, cerca[y, x] * 3)
                largo = (4 + 34 * cerca[y, x]) * escala + 2
                # rayita con una pequeña onda en el medio
                d.line([(x - largo, y), (x - largo * 0.3, y - 0.8 * escala), (x + largo * 0.3, y + 0.6 * escala), (x + largo, y)],
                       fill=int(60 + 140 * cc), width=max(1, int(round(escala * (0.8 + 0.8 * cc)))))
        rizos = np.asarray(img).astype(np.float32) / 255.0 * agua
    if amb == 'bajo_agua' and agua.any():
        # La superficie vista desde abajo: líneas claras y onduladas
        img = Image.new('L', (w, h), 0)
        d = ImageDraw.Draw(img)
        ys, xs = np.nonzero(agua > 0.5)
        for i in rng.choice(len(ys), min(len(ys), int(120 * (w / 1380.0)) + 20), replace=False):
            y, x = float(ys[i]), float(xs[i])
            largo = rng.uniform(20, 70) * escala + 4
            pts = [(x + t * largo, y + np.sin(t * 6.0 + x * 0.05) * 3 * escala) for t in np.linspace(-1, 1, 9)]
            d.line(pts, fill=int(rng.uniform(90, 170)), width=max(1, int(round(escala))))
        rizos = np.asarray(img).astype(np.float32) / 255.0 * agua * 0.8

    # ---------------- burbujas bajo el agua (dibujadas)
    burb = np.zeros((h, w), np.float32)
    if amb == 'bajo_agua':
        img = Image.new('L', (w, h), 0)
        d = ImageDraw.Draw(img)
        for i in range(int(26 * (w / 1380.0) + 8)):
            x = rng.uniform(0, w)
            y = rng.uniform(0, h * 0.85)
            r = rng.uniform(2, 9) * escala + 1
            d.ellipse([x - r, y - r, x + r, y + r], outline=200, width=max(1, int(round(escala * 1.3))))
            d.arc([x - r * 0.55, y - r * 0.6, x + r * 0.2, y + r * 0.1], 200, 280, fill=160)
        burb = np.asarray(img).astype(np.float32) / 255.0 * (1 - seres)

    # ---------------- papel
    grano = 0.965 + 0.05 * ruido(h, w, 3 * escala + 1.2, rng, 2) + 0.02 * ruido(h, w, 40 * escala + 4, rng, 2)
    vin = 1 - 0.08 * (((xx / w - 0.5) ** 2 + (yy / h - 0.5) ** 2) * 2.2)
    papel = PAPEL * grano[..., None] * vin[..., None]

    # ---------------- composición
    capa = np.where(geo[..., None] > 0, lav, fondo)
    capa = capa * papel / PAPEL
    capa = capa * (1 - difumino[..., None])
    capa = capa * (1 - rayado[..., None] * 0.55)
    capa = capa * (1 - nube_borde[..., None] * 0.0)     # sin contorno: a lápiz quedaban como garabatos
    if amb == 'bajo_agua':
        # superficie desde abajo: aguada clara (luz que entra) con ondas claras
        capa = capa * (1 - agua[..., None] * 0.25) + np.array([0.93, 0.96, 0.96]) * agua[..., None] * 0.25
        capa = np.clip(capa + rizos[..., None] * 0.10, 0, 1)
    else:
        capa = capa * (1 - rizos[..., None] * 0.30)
    capa = capa * (1 - burb[..., None] * 0.45)
    # brillos (cristales, llamas): claros, con un halo
    halo = blur(brillo, 6 * escala + 1)
    capa = np.clip(capa + halo[..., None] * np.array([0.14, 0.09, 0.03]), 0, 1)
    out = capa * (1 - tinta[..., None]) + TINTA * tinta[..., None]
    return np.clip(out, 0, 1)


def guardar(arr, path):
    # JPG de buena calidad (sin submuestreo de color): pesa ~5 veces menos que un PNG con el grano del papel, y en el
    # juego da lo mismo (Unity la comprime igual al importarla)
    im = Image.fromarray((arr * 255 + 0.5).astype(np.uint8), 'RGB')
    im.save(path, 'JPEG', quality=92, optimize=True, subsampling=0)
    meta = path + '.meta'
    if not os.path.exists(meta):
        with open(meta, 'w', encoding='utf-8', newline='\n') as f:
            f.write(META.format(guid=uuid.uuid4().hex))


def main():
    src = sys.argv[1]
    solo = set(sys.argv[2:])
    borrador = 'borrador' in os.path.basename(os.path.normpath(src))
    if not borrador:
        os.makedirs(OUT, exist_ok=True)
        # .meta de las carpetas nuevas (Assets/Art y Assets/Art/Comics), con GUID fijo desde la primera vez
        for folder in (os.path.dirname(OUT), OUT):
            folder_meta = folder + '.meta'
            if not os.path.exists(folder_meta):
                with open(folder_meta, 'w', encoding='utf-8', newline='\n') as f:
                    f.write("fileFormatVersion: 2\nguid: %s\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n"
                            "  userData: \n  assetBundleName: \n  assetBundleVariant: \n" % uuid.uuid4().hex)
    for i, (vid, comic, amb, texto) in enumerate(VINETAS):
        if solo and vid not in solo:
            continue
        base = os.path.join(src, vid)
        if not os.path.exists(base + '_luz.png'):
            continue
        arr = boceto(base, amb, 1000 + i)
        if borrador:
            Image.fromarray((arr * 255 + 0.5).astype(np.uint8), 'RGB').save(os.path.join(src, vid + '_boceto.png'))
        else:
            guardar(arr, os.path.join(OUT, vid + '.jpg'))
        print('BOCETO', vid, flush=True)


if __name__ == '__main__':
    main()
