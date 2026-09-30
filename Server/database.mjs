import { DatabaseSync } from 'node:sqlite';
import { mkdirSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import { acquireDatabaseLock } from './database-lock.mjs';
import { ratingKey } from './content-realms.mjs';

export const RANK_BANDS = Object.freeze([
  [0, 'Bronze'], [100, 'Silver'], [250, 'Gold'], [450, 'Platinum'],
  [700, 'Diamond'], [1000, 'Master'], [1400, 'Legend'],
]);
export const visibleRank = points => [...RANK_BANDS].reverse().find(([floor]) => points >= floor)[1];

export class AccountDatabase {
  constructor(path, now = Date.now) {
    this.now = now;
    if (path !== ':memory:') {
      path = resolve(path); mkdirSync(dirname(path), { recursive: true });
      this.releaseLock = acquireDatabaseLock(path);
    }
    try { this.db = new DatabaseSync(path); }
    catch (error) { this.releaseLock?.(); throw error; }
    this.db.exec(`PRAGMA journal_mode=WAL; PRAGMA foreign_keys=ON; PRAGMA busy_timeout=3000;
      CREATE TABLE IF NOT EXISTS accounts (
        id TEXT PRIMARY KEY, username TEXT NOT NULL UNIQUE COLLATE NOCASE,
        salt TEXT NOT NULL, password_hash TEXT NOT NULL, created_at INTEGER NOT NULL
      ) STRICT;
      CREATE TABLE IF NOT EXISTS sessions (
        token_hash TEXT PRIMARY KEY, account_id TEXT NOT NULL REFERENCES accounts(id), expires_at INTEGER NOT NULL
      ) STRICT;
      CREATE INDEX IF NOT EXISTS sessions_account ON sessions(account_id);
      CREATE TABLE IF NOT EXISTS ratings (
        account_id TEXT NOT NULL REFERENCES accounts(id), queue_key TEXT NOT NULL,
        mmr INTEGER NOT NULL DEFAULT 1000, rank_points INTEGER NOT NULL DEFAULT 0,
        wins INTEGER NOT NULL DEFAULT 0, losses INTEGER NOT NULL DEFAULT 0, draws INTEGER NOT NULL DEFAULT 0,
        PRIMARY KEY(account_id, queue_key)
      ) STRICT;
      CREATE TABLE IF NOT EXISTS matches (
        id TEXT PRIMARY KEY, queue TEXT NOT NULL, mode TEXT NOT NULL, content_version TEXT NOT NULL,
        status TEXT NOT NULL, started_at INTEGER NOT NULL, ended_at INTEGER,
        winner_slot INTEGER NOT NULL DEFAULT 0, reason TEXT NOT NULL DEFAULT '',
        duration_seconds REAL NOT NULL DEFAULT 0, tick INTEGER NOT NULL DEFAULT 0
      ) STRICT;
      CREATE TABLE IF NOT EXISTS participants (
        match_id TEXT NOT NULL REFERENCES matches(id), account_id TEXT NOT NULL REFERENCES accounts(id),
        slot INTEGER NOT NULL, faction_id TEXT NOT NULL, statistics_json TEXT NOT NULL DEFAULT '{}',
        rank_points_before INTEGER NOT NULL DEFAULT 0, rank_points_after INTEGER NOT NULL DEFAULT 0,
        PRIMARY KEY(match_id, account_id), UNIQUE(match_id, slot)
      ) STRICT;
      CREATE INDEX IF NOT EXISTS participant_account ON participants(account_id);
      CREATE TABLE IF NOT EXISTS cosmetic_entitlements (
        account_id TEXT NOT NULL REFERENCES accounts(id), item_id TEXT NOT NULL, source TEXT NOT NULL, granted_at INTEGER NOT NULL,
        PRIMARY KEY(account_id,item_id)
      ) STRICT;
      CREATE TABLE IF NOT EXISTS cosmetic_claims (
        account_id TEXT NOT NULL REFERENCES accounts(id), idempotency_key TEXT NOT NULL, item_id TEXT NOT NULL,
        source TEXT NOT NULL, provider TEXT NOT NULL, transaction_id TEXT NOT NULL,
        PRIMARY KEY(account_id,idempotency_key), UNIQUE(provider,transaction_id)
      ) STRICT;
      CREATE TABLE IF NOT EXISTS cosmetic_equipment (
        account_id TEXT NOT NULL REFERENCES accounts(id), slot TEXT NOT NULL, target_id TEXT NOT NULL, item_id TEXT NOT NULL,
        PRIMARY KEY(account_id,slot,target_id), FOREIGN KEY(account_id,item_id) REFERENCES cosmetic_entitlements(account_id,item_id)
      ) STRICT;`);
    this.transaction(() => {
      const columns = new Set(this.db.prepare('PRAGMA table_info(matches)').all().map(c => c.name));
      if (!columns.has('realm_id')) this.db.exec("ALTER TABLE matches ADD COLUMN realm_id TEXT NOT NULL DEFAULT 'historical'");
      if (!columns.has('map_id')) this.db.exec("ALTER TABLE matches ADD COLUMN map_id TEXT NOT NULL DEFAULT 'amber_crossing'");
      if (this.db.prepare('PRAGMA user_version').get().user_version < 3) {
        // Only pre-realm keys migrate. Never reinterpret naval/future namespaces
        // or overwrite an existing scoped rating if a partial legacy import exists.
        const migrate = this.db.prepare(`UPDATE ratings SET queue_key=? WHERE queue_key=? AND NOT EXISTS
          (SELECT 1 FROM ratings scoped WHERE scoped.account_id=ratings.account_id AND scoped.queue_key=?)`);
        for (const key of ['casual:Conquest', 'casual:Dominion', 'ranked:Conquest', 'ranked:Dominion'])
          migrate.run('historical:' + key, key, 'historical:' + key);
        this.db.exec('PRAGMA user_version=3');
      }
    });
    // A restarted process has no authoritative World for an old active match. Never award a result for it.
    this.db.prepare("UPDATE matches SET status='aborted',reason='server_restart',ended_at=? WHERE status='active'").run(now());
    this.pruneSessions();
  }
  close() { try { this.db.close(); } finally { this.releaseLock?.(); } }
  transaction(action) {
    this.db.exec('BEGIN IMMEDIATE');
    try { const value = action(); this.db.exec('COMMIT'); return value; }
    catch (error) { this.db.exec('ROLLBACK'); throw error; }
  }
  accountByName(username) { return this.db.prepare('SELECT * FROM accounts WHERE username=?').get(username); }
  accountById(id) { return this.db.prepare('SELECT * FROM accounts WHERE id=?').get(id); }
  createAccount(id, username, salt, passwordHash) {
    this.db.prepare('INSERT INTO accounts VALUES(?,?,?,?,?)').run(id, username, salt, passwordHash, this.now());
    return this.accountById(id);
  }
  createSession(hash, accountId, expiresAt) {
    this.pruneSessions();
    // Bound concurrent sessions without retaining the bearer secret itself.
    this.db.prepare('DELETE FROM sessions WHERE account_id=? AND token_hash NOT IN (SELECT token_hash FROM sessions WHERE account_id=? ORDER BY expires_at DESC LIMIT 3)').run(accountId, accountId);
    this.db.prepare('INSERT INTO sessions VALUES(?,?,?)').run(hash, accountId, expiresAt);
  }
  session(hash) { return this.db.prepare('SELECT a.* FROM sessions s JOIN accounts a ON a.id=s.account_id WHERE s.token_hash=? AND s.expires_at>?').get(hash, this.now()); }
  logout(hash) { this.db.prepare('DELETE FROM sessions WHERE token_hash=?').run(hash); }
  pruneSessions() { this.db.prepare('DELETE FROM sessions WHERE expires_at<=?').run(this.now()); }
  rating(accountId, key) {
    return this.db.prepare('SELECT * FROM ratings WHERE account_id=? AND queue_key=?').get(accountId, key)
      ?? { account_id: accountId, queue_key: key, mmr: 1000, rank_points: 0, wins: 0, losses: 0, draws: 0 };
  }
  profile(accountId) {
    const account = this.accountById(accountId);
    const ratings = this.db.prepare('SELECT * FROM ratings WHERE account_id=? ORDER BY queue_key').all(accountId);
    return { accountId, username: account.username, createdAt: account.created_at,
      ratings: ratings.map(r => ({ queue: r.queue_key, realmId: r.queue_key.split(':')[0], visibleRank: visibleRank(r.rank_points), rankPoints: r.rank_points, wins: r.wins, losses: r.losses, draws: r.draws })) };
  }
  startMatch(room, contentVersion) {
    this.transaction(() => {
      this.db.prepare("INSERT INTO matches(id,queue,mode,content_version,status,started_at,realm_id,map_id) VALUES(?,?,?,?,'active',?,?,?)")
        .run(room.matchId, room.queue, room.mode, contentVersion, this.now(), room.realmId ?? 'historical', room.mapId ?? 'amber_crossing');
      for (const player of room.players) this.db.prepare('INSERT INTO participants(match_id,account_id,slot,faction_id) VALUES(?,?,?,?)')
        .run(room.matchId, player.accountId, player.playerId, player.factionId);
    });
  }
  abortMatch(matchId, reason) {
    return this.db.prepare("UPDATE matches SET status='aborted',reason=?,ended_at=? WHERE id=? AND status='active'").run(reason, this.now(), matchId).changes > 0;
  }
  finishMatch(matchId, result) {
    return this.transaction(() => {
      const match = this.db.prepare('SELECT * FROM matches WHERE id=?').get(matchId);
      if (!match || match.status !== 'active') return false;
      const participants = this.db.prepare('SELECT * FROM participants WHERE match_id=? ORDER BY slot').all(matchId);
      if (participants.length !== 2) throw new Error('Invalid authority participants');
      this.db.prepare("UPDATE matches SET status='finished',ended_at=?,winner_slot=?,reason=?,duration_seconds=?,tick=? WHERE id=?")
        .run(this.now(), result.winnerPlayerId, result.reason, result.durationSeconds, result.tick, matchId);
      const key = ratingKey(match.realm_id, match.queue, match.mode);
      const before = participants.map(p => this.rating(p.account_id, key));
      participants.forEach((player, i) => {
        const outcome = result.winnerPlayerId === 0 ? 0.5 : result.winnerPlayerId === player.slot ? 1 : 0;
        const rating = before[i];
        let points = rating.rank_points;
        if (match.queue !== 'private') {
          const expected = 1 / (1 + 10 ** ((before[1 - i].mmr - rating.mmr) / 400));
          const mmr = Math.max(100, Math.min(4000, rating.mmr + Math.round(32 * (outcome - expected))));
          if (match.queue === 'ranked') points = Math.max(0, points + (outcome === 1 ? 25 : outcome === 0 ? -15 : 0));
          this.db.prepare(`INSERT INTO ratings VALUES(?,?,?,?,?,?,?) ON CONFLICT(account_id,queue_key) DO UPDATE SET
            mmr=excluded.mmr,rank_points=excluded.rank_points,wins=excluded.wins,losses=excluded.losses,draws=excluded.draws`)
            .run(player.account_id, key, mmr, points, rating.wins + (outcome === 1 ? 1 : 0), rating.losses + (outcome === 0 ? 1 : 0), rating.draws + (outcome === 0.5 ? 1 : 0));
        }
        const stats = result.players?.find(p => p.playerId === player.slot)?.statistics ?? {};
        this.db.prepare('UPDATE participants SET statistics_json=?,rank_points_before=?,rank_points_after=? WHERE match_id=? AND slot=?')
          .run(JSON.stringify(stats), rating.rank_points, points, matchId, player.slot);
      });
      return true;
    });
  }
  wardrobe(accountId) {
    return {
      ownedIds: this.db.prepare('SELECT item_id FROM cosmetic_entitlements WHERE account_id=? ORDER BY item_id').all(accountId).map(r => r.item_id),
      entitlements: this.db.prepare('SELECT item_id AS itemId,source FROM cosmetic_entitlements WHERE account_id=? ORDER BY item_id').all(accountId),
      equipped: this.db.prepare('SELECT slot,target_id AS targetId,item_id AS itemId FROM cosmetic_equipment WHERE account_id=? ORDER BY slot,target_id').all(accountId)
    };
  }
  ownsCosmetic(accountId, itemId) { return !!this.db.prepare('SELECT 1 FROM cosmetic_entitlements WHERE account_id=? AND item_id=?').get(accountId, itemId); }
  cosmeticClaim(accountId, key) { return this.db.prepare('SELECT * FROM cosmetic_claims WHERE account_id=? AND idempotency_key=?').get(accountId, key); }
  grantCosmetic(accountId, itemId, key, source, provider, transactionId) {
    return this.transaction(() => {
      const previous = this.cosmeticClaim(accountId, key);
      if (previous) return previous.item_id === itemId && previous.source === source;
      if (this.db.prepare('SELECT 1 FROM cosmetic_claims WHERE provider=? AND transaction_id=?').get(provider, transactionId)) return false;
      this.db.prepare('INSERT INTO cosmetic_claims VALUES(?,?,?,?,?,?)').run(accountId, key, itemId, source, provider, transactionId);
      this.db.prepare("INSERT INTO cosmetic_entitlements VALUES(?,?,?,?) ON CONFLICT(account_id,item_id) DO UPDATE SET source=CASE WHEN excluded.source='purchase' THEN excluded.source ELSE cosmetic_entitlements.source END").run(accountId, itemId, source, this.now());
      return true;
    });
  }
  equipCosmetic(accountId, item) {
    this.db.prepare('INSERT INTO cosmetic_equipment VALUES(?,?,?,?) ON CONFLICT(account_id,slot,target_id) DO UPDATE SET item_id=excluded.item_id').run(accountId, item.slot, item.targetId, item.id);
  }
  history(accountId, limit = 25, realmId = null) {
    return this.db.prepare(`SELECT m.*,p.slot,p.faction_id,p.statistics_json,p.rank_points_before,p.rank_points_after,
      a.username AS opponent FROM matches m JOIN participants p ON p.match_id=m.id
      JOIN participants other ON other.match_id=m.id AND other.account_id<>p.account_id
      JOIN accounts a ON a.id=other.account_id WHERE p.account_id=? AND m.status<>'active' AND (? IS NULL OR m.realm_id=?)
      ORDER BY m.started_at DESC,m.id DESC LIMIT ?`).all(accountId, realmId, realmId, limit).map(row => ({
      matchId: row.id, realmId: row.realm_id, mapId: row.map_id, queue: row.queue, mode: row.mode, contentVersion: row.content_version, status: row.status,
      startedAt: row.started_at, endedAt: row.ended_at, winnerPlayerId: row.winner_slot, playerId: row.slot,
      outcome: row.status === 'aborted' ? 'aborted' : row.winner_slot === 0 ? 'draw' : row.winner_slot === row.slot ? 'win' : 'loss',
      reason: row.reason, durationSeconds: row.duration_seconds, tick: row.tick, factionId: row.faction_id,
      opponent: row.opponent, statistics: JSON.parse(row.statistics_json), rankPointsBefore: row.rank_points_before, rankPointsAfter: row.rank_points_after,
    }));
  }
}
