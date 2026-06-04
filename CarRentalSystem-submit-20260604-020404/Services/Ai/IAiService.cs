namespace CarRentalSystem.Services.Ai;

public interface IAiService
{
    Task<AiChatResponse> ChatAsync(AiChatRequest request, AiUserContext user, CancellationToken cancellationToken = default);
}
