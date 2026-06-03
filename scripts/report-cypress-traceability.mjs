import fs from 'node:fs';
import path from 'node:path';

const repoRoot = process.cwd();
const matrixPath = path.join(repoRoot, 'cypress', 'fixtures', 'testcase-matrix.json');
const addendumPath = path.join(repoRoot, 'cypress', 'fixtures', 'testcase-addendum.json');
const reportJsonPath = path.join(repoRoot, 'cypress', 'reports', 'testcase-traceability.json');
const reportMdPath = path.join(repoRoot, 'cypress', 'reports', 'testcase-traceability.md');

const coverageRank = {
  'not-covered': 0,
  'route-smoke': 1,
  'ui-real': 2,
  'business-flow': 3,
  'external-real': 4,
};

if (!fs.existsSync(matrixPath)) {
  throw new Error(`Missing ${matrixPath}. Run npm run testcases:extract first.`);
}

const loadMatrix = () => {
  const matrix = JSON.parse(fs.readFileSync(matrixPath, 'utf8'));
  if (!fs.existsSync(addendumPath)) return matrix;

  const addendum = JSON.parse(fs.readFileSync(addendumPath, 'utf8'));
  const cases = [...(matrix.cases || []), ...(addendum.cases || [])].sort((a, b) => a.numericId - b.numericId);
  const ids = cases.map((testCase) => testCase.id);
  const duplicateIds = ids.filter((id, index) => ids.indexOf(id) !== index);
  if (duplicateIds.length) {
    throw new Error(`Duplicate Cypress testcase ids: ${duplicateIds.join(', ')}`);
  }

  return {
    ...matrix,
    sourceWorkbook: `${matrix.sourceWorkbook}; ${addendum.source || 'cypress-addendum'}`,
    totalCases: cases.length,
    modules: [...(matrix.modules || []), ...(addendum.modules || [])],
    cases,
  };
};

const matrix = loadMatrix();
const existingReport = fs.existsSync(reportJsonPath)
  ? JSON.parse(fs.readFileSync(reportJsonPath, 'utf8'))
  : { results: [] };

const byId = new Map((existingReport.results || []).map((item) => [item.id, item]));
const rows = matrix.cases.map((testCase) => {
  const result = byId.get(testCase.id);
  const status = result?.status === 'pending' ? 'skipped' : result?.status;
  return {
    id: testCase.id,
    moduleCode: testCase.moduleCode,
    priority: testCase.priority,
    title: testCase.title,
    status: status || 'not-run',
    coverageDepth: result?.coverageDepth || 'not-covered',
    spec: result?.spec || '',
    durationMs: result?.durationMs ?? null,
  };
});

const summary = rows.reduce((acc, row) => {
  acc[row.status] = (acc[row.status] || 0) + 1;
  return acc;
}, {});
const coverageSummary = rows.reduce((acc, row) => {
  acc[row.coverageDepth] = (acc[row.coverageDepth] || 0) + 1;
  return acc;
}, {});
const unlinkedFailures = existingReport.unlinkedFailures || [];
if (unlinkedFailures.length) {
  summary.unlinkedFailed = unlinkedFailures.length;
}
const missingBusinessFlow = rows.filter((row) => (coverageRank[row.coverageDepth] || 0) < coverageRank['business-flow']);

const report = {
  generatedAtUtc: new Date().toISOString(),
  sourceWorkbook: matrix.sourceWorkbook,
  totalCases: rows.length,
  summary,
  coverageSummary,
  missingBusinessFlow,
  results: rows,
  unlinkedFailures,
};

fs.mkdirSync(path.dirname(reportJsonPath), { recursive: true });
fs.writeFileSync(reportJsonPath, `${JSON.stringify(report, null, 2)}\n`, 'utf8');

const markdown = [
  '# Cypress Testcase Traceability',
  '',
  `- Source workbook: \`${matrix.sourceWorkbook}\``,
  `- Generated at UTC: \`${report.generatedAtUtc}\``,
  `- Total traceability cases: \`${rows.length}\``,
  `- Summary: ${Object.entries(summary).map(([key, value]) => `\`${key}: ${value}\``).join(', ')}`,
  `- Coverage depth: ${Object.entries(coverageSummary).map(([key, value]) => `\`${key}: ${value}\``).join(', ')}`,
  '',
  '| TC | Module | Priority | Status | Depth | Spec | Title |',
  '| --- | --- | --- | --- | --- | --- | --- |',
  ...rows.map((row) => `| ${row.id} | ${row.moduleCode} | ${row.priority} | ${row.status} | ${row.coverageDepth} | ${row.spec} | ${row.title.replace(/\|/g, '\\|')} |`),
  '',
  '## Missing Business-Flow Coverage',
  '',
  '| TC | Module | Priority | Current Depth | Title |',
  '| --- | --- | --- | --- | --- |',
  ...missingBusinessFlow.map((row) => `| ${row.id} | ${row.moduleCode} | ${row.priority} | ${row.coverageDepth} | ${row.title.replace(/\|/g, '\\|')} |`),
  '',
  ...(unlinkedFailures.length
    ? [
        '## Cypress Failures Without TC ID',
        '',
        '| Status | Spec | Title |',
        '| --- | --- | --- |',
        ...unlinkedFailures.map((item) => `| ${item.status} | ${item.spec} | ${String(item.fullTitle || '').replace(/\|/g, '\\|')} |`),
      ]
    : []),
  '',
].join('\n');

fs.writeFileSync(reportMdPath, markdown, 'utf8');
console.log(`Wrote ${reportJsonPath}`);
console.log(`Wrote ${reportMdPath}`);
