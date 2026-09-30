import { readFileSync, existsSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { isRealm } from './content-realms.mjs';

const fields = new Set(['id', 'displayName', 'description', 'slot', 'targetId', 'realmId', 'styleId', 'priceMinor', 'currency']);
const token = value => typeof value === 'string' && /^[a-z0-9_-]{1,80}$/.test(value);
const styles = new Set(['bronze_tide', 'imperial_sand', 'sapphire_frost', 'ember_crown', 'storm_scale', 'void_scale', 'luminous_ward', 'verdant_bloom']);
const creatures = new Set(['ember_drake', 'dune_elephant', 'war_troll', 'grove_guardian', 'sun_lion', 'frostguard']);
const buildings = new Set(['hearth', 'shelter', 'muster_hall', 'storeyard', 'archive', 'supply_outpost', 'wall', 'gate', 'watchtower', 'keep', 'beast_lodge', 'siege_workshop']);
export function validateCatalog(value) {
  if (!value || !Array.isArray(value.items) || value.items.length > 256) throw new Error('Invalid cosmetic catalog');
  const ids = new Set();
  return Object.freeze(value.items.map(item => {
    if (!item || Object.keys(item).some(key => !fields.has(key)) || !token(item.id) || ids.has(item.id) ||
      !styles.has(item.styleId) || !['creature', 'architecture', 'banner'].includes(item.slot) ||
      !(item.targetId === '*' || token(item.targetId)) || !(item.realmId === 'shared' || isRealm(item.realmId)) ||
      item.slot === 'creature' && !creatures.has(item.targetId) || item.slot === 'architecture' && item.targetId !== '*' && !buildings.has(item.targetId) ||
      item.slot === 'banner' && item.targetId !== '*' ||
      typeof item.displayName !== 'string' || item.displayName.length < 1 || item.displayName.length > 100 ||
      typeof item.description !== 'string' || item.description.length > 600 ||
      !Number.isSafeInteger(item.priceMinor) || item.priceMinor < 0 || item.priceMinor > 100000 ||
      typeof item.currency !== 'string' || !/^[A-Z]{3}$/.test(item.currency)) throw new Error('Invalid cosmetic item');
    ids.add(item.id); return Object.freeze({ ...item });
  }));
}
export function loadCatalog() {
  const path = fileURLToPath(new URL('../Assets/Game/Resources/Cosmetics/catalog.json', import.meta.url));
  return validateCatalog(existsSync(path) ? JSON.parse(readFileSync(path, 'utf8')) : { items: [] });
}
