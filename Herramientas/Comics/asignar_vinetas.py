# -*- coding: utf-8 -*-
"""Viñetas del modo Historia, paso 3: pone los dibujos (Assets/Art/Comics/<id>.jpg o .png) y los textos de guion.py en los
cómics del juego (Assets/Data/Comics/<Comic>.asset). Reescribe solo la lista de viñetas de cada cómic (el título, la
música y lo demás quedan igual). Una viñeta cuyo dibujo todavía no existe queda en blanco (con su texto).

    python3 Herramientas/Comics/asignar_vinetas.py
"""
import os, sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from guion import COMICS

REPO = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..'))
ART = os.path.join(REPO, 'Assets', 'Art', 'Comics')
DATA = os.path.join(REPO, 'Assets', 'Data', 'Comics')
SPRITE_FILE_ID = 21300000      # el sprite único de una textura importada como Sprite (Single)


def yaml_text(s):
    """Texto entre comillas como lo escribe Unity (lo que no es ASCII, como \\uXXXX)."""
    out = []
    for ch in s:
        if ch == '"':
            out.append('\\"')
        elif ch == '\\':
            out.append('\\\\')
        elif ord(ch) < 128:
            out.append(ch)
        else:
            out.append('\\u%04X' % ord(ch))
    return '"' + ''.join(out) + '"'


def guid_of(png):
    meta = png + '.meta'
    if not os.path.exists(meta):
        return None
    with open(meta, encoding='utf-8') as f:
        for line in f:
            if line.startswith('guid:'):
                return line.split(':', 1)[1].strip()
    return None


def main():
    for comic, vinetas in COMICS:
        path = os.path.join(DATA, comic + '.asset')
        with open(path, encoding='utf-8') as f:
            text = f.read()
        i = text.find('\n  panels:')
        if i < 0:
            print('SIN VIÑETAS', comic)
            continue
        lines = ['  panels:']
        for vid, amb, texto in vinetas:
            guid = guid_of(os.path.join(ART, vid + '.jpg')) or guid_of(os.path.join(ART, vid + '.png'))
            img = '{fileID: %d, guid: %s, type: 3}' % (SPRITE_FILE_ID, guid) if guid else '{fileID: 0}'
            lines.append('  - image: %s' % img)
            lines.append('    caption: %s' % yaml_text(texto))
        # Lo que viene después de la lista (música del cómic...) se conserva
        resto = text[i + 1:].split('\n')[1:]
        k = 0
        while k < len(resto) and (resto[k].startswith('  - ') or resto[k].startswith('    ')):
            k += 1
        cola = '\n'.join(l for l in resto[k:] if l.strip())
        new = text[:i + 1] + '\n'.join(lines) + '\n' + (cola + '\n' if cola else '')
        with open(path, 'w', encoding='utf-8', newline='\n') as f:
            f.write(new)
        print('%-24s %d viñetas' % (comic, len(vinetas)))


if __name__ == '__main__':
    main()
