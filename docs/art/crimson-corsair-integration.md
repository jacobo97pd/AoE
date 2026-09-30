# Corsario Carmesí — integración del personaje

El personaje entregado por el usuario se incorpora como **Corsario Carmesí**, un héroe pirata con identidad propia (`crimson_corsair`). Conserva su rostro, sombrero, abrigo bordado, sable y loro. El modelo y sus texturas proceden del ZIP de Meshy; el esqueleto, los pesos y las seis animaciones se han creado para este proyecto.

Para verlo, abre [CrimsonCorsair.exe](../../Builds/CrimsonCorsair/CrimsonCorsair.exe). Las teclas **1 / 2 / 3** seleccionan cámara cercana, media y RTS. **I / W / R / A / H / D** reproducen reposo, caminar, correr, ataque, impacto y caída. Arrastrar gira la cámara, la rueda acerca, Espacio pausa y Tab oculta los controles. El botón **JUGAR** abre una partida costera con el héroe seleccionado; también se puede reclutar desde el **Hearth** con la facción **Piratas**, en **Navales**.

Desde el 14 de septiembre de 2026, el héroe y su escenario costero se restringen a Piratas en Navales. La reclasificación conserva el modelo, el material, el prefab y sus clips; las pruebas y capturas originales se mantienen como evidencia de la entrega anterior. Las partidas navales actuales son terrestres en la costa, sin navegación de barcos.

## Qué se ha adaptado

El original tenía 2.386.018 triángulos, una única malla visible y ninguna animación ni esqueleto. El barril, la cuerda y el timón formaban una isla de geometría independiente: se separaron sin recortar ropa ni calzado. El personaje restante se normalizó a 2,5 m de altura, incluyendo el sombrero, y se prepararon tres niveles de detalle de **140.000, 45.000 y 12.000 triángulos**, conservando UV y aspecto original.

Se ajustó un esqueleto genérico de 27 huesos a la pose de origen. El sable sigue la mano; el abrigo y el loro cuentan con controles propios. La asignación de pesos distingue componentes conectados para impedir que solapas y bolsas se arrastren con los brazos. Los bordados y ribetes del abrigo comparten movimiento con la tela mediante propagación sobre la superficie y suavizado de pesos.

Sus clips se generan con el mismo módulo que la tripulación pirata, [pirate_motion.py](../../tools/art/pirate_motion.py). Anda y corre con los pies apoyados en el suelo, a una velocidad de zancada medida de 1,39 m/s andando y 3,10 m/s corriendo, que el juego usa para que no patine. Mantiene los pies anclados al atacar, recibir un golpe o estar en reposo, y su tajo horizontal llega hacia los 0,2 s, cuando la simulación aplica el daño. La [descripción completa](pirate-crew-integration.md#esqueleto-y-movimiento) está en el informe de la tripulación.

El material usa **URP/Lit** con los PNG originales de 2048 × 2048: color, normal, metal y rugosidad. Para Unity se construye un mapa con metal en el canal rojo y suavidad `1 − rugosidad` en alfa. El color se importa como sRGB y los mapas de datos como lineales.

## Comportamiento del héroe

| Parámetro inicial | Valor |
| --- | --- |
| Ámbito, facción y reclutamiento | Navales · Piratas · Hearth |
| Vida / armadura | 380 / 3 |
| Daño / intervalo de ataque | 24 / 1,2 s |
| Velocidad / alcance cuerpo a cuerpo | 3,4 m/s / 1,2 m |
| Coste / entrenamiento | 200 alimento + 100 metal / 40 s |
| Población / límite | 3 / uno vivo o en cola por jugador |

Son valores iniciales de juego. Las animaciones se reproducen en el sitio y la simulación gobierna el desplazamiento, el daño y la muerte.

## Archivos de revisión

- [Galería de capturas de Unity](../../Artifacts/ArtReview/crimson-corsair/index.html).
- [Escena](../../Assets/Game/Scenes/CrimsonCorsair.unity), [prefab](../../Assets/Game/CrimsonCorsair/Resources/ImportedUnits/CrimsonCorsair.prefab) y [material](../../Assets/Game/CrimsonCorsair/Materials/CrimsonCorsair.mat).
- [FBX con esqueleto y clips](../../Assets/Game/CrimsonCorsair/Model/CrimsonCorsair.fbx) y [fuente Blender editable](../../Artifacts/ArtReview/crimson-corsair/source/CrimsonCorsair.blend).
- [Manifiesto del modelo](../../Assets/Game/CrimsonCorsair/model-manifest.json) y [guía técnica](../../Assets/Game/CrimsonCorsair/README.md).

La fuente es `Meshy_AI_Crimson_Corsair_Comma_0911070912_texture_fbx (9).zip`, entregada por el usuario. Su SHA-256 es `ed4163d7e2fde97f7142391ac537a15ff560c4b4b8564371715c87fa78cf642c`. El archivo original se conserva fuera del repositorio. El pequeño patio de revisión reutiliza los recursos CC0 ya documentados del proyecto.

Las animaciones son clips creados sobre este modelo posado, no captura de movimiento. El personaje no tiene rig facial ni configuración Humanoid para retargeting automático. Las manos, la unión entre prendas y los pliegues pueden necesitar refinamiento para movimientos más extremos. Los LOD reducen geometría; por sí solos no acreditan rendimiento móvil ni equilibrio competitivo.

## Validación realizada

[Evidencia de la entrega y resultados conservados](../../Artifacts/ArtReview/crimson-corsair/validation/summary.json).

| Comprobación | Resultado |
| --- | --- |
| Compilación del ejecutable de Unity | Correcta, 0 errores y 0 advertencias |
| Pruebas Unity PlayMode | 15/15 aprobadas |
| Nuevas pruebas del héroe en .NET | 13/13 aprobadas |
| Regresión de expansión en .NET | 44/44 aprobadas |
| Pruebas del servidor | 25/25 aprobadas |

La [revisión del ejecutable](../../Artifacts/ArtReview/crimson-corsair/player/review.json) comprobó la entrada en los seis estados del Animator y el desplazamiento de vértices de la malla deformada. Las capturas nativas se generaron a 1920 × 1080.

La [prueba en partida](../../Artifacts/ArtReview/crimson-corsair/game/gameplay-review.json) utilizó un escenario local determinista en `sapphire_coast`, con un enemigo adicional y pasos de simulación acelerados. Verificó desplazamiento, ataque aceptado, daño al enemigo, recepción de daño, reacción al impacto, muerte, animación del cuerpo, conservación de la pausa y presencia del HUD. Las estadísticas originales del héroe, las reglas de órdenes y la niebla permanecen activas en esa prueba. Estos resultados verifican la integración; no constituyen una medición de FPS ni una certificación de equilibrio PvP.
