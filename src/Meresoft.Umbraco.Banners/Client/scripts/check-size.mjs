/* global console, process */
// Fails the build when the package script is 5,120 bytes or more (FR-010).
import { readFileSync, existsSync } from 'node:fs';
import { gzipSync } from 'node:zlib';
import { fileURLToPath } from 'node:url';
import { dirname, resolve } from 'node:path';

const limit = 5120;
const file = resolve(dirname(fileURLToPath(import.meta.url)), '../../wwwroot/meresoft-banners/banners.js');

if (!existsSync(file)) {
    console.error(`banners.js not found at ${file}. Build the script first.`);
    process.exit(1);
}

const content = readFileSync(file);
const gzip = gzipSync(content, { level: 9 }).length;
console.log(`banners.js is ${content.length} bytes (limit ${limit})`);
console.log(`banners.js gzip size is ${gzip} bytes`);

if (content.length >= limit) {
    console.error(`banners.js is ${content.length} bytes, it must be smaller than ${limit} bytes (FR-010).`);
    process.exit(1);
}
