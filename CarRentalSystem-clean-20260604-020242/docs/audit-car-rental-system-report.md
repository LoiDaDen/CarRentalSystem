# CarRentalSystem Audit Report - Bug, Business Logic, Global Benchmark

Audit date: 2026-05-12  
Scope: main ASP.NET Core app, Controllers/Areas, Services, Models, Data, Middleware, BackgroundJobs, Views, custom `wwwroot` JS/CSS, config, and business docs. Excluded generated `bin/obj`, vendor `wwwroot/lib`, and generated migrations except where schema/index behavior matters.

## Executive Summary

This project already has many solid building blocks for a hybrid car rental platform: customer booking, partner-owned cars, drivers, long-term rental, PayOS deposit payment, digital contracts, handover records, tracking, alerts, membership, promotions, and admin areas.

The main risk is not syntax or simple build quality. The main risk is operational correctness: several booking/payment paths use different rules, there is no durable reservation hold before payment, payment webhooks are not idempotent enough, deposit/refund status is often only an internal flag, and eKYC/contract flows can be bypassed or auto-signed too easily. Compared with global rental and car-sharing platforms, these gaps are serious because double booking, off-platform/fraud payment, unverified renters, unclear liability, and weak audit trails are exactly the failure modes those systems design around.

Top priority:

- Rotate/revoke exposed secrets and move them out of tracked config.
- Unify booking availability into one transactional reservation/hold service.
- Harden PayOS order code, webhook idempotency, amount verification, and payment status transitions.
- Enforce eKYC/license eligibility before rental and keep PII hashes in sync after OCR.
- Replace string-based state jumps with explicit booking/deposit/contract state machines.
- Fix CSRF/rate-limiting gaps on authenticated JSON mutations.
- Repair test project compile errors and add concurrency/payment/eKYC tests.

No runtime code was changed by this audit. This report is the only deliverable.

## Verification Snapshot

Commands run from app root:

- `dotnet build --no-restore`: passed, 0 warnings, 0 errors.
- `dotnet test ..\CarRentalSystem.Tests\CarRentalSystem.Tests.csproj --no-restore`: failed at compile time.

Test compile blockers:

- `AuthService` tests still call an old constructor and miss `IPiiEncryptionService`.
- `AuthController` tests still call an old constructor and miss `ApplicationDbContext` / `IRecaptchaService`.
- `Login` and `Register` tests miss the new `recaptchaToken` argument.

Main code volume reviewed: about 56k lines excluding `bin`, `obj`, vendor libraries, and generated migrations.

## Benchmark Baseline

The comparison baseline is a hybrid model because this project combines fleet rental, P2P partner cars, drivers, long-term rental, and tracking.

- Turo: marketplace terms, renter/host eligibility, payment through platform, cancellation/refund policy, and trip responsibilities. Sources: [Turo Terms](https://turo.com/us/en/policies/terms), [Turo Cancellation](https://turo.com/us/en/policies/cancellation).
- Getaround: payment/fraud guidance and insurance/liability framing for car sharing. Sources: [Getaround payment guidance](https://no.getaround.com/help/articles/ef83ddd42ebc/intl), [Getaround insurance](https://getaround.com/insurance).
- Zipcar: membership eligibility, reservation model, late return, usage rules, and connected car/member contract. Sources: [Zipcar How It Works](https://www.zipcar.com/how-it-works), [Zipcar Membership Contract](https://www.zipcar.com/membership-contract).
- Hertz: fleet rental terms for payment, deposit/authorization, damage, fuel, late return, and liability. Source: [Hertz Terms PDF](https://images.hertz.com/pdfs/Hertz_Terms_Conditions_V8_Nov_23.pdf).

The shared global pattern is: verify eligibility before trip, keep all payments on-platform, hold or reserve inventory atomically, make cancellation/refund rules explicit by state and timing, capture pickup/return evidence, define insurance/damage/late/fuel fees, and keep an audit trail for every sensitive transition.

## Critical Findings

| ID | Area | Evidence | Impact | Benchmark Gap | Recommendation |
| --- | --- | --- | --- | --- | --- |
| C-01 | Secret handling | `appsettings.json`, `appsettings.Development.json`, `appsettings.example.env`; `.gitignore` explicitly unignores config files. Secrets include DB credentials and third-party API keys, masked in this report. | Credential compromise, production data exposure, unauthorized payment/AI/recaptcha API use. | Global platforms isolate production secrets and rotate leaked credentials immediately. | Revoke/rotate all exposed secrets, remove real values from tracked config, use environment variables/User Secrets/Key Vault, keep examples placeholder-only. |
| C-02 | Booking availability and hold | `BookingController.Checkout`, `BookingController.Book`, `CartController.CheckoutAll`, `CarController`, `LongTermRentalService` each implement overlap/status logic differently. Cart checkout deletes cart items and creates only `BookingTransaction`, not bookings/holds, until manual confirm. | Double booking, stale pricing, lost carts, user pays or starts payment after another user takes same car. | Turo/Getaround/Zipcar-style systems must reserve inventory atomically for a payment window. | Create one `ReservationService` with serializable transaction or DB constraint/lock, `HoldExpiresAt`, one overlap rule, and release/expire job. Use it for single, cart, and long-term flows. |
| C-03 | PayOS payment integrity | `PayOSPaymentService.CreateDepositPaymentAsync` uses `OrderCode = BookingID`; webhook maps `OrderCode` to booking ID, does not verify paid amount equals expected deposit, has no event idempotency, can send duplicate emails. | Duplicate or spoofed successful payments, amount mismatch accepted, late failed webhook can alter state, repeated payment link conflict. | Global platforms rely on PSP idempotency, verified amount/currency/order, and append-only payment ledger. | Generate unique order code per transaction, unique index it, verify signature + amount + booking/transaction status, store processed webhook/event id, ignore duplicate terminal events. |
| C-04 | Renter eligibility/eKYC not enforced at booking boundary | Booking and cart checkout do not centrally require verified eKYC/license before reservation. `AuthController` profile upload has file-name/URL mismatch. `EkycController` OCR can update CitizenId/DrivingLicense but not update hashes. | Unverified or invalid-license users can attempt rental; duplicate ID/license checks become stale; uploaded documents may be inaccessible. | Turo/Zipcar/Getaround gate rental eligibility before trip and keep identity/license state consistent. | Add `EligibilityService` required before hold/payment; sync PII hashes after OCR updates; fix secure upload URL naming; add expiry and license class checks. |
| C-05 | State machine is string-based and bypassable | `BookingStatusConstants` lacks several statuses used in code (`Rejected`, `CheckedOut`, `UnderReview`, `PendingRefund`, etc.); admin can call `UpdateStatus` with arbitrary status; services set car/deposit status independently. | Invalid transitions, confirmed bookings without payment, refunded deposit without actual refund, car marked available while a future/active booking exists. | Mature rental systems use explicit state transitions tied to payment, contract, pickup, return, damage review, and refund. | Introduce transition service/table with allowed transitions, actor, preconditions, audit event, and single source of status names. |

## High Findings

| ID | Area | Evidence | Impact | Recommendation |
| --- | --- | --- | --- | --- |
| H-01 | Authenticated CSRF gaps | `CartController` mutating endpoints use `[IgnoreAntiforgeryToken]`; `AuthController.VerifyPiiPin` and `BackfillPiiHashes` also ignore antiforgery. | Cross-site cart manipulation, PII reveal, admin/customer state changes. | Remove ignores from authenticated browser mutations or require antiforgery header for AJAX; keep webhook exemptions only for signed external callbacks. |
| H-02 | Rate limiting bypass/false lockout | `RateLimitingMiddleware` treats only `/home` or `/booking` redirect as login success; PIN failures return HTTP 200 and are reset as success; `X-Forwarded-For` is trusted blindly. | Admin/manager successful logins can count as failures; PII PIN brute force is not effectively limited; IP spoofing. | Use auth result, endpoint metadata, or explicit response marker; rate limit by user + IP; trust forwarded headers only from configured proxies. |
| H-03 | Deposit/refund is mostly an internal flag | `DepositRefundJob` changes `DepositStatus` to `Refunded` after 15 days but does not create PSP refund, bank transfer, or ledger record. Cancellation paths set `Refunded` immediately in some states. | Customers see refund state without actual money movement; finance reconciliation impossible. | Add refund transaction ledger, payout/refund integration, reconciliation status, failure/retry handling, and admin approval workflow for damage deductions. |
| H-04 | Contract signing/legal evidence weak | Booking contract can auto-sign owner/system and auto-sign owner when customer signs; partner registration creates both sides as signed. User-submitted identity fields can be persisted into contract. | Weak legal enforceability and non-repudiation; contract identity may not match verified documents. | Use verified identity snapshots, explicit party signatures, IP/user-agent/timestamp, immutable PDF/hash, and no automatic counterparty signature unless policy explicitly allows it. |
| H-05 | Promotions race condition | `BookingController.Book` applies discount, then `ExecuteUpdate` increments `UsedCount`; if affected rows is 0 the booking still keeps discount. | Promo oversell and incorrect revenue. | Check affected rows before applying discount or reserve promo usage transactionally with unique per-user usage constraints. |
| H-06 | Driver availability not reserved | `BookingController` and cart transaction create `DriverBooking` without checking driver overlap/availability for the requested interval. | Same driver can be assigned to overlapping trips. | Add driver scheduling table/hold with overlap checks and service-area/license constraints. |
| H-07 | Admin area status and area scope | Admin booking index loads all bookings then filters in memory; `UpdateStatus` accepts arbitrary status; `Confirm` can confirm without verifying deposit/payment/contract. | Cross-area exposure/performance risk and invalid operational states. | Move area filters into database queries, enforce resource policy centrally, and route all transitions through transition service. |
| H-08 | Audit middleware performance and coverage | `AuditLogMiddleware` writes `audit_debug.log` per request and runs `SELECT COUNT(*)` after inserts; failed/forbidden requests are not audited. | Disk bloat, database overhead, missing security trail for denied access. | Remove debug file logging, no count query in hot path, audit sensitive denied/failed actions, and add retention/partition strategy. |
| H-09 | Startup mutates production | `Program.cs` runs `Database.Migrate()` and seeds contract/add-on data in every non-Testing environment. | Unexpected production schema/data changes during deployment. | Move migrations/seeding to explicit deployment step, or guard by environment/feature flag with operator approval. |
| H-10 | Car/partner edit overposting | Partner `AddCar`/`EditCar` bind `Car` directly from request, then `_db.Cars.Update(car)`. | User can overpost fields such as status, pricing, location/status IDs, metadata not intended for partner edits. | Use dedicated view models and whitelist editable fields; force approval status after material changes. |

## Medium Findings

- `BookingService.GetUserBookingsAsync` and `GetAllBookingsAsync` auto-cancel expired bookings as a side effect of reads. Move to background job/explicit command with audit.
- `BookingService.CancelBookingAsync` sets deposit `Refunded` and car `Available` unconditionally. It should depend on payment state, refund ledger, and other active/future bookings.
- Cart pricing can double-count add-ons: `EstimatedPrice` is updated with add-ons, then checkout sums `EstimatedPrice + addonTotal`.
- Deposit policy is inconsistent: booking creation uses `DefaultDeposit`; PayOS overwrites to 20% of total; cart transaction sums `DefaultDeposit`.
- Single booking, cart, public search, admin map, and long-term rental exclude different booking statuses and use different time buffers.
- `ApplicationDbContext.SaveChangesAsync` implements soft-delete behavior, but sync `SaveChanges()` is not overridden. Seeders/controllers using sync saves can bypass soft-delete rules.
- PayOS transaction index on `OrderCode` is non-unique. There are no rowversion/concurrency tokens on booking/payment/cart transaction entities.
- Secure file serving accepts a user-supplied `folder` parameter and should whitelist folders; private document responses should use `Cache-Control: no-store`.
- File upload checks are mostly extension-based, without MIME/magic-number validation or malware scanning.
- eKYC OCR errors and external provider errors can be surfaced to users too directly.
- `AreaAuthorizationHandler` role matching and admin inclusion are inconsistent; `Manager` may be treated as globally powerful depending on path.
- Partner approval sets `IsVerified = true`, mixing marketplace partner approval with identity verification.
- Driver dashboard can mark booking/car active/completed outside a shared transaction and without full handover/payment/contract preconditions.
- Handover records capture photos/signature, which is good, but damage/fuel/late fee settlement is not linked to a financial ledger.
- Maintenance/overdue job creates alerts for overdue active bookings but does not drive recovery workflow, fee accrual, or escalation.
- Long-term rental availability ignores some pending/draft payment states and mixes UTC date with Vietnam business-day expectations.
- `Program.cs` serves `../docs` under `/docs`; avoid deploying internal audit/backlog docs publicly.

## Low Findings

- Several service interfaces declare non-null returns while implementations can return `null`, making nullability contracts misleading.
- Security headers are partial: add CSP and Permissions-Policy; remove obsolete `X-XSS-Protection`.
- `SeedController` and demo seeders should be production-disabled or admin-only with explicit environment guard.
- Many business statuses are string literals in controllers, views, services, and jobs. Even before a full state machine, centralize them.
- Time handling mixes `DateTime.UtcNow`, `DateTime.Now`, and local `.Date`; standardize storage in UTC and convert at UI/business boundaries for `Asia/Saigon`.
- Partner revenue calculation uses the first car's commission rate for all completed bookings, which is wrong if cars have different commission rates.

## Business Flow Assessment

### Search and Availability

Current behavior: public search and checkout paths do overlap checks independently, mostly by querying existing bookings and excluding some statuses.

Risk: availability shown to customer is not the same rule used later for payment/cart/manual transaction. There is no short-lived hold while the customer pays. Pending, draft, rejected, long-term, and cart states are treated differently depending on path.

Target behavior: one availability service should return quote + availability + hold token. It should lock the car/time window until payment timeout or release, then create booking from the hold.

### Single Booking

Current behavior: `BookingController.Book` uses a serializable transaction and overlap recheck, which is a good start. It still handles promo, driver assignment, booking creation, deposit status, and car logic locally.

Risk: booking can be confirmed later without verified payment/contract; driver overlap is not checked; status names are not centrally enforced.

Target behavior: use one command such as `CreateReservationAsync`, then `ConfirmPaymentAsync`, then `ConfirmBookingAsync` with explicit preconditions.

### Cart / Multi-Car Checkout

Current behavior: cart checkout validates each cart item, creates `BookingTransaction`, serializes a snapshot, and deletes cart items before actual booking creation/payment confirmation.

Risk: cart data can be lost if payment is abandoned; cars remain free for others; add-ons/deposits can be calculated differently from single checkout.

Target behavior: cart checkout should create multiple holds atomically, keep cart until terminal payment state, and create bookings only from validated holds.

### Payment, Deposit, Refund

Current behavior: PayOS deposit payment exists, webhook signature is checked, and admin/manual flows exist. Deposit/refund states are often updated directly on booking.

Risk: no verified amount matching, no robust idempotency, no actual refund ledger, and manual transaction status can be set to confirmed before real payment settlement.

Target behavior: maintain append-only payment/refund ledger with provider transaction IDs, amount/currency checks, unique idempotency keys, reconciliation status, and explicit refund/deduction workflow.

### eKYC and Eligibility

Current behavior: there is OCR, face verification, secure upload, profile PII encryption, and admin review. However, booking does not consistently require eligibility.

Risk: user can enter booking/payment flows before identity/license eligibility is valid. OCR updates can desync hashes used for duplicate checks.

Target behavior: eligibility must be a hard precondition for creating a hold or starting pickup, with reasons such as `MissingCitizenId`, `LicenseExpired`, `FaceMismatch`, `NotApproved`.

### Pickup, Return, Damage

Current behavior: handover records capture images, odometer, fuel, and signature. Customer confirm pickup/return checks handover records.

Risk: damage/fuel/late fees are not connected to an immutable settlement ledger and refund/deduction decision; car status can be changed outside the handover workflow.

Target behavior: pickup and return should create immutable evidence packages, update booking status through transition service, calculate late/fuel/cleaning/damage fees, then release or deduct deposit via finance workflow.

### Partner, Driver, Admin

Current behavior: partner and driver areas have basic ownership checks; admin areas use role authorization and some area filtering.

Risk: partner direct model binding overposts car fields; partner approval equals identity verification; driver can mutate booking trip states independently; admin status transitions bypass preconditions.

Target behavior: separate identity verification from role approval, use view models, central resource policies, and transition commands with audit for partner/driver/admin actions.

## Prioritized Backlog

### P0 - Must Fix Before Production

1. Rotate all exposed secrets and replace tracked real config with placeholders.
2. Fix test project compile errors so CI can run.
3. Add unified reservation/hold service for single, cart, long-term, and admin booking creation.
4. Harden PayOS: unique order code, unique DB constraint, amount/currency verification, webhook idempotency, terminal-state guards.
5. Remove CSRF exemptions from authenticated JSON mutations or require antiforgery tokens for AJAX.
6. Enforce eKYC/license eligibility before hold/payment/pickup.
7. Replace arbitrary admin booking status update with a transition service.

### P1 - High Operational Risk

1. Implement payment/refund ledger and connect deposit refund job to real refund/reconciliation states.
2. Normalize booking/deposit/car/driver status constants and remove untracked status strings.
3. Add driver scheduling overlap checks.
4. Fix cart checkout data loss and add-on/deposit pricing inconsistencies.
5. Fix profile/eKYC upload URL naming and PII hash sync after OCR updates.
6. Add secure file folder whitelist and no-store cache headers.
7. Remove audit hot-path debug/count queries and add denied-action auditing.
8. Move production migrations/seeding out of app startup.

### P2 - Business Completeness

1. Define cancellation/no-show/refund policy by time window and state.
2. Add damage/fuel/cleaning/late fee settlement workflow tied to deposit.
3. Add immutable contract snapshots with verified identity data and explicit signatures.
4. Add partner payout/revenue ledger instead of dashboard-only revenue sums.
5. Add overdue recovery workflow from tracking/alerts, not alerts only.
6. Add area-scoped policies for admin/manager/staff and central ownership checks.
7. Standardize UTC storage with Vietnam-local business date conversion.

### P3 - Hardening and Maintainability

1. Add CSP and Permissions-Policy headers.
2. Disable or guard seed/demo endpoints in production.
3. Replace direct entity binding in forms with view models.
4. Add rowversion/concurrency tokens to high-contention entities.
5. Add query indexes for booking availability: at minimum `(CarID, StartDate, EndDate, BookingStatusID)`.
6. Add retention and archival policy for audit logs, tracking, handover files, and eKYC files.

## Test Scenarios To Add

- Two users book the same car/time through single checkout at the same time.
- One user books through cart while another books through single checkout for overlapping time.
- Cart payment is abandoned after cart items are removed; hold expiry should restore clean state.
- PayOS webhook repeated with the same event/order.
- PayOS webhook with valid signature but wrong amount/order/booking state.
- PayOS cancel/success callback arrives before webhook and after webhook.
- Cancel booking in `Draft`, `Pending`, `Confirmed`, `Active`, and `Completed`; verify deposit/refund state.
- Confirm admin booking without paid deposit should fail.
- Pickup without signed contract or handover record should fail.
- Return late by more than grace period should produce fee settlement.
- Deposit refund after 15 days should create a refund ledger entry, not just change status.
- User with missing eKYC, rejected eKYC, expired license, or hash conflict tries to rent.
- OCR changes CitizenId/DrivingLicense; hash uniqueness must update.
- Partner edits car and attempts to overpost status/owner/approval fields.
- Driver with overlapping assignment cannot be selected.
- Driver/admin/partner attempts to access resources outside ownership/area.
- Production startup with missing secrets must fail closed, not bypass payment/recaptcha security.

## Notes On Active File

`Controllers/AlertController.cs` itself is relatively small and not the source of the largest risk. Its admin-only alert list/resolve flow is reasonable. The bigger tracking/alert issue is downstream: overdue active bookings currently create alerts but are not tied to a recovery/fee/escalation workflow.

## Assumptions

- The intended product is a hybrid marketplace/fleet platform, not only a simple classroom CRUD rental app.
- Secret values were intentionally not reproduced in this report.
- Audit focused on correctness, security, and rental operations; visual/UI polish was reviewed only where it affects booking/payment correctness.
