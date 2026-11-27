# -*- coding: utf-8 -*-
"""Renders 3D del protagonista para el logo y el ícono (Blender como módulo: pip install bpy; Cycles en CPU).

Usa el modelo del juego (Assets/Models/Animations/PESCAO.fbx) con sus texturas (Assets/Models/Texturas: traje y pez,
la luz del pecho con su emisión) y la luz del menú: azul marino de fondo, luz cálida de frente y bordes azul
eléctrico. Fondo transparente; logo.py les pone el brillo y el título.

    python Herramientas/Logo/render_pez_3d.py [logo] [icono] [borrador]      (desde la raíz del proyecto)

Escribe en Herramientas/Logo/renders/:
  - pez_logo.png    el pez sale volando de una salpicadura de agua, con el fuego del propulsor
  - pez_icono.png   el pez de frente (cabeza y pecho), para el ícono de la aplicación
  - *_brillo.png    lo mismo, pero solo lo que brilla (la luz del pecho y el fuego): logo.py lo usa para el resplandor
Con "solo_brillo" hace solo las imágenes de brillo (rápidas).
Con "estilos" hace las vistas previas de los estilos (skins) del menú: Assets/Resources/Skins/<Estilo>/Vista.png, con las
texturas de Herramientas/Estilos/estilos.py (y la original en Skins/Original).
"""
import math
import os
import random
import sys

import bpy
from mathutils import Euler, Vector

ROOT = os.getcwd()
TEX = os.path.join(ROOT, 'Assets', 'Models', 'Texturas')
OUT = os.path.join(ROOT, 'Herramientas', 'Logo', 'renders')
BORRADOR = 'borrador' in sys.argv
SOLO_BRILLO = 'solo_brillo' in sys.argv
ESTILO_DIR = None     # carpeta con Cuerpo.png y Armadura.png (vistas previas de los estilos)


# ---------------------------------------------------------------- materiales
def _img(nt, path, noncolor):
    n = nt.nodes.new('ShaderNodeTexImage')
    n.image = bpy.data.images.load(path, check_existing=True)
    if noncolor:
        n.image.colorspace_settings.name = 'Non-Color'
    return n


def pbr(mat, prefix, emissive=None, strength=0.0):
    mat.use_nodes = True
    nt = mat.node_tree
    nt.nodes.clear()
    out = nt.nodes.new('ShaderNodeOutputMaterial')
    b = nt.nodes.new('ShaderNodeBsdfPrincipled')
    nt.links.new(b.outputs['BSDF'], out.inputs['Surface'])
    base = prefix + '_BaseColor.png'
    if ESTILO_DIR:
        propio = os.path.join(ESTILO_DIR, 'Cuerpo.png' if 'Body' in prefix else 'Armadura.png')
        if os.path.exists(propio):
            base = propio
    nt.links.new(_img(nt, base, False).outputs['Color'], b.inputs['Base Color'])
    nt.links.new(_img(nt, prefix + '_Metallic.png', True).outputs['Color'], b.inputs['Metallic'])
    nt.links.new(_img(nt, prefix + '_Roughness.png', True).outputs['Color'], b.inputs['Roughness'])
    nm = nt.nodes.new('ShaderNodeNormalMap')
    nt.links.new(_img(nt, prefix + '_Normal.png', True).outputs['Color'], nm.inputs['Color'])
    nt.links.new(nm.outputs['Normal'], b.inputs['Normal'])
    if emissive:
        nt.links.new(_img(nt, emissive, False).outputs['Color'], b.inputs['Emission Color'])
        b.inputs['Emission Strength'].default_value = strength


def vidrio(mat):
    """La escafandra: casi invisible, solo sus reflejos (la cara se ve clara detrás)."""
    mat.use_nodes = True
    nt = mat.node_tree
    nt.nodes.clear()
    out = nt.nodes.new('ShaderNodeOutputMaterial')
    b = nt.nodes.new('ShaderNodeBsdfPrincipled')
    b.inputs['Base Color'].default_value = (0.8, 0.92, 1.0, 1.0)
    b.inputs['Roughness'].default_value = 0.04
    b.inputs['Alpha'].default_value = 0.05
    nt.links.new(b.outputs['BSDF'], out.inputs['Surface'])


def emision(nombre, centro, borde, fuerza, blend=0.35):
    """Material que brilla más en el centro y se vuelve transparente en los bordes (el fuego)."""
    m = bpy.data.materials.new(nombre)
    m.use_nodes = True
    nt = m.node_tree
    nt.nodes.clear()
    out = nt.nodes.new('ShaderNodeOutputMaterial')
    em = nt.nodes.new('ShaderNodeEmission')
    lw = nt.nodes.new('ShaderNodeLayerWeight')
    lw.inputs['Blend'].default_value = blend
    ramp = nt.nodes.new('ShaderNodeValToRGB')
    nt.links.new(lw.outputs['Facing'], ramp.inputs['Fac'])
    ramp.color_ramp.elements[0].color = (*centro, 1)
    ramp.color_ramp.elements[1].color = (*borde, 1)
    nt.links.new(ramp.outputs['Color'], em.inputs['Color'])
    em.inputs['Strength'].default_value = fuerza
    tr = nt.nodes.new('ShaderNodeBsdfTransparent')
    mix = nt.nodes.new('ShaderNodeMixShader')
    nt.links.new(lw.outputs['Facing'], mix.inputs['Fac'])
    nt.links.new(em.outputs[0], mix.inputs[1])
    nt.links.new(tr.outputs[0], mix.inputs[2])
    nt.links.new(mix.outputs[0], out.inputs['Surface'])
    return m


def agua():
    """Agua estilizada como la del juego: azul con los bordes claros; el centro deja ver lo de atrás."""
    m = bpy.data.materials.new('Agua')
    m.use_nodes = True
    nt = m.node_tree
    nt.nodes.clear()
    out = nt.nodes.new('ShaderNodeOutputMaterial')
    b = nt.nodes.new('ShaderNodeBsdfPrincipled')
    b.inputs['Base Color'].default_value = (0.10, 0.42, 0.95, 1)
    b.inputs['Roughness'].default_value = 0.08
    b.inputs['Coat Weight'].default_value = 1.0
    b.inputs['Emission Color'].default_value = (0.35, 0.75, 1.0, 1)
    lw = nt.nodes.new('ShaderNodeLayerWeight')
    lw.inputs['Blend'].default_value = 0.45
    em = nt.nodes.new('ShaderNodeMath')
    em.operation = 'MULTIPLY'
    nt.links.new(lw.outputs['Facing'], em.inputs[0])
    em.inputs[1].default_value = 1.0
    nt.links.new(em.outputs[0], b.inputs['Emission Strength'])
    al = nt.nodes.new('ShaderNodeMapRange')
    nt.links.new(lw.outputs['Facing'], al.inputs['Value'])
    al.inputs['To Min'].default_value = 0.30
    al.inputs['To Max'].default_value = 1.0
    nt.links.new(al.outputs['Result'], b.inputs['Alpha'])
    nt.links.new(b.outputs['BSDF'], out.inputs['Surface'])
    return m


# ---------------------------------------------------------------- el pez
def cargar_pez():
    antes = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=os.path.join(ROOT, 'Assets', 'Models', 'Animations', 'PESCAO.fbx'))
    nuevos = [o for o in bpy.data.objects if o not in antes]
    for m in bpy.data.materials:
        if m.name.startswith('Body_Color'):
            pbr(m, os.path.join(TEX, 'Body', 'Pez_F_Model_Body'))
        elif m.name.startswith('aiStandardSurface2'):
            pbr(m, os.path.join(TEX, 'Armor', 'Pez_F_Model_Armor'),
                os.path.join(TEX, 'Armor', 'Pez_F_Model_Armor_Emissive.png'), 9.0)
        elif m.name.startswith('Glass'):
            vidrio(m)
    arm = [o for o in nuevos if o.type == 'ARMATURE'][0]
    bpy.context.view_layer.update()
    deps = bpy.context.evaluated_depsgraph_get()
    lo = Vector((1e9,) * 3)
    hi = Vector((-1e9,) * 3)
    for o in nuevos:
        if o.type != 'MESH':
            continue
        ev = o.evaluated_get(deps)
        for v in ev.to_mesh().vertices:
            w = ev.matrix_world @ v.co
            lo = Vector(map(min, lo, w))
            hi = Vector(map(max, hi, w))
        ev.to_mesh_clear()
    # alto 1.5, centrado, con la base en z = 0, colgado de un vacío que se puede girar
    k = 1.5 / (hi - lo).z
    centro = (hi + lo) / 2
    raiz = bpy.data.objects.new('Heroe', None)
    bpy.context.scene.collection.objects.link(raiz)
    arm.scale = arm.scale * k
    arm.location = arm.location - Vector((centro.x, centro.y, lo.z)) * k
    arm.parent = raiz
    return raiz


def _llama_malla(nombre, radio, largo, n=40, m=24):
    """Una llama en forma de gota: redonda en la tobera y afinándose hasta la punta (a lo largo de -Z)."""
    verts, caras = [], []
    for i in range(n + 1):
        t = i / n
        r = radio * (math.sin(t / 0.18 * math.pi / 2) if t < 0.18 else ((1 - t) / 0.82) ** 0.85)
        for k in range(m):
            a = k / m * math.tau
            verts.append((math.cos(a) * r, math.sin(a) * r, -t * largo))
    for i in range(n):
        for k in range(m):
            a, b = i * m + k, i * m + (k + 1) % m
            caras.append((a, b, b + m, a + m))
    me = bpy.data.meshes.new(nombre)
    me.from_pydata(verts, [], caras)
    me.update()
    for poly in me.polygons:
        poly.use_smooth = True
    o = bpy.data.objects.new(nombre, me)
    bpy.context.scene.collection.objects.link(o)
    return o


def mat_llama(nombre, largo, colores, fuerza, dureza):
    """Fuego: el color va de blanco en la tobera a naranja y rojo en la punta; los bordes y la punta se desvanecen."""
    m = bpy.data.materials.new(nombre)
    m.use_nodes = True
    nt = m.node_tree
    nt.nodes.clear()
    out = nt.nodes.new('ShaderNodeOutputMaterial')
    tc = nt.nodes.new('ShaderNodeTexCoord')
    sep = nt.nodes.new('ShaderNodeSeparateXYZ')
    nt.links.new(tc.outputs['Object'], sep.inputs[0])
    t = nt.nodes.new('ShaderNodeMapRange')            # 0 en la tobera, 1 en la punta
    nt.links.new(sep.outputs['Z'], t.inputs['Value'])
    t.inputs['From Min'].default_value = 0.0
    t.inputs['From Max'].default_value = -largo
    ramp = nt.nodes.new('ShaderNodeValToRGB')
    nt.links.new(t.outputs['Result'], ramp.inputs['Fac'])
    els = ramp.color_ramp.elements
    els[0].position, els[0].color = 0.0, (*colores[0], 1)
    els[1].position, els[1].color = 1.0, (*colores[2], 1)
    e = els.new(0.35)
    e.color = (*colores[1], 1)
    em = nt.nodes.new('ShaderNodeEmission')
    nt.links.new(ramp.outputs['Color'], em.inputs['Color'])
    em.inputs['Strength'].default_value = fuerza
    lw = nt.nodes.new('ShaderNodeLayerWeight')
    lw.inputs['Blend'].default_value = 0.5
    # opacidad = (1 - bordes)^dureza * (1 - t)^0.8
    a1 = nt.nodes.new('ShaderNodeMath')
    a1.operation = 'SUBTRACT'
    a1.inputs[0].default_value = 1.0
    nt.links.new(lw.outputs['Facing'], a1.inputs[1])
    a2 = nt.nodes.new('ShaderNodeMath')
    a2.operation = 'POWER'
    nt.links.new(a1.outputs[0], a2.inputs[0])
    a2.inputs[1].default_value = dureza
    b1 = nt.nodes.new('ShaderNodeMath')
    b1.operation = 'SUBTRACT'
    b1.inputs[0].default_value = 1.0
    nt.links.new(t.outputs['Result'], b1.inputs[1])
    b2 = nt.nodes.new('ShaderNodeMath')
    b2.operation = 'POWER'
    nt.links.new(b1.outputs[0], b2.inputs[0])
    b2.inputs[1].default_value = 0.8
    op = nt.nodes.new('ShaderNodeMath')
    op.operation = 'MULTIPLY'
    nt.links.new(a2.outputs[0], op.inputs[0])
    nt.links.new(b2.outputs[0], op.inputs[1])
    tr = nt.nodes.new('ShaderNodeBsdfTransparent')
    mix = nt.nodes.new('ShaderNodeMixShader')
    nt.links.new(op.outputs[0], mix.inputs['Fac'])
    nt.links.new(tr.outputs[0], mix.inputs[1])
    nt.links.new(em.outputs[0], mix.inputs[2])
    nt.links.new(mix.outputs[0], out.inputs['Surface'])
    return m


def propulsor(raiz, largo=1.0):
    capas = ((0.17, largo, ((1.0, 0.72, 0.30), (1.0, 0.38, 0.06), (0.80, 0.10, 0.02)), 1.5, 1.7),
             (0.07, largo * 0.4, ((1.0, 1.0, 0.92), (1.0, 0.80, 0.40), (1.0, 0.50, 0.10)), 1.5, 1.3))
    for s in (-1, 1):
        for k, (r, l, colores, fuerza, dureza) in enumerate(capas):
            o = _llama_malla('Llama', r, l)
            o.location = (s * 0.13, 0.40, 0.38)
            o.rotation_euler = (math.radians(-10), 0, 0)
            o.data.materials.append(mat_llama('Llama%d' % k, l, colores, fuerza, dureza))
            o.parent = raiz
    luz = bpy.data.objects.new('LuzLlama', bpy.data.lights.new('LuzLlama', 'POINT'))
    luz.data.energy = 160
    luz.data.color = (1.0, 0.55, 0.2)
    luz.location = (0, 0.25, -0.15)
    luz.parent = raiz
    bpy.context.scene.collection.objects.link(luz)


def salpicadura(rng, base, escala=1.0):
    mat = agua()
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=2, radius=1.0, location=(0, 0, -50))
    gota = bpy.context.object
    gota.data.materials.append(mat)
    bpy.ops.object.shade_smooth()
    # gotas en arcos alrededor, subiendo con el pez
    for _ in range(70):
        a = rng.uniform(0, math.tau)
        t = rng.uniform(0.05, 1.0)
        r = (0.45 + t * rng.uniform(0.7, 1.9)) * escala
        z = (0.15 + 1.9 * t * (1.2 - t) * rng.uniform(0.6, 1.3)) * escala
        s = rng.uniform(0.018, 0.05) * (1.25 - t) * escala
        d = gota.copy()
        d.data = gota.data
        bpy.context.scene.collection.objects.link(d)
        d.location = base + Vector((math.cos(a) * r, math.sin(a) * r * 0.7, z))
        d.scale = (s, s, s * rng.uniform(1.3, 2.4))
        d.rotation_euler = (rng.uniform(-0.4, 0.4), rng.uniform(-0.4, 0.4), 0)
    # la columna de agua que lo sigue
    for _ in range(24):
        t = rng.uniform(0, 1)
        a = rng.uniform(0, math.tau)
        r = rng.uniform(0.05, 0.3) * (1 - t * 0.5) * escala
        d = gota.copy()
        d.data = gota.data
        bpy.context.scene.collection.objects.link(d)
        d.location = base + Vector((math.cos(a) * r, math.sin(a) * r + 0.15, (0.1 + t * 0.7) * escala))
        s = rng.uniform(0.035, 0.07) * escala
        d.scale = (s, s, s * 2.4)
    bpy.data.objects.remove(gota, do_unlink=True)
    # la hoja baja de la salpicadura y la corona de puntas que se abren hacia afuera, cada una con su gota
    bpy.ops.mesh.primitive_cone_add(vertices=64, radius1=0.42 * escala, radius2=0.95 * escala, depth=0.42 * escala,
                                    end_fill_type='NOTHING', location=base + Vector((0, 0, 0.2 * escala)))
    hoja = bpy.context.object
    hoja.data.materials.append(mat)
    tex = bpy.data.textures.new('Ondas', 'CLOUDS')
    tex.noise_scale = 0.22
    sub = hoja.modifiers.new('Sub', 'SUBSURF')
    sub.levels = sub.render_levels = 2
    dsp = hoja.modifiers.new('Ondas', 'DISPLACE')
    dsp.texture = tex
    dsp.strength = 0.12 * escala
    hoja.modifiers.new('Grosor', 'SOLIDIFY').thickness = 0.025 * escala
    bpy.ops.object.shade_smooth()
    bpy.ops.mesh.primitive_uv_sphere_add(segments=16, ring_count=12, radius=1.0, location=(0, 0, -50))
    punta = bpy.context.object
    punta.data.materials.append(mat)
    bpy.ops.object.shade_smooth()
    n = 28
    for i in range(n):
        a = (i + rng.uniform(-0.3, 0.3)) / n * math.tau
        largo = rng.uniform(0.28, 0.62) * escala
        abre = math.radians(rng.uniform(22, 48))
        r0 = 0.9 * escala
        d = Vector((math.cos(a) * math.sin(abre), math.sin(a) * math.sin(abre), math.cos(abre)))
        p = base + Vector((math.cos(a) * r0, math.sin(a) * r0, 0.36 * escala)) + d * largo * 0.5
        e = punta.copy()
        e.data = punta.data
        bpy.context.scene.collection.objects.link(e)
        e.location = p
        e.scale = (0.024 * escala, 0.024 * escala, largo * 0.5)
        e.rotation_euler = d.to_track_quat('Z', 'Y').to_euler()
        g = punta.copy()
        g.data = punta.data
        bpy.context.scene.collection.objects.link(g)
        g.location = p + d * (largo * 0.5 + 0.05 * escala)
        rg = rng.uniform(0.026, 0.04) * escala
        g.scale = (rg, rg, rg * 1.2)
    bpy.data.objects.remove(punta, do_unlink=True)


# ---------------------------------------------------------------- luces y cámara
def escena(muestras, ancho, alto):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    sc = bpy.context.scene
    sc.render.engine = 'CYCLES'
    sc.cycles.device = 'CPU'
    sc.cycles.samples = muestras
    sc.cycles.use_denoising = True
    sc.render.film_transparent = True
    sc.render.resolution_x, sc.render.resolution_y = ancho, alto
    sc.render.image_settings.color_mode = 'RGBA'
    sc.view_settings.view_transform = 'Standard'
    sc.view_settings.look = 'Medium High Contrast'
    # el azul marino del menú como luz de ambiente (el traje gris se ve azul marino como en el juego)
    w = bpy.data.worlds.new('Menu')
    sc.world = w
    w.use_nodes = True
    w.node_tree.nodes['Background'].inputs[0].default_value = (0.10, 0.18, 0.42, 1)
    w.node_tree.nodes['Background'].inputs[1].default_value = 0.9
    return sc


def luz_area(sc, nombre, pos, mira, energia, color, tam):
    o = bpy.data.objects.new(nombre, bpy.data.lights.new(nombre, 'AREA'))
    sc.collection.objects.link(o)
    o.data.energy = energia
    o.data.color = color
    o.data.size = tam
    o.location = pos
    o.rotation_euler = (Vector(mira) - Vector(pos)).to_track_quat('-Z', 'Y').to_euler()


def luces(sc, centro):
    luz_area(sc, 'Clave', centro + Vector((3.2, -5.0, 3.2)), centro, 1100, (1.0, 0.93, 0.84), 3.5)
    luz_area(sc, 'Relleno', centro + Vector((-4.5, -3.0, 0.5)), centro, 260, (0.55, 0.72, 1.0), 4.0)
    luz_area(sc, 'BordeIzq', centro + Vector((-3.2, 3.6, 2.4)), centro, 1300, (0.30, 0.70, 1.0), 2.5)
    luz_area(sc, 'BordeDer', centro + Vector((3.4, 3.0, 1.2)), centro, 1000, (0.35, 0.80, 1.0), 2.5)


def camara(sc, pos, mira, lente):
    cam = bpy.data.objects.new('Camara', bpy.data.cameras.new('Camara'))
    sc.collection.objects.link(cam)
    sc.camera = cam
    cam.data.lens = lente
    cam.location = pos
    cam.rotation_euler = (Vector(mira) - Vector(pos)).to_track_quat('-Z', 'Y').to_euler()
    return cam


def render(sc, nombre):
    os.makedirs(OUT, exist_ok=True)
    if not SOLO_BRILLO:
        sc.render.filepath = os.path.join(OUT, nombre)
        bpy.ops.render.render(write_still=True)
        print('RENDER', sc.render.filepath, flush=True)
    pasada_brillo(sc, nombre.replace('.png', '_brillo.png'))


def pasada_brillo(sc, nombre):
    """Otra toma igual donde solo se ve lo que emite luz (luz del pecho y fuego); lo demás es negro y tapa."""
    negro = bpy.data.materials.new('Negro')
    negro.use_nodes = True
    nt = negro.node_tree
    nt.nodes.clear()
    em = nt.nodes.new('ShaderNodeEmission')
    em.inputs['Color'].default_value = (0, 0, 0, 1)
    nt.links.new(em.outputs[0], nt.nodes.new('ShaderNodeOutputMaterial').inputs['Surface'])
    pecho = bpy.data.materials.new('PechoBrillo')
    pecho.use_nodes = True
    nt = pecho.node_tree
    nt.nodes.clear()
    em = nt.nodes.new('ShaderNodeEmission')
    nt.links.new(_img(nt, os.path.join(TEX, 'Armor', 'Pez_F_Model_Armor_Emissive.png'), False).outputs['Color'],
                 em.inputs['Color'])
    nt.links.new(em.outputs[0], nt.nodes.new('ShaderNodeOutputMaterial').inputs['Surface'])
    for o in sc.objects:
        if o.type != 'MESH':
            continue
        for slot in o.material_slots:
            m = slot.material
            if m is None or m.name.startswith('Llama'):
                continue
            slot.material = pecho if m.name.startswith('aiStandardSurface2') else negro
    sc.cycles.samples = 16
    sc.cycles.use_denoising = False
    sc.view_settings.look = 'None'
    sc.render.filepath = os.path.join(OUT, nombre)
    bpy.ops.render.render(write_still=True)
    print('RENDER', sc.render.filepath, flush=True)


# ---------------------------------------------------------------- tomas
def toma_logo():
    f = 0.5 if BORRADOR else 1.0
    sc = escena(24 if BORRADOR else 160, int(1400 * f), int(1500 * f))
    raiz = cargar_pez()
    propulsor(raiz)
    raiz.location = (0.0, 0.0, 0.95)
    raiz.rotation_euler = Euler((math.radians(6), math.radians(13), math.radians(24)))
    salpicadura(random.Random(11), Vector((-0.2, 0.3, -0.3)), 0.95)
    centro = Vector((0.0, 0.0, 1.55))
    luces(sc, centro)
    camara(sc, (0.75, -4.6, 0.7), (0.05, 0.0, 1.12), 50)
    render(sc, 'pez_logo.png')


def toma_icono():
    f = 0.5 if BORRADOR else 1.0
    sc = escena(24 if BORRADOR else 200, int(1024 * f), int(1024 * f))
    raiz = cargar_pez()
    raiz.rotation_euler = Euler((0, 0, math.radians(16)))
    centro = Vector((0.0, 0.0, 0.95))
    luces(sc, centro)
    camara(sc, (0.45, -3.5, 1.2), (0.0, 0.0, 0.92), 85)
    render(sc, 'pez_icono.png')


def toma_estilos():
    """Vista previa de cada estilo para el menú (cuerpo entero de 3/4, fondo transparente)."""
    global ESTILO_DIR, OUT
    skins = os.path.join(ROOT, 'Assets', 'Resources', 'Skins')
    for nombre in ('Original', 'NeonAbisal', 'SolDorado'):
        ESTILO_DIR = None if nombre == 'Original' else os.path.join(skins, nombre)
        OUT = os.path.join(skins, nombre)
        os.makedirs(OUT, exist_ok=True)
        sc = escena(24 if BORRADOR else 96, 640, 640)
        raiz = cargar_pez()
        raiz.rotation_euler = Euler((0, 0, math.radians(24)))
        luces(sc, Vector((0.0, 0.0, 0.85)))
        camara(sc, (0.55, -4.8, 1.05), (0.0, 0.0, 0.74), 58)
        sc.render.filepath = os.path.join(OUT, 'Vista.png')
        bpy.ops.render.render(write_still=True)
        print('RENDER', sc.render.filepath, flush=True)


if __name__ == '__main__':
    if 'estilos' in sys.argv:
        toma_estilos()
        sys.exit(0)
    solo = [a for a in sys.argv[1:] if a in ('logo', 'icono')]
    if not solo or 'logo' in solo:
        toma_logo()
    if not solo or 'icono' in solo:
        toma_icono()
