# -*- coding: utf-8 -*-
"""Jefe del nivel 10: El Pelícano Saco Sin Fondo. El pelícano con una bolsa gigantesca llena de peces
(se ven sus siluetas), plumaje sucio y cejas pícaras. Se modela al tamaño del pelícano; los datos lo agrandan (x2)."""
import os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), '..'))
import kit
import Ave_Pelicano

ASSET = 'Jefe_PelicanoSaco'

if __name__ == '__main__':
    kit.run_bird(ASSET, lambda: Ave_Pelicano.construir(jefe=True), previas='--sin-previas' not in sys.argv,
                 refs=Ave_Pelicano.REFS)
