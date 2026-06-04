using System.Net.Http.Json;
using System.Text.Json;

namespace CarRentalSystem.Services;

public class RecaptchaService : IRecaptchaService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<RecaptchaService> _logger;
    private readonly HttpClient _httpClient;
    private const string VerifyUrl = "https://www.google.com/recaptcha/api/siteverify";

    public RecaptchaService(IConfiguration configuration, ILogger<RecaptchaService> logger, HttpClient httpClient)
    {
        _configuration = configuration;
        _logger = logger;
        _httpClient = httpClient;
    }

        public async Task<RecaptchaVerifyResult> VerifyAsync(string recaptchaToken, string? expectedAction = null)
        {
            if (string.IsNullOrEmpty(recaptchaToken))
            {
                return new RecaptchaVerifyResult
                {
                    Success = false,
                    ErrorCodes = "Token is empty"
                };
            }

            try
            {
                var secretKey = _configuration["Recaptcha:SecretKey"];

                // Security: KHÔNG bypass khi SecretKey null trong Production
                // Chỉ bypass trong Development khi key chưa được config
                if (string.IsNullOrEmpty(secretKey))
                {
                    var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
                    if (env == "Development")
                    {
                        _logger.LogWarning("reCAPTCHA SecretKey not configured in Development. Bypassing validation.");
                        return new RecaptchaVerifyResult
                        {
                            Success = true,
                            Score = 1.0,
                            ChallengeTimestamp = DateTime.UtcNow
                        };
                    }
                    else
                    {
                        // Production: FAIL thay vì bypass
                        _logger.LogError("reCAPTCHA SecretKey is not configured in Production!");
                        return new RecaptchaVerifyResult
                        {
                            Success = false,
                            ErrorCodes = "reCAPTCHA not configured"
                        };
                    }
                }

                var postData = new Dictionary<string, string>
                {
                    { "secret", secretKey },
                    { "response", recaptchaToken }
                };

                using var content = new FormUrlEncodedContent(postData);
                var response = await _httpClient.PostAsync(VerifyUrl, content);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("reCAPTCHA verification failed with status: {StatusCode}", response.StatusCode);
                return new RecaptchaVerifyResult
                {
                    Success = false,
                    ErrorCodes = $"HTTP Error: {response.StatusCode}"
                };
            }

            var jsonResponse = await response.Content.ReadAsStringAsync();
            _logger.LogDebug("reCAPTCHA response: {Response}", jsonResponse);

            using var document = JsonDocument.Parse(jsonResponse);
            var root = document.RootElement;

            var success = root.TryGetProperty("success", out var successElement) && successElement.GetBoolean();
            var score = root.TryGetProperty("score", out var scoreElement) ? scoreElement.GetDouble() : 1.0;
            var action = root.TryGetProperty("action", out var actionElement) ? actionElement.GetString() : null;
            var errorCodes = root.TryGetProperty("error-codes", out var errorElement)
                ? string.Join(", ", errorElement.EnumerateArray().Select(e => e.GetString()))
                : null;
            var challengeTimestamp = root.TryGetProperty("challenge_ts", out var tsElement)
                ? DateTime.TryParse(tsElement.GetString(), out var ts) ? ts : DateTime.UtcNow
                : DateTime.UtcNow;
            var hostname = root.TryGetProperty("hostname", out var hostElement) ? hostElement.GetString() : null;

            // Validate action only for reCAPTCHA v3 responses. v2 checkbox tokens do not include action.
            if (success && expectedAction != null && action != null && action != expectedAction)
            {
                _logger.LogWarning("reCAPTCHA action mismatch. Expected: {Expected}, Got: {Actual}", expectedAction, action);
                success = false;
                errorCodes = "action-mismatch";
            }

            // Check score threshold (0.5 is recommended minimum)
            if (success && score < 0.5)
            {
                _logger.LogWarning("reCAPTCHA score too low: {Score}. Possible bot activity.", score);
                success = false;
                errorCodes = "score-threshold-failed";
            }

            return new RecaptchaVerifyResult
            {
                Success = success,
                Score = score,
                ErrorCodes = errorCodes,
                ChallengeTimestamp = challengeTimestamp,
                Hostname = hostname,
                Action = action
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying reCAPTCHA token");
            return new RecaptchaVerifyResult
            {
                Success = false,
                ErrorCodes = $"Exception: {ex.Message}"
            };
        }
    }
}
