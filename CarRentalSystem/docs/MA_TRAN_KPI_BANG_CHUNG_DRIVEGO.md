# Ma trận KPI và bằng chứng DriveGo

## Nguyên tắc sử dụng

- **Mục tiêu:** cam kết cần đạt trong sáu tháng.
- **Dữ liệu local:** dữ liệu demo dùng để chứng minh công thức và luồng hệ thống.
- **Pilot:** bằng chứng nhu cầu ban đầu thu thập từ người tham gia thật, có đồng ý và được ẩn danh.
- **Kết quả kinh doanh thật:** chỉ được công bố khi có giao dịch và đối soát thương mại hợp lệ.

| KPI | Công thức | Chức năng hỗ trợ | Bằng chứng hệ thống | Bằng chứng pilot cần thu |
|---|---|---|---|---|
| 100 người dùng | Tài khoản tạo trong kỳ | Đăng ký, xác thực email, eKYC | Admin Dashboard, Startup KPI | 10 đăng ký thử nghiệm |
| 30 xe duyệt | Xe tự lái đã duyệt, không bị xóa | Owner Create Car, Car Approval | Owner Portal, Admin Car Approval | 5 chủ xe quan tâm, pipeline 8 xe |
| 80 booking kế hoạch | Booking hợp lệ, loại Draft/Unpaid | Search, quote, hold, checkout | MyBookings, booking status | 3 booking test đầu-cuối |
| 70% thành công | Completed / trạng thái kết quả cuối | Hold, payment, handover, return | Staff Handover/Return, Startup KPI | Ghi nhận lý do fail/cancel của booking test |
| 250 triệu GMV | Tổng TotalPrice booking Completed | Pricing, booking, contract | Startup KPI | Xác thực thời gian thuê và mức giá chấp nhận |
| 50 triệu doanh thu | Tổng TotalPrice × CommissionRate | Commission, owner wallet, reports | Startup KPI, Owner Wallet | Xác thực mức hoa hồng với chủ xe |
| Trên 80% hài lòng | Review duyệt 4-5 sao / review duyệt | Review sau Completed, support | Startup KPI, Reviews | Phiếu đánh giá sau booking test |

## Ba lớp tài chính không được trộn

| Chỉ số | Ý nghĩa | Không được gọi là |
|---|---|---|
| GMV Completed | Tổng giá trị thuê của booking hoàn thành | Doanh thu DriveGo |
| Hoa hồng DriveGo | Phần nền tảng được hưởng theo CommissionRate | Toàn bộ GMV |
| Ledger đã hạch toán | Dòng tiền đã ghi vào sổ payment ledger | GMV hoặc doanh thu nếu chưa đối soát |

## Bằng chứng kỹ thuật trọng tâm

- `Services/StartupKpiService.cs`: công thức KPI.
- `/Admin/StartupKpi`: dashboard mục tiêu, dữ liệu local và kế hoạch sáu tháng.
- `ReservationService`: Booking Hold và chống trùng lịch.
- `ReviewController`: chỉ khách đã hoàn thành chuyến thuê mới được đánh giá.
- `RevenueMetricsService`: số liệu ledger được trình bày tách khỏi GMV booking.
- Unit test module `StartupKpi`: công thức, mẫu số bằng không, hoa hồng, review và quyền Admin.
- Log kiểm chứng ngày 15/06/2026: `375/375` test .NET pass, `0` failed, `0` skipped; không công bố Playwright pass khi chưa có log full regression mới.
