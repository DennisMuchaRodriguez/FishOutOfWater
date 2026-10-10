# -*- coding: utf-8 -*-
"""Jefe del nivel 5: La Garza Gris Gigante. Misma garza, gris acero, cejas de enfado, cicatrices,
penacho más largo y pico más grande. Se modela al tamaño de la garza; los datos la agrandan (x2.2)."""
import os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), '..'))
import kit
import Ave_Garza

ASSET = 'Jefe_GarzaGris'

if __name__ == '__main__':
    kit.run_bird(ASSET, lambda: Ave_Garza.construir(jefe=True), previas='--sin-previas' not in sys.argv,
                 refs=Ave_Garza.REFS)
