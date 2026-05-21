using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using CarRentalSystem.Controllers;
using CarRentalSystem.Data;
using CarRentalSystem.Models;
using CarRentalSystem.Services;
using CarRentalSystem.Tests.Helpers;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Moq;
using Xunit;

namespace CarRentalSystem.Tests.UnitTests.Controllers
{
    public class CartControllerTests : IDisposable
    {
        private readonly ApplicationDbContext _context;

        public CartControllerTests()
        {
            _context = MockRepositoryHelper.CreateSeededDbContext();
        }

        [Fact]
        [Trait("Module", "Cart")]
        [Trait("Type", "Unit")]
        public async Task CheckoutAll_WhenEkycMissing_ShowsEkycModalWithoutErrorToast()
        {
            var user = await _context.Users.FindAsync(2);
            user!.IsVerified = false;
            user.VerificationStatus = "Pending";

            var cart = new Cart { UserID = user.UserID };
            _context.Carts.Add(cart);
            await _context.SaveChangesAsync();

            _context.CartItems.Add(new CartItem
            {
                CartID = cart.CartID,
                CarID = 1,
                StartDate = DateTime.UtcNow.AddDays(3),
                EndDate = DateTime.UtcNow.AddDays(5),
                EstimatedPrice = 2_000_000
            });
            await _context.SaveChangesAsync();

            var eligibility = new Mock<IEligibilityService>();
            eligibility.Setup(s => s.CheckRentalEligibilityAsync(user.UserID))
                .ReturnsAsync(new EligibilityResult(false, "Cần xác thực eKYC trước khi thuê xe.", EligibilityFailureCode.EkycNotVerified));

            var controller = new CartController(
                _context,
                Mock.Of<IPiiEncryptionService>(),
                Mock.Of<IReservationService>(),
                Mock.Of<IBookingQuoteService>(),
                Mock.Of<IPayOSPaymentService>(),
                eligibility.Object,
                Mock.Of<ILegalComplianceService>());

            var httpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(new List<Claim>
                {
                    new(ClaimTypes.NameIdentifier, user.UserID.ToString())
                }, "Cookies"))
            };

            controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
            controller.TempData = new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>());
            var urlHelper = new Mock<IUrlHelper>();
            urlHelper.Setup(u => u.Action(It.IsAny<UrlActionContext>())).Returns("/Cart");
            controller.Url = urlHelper.Object;

            var result = await controller.CheckoutAll(true, true);

            result.Should().BeOfType<RedirectToActionResult>()
                .Which.ActionName.Should().Be("Index");
            controller.TempData["ShowEkycModal"].Should().Be("true");
            controller.TempData["EkycReturnUrl"].Should().Be("/Cart");
            controller.TempData.ContainsKey("Error").Should().BeFalse();
        }

        public void Dispose()
        {
            _context.Dispose();
        }
    }
}
