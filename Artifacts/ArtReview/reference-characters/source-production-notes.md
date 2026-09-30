# Biblioteca de personajes: fuentes y límites

El catálogo de entrada es `Artifacts/ArtReview/character-animation-intake/reference-catalog.json`. El manifiesto de entrega es `Assets/Game/ReferenceCharacters/SourceModels/manifest.json`; su campo `generatedFigures` indica cuántas figuras están exportadas realmente. Los identificadores `figure_01` a `figure_26` conservan el orden de ese catálogo.

Esta entrega contiene interpretaciones geométricas editables. Las ilustraciones sirven como referencia de familias, prendas y equipamiento; no se proyectan sobre planos, cartones ni impostores. No son reconstrucciones idénticas de esas ilustraciones ni equivalen a su calidad artística.

## Procedencia

- Las figuras 01 y 02 reutilizan los dos piratas Meshy que el usuario aportó previamente. Se conservan las mallas, tres niveles de detalle, texturas, esqueleto y animaciones del proyecto. Los originales no se modifican. Los nombres `Idle`, `Run` y `Fall` son copias de las acciones existentes correspondientes.
- Los humanos nuevos parten de la anatomía y sastrería Royal Soldier ya presentes en el proyecto: cuerpo, guantes, pliegues, costuras, cuero, placas, ribetes y relieve de armas. Su antecedente anatómico es la biblioteca Human Base Meshes de Blender Studio documentada por el proyecto. La biblioteca nueva incluye sus fuentes editables con texturas empaquetadas.
- Se añaden proporciones, prendas, barbas, cascos, coronas, capuchas, herramientas, escudos y armas por familia. Las tres figuras de jinetes de este catálogo están representadas a pie; no se han creado caballos ni un sistema de monta.
- Los tres dragones usan geometría nueva para cráneo, dientes, cuernos, placas, garras, membranas y costillas de alas. Tienen esqueleto propio de 25 huesos, incluidas alas y cuatro articulaciones de cola.

## Archivos y animación

Cada carpeta `figure_NN` de esta revisión contiene el archivo Blender editable. Cada carpeta homónima en `SourceModels` contiene FBX y manifiesto individual. Las texturas PBR se comparten en `SourceModels/Textures`, salvo las copias de piratas, que conservan sus mapas propios. El FBX se escribe primero en el scratch D: y se sustituye de forma atómica.

Los nuevos humanoides tienen 18 huesos. Las acciones nuevas son `Idle` (61 fotogramas), `Run` (25) y `Fall` (49), a 30 fps. Se exportan curvas horneadas y pesos normalizados, con piezas de equipo ligadas a las manos. `Run` es un ciclo y `Fall` termina en una postura caída. No se ha creado una animación de ataque nueva para cada arma ni una simulación de tela, pelo, vuelo o ragdoll. El adaptador de Unity decide los nombres y alternativas de reproducción para las acciones ausentes.

La caída humana se revisó en un render real: se cambió el giro lateral por una caída de espaldas para que el escudo no sostuviera el cuerpo por su canto. Los dragones despliegan las alas al caer y articulan la cola para que no sirva como apoyo vertical. El ajuste de altura usa los vértices deformados; es una animación básica, no una garantía de contactos físicos perfectos.

## Materiales y niveles de detalle

Las superficies distinguen tela, cuero, acero, dorado, piel y superficies orgánicas con mapas de color, normal y, donde corresponde, metal/suavidad. En los mapas empaquetados de Royal Soldier, R representa metal y A suavidad; ambos multiplicadores del material son 1 para conservar los valores de la textura. Los parámetros y rutas exactos están en cada manifiesto.

Se entregan tres mallas por personaje. El objetivo de los nuevos modelos es hasta 250 mil triángulos para inspección cercana, unos 40 mil para distancia media y aproximadamente 11–15 mil para RTS. El manifiesto registra los conteos reales; son presupuestos de revisión, no mediciones de rendimiento móvil. Cuando la fuente supera el presupuesto, el Blender conserva una malla densa oculta `SOURCE_EDITABLE_*` que no se incluye en el FBX.

## Verificación

`source-validation.json` registra pruebas sobre las mallas y acciones de Blender: pesos, número de LOD, presencia de exportaciones y texturas, desplazamiento real de vértices, altura de la pose caída y penetración muestreada del suelo. Los PNG son renders directos de la geometría. La importación, prefabs, cámaras y comprobaciones de Unity se documentan por separado; no deben confundirse con estas pruebas de fuente.

Reproducción desde la raíz del proyecto, con Blender 4.5:

```powershell
blender -b -t 6 -P tools/art/reference_characters.py -- --ids all
blender -b -t 4 -P tools/art/reference_characters_audit.py -- --ids all --motion-renders 18,12,6
```

Las rutas de las bases anteriores y el scratch D: están declaradas al inicio de los scripts. `reference_characters_finalize.py` permite reaplicar acciones y presupuestos de malla a las fuentes generadas, excluyendo los dos piratas originales.

## Calidad pendiente

La semejanza facial, cabello/barbas, bordado, riqueza de silueta, anatomía de dragones y poses todavía necesitan trabajo artístico manual para acercarse a las referencias premium. La generación por código no sustituye una escultura y un texturizado final de personaje. Esta entrega demuestra cobertura de modelos 3D editables e integración técnica, sin afirmar que la dirección artística esté aprobada.
