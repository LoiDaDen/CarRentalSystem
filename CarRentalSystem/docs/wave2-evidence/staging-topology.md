# Wave 2 Evidence - Staging Topology

Status: TEMPLATE - fill with real environment evidence before marking Wave 2 complete.

## Metadata

| Field | Value |
|---|---|
| Evidence date | TBD |
| Owner | TBD |
| Cloud provider/account | TBD |
| Environment URL | TBD |
| Release version | TBD |
| Reviewed by | TBD |

## Resource Inventory

| Component | Resource name | Production-class? | Notes |
|---|---|---|---|
| App hosting | TBD | TBD | Managed app/container/Kubernetes |
| SQL database | TBD | TBD | PITR enabled |
| Private storage | TBD | TBD | Uploads/contracts/eKYC/evidence |
| Secret manager | TBD | TBD | No tracked production secrets |
| WAF/CDN/HTTPS edge | TBD | TBD | Explicit CORS, TLS 1.2+ |
| Redis | TBD | TBD | Required only if enabled |
| Hangfire storage | TBD | TBD | Persistent storage |
| OTLP/log backend | TBD | TBD | Grafana/Datadog/Azure Monitor/etc. |

## Smoke Test Results

| Check | Result | Timestamp | Evidence link |
|---|---|---|---|
| `/live` returns healthy | TBD | TBD | TBD |
| `/ready` returns healthy | TBD | TBD | TBD |
| Login + privileged MFA | TBD | TBD | TBD |
| Checkout hold | TBD | TBD | TBD |
| PayOS test webhook | TBD | TBD | TBD |
| Secure file authorization | TBD | TBD | TBD |
| Admin SLO page | TBD | TBD | TBD |

## Sign-Off

Decision: TBD

Approver: TBD
