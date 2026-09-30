# Revisión de la renovación artística

Fecha: 10 de septiembre de 2026. Comparación con la línea base `7ab2c89bbaadadf414c8042ea4743ef5cb0b1019` y las trece referencias aportadas. **Cuatro escenas generadas; assets, ejecutable y galería verificados.** Este informe utiliza las capturas finales, el validador de las **18:50:05 UTC** y las ocho revisiones Windows de las **18:51:01–18:53:11 UTC**.

## Resultado visible y límite de la entrega

La muestra pasa de dos figuras sobre un suelo de estudio a cuatro entornos 3D con edificios, caminos, agua, vegetación, utilería y personajes de varias anatomías. Los cambios más claros son el contexto de asentamiento, la separación de biomas, la caballería con montura, el enano y el dragón, además de ropa, equipamiento y superficies más trabajadas.

**La mejora todavía no alcanza el acabado de las referencias.** Las figuras conservan construcción procedural visible, varios rostros y poses se repiten, y los asentamientos comparten una composición muy parecida. Este informe no afirma calidad comercial alcanzada, paridad conceptual ni preparación para producción. La integridad técnica y la valoración artística se evalúan por separado.

Abrir la [galería de revisión](../../Artifacts/ArtReview/overhaul/index.html). Sus 29 imágenes son renders nativos de escenas Unity: doce vistas y 17 retratos. El atlas nuevo contiene superficies aplicadas a geometría real; no contiene personajes, edificios o escenarios que sustituyan las mallas. Las capturas del editor emplean una pose horneada temporalmente desde el rig para renderizarla de forma reproducible. Las del [ejecutable](../../Artifacts/ArtReview/overhaul/player/kingdom-rts.png) utilizan SkinnedMeshRenderer y Animator en ejecución. Ambas son inspecciones artísticas dentro de Unity; no son capturas de una partida multijugador.

## Contenido persistente y cámaras

| Escena | Personajes | Vistas del editor |
| --- | --- | --- |
| Kingdom | Recolector, guerrero, jinete, héroe y guerrero montañés. | [Cerca](../../Artifacts/ArtReview/overhaul/kingdom-close.png) · [Media](../../Artifacts/ArtReview/overhaul/kingdom-medium.png) · [RTS](../../Artifacts/ArtReview/overhaul/kingdom-rts.png) |
| Caribbean | Recolector, pirata, héroe y guerrero. | [Cerca](../../Artifacts/ArtReview/overhaul/caribbean-close.png) · [Media](../../Artifacts/ArtReview/overhaul/caribbean-medium.png) · [RTS](../../Artifacts/ArtReview/overhaul/caribbean-rts.png) |
| Desert | Recolector, guerrero, jinete y héroe. | [Cerca](../../Artifacts/ArtReview/overhaul/desert-close.png) · [Media](../../Artifacts/ArtReview/overhaul/desert-medium.png) · [RTS](../../Artifacts/ArtReview/overhaul/desert-rts.png) |
| Fantasy | Elfo, enano, dragón y héroe. | [Cerca](../../Artifacts/ArtReview/overhaul/fantasy-close.png) · [Media](../../Artifacts/ArtReview/overhaul/fantasy-medium.png) · [RTS](../../Artifacts/ArtReview/overhaul/fantasy-rts.png) |

Son **17 prefabs de nueve arquetipos**, tres LOD por prefab y tres clips por controlador: Idle, Walk y Action. Existen **51 mallas de personaje y 51 clips**, sin contar mallas del entorno. Las variantes reutilizan cuerpos, accesorios y patrones de animación: esas cifras no significan 17 esculturas independientes ni 51 animaciones diseñadas individualmente. Los rigs tienen 16 huesos en humanoides, 28 en jinete/montura y 24 en dragón.

Los retratos siguen el patrón `<biome>-<kind>.png`. Ejemplos para inspeccionar anatomía y vestuario: [héroe](../../Artifacts/ArtReview/overhaul/kingdom-hero.png), [caballería](../../Artifacts/ArtReview/overhaul/kingdom-cavalry.png), [montañés](../../Artifacts/ArtReview/overhaul/kingdom-mountainwarrior.png), [pirata](../../Artifacts/ArtReview/overhaul/caribbean-pirate.png), [enano](../../Artifacts/ArtReview/overhaul/fantasy-dwarf.png), [elfo](../../Artifacts/ArtReview/overhaul/fantasy-elf.png) y [dragón](../../Artifacts/ArtReview/overhaul/fantasy-dragon.png). Pirata y trabajador Caribbean se ven despejados: las palmeras ya no ocultan sus figuras principales.

Las unidades comparten `PaintedSurface.mat` y `SurfaceAtlas.png`. El PNG fuente mide **1254×1254**; Unity lo importa a **2048×2048** con mipmaps. El atlas contiene 4×4 regiones de superficie. Ampliar la importación no añade detalle artístico a la fuente. Follaje, cristales y agua utilizan materiales de entorno separados.

## Las siete críticas: mejora observada y brecha restante

Las referencias R01–R13 están identificadas y enlazadas en [reference-gap-analysis.md](reference-gap-analysis.md#registro-de-las-trece-referencias). La comparación anterior al trabajo permanece en ese documento; esta tabla evalúa las imágenes nuevas.

| Crítica | Mejora que se ve | Brecha que sigue visible |
| --- | --- | --- |
| **1. Modelos demasiado simples.** | [Kingdom Close](../../Artifacts/ArtReview/overhaul/kingdom-close.png) muestra placas superpuestas, cuello, correas, ropa y bordes de escudo. El [dragón](../../Artifacts/ArtReview/overhaul/fantasy-dragon.png) incorpora pecho más robusto, placas, garras, cuernos y volúmenes de cejas/mandíbula. | Rostros, manos, botas y articulaciones siguen siendo angulares. Caballo y dragón necesitan transiciones anatómicas más naturales; membranas, dedos del ala y articulaciones del dragón son más rígidos que en R06. Añadir placas no resuelve por sí solo la forma del animal. |
| **2. Siluetas débiles.** | Sombrero/herramienta, lanza/escudo, montura, capa/corona, barba/mazo, arco y alas diferencian los nueve tipos. [Fantasy Medium](../../Artifacts/ArtReview/overhaul/fantasy-medium.png) muestra tres estructuras corporales distintas. | Muchos humanos comparten torso, rostro y postura. Héroe y guerrero están próximos visualmente; pirata y héroe Caribbean son especialmente parecidos. Desde RTS, los humanos pequeños dependen mucho del arma, tocado y color para distinguirse. |
| **3. Materiales planos.** | Tela, madera, mampostería, teja, piedra y arena tienen textura; acero, cuero, paño y agua responden de manera diferente. El [héroe](../../Artifacts/ArtReview/overhaul/kingdom-hero.png) permite revisar esa separación. | Se repiten patrones de suelo y ropa. La tela gana textura sin una construcción equivalente de pliegues; metal y piel continúan uniformes en áreas amplias. Falta tratamiento de bordes y detalle de superficie comparable a R01/R03/R09. No existe un texturizado completo de normales autoradas por personaje. |
| **4. Iluminación de prototipo.** | Sombras y contacto asientan figuras, edificios y árboles. Luz ambiental, reflejos y paletas cálidas/frías producen más volumen que la [línea base](../../Artifacts/ArtReview/rts.png). | El esquema diurno es parecido entre escenas. Fantasy conserva zonas verde oscuro y sombra, con menos luminosidad, atmósfera y profundidad que R11. SSAO, niebla y grading no acreditan por sí solos dirección de luz terminada. |
| **5. Entorno vacío.** | [Kingdom RTS](../../Artifacts/ArtReview/overhaul/kingdom-rts.png) contiene castillo, aldea, cultivos, molino, río y puente. Caribbean añade playa, puerto y barco; Desert, oasis y cúpulas; Fantasy, árbol sagrado, pabellones y cristales. | Se repiten módulos, distribución de viviendas, camino y fila de unidades. Falta la densidad local, topografía y variedad de R10–R13. La unión del castillo Kingdom con ribera/cascada es abrupta; raíces Fantasy y bordes del camino Caribbean revelan geometría simplificada. |
| **6. Cámara que no presenta bien el estilo.** | Tres distancias por bioma y retratos dedicados permiten inspeccionar forma, material y entorno. Caribbean Close está despejado; la cámara RTS incluye las cimas del castillo, ciudadela y santuarios. Con controles abiertos, todos los grupos revisados caben en el espacio libre entre paneles. | Close prioriza una figura y recorta personajes secundarios, montura o ala en el borde: ese contexto no sustituye su retrato individual completo. RTS reduce mucho el detalle humano; reservar espacio al HUD aleja más el escenario. Varias composiciones siguen siendo equivalentes entre biomas. |
| **7. Personajes poco ricos frente a los conceptos.** | Hay capas, corona, cuello de piel, barba, arco/carcaj, arreos, vestuario desértico y equipo de montaña. [Montañés](../../Artifacts/ArtReview/overhaul/kingdom-mountainwarrior.png) y [pirata](../../Artifacts/ArtReview/overhaul/caribbean-pirate.png) tienen accesorios específicos visibles. | R01–R09 muestran más carácter, anatomía, ropa ligada al oficio, agarres y gesto. El trabajador costero lleva una herramienta genérica, sin actividad portuaria visible. Caras y manos se repiten; Idle/Walk/Action son una primera biblioteca, con poses demostrativas y movimiento todavía por refinar. |

## Lectura por bioma

**Kingdom.** Río, puente, molino, cultivos, carretas y aldea producen un lugar reconocible. Piedra clara, techos azules/rojos y trigo diferencian superficies. El castillo aporta jerarquía, pero su apoyo sobre la ribera y la cascada necesita una transición de terreno más convincente. Bosque, desniveles y asentamiento no alcanzan la articulación espacial del valle de R10. Los cinco personajes forman una fila de muestra, con poca relación gestual con el trabajo o la guarnición.

**Caribbean.** Agua turquesa, arena, palmeras, muelle de pilotes y barco 3D hacen inmediata la identidad costera. El barco es decorativo y no demuestra una mecánica naval nueva. Las frondas corregidas dejan ver a los personajes, aunque su masa vegetal es poco exuberante frente a R12; quedan árboles templados y viviendas similares a Kingdom. [Medium](../../Artifacts/ArtReview/overhaul/caribbean-medium.png) permite ver el borde poligonal del sendero y la repetición de utilería que restan naturalidad al puerto.

**Desert.** Arena, cúpulas turquesas, arenisca, toldos, oasis y obelisco forman una paleta coherente. Rocas y ondulaciones aportan relieve, pero se repiten y no construyen los estratos, dunas y mercado de R13. La caballería utiliza caballo: no se presenta como camello ni transforma una decoración de caravana en unidad integrada.

**Fantasy.** Enano, elfo y dragón añaden diversidad corporal; pabellones curvos, cristales y árbol grande distinguen el entorno. El dragón tiene pecho, escamas/placas, garras, mandíbula y alas reales, con más detalle en el retrato que en la vista táctica. Sus uniones y membranas siguen siendo rígidas. Las raíces parecen troncos cilíndricos colocados alrededor del árbol y la copa repite masas; árbol, agua y arquitectura tienen menos integración orgánica y riqueza luminosa que R11.

## Integridad y conteos de assets

El [validador de assets](../../Artifacts/ArtReview/overhaul/asset-validation.json) registra **`passed: true`, 17 prefabs, nueve arquetipos, cuatro escenas y cero errores**. Comprueba persistencia, tres LOD decrecientes, UV de superficie y atlas, material compartido, pesos normalizados, índices, bindposes, huesos, máscara de equipo y datos visuales sin estadísticas de juego. En instancias aisladas verifica el reposo de las tres mallas mediante BakeMesh y cuatro poses de cada clip: **204 poses muestreadas**, con movimiento de geometría y huesos ponderados mientras las raíces permanecen estables. Las **68 comprobaciones de cuatro colores** mediante MaterialPropertyBlock conservan malla, material e identidad.

Queda un aviso no bloqueante: **Elf LOD0 incluye 36 triángulos minúsculos/degenerados** según la tolerancia del validador. Ese exceso de geometría se declara aunque no impida renderizar ni supere las comprobaciones de integridad restantes.

| Prefab | Triángulos LOD0 | LOD1 | LOD2 | Huesos |
| --- | ---: | ---: | ---: | ---: |
| Kingdom / Worker | 7210 | 4114 | 1356 | 16 |
| Kingdom / Warrior | 8516 | 4766 | 1332 | 16 |
| Kingdom / Cavalry | 13136 | 7526 | 2578 | 28 |
| Kingdom / Hero | 9446 | 5196 | 1540 | 16 |
| Kingdom / MountainWarrior | 8610 | 4626 | 1252 | 16 |
| Caribbean / Worker | 5858 | 3304 | 1038 | 16 |
| Caribbean / Pirate | 7270 | 3932 | 1204 | 16 |
| Caribbean / Hero | 7270 | 3932 | 1204 | 16 |
| Caribbean / Warrior | 9176 | 5122 | 1502 | 16 |
| Desert / Worker | 5858 | 3304 | 1038 | 16 |
| Desert / Warrior | 6218 | 3420 | 1054 | 16 |
| Desert / Cavalry | 10798 | 6140 | 2260 | 28 |
| Desert / Hero | 6382 | 3516 | 1124 | 16 |
| Fantasy / Elf | 5954 | 3294 | 1108 | 16 |
| Fantasy / Dwarf | 8040 | 4326 | 1474 | 16 |
| Fantasy / Dragon | 16080 | 7908 | 3576 | 24 |
| Fantasy / Hero | 9834 | 5436 | 1690 | 16 |

El JSON conserva vértices, bindposes y errores numéricos por malla. Estos son conteos de assets, no triángulos dibujados por frame. Warrior Kingdom/Caribbean, MountainWarrior y Dwarf superan la banda orientativa de infantería de 4000–8000 triángulos LOD0 del [plan](art-overhaul-plan.md#presupuestos-y-medición). Varios LOD1 superan también el techo del antiguo laboratorio de dos humanos. El validador de esta ampliación comprueba reducción e integridad: **no impone aquellos techos ni certifica un presupuesto móvil**. Falta una decisión de reducción respaldada por perfilado.

## Ejecutable, capturas y medición

El [build Windows final](../../Artifacts/ArtReview/overhaul/build.txt) registra **Succeeded, cero errores y dos avisos**. Incluye las cuatro escenas. Las **ocho revisiones** registran `completed` y `passed: true`, con el mismo identificador de build **`95d270a8742e424d8d88e35c3fd0e12d`**. No se reutilizan resultados del laboratorio anterior de dos unidades. El recorrido automatizado activa la ejecución en segundo plano antes de cargar el bioma, evitando que una ventana oculta pause esa transición.

| Revisión del player | Unidades | Estados de animación | Cambios de equipo | Figuras completas en RTS |
| --- | ---: | ---: | ---: | ---: |
| [Kingdom](../../Artifacts/ArtReview/overhaul/player/kingdom-review.json) | 5 | 15 | 15 | 5 |
| [Caribbean](../../Artifacts/ArtReview/overhaul/player/caribbean-review.json) | 4 | 12 | 12 | 4 |
| [Desert](../../Artifacts/ArtReview/overhaul/player/desert-review.json) | 4 | 12 | 12 | 4 |
| [Fantasy](../../Artifacts/ArtReview/overhaul/player/fantasy-review.json) | 4 | 12 | 12 | 4 |

Las cuatro ejecuciones de la tabla usan Windows a **1920×1080**, con **NVIDIA GeForce RTX 3060 Ti** y Unity **6000.3.23f1**. Verifican Idle/Walk/Action durante 30 frames por estado y unidad: los 51 estados mueven huesos y la deriva máxima registrada de raíz es **0**. Medium y RTS contienen los 17 personajes de sus respectivos grupos.

Se repite el recorrido completo de Kingdom y Fantasy a **1280×720** y **1440×1080**. Los cuatro informes adicionales también pasan: [Kingdom 720p](../../Artifacts/ArtReview/overhaul/phone/kingdom-review.json), [Fantasy 720p](../../Artifacts/ArtReview/overhaul/phone/fantasy-review.json), [Kingdom 4:3](../../Artifacts/ArtReview/overhaul/tablet/kingdom-review.json) y [Fantasy 4:3](../../Artifacts/ArtReview/overhaul/tablet/fantasy-review.json). Entre las ocho ejecuciones suman **105 estados y 105 cambios de equipo** comprobados; todas registran deriva de raíz 0, controles dentro del área segura y bloqueo de entrada dirigida al mundo desde la UI. Estas resoluciones se ejecutan en Windows: no son pruebas de teléfono/tablet físicos.

La cámara RTS conserva inclinación **55°** y orientación **35°**. Ajusta centro y tamaño ortográfico según los límites de arquitectura y unidades. Con el HUD visible aplica además margen para los paneles; las capturas limpias mantienen su encuadre propio. Cada JSON guarda posición y ángulos exactos.

| Bioma | Tamaño ortográfico RTS limpio | Con HUD a 1920×1080 | Centros / figuras completas fuera del HUD |
| --- | ---: | ---: | ---: |
| Kingdom | 24,025 | 34,670 | 5 / 5 |
| Caribbean | 22,000 | 31,748 | 4 / 4 |
| Desert | 22,968 | 33,145 | 4 / 4 |
| Fantasy | 22,621 | 32,645 | 4 / 4 |

Las ocho revisiones registran `hudFramingApplied`, `hudPanelMeasurementValid` y `selectedBoundsInFreeArea` verdaderos. Todos sus personajes tienen centro y límites completos dentro del área libre: cinco en Kingdom y cuatro en los demás biomas. La medición usa el diseño de los paneles y su escala de pantalla, por lo que no depende de transformaciones pendientes al pasar el Canvas a modo cámara. También verifica que el área libre descuente realmente las alturas de cabecera y pie, más el margen.

| Resolución Windows | Área libre entre paneles, píxeles | Tamaño con HUD Kingdom / Fantasy |
| --- | --- | --- |
| 1920×1080 | 1904×748,4 | 34,670 / 32,645 |
| 1280×720 | 1264×493,6 | 35,044 / 32,997 |
| 1440×1080 | 1424×790,7 | 32,816 / 30,899 |

La inspección de las imágenes nativas de [Kingdom 720p](../../Artifacts/ArtReview/overhaul/phone/kingdom-controls.png) y [Fantasy 4:3](../../Artifacts/ArtReview/overhaul/tablet/fantasy-controls.png) confirma que sus unidades quedan por encima del panel inferior y completas. El solapamiento observado en el build anterior está corregido en estas vistas. El coste visual es un escenario más alejado mientras el HUD está abierto; la figura humana pierde detalle respecto a Medium o Close. Los conteos de malla/frustum y área libre no comprueban oclusión por otros objetos del mundo ni sustituyen esta inspección visual.

Cada ejecución guarda cinco PNG: Close, Medium, RTS, Action y controles, mediante cámara URP activa y RenderTexture con skinning en ejecución. Son **40 capturas del player**, además de las 29 del editor. Para su propia captura, el HUD pasa temporalmente a Canvas de cámara. Ejemplos: [acción Kingdom](../../Artifacts/ArtReview/overhaul/player/kingdom-action.png) y [controles Caribbean](../../Artifacts/ArtReview/overhaul/player/caribbean-controls.png).

**Draw calls y triángulos por frame: no disponibles en los ocho informes.** Se muestrean durante tres frames regulares, después de dos frames de drenaje tras las capturas explícitas; cada vista registra si el HUD estaba visible. Todas las muestras fueron cero; los informes marcan `available: false`, valores `-1` y su motivo. Esos ceros no significan una escena gratuita ni una medición válida de rendimiento. No hay datos calibrados de FPS, memoria, temperatura, carga con 50–300 unidades o dispositivo móvil físico.

La [prueba de galería](../../TestResults/overhaul-gallery-check.json) pasa **122 comprobaciones y 29 imágenes**, sin red, con viewport de escritorio **1440×1000** y de teléfono **390×844**. Comprueba carga, navegación e interacción del visor; el viewport estrecho es una emulación de navegador, no una prueba del ejecutable en teléfono. Tampoco constituye aprobación estética.

## Alcance de juego preservado

Se mantienen las ocho facciones y la separación de PvP histórico/fantástico. Estas cuatro escenas son muestras de arte, no cuatro mapas añadidos a matchmaking. Los arquetipos nuevos no asignan automáticamente enanos, elfos o piratas a facciones existentes; las criaturas militares siguen siendo ejército aunque admitan skins. La apariencia permanece separada de vida, ataque, coste, velocidad y demás reglas.

El nuevo encargo supera la antigua parada tras dos candidatos: la muestra cubre las cuatro escenas y nueve arquetipos solicitados. Quedan visibles las brechas artísticas descritas, el aviso de geometría y la necesidad de medir y optimizar antes de integrar este detalle en partidas móviles. Compilar y pasar un validador no decide su aceptación visual.
