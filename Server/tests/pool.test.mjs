import test from 'node:test';
import assert from 'node:assert/strict';
import { EventEmitter } from 'node:events';
import { AuthorityPool } from '../authority-pool.mjs';

class Bridge extends EventEmitter {
  dead = false; requests = [];
  async request(message) { this.requests.push(message); return { ok: true, observation: { shard: this.index } }; }
  close() { this.dead = true; this.emit('unavailable', 'stopped'); }
}
test('pool pins 32 matches to bounded shards and rejects a 33rd; closing frees capacity', async () => {
  const children = [];
  const pool = new AuthorityPool('', [], { workerCount: 4, matchesPerWorker: 8, bridgeFactory: index => {
    const child = new Bridge(); child.index = index; children.push(child); return child;
  } });
  try {
    for (let i = 0; i < 32; i++) assert.equal((await pool.request({ op: 'create', matchId: 'match-' + i })).ok, true);
    assert.deepEqual(pool.workers.map(worker => worker.matches.size), [8, 8, 8, 8]);
    assert.equal((await pool.request({ op: 'create', matchId: 'overflow' })).ok, false);
    for (let i = 0; i < 32; i++) assert.equal((await pool.request({ op: 'snapshot', matchId: 'match-' + i })).observation.shard, i % 4);
    await pool.request({ op: 'close', matchId: 'match-0' });
    assert.equal((await pool.request({ op: 'create', matchId: 'replacement' })).ok, true);
    assert.equal((await pool.request({ op: 'snapshot', matchId: 'replacement' })).observation.shard, 0);
  } finally { pool.close(); }
});
test('a crashed shard reports only its own matches, cannot forge another shard result, and restarts on admission', async () => {
  const pool = new AuthorityPool('', [], { workerCount: 2, matchesPerWorker: 2, bridgeFactory: index => { const child = new Bridge(); child.index = index; return child; } });
  try {
    await pool.request({ op: 'create', matchId: 'one' }); await pool.request({ op: 'create', matchId: 'two' });
    const results = [], failures = [];
    pool.on('result', result => results.push(result)); pool.on('unavailable', (_reason, ids) => failures.push(ids));
    pool.workers[0].bridge.emit('result', { matchId: 'two' }); assert.equal(results.length, 0);
    pool.workers[1].bridge.emit('result', { matchId: 'two' }); assert.equal(results.length, 1);
    const old = pool.workers[0]; old.bridge.close();
    assert.deepEqual(failures, [['one']]); assert.equal(pool.dead, false);
    assert.equal((await pool.request({ op: 'snapshot', matchId: 'two' })).ok, true);
    assert.equal((await pool.request({ op: 'create', matchId: 'three' })).ok, true);
    assert.notEqual(pool.workers[0], old); assert.equal(pool.assignments.get('three').index, 0);
  } finally { pool.close(); }
});
