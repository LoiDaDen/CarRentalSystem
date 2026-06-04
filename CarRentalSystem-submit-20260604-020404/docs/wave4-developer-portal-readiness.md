# Wave 4 Developer Portal Readiness

Updated: 2026-05-17

Public API remains disabled by default. It can only be enabled after these controls pass:

- API key lifecycle exists and stores only hashed keys.
- API docs in production require Admin/Manager authentication.
- Public API requires API key verification.
- Public API routes use rate limiting.
- API calls and key lifecycle changes are audit logged.
- Outbound webhooks are HMAC signed with timestamp replay protection.
- Sandbox environment and OpenAPI examples are available.

Runtime surfaces added in Wave 4:

- `IApiKeyService` for create/verify/revoke.
- `IWebhookSigningService` for HMAC SHA-256 signing and verification.
- Admin API key lifecycle controller.

Evidence: fill `docs/wave4-evidence/api-readiness.md`.
