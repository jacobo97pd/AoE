# ArtStyleLab — entrega al checkpoint humano 2

Fecha: 10 de septiembre de 2026. **Fase:** auditoría y laboratorio de estilo, hasta el checkpoint 2 del nuevo prompt artístico. **Estado:** candidatos implementados y verificados; pendientes de revisión visual humana. Este documento no declara aprobada la dirección, paridad con las referencias ni calidad final de producción.

La entrega se limita a dos modelos originales de Aven: **Tender**, trabajador, y **Reedguard**, lancero con escudo. La referencia de guerrero orienta proporciones, vestuario y materiales; su espada no sustituye la lanza ni el rol existente. La alfa funcional sigue disponible y sus unidades de batalla conservan su integración actual.

## Resultado propuesto

El laboratorio independiente muestra los dos candidatos sobre suelo neutro, con luz diurna URP, vegetación y un edificio de contexto reutilizados. Incluye comparación con las mallas de la alfa anterior y una fila de cuatro muestras de propietario: azul, rojo, verde y amarillo.

Sus controles permiten seleccionar al trabajador o al lancero, cambiar cámara, color y pose, y mover el seleccionado por el suelo de la escena. Ese movimiento es una interacción local del laboratorio; no envía órdenes de simulación ni constituye una partida PvP.

La cámara **RTS normal es la referencia de aceptación**: ortográfica, inclinación 55°, yaw 35°, zoom 8. Las cámaras cercana y media sirven para revisar volumen, agarres, uniones y material. Una imagen cercana favorable no sustituye la lectura real a distancia táctica.

## Assets creados

| Tipo | Entrega acotada |
| --- | --- |
| Escena | `Assets/Game/Scenes/ArtStyleLab.unity` |
| Modelos | Seis mallas persistentes: Tender y Reedguard con LOD0, LOD1 y LOD2 bajo `Assets/Game/ArtStyleLab/Models/` |
| Prefabs | `KingdomWorker.prefab` y `KingdomWarrior.prefab`, con Animator, LODGroup y un SkinnedMeshRenderer por LOD |
| Rig | Dos avatares Generic y esqueletos equivalentes de 16 huesos; piezas rígidas y mezcla de dos huesos en la cintura del torso |
| Animación | Seis clips y dos controladores: Worker Idle/Walk/Gather; Warrior Idle/Walk/Attack01 |
| Shader | `Assets/Game/ArtStyleLab/StylizedArmy.shader`, compartido por los candidatos |
| Material | `Materials/StylizedArmy.mat`; respuesta PBR estilizada y datos de superficie por vértice |
| Textura | `Materials/SharedSurface.png`, fuente compartida de 1024×1024 con variación amplia y tenue |
| Datos visuales | `AvenVisual.asset`, dos UnitVisualDefinition y dos CosmeticSkinDefinition de aspecto predeterminado |
| Iluminación | Perfil de revisión y materiales de contexto del laboratorio, separados del arte integrado en las partidas |

Los assets de contexto `Context_*` son copias persistentes de geometría existente para poder guardar y reproducir la escena. No se presentan como nuevos modelos de producción.

Las fuentes editables son recetas C# nativas, especialmente `ArtStylePrototypeGeometry.cs` y `ArtStyleLabBaker.cs`. Blender no se ha localizado en las rutas comprobadas; no se ha reinstalado Unity ni añadido herramientas de pago. Este procedimiento genera geometría, bindposes, pesos, clips y prefabs de Unity reales, pero no acredita un flujo Blender/FBX ni una escultura final terminada.

## Código y documentación añadidos

El trabajo añade `ArtStyleUnit` y `ArtStyleLabController` para la presentación interactiva, junto a `FactionVisualDefinition`, `UnitVisualDefinition` y `CosmeticSkinDefinition`. Los datos visuales referencian `tender` o `reedguard`; no contienen copias de vida, daño, costes ni ventajas de combate. Los candidatos permanecen sin aprobación de producción.

El baker genera la escena y sus assets. `ArtAssetValidator` y la assembly de pruebas `Assets/Tests/ArtStyleLab/` comprueban integridad. Los comandos de trabajo están en `tools/Build-ArtStyleLab.ps1`, `tools/Verify-ArtStyleLab.ps1`, `tools/Verify-ArtStyleLabPlayer.ps1`, `tools/build-art-review-gallery.py` y `tools/check-art-review-gallery.mjs`.

Se habilitó el módulo incorporado `com.unity.modules.animation` 1.0.0 en el manifest/lock para disponer de Animator y AnimationClip. No se cambió la versión de Unity ni URP. El README enlaza el laboratorio. La generación reconstruye expresamente los buffers de Mesh al actualizar un asset, conservando su GUID; el validador rechaza vértices huérfanos para detectar el problema de buffers antiguos observado durante la implementación.

La documentación previa a los nuevos assets está en [current-art-audit.md](current-art-audit.md), [REFERENCE_IMAGE_ANALYSIS.md](REFERENCE_IMAGE_ANALYSIS.md) y [ART_STYLE_TARGET.md](ART_STYLE_TARGET.md). Se conserva la documentación y el [atlas de la alfa anterior](visual-atlas/index.html) como línea base.

## Materiales, equipo y animación

El nuevo shader dispone de color base, normal opcional, metalicidad, suavidad, máscara de equipo, tinte de facción, rim contenido, selección y destello de impacto. La misma malla y el mismo material admiten los cuatro colores de propietario. La textura compartida de 1024 es una superficie sencilla; no es un retrato, un atlas exclusivo de piel/armadura pintado ni un mapa normal autorado. El normal está desactivado en el material inicial.

Los materiales de las piezas separan tela, cuero, piel, madera y metal mediante color y parámetros de superficie. El detalle y la iluminación siguen sujetos a la comparación visual. Las propiedades PBR por sí solas no demuestran el acabado de las referencias.

Los clips animan huesos y vértices de mallas skinned mediante Animator Generic. Predominan las piezas con pesos rígidos; la cintura del torso mezcla Hips/Spine para mantener la unión con la pelvis durante la recolección. Hay articulación real de brazos, piernas, cabeza y equipo, pero no deformación humana suave completa. La locomoción es en el sitio, sin root motion de gameplay. Los clips no otorgan recursos, daño ni movimiento autoritativo.

Quedan fuera de esta entrega Run, Attack02, Hit, Death, Selected, Victory y el conjunto completo de tareas del trabajador. La selección y el destello de daño del laboratorio son feedback visual, no clips adicionales. Se mantienen solamente Idle, Walk y una acción por candidato.

## Presupuestos y rendimiento

Conteos de los assets exportados, registrados en `Artifacts/ArtReview/asset-validation.json`:

| Candidato | LOD0 | LOD1 | LOD2 |
| --- | ---: | ---: | ---: |
| Tender | 5.446 triángulos | 2.960 | 940 |
| Reedguard | 6.440 triángulos | 3.348 | 968 |

Reedguard supera las bandas sugeridas de 3.000–6.000 para LOD0 y 1.500–3.000 para LOD1; ese exceso requiere revisión de silueta y coste. Las bandas son orientativas, no objetivos que obliguen a añadir polígonos ni garantías de rendimiento.

**No se han cerrado resultados de FPS, CPU/GPU, skinning, draw calls ni memoria de los candidatos en hardware móvil.** Tampoco existe aquí una prueba de 50/100/200/300 unidades en teléfono o tablet físico. Configurar `Application.targetFrameRate = 60` no demuestra alcanzar 60 FPS. Las capturas offscreen a resoluciones móviles solo sirven para composición y cobertura en píxeles.

La proyección de los vértices reales con cámara RTS a zoom 8 mide aproximadamente **81 px para Tender y 75 px para Reedguard en 1280×720**; en 1920×1080 y 1440×1080 son **122 px y 113 px**, incluyendo equipo. `readability.json` contiene el método y las medidas. Tender rebasa ligeramente los 120 px orientativos a 1080p; esto se presenta para revisión, sin ampliar el zoom para aparentar más detalle.

## Capturas y galería

La carpeta de revisión es [Artifacts/ArtReview](../../Artifacts/ArtReview/README.md), con [galería offline](../../Artifacts/ArtReview/index.html). El generador conserva las referencias conceptuales 1 y 3 como copias íntegras, verificadas por hash; no modifica las capturas para aproximarlas al concepto.

| Evidencia | Propósito |
| --- | --- |
| `close.png`, `mid.png`, `rts.png` | Comparar el mismo par en las tres cámaras |
| `comparison.png` | Contrastar las mallas de la alfa anterior con los candidatos en el laboratorio |
| `gather.png`, `attack.png` | Examinar fotogramas de las acciones iniciales; deben diferir visiblemente del reposo |
| `rts-phone.png` / `rts-tablet.png` | Encuadre offscreen a 1280×720 y 1440×1080, respectivamente; no son pruebas físicas de dispositivo |
| `player-close.png`, `player-mid.png`, `player-rts.png` | Capturas del ejecutable independiente de revisión |
| `player-controls.png`, `player-gather.png`, `player-attack.png`, `player-walk.png` | Controles y acciones en el laboratorio ejecutado |

Las imágenes se regeneraron desde las mallas finales. La captura síncrona del editor usa `SkinnedMeshRenderer.BakeMesh` con la pose real y conserva material y LOD; la del ejecutable usa la cámara activa, skinning en vivo y HUD en un RenderTexture de URP. Es necesario porque un player oculto no proporciona un backbuffer legible a ScreenCapture. Son renders nativos de Unity, sin retoque externo; la evidencia no equivale a aceptación artística. Gather y Attack son diferentes del reposo y se muestran por separado.

## Validación y resultados

El validador comprueba referencias, mallas/materiales/shaders, tres LOD decrecientes, pesos normalizados, bones/bindposes coherentes, zonas de equipo y superficies neutras, clips/controladores, presupuestos de textura y referencias entre prefab y datos visuales. Las pruebas añadidas incluyen deformación efectiva de vértices, conservación de identidad/material/malla al cambiar aspecto y coherencia de los datos visuales.

`asset-validation.json` registra integridad válida: dos prefabs, tres LOD por candidato, un material compartido y una textura. **7/7 casos NUnit pasan**, incluyendo deformación real de vértices por los seis clips, bindposes, separación de apariencia/identidad y rechazo de assets dañados. La comparación de colores admite tolerancia de coma flotante en el recorrido nativo de MaterialPropertyBlock.

La compilación independiente termina **Succeeded, 0 errores y 0 advertencias**, con escena exclusiva ArtStyleLab. El player `f2bfb4e2c82d49bfacf4520a196bd3d6` verifica seis estados animados, raíz sin deriva, cuatro muestras de equipo con material/malla compartidos y doce controles visibles con bloqueo de entrada hacia el suelo. Los informes nativos y de navegador se conservan junto a las capturas y en `TestResults`; el resumen `verification.json` registra las resoluciones y resultados finales.

Los tres recorridos nativos pasan en **1920×1080, 1280×720 y 1440×1080** con el mismo build. La galería carga 19 imágenes locales y supera **26 comprobaciones de navegador**: cámaras, referencias, visor ampliado, teclado, enlaces y ausencia de desbordamiento a 1440, 1024 y 390 px de anchura. Estas comprobaciones verifican el visor y la evidencia, no la experiencia táctil en hardware real.

La revisión visual de Close/Mid/RTS y acciones no encontró mallas rotas, materiales ausentes ni personajes recortados. En el encuadre 4:3 se corta parte del edificio de contexto a la izquierda; no se recortan los candidatos. La comparación muestra una mejora respecto a las figuras anteriores, pero todavía no alcanza el acabado de las referencias.

## Brechas visuales y deuda técnica

- No se ha confirmado paridad con las referencias ni aprobado todavía la lectura del par a distancia RTS.
- El modelado sigue siendo procedural. Proporciones, rostros, manos, agarres, pliegues e intersecciones requieren inspección humana.
- El rig conserva articulaciones rígidas salvo la mezcla de cintura; faltan deformación suave completa, pesos refinados y la biblioteca de animación de producción. El extremo del mango de la horca roza visualmente el sombrero en parte de Gather; las acciones necesitan mejorar agarres, anticipación y transferencia de peso.
- La textura compartida es deliberadamente sencilla. No hay aún un trabajo completo de UV y texturizado autorado de personaje comparable a las láminas.
- Los LOD y materiales deben revisarse durante movimiento y bajo carga. Un material compartido no implica coste de GPU o skinning validado.
- Las capturas de Windows y las resoluciones offscreen no establecen rendimiento, tamaño físico de lectura, consumo o estabilidad térmica en móvil.
- El laboratorio no integra estas mallas en la selección, niebla, animación y combate de todas las partidas. Esa integración requiere una fase posterior a la aceptación del estilo.

## Siguiente paso y motivo de la parada

La siguiente decisión es revisar **este trabajador y este lancero** en la cámara normal, corregir lo que no transmita las proporciones, materiales y legibilidad buscadas, y aprobar la dirección antes de ampliar el kit.

La parada procede de la instrucción explícita del usuario en la **sección 40** del prompt artístico: “stop at CHECKPOINT HUMAN 2” y “DO NOT mass-produce all units yet.” No es una autorización adicional inventada por la herramienta. Después de esa revisión, la fase siguiente propuesta es el kit visual de Aven, con ampliación gradual de roles y validación de rendimiento. No se produce ahora el resto de facciones, caballería, piratas, enanos, elfos, héroes o dragones nuevos.
