# Data Inventory And Classification

Updated: 2026-05-16

| Data domain | Examples | Classification | Retention owner | Default retention |
|---|---|---|---|---|
| PII | name, email, phone, address, citizen ID hash, driving license hash | Restricted | Security / Product | Account lifetime + legal window |
| eKYC | ID images, face image, OCR result, verification status | Restricted | Security | Legal/regulatory window |
| Payment | PayOS order code, ledger, webhook hash, refund/payout status | Confidential | Finance / Engineering | 7 years or local tax rule |
| Contract | digital contract, physical contract, signer snapshot, hashes | Restricted | Legal / Operations | Contract term + dispute window |
| Handover evidence | photos, odo, fuel, signature, geo/time, evidence hash | Restricted | Operations / Legal | Rental term + dispute window |
| Tracking | car location, device nonce, timestamp, signature metadata | Confidential | Operations | Minimize; keep only operationally required data |
| Audit | actor, action, reason, old/new values, hash chain | Confidential | Security | >= 1 year, longer for privileged/payment actions |
| Support/business analytics | feedback, NPS, funnel metrics | Internal | Product | Business-defined |

## Controls

- Restricted data must not be exported raw unless there is a legal/business reason and an audit entry.
- Dispute package export defaults to redacted output.
- Snapshot JSON is not a production backup mechanism for PII.
- Delete/export requests must be reviewed against payment, contract, and legal retention obligations.
- Vendor DPAs and security evidence must be stored before production vendor use.
