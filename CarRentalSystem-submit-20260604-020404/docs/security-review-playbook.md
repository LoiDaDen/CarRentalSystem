# Security Review Playbook

Updated: 2026-05-17

## Cadence

- Run a security review every quarter and before major production releases.
- Run third-party pentest before B2B launch, fundraising security review, or public API launch.
- Track every finding with owner, due date, severity, remediation status, and evidence.

## Severity SLA

- Critical: block release, mitigate within 24 hours.
- High: fix or formally risk-accept within 7 days.
- Medium: fix within 30 days.
- Low: fix through normal backlog.

## Release-Blocking Rules

- Any open Critical finding blocks production and enterprise go-live.
- Any open High finding blocks enterprise go-live unless it has formal risk acceptance with owner, expiry date and compensating controls.
- Risk acceptance cannot be permanent; every accepted Critical/High risk must have an expiry date and follow-up review.
- Public API launch is blocked until API key lifecycle, rate limit, audit logging, webhook signing and vulnerability review all pass.
- Payment, eKYC, secure file, snapshot, tracking and admin/RBAC findings are treated as release-blocking when severity is Critical or High.
- The Wave 5 strict gate uses `docs/wave5-evidence/pentest-remediation-tracker.md` as the source of truth for external security review status.

## Required Inputs

- Latest CI build/test/security scan results.
- Dependency and container vulnerability report.
- Production config guard output.
- Data inventory and vendor list.
- Access review for Admin, Manager, Staff.
- Payment webhook and refund/payout reconciliation evidence.

## Risk Acceptance

Risk acceptance must include:

- Finding summary and exploit scenario.
- Business owner and technical owner.
- Compensating controls.
- Expiration date.
- Follow-up ticket.

Critical findings cannot be accepted without CTO/business owner sign-off.
