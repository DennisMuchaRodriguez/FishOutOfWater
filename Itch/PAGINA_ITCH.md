# Página de itch.io — Fish Out Of Water

Todo lo necesario para publicar el juego en itch.io, campo por campo. Los nombres de los campos están en inglés, como aparecen en itch.io.

## Archivos de esta carpeta

| Archivo | Para qué | Dónde se sube |
|---|---|---|
| `portada_630x500.png` | Portada: la imagen que sale en las listas y búsquedas de itch.io | **Cover image** |
| `portada_1260x1000.png` | La misma portada al doble de tamaño (por si quieres más nitidez) | — |
| `banner_960x300.png` | Banner con el título, arriba de la página | **Edit theme → Banner** |
| `banner_1920x600.png` | El mismo banner al doble de tamaño (pantallas grandes) | **Edit theme → Banner** (si usas la página ancha) |
| `fondo_1920x1080.jpg` | Fondo oscuro de la página (el lago, desenfocado) | **Edit theme → Background image** |
| `arte_principal_1920x1080.png` | Arte con el título: devlogs, prensa, la primera imagen de la descripción | Descripción, devlogs, redes |
| `tarjeta_redes_1200x630.png` | Para compartir el enlace en redes | YouTube, X, Instagram, TikTok… |
| `descripcion.html` | La descripción con formato (español e inglés) | **Description** (copiar y pegar) |

Las imágenes se hicieron con los modelos del juego: el pez con su traje, las texturas y las aves son las mismas del juego. Para rehacerlas, ver [Cómo se hicieron las imágenes](#cómo-se-hicieron-las-imágenes).

---

## 1. Antes de empezar

1. Crea la cuenta en itch.io (o entra a la tuya) y en **Settings → Profile** pon como nombre **Kai Codex**. El usuario define tu dirección: si es `kaicodex`, la página será `kaicodex.itch.io`.
2. Prepara el juego para Windows (ver [Subir el juego](#4-subir-el-juego-windows)).
3. Haz las capturas (ver [Capturas](#5-capturas-y-tráiler)). Son lo que más convence a la gente de descargar.

## 2. Crear el proyecto

**Dashboard → Create new project**, y rellena:

| Campo | Qué poner |
|---|---|
| **Title** | `Fish Out Of Water` |
| **Project URL** | `fish-out-of-water` → `kaicodex.itch.io/fish-out-of-water` |
| **Short description or tagline** | `Un pez. Un traje. Nada de agua. Defiende el lago de las aves con tu traje de combate.` |
| **Classification** | Games |
| **Kind of project** | Downloadable (se descarga; es un juego de Unity para Windows) |
| **Release status** | In development |
| **Pricing** | **No payments** (gratis), o **Donations** / **$0 or donate** si quieres aceptar apoyo voluntario |
| **Uploads** | El `.zip` de Windows (ver punto 4). Marca la casilla **Windows** |
| **Description** | Pega `descripcion.html` (abre el archivo en el navegador, selecciona desde «Un pez. Un traje…» hasta el final, copia y pega). Cambia los enlaces de las redes |
| **Genre** | Action |
| **Tags** (máx. 10) | `action`, `shooter`, `3d`, `flying`, `co-op`, `multiplayer`, `survival`, `fish`, `birds`, `arcade` |
| **AI generation disclosure** | itch.io pregunta si el proyecto tiene contenido hecho con IA generativa. Parte del código, la música, estas imágenes y los textos se hicieron con ayuda de IA, así que lo correcto es marcar **Yes** y las categorías que correspondan (código, sonido, gráficos, texto) |
| **App store links** | Vacío |
| **Custom noun** | Vacío (dice "game") |
| **Community** | **Comments**: así la gente te deja opiniones en la página |
| **Visibility & access** | **Draft** mientras la armas. Cuando esté lista: **Public** |

En **Details → More information** (si aparece):

| Campo | Qué poner |
|---|---|
| **Made with** | Unity |
| **Inputs** | Keyboard, Mouse |
| **Languages** | Spanish |
| **Multiplayer** | Server-based networked multiplayer (en línea) y Ad-hoc networked multiplayer (misma red) |
| **Player count** | 1 – 4 |
| **Average session** | About a half-hour |

**Cover image**: `portada_630x500.png`.

## 3. El tema de la página (Edit theme)

En la página del juego, arriba: **Edit theme**.

| Opción | Valor |
|---|---|
| **Layout** | La columna del juego centrada (o a la derecha, con la descripción a la izquierda) |
| **Banner** | `banner_960x300.png` (posición: centro) |
| **Background image** | `fondo_1920x1080.jpg` · Repeat: **No repeat** · Position: **Top center** (o "Cover" si aparece) |
| **Background** (color) | `#071224` |
| **Inner column / BG2** (el fondo de la columna) | `#0B1A30` (si deja opacidad, al 90 %) |
| **Text** | `#E8F4FF` |
| **Link** | `#40B8FF` (el azul eléctrico del casco) |
| **Button** | `#FF7A2F` (el naranja del pez; es el botón de descargar) |
| **Fonts** | Títulos: una fuente gruesa y técnica, la más parecida a la del juego (Oxanium) que ofrezca la lista; texto: una sin serifa limpia |

Los colores son los mismos de la interfaz del juego: azul marino del casco, texto claro, azul eléctrico y el naranja del pez.

## 4. Subir el juego (Windows)

1. En Unity: **File → Build Profiles → Windows** (Windows 64-bit). Revisa que en la lista de escenas estén `MainMenu` (la primera), `SampleScene` y `Taller`.
2. **Build** → elige una carpeta nueva, por ejemplo `Builds/FishOutOfWater_Windows`.
3. Prueba el `.exe` de esa carpeta: menú, una partida, el Taller y una sala cooperativa.
4. Comprime **la carpeta entera** (el `.exe`, la carpeta `_Data`, `UnityPlayer.dll`, `MonoBleedingEdge`…) en un `.zip`: `FishOutOfWater_Windows_v0.1.zip`.
   - No hace falta incluir la carpeta `..._BurstDebugInformation_DoNotShip` si aparece.
5. En itch.io: **Uploads → Upload files** → el `.zip` → marca **Windows**.
6. Para las versiones siguientes puedes reemplazar el archivo, o usar **butler** (la herramienta de itch.io) para subir solo lo que cambió.

El cooperativo **en línea** necesita los servicios de Unity activados en Unity Cloud (Lobby y Relay; ver la guía del proyecto). En **red local** funciona sin nada más. La versión para navegador (HTML5) no está pensada para este juego: el cooperativo usa conexiones que el navegador no permite.

## 5. Capturas y tráiler

**Screenshots**: entre 3 y 6, a 1920 × 1080. En Unity pon la vista **Game** a 1920 × 1080, **Maximize On Play** y captura con **Win + Shift + S** (o el paquete **Unity Recorder** para imágenes, GIF o vídeo). Ideas:

1. Volando sobre el lago disparando a una bandada.
2. Un jefe (la Garza Gris Gigante) a media pelea.
3. Bajo el agua con el cardumen.
4. El Taller con los tres planos.
5. La sala cooperativa con los 4 trajes en sus pedestales.
6. El menú principal con la cinemática del traje.

Un GIF corto (5–8 s) de la salida del agua o de un combate funciona muy bien como primera captura.

**Gameplay video or trailer**: un vídeo de YouTube de 30–60 s, si lo tienes (también sirve para el canal de Kai Codex).

## 6. Tus redes de Kai Codex

Hay dos lugares:
1. **En tu perfil** (Settings → Profile): los enlaces a tus redes salen en tu página de creador y en todas tus páginas.
2. **En la descripción del juego**: la sección «Sigue a Kai Codex» de `descripcion.html`. Cambia `[ENLACE_YOUTUBE]`, `[ENLACE_TIKTOK]`, `[ENLACE_INSTAGRAM]`, `[ENLACE_X]` y `[ENLACE_DISCORD]` por tus enlaces, y borra los que no uses.

## 7. Antes de publicar

- [ ] Cover image subida (630 × 500).
- [ ] Banner y fondo en **Edit theme**, y los colores puestos.
- [ ] El `.zip` de Windows subido, marcado como **Windows**, y probado: descárgalo desde la página y juega un rato.
- [ ] Descripción pegada y los enlaces de las redes cambiados.
- [ ] Entre 3 y 6 capturas.
- [ ] Genre, tags y la pregunta de IA respondida.
- [ ] Comentarios activados.
- [ ] Mírala con **View page** estando en Draft; cuando te guste, **Public**.

Después de publicar, escribe un **devlog** (en la página: **Devlog → Create new post**) con `arte_principal_1920x1080.png`: qué es el juego, qué tiene esta versión y qué viene después. Cada devlog vuelve a mostrar el juego a quienes te siguen.

## Cómo se hicieron las imágenes

En dos pasos (desde la raíz del proyecto):

1. `Herramientas/Itch/render_arte_itch.py` (Blender como módulo de Python, `pip install bpy`): arma la escena con los modelos del juego (`PESCAO.fbx` con sus texturas de `Assets/Models/Texturas`, y la garza, la gaviota y el cormorán de `Assets/Models/Aves`), el lago, el cielo y la salpicadura, y renderiza tres tomas (ancha, portada y banner).
   `python Herramientas/Itch/render_arte_itch.py <carpeta> final`  (con `borrador` sale rápido y chico, para probar)
2. `Herramientas/Itch/componer_imagenes.py` (Pillow): les pone el título con las fuentes y los colores del juego y escribe todo en `Itch/`.
   `python3 Herramientas/Itch/componer_imagenes.py <carpeta>`
