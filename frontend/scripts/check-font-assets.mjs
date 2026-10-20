import assert from 'node:assert/strict';
import { existsSync, readFileSync, readdirSync } from 'node:fs';
import { dirname, resolve } from 'node:path';

// Check the emitted application, including critical CSS in index.html.
const output = resolve(process.argv[2] ?? 'dist/frontend/browser');
const files = readdirSync(output).filter((name) => name.endsWith('.css') || name === 'index.html');
assert(files.includes('index.html'), 'Build output must contain index.html');
let faces = 0;
let combined = '';
for (const name of files) {
  const path = resolve(output, name);
  const content = readFileSync(path, 'utf8');
  combined += content;
  assert(!/fonts\.(googleapis|gstatic)\.com/i.test(content), `${name} references Google Fonts`);
  for (const face of content.matchAll(/@font-face\s*\{([^}]+)\}/g)) {
    faces++;
    const urls = [...face[1].matchAll(/url\(\s*['"]?([^\s'"\)]+)['"]?\s*\)/g)];
    assert(urls.length > 0, `${name} contains a font without a bundled source`);
    for (const [, url] of urls) {
      assert(!/^(?:[a-z]+:|\/\/)/i.test(url), `Font must be local: ${url}`);
      assert(
        existsSync(resolve(dirname(path), decodeURIComponent(url.split(/[?#]/)[0]))),
        `Missing font: ${url}`,
      );
    }
  }
}
assert(faces >= 4, 'Expected Roboto weights 300/400/500 and Material Icons');
assert(/font-family:\s*['"]?Roboto/i.test(combined), 'Roboto font is missing');
assert(/font-family:\s*['"]?Material Icons/i.test(combined), 'Material Icons font is missing');
for (const license of ['Roboto-OFL.txt', 'Material-Icons-OFL.txt']) {
  assert(existsSync(resolve(output, 'licenses', license)), `Missing font license: ${license}`);
}
console.log('Font assets and licenses are bundled locally; no Google Fonts references.');
