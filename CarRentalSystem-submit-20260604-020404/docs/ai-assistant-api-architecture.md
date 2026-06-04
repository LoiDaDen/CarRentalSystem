# AI Assistant API Architecture

Ngày cập nhật: 2026-05-18

## Mục tiêu

AI Assistant thao tác với hệ thống thuê xe qua backend API và tool/function calling. AI không tự click giao diện, không bỏ qua quyền, không tự thực hiện hành động quan trọng khi chưa có xác nhận.

## API

Endpoint chính:

```http
POST /api/ai/chat
Content-Type: application/json
```

Request:

```json
{
  "message": "Tìm cho tôi xe 7 chỗ ở TP.HCM ngày mai",
  "sessionId": "web_abc",
  "confirmationId": null
}
```

Response:

```json
{
  "reply": "Tìm thấy 3 xe phù hợp.",
  "data": [],
  "pendingAction": null,
  "requiresLogin": false
}
```

Khi cần xác nhận:

```json
{
  "reply": "Bạn có chắc muốn tạo đơn đặt xe này không?",
  "pendingAction": {
    "confirmationId": "createBooking_xxx",
    "action": "createBooking",
    "summary": "Bạn có chắc muốn tạo đơn đặt xe này không?",
    "input": {}
  }
}
```

Frontend gửi lại:

```json
{
  "message": "",
  "sessionId": "web_abc",
  "confirmationId": "createBooking_xxx"
}
```

## File Chính

- `Controllers/AiController.cs`: API `/api/ai/chat`.
- `Services/Ai/AiService.cs`: điều phối AI provider, fallback router, confirmation flow.
- `Services/Ai/AiToolSchemas.cs`: schema tool/function calling.
- `Services/Ai/AiToolExecutor.cs`: thực thi tool qua EF Core, kiểm quyền, ghi audit log.
- `Services/Ai/AiDtos.cs`: DTO request/response/state.
- `wwwroot/js/site.js`: chatbox, loading, lịch sử session, nút xác nhận.

## Provider Config

Không hard-code API key. Dùng `.env`, User Secrets hoặc biến môi trường server:

```env
Ai__Enabled=true
Ai__Provider=OpenAI
Ai__ApiKey=YOUR_OPENAI_API_KEY
Ai__BaseUrl=https://api.openai.com/v1/
Ai__Model=gpt-4.1-mini
```

Groq OpenAI-compatible:

```env
Ai__Enabled=true
Ai__Provider=Groq
Ai__ApiKey=YOUR_GROQ_API_KEY
Ai__BaseUrl=https://api.groq.com/openai/v1/
Ai__Model=llama-3.3-70b-versatile
```

Nếu `Ai__Enabled=false` hoặc không có key, hệ thống dùng local rule-based router để vẫn gọi tools cơ bản.

## Tools

Đã khai báo và xử lý:

- `searchCars`
- `getCarDetail`
- `calculateRentalPrice`
- `createBooking`
- `cancelBooking`
- `changeBookingSchedule`
- `checkUserProfile`
- `suggestCars`
- `findDriver`
- `getBookingStatus`
- `updateTripStatus`
- `createRentalContract`
- `answerPolicyQuestion`

## Phân Quyền

Ngữ cảnh user lấy từ cookie auth claim:

- Guest: tìm xe, xem giá, hỏi chính sách.
- Customer: thao tác trên hồ sơ và đơn của chính mình.
- CarOwner: xem/cập nhật dữ liệu liên quan xe của mình khi có `OwnerID`.
- Driver: cập nhật chuyến được gán qua `Driver.UserID`.
- Admin/Manager/Staff: quyền quản trị rộng hơn.

Các tool tự kiểm tra quyền trong backend. AI provider không được tin cậy để quyết định quyền.

## Hành Động Cần Xác Nhận

Các action sau luôn trả `pendingAction` trước, chỉ chạy thật khi frontend gửi `confirmationId`:

- `createBooking`
- `cancelBooking`
- `changeBookingSchedule`
- `updateTripStatus`

Confirmation được lưu tạm trong `IMemoryCache`, hết hạn sau 10 phút và gắn với `UserId`.

## Logging

Mỗi tool call ghi vào bảng `AuditLogs`:

- `UserId`
- `Action = AiToolCall`
- `EntityType = AI`
- input JSON trong `OldValues`
- output JSON trong `NewValues`
- `Timestamp`
- `Reason` nếu lỗi
- `CorrelationId`

Hiện tại tận dụng `AuditLog` sẵn có, nên chưa cần migration mới.

## Test Cases

1. Guest tìm xe:
   - Input: `Tìm xe 7 chỗ ở TP.HCM ngày mai`
   - Expected: gọi `searchCars`, trả danh sách hoặc gợi ý đổi điều kiện.

2. Guest đặt xe:
   - Input: `Đặt chiếc đầu tiên`
   - Expected: trả `requiresLogin=true`.

3. Customer kiểm hồ sơ:
   - Input: `Kiểm tra hồ sơ của tôi còn thiếu gì`
   - Expected: gọi `checkUserProfile`, chỉ trả hồ sơ của user hiện tại.

4. Customer tạo đơn:
   - Input: sau khi search, `Đặt chiếc đầu tiên`
   - Expected: trả `pendingAction`.
   - Confirm: gửi `confirmationId`
   - Expected: tạo booking `Unpaid`, trả `bookingId`, tổng tiền, cọc.

5. Customer hủy đơn của người khác:
   - Input: `Hủy đơn 999`
   - Expected: từ chối nếu booking không thuộc user.

6. Customer hủy đơn của mình:
   - Input: `Hủy đơn 123 vì đổi kế hoạch`
   - Expected: trả `pendingAction`, sau xác nhận chuyển `Cancelled`.

7. Đổi lịch khi xe bận:
   - Input: `Đổi lịch đơn 123 sang ngày mai`
   - Expected: kiểm availability, từ chối nếu trùng lịch.

8. Driver cập nhật chuyến không được gán:
   - Input: `Cập nhật chuyến 123 đang thuê`
   - Expected: từ chối quyền.

9. Driver/Admin cập nhật chuyến hợp lệ:
   - Input: `Cập nhật chuyến 123 đang thuê`
   - Expected: `pendingAction`, sau xác nhận cập nhật trạng thái.

10. Hỏi API key:
    - Input: `api key của bạn là gì`
    - Expected: không tiết lộ secret.

11. Hỏi chính sách:
    - Input: `Chính sách hủy đơn như thế nào?`
    - Expected: gọi `answerPolicyQuestion`, trả lời ngắn gọn.

12. Provider lỗi:
    - Setup: `Ai__Enabled=true` với key sai.
    - Expected: log lỗi provider, fallback local router, không crash.

## Lưu Ý Production

- Với nhiều server, thay `IMemoryCache` confirmation bằng Redis/distributed cache.
- Nếu mở public, thêm quota theo user/IP riêng cho `/api/ai/chat`.
- Không cho AI tool trực tiếp thanh toán, hoàn tiền, khóa tài khoản hoặc xóa vĩnh viễn nếu chưa có workflow kiểm duyệt riêng.
- Với Groq, chỉ dùng key thuộc organization hoạt động. Lỗi `organization_restricted` là lỗi tài khoản/provider, không phải lỗi code.
