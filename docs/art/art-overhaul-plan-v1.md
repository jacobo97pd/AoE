# Plan de renovación artística — cuatro escenas 3D

Fecha: 10 de septiembre de 2026. Línea base auditada: `7ab2c89bbaadadf414c8042ea4743ef5cb0b1019`. Este documento define el trabajo solicitado en **ART OVERHAUL PROMPT — RTS VISUAL TARGET**. El plan conserva sus objetivos de autoría; el estado comprobado de la implementación se distingue a continuación.

## Estado de la implementación — muestra generada y verificada

Se han generado las cuatro escenas nativas, 17 prefabs de nueve arquetipos, 51 mallas LOD y 51 clips. Hay doce capturas Close/Medium/RTS y 17 retratos de personaje en `Artifacts/ArtReview/overhaul/`. La fuente del atlas compartido mide 1254×1254 y Unity la importa a 2048×2048 con mipmaps; esa ampliación no añade detalle al archivo fuente.

El [validador final de assets](../../Artifacts/ArtReview/overhaul/asset-validation.json), generado a las **18:50:05 UTC**, registra `passed: true` para los 17 prefabs, nueve arquetipos y cuatro escenas. Conserva un aviso no bloqueante de 36 triángulos minúsculos/degenerados en Elf LOD0. El [build Windows](../../Artifacts/ArtReview/overhaul/build.txt) termina correctamente, con cero errores y dos avisos. Ocho revisiones del mismo ejecutable registran `completed` y `passed: true`: cubren los cuatro biomas a 1920×1080 y repiten Kingdom/Fantasy a 1280×720 y 1440×1080, con 105 estados y 105 cambios de equipo comprobados en total. La cámara reserva espacio real para los controles: los grupos revisados quedan completos entre cabecera y panel inferior, confirmado con métricas e imágenes. Son resoluciones de Windows, no dispositivos móviles físicos. La [galería](../../Artifacts/ArtReview/overhaul/index.html) pasa 122 comprobaciones de navegador con sus 29 imágenes, en anchos de 1440 y 390 píxeles.

La revisión visual, los conteos por modelo y los enlaces a la evidencia están en [art-overhaul-review.md](art-overhaul-review.md). El entorno mejora la presentación respecto al suelo de laboratorio, pero persisten carencias de modelado, variedad, composición y riqueza frente a las referencias. El resultado técnico no equivale a aceptación artística. Los contadores de draw calls y triángulos del player devuelven `unavailable`; no existe una medición de rendimiento móvil. Algunas figuras superan las bandas orientativas de autoría de este plan: el informe las declara y no presenta el validador como límite de coste de producción.

## Resultado que debe entregar esta iteración

Crear una muestra artística de RTS 3D estilizado premium dentro de Unity: cuatro escenas con personajes originales, terreno modelado, agua, caminos, vegetación, edificios, utilería, sombras y profundidad atmosférica. La mejora debe apreciarse en anatomía, silueta, superficies y composición del mundo. Cambiar colores, subir bloom o aprobar un validador no cierra este trabajo.

Se empieza por **Kingdom / Temperate** y se continúa con **Caribbean, Desert y Fantasy**, hasta completar las cuatro. El nuevo encargo amplía expresamente el alcance a nueve tipos de personaje y cuatro entornos. La parada tras dos unidades descrita en [ART_STYLE_CHECKPOINT_2.md](ART_STYLE_CHECKPOINT_2.md), [ART_STYLE_TARGET.md](ART_STYLE_TARGET.md) y [current-art-audit.md](current-art-audit.md) pertenece al encargo anterior y no detiene esta iteración. Sigue siendo una muestra artística acotada; no constituye una conversión del roster completo a producción.

## Punto de partida comprobado

El laboratorio de `7ab2c89` contiene Tender y Reedguard, tres LOD por modelo, rig de 16 huesos, Animator, un material PBR compartido y una textura de superficie de 1024². Su [informe de assets](../../Artifacts/ArtReview/asset-validation.json) pasa y el [informe anterior](ART_STYLE_CHECKPOINT_2.md) registra siete pruebas NUnit y capturas del ejecutable. Estos resultados acreditan integridad de esos dos candidatos, no el acabado solicitado ni el funcionamiento de nuevos assets.

La [vista cercana](../../Artifacts/ArtReview/close.png) conserva hombros casi esféricos, uniones simples, vestuario poco estratificado y acero uniforme. La [vista RTS](../../Artifacts/ArtReview/rts.png) está dominada por un suelo liso, peanas, un edificio antiguo y vegetación escasa. La diferencia con las referencias está documentada en [reference-gap-analysis.md](reference-gap-analysis.md), con las siete críticas y su corrección verificable.

## Secuencia de ejecución

| Fase | Trabajo concreto | Evidencia necesaria para continuar |
| --- | --- | --- |
| A · Auditoría | Comparar fuente, modelos persistentes y capturas con las 13 referencias; registrar carencias de forma, superficie y mundo. | Este plan, análisis de brechas y guía de biomas; línea base identificada. |
| B · Materiales y luz | Crear atlas compartido de 4×4 regiones de superficie, ajustar UV, metal/tela/cuero/madera, terreno y agua; establecer luz principal, ambiente y grading coherentes. | Materiales reales bajo luz del entorno; variación visible sin ruido, metal distinguible de tela y agua con orilla. |
| C · Personajes | Mejorar trabajador y lancero; crear caballería, héroe, pirata, enano, elfo, guerrero montañés y dragón con geometría y equipamiento específicos. | Nueve arquetipos distintos; LOD y conteos reales; poses, agarres, contacto con suelo y máscara de equipo revisados. |
| D1 · Kingdom | Construir primero aldea, castillo, cultivo, molino, bosque, río, puente, acantilado y cascada; integrar cinco figuras en lugares creíbles. | Primer conjunto Close/Medium/RTS que resuelva los siete reproches, con revisión y correcciones locales. |
| D2 · Caribbean | Reutilizar la base técnica y autorar costa tropical, puerto, torre, ruina y guarnición. | Tres o más unidades en un puerto completo; identidad distinta desde RTS. |
| D3 · Desert | Construir oasis, dunas, muralla, mercado, obelisco y formación rocosa con variantes de vestuario. | Tres o más unidades; separación clara entre arena, arquitectura y figuras. |
| D4 · Fantasy | Construir bosque encantado, árbol sagrado, arquitectura elegante, ruinas, cristales y agua mística. | Enano, elfo y dragón visibles; luz y vegetación no ocultan sus siluetas. |
| E · Revisión | Generar doce capturas nativas, inventario medido y comprobaciones focalizadas; contrastar nuevamente con conceptos. | Archivos reales, escenas reproducibles, resultados y brechas residuales declarados sin afirmar paridad. |

La revisión de Kingdom es un paso interno para corregir la dirección antes de repetirla; no introduce una nueva solicitud de permiso ni una pausa que deje las otras escenas pendientes.

## Cobertura de personajes y escenarios

| Escena | Grupo previsto | Tipos mínimos cubiertos |
| --- | --- | --- |
| Kingdom | Trabajador con herramienta y carga; lancero con escudo; jinete con montura; héroe con capa; guerrero montañés junto a acceso rocoso. | Worker, warrior, cavalry, hero, mountain warrior. |
| Caribbean | Pirata con abrigo/bandana y arma curva; trabajador portuario con carga; guerrero de guarnición con equipo adaptado al calor. | Pirate, worker, warrior. |
| Desert | Trabajador con paños y herramienta; guerrero con prendas superpuestas; jinete con montura y arreos de viaje. | Worker, warrior, cavalry. |
| Fantasy | Enano compacto de equipo pesado; elfo esbelto con arma larga; dragón de cuerpo, patas, alas y cola articulados visualmente. | Dwarf, elf, dragon. |

Son **nueve arquetipos**, con reutilización justificada y variantes de vestuario en varias escenas. Cambiar únicamente el color de un humano no crea un enano, un elfo ni un pirata. Un jinete exige montura 3D, silla, riendas/arreos y relación anatómica coherente; el dragón exige otra estructura corporal. La guía de cada escena detalla el lenguaje en [biome-style-guide.md](biome-style-guide.md).

## Pipeline y separación de responsabilidades

Se mantiene Unity `6000.3.23f1` con URP. La autoría se realiza mediante fuentes C# que generan mallas, UV, materiales y prefabs nativos y persistentes; no se declara un flujo Blender/FBX que no se ha utilizado. Los modelos, edificios, vegetación y relieve son geometría 3D visible desde las tres cámaras. Las imágenes aportadas orientan diseño; no se convierten en personajes planos ni fondos que sustituyan el entorno.

El atlas de 4×4 contiene **texturas de materiales**: piedra, madera, tela, cuero, metal y superficies afines. Puede crearse una imagen fuente nueva para este atlas y registrar su procedencia. No contiene retratos, edificios completos, iluminación de escena ni capturas fingidas. Las UV asignan cada superficie a su región con márgenes; los materiales siguen respondiendo a iluminación y sombras de Unity. La generación de una textura no demuestra por sí sola que se use correctamente en las mallas.

Reutilizar materiales, mallas de utilería y prefabs por familia. Combinar elementos estáticos compatibles sin perder límites de culling razonables. Usar LOD auténticos; evitar multiplicar materiales o transparencias por hebilla, hoja o hebra. Pooling se aplica a efectos repetidos si los hay, no a una escena estática sin necesidad. Preservar fuentes y capturas anteriores como comparación.

## Reglas del juego que permanecen vigentes

Las ocho facciones actuales conservan su identidad: Aven Compact, Serevin March, Miraj Sultanate y Skeld Clans en PvP histórico; Solar Kingdom, Verdant Covenant, Ashen Dominion y Drakeforged Clans en PvP fantástico. Ambos universos siguen separados. Kingdom, Caribbean, Desert y Fantasy son direcciones de escena; no son cuatro facciones nuevas.

Tender y Reedguard mantienen sus IDs y su rol de trabajador y lancero con escudo. Los arquetipos nuevos de la muestra no asignan automáticamente piratas, enanos o elfos a una facción existente ni añaden estadísticas a `UnitVisualDefinition` o `CosmeticSkinDefinition`. Dragones y otras criaturas del ejército conservan su naturaleza militar; una apariencia comprable no concede vida, daño, alcance, velocidad ni ventajas PvP.

Las escenas de revisión no añaden silenciosamente mapas a matchmaking ni modifican altura navegable, colisiones de combate o recursos autoritativos. Los mapas jugables de la línea base siguen siendo Amber Crossing, Sapphire Coast y Sunscar Basin. Un puerto decorativo, un barco, un oasis o una montura de presentación tampoco acredita un sistema naval o una unidad nueva integrada en partidas.

## Presupuestos y medición

Medición de la línea base: Tender tiene **5.446 / 2.960 / 940** triángulos y Reedguard **6.440 / 3.348 / 968**, en LOD0/1/2. Su validador impone techos de **8.000 / 4.000 / 1.500** por candidato humano. El laboratorio anterior utiliza un material y una textura de 1024²; estos conteos no deben copiarse como si describieran la ampliación.

Para planificar la nueva autoría se proponen las siguientes bandas; son objetivos de coste que deben contrastarse con el resultado visual, no resultados medidos ni autorización para ignorar límites existentes:

| Familia | Banda orientativa LOD0 | Reducción que debe conservar la identidad |
| --- | ---: | --- |
| Infantería y trabajador | 4.000–8.000 triángulos | LOD1 aproximadamente 45–60%; LOD2 aproximadamente 15–25%, respetando los límites aplicables. |
| Héroe y equipo especialmente elaborado | 6.000–10.000 | Reducir ornamentos antes que capa, cabeza, arma o gesto. Un exceso sobre el validador humano exige regla explícita para esa familia. |
| Jinete y montura, suma completa | 10.000–18.000 | Medir ambos cuerpos y arreos; conservar cabeza, patas, silla y arma. |
| Dragón, cuerpo y alas completos | 12.000–22.000 | Preservar membranas, cuello, extremidades y cola; reducir escamas y subdivisiones internas. |

Texturas compartidas con mipmaps y dimensiones potencia de dos; 1024–2048 por atlas como punto de partida. No asignar un atlas máximo exclusivo a cada figura. El inventario final debe registrar dimensiones, materiales, triángulos, vértices, huesos si existen, LOD efectivos y geometría visible por escena, diferenciando instancias de assets únicos. No forzar todas las especies al rig humano de 16 huesos; declarar expresamente cualquier figura estática o animación todavía provisional.

Las capturas de PC a 1280×720 o 1440×1080 prueban encuadre, no rendimiento de teléfono/tablet. FPS, GPU, CPU, memoria, temperatura y coste con 50/100/200/300 unidades necesitan medición posterior en dispositivos identificados. La viabilidad móvil se protege mediante decisiones de autoría y queda pendiente de esos datos; no se afirma alcanzar 30–60 FPS.

## Entrega y aceptación visual

Destino previsto: `Artifacts/ArtReview/overhaul/`. Cada escena debe producir `<biome>-close.png`, `<biome>-medium.png` y `<biome>-rts.png`, con claves **kingdom, caribbean, desert, fantasy**. Son doce imágenes nativas de las escenas reales, sin ilustraciones sustitutivas ni retoque externo de la apariencia.

Close muestra proporciones, superficies y agarres con contexto; Medium relaciona personajes, camino y edificio; RTS revela composición del bioma y calidad durante una observación táctica normal. Registrar configuración de cámara y LOD efectivo, sin aumentar personajes solo para la captura ni ocultar todo el entorno en primer plano. La cámara RTS conserva como referencia la inclinación 55° y yaw 35°; cualquier cambio de zoom o encuadre se declara para que la comparación sea interpretable.

Antes de cerrar: comprobar cobertura de los nueve arquetipos y de todos los elementos ambientales; detectar mallas/materiales ausentes, UV fuera de región, agua gris, intersecciones evidentes, pies flotantes y recortes; revisar roles y cuatro colores de equipo en iluminación de bioma; registrar brechas residuales frente a cada una de las siete críticas. La entrega solo puede describirse como mejora del estilo en la medida en que sus capturas lo respalden. La integridad técnica y la ambición artística se evalúan por separado.
