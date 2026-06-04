# Roadmap Enterprise Production-Grade Cho CarRentalSystem

Updated: 2026-05-16

Latest benchmark audit after Wave 1-5 repo gates: [`docs/enterprise-benchmark-audit-2026-05-17.md`](enterprise-benchmark-audit-2026-05-17.md).

Completion roadmap after the 2026-05-16 enterprise benchmark audit: [`docs/enterprise-completion-roadmap.md`](enterprise-completion-roadmap.md).

## 1. Executive Summary

Muc tieu cua roadmap nay la dua CarRentalSystem tu muc production-MVP foundation len production-grade enterprise co the van hanh that tren cloud managed infrastructure.

Chuan "production thuc thu" trong tai lieu nay khong co nghia la hoan hao tuyet doi. He thong chi duoc xem la san sang production khi co bang chung kiem chung duoc: security gates, automated tests, backup/restore drill, monitoring/alerting, payment reconciliation, rollback, audit trail va quy trinh van hanh cloud.

Baseline hien tai dua tren audit repo ngay 2026-05-16:

| Tieu chi | Diem hien tai | Muc tieu | Trang thai |
|---|---:|---:|---|
| Kien truc he thong | 64% | 100% | Yellow |
| Bao mat & Tuan thu | 61% | 100% | Yellow |
| Do tin cay & San sang | 52% | 100% | Red |
| Hieu nang | 56% | 100% | Yellow |
| Chat luong Code & DevOps | 70% | 100% | Yellow |
| UX & Van hanh | 58% | 100% | Yellow |
| Phan tich kinh doanh | 55% | 100% | Yellow |
| **Tong** | **59%** | **100%** | **Yellow** |

Current strengths:

- Release build, test suite va tracked secret scan dang pass.
- Da co auth, role authorization, privileged email OTP MFA, global antiforgery, Hangfire, health check, audit log, eKYC, PayOS, cart/booking flow va booking hold concept.
- Da co production readiness gates rieng tai `docs/enterprise-production-readiness-gates.md`.

Implementation status:

- Phase 0 P0 blockers: implemented in repo. Cancel booking, same-car cart gap, secure file whitelist/audit, snapshot production block, tracking nonce replay protection, eKYC upload validation, and PayOS semantic idempotency have regression coverage.
- Phase 1 repository gates: in progress. Current scope covers production reCAPTCHA guard, production config fail-fast, CSP `unsafe-eval` removal in production, CI vulnerability gates, Phase 1 safety script, and runbooks. Cloud resources, secret rotation, WAF/CDN, uptime monitor, and centralized log platform still require real environment provisioning.
- Phase 2 operating model: implemented in repo for SSO config guard, audit hash chain, contract/handover evidence hashes, dispute export, settlement review, DB indexes, catalog cache, Phase 2 safety script, ADRs, data classification, security review playbook, and developer onboarding. Real provider app registration, third-party pentest, and real load test execution remain environment work.
- Phase 3 enterprise excellence: implemented repo-first for `/live`, `/ready`, SLO dashboard, OpenTelemetry configuration, PII-safe telemetry wrapper, localization foundation, analytics no-op provider, API docs production guard, Phase 3 runbooks, and Phase 3 safety script. Real OpenTelemetry backend, blue/green platform, multi-AZ cloud failover, analytics vendor, and WCAG external audit remain environment/vendor work.
- Completion Wave 2 operations: implemented repo-first for production-like staging config shape, operations runbook, evidence templates, and `scripts\Test-Wave2OperationsSafety.ps1`. Real cloud staging, secret rotation, restore drill, alert drill and rollback drill still require signed evidence before public traffic.
- Completion Wave 3 performance/quality/compliance: implemented repo-first for DB-side admin aggregates, load-test evidence templates, critical-path coverage gate, runtime privacy request workflow, retention preview, sensitive audit hash-chain conversion and Wave 3 safety scripts. Real load-test evidence still requires staging execution.
- Completion Wave 4 UX/trust/business platform: implemented repo-first for support/dispute ticket workflow, customer self-service, fleet readiness dashboard, API key lifecycle, outbound webhook signing, public API production guard, Wave 4 evidence templates and safety scripts. Real WCAG audit, support SLA proof, fleet ops review and API readiness sign-off still require staging evidence.
- Completion Wave 5 enterprise certification evidence: implemented repo-first for multi-AZ failover evidence, SOC2-ready control matrix, pentest remediation tracker, final readiness review template and `scripts\Test-Wave5EnterpriseEvidence.ps1`. Real failover drill, SOC2 evidence completion, external pentest/security review and executive sign-off remain required before 98-100% enterprise readiness.

Main production blockers:

- Booking/payment/car/deposit state mutation con nam rai rac trong controller va service.
- Con critical logic bugs trong cancel booking va cart same-car gap validation.
- Snapshot JSON co nguy co chua PII/password hash va khong phai backup production an toan.
- Tracking HMAC chua co replay protection.
- Chua co cloud staging production-like, centralized observability, restore drill, rollback drill, SLO va payment reconciliation day du.

## 2. Top Critical Bugs And Risks

| ID | Priority | Risk | Impact | Required outcome |
|---|---|---|---|---|
| BUG-01 | P0 | Huy booking `Confirmed` set status/deposit/car roi redirect nhung khong persist ro rang truoc khi return. | Booking co the khong bi huy, xe/deposit lech trang thai. | Regression test chung minh cancel `Confirmed` persist dung booking, car, deposit, audit. |
| BUG-02 | P0 | Cart minimum 1-hour gap dung `Math.Min()` sai cho 2 khoang thoi gian cung xe. | Co the tao lich gan nhau duoi 1 gio hoac conflict van hanh. | Sort intervals va test overlap/gap 30m/60m/multi-item. |
| BUG-03 | P0 | State mutation truc tiep cho `BookingStatusID`, `DepositStatus`, `PaymentStatus`, `CarStatusID`. | Tao trang thai bat hop le, kho audit, kho dispute/refund. | Moi transition di qua service co actor, reason, audit va transaction. |
| SEC-PII-01 | P0 | Snapshot JSON include `Users` va raw download cho Admin. | Data exfiltration neu admin/session bi compromise; khong dat chuan PII governance. | Disable raw PII snapshot production hoac encrypt + audit + least privilege. |
| SEC-TRACK-01 | P0 | Tracking HMAC co timestamp window nhung khong co nonce/replay store. | Request bi capture co the replay trong 5 phut. | Them nonce/device event id va reject replay. |
| SEC-FILE-01 | P0 | Secure file path dung `folder` input lam base path, chua whitelist. | Mo rong surface truy cap file nhay cam. | Whitelist folder/entity type, ownership policy, audit download. |
| PAY-01 | P0 | PayOS webhook idempotency con phu thuoc mot phan vao raw payload hash. | Duplicate/variant event co the gay xu ly lap hoac reconciliation kho. | Semantic idempotency theo provider/order/event va reconciliation report. |
| OPS-01 | P0 | Chua co restore drill, rollback drill, alert drill. | Khong chung minh duoc production recovery. | RTO/RPO drill co bien ban va owner. |

## 3. Phase 0 - Production Blockers, Week 0-2

Phase nay phai xong truoc khi mo traffic production that.

| ID | Task | Priority | Owner | Effort | Impact | Definition of Done |
|---|---|---|---|---:|---|---|
| BUG-01 | Fix cancel booking `Confirmed` persist bug. | P0 | TBD | 1 day | High | Huy booking confirmed save dung booking status, deposit status, car available; co regression test. |
| BUG-02 | Fix same-car cart minimum gap validation. | P0 | TBD | 1 day | High | Test pass cho overlap, gap 30 phut bi reject, gap 60 phut duoc accept. |
| ARCH-STATE-01 | Tao rule: controller khong set truc tiep critical states. | P0 | TBD | 3 days | High | Booking/payment/car/deposit transition di qua transition service, co actor/reason/audit. |
| SEC-SNAPSHOT-01 | Block raw PII snapshot download in production. | P0 | TBD | 2 days | High | Production khong download raw snapshot chua PII; moi download co audit log. |
| SEC-TRACK-01 | Them tracking replay protection. | P0 | TBD | 2 days | High | Payload cu/nonce da dung bi reject; co test replay. |
| SEC-FILE-01 | Whitelist secure upload folders va ownership policy. | P0 | TBD | 2 days | High | Folder ngoai whitelist bi reject; owner/admin access test pass. |
| SEC-EKYC-01 | Harden eKYC upload validation. | P0 | TBD | 2 days | High | Check extension, MIME/magic bytes, decode image, size; khong expose raw exception. |
| PAY-IDEMP-01 | Chuan hoa PayOS semantic idempotency. | P0 | TBD | 3 days | High | Duplicate webhook cung order/event khong double-process; amount mismatch/late payment tests pass. |

Exit criteria:

- Khong con P0 logic bug trong booking/payment/PII.
- Regression tests cover cac bug P0.
- Release build, tests va secret scan pass.

## 4. Phase 1 - Production Safety Foundation, Month 1-3

### 4.1 Security Foundation

| ID | Task | Priority | Owner | Effort | Impact | Definition of Done |
|---|---|---|---|---:|---|---|
| SEC-01 | Rotate all secrets tung dung ngoai secret manager. | P0 | TBD | 1 day | High | DB/API/SMTP/PayOS/FPT/Recaptcha keys moi; old keys revoked; secret scan pass. |
| SEC-02 | Move production secrets to cloud secret manager/deployment env. | P0 | TBD | 2 days | High | Khong co production credential trong tracked files, logs, scripts. |
| SEC-03 | Enforce privileged MFA for Admin/Manager/Staff. | P0 | TBD | 1 day | High | Privileged users khong login duoc neu chua pass MFA. |
| SEC-04 | Upgrade MFA path from email OTP to TOTP/WebAuthn plan. | P1 | TBD | 5 days | Medium | Privileged roles co enrolled factor; recovery codes documented. |
| SEC-05 | Re-enable reCAPTCHA or rate limit login/register. | P0 | TBD | 2 days | High | Brute-force tests/rate-limit tests pass; no localhost bypass in production. |
| SEC-06 | Harden CSP and security headers. | P1 | TBD | 3 days | Medium | Production CSP bo `unsafe-eval`; inline scripts reduced with nonce/hash plan. |
| SEC-07 | Add dependency/SAST/container scan gates. | P0 | TBD | 3 days | High | CI fail on critical/high findings, no non-blocking critical scan. |

### 4.2 Booking And Payment Consistency

| ID | Task | Priority | Owner | Effort | Impact | Definition of Done |
|---|---|---|---|---:|---|---|
| ARCH-01 | All booking entry points use `ReservationService`. | P0 | TBD | 5 days | High | Single booking, cart, long-term, admin/manual booking share hold/conflict logic. |
| ARCH-02 | Centralize booking/payment/car/deposit transitions. | P0 | TBD | 5 days | High | No controller direct mutation for critical states without transition service. |
| PAY-02 | Implement append-only money ledger standard. | P0 | TBD | 5 days | High | Rental fee, deposit, refund, deduction, partner payout, driver payout trace to booking/provider event. |
| PAY-03 | Manual approval refund/payout MVP. | P0 | TBD | 4 days | High | Refund/payout states include pending, approved, failed, retried, reconciled. |
| PAY-04 | Payment reconciliation report. | P0 | TBD | 3 days | High | PayOS transaction report matches internal payment/ledger daily. |
| EKYC-01 | Enforce `EligibilityService` before hold/payment/pickup. | P0 | TBD | 2 days | High | Unverified/ineligible user cannot hold, pay, or pickup. |

### 4.3 Cloud Staging And Operations

| ID | Task | Priority | Owner | Effort | Impact | Definition of Done |
|---|---|---|---|---:|---|---|
| OPS-01 | Create cloud staging production-like environment. | P0 | TBD | 5 days | High | Staging has managed SQL, app/container hosting, storage, Redis if enabled, WAF/CDN, production-like config. |
| OPS-02 | Create production deployment checklist. | P0 | TBD | 2 days | High | Go-live checklist approved; every release has owner, rollback, migration plan. |
| OPS-03 | Backup SQL with point-in-time restore. | P0 | TBD | 2 days | High | PITR enabled; restore drill evidence stored. |
| OPS-04 | Backup secure uploads/contracts/evidence storage. | P0 | TBD | 2 days | High | Storage backup lifecycle documented and tested. |
| MON-01 | Centralized logging and error tracking. | P0 | TBD | 3 days | High | App/job/payment/eKYC errors searchable and alertable. |
| MON-02 | Uptime and health alerting. | P0 | TBD | 1 day | High | Alert fires within 1 minute for app down/webhook/job failures. |
| DEVOPS-01 | CI release gate hardening. | P0 | TBD | 3 days | High | Build, test, secret scan, dependency scan, migration dry-run, staging smoke all block production. |

Exit criteria:

- Staging mirrors production class of config.
- Restore drill success with RTO <= 4h and RPO <= 1h.
- Payment live webhook safety tests pass.
- Auto payout remains disabled until reconciliation drill passes.

## 5. Phase 2 - Enterprise Operating Model, Month 4-6

### 5.1 Architecture And Performance

| ID | Task | Priority | Owner | Effort | Impact | Definition of Done |
|---|---|---|---|---:|---|---|
| PERF-01 | Add Redis cache for catalog/search/reference data. | P1 | TBD | 4 days | Medium | Cache hit ratio > 80% for read-heavy paths. |
| PERF-02 | Move admin booking/dashboard/report filtering to DB. | P1 | TBD | 5 days | High | No large unbounded `ToList()` before filters on admin critical pages. |
| PERF-03 | Add/tune DB indexes for overlap, payment lookup, admin lists. | P1 | TBD | 3 days | High | Slow query > 1s triaged; execution plans reviewed. |
| PERF-04 | Run baseline load test and tune bottlenecks. | P1 | TBD | 5 days | High | Critical flows P95 < 500ms at agreed target load. |

### 5.2 Security And Compliance

| ID | Task | Priority | Owner | Effort | Impact | Definition of Done |
|---|---|---|---|---:|---|---|
| SEC-08 | Add OIDC/SSO for admin/staff. | P1 | TBD | 7 days | High | Google/Microsoft OIDC works for privileged users. |
| SEC-09 | Tamper-resistant audit trail. | P1 | TBD | 5 days | High | Role/payment/eKYC/contract/payout changes have actor, reason, timestamp, immutable hash chain or append-only store. |
| SEC-10 | Quarterly security review/pentest process. | P1 | TBD | 5 days | High | Critical/high findings remediated or risk-accepted with owner. |
| COMP-01 | Data inventory and classification. | P1 | TBD | 4 days | High | PII/payment/contract/tracking/audit data classified with retention owner. |

### 5.3 Legal Evidence And Disputes

| ID | Task | Priority | Owner | Effort | Impact | Definition of Done |
|---|---|---|---|---:|---|---|
| LEGAL-01 | Contract versioning and rendered PDF hash. | P1 | TBD | 5 days | High | Contract has version, content hash, signer identity snapshot, IP, user-agent, timestamp. |
| LEGAL-02 | Handover evidence package. | P1 | TBD | 5 days | High | Photos, odo, fuel, signature, geo/time, hashes stored immutably. |
| LEGAL-03 | Dispute export package. | P1 | TBD | 4 days | High | Export includes contract, evidence, ledger, payment provider events, audit trail. |
| PAY-05 | Settlement review workflow. | P1 | TBD | 5 days | High | Late/fuel/cleaning/damage/toll/violation deductions require review and audit. |

### 5.4 Quality And Documentation

| ID | Task | Priority | Owner | Effort | Impact | Definition of Done |
|---|---|---|---|---:|---|---|
| QA-01 | Raise business-critical service/domain coverage to >= 80%. | P1 | TBD | 14 days | High | CI coverage gate fails below threshold for critical projects/paths. |
| QA-02 | Add integration tests for top flows. | P1 | TBD | 10 days | High | Register/eKYC, hold/payment, cart multi-car, cancel/refund, handover/settlement, partner approval, admin RBAC pass. |
| DOC-01 | ADRs for core architecture. | P1 | TBD | 5 days | Medium | ADRs exist for reservation, payment ledger, eKYC, contract evidence, cloud topology. |
| DOC-02 | Incident runbooks. | P1 | TBD | 5 days | High | Runbooks exist for webhook failure, DB outage, job backlog, suspicious login, double booking, refund dispute, rollback. |
| DOC-03 | Developer onboarding guide. | P1 | TBD | 2 days | Medium | New developer can setup local in < 30 minutes. |

Exit criteria:

- PR fails if build/test/security gates fail.
- Top business flows have automated integration tests.
- Dispute package can be exported from production data model.
- Audit and compliance evidence can be shown to business/security reviewers.

## 6. Phase 3 - Enterprise Excellence, Month 7-12

### 6.1 SRE And High Availability

| ID | Task | Priority | Owner | Effort | Impact | Definition of Done |
|---|---|---|---|---:|---|---|
| SRE-01 | Define SLI/SLO. | P2 | TBD | 5 days | High | Availability, checkout success, payment webhook latency, booking conflict rate, job freshness tracked. |
| SRE-02 | Blue/green or canary deploy. | P2 | TBD | 7 days | High | Zero-downtime deploy; rollback < 5 minutes. |
| SRE-03 | Multi-AZ managed SQL/app deployment. | P2 | TBD | 14 days | High | App survives single AZ failure; failover drill documented. |
| SRE-04 | Error budget dashboard. | P2 | TBD | 5 days | Medium | Release decisions include error budget status. |

### 6.2 Full Observability

| ID | Task | Priority | Owner | Effort | Impact | Definition of Done |
|---|---|---|---|---:|---|---|
| OBS-01 | Add OpenTelemetry traces. | P2 | TBD | 7 days | High | MVC request -> EF Core -> PayOS/eKYC -> Hangfire trace visible. |
| OBS-02 | Business KPI dashboard. | P2 | TBD | 5 days | Medium | Bookings, revenue, refund rate, payout, utilization, conversion, eKYC approval visible. |
| OBS-03 | Alert severity and routing cleanup. | P2 | TBD | 4 days | Medium | Alert noise reduced; every critical alert has owner and runbook. |

### 6.3 Compliance And Governance

| ID | Task | Priority | Owner | Effort | Impact | Definition of Done |
|---|---|---|---|---:|---|---|
| COMP-02 | GDPR-style privacy controls. | P2 | TBD | 10 days | High | Consent, retention, export/delete request process documented and enforced. |
| COMP-03 | Vendor/DPA/security evidence folder. | P2 | TBD | 5 days | Medium | PayOS/eKYC/cloud/email vendor evidence and contacts stored. |
| COMP-04 | SOC2-ready control mapping. | P2 | TBD | 14 days | Medium | Controls mapped with owner, evidence, gap plan. |

### 6.4 UX, Operations, And Business Platform

| ID | Task | Priority | Owner | Effort | Impact | Definition of Done |
|---|---|---|---|---:|---|---|
| UX-01 | WCAG 2.1 AA audit. | P2 | TBD | 10 days | Medium | Checkout, eKYC, admin booking, payment, contract have no serious/critical axe issues. |
| UX-02 | Vietnamese/English i18n foundation. | P2 | TBD | 7 days | Medium | Date/time/currency locale-aware; no mojibake in user-facing flows. |
| OPS-05 | Support SLA and NPS/feedback loop. | P2 | TBD | 5 days | Medium | Support response tiers and feedback-to-ticket process active. |
| BIZ-01 | Product analytics. | P2 | TBD | 5 days | Medium | Funnel, retention, feature adoption metrics decision-ready. |
| BIZ-02 | Public API/developer portal readiness. | P2 | TBD | 10 days | Medium | Only launched after RBAC, rate limit, audit, key management, webhook signing are stable. |
| BIZ-03 | Partner/accounting/CRM integrations. | P2 | TBD | 14 days | Medium | Webhook/integration platform supports signed outbound events and retries. |

Exit criteria:

- Uptime >= 99.9%.
- Rollback < 5 minutes.
- Critical traces are end-to-end.
- Compliance evidence folder is review-ready.
- UX/accessibility issues are triaged and remediated.

## 7. Production Go/No-Go Checklist

Production go-live is blocked if any item below is not done:

- Release build passes.
- Full test suite passes.
- Secret scan reports no real credential.
- Dependency/SAST/container scans have no open critical/high vulnerability.
- Privileged MFA enabled for Admin/Manager/Staff.
- Production secrets stored outside tracked files.
- Staging smoke test passes.
- SQL backup and restore drill completed.
- Secure uploads/contracts/evidence backup drill completed.
- Payment reconciliation explains every booking/payment/refund state.
- PayOS webhook tests pass: valid signature, duplicate, amount mismatch, late payment after hold expiry.
- Rollback procedure tested.
- Monitoring alerts tested for app down, DB issue, webhook failure, job backlog.
- No P0 bug remains open.

## 8. Monthly Tracking Template

| Month | Target score | Phase | Tasks done | Required evidence | Notes |
|---|---:|---|---|---|---|
| Month 1 | 65% | Phase 0/1 | TBD | P0 bug tests, secret rotation evidence | |
| Month 2 | 70% | Phase 1 | TBD | staging, backup config, security gates | |
| Month 3 | 75% | Phase 1 | TBD | restore drill, monitoring, payment reconciliation MVP | |
| Month 4 | 80% | Phase 2 | TBD | DB performance fixes, audit upgrades | |
| Month 5 | 84% | Phase 2 | TBD | integration tests, legal evidence package | |
| Month 6 | 88% | Phase 2 | TBD | runbooks, SSO, pentest/security review | |
| Month 7 | 90% | Phase 3 | TBD | SLI/SLO dashboard | |
| Month 8 | 92% | Phase 3 | TBD | OpenTelemetry traces | |
| Month 9 | 94% | Phase 3 | TBD | blue/green or canary rollback drill | |
| Month 10 | 96% | Phase 3 | TBD | compliance/privacy controls | |
| Month 11 | 98% | Phase 3 | TBD | WCAG/i18n/support SLA | |
| Month 12 | 100% | Phase 3 | TBD | final production readiness review | |

## 9. Acceptance Test Plan

Every pull request:

- `dotnet build CarRentalSystem.csproj --configuration Release --no-restore /p:UseAppHost=false`
- `dotnet test ..\CarRentalSystem.Tests\CarRentalSystem.Tests.csproj --configuration Release --no-restore /p:UseAppHost=false`
- `powershell -ExecutionPolicy Bypass -File scripts\Test-TrackedSecrets.ps1 -RepoRoot .`
- Dependency/SAST/container scan with critical/high findings blocking merge.
- EF migration script dry-run.

Security acceptance:

- RBAC negative tests for admin/internal endpoints.
- CSRF tests for state-changing browser actions.
- Tracking HMAC replay tests.
- Secure file ownership and folder whitelist tests.
- eKYC upload file validation tests.
- Secret scan with no true positives.

Business acceptance:

- Concurrent hold for same car only allows one active hold.
- Cart multi-car checkout is atomic.
- Cancel/refund states are consistent and auditable.
- PayOS webhook idempotency prevents duplicate processing.
- Expired hold payment goes to `UnderReview`.
- Refund/deduction/payout reconciliation balances against ledger.
- eKYC eligibility blocks hold/payment/pickup where required.

Ops acceptance:

- Staging smoke test passes.
- Backup restore drill passes.
- Failover drill passes when HA is enabled.
- Rollback drill completes within target.
- Alert routing tested for app, DB, Hangfire, payment webhook, storage.

Performance acceptance:

- Baseline load test completed in Phase 1.
- Critical customer/admin flows P95 < 500ms at agreed target load by Phase 2.
- Performance regression test enters CI by Phase 3.

## 10. Operating Rules

- No production release if any P0 is open.
- No auto payout until reconciliation drill has passed.
- No public API/developer portal until RBAC, rate limiting, audit and key management are stable.
- JSON snapshot is not production backup unless encrypted, access-controlled, audited and restored safely.
- Every critical state transition must have actor, reason, timestamp, audit entry and transaction boundary.
- Every incident must produce either a runbook update, test update or monitoring update.
