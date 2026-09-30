# Plan artístico vigente — Kingdom premium para PC

Fecha: 10 de septiembre de 2026. **Alcance actual: una escena Kingdom con Worker, Warrior y Hero, un edificio principal y su paisaje.** El usuario ha rechazado la dirección visual de la primera renovación. Esta iteración reconstruye la base artística para aproximar de forma concreta las figuras Kingdom de sus referencias; el éxito se juzga por lo que muestran las imágenes nativas.

El plan anterior de cuatro biomas queda archivado íntegramente en [art-overhaul-plan-v1.md](art-overhaul-plan-v1.md). Sus 17 prefabs, 51 LOD, 51 clips y pruebas del ejecutable siguen describiendo aquella entrega; no son aceptación estética ni evidencia de la nueva escena. El punto de partida rechazado está en el commit `d8e1708127b054093376b4d41f2b71068a73e453` y en [art-overhaul-review.md](art-overhaul-review.md).

**Estado de esta iteración:** escena Kingdom y tres personajes importados, assets validados y build Windows completado con 0 errores y 2 avisos. Las seis capturas finales de Editor y las tres comparaciones con las referencias ya están revisadas. El ejecutable final, GUID `79363a377fe148bca74510c9cc90d7ad`, supera la revisión a 1920×1080 con siete capturas y las tres figuras completas en Media, RTS y bajo el HUD. La anatomía, el vestuario y el entorno han mejorado, pero rostros similares, prendas rígidas y placas con contornos ondulados mantienen una distancia clara respecto al acabado solicitado. La [revisión de producción](kingdom-premium-production.md#evidencia-unity) distingue esos resultados técnicos de la aceptación artística, que no se da por conseguida.

## Dirección visual y resultado esperado

Crear personas adultas estilizadas con anatomía coherente, rostros distintos, manos que sujetan equipo, ropa con peso y pliegues, armadura ajustada al cuerpo y materiales diferenciados. El trabajador, guerrero y héroe deben reconocerse por su construcción completa, incluso antes de añadir emblemas o color de equipo. Su edificio y paisaje deben mantener ese nivel de detalle y darles una situación creíble.

Las referencias R01, R03 y R09 son el objetivo de diseño específico, no solo inspiración de paleta. La [auditoría Kingdom](kingdom-premium-audit.md) identifica las diferencias actuales y las correcciones por parte del cuerpo, prenda y material. La petición de máxima fidelidad se traduce en comparación directa y correcciones visibles; la entrega no se describirá como idéntica cuando persistan diferencias.

Se prioriza **calidad visual de la muestra PC**. Los techos de polígonos del primer laboratorio móvil no dirigen esta reconstrucción. La optimización posterior deberá conservar la forma lograda y medirse en un hardware identificado. Un número de triángulos, una textura grande o un resultado de compilación no constituyen criterios de calidad artística.

## Contenido de esta escena

| Elemento | Construcción necesaria | Evidencia visual de cierre |
| --- | --- | --- |
| Worker Kingdom | Anatomía adulta propia, sombrero de paja, camisa marfil remangada, vestuario azul, correas/bolsas, botas usadas, horca y carga ligada al oficio. | Silueta reconocible, mano cerrada alrededor del mango, carga sujeta, pliegues y materiales visibles en retrato y grupo. |
| Warrior Kingdom | Casco de acero con abertura facial, malla, tabardo azul/marfil, placas articuladas, capa, espada recta y escudo curvado con agarre. | Cuello/cintura legibles, equipo que envuelve el cuerpo, escudo sostenido de manera creíble y espada proporcionada. |
| Hero Kingdom | Cabeza y postura distintas, peto trabajado, corona, manto largo con azul exterior/rojo interior/armiño, broches y espada ceremonial. | Figura de autoridad por silueta, postura y caída del manto; no basta modificar los accesorios del Warrior. |
| Edificio principal | Arquitectura de aldea con piedra, madera y cubierta; cimientos, entrada, vigas, ventanas y aleros construidos. | Escala coherente junto a los personajes, huecos profundos, materiales diferenciados y apoyo real sobre el terreno. |
| Paisaje próximo | Camino, relieve local, tierra pisada, vegetación agrupada y utilería de trabajo. Agua solo si aporta una ribera resuelta al lugar. | Transiciones naturales de suelo, densidad jerarquizada y espacio visual alrededor de figuras/equipo. |
| Luz y cámara | Iluminación direccional suave, ambiente y contacto; retratos, grupo con edificio y cámara RTS útil. | Rostros, metal y telas mantienen volumen y detalle a resolución nativa, con controles que permiten ver la escena. |

Caribbean, Desert, Fantasy, caballería, piratas, enanos, elfos, montañeses y dragones quedan fuera de esta iteración de autoría. Se conservan sus archivos existentes. No ampliar la muestra para compensar que los tres personajes Kingdom sigan sin alcanzar la dirección solicitada.

## Método de autoría

La base seleccionada y utilizada es **Body Male - Realistic, de Dan Ulrich**, del paquete **Blender Studio Human Base Meshes 1.4.1**, publicado bajo **CC0**. Worker, Warrior y Hero ya tienen fuentes Blender, exportaciones FBX y manifiestos individuales en `Assets/Game/KingdomPremium/Characters/`. La [documentación de producción](kingdom-premium-production.md) registra la descarga exacta, su hash, las modificaciones y la reproducción.

La autoría en Blender añade vestuario, armadura, pelo, equipo y ajustes de proporción sobre esa anatomía. Los tres personajes son **mallas en poses estáticas**, con LOD0, LOD1 y LOD2: nueve mallas exportadas en total. **No incluyen rig, pesos de deformación ni animaciones.** El visor permite inspeccionarlos mediante selección y cámaras; esa interacción no demuestra movimiento del personaje. La diferenciación del cuerpo, los agarres y la calidad de las prendas siguen sujetos a comparación visual, aunque sus archivos ya existan.

Las exportaciones incluyen UV, normales y materiales semánticos. Los LOD reducidos se derivan de la geometría de mayor detalle; su existencia no acredita rendimiento móvil ni conservación estética a cualquier distancia. La ropa debe existir como forma y espesor; mapas de textura pueden describir tejido, desgaste, metal y cuero, pero no sustituir pliegues o siluetas ausentes. Reutilizar materiales cuando conserve coherencia, sin imponer el antiguo atlas como condición para poder trabajar rostro y prendas.

El entorno también tiene una exportación FBX real, sus texturas y un manifiesto de materiales y procedencias. Combina arquitectura y composición propias con vegetación, utilería y superficies CC0 de Poly Haven. La integración de Unity utiliza materiales, luces y cámaras reales; el bake, la validación de assets y las capturas de Editor ya están documentados. Las herramientas de generación de imágenes se usan para superficies aplicadas a geometría, como el bordado real; las imágenes de presentación final se capturan de Unity. No se da por aceptado el resultado artístico a partir de los previews de Blender ni de las comprobaciones técnicas.

## Secuencia de trabajo

| Paso | Trabajo | Comprobación interna para corregir y continuar |
| --- | --- | --- |
| 1. Comparación y base | Base CC0 seleccionada e incorporada; confrontar forma actual y referencias R01/R03/R09. | Cuerpo y rostro con referencias anatómicas visibles; fuente editable y procedencia registrada. |
| 2. Worker | Resolver anatomía, sombrero, camisa, chaleco, manos, horca, botas y carga. | Vistas nativas completa/lateral/tres cuartos; corregir agarres, caída y proporciones antes de acumular detalles. |
| 3. Warrior | Crear identidad corporal, casco, malla, tabardo, protección, escudo y espada. | Equipo articulado y adaptado al cuerpo; lectura distinta de Worker sin depender de color. |
| 4. Hero | Modelar figura, expresión, manto, armiño, peto y accesorios nobles. | Silueta diferente de Warrior, pliegues con peso y detalle jerarquizado. |
| 5. Lugar Kingdom | Construir edificio, terreno y vegetación alrededor de una composición común. | Acceso, escala, apoyo y materiales coherentes; ninguna figura presentada como muestra aislada sobre una peana. |
| 6. Integración y luz | Ajustar materiales bajo la misma iluminación, contacto, sombras y cámaras de conjunto. | Los tres personajes y el edificio mantienen el acabado desde las vistas previstas. |
| 7. Revisión visual final | Comparar capturas nuevas con las anteriores y las referencias; corregir divergencias concretas. | Escena y ejecutable reproducibles, imágenes nativas y declaración honesta de brechas restantes. |

La secuencia es interna y continua. **No hay una pausa de aprobación tras Worker ni un retorno al antiguo checkpoint de dos unidades.** Se continúa hasta completar la única escena y sus tres personajes dentro del alcance solicitado.

## Cámaras y evidencia

Destino de la entrega nueva: `Artifacts/ArtReview/kingdom-premium/`, separado de los renders de `overhaul/` para conservar la comparación. El visor y la galería están preparados; las capturas finales de Editor y del ejecutable, el build y la comparación visual están registrados en la sección de evidencia de [producción](kingdom-premium-production.md#evidencia-unity). Las imágenes `comparison-worker.png`, `comparison-warrior.png` y `comparison-hero.png` muestran referencia/anterior/nuevo con etiquetas claras, capturadas directamente de la galería HTML.

- Retratos completos de Worker, Warrior y Hero, con la misma configuración de luz y altura de presentación comparable. Incluir vistas de apoyo para rostro, manos y perfil.
- Vista de los tres personajes y el edificio que permita evaluar escala, materiales y relación con el terreno.
- Vista RTS normal del mismo lugar a 1920×1080; los roles y el vestuario deben seguir siendo reconocibles sin ampliar externamente la imagen.
- Captura del visor con controles de selección y cámara. El HUD debe dejar el grupo visible; los retratos limpios mantienen su propia cámara. Los tres personajes conservan su pose estática.
- Informe con fuente de las mallas, exportación, build revisado y diferencias que sigan presentes frente a R01/R03/R09.

Las cámaras no cambian el tamaño de las figuras para favorecer una captura. Las vistas de Blender ayudan a modelar; la comparación del resultado integrado utiliza capturas nativas de Unity. No reemplazar esas imágenes por conceptos generados, sprites de figuras ni retoque que oculte problemas del modelo.

## Criterios de aceptación

La [auditoría](kingdom-premium-audit.md#criterios-de-comparación-final) fija comprobaciones visuales concretas: anatomía adulta y diferenciada, equipo sostenido, ropa construida, fidelidad al personaje Kingdom de cada lámina, materiales coherentes, arquitectura integrada y lectura a escala normal. La comparación debe identificar qué formas mejoran y qué divergencias permanecen; no cerrar el trabajo mediante una etiqueta «premium» sin esa evidencia.

Las comprobaciones técnicas verifican que materiales y mallas estén presentes, los tres LOD estén asociados, no existan errores visibles de importación y la escena/ejecutable se reproduzcan. No se evalúan animaciones ni deformación porque esta muestra carece de rig. El perfilado registra hardware y condiciones reales si se realiza. Esas verificaciones sostienen la entrega, pero no sustituyen el juicio sobre su apariencia.

## Reglas del juego preservadas

La escena es una muestra de arte PC. No añade facciones ni cambia la separación entre PvP histórico y fantástico, las criaturas del ejército o la ausencia de ventajas en skins. Vida, daño, coste, velocidad y demás reglas permanecen fuera de los datos visuales.

Warrior toma su espada de R03 para la comparación de esta muestra. Eso no transforma automáticamente el rol de lancero de Reedguard ni lo integra con un nuevo alcance de ataque. Los IDs, asignación al roster y reglas de combate existentes se preservan hasta una integración de juego expresamente definida. Emberfield conserva su identidad; la maquetación «Crowns & Horizons» de las láminas no renombra el proyecto.
