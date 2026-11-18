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
- `Assets/Data/Aves`: las 8 aves (Gaviota, Martín pescador, Charrán, Garza, Cormorán, Serreta, Pelícano, Águila pescadora) y los 4 jefes (Garza Gris, Pelícano Saco, Cormorán Rey, Reina Águila).
- `Assets/Data/Mejoras` y `Assets/Resources/TallerPlan`: las mejoras del traje y qué ofrece el Taller en cada visita.
- `Assets/Resources/Decoracion/Decoracion_Lago`: qué decoración se reparte alrededor y dentro del lago (bosque, orilla, plantas acuáticas, algas, piedras, chatarra). Ver [Vegetación y ambiente del lago](#vegetación-y-ambiente-del-lago).
- `Assets/Resources/Ambiente/Cielo_Lago` y `Assets/Ambiente/CieloEstilizado.shader`: el cielo del lago.
- `Assets/Resources/Sonidos`: los sonidos (ver [Sonido](#sonido-assetsscritpsaudio)).
- `Assets/Data/Peces`: Pez_Naranja, Pez_Dorado y Pez_Rosa.
- `Assets/Data/Comics`: los cómics de la historia (viñetas en blanco por ahora).
- `Assets/Resources/LevelCatalog`: la lista ordenada de niveles que lee el mapa.
- `Assets/Resources/Fonts`: fuentes de la interfaz (Oxanium, Rajdhani y Lilita One, licencia OFL).

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
3. Aturdido → rebota hacia atrás y se queda quieto `Stun Duration` s (0.7) después de embestirte.
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
   - **ARCHIVO DE AVES** (arriba a la derecha): abre el Archivo. Un aviso amarillo muestra cuántas especies nuevas no has abierto.
   - **Archivo de aves** (`MainMenu.Archive.cs` + `BirdArchive.cs`): una ficha por cada especie que aparece en el catálogo de niveles, en el orden en que te las encuentras.
     - Una especie se **registra sola la primera vez que aparece en combate** (aviso "NUEVA AVE EN EL ARCHIVO" en el juego).
     - Las no vistas salen como **???** con el nivel donde aparecen y el boceto apagado.
     - Cada ficha tiene:
       - El **boceto holográfico** del ave, que se dibuja con una línea de escaneo.
       - Barras de **vida, velocidad, daño y vista**, sacadas del archivo Ave.
       - El **estilo de combate** y etiquetas (BLINDADA, A DISTANCIA, BUCEA, EN GRUPO…).
       - Cómo caza, cómo pelea y un consejo.
       - En los jefes, sus **ataques fuertes**.
       - Cuántas has derribado.
     - Flechas arriba/abajo para recorrer la lista; Esc vuelve al mapa.
     - Los textos de cada especie están en `Assets/Scritps/Data/BirdArchive.cs` (según la habilidad o el tipo de jefe del Ave).
4. **Cómic de antes** (`Comic Before`): solo la primera vez que juegas ese nivel, ya sea desde el mapa o con SIGUIENTE NIVEL. Después se puede ver con "VER CÓMIC".
5. **El lago** (SampleScene) con los datos del nivel elegido.
6. **Resultado**:
   - Victoria: estrellas animadas, peces a salvo, aves abatidas y peces rescatados. Botones **SIGUIENTE NIVEL**, **REPETIR** y **MAPA**. Si el nivel tiene `Workshop After`, avisa que ahí irá el Taller (todavía no existe).
   - Derrota (o traje destruido): **REINTENTAR** y **MAPA DE NIVELES**.
7. **MAPA** vuelve al menú, directamente a la pantalla del mapa.

**Pausa (Esc)**: CONTINUAR, REINICIAR NIVEL, MAPA DE NIVELES y MENÚ PRINCIPAL.

**Botones con teclado**: Enter o clic. **Espacio no pulsa botones** (se quitó del eje *Submit* en Project Settings > Input Manager, porque es el propulsor). Los botones del resultado y de "TRAJE DESTRUIDO" esperan medio segundo antes de responder, para no pulsarlos sin querer mientras juegas.

**Visor de cómics** (`ComicViewer`): clic, Espacio, Enter o flecha derecha pasan a la siguiente viñeta; Esc o **SALTAR** lo cierran. Una viñeta sin dibujo sale en blanco con el texto "VIÑETA 2 DE 6" para saber dónde va cada dibujo.

**Progreso guardado** (`SaveSystem`): se guarda solo en el archivo `progreso.json`, en `Application.persistentDataPath` (en Windows: `C:\Users\<tú>\AppData\LocalLow\<Company>\<Product>`). Guarda:
- `unlockedLevels`: cuántos niveles están desbloqueados.
- `stars`: la mejor cantidad de estrellas de cada nivel (nunca baja si repites y sacas menos).
- `comicsSeen`: los cómics que ya viste (para no repetirlos solos).
- `upgrades` y `workshopsUsed`: mejoras del traje instaladas y visitas al Taller ya usadas.
- `birdsSeen`: especies de ave que ya viste en combate (por su nombre, el campo `Name` del Ave).
- `birdsViewed`: especies cuya ficha del Archivo ya abriste (las demás salen como NUEVO).
- `birdKills`: aves derribadas por especie, como `"Gaviota:12"`. Se escribe al terminar cada partida.

**Atajos para probar:**
- **F9** en el mapa: desbloquea todos los niveles.
- **F9** en el Archivo de aves: registra todas las especies (para ver todas las fichas).
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
| **Play Arrival Cinematic** | ✔ | ✔ | Muestra la cinemática de la bandada llegando |
| **Cinematic Only First Wave** | ✘ | ✔ | Solo en la primera oleada |
| **Cinematic Duration** | 6.5 | 6.5 | Segundos de la cinemática, repartidos entre los tres planos (sin contar la entrada de 0.9 s y la vuelta de 0.7 s) |
| **Cinematic Shots** | — | — | Encuadre de cada plano (abajo) |

La cinemática (`WaveCinematic`) son **tres planos seguidos**, con cortes como en una película:
1. **Plano general**: a ras del agua, de lado y algo por detrás del jugador, girando despacio; el jugador abajo y la bandada arriba.
2. **Bandada**: la cámara va por delante de las aves, a un lado, y las ve venir de frente (se acercan durante el plano, con
   un poco de inclinación). Abajo a la izquierda sale el rótulo **AVES DETECTADAS · 4 × GAVIOTA · 2 × CHARRÁN** (o JEFE DETECTADO).
3. **Sobre el hombro**: detrás del jugador mirando a la bandada, acercándose; de ahí vuelve a la vista normal.

Tiene un temblor suave de cámara en mano. Con ESPACIO, ENTER o clic se salta (el aviso sale en la barra negra de abajo).
Si una oleada no tiene aves o dura menos de 2.5 s, no hay plano de bandada.

**Cinematic Shots** (todo se ajusta en el Inspector):

| Campo | Defecto | Qué hace |
|---|---|---|
| **Shot Weights** | (0.34, 0.3, 0.36) | Cuánto dura cada plano: x = general, y = bandada, z = sobre el hombro |
| **Wide Distance / Wide Height** | 13 / 1.3 | Distancia al jugador y altura sobre el agua del plano general |
| **Wide Angle / Wide Orbit** | 38 / 16 | Ángulo alrededor del jugador al empezar (0 = justo detrás) y cuánto gira durante el plano |
| **Wide Fov** | 54 | Zoom del plano general (se cierra 4° durante el plano) |
| **Flock Ahead / Flock Side / Flock Up** | 18 / 8 / 2 | Dónde va la cámara respecto a la bandada (por delante, a un lado, arriba) |
| **Flock Fov / Flock Roll** | 48 / 6 | Zoom e inclinación del plano de la bandada |
| **Shoulder Back / Side / Up** | 5.5 / 2.4 / 3.2 | Posición del plano sobre el hombro |
| **Shoulder Push** | 2.5 | Metros que se acerca durante ese plano |
| **Handheld** | 0.35 | Temblor de cámara en mano (grados) |
| **Show Caption** | ✔ | Muestra el rótulo de las aves |

**Verlo sin darle Play**: con el **GameDirector seleccionado**, la vista de escena dibuja las tres cámaras (celeste = general,
amarillo = bandada, rosa = sobre el hombro). La línea va del inicio al final de cada plano y la pirámide es el encuadre a
mitad del plano. La bandada naranja es un ejemplo: viene desde **Arrival Yaw** (si es aleatoria, desde el norte) a medio
camino del lago. En el menú **⋮** del componente GameDirector:
- **Cinemática: ver plano 1 / 2 / 3**: pone la vista de escena justo en ese encuadre y con su zoom.
- **Cinemática: devolver la vista de escena normal**: deja el zoom de la vista de escena en 60°.

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
- Cada bloque suma especies nuevas (gaviota y martín pescador al principio; garza, cormorán y serreta en la orilla y bajo el agua; pelícano y águila pescadora en los cielos). En los niveles de jefe, la tercera oleada trae al jefe del bloque (ver [Jefes](#jefes-birdaibosscs)).
- Cómics (en blanco): origen antes del nivel 1; antes y después de los jefes 5, 10 y 15; antes del jefe 20 y el final después del nivel 20.

**Para agregar un nivel:** duplica uno (Ctrl+D), cámbiale los datos y súmalo al final de la lista **Levels** de `Resources/LevelCatalog`.
**Para un ave nueva:** duplica una de `Data/Aves` (por ejemplo `Ave_Gaviota`), cambia el modelo y los valores, y úsala en las oleadas. Su **Ability** decide cómo pesca y cómo pelea.

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

**Valores de un Ave** (campo **Bird**; ejemplo: Ave_Gaviota)

| Campo | Defecto | En Ave_Gaviota | Qué hace |
|---|---|---|---|
| **Name** | "Ave" | Gaviota | Nombre (en los jefes, el de la barra de vida) |
| **Model** | — | Ave_Gaviota.fbx | Tu modelo. Sin modelo se usa una cápsula de prueba |
| **Model Rotation / Offset / Scale** | (0,0,0) / (0,0,0) / 1 | (0,0,0) / (0,0,0) / 1.35 | Para que el pico mire hacia +Z, centrarlo y su tamaño (aves comunes: 1.35; jefes: 3 a 3.4). Si cambias la escala, cambia también Collider Radius / Center en la misma proporción |
| **Material Override** | — | Ave_Gaviota | Material que reemplaza al del modelo |
| **Animator Controller** | — | vacío | Animación de vuelo. Si lo pones, se apaga el aleteo por código |
| **Procedural Wing Flap** | ✔ | ✔ | Aleteo por código rotando las alas |
| **Wing Bones / Talon Bones** | … | L_wing,R_wing / LeftFoot,RightFoot | Alas que aletean y punto donde cuelga el pez atrapado |
| **Collider Radius / Center** | 1.3 / (0,0.3,0) | 1.289 / (0,0,0.203) | Esfera del ave (para que la golpeen tus balas) |
| **Health** | 40 | 45 | Vida (se multiplica por el `Health Multiplier` de la oleada; los jefes no) |
| **Speed Multiplier** | 1 | 1.1 | Multiplica todas sus velocidades |
| **Damage** | 15 | 10 | Armadura que te quita al golpearte (sus proyectiles hacen una parte) |
| **Detect Range** | 26 | 26 | Distancia a la que te ve y te ataca (× 1.25) |
| **Carry Time** | 3.5 | 4 | Segundos que tarda en comerse un pez atrapado (el tiempo que tienes para rescatarlo) |
| **Attack Reach** | 2.6 | 2.99 | Distancia a la que te golpea |
| **Ability** | None | SurfaceSnatch | Cómo pesca **y cómo pelea** (ver [Cómo pelea cada ave](#cómo-pelea-cada-ave-birdaicombatcs)) |
| **Catch Radius / Catch Depth / Stalk Altitude** | 2.4 / 2 / 9 | 2.4 / 1.2 / 6 | Alcance para atrapar, profundidad máxima y altura al acechar |
| **Scoop Count / Underwater Time** | 3 / 4 | 1 / 4 | Pelícano: peces por bocado. Buceadores: segundos bajo el agua |
| **Boss** | None | None | Si es un jefe (barra de vida, fases y ataques propios) |
| **Vulnerable Time / Vulnerable Damage Multiplier** | 2.5 / 2 | — | Jefes: pausa después de cada ataque fuerte, y cuánto daño extra recibe en ella |
| **Reinforcements / Reinforcement Count** | — / 3 | — | Reina Águila: aves que llama en cada cambio de fase |

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
| **Air Grip** | 3 | 3 | Qué tan rápido el rumbo en el aire sigue hacia dónde mira el pez: al girar con turbo traza la curva en vez de derrapar de lado |
| **High Speed Turn Factor** | 0.7 | 0.7 | Giro en el aire a velocidad máxima comparado con el normal (curvas más abiertas y estables) |
| **Air Turn Response** | 6 | 6 | Suavizado del giro en el aire (más bajo = más suave) |

El turbo en el aire empuja menos al acercarse a su velocidad máxima: llega a tope sin pasarse ni dar tirones.

**No atraviesa el terreno**: el Rigidbody usa detección continua. Además hay una red de seguridad: si entre un paso
de física y el siguiente el pez cruzó el suelo, un árbol o una roca, vuelve al punto del choque (por fuera) y pierde la
velocidad que lo metía adentro. Si de todos modos quedara debajo del terreno (fuera del lago), sube a la superficie.

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

### Choques, fuera del agua y límite de la zona (`PlayerController_Base.Survival.cs`)
- **Choques**: chocar fuerte contra el suelo, un árbol o una roca quita armadura (más cuanto más fuerte) y rebota.
  Aviso en pantalla "¡CHOQUE! TRAJE −X%". Bajo el agua el golpe se amortiga. Si un ave te acaba de lanzar, la caída cuenta la mitad.
  Ejemplos con los valores de fábrica: caer de 5 m quita ~6 %, de 10 m ~20 %, de 20 m ~43 %; chocar con turbo contra un árbol ~24 %.
- **Varado**: en tierra, fuera del agua y sin carga para encender el propulsor. El casco cuenta los segundos;
  a los 4 s el pez empieza a asfixiarse y pierde armadura a golpes de un segundo, cada vez más rápido (de 3,5 a 9 % por segundo).
  Al volver al agua se acaba. Varado, caminar es muy lento.
- **Arrastrarse**: varado, cada pulsación de **ESPACIO** da un coletazo hacia adelante (hacia donde mira el pez). Hay que pulsar
  varias veces y girar con A/D para llegar al agua.
- **Límite de la zona**: a `Boundary Margin` metros de la orilla. Desde 30 m antes el casco avisa "LÍMITE DE LA ZONA"; al cruzarlo,
  el traje hace solo un giro en U (sube, da la vuelta con la nariz arriba y baja de nuevo hacia el lago) y te devuelve el control.

| Campo | Defecto | Qué hace |
|---|---|---|
| **Safe Impact Speed** / **Severe Impact Speed** / **Max Impact Damage** | 13 / 38 m/s / 0.6 | Desde qué velocidad de choque hace daño, con cuál quita el máximo y cuánto es ese máximo (fracción de la armadura) |
| **Stranded Grace** | 4 s | Segundos varado antes de asfixiarse |
| **Suffocation Rate** / **Rate Max** / **Ramp** | 3.5 / 9 / 8 s | Armadura por segundo al empezar, al final y en cuánto llega al final |
| **Stranded Walk Factor** | 0.35 | Velocidad al caminar varado |
| **Flop Forward** / **Flop Up** / **Flop Cooldown** | 4.5 / 4 / 0.28 s | Coletazo al arrastrarse |
| **Boundary Margin** / **Boundary Warn Band** | 170 m / 30 m | Distancia a la orilla del límite y del aviso |
| **U Turn Duration** / **U Turn Rise** | 1.3 s / 7 | Duración y altura del giro en U |

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

**Disparo recto y asistido:**
- La bala va **en línea recta** (sin gravedad) a 150 m/s, más la velocidad con la que avanzas.
- Se mueve **interpolada** (fluida entre pasos de física) y con detección continua desde el primer paso, así no atraviesa aves.
- La estela mide siempre lo mismo en metros (`Trail Length` de la bala, 14 m), vaya rápida o lenta.
- Apunta a lo que hay bajo la mira. Ignora lo que queda entre la cámara y el pez (en tercera persona eso torcía el disparo).
- **Ayuda de puntería**: si hay un ave a la vista a menos de `Aim Assist Angle` de la mira (más un margen por su tamaño), la
  bala va a donde **va a estar** el ave cuando llegue.
- Con un ave fijada, las marcas de la retícula se cierran en diagonal, se pone blanca y gira más rápido.
- Una bala que llega en menos de 0.1 s (de cerca) no da tiempo a que el ave la esquive.

**Metralleta de burbujas** (mejora del Taller): cada burbuja hace poco daño, así que tiene **su propio tanque de 240
burbujas** que se gasta antes que las balas. El tanque **se rellena solo dentro del agua** (30 burbujas por segundo) y
cada cápsula de munición también lo llena (8 burbujas por cada bala de la cápsula). Si el tanque se vacía, sigue
disparando con las balas del cargador: 8 burbujas por bala. Con la metralleta, el arco de munición del casco muestra el
tanque (marcas cada 40 burbujas).
Campos (no están en la escena, usan estos valores): `Bubble Shots Per Second` 10, `Bubble Damage Factor` 0.4,
`Bubble Capacity` 240, `Bubble Water Refill` 30, `Bubbles Per Pickup Ammo` 8, `Bubbles Per Ammo` 8.

| Campo | Defecto | En escena | Qué hace |
|---|---|---|---|
| **Shoot Key** | Mouse0 | Mouse0 | Disparo |
| **Projectile Prefab** | — | prefab de bala | La bala (`Projectile`) |
| **Shoot Force** | 150 | 150 | Velocidad de la bala en m/s (más alto = más recto y llega antes a donde apuntas) |
| **Fire Rate** | 0.3 | 0.2 | Segundos entre disparos |
| **Hold To Fire** | ✔ | ✔ | Mantener para ráfaga |
| **Fire Point** | — | punto del traje | De dónde sale la bala |
| **Player Camera** | — | PlayerCamera | Para apuntar con el ratón (rayo desde la cámara) |
| **Max Distance** | 500 | 500 | Alcance del rayo de puntería |
| **Aim Layers** | Everything | Everything | Capas contra las que apunta |
| **Base Spread** | 0.1 | 0.1 | Dispersión base (grados) |
| **Spread Per Shot** | 0.25 | 0.25 | Dispersión que suma cada disparo seguido |
| **Max Spread** | 1.2 | 1.2 | Dispersión máxima |
| **Spread Recovery** | 8 | 8 | Rapidez con la que vuelve a la precisión |
| **Air Spread** | 0.2 | 0.2 | Dispersión extra volando |
| **Inherit Forward Speed** | ✔ | ✔ | Suma a la bala la velocidad con la que avanzas: nunca se queda atrás ni parece que cae |
| **Aim Assist Angle** | 4 | 4 | Grados alrededor de la mira en los que el disparo busca un ave (0 = sin ayuda) |
| **Aim Assist Strength** | 0.9 | 0.9 | Cuánto corrige hacia esa ave (1 = justo a donde va a estar) |
| **Lead Targets** | ✔ | ✔ | Apunta adelante del ave según su velocidad, para que la bala llegue a donde VA a estar |
| **Locked Spread Factor** | 0.3 | 0.3 | La dispersión se multiplica por esto mientras la mira tiene un ave fijada |
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
Las barras y el contador viejos se ocultan solos: ahora el propulsor, el traje y la munición se ven en el visor del casco
(`VisorStatusHUD` y `VisorAmmoHUD`). Para volver a los viejos activa **Use Old Bars** / **Use Old Ammo Counter**.
Las grietas y el filtro rojo de daño siguen saliendo de aquí.

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
| **Crack Overlay** | imagen | Grieta vieja (imagen fija). Ya no se usa: ahora el cristal del casco se agrieta solo (HelmetVisorHUD) |
| **Use Old Crack** | ✘ | Vuelve a mostrar la grieta vieja con poca armadura |
| **Red Filter** | imagen | Filtro rojo al recibir daño o morir |
| **Armor Low Color** | rojo | Color con poca armadura |
| **Armor Low Threshold** | 25 | Armadura baja por debajo de este valor |
| **Red Filter Intensity** | 0.3 | Intensidad del filtro rojo |
| **Low Blink Time** | 2.5 | Con poca armadura el filtro rojo parpadea estos segundos y después queda fijo |
| **Low Steady Factor** | 0.35 | Intensidad del rojo fijo (fracción de Red Filter Intensity): avisa sin molestar |
| **Ui Panel Rect** | panel del HUD | Panel que se sacude |
| **Ui Shake Duration** / **Ui Shake Magnitude** | 0.5 / 18.5 | Sacudida del panel al recibir daño |

### HelmetVisorHUD (en VistaInterior): vista desde el casco
Construye el marco, el cristal, la retícula y los textos por código. **Durante la cinemática se apaga entero.**

**Grietas del cristal**: el cristal del casco se raja de a poco. Sale una grieta en forma de telaraña cada vez que el
traje baja del 60%, 40%, 25% y 12%. Cada grieta aparece con un destello, un crujido y una sacudida, y crece desde el
punto del golpe. Se borran si el traje se repara un poco más allá de ese umbral. Bajo el agua se notan menos.
Las grietas se dibujan por código (brazos rectos con quiebres, ramas y anillos), cuatro distintas.

**Poca vida**: el borde del visor, la barra del traje y el filtro rojo de pantalla parpadean unos segundos al bajar del
25% (y otra vez al bajar del 12%). Después quedan en un rojo fijo y suave.

| Campo | Defecto | Qué hace |
|---|---|---|
| **Crack Opacity** / **Crack Color** | 0.9 / blanco azulado | Opacidad y color de las grietas |
| **Low Health Blink Time** | 2.5 | Segundos que parpadea el rojo de poca vida (borde y barra del traje) |
| **Low Health Steady Red** | 0.35 | Qué tan rojo queda el borde después de parpadear |

**Verlo en el editor sin darle Play**: menú **Herramientas > Casco > Vista previa del casco**.
- Abre una ventana y arma el casco como en el juego: marco, cristal, munición, propulsor, traje, misión, retícula y textos.
- Se mira en la ventana **Game** (la abre sola).
- En la ventana eliges los valores de ejemplo: traje, propulsor, munición, en el agua, cámara bajo el agua, tercera persona,
  peces, oleada, depredadores y cuenta atrás.
- Los botones **Encendido / Golpe / Gotas / Mancha** prueban los efectos.
- **Para ajustarlo**: cambia los valores de este HelmetVisorHUD en el Inspector (colores, tamaño del visor, opacidades,
  retícula...). La vista previa se regenera sola al instante.
- Es una **copia** ("Casco (vista previa · no se guarda)") con un jugador de ejemplo invisible. **No se guarda en la escena**:
  se quita sola antes de guardar, al darle Play, al recompilar y al cerrar la escena, y vuelve después.
- Tu canvas y el panel viejo del HUD (`Hud Panel`) no se tocan: se ven y se editan como siempre.
- Si tocas la copia directamente, esos cambios se pierden al regenerar.
- Los misiles, el escudo y el sonar solo salen en Play (dependen de las mejoras).
- **Herramientas > Casco > Quitar vista previa** (o el botón **Quitar**) la borra.
**El casco solo se ve en primera persona**: marco, munición (`VisorAmmoHUD`), propulsor y traje (`VisorStatusHUD`), misión (`VisorMissionHUD`), textos y sonar.
En tercera persona queda solo la retícula (el panel de misión, como todo el casco, es solo de primera persona).
**Panel de misión (`VisorMissionHUD`, borde izquierdo del visor, el espejo de la munición):**
- Arco de celdas: una por pez. Las encendidas son los peces a salvo y se apagan de arriba hacia abajo cuando cazan uno.
- La **marca roja** del arco es el límite: si las celdas encendidas llegan ahí, pierdes el nivel.
- Número grande de peces a salvo con "/ total". Cerca del límite se pone en rojo, parpadea y dice ¡CARDUMEN EN PELIGRO!
- OLEADA 2 / 3 con un rombo por oleada, los depredadores vivos (o ZONA DESPEJADA) y la cuenta atrás de la próxima oleada. Si una gaviota te acierta en primera persona, el cristal se ensucia unos segundos.
**Paleta azul eléctrico con destellos:**
- El borde del visor respira.
- Cada 4.6 s dos destellos recorren el marco: uno por arriba de izquierda a derecha y otro por abajo de derecha a izquierda.
- En las celdas de munición y en las barras de propulsor y traje pasa un brillo casi blanco cada pocos segundos (no pasa si la barra está en rojo).

| Campo | Defecto | En escena | Qué hace |
|---|---|---|---|
| **Player** / **Shooting** | — | PlayerBase | De dónde lee el estado |
| **Hud Panel** | — | panel del HUD | Panel que se mueve con la inercia del casco |
| **Accent / Frame Color / Danger Color / Water Tint** | azul eléctrico (0.25, 0.72, 1) / azul marino casi negro / rojo / verde agua | igual | Colores del casco |
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
Muestra los mensajes, la barra del jefe, las flechas a pájaros y cápsulas, y las barras de cine. Sus objetivos de abajo a la derecha (oleada, depredadores, peces) solo aparecen si no hay casco: con el casco van dentro del visor (`VisorMissionHUD`). Solo se edita en Play o agregándolo a mano al GameDirector:
| Campo | Defecto | Qué hace |
|---|---|---|
| **Pickup Marker Range** | 70 | Distancia a la que marca las cápsulas en pantalla |
| **Low Ammo For Arrow** | 8 | Con estas balas o menos, muestra la flecha a la cápsula más cercana |

### MainMenu (escena MainMenu)
Construye todo por código: el fondo del lago (degradado, rayos de luz, algas, rocas y burbujas), el pez flotando y las pantallas Título, Modos, Mapa, Controles y Archivo de aves. Los niveles los lee de `Resources/LevelCatalog`; los bocetos del Archivo, de `Resources/Archivo`.

| Campo | Defecto | En escena | Qué hace |
|---|---|---|---|
| **Hero Model** | — | PESCAO.fbx | Modelo que flota junto al título (vacío = sin modelo) |
| **Hero Animator** | — | Fish.controller | Animación del modelo |
| **Hero Height** | 3.2 | 3.2 | Tamaño del modelo en pantalla |
| **Title Line 1** / **Title Line 2** | FISH OUT / OF WATER | igual | Las dos líneas del título |
| **Subtitle** | UN PEZ. UN TRAJE. NADA DE AGUA. | igual | Texto bajo el título |

Estilo: el mismo del visor del casco (holograma azul eléctrico con destellos casi blancos y detalles naranja). Fuentes **Oxanium** (títulos) y **Rajdhani** (textos), en `Resources/Fonts` (licencia OFL en `LICENCIAS.txt`). Todo eso está en `MenuUI`, que también usan la pausa, el resultado, el Taller y el visor de cómics.

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

### Peces defensores (`DefenderFish`, desde el nivel 10)
Desde el nivel 10 se suman **2 peces defensores cada 3 niveles**: 2 en los niveles 10-12, 4 en 13-15, 6 en 16-18 y 8 en 19-20.
Son peces de otros colores (verde, violeta y turquesa, no naranja como el protagonista) con un **traje gris y blanco**
(`Traje_Defensor`): casco con visor, mochila con tanques y un cañón de agua en la espalda.
- **No vuelan**: nadan rápido (8,5 m/s; los demás pasean a ~3 m/s) cerca de la superficie, sin hundirse mucho, cada uno
  por su **sector** del lago. Si un ave caza a un pez cerca, van hacia ese pez a defenderlo.
- **Disparan** a las aves que los siguen o que cazan peces cerca (apuntan adelante del ave).
- **Si los atrapan, se sueltan al instante**: la descarga del traje hace que el ave lo suelte, salga despedida y quede
  aturdida un momento (buen momento para dispararle). Cada vez el traje pierde un tercio.
- Al tercer atrapón **el traje se rompe** ("¡UN DEFENSOR PERDIÓ SU TRAJE!") y queda como un pez normal: huye y se lo
  pueden comer como a los demás.
- Cuentan como peces a salvo (están en el total de la misión).

En el GameDirector (**Peces defensores**): `Defender Start Level` (10), `Defender Level Step` (3), `Defenders Per Step`
(2), **`Test Defenders`** (para probarlos en cualquier nivel: pon por ejemplo 2), `Defender Suit Model` (Traje_Defensor) y
`Defender Materials` (PezVerde, PezVioleta, PezTurquesa en `Assets/Models/PezNormal`).
En el componente `DefenderFish` de cada uno (se agrega solo): `Suit Health` 100, `Damage Per Catch` 34,
`Patrol Speed` 8,5, `Patrol Depth` (0,35-0,7), `Escort Range` 20, `Range` 26, `Fire Interval` 0,6, `Damage` 6,
`Shot Speed` 90.

### BirdAI (cada pájaro)
| Campo | Defecto | Qué hace |
|---|---|---|
| Arrival / Patrol / Chase / Dive / Carry Speed | 24 / 12 / 18 / 28 / 9 | Velocidades (**× Speed Multiplier del tipo**) |
| Acceleration / Turn Speed | 26 / 5.5 | Respuesta del vuelo |
| Patrol Altitude | (12, 20) | Altura de patrulla sobre el agua |
| Stalk Altitude | 9 | Altura al acechar un pez |
| Min Altitude Above Ground | 3 | Altura mínima sobre el terreno |
| Max Altitude | 55 | Techo sobre el agua (persiguiendo al jugador puede subir hasta 25 m por encima de él) |
| Leash Margin | 60 | Metros más allá de la orilla a los que se puede alejar antes de volver al lago |

Las aves ya no se van al cielo ni se atascan en los bordes: al chocar con un árbol o una ladera la rodean por un lado (no
suben sin parar); y si quieren volar pero casi no avanzan durante 3 s, o se alejan demasiado, salen un momento volando
hacia el centro del lago a una altura segura.
| Hunt Range | 75 | Distancia máxima a la que elige presa |
| Stalk Time | (1, 2) | Segundos acechando antes de lanzarse |
| Max Dive Distance | 22 | Distancia máxima para lanzarse en picada |
| Catch Radius / Catch Depth | 2.4 / 2 | Alcance y profundidad máxima para atrapar |
| Carry Time | **por tipo** (3.5) | Segundos hasta comerse el pez (tiempo para rescatarlo) |
| Dive Cooldown | (1.8, 3.2) | Espera entre picadas |
| Detect Range | **por tipo** (26) × 1.25 | Distancia a la que te ve |
| Lose Range | 80 | Si te alejas más, pierde el interés |
| Aggro Memory | 10 | Segundos que te persigue después de que le dispares |
| Attack Reach | 2.6 | Distancia para embestirte |
| Damage | **por tipo** (15) | Armadura que te quita |
| Knockback Force / Knockback Up | 16 / 6 | Empujón horizontal y vertical |
| Stun Duration | 0.7 | Segundos quieto tras embestirte, después de rebotar hacia atrás (las embestidas rápidas usan solo una parte) |
| Player Hidden Depth | 1.5 | Más hondo que esto bajo el agua, no te ve |
| Visual / Procedural Flap / Wing y Talon Bone Names / Catch Point Offset | **por tipo** | Modelo, aleteo y garras |
| Flap Speed / Flap Angle | 7 / 28 | Aleteo por código |
| Hit / Death Prefab | del GameDirector | Efectos |

#### Cómo pelea cada ave (`BirdAI.Combat.cs`)
Además de su forma de pescar, cada especie te ataca a su manera:

| Ave | Ataque | ¿La empujan tus disparos? |
|---|---|---|
| Gaviota | **Hostigadora**: rota entre una pasada de bombardeo, "regalos" lanzados en arco desde lejos y una picada en zigzag (los regalos ensucian el visor) | Sí |
| Martín pescador | **Rayo**: se detiene, te apunta y cruza en línea recta a velocidad altísima | Mucho (es liviano) |
| Charrán | **Enjambre**: embestidas rápidas; los de alrededor lo siguen uno tras otro | Mucho |
| Garza | **Estocada** larga y fuerte después de levantar el pico | No (blindada) |
| Cormorán | **Torpedo**: embestidas rectas, también bajo el agua | Poco |
| Serreta | **A distancia**: te rodea de lejos y escupe chorros de agua en ráfagas | Sí |
| Pelícano | **Aplastador**: embestida pesada que empuja mucho | No (blindado) |
| Águila pescadora | **Picada** desde muy alto | Poco |

Los proyectiles de las aves (`EnemyProjectile`) los absorbe el Escudo de burbuja como cualquier golpe.

**Más agresivas:**
- Cuando un ave empieza a pelear contigo o recibe un disparo, **avisa a la bandada**: hasta 3 aves a menos de 30 m se suman al ataque (cada una avisa como mucho cada 4 s).
- Cada especie tiene además un segundo ataque:
  - Martín pescador: a veces rebota y repite el rayo.
  - Garza: a veces encadena una segunda estocada.
  - Serreta: alterna ráfagas de 3 con abanicos de 3 chorros.
  - Gaviota: nunca repite la táctica anterior (pasada, desde lejos o picada en zigzag).
  - Águila pescadora: si te alcanza, te lanza por los aires.
  - Pelícano: si estás lejos, te lanza bolas de agua en arco.

**Pelean como naves (`BirdAI.Evasion.cs`):**
- **Esquivan tus disparos.** Si la bala va hacia ellas, reaccionan con un tonel (giro completo) y se apartan de la línea de fuego. Tienen recarga: con fuego seguido terminas acertando.
- **Zigzag.** Si las sigues con la mira un rato, empiezan a zigzaguear. Las embestidas llegan zigzagueando, pero el zigzag se cierra al final para que el golpe conecte.
- **Viraje.** Después de atacar se van hacia un costado (no siempre hacia arriba). Ahí es cuando menos se mueven.
- **Huida.** Con poca vida (30 %), muchas escapan unos segundos al borde del lago soltando plumas y vuelven furiosas.
- Cada especie tiene su **agilidad**: martín pescador y charrán esquivan mucho; garza y pelícano (blindados) casi nada.

| Ave | Esquiva (prob. por disparo que le iba a dar) | Huye herida |
|---|---|---|
| Martín pescador | 59 % | 80 % |
| Charrán | 55 % | 60 % |
| Gaviota | 44 % | 70 % |
| Serreta | 42 % | 60 % |
| Águila pescadora | 38 % | 45 % |
| Cormorán | 34 % | 50 % |
| Garza | 17 % | 20 % |
| Pelícano | 13 % | 25 % |

**Tamaño y vida:** las aves comunes son 1.35 veces más grandes (modelo y esfera de impacto) y tienen 1.5 veces más vida
(gaviota 45, martín pescador 30, charrán 25, serreta 50, cormorán 90, garza 135, águila pescadora 165, pelícano 210).

#### Efecto al dispararles (`Assets/Scritps/FX/HitFX.cs`)
Se crea solo y no tiene campos. Cada impacto en un ave hace:
- **Parpadeo** del cuerpo del ave: un destello fuerte y dos o tres encendidos cortos que se apagan (blanco; azul si es blindada; naranja si es un jefe vulnerable). Usa la emisión del material, así se ve también a la sombra. Además un pequeño **bamboleo**.
- **Plumas** del color de la especie, un **aro** y un **fogonazo** en el punto del impacto.
- **Número de daño** que sube y se desvanece (los impactos seguidos se suman). En un jefe vulnerable sale con "x2".
- Al primer impacto en un ave blindada sale "BLINDADA". Al morir, una explosión de plumas.

#### Jefes (`BirdAI.Boss.cs`)
Los jefes son más grandes y **no cazan como las demás aves**: te rodean a pocos metros con ataques rápidos y cada pocos segundos
hacen un **ataque fuerte** (después quedan vulnerables). Sus ataques son una amenaza para todo el cardumen:

| Jefe | Ataques fuertes |
|---|---|
| Garza Gris Gigante | Estocada (la ola aturde peces) · Pisotón en el agua (lanza peces fuera, a la orilla) · Lluvia de dardos (fase 2+) |
| Pelícano Saco Sin Fondo | Remolino (arrastra peces y se traga el centro) · Cañonazo de agua (explota y aturde peces) · Bocado (fase 2+) |
| Cormorán Rey | Torpedos (2 a 4 seguidos, arriba y abajo del agua) · Onda expansiva submarina (aturde y se lleva peces) |
| Reina Águila Marina | Picadas (1 a 3) · Ráfaga de plumas · Vendaval (te empuja y saca peces del agua; fase 2+) |

**Entre ataques fuertes pelean como naves:**
- Te rodean cambiando de sentido y de distancia.
- Zigzaguean y esquivan tus disparos (menos que las aves comunes, y nunca durante un ataque fuerte ni en la pausa vulnerable).
- La **Garza Gris** es la más rápida: ataca cada 1.1–1.8 s (más seguido en cada fase) y alterna:
  - Dardos: 3 desde la fase 1 y 5 desde la fase 2.
  - Una embestida rápida.
  - Combos de 2 embestidas seguidas (3 en la fase 3).
  - Su estocada fuerte avisa menos.

**Pausa vulnerable**: después de cada ataque fuerte baja cerca del agua jadeando, te mira y retrocede despacio (la Garza Gris, 2,2 s).
Ningún ave se queda con la cabeza clavada hacia abajo salvo cuando de verdad se lanza en picada.

Si te escondes en lo hondo y el jefe no bucea, ataca a los peces más seguido. Los peces **aturdidos** flotan panza arriba
unos segundos (cualquier ave los atrapa fácil) y los **lanzados** pueden caer en la orilla: si no vuelven al agua, se asfixian.

### Sonido (`Assets/Scritps/Audio`)
- `Sfx.Play(Sonido.X, posición)` / `Sfx.Play2D(Sonido.X)` / `Sfx.Loop(...)`. Los nombres están en `Sonido.cs`.
- Los clips están en `Assets/Resources/Sonidos/<Nombre>.wav`. Las variantes `<Nombre>_2`, `_3`… se eligen al azar.
- **Para poner un sonido propio, reemplaza el `.wav` con el mismo nombre.** No hay que tocar código.
- Los actuales los genera `Herramientas/Sonido/generar_sonidos.py` (síntesis con numpy). Si lo vuelves a ejecutar, pisa los `.wav`.
- `GameAudio` se agrega solo al jugador:
  - Disparos, golpes al traje y alarma con poca armadura.
  - Propulsor, dash y turbo.
  - Ambiente del lago y bajo el agua. Bajo el agua todo se oye apagado (filtro en el AudioListener).
- El volumen general se guarda en PlayerPrefs (`Volumen`, `Sfx.Volume`).

### Archivo de aves: bocetos (`Herramientas/Archivo`)
Los dibujos del Archivo (`Assets/Resources/Archivo/<archivo del Ave>.png`, 512×512 con transparencia) son bocetos holográficos hechos a partir de los modelos de las aves, en dos pasos:
1. `render_aves.py` (Blender como módulo `bpy`, Cycles en CPU): renderiza cada modelo de `Assets/Models/Aves` de 3/4 en dos pasadas (luz y normales).
   `python render_aves.py <carpeta_de_salida>`
2. `bocetos.py` (numpy + Pillow): convierte esos renders en boceto azul eléctrico (contornos dobles, sombreado a rayas, círculo de construcción, brillo y líneas de escaneo) y escribe los `.png` con su `.meta`.
   `python bocetos.py <carpeta_de_los_renders>`
**Para usar un dibujo propio**, reemplaza el `.png` con el mismo nombre que el archivo del Ave (por ejemplo `Ave_Gaviota.png`). Si un Ave no tiene dibujo, su ficha sale sin boceto.

### Vegetación y ambiente del lago
Todo se agrega solo al darle Play en la escena del lago (`LakeDecorator` y `LakeAmbience`), sin tocar la escena.
Por eso, sin Play, la escena se ve con el cielo viejo y sin vegetación.

**Verlo en el editor sin darle Play**: menú **Herramientas > Lago**:
- **Vista previa en el editor**: genera la vegetación y aplica el cielo, la niebla y la luz (tarda unos segundos).
- **Regenerar vista previa**: después de cambiar `Decoracion_Lago`.
- **Quitar vista previa**: la borra y deja el cielo y la luz como estaban.

La vista previa **no se guarda en la escena**: sus objetos son temporales y el cielo y la luz originales se vuelven a poner
antes de guardar, al darle Play y al cerrar la escena. Al darle Play se esconde y el juego genera lo suyo; al volver de Play
reaparece sola. Las hojas, el polen y los efectos bajo el agua solo se ven en Play. La opción queda marcada en el menú
mientras está encendida.

**Vegetación por zonas** (`Decoracion_Lago`). Sigue cómo crecen las plantas en un lago de verdad: del agua hacia afuera,
plantas sumergidas → hojas flotantes → plantas emergentes en el agua baja → franja leñosa de la orilla → pradera → bosque.

| Zona | Distancia a la orilla | Qué hay |
|---|---|---|
| Fondo del lago | dentro del agua | Pradera de pasto acuático, algas (altas, tupidas y brillantes), piedras, troncos y chatarra |
| Superficie | 2–25 m hacia dentro | Nenúfares en grupos (nunca en el centro, que queda libre para jugar) |
| Agua baja (hasta 1,6 m de profundidad) | 0–13 m hacia dentro | Juncos y totoras (unos 3 m de alto) |
| Orilla | −3 a 8 m | Hierba alta y juncos |
| Pradera | 2–60 m | Sauces llorones (unos 16 m) en los primeros metros, hierba, flores y arbustos sueltos |
| Borde del bosque | 12–120 m | Abedules (tronco blanco, copa verde amarilla), árboles de hoja ancha (unos 20 m), arbustos y helechos |
| Bosque cercano | 45–120 m | Pinos, pinos esbeltos y abetos (17–25 m) en grupos, con helechos al pie: lo que se ve desde el lago, más tupido |
| Bosque lejano | 120–250 m | Más pinos y abetos, en grupos más grandes |

- **Tamaño**: el pez mide unos 2 m; los árboles se colocan a 1,7–2,6 veces el tamaño del modelo (pinos de 17 a 25 m), los
  arbustos y la hierba a 1,3–2,6 veces y las plantas del agua a 1,1–1,9 veces (`Scale Range` de cada entrada).
- **Reparto parejo**: cada grupo se coloca en una casilla distinta de una rejilla que cubre toda su zona (en orden al azar),
  así no quedan claros grandes ni montones. Con `Seed` cambia el reparto.

- Los árboles tienen colisión en el tronco (el jugador choca y las aves los esquivan). Las plantas chicas no tienen colisión ni proyectan sombra, y desaparecen de lejos (`Cull Screen Size`).
- Junto a la orilla nada alto pasa de 20 m sobre el agua (`Max Top Above Water`, más 1 m por cada metro que se aleja): espacio para las aves. El centro del lago y la salida del jugador quedan libres.
- En total son unos 12.400 objetos. Si el juego va lento, baja primero `Count` de la hierba, las flores, los helechos y el pasto acuático.
- Hierba, flores, juncos, totoras, helechos y árboles se mecen con el viento. Los nenúfares flotan y giran despacio.
- **Para cambiar cuánto hay de cada cosa**, edita la entrada en `Decoracion_Lago` (`Count`, distancias, escala...). Cambia `Seed` para otro reparto.
- Si alguna vez generaste la decoración en el editor y la guardaste en la escena, presiona **Generar ahora** en `LakeDecorator` (o marca `Regenerate On Play`) para que aparezcan las plantas nuevas.

**Modelos** (`Assets/Models/Entorno`, mismo estilo de plastilina que el pez y las aves; se generan con
`Herramientas/Modelado/entorno/build_all.py`): Pino_A, Pino_B, Abeto, Abedul, Arbol_Hoja, Sauce, Arbusto, Helecho, Hierba,
Flores, Juncos, Totora, Nenufar y Pasto_Acuatico (más los del fondo). Cada uno es un script en esa carpeta; se cambia y se vuelve a ejecutar.

**Cielo, niebla, luz y viento** (`LakeAmbience`, se crea solo como "AmbienteLago"):
- **Cielo estilizado** (`Cielo_Lago`): degradado azul, sol con halo cálido y nubes pintadas (lado iluminado y sombra en bloques) que se mueven con el viento. Todos sus colores y la cantidad de nubes se ajustan en el material.
- **Niebla de distancia** del color del horizonte (exponencial al cuadrado, densidad 0.0026): de cerca no se nota; a 150 m un 14 % y lo muy lejano se funde con el cielo. Da profundidad sin tapar la pelea.
- **Luz ambiente** en degradado (cielo / horizonte / suelo) y un tono cálido de media mañana en el sol (no cambia su intensidad ni dirección).
- **Viento con ráfagas**: cambia la fuerza del vaivén de las plantas, la velocidad de las nubes y la cantidad de hojas que caen. Su dirección gira un poco con el tiempo.
- **Bajo el agua**: con la cámara bajo la superficie la niebla cambia al instante por una densa del color del agua (más oscura en lo hondo): de cerca se ve bien, a 10 m un 30 % borroso, a 20 m un 76 % y a 30 m casi nada. Al salir vuelve la niebla de aire en un instante. Se suma al filtro azul del `Underwater Volume`.
- **Para volver al cielo anterior**: agrega `LakeAmbience` a mano a la escena y desmarca `Use Stylized Sky` (lo mismo con la niebla, la luz o el sol).

| Campo de LakeAmbience | Defecto | Qué hace |
|---|---|---|
| Use Stylized Sky / Sky Material | ✔ / vacío (= Cielo_Lago) | Cielo del juego |
| Fog / Fog From Sky / Fog Color / Fog Density | ✔ / ✔ / celeste / 0.0026 | Niebla de distancia (del color del horizonte) |
| Set Ambient / Ambient Sky, Equator, Ground | ✔ / azul, verde gris, tierra | Luz ambiente en degradado |
| Tint Sun / Sun Color | ✔ / blanco cálido | Tono del sol |
| Wind Direction / Wind Strength / Gust Strength / Gust Period / Cloud Drift | (0.94, 0, 0.34) / 1 / 0.6 / 9 s / 0.01 | Viento y ráfagas |
| Underwater Fog / Water Fog Shallow, Deep / Water Dark Depth / Water Fog Density / Water Fog Fade Out | ✔ / verde azulado claro, oscuro / 8 m / 0.06 / 6 | Visibilidad bajo el agua |
| Ambient Effects | ✔ | Hojas, polen, niebla baja y efectos bajo el agua (`AmbientParticles`) |

**Efectos ambientales** (`AmbientParticles`, siguen a la cámara y no se emiten bajo el agua):
- **Hojas** que caen de los abedules, sauces y árboles de hoja cercanos (hasta 55 m). Giran en el aire, el viento las lleva y las que llegan al agua se quedan flotando hasta desvanecerse. Colores de verde a naranja; caen más con las ráfagas.
- **Polen y motas de luz** cálidas flotando despacio alrededor de la cámara.
- **Niebla baja** sobre el agua: velos suaves que se mueven con el viento, siempre a más de 14 m de la cámara.
- **Bajo el agua**: partículas en suspensión flotando alrededor de la cámara, rayos de luz que bajan desde la superficie (más fuertes cerca de ella) y burbujas que suben.

### El Taller (escena Taller, `WorkshopScene`)
Una **cueva bajo el lago** convertida en el taller de un inventor (referencias: el taller de Tony Stark en las películas
de Iron Man y cuevas submarinas estilizadas). Todo se arma por código al abrir la escena:
- **Cueva**: estalactitas en el techo y estalagmitas junto a las paredes, cristales que brillan (con luz), algas, la
  **superficie del lago** vista desde abajo en el techo (ondula), rayos de luz, **cáusticas** que se mueven en el suelo,
  burbujas y un cardumen nadando cerca del techo.
- **Sala de armaduras**: una repisa alta al fondo con pasarela, baranda y luz de borde, y tres **vitrinas** iluminadas con
  trajes viejos: Mk I (hierro gris), Mk II (plata) y Mk III (rojo y oro).
- **Mesa holográfica** con el traje en holograma girando, parpadeando y con anillos que suben.
- **Brazo robótico** junto al traje: se mece apuntando al traje y **suelda** (chispas y destellos) durante la instalación.
- **Monitores** con interfaces que brillan, **lámparas colgantes** y la **mesa de chatarra** con tablero de herramientas,
  morsa, flexo y cajonero.
- **El mecánico**: un bagre de canal erguido, con gafas, delantal de cuero, ocho barbillas y una llave inglesa.

Modelos (en el componente WorkshopScene, todos hechos con `Herramientas/Modelado/entorno`): `Mechanic Model`
(Mecanico_Bagre), `Bench Model` (Mesa_Chatarra), `Display Case Model` (Vitrina_Traje), `Holo Table Model` (Mesa_Holo),
`Monitors Model` (Pantallas_Taller), `Arm Base / Segment / Claw Model` (BrazoRobot_*), `Stalactite Model` (Estalactitas)
y `Crystal Model` (Cristales_Cueva). Si falta alguno se usa una figura simple.
Para regenerarlos: `python Herramientas/Modelado/entorno/build_all.py Mecanico_Bagre Mesa_Holo ...` (los objetos del
Taller están todos en `Taller_Props.py`; el traje de los defensores en `Traje_Defensor.py`).

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
  - `Trail Length` 14 m: largo de la estela (la cargada 1.7×, la burbuja 0.4×).
  - El Rigidbody del prefab está sin gravedad, interpolado y con detección continua (el código lo asegura igual al crearla).

---

## 11. Cosas a revisar en la escena

1. **PlayerController_Base > Ground Layer**: en la escena está en "Water", pero ya no importa: al empezar, el código agrega solo la capa del terreno y avisa en la consola. Si quieres quitar el aviso, ponlo en **Default**.
2. **"Main Camera " duplicada**: quedó desactivada. Bórrala. Solo debe haber una cámara activa (`PlayerCamera`) y un AudioListener.
3. **Build Settings**: tienen que estar `MainMenu` (primera) y `SampleScene`. Si falta una, los botones JUGAR, MAPA o MENÚ PRINCIPAL no podrán cargarla.
4. Se borraron los campos que ningún script usaba (inclinación vertical, física vieja del agua y salpicaduras del jugador, colores "normales" de la UI, salpicadura de pájaros y balas) y los scripts viejos `EnemyBird` y `EnemyCollision`. Si la consola avisa de un *missing script* en algún objeto o prefab, quítale ese componente vacío.
5. `Assets/Niveles/Nivel_1` (tu archivo anterior) no se usa: los niveles del juego son los de `Assets/Data/Niveles`. Puedes borrarlo cuando quieras.
6. Si al darle Play **no aparecen peces ni pájaros**, mira la consola. Lo más común es "GameDirector: no hay agua...": revisa que Lago_Agua esté dentro del hueco y pulsa *Reconstruir agua*.
7. Si un modelo de pez sale diminuto o de lado: revisa que `Fish_Swim.fbx` tenga **Preserve Hierarchy** activado (pestaña Model).
