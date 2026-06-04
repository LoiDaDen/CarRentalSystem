# Developer Portal Readiness

Updated: 2026-05-16

Public API is not launched in Phase 3. This document lists the gates required before exposing external developer access.

## Required Capabilities

- API key lifecycle: create, rotate, revoke, expire.
- Per-key RBAC and tenant/partner scope.
- Rate limit and abuse detection.
- Webhook signing with timestamp and replay protection.
- Audit log for every API key and webhook admin action.
- Sandbox environment with test PayOS/eKYC providers.
- OpenAPI docs protected in production unless explicitly enabled and authenticated.

## Current Repo Gate

- `ApiDocs:Enabled=false` by default.
- Production startup rejects `ApiDocs:Enabled=true` when `ApiDocs:RequireAuth=false`.
- Swagger UI is only automatic in Development.

## Go-Live Decision

Do not publish the API until key management, rate limits, webhook signing, docs auth, sandbox data, and partner support process are implemented and tested.
