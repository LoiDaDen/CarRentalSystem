# Wave 4 Accessibility Audit Plan

Updated: 2026-05-17

Goal: critical customer/admin flows meet WCAG 2.1 AA before enterprise production.

Critical flows:

- Checkout and cart checkout.
- eKYC upload.
- Payment and receipt/self-service.
- Contract, handover and dispute/claim evidence.
- Admin booking, support, fleet ops and API key management.

Minimum checks:

- Page language is set with `<html lang="vi">` or equivalent.
- Inputs have labels or accessible names.
- Icon-only/action buttons have text or `aria-label`.
- Keyboard navigation reaches primary actions.
- Error messages are visible and associated with relevant fields where practical.
- No serious/critical axe/WAVE findings remain.

Evidence: fill `docs/wave4-evidence/accessibility-audit.md` after manual and automated checks.
