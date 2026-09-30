# Tripulación pirata: integración de los tres modelos Meshy

**Actualización del saqueador:** véase [carrera y muerte del biped Meshy](raider-meshy-animation-replacement.md). Esa versión sustituye solo el modelo, el esqueleto y las animaciones de `BoardingRaider`; las cifras de deformación y el atlas compartido descritos abajo documentan su versión anterior. El pistolero, la buscadora y el capitán conservan sus assets.

Se han separado y preparado los tres personajes del FBX entregado por el usuario: **Saqueador de abordaje**, **Corsario de pólvora** y **Buscadora de tesoros**. Conservan la geometría característica, el vestuario, los accesorios y el atlas de texturas original. Pertenecen exclusivamente a la facción **Piratas** del ámbito **Navales**, junto al Corsario Carmesí existente.

Desde la reclasificación del 14 de septiembre de 2026, las revisiones jugables también usan Piratas en `sapphire_coast`. Se conservan modelos, materiales y clips. Navales describe por ahora partidas terrestres de desembarco; el juego todavía no incluye navegación ni combate entre barcos. Los resultados fechados más abajo corresponden a la entrega anterior.

## Procedencia y preparación

Fuente local: `C:/Users/jacob/Downloads/Meshy_AI_Crimson_Tide_Buccanee_0911083354_texture_fbx.zip`.

SHA-256 del ZIP: `966165df0518639ce9c7e23db7b150bb48aeb7ac397dcacdbadac24df7405012`.

El archivo contiene un único objeto estático de **3.980.198 triángulos**, sin esqueleto ni animaciones. Sus islas desconectadas permiten separar los personajes y retirar el barril, los cofres y los restos decorativos de suelo sin recortar sus cuerpos. Esos elementos se conservan en `D:/CodexTooling/pirate-crew/decor.blend`; el ZIP original permanece intacto.

Cada personaje dispone de tres LOD con **140.000 / 45.000 / 12.000 triángulos** antes de posibles ajustes locales de rigging. La simplificación conserva las UV y la asignación del material. La altura de preparación es 2,35 m, contando sombrero o arma elevada, con los pies en el suelo; el frente en Blender es −Y. Los manifiestos de cada modelo contienen los recuentos definitivos y la huella del FBX exportado.

Los tres personajes comparten cuatro mapas originales de **2048 × 2048**: color base, normal, metal y rugosidad. El importador usa **URP/Lit**, conserva el color base sin teñirlo y convierte la rugosidad a suavidad en el alfa de `MetallicSmoothness.png`. La piel, el cuero, la tela, la madera y el metal mantienen así el detalle del material entregado, en lugar de sustituirse por colores uniformes.

## Esqueleto y movimiento

Cada pose original tiene un esqueleto Generic ajustado a sus articulaciones y accesorios: 26 huesos en el saqueador, 27 en el corsario y 28 en la buscadora. Se distribuyen hasta cuatro influencias por vértice. La espada, las pistolas, el telescopio y el mapa reciben selecciones rígidas específicas para evitar que se doblen con la ropa o arrastren la cara.

La simplificación a 140.000 triángulos suelda superficies que en el original estaban separadas por milímetros:
- el puño derecho con la funda, la pistola y la bolsa;
- la hoja del sable con el pañuelo y la barba;
- los faldones con el pantalón;
- la mochila de la buscadora con su faldón.

El rig recuerda qué región asignó cada vértice (pieza, brazo, faldón con nombre, pierna o torso) y corta la costura entre regiones solo dentro de una caja de contacto. El hombro y la cintura siguen unidos fuera de esa caja, y cada copia de la costura conserva los pesos de su propio lado antes del suavizado.

Junto a la cara, el sable se separa del pañuelo, la barba y la piel mediante el mapa de metal original. Después, una limpieza de islotes rellena las vetas oscuras de la hoja y descarta motas sueltas, como los remaches o el pendiente. Los antebrazos usan un radio menor que el brazo, para que el puño no arrastre la funda que roza. Como el hueso del antebrazo pasa cerca de su cara posterior, ese radio dejaba en el cuerpo el brazal del saqueador y parte de las mangas del corsario y la buscadora; al balancear el brazo, se abrían en astillas. Ahora cada manga del brazo derecho se toma entera: un cilindro de 17 cm alrededor del antebrazo, desde algo por encima del codo hasta pasada la muñeca, salvo por el lado que mira al cuerpo, donde el cinturón y la funda siguen a la cadera. La faja roja trasera del saqueador se reconoce por su color en el atlas y mezcla cadera, faldón y ambos muslos, en lugar de partirse entre las dos piernas.

El brazo del sable y la cabeza del saqueador siguen al pecho en todos los clips salvo `Attack`. Así, la hoja apoyada en el hombro no cruza la cara al andar, recibir un golpe o caer. Por el mismo método, la buscadora mantiene siempre el catalejo junto al ojo. El brazo del mapa también queda fijo delante del pecho, salvo en `Work` y `Attack`: al andar no se balancea y, al caer, la manga no se separa del torso.

Los clips son `Idle`, `Walk`, `Run`, `Attack`, `Hit` y `Death`; la buscadora añade `Work` y el corsario de pólvora, `Aim`. Los cuatro piratas, capitán incluido, comparten el módulo [pirate_motion.py](../../tools/art/pirate_motion.py), que genera cada clip sobre el esqueleto ajustado a la pose. El desplazamiento sigue siendo cosa de la simulación, con root motion desactivado.

- **Marcha y carrera.** Las piernas oscilan en torno a la vertical aunque la pose esculpida adelante una de ellas. La rodilla se pliega al empezar el balanceo y llega casi recta al apoyar el talón; el pie rueda del talón a la punta, y la cadera acompaña con giro y balanceo lateral. Cada fotograma se apoya en el suelo: el cuerpo sube a mitad del apoyo y, al correr, los dos pies despegan en la fase de vuelo, con una oscilación vertical de unos 7 cm.
- **Pies anclados.** En reposo, golpe, ataque, apuntado y trabajo, un solucionador de pierna de dos huesos mantiene los tobillos en su sitio mientras la cadera arremete, retrocede o se agacha.
- **Ataques.** La simulación aplica el daño en el tick en que empieza el ataque, así que el impacto llega hacia los 0,2 s: el saqueador descarga el sable desde el hombro y el capitán corta en horizontal. El corsario dispara desde la pose de apuntado, retrocede y vuelve a apuntar; entre disparos permanece en `Aim` en vez de bajar la pistola.
- **Trabajo.** La buscadora se agacha sobre el hallazgo y lo rasca con la mano del mapa una vez por segundo, sin apartar el catalejo del ojo.

El rig mide a qué velocidad retrocede el pie de apoyo en `Walk` y `Run`, y la guarda en el manifiesto de cada modelo. El juego reproduce la zancada a la velocidad simulada dividida por esa cifra, para que los pies no patinen, y pasa a correr por encima de la media geométrica de ambas. Al desplazarse, los personajes giran como mucho 540° por segundo, en vez de encarar al instante cada tramo del camino; encarar al objetivo al atacar sigue siendo inmediato.

| Personaje | Marcha | Carrera |
| --- | ---: | ---: |
| Saqueador de abordaje | 1,42 m/s | 3,16 m/s |
| Corsario de pólvora | 1,32 m/s | 2,96 m/s |
| Buscadora de tesoros | 1,29 m/s | 2,49 m/s |
| Corsario Carmesí | 1,39 m/s | 3,10 m/s |

Estos modelos parten de una pose artística ya formada. No tienen rig facial, dedos articulados individualmente, simulación de tela ni captura de movimiento. Los objetos sostenidos conservan el agarre original. Donde se corta una costura soldada, la pieza que se aleja puede dejar una pequeña abertura en la superficie que tocaba, porque el modelo no incluye la geometría oculta bajo el contacto. Las piernas pueden atravesar ligeramente los faldones liberados al correr. El clip `Attack` de la buscadora es una acción de presentación: su unidad está desarmada en la simulación.

## Funciones en partida

Los costes y valores provienen del catálogo `greybox.json`. Cada unidad ocupa una plaza y está restringida a Piratas en Navales; las facciones Históricas y Fantasía no pueden reclutarla ni recibirla en snapshots. Ninguna de estas tres unidades hereda el límite de un héroe.

| Unidad | Función | Vida / armadura | Velocidad | Ataque | Reclutamiento |
| --- | --- | ---: | ---: | --- | --- |
| `boarding_raider` · Saqueador de abordaje | Infantería de choque: rompe tiradores y edificios | 110 / 2 | 3,7 m/s | 13 de daño; 1,05 m; intervalo 1,3 s; ×1,6 contra tiradores y ×1,5 contra edificios | Muster Hall; 60 alimento + 20 metal; 10 s |
| `gunpowder_corsair` · Corsario de pólvora | Tirador perforante contra unidades pesadas | 80 / 0 | 3,2 m/s | 18 de daño; 4,5 m; intervalo 1,8 s; proyectil 24 m/s; ×1,5 contra Heavy | Muster Hall; 40 alimento + 40 metal; 10 s |
| `treasure_seeker` · Buscadora de tesoros | Exploración, recolección y construcción | 65 / 0 | 3,5 m/s | Desarmada | Hearth; 70 alimento + 20 madera; 8 s |

La buscadora ve 12 celdas frente a las 9 habituales y tiene capacidad base de carga 10. Utiliza la recolección y construcción existentes, incluidos los modificadores de su facción. Las skins no alteran estos valores.

### Equilibrio medido (13 de septiembre de 2026)

Tras la [auditoría de reglas](../audits/auditoria-reglas-2026-09-12.md), los piratas se reajustaron para que cada uno tenga un papel y rinda lo que cuesta. Los combates son del arnés `tools/Verify-RulesAudit.ps1 --probes`, con 720 recursos por bando y el resultado igual al invertir los lados:

| Enfrentamiento | Gana | Supervivientes |
| --- | --- | --- |
| 9 saqueadores contra 9 Reedguards | Saqueadores | 1 de 9 |
| 9 saqueadores contra 9 Striders | Striders | 2 de 9 |
| 9 saqueadores contra 9 Stringwardens | Stringwardens | 4 de 9 |
| 9 corsarios de pólvora contra 9 Reedguards | Corsarios | 4 de 9 |
| 9 corsarios de pólvora contra 9 Stringwardens | Stringwardens | 4 de 9 |
| 9 corsarios de pólvora contra 9 Striders | Striders | 3 de 9 |
| 6 saqueadores contra 6 corsarios de pólvora | Saqueadores | 4 de 6 |
| Corsario Carmesí (300) contra 3 Reedguards, 3 Striders o 3 Stringwardens (240) | Corsario | 164, 137 y 50 PV de 380 |

Como referencia, en el triángulo básico con el mismo presupuesto los Stringwardens baten a los Reedguards con 6 de 9 supervivientes, los Reedguards a los Striders con 5 y los Striders a los Stringwardens con 6.

## Archivos y reproducción

- [Preparación de las mallas](../../tools/art/prepare_pirate_crew.py).
- [Configuración anatómica y máscaras](../../tools/art/pirate_crew_rigs.json).
- [Generación de esqueletos y clips](../../tools/art/rig_pirate_crew.py).
- [Auditoría de deformación](../../tools/art/audit_pirate_crew.py): estiramiento de aristas por clip y hojas de revisión en primer plano.
- [Importador de materiales, animaciones y prefabs](../../Assets/Game/Editor/PirateCharacterAssetImport.cs).
- [Construcción de la escena de revisión](../../Assets/Game/Editor/PirateCrewBaker.cs).
- [README de los assets](../../Assets/Game/PirateCrew/README.md).

Los FBX se guardan en `Assets/Game/PirateCrew/<Nombre>/Model/<Nombre>.fbx` y las fuentes editables en `Artifacts/ArtReview/pirate-crew/source/<Nombre>.blend`. Los prefabs usan `Assets/Game/PirateCrew/Resources/ImportedUnits/<Nombre>.prefab`.

La escena de revisión es `Assets/Game/Scenes/PirateCrew.unity`; reutiliza el patio, la iluminación y las cámaras de la revisión del capitán. Permite cambiar entre los cuatro piratas, revisar sus animaciones y usar vistas cercana, media y RTS. El botón **JUGAR** abre la demostración de partida costera con sus unidades.

## Verificación registrada

La preparación verificó tres niveles de detalle por personaje, las UV, las texturas y el origen del ZIP. Las máscaras de armas y accesorios se inspeccionaron mediante renders del modelo real, incluyendo ajustes para aislar la espada del cuello, la pistola baja del faldón y el telescopio de la cara.

La auditoría de deformación pesa la LOD0 de cada personaje (unos 71.000 vértices) con el mismo código del rig y anima sus clips actuales. Recorre cada clip fotograma a fotograma alterno y cuenta las aristas que superan 2,5 veces su longitud en reposo, es decir, triángulos de goma. La tabla compara, en el peor fotograma de cada clip, el rig y los clips originales con los actuales; los máximos indican el mayor estiramiento de una sola arista.

| Personaje | Golpe | Muerte | Carrera | Ataque |
| --- | --- | --- | --- | --- |
| Saqueador | 2.024 → 180 (máx. ×57 → ×6) | 2.715 → 176 (×79 → ×8) | 2.843 → 1.145 (×48 → ×19) | 1.334 → 354 (×40 → ×15) |
| Corsario | 862 → 42 (×32 → ×7) | 1.649 → 186 (×44 → ×8) | 1.945 → 691 (×25 → ×16) | 244 → 208 (×13 → ×13) |
| Buscadora | 1.460 → 65 (×24 → ×12) | 2.591 → 96 (×30 → ×11) | 1.901 → 897 (×20 → ×15) | 301 → 22 (×10 → ×6) |

En reposo solo queda una arista estirada, en la buscadora. Lo que resta se concentra en la tela que acompaña a las piernas: la faja trasera y el faldón del saqueador y del corsario, y el cinturón delantero de la buscadora. Ese cinturón es el único punto que empeora respecto al rig original: la marcha pasa de 481 a 679 aristas y el clip `Work`, ahora un golpe de pico agachado, de 494 a 528. En el disparo del corsario queda además el hombro izquierdo. La regla de la manga no añade estiramiento: sin ella, los recuentos son iguales o algo mayores. Los informes están en `Artifacts/ArtReview/pirate-crew/validation/deformation-*.json`; los del rig original llevan el sufijo `-before`.

Quedan dos defectos que la auditoría no mide porque no estiran aristas. Al atacar, en primer plano y por detrás, persiste una astilla fina de la hoja junto al nudo del pañuelo del saqueador. Además, el modelo original funde el puño con la cadera y la cara interna de la manga con el torso: al balancear el brazo quedan dientes en la muñeca del saqueador y esquirlas de unos centímetros junto a su cadera, al codo del corsario y en la manga de la buscadora.

La revisión del movimiento mide el pie de apoyo y el contacto con el suelo en cada clip. En los clips anteriores, el pie de apoyo apenas retrocedía (entre −0,4 y 0,2 m/s): los personajes marchaban en el sitio mientras la simulación los desplazaba a más de 3 m/s. Además, los pies flotaban hasta 16 cm al correr y se hundían entre 2 y 3,5 cm en el golpe y el trabajo, y las piernas del capitán quedaban 29 cm bajo el suelo al morir. Ahora el pie de apoyo retrocede a la velocidad de la tabla anterior. En reposo, golpe, ataque, apuntado y trabajo los pies no se separan del suelo, y la muerte del capitán termina apoyada sobre él.

El conjunto de comprobaciones de simulación de la tripulación pasó **9 de 9 casos**: reclutamiento y costes, separación entre conjuntos histórico y fantástico, persistencia mediante snapshots, combate cuerpo a cuerpo, disparo, recolección, depósito, construcción y visión. Estas comprobaciones no constituyen una medición de rendimiento gráfico.

Tras regenerar los rigs y las animaciones de los cuatro piratas, la reconstrucción de Unity validó los personajes y sus 26 estados animados, y compiló el player sin errores ni advertencias. Pasaron **19 de 19 pruebas PlayMode** (tripulación, capitán y selección de ejércitos). Entre ellas, ahora se comprueba que cada prefab lleva su velocidad de zancada medida y que el corsario sigue apuntando entre disparos. También pasaron las dos revisiones nativas del mismo build:
- la del visor, con los cuatro piratas, sus tres cámaras y todas sus animaciones, incluida la de apuntar;
- la de partida, en la que la buscadora se aparta 2,3 m con una orden real antes del disparo, el pistolero apunta a 19° del objetivo en el primer disparo y la buscadora deposita 2 de metal en el Hearth.

La revisión de partida admite además `-pirateCrewRecordFrames <carpeta>`. Con ese argumento guarda la partida a 60 imágenes por segundo: cada tick de 20 Hz se presenta en tres pasos interpolados, como en una pantalla de 60 Hz, y la cámara grabada se desliza hacia el encuadre de la revisión en lugar de saltar con él. Cada captura se mantiene un segundo en la grabación. Al terminar las comprobaciones, que son las mismas que sin grabar, la partida continúa: la tripulación remata al Reedguard y la buscadora llena la mochila y la lleva al Hearth. Para que esa grabación supere la comprobación del cañón, el fogonazo sigue ahora la boca del arma mientras es visible, en lugar de quedarse donde se disparó; así acompaña el retroceso a cualquier frecuencia de pantalla.

Los resultados detallados y las capturas están en los artefactos de revisión.
