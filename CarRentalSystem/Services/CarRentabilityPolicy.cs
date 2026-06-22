using CarRentalSystem.Models;

namespace CarRentalSystem.Services;

public static class CarRentabilityPolicy
{
    public static IQueryable<Car> WhereSelfDriveRentable(this IQueryable<Car> query)
    {
        return query.Where(car =>
            car.IsApproved &&
            car.ApprovalStatus == CarApprovalStatusConstants.Approved &&
            !car.IsPendingDeletion &&
            car.IsSelfDriveAllowed &&
            !car.IsWithDriverAllowed &&
            car.CarStatus != null &&
            car.CarStatus.Name == CarStatusConstants.Available);
    }

    public static bool IsSelfDriveRentable(Car? car)
    {
        return car != null &&
            car.IsApproved &&
            car.ApprovalStatus == CarApprovalStatusConstants.Approved &&
            !car.IsPendingDeletion &&
            car.IsSelfDriveAllowed &&
            !car.IsWithDriverAllowed &&
            car.CarStatus?.Name == CarStatusConstants.Available;
    }

    public static string GetUnavailableReason(Car? car)
    {
        if (car == null)
            return "Xe không tồn tại.";
        if (!car.IsApproved || car.ApprovalStatus != CarApprovalStatusConstants.Approved)
            return "Xe chưa được phê duyệt để cho thuê.";
        if (car.IsPendingDeletion)
            return "Xe đang chờ duyệt yêu cầu xóa và tạm ngưng nhận booking.";
        if (!car.IsSelfDriveAllowed || car.IsWithDriverAllowed)
            return "Xe này không thuộc luồng thuê xe tự lái.";
        if (car.CarStatus?.Name == CarStatusConstants.Maintenance)
            return "Xe đang bảo trì và chưa thể cho thuê.";
        if (car.CarStatus?.Name != CarStatusConstants.Available)
            return "Xe hiện không có sẵn để thuê.";

        return "Xe hiện không thể cho thuê.";
    }
}
