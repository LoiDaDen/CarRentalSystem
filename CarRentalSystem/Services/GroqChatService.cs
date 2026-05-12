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
            return "Minh dang dung che do ho tro noi bo. De eKYC de qua hon, ban chup CCCD/GPLX ro 4 goc, khong loe sang, anh selfie thay ro mat va trung voi giay to. Neu he thong bao loi dich vu, ho so se duoc giu de xac minh lai.";
        }

        if (normalized.Contains("thanh toan") || normalized.Contains("coc") || normalized.Contains("payos"))
        {
            return "Minh dang dung che do ho tro noi bo. He thong ho tro thanh toan/dat coc online qua PayOS. Ban nen kiem tra lai so tien, noi dung thanh toan va trang thai don trong muc chuyen xe cua toi.";
        }

        if (normalized.Contains("huy") || normalized.Contains("hoan") || normalized.Contains("doi xe"))
        {
            return "Minh dang dung che do ho tro noi bo. Neu can huy, doi xe hoac hoan coc, ban mo chi tiet don dat xe va kiem tra trang thai hien tai. Don da nhan xe hoac dang xu ly co the can admin ho tro.";
        }

        if (normalized.Contains("gia") || normalized.Contains("thue") || normalized.Contains("xe") || normalized.Contains("dat"))
        {
            return "Minh dang dung che do ho tro noi bo. Ban cho minh biet khu vuc, so nguoi di, ngay gio nhan/tra xe, ngan sach va muon tu lai hay co tai xe. Minh se goi y loai xe phu hop nhu sedan tiet kiem, SUV gia dinh, xe dien hoac xe premium.";
        }

        if (normalized.Contains("khuyen mai") || normalized.Contains("ma giam") || normalized.Contains("thanh vien"))
        {
            return "Minh dang dung che do ho tro noi bo. Ban co the kiem tra uu dai trong muc hang thanh vien/khuyen mai. Neu da co ma giam gia, hay nhap o buoc thanh toan de he thong tinh lai tong tien.";
        }

        return "Minh dang dung che do ho tro noi bo nen cau tra loi se ngan gon hon. Ban co the hoi ve thue xe, gia xe, eKYC, GPLX, thanh toan/dat coc, huy don, khuyen mai hoac chinh sach giao nhan xe.";
    }
}
