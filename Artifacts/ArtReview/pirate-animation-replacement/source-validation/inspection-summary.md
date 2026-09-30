# Inspección de animaciones Meshy — Red Tide Corsair

Inspección realizada en Blender 4.5.13 LTS, en archivos temporales. Los ZIP originales se conservaron. La extracción validó cada ruta y rechazó rutas absolutas, traversal y destinos externos. `archives.json` contiene los SHA-256 y el inventario.

## Identidad y geometría

La apariencia corresponde al Saqueador de abordaje: bandana roja, barba, chaleco, brazos tatuados y pantalón de rayas. Es otra malla del personaje, en T-pose y con sable en el cinturón; no es la malla de alta densidad actualmente integrada con sable apoyado en el hombro.

Los dos FBX contienen `output_unwrapped`: 5.064 vértices, 10.291 triángulos, un conjunto UV y 27 grupos de pesos. Todos los vértices tienen pesos. También contienen un `Icosphere` auxiliar sin pesos, de 42 vértices y 80 triángulos. Ese auxiliar está oculto en las capturas de inspección y debe excluirse de cualquier importación visual del personaje.

Ambos archivos comparten exactamente índices de triángulos, UV, pesos, jerarquía y matrices de bind pose. La diferencia máxima entre posiciones de vértices es 1,1921e−7 m, compatible con redondeo de coma flotante; RMS 1,2723e−8 m. Por tanto, los dos clips usan el mismo cuerpo y rig a efectos prácticos.

## Rig y orientación

`Armature` tiene 34 huesos. La raíz es `Hips`, sin hueso `Root` adicional. Cadena del torso: `Hips → Spine02 → Spine01 → Spine → neck → Head`. Las extremidades usan `Left/RightShoulder`, `Arm`, `ForeArm`, `Hand`, `UpLeg`, `Leg`, `Foot`, `ToeBase`; hay extremos auxiliares. El JSON incluye todos los nombres, padres y matrices.

Tras la conversión estándar de FBX a Blender, el personaje mide 1,7000 m, con suelo Z≈0, vertical +Z y frontal −Y. `Left` queda en +X y `Right` en −X. La matriz mundo de Armature es identidad salvo ruido inferior a 5e−8. El eje local +Y de cada hueso apunta de cabeza a cola. FBX declara UpAxis=1, FrontAxis=2 y CoordAxis=0, signos positivos; estos metadatos ya han sido convertidos por el importador y no deben aplicarse una segunda vez.

## Clips

| Archivo | Acción principal | Frames importados | FPS | Duración |
|---|---|---:|---:|---:|
| Running_withSkin | Running | 1–16 | 24 | 0,625 s |
| Fall_Dead_from_Abdominal_Injury_withSkin | Fall_Dead_from_Abdominal_In… | 1–84 | 24 | 3,458333 s |

El FBX original confirma TimeMode=11 y CustomFrameRate=24. Cada archivo contiene además una acción auxiliar de un solo frame: debe seleccionarse la acción larga. Las acciones principales tienen 349 curvas.

El objeto Armature no traslada. `Hips` sí tiene curvas de movimiento: Running oscila en el lugar, con diferencia horizontal entre extremos de aproximadamente 7 mm. Death desplaza la pelvis aproximadamente 0,923 m hacia −Y y baja de Z=0,963 a 0,342 m. La caída necesita conservar ese movimiento de pelvis aunque el desplazamiento de la unidad lo gobierne el juego.

## Texturas

Las cuatro imágenes son idénticas por SHA-256 entre los ZIP: base color y normal a 2048×2048; metallic y roughness a 4096×4096. Los renders conectan únicamente esas imágenes al shader PBR. No se añadieron texturas externas.

## Evidencia

- `run/rest-front.png`, `run/rest-back.png`, `run/rest-side.png`: malla original en reposo.
- `run/animation-8.png`: muestra de carrera suministrada, sin Icosphere.
- `death/animation-84.png`: final de la caída suministrada, sin Icosphere.
- Las carpetas incluyen cinco muestras temporales de cada clip y `imported.blend` editables.
- `run/inspection.json` y `death/inspection.json`: matrices, huesos, acciones, texturas, bounds y movimiento de pelvis por frame.
- `comparison.json`: comprobación numérica de compatibilidad entre ambos archivos.

Las cámaras de las muestras se recentran para inspeccionar la pose; el root motion se evalúa con los números del JSON. Esta inspección no valida todavía el retarget al rig actual, Unity, transiciones ni rendimiento.
