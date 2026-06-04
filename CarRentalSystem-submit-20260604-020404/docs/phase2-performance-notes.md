# Phase 2 Performance Notes

Updated: 2026-05-16

## Targets

- Critical customer/admin flows: P95 < 500ms at agreed staging target load.
- Catalog/reference cache hit ratio: > 80% after warm-up.
- Slow query threshold: > 1s must be triaged with owner and query plan.
- Admin reports must avoid unbounded in-memory filtering on business-critical pages.

## Cached Paths

- `catalog:v1:car-models:{brandId}` for model lookup API.
- Reference data cache TTL default: `Cache:CatalogTtlMinutes=15`.
- Search/catalog cache TTL default: `Cache:SearchTtlMinutes=5`.

## Indexes Added

- Booking overlap and admin lookup indexes.
- Payment/ledger/webhook lookup indexes.
- Audit timestamp/entity/action and user/timestamp indexes.
- Settlement review booking/status/created index.

## Load Test Scope

Baseline scenarios:

- Public car search/filter.
- Booking hold and PayOS checkout callback/webhook.
- Cart multi-car checkout.
- Admin booking list and reports.
- Dispute export for one booking.

Record for every run:

- App version, database size, concurrent users, request rate, P50/P95/P99 latency, error rate, SQL CPU, app CPU/memory, cache hit/miss.
