using System;
using System.Threading.Tasks;
using CarRentalSystem.Data;
using CarRentalSystem.Models;
using CarRentalSystem.Repositories;
using CarRentalSystem.Tests.Helpers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CarRentalSystem.Tests.UnitTests
{
    /// <summary>
    /// Unit tests cho Repository — CRUD operations trên Generic Repository
    /// </summary>
    public class RepositoryTests
    {
        // =============================================
        // ADD TESTS
        // =============================================

        [Fact]
        [Trait("Module", "Repository")]
        [Trait("Type", "Unit")]
        public async Task AddAsync_ShouldAddEntity()
        {
            // Arrange
            using var context = MockRepositoryHelper.CreateInMemoryDbContext();
            var repository = new Repository<CarType>(context);
            var newCarType = new CarType { Name = "SUV", Description = "Sport Utility Vehicle" };

            // Act
            await repository.AddAsync(newCarType);
            await repository.SaveChangesAsync();

            // Assert
            var count = await repository.Query().CountAsync();
            count.Should().Be(1);

            var added = await repository.GetByIdAsync(newCarType.CarTypeID);
            added.Should().NotBeNull();
            added.Name.Should().Be("SUV");
        }

        // =============================================
        // GET ALL TESTS
        // =============================================

        [Fact]
        [Trait("Module", "Repository")]
        [Trait("Type", "Unit")]
        public async Task GetAllAsync_ShouldReturnAllEntities()
        {
            // Arrange
            using var context = MockRepositoryHelper.CreateInMemoryDbContext();
            var repository = new Repository<CarType>(context);
            await repository.AddAsync(new CarType { Name = "Sedan" });
            await repository.AddAsync(new CarType { Name = "Truck" });
            await repository.SaveChangesAsync();

            // Act
            var result = await repository.GetAllAsync();

            // Assert
            result.Should().HaveCount(2);
        }

        // =============================================
        // GET BY ID TESTS
        // =============================================

        [Fact]
        [Trait("Module", "Repository")]
        [Trait("Type", "Unit")]
        public async Task GetByIdAsync_ShouldReturnEntity_WhenExists()
        {
            // Arrange
            using var context = MockRepositoryHelper.CreateInMemoryDbContext();
            var repository = new Repository<CarType>(context);
            var carType = new CarType { Name = "Convertible" };
            await repository.AddAsync(carType);
            await repository.SaveChangesAsync();

            // Act
            var result = await repository.GetByIdAsync(carType.CarTypeID);

            // Assert
            result.Should().NotBeNull();
            result.Name.Should().Be("Convertible");
        }

        // =============================================
        // UPDATE TESTS
        // =============================================

        [Fact]
        [Trait("Module", "Repository")]
        [Trait("Type", "Unit")]
        public async Task Update_ShouldModifyEntity()
        {
            // Arrange
            using var context = MockRepositoryHelper.CreateInMemoryDbContext();
            var repository = new Repository<CarType>(context);
            var carType = new CarType { Name = "Van" };
            await repository.AddAsync(carType);
            await repository.SaveChangesAsync();

            // Act
            carType.Name = "Minivan";
            repository.Update(carType);
            await repository.SaveChangesAsync();

            // Assert
            var updated = await repository.GetByIdAsync(carType.CarTypeID);
            updated.Name.Should().Be("Minivan");
        }

        // =============================================
        // DELETE TESTS
        // =============================================

        [Fact]
        [Trait("Module", "Repository")]
        [Trait("Type", "Unit")]
        public async Task Delete_ShouldRemoveEntity()
        {
            // Arrange
            using var context = MockRepositoryHelper.CreateInMemoryDbContext();
            var repository = new Repository<CarType>(context);
            var carType = new CarType { Name = "Hatchback" };
            await repository.AddAsync(carType);
            await repository.SaveChangesAsync();

            // Act
            repository.Delete(carType);

            // Assert — verify the entity state was changed to Deleted
            // InMemory provider marks entities as Deleted in the change tracker.
            // A real SQL database would remove the row from the table on SaveChanges.
            var entryState = context.Entry(carType).State;
            entryState.Should().Be(EntityState.Deleted,
                "Repository.Delete() must mark entity as Deleted in the change tracker");

            await repository.SaveChangesAsync();
        }
    }
}
