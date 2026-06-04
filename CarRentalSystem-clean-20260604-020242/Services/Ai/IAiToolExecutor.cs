using System.Text.Json;

namespace CarRentalSystem.Services.Ai;

public interface IAiToolExecutor
{
    Task<AiToolResult> ExecuteAsync(AiToolCallRequest request, AiUserContext user, AiConversationState state, CancellationToken cancellationToken = default);
}
