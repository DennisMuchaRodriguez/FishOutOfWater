# -*- coding: utf-8 -*-
"""Jefe final (nivel 20): La Reina Águila Marina, mezcla de pigargo europeo y pigargo americano.
Cuerpo pardo oscuro, cabeza y cuello blanco crema, pico amarillo enorme y ganchudo, cola blanca en cuña,
alas anchas como tablones con "dedos", patas amarillas con uñas negras, tiara dorada y cejas regias.
Envergadura base ~3.8 m (los datos la agrandan x2.4). Lleva el pez en las garras."""
import os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), '..'))
import kit
import Ave_AguilaPescadora as rap

ASSET = 'Jefe_ReinaAguila'

REINA = dict(
    escala=1.1, cabeza=1.15, pico=(0.21, 0.062, 0.115), pico_color='#F0C030', cera='#F3CF55',
    pardo='#4A3524', pardo_claro='#7A5A3C', blanco='#4A3524', cabeza_color='#EFE8D8', cuello_hasta=-0.33,
    antifaz=False, collar=False, cola_color='#F2EFE8', cola_barras=False, pata='#EDC23A', una='#141416',
    alas_le=[(0.14, -0.20, 0.10), (0.60, -0.26, 0.15), (1.05, -0.26, 0.13), (1.42, -0.18, 0.07), (1.70, -0.04, 0.02)],
    alas_te=[(0.14, 0.26, 0.08), (0.60, 0.32, 0.13), (1.05, 0.32, 0.11), (1.42, 0.30, 0.06), (1.70, 0.18, 0.02)],
    dedos=7, ala_abajo_clara=False, corona=True, cejas=True, cresta=False,
)


def construir():
    return rap.rapaz(ASSET, REINA)


def _refs():
    base = os.environ.get('KIT_REFS', '')
    out = []
    for carpeta in ('pigargo', 'aguila_calva'):
        d = os.path.join(base, carpeta)
        if base and os.path.isdir(d):
            out += [os.path.join(d, f) for f in sorted(os.listdir(d)) if f.endswith(('.jpg', '.jpeg'))][:2]
    return out


REFS = _refs()

if __name__ == '__main__':
    kit.run_bird(ASSET, construir, previas='--sin-previas' not in sys.argv, refs=REFS)
