using CarRentalSystem.Models;
using CarRentalSystem.Services;

namespace CarRentalSystem.ViewModels;

public sealed record PortalBookingRowViewModel(
    int BookingID,
    string CustomerName,
    string MaskedPhone,
    string CarName,
    string LicensePlate,
    DateTime StartDate,
    DateTime EndDate,
    string Status,
    decimal TotalPrice,
    int? AssignedStaffUserID,
    string? AssignedStaffName);

public sealed record StaffDashboardViewModel(
    int TodayBookings,
    int HandoverDue,
    int ReturnDue,
    int OpenTickets,
    IReadOnlyList<PortalBookingRowViewModel> AssignedTasks);

public sealed record ManagerDashboardViewModel(
    ManagerPortalSnapshot Snapshot,
    IReadOnlyList<PortalBookingRowViewModel> Alerts);

public sealed record PortalProfileViewModel(
    string Name,
    string Email,
    string? MaskedPhone,
    string Role,
    IReadOnlyList<string> Areas);

public sealed record ManagerStaffRowViewModel(
    int UserID,
    string Name,
    string Email,
    int AreaID,
    string AreaName,
    DateTime AssignedAt,
    int AssignedBookingCount);

public sealed record ManagerOperationViewModel(
    IReadOnlyList<HandoverQueueItem> Handovers,
    IReadOnlyList<HandoverQueueItem> Returns,
    int OpenAlerts,
    int MaintenanceCars);

public sealed record ManagerReportViewModel(
    ManagerPortalSnapshot Snapshot,
    IReadOnlyDictionary<string, int> BookingStatuses,
    IReadOnlyList<PortalBookingRowViewModel> RecentBookings);

public static class PortalViewModelMasking
{
    public static string MaskPhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
            return "Chưa cập nhật";

        var visible = phone.Length <= 3 ? phone : phone[^3..];
        return new string('*', Math.Max(0, phone.Length - visible.Length)) + visible;
    }
}
