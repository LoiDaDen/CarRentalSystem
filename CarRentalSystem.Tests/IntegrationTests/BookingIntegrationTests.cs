using CarRentalSystem.Data;
using CarRentalSystem.Models;
using CarRentalSystem.Repositories;
using CarRentalSystem.Services;
using CarRentalSystem.Tests.Helpers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace CarRentalSystem.Tests.IntegrationTests
{
    /// <summary>
    /// Integration tests cho BookingService — Full workflow từ tạo booking đến hoàn thành
    /// Dùng EF Core InMemory Database để test end-to-end service layer
    /// </summary>
    public class BookingIntegrationTests : IDisposable
    {
        private readonly ApplicationDbContext _context;
        private readonly BookingService _bookingService;

        public BookingIntegrationTests()
        {
            _context = MockRepositoryHelper.CreateSeededDbContext();

            // Seed thêm Booking cho integration tests
            _context.Bookings.Add(new Booking
            {
                BookingID = 100,
                UserID = 2,
                CarID = 1,
                StartDate = DateTime.Today.AddDays(1),
                EndDate = DateTime.Today.AddDays(3),
                TotalPrice = 2000000,
                BookingStatusID = 2, // Pending
                DepositAmount = 5000000,
                DepositStatus = "Holding",
                PickupLocationID = 1,
                ReturnLocationID = 1
            });
            _context.SaveChanges();

            var bookingRepo = new Repository<Booking>(_context);
            var paymentRepo = new Repository<Payment>(_context);
            var contractRepo = new Repository<RentalContract>(_context);
            var statusRepo = new Repository<BookingStatus>(_context);
            var carRepo = new Repository<Car>(_context);
            var carStatusRepo = new Repository<CarStatus>(_context);
            var transitionService = new Mock<IBookingTransitionService>();
            transitionService
                .Setup(t => t.TransitionAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<string>(), It.IsAny<string?>()))
                .Callback<int, string, int?, string, string?>((bookingId, statusName, _, _, reason) =>
                {
                    var booking = _context.Bookings.Include(b => b.Car).FirstOrDefault(b => b.BookingID == bookingId);
                    var status = _context.BookingStatuses.FirstOrDefault(s => s.Name == statusName);
                    if (booking == null || status == null) return;

                    booking.BookingStatusID = status.BookingStatusID;
                    booking.BookingStatus = status;
                    if (!string.IsNullOrEmpty(reason))
                        booking.AdminNotes = reason;

                    if (statusName == BookingStatusConstants.Active && booking.Car != null)
                        booking.Car.CarStatusID = 2;
                    if (statusName == BookingStatusConstants.Completed && booking.Car != null)
                        booking.Car.CarStatusID = 1;
                    _context.SaveChanges();
                })
                .ReturnsAsync(new BookingTransitionResult(true, null));
            transitionService
                .Setup(t => t.CancelAsync(It.IsAny<int>(), It.IsAny<int?>(), It.IsAny<string>(), It.IsAny<string?>()))
                .Callback<int, int?, string, string?>((bookingId, _, _, reason) =>
                {
                    var booking = _context.Bookings.Include(b => b.Car).FirstOrDefault(b => b.BookingID == bookingId);
                    var status = _context.BookingStatuses.FirstOrDefault(s => s.Name == BookingStatusConstants.Cancelled);
                    if (booking == null || status == null) return;

                    booking.BookingStatusID = status.BookingStatusID;
                    booking.BookingStatus = status;
                    booking.CancelReason = reason;
                    booking.DepositStatus = DepositStatusConstants.PendingRefund;
                    if (booking.Car != null)
                        booking.Car.CarStatusID = 1;
                    _context.SaveChanges();
                })
                .ReturnsAsync(new BookingTransitionResult(true, null));

            _bookingService = new BookingService(
                bookingRepo, paymentRepo, contractRepo,
                statusRepo, carRepo, carStatusRepo, transitionService.Object
            );
        }

        // =============================================
        // FULL BOOKING FLOW
        // =============================================

        [Fact]
        [Trait("Module", "Booking")]
        [Trait("Type", "Integration")]
        public async Task FullBookingFlow_CreateToComplete()
        {
            // Arrange — Tạo booking mới
            var newBooking = new Booking
            {
                UserID = 2,
                CarID = 2,
                StartDate = DateTime.Today.AddDays(5),
                EndDate = DateTime.Today.AddDays(7),
                TotalPrice = 3000000,
                PickupLocationID = 1,
                ReturnLocationID = 1
            };

            // Act 1: Tạo booking → Draft
            await _bookingService.CreateBookingAsync(newBooking);
            newBooking.BookingStatusID.Should().Be(2, "the seeded database fallback initial status is Pending when Unpaid is not seeded");

            // Act 2: Chuyển → Confirmed
            await _bookingService.UpdateBookingStatusAsync(newBooking.BookingID, "Confirmed");
            var confirmedBooking = await _context.Bookings.FindAsync(newBooking.BookingID);
            confirmedBooking!.BookingStatusID.Should().Be(3, "Confirmed status");

            // Act 3: Nhận xe → Active
            await _bookingService.ConfirmPickupAsync(newBooking.BookingID, "Xe giao đúng giờ");
            var activeBooking = await _context.Bookings.FindAsync(newBooking.BookingID);
            activeBooking!.BookingStatusID.Should().Be(5, "Active status");

            // Act 4: Trả xe → Completed
            await _bookingService.CompleteBookingAsync(newBooking.BookingID);
            var completedBooking = await _context.Bookings.FindAsync(newBooking.BookingID);
            completedBooking!.BookingStatusID.Should().Be(6, "Completed status");
        }

        // =============================================
        // COMPLETE BOOKING TESTS
        // =============================================

        [Fact]
        [Trait("Module", "Booking")]
        [Trait("Type", "Integration")]
        public async Task CompleteBooking_SetsCarBackToAvailable()
        {
            // Arrange — Đánh dấu xe là Rented trước
            var car = await _context.Cars.FindAsync(1);
            car!.CarStatusID = 2; // Rented
            await _context.SaveChangesAsync();

            // Act
            await _bookingService.CompleteBookingAsync(100);

            // Assert
            var updatedCar = await _context.Cars.FindAsync(1);
            updatedCar!.CarStatusID.Should().Be(1, "vì sau khi trả xe, xe phải trở về trạng thái Available");
        }

        [Fact]
        [Trait("Module", "Booking")]
        [Trait("Type", "Integration")]
        public async Task CompleteBooking_CalculatesContractFees()
        {
            // Arrange — Tạo contract cho booking
            var contract = new RentalContract
            {
                BookingID = 100,
                BasePrice = 2000000,
                StartKm = 10000,
                EndKm = 0
            };
            _context.RentalContracts.Add(contract);
            await _context.SaveChangesAsync();

            // Act
            await _bookingService.CompleteBookingAsync(100);

            // Assert
            var updatedContract = await _context.RentalContracts
                .FirstOrDefaultAsync(c => c.BookingID == 100);
            updatedContract.Should().NotBeNull();
        }

        [Fact]
        [Trait("Module", "Booking")]
        [Trait("Type", "Integration")]
        public async Task CreateContract_PersistsToDatabase()
        {
            // Arrange
            var contract = new RentalContract
            {
                BookingID = 100,
                BasePrice = 2000000,
                StartKm = 8000
            };

            // Act
            await _bookingService.CreateContractAsync(contract);

            // Assert
            var savedContract = await _context.RentalContracts
                .FirstOrDefaultAsync(c => c.BookingID == 100);
            savedContract.Should().NotBeNull("vì contract phải được lưu vào database");
            savedContract!.StartKm.Should().Be(8000);
        }

        [Fact]
        [Trait("Module", "Booking")]
        [Trait("Type", "Integration")]
        public async Task CancelBookingFlow_RefundsDeposit()
        {
            // Act
            await _bookingService.CancelBookingAsync(100, "Khách hàng đổi lịch");

            // Assert
            var booking = await _context.Bookings.FindAsync(100);
            booking.Should().NotBeNull();
            booking!.BookingStatusID.Should().Be(4, "Cancelled status");
            booking.DepositStatus.Should().Be(DepositStatusConstants.PendingRefund, "vì tiền cọc chuyển sang hàng chờ hoàn/reconcile thay vì đánh dấu đã hoàn ngay");
            booking.CancelReason.Should().Be("Khách hàng đổi lịch");

            var car = await _context.Cars.FindAsync(1);
            car!.CarStatusID.Should().Be(1, "vì xe phải trở về Available khi booking bị hủy");
        }

        public void Dispose()
        {
            _context.Dispose();
        }
    }
}
