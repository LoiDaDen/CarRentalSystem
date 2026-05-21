import { defineConfig } from "cypress";
import fs from "node:fs";
import path from "node:path";

const reportsDir = path.resolve("cypress", "reports");
const matrixPath = path.resolve("cypress", "fixtures", "testcase-matrix.json");
const addendumPath = path.resolve("cypress", "fixtures", "testcase-addendum.json");
const reportJsonPath = path.join(reportsDir, "testcase-traceability.json");
const reportMdPath = path.join(reportsDir, "testcase-traceability.md");
const testcaseResults = new Map();
const unlinkedFailures = [];
let cypressSeed = null;

const normalizeStatus = (status) => {
  if (status === "pending") return "skipped";
  return status || "unknown";
};

const loadMatrix = () => {
  if (!fs.existsSync(matrixPath)) return null;
  const matrix = JSON.parse(fs.readFileSync(matrixPath, "utf8"));
  if (!fs.existsSync(addendumPath)) return matrix;

  const addendum = JSON.parse(fs.readFileSync(addendumPath, "utf8"));
  const cases = [...(matrix.cases || []), ...(addendum.cases || [])].sort((a, b) => a.numericId - b.numericId);
  const ids = cases.map((testCase) => testCase.id);
  const duplicateIds = ids.filter((id, index) => ids.indexOf(id) !== index);
  if (duplicateIds.length) {
    throw new Error(`Duplicate Cypress testcase ids: ${duplicateIds.join(", ")}`);
  }

  return {
    ...matrix,
    sourceWorkbook: `${matrix.sourceWorkbook}; ${addendum.source || "cypress-addendum"}`,
    totalCases: cases.length,
    modules: [...(matrix.modules || []), ...(addendum.modules || [])],
    cases,
  };
};

const writeTraceabilityReport = () => {
  const matrix = loadMatrix();
  if (!matrix) return;

  const results = matrix.cases.map((testCase) => {
    const result = testcaseResults.get(testCase.id);
    return {
      id: testCase.id,
      moduleCode: testCase.moduleCode,
      priority: testCase.priority,
      title: testCase.title,
      status: result?.status || "not-run",
      spec: result?.spec || "",
      fullTitle: result?.fullTitle || "",
      durationMs: result?.durationMs ?? null,
      attempts: result?.attempts ?? 0,
    };
  });

  const summary = results.reduce((acc, item) => {
    acc[item.status] = (acc[item.status] || 0) + 1;
    return acc;
  }, {});
  if (unlinkedFailures.length) {
    summary.unlinkedFailed = unlinkedFailures.length;
  }

  const report = {
    generatedAtUtc: new Date().toISOString(),
    sourceWorkbook: matrix.sourceWorkbook,
    totalCases: matrix.totalCases,
    summary,
    results,
    unlinkedFailures,
  };

  fs.mkdirSync(reportsDir, { recursive: true });
  fs.writeFileSync(reportJsonPath, `${JSON.stringify(report, null, 2)}\n`, "utf8");

  const markdown = [
    "# Cypress Testcase Traceability",
    "",
    `- Source workbook: \`${matrix.sourceWorkbook}\``,
    `- Generated at UTC: \`${report.generatedAtUtc}\``,
    `- Total traceability cases: \`${matrix.totalCases}\``,
    `- Summary: ${Object.entries(summary).map(([key, value]) => `\`${key}: ${value}\``).join(", ")}`,
    "",
    "| TC | Module | Priority | Status | Spec | Title |",
    "| --- | --- | --- | --- | --- | --- |",
    ...results.map((item) => `| ${item.id} | ${item.moduleCode} | ${item.priority} | ${item.status} | ${item.spec} | ${item.title.replace(/\|/g, "\\|")} |`),
    "",
    ...(unlinkedFailures.length
      ? [
          "## Cypress Failures Without TC ID",
          "",
          "| Status | Spec | Title |",
          "| --- | --- | --- |",
          ...unlinkedFailures.map((item) => `| ${item.status} | ${item.spec} | ${item.fullTitle.replace(/\|/g, "\\|")} |`),
          "",
        ]
      : []),
    "",
  ].join("\n");

  fs.writeFileSync(reportMdPath, markdown, "utf8");
};

export default defineConfig({
  e2e: {
    baseUrl: 'http://localhost:5168',
    specPattern: 'cypress/e2e/**/*.cy.{js,jsx,ts,tsx}',
    env: {
      resetSecret: process.env.CYPRESS_RESET_SECRET || process.env.Cypress__ResetSecret || 'local-cypress-reset',
      usePayOSSandbox: process.env.CYPRESS_USE_PAYOS_SANDBOX === 'true',
      useFptSandbox: process.env.CYPRESS_USE_FPT_SANDBOX === 'true',
    },
    setupNodeEvents(on, config) {
      on("before:run", () => {
        testcaseResults.clear();
        unlinkedFailures.length = 0;
        cypressSeed = null;
        fs.mkdirSync(reportsDir, { recursive: true });
        for (const file of [reportJsonPath, reportMdPath]) {
          if (fs.existsSync(file)) fs.rmSync(file, { force: true });
        }
      });

      on("task", {
        getCypressSeed() {
          return cypressSeed;
        },
        setCypressSeed(seed) {
          cypressSeed = seed;
          return null;
        },
        clearCypressSeed() {
          cypressSeed = null;
          return null;
        },
      });

      on("after:spec", (spec, results) => {
        for (const test of results.tests || []) {
          const fullTitle = Array.isArray(test.title) ? test.title.join(" ") : String(test.title || "");
          const ids = fullTitle.match(/TC\d{3}/g) || [];

          const lastAttempt = test.attempts?.[test.attempts.length - 1];
          const status = normalizeStatus(test.state || lastAttempt?.state);
          const durationMs = (test.attempts || []).reduce((sum, attempt) => sum + (attempt.duration || 0), 0);

          if (!ids.length) {
            if (status === "failed") {
              unlinkedFailures.push({
                status,
                spec: spec.relative,
                fullTitle,
                durationMs,
                attempts: test.attempts?.length || 0,
              });
            }
            continue;
          }

          for (const id of ids) {
            testcaseResults.set(id, {
              status,
              spec: spec.relative,
              fullTitle,
              durationMs,
              attempts: test.attempts?.length || 0,
            });
          }
        }

        writeTraceabilityReport();
      });

      on("after:run", () => {
        writeTraceabilityReport();
      });

      return config;
    },
    viewportWidth: 1280,
    viewportHeight: 720,
    defaultCommandTimeout: 10000,
  },
});
