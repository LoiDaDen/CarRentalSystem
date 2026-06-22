import fs from 'node:fs';
import path from 'node:path';

const repoRoot = process.cwd();
const matrixPath = path.join(repoRoot, 'playwright', 'fixtures', 'testcase-matrix.json');
const resultsPath = path.join(repoRoot, 'playwright', 'reports', 'results.json');
const reportJsonPath = path.join(repoRoot, 'playwright', 'reports', 'testcase-traceability.json');
const reportMdPath = path.join(repoRoot, 'playwright', 'reports', 'testcase-traceability.md');

const coverageRank = {
  'not-covered': 0,
  'route-smoke': 1,
  'ui-real': 2,
  'business-flow': 3,
  'external-real': 4,
};

function loadJson(filePath, fallback = null) {
  if (!fs.existsSync(filePath)) return fallback;
  return JSON.parse(fs.readFileSync(filePath, 'utf8'));
}

function collectSpecs(suites, parentTitles = [], results = []) {
  for (const suite of suites || []) {
    const titles = suite.title ? [...parentTitles, suite.title] : parentTitles;

    for (const spec of suite.specs || []) {
      for (const testItem of spec.tests || []) {
        const latestResult = testItem.results?.at(-1);
        const tcId = spec.title.match(/\[(TC\d{3})\]/)?.[1] || null;
        const annotations = [
          ...(testItem.annotations || []),
          ...(latestResult?.annotations || []),
        ];
        const annotationValue = (type) => annotations.find((item) => item.type === type)?.description;

        results.push({
          id: tcId,
          fullTitle: [...titles, spec.title].filter(Boolean).join(' > '),
          title: spec.title,
          status: latestResult?.status || testItem.status || 'not-run',
          expectedStatus: testItem.expectedStatus,
          coverageDepth: annotationValue('coverageDepth') || (tcId ? 'route-smoke' : 'not-covered'),
          resolvedRoute: annotationValue('resolvedRoute') || '',
          resolvedRole: annotationValue('resolvedRole') || '',
          spec: spec.file || '',
          line: spec.line,
          durationMs: testItem.results?.reduce((sum, result) => sum + (result.duration || 0), 0) ?? null,
          errors: latestResult?.errors?.map((error) => error.message || String(error)) || [],
        });
      }
    }

    collectSpecs(suite.suites || [], titles, results);
  }

  return results;
}

function normalizeStatus(status) {
  if (status === 'expected') return 'passed';
  if (status === 'unexpected') return 'failed';
  if (status === 'flaky') return 'flaky';
  return status || 'not-run';
}

function markdownEscape(value) {
  return String(value ?? '').replace(/\|/g, '\\|').replace(/\r?\n/g, '<br>');
}

const matrix = loadJson(matrixPath);
if (!matrix) {
  throw new Error(`Missing ${matrixPath}. Run npm run testcases:extract first.`);
}

const playwrightResults = loadJson(resultsPath, { suites: [], errors: [], stats: {} });
const executed = collectSpecs(playwrightResults.suites);
const byId = new Map();
const unlinkedFailures = [];

for (const item of executed) {
  const normalized = { ...item, status: normalizeStatus(item.status) };
  if (!item.id) {
    if (!['passed', 'skipped'].includes(normalized.status)) unlinkedFailures.push(normalized);
    continue;
  }

  const previous = byId.get(item.id);
  if (!previous || previous.status !== 'passed') {
    byId.set(item.id, normalized);
  }
}

const rows = matrix.cases.map((testCase) => {
  const result = byId.get(testCase.id);
  return {
    id: testCase.id,
    moduleCode: testCase.moduleCode,
    moduleName: testCase.moduleName,
    priority: testCase.priority,
    title: testCase.title,
    status: result?.status || 'not-run',
    coverageDepth: result?.coverageDepth || 'not-covered',
    resolvedRoute: result?.resolvedRoute || '',
    resolvedRole: result?.resolvedRole || '',
    spec: result?.spec || '',
    durationMs: result?.durationMs ?? null,
    errors: result?.errors || [],
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

const moduleSummary = rows.reduce((acc, row) => {
  acc[row.moduleCode] ??= { total: 0, passed: 0, failed: 0, skipped: 0, notRun: 0 };
  acc[row.moduleCode].total += 1;
  if (row.status === 'passed') acc[row.moduleCode].passed += 1;
  else if (row.status === 'skipped') acc[row.moduleCode].skipped += 1;
  else if (row.status === 'not-run') acc[row.moduleCode].notRun += 1;
  else acc[row.moduleCode].failed += 1;
  return acc;
}, {});

const missingBusinessFlow = rows.filter((row) => (coverageRank[row.coverageDepth] || 0) < coverageRank['business-flow']);

const report = {
  generatedAtUtc: new Date().toISOString(),
  sourceWorkbook: matrix.sourceWorkbook,
  playwrightResults: path.relative(repoRoot, resultsPath).replace(/\\/g, '/'),
  totalCases: rows.length,
  summary,
  coverageSummary,
  moduleSummary,
  missingBusinessFlowCount: missingBusinessFlow.length,
  missingBusinessFlow,
  results: rows,
  unlinkedFailures,
  runnerErrors: playwrightResults.errors || [],
};

fs.mkdirSync(path.dirname(reportJsonPath), { recursive: true });
fs.writeFileSync(reportJsonPath, `${JSON.stringify(report, null, 2)}\n`, 'utf8');

const markdown = [
  '# Playwright Testcase Traceability',
  '',
  `- Source workbook: \`${matrix.sourceWorkbook}\``,
  `- Generated at UTC: \`${report.generatedAtUtc}\``,
  `- Total traceability cases: \`${rows.length}\``,
  `- Summary: ${Object.entries(summary).map(([key, value]) => `\`${key}: ${value}\``).join(', ') || '`none`'}`,
  `- Coverage depth: ${Object.entries(coverageSummary).map(([key, value]) => `\`${key}: ${value}\``).join(', ') || '`none`'}`,
  `- Missing business-flow coverage: \`${missingBusinessFlow.length}\``,
  '',
  '## Module Summary',
  '',
  '| Module | Total | Passed | Failed | Skipped | Not Run |',
  '| --- | ---: | ---: | ---: | ---: | ---: |',
  ...Object.entries(moduleSummary).map(([moduleCode, item]) => (
    `| ${moduleCode} | ${item.total} | ${item.passed} | ${item.failed} | ${item.skipped} | ${item.notRun} |`
  )),
  '',
  '## Results',
  '',
  '| TC | Module | Priority | Status | Depth | Route | Role | Spec | Title |',
  '| --- | --- | --- | --- | --- | --- | --- | --- | --- |',
  ...rows.map((row) => (
    `| ${row.id} | ${row.moduleCode} | ${row.priority} | ${row.status} | ${row.coverageDepth} | ${markdownEscape(row.resolvedRoute)} | ${markdownEscape(row.resolvedRole)} | ${markdownEscape(row.spec)} | ${markdownEscape(row.title)} |`
  )),
  '',
  '## Missing Business-Flow Coverage',
  '',
  '| TC | Module | Priority | Current Depth | Title |',
  '| --- | --- | --- | --- | --- |',
  ...missingBusinessFlow.map((row) => (
    `| ${row.id} | ${row.moduleCode} | ${row.priority} | ${row.coverageDepth} | ${markdownEscape(row.title)} |`
  )),
  '',
  ...(unlinkedFailures.length
    ? [
        '## Playwright Failures Without TC ID',
        '',
        '| Status | Spec | Title |',
        '| --- | --- | --- |',
        ...unlinkedFailures.map((item) => `| ${item.status} | ${markdownEscape(item.spec)} | ${markdownEscape(item.fullTitle)} |`),
        '',
      ]
    : []),
].join('\n');

fs.writeFileSync(reportMdPath, markdown, 'utf8');
console.log(`Wrote ${reportJsonPath}`);
console.log(`Wrote ${reportMdPath}`);
console.log(`Traceability: ${rows.length}/${matrix.totalCases} cases, ${summary.passed || 0} passed, ${summary['not-run'] || 0} not-run.`);
