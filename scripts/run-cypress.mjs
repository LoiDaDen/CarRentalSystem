import { spawn } from 'node:child_process';
import path from 'node:path';
import process from 'node:process';

const repoRoot = process.cwd();
const cypressBin = path.join(repoRoot, 'node_modules', 'cypress', 'bin', 'cypress');
const env = { ...process.env };

for (const key of Object.keys(env)) {
  if (key.toUpperCase() === 'ELECTRON_RUN_AS_NODE') {
    delete env[key];
  }
}

const child = spawn(process.execPath, [cypressBin, ...process.argv.slice(2)], {
  cwd: repoRoot,
  env,
  stdio: 'inherit',
});

child.on('exit', (code, signal) => {
  if (signal) {
    console.error(`Cypress exited from signal ${signal}`);
    process.exit(1);
  }

  process.exit(code ?? 1);
});
