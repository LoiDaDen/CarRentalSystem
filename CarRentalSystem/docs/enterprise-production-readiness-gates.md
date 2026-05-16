# Enterprise production readiness gates

Updated: 2026-05-16

This document turns the enterprise roadmap into release gates that can be verified from the repository and production runbooks.

Roadmap source of truth: [`docs/roadmap.md`](roadmap.md).
Phase 1 runbook: [`docs/phase1-production-safety-runbook.md`](phase1-production-safety-runbook.md).
Incident runbooks: [`docs/incident-runbooks.md`](incident-runbooks.md).
Phase 3 SRE runbook: [`docs/phase3-sre-runbook.md`](phase3-sre-runbook.md).

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

## Go/no-go rule

Production go-live is blocked if any of these fail:

- Release build or test suite fails.
- Secret scan reports a real credential.
- Privileged MFA is not enabled for production roles.
- Staging smoke test fails.
- Backup restore drill has not been completed.
- Payment reconciliation cannot explain every booking/payment/refund state.
