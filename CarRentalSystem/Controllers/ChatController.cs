using Microsoft.AspNetCore.Mvc;
using CarRentalSystem.Data;
using CarRentalSystem.Services;
using System.Linq;
using System.Threading.Tasks;

namespace CarRentalSystem.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ChatController : ControllerBase
    {
        private readonly ICarService _carService;

        public ChatController(ICarService carService)
        {
            _carService = carService;
        }

        public class ChatRequest
        {
            public string Message { get; set; } = "";
        }

        [HttpPost]
        public async Task<IActionResult> Send([FromBody] ChatRequest request)
        {
            var msg = request.Message?.ToLower().Trim() ?? "";
            string reply;

            if (msg.Contains("xin chào") || msg.Contains("hello") || msg.Contains("hi") || msg == "chào")
            {
                reply = "Xin chào! 👋 Tôi là trợ lý ảo SmartCar. Tôi có thể giúp bạn:\n• Tìm xe phù hợp\n• Giải đáp về giá thuê\n• Hướng dẫn quy trình eKYC\n• Thông tin khuyến mãi\nBạn cần hỗ trợ gì ạ?";
            }
            else if (msg.Contains("giá") || msg.Contains("bao nhiêu") || msg.Contains("phí"))
            {
                var cars = (await _carService.GetAllCarsAsync()).Take(3).ToList();
                reply = "💰 Bảng giá tham khảo:\n";
                foreach (var c in cars)
                {
                    reply += $"• {c.CarBrand?.Name} {c.CarName}: {c.PricePerDay:N0}đ/ngày\n";
                }
                reply += "\n👉 Giá cụ thể phụ thuộc vào thời gian thuê. Bạn muốn xem chi tiết xe nào?";
            }
            else if (msg.Contains("ekyc") || msg.Contains("xác minh") || msg.Contains("giấy tờ") || msg.Contains("cccd"))
            {
                reply = "📋 Hướng dẫn Xác minh eKYC:\n1. Đăng nhập tài khoản\n2. Vào Hồ sơ cá nhân\n3. Tải ảnh CCCD (mặt trước + sau)\n4. Tải ảnh Bằng lái xe\n5. Đợi Admin duyệt (thường 5-15 phút)\n\n✅ Sau khi xác minh, bạn có thể đặt xe ngay!";
            }
            else if (msg.Contains("khuyến mãi") || msg.Contains("mã giảm giá") || msg.Contains("voucher") || msg.Contains("promotion"))
            {
                reply = "🎁 Chương trình khuyến mãi:\n• Mã giảm giá được áp dụng tại bước Thanh toán\n• Nhập mã vào ô \"Mã giảm giá\" để được giảm trực tiếp\n• Liên hệ Hotline để nhận mã ưu đãi đặc biệt!\n\n📞 Hotline: 1900-xxxx";
            }
            else if (msg.Contains("cọc") || msg.Contains("đặt cọc") || msg.Contains("deposit"))
            {
                reply = "🔐 Chính sách Đặt cọc:\n• Tiền cọc thế chấp = tùy từng loại xe (thường 10-15 triệu)\n• Cọc được giữ 15 ngày sau khi trả xe để tra cứu Phạt nguội\n• Hoàn trả 100% nếu không phát sinh phạt\n• Thanh toán qua VietQR hoặc VNPAY";
            }
            else if (msg.Contains("giao xe") || msg.Contains("tận nơi") || msg.Contains("delivery"))
            {
                reply = "🚗 Dịch vụ Giao xe tận nơi:\n• Phí giao xe: 50.000đ (nội thành)\n• Chọn tùy chọn 'Giao xe tận nhà' tại bước Checkout\n• Nhập địa chỉ nhận xe\n• Tài xế sẽ giao xe đến tận cửa!";
            }
            else if (msg.Contains("phụ kiện") || msg.Contains("addon") || msg.Contains("ghế trẻ em") || msg.Contains("dịch vụ") || msg.Contains("nước suối") || msg.Contains("nước ngọt") || msg.Contains("khăn ướt"))
            {
                reply = "🍿 Dịch vụ tiện ích dành cho khách:\n• Ghế trẻ em: 50.000đ/ngày\n• Combo nước suối + khăn ướt: 25.000đ/ngày\n• Gói nước ngọt (6 lon): 60.000đ/ngày\n• Nệm hơi ô tô: 80.000đ/ngày\n• Bộ dụng cụ cắm trại: 100.000đ/ngày\n\nChọn dịch vụ tại bước Checkout nhé! ✅";
            }
            else if (msg.Contains("liên hệ") || msg.Contains("hotline") || msg.Contains("hỗ trợ"))
            {
                reply = "📞 Thông tin liên hệ:\n• Hotline: 1900-xxxx (24/7)\n• Email: support@smartcar.vn\n• Zalo: 0xxx-xxx-xxx\n• Fanpage: fb.com/smartcar\n\nĐội ngũ luôn sẵn sàng hỗ trợ bạn!";
            }
            else if (msg.Contains("7 chỗ") || msg.Contains("gia đình") || msg.Contains("suv"))
            {
                var cars = (await _carService.GetAllCarsAsync()).Where(c => c.Seats >= 7).Take(3).ToList();
                if (cars.Any())
                {
                    reply = "🚙 Xe 7 chỗ phù hợp cho gia đình:\n";
                    foreach (var c in cars)
                        reply += $"• {c.CarBrand?.Name} {c.CarName} ({c.Seats} chỗ) — {c.PricePerDay:N0}đ/ngày\n";
                }
                else
                {
                    reply = "Hiện tại chưa có xe 7 chỗ trong hệ thống. Bạn có muốn xem các loại xe khác không?";
                }
            }
            else
            {
                reply = "Xin lỗi, tôi chưa hiểu rõ câu hỏi của bạn 😅\nBạn có thể hỏi tôi về:\n• 💰 Giá thuê xe\n• 📋 Quy trình eKYC\n• 🔐 Chính sách đặt cọc\n• 🎁 Mã khuyến mãi\n• 🚗 Dịch vụ giao xe\n• 🧳 Phụ kiện cho thuê";
            }

            return Ok(new { reply });
        }
    }
}
