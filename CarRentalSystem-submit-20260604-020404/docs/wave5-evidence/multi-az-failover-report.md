# Wave 5 Evidence - Multi-AZ Failover Report

Status: TEMPLATE - fill after a real staging or production-like failover drill.

| Field | Value |
|---|---|
| Evidence date | TBD |
| Environment | TBD |
| Cloud region / AZs | TBD |
| Release version | TBD |
| Drill owner | TBD |
| Start time UTC | TBD |
| End time UTC | TBD |
| Actual recovery time | TBD |
| Actual data loss window | TBD |
| RTO target | <= 4h |
| RPO target | <= 1h |
| RTO result | TBD |
| RPO result | TBD |
| Overall result | TBD |

## Timeline

| Time UTC | Event | Result | Owner |
|---|---|---|---|
| TBD | Baseline `/ready` and SLO captured | TBD | TBD |
| TBD | App zone failure simulated | TBD | TBD |
| TBD | SQL failover triggered | TBD | TBD |
| TBD | Storage evidence restore validated | TBD | TBD |
| TBD | PayOS webhook test validated | TBD | TBD |
| TBD | Failed zone re-enabled | TBD | TBD |

## Validation Checklist

| Check | Result | Evidence |
|---|---|---|
| `/live` and `/ready` recover | TBD | TBD |
| Customer login works | TBD | TBD |
| Booking hold works | TBD | TBD |
| Admin login works | TBD | TBD |
| Hangfire/job freshness remains within SLO | TBD | TBD |
| Secret manager reachable | TBD | TBD |
| PayOS webhook processing is idempotent | TBD | TBD |
| Secure evidence file restored | TBD | TBD |

## Follow-Up Issues

| Issue | Severity | Owner | Due date | Status |
|---|---|---|---|---|
| TBD | TBD | TBD | TBD | TBD |
