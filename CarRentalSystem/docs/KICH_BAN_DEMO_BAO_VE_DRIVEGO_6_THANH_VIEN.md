# Kịch bản bảo vệ DriveGo 10 phút cho 6 thành viên

## 1. Thông điệp duy nhất

> DriveGo là nền tảng marketplace kết nối chủ xe có xe nhàn rỗi với khách cần thuê xe tự lái. Nền tảng kiểm soát vòng đời giao dịch và tạo doanh thu bằng hoa hồng 20% trên booking hoàn thành.

Không trình diễn hệ thống theo danh sách menu. Toàn bộ bài bảo vệ phải trả lời ba câu:

1. DriveGo giải quyết vấn đề gì cho chủ xe và khách thuê?
2. Hệ thống tạo và kiểm soát giao dịch như thế nào?
3. Nhóm sẽ đo và đạt KPI sáu tháng bằng cách nào?

## 2. KPI chính thức và phép tính bắt buộc

| KPI | Định nghĩa dùng khi bảo vệ | Mục tiêu sáu tháng |
|---|---|---:|
| Người dùng đăng ký | Tài khoản được tạo trong kỳ | 100 |
| Xe đăng cho thuê | Xe tự lái đã duyệt, không bị xóa | 30 |
| Đơn thuê phát sinh | Booking hợp lệ, loại `Draft` và `Unpaid` | Tối thiểu 50; kế hoạch vận hành 80 |
| Giao dịch thành công | `Completed / (Completed + Cancelled + Rejected + Expired)` | Từ 70% |
| GMV | Tổng `TotalPrice` của booking `Completed` | 250 triệu đồng |
| Doanh thu DriveGo | Tổng `TotalPrice × CommissionRate` của booking `Completed` | 50 triệu đồng |
| Hài lòng | Review đã duyệt từ 4-5 sao / tổng review đã duyệt | Trên 80% |

Phép tính phải nói rõ:

> `250 triệu đồng GMV × 20% hoa hồng = 50 triệu đồng doanh thu DriveGo`.

Kế hoạch vận hành dùng để chứng minh tính khả thi:

> `80 booking × 70% = 56 booking Completed`; giá trị trung bình cần đạt khoảng `4,46 triệu đồng/booking Completed`.

Không gọi `Confirmed`, `Active` là giao dịch thành công. Không gọi GMV hoặc tiền giữ chỗ trong ledger là doanh thu DriveGo.

## 3. Timeline tổng 10 phút

| Thời gian | Người nói | Nội dung | Slide/màn hình |
|---:|---|---|---|
| 0:00-0:45 | Huỳnh Lợi | Bài toán hai phía và giải pháp marketplace | Slide 1 |
| 0:45-1:30 | Nguyễn Hoài Phong | Mô hình hoa hồng và phép tính KPI doanh thu | Slide 2-3 |
| 1:30-3:00 | Nguyễn Vũ Hoài Tâm | Customer tìm, đặt và theo dõi vòng đời thuê | `/Booking/MyBookings` |
| 3:00-4:15 | Lê Quốc Vũ | Owner đăng xe và Admin kiểm soát nguồn cung | `/OwnerCars/Create`, ảnh Car Approval |
| 4:15-5:30 | Nguyễn Đắc Gia Huy | Staff bàn giao và đưa booking đến Completed | `/Staff/Handover`, ảnh Return |
| 5:30-7:00 | Võ Anh Tuấn | Dashboard KPI, hài lòng và bằng chứng kiểm thử | `/Admin/StartupKpi` |
| 7:00-9:15 | Huỳnh Lợi | Kế hoạch sáu tháng, pilot một tuần, kết luận | Slide 6-7 |
| 9:15-10:00 | Dự phòng | Chuyển tab hoặc xử lý trang tải chậm | Ảnh dự phòng |

Chỉ Nguyễn Vũ Hoài Tâm điều khiển chuột và bàn phím. Tất cả profile đăng nhập và mở sẵn trước khi trình bày.

## 4. Lời thoại ngắn theo từng thành viên

### 0:00-0:45 - Huỳnh Lợi

> Nhiều chủ xe có thời gian xe nhàn rỗi nhưng khó tiếp cận khách đáng tin cậy. Ở phía ngược lại, khách thuê khó kiểm tra xe, thanh toán và xử lý tranh chấp trên một quy trình thống nhất. DriveGo kết nối hai phía trên một marketplace thuê xe tự lái. Chủ xe tạo nguồn cung, khách tạo nhu cầu; DriveGo kiểm soát giao dịch và thu hoa hồng khi chuyến thuê hoàn thành.

Chuyển phần:

> Mô hình này chỉ khả thi khi KPI doanh thu được tính rõ ràng. Em xin chuyển sang bạn Phong.

### 0:45-1:30 - Nguyễn Hoài Phong

> Nhóm giữ đúng mục tiêu doanh thu DriveGo là 50 triệu đồng, không đổi thành GMV. Với mức hoa hồng mặc định 20%, DriveGo cần tạo 250 triệu đồng GMV từ booking Completed. Kế hoạch vận hành là 80 booking, tỷ lệ thành công 70%, tương đương 56 booking Completed và giá trị trung bình khoảng 4,46 triệu đồng mỗi booking. Hệ thống dùng Booking Hold và kiểm tra trùng lịch để giảm thất bại ngay từ checkout.

Chuyển phần:

> Tiếp theo bạn Tâm sẽ chứng minh hành trình tạo booking từ góc nhìn khách hàng.

### 1:30-3:00 - Nguyễn Vũ Hoài Tâm

Thao tác:

1. Mở `/Booking/MyBookings`.
2. Chỉ vào booking đang `Confirmed` và các bước tiến trình.
3. Chỉ vào một booking `Completed` và tổng giá trị booking.

Lời thoại:

> Khách hàng tìm xe, chọn thời gian và được kiểm tra khả dụng trước khi thanh toán. Khi checkout, hệ thống tạo Booking Hold có thời hạn để hạn chế hai khách giữ cùng xe. Màn hình này cho thấy booking được theo dõi từ đặt xe, xác nhận, bàn giao đến hoàn tất. KPI đơn thuê được tính từ booking hợp lệ; giao dịch thành công chỉ được tính khi booking đã Completed.

Chuyển phần:

> Có nhu cầu từ khách nhưng marketplace chỉ tăng trưởng khi có nguồn cung đủ chất lượng. Em xin chuyển sang bạn Vũ.

### 3:00-4:15 - Lê Quốc Vũ

Thao tác:

1. Mở `/OwnerCars/Create`, chỉ vào đăng kiểm, thông tin xe, giá và hình ảnh.
2. Tại bước giá, đổi đơn giá và chỉ vào ba số: giá khách thấy, hoa hồng DriveGo 20% và chủ xe dự kiến nhận 80%.
3. Mở `/OwnerCars`, chọn **Xem chi tiết** để chỉ vào hồ sơ xe, mục **Việc cần làm** và vị trí OpenStreetMap.
4. Chuyển nhanh sang ảnh dự phòng Admin Car Approval.

Lời thoại:

> Chủ xe có thể tự đăng xe, cấu hình giá thuê và cung cấp hồ sơ cần thiết. Ngay khi nhập đơn giá, hệ thống minh bạch phần hoa hồng DriveGo 20% và số tiền chủ xe dự kiến nhận 80%; hoa hồng chỉ ghi nhận khi booking hoàn tất. Trang chi tiết xe cho chủ xe biết hồ sơ nào còn thiếu, trạng thái duyệt, GPS, đăng kiểm, booking, GMV và vị trí trên OpenStreetMap. Xe không tự động xuất hiện công khai mà phải qua Admin kiểm tra. Luồng này trực tiếp phục vụ KPI 30 xe được duyệt. Trong pilot một tuần, nhóm sẽ lấy xác nhận quan tâm từ ít nhất 5 chủ xe và tạo pipeline tối thiểu 8 xe.

Chuyển phần:

> Sau khi có booking và xe được duyệt, giao dịch phải được vận hành đến hoàn thành. Em xin chuyển sang bạn Huy.

### 4:15-5:30 - Nguyễn Đắc Gia Huy

Thao tác:

1. Mở `/Staff/Handover`.
2. Chỉ vào booking cần bàn giao và nút tạo biên bản.
3. Dùng ảnh dự phòng Return nếu cần.

Lời thoại:

> Staff nhận booking đã xác nhận và lập biên bản bàn giao gồm tình trạng xe, ODO, nhiên liệu, ảnh và chữ ký. Khi nhận trả, dữ liệu trước và sau được đối chiếu trước khi booking chuyển sang Completed. Đây là bước biến booking thành giao dịch thành công có bằng chứng, giúp giảm tranh chấp và hỗ trợ mục tiêu hài lòng trên 80%.

Chuyển phần:

> Các bước vừa trình bày chỉ có ý nghĩa khi được đo bằng một định nghĩa thống nhất. Em xin chuyển sang bạn Tuấn.

### 5:30-7:00 - Võ Anh Tuấn

Thao tác:

1. Mở `/Admin/StartupKpi`.
2. Chỉ vào tỷ lệ thành công, GMV, hoa hồng và hài lòng.
3. Chỉ vào ba lớp tài chính: GMV, hoa hồng, ledger.

Lời thoại:

> Dashboard này tính KPI trực tiếp từ database local trong kỳ sáu tháng. Tỷ lệ thành công chỉ lấy Completed chia cho booking đã có kết quả cuối cùng. GMV là tổng giá trị booking Completed; doanh thu DriveGo là phần hoa hồng; ledger là dòng tiền đã hạch toán và được trình bày riêng. Review hài lòng chỉ tính review đã duyệt từ 4 đến 5 sao. Dữ liệu local chứng minh công thức và luồng đo lường, không được tuyên bố là kết quả kinh doanh thật.

> Nhóm đã thêm unit test cho công thức KPI, trạng thái bị loại, mẫu số bằng không, hoa hồng, hài lòng và quyền Admin của dashboard. Toàn bộ bộ test .NET vừa chạy ngày 15/06/2026 đạt 378/378, không failed, không skipped. Các suite Playwright liên quan OwnerCars, Tracking, Admin Tracking và MapSearch đều pass; nhóm không tuyên bố full regression cho các module ngoài phạm vi này.

Chuyển phần:

> Em xin chuyển lại trưởng nhóm để trình bày kế hoạch đạt KPI và kết luận.

### 7:00-9:15 - Huỳnh Lợi

> Kế hoạch sáu tháng tăng dần từ 5 booking ở tháng đầu lên 25 booking ở tháng thứ sáu. Tổng kế hoạch là 100 người dùng, 30 xe duyệt, 80 booking, 56 booking Completed, 250 triệu đồng GMV và 50 triệu đồng hoa hồng. Nhóm ưu tiên các chuyến thuê nhiều ngày để đạt giá trị trung bình 4,46 triệu đồng.

> Trong một tuần trước bảo vệ, nhóm thu bằng chứng pilot nhỏ gồm 5 chủ xe quan tâm, 20 phản hồi khách thuê, 10 đăng ký thử nghiệm và 3 booking test đầu-cuối. Các bằng chứng được ghi ngày, ẩn danh và tách rõ khỏi dữ liệu mô phỏng.

> DriveGo đã chứng minh được nền tảng kỹ thuật để kết nối nguồn cung, tạo booking, vận hành giao nhận và đo mô hình hoa hồng. Nhóm chưa tuyên bố đã đạt KPI thật hoặc production-ready; bước tiếp theo là xác thực pilot và tối ưu chuyển đổi dựa trên dữ liệu.

## 5. Bốn màn hình duy nhất được mở

| Thứ tự | Route | Mục đích |
|---:|---|---|
| 1 | `/Booking/MyBookings` | Chứng minh booking và vòng đời giao dịch |
| 2 | `/OwnerCars/Create` | Chứng minh khả năng thu hút nguồn cung |
| 3 | `/Staff/Handover` | Chứng minh vận hành đưa booking đến Completed |
| 4 | `/Admin/StartupKpi` | Chứng minh công thức KPI và mô hình doanh thu |

Không mở live SRE, API, audit, GPS, AI, kiến trúc hoặc danh sách controller trong 10 phút. Các nội dung đó chỉ dùng khi Q&A.

## 6. Phương án dự phòng

- Nếu trang không tải sau 8 giây, chuyển ngay sang ảnh chụp cùng route.
- Không đăng nhập, thanh toán, upload giấy tờ hoặc ghi dữ liệu live khi bảo vệ.
- Chuẩn bị video dự phòng tối đa 5 phút cho đúng bốn màn hình.
- Slide 3 và dashboard phải luôn có nhãn: **Mục tiêu/dữ liệu mô phỏng, không phải kết quả kinh doanh thực tế**.

## 7. Câu trả lời Q&A bắt buộc

**Vì sao mục tiêu 50 triệu cần 250 triệu GMV?**

> Vì DriveGo thu hoa hồng 20% trên booking Completed: 250 triệu × 20% = 50 triệu đồng.

**Tại sao không tính Confirmed hoặc Active là thành công?**

> Vì giao dịch chưa hoàn tất vẫn có thể bị hủy hoặc phát sinh sự cố. Nhóm chỉ tính Completed trên các booking đã có kết quả cuối cùng.

**Dữ liệu dashboard là thật hay mô phỏng?**

> Dashboard tính thật từ database local, nhưng dữ liệu local là dữ liệu demo. Nó chứng minh công thức đo và luồng hệ thống, không chứng minh startup đã đạt KPI kinh doanh.

**Làm sao chứng minh kế hoạch 6 tháng khả thi?**

> Nhóm dùng phép tính unit economics, kế hoạch theo tháng và pilot nhỏ có bằng chứng để xác thực nguồn cung, nhu cầu và luồng booking trước khi mở rộng.

## 8. Checklist tập luyện

- [ ] Hoàn thành nội dung chính trong 9 phút 15 giây.
- [ ] Mỗi thành viên nói đúng thời lượng, không giải thích thêm menu.
- [ ] Một người duy nhất điều khiển máy.
- [ ] Tất cả profile và route được mở sẵn.
- [ ] Tập ít nhất ba lần có bấm giờ.
- [ ] Tập một lần theo tình huống trang KPI không tải.
- [ ] Không gọi GMV, tiền giữ chỗ hoặc ledger là doanh thu DriveGo.
- [ ] Không gọi Confirmed/Active là giao dịch thành công.
- [ ] Không gọi dữ liệu local hoặc pilot nhỏ là kết quả KPI sáu tháng.
