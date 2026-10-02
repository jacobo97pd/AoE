import { createHash } from 'node:crypto';
import { readFileSync, readdirSync } from 'node:fs';
import { resolve } from 'node:path';

export const PROTOCOL_VERSION = 2;
export const CONTENT_VERSION = 'frontiers-9c7e44558027b01361399113d1b2b4686c833370fe9c9eb69c08769453332b75';
export function calculateContent(repository) {
  const hash = createHash('sha256');
  const paths = ['Assets/Game/Resources/Definitions/greybox.json'];
  for (const directory of ['Assets/Game/Simulation', 'Assets/Game/Networking', 'Assets/Game/Resources/Maps'])
    for (const name of readdirSync(resolve(repository, directory)).sort())
      if ((name.endsWith('.cs') && name !== 'NetworkBuild.cs') || name.endsWith('.json')) paths.push(`${directory}/${name}`);
  for (const path of paths.sort()) {
    hash.update(path + '\0'); hash.update(readFileSync(resolve(repository, path), 'utf8').replace(/\r\n/g, '\n')); hash.update('\0');
  }
  return `frontiers-${hash.digest('hex')}`;
}
export function verifyContent(repository) {
  const actual = calculateContent(repository);
  if (actual !== CONTENT_VERSION) throw new Error('Content version does not match the pinned server build. Rebuild and version the client/server together.');
  return actual;
}
