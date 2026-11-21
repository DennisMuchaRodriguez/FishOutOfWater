# -*- coding: utf-8 -*-
"""Viñetas del modo Historia, paso 1 de 2: arma cada escena con los modelos del juego y la renderiza con Blender
(Cycles en CPU) en 4 pasadas, que el paso 2 (vinetas.py) convierte en un boceto a lápiz:
  <id>_luz.png      todo de arcilla gris con la luz de la escena (sombreado a rayas)
  <id>_color.png    el color propio de cada cosa, sin luz (aguada apagada); transparente = cielo
  <id>_normal.png   normales como color (contornos y pliegues)
  <id>_clase.png    R = qué es (entorno, agua, pez, ave, mecánico, brillo, héroe), G = qué objeto, B = cercanía

    <python con bpy> Herramientas/Comics/render_vinetas.py <carpeta> [borrador|final] [id ...]
(desde la raíz del proyecto; pip install bpy). Las viñetas, su ambiente y sus textos están en guion.py; cada escena,
abajo en ESCENAS. Después: python3 Herramientas/Comics/vinetas.py <carpeta>.
"""
import bpy, os, sys, math, random, time
from mathutils import Vector, Euler, Matrix

ROOT = os.getcwd()
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from guion import VINETAS, AMBIENTE

W, H = 1380, 660
MODELS = os.path.join(ROOT, 'Assets', 'Models')
ENT = os.path.join(MODELS, 'Entorno')

# Clases (pass_index del objeto; en la pasada de clases R = clase / 100)
ENTORNO, AGUA, PEZ, AVE, MECANICO, BRILLO, HEROE = 10, 25, 50, 62, 75, 85, 100

# ======================================================================= prototipos
# Cada modelo se importa una sola vez y se "hornea" en mallas sueltas (sin armadura) en metros; cada vez que se
# usa, se crean objetos nuevos que comparten esas mallas (rápido y liviano).
PROTO = {}


def import_baked(path):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=path)
    new = [o for o in bpy.data.objects if o not in before]
    bpy.context.view_layer.update()
    deps = bpy.context.evaluated_depsgraph_get()
    parts = []
    for o in new:
        if o.type != 'MESH':
            continue
        ev = o.evaluated_get(deps)
        me = bpy.data.meshes.new_from_object(ev, preserve_all_data_layers=True, depsgraph=deps)
        me.transform(o.matrix_world)
        mats = [s.material.name if s.material else '' for s in o.material_slots]
        parts.append((o.name, me, mats))
    for o in new:
        bpy.data.objects.remove(o, do_unlink=True)
    return parts


def bbox(meshes):
    lo = Vector((1e9,) * 3)
    hi = Vector((-1e9,) * 3)
    for me in meshes:
        for v in me.vertices:
            lo = Vector(map(min, lo, v.co))
            hi = Vector(map(max, hi, v.co))
    return lo, hi


def centroid(me):
    c = Vector()
    for v in me.vertices:
        c += v.co
    return c / max(1, len(me.vertices))


def set_src(me, src):
    me['fow_src'] = src


def proto_entorno(name, folder=ENT):
    key = 'ent:' + name
    if key not in PROTO:
        base = os.path.join(folder, name, name)
        parts = import_baked(base + '.fbx')
        emit = os.path.exists(base + '_Emission.png')
        for _, me, _ in parts:
            set_src(me, ('emit:' if emit else 'tex:') + base + '_BaseColor.png')
        PROTO[key] = [(n, me) for n, me, _ in parts]
    return PROTO[key]


def proto_ave(name, wings=0.0):
    """Ave centrada, mirando hacia -Y, en metros. wings: grados que suben (o bajan, negativo) las alas."""
    key = 'ave:%s:%d' % (name, round(wings))
    if key in PROTO:
        return PROTO[key]
    base_key = 'ave:%s:0' % name
    if base_key not in PROTO:
        base = os.path.join(MODELS, 'Aves', name, name)
        parts = import_baked(base + '.fbx')
        lo, hi = bbox([me for _, me, _ in parts])
        c = (lo + hi) / 2
        for _, me, _ in parts:
            me.transform(Matrix.Translation(-c))
            set_src(me, 'tex:' + base + '_BaseColor.png')
        PROTO[base_key] = [(n, me) for n, me, _ in parts]
    if round(wings) == 0:
        return PROTO[base_key]
    out = []
    for n, me in PROTO[base_key]:
        if 'wing' not in n.lower():
            out.append((n, me))
            continue
        m2 = me.copy()
        lo, hi = bbox([m2])
        side = 1.0 if (lo.x + hi.x) > 0 else -1.0
        pivot = Vector((lo.x if side > 0 else hi.x, (lo.y + hi.y) / 2, (lo.z + hi.z) / 2))
        rot = Matrix.Rotation(math.radians(-wings * side), 4, 'Y')
        m2.transform(Matrix.Translation(pivot) @ rot @ Matrix.Translation(-pivot))
        out.append((n, m2))
    PROTO[key] = out
    return out


def fish_parts(path, body_src, armor_src=None):
    """Pez (con o sin traje): es un personaje parado (cabeza arriba, cola abajo). Centrado, de 1 m de alto, con la
    cara (los ojos) hacia -Y."""
    parts = import_baked(path)
    meshes = [me for _, me, _ in parts]
    lo, hi = bbox(meshes)
    c = (lo + hi) / 2
    eyes = [me for n, me, _ in parts if n.lower().startswith('ojos')]
    f = (centroid(eyes[0]) - c) if eyes else Vector((0, -1, 0))
    f.z = 0
    f = f.normalized() if f.length > 1e-6 else Vector((0, -1, 0))
    ang = math.atan2(f.x, -f.y)          # gira para que la cara quede hacia -Y
    rot = Matrix.Rotation(ang, 4, 'Z')
    length = max(hi.x - lo.x, hi.y - lo.y, hi.z - lo.z)
    k = 1.0 / length
    out = []
    for n, me, mats in parts:
        me.transform(Matrix.Scale(k, 4) @ rot @ Matrix.Translation(-c))
        mname = mats[0] if mats else ''
        if mname.startswith('Glass'):
            set_src(me, 'glass')
        elif mname.startswith('aiStandardSurface2') and armor_src:
            set_src(me, armor_src)
        else:
            set_src(me, body_src)
        out.append((n, me, mname))
    return out


def proto_heroe(traje=True):
    key = 'heroe:%d' % traje
    if 'heroe:full' not in PROTO:
        tex = os.path.join(MODELS, 'Texturas')
        PROTO['heroe:full'] = fish_parts(os.path.join(MODELS, 'Animations', 'PESCAO.fbx'),
                                         'tex:' + os.path.join(tex, 'Body', 'Pez_F_Model_Body_BaseColor.png'),
                                         'tex:' + os.path.join(tex, 'Armor', 'Pez_F_Model_Armor_BaseColor.png'))
    if key not in PROTO:
        full = PROTO['heroe:full']
        PROTO[key] = [(n, me) for n, me, m in full if traje or not (m.startswith('Glass') or m.startswith('aiStandardSurface2'))]
    return PROTO[key]


def proto_pez(variante='PezNormal'):
    key = 'pez:' + variante
    if key not in PROTO:
        tex = os.path.join(MODELS, 'PezNormal', variante + '_BaseColor.png')
        PROTO[key] = [(n, me) for n, me, _ in fish_parts(os.path.join(MODELS, 'PezNormal', 'PezNormal.fbx'), 'tex:' + tex)]
    return PROTO[key]


# ======================================================================= la viñeta en armado
SCENE_OBJS = []


def link(o):
    bpy.context.scene.collection.objects.link(o)
    SCENE_OBJS.append(o)
    return o


def instance(parts, pos=(0, 0, 0), rot=(0, 0, 0), scale=1.0, cls=ENTORNO, name='obj'):
    root = link(bpy.data.objects.new(name, None))
    root.location = Vector(pos)
    root.rotation_euler = Euler([math.radians(a) for a in rot], 'ZXY') if not isinstance(rot, Euler) else rot
    root.scale = (scale,) * 3 if not isinstance(scale, (tuple, list)) else scale
    for n, me in parts:
        o = link(bpy.data.objects.new(n, me))
        o.parent = root
        o.pass_index = cls
    return root


def look_rot(direction, up=(0, 0, 1), bank=0.0, pitch_extra=0.0):
    """Euler para que un modelo que mira hacia -Y mire hacia 'direction' (con alabeo 'bank' en grados)."""
    d = Vector(direction).normalized()
    q = d.to_track_quat('-Y', 'Z')
    q = q @ Euler((math.radians(pitch_extra), math.radians(bank), 0)).to_quaternion()
    return q.to_euler('ZXY')


def heroe(pos, direction=(0, -1, 0), largo=1.0, traje=True, bank=0.0, llamas=False, cls=HEROE, name='Heroe', pitch=0.0):
    """El pez (parado: cabeza arriba). direction: hacia dónde mira; pitch: se inclina hacia adelante (grados)."""
    r = instance(proto_heroe(traje), pos, look_rot(direction, bank=bank, pitch_extra=-pitch), largo, cls, name)
    if llamas:
        # Fuego del propulsor: dos lenguas cortas que salen de la mochila (atrás) hacia abajo
        for s in (-1, 1):
            m = cone_mesh(0.045, 0.0, 0.26, 12)
            set_src(m, 'emit:rgb:1.0,0.55,0.15')
            o = link(bpy.data.objects.new('Llama', m))
            o.parent = r
            o.location = (s * 0.1, 0.3, -0.2)
            o.rotation_euler = (math.radians(180 + 25), 0, 0)
            o.pass_index = BRILLO
    return r


def pez(pos, direction=(0, -1, 0), largo=0.45, variante='PezNormal', bank=0.0, name='Pez', pitch=0.0):
    return instance(proto_pez(variante), pos, look_rot(direction, bank=bank, pitch_extra=-pitch), largo, PEZ, name)


VARIANTES = ['PezNormal', 'PezDorado', 'PezRosa', 'PezTurquesa', 'PezVerde', 'PezVioleta']


def cardumen(center, n, radio, direction=(0, -1, 0), rng=None, largo=0.4, vertical=0.4, desorden=25.0):
    rng = rng or random.Random(3)
    d0 = Vector(direction).normalized()
    for i in range(n):
        a = rng.uniform(0, math.tau)
        r = radio * math.sqrt(rng.random())
        p = Vector(center) + Vector((math.cos(a) * r, math.sin(a) * r, rng.uniform(-vertical, vertical)))
        d = (Matrix.Rotation(math.radians(rng.uniform(-desorden, desorden)), 3, 'Z') @ d0)
        d.z += rng.uniform(-0.15, 0.15)
        pez(p, d, largo * rng.uniform(0.8, 1.2), VARIANTES[rng.randrange(len(VARIANTES))])


def ave(name, pos, direction, envergadura, bank=0.0, alas=0.0, cls=AVE, pitch=0.0):
    parts = proto_ave(name, alas)
    lo, hi = bbox([me for _, me in parts])
    k = envergadura / max(0.01, hi.x - lo.x)
    return instance(parts, pos, look_rot(direction, bank=bank, pitch_extra=pitch), k, cls, name)


def modelo(name, pos, rot_z=0.0, scale=1.0, cls=ENTORNO, tilt=(0.0, 0.0), folder=ENT):
    return instance(proto_entorno(name, folder), pos, (rot_z, tilt[0], tilt[1]), scale, cls, name)


def mecanico(pos, direction=(0, -1, 0), scale=1.0):
    """El bagre mecánico (parado sobre la cola). direction: hacia dónde mira su cara."""
    return instance(proto_entorno('Mecanico_Bagre', os.path.join(MODELS, 'Taller')), pos,
                    look_rot(direction), scale, MECANICO, 'Mecanico')


# ======================================================================= geometría propia
def mesh_obj(name, verts, faces, src, cls=ENTORNO, smooth=True):
    me = bpy.data.meshes.new(name)
    me.from_pydata(verts, [], faces)
    me.update()
    if smooth:
        for p in me.polygons:
            p.use_smooth = True
    set_src(me, src)
    o = link(bpy.data.objects.new(name, me))
    o.pass_index = cls
    return o


def cone_mesh(r0, r1, h, seg=16):
    verts = []
    faces = []
    for i in range(seg):
        a = i / seg * math.tau
        verts.append((math.cos(a) * r0, math.sin(a) * r0, 0))
        verts.append((math.cos(a) * r1, math.sin(a) * r1, h))
    for i in range(seg):
        j = (i + 1) % seg
        faces.append((i * 2, j * 2, j * 2 + 1, i * 2 + 1))
    me = bpy.data.meshes.new('Cono')
    me.from_pydata(verts, [], faces)
    me.update()
    for p in me.polygons:
        p.use_smooth = True
    return me


def fbm(x, y, seed=0, oct=4):
    v = 0.0
    a = 0.5
    f = 1.0
    for i in range(oct):
        v += a * math.sin(x * 0.11 * f + seed * 1.7 + i) * math.cos(y * 0.13 * f - seed * 0.9 + i * 2.1)
        a *= 0.5
        f *= 2.03
    return v


def grid(name, size, n, height_fn, src, cls=ENTORNO, center=(0, 0)):
    verts = []
    faces = []
    for j in range(n + 1):
        for i in range(n + 1):
            x = center[0] + (i / n - 0.5) * size
            y = center[1] + (j / n - 0.5) * size
            verts.append((x, y, height_fn(x, y)))
    for j in range(n):
        for i in range(n):
            a = j * (n + 1) + i
            faces.append((a, a + 1, a + n + 2, a + n + 1))
    return mesh_obj(name, verts, faces, src, cls)


def agua(z=0.0, size=1200.0, desde_abajo=False):
    o = grid('Agua', size, 1, lambda x, y: z, 'agua_abajo' if desde_abajo else 'agua', AGUA)
    o.visible_shadow = False
    return o


def orilla(lago_r=60.0, seed=1, alto=5.0, centro=(0, 0), size=520.0):
    """Terreno con la cuenca del lago (radio lago_r) y lomas alrededor."""
    cx, cy = centro

    def h(x, y):
        d = math.hypot(x - cx, y - cy)
        w = 1.0 + 0.18 * fbm(x * 0.4, y * 0.4, seed)
        e = d / (lago_r * w)
        base = -2.5 + 3.2 * max(0.0, min(1.0, (e - 0.92) / 0.18)) ** 1.5
        loma = max(0.0, e - 1.1) * alto * (0.8 + 0.6 * fbm(x, y, seed + 3))
        return base + loma
    return grid('Orilla', size, 140, h, 'rgb:0.42,0.5,0.32', ENTORNO, centro)


def bosque(rng, lago_r=60.0, n=90, rmin=8.0, rmax=60.0, centro=(0, 0), angulos=(0, 360), altura_fn=None):
    tipos = ['Pino_A', 'Pino_B', 'Abeto', 'Abedul', 'Arbol_Hoja', 'Sauce']
    for i in range(n):
        a = math.radians(rng.uniform(*angulos))
        r = lago_r + rng.uniform(rmin, rmax)
        x = centro[0] + math.cos(a) * r
        y = centro[1] + math.sin(a) * r
        z = altura_fn(x, y) if altura_fn else 0.5
        modelo(tipos[rng.randrange(len(tipos))], (x, y, z - 0.3), rng.uniform(0, 360), rng.uniform(0.75, 1.25))


def juncos(rng, centro, n, radio, z=-0.2):
    for i in range(n):
        a = rng.uniform(0, math.tau)
        r = radio * math.sqrt(rng.random())
        modelo('Juncos' if rng.random() < 0.6 else 'Totora',
               (centro[0] + math.cos(a) * r, centro[1] + math.sin(a) * r, z), rng.uniform(0, 360), rng.uniform(0.8, 1.4))


def nenufares(rng, centro, n, radio):
    for i in range(n):
        a = rng.uniform(0, math.tau)
        r = radio * math.sqrt(rng.random())
        modelo('Nenufar', (centro[0] + math.cos(a) * r, centro[1] + math.sin(a) * r, 0.02), rng.uniform(0, 360), rng.uniform(0.7, 1.3))


def lago(rng, lago_r=60.0, arboles=110, seed=1):
    """Lago visto desde el agua: agua, orillas con lomas, bosque y juncos en la orilla."""
    agua(0.0)
    t = orilla(lago_r, seed)
    bosque(rng, lago_r, arboles, 6.0, 70.0, altura_fn=lambda x, y: height_at(t, x, y))
    for i in range(14):
        a = rng.uniform(0, math.tau)
        juncos(rng, (math.cos(a) * (lago_r * 0.97), math.sin(a) * (lago_r * 0.97)), 5, 3.0)
    return t


def height_at(obj, x, y):
    # Altura del terreno (grid) por el vértice más cercano
    me = obj.data
    n = int(round(math.sqrt(len(me.vertices)))) - 1
    v0 = me.vertices[0].co
    v1 = me.vertices[len(me.vertices) - 1].co
    size = v1.x - v0.x
    i = (x - v0.x) / size * n
    j = (y - v0.y) / size * n
    i = max(0, min(n, int(round(i))))
    j = max(0, min(n, int(round(j))))
    return me.vertices[j * (n + 1) + i].co.z


def fondo(rng, z=-7.0, size=220.0, seed=2, algas=40, rocas=14, centro=(0, 0)):
    """Fondo del lago: lecho ondulado, rocas, algas y piedras."""
    grid('Lecho', size, 90, lambda x, y: z + 0.8 * fbm(x * 1.5, y * 1.5, seed), 'rgb:0.55,0.5,0.38', ENTORNO, centro)
    for i in range(rocas):
        modelo('Roca_A', (centro[0] + rng.uniform(-40, 40), centro[1] + rng.uniform(-10, 60), z - 0.2), rng.uniform(0, 360), rng.uniform(0.6, 1.8))
    tipos = ['Alga_A', 'Alga_B', 'Pasto_Acuatico', 'Piedras_Fondo']
    for i in range(algas):
        modelo(tipos[rng.randrange(4)], (centro[0] + rng.uniform(-30, 30), centro[1] + rng.uniform(-6, 50), z),
               rng.uniform(0, 360), rng.uniform(0.8, 1.8))


def esfera(pos, r, src, cls, seg=10):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=seg, ring_count=max(4, seg // 2), radius=r, location=Vector(pos))
    o = bpy.context.object
    SCENE_OBJS.append(o)
    set_src(o.data, src)
    o.pass_index = cls
    return o


def burbujas(rng, centro, n, radio, alto, size=(0.03, 0.12)):
    for i in range(n):
        t = rng.random()
        p = Vector(centro) + Vector((rng.uniform(-radio, radio) * (0.4 + t), rng.uniform(-radio, radio) * (0.4 + t), t * alto))
        esfera(p, rng.uniform(*size) * (0.6 + 0.6 * t), 'glass', AGUA, 16)


def chorro(rng, desde, hasta, n=40, grosor=0.06):
    """Chorro de la pistola de agua: gotas alargadas en línea, que se abren al final."""
    a = Vector(desde)
    b = Vector(hasta)
    d = (b - a)
    for i in range(n):
        t = i / max(1, n - 1)
        p = a + d * t + Vector((rng.uniform(-1, 1), rng.uniform(-1, 1), rng.uniform(-1, 1))) * grosor * (0.3 + 2.5 * t * t)
        o = esfera(p, 1.0, 'agua_gota', AGUA, 8)
        s = grosor * rng.uniform(0.6, 1.1)
        o.scale = (s, s * 2.6, s)
        o.rotation_euler = d.to_track_quat('Y', 'Z').to_euler()


def plumas(rng, centro, n, radio, largo=(0.07, 0.16)):
    """Plumas sueltas (óvalos planos) flotando."""
    for i in range(n):
        p = Vector(centro) + Vector((rng.uniform(-radio, radio), rng.uniform(-radio, radio), rng.uniform(-radio, radio) * 0.6))
        o = esfera(p, 1.0, 'rgb:0.75,0.75,0.72', AVE, 8)
        l = rng.uniform(*largo)
        o.scale = (l * 0.28, l, l * 0.05)
        o.rotation_euler = (rng.uniform(0, 6.3), rng.uniform(0, 6.3), rng.uniform(0, 6.3))


def vitrina(pos, rot_z=0.0, con_traje=True):
    modelo('Vitrina_Traje', pos, rot_z)
    if con_traje:
        p = Vector(pos)
        modelo('Traje_Defensor', (p.x, p.y, p.z + 1.25), rot_z, 1.0, BRILLO if False else HEROE)


def salto(rng, base, n=10):
    """Anillo y gotitas donde un pez salta o cae al agua."""
    gotas(rng, base, n, 0.5, 0.35, (0.015, 0.04))


def gotas(rng, base, n, radio, alto, size=(0.02, 0.07)):
    """Salpicadura: gotas que suben en arco alrededor de 'base'."""
    for i in range(n):
        a = rng.uniform(0, math.tau)
        t = rng.uniform(0.05, 1.0)
        r = 0.2 + t * radio * rng.uniform(0.5, 1.0)
        z = 0.05 + alto * 4 * t * (1 - t) * rng.uniform(0.6, 1.2)
        s = rng.uniform(*size) * (1.2 - t)
        o = esfera(Vector(base) + Vector((math.cos(a) * r, math.sin(a) * r, z)), 1.0, 'agua_gota', AGUA, 6)
        o.scale = (s, s, s * rng.uniform(1.2, 2.2))


def corona(rng, base, r0=0.5, r1=1.2, alto=0.7, n=60):
    """Corona de salpicadura: gotas en anillo que suben abriéndose (sin malla sólida)."""
    for i in range(n):
        a = i / n * math.tau + rng.uniform(-0.05, 0.05)
        t = rng.uniform(0.0, 1.0)
        r = r0 + (r1 - r0) * t
        z = alto * t * rng.uniform(0.7, 1.15)
        s = rng.uniform(0.03, 0.07) * (1.3 - t * 0.6)
        o = esfera(Vector(base) + Vector((math.cos(a) * r, math.sin(a) * r, z)), 1.0, 'agua_gota', AGUA, 6)
        o.scale = (s, s, s * 1.8)


def cueva(rng, radio=9.0, alto=5.0):
    """Cueva del Taller: casco de roca irregular, piso, estalactitas y cristales."""
    seg, ring = 64, 32
    verts = []
    faces = []
    for j in range(ring + 1):
        v = j / ring * math.pi
        for i in range(seg):
            u = i / seg * math.tau
            x, y, z = math.cos(u) * math.sin(v), math.sin(u) * math.sin(v), math.cos(v)
            k = 1.0 + 0.12 * fbm(x * 30, y * 30 + z * 20, 5)
            verts.append((x * radio * k, y * radio * k, max(-0.3, z * alto * k)))
    for j in range(ring):
        for i in range(seg):
            a = j * seg + i
            b = j * seg + (i + 1) % seg
            faces.append((a, a + seg, b + seg, b))
    mesh_obj('Cueva', verts, faces, 'rgb:0.36,0.38,0.42', ENTORNO)
    grid('Piso', radio * 2.4, 40, lambda x, y: 0.05 * fbm(x * 8, y * 8, 4), 'rgb:0.45,0.43,0.38', ENTORNO)
    for i in range(10):
        a = rng.uniform(0, math.tau)
        r = rng.uniform(1.5, radio * 0.75)
        modelo('Estalactitas', (math.cos(a) * r, math.sin(a) * r, alto * 0.92 - (r / radio) ** 2 * alto * 0.35), rng.uniform(0, 360), rng.uniform(0.7, 1.4))
    for i in range(8):
        a = rng.uniform(0, math.tau)
        r = rng.uniform(radio * 0.6, radio * 0.85)
        modelo('Cristales_Cueva', (math.cos(a) * r, math.sin(a) * r, 0), rng.uniform(0, 360), rng.uniform(0.8, 1.6), BRILLO)


def chatarra(rng, centro, n, radio):
    tipos = ['Lata', 'Lata', 'Rueda_Bici', 'Bateria', 'Lata']
    for i in range(n):
        a = rng.uniform(0, math.tau)
        r = radio * math.sqrt(rng.random())
        t = tipos[rng.randrange(len(tipos))]
        tilt = (rng.uniform(-80, 80), rng.uniform(-80, 80)) if t != 'Rueda_Bici' else (rng.uniform(-30, 30), rng.uniform(-20, 20))
        modelo(t, (centro[0] + math.cos(a) * r, centro[1] + math.sin(a) * r, centro[2]), rng.uniform(0, 360),
               rng.uniform(0.9, 1.4), ENTORNO, tilt)


def frasco(pos, alto=0.55, radio=0.22):
    m = cone_mesh(radio, radio * 0.85, alto, 24)
    set_src(m, 'glass')
    o = link(bpy.data.objects.new('Frasco', m))
    o.location = Vector(pos)
    o.pass_index = ENTORNO
    return o


# ======================================================================= luz y cámara
def luces(amb):
    sc = bpy.context.scene
    w = bpy.data.worlds.get('Cielo') or bpy.data.worlds.new('Cielo')
    sc.world = w
    w.use_nodes = True
    bg = w.node_tree.nodes.get('Background')
    cfg = {
        'dia': ((-0.5, 0.6, 0.62), 3.2, 0.8),
        'amanecer': ((-0.7, 0.55, 0.25), 2.6, 0.7),
        'atardecer': ((0.6, 0.6, 0.22), 2.6, 0.7),
        'cielo_rojo': ((0.4, 0.7, 0.25), 2.4, 0.6),
        'niebla': ((-0.3, 0.4, 0.8), 1.0, 1.4),
        'bajo_agua': ((-0.2, 0.25, 1.0), 2.6, 0.6),
        'cueva': ((0.0, 0.0, 1.0), 0.0, 0.25),
    }[amb]
    d, sun_e, sky_e = cfg
    bg.inputs[0].default_value = (0.8, 0.82, 0.85, 1)
    bg.inputs[1].default_value = sky_e
    if sun_e > 0:
        sun = link(bpy.data.objects.new('Sol', bpy.data.lights.new('Sol', 'SUN')))
        sun.data.energy = sun_e
        sun.data.angle = math.radians(3)
        sun.rotation_euler = (-Vector(d).normalized()).to_track_quat('-Z', 'Y').to_euler()


def lampara(pos, energia=600, radio=0.3, color=(1.0, 0.85, 0.65)):
    l = link(bpy.data.objects.new('Lampara', bpy.data.lights.new('Lampara', 'POINT')))
    l.data.energy = energia
    l.data.shadow_soft_size = radio
    l.data.color = color
    l.location = Vector(pos)


def camara(pos, mira, lente=28, roll=0.0):
    sc = bpy.context.scene
    cam = link(bpy.data.objects.new('Camara', bpy.data.cameras.new('Camara')))
    cam.location = Vector(pos)
    q = (Vector(mira) - Vector(pos)).to_track_quat('-Z', 'Y')
    q = q @ Euler((0, 0, math.radians(roll))).to_quaternion()
    cam.rotation_euler = q.to_euler()
    cam.data.lens = lente
    cam.data.clip_start = 0.05
    cam.data.clip_end = 3000
    sc.camera = cam


# ======================================================================= materiales por pasada
MATS = {}


def _img(nt, path):
    n = nt.nodes.new('ShaderNodeTexImage')
    n.image = bpy.data.images.load(path, check_existing=True)
    return n


def _rgb(s):
    return tuple(float(x) for x in s.split(',')) + (1.0,)


def _mix_transparent(nt, shader_out, opacity):
    tr = nt.nodes.new('ShaderNodeBsdfTransparent')
    mix = nt.nodes.new('ShaderNodeMixShader')
    mix.inputs[0].default_value = opacity
    nt.links.new(tr.outputs[0], mix.inputs[1])
    nt.links.new(shader_out, mix.inputs[2])
    return mix.outputs[0]


def material(src, pas):
    key = (src, pas)
    if key in MATS:
        return MATS[key]
    m = bpy.data.materials.new('%s|%s' % (pas, os.path.basename(src)[-24:]))
    m.use_nodes = True
    nt = m.node_tree
    nt.nodes.clear()
    out = nt.nodes.new('ShaderNodeOutputMaterial')
    kind, _, rest = src.partition(':')
    # Transparencia por pasada: el vidrio del casco y la superficie vista desde abajo no tapan líneas (se ve la cara
    # y lo que vuela arriba); las gotas son sólidas en las pasadas de líneas (así se dibuja su contorno)
    opacity = {
        'luz': {'glass': 0.12, 'agua_gota': 0.5, 'agua_abajo': 0.0},
        'color': {'glass': 0.18, 'agua_gota': 0.55, 'agua_abajo': 0.45},
        'normal': {'glass': 0.0, 'agua_gota': 1.0, 'agua_abajo': 0.0},
        'clase': {},
    }[pas].get(kind, 1.0)
    if opacity <= 0.0:
        tr = nt.nodes.new('ShaderNodeBsdfTransparent')
        nt.links.new(tr.outputs[0], out.inputs['Surface'])
        MATS[key] = m
        return m

    if pas == 'luz':
        if kind == 'emit':
            em = nt.nodes.new('ShaderNodeEmission')
            em.inputs['Strength'].default_value = 2.5
            sh = em.outputs[0]
        else:
            b = nt.nodes.new('ShaderNodeBsdfPrincipled')
            b.inputs['Base Color'].default_value = (0.72, 0.72, 0.72, 1)
            b.inputs['Roughness'].default_value = 0.35 if kind.startswith('agua') else 0.85
            sh = b.outputs[0]
    elif pas == 'color':
        em = nt.nodes.new('ShaderNodeEmission')
        if kind in ('tex', 'emit') and not rest.startswith('rgb:'):
            nt.links.new(_img(nt, rest).outputs['Color'], em.inputs['Color'])
        elif kind == 'emit':
            em.inputs['Color'].default_value = _rgb(rest[4:])
        elif kind == 'rgb':
            em.inputs['Color'].default_value = _rgb(rest)
        elif kind == 'glass':
            em.inputs['Color'].default_value = (0.75, 0.88, 0.95, 1)
        else:   # agua
            em.inputs['Color'].default_value = (0.32, 0.5, 0.58, 1)
        sh = em.outputs[0]
    elif pas == 'normal':
        em = nt.nodes.new('ShaderNodeEmission')
        geo = nt.nodes.new('ShaderNodeNewGeometry')
        vm = nt.nodes.new('ShaderNodeVectorMath')
        vm.operation = 'MULTIPLY_ADD'
        vm.inputs[1].default_value = (0.5, 0.5, 0.5)
        vm.inputs[2].default_value = (0.5, 0.5, 0.5)
        nt.links.new(geo.outputs['Normal'], vm.inputs[0])
        nt.links.new(vm.outputs[0], em.inputs['Color'])
        sh = em.outputs[0]
    else:   # clase
        em = nt.nodes.new('ShaderNodeEmission')
        info = nt.nodes.new('ShaderNodeObjectInfo')
        camd = nt.nodes.new('ShaderNodeCameraData')
        comb = nt.nodes.new('ShaderNodeCombineXYZ')
        div = nt.nodes.new('ShaderNodeMath')
        div.operation = 'DIVIDE'
        div.inputs[1].default_value = 100.0
        nt.links.new(info.outputs['Object Index'], div.inputs[0])
        nt.links.new(div.outputs[0], comb.inputs['X'])
        nt.links.new(info.outputs['Random'], comb.inputs['Y'])
        # cercanía: 1 / (1 + distancia / 12)
        d1 = nt.nodes.new('ShaderNodeMath')
        d1.operation = 'MULTIPLY_ADD'
        d1.inputs[1].default_value = 1.0 / 12.0
        d1.inputs[2].default_value = 1.0
        nt.links.new(camd.outputs['View Distance'], d1.inputs[0])
        d2 = nt.nodes.new('ShaderNodeMath')
        d2.operation = 'DIVIDE'
        d2.inputs[0].default_value = 1.0
        nt.links.new(d1.outputs[0], d2.inputs[1])
        nt.links.new(d2.outputs[0], comb.inputs['Z'])
        nt.links.new(comb.outputs[0], em.inputs['Color'])
        sh = em.outputs[0]
    if opacity < 1.0:
        sh = _mix_transparent(nt, sh, opacity)
    nt.links.new(sh, out.inputs['Surface'])
    MATS[key] = m
    return m


def aplicar(pas):
    for o in SCENE_OBJS:
        if o.type != 'MESH':
            continue
        src = o.data.get('fow_src', 'rgb:0.6,0.6,0.6')
        o.data.materials.clear()
        o.data.materials.append(material(src, pas))


# ======================================================================= render
def render(vid, out_dir, calidad):
    sc = bpy.context.scene
    f = 1.0 if calidad == 'final' else 0.5
    sc.render.resolution_x = int(W * f) // 4 * 4
    sc.render.resolution_y = int(H * f) // 4 * 4
    sc.render.film_transparent = True
    sc.render.engine = 'CYCLES'
    sc.cycles.device = 'CPU'
    sc.render.image_settings.file_format = 'PNG'
    sc.render.image_settings.color_mode = 'RGBA'
    for pas in ('luz', 'color', 'normal', 'clase'):
        aplicar(pas)
        raw = pas in ('normal', 'clase')
        sc.view_settings.view_transform = 'Raw' if raw else 'Standard'
        sc.view_settings.look = 'None'
        sc.render.image_settings.color_depth = '16' if raw else '8'
        if pas == 'luz':
            sc.cycles.samples = 24 if calidad == 'final' else 6
            sc.cycles.use_denoising = True
            sc.cycles.max_bounces = 3
            sc.cycles.diffuse_bounces = 2
            sc.cycles.glossy_bounces = 1
            sc.cycles.transmission_bounces = 2
            sc.cycles.filter_width = 1.5
        else:
            sc.cycles.samples = 1 if pas == 'clase' else (6 if calidad == 'final' else 2)
            sc.cycles.use_denoising = False
            sc.cycles.max_bounces = 0
            sc.cycles.filter_width = 0.01 if pas == 'clase' else 1.5
        sc.cycles.transparent_max_bounces = 16
        sc.render.filepath = os.path.join(out_dir, '%s_%s.png' % (vid, pas))
        bpy.ops.render.render(write_still=True)


def limpiar():
    global SCENE_OBJS
    keep = set()
    for parts in PROTO.values():
        for p in parts:
            keep.add(p[1].name)
    for o in SCENE_OBJS:
        try:
            bpy.data.objects.remove(o, do_unlink=True)
        except ReferenceError:
            pass
    SCENE_OBJS = []
    for me in list(bpy.data.meshes):
        if me.users == 0 and me.name not in keep:
            bpy.data.meshes.remove(me)
    for l in list(bpy.data.lights):
        if l.users == 0:
            bpy.data.lights.remove(l)
    for c in list(bpy.data.cameras):
        if c.users == 0:
            bpy.data.cameras.remove(c)


# ======================================================================= las escenas
ESCENAS = {}


def escena(fn):
    ESCENAS[fn.__name__] = fn
    return fn


def taller_base(rng, radio=9.0):
    """El Taller: cueva con el banco, las pantallas, el brazo robot, chatarra y luces."""
    cueva(rng, radio, 5)
    modelo('Mesa_Chatarra', (0, 1.6, 0), 0)
    modelo('Pantallas_Taller', (2.4, 2.6, 0), -35)
    modelo('BrazoRobot_Base', (-1.9, 1.9, 0), 30)
    modelo('BrazoRobot_Tramo', (-1.9, 1.9, 0.6), 30, 1.0, ENTORNO, (25, 0))
    chatarra(rng, (-3.4, 2.6, 0.0), 12, 1.3)
    chatarra(rng, (3.8, -0.5, 0.0), 8, 1.0)
    lampara((0.4, 0.9, 3.2), 520)
    lampara((-3.5, -2.5, 2.2), 160, 1.0, (0.6, 0.8, 1.0))


def defensores(rng, centro, n, direction, llamas=True, sep=1.4):
    for i in range(n):
        off = Vector(((i % 2) * 2 - 1) * sep * (1 + i // 2), sep * 0.8 * (1 + i // 2), rng.uniform(-0.3, 0.3))
        heroe(Vector(centro) + off, direction, 0.85, True, rng.uniform(-10, 10), llamas, PEZ, 'Defensor', pitch=10)


# ---------------------------------------------------------------- El origen del traje
@escena
def origen_1(rng):
    lago(rng, 26, 150)
    nenufares(rng, (1.5, -1.0), 22, 6)
    juncos(rng, (-5.5, -3.0), 14, 2.2)
    juncos(rng, (6.5, 2.0), 8, 1.6)
    pez((0.2, 0.6, 0.7), (-1, 0.1, 0.0), 0.6, 'PezDorado', pitch=-25)
    salto(rng, (0.9, 0.6, 0.0), 12)
    pez((2.6, 2.4, 0.35), (1, 0.2, 0), 0.5, 'PezTurquesa', pitch=30)
    salto(rng, (2.2, 2.4, 0.0), 10)
    camara((0, -6.0, 1.1), (0.6, 12, 1.9), 30)


@escena
def origen_2(rng):
    lago(rng, 30, 130)
    ave('Ave_Gaviota', (-3.2, 7.0, 6.5), (0.4, -0.3, -1), 2.6, 15, 35)
    ave('Ave_Gaviota', (3.8, 10.0, 8.5), (-0.5, -0.2, -1), 2.6, -20, 40)
    ave('Ave_Gaviota', (-0.5, 14, 11), (0.2, -0.4, -0.9), 2.6, 5, 30)
    ave('Ave_Gaviota', (1.2, 4.0, 1.6), (-0.3, -1, 0.55), 2.6, -10, 45)
    pez((1.2, 3.05, 2.05), (1, 0, 0), 0.4, 'PezRosa', bank=80)
    gotas(rng, (1.2, 4.2, 0.0), 60, 1.0, 1.0)
    cardumen((-2.0, 3.0, 0.4), 3, 1.2, (-1, 0, 0), rng, 0.4, 0.3)
    camara((0.3, -4.5, 0.9), (0.4, 8, 4.5), 26)


@escena
def origen_3(rng):
    agua(0.0, 400, desde_abajo=True)
    fondo(rng, -4.5, 120, 4, 40, 8, (0, 12))
    for x, y, s in ((-1.5, 3.4, 1.5), (-1.1, 3.9, 1.3), (1.5, 3.6, 1.6), (1.9, 4.4, 1.2), (-2.3, 4.8, 1.7), (2.6, 5.4, 1.5)):
        modelo('Alga_A', (x, y, -4.45), rng.uniform(0, 360), s)
    modelo('Alga_B', (0.6, 3.6, -4.45), 30, 1.3)
    heroe((-0.25, 3.0, -3.0), (0.6, -0.5, 0.5), 0.75, traje=False)
    ave('Ave_Gaviota', (2.0, 8, 1.8), (-1, 0.2, -0.2), 2.6, -15, 20)
    burbujas(rng, (0.0, 3.0, -2.6), 8, 0.2, 1.4)
    camara((0.5, -0.4, -3.2), (0.0, 4.0, -2.3), 34)


@escena
def origen_4(rng):
    agua(0.0, 400, desde_abajo=True)
    fondo(rng, -4.0, 120, 6, 40, 6, (0, 12))
    chatarra(rng, (0.4, 3.0, -4.0), 14, 1.5)
    modelo('Rueda_Bici', (1.8, 3.6, -3.6), 20, 1.3, ENTORNO, (70, 0))
    modelo('BrazoRobot_Base', (-1.5, 3.9, -4.15), 0, 1.1, ENTORNO, (12, -20))
    frasco((0.5, 2.2, -4.0), 0.6, 0.26)
    modelo('Bateria', (-0.4, 2.3, -4.0), 30, 1.4)
    heroe((-1.0, 1.5, -3.55), (0.75, -0.45, 0.05), 0.72, traje=False)
    burbujas(rng, (-0.9, 1.5, -3.1), 5, 0.15, 0.9)
    camara((-0.2, -1.8, -3.3), (0.0, 2.8, -3.8), 30)


@escena
def origen_5(rng):
    agua(0.0, 400, desde_abajo=True)
    fondo(rng, -4.0, 120, 6, 50, 6, (0, 12))
    heroe((0.0, 2.6, -3.0), (-0.45, -1, 0.1), 1.1, traje=True)
    chatarra(rng, (0.3, 3.0, -4.0), 10, 1.8)
    modelo('Rueda_Bici', (1.8, 4.0, -3.7), 40, 1.2, ENTORNO, (75, 0))
    burbujas(rng, (0.0, 2.6, -2.4), 12, 0.25, 2.0)
    camara((0.9, -0.9, -3.1), (0, 2.6, -2.9), 34)


@escena
def origen_6(rng):
    lago(rng, 32, 130)
    heroe((0.0, 2.6, 1.25), (0.35, -0.8, 0.9), 1.1, traje=True, llamas=True)
    gotas(rng, (0.0, 2.7, 0.0), 80, 1.5, 1.6)
    corona(rng, (0.0, 2.7, 0.0), 0.45, 1.2, 0.8, 44)
    ave('Ave_Gaviota', (2.8, 5.0, 3.6), (1, 0.4, 0.4), 2.6, 30, 50)
    ave('Ave_Gaviota', (-3.0, 5.5, 4.2), (-1, 0.2, 0.3), 2.6, -30, 45)
    ave('Ave_Gaviota', (0.8, 9, 6.0), (0.3, 1, 0.5), 2.6, 10, 30)
    camara((0.8, -1.8, 0.45), (0, 2.6, 1.9), 26)


# ---------------------------------------------------------------- El Taller (primera visita)
@escena
def taller_1(rng):
    taller_base(rng, 9)
    vitrina((-3.2, 2.4, 0), 40)
    modelo('Mesa_Holo', (2.0, 0.2, 0), 0, 1.0, BRILLO)
    mecanico((0.1, 0.8, 0.0), (0.4, -1, 0), 1.0)
    for x in (-5.0, 4.6, 5.6):
        modelo('Cristales_Cueva', (x, 3.4, 0), rng.uniform(0, 360), 1.4, BRILLO)
    camara((0.6, -4.6, 1.7), (0, 2.0, 1.6), 22)


@escena
def taller_2(rng):
    taller_base(rng)
    mecanico((0.25, 0.85, 0.0), (0.75, -0.65, 0), 1.0)
    modelo('Bateria', (-0.2, 1.35, 0.95), 20)
    modelo('Lata', (-0.7, 1.4, 0.95), 0)
    camara((2.0, -1.9, 1.35), (-0.1, 1.2, 0.9), 32)


@escena
def taller_3(rng):
    taller_base(rng)
    mecanico((0.5, 1.0, 0.0), (-0.45, -1, 0), 1.0)
    heroe((-1.25, -0.05, 0.6), (0.7, 0.75, 0.05), 0.8, traje=True)
    camara((-1.1, -2.9, 1.05), (0.15, 0.9, 0.9), 34)


# ---------------------------------------------------------------- La Garza Gris Gigante
@escena
def garza_antes_1(rng):
    lago(rng, 24, 90)
    juncos(rng, (-3.5, -2.0), 30, 2.6)
    juncos(rng, (4.5, 0.5), 14, 2.0)
    ave('Jefe_GarzaGris', (1.0, 9.0, 3.6), (-1, -0.3, -0.05), 8.0, -10, 5)
    camara((0, -6.5, 0.9), (0.6, 9, 3.4), 26)


@escena
def garza_antes_2(rng):
    lago(rng, 28, 90)
    ave('Jefe_GarzaGris', (-0.4, 8.5, 2.6), (0.6, -0.35, -0.6), 8.0, 15, 8)
    gotas(rng, (1.4, 7.4, 0.0), 90, 2.2, 2.2, (0.03, 0.08))
    corona(rng, (1.4, 7.4, 0.0), 0.8, 2.2, 1.2, 50)
    for p, d in (((3.2, 5.4, 1.1), (1, -0.3, 0)), ((-1.6, 5.8, 0.8), (-1, -0.2, 0)), ((2.0, 4.2, 0.5), (0.6, -1, 0))):
        pez(p, d, 0.5, VARIANTES[rng.randrange(6)], pitch=-20)
    camara((3.6, -2.4, 1.0), (0.6, 7.5, 2.3), 26)


@escena
def garza_antes_3(rng):
    lago(rng, 30, 110)
    ave('Jefe_GarzaGris', (3.4, 10, 4.2), (-1, -0.45, -0.1), 8.0, 10, -15)
    heroe((-1.7, 1.8, 2.3), (1, 0.35, 0.12), 1.0, traje=True, llamas=True, pitch=10)
    chorro(rng, (-1.25, 1.95, 2.45), (2.6, 9.4, 4.2), 60, 0.07)
    camara((-0.6, -2.6, 2.0), (1.2, 7, 3.4), 30)


@escena
def garza_despues_1(rng):
    lago(rng, 30, 120)
    ave('Jefe_GarzaGris', (-6, 28, 9), (-0.6, 1, 0.35), 8.0, -20, 30)
    plumas(rng, (0.4, 1.4, 1.8), 9, 1.4)
    heroe((0.6, 5.0, 1.4), (-0.2, -1, 0.15), 1.0, traje=True, llamas=True)
    for p, d in (((-1.4, 4.2, 0.9), (1, 0, 0)), ((2.4, 4.6, 0.7), (-1, 0, 0)), ((-0.4, 3.6, 0.4), (0.3, -1, 0))):
        pez(p, d, 0.48, VARIANTES[rng.randrange(6)], pitch=-30)
        salto(rng, (p[0], p[1], 0.0), 8)
    camara((0.4, -2.0, 1.0), (0.3, 7, 2.6), 28)


@escena
def garza_despues_2(rng):
    taller_base(rng)
    modelo('Mesa_Holo', (1.1, 0.5, 0), 0, 1.0, BRILLO)
    mecanico((0.0, 0.5, 0.0), (-0.45, -1, 0), 1.0)
    modelo('Bateria', (0.5, 0.05, 0.9), 10)
    heroe((-1.5, -0.4, 0.7), (1, 0.15, 0), 0.85, traje=True)
    camara((0.3, -3.4, 1.25), (-0.3, 0.5, 0.9), 30)


# ---------------------------------------------------------------- El Pelícano Saco Sin Fondo
@escena
def pelicano_antes_1(rng):
    lago(rng, 40, 150)
    ave('Jefe_PelicanoSaco', (6.0, 30, 7), (-0.3, -1, -0.12), 9.0, 6, 10)
    heroe((-1.5, 1.6, 0.3), (1, 0.45, 0.2), 0.95, traje=True)
    camara((-0.5, -1.8, 0.6), (1.6, 26, 4.6), 34)


@escena
def pelicano_antes_2(rng):
    lago(rng, 30, 110)
    ave('Jefe_PelicanoSaco', (-0.5, 9.0, 2.4), (-1, -0.45, -0.3), 9.0, 0, 8)
    for i, (p, d) in enumerate((((-3.6, 6.2, 1.4), (-1, -0.2, 0)), ((-4.2, 7.4, 2.0), (-1, 0.1, 0)),
                                ((-2.6, 5.4, 0.8), (-0.8, -0.6, 0)), ((-3.0, 8.0, 0.6), (-1, 0, 0)))):
        pez(p, d, 0.48, VARIANTES[(i + 1) % 6], pitch=-25)
    gotas(rng, (-1.8, 7.0, 0.0), 60, 2.0, 1.4)
    camara((2.6, -2.0, 0.9), (-1.3, 8, 2.0), 26)


@escena
def pelicano_antes_3(rng):
    lago(rng, 30, 110)
    ave('Jefe_PelicanoSaco', (3.6, 11, 5.2), (-1, -0.5, -0.05), 9.0, 15, 12)
    heroe((-1.8, 2.2, 2.8), (1, 0.45, 0.25), 1.0, traje=True, llamas=True, pitch=10)
    chorro(rng, (-1.35, 2.4, 2.95), (2.6, 10.2, 5.0), 60, 0.07)
    gotas(rng, (2.7, 10.4, 4.9), 30, 0.8, 0.8)
    camara((-0.8, -2.6, 2.0), (1.4, 8, 4.0), 28)


@escena
def pelicano_despues_1(rng):
    lago(rng, 30, 120)
    ave('Jefe_PelicanoSaco', (7, 26, 10), (0.6, 1, 0.3), 9.0, 20, 30)
    for i, p in enumerate(((-1.6, 3.6, 2.6), (0.4, 4.2, 3.4), (1.8, 3.4, 1.8), (-0.6, 3.0, 1.2), (2.6, 5.0, 2.9), (-2.4, 5.0, 3.6))):
        pez(p, (rng.uniform(-1, 1), rng.uniform(-1, 0), 0), 0.5, VARIANTES[i % 6], bank=rng.uniform(-40, 40), pitch=rng.uniform(-60, 60))
    for i in range(4):
        salto(rng, (rng.uniform(-2.5, 2.5), rng.uniform(3, 5.5), 0.0), 14)
    camara((0, -2.6, 1.0), (0.4, 7, 3.0), 26)


@escena
def pelicano_despues_2(rng):
    lago(rng, 34, 120)
    heroe((0.0, 5.0, 3.6), (1, -0.35, 0.05), 1.0, traje=True, llamas=True, pitch=20)
    for i, off in enumerate(((-1.6, 1.4, -0.3), (-1.2, -1.5, 0.35), (-3.0, 0.2, 0.1))):
        heroe(Vector((0.0, 5.0, 3.6)) + Vector(off), (1, -0.35, 0.05), 0.85, True, rng.uniform(-8, 8), True, PEZ, 'Defensor', pitch=20)
    camara((4.2, -1.6, 3.4), (-0.6, 5.6, 3.5), 30)


# ---------------------------------------------------------------- El Cormorán Rey
@escena
def cormoran_antes_1(rng):
    agua(0.0, 400, desde_abajo=True)
    fondo(rng, -7.0, 140, 8, 50, 8, (0, 14))
    ave('Jefe_CormoranRey', (0.4, 8.0, -1.8), (0.1, -0.45, -1), 6.5, 0, -40)
    burbujas(rng, (0.4, 8.8, -1.0), 30, 0.6, 1.2, (0.04, 0.14))
    cardumen((-0.6, 4.5, -3.6), 9, 1.6, (1, 0, 0), rng, 0.48, 0.4)
    camara((0, -1.5, -4.8), (0.2, 7, -1.8), 26)


@escena
def cormoran_antes_2(rng):
    agua(0.0, 400, desde_abajo=True)
    fondo(rng, -5.5, 140, 9, 60, 8, (0, 12))
    ave('Jefe_CormoranRey', (3.0, 5.6, -3.0), (-1, 0, -0.08), 6.5, 0, -55)
    burbujas(rng, (5.0, 5.6, -3.0), 20, 0.8, 0.8)
    cardumen((-1.8, 4.6, -3.0), 12, 1.2, (-1, 0, 0), rng, 0.55, 0.5, 15)
    camara((0, -2.6, -3.0), (0, 6, -3.0), 26)


@escena
def cormoran_antes_3(rng):
    agua(0.0, 400, desde_abajo=True)
    fondo(rng, -5.0, 140, 10, 50, 8, (0, 12))
    heroe((-1.1, 1.6, -2.6), (1, 0.3, 0), 1.0, traje=True)
    ave('Jefe_CormoranRey', (3.4, 4.6, -2.4), (-1, -0.3, 0), 6.5, 0, -30)
    burbujas(rng, (-1.0, 1.6, -2.0), 8, 0.2, 1.2)
    camara((0.3, -2.4, -2.5), (0.9, 3.4, -2.5), 28)


@escena
def cormoran_despues_1(rng):
    lago(rng, 30, 120)
    ave('Jefe_CormoranRey', (0.4, 8.0, 3.2), (0.35, 1, 0.55), 6.5, 15, 45)
    gotas(rng, (0.2, 7.4, 0.0), 100, 2.0, 2.8, (0.03, 0.08))
    corona(rng, (0.2, 7.4, 0.0), 0.8, 2.2, 1.1, 50)
    camara((-2.4, -2.0, 0.8), (0.3, 7, 2.6), 26)


@escena
def cormoran_despues_2(rng):
    taller_base(rng)
    modelo('Mesa_Holo', (0.9, 0.4, 0), 0, 1.0, BRILLO)
    mecanico((0.6, 0.9, 0.0), (-0.85, -0.6, 0), 1.0)
    heroe((-0.7, 0.1, 0.6), (1, 0.4, 0), 0.85, traje=True)
    camara((0.3, -3.0, 1.2), (0.0, 0.6, 0.9), 32)


# ---------------------------------------------------------------- La Reina Águila Marina
@escena
def reina_antes_1(rng):
    lago(rng, 34, 150)
    for i in range(18):
        ave('Ave_Gaviota', (rng.uniform(-30, 30), rng.uniform(40, 80), rng.uniform(10, 24)),
            (rng.uniform(-1, 1), rng.uniform(-1, 1), 0), 2.6, rng.uniform(-30, 30), rng.uniform(-10, 40))
    nenufares(rng, (0.0, 2.0), 10, 3)
    camara((0, -4.5, 1.1), (0, 30, 6.5), 28)


@escena
def reina_antes_2(rng):
    lago(rng, 34, 140)
    ave('Jefe_ReinaAguila', (0.0, 9.5, 11.0), (0.05, -0.55, -1), 9.5, 0, 25)
    for i in range(6):
        ave('Ave_Gaviota', (rng.uniform(-14, 14), rng.uniform(14, 30), rng.uniform(14, 24)),
            (rng.uniform(-1, 1), rng.uniform(-1, 0.2), -0.2), 2.6, rng.uniform(-30, 30), rng.uniform(0, 40))
    camara((0, -2.5, 0.9), (0, 9, 9.5), 22)


@escena
def reina_antes_3(rng):
    lago(rng, 34, 140)
    ave('Jefe_ReinaAguila', (0.0, 16.0, 8.5), (0, -1, 0.05), 9.5, 0, 10)
    for i in range(16):
        a = i / 16 * math.tau
        p = (math.cos(a) * rng.uniform(6, 14), 18 + math.sin(a) * rng.uniform(3, 8), 8.5 + rng.uniform(-4, 6))
        d = (-math.sin(a), math.cos(a), rng.uniform(-0.2, 0.3))
        ave('Ave_Gaviota', p, d, 2.6, rng.uniform(-35, 35), rng.uniform(-10, 45))
    camara((0, -4.0, 2.0), (0, 16, 8.0), 24)


@escena
def reina_antes_4(rng):
    lago(rng, 34, 140)
    base = Vector((0.0, 5.6, 4.2))
    heroe(base, (0.0, 1, 0.45), 1.0, traje=True, llamas=True)
    for off in ((-0.8, -1.3, -0.5), (0.6, -1.0, -1.1), (-0.3, -2.1, -1.3)):
        heroe(base + Vector(off), (0.0, 1, 0.45), 0.85, True, rng.uniform(-8, 8), True, PEZ, 'Defensor')
    ave('Jefe_ReinaAguila', (0.0, 12.5, 8.0), (0.0, -1, -0.35), 9.5, 0, 20)
    camara((4.6, 5.2, 3.3), (0.0, 7.4, 5.5), 22)


# ---------------------------------------------------------------- El lago a salvo
@escena
def final_1(rng):
    lago(rng, 40, 160)
    ave('Jefe_ReinaAguila', (12, 70, 13), (0.3, 1, 0.1), 9.5, -10, 20)
    for i in range(9):
        ave('Ave_Gaviota', (rng.uniform(6, 24), rng.uniform(62, 85), rng.uniform(9, 17)),
            (rng.uniform(-0.2, 0.6), 1, 0.1), 2.6, rng.uniform(-20, 20), rng.uniform(0, 40))
    heroe((-1.6, 2.0, 0.8), (1, 0.5, 0.25), 0.9, traje=True)
    camara((-0.6, -1.8, 0.8), (2.2, 40, 6.0), 32)


@escena
def final_2(rng):
    lago(rng, 26, 150)
    nenufares(rng, (0.5, 0.5), 30, 7)
    juncos(rng, (-6, -1), 14, 2.4)
    juncos(rng, (6.5, 4.0), 10, 2.0)
    pez((1.0, 3.0, 0.6), (-1, 0, 0), 0.5, 'PezDorado', pitch=-35)
    salto(rng, (1.7, 3.0, 0.0), 10)
    camara((0, -6.0, 1.0), (0, 14, 1.8), 28)


@escena
def final_3(rng):
    agua(0.0, 400, desde_abajo=True)
    fondo(rng, -5.5, 140, 11, 50, 8, (0, 12))
    heroe((0.0, 3.0, -2.6), (-0.3, -1, 0.1), 1.0, traje=True)
    for i in range(18):
        a = i / 18 * math.tau
        r = 1.6 + 0.3 * math.sin(i * 1.7)
        p = (math.cos(a) * r, 3.0 + math.sin(a) * r, -2.6 + 0.6 * math.sin(a * 2))
        d = (-math.sin(a), math.cos(a), 0)
        pez(p, d, 0.42, VARIANTES[i % 6])
    burbujas(rng, (0.0, 3.0, -2.0), 10, 0.3, 1.5)
    camara((0.0, -1.4, -2.2), (0.0, 3.0, -2.6), 30)


@escena
def final_4(rng):
    taller_base(rng)
    vitrina((-1.4, 2.2, 0), 20)
    mecanico((0.2, 1.4, 0.0), (0.55, -0.85, 0), 1.0)
    camara((1.4, -2.2, 1.4), (-0.9, 2.0, 1.2), 30)


@escena
def final_5(rng):
    lago(rng, 24, 120)
    nenufares(rng, (0.0, 2.6), 9, 1.8)
    heroe((-0.2, 1.5, -0.08), (0.3, -1, 0.15), 0.62, traje=False)
    juncos(rng, (2.4, 3.5), 8, 1.0)
    camara((0.3, -0.7, 0.42), (-0.1, 2.4, 0.1), 38)


@escena
def final_6(rng):
    cueva(rng, 8, 4.5)
    vitrina((0, 1.6, 0), 0)
    lampara((0, 1.0, 3.6), 380, 0.2, (1.0, 0.9, 0.75))
    for x in (-2.2, 2.4):
        modelo('Cristales_Cueva', (x, 2.0, 0), rng.uniform(0, 360), 1.3, BRILLO)
    camara((0.6, -2.4, 0.6), (0, 1.6, 1.4), 30)


# ======================================================================= main
def main():
    args = [a for a in sys.argv[sys.argv.index('--') + 1:]] if '--' in sys.argv else sys.argv[1:]
    args = [a for a in args if not a.endswith('.py')]
    out_dir = args[0] if args else os.path.join(ROOT, '..', 'vinetas_render')
    calidad = args[1] if len(args) > 1 else 'borrador'
    solo = set(args[2:])
    os.makedirs(out_dir, exist_ok=True)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    for vid, comic, amb, texto in VINETAS:
        if solo and vid not in solo:
            continue
        fn = ESCENAS.get(vid)
        if fn is None:
            continue
        t0 = time.time()
        rng = random.Random(sum(map(ord, vid)) * 7919)
        luces(amb)
        fn(rng)
        render(vid, out_dir, calidad)
        limpiar()
        print('VINETA', vid, '%.1f s' % (time.time() - t0), flush=True)


if __name__ == '__main__':
    main()
