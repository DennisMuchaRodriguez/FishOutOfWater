# Kit de modelado de aves (Blender por código)

Genera las aves enemigas con el mismo estilo de plastilina que el pez protagonista: formas redondas,
ojos saltones, textura pintada con grano, manchas suaves, sombras horneadas y trazos en las plumas.
Cada especie es un script en `aves/` que usa `kit.py` y deja el modelo listo para Unity.

## Cómo se ejecuta
Necesitas Blender 5.x o el módulo `bpy` de Python (`pip install bpy`, Python 3.13):

```
python Herramientas/Modelado/aves/Ave_Gaviota.py              # modelo + textura + metas + vistas previas
python Herramientas/Modelado/aves/Ave_Gaviota.py --sin-previas
```
Con Blender instalado: `blender -b --python Herramientas/Modelado/aves/Ave_Gaviota.py`.
Las vistas previas se guardan en la carpeta de la variable `KIT_PREVIEWS` (por defecto `../previas`).

Salida en `Assets/Models/Aves/<Nombre>/`: `<Nombre>.fbx`, `<Nombre>_BaseColor.png` (1024×1024),
`<Nombre>.mat` (URP Lit) y sus `.meta` con los GUID de `guids.json` (así los datos del juego ya los encuentran).

## Reglas que usa el juego (BirdAI)
- 1 unidad = 1 m. En Blender el pico mira a **-Y**, arriba es **+Z** y el ala izquierda está en **+X**
  (en Unity queda mirando a +Z).
- Jerarquía: raíz `<Nombre>` → `Cuerpo` (todo lo que no aletea), `L_wing` y `R_wing` (las mallas de las alas,
  con el origen en el hombro) y los puntos `LeftFoot` / `RightFoot` (de ahí cuelga el pez atrapado; en las aves
  que pescan con el pico están en la punta del pico).
- No metas mallas dentro de vacíos: el exportador FBX de Blender (con *Apply Transform*) las deja giradas 90°.
- `BirdAI` aletea girando `L_wing` y `R_wing` sobre el eje de avance, así que el pivote del ala va en el hombro.
- Un solo material `M_<Nombre>`; el `.meta` del FBX lo reemplaza por el `.mat` del proyecto.

## Piezas del kit (`kit.py`)
| Función | Para qué |
|---|---|
| `loft(nombre, estaciones, ...)` | Tubo suave por una espina (cuerpo, cuello, patas, plumas largas). Cada estación: `(x, y, z, ancho, alto_arriba, alto_abajo)` |
| `ellipsoid`, `torus` | Esferas/elipsoides y aros |
| `blade(nombre, borde_ataque, borde_salida, grosor, feathers=...)` | Superficie con perfil: alas, colas, membranas. `feathers` festonea el borde en plumas |
| `eye(...)` | Ojo saltón: blanco + pupila + brillo (como el del pez) |
| `beak(...)` | Pico: `hook` (gancho), `gonys` (bulto inferior), `droop` (curvo), `split` (dos mandíbulas) |
| `tail_fan(...)` | Cola en abanico (`fork` la hace en horquilla) |
| `leg`, `foot_webbed`, `foot_talons` | Patas, pies palmeados y garras con uñas |
| `feather`, `spike`, `crest`, `plume` | Plumas sueltas, púas, crestas y penachos |
| `crown`, `brow`, `glasses` | Accesorios (corona, cejas de enfado, gafas) |
| `Bird(nombre, wing_pivot, feet)` | Junta las piezas: `add(piezas, group='body' | 'wing')` (el ala derecha se refleja sola) |
| `Bird.build()` / `texture()` / `assemble()` / `export()` | Plastilina + atlas UV, horneado y pintura, jerarquía, FBX |
| `write_unity_files(nombre, carpeta)` | `.meta` del FBX/PNG/carpeta y el material con los GUID del registro |
| `fbx_summary(fbx)` | Revisa ejes, escala, nodos y tamaño en Unity |
| `render_previews(fbx, carpeta)` | Reimporta el FBX y lo renderiza (frente, lado, arriba, 3/4, prueba de aleteo ±30°) |

## Pintura
Cada pieza lleva una función `paint(T)` que recibe los texeles de la pieza: posición (`T.pos`, `T.y`, …), normal
(`T.nz`), coordenadas propias (`T.u` a lo largo, `T.v` a lo ancho, `T.side` arriba/abajo de un ala) y las plumas
(`T.fc`, `T.fseg`). Helpers: `C('#hex')`, `mix(base, color, máscara)`, `shade` (luz pintada), `feather_strokes`
(trazos como los radios de las aletas del pez). El kit añade solo el grano, las manchas y la oclusión ambiental.

## Nueva especie
1. Copia `aves/Ave_Gaviota.py` con el nombre del registro (`guids.json`, sección `birds` o `characters`).
2. Cambia las estaciones del cuerpo, el pico, la cola, las alas y las funciones de pintura.
3. Ejecuta y mira las vistas previas; repite hasta que se reconozca la especie.
