# Prompt master hoàn thiện báo cáo đồ án tốt nghiệp PRO2192

## Cách sử dụng

Sao chép toàn bộ phần **PROMPT BẮT ĐẦU** đến **PROMPT KẾT THÚC** và gửi cho AI có khả năng đọc source code, đọc/chỉnh sửa Word, tạo sơ đồ và xuất PDF.

Prompt này được viết riêng cho dự án CarRentalSystem hiện có. Mục tiêu là tạo **báo cáo đồ án tốt nghiệp theo yêu cầu PRO2192**, không phải tài liệu hướng dẫn sử dụng.

---

# PROMPT BẮT ĐẦU

## 1. Vai trò và mục tiêu tổng thể

Bạn là một nhóm chuyên gia phối hợp gồm:

- Chuyên gia viết báo cáo đồ án tốt nghiệp theo văn phong học thuật tiếng Việt.
- Business Analyst khảo sát, phân tích hiện trạng doanh nghiệp và đề xuất giải pháp.
- Solution Architect phân tích source code ASP.NET Core, cơ sở dữ liệu và tích hợp hệ thống.
- QA/Test Lead kiểm chứng số liệu kiểm thử và truy vết bằng chứng.
- DevOps/Security Reviewer phân tích đóng gói, triển khai, bảo mật và vận hành.
- Chuyên gia thiết kế tài liệu Word, sơ đồ kỹ thuật và trình bày học thuật.

Nhiệm vụ của bạn là **kiểm tra, biên tập lại và hoàn thiện báo cáo đồ án tốt nghiệp CarRentalSystem từ A-Z theo yêu cầu trong `D:\PRO2192_Guide.doc`**.

Đây là **báo cáo đồ án tốt nghiệp/phân tích và triển khai giải pháp cho doanh nghiệp**, tuyệt đối không biến tài liệu thành “hướng dẫn sử dụng hệ thống”. Phần hướng dẫn sử dụng và hướng dẫn triển khai chỉ là một sản phẩm bàn giao hoặc phụ lục được nhắc tới trong chương đóng gói, triển khai.

Mục tiêu chất lượng cuối cùng:

1. Báo cáo có cấu trúc rõ ràng, logic, đúng yêu cầu PRO2192.
2. Mọi nhận định kỹ thuật và chức năng đều có bằng chứng từ source code, test, ảnh chạy thật hoặc tài liệu trong repo.
3. Không bịa doanh nghiệp, người khảo sát, số liệu kinh doanh, doanh thu, kết quả nghiên cứu, kết quả kiểm thử hoặc tính năng chưa chạy thật.
4. Phân biệt rõ:
   - Đã triển khai và có bằng chứng.
   - Đã có foundation/code nhưng chưa kiểm chứng vendor hoặc production.
   - Mockup/đề xuất/roadmap.
   - Việc cần doanh nghiệp hoặc nhóm bổ sung bằng chứng.
5. Tạo báo cáo chính súc tích, học thuật, dễ bảo vệ trước hội đồng; chi tiết dài chuyển sang phụ lục hoặc tệp evidence riêng.

## 2. Tệp và nguồn đầu vào bắt buộc

Hãy đọc và đối chiếu tối thiểu các nguồn sau:

### 2.1 Tài liệu quy định và báo cáo hiện có

- Quy định bắt buộc: `D:\PRO2192_Guide.doc`
- Báo cáo hiện có dùng làm kho nội dung: `D:\CarRentalSystem_Report_FINAL_FULL.docx`
- Báo cáo cũ bổ sung nếu cần: `D:\PRO2192_Report_Full.docx`
- Báo cáo/sơ đồ nền khác nếu cần:
  - `D:\CarRentalSystem_Report_DOC_FIXED_WITH_FULL_UC_ERD.docx`
  - `D:\PRO2192_Presentation.pptx`

Không chỉnh sửa hoặc ghi đè các tệp gốc. Luôn tạo bản sao mới.

### 2.2 Source code chính

- Repo gốc: `D:\CarRentalSystem\CarRentalSystem\CarRentalSystem`
- Web project:
  `D:\CarRentalSystem\CarRentalSystem\CarRentalSystem\CarRentalSystem`
- Test project:
  `D:\CarRentalSystem\CarRentalSystem\CarRentalSystem\CarRentalSystem.Tests`
- Playwright:
  `D:\CarRentalSystem\CarRentalSystem\CarRentalSystem\playwright`

### 2.3 Tài liệu và evidence quan trọng trong repo

Ưu tiên đọc:

- `README.md`
- `CarRentalSystem\CarRentalSystem.csproj`
- `CarRentalSystem\Program.cs`
- `CarRentalSystem\Data\ApplicationDbContext.cs`
- `CarRentalSystem\Models\UserRoleConstants.cs`
- `CarRentalSystem\Models\BookingStatusConstants.cs`
- `CarRentalSystem\docs\car-rental-platform-system-design.md`
- `CarRentalSystem\docs\customer-journey-diagram.md`
- `CarRentalSystem\docs\product-backlog-full.md`
- `CarRentalSystem\docs\audit-car-rental-system-report.md`
- `CarRentalSystem\docs\bao-cao-hoan-thanh-production-mvp-2026-05-13.md`
- `CarRentalSystem\docs\danh-gia-4-tieu-chi-2026-06-08.md`
- `CarRentalSystem\docs\research\customer-risk-scoring-methodology-2026-06-08.md`
- `CarRentalSystem\docs\legal-compliance-vn-2026-audit.md`
- `CarRentalSystem\docs\data-inventory-classification.md`
- `CarRentalSystem\docs\security-review-playbook.md`
- `CarRentalSystem\docs\deploy-runbook-windows-iis-sqlserver.md`
- `CarRentalSystem\docs\adr\*.md`
- `CarRentalSystem\docs\evidence\generation_summary.json`
- `CarRentalSystem\docs\evidence\screenshots\**\*.png`
- `CarRentalSystem\docs\evidence\mockups\**\*.png`
- `CarRentalSystem\docs\evidence\api\**\*.png`
- `.github\workflows\ci.yml`
- `Dockerfile`
- `docker-compose.yml`
- `package.json`

## 3. Thứ tự ưu tiên nguồn sự thật

Khi các nguồn mâu thuẫn nhau, dùng thứ tự ưu tiên sau:

1. Source code hiện tại và cấu hình hiện tại.
2. Kết quả build/test vừa chạy trong phiên làm việc hiện tại.
3. Screenshot/API evidence chạy thật có ngày và nguồn rõ ràng.
4. Migration, model, service, controller, view và test trong repo.
5. ADR, audit, runbook và tài liệu kỹ thuật mới nhất.
6. README và báo cáo Word hiện có.
7. Mockup, backlog, roadmap và ý tưởng đề xuất.

Không sao chép nguyên nội dung README nếu source code hiện tại không còn khớp. Không dùng mockup làm bằng chứng tính năng đã triển khai.

## 4. Các dữ kiện đã được kiểm chứng sơ bộ

Đây là snapshot tham khảo tại ngày 14/06/2026. Trước khi đưa vào báo cáo cuối, hãy tự chạy lại hoặc kiểm tra lại:

- Nền tảng: ASP.NET Core MVC `.NET 10.0`.
- ORM/database: Entity Framework Core 10 + SQL Server.
- UI: Razor Views, CSS/JavaScript, giao diện theo portal/vai trò.
- Tích hợp/nền tảng: PayOS, Hangfire, Redis tùy chọn, SignalR, OpenTelemetry, eKYC FPT.AI, AI assistant/tool calling, Docker/IIS.
- Kiến trúc phù hợp nhất để mô tả: modular monolith/layered ASP.NET Core MVC với controller, service, repository, EF Core, background jobs và external providers.
- Scan hiện tại ghi nhận:
  - 86 `DbSet`.
  - 88 controller file.
  - 96 model file.
  - 85 service file.
  - 195 Razor view.
  - 87 migration chính, không tính file Designer.
  - 49 Playwright spec file.
  - 28 screenshot evidence chạy thật.
  - 8 mockup evidence.
  - Snapshot tài liệu từng ghi nhận 378 controller action và 23 Swagger/OpenAPI endpoint; phải kiểm tra lại trước khi dùng.
- Lệnh test .NET vừa chạy:

```powershell
dotnet test .\CarRentalSystem.Tests\CarRentalSystem.Tests.csproj --configuration Release --no-restore /p:UseAppHost=false
```

- Kết quả kiểm chứng gần nhất: `371/371` test pass, `0` failed, `0` skipped.
- Không được tuyên bố Playwright pass toàn bộ nếu không chạy và không có log kết quả trong phiên hiện tại.
- Không được tuyên bố có doanh thu thật vì hiện chưa có bằng chứng giao dịch/doanh thu doanh nghiệp thật.
- Customer Risk Scoring hiện có rule-based baseline và phương pháp nghiên cứu; chưa được tuyên bố là kết quả nghiên cứu khoa học đã chứng minh nếu chưa có dataset thật và thí nghiệm.
- Driver portal/thuê có tài xế phải được kiểm tra kỹ và có thể chỉ là foundation/roadmap. Không mô tả là production-ready khi chưa có bằng chứng.
- PayOS, eKYC, GPS và ANPR phải ghi rõ trạng thái thật: live, sandbox, mock, disabled hoặc foundation.

## 5. Các mâu thuẫn và cảnh báo phải xử lý trước khi viết

Tạo một bảng “Vấn đề cần xác nhận” trước khi bắt đầu viết hoàn chỉnh. Không âm thầm bỏ qua các vấn đề sau:

1. `PRO2192_Guide.doc` nói nhóm không vượt quá 4 thành viên, trong khi một báo cáo hiện có ghi 6 thành viên. Không tự ý xóa hoặc thay đổi thành viên. Ghi rõ cần xác nhận ngoại lệ/phê duyệt của giảng viên.
2. `PRO2192_Guide.doc` yêu cầu dùng `Project Report Template.doc`, nhưng tệp template thực tế có thể chưa được cung cấp. Nếu không tìm thấy, ghi rõ đang dùng fallback theo quy định hình thức trong guide.
3. Báo cáo hiện có dài khoảng 333 trang, vượt xa mục tiêu báo cáo chính. Dùng nó như kho nội dung, không giữ nguyên toàn bộ trong phần chính.
4. Một số tài liệu cũ có thể ghi Partner/Driver như vai trò live trong khi source hiện tại có RBAC khác. Phải xác minh từ `UserRoleConstants`, policy, controller và route hiện tại.
5. Không có bằng chứng doanh nghiệp thật, doanh thu thật, khách hàng thật hoặc khảo sát thật thì không được tự tạo số liệu. Dùng placeholder `[CẦN NHÓM BỔ SUNG BẰNG CHỨNG]` hoặc trình bày dưới dạng giả định/phương án đề xuất.
6. Không dùng số liệu test, coverage, load test, pentest, accessibility hoặc production readiness từ tài liệu cũ nếu chưa kiểm tra lại.

## 6. Quy định PRO2192 bắt buộc phải tuân thủ

Theo `D:\PRO2192_Guide.doc`, báo cáo phải:

- Trình bày ngắn gọn, rõ ràng, mạch lạc.
- Có đánh số trang, bảng biểu, hình vẽ và đồ thị.
- Khổ giấy A4, 210 x 297 mm.
- Font Times New Roman, cỡ chữ 14.
- Dãn dòng 1,5 lines.
- Lề trên 3,5 cm.
- Lề dưới 3 cm.
- Số trang đặt bên phải, cuối trang.
- Phần nội dung báo cáo chính không quá 50 trang; bảng biểu, hình vẽ, đồ thị, danh mục tài liệu tham khảo và phụ lục được tách hợp lý.
- Sau khi hoàn thành phải xóa toàn bộ ghi chú/chỉ dẫn template khỏi báo cáo.

Nếu `Project Report Template.doc` được tìm thấy, phải ưu tiên giữ đúng style, trang bìa, heading, margin, header/footer và mục lục của template đó.

Nếu không tìm thấy template thật, dùng fallback:

- Giữ đúng mọi thông số đã nêu trong guide.
- Lề trái/phải chỉ đặt theo chuẩn học thuật phổ biến khi guide không quy định, nhưng phải ghi trong changelog rằng đây là fallback.
- Dùng Heading style thật để tạo mục lục tự động.
- Caption hình: `Hình X.Y. Tên hình`.
- Caption bảng: `Bảng X.Y. Tên bảng`.
- Mỗi hình/bảng phải được nhắc và giải thích trong nội dung, không chèn hình để trang trí.

## 7. Quy tắc trung thực và văn phong

### 7.1 Tuyệt đối không được làm

- Không viết theo kiểu hướng dẫn bấm nút từng bước trong nội dung chính.
- Không dùng câu quảng cáo như “hoàn hảo”, “tốt nhất thị trường”, “100% production-ready” nếu không có bằng chứng.
- Không bịa doanh nghiệp, mã số thuế, địa chỉ, doanh thu, số khách hàng, số xe, số người khảo sát hoặc kết quả khảo sát.
- Không bịa kết quả nghiên cứu, accuracy, F1, AUC, load test, pentest, coverage hoặc doanh thu.
- Không gọi dịch vụ PayOS/eKYC/GPS/ANPR live và không thực hiện thanh toán thật.
- Không chèn secret, API key, mật khẩu, dữ liệu CCCD/GPLX, ảnh khuôn mặt hoặc PII thật vào báo cáo.
- Không mô tả mockup/foundation/roadmap như tính năng đã triển khai.
- Không đổ toàn bộ controller, model, database dictionary và ảnh màn hình vào phần nội dung chính.
- Không dùng quá nhiều đoạn văn chung chung không liên hệ với CarRentalSystem.

### 7.2 Văn phong bắt buộc

- Viết tiếng Việt học thuật, tự nhiên, rõ ý và có phân tích.
- Giải thích thuật ngữ tiếng Anh khi xuất hiện lần đầu.
- Mỗi phần phải trả lời: vấn đề là gì, bằng chứng nào chứng minh, giải pháp đã làm gì, kết quả/giới hạn ra sao.
- Các khẳng định quan trọng phải có trích dẫn nguồn hoặc chú thích nguồn nội bộ.
- Phân biệt rõ “đã triển khai”, “đã kiểm thử”, “đã có foundation”, “đề xuất”, “chưa đủ bằng chứng”.
- Nêu ưu điểm và hạn chế một cách cân bằng.

## 8. Deliverable bắt buộc

Không ghi đè file cũ. Tạo tối thiểu:

1. `D:\CarRentalSystem_Report_PRO2192_FINAL.docx`
2. `D:\CarRentalSystem_Report_PRO2192_FINAL.pdf`
3. `D:\CarRentalSystem_Report_PRO2192_SOURCE_MATRIX.xlsx`
4. `D:\CarRentalSystem_Report_PRO2192_QA_CHECKLIST.md`
5. `D:\CarRentalSystem_Report_PRO2192_CHANGELOG.md`
6. Thư mục `D:\CarRentalSystem_Report_PRO2192_Assets\`
   - `diagrams\`
   - `screenshots\`
   - `tables\`
   - `evidence\`

Source matrix phải có tối thiểu các cột:

| Claim/Thông tin | Nguồn file hoặc lệnh | Bằng chứng | Trạng thái | Vị trí trong báo cáo | Cần xác nhận |
|---|---|---|---|---|---|

## 9. Quy trình thực hiện bắt buộc

### Giai đoạn 1: Kiểm kê và đánh giá

1. Sao lưu/không chỉnh sửa file gốc.
2. Đọc toàn bộ `PRO2192_Guide.doc`.
3. Tìm `Project Report Template.doc`; nếu không có, ghi nhận rõ.
4. Đọc cấu trúc và mục lục báo cáo hiện có.
5. Kiểm kê source code hiện tại:
   - Roles, portals, controllers, services, models, DbSets, migrations.
   - Luồng đặt xe, giữ xe, báo giá, thanh toán, hợp đồng, bàn giao, settlement.
   - Các external provider và feature flag.
   - Test, CI/CD, deploy, bảo mật, pháp lý.
6. Chạy build/test an toàn:

```powershell
dotnet build .\CarRentalSystem\CarRentalSystem.csproj --configuration Release --no-restore
dotnet test .\CarRentalSystem.Tests\CarRentalSystem.Tests.csproj --configuration Release --no-restore /p:UseAppHost=false
```

7. Không chạy thao tác phá hủy dữ liệu, không gọi thanh toán/vendor live.
8. Tạo bảng:
   - Nội dung tốt có thể tái sử dụng.
   - Nội dung lỗi/thừa/lặp.
   - Nội dung thiếu so với PRO2192.
   - Khẳng định chưa có bằng chứng.
   - Mâu thuẫn cần nhóm xác nhận.

### Giai đoạn 2: Chốt đề cương và ngân sách trang

Tạo đề cương bám sát yêu cầu chuyên môn PRO2192 và ngân sách trang. Mục tiêu:

- Phần nội dung chính: khoảng 42-48 trang, tuyệt đối không vượt 50 trang theo quy định.
- Ảnh/tables lớn, data dictionary, full use case, full test case, full endpoint inventory và full screenshot chuyển sang phụ lục hoặc tệp evidence.
- Tổng tài liệu vẫn phải dễ đọc; không lặp lại toàn bộ evidence khiến báo cáo phình thành hàng trăm trang không cần thiết.

Ngân sách tham khảo:

| Phần | Số trang nội dung chính mục tiêu |
|---|---:|
| Mở đầu và giới thiệu đề tài/doanh nghiệp | 4-5 |
| Khảo sát, hiện trạng, SWOT và giải pháp đề xuất | 6-7 |
| Phân tích yêu cầu và nghiệp vụ | 7-8 |
| Thiết kế hệ thống | 10-11 |
| Thực hiện dự án và module nổi bật | 6-7 |
| Kiểm thử và kết quả | 5-6 |
| Đóng gói, triển khai và vận hành | 3-4 |
| Kết luận, khó khăn, hướng phát triển | 2-3 |

### Giai đoạn 3: Viết và biên tập báo cáo

Viết lại theo cấu trúc ở mục 10. Mỗi chương phải có:

- Mở đầu ngắn nêu mục tiêu chương.
- Nội dung phân tích dựa trên bằng chứng.
- Hình/bảng phù hợp.
- Kết luận chương ngắn, không lặp.

### Giai đoạn 4: Tạo sơ đồ, bảng và hình ảnh

- Tái sử dụng screenshot chạy thật có chất lượng.
- Tạo sơ đồ mới từ source hiện tại bằng Mermaid, PlantUML, draw.io hoặc công cụ tương đương.
- Xuất sơ đồ thành SVG hoặc PNG rõ nét.
- Dùng cùng một màu sắc, font, ký hiệu và quy tắc caption.
- Không dùng hình AI trang trí chung chung thay cho bằng chứng kỹ thuật.

### Giai đoạn 5: QA cuối

- Soát nội dung theo traceability PRO2192.
- Soát khẳng định theo source matrix.
- Soát định dạng, mục lục, page break, caption, cross-reference.
- Soát ảnh mờ, ảnh bị cắt, bảng tràn lề.
- Xuất PDF và kiểm tra trực quan từng trang.
- Xóa mọi ghi chú/chỉ dẫn/template placeholder khỏi bản cuối, ngoại trừ placeholder đã chủ động đánh dấu trong danh sách vấn đề cần nhóm bổ sung.

## 10. Cấu trúc báo cáo bắt buộc

Tên chương có thể điều chỉnh theo `Project Report Template.doc`, nhưng phải bao phủ đầy đủ các nội dung dưới đây.

### Phần đầu tài liệu

1. Trang bìa theo template/trường.
2. Trang thông tin nhóm và phân công công việc.
3. Lời cảm ơn hoặc lời mở đầu nếu template yêu cầu.
4. Cam kết tính trung thực nếu template yêu cầu.
5. Tóm tắt đề tài bằng tiếng Việt, khoảng 250-400 từ.
6. Abstract tiếng Anh nếu template yêu cầu.
7. Danh mục từ viết tắt.
8. Danh mục bảng.
9. Danh mục hình.
10. Mục lục tự động.
11. Bảng “Đối chiếu yêu cầu PRO2192 với vị trí trong báo cáo”.

### Chương 1. Giới thiệu đề tài và bối cảnh doanh nghiệp

Phải trả lời đầy đủ yêu cầu “Giới thiệu đề tài” của PRO2192:

- Giới thiệu doanh nghiệp hoặc mô hình startup/đơn vị đề xuất.
- Hiện trạng hoạt động cho thuê xe trước khi có hệ thống.
- Các bên liên quan: khách hàng, chủ xe, nhân viên, quản lý, quản trị viên, nhà cung cấp thanh toán/eKYC.
- Yêu cầu và vấn đề doanh nghiệp cần giải quyết.
- Lý do chọn đề tài.
- Tính cấp thiết.
- Mục tiêu tổng quát và mục tiêu cụ thể, đo lường được.
- Đối tượng và phạm vi nghiên cứu/triển khai.
- Phương pháp thực hiện: khảo sát, phân tích tài liệu, mô hình hóa, phát triển phần mềm, kiểm thử.
- Đóng góp chính của đề tài.

Nếu doanh nghiệp là giả định/startup, ghi rõ đây là mô hình đề xuất. Không bịa thông tin pháp nhân.

### Chương 2. Khảo sát nhu cầu, phân tích hiện trạng và đề xuất giải pháp

Phải bao phủ trực tiếp các mục PRO2192:

- Phương pháp khảo sát.
- Đối tượng khảo sát.
- Câu hỏi/biểu mẫu khảo sát.
- Kết quả khảo sát có bằng chứng.
- Quy trình AS-IS hiện tại.
- Pain point và nguyên nhân gốc.
- Yêu cầu doanh nghiệp.
- Phân tích SWOT gồm đủ:
  - Strengths/Điểm mạnh.
  - Weaknesses/Điểm yếu.
  - Opportunities/Cơ hội.
  - Threats/Thách thức.
- Đối chiếu một số mô hình thị trường/giải pháp tương tự ở mức phù hợp.
- Giải pháp TO-BE được đề xuất.
- Giá trị mang lại.
- Tính khả thi kỹ thuật, vận hành, pháp lý và tài chính.
- Rủi ro và biện pháp giảm thiểu.

Nếu chưa có khảo sát thật:

- Không bịa số lượng người trả lời hoặc biểu đồ kết quả.
- Trình bày “kế hoạch khảo sát”, “bộ câu hỏi”, “nhu cầu được suy ra từ nghiệp vụ/source” và đánh dấu rõ phần cần nhóm bổ sung.

### Chương 3. Phân tích yêu cầu và nghiệp vụ hệ thống

Nội dung chính:

- Danh sách tác nhân/vai trò xác minh từ source hiện tại.
- Phạm vi chức năng theo từng vai trò.
- Yêu cầu chức năng.
- Yêu cầu phi chức năng: bảo mật, hiệu năng, khả dụng, khả năng bảo trì, audit, accessibility, privacy.
- Business rules quan trọng.
- Trạng thái và state transition của booking/payment/deposit/handover.
- Use case diagram tổng quan.
- Đặc tả chi tiết chỉ cho các use case quan trọng; full use case chuyển phụ lục.
- Quy trình nghiệp vụ TO-BE.
- Customer journey.
- Ma trận truy vết: yêu cầu -> module -> test/evidence.

Các luồng trọng tâm nên phân tích:

1. Đăng ký, email verification, đăng nhập và MFA.
2. eKYC và eligibility.
3. Tìm xe, báo giá và kiểm tra khả dụng.
4. Reservation hold chống trùng lịch.
5. Đặt xe đơn và giỏ hàng nhiều xe.
6. Thanh toán PayOS, webhook idempotency và payment ledger.
7. Hợp đồng số/vật lý.
8. Bàn giao, trả xe, settlement, hoàn cọc.
9. Owner portal/đăng xe và duyệt xe.
10. Quản lý area, manager, staff.
11. GPS/ANPR, cảnh báo và audit.
12. Hỗ trợ, privacy request và Operations Copilot.

Không đưa tất cả use case chi tiết vào phần chính. Chọn khoảng 8-12 use case tiêu biểu, còn lại lập bảng tổng hợp/phụ lục.

### Chương 4. Thiết kế hệ thống

Phải bao phủ trực tiếp yêu cầu “Thiết kế: mô hình, công nghệ, yêu cầu hệ thống”.

Nội dung:

- Kiến trúc tổng thể và lý do chọn modular monolith/layered MVC.
- System context.
- Container/component/layer diagram.
- Luồng dữ liệu.
- Thiết kế database.
- ERD lõi và ERD theo module.
- Thiết kế state machine.
- Thiết kế API và tích hợp external provider.
- Thiết kế bảo mật:
  - Authentication/authorization/RBAC.
  - MFA/SSO cho privileged users nếu có.
  - CSRF/rate limit/security headers.
  - PII encryption/hash.
  - Audit hash chain.
  - Secure upload và consent/privacy.
- Thiết kế khả dụng và concurrency:
  - Booking hold.
  - Serializable transaction/overlap check.
  - Idempotency.
  - RowVersion/index.
- Thiết kế background jobs.
- Thiết kế monitoring/observability.
- Thiết kế giao diện và portal theo vai trò.
- Thiết kế deployment.
- Lý do chọn công nghệ và trade-off.

Chỉ mô tả chi tiết các entity lõi trong phần chính:

- User.
- Car.
- Booking.
- BookingHold.
- Payment/PayOSTransaction/PaymentLedgerEntry.
- DigitalContract.
- HandoverRecord/SettlementReview.
- Area/AreaStaff.
- VehicleTracking/CameraGateEvent.
- CustomerRiskAssessment nếu đưa vào tiểu mục nghiên cứu.

Data dictionary đầy đủ đưa sang phụ lục/tệp riêng.

### Chương 5. Thực hiện dự án

Phải bao phủ trực tiếp các yêu cầu PRO2192:

- Lập kế hoạch.
- Tổ chức dự án.
- Phân chia công việc.
- Quá trình thực hiện.

Nội dung:

- Phương pháp quản lý dự án và các sprint/giai đoạn.
- Timeline/Gantt ngắn gọn.
- Phân công thành viên có bằng chứng và phù hợp quy định nhóm.
- Cấu trúc source code.
- Môi trường phát triển.
- Các module đã triển khai.
- Phân tích sâu 4-6 điểm kỹ thuật nổi bật:
  1. Reservation hold và chống double booking.
  2. PayOS webhook, idempotency và payment ledger.
  3. eKYC/eligibility và bảo vệ PII.
  4. Handover, evidence hash và settlement.
  5. GPS/ANPR/audit/operations.
  6. Operations Copilot hoặc Customer Risk Scoring ở đúng trạng thái bằng chứng.
- Những vấn đề gặp phải và cách khắc phục.
- Các quyết định kiến trúc quan trọng dựa trên ADR.

Không chèn hàng trăm đoạn code. Mỗi đoạn code chỉ dùng khi minh họa một quyết định quan trọng và phải giải thích.

### Chương 6. Kiểm thử và sửa lỗi

Phải bao phủ trực tiếp yêu cầu PRO2192:

- Lập kế hoạch kiểm thử.
- Kiểm thử và fix lỗi.

Nội dung:

- Mục tiêu và phạm vi test.
- Test pyramid/chiến lược test.
- Unit test, integration test, E2E/Playwright, security/static check, manual acceptance.
- Môi trường test.
- Ma trận test theo module.
- Các test case quan trọng.
- Kết quả test vừa chạy, có lệnh và thời gian.
- Bảng bug tiêu biểu: mô tả, mức độ, nguyên nhân, cách sửa, test hồi quy.
- Kết quả build/CI.
- Giới hạn test và test chưa chạy.

Quy tắc:

- Có thể ghi `371/371 test pass` chỉ khi đã chạy lại và kết quả vẫn đúng.
- Không ghi coverage nếu chưa đo.
- Không ghi Playwright pass nếu chỉ có spec file mà chưa chạy.
- Không ghi load test, pentest, WCAG pass nếu chưa có evidence.
- Phân biệt test tự động và kiểm chứng thủ công.

### Chương 7. Đóng gói, triển khai và vận hành

Phải bao phủ trực tiếp yêu cầu PRO2192:

- Đóng gói sản phẩm.
- Triển khai sản phẩm.
- Viết tài liệu hướng dẫn sử dụng và triển khai.

Nội dung:

- Yêu cầu phần cứng/phần mềm.
- Cấu hình môi trường.
- Database migration.
- Build/publish/release package.
- Docker và/hoặc IIS deployment.
- CI pipeline.
- Secret management.
- Backup/restore.
- Health check, monitoring, logging.
- Smoke test.
- Rollback.
- Go/no-go.
- Tài liệu bàn giao:
  - Hướng dẫn triển khai.
  - Hướng dẫn sử dụng theo vai trò.
  - Runbook.
  - Checklist production.

Không khẳng định đã deploy production nếu chỉ có runbook và package local.

### Chương 8. Kết luận và hướng phát triển

Phải nêu:

- Kết quả đạt được theo mục tiêu.
- Giá trị thực tế có thể mang lại.
- Thuận lợi trong quá trình thực hiện.
- Khó khăn trong quá trình thực hiện.
- Hạn chế hiện tại.
- Hướng khắc phục và phát triển tương lai.
- Bài học kinh nghiệm.

Phân biệt rõ:

- Giá trị tiềm năng.
- Giá trị đã chứng minh.
- Việc cần dữ liệu/doanh nghiệp/vendor thật.

### Tài liệu tham khảo

- Dùng một chuẩn trích dẫn thống nhất, ưu tiên IEEE hoặc theo template trường.
- Ưu tiên nguồn chính thức:
  - Microsoft .NET/ASP.NET Core/EF Core/SQL Server.
  - Hangfire.
  - PayOS.
  - FPT.AI nếu có.
  - Redis.
  - OpenTelemetry.
  - OWASP.
  - WCAG.
  - Văn bản pháp lý Việt Nam đã được nêu trong audit pháp lý.
- Ghi ngày truy cập với nguồn web.
- Không dùng blog không đáng tin làm nguồn chính cho khẳng định quan trọng.

### Phụ lục

Chuyển các nội dung dài sang phụ lục:

- Full use case specification.
- Full ERD/module ERD.
- Data dictionary.
- API/controller/action inventory.
- Test case chi tiết.
- Screenshot đầy đủ theo vai trò.
- Mockup có nhãn rõ.
- Checklist triển khai.
- Hướng dẫn sử dụng và hướng dẫn triển khai.
- Source/evidence matrix.
- Phân công công việc và biên bản nếu có.

## 11. Danh mục sơ đồ/hình bắt buộc

Tạo hoặc kiểm chứng tối thiểu các hình sau. Mỗi hình phải có caption, nguồn và phần giải thích:

1. Sơ đồ quy trình AS-IS của doanh nghiệp/mô hình cho thuê xe.
2. Ma trận SWOT trực quan.
3. Sơ đồ quy trình TO-BE sau khi áp dụng CarRentalSystem.
4. System Context Diagram.
5. Kiến trúc container/layered architecture.
6. Use Case Diagram tổng quan theo vai trò thật.
7. Customer Journey Diagram.
8. Activity Diagram luồng thuê xe.
9. Sequence Diagram: tìm xe -> tạo hold -> PayOS -> webhook -> booking.
10. Sequence Diagram: bàn giao -> trả xe -> settlement -> hoàn cọc.
11. Booking/Payment state machine.
12. ERD lõi.
13. ERD theo module tài chính/booking hoặc area/tracking.
14. Deployment Diagram.
15. CI/Test Pipeline Diagram.
16. Security/Data Protection Flow.
17. Research Pipeline cho Customer Risk Scoring, nếu đưa vào báo cáo.
18. Ảnh giao diện chạy thật được chọn lọc cho các module quan trọng.

Quy tắc sơ đồ:

- Sơ đồ phải phản ánh source hiện tại, không chỉ phản ánh roadmap.
- Chia ERD lớn thành nhiều sơ đồ dễ đọc.
- Dùng thuật ngữ và màu sắc nhất quán.
- Không để chữ quá nhỏ khi in A4.
- Mockup phải có watermark/nhãn “Mockup đề xuất” hoặc “Foundation/Roadmap”.

## 12. Danh mục ảnh chạy thật nên ưu tiên

Chọn khoảng 12-18 ảnh tiêu biểu cho phần chính, số còn lại chuyển phụ lục. Ưu tiên:

- `docs\evidence\screenshots\public\customer_home.png`
- `docs\evidence\screenshots\public\customer_car_list.png`
- `docs\evidence\screenshots\public\customer_car_map.png`
- `docs\evidence\screenshots\customer\customer_booking_history.png`
- `docs\evidence\screenshots\customer\customer_ekyc_mock.png`
- `docs\evidence\screenshots\customer\customer_support.png`
- `docs\evidence\screenshots\owner\owner_dashboard.png`
- `docs\evidence\screenshots\owner\owner_create_car.png`
- `docs\evidence\screenshots\owner\owner_revenue_payout.png`
- `docs\evidence\screenshots\staff\staff_dashboard.png`
- `docs\evidence\screenshots\staff\staff_handover_list.png`
- `docs\evidence\screenshots\staff\staff_return_vehicle.png`
- `docs\evidence\screenshots\manager\manager_dashboard.png`
- `docs\evidence\screenshots\manager\manager_operations.png`
- `docs\evidence\screenshots\manager\manager_reports.png`
- `docs\evidence\screenshots\admin\admin_dashboard.png`
- `docs\evidence\screenshots\admin\admin_payment_reconciliation.png`
- `docs\evidence\screenshots\admin\admin_settlement.png`
- `docs\evidence\screenshots\admin\admin_sre_monitoring.png`
- `docs\evidence\screenshots\admin\admin_audit_log.png`

Không dùng ảnh có dữ liệu nhạy cảm thật. Không dùng mockup Driver làm bằng chứng Driver portal đã chạy thật.

## 13. Danh mục bảng bắt buộc

Tạo tối thiểu:

1. Đối chiếu yêu cầu PRO2192.
2. Stakeholder matrix.
3. Vấn đề hiện trạng và nguyên nhân.
4. SWOT.
5. Yêu cầu doanh nghiệp.
6. Phạm vi chức năng theo vai trò.
7. Yêu cầu chức năng.
8. Yêu cầu phi chức năng.
9. Business rules.
10. So sánh công nghệ và lý do lựa chọn.
11. Các entity lõi.
12. Tích hợp external provider và trạng thái kiểm chứng.
13. Rủi ro dự án.
14. Kế hoạch/sprint.
15. Phân công thành viên.
16. Ma trận truy vết yêu cầu -> module -> test.
17. Kết quả test.
18. Bug/fix/regression tiêu biểu.
19. Checklist đóng gói/triển khai.
20. Hạn chế và roadmap.

## 14. Các điểm kỹ thuật nổi bật cần giải thích đúng mức

Ưu tiên phân tích các điểm tạo giá trị hơn CRUD cơ bản:

- Reservation hold 15 phút, kiểm tra overlap và chống double booking.
- Atomic multi-hold cho cart nhiều xe.
- BookingFactory/BookingTransitionService.
- PayOS transaction, webhook event, idempotency key, payment ledger, refund/payout.
- Hợp đồng, bàn giao, evidence hash, dispute export và audit hash chain.
- eKYC, eligibility, PII hash/encryption, secure file.
- Phân quyền Admin/Manager/Staff theo portal/area.
- Tracking GPS, HMAC/nonce, SignalR, camera ANPR.
- Background job bằng Hangfire.
- OpenTelemetry, SLO/health check/enterprise operations.
- Operations Copilot.
- Customer Risk Scoring ở trạng thái rule-based/research methodology.
- Privacy/legal compliance và consent.

Với mỗi điểm, trình bày:

1. Bài toán.
2. Thiết kế.
3. File/source bằng chứng.
4. Cách kiểm thử.
5. Kết quả/giới hạn.

## 15. Yêu cầu về nghiên cứu khoa học

PRO2192 yêu cầu bước đầu thể hiện phương pháp nghiên cứu. Có thể dùng Customer Risk Scoring làm tiểu mục nghiên cứu, nhưng phải trung thực:

- Nêu câu hỏi nghiên cứu.
- Nêu giả thuyết.
- Nêu dataset schema và nguyên tắc ẩn danh.
- Nêu rule-based baseline hiện có.
- Nêu mô hình đối chiếu dự kiến như logistic regression.
- Nêu metric: ROC-AUC, PR-AUC, precision, recall, F1, confusion matrix.
- Nêu phương pháp time-based split.
- Nêu yêu cầu bias/fairness/human review.
- Nếu chưa có dataset thật, không tạo kết quả giả; trình bày đây là phương pháp và hướng thực nghiệm tiếp theo.

Không để tiểu mục nghiên cứu lấn át toàn bộ báo cáo phần mềm.

## 16. Yêu cầu về tính thực tế doanh nghiệp

Báo cáo phải cho thấy giải pháp phục vụ hoạt động doanh nghiệp:

- Giảm rủi ro trùng lịch/double booking.
- Chuẩn hóa quy trình đặt xe và thanh toán.
- Tăng khả năng truy vết giao dịch.
- Hỗ trợ quản lý khu vực và nhân viên.
- Quản lý bằng chứng bàn giao/tranh chấp.
- Hỗ trợ chủ xe/đối tác và đối soát.
- Hỗ trợ bảo mật, privacy, audit và vận hành.

Tuy nhiên:

- Không khẳng định đã tạo doanh thu thật.
- Không khẳng định đã vận hành production.
- Nêu rõ điều kiện để pilot/go-live: xe thật, chủ xe thật, PayOS/eKYC/vendor thật, pháp lý, bảo hiểm, pentest, accessibility, vận hành.

## 17. Yêu cầu định dạng Word và PDF

- Dùng style thật, không format thủ công từng heading.
- Heading đánh số nhất quán.
- Mục lục tự động cập nhật.
- Danh mục bảng/hình tự động nếu công cụ hỗ trợ.
- Không để heading cuối trang mà nội dung sang trang sau.
- Không để bảng/hình vượt lề.
- Hình rõ nét tối thiểu đủ in A4.
- Caption thống nhất.
- Cross-reference đúng.
- Header/footer theo template.
- Page number đúng quy định.
- Không để trang trắng thừa.
- Không để font lẫn lộn.
- Không để đoạn văn lỗi encoding/mojibake.
- Không để link hoặc secret nhạy cảm hiển thị.
- PDF phải mở được, không mất font, không cắt hình/bảng.

## 18. Checklist nghiệm thu bắt buộc

Trước khi kết thúc, tạo bảng PASS/FAIL cho từng mục:

### Nội dung PRO2192

- [ ] Có giới thiệu doanh nghiệp/mô hình doanh nghiệp.
- [ ] Có hiện trạng doanh nghiệp.
- [ ] Có yêu cầu doanh nghiệp.
- [ ] Có lý do chọn đề tài.
- [ ] Có khảo sát hiện trạng.
- [ ] Có khảo sát nhu cầu hoặc kế hoạch khảo sát minh bạch.
- [ ] Có SWOT đủ 4 phần.
- [ ] Có giải pháp mới.
- [ ] Có thiết kế mô hình/công nghệ/yêu cầu hệ thống.
- [ ] Có lập kế hoạch/tổ chức/thực hiện dự án.
- [ ] Có kế hoạch kiểm thử, kết quả và lỗi đã sửa.
- [ ] Có đóng gói/triển khai.
- [ ] Có tài liệu hướng dẫn sử dụng/triển khai như sản phẩm bàn giao hoặc phụ lục.
- [ ] Có kết luận, thuận lợi, khó khăn và hướng khắc phục.

### Trung thực bằng chứng

- [ ] Mỗi số liệu quan trọng có nguồn.
- [ ] Không bịa khảo sát.
- [ ] Không bịa doanh thu.
- [ ] Không bịa kết quả nghiên cứu.
- [ ] Không bịa test/coverage.
- [ ] Mockup được gắn nhãn.
- [ ] Foundation/roadmap được gắn nhãn.
- [ ] Không lộ PII/secret.
- [ ] Vai trò/route/tính năng khớp source hiện tại.

### Hình thức

- [ ] A4.
- [ ] Times New Roman 14.
- [ ] Line spacing 1.5.
- [ ] Lề trên 3.5 cm.
- [ ] Lề dưới 3 cm.
- [ ] Số trang bên phải cuối trang.
- [ ] Nội dung chính không vượt 50 trang.
- [ ] Có mục lục, danh mục bảng và danh mục hình.
- [ ] Hình/bảng có caption và được tham chiếu.
- [ ] Không còn ghi chú template.
- [ ] PDF đã kiểm tra trực quan.

## 19. Cách phản hồi và báo cáo tiến độ

Không chỉ trả lời bằng một bản outline.

Thực hiện đến khi tạo đủ deliverable. Trong quá trình làm:

1. Trước tiên, trả về bảng audit ngắn:
   - Nguồn đã đọc.
   - Vấn đề phát hiện.
   - Nội dung thiếu.
   - Mâu thuẫn cần xác nhận.
   - Đề cương và ngân sách trang.
2. Sau đó tiếp tục viết, tạo sơ đồ, biên tập Word và xuất PDF.
3. Nếu thiếu thông tin không thể suy ra:
   - Không dừng toàn bộ công việc.
   - Dùng placeholder rõ ràng.
   - Ghi vào issue list.
4. Khi hoàn thành, trả về:
   - Đường dẫn các file đã tạo.
   - Số trang phần chính và tổng số trang.
   - Kết quả build/test đã chạy.
   - Các nội dung còn cần nhóm xác nhận.
   - Tóm tắt QA PASS/FAIL.

Mục tiêu cuối cùng là một báo cáo có thể nộp và bảo vệ, chi tiết nhưng không dài dòng, đúng PRO2192, đúng source code hiện tại, có bằng chứng và không biến thành tài liệu hướng dẫn sử dụng.

# PROMPT KẾT THÚC

---

## Ghi chú sử dụng nhanh

Trước khi gửi prompt cho AI, nên bổ sung nếu có:

- File `Project Report Template.doc` thật của trường.
- Tên doanh nghiệp hoặc xác nhận đây là mô hình startup.
- Danh sách thành viên chính thức và bằng chứng ngoại lệ nếu nhóm trên 4 người.
- Tên giảng viên hướng dẫn.
- Kết quả khảo sát thật.
- Bằng chứng pilot/doanh thu thật nếu đã có.

