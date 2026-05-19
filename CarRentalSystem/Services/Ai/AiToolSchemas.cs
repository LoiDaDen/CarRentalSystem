using System.Text.Json;

namespace CarRentalSystem.Services.Ai;

public static class AiToolSchemas
{
    public static readonly HashSet<string> ConfirmationRequiredActions = new(StringComparer.OrdinalIgnoreCase)
    {
        "createBooking",
        "cancelBooking",
        "changeBookingSchedule",
        "updateTripStatus"
    };

    public static IReadOnlyList<object> GetOpenAiTools() =>
    [
        Tool("searchCars", "Tìm xe còn trống theo địa điểm, thời gian, số chỗ và nhu cầu thuê.", new
        {
            type = "object",
            properties = new
            {
                location = String("Địa điểm, tỉnh/thành hoặc khu vực nhận xe"),
                startDate = String("Ngày giờ bắt đầu thuê, ISO-8601 nếu có thể"),
                endDate = String("Ngày giờ kết thúc thuê, ISO-8601 nếu có thể"),
                seats = Number("Số chỗ tối thiểu"),
                transmission = String("số tự động hoặc số sàn"),
                fuelType = String("xăng, dầu hoặc điện"),
                rentalType = String("tu_lai hoặc co_tai_xe"),
                maxPrice = Number("Giá tối đa")
            }
        }),
        Tool("getCarDetail", "Xem chi tiết xe.", new
        {
            type = "object",
            properties = new { carId = String("ID xe") },
            required = new[] { "carId" }
        }),
        Tool("calculateRentalPrice", "Tính tổng tiền thuê xe dự kiến.", new
        {
            type = "object",
            properties = new
            {
                carId = String("ID xe"),
                startDate = String("Ngày giờ bắt đầu"),
                endDate = String("Ngày giờ kết thúc"),
                rentalPackage = String("1h, 4h, 8h, 12h, 24h, daily, weekly, monthly"),
                rentalType = String("tu_lai hoặc co_tai_xe"),
                distanceKm = Number("Quãng đường dự kiến"),
                driverService = Boolean("Có thuê tài xế không")
            },
            required = new[] { "carId", "startDate", "endDate" }
        }),
        Tool("createBooking", "Tạo đơn đặt xe. Bắt buộc xác nhận trước.", new
        {
            type = "object",
            properties = new
            {
                userId = String("ID người dùng, backend sẽ kiểm tra lại theo user đăng nhập"),
                carId = String("ID xe"),
                startDate = String("Ngày giờ bắt đầu"),
                endDate = String("Ngày giờ kết thúc"),
                pickupLocation = String("Nơi nhận xe"),
                returnLocation = String("Nơi trả xe"),
                rentalType = String("tu_lai hoặc co_tai_xe"),
                note = String("Ghi chú")
            },
            required = new[] { "carId", "startDate", "endDate", "pickupLocation", "returnLocation" }
        }),
        Tool("cancelBooking", "Hủy đơn đặt xe. Bắt buộc xác nhận trước.", new
        {
            type = "object",
            properties = new { bookingId = String("ID đơn đặt xe"), reason = String("Lý do hủy") },
            required = new[] { "bookingId" }
        }),
        Tool("changeBookingSchedule", "Đổi lịch thuê xe. Bắt buộc xác nhận trước.", new
        {
            type = "object",
            properties = new
            {
                bookingId = String("ID đơn đặt xe"),
                newStartDate = String("Ngày giờ bắt đầu mới"),
                newEndDate = String("Ngày giờ kết thúc mới")
            },
            required = new[] { "bookingId", "newStartDate", "newEndDate" }
        }),
        Tool("checkUserProfile", "Kiểm tra hồ sơ thuê xe của người dùng hiện tại.", new
        {
            type = "object",
            properties = new { userId = String("ID người dùng, backend sẽ kiểm tra lại theo user đăng nhập") }
        }),
        Tool("suggestCars", "Gợi ý xe theo mục đích, số khách và ngân sách.", new
        {
            type = "object",
            properties = new
            {
                purpose = String("đi gia đình, công tác, du lịch, đám cưới..."),
                passengers = Number("Số hành khách"),
                budget = Number("Ngân sách"),
                location = String("Địa điểm"),
                rentalType = String("tu_lai hoặc co_tai_xe")
            }
        }),
        Tool("findDriver", "Tìm tài xế cho chuyến xe có tài.", new
        {
            type = "object",
            properties = new
            {
                pickupLocation = String("Điểm đón"),
                destination = String("Điểm đến"),
                startDate = String("Thời gian khởi hành"),
                tripType = String("noi_thanh, lien_tinh, lien_tinh_mot_chieu")
            }
        }),
        Tool("getBookingStatus", "Kiểm tra trạng thái đơn hoặc chuyến.", new
        {
            type = "object",
            properties = new { bookingId = String("ID đơn đặt xe") },
            required = new[] { "bookingId" }
        }),
        Tool("updateTripStatus", "Cập nhật trạng thái chuyến xe. Bắt buộc có quyền và xác nhận.", new
        {
            type = "object",
            properties = new { tripId = String("ID booking hoặc trip"), status = String("da_xac_nhan, dang_giao_xe, dang_thue, da_tra_xe, hoan_thanh, da_huy") },
            required = new[] { "tripId", "status" }
        }),
        Tool("createRentalContract", "Tạo hoặc lấy link hợp đồng thuê xe.", new
        {
            type = "object",
            properties = new { bookingId = String("ID đơn đặt xe") },
            required = new[] { "bookingId" }
        }),
        Tool("answerPolicyQuestion", "Trả lời chính sách thuê xe.", new
        {
            type = "object",
            properties = new { question = String("Câu hỏi chính sách") },
            required = new[] { "question" }
        })
    ];

    private static object Tool(string name, string description, object parameters) => new
    {
        type = "function",
        function = new { name, description, parameters }
    };

    private static object String(string description) => new { type = "string", description };
    private static object Number(string description) => new { type = "number", description };
    private static object Boolean(string description) => new { type = "boolean", description };

    public static string ToJson() => JsonSerializer.Serialize(GetOpenAiTools(), AiJson.Options);
}
