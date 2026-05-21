using System;
using System.IO;
using CarRentalSystem.Models;
using CarRentalSystem.ViewModels;
using FluentAssertions;
using Xunit;

namespace CarRentalSystem.Tests.UnitTests
{
    public class LegalComplianceStaticTests
    {
        [Fact]
        public void PrivacyPage_DoesNotUseTemplatePlaceholder()
        {
            var content = ReadProjectFile("Views", "Home", "Privacy.cshtml");

            content.Should().NotContain("Use this page to detail your site's privacy policy");
            content.Should().Contain("Thông báo xử lý dữ liệu cá nhân");
        }

        [Fact]
        public void EkycProviderLogging_DoesNotWriteRawFptResponse()
        {
            var content = ReadProjectFile("Services", "EkycService.cs");

            content.Should().NotContain("FPT.AI IDR Response: {Response}");
            content.Should().NotContain("FPT.AI DLR Response: {Response}");
            content.Should().NotContain("FPT.AI Face Compare Response: {Response}");
            content.Should().Contain("response received. Length={Length}; Sha256={Hash}");
        }

        [Fact]
        public void BookingLegalLinks_DoNotUseDeadHashLinks()
        {
            var checkout = ReadProjectFile("Views", "Booking", "Checkout.cshtml");
            var book = ReadProjectFile("Views", "Booking", "Book.cshtml");

            checkout.Should().NotContain("href=\"#\"");
            book.Should().NotContain("href=\"#\"");
            checkout.Should().Contain("asp-action=\"Terms\"");
            checkout.Should().Contain("asp-action=\"Privacy\"");
        }

        [Fact]
        public void RegisterAndBookingModels_CarryLegalEvidenceFields()
        {
            typeof(RegisterViewModel).GetProperty(nameof(RegisterViewModel.AcceptTerms)).Should().NotBeNull();
            typeof(RegisterViewModel).GetProperty(nameof(RegisterViewModel.AcceptPrivacyNotice)).Should().NotBeNull();
            typeof(RegisterViewModel).GetProperty(nameof(RegisterViewModel.AcceptSensitiveDataProcessing)).Should().NotBeNull();

            typeof(Booking).GetProperty(nameof(Booking.TermsDocumentVersionID)).Should().NotBeNull();
            typeof(Booking).GetProperty(nameof(Booking.PrivacyNoticeVersionID)).Should().NotBeNull();
            typeof(Booking).GetProperty(nameof(Booking.SelfDriveAgreementAcceptedAt)).Should().NotBeNull();
        }

        [Fact]
        public void WalletStoredValueFeatureFlag_DefaultsFalse()
        {
            ReadProjectFile("appsettings.json").Should().Contain("\"WalletStoredValue\": false");
            ReadRootFile(".env.example").Should().Contain("Features__WalletStoredValue=false");
        }

        private static string ReadProjectFile(params string[] segments)
        {
            return File.ReadAllText(Path.Combine(ProjectRoot(), Path.Combine(segments)));
        }

        private static string ReadRootFile(params string[] segments)
        {
            return File.ReadAllText(Path.Combine(RepositoryRoot(), Path.Combine(segments)));
        }

        private static string ProjectRoot()
        {
            return Path.Combine(RepositoryRoot(), "CarRentalSystem");
        }

        private static string RepositoryRoot()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null && !File.Exists(Path.Combine(directory.FullName, "CarRentalSystem", "CarRentalSystem.csproj")))
            {
                directory = directory.Parent;
            }

            if (directory == null)
                throw new DirectoryNotFoundException("Could not locate repository root.");

            return directory.FullName;
        }
    }
}
