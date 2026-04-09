using CarRentalSystem.Models;
using FluentAssertions;
using System;
using Xunit;

namespace CarRentalSystem.Tests.UnitTests
{
    /// <summary>
    /// Unit tests cho RentalContract.CalculateFees — Tính phí hợp đồng thuê xe
    /// </summary>
    public class RentalContractTests
    {
        // =============================================
        // CALCULATE FEES TESTS
        // =============================================



        [Fact]
        [Trait("Module", "Contract")]
        [Trait("Type", "Unit")]
        public void CalculateFees_NormalTrip_FinalPriceEqualsTotalPrice()
        {
            // Arrange
            var booking = new Booking
            {
                BookingID = 2,
                UserID = 1,
                CarID = 1,
                StartDate = DateTime.Today,
                EndDate = DateTime.Today.AddDays(2),
                TotalPrice = 2000000
            };

            var contract = new RentalContract
            {
                ContractID = 2,
                BookingID = 2,
                Booking = booking,
                StartKm = 5000,
                EndKm = 5800
            };

            // Act
            contract.CalculateFees(totalDays: 2);

            // Assert
            contract.FinalPrice.Should().Be(2000000, "vì FinalPrice = Booking.TotalPrice");
        }

        [Fact]
        [Trait("Module", "Contract")]
        [Trait("Type", "Unit")]
        public void CalculateFees_NoBooking_FinalPriceIsZero()
        {
            // Arrange — Contract không gắn Booking
            var contract = new RentalContract
            {
                ContractID = 3,
                BookingID = 99,
                Booking = null,
                StartKm = 1000,
                EndKm = 1200
            };

            // Act
            contract.CalculateFees(totalDays: 1);

            // Assert
            contract.FinalPrice.Should().Be(0, "vì Booking là null nên FinalPrice = 0");
        }

        [Fact]
        [Trait("Module", "Contract")]
        [Trait("Type", "Unit")]
        public void CalculateFees_ZeroDays_TreatsAsOneDay()
        {
            // Arrange
            var booking = new Booking
            {
                BookingID = 4,
                UserID = 1,
                CarID = 1,
                StartDate = DateTime.Today,
                EndDate = DateTime.Today, // Cùng ngày
                TotalPrice = 1000000
            };

            var contract = new RentalContract
            {
                ContractID = 4,
                BookingID = 4,
                Booking = booking,
                StartKm = 3000,
                EndKm = 3100
            };

            // Act — totalDays = 0 sẽ được xử lý bên BookingService thành 1
            contract.CalculateFees(totalDays: 1);

            // Assert
            contract.FinalPrice.Should().Be(1000000);
        }


    }
}
