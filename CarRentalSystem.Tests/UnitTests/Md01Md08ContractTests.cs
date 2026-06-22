using System.Text.Json;
using CarRentalSystem.Controllers;
using CarRentalSystem.Data;
using CarRentalSystem.Models;
using CarRentalSystem.Services;
using CarRentalSystem.Tests.Helpers;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore.Metadata;
using Moq;
using Xunit;

namespace CarRentalSystem.Tests.UnitTests
{
    public class Md01Md08ContractTests
    {
        [Fact]
        public void UserIdentityHashes_HaveUniqueFilteredIndexes()
        {
            using var context = MockRepositoryHelper.CreateInMemoryDbContext();
            var user = context.Model.FindEntityType(typeof(User));

            foreach (var propertyName in new[]
            {
                nameof(User.CitizenIdHash),
                nameof(User.DrivingLicenseHash),
                nameof(User.PhoneHash)
            })
            {
                var index = user!.GetIndexes().Single(item =>
                    item.Properties.Count == 1 &&
                    item.Properties[0].Name == propertyName);

                index.IsUnique.Should().BeTrue();
                index.FindAnnotation("Relational:Filter")?.Value?.ToString().Should().Contain(propertyName);
            }
        }

        [Fact]
        public async Task Models_ReturnsStableIdNameContract()
        {
            var cache = new Mock<ICatalogCacheService>();
            cache.Setup(service => service.GetCarModelsAsync(1))
                .ReturnsAsync(new List<CatalogCarModel>
                {
                    new(7, "Playwright Vios", 5, "Gasoline", "Automatic")
                });
            var controller = new CarCatalogController(cache.Object);

            var result = await controller.Models(1) as JsonResult;
            var json = JsonSerializer.Serialize(result!.Value);

            json.Should().Contain("\"id\":7");
            json.Should().Contain("\"name\":\"Playwright Vios\"");
            json.ToLowerInvariant().Should().NotContain("seatcount");
        }

        [Fact]
        public async Task Models_RejectsInvalidBrandId()
        {
            var controller = new CarCatalogController(Mock.Of<ICatalogCacheService>());

            var result = await controller.Models(0);

            result.Should().BeOfType<BadRequestObjectResult>();
        }
    }
}
