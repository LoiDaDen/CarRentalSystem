# AGENT.md

> Tài liệu mô tả toàn bộ hệ thống vận hành, vai trò, nguyên tắc, quy trình làm việc và tiêu chuẩn chất lượng cho AI agent trong dự án này.
>
> Mục tiêu của file này là giúp bất kỳ AI trợ lý nào khi đọc vào đều có thể hiểu nhanh: dự án là gì, cần làm gì, làm như thế nào, ưu tiên ra sao, và những giới hạn nào phải tuân thủ.

---

## 1. Mục đích của hệ thống

Hệ thống này là một **Car Rental System** dùng để quản lý quy trình thuê xe, bao gồm:

- Quản lý khách hàng
- Quản lý xe
- Quản lý đặt xe / booking
- Quản lý hợp đồng thuê
- Quản lý tài xế nếu có dịch vụ đi kèm
- Quản lý thanh toán / hóa đơn
- Quản lý khuyến mãi / mã giảm giá
- Quản lý bảo trì, tình trạng xe, lịch sử sử dụng
- Báo cáo và thống kê

Mục tiêu chính của AI agent khi làm việc với hệ thống này là:

1. Hiểu đúng nghiệp vụ thuê xe
2. Giữ kiến trúc sạch, dễ mở rộng
3. Sửa lỗi cẩn thận, không phá vỡ chức năng cũ
4. Ưu tiên tính đúng, tính ổn định và khả năng bảo trì
5. Viết code rõ ràng, dễ đọc, dễ kiểm thử

---

## 2. Phạm vi hỗ trợ của AI agent

AI agent có thể hỗ trợ các công việc sau:

- Phân tích codebase
- Giải thích luồng nghiệp vụ
- Sửa lỗi
- Refactor code
- Thêm tính năng mới
- Viết hoặc cập nhật tài liệu
- Kiểm tra logic, luồng dữ liệu, tính nhất quán
- Đề xuất cách tổ chức module, service, controller, view, repository
- Hỗ trợ tạo test cases nếu dự án có test
- Hỗ trợ rà soát linter / build error / runtime issue

AI agent **không** nên:

- Tự ý xóa chức năng quan trọng
- Thay đổi kiến trúc lớn nếu chưa có yêu cầu rõ ràng
- Giả định nghiệp vụ khi chưa có dữ liệu trong codebase
- Viết code phức tạp không cần thiết
- Tạo thêm file mới nếu chưa thật sự cần
- Ghi đè quy ước hiện có nếu dự án đã có pattern ổn định

---

## 3. Nguyên tắc làm việc cốt lõi

### 3.1. Hiểu trước khi sửa

Trước khi chỉnh sửa, AI phải:

- Đọc file liên quan
- Hiểu cấu trúc dự án
- Tìm cách các phần đang tương tác với nhau
- Xác định tác động phụ có thể xảy ra

Không nên sửa “theo cảm tính”.

### 3.2. Ưu tiên an toàn

Khi thay đổi code:

- Giữ nguyên hành vi nếu không có yêu cầu thay đổi
- Chỉ sửa đúng phần cần sửa
- Tránh đụng tới nhiều file nếu chưa cần
- Nếu sửa logic có thể ảnh hưởng hệ thống, cần kiểm tra kỹ hơn

### 3.3. Giữ tính nhất quán

Mọi chỉnh sửa phải phù hợp với:

- Cấu trúc dự án hiện có
- Naming convention đang dùng
- Kiến trúc hiện tại
- Style code hiện hữu
- Cách xử lý lỗi hiện hữu

### 3.4. Tối giản nhưng đầy đủ

Giải pháp nên:

- Đủ để giải quyết vấn đề
- Không dư thừa
- Dễ bảo trì
- Dễ mở rộng về sau

---

## 4. Kiến trúc nghiệp vụ tổng quan

Hệ thống thuê xe thường có các thực thể chính sau:

- `Customer` — khách hàng
- `Car` — xe
- `Booking` — đặt xe
- `Rental` — hợp đồng thuê / phiếu thuê
- `Driver` — tài xế
- `Payment` — thanh toán
- `Invoice` — hóa đơn
- `Maintenance` — bảo trì
- `Promotion` — khuyến mãi
- `User` / `Role` — tài khoản và phân quyền

### 4.1. Luồng nghiệp vụ điển hình

1. Khách hàng đăng ký hoặc đăng nhập
2. Khách chọn xe theo thời gian, địa điểm, loại xe
3. Hệ thống kiểm tra tình trạng xe và lịch rảnh
4. Tạo booking
5. Xác nhận booking
6. Tạo hợp đồng thuê / rental
7. Xử lý thanh toán
8. Cập nhật trạng thái xe
9. Kết thúc thuê
10. Tính phí phát sinh nếu có
11. Lưu lịch sử và báo cáo

### 4.2. Luồng trạng thái phổ biến của xe

Một xe thường có các trạng thái như:

- Available
- Reserved
- Rented
- Maintenance
- Unavailable

AI agent phải luôn kiểm tra logic chuyển trạng thái để tránh:

- Cho thuê xe đang bảo trì
- Booking trùng lịch
- Xác nhận thuê khi xe không còn sẵn sàng
- Reset trạng thái sai khi trả xe

---

## 5. Quy tắc nghiệp vụ quan trọng

### 5.1. Booking và lịch thuê

- Không cho phép booking chồng lên cùng một khoảng thời gian nếu xe không còn rảnh
- Nếu booking chưa xác nhận, cần xác định rõ thời hạn giữ chỗ
- Booking hết hạn phải có cơ chế hủy / nhả xe

### 5.2. Tính giá thuê

Khi tính giá cần lưu ý:

- Giá theo ngày / giờ / km / gói
- Phí bảo hiểm nếu có
- Phí tài xế nếu có
- Phí giao nhận xe nếu có
- Phí vượt giới hạn km
- Phí phạt trễ hạn trả xe
- Phí hư hỏng / vệ sinh / nhiên liệu

### 5.3. Trạng thái thanh toán

Thanh toán có thể ở các trạng thái:

- Pending
- Paid
- Partial
- Failed
- Refunded
- Cancelled

### 5.4. Bảo trì xe

Khi xe được đưa vào bảo trì:

- Không được đặt mới
- Các booking liên quan phải được xử lý rõ ràng
- Phải có lịch sử bảo trì
- Cần ghi nhận lý do và thời gian dự kiến hoàn thành

### 5.5. Phân quyền

Phân quyền thường gồm:

- Admin
- Manager
- Staff
- Customer
- Driver

Mỗi vai trò có quyền khác nhau. AI không được tự ý mở quyền nếu không có yêu cầu.

---

## 6. Chuẩn làm việc khi sửa code

### 6.1. Trước khi sửa

- Đọc file liên quan
- Tìm nơi gọi hàm / component / service đó
- Xác định input, output, side effects
- Kiểm tra linter / lỗi hiện có nếu cần

### 6.2. Khi sửa

- Giữ thay đổi tối thiểu
- Không viết lại toàn bộ file nếu chỉ cần sửa một phần
- Ưu tiên chỉnh sửa đúng chỗ thay vì tạo workaround
- Tránh trùng logic

### 6.3. Sau khi sửa

- Kiểm tra lỗi cú pháp
- Kiểm tra linter nếu workspace có cấu hình
- Rà lại tác động liên quan
- Nếu có thể, đề xuất cách test

---

## 7. Quy ước về code quality

### 7.1. Tên biến và hàm

- Tên rõ nghĩa
- Không viết tắt khó hiểu
- Phản ánh đúng chức năng

### 7.2. Cấu trúc hàm

- Một hàm nên làm một việc chính
- Không nên quá dài nếu có thể tách nhỏ
- Tránh lồng điều kiện quá sâu

### 7.3. Xử lý lỗi

- Báo lỗi rõ ràng
- Không nuốt lỗi âm thầm
- Không để trạng thái dữ liệu bị cập nhật nửa chừng
- Nếu cần, dùng transaction / rollback phù hợp với hệ thống

### 7.4. Kiểm thử

Nếu có test framework, nên ưu tiên:

- Test luồng nghiệp vụ chính
- Test validation
- Test trường hợp biên
- Test lỗi và ngoại lệ

---

## 8. Quy trình làm việc chuẩn cho AI agent

### Bước 1. Xác định mục tiêu

Hiểu rõ user muốn gì:

- Fix bug
- Thêm feature
- Viết tài liệu
- Refactor
- Tạo file mới
- Giải thích code

### Bước 2. Khảo sát codebase

Tìm file liên quan, ví dụ:

- Controller
- Service
- Repository
- Model
- View
- Component
- Config
- Migration
- Seed
- Test

### Bước 3. Phân tích tác động

Xác định:

- File nào bị ảnh hưởng
- Chỗ nào đang gọi tới logic đó
- Có cần cập nhật UI / API / database không
- Có rủi ro gì không

### Bước 4. Thực hiện thay đổi

Chỉnh sửa từng phần rõ ràng, có mục tiêu.

### Bước 5. Kiểm tra lại

- Soát logic
- Rà lint
- Đọc lại code sau khi sửa
- Nếu có lỗi, sửa ngay

### Bước 6. Báo cáo ngắn gọn

Khi hoàn tất, tóm tắt:

- Đã làm gì
- File nào thay đổi
- Có điểm nào cần người dùng lưu ý

---

## 9. Quy tắc khi tạo tài liệu

Khi AI tạo tài liệu cho dự án:

- Viết tiếng Việt rõ ràng nếu user dùng tiếng Việt
- Cấu trúc có heading rõ ràng
- Có mục lục nếu tài liệu dài
- Dùng ví dụ thực tế theo nghiệp vụ thuê xe
- Nếu tài liệu cho hệ thống thì phải bám sát nghiệp vụ của hệ thống
- Không viết lan man ngoài phạm vi dự án

Các tài liệu có thể bao gồm:

- Tổng quan hệ thống
- Hướng dẫn cho admin
- Hướng dẫn cho customer
- Hướng dẫn cho partner
- Hướng dẫn cho driver
- Hướng dẫn cho manager/staff
- Sơ đồ nghiệp vụ
- Database overview
- API overview
- Release notes

---

## 10. Quy tắc khi tạo hoặc sửa UI

Nếu dự án có giao diện web:

- Ưu tiên bố cục rõ ràng, sạch sẽ
- Form phải dễ nhập, dễ hiểu
- Trạng thái lỗi phải hiển thị rõ
- Danh sách phải dễ lọc, dễ tìm kiếm
- Hành động quan trọng cần xác nhận trước khi thực hiện
- Cần có thông báo thành công / thất bại
- Responsive nếu dự án có yêu cầu hiển thị trên nhiều thiết bị

### UI ưu tiên cho hệ thống thuê xe

- Trang quản lý xe
- Trang booking
- Trang lịch sử thuê
- Trang thanh toán
- Trang quản trị người dùng
- Trang dashboard thống kê

---

## 11. Tiêu chí chất lượng cho các thay đổi

Một thay đổi tốt nên đạt các tiêu chí:

- Đúng nghiệp vụ
- Đúng phạm vi yêu cầu
- Không phá vỡ chức năng đang có
- Dễ hiểu
- Dễ bảo trì
- Có thể kiểm tra lại
- Phù hợp kiến trúc dự án

---

## 12. Những lỗi cần tránh

AI agent cần tránh:

- Suy đoán business logic không có trong code
- Viết lại toàn bộ hệ thống khi chỉ cần chỉnh nhỏ
- Tạo file không cần thiết
- Đổi tên biến / class hàng loạt nếu không có lý do
- Bỏ qua các file liên quan
- Tối ưu quá mức khi chưa cần
- Dùng giải pháp phức tạp cho bài toán đơn giản
- Làm thay đổi vượt khỏi yêu cầu người dùng

---

## 13. Mẫu suy nghĩ khi xử lý yêu cầu

Khi gặp một task, AI nên tự kiểm tra:

1. User muốn gì chính xác?
2. Chức năng này liên quan đến phần nào của hệ thống?
3. File nào cần đọc trước?
4. Thay đổi nào là tối thiểu nhưng đủ?
5. Có rủi ro gì về dữ liệu, trạng thái, UI, API?
6. Sau khi sửa có cần kiểm tra gì?

---

## 14. Cách ưu tiên khi có nhiều việc

Ưu tiên theo thứ tự:

1. Lỗi gây hỏng chức năng chính
2. Lỗi dữ liệu / trạng thái
3. Vấn đề nghiệp vụ quan trọng
4. Cải thiện trải nghiệm người dùng
5. Refactor và tối ưu
6. Làm đẹp tài liệu / code cleanup

---

## 15. Cách phản hồi với người dùng

Phản hồi nên:

- Ngắn gọn nhưng đủ ý
- Rõ ràng, không mơ hồ
- Nếu có thay đổi file, nói rõ file nào
- Nếu có rủi ro, nói trước
- Nếu cần thêm thông tin mới, hỏi đúng câu hỏi cần thiết

---

## 16. Hướng dẫn mở rộng hệ thống trong tương lai

Nếu dự án phát triển thêm, có thể mở rộng theo các hướng:

- Tích hợp thanh toán online
- Thông báo email / SMS / push notification
- Định giá linh hoạt theo mùa / giờ cao điểm
- Hệ thống loyalty / membership
- Gợi ý xe theo nhu cầu khách hàng
- Theo dõi GPS / vị trí xe
- Báo cáo nâng cao theo chi nhánh
- Tích hợp hợp đồng điện tử
- Quản lý tài xế nâng cao
- Tích hợp AI gợi ý lịch thuê

Khi mở rộng, cần giữ:

- Tính tương thích ngược
- Tách layer rõ ràng
- Tính bảo mật dữ liệu
- Dễ kiểm thử

---

## 17. Checklist nhanh cho AI agent

Trước khi kết thúc một tác vụ, hãy tự kiểm tra:

- [ ] Đã hiểu đúng yêu cầu của user
- [ ] Đã đọc file liên quan
- [ ] Đã sửa đúng chỗ cần sửa
- [ ] Đã tránh phá vỡ logic cũ
- [ ] Đã kiểm tra lỗi cơ bản
- [ ] Đã báo cáo kết quả rõ ràng

---

## 18. Kết luận

Tài liệu này đóng vai trò như **hướng dẫn vận hành tổng thể** cho AI agent khi làm việc với hệ thống thuê xe.

Nguyên tắc quan trọng nhất là:

- Hiểu rõ nghiệp vụ
- Sửa đúng vấn đề
- Giữ hệ thống ổn định
- Tôn trọng cấu trúc hiện có
- Ưu tiên tính rõ ràng, an toàn và dễ bảo trì

Nếu cần, có thể mở rộng file này thành:

- `agent.md` theo chuẩn dự án
- `CONTRIBUTING.md`
- `README.md`
- Bộ prompt nội bộ cho AI coding assistant
- Bộ quy tắc review code / PR checklist
