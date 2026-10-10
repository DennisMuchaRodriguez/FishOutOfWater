# -*- coding: utf-8 -*-
"""
Genera las texturas pintadas del terreno (estilo arcilla / pintado a mano).

Uso:
    python generar_texturas_terreno.py [carpeta_salida] [--res 1024]

Por defecto escribe en Assets/Terreno/Texturas/ (relativo a la raíz del proyecto).
Solo necesita numpy y Pillow. Todas las texturas son repetibles (sin costuras):
el ruido se genera en el dominio de Fourier, que es periódico por naturaleza.

Cada capa produce:
    T_<Nombre>.png         color (RGB) + alfa = suavidad (casi 0, mate)
    T_<Nombre>_Normal.png  mapa de normales suave (convención OpenGL, verde = +V)

Los .meta (GUID e importación) ya existen en el proyecto: al regenerar solo
se sustituyen los PNG y Unity los reimporta.
"""
import os
import sys
import numpy as np
from PIL import Image

RAIZ = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..'))
SALIDA = os.path.join(RAIZ, 'Assets', 'Terreno', 'Texturas')
N = 1024
SUAVIDAD = 0.08  # alfa del color: suavidad muy baja (aspecto mate de arcilla)


# ----------------------------------------------------------------------------
#  Ruido periódico
# ----------------------------------------------------------------------------
def _freqs(n):
    f = np.fft.fftfreq(n) * n  # ciclos por textura
    return f[None, :], f[:, None]


def _norm(a):
    a = a - a.mean()
    s = a.std()
    return a / s if s > 1e-9 else a


def ruido(rng, ciclos, n=None):
    """Ruido suave repetible: ~'ciclos' manchas por lado. Media 0, desviación 1."""
    n = n or N
    fx, fy = _freqs(n)
    w = np.fft.fft2(rng.standard_normal((n, n)))
    g = np.exp(-(fx ** 2 + fy ** 2) / (2.0 * ciclos ** 2))
    g[0, 0] = 0.0
    return _norm(np.real(np.fft.ifft2(w * g)))


def ruido_dir(rng, ciclos, angulo, alargado=5.0, n=None):
    """Ruido alargado en una dirección (trazos de pincel)."""
    n = n or N
    fx, fy = _freqs(n)
    c, s = np.cos(angulo), np.sin(angulo)
    u = fx * c + fy * s          # a lo largo del trazo
    v = -fx * s + fy * c         # a lo ancho
    g = np.exp(-(u ** 2) / (2.0 * (ciclos / alargado) ** 2) - (v ** 2) / (2.0 * ciclos ** 2))
    g[0, 0] = 0.0
    w = np.fft.fft2(rng.standard_normal((n, n)))
    return _norm(np.real(np.fft.ifft2(w * g)))


def trazos(rng, ciclos, alargado=5.0, direcciones=6, campo_ciclos=3.0):
    """Trazos de pincel cuya orientación cambia suavemente por la textura."""
    campo = ruido(rng, campo_ciclos)
    ang = (np.arctan(campo) / np.pi + 0.5) * np.pi  # 0..pi
    out = np.zeros((N, N))
    peso = np.zeros((N, N))
    for i in range(direcciones):
        a = np.pi * i / direcciones
        d = np.abs(((ang - a + np.pi / 2) % np.pi) - np.pi / 2)
        w = np.exp(-(d / (np.pi / direcciones * 0.6)) ** 2)
        out += w * ruido_dir(rng, ciclos, a, alargado)
        peso += w
    return _norm(out / np.maximum(peso, 1e-6))


def fbm(rng, ciclos, octavas=3, ganancia=0.5):
    a, amp, tot = np.zeros((N, N)), 1.0, 0.0
    for o in range(octavas):
        a += amp * ruido(rng, ciclos * (2 ** o))
        tot += amp
        amp *= ganancia
    return _norm(a / tot)


def a01(x, k=1.0):
    """De ruido (media 0) a 0..1 con una curva suave."""
    return 1.0 / (1.0 + np.exp(-k * x))


def smooth(e0, e1, x):
    t = np.clip((x - e0) / (e1 - e0), 0.0, 1.0)
    return t * t * (3 - 2 * t)


def cuantizar_suave(t, niveles, dureza=0.22):
    """Zonas planas de color con bordes suaves (manchas de pintura)."""
    s = np.clip(t, 0, 0.9999) * niveles
    f = s - np.floor(s)
    f = smooth(0.5 - dureza, 0.5 + dureza, f)
    return (np.floor(s) + f) / niveles


def rampa(t, colores):
    """Interpola una lista de colores (0..255) según t en 0..1."""
    cols = np.array(colores, dtype=np.float64) / 255.0
    t = np.clip(t, 0, 1) * (len(cols) - 1)
    i0 = np.floor(t).astype(int).clip(0, len(cols) - 2)
    f = (t - i0)[..., None]
    return cols[i0] * (1 - f) + cols[i0 + 1] * f


def motas(rng, densidad, radio_px, n=None):
    """Puntitos repetibles (salpicado de pincel). Devuelve 0..1."""
    n = n or N
    blanco = rng.random((n, n)) < densidad
    fx, fy = _freqs(n)
    g = np.exp(-(fx ** 2 + fy ** 2) * (np.pi * radio_px / n) ** 2 * 2)
    m = np.real(np.fft.ifft2(np.fft.fft2(blanco.astype(np.float64)) * g))
    return np.clip(m / (m.max() + 1e-9) * 1.6, 0, 1)


def voronoi(rng, puntos, deformar=0.0, semilla_def=None):
    """Celdas repetibles. Devuelve (F1, F2, id de celda) en unidades de textura (0..1)."""
    p = rng.random((puntos, 2))
    ys, xs = np.mgrid[0:N, 0:N] / N
    if deformar > 0:
        r2 = np.random.default_rng(semilla_def)
        xs = xs + deformar * ruido(r2, 4) * 0.02
        ys = ys + deformar * ruido(r2, 4) * 0.02
    F1 = np.full((N, N), 9.0)
    F2 = np.full((N, N), 9.0)
    ID = np.zeros((N, N), int)
    for k in range(puntos):
        dx = xs - p[k, 0]
        dy = ys - p[k, 1]
        dx -= np.round(dx)
        dy -= np.round(dy)
        d = np.sqrt(dx * dx + dy * dy)
        nuevo = d < F1
        F2 = np.where(nuevo, F1, np.minimum(F2, d))
        ID = np.where(nuevo, k, ID)
        F1 = np.where(nuevo, d, F1)
    return F1, F2, ID


def desenfocar(a, px):
    fx, fy = _freqs(a.shape[0])
    g = np.exp(-(fx ** 2 + fy ** 2) * (np.pi * px / a.shape[0]) ** 2 * 2)
    if a.ndim == 2:
        return np.real(np.fft.ifft2(np.fft.fft2(a) * g))
    return np.stack([np.real(np.fft.ifft2(np.fft.fft2(a[..., c]) * g)) for c in range(a.shape[2])], -1)


def normal_desde_altura(h, fuerza):
    """Normal tangente (OpenGL: +X derecha, +Y arriba en la imagen)."""
    dx = (np.roll(h, -1, 1) - np.roll(h, 1, 1)) * 0.5
    dy = (np.roll(h, 1, 0) - np.roll(h, -1, 0)) * 0.5  # fila 0 = arriba = +V
    n = np.dstack([-dx * fuerza, -dy * fuerza, np.ones_like(h)])
    n /= np.linalg.norm(n, axis=2, keepdims=True)
    return n * 0.5 + 0.5


def grano(rng, fuerza):
    return (rng.random((N, N)) - 0.5) * 2 * fuerza


def aplicar_valor(rgb, v):
    """Aclara/oscurece conservando el tono."""
    return np.clip(rgb * (1.0 + v[..., None]), 0, 1)


def calidez(rgb, t, color):
    c = np.array(color, dtype=np.float64) / 255.0
    return rgb * (1 - t[..., None]) + c * t[..., None]


# ----------------------------------------------------------------------------
#  Capas
# ----------------------------------------------------------------------------
def pasto(semilla, colores, acento, oscuro):
    rng = np.random.default_rng(semilla)
    t = 0.6 * ruido(rng, 2.0) + 0.45 * ruido(rng, 5.0) + 0.25 * ruido(rng, 13.0)
    t = cuantizar_suave(a01(_norm(t), 1.25), 4, 0.24)
    t = desenfocar(t, 2.0)
    rgb = rampa(t, colores)
    # trazos de hierba pintada (cortos, en varias direcciones)
    tr = trazos(rng, 34.0, alargado=2.0, direcciones=8, campo_ciclos=5.0)
    tr2 = trazos(rng, 100.0, alargado=2.6, direcciones=8, campo_ciclos=9.0)
    v = 0.045 * tr + 0.014 * tr2
    rgb = aplicar_valor(rgb, v)
    rgb = calidez(rgb, smooth(0.6, 1.8, tr) * 0.16, acento)
    # salpicado de pincel (motitas claras y oscuras)
    m1 = motas(rng, 0.0016, 2.4)
    m2 = motas(rng, 0.0012, 2.0)
    rgb = calidez(rgb, m1 * 0.35, acento)
    rgb = calidez(rgb, m2 * 0.25, oscuro)
    rgb = aplicar_valor(rgb, grano(rng, 0.022))
    altura = 0.6 * tr + 0.4 * tr2 + 0.8 * m1 - 0.3 * m2
    return rgb, altura, 1.2


def tierra(semilla):
    rng = np.random.default_rng(semilla)
    t = 0.6 * ruido(rng, 2.5) + 0.4 * ruido(rng, 7.0) + 0.2 * ruido(rng, 18.0)
    t = cuantizar_suave(a01(_norm(t), 1.3), 4, 0.25)
    t = desenfocar(t, 2.0)
    rgb = rampa(t, [(112, 76, 50), (138, 96, 62), (160, 116, 76), (178, 136, 90)])
    # piedrecitas redondeadas
    F1, F2, ID = voronoi(rng, 120, deformar=0.25, semilla_def=semilla + 1)
    cel = np.random.default_rng(semilla + 2).random(120)
    radio = 0.010 + 0.012 * cel[ID]
    piedra = smooth(radio, radio * 0.55, F1) * (cel[ID] > 0.45)
    cupula = np.sqrt(np.clip(1 - (F1 / radio) ** 2, 0, 1)) * piedra
    col_p = rampa(cel[ID], [(150, 128, 104), (176, 150, 120), (128, 104, 84)])
    luz = (1.0 + 0.18 * (cupula - 0.5))[..., None]
    rgb = rgb * (1 - piedra[..., None] * 0.85) + col_p * luz * piedra[..., None] * 0.85
    tr = trazos(rng, 36.0, alargado=2.2, direcciones=6, campo_ciclos=4.0)
    rgb = aplicar_valor(rgb, 0.035 * tr)
    m1 = motas(rng, 0.0015, 2.0)
    rgb = calidez(rgb, m1 * 0.25, (196, 160, 112))
    m2 = motas(rng, 0.0012, 1.6)
    rgb = calidez(rgb, m2 * 0.3, (86, 58, 40))
    rgb = aplicar_valor(rgb, grano(rng, 0.025))
    altura = 2.5 * cupula + 0.3 * tr - 0.4 * m2
    return rgb, altura, 1.6


def arena(semilla):
    rng = np.random.default_rng(semilla)
    t = 0.6 * ruido(rng, 2.0) + 0.4 * ruido(rng, 6.0)
    t = cuantizar_suave(a01(_norm(t), 1.2), 3, 0.25)
    t = desenfocar(t, 2.0)
    rgb = rampa(t, [(206, 174, 120), (222, 192, 138), (236, 210, 156)])
    # ondulaciones suaves de la arena (se repiten: frecuencias enteras)
    ys, xs = np.mgrid[0:N, 0:N] / N
    def_ = ruido(rng, 3.0) * 0.06
    ond = np.sin(2 * np.pi * (9 * xs + 4 * ys + def_ * 6)) * 0.5 + 0.5
    ond = smooth(0.35, 0.95, ond)
    rgb = aplicar_valor(rgb, 0.05 * (ond - 0.5))
    tr = trazos(rng, 40.0, alargado=2.0, direcciones=6, campo_ciclos=4.0)
    rgb = aplicar_valor(rgb, 0.018 * tr)
    m1 = motas(rng, 0.004, 1.4)
    rgb = calidez(rgb, m1 * 0.35, (168, 128, 86))
    m2 = motas(rng, 0.003, 1.4)
    rgb = calidez(rgb, m2 * 0.3, (248, 232, 196))
    rgb = aplicar_valor(rgb, grano(rng, 0.03))
    altura = 0.9 * ond + 0.25 * tr + 0.3 * m1
    return rgb, altura, 1.4


def lecho(semilla):
    rng = np.random.default_rng(semilla)
    t = 0.7 * ruido(rng, 1.6) + 0.4 * ruido(rng, 4.0) + 0.15 * ruido(rng, 10.0)
    t = cuantizar_suave(a01(_norm(t), 1.2), 3, 0.25)
    t = desenfocar(t, 3.0)
    # ocre -> oliva -> verde azulado (nunca gris)
    rgb = rampa(t, [(164, 132, 80), (156, 144, 88), (130, 138, 86), (108, 134, 108)])
    tinte = desenfocar(a01(ruido(rng, 1.5) + 0.3 * ruido(rng, 4.0), 1.3), 4.0)
    rgb = calidez(rgb, smooth(0.62, 0.92, tinte) * 0.22, (84, 140, 130))   # manchas turquesa
    rgb = calidez(rgb, smooth(0.62, 0.92, 1 - tinte) * 0.14, (176, 142, 84))  # manchas ocre
    # guijarros redondeados
    F1, F2, ID = voronoi(rng, 110, deformar=0.25, semilla_def=semilla + 3)
    cel = np.random.default_rng(semilla + 4).random(110)
    radio = 0.012 + 0.014 * cel[ID]
    piedra = smooth(radio, radio * 0.6, F1) * (cel[ID] > 0.6)
    cupula = np.sqrt(np.clip(1 - (F1 / radio) ** 2, 0, 1)) * piedra
    col_p = rampa(cel[ID], [(132, 128, 96), (150, 132, 98), (110, 122, 104)])
    rgb = rgb * (1 - piedra[..., None] * 0.7) + col_p * (1 + 0.15 * (cupula - 0.5))[..., None] * piedra[..., None] * 0.7
    ys, xs = np.mgrid[0:N, 0:N] / N
    ond = np.sin(2 * np.pi * (5 * xs - 7 * ys + ruido(rng, 2.5) * 0.4)) * 0.5 + 0.5
    rgb = aplicar_valor(rgb, 0.035 * (ond - 0.5))
    tr = trazos(rng, 36.0, alargado=2.2, direcciones=6, campo_ciclos=4.0)
    rgb = aplicar_valor(rgb, 0.025 * tr)
    m1 = motas(rng, 0.002, 1.6)
    rgb = calidez(rgb, m1 * 0.3, (196, 176, 120))
    rgb = aplicar_valor(rgb, grano(rng, 0.025))
    altura = 2.0 * cupula + 0.6 * ond + 0.25 * tr
    return rgb, altura, 1.3


def roca(semilla):
    rng = np.random.default_rng(semilla)
    # placas grandes y redondeadas con grietas sueltas (no un muro de adoquines)
    P = 12
    F1, F2, ID = voronoi(rng, P, deformar=1.0, semilla_def=semilla + 5)
    cel = np.random.default_rng(semilla + 6).random((P, 3))
    borde = desenfocar(F2 - F1, 2.0)
    cupula = smooth(0.0, 0.16, borde) ** 0.6
    visible = smooth(0.35, 0.65, a01(ruido(rng, 3.0), 1.5))      # grietas a trozos
    grieta = 1 - (1 - smooth(0.003, 0.02, borde)) * visible
    t = 0.6 * ruido(rng, 1.6) + 0.4 * ruido(rng, 4.5) + 0.8 * (cel[ID, 0] - 0.5)
    t = cuantizar_suave(a01(_norm(desenfocar(t, 6.0)), 1.2), 3, 0.25)
    rgb = rampa(t, [(116, 122, 140), (128, 132, 146), (140, 139, 146), (152, 146, 142)])
    # luz pintada: arriba-izquierda más clara, según la forma de la placa
    gy, gx = np.gradient(desenfocar(cupula, 3.0))
    luz = np.clip(0.5 + (-gx + gy) * 16.0, 0, 1)
    rgb = rgb * (0.88 + 0.20 * luz[..., None])
    rgb = calidez(rgb, (1 - grieta) * 0.5, (82, 88, 110))            # grietas azuladas
    # estratos: pinceladas largas casi horizontales
    est = ruido_dir(rng, 22.0, 0.12, alargado=4.0)
    rgb = aplicar_valor(rgb, 0.04 * est)
    rgb = calidez(rgb, smooth(0.8, 2.0, est) * 0.12, (172, 160, 146))
    # pinceladas redondeadas
    tr = trazos(rng, 30.0, alargado=1.6, direcciones=6, campo_ciclos=3.0)
    tr2 = trazos(rng, 90.0, alargado=2.0, direcciones=6, campo_ciclos=6.0)
    rgb = aplicar_valor(rgb, 0.035 * tr + 0.012 * tr2)
    m1 = motas(rng, 0.0012, 2.2)
    rgb = calidez(rgb, m1 * 0.2, (186, 182, 176))
    rgb = aplicar_valor(rgb, grano(rng, 0.02))
    altura = 2.2 * cupula + 1.0 * grieta + 0.35 * est + 0.2 * tr
    return rgb, altura, 2.2


CAPAS = {
    'Pasto': lambda: pasto(11, [(80, 132, 48), (94, 146, 54), (108, 158, 60), (124, 170, 66)],
                           (170, 192, 84), (52, 96, 40)),
    'PastoVariante': lambda: pasto(23, [(98, 124, 42), (116, 136, 48), (134, 148, 56), (150, 158, 66)],
                                   (190, 178, 86), (66, 92, 38)),
    'Tierra': lambda: tierra(37),
    'Arena': lambda: arena(41),
    'Lecho': lambda: lecho(53),
    'Roca': lambda: roca(67),
}


def guardar(nombre, rgb, altura, fuerza, carpeta):
    a = np.full(rgb.shape[:2] + (1,), SUAVIDAD)
    img = np.concatenate([np.clip(rgb, 0, 1), a], axis=2)
    Image.fromarray((img * 255 + 0.5).astype(np.uint8), 'RGBA').save(os.path.join(carpeta, 'T_%s.png' % nombre), optimize=True)
    h = _norm(altura) * 0.5
    nrm = normal_desde_altura(desenfocar(h, 1.0), fuerza)
    Image.fromarray((nrm * 255 + 0.5).astype(np.uint8), 'RGB').save(os.path.join(carpeta, 'T_%s_Normal.png' % nombre), optimize=True)


def main():
    global N
    args = [a for a in sys.argv[1:]]
    carpeta = SALIDA
    if '--res' in args:
        i = args.index('--res')
        N = int(args[i + 1])
        del args[i:i + 2]
    if args:
        carpeta = os.path.abspath(args[0])
    os.makedirs(carpeta, exist_ok=True)
    for nombre, fn in CAPAS.items():
        rgb, altura, fuerza = fn()
        guardar(nombre, rgb, altura, fuerza, carpeta)
        print('ok', nombre, 'color medio', (rgb.reshape(-1, 3).mean(0) * 255).round().astype(int))


if __name__ == '__main__':
    main()
