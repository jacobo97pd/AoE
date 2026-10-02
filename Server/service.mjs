import { createServer as createHttpServer } from 'node:http';
import { createServer as createHttpsServer } from 'node:https';
import { randomBytes, randomUUID, createHash, scrypt as scryptCallback, timingSafeEqual } from 'node:crypto';
import { promisify } from 'node:util';
import { isIP } from 'node:net';
import { AccountDatabase } from './database.mjs';
import { CONTENT_VERSION, PROTOCOL_VERSION } from './content-version.mjs';
import { FACTION_REALMS, REALMS, mapInRealm, defaultMapForRealm, factionInRealm, ratingKey } from './content-realms.mjs';
import { loadCatalog, validateCatalog } from './cosmetics.mjs';

const scrypt = promisify(scryptCallback);
const hashToken = token => createHash('sha256').update(token).digest('hex');
const FACTIONS = new Set(Object.keys(FACTION_REALMS));
const MODES = new Set(['Conquest', 'Dominion']);
const QUEUES = new Set(['casual', 'ranked']);
const TERMINAL_REASONS = new Set(['Conquest', 'Dominion', 'Surrender', 'SimultaneousElimination', 'SimultaneousDominion', 'disconnect', 'afk', 'surrender']);
const STATISTICS = ['eraTier', 'workersRemaining', 'armyRemaining', 'buildingsRemaining', 'food', 'wood', 'metal', 'stone', 'technologiesCompleted'];
const ACTIVE = new Set(['lobby', 'starting', 'active']);
const errorResponse = (status, message) => Object.assign(new Error(message), { status });

export function clientAddress(req, trustCloudflareLoopback = false) {
  const peer = req.socket.remoteAddress ?? 'unknown';
  const ipv4 = peer.toLowerCase().startsWith('::ffff:') ? peer.slice(7) : peer;
  const loopback = peer === '::1' || isIP(ipv4) === 4 && ipv4.startsWith('127.');
  const forwarded = req.headers['cf-connecting-ip'];
  return trustCloudflareLoopback && loopback && typeof forwarded === 'string' && forwarded.length <= 64 && isIP(forwarded)
    ? forwarded : peer;
}

export class RateLimiter {
  constructor(now = Date.now) { this.now = now; this.entries = new Map(); }
  allow(key, limit, windowMs) {
    const now = this.now(); let entry = this.entries.get(key);
    if (!entry || now >= entry.until) {
      if (this.entries.size >= 10000) {
        for (const [id, item] of this.entries) if (now >= item.until) this.entries.delete(id);
        if (this.entries.size >= 10000) return false;
      }
      entry = { count: 0, until: now + windowMs }; this.entries.set(key, entry);
    }
    return ++entry.count <= limit;
  }
}

export class EmberfieldService {
  constructor(options) {
    this.now = options.now ?? Date.now;
    this.cosmetics = options.cosmeticCatalog ? validateCatalog(options.cosmeticCatalog) : loadCatalog();
    this.db = options.database ?? new AccountDatabase(options.databasePath ?? ':memory:', this.now);
    this.authority = options.authority;
    this.sandboxCosmetics = options.sandboxCosmetics === true;
    this.trustCloudflareLoopback = options.trustCloudflareLoopback === true;
    this.verifyPurchase = options.verifyPurchase ?? null;
    this.protocolVersion = options.protocolVersion ?? PROTOCOL_VERSION;
    this.contentVersion = options.contentVersion ?? CONTENT_VERSION;
    this.maxRooms = options.maxRooms ?? this.authority.capacity ?? 16;
    if (!Number.isInteger(this.maxRooms) || this.maxRooms < 1 || this.maxRooms > 256) throw new Error('maxRooms must be between 1 and 256.');
    this.maxRequests = Math.max(128, this.maxRooms * 8); this.activeRequests = 0;
    this.sessionMs = options.sessionMs ?? 12 * 60 * 60 * 1000;
    this.connectedMs = options.connectedMs ?? 15000;
    this.reconnectMs = options.reconnectMs ?? 60000;
    this.afkMs = options.afkMs ?? 180000;
    this.lobbyMs = options.lobbyMs ?? 15 * 60 * 1000;
    this.rooms = new Map(); this.userRooms = new Map(); this.queued = new Map();
    this.rate = new RateLimiter(this.now); this.authWork = 0; this.authQueue = []; this.maintenanceRunning = false; this.closed = false;
    this.authority.on('result', result => this.acceptResult(result));
    this.authority.on('unavailable', (_reason, matchIds) => {
      for (const room of this.rooms.values()) if ((room.status === 'active' || room.status === 'starting') &&
          (!Array.isArray(matchIds) || matchIds.includes(room.matchId))) this.abortRoom(room, 'authority_failure');
    });
    const listener = (req, res) => this.handle(req, res);
    this.server = options.tls ? createHttpsServer(options.tls, listener) : createHttpServer(listener);
    this.server.requestTimeout = 15000; this.server.headersTimeout = 10000; this.server.keepAliveTimeout = 5000;
    this.server.maxHeadersCount = 30; this.server.maxConnections = this.maxRequests;
    this.server.on('clientError', (_error, socket) => socket.end('HTTP/1.1 400 Bad Request\r\nConnection: close\r\n\r\n'));
    if (options.maintenance !== false) this.timer = setInterval(() => this.maintenance().catch(() => {}), 1000);
  }
  listen(port = 8787, host = '127.0.0.1') {
    return new Promise((resolve, reject) => { this.server.once('error', reject); this.server.listen(port, host, () => { this.server.off('error', reject); resolve(this.server.address()); }); });
  }
  async close() {
    if (this.closed) return; this.closed = true; clearInterval(this.timer);
    for (const waiting of this.authQueue.splice(0)) { clearTimeout(waiting.timer); waiting.reject(errorResponse(503, 'service_unavailable')); }
    for (const room of this.rooms.values()) if (room.status === 'active' || room.status === 'starting') this.abortRoom(room, 'server_shutdown');
    this.authority.close();
    await new Promise(resolve => { this.server.close(resolve); this.server.closeAllConnections(); });
    this.db.close();
  }
  write(res, status, body) {
    if (res.destroyed) return;
    res.writeHead(status, { 'Content-Type': 'application/json; charset=utf-8', 'Cache-Control': 'no-store', 'X-Content-Type-Options': 'nosniff' });
    res.end(JSON.stringify(body));
  }
  async body(req) {
    if (!String(req.headers['content-type'] ?? '').toLowerCase().startsWith('application/json')) throw errorResponse(415, 'application_json_required');
    if (req.headers['content-encoding'] && req.headers['content-encoding'] !== 'identity') throw errorResponse(415, 'content_encoding_unsupported');
    if (Number(req.headers['content-length']) > 65536) throw errorResponse(413, 'request_too_large');
    let bytes = 0; const chunks = [];
    for await (const chunk of req) {
      bytes += chunk.length;
      if (bytes > 65536) throw errorResponse(413, 'request_too_large');
      chunks.push(chunk);
    }
    let value;
    try { value = JSON.parse(Buffer.concat(chunks).toString('utf8')); } catch { throw errorResponse(400, 'invalid_json'); }
    if (!value || typeof value !== 'object' || Array.isArray(value)) throw errorResponse(400, 'object_required');
    return value;
  }
  bearer(req) {
    const match = /^Bearer ([A-Za-z0-9_-]{43})$/.exec(req.headers.authorization ?? '');
    if (!match) throw errorResponse(401, 'authentication_required');
    const tokenHash = hashToken(match[1]); const account = this.db.session(tokenHash);
    if (!account) throw errorResponse(401, 'session_expired');
    return { account, tokenHash };
  }
  async handle(req, res) {
    if (this.closed) return this.write(res, 503, { ok: false, error: 'service_unavailable' });
    if (this.activeRequests >= this.maxRequests) return this.write(res, 503, { ok: false, error: 'service_busy' });
    this.activeRequests++;
    try {
      const path = new URL(req.url, 'http://localhost').pathname;
      const ip = clientAddress(req, this.trustCloudflareLoopback);
      const authenticatedRoute = path.startsWith('/v1/') && path !== '/v1/register' && path !== '/v1/login';
      if (!authenticatedRoute && !this.rate.allow(`public:${ip}`, 1200, 60000)) throw errorResponse(429, 'request_rate_limited');
      if (req.method === 'GET' && path === '/health') return this.write(res, 200, { ok: true, protocolVersion: this.protocolVersion, contentVersion: this.contentVersion, status: this.authority.dead ? 'unavailable' : 'ready' });
      if (path.startsWith('/v1/') && (req.headers['x-emberfield-protocol'] !== String(this.protocolVersion) || req.headers['x-emberfield-content'] !== this.contentVersion))
        { if (!this.rate.allow(`invalid:${ip}`, 120, 60000)) throw errorResponse(429, 'request_rate_limited'); throw errorResponse(426, 'client_version_mismatch'); }
      if (req.method === 'POST' && (path === '/v1/register' || path === '/v1/login')) {
        if (!this.rate.allow(`auth:${ip}`, 12, 60000)) throw errorResponse(429, 'authentication_rate_limited');
        return this.write(res, 200, await this.authenticate(await this.body(req), path.endsWith('register')));
      }
      let identity;
      try { identity = this.bearer(req); }
      catch (error) { if (!this.rate.allow(`invalid:${ip}`, 120, 60000)) throw errorResponse(429, 'request_rate_limited'); throw error; }
      const { account, tokenHash } = identity;
      if (!this.rate.allow(`account:${account.id}`, 900, 60000)) throw errorResponse(429, 'account_rate_limited');
      let answer;
      if (req.method === 'GET' && path === '/v1/profile') answer = { profile: this.db.profile(account.id) };
      else if (req.method === 'GET' && path === '/v1/cosmetics') answer = this.wardrobe(account.id);
      else if (req.method === 'POST' && path === '/v1/cosmetics/equip') answer = this.equipCosmetic(account.id, await this.body(req));
      else if (req.method === 'POST' && path === '/v1/cosmetics/sandbox-claim') answer = await this.claimCosmetic(account.id, await this.body(req), true);
      else if (req.method === 'POST' && path === '/v1/cosmetics/purchase') answer = await this.claimCosmetic(account.id, await this.body(req), false);
      else if (req.method === 'GET' && REALMS.some(realm => path === `/v1/history/${realm}`)) answer = { matches: this.db.history(account.id, 25, path.split('/').pop()) };
      else if (req.method === 'GET' && path === '/v1/history') answer = { matches: this.db.history(account.id) };
      else if (req.method === 'GET' && path === '/v1/state') { this.heartbeat(account.id); answer = this.state(account.id); }
      else if (req.method === 'POST' && path === '/v1/heartbeat') { await this.body(req); this.heartbeat(account.id); answer = this.state(account.id); }
      else if (req.method === 'POST' && path === '/v1/logout') { await this.body(req); this.db.logout(tokenHash); this.queued.delete(account.id); answer = {}; }
      else if (req.method === 'POST' && path === '/v1/rooms') answer = this.createRoom(account, await this.body(req));
      else if (req.method === 'POST' && path === '/v1/rooms/join') answer = this.joinRoom(account, await this.body(req));
      else if (req.method === 'POST' && path === '/v1/rooms/ready') answer = await this.ready(account.id, await this.body(req));
      else if (req.method === 'POST' && path === '/v1/rooms/leave') { await this.body(req); answer = this.leave(account.id); }
      else if (req.method === 'POST' && path === '/v1/queue') answer = this.enqueue(account, await this.body(req));
      else if (req.method === 'DELETE' && path === '/v1/queue') { this.queued.delete(account.id); answer = this.state(account.id); }
      else if (req.method === 'GET' && path === '/v1/matches/current/snapshot') answer = await this.snapshot(account.id);
      else if (req.method === 'POST' && path === '/v1/matches/current/commands') {
        if (!this.rate.allow(`commands:${account.id}`, 20, 1000)) throw errorResponse(429, 'command_rate_limited');
        answer = await this.command(account.id, await this.body(req));
      }
      else if (req.method === 'POST' && path === '/v1/surrender') { await this.body(req); answer = await this.surrender(account.id); }
      else throw errorResponse(404, 'route_not_found');
      this.write(res, 200, { ok: true, error: '', ...answer });
    } catch (error) {
      const status = Number.isInteger(error.status) ? error.status : 503;
      this.write(res, status, { ok: false, error: status === 503 ? 'service_unavailable' : error.message });
    } finally { this.activeRequests--; }
  }
  async authenticate(input, registration) {
    if (typeof input.username !== 'string' || !/^[A-Za-z0-9_]{3,24}$/.test(input.username) ||
        typeof input.password !== 'string' || Buffer.byteLength(input.password) < 10 || Buffer.byteLength(input.password) > 128)
      throw errorResponse(400, 'username_3_to_24_letters_digits_underscore_password_10_to_128_bytes');
    await this.acquireAuthentication();
    try {
      const existing = this.db.accountByName(input.username);
      const salt = existing?.salt ?? randomBytes(16).toString('hex');
      const derived = await scrypt(input.password, salt, 64, { N: 32768, r: 8, p: 1, maxmem: 64 * 1024 * 1024 });
      if (this.closed) throw errorResponse(503, 'service_unavailable');
      let account = existing;
      if (registration) {
        if (existing) throw errorResponse(409, 'username_unavailable');
        try { account = this.db.createAccount(randomUUID(), input.username, salt, derived.toString('hex')); }
        catch { throw errorResponse(409, 'username_unavailable'); }
      } else if (!existing || !timingSafeEqual(derived, Buffer.from(existing.password_hash, 'hex'))) throw errorResponse(401, 'invalid_credentials');
      const token = randomBytes(32).toString('base64url');
      this.db.createSession(hashToken(token), account.id, this.now() + this.sessionMs);
      this.heartbeat(account.id);
      return { ok: true, error: '', token, profile: this.db.profile(account.id) };
    } finally { this.releaseAuthentication(); }
  }
  acquireAuthentication() {
    if (this.closed) return Promise.reject(errorResponse(503, 'service_unavailable'));
    if (this.authWork < 4) { this.authWork++; return Promise.resolve(); }
    if (this.authQueue.length >= 8) return Promise.reject(errorResponse(429, 'authentication_busy'));
    return new Promise((resolve, reject) => {
      const waiting = { resolve, reject, timer: null };
      waiting.timer = setTimeout(() => {
        const index = this.authQueue.indexOf(waiting); if (index >= 0) this.authQueue.splice(index, 1);
        reject(errorResponse(429, 'authentication_busy'));
      }, 2000);
      this.authQueue.push(waiting);
    });
  }
  releaseAuthentication() {
    const waiting = this.authQueue.shift();
    if (waiting) { clearTimeout(waiting.timer); waiting.resolve(); }
    else this.authWork--;
  }
  validateChoice(input) {
    input.realmId ??= 'historical'; input.mapId ??= defaultMapForRealm(input.realmId);
    if (!mapInRealm(input.mapId, input.realmId)) throw errorResponse(400, 'unsupported_realm_or_map');
    if (!factionInRealm(input.factionId, input.realmId)) throw errorResponse(400, 'faction_realm_mismatch');
    if (!MODES.has(input.mode)) throw errorResponse(400, 'unsupported_faction_or_mode');
  }
  wardrobe(accountId) {
    const state = this.db.wardrobe(accountId);
    return { items: this.cosmetics, ...state, sandboxEnabled: this.sandboxCosmetics, purchasesAvailable: typeof this.verifyPurchase === 'function' };
  }
  cosmetic(input) {
    const item = this.cosmetics.find(item => item.id === input.itemId);
    if (!item) throw errorResponse(400, 'unknown_cosmetic');
    return item;
  }
  equipCosmetic(accountId, input) {
    const item = this.cosmetic(input);
    if (!this.db.ownsCosmetic(accountId, item.id)) throw errorResponse(403, 'cosmetic_not_owned');
    this.db.equipCosmetic(accountId, item);
    return this.wardrobe(accountId);
  }
  async claimCosmetic(accountId, input, sandbox) {
    const item = this.cosmetic(input);
    if (typeof input.idempotencyKey !== 'string' || !/^[A-Za-z0-9_-]{8,80}$/.test(input.idempotencyKey)) throw errorResponse(400, 'invalid_purchase_key');
    const source = sandbox ? 'sandbox' : 'purchase';
    const previous = this.db.cosmeticClaim(accountId, input.idempotencyKey);
    if (previous) {
      if (previous.item_id !== item.id || previous.source !== source) throw errorResponse(409, 'purchase_key_conflict');
      return this.wardrobe(accountId);
    }
    let provider = 'alpha-sandbox', transactionId = `${accountId}:${input.idempotencyKey}`;
    if (sandbox) {
      if (!this.sandboxCosmetics) throw errorResponse(403, 'sandbox_disabled');
    } else {
      if (typeof this.verifyPurchase !== 'function') throw errorResponse(501, 'payment_provider_unconfigured');
      if (!this.rate.allow(`purchases:${accountId}`, 10, 60000)) throw errorResponse(429, 'purchase_rate_limited');
      if (typeof input.receipt !== 'string' || input.receipt.length < 1 || input.receipt.length > 16000) throw errorResponse(400, 'invalid_purchase_receipt');
      // The provider adapter must verify server-to-server and bind the store transaction to this account and product.
      // Client prices, ownership booleans, provider IDs and transaction IDs are never accepted as proof.
      const receipt = await this.verifyPurchase({ accountId, item: { ...item }, receipt: input.receipt });
      if (!receipt || receipt.verified !== true || receipt.accountId !== accountId || receipt.itemId !== item.id ||
        typeof receipt.provider !== 'string' || !/^[a-z0-9_-]{1,40}$/.test(receipt.provider) ||
        typeof receipt.transactionId !== 'string' || receipt.transactionId.length < 1 || receipt.transactionId.length > 200)
        throw errorResponse(400, 'invalid_purchase_receipt');
      provider = receipt.provider; transactionId = receipt.transactionId;
    }
    const outcome = this.db.grantCosmetic(accountId, item.id, input.idempotencyKey, source, provider, transactionId);
    if (!outcome) throw errorResponse(409, 'purchase_receipt_already_used');
    return this.wardrobe(accountId);
  }
  ensureFree(accountId) {
    if (this.queued.has(accountId)) throw errorResponse(409, 'already_queued');
    const room = this.userRooms.get(accountId);
    if (room && ACTIVE.has(room.status)) throw errorResponse(409, 'already_in_room');
    if (room) this.leave(accountId);
    if (this.authority.dead && !this.authority.canRecover) throw errorResponse(503, 'authority_unavailable');
  }
  newPlayer(account, factionId, slot) {
    return { accountId: account.id, username: account.username, factionId, playerId: slot, ready: false, cosmetics: this.db.wardrobe(account.id).equipped.filter(e => { const item = this.cosmetics.find(c => c.id === e.itemId); return item && (item.realmId === 'shared' || item.realmId === FACTION_REALMS[factionId]) && (item.slot !== 'character' || item.factionId === factionId); }),
      lastSeen: this.now(), lastCommand: this.now(), sequence: 0, recentRequestIds: new Set(), commandBusy: false, snapshotPromise: null, snapshotAt: 0, cachedObservation: null };
  }
  createRoom(account, input) {
    this.validateChoice(input); this.ensureFree(account.id);
    if (this.occupiedRooms() >= this.maxRooms) throw errorResponse(503, 'room_capacity_reached');
    let code; do { code = randomBytes(5).toString('hex').toUpperCase(); } while (this.rooms.has(code));
    const room = { code, matchId: '', queue: 'private', mode: input.mode, realmId: input.realmId, mapId: input.mapId, status: 'lobby', createdAt: this.now(), players: [this.newPlayer(account, input.factionId, 1)], result: null };
    this.rooms.set(code, room); this.userRooms.set(account.id, room);
    return this.state(account.id);
  }
  joinRoom(account, input) {
    this.ensureFree(account.id);
    if (typeof input.code !== 'string' || !/^[A-Fa-f0-9]{10}$/.test(input.code) || !FACTIONS.has(input.factionId)) throw errorResponse(400, 'invalid_room_or_faction');
    if (!this.rate.allow(`join:${account.id}`, 20, 60000)) throw errorResponse(429, 'join_rate_limited');
    const room = this.rooms.get(input.code.toUpperCase());
    if (!room || room.status !== 'lobby' || room.players.length !== 1) throw errorResponse(404, 'room_unavailable');
    if ((input.realmId ?? 'historical') !== room.realmId || !factionInRealm(input.factionId, room.realmId)) throw errorResponse(409, 'faction_realm_mismatch');
    room.players.push(this.newPlayer(account, input.factionId, 2)); this.userRooms.set(account.id, room);
    return this.state(account.id);
  }
  async ready(accountId, input) {
    const { room, player } = this.seat(accountId, ['lobby']);
    if (typeof input.ready !== 'boolean') throw errorResponse(400, 'ready_boolean_required');
    player.ready = input.ready; this.heartbeat(accountId);
    if (room.players.length === 2 && room.players.every(p => p.ready)) await this.startRoom(room);
    return this.state(accountId);
  }
  async startRoom(room) {
    if (room.status !== 'lobby') return;
    if (!mapInRealm(room.mapId, room.realmId) || room.players.length !== 2 || room.players.some(p => !factionInRealm(p.factionId, room.realmId))) throw errorResponse(400, 'faction_realm_mismatch');
    room.status = 'starting'; room.matchId = randomUUID();
    try {
      this.db.startMatch(room, this.contentVersion);
      const response = await this.authority.request({ op: 'create', matchId: room.matchId, mode: room.mode, realmId: room.realmId, mapId: room.mapId,
        players: room.players.map(p => ({ slot: p.playerId, factionId: p.factionId })) });
      if (!response.ok || room.status !== 'starting') throw new Error('authority_create_failed');
      room.status = 'active'; room.startedAt = this.now();
      for (const player of room.players) { player.lastSeen = this.now(); player.lastCommand = this.now(); }
    } catch { this.abortRoom(room, 'authority_failure'); }
  }
  seat(accountId, statuses = ['active', 'finished']) {
    const room = this.userRooms.get(accountId);
    if (!room || !statuses.includes(room.status)) throw errorResponse(409, 'no_current_match');
    const player = room.players.find(p => p.accountId === accountId);
    if (!player) throw errorResponse(403, 'not_a_participant');
    return { room, player };
  }
  heartbeat(accountId) {
    const room = this.userRooms.get(accountId);
    const player = room?.players.find(p => p.accountId === accountId);
    if (player) player.lastSeen = this.now();
    const queued = this.queued.get(accountId); if (queued) queued.lastSeen = this.now();
  }
  state(accountId) {
    const room = this.userRooms.get(accountId); const queued = this.queued.get(accountId);
    const player = room?.players.find(p => p.accountId === accountId);
    return { status: queued ? 'queued' : room?.status ?? 'idle', roomCode: room?.code ?? '', matchId: room?.matchId ?? '',
      realmId: queued?.realmId ?? room?.realmId ?? 'historical', mapId: queued?.mapId ?? room?.mapId ?? 'amber_crossing',
      queue: queued?.queue ?? room?.queue ?? '', mode: queued?.mode ?? room?.mode ?? 'Conquest', playerId: player?.playerId ?? 0,
      lastAcceptedSequence: player?.sequence ?? 0, queueSeconds: queued ? Math.max(0, Math.floor((this.now() - queued.since) / 1000)) : 0,
      reconnectGraceSeconds: this.reconnectMs / 1000, afkTimeoutSeconds: this.afkMs / 1000,
      afkSecondsRemaining: player && room.status === 'active' ? Math.max(0, Math.ceil((this.afkMs - (this.now() - player.lastCommand)) / 1000)) : 0,
      players: room?.players.map(p => ({ playerId: p.playerId, username: p.username, factionId: p.factionId, cosmetics: p.cosmetics, ready: p.ready, connected: this.now() - p.lastSeen <= this.connectedMs })) ?? [],
      result: room?.result ?? null };
  }
  leave(accountId) {
    this.queued.delete(accountId);
    const room = this.userRooms.get(accountId);
    if (!room) return this.state(accountId);
    if (room.status === 'active' || room.status === 'starting') throw errorResponse(409, 'surrender_before_leaving_active_match');
    this.userRooms.delete(accountId);
    if (room.status === 'lobby') {
      room.players = room.players.filter(p => p.accountId !== accountId);
      room.players.forEach((p, i) => { p.playerId = i + 1; p.ready = false; });
    }
    if (![...this.userRooms.values()].includes(room)) { this.rooms.delete(room.code); this.authority.request({ op: 'close', matchId: room.matchId }).catch(() => {}); }
    return this.state(accountId);
  }
  enqueue(account, input) {
    this.validateChoice(input); this.ensureFree(account.id);
    if (!QUEUES.has(input.queue)) throw errorResponse(400, 'unsupported_queue');
    if (this.queued.size >= 128) throw errorResponse(503, 'queue_capacity_reached');
    this.queued.set(account.id, { account, queue: input.queue, mode: input.mode, realmId: input.realmId, mapId: input.mapId, factionId: input.factionId, since: this.now(), lastSeen: this.now(), mmr: this.db.rating(account.id, ratingKey(input.realmId, input.queue, input.mode)).mmr });
    return this.state(account.id);
  }
  async snapshot(accountId) {
    const { room, player } = this.seat(accountId);
    this.heartbeat(accountId);
    if (room.finalizing) await room.finalizing;
    if (room.authorityClosed) {
      if (!player.cachedObservation) throw new Error('snapshot_unavailable');
      return { observation: player.cachedObservation, lastAcceptedSequence: player.sequence, state: this.state(accountId) };
    }
    if (!player.snapshotPromise && (!player.cachedObservation || this.now() - player.snapshotAt >= 100)) {
      player.snapshotPromise = this.authority.request({ op: 'snapshot', matchId: room.matchId, playerId: player.playerId }).then(response => {
        if (!response.ok || !response.observation) throw new Error('snapshot_unavailable');
        if (Buffer.byteLength(JSON.stringify(response.observation)) > 4 * 1024 * 1024) throw new Error('snapshot_limit');
        player.cachedObservation = response.observation; player.snapshotAt = this.now();
      }).finally(() => { player.snapshotPromise = null; });
    }
    if (player.snapshotPromise) await player.snapshotPromise;
    return { observation: player.cachedObservation, lastAcceptedSequence: player.sequence, state: this.state(accountId) };
  }
  async command(accountId, input) {
    const { room, player } = this.seat(accountId, ['active']);
    if (!Number.isSafeInteger(input.sequence) || input.sequence < 1 || input.sequence !== player.sequence + 1) throw errorResponse(409, `sequence_expected_${player.sequence + 1}`);
    if (!input.command || typeof input.command !== 'object' || Array.isArray(input.command) || input.command.Sequence !== input.sequence) throw errorResponse(400, 'command_sequence_mismatch');
    if (player.commandBusy) throw errorResponse(409, 'command_pending');
    // Consume exactly once even if gameplay validation rejects the command. A timed-out request cannot be replayed.
    player.sequence = input.sequence; player.commandBusy = true; this.heartbeat(accountId);
    try {
      const requestId = input.command.RequestId;
      if (typeof requestId !== 'string' || !/^[A-Za-z0-9_-]{1,64}$/.test(requestId))
        return { accepted: false, error: 'invalid_request_id', lastAcceptedSequence: player.sequence };
      if (player.recentRequestIds.has(requestId))
        return { accepted: false, error: 'request_id_repeated', lastAcceptedSequence: player.sequence };
      player.recentRequestIds.add(requestId);
      if (player.recentRequestIds.size > 2048) player.recentRequestIds.delete(player.recentRequestIds.values().next().value);
      const response = await this.authority.request({ op: 'command', matchId: room.matchId, playerId: player.playerId, command: input.command });
      if (!response.ok) return { accepted: false, error: typeof response.error === 'string' ? response.error.slice(0, 160) : 'command_rejected', lastAcceptedSequence: player.sequence };
      if (response.accepted) player.lastCommand = this.now();
      return { accepted: response.accepted === true, error: typeof response.error === 'string' ? response.error.slice(0, 160) : '', lastAcceptedSequence: player.sequence };
    } finally { player.commandBusy = false; }
  }
  async surrender(accountId) {
    const { room, player } = this.seat(accountId, ['active']);
    await this.forfeit(room, player, 'surrender'); return this.state(accountId);
  }
  async forfeit(room, player, reason) {
    if (room.finishing || room.status !== 'active') return;
    room.finishing = true; room.finishingAt = this.now();
    try {
      const response = await this.authority.request({ op: 'forfeit', matchId: room.matchId, playerId: player.playerId, reason });
      if (!response.ok) this.abortRoom(room, 'authority_failure');
    } catch { this.abortRoom(room, 'authority_failure'); }
  }
  acceptResult(input) {
    const room = [...this.rooms.values()].find(r => r.matchId === input.matchId);
    if (!room || room.status !== 'active') return false;
    if (input.reason === 'authority_failure') { this.abortRoom(room, 'authority_failure'); return false; }
    if (![0, 1, 2].includes(input.winnerPlayerId) || !TERMINAL_REASONS.has(input.reason) ||
        !Number.isSafeInteger(input.tick) || input.tick < 0 || input.tick > 1728000 ||
        !Number.isFinite(input.durationSeconds) || input.durationSeconds < 0 || input.durationSeconds > 86400) return false;
    const players = room.players.map(p => {
      const source = (Array.isArray(input.players) ? input.players.find(item => item?.playerId === p.playerId)?.statistics : null) ?? {};
      const statistics = {};
      for (const key of STATISTICS) if (Number.isSafeInteger(source[key]) && source[key] >= 0 && source[key] <= 2147483647) statistics[key] = source[key];
      return { playerId: p.playerId, statistics };
    });
    const result = { winnerPlayerId: input.winnerPlayerId, reason: input.reason, durationSeconds: input.durationSeconds, tick: input.tick, players };
    try {
      if (!this.db.finishMatch(room.matchId, result)) return false;
      room.status = 'finished'; room.result = result; room.finishedAt = this.now();
      room.finalizing = this.releaseFinishedAuthority(room);
      return true;
    } catch { this.abortRoom(room, 'result_persistence_failure'); return false; }
  }
  abortRoom(room, reason) {
    if (room.status === 'finished' || room.status === 'aborted') return;
    if (room.matchId) this.db.abortMatch(room.matchId, reason);
    room.status = 'aborted'; room.finishedAt = this.now();
    room.result = { winnerPlayerId: 0, reason, durationSeconds: 0, tick: 0, players: [] };
    if (room.matchId) this.authority.request({ op: 'close', matchId: room.matchId }).catch(() => {});
  }
  occupiedRooms() { return [...this.rooms.values()].filter(room => ACTIVE.has(room.status) || room.finalizing).length; }
  pruneTerminalRooms() {
    // History is durable; full final observations are a bounded, temporary reconnect cache.
    if (this.rooms.size <= this.maxRooms * 2) return;
    const terminal = [...this.rooms.values()].filter(room => !ACTIVE.has(room.status) && !room.finalizing)
      .sort((a, b) => a.finishedAt - b.finishedAt);
    for (const room of terminal) {
      if (this.rooms.size <= this.maxRooms * 2) break;
      for (const player of room.players) if (this.userRooms.get(player.accountId) === room) this.userRooms.delete(player.accountId);
      this.rooms.delete(room.code);
    }
  }
  async releaseFinishedAuthority(room) {
    try {
      // An older in-flight snapshot must settle before the immutable final views replace it.
      await Promise.allSettled(room.players.map(player => player.snapshotPromise));
      await Promise.all(room.players.map(async player => {
        const response = await this.authority.request({ op: 'snapshot', matchId: room.matchId, playerId: player.playerId });
        if (!response.ok || !response.observation) throw new Error('snapshot_unavailable');
        if (Buffer.byteLength(JSON.stringify(response.observation)) > 4 * 1024 * 1024) throw new Error('snapshot_limit');
        player.cachedObservation = response.observation; player.snapshotAt = this.now();
      }));
    } catch { for (const player of room.players) player.cachedObservation = null; }
    finally {
      await this.authority.request({ op: 'close', matchId: room.matchId }).catch(() => {});
      room.authorityClosed = true; room.finalizing = null;
      this.pruneTerminalRooms();
    }
  }
  async maintenance() {
    if (this.maintenanceRunning || this.closed) return; this.maintenanceRunning = true;
    try {
      const now = this.now();
      for (const [id, entry] of this.queued) if (now - entry.lastSeen > this.reconnectMs) this.queued.delete(id);
      for (const room of [...this.rooms.values()]) {
        if (room.status === 'active') {
          if (room.finishing && now - room.finishingAt > 15000) { this.abortRoom(room, 'authority_failure'); continue; }
          const lost = room.players.filter(p => now - p.lastSeen > this.connectedMs + this.reconnectMs);
          if (lost.length === 2) this.abortRoom(room, 'both_disconnected');
          else if (lost.length === 1) await this.forfeit(room, lost[0], 'disconnect');
          else {
            const afk = room.players.filter(p => now - p.lastCommand >= this.afkMs);
            if (afk.length === 2) this.abortRoom(room, 'both_afk');
            else if (afk.length === 1) await this.forfeit(room, afk[0], 'afk');
          }
        }
        if (room.status === 'lobby' && now - Math.max(...room.players.map(p => p.lastSeen)) > this.lobbyMs) this.abortRoom(room, 'lobby_expired');
        if ((room.status === 'finished' || room.status === 'aborted') && now - room.finishedAt > 10 * 60 * 1000) {
          for (const player of room.players) if (this.userRooms.get(player.accountId) === room) this.userRooms.delete(player.accountId);
          this.rooms.delete(room.code); this.authority.request({ op: 'close', matchId: room.matchId }).catch(() => {});
        }
      }
      const entries = [...this.queued.values()].sort((a, b) => a.since - b.since);
      for (const first of entries) {
        if (!this.queued.has(first.account.id)) continue;
        if (this.occupiedRooms() >= this.maxRooms) break;
        const second = entries.find(other => other !== first && this.queued.has(other.account.id) && first.queue === other.queue && first.mode === other.mode && first.realmId === other.realmId && first.mapId === other.mapId &&
          Math.abs(first.mmr - other.mmr) <= Math.min(1200, 100 + Math.floor(Math.max(now - first.since, now - other.since) / 10000) * 50));
        if (!second) continue;
        this.queued.delete(first.account.id); this.queued.delete(second.account.id);
        const code = randomBytes(5).toString('hex').toUpperCase();
        const room = { code, matchId: '', queue: first.queue, mode: first.mode, realmId: first.realmId, mapId: first.mapId, status: 'lobby', createdAt: now,
          players: [this.newPlayer(first.account, first.factionId, 1), this.newPlayer(second.account, second.factionId, 2)], result: null };
        for (const p of room.players) { p.ready = true; this.userRooms.set(p.accountId, room); }
        this.rooms.set(code, room); await this.startRoom(room);
      }
      this.db.pruneSessions();
      this.pruneTerminalRooms();
    } finally { this.maintenanceRunning = false; }
  }
}
