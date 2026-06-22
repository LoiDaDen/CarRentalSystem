# Kế hoạch pilot DriveGo trong một tuần

## Mục tiêu

Thu bằng chứng nhu cầu ban đầu và kiểm tra luồng nghiệp vụ. Pilot không được dùng để tuyên bố đã đạt KPI sáu tháng.

## Chỉ tiêu pilot

| Nhóm bằng chứng | Chỉ tiêu tối thiểu | Dữ liệu cần lưu |
|---|---:|---|
| Chủ xe quan tâm | 5 người, pipeline 8 xe | Ngày, loại xe, khu vực, mức hoa hồng chấp nhận, trạng thái đồng ý |
| Phản hồi khách thuê | 20 phản hồi | Nhu cầu, số ngày thuê, ngân sách, rào cản đặt xe |
| Đăng ký thử nghiệm | 10 tài khoản | Ngày đăng ký, nguồn tiếp cận, trạng thái xác thực |
| Booking test đầu-cuối | 3 booking | Thời gian, trạng thái, lỗi, thời gian xử lý, đánh giá |

## Lịch thực hiện

| Ngày | Việc cần làm | Kết quả |
|---:|---|---|
| 1 | Chuẩn hóa form đồng ý, câu hỏi và tiêu chí chọn người tham gia | Bộ form và mã định danh ẩn danh |
| 2 | Tiếp cận chủ xe và thu thông tin nguồn cung | Danh sách pipeline xe |
| 3 | Thu phản hồi nhu cầu khách thuê | Bảng nhu cầu và mức giá |
| 4 | Hướng dẫn đăng ký và tạo booking test | Tài khoản và booking hợp lệ |
| 5 | Chạy bàn giao/nhận trả mô phỏng có sự đồng ý | Biên bản và lỗi phát hiện |
| 6 | Thu review, tổng hợp lý do thành công/thất bại | Bảng review và issue |
| 7 | Chốt báo cáo pilot, ẩn danh và đưa bằng chứng vào phụ lục | Báo cáo pilot có nguồn |

## Bộ câu hỏi chủ xe

1. Xe có thể cho thuê bao nhiêu ngày mỗi tháng?
2. Mức giá thuê ngày mong muốn là bao nhiêu?
3. Chủ xe có chấp nhận hoa hồng 20% nếu nền tảng hỗ trợ tìm khách và kiểm soát giao dịch không?
4. Rào cản lớn nhất khi cho người lạ thuê xe là gì?
5. Chủ xe cần bằng chứng hoặc bảo vệ nào trước khi tham gia?

## Bộ câu hỏi khách thuê

1. Mục đích và thời gian thuê phổ biến là gì?
2. Ngân sách chấp nhận cho một chuyến thuê là bao nhiêu?
3. Điều gì khiến khách bỏ quá trình đặt xe?
4. Khách cần thông tin nào để tin tưởng xe và chủ xe?
5. Khách đánh giá trải nghiệm booking test ở mức bao nhiêu sao?

## Quy tắc bằng chứng

- Dùng mã người tham gia thay cho tên thật trong tài liệu bảo vệ.
- Không chiếu CCCD, GPLX, số điện thoại, địa chỉ hoặc dữ liệu nhạy cảm.
- Lưu ngày thu thập, phương pháp và người phụ trách.
- Không bịa hoặc nhân rộng kết quả pilot thành dự báo thị trường.
- Gắn nhãn rõ `PILOT`, `DỮ LIỆU LOCAL` hoặc `MỤC TIÊU/MÔ PHỎNG`.
