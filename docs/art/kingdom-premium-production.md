# Kingdom premium — producción y reproducción

Estado documental: 10 de septiembre de 2026. Esta muestra PC contiene **Worker, Warrior y Hero en poses estáticas**, un edificio Kingdom y su paisaje. La validación de assets Unity, la compilación Windows y la revisión del ejecutable a 1920×1080 han terminado correctamente. Se han revisado las capturas finales de Editor, sus tres comparaciones y el HUD nativo del ejecutable. La apariencia mejora frente a la entrega anterior, pero todavía presenta diferencias sustanciales con las referencias y no se da por aceptada como resultado premium.

## Fuentes y autoría

La anatomía de los tres personajes procede de **Body Male - Realistic**, de **Dan Ulrich**, incluida en **Blender Studio Human Base Meshes 1.4.1** bajo **CC0**. La procedencia se conserva en los tres manifiestos y en el [README de personajes](../../Assets/Game/KingdomPremium/Characters/README.md). Se utiliza la colección anatómica CC0; el rig Rain no forma parte de estas exportaciones. Véanse la [publicación oficial de los assets](https://www.blender.org/download/demo-files/) y la [documentación del paquete](https://developer.blender.org/docs/release_notes/4.0/asset_bundles/).

La [descarga exacta del paquete 1.4.1](https://download.blender.org/demo/asset-bundles/human-base-meshes/human-base-meshes-bundle-v1.4.1.zip) tiene SHA256 `811f43accbb31a88266d932f8f5563b2d13586fca0ba2693aad1f5fe582b3515`. Su archivo de entrada es `D:/CodexTooling/kingdom-premium/human-base-meshes/human-base-meshes-bundle-v1.4.1/human_base_meshes_bundle.blend`.

Los scripts propios [kingdom_characters.py](../../tools/art/kingdom_characters.py) y [kingdom_character_meshes.py](../../tools/art/kingdom_character_meshes.py) construyen el vestuario, equipo, accesorios, pelo, ajustes de proporción y pose sobre esa base. La cara y las manos conservan geometría anatómica; los ojos tienen geometría para esclera, iris y pupila. La flor de lis se utiliza como motivo heráldico tradicional. Las referencias del usuario orientan el diseño y la comparación; no se importan como personajes, fondos de escena ni identidad de marca.

Cada FBX contiene una malla por LOD y submallas por material semántico. **LOD0 conserva la superficie visible de la autoría**, sin imponer la reducción global de la exportación anterior. En Hero solo se reduce el teselado de la horma cubierta del calzado y del soporte del collar. LOD1 y LOD2 se derivan con objetivos de aproximadamente 100 000 y 35 000 triángulos; ya no se calculan al 40 % y al 15 % de LOD0. **No hay armature exportado, rig, clips ni nuevas acciones de juego.** Los recuentos siguientes proceden de reimportar los FBX finales en Blender; no constituyen un perfil de rendimiento ni equivalen a los vértices finales que pueda dividir el importador Unity:

| Personaje | Triángulos LOD0 | LOD1 | LOD2 | Materiales por LOD | Manifiesto |
| --- | ---: | ---: | ---: | ---: | --- |
| Worker | 227 654 | 99 999 | 34 999 | 16 | [Worker.json](../../Assets/Game/KingdomPremium/Characters/Worker.json) |
| Warrior | 274 134 | 100 000 | 34 999 | 15 | [Warrior.json](../../Assets/Game/KingdomPremium/Characters/Warrior.json) |
| Hero | 292 168 | 100 000 | 34 829 | 20 | [Hero.json](../../Assets/Game/KingdomPremium/Characters/Hero.json) |

Hero LOD2 registra 34 829 triángulos después del intercambio FBX, frente a los 35 000 previos. Los manifiestos conservan la medición anterior en `beforeFbxReimport` y el SHA256 del FBX validado. Las nueve mallas superan las comprobaciones de coordenadas finitas, normales y UV; esto acredita la transferencia del asset y no su apariencia final en Unity.

El entorno dispone de [README](../../Assets/Game/KingdomPremium/Environment/README.md) y [material-manifest.json](../../Assets/Game/KingdomPremium/Environment/material-manifest.json). El edificio, terreno, composición, tejas individuales, banderas y carpintería arquitectónica se construyen mediante [kingdom_environment.py](../../tools/art/kingdom_environment.py). La vegetación usa geometría con hojas y hierba recortadas por alpha; no usa renders de árboles colocados como fondos. Los assets descargados son [CC0 de Poly Haven](https://polyhaven.com/license); el manifiesto conserva su autor y URL individual:

| Uso | Assets de origen | Autoría indicada por el proveedor |
| --- | --- | --- |
| Árbol | [tree_small_02](https://polyhaven.com/a/tree_small_02) | Rico Cilliers |
| Helechos y hierba | [fern_02](https://polyhaven.com/a/fern_02), [grass_medium_01](https://polyhaven.com/a/grass_medium_01) | Rob Tuytel, Rico Cilliers |
| Rocas | [rock_moss_set_01](https://polyhaven.com/a/rock_moss_set_01) | Kless Gyzen |
| Barril y caja | [wine_barrel_01](https://polyhaven.com/a/wine_barrel_01), [wooden_crate_01](https://polyhaven.com/a/wooden_crate_01) | James Ray Cock |
| Puerta | [large_castle_door](https://polyhaven.com/a/large_castle_door) | Tina |
| Piedra y pavimento | [medieval_blocks_05](https://polyhaven.com/a/medieval_blocks_05), [cobblestone_floor_08](https://polyhaven.com/a/cobblestone_floor_08) | Rob Tuytel |
| Pizarra, enlucido y madera | [roof_slates_02](https://polyhaven.com/a/roof_slates_02), [white_rough_plaster](https://polyhaven.com/a/white_rough_plaster), [wood_planks_dirt](https://polyhaven.com/a/wood_planks_dirt) | Rob Tuytel |
| Suelo | [brown_mud_leaves_01](https://polyhaven.com/a/brown_mud_leaves_01), [leafy_grass](https://polyhaven.com/a/leafy_grass) | Rob Tuytel; Charlotte Baglioni, respectivamente |

El [registro de texturas](kingdom-premium-textures.md) documenta por separado `RoyalEmbroidery.png`, generado como superficie de tela, y `Daylight.hdr`, el HDRI CC0 [Kloppenheim 06 Pure Sky](https://polyhaven.com/a/kloppenheim_06_puresky). `Characters/Textures/HairGrain.png` es variación gris procedural propia que multiplica el color del pelo. Ninguna de estas superficies reemplaza la geometría del personaje o edificio.

## Reproducir la autoría Blender

Ejecutar desde la raíz del repositorio con Blender **4.5.13 LTS**, Python y Pillow disponibles. La ruta de Blender en esta estación es `D:/CodexTooling/blender-portable/blender-4.5.13-windows-x64/blender.exe`. Los scripts guardan descargas, `.blend` y previews en `D:/CodexTooling/kingdom-premium`; solo los FBX finales, texturas y manifiestos entran en `Assets/Game/KingdomPremium`. En esta estación esa carpeta de assets utiliza una junction hacia D:; el importador sigue utilizando las rutas `Assets/...`.

Descargar y extraer el paquete de anatomía en la ruta indicada arriba antes de ejecutar el generador. Comprobar el hash del ZIP conservado con `Get-FileHash -Algorithm SHA256`. Con la importación de Unity detenida, generar los tres personajes:

```powershell
$premiumBlender = 'D:/CodexTooling/blender-portable/blender-4.5.13-windows-x64/blender.exe'
$premiumTemp = 'D:/CodexTooling/kingdom-premium/temp'
New-Item -ItemType Directory -Force -Path $premiumTemp | Out-Null
$env:TEMP = $premiumTemp
$env:TMP = $premiumTemp
foreach ($premiumKind in @('Worker', 'Warrior', 'Hero')) {
    & $premiumBlender --factory-startup --background --disable-autoexec --python tools/art/kingdom_characters.py -- --kind $premiumKind
    if ($LASTEXITCODE -ne 0) { throw "Falló la exportación de $premiumKind" }
}
```

El destino predeterminado es `Assets/Game/KingdomPremium/Characters/`. `--output <carpeta>` permite exportar primero a una carpeta de preparación en D:; `--skip-render` omite los previews y conserva la exportación. Se generan `Worker.blend`, `Warrior.blend` y `Hero.blend` en la carpeta de autoría, junto con previews front/side/back/face cuando están habilitados. Esas vistas se renderizan en Blender y no acreditan la apariencia integrada en Unity.

Después de exportar los tres personajes, [kingdom_character_validate.py](../../tools/art/kingdom_character_validate.py) reimporta los FBX, comprueba mallas, coordenadas finitas, normales, UV y slots de material, y registra los recuentos y hashes reales. Escribe los JSON y `blender-export-validation.json` únicamente si las nueve mallas pasan; no modifica los FBX. Para el destino predeterminado usado arriba:

```powershell
& $premiumBlender --factory-startup --background --disable-autoexec --python tools/art/kingdom_character_validate.py -- --folder 'Assets/Game/KingdomPremium/Characters'
if ($LASTEXITCODE -ne 0) { throw 'Falló la reimportación de los personajes' }
```

El [README de personajes](../../Assets/Game/KingdomPremium/Characters/README.md) documenta la preparación en `D:/CodexTooling/kingdom-premium/refined-characters`. Si se ha utilizado esa ruta con `--output`, ejecutar el mismo validador con `--folder D:/CodexTooling/kingdom-premium/refined-characters` antes de publicar los FBX, manifiestos, texturas y el informe en `Assets/Game/KingdomPremium/Characters/`.

La secuencia principal del entorno descarga los assets y mapas, prepara sus importaciones, construye la variante del árbol y exporta la composición. El script principal ejecuta el compresor JPEG: conserva la resolución de las superficies arquitectónicas de 2K y la utilería de 1K; los mapas alpha siguen siendo PNG sin pérdidas. El último paso añade el sendero, con tres mapas de 1K, y prepara el FBX final en D: antes de transferirlo:

```powershell
python tools/art/kingdom_environment_assets.py
if ($LASTEXITCODE -ne 0) { throw 'Falló la descarga del entorno' }
foreach ($premiumScript in @('kingdom_environment_probe.py', 'kingdom_environment_tree.py', 'kingdom_environment.py')) {
    & $premiumBlender --factory-startup --background --disable-autoexec --python (Join-Path 'tools/art' $premiumScript)
    if ($LASTEXITCODE -ne 0) { throw "Falló $premiumScript" }
}
& $premiumBlender --factory-startup --background --disable-autoexec --python tools/art/kingdom_environment_transition.py -- --from-base
if ($LASTEXITCODE -ne 0) { throw 'Falló la exportación del sendero' }
$premiumStage = 'D:/CodexTooling/kingdom-premium/environment/staged'
$premiumEnvironment = 'Assets/Game/KingdomPremium/Environment'
Copy-Item -LiteralPath (Join-Path $premiumStage 'Environment.fbx') -Destination $premiumEnvironment -Force
Copy-Item -LiteralPath (Join-Path $premiumStage 'material-manifest.json') -Destination $premiumEnvironment -Force
foreach ($premiumMap in @('diffuse', 'nor_gl', 'rough')) {
    Copy-Item -LiteralPath (Join-Path $premiumStage "Textures/courtyard_trail_${premiumMap}_1k.jpg") -Destination (Join-Path $premiumEnvironment 'Textures') -Force
}
& $premiumBlender --factory-startup --background --disable-autoexec --python tools/art/kingdom_environment_validate.py
if ($LASTEXITCODE -ne 0) { throw 'Falló la comprobación del FBX del entorno' }
```

El árbol se prepara en `environment/tree-final.blend`; la composición inicial queda en `environment/kingdom_environment.blend` y la versión completa con sendero en `environment/kingdom_environment_final.blend`. La salida persistente es `Environment/Environment.fbx`, `Environment/Textures/` y su manifiesto. El índice de descargas original queda en `environment/download-index.json`. Las utilidades `stage`, `refine`, `tree_compare` y `tree_probe` documentan revisiones intermedias; no forman parte de esta reconstrucción final.

El manifiesto final del entorno registra 36 mallas únicas, 961 objetos, 25 materiales, 11 árboles de 349 999 triángulos y 5 590 279 triángulos contando las instancias. Las 54 texturas fuente suman 52 389 227 bytes. `fbx-validation.json` supera la comprobación de reimportación en Blender sin errores; esto verifica la transferencia de geometría, materiales y archivos, y no equivale a un resultado Unity ni a rendimiento móvil.

## Contrato de importación Unity

Los personajes usan metros, Blender arriba +Z y frente -Y, con exportación FBX `axis_forward='Z'`, `axis_up='Y'` y conversión horneada. El entorno utiliza sus propios ajustes FBX `axis_forward='-Z'`, `axis_up='Y'`. La intención final común es Unity arriba +Y, frente -Z y pies en Y=0. Las capturas de importación confirmaron que **tanto la raíz del entorno como las raíces de Worker, Warrior y Hero necesitan yaw de 180°** para orientarse hacia la cámara frontal. Aplicar esa corrección a ambos tipos de asset y revisar su orientación y apoyo en la composición final.

Los colores de los JSON están en espacio lineal de Blender. El baker convierte esos valores al alimentar las propiedades de color de Unity; no se deben volver a oscurecer las telas multiplicando su textura por un azul adicional. Albedo usa sRGB; normales, roughness, metallic y opacity se importan como datos. Unity URP Lit recibe roughness convertida a smoothness. Los materiales de follaje mantienen el recorte alpha y las caras necesarias para sus hojas.

El [baker](../../Assets/Game/Editor/KingdomPremiumBaker.cs) importa los modelos sin animación, crea materiales persistentes y LODGroup, guarda los prefabs de las tres figuras y monta `Assets/Game/Scenes/KingdomPremium.unity`. La muestra tiene su propio pipeline y cámaras; el baker restaura la configuración de render previa al terminar. La revisión de esa restauración forma parte de la comprobación final, no se presupone por la mera existencia del código.

## Generar la escena, el ejecutable y sus capturas

Usar los wrappers del proyecto con Unity Editor cerrado. [Build-KingdomPremium.ps1](../../tools/Build-KingdomPremium.ps1) ejecuta por defecto Unity en **`D:/EmberfieldWorkingCache/AoE/KingdomBuildProject`**, una carpeta nativa de D: para que los temporales grandes de compilación permanezcan en ese volumen. `Assets`, `Packages`, `Library`, `Artifacts`, `Builds` y `TestResults` son junctions al proyecto compartido; `ProjectSettings` es una copia. Ambos proyectos utilizan **la misma versión de Unity y se ejecutan de forma secuencial**, porque comparten assets y caché de importación. `-ScratchDirectory` permite elegir el directorio padre de ese proyecto de ejecución.

Antes de copiar `ProjectSettings`, el wrapper comprueba que el proyecto de ejecución esté disponible y que cada una de las seis junctions apunte al checkout actual. Si detecta otro destino, detiene el proceso e indica que se use un `-ScratchDirectory` distinto. También comprueba la disponibilidad del proyecto original antes de preparar la ejecución.

La ejecución normal realiza el bake completo; `-Player` añade el ejecutable Windows. `-PlayerOnly` recompila desde la escena ya guardada sin regenerar los assets ni las capturas de Editor:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools/Build-KingdomPremium.ps1 -Player
powershell -NoProfile -ExecutionPolicy Bypass -File tools/Review-KingdomPremium.ps1 -Width 1920 -Height 1080
```

La ruta Unity predeterminada del wrapper es `D:/Unity/Editors/6000.3.23f1/Editor/Unity.exe`; se puede cambiar con `-UnityPath`. El ejecutable resultante se ubica en `Builds/KingdomPremium/KingdomPremium.exe`. Una reconstrucción del visor utiliza:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools/Build-KingdomPremium.ps1 -PlayerOnly
```

`-SceneOnly` reutiliza los modelos y materiales importados, pero reconstruye la escena, su iluminación y las seis capturas de Editor. Es la opción para revisar encuadre o luz sin rehacer los materiales. Añadir `-Player` produce también el ejecutable de esa escena reconstruida:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools/Build-KingdomPremium.ps1 -SceneOnly
powershell -NoProfile -ExecutionPolicy Bypass -File tools/Build-KingdomPremium.ps1 -SceneOnly -Player
```

Las seis capturas de Editor se guardan en `Artifacts/ArtReview/kingdom-premium/`: `close.png`, `medium.png`, `rts.png`, `worker.png`, `warrior.png` y `hero.png`. El ejecutable genera esas seis vistas y una séptima, `controls.png`, en la subcarpeta `player/`. El [visor HTML local](../../Artifacts/ArtReview/kingdom-premium/index.html) permite alternarlas y compararlas con la entrega anterior y las referencias locales del usuario. La opción «Visor» selecciona el origen «Ejecutable», porque su captura con HUD solo existe allí. Una imagen anterior no sustituye una captura nueva ausente.

La revisión del ejecutable captura cámaras reales de URP a RenderTexture y guarda el resultado con `PlayerSmoke.CaptureTarget`. Comprueba las tres unidades, el encuadre RTS, el espacio libre bajo el HUD, las dimensiones de los PNG y la interacción de controles. El informe `player/review.json` identifica el build y sus referencias a mallas/materiales; el jugador no acredita por sí solo rutas de assets del Editor. No prueba animación, combate ni FPS.

## Evidencia Unity

El bake final genera evidencia nativa de la escena y del build. Los resultados técnicos siguientes no acreditan identidad con las referencias, animación ni rendimiento en PC o móvil.

| Evidencia | Resultado registrado |
| --- | --- |
| [asset-validation.json](../../Artifacts/ArtReview/kingdom-premium/asset-validation.json) | `passed: true`, sin errores; 10-09-2026 a las 20:47:47 UTC, Unity 6000.3.23f1. Tres unidades y 45 registros de malla, incluidos los nueve LOD de personajes, con rutas persistentes, normales y UV. Registra 979 renderers y 76 materiales de escena; son inventario, no draw calls ni tiempos de frame. |
| [build.txt](../../Artifacts/ArtReview/kingdom-premium/build.txt) | `Succeeded`, 0 errores y 2 avisos; 411 064 060 bytes y 25,82 s de compilación. Escena `Assets/Game/Scenes/KingdomPremium.unity`. La duración corresponde al build, no al rendimiento del juego. |
| Seis capturas nativas de Editor | Generadas el 10-09-2026, 20:43:23–25 UTC. `close`, `medium` y `rts`: 2560×1440; `worker`, `warrior` y `hero`: 1200×1600. Las figuras están orientadas de frente en los retratos, con cabeza, pies y equipo dentro de la imagen. La vista RTS muestra las tres figuras y el edificio. |
| [player/review.json](../../Artifacts/ArtReview/kingdom-premium/player/review.json) y siete PNG | `passed: true`, `status: completed`; GUID **`79363a377fe148bca74510c9cc90d7ad`**, 10-09-2026 a las 20:51:35 UTC. Las siete capturas son de 1920×1080, incluidas las tres vistas individuales. Media, RTS y controles contienen las tres figuras completas; cada vista cercana permite inspeccionar su figura seleccionada. Ejecución Windows con NVIDIA GeForce RTX 3060 Ti; no es una medición de FPS. |
| [HUD del ejecutable](../../Artifacts/ArtReview/kingdom-premium/player/controls.png) | `controlsFit` y `controlsBlockWorldInput` verdaderos. Los tres centros y los tres límites completos de las figuras quedan dentro del área libre de 1904×957,6 píxeles; HUD de 92 unidades lógicas en total. Inspección de la imagen confirma que los paneles no cubren las figuras. La ayuda de giro se limita a Cerca; la rueda ajusta el zoom. |
| Configuración del proyecto | Tras el build, `git diff --name-only -- ProjectSettings` no registra cambios en el checkout principal. El proyecto de ejecución usa su copia de settings. |
| Comparación visual R01/R03/R09 | Tres PNG de 1785×892 generados por Chrome desde la sección de comparación de la galería, con las capturas finales de Editor: [Worker](../../Artifacts/ArtReview/kingdom-premium/comparison-worker.png), [Warrior](../../Artifacts/ArtReview/kingdom-premium/comparison-warrior.png) y [Hero](../../Artifacts/ArtReview/kingdom-premium/comparison-hero.png). Imágenes y etiquetas inspeccionadas; no hay retoque de las figuras ni sustitución de capturas. |

Las comparaciones entregadas se reproducen con `node tools/art/capture_kingdom_comparisons.cjs --source editor`. El script pulsa cada selector, comprueba la carga de referencia/anterior/nuevo y captura exclusivamente esa sección HTML. `--check-only` comprueba su estructura sin escribir PNG; `--source player` permite crear comparaciones con las capturas separadas del ejecutable. Los tres PNG de comparación de esta entrega utilizan el origen Editor; no se han renombrado imágenes del ejecutable como capturas de Editor.

## Revisión visual y diferencias pendientes

La comparación usa las figuras Kingdom situadas a la izquierda de R01, R03 y R09. Las láminas completas se conservan como referencia y las otras dos columnas son capturas reales de Unity. También se han inspeccionado [medium.png](../../Artifacts/ArtReview/kingdom-premium/medium.png) y [rts.png](../../Artifacts/ArtReview/kingdom-premium/rts.png) para juzgar la relación entre figuras y entorno.

| Parte | Mejora visible frente a la entrega anterior | Diferencia todavía visible frente a la referencia |
| --- | --- | --- |
| Anatomía y rostro | Cara, ojos, dedos, cuello y extremidades tienen continuidad anatómica y más detalle que las piezas del modelo anterior. | Las tres caras siguen muy emparentadas, con expresión poco definida y ojos destacados. Falta la personalidad, edad y proporción heroica específica de cada figura ilustrada. |
| Worker | El arnés cruza el torso de forma continua; las manos y el agarre de la horca se ven mejor construidos. Camisa, bolsas, sombrero y botas tienen más forma. | El cuerpo mantiene una postura frontal rígida. La camisa y el faldón azul presentan pliegues simples, y el sombrero y cuero se ven demasiado limpios y uniformes frente a R01. |
| Warrior | Escudo, espada, casco, placas y tabardo forman un personaje completo, con bordado visible y mejor relación con el cuerpo. | Hombros y brazos muestran bordes ondulados y reflejos intensos en las placas. El acabado no reproduce la chapa definida, la malla, el peso de las telas ni la postura de R03. |
| Hero | Corona, broches, manto largo con forro rojo, espada y cuello de piel distinguen su equipamiento. El cuello sustituye el anillo simple de la versión anterior. | El cuello continúa siendo poco voluminoso desde la vista frontal frente al armiño de R09. Manto y tabardo caen con rigidez; torso, brazos y rostro siguen demasiado próximos al Warrior. Las placas presentan los mismos contornos blandos y destellos. |
| Materiales e iluminación | Se distinguen acero, tela azul, cuero, madera y piedra. Las figuras reciben la misma luz y sombra que el edificio y suelo. | Piel, paja y cuero conservan una uniformidad marcada; el acero refleja con demasiada fuerza en algunos bordes. El detalle de bordado no reemplaza la construcción y el desgaste de cada prenda. |
| Edificio y paisaje | Piedra, carpintería, huecos, puerta, cubierta, pavimento, árboles, helechos y utilería crean un lugar más desarrollado. La escala entre edificio y figuras se puede evaluar. | El entorno está más resuelto que las figuras. El césped exterior y la transición del sendero conservan repetición y bordes visibles; no constituyen todavía un paisaje acabado al nivel de las referencias. |
| Vista RTS | Las tres figuras y sus roles principales se distinguen dentro del patio, junto con el edificio. | A esta distancia se pierde gran parte del detalle de rostro y material. La lectura de rol no prueba fidelidad artística ni un rendimiento medido. |

La aceptación se rige por la [auditoría Kingdom](kingdom-premium-audit.md) y el [plan vigente](art-overhaul-plan.md). **El resultado no es idéntico a las referencias y la calidad premium solicitada no queda acreditada por esta revisión.** Es una muestra estática sin rig ni animaciones; no presenta pruebas de FPS ni combate terminado. No modifica estadísticas, facciones, separación de PvP histórico/fantástico o ventajas de cosméticos.
