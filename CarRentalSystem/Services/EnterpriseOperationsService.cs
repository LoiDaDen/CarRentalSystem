using CarRentalSystem.Areas.Admin.Models;
using CarRentalSystem.Data;
using CarRentalSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace CarRentalSystem.Services
{
    public interface IEnterpriseOperationsService
    {
        Task<EnterpriseCommandCenterViewModel> GetCommandCenterAsync(CancellationToken cancellationToken = default);
    }

    public class EnterpriseOperationsService : IEnterpriseOperationsService
    {
        private static readonly string[] TerminalSupportStatuses =
        {
            SupportTicketStatusConstants.Resolved,
            SupportTicketStatusConstants.Closed
        };

        private static readonly string[] RevenueBookingStatuses =
        {
            BookingStatusConstants.Completed,
            BookingStatusConstants.Confirmed,
            BookingStatusConstants.Active
        };

        private static readonly string[] ActiveBookingStatuses =
        {
            BookingStatusConstants.Confirmed,
            BookingStatusConstants.Active
        };

        private static readonly string[] PendingRefundStatuses =
        {
            RefundRequestStatusConstants.PendingReview,
            RefundRequestStatusConstants.Approved,
            RefundRequestStatusConstants.Processing,
            RefundRequestStatusConstants.PendingManual
        };

        private readonly ApplicationDbContext _context;

        public EnterpriseOperationsService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<EnterpriseCommandCenterViewModel> GetCommandCenterAsync(CancellationToken cancellationToken = default)
        {
            var now = DateTime.UtcNow;
            var todayStart = now.Date;
            var tomorrowStart = todayStart.AddDays(1);
            var last30Days = now.AddDays(-30);
            var trackingCutoff = now.AddHours(-24);
            var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var maintenanceWindowEnd = now.AddDays(14);

            var bookingQuery = _context.Bookings
                .AsNoTracking()
                .Where(b => !b.IsDeleted);

            var todayPickups = await bookingQuery.CountAsync(b =>
                b.StartDate >= todayStart &&
                b.StartDate < tomorrowStart &&
                b.BookingStatus != null &&
                ActiveBookingStatuses.Contains(b.BookingStatus.Name),
                cancellationToken);

            var todayReturns = await bookingQuery.CountAsync(b =>
                b.EndDate >= todayStart &&
                b.EndDate < tomorrowStart &&
                b.BookingStatus != null &&
                ActiveBookingStatuses.Contains(b.BookingStatus.Name),
                cancellationToken);

            var activeBookings = await bookingQuery.CountAsync(b =>
                b.BookingStatus != null &&
                b.BookingStatus.Name == BookingStatusConstants.Active,
                cancellationToken);

            var overdueReturns = await bookingQuery.CountAsync(b =>
                b.EndDate < now &&
                b.ActualReturnDate == null &&
                b.BookingStatus != null &&
                b.BookingStatus.Name == BookingStatusConstants.Active,
                cancellationToken);

            var activeHolds = await _context.BookingHolds.AsNoTracking().CountAsync(h =>
                h.Status == BookingHoldStatusConstants.Active &&
                h.ExpiresAt >= now,
                cancellationToken);

            var expiredActiveHolds = await _context.BookingHolds.AsNoTracking().CountAsync(h =>
                h.Status == BookingHoldStatusConstants.Active &&
                h.ExpiresAt < now,
                cancellationToken);

            var unsignedContracts = await _context.DigitalContracts.AsNoTracking().CountAsync(c =>
                !c.IsDeleted &&
                (!c.IsSignedByCustomer || !c.IsSignedByOwner),
                cancellationToken);

            var handoversMissingEvidence = await _context.HandoverRecords.AsNoTracking().CountAsync(h =>
                !h.IsDeleted &&
                h.RecordDate >= last30Days &&
                (h.EvidenceHash == null || h.SignatureUrl == null),
                cancellationToken);

            var pendingSettlements = await _context.SettlementReviews.AsNoTracking().CountAsync(s =>
                s.Status == SettlementReviewStatusConstants.PendingReview,
                cancellationToken);

            var pendingRefunds = await _context.RefundRequests.AsNoTracking().CountAsync(r =>
                PendingRefundStatuses.Contains(r.Status),
                cancellationToken);

            var pendingPrivacy = await _context.PrivacyRequests.AsNoTracking().CountAsync(r =>
                r.Status == PrivacyRequestStatusConstants.PendingReview,
                cancellationToken);

            var openSupport = await _context.SupportTickets.AsNoTracking().CountAsync(t =>
                !TerminalSupportStatuses.Contains(t.Status),
                cancellationToken);

            var breachedSupport = await _context.SupportTickets.AsNoTracking().CountAsync(t =>
                !TerminalSupportStatuses.Contains(t.Status) &&
                t.FirstResponseDueAt != null &&
                t.FirstResponseDueAt < now,
                cancellationToken);

            var pendingEkyc = await _context.Users.AsNoTracking().CountAsync(u =>
                u.Role == "Customer" &&
                (u.VerificationStatus == "Pending" || !u.IsVerified || !u.IsEmailVerified),
                cancellationToken);

            var blockedRiskUsers = await _context.Users.AsNoTracking().CountAsync(u =>
                u.IsBlacklisted || u.IsLocked,
                cancellationToken);

            var finance = await BuildFinanceSnapshotAsync(last30Days, cancellationToken);
            var fleet = await BuildFleetSnapshotAsync(now, trackingCutoff, monthStart, maintenanceWindowEnd, cancellationToken);

            var moneyExceptions = finance.PayOSPaidWithoutLedger + finance.LedgerWithoutPayOS + finance.PendingManualPayouts;
            var readinessPercent = fleet.TotalCars > 0
                ? Math.Round((decimal)fleet.AvailableCars / fleet.TotalCars * 100m, 1)
                : 0m;

            return new EnterpriseCommandCenterViewModel
            {
                GeneratedAtUtc = now,
                Finance = finance,
                Fleet = fleet,
                Kpis = new List<EnterpriseKpiCard>
                {
                    new()
                    {
                        Title = "Pickups today",
                        Value = todayPickups.ToString("N0"),
                        Detail = $"{todayReturns:N0} returns today",
                        Severity = todayPickups + todayReturns > 0 ? EnterpriseSeverity.Warning : EnterpriseSeverity.Success,
                        Icon = "bi-calendar2-check",
                        Url = "/Admin/Booking"
                    },
                    new()
                    {
                        Title = "Active rentals",
                        Value = activeBookings.ToString("N0"),
                        Detail = $"{overdueReturns:N0} overdue returns",
                        Severity = overdueReturns > 0 ? EnterpriseSeverity.Critical : EnterpriseSeverity.Success,
                        Icon = "bi-key",
                        Url = "/Admin/Booking"
                    },
                    new()
                    {
                        Title = "Reservation holds",
                        Value = activeHolds.ToString("N0"),
                        Detail = $"{expiredActiveHolds:N0} expired active holds",
                        Severity = expiredActiveHolds > 0 ? EnterpriseSeverity.Critical : EnterpriseSeverity.Info,
                        Icon = "bi-hourglass-split",
                        Url = "/Admin/Booking"
                    },
                    new()
                    {
                        Title = "Money exceptions",
                        Value = moneyExceptions.ToString("N0"),
                        Detail = $"{pendingRefunds:N0} refunds waiting",
                        Severity = moneyExceptions > 0 ? EnterpriseSeverity.Critical : EnterpriseSeverity.Success,
                        Icon = "bi-cash-coin",
                        Url = "/Admin/Payment"
                    },
                    new()
                    {
                        Title = "Support SLA",
                        Value = breachedSupport.ToString("N0"),
                        Detail = $"{openSupport:N0} open tickets",
                        Severity = breachedSupport > 0 ? EnterpriseSeverity.Critical : EnterpriseSeverity.Success,
                        Icon = "bi-headset",
                        Url = "/Admin/Support"
                    },
                    new()
                    {
                        Title = "Fleet ready",
                        Value = $"{readinessPercent:N1}%",
                        Detail = $"{fleet.AvailableCars:N0}/{fleet.TotalCars:N0} cars available",
                        Severity = readinessPercent < 60 ? EnterpriseSeverity.Warning : EnterpriseSeverity.Success,
                        Icon = "bi-car-front",
                        Url = "/Admin/Car/Manage"
                    },
                    new()
                    {
                        Title = "Risk review",
                        Value = pendingEkyc.ToString("N0"),
                        Detail = $"{blockedRiskUsers:N0} locked/blacklisted users",
                        Severity = pendingEkyc + blockedRiskUsers > 0 ? EnterpriseSeverity.Warning : EnterpriseSeverity.Success,
                        Icon = "bi-shield-check",
                        Url = "/Admin/User"
                    },
                    new()
                    {
                        Title = "Legal evidence",
                        Value = unsignedContracts.ToString("N0"),
                        Detail = $"{handoversMissingEvidence:N0} handovers missing evidence",
                        Severity = unsignedContracts + handoversMissingEvidence > 0 ? EnterpriseSeverity.Warning : EnterpriseSeverity.Success,
                        Icon = "bi-file-earmark-text",
                        Url = "/Admin/Booking"
                    }
                },
                Queues = BuildQueues(
                    overdueReturns,
                    expiredActiveHolds,
                    pendingSettlements,
                    pendingRefunds,
                    pendingPrivacy,
                    breachedSupport,
                    pendingEkyc,
                    fleet,
                    finance,
                    unsignedContracts,
                    handoversMissingEvidence),
                Capabilities = BuildCapabilities(
                    pendingSettlements,
                    pendingRefunds,
                    pendingPrivacy,
                    breachedSupport,
                    pendingEkyc,
                    fleet,
                    finance,
                    unsignedContracts,
                    handoversMissingEvidence),
                WorkflowGroups = BuildWorkflowGroups()
            };
        }

        private async Task<EnterpriseFinanceSnapshot> BuildFinanceSnapshotAsync(DateTime last30Days, CancellationToken cancellationToken)
        {
            var ledgerGroups = await _context.PaymentLedgerEntries.AsNoTracking()
                .Where(l => l.CreatedAt >= last30Days)
                .GroupBy(l => l.EntryType)
                .Select(g => new { EntryType = g.Key, Amount = g.Sum(l => l.Amount) })
                .ToListAsync(cancellationToken);

            decimal LedgerAmount(string entryType) =>
                ledgerGroups.FirstOrDefault(g => g.EntryType == entryType)?.Amount ?? 0m;

            var payOSPaidWithoutLedger = await _context.PayOSTransactions.AsNoTracking()
                .CountAsync(t =>
                    t.Status == PaymentStatusConstants.Paid &&
                    !_context.PaymentLedgerEntries.Any(l =>
                        l.OrderCode == t.OrderCode &&
                        l.Status == PaymentStatusConstants.Paid),
                    cancellationToken);

            var ledgerWithoutPayOS = await _context.PaymentLedgerEntries.AsNoTracking()
                .CountAsync(l =>
                    l.Provider == "PayOS" &&
                    l.Status == PaymentStatusConstants.Paid &&
                    l.OrderCode != null &&
                    !_context.PayOSTransactions.Any(t =>
                        t.OrderCode == l.OrderCode &&
                        t.Status == PaymentStatusConstants.Paid),
                    cancellationToken);

            var pendingRefunds = await _context.RefundRequests
                .IgnoreQueryFilters()
                .AsNoTracking()
                .CountAsync(r => PendingRefundStatuses.Contains(r.Status), cancellationToken);

            var pendingManualPayouts = await _context.RefundPayouts
                .IgnoreQueryFilters()
                .AsNoTracking()
                .CountAsync(p =>
                    p.ApprovalState == PaymentStatusConstants.PendingManual ||
                    p.TransactionState == PaymentStatusConstants.PendingManual ||
                    p.TransactionState == PaymentStatusConstants.Processing ||
                    p.TransactionState == PaymentStatusConstants.Pending,
                    cancellationToken);

            return new EnterpriseFinanceSnapshot
            {
                LedgerCharge30Days = LedgerAmount(PaymentLedgerEntryTypeConstants.DepositCharge),
                LedgerRefund30Days = LedgerAmount(PaymentLedgerEntryTypeConstants.Refund),
                LedgerDeduction30Days = LedgerAmount(PaymentLedgerEntryTypeConstants.Deduction),
                LedgerPayout30Days = LedgerAmount(PaymentLedgerEntryTypeConstants.Payout),
                PendingRefunds = pendingRefunds,
                PendingManualPayouts = pendingManualPayouts,
                PayOSPaidWithoutLedger = payOSPaidWithoutLedger,
                LedgerWithoutPayOS = ledgerWithoutPayOS
            };
        }

        private async Task<EnterpriseFleetSnapshot> BuildFleetSnapshotAsync(
            DateTime now,
            DateTime trackingCutoff,
            DateTime monthStart,
            DateTime maintenanceWindowEnd,
            CancellationToken cancellationToken)
        {
            var carsQuery = _context.Cars.AsNoTracking().Where(c => !c.IsDeleted);

            var totalCars = await carsQuery.CountAsync(cancellationToken);
            var availableCars = await carsQuery.CountAsync(c => c.CarStatus != null && c.CarStatus.Name == CarStatusConstants.Available, cancellationToken);
            var rentedCars = await carsQuery.CountAsync(c => c.CarStatus != null && c.CarStatus.Name == CarStatusConstants.Rented, cancellationToken);
            var maintenanceCars = await carsQuery.CountAsync(c => c.CarStatus != null && c.CarStatus.Name == CarStatusConstants.Maintenance, cancellationToken);
            var pendingApprovalCars = await carsQuery.CountAsync(c => !c.IsApproved || (c.CarStatus != null && c.CarStatus.Name == CarStatusConstants.Pending), cancellationToken);
            var maintenanceDueSoon = await carsQuery.CountAsync(c =>
                c.NextMaintenanceDate != null &&
                c.NextMaintenanceDate <= maintenanceWindowEnd,
                cancellationToken);

            var recentlyTrackedCarIds = _context.VehicleTrackings.AsNoTracking()
                .Where(t => !t.IsDeleted && t.Timestamp >= trackingCutoff)
                .Select(t => t.CarID)
                .Distinct();

            var missingTracking = await carsQuery.CountAsync(c =>
                !recentlyTrackedCarIds.Contains(c.CarID),
                cancellationToken);

            var openAlerts = await _context.Alerts.AsNoTracking().CountAsync(a =>
                !a.IsDeleted &&
                a.AlertTime >= now.AddDays(-7) &&
                a.Status != "Resolved" &&
                a.Status != "Closed",
                cancellationToken);

            var monthBookings = await _context.Bookings.AsNoTracking()
                .Where(b =>
                    !b.IsDeleted &&
                    b.StartDate >= monthStart &&
                    b.BookingStatus != null &&
                    RevenueBookingStatuses.Contains(b.BookingStatus.Name))
                .Select(b => new { b.StartDate, b.EndDate })
                .ToListAsync(cancellationToken);

            var daysInMonth = DateTime.DaysInMonth(now.Year, now.Month);
            var totalCarDays = totalCars * daysInMonth;
            var rentedDays = monthBookings.Sum(b => Math.Max(1, (int)Math.Ceiling((b.EndDate - b.StartDate).TotalDays)));

            return new EnterpriseFleetSnapshot
            {
                TotalCars = totalCars,
                AvailableCars = availableCars,
                RentedCars = rentedCars,
                MaintenanceCars = maintenanceCars,
                PendingApprovalCars = pendingApprovalCars,
                MaintenanceDueSoon = maintenanceDueSoon,
                MissingTracking = missingTracking,
                OpenAlerts = openAlerts,
                MonthlyUtilizationPercent = totalCarDays > 0
                    ? Math.Round((decimal)rentedDays / totalCarDays * 100m, 1)
                    : 0m
            };
        }

        private static List<EnterpriseQueueItem> BuildQueues(
            int overdueReturns,
            int expiredActiveHolds,
            int pendingSettlements,
            int pendingRefunds,
            int pendingPrivacy,
            int breachedSupport,
            int pendingEkyc,
            EnterpriseFleetSnapshot fleet,
            EnterpriseFinanceSnapshot finance,
            int unsignedContracts,
            int handoversMissingEvidence)
        {
            return new List<EnterpriseQueueItem>
            {
                Queue("Overdue returns", "Active rentals past planned return time.", overdueReturns, "Fleet ops", "/Admin/Booking", EnterpriseSeverity.Critical),
                Queue("Expired active holds", "Reservation holds that should be released by the expiry job.", expiredActiveHolds, "Booking ops", "/Admin/Booking", EnterpriseSeverity.Critical),
                Queue("Settlement reviews", "Deposit deductions waiting for approval before ledger posting.", pendingSettlements, "Finance", "/Admin/Settlement", EnterpriseSeverity.Warning),
                Queue("Refund queue", "Refund requests not yet terminal.", pendingRefunds, "Finance", "/Admin/Payment", EnterpriseSeverity.Warning),
                Queue("PayOS reconciliation", "Provider and ledger entries that do not match.", finance.PayOSPaidWithoutLedger + finance.LedgerWithoutPayOS, "Finance", "/Admin/Payment", EnterpriseSeverity.Critical),
                Queue("Support SLA breach", "Open support cases past first response target.", breachedSupport, "Support", "/Admin/Support", EnterpriseSeverity.Critical),
                Queue("eKYC / eligibility", "Customers that need identity or eligibility review.", pendingEkyc, "Risk", "/Admin/User", EnterpriseSeverity.Warning),
                Queue("Fleet readiness", "Cars blocked by approval, maintenance, alerts, or tracking gaps.", fleet.PendingApprovalCars + fleet.MaintenanceDueSoon + fleet.MissingTracking + fleet.OpenAlerts, "Fleet", "/Admin/Car/Manage", EnterpriseSeverity.Warning),
                Queue("Privacy requests", "Runtime privacy requests waiting for review.", pendingPrivacy, "Compliance", "/Admin/Privacy", EnterpriseSeverity.Warning),
                Queue("Contract evidence", "Unsigned digital contracts or recent handovers missing evidence.", unsignedContracts + handoversMissingEvidence, "Legal ops", "/Admin/Booking", EnterpriseSeverity.Warning)
            };
        }

        private static EnterpriseQueueItem Queue(string title, string description, int count, string owner, string url, string alertSeverity)
        {
            return new EnterpriseQueueItem
            {
                Title = title,
                Description = description,
                Count = count,
                Owner = owner,
                Url = url,
                Severity = count > 0 ? alertSeverity : EnterpriseSeverity.Success
            };
        }

        private static List<EnterpriseCapabilityStatus> BuildCapabilities(
            int pendingSettlements,
            int pendingRefunds,
            int pendingPrivacy,
            int breachedSupport,
            int pendingEkyc,
            EnterpriseFleetSnapshot fleet,
            EnterpriseFinanceSnapshot finance,
            int unsignedContracts,
            int handoversMissingEvidence)
        {
            return new List<EnterpriseCapabilityStatus>
            {
                Capability("Customer app / self-service", EnterpriseCapabilityState.Partial, "Bookings, payments, refunds, support and privacy are connected.", "Add mobile-first trip center, receipts and roadside assistance.", "P1"),
                Capability("Reservation & inventory", EnterpriseCapabilityState.Partial, "BookingHold and ReservationService exist; expired hold queue is now visible.", "Enforce every booking path through reservation service and release jobs.", "P0"),
                Capability("Pricing & revenue", EnterpriseCapabilityState.Partial, "Holiday pricing, promotions, add-ons and membership are available.", "Add pricing simulation and revenue management workflow.", "P1"),
                Capability("Payment, ledger, refund, payout", finance.PayOSPaidWithoutLedger + finance.LedgerWithoutPayOS + pendingRefunds > 0 ? EnterpriseCapabilityState.NeedsEvidence : EnterpriseCapabilityState.Live, "Ledger, PayOS, refund and payout queues are monitored.", "Run daily reconciliation and maker-checker refund approval evidence.", "P0"),
                Capability("Fleet operations", fleet.PendingApprovalCars + fleet.MaintenanceDueSoon + fleet.MissingTracking + fleet.OpenAlerts > 0 ? EnterpriseCapabilityState.NeedsEvidence : EnterpriseCapabilityState.Partial, "Fleet readiness, maintenance risk, approval and tracking gaps are surfaced.", "Add staff mobile checklist and operational SLA evidence.", "P1"),
                Capability("Handover, contract, legal evidence", unsignedContracts + handoversMissingEvidence > 0 ? EnterpriseCapabilityState.NeedsEvidence : EnterpriseCapabilityState.Live, "Digital contracts, handover evidence and dispute export foundation exist.", "Close unsigned contracts and missing evidence queues.", "P0"),
                Capability("Trust, safety, insurance", pendingEkyc > 0 ? EnterpriseCapabilityState.NeedsEvidence : EnterpriseCapabilityState.Partial, "eKYC, blacklist and eligibility data are tracked.", "Add risk scoring, insurance packages and manual review queue.", "P1"),
                Capability("Partner / owner portal", EnterpriseCapabilityState.Partial, "Owner car onboarding and approval exist.", "Add owner calendar, settlement, payout and performance score.", "P1"),
                Capability("Driver / staff operations", EnterpriseCapabilityState.Planned, "Driver contracts and driver bookings exist in the domain.", "Add driver/staff task board and shift workflow.", "P2"),
                Capability("Admin enterprise console", EnterpriseCapabilityState.Live, "This command center consolidates P0/P1 queues.", "Add drill-down views per queue and saved filters.", "P1"),
                Capability("Reporting & BI", EnterpriseCapabilityState.Partial, "Admin reports and exports exist.", "Add scheduled finance/fleet/support reports.", "P2"),
                Capability("Notification & communication", breachedSupport > 0 ? EnterpriseCapabilityState.NeedsEvidence : EnterpriseCapabilityState.Partial, "Email, support and notifications exist.", "Add template manager, push/Zalo/SMS and booking chat workflow.", "P2"),
                Capability("API & integration", EnterpriseCapabilityState.Partial, "API keys and signed webhooks exist with production guard.", "Add sandbox, protected OpenAPI and partner SDK samples.", "P2"),
                Capability("Security, compliance, enterprise ops", pendingPrivacy > 0 ? EnterpriseCapabilityState.NeedsEvidence : EnterpriseCapabilityState.Partial, "Audit, privacy, snapshots, SLO and runbooks exist.", "Complete strict evidence gates, MFA hardening and CSP cleanup.", "P0")
            };
        }

        private static EnterpriseCapabilityStatus Capability(string module, string status, string currentCoverage, string nextStep, string priority)
        {
            return new EnterpriseCapabilityStatus
            {
                Module = module,
                Status = status,
                CurrentCoverage = currentCoverage,
                NextStep = nextStep,
                Priority = priority
            };
        }

        private static List<EnterpriseWorkflowGroup> BuildWorkflowGroups()
        {
            return new List<EnterpriseWorkflowGroup>
            {
                new()
                {
                    Role = "Customer operations",
                    Focus = "Bookings, trip self-service, refunds and support.",
                    Links = new List<EnterpriseWorkflowLink>
                    {
                        Link("Bookings", "Manage booking lifecycle and customer trip state.", "/Admin/Booking", "bi-calendar2-check"),
                        Link("Support tickets", "Triage customer help, claims and emergency issues.", "/Admin/Support", "bi-headset"),
                        Link("Privacy center", "Review data export, deletion and retention requests.", "/Admin/Privacy", "bi-shield-lock")
                    }
                },
                new()
                {
                    Role = "Finance operations",
                    Focus = "Ledger, provider reconciliation, refunds, deposit settlement and payout.",
                    Links = new List<EnterpriseWorkflowLink>
                    {
                        Link("Payments", "Review payments, provider state and manual exceptions.", "/Admin/Payment", "bi-cash-coin"),
                        Link("Settlement reviews", "Approve deposit deductions before ledger posting.", "/Admin/Settlement", "bi-receipt"),
                        Link("Reports", "Export revenue, fleet cost and operating reports.", "/Admin/Reports", "bi-bar-chart-line")
                    }
                },
                new()
                {
                    Role = "Fleet and area operations",
                    Focus = "Vehicle readiness, tracking, maintenance and branch execution.",
                    Links = new List<EnterpriseWorkflowLink>
                    {
                        Link("Fleet", "Manage vehicles, approval state and readiness.", "/Admin/Car/Manage", "bi-car-front"),
                        Link("Maintenance", "Track service work and upcoming maintenance risk.", "/Admin/Maintenance", "bi-tools"),
                        Link("Tracking", "Review map, IoT heartbeat and location exceptions.", "/Tracking/Map", "bi-radar")
                    }
                },
                new()
                {
                    Role = "Security and compliance",
                    Focus = "Audit chain, backup evidence, API safety and production readiness.",
                    Links = new List<EnterpriseWorkflowLink>
                    {
                        Link("Audit log", "Inspect sensitive admin and financial actions.", "/Admin/AuditLog", "bi-clipboard-data"),
                        Link("Backups", "Review snapshot and restore readiness.", "/Admin/Snapshot", "bi-archive"),
                        Link("SLO", "Monitor production health and reliability signals.", "/Admin/Sre/Slo", "bi-activity")
                    }
                }
            };
        }

        private static EnterpriseWorkflowLink Link(string label, string description, string url, string icon)
        {
            return new EnterpriseWorkflowLink
            {
                Label = label,
                Description = description,
                Url = url,
                Icon = icon
            };
        }
    }
}
