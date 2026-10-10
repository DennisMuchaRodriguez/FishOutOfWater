# Guía del proyecto — Fish Out Of Water

Referencia completa de **cómo está armado el juego** y de **todos los valores del Inspector**.
Los valores "En escena" son los que tiene ahora `SampleScene` (los que no aparecen son iguales al valor por defecto).

**Índice**
1. [La cámara: qué pasaba y cómo funciona ahora](#1-la-cámara)
2. [Estructura de la escena](#2-estructura-de-la-escena)
3. [Cómo se genera todo al darle Play](#3-cómo-se-genera-todo-al-darle-play)
4. [Flujo de la partida (oleadas, victoria y derrota)](#4-flujo-de-la-partida)
   - [Menú, mapa de niveles, cómics y progreso](#menú-mapa-de-niveles-cómics-y-progreso)
5. [Inspector: GameDirector y archivos de datos (Niveles, Aves, Peces, Cómics)](#5-inspector-gamedirector)
6. [Inspector: agua (LakeVolume, LakeWater y material del agua)](#6-inspector-agua)
7. [Inspector: PlayerController_Base (el pez protagonista)](#7-inspector-playercontroller_base)
8. [Inspector: PlayerShooting y SuitFX](#8-inspector-playershooting-y-suitfx)
9. [Inspector: interfaz (UI_PlayerStatus, HelmetVisorHUD, PauseMenu, GameHUD, MainMenu)](#9-inspector-interfaz)
10. [Componentes que se crean por código (peces, pájaros, munición...)](#10-componentes-creados-por-código)
11. [Cosas a revisar en la escena](#11-cosas-a-revisar-en-la-escena)

---

## 1. La cámara

### Qué pasaba
Las versiones anteriores de la cinemática **cambiaban de cámara**: apagaban la cámara del jugador y encendían otra (o movían la del jugador desde otro script). Si la cinemática no terminaba bien, la cámara activa seguía siendo la de la cinemática apuntando a los pájaros.

### Cómo funciona ahora
- Hay **una sola cámara**: `PlayerCamera`. Nunca se apaga ni se reemplaza.
- `PlayerController_Base.LateUpdate()` coloca esa cámara **todos los frames**:
  1. Calcula la vista normal: posición del jugador + `First Person Offset` (o `Third Person Offset`), más el balanceo al caminar, el vaivén al nadar, la vibración del propulsor, la sacudida por daño, el retroceso del disparo y el FOV dinámico.
  2. Le pregunta a la cinemática si hay un plano activo (`WaveCinematic.TryGetPose`). Si lo hay, **mezcla** la vista normal con el plano según un peso de 0 a 1:
     - 0 → vista normal
     - 1 → plano de la cinemática
- La cinemática (`WaveCinematic`) **no toca ninguna cámara**: solo calcula el plano estilo Star Fox (por encima del hombro, siguiendo a la bandada que llega del cielo) y el peso:
  - Entra en 0.9 s.
  - Dura `Cinematic Duration` segundos. Se salta con **Espacio, Enter o clic** después de 0.6 s.
  - Vuelve a tu vista en 0.7 s.
- **No se puede quedar trabada**:
  - Si la cinemática termina, se salta, se destruye, falla o deja de actualizarse medio segundo, el peso vale 0 y en el siguiente frame la cámara ya muestra la vista normal.
  - Usa tiempo real: no le afecta `Time.timeScale`. En pausa se detiene.
  - Tiene un tope de seguridad de duración + 15 s.
- Mientras dura: el jugador queda congelado (no se mueve ni dispara), el casco y el HUD se ocultan y aparecen las barras de cine. Los controles vuelven cuando la cámara termina de regresar.

### El filtro bajo el agua
- El efecto submarino (el `Underwater Volume` y el tinte del casco) se decide **cada frame según dónde está la cámara**, no según la altura de los ojos del jugador. Por eso también funciona en tercera persona y durante la cinemática.
- Al meter la cámara en el agua el filtro entra de golpe; al sacarla se quita en un instante corto (`Underwater Transition Speed`).
- La cámara nunca queda partida por la superficie: si quedaría justo en la línea del agua, se coloca unos centímetros arriba o abajo. Antes, con media pantalla bajo el agua y el filtro apagado, se veía el fondo gris.

### Qué no hacer
- **No pongas otra cámara activa** en la escena (ni otra "Main Camera"). La que agregaste ("Main Camera ") quedó **desactivada**, porque renderizaba todo dos veces y duplicaba el AudioListener. Puedes borrarla.
- `PlayerCamera` debe estar asignada en `PlayerController_Base > Camera Holder`. Al darle Play se separa del jugador (queda en la raíz de la jerarquía): es normal.

---

## 2. Estructura de la escena

El juego tiene dos escenas (en Build Settings, en este orden):
- **MainMenu**: menú principal, elección de modo y mapa de niveles. Tiene una cámara y un objeto con `MainMenu`, que construye toda la interfaz por código.
- **SampleScene**: el lago donde se juega cada nivel (objetos en la tabla de abajo).

Datos del juego (fuera de las escenas):
- `Assets/Data/Niveles`: los 20 niveles (Nivel_01 a Nivel_20).
- `Assets/Data/Aves`: Ave_Arcilla y Jefe_Provisional.
- `Assets/Data/Peces`: Pez_Naranja, Pez_Dorado y Pez_Rosa.
- `Assets/Data/Comics`: los cómics de la historia (viñetas en blanco por ahora).
- `Assets/Resources/LevelCatalog`: la lista ordenada de niveles que lee el mapa.
- `Assets/Resources/Fonts`: fuentes de la interfaz (Bangers y Lilita One, licencia OFL).

| Objeto (SampleScene) | Componentes propios | Para qué |
|---|---|---|
| **PlayerBase** (tag Player, capa Player) | Rigidbody, `PlayerController_Base`, `PlayerShooting`, `SuitFX` | El pez protagonista: movimiento, cámara, disparo y efectos del traje |
| **PlayerCamera** (tag MainCamera) | Camera, AudioListener | La única cámara del juego (la mueve `PlayerController_Base`) |
| **GameDirector** | `GameDirector`, `LakeVolume` | Cerebro de la partida: genera peces, pájaros y munición, controla las oleadas, la cinemática y la victoria o derrota |
| **Lago_Agua** (tag Water, capa Water) | MeshFilter, MeshRenderer, `LakeWater` | El agua: se genera sola rellenando el hueco del terreno |
| **UiManager** | `UI_PlayerStatus`, `PauseMenu` | Barras de energía, munición y armadura; pausa, muerte y fin de partida |
| **VistaInterior** | `HelmetVisorHUD` | Vista desde dentro del casco (marco, cristal, retícula) |
| **Global Volume** / **Global Volume (1)** | Volume (URP) | Postproceso general / efecto bajo el agua (`Underwater Volume` del jugador) |
| Terrain_* | Terrain | Terreno (capa Default) |
| Water, Sphere (1) (desactivados) | Scripts de Bitgem | Agua vieja del asset. No se usa |
| "Main Camera " (desactivada) | Camera | Duplicada. Bórrala |

**Se crea por código al darle Play** (no lo busques en la jerarquía antes de jugar):
- **Peces**: `Pez_<tipo>_<n>`, con Rigidbody, SphereCollider, `FishAI`, `WaterInteractor` y un hijo `Visual` con tu modelo.
- **Pájaros**: `Ave_<tipo>_<oleada>_<n>`, con Rigidbody, SphereCollider, `Damageable`, `BirdAI`, `WaterInteractor` y un hijo `Visual`.
- **Cápsulas de munición**: `CapsulaMunicion`, con `AmmoPickup`.
- **`GameHUD`** (objetivos, mensajes, flechas y barras de cine): se agrega al objeto GameDirector.
- **`CinematicaOleada`**: un objeto vacío con `WaveCinematic` mientras dura la cinemática.
- Partículas de salpicaduras, burbujas y propulsor: las crean `LakeWater` y `SuitFX`.

**Capas y tags que usa el código** (Project Settings > Tags and Layers):
- Capas: `Player` (3), `Water` (4), `Fish` (6).
- Tags: `Player`, `Enemy` (pájaros), `Fish` (peces), `Water`.

---

## 3. Cómo se genera todo al darle Play

En orden:

1. **`LakeWater.Start`**: construye la malla del agua.
   - Muestrea la altura del terreno en una cuadrícula de celdas de `Cell Size` alrededor del objeto (hasta `Max Extent` metros).
   - Rellena "como balde de pintura" todas las celdas cuyo suelo está por debajo del nivel del agua. El nivel del agua es la **Y del objeto Lago_Agua**.
   - Mete la malla `Shore Overlap` celdas bajo la orilla para que no queden huecos.
   - En el editor (sin Play) se reconstruye sola al mover el objeto o cambiar ajustes.
2. **`GameDirector.Start`**:
   1. Busca al jugador y al `LakeVolume` si no están asignados, y hace `lake.Recalculate()`, que lee la forma del agua.
   2. Si no hay agua válida, muestra un error en la consola y **se apaga** (no habrá peces ni oleadas). Revisa la consola si "no pasa nada".
   3. **Elige el nivel** (`ResolveLevel`): el que elegiste en el mapa del menú. Si diste Play directo en SampleScene, usa `Test Level` (y si está vacío, el primero del catálogo).
   4. **Arma el plan de la partida** (`BuildPlan`) con los datos del nivel: peces, cardúmenes, % de pérdida y oleadas. Si el nivel no tiene peces u oleadas con aves, muestra un error y no empieza.
   5. Agrega `GameHUD`.
   6. **Genera los peces** (`SpawnFish`):
      - Crea `Schools` puntos de cardumen dentro del agua.
      - Por cada tipo de pez del plan, crea su cantidad de peces, repartidos entre los cardúmenes, a 0.5–1.6 m bajo la superficie.
      - A cada pez le pone el modelo con `CreateVisual` y le aplica su tipo (velocidad, aguante, animación).
   7. **Genera las cápsulas de munición** (`Ammo Pickups` veces):
      - Con probabilidad `Underwater Pickup Chance` aparece bajo el agua (1.2–3 m); si no, flota en la superficie.
   8. Arranca el **flujo de la partida** (`GameFlow`).

**`CreateVisual` (cómo se pone tu modelo):**
1. Crea un hijo vacío llamado `Visual`, con la **rotación**, **posición** (`Model Offset`) y **escala** del tipo.
2. Instancia tu modelo dentro de `Visual`, sin rotación ni escala propias.
3. Le quita los colliders (el collider es la esfera del pez o pájaro).
4. Si pusiste `Material Override`, lo aplica a todos los renderers.
5. Pone todo en la capa del actor.
6. Si pusiste `Animator Controller`, lo asigna al Animator del modelo (o le agrega uno) y desactiva el root motion.

Si un pez o un ave **no tiene modelo**, se usa una cápsula de prueba.

---

## 4. Flujo de la partida

`GameFlow` repite esto por cada oleada del plan (las **Waves** del Nivel que se está jugando):

1. **Calma**: cuenta atrás de `Delay Before` segundos (en el HUD aparece "NIVEL n · NOMBRE"). Los peces nadan en cardumen y los cardúmenes se mueven cada 8–14 s.
2. **Aparecen los pájaros** (`SpawnWave`):
   - Elige una dirección: `Arrival Yaw` en grados, o aleatoria si vale -1.
   - Coloca los pájaros de la oleada en **formación en V** a `Arrival Distance` metros del centro del lago y `Arrival Height` metros de altura.
   - Salen exactamente las aves de su lista **Birds** (tipo y cantidad).
   - La vida de cada pájaro es `Health × Health Multiplier` de la oleada.
   - Los pájaros vuelan hacia el lago a 14–20 m de altura. Mientras llegan, no cazan.
3. **Cinemática** (si `Play Arrival Cinematic` está activo y, con `Cinematic Only First Wave`, solo en la primera oleada). El juego espera a que termine.
4. **Oleada activa**: espera hasta que **todos los pájaros de la partida estén muertos**. Entonces muestra "OLEADA SUPERADA" y pasa a la siguiente.
5. Al terminar la última oleada: **victoria** ("¡LAGO A SALVO!") con **1 a 3 estrellas** según los peces salvados (`Two Stars` / `Three Stars` del Nivel). En modo Historia se guardan las estrellas y se desbloquea el nivel siguiente.

**Derrota**: si cazan `TotalFish × Max Fish Loss Fraction` peces (redondeado hacia arriba; la fracción es la del Nivel) o si el jugador muere (armadura en 0).

**Al terminar**: cámara lenta 2 s, el cómic de después (si lo hay y es la primera vez que lo ganas) y la pantalla de resultado (`PauseMenu`).

**Eventos que escucha el HUD**:
- `Banner`: título grande.
- `Toast`: mensajes cortos ("UN PEZ FUE CAZADO", "+10 MUNICIÓN", "DEPREDADOR ABATIDO"...).
- `Letterbox`: barras de cine.

**Peces** (árbol de comportamiento de `FishAI`, por prioridad):
1. Atrapado o muerto → espera (lo lleva el pájaro).
2. Cayendo o en tierra → aletea hacia el agua; si pasa `Suffocate Time` s fuera, muere.
3. Amenaza cerca (un pájaro bajo a menos de `Threat Radius`, o uno que lo caza a 1.5×) → **huye solo por el agua**:
   - Con energía, hacia lo profundo (`Panic Depth`) en zigzag.
   - Agotado, en horizontal cerca de la superficie.
4. Agotado y hondo → sube a respirar.
5. Si no pasa nada de lo anterior → nada en cardumen.

Cuando atrapan a un pez, este alerta a los peces a `Alarm Radius` metros. Si derribas al pájaro, el pez cae y, si llega al agua, se cuenta como "¡PEZ RESCATADO!".

**Pájaros** (árbol de `BirdAI`, por prioridad):
1. Muerto → cae.
2. Llegando → vuela al lago.
3. Aturdido → se queda quieto `Stun Duration` s (2.5) después de embestirte.
4. Lleva un pez → sube y se aleja; en `Carry Time` s se lo come (dispárale para que lo suelte).
5. Pelea contigo → si te ve a menos de `Detect Range` o si le disparaste (te recuerda `Aggro Memory` s). Te persigue y te embiste: te quita `Damage` de armadura y te empuja con `Knockback Force`.
6. Picada fallida → remonta.
7. Caza → elige un pez a menos de `Hunt Range`, lo acecha en círculos `Stalk Time` s y se lanza en picada.
8. Si no pasa nada de lo anterior → patrulla en círculos sobre el lago.

No te ve si estás más hondo que `Player Hidden Depth` bajo el agua.

### Menú, mapa de niveles, cómics y progreso

**Recorrido completo:**
1. **Menú principal** (escena MainMenu): JUGAR, CONTROLES y SALIR. El pez flota junto al título.
2. **Elige modo**:
   - **HISTORIA** (1 jugador): abre el mapa de niveles. La tarjeta muestra tus estrellas y cuántos niveles completaste.
   - **SUPERVIVENCIA** y **COOPERATIVO**: bloqueados, con la etiqueta "PRÓXIMAMENTE" (están en el GDD, todavía no existen).
3. **Mapa de niveles**: 20 nenúfares en zigzag, agrupados en los 4 bloques del GDD.
   - Solo puedes jugar los niveles **desbloqueados**: al principio solo el 1, y cada victoria desbloquea el siguiente (de uno en uno).
   - Cada nivel muestra su número, sus estrellas, un candado si está bloqueado y las etiquetas **JEFE** y **TALLER**.
   - El nivel que te toca jugar late y tiene un anillo. Al hacer clic en uno, la ficha de la derecha muestra nombre, descripción, estrellas, peces, oleadas y aves, con los botones **JUGAR** y **VER CÓMIC** (si tiene cómic antes).
4. **Cómic de antes** (`Comic Before`): solo la primera vez que juegas ese nivel. Después se puede ver con "VER CÓMIC".
5. **El lago** (SampleScene) con los datos del nivel elegido.
6. **Resultado**:
   - Victoria: estrellas animadas, peces a salvo, aves abatidas y peces rescatados. Botones **SIGUIENTE NIVEL**, **REPETIR** y **MAPA**. Si el nivel tiene `Workshop After`, avisa que ahí irá el Taller (todavía no existe).
   - Derrota (o traje destruido): **REINTENTAR** y **MAPA DE NIVELES**.
7. **MAPA** vuelve al menú, directamente a la pantalla del mapa.

**Pausa (Esc)**: CONTINUAR, REINICIAR NIVEL, MAPA DE NIVELES y MENÚ PRINCIPAL.

**Visor de cómics** (`ComicViewer`): clic, Espacio, Enter o flecha derecha pasan a la siguiente viñeta; Esc o **SALTAR** lo cierran. Una viñeta sin dibujo sale en blanco con el texto "VIÑETA 2 DE 6" para saber dónde va cada dibujo.

**Progreso guardado** (`SaveSystem`): se guarda solo en el archivo `progreso.json`, en `Application.persistentDataPath` (en Windows: `C:\Users\<tú>\AppData\LocalLow\<Company>\<Product>`). Guarda:
- `unlockedLevels`: cuántos niveles están desbloqueados.
- `stars`: la mejor cantidad de estrellas de cada nivel (nunca baja si repites y sacas menos).
- `comicsSeen`: los cómics que ya viste (para no repetirlos solos).

**Atajos para probar:**
- **F9** en el mapa: desbloquea todos los niveles.
- **BORRAR PROGRESO** (abajo a la izquierda del mapa): pulsa dos veces para confirmar. Deja todo como nuevo.
- Para probar un nivel sin pasar por el menú: ponlo en **Test Level** del GameDirector y dale Play en SampleScene (no guarda progreso si no está en el catálogo).

---

## 5. Inspector: GameDirector

El GameDirector ya **no tiene listas de peces, aves ni oleadas**: todo eso vive en los archivos de datos (más abajo). En el Inspector solo queda lo que es igual para todos los niveles.

### Referencias
| Campo | Defecto | En escena | Qué hace |
|---|---|---|---|
| **Player** | — | PlayerBase | El jugador. Si está vacío, lo busca solo |
| **Lake** | — | LakeVolume (del mismo objeto) | Describe el lago para las IA. Si está vacío, lo busca o lo agrega |

### Nivel de prueba
| Campo | Defecto | En escena | Qué hace |
|---|---|---|---|
| **Test Level** | vacío | Nivel_01 | Nivel que se juega al dar Play **directo en SampleScene**. Desde el menú se juega el que elijas en el mapa |

### Llegada de las aves
| Campo | Defecto | En escena | Qué hace |
|---|---|---|---|
| **Arrival Distance** | 120 | 120 | A cuántos metros del centro del lago aparecen |
| **Arrival Height** | 45 | 45 | Altura a la que aparecen |
| **Arrival Yaw** | -1 | -1 | Dirección de llegada en grados (0 = desde +Z). -1 = aleatoria |

### Cinemática de llegada
| Campo | Defecto | En escena | Qué hace |
|---|---|---|---|
| **Play Arrival Cinematic** | ✔ | ✔ | Muestra el plano de la bandada llegando |
| **Cinematic Only First Wave** | ✘ | ✔ | Solo en la primera oleada |
| **Cinematic Duration** | 5 | 5 | Segundos del plano (sin contar la entrada de 0.9 s y la vuelta de 0.7 s) |

### Munición
| Campo | Defecto | En escena | Qué hace |
|---|---|---|---|
| **Ammo Pickups** | 6 | 6 | Cápsulas en el lago al mismo tiempo |
| **Ammo Per Pickup** | 10 | 10 | Balas que da cada cápsula |
| **Pickup Respawn Time** | 12 | 12 | Segundos hasta que aparece otra al recoger una |
| **Underwater Pickup Chance** | 0.4 | 0.4 | Probabilidad de que aparezca bajo el agua (si no, flota) |

### Efectos
| Campo | En escena | Qué hace |
|---|---|---|
| **Fx Material Template** | material de partículas | Base para las partículas de las cápsulas |
| **Bird Hit Prefab** | prefab | Efecto al dispararle a un pájaro |
| **Bird Death Prefab** | prefab | Efecto al morir un pájaro |
| **Pickup Collect Prefab** | prefab | Efecto al recoger una cápsula |

### Archivos de datos: Niveles, Aves, Peces y Cómics
Se crean con **clic derecho en una carpeta > Create > Fish Out Of Water > Nivel / Ave / Pez / Cómic** y se editan en el Inspector como cualquier archivo.

| Archivo | Carpeta | Qué contiene |
|---|---|---|
| **Nivel** (`LevelDefinition`) | `Data/Niveles` | Peces, oleadas, estrellas, marcas del mapa y cómics de un nivel |
| **Ave** (`BirdDefinition`) | `Data/Aves` | Un campo **Bird** con el modelo y los valores del ave |
| **Pez** (`FishDefinition`) | `Data/Peces` | Un campo **Fish** con el modelo y los valores del pez |
| **Cómic** (`ComicDefinition`) | `Data/Comics` | Título y lista de viñetas (dibujo + texto) |
| **Catálogo de niveles** (`LevelCatalog`) | `Resources/LevelCatalog` | La lista **ordenada** de niveles del modo Historia. El mapa muestra estos, en este orden |

**Campos de un Nivel**

| Campo | Defecto | Qué hace |
|---|---|---|
| **Number** / **Display Name** | 1 / "Nivel 1" | Número y nombre (sale en el mapa, al empezar y en el resultado) |
| **Description** | — | Texto corto de la ficha del mapa |
| **Fish** | vacío | Lista de peces: **Type** (un archivo Pez) y **Count** (cuántos) |
| **Schools** | 3 | Número de cardúmenes |
| **Max Fish Loss Fraction** | 0.7 | Pierdes si cazan este porcentaje de peces |
| **Waves** | vacío | Oleadas, en orden. Cada una: **Delay Before** (segundos de calma antes), **Birds** (lista de **Type** = un archivo Ave + **Count**) y **Health Multiplier** |
| **Is Boss Level** | ✘ | Marca el nivel con "JEFE" en el mapa |
| **Workshop After** | ✘ | Marca "TALLER" en el mapa y avisa al ganar (el Taller todavía no existe) |
| **Comic Before** | vacío | Cómic que se ve antes de jugarlo la primera vez (se puede repetir con "VER CÓMIC") |
| **Comic After** | vacío | Cómic que se ve al ganarlo la primera vez |
| **Two Stars** / **Three Stars** | 0.6 / 0.9 | Porcentaje de peces salvados para 2 y 3 estrellas (ganar = al menos 1) |

**Los 20 niveles** siguen la progresión del GDD:

| Bloque | Niveles | Peces | Pierdes con | Aves por oleada | Jefe | Taller después de |
|---|---|---|---|---|---|---|
| 1 · La llegada | 1–5 | 16 | 70 % | de 2/3/4 a 3/4/6 | Nivel 5 | 3 |
| 2 · La orilla | 6–10 | 20 | 65 % | de 4/5/6 a 5/6/8 | Nivel 10 | 6 y 9 |
| 3 · Bajo el agua | 11–15 | 24 | 60 % | de 5/7/8 a 6/8/10 | Nivel 15 | 12 y 15 |
| 4 · Los cielos | 16–20 | 28 | 55 % | de 7/9/10 a 8/10/12 | Nivel 20 | 18 |

- La vida de las aves sube 5 % por nivel, y la tercera oleada lleva 15 % más.
- Por ahora todas las aves son **Ave_Arcilla**. En los niveles de jefe, la tercera oleada trae un **Jefe_Provisional**: el ave de arcilla al doble de tamaño, con 220 de vida y 25 de daño. Cuando tengas los modelos de cormorán, garza, etc., crea un archivo Ave por cada uno y cámbialos en las oleadas.
- Cómics (en blanco): origen antes del nivel 1; antes y después de los jefes 5, 10 y 15; antes del jefe 20 y el final después del nivel 20.

**Para agregar un nivel:** duplica uno (Ctrl+D), cámbiale los datos y súmalo al final de la lista **Levels** de `Resources/LevelCatalog`.
**Para un ave nueva:** duplica `Ave_Arcilla`, cambia el modelo y los valores, y úsala en las oleadas.

**Valores de un Pez** (campo **Fish**; ejemplo: Pez_Naranja)

| Campo | Defecto | En Pez_Naranja | Qué hace |
|---|---|---|---|
| **Name** | "Pez" | Pez naranja | Nombre (para los objetos creados) |
| **Model** | — | PezNormal.fbx | Tu modelo. Sin modelo se usa una cápsula de prueba |
| **Model Rotation** | (90, 0, 0) | (90, 0, 0) | Rotación para que la cabeza mire hacia adelante (+Z). (90,0,0): cara hacia abajo; (-90,180,0): cara hacia arriba |
| **Model Offset** | (0, 0, 0) | (0, 0, -0.18) | Desplaza el modelo para centrarlo en la esfera del pez |
| **Model Scale** | 28 | 28 | Escala del modelo |
| **Material Override** | — | PezNormal | Material que reemplaza a todos los del modelo (así cambias el color por especie) |
| **Animator Controller** | — | PezNado | Animación de nado. Si lo pones, se apaga el coleteo por código y la animación se acelera cuando huye |
| **Procedural Wiggle** | ✔ | ✔ | Coleteo por código (solo si no hay Animator Controller) |
| **Wiggle Amount** | 14 | 14 | Grados del coleteo por código |
| **Collider Radius** | 0.7 | 0.7 | Radio de la esfera del pez (para atraparlo, rescatarlo, etc.) |
| **Speed Multiplier** | 1 | 1 | Multiplica la velocidad de nado y de huida |
| **Stamina Multiplier** | 1 | 1 | Multiplica cuánto aguanta escondido en lo profundo |

**Valores de un Ave** (campo **Bird**; ejemplo: Ave_Arcilla)

| Campo | Defecto | En Ave_Arcilla | Qué hace |
|---|---|---|---|
| **Name** | "Ave" | Ave de arcilla | Nombre |
| **Model** | — | Bird Clay Dedidara.fbx | Tu modelo. Sin modelo se usa una cápsula de prueba |
| **Model Rotation** | (0, 0, 0) | (0, 0, 0) | Rotación para que el pico mire hacia +Z |
| **Model Offset** | (0, 0, 0) | (0, 0, 0) | Desplazamiento del modelo |
| **Model Scale** | 1 | 1 | Escala |
| **Material Override** | — | vacío | Material que reemplaza al del modelo |
| **Animator Controller** | — | vacío | Animación de vuelo. Si lo pones, se apaga el aleteo por código |
| **Procedural Wing Flap** | ✔ | ✔ | Aleteo por código rotando los huesos de las alas |
| **Wing Bones** | LeftArm,RightArm,L_wing,R_wing | igual | Nombres de los huesos de las alas, separados por coma |
| **Talon Bones** | LeftFoot,RightFoot | igual | Huesos de las garras: el pez atrapado cuelga entre ellos |
| **Catch Point Offset** | (0, -1.1, 0) | igual | Dónde cuelga el pez si no encuentra las garras |
| **Collider Radius** | 1.3 | 1.3 | Radio de la esfera del ave (para que la golpeen tus balas) |
| **Collider Center** | (0, 0.3, 0) | igual | Centro de esa esfera |
| **Health** | 40 | 40 | Vida (se multiplica por el `Health Multiplier` de la oleada) |
| **Speed Multiplier** | 1 | 1 | Multiplica todas sus velocidades (llegada, patrulla, persecución, picada, huida con presa) |
| **Damage** | 15 | 15 | Armadura que te quita al embestirte |
| **Detect Range** | 26 | 26 | Distancia a la que te ve y te ataca |
| **Carry Time** | 3.5 | 3.5 | Segundos que tarda en comerse un pez atrapado (el tiempo que tienes para rescatarlo) |

**Campos de un Cómic:** **Title** (sale arriba del visor) y **Panels**, la lista de viñetas. Cada viñeta tiene **Image** (el dibujo; vacío = viñeta en blanco) y **Caption** (texto del recuadro amarillo; vacío = sin recuadro). Para poner tus dibujos: importa la imagen, cambia su *Texture Type* a **Sprite (2D and UI)** y arrástrala a **Image**.

---

## 6. Inspector: agua

### LakeVolume (en GameDirector)
| Campo | Defecto | En escena | Qué hace |
|---|---|---|---|
| **Water** | — | Lago_Agua | El agua del lago. Vacío = busca la primera LakeWater |
| **Edge Margin** | 4 | 4 | Distancia mínima a la orilla para los puntos de nado (peces, cápsulas, cardúmenes) |
| **Min Water Depth** | 1.2 | 1.2 | Profundidad mínima para que una zona cuente como lago |

Le da a las IA: nivel de la superficie, altura del fondo, centro y radio del lago, y puntos válidos para nadar.

### LakeWater (en Lago_Agua)
**Uso:** pon el objeto dentro del hueco del lago. **Su posición Y es el nivel del agua**. Muévelo o usa los botones del Inspector:
- **Reconstruir agua**.
- **Ajustar altura al hueco**: lo sube hasta justo antes de desbordar, menos `Auto Height Margin`.

El Inspector también muestra el nivel, el área, la profundidad máxima, los vértices y avisos si el agua se desborda fuera del hueco. No lo rotes ni lo escales.

| Campo | Defecto | En escena | Qué hace |
|---|---|---|---|
| **Cell Size** | 2 | 2 | Tamaño de cada celda de la malla (m). Más pequeño = orilla más precisa y más polígonos |
| **Max Extent** | 400 | 400 | Hasta qué distancia busca agua desde el objeto (m) |
| **Shore Overlap** | 2 | 2 | Celdas que la malla se mete bajo la orilla (evita huecos en el borde) |
| **Ground Mask** | Everything | Everything | Capas que cuentan como suelo donde no hay Terrain |
| **Auto Height Margin** | 1.5 | 1.5 | Margen bajo el desborde para "Ajustar altura al hueco" |
| **Splash Material Template** | — | material de partículas | Base de las partículas de salpicadura |
| **Splash Color** | celeste | celeste | Color de las salpicaduras |
| **Big Splash Prefab** | — | prefab de salpicadura | Efecto extra en golpes fuertes |
| **Big Splash Threshold** | 0.55 | 0.55 | Qué tan fuerte debe ser el golpe (0–1) para usar el efecto extra |

Todo lo que cruza la superficie con `WaterInteractor` (jugador, peces, pájaros y balas) hace anillos en el shader y salpicaduras.

### Material AguaLago (shader StylizedLakeWater)
| Propiedad | Valor | Qué hace |
|---|---|---|
| Color poco profundo (orilla) | azul claro, alfa 0.55 | Color del agua donde es poco profunda |
| Color profundo | azul marino, alfa 0.93 | Color donde es profunda |
| Profundidad del color profundo | 6 m | A qué profundidad ya se ve el color profundo |
| Color visto desde abajo | celeste, alfa 0.55 | Color de la superficie vista desde dentro del agua |
| Color / Concentración / Intensidad del brillo | celeste / 3 / 0.7 | Brillo de borde estilo traje (fresnel) |
| Color / Ancho / Brillo de la espuma | celeste / 0.9 m / 1.3 | Espuma de la orilla |
| Color / Ancho / Intensidad del borde | blanco / 0.35 m / 1.2 | Borde blanco en la silueta de objetos medio sumergidos |
| Altura / Tamaño / Velocidad de las ondas | 0.035 m / 0.08 / 0.22 | Ondas suaves (agua calmada) |
| Relieve de la superficie | 0.6 | Intensidad del relieve de la superficie |
| Color / Tamaño del reflejo, Destellos | blanco / 0.975 / 0.4 | Reflejo del sol y destellos |
| Color / Velocidad / Duración / Grosor de los anillos | celeste / 3.2 / 2.2 s / 0.35 | Anillos al entrar o salir del agua |

---

## 7. Inspector: PlayerController_Base

En **PlayerBase**.

**Controles**:
- **W/S**: avanzar o retroceder. **A/D**: girar.
- **Espacio**: propulsor (en el agua, subir o salir de un salto).
- **Shift izquierdo**: turbo (mantener). En el agua, al pulsarlo también hace el dash.
- **Ctrl izquierdo**: bucear (en el agua).
- **Q**: cambiar entre primera y tercera persona (en la escena está en Q; por defecto, C).
- **Clic izquierdo**: disparar. **Clic derecho**: disparo cargado. **Esc**: pausa.

### Movimiento
| Campo | Defecto | En escena | Qué hace |
|---|---|---|---|
| **Walk Speed** | 5 | 6 | Velocidad máxima caminando. Con propulsor ×1.5 y en caída × `Air Control` |
| **Turn Speed** | 120 | 120 | Grados por segundo al girar fuera del agua |
| **Acceleration** | 8 | 8 | Fuerza con la que alcanza la velocidad |
| **Deceleration** | 6 | 6 | Frenado al soltar (en el suelo) y del giro |

### Vuelo
| Campo | Defecto | En escena | Qué hace |
|---|---|---|---|
| **Fly Force** | 15 | 15 | Empuje hacia arriba del propulsor |
| **Max Fly Speed** | 8 | 8 | Velocidad máxima de subida |
| **Air Control** | 0.7 | 0.7 | Control al caer sin propulsor (multiplica la velocidad) |

### Referencias
| Campo | En escena | Qué hace |
|---|---|---|
| **Modelo Pez** | el modelo PESCAO | Modelo que se inclina al subir, caer y girar |
| **Camera Holder** | PlayerCamera | La cámara que maneja este script (**obligatorio**) |

### Cámara y efectos
| Campo | Defecto | En escena | Qué hace |
|---|---|---|---|
| **Camera Tilt Amount** | 3 | 3 | Inclinación lateral de la cámara al girar (al nadar ×2.2) |
| **Camera Bob Speed** | 8 | 8 | Velocidad del balanceo al caminar |
| **Camera Bob Amount** | 0.05 | 0.05 | Altura del balanceo al caminar |

### Cámara en tercera persona
| Campo | Defecto | En escena | Qué hace |
|---|---|---|---|
| **Is Third Person** | ✘ | ✘ | Empieza en tercera persona |
| **Toggle Camera Key** | C | Q | Tecla para cambiar de vista |
| **Third Person Offset** | (0, 1.7, -5) | igual | Posición de la cámara en tercera persona (relativa al jugador) |
| **First Person Offset** | (0, 1.7, 0) | igual | Posición de la cámara en primera persona (ojos). También es la altura de los ojos para saber si estás bajo el agua |

### Caída
| Campo | Defecto | En escena | Qué hace |
|---|---|---|---|
| **Fall Multiplier** | 2.5 | 2.5 | Gravedad extra al caer (caída más pesada) |
| **Ground Check Distance** | 0.2 | 0.2 | Largo del rayo hacia abajo para saber si pisas suelo |
| **Ground Layer** | Nothing | Water | Capas que cuentan como suelo. La capa del terreno se agrega sola al empezar (sale un aviso en la consola si faltaba) |

### Efectos de pez
| Campo | Defecto | En escena | Qué hace |
|---|---|---|---|
| **Tilt Speed** | 5 | 5 | Rapidez con la que el modelo se inclina |
| **Horizontal Tilt X** | 90 | 90 | Rotación X que deja al modelo acostado |
| **Max Rise Pitch** | 30 | 30 | Máxima inclinación de la nariz hacia arriba al subir |
| **Max Fall Pitch** | 22 | 22 | Máxima inclinación hacia abajo al caer |
| **Turn Bank** | 18 | 18 | Inclinación lateral al girar |

### Efectos bajo agua
| Campo | Defecto | En escena | Qué hace |
|---|---|---|---|
| **Underwater Volume** | — | Global Volume (1) | Postproceso que se activa con los ojos bajo el agua |
| **Underwater Transition Speed** | 2 | 2 | Rapidez con la que se quita el efecto al sacar la cámara del agua. Al meterla es inmediato |

### Energía del Jetpack
| Campo | Defecto | En escena | Qué hace |
|---|---|---|---|
| **Max Jetpack Energy** | 100 | 100 | Combustible máximo (también lo usa el turbo) |
| **Current Jetpack Energy** | — | 0 | Estado (no editar; empieza lleno) |
| **Jetpack Drain Rate** | 7 | 7 | Combustible por segundo con el propulsor encendido |
| **Jetpack Recharge Rate** | 35 | 35 | Recarga por segundo dentro del agua |
| **Jetpack Ignition Cost** | 3 | 3 | Coste extra al encenderlo (evita spamear Espacio) |
| **Jetpack Ignition Boost** | 3 | 3 | Impulso hacia arriba al encenderlo |
| **Jetpack Fall Brake** | 1.5 | 1.5 | Empuje extra si vienes cayendo (frena la caída) |
| **Jetpack Sputter Threshold** | 0.12 | 0.12 | Por debajo de este % el propulsor "tose" (empuje intermitente) |
| **Is In Water** | — | ✘ | Estado (no editar) |

### Turbo
| Campo | Defecto | En escena | Qué hace |
|---|---|---|---|
| **Turbo Key** | Shift izq. | Shift izq. | Tecla del turbo (mantener) |
| **Turbo Air Speed** | 24 | 24 | Velocidad máxima con turbo fuera del agua |
| **Turbo Thrust** | 40 | 40 | Empuje hacia adelante del turbo |
| **Turbo Drain Rate** | 22 | 22 | Combustible extra por segundo con turbo (se suma al del propulsor) |
| **Turbo Swim Multiplier** | 1.7 | 1.7 | Multiplica la velocidad de nado con turbo |
| **Turbo Swim Drain Rate** | 10 | 10 | Combustible por segundo con turbo bajo el agua (y no recarga mientras lo usas) |
| **Turbo Fov Kick** | 14 | 14 | Grados que se abre el FOV con turbo |

### Física en agua
| Campo | Defecto | En escena | Qué hace |
|---|---|---|---|
| **Water Surface Offset** | 0 | 0 | Ajuste fino de la altura de la superficie para el jugador |

### Nado
| Campo | Defecto | En escena | Qué hace |
|---|---|---|---|
| **Swim Speed** | 11 | 11 | Velocidad de nado |
| **Swim Responsiveness** | 4 | 4 | Qué tan rápido alcanza esa velocidad |
| **Swim Glide** | 1.2 | 1.2 | Qué tan rápido frena al soltar (más bajo = planea más) |
| **Swim Turn Speed** | 170 | 170 | Grados por segundo al girar nadando |
| **Swim Eye Depth** | 0.7 | 0.7 | Profundidad a la que nada (cámara bajo la superficie) |
| **Dive Key** | Ctrl izq. | Ctrl izq. | Tecla para bucear |
| **Dive Depth** | 3 | 3 | Profundidad al bucear |
| **Buoyancy Spring** | 6 | 6 | Fuerza con la que se mantiene a la profundidad de nado |
| **Buoyancy Damping** | 3.5 | 3.5 | Amortiguación (evita que rebote arriba y abajo) |
| **Swim Rise Acceleration** | 22 | 22 | Aceleración hacia arriba con el propulsor bajo el agua |
| **Breach Boost** | 6 | 6 | Impulso extra al salir del agua con el propulsor (salto de delfín) |

### Impulso de nado (dash)
| Campo | Defecto | En escena | Qué hace |
|---|---|---|---|
| **Swim Dash Key** | Shift izq. | Shift izq. | Tecla del dash (al pulsarla en el agua) |
| **Swim Dash Speed** | 22 | 22 | Velocidad del dash |
| **Swim Dash Duration** | 0.35 | 0.35 | Duración (s) |
| **Swim Dash Cooldown** | 1.1 | 1.1 | Espera entre dashes (s) |

### Cámara de nado
| Campo | Defecto | En escena | Qué hace |
|---|---|---|---|
| **Swim Camera Sway** | 1.2 | 1.2 | Vaivén de la cámara al nadar |
| **Swim Fov Boost** | 10 | 10 | Grados que se abre el FOV a máxima velocidad de nado |
| **Dash Fov Kick** | 12 | 12 | Golpe de FOV al hacer el dash |
| **Water Contact Height** | 0.45 | 0.45 | Qué tan abajo del centro del jugador "toca" el agua (m) |

### Armadura
| Campo | Defecto | En escena | Qué hace |
|---|---|---|---|
| **Max Armor** | 100 | 100 | Armadura máxima (vida). En 0 mueres |
| **Current Armor** | — | 0 | Estado (no editar; empieza llena) |
| **Min Damage** / **Max Damage** | 13 / 17 | 13 / 17 | Daño al chocar con objetos con `Enemy Tag` que no son pájaros con IA (los pájaros usan su propio `Damage`) |
| **Enemy Tag** | Enemy | Enemy | Tag de los enemigos |
| **Jetpack Water Effect** | — | partículas | Partículas del propulsor (SuitFX las usa si su propio *Jetpack Effect* está vacío) |

### Referencias UI y efectos de impacto
| Campo | Defecto | En escena | Qué hace |
|---|---|---|---|
| **Ui Status** | — | UiManager | Barras del HUD (destello de daño) |
| **Ui Panel Rect** | — | panel del HUD | Panel que se sacude al recibir daño |
| **Shake Duration** | 0.2 | 0.5 | Duración de la sacudida de cámara al recibir daño (s) |
| **Shake Magnitude** | 0.3 | 20 | Intensidad de esa sacudida |
| **Is Dead** | — | ✘ | Estado (no editar) |

---

## 8. Inspector: PlayerShooting y SuitFX

### PlayerShooting (en PlayerBase)
La munición **no se recarga con el agua** (salvo que actives `Recharge Ammo In Water`): se recarga con las cápsulas del lago. No dispara durante la cinemática ni en pausa.

| Campo | Defecto | En escena | Qué hace |
|---|---|---|---|
| **Shoot Key** | Mouse0 | Mouse0 | Disparo |
| **Projectile Prefab** | — | prefab de bala | La bala (`Projectile`) |
| **Shoot Force** | 20 | 80 | Velocidad de la bala |
| **Fire Rate** | 0.3 | 0.2 | Segundos entre disparos |
| **Hold To Fire** | ✔ | ✔ | Mantener para ráfaga |
| **Fire Point** | — | punto del traje | De dónde sale la bala |
| **Player Camera** | — | PlayerCamera | Para apuntar con el ratón (rayo desde la cámara) |
| **Max Distance** | 500 | 500 | Alcance del rayo de puntería |
| **Aim Layers** | Everything | Everything | Capas contra las que apunta |
| **Base Spread** | 0.25 | 0.25 | Dispersión base (grados) |
| **Spread Per Shot** | 0.6 | 0.6 | Dispersión que suma cada disparo seguido |
| **Max Spread** | 3 | 3 | Dispersión máxima |
| **Spread Recovery** | 5 | 5 | Rapidez con la que vuelve a la precisión |
| **Air Spread** | 1 | 1 | Dispersión extra volando |
| **Charge Key** | Mouse1 | Mouse1 | Disparo cargado (mantener y soltar) |
| **Charge Time** | 0.9 | 0.9 | Segundos para cargarlo |
| **Charged Ammo Cost** | 3 | 3 | Balas que gasta |
| **Charged Damage Multiplier** | 3 | 3 | Multiplica el daño |
| **Charged Scale** | 2.2 | 2.2 | Tamaño de la bala cargada |
| **Charged Speed Multiplier** | 1.25 | 1.25 | Velocidad de la bala cargada |
| **Muzzle Flash / Impact Effect / Charged Impact** | — | prefabs | Efectos de disparo e impacto (las salpicaduras en el agua las hace LakeWater) |
| **Fx Material Template** | — | material de partículas | Base de las partículas del disparo |
| **Shot Color** | celeste | celeste | Color de los efectos del disparo |
| **Recoil Kick** / **Charged Recoil Kick** | 1.2 / 4 | 1.2 / 4 | Retroceso de cámara normal y cargado |
| **Max Ammo** | 30 | 30 | Balas máximas (empieza lleno) |
| **Current Ammo** | — | 0 | Estado (no editar) |
| **Is In Water For Ammo** | — | ✘ | Estado (no editar) |
| **Recharge Ammo In Water** | ✘ | ✘ | Si lo activas, el agua también recarga balas |
| **Ammo Recharge Interval** | 0.2 | 0.2 | Segundos por bala al recargar en el agua |

### SuitFX (en PlayerBase)
Efectos del traje: burbujas al nadar, partículas de velocidad bajo el agua, dash, y luz, chispas, ignición y "tos" del propulsor.

| Campo | Defecto | En escena | Qué hace |
|---|---|---|---|
| **Fx Material Template** | — | material de partículas | Base de todas las partículas del traje |
| **Bubble Color** | celeste | celeste | Color de las burbujas |
| **Bubbles Idle** / **Bubbles At Full Speed** | 3 / 45 | 3 / 45 | Burbujas por segundo quieto y a máxima velocidad |
| **Bubble Offset** | (0, 0, -0.7) | igual | De dónde salen las burbujas (relativo al jugador) |
| **Dash Burst Prefab** | — | prefab | Efecto del dash |
| **Speed Motes** | ✔ | ✔ | Partículas que flotan en el agua y se estiran al ir rápido |
| **Jetpack Effect** | — | partículas | Llama del propulsor |
| **Jet Color** | celeste | celeste | Color del propulsor |
| **Jet Light Intensity** | 4 | 4 | Intensidad de la luz del propulsor |
| **Jet Ignite Prefab** | — | prefab | Efecto al encender el propulsor |

---

## 9. Inspector: interfaz

### UI_PlayerStatus (en UiManager): barras de jetpack, munición y armadura
| Campo | En escena | Qué hace |
|---|---|---|
| **Player Controller** / **Player Shooting** | PlayerBase | De dónde lee energía, armadura y balas |
| **Ui Panel** | panel del HUD | Panel principal |
| **Jetpack Slider / Text / Icon / Back Ground / Slider Image** | elementos UI | Barra de combustible |
| **Ammo Text / Ammo Icon** | elementos UI | Contador de balas |
| **Ammo Empty Color** | rojo | Color al quedarte sin balas |
| **Blink Speed** | 2.5 | Velocidad del parpadeo de aviso |
| **Low Ammo Threshold** | 5 | Parpadea con estas balas o menos |
| **Low Energy Threshold** | 25 | Parpadea con este % de combustible o menos |
| **Armor Slider / Fill Image / Icon / Text** | elementos UI | Barra de armadura |
| **Crack Overlay** | imagen | Grietas en pantalla con poca armadura |
| **Red Filter** | imagen | Filtro rojo al recibir daño o morir |
| **Armor Low Color** | rojo | Color con poca armadura |
| **Armor Low Threshold** | 25 | Armadura baja por debajo de este valor |
| **Red Filter Intensity** | 0.3 | Intensidad del filtro rojo |
| **Ui Panel Rect** | panel del HUD | Panel que se sacude |
| **Ui Shake Duration** / **Ui Shake Magnitude** | 0.5 / 18.5 | Sacudida del panel al recibir daño |

### HelmetVisorHUD (en VistaInterior): vista desde el casco
Construye el marco, el cristal, la retícula y los textos por código. **Durante la cinemática se apaga entero.** El marco solo se ve en primera persona.

| Campo | Defecto | En escena | Qué hace |
|---|---|---|---|
| **Player** / **Shooting** | — | PlayerBase | De dónde lee el estado |
| **Hud Panel** | — | panel del HUD | Panel que se mueve con la inercia del casco |
| **Accent / Frame Color / Danger Color / Water Tint** | cian / azul oscuro / rojo / verde agua | igual | Colores del casco |
| **Frame Opacity** | 0.92 | 0.92 | Opacidad del marco |
| **Rim Glow** | 0.65 | 0.65 | Brillo del borde del visor |
| **Visor Size** | 0.97 | 0.97 | Qué tanto invade el marco (más alto = casco más cerrado) |
| **Scanline Opacity** | 0.05 | 0.05 | Líneas de escaneo del cristal |
| **Reflection Opacity** | 0.07 | 0.07 | Reflejo del cristal |
| **Underwater Tint Opacity** | 0.45 | 0.45 | Tinte del cristal bajo el agua |
| **Droplets On Exit** | 14 | 14 | Gotas en el cristal al salir del agua |
| **Sway From Turn / Sway From Vertical / Max Sway / Sway Smoothing** | 0.12 / 1.8 / 28 / 6 | igual | Inercia del HUD al girar o subir |
| **Breathing Amount** | 2 | 2 | "Respiración" del HUD |
| **Show Reticle** | ✔ | ✔ | Muestra la retícula |
| **Reticle Follows Mouse** | ✔ | ✔ | La retícula sigue al ratón |
| **Hide System Cursor** | ✔ | ✔ | Oculta el cursor del sistema |
| **Reticle Size** | 34 | 34 | Tamaño de la retícula |
| **Show Status Text** / **Show Readout** | ✔ / ✔ | ✔ / ✔ | Textos de estado y lectura del traje |
| **Boot Duration** | 1.4 | 1.4 | Duración de la animación de encendido del casco |

### PauseMenu (en UiManager)
Construye por código la pausa, la pantalla de "TRAJE DESTRUIDO" y la de resultado (ver [Menú, mapa de niveles, cómics y progreso](#menú-mapa-de-niveles-cómics-y-progreso)). Los nombres de las escenas están en `GameSession` (`MainMenu` y `SampleScene`).

| Campo | Defecto | En escena | Qué hace |
|---|---|---|---|
| **Player** | — | PlayerBase | Para detectar la muerte |
| **Pause Key** | Escape | Escape | Tecla de pausa |
| **Death Screen Delay** | 1.5 | 1.5 | Segundos antes de mostrar "TRAJE DESTRUIDO" |

### GameHUD (se agrega solo al GameDirector al darle Play)
Muestra los objetivos (oleada, depredadores, peces a salvo), los mensajes, las flechas a pájaros y cápsulas, y las barras de cine. Solo se edita en Play o agregándolo a mano al GameDirector:
| Campo | Defecto | Qué hace |
|---|---|---|
| **Pickup Marker Range** | 70 | Distancia a la que marca las cápsulas en pantalla |
| **Low Ammo For Arrow** | 8 | Con estas balas o menos, muestra la flecha a la cápsula más cercana |

### MainMenu (escena MainMenu)
Construye todo por código: el fondo del lago (degradado, rayos de luz, algas, rocas y burbujas), el pez flotando y las pantallas Título, Modos, Mapa y Controles. Los niveles los lee de `Resources/LevelCatalog`.

| Campo | Defecto | En escena | Qué hace |
|---|---|---|---|
| **Hero Model** | — | PESCAO.fbx | Modelo que flota junto al título (vacío = sin modelo) |
| **Hero Animator** | — | Fish.controller | Animación del modelo |
| **Hero Height** | 3.2 | 3.2 | Tamaño del modelo en pantalla |
| **Title Line 1** / **Title Line 2** | FISH OUT / OF WATER | igual | Las dos líneas del título |
| **Subtitle** | UN PEZ. UN TRAJE. NADA DE AGUA. | igual | Texto bajo el título |

Estilo: fuentes **Bangers** (títulos) y **Lilita One** (textos), en `Resources/Fonts` (licencia OFL en `LICENCIAS.txt`); paneles de cómic con contorno grueso y sombra, y colores del lago (naranja del pez, azules del agua, amarillo de las viñetas). Todo eso está en `MenuUI`, que también usan la pausa, el resultado y el visor de cómics.

### ComicViewer (se crea solo)
No tiene campos: lo abren el mapa (cómic de antes) y el resultado (cómic de después). Los cómics se editan en `Data/Comics` (ver [Campos de un Cómic](#archivos-de-datos-niveles-aves-peces-y-cómics)).

---

## 10. Componentes creados por código

Estos componentes aparecen al darle Play. **Lo que cambies en su Inspector durante el juego se pierde.** Para ajustarlos de forma permanente:
- Cambia el archivo **Pez** o **Ave** en `Assets/Data` (lo marcado como "por tipo").
- O cambia el valor por defecto en el script.

### FishAI (cada pez)
| Campo | Defecto | Qué hace |
|---|---|---|
| Wander Speed / Flee Speed | 3.2 / 9.5 | Velocidad nadando tranquilo y huyendo (**× Speed Multiplier del tipo**) |
| Acceleration / Turn Speed | 6 / 5 | Respuesta al cambiar de velocidad y de dirección |
| Cruise Depth | (0.5, 1.6) | Profundidad normal bajo la superficie |
| Bottom Clearance | 0.6 | Distancia mínima al fondo |
| Threat Radius | 22 | A qué distancia detecta a un pájaro (×1.5 si lo está cazando) |
| Panic Depth | (3.5, 6) | Profundidad a la que intenta escapar |
| Panic Stamina | 6 | Segundos que aguanta escondido (**× Stamina Multiplier del tipo**) |
| Exhausted Time / Stamina Recovery | 4 / 0.8 | Tiempo agotado y velocidad de recuperación |
| Alarm Radius | 14 | Al ser atrapado, asusta a los peces en este radio |
| School Id / School Radius / Separation Radius | auto / 14 / 2.2 | Cardumen asignado, tamaño del cardumen y distancia entre peces |
| Suffocate Time | 6 | Segundos fuera del agua antes de morir |
| Visual / Procedural Wiggle / Wiggle Amount | auto / **por tipo** | Modelo y coleteo |

### BirdAI (cada pájaro)
| Campo | Defecto | Qué hace |
|---|---|---|
| Arrival / Patrol / Chase / Dive / Carry Speed | 22 / 10 / 15 / 24 / 8 | Velocidades (**× Speed Multiplier del tipo**) |
| Acceleration / Turn Speed | 18 / 4 | Respuesta del vuelo |
| Patrol Altitude | (12, 20) | Altura de patrulla sobre el agua |
| Stalk Altitude | 9 | Altura al acechar un pez |
| Min Altitude Above Ground | 3 | Altura mínima sobre el terreno |
| Hunt Range | 75 | Distancia máxima a la que elige presa |
| Stalk Time | (1.5, 3) | Segundos acechando antes de lanzarse |
| Max Dive Distance | 22 | Distancia máxima para lanzarse en picada |
| Catch Radius / Catch Depth | 2.4 / 2 | Alcance y profundidad máxima para atrapar |
| Carry Time | **por tipo** (3.5) | Segundos hasta comerse el pez (tiempo para rescatarlo) |
| Dive Cooldown | (2.5, 4.5) | Espera entre picadas |
| Detect Range | **por tipo** (26) | Distancia a la que te ve |
| Lose Range | 80 | Si te alejas más, pierde el interés |
| Aggro Memory | 10 | Segundos que te persigue después de que le dispares |
| Attack Reach | 2.6 | Distancia para embestirte |
| Damage | **por tipo** (15) | Armadura que te quita |
| Knockback Force / Knockback Up | 16 / 6 | Empujón horizontal y vertical |
| Stun Duration | 2.5 | Segundos quieto tras embestirte |
| Player Hidden Depth | 1.5 | Más hondo que esto bajo el agua, no te ve |
| Visual / Procedural Flap / Wing y Talon Bone Names / Catch Point Offset | **por tipo** | Modelo, aleteo y garras |
| Flap Speed / Flap Angle | 7 / 28 | Aleteo por código |
| Hit / Death Prefab | del GameDirector | Efectos |

### Otros
- **Damageable** (cada pájaro): `Max Health` = Health del tipo × multiplicador de la oleada. `Destroy On Death` = ✘ (la muerte la maneja BirdAI).
- **AmmoPickup** (cada cápsula):
  - `Ammo` = Ammo Per Pickup.
  - `Bob Amplitude` 0.25, `Bob Speed` 1.6, `Spin Speed` 90: flotación y giro.
  - `Pickup Radius` 1.8: distancia para recogerla.
  - `Collect Prefab` = Pickup Collect Prefab.
- **WaterInteractor**: `Size`, `Min Speed`, `Wake`, `Wake Interval` y `Wake Min Speed` controlan el tamaño de la salpicadura, la velocidad mínima para salpicar y las ondas al moverse rozando el agua. El código lo pone con:
  - Jugador: size 1.3, ondas desde 4 m/s.
  - Peces: size 0.8, salpica desde 1 m/s.
  - Pájaros: size 1.5, ondas desde 4 m/s.
- **Projectile** (en el prefab de la bala):
  - `Damage` 10 (lo ajusta el disparo).
  - `Life Time` 3 s.
  - `Impact Effect Prefab`.
  - `Water Slowdown` 0.55: freno al entrar al agua.

---

## 11. Cosas a revisar en la escena

1. **PlayerController_Base > Ground Layer**: en la escena está en "Water", pero ya no importa: al empezar, el código agrega solo la capa del terreno y avisa en la consola. Si quieres quitar el aviso, ponlo en **Default**.
2. **"Main Camera " duplicada**: quedó desactivada. Bórrala. Solo debe haber una cámara activa (`PlayerCamera`) y un AudioListener.
3. **Build Settings**: tienen que estar `MainMenu` (primera) y `SampleScene`. Si falta una, los botones JUGAR, MAPA o MENÚ PRINCIPAL no podrán cargarla.
4. Se borraron los campos que ningún script usaba (inclinación vertical, física vieja del agua y salpicaduras del jugador, colores "normales" de la UI, salpicadura de pájaros y balas) y los scripts viejos `EnemyBird` y `EnemyCollision`. Si la consola avisa de un *missing script* en algún objeto o prefab, quítale ese componente vacío.
5. `Assets/Niveles/Nivel_1` (tu archivo anterior) no se usa: los niveles del juego son los de `Assets/Data/Niveles`. Puedes borrarlo cuando quieras.
6. Si al darle Play **no aparecen peces ni pájaros**, mira la consola. Lo más común es "GameDirector: no hay agua...": revisa que Lago_Agua esté dentro del hueco y pulsa *Reconstruir agua*.
7. Si un modelo de pez sale diminuto o de lado: revisa que `Fish_Swim.fbx` tenga **Preserve Hierarchy** activado (pestaña Model).
