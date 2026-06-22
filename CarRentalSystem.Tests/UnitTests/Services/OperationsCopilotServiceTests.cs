using System.Text.Json;
using CarRentalSystem.Areas.Admin.Models;
using CarRentalSystem.Data;
using CarRentalSystem.Models;
using CarRentalSystem.Services;
using CarRentalSystem.Tests.Helpers;
using FluentAssertions;
using Xunit;

namespace CarRentalSystem.Tests.UnitTests.Services
{
    public class OperationsCopilotServiceTests : IDisposable
    {
        private readonly ApplicationDbContext _context;
        private readonly OperationsCopilotService _service;

        public OperationsCopilotServiceTests()
        {
            _context = MockRepositoryHelper.CreateSeededDbContext();
            _service = new OperationsCopilotService(_context, new EnterpriseOperationsService(_context));
        }

        [Fact]
        [Trait("Module", "OperationsCopilot")]
        [Trait("Type", "Regression")]
        public async Task OpsSnapshot_SurfacesCommandCenterRiskSignals()
        {
            SeedOpsSignals(DateTime.UtcNow);

            var snapshot = await _service.GetOpsSnapshotAsync();

            snapshot.RiskScore.Should().BeGreaterThan(0);
            snapshot.TopSignals.Should().Contain(s => s.Message.Contains("PayOS reconciliation"));
            snapshot.Queues.Should().Contain(q => q.Count > 0 && q.Severity == EnterpriseSeverity.Critical);
        }

        [Fact]
        [Trait("Module", "OperationsCopilot")]
        [Trait("Type", "Regression")]
        public async Task MoneyExceptionReport_ExplainsProviderLedgerRefundAndPayoutQueues()
        {
            SeedOpsSignals(DateTime.UtcNow);

            var report = await _service.ExplainMoneyExceptionsAsync();

            report.PayOSPaidWithoutLedger.Should().ContainSingle(i => i.OrderCode == 900001);
            report.LedgerWithoutPayOS.Should().ContainSingle(i => i.OrderCode == 900002);
            report.PendingRefunds.Should().ContainSingle(i => i.BookingID == 2001);
            report.PendingPayouts.Should().ContainSingle(i => i.RefundRequestID == 3001);
            report.RecommendedActions.Should().Contain(a => a.Priority == "P0" && a.Owner == "Finance");
            AssertNoRawPii(report);
        }

        [Fact]
        [Trait("Module", "OperationsCopilot")]
        [Trait("Type", "Regression")]
        public async Task VehicleTimeline_ComposesGpsCameraBookingLegalRiskWithoutRawPii()
        {
            var now = DateTime.UtcNow;
            SeedOpsSignals(now);

            var timeline = await _service.InspectVehicleTimelineAsync(carId: 1);

            timeline.CarID.Should().Be(1);
            timeline.RiskScore.Should().BeGreaterThan(50);
            timeline.RiskSignals.Should().Contain(s => s.Area == "Camera");
            timeline.Timeline.Select(e => e.Type).Should().Contain(new[] { "Booking", "GPS", "Camera", "Contract", "Handover" });
            timeline.RecommendedActions.Any(a => a.Priority == "P0" || a.Priority == "P1").Should().BeTrue();
            AssertNoRawPii(timeline);
        }

        [Fact]
        [Trait("Module", "OperationsCopilot")]
        [Trait("Type", "Regression")]
        public async Task CameraRiskSummary_CountsAnprRiskEvents()
        {
            SeedOpsSignals(DateTime.UtcNow);

            var summary = await _service.SummarizeCameraRiskAsync();

            summary.TotalEvents24h.Should().Be(3);
            summary.AreaMismatchCount.Should().Be(1);
            summary.UnknownPlateCount.Should().Be(1);
            summary.LowConfidenceCount.Should().Be(1);
            summary.RecentRiskEvents.Should().HaveCount(3);
        }

        private void SeedOpsSignals(DateTime now)
        {
            var activeStatus = _context.BookingStatuses.Single(s => s.Name == BookingStatusConstants.Active);

            _context.Cars.Single(c => c.CarID == 1).NextMaintenanceDate = now.AddDays(3);
            _context.Cars.Single(c => c.CarID == 1).LicensePlateNormalized = "51A12345";

            _context.Bookings.Add(new Booking
            {
                BookingID = 2001,
                UserID = 2,
                CarID = 1,
                StartDate = now.AddDays(-2),
                EndDate = now.AddHours(-3),
                PickupLocationID = 1,
                ReturnLocationID = 1,
                TotalHours = 48,
                TotalPrice = 2500000m,
                BookingStatusID = activeStatus.BookingStatusID,
                BookingStatus = activeStatus,
                DepositStatus = DepositStatusConstants.Holding
            });

            _context.VehicleTrackings.Add(new VehicleTracking
            {
                CarID = 1,
                Latitude = 10.7769,
                Longitude = 106.7009,
                Speed = 0,
                Timestamp = now.AddHours(-26),
                Source = "HardwareGps",
                BatteryLevel = 12
            });

            _context.Alerts.Add(new Alert
            {
                CarID = 1,
                AlertType = "GpsLost",
                Status = "Active",
                AlertTime = now.AddHours(-2),
                Description = "GPS lost during rental"
            });

            _context.CameraDevices.Add(new CameraDevice
            {
                CameraDeviceID = 501,
                Code = "GATE-IN-01",
                Name = "Gate In",
                Status = CameraDeviceStatusConstants.Active,
                EncryptedWebhookSecret = "test"
            });

            _context.CameraGateEvents.AddRange(
                new CameraGateEvent
                {
                    CameraDeviceID = 501,
                    CarID = 1,
                    LicensePlateRaw = "51A-12345",
                    LicensePlateNormalized = "51A12345",
                    Direction = CameraGateDirectionConstants.In,
                    Confidence = 0.96m,
                    CapturedAt = now.AddHours(-1),
                    MatchStatus = CameraGateMatchStatusConstants.AreaMismatch,
                    FailureReason = "Wrong area"
                },
                new CameraGateEvent
                {
                    CameraDeviceID = 501,
                    LicensePlateRaw = "99Z-99999",
                    LicensePlateNormalized = "99Z99999",
                    Direction = CameraGateDirectionConstants.In,
                    Confidence = 0.91m,
                    CapturedAt = now.AddMinutes(-40),
                    MatchStatus = CameraGateMatchStatusConstants.UnknownPlate,
                    FailureReason = "Unknown plate"
                },
                new CameraGateEvent
                {
                    CameraDeviceID = 501,
                    CarID = 1,
                    LicensePlateRaw = "51A-12345",
                    LicensePlateNormalized = "51A12345",
                    Direction = CameraGateDirectionConstants.Out,
                    Confidence = 0.42m,
                    CapturedAt = now.AddMinutes(-20),
                    MatchStatus = CameraGateMatchStatusConstants.LowConfidence,
                    FailureReason = "Low confidence"
                });

            _context.PayOSTransactions.Add(new PayOSTransaction
            {
                PayOSTransactionID = 7001,
                OrderCode = 900001,
                Amount = 500000m,
                Status = PaymentStatusConstants.Paid,
                CreatedAt = now.AddHours(-5),
                PaidAt = now.AddHours(-4)
            });

            _context.PaymentLedgerEntries.Add(new PaymentLedgerEntry
            {
                PaymentLedgerEntryID = 8001,
                Provider = "PayOS",
                OrderCode = 900002,
                EntryType = PaymentLedgerEntryTypeConstants.DepositCharge,
                Amount = 500000m,
                Status = PaymentStatusConstants.Paid,
                CreatedAt = now.AddHours(-3)
            });

            _context.RefundRequests.Add(new RefundRequest
            {
                RefundRequestID = 3001,
                BookingID = 2001,
                Amount = 200000m,
                Reason = "Deposit review",
                Status = RefundRequestStatusConstants.PendingReview,
                CreatedAt = now.AddHours(-2)
            });

            _context.RefundPayouts.Add(new RefundPayout
            {
                RefundPayoutID = 4001,
                RefundRequestID = 3001,
                ReferenceId = "RF-3001",
                IdempotencyKey = "refund-3001",
                Amount = 200000m,
                ApprovalState = PaymentStatusConstants.PendingManual,
                TransactionState = PaymentStatusConstants.PendingManual,
                AttemptCount = 1,
                LastError = "Provider disabled",
                CreatedAt = now.AddHours(-1),
                EncryptedRecipientAccountNumber = "0900000002"
            });

            _context.DigitalContracts.Add(new DigitalContract
            {
                DigitalContractID = 6001,
                ContractNo = "DC-2001",
                BookingID = 2001,
                CustomerName = "Customer Test",
                CustomerIdentityCard = "012345678901",
                CustomerPhone = "0900000002",
                CustomerAddress = "Hidden address",
                CarLicensePlate = "51A-12345",
                CarModel = "Toyota Camry 2024",
                RentalStartDate = now.AddDays(-2),
                RentalEndDate = now.AddHours(-3),
                RentalPricePerDay = 1000000m,
                TotalContractValue = 2500000m,
                DepositAmount = 5000000m,
                TermsAndConditions = "Terms",
                IsSignedByCustomer = true,
                IsSignedByOwner = false,
                CreatedAt = now.AddDays(-2)
            });

            _context.HandoverRecords.Add(new HandoverRecord
            {
                BookingID = 2001,
                Type = "CheckOut",
                RecordDate = now.AddMinutes(-30),
                Odo = 10000,
                FuelLevel = "F"
            });

            _context.SupportTickets.Add(new SupportTicket
            {
                UserID = 2,
                TicketType = SupportTicketTypeConstants.Emergency,
                Priority = SupportTicketPriorityConstants.P0,
                Status = SupportTicketStatusConstants.Open,
                Subject = "Vehicle issue",
                Description = "Need support",
                FirstResponseDueAt = now.AddMinutes(-15),
                CreatedAt = now.AddHours(-1)
            });

            _context.SaveChanges();
        }

        private static void AssertNoRawPii(object value)
        {
            var json = JsonSerializer.Serialize(value);
            json.Should().NotContain("012345678901");
            json.Should().NotContain("0900000002");
            json.Should().NotContain("customer@test.com");
            json.Should().NotContain("Hidden address");
            json.Should().NotContain("EncryptedRecipientAccountNumber");
        }

        public void Dispose()
        {
            _context.Dispose();
        }
    }
}
