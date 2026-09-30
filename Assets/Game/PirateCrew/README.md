# Tripulación pirata

**Actualización del saqueador:** `BoardingRaider` utiliza ahora el biped entregado en los ZIP `Meshy_AI_Red_Tide_Corsair_biped`, con sus clips originales de correr y morir, material propio y LOD de 10.291 / 5.000 / 1.999 triángulos. Sus cuatro estados restantes se adaptan al nuevo rig. [Detalles y límites](../../../docs/art/raider-meshy-animation-replacement.md). Los datos del atlas compartido y del rig generado que siguen se refieren a la preparación anterior del saqueador y a los otros dos tripulantes.

Tres personajes procedentes del ZIP Meshy entregado por el usuario, con funciones propias exclusivas de Piratas en Navales:

| Asset | Unidad | Función |
| --- | --- | --- |
| `BoardingRaider` | Saqueador de abordaje | Infantería con sable |
| `GunpowderCorsair` | Corsario de pólvora | Tirador con pistola |
| `TreasureSeeker` | Buscadora de tesoros | Exploración, recolección y construcción |

Cada carpeta contiene un FBX animado y su `model-manifest.json`. Los prefabs se generan en `Resources/ImportedUnits`; cada uno usa un `Animator` Generic, un controlador de animación propio y tres mallas con LOD. El pistolero y la buscadora comparten `Materials/PirateCrew.mat` y los mapas de `Textures`. El saqueador utiliza `BoardingRaider/Materials/MeshyRaider.mat` y sus propios mapas en `BoardingRaider/Textures`.

Las mallas del pistolero y la buscadora tienen 140.000 / 45.000 / 12.000 triángulos por personaje; el saqueador, 10.291 / 5.000 / 1.999. El atlas compartido de los dos primeros tiene cuatro mapas de 2048 × 2048. Los materiales URP/Lit utilizan color base, normal y metal/suavidad; esta última textura convierte la rugosidad original al canal esperado por Unity. Se utilizan las texturas suministradas con cada modelo.

Clips: `Idle`, `Walk`, `Run`, `Attack`, `Hit`, `Death`; la buscadora añade `Work` y el corsario de pólvora, `Aim`, la pose que mantiene entre disparos. `tools/art/pirate_motion.py` genera las animaciones del pistolero, la buscadora y el capitán. Para el saqueador, `tools/art/import_meshy_raider.py` conserva `Run` y `Death` suministrados y adapta los cuatro estados restantes. El manifiesto de cada modelo guarda la velocidad del pie de apoyo en `Walk` y `Run`; el prefab la usa para ajustar la reproducción al desplazamiento. La simulación mueve la unidad; root motion permanece desactivado. La buscadora no inflige daño aunque el visor permita mostrar su clip de presentación `Attack`.

Los esqueletos se adaptan a las poses originales, con huesos propios para armas y accesorios. No incluyen rig facial, dedos independientes ni simulación física de tela. Las fuentes Blender editables están en `Artifacts/ArtReview/pirate-crew/source`.

La escena `Assets/Game/Scenes/PirateCrew.unity` permite revisar los tres modelos y el Corsario Carmesí existente con cámaras cercana, media y RTS. El pipeline reproducible está en `tools/art/prepare_pirate_crew.py`, `tools/art/pirate_crew_rigs.json`, `tools/art/rig_pirate_crew.py` y `tools/Build-PirateCrew.ps1`. `tools/art/audit_pirate_crew.py` mide el estiramiento de la malla en cada clip.

[Informe de integración y valores de juego](../../../docs/art/pirate-crew-integration.md).

Fuente: `Meshy_AI_Crimson_Tide_Buccanee_0911083354_texture_fbx.zip`, conservado en la carpeta Downloads del usuario. SHA-256: `966165df0518639ce9c7e23db7b150bb48aeb7ac397dcacdbadac24df7405012`.
