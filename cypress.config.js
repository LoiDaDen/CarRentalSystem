import { defineConfig } from "cypress";
import fs from "node:fs";
import path from "node:path";

const reportsDir = path.resolve("cypress", "reports");
const matrixPath = path.resolve("cypress", "fixtures", "testcase-matrix.json");
const addendumPath = path.resolve("cypress", "fixtures", "testcase-addendum.json");
const reportJsonPath = path.join(reportsDir, "testcase-traceability.json");
const reportMdPath = path.join(reportsDir, "testcase-traceability.md");
const productionReportJsonPath = path.join(reportsDir, "production-run-summary.json");
const productionReportMdPath = path.join(reportsDir, "production-run-summary.md");
const testcaseResults = new Map();
const unlinkedFailures = [];
const productionArtifacts = [];
const productionSpecResults = [];
let cypressSeed = null;

const isTruthy = (value) => /^(1|true|yes|y)$/i.test(String(value || ""));
const e2eEnv = process.env.CYPRESS_E2E_ENV || process.env.CYPRESS_TEST_ENV || "local";
const productionMode = isTruthy(process.env.CYPRESS_PRODUCTION_MODE || process.env.CYPRESS_PRODUCTION)
  || /^prod(?:uction)?$/i.test(e2eEnv);
const defaultRunId = `E2E-${new Date().toISOString().replace(/[-:]/g, "").replace(/\..+$/, "Z")}`;
const productionRunId = process.env.CYPRESS_RUN_ID || (productionMode ? defaultRunId : "");
const baseUrl = process.env.CYPRESS_BASE_URL || process.env.Cypress__BaseUrl || "http://localhost:5168";
const appendTraceability = isTruthy(process.env.CYPRESS_APPEND_TRACEABILITY);

const roleEnvName = (roleKey) => String(roleKey)
  .replace(/([a-z0-9])([A-Z])/g, "$1_$2")
  .replace(/[^a-zA-Z0-9]+/g, "_")
  .toUpperCase();

const prodUser = (roleKey) => {
  const normalized = roleEnvName(roleKey);
  return {
    email: process.env[`CYPRESS_PROD_${normalized}_EMAIL`] || "",
    password: process.env[`CYPRESS_PROD_${normalized}_PASSWORD`] || "",
  };
};

const prodUsers = {
  admin: prodUser("admin"),
  manager: prodUser("manager"),
  staff: prodUser("staff"),
  viewer: prodUser("viewer"),
  customer: prodUser("customer"),
  customerUnverified: prodUser("customerUnverified"),
  owner: prodUser("owner"),
};

const coverageRank = {
  "not-covered": 0,
  "route-smoke": 1,
  "ui-real": 2,
  "business-flow": 3,
  "external-real": 4,
};

const normalizeCoverageDepth = (depth) => {
  const normalized = String(depth || "").trim().toLowerCase();
  return Object.prototype.hasOwnProperty.call(coverageRank, normalized) ? normalized : "route-smoke";
};

const inferCoverageDepth = (specRelative = "", fullTitle = "") => {
  const tagged = String(fullTitle).match(/\[(?:DEPTH|FLOW):([a-z-]+)\]/i);
  if (tagged) return normalizeCoverageDepth(tagged[1]);
  if (/production/i.test(specRelative) && /payos|payment|ekyc|vendor|external/i.test(fullTitle)) return "external-real";
  if (/production/i.test(specRelative)) return "business-flow";
  if (/sandbox-realtime/i.test(specRelative)) return "external-real";
  if (/excel|workbook/i.test(specRelative) || /workbook/i.test(fullTitle)) return "route-smoke";
  return "ui-real";
};

const chooseTraceabilityResult = (current, next) => {
  if (!current) return next;
  if (next.status === "failed") return next;
  if (current.status === "failed") return current;
  if ((coverageRank[next.coverageDepth] || 0) > (coverageRank[current.coverageDepth] || 0)) return next;
  if (next.status === "passed" && current.status !== "passed") return next;
  return current;
};

const hydrateTraceabilityFromExistingReport = () => {
  if (!appendTraceability || !fs.existsSync(reportJsonPath)) return;

  try {
    const existingReport = JSON.parse(fs.readFileSync(reportJsonPath, "utf8"));
    for (const item of existingReport.results || []) {
      if (!item?.id || !item.status || item.status === "not-run") continue;
      testcaseResults.set(item.id, {
        status: item.status,
        spec: item.spec || "",
        fullTitle: item.fullTitle || "",
        coverageDepth: item.coverageDepth || "not-covered",
        durationMs: item.durationMs ?? null,
        attempts: item.attempts ?? 0,
      });
    }

    for (const item of existingReport.unlinkedFailures || []) {
      unlinkedFailures.push(item);
    }
  } catch (error) {
    console.warn(`Could not hydrate existing Cypress traceability report: ${error.message}`);
  }
};

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
      coverageDepth: result?.coverageDepth || "not-covered",
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
  const coverageSummary = results.reduce((acc, item) => {
    acc[item.coverageDepth] = (acc[item.coverageDepth] || 0) + 1;
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
    coverageSummary,
    missingBusinessFlow: results.filter((item) => (coverageRank[item.coverageDepth] || 0) < coverageRank["business-flow"]),
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
    `- Coverage depth: ${Object.entries(coverageSummary).map(([key, value]) => `\`${key}: ${value}\``).join(", ")}`,
    "",
    "| TC | Module | Priority | Status | Depth | Spec | Title |",
    "| --- | --- | --- | --- | --- | --- | --- |",
    ...results.map((item) => `| ${item.id} | ${item.moduleCode} | ${item.priority} | ${item.status} | ${item.coverageDepth} | ${item.spec} | ${item.title.replace(/\|/g, "\\|")} |`),
    "",
    "## Missing Business-Flow Coverage",
    "",
    "| TC | Module | Priority | Current Depth | Title |",
    "| --- | --- | --- | --- | --- |",
    ...report.missingBusinessFlow.map((item) => `| ${item.id} | ${item.moduleCode} | ${item.priority} | ${item.coverageDepth} | ${item.title.replace(/\|/g, "\\|")} |`),
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

const writeProductionReport = () => {
  if (!productionMode) return;

  const summary = productionSpecResults.reduce((acc, spec) => {
    acc.total += spec.total || 0;
    acc.passed += spec.passed || 0;
    acc.failed += spec.failed || 0;
    acc.pending += spec.pending || 0;
    acc.skipped += spec.skipped || 0;
    return acc;
  }, { total: 0, passed: 0, failed: 0, pending: 0, skipped: 0 });

  const artifactsByType = productionArtifacts.reduce((acc, item) => {
    const type = item.type || "artifact";
    acc[type] = (acc[type] || 0) + 1;
    return acc;
  }, {});

  const report = {
    generatedAtUtc: new Date().toISOString(),
    e2eEnv,
    productionMode,
    baseUrl,
    runId: productionRunId,
    paymentAmountVnd: Number(process.env.CYPRESS_PAYMENT_AMOUNT_VND || 10000),
    summary,
    artifactsByType,
    artifacts: productionArtifacts,
    specs: productionSpecResults,
  };

  fs.mkdirSync(reportsDir, { recursive: true });
  fs.writeFileSync(productionReportJsonPath, `${JSON.stringify(report, null, 2)}\n`, "utf8");

  const markdown = [
    "# Cypress Production Run Summary",
    "",
    `- Base URL: \`${baseUrl}\``,
    `- Run ID: \`${productionRunId}\``,
    `- Generated at UTC: \`${report.generatedAtUtc}\``,
    `- Payment amount target: \`${report.paymentAmountVnd} VND\``,
    `- Spec summary: \`passed: ${summary.passed}\`, \`failed: ${summary.failed}\`, \`pending: ${summary.pending}\`, \`skipped: ${summary.skipped}\``,
    `- Artifacts: ${Object.entries(artifactsByType).map(([key, value]) => `\`${key}: ${value}\``).join(", ") || "`none`"}`,
    "",
    "## Artifacts",
    "",
    "| Type | Label | Data |",
    "| --- | --- | --- |",
    ...productionArtifacts.map((item) => `| ${item.type || ""} | ${String(item.label || "").replace(/\|/g, "\\|")} | \`${JSON.stringify(item.data || {}).replace(/`/g, "'")}\` |`),
    "",
    "## Specs",
    "",
    "| Spec | Total | Passed | Failed | Pending | Skipped |",
    "| --- | --- | --- | --- | --- | --- |",
    ...productionSpecResults.map((item) => `| ${item.spec} | ${item.total} | ${item.passed} | ${item.failed} | ${item.pending} | ${item.skipped} |`),
    "",
  ].join("\n");

  fs.writeFileSync(productionReportMdPath, markdown, "utf8");
};

export default defineConfig({
  e2e: {
    baseUrl,
    specPattern: 'cypress/e2e/**/*.cy.{js,jsx,ts,tsx}',
    env: {
      resetSecret: process.env.CYPRESS_RESET_SECRET || process.env.Cypress__ResetSecret || 'local-cypress-reset',
      usePayOSSandbox: process.env.CYPRESS_USE_PAYOS_SANDBOX === 'true',
      useFptSandbox: process.env.CYPRESS_USE_FPT_SANDBOX === 'true',
      e2eEnv,
      productionMode,
      productionRunId,
      appendTraceability,
      productionPaymentAmountVnd: Number(process.env.CYPRESS_PAYMENT_AMOUNT_VND || 10000),
      productionWaitForPayment: process.env.CYPRESS_WAIT_FOR_PROD_PAYMENT !== "false",
      productionPaymentTimeoutMs: Number(process.env.CYPRESS_PAYMENT_TIMEOUT_MS || 10 * 60 * 1000),
      productionPaymentPollIntervalMs: Number(process.env.CYPRESS_PAYMENT_POLL_INTERVAL_MS || 10 * 1000),
      productionBookingCarId: process.env.CYPRESS_PROD_BOOKING_CAR_ID || "",
      prodUsers,
    },
    setupNodeEvents(on, config) {
      on("before:run", () => {
        testcaseResults.clear();
        unlinkedFailures.length = 0;
        productionArtifacts.length = 0;
        productionSpecResults.length = 0;
        cypressSeed = null;
        fs.mkdirSync(reportsDir, { recursive: true });
        if (appendTraceability) {
          hydrateTraceabilityFromExistingReport();
        }

        const filesToClear = appendTraceability
          ? [productionReportJsonPath, productionReportMdPath]
          : [reportJsonPath, reportMdPath, productionReportJsonPath, productionReportMdPath];
        for (const file of filesToClear) {
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
        registerProductionArtifact(artifact) {
          if (!productionMode) return null;
          productionArtifacts.push({
            recordedAtUtc: new Date().toISOString(),
            type: artifact?.type || "artifact",
            label: artifact?.label || "",
            data: artifact?.data || {},
          });
          writeProductionReport();
          return null;
        },
        getProductionRunContext() {
          return {
            e2eEnv,
            productionMode,
            baseUrl,
            runId: productionRunId,
            paymentAmountVnd: Number(process.env.CYPRESS_PAYMENT_AMOUNT_VND || 10000),
          };
        },
      });

      on("after:spec", (spec, results) => {
        if (productionMode && /(?:^|[/\\])production[/\\]/i.test(spec.relative)) {
          productionSpecResults.push({
            spec: spec.relative,
            total: results.stats?.tests || 0,
            passed: results.stats?.passes || 0,
            failed: results.stats?.failures || 0,
            pending: results.stats?.pending || 0,
            skipped: results.stats?.skipped || 0,
            durationMs: results.stats?.duration || 0,
          });
          writeProductionReport();
        }

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
            const nextResult = {
              status,
              spec: spec.relative,
              fullTitle,
              coverageDepth: inferCoverageDepth(spec.relative, fullTitle),
              durationMs,
              attempts: test.attempts?.length || 0,
            };
            testcaseResults.set(id, chooseTraceabilityResult(testcaseResults.get(id), nextResult));
          }
        }

        writeTraceabilityReport();
      });

      on("after:run", () => {
        writeTraceabilityReport();
        writeProductionReport();
      });

      return config;
    },
    viewportWidth: 1280,
    viewportHeight: 720,
    defaultCommandTimeout: 10000,
  },
});
