# ADR-001 Reservation And Payment Ledger

Status: Accepted

Booking holds, payment webhook handling, refund requests, settlement deductions, and payout reconciliation must be represented by append-only ledger entries with idempotency keys. Controllers must not directly create money movement without a service-level transition and audit reason.
