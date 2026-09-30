"""Validate the 26 source deliveries without Unity or Blender.

By default only review reports are written. --write-manifest also regenerates
the Unity source manifest and must be used only before the import freeze.
"""
from pathlib import Path
from datetime import datetime, timezone
import hashlib
import json
import os
import sys

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'Assets/Game/ReferenceCharacters/SourceModels'
REVIEW = ROOT / 'Artifacts/ArtReview/reference-characters'


def atomic_json(path, value):
    temporary = path.with_name(path.name + f'.{os.getpid()}.tmp')
    temporary.write_text(json.dumps(value, ensure_ascii=False, indent=2), encoding='utf-8')
    os.replace(temporary, path)


entries = [json.loads(path.read_text(encoding='utf-8'))
           for path in sorted(OUT.glob('figure_*/model-manifest.json'))]
expected = [f'figure_{index:02d}' for index in range(1, 27)]
results = []
audits = []
for entry in entries:
    identifier = entry['id']
    model = OUT / entry['modelPath']
    blend = ROOT / entry['sourceBlendPath']
    audit_path = REVIEW / identifier / 'source-validation.json'
    audit = json.loads(audit_path.read_text(encoding='utf-8')) if audit_path.exists() else {}
    fresh = bool(audit) and blend.exists() and audit_path.stat().st_mtime_ns >= blend.stat().st_mtime_ns
    missing_maps = [material[key] for material in entry['materials']
                    for key in ('baseMap', 'normalMap', 'metallicGlossMap')
                    if material.get(key) and not (ROOT / material[key]).is_file()]
    lods = entry['lodTriangles']
    model_digest = hashlib.sha256(model.read_bytes()).hexdigest() if model.is_file() else None
    result = {
        'id': identifier,
        'fbxPresent': model.is_file(),
        'fbxSha256': model_digest,
        'digestMatchesManifest': model_digest == entry['fbxSha256'],
        'editableBlendPresent': blend.is_file(),
        'sourceQaPresent': bool(audit),
        'sourceQaFresh': fresh,
        'sourceQaPassed': audit.get('passed', False),
        'missingMaps': missing_maps,
        'lodTriangles': lods,
        'lodBudgetAndOrder': len(lods) == 3 and 0 < lods[2] <= 15000 < lods[1] < lods[0] <= 250000,
        'blenderRenders': [name for name in ('front.png', 'rts.png', 'animation-run.png', 'animation-fall.png')
                           if (REVIEW / identifier / name).is_file()],
    }
    result['passed'] = all(result[key] for key in (
        'fbxPresent', 'digestMatchesManifest', 'editableBlendPresent', 'sourceQaPresent',
        'sourceQaFresh', 'sourceQaPassed', 'lodBudgetAndOrder')) and not missing_maps
    results.append(result)
    if audit:
        audit['fbxSha256'] = model_digest
        audits.append(audit)

complete = [entry['id'] for entry in entries] == expected
passed = complete and all(result['passed'] for result in results)
stamp = datetime.now(timezone.utc).isoformat()
if '--write-manifest' in sys.argv:
    atomic_json(OUT / 'manifest.json', {
        'schemaVersion': 1, 'generatedUtc': stamp,
        'coordinateSystem': 'metres; Blender Z up/front -Y; FBX Unity Y up/front +Z',
        'entries': entries, 'requestedFigures': 26, 'generatedFigures': len(entries),
        'scope': 'Editable interpreted models and reused user-supplied pirates. Not identical reconstructions or a claim of commercial art quality.',
    })
atomic_json(REVIEW / 'source-inventory.json', {
    'generatedUtc': stamp, 'passed': passed, 'complete': complete,
    'requested': 26, 'inspected': len(results), 'entries': results,
})
atomic_json(REVIEW / 'source-validation.json', {
    'generatedUtc': stamp, 'passed': passed, 'requested': 26, 'inspected': len(audits),
    'entries': audits,
    'scope': 'Blender mesh/skin/action evidence with complete FBX/texture inventory. Does not certify visual likeness, physical animation quality, gameplay balance, Unity integration, or mobile GPU performance.',
})
print(json.dumps({'passed': passed, 'complete': complete, 'inspected': len(results),
                  'failed': [result['id'] for result in results if not result['passed']]}, indent=2))
if not passed:
    raise SystemExit('Source inventory is incomplete, stale or failed QA; inspect source-inventory.json.')
