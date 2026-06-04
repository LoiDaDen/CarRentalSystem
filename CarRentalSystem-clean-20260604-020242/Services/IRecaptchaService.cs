namespace CarRentalSystem.Services;

public interface IRecaptchaService
{
    Task<RecaptchaVerifyResult> VerifyAsync(string recaptchaToken, string? expectedAction = null);
}

public class RecaptchaVerifyResult
{
    public bool Success { get; set; }
    public double Score { get; set; }
    public string? ErrorCodes { get; set; }
    public DateTime ChallengeTimestamp { get; set; }
    public string? Hostname { get; set; }
    public string? Action { get; set; }
}
