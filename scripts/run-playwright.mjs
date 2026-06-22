import { spawn } from 'node:child_process';
import fs from 'node:fs';
import path from 'node:path';
import process from 'node:process';

const repoRoot = process.cwd();
const playwrightCli = path.join(repoRoot, 'node_modules', '@playwright', 'test', 'cli.js');
const env = { ...process.env };

for (const key of Object.keys(env)) {
  if (key.toUpperCase() === 'ELECTRON_RUN_AS_NODE') {
    delete env[key];
  }
}

function walkFiles(directory) {
  if (!fs.existsSync(directory)) return [];

  const entries = fs.readdirSync(directory, { withFileTypes: true });
  return entries.flatMap((entry) => {
    const fullPath = path.join(directory, entry.name);
    if (entry.isDirectory()) return walkFiles(fullPath);
    return entry.isFile() ? [fullPath] : [];
  });
}

function globToRegExp(pattern) {
  let regex = '^';
  const normalized = pattern.replace(/\\/g, '/');

  for (let index = 0; index < normalized.length; index += 1) {
    const char = normalized[index];
    const next = normalized[index + 1];

    if (char === '*' && next === '*') {
      const after = normalized[index + 2];
      if (after === '/') {
        regex += '(?:.*\\/)?';
        index += 2;
      } else {
        regex += '.*';
        index += 1;
      }
      continue;
    }

    if (char === '*') {
      regex += '[^/]*';
      continue;
    }

    if (char === '?') {
      regex += '[^/]';
      continue;
    }

    if (char === '{') {
      const close = normalized.indexOf('}', index);
      if (close > index) {
        const choices = normalized
          .slice(index + 1, close)
          .split(',')
          .map((value) => value.replace(/[|\\{}()[\]^$+*?.]/g, '\\$&'));
        regex += `(?:${choices.join('|')})`;
        index = close;
        continue;
      }
    }

    regex += char.replace(/[|\\{}()[\]^$+*?.]/g, '\\$&');
  }

  return new RegExp(`${regex}$`);
}

function expandGlobArg(arg) {
  if (!/[{}*?]/.test(arg)) return [arg];

  const normalized = arg.replace(/\\/g, '/');
  const baseSegments = [];
  for (const segment of normalized.split('/')) {
    if (/[{}*?]/.test(segment)) break;
    baseSegments.push(segment);
  }

  const baseDir = path.join(repoRoot, ...baseSegments);
  const matcher = globToRegExp(normalized);
  const matches = walkFiles(baseDir)
    .map((file) => path.relative(repoRoot, file).replace(/\\/g, '/'))
    .filter((file) => matcher.test(file))
    .sort();

  return matches.length > 0 ? matches : [arg];
}

const playwrightArgs = process.argv.slice(2).flatMap(expandGlobArg);

const child = spawn(process.execPath, [playwrightCli, ...playwrightArgs], {
  cwd: repoRoot,
  env,
  stdio: 'inherit',
});

child.on('exit', (code, signal) => {
  if (signal) {
    console.error(`Playwright exited from signal ${signal}`);
    process.exit(1);
  }

  process.exit(code ?? 1);
});
