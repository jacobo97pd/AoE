import { openSync, closeSync, writeFileSync, readFileSync, unlinkSync, mkdirSync, rmdirSync } from 'node:fs';

// A second process must not run startup recovery against the first process's live matches.
export function acquireDatabaseLock(databasePath) {
  const path = `${databasePath}.lock`;
  let handle;
  for (let attempt = 0; attempt < 2; attempt++) {
    try { handle = openSync(path, 'wx', 0o600); break; }
    catch (error) {
      if (error.code !== 'EEXIST') throw error;
      // Serialize stale-lock reclamation, so two simultaneous restarts cannot unlink one another's new lock.
      const recovery = `${path}.recovery`;
      try { mkdirSync(recovery); }
      catch { throw new Error('Database lock recovery is in progress; retry after the other startup finishes.'); }
      try {
        let owner;
        try { owner = JSON.parse(readFileSync(path, 'utf8')); }
        catch { throw new Error('Database lock is unreadable; verify no service is running before removing its .lock file.'); }
        if (!Number.isSafeInteger(owner.pid) || owner.pid < 1) throw new Error('Invalid database lock; operator inspection required.');
        let alive = true;
        try { process.kill(owner.pid, 0); } catch (probe) { if (probe.code === 'ESRCH') alive = false; }
        if (alive) throw new Error('This database is already owned by a running service. Use a separate database for another instance.');
        unlinkSync(path);
        try { handle = openSync(path, 'wx', 0o600); }
        catch { throw new Error('Another service acquired the database during recovery.'); }
      } finally { rmdirSync(recovery); }
      if (handle !== undefined) break;
    }
  }
  if (handle === undefined) throw new Error('Could not acquire the database lock.');
  const nonce = `${process.pid}:${Date.now()}:${Math.random()}`;
  writeFileSync(handle, JSON.stringify({ pid: process.pid, nonce }), 'utf8');
  let released = false;
  return () => {
    if (released) return; released = true; closeSync(handle);
    // Do not remove a lock replaced by an operator or another process.
    try { if (JSON.parse(readFileSync(path, 'utf8')).nonce === nonce) unlinkSync(path); } catch { }
  };
}
