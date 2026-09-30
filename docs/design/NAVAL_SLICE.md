# Primer tramo naval — 28 de septiembre de 2026

El trabajo naval pendiente sobre `4f89b74` incorpora un astillero costero, barcos de combate con transporte, navegación por agua y desembarcos. La facción jugable naval sigue siendo Piratas. Los modelos de fragata inglesa, galeón español y barco fantasma están importados, pero sus facciones permanecen bloqueadas hasta tener ejércitos y reglas propios.

## Recorrido de juego

1. Revelar una costa y construir el astillero (`dock`, 150 de madera) con una buscadora de tesoros. Su huella ocupa tierra libre junto al agua navegable.
2. Disponer de población libre y entrenar una balandra (`pirate_sloop`, 20 de comida, 130 de madera y 2 de población).
3. Seleccionar unidades terrestres, pulsar **Embarcar** y elegir una balandra propia junto a una costa accesible. Cada balandra transporta hasta seis unidades; las reservas de embarque cuentan para ese límite.
4. Seleccionar el barco para navegar o atacar. **Desembarcar** permite elegir una orilla en el mundo o en el minimapa. El barco se aproxima por agua y deposita la carga en tierra despejada y conectada al destino.

El embarque tarda un segundo una vez alcanzado el casco. Las unidades transportadas dejan de aportar visión y de combatir; siguen consumiendo población. Si no hay espacio al desembarcar, los pasajeros que no caben permanecen dentro. Al hundirse un barco, mueren sus pasajeros y se cancelan las reservas pendientes. Una orden nueva cancela el embarque o desembarco anterior.

## Navegación y combate

El dominio terrestre conserva su navegación existente. El dominio naval utiliza la intersección `WaterCells ∩ BlockedCells`: agua marcada como profunda e infranqueable para tropas. Los puentes y vados, que ya eran agua transitable por tierra, siguen siendo terrestres y no admiten barcos. Las rocas sin agua tampoco se convierten en mar. Este criterio adapta el diseño a la codificación de los mapas existentes.

El recurso `sapphire_coast_naval.json` conecta el mar por los bordes norte y sur y abre pasos alrededor de las islas. La máscara de agua profunda mantiene simetría entre jugadores. Solo el reino Navales carga esta variante; Históricas y Fantasía conservan el recurso `sapphire_coast.json` de `4f89b74`. El identificador público sigue siendo `sapphire_coast`, y cliente sin conexión y autoridad resuelven el recurso mediante la misma función de `ContentRealms`. Los seis escenarios terrestres de regresión se comparan contra el commit limpio `4f89b74`.

Los barcos atacan barcos y objetivos costeros que estén al alcance. Las tropas terrestres no persiguen objetivos por agua. Los barcos no capturan objetivos de tierra. Conquista mantiene la condición de destruir los edificios centrales; este tramo no redefine la victoria naval.

## Autoridad y reconexión

Las órdenes de embarque/desembarque pasan por el protocolo y la simulación autoritativa. Se comprueban propiedad, dominio, capacidad, rutas y espacio de desembarco. El estado de carga propio conserva identificadores, salud y posición del portador; se valida al reconstruir la réplica. La carga enemiga no revela su manifiesto privado. Los límites de héroes y población incluyen pasajeros.

El minimapa distingue el agua profunda. Los barcos usan modelos importados y se ajustan a su línea de flotación. El HUD presenta capacidad y órdenes de transporte para trabajadores y soldados; la disponibilidad de embarque cuenta las reservas existentes. La IA del reino naval construye y paga muelles, produce barcos, combate y organiza desembarcos mediante órdenes normales; sigue siendo una IA mínima, sin planificación estratégica naval avanzada.

## Verificación reproducible

- `tools/Verify-Unity.ps1 -Stage Compile`, después `EditMode` y `PlayMode`.
- `tools/Verify-SimulationTicks.ps1 --scenario all --compare <baseline-4f89b74.json>`.
- Tras confirmar Simulation/Networking/Maps/greybox: `node Server/update-content-version.mjs`, `tools/Build-Authority.ps1` y `npm test` dentro de `Server`.
- Ejecutable: `tools/Build-Unity.ps1 -Target Windows -ProjectPath D:/EmberfieldWorkingCache/AoE/MobileTierBuildProject`.
- Transporte y reconexión con dos ejecutables: `tools/Smoke-Online.ps1 -Realm naval -NavalSlice -TimeoutSeconds 360`.
- Recorrido de producto: `tools/Smoke-AlphaProduct.ps1`.

La prueba nativa naval recoge recursos, compra una casa y un astillero, entrena una balandra, embarca y desembarca un trabajador, reinicia el proceso invitado con carga a bordo y vuelve a desembarcar. Usa una base de datos y ajustes de prueba aislados. No modifica `D:/EmberfieldJugar` ni despliega el servidor público. Los resultados concretos se registran en el informe técnico de esta entrega.
