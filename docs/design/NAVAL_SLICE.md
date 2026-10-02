# Batallas navales — 2 de octubre de 2026

El reino Navales tiene tres flotas jugables en Sapphire Coast: **Piratas**, **Marina inglesa** y **Marina española**. Cada una construye el mismo astillero costero y bota su propio buque de guerra, que combate, bombardea la costa y transporta tropas. La **Flota esquelética** sigue bloqueada hasta que existan sus tripulaciones (última sección).

El primer tramo naval (`c6714fe`–`ba3ae3f`) incorporó el astillero, la navegación por agua, el combate naval y los desembarcos con la balandra pirata. Este tramo añade las dos marinas, el triángulo de cascos y la IA que las juega.

## Las tres flotas

| Flota | Buque (astillero) | Ejército de tierra | Bonificación |
| --- | --- | --- | --- |
| Piratas (`pirates`) | Balandra pirata (`pirate_sloop`) | Su tripulación (buscadoras de tesoros, saqueadores de abordaje, corsarios de pólvora y el Corsario Carmesí), además de lanceros, arqueros y jinetes | Las buscadoras de tesoros cargan dos recursos más por viaje |
| Marina inglesa (`english_navy`) | Fragata inglesa (`english_frigate`) | Ejército del reino: aldeanos, lanceros, arqueros, jinetes y asedio | Sus tropas a distancia ganan un punto de armadura |
| Marina española (`spanish_navy`) | Galeón español (`spanish_galleon`) | Ejército del reino, como los tercios | Su infantería gana un punto de armadura |

- Cada flota bota solo su casco. Las marinas no construyen la galera compartida (`war_galley`), que queda para los mapas costeros de los otros reinos. Si se intenta, la orden se rechaza con *Esta marina navega en su propio buque de guerra, no en la galera*.
- Las marinas reutilizan el arte existente. Los soldados y la ciudad de la Marina inglesa son los modelos Meshy del reino que usan los Ingleses; los de la Marina española, los de los Hispanos. La máscara de equipo los tiñe del color del jugador (`AlphaWorldArt.Culture`). Los cascos son los modelos importados `english_frigate` y `spanish_galleon`. Un buque cuyo identificador coincide con un modelo usa ese modelo, sea cual sea su dueño (`MeshyPropVisuals.ResolveShip`). En pantalla, la balandra mide 3,0 m, la fragata 3,5 m y el galeón 3,8 m; las reglas usan el mismo disco de un metro para los tres.
- Sin conexión, cada flota se enfrenta a la siguiente: Piratas contra Marina inglesa, Marina inglesa contra Marina española y Marina española contra Piratas.
- La bonificación pirata compensa que la buscadora de tesoros cueste 90 recursos, frente a los 50 del aldeano. Sin ella, las IA piratas perdían 11 de 12 partidas contra las marinas.

## Los cascos y el triángulo

| Casco | Coste | Pob. | Vida | Armadura | Velocidad | Alcance | Disparo | Bonos | Plazas |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Balandra | 20 comida, 130 madera | 2 | 240 | 1 | 4,8 m/s | 3,5 m | 8 cada 0,8 s | ×1,8 contra barcos (abordaje), ×1,3 contra edificios | 6 |
| Fragata | 40 comida, 200 madera | 3 | 380 | 0 | 4,0 m/s | 6,5 m | 18 cada 2 s | ×3,4 contra casco pesado, ×1,2 contra edificios | 5 |
| Galeón | 60 comida, 280 madera | 4 | 600 | 10 | 2,8 m/s | 5,5 m | 32 cada 2,4 s | ×1,5 contra edificios | 10 |

Etiquetas: la balandra es `Naval | Light`, la fragata solo `Naval` (casco medio) y el galeón `Naval | Heavy`.

**La balandra vence a la fragata, la fragata al galeón y el galeón a la balandra.**

- **La balandra vence a la fragata.** Es más rápida, así que la fragata no puede escapar ni aprovechar su alcance. Su abordaje (×1,8 contra cualquier barco) impacta de lleno en un casco sin armadura.
- **La fragata vence al galeón.** Tiene más alcance y velocidad, y sus cañones largos (×3,4 contra casco pesado) atraviesan la armadura 10 del galeón.
- **El galeón vence a la balandra.** Su armadura 10 reduce cada golpe de la balandra a 4 de daño, y su andanada, la más fuerte de los tres, hunde una balandra en ocho disparos. La andanada no tiene bonificación contra tropas: el galeón no barre una playa por ser galeón.

Duelos controlados en mar abierto, con cada casco atacando al enemigo más cercano y cada flota probada en ambos asientos (`NavalFactionTests.TheFavouredHullWinsADuelFromEitherSeat`, mismos resultados en los dos asientos):

| Duelo | Recursos | Ganador | Duración | Supervivientes / vida |
| --- | --- | --- | --- | --- |
| 1 balandra contra 1 fragata | 150 contra 240 | Balandra | 23,0 s | 1 / 36 |
| 1 fragata contra 1 galeón | 240 contra 340 | Fragata | 23,1 s | 1 / 60 |
| 1 galeón contra 1 balandra | 340 contra 150 | Galeón | 17,9 s | 1 / 516 |
| 8 balandras contra 5 fragatas | 1.200 contra 1.200 | Balandras | 24,3–24,5 s | 7 / 1.153–1.187 |
| 7 fragatas contra 5 galeones | 1.680 contra 1.700 | Fragatas | 19,1 s | 6–7 / 1.736–1.828 |
| 3 galeones contra 7 balandras | 1.020 contra 1.050 | Galeones | 44,3–44,4 s | 3 / 956–960 |

Uno contra uno, el casco favorecido gana por poco (salvo el galeón contra la balandra). A igual coste, el contraataque es claro: el ganador conserva casi todos sus barcos.

Frente a la costa: la fragata alcanza lo mismo que una atalaya (6,5 m) y supera a los arqueros (5 m). El galeón apenas recibe daño de las flechas (armadura 10); los corsarios de pólvora (×1,5 contra casco pesado), las fragatas y las fortificaciones son su respuesta. La balandra debe acercarse a 3,5 m para disparar.

## Recorrido de juego

1. Revelar una costa y construir el astillero (`dock`, 150 de madera) con un trabajador. Su huella ocupa tierra libre junto al agua navegable.
2. Con población libre, entrenar el buque de la flota: balandra (2 de población), fragata (3) o galeón (4).
3. Seleccionar unidades terrestres, pulsar **Embarcar** y elegir un barco propio junto a una costa accesible. La balandra carga seis unidades, la fragata cinco y el galeón diez; las reservas de embarque cuentan para ese límite.
4. Seleccionar el barco para navegar o atacar. **Desembarcar** permite elegir una orilla en el mundo o en el minimapa. El barco se aproxima por agua y deposita la carga en tierra despejada y conectada al destino.

El embarque tarda un segundo una vez alcanzado el casco. Las unidades transportadas dejan de aportar visión y de combatir, pero siguen consumiendo población. Si no hay espacio al desembarcar, los pasajeros que no caben permanecen a bordo. Al hundirse un barco, mueren sus pasajeros y se cancelan las reservas pendientes. Una orden nueva cancela el embarque o desembarco anterior.

## Navegación y combate

El dominio terrestre conserva su navegación. El dominio naval usa la intersección `WaterCells ∩ BlockedCells`: agua marcada como profunda e infranqueable para las tropas. Los puentes y vados, que ya eran agua transitable a pie, siguen siendo terrestres y no admiten barcos. Las rocas sin agua no se convierten en mar.

`sapphire_coast_naval.json` conecta el mar por los bordes norte y sur y abre pasos alrededor de las islas. La máscara de agua profunda es simétrica entre jugadores. Solo el reino Navales carga esta variante; Históricas y Fantasía conservan `sapphire_coast.json`. El identificador público sigue siendo `sapphire_coast`, y el cliente sin conexión y la autoridad resuelven el recurso con la misma función de `ContentRealms`.

Los barcos atacan barcos y objetivos costeros a su alcance. Solo los ataques a distancia alcanzan un barco; las tropas cuerpo a cuerpo no persiguen objetivos por agua. Los barcos no capturan objetivos de tierra. Conquista exige destruir los edificios centrales, que están en tierra: el mar se gana con barcos, pero la partida se decide con los desembarcos y el ejército.

## Inteligencia artificial

La IA sin conexión juega las tres flotas con órdenes normales, sin recursos ni información extra (`OfflineAi`, solo en el reino Navales):

- Construye y paga el astillero cuando tiene seis trabajadores, y entrena el único casco de su flota.
- Mantiene la flota de su dificultad (2, 3 o 4 barcos) y un transporte. Por cada casco enemigo visto en los últimos tres minutos añade un buque de guerra más, hasta duplicar la flota.
- Los buques libres atacan primero a los barcos enemigos a la vista. Mientras el transporte lleva una partida de desembarco, los buques de guerra lo escoltan hasta el agua frente a esa playa. Allí solo combaten barcos o lo que haya a menos de 10 m de la playa. Después vuelven a hostigar la costa enemiga.
- El transporte embarca a los soldados más cercanos que no estén luchando, hasta llenar la bodega (máximo diez), y los desembarca en la orilla libre más cercana al enemigo.
- Ninguna facción construye un Recinto de bestias si no tiene criaturas que entrenar en él. Esto también corrige el recinto vacío que levantaban los Ingleses y los Piratas.

Partidas naturales en dificultad difícil (`tools/Verify-SimulationTicks.ps1 --scenario naval-pirates-english,naval-english-spanish,naval-spanish-pirates`; `NavalFactionTests.ANaturalHardNavalMatchEndsInConquest` obtiene los mismos resultados):

| Emparejamiento | Resultado | Duración | Barcos botados (por asiento) | Escoltas (por asiento) | Embarques / desembarcos (total) |
| --- | --- | --- | --- | --- | --- |
| Piratas contra Marina inglesa | Conquista de la Marina inglesa | 9,5 min | 6 / 6 | 3 / 0 | 4 / 4 |
| Marina inglesa contra Marina española | Conquista de la Marina inglesa | 7,8 min | 4 / 2 | 2 / 1 | 9 / 2 |
| Marina española contra Piratas | Conquista de los Piratas | 11,9 min | 3 / 11 | 1 / 6 | 6 / 9 |

Dos ejecuciones de cada emparejamiento dan hashes idénticos en todos los puntos de control. En un sondeo más amplio (tres dificultades y los dos órdenes de asiento, 18 partidas), todas terminaron por conquista entre los 7,8 y los 19,3 minutos. Los Piratas ganaron 7 de sus 12 partidas contra las marinas, y la Marina inglesa y la española quedaron 3 a 3. Estas partidas miden que la IA juega y termina, no un equilibrio competitivo.

## Autoridad, servidor y reconexión

Las órdenes de embarque y desembarque pasan por el protocolo y la simulación autoritativa. Se comprueban propiedad, dominio, capacidad, rutas y espacio de desembarco. El estado de carga propio conserva identificadores, salud y posición del portador, y se valida al reconstruir la réplica. La carga enemiga no revela su manifiesto. Los límites de héroes y población incluyen a los pasajeros.

El servidor acepta `english_navy` y `spanish_navy` en el reino Navales (`Server/content-realms.mjs`) para salas, colas, clasificación e historial. Los rechaza en Históricas y Fantasía, igual que rechaza `skeleton_fleet` en cualquier reino. La autoridad comparte `ContentRealms`, así que crea las partidas de los tres emparejamientos. La prueba nativa naval (`tools/Smoke-Online.ps1 -Realm naval -NavalSlice`) enfrenta a los Piratas (anfitrión, balandra) con la Marina inglesa (invitado, fragata). Ambos pagan su astillero y su casco, embarcan, navegan y desembarcan, y el invitado reinicia el proceso con un pasajero a bordo.

## Flota esquelética: lo que falta

La Flota esquelética sigue bloqueada en los selectores, el servidor y la autoridad. Su casco existe (`skeleton_ghost_ship`, importado en `Assets/Models/Props/caribbean`), pero no hay tripulaciones de esqueletos y quedan pocos créditos de Meshy. Para desbloquearla hace falta este arte, con la tubería de `docs/art/meshy-pipeline.md` (máscara de equipo en el alfa del color base, rig y clips de caminar, correr, atacar y morir, y dos niveles de detalle):

| Papel | Modelo necesario | Notas |
| --- | --- | --- |
| Trabajador | Grumete esqueleto con saco o pala | Recolecta y construye; sustituye al aldeano |
| Infantería | Esqueleto de abordaje con alfanje y escudo | Papel de lancero |
| A distancia | Mosquetero esqueleto | Papel de arquero; proyectil de pólvora como los corsarios |
| Caballería | Jinete esqueleto sobre caballo fantasma | Cuadrúpedo: plantilla de Blender de `tools/art/rig_quadruped.py`, un solo modelo de jinete y montura |
| Héroe | Capitán fantasma | Unidad única con límite de una, como el Corsario Carmesí |
| Pueblo (opcional) | 9 edificios hundidos o de restos de naufragio, más muralla, puerta y escalera de asedio | Mientras no existan, puede usar el pueblo pirata o el procedural |

Coste estimado, con los precios de las tandas anteriores: unos 35 créditos por personaje humanoide rigado (4 × 35 = 140), unos 15 por el jinete con montura y unos 15 por edificio (135 por los nueve). En total, unos 155 créditos sin pueblo propio y unos 290 con él. Además de las reglas (facción `skeleton_fleet` con `FactionKind = 12`, roster, bonificación, casco con su lugar en el triángulo), hay que añadirla a `ContentRealms`, al servidor, a los textos y a sus pruebas.

## Verificación reproducible

- `tools/Verify-Unity.ps1 -Stage Compile`, después `EditMode` y `PlayMode`. `NavalFactionTests` cubre el reino y los rivales, cada casco y su facción, el astillero, el casco y el ejército de cada flota en Sapphire Coast, los duelos del triángulo, una partida natural de la IA por emparejamiento, la ida y vuelta de una instantánea con un casco cargado y la cultura de cada marina.
- `tools/Verify-SimulationTicks.ps1 --scenario all --compare <base.json>`: los seis escenarios terrestres siguen siendo idénticos. Los escenarios navales `naval-*` se ejecutan por nombre.
- Tras confirmar Simulation, Networking, Maps y greybox: `node Server/update-content-version.mjs`, `tools/Build-Authority.ps1` y `npm test` dentro de `Server`.
- Ejecutable: `tools/Build-Unity.ps1 -Target Windows -ProjectPath D:/EmberfieldWorkingCache/AoE/MobileTierBuildProject`.
- Transporte y reconexión con dos ejecutables: `tools/Smoke-Online.ps1 -Realm naval -NavalSlice -TimeoutSeconds 360`. Recorrido de producto: `tools/Smoke-AlphaProduct.ps1`.
- Película de revisión, solo en versiones de desarrollo: `Emberfield.exe -emberfieldNavalFilm <carpeta> [facción]`. Graba las dos flotas, las andanadas y un desembarco, con `frames.csv` para `tools/art/encode_film.py`. Resultados del 2 de octubre: [validación de las tres flotas](../technical/naval-fleets-20261002.md).
