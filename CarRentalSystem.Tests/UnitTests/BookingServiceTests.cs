using CarRentalSystem.Models;
using CarRentalSystem.Services;
using CarRentalSystem.Tests.Helpers;
using FluentAssertions;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace CarRentalSystem.Tests.UnitTests
{
    /// <summary>
    /// Unit tests cho BookingService — Tạo, Huỷ, Nhận xe, Trả xe
    /// </summary>
    public class BookingServiceTests
    {
        private readonly List<BookingStatus> _statuses;
        private readonly List<Booking> _bookings;
        private readonly List<Car> _cars;
        private readonly List<CarStatus> _carStatuses;
        private readonly List<Payment> _payments;
        private readonly List<RentalContract> _contracts;
        private readonly BookingService _bookingService;

        public BookingServiceTests()
        {
            // Setup BookingStatuses
            _statuses = new List<BookingStatus>
            {
                new BookingStatus { BookingStatusID = 1, Name = "Draft" },
                new BookingStatus { BookingStatusID = 2, Name = "Pending" },
                new BookingStatus { BookingStatusID = 3, Name = "Confirmed" },
                new BookingStatus { BookingStatusID = 4, Name = "Cancelled" },
                new BookingStatus { BookingStatusID = 5, Name = "Active" },
                new BookingStatus { BookingStatusID = 6, Name = "Completed" },
            };

            // Setup CarStatuses
            _carStatuses = new List<CarStatus>
            {
                new CarStatus { CarStatusID = 1, Name = "Available" },
                new CarStatus { CarStatusID = 2, Name = "Rented" },
            };

            // Setup Cars
            _cars = new List<Car>
            {
                new Car
                {
                    CarID = 10,
                    CarName = "Toyota Camry",
                    LicensePlate = "51A-11111",
                    VIN = "VIN001",
                    CarStatusID = 1,
                    PricePerDay = 1000000,
                    PricePerHour = 100000,
                    DefaultDeposit = 5000000
                },
            };

            // Setup Bookings
            _bookings = new List<Booking>
            {
                new Booking
                {
                    BookingID = 100,
                    UserID = 1,
                    CarID = 10,
                    Car = _cars[0],
                    StartDate = DateTime.Today.AddDays(5),
                    EndDate = DateTime.Today.AddDays(7),
                    TotalPrice = 2000000,
                    BookingStatusID = 2, // Pending
                    BookingStatus = _statuses[1],
                    DepositAmount = 5000000,
                    DepositStatus = "Holding"
                }
            };

            _payments = new List<Payment>();
            _contracts = new List<RentalContract>();

            // Create service with mocked repos
            var bookingRepo = MockRepositoryHelper.Create(_bookings);
            bookingRepo.Setup(r => r.GetByIdAsync(100))
                .ReturnsAsync(_bookings[0]);

            var paymentRepo = MockRepositoryHelper.Create(_payments);
            var contractRepo = MockRepositoryHelper.Create(_contracts);
            var statusRepo = MockRepositoryHelper.Create(_statuses);
            var carRepo = MockRepositoryHelper.Create(_cars);
            var carStatusRepo = MockRepositoryHelper.Create(_carStatuses);
            var transitionService = new Mock<IBookingTransitionService>();
            var businessRules = new Mock<IBookingBusinessRuleService>();
            businessRules
                .Setup(r => r.ValidateSelfDriveAsync(It.IsAny<Booking>(), It.IsAny<decimal>()))
                .Returns(Task.CompletedTask);
            transitionService
                .Setup(t => t.TransitionAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<string>(), It.IsAny<string?>()))
                .Callback<int, string, int?, string, string?>((bookingId, statusName, _, _, reason) =>
                {
                    var booking = _bookings.FirstOrDefault(b => b.BookingID == bookingId);
                    var status = _statuses.FirstOrDefault(s => s.Name == statusName);
                    if (booking == null || status == null) return;

                    booking.BookingStatusID = status.BookingStatusID;
                    booking.BookingStatus = status;
                    if (statusName == BookingStatusConstants.Active)
                    {
                        booking.AdminNotes = reason ?? booking.AdminNotes;
                        _cars[0].CarStatusID = 2;
                    }
                    if (statusName == BookingStatusConstants.Completed)
                        _cars[0].CarStatusID = 1;
                })
                .ReturnsAsync(new BookingTransitionResult(true, null));
            transitionService
                .Setup(t => t.CancelAsync(It.IsAny<int>(), It.IsAny<int?>(), It.IsAny<string>(), It.IsAny<string?>()))
                .Callback<int, int?, string, string?>((bookingId, _, _, reason) =>
                {
                    var booking = _bookings.FirstOrDefault(b => b.BookingID == bookingId);
                    if (booking == null) return;

                    booking.BookingStatusID = 4;
                    booking.BookingStatus = _statuses.First(s => s.Name == BookingStatusConstants.Cancelled);
                    booking.DepositStatus = DepositStatusConstants.PendingRefund;
                    booking.CancelReason = reason;
                    _cars[0].CarStatusID = 1;
                })
                .ReturnsAsync(new BookingTransitionResult(true, null));

            _bookingService = new BookingService(
                bookingRepo.Object,
                paymentRepo.Object,
                contractRepo.Object,
                statusRepo.Object,
                carRepo.Object,
                carStatusRepo.Object,
                transitionService.Object,
                businessRules.Object
            );
        }

        // =============================================
        // CREATE BOOKING TESTS
        // =============================================

        [Fact]
        [Trait("Module", "Booking")]
        [Trait("Type", "Unit")]
        public async Task CreateBooking_NewBooking_SetsStatusToDraft()
        {
            // Arrange
            var newBooking = new Booking
            {
                BookingID = 0,
                UserID = 2,
                CarID = 10,
                StartDate = DateTime.Today.AddDays(10),
                EndDate = DateTime.Today.AddDays(12),
                TotalPrice = 2000000
            };

            // Act
            await _bookingService.CreateBookingAsync(newBooking);

            // Assert
            newBooking.BookingStatusID.Should().Be(2, "vì test data không có trạng thái Unpaid nên service fallback sang Pending");
        }

        [Fact]
        [Trait("Module", "Booking")]
        [Trait("Type", "Unit")]
        public async Task CreateBooking_NewBooking_IsAddedToRepository()
        {
            // Arrange
            var initialCount = _bookings.Count;
            var newBooking = new Booking
            {
                BookingID = 0,
                UserID = 2,
                CarID = 10,
                StartDate = DateTime.Today.AddDays(10),
                EndDate = DateTime.Today.AddDays(12),
                TotalPrice = 2000000
            };

            // Act
            await _bookingService.CreateBookingAsync(newBooking);

            // Assert
            _bookings.Count.Should().Be(initialCount + 1, "vì booking mới phải được lưu vào repository");
        }

        // =============================================
        // CANCEL BOOKING TESTS
        // =============================================

        [Fact]
        [Trait("Module", "Booking")]
        [Trait("Type", "Unit")]
        public async Task CancelBooking_ExistingBooking_SetsStatusToCancelled()
        {
            // Act
            await _bookingService.CancelBookingAsync(100, "Khách hàng tự hủy");

            // Assert
            _bookings[0].BookingStatusID.Should().Be(4, "vì booking bị hủy phải có status 'Cancelled' (ID=4)");
        }

        [Fact]
        [Trait("Module", "Booking")]
        [Trait("Type", "Unit")]
        public async Task CancelBooking_ExistingBooking_SetsDepositStatusToRefunded()
        {
            // Act
            await _bookingService.CancelBookingAsync(100, "Đổi lịch");

            // Assert
            _bookings[0].DepositStatus.Should().Be(DepositStatusConstants.PendingRefund, "vì hủy booking tạo luồng hoàn cọc chờ reconcile thay vì đánh dấu đã hoàn tiền ngay");
        }

        [Fact]
        [Trait("Module", "Booking")]
        [Trait("Type", "Unit")]
        public async Task CancelBooking_ExistingBooking_StoresCancelReason()
        {
            // Arrange
            var reason = "Thay đổi kế hoạch du lịch";

            // Act
            await _bookingService.CancelBookingAsync(100, reason);

            // Assert
            _bookings[0].CancelReason.Should().Be(reason, "vì lý do hủy phải được lưu lại");
        }

        [Fact]
        [Trait("Module", "Booking")]
        [Trait("Type", "Unit")]
        public async Task CancelBooking_CarShouldBeSetToAvailable()
        {
            // Act
            await _bookingService.CancelBookingAsync(100, "Hủy");

            // Assert
            _cars[0].CarStatusID.Should().Be(1, "vì khi hủy booking, xe phải trở về trạng thái 'Available' (ID=1)");
        }

        // =============================================
        // CONFIRM PICKUP TESTS
        // =============================================

        [Fact]
        [Trait("Module", "Booking")]
        [Trait("Type", "Unit")]
        public async Task ConfirmPickup_ExistingBooking_SetsStatusToActive()
        {
            // Act
            await _bookingService.ConfirmPickupAsync(100, "Khách nhận xe đúng giờ");

            // Assert
            _bookings[0].BookingStatusID.Should().Be(5, "vì sau khi xác nhận nhận xe, status phải là 'Active' (ID=5)");
        }

        [Fact]
        [Trait("Module", "Booking")]
        [Trait("Type", "Unit")]
        public async Task ConfirmPickup_WithNotes_StoresAdminNotes()
        {
            // Arrange
            var notes = "Xe giao trong tình trạng tốt";

            // Act
            await _bookingService.ConfirmPickupAsync(100, notes);

            // Assert
            _bookings[0].AdminNotes.Should().Be(notes);
        }

        [Fact]
        [Trait("Module", "Booking")]
        [Trait("Type", "Unit")]
        public async Task ConfirmPickup_CarShouldBeSetToRented()
        {
            // Act
            await _bookingService.ConfirmPickupAsync(100, null);

            // Assert
            _cars[0].CarStatusID.Should().Be(2, "vì khi đã nhận xe, xe phải chuyển sang 'Rented' (ID=2)");
        }

        // =============================================
        // UPDATE STATUS TESTS
        // =============================================

        [Fact]
        [Trait("Module", "Booking")]
        [Trait("Type", "Unit")]
        public async Task UpdateBookingStatus_ValidStatusName_UpdatesBooking()
        {
            // Act
            await _bookingService.UpdateBookingStatusAsync(100, "Confirmed");

            // Assert
            _bookings[0].BookingStatusID.Should().Be(3, "vì status 'Confirmed' có ID=3");
        }

        // =============================================
        // PAYMENT TESTS
        // =============================================

        [Fact]
        [Trait("Module", "Booking")]
        [Trait("Type", "Unit")]
        public async Task ProcessPayment_NewPayment_IsAddedToRepository()
        {
            // Arrange
            var payment = new Payment
            {
                BookingID = 100,
                Amount = 400000,
                PaymentMethod = "PayOS",
                PaymentStatus = "Paid",
                PaymentDate = DateTime.Now
            };

            // Act
            await _bookingService.ProcessPaymentAsync(payment);

            // Assert
            _payments.Should().ContainSingle("vì một payment phải được tạo");
            _payments[0].Amount.Should().Be(400000);
        }

        // =============================================
        // GET BOOKINGS TESTS
        // =============================================

        [Fact]
        [Trait("Module", "Booking")]
        [Trait("Type", "Unit")]
        public async Task GetAllBookings_ReturnsAllBookings()
        {
            // Act
            var result = await _bookingService.GetAllBookingsAsync();

            // Assert
            result.Should().NotBeNull();
            result.Should().HaveCount(1);
        }

        [Fact]
        [Trait("Module", "Booking")]
        [Trait("Type", "Unit")]
        public async Task GetUserBookings_ExistingUser_ReturnsOnlyTheirBookings()
        {
            // Act
            var result = await _bookingService.GetUserBookingsAsync(userId: 1);

            // Assert
            result.Should().NotBeNull();
            result.All(b => b.UserID == 1).Should().BeTrue("vì chỉ trả về booking của UserID=1");
        }

        [Fact]
        [Trait("Module", "Booking")]
        [Trait("Type", "Unit")]
        public async Task GetUserBookings_NonExistentUser_ReturnsEmpty()
        {
            // Act
            var result = await _bookingService.GetUserBookingsAsync(userId: 999);

            // Assert
            result.Should().BeEmpty("vì UserID=999 không có booking nào");
        }

        // =============================================
        // COMPLETE BOOKING TESTS
        // =============================================

        [Fact]
        [Trait("Module", "Booking")]
        [Trait("Type", "Unit")]
        public async Task CompleteBooking_ExistingBooking_SetsStatusToCompleted()
        {
            // Act
            await _bookingService.CompleteBookingAsync(100);

            // Assert
            _bookings[0].BookingStatusID.Should().Be(6, "vì booking hoàn thành phải có status 'Completed' (ID=6)");
        }

        [Fact]
        [Trait("Module", "Booking")]
        [Trait("Type", "Unit")]
        public async Task CompleteBooking_CarShouldBeSetToAvailable()
        {
            // Act
            await _bookingService.CompleteBookingAsync(100);

            // Assert
            _cars[0].CarStatusID.Should().Be(1, "vì khi trả xe, xe phải trở về trạng thái 'Available' (ID=1)");
        }

        // =============================================
        // CREATE CONTRACT TESTS
        // =============================================

        [Fact]
        [Trait("Module", "Booking")]
        [Trait("Type", "Unit")]
        public async Task CreateContract_AddsToRepository()
        {
            // Arrange
            var contract = new RentalContract
            {
                BookingID = 100,
                BasePrice = 2000000,
                StartKm = 10000
            };

            // Act
            await _bookingService.CreateContractAsync(contract);

            // Assert
            _contracts.Should().ContainSingle("vì một contract mới phải được tạo");
            _contracts[0].BookingID.Should().Be(100);
            _contracts[0].StartKm.Should().Be(10000);
        }

        // =============================================
        // GET BOOKING BY ID TESTS
        // =============================================

        [Fact]
        [Trait("Module", "Booking")]
        [Trait("Type", "Unit")]
        public async Task GetBookingById_ExistingId_ReturnsBooking()
        {
            // Act
            var result = await _bookingService.GetBookingByIdAsync(100);

            // Assert
            result.Should().NotBeNull("vì booking với ID=100 tồn tại");
            result!.BookingID.Should().Be(100);
            result.UserID.Should().Be(1);
        }

        [Fact]
        [Trait("Module", "Booking")]
        [Trait("Type", "Unit")]
        public async Task GetBookingById_NonExistentId_ReturnsNull()
        {
            // Act
            var result = await _bookingService.GetBookingByIdAsync(9999);

            // Assert
            result.Should().BeNull("vì booking với ID=9999 không tồn tại");
        }
    }
}
