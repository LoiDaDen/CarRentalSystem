using System.Text.Json;
using CarRentalSystem.Data;
using CarRentalSystem.Models;
using CarRentalSystem.Services;
using Microsoft.EntityFrameworkCore;

namespace CarRentalSystem.Services.Ai;

public sealed class AiToolExecutor : IAiToolExecutor
{
    private const decimal ServiceFee = 150000m;
    private readonly ApplicationDbContext _db;
    private readonly IOperationsCopilotService _operationsCopilot;
    private readonly ILogger<AiToolExecutor> _logger;

    public AiToolExecutor(
        ApplicationDbContext db,
        IOperationsCopilotService operationsCopilot,
        ILogger<AiToolExecutor> logger)
    {
        _db = db;
        _operationsCopilot = operationsCopilot;
        _logger = logger;
    }

    public async Task<AiToolResult> ExecuteAsync(AiToolCallRequest request, AiUserContext user, AiConversationState state, CancellationToken cancellationToken = default)
    {
        var status = "Success";
        string? error = null;
        AiToolResult result;

        try
        {
            if (IsOpsTool(request.Name) && !user.IsInternalStaff)
                return new AiToolResult
                {
                    Success = false,
                    RequiresLogin = !user.IsAuthenticated,
                    Message = "Operations Copilot chỉ dành cho Admin, Manager hoặc Staff."
                };

            result = request.Name switch
            {
                "searchCars" => await SearchCarsAsync(request.Arguments, state, cancellationToken),
                "getCarDetail" => await GetCarDetailAsync(request.Arguments, cancellationToken),
                "calculateRentalPrice" => await CalculateRentalPriceAsync(request.Arguments, cancellationToken),
                "createBooking" => await CreateBookingAsync(request.Arguments, user, request.IsConfirmed, cancellationToken),
                "cancelBooking" => await CancelBookingAsync(request.Arguments, user, request.IsConfirmed, cancellationToken),
                "changeBookingSchedule" => await ChangeBookingScheduleAsync(request.Arguments, user, request.IsConfirmed, cancellationToken),
                "checkUserProfile" => await CheckUserProfileAsync(user, cancellationToken),
                "suggestCars" => await SuggestCarsAsync(request.Arguments, state, cancellationToken),
                "findDriver" => await FindDriverAsync(request.Arguments, cancellationToken),
                "getBookingStatus" => await GetBookingStatusAsync(request.Arguments, user, cancellationToken),
                "updateTripStatus" => await UpdateTripStatusAsync(request.Arguments, user, request.IsConfirmed, cancellationToken),
                "createRentalContract" => await CreateRentalContractAsync(request.Arguments, user, cancellationToken),
                "answerPolicyQuestion" => AnswerPolicyQuestion(request.Arguments),
                "getOpsSnapshot" => await GetOpsSnapshotAsync(cancellationToken),
                "explainMoneyExceptions" => await ExplainMoneyExceptionsAsync(cancellationToken),
                "inspectVehicleTimeline" => await InspectVehicleTimelineAsync(request.Arguments, cancellationToken),
                "summarizeCameraRisk" => await SummarizeCameraRiskAsync(cancellationToken),
                "recommendNextActions" => await RecommendNextActionsAsync(cancellationToken),
                _ => new AiToolResult { Success = false, Message = $"Tool {request.Name} chưa được hỗ trợ." }
            };
            status = result.Success ? "Success" : "DeniedOrFailed";
            error = result.Success ? null : result.Message;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AI tool {ToolName} failed", request.Name);
            status = "Error";
            error = ex.Message;
            result = new AiToolResult { Success = false, Message = "Có lỗi hệ thống khi xử lý yêu cầu. Vui lòng thử lại sau." };
        }

        await LogToolCallAsync(request.Name, request.Arguments, result, user, status, error, cancellationToken);
        return result;
    }

    private static bool IsOpsTool(string name) =>
        name is "getOpsSnapshot" or
            "explainMoneyExceptions" or
            "inspectVehicleTimeline" or
            "summarizeCameraRisk" or
            "recommendNextActions";

    private async Task<AiToolResult> GetOpsSnapshotAsync(CancellationToken ct)
    {
        var snapshot = await _operationsCopilot.GetOpsSnapshotAsync(ct);
        var message = snapshot.TopSignals.Count == 0
            ? $"Ops snapshot: rủi ro {snapshot.RiskLevel} ({snapshot.RiskScore}/100). Không có queue nổi bật."
            : $"Ops snapshot: rủi ro {snapshot.RiskLevel} ({snapshot.RiskScore}/100). Tín hiệu chính: {string.Join("; ", snapshot.TopSignals.Take(3).Select(s => s.Message))}.";
        return new AiToolResult { Success = true, Message = message, Data = snapshot };
    }

    private async Task<AiToolResult> ExplainMoneyExceptionsAsync(CancellationToken ct)
    {
        var report = await _operationsCopilot.ExplainMoneyExceptionsAsync(ct);
        var exceptionCount = report.PayOSPaidWithoutLedger.Count + report.LedgerWithoutPayOS.Count;
        var message = exceptionCount == 0 && report.PendingRefunds.Count == 0 && report.PendingPayouts.Count == 0
            ? "Tài chính hiện không có exception PayOS/ledger/refund/payout nổi bật."
            : $"Có {exceptionCount} lệch PayOS/ledger, {report.PendingRefunds.Count} refund pending và {report.PendingPayouts.Count} payout cần theo dõi.";
        return new AiToolResult { Success = true, Message = message, Data = report };
    }

    private async Task<AiToolResult> InspectVehicleTimelineAsync(JsonElement args, CancellationToken ct)
    {
        var carId = GetInt(args, "carId");
        var plate = GetString(args, "licensePlate") ?? GetString(args, "plate");
        var timeline = await _operationsCopilot.InspectVehicleTimelineAsync(carId, plate, ct);
        var message = timeline.CarID.HasValue
            ? $"Timeline xe #{timeline.CarID} ({timeline.LicensePlate}): rủi ro {timeline.RiskLevel} ({timeline.RiskScore}/100), {timeline.Timeline.Count} sự kiện, {timeline.RiskSignals.Count} tín hiệu cần chú ý."
            : "Không tìm thấy xe phù hợp để dựng timeline.";
        return new AiToolResult { Success = timeline.CarID.HasValue, Message = message, Data = timeline };
    }

    private async Task<AiToolResult> SummarizeCameraRiskAsync(CancellationToken ct)
    {
        var summary = await _operationsCopilot.SummarizeCameraRiskAsync(ct);
        var message = $"Camera 24h: {summary.TotalEvents24h} events, {summary.AreaMismatchCount} mismatch, {summary.UnknownPlateCount} biển lạ, {summary.LowConfidenceCount} low confidence. Risk {summary.RiskScore}/100.";
        return new AiToolResult { Success = true, Message = message, Data = summary };
    }

    private async Task<AiToolResult> RecommendNextActionsAsync(CancellationToken ct)
    {
        var plan = await _operationsCopilot.RecommendNextActionsAsync(ct);
        var top = plan.Actions.FirstOrDefault();
        var message = top == null
            ? "Chưa có hành động khẩn cấp."
            : $"Ưu tiên cao nhất: {top.Priority} - {top.Action} Lý do: {top.Reason}";
        return new AiToolResult { Success = true, Message = message, Data = plan };
    }

    private async Task<AiToolResult> SearchCarsAsync(JsonElement args, AiConversationState state, CancellationToken ct)
    {
        var location = GetString(args, "location");
        var seats = GetInt(args, "seats");
        var transmission = GetString(args, "transmission");
        var fuelType = GetString(args, "fuelType");
        var rentalType = NormalizeRentalType(GetString(args, "rentalType"));
        var maxPrice = GetDecimal(args, "maxPrice");
        var start = ParseDate(GetString(args, "startDate"), DateTime.Today.AddDays(1).AddHours(8));
        var end = ParseDate(GetString(args, "endDate"), start.AddDays(1));
        if (end <= start) end = start.AddDays(1);

        var query = BaseCarQuery()
            .Where(c => c.Seats >= Math.Max(1, seats ?? 1));

        if (!string.IsNullOrWhiteSpace(location))
        {
            var keyword = location.Trim();
            var isHcm = IsHcmLocation(keyword);
            if (isHcm)
            {
                query = query.Where(c =>
                    (c.Location != null &&
                        (c.Location.Name.Contains(keyword) ||
                         c.Location.Name.Contains("HCM") ||
                         c.Location.Name.Contains("Hồ Chí Minh") ||
                         c.Location.Name.Contains("Ho Chi Minh") ||
                         c.Location.Name.Contains("Sài Gòn") ||
                         c.Location.Name.Contains("Sai Gon") ||
                         (c.Location.Address != null &&
                            (c.Location.Address.Contains(keyword) ||
                             c.Location.Address.Contains("HCM") ||
                             c.Location.Address.Contains("Hồ Chí Minh") ||
                             c.Location.Address.Contains("Ho Chi Minh") ||
                             c.Location.Address.Contains("Sài Gòn") ||
                             c.Location.Address.Contains("Sai Gon"))))) ||
                    (c.CarAddress != null &&
                        (c.CarAddress.Contains(keyword) ||
                         c.CarAddress.Contains("HCM") ||
                         c.CarAddress.Contains("Hồ Chí Minh") ||
                         c.CarAddress.Contains("Ho Chi Minh") ||
                         c.CarAddress.Contains("Sài Gòn") ||
                         c.CarAddress.Contains("Sai Gon"))));
            }
            else
            {
                query = query.Where(c =>
                    (c.Location != null &&
                        (c.Location.Name.Contains(keyword) ||
                         (c.Location.Address != null && c.Location.Address.Contains(keyword)))) ||
                    (c.CarAddress != null && c.CarAddress.Contains(keyword)));
            }
        }

        if (!string.IsNullOrWhiteSpace(transmission))
            query = query.Where(c => c.Transmission.Contains(transmission));
        if (!string.IsNullOrWhiteSpace(fuelType))
            query = query.Where(c => c.FuelType.Contains(fuelType));
        if (rentalType == "tu_lai")
            query = query.Where(c => c.IsSelfDriveAllowed);
        if (rentalType == "co_tai_xe")
            query = query.Where(c => c.IsWithDriverAllowed);
        if (maxPrice.HasValue)
            query = query.Where(c => (c.DiscountPrice ?? c.PricePerDay) <= maxPrice.Value);

        query = query.Where(c => !c.Bookings!.Any(b =>
            BookingStatusConstants.AvailabilityBlocking.Contains(b.BookingStatus!.Name) &&
            b.StartDate < end &&
            start < b.EndDate));

        var cars = await query
            .OrderBy(c => c.PricePerDay)
            .Take(8)
            .Select(c => new AiCarSummary
            {
                CarId = c.CarID,
                Name = c.CarName,
                Brand = c.CarBrand != null ? c.CarBrand.Name : null,
                Location = c.Location != null ? c.Location.Name : null,
                Seats = c.Seats,
                FuelType = c.FuelType,
                Transmission = c.Transmission,
                PricePerDay = c.DiscountPrice ?? c.PricePerDay,
                PricePerHour = c.PricePerHour,
                ImageUrl = c.ImageURL
            })
            .ToListAsync(ct);

        state.LastCars = cars;
        return new AiToolResult
        {
            Success = true,
            Message = cars.Count == 0
                ? "Chưa tìm thấy xe phù hợp. Bạn có thể thử đổi ngày, khu vực hoặc giảm bớt bộ lọc."
                : FormatCarSearchMessage(cars),
            Data = cars
        };
    }

    private async Task<AiToolResult> GetCarDetailAsync(JsonElement args, CancellationToken ct)
    {
        var carId = GetCarId(args);
        if (carId <= 0) return Missing("carId");

        var car = await BaseCarQuery()
            .Where(c => c.CarID == carId)
            .Select(c => new
            {
                c.CarID,
                c.CarName,
                Brand = c.CarBrand != null ? c.CarBrand.Name : null,
                Model = c.CarModel != null ? c.CarModel.Name : null,
                c.Seats,
                c.FuelType,
                c.Transmission,
                c.PricePerDay,
                c.PricePerHour,
                c.ImageURL,
                c.Features,
                c.RentalTerms,
                Location = c.Location != null ? c.Location.Name : null,
                c.IsSelfDriveAllowed,
                c.IsWithDriverAllowed
            })
            .FirstOrDefaultAsync(ct);

        return car == null
            ? new AiToolResult { Success = false, Message = "Không tìm thấy xe này." }
            : new AiToolResult { Success = true, Message = $"Đây là thông tin xe {car.CarName}.", Data = car };
    }

    private async Task<AiToolResult> CalculateRentalPriceAsync(JsonElement args, CancellationToken ct)
    {
        var carId = GetCarId(args);
        if (carId <= 0) return Missing("carId");

        var car = await _db.Cars.AsNoTracking().FirstOrDefaultAsync(c => c.CarID == carId, ct);
        if (car == null) return new AiToolResult { Success = false, Message = "Không tìm thấy xe." };

        var start = ParseDate(GetString(args, "startDate"), DateTime.Today.AddDays(1).AddHours(8));
        var end = ParseDate(GetString(args, "endDate"), start.AddDays(1));
        if (end <= start) return new AiToolResult { Success = false, Message = "Thời gian trả xe phải sau thời gian nhận xe." };

        var totalHours = Math.Max(1, (int)Math.Ceiling((end - start).TotalHours));
        var days = Math.Max(1, (int)Math.Ceiling(totalHours / 24m));
        var package = GetString(args, "rentalPackage")?.ToLowerInvariant();
        var driverService = GetBool(args, "driverService") || NormalizeRentalType(GetString(args, "rentalType")) == "co_tai_xe";

        var rentalAmount = package is "1h" or "4h" or "8h" or "12h" or "24h"
            ? totalHours * car.PricePerHour
            : days * (car.DiscountPrice ?? car.PricePerDay);
        var driverFee = driverService ? totalHours * 80000m : 0m;
        var estimatedTotal = rentalAmount + driverFee + ServiceFee;
        var quote = new AiPriceQuote
        {
            CarId = car.CarID,
            CarName = car.CarName,
            RentalAmount = rentalAmount,
            DriverFee = driverFee,
            ServiceFee = ServiceFee,
            Deposit = DepositPolicy.Calculate(estimatedTotal),
            EstimatedTotal = estimatedTotal,
            TotalHours = totalHours,
            RentalDays = days
        };

        return new AiToolResult { Success = true, Message = "Đây là giá thuê dự kiến. Giá cuối cùng sẽ được xác nhận ở bước thanh toán.", Data = quote };
    }

    private async Task<AiToolResult> CreateBookingAsync(JsonElement args, AiUserContext user, bool confirmed, CancellationToken ct)
    {
        if (!user.IsAuthenticated) return LoginRequired("Bạn cần đăng nhập để đặt xe.");
        if (!confirmed) return RequireConfirmation("createBooking", args, "Bạn có chắc muốn tạo đơn đặt xe này không?");

        var carId = GetCarId(args);
        var car = await _db.Cars.Include(c => c.Location).FirstOrDefaultAsync(c => c.CarID == carId, ct);
        if (car == null) return new AiToolResult { Success = false, Message = "Không tìm thấy xe." };

        var start = ParseDate(GetString(args, "startDate"), DateTime.Today.AddDays(1).AddHours(8));
        var end = ParseDate(GetString(args, "endDate"), start.AddDays(1));
        if (end <= start) return new AiToolResult { Success = false, Message = "Thời gian thuê không hợp lệ." };
        if (!await IsCarAvailableAsync(carId, start, end, ct))
            return new AiToolResult { Success = false, Message = "Xe này không còn trống trong khung giờ bạn chọn." };

        var profile = await BuildProfileAsync(user.UserId!.Value, ct);
        if (NormalizeRentalType(GetString(args, "rentalType")) == "tu_lai" && !(bool)profile.eligibleForSelfDrive)
            return new AiToolResult { Success = false, Message = "Hồ sơ của bạn chưa đủ điều kiện thuê xe tự lái. Vui lòng bổ sung CCCD/GPLX và xác minh email/số điện thoại.", Data = profile };

        var priceResult = await CalculateRentalPriceAsync(args, ct);
        var quote = (AiPriceQuote)priceResult.Data!;
        var status = await GetOrCreateBookingStatusAsync(BookingStatusConstants.Unpaid, ct);
        var booking = new Booking
        {
            UserID = user.UserId.Value,
            CarID = carId,
            StartDate = start,
            EndDate = end,
            PickupLocationID = car.LocationID,
            ReturnLocationID = car.LocationID,
            TotalHours = quote.TotalHours,
            TotalPrice = quote.EstimatedTotal,
            DepositAmount = quote.Deposit,
            ReservationFee = quote.Deposit,
            BookingStatusID = status.BookingStatusID,
            DeliveryAddress = GetString(args, "pickupLocation"),
            AdminNotes = GetString(args, "note")
        };

        _db.Bookings.Add(booking);
        await _db.SaveChangesAsync(ct);

        return new AiToolResult
        {
            Success = true,
            Message = $"Đã tạo đơn #{booking.BookingID}. Bạn vui lòng thanh toán/đặt cọc để giữ xe.",
            Data = new { bookingId = booking.BookingID, status = status.Name, total = booking.TotalPrice, deposit = booking.ReservationFee }
        };
    }

    private async Task<AiToolResult> CancelBookingAsync(JsonElement args, AiUserContext user, bool confirmed, CancellationToken ct)
    {
        if (!user.IsAuthenticated) return LoginRequired("Bạn cần đăng nhập để hủy đơn.");
        if (!confirmed) return RequireConfirmation("cancelBooking", args, "Bạn có chắc muốn hủy đơn này không? Nếu đã thanh toán/cọc, chính sách hoàn tiền sẽ được xử lý theo trạng thái đơn.");

        var bookingId = GetInt(args, "bookingId") ?? 0;
        var booking = await _db.Bookings.Include(b => b.BookingStatus).Include(b => b.Payment).FirstOrDefaultAsync(b => b.BookingID == bookingId, ct);
        if (booking == null) return new AiToolResult { Success = false, Message = "Không tìm thấy đơn đặt xe." };
        if (!CanAccessBooking(user, booking)) return Forbidden();
        if (booking.BookingStatus?.Name == BookingStatusConstants.Completed)
            return new AiToolResult { Success = false, Message = "Đơn đã hoàn thành nên không thể hủy." };

        var status = await GetOrCreateBookingStatusAsync(BookingStatusConstants.Cancelled, ct);
        booking.BookingStatusID = status.BookingStatusID;
        booking.CancelReason = GetString(args, "reason") ?? "Khách hàng yêu cầu hủy qua AI Assistant.";
        booking.DepositStatus = booking.Payment?.PaymentStatus == PaymentStatusConstants.Paid
            ? DepositStatusConstants.PendingRefund
            : DepositStatusConstants.Unpaid;
        await _db.SaveChangesAsync(ct);

        return new AiToolResult { Success = true, Message = $"Đã hủy đơn #{booking.BookingID}. Trạng thái cọc: {booking.DepositStatus}.", Data = new { booking.BookingID, status = status.Name, booking.DepositStatus } };
    }

    private async Task<AiToolResult> ChangeBookingScheduleAsync(JsonElement args, AiUserContext user, bool confirmed, CancellationToken ct)
    {
        if (!user.IsAuthenticated) return LoginRequired("Bạn cần đăng nhập để đổi lịch.");
        if (!confirmed) return RequireConfirmation("changeBookingSchedule", args, "Bạn có chắc muốn đổi lịch thuê xe không? Hệ thống sẽ kiểm tra xe còn trống và chênh lệch giá.");

        var bookingId = GetInt(args, "bookingId") ?? 0;
        var booking = await _db.Bookings.Include(b => b.Car).FirstOrDefaultAsync(b => b.BookingID == bookingId, ct);
        if (booking == null) return new AiToolResult { Success = false, Message = "Không tìm thấy đơn đặt xe." };
        if (!CanAccessBooking(user, booking)) return Forbidden();

        var newStart = ParseDate(GetString(args, "newStartDate"), booking.StartDate);
        var newEnd = ParseDate(GetString(args, "newEndDate"), booking.EndDate);
        if (newEnd <= newStart) return new AiToolResult { Success = false, Message = "Lịch mới không hợp lệ." };
        if (!await IsCarAvailableAsync(booking.CarID, newStart, newEnd, ct, booking.BookingID))
            return new AiToolResult { Success = false, Message = "Xe không còn trống trong lịch mới." };

        var oldTotal = booking.TotalPrice;
        booking.StartDate = newStart;
        booking.EndDate = newEnd;
        var quote = (AiPriceQuote)(await CalculateRentalPriceAsync(ToArgs(new { carId = booking.CarID, startDate = newStart, endDate = newEnd }), ct)).Data!;
        booking.TotalHours = quote.TotalHours;
        booking.TotalPrice = quote.EstimatedTotal;
        await _db.SaveChangesAsync(ct);

        return new AiToolResult { Success = true, Message = $"Đã đổi lịch đơn #{booking.BookingID}. Chênh lệch dự kiến: {booking.TotalPrice - oldTotal:n0}đ.", Data = new { booking.BookingID, booking.StartDate, booking.EndDate, oldTotal, newTotal = booking.TotalPrice, difference = booking.TotalPrice - oldTotal } };
    }

    private async Task<AiToolResult> CheckUserProfileAsync(AiUserContext user, CancellationToken ct)
    {
        if (!user.IsAuthenticated) return LoginRequired("Bạn cần đăng nhập để kiểm tra hồ sơ.");
        var profile = await BuildProfileAsync(user.UserId!.Value, ct);
        return new AiToolResult { Success = true, Message = (bool)profile.eligibleForSelfDrive ? "Hồ sơ của bạn đủ điều kiện thuê xe tự lái." : "Hồ sơ của bạn còn thiếu hoặc chưa được xác minh đầy đủ.", Data = profile };
    }

    private async Task<AiToolResult> SuggestCarsAsync(JsonElement args, AiConversationState state, CancellationToken ct)
    {
        var passengers = GetInt(args, "passengers") ?? 4;
        var budget = GetDecimal(args, "budget");
        var location = GetString(args, "location");
        var purpose = GetString(args, "purpose") ?? "nhu cầu thuê xe";
        var searchArgs = ToArgs(new { location, seats = passengers, maxPrice = budget, rentalType = GetString(args, "rentalType") });
        var search = await SearchCarsAsync(searchArgs, state, ct);
        var cars = state.LastCars.Select(c => new { car = c, reason = BuildSuggestionReason(c, purpose, passengers) }).ToList();
        return new AiToolResult { Success = true, Message = cars.Count == 0 ? "Chưa có xe gợi ý phù hợp." : $"Mình gợi ý {cars.Count} xe phù hợp với {purpose}.", Data = cars };
    }

    private async Task<AiToolResult> FindDriverAsync(JsonElement args, CancellationToken ct)
    {
        var drivers = await _db.Drivers
            .AsNoTracking()
            .Include(d => d.Location)
            .Where(d => d.IsApproved && d.IsAvailable && !d.IsLocked && !d.IsBlacklisted)
            .OrderByDescending(d => d.Rating)
            .ThenByDescending(d => d.ExperienceYears)
            .Take(5)
            .Select(d => new
            {
                d.DriverID,
                d.FullName,
                d.Phone,
                d.Rating,
                d.ExperienceYears,
                d.HourlyRate,
                d.DailyRate,
                location = d.Location != null ? d.Location.Name : null,
                available = d.IsAvailable
            })
            .ToListAsync(ct);

        return new AiToolResult { Success = true, Message = drivers.Count == 0 ? "Chưa tìm thấy tài xế khả dụng." : $"Tìm thấy {drivers.Count} tài xế phù hợp.", Data = drivers };
    }

    private async Task<AiToolResult> GetBookingStatusAsync(JsonElement args, AiUserContext user, CancellationToken ct)
    {
        if (!user.IsAuthenticated) return LoginRequired("Bạn cần đăng nhập để xem trạng thái đơn.");
        var bookingId = GetInt(args, "bookingId") ?? 0;
        var booking = await _db.Bookings
            .AsNoTracking()
            .Include(b => b.BookingStatus)
            .Include(b => b.Payment)
            .Include(b => b.Car)
            .Include(b => b.Driver)
            .FirstOrDefaultAsync(b => b.BookingID == bookingId, ct);
        if (booking == null) return new AiToolResult { Success = false, Message = "Không tìm thấy đơn đặt xe." };
        if (!CanAccessBooking(user, booking)) return Forbidden();

        return new AiToolResult
        {
            Success = true,
            Message = $"Đơn #{booking.BookingID} đang ở trạng thái {booking.BookingStatus?.Name ?? "chưa rõ"}.",
            Data = new
            {
                booking.BookingID,
                bookingStatus = booking.BookingStatus?.Name,
                paymentStatus = booking.Payment?.PaymentStatus ?? "Chưa thanh toán",
                carStatus = booking.Car?.CarStatus?.Name,
                driverStatus = booking.Driver == null ? null : booking.Driver.Status,
                booking.DepositStatus
            }
        };
    }

    private async Task<AiToolResult> UpdateTripStatusAsync(JsonElement args, AiUserContext user, bool confirmed, CancellationToken ct)
    {
        if (!user.IsAuthenticated) return LoginRequired("Bạn cần đăng nhập để cập nhật chuyến xe.");
        if (!confirmed) return RequireConfirmation("updateTripStatus", args, "Bạn có chắc muốn cập nhật trạng thái chuyến xe không?");
        var bookingId = GetInt(args, "tripId") ?? 0;
        var booking = await _db.Bookings.Include(b => b.BookingStatus).Include(b => b.Driver).Include(b => b.Car).FirstOrDefaultAsync(b => b.BookingID == bookingId, ct);
        if (booking == null) return new AiToolResult { Success = false, Message = "Không tìm thấy chuyến/đơn." };
        if (!CanManageTrip(user, booking)) return Forbidden();

        var target = MapTripStatus(GetString(args, "status"));
        if (target == null) return new AiToolResult { Success = false, Message = "Trạng thái chuyến xe không hợp lệ." };
        booking.BookingStatusID = (await GetOrCreateBookingStatusAsync(target, ct)).BookingStatusID;
        await _db.SaveChangesAsync(ct);
        return new AiToolResult { Success = true, Message = $"Đã cập nhật trạng thái chuyến #{booking.BookingID} thành {target}.", Data = new { booking.BookingID, status = target } };
    }

    private async Task<AiToolResult> CreateRentalContractAsync(JsonElement args, AiUserContext user, CancellationToken ct)
    {
        if (!user.IsAuthenticated) return LoginRequired("Bạn cần đăng nhập để tạo/xem hợp đồng.");
        var bookingId = GetInt(args, "bookingId") ?? 0;
        var booking = await _db.Bookings.Include(b => b.RentalContract).FirstOrDefaultAsync(b => b.BookingID == bookingId, ct);
        if (booking == null) return new AiToolResult { Success = false, Message = "Không tìm thấy đơn đặt xe." };
        if (!CanAccessBooking(user, booking)) return Forbidden();

        if (booking.RentalContract == null)
        {
            booking.RentalContract = new RentalContract
            {
                BookingID = booking.BookingID,
                BasePrice = booking.TotalPrice,
                FinalPrice = booking.TotalPrice
            };
            await _db.SaveChangesAsync(ct);
        }

        var link = $"/Contract/Details/{booking.RentalContract.ContractID}";
        return new AiToolResult { Success = true, Message = "Đã sẵn sàng hợp đồng thuê xe.", Data = new { contractId = booking.RentalContract.ContractID, link, bookingId } };
    }

    private static AiToolResult AnswerPolicyQuestion(JsonElement args)
    {
        var question = GetString(args, "question")?.ToLowerInvariant() ?? "";
        var answer = question.Contains("hủy") || question.Contains("huy")
            ? "Bạn có thể hủy đơn trước khi nhận xe. Nếu đã thanh toán/cọc, khoản hoàn sẽ phụ thuộc trạng thái đơn và chính sách tại thời điểm hủy."
            : question.Contains("cọc") || question.Contains("coc")
                ? "Tiền cọc dùng để giữ xe và bảo đảm trách nhiệm trong thời gian thuê. Hệ thống sẽ hiển thị số tiền cọc trước khi bạn xác nhận đặt xe."
                : question.Contains("giấy") || question.Contains("cccd") || question.Contains("gplx")
                    ? "Thuê xe tự lái thường cần CCCD, GPLX hợp lệ, số điện thoại và email đã xác minh. Một số xe có thể yêu cầu thêm điều kiện riêng."
                    : "Mình có thể hỗ trợ tìm xe, tính giá, kiểm tra hồ sơ và chuẩn bị đặt xe. Bạn cho mình biết khu vực, ngày nhận/trả, số chỗ và ngân sách để mình thao tác tiếp nhé.";
        return new AiToolResult { Success = true, Message = answer, Data = new { answer } };
    }

    private static string FormatCarSearchMessage(IReadOnlyList<AiCarSummary> cars)
    {
        var topCars = cars.Take(5).Select((car, index) =>
            $"{index + 1}. {car.Name} - {car.Seats} chỗ, {car.Location ?? "chưa rõ khu vực"}, {car.PricePerDay:n0}đ/ngày");
        var suffix = cars.Count > 5
            ? $"\n\nCòn {cars.Count - 5} xe khác. Bạn có thể bấm Xem chi tiết, Tính giá hoặc Đặt xe trên từng xe."
            : "\n\nBạn có thể bấm Xem chi tiết, Tính giá hoặc Đặt xe trên từng xe.";
        return $"Mình tìm thấy {cars.Count} xe phù hợp:\n{string.Join("\n", topCars)}{suffix}";
    }

    private static bool IsHcmLocation(string keyword)
    {
        var normalized = keyword.Trim().ToLowerInvariant();
        return normalized.Contains("hcm") ||
               normalized.Contains("tp.hcm") ||
               normalized.Contains("tphcm") ||
               normalized.Contains("hồ chí minh") ||
               normalized.Contains("ho chi minh") ||
               normalized.Contains("sài gòn") ||
               normalized.Contains("sai gon");
    }

    private IQueryable<Car> BaseCarQuery() => _db.Cars
        .AsNoTracking()
        .Include(c => c.CarBrand)
        .Include(c => c.CarModel)
        .Include(c => c.Location)
        .Include(c => c.Bookings)!.ThenInclude(b => b.BookingStatus)
        .Where(c => c.IsApproved && c.CarStatus != null && c.CarStatus.Name == CarStatusConstants.Available);

    private async Task<bool> IsCarAvailableAsync(int carId, DateTime start, DateTime end, CancellationToken ct, int? excludeBookingId = null) =>
        !await _db.Bookings.AnyAsync(b =>
            b.CarID == carId &&
            (!excludeBookingId.HasValue || b.BookingID != excludeBookingId.Value) &&
            BookingStatusConstants.AvailabilityBlocking.Contains(b.BookingStatus!.Name) &&
            b.StartDate < end &&
            start < b.EndDate, ct);

    private async Task<dynamic> BuildProfileAsync(int userId, CancellationToken ct)
    {
        var user = await _db.Users.AsNoTracking().FirstAsync(u => u.UserID == userId, ct);
        var hasCccd = !string.IsNullOrWhiteSpace(user.IdCardFrontUrl) && !string.IsNullOrWhiteSpace(user.IdCardBackUrl);
        var hasGplx = !string.IsNullOrWhiteSpace(user.DrivingLicenseUrl) || !string.IsNullOrWhiteSpace(user.DrivingLicenseNumber);
        var phoneVerified = !string.IsNullOrWhiteSpace(user.Phone);
        var emailVerified = user.IsEmailVerified;
        var eligible = hasCccd && hasGplx && phoneVerified && emailVerified && user.VerificationStatus == "Verified" && !user.IsLocked && !user.IsBlacklisted;
        return new { hasCccd, hasGplx, phoneVerified, emailVerified, verificationStatus = user.VerificationStatus, eligibleForSelfDrive = eligible };
    }

    private async Task<BookingStatus> GetOrCreateBookingStatusAsync(string name, CancellationToken ct)
    {
        var status = await _db.BookingStatuses.FirstOrDefaultAsync(s => s.Name == name, ct);
        if (status != null) return status;
        status = new BookingStatus { Name = name };
        _db.BookingStatuses.Add(status);
        await _db.SaveChangesAsync(ct);
        return status;
    }

    private static bool CanAccessBooking(AiUserContext user, Booking booking) =>
        user.Role is "Admin" or "Manager" or "Staff" ||
        booking.UserID == user.UserId ||
        booking.Car?.OwnerID == user.UserId ||
        booking.Driver?.UserID == user.UserId;

    private static bool CanManageTrip(AiUserContext user, Booking booking) =>
        user.Role is "Admin" or "Manager" or "Staff" ||
        booking.Car?.OwnerID == user.UserId ||
        booking.Driver?.UserID == user.UserId;

    private static string? MapTripStatus(string? status) => status?.ToLowerInvariant() switch
    {
        "da_xac_nhan" => BookingStatusConstants.Confirmed,
        "dang_giao_xe" => BookingStatusConstants.Confirmed,
        "dang_thue" => BookingStatusConstants.Active,
        "da_tra_xe" => BookingStatusConstants.Completed,
        "hoan_thanh" => BookingStatusConstants.Completed,
        "da_huy" => BookingStatusConstants.Cancelled,
        _ => null
    };

    private static AiToolResult LoginRequired(string message) => new() { Success = false, Message = message, RequiresLogin = true };
    private static AiToolResult Forbidden() => new() { Success = false, Message = "Bạn không có quyền thực hiện thao tác này." };
    private static AiToolResult Missing(string field) => new() { Success = false, Message = $"Thiếu thông tin bắt buộc: {field}." };

    private static AiToolResult RequireConfirmation(string action, JsonElement args, string summary)
    {
        var id = $"{action}_{Guid.NewGuid():N}";
        return new AiToolResult
        {
            Success = false,
            Message = summary,
            PendingAction = new AiPendingAction { ConfirmationId = id, Action = action, Summary = summary, Input = JsonSerializer.Deserialize<object>(args.GetRawText(), AiJson.Options) }
        };
    }

    private static int GetCarId(JsonElement args) => GetInt(args, "carId") ?? GetInt(args, "id") ?? 0;

    private static string? GetString(JsonElement args, string name)
    {
        if (!args.TryGetProperty(name, out var value)) return null;
        return value.ValueKind == JsonValueKind.String ? value.GetString() : value.ToString();
    }

    private static int? GetInt(JsonElement args, string name)
    {
        if (!args.TryGetProperty(name, out var value)) return null;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var i)) return i;
        return int.TryParse(value.ToString(), out var parsed) ? parsed : null;
    }

    private static decimal? GetDecimal(JsonElement args, string name)
    {
        if (!args.TryGetProperty(name, out var value)) return null;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var d)) return d;
        return decimal.TryParse(value.ToString(), out var parsed) ? parsed : null;
    }

    private static bool GetBool(JsonElement args, string name) =>
        args.TryGetProperty(name, out var value) &&
        (value.ValueKind == JsonValueKind.True || (value.ValueKind == JsonValueKind.String && bool.TryParse(value.GetString(), out var b) && b));

    private static DateTime ParseDate(string? raw, DateTime fallback)
    {
        if (string.IsNullOrWhiteSpace(raw)) return fallback;
        var lower = raw.Trim().ToLowerInvariant();
        if (lower.Contains("ngày mai") || lower.Contains("ngay mai") || lower == "tomorrow")
            return DateTime.Today.AddDays(1).AddHours(fallback.Hour == 0 ? 8 : fallback.Hour);
        if (DateTime.TryParse(raw, out var parsed)) return parsed;
        return fallback;
    }

    private static string NormalizeRentalType(string? value)
    {
        var normalized = value?.Trim().ToLowerInvariant() ?? "";
        if (normalized.Contains("tài") || normalized.Contains("tai") || normalized.Contains("driver")) return "co_tai_xe";
        return "tu_lai";
    }

    private static JsonElement ToArgs(object value) =>
        JsonSerializer.Deserialize<JsonElement>(JsonSerializer.Serialize(value, AiJson.Options));

    private static string BuildSuggestionReason(AiCarSummary car, string purpose, int passengers)
    {
        if (car.Seats >= passengers + 2) return $"Rộng rãi cho {passengers} người, phù hợp {purpose}.";
        if (car.FuelType.Contains("điện", StringComparison.OrdinalIgnoreCase)) return $"Tiết kiệm nhiên liệu, phù hợp {purpose}.";
        return $"Đáp ứng số chỗ và ngân sách cho {purpose}.";
    }

    private async Task LogToolCallAsync(string action, JsonElement input, AiToolResult output, AiUserContext user, string status, string? error, CancellationToken ct)
    {
        _db.AuditLogs.Add(new AuditLog
        {
            Action = "AiToolCall",
            EntityType = "AI",
            UserId = user.UserId,
            UserName = user.UserName ?? user.Role,
            Description = $"{action} => {status}",
            OldValues = input.GetRawText(),
            NewValues = JsonSerializer.Serialize(new { output.Success, output.Message, output.Data }, AiJson.Options),
            Reason = error,
            CorrelationId = Guid.NewGuid().ToString("N"),
            Timestamp = DateTime.UtcNow
        });
        await _db.SaveChangesAsync(ct);
    }
}
