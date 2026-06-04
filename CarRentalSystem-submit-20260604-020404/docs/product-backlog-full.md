# CarRentalSystem - Product Backlog (Phát Hành 1.0)

> **Template**: As a/an [Vai trò] | I want to [Mục tiêu] | So that [Lý do]
> **Độ ưu tiên**: 1=Critical, 2=High, 3=Medium, 4=Low
> **Trạng thái**: Mới | Đang thực hiện | Đang review | Hoàn thành | Đã xóa
> **Story Points**: Fibonacci (1, 2, 3, 5, 8, 13, 21)

---

## TỔNG QUAN BACKLOG

| Chỉ số | Giá trị |
|---|---|
| Tổng PBIs | 72 |
| Tổng Story Points | ~310 |
| Critical (P1) | 12 |
| High (P2) | 20 |
| Medium (P3) | 25 |
| Low (P4) | 15 |
| Sprints | 6 (Sprint 1-6) |

---

## SPRINT 1 - Nền tảng & Luồng Đặt Xe Cốt Lõi (MVP)

### Sprint 1.1 - Xác thực & Bảo mật (Tuần 1-2)

| Backlog ID | Backlog | As a/an [Vai trò] | I want to [Mục tiêu] | So that [Lý do] | Story ID | Độ ưu tiên | Giá trị kinh doanh | SP | Sprint# | Trạng thái | Ghi chú | Story Points |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| PB01 | Xác thực người dùng | **Khách hàng** | Đăng ký và xác minh email | Tôi có thể có tài khoản bảo mật để sử dụng hệ thống | AUTH01 | 1 | High | 2 | 1 | Mới | Luồng xác minh email, token | 2 |
| PB01 | Xác thực người dùng | **Khách hàng** | Đăng nhập bằng email/mật khẩu | Tôi có thể truy cập tài khoản của mình một cách bảo mật | AUTH02 | 1 | High | 1 | 1 | Mới | Xác thực cookie với remember me | 1 |
| PB01 | Xác thực người dùng | **Quản trị viên** | Quản lý tài khoản người dùng (kích hoạt/vô hiệu hóa) | Tôi có thể kiểm soát quyền truy cập người dùng vào hệ thống | AUTH03 | 1 | High | 1 | 1 | Mới | Quản lý người dùng, phân quyền | 2 |
| PB01 | Xác thực người dùng | **Hệ thống** | Khóa tài khoản sau 5 lần đăng nhập thất bại | Tôi có thể ngăn chặn tấn công brute force | AUTH04 | 1 | High | 1 | 1 | Mới | Khóa tài khoản qua middleware | 2 |
| PB01 | Xác thực người dùng | **Quản trị viên** | Xem nhật ký kiểm tra của tất cả hành động hệ thống | Tôi có thể theo dõi mọi hoạt động trên hệ thống | AUTH05 | 1 | High | 1 | 1 | Mới | Audit trail với thời gian, người dùng, hành động, IP | 3 |
| PB02 | Bảo mật | **Hệ thống** | Mã hóa dữ liệu PII khi lưu trữ (CCCD, SĐT, GPLX) | Tôi có thể bảo vệ dữ liệu nhạy cảm của người dùng | SEC01 | 1 | High | 1 | 1 | Mới | Mã hóa DataProtection API | 3 |
| PB02 | Bảo mật | **Hệ thống** | Áp dụng giới hạn tốc độ trên tất cả endpoint công khai | Tôi có thể ngăn chặn lạm dụng và tấn công brute force | SEC02 | 1 | High | 1 | 1 | Mới | Middleware giới hạn tốc độ | 2 |
| PB02 | Bảo mật | **Quản trị viên** | Xem và quản lý danh sách đen người dùng | Tôi có thể chặn người dùng độc hại | SEC03 | 2 | High | 1 | 1 | Mới | Danh sách đen kèm ghi chú lý do | 2 |
| PB02 | Bảo mật | **Khách hàng** | Đặt lại mật khẩu qua liên kết email | Tôi có thể khôi phục tài khoản khi quên mật khẩu | SEC04 | 2 | Medium | 1 | 1 | Mới | Đặt lại mật khẩu bảo mật qua token | 2 |
| PB03 | Hồ sơ người dùng | **Khách hàng** | Hoàn thành eKYC của tôi (tải lên CCCD, xác minh khuôn mặt) | Tôi có thể xác minh danh tính để thuê xe | PRO01 | 1 | High | 1 | 1 | Mới | Tích hợp FPT eKYC - OCR + kiểm tra khuôn mặt | 5 |
| PB03 | Hồ sơ người dùng | **Khách hàng** | Xem và chỉnh sửa thông tin hồ sơ của tôi | Tôi có thể cập nhật thông tin cá nhân của mình | PRO02 | 2 | High | 1 | 1 | Mới | Tên, SĐT, địa chỉ, ngày sinh, giới tính | 2 |
| PB03 | Hồ sơ người dùng | **Quản trị viên** | Phê duyệt hoặc từ chối hồ sơ eKYC của người dùng | Tôi có thể xác minh giấy tờ danh tính người dùng | PRO03 | 1 | High | 1 | 1 | Mới | Hàng đợi duyệt eKYC của admin | 3 |

### Sprint 1.2 - Danh mục xe & Tìm kiếm (Tuần 2-3)

| Backlog ID | Backlog | As a/an [Vai trò] | I want to [Mục tiêu] | So that [Lý do] | Story ID | Độ ưu tiên | Giá trị kinh doanh | SP | Sprint# | Trạng thái | Ghi chú | Story Points |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| PB04 | Danh mục xe | **Khách hàng** | Duyệt và tìm kiếm xe với bộ lọc (hãng, loại, số ghế, giá, nhiên liệu) | Tôi có thể tìm chiếc xe phù hợp với nhu cầu của mình | CAR01 | 1 | High | 1 | 1 | Mới | 21 tham số tìm kiếm, phân trang | 5 |
| PB04 | Danh mục xe | **Khách hàng** | Xem thông tin chi tiết xe (ảnh, tính năng, giá, bảo hiểm) | Tôi có thể đưa ra quyết định đặt xe sáng suốt | CAR02 | 1 | High | 1 | 1 | Mới | Bộ sưu tập ảnh, chi tiết giá | 2 |
| PB04 | Danh mục xe | **Đối tác** | Đăng ký làm đối tác cho thuê xe | Tôi có thể đăng xe của mình để cho thuê | CAR03 | 1 | High | 1 | 1 | Mới | Luồng đăng ký đối tác | 5 |
| PB04 | Danh mục xe | **Đối tác** | Thêm và quản lý danh sách xe của tôi | Tôi có thể kiểm soát hàng tồn kho xe của mình | CAR04 | 1 | High | 1 | 1 | Mới | Thêm xe, chỉnh sửa, tải ảnh | 5 |
| PB04 | Danh mục xe | **Đối tác** | Đặt giá tùy chỉnh và giảm giá cho xe của tôi | Tôi có thể tối đa hóa doanh thu | CAR05 | 2 | High | 1 | 1 | Mới | Tỷ lệ giảm giá cấp đối tác | 3 |
| PB04 | Danh mục xe | **Quản trị viên** | Phê duyệt hoặc từ chối danh sách xe mới từ đối tác | Tôi có thể đảm bảo chất lượng xe trên nền tảng | CAR06 | 1 | High | 1 | 1 | Mới | Hàng đợi phê duyệt của admin | 3 |
| PB04 | Danh mục xe | **Quản trị viên** | Quản lý toàn bộ hàng tồn kho xe (CRUD tất cả xe) | Tôi có thể kiểm soát toàn bộ hàng tồn kho xe | CAR07 | 1 | High | 1 | 1 | Mới | Bảng quản lý xe đầy đủ | 3 |
| PB04 | Danh mục xe | **Đối tác** | Xem doanh thu và thu nhập của tôi cho mỗi xe | Tôi có thể theo dõi hiệu suất kinh doanh | CAR08 | 2 | Medium | 1 | 1 | Mới | Bảng thu nhập đối tác | 3 |
| PB05 | Trạng thái xe | **Hệ thống** | Theo dõi tình trạng sẵn sàng của xe dựa trên lịch đặt | Tôi có thể ngăn chặn đặt trùng | STA01 | 1 | High | 1 | 1 | Mới | Lịch trình sẵn sàng với khóa TTL | 5 |
| PB05 | Trạng thái xe | **Quản trị viên** | Đặt trạng thái xe (Sẵn sàng, Bảo dưỡng, Ngừng hoạt động) | Tôi có thể quản lý vòng đời xe | STA02 | 2 | High | 1 | 1 | Mới | Quản lý trạng thái kèm lịch sử | 2 |

### Sprint 1.3 - Đặt xe & Giỏ hàng (Tuần 3-4)

| Backlog ID | Backlog | As a/an [Vai trò] | I want to [Mục tiêu] | So that [Lý do] | Story ID | Độ ưu tiên | Giá trị kinh doanh | SP | Sprint# | Trạng thái | Ghi chú | Story Points |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| PB06 | Đặt xe đơn lẻ | **Khách hàng** | Đặt một xe với khoảng thời gian và địa điểm | Tôi có thể thuê xe cho chuyến đi của mình | BOOK01 | 1 | High | 1 | 1 | Mới | Luồng đặt xe bình thường (workflow F1) | 5 |
| PB06 | Đặt xe đơn lẻ | **Khách hàng** | Chọn địa điểm nhận và trả xe | Tôi có thể chọn địa điểm thuận tiện | BOOK02 | 1 | High | 1 | 1 | Mới | Bộ chọn địa điểm, tùy chọn giao xe | 3 |
| PB06 | Đặt xe đơn lẻ | **Khách hàng** | Chọn các dịch vụ bổ sung tùy chọn (nước, đồ ăn nhẹ, gói VIP) | Tôi có thể tùy chỉnh trải nghiệm thuê xe của mình | BOOK03 | 2 | Medium | 1 | 1 | Mới | Dịch vụ bổ sung khi thanh toán | 2 |
| PB06 | Đặt xe đơn lẻ | **Khách hàng** | Áp dụng mã voucher/khuyến mãi khi thanh toán | Tôi có thể được giảm giá khi đặt xe | BOOK04 | 2 | High | 1 | 1 | Mới | Xác thực mã khuyến mãi và giảm giá | 3 |
| PB06 | Đặt xe đơn lẻ | **Khách hàng** | Thanh toán tiền đặt cọc qua cổng thanh toán PayOS | Tôi có thể thanh toán đặt xe của mình một cách bảo mật | BOOK05 | 1 | High | 1 | 1 | Mới | Tích hợp PayOS, xử lý webhook | 5 |
| PB06 | Đặt xe đơn lẻ | **Khách hàng** | Xem lịch sử và trạng thái đặt xe của tôi | Tôi có thể theo dõi tất cả các đơn đặt | BOOK06 | 1 | High | 1 | 1 | Mới | Tất cả đơn đặt với theo dõi trạng thái | 3 |
| PB06 | Đặt xe đơn lẻ | **Khách hàng** | Hủy đơn đặt xe của tôi và nhận hoàn tiền | Tôi có thể thay đổi kế hoạch khi cần | BOOK07 | 2 | High | 1 | 1 | Mới | Chính sách hủy, hoàn tiền một phần/toàn bộ | 3 |
| PB07 | Giỏ hàng nhiều xe | **Khách hàng** | Thêm nhiều xe vào giỏ hàng và thanh toán cùng lúc | Tôi có thể đặt nhiều xe trong một giao dịch | CART01 | 1 | High | 1 | 1 | Mới | Giỏ hàng nhiều xe (workflow F2) | 8 |
| PB07 | Giỏ hàng nhiều xe | **Khách hàng** | Thêm tùy chọn tài xế cho các mục trong giỏ hàng | Tôi có thể thuê tài xế cho chuyến đi | CART02 | 2 | Medium | 1 | 1 | Mới | Chọn tài xế theo từng mục | 2 |
| PB07 | Giỏ hàng nhiều xe | **Hệ thống** | Giữ hàng tồn kho cho các mục trong giỏ hàng với thời hạn | Tôi có thể ngăn chặn bán quá số lượng | CART03 | 1 | High | 1 | 1 | Mới | Khóa hàng tồn kho dựa trên TTL | 5 |
| PB08 | Hợp đồng | **Khách hàng** | Ký hợp đồng số khi đặt xe | Tôi có thể hoàn thành đặt xe với thỏa thuận kỹ thuật số | CON01 | 1 | High | 1 | 1 | Mới | Hợp đồng số với thông tin bên B | 5 |
| PB08 | Hợp đồng | **Khách hàng** | Ký hợp đồng số | Tôi có thể đồng ý hợp pháp với các điều khoản thuê xe | CON02 | 1 | High | 1 | 1 | Mới | Luồng ký số | 3 |
| PB08 | Hợp đồng | **Đối tác** | Ký hợp đồng và phê duyệt đơn đặt | Tôi có thể chấp nhận yêu cầu thuê xe trên xe của mình | CON03 | 1 | High | 1 | 1 | Mới | Luồng chấp nhận của đối tác | 3 |

### Sprint 1.4 - Tài xế & Bàn giao (Tuần 4)

| Backlog ID | Backlog | As a/an [Vai trò] | I want to [Mục tiêu] | So that [Lý do] | Story ID | Độ ưu tiên | Giá trị kinh doanh | SP | Sprint# | Trạng thái | Ghi chú | Story Points |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| PB09 | Tài xế | **Đối tác** | Đăng ký làm tài xế | Tôi có thể cung cấp dịch vụ lái xe | DRI01 | 2 | Medium | 1 | 1 | Mới | Đăng ký tài xế với tải GPLX | 3 |
| PB09 | Tài xế | **Khách hàng** | Thêm dịch vụ tài xế vào đơn đặt xe của tôi | Tôi có thể thuê xe có tài xế chuyên nghiệp | DRI02 | 2 | Medium | 1 | 1 | Mới | Cờ WithDriver, chọn tài xế | 3 |
| PB09 | Tài xế | **Tài xế** | Xem và quản lý các đơn đặt được phân công | Tôi có thể xem lịch làm việc của mình | DRI03 | 1 | High | 1 | 1 | Mới | Bảng điều khiển tài xế | 3 |
| PB09 | Tài xế | **Tài xế** | Chấp nhận/từ chối phân công chuyến đi | Tôi có thể chọn công việc phù hợp | DRI04 | 1 | High | 1 | 1 | Mới | Bật/tắt trạng thái sẵn sàng | 2 |
| PB09 | Tài xế | **Tài xế** | Bắt đầu và hoàn thành chuyến đi | Tôi có thể ghi nhận trạng thái chuyến đi | DRI05 | 1 | High | 1 | 1 | Mới | Hành động StartTrip, CompleteTrip | 3 |
| PB09 | Tài xế | **Quản trị viên** | Phê duyệt đơn đăng ký tài xế | Tôi có thể xác minh trình độ tài xế | DRI06 | 1 | High | 1 | 1 | Mới | Hàng đợi phê duyệt tài xế của admin | 2 |
| PB09 | Tài xế | **Quản trị viên** | Quản lý hợp đồng tài xế (lương, phí chuyến, thưởng) | Tôi có thể quản lý hợp đồng lao động tài xế | DRI07 | 2 | Medium | 1 | 1 | Mới | Quản lý hợp đồng tài xế | 3 |
| PB10 | Bàn giao | **Tài xế** | Ghi nhận việc bàn giao xe với ảnh và chữ ký | Tôi có thể ghi lại tình trạng xe khi nhận | HND01 | 1 | High | 1 | 1 | Mới | Bàn giao CheckOut - đồng hồ, nhiên liệu, ảnh | 3 |
| PB10 | Bàn giao | **Tài xế** | Ghi nhận việc trả xe với ảnh và chữ ký | Tôi có thể ghi lại tình trạng xe khi trả | HND02 | 1 | High | 1 | 1 | Mới | Bàn giao CheckIn - kiểm tra hư hỏng, nhiên liệu | 3 |
| PB10 | Bàn giao | **Hệ thống** | Tính giá cuối cùng dựa trên dữ liệu bàn giao | Tôi có thể tính phí hư hỏng và trả muộn | HND03 | 1 | High | 1 | 1 | Mới | Phí trả muộn, phí nhiên liệu, phí hư hỏng | 5 |
| PB10 | Bàn giao | **Quản trị viên** | Tạo báo cáo kiểm tra sau mỗi chuyến đi | Tôi có thể ghi lại tình trạng xe | INS01 | 2 | Medium | 1 | 1 | Mới | Bản ghi kiểm tra với ảnh | 3 |

---

## SPRINT 2 - Thanh toán, Hóa đơn & Thuê dài hạn

### Sprint 2.1 - Thanh toán & Ví điện tử (Tuần 5-6)

| Backlog ID | Backlog | As a/an [Vai trò] | I want to [Mục tiêu] | So that [Lý do] | Story ID | Độ ưu tiên | Giá trị kinh doanh | SP | Sprint# | Trạng thái | Ghi chú | Story Points |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| PB11 | Thanh toán | **Khách hàng** | Thanh toán toàn bộ qua PayOS | Tôi có thể hoàn tất thanh toán của mình một cách bảo mật | PAY01 | 1 | High | 2 | 2 | Mới | Tích hợp thanh toán PayOS đầy đủ | 5 |
| PB11 | Thanh toán | **Khách hàng** | Xem lịch sử và chi tiết giao dịch thanh toán | Tôi có thể theo dõi tất cả giao dịch của mình | PAY02 | 1 | High | 2 | 2 | Mới | Lịch sử giao dịch kèm trạng thái | 2 |
| PB11 | Thanh toán | **Quản trị viên** | Giám sát tất cả thanh toán và yêu cầu hoàn tiền | Tôi có thể giám sát tất cả hoạt động tài chính | PAY03 | 1 | High | 2 | 2 | Mới | Bảng giám sát thanh toán | 2 |
| PB11 | Thanh toán | **Khách hàng** | Yêu cầu hoàn tiền cho đơn đặt đã hủy | Tôi có thể nhận lại tiền khi hủy đơn | PAY04 | 2 | High | 2 | 2 | Mới | Luồng yêu cầu hoàn tiền, theo dõi trạng thái | 3 |
| PB11 | Thanh toán | **Hệ thống** | Xử lý hoàn tiền tự động khi hủy đơn | Tôi có thể đảm bảo hoàn tiền công bằng cho khách hàng | PAY05 | 1 | High | 2 | 2 | Mới | Tính toán và xử lý hoàn tiền tự động | 3 |
| PB12 | Ví điện tử | **Khách hàng** | Nạp tiền vào số dư ví | Tôi có thể thanh toán nhanh với số dư đã lưu | WAL01 | 1 | High | 2 | 2 | Mới | Nạp tiền qua PayOS | 3 |
| PB12 | Ví điện tử | **Khách hàng** | Xem số dư ví và lịch sử giao dịch | Tôi có thể quản lý quỹ trong ví của mình | WAL02 | 1 | High | 2 | 2 | Mới | Bảng điều khiển ví | 2 |
| PB12 | Ví điện tử | **Đối tác** | Nhận thanh toán vào ví | Tôi có thể được trả tiền cho việc cho thuê xe | WAL03 | 1 | High | 2 | 2 | Mới | Thanh toán doanh thu (chia sẻ 80%) | 5 |
| PB12 | Ví điện tử | **Tài xế** | Nhận phí chuyến đi vào ví | Tôi có thể được trả cho các chuyến đi của mình | WAL04 | 2 | Medium | 2 | 2 | Mới | Thanh toán thu nhập tài xế | 3 |
| PB12 | Ví điện tử | **Hệ thống** | Khóa số tiền trong ví trong quá trình giao dịch đang chờ | Tôi có thể ngăn chặn chi tiêu kép | WAL05 | 1 | High | 2 | 2 | Mới | Ngăn chi tiêu kép | 3 |
| PB13 | Hóa đơn | **Khách hàng** | Xem và tải xuống hóa đơn cho đơn đặt đã hoàn thành | Tôi có thể nhận bằng chứng giao dịch | INV01 | 1 | High | 2 | 2 | Mới | Hóa đơn PDF với chi tiết từng mục | 3 |
| PB13 | Hóa đơn | **Quản trị viên** | Tạo hóa đơn cho tất cả các đơn đặt | Tôi có thể xuất hóa đơn chính thức | INV02 | 1 | High | 2 | 2 | Mới | Tự động tạo khi hoàn thành đặt xe | 5 |

### Sprint 2.2 - Công cụ tính giá & Định giá (Tuần 6-7)

| Backlog ID | Backlog | As a/an [Vai trò] | I want to [Mục tiêu] | So that [Lý do] | Story ID | Độ ưu tiên | Giá trị kinh doanh | SP | Sprint# | Trạng thái | Ghi chú | Story Points |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| PB14 | Định giá | **Hệ thống** | Tính giá thuê dựa trên thời gian, loại xe, hội viên | Tôi có thể cung cấp giá chính xác cho khách hàng | BIL01 | 1 | High | 2 | 2 | Mới | Công cụ báo giá với 15+ thành phần giá | 8 |
| PB14 | Định giá | **Quản trị viên** | Đặt hệ số tăng giá ngày lễ | Tôi có thể tối đa hóa doanh thu trong mùa cao điểm | BIL02 | 1 | High | 2 | 2 | Mới | Quản lý giá ngày lễ | 3 |
| PB14 | Định giá | **Quản trị viên** | Cấu hình quy tắc định giá động (theo địa điểm, thời gian) | Tôi có thể điều chỉnh giá dựa trên nhu cầu | BIL03 | 2 | Medium | 2 | 2 | Mới | Hệ số tăng giá theo vị trí | 5 |
| PB14 | Định giá | **Khách hàng** | Xem chi tiết giá minh bạch trước khi thanh toán | Tôi có thể hiểu chính xác tôi đang trả gì | BIL04 | 1 | High | 2 | 2 | Mới | Hiển thị báo giá chi tiết từng dòng | 3 |
| PB14 | Định giá | **Khách hàng** | Nhận tính phí trả muộn khi trả xe trễ | Tôi có thể bị tính phí công bằng khi trả muộn | BIL05 | 1 | High | 2 | 2 | Mới | Tính phí trả muộn tự động | 3 |
| PB15 | Hội viên | **Hệ thống** | Tự động tính cấp hội viên dựa trên chi tiêu | Tôi có thể thưởng cho khách hàng trung thành | MEM01 | 2 | High | 2 | 2 | Mới | Cấp Bronze/Silver/Gold/Platinum/Diamond | 5 |
| PB15 | Hội viên | **Khách hàng** | Xem cấp hội viên và quyền lợi của tôi | Tôi có thể xem phần thưởng trung thành của mình | MEM02 | 2 | High | 2 | 2 | Mới | Bảng cấp hội viên, hiển thị quyền lợi | 2 |
| PB15 | Hội viên | **Hệ thống** | Tự động áp dụng giảm giá hội viên khi thanh toán | Tôi có thể được giảm giá vì là khách trung thành | MEM03 | 2 | High | 2 | 2 | Mới | Tự động giảm giá theo cấp | 3 |
| PB15 | Hội viên | **Khách hàng** | Tích lũy và đổi điểm trung thành | Tôi có thể tích lũy phần thưởng cho các lần đặt tương lai | MEM04 | 3 | Medium | 2 | 2 | Mới | Hệ thống điểm trung thành | 3 |
| PB16 | Tiền đặt cọc | **Khách hàng** | Thanh toán tiền đặt cọc hoàn tiền được khi đặt xe | Tôi có thể bảo đảm đơn đặt của mình bằng đặt cọc | DEP01 | 1 | High | 2 | 2 | Mới | Số tiền đặt cọc dựa trên cấp xe | 3 |
| PB16 | Tiền đặt cọc | **Hệ thống** | Hoàn tiền đặt cọc sau khi trả xe thành công | Tôi có thể hoàn tiền đặt cọc cho khách hàng kịp thời | DEP02 | 1 | High | 2 | 2 | Mới | Job hoàn tiền đặt cọc (chạy hàng ngày) | 3 |
| PB16 | Tiền đặt cọc | **Quản trị viên** | Giám sát tình trạng đặt cọc trên tất cả các đơn đặt | Tôi có thể theo dõi tất cả các khoản đặt cọc | DEP03 | 2 | Medium | 2 | 2 | Mới | Bảng theo dõi đặt cọc | 2 |

### Sprint 2.3 - Thuê dài hạn (Tuần 7-8)

| Backlog ID | Backlog | As a/an [Vai trò] | I want to [Mục tiêu] | So that [Lý do] | Story ID | Độ ưu tiên | Giá trị kinh doanh | SP | Sprint# | Trạng thái | Ghi chú | Story Points |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| PB17 | Thuê dài hạn | **Khách hàng** | Duyệt các gói thuê dài hạn (1 tuần đến 1 năm) | Tôi có thể tìm các tùy chọn dài hạn giá cả phải chăng | LT01 | 1 | High | 2 | 2 | Mới | Loại hợp đồng: hàng tuần, hàng tháng, hàng quý, hàng năm | 3 |
| PB17 | Thuê dài hạn | **Khách hàng** | Đặt xe với hợp đồng thuê dài hạn | Tôi có thể thuê xe trong thời gian dài | LT02 | 1 | High | 2 | 2 | Mới | Luồng đặt dài hạn (workflow F3) | 5 |
| PB17 | Thuê dài hạn | **Hệ thống** | Tạo lịch thanh toán hàng tháng cho thuê dài hạn | Tôi có thể quản lý thanh toán trả góp | LT03 | 1 | High | 2 | 2 | Mới | Tự động lên lịch: ngày đến hạn hàng tháng, số tiền | 5 |
| PB17 | Thuê dài hạn | **Khách hàng** | Xem và thanh toán các khoản trả góp hàng tháng | Tôi có thể quản lý thanh toán thuê dài hạn | LT04 | 1 | High | 2 | 2 | Mới | Bảng lịch thanh toán, PayOS | 3 |
| PB17 | Thuê dài hạn | **Hệ thống** | Gửi nhắc nhở thanh toán trước ngày đến hạn | Tôi có thể nhắc khách hàng thanh toán đúng hạn | LT05 | 2 | High | 2 | 2 | Mới | Nhắc trước 7 ngày, 3 ngày, 1 ngày | 3 |
| PB17 | Thuê dài hạn | **Quản trị viên** | Đánh dấu thanh toán quá hạn và áp dụng phạt | Tôi có thể quản lý các khoản thanh toán trễ | LT06 | 1 | High | 2 | 2 | Mới | Trạng thái quá hạn, tính phạt | 3 |
| PB17 | Thuê dài hạn | **Quản trị viên** | Quản lý hợp đồng thuê dài hạn | Tôi có thể giám sát tất cả các đơn thuê dài hạn | LT07 | 2 | Medium | 2 | 2 | Mới | Quản lý vòng đời hợp đồng | 3 |
| PB18 | Quản lý hợp đồng | **Khách hàng** | Ký hợp đồng thuê dài hạn số | Tôi có thể đồng ý hợp pháp với các điều khoản dài hạn | CON04 | 1 | High | 2 | 2 | Mới | Ký số nhiều bên | 3 |
| PB18 | Quản lý hợp đồng | **Đối tác** | Ký và phê duyệt hợp đồng thuê dài hạn | Tôi có thể phê duyệt thuê dài hạn trên xe của mình | CON05 | 1 | High | 2 | 2 | Mới | Luồng hợp đồng đối tác | 3 |
| PB18 | Quản lý hợp đồng | **Quản trị viên** | Xuất PDF của tất cả hợp đồng | Tôi có thể cung cấp bản cứng hợp đồng | CON06 | 2 | Medium | 2 | 2 | Mới | Xuất PDF cho tất cả loại hợp đồng | 5 |

---

## SPRINT 3 - Quản lý Khu vực, GPS & Tính năng Nâng cao

### Sprint 3.1 - Đa tenant theo Khu vực (Tuần 9-10)

| Backlog ID | Backlog | As a/an [Vai trò] | I want to [Mục tiêu] | So that [Lý do] | Story ID | Độ ưu tiên | Giá trị kinh doanh | SP | Sprint# | Trạng thái | Ghi chú | Story Points |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| PB19 | Quản lý khu vực | **Quản trị viên** | Tạo và quản lý các khu vực địa lý | Tôi có thể tổ chức hệ thống theo khu vực | AREA01 | 1 | High | 3 | 3 | Mới | Khu vực cấp tỉnh với lat/lng | 5 |
| PB19 | Quản lý khu vực | **Quản trị viên** | Phân công quản lý khu vực cho các khu vực | Tôi có thể ủy quyền trách nhiệm khu vực | AREA02 | 1 | High | 3 | 3 | Mới | Phân công quản lý khu vực | 2 |
| PB19 | Quản lý khu vực | **Quản trị viên** | Phân công nhân viên cho các khu vực | Tôi có thể tổ chức nhân viên theo khu vực | AREA03 | 1 | High | 3 | 3 | Mới | Ánh xạ nhân viên - khu vực | 2 |
| PB19 | Quản lý khu vực | **Quản lý khu vực** | Xem bảng điều khiển và thống kê dành riêng cho khu vực | Tôi có thể quản lý khu vực của mình một cách hiệu quả | AREA04 | 1 | High | 3 | 3 | Mới | KPI và báo cáo cấp khu vực | 3 |
| PB19 | Quản lý khu vực | **Quản trị viên** | Xem tất cả đơn đặt trong một khu vực cụ thể | Tôi có thể giám sát hiệu suất khu vực | AREA05 | 2 | Medium | 3 | 3 | Mới | Danh sách đơn đặt theo khu vực | 3 |
| PB19 | Quản lý khu vực | **Khách hàng** | Tìm kiếm xe trong một khu vực cụ thể | Tôi có thể tìm xe gần vị trí của mình | AREA06 | 2 | Medium | 3 | 3 | Mới | Lọc xe theo khu vực | 2 |
| PB20 | Quản lý nhân viên | **Quản trị viên** | Tạo và quản lý tài khoản nhân viên | Tôi có thể quản lý đội ngũ | STAFF01 | 1 | High | 3 | 3 | Mới | CRUD nhân viên với phân quyền | 2 |
| PB20 | Quản lý nhân viên | **Quản lý khu vực** | Xem và quản lý nhân viên trong khu vực của tôi | Tôi có thể quản lý các thành viên trong đội của mình | STAFF02 | 1 | High | 3 | 3 | Mới | Quản lý nhân viên cấp khu vực | 2 |
| PB20 | Quản lý nhân viên | **Nhân viên** | Xem đơn đặt và phân công dành riêng cho khu vực | Tôi có thể xem các công việc hàng ngày của mình | STAFF03 | 2 | Medium | 3 | 3 | Mới | Bảng điều khiển nhân viên | 2 |

### Sprint 3.2 - GPS & Theo dõi xe (Tuần 10-11)

| Backlog ID | Backlog | As a/an [Vai trò] | I want to [Mục tiêu] | So that [Lý do] | Story ID | Độ ưu tiên | Giá trị kinh doanh | SP | Sprint# | Trạng thái | Ghi chú | Story Points |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| PB21 | Theo dõi GPS | **Hệ thống** | Theo dõi vị trí xe theo thời gian thực (Lat, Lng, Tốc độ) | Tôi có thể giám sát tất cả xe | GPS01 | 1 | High | 3 | 3 | Mới | Nhập dữ liệu GPS từ thiết bị xe | 5 |
| PB21 | Theo dõi GPS | **Quản trị viên** | Xem bản đồ trực tiếp của tất cả xe | Tôi có thể xem vị trí xe trên bản đồ | GPS02 | 1 | High | 3 | 3 | Mới | Hiển thị bản đồ thời gian thực | 5 |
| PB21 | Theo dõi GPS | **Quản trị viên** | Xem lịch sử lộ trình của một xe | Tôi có thể điều tra chuyển động xe | GPS03 | 2 | High | 3 | 3 | Mới | Lịch sử lộ trình với trực quan hóa đường đi | 3 |
| PB21 | Theo dõi GPS | **Tài xế** | Xem lộ trình chuyến đi hiện tại của tôi | Tôi có thể điều hướng chuyến đi của mình | GPS04 | 2 | Medium | 3 | 3 | Mới | Hiển thị lộ trình trên ứng dụng tài xế | 3 |
| PB22 | Cảnh báo | **Hệ thống** | Phát hiện và tạo cảnh báo cho các sự kiện lệch tuyến | Tôi có thể phát hiện độ lệch xe | ALERT01 | 1 | High | 3 | 3 | Mới | Phát hiện cảnh báo lệch tuyến | 5 |
| PB22 | Cảnh báo | **Hệ thống** | Phát hiện và tạo cảnh báo cho việc trả xe quá hạn | Tôi có thể theo dõi các lần trả muộn | ALERT02 | 1 | High | 3 | 3 | Mới | Cảnh báo quá hạn khi thời gian trả đã qua | 3 |
| PB22 | Cảnh báo | **Hệ thống** | Phát hiện và tạo cảnh báo khi mất tín hiệu GPS | Tôi có thể phát hiện sự cố thiết bị | ALERT03 | 1 | High | 3 | 3 | Mới | Cảnh báo mất GPS | 3 |
| PB22 | Cảnh báo | **Hệ thống** | Phát hiện và tạo cảnh báo nguy cơ trộm cắp | Tôi có thể ngăn ngừa trộm xe | ALERT04 | 1 | High | 3 | 3 | Mới | Thuật toán phát hiện nguy cơ trộm | 5 |
| PB22 | Cảnh báo | **Quản trị viên** | Xem và quản lý tất cả cảnh báo xe | Tôi có thể phản ứng với các tình huống khẩn cấp xe | ALERT05 | 1 | High | 3 | 3 | Mới | Bảng cảnh báo với chức năng xác nhận | 3 |
| PB22 | Cảnh báo | **Đối tác** | Nhận thông báo cảnh báo cho xe của tôi | Tôi có thể phản ứng nhanh với các vấn đề | ALERT06 | 2 | Medium | 3 | 3 | Mới | Thông báo đẩy cho đối tác | 2 |

### Sprint 3.3 - Bảo dưỡng xe (Tuần 11-12)

| Backlog ID | Backlog | As a/an [Vai trò] | I want to [Mục tiêu] | So that [Lý do] | Story ID | Độ ưu tiên | Giá trị kinh doanh | SP | Sprint# | Trạng thái | Ghi chú | Story Points |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| PB23 | Bảo dưỡng | **Quản trị viên** | Lên lịch bảo dưỡng xe (thay dầu, kiểm tra) | Tôi có thể giữ xe trong tình trạng tốt | MAINT01 | 2 | High | 3 | 3 | Mới | Lên lịch bảo dưỡng với kích hoạt theo đồng hồ | 3 |
| PB23 | Bảo dưỡng | **Đối tác** | Xem và cập nhật lịch bảo dưỡng cho xe của tôi | Tôi có thể quản lý việc bảo dưỡng xe của mình | MAINT02 | 2 | Medium | 3 | 3 | Mới | Xem bảo dưỡng của đối tác | 2 |
| PB23 | Bảo dưỡng | **Hệ thống** | Gửi thông báo nhắc nhở bảo dưỡng | Tôi có thể nhắc đối tác bảo dưỡng xe | MAINT03 | 2 | Medium | 3 | 3 | Mới | Job hàng ngày lúc 8:00 sáng | 2 |
| PB23 | Bảo dưỡng | **Hệ thống** | Ghi nhận hồ sơ dịch vụ bảo dưỡng | Tôi có thể theo dõi lịch sử dịch vụ | MAINT04 | 3 | Medium | 3 | 3 | Mới | Theo dõi lịch sử dịch vụ | 2 |

---

## SPRINT 4 - Đánh giá, Hỗ trợ & Tính năng AI

### Sprint 4.1 - Đánh giá & Xếp hạng (Tuần 13)

| Backlog ID | Backlog | As a/an [Vai trò] | I want to [Mục tiêu] | So that [Lý do] | Story ID | Độ ưu tiên | Giá trị kinh doanh | SP | Sprint# | Trạng thái | Ghi chú | Story Points |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| PB24 | Đánh giá | **Khách hàng** | Đánh giá và nhận xét một đơn đặt đã hoàn thành | Tôi có thể chia sẻ trải nghiệm của mình | REV01 | 2 | High | 4 | 4 | Mới | 1-5 sao, bình luận văn bản | 3 |
| PB24 | Đánh giá | **Khách hàng** | Đánh giá tài xế cho chuyến đi của tôi | Tôi có thể phản hồi về dịch vụ tài xế | REV02 | 2 | Medium | 4 | 4 | Mới | Đánh giá tài xế riêng biệt với đánh giá xe | 2 |
| PB24 | Đánh giá | **Quản trị viên** | Phê duyệt hoặc từ chối các đánh giá đã gửi | Tôi có thể duy trì chất lượng đánh giá | REV03 | 2 | Medium | 4 | 4 | Mới | Hàng đợi kiểm duyệt đánh giá | 2 |
| PB24 | Đánh giá | **Khách hàng** | Xem đánh giá và xếp hạng trung bình của xe | Tôi có thể đưa ra quyết định đặt xe sáng suốt | REV04 | 2 | High | 4 | 4 | Mới | Hiển thị đánh giá trên trang chi tiết xe | 2 |
| PB24 | Đánh giá | **Khách hàng** | Xem đánh giá và xếp hạng trung bình của tài xế | Tôi có thể chọn tài xế đáng tin cậy | REV05 | 3 | Low | 4 | 4 | Mới | Xếp hạng tài xế trên hồ sơ | 2 |

### Sprint 4.2 - Hỗ trợ & Liên hệ (Tuần 13)

| Backlog ID | Backlog | As a/an [Vai trò] | I want to [Mục tiêu] | So that [Lý do] | Story ID | Độ ưu tiên | Giá trị kinh doanh | SP | Sprint# | Trạng thái | Ghi chú | Story Points |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| PB25 | Liên hệ | **Khách hàng** | Gửi tin nhắn liên hệ đến bộ phận hỗ trợ | Tôi có thể nhận trợ giúp cho các vấn đề của mình | SUP01 | 2 | High | 4 | 4 | Mới | Biểu mẫu liên hệ với chủ đề, tin nhắn | 2 |
| PB25 | Liên hệ | **Quản trị viên** | Xem và phản hồi tin nhắn liên hệ | Tôi có thể cung cấp hỗ trợ khách hàng | SUP02 | 1 | High | 4 | 4 | Mới | Quản lý liên hệ của admin | 2 |
| PB25 | Liên hệ | **Khách hàng** | Xem tin nhắn liên hệ đã gửi và phản hồi | Tôi có thể theo dõi các phiếu hỗ trợ của mình | SUP03 | 3 | Low | 4 | 4 | Mới | Lịch sử liên hệ | 1 |
| PB26 | Thông báo | **Khách hàng** | Nhận thông báo email khi trạng thái đơn đặt thay đổi | Tôi có thể cập nhật về các đơn đặt của mình | NOT01 | 2 | High | 4 | 4 | Mới | Đặt xe xác nhận, tài xế được phân công, v.v. | 3 |
| PB26 | Thông báo | **Khách hàng** | Nhận nhắc nhở SMS cho các đơn đặt sắp tới | Tôi có thể nhớ đến lấy xe của mình | NOT02 | 2 | Medium | 4 | 4 | Mới | Nhắc nhở SMS trước khi nhận xe | 3 |
| PB26 | Thông báo | **Đối tác** | Nhận thông báo cho các đơn đặt mới trên xe của tôi | Tôi có thể phản ứng nhanh với các đơn đặt | NOT03 | 2 | Medium | 4 | 4 | Mới | Cảnh báo đơn đặt mới | 2 |
| PB26 | Thông báo | **Tài xế** | Nhận thông báo cho các chuyến đi được phân công | Tôi có thể biết khi nào tôi có công việc mới | NOT04 | 1 | High | 4 | 4 | Mới | Thông báo phân công chuyến đi | 2 |

### Sprint 4.3 - AI Chat (Tuần 14)

| Backlog ID | Backlog | As a/an [Vai trò] | I want to [Mục tiêu] | So that [Lý do] | Story ID | Độ ưu tiên | Giá trị kinh doanh | SP | Sprint# | Trạng thái | Ghi chú | Story Points |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| PB27 | AI Chat | **Khách hàng** | Trò chuyện với trợ lý AI để được hỗ trợ và FAQ | Tôi có thể nhận câu trả lời tức thì cho các câu hỏi | AI01 | 2 | Medium | 4 | 4 | Mới | Groq API (miễn phí) với dự phòng Gemini | 5 |
| PB27 | AI Chat | **Quản trị viên** | Giám sát các phiên trò chuyện và phản hồi AI | Tôi có thể xem lại hiệu suất AI | AI02 | 3 | Low | 4 | 4 | Mới | Lịch sử phiên trò chuyện | 2 |
| PB27 | AI Chat | **Khách hàng** | Xem hướng dẫn trợ giúp và FAQ | Tôi có thể tìm câu trả lời một mình | AI03 | 3 | Low | 4 | 4 | Mới | GuideController, tìm kiếm trợ giúp | 3 |
| PB28 | Hệ thống trợ giúp | **Khách hàng** | Truy cập hướng dẫn sử dụng cho từng vai trò | Tôi có thể tìm hiểu cách sử dụng hệ thống | HELP01 | 3 | Low | 4 | 4 | Mới | Hướng dẫn admin, khách hàng, đối tác, tài xế | 2 |

---

## SPRINT 5 - Báo cáo, Sao lưu & Quản trị Nâng cao

### Sprint 5.1 - Báo cáo & Phân tích (Tuần 15)

| Backlog ID | Backlog | As a/an [Vai trò] | I want to [Mục tiêu] | So that [Lý do] | Story ID | Độ ưu tiên | Giá trị kinh doanh | SP | Sprint# | Trạng thái | Ghi chú | Story Points |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| PB29 | Bảng điều khiển admin | **Quản trị viên** | Xem bảng điều khiển toàn hệ thống với KPI | Tôi có thể giám sát tình trạng hệ thống | DASH01 | 1 | High | 5 | 5 | Mới | Doanh thu, đơn đặt, xe hoạt động, người dùng | 5 |
| PB29 | Bảng điều khiển admin | **Quản trị viên** | Xem báo cáo doanh thu theo khoảng thời gian | Tôi có thể phân tích hiệu suất tài chính | DASH02 | 1 | High | 5 | 5 | Mới | Doanh thu hàng ngày, hàng tuần, hàng tháng, hàng năm | 3 |
| PB29 | Bảng điều khiển admin | **Quản trị viên** | Xem thống kê đơn đặt (tổng, theo trạng thái, theo khu vực) | Tôi có thể theo dõi các số liệu vận hành | DASH03 | 2 | High | 5 | 5 | Mới | Bảng phân tích đơn đặt | 3 |
| PB29 | Bảng điều khiển admin | **Quản lý khu vực** | Xem thống kê dành riêng cho khu vực | Tôi có thể quản lý các số liệu khu vực của mình | DASH04 | 2 | Medium | 5 | 5 | Mới | Doanh thu và báo cáo đơn đặt cấp khu vực | 3 |
| PB30 | Báo cáo | **Quản trị viên** | Xuất báo cáo ra PDF/Excel | Tôi có thể chia sẻ báo cáo với các bên liên quan | REP01 | 3 | Medium | 5 | 5 | Mới | Xuất PDF và Excel cho tất cả báo cáo | 5 |
| PB30 | Báo cáo | **Quản trị viên** | Xem báo cáo hiệu suất đối tác | Tôi có thể đánh giá hiệu suất đối tác | REP02 | 3 | Medium | 5 | 5 | Mới | Doanh thu theo đối tác, theo xe | 3 |
| PB30 | Báo cáo | **Quản trị viên** | Xem phân tích khách hàng (tiếp cận, giữ chân, LTV) | Tôi có thể hiểu hành vi khách hàng | REP03 | 3 | Medium | 5 | 5 | Mới | Phân tích vòng đời khách hàng | 3 |
| PB31 | Bảng tham chiếu | **Quản trị viên** | Quản lý hãng xe, loại xe và trạng thái | Tôi có thể cấu hình danh mục xe | LOOK01 | 2 | Medium | 5 | 5 | Mới | CRUD hãng, loại, trạng thái | 2 |
| PB31 | Bảng tham chiếu | **Quản trị viên** | Quản lý trạng thái đơn đặt | Tôi có thể cấu hình quy trình công việc đặt xe | LOOK02 | 2 | Medium | 5 | 5 | Mới | Cấu hình quy trình trạng thái | 1 |

### Sprint 5.2 - Sao lưu & Khôi phục Database (Tuần 15-16)

| Backlog ID | Backlog | As a/an [Vai trò] | I want to [Mục tiêu] | So that [Lý do] | Story ID | Độ ưu tiên | Giá trị kinh doanh | SP | Sprint# | Trạng thái | Ghi chú | Story Points |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| PB32 | Sao lưu | **Hệ thống** | Tạo ảnh chụp database tự động hàng ngày | Tôi có thể khôi phục theo thời điểm | BACK01 | 1 | High | 5 | 5 | Mới | Tự động tạo ảnh với cấu hình giữ lại | 5 |
| PB32 | Sao lưu | **Quản trị viên** | Xem danh sách tất cả ảnh chụp database | Tôi có thể quản lý các tệp sao lưu | BACK02 | 1 | High | 5 | 5 | Mới | Danh sách ảnh với kích thước, số dòng, trạng thái | 2 |
| PB32 | Sao lưu | **Quản trị viên** | Khôi phục database từ ảnh chụp | Tôi có thể phục hồi từ mất dữ liệu | BACK03 | 1 | High | 5 | 5 | Mới | Khả năng khôi phục theo thời điểm | 8 |
| PB32 | Sao lưu | **Quản trị viên** | Tạo sao lưu thủ công theo yêu cầu | Tôi có thể tạo sao lưu trước các thay đổi lớn | BACK04 | 2 | Medium | 5 | 5 | Mới | Kích hoạt tạo ảnh thủ công | 2 |
| PB33 | Quản lý dữ liệu | **Quản trị viên** | Xem kích thước dữ liệu và số dòng mỗi bảng | Tôi có thể giám sát tình trạng database | DATA01 | 2 | Medium | 5 | 5 | Mới | Bảng thống kê database | 2 |
| PB33 | Quản lý dữ liệu | **Hệ thống** | Dọn dẹp các mục giỏ hàng và phiên đã hết hạn | Tôi có thể duy trì hiệu suất database | DATA02 | 3 | Low | 5 | 5 | Mới | Job dọn dẹp theo lịch trình | 2 |

---

## SPRINT 6 - SMS, Marketing & Hoàn thiện

### Sprint 6.1 - SMS & Marketing (Tuần 17)

| Backlog ID | Backlog | As a/an [Vai trò] | I want to [Mục tiêu] | So that [Lý do] | Story ID | Độ ưu tiên | Giá trị kinh doanh | SP | Sprint# | Trạng thái | Ghi chú | Story Points |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| PB34 | Quản lý SMS | **Quản trị viên** | Cấu hình chiến dịch SMS (lên lịch, nội dung, người nhận) | Tôi có thể gửi tin nhắn có mục tiêu | SMS01 | 2 | Medium | 6 | 6 | Mới | SMS được lên lịch với quản lý mẫu | 5 |
| PB34 | Quản lý SMS | **Quản trị viên** | Gửi SMS nhắc nhở bảo dưỡng cho khách hàng | Tôi có thể nhắc khách hàng về dịch vụ xe | SMS02 | 2 | Medium | 6 | 6 | Mới | Nhắc nhở bảo dưỡng tự động | 3 |
| PB34 | Quản lý SMS | **Quản trị viên** | Gửi SMS chúc mừng sinh nhật cho khách hàng | Tôi có thể xây dựng lòng trung thành khách hàng | SMS03 | 3 | Medium | 6 | 6 | Mới | Chiến dịch sinh nhật | 2 |
| PB34 | Quản lý SMS | **Quản trị viên** | Xem lịch sử chiến dịch SMS và tình trạng gửi | Tôi có thể theo dõi việc gửi SMS | SMS04 | 3 | Low | 6 | 6 | Mới | Theo dõi lịch sử SMS | 2 |
| PB35 | Khuyến mãi | **Quản trị viên** | Tạo và quản lý chiến dịch khuyến mãi | Tôi có thể chạy các chiến dịch marketing | PROMO01 | 2 | High | 6 | 6 | Mới | Quản lý mã khuyến mãi (giảm %, số tiền tối đa) | 3 |
| PB35 | Khuyến mãi | **Quản trị viên** | Đặt thời gian hiệu lực và giới hạn sử dụng khuyến mãi | Tôi có thể kiểm soát việc sử dụng khuyến mãi | PROMO02 | 2 | Medium | 6 | 6 | Mới | Theo dõi ValidFrom/Until, UsageLimit | 2 |
| PB35 | Khuyến mãi | **Khách hàng** | Áp dụng mã khuyến mãi khi thanh toán | Tôi có thể được giảm giá | PROMO03 | 2 | High | 6 | 6 | Mới | Nhập và xác thực mã khuyến mãi | 2 |

### Sprint 6.2 - Nhập/Xuất & Dữ liệu khách hàng (Tuần 17-18)

| Backlog ID | Backlog | As a/an [Vai trò] | I want to [Mục tiêu] | So that [Lý do] | Story ID | Độ ưu tiên | Giá trị kinh doanh | SP | Sprint# | Trạng thái | Ghi chú | Story Points |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| PB36 | Dữ liệu khách hàng | **Quản trị viên** | Nhập dữ liệu khách hàng từ file Excel | Tôi có thể nhập hàng loạt thông tin khách hàng | IMP01 | 2 | High | 6 | 6 | Mới | Nhập Excel với xác thực | 5 |
| PB36 | Dữ liệu khách hàng | **Quản trị viên** | Xuất danh sách khách hàng ra Excel | Tôi có thể chia sẻ dữ liệu khách hàng với các bên liên quan | EXP01 | 3 | Medium | 6 | 6 | Mới | Xuất khách hàng với bộ lọc | 3 |
| PB36 | Dữ liệu khách hàng | **Quản trị viên** | In danh sách khách hàng có sinh nhật | Tôi có thể chuẩn bị các chiến dịch sinh nhật | EXP02 | 3 | Medium | 6 | 6 | Mới | Báo cáo sinh nhật cho marketing | 2 |
| PB37 | Voucher | **Quản trị viên** | Tạo mã voucher cho khuyến mãi | Tôi có thể phát hành voucher khuyến mãi | VOC01 | 2 | Medium | 6 | 6 | Mới | CRUD voucher kèm ngày hết hạn | 3 |
| PB37 | Voucher | **Khách hàng** | Đổi mã voucher khi thanh toán | Tôi có thể sử dụng voucher để được giảm giá | VOC02 | 2 | Medium | 6 | 6 | Mới | Xác thực voucher | 2 |

### Sprint 6.3 - Sửa lỗi & Hoàn thiện (Tuần 18)

| Backlog ID | Backlog | As a/an [Vai trò] | I want to [Mục tiêu] | So that [Lý do] | Story ID | Độ ưu tiên | Giá trị kinh doanh | SP | Sprint# | Trạng thái | Ghi chú | Story Points |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| PB38 | Hoàn thiện | **Hệ thống** | Tối ưu hóa hiệu suất và kiểm tra tải | Tôi có thể đảm bảo hệ thống xử lý được lưu lượng truy cập | POL01 | 2 | High | 6 | 6 | Mới | Redis caching, tối ưu hóa truy vấn | 5 |
| PB38 | Hoàn thiện | **Khách hàng** | Giao diện responsive trên di động cho tất cả các trang | Tôi có thể sử dụng hệ thống trên thiết bị di động | POL02 | 2 | Medium | 6 | 6 | Mới | Tối ưu hóa di động | 5 |
| PB38 | Hoàn thiện | **Quản trị viên** | Tối ưu hóa SEO cho các trang công khai | Tôi có thể cải thiện khả năng hiển thị trên công cụ tìm kiếm | POL03 | 3 | Low | 6 | 6 | Mới | Thẻ meta, sitemap, robots.txt | 2 |
| PB38 | Hoàn thiện | **Hệ thống** | Kiểm thử end-to-end toàn diện | Tôi có thể đảm bảo chất lượng trước khi phát hành | POL04 | 1 | High | 6 | 6 | Mới | Kiểm thử E2E Cypress cho các luồng quan trọng | 8 |

---

## TÓM TẮT LÊN KẾ HOẠCH SPRINT

### Sprint 1 - Nền tảng & Luồng Đặt Xe Cốt Lõi (~124 SP)
| PBI | SP | Trọng tâm |
|---|---|---|
| PB01 - PB03 (Xác thực, Bảo mật, eKYC) | 28 | Nền tảng xác thực và bảo mật |
| PB04 - PB05 (Danh mục xe, Trạng thái) | 25 | Quản lý xe |
| PB06 - PB08 (Đặt xe, Giỏ hàng, Hợp đồng) | 44 | Luồng đặt xe cốt lõi |
| PB09 - PB10 (Tài xế, Bàn giao) | 27 | Quản lý tài xế và bàn giao |
| **Tổng Sprint 1** | **~124 SP** | |

### Sprint 2 - Thanh toán, Hóa đơn & Thuê dài hạn (~99 SP)
| PBI | SP | Trọng tâm |
|---|---|---|
| PB11 - PB13 (Thanh toán, Ví, Hóa đơn) | 31 | Giao dịch tài chính |
| PB14 - PB16 (Định giá, Hội viên, Đặt cọc) | 38 | Công cụ tính giá |
| PB17 - PB18 (Thuê dài hạn, Quản lý hợp đồng) | 30 | Thuê dài hạn |
| **Tổng Sprint 2** | **~99 SP** | |

### Sprint 3 - Quản lý Khu vực, GPS & Tính năng Nâng cao (~60 SP)
| PBI | SP | Trọng tâm |
|---|---|---|
| PB19 - PB20 (Khu vực, Nhân viên) | 19 | Đa tenant theo khu vực |
| PB21 - PB22 (GPS, Cảnh báo) | 30 | Theo dõi xe |
| PB23 (Bảo dưỡng) | 11 | Bảo dưỡng xe |
| **Tổng Sprint 3** | **~60 SP** | |

### Sprint 4 - Đánh giá, Hỗ trợ & AI (~37 SP)
| PBI | SP | Trọng tâm |
|---|---|---|
| PB24 (Đánh giá) | 11 | Hệ thống xếp hạng |
| PB25 - PB26 (Hỗ trợ, Thông báo) | 14 | Hỗ trợ khách hàng |
| PB27 - PB28 (AI Chat, Trợ giúp) | 12 | Hỗ trợ bằng AI |
| **Tổng Sprint 4** | **~37 SP** | |

### Sprint 5 - Báo cáo, Sao lưu & Quản trị Nâng cao (~47 SP)
| PBI | SP | Trọng tâm |
|---|---|---|
| PB29 - PB30 (Bảng điều khiển, Báo cáo) | 25 | Phân tích |
| PB31 (Bảng tham chiếu) | 5 | Cấu hình |
| PB32 - PB33 (Sao lưu, Quản lý dữ liệu) | 17 | Hạ tầng |
| **Tổng Sprint 5** | **~47 SP** | |

### Sprint 6 - SMS, Marketing & Hoàn thiện (~52 SP)
| PBI | SP | Trọng tâm |
|---|---|---|
| PB34 - PB35 (SMS, Khuyến mãi) | 17 | Marketing |
| PB36 - PB37 (Nhập/Xuất, Voucher) | 15 | Quản lý dữ liệu |
| PB38 (Hoàn thiện) | 20 | Tối ưu hóa và kiểm thử |
| **Tổng Sprint 6** | **~52 SP** | |

---

## ĐỊNH NGHĨA SẴN SÀNG (DoR)

- [ ] User story có tiêu chí chấp nhận rõ ràng (định dạng Given/When/Then)
- [ ] Phương pháp kỹ thuật đã được ghi lại (thiết kế API, thay đổi mô hình dữ liệu)
- [ ] Các phụ thuộc đã được xác định và ước tính
- [ ] Mockup thiết kế/UI đã được phê duyệt bởi các bên liên quan
- [ ] Các trường hợp kiểm thử đã được xác định cho các luồng quan trọng

## ĐỊNH NGHĨA HOÀN THÀNH (DoD)

- [ ] Mã nguồn biên dịch không có lỗi hoặc cảnh báo
- [ ] Các bài kiểm thử đơn vị bao phủ logic cốt lõi (>80% mục tiêu)
- [ ] Được chấp thuận bởi ít nhất 1 đồng nghiệp
- [ ] Tất cả tiêu chí chấp nhận được đáp ứng
- [ ] Đã triển khai lên môi trường Phát triển
- [ ] QA đã kiểm thử và phê duyệt
- [ ] Tài liệu đã được cập nhật (tài liệu API, hướng dẫn sử dụng)
- [ ] Không có lỗi nghiêm trọng hoặc mức cao mở

---

## CÁC VAI TRÒ TRONG HỆ THỐNG

| Vai trò | Mô tả | Cấp độ truy cập |
|---|---|---|
| `Quản trị viên` | Quản trị viên hệ thống | Toàn quyền truy cập tất cả modules |
| `Quản lý khu vực` | Quản lý cấp khu vực | Truy cập giới hạn theo khu vực |
| `Nhân viên` | Nhân viên vận hành | Truy cập giới hạn cấp khu vực |
| `Khách hàng` | Khách hàng cuối | Tự đặt xe, hồ sơ, lịch sử |
| `Đối tác` | Đối tác cho thuê xe | Quản lý xe, thu nhập của mình |
| `Tài xế` | Tài xế được phân công | Chỉ phân công chuyến đi |

---

## CÁC LUỒNG CÔNG VIỆC CHÍNH (F1-F7)

| Workflow ID | Tên | Mô tả |
|---|---|---|
| F1 | Đặt xe bình thường | Tìm kiếm > Thanh toán > Đặt cọc PayOS > Bàn giao > Hoàn thành |
| F2 | Giỏ hàng nhiều xe | Thêm nhiều xe > Thanh toán giỏ hàng > PayOS > Nhiều đơn đặt |
| F3 | Thuê dài hạn | Chọn gói > Hợp đồng > Kiểm tra > Đặt cọc > Thanh toán hàng tháng |
| F4 | eKYC FPT | Quét CCCD > OCR FPT > Đối sánh khuôn mặt > Phê duyệt admin |
| F5 | Đăng ký đối tác | Đăng ký > Tải tài liệu > Phê duyệt admin > Ký hợp đồng |
| F6 | Quản lý khu vực | Khu vực > Địa điểm > Nhân viên > Bảng điều khiển > Báo cáo |
| F7 | AI Chat | Phiên trò chuyện > Groq API > Phản hồi AI > Lịch sử phiên |
