using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace CarRentalSystem.Services
{
    public interface IEkycService
    {
        Task<EkycIdResult> ExtractIdCardAsync(IFormFile frontImage, IFormFile backImage);
        Task<EkycLicenseResult> ExtractDrivingLicenseAsync(IFormFile licenseImage);
        Task<EkycFaceResult> CompareFaceAsync(IFormFile faceImage, IFormFile cardImage);
    }

    public class FptEkycService : IEkycService
    {
        private readonly HttpClient _httpClient;
        private readonly string? _apiKey;
        private readonly ILogger<FptEkycService> _logger;

        public FptEkycService(HttpClient httpClient, IConfiguration config, ILogger<FptEkycService> logger)
        {
            _httpClient = httpClient;
            _apiKey = config["FptAi:ApiKey"];
            _logger = logger;
        }

        private void ResetStream(IFormFile file)
        {
            if (file == null || file.Length <= 0) return;
            var stream = file.OpenReadStream();
            if (stream.CanSeek) stream.Position = 0;
        }

        private static async Task<byte[]> ReadFullyAsync(IFormFile file)
        {
            using var ms = new MemoryStream();
            await file.CopyToAsync(ms);
            return ms.ToArray();
        }

        public async Task<EkycIdResult> ExtractIdCardAsync(IFormFile frontImage, IFormFile backImage)
        {
            ResetStream(frontImage);
            ResetStream(backImage);

            // =========================================
            // FAIL-CLOSED: Không có API key = reject
            // Production phải có API key hợp lệ
            // =========================================
            if (string.IsNullOrWhiteSpace(_apiKey) || _apiKey == "YOUR_FPT_API_KEY")
            {
                _logger.LogError("eKYC OCR rejected: FPT API key not configured or using demo key.");
                return new EkycIdResult
                {
                    IsSuccess = false,
                    IsProviderError = true,
                    Message = "Dịch vụ xác thực CCCD chưa được cấu hình. Vui lòng liên hệ hỗ trợ."
                };
            }

            try
            {
                // Call FPT.AI IDR API for front side (extracts most data)
                var frontResult = await CallIdrApiAsync(frontImage);
                if (!frontResult.IsSuccess)
                {
                    return frontResult;
                }

                // Call FPT.AI IDR API for back side (extracted info on back of new CCCD)
                var backResult = await CallIdrApiAsync(backImage);
                if (backResult.IsSuccess && !string.IsNullOrEmpty(backResult.Features))
                {
                    frontResult.Features = backResult.Features;
                    frontResult.IssueDate = backResult.IssueDate;
                    frontResult.IssueLocation = backResult.IssueLocation;
                }

                // If back side returns issue_loc, use it
                if (!string.IsNullOrEmpty(backResult.IssueLocation) && string.IsNullOrEmpty(frontResult.IssueLocation))
                {
                    frontResult.IssueLocation = backResult.IssueLocation;
                }

                frontResult.Message = "ID Card recognized successfully via FPT.AI";
                return frontResult;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "FPT.AI IDR extraction failed");
                return new EkycIdResult
                {
                    IsSuccess = false,
                    IsProviderError = true,
                    Message = "OCR extraction failed: " + ex.Message
                };
            }
        }

        private async Task<EkycIdResult> CallIdrApiAsync(IFormFile image)
        {
            var bytes = await ReadFullyAsync(image);
            using var formData = new MultipartFormDataContent();
            var content = new ByteArrayContent(bytes);
            content.Headers.ContentType = MediaTypeHeaderValue.Parse(image.ContentType);
            formData.Add(content, "image", image.FileName);

            var request = new HttpRequestMessage(HttpMethod.Post, "https://api.fpt.ai/vision/idr/vnm");
            request.Headers.Add("api-key", _apiKey!);
            request.Content = formData;

            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("FPT.AI IDR API returned {StatusCode}", response.StatusCode);
                return new EkycIdResult { IsSuccess = false, IsProviderError = true, Message = "FPT.AI API error: " + response.StatusCode };
            }

            var jsonString = await response.Content.ReadAsStringAsync();
            LogProviderResponseMetadata("FPT.AI IDR", jsonString);

            var json = System.Text.Json.JsonDocument.Parse(jsonString);
            var root = json.RootElement;

            int errorCode = root.TryGetProperty("errorCode", out var ec) ? ec.GetInt32() : -1;
            if (errorCode != 0)
            {
                string errorMessage = root.TryGetProperty("errorMessage", out var em) ? em.GetString() ?? "Unknown error" : "Unknown error";
                _logger.LogWarning("FPT.AI IDR errorCode={ErrorCode}: {Message}", errorCode, errorMessage);
                return new EkycIdResult { IsSuccess = false, Message = $"FPT.AI error {errorCode}: {errorMessage}" };
            }

            var dataArray = root.GetProperty("data");
            if (dataArray.ValueKind == System.Text.Json.JsonValueKind.Array && dataArray.GetArrayLength() == 0)
            {
                return new EkycIdResult { IsSuccess = false, Message = "No ID card detected in image" };
            }

            var data = dataArray[0];

            // Determine card type
            string cardType = GetString(data, "type");
            string typeNew = GetString(data, "type_new");

            // Extract common fields
            string id = NormalizeIdNumber(GetString(data, "id"));
            string name = NormalizeVietnameseName(GetString(data, "name"));
            string dob = NormalizeDate(GetString(data, "dob"));
            string doe = NormalizeDate(GetString(data, "doe"));
            string sex = GetString(data, "sex");
            string nationality = GetString(data, "nationality");
            string address = GetString(data, "address");
            string home = GetString(data, "home");
            string ethnicity = GetString(data, "ethnicity");
            string religion = GetString(data, "religion");
            string features = GetString(data, "features");
            string issueDate = NormalizeDate(GetString(data, "issue_date"));
            string issueLoc = GetString(data, "issue_loc");

            // For new CCCD, doe (date of expiry) is available
            // For old CMND, doe is N/A
            if (string.IsNullOrEmpty(doe) || doe == "N/A")
            {
                doe = "20/10/2035"; // Default for new CCCD (20-year validity from issue)
            }

            return new EkycIdResult
            {
                IsSuccess = true,
                CitizenId = id,
                FullName = name,
                DateOfBirth = dob,
                Validity = doe,
                Gender = sex,
                Address = address,
                Home = home,
                Nationality = nationality,
                Ethnicity = ethnicity,
                Religion = religion,
                Features = features,
                IssueDate = issueDate,
                IssueLocation = issueLoc,
                CardType = cardType,
                Message = "OK"
            };
        }

        public async Task<EkycLicenseResult> ExtractDrivingLicenseAsync(IFormFile licenseImage)
        {
            ResetStream(licenseImage);

            // =========================================
            // FAIL-CLOSED: Không có API key = reject
            // Production phải có API key hợp lệ
            // =========================================
            if (string.IsNullOrWhiteSpace(_apiKey) || _apiKey == "YOUR_FPT_API_KEY")
            {
                _logger.LogError("eKYC GPLX rejected: FPT API key not configured or using demo key.");
                return new EkycLicenseResult
                {
                    IsSuccess = false,
                    IsProviderError = true,
                    Message = "Dịch vụ xác thực GPLX chưa được cấu hình. Vui lòng liên hệ hỗ trợ."
                };
            }

            try
            {
                var bytes = await ReadFullyAsync(licenseImage);
                using var formData = new MultipartFormDataContent();
                var content = new ByteArrayContent(bytes);
                content.Headers.ContentType = MediaTypeHeaderValue.Parse(licenseImage.ContentType);
                formData.Add(content, "image", licenseImage.FileName);

                var request = new HttpRequestMessage(HttpMethod.Post, "https://api.fpt.ai/vision/dlr/vnm");
                request.Headers.Add("api-key", _apiKey!);
                request.Content = formData;

                var response = await _httpClient.SendAsync(request);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("FPT.AI DLR API returned {StatusCode}", response.StatusCode);
                    return new EkycLicenseResult { IsSuccess = false, IsProviderError = true, Message = "FPT.AI API error: " + response.StatusCode };
                }

                var jsonString = await response.Content.ReadAsStringAsync();
                LogProviderResponseMetadata("FPT.AI DLR", jsonString);

                var json = System.Text.Json.JsonDocument.Parse(jsonString);
                var root = json.RootElement;

                int errorCode = root.TryGetProperty("errorCode", out var ec) ? ec.GetInt32() : -1;
                if (errorCode != 0)
                {
                    string errorMessage = root.TryGetProperty("errorMessage", out var em) ? em.GetString() ?? "Unknown error" : "Unknown error";
                    return new EkycLicenseResult { IsSuccess = false, Message = $"FPT.AI error {errorCode}: {errorMessage}" };
                }

                var dataArray = root.GetProperty("data");
                if (dataArray.ValueKind == System.Text.Json.JsonValueKind.Array && dataArray.GetArrayLength() == 0)
                {
                    return new EkycLicenseResult { IsSuccess = false, Message = "No driving license detected" };
                }

                var data = dataArray[0];

                string licenseNumber = NormalizeIdNumber(GetString(data, "id"));
                string name = NormalizeVietnameseName(GetString(data, "name"));
                string dob = NormalizeDate(GetString(data, "dob"));
                string licenseClass = GetString(data, "class");
                string nationality = GetString(data, "nationality");
                string address = GetString(data, "address");
                string issueDate = NormalizeDate(GetString(data, "issue_date"));
                string issueLoc = GetString(data, "issue_loc");
                string doe = NormalizeDate(GetString(data, "doe"));

                if (string.IsNullOrEmpty(doe) || doe == "N/A")
                {
                    doe = "01/01/2035";
                }

                return new EkycLicenseResult
                {
                    IsSuccess = true,
                    LicenseNumber = licenseNumber,
                    Class = licenseClass,
                    FullName = name,
                    DateOfBirth = dob,
                    Validity = doe,
                    Nationality = nationality,
                    Address = address,
                    IssueDate = issueDate,
                    IssueLocation = issueLoc,
                    Message = "Driving license recognized successfully"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "FPT.AI DLR extraction failed");
                return new EkycLicenseResult
                {
                    IsSuccess = false,
                    IsProviderError = true,
                    Message = "OCR extraction failed: " + ex.Message
                };
            }
        }

        public async Task<EkycFaceResult> CompareFaceAsync(IFormFile faceImage, IFormFile cardImage)
        {
            ResetStream(faceImage);
            ResetStream(cardImage);

            // =========================================
            // FAIL-CLOSED: Không có API key = reject
            // Face match phải được thực hiện bởi AI, không simulate được
            // =========================================
            if (string.IsNullOrWhiteSpace(_apiKey) || _apiKey == "YOUR_FPT_API_KEY")
            {
                _logger.LogError("eKYC Face Compare rejected: FPT API key not configured or using demo key.");
                return new EkycFaceResult
                {
                    IsSuccess = false,
                    IsProviderError = true,
                    SimilarityPercent = 0,
                    IsMatch = false,
                    Message = "Dịch vụ xác thực khuôn mặt chưa được cấu hình. Vui lòng liên hệ hỗ trợ."
                };
            }

            try
            {
                var faceBytes = await ReadFullyAsync(faceImage);
                var cardBytes = await ReadFullyAsync(cardImage);

                using var formData = new MultipartFormDataContent();
                var faceContent = new ByteArrayContent(faceBytes);
                faceContent.Headers.ContentType = MediaTypeHeaderValue.Parse(faceImage.ContentType);
                formData.Add(faceContent, "file[]", faceImage.FileName);

                var cardContent = new ByteArrayContent(cardBytes);
                cardContent.Headers.ContentType = MediaTypeHeaderValue.Parse(cardImage.ContentType);
                formData.Add(cardContent, "file[]", cardImage.FileName);

                var request = new HttpRequestMessage(HttpMethod.Post, "https://api.fpt.ai/dmp/checkface/v1");
                request.Headers.Add("api-key", _apiKey!);
                request.Content = formData;

                var response = await _httpClient.SendAsync(request);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("FPT.AI Face Compare API returned {StatusCode}", response.StatusCode);
                    return new EkycFaceResult { IsSuccess = false, IsProviderError = true, Message = "Face compare API error: " + response.StatusCode };
                }

                var jsonString = await response.Content.ReadAsStringAsync();
                LogProviderResponseMetadata("FPT.AI Face Compare", jsonString);

                var json = System.Text.Json.JsonDocument.Parse(jsonString);
                var root = json.RootElement;

                // FPT Face Compare returns: { code, message, data: { isMatch, similarity, isBothImgIDCard } }
                // code can be int or string "200", similarity can be number or string "0.87", isMatch can be bool or string "true"
                int code = -1;
                if (root.TryGetProperty("code", out var c))
                {
                    if (c.ValueKind == System.Text.Json.JsonValueKind.Number)
                        code = c.GetInt32();
                    else if (c.ValueKind == System.Text.Json.JsonValueKind.String)
                        int.TryParse(c.GetString(), out code);
                }
                string message = root.TryGetProperty("message", out var m) ? m.GetString() ?? "" : "";

                if (code != 200)
                {
                    return new EkycFaceResult
                    {
                        IsSuccess = false,
                        IsProviderError = true,
                        Message = $"Face compare error {code}: {message}"
                    };
                }

                var data = root.GetProperty("data");

                double similarity = 0;
                if (data.TryGetProperty("similarity", out var sim))
                {
                    if (sim.ValueKind == System.Text.Json.JsonValueKind.Number)
                        similarity = sim.GetDouble();
                    else if (sim.ValueKind == System.Text.Json.JsonValueKind.String)
                        double.TryParse(sim.GetString(), out similarity);
                }

                bool isMatch = false;
                if (data.TryGetProperty("isMatch", out var match))
                {
                    if (match.ValueKind == System.Text.Json.JsonValueKind.True || match.ValueKind == System.Text.Json.JsonValueKind.False)
                        isMatch = match.GetBoolean();
                    else if (match.ValueKind == System.Text.Json.JsonValueKind.String)
                        bool.TryParse(match.GetString(), out isMatch);
                }

                // Auto-accept if similarity >= 90%, regardless of isMatch flag from FPT
                // FPT returns similarity as percentage (0-100), not decimal (0-1)
                if (similarity >= 90)
                    isMatch = true;

                return new EkycFaceResult
                {
                    IsSuccess = true,
                    SimilarityPercent = similarity,
                    IsMatch = isMatch,
                    Message = $"Match: {isMatch}, Similarity: {similarity:F1}%"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "FPT.AI Face Compare failed");
                return new EkycFaceResult
                {
                    IsSuccess = false,
                    IsProviderError = true,
                    Message = "Face compare failed: " + ex.Message
                };
            }
        }

        private static string GetString(System.Text.Json.JsonElement data, string key)
        {
            if (data.TryGetProperty(key, out var val))
            {
                string s = val.ValueKind == System.Text.Json.JsonValueKind.String ? val.GetString() ?? "" : val.ToString();
                return s.Trim();
            }
            return "";
        }

        private static string NormalizeIdNumber(string id)
        {
            if (string.IsNullOrEmpty(id) || id == "N/A") return "";
            return Regex.Replace(id, @"\s+", "");
        }

        private static string NormalizeVietnameseName(string name)
        {
            if (string.IsNullOrEmpty(name) || name == "N/A") return "";
            // Title-case the name: each word first letter uppercase
            var words = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var normalized = words.Select(w => char.ToUpper(w[0]) + (w.Length > 1 ? w[1..].ToLower() : ""));
            return string.Join(" ", normalized);
        }

        private static string NormalizeDate(string date)
        {
            if (string.IsNullOrEmpty(date) || date == "N/A") return "";
            // FPT returns YYYY-MM-DD or DD/MM/YYYY
            if (DateTime.TryParse(date, out var dt))
            {
                return dt.ToString("dd/MM/yyyy");
            }
            return date;
        }

        private void LogProviderResponseMetadata(string operation, string responseBody)
        {
            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(responseBody))).ToLowerInvariant();
            _logger.LogInformation("{Operation} response received. Length={Length}; Sha256={Hash}", operation, responseBody.Length, hash);
        }
    }

    public class EkycIdResult
    {
        public bool IsSuccess { get; set; }
        public bool IsProviderError { get; set; }
        public string? CitizenId { get; set; }
        public string? FullName { get; set; }
        public string? DateOfBirth { get; set; }
        public string? Validity { get; set; }
        public string? Gender { get; set; }
        public string? Address { get; set; }
        public string? Home { get; set; }
        public string? Nationality { get; set; }
        public string? Ethnicity { get; set; }
        public string? Religion { get; set; }
        public string? Features { get; set; }
        public string? IssueDate { get; set; }
        public string? IssueLocation { get; set; }
        public string? CardType { get; set; }
        public string? Message { get; set; }
    }

    public class EkycLicenseResult
    {
        public bool IsSuccess { get; set; }
        public bool IsProviderError { get; set; }
        public string? LicenseNumber { get; set; }
        public string? Class { get; set; }
        public string? FullName { get; set; }
        public string? DateOfBirth { get; set; }
        public string? Validity { get; set; }
        public string? Nationality { get; set; }
        public string? Address { get; set; }
        public string? IssueDate { get; set; }
        public string? IssueLocation { get; set; }
        public string? Message { get; set; }
    }

    public class EkycFaceResult
    {
        public bool IsSuccess { get; set; }
        public bool IsProviderError { get; set; }
        public double SimilarityPercent { get; set; }
        public bool IsMatch { get; set; }
        public string? Message { get; set; }
    }
}
