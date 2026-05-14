using System.Text;
using System.Text.Json;

namespace CarRentalSystem.Services;

public class GroqChatService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<GroqChatService> _logger;
    private const string ModelId = "llama-3.3-70b-versatile";
    private const int MaxHistoryMessages = 20;

    private const string SystemPrompt = """
        You are SmartCar AI for the CarRental web app.
        Stay inside the car-rental domain: renting cars, vehicle choices, booking flow, eKYC, CCCD/GPLX, payment/deposit, promotions, membership, cancellation, delivery range, pickup/return, partner/driver registration, and platform policies.
        You may suggest a few travel destinations only when the user asks about a trip or travel plan, and connect the suggestion back to renting a suitable car.
        If the question is outside this scope, politely decline and guide the user back to car rental support.
        Ask concise follow-up questions when needed: city/province, dates, passenger count, budget, self-drive/with-driver, and delivery preference.
        Do not invent exact live prices or availability. Tell users to check the car listing or booking page for final prices.
        Match the user's language. If culture starts with en, answer in English; otherwise answer in Vietnamese.
        """;

    public GroqChatService(IHttpClientFactory httpClientFactory, ILogger<GroqChatService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _logger.LogInformation("GroqChatService initialized");
    }

    public async Task<string> GetResponseAsync(List<ChatMessage> conversationHistory, string culture = "vi")
    {
        if (conversationHistory == null || conversationHistory.Count == 0)
            return "Ban can nhap noi dung de toi co the ho tro.";

        try
        {
            var messages = new List<object> { new { role = "system", content = $"{SystemPrompt}\nCurrent culture: {culture}" } };

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
                return BuildFallbackResponse(conversationHistory);
            }

            using var doc = JsonDocument.Parse(responseBody);
            var root = doc.RootElement;
            var reply = root.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();

            return !string.IsNullOrWhiteSpace(reply)
                ? reply
                : BuildFallbackResponse(conversationHistory);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Groq API call failed");
            return BuildFallbackResponse(conversationHistory);
        }
    }

    private static string BuildFallbackResponse(List<ChatMessage> conversationHistory)
    {
        var message = conversationHistory
            .LastOrDefault(m => string.Equals(m.Role, "user", StringComparison.OrdinalIgnoreCase))
            ?.Content?.Trim() ?? "";

        var normalized = message.ToLowerInvariant();

        if (normalized.Contains("ekyc") || normalized.Contains("xac thuc") || normalized.Contains("cccd") || normalized.Contains("gplx"))
        {
            return "Mình đang dùng chế độ hỗ trợ nội bộ. Để eKYC dễ qua hơn, bạn chụp CCCD/GPLX rõ 4 góc, không lóa sáng, ảnh selfie thấy rõ mặt và trùng với giấy tờ. Nếu hệ thống báo lỗi dịch vụ, hồ sơ sẽ được giữ để xác minh lại.";
        }

        if (normalized.Contains("thanh toan") || normalized.Contains("coc") || normalized.Contains("payos"))
        {
            return "Mình đang dùng chế độ hỗ trợ nội bộ. Hệ thống hỗ trợ thanh toán/đặt cọc online qua PayOS. Bạn nên kiểm tra lại số tiền, nội dung thanh toán và trạng thái đơn trong mục chuyến xe của tôi.";
        }

        if (normalized.Contains("huy") || normalized.Contains("hoan") || normalized.Contains("doi xe"))
        {
            return "Mình đang dùng chế độ hỗ trợ nội bộ. Nếu cần hủy, đổi xe hoặc hoàn cọc, bạn mở chi tiết đơn đặt xe và kiểm tra trạng thái hiện tại. Đơn đã nhận xe hoặc đang xử lý có thể cần admin hỗ trợ.";
        }

        if (normalized.Contains("gia") || normalized.Contains("thue") || normalized.Contains("xe") || normalized.Contains("dat"))
        {
            return "Mình đang dùng chế độ hỗ trợ nội bộ. Bạn cho mình biết khu vực, số người đi, ngày giờ nhận/trả xe, ngân sách và muốn tự lái hay có tài xế. Mình sẽ gợi ý loại xe phù hợp như sedan tiết kiệm, SUV gia đình, xe điện hoặc xe premium.";
        }

        if (normalized.Contains("khuyen mai") || normalized.Contains("ma giam") || normalized.Contains("thanh vien"))
        {
            return "Mình đang dùng chế độ hỗ trợ nội bộ. Bạn có thể kiểm tra ưu đãi trong mục hạng thành viên/khuyến mãi. Nếu đã có mã giảm giá, hãy nhập ở bước thanh toán để hệ thống tính lại tổng tiền.";
        }

        return "Mình đang dùng chế độ hỗ trợ nội bộ nên câu trả lời sẽ ngắn gọn hơn. Bạn có thể hỏi về thuê xe, giá xe, eKYC, GPLX, thanh toán/đặt cọc, hủy đơn, khuyến mãi hoặc chính sách giao nhận xe.";
    }
}
