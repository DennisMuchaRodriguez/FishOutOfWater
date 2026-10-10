# -*- coding: utf-8 -*-
"""Genera los modelos del entorno (cada uno en su propio proceso, con la escena limpia).
    python build_all.py                      # todos
    python build_all.py Alga_A Lata          # solo esos
    python build_all.py --sin-previas        # sin vistas previas (más rápido)
Necesita Python con el módulo bpy (o Blender: blender -b --python <script>)."""
import os, sys, subprocess, time

AQUI = os.path.dirname(os.path.abspath(__file__))
TODOS = ['Pino_A', 'Pino_B', 'Abeto', 'Arbol_Hoja', 'Arbusto', 'Hierba', 'Flores', 'Juncos', 'Totora', 'Nenufar',
         'Roca_A', 'Piedras_Fondo', 'Tronco_Hundido', 'Alga_A', 'Alga_B', 'Alga_Brillante', 'Lata', 'Bateria',
         'Rueda_Bici']


def main():
    extra = [a for a in sys.argv[1:] if a.startswith('--')]
    nombres = [a for a in sys.argv[1:] if not a.startswith('--')] or TODOS
    fallos = []
    for n in nombres:
        script = os.path.join(AQUI, n + '.py')
        if not os.path.exists(script):
            print('FALTA', script)
            fallos.append(n)
            continue
        t = time.time()
        r = subprocess.run([sys.executable, script] + extra, capture_output=True, text=True)
        lineas = [l for l in r.stdout.splitlines() if l.startswith(('TRIANGULOS', 'TAMANO_UNITY', 'HOJA'))]
        print('%-16s %s  %.0f s' % (n, 'OK' if r.returncode == 0 else 'ERROR', time.time() - t))
        for l in lineas:
            print('    ' + l)
        if r.returncode != 0:
            fallos.append(n)
            print(r.stderr[-3000:])
    if fallos:
        print('FALLARON:', ', '.join(fallos))
        sys.exit(1)


if __name__ == '__main__':
    main()
