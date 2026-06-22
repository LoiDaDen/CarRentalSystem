using CarRentalSystem.Areas.Admin.Controllers;
using CarRentalSystem.Services;
using CarRentalSystem.Tests.Helpers;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace CarRentalSystem.Tests.UnitTests.Controllers
{
    public class AdminSupportControllerTests
    {
        [Fact]
        [Trait("Module", "AdminSupport")]
        [Trait("Type", "Regression")]
        public async Task Index_ReturnsHtmlViewForBrowserRequest()
        {
            await using var context = MockRepositoryHelper.CreateSeededDbContext();
            var controller = CreateController(context, "text/html");

            var result = await controller.Index();

            result.Should().BeOfType<ViewResult>();
        }

        [Fact]
        [Trait("Module", "AdminSupport")]
        [Trait("Type", "Regression")]
        public async Task Index_ReturnsJsonForExplicitJsonRequest()
        {
            await using var context = MockRepositoryHelper.CreateSeededDbContext();
            var controller = CreateController(context, "application/json");

            var result = await controller.Index();

            result.Should().BeOfType<JsonResult>();
        }

        [Fact]
        [Trait("Module", "AdminSupport")]
        [Trait("Type", "Regression")]
        public async Task Data_AlwaysReturnsJson()
        {
            await using var context = MockRepositoryHelper.CreateSeededDbContext();
            var controller = CreateController(context, "text/html");

            var result = await controller.Data();

            result.Should().BeOfType<JsonResult>();
        }

        private static SupportController CreateController(
            CarRentalSystem.Data.ApplicationDbContext context,
            string accept)
        {
            var httpContext = new DefaultHttpContext();
            httpContext.Request.Headers.Accept = accept;
            return new SupportController(context, Mock.Of<ISupportTicketService>())
            {
                ControllerContext = new ControllerContext { HttpContext = httpContext }
            };
        }
    }
}
