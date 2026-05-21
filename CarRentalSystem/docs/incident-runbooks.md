# Incident Runbooks

Updated: 2026-05-16

Use this file as the Phase 1 incident skeleton. Every real incident must produce timeline, impact, actions taken, customer impact, root cause, and follow-up tickets.

## Severity Matrix

- Critical: production down, data loss risk, money movement mismatch, confirmed account takeover, or double booking with customer impact.
- High: degraded checkout/payment/eKYC/admin operations, privileged suspicious login, refund dispute above business threshold.
- Medium: job backlog, partial provider outage, delayed reports, non-critical alert noise.
- Low: cosmetic/admin-only issue with no data or customer impact.

Evidence required for every High/Critical incident:

- Timeline with exact timestamps.
- Impacted users/bookings/payments.
- Logs, audit hash chain range, provider events, and database evidence.
- Owner, mitigation, root cause, and follow-up ticket.

## Payment Webhook Failure

Severity: High if checkout/payment confirmation is delayed; Critical if money is captured but booking state is wrong.

Immediate actions:

- Check PayOS provider status and webhook delivery logs.
- Check app logs for invalid signature, semantic duplicate, amount mismatch, or processing exception.
- Pause automatic payout/refund if reconciliation is uncertain.
- Re-run idempotent webhook processing only through approved admin/reconciliation tooling.
- Export booking, ledger, provider event, and audit trail for affected orders.

Resolution evidence:

- Affected order list.
- Reconciliation result per order.
- Confirmation that no order was double-processed.

## Database Outage

Severity: Critical when write path or login/checkout is unavailable.

Immediate actions:

- Confirm managed SQL service health and app connection errors.
- Stop non-critical background jobs if they increase load.
- Route users to maintenance/read-only page if available.
- Start restore/failover procedure if RTO risk exceeds threshold.
- Preserve logs before scaling or restart operations.

Resolution evidence:

- Outage start/end.
- RTO/RPO actuals.
- Data loss assessment.
- Slow query/deadlock findings if caused by load.

## eKYC Provider Outage

Severity: High when new self-drive customers cannot complete eligibility; Critical if approved/rejected license decisions are inconsistent.

Immediate actions:

- Check FPT eKYC provider status, app logs, and recent failed verification attempts.
- Disable automatic approval paths; self-drive rentals must require an approved active `CustomerDrivingLicense`.
- Keep pending applications in `Pending` and show a customer-facing retry/support path.
- Do not backfill legacy user license fields as approved during the outage.
- Export affected users, uploaded evidence metadata, provider responses, and audit trail.

Resolution evidence:

- Provider outage start/end.
- Count of affected users and retries.
- Confirmation that no unapproved license was promoted to approved.
- Follow-up test or ticket covering the failure mode.

## Job Backlog

Severity: Medium unless payment, contract, notification, or cleanup jobs are stale beyond SLA.

Immediate actions:

- Check Hangfire dashboard, failed jobs, retry storms, and queue age.
- Disable noisy recurring jobs if they block critical queues.
- Scale worker count only after DB load is checked.
- Replay failed jobs only when idempotency is confirmed.

Resolution evidence:

- Queue age before/after.
- Failed job types and root cause.
- Any customer-facing delay.

## Suspicious Login

Severity: High for privileged account attempts; Critical if account takeover is suspected.

Immediate actions:

- Confirm MFA status and recent privileged login audit events.
- Temporarily lock suspicious accounts.
- Rotate affected credentials and active sessions.
- Check unusual IP/user-agent patterns and failed reCAPTCHA spikes.
- Preserve audit logs for investigation.

Resolution evidence:

- Account list.
- Session revocation status.
- Credential rotation status.
- Customer/admin notification decision.

## Double Booking

Severity: Critical if two confirmed bookings overlap for the same car.

Immediate actions:

- Freeze affected car availability.
- Export bookings, holds, payment events, transition audit, and ledger entries.
- Identify winning reservation by transaction timestamp and payment state.
- Move conflicting booking to manual review or offer alternative car/refund.
- Open regression ticket against reservation/hold logic.

Resolution evidence:

- Affected car and booking IDs.
- Customer resolution.
- Root cause and regression test reference.

## Refund Dispute

Severity: Medium to High depending on amount and legal risk.

Immediate actions:

- Export dispute package: contract, handover evidence, return evidence, ledger, payment provider events, and audit trail.
- Confirm refund/deduction/payout approvals.
- Pause payout if dispute can affect partner settlement.
- Assign business owner for customer communication.

Resolution evidence:

- Final settlement amount.
- Approval chain.
- Provider transaction IDs.
- Customer/partner communication record.

## Rollback

Severity: follows triggering incident.

Immediate actions:

- Confirm rollback owner and target artifact version.
- Stop deploy pipeline if still running.
- Roll back app artifact first; roll back database only if migration is proven reversible or restore plan is approved.
- Run smoke tests after rollback.
- Confirm payment webhooks and jobs are healthy.

Resolution evidence:

- Rollback start/end.
- Artifact versions before/after.
- Migration status.
- Smoke test and monitoring screenshots.
