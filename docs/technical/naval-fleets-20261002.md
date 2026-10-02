# Validación de las tres flotas — 2 de octubre de 2026

Base: `b741a44`. La Marina inglesa y la Marina española se juegan en el reino Navales junto a los Piratas. La balandra, la fragata y el galeón forman un triángulo de contraataques. La IA naval responde a los barcos con barcos y escolta sus desembarcos. La Flota esquelética sigue bloqueada. Diseño, cifras y arte pendiente: [NAVAL_SLICE.md](../design/NAVAL_SLICE.md).

## Commits

| Commit | Contenido |
| --- | --- |
| `75f8b1f` | IA naval: flota que crece con los cascos enemigos vistos, escolta del transporte, bodega de hasta diez y ningún Recinto de bestias sin criaturas |
| `2f3035c` | Las dos marinas: reglas, cascos, triángulo, culturas de arte, selectores, catálogos, voz, prueba nativa naval y pruebas |
| `d4fe5c9` | El servidor acepta las marinas en el reino Navales |
| `b04f50c` | Versión de contenido fijada |
| `7682d8c` | Película de batalla naval (`-emberfieldNavalFilm`) |

## Determinismo

La referencia se grabó sobre `b741a44` antes de cualquier cambio (`D:/EmberfieldWorkingCache/naval2-before.json`).

| Comprobación | Resultado |
| --- | --- |
| Seis escenarios de `--scenario all` (56.506 ticks, 572 puntos de control) | `SIMULATION_TICKS_IDENTICAL` |
| `naval-pirates-english`, `naval-english-spanish` y `naval-spanish-pirates`, dos ejecuciones cada uno (35.085 ticks, 355 puntos de control) | Idénticas |

Los tres emparejamientos navales terminan por conquista en 9,5, 7,8 y 11,9 minutos de partida. `NavalFactionTests` reproduce en PlayMode los mismos ganadores y duraciones.

## Unity y servidor

- Compilación verificada; **833/833 EditMode** y **475/475 PlayMode**, sin pruebas omitidas.
- Contenido fijado a `frontiers-9c7e44558027b01361399113d1b2b4686c833370fe9c9eb69c08769453332b75`, protocolo 2. Autoridad recompilada; **45/45 pruebas de servidor**.
- Las pruebas se ejecutaron sobre el árbol de trabajo, que también contenía cambios de cosméticos aún sin confirmar de otra tarea en paralelo.

## Ejecutable y pruebas nativas

Windows se compiló con `D:/EmberfieldWorkingCache/AoE/MobileTierBuildProject`: `Builds/Windows/Emberfield.exe`, GUID `cd5af24f0e7a492da8b1b36ca90c3d3e`. El registro del reproductor no contiene scripts «(Unknown) missing».

- **Producto: `Passed: true`**, 11 controles visibles a 1280×720 ([product-smoke.json](../../TestResults/AlphaProduct-1280x720-20261002-073937/product-smoke.json)).
- **Naval en línea: PASS** en 131 s, con dos ejecutables, autoridad real y SQLite aislado ([summary.json](../../TestResults/OnlineSmoke-1280x720-2026-10-02T07-40-09-241Z-ade689/summary.json)).
  - El anfitrión juega Piratas y bota una balandra (130 de madera); el invitado juega la Marina inglesa y bota una fragata (200 de madera). Cada uno paga su astillero (150).
  - Ambos embarcan un trabajador, navegan y desembarcan. El invitado reinicia el proceso con el pasajero a bordo, lo recupera y vuelve a desembarcar.
  - La Flota esquelética aparece bloqueada en la sala naval. Ambos asientos usan almacenamiento aislado.

## Película y hoja de contactos

`Emberfield.exe -emberfieldNavalFilm D:/EmberfieldRecordings/naval-2 english_navy` grabó 963 fotogramas a 1280×720: tres fragatas inglesas contra tres galeones españoles en el mar del norte de Sapphire Coast, y una fragata que embarca cinco soldados, navega más allá de la batalla y los desembarca en la orilla. La hoja de contactos tiene 16 fotogramas, dos por toma: `D:/EmberfieldRecordings/naval-2/naval-battle-sheet.jpg`. Termina con una fragata a flote, ningún galeón y los cinco soldados en tierra.

En la hoja se distinguen los dos cascos, las barras de vida y el color de cada jugador en velas y gallardetes. El ejército del reino embarca y desembarca con sus modelos Meshy.

## Límites conocidos

- La conquista se decide en tierra: Sapphire Coast une las dos bases por tierra y la flota domina la costa. La IA naval sigue siendo sencilla; no rehúye un combate naval desfavorable ni planifica bloqueos.
- Las unidades del reino de cada marina se dibujan con la cultura de su reino (`AlphaWorldArt.Culture`). Por eso los cosméticos de color se resuelven con el reino histórico de esa cultura. Si un jugador equipa un color histórico para un lancero, también lo verá en los lanceros de su marina. La decisión corresponde a quien lleva los cosméticos.
- Las partidas guardadas no registran el rival ni la versión de contenido. Una partida pirata guardada antes de este cambio se reanuda contra la Marina inglesa.
- La Flota esquelética necesita el arte descrito en [NAVAL_SLICE.md](../design/NAVAL_SLICE.md#flota-esquelética-lo-que-falta).

No se modifica `D:/EmberfieldJugar`, no se publica al servidor público y no se hace push.
