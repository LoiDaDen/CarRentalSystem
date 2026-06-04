# Wave 2 Production Operations Runbook

Updated: 2026-05-16

Scope: Wave 2 turns repository readiness into real operations evidence. This document is repo-first: it defines required cloud topology, drills, evidence and go/no-go gates. It does not provision cloud resources by itself.

## 1. Required Topology

Staging must be production-like and separate from local/IIS development.

Required components:

- Managed app hosting: Azure App Service, Container Apps, ECS/Fargate, Cloud Run, Kubernetes, or equivalent.
- Managed SQL Server with automated backup and point-in-time restore.
- Managed private storage for uploads, contracts, eKYC, handover evidence and dispute exports.
- Secret manager or deployment environment variables for all runtime secrets.
- HTTPS edge with WAF/CDN, explicit CORS origins and TLS 1.2+.
- Redis only if enabled for cache paths.
- Persistent Hangfire storage backed by managed SQL or production-grade storage.
- OTLP-compatible observability backend such as Grafana, Datadog, Azure Monitor or another collector.

Minimum staging settings:

- Use `appsettings.Staging.example.json` as the safe config shape.
- `Security:RequirePrivilegedMfa=true`.
- `Security:RequireRecaptcha=true`.
- `Audit:HashChainEnabled=true`.
- `Snapshot:AllowUnsafeDownload=false`.
- `Snapshot:AllowUnsafeRestore=false`.
- `Snapshot:AllowUnsafeDetails=false`.
- `Observability:OpenTelemetry:Enabled=true` with a real OTLP endpoint.
- `ApiDocs:Enabled=true` only when `ApiDocs:RequireAuth=true`.
- CORS is explicit HTTPS staging/production domains only.

## 2. ENT-INFRA-01 Staging Evidence

Required evidence:

- Topology diagram or text diagram naming every cloud resource.
- Config diff showing staging and production use the same security class.
- Staging smoke test output for `/live`, `/ready`, login, privileged MFA, checkout hold, PayOS webhook test mode, secure file access and admin SLO page.
- Owner and timestamp for the last smoke run.

Evidence template: `docs/wave2-evidence/staging-topology.md`.

## 3. ENT-SECRET-01 Secret Rotation

Rotate before any public traffic:

- SQL credential or managed identity assignment.
- PayOS client ID, API key, checksum key and partner code if used.
- SMTP user/password.
- FPT AI API key.
- reCAPTCHA secret.
- tracking ingest secret.
- admin PIN and bootstrap admin credential.
- data protection key storage access key if applicable.

Evidence must never include secret values. Store only provider, key name, rotation date, owner, old version revoked status and verification result.

Evidence template: `docs/wave2-evidence/secret-rotation.md`.

## 4. ENT-DR-01 Restore Drill

Targets:

- RTO <= 4 hours.
- RPO <= 1 hour.

SQL drill:

1. Restore latest staging/production-like backup to isolated SQL database.
2. Verify data timestamp and backup policy.
3. Run migration dry-run against restored database.
4. Run smoke tests against restored database.
5. Record start/end time, restored-to timestamp, owner and result.

Storage drill:

1. Restore uploads/contracts/eKYC/handover evidence to isolated storage.
2. Verify secure file ownership rules.
3. Export one dispute package from restored data.
4. Record object count and sample hashes.

Evidence template: `docs/wave2-evidence/restore-drill.md`.

## 5. ENT-OBS-01 Observability And Alert Drill

Minimum dashboards:

- Request rate, error rate, P95/P99 latency and saturation.
- SQL CPU/storage/deadlocks/slow queries/connection count.
- Hangfire processed/failed/retried jobs and backlog freshness.
- PayOS webhook success/failure/duplicate ignored/reconciliation gap.
- Booking hold success/failure/conflict rate and checkout conversion.

Minimum alert drills:

- App down or `/ready` unhealthy alerts within 1 minute.
- PayOS webhook failure or invalid signature spike alerts within 1 minute.
- Hangfire failed jobs or backlog older than 15 minutes.
- DB connectivity failure or connection error spike.
- Privileged MFA failures or suspicious login spike.

Evidence template: `docs/wave2-evidence/observability-alert-drill.md`.

## 6. ENT-RELEASE-01 Release And Rollback Drill

Required process:

1. Record release version, artifact hash and migration script.
2. Deploy to green/staging slot or canary target.
3. Run smoke tests before traffic switch.
4. Switch traffic.
5. Trigger rollback drill to previous stable artifact.
6. Prove rollback completes in under 5 minutes.
7. Record owner, timestamps, reason and result.

Evidence template: `docs/wave2-evidence/release-rollback-drill.md`.

## 7. Wave 2 Go/No-Go

Wave 2 is not complete until all of these are true:

- `scripts\Test-Wave2OperationsSafety.ps1` passes.
- Staging topology evidence is filled and reviewed.
- Secret rotation evidence confirms old keys were revoked.
- SQL and secure-file restore drill evidence proves RTO/RPO targets.
- Observability alert drill evidence proves app/webhook/job alerts.
- Rollback drill evidence proves rollback under 5 minutes.
- Phase 1, Phase 2, Phase 3 and Wave 1 safety scripts still pass.

No-go if any evidence is missing, stale, unsigned or includes raw secrets.
