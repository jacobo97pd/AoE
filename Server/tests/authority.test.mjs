import test from 'node:test';
import assert from 'node:assert/strict';
import { existsSync } from 'node:fs';
import { resolve, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';
import { setTimeout as delay } from 'node:timers/promises';
import { AuthorityBridge } from '../authority-bridge.mjs';
import { AuthorityPool } from '../authority-pool.mjs';
import { EmberfieldService } from '../service.mjs';
import { CONTENT_VERSION, PROTOCOL_VERSION, verifyContent } from '../content-version.mjs';
import { REALM_FACTIONS, REALMS, MAPS, mapInRealm } from '../content-realms.mjs';

const repository = resolve(dirname(fileURLToPath(import.meta.url)), '../..');
const worker = resolve(repository, 'Server/AuthorityWorker/out/Emberfield.Authority.dll');

test('real worker process loss aborts only its shard; another live match continues and a replacement process admits a new match', async t => {
  const authority = new AuthorityPool(process.env.AUTHORITY_COMMAND ?? 'dotnet', [worker], { cwd: repository, workerCount: 2, matchesPerWorker: 1 });
  const service = new EmberfieldService({ authority, maintenance: false }); await service.listen(0); t.after(() => service.close());
  const accounts = [];
  for (let i = 0; i < 6; i++) accounts.push(await service.authenticate({ username: 'PoolHuman' + i, password: 'real-pool-test-password' }, true));
  const start = async offset => {
    const one = service.db.accountById(accounts[offset].profile.accountId), two = service.db.accountById(accounts[offset + 1].profile.accountId);
    const room = service.createRoom(one, { factionId: 'aven', mode: 'Conquest' });
    service.joinRoom(two, { code: room.roomCode, factionId: 'serevin' });
    await service.ready(one.id, { ready: true }); return service.ready(two.id, { ready: true });
  };
  const first = await start(0), second = await start(2);
  const before = await service.snapshot(accounts[2].profile.accountId);
  const failed = authority.assignments.get(first.matchId), survivor = authority.assignments.get(second.matchId);
  assert.notEqual(failed, survivor); failed.bridge.child.kill();
  for (let attempt = 0; attempt < 30 && service.state(accounts[0].profile.accountId).status !== 'aborted'; attempt++) await delay(50);
  assert.equal(service.state(accounts[0].profile.accountId).status, 'aborted');
  assert.equal(service.db.rating(accounts[0].profile.accountId, 'historical:ranked:Conquest').mmr, 1000);
  assert.equal(service.state(accounts[2].profile.accountId).status, 'active');
  await delay(200);
  const after = await service.snapshot(accounts[2].profile.accountId); assert.ok(after.observation.Tick > before.observation.Tick);
  const replacement = await start(4); assert.equal(replacement.status, 'active');
  assert.notEqual(authority.assignments.get(replacement.matchId).bridge.child.pid, failed.bridge.child.pid);
  assert.equal(authority.assignments.get(second.matchId), survivor);
});

async function realFixture(t) {
  assert.ok(existsSync(worker), 'Build tools/Build-Authority.ps1 before running the real authority integration suite.');
  verifyContent(repository);
  const authority = new AuthorityBridge(process.env.AUTHORITY_COMMAND ?? 'dotnet', [worker], { cwd: repository });
  const service = new EmberfieldService({ authority, maintenance: false });
  const address = await service.listen(0); t.after(() => service.close());
  const api = async (path, method = 'GET', body, token) => {
    const response = await fetch(`http://127.0.0.1:${address.port}${path}`, { method,
      headers: { 'X-Emberfield-Protocol': String(PROTOCOL_VERSION), 'X-Emberfield-Content': CONTENT_VERSION,
        ...(body !== undefined ? { 'Content-Type': 'application/json' } : {}), ...(token ? { Authorization: `Bearer ${token}` } : {}) },
      ...(body !== undefined ? { body: JSON.stringify(body) } : {}) });
    return { status: response.status, data: await response.json() };
  };
  const accounts = [];
  for (const username of ['RealAlphaOne', 'RealAlphaTwo']) {
    const result = await api('/v1/register', 'POST', { username, password: 'authority-test-password' });
    assert.equal(result.status, 200); accounts.push(result.data);
  }
  const cursors = [0, 0];
  const snapshot = async i => {
    const result = await api('/v1/matches/current/snapshot', 'GET', undefined, accounts[i].token);
    assert.equal(result.status, 200, JSON.stringify(result.data)); return result.data.observation;
  };
  const command = async (i, fields) => {
    const observation = await snapshot(i); const sequence = ++cursors[i];
    return api('/v1/matches/current/commands', 'POST', { sequence,
      command: { Version: PROTOCOL_VERSION, Sequence: sequence, IssuedTick: observation.Tick, RequestId: `test-${i}-${sequence}`, UnitIds: [], ...fields } }, accounts[i].token);
  };
  return { api, accounts, service, authority, snapshot, command };
}

test('real C# World serves two private humans, filters hidden state, validates commands, and persists its result', async t => {
  const f = await realFixture(t); const [one, two] = f.accounts;
  const room = await f.api('/v1/rooms', 'POST', { factionId: 'aven', mode: 'Conquest' }, one.token);
  await f.api('/v1/rooms/join', 'POST', { code: room.data.roomCode, factionId: 'serevin' }, two.token);
  await f.api('/v1/rooms/ready', 'POST', { ready: true }, one.token);
  const start = await f.api('/v1/rooms/ready', 'POST', { ready: true }, two.token);
  assert.equal(start.data.status, 'active', JSON.stringify(start.data));
  const beforeOne = await f.snapshot(0); const beforeTwo = await f.snapshot(1);
  assert.equal(beforeOne.ServerPlayerId, 1); assert.equal(beforeTwo.ServerPlayerId, 2);
  assert.equal(beforeOne.LocalPlayer.FactionId, 'aven'); assert.equal(beforeTwo.LocalPlayer.FactionId, 'serevin');
  assert.equal(beforeOne.Units.length, 4); assert.equal(beforeTwo.Units.length, 4);
  assert.ok(beforeOne.Units.every(u => u.OwnerId === 1 && u.Id < 10));
  assert.ok(beforeTwo.Units.every(u => u.OwnerId === 1 && u.Id > 10));
  assert.equal(beforeOne.Units.some(u => u.Id === 11), false); assert.equal(beforeOne.Buildings.some(b => b.Id === 101), false);
  assert.equal(beforeOne.Resources.some(r => r.Id === 220), false); assert.equal(beforeOne.OpponentFactionId, 'serevin');
  assert.equal(Object.hasOwn(beforeOne, 'OpponentResources'), false);
  const illegal = await f.command(1, { Kind: 1, UnitIds: [1], X: 20000, Z: 20000, PlayerId: 1 });
  assert.equal(illegal.data.accepted, false, 'Cannot order the other authenticated seat units.');
  const hidden = await f.command(0, { Kind: 9, UnitIds: [1], TargetId: 11 });
  assert.equal(hidden.data.accepted, false, 'Cannot target a unit hidden by fog.');
  const destination = { X: 16500, Z: 25500 };
  const move = await f.command(0, { Kind: 1, UnitIds: [1], ...destination });
  assert.equal(move.data.accepted, true, JSON.stringify(move.data));
  const train = await f.command(0, { Kind: 7, EntityId: 100, DefinitionId: 'tender' });
  assert.equal(train.data.accepted, true, JSON.stringify(train.data));
  const build = await f.command(0, { Kind: 5, UnitIds: [2], DefinitionId: 'storeyard', X: 18000, Z: 27000 });
  assert.equal(build.data.accepted, true, JSON.stringify(build.data));
  const gather = await f.command(0, { Kind: 3, UnitIds: [3], TargetId: 200 });
  assert.equal(gather.data.accepted, true, JSON.stringify(gather.data));
  const research = await f.command(0, { Kind: 10, EntityId: 100, DefinitionId: 'advance_kingdom' });
  assert.equal(research.data.accepted, false, 'Server must enforce costs and occupied production.');
  await delay(700);
  const after = await f.snapshot(0);
  assert.ok(after.Tick > beforeOne.Tick);
  // Closing on the destination, rather than moving in a named direction: which way that is depends on
  // where the map spawns the unit, and the battlefields have been rebaked since this was written.
  const range = unit => Math.hypot(unit.Position.X - destination.X, unit.Position.Z - destination.Z);
  assert.ok(range(after.Units.find(u => u.Id === 1)) < range(beforeOne.Units.find(u => u.Id === 1)),
    'The ordered unit must close on its destination.');
  assert.equal(after.LocalPlayer.Resources.Food, 70); assert.equal(after.LocalPlayer.Resources.Wood, 40);
  assert.equal(after.Buildings.find(b => b.Id === 100).ProductionQueue.length, 1);
  assert.equal(after.Buildings.some(b => b.DefinitionId === 'storeyard'), true);
  assert.equal(after.Units.find(u => u.Id === 3).TargetResourceId, 200);
  const replay = await f.api('/v1/matches/current/commands', 'POST', { sequence: 1, command: { Sequence: 1, Kind: 1 } }, one.token);
  assert.equal(replay.status, 409);
  assert.equal((await f.api('/v1/results', 'POST', { matchId: start.data.matchId, winnerPlayerId: 1 }, two.token)).status, 404);
  await f.api('/v1/surrender', 'POST', {}, two.token);
  await delay(50);
  const state = (await f.api('/v1/state', 'GET', undefined, one.token)).data;
  assert.equal(state.status, 'finished'); assert.equal(state.result.winnerPlayerId, 1); assert.equal(state.result.reason, 'surrender');
  const finalSnapshot = await f.snapshot(1); assert.equal(finalSnapshot.Match.IsFinished, true); assert.equal(finalSnapshot.Match.WinnerId, 2);
  const history = (await f.api('/v1/history', 'GET', undefined, one.token)).data.matches;
  assert.equal(history.length, 1); assert.equal(history[0].outcome, 'win'); assert.equal(history[0].statistics.workersRemaining, 4);
  assert.equal(history[0].statistics.food, 70); assert.equal(history[0].contentVersion, CONTENT_VERSION);
});

test('real authority ranked queue, re-login, and surrender award one server-owned rating result', async t => {
  const f = await realFixture(t); const [one, two] = f.accounts;
  for (const player of f.accounts) await f.api('/v1/queue', 'POST', { queue: 'ranked', mode: 'Dominion', factionId: 'serevin' }, player.token);
  await f.service.maintenance();
  assert.equal((await f.api('/v1/state', 'GET', undefined, one.token)).data.status, 'active');
  const snapshot = await f.snapshot(0); assert.equal(snapshot.Match.Mode, 1); assert.equal(snapshot.Match.Objectives.length, 3);
  assert.equal((await f.command(0, { Kind: 2, UnitIds: [1] })).data.accepted, true);
  await f.api('/v1/logout', 'POST', {}, one.token);
  const login = await f.api('/v1/login', 'POST', { username: one.profile.username, password: 'authority-test-password' });
  one.token = login.data.token;
  const reconnected = (await f.api('/v1/state', 'GET', undefined, one.token)).data;
  assert.equal(reconnected.playerId, 1); assert.equal(reconnected.lastAcceptedSequence, 1);
  assert.equal((await f.snapshot(0)).ServerPlayerId, 1);
  await f.api('/v1/surrender', 'POST', {}, two.token); await delay(50);
  const profile = (await f.api('/v1/profile', 'GET', undefined, one.token)).data.profile;
  assert.equal(profile.ratings[0].queue, 'historical:ranked:Dominion'); assert.equal(profile.ratings[0].rankPoints, 25); assert.equal(profile.ratings[0].wins, 1);
  assert.equal(f.service.db.rating(one.profile.accountId, 'historical:ranked:Dominion').mmr, 1016);
  assert.equal(profile.ratings[0].realmId, 'historical');
  assert.equal(f.service.db.rating(one.profile.accountId, 'fantasy:ranked:Dominion').mmr, 1000);
  const history = (await f.api('/v1/history', 'GET', undefined, one.token)).data.matches;
  assert.equal(history.length, 1); assert.equal(history[0].outcome, 'win'); assert.equal(history[0].rankPointsAfter, 25);
});

test('real paid command request ID dedup survives re-login and consumes the rejected sequence without a second charge', async t => {
  const f = await realFixture(t); const [one, two] = f.accounts;
  const room = await f.api('/v1/rooms', 'POST', { factionId: 'aven', mode: 'Conquest' }, one.token);
  await f.api('/v1/rooms/join', 'POST', { code: room.data.roomCode, factionId: 'serevin' }, two.token);
  await f.api('/v1/rooms/ready', 'POST', { ready: true }, one.token);
  await f.api('/v1/rooms/ready', 'POST', { ready: true }, two.token);
  const train = { Kind: 7, EntityId: 100, DefinitionId: 'tender', RequestId: 'paid-once' };
  assert.equal((await f.command(0, train)).data.accepted, true);
  await f.api('/v1/logout', 'POST', {}, one.token);
  const login = await f.api('/v1/login', 'POST', { username: one.profile.username, password: 'authority-test-password' });
  assert.equal(login.status, 200); one.token = login.data.token;
  const duplicate = await f.command(0, train);
  assert.equal(duplicate.data.accepted, false); assert.equal(duplicate.data.error, 'request_id_repeated');
  assert.equal(duplicate.data.lastAcceptedSequence, 2);
  await delay(120);
  const unchanged = await f.snapshot(0);
  assert.equal(unchanged.LocalPlayer.Resources.Food, 70);
  assert.equal(unchanged.Buildings.find(b => b.Id === 100).ProductionQueue.length, 1);
  const fresh = await f.command(0, { ...train, RequestId: 'paid-twice-intentionally' });
  assert.equal(fresh.data.accepted, true); assert.equal(fresh.data.lastAcceptedSequence, 3);
  await delay(120);
  const paidAgain = await f.snapshot(0);
  assert.equal(paidAgain.LocalPlayer.Resources.Food, 20);
  assert.equal(paidAgain.Buildings.find(b => b.Id === 100).ProductionQueue.length, 2);
});


test('real authority enforces all three active rosters, rejects legacy/planned factions and restricts naval to its coast', async t => {
  const f = await realFixture(t);
  const create = (matchId, realmId, mapId, first, second) => f.authority.request({ op: 'create', matchId, mode: 'Conquest', realmId, mapId,
    players: [{ slot: 1, factionId: first }, { slot: 2, factionId: second }] });
  for (const [realm, map, first, second] of [
    ['fantasy', 'amber_crossing', 'ashen', 'aven'], ['historical', 'amber_crossing', 'english', 'skeld'],
    ['hybrid', 'amber_crossing', 'ashen', 'verdant'], ['fantasy', '../Definitions/greybox', 'ashen', 'verdant'],
    ['fantasy', 'amber_crossing', 'unknown', 'verdant'], ['historical', 'amber_crossing', 'miraj', 'aven'],
    ['fantasy', 'amber_crossing', 'solar', 'ashen'], ['naval', 'sapphire_coast', 'skeleton_fleet', 'pirates'],
    ['naval', 'sapphire_coast', 'english_navy', 'skeleton_fleet'], ['historical', 'sapphire_coast', 'english_navy', 'english'],
    ['naval', 'amber_crossing', 'pirates', 'pirates'], ['naval', 'sunscar_basin', 'pirates', 'pirates'],
    ['historical', 'sapphire_coast', 'pirates', 'english'], ['naval', 'sapphire_coast', 'pirates', 'english']
  ]) assert.equal((await create('invalid-' + first + '-' + realm, realm, map, first, second)).ok, false);
  for (const realmId of REALMS) for (const mapId of Object.keys(MAPS).filter(id => mapInRealm(id, realmId)))
    for (let i = 0; i < REALM_FACTIONS[realmId].length; i++) {
      const factions = REALM_FACTIONS[realmId], first = factions[i], second = factions[(i + 1) % factions.length];
      const matchId = `valid-${realmId}-${mapId}-${first}`;
      assert.equal((await create(matchId, realmId, mapId, first, second)).ok, true, matchId);
      for (const playerId of [1, 2]) {
        const { observation } = await f.authority.request({ op: 'snapshot', matchId, playerId });
        assert.equal(observation.Version, PROTOCOL_VERSION); assert.equal(observation.RealmId, realmId); assert.equal(observation.MapId, mapId);
        assert.equal(observation.BiomeId, MAPS[mapId]);
        assert.equal(observation.LocalPlayer.FactionId, playerId === 1 ? first : second);
        assert.equal(observation.OpponentFactionId, playerId === 1 ? second : first);
        const hearth = observation.Buildings.find(b => b.OwnerId === 1 && b.DefinitionId === 'hearth');
        if (realmId === 'naval') {
          // The pirates start with treasure seekers and may not train tenders; the navies the other way round.
          const pirates = observation.LocalPlayer.FactionId === 'pirates';
          const workers = observation.Units.filter(unit => unit.OwnerId === 1 && unit.CarryCapacity > 0 && unit.GatherAmount > 0);
          assert.ok(workers.length > 0); assert.ok(workers.every(unit => unit.DefinitionId === (pirates ? 'treasure_seeker' : 'tender')));
          const forbidden = await f.authority.request({ op: 'command', matchId, playerId, command: {
            Version: PROTOCOL_VERSION, Sequence: 1, IssuedTick: observation.Tick, RequestId: `forbidden-worker-${playerId}`,
            Kind: 7, EntityId: hearth.Id, DefinitionId: pirates ? 'tender' : 'treasure_seeker' } });
          assert.equal(forbidden.accepted, false, `${matchId}/seat${playerId} must reject a worker outside its roster`);
          const unchanged = (await f.authority.request({ op: 'snapshot', matchId, playerId })).observation;
          assert.deepEqual(unchanged.LocalPlayer.Resources, observation.LocalPlayer.Resources);
          assert.equal(unchanged.LocalPlayer.PopulationReserved, observation.LocalPlayer.PopulationReserved);
          assert.deepEqual(unchanged.Buildings.find(b => b.Id === hearth.Id).ProductionQueue, hearth.ProductionQueue);
        }
        const result = await f.authority.request({ op: 'command', matchId, playerId, command: {
          Version: PROTOCOL_VERSION, Sequence: realmId === 'naval' ? 2 : 1, IssuedTick: observation.Tick, RequestId: `recruit-${playerId}`, Kind: 7,
          EntityId: hearth.Id,
          DefinitionId: observation.LocalPlayer.FactionId === 'pirates' ? 'treasure_seeker' : 'tender' } });
        assert.equal(result.accepted, true, `${matchId}/seat${playerId}: ${result.error}`);
      }
      await f.authority.request({ op: 'close', matchId });
    }
});
