using System.Text.Json;
using System.Text.Json.Serialization;

namespace CarRentalSystem.Services.Ai;

public sealed class AiChatRequest
{
    public string Message { get; set; } = "";
    public string? SessionId { get; set; }
    public string? ConfirmationId { get; set; }
    public string? Mode { get; set; }
}

public sealed class AiChatResponse
{
    public string Reply { get; set; } = "";
    public List<object> Data { get; set; } = new();
    public List<string> SuggestedReplies { get; set; } = new();
    public AiPendingAction? PendingAction { get; set; }
    public bool RequiresLogin { get; set; }
}

public sealed class AiPendingAction
{
    public required string ConfirmationId { get; set; }
    public required string Action { get; set; }
    public required string Summary { get; set; }
    public object? Input { get; set; }
}

public sealed class AiToolCallRequest
{
    public required string Name { get; set; }
    public JsonElement Arguments { get; set; }
    public bool IsConfirmed { get; set; }
}

public sealed class AiToolResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = "";
    public object? Data { get; set; }
    public AiPendingAction? PendingAction { get; set; }
    public bool RequiresLogin { get; set; }
}

public sealed class AiUserContext
{
    public int? UserId { get; init; }
    public string Role { get; init; } = "Guest";
    public string? UserName { get; init; }
    public bool IsAuthenticated => UserId.HasValue;
    public bool IsInternalStaff =>
        string.Equals(Role, "Admin", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(Role, "Manager", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(Role, "Staff", StringComparison.OrdinalIgnoreCase);
}

public sealed class AiProviderOptions
{
    public bool Enabled { get; set; }
    public string Provider { get; set; } = "OpenAI";
    public string ApiKey { get; set; } = "";
    public string BaseUrl { get; set; } = "https://api.openai.com/v1/";
    public string Model { get; set; } = "gpt-4.1-mini";
}

public sealed class AiConversationState
{
    public List<AiCarSummary> LastCars { get; set; } = new();
    public int? LastBookingId { get; set; }
    public AiRentalNeed RentalNeed { get; set; } = new();
    public AiPriceQuote? LastQuote { get; set; }
    public int TurnCount { get; set; }
}

public sealed class AiRentalNeed
{
    public string? Location { get; set; }
    public int? Seats { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public decimal? MinBudget { get; set; }
    public decimal? MaxBudget { get; set; }
    public string? BudgetPeriod { get; set; }
    public string RentalType { get; set; } = "tu_lai";
}

public sealed class AiCarSummary
{
    public int CarId { get; set; }
    public string Name { get; set; } = "";
    public string? Brand { get; set; }
    public string? Location { get; set; }
    public int Seats { get; set; }
    public string FuelType { get; set; } = "";
    public string Transmission { get; set; } = "";
    public decimal PricePerDay { get; set; }
    public decimal PricePerHour { get; set; }
    public string? ImageUrl { get; set; }
}

public sealed class AiPriceQuote
{
    public int CarId { get; set; }
    public string CarName { get; set; } = "";
    public decimal RentalAmount { get; set; }
    public decimal DriverFee { get; set; }
    public decimal ServiceFee { get; set; }
    public decimal Deposit { get; set; }
    public decimal EstimatedTotal { get; set; }
    public int TotalHours { get; set; }
    public int RentalDays { get; set; }
}

public sealed class AiBookingDraft
{
    public int? UserId { get; set; }
    public int CarId { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string PickupLocation { get; set; } = "";
    public string ReturnLocation { get; set; } = "";
    public string RentalType { get; set; } = "tu_lai";
    public string? Note { get; set; }
}

public sealed class AiConfirmationEnvelope
{
    public required string Action { get; set; }
    public required JsonElement Arguments { get; set; }
    public required string Summary { get; set; }
    public int? UserId { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
}

public static class AiJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };
}
