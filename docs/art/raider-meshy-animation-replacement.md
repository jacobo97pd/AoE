# Saqueador: carrera y muerte de Meshy

La actualización afecta exclusivamente a `boarding_raider`, el saqueador de abordaje. Los dos ZIP nuevos incluyen una versión del personaje con esqueleto bípedo y el sable en el cinturón. Se utiliza ese modelo para conservar sus movimientos: transferirlos a la malla anterior, cuyo puño y sable estaban unidos a otras superficies, abría huecos en la muñeca y la hoja.

## Fuentes

| Archivo del usuario | Clip | Duración original | SHA-256 del ZIP |
| --- | --- | --- | --- |
| `Meshy_AI_Red_Tide_Corsair_biped.zip` | Running | 0,625 s; 24 fps | `445ff34a9c10b1ee9c701926737cc83137107239e18d13450a3766b58ecefe3a` |
| `Meshy_AI_Red_Tide_Corsair_biped (1).zip` | Fall Dead from Abdominal Injury | 3,458333 s; 24 fps | `c734ee928cd486f9d0c34783c9a1ba58889adb32346ff1ed29f044d011929e17` |

Ambos FBX contienen la misma topología, UV, pesos, esqueleto y cuatro texturas. Las diferencias de coordenadas entre las dos exportaciones no superan 0,00000012 m. El objeto auxiliar `Icosphere` se excluye del personaje. La malla original contiene 10.291 triángulos; los LOD reducidos contienen 5.000 y 1.999. Se conserva el detalle entregado mediante sus mapas de color, normal, metal y rugosidad, con un material URP/Lit propio del saqueador.

## Integración

La altura se ajusta a 2,35 m. Una raíz estática permite usar el Animator Generic con root motion desactivado; la simulación sigue controlando el desplazamiento. El movimiento de la cadera durante la caída pertenece al clip y se conserva. La preparación del esqueleto comparó diez poses contra los originales escalados: la diferencia máxima fue de 3,27 micrómetros.

`Run` y `Death` proceden de los clips suministrados. `Run` se reproduce en bucle y `Death` una sola vez. Los ZIP no contienen reposo, marcha, ataque ni reacción al golpe: esos cuatro estados se adaptan al nuevo esqueleto para mantener el personaje operativo. El ataque provisional es un golpe corto de brazo y cuerpo; no se ha creado una animación de desenvainar el sable del cinturón.

El capitán, el pistolero y la buscadora conservan sus modelos, materiales, prefabs y controladores. El catálogo de unidades, sus costes, daño y reglas de juego no forma parte de este cambio.

## Archivos

- Modelo: `Assets/Game/PirateCrew/BoardingRaider/Model/BoardingRaider.fbx`.
- Prefab: `Assets/Game/PirateCrew/Resources/ImportedUnits/BoardingRaider.prefab`.
- Material y texturas: `Assets/Game/PirateCrew/BoardingRaider/Materials` y `Textures`.
- Fuente editable: `Artifacts/ArtReview/pirate-crew/source/BoardingRaider.blend`.
- Preparación: `tools/art/import_meshy_raider.py`.
- Importación y comprobación: `Emberfield.Editor.PirateAnimationReplacement.ApplyMeshyRaiderAndValidate`.
- Escena: `Assets/Game/Scenes/PirateCrew.unity`.

La generación antigua de rigs reconoce `pipeline: MeshyBiped` en el manifiesto y conserva este asset. Los bakers de Unity usan el material individual cuando regeneran el prefab.

## Validación

Validado el 13 de septiembre de 2026 en Unity 6000.3.23f1:

- Importación aprobada: seis clips con rutas de huesos válidas y deformación comprobada en los tres LOD; GUID de modelo, prefab y controlador conservados. Se generaron 18 capturas de poses en el editor.
- 19 pruebas PlayMode aprobadas, sin fallos ni pruebas omitidas. Incluyen importación, estados de animación y selección de unidades.
- Ejecutable Windows compilado con cero errores y cero avisos. Visor nativo: cuatro personajes y 26 estados aprobados; incluye la carrera y muerte nuevas del saqueador.
- Prueba nativa de partida aprobada: desplazamiento del saqueador de 4,96 m, ataque y daño, materiales propios, conservación del atlas del pistolero/buscadora y comprobaciones existentes de disparo, trabajo, depósito y pausa.
- Visor y partida corresponden al mismo build: `6b6434c4d93b490785095871057523ed`.
- La comparación final SHA-256 conserva los 76 archivos protegidos de la captura inicial fuera del saqueador y su README. Entre ellos están todos los modelos, texturas, prefabs y controladores del capitán, pistolero y buscadora.

Las comprobaciones de partida aceleran los ticks de simulación; no son una prueba de rendimiento. Los cuatro clips provisionales no tienen la misma procedencia ni calidad de movimiento que los dos suministrados por Meshy.

Evidencias: `Artifacts/ArtReview/pirate-animation-replacement` contiene `BoardingRaider/meshy-raider-validation.json`, `playmode.xml`, `native-viewer.json`, `native-game.json`, `other-pirates-unchanged.json` y `build.txt`. Las capturas nativas actualizadas están en `Artifacts/ArtReview/pirate-crew/player/boarding_raider`; la galería se abre desde `Artifacts/ArtReview/pirate-crew/index.html`.

Para revisarlo en movimiento, abrir `Builds/PirateCrew/PirateCrew.exe`. Seleccionar **Saqueador de abordaje** con las flechas; **R** reproduce correr, **D** morir, **I** reposo y **1 / 2 / 3** cambian entre cámara cercana, media y RTS. **JUGAR** abre la partida de revisión con la tripulación.
