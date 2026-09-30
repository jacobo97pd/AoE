# Colección de personajes en Blender y Unity

Se entregan 26 figuras editables a partir de las diez láminas recibidas: 21 humanos, elfos o enanos, tres dragones bípedos y dos piratas procedentes de los modelos Meshy del proyecto. Las imágenes sirven de referencia; no se proyectan sobre planos para simular personajes tridimensionales.

**Son interpretaciones. El parecido facial, la ropa, los bordados y el movimiento todavía no alcanzan el detalle de las ilustraciones.** Los piratas conservan su geometría y texturas anteriores. Las nuevas figuras usan anatomía, prendas, armaduras y accesorios modelados, un esqueleto y mapas de superficie. Las caídas y carreras se revisan por separado de la calidad artística.

## Archivos y revisión

- Fuentes: `Artifacts/ArtReview/reference-characters/figure_XX/figure_XX.blend`. Texturas empaquetadas; malla editable y acciones de reposo, carrera y caída.
- Paquete Blender: `Artifacts/ArtReview/reference-characters/Personajes-Blender.zip`, 26 archivos Blender y tres documentos, CRC verificado.
- FBX: `Assets/Game/ReferenceCharacters/SourceModels/figure_XX/figure_XX.fbx`.
- Materiales URP Lit: `Assets/Game/ReferenceCharacters/Materials/`.
- Prefabs: `Assets/Game/ReferenceCharacters/Resources/ReferenceUnits/`.
- Escena: `Assets/Game/Scenes/CharacterCollection.unity`.
- Galería de capturas: `Artifacts/ArtReview/reference-characters/index.html`.
- Evidencia de importación: `Artifacts/ArtReview/reference-characters/unity-import-report.json`.
- Evidencia del ejecutable: `Artifacts/ArtReview/reference-characters/player/collection-review.json`.

En el menú principal, **PERSONAJES** abre la galería. Flechas cambian de figura; I reproduce reposo, R carrera y D caída. Las teclas 1, 2 y 3 seleccionan las vistas cercana, media y RTS. La galería conserva la partida offline pausada y vuelve al mismo mundo. No se abre mientras hay una operación online, cola o partida pendiente.

## Correspondencia de figuras

| ID | Figura | Uso en las partidas |
|---|---|---|
| 01 | Corsario de pólvora | Piratas, Navales; se mantiene su prefab previo |
| 02 | Capitán carmesí | Héroe de los Piratas, Navales; conserva su prefab previo |
| 03 | Héroe del reino | Galería y prefab |
| 04 | Héroe del desierto | Galería y prefab |
| 05 | Héroe de montaña | Galería y prefab |
| 06 | Dragón metálico | Aspecto de `ember_drake`, Solar (legado) |
| 07 | Dragón glacial | Aspecto de `ember_drake`, Elfos |
| 08 | Dragón ígneo | Aspecto de `ember_drake`, otras facciones de fantasía |
| 09 | Arquera elfa | `stringwarden`, Elfos |
| 10 | Centinela élfico | `reedguard`, Elfos |
| 11 | Maga elfa | `threadkeeper`, Elfos |
| 12 | Guerrero enano | `reedguard`, Enanos |
| 13 | Minero enano | `tender`, Enanos |
| 14 | Thane enano | `frostguard`, Enanos |
| 15 | Piquero de montaña | `frostguard`, Hombres de las montañas (Fantasía) |
| 16 | Guerrero de montaña, variante | Galería y prefab |
| 17 | Cazador de montaña | `stringwarden`, Hombres de las montañas (Fantasía) |
| 18 | Guerrero del reino | `reedguard`, Franceses / Hispanos / Ingleses (arte del reino compartido) |
| 19 | Guerrero del desierto | `reedguard`, Miraj (legado, fuera del selector) |
| 20 | Guerrero de montaña | `reedguard`, Hombres de las montañas (Fantasía) |
| 21 | Recolector del reino | `tender`, Franceses / Hispanos / Ingleses (arte del reino compartido) |
| 22 | Recolector del desierto | `tender`, Miraj (legado, fuera del selector) |
| 23 | Recolector de montaña | `tender`, Hombres de las montañas (Fantasía) |
| 24 | Jinete del reino, a pie | Galería y prefab |
| 25 | Jinete del desierto, a pie | Galería y prefab |
| 26 | Jinete de montaña, a pie | Galería y prefab |

Hay 17 figuras nuevas asignadas visualmente a roles existentes; algunas se comparten entre facciones. Las estadísticas, costes y reglas siguen en la simulación. Los siete héroes/variantes/jinetes restantes no se anuncian como nuevas unidades reclutables. Los jinetes de las referencias están a pie; faltan sus monturas. Las apariencias equipadas de la tienda conservan su representación anterior, con reconstrucción del visual al cambiar entre apariencia base y equipada.

## Clasificación de facciones actualizada el 14 de septiembre de 2026

El catálogo de revisión y los manifiestos de exportación comparten tres ámbitos. Los hombres de las montañas pertenecen a **Fantasía** (figuras 05, 15, 16, 17, 20, 23 y 26). Las dos figuras piratas de la colección pertenecen a **Navales**; en partida siguen teniendo prioridad los cuatro prefabs Meshy originales y sus animaciones.

**Históricas** ofrece Franceses, Hispanos e Ingleses. Las figuras 18 y 21 son representaciones genéricas del reino compartidas; todavía no aportan vestuario o armamento nacional específico. **Fantasía** ofrece Orcos, Enanos, Hombres de las montañas y Elfos. No existe un modelo orco propio en estas 26 referencias; el troll del proyecto no se considera una entrega de arte orco. Miraj y Solar se conservan como contenido legado y en la galería, fuera de las opciones de nuevas partidas.

**Navales** ofrece Piratas. Marina inglesa, Marina española y Flota esquelética figuran como contenido pendiente, sin modelos, unidades o selección jugable terminados. Las partidas actuales de Piratas usan el mapa costero `sapphire_coast` y movimiento terrestre de desembarco; esta clasificación no añade navegación ni combate de barcos.

Este cambio actualiza identidades y restricciones; no altera mallas, materiales, esqueletos ni clips. Las capturas y pruebas técnicas del 13 de septiembre que aparecen abajo documentan la entrega anterior de arte. La nueva clasificación tiene pruebas específicas de metadatos, selección visual y aislamiento de reclutamiento y snapshots.

## Animación y materiales

Los controladores usan `CorsairAnimationDriver`, con animación esquelética sincronizada con la simulación. `Fall` se importa como `Corsair_Death`, sin bucle; la traslación del personaje pertenece al juego. Las nuevas figuras cuentan con Idle, Run y Fall. Walk reutiliza Run a media velocidad; Attack/Hit usan reposo como transición provisional cuando no existe una acción propia. Por tanto, **no se presentan como terminadas las animaciones de combate, recolección, montura o vuelo**. Los piratas reutilizados conservan sus acciones anteriores.

Cada prefab tiene tres LODs. Los presupuestos y recuentos efectivos están en el manifiesto y el informe de Unity. La fuente Blender puede conservar una malla más densa oculta para editar detalles. Los materiales separan color, normal, metal y rugosidad aproximada mediante smoothness de URP; las texturas se importan con su espacio de color adecuado. La escena guarda iluminación, sombras, suelo y ajuste ACES como recursos persistentes.

## Reproducir la importación

Con los FBX ya exportados y el editor cerrado:

```powershell
./tools/Build-ReferenceCharacters.ps1 -BuildPlayer -CapturePlayer
```

La importación completa exige 26 figuras. `-AllowPartial` sirve exclusivamente para preparar y revisar una exportación intermedia. Las capturas finales se realizan con el ejecutable y comprueban entrada de estados y deformación real; no equivalen a una aprobación estética. Las pruebas `ReferenceCharacterIntegrationTests` verifican los prefabs, LODs, mapas, dominios y animación; `ReferenceCharacterNavigationTests` verifica la conservación de la partida al entrar y salir de la galería.

## Resultado verificado el 13 de septiembre de 2026

La compilación Windows `05a91cf2371d432293b419110de45b34`, Unity 6000.3.23f1, generó **156 capturas nativas a 1600 × 900** en una RTX 3060 Ti: seis por figura. Las 26 figuras entran en Idle/Run/Death y deforman su malla real. El informe nativo termina con `passed: true`; esto acredita funcionamiento técnico, no semejanza artística ni rendimiento con grandes ejércitos.

La revisión final usa el render de escritorio del proyecto, suavizado SMAA, reflejos de entorno, sombras de contacto y LOD0 en cerca/media; RTS vuelve a la selección automática de LOD. Al volver al juego se restaura su render y se libera la caché de prefabs de la galería.

| Comprobación | Resultado | Evidencia |
|---|---|---|
| Fuentes, SHA-256, pesos, mapas y Blender editables | 26/26 | `source-inventory.json`, `source-validation.json` |
| Prefabs, materiales, LOD, esqueleto y deformación importada | 26/26 | `unity-import-report.json` |
| Capturas y animación dentro del ejecutable | 26/26, 156 PNG | `player/collection-review.json` |
| Integración de modelos y navegación de galería | 13/13 | `D:/CodexTooling/reference-characters/unity-acceptance-verified.xml` |
| Navegación y restauración del render tras el ajuste visual final | 7/7 | `D:/CodexTooling/reference-characters/unity-gallery-final.xml` |
| Regresiones de los piratas anteriores | 17/17 | Suites Corsair/PirateCrew en `D:/CodexTooling/reference-characters/unity-acceptance.xml` |

Las versiones iniciales de la prueba de cosméticos tenían un mapa de prueba sin partida central válida; se corrigió la preparación del caso, manteniendo las comprobaciones sobre apariencia y estado autoritativo. La prueba final de navegación detectó que restaurar el render solo al destruir la escena llegaba tarde; ahora se restaura antes de reactivar la partida.

Para liberar C: se conservaron y trasladaron a D: los recursos pesados de esta colección, revisiones anteriores y cachés de Unity, pip y NuGet mediante enlaces de directorio. Las rutas originales siguen funcionando. El ZIP Blender incluye sus texturas y no depende de esos enlaces para abrirse en otro ordenador.
