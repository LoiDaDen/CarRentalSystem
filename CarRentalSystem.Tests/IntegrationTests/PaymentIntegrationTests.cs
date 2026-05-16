using CarRentalSystem.Data;
using CarRentalSystem.Models;
using CarRentalSystem.Repositories;
using CarRentalSystem.Services;
using CarRentalSystem.Tests.Helpers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using System;
using System.Threading.Tasks;
using Xunit;

namespace CarRentalSystem.Tests.IntegrationTests
{
    public class PaymentIntegrationTests : IDisposable
    {
        private readonly ApplicationDbContext _context;
        private readonly BookingService _bookingService;

        public PaymentIntegrationTests()
        {
            _context = MockRepositoryHelper.CreateSeededDbContext();

            var bookingRepo = new Repository<Booking>(_context);
            var paymentRepo = new Repository<Payment>(_context);
            var contractRepo = new Repository<RentalContract>(_context);
            var statusRepo = new Repository<BookingStatus>(_context);
            var carRepo = new Repository<Car>(_context);
            var carStatusRepo = new Repository<CarStatus>(_context);
            var transitionService = new Mock<IBookingTransitionService>();
            var businessRules = new Mock<IBookingBusinessRuleService>();
            businessRules
                .Setup(r => r.ValidateSelfDriveAsync(It.IsAny<Booking>(), It.IsAny<decimal>()))
                .Returns(Task.CompletedTask);
            transitionService
                .Setup(t => t.TransitionAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<string>(), It.IsAny<string?>()))
                .Callback<int, string, int?, string, string?>((bookingId, statusName, _, _, _) =>
                {
                    var booking = _context.Bookings.FirstOrDefault(b => b.BookingID == bookingId);
                    var status = _context.BookingStatuses.FirstOrDefault(s => s.Name == statusName);
                    if (booking != null && status != null)
                    {
                        booking.BookingStatusID = status.BookingStatusID;
                        booking.BookingStatus = status;
                        _context.SaveChanges();
                    }
                })
                .ReturnsAsync(new BookingTransitionResult(true, null));

            _bookingService = new BookingService(
                bookingRepo, paymentRepo, contractRepo, statusRepo, carRepo, carStatusRepo, transitionService.Object, businessRules.Object);
        }

        [Fact]
        [Trait("Module", "Payment")]
        [Trait("Type", "Integration")]
        public async Task ProcessPaymentAsync_ShouldAddPaymentToDatabase()
        {
            // Arrange
            // Assumes a seeded booking with ID 1 exists
            var payment = new Payment
            {
                BookingID = 1,
                Amount = 500000,
                PaymentMethod = "Bank Transfer",
                PaymentStatus = "Completed",
                PaymentDate = DateTime.Now
            };

            // Act
            await _bookingService.ProcessPaymentAsync(payment);

            // Assert
            var savedPayment = await _context.Payments.FirstOrDefaultAsync(p => p.Amount == 500000);
            savedPayment.Should().NotBeNull();
            savedPayment.Amount.Should().Be(500000);
            savedPayment.BookingID.Should().Be(1);
            savedPayment.PaymentStatus.Should().Be("Completed");
        }

        [Fact]
        [Trait("Module", "Payment")]
        [Trait("Type", "Integration")]
        public async Task PaymentWebhook_MocksUpdateBookingStatusToConfirmed()
        {
            // This test simulates the outcome of the PayOS webhook successfully firing
            // and the Controller calling UpdateBookingStatusAsync(bookingId, "Confirmed");
            
            // Arrange
            var booking = new Booking
            {
                CarID = 1,
                UserID = 1,
                StartDate = DateTime.Now.AddDays(1),
                EndDate = DateTime.Now.AddDays(3),
                BookingStatusID = 1 // 1 is Pending per Seed Data
            };
            
            _context.Bookings.Add(booking);
            await _context.SaveChangesAsync();
            
            int bookingId = booking.BookingID;

            // Act
            await _bookingService.UpdateBookingStatusAsync(bookingId, "Confirmed");

            // Assert
            var updatedBooking = await _context.Bookings.Include(b => b.BookingStatus).FirstOrDefaultAsync(b => b.BookingID == bookingId);
            updatedBooking.Should().NotBeNull();
            updatedBooking.BookingStatus.Name.Should().Be("Confirmed");
        }

        public void Dispose()
        {
            _context.Dispose();
        }
    }
}
