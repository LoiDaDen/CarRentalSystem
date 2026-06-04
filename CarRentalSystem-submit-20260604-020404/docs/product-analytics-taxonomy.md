# Product Analytics Taxonomy

Updated: 2026-05-16

Analytics is disabled by default and uses a no-op provider until a real vendor is approved.

Config:

- `Analytics:Enabled=false`
- `Analytics:Provider=Noop`
- `Analytics:WriteKey` from secret manager only when a provider is approved.

## Allowed Server Events

| Event | Purpose | Safe properties |
|---|---|---|
| `booking_hold_created` | measure hold creation | booking id, car id, user id, role, status |
| `checkout_started` | funnel start | cart size, amount bucket, role |
| `payment_webhook_processed` | payment reliability | provider, status, amount bucket, latency |
| `ekyc_submitted` | eligibility funnel | user id, status, provider |
| `settlement_review_created` | operations workload | booking id, type, amount bucket |
| `dispute_export_created` | legal operations | booking id, include sensitive flag, archive hash |

## Prohibited Properties

Do not send raw email, phone, citizen id, license number, password, token, provider payload, address, file path, or document image metadata.
