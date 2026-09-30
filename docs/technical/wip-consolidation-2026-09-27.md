# Consolidación del trabajo pendiente — 27 de septiembre de 2026

## Estado de la consolidación

La compilación, las suites de Unity, el servidor reconstruido, la comparación de ticks, las tres partidas nativas por reino, una partida adicional por HTTPS y el recorrido final del producto han pasado. No se hizo push ni se modificó `D:/EmberfieldJugar`. No se descartó trabajo pendiente.

La copia previa parte de `e1c6afbffcf2f8383e82bcac34e5ced1d554c7c1`: parche en `D:/EmberfieldWorkingCache/backup/codex-wip-2026-09-27.patch` y copia de 2.428 archivos sin seguimiento, conservando rutas, en `D:/EmberfieldWorkingCache/backup/codex-untracked-2026-09-27/`. El manifiesto `codex-backup-2026-09-27.json` de esa carpeta de copias registra tamaños y hashes. El inventario de consolidación y las salidas de verificación se guardan en `D:/EmberfieldWorkingCache/codex-consolidation-20260927/`.

## Commits confirmados

Commits de código, assets, herramientas y evidencia anteriores al commit que incorpora este informe (`Document the consolidated realms and verification results`).

| Commit | Mensaje |
| --- | --- |
| `d14d0e0` | Preserve realm rosters and deterministic simulation fixes |
| `d0618dd` | Harden realm-aware online play and authority recovery |
| `b94b47e` | Integrate the pirate crew and authored character animations |
| `69048f5` | Add the reference character collection and review scene |
| `bd95119` | Complete the realm selectors and integration coverage |
| `9cf4afa` | Track the recording diagnostics and safe player shutdown |
| `1877e1c` | Re-serialize baked unit controllers |
| `9fe2b10` | Preserve editable art sources and canonical review evidence |
| `45ec76d` | Pin the content version to the committed simulation |
| `92d66e2` | Scout resources before verifying online fortifications |

Las rutas se prepararon explícitamente. Los blobs preparados se inspeccionaron antes de cada commit para detectar claves de Meshy, tokens, contraseñas y otros secretos; los informes de escaneo se conservan en la carpeta externa de consolidación. Las referencias a variables de entorno y los valores ficticios de pruebas se revisaron sin incorporar credenciales reales.

## Verificación completada

| Comprobación | Resultado | Evidencia |
| --- | --- | --- |
| Unity Compile | Correcta, salida 0 | `D:/EmberfieldWorkingCache/codex-consolidation-20260927/compile-passed.log` |
| Unity EditMode | **751/751**, sin fallos ni omisiones | `TestResults/EditMode.xml`, copia fechada en la carpeta de consolidación |
| Unity PlayMode | **380/380**, sin fallos ni omisiones | `TestResults/PlayMode.xml`, copia fechada en la carpeta de consolidación |
| Servidor y autoridad C# reconstruida | **40/40**, sin fallos ni omisiones; 10,230 s | `D:/EmberfieldWorkingCache/codex-consolidation-20260927/server-tests.txt` |
| Determinismo | `SIMULATION_TICKS_IDENTICAL` | `D:/EmberfieldWorkingCache/codex-before.json` y `codex-after.json` |
| Build Windows final | **Succeeded**, 0 errores y 0 avisos; 78,859 s | `TestResults/build-summary-StandaloneWindows64.txt` y `TestResults/build-Windows.log`, copias `build-summary-final.txt` y `build-Windows-final.log` en la carpeta externa |
| Recorrido de producto final | **Passed: true**, 11 botones visibles, 1280×720 | `TestResults/AlphaProduct-1280x720-20260927-155155/product-smoke.json` |
| Identidad de procesos online | **4/4 PASS** | Salida de consola de `tools/Test-OnlineIdentity.ps1` |

Los hashes finales y todos los puntos de comparación coinciden en seis escenarios: `match-amber` (14.400 ticks), `match-legend` (13.306), `match-sunscar` (14.400), `battle` (6.000), `mass-order` (3.600) y `siege` (4.800). Son 56.506 ticks simulados por ejecución y 572 puntos registrados; la comparación mide identidad del estado, no igualdad de tiempos de CPU.

El build produce `Builds/Windows/Emberfield.exe`; el resumen de Unity registra 1.422.227.284 bytes para el producto completo. GUID del build final: `6192e735a107477d8a653f83a53ff08c`. Las cuatro partidas nativas y el último recorrido de producto corresponden a ese mismo GUID. No hubo errores de ejecución, shaders ni avisos de scripts desconocidos ausentes en sus logs; no fue necesario borrar `Library/Bee`.

El recorrido final midió 59,99 FPS y P95 de fotograma de 16,67 ms en su muestra local con límite de 60 FPS. No es una medición de grandes batallas ni de equipos móviles. La primera compilación también pasó producto, y su evidencia se conserva bajo `TestResults/AlphaProduct-1280x720-20260927-154325/`. Las suites completas 751/380 preceden a la corrección de exploración del smoke; esa corrección solo modifica la automatización de desarrollo y se validó con la nueva compilación y las cuatro ejecuciones nativas.

La versión compartida por `Server/content-version.mjs` y `Assets/Game/Networking/NetworkBuild.cs`, con protocolo 2, es:

```text
frontiers-66b1d722e3bae0f41224c291895628fd2a92a3621e95ce017392ea50ee73e31e
```

Las rutas que alimentan el hash (`Simulation`, `Networking`, mapas y `greybox.json`) están confirmadas. El pin se hizo después de confirmar ese contenido y reconstruir la autoridad antes de la suite del servidor.

SHA-256 de `Server/AuthorityWorker/out/Emberfield.Authority.dll`: `D35E1F1C92F17B84200F79CB77CA24081211A0DD21E521FFF3A8318FB04394DE`. La verificación final de `verifyContent` coincide con el pin y las cuatro rutas de contenido siguen limpias.

## Partidas nativas verificadas

| Partida | Resultado | Informe bajo `TestResults/` |
| --- | --- | --- |
| Históricas, Franceses contra Hispanos, `amber_crossing` | PASS, 128,04 s, 23 capturas | `Consolidation-20260927-online-historical-scout/summary.json` |
| Fantasía, Elfos contra Orcos, `legend_lands` | PASS, 128,79 s, 23 capturas | `Consolidation-20260927-online-fantasy-scout/summary.json` |
| Navales, Piratas contra Piratas, `sapphire_coast` | PASS, 130,79 s, 25 capturas | `Consolidation-20260927-online-naval-scout/summary.json` |
| Fantasía por HTTPS, Elfos contra Orcos, `legend_lands` | PASS, 127,61 s | `Consolidation-20260927-online-fantasy-https/summary.json` |

Las tres primeras partidas se solaparon en ejecución: seis clientes Windows y tres servicios aislados con autoridad C#, HTTP real y SQLite. No se aceleró la simulación. Cada pareja completó registro, sala, movimiento, reclutamiento, entrega real, investigación pagada de Reino y construcción. Cada jugador pagó 80 de piedra por `BuildRunCommand` de dos piezas norte-sur de 1×3 celdas (6 celdas continuas) y 40 por un `BuildCommand` girado independiente. Los trabajadores terminaron los tres edificios.

El proceso del invitado se terminó y se volvió a iniciar; se verificaron la reconexión, el avance de la autoridad durante su ausencia, los mismos IDs y huellas de muralla, los resultados victoria/derrota, las estadísticas y el historial filtrado por reino. Ambos clientes regresaron al lobby conservando selección de reino y mapa. Las configuraciones con credenciales temporales se borraron y todos los procesos propiedad de cada runner se cerraron.

La partida HTTPS usa la alpha arrancada con `tools/Start-OnlineAlpha.ps1 -Public`, el mismo pin y la autoridad reconstruida. Dirección comprobada el 27 de septiembre: `https://interval-conferencing-submitting-reach.trycloudflare.com`. El servicio y túnel de la alpha quedan activos, con identidades en `D:/CodexTooling/online-validation/alpha/state.json`; `tools/Play-OnlineAlpha.ps1` abre el ejecutable de `Builds/Windows` usando esa dirección. Es una URL temporal que depende de que este PC, servicio y túnel sigan funcionando. No se ha modificado la copia de juego de `D:/EmberfieldJugar` ni se ha incrustado la URL temporal en el cliente.

Son clientes nativos reales controlados automáticamente desde este mismo ordenador. La prueba HTTPS añade transporte por el túnel público; no equivale a dos humanos en redes físicas independientes ni valida capacidad de producción.

Los primeros intentos de Históricas y Fantasía conservan sus informes fallidos en `TestResults/Consolidation-20260927-online-historical/` y `TestResults/Consolidation-20260927-online-fantasy/`. El smoke suponía que la piedra inicial era visible, pero quedaba fuera de la visión inicial. `92d66e2` corrige exclusivamente la preparación de la prueba: dos trabajadores exploran desde su base con órdenes normales y esperan a que la piedra aparezca en la observación recibida antes de recogerla. No se reveló niebla ni se concedieron recursos. Las repeticiones completas indicadas arriba pasaron.

## Trabajo sin confirmar

Quedan tres archivos modificados pendientes de la respuesta a la consulta de autoría exigida por la solicitud del usuario:

- `docs/art/UI_UX_GUIDE.md`: documentación de comandos de voz, asociada al trabajo de Claude.
- `ProjectSettings/QualitySettings.asset`: antialiasing PC de 0 a 4; origen no confirmado.
- `Assets/Models/Units/Resources/MeshyUnits/kingdom_knight.prefab`: modificación real de jerarquía/rig para el caballo ya incorporado por Claude en `1599fae`; no es simple reserialización. Se separó del commit de controladores.

El selector de dificultad de `FrontierShell.cs` sí se confirmó: la copia congelada del 14 de septiembre permitió reconocerlo como trabajo propio anterior. Los dos cambios de assets pendientes permanecen activos en disco y, por tanto, en el ejecutable probado. Una compilación de un checkout limpio no reproduciría exactamente esos dos ajustes hasta que se decida su confirmación.

Además quedan 169 archivos sin seguimiento de distribución, copias `.blend1`, logs y ejecuciones duplicadas bajo `Artifacts/ArtReview/`. Permanecen en disco y en la copia previa. Los 454 archivos de evidencia canónica y fuentes editables seleccionados sí se confirmaron conforme a las convenciones del repositorio. El inventario externo conserva cada ruta y su motivo; la auditoría final no encontró código, modelos, materiales ni metas aprobados olvidados. `Builds/`, `TestResults/`, `Library/` y binarios temporales no forman parte de la entrega confirmada.

## Auditoría de compatibilidad

Revisión del código en disco y sus diferencias respecto a `e1c6afb`, antes de consolidar el trabajo pendiente. Esta auditoría no modificó la simulación. No encontró una incompatibilidad concreta entre esos cambios y las incorporaciones confirmadas de Claude que exigiera reescribirla.

| Archivos | Compatibilidad revisada |
| --- | --- |
| `Navigation.cs`, `NavigationReuse.cs` | `TravelOrder` conserva los signos que distinguen las cuatro cachés de rutas y que recibe la búsqueda `Staircase`. Se mantienen las optimizaciones confirmadas y el orden de visitas pendiente. |
| `InteractionSearch.cs` | Los desempates y la salida de reclutas hacia el punto de reunión o el centro conservan la búsqueda de cuerpos por huella y la construcción de una sola ruta elegida. |
| `EconomySystem.cs` | La recuperación de una celda de interacción bloqueada vuelve a buscar acceso; la entrega sigue exigiendo llegada física a un depósito propio operativo antes de acreditar recursos. |
| `CombatSystem.cs` | El cambio pendiente impide robar vida golpeando edificios y conserva la resolución de ataques simultáneos y daño de área. |
| `NetworkReplica.cs` | La sustitución de trabajadores piratas ocurre después de normalizar los asientos y antes de crear el mundo. Se conservan las tierras públicas del mapa y el giro `Turned` de edificios. |
| `EconomyControls.cs` | El cambio pendiente es el texto genérico «Workers are constructing», ya traducido en el catálogo español. Se conservan `BuildRunCommand` y los edificios girados. |
| `FactionCatalog.cs`, `FactionDefinitions.cs`, `UnitRecruitment.cs` | Los nuevos valores de facción se añaden al final del enum; las restricciones piratas cubren creación, entrenamiento y colecciones recibidas. La validación temprana usa las asignaciones de facción. |
| `OfflineMatchState.cs` | El trabajo pendiente contiene dos reglas intencionadas: las criaturas armadas capturan objetivos y una fundación de edificio central no impide la eliminación. No son cambios nuevos introducidos por esta consolidación. |

Se conservan los cambios de `legend_lands` en `ContentRealms.cs`, `FrontierShell.cs`, `OnlineControls.cs`, `Server/content-realms.mjs`, `Server/tests/service.test.mjs` y `docs/design/FACTION_GROUPS.md`, además del mapa confirmado en `bef1c46`. Es exclusivo de Fantasía y su mapa predeterminado.

La cobertura relevante de las suites ejecutadas incluye `WorkerInteractionRecoveryTests`, `ExpansionSimulationTests`, `OfflineMatchTests`, `MovementTests`, `GroupMovementTests`, `EconomyTests`, `WallRunTests`, `FactionRealmIntegrationTests` y `LegendLandsTests`. La comparación de ticks usa la referencia del árbol de trabajo capturada antes de editar, que ya contiene los cambios pendientes.

## Fuera de alcance y siguientes pasos

- Medir y optimizar `CorsairAnimationDriver` para batallas de 500 unidades.
- Evaluar una reescritura de las visitas de vecinos de `TravelOrder` en `Navigation.cs`, con su propia comparación de determinismo y mediciones.

La validación local del servidor y el determinismo no acreditan capacidad de producción por Internet, equilibrio competitivo ni rendimiento de batallas de 500 unidades. Los informes del 13 y 14 de septiembre conservan sus versiones y resultados históricos; el presente informe separa las comprobaciones de la consolidación actual.
