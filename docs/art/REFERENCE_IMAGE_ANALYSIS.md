# Análisis de las trece referencias visuales

Fecha: 2026-09-10. Estado: referencias inspeccionadas individualmente; dirección candidata para el primer **ArtStyleLab**. Este análisis describe las imágenes entregadas por el usuario, no modelos que ya existan en el juego.

La secuencia inmediata del nuevo prompt comprende auditoría, especificación, un trabajador y un guerrero 3D, comparativas y revisión humana en el **checkpoint 2**. Las demás familias se estudian para evitar contradicciones futuras; no se producen en esta entrega. Véase [ART_STYLE_TARGET.md](ART_STYLE_TARGET.md).

## Registro de fuentes

Directorio original: `C:/Users/jacob/Downloads/`. Los nombres siguientes identifican exactamente los archivos inspeccionados. No se incorporan sus píxeles a los modelos ni se convierten las láminas en sprites de juego.

| Ref. | Archivo | Contenido |
| --- | --- | --- |
| 01 | `ChatGPT Image 10 sept 2026, 18_03_48 (1).png` | Recolectores |
| 02 | `ChatGPT Image 10 sept 2026, 18_03_48 (2).png` | Piratas |
| 03 | `ChatGPT Image 10 sept 2026, 18_03_48 (3).png` | Guerreros |
| 04 | `ChatGPT Image 10 sept 2026, 18_03_48 (4).png` | Enanos |
| 05 | `ChatGPT Image 10 sept 2026, 18_03_48 (5).png` | Jinetes |
| 06 | `ChatGPT Image 10 sept 2026, 18_03_48 (6).png` | Dragones |
| 07 | `ChatGPT Image 10 sept 2026, 18_03_48 (7).png` | Elfos |
| 08 | `ChatGPT Image 10 sept 2026, 18_03_48 (8).png` | Hombres de las montañas |
| 09 | `ChatGPT Image 10 sept 2026, 18_03_48 (9).png` | Héroes |
| 10 | `ChatGPT Image 10 sept 2026, 18_10_09 (3).png` | Valle boscoso, población y fortaleza |
| 11 | `ChatGPT Image 10 sept 2026, 18_10_09 (4).png` | Bosque fantástico y arquitectura arbórea |
| 12 | `ChatGPT Image 10 sept 2026, 18_10_08 (1).png` | Costa caribeña, puerto y fortaleza |
| 13 | `ChatGPT Image 10 sept 2026, 18_10_09 (2).png` | Desierto, oasis, cantera y ciudad |

Las láminas 01–09 combinan retratos completos, detalles de equipo e ilustraciones de vista elevada. Su render aparente es ilustración digital volumétrica con iluminación suave, contacto al suelo y materiales detallados; no demuestra topología, rig, cantidad de polígonos ni coste de render. Los pequeños recuadros «vista en el juego» también son conceptos. La marca **Crowns & Horizons**, sus títulos y sus emblemas pertenecen a esas láminas: no constituyen una decisión de renombrar Emberfield.

## 01 — Recolectores

- **Proporciones y silueta:** humanos robustos, manos grandes y botas anchas. Sombrero horizontal, turbante, caja elevada y mochila alta distinguen las cuatro figuras. El montañés parece más compacto por la ropa y el volumen de carga.
- **Paleta y materiales:** azul real/marfil/cuero en el campesino; crema/rojo/oro en el caravanero; azul/rojo/turquesa en el portuario; gris, piel clara y ámbar de linterna en el prospector. Tela mate, cuero grueso, madera de cajas y acero oscuro ofrecen respuestas distintas.
- **Herramienta, ropa y accesorios:** horca de altura similar al cuerpo, picos de cabeza ancha, mangas arremangadas, paños largos, correas cruzadas, bolsas, cereal, cuerdas y recipientes. La herramienta y el contenedor expresan trabajo antes que la ornamentación.
- **Lenguaje y render:** variantes culturales de una misma función económica, con pliegues grandes y desgaste pintado. El brillo se concentra en pequeñas piezas metálicas.
- **Lectura RTS:** conservar sombrero, herramienta lateral, carga y un paño de equipo visible desde arriba. Agrupar cuerda/cereal; costuras y hebillas pequeñas no justifican geometría individual. Fuente principal del candidato `aven/tender`.

## 02 — Piratas

- **Proporciones y silueta:** humanos de hombros fuertes; abrigo largo y tricornio del capitán, torso descubierto del abordador, sombrero amplio del corsario y mochila de la exploradora. Las poses son asimétricas y abiertas.
- **Paleta y materiales:** rojo vino, azul marino, marfil y cuero oscuro con latón cálido; hierro gastado y madera de pistolas/cofres. El oro no cubre toda la superficie.
- **Armas, ropa y accesorios:** sables curvos grandes, pistolas alargadas, pañuelos, pantalones amplios, correajes, loro, telescopio, mapa, cuerda y cofre. El abrigo crea una masa legible; las cadenas pequeñas crean ruido.
- **Lenguaje y render:** jerarquía naval carismática y práctica, con reflejos suaves sobre metales envejecidos. El capitán tiene mayor riqueza de bordes y color.
- **Lectura RTS:** elegir un accesorio dominante por rol; no acumular pistola, sable, mapa y animal en cada tropa. Son referencias futuras, no una facción pirata, sistema de pólvora o contenido naval ya habilitados.

## 03 — Guerreros

- **Proporciones y silueta:** anatomía humana heroica sin extremos; hombros protegidos, piernas estables y manos amplias. El escudo alargado del reino contrasta con los redondos y con la gran masa de piel del montañés.
- **Paleta y materiales:** acero frío con azul/marfil/oro; metal cálido con crema y rojo; cuero y tela portuarios; hierro oscuro, madera y piel norteños. Separación visible entre placa, malla, tela y cuero.
- **Armas, ropa y accesorios:** espada recta, sable curvo, alfanje y hacha; hojas anchas y escudos grandes. Cascos, tabardos, capas cortas, fajines y hombreras establecen la cultura antes que sus emblemas.
- **Lenguaje y render:** disciplina del reino, capas desérticas, movilidad marítima y peso montañés. Biseles iluminados y superficies continuas hacen que las placas parezcan metal, no bloques pintados.
- **Lectura RTS:** escudo, casco y paño superior deben funcionar juntos en una masa de tropas. Para `aven/reedguard` se toma la construcción material del guerrero del reino y se **mantiene la lanza y el escudo de su rol existente**; esta imagen no autoriza convertirlo en espadachín.

## 04 — Enanos

- **Proporciones y silueta:** cuerpos bajos y anchos, torso dominante, piernas cortas robustas y manos grandes. Barbas trenzadas, martillo, mochila minera y aparato de cristales generan perfiles distintos de un humano reducido.
- **Paleta y materiales:** hierro/latón, cuero marrón, rojo de forja, azul de escudo y cian de cristal. Piel y pelo mate separan rostro y barba de la armadura.
- **Armas, ropa y accesorios:** martillos de cabeza enorme, pico ancho, gran escudo redondo, delantales, bolsas, linternas, gafas y maquinaria a la espalda. Las barbas se leen como mechones agrupados.
- **Lenguaje y render:** minería, ingeniería y defensa; metal pesado con cantos cálidos y pequeñas fuentes luminosas. La forja y la tecnología se expresan mediante masas, no mediante un cambio uniforme de escala.
- **Lectura RTS:** preservar ancho corporal, barba y herramienta; simplificar grabados y tubos. La raza enana es una referencia de futura exploración visual y no se asigna automáticamente a Skeld, Drakeforged ni a una facción nueva.

## 05 — Jinetes

- **Proporciones y silueta:** figura larga de montura más volumen elevado de jinete; caballo armado, camello, caballo portuario y caballo lanudo. Lanzas y estandartes prolongan la vertical sin ocultar el cuerpo del animal.
- **Paleta y materiales:** azul/acero/marfil; crema/rojo/oro; cuero y madera con pañuelo rojo; grises y pieles. El pelo de montura, los tejidos de silla y la barda metálica se distinguen.
- **Armas, ropa y accesorios:** lanza/bandera alta, sable ancho, silla, alforjas, mantas, armadura frontal, cuerda y barril. Las telas de montura ofrecen una zona de equipo grande y visible.
- **Lenguaje y render:** la montura prolonga la cultura del jinete; superficies grandes y brillo dirigido mantienen jerarquía de materiales.
- **Lectura RTS:** conservar cuello/cabeza, hueco entre patas y dirección frontal. Pendiente después de estabilizar los dos humanos; el camello de referencia no transforma las caravanas decorativas actuales en caballería jugable.

## 06 — Dragones

- **Proporciones y silueta:** cuadrúpedos con dos alas independientes, cuello curvo, pecho ancho y cola prolongada. Alas abiertas y cabeza cornuda son las masas dominantes; las familias varían crestas y puntas.
- **Paleta y materiales:** metálico oro/plata; glacial blanco/azul; ígneo carbón/naranja; tormenta azul oscuro/violeta. Placas, membrana y cuernos reciben tratamientos distintos; hielo y fulgor se reservan a zonas concretas.
- **Armas, revestimiento y accesorios:** mandíbula, garras, cola y aliento sustituyen armas portadas; no hay ropa. Huevos y emblemas son complementos de la lámina, no entidades presentes por este motivo.
- **Lenguaje y render:** cuatro familias de una misma criatura prestigiosa, con contraste corporal fuerte y efectos elementales. El dibujo de cada escama aporta densidad de primer plano que no debe convertirse en miles de piezas.
- **Lectura RTS:** priorizar envergadura, cuello, cabeza, membrana y grandes placas. Mantener visibilidad de equipo y espacio para otras tropas; efectos eléctricos/niebla no deben taparlas. Futuro candidato: un dragón original antes de variantes; las skins conservan la definición de combate subyacente.

## 07 — Elfos

- **Proporciones y silueta:** cuerpos altos y estilizados, hombros estrechos, cabello y paños largos; arquero, centinela, mago y exploradora montada. Orejas y ornamento fino son secundarios a arco, escudo curvo y bastón.
- **Paleta y materiales:** verde/marrón, azul/plata y marfil/verde/oro; cuero vegetal, tela fluida y metal pulido. Cristal y magia aparecen como acentos limpios.
- **Armas, ropa y accesorios:** arco casi corporal, espada fina, bastón alto, capa de hojas, carcaj, libro, ave y montura con astas. Se repiten curvas de hoja y ramas bifurcadas.
- **Lenguaje y render:** naturaleza, precisión y elegancia, con bordes de armadura finos y grandes superficies de tela. La magia tiene zonas luminosas definidas.
- **Lectura RTS:** engrosar arco/bastón y agrupar hojas/cabello; separar capa del fondo verde por valor. Posible vocabulario futuro de fantasía; ni Verdant ni Solar se convierten por inferencia en nuevas razas jugables.

## 08 — Hombres de las montañas

- **Proporciones y silueta:** humanos macizos envueltos en piel, botas pesadas y capas irregulares. Cazador, escalador, piquero y cargador se distinguen por arco, pico, lanza y mochila respectivamente.
- **Paleta y materiales:** verde pino, rojo apagado, azul pizarra, gris y cuero; bordes claros de piel y hierro oscuro. Ámbar de linterna y cristales azules puntúan la carga.
- **Armas, ropa y accesorios:** lanza alta, pico de dos extremos, arco y equipo de escalada; capuchas, cuerda gruesa, ganchos, crampones y bolsas. El volumen del abrigo no elimina la separación de brazos.
- **Lenguaje y render:** supervivencia, altura y exploración; piel tratada como mechones grandes y tejidos gruesos con contacto claro en sus capas.
- **Lectura RTS:** reservar una herramienta o carga reconocible y una banda grande de equipo. Los mosquetones y crampones se sugieren, no se modelan exhaustivamente. Compatible como inspiración con un norte humano; no introduce habilidades de escalada ni magia en Skeld.

## 09 — Héroes

- **Proporciones y silueta:** humanos comparables a las tropas, con mayor presencia por capa, cuello de piel, corona, turbante o tricornio; el guardián montañés es más ancho, no simplemente más alto.
- **Paleta y materiales:** versiones más ricas de azul/oro, rojo/oro, azul marino/rojo y gris/acero; metal pulido, tela pesada, gemas y piel crean jerarquía.
- **Armas, ropa y accesorios:** espada ceremonial, sable, telescopio y martillo pesado; capas largas, hombreras, ave, pluma y tocado. Los adornos convergen en pocos puntos focales.
- **Lenguaje y render:** evolución premium de cada cultura. Más biseles y mejores pliegues distinguen al héroe; la ilustración no prueba una animación o mecánica especial.
- **Lectura RTS:** la silueta del manto y un arma singular deben funcionar sin aumentar la escala arbitrariamente. No se implementan héroes ni estadísticas especiales en el primer lab.

## 10 — Valle boscoso

- **Proporciones y silueta ambiental:** fortaleza dominante sobre roca, población de tejados pequeños, molinos altos y bosque en masas. El río diagonal, puentes y caminos generan jerarquía territorial.
- **Paleta y materiales:** vegetación verde profunda, cereal dorado, agua turquesa, roca gris y tejados terracota. Madera, yeso, piedra y agua mantienen superficies separadas bajo sol cálido.
- **Ropa, armas y accesorios:** las personas son demasiado pequeñas para juzgar sus proporciones o armas finas; se perciben manchas de equipo azul. Carros, cercas, troncos, puestos y maquinaria comunican economía.
- **Lenguaje y render:** asentamiento humano próspero, arquitectura pétrea de defensa y entramado civil. Vista elevada con sombras legibles, espuma y profundidad atmosférica suave.
- **Lectura RTS:** conservar rutas despejadas y graduar la densidad de decoración. Cascadas, desniveles y puentes son objetivos visuales futuros; la navegación actual de Amber Crossing sigue siendo plana y no se deduce de esta ilustración.

## 11 — Bosque fantástico

- **Proporciones y silueta ambiental:** árbol monumental con edificios insertos, torres puntiagudas, raíces y puentes orgánicos. Los cristales forman hitos verticales; claros y senderos separan grandes masas de follaje.
- **Paleta y materiales:** verde oscuro, cian/turquesa, azul de cubiertas, oro cálido y pequeñas flores violetas. Corteza, piedra, tela y cristal luminoso se distinguen por valor y acabado.
- **Ropa, armas y accesorios:** figuras pequeñas en azul, sin detalle suficiente para valorar anatomía o armas; ciervos, faroles y cristales son motivos de entorno. No acreditan unidades disponibles.
- **Lenguaje y render:** civilización integrada en naturaleza, con luz cian localizada y luz cálida interior. La riqueza procede de grandes curvas y profundidades, además del brillo.
- **Lectura RTS:** controlar oclusión del dosel y reservar claros para combate. Cristales selectivos, no bloom en toda la escena. No existe por ello un cuarto mapa jugable ni una raza nueva; referencia ambiental posterior.

## 12 — Costa caribeña

- **Proporciones y silueta ambiental:** ensenada como espacio dominante, muelles que entran en el agua, mástiles oscuros, torres de madera y fortaleza de piedra. Palmeras y arcos rocosos marcan la costa.
- **Paleta y materiales:** turquesa/cian, arena marfil, verde luminoso, madera marrón y telas rojas/negras. El agua clara, espuma y fondo costero contrastan con roca y tablones mates.
- **Ropa, armas y accesorios:** soldados diminutos; armas individuales no evaluables, mientras cañones sobre la fortaleza y aparejos navales tienen presencia. Tiendas, barriles, mina y embarcaciones apoyan el tema.
- **Lenguaje y render:** puerto carismático con civilización improvisada y defensa sólida; sol abierto y amplias superficies luminosas.
- **Lectura RTS:** mantener el borde tierra/agua y accesos al muelle inequívocos; limitar cuerdas y transparencia. Sapphire Coast puede recibir esta dirección después; su barco y puerto actuales siguen siendo decoración, sin combate naval por inferencia.

## 13 — Desierto y oasis

- **Proporciones y silueta ambiental:** ciudad amurallada, pirámides y obeliscos grandes frente a campamentos pequeños. El oasis central y los cañones organizan corredores de circulación.
- **Paleta y materiales:** arena cálida, roca naranja, agua cian, verde de palmera, acentos azul/oro y paños rojos. Adobe/arenisca mate contrastan con cúpulas y agua.
- **Ropa, armas y accesorios:** camellos y figuras pequeñas con manchas azules/rojas; no se resuelve vestuario o escala de arma individual. Toldos, grúas, cantera, botes y puentes colgantes aportan función y escala.
- **Lenguaje y render:** comercio, oasis y monumentalidad. La luz cálida conserva sombras de contacto y no borra el contraste de caminos.
- **Lectura RTS:** evitar que arena, polvo y piedra compartan un único valor. Monumentos funcionan como hitos, no como modelos arquitectónicos que copiar. Sunscar Basin mantiene su mapa y reglas actuales; ciudades, barcos y desniveles de la referencia no se declaran entregados.

## Decisiones derivadas para esta entrega

El salto buscado respecto al atlas alfa es continuidad de volumen, proporciones humanas más convincentes, piezas de vestuario que se superponen con intención y materiales que responden de manera distinta a la luz. Aumentar triángulos o añadir adornos diminutos no demuestra por sí solo esa mejora.

La primera comparación usa únicamente **Aven Tender** y **Aven Reedguard**, con equipo y estadísticas existentes. Reino es aquí un arquetipo visual de prueba, no una novena facción. Las ocho facciones y los dos universos conservan su identidad y sus restricciones. Los diseños nuevos utilizarán emblemas propios y no copiarán personajes, indumentaria completa o dragones reconocibles de otras obras. El resultado debe juzgarse en capturas de Unity a distancia RTS; este documento no declara paridad con las referencias.
