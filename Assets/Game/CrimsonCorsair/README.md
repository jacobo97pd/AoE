# Corsario Carmesí

Personaje pirata proporcionado por el usuario mediante Meshy, integrado como el héroe `crimson_corsair` de Piratas, en Navales. La definición jugable está en [greybox.json](../Resources/Definitions/greybox.json); la preparación artística conserva el diseño y las texturas de origen.

## Abrir y utilizar

Abre [la escena de revisión](../Scenes/CrimsonCorsair.unity) en Unity o ejecuta [CrimsonCorsair.exe](../../../Builds/CrimsonCorsair/CrimsonCorsair.exe). **JUGAR** inicia una partida costera con el héroe disponible y seleccionado. Con Piratas, en Navales, se recluta en el Hearth por 200 alimento y 100 metal, tarda 40 s, ocupa 3 de población y admite uno vivo o en cola por jugador. Los valores de combate vigentes están en `greybox.json`. El ámbito naval actual usa desembarcos terrestres, sin navegación ni combate entre barcos.

| Control del visor | Función |
| --- | --- |
| 1 / 2 / 3 | Cercana / media / RTS |
| I / W / R | Reposo / caminar / correr |
| A / H / D | Ataque / impacto / caída |
| Arrastrar / rueda | Girar / acercar |
| Espacio / Tab | Pausar animación / ocultar controles |

El [prefab](Resources/ImportedUnits/CrimsonCorsair.prefab) se carga como `ImportedUnits/CrimsonCorsair`. Incluye `Animator`, `LODGroup` y `CorsairAnimationDriver`. Las unidades de simulación con el identificador propio utilizan este aspecto. El controlador visual recibe los estados de movimiento y combate; las animaciones no aplican desplazamiento de raíz ni deciden daño.

## Modelo, esqueleto y movimiento

El FBX de origen contenía una malla estática de 2.386.018 triángulos sin huesos ni clips. Dos islas separan personaje y decoración. El barril, cuerda y timón se retiraron por conectividad; no se cortaron botas ni faldones. La altura del personaje, incluido el sombrero, se fijó en 2,5 m. En Blender, Z es vertical y el frente es −Y. Se mantuvo el origen X/Y del modelo al escalarlo.

| Malla | Triángulos | Vértices de Blender |
| --- | ---: | ---: |
| Corsair_LOD0 | 140.000 | 69.764 |
| Corsair_LOD1 | 45.000 | 22.264 |
| Corsair_LOD2 | 12.000 | 5.764 |

Unity puede dividir vértices en costuras de UV y normales. El manifiesto registra las cifras de preparación; `asset-validation.json` registra las cifras importadas. Cada vértice admite hasta cuatro influencias. El esqueleto tiene 27 huesos ajustados a la pose existente, con controles de abrigo, sable y loro. Las máscaras se filtran por componentes conectados; el abrigo incorpora sus ribetes por distancia sobre la superficie y los pesos se suavizan antes de limitar influencias.

| Clip | Duración | Bucle |
| --- | ---: | --- |
| Corsair_Idle | 3 s | Sí |
| Corsair_Walk | 1,2 s | Sí |
| Corsair_Run | 0,8 s | Sí |
| Corsair_Attack | 0,9 s | No |
| Corsair_Hit | 0,5 s | No |
| Corsair_Death | 1,6 s | No |

Los clips se han creado por claves sobre un rig Generic; no son mocap ni un paquete Humanoid. No hay rig facial. El modelo posado y la ropa unida requieren revisar intersecciones al añadir movimientos más amplios. Los LOD no sustituyen una medición de rendimiento con ejércitos completos.

## Material y procedencia

[CrimsonCorsair.mat](Materials/CrimsonCorsair.mat) utiliza `Universal Render Pipeline/Lit`, opaco y con normales tangentes. Los cuatro PNG originales de 2048 × 2048 son `BaseColor.png`, `Normal.png`, `Metallic.png` y `Roughness.png`. Se usan las imágenes externas del archivo entregado, no las previsualizaciones JPG embebidas en el FBX. `MetallicSmoothness.png` empaqueta R=metal y A=255−rugosidad; los multiplicadores de metal y suavidad son 1. Color usa sRGB; metal, rugosidad y normales se tratan como datos lineales. Los mapas conservan mipmaps y filtrado trilineal.

ZIP proporcionado por el usuario: `C:/Users/jacob/Downloads/Meshy_AI_Crimson_Corsair_Comma_0911070912_texture_fbx (9).zip`.

SHA-256 de la fuente: `ed4163d7e2fde97f7142391ac537a15ff560c4b4b8564371715c87fa78cf642c`.

No se atribuye una licencia nueva al personaje ni se incorpora el ZIP al repositorio. La preparación no descarga personajes externos. El patio de revisión reutiliza los recursos CC0 del proyecto, descritos en [la documentación previa del entorno](../../../docs/art/kingdom-premium-production.md).

## Reproducir y revisar

La fuente editable con materiales y clips está en [CrimsonCorsair.blend](../../../Artifacts/ArtReview/crimson-corsair/source/CrimsonCorsair.blend). [model-manifest.json](model-manifest.json) registra el hash del FBX, el rig, los LOD y las duraciones.

La preparación original usa `tools/art/prepare_corsair_mesh.py` sobre `D:/CodexTooling/crimson-corsair/imported.blend`; `tools/art/rig_corsair.py` añade pesos, clips y exporta el FBX. Estos scripts requieren Blender y los archivos de trabajo conservados en esa ruta. Para volver a importar y construir la escena y el visor desde el FBX incluido, cierra Unity y ejecuta desde la raíz:

```powershell
.\tools\Build-CrimsonCorsair.ps1 -Player
```

El constructor guarda escena, prefab, material, capturas y datos de importación en [Artifacts/ArtReview/crimson-corsair](../../../Artifacts/ArtReview/crimson-corsair/index.html). `asset-validation.json` describe su alcance: geometría persistente, pesos, UV, clips y desplazamiento de vértices muestreado. La aceptación artística, el equilibrio y el rendimiento son evaluaciones distintas.
