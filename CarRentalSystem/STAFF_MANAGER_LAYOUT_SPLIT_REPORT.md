# Staff/Manager Layout Split Report

**Date:** 2026-06-11  
**Status:** Implemented and verified

## Summary

- Staff and Manager now use independent MVC Areas, layouts, navigation, assets, controllers, and authorization policies.
- Every concrete controller in `Areas/Admin/Controllers` requires `RequireAdmin`.
- Staff/Manager source contains no reference to `_AdminLayout`, `_AreaManagerLayout`, `/Admin/`, or Admin-only menu labels.
- Admin legacy shared layout remains available for non-area compatibility; Admin Area now selects its own absolute layout.

## Main Files Added Or Updated

### Authorization And Routing

- `Authorization/PortalPolicyNames.cs`
- `Program.cs`
- `Controllers/AuthController.cs`
- `Controllers/InternalPortalControllerBase.cs`
- All concrete `Areas/Admin/Controllers/*.cs`
- `Areas/Admin/Controllers/CarController.cs` (`/Admin/Car` compatibility index)

Policies:

- `RequireAdmin` -> `Admin`
- `RequireManager` -> `Manager`
- `RequireStaff` -> `Staff`

The existing area route remains:

```text
{area:exists}/{controller=Dashboard}/{action=Index}/{id?}
```

Login redirects are explicit and role-safe:

- Admin -> `/Admin/Dashboard`
- Manager -> `/Manager/Dashboard`
- Staff -> `/Staff/Dashboard`
- Customer -> `/`
- Approved partner/customer owner -> `/OwnerCars`

### Independent Frontends

- `Areas/Admin/Views/_ViewStart.cshtml`
- `Areas/Admin/Views/Shared/_AdminLayout.cshtml`
- `Areas/Admin/Views/Shared/_AdminSidebar.cshtml`
- `Areas/Admin/Views/Shared/_AdminTopbar.cshtml`
- `Areas/Admin/Views/Shared/_AdminBreadcrumb.cshtml`
- `Areas/Staff/Controllers/*`, `Areas/Staff/Views/*`
- `Areas/Manager/Controllers/*`, `Areas/Manager/Views/*`
- `Views/Shared/_PortalBookingTable.cshtml`
- `Views/Shared/_PortalFlash.cshtml`
- `wwwroot/css/enterprise-common.css`
- `wwwroot/css/staff.css`
- `wwwroot/css/manager.css`
- `wwwroot/js/enterprise-common.js`
- `wwwroot/js/staff.js`
- `wwwroot/js/manager.js`

Staff Portal provides dashboard tasks, scoped booking operations, handover, return, support, profile, and its own validated area selector.

Manager Portal provides area-scoped KPI, booking assignment/controlled transitions, fleet status, staff assignment, operations, reports, revenue, profile, and its own validated area selector.

### Data And Services

- `Models/Booking.cs`
- `Data/ApplicationDbContext.cs`
- `Services/InternalPortalServices.cs`
- `ViewModels/InternalPortalViewModels.cs`
- `Data/InternalPortalDemoSeeder.cs`
- `Migrations/20260611133211_AddInternalPortalBookingAssignment.cs`

Booking assignment fields:

- `AssignedStaffUserID`
- `AssignedByUserID`
- `AssignedAtUtc`

The migration adds nullable columns, indexes, and `NoAction` user foreign keys. Assignment verifies that the acting Manager manages the booking area and the selected user has the Staff system role in the same area. Assignment changes are audited.

Development-only demo seeding is idempotent, uses `IPasswordHashService`, and does not overwrite existing users.

## Security Verification

- Admin controllers checked: `39/39` require `RequireAdmin`.
- Staff controllers checked: `7/7` require `RequireStaff`.
- Manager controllers checked: `9/9` require `RequireManager`.
- Staff -> Admin/Manager: blocked.
- Manager -> Admin: blocked.
- Customer -> Staff/Manager: blocked.
- Anonymous -> Staff/Manager: challenged.
- Staff/Manager views use masked portal projections and do not expose full User entities.
- Logout remains POST with anti-forgery protection.

## Route And UI Smoke Results

Verified through HTTP integration tests and Playwright:

- Admin: `/Admin/Dashboard`, `/Admin/User`, `/Admin/Car`, `/Admin/Booking`, `/Admin/Payment`, `/Admin/Enterprise`
- Manager: `/Manager/Dashboard`, `/Manager/Booking`, `/Manager/Car`, `/Manager/Staff`, `/Manager/Operation`, `/Manager/Report`, `/Manager/Revenue`, `/Manager/Profile`
- Staff: `/Staff/Dashboard`, `/Staff/Booking`, `/Staff/Handover`, `/Staff/Return`, `/Staff/Support`, `/Staff/Profile`

Staff/Manager dashboard HTML does not contain `Admin Portal`, `User Management`, `Enterprise`, `System Settings`, or `SRE`.

## Tests Added Or Updated

- `CarRentalSystem.Tests/IntegrationTests/InternalPortalAuthorizationIntegrationTests.cs`
- `CarRentalSystem.Tests/UnitTests/Services/InternalPortalServicesTests.cs`
- `CarRentalSystem.Tests/UnitTests/SecurityAttributeTests.cs`
- `CarRentalSystem.Tests/UnitTests/Controllers/AuthControllerTests.cs`
- `playwright/e2e/staff-layout.spec.js`
- `playwright/e2e/manager-layout.spec.js`
- `playwright/e2e/role-redirect.spec.js`
- Updated `playwright/e2e/02-rbac/rbac.spec.js`
- Updated `playwright/e2e/07-area-ops/area-ops.spec.js`

## Verification Results

- `dotnet build CarRentalSystem.csproj --no-restore`: **PASS**, 0 warnings, 0 errors.
- Targeted portal/auth/security .NET tests: **PASS**, 58/58.
- Full `dotnet test`: **PASS**, 336/336.
- Playwright Staff/Manager layout, role redirect, RBAC, area operations: **PASS**, 15/15.
- Final Playwright Admin/Manager/Staff route smoke rerun: **PASS**, 8/8.
- Playwright database reset applied the new migration successfully.
- Development startup smoke: **PASS**, `/live` returned 200.
- Development database migration: **PASS**, `AddInternalPortalBookingAssignment` applied successfully.
- Development demo seed verification: **PASS**, Admin/Manager/Staff/Customer demo accounts exist with the expected roles.
- Static scan: no Staff/Manager Admin-layout, legacy-area-layout, Admin-route, or Admin-only-menu references.

## Remaining Notes

- The full repository-wide Playwright A-Z suite was not run; the portal layout, redirect, RBAC, and area-operations suites requested for this split all passed.
- `Views/Shared/_AreaManagerLayout.cshtml` is retained for compatibility but is not referenced by Staff/Manager.
- Existing unrelated worktree changes were preserved.
