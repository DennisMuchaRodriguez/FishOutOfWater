# -*- coding: utf-8 -*-
"""Jefe del nivel 15: El Cormorán Rey. El cormorán con corona de oro, collar rojo, cejas de enfado y
plumaje negro con brillo verde azulado y morado. Se modela al tamaño del cormorán; los datos lo agrandan (x2.2)."""
import os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), '..'))
import kit
import Ave_Cormoran

ASSET = 'Jefe_CormoranRey'

if __name__ == '__main__':
    kit.run_bird(ASSET, lambda: Ave_Cormoran.construir(jefe=True), previas='--sin-previas' not in sys.argv,
                 refs=Ave_Cormoran.REFS)
