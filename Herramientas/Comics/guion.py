# -*- coding: utf-8 -*-
"""Guion de los cómics del modo Historia: cada cómic (Assets/Data/Comics/<Comic>.asset) y sus viñetas, con el texto
del recuadro de narración. Lo usan render_vinetas.py (qué escena armar), vinetas.py (los bocetos) y
asignar_vinetas.py (pone dibujos y textos en los .asset).

La historia: un pez pequeño se cansa de que las aves se lleven a su cardumen, se arma un traje con la chatarra del
fondo del lago (un frasco de escafandra, un propulsor y una pistola de agua) y sale a pelear. Después del nivel 3
conoce al viejo bagre mecánico del Taller («¡Así que tú eres el pez del traje!»), que le mejora el traje. Vence a la
Garza Gris Gigante, al Pelícano Saco Sin Fondo, al Cormorán Rey y a la Reina Águila Marina, y el lago queda en calma.
"""

# (comic, [(id de la viñeta, ambiente, texto)])
#   ambiente: dia, amanecer, niebla, atardecer, cielo_rojo, bajo_agua, cueva  (luz y aguada de la viñeta)
COMICS = [
    ('Comic_00_Origen', [
        ('origen_1', 'amanecer', "En el lago, los peces vivían tranquilos entre los nenúfares."),
        ('origen_2', 'dia', "Hasta que llegaron las aves. Cada día se llevaban más peces del cardumen."),
        ('origen_3', 'bajo_agua', "Un pez pequeño se cansó de esconderse entre las algas."),
        ('origen_4', 'bajo_agua', "En el fondo había chatarra: latas, ruedas, frascos... y un motor viejo."),
        ('origen_5', 'bajo_agua', "Con todo eso se armó un traje: escafandra, propulsor y una pistola de agua."),
        ('origen_6', 'dia', "Los peces no saben volar... ¡hasta ahora!"),
    ]),
    ('Comic_Taller_Primera', [
        ('taller_1', 'cueva', "Bajo el lago hay una cueva llena de chatarra... y de ideas."),
        ('taller_2', 'cueva', "Un viejo bagre mecánico arregla todo lo que cae al agua."),
        ('taller_3', 'cueva', "«Ese traje se puede mejorar», dijo, ajustándose los lentes."),
    ]),
    ('Comic_05_Jefe_Antes', [
        ('garza_antes_1', 'niebla', "Entre la niebla, una sombra enorme planeaba sobre los juncos."),
        ('garza_antes_2', 'niebla', "La Garza Gris Gigante: cada estocada de su pico levanta una ola."),
        ('garza_antes_3', 'dia', "Cuando se canse y baje la guardia, ¡dispárale con todo!"),
    ]),
    ('Comic_05_Jefe_Despues', [
        ('garza_despues_1', 'dia', "La garza huyó con las plumas revueltas. ¡El cardumen está a salvo!"),
        ('garza_despues_2', 'cueva', "Abajo, en el Taller, el bagre ya tenía otra pieza lista."),
    ]),
    ('Comic_10_Jefe_Antes', [
        ('pelicano_antes_1', 'dia', "Un pico enorme apareció en el horizonte."),
        ('pelicano_antes_2', 'dia', "El Pelícano Saco Sin Fondo quiere tragarse el cardumen de un bocado."),
        ('pelicano_antes_3', 'dia', "Hay que vaciarle el saco... ¡a chorros de agua!"),
    ]),
    ('Comic_10_Jefe_Despues', [
        ('pelicano_despues_1', 'dia', "Los peces escaparon del saco y el pelícano se fue con el pico vacío."),
        ('pelicano_despues_2', 'dia', "Desde ese día, otros peces se ponen un traje para pelear a tu lado."),
    ]),
    ('Comic_15_Jefe_Antes', [
        ('cormoran_antes_1', 'bajo_agua', "Esta vez el peligro venía por debajo del agua."),
        ('cormoran_antes_2', 'bajo_agua', "El Cormorán Rey bucea y vuela: pelea arriba y abajo del agua."),
        ('cormoran_antes_3', 'bajo_agua', "Ni el fondo del lago es seguro. ¡A defender el cardumen!"),
    ]),
    ('Comic_15_Jefe_Despues', [
        ('cormoran_despues_1', 'dia', "El rey salió volando, empapado y sin ganas de pescar."),
        ('cormoran_despues_2', 'cueva', "El bagre revisó el traje: «Lo que viene es más grande. Prepárate»."),
    ]),
    ('Comic_20_Jefe_Antes', [
        ('reina_antes_1', 'cielo_rojo', "El cielo se tiñó de rojo. Todas las aves callaron."),
        ('reina_antes_2', 'cielo_rojo', "La Reina Águila Marina bajó de las nubes, con las garras por delante."),
        ('reina_antes_3', 'cielo_rojo', "Con un grito llamó a sus gaviotas: el cielo se llenó de alas."),
        ('reina_antes_4', 'cielo_rojo', "La última batalla. ¡Por el lago y por el cardumen!"),
    ]),
    ('Comic_20_Final', [
        ('final_1', 'atardecer', "La reina y su bandada se fueron lejos, muy lejos."),
        ('final_2', 'atardecer', "El lago volvió a estar en calma."),
        ('final_3', 'bajo_agua', "Todo el cardumen nadó alrededor del pez del traje."),
        ('final_4', 'cueva', "En el Taller, el bagre guardó el traje en su vitrina, con mucho cuidado."),
        ('final_5', 'atardecer', "Y el pez pequeño, por fin, pudo descansar."),
        ('final_6', 'cueva', "Pero si algún día vuelven las aves... el traje estará listo."),
    ]),
]

VINETAS = [(vid, comic, amb, texto) for comic, vs in COMICS for (vid, amb, texto) in vs]
AMBIENTE = {vid: amb for vid, _, amb, _ in VINETAS}
