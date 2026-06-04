using System.Security.Claims;
using CarRentalSystem.Services.Ai;
using Microsoft.AspNetCore.Mvc;

namespace CarRentalSystem.Controllers;

[ApiController]
[Route("api/ai")]
[IgnoreAntiforgeryToken]
public class AiController : ControllerBase
{
    private readonly IAiService _aiService;

    public AiController(IAiService aiService)
    {
        _aiService = aiService;
    }

    [HttpPost("chat")]
    public async Task<IActionResult> Chat([FromBody] AiChatRequest request, CancellationToken cancellationToken)
    {
        if (request == null || (string.IsNullOrWhiteSpace(request.Message) && string.IsNullOrWhiteSpace(request.ConfirmationId)))
            return BadRequest(new { error = "Message or confirmationId is required." });

        var response = await _aiService.ChatAsync(request, BuildUserContext(), cancellationToken);
        return Ok(response);
    }

    private AiUserContext BuildUserContext()
    {
        if (User.Identity?.IsAuthenticated != true)
            return new AiUserContext();

        var role = User.FindFirstValue(ClaimTypes.Role) ?? "Customer";
        return new AiUserContext
        {
            UserId = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) ? userId : null,
            Role = role,
            UserName = User.Identity?.Name ?? User.FindFirstValue(ClaimTypes.Email)
        };
    }
}
