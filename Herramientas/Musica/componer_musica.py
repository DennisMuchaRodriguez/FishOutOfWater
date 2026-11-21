# -*- coding: utf-8 -*-
"""
Música original de Fish Out Of Water.

Las partituras están escritas aquí (notas, acordes, ritmos) y se tocan con instrumentos de verdad (muestras de la
colección FluidR3_GM, licencia MIT) usando FluidSynth. Resultado: archivos OGG en Assets/Resources/Musica.

    Menu      "El Traje"            Re menor, 96 pulsos, 32 compases (80 s, en bucle). Épica: del fondo del lago
                                    (celesta, coro, pads) al taller (ostinato de cuerdas, trompas con el tema) y a la
                                    salida (trompetas, coro, taikos y timbales).
    Juego     "Batalla en el lago"  Mi menor, 132 pulsos, 40 compases (73 s, en bucle). Acción: cuerdas y bajo
                                    que empujan, batería y taikos, el tema en trompas y trompetas.
    Carga     "Bajo la superficie"  Re menor, 76 pulsos, 8 compases (25 s, en bucle). Tranquila: pads, cristal y arpa.
    Derrota   (unos 10 s, una vez)  Lamento: cuerdas y trompa que bajan, coro y un golpe de timbal.
    Victoria  (unos 6 s, una vez)   Fanfarria en Re mayor.

Los bucles se tocan tres veces seguidas y se queda la del medio: así el principio ya trae la cola (reverb, notas
largas) del final y el bucle no se corta al repetirse.

Requisitos (Ubuntu): apt install fluidsynth fluid-soundfont-gm ffmpeg  ·  pip install mido numpy
Uso: python3 Herramientas/Musica/componer_musica.py [Menu Juego Carga Derrota Victoria]   (desde la raíz del proyecto)
"""
import os
import random
import subprocess
import sys
import tempfile
import wave

import mido
import numpy as np

SF2 = "/usr/share/sounds/sf2/FluidR3_GM.sf2"
OUT_DIR = os.path.join("Assets", "Resources", "Musica")
SR = 44100
TPB = 480

# ---------------------------------------------------------------- notas y acordes

PC = {'C': 0, 'C#': 1, 'Db': 1, 'D': 2, 'D#': 3, 'Eb': 3, 'E': 4, 'F': 5, 'F#': 6, 'Gb': 6, 'G': 7,
      'G#': 8, 'Ab': 8, 'A': 9, 'A#': 10, 'Bb': 10, 'B': 11}


def n(s):
    """'D4' -> 62 (Do central = C4 = 60)."""
    return 12 * (int(s[-1]) + 1) + PC[s[:-1]]


CHORDS = {
    'Dm': ['D', 'F', 'A'], 'Bb': ['Bb', 'D', 'F'], 'F': ['F', 'A', 'C'], 'C': ['C', 'E', 'G'],
    'Gm': ['G', 'Bb', 'D'], 'A': ['A', 'C#', 'E'], 'Am': ['A', 'C', 'E'], 'D': ['D', 'F#', 'A'],
    'Em': ['E', 'G', 'B'], 'G': ['G', 'B', 'D'], 'B': ['B', 'D#', 'F#'],
}


def pcs(ch):
    return [PC[x] for x in CHORDS[ch]]


def tones(ch, lo, hi):
    """Notas del acorde entre lo y hi (MIDI)."""
    p = pcs(ch)
    return [m for m in range(lo, hi + 1) if m % 12 in p]


def root(ch, octave):
    return 12 * (octave + 1) + PC[CHORDS[ch][0]]


def third(ch, octave):
    r, t = PC[CHORDS[ch][0]], PC[CHORDS[ch][1]]
    return 12 * (octave + 1) + t + (12 if t < r else 0)


def low_root(ch):
    """Raíz para los timbales (entre Fa2 y Mi3)."""
    r = root(ch, 2)
    return r if r <= n('E3') else r - 12


# ---------------------------------------------------------------- canción

class Track:
    def __init__(self, name, ch, prog, bank=0, vol=100, pan=64, rev=50, cho=0):
        self.name, self.ch, self.prog, self.bank = name, ch, prog, bank
        self.vol, self.pan, self.rev, self.cho = vol, pan, rev, cho
        self.notes = []   # (inicio, duración, nota, fuerza) en pulsos
        self.ccs = []     # (pulso, cc, valor)

    def note(self, start, dur, pitch, vel):
        if pitch is None or dur <= 0:
            return
        self.notes.append((start, dur, int(pitch), int(max(1, min(127, vel)))))

    def chord(self, start, dur, pitches, vel):
        for p in pitches:
            self.note(start, dur, p, vel)

    def expr(self, start, end, v0, v1, steps=24):
        """Rampa de expresión (CC11): crescendos y diminuendos."""
        for i in range(steps + 1):
            k = i / steps
            self.ccs.append((start + (end - start) * k, 11, int(v0 + (v1 - v0) * k)))


class Song:
    def __init__(self, bpm, seed=1):
        self.bpm = bpm
        self.tracks = []
        self.rng = random.Random(seed)
        self.rng_seed = seed
        self.T = {}

    def track(self, *a, **k):
        t = Track(*a, **k)
        self.tracks.append(t)
        return t

    def beat_seconds(self):
        return 60.0 / self.bpm

    def write_midi(self, path, period=None):
        """period: largo del bucle en pulsos. La imprecisión humana sale de la posición dentro del bucle, así las
        tres copias suenan idénticas (el final empalma exacto con el principio)."""
        mf = mido.MidiFile(ticks_per_beat=TPB)
        meta = mido.MidiTrack()
        meta.append(mido.MetaMessage('set_tempo', tempo=mido.bpm2tempo(self.bpm), time=0))
        mf.tracks.append(meta)
        for t in self.tracks:
            ev = []
            if t.ch != 9:
                ev.append((0, 0, mido.Message('control_change', channel=t.ch, control=0, value=t.bank)))
            ev.append((0, 1, mido.Message('program_change', channel=t.ch, program=t.prog)))
            for (num, val) in ((7, t.vol), (10, t.pan), (91, t.rev), (93, t.cho), (11, 127)):
                ev.append((0, 2, mido.Message('control_change', channel=t.ch, control=num, value=val)))
            for (b, num, val) in t.ccs:
                ev.append((int(round(b * TPB)), 3, mido.Message('control_change', channel=t.ch, control=num, value=max(0, min(127, val)))))
            for (s, d, p, v) in t.notes:
                # Pequeña imprecisión humana: solo hacia adelante (ninguna nota empieza antes de su pulso)
                pos = (s % period) if period else s
                r = random.Random(hash((t.ch, p, int(round(pos * 960)), int(round(d * 960)), self.rng_seed)))
                off = r.uniform(0.0, 0.012 if t.ch != 9 else 0.006)
                vel = v + r.randint(-4, 4)
                on = int(round((s + off) * TPB))
                end = max(on + 1, int(round((s + off + d) * TPB)))
                ev.append((on, 5, mido.Message('note_on', channel=t.ch, note=p, velocity=max(1, min(127, vel)))))
                ev.append((end, 4, mido.Message('note_off', channel=t.ch, note=p, velocity=0)))
            ev.sort(key=lambda e: (e[0], e[1]))
            mt = mido.MidiTrack()
            mt.append(mido.MetaMessage('track_name', name=t.name, time=0))
            last = 0
            for (tick, _, msg) in ev:
                mt.append(msg.copy(time=tick - last))
                last = tick
            mf.tracks.append(mt)
        mf.save(path)


# ---------------------------------------------------------------- figuras que se repiten

def pad(t, start, bars, prog, lo, hi, vel, beats=4):
    for i, ch in enumerate(prog[:bars]):
        t.chord(start + i * beats, beats * 0.98, tones(ch, lo, hi), vel)


def arp(t, start, prog, lo, hi, vel, step=0.5, beats=4, shape=(0, 1, 2, 3, 2, 1, 2, 3)):
    for i, ch in enumerate(prog):
        tt = tones(ch, lo, hi)
        if not tt:
            continue
        k = 0
        b = 0.0
        while b < beats - 1e-6:
            idx = shape[k % len(shape)] % len(tt)
            acc = 8 if k % 4 == 0 else 0
            t.note(start + i * beats + b, step * 0.95, tt[idx], vel + acc)
            b += step
            k += 1


def melody(t, start, line, vel, shift=0):
    """line: lista de (nota o None, duración en pulsos)."""
    b = start
    for (p, d) in line:
        if p is not None:
            t.note(b, d * 0.97, n(p) + shift, vel)
        b += d
    return b


def gallop(t, start, prog, octave, vel, beats=4):
    """Bajo al galope (da-da-DA): el pulso de las bandas sonoras épicas."""
    rhythm = [(0, 0.45, 1.0), (0.75, 0.45, 0.8), (1.5, 0.45, 0.85), (2, 0.45, 1.0), (2.75, 0.45, 0.8), (3.5, 0.45, 0.85)]
    for i, ch in enumerate(prog):
        r = root(ch, octave)
        for (b, d, a) in rhythm:
            t.note(start + i * beats + b, d, r, vel * a)


def ostinato16(t, start, prog, octave, vel, beats=4):
    """Cuerdas en semicorcheas: raíz alta, raíz, quinta, raíz (el motor del tema)."""
    for i, ch in enumerate(prog):
        r = root(ch, octave)
        cell = [r + 12, r, r + 7, r]
        for k in range(int(beats * 4)):
            acc = 1.0 if k % 4 == 0 else (0.82 if k % 2 == 0 else 0.7)
            t.note(start + i * beats + k * 0.25, 0.22, cell[k % 4], vel * acc)


def ostinato8(t, start, prog, octave, vel, beats=4):
    """Cuerdas en corcheas arpegiadas (raíz, quinta, octava, quinta, tercera...)."""
    for i, ch in enumerate(prog):
        r = root(ch, octave)
        th = third(ch, octave)
        cell = [r, r + 7, r + 12, r + 7, th + 12, r + 7, r + 12, r + 7]
        for k in range(int(beats * 2)):
            acc = 1.0 if k % 2 == 0 else 0.78
            t.note(start + i * beats + k * 0.5, 0.42, cell[k % 8], vel * acc)


def roll(t, start, dur, pitch, v0, v1, rate=0.125):
    """Redoble (timbal, platillo): notas muy rápidas con crescendo."""
    steps = max(1, int(round(dur / rate)))
    for k in range(steps):
        a = k / max(1, steps - 1)
        t.note(start + k * rate, rate * 0.9, pitch, v0 + (v1 - v0) * a)


# Percusión (canal 10, números GM)
KICK, CONCERT_BD, SNARE, HAT, CRASH, CRASH2, CHINA = 36, 35, 38, 42, 49, 57, 52
TOM_LO, TOM_MID, TOM_HI, TOM_FLOOR = 45, 47, 48, 41

# ================================================================ MENÚ: "El Traje"


def compose_menu(song, off):
    A = ['Dm', 'Bb', 'F', 'C', 'Dm', 'Bb', 'Gm', 'A']
    B = ['Dm', 'Bb', 'C', 'F', 'Dm', 'Bb', 'C', 'A']
    C = ['Dm', 'Bb', 'C', 'Am', 'Dm', 'Bb', 'C', 'D']
    D = ['Gm', 'Bb', 'F', 'C', 'Dm', 'Bb', 'Gm', 'A']
    T = song.T
    a, b, c, d = off, off + 32, off + 64, off + 96

    # ---- A: el fondo del lago (bajo el agua, misterioso)
    pad(T['halo'], a, 8, A, n('D3'), n('A4'), 62)
    T['halo'].expr(a, a + 32, 70, 110)
    for i, ch in enumerate(A[1:]):
        T['slow'].chord(a + 4 + i * 4, 3.92, tones(ch, n('A2'), n('D4')), 60)
    T['slow'].expr(a + 4, a + 32, 60, 105)
    for i, ch in enumerate(A):
        T['cbass'].note(a + i * 4, 3.9, root(ch, 2), 70)
    arp(T['celesta'], a, A, n('D5'), n('A6'), 56)
    pad(T['choir'], a + 16, 4, A[4:], n('D4'), n('A4'), 54)
    T['choir'].expr(a + 16, a + 32, 70, 115)
    T['timp'].note(a, 2, n('D2'), 70)
    roll(T['timp'], a + 28, 4, n('A2'), 25, 85)
    # Arpa: un barrido hacia arriba al entrar el coro
    for k, p in enumerate(tones('Dm', n('D3'), n('D6'))):
        T['harp'].note(a + 16 + k * 0.125, 1.5, p, 55 + k)

    # ---- B: el taller (motor de cuerdas, el tema en las trompas)
    ostinato8(T['strings'], b, B, 3, 74)
    T['strings'].expr(b, b + 32, 95, 118)
    gallop(T['cello'], b, B, 2, 82)
    for i, ch in enumerate(B):
        T['cbass'].note(b + i * 4, 3.9, root(ch, 1), 70)
    pad(T['slow'], b, 8, B, n('D3'), n('F4'), 54)
    theme = [('A3', 1.5), ('D4', 0.5), ('D4', 1), ('E4', 0.5), ('F4', 0.5),
             ('F4', 1.5), ('E4', 0.5), ('D4', 1), ('C4', 1),
             ('E4', 1.5), ('D4', 0.5), ('C4', 1), ('G3', 1),
             ('A3', 3), (None, 1),
             ('A3', 1.5), ('D4', 0.5), ('D4', 1), ('E4', 0.5), ('F4', 0.5),
             ('G4', 1.5), ('F4', 0.5), ('E4', 1), ('D4', 1),
             ('E4', 1), ('F4', 1), ('G4', 1.5), ('F4', 0.5),
             ('E4', 2), ('C#4', 1), ('E4', 1)]
    melody(T['horns'], b, theme, 88)
    for i in range(8):
        T['taiko'].note(b + i * 4, 1, n('C3'), 92)
        T['taiko'].note(b + i * 4 + 2, 1, n('C3'), 74)
        if i % 4 == 3:
            T['taiko'].note(b + i * 4 + 3.5, 0.5, n('C3'), 80)
        T['timp'].note(b + i * 4, 1.5, low_root(B[i]), 78)
    arp(T['synth'], b + 16, B[4:], n('D4'), n('D6'), 34, step=0.25, shape=(0, 2, 1, 3, 2, 4, 3, 1))
    arp(T['harp'], b + 16, B[4:], n('D4'), n('A5'), 52, step=0.5)
    roll(T['drums'], b + 30, 2, CRASH2, 20, 75, rate=0.1)

    # ---- C: ¡al agua! (todo: trompetas, coro, metales, tambores)
    theme_hi = [('A4', 1.5), ('D5', 0.5), ('D5', 1), ('E5', 0.5), ('F5', 0.5),
                ('F5', 1.5), ('E5', 0.5), ('D5', 1), ('C5', 1),
                ('E5', 1.5), ('D5', 0.5), ('C5', 1), ('G4', 1),
                ('A4', 2), ('C5', 1), ('E5', 1),
                ('A4', 1.5), ('D5', 0.5), ('D5', 1), ('E5', 0.5), ('F5', 0.5),
                ('G5', 1.5), ('F5', 0.5), ('E5', 1), ('D5', 1),
                ('E5', 1), ('F5', 1), ('G5', 1.5), ('E5', 0.5),
                ('F#5', 2), ('A5', 2)]
    melody(T['trumpet'], c, theme_hi, 100)
    melody(T['horns'], c, theme_hi, 92, shift=-12)
    melody(T['glock'], c, theme_hi, 40, shift=12)
    melody(T['slow'], c, theme_hi, 70)
    for i, ch in enumerate(C):
        T['brass'].chord(c + i * 4, 1.9, tones(ch, n('D3'), n('D4')), 82)
        T['brass'].chord(c + i * 4 + 2, 1.9, tones(ch, n('D3'), n('D4')), 74)
        T['cbass'].note(c + i * 4, 3.9, root(ch, 1), 80)
    pad(T['choir'], c, 8, C, n('A3'), n('A4'), 84)
    T['choir'].expr(c, c + 32, 110, 127)
    ostinato16(T['strings'], c, C, 3, 80)
    gallop(T['cello'], c, C, 2, 92)
    for i in range(8):
        for (bb, v) in ((0, 104), (1, 70), (1.5, 82), (2, 96), (3, 74), (3.5, 86)):
            T['taiko'].note(c + i * 4 + bb, 0.5, n('C3'), v)
        T['drums'].note(c + i * 4, 1, CONCERT_BD, 100)
        T['drums'].note(c + i * 4 + 2, 1, CONCERT_BD, 84)
        if i < 7:
            T['timp'].note(c + i * 4, 1, low_root(C[i]), 92)
            T['timp'].note(c + i * 4 + 2, 1, low_root(C[i]), 80)
    T['drums'].note(c, 3, CRASH, 110)
    T['drums'].note(c + 16, 3, CRASH2, 104)
    roll(T['drums'], c + 30, 2, SNARE, 40, 112, rate=0.125)
    roll(T['timp'], c + 28, 4, n('D2'), 60, 118)

    # ---- D: hacia el cielo (resuelve y vuelve a calmarse para empezar otra vez)
    D1 = D[:4]
    counter = [('Bb4', 2), ('A4', 1), ('G4', 1), ('D5', 2), ('C5', 1), ('Bb4', 1),
               ('C5', 2), ('A4', 1), ('F4', 1), ('G4', 2), ('E4', 2)]
    melody(T['strings'], d, counter, 86)
    T['strings'].expr(d, d + 16, 118, 90)
    melody(T['horns'], d, counter, 70, shift=-12)
    pad(T['choir'], d, 4, D1, n('G3'), n('G4'), 70)
    T['choir'].expr(d, d + 16, 115, 70)
    for i, ch in enumerate(D):
        T['cbass'].note(d + i * 4, 3.9, root(ch, 2) if i >= 4 else root(ch, 1) + 12, 64)
    arp(T['harp'], d, D1, n('G3'), n('D5'), 56, step=0.5)
    for i in range(4):
        T['taiko'].note(d + i * 4, 1.5, n('C3'), 78 - i * 8)
    T['timp'].note(d, 2, n('G2'), 80)
    # Últimos 4 compases: solo el agua (pads y celesta); el compás de La lleva otra vez al principio
    pad(T['halo'], d + 16, 4, D[4:], n('D3'), n('A4'), 50)
    T['halo'].expr(d + 16, d + 32, 105, 75)
    for i, ch in enumerate(D[4:]):
        T['slow'].chord(d + 16 + i * 4, 3.92, tones(ch, n('A2'), n('D4')), 52)
    T['slow'].expr(d + 16, d + 32, 100, 62)
    arp(T['celesta'], d + 16, D[4:], n('D5'), n('A6'), 42)


def build_menu():
    s = Song(96, seed=11)
    s.T = {
        'slow': s.track('Cuerdas lentas', 0, 49, vol=96, pan=60, rev=70),
        'strings': s.track('Cuerdas', 1, 48, vol=100, pan=68, rev=55),
        'cello': s.track('Chelos', 2, 42, vol=96, pan=54, rev=45),
        'cbass': s.track('Contrabajos', 3, 43, vol=94, pan=60, rev=40),
        'horns': s.track('Trompas', 4, 60, vol=104, pan=50, rev=70),
        'trumpet': s.track('Trompetas', 5, 56, vol=96, pan=74, rev=60),
        'brass': s.track('Metales', 6, 61, vol=88, pan=64, rev=60),
        'choir': s.track('Coro', 7, 52, vol=92, pan=64, rev=85),
        'celesta': s.track('Celesta', 8, 8, vol=84, pan=80, rev=90),
        'drums': s.track('Percusión orquestal', 9, 48, vol=100, pan=64, rev=60),
        'timp': s.track('Timbales', 10, 47, vol=104, pan=60, rev=60),
        'taiko': s.track('Taikos', 11, 116, vol=96, pan=64, rev=50),
        'halo': s.track('Pad del lago', 12, 94, vol=82, pan=64, rev=90),
        'harp': s.track('Arpa', 13, 46, vol=88, pan=44, rev=80),
        'glock': s.track('Glockenspiel', 14, 9, vol=70, pan=84, rev=80),
        'synth': s.track('Cristal', 15, 98, vol=70, pan=90, rev=80),
    }
    return s, compose_menu, 32


# ================================================================ JUEGO: "Batalla en el lago"


def compose_game(song, off):
    A = ['Em', 'C', 'G', 'D', 'Em', 'C', 'G', 'D']
    B = ['Em', 'C', 'Am', 'B', 'Em', 'C', 'Am', 'B']
    C = ['C', 'G', 'D', 'Em', 'C', 'G', 'D', 'D']
    D = ['Am', 'Em', 'C', 'D', 'Am', 'Em', 'B', 'B']
    E = ['Em', 'C', 'G', 'D', 'Em', 'C', 'Am', 'B']
    T = song.T
    sa, sb, sc, sd, se = off, off + 32, off + 64, off + 96, off + 128

    def groove(start, bars, full, snare=True):
        for i in range(bars):
            s0 = start + i * 4
            for k in range(8):
                T['drums'].note(s0 + k * 0.5, 0.25, HAT, 70 if k % 2 == 0 else 52)
            T['drums'].note(s0, 0.5, KICK, 108)
            T['drums'].note(s0 + 1.5, 0.5, KICK, 92)
            T['drums'].note(s0 + 2, 0.5, KICK, 104)
            if full:
                T['drums'].note(s0 + 2.75, 0.25, KICK, 84)
            if snare:
                T['drums'].note(s0 + 1, 0.5, SNARE, 100)
                T['drums'].note(s0 + 3, 0.5, SNARE, 104)

    def fill(start):
        for k, (p, v) in enumerate(((TOM_HI, 96), (TOM_HI, 90), (TOM_MID, 98), (TOM_MID, 92),
                                    (TOM_LO, 100), (TOM_LO, 96), (TOM_FLOOR, 104), (SNARE, 110))):
            T['drums'].note(start + k * 0.25, 0.25, p, v)

    def bass8(start, prog, vel):
        for i, ch in enumerate(prog):
            r = root(ch, 1)
            for k in range(8):
                T['bass'].note(start + i * 4 + k * 0.5, 0.42, r + (12 if k in (3, 7) else 0), vel if k % 2 == 0 else vel - 14)

    # ---- A: en guardia
    groove(sa, 8, False, snare=False)
    T['drums'].note(sa, 2, CRASH, 96)
    ostinato8(T['strings'], sa, A, 3, 76)
    bass8(sa, A, 86)
    for i in range(0, 8, 2):
        T['brass'].chord(sa + i * 4, 0.45, tones(A[i], n('E3'), n('E4')), 96)
        T['taiko'].note(sa + i * 4, 1, n('C3'), 100)
    fill(sa + 30)

    # ---- B: el tema (trompas)
    groove(sb, 8, True)
    ostinato8(T['strings'], sb, B, 3, 80)
    bass8(sb, B, 90)
    pad(T['slow'], sb, 8, B, n('B2'), n('E4'), 56)
    theme = [('E4', 1), ('B3', 0.5), ('E4', 0.5), ('F#4', 0.5), ('G4', 1.5),
             ('G4', 0.5), ('F#4', 0.5), ('E4', 1), ('C4', 1), ('E4', 1),
             ('A4', 1.5), ('G4', 0.5), ('E4', 1), ('C4', 1),
             ('B3', 2), ('D#4', 1), ('F#4', 1),
             ('E4', 1), ('B3', 0.5), ('E4', 0.5), ('F#4', 0.5), ('G4', 1.5),
             ('A4', 1), ('G4', 0.5), ('F#4', 0.5), ('E4', 1), ('G4', 1),
             ('C5', 1.5), ('B4', 0.5), ('A4', 1), ('E4', 1),
             ('D#4', 2), ('F#4', 1), ('B4', 1)]
    melody(T['horns'], sb, theme, 96)
    for i in range(8):
        T['timp'].note(sb + i * 4, 1, low_root(B[i]), 84)
    fill(sb + 30)

    # ---- C: el estribillo (trompetas, coro, platillos)
    groove(sc, 8, True)
    T['drums'].note(sc, 2, CRASH, 112)
    T['drums'].note(sc + 16, 2, CRASH2, 106)
    ostinato16(T['strings'], sc, C, 3, 82)
    bass8(sc, C, 94)
    chorus = [('E5', 2), ('G5', 1), ('E5', 1),
              ('D5', 2), ('B4', 1), ('D5', 1),
              ('F#5', 1.5), ('E5', 0.5), ('D5', 1), ('A4', 1),
              ('B4', 3), (None, 1),
              ('E5', 2), ('G5', 1), ('A5', 1),
              ('B5', 2), ('A5', 1), ('G5', 1),
              ('F#5', 1), ('G5', 1), ('A5', 1.5), ('F#5', 0.5),
              ('D5', 4)]
    melody(T['trumpet'], sc, chorus, 104)
    melody(T['horns'], sc, chorus, 90, shift=-12)
    pad(T['choir'], sc, 8, C, n('G3'), n('G4'), 86)
    for i, ch in enumerate(C):
        T['brass'].chord(sc + i * 4, 0.9, tones(ch, n('E3'), n('E4')), 92)
        T['brass'].chord(sc + i * 4 + 2.5, 0.45, tones(ch, n('E3'), n('E4')), 84)
        T['taiko'].note(sc + i * 4, 0.5, n('C3'), 106)
        T['taiko'].note(sc + i * 4 + 1.5, 0.5, n('C3'), 84)
        T['taiko'].note(sc + i * 4 + 2, 0.5, n('C3'), 96)
    fill(sc + 30)

    # ---- D: tensión (trémolo, pizzicato, redoble que crece)
    for i, ch in enumerate(D):
        T['trem'].chord(sd + i * 4, 3.95, tones(ch, n('E3'), n('B4')), 70 + i * 3)
        r = root(ch, 2)
        for k in range(8):
            T['pizz'].note(sd + i * 4 + k * 0.5, 0.4, [r, r + 7, r + 12, r + 7][k % 4], 84 if k % 2 == 0 else 70)
        T['bass'].note(sd + i * 4, 1.8, root(ch, 1), 92)
        T['bass'].note(sd + i * 4 + 2, 1.8, root(ch, 1), 80)
    T['trem'].expr(sd, sd + 32, 80, 120)
    for i in range(8):
        T['drums'].note(sd + i * 4, 0.5, KICK, 100)
        T['drums'].note(sd + i * 4 + 2, 0.5, KICK, 90)
        T['taiko'].note(sd + i * 4 + 3, 0.5, n('C3'), 70 + i * 4)
    for i in range(4, 8):
        for k in range(8):
            T['drums'].note(sd + i * 4 + k * 0.5, 0.25, HAT, 60)
    roll(T['timp'], sd + 24, 8, n('B2'), 40, 120, rate=0.125)
    roll(T['drums'], sd + 28, 4, SNARE, 30, 116, rate=0.125)
    melody(T['horns'], sd + 16, [('E4', 4), ('G4', 4), ('F#4', 4), ('D#4', 4)], 80)

    # ---- E: el tema a lo grande (y vuelve al principio)
    groove(se, 8, True)
    T['drums'].note(se, 3, CRASH, 116)
    T['drums'].note(se, 3, CRASH2, 100)
    T['drums'].note(se + 16, 2, CRASH, 108)
    ostinato16(T['strings'], se, E, 3, 84)
    bass8(se, E, 96)
    theme_e = [('E5', 1), ('B4', 0.5), ('E5', 0.5), ('F#5', 0.5), ('G5', 1.5),
               ('G5', 0.5), ('F#5', 0.5), ('E5', 1), ('C5', 1), ('E5', 1),
               ('D5', 1.5), ('B4', 0.5), ('D5', 1), ('G5', 1),
               ('F#5', 2), ('D5', 1), ('A4', 1),
               ('E5', 1), ('B4', 0.5), ('E5', 0.5), ('F#5', 0.5), ('G5', 1.5),
               ('A5', 1), ('G5', 0.5), ('F#5', 0.5), ('E5', 1), ('G5', 1),
               ('C6', 1.5), ('B5', 0.5), ('A5', 1), ('E5', 1),
               ('D#5', 2), ('F#5', 1), ('B5', 1)]
    melody(T['trumpet'], se, theme_e, 106)
    melody(T['horns'], se, theme_e, 94, shift=-12)
    melody(T['slow'], se, theme_e, 66, shift=-12)
    pad(T['choir'], se, 8, E, n('G3'), n('B4'), 88)
    for i, ch in enumerate(E):
        T['brass'].chord(se + i * 4, 0.9, tones(ch, n('E3'), n('E4')), 94)
        T['brass'].chord(se + i * 4 + 2.5, 0.45, tones(ch, n('E3'), n('E4')), 86)
        T['timp'].note(se + i * 4, 1, low_root(ch), 92)
        for (bb, v) in ((0, 106), (1.5, 84), (2, 98), (3.5, 88)):
            T['taiko'].note(se + i * 4 + bb, 0.5, n('C3'), v)
    fill(se + 30)


def build_game():
    s = Song(132, seed=23)
    s.T = {
        'slow': s.track('Cuerdas lentas', 0, 49, vol=90, pan=60, rev=60),
        'strings': s.track('Cuerdas', 1, 48, vol=100, pan=68, rev=45),
        'trem': s.track('Trémolo', 2, 44, vol=92, pan=56, rev=55),
        'pizz': s.track('Pizzicato', 3, 45, vol=96, pan=74, rev=45),
        'horns': s.track('Trompas', 4, 60, vol=106, pan=50, rev=60),
        'trumpet': s.track('Trompetas', 5, 56, vol=98, pan=76, rev=55),
        'brass': s.track('Metales', 6, 61, vol=92, pan=64, rev=50),
        'choir': s.track('Coro', 7, 52, vol=86, pan=64, rev=80),
        'bass': s.track('Bajo', 8, 38, vol=92, pan=64, rev=20),
        'drums': s.track('Batería', 9, 16, vol=100, pan=64, rev=35),
        'timp': s.track('Timbales', 10, 47, vol=104, pan=60, rev=55),
        'taiko': s.track('Taikos', 11, 116, vol=100, pan=64, rev=45),
    }
    return s, compose_game, 40


# ================================================================ CARGA: "Bajo la superficie"


def compose_loading(song, off):
    P = ['Dm', 'Bb', 'F', 'C', 'Dm', 'Bb', 'Gm', 'A']
    T = song.T
    pad(T['pad'], off, 8, P, n('D3'), n('A4'), 58)
    for i in range(8):
        T['pad'].expr(off + i * 4, off + i * 4 + 2, 80, 110, steps=8)
        T['pad'].expr(off + i * 4 + 2, off + i * 4 + 4, 110, 80, steps=8)
    arp(T['crystal'], off, P, n('A4'), n('D6'), 50, step=1.0, shape=(0, 2, 1, 3))
    arp(T['harp'], off, P, n('D3'), n('A4'), 40, step=0.5, shape=(0, 1, 2, 1))
    for i, ch in enumerate(P):
        T['bass'].note(off + i * 4, 3.9, root(ch, 2), 56)
    melody(T['celesta'], off + 16, [('A5', 2), ('F5', 2), ('G5', 2), ('D5', 2), ('F5', 3), ('E5', 1), ('C#5', 4)], 44)


def build_loading():
    s = Song(76, seed=5)
    s.T = {
        'pad': s.track('Pad', 0, 89, vol=96, pan=64, rev=90),
        'crystal': s.track('Cristal', 1, 98, vol=80, pan=84, rev=95),
        'harp': s.track('Arpa', 2, 46, vol=84, pan=44, rev=85),
        'bass': s.track('Contrabajo', 3, 43, vol=88, pan=60, rev=60),
        'celesta': s.track('Celesta', 4, 8, vol=80, pan=70, rev=95),
    }
    return s, compose_loading, 8


# ================================================================ DERROTA y VICTORIA


def compose_defeat(song, off):
    T = song.T
    roll(T['timp'], off, 1.5, n('D2'), 30, 80, rate=0.1)
    T['timp'].note(off + 1.5, 3, n('D2'), 100)
    for (ch, b, d) in (('Dm', 0, 2), ('Bb', 2, 2), ('Gm', 4, 2), ('Dm', 6, 4)):
        T['strings'].chord(off + 1.5 + b, d * 0.98, tones(ch, n('D3'), n('F4')), 70)
    T['strings'].expr(off, off + 12, 115, 60)
    melody(T['horn'], off + 1.5, [('A4', 1.5), ('G4', 0.5), ('F4', 1), ('E4', 1), ('D4', 4)], 86)
    for (p, b, d) in (('D3', 0, 2), ('Bb2', 2, 2), ('G2', 4, 2), ('D2', 6, 4)):
        T['cello'].note(off + 1.5 + b, d * 0.98, n(p), 78)
    T['choir'].chord(off + 7.5, 4, tones('Dm', n('D4'), n('A4')), 60)
    T['timp'].note(off + 7.5, 3, n('D2'), 92)
    T['drums'].note(off + 7.5, 4, CHINA, 60)


def build_defeat():
    s = Song(60, seed=3)
    s.T = {
        'strings': s.track('Cuerdas', 0, 49, vol=100, pan=60, rev=80),
        'horn': s.track('Trompa', 1, 60, vol=108, pan=54, rev=85),
        'cello': s.track('Chelos', 2, 42, vol=100, pan=58, rev=70),
        'choir': s.track('Coro', 3, 53, vol=90, pan=64, rev=95),
        'timp': s.track('Timbal', 4, 47, vol=110, pan=60, rev=70),
        'drums': s.track('Platillo', 9, 48, vol=80, pan=64, rev=80),
    }
    return s, compose_defeat, None


def compose_victory(song, off):
    T = song.T
    melody(T['trumpet'], off, [('D5', 0.5), ('D5', 0.25), ('D5', 0.25), ('A5', 1), ('F#5', 0.5), ('A5', 0.5), ('D6', 4)], 108)
    melody(T['horns'], off, [('A4', 0.5), ('A4', 0.25), ('A4', 0.25), ('D5', 1), ('D5', 0.5), ('E5', 0.5), ('F#5', 4)], 96)
    for (ch, b, d) in (('D', 0, 1), ('G', 1, 1), ('A', 2, 1), ('D', 3, 4)):
        T['brass'].chord(off + b, d * 0.95, tones(ch, n('D3'), n('A4')), 98)
        T['strings'].chord(off + b, d * 0.95, tones(ch, n('D4'), n('A5')), 84)
    T['timp'].note(off, 0.5, n('D2'), 104)
    T['timp'].note(off + 1, 0.5, n('G2'), 96)
    T['timp'].note(off + 2, 0.5, n('A2'), 100)
    roll(T['timp'], off + 2.5, 0.5, n('A2'), 70, 110, rate=0.0625)
    T['timp'].note(off + 3, 2, n('D2'), 118)
    T['drums'].note(off + 3, 4, CRASH, 116)
    T['drums'].note(off + 3, 4, CRASH2, 104)
    for k, p in enumerate(('D6', 'F#6', 'A6', 'D7')):
        T['glock'].note(off + 3 + k * 0.25, 2, n(p), 64)


def build_victory():
    s = Song(110, seed=9)
    s.T = {
        'trumpet': s.track('Trompetas', 0, 56, vol=104, pan=74, rev=70),
        'horns': s.track('Trompas', 1, 60, vol=104, pan=50, rev=70),
        'brass': s.track('Metales', 2, 61, vol=96, pan=64, rev=70),
        'strings': s.track('Cuerdas', 3, 48, vol=92, pan=64, rev=75),
        'timp': s.track('Timbales', 4, 47, vol=110, pan=60, rev=70),
        'glock': s.track('Glockenspiel', 5, 9, vol=80, pan=84, rev=85),
        'drums': s.track('Platillos', 9, 48, vol=100, pan=64, rev=70),
    }
    return s, compose_victory, None


# ---------------------------------------------------------------- render y mezcla

def render(mid, wav):
    cmd = ['fluidsynth', '-ni', '-q', '-g', '0.42', '-r', str(SR), '-T', 'wav', '-F', wav,
           '-o', 'synth.polyphony=768', '-o', 'synth.reverb.room-size=0.82', '-o', 'synth.reverb.damp=0.35',
           '-o', 'synth.reverb.width=0.95', '-o', 'synth.reverb.level=0.8', '-o', 'synth.chorus.active=0',
           SF2, mid]
    subprocess.run(cmd, check=True, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)


def read_wav(path):
    with wave.open(path, 'rb') as w:
        ch = w.getnchannels()
        data = np.frombuffer(w.readframes(w.getnframes()), dtype=np.int16).astype(np.float64) / 32768.0
    return data.reshape(-1, ch)


def write_wav(path, x):
    pcm = (np.clip(x, -1.0, 1.0) * 32767.0).astype(np.int16)
    with wave.open(path, 'wb') as w:
        w.setnchannels(x.shape[1])
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(pcm.tobytes())


def highpass(x, hz=30.0, circular=True):
    """Quita el retumbe muy grave (filtro en frecuencia; circular = respeta el bucle)."""
    pad_n = 0 if circular else SR
    y = np.vstack([x, np.zeros((pad_n, x.shape[1]))]) if pad_n else x
    F = np.fft.rfft(y, axis=0)
    f = np.fft.rfftfreq(y.shape[0], 1.0 / SR)
    h = (f ** 2) / (f ** 2 + hz ** 2)
    out = np.fft.irfft(F * h[:, None], n=y.shape[0], axis=0)
    return out[:x.shape[0]]


def master(x, target_db):
    """Nivel parejo entre pistas (RMS) y un limitador suave para que nunca sature."""
    rms = np.sqrt(np.mean(x ** 2)) + 1e-9
    x = x * (10 ** (target_db / 20.0) / rms)
    knee = 0.82
    ax = np.abs(x)
    over = ax > knee
    x[over] = np.sign(x[over]) * (knee + (1 - knee) * np.tanh((ax[over] - knee) / (1 - knee)))
    peak = np.max(np.abs(x))
    if peak > 0.95:
        x *= 0.95 / peak
    return x


def encode(wav, ogg, quality='5'):
    subprocess.run(['ffmpeg', '-y', '-loglevel', 'error', '-i', wav, '-c:a', 'libvorbis', '-q:a', quality, ogg], check=True)


def make(name, builder, target_db, tail=4.0, fade=0.0):
    song, compose, bars = builder()
    beat = song.beat_seconds()
    tmp = tempfile.mkdtemp(prefix='musica_')
    mid, wav = os.path.join(tmp, name + '.mid'), os.path.join(tmp, name + '.wav')
    if bars:
        # Bucle: la partitura tres veces; se queda la del medio
        L = bars * 4
        for k in range(3):
            compose(song, k * L)
        song.write_midi(mid, period=L)
        render(mid, wav)
        full = read_wav(wav)
        n0 = int(round(L * beat * SR))
        x = full[n0:2 * n0].copy()
        # El principio del bucle se funde con lo que de verdad sigue al final: el empalme es exacto
        xf = int(0.3 * SR)
        w = np.linspace(0.0, 1.0, xf)[:, None]
        x[:xf] = full[2 * n0:2 * n0 + xf] * (1.0 - w) + x[:xf] * w
        x = highpass(x, circular=True)
        # Volumen de cada sección (para revisar la dinámica)
        sec = max(1, bars // 8)
        parts = []
        for i in range(0, bars, 8):
            a0 = int(i * 4 * beat * SR)
            a1 = int(min(bars, i + 8) * 4 * beat * SR)
            parts.append('%.1f' % (20 * np.log10(np.sqrt(np.mean(x[a0:a1] ** 2)) + 1e-9)))
        print('   secciones (dB antes de nivelar):', ' / '.join(parts))
    else:
        compose(song, 0.0)
        song.write_midi(mid)
        render(mid, wav)
        x = read_wav(wav)
        energy = np.max(np.abs(x), axis=1)
        last = np.nonzero(energy > 1e-4)[0]
        end = (last[-1] if len(last) else len(x) - 1) + 1
        x = x[:min(len(x), end + int(tail * SR))].copy()
        x = highpass(x, circular=False)
    x = master(x, target_db)
    if fade > 0:
        k = min(len(x), int(fade * SR))
        x[-k:] *= (np.linspace(1.0, 0.0, k) ** 2)[:, None]
    out_wav = os.path.join(tmp, name + '_final.wav')
    write_wav(out_wav, x)
    os.makedirs(OUT_DIR, exist_ok=True)
    out = os.path.join(OUT_DIR, name + '.ogg')
    encode(out_wav, out)
    # Salto en el empalme comparado con el salto normal entre dos muestras seguidas (1 = como cualquier otro)
    if bars:
        typical = np.percentile(np.abs(np.diff(x, axis=0)), 99.5) + 1e-9
        seam = float(np.max(np.abs(x[0] - x[-1])) / typical)
    else:
        seam = 0.0
    rms_db = 20 * np.log10(np.sqrt(np.mean(x ** 2)) + 1e-9)
    print('%-9s %6.1f s  RMS %5.1f dB  pico %.2f  salto en el bucle x%.2f  -> %s' %
          (name, len(x) / SR, rms_db, np.max(np.abs(x)), seam, out))
    return x, mid, wav


def main():
    if not os.path.isfile(SF2):
        sys.exit('Falta ' + SF2 + ' (apt install fluid-soundfont-gm)')
    only = set(sys.argv[1:])
    jobs = [('Menu', build_menu, -17.0, 0, 0), ('Juego', build_game, -18.0, 0, 0), ('Carga', build_loading, -21.0, 0, 0),
            ('Derrota', build_defeat, -18.0, 3.5, 2.0), ('Victoria', build_victory, -17.0, 3.0, 1.5)]
    for (name, b, db, tail, fade) in jobs:
        if only and name not in only:
            continue
        make(name, b, db, tail, fade)


if __name__ == '__main__':
    main()
