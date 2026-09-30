import assert from 'node:assert/strict';
import { fork } from 'node:child_process';
import { mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { performance, monitorEventLoopDelay } from 'node:perf_hooks';
import { createHash, randomBytes } from 'node:crypto';
import { setTimeout as delay } from 'node:timers/promises';
import { AuthorityPool } from '../Server/authority-pool.mjs';
import { EmberfieldService } from '../Server/service.mjs';
import { CONTENT_VERSION, PROTOCOL_VERSION, verifyContent } from '../Server/content-version.mjs';
import { REALM_FACTIONS, REALMS, MAPS, mapInRealm } from '../Server/content-realms.mjs';

const filename = fileURLToPath(import.meta.url), repository = resolve(dirname(filename), '..');
const option = (name, fallback) => { const index = process.argv.indexOf(name); return index < 0 ? fallback : process.argv[index + 1]; };
const percentile = (values, p) => values.length ? [...values].sort((a, b) => a - b)[Math.min(values.length - 1, Math.ceil(values.length * p) - 1)] : 0;
const summary = values => ({ count: values.length, mean: values.reduce((a, b) => a + b, 0) / Math.max(1, values.length), min: values.reduce((a, b) => Math.min(a, b), values.length ? Infinity : 0), p50: percentile(values, .5), p95: percentile(values, .95), p99: percentile(values, .99), max: values.reduce((a, b) => Math.max(a, b), 0) });

async function clients(config) {
  const { address, accounts, durationSeconds, matches } = config;
  const samples = { snapshot: [], command: [], other: [] }, errors = [], ticks = new Map();
  let accepted = 0, rejected = 0, bytes = 0, reconnects = 0, snapshots = 0;
  const api = async (client, path, method = 'GET', body, expected = 200) => {
    const begin = performance.now();
    let response;
    try { response = await fetch(address + path, { method, signal: AbortSignal.timeout(10000), headers: {
      'X-Emberfield-Protocol': String(PROTOCOL_VERSION), 'X-Emberfield-Content': CONTENT_VERSION,
      ...(body !== undefined ? { 'Content-Type': 'application/json' } : {}),
      ...(client?.token ? { Authorization: 'Bearer ' + client.token } : {})
    }, ...(body !== undefined ? { body: JSON.stringify(body) } : {}) }); }
    catch (error) { throw new Error(`${method} ${path}: ${error.message}; cause=${error.cause?.code ?? error.name}: ${error.cause?.message ?? ''}`); }
    const text = await response.text(); bytes += Buffer.byteLength(text);
    const kind = path.endsWith('/snapshot') ? 'snapshot' : path.endsWith('/commands') ? 'command' : 'other';
    samples[kind].push(performance.now() - begin);
    const data = JSON.parse(text);
    assert.equal(response.status, expected, `${path}: ${response.status} ${data.error}`);
    return data;
  };
  const observe = async client => {
    const state = await api(client, '/v1/matches/current/snapshot'); const view = state.observation;
    assert.equal(state.state.matchId, client.matchId); assert.equal(view.ServerPlayerId, client.playerId);
    assert.equal(view.RealmId, client.realmId); assert.equal(view.MapId, client.mapId);
    assert.equal(view.LocalPlayer.FactionId, client.factionId);
    assert.equal(Object.hasOwn(view, 'OpponentResources'), false);
    if (client.view) assert.ok(view.Tick >= client.view.Tick, 'A match tick must not move backwards.');
    client.view = view; client.sequence = Math.max(client.sequence, state.lastAcceptedSequence);
    if (state.state.status === 'active') {
      const now = performance.now(); let sample = ticks.get(client.matchId);
      if (!sample) ticks.set(client.matchId, sample = { firstTick: view.Tick, firstTime: now });
      sample.lastTick = view.Tick; sample.lastTime = now;
    }
    snapshots++;
    return state;
  };
  const command = async (client, fields, requestId) => {
    const sequence = ++client.sequence;
    const result = await api(client, '/v1/matches/current/commands', 'POST', { sequence, matchId: 'forged-wrapper-is-ignored', playerId: 99,
      command: { Version: PROTOCOL_VERSION, Sequence: sequence, IssuedTick: client.view.Tick,
        RequestId: requestId ?? `load-${client.index}-${sequence}`, UnitIds: [], ...fields } });
    assert.equal(result.lastAcceptedSequence, sequence);
    if (result.accepted) accepted++; else rejected++;
    return result;
  };
  // Rotate every active roster, including pirate mirrors, across all legal maps,
  // private/casual/ranked flows and both victory modes. Match counts may be odd.
  for (let pair = 0; pair < matches; pair++) {
    const one = accounts[pair * 2], two = accounts[pair * 2 + 1];
    const realmId = REALMS[pair % REALMS.length], cycle = Math.floor(pair / REALMS.length);
    const maps = Object.keys(MAPS).filter(id => mapInRealm(id, realmId)), factions = REALM_FACTIONS[realmId];
    const mapId = maps[cycle % maps.length], mode = Math.floor(pair / 9) % 2 ? 'Dominion' : 'Conquest';
    const firstFaction = factions[cycle % factions.length], secondFaction = factions[(cycle + 1) % factions.length];
    Object.assign(one, { index: pair * 2, realmId, mapId, mode, factionId: firstFaction, sequence: 0 });
    Object.assign(two, { index: pair * 2 + 1, realmId, mapId, mode, factionId: secondFaction, sequence: 0 });
    if (cycle % 3 === 0) {
      const room = await api(one, '/v1/rooms', 'POST', { realmId, mapId, mode, factionId: firstFaction });
      await api(two, '/v1/rooms/join', 'POST', { code: room.roomCode, realmId, factionId: secondFaction });
      await api(one, '/v1/rooms/ready', 'POST', { ready: true }); await api(two, '/v1/rooms/ready', 'POST', { ready: true });
    } else {
      const queue = cycle % 3 === 1 ? 'casual' : 'ranked';
      for (const client of [one, two]) await api(client, '/v1/queue', 'POST', { realmId, mapId, mode, queue, factionId: client.factionId });
    }
  }
  const deadline = performance.now() + 30000;
  for (const client of accounts) {
    let state;
    do { state = await api(client, '/v1/state'); if (state.status !== 'active') await delay(100); }
    while (state.status !== 'active' && performance.now() < deadline);
    assert.equal(state.status, 'active'); client.matchId = state.matchId; client.playerId = state.playerId;
    await observe(client);
  }
  assert.equal(new Set(accounts.map(client => client.matchId)).size, matches);
  process.send({ progress: 'all_matches_active', matches, clients: accounts.length });
  // Ownership attack and server-owned sequence/request-ID rejection use the actual authority.
  const first = accounts[0], rival = accounts.find(client => client.matchId === first.matchId && client !== first);
  assert.equal((await command(first, { Kind: 1, UnitIds: [rival.view.Units[0].Id], X: 20000, Z: 20000 })).accepted, false);
  const paid = { Kind: 7, EntityId: first.view.Buildings.find(building => building.DefinitionId === 'hearth').Id, DefinitionId: 'tender' };
  assert.equal((await command(first, paid, 'paid-dedup-proof')).accepted, true);
  assert.equal((await command(first, paid, 'paid-dedup-proof')).accepted, false);
  await api(first, '/v1/matches/current/commands', 'POST', { sequence: first.sequence, command: { Sequence: first.sequence } }, 409);
  await api(first, '/v1/results', 'POST', { matchId: first.matchId, winnerPlayerId: 1 }, 404);
  const started = performance.now();
  const disconnectAt = Math.min(10, Math.max(4, durationSeconds - 30)), reconnectAt = disconnectAt + 20;
  let droppedObserved = false;
  const runClient = async client => {
    let step = 0, next = performance.now(), nextCommand = next;
    const disconnectThisClient = client.index % 16 === 1;
    let loggedOut = false, reconnected = false;
    while (performance.now() - started < durationSeconds * 1000) {
      const elapsed = (performance.now() - started) / 1000;
      try {
        if (disconnectThisClient && elapsed >= disconnectAt && elapsed < reconnectAt) {
          if (!loggedOut) { await api(client, '/v1/logout', 'POST', {}); loggedOut = true; }
          await delay(200); next = performance.now(); continue;
        }
        if (loggedOut && !reconnected) {
          const login = await api(null, '/v1/login', 'POST', { username: client.username, password: client.password });
          client.token = login.token;
          const restored = await api(client, '/v1/state');
          assert.equal(restored.matchId, client.matchId); assert.equal(restored.playerId, client.playerId);
          assert.equal(restored.lastAcceptedSequence, client.sequence);
          reconnected = true; reconnects++;
        }
        const state = await observe(client);
        if (elapsed > disconnectAt + 16 && elapsed < reconnectAt && state.state.players.some(player => !player.connected)) droppedObserved = true;
        assert.equal(state.state.status, 'active');
        if (performance.now() >= nextCommand) {
          nextCommand += 1000;
          const own = client.view.Units.filter(unit => unit.OwnerId === 1 && unit.CarryCapacity > 0 && unit.GatherAmount > 0);
          const worker = own[Math.min(1, own.length - 1)];
          const resource = client.view.Resources.find(item => item.RemainingAmount > 0) ?? client.view.Resources[0];
          let fields;
          if (step === 0 && client.index !== 0) fields = { Kind: 7, EntityId: client.view.Buildings.find(building => building.DefinitionId === 'hearth').Id,
            DefinitionId: client.realmId === 'naval' ? 'treasure_seeker' : 'tender' };
          else if (step % 10 === 1 && own.length && resource) fields = { Kind: 3, UnitIds: [own[0].Id], TargetId: resource.Id };
          else fields = { Kind: 1, UnitIds: [worker.Id], X: worker.Position.X + (step % 2 ? 500 : -500), Z: worker.Position.Z };
          const result = await command(client, fields);
          if (!result.accepted) errors.push({ client: client.index, kind: 'gameplay_rejected', error: result.error });
          step++;
        }
      } catch (error) { errors.push({ client: client.index, error: error.message }); }
      next += 200;
      await delay(Math.max(0, next - performance.now()));
      if (performance.now() - next > 1000) next = performance.now();
    }
  };
  await Promise.all(accounts.map(runClient));
  const elapsedSeconds = (performance.now() - started) / 1000;
  assert.equal(errors.length, 0, JSON.stringify(errors.slice(0, 5)));
  assert.equal(reconnects, Math.ceil(accounts.length / 16)); assert.equal(droppedObserved, true);
  process.send({ progress: 'timed_gameplay_validated', matches, clients: accounts.length, elapsedSeconds,
    snapshots, acceptedCommands: accepted, intentionalRejectedCommands: rejected, reconnects,
    snapshotLatencyMs: summary(samples.snapshot), commandLatencyMs: summary(samples.command) });
  // Finish every match, verify one durable record for each seat, and retain filtered terminal observations.
  const byMatch = new Map(); for (const client of accounts) { if (!byMatch.has(client.matchId)) byMatch.set(client.matchId, []); byMatch.get(client.matchId).push(client); }
  await Promise.all([...byMatch.values()].map(async pair => {
    await api(pair[1], '/v1/surrender', 'POST', {});
    for (const client of pair) {
      const final = await observe(client); assert.equal(final.observation.Match.IsFinished, true);
      const history = await api(client, '/v1/history'); assert.equal(history.matches.length, 1);
      assert.equal(history.matches[0].matchId, client.matchId);
      const profile = await api(client, '/v1/profile');
      const rank = profile.profile.ratings.find(rating => rating.queue === client.realmId + ':ranked:' + client.mode);
      if (history.matches[0].queue === 'ranked') assert.equal(rank.wins + rank.losses + rank.draws, 1);
    }
  }));
  // Finished viewers need not leave before new users can use all freed capacity.
  process.send({ progress: 'matches_finished_and_history_verified', matches });
  const replacement = accounts[0], replacementPeer = accounts[1];
  const room = await api(replacement, '/v1/rooms', 'POST', { realmId: replacement.realmId, mapId: replacement.mapId, factionId: replacement.factionId, mode: 'Conquest' });
  await api(replacementPeer, '/v1/rooms/join', 'POST', { code: room.roomCode, realmId: replacementPeer.realmId, factionId: replacementPeer.factionId });
  await api(replacement, '/v1/rooms/ready', 'POST', { ready: true });
  const restarted = await api(replacementPeer, '/v1/rooms/ready', 'POST', { ready: true }); assert.equal(restarted.status, 'active');
  await api(replacementPeer, '/v1/surrender', 'POST', {});
  assert.equal((await api(replacement, '/v1/matches/current/snapshot')).observation.Match.IsFinished, true);
  const tickRates = [...ticks.values()].map(sample => (sample.lastTick - sample.firstTick) * 1000 / (sample.lastTime - sample.firstTime));
  return { matches, clients: accounts.length, elapsedSeconds, requests: samples.snapshot.length + samples.command.length + samples.other.length,
    realmMatches: Object.fromEntries(REALMS.map(realm => [realm, new Set(accounts.filter(client => client.realmId === realm).map(client => client.matchId)).size])),
    snapshots, acceptedCommands: accepted, intentionalRejectedCommands: rejected, errors, reconnects, disconnectedSeatObserved: droppedObserved,
    completedHistories: accounts.length, replacementMatchStarted: true, snapshotLatencyMs: summary(samples.snapshot), commandLatencyMs: summary(samples.command),
    otherLatencyMs: summary(samples.other), observedTicksPerSecond: summary(tickRates), responseBytes: bytes,
    workload: 'Separate HTTP client process: 5 snapshots/s and 1 accepted movement/economy/production order/s per connected player; initial armies, no late-game unit saturation.' };
}

async function orchestrate() {
  verifyContent(repository);
  const matches = Number(option('--matches', 32)), durationSeconds = Number(option('--seconds', 75));
  assert.ok(Number.isInteger(matches) && matches >= 2 && matches <= 128);
  assert.ok(durationSeconds >= 30 && durationSeconds <= 600);
  const stamp = new Date().toISOString().replace(/[:.]/g, '-');
  const output = resolve(option('--output', `D:/CodexTooling/online-validation/load-${matches}-${stamp}`)); mkdirSync(output, { recursive: true });
  const authority = new AuthorityPool(process.env.AUTHORITY_COMMAND ?? 'dotnet', [resolve(repository, 'Server/AuthorityWorker/out/Emberfield.Authority.dll')],
    { cwd: repository, workerCount: Math.ceil(matches / 8), matchesPerWorker: 8 });
  const service = new EmberfieldService({ authority, databasePath: resolve(output, 'scratch.sqlite') });
  const lag = monitorEventLoopDelay({ resolution: 20 }); lag.enable();
  let maxRss = process.memoryUsage().rss, maxInFlight = 0;
  const sampler = setInterval(() => { maxRss = Math.max(maxRss, process.memoryUsage().rss); maxInFlight = Math.max(maxInFlight, service.activeRequests); }, 100);
  let child;
  const report = { startedAt: new Date().toISOString(), protocolVersion: PROTOCOL_VERSION, contentVersion: CONTENT_VERSION,
    authoritySha256: createHash('sha256').update(readFileSync(resolve(repository, 'Server/AuthorityWorker/out/Emberfield.Authority.dll'))).digest('hex'),
    transport: 'HTTP IPv4 loopback only; not Internet or real-device validation', workers: authority.workerCount, matchesPerWorker: 8,
    progress: [], transportDiagnostics: { droppedAtConnectionLimit: 0, clientErrors: {} },
    authenticationSetup: 'Isolated accounts are provisioned through the real authentication implementation before the timed load; HTTP auth/IP quotas remain unchanged. Re-logins during load use HTTP.' };
  service.server.on('drop', () => report.transportDiagnostics.droppedAtConnectionLimit++);
  service.server.on('clientError', error => {
    const code = error.code ?? error.name;
    report.transportDiagnostics.clientErrors[code] = (report.transportDiagnostics.clientErrors[code] ?? 0) + 1;
  });
  try {
    const listener = await service.listen(0, '127.0.0.1');
    const accounts = [];
    for (let offset = 0; offset < matches * 2; offset += 4) {
      const batch = await Promise.all(Array.from({ length: Math.min(4, matches * 2 - offset) }, async (_, i) => {
        const username = `Load${offset + i}`, password = randomBytes(24).toString('base64url');
        const auth = await service.authenticate({ username, password }, true); return { username, password, token: auth.token };
      })); accounts.push(...batch);
    }
    child = fork(filename, ['--clients'], { cwd: repository, windowsHide: true, stdio: ['ignore', 'inherit', 'inherit', 'ipc'] });
    const result = new Promise((resolveResult, reject) => {
      child.on('message', message => {
        if (message.progress) { report.progress.push(message); console.log(JSON.stringify(message)); }
        if (message.result) resolveResult(message.result);
        if (message.error) reject(new Error(message.error));
      });
      child.on('error', reject); child.on('exit', code => { if (code) reject(new Error('Load client exited ' + code)); });
    });
    child.send({ address: `http://127.0.0.1:${listener.port}`, accounts, durationSeconds, matches });
    report.result = await result;
    assert.equal(authority.assignments.size, 0, 'Terminal worlds must release all authority slots.');
    report.status = 'PASS';
  } catch (error) { report.status = 'FAIL'; report.error = error.stack; process.exitCode = 1; }
  finally {
    child?.kill(); clearInterval(sampler); lag.disable();
    report.serviceEventLoopP95Ms = lag.percentile(95) / 1e6; report.serviceEventLoopMaxMs = lag.max / 1e6;
    report.nodeServicePeakRssBytes = maxRss; report.peakConcurrentHttpRequests = maxInFlight;
    await service.close();
    writeFileSync(resolve(output, 'report.json'), JSON.stringify(report, null, 2) + '\n');
    console.log(JSON.stringify({ status: report.status, report: resolve(output, 'report.json'), result: report.result, error: report.error }, null, 2));
  }
}
if (process.argv.includes('--clients')) process.once('message', config => clients(config).then(result => process.send({ result }, () => process.exit(0))).catch(error => process.send({ error: error.stack }, () => process.exit(1))));
else await orchestrate();
