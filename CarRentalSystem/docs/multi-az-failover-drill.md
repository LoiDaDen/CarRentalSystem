# Multi-AZ Failover Drill

Updated: 2026-05-17

## Objective

Prove CarRentalSystem can survive an availability-zone failure with RTO <= 4 hours and RPO <= 1 hour.

## Preconditions

- App deployed to at least two zones or equivalent managed platform redundancy.
- Managed SQL PITR and zone redundancy enabled.
- Uploads/contracts/evidence storage has backup or replication.
- Secret manager is reachable from all zones.
- Hangfire/job storage is production-class and reachable from the standby zone.
- PayOS webhook URL, callback URL and public DNS route through the failover path.
- `/live`, `/ready`, `/health`, and SLO dashboard are available before the drill.
- On-call owner, rollback owner and database owner are assigned for the drill window.

## Drill Steps

1. Capture baseline: release version, deployment slot, `/ready`, SLO dashboard, active worker count, DB backup timestamp and storage replication status.
2. Simulate app-zone failure by draining or stopping one app zone/instance group.
3. Verify DNS/load balancer routes traffic to healthy app instances and `/ready` returns healthy.
4. Trigger managed SQL failover in staging or production-like environment.
5. Validate customer login, checkout read paths, booking hold, admin login, SLO dashboard and payment webhook processing.
6. Validate Hangfire/job freshness and no stuck payment/notification jobs.
7. Validate secret manager access from the active zone.
8. Restore one uploaded evidence/contract file from backup or replicated storage.
9. Send a PayOS webhook test event or replay a signed staging webhook event and verify idempotent processing.
10. Re-enable failed zone and verify app/job stability for at least 30 minutes.
11. Record follow-up issues, owner, due date and whether the release remains allowed.

## Evidence Template

Use `docs/wave5-evidence/multi-az-failover-report.md` for the signed drill evidence.
