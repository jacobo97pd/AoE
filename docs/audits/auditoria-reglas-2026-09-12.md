# Auditoría de las reglas del juego (12 de septiembre de 2026)

Esta auditoría compara tres cosas: las reglas que describen los documentos de diseño, los datos que las fijan (`greybox.json` y los mapas) y el código de la simulación que las aplica. Para no depender solo de la lectura, cada conclusión relevante se comprobó en ejecución. El juego estaba en Alpha 0.3, con la tripulación pirata sin confirmar en git (último commit `7427316`).

## Estado tras las correcciones (13 de septiembre de 2026)

Los hallazgos de abajo se conservan tal como se encontraron. Las correcciones se hicieron en dos rondas el 13 de septiembre; esta tabla resume el estado final:

| # | Hallazgo | Estado | Cambio |
| --- | --- | --- | --- |
| 1 | Guardián y Trol no capturan balizas | Corregido | Captura toda unidad armada con Infantry, Cavalry, Ranged o Creature. El ariete, los trabajadores y los puestos plegados siguen sin capturar. |
| 2 | Aven domina el reino histórico | Corregido | Pasivas reajustadas con muestras de 288 partidas. Aven: carga +1 (antes +2) y carta de Muster +10 % (antes +20 %). Serevin: caballería +15 % (antes +5 %) y carga +1. Miraj: carga +1 (antes +4). Skeld: su armadura +1 cubre también la caballería. Drakeforged: carga +1. |
| 3 | Piratas por debajo de su coste | Corregido | Cada pirata tiene un papel propio. Las cifras están en la [lista de unidades](../design/UNIT_ROSTER.md). |
| 4 | La IA no entrena piratas | Corregido | Rota entre todos los soldados que puede entrenar y pagar, y recluta una buscadora de tesoros. |
| 5 | IA pasiva y sin niveles | Corregido | Tres niveles elegibles en la escaramuza: Fácil, Normal (por defecto) y Difícil. Normal ataca desde el minuto 7 y Difícil desde el 6. Difícil gana la mayoría de los rushes del jugador guionizado, también los del minuto 5. |
| 6 | Piratas abiertos a las cuatro facciones | Decidido | Son una compañía común del reino histórico, no una facción. |
| 7 | Las mejoras de armas escalan el ariete | Corregido | Las mejoras de armas usan las etiquetas 142, sin Siege. Las de armadura siguen con 398. |
| 8 | El robo de vida cura contra edificios | Corregido | Solo cura al golpear unidades. |
| 9 | Un Hearth en construcción mantiene viva la facción | Cambiado | Solo cuenta un Hearth terminado. En la sonda, la partida termina al caer el original, a los 20,9 s. |
| 10 | Ritmo | Medido | Conquest: de 7,8 a 16,7 minutos, con una media de 10,1. Dominion: de 12,8 a 22,6, con una media de 17,5. |
| 11 | Documentación desfasada | Corregido | Se actualizaron el documento de diseño, las listas de unidades y edificios, el árbol tecnológico (17 investigaciones), la biblia de facciones, el diseño de mapas, la simulación de la expansión, la navegación y la integración pirata. |

### Cifras finales

**Partidas naturales oficiales** (`Verify-ExpansionSimulation.ps1`): las 48 terminan por las reglas del juego, sin fallos.
- **Histórico:** Aven gana 9 de 18, Serevin 6 de 12, Miraj 3 de 6 y Skeld 6 de 12. Las cuatro facciones quedan al 50 %; en la auditoría, Aven ganaba 18 de 18 y Serevin y Skeld ninguna.
- **Fantasía:** Verdant gana 9 de 12, Drakeforged 6, Solar 5 y Ashen 4.
- **Asiento:** el jugador 1 gana 12 de 24 en Conquest y 13 de 24 en Dominion.
- **Piratas en las IA históricas:** saqueador y corsario de pólvora en 48 de 48, Capitán en 41 y buscadora en 29. Antes solo aparecía el Capitán.

**Muestra ampliada:** 288 partidas con `--natural`, en las que cada mapa se juega desde las dos bases y con tres desfases de tiempo.
- **Histórico:** Aven gana el 42 %, Serevin el 57 %, Miraj el 61 % y Skeld el 50 %.
- **Fantasía:** Solar gana el 39 %, Verdant el 61 %, Ashen el 50 % y Drakeforged el 49 %.
- **Lado:** la primera base gana el 47 % en Conquest y el 46 % en Dominion; antes del arreglo de simetría ganaba el 71 % en Dominion. Por mapa, los resultados varían de una muestra a otra dentro del ruido estadístico.

**Combates piratas**, con 720 recursos por bando:
- **Saqueadores:** vencen a Reedguards (1 superviviente de 9) y a corsarios de pólvora. Pierden contra Striders y Stringwardens.
- **Corsarios de pólvora:** vencen a Reedguards (4 de 9). Pierden contra Stringwardens y Striders.
- **Capitán:** vence a tres Reedguards, tres Striders o tres Stringwardens.

**Escalera de IA:** 24 partidas por pareja, en Conquest, con cada nivel en los dos asientos.
- Difícil gana a Normal 24–0.
- Normal gana a Fácil 21–3.
- Difícil gana a Fácil 24–0.

**Jugador guionizado contra la IA:** 48 partidas por nivel, en 3 mapas × 4 facciones, con ataques desde el minuto 3 o el 5 y oleadas de 12 o 20 unidades.
- La IA gana 0 con Fácil, 17 con Normal y 28 con Difícil.
- Contra los ataques del minuto 5, Difícil gana 13 de 24 y Normal 4.

### Hallazgos nuevos y su corrección

- **Ventaja de lado en la simulación.** En la primera ronda parecía un sesgo de asiento en Dominion. Con una muestra ampliada, en la que cada mapa se juega desde las dos bases, resultó ser de lado: la primera base ganaba el 71 % en Dominion.
  - **Descartes:** los mapas son espejos exactos, y el orden de las entidades no influía (experimento `--renumber`).
  - **Detector:** `--divergence` juega la misma partida en el mapa y girada 180°, y compara órdenes y posiciones en cada tick. Localizó tres causas:
    - Las unidades nuevas salían siempre por la esquina inferior izquierda del edificio (`InteractionSearch.SpawnPoint`). Ahora salen por la casilla libre más cercana al punto de reunión o al centro del mapa.
    - La navegación expandía los vecinos en un orden fijo (+x, +z, −x, −z), lo que favorecía moverse hacia +x/+z uno o dos ticks por trayecto. Ahora el orden sigue la dirección del viaje.
    - La IA colocaba, exploraba y se reagrupaba con búsquedas no reflejadas. Ahora usa un marco espejo respecto al centro del mapa.
  - **Resultado:** tras los cambios, la primera base gana el 49 % en Conquest y el 49 % en Dominion.
- **Prueba de sombreado.** `PirateCrewBaker` restauraba el pipeline de render desde referencias que el build dejaba inválidas. El proyecto de build quedaba así guardado sin URP, y ahí fallaba `CosmeticShaderRenderTests`. Ahora restaura por ruta, y `Build-PirateCrew.ps1` vuelve a copiar los ajustes del proyecto al terminar. La prueba pasa en los dos proyectos.
- **Otros fallos de la IA:**
  - Un Muster Hall se quedaba parado esperando metal para un pirata que no podía pagar. Ahora solo elige lo que puede pagar.
  - La unidad de facción llenaba el ejército, con Ashrunners en Serevin y Frostguards en Skeld. Ahora ocupa como mucho un tercio.
  - En Normal y Difícil, el combate ya no redirige cada ciclo a todas las unidades hacia un único objetivo, sacando de la pelea a las que ya estaban combatiendo. Tampoco retira heridos mientras defiende en casa.
  - Una investigación en el Muster Hall detenía el entrenamiento en todos ellos. En Normal y Difícil ya solo espera el edificio que investiga.
- **Cierre del player con Direct3D 12.** El build final se cerraba con un fallo de acceso (0xC0000005 en `D3D12Core.dll`) justo después de que el smoke escribiera su informe.
  - **Cuándo ocurría:** solo con Direct3D 12 y los graphics jobs multihilo, y al salir en el mismo fotograma que una captura.
  - **Cuándo no:** los builds anteriores cerraban bien, y el mismo build también con `-force-d3d11` o `-force-gfx-direct`.
  - **Arreglo:** `SafeQuit` espera a que se presente el fotograma en curso y dos más antes de salir. Lo usan el botón EXIT GAME y el smoke del menú. Con él, tres cierres seguidos en Direct3D 12 terminan con código 0.
- **Muestras pequeñas.** Con 12 partidas por facción, el resultado de Drakeforged oscilaba entre 2 y 8 victorias tras cambios menores de la IA. El reajuste final se decidió con muestras de 288 partidas (`--natural`), que cubren las dos bases y tres desfases de tiempo, y comparando candidatos con `--rules`.

### Verificación

- **EditMode:** 484 de 484. Son cuatro más que en la auditoría, todas nuevas: Hearth sin terminar, Hearth terminado, captura por criaturas y asedio, y robo de vida.
- **PlayMode:** 152 de 152 en el proyecto principal. Incluye la prueba de sombreado y las de facciones, que ahora calculan sus cifras a partir de los datos.
- **Servidor:** 25 de 25, con la versión de contenido `frontiers-4aae23c4…` regenerada.
- **Build de desarrollo** `4431ed71df144beb92ff9cc8c57186c1`:
  - El smoke del menú elige Difícil con el botón nuevo, comprueba que la partida arranca con esa IA y pasa.
  - Tres ejecuciones seguidas en Direct3D 12 se cierran limpiamente.
  - El proyecto de build conserva URP tras compilar.

## Método

Documentos revisados:
- [Documento de diseño](../design/GAME_DESIGN_DOCUMENT.md).
- Listas de [unidades](../design/UNIT_ROSTER.md) y [edificios](../design/BUILDING_ROSTER.md).
- [Árbol tecnológico](../design/TECHNOLOGY_TREE.md).
- [Biblia de facciones](../design/FACTION_BIBLE.md).
- [Diseño de mapas](../design/MAP_DESIGN.md).
- [Informe de la fase 7](../tasks/PHASE_7_REPORT.md).
- [Expansión Alpha 0.3](../tasks/ALPHA_03_EXPANSION.md) y [su simulación](../technical/EXPANSION_SIMULATION.md).

Código revisado: los sistemas de `Assets/Game/Simulation`, que cubren economía, producción, construcción, combate, investigación, facciones, asedio, niebla, fin de partida e IA.

Evidencias:
- **Pruebas EditMode:** 480 de 480 superadas.
- **48 partidas naturales de IA contra IA** con las reglas actuales, combinando 3 mapas, 8 facciones y los modos Conquest y Dominion. Se lanzaron con `tools/Verify-ExpansionSimulation.ps1 -NaturalOnly`. Todas terminaron por las reglas del juego; ninguna agotó el tiempo.
- **Sondas dirigidas** con un arnés .NET que compila la simulación real sin Unity, mediante `tools/Verify-RulesAudit.ps1`. Cubren la captura de balizas por tipo de unidad, un Hearth de reemplazo y 26 combates de las unidades piratas.
- **Un jugador guionizado** (`Assets/Game/Diagnostics/ScriptedCommander.cs`). Juega solo con órdenes legales y de pago, y ve al rival a través de su propia niebla. Jugó 12 partidas en el arnés y una completa grabada en el juego compilado.

## Resumen

Las reglas centrales son coherentes y están bien protegidas por pruebas. La economía, la población, la construcción, la investigación, el combate, la niebla y el fin de partida hacen lo que dicen los documentos.

Los problemas están en tres zonas:
- **Un fallo real en Dominion:** dos unidades de facción no capturan balizas.
- **Equilibrio:** el reino histórico lo domina Aven, y las unidades piratas rinden menos de lo que cuestan y la IA no las usa.
- **La IA:** es pasiva y se puede ganar en unos cinco minutos atacando antes de su cuarta era.

Además, varios documentos se han quedado atrás respecto a los datos.

## Hallazgos

### 1. Alta: el Guardián del Bosque y el Trol de Guerra no capturan balizas de Dominion

`OfflineMatchState.UpdateObjective` solo cuenta unidades armadas que no sean trabajadores y tengan la etiqueta Infantry, Cavalry o Ranged ([OfflineMatchState.cs:122](../../Assets/Game/Simulation/OfflineMatchState.cs#L122)). `grove_guardian` y `war_troll` son Creature + Heavy. Esa elección es deliberada en [la simulación de la expansión](../technical/EXPANSION_SIMULATION.md), para que no sufran también la ventaja de los arqueros contra infantería. El efecto secundario es que no capturan ni disputan balizas.

Sonda: se dejó cada unidad 20 s sola en la baliza central de Sapphire Coast; la captura requiere 15 s.
- **Capturan:** Reedguard, saqueador, Capitán, corsario de pólvora, Frostguard, Elefante, León Solar y Draco.
- **No capturan:** Guardián, Trol y ariete. Lo del ariete parece intencionado.

**Impacto:** las unidades distintivas de Verdant y Ashen no cuentan en Dominion, y la IA las manda a las balizas igualmente.

**Propuesta:** añadir Creature a la máscara de captura, o un campo explícito por unidad, con una prueba.

### 2. Alta: Aven domina el reino histórico

Resultado de las 48 partidas de IA contra IA:
- **Histórico:**
  - Aven gana **18 de 18**; Serevin y Skeld, **0 de 12** cada una.
  - Miraj gana 6 de 6, pero solo se enfrenta a Skeld.
- **Fantasía:** Verdant 8/12, Drakeforged 7/12, Solar 6/12, Ashen 3/12.

El jugador guionizado ganó 10 de sus 12 partidas. Solo perdió las dos que jugó con Serevin y con Skeld contra una IA Aven.

**Límites de la muestra:**
- La política de IA es la misma en los dos lados.
- El rival está fijado por facción ([ContentRealms.cs:23](../../Assets/Game/Simulation/ContentRealms.cs#L23)), así que Miraj nunca juega contra Aven ni Serevin.

La tendencia coincide con la ya observada en la fase 7.

**Propuesta:** revisar la ventaja de Aven, que suma la carga +2, las cartas de Muster que la IA sí usa y el Threadkeeper. Conviene además ampliar la matriz del verificador a todos los emparejamientos del reino.

### 3. Media: las unidades piratas rinden menos de lo que cuestan

Combates en campo abierto, con ambos lados en cada posición; los resultados fueron idénticos:

| Combate | Ganador | Supervivientes |
| --- | --- | --- |
| Saqueador contra Reedguard, 1 contra 1 | Reedguard | 4 PV |
| Saqueador contra Strider, 1 contra 1 | Strider | 14 PV |
| Saqueador contra Stringwarden, 1 contra 1 | Stringwarden | 18 PV |
| 8 saqueadores (720) contra 9 Reedguards (720) | Reedguards | 6 de 9 |
| 8 saqueadores (720) contra 9 Striders (720) | Striders | 5 de 9 |
| Corsario de pólvora contra Stringwarden, 1 contra 1 | Corsario | 15 PV |
| 9 corsarios (810) contra 10 Stringwardens (800) | Stringwardens | 4 de 10 |
| 9 corsarios (810) contra 9 Reedguards (720) | Corsarios | 4 de 9 |
| Capitán (300, 3 de población) contra 3 Reedguards (240) | Reedguards | 2 de 3 |
| Capitán (300) contra 3 Striders (240) | Striders | 2 de 3 |

El saqueador pierde contra los tres roles básicos, a pesar de costar metal (70 F + 20 M) y no tener ninguna bonificación. El Capitán tampoco compensa su coste, su población y sus 40 s de entrenamiento.

**Propuesta:**
- Dar al saqueador un papel propio, por ejemplo una bonificación contra Ranged o contra estructuras al abordar.
- Dar al corsario de pólvora un perforado contra Heavy.
- Subir al Capitán o darle un efecto de mando.

### 4. Media: la IA no entrena piratas, salvo el Capitán

En las 48 partidas, el Capitán aparece en todas las IA históricas. En cambio, `boarding_raider`, `gunpowder_corsair` y `treasure_seeker` no aparecen nunca.

Hay dos causas:
- `RecruitSoldier` puntúa por bonificación contra lo observado menos el coste ([OfflineAi.cs:400](../../Assets/Game/Simulation/OfflineAi.cs#L400)), y los piratas no tienen bonificación y cuestan más.
- El trabajador que se entrena es siempre el primero de la lista del Hearth, es decir, el Tender ([OfflineAi.cs:340](../../Assets/Game/Simulation/OfflineAi.cs#L340)).

### 5. Media: IA pasiva y sin niveles de dificultad

La IA solo se lanza al asalto con 26 tropas listas, 10 trabajadores, la cuarta era y 8 tecnologías ([OfflineAi.cs:442](../../Assets/Game/Simulation/OfflineAi.cs#L442)). Los niveles Easy, Medium, Hard y Master del documento de diseño no existen.

El jugador guionizado lo explota. Con Aven en Sapphire Coast, las nueve combinaciones probadas (ataque desde el minuto 3, 4 o 5; oleadas de 12, 16 o 20 unidades) ganaron en una sola oleada, entre 4:11 y 5:42. La IA tenía entonces entre 12 y 19 soldados.

**Propuesta:** niveles de dificultad con ataques de tiempo y una defensa que reaccione antes de que el ejército rival llegue al Hearth.

### 6. Media (diseño): los piratas están abiertos a las cuatro facciones históricas

Las unidades piratas solo exigen el reino histórico (`RequiredRealmId`), no una facción. Cada facción histórica gana cuatro unidades y un héroe, y llega a unas 12 opciones, frente a las 6–8 que fija [la lista de unidades](../design/UNIT_ROSTER.md). Hay que decidir si son una facción propia, mercenarios con límite o una opción común.

### 7. Baja: las mejoras de armas disparan el daño del ariete contra edificios

`weapons_1` y `weapons_2` suman +2 y +4 al daño base de las etiquetas 398, que incluyen Siege, antes del multiplicador ×10 del ariete contra estructuras.

- **Daño por golpe contra un Hearth (armadura 2):** 78 sin mejoras, 98 con la primera y 118 con la segunda.
- **Tiempo para derribarlo con un ariete:** 36 s, 30 s y 24 s.

El [árbol tecnológico](../design/TECHNOLOGY_TREE.md) todavía dice que estas mejoras solo afectan a Infantry, Cavalry y Ranged (etiquetas 14).

### 8. Baja: el robo de vida de Ashen también cura al golpear edificios

`CombatSystem` aplica `MeleeLifeSteal` tras cualquier golpe cuerpo a cuerpo, también contra estructuras ([CombatSystem.cs:110](../../Assets/Game/Simulation/CombatSystem.cs#L110)). Las unidades de Ashen recuperan 2 PV por golpe mientras derriban edificios. Conviene limitarlo a unidades o documentarlo.

### 9. Informativo: un Hearth en construcción mantiene viva a su facción

Es la regla documentada en la fase 7 y la sonda la confirma. Además, el HUD ofrece el Hearth a los trabajadores junto al resto de edificios ([MatchHud.cs:341](../../Assets/Game/Presentation/MatchHud.cs#L341)), así que cualquier jugador puede usarlo. En la sonda, un defensor colocó un segundo Hearth. Cuando el original cayó, a los 20,9 s, la partida siguió, y solo terminó al destruir el nuevo, a los 43,8 s. Un cimiento cuesta 400 F, 300 W y 100 S y exige visión, pero permite alargar partidas perdidas. Se podría exigir que esté terminado o añadir una cuenta atrás.

### 10. Informativo: ritmo de las partidas

Entre IA:
- **Conquest:** de 7,7 a 16 minutos, con una media de 11,2.
- **Dominion:** de 14 a 25 minutos, con una media de 19,2.

El objetivo del documento de diseño es de 12 a 20 minutos. Contra un ataque temprano, Conquest termina en unos 5 minutos (hallazgo 5).

### 11. Documentación desfasada

- **Documento de diseño:**
  - Aún dice que un Hearth destruido no termina la partida.
  - Titula como «futuras» las fases 5 y 7, que ya están hechas.
- **Unidades, edificios y biblia de facciones:**
  - Solo describen Aven y Serevin.
  - No incluyen a los piratas.
  - Las criaturas solo aparecen en la documentación de la expansión.
- **Árbol tecnológico:** lista 11 investigaciones cuando hay 17, y dice etiquetas 14 cuando los datos usan 398.
- **Alcance del MVP:** habla de dos facciones; hay ocho.

## Partida completa grabada

La partida se jugó en el player de desarrollo, build `60fdd9a05aed4cb7bd0e79fac9a0d712`. Fue la escaramuza normal de Sapphire Coast en modo Conquest, con Aven (el jugador guionizado) contra Serevin (la IA del juego), de principio a fin sin recursos extra ni resultados forzados.

El jugador guionizado ganó por Conquest a los **5:47** de juego (6.944 ticks):
- **Órdenes:** 202 aceptadas de 206.
- **Bajas:** 33 muertes en total, 6 de ellas suyas.
- **IA rival:** 13 edificios, 35 unidades encargadas y 17 órdenes de ataque.

| Tiempo | Momento |
| --- | --- |
| 0:56 | Primer Muster Hall terminado |
| 1:06–1:44 | Primeros corsarios de pólvora, Reedguards y saqueadores |
| 2:20 | Kingdom |
| 2:50–3:03 | Tempered Edges y Layered Gear |
| 4:01 | Corsario Carmesí en el campo |
| 5:00 | Sale el ataque con 40 unidades |
| 5:33 | Hearth enemigo a la vista |
| 5:47 | Hearth destruido: victoria |

La partida corrobora los hallazgos 5 y 9. La IA no llegó a atacar la base y cayó en la primera oleada, antes de su cuarta era.

La grabación consta de 3.547 imágenes: un time-lapse de toda la partida (un fotograma cada cuatro ticks) y los combates en tiempo real (dos fotogramas por tick). Las imágenes clave están junto al informe de la partida (`full-match.json`).

## Reglas comprobadas sin incidencias

| Regla | Dónde | Estado |
| --- | --- | --- |
| Los recursos cuentan al entregarlos en un punto de entrega propio y operativo; el trabajador muerto pierde su carga | `EconomySystem`, `World.DestroyUnit` | Correcta |
| Recolectar, atacar y construir exigen visión actual | `World.Submit`, `CombatSystem`, `FogOfWarSystem` | Correcta |
| La población se reserva al encolar y la unidad espera si ya no cabe | `ProductionSystem` | Correcta |
| La construcción se cobra una vez y da capacidad al terminar | `ConstructionSystem` | Correcta |
| La investigación requiere cola vacía y es una por edificio; las eras van en orden; el edificio requerido se comprueba al empezar | `ResearchSystem` | Correcta |
| Mayor multiplicador contra etiquetas, luego armadura, mínimo 1; golpes cuerpo a cuerpo simultáneos; el proyectil fija su daño al salir | `CombatSystem` | Correcta |
| El límite del héroe (1) cuenta también las colas | `UnitRecruitment` | Correcta |
| Las unidades de un reino se rechazan en el otro | `UnitRecruitment`, `World` | Correcta |
| Al terminar la partida el mundo se congela y rechaza órdenes | `World.Submit`, `World.Tick` | Correcta |
| Los tres mapas de partida son espejos exactos (mismos nodos, cantidades y distancias por lado) | Mapas | Correcta |

## Reproducir

```powershell
./tools/Verify-RulesAudit.ps1 --probes --mirror
./tools/Verify-RulesAudit.ps1 --match --map sapphire_coast,amber_crossing,sunscar_basin --faction aven,serevin,miraj,skeld --difficulty Normal --verbose
./tools/Verify-RulesAudit.ps1 --ladder --difficulty Hard --opponent Normal
./tools/Verify-RulesAudit.ps1 --natural --map sunscar_basin --mode Dominion --output TestResults/RulesAudit/natural.json
./tools/Verify-RulesAudit.ps1 --natural --rules <copia-de-greybox.json> --faction aven,serevin --output TestResults/RulesAudit/candidato.json
./tools/Verify-RulesAudit.ps1 --divergence --map sunscar_basin --faction aven --mode Dominion
./tools/Verify-RulesAudit.ps1 --race
./tools/Verify-ExpansionSimulation.ps1 -NaturalOnly -Output TestResults/RulesAudit/natural-matches.json
```

La partida grabada se juega en el player de desarrollo con `-emberfieldFullMatch <carpeta>`. Opcionalmente admite `-emberfieldMatchMap`, `-emberfieldMatchFaction`, `-emberfieldMatchMode`, `-emberfieldMatchWave` y `-emberfieldMatchEarliest`.
