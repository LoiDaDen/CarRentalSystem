# External Checklist To Reach 100 Percent Readiness

Date: 2026-06-08

These tasks cannot be completed only by editing the repository. They require server access, providers, legal/ops work, or real paid bookings.

| Task | Owner | Evidence needed |
| --- | --- | --- |
| Staging subdomain with SSL | User/infra | URL, certificate status, DNS record |
| IIS/Plesk site, app pool, physical path | User/infra | Site/app pool names, deployment screenshot/log |
| .NET 10 Hosting Bundle installed | User/infra | Installed version evidence |
| Dedicated staging SQL database | User/infra | DB name, migration log |
| Redis configured for distributed cache/rate limit | User/infra | Redis endpoint configured on server, no secret in repo |
| DataProtection key folder | User/infra | Path and permission evidence |
| PayOS sandbox keys | User | Server env vars and sandbox reconciliation result |
| FPT eKYC sandbox keys | User | Server env vars and OCR/eKYC smoke result |
| SMTP, reCAPTCHA, OAuth | User | Smoke test evidence |
| OTLP/log/uptime backend | User/infra | Dashboard link or screenshot |
| Legal TMĐT, terms, privacy, DPA, contracts | User/legal | Signed or approved documents |
| Insurance and e-invoice provider | User/business | Provider contract/SLA and claim policy |
| Pilot fleet 20-50 cars/owners | User/business | Owner consent, insurance, inspection, car photos |
| Independent pentest | User/security vendor | Report and retest showing no Critical/High open findings |
| Manual accessibility audit | User/QA | WCAG report for key flows |
| Real paid pilot | User/operations | At least 10 paid bookings, PayOS/bank statements, refund/payout evidence |
| Executive sign-off | User/management | Restore, rollback, alert, failover, pentest, go-live sign-off |

Do not send secrets in chat or commit them to the repository. Configure secrets directly on the staging server.
