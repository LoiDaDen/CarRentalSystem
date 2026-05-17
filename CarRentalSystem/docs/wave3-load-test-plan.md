# Wave 3 Load Test Plan

Updated: 2026-05-16

Goal: prove critical flows meet P95 < 500ms at agreed staging target load before public production traffic.

## Scenarios

| Scenario | Target metric | Notes |
|---|---|---|
| Checkout single car | P95 < 500ms | Search, quote, hold creation. |
| Cart multi-car checkout | P95 < 500ms | Multi-hold and PayOS checkout creation. |
| PayOS webhook | P95 < 500ms | Valid, duplicate, failed and amount mismatch events. |
| Admin reports | P95 < 500ms | Dashboard and reports after DB-side aggregate refactor. |
| eKYC upload | P95 < 500ms excluding provider latency | Validate upload and enqueue/provider call boundary. |
| SLO dashboard | P95 < 500ms | Admin SRE dashboard JSON/view. |

## Evidence Required

Fill `docs/wave3-evidence/load-test-report.md` after running on cloud staging with production-class database/storage/observability.

Wave 3 is not complete if load-test evidence remains a template.
