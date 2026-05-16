# Phase 1 Production Safety Runbook

Updated: 2026-05-16

Scope: repository-first Phase 1 safety foundation for CarRentalSystem. This runbook does not create cloud resources by itself; it defines the required topology, checks, owners, and evidence before real production traffic is allowed.

## 1. Target Staging Topology

Staging must be production-like, not a local IIS substitute.

Required components:

- App runtime: managed app service, container app, or Kubernetes workload.
- Database: managed SQL Server with point-in-time restore enabled.
- Storage: managed private blob/file storage for uploads, contracts, eKYC, handover evidence, and logs that must survive app redeploy.
- Secrets: cloud secret manager or deployment environment variables. No production secret may live in tracked JSON.
- Network edge: HTTPS, WAF/CDN, explicit CORS origins, TLS 1.2+.
- Cache: Redis when enabled for catalog/search/reference data.
- Jobs: Hangfire storage backed by managed SQL or another production-grade persistent store.

Minimum staging config:

- `ASPNETCORE_ENVIRONMENT=Production` for final release smoke tests, or a separate `Staging` environment with production-equivalent security flags.
- `Security:RequirePrivilegedMfa=true`.
- `Security:RequireRecaptcha=true`.
- `Snapshot:AllowUnsafeDownload=false`.
- `Snapshot:AllowUnsafeRestore=false`.
- `Tracking:IngestSecret` set to a long random secret.
- `Cors:AllowedOrigins` set to explicit HTTPS domains only.

## 2. Secret Rotation Checklist

Before go-live:

- Rotate SQL password or managed identity credential.
- Rotate PayOS client ID/API key/checksum key.
- Rotate SMTP user/password.
- Rotate FPT AI API key.
- Rotate reCAPTCHA secret.
- Rotate tracking ingest secret.
- Rotate admin PIN and any bootstrap admin credential.
- Confirm old secrets are revoked at the provider.
- Run `scripts\Test-TrackedSecrets.ps1 -RepoRoot .`.

Evidence to keep:

- Rotation date, owner, provider, and verification result.
- Screenshot/export from secret manager showing current version metadata, not secret values.
- CI secret scan result.

## 3. Backup And Restore Drill

Targets:

- RTO: <= 4 hours.
- RPO: <= 1 hour.

SQL drill:

1. Confirm automated backup/PITR is enabled.
2. Restore the latest production-like backup to an isolated database.
3. Run migration compatibility check.
4. Run smoke tests against restored DB.
5. Record restore start time, restore end time, data timestamp, and validation owner.

Storage drill:

1. Restore uploads/contracts/eKYC/evidence storage to an isolated container.
2. Confirm secure file download still respects ownership and admin policy.
3. Export one dispute evidence package from restored data.
4. Record evidence object count and sample hashes.

Go-live is blocked if no successful restore drill evidence exists.

## 4. Monitoring And Alerting Checklist

Minimum alerts:

- App down or `/health` failing for more than 1 minute.
- Database unavailable or connection error spike.
- Hangfire job failures or backlog older than 15 minutes.
- PayOS webhook failures, invalid signature spikes, or duplicate processing attempts.
- eKYC provider failures or throttling spike.
- Suspicious login rate or privileged MFA failures.
- Disk/storage quota above 80%.

Minimum dashboards:

- Request rate, error rate, latency P95/P99, and saturation.
- SQL CPU, storage, deadlocks, slow queries, connection count.
- Hangfire processed/failed/retried jobs.
- Payment webhook count, success/failure, duplicate ignored, reconciliation gap.
- Booking hold success/failure, conflict rate, checkout conversion.

## 5. Release Checklist

Before production deploy:

- Release build passes.
- Full test suite passes.
- Secret scan passes.
- Phase 1 safety script passes.
- Migration script is generated and reviewed.
- Staging smoke tests pass with production-equivalent flags.
- Payment webhook test passes for valid, duplicate, invalid signature, and amount mismatch cases.
- Rollback owner is online.
- Backup restore drill evidence is less than 30 days old.
- Current deployment artifact version is recorded.

Rollback target:

- Phase 1 target: rollback in < 15 minutes.
- Phase 3 target: rollback in < 5 minutes with blue/green or canary.

## 6. Payment Reconciliation Check

Before release:

- Confirm every PayOS event maps to a booking/order semantic key.
- Confirm duplicate webhook is ignored without double ledger entry.
- Confirm refund, deduction, and payout remain manual approval in Phase 1.
- Confirm late payment after expired hold enters review flow.
- Export reconciliation report for at least one successful booking and one failed/duplicate webhook case.

## 7. Production Go/No-Go

No-go if any condition is true:

- Production config guard fails.
- `Security:RequireRecaptcha` or privileged MFA is disabled.
- CORS contains wildcard or empty origin.
- Unsafe snapshot download/restore flag is enabled.
- Backup restore drill is missing or failed.
- Monitoring has no alert for app down, DB down, job failures, or payment webhook failures.
- Payment reconciliation cannot explain every money movement.
