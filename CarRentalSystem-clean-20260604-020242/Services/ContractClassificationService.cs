using System.Text.Json;
using CarRentalSystem.Models;

namespace CarRentalSystem.Services
{
    public record ContractClassificationResult(string SuggestedContractTypeCode, decimal Confidence, string Reason);

    public interface IContractClassificationService
    {
        Task<ContractClassificationResult> ClassifyAsync(string fileName, string? note, string? extractedText = null);
    }

    public class ContractClassificationService : IContractClassificationService
    {
        private readonly GroqChatService _groqChatService;

        public ContractClassificationService(GroqChatService groqChatService)
        {
            _groqChatService = groqChatService;
        }

        public async Task<ContractClassificationResult> ClassifyAsync(string fileName, string? note, string? extractedText = null)
        {
            var prompt = $$"""
                Phân loại tài liệu hợp đồng thuê xe. Chỉ trả về JSON hợp lệ với 3 field:
                suggestedContractTypeCode, confidence, reason.

                Các mã hợp lệ:
                - SELF_DRIVE_RENTAL: hợp đồng thuê xe tự lái theo booking
                - CAR_OWNER_DOCUMENT: giấy tờ xe/chủ xe, đăng kiểm, bảo hiểm, cà vẹt
                - HANDOVER_RECORD: biên bản bàn giao/nhận xe
                - DEPOSIT_AGREEMENT: thỏa thuận đặt cọc/chứng từ cọc
                - OTHER: khác hoặc không đủ thông tin

                Tên file: {{fileName}}
                Ghi chú: {{note ?? ""}}
                Nội dung OCR nếu có: {{extractedText ?? ""}}
                """;

            var response = await _groqChatService.GetResponseAsync(new List<ChatMessage>
            {
                new() { Role = "user", Content = prompt }
            });

            try
            {
                using var doc = JsonDocument.Parse(response);
                var root = doc.RootElement;
                var code = root.GetProperty("suggestedContractTypeCode").GetString() ?? PhysicalContractTypeCodes.Other;
                var confidence = root.TryGetProperty("confidence", out var c) && c.TryGetDecimal(out var value) ? value : 50m;
                var reason = root.GetProperty("reason").GetString() ?? "AI gợi ý dựa trên tên file và mô tả.";
                return Normalize(code, confidence, reason);
            }
            catch
            {
                return RuleBasedFallback(fileName, note, extractedText);
            }
        }

        private static ContractClassificationResult RuleBasedFallback(string fileName, string? note, string? extractedText)
        {
            var text = $"{fileName} {note} {extractedText}".ToLowerInvariant();
            if (text.Contains("ban giao") || text.Contains("bàn giao") || text.Contains("handover"))
                return Normalize(PhysicalContractTypeCodes.HandoverRecord, 78m, "Nội dung có dấu hiệu là biên bản bàn giao xe.");
            if (text.Contains("coc") || text.Contains("cọc") || text.Contains("deposit"))
                return Normalize(PhysicalContractTypeCodes.DepositAgreement, 76m, "Nội dung có dấu hiệu liên quan đến đặt cọc.");
            if (text.Contains("dang kiem") || text.Contains("đăng kiểm") || text.Contains("bao hiem") || text.Contains("bảo hiểm") || text.Contains("cavet") || text.Contains("cà vẹt"))
                return Normalize(PhysicalContractTypeCodes.CarOwnerDocument, 80m, "Nội dung có dấu hiệu là hồ sơ xe hoặc chủ xe.");
            if (text.Contains("hop dong") || text.Contains("hợp đồng") || text.Contains("thue xe") || text.Contains("thuê xe"))
                return Normalize(PhysicalContractTypeCodes.SelfDriveRental, 74m, "Nội dung có dấu hiệu là hợp đồng thuê xe.");

            return Normalize(PhysicalContractTypeCodes.Other, 40m, "Chưa đủ thông tin để phân loại chắc chắn.");
        }

        private static ContractClassificationResult Normalize(string code, decimal confidence, string reason)
        {
            var allowed = new[]
            {
                PhysicalContractTypeCodes.SelfDriveRental,
                PhysicalContractTypeCodes.CarOwnerDocument,
                PhysicalContractTypeCodes.HandoverRecord,
                PhysicalContractTypeCodes.DepositAgreement,
                PhysicalContractTypeCodes.Other
            };

            if (!allowed.Contains(code))
                code = PhysicalContractTypeCodes.Other;

            return new ContractClassificationResult(code, Math.Clamp(confidence, 0m, 100m), reason);
        }
    }
}
