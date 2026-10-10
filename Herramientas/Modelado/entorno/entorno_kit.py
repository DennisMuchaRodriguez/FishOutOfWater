# -*- coding: utf-8 -*-
"""Kit del entorno (árboles, plantas, rocas, algas y chatarra) con el mismo estilo de plastilina que el pez
y las aves. Se apoya en ../kit.py: piezas, plastilina, atlas, horneado, pintura, FBX, metas y vistas previas.

Ejes (Blender): arriba es +Z, 1 unidad = 1 m y el origen es el pivote: la base que toca el suelo (nenúfares:
la superficie del agua; algas: la raíz). En Unity queda +Y arriba.
Cada modelo es un script en esta carpeta con construir() y run_prop(); build_all.py los genera todos.
"""
import os, sys, math, json

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
import numpy as np
import bpy
import kit
from kit import (V, unit, smoothstep, lerp, spline, fbm, Part, loft, ellipsoid, torus, axes_from, rot_matrix,
                 C, mix, paint_flat, Bird, REPO)

ENTORNO_DIR = os.path.dirname(os.path.abspath(__file__))
PREVIAS = os.environ.get('KIT_PREVIEWS', os.path.join(REPO, '..', 'previas'))


# =====================================================================================
# Formas
# =====================================================================================

def _orient(verts, origin, axes):
    if axes is not None:
        verts = verts @ np.asarray(axes).T
    return verts + V(origin)


def revolve(name, profile, segs=16, origin=(0, 0, 0), axes=None, rmod=None, zmod=None, clay=1.0, paint=None,
            uv_weight=1.0):
    """Superficie de revolución alrededor del eje Z local. profile: [(radio, z), ...] de arriba abajo.
    Los extremos se cierran solos (con un polo). rmod(theta, t, r, z) multiplica el radio y zmod(theta, t, r, z)
    suma a la altura (t = 0..1 a lo largo del perfil; r, z = punto del perfil): festonea el borde de las copas,
    abolla latas, etc.
    Pintura: T.u = t (0 arriba, 1 abajo), T.v = ángulo (0..1)."""
    P = [(float(r), float(z)) for r, z in profile]
    if P[0][0] > 1e-6:
        P.insert(0, (0.0, P[0][1]))
    if P[-1][0] > 1e-6:
        P.append((0.0, P[-1][1]))
    n = len(P)
    # t por longitud del perfil
    L = np.concatenate([[0.0], np.cumsum([math.hypot(P[i + 1][0] - P[i][0], P[i + 1][1] - P[i][1]) for i in range(n - 1)])])
    T = L / max(L[-1], 1e-9)
    th = 2 * math.pi * np.arange(segs) / segs
    verts, rows = [], []
    for i, (r, z) in enumerate(P):
        if i == 0 or i == n - 1:
            rows.append(len(verts))
            verts.append((0.0, 0.0, z))
            continue
        rr = r * np.broadcast_to(rmod(th, T[i], r, z) if rmod else 1.0, th.shape)
        zz = z + np.broadcast_to(zmod(th, T[i], r, z) if zmod else 0.0, th.shape)
        rows.append(len(verts))
        for j in range(segs):
            verts.append((rr[j] * math.cos(th[j]), rr[j] * math.sin(th[j]), zz[j]))
    verts = V(verts)
    faces, ca = [], []
    top, bot = rows[0], rows[-1]
    for j in range(segs):
        j1 = (j + 1) % segs
        a = rows[1]
        faces.append((top, a + j1, a + j))
        ca.append(((T[0], (j + .5) / segs, 0), (T[1], (j + 1) / segs, 0), (T[1], j / segs, 0)))
    for i in range(1, n - 2):
        a, b = rows[i], rows[i + 1]
        for j in range(segs):
            j1 = (j + 1) % segs
            faces.append((a + j, a + j1, b + j1, b + j))
            ca.append(((T[i], j / segs, 0), (T[i], (j + 1) / segs, 0), (T[i + 1], (j + 1) / segs, 0), (T[i + 1], j / segs, 0)))
    for j in range(segs):
        j1 = (j + 1) % segs
        b = rows[n - 2]
        faces.append((b + j, b + j1, bot))
        ca.append(((T[n - 2], j / segs, 0), (T[n - 2], (j + 1) / segs, 0), (T[-1], (j + .5) / segs, 0)))
    p = Part(name, _orient(verts, origin, axes), faces, ca, clay=clay, paint=paint, uv_weight=uv_weight)
    return p.fix_winding()


def cyl(name, a, b, r, segs=10, bevel=0.18, r_end=None, clay=0.3, paint=None):
    """Cilindro de a hasta b con los bordes biselados (latas, tubos, motores, bidones)."""
    a, b = V(a), V(b)
    d = b - a
    L = float(np.linalg.norm(d))
    r1 = r if r_end is None else r_end
    bv = min(r, r1) * bevel
    prof = [(r1 * (1 - bevel), L), (r1, L - bv), (r, bv), (r * (1 - bevel), 0.0)]
    return revolve(name, prof, segs, origin=a, axes=axes_from(d), clay=clay, paint=paint)


def blob(name, center, radii, rings=8, segs=14, lump=0.1, freq=1.5, seed=0, axes=None, floor=None, clay=0.6,
         paint=None, uv_weight=1.0):
    """Elipsoide abultado (copas, arbustos, rocas). lump = relieve (fracción del radio).
    floor: lo que queda por debajo de esa altura se aplasta (se apoya en el suelo)."""
    p = ellipsoid(name, (0, 0, 0), radii, None, rings, segs, clay=clay, paint=paint, uv_weight=uv_weight)
    v = p.verts
    nrm = unit(v / np.maximum(V(radii), 1e-6) ** 2)
    s = float(np.mean(radii))
    d = fbm(v / s, freq, 3, seed=seed)
    v = v + nrm * (d * lump * s)[:, None]
    v = _orient(v, center, axes)
    if floor is not None:
        below = v[:, 2] < floor
        v[below, 2] = floor - (floor - v[below, 2]) * 0.15
    p.verts = v
    return p


def tube(name, pts, radii, ring=8, samples=None, caps=('round', 'round'), up=(0, 0, 1), clay=0.5, paint=None,
         uv_weight=1.0):
    """Tubo por una curva con un radio por punto (troncos, ramas, tallos, caños)."""
    r = radii if np.ndim(radii) else [radii] * len(pts)
    st = [tuple(p) + (rr,) for p, rr in zip(pts, r)]
    return loft(name, st, ring=ring, samples=samples or max(len(pts), 3), caps=caps, up=up, clay=clay, paint=paint,
                uv_weight=uv_weight)


def strap(name, pts, widths, thick, face=(0, 0, 1), ring=4, samples=None, caps=('flat', 'point'), clay=0.25,
          paint=None, twist=0.0):
    """Hoja plana por una curva (hierba, totora, cinta de alga, pétalo). widths: medio ancho por punto;
    thick: medio grosor; face: hacia dónde mira la cara plana."""
    n = len(pts)
    w = widths if np.ndim(widths) else [widths] * n
    t = thick if np.ndim(thick) else [thick] * n
    st = [tuple(p) + (ww, tt) for p, ww, tt in zip(pts, w, t)]
    return loft(name, st, ring=ring, samples=samples or max(n, 3), caps=caps, up=face, clay=clay, paint=paint,
                twist=twist)


def star_disc(name, center, normal, R, petals=5, inner=0.55, thick=0.01, dome=0.25, spin=0.0, cup=0.0, paint=None):
    """Flor plana: disco con pétalos redondeados (abanico arriba y abajo).
    Pintura: T.u = 0 en el centro, 1 en la punta de los pétalos."""
    rim, ru = [], []
    for k in range(petals):
        a = spin + 2 * math.pi * k / petals
        da = math.pi / petals * 0.42
        for ang, rad in ((a - da, R), (a + da, R), (a + math.pi / petals, R * inner)):
            rim.append((rad * math.cos(ang), rad * math.sin(ang), cup * R * (rad / R) ** 2))
            ru.append(rad / R)
    m = len(rim)
    verts = [(0.0, 0.0, thick + dome * R * 0.35), (0.0, 0.0, -thick)] + rim
    faces, ca = [], []
    for j in range(m):
        j1 = (j + 1) % m
        faces.append((0, 2 + j, 2 + j1))
        ca.append(((0.0, (j + .5) / m, 1), (ru[j], j / m, 1), (ru[j1], (j + 1) / m, 1)))
        faces.append((1, 2 + j1, 2 + j))
        ca.append(((0.0, (j + .5) / m, -1), (ru[j1], (j + 1) / m, -1), (ru[j], j / m, -1)))
    p = Part(name, _orient(V(verts), center, axes_from(normal)), faces, ca, clay=0.15, paint=paint)
    return p.fix_winding()


def pad(name, center, R, thick=0.025, segs=16, notch=26.0, rot=0.0, wave=0.012, seed=0, paint=None):
    """Hoja de nenúfar: disco con muesca, la base en z = center.z. T.u = 0 centro .. 1 borde; T.side +1 arriba."""
    rng = np.random.RandomState(seed)
    a0 = math.radians(notch) * 0.5
    angs = rot + a0 + (2 * math.pi - 2 * a0) * np.arange(segs + 1) / segs
    ph = rng.uniform(0, 6.28)
    rim = [(R * math.cos(a), R * math.sin(a), thick * 0.45 + wave * math.sin(3 * a + ph)) for a in angs]
    verts = [(0.0, 0.0, thick), (0.0, 0.0, 0.0)] + rim
    m = len(rim)
    faces, ca = [], []
    for j in range(m - 1):
        faces.append((0, 2 + j, 3 + j))
        ca.append(((0.0, (j + .5) / m, 1), (1.0, j / m, 1), (1.0, (j + 1) / m, 1)))
        faces.append((1, 3 + j, 2 + j))
        ca.append(((0.0, (j + .5) / m, -1), (1.0, (j + 1) / m, -1), (1.0, j / m, -1)))
    # lados de la muesca
    faces.append((0, 1, 2)); ca.append(((0.0, 0.0, 1), (0.0, 0.0, -1), (1.0, 0.0, 0)))
    faces.append((0, 1 + m, 1)); ca.append(((0.0, 1.0, 1), (1.0, 1.0, 0), (0.0, 1.0, -1)))
    p = Part(name, V(verts) + V(center), faces, ca, clay=0.1, paint=paint)
    return p.fix_winding()


def _spow(x, e):
    return np.sign(x) * np.abs(x) ** e


def rounded_box(name, center, size, e=0.3, rings=8, segs=16, axes=None, clay=0.2, paint=None, uv_weight=1.0):
    """Caja de bordes redondeados (superelipsoide). size = medidas completas; e: 0.2 caja .. 1 elipsoide."""
    rx, ry, rz = V(size) * 0.5
    verts = [(0.0, 0.0, -rz)]
    for i in range(1, rings):
        th = -math.pi / 2 + math.pi * i / rings
        for j in range(segs):
            ph = 2 * math.pi * j / segs + math.pi / segs
            ce, se = _spow(math.cos(th), e), _spow(math.sin(th), e)
            verts.append((rx * ce * _spow(math.cos(ph), e), ry * ce * _spow(math.sin(ph), e), rz * se))
    verts.append((0.0, 0.0, rz))
    q = ellipsoid(name, (0, 0, 0), (1, 1, 1), None, rings, segs, clay=clay, paint=paint, uv_weight=uv_weight)
    q.verts = _orient(V(verts), center, axes)
    return q.fix_winding()


# =====================================================================================
# Pintura
# =====================================================================================

def nse(T, freq=1.0, seed=0, octaves=2):
    return fbm(T.pos, freq, octaves, seed=seed)


def light(T, rgb, top=0.14, bottom=0.3):
    """Luz pintada: lo que mira arriba más claro, lo de abajo más oscuro."""
    nz = T.nz
    f = 1 + top * np.clip(nz, 0, 1) - bottom * np.clip(-nz, 0, 1)
    return rgb * f[:, None]


def grad(T, c0, c1, t):
    """Degradado de c0 a c1 con t (n,) en 0..1."""
    return mix(T.fill(C(c0) if isinstance(c0, str) else V(c0)), c1, t)


def moss(T, rgb, col='#6F8F3F', amount=1.0, up=0.45, freq=0.9, seed=5):
    """Musgo en lo que mira hacia arriba, en manchas."""
    m = smoothstep(up, up + 0.35, T.nz) * smoothstep(-0.25, 0.2, nse(T, freq, seed, 3)) * amount
    return mix(rgb, col, m)


def spots(T, rgb, col, freq=6.0, thresh=0.35, soft=0.1, seed=11, amount=1.0):
    """Manchas sueltas (óxido, líquenes, puntos de flores)."""
    m = smoothstep(thresh, thresh + soft, nse(T, freq, seed, 2)) * amount
    return mix(rgb, col, m)


def film(T, rgb, col='#5E7A4E', z0=0.0, z1=0.25, amount=0.6, freq=3.0, seed=21):
    """Película de algas/limo desde el fondo hasta z1 (chatarra hundida)."""
    m = (1 - smoothstep(z0, z1, T.z + 0.08 * nse(T, freq, seed))) * amount
    return mix(rgb, col, m)


def strokes(T, rgb, count=14, coord='v', width=0.18, strength=0.25, wobble=0.04, seed=3, color=None):
    """Trazos finos a lo largo de la pieza (corteza, nervios): rayas en la coordenada indicada."""
    c = getattr(T, coord)
    w = c * count + wobble * count * nse(T, 2.0, seed)
    d = np.abs(w - np.round(w))
    m = (1 - smoothstep(width * 0.3, width, d)) * strength
    if color is None:
        return rgb * (1 - m[:, None] * 0.7)
    return mix(rgb, color, m)


# =====================================================================================
# Construcción, exportación y vistas previas
# =====================================================================================

def prop(name, clay=0.012, seed=1):
    """Nuevo modelo de entorno (una sola malla 'Malla' bajo la raíz <Nombre>)."""
    return Bird(name, wing_pivot=(0, 0, 0), feet=None, clay=clay, seed=seed, body_name='Malla')


def run_prop(asset, construir, res=1024, smoothness=0.15, instancing=True, readable=False, emission=None,
             ao_distance=0.35, ao_samples=64, bake_range=4.0, grain=0.05, blotch=0.07, ao_min=0.58, previas=None,
             views=None):
    """Construye, pinta, exporta y verifica un modelo de entorno; deja una hoja de vistas previas.
    emission = color HDR (r, g, b) del material si alguna pieza tiene emit(T)."""
    if previas is None:
        previas = '--sin-previas' not in sys.argv
    kit.BAKE_RANGE = bake_range
    out_dir = os.path.join(REPO, 'Assets', 'Models', 'Entorno', asset)
    os.makedirs(out_dir, exist_ok=True)
    kit.reset_scene()
    b = construir()
    b.build()
    png = os.path.join(out_dir, asset + '_BaseColor.png')
    em_png = os.path.join(out_dir, asset + '_Emission.png') if emission is not None else None
    b.texture(png, res=res, ao_distance=ao_distance, ao_samples=ao_samples, emission_png=em_png,
              grain_amt=grain, blotch_amt=blotch, ao_min=ao_min)
    b.assemble()
    fbx = b.export(out_dir)
    kit.write_unity_files(asset, out_dir, smoothness=smoothness, instancing=instancing, readable=readable,
                          emission=emission if b.emission_png else None, max_size=res)
    info = fbx_info(fbx)
    info['triangles'] = b.stats['triangles']
    print('TRIANGULOS', asset, b.stats['triangles'])
    print('TAMANO_UNITY', asset, info.get('size_unity'), 'LIMITES', info.get('bounds_unity'))
    prev = os.path.join(PREVIAS, 'Entorno', asset)
    os.makedirs(prev, exist_ok=True)
    with open(os.path.join(prev, 'resumen.json'), 'w', encoding='utf-8') as f:
        json.dump(info, f, indent=1)
    if previas:
        hoja = previews(fbx, prev, asset, views=views)
        print('HOJA', hoja)
    return info


def fbx_info(fbx):
    info = kit.fbx_summary(fbx)
    nodes = {k: {'path': n['path'], 't': [round(x, 4) for x in n['world_t']], 'tris': n.get('tris')}
             for k, n in info['nodes'].items()}
    return {'axes': info['axes'], 'nodes': nodes, 'bounds_unity': info.get('bounds_unity'),
            'size_unity': info.get('size_unity')}


def _hook_emission(fbx):
    em = os.path.splitext(fbx)[0] + '_Emission.png'
    if not os.path.exists(em):
        return
    img = bpy.data.images.load(em, check_existing=True)
    for m in bpy.data.materials:
        nt = m.node_tree
        if nt is None:
            continue
        bsdf = [n for n in nt.nodes if n.type == 'BSDF_PRINCIPLED']
        if not bsdf:
            continue
        tex = nt.nodes.new('ShaderNodeTexImage')
        tex.image = img
        nt.links.new(tex.outputs['Color'], bsdf[0].inputs['Emission Color'])
        bsdf[0].inputs['Emission Strength'].default_value = 2.5


VIEWS = [('tres_cuartos', (-0.85, -1, 0.55)), ('frente', (0, -1, 0.1)), ('arriba', (0, 0, 1)),
         ('desde_abajo', (-0.5, -1, -0.7))]


def previews(fbx, out_dir, title, res=400, samples=20, views=None):
    """Vistas del FBX exportado (reimportado en una escena limpia) y una hoja con todas."""
    kit.import_fbx_clean(fbx)
    _hook_emission(fbx)
    cam = kit.setup_render(res, samples)
    lo, hi = kit.scene_bounds()
    center = (lo + hi) / 2
    radius = np.linalg.norm(hi - lo) / 2 * 0.82
    tiles = []
    for k, d in (views or VIEWS):
        kit.look_from(cam, d, center, radius)
        tiles.append((kit.render(os.path.join(out_dir, 'vista_%s.png' % k)), k))
    return kit.make_sheet([tiles], os.path.join(out_dir, 'hoja.png'), size=300, title=title)
