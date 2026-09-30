import { readFileSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { AuthorityPool } from './authority-pool.mjs';
import { EmberfieldService } from './service.mjs';
import { verifyContent } from './content-version.mjs';

const serverDirectory = dirname(fileURLToPath(import.meta.url));
const repository = resolve(serverDirectory, '..');
verifyContent(repository);
const host = process.env.EMBERFIELD_HOST ?? '127.0.0.1';
const port = Number(process.env.EMBERFIELD_PORT ?? 8787);
if (!Number.isInteger(port) || port < 1 || port > 65535) throw new Error('EMBERFIELD_PORT must be between 1 and 65535.');
let tls;
if (process.env.EMBERFIELD_TLS_CERT && process.env.EMBERFIELD_TLS_KEY) tls = {
  cert: readFileSync(process.env.EMBERFIELD_TLS_CERT), key: readFileSync(process.env.EMBERFIELD_TLS_KEY), minVersion: 'TLSv1.2',
};
if (!['127.0.0.1', '::1', 'localhost'].includes(host) && !tls)
  throw new Error('External listening requires EMBERFIELD_TLS_CERT and EMBERFIELD_TLS_KEY. Default loopback HTTP is for local testing.');
const workerCommand = process.env.AUTHORITY_COMMAND ?? 'dotnet';
const workerArgs = process.env.AUTHORITY_ARGS_JSON ? JSON.parse(process.env.AUTHORITY_ARGS_JSON) : [resolve(serverDirectory, 'AuthorityWorker/out/Emberfield.Authority.dll')];
if (!Array.isArray(workerArgs) || workerArgs.some(arg => typeof arg !== 'string')) throw new Error('AUTHORITY_ARGS_JSON must be a JSON string array.');
const authority = new AuthorityPool(workerCommand, workerArgs, { cwd: repository,
  workerCount: Number(process.env.EMBERFIELD_AUTHORITY_WORKERS ?? 4),
  matchesPerWorker: Number(process.env.EMBERFIELD_MATCHES_PER_WORKER ?? 8) });
let service;
try {
  service = new EmberfieldService({ authority, sandboxCosmetics: process.env.EMBERFIELD_COSMETIC_SANDBOX === '1',
    trustCloudflareLoopback: process.env.EMBERFIELD_TRUST_CLOUDFLARE_LOOPBACK === '1',
    databasePath: process.env.EMBERFIELD_DATABASE ?? resolve(serverDirectory, 'data/emberfield.sqlite'), tls });
  await service.listen(port, host);
} catch (error) {
  if (service) await service.close(); else authority.close();
  throw error;
}
console.log(`Emberfield alpha service ready at ${tls ? 'https' : 'http'}://${host}:${port}; protocol ${service.protocolVersion}; content ${service.contentVersion}; capacity ${authority.capacity} matches across ${authority.workerCount} workers.`);
let stopping = false;
async function stop() { if (stopping) return; stopping = true; await service.close(); process.exitCode = 0; }
process.on('SIGINT', stop); process.on('SIGTERM', stop);
