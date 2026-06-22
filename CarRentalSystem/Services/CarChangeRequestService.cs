using System.Text.Json;
using CarRentalSystem.Data;
using CarRentalSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace CarRentalSystem.Services;

public interface ICarChangeRequestService
{
    Task<CarChangeRequest> CreateAsync(Car car, string operation, int ownerId, CarChangeSnapshot? snapshot, string? ownerNote = null, CancellationToken cancellationToken = default);
    Task<bool> HasActiveRequestAsync(int carId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CarChangeRequest>> GetQueueAsync(int userId, string role, string statusFilter = CarApprovalStatusConstants.Pending, CancellationToken cancellationToken = default);
    Task<CarChangeApprovalResult> ApproveAsync(int requestId, int actorUserId, string role, string? note = null, CancellationToken cancellationToken = default);
    Task<CarChangeApprovalResult> RejectAsync(int requestId, int actorUserId, string role, string reason, CancellationToken cancellationToken = default);
}

public sealed record CarChangeApprovalResult(bool Success, string Message, CarChangeRequest? Request = null);

public sealed class CarChangeRequestService : ICarChangeRequestService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly string[] ActiveStatuses =
    {
        CarChangeRequestStatusConstants.PendingStaff,
        CarChangeRequestStatusConstants.PendingManager,
        CarChangeRequestStatusConstants.PendingAdmin
    };

    private readonly ApplicationDbContext _context;
    private readonly IAreaService _areaService;
    private readonly IAuditTrailService _auditTrail;

    public CarChangeRequestService(ApplicationDbContext context, IAreaService areaService, IAuditTrailService auditTrail)
    {
        _context = context;
        _areaService = areaService;
        _auditTrail = auditTrail;
    }

    public async Task<CarChangeRequest> CreateAsync(Car car, string operation, int ownerId, CarChangeSnapshot? snapshot, string? ownerNote = null, CancellationToken cancellationToken = default)
    {
        if (await HasActiveRequestAsync(car.CarID, cancellationToken))
            throw new InvalidOperationException("Xe đang có yêu cầu thay đổi chờ duyệt.");

        var areaId = await _areaService.GetAreaIdForLocationAsync(car.LocationID);
        var startsAtAdmin = areaId == null;
        var request = new CarChangeRequest
        {
            CarID = car.CarID,
            OwnerID = ownerId,
            AreaID = areaId,
            Operation = operation,
            Status = startsAtAdmin
                ? CarChangeRequestStatusConstants.PendingAdmin
                : CarChangeRequestStatusConstants.PendingStaff,
            CurrentStage = startsAtAdmin
                ? CarChangeApprovalStageConstants.Admin
                : CarChangeApprovalStageConstants.Staff,
            SnapshotJson = snapshot == null ? null : JsonSerializer.Serialize(snapshot, JsonOptions),
            OwnerNote = Truncate(ownerNote, 500),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        request.ApprovalSteps.Add(new CarChangeApprovalStep { Stage = request.CurrentStage });

        if (startsAtAdmin)
        {
            foreach (var skippedStep in request.ApprovalSteps.Where(step => step.Stage != CarChangeApprovalStageConstants.Admin))
            {
                skippedStep.Decision = CarChangeApprovalDecisionConstants.Approved;
                skippedStep.DecidedAt = DateTime.UtcNow;
                skippedStep.Note = "Tự động bỏ qua vì xe chưa gắn khu vực.";
            }
        }

        _context.CarChangeRequests.Add(request);
        await _context.SaveChangesAsync(cancellationToken);

        await RecordAuditAsync("CreateCarChangeRequest", actorUserId: ownerId, request, new
        {
            request.Operation,
            request.Status,
            request.CurrentStage,
            request.AreaID
        }, cancellationToken);

        return request;
    }

    public Task<bool> HasActiveRequestAsync(int carId, CancellationToken cancellationToken = default)
    {
        return _context.CarChangeRequests
            .AsNoTracking()
            .AnyAsync(request => request.CarID == carId && ActiveStatuses.Contains(request.Status), cancellationToken);
    }

    public async Task<IReadOnlyList<CarChangeRequest>> GetQueueAsync(int userId, string role, string statusFilter = CarApprovalStatusConstants.Pending, CancellationToken cancellationToken = default)
    {
        var normalizedStatus = NormalizeQueueStatusFilter(statusFilter);
        var requests = await _context.CarChangeRequests
            .AsNoTracking()
            .Include(request => request.ApprovalSteps)
            .ToListAsync(cancellationToken);
        await HydrateQueueRequestsAsync(requests, cancellationToken);

        IEnumerable<CarChangeRequest> query = normalizedStatus == CarApprovalStatusConstants.Pending
            ? requests.Where(request =>
                request.Status == CarChangeRequestStatusConstants.PendingStaff ||
                request.Status == CarChangeRequestStatusConstants.PendingManager ||
                request.Status == CarChangeRequestStatusConstants.PendingAdmin)
            : requests.Where(request => request.Status == normalizedStatus);

        if (string.Equals(role, UserRoleConstants.Staff, StringComparison.OrdinalIgnoreCase))
        {
            var areaIds = await _areaService.GetAreaIdsForUserAsync(userId);
            query = query.Where(request =>
                request.AreaID.HasValue &&
                areaIds.Contains(request.AreaID.Value));
        }
        else if (string.Equals(role, UserRoleConstants.Manager, StringComparison.OrdinalIgnoreCase))
        {
            var areaIds = await _context.AreaStaffs
                .AsNoTracking()
                .Where(item => item.UserID == userId &&
                    item.RoleInArea.ToLower() == AreaStaffRoleConstants.Manager &&
                    !item.IsDeleted)
                .Select(item => item.AreaID)
                .ToListAsync(cancellationToken);

            query = query.Where(request =>
                request.AreaID.HasValue &&
                areaIds.Contains(request.AreaID.Value));
        }
        else if (string.Equals(role, UserRoleConstants.Admin, StringComparison.OrdinalIgnoreCase))
        {
            // Admin sees the full active queue, including requests without a resolved area.
        }
        else
        {
            return Array.Empty<CarChangeRequest>();
        }

        return query
            .OrderBy(request => request.CreatedAt)
            .ThenBy(request => request.CarID)
            .ToList();
    }

    private async Task HydrateQueueRequestsAsync(IReadOnlyCollection<CarChangeRequest> requests, CancellationToken cancellationToken)
    {
        if (requests.Count == 0)
            return;

        var carIds = requests.Select(request => request.CarID).Distinct().ToList();
        var cars = await _context.Cars
            .AsNoTracking()
            .Where(car => carIds.Contains(car.CarID))
            .ToDictionaryAsync(car => car.CarID, cancellationToken);

        var brandIds = cars.Values.Select(car => car.BrandID).Distinct().ToList();
        var brands = await _context.CarBrands
            .AsNoTracking()
            .Where(brand => brandIds.Contains(brand.BrandID))
            .ToDictionaryAsync(brand => brand.BrandID, cancellationToken);

        var modelIds = cars.Values
            .Where(car => car.CarModelID.HasValue)
            .Select(car => car.CarModelID!.Value)
            .Distinct()
            .ToList();
        var models = await _context.CarModels
            .AsNoTracking()
            .Where(model => modelIds.Contains(model.CarModelID))
            .ToDictionaryAsync(model => model.CarModelID, cancellationToken);

        var images = await _context.CarImages
            .AsNoTracking()
            .Where(image => carIds.Contains(image.CarID))
            .OrderBy(image => image.ImageID)
            .ToListAsync(cancellationToken);
        var imagesByCar = images
            .GroupBy(image => image.CarID)
            .ToDictionary(group => group.Key, group => (ICollection<CarImage>)group.ToList());

        var ownerIds = requests.Select(request => request.OwnerID).Distinct().ToList();
        var owners = await _context.Users
            .AsNoTracking()
            .Where(user => ownerIds.Contains(user.UserID))
            .ToDictionaryAsync(user => user.UserID, cancellationToken);

        var areaIds = requests
            .Where(request => request.AreaID.HasValue)
            .Select(request => request.AreaID!.Value)
            .Distinct()
            .ToList();
        var areas = await _context.Areas
            .AsNoTracking()
            .Where(area => areaIds.Contains(area.AreaID))
            .ToDictionaryAsync(area => area.AreaID, cancellationToken);

        foreach (var request in requests)
        {
            if (cars.TryGetValue(request.CarID, out var car))
            {
                if (brands.TryGetValue(car.BrandID, out var brand))
                    car.CarBrand = brand;
                if (car.CarModelID.HasValue && models.TryGetValue(car.CarModelID.Value, out var model))
                    car.CarModel = model;
                if (imagesByCar.TryGetValue(car.CarID, out var carImages))
                    car.CarImages = carImages;

                request.Car = car;
            }

            if (owners.TryGetValue(request.OwnerID, out var owner))
                request.Owner = owner;

            if (request.AreaID.HasValue && areas.TryGetValue(request.AreaID.Value, out var area))
                request.Area = area;
        }
    }

    private static string NormalizeQueueStatusFilter(string? statusFilter)
    {
        return string.Equals(statusFilter, CarChangeRequestStatusConstants.Approved, StringComparison.OrdinalIgnoreCase)
            ? CarChangeRequestStatusConstants.Approved
            : string.Equals(statusFilter, CarChangeRequestStatusConstants.Rejected, StringComparison.OrdinalIgnoreCase)
                ? CarChangeRequestStatusConstants.Rejected
                : CarApprovalStatusConstants.Pending;
    }

    public async Task<CarChangeApprovalResult> ApproveAsync(int requestId, int actorUserId, string role, string? note = null, CancellationToken cancellationToken = default)
    {
        var request = await LoadForDecisionAsync(requestId, cancellationToken);
        if (request == null)
            return new(false, "Không tìm thấy yêu cầu duyệt xe.");

        if (!await CanDecideAsync(request, actorUserId, role, cancellationToken))
            return new(false, "Bạn không có quyền duyệt yêu cầu này.");

        var step = request.ApprovalSteps.FirstOrDefault(item => item.Stage == request.CurrentStage);
        if (step == null)
            return new(false, "Yêu cầu duyệt thiếu cấu hình bước duyệt.");

        step.Decision = CarChangeApprovalDecisionConstants.Approved;
        step.ActorUserID = actorUserId;
        step.DecidedAt = DateTime.UtcNow;
        step.Note = Truncate(note, 500);
        request.UpdatedAt = DateTime.UtcNow;

        var applied = await ApplyApprovedRequestAsync(request, cancellationToken);
        if (!applied.Success)
            return applied;

        request.Status = CarChangeRequestStatusConstants.Approved;
        request.CompletedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
        await RecordAuditAsync("ApproveCarChangeRequest", actorUserId, request, new
        {
            request.Status,
            request.CurrentStage,
            Step = step.Stage,
            Note = step.Note
        }, cancellationToken);

        return new(true, BuildApproveMessage(request), request);
    }

    public async Task<CarChangeApprovalResult> RejectAsync(int requestId, int actorUserId, string role, string reason, CancellationToken cancellationToken = default)
    {
        reason = (reason ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(reason))
            return new(false, "Vui lòng nhập lý do từ chối.");

        var request = await LoadForDecisionAsync(requestId, cancellationToken);
        if (request == null)
            return new(false, "Không tìm thấy yêu cầu duyệt xe.");

        if (!await CanDecideAsync(request, actorUserId, role, cancellationToken))
            return new(false, "Bạn không có quyền từ chối yêu cầu này.");

        var step = request.ApprovalSteps.FirstOrDefault(item => item.Stage == request.CurrentStage);
        if (step != null)
        {
            step.Decision = CarChangeApprovalDecisionConstants.Rejected;
            step.ActorUserID = actorUserId;
            step.DecidedAt = DateTime.UtcNow;
            step.Note = Truncate(reason, 500);
        }

        request.Status = CarChangeRequestStatusConstants.Rejected;
        request.RejectionReason = Truncate(reason, 500);
        request.CompletedAt = DateTime.UtcNow;
        request.UpdatedAt = DateTime.UtcNow;

        var car = request.Car;
        if (car != null)
        {
            if (request.Operation == CarChangeOperationConstants.Create)
            {
                car.IsApproved = false;
                car.ApprovalStatus = CarApprovalStatusConstants.Rejected;
                car.ApprovalRejectionReason = request.RejectionReason;
            }
            else if (request.Operation == CarChangeOperationConstants.Delete)
            {
                car.IsPendingDeletion = false;
            }
            else if (request.Operation == CarChangeOperationConstants.Update)
            {
                car.ApprovalRejectionReason = request.RejectionReason;
            }

            car.UpdatedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync(cancellationToken);
        await RecordAuditAsync("RejectCarChangeRequest", actorUserId, request, new
        {
            request.Status,
            request.CurrentStage,
            request.RejectionReason
        }, cancellationToken);

        return new(true, "Đã từ chối yêu cầu thay đổi xe.", request);
    }

    private async Task<CarChangeRequest?> LoadForDecisionAsync(int requestId, CancellationToken cancellationToken)
    {
        return await _context.CarChangeRequests
            .Include(request => request.Car)
            .Include(request => request.ApprovalSteps)
            .FirstOrDefaultAsync(request => request.CarChangeRequestID == requestId && ActiveStatuses.Contains(request.Status), cancellationToken);
    }

    private async Task<bool> CanDecideAsync(CarChangeRequest request, int actorUserId, string role, CancellationToken cancellationToken)
    {
        if (string.Equals(role, UserRoleConstants.Admin, StringComparison.OrdinalIgnoreCase))
            return true;

        if (!request.AreaID.HasValue)
            return false;

        if (string.Equals(role, UserRoleConstants.Staff, StringComparison.OrdinalIgnoreCase))
            return await _areaService.IsUserInAreaAsync(actorUserId, request.AreaID.Value);

        if (string.Equals(role, UserRoleConstants.Manager, StringComparison.OrdinalIgnoreCase))
            return await _areaService.IsUserAreaManagerAsync(actorUserId, request.AreaID.Value);

        return false;
    }

    private async Task<CarChangeApprovalResult> ApplyApprovedRequestAsync(CarChangeRequest request, CancellationToken cancellationToken)
    {
        var car = request.Car;
        if (car == null)
            return new(false, "Không tìm thấy xe cần cập nhật.");

        if (request.Operation == CarChangeOperationConstants.Delete)
        {
            car.IsDeleted = true;
            car.IsPendingDeletion = false;
            car.DeletedAt = DateTime.UtcNow;
            car.UpdatedAt = DateTime.UtcNow;
            return new(true, "Đã xóa xe sau khi duyệt.");
        }

        if (request.Operation == CarChangeOperationConstants.Update)
        {
            var snapshot = DeserializeSnapshot(request);
            if (snapshot == null)
                return new(false, "Không đọc được bản nháp chỉnh sửa xe.");

            await ApplySnapshotAsync(car, snapshot, cancellationToken);
        }

        var availableStatusId = await _context.CarStatuses
            .Where(status => status.Name == CarStatusConstants.Available)
            .Select(status => (int?)status.CarStatusID)
            .FirstOrDefaultAsync(cancellationToken);

        if (!availableStatusId.HasValue)
            return new(false, "Không tìm thấy trạng thái Available trong hệ thống.");

        car.CarStatusID = availableStatusId.Value;
        car.IsApproved = true;
        car.ApprovalStatus = CarApprovalStatusConstants.Approved;
        car.ApprovalRejectionReason = null;
        car.IsPendingDeletion = false;
        car.UpdatedAt = DateTime.UtcNow;

        return new(true, "Đã áp dụng thay đổi xe.");
    }

    private static CarChangeSnapshot? DeserializeSnapshot(CarChangeRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.SnapshotJson))
            return null;

        return JsonSerializer.Deserialize<CarChangeSnapshot>(request.SnapshotJson, JsonOptions);
    }

    private async Task ApplySnapshotAsync(Car car, CarChangeSnapshot snapshot, CancellationToken cancellationToken)
    {
        car.LicensePlate = snapshot.LicensePlate.Trim().ToUpperInvariant();
        car.VIN = string.IsNullOrWhiteSpace(snapshot.VIN) ? car.VIN : snapshot.VIN.Trim().ToUpperInvariant();
        car.BrandID = snapshot.BrandID;
        car.CarModelID = snapshot.CarModelID;
        car.CarName = string.IsNullOrWhiteSpace(snapshot.CarName) ? car.CarName : snapshot.CarName;
        car.Seats = snapshot.Seats;
        car.Transmission = snapshot.Transmission;
        car.FuelType = snapshot.FuelType;
        car.FuelConsumption = snapshot.FuelConsumption;
        car.Description = snapshot.Description;
        car.NextInspectionDate = snapshot.NextInspectionDate?.Date;
        car.ServiceTypeID = snapshot.ServiceTypeID;
        car.PricePerDay = snapshot.PricePerDay;
        car.PricePerHour = Math.Max(1, Math.Round(snapshot.PricePerDay / 8m));
        car.WeeklyDiscountPercent = snapshot.WeeklyDiscountPercent;
        car.MonthlyDiscountPercent = snapshot.MonthlyDiscountPercent;
        car.DeliveryEnabled = snapshot.DeliveryEnabled;
        car.MaxDeliveryDistanceKm = snapshot.MaxDeliveryDistanceKm;
        car.DeliveryFeePerKm = snapshot.DeliveryFeePerKm;
        car.FreeDeliveryRadiusKm = snapshot.FreeDeliveryRadiusKm;
        car.MileageLimitEnabled = snapshot.MileageLimitEnabled;
        car.DailyMileageLimitKm = snapshot.DailyMileageLimitKm;
        car.ExtraMileageFeePerKm = snapshot.ExtraMileageFeePerKm;
        car.RentalTerms = snapshot.RentalTerms;
        car.ContractSigningMode = CarContractSigningModes.Normalize(snapshot.ContractSigningMode);
        car.LocationID = snapshot.LocationID;
        car.CarAddress = snapshot.CarAddress;
        car.Latitude = snapshot.Latitude;
        car.Longitude = snapshot.Longitude;
        car.CarTypeID = snapshot.CarTypeID;
        car.IsSelfDriveAllowed = snapshot.IsSelfDriveAllowed;
        car.IsWithDriverAllowed = snapshot.IsWithDriverAllowed;
        car.TransportPermitStatus = snapshot.TransportPermitStatus;
        car.CommissionRate = snapshot.CommissionRate;

        var amenityIds = snapshot.AmenityIDs.Distinct().ToList();
        var amenityNames = await _context.CarAmenities
            .Where(amenity => amenityIds.Contains(amenity.CarAmenityID))
            .OrderBy(amenity => amenity.DisplayOrder)
            .ThenBy(amenity => amenity.Name)
            .Select(amenity => amenity.Name)
            .ToListAsync(cancellationToken);
        car.Features = string.Join(", ", amenityNames);

        var mappings = await _context.CarAmenityMappings
            .IgnoreQueryFilters()
            .Where(mapping => mapping.CarID == car.CarID)
            .ToListAsync(cancellationToken);
        foreach (var mapping in mappings)
            mapping.IsDeleted = !amenityIds.Contains(mapping.CarAmenityID);

        foreach (var amenityId in amenityIds.Where(id => mappings.All(mapping => mapping.CarAmenityID != id)))
        {
            _context.CarAmenityMappings.Add(new CarAmenityMapping
            {
                CarID = car.CarID,
                CarAmenityID = amenityId
            });
        }

        if (snapshot.ImageUrls.Any())
        {
            var oldImages = await _context.CarImages
                .IgnoreQueryFilters()
                .Where(image => image.CarID == car.CarID)
                .ToListAsync(cancellationToken);
            foreach (var image in oldImages)
                image.IsDeleted = true;

            foreach (var url in snapshot.ImageUrls.Where(url => !string.IsNullOrWhiteSpace(url)))
            {
                _context.CarImages.Add(new CarImage
                {
                    CarID = car.CarID,
                    ImageUrl = url
                });
            }

            car.ImageURL = snapshot.ImageUrls.FirstOrDefault(url => !string.IsNullOrWhiteSpace(url));
        }
    }

    private async Task RecordAuditAsync(string action, int actorUserId, CarChangeRequest request, object values, CancellationToken cancellationToken)
    {
        await _auditTrail.RecordAsync(new AuditLog
        {
            Action = action,
            EntityType = nameof(CarChangeRequest),
            EntityId = request.CarChangeRequestID,
            UserId = actorUserId,
            Description = $"{action} #{request.CarChangeRequestID} for car #{request.CarID}.",
            NewValues = JsonSerializer.Serialize(values, JsonOptions),
            Timestamp = DateTime.UtcNow
        }, cancellationToken);
    }

    private static string BuildApproveMessage(CarChangeRequest request)
    {
        return request.Status == CarChangeRequestStatusConstants.Approved
            ? "Đã duyệt xong và áp dụng thay đổi xe."
            : "Đã duyệt bước hiện tại, yêu cầu đã chuyển sang cấp tiếp theo.";
    }

    private static string? Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        value = value.Trim();
        return value.Length <= maxLength ? value : value[..maxLength];
    }
}
