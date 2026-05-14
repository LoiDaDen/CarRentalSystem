using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CarRentalSystem.Data;
using CarRentalSystem.Models;
using CarRentalSystem.ViewModels;
using Microsoft.EntityFrameworkCore;
using PayOS;
using PayOS.Models.V2.PaymentRequests;
using PayOS.Models.Webhooks;

namespace CarRentalSystem.Services
{
    public class PayOSPaymentService : IPayOSPaymentService
    {
        private const string DepositPaymentMethod = "PayOS";
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = true
        };

        private readonly ApplicationDbContext _context;
        private readonly IEmailService _emailService;
        private readonly IPaymentLedgerService _ledger;
        private readonly IReservationService _reservationService;
        private readonly IReferralService _referralService;
        private readonly ILongTermRentalService _longTermRentalService;
        private readonly PayOSClient? _payOS;
        private readonly ILogger<PayOSPaymentService> _logger;

        public bool IsConfigured => _payOS != null;

        public PayOSPaymentService(
            ApplicationDbContext context,
            IEmailService emailService,
            IConfiguration configuration,
            IPaymentLedgerService ledger,
            IReservationService reservationService,
            IReferralService referralService,
            ILongTermRentalService longTermRentalService,
            ILogger<PayOSPaymentService> logger)
        {
            _context = context;
            _emailService = emailService;
            _ledger = ledger;
            _reservationService = reservationService;
            _referralService = referralService;
            _longTermRentalService = longTermRentalService;
            _logger = logger;

            var clientId = configuration["PayOS:ClientId"] ?? string.Empty;
            var apiKey = configuration["PayOS:ApiKey"] ?? string.Empty;
            var checksumKey = configuration["PayOS:ChecksumKey"] ?? string.Empty;

            if (!string.IsNullOrWhiteSpace(clientId) &&
                !string.IsNullOrWhiteSpace(apiKey) &&
                !string.IsNullOrWhiteSpace(checksumKey))
            {
                _payOS = new PayOSClient(new PayOSOptions
                {
                    ClientId = clientId,
                    ApiKey = apiKey,
                    ChecksumKey = checksumKey
                });
            }
        }

        public async Task<PayOSCreatePaymentResult> CreateHoldPaymentAsync(BookingHold hold, CheckoutQuoteViewModel quote, string baseUrl)
        {
            if (_payOS == null)
                return new PayOSCreatePaymentResult(false, null, "PayOS is not configured.", null);

            if (quote.HoldingFee <= 0)
                return new PayOSCreatePaymentResult(false, null, "Holding fee is invalid.", null);

            var orderCode = await GenerateUniqueOrderCodeAsync(hold.BookingHoldID);
            var description = $"GIU CHO {hold.BookingHoldID}";
            var transaction = new PayOSTransaction
            {
                BookingHoldID = hold.BookingHoldID,
                UserID = hold.UserID,
                OrderCode = orderCode,
                Amount = quote.HoldingFee,
                Status = PaymentStatusConstants.Pending,
                PayOSDescription = description
            };

            var cleanBaseUrl = baseUrl.TrimEnd('/');
            var amountAsLong = (long)Math.Round(quote.HoldingFee, 0, MidpointRounding.AwayFromZero);
            var request = new CreatePaymentLinkRequest
            {
                OrderCode = orderCode,
                Amount = amountAsLong,
                Description = description,
                BuyerName = quote.CustomerName,
                BuyerEmail = quote.CustomerEmail,
                BuyerPhone = quote.CustomerPhone,
                Items = new List<PaymentLinkItem>
                {
                    new()
                    {
                        Name = $"Phí giữ chỗ xe {quote.CarName}",
                        Quantity = 1,
                        Price = amountAsLong
                    }
                },
                ReturnUrl = $"{cleanBaseUrl}/Payment/Hold?orderCode={orderCode}",
                CancelUrl = $"{cleanBaseUrl}/Payment/CancelHold?orderCode={orderCode}"
            };

            try
            {
                _context.PayOSTransactions.Add(transaction);
                await _context.SaveChangesAsync();

                var response = await _payOS.PaymentRequests.CreateAsync(request);
                ApplyPayOSResponse(transaction, response);

                await _ledger.RecordAsync(new PaymentLedgerEntry
                {
                    Provider = "PayOS",
                    OrderCode = transaction.OrderCode,
                    EntryType = PaymentLedgerEntryTypeConstants.DepositCharge,
                    Amount = quote.HoldingFee,
                    Status = PaymentStatusConstants.Pending,
                    IdempotencyKey = $"payos-hold-created:{transaction.OrderCode}",
                    Description = $"PayOS holding payment created for hold #{hold.BookingHoldID}."
                });

                await _context.SaveChangesAsync();
                return new PayOSCreatePaymentResult(true, transaction.CheckoutUrl, null, transaction);
            }
            catch (Exception ex)
            {
                await MarkCreatePaymentFailedAsync(transaction, ex, "Failed to create PayOS hold payment for hold {EntityId}.", hold.BookingHoldID);
                return new PayOSCreatePaymentResult(false, null, "Không thể tạo thanh toán giữ chỗ PayOS. Vui lòng thử lại sau.", transaction.PayOSTransactionID > 0 ? transaction : null);
            }
        }

        public async Task<PayOSCreatePaymentResult> CreateCartHoldPaymentAsync(BookingTransaction cartTransaction, decimal holdingFee, string baseUrl)
        {
            if (_payOS == null)
                return new PayOSCreatePaymentResult(false, null, "PayOS is not configured.", null);

            if (holdingFee <= 0)
                return new PayOSCreatePaymentResult(false, null, "Holding fee is invalid.", null);

            var orderCode = await GenerateUniqueOrderCodeAsync(cartTransaction.TransactionID);
            var description = $"GIO HANG {cartTransaction.TransactionID}";
            var transaction = new PayOSTransaction
            {
                TransactionID = cartTransaction.TransactionID,
                UserID = cartTransaction.UserID,
                OrderCode = orderCode,
                Amount = holdingFee,
                Status = PaymentStatusConstants.Pending,
                PayOSDescription = description
            };

            var cleanBaseUrl = baseUrl.TrimEnd('/');
            var amountAsLong = (long)Math.Round(holdingFee, 0, MidpointRounding.AwayFromZero);
            var request = new CreatePaymentLinkRequest
            {
                OrderCode = orderCode,
                Amount = amountAsLong,
                Description = description,
                Items = new List<PaymentLinkItem>
                {
                    new()
                    {
                        Name = $"Phí giữ chỗ giỏ hàng #{cartTransaction.TransactionID}",
                        Quantity = 1,
                        Price = amountAsLong
                    }
                },
                ReturnUrl = $"{cleanBaseUrl}/Payment/Transaction?orderCode={orderCode}",
                CancelUrl = $"{cleanBaseUrl}/Payment/CancelTransaction?orderCode={orderCode}"
            };

            try
            {
                _context.PayOSTransactions.Add(transaction);
                await _context.SaveChangesAsync();

                var response = await _payOS.PaymentRequests.CreateAsync(request);
                ApplyPayOSResponse(transaction, response);

                await _ledger.RecordAsync(new PaymentLedgerEntry
                {
                    TransactionID = cartTransaction.TransactionID,
                    Provider = "PayOS",
                    OrderCode = transaction.OrderCode,
                    EntryType = PaymentLedgerEntryTypeConstants.DepositCharge,
                    Amount = holdingFee,
                    Status = PaymentStatusConstants.Pending,
                    IdempotencyKey = $"payos-cart-created:{transaction.OrderCode}",
                    Description = $"PayOS cart holding payment created for transaction #{cartTransaction.TransactionID}."
                });

                await _context.SaveChangesAsync();
                return new PayOSCreatePaymentResult(true, transaction.CheckoutUrl, null, transaction);
            }
            catch (Exception ex)
            {
                await MarkCreatePaymentFailedAsync(transaction, ex, "Failed to create PayOS cart payment for transaction {EntityId}.", cartTransaction.TransactionID);
                return new PayOSCreatePaymentResult(false, null, "Không thể tạo thanh toán giữ chỗ giỏ hàng. Vui lòng thử lại sau.", transaction.PayOSTransactionID > 0 ? transaction : null);
            }
        }

        public async Task<PayOSCreatePaymentResult> CreateDepositPaymentAsync(Booking booking, string returnUrl, string cancelUrl)
        {
            if (_payOS == null)
                return new PayOSCreatePaymentResult(false, null, "PayOS is not configured.", null);

            var existingPaid = await _context.Payments
                .AnyAsync(p => p.BookingID == booking.BookingID && p.PaymentStatus == PaymentStatusConstants.Paid);
            if (existingPaid)
                return new PayOSCreatePaymentResult(false, null, "Reservation fee was already paid.", null);

            var bookingFromDb = await _context.Bookings
                .Include(b => b.Car)
                .Include(b => b.User)
                .Include(b => b.Payment)
                .FirstOrDefaultAsync(b => b.BookingID == booking.BookingID);
            if (bookingFromDb == null)
                return new PayOSCreatePaymentResult(false, null, "Booking not found.", null);

            var reservationFee = ResolveReservationFee(bookingFromDb);
            if (reservationFee <= 0)
                return new PayOSCreatePaymentResult(false, null, "Reservation fee is invalid.", null);

            bookingFromDb.ReservationFee = reservationFee;
            bookingFromDb.DepositStatus = DepositStatusConstants.Unpaid;

            var payment = bookingFromDb.Payment ?? await _context.Payments.FirstOrDefaultAsync(p => p.BookingID == bookingFromDb.BookingID);
            if (payment == null)
            {
                payment = new Payment
                {
                    BookingID = bookingFromDb.BookingID,
                    Amount = reservationFee,
                    PaymentMethod = DepositPaymentMethod,
                    PaymentStatus = PaymentStatusConstants.Pending,
                    PaymentDate = DateTime.UtcNow
                };
                _context.Payments.Add(payment);
            }
            else
            {
                payment.Amount = reservationFee;
                payment.PaymentMethod = DepositPaymentMethod;
                payment.PaymentStatus = PaymentStatusConstants.Pending;
                payment.PaymentDate = DateTime.UtcNow;
            }

            var transaction = new PayOSTransaction
            {
                BookingID = bookingFromDb.BookingID,
                UserID = bookingFromDb.UserID,
                Payment = payment,
                OrderCode = await GenerateUniqueOrderCodeAsync(bookingFromDb.BookingID),
                Amount = reservationFee,
                Status = PaymentStatusConstants.Pending,
                PayOSDescription = $"GIU CHO {bookingFromDb.BookingID}"
            };
            _context.PayOSTransactions.Add(transaction);
            await _context.SaveChangesAsync();

            var amountAsLong = (long)Math.Round(reservationFee, 0, MidpointRounding.AwayFromZero);
            var request = new CreatePaymentLinkRequest
            {
                OrderCode = transaction.OrderCode,
                Amount = amountAsLong,
                Description = transaction.PayOSDescription,
                BuyerName = bookingFromDb.User?.Name,
                BuyerEmail = bookingFromDb.User?.Email,
                BuyerPhone = bookingFromDb.User?.Phone,
                Items = new List<PaymentLinkItem>
                {
                    new()
                    {
                        Name = $"Phí giữ chỗ booking #{bookingFromDb.BookingID}",
                        Quantity = 1,
                        Price = amountAsLong
                    }
                },
                CancelUrl = cancelUrl,
                ReturnUrl = returnUrl
            };

            try
            {
                var response = await _payOS.PaymentRequests.CreateAsync(request);
                ApplyPayOSResponse(transaction, response);

                await _ledger.RecordAsync(new PaymentLedgerEntry
                {
                    BookingID = bookingFromDb.BookingID,
                    PaymentID = payment.PaymentID,
                    Provider = "PayOS",
                    OrderCode = transaction.OrderCode,
                    EntryType = PaymentLedgerEntryTypeConstants.DepositCharge,
                    Amount = reservationFee,
                    Status = PaymentStatusConstants.Pending,
                    IdempotencyKey = $"payos-created:{transaction.OrderCode}",
                    Description = "PayOS reservation checkout created."
                });

                await _context.SaveChangesAsync();
                return new PayOSCreatePaymentResult(true, response.CheckoutUrl, null, transaction);
            }
            catch (Exception ex)
            {
                payment.PaymentStatus = PaymentStatusConstants.Failed;
                transaction.Status = PaymentStatusConstants.Failed;
                transaction.FailedAt = DateTime.UtcNow;
                transaction.PayOSDescription = ex.Message;
                await _context.SaveChangesAsync();
                return new PayOSCreatePaymentResult(false, null, ex.Message, transaction);
            }
        }

        public async Task<PayOSWebhookResult> HandleWebhookAsync(Webhook webhookData)
        {
            if (_payOS == null)
                return new PayOSWebhookResult(false, false, "PayOS not configured", null, null);

            var rawPayload = JsonSerializer.Serialize(webhookData);
            var payloadHash = ComputeSha256(rawPayload);
            var data = await _payOS.Webhooks.VerifyAsync(webhookData);
            if (data == null)
                return new PayOSWebhookResult(false, false, "Invalid webhook signature", null, null);

            var eventKey = $"payos:{data.OrderCode}:{data.Code}:{data.Amount}:{payloadHash}";
            var existingEvent = await _context.PaymentWebhookEvents
                .FirstOrDefaultAsync(e => e.Provider == "PayOS" && e.EventKey == eventKey);
            if (existingEvent?.Status == "Processed")
            {
                var existingTransaction = await _context.PayOSTransactions.FirstOrDefaultAsync(t => t.OrderCode == data.OrderCode);
                return new PayOSWebhookResult(true, existingTransaction?.Status == PaymentStatusConstants.Paid, "Duplicate webhook ignored", existingTransaction?.BookingID, existingTransaction?.Amount);
            }

            var webhookEvent = existingEvent ?? new PaymentWebhookEvent
            {
                Provider = "PayOS",
                EventKey = eventKey,
                PayloadHash = payloadHash,
                OrderCode = data.OrderCode,
                RawPayload = rawPayload,
                Status = "Received"
            };
            if (existingEvent == null)
                _context.PaymentWebhookEvents.Add(webhookEvent);

            var transaction = await _context.PayOSTransactions
                .Include(t => t.Booking)
                    .ThenInclude(b => b!.User)
                .Include(t => t.Booking)
                    .ThenInclude(b => b!.Car)
                .Include(t => t.BookingHold)
                    .ThenInclude(h => h!.Car)
                .Include(t => t.BookingHold)
                    .ThenInclude(h => h!.User)
                .Include(t => t.BookingTransaction)
                .Include(t => t.Payment)
                .FirstOrDefaultAsync(t => t.OrderCode == data.OrderCode);

            if (transaction == null)
            {
                webhookEvent.Status = "Failed";
                webhookEvent.Error = "Transaction not found.";
                await _context.SaveChangesAsync();
                return new PayOSWebhookResult(false, false, "Transaction not found", null, null);
            }

            var expectedAmount = Math.Round(transaction.Amount, 0, MidpointRounding.AwayFromZero);
            var paidAmount = Math.Round((decimal)data.Amount, 0, MidpointRounding.AwayFromZero);
            if (paidAmount != expectedAmount)
            {
                webhookEvent.Status = "Rejected";
                webhookEvent.Error = $"Amount mismatch. Expected {expectedAmount}, received {paidAmount}.";
                await _context.SaveChangesAsync();
                return new PayOSWebhookResult(false, false, "Amount mismatch", transaction.BookingID, paidAmount);
            }

            transaction.PayOSCode = data.Code;
            transaction.WebhookPayload = rawPayload;

            var isPaid = string.Equals(data.Code, "00", StringComparison.OrdinalIgnoreCase);
            if (isPaid && transaction.TransactionID.HasValue && !transaction.BookingID.HasValue && !transaction.BookingHoldID.HasValue)
                return await ConvertPaidCartToBookingsAsync(transaction, webhookEvent);

            if (isPaid && transaction.BookingHoldID.HasValue && !transaction.BookingID.HasValue)
                return await ConvertPaidHoldToBookingAsync(transaction, webhookEvent);

            if (isPaid)
                return await ConfirmExistingBookingPaymentAsync(transaction, webhookEvent);

            await MarkTransactionFailedAsync(transaction, webhookEvent);
            return new PayOSWebhookResult(true, false, "Payment failed", transaction.BookingID, transaction.Amount);
        }

        public async Task<PayOSWebhookResult> ReconcilePaymentAsync(long orderCode, string source)
        {
            if (_payOS == null)
                return new PayOSWebhookResult(false, false, "PayOS not configured", null, null);

            PaymentLink paymentLink;
            try
            {
                paymentLink = await _payOS.PaymentRequests.GetAsync(orderCode);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not reconcile PayOS order {OrderCode} from {Source}.", orderCode, source);
                return new PayOSWebhookResult(false, false, "Could not query PayOS payment status", null, null);
            }

            var paidAmount = Math.Round((decimal)paymentLink.AmountPaid, 0, MidpointRounding.AwayFromZero);
            if (paymentLink.Status != PaymentLinkStatus.Paid)
                return new PayOSWebhookResult(true, false, $"Payment status is {paymentLink.Status}", null, paidAmount);

            var transaction = await _context.PayOSTransactions
                .Include(t => t.Booking)
                    .ThenInclude(b => b!.User)
                .Include(t => t.Booking)
                    .ThenInclude(b => b!.Car)
                .Include(t => t.BookingHold)
                    .ThenInclude(h => h!.Car)
                .Include(t => t.BookingHold)
                    .ThenInclude(h => h!.User)
                .Include(t => t.BookingTransaction)
                .Include(t => t.Payment)
                .FirstOrDefaultAsync(t => t.OrderCode == orderCode);

            var rawPayload = JsonSerializer.Serialize(new
            {
                source,
                paymentLink.Id,
                paymentLink.OrderCode,
                paymentLink.Amount,
                paymentLink.AmountPaid,
                paymentLink.AmountRemaining,
                status = paymentLink.Status.ToString(),
                paymentLink.Transactions
            });
            var eventKey = $"payos-return:{orderCode}:paid:{paidAmount}";
            var existingEvent = await _context.PaymentWebhookEvents
                .FirstOrDefaultAsync(e => e.Provider == "PayOS" && e.EventKey == eventKey);
            if (existingEvent?.Status == "Processed")
            {
                return new PayOSWebhookResult(
                    true,
                    transaction?.Status == PaymentStatusConstants.Paid,
                    "Duplicate return reconciliation ignored",
                    transaction?.BookingID,
                    paidAmount);
            }

            var reconcileEvent = existingEvent ?? new PaymentWebhookEvent
            {
                Provider = "PayOS",
                EventKey = eventKey,
                PayloadHash = ComputeSha256(rawPayload),
                OrderCode = orderCode,
                RawPayload = rawPayload,
                Status = "Received"
            };
            if (existingEvent == null)
                _context.PaymentWebhookEvents.Add(reconcileEvent);
            else
            {
                reconcileEvent.PayloadHash = ComputeSha256(rawPayload);
                reconcileEvent.RawPayload = rawPayload;
                reconcileEvent.Error = null;
                reconcileEvent.Status = "Received";
            }

            if (transaction == null)
            {
                reconcileEvent.Status = "Failed";
                reconcileEvent.Error = "Transaction not found.";
                await _context.SaveChangesAsync();
                return new PayOSWebhookResult(false, true, "Transaction not found", null, paidAmount);
            }

            var expectedAmount = Math.Round(transaction.Amount, 0, MidpointRounding.AwayFromZero);
            if (paidAmount != expectedAmount)
            {
                reconcileEvent.Status = "Rejected";
                reconcileEvent.Error = $"Amount mismatch. Expected {expectedAmount}, received {paidAmount}.";
                await _context.SaveChangesAsync();
                return new PayOSWebhookResult(false, false, "Amount mismatch", transaction.BookingID, paidAmount);
            }

            transaction.PayOSCode = "00";
            transaction.WebhookPayload = rawPayload;

            if (transaction.TransactionID.HasValue && !transaction.BookingID.HasValue && !transaction.BookingHoldID.HasValue)
                return await ConvertPaidCartToBookingsAsync(transaction, reconcileEvent);

            if (transaction.BookingHoldID.HasValue && !transaction.BookingID.HasValue)
                return await ConvertPaidHoldToBookingAsync(transaction, reconcileEvent);

            return await ConfirmExistingBookingPaymentAsync(transaction, reconcileEvent);
        }

        public async Task MarkCancelledAsync(int bookingId)
        {
            var payment = await _context.Payments.FirstOrDefaultAsync(p => p.BookingID == bookingId);
            if (payment != null && payment.PaymentStatus == PaymentStatusConstants.Pending)
                payment.PaymentStatus = PaymentStatusConstants.Failed;

            var transaction = await _context.PayOSTransactions
                .OrderByDescending(t => t.CreatedAt)
                .FirstOrDefaultAsync(t => t.BookingID == bookingId && t.Status == PaymentStatusConstants.Pending);

            if (transaction != null)
            {
                transaction.Status = PaymentStatusConstants.Cancelled;
                transaction.CancelledAt = DateTime.UtcNow;

                await _ledger.RecordAsync(new PaymentLedgerEntry
                {
                    BookingID = bookingId,
                    PaymentID = payment?.PaymentID,
                    Provider = "PayOS",
                    OrderCode = transaction.OrderCode,
                    EntryType = PaymentLedgerEntryTypeConstants.DepositCharge,
                    Amount = transaction.Amount,
                    Status = PaymentStatusConstants.Cancelled,
                    IdempotencyKey = $"payos-cancelled:{transaction.OrderCode}",
                    Description = "PayOS checkout cancelled by user."
                });
            }

            await _context.SaveChangesAsync();
        }

        public async Task MarkHoldCancelledAsync(long orderCode)
        {
            var transaction = await _context.PayOSTransactions
                .Include(t => t.BookingHold)
                .FirstOrDefaultAsync(t => t.OrderCode == orderCode);
            if (transaction == null) return;

            if (transaction.Status == PaymentStatusConstants.Pending)
            {
                transaction.Status = PaymentStatusConstants.Cancelled;
                transaction.CancelledAt = DateTime.UtcNow;
                await _ledger.RecordAsync(new PaymentLedgerEntry
                {
                    Provider = "PayOS",
                    OrderCode = transaction.OrderCode,
                    EntryType = PaymentLedgerEntryTypeConstants.DepositCharge,
                    Amount = transaction.Amount,
                    Status = PaymentStatusConstants.Cancelled,
                    IdempotencyKey = $"payos-hold-cancelled:{transaction.OrderCode}",
                    Description = $"PayOS holding checkout cancelled for hold #{transaction.BookingHoldID}."
                });
            }

            if (transaction.BookingHoldID.HasValue)
                await _reservationService.ReleaseHoldAsync(transaction.BookingHoldID.Value, "PayOS checkout cancelled.");

            if (transaction.TransactionID.HasValue)
            {
                var holds = await _context.BookingHolds
                    .Where(h => h.TransactionID == transaction.TransactionID.Value && h.Status == BookingHoldStatusConstants.Active)
                    .ToListAsync();
                foreach (var hold in holds)
                {
                    hold.Status = BookingHoldStatusConstants.Released;
                    hold.ReleasedAt = DateTime.UtcNow;
                }

                var bookingTransaction = await _context.BookingTransactions.FindAsync(transaction.TransactionID.Value);
                if (bookingTransaction != null && bookingTransaction.Status == BookingStatusConstants.Unpaid)
                    bookingTransaction.Status = PaymentStatusConstants.Cancelled;
            }

            await _context.SaveChangesAsync();
        }

        private async Task<PayOSWebhookResult> ConvertPaidHoldToBookingAsync(PayOSTransaction transaction, PaymentWebhookEvent webhookEvent)
        {
            var hold = transaction.BookingHold;
            if (hold == null)
            {
                webhookEvent.Status = "Failed";
                webhookEvent.Error = "Booking hold not found.";
                await _context.SaveChangesAsync();
                return new PayOSWebhookResult(false, true, "Booking hold not found", null, transaction.Amount);
            }

            if (transaction.Status == PaymentStatusConstants.Paid && transaction.BookingID.HasValue)
            {
                webhookEvent.Status = "Processed";
                webhookEvent.ProcessedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                return new PayOSWebhookResult(true, true, "Payment already processed", transaction.BookingID, transaction.Amount);
            }

            if (hold.Status != BookingHoldStatusConstants.Active || hold.ExpiresAt <= DateTime.UtcNow)
            {
                transaction.Status = DepositStatusConstants.UnderReview;
                transaction.PaidAt = DateTime.UtcNow;
                webhookEvent.Status = "Processed";
                webhookEvent.Error = "Paid after hold expired; manual review required.";
                webhookEvent.ProcessedAt = DateTime.UtcNow;
                await _ledger.RecordAsync(new PaymentLedgerEntry
                {
                    Provider = "PayOS",
                    OrderCode = transaction.OrderCode,
                    EntryType = PaymentLedgerEntryTypeConstants.DepositCharge,
                    Amount = transaction.Amount,
                    Status = DepositStatusConstants.UnderReview,
                    PostedAt = DateTime.UtcNow,
                    IdempotencyKey = $"payos-hold-expired-paid:{transaction.OrderCode}",
                    Description = $"Payment received after hold #{hold.BookingHoldID} expired."
                });
                await _context.SaveChangesAsync();
                return new PayOSWebhookResult(true, true, "Payment received after hold expired", null, transaction.Amount);
            }

            var quote = DeserializeQuote(hold.QuoteSnapshotJson);
            if (quote == null)
            {
                webhookEvent.Status = "Failed";
                webhookEvent.Error = "Quote snapshot not found.";
                await _context.SaveChangesAsync();
                return new PayOSWebhookResult(false, true, "Quote snapshot not found", null, transaction.Amount);
            }

            await using var dbTransaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            try
            {
                var availability = await _reservationService.CheckCarAvailabilityAsync(hold.CarID, hold.StartDate, hold.EndDate, null, hold.BookingHoldID);
                if (!availability.IsAvailable)
                {
                    transaction.Status = DepositStatusConstants.UnderReview;
                    transaction.PaidAt = DateTime.UtcNow;
                    webhookEvent.Status = "Processed";
                    webhookEvent.Error = availability.Reason ?? "Car is no longer available.";
                    webhookEvent.ProcessedAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync();
                    await dbTransaction.CommitAsync();
                    return new PayOSWebhookResult(true, true, "Payment requires manual review", null, transaction.Amount);
                }

                var pendingStatus = await _context.BookingStatuses
                    .FirstOrDefaultAsync(s => s.Name == BookingStatusConstants.Pending);
                if (pendingStatus == null)
                    throw new InvalidOperationException("Pending booking status is missing.");

                var booking = new Booking
                {
                    UserID = hold.UserID,
                    CarID = hold.CarID,
                    StartDate = hold.StartDate,
                    EndDate = hold.EndDate,
                    PickupLocationID = quote.PickupLocationID,
                    ReturnLocationID = quote.ReturnLocationID,
                    TotalPrice = quote.TotalPrice,
                    TotalHours = quote.TotalHours,
                    BookingStatusID = pendingStatus.BookingStatusID,
                    InsuranceTier = quote.InsuranceTier,
                    DepositAmount = quote.CollateralAmount,
                    ReservationFee = transaction.Amount,
                    DepositStatus = DepositStatusConstants.Holding,
                    PromotionID = quote.PromotionID,
                    ContractTypeID = quote.ContractTypeID,
                    DiscountAmount = quote.DiscountAmount,
                    DeliveryAddress = quote.DeliveryAddress,
                    DeliveryFee = quote.DeliveryFee,
                    AddOnFee = quote.AddOnFee
                };
                _context.Bookings.Add(booking);
                await _context.SaveChangesAsync();

                var payment = new Payment
                {
                    BookingID = booking.BookingID,
                    Amount = transaction.Amount,
                    PaymentMethod = DepositPaymentMethod,
                    PaymentStatus = PaymentStatusConstants.Paid,
                    PaymentDate = DateTime.UtcNow
                };
                _context.Payments.Add(payment);
                await _context.SaveChangesAsync();

                await AttachAddOnsAsync(booking.BookingID, quote);
                if (quote.DriverID.HasValue)
                    await AttachDriverAsync(booking, quote);

                if (quote.PromotionID.HasValue)
                {
                    await _context.Promotions
                        .Where(p => p.PromotionID == quote.PromotionID.Value && p.UsedCount < p.UsageLimit)
                        .ExecuteUpdateAsync(s => s.SetProperty(p => p.UsedCount, p => p.UsedCount + 1));
                }

                if (quote.ReferralDiscountAmount > 0)
                    await _referralService.MarkReferralDiscountUsedAsync(hold.UserID);

                if (quote.IsLongTerm)
                {
                    var longTermResult = await _longTermRentalService.CreatePostPaymentLongTermRecordsAsync(
                        booking.BookingID,
                        quote.CustomerName,
                        quote.CustomerPhone);
                    if (!longTermResult.IsSuccess)
                        throw new InvalidOperationException(longTermResult.ErrorMessage ?? "Long-term rental records could not be created.");
                }

                transaction.BookingID = booking.BookingID;
                transaction.PaymentID = payment.PaymentID;
                transaction.Status = PaymentStatusConstants.Paid;
                transaction.PaidAt = DateTime.UtcNow;

                hold.BookingID = booking.BookingID;
                hold.Status = BookingHoldStatusConstants.Converted;
                hold.ConvertedAt = DateTime.UtcNow;

                await _ledger.RecordAsync(new PaymentLedgerEntry
                {
                    BookingID = booking.BookingID,
                    PaymentID = payment.PaymentID,
                    Provider = "PayOS",
                    OrderCode = transaction.OrderCode,
                    EntryType = PaymentLedgerEntryTypeConstants.DepositCharge,
                    Amount = transaction.Amount,
                    Status = PaymentStatusConstants.Paid,
                    PostedAt = DateTime.UtcNow,
                    IdempotencyKey = $"payos-hold-paid:{transaction.OrderCode}",
                    Description = $"PayOS holding fee paid and booking #{booking.BookingID} created."
                });

                _context.AuditLogs.Add(new AuditLog
                {
                    Action = "Tạo đơn sau thanh toán",
                    EntityType = "Thanh toan",
                    EntityId = booking.BookingID,
                    UserId = hold.UserID,
                    UserName = quote.CustomerName,
                    Description = $"PayOS đã xác nhận {transaction.Amount:N0} VND. Hệ thống tạo đơn #{booking.BookingID} từ hold #{hold.BookingHoldID}.",
                    RequestUrl = "POST /Payment/Webhook",
                    Timestamp = DateTime.UtcNow
                });

                webhookEvent.Status = "Processed";
                webhookEvent.ProcessedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                await dbTransaction.CommitAsync();

                try
                {
                    var emailBooking = await _context.Bookings
                        .Include(b => b.User)
                        .Include(b => b.Car)
                        .FirstOrDefaultAsync(b => b.BookingID == booking.BookingID);
                    if (emailBooking != null)
                        await _emailService.SendBookingConfirmationEmailAsync(emailBooking, transaction.Amount);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to send booking confirmation email for booking {BookingId}.", booking.BookingID);
                }

                return new PayOSWebhookResult(true, true, "Payment confirmed - booking created", booking.BookingID, transaction.Amount);
            }
            catch (Exception ex)
            {
                await dbTransaction.RollbackAsync();
                webhookEvent.Status = "Failed";
                webhookEvent.Error = ex.Message;
                await _context.SaveChangesAsync();
                return new PayOSWebhookResult(false, true, ex.Message, null, transaction.Amount);
            }
        }

        private async Task<PayOSWebhookResult> ConfirmExistingBookingPaymentAsync(PayOSTransaction transaction, PaymentWebhookEvent webhookEvent)
        {
            if (transaction.Booking == null)
            {
                webhookEvent.Status = "Failed";
                webhookEvent.Error = "Booking not found.";
                await _context.SaveChangesAsync();
                return new PayOSWebhookResult(false, true, "Booking not found", null, transaction.Amount);
            }

            var bookingId = transaction.BookingID ?? transaction.Booking.BookingID;
            var payment = transaction.Payment ?? await _context.Payments.FirstOrDefaultAsync(p => p.BookingID == bookingId);
            if (payment == null)
            {
                payment = new Payment
                {
                    BookingID = bookingId,
                    Amount = transaction.Amount,
                    PaymentMethod = DepositPaymentMethod,
                    PaymentStatus = PaymentStatusConstants.Pending,
                    PaymentDate = DateTime.UtcNow
                };
                _context.Payments.Add(payment);
                await _context.SaveChangesAsync();
                transaction.PaymentID = payment.PaymentID;
            }

            payment.Amount = transaction.Amount;
            payment.PaymentStatus = PaymentStatusConstants.Paid;
            payment.PaymentDate = DateTime.UtcNow;
            transaction.Status = PaymentStatusConstants.Paid;
            transaction.PaidAt = DateTime.UtcNow;
            transaction.Booking.ReservationFee = transaction.Amount;
            transaction.Booking.DepositStatus = DepositStatusConstants.Holding;

            var pendingStatus = await _context.BookingStatuses
                .FirstOrDefaultAsync(s => s.Name == BookingStatusConstants.Pending);
            if (pendingStatus != null)
                transaction.Booking.BookingStatusID = pendingStatus.BookingStatusID;

            await _ledger.RecordAsync(new PaymentLedgerEntry
            {
                BookingID = transaction.BookingID,
                PaymentID = payment.PaymentID,
                Provider = "PayOS",
                OrderCode = transaction.OrderCode,
                EntryType = PaymentLedgerEntryTypeConstants.DepositCharge,
                Amount = transaction.Amount,
                Status = PaymentStatusConstants.Paid,
                PostedAt = DateTime.UtcNow,
                IdempotencyKey = $"payos-paid:{transaction.OrderCode}",
                Description = "PayOS reservation fee paid."
            });

            webhookEvent.Status = "Processed";
            webhookEvent.ProcessedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return new PayOSWebhookResult(true, true, "Payment confirmed", transaction.BookingID, transaction.Amount);
        }

        private async Task MarkTransactionFailedAsync(PayOSTransaction transaction, PaymentWebhookEvent webhookEvent)
        {
            transaction.Status = PaymentStatusConstants.Failed;
            transaction.FailedAt = DateTime.UtcNow;

            if (transaction.Payment != null && transaction.Payment.PaymentStatus != PaymentStatusConstants.Paid)
                transaction.Payment.PaymentStatus = PaymentStatusConstants.Failed;

            if (transaction.Booking != null)
                transaction.Booking.DepositStatus = DepositStatusConstants.Failed;

            if (transaction.BookingHoldID.HasValue)
                await _reservationService.ReleaseHoldAsync(transaction.BookingHoldID.Value, "PayOS payment failed.");

            webhookEvent.Status = "Processed";
            webhookEvent.ProcessedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }

        private async Task<PayOSWebhookResult> ConvertPaidCartToBookingsAsync(PayOSTransaction transaction, PaymentWebhookEvent webhookEvent)
        {
            var bookingTransaction = transaction.BookingTransaction ??
                await _context.BookingTransactions.FirstOrDefaultAsync(t => t.TransactionID == transaction.TransactionID);
            if (bookingTransaction == null)
            {
                webhookEvent.Status = "Failed";
                webhookEvent.Error = "Cart transaction not found.";
                await _context.SaveChangesAsync();
                return new PayOSWebhookResult(false, true, "Cart transaction not found", null, transaction.Amount);
            }

            if (transaction.Status == PaymentStatusConstants.Paid && bookingTransaction.Status == PaymentStatusConstants.Paid)
            {
                webhookEvent.Status = "Processed";
                webhookEvent.ProcessedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                return new PayOSWebhookResult(true, true, "Cart payment already processed", null, transaction.Amount);
            }

            var snapshot = DeserializeCartSnapshot(bookingTransaction.PendingCartSnapshot);
            if (snapshot?.Quotes == null || snapshot.Quotes.Count == 0)
            {
                webhookEvent.Status = "Failed";
                webhookEvent.Error = "Cart quote snapshot not found.";
                await _context.SaveChangesAsync();
                return new PayOSWebhookResult(false, true, "Cart quote snapshot not found", null, transaction.Amount);
            }

            var holds = await _context.BookingHolds
                .Where(h => h.TransactionID == bookingTransaction.TransactionID)
                .OrderBy(h => h.BookingHoldID)
                .ToListAsync();

            if (holds.Count != snapshot.Quotes.Count ||
                holds.Any(h => h.Status != BookingHoldStatusConstants.Active || h.ExpiresAt <= DateTime.UtcNow))
            {
                transaction.Status = DepositStatusConstants.UnderReview;
                transaction.PaidAt = DateTime.UtcNow;
                bookingTransaction.Status = DepositStatusConstants.UnderReview;
                webhookEvent.Status = "Processed";
                webhookEvent.Error = "Cart payment received after one or more holds were unavailable.";
                webhookEvent.ProcessedAt = DateTime.UtcNow;
                await _ledger.RecordAsync(new PaymentLedgerEntry
                {
                    TransactionID = bookingTransaction.TransactionID,
                    Provider = "PayOS",
                    OrderCode = transaction.OrderCode,
                    EntryType = PaymentLedgerEntryTypeConstants.DepositCharge,
                    Amount = transaction.Amount,
                    Status = DepositStatusConstants.UnderReview,
                    PostedAt = DateTime.UtcNow,
                    IdempotencyKey = $"payos-cart-under-review:{transaction.OrderCode}",
                    Description = $"Payment received for cart transaction #{bookingTransaction.TransactionID}, but holds require manual review."
                });
                await _context.SaveChangesAsync();
                return new PayOSWebhookResult(true, true, "Cart payment requires manual review", null, transaction.Amount);
            }

            await using var dbTransaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            try
            {
                var pendingStatus = await _context.BookingStatuses
                    .FirstOrDefaultAsync(s => s.Name == BookingStatusConstants.Pending);
                if (pendingStatus == null)
                    throw new InvalidOperationException("Pending booking status is missing.");

                var createdBookings = new List<Booking>();
                for (var i = 0; i < holds.Count; i++)
                {
                    var hold = holds[i];
                    var quote = snapshot.Quotes[i];
                    var availability = await _reservationService.CheckCarAvailabilityAsync(
                        hold.CarID,
                        hold.StartDate,
                        hold.EndDate,
                        ignoreHoldId: hold.BookingHoldID);
                    if (!availability.IsAvailable)
                        throw new InvalidOperationException(availability.Reason ?? "A held car is no longer available.");

                    var booking = new Booking
                    {
                        UserID = hold.UserID,
                        CarID = hold.CarID,
                        TransactionID = bookingTransaction.TransactionID,
                        StartDate = hold.StartDate,
                        EndDate = hold.EndDate,
                        PickupLocationID = quote.PickupLocationID,
                        ReturnLocationID = quote.ReturnLocationID,
                        TotalPrice = quote.TotalPrice,
                        TotalHours = quote.TotalHours,
                        BookingStatusID = pendingStatus.BookingStatusID,
                        InsuranceTier = quote.InsuranceTier,
                        DepositAmount = quote.CollateralAmount,
                        ReservationFee = quote.HoldingFee,
                        DepositStatus = DepositStatusConstants.Holding,
                        PromotionID = quote.PromotionID,
                        ContractTypeID = quote.ContractTypeID,
                        DiscountAmount = quote.DiscountAmount,
                        DeliveryAddress = quote.DeliveryAddress,
                        DeliveryFee = quote.DeliveryFee,
                        AddOnFee = quote.AddOnFee
                    };
                    _context.Bookings.Add(booking);
                    await _context.SaveChangesAsync();

                    var payment = new Payment
                    {
                        BookingID = booking.BookingID,
                        Amount = quote.HoldingFee,
                        PaymentMethod = DepositPaymentMethod,
                        PaymentStatus = PaymentStatusConstants.Paid,
                        PaymentDate = DateTime.UtcNow
                    };
                    _context.Payments.Add(payment);
                    await _context.SaveChangesAsync();

                    await AttachAddOnsAsync(booking.BookingID, quote);
                    if (quote.DriverID.HasValue)
                        await AttachDriverAsync(booking, quote);

                    if (quote.PromotionID.HasValue)
                    {
                        await _context.Promotions
                            .Where(p => p.PromotionID == quote.PromotionID.Value && p.UsedCount < p.UsageLimit)
                            .ExecuteUpdateAsync(s => s.SetProperty(p => p.UsedCount, p => p.UsedCount + 1));
                    }

                    if (quote.ReferralDiscountAmount > 0)
                        await _referralService.MarkReferralDiscountUsedAsync(hold.UserID);

                    if (quote.IsLongTerm)
                    {
                        var longTermResult = await _longTermRentalService.CreatePostPaymentLongTermRecordsAsync(
                            booking.BookingID,
                            quote.CustomerName,
                            quote.CustomerPhone);
                        if (!longTermResult.IsSuccess)
                            throw new InvalidOperationException(longTermResult.ErrorMessage ?? "Long-term rental records could not be created.");
                    }

                    hold.BookingID = booking.BookingID;
                    hold.Status = BookingHoldStatusConstants.Converted;
                    hold.ConvertedAt = DateTime.UtcNow;

                    await _ledger.RecordAsync(new PaymentLedgerEntry
                    {
                        BookingID = booking.BookingID,
                        PaymentID = payment.PaymentID,
                        TransactionID = bookingTransaction.TransactionID,
                        Provider = "PayOS",
                        OrderCode = transaction.OrderCode,
                        EntryType = PaymentLedgerEntryTypeConstants.DepositCharge,
                        Amount = quote.HoldingFee,
                        Status = PaymentStatusConstants.Paid,
                        PostedAt = DateTime.UtcNow,
                        IdempotencyKey = $"payos-cart-paid:{transaction.OrderCode}:{booking.BookingID}",
                        Description = $"PayOS cart payment created booking #{booking.BookingID} from hold #{hold.BookingHoldID}."
                    });

                    createdBookings.Add(booking);
                }

                transaction.Status = PaymentStatusConstants.Paid;
                transaction.PaidAt = DateTime.UtcNow;
                bookingTransaction.Status = PaymentStatusConstants.Paid;
                bookingTransaction.CompletedAt = DateTime.UtcNow;

                var userCartItems = await _context.CartItems
                    .Include(ci => ci.Cart)
                    .Where(ci => ci.Cart.UserID == bookingTransaction.UserID)
                    .ToListAsync();
                var cartItemsToRemove = userCartItems
                    .Where(ci => holds.Any(h => h.CarID == ci.CarID && h.StartDate == ci.StartDate && h.EndDate == ci.EndDate))
                    .ToList();
                _context.CartItems.RemoveRange(cartItemsToRemove);

                _context.AuditLogs.Add(new AuditLog
                {
                    Action = "Tạo đơn giỏ hàng sau thanh toán",
                    EntityType = "Thanh toan",
                    EntityId = bookingTransaction.TransactionID,
                    UserId = bookingTransaction.UserID,
                    Description = $"PayOS confirmed {transaction.Amount:N0} VND. Created {createdBookings.Count} bookings from cart transaction #{bookingTransaction.TransactionID}.",
                    RequestUrl = "POST /Payment/Webhook",
                    Timestamp = DateTime.UtcNow
                });

                webhookEvent.Status = "Processed";
                webhookEvent.ProcessedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                await dbTransaction.CommitAsync();

                return new PayOSWebhookResult(true, true, "Cart payment confirmed - bookings created", createdBookings.FirstOrDefault()?.BookingID, transaction.Amount);
            }
            catch (Exception ex)
            {
                await dbTransaction.RollbackAsync();
                transaction.Status = DepositStatusConstants.UnderReview;
                transaction.PaidAt = DateTime.UtcNow;
                bookingTransaction.Status = DepositStatusConstants.UnderReview;
                webhookEvent.Status = "Failed";
                webhookEvent.Error = ex.Message;
                await _context.SaveChangesAsync();
                return new PayOSWebhookResult(false, true, ex.Message, null, transaction.Amount);
            }
        }

        private async Task AttachAddOnsAsync(int bookingId, CheckoutQuoteViewModel quote)
        {
            foreach (var line in quote.AddOns.Where(a => a.TotalPrice > 0))
            {
                var addOn = await _context.AddOns.FirstOrDefaultAsync(a => a.Name == line.Name);
                if (addOn == null)
                {
                    addOn = new AddOn
                    {
                        Name = line.Name,
                        PricePerDay = line.UnitPrice,
                        IsActive = true
                    };
                    _context.AddOns.Add(addOn);
                    await _context.SaveChangesAsync();
                }

                _context.BookingAddOns.Add(new BookingAddOn
                {
                    BookingID = bookingId,
                    AddOnID = addOn.AddOnID,
                    Quantity = line.Quantity,
                    TotalPrice = line.TotalPrice
                });
            }
        }

        private async Task AttachDriverAsync(Booking booking, CheckoutQuoteViewModel quote)
        {
            var driverBooking = new DriverBooking
            {
                BookingID = booking.BookingID,
                DriverID = quote.DriverID!.Value,
                DriverFee = quote.DriverFee,
                Status = BookingStatusConstants.Pending
            };
            _context.DriverBookings.Add(driverBooking);
            await _context.SaveChangesAsync();

            booking.DriverBookingID = driverBooking.DriverBookingID;
        }

        private static CheckoutQuoteViewModel? DeserializeQuote(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            return JsonSerializer.Deserialize<CheckoutQuoteViewModel>(json, JsonOptions);
        }

        private static CartCheckoutSnapshot? DeserializeCartSnapshot(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            return JsonSerializer.Deserialize<CartCheckoutSnapshot>(json, JsonOptions);
        }

        private sealed class CartCheckoutSnapshot
        {
            public List<CheckoutQuoteViewModel> Quotes { get; set; } = new();
            public decimal TotalAmount { get; set; }
            public decimal HoldingFee { get; set; }
            public decimal TotalDeposit { get; set; }
        }

        private static void ApplyPayOSResponse(PayOSTransaction transaction, CreatePaymentLinkResponse response)
        {
            transaction.CheckoutUrl = response.CheckoutUrl;
            transaction.QrCode = response.QrCode;
            transaction.Bin = response.Bin;
            transaction.AccountNumber = response.AccountNumber;
            transaction.AccountName = response.AccountName;
            transaction.PaymentLinkId = response.PaymentLinkId;
            transaction.Currency = string.IsNullOrWhiteSpace(response.Currency) ? "VND" : response.Currency;
            transaction.Status = PaymentStatusConstants.Pending;
        }

        private static decimal ResolveReservationFee(Booking booking)
        {
            if (booking.ReservationFee > 0) return booking.ReservationFee;
            return Math.Round(booking.TotalPrice * 0.2m, 0, MidpointRounding.AwayFromZero);
        }

        private async Task MarkCreatePaymentFailedAsync(PayOSTransaction transaction, Exception exception, string logMessage, int entityId)
        {
            _logger.LogError(exception, logMessage, entityId);

            try
            {
                var entry = _context.Entry(transaction);
                if (entry.State == EntityState.Added)
                {
                    entry.State = EntityState.Detached;
                    return;
                }

                transaction.Status = PaymentStatusConstants.Failed;
                transaction.FailedAt = DateTime.UtcNow;
                transaction.PayOSDescription = exception.GetBaseException().Message;
                await _context.SaveChangesAsync();
            }
            catch (Exception saveException)
            {
                _logger.LogError(saveException, "Failed to mark PayOS create failure for order {OrderCode}.", transaction.OrderCode);
            }
        }

        private async Task<long> GenerateUniqueOrderCodeAsync(int seed)
        {
            for (var i = 0; i < 10; i++)
            {
                var candidate = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 1000 + (seed % 1000) + i;
                if (!await _context.PayOSTransactions.AnyAsync(t => t.OrderCode == candidate))
                    return candidate;
            }

            throw new InvalidOperationException("Could not generate a unique PayOS order code.");
        }

        private static string ComputeSha256(string value)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
            return Convert.ToHexString(bytes).ToLowerInvariant();
        }
    }
}
