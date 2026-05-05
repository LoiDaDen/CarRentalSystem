using System;
using System.Collections.Generic;
using System.IO;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

class Program
{
    static void Main()
    {
        var filePath = Path.Combine(Environment.CurrentDirectory, "..", "CarRentalSystem_ReleaseBacklog_TiengViet.xlsx");

        using (var spreadsheet = SpreadsheetDocument.Create(filePath, SpreadsheetDocumentType.Workbook))
        {
            var workbookPart = spreadsheet.AddWorkbookPart();
            workbookPart.Workbook = new Workbook();
            workbookPart.Workbook.Sheets = new Sheets();

            uint sheetId = 1;
            var sheetPart = workbookPart.AddNewPart<WorksheetPart>();
            var sheetData = new SheetData();
            sheetPart.Worksheet = new Worksheet(sheetData);

            var sheets = workbookPart.Workbook.Sheets;

            var sheet = new Sheet
            {
                Id = workbookPart.GetIdOfPart(sheetPart),
                SheetId = sheetId,
                Name = "Product Backlog"
            };
            sheets.Append(sheet);

            // Styles
            var stylesPart = workbookPart.AddNewPart<WorkbookStylesPart>();
            var stylesheet = CreateStylesheet();
            stylesPart.Stylesheet = stylesheet;

            // Column widths
            var cols = new Columns(
                new Column { Min = 1, Max = 1, Width = 8000, CustomWidth = true },  // Description
                new Column { Min = 2, Max = 2, Width = 2500, CustomWidth = true },  // User Story
                new Column { Min = 3, Max = 3, Width = 4000, CustomWidth = true },  // Acceptance Criteria
                new Column { Min = 4, Max = 4, Width = 3000, CustomWidth = true },  // Story Points
                new Column { Min = 5, Max = 5, Width = 3000, CustomWidth = true },  // Status
                new Column { Min = 6, Max = 6, Width = 4000, CustomWidth = true },  // Area
                new Column { Min = 7, Max = 7, Width = 2500, CustomWidth = true },  // Sprint
                new Column { Min = 8, Max = 8, Width = 3000, CustomWidth = true }   // Priority
            );
            sheetData.Append(cols);

            // Headers
            var headerRow = new Row { RowIndex = 1 };
            var headers = new[] { "Title / Description", "User Story", "Acceptance Criteria", "Story Points", "Status", "Area", "Sprint", "Priority" };
            for (int i = 0; i < headers.Length; i++)
            {
                var cell = new Cell
                {
                    CellReference = GetColumnLetter(i) + "1",
                    CellValue = new CellValue(headers[i]),
                    DataType = CellValues.String,
                    StyleIndex = 1U
                };
                headerRow.Append(cell);
            }
            sheetData.Append(headerRow);

            // Data rows - mapping from backlog
            var data = GetBacklogData();
            uint rowIdx = 2;
            foreach (var row in data)
            {
                var dataRow = new Row { RowIndex = rowIdx };
                for (int i = 0; i < row.Length; i++)
                {
                    var cell = new Cell
                    {
                        CellReference = GetColumnLetter(i) + rowIdx,
                        CellValue = new CellValue(row[i]),
                        DataType = CellValues.String,
                        StyleIndex = 0U
                    };
                    dataRow.Append(cell);
                }
                sheetData.Append(dataRow);
                rowIdx++;
            }

            sheetPart.Worksheet.Save();
            workbookPart.Workbook.Save();
        }

        Console.WriteLine($"Excel file created: {filePath}");
    }

    static string GetColumnLetter(int index)
    {
        return ((char)('A' + index)).ToString();
    }

    static Stylesheet CreateStylesheet()
    {
        var stylesheet = new Stylesheet();

        var fonts = new Fonts { Count = 2 };
        fonts.Append(new Font(
            new FontSize { Val = 11 },
            new Color { Rgb = new HexBinaryValue("000000") },
            new FontName { Val = "Calibri" }
        ));
        fonts.Append(new Font(
            new FontSize { Val = 11 },
            new Bold(),
            new Color { Rgb = new HexBinaryValue("FFFFFF") },
            new FontName { Val = "Calibri" }
        ));
        stylesheet.Fonts = fonts;

        var fills = new Fills { Count = 2 };
        fills.Append(new Fill(new PatternFill { PatternType = PatternValues.None }));
        fills.Append(new Fill(new PatternFill { PatternType = PatternValues.Gray125 }));
        stylesheet.Fills = fills;

        var borders = new Borders { Count = 1 };
        borders.Append(new Border());
        stylesheet.Borders = borders;

        var cellFormats = new CellFormats { Count = 2 };
        cellFormats.Append(new CellFormat
        {
            NumberFormatId = 0,
            FontId = 0,
            FillId = 0,
            BorderId = 0,
            ApplyFont = true
        });
        cellFormats.Append(new CellFormat
        {
            NumberFormatId = 0,
            FontId = 1,
            FillId = 0,
            BorderId = 0,
            ApplyFont = true
        });
        stylesheet.CellFormats = cellFormats;

        return stylesheet;
    }

    static List<string[]> GetBacklogData()
    {
        var data = new List<string[]>();

        // Header rows for each area
        data.Add(new[] {
            "=== PHAN HE XAC THUC (AUTHENTICATION) ===",
            "", "", "", "", "", "", ""
        });

        data.Add(new[] {
            "US-AUTH-01: Dang ky tai khoan nguoi dung",
            "Làm người dùng mới, tôi muốn đăng ký tài khoản để sử dụng dịch vụ thuê xe.",
            "- Form đăng ký: Họ tên, Email, SĐT, Mật khẩu\n- Validate email, SĐT VN\n- Hash mật khẩu BCrypt\n- Gửi email xác thực",
            "3", "Todo", "Authentication", "S1", "1 - Cao"
        });

        data.Add(new[] {
            "US-AUTH-02: Dang nhap / Dang xuat",
            "Làm người dùng, tôi muốn đăng nhập để truy cập tài khoản cá nhân.",
            "- Form: Email, Mật khẩu, Ghi nhớ\n- Kiểm tra IsLocked, IsBlacklisted\n- Session timeout 8h\n- Redirect theo Role",
            "3", "Todo", "Authentication", "S1", "1 - Cao"
        });

        data.Add(new[] {
            "US-AUTH-03: Quan ly ho so ca nhan",
            "Làm khách hàng, tôi muốn quản lý hồ sơ để cập nhật thông tin liên lạc.",
            "- Hiển thị: Họ tên, Email, SĐT, Địa chỉ\n- Upload ảnh đại diện\n- Đổi mật khẩu\n- Hiển thị trạng thái eKYC",
            "5", "Todo", "Authentication", "S1", "1 - Cao"
        });

        data.Add(new[] {
            "US-AUTH-04: Khoa tai khoan & Danh sach den",
            "Làm quản trị viên, tôi muốn khóa tài khoản vi phạm để bảo vệ hệ thống.",
            "- Admin khóa với lý do\n- User bị khóa thấy thông báo\n- Mở khóa tài khoản\n- Ghi log hành động",
            "5", "Todo", "Authentication", "S10", "1 - Cao"
        });

        data.Add(new[] {
            "US-AUTH-05: Quen mat khau",
            "Làm người dùng, tôi muốn khôi phục mật khẩu khi quên.",
            "- Gửi link reset mật khẩu qua email\n- Link có hiệu lực 1 giờ\n- Mật khẩu cũ không sử dụng được",
            "3", "Todo", "Authentication", "S1", "2 - Trung binh"
        });

        data.Add(new[] {
            "US-AUTH-06: Xac thuc email",
            "Làm người dùng, tôi muốn xác thực email để tăng cường bảo mật.",
            "- Gửi link xác thực sau đăng ký\n- Click link -> IsEmailVerified = true\n- Một số tính năng bị giới hạn nếu chưa xác thực",
            "3", "Todo", "Authentication", "S1", "2 - Trung binh"
        });

        data.Add(new[] {
            "US-AUTH-07: Ma PIN bao ve PII",
            "Làm khách hàng eKYC, tôi muốn đặt mã PIN để bảo vệ thông tin nhạy cảm.",
            "- Đặt mã PIN 6 số\n- Xem CCCD/GPLX đều yêu cầu PIN\n- Nhập sai 3 lần -> khóa 5 phút",
            "5", "Todo", "Authentication", "S10", "2 - Trung binh"
        });

        data.Add(new[] {
            "=== PHAN HE eKYC ===",
            "", "", "", "", "", "", ""
        });

        data.Add(new[] {
            "US-EKYC-01: Xac thuc eKYC bang CCCD",
            "Làm khách hàng, tôi muốn xác thực danh tính bằng CCCD để thuê xe và đăng ký đối tác.",
            "- Bước 1: Upload CCCD mặt trước\n- Bước 2: Upload CCCD mặt sau\n- Bước 3: Chụp khuôn mặt\n- Gọi FPT eKYC API\n- Trạng thái: Pending -> Verified/Rejected",
            "13", "Todo", "eKYC", "S2", "1 - Cao"
        });

        data.Add(new[] {
            "US-EKYC-02: Duyet / Tu choi eKYC (Admin)",
            "Làm quản trị viên, tôi muốn duyệt hồ sơ eKYC để đảm bảo người dùng hợp lệ.",
            "- Danh sách eKYC chờ duyệt\n- So sánh ảnh CCCD với selfie\n- Duyệt -> VerificationStatus = 'Verified'\n- Từ chối + nhập lý do\n- Gửi email thông báo",
            "5", "Todo", "eKYC", "S2", "1 - Cao"
        });

        data.Add(new[] {
            "US-EKYC-03: Quet QR CCCD chip",
            "Làm khách hàng, tôi muốn quét QR trên CCCD chip để điền thông tin nhanh hơn.",
            "- Tích hợp thư viện quét QR\n- Parse dữ liệu NFC chip\n- Điền tự động: Họ tên, Ngày sinh, CCCD\n- Vẫn yêu cầu chụp ảnh CCCD và selfie",
            "8", "Todo", "eKYC", "S2", "2 - Trung binh"
        });

        data.Add(new[] {
            "=== PHAN HE QUAN LY XE (CAR) ===",
            "", "", "", "", "", "", ""
        });

        data.Add(new[] {
            "US-CAR-01: Tim kiem va loc xe",
            "Làm khách hàng, tôi muốn tìm kiếm xe theo nhiều tiêu chí để nhanh chóng tìm được xe phù hợp.",
            "- Bộ lọc: Loại xe, Số chỗ, Khoảng giá, Địa điểm\n- Chọn ngày nhận/trả xe\n- Sắp xếp: Giá, Đánh giá, Xe mới\n- Phân trang (12 xe/trang)\n- Cache kết quả (Redis)",
            "8", "Todo", "Car", "S1", "1 - Cao"
        });

        data.Add(new[] {
            "US-CAR-02: Xem chi tiet xe",
            "Làm khách hàng, tôi muốn xem chi tiết xe để quyết định có đặt hay không.",
            "- Hình ảnh carousel\n- Thông tin: Tên, Hãng, Biển số, Số chỗ\n- Giá ngày / giờ\n- Tính năng: Bluetooth, GPS...\n- Đánh giá sao\n- Nút 'Đặt xe ngay'",
            "5", "Todo", "Car", "S1", "1 - Cao"
        });

        data.Add(new[] {
            "US-CAR-03: Dang ky xe moi (Partner)",
            "Làm đối tác, tôi muốn đăng ký xe mới để bắt đầu cho thuê trên nền tảng.",
            "- Form: Tên xe, Hãng, Biển số, VIN, Số chỗ\n- Upload ảnh (tối thiểu 3)\n- Nhập giá ngày / giờ\n- Chọn vị trí đỗ (bản đồ)\n- Trạng thái: Chờ phê duyệt",
            "8", "Todo", "Car", "S3", "1 - Cao"
        });

        data.Add(new[] {
            "US-CAR-04: Phe duyet xe (Admin)",
            "Làm quản trị viên, tôi muốn duyệt xe mới để đảm bảo chất lượng xe trên nền tảng.",
            "- Danh sách xe chờ duyệt\n- Xem chi tiết xe\n- Duyệt -> Available\n- Từ chối + lý do\n- Thông báo email",
            "5", "Todo", "Car", "S3", "1 - Cao"
        });

        data.Add(new[] {
            "US-CAR-05: Bao duong xe (Maintenance)",
            "Làm quản trị viên, tôi muốn quản lý bảo dưỡng xe để đảm bảo xe luôn trong tình trạng tốt.",
            "- Đặt lịch bảo dưỡng định kỳ\n- Ghi nhận lịch sử bảo dưỡng\n- Cảnh báo khi sắp đến hạn\n- Chuyển sang trạng thái Maintenance",
            "8", "Todo", "Car", "S4", "2 - Trung binh"
        });

        data.Add(new[] {
            "US-CAR-06: Dinh gia dong theo ngay le",
            "Làm quản trị viên, tôi muốn cấu hình giá động để tối ưu doanh thu vào mùa cao điểm.",
            "- Cấu hình ngày lễ\n- Cấu hình % tăng giá\n- Tự động áp dụng khi đặt\n- Hiển thị giá gốc và giá lễ",
            "5", "Todo", "Car", "S5", "2 - Trung binh"
        });

        data.Add(new[] {
            "US-ELEC-01: Ho tro xe dien (EV)",
            "Làm khách hàng thuê xe điện, tôi muốn xem thông tin pin và trạm sạc.",
            "- Hiển thị dung lượng pin (kWh)\n- Quãng đường ước tính mỗi lần sạc\n- Tích hợp bản đồ trạm sạc\n- Tính phí sạc khi trả xe",
            "8", "Todo", "Car", "S5", "4 - Thap"
        });

        data.Add(new[] {
            "=== PHAN HE DAT XE & GIO HANG (BOOKING) ===",
            "", "", "", "", "", "", ""
        });

        data.Add(new[] {
            "US-BOOK-01: Tao don dat xe don le",
            "Làm khách hàng, tôi muốn đặt một chiếc xe để sử dụng trong khoảng thời gian nhất định.",
            "- Chọn xe từ trang chi tiết\n- Chọn ngày giờ nhận/trả\n- Chọn địa điểm nhận/trả\n- Chọn gói bảo hiểm\n- Tính tổng tiền\n- Auto block xe tránh double booking",
            "8", "Todo", "Booking", "S1", "1 - Cao"
        });

        data.Add(new[] {
            "US-BOOK-02: Gio hang dat nhieu xe",
            "Làm khách hàng, tôi muốn đặt nhiều xe cùng lúc để tiết kiệm thời gian thanh toán.",
            "- Thêm xe vào giỏ hàng\n- Xem danh sách xe trong giỏ\n- Cập nhật thông tin thuê cho từng xe\n- Xóa xe khỏi giỏ\n- Checkout tất cả -> tạo BookingTransaction",
            "13", "Todo", "Booking", "S2", "1 - Cao"
        });

        data.Add(new[] {
            "US-BOOK-03: Thanh toan coc (PayOS)",
            "Làm khách hàng, tôi muốn thanh toán cọc online qua PayOS để xác nhận đơn đặt xe.",
            "- Hiển thị số tiền cọc (30-50% tổng)\n- Tạo Payment URL từ PayOS API\n- Chuyển hướng sang trang PayOS\n- Thành công -> BookingStatus = Pending\n- DepositStatus = Holding",
            "8", "Todo", "Booking", "S2", "1 - Cao"
        });

        data.Add(new[] {
            "US-BOOK-04: Xac nhan don dat (Admin)",
            "Làm quản trị viên, tôi muốn xác nhận đơn đặt xe sau khi khách đã thanh toán cọc.",
            "- Danh sách đơn chờ xác nhận\n- Xem chi tiết đơn\n- Xác nhận -> BookingStatus = Confirmed\n- Từ chối + lý do -> hoàn cọc\n- Gửi email thông báo",
            "5", "Todo", "Booking", "S2", "1 - Cao"
        });

        data.Add(new[] {
            "US-BOOK-05: Ban giao xe (Handover)",
            "Làm nhân viên khu vực, tôi muốn tạo biên bản bàn giao xe để xác nhận xe được giao đúng tình trạng.",
            "- Tạo HandoverRecord\n- Ghi nhận số km hiện tại\n- Kiểm tra ngoại thất, nội thất\n- Chụp ảnh xe\n- Khách ký xác nhận\n- BookingStatus = Active",
            "8", "Todo", "Booking", "S2", "1 - Cao"
        });

        data.Add(new[] {
            "US-BOOK-06: Hoan thanh & Tra xe",
            "Làm nhân viên, tôi muốn hoàn tất trả xe để đóng booking và tính toán chi phí.",
            "- Ghi nhận thời gian trả thực tế\n- Kiểm tra xe sau trả\n- Tính phí quá giờ (100K/giờ)\n- Tính phí vệ sinh (150K)\n- So sánh với DepositAmount\n- DepositStatus = Refunded/Deducted\n- BookingStatus = Completed",
            "5", "Todo", "Booking", "S2", "1 - Cao"
        });

        data.Add(new[] {
            "US-BOOK-07: Huy don dat xe",
            "Làm khách hàng, tôi muốn hủy đơn đặt xe khi có thay đổi kế hoạch.",
            "- Hủy trước 24h -> hoàn 100% cọc\n- Hủy trước 12h -> hoàn 50% cọc\n- Hủy dưới 12h -> không hoàn cọc\n- Admin hủy -> hoàn 100%\n- Giải phóng xe",
            "5", "Todo", "Booking", "S2", "1 - Cao"
        });

        data.Add(new[] {
            "US-BOOK-08: Giao xe tan noi (Delivery)",
            "Làm khách hàng, tôi muốn đặt dịch vụ giao xe tận nơi để tiết kiệm thời gian.",
            "- Nhập địa chỉ giao xe\n- Tính khoảng cách từ điểm đặt\n- Tính phí giao theo km\n- Tài xế được phân công giao",
            "8", "Todo", "Booking", "S3", "2 - Trung binh"
        });

        data.Add(new[] {
            "US-BOOK-09: Thue dai han (Long-term)",
            "Làm khách hàng, tôi muốn thuê xe dài hạn để được giảm giá và bảo dưỡng miễn phí.",
            "- Chọn gói: 1 Tuần (-5%), 1 Tháng (-10%), 3 Tháng (-15%), 6 Tháng (-20%), 1 Năm (-25%)\n- Tạo LongTermRental + RentalContract\n- Thanh toán cọc PayOS\n- FreeMaintenanceCount theo gói",
            "13", "Todo", "Booking", "S5", "2 - Trung binh"
        });

        data.Add(new[] {
            "US-BOOK-10: Thanh toan tra gop",
            "Làm khách hàng thuê dài hạn, tôi muốn thanh toán trả góp hàng tháng.",
            "- Tạo PaymentSchedule\n- Nhắc nhở thanh toán trước 3 ngày\n- Thanh toán trễ -> cảnh báo + phí phạt\n- Trễ quá 7 ngày -> khóa xe từ xa",
            "13", "Todo", "Booking", "S10", "2 - Trung binh"
        });

        data.Add(new[] {
            "US-ADDON-01: Dich vu bo sung (Add-on)",
            "Làm khách hàng, tôi muốn thêm dịch vụ bổ sung để chuyến đi thoải mái hơn.",
            "- Add-on: Nước suối (10K/ngày), Khăn lạnh (5K/ngày), Combo Ăn Nhẹ (50K/ngày), Combo VIP (100K/ngày)\n- Tính phí vào Booking",
            "5", "Todo", "Booking", "S3", "3 - Thap"
        });

        data.Add(new[] {
            "US-INS-01: Bao hiem thue xe",
            "Làm khách hàng, tôi muốn chọn gói bảo hiểm phù hợp để yên tâm khi thuê xe.",
            "- Basic: Miễn phí, bảo vệ tối thiểu\n- Premium: Bảo vệ va chạm, trộm\n- Full Cover: Bảo vệ toàn diện, khấu trừ 0đ",
            "5", "Todo", "Booking", "S5", "3 - Thap"
        });

        data.Add(new[] {
            "US-SUB-01: Dang ky goi thue dinh ky",
            "Làm khách hàng thường xuyên, tôi muốn đăng ký gói thuê định kỳ để được ưu đãi.",
            "- Chọn gói: Hàng tuần, Hàng tháng\n- Chọn xe cố định\n- Thanh toán tự động định kỳ\n- Hủy / Thay đổi gói",
            "5", "Todo", "Booking", "S10", "4 - Thap"
        });

        data.Add(new[] {
            "=== PHAN HE THANH TOAN (PAYMENT) ===",
            "", "", "", "", "", "", ""
        });

        data.Add(new[] {
            "US-PAY-01: Thanh toan PayOS tich hop",
            "Làm khách hàng, tôi muốn thanh toán online qua PayOS để xác nhận đơn đặt xe nhanh chóng.",
            "- Gọi PayOS API tạo payment link\n- Chuyển hướng sang trang thanh toán\n- Hiển thị QR code\n- Lưu Transaction vào BookingTransaction",
            "8", "Todo", "Payment", "S2", "1 - Cao"
        });

        data.Add(new[] {
            "US-PAY-02: Webhook xu ly thanh toan",
            "Làm hệ thống, tôi muốn tự động cập nhật trạng thái đơn hàng khi nhận webhook thanh toán từ PayOS.",
            "- Nhận webhook từ PayOS\n- Verify chữ ký webhook\n- Cập nhật BookingTransaction.Status\n- Cập nhật Booking.DepositStatus\n- Gửi email xác nhận",
            "5", "Todo", "Payment", "S2", "1 - Cao"
        });

        data.Add(new[] {
            "US-PAY-03: Hoan tien coc",
            "Làm khách hàng, tôi muốn được hoàn tiền cọc nhanh chóng khi trả xe hoặc hủy đơn.",
            "- Gọi PayOS refund API\n- DepositRefundJob chạy 9AM hàng ngày\n- Gửi email thông báo hoàn tiền",
            "5", "Todo", "Payment", "S4", "1 - Cao"
        });

        data.Add(new[] {
            "US-PAY-04: Vi dien tu (Wallet)",
            "Làm khách hàng thân thiết, tôi muốn có ví điện tử để thanh toán nhanh hơn và tích điểm.",
            "- Mỗi User có một Wallet\n- Nạp tiền qua PayOS\n- Thanh toán đơn từ số dư\n- Nhận hoàn tiền cọc vào ví\n- Lịch sử giao dịch ví",
            "8", "Todo", "Payment", "S6", "2 - Trung binh"
        });

        data.Add(new[] {
            "US-INV-01: Xuat hoa don (Invoice)",
            "Làm khách hàng doanh nghiệp, tôi muốn nhận hóa đơn GTGT để thanh toán và quyết toán thuế.",
            "- Tạo Invoice cho mỗi Booking\n- Thông tin: Tên công ty, MST, Địa chỉ\n- Xuất hóa đơn PDF\n- Gửi email hóa đơn",
            "5", "Todo", "Payment", "S5", "4 - Thap"
        });

        data.Add(new[] {
            "=== PHAN HE DOI TAC (PARTNER) ===",
            "", "", "", "", "", "", ""
        });

        data.Add(new[] {
            "US-PART-01: Dang ky doi tac",
            "Làm chủ xe, tôi muốn đăng ký làm đối tác để cho thuê xe và tạo thêm thu nhập.",
            "- Đăng ký với vai trò Partner\n- Cập nhật: Địa chỉ, Tọa độ\n- Upload giấy tờ\n- Chờ Admin phê duyệt",
            "5", "Todo", "Partner", "S3", "1 - Cao"
        });

        data.Add(new[] {
            "US-PART-02: Quan ly xe doi tac",
            "Làm đối tác, tôi muốn quản lý xe của mình để cập nhật thông tin và giá thuê.",
            "- Xem danh sách xe của mình\n- Thêm xe mới -> chờ phê duyệt\n- Chỉnh sửa: giá, mô tả, tính năng, ảnh\n- Đặt lịch bảo dưỡng\n- Xem doanh thu theo xe",
            "8", "Todo", "Partner", "S3", "1 - Cao"
        });

        data.Add(new[] {
            "US-PART-03: Hop dong doi tac (Admin)",
            "Làm quản trị viên, tôi muốn quản lý hợp đồng đối tác để đảm bảo quan hệ hợp tác chính thức.",
            "- Tạo PartnerContract\n- Cấu hình: Tỷ lệ hoa hồng\n- Gửi hợp đồng cho đối tác ký\n- Digital Contract (chữ ký số)\n- Gia hạn / Chấm dứt",
            "8", "Todo", "Partner", "S3", "1 - Cao"
        });

        data.Add(new[] {
            "US-PART-04: P2P - Chu xe cho thue",
            "Làm chủ xe cá nhân, tôi muốn cho thuê xe của mình trên nền tảng để tạo thêm thu nhập.",
            "- Đăng ký vai trò P2P Host\n- Đăng ký xe cá nhân\n- eKYC bắt buộc\n- Đặt giá tự do\n- Hoa hồng platform: 20%",
            "8", "Todo", "Partner", "S7", "3 - Thap"
        });

        data.Add(new[] {
            "US-PART-05: Hoa hong & Thanh toan doi tac",
            "Làm đối tác, tôi muốn xem doanh thu và nhận thanh toán hoa hồng hàng tháng.",
            "- Tính: Tổng tiền thuê - Hoa hồng\n- Báo cáo doanh thu theo tháng / xe\n- Thanh toán qua chuyển khoản",
            "5", "Todo", "Partner", "S7", "3 - Thap"
        });

        data.Add(new[] {
            "US-PART-06: Doi tac doanh nghiep (B2B)",
            "Làm doanh nghiệp, tôi muốn đăng ký đối tác B2B để thuê xe cho nhân viên.",
            "- Đăng ký: Tên công ty, MST, Người đại diện\n- Hạn mức công nợ (Credit Limit)\n- Thanh toán theo kỳ (cuối tháng)\n- Xuất hóa đơn GTGT hàng tháng",
            "8", "Todo", "Partner", "S8", "4 - Thap"
        });

        data.Add(new[] {
            "=== PHAN HE TAI Xe (DRIVER) ===",
            "", "", "", "", "", "", ""
        });

        data.Add(new[] {
            "US-DRIV-01: Quan ly tai xe",
            "Làm quản trị viên, tôi muốn quản lý tài xế để phân công hợp lý cho các chuyến đi.",
            "- Thêm tài xế mới\n- Upload giấy tờ: GPLX, CCCD\n- Duyệt tài xế (IsApproved)\n- Cập nhật: kinh nghiệm, đánh giá, cước phí\n- Khóa tài xế vi phạm",
            "8", "Todo", "Driver", "S4", "1 - Cao"
        });

        data.Add(new[] {
            "US-DRIV-02: Phan cong tai xe",
            "Làm quản trị viên, tôi muốn phân công tài xế cho booking để đáp ứng nhu cầu của khách hàng.",
            "- Danh sách tài xế đang rảnh\n- Chọn tài xế: theo khu vực, GPLX, đánh giá\n- Tạo DriverBooking\n- Gửi thông báo cho tài xế",
            "5", "Todo", "Driver", "S4", "1 - Cao"
        });

        data.Add(new[] {
            "US-DRIV-03: Dashboard tai xe",
            "Làm tài xế, tôi muốn có dashboard để xem lịch sử chuyến đi và thông tin chi tiết từng chuyến.",
            "- Xem danh sách chuyến đã phân công\n- Xem chi tiết: thông tin khách, địa điểm\n- Bật/tắt trạng thái sẵn sàng\n- Xem thống kê: tổng chuyến, thu nhập, đánh giá",
            "5", "Todo", "Driver", "S4", "1 - Cao"
        });

        data.Add(new[] {
            "US-DRIV-04: Bat dau / Ket thuc chuyen",
            "Làm tài xế, tôi muốn bắt đầu và kết thúc chuyến đi trên ứng dụng để cập nhật trạng thái.",
            "- Nút 'Bắt đầu chuyến' -> cập nhật StartedAt\n- Ghi nhận vị trí GPS\n- Nút 'Kết thúc chuyến' -> cập nhật EndedAt\n- Hủy phân công với lý do",
            "5", "Todo", "Driver", "S4", "1 - Cao"
        });

        data.Add(new[] {
            "US-DRIV-05: Danh gia tai xe",
            "Làm khách hàng, tôi muốn đánh giá tài xế để giúp người khác chọn được tài xế tốt.",
            "- Đánh giá sao (1-5) + bình luận\n- Cập nhật Driver.Rating trung bình\n- Tăng Driver.TotalTrips\n- Admin duyệt đánh giá",
            "3", "Todo", "Driver", "S4", "2 - Trung binh"
        });

        data.Add(new[] {
            "=== PHAN HE KHUYEN MAI & HANG THANH VIEN ===",
            "", "", "", "", "", "", ""
        });

        data.Add(new[] {
            "US-PROMO-01: Khuyen mai & Ma giam gia",
            "Làm quản trị viên, tôi muốn tạo mã giảm giá để thu hút khách hàng trong các dịp đặc biệt.",
            "- Tạo Promotion: Mã, % giảm, MaxDiscount\n- Cấu hình: Ngày bắt đầu, kết thúc, số lượt\n- Khách nhập PromoCode khi checkout\n- Cập nhật UsedCount",
            "5", "Todo", "Promotion", "S3", "2 - Trung binh"
        });

        data.Add(new[] {
            "US-PROMO-02: Membership - Hang thanh vien",
            "Làm khách hàng thân thiết, tôi muốn được xếp hạng thành viên để hưởng các ưu đãi đặc biệt.",
            "- Các hạng: Bronze, Silver, Gold, Platinum, Diamond\n- Auto nâng hạng khi TotalSpending đạt ngưỡng\n- Áp dụng MembershipDiscountPercent\n- Hiển thị badge hạng thành viên",
            "8", "Todo", "Promotion", "S5", "2 - Trung binh"
        });

        data.Add(new[] {
            "=== PHAN HE DANH GIA (REVIEW) ===",
            "", "", "", "", "", "", ""
        });

        data.Add(new[] {
            "US-REVIEW-01: Danh gia & Nhan xet xe",
            "Làm khách hàng, tôi muốn đánh giá xe đã thuê để giúp những khách hàng khác đưa ra quyết định.",
            "- Mời đánh giá sau khi Booking = Completed\n- Đánh giá sao (1-5) + nhận xét\n- Admin duyệt trước khi hiển thị\n- Tính điểm đánh giá trung bình",
            "3", "Todo", "Review", "S3", "2 - Trung binh"
        });

        data.Add(new[] {
            "=== PHAN HE HOP DONG (CONTRACT) ===",
            "", "", "", "", "", "", ""
        });

        data.Add(new[] {
            "US-CONTRACT-01: Hop dong so (Digital Contract)",
            "Làm khách hàng, tôi muốn ký hợp đồng số để tiết kiệm thời gian và dễ dàng lưu trữ.",
            "- Tạo DigitalContract tự động khi booking xác nhận\n- Lưu snapshot thông tin khách và xe\n- Khách ký online\n- Chủ xe ký (Admin/Partner)\n- Download PDF hợp đồng",
            "8", "Todo", "Contract", "S3", "2 - Trung binh"
        });

        data.Add(new[] {
            "US-CONTRACT-02: Hop dong thue dai han",
            "Làm khách hàng thuê dài hạn, tôi muốn có hợp đồng chi tiết bao gồm quyền lợi bảo dưỡng miễn phí.",
            "- RentalContract cho từng gói thuê\n- Điều khoản: Thời hạn, Giá, Lịch thanh toán\n- Gia hạn tự động\n- ContractExpirationJob kiểm tra hết hạn",
            "8", "Todo", "Contract", "S5", "2 - Trung binh"
        });

        data.Add(new[] {
            "=== PHAN HE GPS & THEO DOI (GPS/TRACKING) ===",
            "", "", "", "", "", "", ""
        });

        data.Add(new[] {
            "US-GPS-01: Theo doi GPS xe",
            "Làm quản trị viên, tôi muốn xem vị trí xe trên bản đồ để quản lý hiệu quả.",
            "- Hiển thị bản đồ với vị trí tất cả xe đang thuê\n- Cập nhật vị trí theo thời gian thực\n- Xem lộ trình chi tiết của một chuyến đi\n- Lưu lịch sử lộ trình",
            "8", "Todo", "GPS/Tracking", "S4", "2 - Trung binh"
        });

        data.Add(new[] {
            "US-GPS-02: Canh bao lo trinh bat thuong",
            "Làm quản trị viên, tôi muốn nhận cảnh báo khi xe đi sai lộ trình hoặc có dấu hiệu bất thường.",
            "- Phát hiện xe đi off-route\n- Phát hiện dừng đỗ bất thường\n- Phát hiện mất GPS\n- Phát hiện nguy cơ trộm\n- Cảnh báo Overdue (quá giờ trả)",
            "5", "Todo", "GPS/Tracking", "S4", "2 - Trung binh"
        });

        data.Add(new[] {
            "=== PHAN HE CHATBOT & HUONG DAN (CHAT & GUIDE) ===",
            "", "", "", "", "", "", ""
        });

        data.Add(new[] {
            "US-CHAT-01: Chat AI ho tro (Groq/Gemini)",
            "Làm khách hàng, tôi muốn chat với AI để được hỗ trợ nhanh 24/7 về thông tin thuê xe.",
            "- Giao diện chat trực tuyến\n- Tích hợp Groq AI (miễn phí, không giới hạn)\n- AI trả lời về: quy trình, giá cả, chính sách\n- Hỗ trợ tiếng Việt",
            "8", "Todo", "Chat", "S3", "3 - Thap"
        });

        data.Add(new[] {
            "US-CHAT-02: Lich su chat",
            "Làm khách hàng, tôi muốn xem lại lịch sử chat để tra cứu thông tin đã được hỗ trợ.",
            "- Mỗi người dùng có lịch sử chat riêng\n- Xem danh sách phiên chat\n- Xem chi tiết nội dung từng phiên",
            "3", "Todo", "Chat", "S3", "3 - Thap"
        });

        data.Add(new[] {
            "US-GUIDE-01: Trang huong dan su dung",
            "Làm khách hàng mới, tôi muốn xem hướng dẫn sử dụng để biết cách đặt xe và sử dụng dịch vụ.",
            "- Hướng dẫn đặt xe cho khách hàng\n- Hướng dẫn đăng ký đối tác\n- Hướng dẫn sử dụng cho tài xế\n- Hướng dẫn cho nhân viên quản lý\n- FAQ - Câu hỏi thường gặp",
            "3", "Todo", "Guide", "S1", "3 - Thap"
        });

        data.Add(new[] {
            "=== PHAN HE KHU VUC (AREA MANAGEMENT) ===",
            "", "", "", "", "", "", ""
        });

        data.Add(new[] {
            "US-ADM-05: Quan ly khu vuc (Multi-Tenant)",
            "Làm quản trị viên, tôi muốn tạo và quản lý nhiều khu vực để phục vụ khách hàng theo từng địa bàn.",
            "- Tạo Area: Tên, Mã, Tỉnh/Thành, Địa chỉ, Tọa độ\n- Gán quản lý (Manager) cho mỗi khu vực\n- Gán nhân viên (Staff) vào khu vực\n- Gán địa điểm (Location) vào khu vực\n- Staff đăng nhập -> chỉ thấy dữ liệu khu vực mình",
            "13", "Todo", "Admin", "S6", "2 - Trung binh"
        });

        data.Add(new[] {
            "US-ADM-06: Phan quyen Area-based",
            "Làm nhân viên khu vực, tôi chỉ muốn thấy và quản lý dữ liệu của khu vực mình phụ trách.",
            "- Authorization Policy: AreaView, AreaCreate, AreaUpdate...\n- Kiểm tra AreaID của user\n- Controller kiểm tra BelongsToArea()\n- Staff chỉ xem Dashboard khu vực mình",
            "8", "Todo", "Admin", "S6", "2 - Trung binh"
        });

        data.Add(new[] {
            "US-AREA-01: Diem don / Tra xe",
            "Làm quản lý khu vực, tôi muốn quản lý các điểm đón trả xe để khách hàng dễ dàng chọn địa điểm.",
            "- Tạo Location: Tên, Địa chỉ, Tọa độ\n- Gán Location vào Area\n- Hiển thị danh sách điểm đón khi khách đặt",
            "5", "Todo", "Area", "S6", "3 - Thap"
        });

        data.Add(new[] {
            "US-AREA-02: Thong ke theo khu vuc",
            "Làm quản lý khu vực, tôi muốn xem thống kê của khu vực mình để đánh giá hiệu quả kinh doanh.",
            "- Thống kê: Số xe, Số booking, Doanh thu, Tỷ lệ sử dụng\n- So sánh giữa các khu vực\n- Xuất báo cáo Excel/PDF",
            "5", "Todo", "Area", "S6", "3 - Thap"
        });

        data.Add(new[] {
            "=== PHAN HE QUAN TRI HE THONG (ADMIN) ===",
            "", "", "", "", "", "", ""
        });

        data.Add(new[] {
            "US-ADM-01: Dashboard quan tri",
            "Làm quản trị viên, tôi muốn xem Dashboard để nắm bắt tình hình hoạt động của toàn hệ thống.",
            "- Tổng quan: Booking hôm nay, Doanh thu, Xe đang thuê\n- Badge: eKYC chờ duyệt, Xe chờ duyệt, Đơn chờ xác nhận\n- Biểu đồ doanh thu theo tháng\n- Top xe được thuê nhiều nhất",
            "8", "Todo", "Admin", "S1", "1 - Cao"
        });

        data.Add(new[] {
            "US-ADM-02: Quan ly nguoi dung",
            "Làm quản trị viên, tôi muốn quản lý người dùng để theo dõi và quản lý tài khoản.",
            "- Danh sách người dùng: Customer, Partner, Staff, Admin\n- Tìm kiếm, lọc theo vai trò, trạng thái eKYC\n- Xem chi tiết, Khóa/mở khóa\n- Thêm người dùng mới",
            "5", "Todo", "Admin", "S1", "1 - Cao"
        });

        data.Add(new[] {
            "US-ADM-03: Bao cao thong ke",
            "Làm quản trị viên, tôi muốn xem các báo cáo thống kê chi tiết để đưa ra quyết định kinh doanh.",
            "- Báo cáo doanh thu theo ngày / tuần / tháng / năm\n- Báo cáo số lượng booking theo loại xe, khu vực\n- Báo cáo tỷ lệ sử dụng xe\n- Xuất Excel / PDF",
            "8", "Todo", "Admin", "S5", "1 - Cao"
        });

        data.Add(new[] {
            "US-ADM-04: Audit Log - Nhat ky he thong",
            "Làm quản trị viên, tôi muốn xem nhật ký hệ thống để kiểm tra và điều tra khi có sự cố.",
            "- AuditLogMiddleware ghi lại mọi HTTP request\n- Thông tin: UserID, Action, Controller, Timestamp, IP\n- Lọc: Người dùng, Thời gian, Controller\n- Xuất log ra file",
            "5", "Todo", "Admin", "S10", "1 - Cao"
        });

        data.Add(new[] {
            "US-LOOKUP-01: Quan ly danh muc (Car Brand/Type)",
            "Làm quản trị viên, tôi muốn quản lý danh mục xe để hệ thống luôn cập nhật các hãng và loại xe mới.",
            "- CRUD CarBrand (Toyota, Honda, Mercedes...)\n- CRUD CarType (Sedan, SUV, Hatchback...)\n- Thêm/sửa/xóa từ Admin panel",
            "3", "Todo", "Admin", "S1", "3 - Thap"
        });

        data.Add(new[] {
            "=== PHAN HE HA TANG & BAO MAT (INFRASTRUCTURE & SECURITY) ===",
            "", "", "", "", "", "", ""
        });

        data.Add(new[] {
            "US-BACKUP-01: Backup database tu dong",
            "Làm quản trị viên, tôi muốn hệ thống tự động backup database để đảm bảo an toàn dữ liệu.",
            "- Backup tự động hàng ngày (Hangfire)\n- Lưu trữ trên cloud (Azure Blob / AWS S3)\n- Chính sách giữ: 7 bản gần nhất\n- Admin tạo backup thủ công\n- Khôi phục từ backup",
            "5", "Todo", "Infrastructure", "S10", "3 - Thap"
        });

        data.Add(new[] {
            "US-BACKUP-02: Snapshot he thong",
            "Làm quản trị viên, tôi muốn tạo snapshot để có thể phục hồi hệ thống về trạng thái trước đó.",
            "- Tạo snapshot: Database state, File uploads\n- Ghi chú mô tả snapshot\n- Danh sách snapshot đã tạo\n- Phục hồi từ snapshot",
            "5", "Todo", "Infrastructure", "S10", "3 - Thap"
        });

        data.Add(new[] {
            "US-SEC-01: Rate Limiting nang cao",
            "Làm quản trị viên, tôi muốn hệ thống tự động chặn IP tấn công brute force.",
            "- Giới hạn request/giây theo endpoint\n- Chặn IP tạm thời khi vượt ngưỡng\n- Danh sách IP bị chặn (Admin unblock)\n- Log các attempt tấn công",
            "5", "Todo", "Security", "S10", "4 - Thap"
        });

        data.Add(new[] {
            "US-SEC-02: Ma hoa PII nang cao",
            "Làm quản trị viên, tôi muốn đảm bảo PII luôn được mã hóa và chỉ người được ủy quyền mới xem được.",
            "- PIIEncryptionService mã hóa CCCD, GPLX, SĐT\n- Key rotation định kỳ\n- Audit mọi lần truy cập PII\n- SHA-256 hash kiểm tra trùng lặp",
            "5", "Todo", "Security", "S10", "4 - Thap"
        });

        data.Add(new[] {
            "=== PHAN HE THONG BAO (NOTIFICATION) ===",
            "", "", "", "", "", "", ""
        });

        data.Add(new[] {
            "US-NOTIFY-01: Thong bao day (Email/SMS)",
            "Làm khách hàng, tôi muốn nhận thông báo qua email và SMS để cập nhật trạng thái đơn đặt xe.",
            "- Gửi email: Xác nhận đặt, Thanh toán thành công, Nhắc trả xe\n- Gửi SMS: Mã OTP, Thông báo quan trọng\n- EmailBackgroundJob xử lý hàng đợi\n- Template email HTML đẹp",
            "5", "Todo", "Notification", "S4", "3 - Thap"
        });

        return data;
    }
}
