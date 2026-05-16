# ADR-004 Contract Evidence And Dispute Export

Status: Accepted

Contract and handover evidence must store immutable hashes and signer/recorder metadata. Dispute export packages are ZIP files with a manifest, ledger, provider events, evidence, and audit trail. Exports are redacted by default and every export is audit logged.
