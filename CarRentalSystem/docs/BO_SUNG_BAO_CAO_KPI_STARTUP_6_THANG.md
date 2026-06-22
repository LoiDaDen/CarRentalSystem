# Nội dung bổ sung báo cáo: Mô hình startup và KPI sáu tháng

## Mô hình kinh doanh

DriveGo được định vị là nền tảng marketplace kết nối chủ xe có xe nhàn rỗi với khách hàng có nhu cầu thuê xe tự lái. Chủ xe cung cấp nguồn xe và quyết định mức giá; khách hàng tìm kiếm, đặt và theo dõi chuyến thuê; DriveGo cung cấp quy trình kiểm soát giao dịch, thanh toán, hợp đồng, bàn giao, nhận trả và đánh giá.

Nguồn doanh thu chính của DriveGo là phí hoa hồng trên booking hoàn thành. Với tỷ lệ hoa hồng mặc định 20%, mục tiêu 50 triệu đồng doanh thu yêu cầu tối thiểu 250 triệu đồng GMV từ các booking Completed. Báo cáo phân biệt rõ GMV, hoa hồng nền tảng và dòng tiền đã hạch toán trong payment ledger.

## Định nghĩa KPI

Giao dịch thành công được định nghĩa là booking Completed trên tổng booking đã có kết quả cuối cùng gồm Completed, Cancelled, Rejected và Expired. Các booking Pending, Confirmed và Active không nằm trong mẫu số vì chưa có kết quả cuối cùng. Cách tính này tránh đánh giá sai giao dịch đang xử lý là thành công hoặc thất bại.

Mức độ hài lòng được tính bằng tỷ lệ review đã duyệt từ 4 đến 5 sao trên tổng review đã duyệt. Chỉ khách hàng có booking Completed mới được gửi đánh giá.

## Unit economics và kế hoạch vận hành

Kế hoạch vận hành sáu tháng đặt mục tiêu 80 booking, trong đó 56 booking Completed tương ứng tỷ lệ thành công 70%. Để tạo 250 triệu đồng GMV, giá trị trung bình cần đạt khoảng 4,46 triệu đồng cho mỗi booking Completed. Vì vậy chiến lược ưu tiên chuyến thuê nhiều ngày thay vì phụ thuộc hoàn toàn vào đơn thuê một ngày.

Kế hoạch tăng trưởng theo tháng:

| Tháng | Người dùng mới | Xe duyệt | Booking | Completed | GMV | Hoa hồng |
|---:|---:|---:|---:|---:|---:|---:|
| 1 | 15 | 5 | 5 | 3 | 10 triệu | 2 triệu |
| 2 | 15 | 5 | 8 | 6 | 20 triệu | 4 triệu |
| 3 | 15 | 5 | 10 | 7 | 30 triệu | 6 triệu |
| 4 | 20 | 5 | 14 | 10 | 45 triệu | 9 triệu |
| 5 | 17 | 5 | 18 | 12 | 60 triệu | 12 triệu |
| 6 | 18 | 5 | 25 | 18 | 85 triệu | 17 triệu |
| Tổng | 100 | 30 | 80 | 56 | 250 triệu | 50 triệu |

## Phương pháp chứng minh

Nhóm sử dụng ba lớp bằng chứng. Thứ nhất, source code, unit test và dashboard KPI chứng minh công thức cùng khả năng đo lường. Thứ hai, dữ liệu local chứng minh luồng nghiệp vụ hoạt động nhưng không được xem là kết quả kinh doanh. Thứ ba, pilot nhỏ trong một tuần xác thực nhu cầu ban đầu từ chủ xe và khách thuê. Chỉ số pilot được ghi nguồn, ẩn danh và không được suy diễn thành việc đã đạt KPI sáu tháng.

## Giới hạn

Hệ thống hiện chưa có bằng chứng vận hành thương mại, doanh thu thật hoặc quy mô 100 người dùng và 30 xe. PayOS, eKYC, GPS và ANPR vẫn cần kiểm chứng với vendor hoặc thiết bị thật. Dashboard KPI phục vụ đo lường và bảo vệ đồ án; trước khi pilot/go-live cần hoàn thiện pháp lý, bảo hiểm, đối soát tài chính, pentest, load test và quy trình hỗ trợ vận hành.
