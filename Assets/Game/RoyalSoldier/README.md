# Guardia de infantería del reino

Una sola figura 3D en pose estática, preparada para revisar su dirección artística. Abrir `Assets/Game/Scenes/RoyalSoldier.unity` y pulsar **Play**, o ejecutar `Builds/RoyalSoldier/RoyalSoldier.exe`.

**1 / 2 / 3**: cámara cercana, media y RTS. Arrastrar en cercana o media gira la cámara; la rueda cambia el zoom. **H** oculta los controles.

El prefab es `Prefabs/RoyalSoldier.prefab`; sus mallas están en `Model/RoyalSoldier.fbx`. Las superficies persistentes están en `Materials/`, las texturas en `Textures/` y el shader PBR en `Shaders/RoyalSoldierSurface.shader`. La escena utiliza su propio renderer Forward y cambia el pipeline al iniciar el visor. No reemplaza las unidades de la partida.

## Autoría

La cabeza y el cuello derivan de `GEO-body_male_realistic`, de Dan Ulrich, en Blender Studio Human Base Meshes 1.4.1, CC0. La fuente y su descarga exacta están registradas en [producción anterior](../../../docs/art/kingdom-premium-production.md). La ropa, los guantes, la armadura, el escudo, la espada y sus accesorios se construyen de nuevo con los scripts `tools/art/royal_soldier_body.py` y `royal_soldier_armor.py`.

`royal_soldier_materials.py` genera las superficies matemáticas originales. Albedo neutral multiplica el tinte sRGB del manifiesto; normales usan tangente +Y; metalicidad ocupa R y suavidad absoluta A. El shader deriva del URP instalado y añade variación de color por vértice en su pase Forward. Se conserva [la licencia de Unity](Shaders/UNITY-LICENSE.md). No implementa la dispersión subsuperficial ni el brillo textil de la sonda Blender.

El patio se crea con `royal_soldier_stage.py`. Reutiliza las texturas CC0 de piedra/pavimento y el HDRI del paquete `KingdomPremium`; las rutas, autores y licencias se conservan en [su registro de producción](../../../docs/art/kingdom-premium-production.md). Ninguna captura sustituye el modelo por una imagen de referencia.

## Reproducir

La autoría usa Blender 4.5.13 y guarda sus fuentes editables en `D:/CodexTooling/royal-soldier/`. `body.blend` y `armor.blend` conservan las piezas. El ensamblado editable con texturas empaquetadas también se entrega en [RoyalSoldier.blend](../../../Artifacts/ArtReview/royal-soldier/source/RoyalSoldier.blend). Ejecutar los scripts de materiales, cuerpo y armadura antes de `assemble_royal_soldier.py`. Este último combina piezas por material y exporta el FBX sin deformar ni decimar globalmente las placas. El mapa de rutas de la estación está en esos scripts.

Con Unity cerrado, desde la raíz del proyecto:

```powershell
python tools/art/prepare_royal_soldier_shader.py
./tools/Build-RoyalSoldier.ps1 -Player
```

El wrapper ejecuta Unity 6000.3.23f1 en una carpeta de trabajo de D:, compartiendo los assets de este checkout y copiando sus settings. `-SceneOnly` reconstruye iluminación y cámaras con los materiales ya importados; `-PlayerOnly` recompila el visor desde la escena guardada. La galería y la evidencia están en `Artifacts/ArtReview/royal-soldier/`.

Este asset de revisión conserva geometría densa para evaluar la forma. No contiene rig, animaciones ni LOD de producción, y no acredita rendimiento móvil.

## Validación de esta entrega

El modelo fuente contiene 830.202 triángulos agrupados en 18 mallas; Unity importa 829.308 triángulos. El catálogo tiene 21 materiales de superficie y 60 mapas PNG. La [validación de assets](../../../Artifacts/ArtReview/royal-soldier/asset-validation.json) comprueba referencias persistentes, UV, normales y materiales. La [compilación Windows](../../../Artifacts/ArtReview/royal-soldier/build.txt) termina con cero errores y cero advertencias.

El [registro del visor](../../../Artifacts/ArtReview/royal-soldier/player/review.json) identifica la compilación `280d23e1752e4746a3b05f37018a89a5`, ejecutada en Unity 6000.3.23f1 con una RTX 3060 Ti. Capturó las tres cámaras a 1920 × 1080 y verificó la presencia de una única unidad. Esta comprobación técnica no certifica calidad artística ni FPS.

La copia local `Textures/SoldierReflection.hdr` utiliza convolución especular para que las superficies rugosas reciban reflejos filtrados. La captura de comparación se reproduce con `node tools/art/capture_royal_soldier_comparison.cjs`; las dos imágenes originales de Unity se muestran sin retoque.
