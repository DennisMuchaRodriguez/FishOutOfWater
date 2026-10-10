# -*- coding: utf-8 -*-
"""Kit de modelado de aves estilo plastilina para Fish Out Of Water.

Se usa desde un script de especie (ver aves/Ave_Gaviota.py) con Blender como módulo de Python:
    <python con bpy> Herramientas/Modelado/aves/Ave_Gaviota.py

Ejes (Blender): el pico apunta a -Y, arriba es +Z, la izquierda del ave es +X. 1 unidad = 1 m.
Flujo: piezas (Part) -> Bird.build() -> Bird.texture() -> Bird.export() -> Bird.verify() -> Bird.previews()
"""
import math, os, json, re, glob

import numpy as np
import bpy
from mathutils import Vector, Matrix

KIT_DIR = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(KIT_DIR, '..', '..'))
GUIDS_JSON = os.path.join(KIT_DIR, 'guids.json')


# =====================================================================================
# Utilidades numéricas
# =====================================================================================

def V(x):
    return np.asarray(x, dtype=float)


def unit(v):
    v = V(v)
    n = np.linalg.norm(v, axis=-1, keepdims=True)
    return v / np.maximum(n, 1e-12)


def smoothstep(e0, e1, x):
    t = np.clip((np.asarray(x, float) - e0) / (e1 - e0), 0.0, 1.0)
    return t * t * (3.0 - 2.0 * t)


def lerp(a, b, t):
    return a + (b - a) * t


def spline(P, n):
    """Catmull-Rom uniforme por filas de P (k, d) -> (n, d)."""
    P = V(P)
    if P.ndim == 1:
        P = P[:, None]
    k = len(P)
    if k == 1:
        return np.repeat(P, n, 0)
    t = np.linspace(0.0, k - 1.0, n)
    return spline_at(P, t)


def spline_at(P, t):
    """Evalúa la Catmull-Rom de P en parámetros t (0..k-1)."""
    P = V(P)
    if P.ndim == 1:
        P = P[:, None]
    k = len(P)
    t = np.asarray(t, float)
    if k == 1:
        return np.repeat(P, len(t), 0)
    if k == 2:
        u = (t / 1.0)[:, None]
        return P[0] + (P[1] - P[0]) * u
    ext = np.vstack([2 * P[0] - P[1], P, 2 * P[-1] - P[-2]])
    i = np.clip(np.floor(t).astype(int), 0, k - 2)
    u = (t - i)[:, None]
    p0, p1, p2, p3 = ext[i], ext[i + 1], ext[i + 2], ext[i + 3]
    return 0.5 * ((2 * p1) + (-p0 + p2) * u + (2 * p0 - 5 * p1 + 4 * p2 - p3) * u ** 2
                  + (-p0 + 3 * p1 - 3 * p2 + p3) * u ** 3)


def spline_even(P, n, cols=3):
    """Catmull-Rom remuestreada a n puntos equiespaciados por longitud (columnas 0..cols-1 = xyz)."""
    P = V(P)
    dense = spline(P, max(200, n * 12))
    seg = np.linalg.norm(np.diff(dense[:, :cols], axis=0), axis=1)
    s = np.concatenate([[0.0], np.cumsum(seg)])
    s /= max(s[-1], 1e-9)
    tgt = np.linspace(0.0, 1.0, n)
    return np.stack([np.interp(tgt, s, dense[:, c]) for c in range(P.shape[1])], 1)


def tangents(C):
    T = np.zeros_like(C)
    T[1:-1] = C[2:] - C[:-2]
    T[0] = C[1] - C[0]
    T[-1] = C[-1] - C[-2]
    return unit(T)


def frames(T, up):
    """Marcos (lado, arriba) por punto de la espina, con 'up' de referencia (vector o lista por punto)."""
    up = V(up)
    if up.ndim == 1:
        up = np.repeat(up[None], len(T), 0)
    side = np.cross(T, up)
    bad = np.linalg.norm(side, axis=1) < 1e-6
    if bad.any():
        side[bad] = np.cross(T[bad], [1.0, 0.0, 0.0])
    side = unit(side)
    upv = unit(np.cross(side, T))
    return side, upv


def rot_matrix(axis, ang_deg):
    a = unit(axis)
    return np.array(Matrix.Rotation(math.radians(ang_deg), 3, Vector(a)))


# =====================================================================================
# Ruido (Perlin 3D vectorizado) para la plastilina y la pintura
# =====================================================================================

_rng = np.random.RandomState(7)
_PERM = np.concatenate([_rng.permutation(256)] * 2)
_GRAD = unit(_rng.normal(size=(256, 3)))


def perlin(p):
    p = V(p)
    pi = np.floor(p).astype(np.int64)
    pf = p - pi
    u = pf * pf * pf * (pf * (pf * 6 - 15) + 10)
    out = np.zeros(len(p))
    xi, yi, zi = pi[:, 0] & 255, pi[:, 1] & 255, pi[:, 2] & 255
    for dx in (0, 1):
        for dy in (0, 1):
            for dz in (0, 1):
                h = _PERM[_PERM[_PERM[(xi + dx) & 255] + ((yi + dy) & 255)] + ((zi + dz) & 255)]
                g = _GRAD[h]
                d = pf - np.array([dx, dy, dz], float)
                w = (u[:, 0] if dx else 1 - u[:, 0]) * (u[:, 1] if dy else 1 - u[:, 1]) * (u[:, 2] if dz else 1 - u[:, 2])
                out += w * (g * d).sum(1)
    return out * 1.6


def fbm(p, freq=1.0, octaves=3, gain=0.5, seed=0):
    """Ruido fractal en [-1, 1] aprox."""
    p = V(p) * freq + np.array([seed * 17.13, seed * 31.7, seed * 7.77])
    out = np.zeros(len(p))
    amp, tot = 1.0, 0.0
    for o in range(octaves):
        out += amp * perlin(p)
        tot += amp
        p = p * 2.03 + 11.5
        amp *= gain
    return np.clip(out / tot, -1.0, 1.0)


# =====================================================================================
# Piezas
# =====================================================================================

class Part:
    """Malla de una pieza: vértices, caras y parámetros por esquina para la pintura.
    a = (u, v, lado): u a lo largo (espina / envergadura), v alrededor / cuerda, lado +1 arriba -1 abajo.
    b = (id, pluma, tramo): coordenada de pluma continua y número de tramo de plumas (0 = ninguno)."""

    def __init__(self, name, verts, faces, ca, cb=None, ddir=None, clay=1.0, paint=None, uv_weight=1.0):
        self.name = name
        self.verts = V(verts)
        self.faces = [tuple(f) for f in faces]
        self.ca = [tuple(map(tuple, c)) for c in ca]
        self.cb = cb if cb is not None else [tuple((0.0, 0.0) for _ in f) for f in self.faces]
        self.ddir = None if ddir is None else V(ddir)
        self.clay = clay
        self.paint = paint
        self.uv_weight = uv_weight
        self.pid = 0
        self.group = 'body'

    def copy(self, name=None):
        p = Part(name or self.name, self.verts.copy(), list(self.faces), list(self.ca), list(self.cb),
                 None if self.ddir is None else self.ddir.copy(), self.clay, self.paint, self.uv_weight)
        p.group = self.group
        return p

    def mirrored(self, name=None):
        """Copia reflejada en X (para el lado derecho)."""
        p = self.copy(name)
        p.verts[:, 0] *= -1
        if p.ddir is not None:
            p.ddir[:, 0] *= -1
        p.faces = [tuple(reversed(f)) for f in p.faces]
        p.ca = [tuple(reversed(c)) for c in p.ca]
        p.cb = [tuple(reversed(c)) for c in p.cb]
        return p

    def transformed(self, M=None, t=(0, 0, 0)):
        p = self.copy()
        if M is not None:
            p.verts = p.verts @ np.asarray(M).T
            if p.ddir is not None:
                p.ddir = p.ddir @ np.asarray(M).T
        p.verts = p.verts + V(t)
        return p

    def size(self):
        return float(np.linalg.norm(self.verts.max(0) - self.verts.min(0)))

    def volume(self):
        v = self.verts
        vol = 0.0
        for f in self.faces:
            for k in range(1, len(f) - 1):
                vol += np.dot(v[f[0]], np.cross(v[f[k]], v[f[k + 1]]))
        return vol / 6.0

    def fix_winding(self):
        if self.volume() < 0:
            self.faces = [tuple(reversed(f)) for f in self.faces]
            self.ca = [tuple(reversed(c)) for c in self.ca]
            self.cb = [tuple(reversed(c)) for c in self.cb]
        return self

    def tri_count(self):
        return sum(len(f) - 2 for f in self.faces)


def _set_cb(part, fcoord=None, fseg=None):
    """Rellena la capa b (pluma/tramo) por vértice."""
    if fcoord is None:
        return
    part.cb = [tuple((fcoord[i], fseg[i]) for i in f) for f in part.faces]


def loft(name, stations, ring=16, samples=16, caps=('round', 'round'), up=(0, 0, 1), expo=2.0,
         cap_len=None, clay=1.0, paint=None, uv_weight=1.0, twist=0.0):
    """Tubo suave a lo largo de una espina.
    stations: lista de (x,y,z,r) | (x,y,z,ancho,alto) | (x,y,z,ancho,alto_arriba,alto_abajo) | (...,expo).
    Se interpolan con Catmull-Rom y se remuestrean por longitud. caps: 'round' | 'point' | 'flat'.
    up: vector de referencia para orientar la sección (alto = hacia 'up')."""
    S = []
    for s in stations:
        s = list(s)
        if len(s) == 4:
            s = s[:3] + [s[3], s[3], s[3], expo]
        elif len(s) == 5:
            s = s[:3] + [s[3], s[4], s[4], expo]
        elif len(s) == 6:
            s = s + [expo]
        S.append(s)
    D = spline_even(S, max(samples, len(S)))
    C, W, HT, HB, E = D[:, :3], np.maximum(D[:, 3], 1e-4), np.maximum(D[:, 4], 1e-4), np.maximum(D[:, 5], 1e-4), D[:, 6]
    T = tangents(C)
    side, upv = frames(T, up)
    if twist:
        tw = np.radians(np.linspace(0, twist, len(C)))[:, None]
        side, upv = side * np.cos(tw) + upv * np.sin(tw), upv * np.cos(tw) - side * np.sin(tw)

    # anillos extra para las tapas redondeadas
    rings_c, rings_s, rings_u, rings_w, rings_ht, rings_hb, rings_e = [list(x) for x in (C, side, upv, W, HT, HB, E)]
    pole0 = pole1 = None
    K = max(2, ring // 4)
    for end in (0, 1):
        cap = caps[end]
        idx = 0 if end == 0 else -1
        sgn = -1.0 if end == 0 else 1.0
        tdir = T[idx] * sgn
        L = cap_len if cap_len is not None else 0.92 * min(W[idx], (HT[idx] + HB[idx]) * 0.5)
        extra_c, extra = [], []
        if cap == 'round':
            for k in range(1, K + 1):
                th = k / (K + 1) * math.pi / 2
                extra.append((C[idx] + tdir * L * math.sin(th), math.cos(th)))
            pole = C[idx] + tdir * L
        elif cap == 'point':
            pole = C[idx] + tdir * L * 0.35
        else:
            pole = C[idx].copy()
        for (c, f) in extra:
            if end == 0:
                rings_c.insert(0, c); rings_s.insert(0, side[idx]); rings_u.insert(0, upv[idx])
                rings_w.insert(0, W[idx] * f); rings_ht.insert(0, HT[idx] * f); rings_hb.insert(0, HB[idx] * f); rings_e.insert(0, E[idx])
            else:
                rings_c.append(c); rings_s.append(side[idx]); rings_u.append(upv[idx])
                rings_w.append(W[idx] * f); rings_ht.append(HT[idx] * f); rings_hb.append(HB[idx] * f); rings_e.append(E[idx])
        if end == 0:
            pole0 = pole
        else:
            pole1 = pole

    R = len(rings_c)
    phi = -math.pi / 2 + 2 * math.pi * np.arange(ring) / ring
    cs, sn = np.cos(phi), np.sin(phi)
    verts = []
    for r in range(R):
        e = 2.0 / rings_e[r]
        x = np.sign(cs) * np.abs(cs) ** e * rings_w[r]
        hz = np.where(sn > 0, rings_ht[r], rings_hb[r])
        z = np.sign(sn) * np.abs(sn) ** e * hz
        verts.append(rings_c[r][None] + x[:, None] * rings_s[r][None] + z[:, None] * rings_u[r][None])
    verts = np.vstack(verts)
    # parámetro u por longitud de la espina (incluye tapas)
    cc = np.vstack([pole0[None], np.array(rings_c), pole1[None]])
    seg = np.concatenate([[0], np.cumsum(np.linalg.norm(np.diff(cc, axis=0), axis=1))])
    useg = seg / max(seg[-1], 1e-9)
    ur = useg[1:-1]
    i0 = len(verts)
    verts = np.vstack([verts, pole0[None], pole1[None]])
    rows = [r * ring for r in range(R)]
    faces, ca = [], []
    for r in range(R - 1):
        a, b = rows[r], rows[r + 1]
        for j in range(ring):
            j1 = (j + 1) % ring
            faces.append((a + j, a + j1, b + j1, b + j))
            ca.append(((ur[r], j / ring, 0), (ur[r], (j + 1) / ring, 0), (ur[r + 1], (j + 1) / ring, 0), (ur[r + 1], j / ring, 0)))
    for j in range(ring):
        j1 = (j + 1) % ring
        faces.append((i0, rows[0] + j1, rows[0] + j))
        ca.append(((0.0, (j + .5) / ring, 0), (ur[0], (j + 1) / ring, 0), (ur[0], j / ring, 0)))
        faces.append((rows[-1] + j, rows[-1] + j1, i0 + 1))
        ca.append(((ur[-1], j / ring, 0), (ur[-1], (j + 1) / ring, 0), (1.0, (j + .5) / ring, 0)))
    p = Part(name, verts, faces, ca, clay=clay, paint=paint, uv_weight=uv_weight)
    return p.fix_winding()


def ellipsoid(name, center, radii, axes=None, rings=10, segs=16, clay=0.0, paint=None, uv_weight=1.0):
    """Elipsoide (polos sobre el eje local Z). axes = matriz 3x3 con columnas X,Y,Z locales."""
    rx, ry, rz = radii
    verts = [(0, 0, -rz)]
    for i in range(1, rings):
        th = -math.pi / 2 + math.pi * i / rings
        for j in range(segs):
            ph = 2 * math.pi * j / segs
            verts.append((rx * math.cos(th) * math.cos(ph), ry * math.cos(th) * math.sin(ph), rz * math.sin(th)))
    verts.append((0, 0, rz))
    verts = V(verts)
    if axes is not None:
        verts = verts @ np.asarray(axes).T
    verts = verts + V(center)
    faces, ca = [], []
    top = len(verts) - 1
    for j in range(segs):
        j1 = (j + 1) % segs
        faces.append((0, 1 + j1, 1 + j))
        ca.append(((0, (j + .5) / segs, 0), (1 / rings, (j + 1) / segs, 0), (1 / rings, j / segs, 0)))
    for i in range(rings - 2):
        a, b = 1 + i * segs, 1 + (i + 1) * segs
        for j in range(segs):
            j1 = (j + 1) % segs
            faces.append((a + j, a + j1, b + j1, b + j))
            u0, u1 = (i + 1) / rings, (i + 2) / rings
            ca.append(((u0, j / segs, 0), (u0, (j + 1) / segs, 0), (u1, (j + 1) / segs, 0), (u1, j / segs, 0)))
    a = 1 + (rings - 2) * segs
    for j in range(segs):
        j1 = (j + 1) % segs
        faces.append((a + j, a + j1, top))
        u0 = (rings - 1) / rings
        ca.append(((u0, j / segs, 0), (u0, (j + 1) / segs, 0), (1, (j + .5) / segs, 0)))
    return Part(name, verts, faces, ca, clay=clay, paint=paint, uv_weight=uv_weight).fix_winding()


def axes_from(z, y_hint=(0, 0, 1)):
    """Matriz con columnas X,Y,Z donde Z = z (normalizado)."""
    z = unit(z)
    x = np.cross(V(y_hint), z)
    if np.linalg.norm(x) < 1e-6:
        x = np.cross([1.0, 0, 0], z)
    x = unit(x)
    y = np.cross(z, x)
    return np.stack([x, y, z], 1)


def torus(name, center, R, r, axis=(0, 0, 1), segs=20, sides=8, clay=0.0, paint=None):
    """Toro (aros de gafas, coronas)."""
    verts, faces, ca = [], [], []
    for i in range(segs):
        a = 2 * math.pi * i / segs
        for j in range(sides):
            b = 2 * math.pi * j / sides
            verts.append(((R + r * math.cos(b)) * math.cos(a), (R + r * math.cos(b)) * math.sin(a), r * math.sin(b)))
    verts = V(verts) @ axes_from(axis).T + V(center)
    for i in range(segs):
        i1 = (i + 1) % segs
        for j in range(sides):
            j1 = (j + 1) % sides
            faces.append((i * sides + j, i1 * sides + j, i1 * sides + j1, i * sides + j1))
            ca.append(((i / segs, j / sides, 0), ((i + 1) / segs, j / sides, 0), ((i + 1) / segs, (j + 1) / sides, 0), (i / segs, (j + 1) / sides, 0)))
    return Part(name, verts, faces, ca, clay=clay, paint=paint).fix_winding()


def blade(name, le, te, thick, n=20, chord=5, feathers=(), camber=0.0, te_round=0.35, up_hint=(0, 0, 1),
          clay=0.6, paint=None, uv_weight=1.0, tip_len=None):
    """Superficie con perfil de ala entre un borde de ataque (le) y uno de salida (te).
    le, te: puntos de control (k, 3) para s = 0..1 (envergadura). thick: medio grosor (escalar o lista k).
    feathers: tramos ((s0, s1, cantidad, profundidad), ...) que festonean el borde de salida en plumas.
    Sirve para alas, colas, plumas sueltas, membranas de patas, crestas planas."""
    le, te = V(le), V(te)
    k = len(le)
    thick = V(thick) if np.ndim(thick) else np.full(k, float(thick))
    camber = V(camber) if np.ndim(camber) else np.full(k, float(camber))
    # muestras en s (más densas en los tramos de plumas, con una muestra en cada muesca)
    base = np.linspace(0, 1, n)
    keep = np.ones(n, bool)
    extra = []
    for (s0, s1, cnt, dep) in feathers:
        keep &= ~((base > s0 - 1e-6) & (base < s1 + 1e-6))
        extra.append(np.linspace(s0, s1, int(cnt) * 4 + 1))
    s = np.unique(np.round(np.concatenate([base[keep]] + extra), 6))
    t = s * (k - 1)
    LE = spline_at(le, t)
    TE = spline_at(te, t)
    TH = np.maximum(spline_at(thick, t)[:, 0], 1e-4)
    CB = spline_at(camber, t)[:, 0]
    dLE = np.gradient(LE, axis=0)
    cdir = unit(TE - LE)
    nrm = unit(np.cross(dLE, cdir))
    if np.dot(nrm.mean(0), V(up_hint)) < 0:
        nrm = -nrm
    fco = np.zeros(len(s))
    fsg = np.zeros(len(s))
    for si, (s0, s1, cnt, dep) in enumerate(feathers):
        inside = (s >= s0 - 1e-6) & (s <= s1 + 1e-6)
        p = (s[inside] - s0) / (s1 - s0) * cnt
        ph = p - np.floor(p)
        ph[np.isclose(p, np.round(p))] = 0.0
        bump = np.sqrt(np.clip(1 - (2 * ph - 1) ** 2, 0, 1))
        TE[inside] += cdir[inside] * (dep * bump)[:, None]
        fco[inside] = p
        fsg[inside] = si + 1
    M = chord
    cc = 0.5 * (1 - np.cos(np.pi * np.arange(M + 1) / M))  # 0..1 con más puntos en el borde de ataque
    tp = lambda c: (1 - te_round) * np.sqrt(c) * (1 - c) / 0.3849 + te_round * np.sqrt(c)
    ring_c = np.concatenate([cc[::-1][:-1], [0.0], cc[1:]])          # TE arriba ... LE ... TE abajo
    ring_side = np.concatenate([np.ones(M), [0.0], -np.ones(M)])
    R = len(ring_c)
    verts, vi_s, vi_c, vi_side = [], [], [], []
    for i in range(len(s)):
        c = ring_c
        off = CB[i] * 4 * c * (1 - c) + ring_side * TH[i] * tp(c)
        pts = LE[i][None] + c[:, None] * (TE[i] - LE[i])[None] + off[:, None] * nrm[i][None]
        verts.append(pts)
    verts = np.vstack(verts)
    ddir = np.repeat(nrm, R, 0)
    faces, ca, cb = [], [], []
    for i in range(len(s) - 1):
        a, b = i * R, (i + 1) * R
        for j in range(R):
            j1 = (j + 1) % R
            faces.append((a + j, a + j1, b + j1, b + j))
            q = [(i, j), (i, j1), (i + 1, j1), (i + 1, j)]
            ca.append(tuple((s[ii], ring_c[jj], ring_side[jj]) for ii, jj in q))
            cb.append(tuple((fco[ii], fsg[ii]) for ii, jj in q))
    # tapas: polo en la raíz y en la punta
    nv = len(verts)
    span0 = unit(LE[0] - LE[1])
    span1 = unit(LE[-1] - LE[-2])
    r0 = verts[:R].mean(0) + span0 * TH[0] * 0.6
    tl = tip_len if tip_len is not None else TH[-1] * 0.8
    r1 = verts[-R:].mean(0) + span1 * tl
    verts = np.vstack([verts, r0[None], r1[None]])
    ddir = np.vstack([ddir, nrm[0][None], nrm[-1][None]])
    last = (len(s) - 1) * R
    for j in range(R):
        j1 = (j + 1) % R
        faces.append((nv, j1, j))
        ca.append(((s[0], 0.5, 0), (s[0], ring_c[j1], ring_side[j1]), (s[0], ring_c[j], ring_side[j])))
        cb.append(((fco[0], fsg[0]),) * 3)
        faces.append((last + j, last + j1, nv + 1))
        ca.append(((s[-1], ring_c[j], ring_side[j]), (s[-1], ring_c[j1], ring_side[j1]), (s[-1], 0.5, 0)))
        cb.append(((fco[-1], fsg[-1]),) * 3)
    p = Part(name, verts, faces, ca, cb, ddir=ddir, clay=clay, paint=paint, uv_weight=uv_weight)
    return p.fix_winding()


# ------------------------------------------------------------------ piezas de ave

def eye(name, center, radius, look, up=(0, 0, 1), pupil=0.47, highlight=0.12, segs=16, paint_sclera=None,
        paint_pupil=None, paint_hl=None):
    """Ojo saltón: esclerótica blanca + pupila negra + brillo. Devuelve 3 piezas."""
    c = V(center)
    look = unit(look)
    sclera = ellipsoid(name + '_blanco', c, (radius, radius, radius), axes_from(look, up), rings=segs // 2 + 2,
                       segs=segs, paint=paint_sclera or paint_flat('#F4F3EE'))
    pr = radius * pupil
    d = math.sqrt(max(radius ** 2 - pr ** 2, 0)) * 0.985
    pup = ellipsoid(name + '_pupila', c + look * d, (pr, pr, pr * 0.42), axes_from(look, up), rings=6, segs=12,
                    paint=paint_pupil or paint_flat('#0B0B10'))
    upp = unit(V(up) - look * np.dot(V(up), look))
    sidep = unit(np.cross(look, upp))
    hd = unit(look * 1.0 + upp * 0.34 + sidep * 0.12)
    hl = ellipsoid(name + '_brillo', c + hd * (radius + pr * 0.12), (radius * highlight,) * 2 + (radius * highlight * 0.5,),
                   axes_from(hd, up), rings=4, segs=8, paint=paint_hl or paint_flat('#FFFFFF'))
    for p in (sclera, pup, hl):
        p.clay = 0.0
    return [sclera, pup, hl]


def beak(name, base, length, width, height, pitch=0.0, hook=0.0, gonys=0.0, lower=0.45, lower_len=0.92,
         split=True, droop=0.0, ring=12, samples=10, paint_upper=None, paint_lower=None, tip_round=0.15):
    """Pico genérico hacia -Y. hook = cuánto baja la punta superior (fracción de la longitud);
    gonys = abultamiento de la mandíbula inferior (gaviota); droop = curva hacia abajo de todo el pico;
    lower = parte de la altura que es mandíbula inferior. split=False -> una sola pieza."""
    b = V(base)
    p = math.radians(pitch)
    f = V((0, -math.cos(p), -math.sin(p)))
    u = V((0, -math.sin(p), math.cos(p)))
    hu = height * (1 - lower)
    hl = height * lower
    parts = []
    ts = np.linspace(0, 1, 7)
    st = []
    for t in ts:
        dz = -droop * length * t * t - hook * length * smoothstep(0.6, 1.0, t) ** 2
        w = width * (1 - t) ** 0.75 + width * tip_round * 0.25
        h = hu * (1 - t * 0.85) ** 0.8 + height * 0.02
        cpos = b + f * length * t + u * (dz + hl * 0.15 * (1 - t))
        st.append(tuple(cpos) + (w, h, max(hl * 0.25 * (1 - t), height * 0.03) if split else hl * (1 - t) ** 0.8 + height * 0.02))
    parts.append(loft(name + '_sup', st, ring=ring, samples=samples, caps=('round', 'point'), up=u,
                      paint=paint_upper))
    if split:
        st2 = []
        L2 = length * lower_len
        for t in ts:
            g = gonys * math.exp(-((t - 0.72) / 0.13) ** 2)
            dz = -droop * L2 * t * t
            w = width * 0.92 * (1 - t) ** 0.8 + width * tip_round * 0.2
            h = hl * (1 - t) ** 0.7 + height * 0.02 + g * height
            cpos = b + f * L2 * t + u * (dz - hl * 0.1)
            st2.append(tuple(cpos) + (w, height * 0.06, h))
        parts.append(loft(name + '_inf', st2, ring=ring, samples=samples, caps=('round', 'point'), up=u,
                          paint=paint_lower or paint_upper))
    return parts


def tail_fan(name, base, base_half, length, end_half, thick=0.03, feathers=6, depth=0.03, roundness=0.03,
             fork=0.0, lift=0.0, chord=4, paint=None):
    """Cola en abanico (fork > 0 la convierte en horquilla: el centro queda más corto)."""
    bx, by, bz = base
    le = [(-base_half, by, bz), (0, by, bz), (base_half, by, bz)]
    ey = by + length
    te = [(-end_half, ey - roundness + fork * 0.0, bz + lift - 0.01),
          (-end_half * 0.5, ey + roundness * 0.6 - fork * 0.55, bz + lift),
          (0, ey + roundness - fork, bz + lift),
          (end_half * 0.5, ey + roundness * 0.6 - fork * 0.55, bz + lift),
          (end_half, ey - roundness, bz + lift - 0.01)]
    le5 = spline(le, 5)
    return blade(name, le5, te, [thick, thick * 0.8, thick, thick * 0.8, thick], n=12, chord=chord,
                 feathers=[(0.0, 1.0, feathers, depth)] if feathers else (), te_round=0.45, paint=paint)


def leg(name, points, radius, ring=8, samples=8, paint=None):
    """Pata/tarso: tubo por los puntos indicados (radio escalar o lista)."""
    r = radius if np.ndim(radius) else [radius] * len(points)
    st = [tuple(p) + (rr,) for p, rr in zip(points, r)]
    return loft(name, st, ring=ring, samples=samples, caps=('round', 'round'), up=(0, -1, 0.3), paint=paint, clay=0.4)


def foot_webbed(name, ankle, direction, length, width, toes=3, thick=0.012, depth=None, paint=None):
    """Pie palmeado plano (membrana con 3 dedos) desde el tobillo hacia 'direction'."""
    a = V(ankle)
    d = unit(direction)
    side = unit(np.cross(d, (0, 0, 1)))
    if np.linalg.norm(np.cross(d, (0, 0, 1))) < 1e-6:
        side = V((1, 0, 0))
    le = [a - side * width * 0.18, a, a + side * width * 0.18]
    te = [a + d * length * 0.82 - side * width * 0.5, a + d * length + side * 0.0, a + d * length * 0.82 + side * width * 0.5]
    dep = depth if depth is not None else length * 0.22
    return blade(name, le, te, [thick, thick * 1.2, thick], n=8, chord=4,
                 feathers=[(0.0, 1.0, toes, dep)], te_round=0.6, paint=paint, clay=0.3,
                 up_hint=(0, 0, 1))


def foot_talons(name, ankle, direction, length, toe_r, claws=True, spread=35, paint_toe=None, paint_claw=None):
    """Garras: 3 dedos hacia delante + 1 atrás, con uña oscura curvada."""
    a = V(ankle)
    d = unit(direction)
    parts = []
    for k, ang in enumerate((-spread, 0, spread, 180)):
        M = rot_matrix((0, 0, 1), ang)
        dd = unit(M @ d)
        L = length * (0.6 if ang == 180 else 1.0)
        p0 = a
        p1 = a + dd * L * 0.55 + V((0, 0, -toe_r * 0.4))
        p2 = a + dd * L * 0.95 + V((0, 0, -toe_r * 1.2))
        parts.append(loft('%s_dedo%d' % (name, k), [tuple(p0) + (toe_r,), tuple(p1) + (toe_r * 0.85,), tuple(p2) + (toe_r * 0.7,)],
                          ring=8, samples=6, up=(0, 0, 1), paint=paint_toe, clay=0.3))
        if claws:
            c0 = p2
            c1 = p2 + dd * toe_r * 1.6 + V((0, 0, -toe_r * 0.8))
            c2 = p2 + dd * toe_r * 2.2 + V((0, 0, -toe_r * 2.4))
            parts.append(loft('%s_una%d' % (name, k), [tuple(c0) + (toe_r * 0.62,), tuple(c1) + (toe_r * 0.4,), tuple(c2) + (toe_r * 0.08,)],
                              ring=8, samples=6, caps=('round', 'point'), up=(0, 0, 1), paint=paint_claw, clay=0.0))
    return parts


def feather(name, base, tip, width, thick=0.012, curl=0.0, up_hint=(0, 0, 1), paint=None, n=8):
    """Pluma suelta / dedo de ala (lanceolada). curl dobla la punta hacia 'up_hint'."""
    b, t = V(base), V(tip)
    d = t - b
    L = np.linalg.norm(d)
    d = d / L
    side = unit(np.cross(V(up_hint), d))
    up = unit(np.cross(d, side))
    ss = np.linspace(0, 1, 5)
    le = [b + d * L * s_ - side * width * 0.5 * math.sin(math.pi * min(s_ * 1.15, 1.0)) ** 0.7 + up * curl * L * s_ ** 2 for s_ in ss]
    te = [b + d * L * s_ + side * width * 0.5 * math.sin(math.pi * min(s_ * 1.15, 1.0)) ** 0.7 + up * curl * L * s_ ** 2 for s_ in ss]
    le[0] = b - side * width * 0.25
    te[0] = b + side * width * 0.25
    le[-1] = t + up * curl * L - side * width * 0.04
    te[-1] = t + up * curl * L + side * width * 0.04
    return blade(name, le, te, [thick, thick, thick * 0.9, thick * 0.7, thick * 0.4], n=n, chord=3,
                 te_round=0.6, up_hint=up, paint=paint, clay=0.3)


def spike(name, base, tip, radius, bend=(0, 0, 0), ring=8, paint=None):
    """Cono curvo (púa de cresta, pluma rígida, uña)."""
    b, t = V(base), V(tip)
    m = (b + t) * 0.5 + V(bend)
    return loft(name, [tuple(b) + (radius,), tuple(m) + (radius * 0.55,), tuple(t) + (radius * 0.08,)], ring=ring,
                samples=7, caps=('round', 'point'), up=(0, 0, 1) if abs(unit(t - b)[2]) < 0.9 else (0, -1, 0),
                paint=paint, clay=0.3)


def crest(name, base_points, tip_points, radius, paint=None):
    """Cresta de púas: una púa por par (base, punta)."""
    return [spike('%s%d' % (name, i), b, t, radius, paint=paint) for i, (b, t) in enumerate(zip(base_points, tip_points))]


def plume(name, points, radius, paint=None):
    """Pluma larga y fina (penacho de la garza) por una curva de puntos."""
    n = len(points)
    st = [tuple(p) + (radius * (1 - 0.85 * i / (n - 1)),) for i, p in enumerate(points)]
    return loft(name, st, ring=8, samples=10, caps=('round', 'point'), up=(0, 0, 1), paint=paint, clay=0.2)


def crown(name, center, radius, height, points=5, axis=(0, 0, 1), paint=None, paint_gem=None):
    """Corona: aro + puntas con bolita (+ gema delante)."""
    c, ax = V(center), unit(axis)
    A = axes_from(ax, (0, -1, 0))
    parts = [loft(name + '_aro', [tuple(c + ax * height * 0.0) + (radius, radius), tuple(c + ax * height * 0.45) + (radius * 1.04, radius * 1.04)],
                  ring=20, samples=4, caps=('flat', 'flat'), up=(0, -1, 0), paint=paint)]
    for i in range(points):
        a = 2 * math.pi * (i + 0.5) / points
        r = A @ V((math.cos(a), math.sin(a), 0))
        b = c + r * radius * 0.98 + ax * height * 0.3
        t = c + r * radius * 1.08 + ax * height
        parts.append(spike('%s_punta%d' % (name, i), b, t, height * 0.28, paint=paint))
        parts.append(ellipsoid('%s_bola%d' % (name, i), t + ax * height * 0.06, (height * 0.12,) * 3, rings=5, segs=8, paint=paint))
    fr = A @ V((0, -1, 0))
    parts.append(ellipsoid(name + '_gema', c + fr * radius * 1.03 + ax * height * 0.22, (height * 0.13, height * 0.13, height * 0.07),
                           axes_from(fr), rings=5, segs=8, paint=paint_gem or paint))
    return parts


def brow(name, a, b, thickness, paint=None):
    """Ceja enfadada: cuña de a (interior) a b (exterior)."""
    a, b = V(a), V(b)
    m = (a + b) / 2 + V((0, 0, thickness * 0.3))
    return loft(name, [tuple(a) + (thickness * 0.9, thickness), tuple(m) + (thickness * 0.8, thickness * 0.8),
                       tuple(b) + (thickness * 0.35, thickness * 0.4)], ring=10, samples=8, caps=('round', 'round'),
                up=(0, 0, 1), paint=paint, clay=0.3)


def glasses(name, left, right, radius, rim=0.08, look=(0, -1, 0), paint_frame=None, paint_lens=None):
    """Gafas redondas: dos aros, dos lentes y el puente."""
    parts = []
    for tag, c in (('I', left), ('D', right)):
        parts.append(torus('%s_aro%s' % (name, tag), c, radius, radius * rim, axis=look, paint=paint_frame))
        parts.append(ellipsoid('%s_lente%s' % (name, tag), c, (radius * 0.97, radius * 0.97, radius * 0.12), axes_from(look),
                               rings=6, segs=16, paint=paint_lens))
    l, r = V(left), V(right)
    mid = (l + r) / 2 + V((0, 0, radius * 0.25))
    parts.append(loft(name + '_puente', [tuple(l + unit(r - l) * radius) + (radius * rim,), tuple(mid) + (radius * rim,),
                                         tuple(r + unit(l - r) * radius) + (radius * rim,)], ring=8, samples=6, up=(0, 0, 1),
                      paint=paint_frame))
    return parts


# =====================================================================================
# Pintura (sobre los mapas horneados: posición, normal y parámetros por texel)
# =====================================================================================

def C(hexstr):
    h = hexstr.lstrip('#')
    return np.array([int(h[i:i + 2], 16) / 255.0 for i in (0, 2, 4)])


class Texels:
    """Datos por texel de una pieza: pos, nrm (n,3), u, v, side, fc (pluma), fseg (tramo), ao."""

    def __init__(self, **kw):
        self.__dict__.update(kw)
        self.n = len(self.pos)

    @property
    def x(self): return self.pos[:, 0]

    @property
    def y(self): return self.pos[:, 1]

    @property
    def z(self): return self.pos[:, 2]

    @property
    def nz(self): return self.nrm[:, 2]

    def fill(self, col):
        return np.repeat(np.asarray(col, float)[None], self.n, 0)


def paint_flat(col):
    c = C(col) if isinstance(col, str) else V(col)
    return lambda T: T.fill(c)


def mix(base, col, m):
    """Mezcla base -> col con máscara m (n,)."""
    col = C(col) if isinstance(col, str) else V(col)
    m = np.clip(np.asarray(m, float), 0, 1)[:, None]
    return base * (1 - m) + col * m


def shade(T, rgb, top=0.06, bottom=0.12):
    """Degradado de luz pintado: más claro arriba, más oscuro abajo."""
    nz = T.nz
    f = 1 + top * np.clip(nz, 0, 1) - bottom * np.clip(-nz, 0, 1)
    return rgb * f[:, None]


def feather_strokes(T, rgb, seg=None, start=0.5, end=1.02, width=0.05, strength=0.35, color=None, both=False):
    """Trazos finos de pluma (como los radios de las aletas del pez) en las separaciones de las plumas."""
    fc = T.fc
    d = np.abs(fc - np.round(fc))
    m = 1 - smoothstep(width * 0.4, width, d)
    m *= smoothstep(start, start + 0.12, T.v) * (1 - smoothstep(end - 0.02, end, T.v))
    if seg is not None:
        m *= np.isin(np.round(T.fseg).astype(int), np.atleast_1d(seg))
    else:
        m *= T.fseg > 0.5
    if not both:
        m *= T.side > -0.5
    m *= strength
    if color is None:
        return rgb * (1 - m[:, None] * 0.75)
    return mix(rgb, color, m)


def grain(T, amount=0.035, freq=260.0):
    return 1 + amount * (0.6 * fbm(T.pos, freq, 1, seed=3) + 0.4 * fbm(T.pos, freq * 0.31, 2, seed=5))


def blotches(T, amount=0.05, freq=3.0):
    return 1 + amount * fbm(T.pos, freq, 3, seed=9)


# =====================================================================================
# Escena, objetos y horneado
# =====================================================================================

def reset_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    sc = bpy.context.scene
    sc.unit_settings.system = 'METRIC'
    sc.unit_settings.scale_length = 1.0
    sc.render.engine = 'CYCLES'
    sc.cycles.device = 'CPU'
    return sc


def _clear_all():
    for o in list(bpy.data.objects):
        bpy.data.objects.remove(o, do_unlink=True)
    for coll in (bpy.data.meshes, bpy.data.materials, bpy.data.images, bpy.data.lights, bpy.data.cameras):
        for d in list(coll):
            if d.users == 0:
                coll.remove(d)


def vertex_normals(verts, faces):
    n = np.zeros_like(verts)
    for f in faces:
        for k in range(1, len(f) - 1):
            a, b, c = verts[f[0]], verts[f[k]], verts[f[k + 1]]
            fn = np.cross(b - a, c - a)
            for i in (f[0], f[k], f[k + 1]):
                n[i] += fn
    return unit(n)


def clay_displace(part, amount=0.012, seed=0):
    """Irregularidad de plastilina: ruido de baja frecuencia (~1-2 % del tamaño) sobre la normal."""
    if part.clay <= 0:
        return part
    sz = max(part.size(), 1e-3)
    d = part.ddir if part.ddir is not None else vertex_normals(part.verts, part.faces)
    nse = fbm(part.verts, 2.2 / sz, 2, seed=seed)
    part.verts = part.verts + d * (nse * amount * part.clay * sz)[:, None]
    return part


def mesh_from_parts(name, parts, offset=(0, 0, 0)):
    """Crea una malla de Blender con las piezas (en orden) y sus capas kit_a / kit_b por esquina."""
    verts, faces, ca, cb = [], [], [], []
    base = 0
    for p in parts:
        verts.append(p.verts - V(offset))
        for f, a, b in zip(p.faces, p.ca, p.cb):
            faces.append(tuple(i + base for i in f))
            ca.append([(x[0], x[1], x[2]) for x in a])
            cb.append([(float(p.pid), x[0], x[1]) for x in b])
        base += len(p.verts)
    verts = np.vstack(verts)
    me = bpy.data.meshes.new(name)
    me.from_pydata(verts.tolist(), [], faces)
    if len(me.polygons) != len(faces):
        raise RuntimeError('malla %s: caras perdidas' % name)
    la = me.attributes.new('kit_a', 'FLOAT_VECTOR', 'CORNER')
    la.data.foreach_set('vector', np.array([c for f in ca for c in f], np.float32).ravel())
    lb = me.attributes.new('kit_b', 'FLOAT_VECTOR', 'CORNER')
    lb.data.foreach_set('vector', np.array([c for f in cb for c in f], np.float32).ravel())
    me.shade_smooth()
    ob = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(ob)
    return ob


def _select_only(objs):
    bpy.ops.object.select_all(action='DESELECT')
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]


def unwrap_atlas(ob, parts, margin=0.006):
    """UV de todas las piezas en un único atlas (smart project + empaquetado)."""
    me = ob.data
    uv = me.uv_layers.new(name='UVMap')
    me.uv_layers.active = uv
    uv.active_render = True
    _select_only([ob])
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.smart_project(angle_limit=math.radians(60), island_margin=0.0, area_weight=0.0,
                             correct_aspect=True, scale_to_bounds=False)
    bpy.ops.object.mode_set(mode='OBJECT')
    uv = me.uv_layers['UVMap']   # la referencia anterior deja de valer al cambiar de modo
    # más resolución a las piezas importantes (cara, ojos)
    uvs = np.zeros(len(me.loops) * 2, np.float32)
    uv.uv.foreach_get('vector', uvs)
    uvs = uvs.reshape(-1, 2)
    li = 0
    fi = 0
    for p in parts:
        nl = sum(len(f) for f in p.faces)
        if p.uv_weight != 1.0:
            s = math.sqrt(p.uv_weight)
            seg = uvs[li:li + nl]
            cen = seg.mean(0)
            uvs[li:li + nl] = cen + (seg - cen) * s
        li += nl
        fi += len(p.faces)
    uv.uv.foreach_set('vector', uvs.ravel())
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.select_all(action='SELECT')
    bpy.ops.uv.pack_islands(rotate=True, scale=True, margin_method='SCALED', margin=margin, shape_method='CONCAVE')
    bpy.ops.object.mode_set(mode='OBJECT')
    uv = me.uv_layers['UVMap']
    uvs = np.zeros(len(me.loops) * 2, np.float32)
    uv.uv.foreach_get('vector', uvs)
    return uvs.reshape(-1, 2)


def _bake_material(ob):
    mat = bpy.data.materials.new('KIT_BAKE')
    nt = mat.node_tree
    for n in list(nt.nodes):
        nt.nodes.remove(n)
    out = nt.nodes.new('ShaderNodeOutputMaterial')
    em = nt.nodes.new('ShaderNodeEmission')
    nt.links.new(em.outputs[0], out.inputs['Surface'])
    tex = nt.nodes.new('ShaderNodeTexImage')
    nt.nodes.active = tex
    ob.data.materials.clear()
    ob.data.materials.append(mat)
    return mat, em, tex


def _emit_source(mat, em, kind):
    nt = mat.node_tree
    for n in list(nt.nodes):
        if n.name.startswith('SRC'):
            nt.nodes.remove(n)
    if kind in ('pos', 'nrm'):
        g = nt.nodes.new('ShaderNodeNewGeometry'); g.name = 'SRC_g'
        vm = nt.nodes.new('ShaderNodeVectorMath'); vm.name = 'SRC_m'
        vm.operation = 'MULTIPLY_ADD'
        if kind == 'pos':
            nt.links.new(g.outputs['Position'], vm.inputs[0])
            vm.inputs[1].default_value = (0.125, 0.125, 0.125)
            vm.inputs[2].default_value = (0.5, 0.5, 0.5)
        else:
            nt.links.new(g.outputs['Normal'], vm.inputs[0])
            vm.inputs[1].default_value = (0.5, 0.5, 0.5)
            vm.inputs[2].default_value = (0.5, 0.5, 0.5)
        nt.links.new(vm.outputs[0], em.inputs['Color'])
    else:
        a = nt.nodes.new('ShaderNodeAttribute'); a.name = 'SRC_a'
        a.attribute_type = 'GEOMETRY'
        a.attribute_name = kind
        vm = nt.nodes.new('ShaderNodeVectorMath'); vm.name = 'SRC_m'
        vm.operation = 'MULTIPLY_ADD'
        nt.links.new(a.outputs['Vector'], vm.inputs[0])
        if kind == 'kit_a':   # (u, v, lado)
            vm.inputs[1].default_value = (0.5, 0.5, 0.25)
            vm.inputs[2].default_value = (0.25, 0.25, 0.5)
        else:                 # (id, pluma, tramo)
            vm.inputs[1].default_value = (1 / 512.0, 1 / 128.0, 1 / 32.0)
            vm.inputs[2].default_value = (0.0, 0.25, 0.0)
        nt.links.new(vm.outputs[0], em.inputs['Color'])


def _img_array(img):
    a = np.zeros(img.size[0] * img.size[1] * 4, np.float32)
    img.pixels.foreach_get(a)
    return a.reshape(img.size[1], img.size[0], 4)


def bake_maps(ob, res=1024, ao_samples=96, ao_distance=0.35, margin=6):
    """Hornea (Cycles) posición, normal, parámetros kit_a/kit_b y oclusión ambiental al atlas."""
    sc = bpy.context.scene
    sc.render.engine = 'CYCLES'
    sc.cycles.device = 'CPU'
    sc.render.bake.margin = margin
    sc.render.bake.margin_type = 'EXTEND'
    sc.render.bake.use_clear = True
    if sc.world is None:
        sc.world = bpy.data.worlds.new('KIT_WORLD')
    sc.world.light_settings.distance = ao_distance
    mat, em, tex = _bake_material(ob)
    _select_only([ob])
    maps = {}
    for kind in ('pos', 'nrm', 'kit_a', 'kit_b', 'ao'):
        img = bpy.data.images.new('KIT_' + kind, res, res, alpha=True, float_buffer=True)
        img.colorspace_settings.name = 'Non-Color'
        img.generated_color = (0, 0, 0, 0)
        tex.image = img
        if kind == 'ao':
            sc.cycles.samples = ao_samples
            bpy.ops.object.bake(type='AO', margin=margin, use_clear=True)
        else:
            sc.cycles.samples = 1
            _emit_source(mat, em, kind)
            bpy.ops.object.bake(type='EMIT', margin=margin, use_clear=True)
        maps[kind] = _img_array(img)
    ob.data.materials.clear()
    bpy.data.materials.remove(mat)
    return maps


def paint_atlas(maps, parts, grain_amt=0.05, blotch_amt=0.07, ao_min=0.58, ao_power=1.0):
    """Pinta el atlas: cada pieza con su función paint(T) + grano + manchas + AO suave."""
    pos = (maps['pos'][..., :3] - 0.5) * 8.0
    nrm = unit(maps['nrm'][..., :3] * 2 - 1)
    ka = maps['kit_a'][..., :3]
    kb = maps['kit_b'][..., :3]
    u, v, side = (ka[..., 0] - 0.25) * 2, (ka[..., 1] - 0.25) * 2, (ka[..., 2] - 0.5) * 4
    pid = np.round(kb[..., 0] * 512).astype(int)
    fc, fseg = (kb[..., 1] - 0.25) * 128, kb[..., 2] * 32
    ao = maps['ao'][..., 0]
    covered = maps['pos'][..., 3] > 0.5
    H, W = covered.shape
    out = np.zeros((H, W, 3))
    for p in parts:
        m = covered & (pid == p.pid)
        if not m.any() or p.paint is None:
            continue
        T = Texels(pos=pos[m], nrm=nrm[m], u=u[m], v=v[m], side=side[m], fc=fc[m], fseg=fseg[m], ao=ao[m])
        out[m] = np.clip(p.paint(T), 0, 1)
    P = pos[covered]
    Tg = Texels(pos=P, nrm=nrm[covered])
    f = grain(Tg, grain_amt) * blotches(Tg, blotch_amt)
    a = np.clip(ao[covered], 0, 1) ** ao_power
    a = ao_min + (1 - ao_min) * a
    out[covered] = np.clip(out[covered] * (f * a)[:, None], 0, 1)
    return dilate(out, covered)


def dilate(img, mask, iters=24):
    """Extiende los colores fuera de las islas (para los mipmaps)."""
    img = img.copy()
    m = mask.copy()
    for _ in range(iters):
        acc = np.zeros_like(img)
        cnt = np.zeros(m.shape)
        for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1), (1, 1), (-1, -1), (1, -1), (-1, 1)):
            sm = np.roll(np.roll(m, dy, 0), dx, 1)
            si = np.roll(np.roll(img, dy, 0), dx, 1)
            acc += si * sm[..., None]
            cnt += sm
        new = (~m) & (cnt > 0)
        img[new] = acc[new] / cnt[new][:, None]
        m = m | new
    if (~m).any():
        img[~m] = img[mask].mean(0)
    return img


def save_png(rgb, path):
    from PIL import Image
    a = (np.clip(rgb, 0, 1) * 255 + 0.5).astype(np.uint8)[::-1]   # Blender guarda de abajo arriba
    Image.fromarray(a, 'RGB').save(path)


def final_material(name, png_path):
    mat = bpy.data.materials.new(name)
    nt = mat.node_tree
    bsdf = nt.nodes.get('Principled BSDF')
    if bsdf is None:
        for n in list(nt.nodes):
            nt.nodes.remove(n)
        out = nt.nodes.new('ShaderNodeOutputMaterial')
        bsdf = nt.nodes.new('ShaderNodeBsdfPrincipled')
        nt.links.new(bsdf.outputs[0], out.inputs['Surface'])
    img = bpy.data.images.load(png_path, check_existing=True)
    img.colorspace_settings.name = 'sRGB'
    tex = nt.nodes.new('ShaderNodeTexImage')
    tex.image = img
    nt.links.new(tex.outputs['Color'], bsdf.inputs['Base Color'])
    bsdf.inputs['Roughness'].default_value = 0.8
    if 'Specular IOR Level' in bsdf.inputs:
        bsdf.inputs['Specular IOR Level'].default_value = 0.3
    return mat


# =====================================================================================
# Ave (jerarquía, textura, exportación, metas de Unity y verificación)
# =====================================================================================

class Bird:
    """Constructor de un ave. Grupos: 'body' (Cuerpo) y 'wing' (ala izquierda; la derecha se refleja)."""

    def __init__(self, name, wing_pivot=(0.14, 0.0, 0.06), feet=None, clay=0.012, seed=1):
        self.name = name
        self.parts = []
        self.wing_pivot = V(wing_pivot)
        self.feet = V(feet) if feet is not None else None   # pie izquierdo; el derecho se refleja
        self.clay = clay
        self.seed = seed
        self.objects = {}
        self.stats = {}

    def add(self, parts, group='body'):
        if isinstance(parts, Part):
            parts = [parts]
        for p in parts:
            p.group = group
            self.parts.append(p)
        return parts

    def _ordered(self):
        body = [p for p in self.parts if p.group == 'body']
        wl = [p for p in self.parts if p.group == 'wing']
        wr = [p.mirrored(p.name + '_D') for p in wl]
        for p in wr:
            p.group = 'wing_r'
        return body, wl, wr

    def build(self):
        """Aplica la plastilina, crea el atlas, y los objetos finales con la jerarquía de Unity."""
        _clear_all()
        for i, p in enumerate(self.parts):
            clay_displace(p, self.clay, seed=self.seed + i)
        body, wl, wr = self._ordered()
        allp = body + wl + wr
        for i, p in enumerate(allp):
            p.pid = i + 1
        if len(allp) > 500:
            raise RuntimeError('demasiadas piezas')
        tmp = mesh_from_parts('KIT_ALL', allp)
        uvs = unwrap_atlas(tmp, allp)
        self._tmp = tmp
        self._all = allp
        self._groups = (body, wl, wr)
        self._uvs = uvs
        return tmp

    def texture(self, out_png, res=1024, **paint_kw):
        maps = bake_maps(self._tmp, res=res)
        self.maps = maps
        rgb = paint_atlas(maps, self._all, **paint_kw)
        save_png(rgb, out_png)
        self.png = out_png
        return out_png

    def assemble(self):
        """Objetos finales: raíz, Cuerpo, L_wing/Ala_I, R_wing/Ala_D, LeftFoot/RightFoot."""
        body, wl, wr = self._groups
        uvs = self._uvs
        mat = final_material('M_' + self.name, self.png)
        root = bpy.data.objects.new(self.name, None)
        bpy.context.scene.collection.objects.link(root)
        root.empty_display_size = 0.3
        li = 0
        piv = self.wing_pivot
        pivr = piv * V((-1, 1, 1))
        specs = [('Cuerpo', body, None, (0, 0, 0)), ('Ala_I', wl, 'L_wing', piv), ('Ala_D', wr, 'R_wing', pivr)]
        for oname, parts, pname, off in specs:
            ob = mesh_from_parts(oname, parts, offset=off)
            nl = len(ob.data.loops)
            uv = ob.data.uv_layers.new(name='UVMap')
            uv.uv.foreach_set('vector', uvs[li:li + nl].ravel())
            li += nl
            for a in ('kit_a', 'kit_b'):
                ob.data.attributes.remove(ob.data.attributes[a])
            ob.data.materials.append(mat)
            if pname:
                e = bpy.data.objects.new(pname, None)
                bpy.context.scene.collection.objects.link(e)
                e.empty_display_size = 0.15
                e.parent = root
                e.location = Vector(off)
                ob.parent = e
            else:
                ob.parent = root
            self.objects[oname] = ob
        if self.feet is not None:
            for nm_, p in (('LeftFoot', self.feet), ('RightFoot', self.feet * V((-1, 1, 1)))):
                e = bpy.data.objects.new(nm_, None)
                bpy.context.scene.collection.objects.link(e)
                e.empty_display_size = 0.05
                e.parent = root
                e.location = Vector(p)
        bpy.data.objects.remove(self._tmp, do_unlink=True)
        self.root = root
        self.stats['triangles'] = sum(p.tri_count() for p in body + wl + wr)
        self.stats['triangles_by_object'] = {'Cuerpo': sum(p.tri_count() for p in body),
                                             'Ala_I': sum(p.tri_count() for p in wl),
                                             'Ala_D': sum(p.tri_count() for p in wr)}
        return root

    def export(self, out_dir):
        os.makedirs(out_dir, exist_ok=True)
        path = os.path.join(out_dir, self.name + '.fbx')
        bpy.ops.export_scene.fbx(filepath=path, object_types={'EMPTY', 'MESH'}, use_selection=False,
                                 apply_scale_options='FBX_SCALE_ALL', bake_space_transform=True,
                                 axis_forward='-Z', axis_up='Y', use_mesh_modifiers=True,
                                 mesh_smooth_type='FACE', add_leaf_bones=False, bake_anim=False,
                                 path_mode='STRIP')
        self.fbx = path
        return path


# ------------------------------------------------------------------ metas de Unity

def load_guids(asset):
    with open(GUIDS_JSON, encoding='utf-8') as f:
        g = json.load(f)
    for k in ('birds', 'characters'):
        if asset in g.get(k, {}):
            return g[k][asset], g.get('folders', {})
    raise KeyError(asset)


def _read(p):
    with open(p, encoding='utf-8') as f:
        return f.read()


def _write(p, s):
    with open(p, 'w', encoding='utf-8', newline='\n') as f:
        f.write(s)


def write_unity_files(asset, out_dir, smoothness=0.2):
    """Escribe .meta del FBX/PNG/carpeta y el material .mat (+meta) con los GUID del registro."""
    g, folders = load_guids(asset)
    src = os.path.join(REPO, 'Assets', 'Models', 'PezNormal')
    # FBX
    m = _read(os.path.join(src, 'PezNormal.fbx.meta'))
    m = re.sub(r'^guid: \w+', 'guid: ' + g['fbx'], m, count=1, flags=re.M)
    ext = ('  externalObjects:\n  - first:\n      type: UnityEngine:Material\n      assembly: UnityEngine.CoreModule\n'
           '      name: M_%s\n    second: {fileID: 2100000, guid: %s, type: 2}\n' % (asset, g['mat']))
    m = re.sub(r'  externalObjects:\n(?:  - first:\n(?:      .*\n)+    second: .*\n)+', ext, m)
    m = m.replace('  animationType: 2\n', '  animationType: 0\n')
    m = m.replace('  importAnimation: 1\n', '  importAnimation: 0\n')
    m = m.replace('    importCameras: 1\n', '    importCameras: 0\n').replace('    importLights: 1\n', '    importLights: 0\n')
    assert 'name: M_%s' % asset in m and 'animationType: 0' in m and 'importAnimation: 0' in m
    _write(os.path.join(out_dir, asset + '.fbx.meta'), m)
    # PNG
    t = _read(os.path.join(src, 'PezNormal_BaseColor.png.meta'))
    t = re.sub(r'^guid: \w+', 'guid: ' + g['png'], t, count=1, flags=re.M)
    t = t.replace('maxTextureSize: 2048', 'maxTextureSize: 1024')
    _write(os.path.join(out_dir, asset + '_BaseColor.png.meta'), t)
    # Material
    mt = _read(os.path.join(src, 'PezNormal.mat'))
    mt = mt.replace('m_Name: PezNormal', 'm_Name: ' + asset)
    mt = re.sub(r'guid: 7df6445764d345f281d9252b7c638b01', 'guid: ' + g['png'], mt)
    mt = re.sub(r'- _Smoothness: [\d.]+', '- _Smoothness: %g' % smoothness, mt)
    mt = re.sub(r'- _Metallic: [\d.]+', '- _Metallic: 0', mt)
    _write(os.path.join(out_dir, asset + '.mat'), mt)
    mm = _read(os.path.join(src, 'PezNormal.mat.meta'))
    mm = re.sub(r'^guid: \w+', 'guid: ' + g['mat'], mm, count=1, flags=re.M)
    _write(os.path.join(out_dir, asset + '.mat.meta'), mm)
    # carpetas
    fm = _read(os.path.join(REPO, 'Assets', 'Models', 'PezNormal.meta'))
    _write(out_dir.rstrip('/') + '.meta', re.sub(r'^guid: \w+', 'guid: ' + g['folder_meta'], fm, count=1, flags=re.M))
    parent = os.path.dirname(out_dir.rstrip('/'))
    rel = os.path.relpath(parent, REPO).replace(os.sep, '/')
    if rel in folders and not os.path.exists(parent + '.meta'):
        _write(parent + '.meta', re.sub(r'^guid: \w+', 'guid: ' + folders[rel], fm, count=1, flags=re.M))
    return g


# ------------------------------------------------------------------ verificación del FBX

def fbx_summary(path):
    """Lee el FBX binario y devuelve ejes, escala, nodos (con transformaciones) y límites en Unity."""
    import struct, zlib
    data = open(path, 'rb').read()
    ver = struct.unpack('<I', data[23:27])[0]
    big = ver >= 7500

    def rd(o):
        if big:
            end, npr, pl = struct.unpack('<QQQ', data[o:o + 24]); o += 24
        else:
            end, npr, pl = struct.unpack('<III', data[o:o + 12]); o += 12
        nl = data[o]; o += 1
        if end == 0:
            return None, o
        name = data[o:o + nl].decode('latin1'); o += nl
        props = []
        po = o
        for _ in range(npr):
            t = chr(data[po]); po += 1
            if t in 'YCIFDL':
                fmt = {'Y': '<h', 'C': '<?', 'I': '<i', 'F': '<f', 'D': '<d', 'L': '<q'}[t]
                sz = struct.calcsize(fmt)
                props.append(struct.unpack(fmt, data[po:po + sz])[0]); po += sz
            elif t in 'fdlib':
                n, enc, cl = struct.unpack('<III', data[po:po + 12]); po += 12
                raw = data[po:po + cl]; po += cl
                if enc == 1:
                    raw = zlib.decompress(raw)
                props.append(np.frombuffer(raw, dtype={'f': '<f4', 'd': '<f8', 'l': '<i8', 'i': '<i4', 'b': '?'}[t]))
            elif t in 'SR':
                n = struct.unpack('<I', data[po:po + 4])[0]; po += 4
                props.append(data[po:po + n]); po += n
        o = o + pl
        ch = []
        while o < end:
            c, o2 = rd(o)
            if c is None:
                o = o2
                break
            ch.append(c)
            o = o2
        return (name, props, ch), end

    o, nodes = 27, []
    while o < len(data) - 200:
        n, o2 = rd(o)
        if n is None:
            break
        nodes.append(n)
        o = o2
    top = {n[0]: n for n in nodes}

    def p70(n):
        out = {}
        for c in n[2]:
            if c[0] == 'Properties70':
                for q in c[2]:
                    out[q[1][0].decode()] = q[1][4:]
        return out

    gs = p70(top['GlobalSettings'])
    objs = top['Objects'][2]
    byid = {o_[1][0]: o_ for o_ in objs}
    links = [(c[1][1], c[1][2]) for c in top['Connections'][2]]
    models, geoms = {}, {}
    for oid, o_ in byid.items():
        if o_[0] == 'Model':
            pr = p70(o_)
            models[oid] = dict(name=o_[1][1].split(b'\x00')[0].decode(), type=o_[1][2].decode(),
                               t=list(pr.get('Lcl Translation', (0, 0, 0))), r=list(pr.get('Lcl Rotation', (0, 0, 0))),
                               s=list(pr.get('Lcl Scaling', (1, 1, 1))), pre=list(pr.get('PreRotation', (0, 0, 0))))
        elif o_[0] == 'Geometry':
            vv = [c for c in o_[2] if c[0] == 'Vertices'][0][1][0].reshape(-1, 3)
            pi = [c for c in o_[2] if c[0] == 'PolygonVertexIndex'][0][1][0]
            geoms[oid] = (vv, pi)
    parent = {}
    geo_of = {}
    for a, b in links:
        if a in models and (b in models or b == 0):
            parent[a] = b
        if a in geoms and b in models:
            geo_of[b] = a
    res = dict(axes={k: gs.get(k, [None])[0] for k in ('UpAxis', 'UpAxisSign', 'FrontAxis', 'FrontAxisSign',
                                                       'CoordAxis', 'CoordAxisSign', 'UnitScaleFactor')}, nodes={})
    allv = []
    for mid, m_ in models.items():
        chain, cur = [], mid
        off = np.zeros(3)
        while cur in models:
            off += np.array(models[cur]['t'])
            chain.append(models[cur]['name'])
            cur = parent.get(cur, 0)
        m_['world_t'] = off.tolist()
        m_['path'] = '/'.join(reversed(chain))
        if mid in geo_of:
            vv, pi = geoms[geo_of[mid]]
            w = vv + off
            m_['bounds_fbx'] = [w.min(0).tolist(), w.max(0).tolist()]
            m_['tris'] = _count_tris(pi)
            allv.append(w)
        res['nodes'][m_['name']] = m_
    if allv:
        A = np.vstack(allv)
        U = A * np.array([-1, 1, 1])  # Unity invierte X
        res['bounds_unity'] = [U.min(0).round(4).tolist(), U.max(0).round(4).tolist()]
        res['size_unity'] = (U.max(0) - U.min(0)).round(4).tolist()
        res['beak_tip_unity'] = U[np.argmax(U[:, 2])].round(4).tolist()
        res['top_unity'] = U[np.argmax(U[:, 1])].round(4).tolist()
    return res


def _count_tris(pi):
    n, cnt = 0, 0
    for v in pi:
        cnt += 1
        if v < 0:
            n += cnt - 2
            cnt = 0
    return n


# =====================================================================================
# Previsualizaciones (Cycles) y hojas de comparación
# =====================================================================================

def import_fbx_clean(path):
    _clear_all()
    bpy.ops.import_scene.fbx(filepath=path)
    obs = {o.name: o for o in bpy.data.objects}
    # si el importador no encontró la textura, se la pone
    png = os.path.splitext(path)[0] + '_BaseColor.png'
    for m in bpy.data.materials:
        has = any(n.type == 'TEX_IMAGE' and n.image for n in m.node_tree.nodes)
        if not has and os.path.exists(png):
            nt = m.node_tree
            bsdf = [n for n in nt.nodes if n.type == 'BSDF_PRINCIPLED'][0]
            tex = nt.nodes.new('ShaderNodeTexImage')
            tex.image = bpy.data.images.load(png, check_existing=True)
            nt.links.new(tex.outputs['Color'], bsdf.inputs['Base Color'])
    return obs


def setup_render(res=512, samples=40, sky=(0.62, 0.68, 0.78), sky_strength=0.9, sun_strength=3.2):
    sc = bpy.context.scene
    sc.render.engine = 'CYCLES'
    sc.cycles.device = 'CPU'
    sc.cycles.samples = samples
    sc.cycles.use_denoising = True
    try:
        sc.cycles.denoiser = 'OPENIMAGEDENOISE'
    except Exception:
        pass
    sc.render.resolution_x = sc.render.resolution_y = res
    sc.render.resolution_percentage = 100
    sc.render.film_transparent = True
    sc.render.image_settings.file_format = 'PNG'
    sc.render.image_settings.color_mode = 'RGBA'
    try:
        sc.view_settings.view_transform = 'Standard'
        sc.view_settings.look = 'None'
    except Exception:
        pass
    w = bpy.data.worlds.new('KIT_SKY')
    sc.world = w
    nt = w.node_tree
    bg = nt.nodes.get('Background')
    if bg is None:
        bg = nt.nodes.new('ShaderNodeBackground')
        out = nt.nodes.new('ShaderNodeOutputWorld')
        nt.links.new(bg.outputs[0], out.inputs[0])
    bg.inputs[0].default_value = sky + (1,)
    bg.inputs[1].default_value = sky_strength
    sun = bpy.data.lights.new('KIT_SUN', 'SUN')
    sun.energy = sun_strength
    sun.angle = math.radians(18)
    so = bpy.data.objects.new('KIT_SUN', sun)
    sc.collection.objects.link(so)
    so.rotation_euler = (math.radians(38), math.radians(-18), math.radians(-30))
    cam = bpy.data.cameras.new('KIT_CAM')
    cam.lens = 50
    co = bpy.data.objects.new('KIT_CAM', cam)
    sc.collection.objects.link(co)
    sc.camera = co
    return co


def scene_bounds():
    pts = []
    for o in bpy.data.objects:
        if o.type == 'MESH':
            for c in o.bound_box:
                pts.append(o.matrix_world @ Vector(c))
    P = np.array([tuple(p) for p in pts])
    return P.min(0), P.max(0)


def look_from(cam, direction, center, radius):
    """Coloca la cámara mirando al centro desde 'direction' (vista cenital: pico hacia arriba)."""
    d = unit(direction)
    fov = 2 * math.atan(18.0 / cam.data.lens)
    dist = radius / math.tan(fov / 2) * 1.08
    cam.location = Vector(V(center) + d * dist)
    if abs(d[2]) > 0.98:
        cam.rotation_euler = (0.0, 0.0, math.pi) if d[2] > 0 else (math.pi, 0.0, 0.0)
    else:
        cam.rotation_euler = Vector(tuple(-d)).to_track_quat('-Z', 'Y').to_euler()


def render(path):
    bpy.context.scene.render.filepath = path
    bpy.ops.render.render(write_still=True)
    return path


VIEWS = {
    'frente': (0, -1, 0.12),
    'lado': (-1, 0, 0.08),
    'arriba': (0, 0, 1),
    'tres_cuartos': (-0.85, -1, 0.55),
    'desde_abajo': (-0.5, -1, -0.75),
    'detras': (0.4, 1, 0.35),
}


def flap(obs, angle_deg):
    """Gira L_wing/R_wing alrededor del eje de avance (Y de Blender) como BirdAI (+ = alas arriba)."""
    for nm_, sgn in (('L_wing', -1), ('R_wing', 1)):
        e = obs.get(nm_)
        if e is None:
            continue
        p = e.matrix_world.translation.copy()
        M = Matrix.Translation(p) @ Matrix.Rotation(math.radians(angle_deg) * sgn, 4, 'Y') @ Matrix.Translation(-p)
        e.matrix_world = M @ e.matrix_world


def render_previews(fbx_path, out_dir, res=512, samples=40, turntable=8):
    """Importa el FBX exportado en una escena limpia y renderiza vistas, giro y prueba de aleteo."""
    os.makedirs(out_dir, exist_ok=True)
    obs = import_fbx_clean(fbx_path)
    cam = setup_render(res, samples)
    lo, hi = scene_bounds()
    center = (lo + hi) / 2
    radius = np.linalg.norm(hi - lo) / 2 * 0.82
    out = {}
    for k, d in VIEWS.items():
        look_from(cam, d, center, radius)
        out[k] = render(os.path.join(out_dir, 'vista_%s.png' % k))
    for i in range(turntable):
        a = 2 * math.pi * i / turntable
        look_from(cam, (math.sin(a), -math.cos(a), 0.45), center, radius)
        out['giro_%d' % i] = render(os.path.join(out_dir, 'giro_%d.png' % i))
    for ang in (30, -30):
        flap(obs, ang)
        for k in ('frente', 'tres_cuartos'):
            look_from(cam, VIEWS[k], center, radius * 1.05)
            out['aleteo_%+d_%s' % (ang, k)] = render(os.path.join(out_dir, 'aleteo_%+d_%s.png' % (ang, k)))
        flap(obs, -ang)
    return out


def _bg(size, top=(70, 92, 124), bottom=(28, 36, 58)):
    from PIL import Image
    w, h = size
    g = np.linspace(0, 1, h)[:, None, None]
    a = (np.array(top)[None, None] * (1 - g) + np.array(bottom)[None, None] * g) * np.ones((1, w, 1))
    return Image.fromarray(a.astype(np.uint8), 'RGB')


def _tile(path, size, label=None, bg=True):
    from PIL import Image, ImageDraw
    im = Image.open(path)
    if im.mode == 'RGBA' and bg:
        base = _bg(im.size)
        base.paste(im, (0, 0), im)
        im = base
    im = im.convert('RGB')
    im.thumbnail((size, size))
    t = Image.new('RGB', (size, size), (20, 22, 30))
    t.paste(im, ((size - im.width) // 2, (size - im.height) // 2))
    if label:
        d = ImageDraw.Draw(t)
        d.rectangle([0, size - 18, size, size], fill=(10, 12, 20))
        d.text((5, size - 15), label, fill=(230, 230, 230))
    return t


def make_sheet(rows, path, size=300, title=None):
    """rows: lista de filas; cada fila lista de (ruta, etiqueta)."""
    from PIL import Image, ImageDraw
    ncol = max(len(r) for r in rows)
    top = 26 if title else 0
    sheet = Image.new('RGB', (ncol * size, len(rows) * size + top), (14, 16, 24))
    if title:
        ImageDraw.Draw(sheet).text((8, 6), title, fill=(240, 240, 240))
    for ri, r in enumerate(rows):
        for ci, (p, lab) in enumerate(r):
            sheet.paste(_tile(p, size, lab), (ci * size, top + ri * size))
    sheet.save(path)
    return path


def crop_image(src, box, dst):
    from PIL import Image
    Image.open(src).crop(box).save(dst)
    return dst
