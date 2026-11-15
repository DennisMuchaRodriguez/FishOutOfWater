# -*- coding: utf-8 -*-
"""Paso 1 de los bocetos del Archivo de aves: renderiza cada modelo con Blender (Cycles en CPU, sin GPU).
    <python con bpy> Herramientas/Archivo/render_aves.py <carpeta de salida>
Por cada ave de Assets/Models/Aves deja dos imágenes con fondo transparente:
  <Nombre>_luz.png     arcilla gris con luz de arriba a la izquierda (sombras para el sombreado a rayas)
  <Nombre>_normal.png  normales como color (para encontrar los contornos y pliegues)
El paso 2 (bocetos.py) las convierte en bocetos holográficos para Assets/Resources/Archivo."""
import os, sys, math, glob
import bpy
from mathutils import Vector

REPO = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..'))
OUT = sys.argv[-1] if len(sys.argv) > 1 and not sys.argv[-1].endswith('.py') else os.path.join(REPO, '..', 'renders_aves')
RES = 640


def clear():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def setup(scene):
    scene.render.engine = 'CYCLES'
    scene.cycles.device = 'CPU'
    scene.render.resolution_x = scene.render.resolution_y = RES
    scene.render.film_transparent = True
    scene.render.image_settings.file_format = 'PNG'
    scene.render.image_settings.color_mode = 'RGBA'
    try:
        scene.view_settings.view_transform = 'Standard'
    except Exception:
        pass


def clay_material():
    m = bpy.data.materials.new('Arcilla')
    m.use_nodes = True
    bsdf = m.node_tree.nodes.get('Principled BSDF')
    bsdf.inputs['Base Color'].default_value = (0.8, 0.8, 0.8, 1)
    bsdf.inputs['Roughness'].default_value = 0.9
    return m


def normal_material():
    m = bpy.data.materials.new('Normales')
    m.use_nodes = True
    nt = m.node_tree
    for n in list(nt.nodes):
        nt.nodes.remove(n)
    out = nt.nodes.new('ShaderNodeOutputMaterial')
    em = nt.nodes.new('ShaderNodeEmission')
    geo = nt.nodes.new('ShaderNodeNewGeometry')
    vm = nt.nodes.new('ShaderNodeVectorMath')
    vm.operation = 'MULTIPLY_ADD'
    vm.inputs[1].default_value = (0.5, 0.5, 0.5)
    vm.inputs[2].default_value = (0.5, 0.5, 0.5)
    nt.links.new(geo.outputs['Normal'], vm.inputs[0])
    nt.links.new(vm.outputs[0], em.inputs['Color'])
    nt.links.new(em.outputs[0], out.inputs['Surface'])
    return m


def assign(meshes, mat):
    for o in meshes:
        o.data.materials.clear()
        o.data.materials.append(mat)


def render_bird(fbx, out_png):
    clear()
    scene = bpy.context.scene
    setup(scene)
    bpy.ops.import_scene.fbx(filepath=fbx)
    meshes = [o for o in bpy.data.objects if o.type == 'MESH']
    if not meshes:
        return False
    pts = []
    for o in meshes:
        for c in o.bound_box:
            pts.append(o.matrix_world @ Vector(c))
    lo = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
    hi = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
    center = (lo + hi) / 2
    radius = (hi - lo).length / 2
    cam_data = bpy.data.cameras.new('cam')
    cam_data.type = 'ORTHO'
    cam_data.ortho_scale = radius * 2.15
    cam = bpy.data.objects.new('cam', cam_data)
    scene.collection.objects.link(cam)
    scene.camera = cam
    # Vista de tres cuartos desde arriba y de frente-izquierda (alas abiertas, se ve la cabeza)
    beak_front = max(p.y for p in pts) - center.y > center.y - min(p.y for p in pts)
    d = Vector((-0.6, 0.85 if beak_front else -0.85, 1.0)).normalized()
    cam.location = center + d * radius * 4
    cam.rotation_euler = (-d).to_track_quat('-Z', 'Y').to_euler()
    bpy.context.view_layer.update()
    # Encuadre justo: mide la caja proyectada en la cámara
    inv = cam.matrix_world.inverted()
    loc = [inv @ p for p in pts]
    w = max(p.x for p in loc) - min(p.x for p in loc)
    h = max(p.y for p in loc) - min(p.y for p in loc)
    cx = (max(p.x for p in loc) + min(p.x for p in loc)) / 2
    cy = (max(p.y for p in loc) + min(p.y for p in loc)) / 2
    cam_data.ortho_scale = max(w, h) * 1.12
    cam_data.shift_x = cx / cam_data.ortho_scale
    cam_data.shift_y = cy / cam_data.ortho_scale

    # Luz: sol de arriba a la izquierda + cielo suave
    world = bpy.data.worlds.new('Cielo')
    scene.world = world
    world.use_nodes = True
    world.node_tree.nodes['Background'].inputs[1].default_value = 0.35
    sun = bpy.data.objects.new('Sol', bpy.data.lights.new('Sol', 'SUN'))
    sun.data.energy = 4.0
    scene.collection.objects.link(sun)
    # La luz viaja hacia abajo (y un poco de lado): el -Z del sol apunta en esa dirección
    sun.rotation_euler = Vector((0.45, -0.35 if beak_front else 0.35, -1.0)).normalized().to_track_quat('-Z', 'Y').to_euler()

    base = os.path.splitext(out_png)[0]
    assign(meshes, clay_material())
    scene.cycles.samples = 24
    scene.render.filepath = base + '_luz.png'
    bpy.ops.render.render(write_still=True)

    assign(meshes, normal_material())
    scene.cycles.samples = 1
    scene.cycles.use_denoising = False
    scene.render.filepath = base + '_normal.png'
    bpy.ops.render.render(write_still=True)
    return True


def main():
    os.makedirs(OUT, exist_ok=True)
    for fbx in sorted(glob.glob(os.path.join(REPO, 'Assets', 'Models', 'Aves', '*', '*.fbx'))):
        name = os.path.splitext(os.path.basename(fbx))[0]
        ok = render_bird(fbx, os.path.join(OUT, name + '.png'))
        print('RENDER', name, ok)


if __name__ == '__main__':
    main()
