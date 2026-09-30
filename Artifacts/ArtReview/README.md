# ArtStyleLab — revisión humana 2

Abre [index.html](index.html). La galería funciona sin conexión: compara las cámaras, cambia la referencia entre trabajador y guerrero y pulsa las imágenes para ampliarlas. En el visor funcionan Escape y las flechas izquierda/derecha.

## Alcance

Dos candidatos originales de Aven: Tender (trabajador) y Reedguard (lancero). Son modelos 3D del laboratorio, con tres LOD, material PBR compartido y rig de 16 huesos con articulaciones rígidas. La animación inicial se limita a Idle, Walk y Gather o Attack01. No son personajes finales de deformación suave ni un set completo de animaciones de producción.

La cámara RTS normal es la referencia de aceptación: ortográfica, inclinación 55°, yaw 35°, zoom 8. Close/Mid ayudan a inspeccionar materiales y proporciones. La fila trasera prueba cuatro colores de equipo. El laboratorio permanece separado de las partidas; no se han cambiado estadísticas, facciones ni el roster completo.

## Evidencia

Las seis imágenes obligatorias proceden de Unity: [RTS](rts.png), [media](mid.png), [cercana](close.png), [comparación con la alfa anterior](comparison.png), [recolección](gather.png), [ataque](attack.png). Las dos últimas son fotogramas y no acreditan por sí solas la calidad temporal de las animaciones.

Se incluye `player-review.json` y las capturas del ejecutable disponibles. El informe acredita el recorrido de captura del laboratorio de Windows, no rendimiento móvil.

Avisos de esta generación:

No hay avisos de integridad de archivos de captura.

El [informe de assets](asset-validation.json) indica `passed: true`, Unity `6000.3.23f1`, generado `2026-09-10T17:00:57.3004249Z`. Esto valida integridad y presupuestos de autoría; no supone aprobación artística, paridad con los conceptos ni mediciones FPS/GPU.

| Candidato | LOD | Triángulos | Vértices | Huesos |
| --- | ---: | ---: | ---: | ---: |
| tender | 0 | 5446 | 7196 | 16 |
| tender | 1 | 2960 | 4440 | 16 |
| tender | 2 | 940 | 1577 | 16 |
| reedguard | 0 | 6440 | 8744 | 16 |
| reedguard | 1 | 3348 | 5119 | 16 |
| reedguard | 2 | 968 | 1672 | 16 |

Materiales compartidos: 1. Texturas referenciadas: 1. No se han validado 30–60 FPS o cargas de 50–300 unidades en móvil/tablet físico para estos candidatos.

## Referencias conceptuales

Los archivos de `references/` son copias íntegras de las láminas 1 y 3 aportadas por el usuario. No se generan imágenes, se retocan píxeles ni se incorporan las láminas como sprites de los personajes. Son referencias, no capturas de gameplay.

SHA-256 de las copias verificadas byte a byte:

- `worker-reference.png`: `0a45a5df934ad966af2ec653ae80d17962505a6c9cb4d80543ec05cf26024cbd`
- `warrior-reference.png`: `e688fa31f6498b781322d48dea6cc34418bee31c0479f60ca8c61ae9ea6558da`

## Regenerar

Desde la raíz del proyecto, cuando existan las seis capturas y `asset-validation.json`:

```powershell
python tools/build-art-review-gallery.py
```

Opciones: `--output Artifacts/ArtReview` y `--reference-dir C:/Users/jacob/Downloads`. El generador devuelve error y enumera los archivos obligatorios ausentes; no crea un HTML que simule evidencia pendiente. Las capturas del ejecutable se incorporan al volver a ejecutar el generador cuando estén listas. No modifica informes, shaders, modelos ni capturas.

La aprobación de la dirección corresponde al checkpoint humano 2 antes de ampliar el kit.
