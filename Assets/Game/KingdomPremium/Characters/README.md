# Personajes Kingdom — estudio visual 3D

Worker, Warrior y Hero son mallas 3D estáticas posadas, con tres LOD por FBX. No incluyen un rig de animación ni acciones jugables nuevas.

## Procedencia

La anatomía parte de **Body Male - Realistic**, de **Dan Ulrich**, incluida en Blender Studio Human Base Meshes v1.4.1. El README del paquete, la colección del asset y la publicación oficial indican **CC0**. Se conserva el reconocimiento de autor aunque CC0 no obliga a atribuir.

- Publicación y licencia: https://www.blender.org/download/demo-files/
- Documentación: https://developer.blender.org/docs/release_notes/4.0/asset_bundles/
- Descarga exacta: https://download.blender.org/demo/asset-bundles/human-base-meshes/human-base-meshes-bundle-v1.4.1.zip
- SHA256 del ZIP: `811f43accbb31a88266d932f8f5563b2d13586fca0ba2693aad1f5fe582b3515`

Se usa únicamente la anatomía de esa colección CC0. No se usa ni se redistribuye el rig Rain. Un bloque de texto antiguo sobre Rain permanece dentro del paquete original; no describe la licencia CC0 de la colección seleccionada.

Vestuario, equipo, accesorios, peinado, ajustes de proporción y pose se construyen en los scripts propios `tools/art/kingdom_characters.py` y `tools/art/kingdom_character_meshes.py`. No reutilizan la geometría anterior de ArtOverhaul. La flor de lis es un motivo heráldico tradicional, modelado con geometría propia. La textura RoyalEmbroidery tiene su procedencia documentada en el conjunto de texturas del proyecto.

## Contrato de importación

- Archivos: `Worker.fbx`, `Warrior.fbx`, `Hero.fbx`.
- Un mesh por LOD; nombres `Worker_LOD0`, `Worker_LOD1`, `Worker_LOD2`, equivalentes para los demás.
- Submallas por material semántico; accesorios unidos al mesh, sin miles de GameObjects.
- Geometría anatómica real para cara y manos; ojos con esclera, iris y pupila geométricos.
- Fuente Blender: metros, arriba +Z, frente -Y. Exportación FBX: `axis_forward='Z'`, `axis_up='Y'`, `bake_space_transform=True`. La inspección nativa en Unity mostró los tres personajes de espaldas: **cada raíz importada requiere un giro de 180° alrededor de Y**, igual que el entorno. La convención final de la escena es arriba +Y, frente -Z y pies Y0; se comprueba en Unity, no únicamente con el vector calculado por Blender.
- Cuerpo aproximadamente 2,1 m; sombrero, corona y armas pueden superar esa altura.
- LOD0 conserva la malla autorada completa, sin decimación global, con un máximo de 300.000 triángulos por figura. LOD1 se reduce a aproximadamente 100.000 y LOD2 a 35.000. La limpieza de degenerados puede cambiar ligeramente las cifras. Son presupuestos de esta muestra PC, no una validación de rendimiento móvil.
- UV0 presentes. Los UDIM de la anatomía se reorganizan al rango 0..1; la ropa y el equipo llevan UV propias. Las coordenadas de algunas fibras geométricas pequeñas son de detalle genérico, no un atlas único pintado de cada hebra.

`blender-export-validation.json` valida la reimportación de geometría, normales y UV y registra el SHA256 de cada FBX. No valida la orientación visible tras el importador de Unity. La corrección de raíz se documenta en `unityImport.rootYawDegrees` de los manifiestos. Las capturas nativas de referencia están en `Artifacts/ArtReview/kingdom-premium/hero.png` y `medium.png`.

## Materiales

Los JSON respectivos contienen las cifras exactas de cada exportación y los materiales. `baseColor` contiene valores **lineales** de Blender Principled; Unity debe convertirlos a sRGB al alimentar una propiedad Color que almacene sRGB. `metallic` y `smoothness` se expresan en 0..1; `smoothness = 1 - roughness`.

`RoyalEmbroidery` usa su imagen en lugar del color azul constante; no debe multiplicarse de nuevo por azul. `Hair`, `HairLight`, `HairRoyal` y `HairSilver` usan `Characters/Textures/HairGrain.png` como variación gris que **multiplica** su baseColor. HairGrain es una textura procedural propia, no una fotografía ni un retrato impreso sobre una superficie.

Se conservan nombres semánticos Skin, SkinLips, EyeIvory, EyeIris, EyePupil, Steel, SteelDark, Gold, BlueCloth, RoyalEmbroidery, IvoryCloth, Leather, LeatherLight, Hair, HairLight, HairRoyal, HairSilver, Wood, Straw, StrawLight, Fur, FurSpot y CapeLining. Los materiales de preview de Blender no sustituyen la iluminación ni las texturas PBR de la escena Unity.

## Reproducción

Blender 4.5 LTS, sin Unity ni servicios externos de generación 3D. Ejemplo:

```powershell
$env:TEMP='D:/CodexTooling/kingdom-premium/temp'
$env:TMP='D:/CodexTooling/kingdom-premium/temp'
& 'D:/CodexTooling/blender-portable/blender-4.5.13-windows-x64/blender.exe' --factory-startup --background --disable-autoexec --python 'tools/art/kingdom_characters.py' -- --kind Worker --output 'D:/CodexTooling/kingdom-premium/refined-characters'
```

Los .blend y previews intermedios se guardan en D:/CodexTooling/kingdom-premium. Las capturas front/side/back/face se renderizan desde las mismas mallas exportadas, en Blender Cycles. No son arte conceptual ni pruebas de rendimiento de la escena final.

## Refinamiento de prendas y manto

El arnés del Worker contiene dos bandas continuas con grosor, ajustadas a la ropa final evaluada mediante proyección de superficie y ancladas al cinturón. Se retiraron las tiras antiguas superpuestas y el pespunte de suela que sobresalía de la horma. El cuello del Hero conserva una base curvada continua cubierta por mechones cortos de longitudes, direcciones y tonos irregulares. Sus sabatones y placas mantienen la geometría completa; únicamente la horma de cuero cubierta y el soporte del cuello usan una subdivisión autorada menor para respetar el presupuesto de LOD0.

| Figura | LOD0 autorado antes de limpieza | LOD0 real | LOD1 real | LOD2 real | Slots por LOD |
|---|---:|---:|---:|---:|---:|
| Worker | 227.758 | 227.654 | 99.999 | 34.999 | 16 |
| Warrior | 274.242 | 274.134 | 100.000 | 34.999 | 15 |
| Hero | 293.198 | 292.168 | 100.000 | 34.829 | 20 |

Las cifras reales proceden del FBX reimportado. El archivo Hero LOD2 tiene 34.829 triángulos válidos, frente a 35.000 calculados antes del intercambio FBX. Los JSON conservan ambas mediciones mediante `beforeFbxReimport`; no se fuerza un conteo idéntico a costa de cambiar la silueta. Las nueve mallas pasan las comprobaciones de coordenadas finitas, normales y UV.

Tras generar los tres FBX, ejecutar `tools/art/kingdom_character_validate.py` con Blender y `-- --folder D:/CodexTooling/kingdom-premium/refined-characters`. Este paso registra los conteos reales en los JSON y el informe de validación sin modificar los FBX.
