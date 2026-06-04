# ADR-002 Audit Hash Chain

Status: Accepted

Privileged, payment, eKYC, contract, dispute export, and settlement actions must be written through `IAuditTrailService`. Each audit record stores a SHA-256 hash over canonical fields and the previous record hash so tampering can be detected by chain verification.
