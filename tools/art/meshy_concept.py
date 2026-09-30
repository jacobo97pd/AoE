"""Draw a missing unit's reference sheet with Meshy image-to-image, in the style of the sheets it sits beside.

    python tools/art/meshy_concept.py <unit-id> [--model nano-banana-2] [--dry-run]

The roster entry (tools/art/meshy_units.json) carries a "concept" block: the prompt and the units whose crops are the
style references. The picture lands at Assets/Models/Units/<id>/concept.png, and the entry's sheet points there, so
meshy_unit.py turns it into a model exactly as it does a figure cut from the user's own sheets. Nothing is modelled
until someone has looked at the picture. 3 to 12 credits an image, depending on the model.
"""
import argparse
import base64
import json
import sys
from datetime import datetime, timezone
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import meshy_unit as meshy  # noqa: E402

COST = {'nano-banana': 3, 'nano-banana-2': 6, 'nano-banana-pro': 9}
# Every concept is asked for in the form image-to-3D reads best: one figure, whole, square to the camera.
FORM = (' Full body from head to boots, one single character only, standing in a strict T-pose with both arms straight '
        'out to the sides, facing the camera, weapons and tools slung on the back or belt so the hands are empty. '
        'Plain dark charcoal studio background, no text, no other figures.')


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('unit')
    parser.add_argument('--roster', default='tools/art/meshy_units.json')
    parser.add_argument('--model', default='nano-banana-2', choices=sorted(COST))
    parser.add_argument('--dry-run', action='store_true')
    args = parser.parse_args()
    roster = json.loads((meshy.ROOT / args.roster).read_text(encoding='utf-8'))
    entry = next((e for e in roster['units'] if e['id'] == args.unit), None)
    if entry is None or 'concept' not in entry:
        raise SystemExit('No concept block for %r in %s' % (args.unit, args.roster))
    out = meshy.ROOT / roster['output'] / entry['id']
    out.mkdir(parents=True, exist_ok=True)
    references = [meshy.ROOT / roster['output'] / ref / 'reference.png' for ref in entry['concept']['references']]
    missing = [str(r) for r in references if not r.exists()]
    if missing:
        raise SystemExit('Reference crops missing: %s' % missing)
    prompt = entry['concept']['prompt'] + FORM
    print('%s: %d references, %d characters, %s (%d credits)' % (entry['id'], len(references), len(prompt), args.model, COST[args.model]))
    if args.dry_run:
        print(prompt)
        return
    key = meshy.api_key()
    body = {'ai_model': args.model, 'prompt': prompt, 'aspect_ratio': '3:4',
            'reference_image_urls': ['data:image/png;base64,' + base64.b64encode(r.read_bytes()).decode() for r in references]}
    before = meshy.balance(key)
    task_id = meshy.call('POST', '/v1/image-to-image', key, body)['result']
    print('Concept task %s' % task_id, flush=True)
    task = meshy.wait(key, '/v1/image-to-image/' + task_id, 'concept')
    after = meshy.balance(key)
    image = next(url for where, url in meshy.urls(task) if where.startswith('image_urls'))
    record = meshy.download(image, out / 'concept.png')
    # List price, not the balance difference: other batches may be spending at the same time.
    manifest = {'id': entry['id'], 'concept': entry['concept'], 'model': args.model, 'task': meshy.strip(task),
                'credits': COST[args.model], 'balance_after': after, 'file': record,
                'finished_utc': datetime.now(timezone.utc).isoformat()}
    (out / 'concept-manifest.json').write_text(json.dumps(manifest, indent=2, ensure_ascii=False) + '\n', encoding='utf-8')
    print('MESHY_CONCEPT_OK %s credits=%s' % (entry['id'], COST[args.model]))


if __name__ == '__main__':
    main()
