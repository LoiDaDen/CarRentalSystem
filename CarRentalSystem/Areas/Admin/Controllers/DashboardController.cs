using CarRentalSystem.Areas.Admin.Models;
using CarRentalSystem.Authorization;
using CarRentalSystem.Data;
using CarRentalSystem.Models;
using CarRentalSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CarRentalSystem.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Policy = PortalPolicyNames.RequireAdmin)]
    public class DashboardController : Controller
    {
        private static readonly string[] HiddenBookingStatuses = { BookingStatusConstants.Draft, BookingStatusConstants.Unpaid };
        private static readonly string[] RevenueBookingStatuses = { BookingStatusConstants.Completed, BookingStatusConstants.Confirmed };
        private static readonly string[] TerminalSupportStatuses = { SupportTicketStatusConstants.Resolved, SupportTicketStatusConstants.Closed };
        private static readonly string[] ActiveCarChangeStatuses =
        {
            CarChangeRequestStatusConstants.PendingStaff,
            CarChangeRequestStatusConstants.PendingManager,
            CarChangeRequestStatusConstants.PendingAdmin
        };

        private readonly ApplicationDbContext _context;
        private readonly IRevenueMetricsService? _revenueMetrics;
        private readonly ILogger<DashboardController>? _logger;

        public DashboardController(
            ICarService carService,
            IBookingService bookingService,
            ApplicationDbContext context,
            IRevenueMetricsService? revenueMetrics = null,
            ILogger<DashboardController>? logger = null)
        {
            _context = context;
            _revenueMetrics = revenueMetrics;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> Index(CancellationToken cancellationToken = default)
        {
            var nowUtc = DateTime.UtcNow;
            var (businessDate, todayStartUtc, tomorrowStartUtc) = GetVietnamBusinessDay(nowUtc);
            var model = new DashboardViewModel
            {
                GeneratedAtUtc = nowUtc,
                BusinessDate = businessDate
            };

            try
            {
                await PopulateDashboardAsync(model, nowUtc, todayStartUtc, tomorrowStartUtc, cancellationToken);
            }
            catch (Exception exception)
            {
                _logger?.LogError(exception, "Admin dashboard data load failed.");
                model.HasLoadError = true;
                model.LoadErrorMessage = "Một số dữ liệu vận hành chưa thể tải. Vui lòng thử lại sau.";
            }

            return View(model);
        }

        private async Task PopulateDashboardAsync(
            DashboardViewModel model,
            DateTime nowUtc,
            DateTime todayStartUtc,
            DateTime tomorrowStartUtc,
            CancellationToken cancellationToken)
        {
            var bookingsQuery = _context.Bookings
                .AsNoTracking()
                .Where(booking => !booking.IsDeleted &&
                    (booking.BookingStatus == null || !HiddenBookingStatuses.Contains(booking.BookingStatus.Name)));
            var carsQuery = _context.Cars.AsNoTracking().Where(car => !car.IsDeleted);

            model.TotalUsers = await _context.Users.AsNoTracking().CountAsync(cancellationToken);
            model.TotalCars = await carsQuery.CountAsync(cancellationToken);
            model.AvailableCars = await carsQuery.CountAsync(car => car.CarStatus != null && car.CarStatus.Name == CarStatusConstants.Available, cancellationToken);
            model.RentedCars = await carsQuery.CountAsync(car => car.CarStatus != null && car.CarStatus.Name == CarStatusConstants.Rented, cancellationToken);
            model.MaintenanceCars = await carsQuery.CountAsync(car => car.CarStatus != null && car.CarStatus.Name == CarStatusConstants.Maintenance, cancellationToken);
            model.PendingApprovalCars = await _context.CarChangeRequests
                .AsNoTracking()
                .CountAsync(request => ActiveCarChangeStatuses.Contains(request.Status), cancellationToken);
            model.TotalBookings = await bookingsQuery.CountAsync(cancellationToken);
            model.TodayBookings = await bookingsQuery.CountAsync(booking => booking.CreatedAt >= todayStartUtc && booking.CreatedAt < tomorrowStartUtc, cancellationToken);
            model.PendingBookings = await bookingsQuery.CountAsync(booking => booking.BookingStatus != null && booking.BookingStatus.Name == BookingStatusConstants.Pending, cancellationToken);
            model.OpenSupportTickets = await _context.SupportTickets.AsNoTracking().CountAsync(ticket => !TerminalSupportStatuses.Contains(ticket.Status), cancellationToken);
            model.TotalRevenue = await bookingsQuery
                .Where(booking => booking.BookingStatus != null && RevenueBookingStatuses.Contains(booking.BookingStatus.Name))
                .SumAsync(booking => booking.TotalPrice, cancellationToken);

            var heldDeposits = bookingsQuery.Where(booking =>
                booking.DepositStatus == DepositStatusConstants.Holding &&
                booking.ActualReturnDate == null);
            model.TotalHeldDepositsAmount = await heldDeposits.SumAsync(booking => booking.DepositAmount, cancellationToken);
            model.TotalHeldDepositsCount = await heldDeposits.CountAsync(cancellationToken);

            var last30Days = nowUtc.AddDays(-30);
            var recentBookingCount = await bookingsQuery.CountAsync(booking => booking.CreatedAt >= last30Days, cancellationToken);
            var cancelledBookingCount = await bookingsQuery.CountAsync(booking =>
                booking.CreatedAt >= last30Days &&
                booking.BookingStatus != null &&
                (booking.BookingStatus.Name == BookingStatusConstants.Cancelled || booking.BookingStatus.Name == BookingStatusConstants.Rejected),
                cancellationToken);
            model.CancellationRate30Days = recentBookingCount > 0
                ? Math.Round(cancelledBookingCount * 100m / recentBookingCount, 1)
                : 0m;

            model.OverdueBookings = await bookingsQuery.CountAsync(booking =>
                booking.EndDate < nowUtc &&
                booking.ActualReturnDate == null &&
                booking.BookingStatus != null &&
                booking.BookingStatus.Name == BookingStatusConstants.Active,
                cancellationToken);
            model.MaintenanceDueSoon = await carsQuery.CountAsync(car =>
                car.NextMaintenanceDate != null && car.NextMaintenanceDate <= nowUtc.AddDays(14),
                cancellationToken);
            model.StalePendingPayments = await _context.PayOSTransactions.AsNoTracking().CountAsync(transaction =>
                transaction.Status == PaymentStatusConstants.Pending && transaction.CreatedAt < nowUtc.AddHours(-24),
                cancellationToken);
            model.PendingEkyc = await _context.Users.AsNoTracking().CountAsync(user =>
                user.Role == UserRoleConstants.Customer &&
                (user.VerificationStatus == "Pending" || !user.IsVerified || !user.IsEmailVerified),
                cancellationToken);
            model.HighRiskUsers = await _context.CustomerRiskAssessments.AsNoTracking().CountAsync(risk =>
                risk.AssessedAtUtc >= last30Days &&
                (risk.RiskLevel == CustomerRiskLevels.High || risk.RiskLevel == CustomerRiskLevels.Critical),
                cancellationToken);

            model.RecentBookings = await bookingsQuery
                .Include(booking => booking.BookingStatus)
                .Include(booking => booking.User)
                .Include(booking => booking.Car).ThenInclude(car => car!.CarBrand)
                .OrderByDescending(booking => booking.CreatedAt)
                .Take(6)
                .ToListAsync(cancellationToken);
            model.PendingCars = await carsQuery
                .Include(car => car.CarBrand)
                .Include(car => car.Owner)
                .Where(car => !car.IsApproved || (car.CarStatus != null && car.CarStatus.Name == CarStatusConstants.Pending))
                .OrderByDescending(car => car.CreatedAt)
                .Take(6)
                .ToListAsync(cancellationToken);
            model.RecentPayments = await _context.Payments.AsNoTracking()
                .Include(payment => payment.Booking).ThenInclude(booking => booking!.User)
                .OrderByDescending(payment => payment.PaymentDate)
                .Take(6)
                .ToListAsync(cancellationToken);
            model.RecentSupportTickets = await _context.SupportTickets.AsNoTracking()
                .Include(ticket => ticket.User)
                .OrderByDescending(ticket => ticket.CreatedAt)
                .Take(6)
                .ToListAsync(cancellationToken);
            model.MaintenanceLogs = await _context.MaintenanceLogs.AsNoTracking()
                .Include(log => log.Car).ThenInclude(car => car!.CarBrand)
                .Where(log => !log.IsDeleted)
                .OrderByDescending(log => log.ServiceDate)
                .Take(5)
                .ToListAsync(cancellationToken);

            var statusRows = await bookingsQuery
                .Select(booking => booking.BookingStatus != null ? booking.BookingStatus.Name : "Unknown")
                .GroupBy(status => status)
                .Select(group => new { Status = group.Key, Count = group.Count() })
                .OrderByDescending(group => group.Count)
                .ToListAsync(cancellationToken);
            model.StatusGroups = statusRows.ToDictionary(row => row.Status, row => row.Count, StringComparer.OrdinalIgnoreCase);
            model.BookingLabels = statusRows.Select(row => row.Status).ToList();
            model.BookingData = statusRows.Select(row => row.Count).ToList();

            var carStatusRows = await carsQuery
                .Select(car => car.CarStatus != null ? car.CarStatus.Name : "Unknown")
                .GroupBy(status => status)
                .Select(group => new { Status = group.Key, Count = group.Count() })
                .OrderByDescending(group => group.Count)
                .ToListAsync(cancellationToken);
            model.CarLabels = carStatusRows.Select(row => row.Status).ToList();
            model.CarData = carStatusRows.Select(row => row.Count).ToList();

            var topCarRows = await bookingsQuery
                .Where(booking => booking.BookingStatus != null && booking.BookingStatus.Name == BookingStatusConstants.Completed)
                .GroupBy(booking => booking.CarID)
                .Select(group => new { CarID = group.Key, RentalCount = group.Count(), TotalRevenue = group.Sum(booking => booking.TotalPrice) })
                .OrderByDescending(row => row.RentalCount)
                .Take(5)
                .ToListAsync(cancellationToken);
            var topCarLookup = await carsQuery
                .Include(car => car.CarBrand)
                .Where(car => topCarRows.Select(row => row.CarID).Contains(car.CarID))
                .ToDictionaryAsync(car => car.CarID, cancellationToken);
            model.TopCars = topCarRows
                .Where(row => topCarLookup.ContainsKey(row.CarID))
                .Select(row => new TopCarViewModel { Car = topCarLookup[row.CarID], RentalCount = row.RentalCount, TotalRevenue = row.TotalRevenue })
                .ToList();

            await PopulateDepositFlowAsync(model, todayStartUtc, tomorrowStartUtc, cancellationToken);
        }

        private async Task PopulateDepositFlowAsync(
            DashboardViewModel model,
            DateTime todayStartUtc,
            DateTime tomorrowStartUtc,
            CancellationToken cancellationToken)
        {
            var sevenDayStartUtc = todayStartUtc.AddDays(-6);

            model.DepositLabels = Enumerable.Range(0, 7)
                .Select(offset => model.BusinessDate.AddDays(offset - 6).ToString("dd/MM"))
                .ToList();
            model.DepositData = new List<decimal>(7);

            for (var offset = 0; offset < 7; offset++)
            {
                var periodStartUtc = sevenDayStartUtc.AddDays(offset);
                var periodEndUtc = periodStartUtc.AddDays(1);
                var depositAmount = await GetDepositChargesAsync(periodStartUtc, periodEndUtc, cancellationToken);
                model.DepositData.Add(depositAmount / 1_000_000m);

                if (offset == 6)
                    model.TodayDepositAmount = depositAmount;
            }
        }

        private async Task<decimal> GetDepositChargesAsync(DateTime periodStartUtc, DateTime periodEndUtc, CancellationToken cancellationToken)
        {
            if (_revenueMetrics != null)
            {
                var snapshot = await _revenueMetrics.GetSnapshotAsync(periodStartUtc, periodEndUtc, cancellationToken);
                return snapshot.CustomerCharges;
            }

            return await _context.PaymentLedgerEntries
                .AsNoTracking()
                .Where(entry =>
                    entry.CreatedAt >= periodStartUtc &&
                    entry.CreatedAt < periodEndUtc &&
                    entry.EntryType == PaymentLedgerEntryTypeConstants.DepositCharge &&
                    entry.Status == PaymentStatusConstants.Paid)
                .SumAsync(entry => (decimal?)entry.Amount, cancellationToken) ?? 0m;
        }

        internal static (DateTime BusinessDate, DateTime StartUtc, DateTime EndUtc) GetVietnamBusinessDay(DateTime nowUtc)
        {
            TimeZoneInfo timeZone;
            try
            {
                timeZone = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
            }
            catch (TimeZoneNotFoundException)
            {
                timeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");
            }

            var localNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(nowUtc, DateTimeKind.Utc), timeZone);
            var localStart = DateTime.SpecifyKind(localNow.Date, DateTimeKind.Unspecified);
            return (
                localNow.Date,
                TimeZoneInfo.ConvertTimeToUtc(localStart, timeZone),
                TimeZoneInfo.ConvertTimeToUtc(localStart.AddDays(1), timeZone));
        }
    }
}
