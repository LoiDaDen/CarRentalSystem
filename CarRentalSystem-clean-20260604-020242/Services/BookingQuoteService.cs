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
        public string DeliveryMode { get; set; } = DeliveryModeConstants.CustomerPickup;
        public double? DeliveryLatitude { get; set; }
        public double? DeliveryLongitude { get; set; }
        public decimal DeliveryDistanceKm { get; set; }
        public int? DeliveryEtaMinutes { get; set; }
        public DateTime? DeliveryQuotedAt { get; set; }
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
        private readonly ISelfDrivePricingService _selfDrivePricingService;
        private readonly IBookingBusinessRuleService _businessRules;
        private readonly IHolidayPricingService _holidayPricingService;

        public BookingQuoteService(
            ApplicationDbContext context,
            IReferralService referralService,
            IReservationService reservationService,
            ISelfDrivePricingService selfDrivePricingService,
            IBookingBusinessRuleService businessRules,
            IHolidayPricingService holidayPricingService)
        {
            _context = context;
            _referralService = referralService;
            _reservationService = reservationService;
            _selfDrivePricingService = selfDrivePricingService;
            _businessRules = businessRules;
            _holidayPricingService = holidayPricingService;
        }

        public async Task<CheckoutQuoteViewModel> BuildQuoteAsync(BookingQuoteRequest request)
        {
            if (request.EndDate <= request.StartDate)
                throw new InvalidOperationException("End date must be after start date.");

            if (request.SelectedDriverID.HasValue)
                throw new InvalidOperationException("Đơn thuê xe tự lái không được gán tài xế.");

            var car = await _context.Cars
                .Include(c => c.CarBrand)
                .Include(c => c.Location)
                .Include(c => c.ServiceType)
                .FirstOrDefaultAsync(c => c.CarID == request.CarID);
            if (car == null)
                throw new InvalidOperationException("Car not found.");

            var selfDriveServiceTypeId = await _businessRules.GetServiceTypeIdAsync(ServiceTypeConstants.SelfDrive);
            if (car.ServiceTypeID.HasValue && car.ServiceTypeID != selfDriveServiceTypeId)
                throw new InvalidOperationException("Xe này không thuộc luồng thuê xe tự lái.");

            if (!car.IsSelfDriveAllowed || car.IsWithDriverAllowed)
                throw new InvalidOperationException("Xe tự lái không được cấu hình kèm tài xế.");

            var user = await _context.Users
                .Include(u => u.MembershipTier)
                .FirstOrDefaultAsync(u => u.UserID == request.UserID);
            if (user == null)
                throw new InvalidOperationException("User not found.");

            var drivingLicense = await _businessRules.GetValidCustomerDrivingLicenseAsync(user.UserID);
            if (drivingLicense == null)
                throw new InvalidOperationException("Khách thuê xe tự lái phải cung cấp giấy phép lái xe hợp lệ.");

            var selfDrivePrice = _selfDrivePricingService.Calculate(car, request.StartDate, request.EndDate);
            var totalHours = selfDrivePrice.TotalHours;
            var rentalDays = selfDrivePrice.RentalDays;
            var holidayMultiplier = _holidayPricingService.GetMultiplier(request.StartDate, request.EndDate);
            var baseRental = Math.Round(selfDrivePrice.BaseRentalAmount * holidayMultiplier, 0, MidpointRounding.AwayFromZero);
            var insuranceTier = NormalizeInsuranceTier(request.InsuranceTier);
            var insuranceFee = GetDailyInsuranceFee(insuranceTier) * rentalDays;

            var addOns = BuildAddOnLines(request.SelectedAddons, rentalDays);
            var addOnFee = addOns.Sum(a => a.TotalPrice);

            decimal driverFee = 0m;
            int? driverId = null;
            string? driverName = null;

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
            int? appliedPromotionId = null;
            if (request.PromotionID.HasValue)
            {
                var promo = await _context.Promotions
                    .AsNoTracking()
                    .FirstOrDefaultAsync(p => p.PromotionID == request.PromotionID.Value);
                if (promo != null &&
                    promo.IsActive &&
                    promo.ValidFrom <= DateTime.UtcNow &&
                    promo.ValidUntil >= DateTime.UtcNow &&
                    promo.UsedCount < promo.UsageLimit &&
                    !await HasUserAlreadyUsedPromotionAsync(user.UserID, promo.PromotionID))
                {
                    var afterMembership = Math.Max(0m, totalBeforeDiscount - discountTotal);
                    promotionDiscount = afterMembership * promo.DiscountPercent / 100m;
                    if (promo.MaxDiscountAmount.HasValue && promotionDiscount > promo.MaxDiscountAmount.Value)
                        promotionDiscount = promo.MaxDiscountAmount.Value;
                    discountTotal += promotionDiscount;
                    appliedPromotionId = promo.PromotionID;
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
                ServiceTypeID = selfDriveServiceTypeId,
                ServiceTypeCode = ServiceTypeConstants.SelfDrive,
                CustomerDrivingLicenseID = drivingLicense.CustomerDrivingLicenseID,
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
                DeliveryMode = request.DeliveryMode,
                DeliveryLatitude = request.DeliveryLatitude,
                DeliveryLongitude = request.DeliveryLongitude,
                DeliveryDistanceKm = request.DeliveryDistanceKm,
                DeliveryEtaMinutes = request.DeliveryEtaMinutes,
                DeliveryQuotedAt = request.DeliveryQuotedAt,
                InsuranceFee = insuranceFee,
                InsuranceTier = insuranceTier,
                ContractTypeID = request.ContractTypeID,
                IsLongTerm = request.IsLongTerm,
                LongTermMonths = request.LongTermMonths,
                AddOnFee = addOnFee,
                DriverFee = driverFee,
                DriverID = driverId,
                DriverName = driverName,
                PromotionID = appliedPromotionId,
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
            quote.Items.Add(new CheckoutQuoteLineItem { Category = "Service", Name = "Phí dịch vụ", Quantity = 1, UnitPrice = ServiceFee, TotalPrice = ServiceFee });
            if (request.DeliveryFee > 0)
                quote.Items.Add(new CheckoutQuoteLineItem { Category = "Delivery", Name = "Giao xe tận nơi", Quantity = 1, UnitPrice = request.DeliveryFee, TotalPrice = request.DeliveryFee });
            if (insuranceFee > 0)
                quote.Items.Add(new CheckoutQuoteLineItem { Category = "Insurance", Name = $"Gói bảo vệ {insuranceTier}", Quantity = rentalDays, UnitPrice = GetDailyInsuranceFee(insuranceTier), TotalPrice = insuranceFee });
            quote.Items.AddRange(addOns);
            return quote;
        }

        private static List<CheckoutQuoteLineItem> BuildAddOnLines(List<string>? selectedAddons, int rentalDays)
        {
            var lines = new List<CheckoutQuoteLineItem>();
            if (selectedAddons == null) return lines;

            foreach (var raw in selectedAddons.Where(a => !string.IsNullOrWhiteSpace(a)).Distinct())
            {
                var parts = raw.Split('|', StringSplitOptions.TrimEntries);
                if (parts.Length < 2)
                    continue;

                int? addOnId = null;
                string name;
                decimal dailyPrice;
                if (parts.Length >= 3 && int.TryParse(parts[0], out var parsedId) && decimal.TryParse(parts[2], out dailyPrice))
                {
                    addOnId = parsedId;
                    name = parts[1].Trim();
                }
                else if (decimal.TryParse(parts[1], out dailyPrice))
                {
                    name = parts[0].Trim();
                }
                else
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(name) || dailyPrice < 0) continue;

                lines.Add(new CheckoutQuoteLineItem
                {
                    Category = "AddOn",
                    Name = name,
                    AddOnID = addOnId,
                    Quantity = rentalDays,
                    UnitPrice = dailyPrice,
                    TotalPrice = dailyPrice * rentalDays
                });
            }

            return lines;
        }

        private async Task<bool> HasUserAlreadyUsedPromotionAsync(int userId, int promotionId)
        {
            var countedStatuses = new[]
            {
                BookingStatusConstants.Unpaid,
                BookingStatusConstants.Pending,
                BookingStatusConstants.Confirmed,
                BookingStatusConstants.Active,
                BookingStatusConstants.Completed
            };

            return await _context.Bookings
                .AsNoTracking()
                .AnyAsync(b =>
                    b.UserID == userId &&
                    b.PromotionID == promotionId &&
                    b.BookingStatus != null &&
                    countedStatuses.Contains(b.BookingStatus.Name));
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
