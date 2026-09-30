# Kingdom PC — auditoría de fidelidad visual

Fecha: 10 de septiembre de 2026. Línea base visual rechazada: commit `d8e1708127b054093376b4d41f2b71068a73e453`, retratos nativos de la primera renovación artística. Esta auditoría define la reconstrucción de **Worker, Warrior y Hero dentro de una única escena Kingdom para PC**. No presenta una nueva versión como terminada.

## Evidencia observada

Se comparan las figuras Kingdom, situadas a la izquierda de cada lámina. Sus otras culturas, emblemas de maquetación y textos no amplían el alcance de esta iteración.

| Figura | Referencia aportada | Resultado nativo actual |
| --- | --- | --- |
| Worker | [R01 — recolectores](<C:/Users/jacob/Downloads/ChatGPT Image 10 sept 2026, 18_03_48 (1).png>) | [Trabajador Kingdom](../../Artifacts/ArtReview/overhaul/kingdom-worker.png) |
| Warrior | [R03 — guerreros](<C:/Users/jacob/Downloads/ChatGPT Image 10 sept 2026, 18_03_48 (3).png>) | [Guerrero Kingdom](../../Artifacts/ArtReview/overhaul/kingdom-warrior.png) |
| Hero | [R09 — héroes](<C:/Users/jacob/Downloads/ChatGPT Image 10 sept 2026, 18_03_48 (9).png>) | [Héroe Kingdom](../../Artifacts/ArtReview/overhaul/kingdom-hero.png) |

La [vista Kingdom RTS actual](../../Artifacts/ArtReview/overhaul/kingdom-rts.png) permite comprobar la relación con edificios, terreno y vegetación. Los retratos son renders Unity de mallas reales. Las referencias son ilustraciones de diseño, no capturas del proyecto. Las comparaciones finales de implementación utilizarán nuevamente imágenes nativas de la escena nueva; una ilustración generada o una composición retocada no acredita su aspecto dentro del ejecutable.

## Por qué el resultado actual se aleja de las referencias

La diferencia principal está en la construcción de las formas. Las láminas representan personas adultas estilizadas, con cuello, clavículas, cintura, manos que sujetan objetos, prendas que caen sobre el cuerpo y piezas de armadura ajustadas a él. Las tres figuras actuales comparten una cabeza de grandes planos, nariz muy saliente, bigote y barba, tórax casi cilíndrico, hombros rígidos y botas redondeadas. La ropa divide ese cuerpo en paneles de color, pero rara vez altera su volumen de forma creíble.

El héroe actual evidencia el problema: corona, collar claro y capa roja distinguen el rol, pero debajo conserva la forma y el rostro del guerrero. La referencia cambia la postura, el peso de las prendas, las proporciones del equipo, los detalles del peto y la expresión. Repetir hebillas, ampliar texturas o subdividir los mismos volúmenes no corrige esa diferencia.

## Proporciones y construcción que deben cambiar

| Zona | Diagnóstico visible actual | Objetivo de construcción | Cómo comprobarlo en Unity |
| --- | --- | --- | --- |
| Cabeza y cuello | Rostro repetido; nariz triangular prominente, ojos dibujados sobre planos y transición brusca hacia el torso. | Base anatómica adulta con frente, órbitas, pómulo, mandíbula y boca construidos; cuello que conecte con hombros y clavículas. Individualizar edad, mandíbula, pelo y barba de los tres roles. | Retratos frontal, lateral y tres cuartos bajo una misma luz: el perfil debe tener frente/nariz/labios/mentón diferenciados y la expresión debe persistir al girar. |
| Tórax y pelvis | Pecho ancho de sección uniforme; cinturón separa dos bloques sin cintura ni apoyo del abdomen. | Caja torácica que se estrecha hacia la cintura, pelvis reconocible y separación clara entre pierna y faldón. La masa corporal debe funcionar antes de vestirla. | Render de diagnóstico con material uniforme; cuello, hombro, cintura, cadera y rodilla deben leerse por forma, sin depender de colores. |
| Brazos y manos | Antebrazos de grosor uniforme, manos como manoplas y dedos alineados; agarres poco convincentes. | Deltoides unido al brazo, codo y muñeca proporcionados; palma y pulgar oponente, dedos curvados alrededor de mango y empuñadura. | Inspeccionar ambas manos de perfil y tres cuartos: contacto real, sin mango atravesando la palma, pulgar ausente ni puño abierto junto al arma. |
| Piernas y pies | Rodilleras redondas, espinillas rectas y botas abultadas; poco cambio de apoyo entre figuras. | Muslo y pantorrilla con volumen propio, tobillo contenido y pie con empeine, talón y puntera. Apoyo coherente con la dirección de la pelvis. | Vista completa en reposo y movimiento: pies asentados, flexión en rodillas/tobillos y ninguna apariencia de extremidad encajada en una esfera. |
| Vestuario | Paneles frontales planos y rígidos; una textura repetida sugiere tejido pero no pliegues. | Prendas con espesor, costuras, capas y pliegues causados por cinturón, hombros, flexión y gravedad. Separar camisa, chaleco/tabardo, pantalón, capa y correas. | Los pliegues deben modificar luz y contorno. Al retirar temporalmente color y detalles finos, debe seguir reconociéndose qué prenda es. |
| Armadura | Hombreras como escalones sueltos, placas de formas genéricas y brillo uniforme. | Placas adaptadas al torso, con bordes, solapes, remaches y articulación; zonas flexibles de malla o tela en axila, codo e ingle. | Tres cuartos y perfil: placas con espesor y unión creíble, sin atravesarse; highlights continuos sobre metal curvo. |
| Postura | Tres figuras casi verticales con brazos separados de manera parecida. | Worker apoyado y ocupado con su herramienta; Warrior preparado pero relajado; Hero erguido con peso, autoridad y capa asentada. | Los tres deben distinguirse como siluetas sin color, corona o icono de selección. La pose debe resultar plausible desde ambos lados. |

No se deduce una proporción exacta en «cabezas» de láminas con perspectiva, sombrero, casco y corona. La corrección buscada es concreta: recuperar referencias anatómicas adultas, una exageración moderada y relaciones creíbles entre cabeza, cuello, cintura, manos y pies. Usar una base humana proporciona esas relaciones; vestir una base sin adaptar silueta y gesto tampoco basta.

## Objetivo por personaje

**Worker — R01.** Debe parecer una persona que trabaja en ese terreno. Construir camisa marfil con cuello abierto y mangas remangadas; chaleco/sobretúnica azul con espesor y caída; correas de cuero que envuelvan hombros y tórax; cinturón, bolsas y botas gastadas. El ala del sombrero tendrá curvatura, espesor fino y fibra de paja mate, evitando el disco liso naranja del modelo actual. La horca necesita madera diferenciada, casquillo metálico y dientes estrechos; su longitud y agarre deben relacionarse con la postura. El haz de trigo y la carga deben quedar sujetos al equipo y ser visibles desde tres cuartos. La cara, barba y pelo tendrán rasgos propios, sin reutilizar la expresión del héroe.

**Warrior — R03.** La referencia Kingdom usa espada recta y escudo, casco de acero, malla, tabardo azul/marfil, capa y cinturón con equipo. Construir un casco de chapa con reborde y protección lateral, conservando un hueco facial real; evitar una cúpula gruesa sobre una cabeza sin cuello. Usar el tabardo para expresar caída y movimiento sobre la protección, no como dos rectángulos pegados al torso. El escudo debe tener curvatura, espesor, borde y agarraderas; la mano y el antebrazo deben explicar cómo se sostiene. La espada será alargada y de sección legible, con filo, guarda y empuñadura proporcionados. El modelo anterior lleva lanza: esa diferencia se documenta; la figura nueva es una muestra visual y no transforma por sí sola el rol ni las estadísticas de Reedguard.

**Hero — R09.** La diferencia con Warrior debe empezar en la figura entera: postura más asentada, cabeza y expresión distintas, torso protegido por un peto trabajado, manto amplio y largo con exterior azul, interior rojo profundo y ribete de armiño. El pelo del ribete necesita dirección y agrupaciones desiguales; el aro beige uniforme actual no representa un manto. La corona requiere banda con espesor y elementos pequeños de metal/engaste, en lugar de grandes dientes triangulares. La espada ceremonial conserva una hoja esbelta y una empuñadura detallada; la pose debe transmitir su peso y apoyo. Jerarquizar broches, bordado y correas para que no compitan todos con rostro y pecho.

La propuesta de espada del Warrior procede de la lámina solicitada. Se preserva la identidad del proyecto y se documentan las decisiones sobre motivos heráldicos; el texto «Crowns & Horizons» pertenece a la presentación de referencia y no renombra Emberfield.

## Paleta y respuesta de materiales

| Superficie | Lectura de las referencias | Corrección verificable |
| --- | --- | --- |
| Tela azul y marfil | Azul profundo, marfil cálido, pliegues anchos y desgaste localizado. | Evitar azul eléctrico y blanco plano; orientar tejido y costuras por prenda. La trama debe desaparecer a distancia antes que la forma de sus pliegues. |
| Acero | Volumen frío con reflejos controlados, cantos más claros y zonas oscuras entre piezas. | Variar rugosidad con escala coherente; bordes biselados y normales limpias. El metal debe distinguirse de tela gris sin depender de brillo blanco uniforme. |
| Cuero y madera | Marrones cálidos, grosor, pliegue o veta según el objeto, desgaste en contacto. | Correas con vueltas y perforaciones, hebillas apoyadas, mangos con veta longitudinal. Retirar ruido que atraviese costuras o cambie arbitrariamente de escala. |
| Piel, pelo y barba | Rostros cálidos con color matizado y mechones que siguen mandíbula y cráneo. | Piel menos naranja y menos uniforme; ojos y labios integrados en volumen. Pelo construido en grupos que describan dirección y masa. |
| Oro y armiño | Acentos de prestigio, concentrados en corona, broches y bordados. | Oro diferenciado del cuero amarillo; piel clara de pelo corto con borde irregular. Conservar predominio de azul, acero y silueta del manto. |

El detalle de superficie debe acompañar las formas grandes y medianas. La nueva autoría requiere UV y mapas de material adecuados a cada pieza; el atlas anterior no se impone como sustituto de trabajo de ropa, rostro o metal. Los mapas de normales sirven para detalle, no para inventar una silueta ausente.

## Edificio, paisaje e iluminación

La escena actual combina un castillo muy grande, casas repetidas, camino de patrón uniforme, cauce recto y vegetación dispersa. Los personajes quedan diminutos y alineados. La nueva escena acotada necesita un lugar concreto de la aldea, con un edificio principal y acceso, terreno próximo, vegetación agrupada y actividad sugerida por objetos de trabajo.

El edificio debe revelar construcción: cimientos apoyados, entrada proporcionada a una persona, vigas con uniones, huecos con profundidad, aleros y cubierta con espesor. El terreno debe conectar puerta, camino y zona de trabajo mediante desnivel suave, tierra pisada, piedras y hierba de tamaños variados. Si se conserva agua, su ribera necesita volumen y transición de humedad; no colocar una base del edificio suspendida sobre una banda de río. Mantener zonas tranquilas alrededor de manos, herramienta, rostro y escudo para que el entorno no tape la mejora.

Una luz principal suave y direccional, relleno ambiental y sombras de contacto deben revelar rostro, ropa y material. Revisar que acero conserve zonas de reflejo, camisa mantenga detalle, piel no se vuelva naranja y el manto no pierda sus pliegues en sombra. La sensación pictórica de las referencias procede también de su organización de valores: añadir bloom o niebla sobre volúmenes sin resolver no produce esa organización.

## Criterios de comparación final

1. **Personas adultas diferenciadas.** Las tres cabezas, cuerpos, manos y apoyos se sostienen en vistas frontal, lateral y tres cuartos; no comparten el aspecto de un mismo muñeco vestido de tres maneras.
2. **Equipo y ropa construidos.** Se pueden señalar en el render las prendas superpuestas, sus puntos de tensión, el agarre de cada objeto y la articulación de las placas.
3. **Fidelidad reconocible.** Worker se reconoce por oficio y vestuario; Warrior por protección/escudo/espada; Hero por figura, manto y tratamiento noble del equipo. Comparar con el personaje Kingdom concreto de cada lámina, no con una descripción genérica medieval.
4. **Materiales bajo luz real.** Tela, piel, metal, madera y cuero se distinguen dentro de la misma escena, sin retoque del PNG ni materiales que solo funcionen desde una cámara.
5. **Lugar coherente.** Un edificio bien resuelto y su paisaje próximo sostienen a los personajes; no se acepta compensar figuras débiles añadiendo biomas o monumentos.
6. **Escalas útiles.** Retratos completos, vista de grupo con edificio y cámara RTS normal a 1920×1080. Las figuras deben seguir distinguiéndose a resolución de presentación, sin aumentarlas únicamente para la captura ni hacer depender la evaluación de un zoom externo.
7. **Evidencia y límites explícitos.** Publicar los renders nativos nuevos junto a los anteriores y anotar diferencias todavía visibles frente a las referencias. Un archivo importado, un rig funcional o un conteo alto de polígonos no demuestra acabado premium ni identidad con la lámina.

Estos criterios orientan iteración interna continua sobre la escena. No introducen una parada de aprobación después del primer personaje. El alcance y la secuencia vigentes están en [art-overhaul-plan.md](art-overhaul-plan.md); la planificación de cuatro biomas se conserva únicamente como [archivo v1](art-overhaul-plan-v1.md).
