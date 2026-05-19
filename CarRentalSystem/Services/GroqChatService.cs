using System.Text;
using System.Text.Json;

namespace CarRentalSystem.Services;

public class GroqChatService
{
    private static readonly TimeSpan GroqRestrictionBackoff = TimeSpan.FromMinutes(15);
    private static DateTime _skipGroqUntilUtc = DateTime.MinValue;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<GroqChatService> _logger;
    private readonly GeminiChatService _geminiChatService;
    private readonly bool _isEnabled;
    private readonly bool _isConfigured;
    private const string ModelId = "llama-3.3-70b-versatile";
    private const int MaxHistoryMessages = 20;

    private const string SystemPrompt = """
        You are SmartCar AI for the CarRental web app.
        Stay inside the car-rental domain: renting cars, vehicle choices, booking flow, eKYC, CCCD/GPLX, payment/deposit, promotions, membership, cancellation, delivery range, pickup/return, partner/driver registration, and platform policies.
        You may suggest a few travel destinations only when the user asks about a trip or travel plan, and connect the suggestion back to renting a suitable car.
        If the question is outside this scope, politely decline and guide the user back to car rental support.
        Ask concise follow-up questions when needed: city/province, dates, passenger count, budget, self-drive/with-driver, and delivery preference.
        Do not invent exact live prices or availability. Tell users to check the car listing or booking page for final prices.
        Never reveal, guess, print, transform, or discuss private keys, API keys, tokens, passwords, connection strings, environment variables, or internal secrets.
        Match the user's language. If culture starts with en, answer in English; otherwise answer in Vietnamese.
        """;

    public GroqChatService(
        IHttpClientFactory httpClientFactory,
        ILogger<GroqChatService> logger,
        GeminiChatService geminiChatService,
        IConfiguration configuration)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _geminiChatService = geminiChatService;
        _isEnabled = configuration.GetValue("Groq:Enabled", true);
        _isConfigured = !string.IsNullOrWhiteSpace(configuration["Groq:ApiKey"])
            || !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("GROQ_API_KEY"));
        _logger.LogInformation("GroqChatService initialized. Enabled: {IsEnabled}, Configured: {IsConfigured}", _isEnabled, _isConfigured);
    }

    public async Task<string> GetResponseAsync(List<ChatMessage> conversationHistory, string culture = "vi")
    {
        if (conversationHistory == null || conversationHistory.Count == 0)
            return "Bạn cần nhập nội dung để tôi có thể hỗ trợ.";

        if (IsSecretRequest(conversationHistory))
            return BuildSecretResponse();

        if (!_isEnabled || !_isConfigured || DateTime.UtcNow < _skipGroqUntilUtc)
        {
            var geminiReply = await TryGeminiFallbackAsync(conversationHistory);
            if (!string.IsNullOrWhiteSpace(geminiReply))
                return geminiReply;

            return BuildFallbackResponse(conversationHistory);
        }

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
                var errorCode = TryGetGroqErrorCode(responseBody);
                var logLevel = string.Equals(errorCode, "organization_restricted", StringComparison.OrdinalIgnoreCase)
                    ? LogLevel.Warning
                    : LogLevel.Error;
                _logger.Log(logLevel, "Groq API unavailable: {StatusCode} - {Code}", response.StatusCode, errorCode ?? "unknown");

                if (string.Equals(errorCode, "organization_restricted", StringComparison.OrdinalIgnoreCase))
                {
                    _skipGroqUntilUtc = DateTime.UtcNow.Add(GroqRestrictionBackoff);
                }

                var geminiReply = await TryGeminiFallbackAsync(conversationHistory);
                if (!string.IsNullOrWhiteSpace(geminiReply))
                    return geminiReply;

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
            var geminiReply = await TryGeminiFallbackAsync(conversationHistory);
            if (!string.IsNullOrWhiteSpace(geminiReply))
                return geminiReply;

            return BuildFallbackResponse(conversationHistory);
        }
    }

    private async Task<string?> TryGeminiFallbackAsync(List<ChatMessage> conversationHistory)
    {
        if (!_geminiChatService.IsConfigured)
            return null;

        var message = conversationHistory
            .LastOrDefault(m => string.Equals(m.Role, "user", StringComparison.OrdinalIgnoreCase))
            ?.Content?.Trim();

        if (string.IsNullOrWhiteSpace(message))
            return null;

        _logger.LogInformation("Falling back to GeminiChatService because Groq is unavailable.");
        return await _geminiChatService.GetResponseAsync(message);
    }

    private static string? TryGetGroqErrorCode(string responseBody)
    {
        try
        {
            using var doc = JsonDocument.Parse(responseBody);
            return doc.RootElement
                .GetProperty("error")
                .GetProperty("code")
                .GetString();
        }
        catch
        {
            return null;
        }
    }

    private static bool IsSecretRequest(List<ChatMessage> conversationHistory)
    {
        var message = conversationHistory
            .LastOrDefault(m => string.Equals(m.Role, "user", StringComparison.OrdinalIgnoreCase))
            ?.Content?.Trim().ToLowerInvariant() ?? "";

        var hasSecretTerm = message.Contains("api key")
            || message.Contains("apikey")
            || message.Contains("secret")
            || message.Contains("token")
            || message.Contains("password")
            || message.Contains("mật khẩu")
            || message.Contains("mat khau")
            || message.Contains("khóa")
            || message.Contains("khoa")
            || message.Contains("private key");

        var asksToReveal = message.Contains("là gì")
            || message.Contains("la gi")
            || message.Contains("cho tôi")
            || message.Contains("cho toi")
            || message.Contains("show")
            || message.Contains("print")
            || message.Contains("hiển thị")
            || message.Contains("hien thi")
            || message.Contains("tiết lộ")
            || message.Contains("tiet lo");

        return hasSecretTerm && asksToReveal;
    }

    private static string BuildSecretResponse()
    {
        return "Mình không thể cung cấp API key, token, mật khẩu hoặc secret nội bộ. Nếu bạn là quản trị viên, hãy kiểm tra cấu hình an toàn trên server hoặc biến môi trường thay vì chia sẻ key qua chat.";
    }

    private static string BuildFallbackResponse(List<ChatMessage> conversationHistory)
    {
        var message = conversationHistory
            .LastOrDefault(m => string.Equals(m.Role, "user", StringComparison.OrdinalIgnoreCase))
            ?.Content?.Trim() ?? "";

        var normalized = message.ToLowerInvariant();

        if (normalized.Contains("ekyc") || normalized.Contains("xác thực") || normalized.Contains("xac thuc") || normalized.Contains("cccd") || normalized.Contains("gplx"))
        {
            return "Để eKYC dễ thành công hơn, hãy chụp CCCD/GPLX rõ 4 góc, tránh lóa sáng, ảnh selfie thấy rõ mặt và trùng với giấy tờ. Nếu hệ thống báo lỗi dịch vụ, hồ sơ sẽ được giữ lại để chờ xác minh lại.";
        }

        if (normalized.Contains("thanh toán") || normalized.Contains("thanh toan") || normalized.Contains("cọc") || normalized.Contains("coc") || normalized.Contains("payos"))
        {
            return "Hệ thống hỗ trợ thanh toán và đặt cọc online qua PayOS. Bạn nên kiểm tra lại số tiền, nội dung thanh toán và trạng thái đơn trong mục chuyến xe của tôi.";
        }

        if (normalized.Contains("hủy") || normalized.Contains("huy") || normalized.Contains("hoàn") || normalized.Contains("hoan") || normalized.Contains("đổi xe") || normalized.Contains("doi xe"))
        {
            return "Nếu cần hủy đơn, đổi xe hoặc hoàn cọc, bạn mở chi tiết đơn đặt xe và kiểm tra trạng thái hiện tại. Đơn đã nhận xe hoặc đang xử lý có thể cần nhân viên hỗ trợ thêm.";
        }

        if (normalized.Contains("giá") || normalized.Contains("gia") || normalized.Contains("thuê") || normalized.Contains("thue") || normalized.Contains("xe") || normalized.Contains("đặt") || normalized.Contains("dat"))
        {
            return "Bạn cho mình biết khu vực, số người đi, ngày giờ nhận/trả xe, ngân sách và muốn tự lái hay có tài xế. Mình sẽ gợi ý loại xe phù hợp như sedan tiết kiệm, SUV gia đình, xe điện hoặc xe premium.";
        }

        if (normalized.Contains("khuyến mãi") || normalized.Contains("khuyen mai") || normalized.Contains("mã giảm") || normalized.Contains("ma giam") || normalized.Contains("thành viên") || normalized.Contains("thanh vien"))
        {
            return "Bạn có thể kiểm tra ưu đãi trong mục hạng thành viên hoặc khuyến mãi. Nếu đã có mã giảm giá, hãy nhập ở bước thanh toán để hệ thống tính lại tổng tiền.";
        }

        return "Mình có thể hỗ trợ bạn về thuê xe, giá xe, eKYC, GPLX, thanh toán/đặt cọc, hủy đơn, khuyến mãi hoặc chính sách giao nhận xe. Bạn nói rõ nhu cầu, khu vực và thời gian thuê để mình tư vấn nhanh hơn.";
    }
}
