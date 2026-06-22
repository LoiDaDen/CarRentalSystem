using System.Text.Json;
using CarRentalSystem.Data;
using CarRentalSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace CarRentalSystem.Services;

public interface IInternalPortalScopeService
{
    Task<IReadOnlyList<int>> GetAllowedLocationIdsAsync(int userId, string role);
    Task<bool> CanAccessBookingAsync(int userId, string role, int bookingId);
    Task<bool> CanAccessCarAsync(int userId, string role, int carId);
    Task<int?> ResolveCurrentAreaIdAsync(int userId, string role, int? requestedAreaId);
}

public sealed class InternalPortalScopeService : IInternalPortalScopeService
{
    private readonly ApplicationDbContext _context;
    private readonly IAreaService _areaService;

    public InternalPortalScopeService(ApplicationDbContext context, IAreaService areaService)
    {
        _context = context;
        _areaService = areaService;
    }

    public async Task<IReadOnlyList<int>> GetAllowedLocationIdsAsync(int userId, string role)
    {
        if (role == UserRoleConstants.Admin)
            return await _context.Locations.AsNoTracking().Select(location => location.LocationID).ToListAsync();

        return await _areaService.GetLocationIdsForUserAsync(userId, role);
    }

    public async Task<bool> CanAccessBookingAsync(int userId, string role, int bookingId)
    {
        var allowedLocations = await GetAllowedLocationIdsAsync(userId, role);
        return await _context.Bookings.AsNoTracking()
            .AnyAsync(booking => booking.BookingID == bookingId &&
                (allowedLocations.Contains(booking.PickupLocationID) ||
                 allowedLocations.Contains(booking.ReturnLocationID) ||
                 (booking.Car != null && allowedLocations.Contains(booking.Car.LocationID))));
    }

    public async Task<bool> CanAccessCarAsync(int userId, string role, int carId)
    {
        var allowedLocations = await GetAllowedLocationIdsAsync(userId, role);
        return await _context.Cars.AsNoTracking()
            .AnyAsync(car => car.CarID == carId && allowedLocations.Contains(car.LocationID));
    }

    public async Task<int?> ResolveCurrentAreaIdAsync(int userId, string role, int? requestedAreaId)
    {
        if (role == UserRoleConstants.Admin)
            return requestedAreaId;

        var allowedAreas = await _areaService.GetAreaIdsForUserAsync(userId);
        if (requestedAreaId.HasValue && allowedAreas.Contains(requestedAreaId.Value))
            return requestedAreaId;

        return allowedAreas.FirstOrDefault() is var first && first > 0 ? first : null;
    }
}

public record BookingAssignmentResult(bool IsSuccess, string Message);

public interface IBookingAssignmentService
{
    Task<BookingAssignmentResult> AssignAsync(int bookingId, int staffUserId, int managerUserId);
    Task<BookingAssignmentResult> ClearAsync(int bookingId, int managerUserId);
}

public sealed class BookingAssignmentService : IBookingAssignmentService
{
    private readonly ApplicationDbContext _context;
    private readonly IAreaService _areaService;
    private readonly IAuditTrailService _auditTrail;

    public BookingAssignmentService(ApplicationDbContext context, IAreaService areaService, IAuditTrailService auditTrail)
    {
        _context = context;
        _areaService = areaService;
        _auditTrail = auditTrail;
    }

    public async Task<BookingAssignmentResult> AssignAsync(int bookingId, int staffUserId, int managerUserId)
    {
        var booking = await _context.Bookings.Include(item => item.Car).FirstOrDefaultAsync(item => item.BookingID == bookingId);
        if (booking?.Car == null)
            return new(false, "Không tìm thấy booking hoặc xe.");

        var areaId = await _areaService.GetAreaIdForLocationAsync(booking.Car.LocationID);
        if (!areaId.HasValue || !await _areaService.IsUserAreaManagerAsync(managerUserId, areaId.Value))
            return new(false, "Manager không có quyền phân công booking ngoài khu vực.");

        var validStaff = await _context.AreaStaffs.AsNoTracking()
            .AnyAsync(item => item.AreaID == areaId.Value &&
                item.UserID == staffUserId &&
                !item.IsDeleted &&
                item.User != null &&
                item.User.Role == UserRoleConstants.Staff);
        if (!validStaff)
            return new(false, "Nhân viên không thuộc khu vực của booking.");

        var oldStaffId = booking.AssignedStaffUserID;
        booking.AssignedStaffUserID = staffUserId;
        booking.AssignedByUserID = managerUserId;
        booking.AssignedAtUtc = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        await WriteAuditAsync("AssignBooking", bookingId, managerUserId, oldStaffId, staffUserId);
        return new(true, "Đã phân công booking cho nhân viên.");
    }

    public async Task<BookingAssignmentResult> ClearAsync(int bookingId, int managerUserId)
    {
        var booking = await _context.Bookings.Include(item => item.Car).FirstOrDefaultAsync(item => item.BookingID == bookingId);
        if (booking?.Car == null)
            return new(false, "Không tìm thấy booking hoặc xe.");

        var areaId = await _areaService.GetAreaIdForLocationAsync(booking.Car.LocationID);
        if (!areaId.HasValue || !await _areaService.IsUserAreaManagerAsync(managerUserId, areaId.Value))
            return new(false, "Manager không có quyền thay đổi booking ngoài khu vực.");

        var oldStaffId = booking.AssignedStaffUserID;
        booking.AssignedStaffUserID = null;
        booking.AssignedByUserID = managerUserId;
        booking.AssignedAtUtc = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        await WriteAuditAsync("UnassignBooking", bookingId, managerUserId, oldStaffId, null);
        return new(true, "Đã bỏ phân công booking.");
    }

    private Task<AuditLog> WriteAuditAsync(string action, int bookingId, int actorUserId, int? oldStaffId, int? newStaffId) =>
        _auditTrail.RecordAsync(new AuditLog
        {
            Action = action,
            EntityType = nameof(Booking),
            EntityId = bookingId,
            UserId = actorUserId,
            Description = "Internal portal booking assignment changed.",
            OldValues = JsonSerializer.Serialize(new { AssignedStaffUserID = oldStaffId }),
            NewValues = JsonSerializer.Serialize(new { AssignedStaffUserID = newStaffId })
        });
}

public sealed record HandoverQueueItem(
    int BookingID,
    string CustomerName,
    string CarName,
    string LicensePlate,
    DateTime DueAt,
    string Status,
    bool HasRecord);

public interface IHandoverWorkflowService
{
    Task<IReadOnlyList<HandoverQueueItem>> GetQueueAsync(int userId, string role, string type);
}

public sealed class HandoverWorkflowService : IHandoverWorkflowService
{
    private readonly ApplicationDbContext _context;
    private readonly IInternalPortalScopeService _scope;

    public HandoverWorkflowService(ApplicationDbContext context, IInternalPortalScopeService scope)
    {
        _context = context;
        _scope = scope;
    }

    public async Task<IReadOnlyList<HandoverQueueItem>> GetQueueAsync(int userId, string role, string type)
    {
        var locations = await _scope.GetAllowedLocationIdsAsync(userId, role);
        var isReturn = string.Equals(type, "CheckOut", StringComparison.OrdinalIgnoreCase);
        var requiredStatus = isReturn ? BookingStatusConstants.Active : BookingStatusConstants.Confirmed;

        return await _context.Bookings.AsNoTracking()
            .Where(booking => booking.BookingStatus != null &&
                booking.BookingStatus.Name == requiredStatus &&
                booking.Car != null &&
                locations.Contains(booking.Car.LocationID))
            .OrderBy(booking => isReturn ? booking.EndDate : booking.StartDate)
            .Take(100)
            .Select(booking => new HandoverQueueItem(
                booking.BookingID,
                booking.User != null ? booking.User.Name : "Khách hàng",
                booking.Car != null ? booking.Car.CarName : "Xe",
                booking.Car != null ? booking.Car.LicensePlate : string.Empty,
                isReturn ? booking.EndDate : booking.StartDate,
                booking.BookingStatus != null ? booking.BookingStatus.Name : string.Empty,
                booking.HandoverRecords != null && booking.HandoverRecords.Any(record => record.Type == type && !record.IsDeleted)))
            .ToListAsync();
    }
}

public sealed record ManagerPortalSnapshot(
    int TodayBookings,
    decimal TodayRevenue,
    int ActiveRentals,
    int DueReturns,
    int OpenTickets,
    decimal CancellationRate,
    int FleetCount,
    int MaintenanceCount);

public interface IManagerReportService
{
    Task<ManagerPortalSnapshot> GetSnapshotAsync(int managerUserId);
}

public sealed class ManagerReportService : IManagerReportService
{
    private readonly ApplicationDbContext _context;
    private readonly IInternalPortalScopeService _scope;

    public ManagerReportService(ApplicationDbContext context, IInternalPortalScopeService scope)
    {
        _context = context;
        _scope = scope;
    }

    public async Task<ManagerPortalSnapshot> GetSnapshotAsync(int managerUserId)
    {
        var locations = await _scope.GetAllowedLocationIdsAsync(managerUserId, UserRoleConstants.Manager);
        var today = DateTime.UtcNow.Date;
        var tomorrow = today.AddDays(1);
        var bookings = _context.Bookings.AsNoTracking()
            .Where(booking => booking.Car != null && locations.Contains(booking.Car.LocationID));
        var todayBookings = bookings.Where(booking => booking.CreatedAt >= today && booking.CreatedAt < tomorrow);
        var todayCount = await todayBookings.CountAsync();
        var cancelled = await todayBookings.CountAsync(booking => booking.BookingStatus != null && booking.BookingStatus.Name == BookingStatusConstants.Cancelled);

        return new ManagerPortalSnapshot(
            todayCount,
            await todayBookings.Where(booking => booking.BookingStatus != null && booking.BookingStatus.Name != BookingStatusConstants.Cancelled).SumAsync(booking => booking.TotalPrice),
            await bookings.CountAsync(booking => booking.BookingStatus != null && booking.BookingStatus.Name == BookingStatusConstants.Active),
            await bookings.CountAsync(booking => booking.BookingStatus != null && booking.BookingStatus.Name == BookingStatusConstants.Active && booking.EndDate < tomorrow),
            await _context.SupportTickets.AsNoTracking().CountAsync(ticket =>
                ticket.Status != SupportTicketStatusConstants.Closed &&
                ticket.Status != SupportTicketStatusConstants.Resolved &&
                (ticket.AssignedToUserID == managerUserId ||
                 (ticket.Booking != null && ticket.Booking.Car != null && locations.Contains(ticket.Booking.Car.LocationID)))),
            todayCount == 0 ? 0 : Math.Round((decimal)cancelled / todayCount * 100, 1),
            await _context.Cars.AsNoTracking().CountAsync(car => !car.IsDeleted && locations.Contains(car.LocationID)),
            await _context.Cars.AsNoTracking().CountAsync(car => !car.IsDeleted && locations.Contains(car.LocationID) && car.CarStatus != null && car.CarStatus.Name == CarStatusConstants.Maintenance));
    }
}
