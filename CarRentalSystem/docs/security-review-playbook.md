# Security Review Playbook

Updated: 2026-05-16

## Cadence

- Run a security review every quarter and before major production releases.
- Run third-party pentest before B2B launch, fundraising security review, or public API launch.
- Track every finding with owner, due date, severity, remediation status, and evidence.

## Severity SLA

- Critical: block release, mitigate within 24 hours.
- High: fix or formally risk-accept within 7 days.
- Medium: fix within 30 days.
- Low: fix through normal backlog.

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
