# Objetivo de estilo — ArtStyleLab

Fecha: 2026-09-10. Estado: **especificación candidata para checkpoint humano 2**. La dirección es un RTS 3D estilizado de calidad premium: heroico, colorido, con superficies cuidadas y siluetas claras. Las [trece referencias analizadas](REFERENCE_IMAGE_ANALYSIS.md) fijan el lenguaje; la calidad del prototipo se acepta mediante imágenes del motor y revisión humana, no mediante este documento.

## Alcance inmediato

Entregar una escena de inspección **ArtStyleLab**, un trabajador y un guerrero originales en 3D, material URP reutilizable, máscara de equipo y capturas cercanas, medias y a distancia RTS. El laboratorio comprueba la viabilidad de esta dirección antes de producir un kit completo. La instrucción de ejecución inmediata del prompt lleva la auditoría y este par hasta el checkpoint 2; allí se detiene la expansión artística para revisión.

| Prototipo visual | Identidad de juego conservada | Lectura que debe mantenerse |
| --- | --- | --- |
| Trabajador del reino | Aven `tender` | Herramienta, carga, ropa de trabajo; recolecta y construye |
| Guerrero del reino | Aven `reedguard` | **Lanza y escudo**; infantería de lanza existente |

La espada de la referencia 03 informa del material y vestuario, pero no reemplaza la lanza de Reedguard. El proyecto continúa llamándose **Emberfield**. Aven, Serevin, Miraj y Skeld permanecen históricos; Solar, Verdant, Ashen y Drakeforged permanecen fantásticos. Sus PvP están separados. Piratas, enanos, elfos, héroes y nuevas monturas son familias de referencia para posibles etapas posteriores, sin asignación automática a nuevas facciones ni cambios de reglas.

Se conserva la alfa funcional y sus fuentes de arte. El laboratorio no sustituye masivamente los modelos de batalla. El informe de entrega debe indicar qué materiales, mallas, prefabs, LOD y animaciones se han creado realmente y qué continúa como objetivo.

## Jerarquía visual

**Silueta → proporciones → herramienta/arma → color de equipo → pose/animación → detalle material.**

Se buscan cuerpos con continuidad entre torso, hombros, brazos y piernas; articulaciones claras, manos amplias y caras simplificadas con volúmenes legibles. La cabeza y las manos se exageran moderadamente. Evitar cuerpo de cápsula con equipo pegado, facetas muy gruesas en piel y tela, o brillos uniformes sobre todos los materiales.

Como guía inicial de modelado, explorar humanos de unas **5,5–6,5 cabezas** de altura aparente, ajustando casco y sombrero por separado. Es una elección de diseño aproximada, no una medición exacta de las láminas ni un requisito anatómico rígido. Hombros y botas anclan la figura; el hueco entre brazo/torso, entre piernas y entre arma/cuerpo debe sobrevivir a la reducción.

### Trabajador

Sombrero de ala amplia, camisa marfil, mangas de trabajo, paño de equipo estructurado, correas de cuero y carga compacta. Dar prioridad a una herramienta larga y a una bolsa/cesto reconocible; no acumular varios recursos o herramientas sobre la espalda. La carga debe permitir ver cabeza y brazos desde arriba. Bolsas y correas se funden en masas grandes en LOD reducido.

### Guerrero

Casco de acero, hombreras separadas del cuello, torso protegido y paño de equipo frontal y posterior. Placas con biseles y grosor; botas y guantes con forma. La lanza supera claramente la cabeza y conserva una punta suficientemente ancha para no desaparecer. El escudo es una masa lateral grande con borde metálico y campo de equipo; no debe convertirse en un rectángulo que oculte toda la pose. Detalles de Aven: marcos, bandas y geometría cívica propia, sin reproducir la flor de lis de las láminas.

## Paleta, materiales y luz

Bloques amplios de marfil, cuero y acero permiten que el equipo destaque. El azul real de las referencias es una prueba de color del laboratorio, no una sustitución de todos los colores de propietario del juego. Mantener acentos dorados contenidos y suficiente separación respecto al equipo amarillo.

| Material | Objetivo perceptivo | Guía inicial de respuesta URP |
| --- | --- | --- |
| Acero | Frío, limpio, con reflejo ancho y borde iluminado | Metallic alto; smoothness aproximado 0,45–0,65 |
| Latón/oro de adorno | Más cálido y escaso que el acero | Metallic alto; smoothness 0,35–0,55 |
| Tela | Color estable, pliegues amplios, brillo débil | Metallic 0; smoothness 0,10–0,25 |
| Cuero | Oscuro pero con volumen y cantos diferenciados | Metallic 0; smoothness 0,20–0,35 |
| Piel | Mate, tonos amplios, rostro simplificado | Metallic 0; smoothness baja y sin microdetalle |
| Madera | Fibra sugerida y valor distinto al cuero | Metallic 0; smoothness 0,10–0,25 |

Los intervalos son puntos de partida para revisión, no valores deducidos de las imágenes. Un prototipo con respuesta material analítica y color de vértice puede validar volumen y luz; no se debe presentar como una tubería UV/PBR texturizada completa si no dispone de ella. La vía de producción incorpora UV, color base, máscara y normal solo donde aporte una diferencia visible.

Un shader URP compartido debe admitir color base, respuesta de metal/smoothness, máscara de equipo, tinte visual, sombra y feedback controlado. Normal map y rim son opcionales: deben justificarse en la comparación. No crear un shader o un material independiente por cada pieza de armadura. Mantener máscaras y valores fuera de las definiciones de combate.

Iluminación del laboratorio: sol cálido, ambiente más frío, sombras suaves legibles y exposición suficientemente clara para distinguir piel, cuero oscuro y metal. Bloom bajo o desactivado para los dos humanos; sin niebla que disimule el modelo. Una imagen atractiva de estudio no reemplaza la revisión con la iluminación y la cámara de batalla.

## Lenguaje de facción reutilizable

El contrato visual de una facción debe recoger identidad estable, proporciones/pose, silueta, armadura, tela, paleta, material de adorno, símbolos originales, arquitectura, armas, VFX, objetos de entorno y forma de estandarte. Puede expresarse mediante `FactionVisualDefinition` o equivalente de presentación; no añade salud, velocidad, daño o costes.

| Familia de referencia | Vocabulario aprovechable | Situación de esta entrega |
| --- | --- | --- |
| Reino | Acero, paños estructurados, escudos amplios, disciplina | Dos candidatos visuales Aven |
| Desierto | Capas de tela, crema/rojo, metal cálido | Referencia posterior; Miraj no cambia |
| Caribe/piratas | Cuero, cuerdas, paños, equipo naval | Sin nueva facción ni armas de pólvora |
| Montaña | Abrigo, piel, hierro, herramientas de supervivencia | Referencia posterior; Skeld no cambia |
| Enanos | Cuerpo compacto, barba, forja e ingeniería | Exploración futura sin asignación de roster |
| Elfos | Verticales elegantes, arco, hojas y tela fluida | Exploración futura sin asignación de raza |
| Héroes/dragones | Mayor presencia mediante forma y material | Fuera del par inicial |

Arquitectura, edificios representativos, suelo y vegetación pueden servir de contexto reutilizando contenido existente; no cuentan como nuevos kits de producción en este checkpoint.

## Presupuestos iniciales y pipeline

| Área | Objetivo de autoría inicial | Condición de aceptación |
| --- | --- | --- |
| Unidad LOD0 | 3.000–6.000 triángulos | Volumen cercano convincente; contar el modelo y su equipo completos |
| Unidad LOD1 | 1.500–3.000 | Conservar cabeza, herramienta, escudo y bloque de equipo |
| Unidad LOD2 | 500–1.200 | Rol y propietario legibles a distancia normal/lejana |
| LOD3 | Solo si el tamaño en pantalla lo justifica | No imponer billboard prematuramente |
| Texturas estándar | Atlas compartido hasta 1024 inicialmente | Mipmaps, máscara lineal, base color sRGB; medir memoria real |
| Texturas de props | 512–1024 como guía | Reutilización según cobertura en pantalla |
| Héroes/dragones futuros | Hasta 2048 inicialmente si se justifica | Fuera del par inicial |
| Materiales | Preferir un shader y un conjunto compartido | Registrar renderers, submallas y cambios de material reales |
| Equipo | Azul, rojo, verde y amarillo sobre la misma malla | Paneles visibles por delante y detrás; sin texturas duplicadas por equipo |

Son presupuestos revisables, no mínimos que obliguen a añadir geometría ni garantías de 30/60 FPS. Medir triángulos de cada LOD activo, materiales y memoria del resultado, además de comprobar la calidad del contorno. El precio de piel, sombra, draw calls y efectos puede dominar aunque el conteo de triángulos sea correcto.

Flujo de producción previsto: referencia → especificación → blockout → modelado de complejidad media → UV/textura → rig → animación → LOD → prefab Unity → cámara RTS → dispositivo físico. La primera entrega debe nombrar con precisión qué tramos cubre: una receta nativa reproducible es autoría 3D válida, pero no acredita skinning, clips o texturas que aún no existen.

Usar las herramientas instaladas y conservar fuentes editables, pivote al suelo, +Y arriba y orientación +Z. LOD, selección, propietario y animación son presentación; la simulación determina posición y acciones. Las skins resuelven una apariencia para la misma identidad de unidad. Cambiar el aspecto no altera colisión, selección, alcance, producción o estadísticas, ni habilita encuentros entre universos.

## Poses y animación

Validar primero postura, proporciones y lectura del equipo. Si el prototipo incorpora movimiento, comenzar con idle y una acción clara por rol, en el sitio y sin root motion autoritativo: gesto de recolección para Tender y preparación/estocada para Reedguard. Indicar si es articulación por transform, clips de Animator o malla skinned; no confundir balanceo del modelo completo con un rig terminado.

Idle, locomoción, ataque, impacto, muerte, selección, victoria y los ciclos de trabajo del prompt constituyen la biblioteca posterior. No producirla en masa antes de aceptar los dos candidatos. Ningún evento de animación aplica daño, entrega recursos o crea entidades.

## Validación visual del checkpoint 2

La referencia principal es la **vista RTS normal**, sin etiquetas que revelen el rol. El `RtsCamera` existente es ortográfico, con pitch **55°** y yaw inicial **35°**; sus giros mantienen el pitch. Las tomas deben registrar orientación, tamaño ortográfico, resolución, iluminación y LOD mostrado para hacer comparaciones repetibles.

| Toma | Qué resuelve | Evidencia requerida |
| --- | --- | --- |
| CloseCamera | Forma, cara/manos, uniones, pliegues y materiales | Trabajador y guerrero sin recortes de herramienta/escudo |
| MidCamera | Proporción y diferencia de rol | Ambos modelos sobre suelo neutral con igual escala de mundo |
| RTSCamera | Criterio principal de aceptación | Cuerpo de aproximadamente 40–120 px, indicando altura observada; HUD/etiquetas no necesarios para reconocerlos |
| Equipo | Separación de propietario y facción | Azul/rojo/verde/amarillo, misma geometría, frente y vista elevada posterior |
| Contexto y LOD | Pérdida de detalle y oclusión | Suelo con vegetación/edificio existente y transiciones sin perder arma o paño de equipo |

Guardar las capturas en `Artifacts/ArtReview/` junto con un informe que identifique las fuentes. Mostrar la comparación con la alfa anterior cuando esté disponible usando encuadre equivalente. No reescalar una imagen de primer plano y presentarla como captura de cámara RTS.

Comprobar visualmente: trabajador reconocible por sombrero/herramienta/carga; guerrero por lanza/escudo/casco; metal distinguible de tela y cuero; rostro sin apariencia de cubo; ausencia de intersecciones grandes; dedos simplificados coherentes; arma visible sobre suelo claro y oscuro; color de equipo reconocible sin depender solo del matiz; silueta estable al girar; capa/escudo sin ocultar toda la acción. Un test automatizado puede detectar referencias rotas, pero no aceptar estas cualidades por sí solo.

## Verificación técnica y límites del resultado

El validador debe detectar malla/material ausente, geometría no finita, referencias de prefab rotas, LOD incompleto, máscaras de equipo sin soporte, texturas excesivas y materiales duplicados. Revisar Animator/rig conforme a lo realmente declarado: si un prototipo carece de ellos, registrarlo como deuda y no afirmar que la biblioteca de animaciones está terminada. Compilar y ejecutar los controles apropiados al contenido añadido; mantener las reglas existentes.

Los objetivos de **60 FPS en tablets capaces** y **30 FPS estables como mínimo** siguen siendo metas de producto. Las pruebas futuras con 50/100/200/300 unidades deben medir CPU y GPU por separado, draw calls, triángulos, skinning, materiales, variantes y memoria en dispositivos reales. Una captura de escritorio a resolución de teléfono no demuestra rendimiento, consumo térmico o legibilidad física móvil.

La entrega al checkpoint 2 debe incluir archivos creados/modificados, conteos reales, materiales, tipo de animación, pruebas ejecutadas, capturas y brechas visibles. La decisión humana pendiente es si **este par** transmite la dirección y funciona en la cámara real. Hasta contar con esa valoración, el resultado es un candidato visual; no hay declaración de paridad con el arte conceptual ni autorización implícita para producir todo el roster.
