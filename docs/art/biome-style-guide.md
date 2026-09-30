# Guía de estilo — Kingdom, Caribbean, Desert y Fantasy

Fecha: 10 de septiembre de 2026. Dirección de autoría para las cuatro escenas del [plan de renovación](art-overhaul-plan.md). Todas las escenas son entornos 3D reales con personajes y edificios volumétricos. Esta guía define objetivos pendientes; no declara que las nuevas capturas o mediciones ya existan.

## Lenguaje común

RTS estilizado heroico de detalle medio: cabezas y manos ligeramente ampliadas, hombros con peso, herramientas grandes, prendas construidas en capas y formas limpias con planos de luz coherentes. La riqueza se concentra primero en silueta, después en ropa/equipo, y por último en detalle de superficie. Evitar esferas de armadura, piernas cilíndricas sin articulación, rostros como bloques y ruido de pequeños adornos repetidos.

El color distingue bioma, material y propietario. La base del bioma pertenece al mundo; el color de equipo ocupa paneles de ropa, escudos, bandas o arreos. Paleta inicial de equipo del laboratorio: azul `#305E9E`, rojo `#9E2E1F`, verde `#306E40`, oro `#C98A2B`. Probar los cuatro bajo cada luz sin convertir piel, acero, madera o vegetación en color de equipo. Las insignias deben ser originales y su lectura debe sobrevivir al LOD.

Acero con reflejo amplio contenido; tela mate; cuero de brillo bajo e irregular; madera con dirección de veta y extremos legibles; piedra con planos y variación amplia. El atlas compartido de 4×4 sirve a esas superficies. No debe llevar sombras de escena pintadas, siluetas de personajes ni edificios completos. UV con márgenes y escala consistente, sin estirar costuras o vetas sobre toda una figura.

Una luz direccional con sombras, ambiente ajustado al bioma y grading moderado sostienen la lectura. Las luces decorativas y emisión no necesitan convertirse todas en luces dinámicas. La niebla separa distancias; el bloom queda en acentos luminosos. Evitar zonas quemadas, agua gris opaca, piel verdosa, metal plástico y resplandor que desdibuje límites.

Cada escena combina una zona de actividad clara, un grupo de edificios, una ruta que conecte ambos y varias capas de vegetación/roca/agua. Colocar utilería por función: sacos junto a almacén, herramientas cerca del cultivo, cuerdas en el muelle, puestos dentro del mercado. Dejar espacio para reconocer cuerpos y armas, con los bordes ricos y el recorrido central claro. Una escena rica no exige saturar cada metro de props.

## Kingdom / Temperate — primera escena

**Referencia principal:** R10, con personajes R01/R03/R05/R08/R09 del [registro](reference-gap-analysis.md#registro-de-las-trece-referencias). Sensación: valle habitado, fértil y protegido, con piedra clara, madera cálida y actividad cotidiana.

| Capa | Dirección de autoría |
| --- | --- |
| Paleta | Verde bosque `#355747`, hierba `#789454`, trigo `#C7A45D`, piedra `#ACAA91`, teja `#A65F42`, agua `#3F929A`; azul y oro como acentos de vestuario/estandarte. |
| Terreno | Loma y ribera modeladas, camino de tierra/piedra que cruza el río, transición de pradera a bosque; acantilado al fondo con cambio de altura visible. |
| Agua | Río con orilla definida, puente funcional en composición y cascada desde roca elevada; lámina con variación de color, espuma contenida y contacto con la ribera. |
| Edificios | Grupo mínimo de castillo/torre y dos viviendas de madera/piedra; molino identificable por sus aspas; parcela de cultivo y valla. Tejados, puertas, ventanas y aleros con profundidad real. |
| Vegetación y roca | Dos o más siluetas de árbol, arbustos y hierba agrupada; rocas estratificadas en ribera/acantilado. Cultivo en masas doradas con bordes de parcela. |
| Utilería | Carro, troncos, sacos/cajas, herramientas, poste de bandera y elementos de granja. Distribución vinculada a oficio y ruta. |
| Luz | Sol cálido lateral, ambiente de cielo fresco, sombras suaves y fondo ligeramente más frío/desaturado. Mantener acero frío y tela azul separados de las sombras. |

**Personajes previstos: cinco.** Trabajador con sombrero, camisa, correas, bolsas y herramienta; lancero con casco, placas, tabardo y escudo; caballería con caballo completo y arreos; héroe con capa y cabeza distintiva; guerrero montañés con piel, mochila/cuerda y arma pesada junto al camino rocoso. Tender y Reedguard conservan sus roles existentes; los otros son candidatos de presentación.

**Composición.** Close muestra dos o tres figuras junto a un tramo de camino, cultivo o muro, con herramientas completas dentro de cuadro. Medium conecta grupo, molino/viviendas y puente. RTS utiliza el río como diagonal, el castillo como hito y bosque/roca como marco; no deja una gran plataforma vacía ni oculta los personajes detrás de tejados.

**Rechazar en revisión:** castillo como caja con conos, árboles idénticos distribuidos a cuadrícula, molino sin estructura, agua sin conexión con cascada, pies en pendientes flotantes, peanas negras como suelo principal o metal blanco uniforme.

## Caribbean — segunda escena

**Referencias:** R12, R02 y variantes costeras de R01/R03/R05. Sensación: puerto tropical activo, sal, madera, arena clara y vegetación húmeda; la identidad náutica se comunica mediante construcción y equipo.

| Capa | Dirección de autoría |
| --- | --- |
| Paleta | Agua turquesa `#39B6BD`, profundidad `#216E88`, arena `#DDCF98`, palmera `#37745A`, madera `#87583B`, coral/rojo `#A94B3D`. Blanco roto para velas, camisas y espuma. |
| Terreno | Playa curva que asciende a terreno vegetal; camino desde costa a guarnición, pequeñas terrazas y rocas de caliza. La playa tiene grosor y transición, no un disco plano de color. |
| Agua | Bahía visible, franja somera más clara, borde de espuma fino y variación amplia. Muelles apoyados con pilotes que entran en el agua. |
| Edificios | Embarcadero y almacén/casa portuaria, torre de vigilancia de madera y ruina tropical con mampostería erosionada; piers, barandillas y cubiertas de tejido/madera. |
| Vegetación y roca | Palmeras de varias inclinaciones, frondas con volumen, arbustos tropicales y masas verdes en bordes; roca costera y piedras junto a orillas. |
| Utilería | Barriles, cajas, cuerda enrollada, sacos, ancla y farol; barco decorativo opcional con casco/mástil real. No implica combate naval. |
| Luz | Día cálido claro con ambiente azul/cian suave; contacto legible bajo muelles y palmeras, reflejos contenidos, aire algo más azulado en fondo. |

**Personajes previstos: tres o más.** Pirata con abrigo o bandana, cinturón, botas y arma curva; trabajador portuario con carga y equipo de cuerda; guerrero de guarnición con camisa/prendas ligeras y arma/escudo reconocibles. Comparten proporciones y lenguaje de materiales, pero difieren de Aven en ropa y accesorios; no basta cambiar azul por rojo.

**Composición.** Close se sitúa en playa o muelle ancho con carga y barandilla de fondo. Medium une personajes, torre y embarcadero. RTS muestra bahía, puerto y vegetación en capas, dejando canales claros entre palmeras y tejados. El barco, si existe, acompaña la escena sin ocupar todo el encuadre.

**Rechazar en revisión:** agua gris, arena sin orilla, palmeras en forma de estrella plana, muelles suspendidos sin apoyos, cajas repartidas al azar, pirata idéntico al lancero con otro tinte.

## Desert — tercera escena

**Referencias:** R13 y variantes de desierto de R01/R03/R05/R09. Sensación: asentamiento protegido alrededor de agua escasa, arquitectura cálida, telas, comercio y relieve erosionado.

| Capa | Dirección de autoría |
| --- | --- |
| Paleta | Arena `#CDA567`, arenisca `#B88452`, piedra clara `#E0C28D`, sombra malva `#776476`, agua `#3D9A9D`, palmera `#557C4E`; rojo óxido y azul profundo en toldos/equipo. |
| Terreno | Dunas anchas de distinta altura, senderos de paso, cuenca de oasis y pared rocosa erosionada. La arena cambia de valor con forma y luz, no solo con manchas. |
| Agua | Oasis con borde de roca/arena y vegetación más densa junto al agua; contraste turquesa concentrado y continuidad de superficie. |
| Edificios | Grupo de puerta/muralla de arenisca, edificio con cúpula o cubierta característica, dos puestos/toldos de mercado y obelisco. Arcos, contrafuertes y aberturas con volumen. |
| Vegetación y roca | Palmeras junto al oasis, matorral disperso, piedras erosionadas y bloque rocoso mayor como fondo; evitar vegetación húmeda por toda la arena. |
| Utilería | Ánforas, cestos, alfombras/toldos, cajas, sacos, carretilla o equipo de caravana y lámparas discretas. |
| Luz | Sol cálido alto/lateral y ambiente ligeramente frío; arena luminosa sin quemar blancos, sombras suficientes para separar paños crema del suelo. Niebla cálida tenue al fondo. |

**Personajes previstos: tres o más.** Trabajador con paños enrollados, bolsas y herramienta; guerrero con ropa superpuesta, faja y protección de metal/cuero; caballería con montura completa, silla, manta y arreos. Si se elige camello por la referencia, debe tener anatomía y postura propias: una decoración de caravana reutilizada no acredita por sí sola un jinete nuevo.

**Composición.** Close usa sombra parcial de mercado y suelo claro para separar ropas y metal. Medium relaciona grupo, puestos y entrada. RTS organiza el oasis como foco secundario, la ruta como conexión y las dunas/roca como marco; el muro no tapa unidades pequeñas.

**Rechazar en revisión:** suelo totalmente plano, arquitectura igual a Kingdom recoloreada, palmeras aisladas sin relación con agua, sobreexposición amarilla, camello con jinete desalineado, saturar el oasis de transparencias.

## Fantasy — cuarta escena

**Referencias:** R11, R04/R06/R07 y rasgos heroicos de R09. Sensación: bosque antiguo habitado, arquitectura orgánica elegante y magia integrada en materiales y luz; criaturas con cuerpo y peso.

| Capa | Dirección de autoría |
| --- | --- |
| Paleta | Verde profundo `#254E49`, follaje `#55816A`, corteza `#695548`, marfil `#CDC7A5`, agua/cian `#55B8B5`, cristal violeta `#8C79BF`; oro suave y luces cálidas puntuales. |
| Terreno | Raíces elevadas, sendero de piedra curva, bancales/roca y claro central. Las elevaciones y puentes son presentación, sin añadir reglas de navegación PvP. |
| Agua | Arroyo o estanque místico que conecta raíces/ruina; emisión moderada o reflejo estilizado, borde claro y cascada pequeña si la topografía la justifica. |
| Edificios | Árbol sagrado con base arquitectónica, pabellón o torre elegante, arcos/ruinas y puente de raíces/piedra; curvas y remates volumétricos. Formar un asentamiento, no solo un árbol con cristales alrededor. |
| Vegetación y roca | Árboles de copa en capas, raíces, helechos/arbustos agrupados, rocas con musgo y cristales de varias alturas en concentraciones limitadas. |
| Utilería | Faroles, piedra tallada, bancadas/altar, escombros de ruina, marcadores de ruta y pequeños acentos luminosos. Símbolos originales. |
| Luz | Ambiente fresco verde/azulado controlado, luz principal cálida/neutra que conserve piel y metal, bruma suave y emisión localizada. Dragón y elfo deben leerse sin halo excesivo. |

**Personajes previstos: tres o más.** Enano de cuerpo bajo/ancho, barba y herramienta o arma pesada; elfo alto/esbelto con capa, cabello/orejas y arco/bastón; dragón con cuatro patas, dos alas, cuello, mandíbula y cola. Los tres requieren estructuras y siluetas distintas. Pueden convivir en la escena de dirección fantástica sin que esto cree una alianza, facción, raza del roster o unidad comprable nueva.

El dragón es un arquetipo militar, coherente con las criaturas del ejército del juego. Diseñar placas principales, cuernos y membranas antes que escamas pequeñas. Alas y cola deben caber completas en la revisión, proyectar sombras y contactar coherentemente con el terreno. Una variante de color/elemento es apariencia; no concede poder PvP.

**Composición.** Close presenta enano y elfo con raíz/ruina como contexto y permite inspeccionar al dragón sin cortar alas mediante encuadre declarado. Medium agrupa las tres especies cerca del camino. RTS reserva un claro para su lectura, sitúa el árbol sagrado detrás y concentra luz/cristales fuera del contorno de personajes.

**Rechazar en revisión:** enano como humano reducido, elfo como mismo cuerpo pintado de verde, dragón hecho de tubos sin patas/alas construidas, exceso de cian/violeta, arquitectura histórica recoloreada, cristales que sustituyen al resto del entorno.

## Revisión transversal y salidas

Las tres distancias cumplen funciones diferentes y todas deben resultar atractivas. Close verifica construcción y superficie; Medium verifica personajes dentro del mundo; RTS verifica identidad, composición y calidad a distancia táctica. Registrar la cámara efectiva y la resolución. La referencia normal es ortográfica a 55° de inclinación y yaw 35°; el zoom se comunica, sin agrandar secretamente unidades o congelar un LOD superior para simular calidad de juego.

Producir doce capturas nativas: `Artifacts/ArtReview/overhaul/{kingdom,caribbean,desert,fantasy}-{close,medium,rts}.png`. Revisar 16:9 y 4:3, los cuatro colores de equipo y siluetas completas. No superponer ilustraciones de personajes, fotomontar edificios ni retocar después las capturas. Si una toma usa una pose estática o un LOD forzado, indicarlo junto a la evidencia.

El inventario final debe distinguir geometría única, instancias y variantes de material, y registrar coste real por familia y escena. La densidad de detalle se adapta con LOD, atlases, reutilización y culling; no se valida por el número de objetos del Hierarchy. Las bandas de coste están en el [plan](art-overhaul-plan.md#presupuestos-y-medición). Rendimiento, memoria y estabilidad térmica en móvil físico permanecen pendientes de medición; las imágenes de revisión no autorizan afirmaciones de FPS.
