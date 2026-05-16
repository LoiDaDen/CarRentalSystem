# ADR-003 SSO For Privileged Users

Status: Accepted

Phase 2 SSO is limited to existing verified Admin, Manager, and Staff users. Google and Microsoft providers are configured through secret manager/environment settings. SSO does not auto-create privileged users and still flows through privileged MFA before issuing the application cookie.
