using Google.GenAI;
using Microsoft.Extensions.Configuration;

namespace CarRentalSystem.Services;

public class GeminiChatService
{
    private readonly Client? _geminiClient;
    private readonly string _apiKey;
    private readonly ILogger<GeminiChatService> _logger;
    private const string ModelId = "gemini-2.0-flash";
    public bool IsConfigured => !string.IsNullOrWhiteSpace(_apiKey) && _geminiClient != null;

    private const string SystemPrompt = @"Bạn là trợ lý AI cho ứng dụng thuê xe CAR RENTAL SYSTEM.
Hãy trả lời ngắn gọn, thân thiện bằng tiếng Việt.
Chỉ hỗ trợ các câu hỏi liên quan đến: thuê xe, xe điện, xe xăng, đặt xe, giá thuê, eKYC (xác thực danh tính), thanh toán, khuyến mãi, giao xe, GPLX (giấy phép lái xe), CCCD, chính sách thuê xe, thành viên, điểm tích lũy, bảo hiểm, dịch vụ giao xe.
Nếu câu hỏi không liên quan, hãy lịch sự từ chối và gợi ý quay lại chủ đề thuê xe.
Không đưa ra thông tin y tế, pháp lý chuyên sâu. Không cam kết giá cả cụ thể — hãy bảo user xem trang chủ để biết giá chi tiết.";

    public GeminiChatService(IConfiguration configuration, ILogger<GeminiChatService> logger)
    {
        _apiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY")
                   ?? configuration["Gemini:ApiKey"] ?? "";
        _logger = logger;

        if (!string.IsNullOrWhiteSpace(_apiKey))
        {
            _geminiClient = new Client(apiKey: _apiKey);
        }
    }

    public async Task<string> GetResponseAsync(string userMessage)
    {
        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            return "Xin lỗi, dịch vụ AI đang bảo trì. Vui lòng thử lại sau.";
        }

        if (string.IsNullOrWhiteSpace(userMessage))
            return "Bạn cần nhập nội dung để tôi có thể hỗ trợ.";

        if (_geminiClient == null)
        {
            return "Xin lỗi, dịch vụ AI chưa được cấu hình. Vui lòng liên hệ quản trị viên.";
        }

        try
        {
            var fullPrompt = $"{SystemPrompt}\n\nNgười dùng hỏi: {userMessage}";

            _logger.LogInformation("Calling Gemini for user message: {Message}", userMessage);

            var response = await _geminiClient.Models.GenerateContentAsync(
                ModelId, fullPrompt);

            if (response?.Candidates != null && response.Candidates.Count > 0)
            {
                var text = response.Candidates[0].Content.Parts[0].Text;
                if (!string.IsNullOrWhiteSpace(text))
                    return text;
            }

            return "Xin lỗi, tôi chưa có câu trả lời phù hợp. Vui lòng thử lại.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Gemini API call failed");
            return "Xin lỗi, dịch vụ AI đang bận hoặc đã hết quota miễn phí. Vui lòng thử lại sau hoặc liên hệ hotline để được hỗ trợ.";
        }
    }
}
