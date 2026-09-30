import { spawn } from 'node:child_process';
import { EventEmitter } from 'node:events';

// Only a trusted local child can submit results. Public HTTP has no result-write route.
export class AuthorityBridge extends EventEmitter {
  constructor(command, args, options = {}) {
    super();
    this.pending = new Map(); this.nextId = 1; this.buffer = ''; this.dead = false;
    this.timeoutMs = options.timeoutMs ?? 10000;
    this.child = spawn(command, args, { windowsHide: true, shell: false, stdio: ['pipe', 'pipe', 'pipe'], cwd: options.cwd });
    this.child.stdout.setEncoding('utf8');
    this.child.stdout.on('data', chunk => this.consume(chunk));
    // Intentionally omit raw stderr: it may include local paths or request content.
    this.child.stderr.resume();
    this.child.on('error', () => this.fail('authority_unavailable'));
    this.child.on('exit', () => this.fail('authority_stopped'));
    this.child.stdin.on('error', () => this.fail('authority_unavailable'));
  }
  consume(chunk) {
    this.buffer += chunk;
    if (this.buffer.length > 8 * 1024 * 1024) return this.fail('authority_protocol_limit');
    let newline;
    while ((newline = this.buffer.indexOf('\n')) >= 0) {
      const line = this.buffer.slice(0, newline); this.buffer = this.buffer.slice(newline + 1);
      if (!line.trim()) continue;
      let message;
      try { message = JSON.parse(line); } catch { return this.fail('authority_protocol_error'); }
      if (!message || typeof message !== 'object' || Array.isArray(message)) return this.fail('authority_protocol_error');
      if (message.event === 'result') this.emit('result', message);
      else if (Number.isSafeInteger(message.requestId) && this.pending.has(message.requestId)) {
        const entry = this.pending.get(message.requestId); this.pending.delete(message.requestId); clearTimeout(entry.timer);
        entry.resolve(message);
      }
    }
  }
  request(message) {
    if (this.dead) return Promise.reject(new Error('authority_unavailable'));
    if (this.pending.size >= 256) return Promise.reject(new Error('authority_busy'));
    const requestId = this.nextId++;
    return new Promise((resolve, reject) => {
      const timer = setTimeout(() => { this.pending.delete(requestId); reject(new Error('authority_timeout')); }, this.timeoutMs);
      this.pending.set(requestId, { resolve, reject, timer });
      this.child.stdin.write(JSON.stringify({ ...message, requestId }) + '\n', error => { if (error) this.fail('authority_unavailable'); });
    });
  }
  fail(reason) {
    if (this.dead) return;
    this.dead = true;
    for (const entry of this.pending.values()) { clearTimeout(entry.timer); entry.reject(new Error(reason)); }
    this.pending.clear(); this.child.kill(); this.emit('unavailable', reason);
  }
  close() { this.fail('server_shutdown'); }
}
