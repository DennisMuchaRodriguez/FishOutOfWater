# -*- coding: utf-8 -*-
"""
Imágenes de la página de itch.io (paso 1 de 2): el arte, renderizado con Blender y los modelos del juego.

Escena: el pez con su traje (Assets/Models/Animations/PESCAO.fbx, con las texturas de Assets/Models/Texturas: traje
y pez) sale disparado del lago con una corona de agua; la garza, la gaviota y el cormorán (Assets/Models/Aves) se le
tiran encima. Cielo estilizado con los colores del cielo del juego, sol cálido, nubes, y orillas con bosque a lo lejos.
Tres tomas: arte_ancho.png (16:9), arte_portada.png (para la portada 630x500) y arte_banner.png (3,2:1).

    python Herramientas/Itch/render_arte_itch.py <carpeta de salida> final       (o "borrador": rápido y chico)
    (desde la raíz del proyecto; necesita Blender como módulo: pip install bpy, Cycles en CPU)
Después: python3 Herramientas/Itch/componer_imagenes.py <carpeta de salida>  (les pone el título)
"""
import bpy, sys, os, math, random
from mathutils import Vector, Euler, Matrix

ROOT = os.getcwd()

# ---------------------------------------------------------------- materiales y el pez
TEX = os.path.join(ROOT, 'Assets/Models/Texturas')

def img(nt, path, noncolor):
    n = nt.nodes.new('ShaderNodeTexImage')
    n.image = bpy.data.images.load(path, check_existing=True)
    if noncolor: n.image.colorspace_settings.name = 'Non-Color'
    return n

def pbr(mat, prefix, emissive=None, strength=0.0):
    mat.use_nodes = True
    nt = mat.node_tree; nt.nodes.clear()
    out = nt.nodes.new('ShaderNodeOutputMaterial'); b = nt.nodes.new('ShaderNodeBsdfPrincipled')
    nt.links.new(b.outputs['BSDF'], out.inputs['Surface'])
    nt.links.new(img(nt, prefix + '_BaseColor.png', False).outputs['Color'], b.inputs['Base Color'])
    nt.links.new(img(nt, prefix + '_Metallic.png', True).outputs['Color'], b.inputs['Metallic'])
    nt.links.new(img(nt, prefix + '_Roughness.png', True).outputs['Color'], b.inputs['Roughness'])
    nm = nt.nodes.new('ShaderNodeNormalMap')
    nt.links.new(img(nt, prefix + '_Normal.png', True).outputs['Color'], nm.inputs['Color'])
    nt.links.new(nm.outputs['Normal'], b.inputs['Normal'])
    if emissive:
        nt.links.new(img(nt, emissive, False).outputs['Color'], b.inputs['Emission Color'])
        b.inputs['Emission Strength'].default_value = strength

def glass(mat):
    mat.use_nodes = True
    nt = mat.node_tree; nt.nodes.clear()
    out = nt.nodes.new('ShaderNodeOutputMaterial'); b = nt.nodes.new('ShaderNodeBsdfPrincipled')
    nt.links.new(b.outputs['BSDF'], out.inputs['Surface'])
    b.inputs['Base Color'].default_value = (0.85, 0.95, 1.0, 1.0)
    b.inputs['Transmission Weight'].default_value = 1.0
    b.inputs['Roughness'].default_value = 0.03
    b.inputs['IOR'].default_value = 1.3

def load_hero():
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=os.path.join(ROOT, 'Assets/Models/Animations/PESCAO.fbx'))
    new = [o for o in bpy.data.objects if o not in before]
    for m in bpy.data.materials:
        if m.name.startswith('Body_Color'): pbr(m, os.path.join(TEX, 'Body/Pez_F_Model_Body'))
        elif m.name.startswith('aiStandardSurface2'): pbr(m, os.path.join(TEX, 'Armor/Pez_F_Model_Armor'), os.path.join(TEX, 'Armor/Pez_F_Model_Armor_Emissive.png'), 6.0)
        elif m.name.startswith('Glass'): glass(m)
    arm = [o for o in new if o.type == 'ARMATURE'][0]
    bpy.context.view_layer.update()
    deps = bpy.context.evaluated_depsgraph_get()
    lo = Vector((1e9,)*3); hi = Vector((-1e9,)*3)
    for o in new:
        if o.type != 'MESH': continue
        ev = o.evaluated_get(deps)
        for v in ev.to_mesh().vertices:
            w = ev.matrix_world @ v.co
            lo = Vector(map(min, lo, w)); hi = Vector(map(max, hi, w))
        ev.to_mesh_clear()
    return arm, lo, hi

# ---------------------------------------------------------------- aves
def load_bird(name):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=os.path.join(ROOT, 'Assets/Models/Aves', name, name + '.fbx'))
    new = [o for o in bpy.data.objects if o not in before]
    mat = bpy.data.materials.new(name + '_Mat'); mat.use_nodes = True
    nt = mat.node_tree; b = nt.nodes['Principled BSDF']
    t = nt.nodes.new('ShaderNodeTexImage'); t.image = bpy.data.images.load(os.path.join(ROOT, 'Assets/Models/Aves', name, name + '_BaseColor.png'))
    nt.links.new(t.outputs['Color'], b.inputs['Base Color']); b.inputs['Roughness'].default_value = 0.65
    for o in new:
        if o.type == 'MESH':
            o.data.materials.clear(); o.data.materials.append(mat)
    root = bpy.data.objects.new(name + '_Raiz', None); bpy.context.scene.collection.objects.link(root)
    for o in new:
        if o.parent is None: o.parent = root
    bpy.context.view_layer.update()
    lo = Vector((1e9,)*3); hi = Vector((-1e9,)*3)
    deps = bpy.context.evaluated_depsgraph_get()
    for o in new:
        if o.type != 'MESH': continue
        ev = o.evaluated_get(deps); me = ev.to_mesh()
        for v in me.vertices:
            w = ev.matrix_world @ v.co; lo = Vector(map(min, lo, w)); hi = Vector(map(max, hi, w))
        ev.to_mesh_clear()
    return root, lo, hi

# ---------------------------------------------------------------- escena y tomas
HORIZON = (0.78, 0.88, 0.97); MID = (0.40, 0.66, 0.95); TOP = (0.12, 0.33, 0.78)
SUN_DIR = Vector((-0.55, 0.75, 0.36)).normalized()   # detrás y a la izquierda del pez (contraluz cálido)

def node(nt, kind, **inputs):
    n = nt.nodes.new(kind)
    for k, v in inputs.items(): n.inputs[k].default_value = v
    return n

def world():
    w = bpy.data.worlds.new('Cielo'); bpy.context.scene.world = w; w.use_nodes = True
    nt = w.node_tree; nt.nodes.clear()
    out = nt.nodes.new('ShaderNodeOutputWorld'); bg = nt.nodes.new('ShaderNodeBackground')
    tc = nt.nodes.new('ShaderNodeTexCoord')
    sep = nt.nodes.new('ShaderNodeSeparateXYZ'); nt.links.new(tc.outputs['Generated'], sep.inputs[0])
    ramp = nt.nodes.new('ShaderNodeValToRGB')
    nt.links.new(sep.outputs['Z'], ramp.inputs['Fac'])
    cr = ramp.color_ramp
    cr.elements[0].position = 0.0; cr.elements[0].color = (*HORIZON, 1)
    cr.elements[1].position = 0.42; cr.elements[1].color = (*TOP, 1)
    e = cr.elements.new(0.07); e.color = (*MID, 1)
    # Brillo del sol: dot(dir, sol)^n
    dot = nt.nodes.new('ShaderNodeVectorMath'); dot.operation = 'DOT_PRODUCT'
    nt.links.new(tc.outputs['Generated'], dot.inputs[0]); dot.inputs[1].default_value = SUN_DIR
    p1 = nt.nodes.new('ShaderNodeMath'); p1.operation = 'POWER'; nt.links.new(dot.outputs['Value'], p1.inputs[0]); p1.inputs[1].default_value = 14.0
    p2 = nt.nodes.new('ShaderNodeMath'); p2.operation = 'POWER'; nt.links.new(dot.outputs['Value'], p2.inputs[0]); p2.inputs[1].default_value = 3000.0
    glow = nt.nodes.new('ShaderNodeMix'); glow.data_type = 'RGBA'; glow.blend_type = 'ADD'
    nt.links.new(ramp.outputs['Color'], glow.inputs[6]); glow.inputs[7].default_value = (1.0, 0.72, 0.42, 1)
    g1 = nt.nodes.new('ShaderNodeMath'); g1.operation = 'MULTIPLY'; nt.links.new(p1.outputs['Value'], g1.inputs[0]); g1.inputs[1].default_value = 0.45
    nt.links.new(g1.outputs['Value'], glow.inputs['Factor'])
    disk = nt.nodes.new('ShaderNodeMix'); disk.data_type = 'RGBA'; disk.blend_type = 'ADD'
    nt.links.new(glow.outputs[2], disk.inputs[6]); disk.inputs[7].default_value = (6.0, 5.0, 3.6, 1)
    nt.links.new(p2.outputs['Value'], disk.inputs['Factor'])
    # Nubes: ruido sobre la dirección, solo por encima del horizonte
    noise = nt.nodes.new('ShaderNodeTexNoise'); noise.inputs['Scale'].default_value = 2.6; noise.inputs['Detail'].default_value = 6
    nt.links.new(tc.outputs['Generated'], noise.inputs['Vector'])
    nr = nt.nodes.new('ShaderNodeValToRGB'); nt.links.new(noise.outputs['Fac'], nr.inputs['Fac'])
    nr.color_ramp.elements[0].position = 0.52; nr.color_ramp.elements[0].color = (0, 0, 0, 1)
    nr.color_ramp.elements[1].position = 0.66; nr.color_ramp.elements[1].color = (1, 1, 1, 1)
    band = nt.nodes.new('ShaderNodeMapRange'); nt.links.new(sep.outputs['Z'], band.inputs['Value'])
    band.inputs['From Min'].default_value = 0.02; band.inputs['From Max'].default_value = 0.16
    m1 = nt.nodes.new('ShaderNodeMath'); m1.operation = 'MULTIPLY'
    nt.links.new(nr.outputs['Color'], m1.inputs[0]); nt.links.new(band.outputs['Result'], m1.inputs[1])
    m2 = nt.nodes.new('ShaderNodeMath'); m2.operation = 'MULTIPLY'; nt.links.new(m1.outputs['Value'], m2.inputs[0]); m2.inputs[1].default_value = 0.85
    clouds = nt.nodes.new('ShaderNodeMix'); clouds.data_type = 'RGBA'
    nt.links.new(disk.outputs[2], clouds.inputs[6]); clouds.inputs[7].default_value = (1.0, 0.97, 0.93, 1)
    nt.links.new(m2.outputs['Value'], clouds.inputs['Factor'])
    nt.links.new(clouds.outputs[2], bg.inputs['Color']); bg.inputs['Strength'].default_value = 1.0
    nt.links.new(bg.outputs['Background'], out.inputs['Surface'])

def mat_principled(name, color, rough=0.5, metal=0.0, trans=0.0, ior=1.45, emission=None, strength=0.0):
    m = bpy.data.materials.new(name); m.use_nodes = True
    b = m.node_tree.nodes['Principled BSDF']
    b.inputs['Base Color'].default_value = (*color, 1); b.inputs['Roughness'].default_value = rough
    b.inputs['Metallic'].default_value = metal; b.inputs['Transmission Weight'].default_value = trans; b.inputs['IOR'].default_value = ior
    if emission:
        b.inputs['Emission Color'].default_value = (*emission, 1); b.inputs['Emission Strength'].default_value = strength
    return m

def water():
    bpy.ops.mesh.primitive_plane_add(size=1, location=(0, 0, 0)); p = bpy.context.object; p.name = 'Lago'
    p.scale = (900, 900, 1)
    m = bpy.data.materials.new('Agua'); m.use_nodes = True; nt = m.node_tree
    b = nt.nodes['Principled BSDF']
    b.inputs['Base Color'].default_value = (0.02, 0.12, 0.16, 1); b.inputs['Roughness'].default_value = 0.06
    b.inputs['Specular IOR Level'].default_value = 0.7
    tc = nt.nodes.new('ShaderNodeTexCoord'); wv = nt.nodes.new('ShaderNodeTexNoise'); wv.inputs['Scale'].default_value = 0.6; wv.inputs['Detail'].default_value = 8
    mp = nt.nodes.new('ShaderNodeMapping'); mp.inputs['Scale'].default_value = (900, 900, 1)
    nt.links.new(tc.outputs['UV'], mp.inputs['Vector']); nt.links.new(mp.outputs['Vector'], wv.inputs['Vector'])
    bump = nt.nodes.new('ShaderNodeBump'); bump.inputs['Strength'].default_value = 0.25
    nt.links.new(wv.outputs['Fac'], bump.inputs['Height']); nt.links.new(bump.outputs['Normal'], b.inputs['Normal'])
    p.data.materials.append(m)

def shore(rng):
    # Orillas con lomas y bosque a lo lejos (color mezclado con el horizonte: parecen lejanas)
    hill = mat_principled('Lomas', (0.20, 0.33, 0.30), 0.9)
    trees = mat_principled('Bosque', (0.10, 0.22, 0.20), 0.9)
    verts, faces = [], []
    n = 180; R0, R1 = 210.0, 260.0
    for i in range(n + 1):
        a = i / n * math.tau
        h = 6 + 10 * (0.5 + 0.5 * math.sin(a * 3.1 + 1.0)) * (0.5 + 0.5 * math.sin(a * 7.3))
        for r, z in ((R0, -0.5), (R0 + 12, h * 0.6), (R1, h)):
            verts.append((math.cos(a) * r, math.sin(a) * r, z))
    for i in range(n):
        for j in range(2):
            a0 = i * 3 + j; faces.append((a0, a0 + 3, a0 + 4, a0 + 1))
    me = bpy.data.meshes.new('Orilla'); me.from_pydata(verts, [], faces); me.update()
    o = bpy.data.objects.new('Orilla', me); bpy.context.scene.collection.objects.link(o); me.materials.append(hill)
    # Pinos: conos sobre la orilla
    bpy.ops.mesh.primitive_cone_add(vertices=8, radius1=1.0, depth=3.0, location=(0, 0, -50)); cone = bpy.context.object; cone.data.materials.append(trees)
    for i in range(420):
        a = rng.uniform(0, math.tau)
        r = rng.uniform(R0 + 4, R0 + 30)
        h = rng.uniform(5, 12)
        c = cone.copy(); c.data = cone.data; bpy.context.scene.collection.objects.link(c)
        c.location = (math.cos(a) * r, math.sin(a) * r, h * 0.5 - 0.5)
        c.scale = (h * 0.28, h * 0.28, h / 3.0)
    bpy.data.objects.remove(cone, do_unlink=True)

def hero_rig():
    arm, lo, hi = load_hero()
    size = hi - lo; center = (hi + lo) / 2
    k = 1.5 / size.z
    root = bpy.data.objects.new('Heroe', None); bpy.context.scene.collection.objects.link(root)
    arm.scale = arm.scale * k
    arm.location = (arm.location - Vector((center.x, center.y, lo.z)) * k)
    arm.parent = root
    # Llamas del propulsor: conos con emisión que salen de la espalda hacia abajo
    fl = bpy.data.materials.new('Llama'); fl.use_nodes = True; nt = fl.node_tree; nt.nodes.clear()
    out = nt.nodes.new('ShaderNodeOutputMaterial'); em = nt.nodes.new('ShaderNodeEmission')
    lw = nt.nodes.new('ShaderNodeLayerWeight'); lw.inputs['Blend'].default_value = 0.35
    ramp = nt.nodes.new('ShaderNodeValToRGB'); nt.links.new(lw.outputs['Facing'], ramp.inputs['Fac'])
    ramp.color_ramp.elements[0].color = (1.0, 0.95, 0.7, 1); ramp.color_ramp.elements[1].color = (1.0, 0.35, 0.05, 1)
    nt.links.new(ramp.outputs['Color'], em.inputs['Color']); em.inputs['Strength'].default_value = 14
    tr = nt.nodes.new('ShaderNodeBsdfTransparent'); mix = nt.nodes.new('ShaderNodeMixShader')
    nt.links.new(lw.outputs['Facing'], mix.inputs['Fac']); nt.links.new(em.outputs[0], mix.inputs[1]); nt.links.new(tr.outputs[0], mix.inputs[2])
    nt.links.new(mix.outputs[0], out.inputs['Surface'])
    for s in (-1, 1):
        bpy.ops.mesh.primitive_cone_add(vertices=24, radius1=0.11, radius2=0.0, depth=1.1, location=(s * 0.13, 0.36, -0.05))
        c = bpy.context.object; c.rotation_euler = (math.radians(180 - 8), 0, 0); c.data.materials.append(fl); c.parent = root
        bpy.ops.object.shade_smooth()
    l = bpy.data.objects.new('LuzLlama', bpy.data.lights.new('LuzLlama', 'POINT')); l.data.energy = 120; l.data.color = (1, 0.55, 0.2)
    l.location = (0, 0.4, 0.1); l.parent = root; bpy.context.scene.collection.objects.link(l)
    return root

def splash(rng, base):
    wmat = mat_principled('Gota', (0.85, 0.95, 1.0), 0.02, trans=1.0, ior=1.33)
    foam = mat_principled('Espuma', (0.95, 0.98, 1.0), 0.8)
    # Gotas en arcos alrededor (subiendo con el pez)
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=2, radius=1.0, location=(0, 0, -50)); drop = bpy.context.object; drop.data.materials.append(wmat)
    bpy.ops.object.shade_smooth()
    for i in range(260):
        a = rng.uniform(0, math.tau); t = rng.uniform(0.05, 1.0)
        r = 0.35 + t * rng.uniform(0.8, 2.2)
        z = 0.1 + 2.2 * t * (1.2 - t) * rng.uniform(0.6, 1.3)
        s = rng.uniform(0.015, 0.06) * (1.2 - t)
        d = drop.copy(); d.data = drop.data; bpy.context.scene.collection.objects.link(d)
        d.location = base + Vector((math.cos(a) * r, math.sin(a) * r, z))
        d.scale = (s, s, s * rng.uniform(1.2, 2.2))
    # Columna de agua que lo sigue y corona de salpicadura
    for i in range(70):
        t = rng.uniform(0, 1); a = rng.uniform(0, math.tau); r = rng.uniform(0.05, 0.35) * (1 - t * 0.5)
        d = drop.copy(); d.data = drop.data; bpy.context.scene.collection.objects.link(d)
        d.location = base + Vector((math.cos(a) * r, math.sin(a) * r + 0.2, 0.05 + t * 0.9))
        s = rng.uniform(0.04, 0.1); d.scale = (s, s, s * 2.5)
    bpy.data.objects.remove(drop, do_unlink=True)
    bpy.ops.mesh.primitive_cone_add(vertices=48, radius1=0.55, radius2=1.25, depth=0.75, end_fill_type='NOTHING', location=base + Vector((0, 0, 0.32)))
    crown = bpy.context.object; crown.data.materials.append(wmat)
    tex = bpy.data.textures.new('Ondas', 'CLOUDS'); tex.noise_scale = 0.25
    sub = crown.modifiers.new('Sub', 'SUBSURF'); sub.levels = 2; sub.render_levels = 2
    dsp = crown.modifiers.new('Ondas', 'DISPLACE'); dsp.texture = tex; dsp.strength = 0.18

def bird(name, pos, target, wingspan, bank):
    root, lo, hi = load_bird(name)
    size = hi - lo; c = (hi + lo) / 2
    k = wingspan / size.x
    holder = bpy.data.objects.new(name + '_Vuelo', None); bpy.context.scene.collection.objects.link(holder)
    root.parent = holder; root.scale = (k, k, k); root.location = -c * k
    d = (Vector(target) - Vector(pos)).normalized()
    holder.location = pos
    q = d.to_track_quat('-Y', 'Z')
    holder.rotation_euler = (q @ Euler((0, bank, 0)).to_quaternion()).to_euler()
    return holder

def lights():
    sc = bpy.context.scene
    sun = bpy.data.objects.new('Sol', bpy.data.lights.new('Sol', 'SUN')); sc.collection.objects.link(sun)
    sun.data.energy = 3.2; sun.data.color = (1.0, 0.86, 0.68); sun.data.angle = math.radians(2)
    sun.rotation_euler = (-SUN_DIR).to_track_quat('-Z', 'Y').to_euler()
    key = bpy.data.objects.new('Clave', bpy.data.lights.new('Clave', 'AREA')); sc.collection.objects.link(key)
    key.data.energy = 900; key.data.size = 4; key.data.color = (1.0, 0.9, 0.78)
    key.location = (4.5, -6.0, 5.0); key.rotation_euler = (Vector((0, 0, 1.8)) - key.location).to_track_quat('-Z', 'Y').to_euler()
    rim = bpy.data.objects.new('Borde', bpy.data.lights.new('Borde', 'AREA')); sc.collection.objects.link(rim)
    rim.data.energy = 700; rim.data.size = 3; rim.data.color = (0.45, 0.8, 1.0)
    rim.location = (-3.5, 4.0, 3.5); rim.rotation_euler = (Vector((0, 0, 1.8)) - rim.location).to_track_quat('-Z', 'Y').to_euler()

def build():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    rng = random.Random(7)
    sc = bpy.context.scene
    sc.render.engine = 'CYCLES'; sc.cycles.device = 'CPU'; sc.cycles.use_denoising = True
    sc.view_settings.view_transform = 'Standard'; sc.view_settings.look = 'Medium High Contrast'
    world(); water(); shore(rng); lights()
    hero = hero_rig()
    hero.location = (0.0, 0.0, 0.42)
    hero.rotation_euler = (math.radians(-6), math.radians(-9), math.radians(12))
    splash(rng, Vector((0.0, 0.25, 0.0)))
    bird('Ave_Garza', (-2.3, -0.4, 3.3), (0.2, -0.1, 1.7), 3.6, math.radians(-25))
    bird('Ave_Gaviota', (-0.6, 6.0, 5.8), (0.0, 0.0, 2.0), 2.8, math.radians(20))
    bird('Ave_Cormoran', (-7.5, 9.5, 6.2), (0.0, 0.0, 2.2), 3.2, math.radians(-12))
    cam = bpy.data.objects.new('Camara', bpy.data.cameras.new('Camara')); sc.collection.objects.link(cam); sc.camera = cam
    return sc, cam

def shoot(sc, cam, pos, look, lens, w, h, samples, path, shift_x=0.0, shift_y=0.0):
    cam.location = Vector(pos); cam.data.lens = lens
    cam.rotation_euler = (Vector(look) - cam.location).to_track_quat('-Z', 'Y').to_euler()
    cam.data.shift_x = shift_x; cam.data.shift_y = shift_y
    sc.render.resolution_x = w; sc.render.resolution_y = h; sc.cycles.samples = samples
    sc.render.filepath = path
    bpy.ops.render.render(write_still=True)

if __name__ == '__main__':
    out = sys.argv[-2]; quality = sys.argv[-1]
    sc, cam = build()
    s = 24 if quality == 'borrador' else 160
    f = 1 if quality == 'borrador' else 3
    shoot(sc, cam, (1.3, -4.1, 0.5), (-1.0, 0.8, 1.85), 22, 640 * f, 360 * f, s, os.path.join(out, 'arte_ancho.png'))
    shoot(sc, cam, (1.15, -3.9, 0.55), (-0.6, 0.8, 2.15), 20, 420 * f, 334 * f, s, os.path.join(out, 'arte_portada.png'))
    shoot(sc, cam, (1.5, -5.4, 0.55), (-2.6, 1.0, 2.05), 19, 640 * f, 200 * f, s, os.path.join(out, 'arte_banner.png'))
