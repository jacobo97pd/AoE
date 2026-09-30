import test from 'node:test';
import assert from 'node:assert/strict';
import { EventEmitter } from 'node:events';
import { mkdtempSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { EmberfieldService, RateLimiter, clientAddress } from '../service.mjs';
import { AccountDatabase, visibleRank } from '../database.mjs';
import { CONTENT_VERSION, PROTOCOL_VERSION } from '../content-version.mjs';
import { REALM_FACTIONS, REALMS, MAPS, mapInRealm } from '../content-realms.mjs';
import { loadCatalog } from '../cosmetics.mjs';

class FakeAuthority extends EventEmitter {
  constructor() { super(); this.requests = []; this.dead = false; this.matches = new Map(); }
  async request(input) {
    this.requests.push(input);
    if (input.op === 'create') this.matches.set(input.matchId, input);
    if (input.op === 'snapshot') return { ok: true, observation: { viewingPlayerId: input.playerId, privateStock: `stock-for-${input.playerId}` } };
    if (input.op === 'command') return { ok: true, accepted: input.command.Kind !== 999, error: input.command.Kind === 999 ? 'unsupported_kind' : '' };
    if (input.op === 'forfeit') this.emit('result', { event: 'result', matchId: input.matchId, winnerPlayerId: input.playerId === 1 ? 2 : 1, reason: input.reason, tick: 400, durationSeconds: 20, players: [{ playerId: 1, statistics: { food: 12 } }, { playerId: 2, statistics: { food: 24 } }] });
    return { ok: true };
  }
  close() { this.dead = true; }
}
async function fixture(t, options = {}) {
  let now = 1000000;
  const authority = new FakeAuthority();
  const service = new EmberfieldService({ authority, now: () => now, maintenance: false, ...options });
  const address = await service.listen(0);
  t.after(() => service.close());
  const api = async (path, method = 'GET', body, token) => {
    if (path === '/v1/matches/current/commands' && body?.command && !Object.hasOwn(body.command, 'RequestId'))
      body = { ...body, command: { ...body.command, RequestId: `fake-${body.sequence}` } };
    const response = await fetch(`http://127.0.0.1:${address.port}${path}`, { method,
      headers: { 'X-Emberfield-Protocol': String(PROTOCOL_VERSION), 'X-Emberfield-Content': CONTENT_VERSION,
        ...(body !== undefined ? { 'Content-Type': 'application/json' } : {}), ...(token ? { Authorization: `Bearer ${token}` } : {}) },
      ...(body !== undefined ? { body: JSON.stringify(body) } : {}) });
    return { status: response.status, data: await response.json() };
  };
  const register = async name => {
    const result = await api('/v1/register', 'POST', { username: name, password: 'alpha-test-password' });
    assert.equal(result.status, 200); return result.data;
  };
  const accounts = async () => [await register('AlphaOne'), await register('AlphaTwo'), await register('Outsider')];
  const privateMatch = async (one, two) => {
    const room = await api('/v1/rooms', 'POST', { factionId: 'aven', mode: 'Conquest' }, one.token);
    assert.equal(room.status, 200);
    assert.equal((await api('/v1/rooms/join', 'POST', { code: room.data.roomCode, factionId: 'serevin' }, two.token)).status, 200);
    await api('/v1/rooms/ready', 'POST', { ready: true }, one.token);
    const started = await api('/v1/rooms/ready', 'POST', { ready: true }, two.token);
    assert.equal(started.data.status, 'active'); return started.data;
  };
  return { service, authority, api, register, accounts, privateMatch, baseUrl: `http://127.0.0.1:${address.port}`, advance: ms => { now += ms; } };
}

test('public health is discoverable but every versioned route requires matching protocol and content', async t => {
  const f = await fixture(t);
  assert.equal((await f.api('/health')).data.contentVersion, CONTENT_VERSION);
  const missing = await fetch(`${f.baseUrl}/v1/profile`); assert.equal(missing.status, 426);
  const wrong = await fetch(`${f.baseUrl}/v1/profile`, { headers: { 'X-Emberfield-Protocol': String(PROTOCOL_VERSION + 1), 'X-Emberfield-Content': CONTENT_VERSION } });
  assert.equal(wrong.status, 426);
  assert.equal((await f.api('/v1/profile')).status, 401);
});

test('accounts authenticate case-insensitively, hash secrets, revoke sessions, and hide MMR', async t => {
  const f = await fixture(t); const one = await f.register('AlphaOne');
  const duplicate = await f.api('/v1/register', 'POST', { username: 'alphaone', password: 'alpha-test-password' });
  assert.equal(duplicate.status, 409);
  const wrong = await f.api('/v1/login', 'POST', { username: 'AlphaOne', password: 'incorrect-password' });
  assert.equal(wrong.status, 401);
  const login = await f.api('/v1/login', 'POST', { username: 'alphaone', password: 'alpha-test-password' });
  assert.equal(login.status, 200); assert.notEqual(login.data.token, one.token);
  const account = f.service.db.accountById(one.profile.accountId);
  assert.notEqual(account.password_hash, 'alpha-test-password'); assert.equal(account.password_hash.length, 128);
  const sessions = f.service.db.db.prepare('SELECT * FROM sessions').all();
  assert.ok(sessions.every(s => s.token_hash !== one.token && s.token_hash.length === 64));
  assert.ok(!JSON.stringify(login.data.profile).includes('mmr'));
  assert.equal((await f.api('/v1/logout', 'POST', {}, one.token)).status, 200);
  assert.equal((await f.api('/v1/profile', 'GET', undefined, one.token)).status, 401);
  f.advance(13 * 60 * 60 * 1000);
  assert.equal((await f.api('/v1/profile', 'GET', undefined, login.data.token)).status, 401);
});

test('private lobbies need two ready accounts; access and command identity are server bound', async t => {
  const f = await fixture(t); const [one, two, outsider] = await f.accounts();
  const state = await f.privateMatch(one, two);
  assert.equal((await f.api('/v1/matches/current/snapshot', 'GET', undefined, outsider.token)).status, 409);
  const firstSnapshot = await f.api('/v1/matches/current/snapshot', 'GET', undefined, one.token);
  const secondSnapshot = await f.api('/v1/matches/current/snapshot', 'GET', undefined, two.token);
  assert.equal(firstSnapshot.data.observation.privateStock, 'stock-for-1');
  assert.equal(secondSnapshot.data.observation.privateStock, 'stock-for-2');
  const command = { Version: PROTOCOL_VERSION, Sequence: 1, Kind: 1, PlayerId: 2, UnitIds: [7] };
  const accepted = await f.api('/v1/matches/current/commands', 'POST', { sequence: 1, playerId: 2, matchId: 'forged', command }, one.token);
  assert.equal(accepted.data.accepted, true);
  const dispatched = f.authority.requests.find(r => r.op === 'command');
  assert.equal(dispatched.playerId, 1); assert.equal(dispatched.matchId, state.matchId);
  assert.equal((await f.api('/v1/matches/current/commands', 'POST', { sequence: 1, command }, one.token)).status, 409);
  assert.equal((await f.api('/v1/matches/current/commands', 'POST', { sequence: 2, command }, one.token)).status, 400);
  const rejected = await f.api('/v1/matches/current/commands', 'POST', { sequence: 2, command: { Sequence: 2, Kind: 999 } }, one.token);
  assert.equal(rejected.data.accepted, false); assert.equal(rejected.data.lastAcceptedSequence, 2);
  assert.equal((await f.api('/v1/state', 'GET', undefined, one.token)).data.lastAcceptedSequence, 2);
  assert.equal((await f.api('/v1/rooms/leave', 'POST', {}, one.token)).status, 409);
});

test('only authority results create history; private matches never update ranking; duplicate terminal is idempotent', async t => {
  const f = await fixture(t); const [one, two] = await f.accounts(); const state = await f.privateMatch(one, two);
  assert.equal((await f.api('/v1/results', 'POST', { matchId: state.matchId, winnerPlayerId: 1 }, one.token)).status, 404);
  assert.equal((await f.api('/v1/matches/current/result', 'POST', { winnerPlayerId: 1 }, one.token)).status, 404);
  const result = { matchId: state.matchId, winnerPlayerId: 2, reason: 'Conquest', tick: 400, durationSeconds: 20,
    players: [{ playerId: 1, statistics: { food: 32, secret: 'drop-me', wood: -1 } }] };
  assert.equal(f.service.acceptResult(result), true); assert.equal(f.service.acceptResult(result), false);
  const history = await f.api('/v1/history', 'GET', undefined, one.token);
  assert.equal(history.data.matches.length, 1); assert.equal(history.data.matches[0].outcome, 'loss');
  assert.deepEqual(history.data.matches[0].statistics, { food: 32 });
  assert.deepEqual((await f.api('/v1/profile', 'GET', undefined, one.token)).data.profile.ratings, []);
  assert.equal((await f.api('/v1/rooms/leave', 'POST', {}, one.token)).data.status, 'idle');
});

test('ranked matchmaking updates hidden Elo and separate visible points exactly once', async t => {
  const f = await fixture(t); const [one, two] = await f.accounts();
  for (const player of [one, two]) await f.api('/v1/queue', 'POST', { queue: 'ranked', mode: 'Conquest', factionId: 'aven' }, player.token);
  await f.service.maintenance();
  const state = (await f.api('/v1/state', 'GET', undefined, one.token)).data;
  assert.equal(state.status, 'active'); assert.equal(state.queue, 'ranked');
  await f.api('/v1/surrender', 'POST', {}, two.token);
  const profile = (await f.api('/v1/profile', 'GET', undefined, one.token)).data.profile;
  assert.equal(profile.ratings[0].rankPoints, 25); assert.equal(profile.ratings[0].wins, 1); assert.equal(profile.ratings[0].visibleRank, 'Bronze');
  assert.ok(!JSON.stringify(profile).includes('mmr'));
  assert.equal(f.service.db.rating(one.profile.accountId, 'historical:ranked:Conquest').mmr, 1016);
  assert.equal(f.service.db.rating(two.profile.accountId, 'historical:ranked:Conquest').mmr, 984);
  assert.equal(f.service.db.rating(one.profile.accountId, 'historical:casual:Conquest').mmr, 1000);
  assert.equal(f.service.acceptResult({ matchId: state.matchId, winnerPlayerId: 1, reason: 'Conquest', tick: 400, durationSeconds: 20 }), false);
  assert.equal(f.service.db.rating(one.profile.accountId, 'historical:ranked:Conquest').wins, 1);
  assert.equal(visibleRank(100), 'Silver'); assert.equal(visibleRank(1400), 'Legend');
});

test('queue partitions by mode and type; widening window finds older compatible opponents', async t => {
  const f = await fixture(t); const [one, two, three] = await f.accounts();
  f.service.db.db.prepare('INSERT INTO ratings VALUES(?,?,?,?,?,?,?)').run(two.profile.accountId, 'historical:ranked:Conquest', 1350, 0, 0, 0, 0);
  await f.api('/v1/queue', 'POST', { queue: 'ranked', mode: 'Conquest', factionId: 'aven' }, one.token);
  await f.api('/v1/queue', 'POST', { queue: 'ranked', mode: 'Conquest', factionId: 'serevin' }, two.token);
  await f.api('/v1/queue', 'POST', { queue: 'casual', mode: 'Conquest', factionId: 'aven' }, three.token);
  await f.service.maintenance(); assert.equal(f.service.state(one.profile.accountId).status, 'queued');
  f.advance(50000); await f.service.maintenance();
  assert.equal(f.service.state(one.profile.accountId).status, 'active'); assert.equal(f.service.state(three.profile.accountId).status, 'queued');
  assert.equal((await f.api('/v1/queue', 'DELETE', undefined, three.token)).data.status, 'idle');
});

test('re-login restores seat and cursor; disconnect grace forfeits only missing participant', async t => {
  const f = await fixture(t); const [one, two] = await f.accounts(); await f.privateMatch(one, two);
  await f.api('/v1/matches/current/commands', 'POST', { sequence: 1, command: { Sequence: 1, Kind: 1 } }, one.token);
  await f.api('/v1/logout', 'POST', {}, one.token); f.advance(30000);
  const login = await f.api('/v1/login', 'POST', { username: 'AlphaOne', password: 'alpha-test-password' });
  const state = (await f.api('/v1/state', 'GET', undefined, login.data.token)).data;
  assert.equal(state.playerId, 1); assert.equal(state.lastAcceptedSequence, 1); assert.equal(state.status, 'active');
  f.advance(76000); f.service.heartbeat(two.profile.accountId); await f.service.maintenance();
  assert.equal(f.service.state(one.profile.accountId).result.reason, 'disconnect');
  assert.equal(f.service.state(one.profile.accountId).result.winnerPlayerId, 2);
});

test('AFK requires accepted activity; both absent aborts without rating award', async t => {
  const f = await fixture(t); const [one, two] = await f.accounts(); await f.privateMatch(one, two);
  f.advance(170000); f.service.heartbeat(one.profile.accountId); f.service.heartbeat(two.profile.accountId);
  await f.api('/v1/matches/current/commands', 'POST', { sequence: 1, command: { Sequence: 1, Kind: 1 } }, two.token);
  f.advance(11000); f.service.heartbeat(one.profile.accountId); f.service.heartbeat(two.profile.accountId); await f.service.maintenance();
  assert.equal(f.service.state(one.profile.accountId).result.reason, 'afk');
  await f.api('/v1/rooms/leave', 'POST', {}, one.token); await f.api('/v1/rooms/leave', 'POST', {}, two.token);
  for (const player of [one, two]) await f.api('/v1/queue', 'POST', { queue: 'ranked', mode: 'Conquest', factionId: 'aven' }, player.token);
  await f.service.maintenance(); f.advance(76000); await f.service.maintenance();
  assert.equal(f.service.state(one.profile.accountId).status, 'aborted');
  assert.equal(f.service.state(one.profile.accountId).result.reason, 'both_disconnected');
  assert.equal(f.service.db.rating(one.profile.accountId, 'historical:ranked:Conquest').wins, 0);
});

test('authority failure aborts matches without ratings; capacity and malformed requests are bounded', async t => {
  const f = await fixture(t, { maxRooms: 1 }); const [one, two, three] = await f.accounts(); await f.privateMatch(one, two);
  assert.equal((await f.api('/v1/rooms', 'POST', { mode: 'Conquest', factionId: 'aven' }, three.token)).status, 503);
  assert.equal((await f.api('/v1/rooms/join', 'POST', { code: 'invalid', factionId: 'aven' }, three.token)).status, 400);
  assert.equal((await f.api('/v1/rooms', 'POST', { mode: 'bad', factionId: 'aven' }, three.token)).status, 400);
  assert.equal((await f.api('/v1/heartbeat', 'POST', { padding: 'x'.repeat(66000) }, one.token)).status, 413);
  f.authority.dead = true; f.authority.emit('unavailable', 'worker_crashed');
  assert.equal(f.service.state(one.profile.accountId).status, 'aborted');
  assert.equal(f.service.db.history(one.profile.accountId)[0].reason, 'authority_failure');
  assert.equal((await f.api('/health')).data.status, 'unavailable');
});

test('authentication and command rate limits reject floods', async t => {
  const f = await fixture(t);
  for (let i = 0; i < 12; i++) assert.equal((await f.api('/v1/login', 'POST', { username: '?', password: 'short' })).status, 400);
  assert.equal((await f.api('/v1/login', 'POST', { username: '?', password: 'short' })).status, 429);
  let now = 0; const rate = new RateLimiter(() => now);
  assert.equal(rate.allow('command', 2, 1000), true); assert.equal(rate.allow('command', 2, 1000), true); assert.equal(rate.allow('command', 2, 1000), false);
  now = 1000; assert.equal(rate.allow('command', 2, 1000), true);
});

test('authenticated players sharing one IP each retain their gameplay budget; anonymous floods stay bounded', async t => {
  const f = await fixture(t); const one = await f.register('SharedIpOne'), two = await f.register('SharedIpTwo');
  for (let i = 0; i < 650; i++) {
    assert.equal((await f.api('/v1/state', 'GET', undefined, one.token)).status, 200);
    assert.equal((await f.api('/v1/state', 'GET', undefined, two.token)).status, 200);
  }
  for (let i = 650; i < 900; i++) assert.equal((await f.api('/v1/state', 'GET', undefined, one.token)).status, 200);
  assert.equal((await f.api('/v1/state', 'GET', undefined, one.token)).status, 429);
  assert.equal((await f.api('/v1/state', 'GET', undefined, two.token)).status, 200);
  for (let i = 0; i < 120; i++) assert.equal((await f.api('/v1/state')).status, 401);
  assert.equal((await f.api('/v1/state')).status, 429);
});

test('a simultaneous reconnect burst waits in a bounded queue while at most four password hashes execute', async t => {
  const f = await fixture(t); let peakWork = 0, peakQueue = 0;
  const sample = setInterval(() => { peakWork = Math.max(peakWork, f.service.authWork); peakQueue = Math.max(peakQueue, f.service.authQueue.length); }, 1);
  try {
    const responses = await Promise.all(Array.from({ length: 8 }, (_, i) => f.api('/v1/register', 'POST', { username: 'BurstUser' + i, password: 'burst-password-test' })));
    assert.ok(responses.every(response => response.status === 200), JSON.stringify(responses.map(response => response.data.error)));
    assert.equal(peakWork, 4); assert.ok(peakQueue > 0 && peakQueue <= 8);
    assert.equal(f.service.authWork, 0); assert.equal(f.service.authQueue.length, 0);
  } finally { clearInterval(sample); }
});

test('proxy visitor headers are ignored by default and never trusted from a non-loopback peer', async t => {
  const request = (peer, header) => ({ socket: { remoteAddress: peer }, headers: { 'cf-connecting-ip': header, 'x-forwarded-for': '203.0.113.99' } });
  assert.equal(clientAddress(request('127.0.0.1', '203.0.113.1')), '127.0.0.1');
  assert.equal(clientAddress(request('198.51.100.1', '203.0.113.1'), true), '198.51.100.1');
  assert.equal(clientAddress(request('::ffff:198.51.100.1', '203.0.113.1'), true), '::ffff:198.51.100.1');
  const f = await fixture(t);
  for (let i = 0; i < 13; i++) {
    const response = await fetch(f.baseUrl + '/v1/login', { method: 'POST', headers: {
      'Content-Type': 'application/json', 'X-Emberfield-Protocol': String(PROTOCOL_VERSION), 'X-Emberfield-Content': CONTENT_VERSION,
      'CF-Connecting-IP': `203.0.113.${i + 1}`, 'X-Forwarded-For': `198.51.100.${i + 1}`
    }, body: JSON.stringify({ username: '?', password: 'short' }) });
    await response.json(); assert.equal(response.status, i < 12 ? 400 : 429);
  }
});

test('explicit Cloudflare loopback proxy trust uses one valid IP and falls back for missing, malformed or multiple addresses', async t => {
  for (const peer of ['127.0.0.1', '127.1.2.3', '::1', '::ffff:127.0.0.1']) {
    const request = header => ({ socket: { remoteAddress: peer }, headers: { 'cf-connecting-ip': header, 'x-forwarded-for': '203.0.113.9' } });
    assert.equal(clientAddress(request('203.0.113.7'), true), '203.0.113.7');
    assert.equal(clientAddress(request('2001:db8::7'), true), '2001:db8::7');
    for (const header of [undefined, '', 'unknown', '203.0.113.1, 203.0.113.2', ['203.0.113.1'], '203.0.113.1:80'])
      assert.equal(clientAddress(request(header), true), peer);
  }
  const f = await fixture(t, { trustCloudflareLoopback: true });
  for (let i = 0; i < 14; i++) {
    const response = await fetch(f.baseUrl + '/v1/login', { method: 'POST', headers: {
      'Content-Type': 'application/json', 'X-Emberfield-Protocol': String(PROTOCOL_VERSION), 'X-Emberfield-Content': CONTENT_VERSION,
      'CF-Connecting-IP': i === 13 ? '203.0.113.8' : '203.0.113.7'
    }, body: JSON.stringify({ username: '?', password: 'short' }) });
    await response.json(); assert.equal(response.status, i === 12 ? 429 : 400);
  }
});

test('finished observations survive worker release and the next room can start while the other former seat reads results', async t => {
  const f = await fixture(t, { maxRooms: 1 }); const [one, two, outsider] = await f.accounts();
  const previous = await f.privateMatch(one, two);
  await f.api('/v1/surrender', 'POST', {}, two.token);
  const final = await f.api('/v1/matches/current/snapshot', 'GET', undefined, two.token);
  assert.equal(final.status, 200); assert.equal(final.data.state.status, 'finished');
  assert.equal(f.service.occupiedRooms(), 0);
  assert.equal(f.authority.requests.filter(request => request.op === 'close' && request.matchId === previous.matchId).length, 1);
  const room = await f.api('/v1/rooms', 'POST', { mode: 'Conquest', factionId: 'aven' }, one.token);
  assert.equal(room.status, 200);
  assert.equal((await f.api('/v1/rooms/join', 'POST', { code: room.data.roomCode, factionId: 'serevin' }, outsider.token)).status, 200);
  await f.api('/v1/rooms/ready', 'POST', { ready: true }, one.token);
  assert.equal((await f.api('/v1/rooms/ready', 'POST', { ready: true }, outsider.token)).data.status, 'active');
  const retained = await f.api('/v1/matches/current/snapshot', 'GET', undefined, two.token);
  assert.equal(retained.data.state.matchId, previous.matchId);
  assert.deepEqual(retained.data.observation, final.data.observation);
});

test('database survives restart, keeps completed history, aborts interrupted match, and stores no bearer token', t => {
  const directory = mkdtempSync(join(tmpdir(), 'emberfield-db-')); t.after(() => rmSync(directory, { recursive: true, force: true }));
  const file = join(directory, 'alpha.sqlite'); let db = new AccountDatabase(file, () => 100);
  db.createAccount('one', 'AlphaOne', 'salt', 'hash'); db.createAccount('two', 'AlphaTwo', 'salt', 'hash');
  const room = id => ({ matchId: id, queue: 'ranked', mode: 'Conquest', players: [{ accountId: 'one', playerId: 1, factionId: 'aven' }, { accountId: 'two', playerId: 2, factionId: 'serevin' }] });
  db.startMatch(room('complete'), 'test-content'); db.finishMatch('complete', { winnerPlayerId: 1, reason: 'Conquest', durationSeconds: 42, tick: 840 });
  db.startMatch(room('interrupted'), 'test-content'); db.close();
  db = new AccountDatabase(file, () => 200);
  assert.equal(db.profile('one').username, 'AlphaOne'); assert.equal(db.history('one').length, 2);
  assert.equal(db.history('one').find(m => m.matchId === 'interrupted').reason, 'server_restart');
  assert.equal(db.rating('one', 'historical:ranked:Conquest').wins, 1);
  assert.equal(db.finishMatch('complete', { winnerPlayerId: 2, reason: 'Surrender', durationSeconds: 0, tick: 0 }), false);
  db.close();
});

test('a second database owner cannot abort the live service matches during startup', t => {
  const directory = mkdtempSync(join(tmpdir(), 'emberfield-lock-')); t.after(() => rmSync(directory, { recursive: true, force: true }));
  const file = join(directory, 'alpha.sqlite'); const first = new AccountDatabase(file);
  try {
    first.createAccount('one', 'AlphaOne', 'salt', 'hash'); first.createAccount('two', 'AlphaTwo', 'salt', 'hash');
    first.startMatch({ matchId: 'live', queue: 'ranked', mode: 'Conquest', players: [{ accountId: 'one', playerId: 1, factionId: 'aven' }, { accountId: 'two', playerId: 2, factionId: 'serevin' }] }, 'version');
    assert.throws(() => new AccountDatabase(file), /already owned/);
    assert.equal(first.db.prepare('SELECT status FROM matches WHERE id=?').get('live').status, 'active');
  } finally { first.close(); }
  const restart = new AccountDatabase(file);
  assert.equal(restart.db.prepare('SELECT status FROM matches WHERE id=?').get('live').status, 'aborted'); restart.close();
});

test('request ID replay is rejected, malformed IDs are not retained, and the seat cache is bounded', async t => {
  const f = await fixture(t); const [one, two] = await f.accounts(); await f.privateMatch(one, two);
  const id = one.profile.accountId; const seat = f.service.seat(id).player;
  const issue = (sequence, RequestId) => f.service.command(id, { sequence, command: { Sequence: sequence, RequestId, Kind: 1 } });
  assert.equal((await issue(1, 'once')).accepted, true);
  assert.equal((await issue(2, 'once')).error, 'request_id_repeated'); assert.equal(seat.sequence, 2);
  assert.equal(f.authority.requests.filter(r => r.op === 'command').length, 1);
  assert.equal((await issue(3, 'x'.repeat(65))).error, 'invalid_request_id');
  assert.equal((await issue(4, 'bad id')).error, 'invalid_request_id');
  assert.equal(seat.recentRequestIds.size, 1);
  for (let sequence = 5; sequence <= 2052; sequence++) assert.equal((await issue(sequence, `request-${sequence}`)).accepted, true);
  assert.equal(seat.recentRequestIds.size, 2048); assert.equal(seat.recentRequestIds.has('once'), false);
  assert.equal(seat.recentRequestIds.has('request-2052'), true);
});


test('private rooms reject cross-realm factions and unknown content before authority creation', async t => {
  const f = await fixture(t); const [one, two] = await f.accounts();
  for (const choice of [{ realmId: 'fantasy', factionId: 'aven' }, { realmId: 'historical', factionId: 'solar' },
    { realmId: 'hybrid', factionId: 'solar' }, { realmId: 'fantasy', factionId: '__proto__' }, { realmId: 'fantasy', factionId: 'solar', mapId: '../greybox' }]) {
    const response = await f.api('/v1/rooms', 'POST', { mode: 'Conquest', ...choice }, one.token);
    assert.equal(response.status, 400);
  }
  const room = await f.api('/v1/rooms', 'POST', { realmId: 'fantasy', factionId: 'skeld', mode: 'Dominion', mapId: 'sunscar_basin' }, one.token);
  assert.equal(room.status, 200); assert.equal(room.data.realmId, 'fantasy');
  for (const choice of [{ realmId: 'historical', factionId: 'aven' }, { realmId: 'fantasy', factionId: 'aven' }, { realmId: 'historical', factionId: 'verdant' }])
    assert.equal((await f.api('/v1/rooms/join', 'POST', { code: room.data.roomCode, ...choice }, two.token)).status, 409);
  assert.equal((await f.api('/v1/rooms/join', 'POST', { code: room.data.roomCode, realmId: 'fantasy', factionId: 'verdant' }, two.token)).status, 200);
  await f.api('/v1/rooms/ready', 'POST', { ready: true, factionId: 'aven', realmId: 'historical' }, one.token);
  const started = await f.api('/v1/rooms/ready', 'POST', { ready: true }, two.token);
  assert.equal(started.data.status, 'active');
  const request = f.authority.requests.find(r => r.op === 'create');
  assert.equal(request.realmId, 'fantasy'); assert.equal(request.mapId, 'sunscar_basin');
  assert.deepEqual(request.players.map(p => p.factionId), ['skeld', 'verdant']);
});

test('widened matchmaking still cannot mix realms or battlefields', async t => {
  const f = await fixture(t); const [one, two, three] = await f.accounts(); const four = await f.register('FourthPlayer');
  const choices = [
    [one, 'historical', 'aven', 'amber_crossing'], [two, 'fantasy', 'skeld', 'amber_crossing'],
    [three, 'fantasy', 'ashen', 'sunscar_basin'], [four, 'fantasy', 'drakeforged', 'amber_crossing']
  ];
  for (const [account, realmId, factionId, mapId] of choices)
    assert.equal((await f.api('/v1/queue', 'POST', { realmId, factionId, mapId, queue: 'ranked', mode: 'Conquest' }, account.token)).status, 200);
  f.advance(50000); await f.service.maintenance();
  assert.equal(f.service.state(one.profile.accountId).status, 'queued'); assert.equal(f.service.state(three.profile.accountId).status, 'queued');
  assert.equal(f.service.state(two.profile.accountId).status, 'active'); assert.equal(f.service.state(four.profile.accountId).status, 'active');
  assert.equal(f.authority.requests.filter(r => r.op === 'create').length, 1);
});

test('the same accounts have independent ranks, results and scoped history in all three realms', async t => {
  const f = await fixture(t); const [one, two] = await f.accounts();
  for (const realmId of REALMS) {
    for (const account of [one, two]) assert.equal((await f.api('/v1/queue', 'POST', { realmId, factionId: REALM_FACTIONS[realmId].at(-1), queue: 'ranked', mode: 'Conquest' }, account.token)).status, 200);
    await f.service.maintenance();
    await f.api('/v1/surrender', 'POST', {}, (realmId === 'historical' ? two : one).token);
    for (const account of [one, two]) await f.api('/v1/rooms/leave', 'POST', {}, account.token);
  }
  const ratings = f.service.db.profile(one.profile.accountId).ratings;
  assert.equal(ratings.find(r => r.realmId === 'historical').rankPoints, 25);
  assert.equal(ratings.find(r => r.realmId === 'fantasy').rankPoints, 0);
  assert.equal(ratings.find(r => r.realmId === 'naval').rankPoints, 0);
  assert.equal(f.service.db.rating(one.profile.accountId, 'historical:ranked:Conquest').mmr, 1016);
  assert.equal(f.service.db.rating(one.profile.accountId, 'fantasy:ranked:Conquest').mmr, 984);
  assert.equal(f.service.db.rating(one.profile.accountId, 'naval:ranked:Conquest').mmr, 984);
  const history = f.service.db.history(one.profile.accountId);
  assert.equal(history.find(h => h.realmId === 'historical').outcome, 'win');
  assert.equal(history.find(h => h.realmId === 'fantasy').outcome, 'loss');
  assert.equal(history.length, 3);
  assert.ok(history.every(h => h.mapId === ({ naval: 'sapphire_coast', fantasy: 'legend_lands' }[h.realmId] ?? 'amber_crossing')));
  for (const realmId of REALMS) {
    const filtered = await f.api('/v1/history/' + realmId, 'GET', undefined, one.token);
    assert.equal(filtered.status, 200); assert.equal(filtered.data.matches.length, 1);
    assert.equal(filtered.data.matches[0].realmId, realmId);
  }
  assert.equal((await f.api('/v1/history/hybrid', 'GET', undefined, one.token)).status, 404);
});

test('only the 5 historical, 4 fantasy and 1 naval active factions can select their allowed maps', async t => {
  assert.deepEqual(REALM_FACTIONS, { historical: ['aven', 'serevin', 'english', 'sultanate', 'sahel'], fantasy: ['ashen', 'drakeforged', 'skeld', 'verdant'], naval: ['pirates'] });
  const f = await fixture(t); const one = await f.register('CatalogPlayer');
  for (const realmId of REALMS) for (const factionId of REALM_FACTIONS[realmId]) for (const mapId of Object.keys(MAPS)) {
    const room = await f.api('/v1/rooms', 'POST', { realmId, factionId, mapId, mode: 'Conquest' }, one.token);
    assert.equal(room.status, mapInRealm(mapId, realmId) ? 200 : 400, `${realmId}/${factionId}/${mapId}`);
    if (room.status === 200) assert.equal((await f.api('/v1/rooms/leave', 'POST', {}, one.token)).status, 200);
  }
  for (const realmId of REALMS) for (const other of REALMS.filter(id => id !== realmId))
    for (const path of ['/v1/rooms', '/v1/queue'])
      assert.equal((await f.api(path, 'POST', { realmId, factionId: REALM_FACTIONS[other][0], queue: 'ranked', mode: 'Conquest' }, one.token)).status, 400);
  for (const patch of [{ factionId: ['aven'] }, { mapId: ['amber_crossing'] }, { realmId: ['historical'] }, { factionId: {} }])
    for (const path of ['/v1/rooms', '/v1/queue'])
      assert.equal((await f.api(path, 'POST', { realmId: 'historical', factionId: 'aven', mapId: 'amber_crossing', queue: 'ranked', mode: 'Conquest', ...patch }, one.token)).status, 400);
  assert.equal(f.authority.requests.filter(r => r.op === 'create').length, 0);
});

test('the desert factions queue as historical, meet on their home desert, and are ranked and recorded there', async t => {
  const f = await fixture(t); const [one, two] = await f.accounts();
  for (const [realmId, factionId] of [['fantasy', 'sultanate'], ['naval', 'sahel'], ['fantasy', 'sahel']])
    for (const path of ['/v1/rooms', '/v1/queue'])
      assert.equal((await f.api(path, 'POST', { realmId, factionId, mapId: 'sunscar_basin', queue: 'ranked', mode: 'Conquest' }, one.token)).status, 400, `${realmId}/${factionId}`);
  assert.equal((await f.api('/v1/queue', 'POST', { realmId: 'historical', factionId: 'sahel', mapId: 'legend_lands', queue: 'ranked', mode: 'Conquest' }, one.token)).status, 400);
  for (const [account, factionId] of [[one, 'sultanate'], [two, 'sahel']])
    assert.equal((await f.api('/v1/queue', 'POST', { realmId: 'historical', factionId, mapId: 'sunscar_basin', queue: 'ranked', mode: 'Conquest' }, account.token)).status, 200);
  await f.service.maintenance();
  const creation = f.authority.requests.find(r => r.op === 'create');
  assert.equal(creation.realmId, 'historical'); assert.equal(creation.mapId, 'sunscar_basin');
  assert.deepEqual(creation.players.map(p => p.factionId).sort(), ['sahel', 'sultanate']);
  await f.api('/v1/surrender', 'POST', {}, two.token);
  assert.equal(f.service.db.rating(one.profile.accountId, 'historical:ranked:Conquest').wins, 1);
  for (const [account, factionId, outcome] of [[one, 'sultanate', 'win'], [two, 'sahel', 'loss']]) {
    const history = await f.api('/v1/history/historical', 'GET', undefined, account.token);
    assert.equal(history.status, 200); assert.equal(history.data.matches.length, 1);
    const [match] = history.data.matches;
    assert.equal(match.realmId, 'historical'); assert.equal(match.mapId, 'sunscar_basin');
    assert.equal(match.factionId, factionId); assert.equal(match.outcome, outcome);
    assert.equal((await f.api('/v1/history/fantasy', 'GET', undefined, account.token)).data.matches.length, 0);
  }
  const room = await f.api('/v1/rooms', 'POST', { realmId: 'historical', factionId: 'sahel', mode: 'Dominion', mapId: 'amber_crossing' }, one.token);
  assert.equal(room.status, 200); assert.equal(room.data.realmId, 'historical');
});

test('legacy and planned factions cannot create, join or queue; naval mirror rooms preserve authoritative choices', async t => {
  const f = await fixture(t); const [one, two] = await f.accounts();
  const room = await f.api('/v1/rooms', 'POST', { realmId: 'naval', factionId: 'pirates', mode: 'Dominion' }, one.token);
  assert.equal(room.status, 200); assert.equal(room.data.mapId, 'sapphire_coast');
  for (const [realmId, factionId] of [['historical', 'miraj'], ['fantasy', 'solar'], ['historical', 'skeld'],
    ['naval', 'english_navy'], ['naval', 'spanish_navy'], ['naval', 'skeleton_fleet']]) {
    for (const path of ['/v1/rooms', '/v1/queue'])
      assert.equal((await f.api(path, 'POST', { realmId, factionId, queue: 'casual', mode: 'Conquest' }, two.token)).status, 400);
    const joined = await f.api('/v1/rooms/join', 'POST', { code: room.data.roomCode, realmId, factionId }, two.token);
    assert.ok(joined.status === 400 || joined.status === 409);
  }
  for (const mapId of ['amber_crossing', 'sunscar_basin'])
    assert.equal((await f.api('/v1/queue', 'POST', { realmId: 'naval', factionId: 'pirates', mapId, queue: 'ranked', mode: 'Conquest' }, two.token)).status, 400);
  assert.equal((await f.api('/v1/rooms/join', 'POST', { code: room.data.roomCode, realmId: 'naval', factionId: 'pirates' }, two.token)).status, 200);
  await f.api('/v1/rooms/ready', 'POST', { ready: true, realmId: 'historical', factionId: 'english', mapId: 'amber_crossing' }, one.token);
  assert.equal((await f.api('/v1/rooms/ready', 'POST', { ready: true }, two.token)).data.status, 'active');
  const creation = f.authority.requests.find(r => r.op === 'create');
  assert.equal(creation.realmId, 'naval'); assert.equal(creation.mapId, 'sapphire_coast');
  assert.deepEqual(creation.players, [{ slot: 1, factionId: 'pirates' }, { slot: 2, factionId: 'pirates' }]);
});

test('odd queue populations keep one seat waiting per realm and match only a later compatible account', async t => {
  const f = await fixture(t); const waiting = [];
  for (const realmId of REALMS) {
    const group = [];
    for (let i = 0; i < 3; i++) {
      const account = await f.register(`${realmId}_${i}`); group.push(account);
      assert.equal((await f.api('/v1/queue', 'POST', { realmId, factionId: REALM_FACTIONS[realmId][i % REALM_FACTIONS[realmId].length], queue: 'ranked', mode: 'Conquest' }, account.token)).status, 200);
    }
    waiting.push([realmId, group[2]]);
  }
  f.advance(50000); await f.service.maintenance();
  assert.equal(f.authority.requests.filter(r => r.op === 'create').length, 3);
  for (const [realmId, account] of waiting) {
    assert.equal(f.service.state(account.profile.accountId).status, 'queued');
    const peer = await f.register(`${realmId}_late`);
    assert.equal((await f.api('/v1/queue', 'POST', { realmId, factionId: REALM_FACTIONS[realmId][0], queue: 'ranked', mode: 'Conquest' }, peer.token)).status, 200);
    await f.service.maintenance();
    const state = f.service.state(account.profile.accountId);
    assert.equal(state.status, 'active'); assert.equal(state.realmId, realmId);
    assert.equal(state.matchId, f.service.state(peer.profile.accountId).matchId);
  }
  assert.equal(f.service.queued.size, 0);
  assert.equal(f.authority.requests.filter(r => r.op === 'create').length, 6);
});

const cosmeticCatalog = { items: [
  { id: 'frost_skin', displayName: 'Frost', description: 'Appearance only', slot: 'creature', targetId: 'ember_drake', realmId: 'fantasy', styleId: 'sapphire_frost', priceMinor: 399, currency: 'EUR' },
  { id: 'ember_skin', displayName: 'Ember', description: 'Appearance only', slot: 'creature', targetId: 'ember_drake', realmId: 'fantasy', styleId: 'ember_crown', priceMinor: 399, currency: 'EUR' }
] };

test('cosmetic purchases fail closed without a provider and client ownership cannot equip an item', async t => {
  const f = await fixture(t, { cosmeticCatalog }); const one = await f.register('SkinBuyer');
  const wardrobe = await f.api('/v1/cosmetics', 'GET', undefined, one.token);
  assert.equal(wardrobe.data.purchasesAvailable, false); assert.equal(wardrobe.data.sandboxEnabled, false);
  assert.deepEqual(wardrobe.data.ownedIds, []);
  assert.equal((await f.api('/v1/cosmetics/equip', 'POST', { itemId: 'frost_skin', owned: true }, one.token)).status, 403);
  const request = { itemId: 'frost_skin', idempotencyKey: 'purchase-0001', receipt: '{"verified":true}' };
  assert.equal((await f.api('/v1/cosmetics/purchase', 'POST', request, one.token)).data.error, 'payment_provider_unconfigured');
  assert.equal((await f.api('/v1/cosmetics/sandbox-claim', 'POST', request, one.token)).status, 403);
  assert.deepEqual(f.service.db.wardrobe(one.profile.accountId).ownedIds, []);
  assert.equal(f.authority.requests.length, 0);
});

test('sandbox entitlements are explicit and idempotent; equipment never enters authority gameplay inputs', async t => {
  const f = await fixture(t, { cosmeticCatalog, sandboxCosmetics: true }); const [one, two] = await f.accounts();
  const request = { itemId: 'frost_skin', idempotencyKey: 'sandbox-0001', damage: 999999 };
  assert.equal((await f.api('/v1/cosmetics/sandbox-claim', 'POST', request, one.token)).status, 200);
  assert.equal((await f.api('/v1/cosmetics/sandbox-claim', 'POST', request, one.token)).status, 200);
  assert.equal((await f.api('/v1/cosmetics/sandbox-claim', 'POST', { ...request, itemId: 'ember_skin' }, one.token)).status, 409);
  const equipped = await f.api('/v1/cosmetics/equip', 'POST', { itemId: 'frost_skin' }, one.token);
  assert.deepEqual(equipped.data.entitlements, [{ itemId: 'frost_skin', source: 'sandbox' }]);
  assert.equal((await f.api('/v1/cosmetics/equip', 'POST', { itemId: 'frost_skin' }, two.token)).status, 403);
  const room = await f.api('/v1/rooms', 'POST', { realmId: 'fantasy', factionId: 'drakeforged', mode: 'Conquest' }, one.token);
  await f.api('/v1/rooms/join', 'POST', { code: room.data.roomCode, realmId: 'fantasy', factionId: 'ashen' }, two.token);
  await f.api('/v1/rooms/ready', 'POST', { ready: true }, one.token); await f.api('/v1/rooms/ready', 'POST', { ready: true }, two.token);
  assert.equal(f.service.state(one.profile.accountId).players[0].cosmetics[0].itemId, 'frost_skin');
  const creation = f.authority.requests.find(r => r.op === 'create');
  assert.ok(!JSON.stringify(creation).includes('skin')); assert.ok(!JSON.stringify(creation).includes('damage'));
  assert.deepEqual(creation.players, [{ slot: 1, factionId: 'drakeforged' }, { slot: 2, factionId: 'ashen' }]);
});

test('only verified account/product bound receipts grant ownership and transaction replay is rejected', async t => {
  let verificationCalls = 0;
  const verifyPurchase = async ({ accountId, item, receipt }) => {
    verificationCalls++;
    if (receipt === 'valid-proof') return { verified: true, accountId, itemId: item.id, provider: 'test-provider', transactionId: 'transaction-1' };
    if (receipt === 'wrong-account') return { verified: true, accountId: 'other-account', itemId: item.id, provider: 'test-provider', transactionId: 'transaction-2' };
    if (receipt === 'wrong-product') return { verified: true, accountId, itemId: 'other-item', provider: 'test-provider', transactionId: 'transaction-3' };
    return null;
  };
  const f = await fixture(t, { cosmeticCatalog, verifyPurchase }); const [one, two] = await f.accounts();
  for (const receipt of ['forged', 'wrong-account', 'wrong-product'])
    assert.equal((await f.api('/v1/cosmetics/purchase', 'POST', { itemId: 'frost_skin', idempotencyKey: 'purchase-' + receipt, receipt }, one.token)).status, 400);
  const request = { itemId: 'frost_skin', idempotencyKey: 'purchase-valid', receipt: 'valid-proof' };
  assert.equal((await f.api('/v1/cosmetics/purchase', 'POST', request, one.token)).status, 200);
  const calls = verificationCalls;
  assert.equal((await f.api('/v1/cosmetics/purchase', 'POST', request, one.token)).status, 200); assert.equal(verificationCalls, calls);
  assert.equal((await f.api('/v1/cosmetics/purchase', 'POST', { ...request, idempotencyKey: 'purchase-replay' }, one.token)).status, 409);
  assert.equal((await f.api('/v1/cosmetics/purchase', 'POST', request, two.token)).status, 409);
  assert.deepEqual(f.service.db.wardrobe(two.profile.accountId).ownedIds, []);
  assert.deepEqual(f.service.db.wardrobe(one.profile.accountId).entitlements.map(e => ({ ...e })), [{ itemId: 'frost_skin', source: 'purchase' }]);
});

test('cosmetic catalogs reject gameplay fields and unrecognized appearances before service startup', () => {
  for (const patch of [{ damage: 200 }, { MoveSpeedMillimetresPerSecond: 9000 }, { hitbox: 10 }, { targetId: 'unknown_unit' }, { styleId: 'unknown_style' }])
    assert.throws(() => new EmberfieldService({ authority: new FakeAuthority(), cosmeticCatalog: { items: [{ ...cosmeticCatalog.items[0], ...patch }] }, maintenance: false }), /Invalid cosmetic item/);
});


test('legacy ratings and matches migrate to historical while ownership persists across restart', () => {
  const folder = mkdtempSync(join(tmpdir(), 'emberfield-migration-')); const path = join(folder, 'accounts.sqlite');
  let db;
  try {
    db = new AccountDatabase(path); db.createAccount('one', 'PlayerOne', 'salt', 'hash'); db.createAccount('two', 'PlayerTwo', 'salt', 'hash');
    db.startMatch({ matchId: 'legacy-match', queue: 'ranked', mode: 'Conquest', players: [
      { accountId: 'one', playerId: 1, factionId: 'aven' }, { accountId: 'two', playerId: 2, factionId: 'serevin' }] }, 'legacy-content');
    db.finishMatch('legacy-match', { winnerPlayerId: 1, reason: 'Conquest', durationSeconds: 20, tick: 400 });
    db.db.exec("UPDATE ratings SET queue_key='ranked:Conquest'; ALTER TABLE matches DROP COLUMN realm_id; ALTER TABLE matches DROP COLUMN map_id; PRAGMA user_version=1;");
    assert.equal(db.grantCosmetic('one', 'frost_skin', 'durable-claim', 'purchase', 'test-provider', 'durable-transaction'), true);
    db.equipCosmetic('one', cosmeticCatalog.items[0]); db.close(); db = null;
    db = new AccountDatabase(path);
    assert.equal(db.rating('one', 'historical:ranked:Conquest').mmr, 1016);
    assert.equal(db.rating('one', 'fantasy:ranked:Conquest').mmr, 1000);
    assert.equal(db.history('one')[0].realmId, 'historical'); assert.equal(db.history('one')[0].mapId, 'amber_crossing');
    assert.deepEqual(db.wardrobe('one').ownedIds, ['frost_skin']); assert.equal(db.wardrobe('one').equipped[0].itemId, 'frost_skin');
    assert.equal(db.grantCosmetic('two', 'frost_skin', 'replay-claim', 'purchase', 'test-provider', 'durable-transaction'), false);
  } finally { db?.close(); rmSync(folder, { recursive: true, force: true }); }
});

test('shipped Frostguard cosmetic moves to fantasy while existing ownership and equipment survive catalog reload', async t => {
  const catalog = loadCatalog(), item = catalog.find(value => value.id === 'frostguard_bronze');
  assert.ok(item); assert.equal(item.realmId, 'fantasy'); assert.equal(item.targetId, 'frostguard');
  const folder = mkdtempSync(join(tmpdir(), 'emberfield-frostguard-')), databasePath = join(folder, 'accounts.sqlite');
  let previous, current;
  try {
    // Model the previously shipped historical catalog against a durable test account.
    previous = await fixture(t, { databasePath, cosmeticCatalog: { items: catalog.map(value => value.id === item.id ? { ...value, realmId: 'historical' } : value) } });
    const owner = await previous.register('FrostguardOwner');
    previous.service.db.grantCosmetic(owner.profile.accountId, item.id, 'original-purchase', 'purchase', 'test-provider', 'original-frostguard-transaction');
    assert.equal((await previous.api('/v1/cosmetics/equip', 'POST', { itemId: item.id }, owner.token)).status, 200);
    const before = previous.service.db.wardrobe(owner.profile.accountId);
    await previous.service.close();

    current = await fixture(t, { databasePath }); // Read the actual corrected shipped catalog on restart.
    const wardrobe = await current.api('/v1/cosmetics', 'GET', undefined, owner.token);
    assert.equal(wardrobe.status, 200); assert.equal(wardrobe.data.items.find(value => value.id === item.id).realmId, 'fantasy');
    assert.deepEqual(current.service.db.wardrobe(owner.profile.accountId), before);
    assert.deepEqual(wardrobe.data.entitlements, [{ itemId: item.id, source: 'purchase' }]);
    assert.equal((await current.api('/v1/cosmetics/equip', 'POST', { itemId: item.id }, owner.token)).status, 200);
    const outsider = await current.register('FrostguardOutsider');
    assert.equal((await current.api('/v1/cosmetics/equip', 'POST', { itemId: item.id }, outsider.token)).status, 403);
    for (const [realmId, factionId] of [['fantasy', 'skeld'], ['historical', 'aven'], ['naval', 'pirates']]) {
      const room = await current.api('/v1/rooms', 'POST', { realmId, factionId, mode: 'Conquest' }, owner.token);
      assert.equal(room.status, 200);
      assert.deepEqual(room.data.players[0].cosmetics.map(value => value.itemId), realmId === 'fantasy' ? [item.id] : []);
      assert.equal((await current.api('/v1/rooms/leave', 'POST', {}, owner.token)).status, 200);
    }
    assert.deepEqual(current.service.db.wardrobe(owner.profile.accountId), before);
  } finally { await previous?.service.close(); await current?.service.close(); rmSync(folder, { recursive: true, force: true }); }
});

test('v3 migration preserves naval, fantasy, legacy history and colliding ratings across two restarts', () => {
  const folder = mkdtempSync(join(tmpdir(), 'emberfield-realms-migration-')); const path = join(folder, 'accounts.sqlite');
  let db;
  try {
    db = new AccountDatabase(path, () => 1000);
    db.createAccount('one', 'OriginalAccount', 'original-salt', 'original-password-hash'); db.createAccount('two', 'OriginalPeer', 'salt', 'hash');
    db.createSession('hashed-session', 'one', 1000000);
    for (const [matchId, realmId, factionId] of [['old-skeld', 'historical', 'skeld'], ['old-miraj', 'historical', 'miraj'],
      ['old-solar', 'fantasy', 'solar'], ['new-pirates', 'naval', 'pirates']]) {
      db.startMatch({ matchId, realmId, mapId: realmId === 'naval' ? 'sapphire_coast' : 'amber_crossing', queue: 'ranked', mode: 'Conquest', players: [
        { accountId: 'one', playerId: 1, factionId }, { accountId: 'two', playerId: 2, factionId }] }, 'original-' + matchId);
      db.finishMatch(matchId, { winnerPlayerId: 1, reason: 'Conquest', durationSeconds: 20, tick: 400 });
    }
    const insert = db.db.prepare('INSERT INTO ratings VALUES(?,?,?,?,?,?,?)');
    insert.run('one', 'ranked:Conquest', 1190, 77, 6, 5, 4); // Collision: preserve both original records.
    insert.run('one', 'casual:Dominion', 1120, 0, 3, 2, 1); // Only this recognized unscoped key migrates.
    insert.run('one', 'future:ranked:Conquest', 1300, 91, 8, 7, 6);
    db.grantCosmetic('one', 'frost_skin', 'realm-stable-claim', 'purchase', 'test-provider', 'realm-stable-transaction');
    db.equipCosmetic('one', cosmeticCatalog.items[0]);
    const account = { ...db.accountById('one') }, histories = db.history('one'), wardrobe = db.wardrobe('one');
    const ratings = db.db.prepare('SELECT * FROM ratings ORDER BY account_id,queue_key').all()
      .map(row => ({ ...row, queue_key: row.queue_key === 'casual:Dominion' ? 'historical:casual:Dominion' : row.queue_key }))
      .sort((a, b) => a.account_id.localeCompare(b.account_id) || a.queue_key.localeCompare(b.queue_key));
    db.db.exec('PRAGMA user_version=2'); db.close(); db = null;
    for (let restart = 0; restart < 2; restart++) {
      db = new AccountDatabase(path, () => 2000 + restart);
      assert.equal(db.db.prepare('PRAGMA user_version').get().user_version, 3);
      assert.deepEqual({ ...db.accountById('one') }, account); assert.equal(db.session('hashed-session').id, 'one');
      assert.deepEqual(db.history('one'), histories); assert.deepEqual(db.wardrobe('one'), wardrobe);
      const actual = db.db.prepare('SELECT * FROM ratings').all().map(row => ({ ...row }))
        .sort((a, b) => a.account_id.localeCompare(b.account_id) || a.queue_key.localeCompare(b.queue_key));
      assert.deepEqual(actual, ratings);
      assert.equal(db.history('one', 25, 'naval')[0].factionId, 'pirates');
      assert.equal(db.history('one', 25, 'historical').find(row => row.matchId === 'old-skeld').factionId, 'skeld');
      assert.equal(db.grantCosmetic('two', 'frost_skin', 'replay-attempt', 'purchase', 'test-provider', 'realm-stable-transaction'), false);
      db.close(); db = null;
    }
  } finally { db?.close(); rmSync(folder, { recursive: true, force: true }); }
});
