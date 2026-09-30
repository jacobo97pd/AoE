# Auditoría del arte actual — punto de partida para ArtStyleLab

Fecha: 10 de septiembre de 2026. Esta auditoría registra la alfa existente **antes de crear los nuevos assets del laboratorio**. Su alcance inmediato es un Tender y un Reedguard de Aven; la aprobación visual corresponde al checkpoint humano 2. Las referencias son dirección artística, no sprites que sustituyan modelos ni autorización para producir todo el roster.

## Estado comprobado

| Área | Situación actual | Consecuencia para el laboratorio |
| --- | --- | --- |
| Editor | Unity `6000.3.23f1` (`09d2ecc7fb28`). | Reutilizar la instalación existente. |
| Pipeline | URP `17.3.0`; Input System `1.20.0`, uGUI `2.0.0`, Test Framework `1.6.0`. | Mantener URP y la estructura del proyecto. |
| Pipeline activo | `GraphicsSettings` y ambas calidades apuntan a `Mobile_RPAsset`. `PC_RPAsset` existe, pero no es el pipeline efectivo. | No asumir que la etiqueta PC activa un renderer diferente. |
| Renderizado | Forward, SRP Batcher activado, batching dinámico desactivado, sin renderer features. HDR, escala 1, MSAA 2; no requiere texturas globales de profundidad u opaco. | Un shader compartido y opaco es la primera opción; evaluar cada coste adicional. |
| Sombras | Luz principal, mapa de sombras 2048, dos cascadas, distancia 80, sombras suaves. Sin sombras de luces adicionales. | Mantener una luz direccional y encuadrar dentro del alcance real. |
| Modelos | Recetas C# originales: `AlphaMeshBuilder`, `AlphaWorldGeometry`, `AlphaExpandedGeometry`, `AlphaBiomeGeometry`. Mallas combinadas con colores por vértice. | Reutilizar identificadores y escala, pero sustituir la geometría humana del laboratorio. |
| Inventario visual | 16 IDs de unidades/transporte/asedio, 12 de edificios y ocho vocabularios de facción. Las reglas restringen combinaciones disponibles. | El número de fichas del atlas no equivale a esculturas únicas. |
| Prefabs y archivos DCC | En la línea base inspeccionada no hay `.fbx`, `.obj`, `.blend`, prefabs de unidad, `.anim` ni controladores Animator. Las vistas se instancian mediante fábricas. | El laboratorio necesita assets persistentes, rig y referencias verificables; los prototipos actuales no ofrecen esa cadena. |
| LOD | Dos mallas compartidas por combinación, transiciones `.06` y `.008`, sin crossfade en esos grupos. Cache público máximo de 480 mallas, más tres proyectiles. | Incorporar LOD0/1/2 a los dos candidatos y validar siluetas; el límite de cache no demuestra rendimiento GPU. |
| Animación | `WorldView` aplica bob, inclinación de trabajo/carga, ataque y ascenso mediante transforms. Alas y rastrillo tienen transforms propios. | No existe aún animación esquelética de personajes. Crear un rig mínimo y locomoción/acciones legibles en el laboratorio. |
| Cámara real | Ortográfica, inclinación 55°, yaw inicial 35°, foco a distancia 55, zoom 5–22, movimiento limitado a la huella del mapa. | La vista RTS normal, a escala común, será la referencia de aceptación. |
| Iluminación | `AlphaLighting` usa sol cálido, ambiente tricolor más frío y variantes por bioma. | Preservar una base legible de luz diurna; separar color de material de iluminación. |
| Postprocesado | ACES, FXAA, exposición `.06` desierto / `.20` otros, contraste 9 o 12 fantástico, saturación 8 o 10 costa, bloom `.13` / `.19` fantástico, vignette `.14`. | Ajustar con moderación; bloom o grading no sustituyen anatomía, rig o materiales. |

Fuentes comprobadas: `ProjectSettings/ProjectVersion.txt`, `Packages/manifest.json`, `ProjectSettings/GraphicsSettings.asset`, `ProjectSettings/QualitySettings.asset`, `Assets/Settings/Mobile_RPAsset.asset`, `Mobile_Renderer.asset`, y código de presentación. La configuración publicada no constituye una medición de dispositivo.

## Calidad visual frente a las referencias

Se han comparado directamente el trabajador de la referencia `(1)` y el guerrero de la referencia `(3)` —archivos `ChatGPT Image 10 sept 2026, 18_03_48 …` de Downloads— con las capturas nativas del [Tender](visual-atlas/images/unit-aven-tender.png) y el [Reedguard](visual-atlas/images/unit-aven-reedguard.png). El análisis del conjunto de imágenes se documenta por separado en `REFERENCE_IMAGE_ANALYSIS.md`.

La alfa actual ofrece siluetas reconocibles, colores de propietario y objetos 3D reales, pero sus cuerpos son prismas rígidos, los rostros casi no tienen rasgos, manos y botas son bloques y los materiales se diferencian principalmente por color. Es una línea base de geometría facetada muy simplificada. **Todavía no alcanza el objetivo de RTS 3D estilizado premium.**

Las referencias combinan cabezas y manos legibles, hombros sólidos, postura con peso, ropa en capas, herramientas amplias y superficies claramente distintas: tela mate, cuero, metal con reflejos y madera. El objetivo es trasladar esas masas y materiales, no reproducir microbordados, poros, símbolos o personajes reconocibles. Los detalles que desaparecen a 40–120 píxeles de altura tienen menor prioridad.

- **Tender:** conservar sombrero, herramienta y carga como identificadores. Rehacer torso, cabeza, brazos, manos, piernas, botas y contacto con la herramienta; añadir ropa superpuesta, cuero y metal legibles. El pack no debe ocultar la silueta humana ni dominarla desde arriba.
- **Reedguard:** conservar su identidad de lancero, arma alta y escudo; la referencia de guerrero aporta proporciones, ropa y metal, sin cambiar su rol de combate. Rehacer casco, cara, hombros, brazos, faldón, grebas y agarres. Diferenciar claramente acero, tejido y correas.
- **Ambos:** probar azul, rojo, verde y amarillo sobre una máscara de equipo separada de la paleta de facción. Usar insignias originales y evitar trasladar literalmente la heráldica de las láminas.

## Shaders, materiales y cosméticos

La línea base contiene `FacetedTeam`, `AmberSurface`, `AlphaWater` y `FogOfWar`, junto a `FacetedTeam.mat` y el material auxiliar `Greybox.mat`. La mayor parte del mundo comparte el material de colores por vértice. La máscara de propietario está en alpha del vértice y se combina con `_TeamColor` mediante `MaterialPropertyBlock`.

`FacetedTeam` resuelve color, iluminación estilizada, sombras, cambios de paleta y el modo de iluminación de estudio de la tienda. No existe una biblioteca de materiales de personaje con mapas de base, normales, metalicidad y rugosidad que reproduzca la riqueza de las referencias. Los assets del mundo no contienen atlases PBR autorados. La ilustración del menú tiene procedencia separada y no es una textura de personaje.

Se conserva la separación ya funcional entre definición de juego y aspecto: `CosmeticLoadout` selecciona apariencias y `ApplyCosmetic` modifica propiedades de presentación. No duplicar vida, ataque, radio ni costes dentro de assets visuales; el laboratorio no integrará nuevos modelos en partidas antes de la revisión humana. El shader del laboratorio debe permitir material base, metalicidad/suavidad, máscara de equipo, tinte de facción y feedback controlado. Normales y rim son opcionales y deben justificar su coste visual.

## Facciones y biomas que se conservan

| Universo PvP | Facciones actuales |
| --- | --- |
| Histórico | Aven Compact, Serevin March, Miraj Sultanate, Skeld Clans |
| Fantástico | Solar Kingdom, Verdant Covenant, Ashen Dominion, Drakeforged Clans |

No pueden enfrentarse universos distintos. Dragones, trolls, guardianes, leones y elefantes entrenables son ejército; sus skins no cambian estadísticas. Los ejemplos de piratas, enanos y elfos del nuevo prompt son dirección para fases posteriores, no nuevas facciones implementadas en este checkpoint.

Los mapas existentes son Amber Crossing/bosque (96×72), Sapphire Coast/caribe (104×80) y Sunscar Basin/desierto (104×80). `AmberBiome` genera suelo plano con color continuo; `AlphaEnvironment` genera agua, vegetación, rocas y monumentos. El color del terreno no introduce alturas de navegación. La iluminación, recursos y decoración usan `BiomeId`; la niebla controla la presentación.

Las inspecciones del [atlas actual](visual-atlas/index.html) expusieron dos deudas reales: adornos fuera del suelo en los bordes y agua gris en costa/oasis. En el agua, las UV de los biomas usan coordenadas de mundo mientras el cálculo de orilla del shader espera una coordenada transversal normalizada. No son razones para alterar mapas o gameplay durante la prueba de los dos personajes.

## Qué reutilizar y qué sustituir

**Reutilizar:** Unity/URP, separación Simulation–Presentation, IDs de Tender/Reedguard, colores y selección de propietario, cámara táctica, pruebas de aislamiento cosmético, fábricas antiguas como comparación, terreno y vegetación representativos, estructuras de documentación/procedencia y herramientas de capturas. Mantener disponibles todos los assets actuales.

**Sustituir primero dentro de ArtStyleLab:** mallas humanas facetadas, ausencia de rasgos/agarres, superficies uniformes y movimiento de cuerpo rígido. Producir solo los dos candidatos, con rig, materiales compartidos, animación mínima y LOD verificables. No recolorear el roster entero y presentarlo como cambio de calidad.

**Diferir:** caballería nueva, piratas, razas fantásticas, héroes, familia de dragones, variantes de bioma, animaciones comerciales y renovación global del paisaje. Requieren aprobar antes la dirección visible en la cámara de juego.

## Pipeline propuesto y herramientas disponibles

No se ha encontrado Blender en PATH, directorios habituales de Blender Foundation, App Paths/registro de instalación, entradas de desinstalación ni rutas estándar de Steam/Scoop comprobadas. No se ha instalado software. Esto significa **Blender no localizado en las rutas comprobadas**, no una afirmación de búsqueda exhaustiva de todos los discos.

Para este checkpoint puede generarse geometría nativa de Unity con pesos, bindposes y rig explícitos, guardarla como assets ordinarios y crear prefabs/controladores reproducibles mediante un baker de editor. Es un prototipo técnico con fuente editable, no equivalente a una escultura final, un rig facial ni un pipeline Blender/FBX terminado.

1. Extraer proporciones, paleta y jerarquía de lectura de las referencias.
2. Construir un blockout común a escala real; comparar Tender y Reedguard juntos desde +Z y cámara RTS.
3. Crear mallas de detalle medio con normales coherentes, zonas de material y máscara de equipo; reservar UV/atlas donde aporte valor.
4. Añadir rig mínimo con locomoción en el sitio y acciones legibles; ninguna animación concede daño o recursos ni mueve la posición autoritativa.
5. Generar LOD0/1/2 y prefabs con referencias persistentes; registrar triángulos, huesos, influencias, materiales y texturas reales.
6. Capturar Close/Mid/RTS en `Artifacts/ArtReview/`, con escala y encuadre comparables. La vista normal decide legibilidad; una vista de estudio no demuestra paridad durante partida.
7. Ejecutar validadores y pruebas focalizadas, documentar carencias y parar en checkpoint humano 2.

## Riesgos móviles y límites de la evidencia

- Los nuevos personajes requieren medir skinning, huesos, materiales y sombras, además de triángulos. Un único `SkinnedMeshRenderer` no implica un único coste barato ni instancing automático.
- Mantener un shader y pocos materiales compartidos. Las propiedades por instancia, keywords y variantes pueden afectar batching; verificar en Frame Debugger/Profiler cuando exista el prototipo.
- HDR, MSAA2, FXAA, bloom y sombras suaves de dos cascadas consumen tiempo y memoria. La combinación actual no demuestra que soporte cientos de unidades nuevas.
- Evitar transparencias de tela/pelo, VFX superpuestos, normales costosas sin beneficio visible y texturas máximas únicas por personaje. Empezar con atlas compartido 512–1024 si hace falta, con mipmaps y formatos de plataforma posteriormente validados.
- Configurar límites de skinning y bounds para evitar desaparición o cálculo fuera de pantalla; verificar clipping durante ataque y locomoción en todos los LOD.
- URP puede sincronizar `QualitySettings.antiAliasing` al inicializarse. Las herramientas de exportación deben restaurar cualquier configuración que cambien y no dejar ajustes de proyecto derivados de una captura.

Las pruebas y capturas de PC anteriores validan aspectos concretos de reglas, presentación y composición. No existen aquí resultados de GPU en tiempo real ni validación en móvil/tablet físico para estos candidatos. Los objetivos de 30–60 FPS y 50/100/200/300 unidades permanecen como tareas de medición posteriores; no se deducen de las capturas ni de la configuración.

## Documentación y validación focalizada

Documentos reutilizables: [ART_BIBLE.md](ART_BIBLE.md), [ASSET_PIPELINE.md](ASSET_PIPELINE.md), [ORIGINAL_PROVENANCE.md](ORIGINAL_PROVENANCE.md), [ALPHA_ASSET_PROVENANCE.md](ALPHA_ASSET_PROVENANCE.md), [ALPHA_03_WORLD_ART.md](ALPHA_03_WORLD_ART.md), [UI_UX_GUIDE.md](UI_UX_GUIDE.md) y [atlas visual](visual-atlas/README.md). Los informes de fases antiguas describen sus propios cortes de evidencia y no prueban la calidad del nuevo laboratorio.

Pruebas existentes útiles: `Assets/Tests/PlayMode/AlphaWorldArtTests.cs`, `ArtKitTests.cs`, `FrontierBiomeArtTests.cs`, `FrontierPresentationTests.cs`, `CosmeticShaderRenderTests.cs` y `CameraRotationTests.cs`. Las de shader comprueban píxeles reales; cambiar una propiedad sin comprobar la imagen no basta.

Para el laboratorio, limitar las nuevas comprobaciones a referencias válidas de prefab, mallas y materiales; LOD presentes y decrecientes; rig, bindposes y pesos coherentes; clips/controlador válidos y sin root motion de gameplay; máscara de equipo que cambie realmente el render; bounds que contengan las poses; y escena con cámaras Close/Mid/RTS. No hace falta repetir toda la simulación si no se integra ni se modifica gameplay. El inventario medido y el resultado visual requieren la ejecución de Unity del checkpoint, todavía pendiente al redactar este punto de partida.

`Emberfield.Tests.EditMode` tiene `noEngineReferences: true` y referencia únicamente Simulation: las nuevas pruebas de prefab/AssetDatabase deben ir en una assembly de arte Editor-only separada. `Emberfield.Tests.PlayMode` ya referencia Presentation y sirve para pruebas de render/animación focalizadas. El aplicador cosmético actual recorre `MeshRenderer`; un candidato con `SkinnedMeshRenderer` necesita un adaptador visual del laboratorio que opere sobre `Renderer`, sin modificar por ello la integración del ejército existente.
