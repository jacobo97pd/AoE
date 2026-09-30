# Emberfield — Atlas visual de la alfa

Abre [index.html](index.html) en tu navegador. Funciona sin conexión: las fichas se incluyen dentro del HTML y las imágenes se cargan desde esta carpeta.

Busca por nombre, cambia de categoría, facción o universo, y pulsa cualquier imagen para ampliarla. Dentro del visor puedes usar las flechas del teclado y Escape. El catálogo general muestra las entradas marcadas como representativas; activa las variantes para comparar todas las facciones.

## Qué muestran las imágenes

Son capturas de inspección renderizadas por Unity a partir de las mallas, materiales y escenarios actuales. Los modelos se presentan aislados y las vistas de mapas no tienen niebla. No son ilustraciones generadas ni capturas de una partida normal en curso. Cada ficha se encuadra por separado, por lo que su tamaño en pantalla no compara la escala real entre unidades.

Las facciones históricas y fantásticas tienen PvP separado. Dragones, trolls, leones, guardianes y elefantes de guerra son ejército. Los barcos, muelles y caravanas de camellos del paisaje son decoración. Las fichas de variantes pueden compartir malla; el número de imágenes no equivale al número de esculturas únicas.

Las 12 apariencias son cosméticas y no añaden ventajas de combate. Los tres estilos arquitectónicos se muestran sobre un Keep como ejemplo; esta galería no enumera todas las combinaciones de estilos y edificios. Las cifras de vida, población y coste, cuando aparecen, proceden de las definiciones base y no incluyen mejoras temporales o bonificaciones de facción.

El arte actual sigue siendo provisional. Las vistas completas exponen adornos fuera del suelo en los bordes y agua demasiado gris en costa y oasis; son detalles existentes de la alfa que requieren pulido.

## Contenido

| Categoría | Fichas |
| --- | ---: |
| Personajes y ejército | 65 |
| Edificios | 89 |
| Recursos | 8 |
| Objetos del paisaje | 17 |
| Objetivos | 1 |
| Proyectiles y efectos | 3 |
| Apariencias | 12 |
| Estados visuales | 4 |
| Mapas | 12 |

Total: **211 fichas** y **7 láminas**.

Los tres escenarios jugables se pueden emplear en ambos universos. Si aparecen escenarios de diagnóstico, sus fichas lo indican expresamente.

## Procedencia

- Unity: `6000.3.23f1`.
- Fuente declarada por el renderizador: `c8d9024047656b95046d80ddc5b1d6e370cfa36b`.
- Generación de imágenes: `2026-09-10T13:50:58.2770535Z`.
- Inventario de modelos: [manifest.json](manifest.json).
- Inventario de mapas: [maps.json](maps.json).

Regenerar las capturas de Unity, los manifiestos y el visor completo:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools/Export-VisualAtlas.ps1
```

Reconstruir solo el HTML y sus notas a partir de las imágenes y manifiestos existentes, comprobando que todas las imágenes estén presentes:

```powershell
python tools/build-visual-atlas.py --strict
```

El generador del visor no cambia el ejecutable, las reglas, las mallas ni las imágenes.

## Vistas completas de los mapas

- [Amber Crossing · mapa completo](map-amber_crossing.png) — inspección del escenario sin niebla.
- [Sapphire Coast · mapa completo](map-sapphire_coast.png) — inspección del escenario sin niebla.
- [Sunscar Basin · mapa completo](map-sunscar_basin.png) — inspección del escenario sin niebla.
