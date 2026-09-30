"""Give the wall-boarding infantry Meshy's ladder climb, as one more take on the rig they already have.

    python tools/art/meshy_climb.py [<unit-id> ...] [--roster tools/art/meshy_units.json] [--max-credits 40] [--dry-run]

With no ids, every roster figure that can board a wall gets it: the reedguard, stringwarden and frostguard models on
Meshy's biped rig (SiegeSystem lets only non-worker, non-cavalry, non-creature infantry and ranged climb). Mounts and
beasts are rigged in Blender and have no Meshy rig task to animate.

One paid step per unit, 3 credits: POST /v1/animations with the unit's rig task and library action 438,
'Ladder_Climb_Loop'. The GLB (and FBX) land in the unit's raw folder as <id>_climb.glb; meshy_unit_finish.py picks
the GLB up as the 'Climb' take, and MeshyUnitBaker gives the controller a looping Climb state. The step is recorded as
'climb' in meshy-manifest.json before the next unit starts, so a rerun skips what is already paid for.

Meshy keeps a rig task for three days after it was made; past that the animation call is refused and the unit has to
be rigged again (5 credits) before it can climb.

The key is read ONLY from MESHY_API_KEY (process first, then the Windows user environment) and is never printed or
written to disk. Signed download URLs are stored without their query string.
"""
import argparse
import json
import os
import sys
from datetime import datetime, timezone
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from meshy_unit import ROOT, api_key, balance, call, download, save, strip, urls, wait  # noqa: E402

CLIMB = ('Climb', 438, 'Ladder_Climb_Loop')
PRICE = 3
# SiegeSystem.BoardWallCommand: the unit ids whose soldiers may climb a ladder or a tower onto an enemy wall.
BOARDERS = {'reedguard', 'stringwarden', 'frostguard'}


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('units', nargs='*', help='Roster ids; default every boarding figure on a Meshy rig.')
    parser.add_argument('--roster', default='tools/art/meshy_units.json')
    parser.add_argument('--max-credits', type=int, default=40, help='Refuse any unit that would take this run past it.')
    parser.add_argument('--dry-run', action='store_true', help='Show the plan without spending anything.')
    args = parser.parse_args()

    roster = json.loads((ROOT / args.roster).read_text(encoding='utf-8'))
    entries = {e['id']: e for e in roster['units']}
    missing = [unit for unit in args.units if unit not in entries]
    if missing:
        raise SystemExit('No unit %s in %s' % (', '.join(missing), args.roster))
    chosen = [entries[unit] for unit in args.units] or \
        [e for e in roster['units'] if e.get('unit') in BOARDERS and not e.get('rig')]

    plan = []
    for entry in chosen:
        manifest_path = ROOT / roster['output'] / entry['id'] / 'meshy-manifest.json'
        if not manifest_path.exists():
            print('%s: no manifest yet; run meshy_unit.py first' % entry['id'])
            continue
        manifest = json.loads(manifest_path.read_text(encoding='utf-8'))
        if 'climb' in manifest['steps']:
            print('%s: already has the climb' % entry['id'])
            continue
        if 'rig' not in manifest['steps']:
            print('%s: no Meshy rig task (a Blender-rigged mount or beast); skipped' % entry['id'])
            continue
        plan.append((entry, manifest_path, manifest))
    print('Climb for %d units, about %d credits (cap %d): %s'
          % (len(plan), PRICE * len(plan), args.max_credits, ', '.join(e['id'] for e, _, _ in plan) or '-'))
    if args.dry_run or not plan:
        return

    key = api_key()
    library = call('GET', '/v1/animations/library', key)
    action = next((a for a in library if isinstance(a, dict) and a.get('action_id') == CLIMB[1]), None)
    # Library ids are Meshy's, not ours; a renumbering would otherwise pay for the wrong motion eleven times over.
    if action is None or action.get('key') != CLIMB[2]:
        raise SystemExit('Library action %d is %r, not %s; not spending.'
                         % (CLIMB[1], action and action.get('key'), CLIMB[2]))

    spent, done, failed = 0, [], []
    for entry, manifest_path, manifest in plan:
        unit = entry['id']
        if spent + PRICE > args.max_credits:
            print('Stopping before %s: it would take this run to %d credits, over the cap of %d.'
                  % (unit, spent + PRICE, args.max_credits))
            break
        raw = Path(os.path.expandvars(os.path.expanduser(roster['raw']))) / unit
        raw.mkdir(parents=True, exist_ok=True)
        try:
            task_id = call('POST', '/v1/animations', key, {'rig_task_id': manifest['steps']['rig']['task']['id'],
                                                          'action_id': CLIMB[1]})['result']
        except RuntimeError as error:
            # An expired rig task or a refused call costs nothing; report it and go on with the others.
            print('%s: refused: %s' % (unit, error), flush=True)
            failed.append(unit)
            continue
        # Counted once Meshy has accepted the task, so a clip that later fails or times out still counts toward the cap.
        spent += PRICE
        print('%s: climb task %s' % (unit, task_id), flush=True)
        try:
            task = wait(key, '/v1/animations/' + task_id, unit + ' climb')
        except RuntimeError as error:
            print('%s: %s' % (unit, error), flush=True)
            failed.append(unit)
            continue
        files = {}
        for where, url in urls(task):
            name = where.split('.')[-1]
            # One action comes back as animation_glb_url / animation_fbx_url; the armature-only and USDZ extras are
            # not wanted, and whatever else a later API adds is left alone rather than guessed at.
            if name in ('animation_glb_url', 'animation_fbx_url'):
                ext = 'glb' if name.endswith('_glb_url') else 'fbx'
                files['climb_' + ext] = download(url, raw / ('%s_climb.%s' % (unit, ext)))
        if 'climb_glb' not in files:
            print('%s: the task finished without a GLB; not recorded' % unit, flush=True)
            failed.append(unit)
            continue
        # Read again: the finish or another batch may have written this manifest while the clip was being made.
        manifest = json.loads(manifest_path.read_text(encoding='utf-8'))
        manifest['files'].update(files)
        manifest['clips'] = [c for c in manifest.get('clips', []) if c.get('state') != CLIMB[0]] + \
            [{'state': CLIMB[0], 'action_id': CLIMB[1], 'step': 'climb'}]
        # List price, not the balance difference: other batches may be spending at the same time.
        manifest['credits'] = (manifest.get('credits') or 0) + PRICE
        manifest['steps']['climb'] = {'task': strip(task), 'credits': PRICE, 'balance_after': balance(key),
                                      'finished_utc': datetime.now(timezone.utc).isoformat()}
        save(manifest, manifest_path)
        done.append(unit)
        print('  %s climb cost %d credits; run total %d' % (unit, PRICE, spent), flush=True)
    print('MESHY_CLIMB_OK spent=%d done=%s failed=%s' % (spent, ','.join(done) or '-', ','.join(failed) or '-'))
    if failed:
        sys.exit(1)


if __name__ == '__main__':
    main()
