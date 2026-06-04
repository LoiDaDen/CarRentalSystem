# Enterprise production readiness gates

Updated: 2026-05-16

This document turns the enterprise roadmap into release gates that can be verified from the repository and production runbooks.

Roadmap source of truth: [`docs/roadmap.md`](roadmap.md).
Phase 1 runbook: [`docs/phase1-production-safety-runbook.md`](phase1-production-safety-runbook.md).
Incident runbooks: [`docs/incident-runbooks.md`](incident-runbooks.md).
Phase 3 SRE runbook: [`docs/phase3-sre-runbook.md`](phase3-sre-runbook.md).
Enterprise completion roadmap: [`docs/enterprise-completion-roadmap.md`](enterprise-completion-roadmap.md).

## Current implemented gates

- Build gate: `dotnet build CarRentalSystem.csproj --configuration Release --no-restore /p:UseAppHost=false`.
- Test gate: `dotnet test ..\CarRentalSystem.Tests\CarRentalSystem.Tests.csproj --configuration Release --no-restore /p:UseAppHost=false`.
- Secret gate: `scripts\Test-TrackedSecrets.ps1`.
- Production config guard: app fails fast in `Production` when required secrets/config are missing, `Security:RequireRecaptcha` is disabled, CORS is empty/wildcarded, or unsafe snapshot flags are enabled.
- Production reCAPTCHA gate: login, register, and email verification require valid bot protection when `Security:RequireRecaptcha=true`; production requires this flag and `Recaptcha:SecretKey`.
- Snapshot safety gate: production blocks raw snapshot download/restore unless explicitly allowed, and the production guard rejects unsafe allow flags.
- Tracking ingest gate: device payloads require `X-Device-Id`, `X-Timestamp`, `X-Nonce`, and `X-Signature` when `Tracking:IngestSecret` is set.
- CI dependency/container gate: CI runs NuGet audit and Trivy filesystem scan and blocks critical/high findings.
- Admin API gate: `/admin/api/*` requires authenticated Admin or Manager.
- Privileged MFA gate: Admin, Manager, and Staff must pass email OTP MFA in `Production`; local/staging can force the same behavior with `Security:RequirePrivilegedMfa=true`.

## Phase 1 required before real production traffic

- Rotate all database/API/SMTP/PayOS/FPT/Recaptcha secrets that were ever used outside a secret manager.
- Store production secrets in the cloud secret manager or deployment environment, not tracked files.
- Run staging with production-equivalent config: HTTPS, managed SQL, Redis if enabled, storage, WAF/CDN, and production security headers.
- Complete backup and restore drill for SQL database and secure uploads/contracts/evidence storage.
- Prove payment safety with PayOS live webhook tests: valid signature, duplicate webhook, amount mismatch, late payment after hold expiry.
- Keep auto payout disabled until provider payout has a successful reconciliation drill.

## Phase 2 required for enterprise operating model

- Expand SAST/dependency vulnerability scan coverage and keep critical/high findings blocking.
- Add integration tests for register/eKYC, booking hold/payment, cart multi-car, cancel/refund, handover/settlement, and admin RBAC negative cases.
- Add ADRs for reservation, payment ledger, eKYC, contract evidence, and cloud topology.
- Add runbooks for payment webhook failure, DB outage, job backlog, suspicious login, double booking, refund dispute, and rollback.

## Phase 2 implemented gates

- Audit hash chain gate: `Audit:HashChainEnabled=true` in production, `IAuditTrailService` writes `PreviousHash` and `RecordHash`, and admin can verify a selected chain range.
- SSO config gate: `Security:RequirePrivilegedSso=true` is rejected in production unless Google or Microsoft provider credentials are configured through environment/secret manager.
- Dispute export gate: admin/manager export produces a ZIP with `manifest.json`, contract, handover evidence, payment ledger, provider events, refund/payout data, and audit trail; export is redacted by default.
- Settlement gate: late/fuel/cleaning/damage/toll/violation deductions require `SettlementReview` approval before a ledger deduction is created.
- Evidence gate: contract/handover records store content/evidence hashes and signer/recorder metadata.
- Phase 2 script gate: `scripts\Test-Phase2ProductionSafety.ps1` runs build/test/migration dry-run and static checks for Phase 2 safety surfaces.

## Phase 3 required for enterprise excellence

- Define SLI/SLO for availability, checkout success, payment webhook latency, booking conflict rate, and job freshness.
- Add OpenTelemetry traces across MVC request, EF Core, payment/eKYC providers, and Hangfire jobs.
- Run multi-AZ/failover drill and document actual RTO/RPO.
- Complete data inventory/classification for PII, payment records, contracts, tracking, and audit logs.
- Run WCAG 2.1 AA audit on customer checkout, admin booking, eKYC, payment, and contract flows.

## Phase 3 implemented gates

- Health endpoint gate: `/live` is process-only, `/ready` checks DB, data protection keys, PayOS/FPT config, Redis config when present, and Hangfire enablement.
- SRE config gate: production requires `Sre:SloAvailabilityTarget >= 99.9`, `Sre:RtoHours <= 4`, and `Sre:RpoHours <= 1`.
- SLO dashboard gate: Admin/Manager can review `/Admin/Sre/Slo` for availability, checkout success, payment webhook success/latency, booking conflict rate, job freshness, and error budget.
- OpenTelemetry gate: production rejects `Observability:OpenTelemetry:Enabled=true` unless `Observability:OpenTelemetry:OtlpEndpoint` is configured.
- PII-safe telemetry gate: enterprise telemetry filters sensitive tag names such as email, phone, citizen id, license, password, token, secret, payload, file path, and address.
- Localization gate: app enables ASP.NET Core request localization with `vi-VN` default and `en-US` supported.
- Analytics gate: product analytics defaults to no-op and filters sensitive properties; only approved server-side event names are allowed.
- API docs gate: Swagger is automatic only in Development; production with `ApiDocs:Enabled=true` requires `ApiDocs:RequireAuth=true` and runtime Admin/Manager authentication.
- Phase 3 script gate: `scripts\Test-Phase3ProductionSafety.ps1` checks build/test/migration dry-run, docs, config, observability, localization, analytics, and API-doc guards.

## Completion roadmap gates

- Wave 1 gate: no direct critical state mutation remains outside approved transition services, and manual payment/return/PayOS flows reconcile through ledger and hash-chain audit.
- Wave 2 gate: production-like staging, secret rotation, restore drill, alert drill and rollback drill have signed evidence.
- Wave 3 gate: admin report/dashboard aggregates are DB-side or materialized, critical-path coverage gate is active, privacy controls exist at runtime, and sensitive audits use `IAuditTrailService`.
- Wave 4 gate: WCAG 2.1 AA critical-flow audit passes, support/dispute SLA is active, fleet/customer self-service is improved, and API/developer portal controls are gated.
- Wave 5 gate: multi-AZ failover, SOC2-ready evidence, external security review and final enterprise readiness review are complete.

## Wave 1 implemented gates

- Payment transition gate: `IPaymentTransitionService` owns manual payment confirmation, PayOS paid/failed transitions, cart under-review transition, deposit status changes, ledger entries and hash-chain audit.
- Manual payment gate: Admin payment confirmation rejects PayOS-owned payments and records idempotent ledger/audit evidence instead of mutating payment/deposit state in the controller.
- Return settlement gate: customer return flow marks clean returns as `PendingRefund` through the transition service and creates `SettlementReview` for violation/late-return cases before any deduction ledger can be approved.
- PayOS delegation gate: PayOS webhook handling delegates existing-booking paid, failed and cart under-review state transitions to the domain transition service while preserving public callback/webhook routes.
- Snapshot details gate: production snapshot details render metadata-only by default, raw details/download/restore remain blocked unless explicitly configured, and snapshot attempts use hash-chain audit.
- Static Wave 1 gate: `scripts\Test-Wave1StateMutationSafety.ps1` checks the transition service surface, controller/provider mutation guardrails, snapshot governance and Wave 1 regression tests.

## Wave 2 implemented gates

- Staging config gate: `appsettings.Staging.example.json` defines a production-class staging shape with privileged MFA, reCAPTCHA, audit hash chain, safe snapshot flags, authenticated API docs and OpenTelemetry enabled for a real backend.
- Operations runbook gate: `docs/wave2-production-operations-runbook.md` defines required managed hosting, managed SQL, managed storage, secret manager, WAF/CDN, Hangfire persistence, OTLP backend and go/no-go rules.
- Evidence gate: `docs/wave2-evidence/` contains required templates for staging topology, secret rotation, restore drill, observability alert drill and release rollback drill.
- Restore gate: Wave 2 evidence must prove SQL PITR and secure-file restore with RTO <= 4h and RPO <= 1h.
- Alert gate: Wave 2 evidence must prove app down, PayOS webhook failure and job backlog alerts route within the target window.
- Rollback gate: Wave 2 evidence must prove rollback under 5 minutes with version, artifact and owner recorded.
- Static Wave 2 gate: `scripts\Test-Wave2OperationsSafety.ps1` checks staging config, runbook coverage and evidence templates; use `-RequireCompletedEvidence` before real go-live.

## Wave 3 implemented gates

- Performance query gate: critical admin dashboard/report aggregate paths use DB-side `CountAsync`, `SumAsync`, `GroupBy`, paging and `AsNoTracking` instead of loading broad tables for KPI calculations.
- Load-test evidence gate: `docs/wave3-load-test-plan.md` and `docs/wave3-evidence/load-test-report.md` define P95 < 500ms acceptance for checkout, cart, PayOS webhook, admin reports, eKYC and SLO dashboard.
- Critical-path coverage gate: `scripts\Test-Wave3CoverageGate.ps1` verifies regression coverage exists for payment transition, settlement, upload validation, privacy workflow and retention dry-run.
- Runtime privacy gate: customer export/delete requests and admin review/complete workflow exist through `IPrivacyRequestService`; delete requests do not hard-delete audit, ledger, payment or evidence records.
- Retention gate: privacy retention preview returns candidates without mutating production data.
- Sensitive audit gate: secure file, booking transition and PayOS sensitive audit paths use `IAuditTrailService` so records can be hash-chain verified.
- Static Wave 3 gate: `scripts\Test-Wave3ProductionSafety.ps1` checks docs, evidence templates, privacy runtime controls and sensitive audit guardrails; use `-RequireCompletedEvidence` before real go-live.

## Wave 4 implemented gates

- Accessibility evidence gate: `docs/wave4-accessibility-audit-plan.md` defines WCAG 2.1 AA checks for checkout, eKYC, payment, contract, admin booking, support, fleet and API key surfaces.
- Support SLA gate: customers can create support/dispute/claim tickets, privileged staff can update/assign them, SLA priority targets are modeled, and ticket changes are written through hash-chain audit.
- Customer self-service gate: customers can retrieve their own rental history, payments, refund/dispute/support status and membership/reward summary without exposing another user's data.
- Fleet operations gate: Admin/Manager fleet readiness dashboard uses DB-side aggregate/paging for vehicle status, maintenance risk, utilization, tracking alerts and missing tracking data.
- API readiness gate: public API remains disabled by default; API key lifecycle stores only hashed keys, supports revoke/expiry, and outbound webhook signing uses HMAC with timestamp replay protection.
- Public API production guard: production rejects `PublicApi:Enabled=true` unless API key auth, rate limiting, audit and webhook signing controls are explicitly required.
- Static Wave 4 gate: `scripts\Test-Wave4ProductionSafety.ps1`, `scripts\Test-Wave4AccessibilitySafety.ps1`, and `scripts\Test-Wave4FleetSafety.ps1` check Wave 4 runtime surfaces, docs, API guardrails and fleet query posture; use `-RequireCompletedEvidence` before real enterprise go-live.

## Wave 5 implemented gates

- Failover evidence gate: `docs/multi-az-failover-drill.md` and `docs/wave5-evidence/multi-az-failover-report.md` define the required multi-AZ app, SQL, storage, secret manager, Hangfire and PayOS webhook drill evidence.
- SOC2-ready evidence gate: `docs/soc2-ready-control-matrix.md` and `docs/wave5-evidence/soc2-evidence-index.md` map access, change, incident, backup/DR, vendor, vulnerability, audit and privacy controls to reviewable evidence.
- Pentest remediation gate: `docs/security-review-playbook.md` and `docs/wave5-evidence/pentest-remediation-tracker.md` make Critical/High findings release-blocking unless remediated or formally risk-accepted with expiry.
- Final review gate: `docs/wave5-final-enterprise-readiness-review.md` records final scorecard, all gate status, open risks, go/no-go decision and sign-off.
- Static Wave 5 gate: `scripts\Test-Wave5EnterpriseEvidence.ps1` checks Wave 5 evidence surfaces and prior wave gate scripts; use `-RequireCompletedEvidence` before final enterprise go-live.

## Go/no-go rule

Production go-live is blocked if any of these fail:

- Release build or test suite fails.
- Secret scan reports a real credential.
- Privileged MFA is not enabled for production roles.
- Staging smoke test fails.
- Backup restore drill has not been completed.
- Payment reconciliation cannot explain every booking/payment/refund state.
- Wave 5 strict evidence gate has not passed for enterprise go-live.
