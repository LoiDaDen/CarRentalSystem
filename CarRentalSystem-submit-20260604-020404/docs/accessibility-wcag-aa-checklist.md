# WCAG 2.1 AA Checklist

Updated: 2026-05-16

Critical flows:

- Checkout and payment.
- eKYC upload.
- Admin booking management.
- Contract signing.
- Dispute export and settlement review.

## Required Checks

| Area | Requirement |
|---|---|
| HTML language | `<html lang>` is present and matches current culture |
| Keyboard | all controls are reachable and operable without mouse |
| Labels | inputs have visible label or `aria-label` |
| Images | informative images have alt text; decorative images use empty alt |
| Contrast | text and controls meet AA contrast |
| Focus | focus state is visible and not clipped |
| Errors | validation errors are programmatically associated with fields |
| Screen reader | checkout/eKYC/payment flow can be completed with screen reader |

## Release Rule

Production launch is blocked for any serious/critical issue in checkout, payment, eKYC, or contract signing.
