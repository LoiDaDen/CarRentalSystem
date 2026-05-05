using System.Text;
using System.Text.Json;

namespace CarRentalSystem.Services;

public class GroqChatService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<GroqChatService> _logger;
    private const string ModelId = "llama-3.3-70b-versatile";
    private const int MaxHistoryMessages = 20;

    private const string SystemPrompt = @"Bạn là trợ lý AI cho ứng dụng thuê xe CAR RENTAL SYSTEM.
Hãy trả lời ngắn gọn, thân thiện bằng tiếng Việt.

KHI NGƯỜI DÙNG MUỐN THUÊ XE — hãy CHỦ ĐỘNG gợi ý xe phù hợp bằng cách hỏi các thông tin sau (1-2 câu hỏi mỗi lần):
A) Địa điểm đi (thành phố, tỉnh nào?)
B) Số người đi cùng (1-2 người, gia đình 4-5 người, nhóm đông?)
C) Loại xe ưa thích (xe xăng, xe điện, SUV, sedan, xe sang?)
D) Ngân sách (bình dân, trung cấp, cao cấp?)
E) Thuê có tài xế hay tự lái?
F) Thời gian thuê (theo giờ, theo ngày, dài hạn?)

SAU KHI BIẾT ĐỦ THÔNG TIN — hãy gợi ý CỤ THỂ:
- Loại xe phù hợp (Ví dụ: Với 4 người đi Đà Lạt, bạn nên chọn SUV 7 chỗ như Toyota Fortuner hoặc xe sedan tiết kiệm như Mazda 3)
- Ưu điểm của từng loại xe
- Giá tham khảo theo ngày (Ví dụ: Giá thuê thường từ 800k-1.5 triệu/ngày tùy loại xe)

VỚI CÁC CHỦ ĐỀ KHÁC — trả lời ngắn gọn:
- eKYC: hướng dẫn chụp CCCD, chụp mặt để xác thực
- Thanh toán: hỗ trợ PayOS, thanh toán đặt cọc online
- Khuyến mãi: thành viên, tích điểm, mã giảm giá
- Chính sách: đặt cọc, hủy xe, bảo hiểm, giao xe
- GPLX: yêu cầu bằng lái xe, các hạng xe

KHÔNG VIẾT câu bạn có thể xem trang chủ để biết giá chi tiết — hãy cố gắng đưa ra gợi ý cụ thể nhất có thể.
Không đưa ra thông tin y tế, pháp lý chuyên sâu.";

    public GroqChatService(IHttpClientFactory httpClientFactory, ILogger<GroqChatService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _logger.LogInformation("GroqChatService initialized");
    }

    public async Task<string> GetResponseAsync(List<ChatMessage> conversationHistory)
    {
        if (conversationHistory == null || conversationHistory.Count == 0)
            return "Bạn cần nhập nội dung để tôi có thể hỗ trợ.";

        try
        {
            var messages = new List<object> { new { role = "system", content = SystemPrompt } };

            var historyToSend = conversationHistory.Count > MaxHistoryMessages
                ? conversationHistory.Skip(conversationHistory.Count - MaxHistoryMessages).ToList()
                : conversationHistory;

            foreach (var msg in historyToSend)
            {
                messages.Add(new { role = msg.Role, content = msg.Content });
            }

            var requestBody = new
            {
                model = ModelId,
                messages,
                temperature = 0.7,
                max_tokens = 800
            };

            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var client = _httpClientFactory.CreateClient("GroqChatService");
            var response = await client.PostAsync("chat/completions", content);

            var responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Groq API error: {StatusCode} - {Body}", response.StatusCode, responseBody);
                return "Xin lỗi, dịch vụ AI đang bận. Vui lòng thử lại sau.";
            }

            using var doc = JsonDocument.Parse(responseBody);
            var root = doc.RootElement;
            var reply = root.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();

            if (!string.IsNullOrWhiteSpace(reply))
                return reply;

            return "Xin lỗi, tôi chưa có câu trả lời phù hợp. Vui lòng thử lại.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Groq API call failed");
            return "Xin lỗi, dịch vụ AI đang bận. Vui lòng thử lại sau.";
        }
    }
}
