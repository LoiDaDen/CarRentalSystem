# Phase 3 SRE Runbook

Updated: 2026-05-16

## Scope

This runbook defines the repo-first SRE operating model for CarRentalSystem. Real cloud work still needs platform credentials, but every production release must use these checks.

## SLI/SLO

| SLI | Target | Source |
|---|---:|---|
| Availability | >= 99.9% | `/health`, uptime monitor, audit outage signals |
| Checkout success | >= 95% | bookings confirmed/active/completed over booking attempts |
| Payment webhook success | >= 99% | `PaymentWebhookEvents` processed over received |
| Payment webhook P95 latency | <= 60s | `ReceivedAt` to `ProcessedAt` |
| Booking conflict rate | <= 1% | conflict audit signals over holds |
| Job freshness | <= 15 minutes | Hangfire/job audit signal |

## Release Gate

Block release when:

- Error budget remaining is below 25%.
- `/ready` is unhealthy in staging.
- Payment webhook smoke test fails.
- Migration rollback plan is missing.
- Rollback owner is not assigned.

## Blue/Green Or Canary

1. Deploy new build to inactive slot or canary pool.
2. Run smoke tests against `/live`, `/ready`, login, checkout start, PayOS webhook test endpoint if available, and admin dashboard.
3. Shift 5% traffic for 10 minutes.
4. Check SLO dashboard, payment webhook latency, error rate, and job freshness.
5. Shift to 50%, then 100% only if no Sev1/Sev2 signals appear.

## Rollback Under 5 Minutes

1. Stop traffic shift immediately.
2. Route traffic back to last healthy slot.
3. Disable risky feature flags.
4. Confirm `/ready` healthy on previous slot.
5. Record incident audit note and attach deploy version, migration status, and payment reconciliation status.

## Failover Drill

Run at least quarterly:

- App instance restart.
- Managed SQL failover.
- Storage read/write validation.
- Redis unavailable path if Redis is optional.
- Hangfire worker restart.

Evidence must include timestamps, actual RTO/RPO, failed checks, and remediation owner.
