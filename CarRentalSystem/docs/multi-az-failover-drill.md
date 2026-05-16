# Multi-AZ Failover Drill

Updated: 2026-05-16

## Objective

Prove CarRentalSystem can survive an availability-zone failure with RTO <= 4 hours and RPO <= 1 hour.

## Preconditions

- App deployed to at least two zones or equivalent managed platform redundancy.
- Managed SQL PITR and zone redundancy enabled.
- Uploads/contracts/evidence storage has backup or replication.
- Secret manager is reachable from all zones.
- `/live`, `/ready`, `/health`, and SLO dashboard are available.

## Drill Steps

1. Capture baseline: `/ready`, SLO dashboard, active worker count, DB backup timestamp.
2. Simulate app-zone failure by draining or stopping one zone.
3. Verify traffic routes to healthy app instances.
4. Trigger managed SQL failover in staging.
5. Validate checkout read paths, booking hold, admin login, and payment webhook processing.
6. Restore one evidence file from backup or replicated storage.
7. Re-enable failed zone and verify worker/job freshness.

## Evidence Template

| Item | Value |
|---|---|
| Drill date | TBD |
| Environment | Staging |
| Release version | TBD |
| Start time UTC | TBD |
| Recovery time | TBD |
| Data loss window | TBD |
| RTO pass/fail | TBD |
| RPO pass/fail | TBD |
| Owner | TBD |
| Follow-up issues | TBD |
