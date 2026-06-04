# Rà Soát Pháp Lý Việt Nam 2026 - CarRentalSystem

Ngày rà soát: 2026-05-20  
Phạm vi: toàn bộ repo, gồm mã nguồn ASP.NET Core, views, services, migrations, SQL/docs/scripts, cấu hình Docker/nginx, `.env.example`, Cypress/test assets và tài liệu vận hành.

Tài liệu này là rà soát kỹ thuật theo dấu vết trong repo, không thay thế ý kiến luật sư. Các giấy phép, hợp đồng DPA, đăng ký website TMĐT, hóa đơn điện tử và điều kiện kinh doanh vận tải nếu không có file chứng minh trong repo đều được đánh dấu là **chưa đủ bằng chứng**.

## Nguồn pháp lý đối chiếu

- Luật Bảo vệ dữ liệu cá nhân 91/2025/QH15: https://congbao.chinhphu.vn/van-ban/luat-so-91-2025-qh15-45578.htm
- Nghị định 356/2025/NĐ-CP hướng dẫn Luật Bảo vệ dữ liệu cá nhân: https://congbao.chinhphu.vn/van-ban/nghi-dinh-so-356-2025-nd-cp-468371.htm
- Luật Giao dịch điện tử 20/2023/QH15: https://vbpl.vn/TW/pages/vbpq-toanvan.aspx?ItemID=165913
- Luật Bảo vệ quyền lợi người tiêu dùng 19/2023/QH15: https://vbpl.vn/bocongan/Pages/vbpq-toanvan.aspx?ItemID=161263
- Nghị định 85/2021/NĐ-CP về thương mại điện tử: https://vbpl.vn/TW/Pages/vbpq-toanvan.aspx?ItemID=150735&Keyword=52%2F2013%2FN%C4%90-CP
- Nghị định 52/2024/NĐ-CP về thanh toán không dùng tiền mặt: https://sbv.gov.vn/vi/w/sbv598227
- Nghị định 158/2024/NĐ-CP về vận tải đường bộ: https://vbpl.vn/tw/pages/ivbpq-luocdo.aspx?ItemID=173412
- Hóa đơn điện tử: Nghị định 123/2020/NĐ-CP và Nghị định 70/2025/NĐ-CP.

## Inventory kỹ thuật

| Nhóm | File/module chính | Dữ liệu/rủi ro |
| --- | --- | --- |
| Đăng ký/tài khoản | `Controllers/AuthController.cs`, `ViewModels/RegisterViewModel.cs`, `Views/Auth/Register.cshtml`, `Models/User.cs` | Họ tên, email, phone, ngày sinh, CCCD/GPLX mã hóa/băm, consent đăng ký |
| eKYC | `Controllers/EkycController.cs`, `Services/EkycService.cs`, `Views/Ekyc/Index.cshtml`, `SecureFileController` | Ảnh CCCD/GPLX/khuôn mặt, OCR FPT.AI, biometric, secure file |
| Đặt xe/hợp đồng | `BookingController`, `CartController`, `Booking.cs`, `DigitalContract.cs`, `PhysicalContract.cs` | Hợp đồng điện tử, cam kết tự lái, IP/user-agent, terms/privacy version |
| Thanh toán/hoàn tiền | `PayOSPaymentService`, `RefundService`, `PaymentController`, `PaymentLedgerEntry`, `PayOSTransaction` | PayOS, payout, ledger, webhook/audit |
| Wallet | `Models/Wallet.cs`, `WalletTransaction.cs`, README/cấu hình | Chỉ được coi là ledger nội bộ; không bật stored value khi chưa có giấy phép |
| Tracking/GPS | `TrackingController`, `TrackingService`, `VehicleTracking.cs` | Vị trí xe, HMAC ingest, retention review |
| Thuế/hóa đơn | `Invoice.cs`, `BillingService`, admin invoice/report | Chưa có bằng chứng tích hợp hóa đơn điện tử hợp lệ |
| AI/chat/vendor | `AiService`, `GroqChatService`, `GeminiChatService`, `AiToolExecutor` | Cần DPA/vendor terms, hạn chế gửi PII sang AI |
| Cấu hình/vận hành | `.env.example`, `appsettings*.json`, Docker/nginx, docs | Secrets mẫu, feature flags pháp lý, chứng cứ vận hành |

## Kết luận theo ma trận pháp lý

| Lĩnh vực | Trạng thái repo sau chỉnh sửa | Rủi ro còn lại |
| --- | --- | --- |
| Dữ liệu cá nhân | Đã có `LegalDocumentVersion`, `ConsentRecord`, privacy notice thật, SLA/due date cho `PrivacyRequest` | Cần luật sư phê duyệt nội dung privacy notice và quy trình xử lý yêu cầu |
| eKYC/sinh trắc học | Submit/scan yêu cầu consent, ghi version, log FPT chỉ còn length/hash | Cần hợp đồng DPA với FPT.AI, retention policy ảnh eKYC, DPIA/impact assessment nếu áp dụng |
| Thanh toán/PayOS | Luồng PayOS/ledger có audit; payout mặc định tắt | Cần bằng chứng PayOS/đối tác thanh toán được cấp phép và điều khoản hoàn tiền |
| Wallet | Feature `Features:WalletStoredValue=false`; audit xác định wallet chỉ là ledger nội bộ | Nếu bật nạp/rút/giữ số dư như ví điện tử phải có giấy phép trung gian thanh toán |
| Hợp đồng điện tử | Booking/checkout lưu terms/privacy version, self-drive agreement time/IP/user-agent | Cần template hợp đồng được pháp chế phê duyệt, cơ chế ký/OTP thống nhất |
| TMĐT/marketplace | README và code thể hiện P2P marketplace/partner | Chưa có bằng chứng thông báo/đăng ký website TMĐT với Bộ Công Thương |
| Vận tải | Self-drive có cam kết GPLX và không dùng xe để vận tải thu tiền | With-driver public booking phải giữ tắt đến khi có điều kiện kinh doanh vận tải |
| Thuế/hóa đơn | Có model invoice nội bộ | Chưa có bằng chứng phát hành hóa đơn điện tử theo NĐ 123/2020 và 70/2025 |
| AI/chat | Có nhà cung cấp Groq/Gemini/OpenAI-like | Cần policy không gửi PII vào prompt, DPA/vendor terms và log redaction |
| Bảo mật/lưu trữ | SecureFile có kiểm tra quyền; tracking HMAC; audit log | Cần retention job thực thi thật cho tracking/eKYC/export/delete |

## Findings và backlog ưu tiên

### Critical

1. Privacy notice placeholder đã được thay thế bằng thông báo xử lý dữ liệu cá nhân tại `Views/Home/Privacy.cshtml`. Cần pháp chế phê duyệt version chính thức.
2. eKYC trước đây gọi FPT.AI và log raw response. Đã thay bằng log metadata hash/length trong `Services/EkycService.cs`, nhưng cần rà log lịch sử trước khi go-live.
3. Consent/version trước đây không được lưu. Đã thêm `LegalDocumentVersion`, `ConsentRecord` và lưu vào register/eKYC/booking/cart checkout.
4. Wallet trước đây mô tả như ví điện tử. Đã thêm feature flag `WalletStoredValue=false`; mọi nạp/rút/giữ số dư phải bị khóa nếu không có giấy phép.
5. Chưa có bằng chứng ngoài repo: đăng ký/thông báo TMĐT, DPA với FPT/PayOS/Groq/Google, hóa đơn điện tử, giấy phép/điều kiện vận tải.

### High

1. Checkout/booking phải luôn hiển thị link thật đến Điều khoản/Privacy. Các view chính đã thêm link route `Home/Terms` và `Home/Privacy`; cần rà các layout/footer còn `href="#"` không liên quan pháp lý.
2. Privacy request trước đây chỉ tạo manifest. Đã thêm due date, scope, identity verification status, completed-by và deletion manifest; cần triển khai export package/secure delivery thật.
3. Cart checkout nay ghi consent giống booking đơn lẻ. Cần đảm bảo mobile/JS không bypass required checkbox.
4. Với tracking GPS, cần chính sách thời hạn lưu, mục đích và hiển thị notice rõ trong hợp đồng.

### Medium

1. Nhiều view cũ còn mojibake; cần chuẩn hóa encoding UTF-8 để điều khoản/ngôn ngữ người tiêu dùng không gây hiểu nhầm.
2. Invoice model chưa chứng minh là hóa đơn điện tử hợp lệ.
3. AI chat cần guard không gửi CCCD/GPLX/khuôn mặt/booking dispute nhạy cảm ra vendor.
4. Data export vẫn ở mức manifest; cần zip dữ liệu người dùng, redaction và audit tải xuống.

### Low

1. Footer/social links còn `href="#"`; không phải lỗi pháp lý trực tiếp nhưng nên thay route thật hoặc ẩn.
2. Tài liệu README cần đồng bộ ngôn ngữ “ledger nội bộ” cho mọi đoạn còn mô tả wallet.
3. Cần thêm checklist vận hành định kỳ cho DPA, retention, incident response.

## Bằng chứng ngoài repo cần thu thập trước khi kết luận phù hợp

- Hồ sơ thông báo/đăng ký website/app TMĐT với Bộ Công Thương.
- Hợp đồng xử lý dữ liệu/DPA với FPT.AI, PayOS, Groq/Gemini/Google, SMTP, hosting/CDN.
- Bằng chứng PayOS/đối tác thanh toán là kênh được cấp phép; nếu có payout thì có điều khoản/ủy quyền phù hợp.
- Hồ sơ hóa đơn điện tử: nhà cung cấp hóa đơn, mẫu/ký số, quy trình phát hành/điều chỉnh/hủy.
- Hồ sơ điều kiện kinh doanh vận tải nếu bật dịch vụ có tài xế.
- Quy trình xử lý quyền chủ thể dữ liệu: nhận yêu cầu, xác minh, SLA, xuất/xóa/hạn chế, audit.
- Quy trình retention: tracking 90 ngày hoặc policy được duyệt, eKYC, hợp đồng, invoice, dispute.

## Kiểm thử pháp lý đề xuất

- Static: không còn placeholder privacy, không có raw `FPT.AI ... Response` log, link điều khoản/chính sách trong booking không còn `href="#"`, config wallet stored value mặc định false.
- Unit/integration: register/eKYC/checkout bắt buộc consent version; booking lưu terms/privacy/self-drive evidence; privacy request có due date và completion audit.
- Payment: PayOS qua provider, payout mặc định tắt, wallet top-up/withdraw không xuất hiện public khi `Features:WalletStoredValue=false`.
- Manual acceptance: chỉ kết luận “phù hợp để vận hành thương mại tại Việt Nam” khi checklist bằng chứng ngoài repo có đủ tài liệu được duyệt.
