# Phase 3 Observability

Updated: 2026-05-16

## OpenTelemetry

Default exporter: OTLP.

Production config:

- `Observability:OpenTelemetry:Enabled=true`
- `Observability:OpenTelemetry:ServiceName=CarRentalSystem`
- `Observability:OpenTelemetry:OtlpEndpoint=https://otel.example.com`
- `Observability:OpenTelemetry:Headers` from secret manager when needed.

If OpenTelemetry is enabled in production without an OTLP endpoint, startup is blocked.

## Trace Names

| Flow | Activity name |
|---|---|
| Booking hold | `booking_hold_created` |
| Cart checkout | `checkout_started` |
| PayOS webhook | `payment_webhook_processed` |
| eKYC upload | `ekyc_submitted` |
| Settlement approval | `settlement_approval` |
| Dispute export | `dispute_export` |

## PII-Safe Rules

Never tag raw email, phone, citizen ID, license number, token, secret, provider payload, file path, or address.

Allowed tags:

- IDs: booking id, car id, user id, review id.
- Status/category: booking status, payment status, event type.
- Durations/counts/amount buckets.
- Role names.

## Dashboard Panels

- Availability and error budget remaining.
- Checkout success rate.
- Payment webhook success and P95 latency.
- Booking conflict rate.
- Hangfire/job freshness.
- Top exceptions by route and version.

## Alert Severity

| Severity | Examples | Response |
|---|---|---|
| Sev1 | app down, DB unavailable, payment webhook outage | page on-call immediately |
| Sev2 | checkout success drop, job freshness stale, high conflict spike | page business-hours owner or on-call |
| Sev3 | warning-level latency or isolated provider failure | ticket within 1 business day |
