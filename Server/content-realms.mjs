// Only playable factions belong here. Legacy history retains its original IDs;
// planned naval factions have no authoritative gameplay entry yet.
export const REALM_FACTIONS = Object.freeze({
  historical: Object.freeze(['aven', 'serevin', 'english', 'sultanate', 'sahel']),
  fantasy: Object.freeze(['ashen', 'drakeforged', 'skeld', 'verdant']),
  naval: Object.freeze(['pirates']),
});
export const REALMS = Object.freeze(Object.keys(REALM_FACTIONS));
export const FACTION_REALMS = Object.freeze(Object.fromEntries(
  Object.entries(REALM_FACTIONS).flatMap(([realm, factions]) => factions.map(faction => [faction, realm]))));
export const MAPS = Object.freeze({ amber_crossing: 'forest', sapphire_coast: 'caribbean', sunscar_basin: 'desert', legend_lands: 'highland' });
export const isRealm = value => REALMS.includes(value);
export const factionInRealm = (faction, realm) => typeof faction === 'string' && isRealm(realm) && Object.hasOwn(FACTION_REALMS, faction) && FACTION_REALMS[faction] === realm;
// The Lands of Legend dress each start in its culture's land, so only the fantasy realm plays them.
export const mapInRealm = (map, realm) => typeof map === 'string' && isRealm(realm) && Object.hasOwn(MAPS, map) &&
  (realm !== 'naval' || map === 'sapphire_coast') && (map !== 'legend_lands' || realm === 'fantasy');
export const defaultMapForRealm = realm => realm === 'naval' ? 'sapphire_coast' : realm === 'fantasy' ? 'legend_lands' : 'amber_crossing';
export const ratingKey = (realm, queue, mode) => `${realm}:${queue}:${mode}`;
