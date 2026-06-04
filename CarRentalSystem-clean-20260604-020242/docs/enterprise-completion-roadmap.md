# Enterprise Completion Roadmap Cho CarRentalSystem

Updated: 2026-05-16

Latest enterprise benchmark audit after Wave 1-5 repo gates: [`docs/enterprise-benchmark-audit-2026-05-17.md`](enterprise-benchmark-audit-2026-05-17.md).

## 1. Executive Summary

Muc tieu cua roadmap nay la dua CarRentalSystem tu muc **70% enterprise readiness** sau audit ngay 2026-05-16 len **production-grade enterprise 100%** co the chung minh bang code gates, production operations evidence, security/compliance evidence, performance evidence va runbook/drill thuc te.

Trang thai hien tai: **No-Go for public production traffic**.

He thong co the chay demo/staging va beta/internal traffic co kiem soat, nhung chua nen mo public production traffic lon cho den khi hoan thanh Wave 1 va Wave 2.

Roadmap nay tap trung vao phan con thieu sau audit:

- State machine va payment consistency.
- Payment reconciliation va ledger/audit bat buoc.
- Production cloud evidence: staging, backup/restore, rollback, alert drill.
- Observability that: OTLP backend, centralized logs, uptime/error/job/webhook alert.
- Performance va quality gates.
- Compliance runtime controls.
- UX/accessibility, support, fleet operations va developer ecosystem.

## 2. Updated Enterprise Scorecard

| Tieu chi | Hien tai | Muc tieu | Trang thai |
|---|---:|---:|---|
| Kien truc he thong | 76% | 100% | Yellow |
| Bao mat & Tuan thu | 74% | 100% | Yellow |
| Do tin cay & San sang | 67% | 100% | Yellow |
| Hieu nang | 63% | 100% | Yellow |
| Chat luong Code & DevOps | 79% | 100% | Yellow |
| UX & Van hanh | 65% | 100% | Yellow |
| Phan tich kinh doanh | 62% | 100% | Yellow |
| **Tong** | **70%** | **100%** | **Yellow** |

Target score by wave:

| Wave | Timeline | Target score | Production decision |
|---|---|---:|---|
| Wave 1 | Week 1-3 | 76-80% | No-Go public traffic |
| Wave 2 | Week 4-8 | 82-86% | Conditional beta/staged production |
| Wave 3 | Week 9-16 | 88-92% | Limited production if operations evidence is clean |
| Wave 4 | Week 17-28 | 94-97% | Enterprise production candidate |
| Wave 5 | Week 29-40 | 98-100% | Enterprise-ready with evidence |

## 3. Production Go/No-Go Criteria

Production public traffic is blocked until all mandatory gates pass:

- Release build and full test suite pass.
- Secret scan and dependency/container/security scans pass with no open critical/high findings.
- No direct critical state mutation remains outside approved transition services.
- Manual payment, PayOS webhook, refund, deduction and payout reconcile through ledger.
- Staging is production-like and uses managed SQL/storage/secret manager/WAF/CDN.
- SQL PITR and secure file restore drill pass with RTO <= 4h and RPO <= 1h.
- Uptime, app down, DB issue, webhook failure and job backlog alerts fire within 1 minute.
- Rollback drill finishes in under 5 minutes.
- PayOS live reconciliation explains every booking/payment/refund state.
- No P0/P1 production blocker remains open.

## 4. Wave 1 - Production Blocker Closure, Week 1-3

Goal: remove every P1 risk that can cause wrong money, wrong booking state, wrong vehicle state or incomplete audit before production.

Wave 1 implementation status as of 2026-05-16:

- Repo implementation is in progress/completed for core Wave 1 blockers: manual payment transition, PayOS existing-booking transition delegation, booking return deposit transition, settlement-review gating, and snapshot production details redaction.
- New safety gate: `scripts\Test-Wave1StateMutationSafety.ps1`.
- Remaining Wave 1 evidence required before public traffic: run the full acceptance command set, review any remaining direct state mutation findings, and attach payment/settlement reconciliation samples from staging.

| ID | Task | Priority | Owner | Effort | Impact | Dependency | Definition of Done | Evidence required |
|---|---|---|---|---:|---|---|---|---|
| ENT-STATE-01 | Enforce critical state transition service for booking/payment/car/deposit. | P0 | TBD | 5 days | High | Current transition services | All updates to `BookingStatusID`, `DepositStatus`, `PaymentStatus`, `CarStatusID` outside seed/migration/test go through transition services with actor, reason, audit and transaction. | Static guard output, regression tests, code review checklist. |
| ENT-PAY-01 | Standardize manual payment confirmation. | P0 | TBD | 3 days | High | ENT-STATE-01 | Admin manual confirm records ledger, hash-chain audit, actor, reason and idempotency key; controller no longer sets payment/deposit state directly. | Manual payment reconciliation test and audit entry sample. |
| ENT-RETURN-01 | Gate handover/return through settlement review. | P0 | TBD | 4 days | High | SettlementReview service | Return flow creates `SettlementReview` for late/fuel/cleaning/damage/toll/violation; refund/deduction ledger is created only after Admin/Manager approval. | Return/settlement integration tests and ledger sample. |
| ENT-PAYOS-01 | Separate PayOS adapter from domain transition. | P0 | TBD | 5 days | High | Ledger and reservation services | PayOS service validates provider event and delegates domain transition; duplicate, late hold, amount mismatch, failed payment and cart checkout are idempotent. | PayOS webhook test matrix and reconciliation report. |
| ENT-SNAPSHOT-01 | Harden snapshot production governance. | P0 | TBD | 3 days | High | Snapshot service | Production blocks raw details/download/restore by default; any production snapshot path is encrypted or redacted and audited through hash-chain audit. | Production config test, snapshot view test, audit evidence. |

Wave 1 exit criteria:

- Build/test/safety scripts pass.
- No P1 state/payment blocker remains open.
- Static guard for direct state mutation passes.
- Target score: **76-80%**.

## 5. Wave 2 - Real Production Operations, Week 4-8

Goal: turn repo-first readiness into real production operations evidence.

Wave 2 implementation status as of 2026-05-16:

- Repo implementation is ready for Wave 2 operations evidence: staging config shape, production operations runbook, evidence templates and static safety script have been added.
- New safety gate: `scripts\Test-Wave2OperationsSafety.ps1`.
- Real cloud completion is still required before public traffic: fill and sign `docs/wave2-evidence/*.md` after a real staging smoke test, secret rotation, restore drill, alert drill and rollback drill.
- Optional strict mode for real go-live: run `scripts\Test-Wave2OperationsSafety.ps1 -RequireCompletedEvidence` so placeholder evidence cannot pass.

| ID | Task | Priority | Owner | Effort | Impact | Dependency | Definition of Done | Evidence required |
|---|---|---|---|---:|---|---|---|---|
| ENT-INFRA-01 | Create cloud staging production-like environment. | P0 | TBD | 5 days | High | Wave 1 complete | Staging uses managed app hosting/container, managed SQL, managed storage, secret manager, WAF/CDN and Redis if enabled. | Staging topology diagram, config diff, smoke test result. |
| ENT-SECRET-01 | Rotate secrets and move to secret manager. | P0 | TBD | 2 days | High | Cloud environment | DB, SMTP, PayOS, FPT, Recaptcha and tracking secrets are rotated; old keys revoked; no production secret in tracked files. | Secret rotation checklist and secret scan output. |
| ENT-DR-01 | Run SQL and secure file restore drill. | P0 | TBD | 3 days | High | ENT-INFRA-01 | SQL PITR restore and secure uploads/contracts/evidence restore succeed with RTO <= 4h and RPO <= 1h. | Restore drill report, timestamps, owner sign-off. |
| ENT-OBS-01 | Connect real observability backend. | P0 | TBD | 4 days | High | OpenTelemetry config | OTLP sends to Grafana/Datadog/Azure Monitor; central logs, error tracking, uptime, Hangfire/job and PayOS webhook alerts are active. | Dashboard links, alert screenshots, test incident log. |
| ENT-RELEASE-01 | Run release and rollback drill. | P0 | TBD | 3 days | High | ENT-INFRA-01 | Blue/green, deployment slot or equivalent rollback completes in under 5 minutes. | Rollback evidence with timestamp, release version and owner. |

Wave 2 exit criteria:

- Staging smoke passes.
- Backup/restore drill passes.
- Alert drill passes.
- Rollback drill passes.
- Target score: **82-86%**.

## 6. Wave 3 - Performance, Quality, Compliance, Week 9-16

Goal: prove the system can handle production load, prevent critical regressions and enforce privacy controls at runtime.

Wave 3 implementation status as of 2026-05-16:

- Repo implementation is ready for Wave 3 safety: critical admin dashboard/report aggregates were moved toward DB-side query patterns, runtime privacy request workflow was added, sensitive audit writes were moved to `IAuditTrailService` for core paths, and Wave 3 scripts/evidence templates were added.
- New safety gates: `scripts\Test-Wave3ProductionSafety.ps1`, `scripts\Test-Wave3PerformanceSafety.ps1`, and `scripts\Test-Wave3CoverageGate.ps1`.
- Real completion still requires staging load-test evidence in `docs/wave3-evidence/load-test-report.md`.
- Optional strict mode for real go-live: run `scripts\Test-Wave3ProductionSafety.ps1 -RequireCompletedEvidence`.

| ID | Task | Priority | Owner | Effort | Impact | Dependency | Definition of Done | Evidence required |
|---|---|---|---|---:|---|---|---|---|
| ENT-PERF-01 | Refactor admin reports/dashboard aggregation. | P1 | TBD | 5 days | High | Current reports/controllers | Large `ToListAsync()` plus in-memory aggregate is replaced with DB-side aggregate, paging or materialized KPIs for critical admin pages. | Static check output, query plan sample, before/after timings. |
| ENT-PERF-02 | Run baseline load test. | P1 | TBD | 5 days | High | ENT-PERF-01 | Checkout, cart multi-car, PayOS webhook, admin reports, eKYC upload and SLO dashboard meet P95 < 500ms at agreed target load. | Load test report, P95/P99 chart, bottleneck list. |
| ENT-QA-01 | Add critical-path coverage gate. | P1 | TBD | 5 days | High | Stable test suite | CI fails below threshold for reservation, payment, refund, audit, dispute, eligibility and upload validation paths. | Coverage report and CI gate result. |
| ENT-COMP-01 | Implement GDPR-style runtime privacy controls. | P1 | TBD | 10 days | High | Data inventory | Data export/delete request workflow and retention policy exist for PII, eKYC, contracts, tracking and audit data. | Privacy request test, retention job evidence, policy mapping. |
| ENT-AUDIT-01 | Move sensitive audit writes to `IAuditTrailService`. | P1 | TBD | 4 days | High | Audit hash chain | Secure file, snapshot, payment, role, eKYC, contract and payout actions are tamper-evident. | Audit verification output and negative tamper test. |

Wave 3 exit criteria:

- Load test report exists.
- Critical-path coverage gate enabled.
- Privacy controls exist in runtime, not docs only.
- Target score: **88-92%**.

## 7. Wave 4 - UX, Trust, Business Platform, Week 17-28

Goal: close capability gaps against Hertz/Enterprise/Turo/SIXT-style platforms.

Wave 4 implementation status as of 2026-05-17:

- Repo implementation is completed for the Wave 4 runtime foundation: support/dispute ticket workflow, customer self-service summary, fleet readiness dashboard, API key lifecycle, outbound webhook signing, and production public API safety guard.
- New safety gates: `scripts\Test-Wave4ProductionSafety.ps1`, `scripts\Test-Wave4AccessibilitySafety.ps1`, and `scripts\Test-Wave4FleetSafety.ps1`.
- New evidence templates: `docs/wave4-evidence/accessibility-audit.md`, `docs/wave4-evidence/support-sla.md`, `docs/wave4-evidence/fleet-ops.md`, and `docs/wave4-evidence/api-readiness.md`.
- Remaining Wave 4 evidence required before enterprise go-live: complete external/manual WCAG checks, verify support SLA on staging, capture fleet ops metrics, and run API readiness review before enabling any public API.

| ID | Task | Priority | Owner | Effort | Impact | Dependency | Definition of Done | Evidence required |
|---|---|---|---|---:|---|---|---|---|
| ENT-UX-01 | Run WCAG 2.1 AA and encoding audit. | P2 | TBD | 10 days | Medium | Critical flows stable | Checkout, eKYC, payment, contract and admin booking have no serious/critical accessibility issue; mojibake/UTF-8 issues fixed in user-facing pages. | axe/WAVE report, manual keyboard/screen-reader checklist. |
| ENT-SUPPORT-01 | Implement support SLA and dispute/claim operations. | P2 | TBD | 7 days | Medium | Dispute export | Ticket workflow, SLA tiers, refund dispute evidence checklist and emergency support path are active. | SLA matrix, sample dispute case, support dashboard. |
| ENT-LOYALTY-01 | Mature customer loyalty and self-service. | P2 | TBD | 10 days | Medium | Customer account flows | Customer can view receipts, rental history, support cases, refund/dispute progress and rewards status. | Customer flow tests and UX screenshots. |
| ENT-FLEET-01 | Mature fleet operations. | P2 | TBD | 10 days | Medium | Tracking and maintenance data | Fleet managers can see readiness, utilization, maintenance risk and tracking alert policy. | Fleet ops dashboard and alert test. |
| ENT-API-01 | Implement developer portal readiness controls. | P2 | TBD | 14 days | Medium | RBAC/rate limit/audit stable | API key lifecycle, signed outbound webhooks, rate limit, sandbox and protected OpenAPI docs exist; public API remains disabled until all gates pass. | API key tests, webhook signature tests, sandbox smoke. |

Wave 4 exit criteria:

- Accessibility pass.
- Support/claim process active.
- Fleet and customer self-service improved.
- API platform gated and safe.
- Target score: **94-97%**.

## 8. Wave 5 - Enterprise Certification Evidence, Week 29-40

Goal: reach enterprise-ready status with evidence that can survive external review.

Wave 5 implementation status as of 2026-05-17:

- Repo implementation is completed for the Wave 5 certification framework: multi-AZ failover evidence template, SOC2-ready control matrix, SOC2 evidence index, pentest remediation tracker, final enterprise readiness review template, and Wave 5 strict evidence script.
- New safety gate: `scripts\Test-Wave5EnterpriseEvidence.ps1`.
- Remaining Wave 5 evidence required before enterprise go-live: run a real multi-AZ failover drill, complete SOC2-ready evidence mapping, run external security review or pentest, remediate/risk-accept findings, and sign the final go/no-go review.

| ID | Task | Priority | Owner | Effort | Impact | Dependency | Definition of Done | Evidence required |
|---|---|---|---|---:|---|---|---|---|
| ENT-SRE-01 | Run multi-AZ failover drill. | P2 | TBD | 10 days | High | Cloud production architecture | App, SQL and storage survive one AZ failure; actual RTO/RPO recorded. | Failover drill report and timeline. |
| ENT-SOC2-01 | Build SOC2-ready evidence folder. | P2 | TBD | 14 days | Medium | Operations evidence | Access, change management, incident response, backup, vendor and vulnerability controls are mapped to evidence. | Control matrix and evidence index. |
| ENT-PENTEST-01 | Run external security review or pentest. | P2 | TBD | 10 days | High | Stable staging/prod-like env | Critical/high findings are remediated or formally risk-accepted with owner and due date. | Pentest report and remediation tracker. |
| ENT-FINAL-01 | Run final enterprise readiness review. | P2 | TBD | 5 days | High | Waves 1-5 complete | All gates pass; score >= 95% with remaining gap only scale/vendor maturity; final go/no-go is documented. | Final audit report, updated scorecard, executive sign-off. |

Wave 5 exit criteria:

- Uptime target >= 99.9% proven.
- RTO/RPO drill proven.
- Pentest/security review clean.
- Compliance evidence complete.
- Target score: **98-100%**.

## 9. Monthly Tracking Template

| Month | Target score | Main wave | Required evidence | Done | Notes |
|---|---:|---|---|---|---|
| Month 1 | 78% | Wave 1 | Static state guard, payment transition tests, snapshot governance tests | TBD | |
| Month 2 | 84% | Wave 2 | Staging topology, secret rotation, restore drill, alert drill | TBD | |
| Month 3 | 88% | Wave 2/3 | Rollback drill, PayOS reconciliation, initial load test | TBD | |
| Month 4 | 92% | Wave 3 | Performance report, coverage gate, privacy controls | TBD | |
| Month 5 | 95% | Wave 4 | WCAG audit, support SLA, fleet/customer self-service | TBD | |
| Month 6 | 97% | Wave 4 | API readiness controls, analytics/business dashboards | TBD | |
| Month 7 | 99% | Wave 5 | Multi-AZ failover, SOC2 evidence, pentest remediation | TBD | |
| Month 8 | 100% | Wave 5 | Final enterprise readiness review and go/no-go sign-off | TBD | |

## 10. Acceptance Test Plan

Every implementation batch must run:

- `dotnet build CarRentalSystem.csproj --configuration Release --no-restore /p:UseAppHost=false`
- `dotnet test ..\CarRentalSystem.Tests\CarRentalSystem.Tests.csproj --configuration Release --no-restore /p:UseAppHost=false`
- `powershell -ExecutionPolicy Bypass -File scripts\Test-TrackedSecrets.ps1 -RepoRoot .`
- `powershell -ExecutionPolicy Bypass -File scripts\Test-Phase1ProductionSafety.ps1`
- `powershell -ExecutionPolicy Bypass -File scripts\Test-Phase2ProductionSafety.ps1`
- `powershell -ExecutionPolicy Bypass -File scripts\Test-Phase3ProductionSafety.ps1`

Additional gates by wave:

- Wave 1: static direct state mutation guard, payment/return/PayOS regression tests.
- Wave 2: staging smoke, restore drill, alert drill, rollback drill.
- Wave 3: load test, coverage report, privacy request tests, audit tamper tests.
- Wave 4: accessibility audit, support SLA scenario, API key/webhook signature tests.
- Wave 5: failover drill, SOC2 evidence index, pentest remediation tracker, final benchmark audit, and `scripts\Test-Wave5EnterpriseEvidence.ps1`.

## 11. Definition Of Enterprise Ready

CarRentalSystem reaches enterprise-ready status only when all of these are true:

- Security: no open critical/high vulnerabilities; production secrets outside repo; MFA/SSO for privileged users; PII access is auditable.
- Reliability: uptime >= 99.9%; RTO <= 4h; RPO <= 1h; rollback < 5 minutes; failover drill passed.
- Consistency: booking/payment/car/deposit transitions are centralized, transactional and auditable.
- Money: every payment/refund/deduction/payout reconciles to ledger and provider events.
- Observability: logs, metrics, traces, SLO dashboard and alerts are live in production.
- Performance: critical flows P95 < 500ms at agreed target load.
- Compliance: privacy request/retention controls exist at runtime; SOC2-ready evidence is organized.
- UX/Ops: critical flows pass WCAG 2.1 AA; support SLA and dispute/claim process are active.
- Business: reporting, fleet ops, loyalty/self-service and API/developer readiness are operationally safe.
