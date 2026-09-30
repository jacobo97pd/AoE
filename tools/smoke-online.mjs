import { spawn, execFile } from 'node:child_process';
import { createWriteStream } from 'node:fs';
import { mkdir, readFile, writeFile, unlink, access, readdir } from 'node:fs/promises';
import { createHash, randomBytes } from 'node:crypto';
import { createServer, isIP } from 'node:net';
import { dirname, resolve, relative, sep } from 'node:path';
import { fileURLToPath } from 'node:url';
import { promisify } from 'node:util';
import { MAPS, isRealm, mapInRealm, defaultMapForRealm } from '../Server/content-realms.mjs';
import { CONTENT_VERSION, PROTOCOL_VERSION } from '../Server/content-version.mjs';

const execFileAsync = promisify(execFile);
const project = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const args = process.argv.slice(2);
function option(name, fallback) {
  const i = args.indexOf(name); if (i < 0) return fallback;
  if (!args[i + 1] || args[i + 1].startsWith('--')) throw new Error(`Missing value for ${name}.`);
  return args[i + 1];
}
const width = Number(option('--width', '1280')), height = Number(option('--height', '720'));
const navalSlice = args.includes('--naval-slice');
const seconds = Number(option('--timeout', navalSlice ? '360' : '240'));
const realmId = option('--realm', 'historical'), mapId = option('--map', defaultMapForRealm(realmId));
const expectedFactions = realmId === 'fantasy' ? ['verdant', 'ashen'] : realmId === 'naval' ? ['pirates', 'pirates'] : ['aven', 'serevin'];
function loopback(host) { return host === 'localhost' || host === '[::1]' || isIP(host) === 4 && host.split('.')[0] === '127'; }
function serverAddress(value) {
  if (!value) return '';
  let url; try { url = new URL(value); } catch { throw new Error('Invalid server address.'); }
  if (url.username || url.password || url.search || url.hash || url.pathname !== '/' ||
      !(url.protocol === 'https:' || url.protocol === 'http:' && loopback(url.hostname)))
    throw new Error('The server must use HTTPS outside loopback, without credentials, path, query or fragment.');
  return url.origin;
}
const configuredServer = serverAddress(option('--server', ''));
if (navalSlice && realmId !== 'naval') throw new Error('The naval slice requires --realm naval.');
if (!isRealm(realmId) || !mapInRealm(mapId, realmId)) throw new Error('Invalid smoke realm or battlefield.');
if (!Number.isInteger(width) || width < 640 || width > 3840 || !Number.isInteger(height) || height < 480 || height > 2160 || !Number.isInteger(seconds) || seconds < 60 || seconds > (navalSlice ? 360 : 240))
  throw new Error('Invalid smoke dimensions or timeout.');
if (process.platform !== 'win32') throw new Error('This runner requires the Windows development player.');
const testRoot = resolve(project, 'TestResults');
const suffix = new Date().toISOString().replace(/[:.]/g, '-') + '-' + randomBytes(3).toString('hex');
const output = resolve(option('--output', resolve(testRoot, `OnlineSmoke-${width}x${height}-${suffix}`)));
if (!output.toLowerCase().startsWith((testRoot + sep).toLowerCase())) throw new Error('Online smoke output must be inside this project TestResults directory.');
await mkdir(output, { recursive: true });
if ((await readdir(output)).length !== 0) throw new Error('Use a new empty smoke output directory. Existing evidence is preserved.');
const sync = resolve(output, 'sync'); await mkdir(sync);
const player = resolve(option('--player', resolve(project, 'Builds/Windows/Emberfield.exe')));
const authority = resolve(project, 'Server/AuthorityWorker/out/Emberfield.Authority.dll');
await access(player); if (!configuredServer) await access(authority);
const started = Date.now(), deadline = started + seconds * 1000;
const children = [], streams = [], configs = [];
let guest, host, service;
const summary = { Schema: 5, NavalSlice: navalSlice, RealmId: realmId, MapId: mapId, BiomeId: MAPS[mapId], ExpectedFactions: expectedFactions, Passed: false, Failure: '', Automated: true, TwoStandaloneClients: true,
  GuestProcessKilledAndRestarted: false, RealAuthority: true, RealHttpAndSqlite: !configuredServer, SimulationAccelerated: false,
  ExternalServer: !!configuredServer, ServerStartedByRunner: !configuredServer, ServerAddress: configuredServer,
  TwoClientsOnSameComputer: true, IndependentPhysicalNetworks: false, ExternalServiceRuntimeNotInspected: !!configuredServer,
  CredentialsExported: false, Width: width, Height: height, BuildGuid: '', MatchId: '', Seconds: 0, Screenshots: [],
  ValidationScope: configuredServer && !loopback(new URL(configuredServer).hostname) ?
    'Two Windows executables on this computer through a configured HTTPS endpoint. Physical computers/networks are not independent; the external service binary and database are not inspected by this runner. This is not human, Android or production-capacity validation.' :
    'Automated loopback Windows processes on this computer. This does not validate remote hosting, independent physical networks, human interaction, Android or production capacity.' };
const delay = ms => new Promise(resolve => setTimeout(resolve, ms));
async function exists(path) { try { await access(path); return true; } catch { return false; } }
async function json(path) { return JSON.parse(await readFile(path, 'utf8')); }
async function waitFor(predicate, label, live = []) {
  while (!(await predicate())) {
    if (Date.now() >= deadline) throw new Error(`Timed out: ${label}.`);
    for (const item of live) if (item?.exited) throw new Error(`${item.label} exited before ${label}. Inspect its local log.`);
    await delay(150);
  }
}
function launch(label, executable, arguments_, options = {}) {
  const child = spawn(executable, arguments_, { cwd: project, windowsHide: true, shell: false, stdio: ['ignore', 'ignore', 'ignore'], ...options });
  const entry = { label, child, exited: false, code: null, error: false };
  entry.closed = new Promise(resolve => {
    child.once('error', () => { entry.error = true; entry.exited = true; resolve(); });
    child.once('exit', code => { entry.code = code; entry.exited = true; resolve(); });
  });
  children.push(entry); return entry;
}
async function stop(entry) {
  if (!entry || entry.exited) return;
  try { await execFileAsync('taskkill.exe', ['/PID', String(entry.child.pid), '/T', '/F'], { windowsHide: true, timeout: 10000 }); }
  catch { try { entry.child.kill('SIGKILL'); } catch { } }
  await Promise.race([entry.closed, delay(5000)]);
}
async function freePort() {
  const socket = createServer();
  await new Promise((resolve, reject) => { socket.once('error', reject); socket.listen(0, '127.0.0.1', resolve); });
  const port = socket.address().port;
  await new Promise(resolve => socket.close(resolve)); return port;
}
async function config(role, address) {
  const folder = resolve(output, role); await mkdir(folder);
  const value = { Role: role, Address: address, Username: `smoke_${role}_${randomBytes(4).toString('hex')}`,
    RealmId: realmId, MapId: mapId, NavalSlice: navalSlice, Password: randomBytes(24).toString('base64url'), SyncDirectory: sync, OutputDirectory: folder, Resume: false, TimeoutSeconds: seconds };
  const path = resolve(sync, `${role}-config.json`); configs.push(path); await writeFile(path, JSON.stringify(value)); return { path, value, folder };
}
function launchPlayer(label, definition) {
  return launch(label, player, ['-batchmode', '-force-d3d11', '-screen-fullscreen', '0', '-screen-width', String(width), '-screen-height', String(height),
    '-logFile', resolve(definition.folder, label + '.log'), '-emberfieldServer', definition.value.Address, '-emberfieldOnlineSmoke', definition.path]);
}
function check(value, message) { if (!value) throw new Error(message); }

try {
  const address = configuredServer || `http://127.0.0.1:${await freePort()}`;
  summary.ServerAddress = address; summary.UsesHttpsEndpoint = new URL(address).protocol === 'https:';
  summary.UsesLoopbackEndpoint = loopback(new URL(address).hostname);
  if (!configuredServer) {
    const serviceOut = createWriteStream(resolve(output, 'service.stdout.log'));
    const serviceError = createWriteStream(resolve(output, 'service.stderr.log')); streams.push(serviceOut, serviceError);
    service = launch('Local service', process.execPath, ['--experimental-sqlite', resolve(project, 'Server/server.mjs')], {
      stdio: ['ignore', 'pipe', 'pipe'], env: { ...process.env, EMBERFIELD_HOST: '127.0.0.1', EMBERFIELD_PORT: String(new URL(address).port), EMBERFIELD_DATABASE: resolve(sync, 'test-accounts.sqlite'),
        EMBERFIELD_TLS_CERT: '', EMBERFIELD_TLS_KEY: '', AUTHORITY_COMMAND: 'dotnet', AUTHORITY_ARGS_JSON: JSON.stringify([authority]) } });
    service.child.stdout.pipe(serviceOut); service.child.stderr.pipe(serviceError);
  }
  await waitFor(async () => {
    let response, health;
    try {
      response = await fetch(address + '/health', { redirect: 'error', signal: AbortSignal.timeout(4000) }); health = await response.json();
    }
    catch { return false; }
    if (response.ok && health.ok && health.status === 'ready') {
      check(health.protocolVersion === PROTOCOL_VERSION && health.contentVersion === CONTENT_VERSION, 'Configured service does not match current protocol/content.');
      summary.ServerProtocolVersion = health.protocolVersion; summary.ServerContentVersion = health.contentVersion; return true;
    }
    return false;
  }, 'compatible service health', [service]);
  const hostConfig = await config('host', address), guestConfig = await config('guest', address);
  console.log(`Online smoke: starting two standalone ${realmId} clients on ${mapId}, using ${configuredServer ? 'the configured existing endpoint' : 'a fresh local authority'}.`);
  host = launchPlayer('host', hostConfig); guest = launchPlayer('guest', guestConfig);
  await waitFor(() => exists(resolve(sync, 'guest-restart-ready.flag')), 'guest movement, gathering, research and completed fortifications', [host, guest, service]);
  const beforeRestart = await json(resolve(sync, 'guest-progress.json'));
  check(beforeRestart.Moved && beforeRestart.Trained && beforeRestart.ResourcesDelivered, 'Guest did not complete real server actions.');
  check(navalSlice ? beforeRestart.ShipTrained && beforeRestart.ShipSailed && beforeRestart.PassengerLanded : beforeRestart.KingdomResearched && beforeRestart.WallRunVerified && beforeRestart.TurnedBuildVerified,
    'Guest did not complete the requested paid gameplay checks before restart.');
  console.log('Online smoke: terminating the guest process and waiting for the host to observe disconnection.');
  await stop(guest); summary.GuestProcessKilledAndRestarted = true;
  await waitFor(() => exists(resolve(sync, 'host-disconnected.flag')), 'host-visible peer disconnection', [host, service]);
  guestConfig.value.Resume = true; await writeFile(guestConfig.path, JSON.stringify(guestConfig.value));
  guest = launchPlayer('guest-resumed', guestConfig);
  console.log('Online smoke: guest relaunched; verifying relogin, state recovery, result and persistent history.');
  await waitFor(async () => await exists(resolve(hostConfig.folder, 'smoke.json')) && await exists(resolve(guestConfig.folder, 'smoke.json')), 'both final reports', [service]);
  await waitFor(() => host.exited && guest.exited, 'normal player shutdown', [service]);
  const hostReport = await json(resolve(hostConfig.folder, 'smoke.json')), guestReport = await json(resolve(guestConfig.folder, 'smoke.json'));
  check(host.code === 0 && guest.code === 0 && hostReport.Passed && guestReport.Passed, 'A standalone player reported a smoke failure.');
  check(hostReport.BuildGuid && hostReport.BuildGuid === guestReport.BuildGuid, 'The two clients did not run the same build.');
  check(hostReport.MatchId && hostReport.MatchId === guestReport.MatchId, 'Clients did not finish the same match.');
  check(hostReport.ServerPlayerId === 1 && guestReport.ServerPlayerId === 2, 'Private room seats were incorrect.');
  check([hostReport, guestReport].every(report => report.RealmId === realmId && report.MapId === mapId && report.BiomeId === MAPS[mapId] && report.RealmAndMapVerified && report.HistoryRealmAndMapVerified), 'Realm, battlefield, biome or filtered history did not match the requested PvP ecosystem.');
  check(hostReport.LocalFactionId === expectedFactions[0] && hostReport.OpponentFactionId === expectedFactions[1] &&
    guestReport.LocalFactionId === expectedFactions[1] && guestReport.OpponentFactionId === expectedFactions[0], 'The actual authoritative factions did not match the requested two players.');
  check([hostReport, guestReport].every(report => report.StarterRosterVerified && (realmId !== 'naval' || report.PlannedFactionsLocked)), 'Faction-specific starters or locked planned naval fleets were not verified.');
  check(hostReport.Replica && guestReport.Replica && hostReport.ReplicaTickDoesNotAdvance && guestReport.ReplicaTickDoesNotAdvance, 'A client did not remain a read-only simulation replica.');
  check(hostReport.PartnerDisconnectedObserved && hostReport.AuthorityAdvancedWhilePeerAbsent && guestReport.Reconnected, 'Process restart/reconnection evidence was incomplete.');
  check(guestReport.ReconnectedTick > beforeRestart.BeforeRestartTick, 'Authority tick did not advance while the guest process was absent.');
  if (navalSlice) {
    check([hostReport, guestReport].every(report => report.ShipTrained && report.ShipSailed && report.PassengerLanded && report.NavalAssetsPersisted &&
      report.DockId > 0 && report.ShipId > 0 && report.PassengerId > 0 && report.DockWoodSpent === 150 && report.ShipWoodSpent === 130),
      'Both native clients must build and pay for a dock/ship, transport a worker and preserve cargo across restart.');
    check(guestReport.DockId === beforeRestart.DockId && guestReport.ShipId === beforeRestart.ShipId && guestReport.PassengerId === beforeRestart.PassengerId,
      'Guest dock, ship or passenger identity changed during reconnect.');
  } else {
  check([hostReport, guestReport].every(report => report.KingdomResearched && report.WallRunVerified && report.TurnedBuildVerified && report.FortificationsPersisted &&
    report.WallRunIds?.length === 2 && new Set([...report.WallRunIds, report.TurnedBuildId]).size === 3 &&
    report.WallRunWidthCells === 1 && report.WallRunDepthCells === 3 && report.WallRunLengthCells === 6 &&
    report.TurnedBuildWidthCells === 1 && report.TurnedBuildDepthCells === 3 &&
    report.WallRunStoneSpent > 0 && report.WallRunStoneSpent === report.TurnedBuildStoneSpent * 2),
    'Both replica clients must verify paid BuildRun and turned BuildCommand footprints, completion and persistence.');
  check(JSON.stringify(guestReport.WallRunIds) === JSON.stringify(beforeRestart.WallRunIds) && guestReport.TurnedBuildId === beforeRestart.TurnedBuildId,
    'Guest reconnection did not retain the same authoritative fortification entity IDs.');
  }
  check(hostReport.HistoryOutcome === 'loss' && guestReport.HistoryOutcome === 'win' && hostReport.StatisticsPresent && guestReport.StatisticsPresent, 'Result history or server statistics did not match the two seats.');
  check(hostReport.ReturnedToLobby && guestReport.ReturnedToLobby, 'The native Return to lobby controls did not reopen both lobbies.');
  check(hostReport.ConnectionChecked && guestReport.ConnectionChecked, 'Native service readiness/version checks were not completed.');
  check(hostReport.ReturnedLobbySelectionPreserved && guestReport.ReturnedLobbySelectionPreserved, 'Returning to the lobby changed the selected PvP realm or battlefield.');
  for (const role of ['host', 'guest']) {
    for (const filename of await readdir(resolve(output, role))) {
      if (filename.endsWith('.png')) summary.Screenshots.push(relative(project, resolve(output, role, filename)).replaceAll('\\', '/'));
      if (filename.endsWith('.log')) {
        const log = await readFile(resolve(output, role, filename), 'utf8');
        check(!/Exception:|Error:|Shader error/i.test(log), 'A player log contained a runtime error.');
        check(!log.includes(hostConfig.value.Password) && !log.includes(guestConfig.value.Password), 'A player log unexpectedly contained a test secret.');
      }
    }
  }
  check(summary.Screenshots.length >= 12, 'Expected native lobby, gameplay, reconnect and result screenshots were missing.');
  summary.Passed = true; summary.BuildGuid = hostReport.BuildGuid; summary.MatchId = hostReport.MatchId;
  summary.Host = hostReport; summary.Guest = guestReport;
  summary.PlayerExeSha256 = createHash('sha256').update(await readFile(player)).digest('hex').toUpperCase();
  if (!configuredServer) summary.AuthoritySha256 = createHash('sha256').update(await readFile(authority)).digest('hex').toUpperCase();
} catch (error) {
  // Only deliberate fixed error messages enter this report. Raw HTTP content and config are excluded.
  summary.Failure = error instanceof Error ? error.message : 'Online smoke failed.';
} finally {
  for (const entry of [...children].reverse()) await stop(entry);
  for (const stream of streams) stream.end();
  // Preserve structured partial client evidence on failure, without ever reading credential configs into the report.
  for (const role of ['host', 'guest']) {
    try {
      const path = resolve(output, role, 'smoke.json');
      if (await exists(path)) summary[role === 'host' ? 'Host' : 'Guest'] = await json(path);
    } catch { /* A missing or incomplete failure artifact must not replace the original runner failure. */ }
  }
  for (const path of configs) { try { await unlink(path); } catch { } }
  summary.Seconds = Math.round((Date.now() - started) / 10) / 100;
  summary.TestCredentialConfigsDeleted = (await Promise.all(configs.map(path => exists(path)))).every(value => !value);
  summary.OwnedProcessesStopped = children.every(entry => entry.exited);
  if (!summary.TestCredentialConfigsDeleted) { summary.Passed = false; summary.Failure = 'Disposable credential configs could not be deleted.'; }
  if (!summary.OwnedProcessesStopped) { summary.Passed = false; summary.Failure = 'A process launched by this smoke did not stop during cleanup.'; }
  await writeFile(resolve(output, 'summary.json'), JSON.stringify(summary, null, 2));
  console.log(`Online smoke: ${summary.Passed ? 'PASS' : 'FAIL'}; report ${relative(project, resolve(output, 'summary.json'))}`);
  if (!summary.Passed) console.error(summary.Failure);
  process.exitCode = summary.Passed ? 0 : 1;
}
