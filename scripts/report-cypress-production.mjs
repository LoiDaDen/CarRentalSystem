import fs from 'node:fs';
import path from 'node:path';

const repoRoot = process.cwd();
const reportsDir = path.join(repoRoot, 'cypress', 'reports');
const productionJsonPath = path.join(reportsDir, 'production-run-summary.json');
const productionMdPath = path.join(reportsDir, 'production-run-summary.md');
const traceabilityJsonPath = path.join(reportsDir, 'testcase-traceability.json');

const readJson = (filePath, fallback) => (
  fs.existsSync(filePath)
    ? JSON.parse(fs.readFileSync(filePath, 'utf8'))
    : fallback
);

const production = readJson(productionJsonPath, null);
const traceability = readJson(traceabilityJsonPath, { results: [] });

if (!production) {
  throw new Error(`Missing ${productionJsonPath}. Run npm run test:e2e:prod:preflight|smoke|full first.`);
}

const missingBusinessFlow = traceability.missingBusinessFlow
  || (traceability.results || []).filter((row) => !['business-flow', 'external-real'].includes(row.coverageDepth));

const paymentArtifacts = (production.artifacts || []).filter((item) => item.type === 'payment');
const cleanupArtifacts = (production.artifacts || []).filter((item) => item.type === 'cleanup');

const markdown = [
  '# Cypress Production Evidence Report',
  '',
  `- Base URL: \`${production.baseUrl}\``,
  `- Run ID: \`${production.runId}\``,
  `- Generated at UTC: \`${new Date().toISOString()}\``,
  `- Payment artifacts: \`${paymentArtifacts.length}\``,
  `- Cleanup artifacts: \`${cleanupArtifacts.length}\``,
  `- Missing business-flow testcase coverage: \`${missingBusinessFlow.length}\``,
  '',
  '## Payment Checkpoints',
  '',
  '| Label | Order Code | Amount | Status | Checkout URL |',
  '| --- | --- | --- | --- | --- |',
  ...paymentArtifacts.map((item) => {
    const data = item.data || {};
    return `| ${item.label || ''} | ${data.orderCode || ''} | ${data.amountVnd || ''} | ${data.status || ''} | ${data.checkoutUrl || ''} |`;
  }),
  '',
  '## Cleanup Checkpoints',
  '',
  '| Label | Status | Notes |',
  '| --- | --- | --- |',
  ...cleanupArtifacts.map((item) => {
    const data = item.data || {};
    return `| ${item.label || ''} | ${data.status || ''} | ${String(data.notes || '').replace(/\|/g, '\\|')} |`;
  }),
  '',
  '## Still Missing Business-Flow Depth',
  '',
  '| TC | Module | Priority | Current Depth | Title |',
  '| --- | --- | --- | --- | --- |',
  ...missingBusinessFlow.map((row) => `| ${row.id} | ${row.moduleCode} | ${row.priority} | ${row.coverageDepth || 'not-covered'} | ${String(row.title || '').replace(/\|/g, '\\|')} |`),
  '',
].join('\n');

fs.mkdirSync(reportsDir, { recursive: true });
fs.writeFileSync(productionMdPath, markdown, 'utf8');

console.log(`Wrote ${productionMdPath}`);
