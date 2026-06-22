import { spawn } from 'node:child_process';
import path from 'node:path';
import process from 'node:process';

const repoRoot = process.cwd();
const playwrightCli = path.join(repoRoot, 'node_modules', '@playwright', 'test', 'cli.js');
const mode = (process.argv[2] || 'full').toLowerCase();

const specByMode = {
  preflight: 'playwright/e2e/production/00-prod-preflight.spec.js',
  smoke: 'playwright/e2e/production/{00-prod-preflight,01-prod-public-smoke}.spec.js',
  full: 'playwright/e2e/production/**/*.spec.js',
  cleanup: 'playwright/e2e/production/99-prod-cleanup.spec.js',
};

if (!Object.prototype.hasOwnProperty.call(specByMode, mode)) {
  console.error(`Unknown production Playwright mode "${mode}". Use preflight, smoke, full, or cleanup.`);
  process.exit(1);
}

const requiredEnv = [
  'PLAYWRIGHT_BASE_URL',
  'PLAYWRIGHT_PROD_ADMIN_EMAIL',
  'PLAYWRIGHT_PROD_ADMIN_PASSWORD',
  'PLAYWRIGHT_PROD_CUSTOMER_EMAIL',
  'PLAYWRIGHT_PROD_CUSTOMER_PASSWORD',
  'PLAYWRIGHT_PROD_CUSTOMER_UNVERIFIED_EMAIL',
  'PLAYWRIGHT_PROD_CUSTOMER_UNVERIFIED_PASSWORD',
  'PLAYWRIGHT_PROD_OWNER_EMAIL',
  'PLAYWRIGHT_PROD_OWNER_PASSWORD',
  'PLAYWRIGHT_PROD_MANAGER_EMAIL',
  'PLAYWRIGHT_PROD_MANAGER_PASSWORD',
  'PLAYWRIGHT_PROD_STAFF_EMAIL',
  'PLAYWRIGHT_PROD_STAFF_PASSWORD',
  'PLAYWRIGHT_PROD_VIEWER_EMAIL',
  'PLAYWRIGHT_PROD_VIEWER_PASSWORD',
];

const missing = requiredEnv.filter((key) => !process.env[key]);
if (missing.length) {
  console.error(`Missing required production Playwright env vars: ${missing.join(', ')}`);
  process.exit(1);
}

const env = {
  ...process.env,
  PLAYWRIGHT_E2E_ENV: 'production',
  PLAYWRIGHT_PRODUCTION_MODE: 'true',
  PLAYWRIGHT_RUN_ID: process.env.PLAYWRIGHT_RUN_ID || `E2E-${new Date().toISOString().replace(/[-:]/g, '').replace(/\..+$/, 'Z')}`,
  PLAYWRIGHT_PAYMENT_AMOUNT_VND: process.env.PLAYWRIGHT_PAYMENT_AMOUNT_VND || '10000',
};

for (const key of Object.keys(env)) {
  if (key.toUpperCase() === 'ELECTRON_RUN_AS_NODE') {
    delete env[key];
  }
}

const args = [
  playwrightCli,
  'test',
  specByMode[mode],
  `--project=${process.env.PLAYWRIGHT_PROJECT || 'chromium'}`,
  ...process.argv.slice(3),
];

console.log(`Production Playwright mode: ${mode}`);
console.log(`Base URL: ${env.PLAYWRIGHT_BASE_URL}`);
console.log(`Run ID: ${env.PLAYWRIGHT_RUN_ID}`);
console.log(`Spec: ${specByMode[mode]}`);

const child = spawn(process.execPath, args, {
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
