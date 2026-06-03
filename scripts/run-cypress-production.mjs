import { spawn } from 'node:child_process';
import path from 'node:path';
import process from 'node:process';

const repoRoot = process.cwd();
const cypressBin = path.join(repoRoot, 'node_modules', 'cypress', 'bin', 'cypress');
const mode = (process.argv[2] || 'full').toLowerCase();

const specByMode = {
  preflight: 'cypress/e2e/production/00-prod-preflight.cy.js',
  smoke: 'cypress/e2e/production/{00-prod-preflight,01-prod-public-smoke}.cy.js',
  full: 'cypress/e2e/production/**/*.cy.js',
  cleanup: 'cypress/e2e/production/99-prod-cleanup.cy.js',
};

if (!Object.prototype.hasOwnProperty.call(specByMode, mode)) {
  console.error(`Unknown production Cypress mode "${mode}". Use preflight, smoke, full, or cleanup.`);
  process.exit(1);
}

const requiredEnv = [
  'CYPRESS_BASE_URL',
  'CYPRESS_PROD_ADMIN_EMAIL',
  'CYPRESS_PROD_ADMIN_PASSWORD',
  'CYPRESS_PROD_CUSTOMER_EMAIL',
  'CYPRESS_PROD_CUSTOMER_PASSWORD',
  'CYPRESS_PROD_CUSTOMER_UNVERIFIED_EMAIL',
  'CYPRESS_PROD_CUSTOMER_UNVERIFIED_PASSWORD',
  'CYPRESS_PROD_OWNER_EMAIL',
  'CYPRESS_PROD_OWNER_PASSWORD',
  'CYPRESS_PROD_MANAGER_EMAIL',
  'CYPRESS_PROD_MANAGER_PASSWORD',
  'CYPRESS_PROD_STAFF_EMAIL',
  'CYPRESS_PROD_STAFF_PASSWORD',
  'CYPRESS_PROD_VIEWER_EMAIL',
  'CYPRESS_PROD_VIEWER_PASSWORD',
];

const missing = requiredEnv.filter((key) => !process.env[key]);
if (missing.length) {
  console.error(`Missing required production Cypress env vars: ${missing.join(', ')}`);
  process.exit(1);
}

const env = {
  ...process.env,
  CYPRESS_E2E_ENV: 'production',
  CYPRESS_PRODUCTION_MODE: 'true',
  CYPRESS_RUN_ID: process.env.CYPRESS_RUN_ID || `E2E-${new Date().toISOString().replace(/[-:]/g, '').replace(/\..+$/, 'Z')}`,
  CYPRESS_PAYMENT_AMOUNT_VND: process.env.CYPRESS_PAYMENT_AMOUNT_VND || '10000',
};

for (const key of Object.keys(env)) {
  if (key.toUpperCase() === 'ELECTRON_RUN_AS_NODE') {
    delete env[key];
  }
}

const args = [
  cypressBin,
  'run',
  '--browser',
  process.env.CYPRESS_BROWSER || 'chrome',
  '--spec',
  specByMode[mode],
  ...process.argv.slice(3),
];

console.log(`Production Cypress mode: ${mode}`);
console.log(`Base URL: ${env.CYPRESS_BASE_URL}`);
console.log(`Run ID: ${env.CYPRESS_RUN_ID}`);
console.log(`Spec: ${specByMode[mode]}`);

const child = spawn(process.execPath, args, {
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
