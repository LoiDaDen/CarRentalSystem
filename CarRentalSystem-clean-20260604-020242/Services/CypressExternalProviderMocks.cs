using PayOS.Models.V2.PaymentRequests;
using PayOS.Models.Webhooks;

namespace CarRentalSystem.Services
{
    public sealed class CypressPayOSGateway : IPayOSGateway
    {
        public bool IsConfigured => false;

        public Task<CreatePaymentLinkResponse> CreatePaymentLinkAsync(CreatePaymentLinkRequest request) =>
            throw new InvalidOperationException("PayOS is disabled in Cypress unless Cypress:UsePayOSSandbox is true.");

        public Task<PaymentLink> GetPaymentLinkAsync(long orderCode) =>
            throw new InvalidOperationException("PayOS is disabled in Cypress unless Cypress:UsePayOSSandbox is true.");

        public Task<WebhookData?> VerifyWebhookAsync(Webhook webhook) =>
            Task.FromResult<WebhookData?>(null);
    }

    public sealed class CypressEkycService : IEkycService
    {
        public Task<EkycIdResult> ExtractIdCardAsync(IFormFile frontImage, IFormFile backImage)
        {
            return Task.FromResult(new EkycIdResult
            {
                IsSuccess = true,
                CitizenId = "079200000001",
                FullName = "CYPRESS USER",
                DateOfBirth = "01/01/1990",
                Validity = "01/01/2040",
                Gender = "Nam",
                Address = "Cypress Test Address",
                IssueDate = "01/01/2020",
                IssueLocation = "Cypress Authority",
                Message = "Cypress eKYC ID fixture"
            });
        }

        public Task<EkycLicenseResult> ExtractDrivingLicenseAsync(IFormFile licenseImage)
        {
            return Task.FromResult(new EkycLicenseResult
            {
                IsSuccess = true,
                LicenseNumber = "790000000001",
                Class = "B2",
                FullName = "CYPRESS USER",
                DateOfBirth = "01/01/1990",
                Validity = "01/01/2035",
                Message = "Cypress eKYC license fixture"
            });
        }

        public Task<EkycFaceResult> CompareFaceAsync(IFormFile faceImage, IFormFile cardImage)
        {
            return Task.FromResult(new EkycFaceResult
            {
                IsSuccess = true,
                SimilarityPercent = 99,
                IsMatch = true,
                Message = "Cypress eKYC face fixture"
            });
        }
    }
}
