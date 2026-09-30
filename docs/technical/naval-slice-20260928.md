# Validación del tramo naval — 28 de septiembre de 2026

Base: `4f89b74a9a74cda019c7b0d8fd70cb0d1abcb456`. Se completa el trabajo naval que estaba sin confirmar, conservando los cambios previos de personajes, facciones del desierto y audio. Diseño y controles: [NAVAL_SLICE.md](../design/NAVAL_SLICE.md).

## Cambios principales

- Astillero costero y balandra pirata, navegación independiente por agua, combate naval y contra objetivos costeros.
- Transporte con reserva de plazas, embarque, desembarque, hundimiento y carga propia restaurada al reconectar. La salud de los pasajeros y los límites de héroes/población se conservan.
- HUD para trabajadores y soldados, selección de destino desde minimapa, modelos de barcos, flotación y hundimiento. Las otras flotas permanecen bloqueadas.
- La cámara naval permite centrar barcos junto al borde del mapa; las cámaras terrestres conservan sus límites. La capacidad de transporte aparece al principio del panel de selección. Se retiran los muelles y barcos puramente decorativos de las partidas navales porque podían tapar barcos navegables y puntos de embarque.
- IA naval que construye y paga muelles, produce barcos, ataca y desembarca. Reintenta emplazamientos alternativos cuando los primeros están obstruidos.
- Separación entre la costa terrestre original y `sapphire_coast_naval.json`. El mismo resolvedor selecciona el recurso en partidas sin conexión, autoridad y réplica online; el identificador público sigue siendo `sapphire_coast`.

Se corrigieron reservas persistentes al hundirse el portador, pérdida de salud de carga en la réplica, embarque bloqueado por una tropa inmóvil y conflicto entre embarcar y subir a una muralla. Ninguna de esas correcciones modifica el buscador de rutas terrestre.

El archivo de partida también conserva las órdenes de embarcar y desembarcar. El formato pasa a versión 3 para que los clientes antiguos rechacen una partida que no podrían reconstruir; esta versión sigue leyendo los formatos 1 y 2. Se comprueba la restauración tanto con un pasajero a bordo como después de desembarcar en la orilla opuesta.

## Determinismo

Se exportó el commit limpio `4f89b74` antes de comparar. El serializador de hashes conserva todos los campos antiguos y omite únicamente los nuevos campos navales cuando tienen su valor por defecto; una prueba adicional comprueba que cada uno de esos diez campos altera el hash al activarse.

| Comprobación | Resultado |
| --- | --- |
| Seis escenarios existentes de `--scenario all` | `SIMULATION_TICKS_IDENTICAL`: 56.506 ticks y 572 checkpoints |
| Sapphire Coast histórico, caso adicional con el mismo guion en ambas versiones | `SIMULATION_TICKS_IDENTICAL`: 11.698 ticks y 118 checkpoints |
| Archivo terrestre `sapphire_coast.json` | Idéntico byte a byte al commit base |
| Dos ejecuciones del escenario naval | Idénticas: 14.400 ticks y 145 checkpoints cada una |
| Campos navales incluidos en el hash cuando están activos | 10/10 comprobados |

Total terrestre conservado: **68.204 ticks y 690 checkpoints**. La partida naval nueva termina el intervalo medido todavía en curso; esa repetición demuestra determinismo, no victoria ni equilibrio estratégico.

Un diagnóstico separado sobre Sapphire Coast naval registró dos muelles, 24 balandras producidas, 374 proyectiles navales, 34 pasajeros distintos embarcados y 12 desembarcados vivos durante 14.400 ticks. No detectó discrepancias entre las posiciones de la carga y su portador. Estas cifras corresponden a ese escenario concreto.

## Unity

Compilación verificada; **806/806 EditMode** y **450/450 PlayMode** pasan, sin pruebas omitidas. Los resultados completos están en `TestResults/EditMode.xml` y `TestResults/PlayMode.xml`. Las nuevas pruebas cubren navegación, combate, transporte, autoridad de órdenes, carga de réplica, IA, embarque desde el HUD, selección de orilla desde el minimapa y materiales de propietario de los barcos. La última pasada de PlayMode incluye el encuadre naval en las cuatro esquinas con rotación/zoom, el contador de pasajeros antes de los detalles de combate, dos casos de restauración naval y diez casos de aislamiento de almacenamiento.

## Ejecutable y prueba nativa final

Windows se compiló mediante `D:/EmberfieldWorkingCache/AoE/MobileTierBuildProject`. El ejecutable está en `Builds/Windows/Emberfield.exe`; GUID final: `e98e1eb31c954a74bd2ad3031f47d2c8`. SHA-256 de `Emberfield_Data/Managed/Emberfield.Presentation.dll`: `8568e72e02bb6e7e83ccedc3840fd82e7456532ff79a999e722a9b2a570ac94a`. El `.exe` es el lanzador de Unity y su hash puede mantenerse entre builds; el GUID y los ensamblados identifican esta entrega.

- **Producto: `Passed: true`**, 11 controles visibles a 1280×720. Resultado: [product-smoke.json](../../TestResults/AlphaProduct-1280x720-20260928-191147/product-smoke.json). Se observaron 59,93 FPS y cero pausas por encima del umbral en los intervalos muestreados; el recorrido incluye avance acelerado entre esos intervalos y no constituye una prueba de rendimiento de grandes batallas.
- **Naval nativo: `Passed: true`**, dos ejecutables Windows, autoridad real y SQLite aislado, sin acelerar la simulación. Duración: 122,44 segundos. Ambos jugadores pagaron astillero y balandra, embarcaron, navegaron, desembarcaron y volvieron a embarcar. El proceso invitado fue terminado y reiniciado; recuperó barco y pasajero, volvió a desembarcar y ambos clientes conservaron el resultado en su historial. Resultado: [summary.json](../../TestResults/Naval-20260928-native-final/summary.json).
- Los tres procesos de cliente utilizados en esa prueba naval —host, guest y guest reiniciado— emplearon almacenamiento de prueba; los informes de ambos asientos registran `IsolatedStorage: true` y raíces distintas en `sync/local-state/host-config` y `sync/local-state/guest-config`. El recorrido de producto empleó su propio `local-state`. Se borraron los archivos de configuración con credenciales y se cerraron los procesos creados por el runner.

Se revisaron seis capturas finales del ciclo naval. El barco del borde está centrado, el contador `1/6 a bordo` es legible y la trabajadora desembarcada reaparece en tierra. La palmera existente oculta parcialmente el barco del host en una captura; el guest permite revisar el casco y la vela completos. El borde exterior muestra una transición a mar plano, sin ocultar la selección.

| Evidencia visual | Captura |
| --- | --- |
| Barco y contador antes de corregir el encuadre | [Antes](../../TestResults/Naval-20260928-native/guest/naval-cargo-before-reconnect.png) |
| Barco centrado y carga visible en la versión final | [Después](../../TestResults/Naval-20260928-native-final/guest/naval-cargo-before-reconnect.png) |
| Astillero construido y barco operativo | [Astillero](../../TestResults/Naval-20260928-native-final/host/naval-dock-and-ship.png) |
| Unidad desembarcada en tierra | [Desembarco](../../TestResults/Naval-20260928-native-final/guest/naval-worker-landed.png) |

Estas comprobaciones usan dos clientes en este ordenador y HTTP loopback. No validan un servidor público ni redes físicas independientes.

## Carga online local

`tools/Test-OnlineLoad.ps1 -Matches 64 -Seconds 45` terminó con **PASS**: 64 partidas y 128 clientes durante 45,20 segundos, distribuidos en 22 históricas, 21 de fantasía y 21 navales. Se validaron 28.256 snapshots, 5.763 órdenes aceptadas, ocho reconexiones y 128 historiales, sin errores inesperados. Al terminar, se liberaron las plazas y se pudo iniciar una partida de reemplazo.

El promedio observado fue 19,998 ticks/s; latencia p95 de snapshots 65,51 ms y de órdenes 70,81 ms. Autoridad SHA-256: `8baadbba14a1f04b5fa06853888d7176657c1ff94c06a6157439053d9c0525f5`. Reporte: `D:/EmberfieldWorkingCache/naval-completion-20260928/load64/report.json`.

Esta carga usa HTTP loopback, ejércitos iniciales, cinco snapshots/s y aproximadamente una orden/s por jugador. No equivale a 64 batallas de grandes flotas ni certifica Internet, equipos físicos independientes o capacidad de producción.

## Evidencia local

Contenido cliente-servidor fijado a `frontiers-73e9ff1c4465fab8de3c93ec06ff2f354131166454fd88b1fe038a5f1db7a17f`, protocolo 2. La autoridad se recompiló y las **41/41 pruebas de servidor** pasaron. Implementación confirmada en `c6714fe`; pin confirmado en `6db807d`; correcciones de presentación, guardados y aislamiento de las pruebas en `dcaca8b`.

La copia de seguridad inicial y las mediciones de simulación están en `D:/EmberfieldWorkingCache/naval-completion-20260928/`. Incluyen `incoming-working-tree.patch`, `incoming-index.patch`, `incoming-naval-untracked.tar`, baseline limpio, comparaciones `wip-routed-land-compare.log` y `wip-routed-coast-land-compare.log`, repetición naval y comprobación de hashes de entradas. Se conservan también los resultados fallidos que motivaron las correcciones.

La auditoría final encontró que el aislamiento previo de PlayMode no cubría los ejecutables de smoke: su inicialización podía leer ajustes reales y escribir o limpiar diagnósticos. Las primeras ejecuciones nativas no acreditan aislamiento y no existe una captura anterior que permita certificar retrospectivamente que esos datos quedaron intactos. Se corrige resolviendo una carpeta `local-state` por ejecución y cliente antes de inicializar ajustes, diagnósticos o partidas guardadas, y aislando las preferencias del servidor y cosméticos. Los argumentos de smoke inválidos fallan sin recurrir a los datos reales. El arranque normal conserva su almacenamiento habitual.

Para la pasada final se compararon hashes de los archivos de `Alpha`, `saves` y las preferencias `Emberfield.*` antes y después de PlayMode y de ambos recorridos nativos: **sin cambios**. El perfil observado contenía un archivo y una preferencia del juego. Evidencia: `D:/EmberfieldWorkingCache/naval-completion-20260928/native-user-storage-verification.json`. Esta comparación corresponde a esa pasada final, no a ejecuciones anteriores.

No se modifica `D:/EmberfieldJugar`, no se publica al servidor público y no se hace push. `ProjectSettings/QualitySettings.asset`, `docs/art/UI_UX_GUIDE.md` y los `Artifacts/*` existentes quedan fuera de los commits navales.
