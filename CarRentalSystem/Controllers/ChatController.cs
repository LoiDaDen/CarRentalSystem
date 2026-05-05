using Microsoft.AspNetCore.Mvc;
using CarRentalSystem.Services;

namespace CarRentalSystem.Controllers;

[ApiController]
[Route("api/[controller]")]
[IgnoreAntiforgeryToken]
public class ChatController : ControllerBase
{
    private readonly GroqChatService _groqChatService;
    private readonly IChatSessionService _chatSessionService;
    private readonly ILogger<ChatController> _logger;

    public ChatController(
        GroqChatService groqChatService,
        IChatSessionService chatSessionService,
        ILogger<ChatController> logger)
    {
        _groqChatService = groqChatService;
        _chatSessionService = chatSessionService;
        _logger = logger;
    }

    public class ChatRequest
    {
        public string Message { get; set; } = "";
        public string? SessionId { get; set; }
        public bool ClearHistory { get; set; } = false;
    }

    [HttpPost]
    public async Task<IActionResult> Send([FromBody] ChatRequest request)
    {
        if (string.IsNullOrWhiteSpace(request?.Message))
        {
            return Ok(new { reply = "Bạn cần nhập nội dung để tôi có thể hỗ trợ." });
        }

        var sessionId = GetSessionId(request.SessionId);

        if (request.ClearHistory)
        {
            _chatSessionService.ClearSession(sessionId);
            return Ok(new { reply = "Đã xóa lịch sử cuộc trò chuyện. Bạn có thể bắt đầu mới!" });
        }

        _chatSessionService.AddUserMessage(sessionId, request.Message.Trim());

        var history = _chatSessionService.GetOrCreateSession(sessionId);
        var reply = await _groqChatService.GetResponseAsync(history);

        _chatSessionService.AddAssistantMessage(sessionId, reply);

        return Ok(new { reply });
    }

    private string GetSessionId(string? clientSessionId)
    {
        if (!string.IsNullOrWhiteSpace(clientSessionId))
            return clientSessionId;

        if (User.Identity?.IsAuthenticated == true)
            return $"user_{User.Identity.Name}";

        return $"guest_{HttpContext.Connection.RemoteIpAddress}";
    }
}
