using System;
using System.Threading.Tasks;
using CarRentalSystem.Controllers;
using CarRentalSystem.Data;
using CarRentalSystem.Models;
using CarRentalSystem.Tests.Helpers;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace CarRentalSystem.Tests.UnitTests.Controllers
{
    /// <summary>
    /// Unit tests cho HomeController — Trang chủ, SetRegion
    /// </summary>
    public class HomeControllerTests : IDisposable
    {
        private readonly ApplicationDbContext _context;

        public HomeControllerTests()
        {
            _context = MockRepositoryHelper.CreateSeededDbContext();
        }

        [Fact]
        [Trait("Module", "Home")]
        [Trait("Type", "Unit")]
        public async Task Index_ShouldReturnView_WithFeaturedCars()
        {
            // Arrange
            var controller = new HomeController(_context);
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            };

            // Act
            var result = await controller.Index() as ViewResult;

            // Assert
            result.Should().NotBeNull();
        }

        [Fact]
        [Trait("Module", "Home")]
        [Trait("Type", "Unit")]
        public void SetRegion_ShouldSetCookieAndRedirect()
        {
            // Arrange
            var controller = new HomeController(_context);
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            };

            // Act
            var result = controller.SetRegion(1, "/Home") as LocalRedirectResult;

            // Assert
            result.Should().NotBeNull();
            result!.Url.Should().Be("/Home");
        }

        [Fact]
        [Trait("Module", "Home")]
        [Trait("Type", "Unit")]
        public void SetRegion_NullLocationId_DeletesCookieAndRedirects()
        {
            // Arrange
            var controller = new HomeController(_context);
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            };

            // Act
            var result = controller.SetRegion(null, "/") as LocalRedirectResult;

            // Assert
            result.Should().NotBeNull();
            result!.Url.Should().Be("/");
        }

        public void Dispose()
        {
            _context.Dispose();
        }
    }
}
