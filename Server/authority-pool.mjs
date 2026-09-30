import { EventEmitter } from 'node:events';
import { AuthorityBridge } from './authority-bridge.mjs';

// Each match has one process owner. A failed process cannot terminate other shards.
export class AuthorityPool extends EventEmitter {
  constructor(command, args, options = {}) {
    super();
    this.workerCount = options.workerCount ?? 4;
    this.matchesPerWorker = options.matchesPerWorker ?? 8;
    if (!Number.isInteger(this.workerCount) || this.workerCount < 1 || this.workerCount > 16 ||
        !Number.isInteger(this.matchesPerWorker) || this.matchesPerWorker < 1 || this.matchesPerWorker > 16)
      throw new Error('Authority pool requires 1-16 workers and 1-16 matches per worker.');
    this.capacity = this.workerCount * this.matchesPerWorker;
    this.command = command; this.args = args; this.options = options;
    this.assignments = new Map(); this.workers = []; this.closed = false;
    for (let index = 0; index < this.workerCount; index++) this.workers.push(this.startWorker(index));
  }
  get dead() { return this.closed || this.workers.every(worker => worker.bridge.dead); }
  get canRecover() { return !this.closed; }
  startWorker(index) {
    const bridge = this.options.bridgeFactory?.(index) ?? new AuthorityBridge(this.command, this.args, this.options);
    const worker = { index, bridge, matches: new Set() };
    bridge.on('result', result => {
      if (this.assignments.get(result.matchId) === worker) this.emit('result', result);
    });
    bridge.on('unavailable', reason => {
      const matchIds = [...worker.matches];
      for (const matchId of matchIds) this.assignments.delete(matchId);
      worker.matches.clear();
      this.emit('unavailable', reason, matchIds);
    });
    return worker;
  }
  async request(message) {
    if (this.closed) throw new Error('authority_unavailable');
    let worker = this.assignments.get(message.matchId);
    if (message.op === 'create') {
      if (worker) return { ok: false, error: 'match_already_exists' };
      // Restart a failed child on admission, rather than creating an unbounded restart loop.
      for (let index = 0; index < this.workers.length; index++)
        if (this.workers[index].bridge.dead) this.workers[index] = this.startWorker(index);
      worker = this.workers.filter(item => !item.bridge.dead && item.matches.size < this.matchesPerWorker)
        .sort((a, b) => a.matches.size - b.matches.size || a.index - b.index)[0];
      if (!worker) return { ok: false, error: 'authority_capacity_reached' };
      this.assignments.set(message.matchId, worker); worker.matches.add(message.matchId);
      try {
        const reply = await worker.bridge.request(message);
        if (!reply.ok) this.release(message.matchId, worker);
        return reply;
      } catch (error) {
        // A timeout may have created a World whose acknowledgement was lost. Kill only this shard
        // rather than forget its ownership and leave an untracked simulation occupying capacity.
        if (typeof worker.bridge.fail === 'function') worker.bridge.fail('authority_create_uncertain');
        else worker.bridge.close();
        this.release(message.matchId, worker); throw error;
      }
    }
    if (!worker) return { ok: message.op === 'close', error: message.op === 'close' ? '' : 'unknown_match' };
    if (message.op !== 'close') return worker.bridge.request(message);
    // Keep the slot reserved until its close is acknowledged, preventing over-admission.
    try { return await worker.bridge.request(message); }
    catch (error) {
      if (typeof worker.bridge.fail === 'function') worker.bridge.fail('authority_close_uncertain');
      else worker.bridge.close();
      throw error;
    }
    finally { this.release(message.matchId, worker); }
  }
  release(matchId, worker) {
    if (this.assignments.get(matchId) === worker) this.assignments.delete(matchId);
    worker.matches.delete(matchId);
  }
  close() {
    if (this.closed) return;
    this.closed = true;
    for (const worker of this.workers) worker.bridge.close();
    this.assignments.clear();
  }
}
