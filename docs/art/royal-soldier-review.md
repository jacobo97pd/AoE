# Soldado del reino: revisión artística

Se ha rehecho un único soldado humano de infantería y se ha integrado en una [escena de Unity](../../Assets/Game/Scenes/RoyalSoldier.unity), con [prefab reutilizable](../../Assets/Game/RoyalSoldier/Prefabs/RoyalSoldier.prefab) y [shader de superficie](../../Assets/Game/RoyalSoldier/Shaders/RoyalSoldierSurface.shader). La referencia sigue siendo el guerrero azul del reino situado a la izquierda de la lámina proporcionada por el usuario.

El cambio más visible está en la construcción del equipo. El soldado incorpora peto y espalda, hombreras por capas, protecciones de brazos y piernas y sabatones segmentados. El escudo tiene profundidad, marcos separados, remaches y una flor de lis curva modelada; sustituye el emblema angular del soldado anterior. La espada incorpora hoja con sección, guarda, empuñadura y pomo diferenciados. El faldón dividido, el pañuelo y la capa añaden superficies azules que se reconocen junto al acero y el oro. Se ajustaron casco, mangas y calzado para reducir las intersecciones detectadas durante la revisión.

Los materiales separan acero satinado, acero oscuro, oro envejecido, pintura del escudo, tela y cuero. El microtejido y la veta son discretos: los pliegues, bordes y ornamentos proceden de geometría. La piel usa variación de color por vértice; barba y bigote combinan volumen y mechones. La mayor rugosidad del acero y una reflexión filtrada eliminan la lectura de nubes como manchas sobre las placas. El shader conserva esa respuesta PBR y aplica el color facial dentro de Unity.

La presentación utiliza un patio pequeño con piedra biselada, parapeto y paño azul. El fondo gris pizarra, la luz principal moderada y el relleno frontal permiten valorar el contorno sin perderlo contra un fondo negro. Las siguientes imágenes son capturas nativas sin retoque:

- [Vista cercana](../../Artifacts/ArtReview/royal-soldier/close.png): rostro, equipo y materiales.
- [Vista media](../../Artifacts/ArtReview/royal-soldier/medium.png): proporciones y conjunto.
- [Vista RTS](../../Artifacts/ArtReview/royal-soldier/rts.png): identificación por silueta y colores.

El resultado se acerca a la referencia por la heráldica, la separación material y el equipo trabajado. Todavía hay diferencias claras: cabello y barba forman masas más uniformes, la ropa tiene menos pliegues naturales y desgaste localizado, y la silueta conserva articulaciones y volúmenes más regulares que la ilustración. No se considera igualada su calidad artística. Es un modelo posado para validación visual; no demuestra animación, deformación ni rendimiento con ejércitos completos.
