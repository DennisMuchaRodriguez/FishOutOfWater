# -*- coding: utf-8 -*-
"""Piezas de síntesis para los sonidos del juego (solo numpy). Las usa generar_sonidos.py."""
import math, wave
import numpy as np

SR = 44100
RNG = np.random.default_rng(7)


def n(sec):
    return max(1, int(round(sec * SR)))


def t(sec):
    return np.arange(n(sec)) / SR


def silence(sec):
    return np.zeros(n(sec))


def noise(sec, rng=None):
    rng = rng or RNG
    return rng.standard_normal(n(sec))


def brown(sec, rng=None):
    x = np.cumsum(noise(sec, rng))
    x -= np.linspace(x[0], x[-1], len(x))   # sin deriva: empieza y termina en 0
    return x / (np.abs(x).max() + 1e-9)


# ---------- envolventes ----------

def env_ad(sec, attack=0.005, decay=None, curve=4.0):
    """Ataque lineal y caída exponencial hasta el final."""
    N = n(sec)
    a = min(N, n(attack))
    e = np.ones(N)
    if a > 0:
        e[:a] = np.linspace(0, 1, a)
    d = N - a
    if d > 0:
        k = np.linspace(0, 1, d)
        e[a:] = np.exp(-curve * k) * (1 - k) ** 0.3
    return e


def env_exp(sec, tau, attack=0.002):
    """Caída exponencial que termina exactamente en 0 (si no, el corte del final suena como un clic)."""
    tt = t(sec)
    end = np.exp(-sec / tau)
    e = (np.exp(-tt / tau) - end) / max(1e-9, 1 - end)
    a = n(attack)
    if a > 0:
        e[:a] *= np.linspace(0, 1, a)
    return e


def bell(sec, peak=0.4, width=0.25):
    """Campana: sube y baja (centro en 'peak' fracción del largo)."""
    x = np.linspace(0, 1, n(sec))
    return np.exp(-((x - peak) / width) ** 2)


def fade(x, fin=0.003, fout=0.01):
    x = x.copy()
    a, b = n(fin), n(fout)
    if a > 0:
        x[:a] *= np.linspace(0, 1, a)
    if b > 0:
        x[-b:] *= np.linspace(1, 0, b)
    return x


# ---------- osciladores ----------

def phase(freq, sec):
    f = np.broadcast_to(np.asarray(freq, float), (n(sec),))
    return 2 * np.pi * np.cumsum(f) / SR


def sine(freq, sec):
    return np.sin(phase(freq, sec))


def tri(freq, sec):
    p = phase(freq, sec) / (2 * np.pi)
    return 2 * np.abs(2 * (p - np.floor(p + 0.5))) - 1


def saw(freq, sec, harmonics=None):
    """Diente de sierra suavizado (suma de armónicos) para que no suene áspero."""
    f = np.broadcast_to(np.asarray(freq, float), (n(sec),))
    ph = phase(f, sec)
    out = np.zeros(len(ph))
    top = harmonics or 24
    fmax = f.max()
    for k in range(1, top + 1):
        if k * fmax > SR * 0.45:
            break
        out += np.sin(k * ph) / k
    return out * 0.6


def square(freq, sec, harmonics=15):
    f = np.broadcast_to(np.asarray(freq, float), (n(sec),))
    ph = phase(f, sec)
    out = np.zeros(len(ph))
    for k in range(1, harmonics * 2, 2):
        if k * f.max() > SR * 0.45:
            break
        out += np.sin(k * ph) / k
    return out * 0.8


def sweep(f0, f1, sec, shape=1.0):
    """Frecuencia que va de f0 a f1 (shape > 1: cambia más al final)."""
    x = np.linspace(0, 1, n(sec)) ** shape
    return f0 * (f1 / f0) ** x


def glide(points, sec):
    """Contorno de frecuencia por puntos [(fracción, Hz), ...] (interpolación logarítmica)."""
    x = np.linspace(0, 1, n(sec))
    xs = [p[0] for p in points]
    ys = [math.log(p[1]) for p in points]
    return np.exp(np.interp(x, xs, ys))


def bellnote(freq, sec, tau=0.25, partials=((1, 1), (2.76, 0.35), (5.4, 0.15), (8.93, 0.06))):
    """Campanita (parciales inarmónicos)."""
    out = np.zeros(n(sec))
    tt = t(sec)
    for ratio, amp in partials:
        out += amp * np.sin(2 * np.pi * freq * ratio * tt) * np.exp(-tt / (tau / ratio ** 0.5))
    return out


def metal(freqs, sec, tau=0.15, rng=None):
    rng = rng or RNG
    out = np.zeros(n(sec))
    tt = t(sec)
    for i, f in enumerate(freqs):
        out += np.sin(2 * np.pi * f * tt + rng.uniform(0, 6.28)) * np.exp(-tt / (tau * (1 - 0.12 * i))) / (1 + i * 0.4)
    return out


# ---------- filtros (por FFT; los que cambian con el tiempo van por bloques) ----------

def _filter(x, gain_fn):
    """Filtro de fase cero por FFT. Se rellena con ceros a los lados para que la respuesta no "dé la vuelta"
    (sin el relleno, el final del sonido se colaría al principio y al revés)."""
    N = len(x)
    pad = min(max(N // 2, 2048), 16384)
    xp = np.concatenate([np.zeros(pad), x, np.zeros(pad)])
    F = np.fft.rfft(xp)
    f = np.fft.rfftfreq(len(xp), 1 / SR)
    return np.fft.irfft(F * gain_fn(f), len(xp))[pad:pad + N]


def lp(x, fc, order=2):
    return _filter(x, lambda f: 1 / np.sqrt(1 + (f / max(fc, 1)) ** (2 * order)))


def hp(x, fc, order=2):
    return _filter(x, lambda f: 1 / np.sqrt(1 + (max(fc, 1) / np.maximum(f, 1e-3)) ** (2 * order)))


def bp(x, lo, hi, order=2):
    return hp(lp(x, hi, order), lo, order)


def peak(x, fc, q=4.0, gain=4.0):
    """Realce estrecho alrededor de fc (formante)."""
    return _filter(x, lambda f: 1 + gain * np.exp(-((f - fc) / (fc / q)) ** 2))


def moving_filter(x, fc_curve, kind='lp', order=2, block=1024):
    """Filtro cuya frecuencia de corte sigue fc_curve (array del largo de x).
    Bloques con ventana de Hann al 50 % (suman 1) y cada bloque filtrado dentro de un búfer el doble de largo,
    centrado, para que la respuesta del filtro no se solape consigo misma (eso sonaba como un zumbido)."""
    N = len(x)
    hop = block // 2
    big = block * 2
    off = (big - block) // 2
    win = np.hanning(block + 1)[:-1]          # Hann periódica: con 50 % de solape suma exactamente 1
    pad = np.concatenate([x, np.zeros(block)])
    out = np.zeros(N + big + block)
    f = np.fft.rfftfreq(big, 1 / SR)
    fc_curve = np.concatenate([np.asarray(fc_curve, float), np.full(block, fc_curve[-1])])
    for s in range(0, N, hop):
        seg = np.zeros(big)
        seg[off:off + block] = pad[s:s + block] * win
        fc = max(fc_curve[min(s + hop, len(fc_curve) - 1)], 20)
        if kind == 'lp':
            g = 1 / np.sqrt(1 + (f / fc) ** (2 * order))
        elif kind == 'hp':
            g = 1 / np.sqrt(1 + (fc / np.maximum(f, 1e-3)) ** (2 * order))
        else:  # banda alrededor de fc
            g = np.exp(-((f - fc) / (fc * 0.35)) ** 2)
        y = np.fft.irfft(np.fft.rfft(seg) * g, big)
        start = s - off + block            # el búfer de salida tiene 'block' de margen al principio
        out[start:start + big] += y
    return out[block:block + N]


def reverb(x, mix=0.25, decay=0.8, tone=3500, rng=None):
    """Reverberación simple: convolución con ruido que se apaga."""
    rng = rng or np.random.default_rng(3)
    L = n(decay * 1.5)
    tt = np.arange(L) / SR
    ir = rng.standard_normal(L) * np.exp(-tt / (decay / 4.6))
    ir = lp(ir, tone)
    ir[:n(0.008)] *= np.linspace(0, 1, n(0.008))
    ir /= np.sqrt(np.sum(ir ** 2)) + 1e-9
    N = len(x) + L
    wet = np.fft.irfft(np.fft.rfft(x, N) * np.fft.rfft(ir, N), N)
    dry = np.concatenate([x, np.zeros(L)])
    return dry * (1 - mix) + wet * mix * 1.6


def place(base, clip, at):
    """Suma 'clip' dentro de 'base' empezando en 'at' segundos (alarga si hace falta)."""
    i = n(at) if at > 0 else 0
    end = i + len(clip)
    if end > len(base):
        base = np.concatenate([base, np.zeros(end - len(base))])
    base = base.copy()
    base[i:end] += clip
    return base


def mixdown(*parts):
    L = max(len(p) for p in parts)
    out = np.zeros(L)
    for p in parts:
        out[:len(p)] += p
    return out


def loopable(x, xfade=0.4):
    """Hace un bucle sin saltos: mezcla el final con el principio."""
    c = n(xfade)
    body = x[:-c].copy()
    tail = x[-c:]
    k = np.linspace(0, 1, c)
    body[:c] = body[:c] * np.sqrt(k) + tail * np.sqrt(1 - k)
    return body


def normalize(x, peak_db=-1.0):
    m = np.abs(x).max()
    if m < 1e-9:
        return x
    return x / m * (10 ** (peak_db / 20))


def trim_tail(x, floor_db=-55.0):
    """Quita el silencio del final (colas de reverberación que ya no se oyen)."""
    m = np.abs(x).max()
    if m < 1e-9:
        return x
    loud = np.nonzero(np.abs(x) > m * 10 ** (floor_db / 20))[0]
    end = min(len(x), loud[-1] + n(0.03)) if len(loud) else len(x)
    return x[:end]


def write(path, x, peak_db=-1.0, fin=0.002, fout=0.01, loop=False):
    if not loop:
        x = fade(trim_tail(x), fin, fout)
    x = normalize(x, peak_db)
    data = np.clip(x * 32767, -32768, 32767).astype('<i2')
    with wave.open(path, 'wb') as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(data.tobytes())
    return len(x) / SR
