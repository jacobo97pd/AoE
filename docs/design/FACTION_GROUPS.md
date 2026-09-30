# Organización de facciones — 14 de septiembre de 2026

El selector distingue **Históricas**, **Fantasía** y **Navales**. Cada partida pertenece a un único grupo: sus jugadores, unidades exclusivas, emparejamiento, clasificación e historial usan ese mismo ámbito. Conquista y Dominio siguen siendo condiciones de victoria dentro de cada grupo.

| Grupo | Facción | Estado y contenido actual |
| --- | --- | --- |
| Históricas | Franceses | Seleccionable. Economía y logística del reino; conserva las reglas del antiguo Aven. |
| Históricas | Hispanos | Seleccionable. Caballería y puestos móviles; conserva las reglas del antiguo Serevin. |
| Históricas | Ingleses | Seleccionable. Ejército común, con un punto adicional de armadura para tropas a distancia. |
| Históricas | Sultanato | Seleccionable desde el 28 de septiembre de 2026. Piedra y caballería veloz; Arquero de camello. Modelos propios del desierto ([diseño](DESERT_FACTIONS.md)). |
| Históricas | Confederación del Sahel | Seleccionable desde el 28 de septiembre de 2026. Comida y escudos de mimbre; Jinete acolchado. Modelos propios de barro y algodón ([diseño](DESERT_FACTIONS.md)). |
| Fantasía | Orcos | Seleccionable. Combate cuerpo a cuerpo y trol de guerra; modelos propios de orcos pendientes. |
| Fantasía | Enanos | Seleccionable. Minería y metal; personajes enanos de la colección actual. |
| Fantasía | Hombres de las montañas | Seleccionable. Infantería resistente y guardia de escarcha; personajes montañeses. |
| Fantasía | Elfos | Seleccionable. Madera, arqueros y guardianes del bosque; personajes élficos. |
| Navales | Piratas | Seleccionable. Capitán caribeño, saqueador de abordaje, corsario de pólvora, buscadora de tesoros, astillero y balandra de combate/transporte. |
| Navales | Marina inglesa | Ficha pendiente, bloqueada. Modelo de fragata disponible; faltan ejército y reglas propios. |
| Navales | Marina española | Ficha pendiente, bloqueada. Modelo de galeón disponible; faltan ejército y reglas propios. |
| Navales | Flota esquelética | Ficha pendiente, bloqueada. Modelo de barco fantasma disponible; faltan ejército y reglas propios. |

## Límites de esta entrega

La categoría Navales ofrece **navegación, combate entre barcos y desembarcos en Sapphire Coast**. El primer tramo naval permite construir un astillero en la costa, entrenar una balandra y transportar hasta seis unidades terrestres. Incluye órdenes de embarcar y desembarcar, minimapa e IA naval básica. Al haber una facción naval disponible, las partidas enfrentan Piratas contra Piratas. Las marinas son facciones independientes de los ejércitos terrestres del mismo país; no se habilitan enviando sus identificadores directamente al servidor. Alcance y reglas: [tramo naval](NAVAL_SLICE.md).

Franceses, Hispanos e Ingleses comparten por ahora modelos genéricos del reino; el Sultanato y el Sahel tienen su propia cultura: unidades, nueve edificios, muralla, puerta y escalera de asedio. Esta organización no certifica uniformes históricamente exactos ni implica tres colecciones artísticas terminadas. Los modelos de orcos y los ejércitos navales pendientes requieren trabajo artístico adicional. Las animaciones y los modelos piratas importados se conservan.

Históricas y Fantasía admiten Amber Crossing, Sapphire Coast y Sunscar Basin. Al elegir el Sultanato o el Sahel, el selector propone Sunscar Basin, su desierto, mientras el jugador no haya elegido otro campo de batalla; sin conexión se enfrentan entre sí. Fantasía tiene además su propio mapa, Tierras de Leyenda (`legend_lands`), que es el que abre por defecto: cada ejército empieza en la tierra de su cultura (bosque élfico para los Elfos, páramo volcánico con ríos de lava para los Orcos, llanuras de montaña para los Hombres de las montañas y los Enanos) y el centro es una franja neutral de montaña. Las tierras solo cambian el aspecto; el terreno, el agua y los recursos están en espejo como en los demás mapas. Navales admite exclusivamente Sapphire Coast. Los cosméticos no permiten cambiar de grupo ni desbloquear una facción pendiente.

## Compatibilidad

Los identificadores internos `aven`, `serevin`, `ashen`, `drakeforged`, `skeld` y `verdant` se conservan para sus reglas existentes; los nombres visibles cambian a los de la tabla. `english`, `pirates`, `sultanate` y `sahel` son entradas nuevas. Las definiciones antiguas `miraj` y `solar` se mantienen para interpretar datos de versiones anteriores, pero no aparecen en los selectores ni se aceptan al crear partidas nuevas. No se reescriben resultados históricos, cuentas ni rangos antiguos.

La pertenencia de montañeses cambia a Fantasía y la de piratas a Navales, tanto en simulación como en el catálogo visual. El servidor rechaza jugadores de distintos grupos en la misma sala. Los trabajadores iniciales piratas son buscadoras de tesoros y las cuatro unidades piratas exigen la facción Piratas.

Definiciones: [ContentRealms.cs](../../Assets/Game/Simulation/ContentRealms.cs), [greybox.json](../../Assets/Game/Resources/Definitions/greybox.json) y [contrato del servidor](../../Server/content-realms.mjs). Evidencia de esta revisión: [pruebas de ámbitos](../technical/faction-realms-review.md).
