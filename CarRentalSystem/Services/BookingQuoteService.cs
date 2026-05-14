using CarRentalSystem.Data;
using CarRentalSystem.Models;
using CarRentalSystem.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace CarRentalSystem.Services
{
    public class BookingQuoteRequest
    {
        public int UserID { get; set; }
        public int CarID { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string InsuranceTier { get; set; } = "Basic";
        public decimal DeliveryFee { get; set; }
        public string? DeliveryAddress { get; set; }
        public List<string>? SelectedAddons { get; set; }
        public int? SelectedDriverID { get; set; }
        public int? PromotionID { get; set; }
        public int? ContractTypeID { get; set; }
        public bool IsLongTerm { get; set; }
        public int? LongTermMonths { get; set; }
    }

    public interface IBookingQuoteService
    {
        Task<CheckoutQuoteViewModel> BuildQuoteAsync(BookingQuoteRequest request);
    }

    public class BookingQuoteService : IBookingQuoteService
    {
        private const decimal ServiceFee = 150000m;
        private readonly ApplicationDbContext _context;
        private readonly IReferralService _referralService;
        private readonly IReservationService _reservationService;

        public BookingQuoteService(
            ApplicationDbContext context,
            IReferralService referralService,
            IReservationService reservationService)
        {
            _context = context;
            _referralService = referralService;
            _reservationService = reservationService;
        }

        public async Task<CheckoutQuoteViewModel> BuildQuoteAsync(BookingQuoteRequest request)
        {
            if (request.EndDate <= request.StartDate)
                throw new InvalidOperationException("End date must be after start date.");

            var car = await _context.Cars
                .Include(c => c.CarBrand)
                .Include(c => c.Location)
                .FirstOrDefaultAsync(c => c.CarID == request.CarID);
            if (car == null)
                throw new InvalidOperationException("Car not found.");

            var user = await _context.Users
                .Include(u => u.MembershipTier)
                .FirstOrDefaultAsync(u => u.UserID == request.UserID);
            if (user == null)
                throw new InvalidOperationException("User not found.");

            var totalHours = (int)Math.Ceiling((request.EndDate - request.StartDate).TotalHours);
            if (totalHours <= 0) totalHours = 1;
            var rentalDays = Math.Max(1, (int)Math.Ceiling(totalHours / 24.0));

            var baseRental = CalculateRental(car, totalHours);
            var insuranceTier = NormalizeInsuranceTier(request.InsuranceTier);
            var insuranceFee = GetDailyInsuranceFee(insuranceTier) * rentalDays;

            var addOns = BuildAddOnLines(request.SelectedAddons, rentalDays);
            var addOnFee = addOns.Sum(a => a.TotalPrice);

            decimal driverFee = 0m;
            int? driverId = null;
            string? driverName = null;
            if (request.SelectedDriverID.HasValue && totalHours > 4)
            {
                var driver = await _context.Drivers
                    .FirstOrDefaultAsync(d => d.DriverID == request.SelectedDriverID.Value && d.IsApproved && d.IsAvailable);
                if (driver != null)
                {
                    var available = await _reservationService.IsDriverAvailableAsync(driver.DriverID, request.StartDate, request.EndDate);
                    if (!available)
                        throw new InvalidOperationException("Driver is not available for the selected time.");

                    driverId = driver.DriverID;
                    driverName = driver.FullName;
                    driverFee = driver.DailyRate * rentalDays;
                }
            }

            var totalBeforeDiscount = baseRental + ServiceFee + request.DeliveryFee + insuranceFee + addOnFee + driverFee;
            var discountTotal = 0m;

            var contractDiscount = 0m;
            if (request.ContractTypeID.HasValue)
            {
                var contractType = await _context.ContractTypes
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c => c.ContractTypeID == request.ContractTypeID.Value && c.IsActive);
                if (contractType?.DiscountPercent > 0)
                {
                    var contractDiscountBase = baseRental + driverFee;
                    contractDiscount = contractDiscountBase * (contractType.DiscountPercent / 100m);
                    discountTotal += contractDiscount;
                }
            }

            var membershipDiscount = 0m;
            if (user.MembershipTier?.DiscountPercent > 0)
            {
                membershipDiscount = totalBeforeDiscount * (user.MembershipTier.DiscountPercent / 100m);
                discountTotal += membershipDiscount;
            }

            var promotionDiscount = 0m;
            if (request.PromotionID.HasValue)
            {
                var promo = await _context.Promotions
                    .AsNoTracking()
                    .FirstOrDefaultAsync(p => p.PromotionID == request.PromotionID.Value);
                if (promo != null &&
                    promo.IsActive &&
                    promo.ValidFrom <= DateTime.UtcNow &&
                    promo.ValidUntil >= DateTime.UtcNow &&
                    promo.UsedCount < promo.UsageLimit)
                {
                    var afterMembership = Math.Max(0m, totalBeforeDiscount - discountTotal);
                    promotionDiscount = afterMembership * promo.DiscountPercent / 100m;
                    if (promo.MaxDiscountAmount.HasValue && promotionDiscount > promo.MaxDiscountAmount.Value)
                        promotionDiscount = promo.MaxDiscountAmount.Value;
                    discountTotal += promotionDiscount;
                }
            }

            var referralDiscount = await _referralService.GetFirstRentalDiscountAsync(user.UserID);
            if (referralDiscount > 0)
            {
                referralDiscount = Math.Min(referralDiscount, Math.Max(0m, totalBeforeDiscount - discountTotal));
                discountTotal += referralDiscount;
            }

            var totalPrice = Math.Max(0m, totalBeforeDiscount - discountTotal);
            var holdingFee = Math.Round(totalPrice * 0.2m, 0, MidpointRounding.AwayFromZero);

            var quote = new CheckoutQuoteViewModel
            {
                UserID = user.UserID,
                CarID = car.CarID,
                CarName = car.CarName ?? string.Empty,
                CarBrand = car.CarBrand?.Name ?? string.Empty,
                LicensePlate = car.LicensePlate ?? string.Empty,
                CustomerName = user.Name,
                CustomerEmail = user.Email,
                CustomerPhone = user.Phone,
                StartDate = request.StartDate,
                EndDate = request.EndDate,
                TotalHours = totalHours,
                RentalDays = rentalDays,
                PickupLocationID = car.LocationID,
                ReturnLocationID = car.LocationID,
                PickupLocationName = car.Location?.Name,
                BaseRentalAmount = baseRental,
                ServiceFee = ServiceFee,
                DeliveryFee = request.DeliveryFee,
                DeliveryAddress = request.DeliveryAddress,
                InsuranceFee = insuranceFee,
                InsuranceTier = insuranceTier,
                ContractTypeID = request.ContractTypeID,
                IsLongTerm = request.IsLongTerm,
                LongTermMonths = request.LongTermMonths,
                AddOnFee = addOnFee,
                DriverFee = driverFee,
                DriverID = driverId,
                DriverName = driverName,
                PromotionID = request.PromotionID,
                MembershipDiscountAmount = membershipDiscount,
                PromotionDiscountAmount = promotionDiscount,
                ReferralDiscountAmount = referralDiscount,
                DiscountAmount = discountTotal,
                TotalBeforeDiscount = totalBeforeDiscount,
                TotalPrice = totalPrice,
                HoldingFee = holdingFee,
                RemainingRentalAmount = Math.Max(0m, totalPrice - holdingFee),
                CollateralAmount = car.DefaultDeposit,
                AddOns = addOns
            };

            quote.Items.Add(new CheckoutQuoteLineItem { Category = "Rental", Name = "Tiền thuê xe", Quantity = 1, UnitPrice = baseRental, TotalPrice = baseRental });
            quote.Items.Add(new CheckoutQuoteLineItem { Category = "Service", Name = "Phi dich vu", Quantity = 1, UnitPrice = ServiceFee, TotalPrice = ServiceFee });
            if (request.DeliveryFee > 0)
                quote.Items.Add(new CheckoutQuoteLineItem { Category = "Delivery", Name = "Giao xe tan noi", Quantity = 1, UnitPrice = request.DeliveryFee, TotalPrice = request.DeliveryFee });
            if (insuranceFee > 0)
                quote.Items.Add(new CheckoutQuoteLineItem { Category = "Insurance", Name = $"Goi bao ve {insuranceTier}", Quantity = rentalDays, UnitPrice = GetDailyInsuranceFee(insuranceTier), TotalPrice = insuranceFee });
            quote.Items.AddRange(addOns);
            if (driverFee > 0)
                quote.Items.Add(new CheckoutQuoteLineItem { Category = "Driver", Name = driverName ?? "Tai xe", Quantity = rentalDays, UnitPrice = driverFee / rentalDays, TotalPrice = driverFee });

            return quote;
        }

        private static decimal CalculateRental(Car car, int hours)
        {
            var pricePerHour = car.PricePerHour > 0 ? car.PricePerHour : Math.Round(car.PricePerDay / 8m);
            var p4 = car.PricePerDay > 0 ? car.PricePerDay * 0.4m : 4 * pricePerHour;
            var p8 = car.PricePerDay > 0 ? car.PricePerDay * 0.7m : 8 * pricePerHour;
            var p12 = car.PricePerDay > 0 ? car.PricePerDay * 0.85m : 12 * pricePerHour;
            var p24 = car.PricePerDay > 0 ? car.PricePerDay : 24 * pricePerHour;

            if (hours <= 0) return 0m;
            if (hours <= 4) return Math.Min(hours * pricePerHour, p4);
            if (hours <= 8) return Math.Min(p4 + (hours - 4) * pricePerHour, p8);
            if (hours <= 12) return Math.Min(p8 + (hours - 8) * pricePerHour, p12);
            if (hours <= 24) return Math.Min(p12 + (hours - 12) * pricePerHour, p24);

            var fullDays = hours / 24;
            var rem = hours % 24;
            var remPrice = rem switch
            {
                <= 0 => 0m,
                <= 4 => Math.Min(rem * pricePerHour, p4),
                <= 8 => Math.Min(p4 + (rem - 4) * pricePerHour, p8),
                <= 12 => Math.Min(p8 + (rem - 8) * pricePerHour, p12),
                _ => Math.Min(p12 + (rem - 12) * pricePerHour, p24)
            };

            return (fullDays * p24) + remPrice;
        }

        private static List<CheckoutQuoteLineItem> BuildAddOnLines(List<string>? selectedAddons, int rentalDays)
        {
            var lines = new List<CheckoutQuoteLineItem>();
            if (selectedAddons == null) return lines;

            foreach (var raw in selectedAddons.Where(a => !string.IsNullOrWhiteSpace(a)).Distinct())
            {
                var parts = raw.Split('|', 2);
                if (parts.Length != 2 || !decimal.TryParse(parts[1], out var dailyPrice))
                    continue;

                var name = parts[0].Trim();
                if (string.IsNullOrWhiteSpace(name) || dailyPrice < 0) continue;

                lines.Add(new CheckoutQuoteLineItem
                {
                    Category = "AddOn",
                    Name = name,
                    Quantity = rentalDays,
                    UnitPrice = dailyPrice,
                    TotalPrice = dailyPrice * rentalDays
                });
            }

            return lines;
        }

        private static string NormalizeInsuranceTier(string? tier)
        {
            return tier switch
            {
                "Premium" => "Premium",
                "Full Cover" => "Full Cover",
                _ => "Basic"
            };
        }

        private static decimal GetDailyInsuranceFee(string tier)
        {
            return tier switch
            {
                "Premium" => 150000m,
                "Full Cover" => 300000m,
                _ => 0m
            };
        }
    }
}
