#!/usr/bin/env python3
"""Build an offline review page from unchanged Unity screenshots and supplied concepts."""
from __future__ import annotations

import argparse
import hashlib
import json
import os
from pathlib import Path
import shutil
import struct
import sys
from urllib.parse import quote

REPO = Path(__file__).resolve().parents[1]
REQUIRED = {
    "rts": ("rts.png", "Vista RTS normal", "Referencia principal de aceptación. Cámara ortográfica real: 55° de inclinación, 35° de giro y zoom 8."),
    "mid": ("mid.png", "Vista media", "Mismos candidatos y entorno; encuadre para revisar silueta, equipo y materiales."),
    "close": ("close.png", "Vista cercana", "Inspección del trabajador Tender y del lancero Reedguard. El acabado cercano se evalúa junto con la vista RTS."),
    "comparison": ("comparison.png", "Alfa anterior y candidatos", "Las figuras de la alfa anterior y las dos propuestas comparten el laboratorio para comparar formas y proporciones."),
    "gather": ("gather.png", "Tender · Gather", "Fotograma de la acción de recolección. Unity proyecta la malla deformada mediante BakeMesh para la captura síncrona del editor. El ejecutable muestra además la animación en vivo."),
    "attack": ("attack.png", "Reedguard · Attack01", "Fotograma del ataque de lanza. El rol, las estadísticas y la posición autoritativa del ejército no cambian."),
}
OPTIONAL = {
    "player-rts": ("player-rts.png", "Ejecutable · RTS", "Captura nativa del laboratorio ejecutado en Windows, con la cámara RTS."),
    "player-mid": ("player-mid.png", "Ejecutable · vista media", "Captura nativa de la cámara media del laboratorio."),
    "player-close": ("player-close.png", "Ejecutable · vista cercana", "Captura nativa de los dos candidatos dentro del laboratorio."),
    "player-controls": ("player-controls.png", "Ejecutable · controles y ataque", "Controles del laboratorio y acción del lancero. Esta escena de revisión es independiente de una partida PvP."),
    "player-gather": ("player-gather.png", "Ejecutable · recolección", "Acción de recolección con Animator y skinning activos en el ejecutable de revisión."),
    "player-walk": ("player-walk.png", "Ejecutable · caminar", "Los dos candidatos reproducen Walk en el ejecutable; locomoción en el sitio para inspeccionar la articulación."),
    "player-attack": ("player-attack.png", "Ejecutable · ataque", "Fotograma nativo de Attack01 con Animator y skinning activos en el ejecutable de revisión."),
    "player-controls-phone": ("player-controls-phone.png", "Ejecutable · controles a 720p", "Visor ejecutado en Windows a 1280 × 720: doce controles y cuatro equipos. Prueba de resolución, no de hardware telefónico."),
    "player-controls-tablet": ("player-controls-tablet.png", "Ejecutable · controles 4:3", "Visor ejecutado en Windows a 1440 × 1080: controles dentro del encuadre. Prueba de resolución, no de tablet física."),
}
FORM_FACTORS = {
    "rts-phone": ("rts-phone.png", "RTS · formato teléfono", "Render offscreen de Unity a 1280 × 720 con inclinación 55°, yaw 35° y zoom 8. Comprueba composición y tamaño en píxeles; no es una prueba en un teléfono físico."),
    "rts-tablet": ("rts-tablet.png", "RTS · formato tablet", "Render offscreen de Unity a 1440 × 1080 con inclinación 55°, yaw 35° y zoom 8. Comprueba composición y tamaño en píxeles; no es una medida de rendimiento de tablet."),
}
REFERENCES = {
    "reference-worker": ("ChatGPT Image 10 sept 2026, 18_03_48 (1).png", "worker-reference.png", "Referencia conceptual · recolectores"),
    "reference-warrior": ("ChatGPT Image 10 sept 2026, 18_03_48 (3).png", "warrior-reference.png", "Referencia conceptual · guerreros"),
}


def read_json(path: Path) -> dict:
    data = json.loads(path.read_text(encoding="utf-8-sig"))
    if not isinstance(data, dict):
        raise ValueError(f"El informe debe ser un objeto JSON: {path}")
    return data


def png_info(path: Path) -> tuple[int, int]:
    with path.open("rb") as stream:
        head = stream.read(24)
    if len(head) != 24 or head[:8] != b"\x89PNG\r\n\x1a\n" or head[12:16] != b"IHDR":
        raise ValueError(f"No es un PNG válido: {path}")
    width, height = struct.unpack(">II", head[16:24])
    if width < 1 or height < 1:
        raise ValueError(f"Dimensiones inválidas: {path}")
    return width, height


def sha256(path: Path) -> str:
    with path.open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def rel_link(path: Path, output: Path) -> str:
    return quote(Path(os.path.relpath(path, output)).as_posix(), safe="/.-")


def prepare(output: Path, reference_dir: Path) -> dict:
    problems = []
    mandatory = [output / "asset-validation.json"] + [output / row[0] for row in REQUIRED.values()]
    sources = {key: reference_dir / row[0] for key, row in REFERENCES.items()}
    for path in mandatory + list(sources.values()):
        if not path.is_file() or path.stat().st_size == 0:
            problems.append(f"Falta el archivo obligatorio o está vacío: {path}")
    if problems:
        raise ValueError("No se genera una revisión incompleta.\n" + "\n".join(problems))
    validation = read_json(output / "asset-validation.json")
    if not isinstance(validation.get("prefabs"), list) or not isinstance(validation.get("passed"), bool):
        raise ValueError("asset-validation.json no tiene el esquema esperado: passed y prefabs.")
    images = []
    for key, (filename, title, caption) in REQUIRED.items():
        width, height = png_info(output / filename)
        images.append(dict(key=key, image=filename, title=title, caption=caption, width=width, height=height, kind="unity"))
    # Validate every source before copying; concept files are never edited or resampled.
    for source in sources.values():
        png_info(source)
    (output / "references").mkdir(exist_ok=True)
    reference_hashes = {}
    for key, (_, filename, title) in REFERENCES.items():
        destination = output / "references" / filename
        source = sources[key]
        if source.resolve() != destination.resolve():
            shutil.copy2(source, destination)
        source_hash = sha256(source)
        if sha256(destination) != source_hash:
            raise ValueError(f"La copia de referencia no coincide byte a byte: {filename}")
        reference_hashes[filename] = source_hash
        width, height = png_info(destination)
        images.append(dict(key=key, image="references/" + filename, title=title, caption="Lámina conceptual aportada por el usuario; copia íntegra, sin retoque. Es una referencia de dirección artística, no un render del juego ni un asset 3D terminado.", width=width, height=height, kind="concept"))
    warnings = []
    for pose in ("gather.png", "attack.png"):
        if sha256(output / pose) == sha256(output / "close.png"):
            warnings.append(f"{pose} coincide píxel a píxel con close.png: esta captura no demuestra una pose de acción distinta.")
    player = None
    if (output / "player-review.json").is_file():
        player = read_json(output / "player-review.json")
    player_images = 0
    for key, (filename, title, caption) in OPTIONAL.items():
        path = output / filename
        if path.is_file():
            width, height = png_info(path)
            images.append(dict(key=key, image=filename, title=title, caption=caption, width=width, height=height, kind="player"))
            player_images += 1
        elif player is not None:
            warnings.append(f"El informe del ejecutable existe, pero falta {filename}.")
    if player_images and player is None:
        warnings.append("Hay capturas del ejecutable, pero todavía no existe player-review.json.")
    for key, (filename, title, caption) in FORM_FACTORS.items():
        path = output / filename
        if path.is_file():
            width, height = png_info(path)
            images.append(dict(key=key, image=filename, title=title, caption=caption, width=width, height=height, kind="format"))
    documents = [
        ("Auditoría de la alfa", REPO / "docs/art/current-art-audit.md"),
        ("Análisis de las 13 referencias", REPO / "docs/art/REFERENCE_IMAGE_ANALYSIS.md"),
        ("Objetivo de estilo y aceptación", REPO / "docs/art/ART_STYLE_TARGET.md"),
        ("Entrega y límites del checkpoint 2", REPO / "docs/art/ART_STYLE_CHECKPOINT_2.md"),
        ("Tamaño proyectado en píxeles", output / "readability.json"),
        ("Resumen de verificación", output / "verification.json"),
        ("Resultados NUnit", output / "art-style-tests.xml"),
        ("Recorrido del player a 720p", output / "player-phone-review.json"),
        ("Recorrido del player en 4:3", output / "player-tablet-review.json"),
    ]
    return dict(validation=validation, player=player, images=images, warnings=warnings, referenceHashes=reference_hashes,
                documents=[dict(name=name, href=rel_link(path, output)) for name, path in documents if path.is_file()],
                hasBuild=(output / "build.txt").is_file())


PAGE = r'''<!doctype html>
<html lang="es"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><meta name="color-scheme" content="dark">
<title>Emberfield · Revisión de arte · Checkpoint 2</title>
<style>
:root{color-scheme:dark;--bg:#10171b;--panel:#182328;--line:#35434a;--ink:#f5f1e7;--muted:#b4bfc1;--gold:#e8c58e;--teal:#9bd4cc}*{box-sizing:border-box}html{scroll-behavior:smooth;scroll-padding-top:26px}body{margin:0;background:var(--bg);color:var(--ink);font:15px/1.65 system-ui,-apple-system,"Segoe UI",sans-serif}button,a{touch-action:manipulation}button{font:inherit;cursor:pointer;color:inherit}a{color:var(--teal);text-underline-offset:4px}button:focus-visible,a:focus-visible{outline:3px solid var(--gold);outline-offset:4px}.wrap{width:min(1440px,calc(100% - 72px));margin:auto}.masthead{display:flex;align-items:center;justify-content:space-between;gap:20px;border-bottom:1px solid var(--line);padding:25px 0;font-size:12px;letter-spacing:.14em}.brand{font-weight:800;color:var(--gold);letter-spacing:.27em}.status{border:1px solid #706045;color:var(--gold);border-radius:30px;padding:5px 12px;letter-spacing:.02em}.intro{display:grid;grid-template-columns:1.2fr 1fr;gap:90px;align-items:end;padding:55px 0 32px}.eyebrow{text-transform:uppercase;letter-spacing:.16em;font-size:11px;color:var(--teal);font-weight:700;margin:0 0 12px}h1,h2{font-family:Georgia,"Times New Roman",serif;font-weight:400;line-height:1.1}h1{font-size:clamp(38px,5vw,65px);letter-spacing:-.04em;margin:0}h2{font-size:32px;margin:0}h3{font-size:17px;line-height:1.4;margin:0 0 8px}p{margin:0 0 12px}.intro p:last-child{color:var(--muted);margin:0}.intro strong{color:var(--ink)}.section{padding:44px 0;border-top:1px solid var(--line)}.section-head{display:flex;justify-content:space-between;align-items:end;gap:28px;margin-bottom:22px}.section-head p{color:var(--muted);max-width:540px;font-size:13px;margin:0}.section-head .eyebrow{color:var(--teal);margin-bottom:10px}.tabs{display:flex;gap:8px;flex-wrap:wrap}.tab{border:1px solid var(--line);border-radius:7px;background:var(--panel);padding:10px 15px;font-size:13px;min-height:44px}.tab[aria-pressed=true]{background:var(--gold);color:#20272a;border-color:var(--gold);font-weight:700}.tab:hover{border-color:var(--gold)}.stage{border:1px solid var(--line);border-radius:14px;overflow:hidden;background:#0c1216;margin-top:18px}.picture{display:block;border:0;padding:0;width:100%;background:#0f171d;position:relative}.picture img{display:block;width:100%;object-fit:contain}.picture::after{content:"Ampliar ↗";position:absolute;bottom:14px;right:15px;background:#10171be8;padding:4px 10px;border:1px solid #b4bfc166;border-radius:5px;color:var(--ink);font-size:11px}.stage>.picture img{aspect-ratio:16/9}.caption{padding:19px 22px;background:var(--panel)}.caption p{font-size:13px;color:var(--muted);margin:0}.caption .kicker{color:var(--gold);text-transform:uppercase;font-size:10px;letter-spacing:.1em;margin-bottom:7px}.stats{display:grid;grid-template-columns:repeat(4,1fr);gap:0;padding:29px 0}.stat{padding:0 25px;border-left:1px solid var(--line)}.stat:first-child{padding-left:0;border-left:0}.stat b{font-family:Georgia,serif;font-weight:400;font-size:34px;color:var(--gold);line-height:1.2}.stat span{display:block;color:var(--muted);font-size:12px;margin-top:5px}.note{padding:17px 20px;border-left:2px solid var(--gold);background:#e8c58e07;color:var(--muted);font-size:13px}.note strong{color:var(--ink)}.split{display:grid;grid-template-columns:.75fr 1.25fr;gap:22px;margin-top:20px}.card{border:1px solid var(--line);border-radius:12px;overflow:hidden;background:var(--panel)}.compare .picture{height:460px;display:flex;align-items:center;justify-content:center}.compare .picture img{height:100%;width:100%;object-fit:contain}.compare .concept{background:#242320}.compare h3{font-size:16px}.shots{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:20px}.shots .picture img{aspect-ratio:16/9}.shots .caption{padding:16px 18px}.pill{display:inline-block;font-size:11px;border:1px solid var(--line);border-radius:20px;padding:4px 10px;color:var(--muted)}.good{color:#a8dfb8;border-color:#416451}.bad{color:#ffbc9c;border-color:#785442}.table-wrap{overflow-x:auto;border:1px solid var(--line);border-radius:12px;margin-top:18px}table{width:100%;border-collapse:collapse;white-space:nowrap;font-size:13px}th,td{text-align:left;padding:13px 17px;border-bottom:1px solid var(--line)}thead{color:var(--gold);background:#202d33}tbody tr:last-child td{border:0}td:first-child{font-weight:650}td:not(:first-child){color:var(--muted)}.list{padding-left:20px;color:var(--muted);font-size:13px}.list li{padding:4px 0}.tech{display:grid;grid-template-columns:1fr 1fr;gap:28px;margin-top:24px}.tech-box{border:1px solid var(--line);padding:23px;border-radius:12px}.tech-box p{font-size:13px;color:var(--muted)}.doclinks{display:flex;gap:12px;flex-wrap:wrap}.doclinks a{font-size:12px;border:1px solid var(--line);padding:8px 12px;border-radius:6px;text-decoration:none;min-height:40px}.doclinks a:hover{border-color:var(--gold)}.checks{display:grid;grid-template-columns:repeat(3,1fr);gap:26px;margin-top:25px}.checks p{font-size:13px;color:var(--muted)}.checks span{font-size:11px;color:var(--gold);letter-spacing:.1em}.footer{border-top:1px solid var(--line);padding:25px 0 35px;font-size:11px;color:var(--muted);display:flex;justify-content:space-between;gap:20px}.dialog{background:#10171b;color:var(--ink);border:1px solid var(--line);border-radius:12px;padding:0;width:min(1500px,calc(100vw - 28px));max-height:calc(100vh - 28px);overflow:auto}.dialog::backdrop{background:#000e}.dialog-head{padding:14px 20px;border-bottom:1px solid var(--line);display:flex;align-items:center;justify-content:space-between;gap:18px}.dialog-head h2{font:600 18px/1.3 system-ui;margin:0}.dialog button{border:1px solid var(--line);background:var(--panel);border-radius:6px;padding:8px 13px;min-height:44px}.dialog>img{display:block;width:100%;height:70vh;object-fit:contain;background:#090f12}.dialog-footer{padding:16px 20px;display:flex;justify-content:space-between;gap:20px;align-items:start}.dialog-footer p{font-size:12px;max-width:850px;color:var(--muted);margin:0}.dialog-actions{display:flex;gap:8px;align-items:center;white-space:nowrap}.dialog-actions a{font-size:12px}.hide{display:none!important}.empty{color:var(--muted);font-size:13px;padding:20px;border:1px dashed var(--line);border-radius:10px}code{font-family:ui-monospace,Consolas,monospace;font-size:.88em;overflow-wrap:anywhere}.warnings{background:#30271d;border:1px solid #6f5537;padding:15px 20px;border-radius:8px;margin:15px 0;color:#efd0a3;font-size:13px}
@media(max-width:1000px){.intro{gap:35px}.compare .picture{height:360px}.checks{gap:18px}.section-head{align-items:start}.stat{padding:0 17px}}@media(max-width:700px){.wrap{width:calc(100% - 30px)}.masthead{font-size:10px;letter-spacing:.04em;padding:19px 0}.brand{letter-spacing:.18em}.status{padding:4px 8px;font-size:10px}.intro{grid-template-columns:1fr;gap:22px;padding:34px 0 27px}.intro p{font-size:14px}.section{padding:30px 0}.section-head{display:block}.section-head p{margin-top:13px}h2{font-size:28px}.stats{grid-template-columns:1fr 1fr;row-gap:21px}.stat:nth-child(3){border-left:0;padding-left:0}.stat b{font-size:29px}.split{grid-template-columns:1fr}.compare .picture{height:330px}.compare .concept{height:420px}.shots,.tech,.checks{grid-template-columns:1fr}.tabs{gap:6px}.tab{padding:9px 12px;font-size:12px}.caption{padding:15px}.caption p,.note{font-size:12px}.tech-box{padding:18px}.footer{display:block}.footer p{margin:0 0 8px}.dialog>img{height:58vh}.dialog-footer{display:block}.dialog-actions{margin-top:14px;justify-content:space-between}.dialog-head{padding:12px}.dialog-head h2{font-size:15px}.table-wrap{margin-top:12px}th,td{padding:12px;font-size:12px}}@media(prefers-reduced-motion:reduce){html{scroll-behavior:auto}}
</style></head><body><main class="wrap">
<header class="masthead"><span class="brand">EMBERFIELD</span><span class="status">Checkpoint 2 · Revisión humana pendiente</span></header>
<section class="intro"><div><p class="eyebrow">ArtStyleLab · Primera pareja de Aven</p><h1>Del concepto<br>al campo de batalla.</h1></div><p><strong>Un trabajador y un lancero, ahora como candidatos 3D articulados.</strong> Esta revisión compara la dirección conceptual con lo que Unity dibuja de verdad. La decisión se toma desde la cámara RTS normal; el primer plano ayuda a revisar el acabado.</p></section>
<div class="tabs" aria-label="Cámara de revisión" id="view-tabs"><button class="tab" type="button" data-view="rts" aria-pressed="true">RTS normal · aceptación</button><button class="tab" type="button" data-view="mid" aria-pressed="false">Media</button><button class="tab" type="button" data-view="close" aria-pressed="false">Cercana</button><button class="tab" type="button" data-view="comparison" aria-pressed="false">Alfa anterior / candidatos</button></div>
<div class="stage"><button class="picture" id="main-picture" type="button" data-zoom="rts" aria-label="Ampliar vista RTS normal"><img id="main-image" src="rts.png" alt="Candidatos Tender y Reedguard vistos con la cámara RTS real de Unity"></button><div class="caption"><p class="kicker">Render nativo de Unity · Laboratorio independiente</p><h3 id="main-title">Vista RTS normal</h3><p id="main-caption"></p></div></div>
<div class="stats" id="stats"></div>
<div id="review-warnings"></div>
<aside class="note"><strong>Prototipos de dirección artística; aprobación pendiente.</strong> Tres LOD, un material PBR compartido y un rig de 16 huesos con articulaciones rígidas. El movimiento inicial cubre Idle, Walk y Gather o Attack01. Todavía no es un personaje final con deformación suave completa ni una biblioteca de animaciones de producción. La fila trasera prueba cuatro colores de equipo.</aside>
<section class="section" aria-labelledby="references-title"><div class="section-head"><div><p class="eyebrow">01 · Dirección y traducción a 3D</p><h2 id="references-title">La referencia junto al resultado.</h2></div><p>Las láminas son conceptos aportados por el usuario. Se conservan completas, sin retoque. La imagen de Unity muestra los dos candidatos; no se afirma paridad visual.</p></div><div class="tabs" aria-label="Referencia conceptual"><button class="tab" data-reference="reference-worker" aria-pressed="true" type="button">Trabajador</button><button class="tab" data-reference="reference-warrior" aria-pressed="false" type="button">Guerrero / lancero</button></div><div class="split compare"><article class="card"><button class="picture concept" id="concept-picture" type="button" data-zoom="reference-worker" aria-label="Ampliar referencia conceptual del trabajador"><img id="concept-image" src="references/worker-reference.png" alt="Lámina conceptual de recolectores proporcionada como referencia"></button><div class="caption"><p class="kicker">Concepto · No es una captura del juego</p><h3 id="concept-title">Recolectores</h3><p>Se trabaja el arquetipo humano de Aven. Las otras culturas de la lámina quedan para fases posteriores.</p></div></article><article class="card"><button class="picture" type="button" data-zoom="close" aria-label="Ampliar render cercano de los candidatos"><img src="close.png" alt="Render 3D cercano del Tender y el Reedguard" loading="lazy"></button><div class="caption"><p class="kicker">Unity · Dos candidatos reales</p><h3>Tender y Reedguard</h3><p>Sombrero, herramienta y carga frente a casco, escudo y lanza. Identidades visuales vinculadas a las definiciones existentes, sin modificar sus estadísticas.</p></div></article></div></section>
<section class="section" aria-labelledby="motion-title"><div class="section-head"><div><p class="eyebrow">02 · Lectura de las acciones</p><h2 id="motion-title">Poses que se entienden a distancia.</h2></div><p>Fotogramas de los clips iniciales. Para valorar ritmo, anticipación y continuidad hay que reproducirlos en el laboratorio; una imagen fija no los acredita.</p></div><div class="shots" id="action-shots"></div></section>
<section class="section hide" id="format-section" aria-labelledby="format-title"><div class="section-head"><div><p class="eyebrow">La misma cámara · Dos proporciones de pantalla</p><h2 id="format-title">Lectura en teléfono y tablet.</h2></div><p>Renders offscreen a 1280 × 720 y 1440 × 1080, manteniendo 55° / 35° y zoom 8. Validan el encuadre en esas resoluciones; no el tamaño físico de pantalla, los FPS ni la experiencia en un dispositivo real.</p></div><div class="shots" id="format-shots"></div></section>
<section class="section" aria-labelledby="player-title"><div class="section-head"><div><p class="eyebrow">03 · Ejecutable de revisión</p><h2 id="player-title">Dentro del laboratorio.</h2></div><p>Capturas nativas del ejecutable de Windows cuando estén disponibles. Son pruebas del laboratorio, no de una partida normal ni de rendimiento móvil.</p></div><div id="player-status"></div><div class="shots" id="player-shots"></div></section>
<section class="section" aria-labelledby="validation-title"><div class="section-head"><div><p class="eyebrow">04 · Datos de los assets</p><h2 id="validation-title">Medidas, no estimaciones.</h2></div><div id="validation-status"></div></div><p style="color:var(--muted);font-size:13px">Inventario leído de <a href="asset-validation.json">asset-validation.json</a>. Describe integridad y presupuestos de autoría; no mide FPS, coste de skinning ni tiempo GPU.</p><div class="table-wrap"><table><thead><tr><th>Personaje</th><th>LOD</th><th>Triángulos</th><th>Vértices</th><th>Huesos</th><th>Umbral de pantalla</th></tr></thead><tbody id="lod-body"></tbody></table></div><div id="validation-problems"></div><div class="tech"><div class="tech-box"><h3>Animación disponible</h3><div id="clip-details"></div><p>La animación es local y no aplica root motion de gameplay. No se ha creado todavía el conjunto de muerte, hit, victoria, selección, carrera ni todas las tareas del trabajador.</p></div><div class="tech-box"><h3>Materiales y textura</h3><div id="texture-details"></div><p>Superficie URP compartida con máscara de equipo, metalicidad/suavidad y feedback visual. La textura fuente compartida de 1024 no es un retrato del personaje ni un mapa normal pintado. Las apariencias permanecen separadas de las estadísticas. Los materiales, luces y rig todavía requieren pruebas de coste en hardware objetivo.</p></div></div></section>
<section class="section" aria-labelledby="review-title"><p class="eyebrow">05 · Qué decidir en este checkpoint</p><h2 id="review-title">Aprobar la dirección antes de ampliarla.</h2><div class="checks"><div><span>01 / SILUETA</span><h3>Trabajador y lancero reconocibles</h3><p>Desde RTS normal, distinguir cuerpo, herramienta o arma y equipo sin recurrir a la etiqueta. Revisar lectura entre 40 y 120 píxeles de altura.</p></div><div><span>02 / MATERIAL Y COLOR</span><h3>Tela, cuero y metal separados</h3><p>Contrastar brillo y masas de color. Azul, rojo, verde y amarillo deben seguir siendo claros sin alterar la identidad de Aven.</p></div><div><span>03 / PROPORCIÓN Y MOVIMIENTO</span><h3>Formas heroicas con peso</h3><p>Evaluar cabeza, hombros, manos, agarres y apoyo de los pies. El primer plano no debe ocultar problemas de postura o deformación.</p></div></div><aside class="note"><strong>Alcance cerrado en dos candidatos.</strong> No se han sustituido las unidades del ejército ni renovado todo el roster. Caballería, piratas, enanos, elfos, héroes y dragones nuevos se dejan para después de la revisión. No se han validado 30–60 FPS ni 50–300 unidades en un móvil o tablet físico.</aside></section>
<section class="section"><div class="section-head"><h2>Fuentes y evidencia.</h2><p>La página funciona sin conexión y utiliza únicamente imágenes locales.</p></div><div class="doclinks" id="documents"></div></section>
<footer class="footer"><p id="provenance"></p><p>Emberfield · ArtStyleLab · Checkpoint humano 2</p></footer>
</main><dialog class="dialog" id="viewer"><div class="dialog-head"><h2 id="viewer-title"></h2><button type="button" id="viewer-close" aria-label="Cerrar imagen">Cerrar ×</button></div><img id="viewer-image" alt=""><div class="dialog-footer"><p id="viewer-caption"></p><div class="dialog-actions"><button type="button" id="viewer-prev" aria-label="Imagen anterior">←</button><button type="button" id="viewer-next" aria-label="Imagen siguiente">→</button><a id="viewer-original" target="_blank" rel="noopener">Original ↗</a></div></div></dialog>
<script id="review-data" type="application/json">__DATA__</script><script>
'use strict';
const data=JSON.parse(document.getElementById('review-data').textContent),byKey=Object.fromEntries(data.images.map(x=>[x.key,x]));
const $=id=>document.getElementById(id),num=x=>Number(x||0).toLocaleString('es-ES');
function el(tag,text,cls){const node=document.createElement(tag);if(text!==undefined)node.textContent=text;if(cls)node.className=cls;return node;}
function setView(key){const item=byKey[key];$('main-image').src=item.image;$('main-image').alt=item.title;$('main-title').textContent=item.title;$('main-caption').textContent=item.caption;$('main-picture').dataset.zoom=key;$('main-picture').setAttribute('aria-label','Ampliar '+item.title);document.querySelectorAll('[data-view]').forEach(b=>b.setAttribute('aria-pressed',String(b.dataset.view===key)));}
document.querySelectorAll('[data-view]').forEach(b=>b.addEventListener('click',()=>setView(b.dataset.view)));setView('rts');
document.querySelectorAll('[data-reference]').forEach(b=>b.addEventListener('click',()=>{const item=byKey[b.dataset.reference];$('concept-image').src=item.image;$('concept-image').alt=item.title;$('concept-picture').dataset.zoom=item.key;$('concept-picture').setAttribute('aria-label','Ampliar '+item.title);$('concept-title').textContent=item.key==='reference-worker'?'Recolectores':'Guerreros';document.querySelectorAll('[data-reference]').forEach(t=>t.setAttribute('aria-pressed',String(t===b)));}));
const report=data.validation,stats=[[report.prefabs.length,'candidatos de Aven'],[Math.max(...report.prefabs.map(p=>p.lods.length),0),'LOD por candidato'],[report.materialCount,'material compartido'],[report.textureCount,'texturas referenciadas']];stats.forEach(([value,label])=>{const box=el('div',undefined,'stat');box.append(el('b',num(value)),el('span',label));$('stats').append(box);});
function card(item){const c=el('article',undefined,'card'),b=el('button',undefined,'picture');b.type='button';b.dataset.zoom=item.key;b.setAttribute('aria-label','Ampliar '+item.title);const i=el('img');i.src=item.image;i.alt=item.title;i.loading='lazy';b.append(i);const cap=el('div',undefined,'caption');cap.append(el('h3',item.title),el('p',item.caption));c.append(b,cap);return c;}
['gather','attack'].forEach(key=>$('action-shots').append(card(byKey[key])));
const formatImages=data.images.filter(x=>x.kind==='format');if(formatImages.length){$('format-section').classList.remove('hide');formatImages.forEach(item=>$('format-shots').append(card(item)));}
const playerImages=data.images.filter(x=>x.kind==='player');if(playerImages.length){playerImages.forEach(item=>$('player-shots').append(card(item)));const p=data.player;$('player-status').append(el('p',p?'Ejecutable: '+(p.status==='completed'?'recorrido de captura completado':String(p.status))+' · '+num(p.width)+' × '+num(p.height)+' · Unity '+p.unity:'Capturas disponibles; informe del ejecutable pendiente.','note'));}else{$('player-status').append(el('p','Todavía no se han añadido las capturas del ejecutable a esta revisión. Las vistas anteriores proceden del render de Unity en el editor.','empty'));}
data.warnings.forEach(w=>$('review-warnings').append(el('p',w,'warnings')));
$('validation-status').append(el('span',report.passed?'Integridad de assets: válida':'Integridad de assets: incidencias', 'pill '+(report.passed?'good':'bad')));
const names={tender:'Tender · trabajador',reedguard:'Reedguard · lancero'};
report.prefabs.forEach(prefab=>{prefab.lods.forEach(lod=>{const tr=el('tr');[names[prefab.gameplayId]||prefab.gameplayId,'LOD '+lod.level,num(lod.triangles),num(lod.vertices),num(lod.bones),(Number(lod.screenRelativeHeight)*100).toLocaleString('es-ES',{maximumFractionDigits:2})+' %'].forEach(v=>tr.append(el('td',v)));$('lod-body').append(tr);});const title=el('h3',names[prefab.gameplayId]||prefab.gameplayId),list=el('ul',undefined,'list');prefab.clips.forEach(clip=>list.append(el('li',clip.name+' · '+Number(clip.length).toLocaleString('es-ES',{maximumFractionDigits:2})+' s · '+num(clip.changingBoneBindings)+' canales de hueso con variación')));$('clip-details').append(title,list);});
const problems=[...(report.problems||[]),...report.prefabs.flatMap(p=>(p.warnings||[]).map(w=>(names[p.gameplayId]||p.gameplayId)+': '+w))];if(problems.length){const d=el('details',undefined,'warnings');d.append(el('summary','Observaciones del validador ('+problems.length+')'));const list=el('ul');problems.forEach(p=>list.append(el('li',p)));d.append(list);$('validation-problems').append(d);}
const tex=el('ul',undefined,'list');(report.textures||[]).forEach(t=>tex.append(el('li',t.name+' · '+t.width+' × '+t.height)));$('texture-details').append(tex);
const links=[...data.documents,{name:'Informe de assets · JSON',href:'asset-validation.json'},{name:'Notas de esta revisión',href:'README.md'}];if(data.player)links.push({name:'Informe del ejecutable · JSON',href:'player-review.json'});if(data.hasBuild)links.push({name:'Resultado de compilación',href:'build.txt'});links.forEach(item=>{const a=el('a',item.name);a.href=item.href;$('documents').append(a);});
$('provenance').textContent='Assets comprobados con Unity '+report.unityVersion+' · '+report.generatedUtc;
const viewer=$('viewer');let selected=0;function show(index){selected=(index+data.images.length)%data.images.length;const item=data.images[selected];$('viewer-title').textContent=item.title;$('viewer-image').src=item.image;$('viewer-image').alt=item.title;$('viewer-caption').textContent=item.caption+' · '+item.width+' × '+item.height+' px';$('viewer-original').href=item.image;if(!viewer.open)viewer.showModal();}
document.addEventListener('click',event=>{const b=event.target.closest('[data-zoom]');if(b){const index=data.images.findIndex(x=>x.key===b.dataset.zoom);if(index>=0)show(index);}});$('viewer-close').addEventListener('click',()=>viewer.close());$('viewer-prev').addEventListener('click',()=>show(selected-1));$('viewer-next').addEventListener('click',()=>show(selected+1));viewer.addEventListener('click',event=>{if(event.target===viewer){const r=viewer.getBoundingClientRect();if(event.clientX<r.left||event.clientX>r.right||event.clientY<r.top||event.clientY>r.bottom)viewer.close();}});document.addEventListener('keydown',event=>{if(!viewer.open)return;if(event.key==='ArrowLeft'){event.preventDefault();show(selected-1);}if(event.key==='ArrowRight'){event.preventDefault();show(selected+1);}});
</script></body></html>'''


def readme(data: dict) -> str:
    report = data["validation"]
    rows = []
    for prefab in report["prefabs"]:
        for lod in prefab.get("lods", []):
            rows.append(f"| {prefab['gameplayId']} | {lod['level']} | {lod['triangles']} | {lod['vertices']} | {lod['bones']} |")
    hashes = "\n".join(f"- `{name}`: `{digest}`" for name, digest in data["referenceHashes"].items())
    player_text = ("Se incluye `player-review.json` y las capturas del ejecutable disponibles. "
                   "El informe acredita el recorrido de captura del laboratorio de Windows, no rendimiento móvil."
                   if data["player"] else "Las capturas y el informe del ejecutable todavía no se han añadido a esta generación.")
    warnings = "\n".join("- " + warning for warning in data["warnings"]) or "No hay avisos de integridad de archivos de captura."
    return f"""# ArtStyleLab — revisión humana 2

Abre [index.html](index.html). La galería funciona sin conexión: compara las cámaras, cambia la referencia entre trabajador y guerrero y pulsa las imágenes para ampliarlas. En el visor funcionan Escape y las flechas izquierda/derecha.

## Alcance

Dos candidatos originales de Aven: Tender (trabajador) y Reedguard (lancero). Son modelos 3D del laboratorio, con tres LOD, material PBR compartido y rig de 16 huesos con articulaciones rígidas. La animación inicial se limita a Idle, Walk y Gather o Attack01. No son personajes finales de deformación suave ni un set completo de animaciones de producción.

La cámara RTS normal es la referencia de aceptación: ortográfica, inclinación 55°, yaw 35°, zoom 8. Close/Mid ayudan a inspeccionar materiales y proporciones. La fila trasera prueba cuatro colores de equipo. El laboratorio permanece separado de las partidas; no se han cambiado estadísticas, facciones ni el roster completo.

## Evidencia

Las seis imágenes obligatorias proceden de Unity: [RTS](rts.png), [media](mid.png), [cercana](close.png), [comparación con la alfa anterior](comparison.png), [recolección](gather.png), [ataque](attack.png). Las dos últimas son fotogramas y no acreditan por sí solas la calidad temporal de las animaciones.

{player_text}

Avisos de esta generación:

{warnings}

El [informe de assets](asset-validation.json) indica `passed: {str(report['passed']).lower()}`, Unity `{report.get('unityVersion', '')}`, generado `{report.get('generatedUtc', '')}`. Esto valida integridad y presupuestos de autoría; no supone aprobación artística, paridad con los conceptos ni mediciones FPS/GPU.

| Candidato | LOD | Triángulos | Vértices | Huesos |
| --- | ---: | ---: | ---: | ---: |
{chr(10).join(rows)}

Materiales compartidos: {report.get('materialCount', 0)}. Texturas referenciadas: {report.get('textureCount', 0)}. No se han validado 30–60 FPS o cargas de 50–300 unidades en móvil/tablet físico para estos candidatos.

## Referencias conceptuales

Los archivos de `references/` son copias íntegras de las láminas 1 y 3 aportadas por el usuario. No se generan imágenes, se retocan píxeles ni se incorporan las láminas como sprites de los personajes. Son referencias, no capturas de gameplay.

SHA-256 de las copias verificadas byte a byte:

{hashes}

## Regenerar

Desde la raíz del proyecto, cuando existan las seis capturas y `asset-validation.json`:

```powershell
python tools/build-art-review-gallery.py
```

Opciones: `--output Artifacts/ArtReview` y `--reference-dir C:/Users/jacob/Downloads`. El generador devuelve error y enumera los archivos obligatorios ausentes; no crea un HTML que simule evidencia pendiente. Las capturas del ejecutable se incorporan al volver a ejecutar el generador cuando estén listas. No modifica informes, shaders, modelos ni capturas.

La aprobación de la dirección corresponde al checkpoint humano 2 antes de ampliar el kit.
"""


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, default=REPO / "Artifacts/ArtReview")
    parser.add_argument("--reference-dir", type=Path, default=Path.home() / "Downloads")
    args = parser.parse_args()
    output = args.output.resolve()
    try:
        data = prepare(output, args.reference_dir.resolve())
        payload = json.dumps(data, ensure_ascii=False).replace("<", "\\u003c").replace(">", "\\u003e").replace("&", "\\u0026")
        (output / "index.html").write_text(PAGE.replace("__DATA__", payload), encoding="utf-8")
        (output / "README.md").write_text(readme(data), encoding="utf-8")
        print(json.dumps({"status": "generated", "page": str(output / "index.html"), "images": len(data["images"]), "assetValidationPassed": data["validation"]["passed"], "playerReport": data["player"] is not None, "warnings": data["warnings"]}, ensure_ascii=False))
        return 0
    except (OSError, ValueError, KeyError, TypeError) as error:
        print(json.dumps({"status": "error", "message": str(error)}, ensure_ascii=False), file=sys.stderr)
        return 2


if __name__ == "__main__":
    raise SystemExit(main())
