using CarRentalSystem.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace CarRentalSystem.Controllers
{
    public class FaqController : Controller
    {
        [HttpGet("/FAQ")]
        public IActionResult Index()
        {
            ViewData["Title"] = "Câu hỏi thường gặp";
            ViewData["MetaDescription"] = "Giải đáp nhanh các câu hỏi phổ biến về thuê xe tự lái, xác thực eKYC, thanh toán, nhận trả xe, hủy chuyến và hỗ trợ trên CarRental.";

            return View(new FaqIndexViewModel
            {
                Sections = FaqSections
            });
        }

        private static readonly IReadOnlyList<FaqSectionViewModel> FaqSections = new List<FaqSectionViewModel>
        {
            new()
            {
                Slug = "booking",
                Title = "Thuê xe & đặt chuyến",
                Description = "Các bước tìm xe, kiểm tra lịch trống và hoàn tất đặt chuyến.",
                Icon = "bi-calendar-check",
                Items = new List<FaqItemViewModel>
                {
                    new()
                    {
                        Question = "Tôi cần chuẩn bị gì trước khi đặt xe tự lái?",
                        Answer = "Bạn cần tài khoản CarRental, số điện thoại/email có thể nhận thông báo, CCCD và giấy phép lái xe hợp lệ để hoàn tất eKYC. Khi chọn xe, hãy kiểm tra kỹ thời gian nhận - trả, địa điểm, giá thuê và các khoản cọc hoặc phí phát sinh trước khi xác nhận.",
                        Badge = "Bắt đầu"
                    },
                    new()
                    {
                        Question = "Làm sao biết xe còn trống đúng ngày tôi muốn thuê?",
                        Answer = "Mỗi xe có lịch khả dụng riêng. Khi bạn chọn ngày nhận và ngày trả, hệ thống chỉ cho phép tiếp tục với những khung giờ xe còn có thể phục vụ. Nếu xe đã có chuyến khác, đang bảo trì hoặc không đủ điều kiện vận hành, bạn sẽ cần chọn thời gian hoặc xe khác.",
                        Badge = "Lịch xe"
                    },
                    new()
                    {
                        Question = "Tôi có thể đặt nhiều xe trong cùng một lần không?",
                        Answer = "Bạn có thể thêm nhiều xe phù hợp vào giỏ hàng nếu các xe đều còn trống và đáp ứng điều kiện thuê. Trước khi thanh toán, hệ thống sẽ hiển thị lại từng xe, thời gian thuê, giá tạm tính và các khoản cần xác nhận.",
                        Badge = "Giỏ hàng"
                    },
                    new()
                    {
                        Question = "Xe hết đăng kiểm hoặc quá hạn bảo trì có đặt được không?",
                        Answer = "Không. Hệ thống sẽ cảnh báo và chặn đặt xe nếu xe không đủ điều kiện vận hành, bao gồm quá hạn đăng kiểm hoặc đang trong trạng thái không khả dụng. Bạn nên chọn xe khác để bảo đảm an toàn và đúng quy định.",
                        Badge = "An toàn"
                    }
                }
            },
            new()
            {
                Slug = "verification",
                Title = "Xác thực tài khoản & giấy tờ",
                Description = "Quy định về eKYC, CCCD, giấy phép lái xe và bảo mật thông tin.",
                Icon = "bi-shield-check",
                Items = new List<FaqItemViewModel>
                {
                    new()
                    {
                        Question = "Vì sao tôi cần xác thực eKYC trước khi thuê xe?",
                        Answer = "eKYC giúp xác nhận đúng người thuê, giảm rủi ro tranh chấp và bảo vệ tài sản của chủ xe. Một số xe hoặc chuyến thuê có thể yêu cầu hồ sơ đã xác thực trước khi thanh toán hoặc nhận xe.",
                        Badge = "eKYC"
                    },
                    new()
                    {
                        Question = "Tôi cần tải lên những giấy tờ nào?",
                        Answer = "Thông thường bạn cần ảnh CCCD mặt trước, mặt sau, giấy phép lái xe và ảnh khuôn mặt theo hướng dẫn trên màn hình. Ảnh nên rõ nét, không bị lóa, không che mất thông tin quan trọng.",
                        Badge = "Giấy tờ"
                    },
                    new()
                    {
                        Question = "Nếu xác thực không đạt thì tôi phải làm gì?",
                        Answer = "Bạn có thể chụp lại ảnh rõ hơn, kiểm tra thông tin trên giấy tờ và gửi lại. Nếu vẫn không đạt, hãy dùng khu vực hỗ trợ để đội ngũ vận hành kiểm tra trường hợp cụ thể.",
                        Badge = "Xử lý"
                    },
                    new()
                    {
                        Question = "Thông tin giấy tờ của tôi có được bảo mật không?",
                        Answer = "CarRental chỉ sử dụng dữ liệu xác thực cho mục đích vận hành chuyến thuê, quản lý rủi ro, hỗ trợ tranh chấp và tuân thủ quy định. Bạn nên đọc thêm chính sách bảo mật để nắm rõ phạm vi xử lý dữ liệu.",
                        Badge = "Bảo mật"
                    }
                }
            },
            new()
            {
                Slug = "payment",
                Title = "Thanh toán, đặt cọc & khuyến mãi",
                Description = "Cách tính giá, giữ chỗ, đặt cọc, ưu đãi và đối soát thanh toán.",
                Icon = "bi-credit-card-2-front",
                Items = new List<FaqItemViewModel>
                {
                    new()
                    {
                        Question = "Giá thuê hiển thị đã bao gồm những gì?",
                        Answer = "Giá thuê phụ thuộc vào xe, thời gian thuê, địa điểm, ưu đãi và các dịch vụ bạn chọn thêm. Trước khi thanh toán, màn hình checkout sẽ tách rõ tiền thuê, phụ phí, khuyến mãi, đặt cọc hoặc khoản giữ chỗ nếu có.",
                        Badge = "Bảng giá"
                    },
                    new()
                    {
                        Question = "Tôi thanh toán bằng cách nào?",
                        Answer = "Hệ thống ưu tiên các kênh thanh toán online được tích hợp trong checkout. Bạn cần kiểm tra đúng số tiền, nội dung giao dịch và trạng thái đơn trong tài khoản sau khi thanh toán.",
                        Badge = "PayOS"
                    },
                    new()
                    {
                        Question = "Tiền cọc được xử lý ra sao sau khi trả xe?",
                        Answer = "Sau khi xe được trả và hoàn tất kiểm tra bàn giao, khoản cọc hoặc khoản giữ chỗ sẽ được đối soát theo tình trạng thực tế của chuyến đi. Nếu không phát sinh phí bồi thường, vi phạm hoặc công nợ, hệ thống sẽ xử lý hoàn/giải tỏa theo chính sách thanh toán.",
                        Badge = "Đặt cọc"
                    },
                    new()
                    {
                        Question = "Tôi nhập mã khuyến mãi ở đâu?",
                        Answer = "Mã khuyến mãi được áp dụng trong giỏ hàng hoặc màn hình checkout nếu mã còn hiệu lực và thỏa điều kiện. Một số ưu đãi có thể giới hạn theo hạng thành viên, khu vực, dòng xe hoặc thời gian thuê.",
                        Badge = "Ưu đãi"
                    }
                }
            },
            new()
            {
                Slug = "handover",
                Title = "Nhận xe, trả xe & giao xe",
                Description = "Những việc cần kiểm tra khi bàn giao, nhận xe tận nơi hoặc trả xe.",
                Icon = "bi-key",
                Items = new List<FaqItemViewModel>
                {
                    new()
                    {
                        Question = "Khi nhận xe tôi cần kiểm tra những gì?",
                        Answer = "Bạn nên kiểm tra ngoại thất, nội thất, nhiên liệu hoặc pin, số km, phụ kiện, giấy tờ xe và chụp ảnh bàn giao theo hướng dẫn. Các ghi nhận này là căn cứ quan trọng nếu có tranh chấp sau chuyến đi.",
                        Badge = "Bàn giao"
                    },
                    new()
                    {
                        Question = "CarRental có hỗ trợ giao xe tận nơi không?",
                        Answer = "Một số xe có hỗ trợ giao xe tùy theo khu vực và phạm vi dịch vụ. Khi chọn địa điểm nhận xe, hệ thống sẽ kiểm tra tuyến có được hỗ trợ hay không và hiển thị phí giao xe nếu có.",
                        Badge = "Giao xe"
                    },
                    new()
                    {
                        Question = "Tôi có thể trả xe muộn hơn giờ đã đặt không?",
                        Answer = "Bạn cần liên hệ hỗ trợ hoặc kiểm tra khả năng gia hạn trước khi quá giờ trả xe. Việc trả muộn khi chưa được xác nhận có thể phát sinh phụ phí và ảnh hưởng đến chuyến thuê kế tiếp của xe.",
                        Badge = "Gia hạn"
                    },
                    new()
                    {
                        Question = "Nếu phát hiện xe có vấn đề lúc nhận thì sao?",
                        Answer = "Hãy ghi nhận ngay trong bước bàn giao, chụp ảnh/video rõ ràng và liên hệ hỗ trợ trước khi khởi hành. Không nên tự ý tiếp tục chuyến đi nếu lỗi có thể ảnh hưởng đến an toàn vận hành.",
                        Badge = "Kiểm tra"
                    }
                }
            },
            new()
            {
                Slug = "policy",
                Title = "Hủy chuyến, hoàn tiền & phụ phí",
                Description = "Các tình huống đổi kế hoạch, hoàn tiền, phụ phí và trách nhiệm trong chuyến thuê.",
                Icon = "bi-receipt-cutoff",
                Items = new List<FaqItemViewModel>
                {
                    new()
                    {
                        Question = "Tôi hủy chuyến thì có được hoàn tiền không?",
                        Answer = "Khả năng hoàn tiền phụ thuộc vào thời điểm hủy, trạng thái đơn và chính sách áp dụng cho chuyến thuê. Màn hình chi tiết đặt xe sẽ hiển thị các hành động còn khả dụng; trường hợp đặc biệt có thể cần đội ngũ hỗ trợ kiểm tra thêm.",
                        Badge = "Hủy chuyến"
                    },
                    new()
                    {
                        Question = "Những phụ phí nào có thể phát sinh?",
                        Answer = "Phụ phí có thể gồm trả xe muộn, vượt giới hạn sử dụng nếu hợp đồng có quy định, vệ sinh, thiếu nhiên liệu/sạc, vi phạm giao thông, hư hỏng hoặc các khoản đã được nêu trong điều khoản thuê.",
                        Badge = "Phụ phí"
                    },
                    new()
                    {
                        Question = "Tôi có thể đổi thời gian hoặc đổi xe sau khi đặt không?",
                        Answer = "Bạn nên kiểm tra chi tiết đơn hoặc liên hệ hỗ trợ. Việc đổi thời gian/đổi xe chỉ thực hiện được khi xe mới còn trống, điều kiện thanh toán phù hợp và chưa ảnh hưởng đến lịch vận hành đã xác nhận.",
                        Badge = "Thay đổi"
                    },
                    new()
                    {
                        Question = "Nếu phát sinh phạt nguội sau chuyến đi thì xử lý thế nào?",
                        Answer = "Khi có thông tin vi phạm liên quan đến thời gian thuê, hệ thống hoặc đội ngũ vận hành sẽ đối soát bằng hợp đồng, biên bản bàn giao và dữ liệu chuyến đi để xác định trách nhiệm xử lý.",
                        Badge = "Phạt nguội"
                    }
                }
            },
            new()
            {
                Slug = "support",
                Title = "Sự cố, bảo hiểm & hỗ trợ",
                Description = "Kênh hỗ trợ khi có tai nạn, hư hỏng, tranh chấp hoặc câu hỏi ngoài FAQ.",
                Icon = "bi-headset",
                Items = new List<FaqItemViewModel>
                {
                    new()
                    {
                        Question = "Tôi cần làm gì nếu xe gặp sự cố trên đường?",
                        Answer = "Hãy dừng xe ở vị trí an toàn, bật cảnh báo nếu cần, không tự ý sửa chữa lớn khi chưa được xác nhận và liên hệ hỗ trợ ngay. Bạn nên cung cấp vị trí, ảnh/video hiện trường và mô tả ngắn tình trạng xe.",
                        Badge = "Khẩn cấp"
                    },
                    new()
                    {
                        Question = "Nếu xảy ra va chạm hoặc tai nạn thì sao?",
                        Answer = "Ưu tiên an toàn con người trước, sau đó liên hệ cơ quan chức năng khi cần và báo ngay cho CarRental. Không tự thỏa thuận hoặc rời hiện trường nếu sự việc cần biên bản, bảo hiểm hoặc xác minh trách nhiệm.",
                        Badge = "Tai nạn"
                    },
                    new()
                    {
                        Question = "Bảo hiểm có áp dụng cho mọi trường hợp không?",
                        Answer = "Không phải mọi tình huống đều được bảo hiểm chi trả. Phạm vi phụ thuộc vào gói bảo hiểm, điều kiện hợp đồng, hồ sơ sự cố và hành vi sử dụng xe. Đội ngũ hỗ trợ sẽ hướng dẫn bạn chuẩn bị thông tin cần thiết khi có yêu cầu bồi thường.",
                        Badge = "Bảo hiểm"
                    },
                    new()
                    {
                        Question = "Tôi không tìm thấy câu trả lời thì liên hệ ở đâu?",
                        Answer = "Bạn có thể dùng trợ lý AI trên web để hỏi nhanh, gọi hotline 1900 1234 hoặc gửi yêu cầu hỗ trợ từ tài khoản CarRental. Với vấn đề liên quan đến chuyến xe, hãy kèm mã đặt xe và ảnh chứng minh nếu có.",
                        Badge = "Hỗ trợ"
                    }
                }
            }
        };
    }
}
