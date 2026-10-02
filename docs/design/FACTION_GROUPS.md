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
| Navales | Piratas | Seleccionable. Capitán caribeño, saqueador de abordaje, corsario de pólvora y buscadora de tesoros, que carga dos recursos más por viaje. Astillero y balandra: el casco ligero, barato y rápido que aborda fragatas. |
| Navales | Marina inglesa | Seleccionable. Ejército del reino con los modelos de los Ingleses; sus tropas a distancia ganan un punto de armadura. Astillero y fragata inglesa: rápida y de largo alcance, desarbola galeones. |
| Navales | Marina española | Seleccionable. Ejército del reino con los modelos de los Hispanos; su infantería gana un punto de armadura. Astillero y galeón español: lento, de casco pesado, con la andanada más fuerte y diez plazas; hunde balandras. |
| Navales | Flota esquelética | Ficha pendiente, bloqueada. Modelo de barco fantasma disponible; faltan las tripulaciones de esqueletos (arte detallado en el [tramo naval](NAVAL_SLICE.md)). |

## Límites de esta entrega

La categoría Navales ofrece **navegación, combate entre barcos y desembarcos en Sapphire Coast** con tres flotas. Cada una construye un astillero en la costa y bota su propio casco, que combate, bombardea la costa y transporta tropas: la balandra pirata lleva seis, la fragata inglesa cinco y el galeón español diez. La balandra vence a la fragata, la fragata al galeón y el galeón a la balandra. Sin conexión, cada flota se enfrenta a la siguiente: Piratas contra Marina inglesa, Marina inglesa contra Marina española y Marina española contra Piratas. Incluye órdenes de embarcar y desembarcar, minimapa e IA naval con escolta y desembarcos. Las marinas son facciones independientes de los ejércitos terrestres del mismo país: no pueden jugarse en Históricas, y la Flota esquelética no se habilita enviando su identificador directamente al servidor. Alcance y reglas: [tramo naval](NAVAL_SLICE.md).

Franceses, Hispanos e Ingleses comparten por ahora modelos genéricos del reino; el Sultanato y el Sahel tienen su propia cultura: unidades, nueve edificios, muralla, puerta y escalera de asedio. Esta organización no certifica uniformes históricamente exactos ni implica tres colecciones artísticas terminadas. Las marinas inglesa y española usan los soldados y la ciudad del reino de los Ingleses y los Hispanos, con el color del jugador. Los modelos de orcos y la Flota esquelética requieren trabajo artístico adicional. Las animaciones y los modelos piratas importados se conservan.

Históricas y Fantasía admiten Amber Crossing, Sapphire Coast y Sunscar Basin. Al elegir el Sultanato o el Sahel, el selector propone Sunscar Basin, su desierto, mientras el jugador no haya elegido otro campo de batalla; sin conexión se enfrentan entre sí. Fantasía tiene además su propio mapa, Tierras de Leyenda (`legend_lands`), que es el que abre por defecto: cada ejército empieza en la tierra de su cultura (bosque élfico para los Elfos, páramo volcánico con ríos de lava para los Orcos, llanuras de montaña para los Hombres de las montañas y los Enanos) y el centro es una franja neutral de montaña. Las tierras solo cambian el aspecto; el terreno, el agua y los recursos están en espejo como en los demás mapas. Navales admite exclusivamente Sapphire Coast. Los cosméticos no permiten cambiar de grupo ni desbloquear una facción pendiente.

## Compatibilidad

Los identificadores internos `aven`, `serevin`, `ashen`, `drakeforged`, `skeld` y `verdant` se conservan para sus reglas existentes; los nombres visibles cambian a los de la tabla. `english`, `pirates`, `sultanate`, `sahel`, `english_navy` y `spanish_navy` son entradas nuevas. Las definiciones antiguas `miraj` y `solar` se mantienen para interpretar datos de versiones anteriores, pero no aparecen en los selectores ni se aceptan al crear partidas nuevas. No se reescriben resultados históricos, cuentas ni rangos antiguos.

La pertenencia de montañeses cambia a Fantasía y la de piratas a Navales, tanto en simulación como en el catálogo visual. El servidor rechaza jugadores de distintos grupos en la misma sala. Los trabajadores iniciales piratas son buscadoras de tesoros y las cuatro unidades piratas exigen la facción Piratas.

Definiciones: [ContentRealms.cs](../../Assets/Game/Simulation/ContentRealms.cs), [greybox.json](../../Assets/Game/Resources/Definitions/greybox.json) y [contrato del servidor](../../Server/content-realms.mjs). Evidencia de esta revisión: [pruebas de ámbitos](../technical/faction-realms-review.md).
