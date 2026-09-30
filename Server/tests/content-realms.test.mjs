import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { resolve, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';
import { REALMS, MAPS, mapInRealm, defaultMapForRealm } from '../content-realms.mjs';

const repository = resolve(dirname(fileURLToPath(import.meta.url)), '../..');

test('the Lands of Legend are the fantasy realm\'s own default map and no other realm plays them', () => {
  assert.equal(MAPS.legend_lands, 'highland');
  for (const realm of REALMS) assert.equal(mapInRealm('legend_lands', realm), realm === 'fantasy', realm);
  assert.deepEqual(Object.fromEntries(REALMS.map(realm => [realm, defaultMapForRealm(realm)])),
    { historical: 'amber_crossing', fantasy: 'legend_lands', naval: 'sapphire_coast' });
  for (const realm of REALMS) assert.ok(mapInRealm(defaultMapForRealm(realm), realm), realm);
});

test('every listed map ships with the biome the server reports for it', () => {
  for (const [mapId, biome] of Object.entries(MAPS)) {
    const map = JSON.parse(readFileSync(resolve(repository, 'Assets/Game/Resources/Maps', mapId + '.json'), 'utf8'));
    assert.equal(map.Id, mapId); assert.equal(map.BiomeId, biome, mapId);
  }
});
