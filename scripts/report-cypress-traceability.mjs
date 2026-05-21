import fs from 'node:fs';
import path from 'node:path';

const repoRoot = process.cwd();
const matrixPath = path.join(repoRoot, 'cypress', 'fixtures', 'testcase-matrix.json');
const addendumPath = path.join(repoRoot, 'cypress', 'fixtures', 'testcase-addendum.json');
const reportJsonPath = path.join(repoRoot, 'cypress', 'reports', 'testcase-traceability.json');
const reportMdPath = path.join(repoRoot, 'cypress', 'reports', 'testcase-traceability.md');

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
  return {
    id: testCase.id,
    moduleCode: testCase.moduleCode,
    priority: testCase.priority,
    title: testCase.title,
    status: result?.status || 'not-run',
    spec: result?.spec || '',
    durationMs: result?.durationMs ?? null,
  };
});

const summary = rows.reduce((acc, row) => {
  acc[row.status] = (acc[row.status] || 0) + 1;
  return acc;
}, {});

const report = {
  generatedAtUtc: new Date().toISOString(),
  sourceWorkbook: matrix.sourceWorkbook,
  totalCases: rows.length,
  summary,
  results: rows,
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
  '',
  '| TC | Module | Priority | Status | Spec | Title |',
  '| --- | --- | --- | --- | --- | --- |',
  ...rows.map((row) => `| ${row.id} | ${row.moduleCode} | ${row.priority} | ${row.status} | ${row.spec} | ${row.title.replace(/\|/g, '\\|')} |`),
  '',
].join('\n');

fs.writeFileSync(reportMdPath, markdown, 'utf8');
console.log(`Wrote ${reportJsonPath}`);
console.log(`Wrote ${reportMdPath}`);
