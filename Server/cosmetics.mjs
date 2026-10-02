import { readFileSync, existsSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { factionInRealm, isRealm } from './content-realms.mjs';

const fields = new Set(['id', 'displayName', 'description', 'slot', 'targetId', 'realmId', 'styleId', 'priceMinor', 'currency', 'factionId', 'modelId']);
const token = value => typeof value === 'string' && /^[a-z0-9_-]{1,80}$/.test(value);
// A character skin's styleId only names the accent the store paints beside it; the model swap is what the item is.
const styles = new Set(['bronze_tide', 'imperial_sand', 'sapphire_frost', 'ember_crown', 'storm_scale', 'void_scale', 'luminous_ward', 'verdant_bloom',
  'forge_copper', 'forest_leaf', 'wanderer_grey']);
const creatures = new Set(['ember_drake', 'dune_elephant', 'war_troll', 'grove_guardian', 'sun_lion', 'frostguard']);
const buildings = new Set(['hearth', 'shelter', 'muster_hall', 'storeyard', 'archive', 'supply_outpost', 'wall', 'gate', 'watchtower', 'keep', 'beast_lodge', 'siege_workshop']);
// Soldiers, riders and workers: the units whose Meshy model one faction can dress in a character skin.
const characters = new Set(['tender', 'reedguard', 'stringwarden', 'strider', 'threadkeeper', 'ashrunner', 'frostguard', 'quilted_lancer', 'camel_archer']);
// What an equipped item occupies. Two skins for the same unit of different factions (the dwarf's and the ranger's
// reedguard) are both worn at once, so a character skin is keyed by its faction as well as its unit.
export const equipmentTarget = item => item.slot === 'character' ? `${item.factionId}:${item.targetId}` : item.targetId;
const validSlotRules = item => {
  if (item.slot === 'character')
    return characters.has(item.targetId) && isRealm(item.realmId) && factionInRealm(item.factionId, item.realmId) && token(item.modelId);
  if (item.factionId !== undefined || item.modelId !== undefined) return false;
  return item.slot === 'creature' ? creatures.has(item.targetId) :
    item.slot === 'architecture' ? item.targetId === '*' || buildings.has(item.targetId) : item.slot === 'banner' && item.targetId === '*';
};
export function validateCatalog(value) {
  if (!value || !Array.isArray(value.items) || value.items.length > 256) throw new Error('Invalid cosmetic catalog');
  const ids = new Set();
  return Object.freeze(value.items.map(item => {
    if (!item || Object.keys(item).some(key => !fields.has(key)) || !token(item.id) || ids.has(item.id) ||
      !styles.has(item.styleId) || !['creature', 'architecture', 'banner', 'character'].includes(item.slot) ||
      !(item.targetId === '*' || token(item.targetId)) || !(item.realmId === 'shared' || isRealm(item.realmId)) ||
      !validSlotRules(item) ||
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
