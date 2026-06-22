using CarRentalSystem.Models;
using CarRentalSystem.Services;
using CarRentalSystem.Tests.Helpers;
using FluentAssertions;
using Moq;
using Xunit;

namespace CarRentalSystem.Tests.UnitTests.Services
{
    public class Pass2RegressionTests
    {
        [Theory]
        [InlineData(4, 400_000)]
        [InlineData(8, 700_000)]
        [InlineData(12, 850_000)]
        [InlineData(24, 1_000_000)]
        [InlineData(28, 1_400_000)]
        [Trait("Module", "Pricing")]
        [Trait("Type", "Regression")]
        public void SelfDrivePricing_UsesCentralTierCaps(int hours, decimal expectedBaseRental)
        {
            var service = new SelfDrivePricingService();
            var car = new Car
            {
                CarName = "Test car",
                LicensePlate = "51A-00001",
                VIN = "TESTVIN000000001",
                PricePerDay = 1_000_000m,
                PricePerHour = 150_000m
            };

            var result = service.Calculate(
                car,
                new DateTime(2026, 5, 19, 8, 0, 0),
                new DateTime(2026, 5, 19, 8, 0, 0).AddHours(hours));

            result.TotalHours.Should().Be(hours);
            result.BaseRentalAmount.Should().Be(expectedBaseRental);
        }

        [Fact]
        [Trait("Module", "Pricing")]
        [Trait("Type", "Regression")]
        public async Task BookingQuote_AppliesHolidayMultiplierAndKeepsAddOnId()
        {
            using var context = MockRepositoryHelper.CreateSeededDbContext();
            var service = CreateQuoteService(context, holidayMultiplier: 2m);

            var quote = await service.BuildQuoteAsync(new BookingQuoteRequest
            {
                UserID = 2,
                CarID = 1,
                StartDate = new DateTime(2026, 5, 19, 8, 0, 0),
                EndDate = new DateTime(2026, 5, 20, 8, 0, 0),
                SelectedAddons = new List<string> { "7|Baby seat|100000" }
            });

            quote.BaseRentalAmount.Should().Be(2_000_000m);
            quote.AddOns.Should().ContainSingle(a =>
                a.AddOnID == 7 &&
                a.Name == "Baby seat" &&
                a.TotalPrice == 100_000m);
            quote.HoldingFee.Should().Be(Math.Round(quote.TotalPrice * 0.2m, 0, MidpointRounding.AwayFromZero));
        }

        [Fact]
        [Trait("Module", "Pricing")]
        [Trait("Type", "Regression")]
        public async Task BookingQuote_AppliesMembershipPromotionAndReferralDiscounts()
        {
            using var context = MockRepositoryHelper.CreateSeededDbContext();
            var user = context.Users.Single(u => u.UserID == 2);
            context.MembershipTiers.Add(new MembershipTier
            {
                MembershipTierID = 30,
                Name = "Gold",
                DiscountPercent = 10m
            });
            user.MembershipTierID = 30;
            context.Promotions.Add(new Promotion
            {
                PromotionID = 40,
                PromoCode = "SAVE20",
                DiscountPercent = 20m,
                MaxDiscountAmount = 100_000m,
                ValidFrom = DateTime.UtcNow.AddDays(-1),
                ValidUntil = DateTime.UtcNow.AddDays(7),
                UsageLimit = 10,
                UsedCount = 0,
                IsActive = true
            });
            await context.SaveChangesAsync();

            var service = CreateQuoteService(context, referralDiscount: 50_000m);

            var quote = await service.BuildQuoteAsync(new BookingQuoteRequest
            {
                UserID = 2,
                CarID = 1,
                StartDate = new DateTime(2026, 5, 19, 8, 0, 0),
                EndDate = new DateTime(2026, 5, 20, 8, 0, 0),
                PromotionID = 40
            });

            quote.TotalBeforeDiscount.Should().Be(1_150_000m);
            quote.MembershipDiscountAmount.Should().Be(115_000m);
            quote.PromotionDiscountAmount.Should().Be(100_000m);
            quote.ReferralDiscountAmount.Should().Be(50_000m);
            quote.DiscountAmount.Should().Be(265_000m);
            quote.PromotionID.Should().Be(40);
            quote.TotalPrice.Should().Be(885_000m);
        }

        [Fact]
        [Trait("Module", "Pricing")]
        [Trait("Type", "Regression")]
        public async Task BookingQuote_DoesNotApplyPromotion_WhenUserAlreadyUsedIt()
        {
            using var context = MockRepositoryHelper.CreateSeededDbContext();
            context.Promotions.Add(new Promotion
            {
                PromotionID = 41,
                PromoCode = "ONCE",
                DiscountPercent = 30m,
                ValidFrom = DateTime.UtcNow.AddDays(-1),
                ValidUntil = DateTime.UtcNow.AddDays(7),
                UsageLimit = 10,
                UsedCount = 1,
                IsActive = true
            });
            var completedStatus = context.BookingStatuses.Single(s => s.Name == BookingStatusConstants.Completed);
            context.Bookings.Add(new Booking
            {
                UserID = 2,
                CarID = 1,
                StartDate = DateTime.UtcNow.AddDays(-10),
                EndDate = DateTime.UtcNow.AddDays(-9),
                BookingStatusID = completedStatus.BookingStatusID,
                PromotionID = 41,
                PickupLocationID = 1,
                ReturnLocationID = 1,
                TotalHours = 24,
                TotalPrice = 1_000_000m
            });
            await context.SaveChangesAsync();

            var service = CreateQuoteService(context);

            var quote = await service.BuildQuoteAsync(new BookingQuoteRequest
            {
                UserID = 2,
                CarID = 1,
                StartDate = new DateTime(2026, 5, 19, 8, 0, 0),
                EndDate = new DateTime(2026, 5, 20, 8, 0, 0),
                PromotionID = 41
            });

            quote.PromotionID.Should().BeNull();
            quote.PromotionDiscountAmount.Should().Be(0m);
        }

        [Fact]
        [Trait("Module", "Booking")]
        [Trait("Type", "Regression")]
        public async Task BookingQuote_ThrowsBusinessError_WhenDrivingLicenseIsMissing()
        {
            using var context = MockRepositoryHelper.CreateSeededDbContext();
            var service = CreateQuoteService(context, hasDrivingLicense: false);

            var act = () => service.BuildQuoteAsync(new BookingQuoteRequest
            {
                UserID = 2,
                CarID = 1,
                StartDate = new DateTime(2026, 5, 19, 8, 0, 0),
                EndDate = new DateTime(2026, 5, 20, 8, 0, 0)
            });

            var exception = await act.Should().ThrowAsync<BookingQuoteException>();
            exception.Which.Code.Should().Be(BookingQuoteFailureCode.MissingDrivingLicense);
        }

        [Fact]
        [Trait("Module", "Data")]
        [Trait("Type", "Regression")]
        public void Booking_DefaultDepositStatus_IsUnpaid()
        {
            new Booking().DepositStatus.Should().Be(DepositStatusConstants.Unpaid);
        }

        [Fact]
        [Trait("Module", "Blog")]
        [Trait("Type", "Regression")]
        public async Task BlogService_CreateBlog_StripsDangerousHtml()
        {
            using var context = MockRepositoryHelper.CreateSeededDbContext();
            context.BlogCategories.Add(new BlogCategory
            {
                BlogCategoryID = 1,
                Name = "Safety",
                Slug = "safety",
                IsActive = true
            });
            await context.SaveChangesAsync();

            var service = new BlogService(context);

            var blog = await service.CreateBlogAsync(new Blog
            {
                Title = "Safe trip",
                Summary = "Short summary",
                CategoryID = 1,
                Content = "<p onclick=\"alert(1)\">Hello</p><script>alert(2)</script><a href=\"javascript:alert(3)\">bad</a>",
                Status = BlogStatusConstants.Published,
                PublishedDate = DateTime.UtcNow.AddMinutes(-1)
            }, Array.Empty<string>());

            blog.Content.Should().NotContain("<script");
            blog.Content.Should().NotContain("onclick");
            blog.Content.Should().NotContain("javascript:");
            blog.Content.Should().Contain("href=\"#\"");
        }

        private static BookingQuoteService CreateQuoteService(
            CarRentalSystem.Data.ApplicationDbContext context,
            decimal holidayMultiplier = 1m,
            decimal referralDiscount = 0m,
            bool hasDrivingLicense = true)
        {
            if (!context.ServiceTypes.Any(s => s.ServiceTypeID == 10))
            {
                context.ServiceTypes.Add(new ServiceType
                {
                    ServiceTypeID = 10,
                    Code = ServiceTypeConstants.SelfDrive,
                    Name = "Self drive"
                });
            }

            var car = context.Cars.Single(c => c.CarID == 1);
            car.ServiceTypeID = 10;
            car.IsSelfDriveAllowed = true;
            car.IsWithDriverAllowed = false;

            if (hasDrivingLicense && !context.CustomerDrivingLicenses.Any(l => l.CustomerDrivingLicenseID == 22))
            {
                context.CustomerDrivingLicenses.Add(new CustomerDrivingLicense
                {
                    CustomerDrivingLicenseID = 22,
                    UserID = 2,
                    IsActive = true,
                    IsApproved = true,
                    VerificationStatus = "Verified"
                });
            }

            context.SaveChanges();

            var referralService = new Mock<IReferralService>();
            referralService.Setup(s => s.GetFirstRentalDiscountAsync(2)).ReturnsAsync(referralDiscount);

            var pricingService = new Mock<ISelfDrivePricingService>();
            pricingService
                .Setup(s => s.Calculate(It.IsAny<Car>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                .Returns(new SelfDrivePriceResult(24, 1, 1_000_000m));

            var businessRules = new Mock<IBookingBusinessRuleService>();
            businessRules.Setup(s => s.GetServiceTypeIdAsync(ServiceTypeConstants.SelfDrive)).ReturnsAsync(10);
            businessRules.Setup(s => s.GetValidCustomerDrivingLicenseAsync(2))
                .ReturnsAsync(hasDrivingLicense
                    ? new CustomerDrivingLicense { CustomerDrivingLicenseID = 22, UserID = 2 }
                    : null);

            var holidayPricing = new Mock<IHolidayPricingService>();
            holidayPricing.Setup(s => s.GetMultiplier(It.IsAny<DateTime>(), It.IsAny<DateTime>())).Returns(holidayMultiplier);

            return new BookingQuoteService(
                context,
                referralService.Object,
                Mock.Of<IReservationService>(),
                pricingService.Object,
                businessRules.Object,
                holidayPricing.Object);
        }
    }
}
