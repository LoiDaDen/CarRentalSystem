# SOC2-Ready Control Matrix

Updated: 2026-05-17

Purpose: organize CarRentalSystem evidence for a SOC2-style readiness review. This is not a certification report; it is the operating evidence index used before an external review.

| Control ID | Area | Control objective | Owner | Evidence path | Cadence | Status | Gap | Remediation due |
|---|---|---|---|---|---|---|---|---|
| SOC2-AC-01 | Access control | Privileged access requires RBAC, MFA/SSO gate and periodic review. | TBD | docs/wave5-evidence/soc2-evidence-index.md | Quarterly | TEMPLATE | TBD | TBD |
| SOC2-AC-02 | Access control | Production secrets are stored outside tracked files and rotated. | TBD | docs/wave2-evidence/secret-rotation.md | Quarterly | TEMPLATE | TBD | TBD |
| SOC2-CM-01 | Change management | Release build, tests, security gates and rollback evidence are required before production. | TBD | docs/enterprise-production-readiness-gates.md | Every release | TEMPLATE | TBD | TBD |
| SOC2-CM-02 | Change management | Database migrations are reviewed and dry-run before deploy. | TBD | scripts/Test-Phase1ProductionSafety.ps1 | Every release | TEMPLATE | TBD | TBD |
| SOC2-IR-01 | Incident response | Severity matrix, escalation path and incident runbooks exist. | TBD | docs/incident-runbooks.md | Quarterly | TEMPLATE | TBD | TBD |
| SOC2-DR-01 | Backup/DR | SQL PITR and secure-file restore drills prove RTO <= 4h and RPO <= 1h. | TBD | docs/wave2-evidence/restore-drill.md | Quarterly | TEMPLATE | TBD | TBD |
| SOC2-DR-02 | Availability | Multi-AZ failover drill proves zone failure survival. | TBD | docs/wave5-evidence/multi-az-failover-report.md | Semiannual | TEMPLATE | TBD | TBD |
| SOC2-VM-01 | Vulnerability management | Critical/high dependency/container/security findings block release. | TBD | docs/wave5-evidence/pentest-remediation-tracker.md | Every release | TEMPLATE | TBD | TBD |
| SOC2-AU-01 | Audit logging | Sensitive actions use hash-chain audit and can be verified. | TBD | docs/enterprise-production-readiness-gates.md | Quarterly | TEMPLATE | TBD | TBD |
| SOC2-PI-01 | Privacy/retention | Export/delete/retention review workflow exists and avoids unsafe hard delete. | TBD | docs/wave3-evidence/privacy-controls.md | Quarterly | TEMPLATE | TBD | TBD |
| SOC2-VD-01 | Vendor/security | Provider risks, DPAs and security evidence are tracked for PayOS, FPT, email, hosting and monitoring. | TBD | docs/wave5-evidence/soc2-evidence-index.md | Quarterly | TEMPLATE | TBD | TBD |

Release rule: any control marked `Failed` or any open Critical/High security finding blocks enterprise go-live unless formally risk-accepted by the business owner and technical owner with expiry.
