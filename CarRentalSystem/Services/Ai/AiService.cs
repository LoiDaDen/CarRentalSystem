using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace CarRentalSystem.Services.Ai;

public sealed class AiService : IAiService
{
    private const int MaxMessageLength = 2000;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IAiToolExecutor _toolExecutor;
    private readonly IMemoryCache _cache;
    private readonly IDistributedCache _distributedCache;
    private readonly AiProviderOptions _options;
    private readonly ILogger<AiService> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public AiService(
        IHttpClientFactory httpClientFactory,
        IAiToolExecutor toolExecutor,
        IMemoryCache cache,
        IDistributedCache distributedCache,
        IOptions<AiProviderOptions> options,
        ILogger<AiService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _toolExecutor = toolExecutor;
        _cache = cache;
        _distributedCache = distributedCache;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<AiChatResponse> ChatAsync(AiChatRequest request, AiUserContext user, CancellationToken cancellationToken = default)
    {
        request.Message ??= "";
        if (request.Message.Length > MaxMessageLength)
            return new AiChatResponse { Reply = $"Tin nhắn quá dài. Vui lòng nhập tối đa {MaxMessageLength} ký tự." };

        var sessionId = string.IsNullOrWhiteSpace(request.SessionId)
            ? $"guest_{Guid.NewGuid():N}"
            : request.SessionId.Trim()[..Math.Min(request.SessionId.Trim().Length, 100)];
        var state = _cache.GetOrCreate($"ai_state:{sessionId}", entry =>
        {
            entry.SlidingExpiration = TimeSpan.FromHours(4);
            return new AiConversationState();
        })!;
        state.TurnCount++;

        if (!string.IsNullOrWhiteSpace(request.ConfirmationId))
            return await ExecuteConfirmedActionAsync(request.ConfirmationId, user, state, cancellationToken);

        if (IsOpsMode(request))
            return await RunOpsModeAsync(request.Message, user, state, cancellationToken);

        var slotUpdate = UpdateRentalNeedFromMessage(request.Message, state);
        if (slotUpdate && IsRentalNeedOnlyMessage(request.Message))
            return await ContinueRentalNeedConversationAsync(state, user, cancellationToken);

        if (TryBuildComparisonResponse(request.Message, state, out var comparisonResponse))
            return comparisonResponse;

        if (TryBuildDeterministicToolCall(request.Message, state, out var deterministicTool, out var deterministicArgs))
        {
            var deterministicResult = await ExecuteOrQueueToolAsync(deterministicTool, deterministicArgs, user, state, cancellationToken);
            return ToResponse(deterministicResult, state);
        }

        if (!_options.Enabled || string.IsNullOrWhiteSpace(_options.ApiKey))
            return await RunRuleBasedAsync(request.Message, user, state, cancellationToken);

        try
        {
            return await RunModelWithToolsAsync(request.Message, user, state, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AI provider failed; falling back to local tool router.");
            return await RunRuleBasedAsync(request.Message, user, state, cancellationToken);
        }
    }

    private async Task<AiChatResponse> ExecuteConfirmedActionAsync(string confirmationId, AiUserContext user, AiConversationState state, CancellationToken ct)
    {
        var confirmationKey = $"ai_confirm:{confirmationId}";
        var envelopeJson = await _distributedCache.GetStringAsync(confirmationKey, ct);
        var envelope = string.IsNullOrWhiteSpace(envelopeJson)
            ? null
            : JsonSerializer.Deserialize<AiConfirmationEnvelope>(envelopeJson, JsonOptions);
        if (envelope == null || envelope.ExpiresAtUtc < DateTime.UtcNow)
            return new AiChatResponse { Reply = "Yêu cầu xác nhận đã hết hạn. Bạn vui lòng thao tác lại." };

        if (envelope.UserId != user.UserId)
            return new AiChatResponse { Reply = "Yêu cầu xác nhận không khớp với tài khoản hiện tại." };

        var result = await _toolExecutor.ExecuteAsync(new AiToolCallRequest
        {
            Name = envelope.Action,
            Arguments = envelope.Arguments,
            IsConfirmed = true
        }, user, state, ct);

        await _distributedCache.RemoveAsync(confirmationKey, ct);
        return ToResponse(result, state);
    }

    private async Task<AiChatResponse> RunModelWithToolsAsync(string message, AiUserContext user, AiConversationState state, CancellationToken ct)
    {
        var client = _httpClientFactory.CreateClient("AiProvider");
        client.BaseAddress = new Uri(_options.BaseUrl.EndsWith('/') ? _options.BaseUrl : _options.BaseUrl + "/");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);

        var requestBody = new
        {
            model = _options.Model,
            messages = new object[]
            {
                new { role = "system", content = BuildSystemPrompt(user) },
                new { role = "user", content = message }
            },
            tools = AiToolSchemas.GetOpenAiTools(),
            tool_choice = "auto",
            temperature = 0.1
        };

        var response = await client.PostAsync("chat/completions",
            new StringContent(JsonSerializer.Serialize(requestBody, AiJson.Options), Encoding.UTF8, "application/json"), ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"AI provider error: {response.StatusCode}");

        using var doc = JsonDocument.Parse(body);
        var messageElement = doc.RootElement.GetProperty("choices")[0].GetProperty("message");
        if (!messageElement.TryGetProperty("tool_calls", out var toolCalls) || toolCalls.GetArrayLength() == 0)
        {
            var content = messageElement.TryGetProperty("content", out var contentElement) ? contentElement.GetString() : null;
            return new AiChatResponse { Reply = string.IsNullOrWhiteSpace(content) ? "Mình chưa hiểu yêu cầu. Bạn nói rõ hơn giúp mình nhé." : content };
        }

        var firstCall = toolCalls[0].GetProperty("function");
        var toolName = firstCall.GetProperty("name").GetString() ?? "";
        var argumentsJson = firstCall.GetProperty("arguments").GetString() ?? "{}";
        var arguments = JsonSerializer.Deserialize<JsonElement>(argumentsJson);

        var toolResult = await ExecuteOrQueueToolAsync(toolName, arguments, user, state, ct);
        return ToResponse(toolResult, state);
    }

    private async Task<AiChatResponse> RunRuleBasedAsync(string message, AiUserContext user, AiConversationState state, CancellationToken ct)
    {
        if (TryBuildDeterministicToolCall(message, state, out var toolName, out var args))
        {
            var result = await ExecuteOrQueueToolAsync(toolName, args, user, state, ct);
            return ToResponse(result, state);
        }

        var normalized = NormalizeText(message);
        if (state.LastCars.Count > 0 && (IsBookingCapabilityQuestion(normalized) || normalized.Contains("muon thue xe") || normalized.Contains("can thue xe")))
        {
            return new AiChatResponse
            {
                Reply = "Mình đang có danh sách xe phù hợp rồi. Bạn có thể chọn một xe để tính giá hoặc đặt xe; nếu muốn nhanh, mình có thể lấy xe đáng tiền nhất trong danh sách.",
                SuggestedReplies = BuildSuggestedReplies(state)
            };
        }

        if (normalized.Contains("la sao") || normalized.Contains("giai thich"))
        {
            return new AiChatResponse
            {
                Reply = state.LastCars.Count > 0
                    ? $"Mình vừa tìm thấy {state.LastCars.Count} xe phù hợp. Bạn có thể bấm Xem chi tiết, Tính giá hoặc Đặt xe trên từng xe; nếu muốn, bạn cũng có thể nhắn 'tính giá chiếc đầu tiên'."
                    : "Ý mình là bạn hãy cho mình thêm nhu cầu thuê xe: khu vực, ngày nhận/trả, số chỗ, tự lái hay có tài xế và ngân sách dự kiến."
            };
        }

        if (IsBookingCapabilityQuestion(normalized) || normalized.Contains("muon thue xe") || normalized.Contains("can thue xe"))
        {
            return new AiChatResponse
            {
                Reply = state.LastCars.Count > 0
                    ? $"Mình đang có {state.LastCars.Count} xe vừa tìm được. Bạn chọn một xe trong danh sách rồi bấm Tính giá hoặc Đặt xe nhé. Nếu muốn nhanh, bạn có thể nhắn 'đặt chiếc đầu tiên'."
                    : "Được chứ, mình có thể tìm xe, tính giá và chuẩn bị đơn cho bạn. Bạn cho mình biết khu vực, ngày nhận/trả xe, số chỗ, tự lái hay có tài xế và ngân sách dự kiến nhé."
            };
        }

        return BuildConciergeFallback(message, state);
    }

    private async Task<AiChatResponse> RunOpsModeAsync(string message, AiUserContext user, AiConversationState state, CancellationToken ct)
    {
        if (!user.IsInternalStaff)
        {
            return new AiChatResponse
            {
                Reply = "Operations Copilot chỉ dành cho Admin, Manager hoặc Staff.",
                RequiresLogin = !user.IsAuthenticated
            };
        }

        var normalized = NormalizeText(message);
        var toolName = "getOpsSnapshot";
        var args = ToArgs(new { });

        if (IsOpsMoneyIntent(normalized))
        {
            toolName = "explainMoneyExceptions";
        }
        else if (IsOpsCameraIntent(normalized))
        {
            toolName = "summarizeCameraRisk";
        }
        else if (IsOpsActionIntent(normalized))
        {
            toolName = "recommendNextActions";
        }
        else if (IsOpsVehicleTimelineIntent(normalized))
        {
            toolName = "inspectVehicleTimeline";
            args = ToArgs(new
            {
                carId = ExtractCarIdForOps(normalized),
                licensePlate = ExtractLicensePlateHint(message)
            });
        }

        var result = await _toolExecutor.ExecuteAsync(new AiToolCallRequest
        {
            Name = toolName,
            Arguments = args
        }, user, state, ct);

        return ToResponse(result, state);
    }

    private static bool IsOpsMode(AiChatRequest request) =>
        string.Equals(request.Mode, "ops", StringComparison.OrdinalIgnoreCase);

    private static bool IsOpsMoneyIntent(string normalized) =>
        normalized.Contains("payos") ||
        normalized.Contains("ledger") ||
        normalized.Contains("refund") ||
        normalized.Contains("payout") ||
        normalized.Contains("thanh toan") ||
        normalized.Contains("tien") ||
        normalized.Contains("doi soat") ||
        normalized.Contains("lech");

    private static bool IsOpsCameraIntent(string normalized) =>
        normalized.Contains("camera") ||
        normalized.Contains("anpr") ||
        normalized.Contains("bien so") ||
        normalized.Contains("plate") ||
        normalized.Contains("mismatch") ||
        normalized.Contains("unknown");

    private static bool IsOpsActionIntent(string normalized) =>
        normalized.Contains("viec can lam") ||
        normalized.Contains("lam gi") ||
        normalized.Contains("next action") ||
        normalized.Contains("recommend") ||
        normalized.Contains("de xuat") ||
        normalized.Contains("checklist") ||
        normalized.Contains("xu ly tiep");

    private static bool IsOpsVehicleTimelineIntent(string normalized) =>
        normalized.Contains("timeline") ||
        normalized.Contains("gps") ||
        normalized.Contains("tracking") ||
        normalized.Contains("xe") ||
        Regex.IsMatch(normalized, @"\b\d{2}[a-z]\s*-?\s*\d{3,5}");

    private static int? ExtractCarIdForOps(string normalized)
    {
        var match = Regex.Match(normalized, @"(?:car|xe|id)\s*#?\s*(\d{1,9})");
        if (match.Success && int.TryParse(match.Groups[1].Value, out var id))
            return id;
        return null;
    }

    private static string? ExtractLicensePlateHint(string message)
    {
        var match = Regex.Match(message, @"\b\d{2}[A-Za-z][A-Za-z0-9]?\s*[-.]?\s*\d{3,5}(?:[.]?\d{2})?\b");
        return match.Success ? match.Value.Trim() : null;
    }

    private async Task<AiChatResponse> ContinueRentalNeedConversationAsync(AiConversationState state, AiUserContext user, CancellationToken ct)
    {
        if (HasEnoughInformationToSearch(state.RentalNeed))
        {
            var args = ToSearchArgsFromNeed(state.RentalNeed);
            var result = await ExecuteOrQueueToolAsync("searchCars", args, user, state, ct);
            return ToResponse(result, state);
        }

        return new AiChatResponse
        {
            Reply = BuildNaturalNeedFollowUp(state.RentalNeed),
            SuggestedReplies = BuildSuggestedReplies(state)
        };
    }

    private static bool TryBuildDeterministicToolCall(string message, AiConversationState state, out string toolName, out JsonElement args)
    {
        var normalized = NormalizeText(message);
        toolName = "answerPolicyQuestion";
        args = ToArgs(new { question = message });

        if (string.IsNullOrWhiteSpace(normalized))
            return false;

        if (IsProfileIntent(normalized))
        {
            toolName = "checkUserProfile";
            args = ToArgs(new { });
            return true;
        }

        if (IsCancelIntent(normalized))
        {
            toolName = "cancelBooking";
            args = ToArgs(new { bookingId = ExtractFirstNumber(normalized), reason = message });
            return true;
        }

        if (IsBookingStatusIntent(normalized))
        {
            toolName = "getBookingStatus";
            args = ToArgs(new { bookingId = ExtractFirstNumber(normalized) });
            return true;
        }

        if (IsDriverIntent(normalized))
        {
            toolName = "findDriver";
            args = ToArgs(new
            {
                pickupLocation = ExtractLocationHint(message),
                destination = message,
                startDate = ExtractStartDate(message)?.ToString("O") ?? DateTime.Today.AddDays(1).AddHours(8).ToString("O"),
                tripType = normalized.Contains("mot chieu") ? "lien_tinh_mot_chieu" : normalized.Contains("lien tinh") ? "lien_tinh" : "noi_thanh"
            });
            return true;
        }

        if (IsCarDetailIntent(normalized))
        {
            var selectedCar = ResolveReferencedCar(normalized, state);
            toolName = "getCarDetail";
            args = ToArgs(new { carId = selectedCar?.CarId ?? ExtractFirstNumber(normalized) });
            return true;
        }

        if (IsCreateBookingIntent(normalized))
        {
            var selectedCar = ResolveReferencedCar(normalized, state);
            if (selectedCar == null && state.LastCars.Count == 0 && ExtractFirstNumber(normalized) <= 0)
            {
                return false;
            }

            toolName = "createBooking";
            var start = ExtractStartDate(message) ?? state.RentalNeed.StartDate ?? DateTime.Today.AddDays(1).AddHours(8);
            var end = ExtractEndDate(message, start) ?? state.RentalNeed.EndDate ?? start.AddDays(1);
            args = ToArgs(new
            {
                carId = selectedCar?.CarId ?? ExtractFirstNumber(normalized),
                startDate = start.ToString("O"),
                endDate = end.ToString("O"),
                pickupLocation = selectedCar?.Location ?? ExtractLocationHint(message).NullIfWhiteSpace() ?? state.RentalNeed.Location,
                returnLocation = selectedCar?.Location ?? ExtractLocationHint(message).NullIfWhiteSpace() ?? state.RentalNeed.Location,
                rentalType = ExtractRentalType(normalized, state.RentalNeed.RentalType),
                note = message
            });
            return true;
        }

        if (IsSuggestIntent(normalized))
        {
            toolName = "suggestCars";
            args = ToArgs(new
            {
                purpose = message,
                passengers = ExtractSeatCount(normalized) ?? ExtractPassengerCount(normalized) ?? 5,
                budget = ExtractBudget(normalized),
                location = ExtractLocationHint(message).NullIfWhiteSpace() ?? state.RentalNeed.Location,
                rentalType = ExtractRentalType(normalized, state.RentalNeed.RentalType)
            });
            return true;
        }

        if (IsSearchCarsIntent(normalized))
        {
            var start = ExtractStartDate(message);
            var end = ExtractEndDate(message, start);
            toolName = "searchCars";
            args = ToArgs(new
            {
                location = ExtractLocationHint(message).NullIfWhiteSpace() ?? state.RentalNeed.Location,
                startDate = (start ?? state.RentalNeed.StartDate)?.ToString("O"),
                endDate = (end ?? state.RentalNeed.EndDate)?.ToString("O"),
                seats = ExtractSeatCount(normalized) ?? ExtractPassengerCount(normalized) ?? state.RentalNeed.Seats,
                transmission = ExtractTransmission(normalized),
                fuelType = ExtractFuelType(normalized),
                rentalType = ExtractRentalType(normalized, state.RentalNeed.RentalType),
                maxPrice = ExtractBudget(normalized) ?? state.RentalNeed.MaxBudget
            });
            return true;
        }

        if (IsPriceIntent(normalized))
        {
            var selectedCar = ResolveReferencedCar(normalized, state);
            var start = ExtractStartDate(message) ?? state.RentalNeed.StartDate ?? DateTime.Today.AddDays(1).AddHours(8);
            var end = ExtractEndDate(message, start) ?? state.RentalNeed.EndDate ?? start.AddDays(1);
            toolName = "calculateRentalPrice";
            args = ToArgs(new
            {
                carId = selectedCar?.CarId ?? ExtractFirstNumber(normalized),
                startDate = start.ToString("O"),
                endDate = end.ToString("O"),
                rentalPackage = ExtractRentalPackage(normalized),
                rentalType = ExtractRentalType(normalized, state.RentalNeed.RentalType),
                driverService = ExtractRentalType(normalized, state.RentalNeed.RentalType) == "co_tai_xe"
            });
            return true;
        }

        return false;
    }

    private async Task<AiToolResult> ExecuteOrQueueToolAsync(string toolName, JsonElement arguments, AiUserContext user, AiConversationState state, CancellationToken ct)
    {
        var result = await _toolExecutor.ExecuteAsync(new AiToolCallRequest { Name = toolName, Arguments = arguments }, user, state, ct);
        if (result.PendingAction != null)
        {
            var confirmationId = result.PendingAction.ConfirmationId;
            var envelope = new AiConfirmationEnvelope
            {
                Action = toolName,
                Arguments = arguments,
                Summary = result.PendingAction.Summary,
                UserId = user.UserId,
                ExpiresAtUtc = DateTime.UtcNow.AddMinutes(10)
            };
            await _distributedCache.SetStringAsync(
                $"ai_confirm:{confirmationId}",
                JsonSerializer.Serialize(envelope, JsonOptions),
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10) },
                ct);
        }
        return result;
    }

    private static AiChatResponse ToResponse(AiToolResult result, AiConversationState state)
    {
        if (result.Data is AiPriceQuote quote)
            state.LastQuote = quote;

        var response = new AiChatResponse
        {
            Reply = result.Message,
            PendingAction = result.PendingAction,
            RequiresLogin = result.RequiresLogin,
            SuggestedReplies = BuildSuggestedReplies(state, result)
        };
        if (result.Data != null) response.Data.Add(result.Data);
        return response;
    }

    private static string BuildSystemPrompt(AiUserContext user) => $"""
        Bạn là SmartCar AI, trợ lý tiếng Việt cho web/app thuê xe ô tô.
        Chỉ thao tác bằng tool/function calling, không tự click giao diện, không bịa dữ liệu.
        Role hiện tại: {user.Role}. UserId: {user.UserId?.ToString() ?? "Guest"}.
        Guest chỉ được tìm xe, xem giá và hỏi chính sách.
        Ưu tiên searchCars cho câu có ý định tìm xe hoặc có ngân sách dạng "giá dưới".
        Chỉ dùng calculateRentalPrice khi người dùng muốn tính giá cho một xe cụ thể.
        Mọi thao tác tạo đơn, hủy đơn, đổi lịch, cập nhật chuyến phải chờ backend xác nhận.
        Nếu thiếu thông tin thì hỏi lại ngắn gọn.
        Không tiết lộ API key, token, mật khẩu, secret hoặc dữ liệu người dùng khác.
        """;

    private static bool IsSearchCarsIntent(string normalized) =>
        normalized.Contains("tim xe") ||
        normalized.Contains("tim cho toi xe") ||
        normalized.Contains("can xe") ||
        normalized.Contains("xe ") && (normalized.Contains(" o ") || normalized.Contains(" tai ") || normalized.Contains("tp.hcm") || normalized.Contains("hcm")) ||
        Regex.IsMatch(normalized, @"\b\d+\s*(cho|nguoi)\b") && !IsPriceIntent(normalized);

    private static bool IsPriceIntent(string normalized) =>
        (normalized.Contains("tinh gia") || normalized.Contains("gia thue xe nay") || normalized.Contains("tinh tien") || normalized.Contains("bao gia"))
        && !normalized.Contains("gia duoi")
        && !normalized.Contains("duoi ");

    private static bool IsCreateBookingIntent(string normalized) =>
        normalized.Contains("dat chiec") ||
        normalized.Contains("dat xe") ||
        normalized.Contains("dat giup") ||
        normalized.Contains("thue xe nay") ||
        normalized.Contains("muon thue xe nay");

    private static bool IsBookingCapabilityQuestion(string normalized) =>
        (normalized.Contains("co the") || normalized.Contains("duoc khong") || normalized.Contains("giup toi"))
        && (normalized.Contains("dat xe") || normalized.Contains("thue xe") || normalized.Contains("thao tac"));

    private static bool IsCancelIntent(string normalized) => normalized.Contains("huy don") || normalized.Contains("huy booking");
    private static bool IsBookingStatusIntent(string normalized) => normalized.Contains("trang thai") || normalized.Contains("don dang o dau");
    private static bool IsProfileIntent(string normalized) => normalized.Contains("ho so") || normalized.Contains("thieu gi") || normalized.Contains("cccd") && normalized.Contains("gplx");
    private static bool IsDriverIntent(string normalized) =>
        normalized.Contains("tim tai xe") ||
        normalized.Contains("tim cho toi tai xe") ||
        normalized.Contains("can tai xe") ||
        normalized.Contains("tai xe cho chuyen");
    private static bool IsCarDetailIntent(string normalized) => normalized.Contains("xem chi tiet") || normalized.Contains("chi tiet xe") || normalized.Contains("thong tin xe");
    private static bool IsSuggestIntent(string normalized) => normalized.Contains("goi y") || normalized.Contains("gia dinh") || normalized.Contains("di du lich") || normalized.Contains("cong tac");

    private static bool TryBuildComparisonResponse(string message, AiConversationState state, out AiChatResponse response)
    {
        response = new AiChatResponse();
        var normalized = NormalizeText(message);
        if (!IsCompareIntent(normalized))
            return false;

        if (state.LastCars.Count == 0)
        {
            response = new AiChatResponse
            {
                Reply = "Mình chưa có danh sách xe để so sánh. Bạn cho mình khu vực, ngày thuê, số chỗ và ngân sách trước nhé.",
                SuggestedReplies = BuildSuggestedReplies(state)
            };
            return true;
        }

        var count = Math.Clamp(ExtractFirstNumber(normalized) > 0 ? ExtractFirstNumber(normalized) : 3, 2, Math.Min(5, state.LastCars.Count));
        var cars = state.LastCars.Take(count).ToList();
        var cheapest = cars.OrderBy(c => c.PricePerDay).First();
        var spacious = cars.OrderByDescending(c => c.Seats).ThenBy(c => c.PricePerDay).First();
        var bestValue = cars.OrderBy(c => c.PricePerDay / Math.Max(1, c.Seats)).First();
        var lines = cars.Select((car, index) =>
            $"{index + 1}. {car.Name}: {car.Seats} chỗ, {car.FuelType}, {car.Transmission}, {car.PricePerDay:n0}đ/ngày, khu vực {car.Location ?? "chưa rõ"}.");

        response = new AiChatResponse
        {
            Reply = $"Mình so sánh nhanh {cars.Count} xe đầu cho bạn:\n{string.Join("\n", lines)}\n\nNổi bật: rẻ nhất là {cheapest.Name}; rộng rãi nhất là {spacious.Name}; đáng tiền nhất là {bestValue.Name}.",
            Data = { new { type = "carComparison", cars, highlights = new { cheapest = cheapest.CarId, spacious = spacious.CarId, bestValue = bestValue.CarId } } },
            SuggestedReplies = new List<string>
            {
                $"Tính giá xe {bestValue.CarId}",
                $"Đặt xe {bestValue.CarId}",
                "Xem chi tiết chiếc đầu tiên"
            }
        };
        return true;
    }

    private static bool IsCompareIntent(string normalized) =>
        normalized.Contains("so sanh") ||
        normalized.Contains("khac nhau") ||
        normalized.Contains("xe nao tot") ||
        normalized.Contains("xe nao hop") ||
        normalized.Contains("dang thue nhat");

    private static bool UpdateRentalNeedFromMessage(string message, AiConversationState state)
    {
        var normalized = NormalizeText(message);
        var changed = false;
        var need = state.RentalNeed;

        var location = ExtractLocationHint(message).NullIfWhiteSpace() ?? ExtractStandaloneLocation(message);
        if (!string.IsNullOrWhiteSpace(location))
        {
            need.Location = location;
            changed = true;
        }

        var seats = ExtractSeatCount(normalized) ?? ExtractPassengerCount(normalized);
        if (seats.HasValue && seats.Value > 0)
        {
            need.Seats = seats.Value;
            changed = true;
        }

        var start = ExtractStartDate(message);
        var end = ExtractEndDate(message, start);
        if (start.HasValue)
        {
            need.StartDate = start;
            need.EndDate = end ?? start.Value.AddDays(1);
            changed = true;
        }

        var budget = ExtractBudgetRange(normalized);
        if (budget.min.HasValue || budget.max.HasValue)
        {
            need.MinBudget = budget.min;
            need.MaxBudget = budget.max;
            need.BudgetPeriod = budget.period;
            changed = true;
        }

        var rentalType = ExtractRentalType(normalized, need.RentalType);
        if (rentalType != need.RentalType)
        {
            need.RentalType = rentalType;
            changed = true;
        }

        return changed;
    }

    private static bool IsRentalNeedOnlyMessage(string message)
    {
        var normalized = NormalizeText(message);
        if (IsSearchCarsIntent(normalized) || IsPriceIntent(normalized) || IsCreateBookingIntent(normalized) ||
            IsCancelIntent(normalized) || IsBookingStatusIntent(normalized) || IsProfileIntent(normalized) ||
            IsDriverIntent(normalized) || IsCarDetailIntent(normalized) || IsSuggestIntent(normalized))
            return false;

        return normalized.Contains("khu vuc") ||
               normalized.Contains("ngan sach") ||
               normalized.Contains("budget") ||
               normalized.Contains("tu lai") ||
               normalized.Contains("co tai xe") ||
               normalized.Contains("tu ngay") ||
               normalized.Contains("ngay mai") ||
               Regex.IsMatch(normalized, @"\b\d{1,2}/\d{1,2}\s*(den|-|toi)\s*\d{1,2}/?\d{0,2}\b") ||
               Regex.IsMatch(normalized, @"\b\d+\s*(trieu|cho|nguoi|tuan|ngay)\b");
    }

    private static bool HasEnoughInformationToSearch(AiRentalNeed need) =>
        !string.IsNullOrWhiteSpace(need.Location) &&
        need.Seats.HasValue &&
        (need.StartDate.HasValue || need.MaxBudget.HasValue);

    private static JsonElement ToSearchArgsFromNeed(AiRentalNeed need) => ToArgs(new
    {
        location = need.Location,
        startDate = need.StartDate?.ToString("O"),
        endDate = need.EndDate?.ToString("O"),
        seats = need.Seats,
        rentalType = need.RentalType,
        maxPrice = NormalizeBudgetToDailyMax(need)
    });

    private static string BuildNaturalNeedFollowUp(AiRentalNeed need)
    {
        var known = new List<string>();
        if (!string.IsNullOrWhiteSpace(need.Location)) known.Add($"khu vực {need.Location}");
        if (need.Seats.HasValue) known.Add($"{need.Seats} chỗ");
        if (need.MaxBudget.HasValue)
        {
            var budget = need.MinBudget.HasValue
                ? $"{need.MinBudget.Value:n0}-{need.MaxBudget.Value:n0}đ"
                : $"tối đa {need.MaxBudget.Value:n0}đ";
            known.Add($"ngân sách {budget}{FormatBudgetPeriod(need.BudgetPeriod)}");
        }
        if (need.StartDate.HasValue) known.Add($"nhận xe {need.StartDate.Value:dd/MM}");

        var missing = new List<string>();
        if (string.IsNullOrWhiteSpace(need.Location)) missing.Add("khu vực nhận xe");
        if (!need.Seats.HasValue) missing.Add("số chỗ hoặc số người đi");
        if (!need.StartDate.HasValue) missing.Add("ngày nhận và trả xe");

        var prefix = known.Count > 0
            ? $"Mình ghi nhận {string.Join(", ", known)} rồi."
            : "Mình sẽ giúp bạn tìm xe phù hợp.";
        return missing.Count == 0
            ? $"{prefix} Mình sẽ bắt đầu tìm xe phù hợp cho bạn nhé."
            : $"{prefix} Bạn cho mình thêm {string.Join(", ", missing)} là mình tìm xe được ngay.";
    }

    private static AiChatResponse BuildConciergeFallback(string message, AiConversationState state)
    {
        var normalized = NormalizeText(message);
        if (state.LastCars.Count > 0)
        {
            return new AiChatResponse
            {
                Reply = "Mình đang giữ danh sách xe vừa tìm được cho bạn. Bạn có thể yêu cầu so sánh, tính giá, xem chi tiết hoặc đặt một xe cụ thể.",
                SuggestedReplies = BuildSuggestedReplies(state)
            };
        }

        if (normalized.Contains("dat") || normalized.Contains("thue") || normalized.Contains("xe"))
        {
            return new AiChatResponse
            {
                Reply = BuildNaturalNeedFollowUp(state.RentalNeed),
                SuggestedReplies = BuildSuggestedReplies(state)
            };
        }

        return new AiChatResponse
        {
            Reply = "Mình có thể làm trợ lý thuê xe cho bạn: tìm xe, gợi ý xe phù hợp, so sánh, tính giá và chuẩn bị đặt xe qua hệ thống. Bạn cứ nói nhu cầu theo kiểu tự nhiên, ví dụ: \"Tìm xe 7 chỗ ở Đồng Nai từ 20/5 đến 22/5\".",
            SuggestedReplies = BuildSuggestedReplies(state)
        };
    }

    private static List<string> BuildSuggestedReplies(AiConversationState state, AiToolResult? result = null)
    {
        if (result?.PendingAction != null)
            return new List<string>();

        if (state.LastQuote != null)
            return new List<string> { $"Đặt xe {state.LastQuote.CarId}", "Kiểm tra hồ sơ của tôi", "Tìm xe khác" };

        if (state.LastCars.Count > 0)
        {
            var first = state.LastCars[0].CarId;
            var best = state.LastCars.OrderBy(c => c.PricePerDay / Math.Max(1, c.Seats)).First().CarId;
            return new List<string>
            {
                "So sánh 3 xe đầu",
                $"Tính giá xe {first}",
                $"Đặt xe {best}"
            };
        }

        var suggestions = new List<string>();
        if (string.IsNullOrWhiteSpace(state.RentalNeed.Location)) suggestions.Add("Khu vực TP.HCM");
        if (!state.RentalNeed.Seats.HasValue) suggestions.Add("Xe 7 chỗ");
        if (!state.RentalNeed.StartDate.HasValue) suggestions.Add("Từ ngày 20/5 đến 22/5");
        if (!state.RentalNeed.MaxBudget.HasValue) suggestions.Add("Ngân sách dưới 2 triệu/ngày");
        suggestions.Add("Thuê xe tự lái");
        return suggestions.Take(4).ToList();
    }

    private static AiCarSummary? ResolveReferencedCar(string normalized, AiConversationState state)
    {
        if (state.LastCars.Count == 0) return null;
        if (normalized.Contains("re nhat") || normalized.Contains("tiet kiem"))
            return state.LastCars.OrderBy(c => c.PricePerDay).First();
        if (normalized.Contains("rong nhat") || normalized.Contains("gia dinh") || normalized.Contains("nhieu cho"))
            return state.LastCars.OrderByDescending(c => c.Seats).ThenBy(c => c.PricePerDay).First();
        if (normalized.Contains("phu hop nhat") || normalized.Contains("dang tien") || normalized.Contains("tot nhat"))
            return state.LastCars.OrderBy(c => c.PricePerDay / Math.Max(1, c.Seats)).First();
        if (normalized.Contains("dau tien") || normalized.Contains("so 1") || normalized.Contains("thu nhat") || normalized.Contains("xe nay"))
            return state.LastCars[0];
        if (normalized.Contains("thu hai") || normalized.Contains("so 2"))
            return state.LastCars.Count > 1 ? state.LastCars[1] : state.LastCars[0];
        if (normalized.Contains("thu ba") || normalized.Contains("so 3"))
            return state.LastCars.Count > 2 ? state.LastCars[2] : state.LastCars[0];
        return null;
    }

    private static int ExtractFirstNumber(string text)
    {
        var match = Regex.Match(text, @"\d+");
        return match.Success && int.TryParse(match.Value, out var number) ? number : 0;
    }

    private static int? ExtractSeatCount(string normalized)
    {
        var match = Regex.Match(normalized, @"(\d+)\s*cho");
        return match.Success && int.TryParse(match.Groups[1].Value, out var seats) ? seats : null;
    }

    private static int? ExtractPassengerCount(string normalized)
    {
        var match = Regex.Match(normalized, @"(\d+)\s*nguoi");
        return match.Success && int.TryParse(match.Groups[1].Value, out var passengers) ? passengers : null;
    }

    private static decimal? ExtractBudget(string normalized)
    {
        var range = ExtractBudgetRange(normalized);
        if (range.max.HasValue) return NormalizeBudgetToDailyMax(range.max.Value, range.period);

        var raw = Regex.Match(normalized, @"(?:duoi|toi da|max|ngan sach|gia duoi)\s*(\d{6,})");
        return raw.Success && decimal.TryParse(raw.Groups[1].Value, out var value) ? value : null;
    }

    private static (decimal? min, decimal? max, string? period) ExtractBudgetRange(string normalized)
    {
        var period = normalized.Contains("tuan") || normalized.Contains("week")
            ? "weekly"
            : normalized.Contains("thang") || normalized.Contains("month")
                ? "monthly"
                : "daily";

        var range = Regex.Match(normalized, @"(\d+(?:[\.,]\d+)?)\s*(?:-|den|toi)\s*(\d+(?:[\.,]\d+)?)\s*(?:tr|trieu)");
        if (range.Success &&
            decimal.TryParse(range.Groups[1].Value.Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out var min) &&
            decimal.TryParse(range.Groups[2].Value.Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out var max))
        {
            return (min * 1_000_000m, max * 1_000_000m, period);
        }

        var million = Regex.Match(normalized, @"(?:duoi|toi da|max|ngan sach|gia duoi)?\s*(\d+(?:[\.,]\d+)?)\s*(?:tr|trieu)");
        if (million.Success && decimal.TryParse(million.Groups[1].Value.Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out var millionValue))
            return (null, millionValue * 1_000_000m, period);

        var raw = Regex.Match(normalized, @"(?:duoi|toi da|max|ngan sach|gia duoi)\s*(\d{6,})");
        if (raw.Success && decimal.TryParse(raw.Groups[1].Value, out var value))
            return (null, value, period);

        return (null, null, null);
    }

    private static string ExtractLocationHint(string text)
    {
        var normalized = NormalizeText(text);
        if (normalized.Contains("tp.hcm") || normalized.Contains("tphcm") || normalized.Contains("hcm") || normalized.Contains("sai gon") || normalized.Contains("saigon"))
            return "TP.HCM";

        var plainMatch = Regex.Match(normalized, @"(?:\bo\b|\btai\b)\s+(.+?)(?:\s+(?:ngay|tu|gia|duoi|ngan sach)|,|$)");
        if (plainMatch.Success)
            return CultureInfo.CurrentCulture.TextInfo.ToTitleCase(plainMatch.Groups[1].Value.Trim());

        var match = Regex.Match(text, @"(?:ở|o|tại|tai)\s+(.+?)(?:\s+(?:ngày|ngay|từ|tu|giá|gia|dưới|duoi|,)|$)", RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value.Trim() : "";
    }

    private static string? ExtractStandaloneLocation(string text)
    {
        var plain = NormalizeText(text);
        var normalizedLocation = Regex.Match(plain, @"(?:khu vuc|dia diem)\s+(.+?)(?:,|$)");
        if (normalizedLocation.Success)
            return CultureInfo.CurrentCulture.TextInfo.ToTitleCase(normalizedLocation.Groups[1].Value.Trim());

        var match = Regex.Match(text, @"(?:khu\s*vực|khu vuc|địa điểm|dia diem)\s+(.+?)(?:,|$)", RegexOptions.IgnoreCase);
        if (match.Success)
            return match.Groups[1].Value.Trim();

        var normalized = NormalizeText(text);
        if (normalized is "dong nai" or "binh duong" or "tp.hcm" or "hcm" or "sai gon")
            return text.Trim();

        return null;
    }

    private static DateTime? ExtractStartDate(string message)
    {
        var normalized = NormalizeText(message);
        if (Regex.IsMatch(normalized, @"\b\d+(?:[\.,]\d+)?\s*(?:-|den|toi)\s*\d+(?:[\.,]\d+)?\s*(?:tr|trieu)\b"))
            return null;

        if (normalized.Contains("ngay mai"))
            return DateTime.Today.AddDays(1).AddHours(8);

        var range = Regex.Match(normalized, @"(?:tu\s+ngay\s+|tu\s+|ngay\s+)?(\d{1,2})(?:/(\d{1,2}))?\s*(?:den|-|toi)\s*(\d{1,2})(?:/(\d{1,2}))?");
        if (range.Success)
            return BuildDate(range.Groups[1].Value, range.Groups[2].Value);

        var single = Regex.Match(normalized, @"ngay\s+(\d{1,2})(?:/(\d{1,2}))?");
        return single.Success ? BuildDate(single.Groups[1].Value, single.Groups[2].Value) : null;
    }

    private static DateTime? ExtractEndDate(string message, DateTime? start)
    {
        var normalized = NormalizeText(message);
        if (Regex.IsMatch(normalized, @"\b\d+(?:[\.,]\d+)?\s*(?:-|den|toi)\s*\d+(?:[\.,]\d+)?\s*(?:tr|trieu)\b"))
            return start?.AddDays(1);

        var range = Regex.Match(normalized, @"(?:tu\s+ngay\s+|tu\s+|ngay\s+)?(\d{1,2})(?:/(\d{1,2}))?\s*(?:den|-|toi)\s*(\d{1,2})(?:/(\d{1,2}))?");
        if (range.Success)
        {
            var month = range.Groups[4].Success ? range.Groups[4].Value : range.Groups[2].Value;
            var end = BuildDate(range.Groups[3].Value, month);
            if (start.HasValue && end <= start.Value) end = end.AddMonths(1);
            return end;
        }

        return start?.AddDays(1);
    }

    private static DateTime BuildDate(string dayText, string? monthText)
    {
        var day = int.Parse(dayText);
        var month = int.TryParse(monthText, out var parsedMonth) ? parsedMonth : DateTime.Today.Month;
        var year = DateTime.Today.Year;
        var date = new DateTime(year, month, day, 8, 0, 0);
        if (date.Date < DateTime.Today.Date) date = date.AddYears(1);
        return date;
    }

    private static string ExtractRentalType(string normalized) => ExtractRentalType(normalized, "tu_lai");

    private static string ExtractRentalType(string normalized, string current) =>
        normalized.Contains("co tai") || normalized.Contains("tai xe") || normalized.Contains("with driver")
            ? "co_tai_xe"
            : normalized.Contains("tu lai") || normalized.Contains("self drive")
                ? "tu_lai"
                : current;

    private static string? ExtractTransmission(string normalized)
    {
        if (normalized.Contains("so tu dong") || normalized.Contains("tu dong")) return "tự động";
        if (normalized.Contains("so san")) return "số sàn";
        return null;
    }

    private static string? ExtractFuelType(string normalized)
    {
        if (normalized.Contains("dien")) return "điện";
        if (normalized.Contains("dau")) return "dầu";
        if (normalized.Contains("xang")) return "xăng";
        return null;
    }

    private static string ExtractRentalPackage(string normalized)
    {
        foreach (var package in new[] { "1h", "4h", "8h", "12h", "24h" })
        {
            if (normalized.Contains(package)) return package;
        }
        if (normalized.Contains("thang")) return "monthly";
        if (normalized.Contains("tuan")) return "weekly";
        return "daily";
    }

    private static JsonElement ToArgs(object value) =>
        JsonSerializer.Deserialize<JsonElement>(JsonSerializer.Serialize(value, AiJson.Options));

    private static decimal? NormalizeBudgetToDailyMax(AiRentalNeed need) =>
        need.MaxBudget.HasValue ? NormalizeBudgetToDailyMax(need.MaxBudget.Value, need.BudgetPeriod) : null;

    private static decimal NormalizeBudgetToDailyMax(decimal amount, string? period) => period switch
    {
        "weekly" => Math.Round(amount / 7m, 0, MidpointRounding.AwayFromZero),
        "monthly" => Math.Round(amount / 30m, 0, MidpointRounding.AwayFromZero),
        _ => amount
    };

    private static string FormatBudgetPeriod(string? period) => period switch
    {
        "weekly" => "/tuần",
        "monthly" => "/tháng",
        _ => "/ngày"
    };

    private static string NormalizeText(string text)
    {
        var normalized = text.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);
        foreach (var ch in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (category != UnicodeCategory.NonSpacingMark)
                builder.Append(ch == 'đ' ? 'd' : ch);
        }

        return Regex.Replace(builder.ToString().Normalize(NormalizationForm.FormC), @"\s+", " ");
    }
}

internal static class AiStringExtensions
{
    public static string? NullIfWhiteSpace(this string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;
}
