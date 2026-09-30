import { readFileSync, writeFileSync } from 'node:fs';
import { resolve, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';
import { calculateContent, PROTOCOL_VERSION } from './content-version.mjs';
const repository = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const version = calculateContent(repository);
for (const relative of ['Server/content-version.mjs', 'Assets/Game/Networking/NetworkBuild.cs']) {
  const path = resolve(repository, relative);
  const updated = readFileSync(path, 'utf8')
    .replace(/(CONTENT_VERSION = ')[^']+/, `$1${version}`)
    .replace(/(ContentVersion = ")[^"]+/, `$1${version}`)
    .replace(/(ProtocolVersion = )\d+/, `$1${PROTOCOL_VERSION}`);
  writeFileSync(path, updated);
}
process.stdout.write(`Pinned protocol ${PROTOCOL_VERSION} and content ${version}\n`);
