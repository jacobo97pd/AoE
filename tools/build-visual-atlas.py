#!/usr/bin/env python3
"""Build the offline, native-render Emberfield visual atlas using only the stdlib.

Run after the Unity inspection renderer and map capture runner have written their
manifests. No meshes, game rules, images, or player files are modified here.
"""

from __future__ import annotations

import argparse
import json
from pathlib import Path, PurePosixPath
import sys


CATEGORY_NAMES = {
    "units": "Personajes y ejército",
    "buildings": "Edificios",
    "resources": "Recursos",
    "scenery": "Objetos del paisaje",
    "objectives": "Objetivos",
    "effects": "Proyectiles y efectos",
    "cosmetics": "Apariencias",
    "states": "Estados visuales",
    "maps": "Mapas",
}


def read_document(path: Path) -> dict:
    with path.open(encoding="utf-8-sig") as stream:
        value = json.load(stream)
    if not isinstance(value, dict):
        raise ValueError(f"El manifiesto debe ser un objeto JSON: {path}")
    return value


def relative_image(value: object) -> str:
    """Accept only local paths inside the atlas, including ordinary subfolders."""
    if not isinstance(value, str) or not value.strip():
        raise ValueError("Cada ficha y lámina debe tener una ruta de imagen.")
    path = PurePosixPath(value.replace("\\", "/"))
    if path.is_absolute() or ".." in path.parts or ":" in value:
        raise ValueError(f"La imagen debe estar dentro del atlas: {value}")
    if path.suffix.lower() not in {".png", ".jpg", ".jpeg", ".webp"}:
        raise ValueError(f"Formato de imagen no admitido: {value}")
    return str(path)


def prepare(atlas_dir: Path, strict: bool) -> tuple[dict, list[str]]:
    manifest = read_document(atlas_dir / "manifest.json")
    map_path = atlas_dir / "maps.json"
    maps = read_document(map_path) if map_path.is_file() else {"items": []}
    warnings = []
    if not map_path.is_file():
        warnings.append("Falta maps.json; la galería todavía no incluye los mapas.")
    result = {
        "generatedUtc": manifest.get("generatedUtc", ""),
        "unityVersion": manifest.get("unityVersion", ""),
        "sourceCommit": manifest.get("sourceCommit", ""),
        "categories": CATEGORY_NAMES,
        "items": [],
        "sheets": [],
    }
    keys = set()
    for index, raw in enumerate(list(manifest.get("items", [])) + list(maps.get("items", []))):
        if not isinstance(raw, dict):
            raise ValueError(f"Ficha {index} no es un objeto JSON.")
        entry = dict(raw)
        entry["category"] = entry.get("category", "maps" if index >= len(manifest.get("items", [])) else "scenery")
        entry["key"] = str(entry.get("key") or f"{entry['category']}/{entry.get('id', index)}/{entry.get('faction', '')}/{entry.get('biome', '')}")
        if entry["key"] in keys:
            raise ValueError(f"Clave de ficha duplicada: {entry['key']}")
        keys.add(entry["key"])
        entry["id"] = str(entry.get("id") or entry["key"])
        entry["name"] = str(entry.get("name") or entry["id"])
        entry["canonical"] = entry.get("canonical", True) is not False
        entry["image"] = relative_image(entry.get("image"))
        entry["description"] = str(entry.get("description") or "")
        result["items"].append(entry)
    for raw in manifest.get("sheets", []):
        entry = dict(raw)
        entry["image"] = relative_image(entry.get("image"))
        entry["id"] = str(entry.get("id") or Path(entry["image"]).stem)
        entry["name"] = str(entry.get("name") or entry["id"])
        result["sheets"].append(entry)
    if not result["items"]:
        raise ValueError("El manifiesto no contiene fichas.")
    for image_path in sorted({entry["image"] for entry in result["items"] + result["sheets"]}):
        candidate = atlas_dir / image_path
        if not candidate.is_file() or candidate.stat().st_size == 0:
            warnings.append(f"Imagen ausente o vacía: {image_path}")
    if strict and warnings:
        raise ValueError("\n".join(warnings))
    result["validationWarnings"] = warnings
    return result, warnings


PAGE = r'''<!doctype html>
<html lang="es">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<meta name="color-scheme" content="dark">
<title>Emberfield · Atlas visual de la alfa</title>
<style>
:root{color-scheme:dark;--bg:#0c1217;--panel:#141e25;--surface:#1a2830;--line:#2a3b43;--ink:#eff3ef;--muted:#a8b7ba;--gold:#e3bd78;--teal:#8bd0c9;--radius:18px}
*{box-sizing:border-box}html{scroll-behavior:smooth;scroll-padding-top:200px}body{margin:0;background:var(--bg);color:var(--ink);font-family:system-ui,-apple-system,"Segoe UI",sans-serif;line-height:1.6}a{color:var(--teal);text-decoration:none}a:hover{text-decoration:underline}button,input,select{font:inherit}button,a,input,select{touch-action:manipulation}button{cursor:pointer}button:focus-visible,a:focus-visible,input:focus-visible,select:focus-visible{outline:3px solid var(--gold);outline-offset:4px}button{color:inherit}button:disabled{cursor:default;opacity:.35}.wrap{width:min(1560px,calc(100% - 64px));margin:auto}.topline{display:flex;align-items:center;justify-content:space-between;gap:16px;padding:24px 0;border-bottom:1px solid var(--line);font-size:12px;letter-spacing:.16em;text-transform:uppercase}.wordmark{color:var(--gold);font-weight:800;letter-spacing:.27em}.topline a{letter-spacing:.04em}.hero{padding:56px 0 38px;display:grid;grid-template-columns:1.25fr 1fr;gap:60px;align-items:end}.eyebrow{color:var(--teal);font-size:12px;font-weight:750;letter-spacing:.19em;text-transform:uppercase;margin:0 0 12px}h1{font-family:Georgia,"Times New Roman",serif;font-size:clamp(36px,5vw,70px);font-weight:400;letter-spacing:-.035em;line-height:1.02;margin:0 0 22px}.lead{color:var(--muted);font-size:17px;max-width:670px;margin:0}.hero-note{border-left:2px solid var(--gold);padding:0 0 0 22px;color:var(--muted);font-size:14px}.hero-note strong{color:var(--ink);font-weight:650}.stats{display:flex;flex-wrap:wrap;gap:24px;padding:24px 0 30px}.stat{padding-right:25px;border-right:1px solid var(--line)}.stat:last-child{border:0}.stat b{display:block;font-family:Georgia,serif;font-size:31px;line-height:1.2;color:var(--gold);font-weight:400}.stat span{display:block;color:var(--muted);font-size:12px;margin-top:6px}.section-head{display:flex;align-items:end;justify-content:space-between;gap:20px;margin-bottom:20px}.section-head h2{font-family:Georgia,serif;font-size:28px;font-weight:400;margin:0}.section-head p{color:var(--muted);font-size:13px;margin:0}.sheets{display:grid;grid-template-columns:repeat(6,minmax(0,1fr));gap:12px;padding-bottom:44px}.sheet{background:var(--panel);border:1px solid var(--line);padding:0;overflow:hidden;border-radius:12px;text-align:left;transition:border-color .15s,transform .15s}.sheet:hover{border-color:var(--gold);transform:translateY(-2px)}.sheet img{width:100%;aspect-ratio:1.35;object-fit:contain;background:#111c24;display:block}.sheet span{display:block;padding:12px;font-size:12px;font-weight:650}.filter-wrap{position:sticky;top:0;z-index:5;background:rgba(12,18,23,.96);backdrop-filter:blur(16px);border-top:1px solid var(--line);border-bottom:1px solid var(--line);padding:16px 0}.controls{display:grid;grid-template-columns:minmax(220px,1.5fr) repeat(3,minmax(140px,1fr));gap:12px}.control{display:block;color:var(--muted);font-size:11px;font-weight:650;letter-spacing:.08em;text-transform:uppercase}.control input,.control select{display:block;letter-spacing:normal;text-transform:none;font-size:14px;width:100%;height:45px;border:1px solid var(--line);border-radius:9px;background:var(--panel);color:var(--ink);padding:0 12px;margin-top:5px}.control input::placeholder{color:#809298}.filter-foot{display:flex;align-items:center;justify-content:space-between;gap:12px;margin-top:12px;font-size:13px;color:var(--muted)}.toggle{display:flex;align-items:center;gap:9px;cursor:pointer;color:var(--ink)}.toggle input{width:17px;height:17px;accent-color:var(--gold);cursor:pointer}.quiet-btn{padding:4px 0;border:0;background:transparent;color:var(--teal);font-size:13px}.results{padding:28px 0 70px}.result-line{display:flex;align-items:center;justify-content:space-between;gap:20px;color:var(--muted);font-size:13px;margin:0 0 20px}.result-line strong{color:var(--ink)}.grid{display:grid;grid-template-columns:repeat(4,minmax(0,1fr));gap:19px}.card{border:1px solid var(--line);background:var(--panel);border-radius:var(--radius);overflow:hidden;transition:border-color .18s,transform .18s}.card:hover{border-color:#607b80;transform:translateY(-3px)}.card-picture{display:block;width:100%;border:0;background:#111c24;padding:0;position:relative;overflow:hidden;aspect-ratio:1.15}.card-picture img{width:100%;height:100%;object-fit:contain;display:block}.card-picture .zoom{position:absolute;bottom:11px;right:11px;color:var(--ink);background:#0b141ae0;border:1px solid #80949966;border-radius:6px;padding:2px 7px;font-size:11px;opacity:0;transition:opacity .15s}.card-picture:hover .zoom,.card-picture:focus .zoom{opacity:1}.card.map .card-picture{aspect-ratio:1.4}.card.map{grid-column:span 2}.card-body{padding:17px 18px 19px}.tags{display:flex;gap:5px;flex-wrap:wrap;margin-bottom:10px}.tag{border:1px solid #43564d;color:#c3d3c5;padding:2px 6px;border-radius:4px;font-size:9px;letter-spacing:.06em;text-transform:uppercase}.tag.gold{border-color:#62513a;color:var(--gold)}.tag.blue{border-color:#35565d;color:var(--teal)}.card h3{font-weight:650;font-size:17px;line-height:1.35;margin:0 0 6px}.card .sub{color:var(--muted);font-size:12px;margin:0}.card .desc{color:#afbdc0;font-size:12px;line-height:1.55;margin:11px 0 0}.empty{padding:65px 20px;text-align:center;color:var(--muted);border:1px dashed var(--line);border-radius:var(--radius)}.empty h3{color:var(--ink)}.guide{border-top:1px solid var(--line);padding:38px 0 46px;display:grid;grid-template-columns:repeat(3,1fr);gap:34px}.guide h3{font-size:14px;font-weight:650;color:var(--gold);margin:0 0 12px}.guide p{font-size:13px;color:var(--muted);margin:0}.footer{border-top:1px solid var(--line);padding:25px 0 35px;font-size:11px;color:#879ca0;display:flex;justify-content:space-between;flex-wrap:wrap;gap:10px}.footer code{color:#a9b8b8}.warning{padding:14px 18px;background:#39291a;border:1px solid #725032;border-radius:10px;color:#f2cf97;font-size:13px;margin-bottom:20px}.dialog{border:1px solid #4d6268;padding:0;border-radius:18px;background:var(--bg);color:var(--ink);width:min(1400px,calc(100vw - 36px));max-height:calc(100vh - 36px);overflow:auto;box-shadow:0 30px 100px #000b}.dialog::backdrop{background:#000d;backdrop-filter:blur(8px)}.dialog-head{display:flex;align-items:center;justify-content:space-between;gap:18px;padding:18px 22px;border-bottom:1px solid var(--line)}.dialog-head h2{margin:0;font-size:20px;font-weight:600}.close{width:38px;height:38px;flex-shrink:0;border:1px solid var(--line);border-radius:50%;background:var(--panel);font-size:24px;line-height:1}.dialog-image{width:100%;height:min(68vh,850px);object-fit:contain;display:block;background:#111b23}.dialog-detail{display:grid;grid-template-columns:1fr auto;gap:24px;padding:20px 24px 24px}.dialog-detail p{font-size:14px;color:var(--muted);margin:0 0 10px}.meta{display:flex;gap:18px;flex-wrap:wrap;font-size:12px;color:var(--muted)}.meta strong{color:var(--ink);font-weight:600}.dialog-actions{display:flex;align-items:center;gap:10px;align-self:start}.action{background:var(--surface);border:1px solid var(--line);border-radius:8px;padding:9px 13px;color:var(--ink);font-size:12px;white-space:nowrap}.action:hover{border-color:var(--gold);text-decoration:none}.sr-only{position:absolute;width:1px;height:1px;clip:rect(0,0,0,0);overflow:hidden}.hide{display:none!important}
@media(min-width:1550px){.grid{grid-template-columns:repeat(5,minmax(0,1fr))}}@media(max-width:1100px){.hero{gap:35px}.grid{grid-template-columns:repeat(3,minmax(0,1fr))}.sheets{grid-template-columns:repeat(3,minmax(0,1fr))}.sheet img{aspect-ratio:1.8}.controls{grid-template-columns:repeat(2,minmax(0,1fr))}.guide{gap:22px}}@media(max-width:700px){.wrap{width:calc(100% - 30px)}.topline{font-size:10px;letter-spacing:.06em}.topline a{font-size:10px}.hero{grid-template-columns:1fr;gap:25px;padding:34px 0 20px}.hero-note{font-size:12px}.lead{font-size:15px}.stats{gap:16px}.stat{padding-right:15px}.stat b{font-size:25px}.stat span{font-size:10px}.section-head{display:block}.section-head p{margin-top:6px}.sheets{gap:9px;padding-bottom:28px}.sheet span{font-size:10px;padding:9px}.sheet img{aspect-ratio:1.15}.filter-wrap{padding:10px 0;position:relative}.controls{gap:8px}.control{font-size:10px}.control input,.control select{height:42px;font-size:12px}.filter-foot{align-items:start;font-size:11px}.grid{grid-template-columns:repeat(2,minmax(0,1fr));gap:10px}.card.map{grid-column:span 2}.card-body{padding:12px}.card h3{font-size:14px}.card .sub,.card .desc{font-size:11px}.tags{gap:3px}.tag{font-size:8px;padding:1px 4px}.card-picture{aspect-ratio:1}.result-line{font-size:11px;gap:10px}.guide{grid-template-columns:1fr;gap:25px}.dialog{width:calc(100vw - 16px);max-height:calc(100vh - 16px);border-radius:12px}.dialog-head{padding:14px}.dialog-head h2{font-size:16px}.dialog-image{height:48vh}.dialog-detail{grid-template-columns:1fr;padding:15px;gap:12px}.dialog-actions{flex-wrap:wrap}.dialog-detail p{font-size:12px}.footer{font-size:10px}}
@media(prefers-reduced-motion:reduce){html{scroll-behavior:auto}*{transition:none!important}}
</style>
</head>
<body>
<div class="wrap">
  <header class="topline"><span class="wordmark">Emberfield</span><a href="#catalog">Explorar catálogo ↓</a><span>Alfa · Atlas visual</span></header>
  <section class="hero" aria-labelledby="title">
    <div><p class="eyebrow">Realms &amp; Frontiers</p><h1 id="title">Todo lo que habita<br>este mundo.</h1><p class="lead">Personajes, criaturas, arquitectura y escenarios de la alfa actual. Examina cada modelo, compara las facciones y recorre los tres biomas.</p></div>
    <aside class="hero-note"><strong>Los modelos que existen hoy, renderizados en Unity.</strong><br>Estas imágenes se han preparado como vistas de inspección: modelos aislados y escenarios completos sin niebla. Puedes ampliar cada captura hasta su resolución original.</aside>
  </section>
  <div id="stats" class="stats" aria-label="Contenido del atlas"></div>
  <section id="sheetSection" aria-labelledby="sheets-title"><div class="section-head"><h2 id="sheets-title">Una mirada al conjunto</h2><p>Láminas de contacto · Haz clic para ampliar</p></div><div id="sheets" class="sheets"></div></section>
</div>
<section id="catalog" aria-labelledby="catalog-title">
  <div class="filter-wrap"><div class="wrap">
    <h2 id="catalog-title" class="sr-only">Catálogo visual</h2>
    <div class="controls">
      <label class="control">Buscar<input id="search" type="search" placeholder="Dragón, muralla, Miraj, oasis…" autocomplete="off"></label>
      <label class="control">Categoría<select id="category"><option value="">Todo el catálogo</option></select></label>
      <label class="control">Facción<select id="faction"><option value="">Todas las facciones</option></select></label>
      <label class="control">Universo<select id="realm"><option value="">Todos los universos</option><option value="historical">Histórico</option><option value="fantasy">Fantástico</option><option value="shared">Compartido / entorno</option></select></label>
    </div>
    <div class="filter-foot"><label class="toggle"><input id="variants" type="checkbox"> Mostrar todas las variantes de facción</label><button id="reset" class="quiet-btn" type="button">Restablecer filtros</button></div>
  </div></div>
  <div class="wrap results"><div id="warning" class="warning hide" role="status"></div><p class="result-line"><span id="count" aria-live="polite"></span><span id="view-label">Catálogo general · Una muestra por modelo</span></p><div id="grid" class="grid"></div><div id="empty" class="empty hide"><h3>No hay fichas con estos filtros.</h3><p>Prueba otro nombre o restablece los filtros para ver el catálogo completo.</p></div></div>
</section>
<div class="wrap">
  <aside class="guide" aria-label="Cómo interpretar el atlas">
    <div><h3>Ejércitos y universos</h3><p>Las facciones históricas y fantásticas tienen PvP separado. Dragones, trolls, leones, guardianes y elefantes de guerra son unidades del ejército. Los barcos y las caravanas de camellos de los escenarios son decoración.</p></div>
    <div><h3>Modelos, variantes y apariencias</h3><p>El catálogo general muestra una selección representativa de cada modelo. Activa las variantes para comparar facciones: algunas comparten geometría y cambian sus colores o detalles. Las 12 apariencias son cosméticas; los estilos de arquitectura se ilustran sobre un Keep como ejemplo.</p></div>
    <div><h3>Una vista de inspección</h3><p>Los modelos usan las mallas y materiales actuales del juego con iluminación de revisión. Los mapas se muestran sin niebla para enseñar su distribución y paisaje. El tamaño de las fichas se ajusta para ver cada pieza; no compara sus escalas reales. La alfa utiliza arte 3D estilizado.</p></div>
  </aside>
  <footer class="footer"><span id="provenance"></span><span><a href="README.md">Notas del catálogo</a> · <a href="manifest.json">Inventario de modelos</a> · <a href="maps.json">Inventario de mapas</a></span></footer>
</div>
<dialog id="viewer" class="dialog" aria-labelledby="viewer-title"><div class="dialog-head"><h2 id="viewer-title"></h2><button id="close" class="close" type="button" aria-label="Cerrar imagen">×</button></div><img id="viewer-image" class="dialog-image" alt=""><div class="dialog-detail"><div><p id="viewer-desc"></p><div id="viewer-meta" class="meta"></div></div><div class="dialog-actions"><button id="previous" class="action" type="button" aria-label="Imagen anterior">←</button><button id="next" class="action" type="button" aria-label="Imagen siguiente">→</button><a id="original" class="action" target="_blank" rel="noopener">Abrir original ↗</a></div></div></dialog>
<script id="atlas-data" type="application/json">__DATA__</script>
<script>
'use strict';
const data=JSON.parse(document.getElementById('atlas-data').textContent);
const $=id=>document.getElementById(id);
const norm=value=>String(value??'').normalize('NFD').replace(/[\u0300-\u036f]/g,'').toLowerCase();
const realmLabel=value=>({historical:'Histórico',fantasy:'Fantástico',shared:'Compartido'})[value]||'';
const biomeLabel=value=>value==='shared'?'':({forest:'Bosque templado',caribbean:'Costa caribeña',desert:'Desierto'})[value]||value||'';
const aliases={tender:'aldeano trabajador recolector',reedguard:'lancero infanteria escudo',stringwarden:'arquero arco',strider:'caballeria jinete caballo',threadkeeper:'logistica apoyo enlace',ashrunner:'caballeria jinete caballo',supply_cart:'carro suministros transporte',dune_elephant:'elefante gigante guerra',frostguard:'guardia norte infanteria',sun_lion:'leon solar criatura',grove_guardian:'guardian arbol bosque criatura',war_troll:'trol troll criatura',ember_drake:'dragon draco criatura',siege_ram:'ariete asedio',siege_ladder:'escalera asedio',siege_tower:'torre asedio',hearth:'centro urbano hogar',shelter:'casa vivienda refugio',storeyard:'almacen depositos',muster_hall:'cuartel',archive:'archivo investigacion',supply_outpost:'puesto suministros',wall:'muralla muro',gate:'puerta rastrillo',watchtower:'torre vigilancia',keep:'castillo fortaleza',beast_lodge:'criadero bestias establo criaturas',siege_workshop:'taller asedio',ship:'barco nave decoracion',harbor:'puerto muelle decoracion',caravan:'camello caravana decoracion',pyramid:'piramide desierto',palm:'palmera costa',date_palm:'palmera datilera',windmill:'molino viento',village:'pueblo aldea',beacon:'baliza objetivo dominio',Food:'comida alimento cultivo',Wood:'madera arbol',Metal:'metal mina',Stone:'piedra roca'};
const names=data.categories;
const unique=(items,key)=>new Set(items.map(item=>item[key]).filter(Boolean)).size;
const categoryItems=category=>data.items.filter(item=>item.category===category);
const unitNames=new Set(categoryItems('units').map(item=>item.id));
const buildingNames=new Set(categoryItems('buildings').map(item=>item.id));
const factionEntries=new Map();
for(const item of data.items){if(item.faction&&item.faction!=='shared'&&item.faction!=='neutral')factionEntries.set(item.faction,item.factionName||factionEntries.get(item.faction)||item.faction);}
for(const item of data.items){if(item.faction&&!item.factionName)item.factionName=factionEntries.get(item.faction)||item.faction;}
const categoryKeys=Object.keys(names).filter(key=>data.items.some(item=>item.category===key));
for(const key of categoryKeys){const option=document.createElement('option');option.value=key;option.textContent=names[key];$('category').append(option);}
for(const [key,name]of factionEntries){const option=document.createElement('option');option.value=key;option.textContent=name;$('faction').append(option);}
function node(tag,className,text){const value=document.createElement(tag);if(className)value.className=className;if(text!==undefined)value.textContent=text;return value;}
for(const [value,label]of [[unitNames.size,'tipos de unidad'],[buildingNames.size,'tipos de edificio'],[factionEntries.size,'facciones'],[categoryItems('cosmetics').length,'apariencias'],[unique(categoryItems('maps'),'biome'),'biomas']]){
  const stat=node('div','stat');stat.append(node('b','',value),node('span','',label));$('stats').append(stat);
}
let visible=[],dialogItems=[],dialogIndex=0,lastFocus=null;
function imageURL(path){return path.split('/').map(encodeURIComponent).join('/');}
function tag(parent,text,className=''){if(text)parent.append(node('span','tag '+className,text));}
function card(entry){
  const article=node('article','card'+(entry.category==='maps'?' map':''));
  const button=node('button','card-picture');button.type='button';button.setAttribute('aria-label','Ampliar '+entry.name);
  const img=node('img');img.src=imageURL(entry.image);img.alt=entry.name+(entry.factionName?' · '+entry.factionName:'');img.loading='lazy';img.decoding='async';
  button.append(img,node('span','zoom','Ampliar ↗'));button.addEventListener('click',()=>openViewer(visible,visible.indexOf(entry)));article.append(button);
  const body=node('div','card-body'),tags=node('div','tags');tag(tags,names[entry.category]||entry.category);
  tag(tags,realmLabel(entry.realm),entry.realm==='fantasy'?'gold':'blue');if(entry.biome)tag(tags,biomeLabel(entry.biome));
  body.append(tags,node('h3','',entry.name));
  if(entry.factionName||entry.faction)body.append(node('p','sub',entry.factionName||entry.faction));
  else if(entry.category==='maps')body.append(node('p','sub','Escenario de inspección · Sin niebla'));
  if(entry.description)body.append(node('p','desc',entry.description));
  article.append(body);return article;
}
function render(){
  const terms=norm($('search').value).trim().split(/\s+/).filter(Boolean),category=$('category').value,faction=$('faction').value,realm=$('realm').value,variants=$('variants').checked;
  visible=data.items.filter(item=>{
    if(!variants&&!item.canonical)return false;
    if(category&&item.category!==category)return false;
    if(faction&&item.faction!==faction)return false;
    if(realm&&(realm==='shared'?(item.realm&&item.realm!=='shared'):(item.realm&&item.realm!=='shared'&&item.realm!==realm)))return false;
    const hay=norm([item.name,item.id,aliases[item.id],item.description,item.faction,item.factionName,item.biome,biomeLabel(item.biome),item.realm,realmLabel(item.realm),names[item.category]].join(' '));
    return terms.every(term=>hay.includes(term));
  });
  const fragment=document.createDocumentFragment();for(const item of visible)fragment.append(card(item));$('grid').replaceChildren(fragment);
  $('empty').classList.toggle('hide',visible.length>0);$('count').replaceChildren(node('strong','',visible.length+' fichas'),document.createTextNode(' visibles · '+data.items.length+' capturas en el inventario'));
  $('view-label').textContent=variants?'Todas las variantes · Algunas comparten malla':'Catálogo general · Una muestra por modelo';
}
for(const id of ['search','category','variants'])$(id).addEventListener(id==='search'?'input':'change',render);
$('realm').addEventListener('change',()=>{if($('realm').value&&$('realm').value!=='shared')$('variants').checked=true;render();});
$('faction').addEventListener('change',()=>{if($('faction').value)$('variants').checked=true;render();});
$('reset').addEventListener('click',()=>{for(const id of ['search','category','faction','realm'])$(id).value='';$('variants').checked=false;render();});
function openViewer(items,index){dialogItems=items;dialogIndex=index;lastFocus=document.activeElement;updateViewer();$('viewer').showModal();}
function addMeta(label,value){if(value===undefined||value===null||value==='')return;const span=node('span');span.append(document.createTextNode(label+' '),node('strong','',value));$('viewer-meta').append(span);}
function costText(cost){if(typeof cost==='string')return cost;if(!cost||typeof cost!=='object')return '';return Object.entries(cost).filter(([,value])=>Number(value)>0).map(([key,value])=>value+' '+(({food:'comida',wood:'madera',metal:'metal',stone:'piedra'})[key.toLowerCase()]||key)).join(' · ');}
function updateViewer(){
  const entry=dialogItems[dialogIndex];$('viewer-title').textContent=entry.name;$('viewer-image').src=imageURL(entry.image);$('viewer-image').alt=entry.name;
  $('viewer-desc').textContent=entry.description||(entry.category==='maps'?'Vista del escenario completo sin niebla, preparada para inspección visual.':'Modelo actual del juego, capturado en Unity para este atlas.');
  $('viewer-meta').replaceChildren();addMeta('Facción:',entry.factionName||entry.faction);addMeta('Universo:',realmLabel(entry.realm));addMeta('Bioma:',biomeLabel(entry.biome));
  if(Number(entry.baseHealth)>0)addMeta('Vida base:',entry.baseHealth);if(Number(entry.population)>0)addMeta('Población:',entry.population);addMeta('Coste base:',costText(entry.cost));addMeta('Era:',entry.era);addMeta('Referencia:',entry.id);
  $('original').href=imageURL(entry.image);$('previous').disabled=dialogIndex===0;$('next').disabled=dialogIndex===dialogItems.length-1;
}
function step(delta){const next=dialogIndex+delta;if(next>=0&&next<dialogItems.length){dialogIndex=next;updateViewer();}}
$('close').addEventListener('click',()=>$('viewer').close());$('viewer').addEventListener('close',()=>{if(lastFocus)lastFocus.focus();});$('previous').addEventListener('click',()=>step(-1));$('next').addEventListener('click',()=>step(1));
$('viewer').addEventListener('click',event=>{if(event.target===$('viewer')){const box=$('viewer').getBoundingClientRect();if(event.clientX<box.left||event.clientX>box.right||event.clientY<box.top||event.clientY>box.bottom)$('viewer').close();}});
document.addEventListener('keydown',event=>{if(!$('viewer').open)return;if(event.key==='ArrowLeft'){event.preventDefault();step(-1);}if(event.key==='ArrowRight'){event.preventDefault();step(1);}});
for(const [index,sheet]of data.sheets.entries()){const button=node('button','sheet');button.type='button';const img=node('img');img.src=imageURL(sheet.image);img.alt=sheet.name;img.loading='lazy';img.decoding='async';button.append(img,node('span','',sheet.name+' ↗'));button.addEventListener('click',()=>openViewer(data.sheets,index));$('sheets').append(button);}
$('sheetSection').classList.toggle('hide',!data.sheets.length);
if(data.validationWarnings.length){$('warning').classList.remove('hide');$('warning').textContent='Catálogo en generación: '+data.validationWarnings.length+' archivos pendientes de validación.';}
const date=data.generatedUtc?new Date(data.generatedUtc):null,dateLabel=date&&!Number.isNaN(date.valueOf())?date.toLocaleDateString('es-ES',{year:'numeric',month:'long',day:'numeric'}):'';
$('provenance').textContent=['Unity '+(data.unityVersion||'· versión del proyecto'),dateLabel,data.sourceCommit?'Fuente '+data.sourceCommit.substring(0,12):'Fuente: espacio de trabajo actual'].filter(Boolean).join(' · ');
render();
</script>
</body>
</html>
'''


def readme(data: dict) -> str:
    counts = [(name, sum(item["category"] == key for item in data["items"])) for key, name in CATEGORY_NAMES.items()]
    lines = [
        "# Emberfield — Atlas visual de la alfa", "",
        "Abre [index.html](index.html) en tu navegador. Funciona sin conexión: las fichas se incluyen dentro del HTML y las imágenes se cargan desde esta carpeta.", "",
        "Busca por nombre, cambia de categoría, facción o universo, y pulsa cualquier imagen para ampliarla. Dentro del visor puedes usar las flechas del teclado y Escape. El catálogo general muestra las entradas marcadas como representativas; activa las variantes para comparar todas las facciones.", "",
        "## Qué muestran las imágenes", "",
        "Son capturas de inspección renderizadas por Unity a partir de las mallas, materiales y escenarios actuales. Los modelos se presentan aislados y las vistas de mapas no tienen niebla. No son ilustraciones generadas ni capturas de una partida normal en curso. Cada ficha se encuadra por separado, por lo que su tamaño en pantalla no compara la escala real entre unidades.", "",
        "Las facciones históricas y fantásticas tienen PvP separado. Dragones, trolls, leones, guardianes y elefantes de guerra son ejército. Los barcos, muelles y caravanas de camellos del paisaje son decoración. Las fichas de variantes pueden compartir malla; el número de imágenes no equivale al número de esculturas únicas.", "",
        "Las 12 apariencias son cosméticas y no añaden ventajas de combate. Los tres estilos arquitectónicos se muestran sobre un Keep como ejemplo; esta galería no enumera todas las combinaciones de estilos y edificios. Las cifras de vida, población y coste, cuando aparecen, proceden de las definiciones base y no incluyen mejoras temporales o bonificaciones de facción.", "",
        "El arte actual sigue siendo provisional. Las vistas completas exponen adornos fuera del suelo en los bordes y agua demasiado gris en costa y oasis; son detalles existentes de la alfa que requieren pulido.", "",
        "## Contenido", "", "| Categoría | Fichas |", "| --- | ---: |",
    ]
    lines.extend(f"| {name} | {count} |" for name, count in counts if count)
    lines.extend([
        "", f"Total: **{len(data['items'])} fichas** y **{len(data['sheets'])} láminas**.", "",
        "Los tres escenarios jugables se pueden emplear en ambos universos. Si aparecen escenarios de diagnóstico, sus fichas lo indican expresamente.", "",
        "## Procedencia", "",
        f"- Unity: `{data['unityVersion'] or 'consultar el proyecto'}`.",
        f"- Fuente declarada por el renderizador: `{data['sourceCommit'] or 'espacio de trabajo actual'}`.",
        f"- Generación de imágenes: `{data['generatedUtc'] or 'consultar manifest.json'}`.",
        "- Inventario de modelos: [manifest.json](manifest.json).",
        "- Inventario de mapas: [maps.json](maps.json).", "",
        "Regenerar las capturas de Unity, los manifiestos y el visor completo:", "",
        "```powershell", "powershell -NoProfile -ExecutionPolicy Bypass -File tools/Export-VisualAtlas.ps1", "```", "",
        "Reconstruir solo el HTML y sus notas a partir de las imágenes y manifiestos existentes, comprobando que todas las imágenes estén presentes:", "",
        "```powershell", "python tools/build-visual-atlas.py --strict", "```", "",
        "El generador del visor no cambia el ejecutable, las reglas, las mallas ni las imágenes.", "",
    ])
    overview_maps = [item for item in data["items"] if item["category"] == "maps" and item.get("mapId", item["id"]) == item["id"]]
    if overview_maps:
        lines.extend(["## Vistas completas de los mapas", ""])
        lines.extend(f"- [{item['name']}]({item['image'].replace(' ', '%20')}) — inspección del escenario sin niebla." for item in overview_maps)
        lines.append("")
    if data["validationWarnings"]:
        lines.extend(["## Archivos pendientes", ""] + [f"- {entry}" for entry in data["validationWarnings"]] + [""])
    return "\n".join(lines)


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--atlas-dir", type=Path, default=Path(__file__).resolve().parents[1] / "docs" / "art" / "visual-atlas")
    parser.add_argument("--strict", action="store_true", help="Falla si falta maps.json o una imagen referenciada.")
    args = parser.parse_args()
    try:
        data, warnings = prepare(args.atlas_dir, args.strict)
        embedded = json.dumps(data, ensure_ascii=False, separators=(",", ":")).replace("<", "\\u003c").replace(">", "\\u003e").replace("&", "\\u0026")
        (args.atlas_dir / "index.html").write_text(PAGE.replace("__DATA__", embedded), encoding="utf-8")
        (args.atlas_dir / "README.md").write_text(readme(data), encoding="utf-8")
        print(json.dumps({"index": str(args.atlas_dir / "index.html"), "items": len(data["items"]), "sheets": len(data["sheets"]), "images": len({item["image"] for item in data["items"] + data["sheets"]}), "warnings": warnings}, ensure_ascii=False))
        return 0
    except (OSError, ValueError, TypeError, KeyError) as error:
        print(f"No se pudo generar el atlas: {error}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
