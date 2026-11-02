# -*- coding: utf-8 -*-
"""Genera los sonidos del juego (Assets/Resources/Sonidos/*.wav) con síntesis, sin archivos externos.

    python Herramientas/Sonido/generar_sonidos.py            # todos
    python Herramientas/Sonido/generar_sonidos.py Disparo    # solo los que empiezan con ese nombre

Cada sonido es una función de abajo. Las variantes (<Nombre>_2, _3...) se eligen al azar en el juego.
Para usar un sonido propio basta con reemplazar el .wav (mismo nombre); este script lo volvería a pisar.
Los .meta se crean una sola vez (GUID estable) y con 'normalize' apagado: el volumen relativo es el de aquí.
"""
import os, sys, uuid
import numpy as np
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from sintesis import *

REPO = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..'))
OUT = os.path.join(REPO, 'Assets', 'Resources', 'Sonidos')

META = """fileFormatVersion: 2
guid: {guid}
AudioImporter:
  externalObjects: {{}}
  serializedVersion: 7
  defaultSettings:
    serializedVersion: 2
    loadType: {load}
    sampleRateSetting: 0
    sampleRateOverride: 44100
    compressionFormat: 1
    quality: 0.7
    conversionMode: 0
    preloadAudioData: 1
  platformSettingOverrides: {{}}
  forceToMono: 1
  normalize: 0
  loadInBackground: 0
  ambisonic: 0
  3D: 1
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""

RECIPES = []


def sound(name, peak=-1.0, variants=1, loop=False):
    def deco(fn):
        RECIPES.append((name, fn, peak, variants, loop))
        return fn
    return deco


def rng(seed):
    return np.random.default_rng(seed)


# ======================= EL TRAJE =======================

@sound('Disparo', peak=-3, variants=3)
def disparo(v):
    r = rng(10 + v)
    k = 1 + (v - 1) * 0.06
    chirp = sine(sweep(1500 * k, 420 * k, 0.09, 0.6), 0.09) * env_exp(0.09, 0.035)
    squirt = bp(noise(0.16, r), 1200, 6000) * env_exp(0.16, 0.045) * 0.6
    bloop = sine(sweep(700 * k, 260, 0.07), 0.07) * env_exp(0.07, 0.03) * 0.5
    return mixdown(chirp, squirt, place(silence(0.02), bloop, 0.02))


@sound('DisparoCargado', peak=-1)
def disparo_cargado(v):
    r = rng(20)
    thump = sine(sweep(140, 45, 0.35), 0.35) * env_exp(0.35, 0.12)
    whoosh = moving_filter(noise(0.6, r), sweep(7000, 600, 0.6), 'lp') * env_ad(0.6, 0.01, curve=3) * 0.7
    zap = saw(sweep(2400, 380, 0.25, 0.7), 0.25) * env_exp(0.25, 0.08) * 0.35
    return reverb(mixdown(thump, whoosh, zap), 0.2, 0.7)


@sound('Burbuja', peak=-6, variants=4)
def burbuja(v):
    f = [520, 610, 700, 820][v - 1]
    pop = sine(sweep(f, f * 2.6, 0.05, 2), 0.05) * env_exp(0.05, 0.018)
    click = hp(noise(0.006, rng(30 + v)), 3000) * 0.25
    return mixdown(pop, click)


@sound('SinMunicion', peak=-8)
def sin_municion(v):
    r = rng(40)
    c = hp(noise(0.004, r), 2500) * 0.9
    ring = sine(2600, 0.04) * env_exp(0.04, 0.008) * 0.3
    one = mixdown(c, ring)
    return place(one, one * 0.7, 0.06)


@sound('Capsula', peak=-5)
def capsula(v):
    a = bellnote(1318.5, 0.3, 0.18)
    b = bellnote(1975.5, 0.35, 0.2)
    sparkle = hp(noise(0.25, rng(50)), 5000) * env_exp(0.25, 0.06) * 0.15
    return mixdown(a, place(silence(0.08), b, 0.08), sparkle)


@sound('Acierto', peak=-10)
def acierto(v):
    return mixdown(hp(noise(0.004, rng(60)), 3000) * 0.6, sine(1900, 0.045) * env_exp(0.045, 0.012))


@sound('Derribo', peak=-6)
def derribo(v):
    out = silence(0.4)
    for i, f in enumerate((880, 1174.7, 1568)):
        note = (square(f, 0.12, 5) * 0.5 + sine(f, 0.12)) * env_exp(0.12, 0.05)
        out = place(out, note, i * 0.065)
    return out


@sound('GolpeTraje', peak=-2, variants=2)
def golpe_traje(v):
    r = rng(70 + v)
    k = 1 + (v - 1) * 0.08
    clank = metal([410 * k, 1135 * k, 2210 * k, 3460 * k, 5100 * k], 0.5, 0.11, r)
    thud = sine(sweep(160, 60, 0.2), 0.2) * env_exp(0.2, 0.06)
    crunch = bp(noise(0.08, r), 800, 5000) * env_exp(0.08, 0.02) * 0.8
    return reverb(mixdown(clank * 0.7, thud, crunch), 0.15, 0.5)


@sound('AlarmaTraje', peak=-6)
def alarma(v):
    a = square(880, 0.11, 6) * env_ad(0.11, 0.005, curve=1.2)
    b = square(660, 0.13, 6) * env_ad(0.13, 0.005, curve=1.5)
    return lp(place(a, b, 0.12), 4000)


@sound('Propulsor', peak=-4, loop=True)
def propulsor(v):
    r = rng(80)
    sec = 2.4
    hiss = bp(noise(sec, r), 350, 3000)
    tt = t(sec)
    mod = 1 + 0.15 * np.sin(2 * np.pi * 6.0 * tt) + 0.08 * np.sin(2 * np.pi * 13.7 * tt)
    rumble = lp(brown(sec, r), 180) * 2.5
    whine = sine(420 + 12 * np.sin(2 * np.pi * 0.8 * tt), sec) * 0.06
    return loopable(hiss * mod * 0.5 + rumble + whine, 0.4)


@sound('Turbo', peak=-2)
def turbo(v):
    r = rng(90)
    whoosh = moving_filter(noise(0.7, r), sweep(500, 7000, 0.7, 0.7), 'lp') * bell(0.7, 0.35, 0.3)
    whine = saw(sweep(300, 1400, 0.7, 0.8), 0.7, 10) * env_ad(0.7, 0.05, curve=2) * 0.2
    pop = lp(noise(0.05, r), 1500) * env_exp(0.05, 0.015)
    return mixdown(pop, whoosh, whine)


@sound('Dash', peak=-3)
def dash(v):
    r = rng(100)
    sec = 0.35
    fc = np.concatenate([sweep(900, 3500, sec * 0.5), sweep(3500, 900, sec * 0.5)])
    return moving_filter(noise(sec, r), fc[:n(sec)], 'band') * bell(sec, 0.45, 0.28) * 1.4


@sound('Muerte', peak=-1)
def muerte(v):
    r = rng(110)
    down = saw(sweep(620, 70, 1.4, 1.4), 1.4, 12) * env_ad(1.4, 0.01, curve=1.5) * 0.6
    static = bp(noise(1.4, r), 1500, 6000) * env_ad(1.4, 0.01, curve=2.5) * 0.35
    sad = mixdown(sine(523.3, 0.4) * env_exp(0.4, 0.2), place(silence(0.3), sine(392, 0.6) * env_exp(0.6, 0.3), 0.3))
    return reverb(mixdown(lp(down, 2500), static, sad * 0.4), 0.25, 1.0)


@sound('Misil', peak=-3)
def misil(v):
    r = rng(120)
    pop = lp(noise(0.04, r), 2500) * env_exp(0.04, 0.012)
    hiss = moving_filter(noise(0.7, r), sweep(1200, 5000, 0.7), 'lp') * env_ad(0.7, 0.03, curve=2.2) * 0.8
    return mixdown(pop, hiss)


@sound('Explosion', peak=-1, variants=2)
def explosion(v):
    r = rng(130 + v)
    boom = sine(sweep(90, 35, 0.9), 0.9) * env_exp(0.9, 0.25)
    crash = moving_filter(noise(1.1, r), sweep(5000, 400, 1.1, 0.5), 'lp') * env_exp(1.1, 0.25)
    return reverb(mixdown(boom * 1.2, crash), 0.25, 1.1)


@sound('Escudo', peak=-2)
def escudo(v):
    r = rng(140)
    bub = sine(sweep(300, 1300, 0.12, 1.5), 0.12) * env_exp(0.12, 0.05)
    splash = bp(noise(0.35, r), 900, 7000) * env_exp(0.35, 0.08) * 0.6
    shimmer = mixdown(*[bellnote(f, 0.4, 0.15) * 0.25 for f in (1046, 1568)])
    return mixdown(bub, splash, shimmer)


@sound('EscudoListo', peak=-6)
def escudo_listo(v):
    out = silence(0.6)
    for i, f in enumerate((784, 1046.5, 1568)):
        out = place(out, bellnote(f, 0.4, 0.2) * 0.6, i * 0.07)
    return out


# ======================= AGUA =======================

def drops(sec, count, r, fmin=500, fmax=1600, amp=0.35):
    out = silence(sec)
    for i in range(count):
        f = r.uniform(fmin, fmax)
        d = sine(sweep(f, f * r.uniform(1.6, 2.6), 0.04, 2), 0.04) * env_exp(0.04, 0.012) * amp * r.uniform(0.5, 1)
        out = place(out, d, r.uniform(0.02, sec * 0.8))
    return out


@sound('Chapuzon', peak=-2, variants=3)
def chapuzon(v):
    r = rng(150 + v)
    body = bp(noise(0.5, r), 300, 7000) * env_ad(0.5, 0.006, curve=4.5)
    plunk = sine(sweep(260, 110, 0.18), 0.18) * env_exp(0.18, 0.06) * 0.8
    return mixdown(body, plunk, drops(0.6, 7, r))


@sound('SalidaAgua', peak=-5, variants=2)
def salida_agua(v):
    r = rng(160 + v)
    body = bp(noise(0.3, r), 800, 6000) * env_ad(0.3, 0.01, curve=4) * 0.6
    return mixdown(body, drops(0.45, 6, r, 700, 1800, 0.4))


@sound('AmbienteLago', peak=-6, loop=True)
def ambiente_lago(v):
    r = rng(170)
    sec = 9.0
    tt = t(sec)
    wind = lp(brown(sec, r), 700) * (0.6 + 0.4 * np.sin(2 * np.pi * tt / 4.5) ** 2)
    lap = silence(sec)
    for i in range(10):
        w = bp(noise(0.7, r), 200, 1600) * bell(0.7, 0.35, 0.25) * r.uniform(0.15, 0.35)
        lap = place(lap, w, r.uniform(0, sec - 0.8))
    leaves = hp(noise(sec, r), 3500) * 0.03 * (0.5 + 0.5 * np.sin(2 * np.pi * tt / 3.0))
    return loopable(mixdown(wind * 1.4, lap[:n(sec)], leaves), 0.8)


@sound('AmbienteSubacuatico', peak=-5, loop=True)
def ambiente_subacuatico(v):
    r = rng(180)
    sec = 7.0
    tt = t(sec)
    rumble = lp(brown(sec, r), 160) * 1.6
    hum = sine(58 + 2 * np.sin(2 * np.pi * 0.1 * tt), sec) * 0.08
    bub = silence(sec)
    for i in range(14):
        f = r.uniform(180, 420)
        b = sine(sweep(f, f * 1.8, 0.06, 2), 0.06) * env_exp(0.06, 0.02) * r.uniform(0.08, 0.2)
        bub = place(bub, b, r.uniform(0, sec - 0.1))
    return loopable(mixdown(rumble, hum, lp(bub, 1500)[:n(sec)]), 0.8)


# ======================= AVES =======================

def squawk(contour, sec, r, rough=0.35, formant=1400, bright=1.0):
    f = glide(contour, sec)
    vib = 1 + 0.02 * np.sin(2 * np.pi * 28 * t(sec))
    s = saw(f * vib, sec, 18)
    s *= 1 + rough * lp(noise(sec, r), 120)          # aspereza
    s = peak(s, formant, 3, 3) * bright
    breath = bp(noise(sec, r), 1500, 5000) * 0.08
    return (s + breath) * bell(sec, 0.35, 0.4)


@sound('Graznido', peak=-3, variants=3)
def graznido(v):
    r = rng(190 + v)
    contours = [[(0, 650), (0.3, 900), (1, 520)], [(0, 800), (0.5, 760), (1, 560)], [(0, 560), (0.2, 820), (0.6, 700), (1, 480)]]
    sec = [0.32, 0.26, 0.38][v - 1]
    return lp(squawk(contours[v - 1], sec, r, 0.45, 1300), 6000)


@sound('Gaviota', peak=-3, variants=2)
def gaviota(v):
    r = rng(200 + v)
    a = squawk([(0, 1100), (0.25, 1750), (1, 950)], 0.28, r, 0.25, 2200)
    b = squawk([(0, 1050), (0.3, 1650), (1, 900)], 0.22, r, 0.25, 2100)
    out = place(a, b * 0.8, 0.32)
    if v == 2:
        out = place(out, squawk([(0, 1000), (0.3, 1500), (1, 850)], 0.18, r, 0.25, 2000) * 0.6, 0.6)
    return lp(out, 7000)


@sound('GolpeAve', peak=-3, variants=2)
def golpe_ave(v):
    r = rng(210 + v)
    thump = sine(sweep(150 + 20 * v, 55, 0.14), 0.14) * env_exp(0.14, 0.04)
    rustle = hp(noise(0.22, r), 2200) * env_exp(0.22, 0.05) * 0.45
    smack = bp(noise(0.03, r), 600, 3500) * env_exp(0.03, 0.008) * 0.8
    return mixdown(thump, rustle, smack)


@sound('GolpeBlindado', peak=-3)
def golpe_blindado(v):
    r = rng(220)
    ping = metal([1820, 2910, 4330], 0.3, 0.06, r)
    thud = sine(sweep(120, 60, 0.12), 0.12) * env_exp(0.12, 0.04)
    return mixdown(ping * 0.6, thud)


@sound('MuerteAve', peak=-2, variants=2)
def muerte_ave(v):
    r = rng(230 + v)
    cry = squawk([(0, 900), (0.2, 1100), (1, 380)], 0.45, r, 0.55, 1200)
    poof = moving_filter(noise(0.5, r), sweep(4000, 500, 0.5), 'lp') * env_exp(0.5, 0.12) * 0.6
    rustle = hp(noise(0.6, r), 2500) * env_exp(0.6, 0.15) * 0.3
    return mixdown(cry, place(silence(0.1), poof, 0.1), rustle)


@sound('Embestida', peak=-3)
def embestida(v):
    r = rng(240)
    sec = 0.32
    fc = np.concatenate([sweep(1200, 5000, sec * 0.4), sweep(5000, 1500, sec * 0.6)])[:n(sec)]
    return moving_filter(noise(sec, r), fc, 'band') * bell(sec, 0.35, 0.25) * 1.5


@sound('Escupitajo', peak=-3)
def escupitajo(v):
    r = rng(250)
    pop = lp(noise(0.012, r), 2500) * 1.2
    squirt = bp(noise(0.18, r), 1000, 4500) * env_exp(0.18, 0.05)
    tone = sine(sweep(500, 220, 0.12), 0.12) * env_exp(0.12, 0.04) * 0.5
    return mixdown(pop, place(silence(0.01), squirt, 0.01), tone)


@sound('Caca', peak=-5)
def caca(v):
    r = rng(260)
    pff = bp(noise(0.08, r), 400, 2500) * env_exp(0.08, 0.03)
    whistle = sine(sweep(1300, 600, 0.35), 0.35) * env_ad(0.35, 0.03, curve=1.5) * 0.35
    return mixdown(pff, place(silence(0.05), whistle, 0.05))


@sound('Plaf', peak=-2)
def plaf(v):
    r = rng(270)
    splat = lp(noise(0.25, r), 2200) * env_exp(0.25, 0.05)
    squelch = sine(sweep(240, 90, 0.2) * (1 + 0.3 * np.sin(2 * np.pi * 35 * t(0.2))), 0.2) * env_exp(0.2, 0.07) * 0.7
    return mixdown(splat, squelch)


@sound('Plumas', peak=-3)
def plumas(v):
    r = rng(280)
    out = silence(0.45)
    for i in range(5):
        sec = 0.12
        fc = sweep(2500, 7000, sec)
        w = moving_filter(noise(sec, r), fc, 'band') * bell(sec, 0.3, 0.25)
        out = place(out, w * r.uniform(0.6, 1), i * 0.045 + r.uniform(0, 0.01))
    return out


@sound('Picada', peak=-2)
def picada(v):
    r = rng(290)
    sec = 0.9
    tt = t(sec)
    scream = sine(sweep(2300, 850, sec, 1.3) * (1 + 0.012 * np.sin(2 * np.pi * 9 * tt)), sec) * env_ad(sec, 0.05, curve=1.2) * 0.6
    wind = moving_filter(noise(sec, r), sweep(800, 4500, sec), 'lp') * np.linspace(0.2, 1, n(sec)) * 0.7
    return mixdown(scream, wind)


# ======================= JEFES =======================

@sound('RugidoJefe', peak=-1)
def rugido(v):
    r = rng(300)
    sec = 1.6
    tt = t(sec)
    f = glide([(0, 95), (0.25, 140), (0.7, 120), (1, 70)], sec)
    growl = 1 + 0.45 * np.sin(2 * np.pi * 31 * tt) * (0.6 + 0.4 * np.sin(2 * np.pi * 3 * tt))
    body = saw(f, sec, 30) * growl
    body = peak(peak(body, 650, 3, 4), 1300, 4, 2.5)
    rasp = bp(noise(sec, r), 600, 3500) * 0.25 * growl
    return reverb(lp(mixdown(body, rasp), 5000) * bell(sec, 0.35, 0.45), 0.3, 1.4, 2500)


@sound('Pisoton', peak=-1)
def pisoton(v):
    r = rng(310)
    sub = sine(sweep(70, 32, 1.2), 1.2) * env_exp(1.2, 0.35) * 1.4
    crash = moving_filter(noise(1.4, r), sweep(6000, 300, 1.4, 0.4), 'lp') * env_exp(1.4, 0.3)
    return reverb(mixdown(sub, crash, drops(1.4, 14, r, 400, 1400, 0.4)), 0.25, 1.3)


@sound('Remolino', peak=-3, loop=True)
def remolino(v):
    r = rng(320)
    sec = 3.2
    tt = t(sec)
    fc = 700 + 500 * np.sin(2 * np.pi * tt / 1.6)
    swirl = moving_filter(noise(sec, r), fc, 'band') * 1.6
    rumble = lp(brown(sec, r), 220) * 1.2
    gurgle = drops(sec, 16, r, 200, 600, 0.3)
    return loopable(mixdown(swirl, rumble, gurgle[:n(sec)]), 0.4)


@sound('OndaSubacuatica', peak=-1)
def onda(v):
    r = rng(330)
    boom = sine(sweep(85, 30, 1.3), 1.3) * env_exp(1.3, 0.4) * 1.5
    rumble = lp(noise(1.6, r), 300) * env_exp(1.6, 0.4)
    return reverb(lp(mixdown(boom, rumble, drops(1.6, 18, r, 150, 500, 0.4)), 1800), 0.35, 1.5, 1500)


@sound('Vendaval', peak=-1)
def vendaval(v):
    r = rng(340)
    sec = 1.6
    tt = t(sec)
    fc = glide([(0, 300), (0.35, 3200), (1, 500)], sec)
    gust = moving_filter(noise(sec, r), fc, 'lp') * bell(sec, 0.38, 0.32) * 1.5
    flaps = 1 + 0.9 * (0.5 + 0.5 * np.sin(2 * np.pi * 7 * tt)) ** 3 * np.exp(-tt / 0.45)   # aletazos al principio
    return gust * flaps


@sound('Estocada', peak=-2)
def estocada(v):
    r = rng(350)
    sec = 0.25
    whoosh = moving_filter(noise(sec, r), sweep(1500, 6000, sec), 'band') * bell(sec, 0.4, 0.3)
    clack = mixdown(bp(noise(0.03, r), 1500, 4000) * env_exp(0.03, 0.006), sine(1100, 0.05) * env_exp(0.05, 0.01) * 0.5)
    return place(whoosh, clack, 0.2)


# ======================= PECES Y PARTIDA =======================

@sound('PezAtrapado', peak=-4)
def pez_atrapado(v):
    out = silence(0.6)
    for i, (f1, f2) in enumerate(((1046.5, 784), (1046.5, 784))):
        a = (square(f1, 0.1, 5) * 0.4 + sine(f1, 0.1)) * env_ad(0.1, 0.004, curve=1.5)
        b = (square(f2, 0.12, 5) * 0.4 + sine(f2, 0.12)) * env_ad(0.12, 0.004, curve=2)
        out = place(out, a, i * 0.26)
        out = place(out, b, i * 0.26 + 0.1)
    return lp(out, 5000)


@sound('Trago', peak=-3)
def trago(v):
    r = rng(360)
    f = sweep(320, 110, 0.28)
    g = sine(f, 0.28) * env_ad(0.28, 0.02, curve=2) + saw(f, 0.28, 6) * 0.2 * env_ad(0.28, 0.02, curve=3)
    return mixdown(peak(g, 500, 3, 3), lp(noise(0.06, r), 800) * 0.3)


@sound('PezRescatado', peak=-4)
def pez_rescatado(v):
    out = silence(0.8)
    for i, f in enumerate((523.3, 659.3, 784, 1046.5)):
        out = place(out, bellnote(f, 0.45, 0.25) * 0.7, i * 0.08)
    return out


@sound('Oleada', peak=-2)
def oleada(v):
    out = silence(1.6)
    for i, f in enumerate((220, 165)):
        horn = saw(f * (1 + 0.004 * np.sin(2 * np.pi * 5 * t(0.6))), 0.6, 20)
        horn = lp(peak(horn, 800, 3, 2), 2200) * env_ad(0.6, 0.05, curve=1.2)
        out = place(out, horn, i * 0.55)
    return reverb(out, 0.25, 1.0, 2500)


@sound('Victoria', peak=-2)
def victoria(v):
    out = silence(2.0)
    notes = [(523.3, 0.0), (659.3, 0.12), (784, 0.24), (1046.5, 0.36)]
    for f, at in notes:
        tone = (square(f, 0.25, 6) * 0.3 + sine(f, 0.25)) * env_ad(0.25, 0.01, curve=2)
        out = place(out, tone, at)
    chord = mixdown(*[(sine(f, 1.2) + square(f, 1.2, 4) * 0.15) * env_ad(1.2, 0.02, curve=2.2) for f in (523.3, 659.3, 784, 1046.5)])
    out = place(out, chord * 0.5, 0.52)
    return reverb(lp(out, 6000), 0.2, 1.0)


@sound('Derrota', peak=-2)
def derrota(v):
    out = silence(2.0)
    for i, f in enumerate((392, 349.2, 311.1, 261.6)):
        tone = (tri(f, 0.4) + sine(f, 0.4) * 0.5) * env_ad(0.4, 0.02, curve=1.6)
        out = place(out, tone, i * 0.3)
    return reverb(lp(out, 3000), 0.25, 1.2)


@sound('Aviso', peak=-8)
def aviso(v):
    return sine(sweep(950, 1300, 0.12), 0.12) * env_ad(0.12, 0.005, curve=2)


# ======================= INTERFAZ =======================

@sound('UIPasar', peak=-14)
def ui_pasar(v):
    return mixdown(sine(2400, 0.025) * env_exp(0.025, 0.006), hp(noise(0.003, rng(370)), 4000) * 0.4)


@sound('UIClick', peak=-9)
def ui_click(v):
    return mixdown(hp(noise(0.004, rng(380)), 2000), sine(sweep(900, 600, 0.06), 0.06) * env_exp(0.06, 0.02) * 0.8)


@sound('Instalar', peak=-3)
def instalar(v):
    r = rng(390)
    sec = 1.5
    whirr = saw(sweep(90, 420, sec, 0.8), sec, 14) * env_ad(sec, 0.05, curve=0.8) * 0.4
    whirr = lp(whirr, 2500)
    clicks = silence(sec)
    k = 0.0
    step = 0.11
    while k < sec - 0.1:
        clicks = place(clicks, hp(noise(0.004, r), 2500) * 0.5, k)
        k += step
        step = max(0.035, step * 0.9)
    ding = bellnote(1568, 0.8, 0.35)
    out = place(mixdown(whirr, clicks[:n(sec)]), ding * 0.8, sec - 0.05)
    return reverb(out, 0.15, 0.8)


def main():
    only = sys.argv[1:]
    os.makedirs(OUT, exist_ok=True)
    folder_meta = OUT + '.meta'
    if not os.path.exists(folder_meta):
        with open(folder_meta, 'w', encoding='utf-8', newline='\n') as f:
            f.write("fileFormatVersion: 2\nguid: %s\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n"
                    "  userData: \n  assetBundleName: \n  assetBundleVariant: \n" % uuid.uuid4().hex)
    total = 0
    for name, fn, peak, variants, loop in RECIPES:
        if only and not any(name.startswith(o) for o in only):
            continue
        for v in range(1, variants + 1):
            fname = name if v == 1 else '%s_%d' % (name, v)
            path = os.path.join(OUT, fname + '.wav')
            x = fn(v)
            dur = write(path, x, peak, loop=loop)
            meta = path + '.meta'
            if not os.path.exists(meta):
                with open(meta, 'w', encoding='utf-8', newline='\n') as f:
                    f.write(META.format(guid=uuid.uuid4().hex, load=1 if loop or dur > 2 else 0))
            total += 1
            print('%-24s %.2f s' % (fname, dur))
    print('sonidos:', total)


if __name__ == '__main__':
    main()
