using System;
using System.Threading.Tasks;
using CarRentalSystem.Controllers;
using CarRentalSystem.Data;
using CarRentalSystem.Models;
using CarRentalSystem.Tests.Helpers;
using CarRentalSystem.ViewModels;
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
            result!.Model.Should().BeOfType<HomeIndexViewModel>();
        }

        [Fact]
        [Trait("Module", "Home")]
        [Trait("Type", "Unit")]
        public async Task SetRegion_ShouldSetCookieAndRedirect()
        {
            // Arrange
            var controller = new HomeController(_context);
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            };

            // Act
            var result = await controller.SetRegion(1, "/Home") as LocalRedirectResult;

            // Assert
            result.Should().NotBeNull();
            result!.Url.Should().Be("/Home");
        }

        [Fact]
        [Trait("Module", "Home")]
        [Trait("Type", "Unit")]
        public async Task SetRegion_NullLocationId_DeletesCookieAndRedirects()
        {
            // Arrange
            var controller = new HomeController(_context);
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            };

            // Act
            var result = await controller.SetRegion(null, "/") as LocalRedirectResult;

            // Assert
            result.Should().NotBeNull();
            result!.Url.Should().Be("/");
        }

        [Fact]
        public async Task SetRegion_InvalidLocation_ReturnsBadRequestForJson()
        {
            var controller = new HomeController(_context);
            var httpContext = new DefaultHttpContext();
            httpContext.Request.Headers.Accept = "application/json";
            controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

            var result = await controller.SetRegion(999999, "/");

            result.Should().BeOfType<BadRequestObjectResult>();
        }

        public void Dispose()
        {
            _context.Dispose();
        }
    }
}
