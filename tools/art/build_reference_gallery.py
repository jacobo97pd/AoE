"""Index actual Blender files and native Unity captures without modifying images."""
from pathlib import Path
import json
import html

ROOT = Path(__file__).resolve().parents[2]
DEST = ROOT / 'Artifacts/ArtReview/reference-characters'
manifest = json.loads((ROOT / 'Assets/Game/ReferenceCharacters/SourceModels/manifest.json').read_text(encoding='utf-8-sig'))
native_file = DEST / 'player/collection-review.json'
native = json.loads(native_file.read_text(encoding='utf-8-sig')) if native_file.exists() else {}
entries = []
for entry in sorted(manifest['entries'], key=lambda value: value['id']):
    item = {key: entry.get(key, '') for key in ('id', 'name', 'family', 'role', 'realm', 'interpretationNotes', 'referenceImage')}
    item['blend'] = f"{entry['id']}/{entry['id']}.blend"
    item['fbx'] = '../../../Assets/Game/ReferenceCharacters/SourceModels/' + entry['modelPath']
    item['triangles'] = entry.get('lodTriangles', [])
    item['bones'] = entry.get('bones', 0)
    item['captures'] = [name for name in ('close', 'medium', 'rts', 'idle', 'run', 'death') if (DEST / 'player' / entry['id'] / (name + '.png')).exists()]
    entries.append(item)
data = json.dumps({'entries': entries, 'native': native}, ensure_ascii=False).replace('<', '\\u003c')
page = '''<!doctype html><html lang="es"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>Personajes · Blender y Unity</title><style>
*{box-sizing:border-box}body{margin:0;background:#111922;color:#e5e9e9;font:16px system-ui}header{padding:28px 4vw;border-bottom:1px solid #35434d}h1{margin:5px 0 12px;font:38px Georgia}p{line-height:1.55;max-width:960px;color:#b5c1c9}a{color:#e7c483}main{display:grid;grid-template-columns:260px minmax(0,1fr);gap:25px;padding:24px 4vw}nav{display:flex;flex-direction:column;gap:7px}button{background:#202e3a;border:1px solid #425666;color:#e2e9eb;border-radius:5px;padding:12px;cursor:pointer;text-align:left;font:inherit}button.active{color:#f6d493;border-color:#c6a259;background:#36404a}figure{margin:0;background:#090e13;border:1px solid #354552}img{display:block;width:100%;max-height:72vh;object-fit:contain}figcaption{padding:12px;color:#9fadb9;font-size:13px}.controls{display:flex;gap:8px;flex-wrap:wrap;margin:15px 0}h2{font:30px Georgia;margin:0 0 10px}.links{display:flex;gap:20px;padding:12px 0}.meta{color:#aac0cf}.status{font-size:14px;color:#d4c6aa}@media(max-width:800px){main{grid-template-columns:1fr}nav{max-height:220px;overflow:auto}h1{font-size:28px}}
</style><header><div class="status">EMBERFIELD · COLECCIÓN DE PERSONAJES</div><h1>Modelos editables y animación</h1><p>Interpretaciones 3D de las referencias, junto a los dos piratas Meshy ya existentes. Las vistas de esta página son capturas del ejecutable de Unity. El parecido facial, los bordados y la calidad de animación todavía no igualan las ilustraciones.</p><div id="status" class="status"></div></header>
<main><nav id="roster" aria-label="Personajes"></nav><article><h2 id="name"></h2><div class="meta" id="meta"></div><div class="links"><a id="blend" download>Abrir Blender (.blend)</a><a id="fbx" download>FBX con animaciones</a><a href="unity-import-report.json">Informe de importación</a></div><figure><img id="image" alt=""><figcaption id="caption"></figcaption></figure><div class="controls" id="views"></div><p id="notes"></p><p>Dentro del juego: <strong>PERSONAJES</strong> en el menú principal. Flechas para cambiar; I reposo, R correr, D caer; 1 cerca, 2 media, 3 RTS. Los modelos asignados a roles existentes aparecen también en las partidas. Los héroes y jinetes a pie restantes están disponibles en la galería y como prefabs; su presencia en la galería no implica que sean unidades reclutables. Los piratas pertenecen a Navales y los hombres de las montañas a Fantasía.</p></article></main>
<script>const data=__DATA__;const $=id=>document.getElementById(id);const labels={close:'Cerca',medium:'Media',rts:'RTS',idle:'Reposo',run:'Carrera',death:'Caída'};
$('status').textContent=data.entries.length+' personajes · '+(data.native.passed?'Verificación nativa completada':'Verificación nativa pendiente')+(data.native.buildGuid?' · Build '+data.native.buildGuid:'');
function select(entry){[...$('roster').children].forEach(b=>b.classList.toggle('active',b.dataset.id===entry.id));$('name').textContent=entry.name;$('meta').textContent=entry.id+' · '+({historical:'Históricas',fantasy:'Fantasía',naval:'Navales'}[entry.realm]||entry.realm)+' · '+entry.family+' · '+entry.role+' · '+entry.bones+' huesos';$('blend').href=entry.blend;$('fbx').href=entry.fbx;$('notes').textContent=entry.interpretationNotes;$('views').replaceChildren();function view(name){$('image').src='player/'+entry.id+'/'+name+'.png';$('image').alt=entry.name+' · '+labels[name];$('caption').textContent='Unity · '+labels[name]+' · captura sin retoque';[...$('views').children].forEach(b=>b.classList.toggle('active',b.dataset.view===name))}for(const name of entry.captures){let b=document.createElement('button');b.dataset.view=name;b.textContent=labels[name];b.onclick=()=>view(name);$('views').append(b)}if(entry.captures.length)view(entry.captures.includes('medium')?'medium':entry.captures[0]);else{$('image').removeAttribute('src');$('caption').textContent='Capturas nativas pendientes.'}}
for(const entry of data.entries){let b=document.createElement('button');b.dataset.id=entry.id;b.textContent=entry.name;b.onclick=()=>select(entry);$('roster').append(b)}if(data.entries.length)select(data.entries[0]);</script></html>'''
(DEST / 'index.html').write_text(page.replace('__DATA__', data), encoding='utf-8')
print(f"Gallery indexed {len(entries)} figures; native verified={native.get('passed', False)}")
