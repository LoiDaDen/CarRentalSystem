using CarRentalSystem.Models;
using CarRentalSystem.ViewModels;

namespace CarRentalSystem.Services
{
    public interface IBookingFactory
    {
        Booking CreateFromPaidHold(
            BookingHold hold,
            CheckoutQuoteViewModel quote,
            int pendingStatusId,
            int? transactionId = null,
            decimal? reservationFeeOverride = null,
            int? serviceTypeIdOverride = null);
    }

    public sealed class BookingFactory : IBookingFactory
    {
        public Booking CreateFromPaidHold(
            BookingHold hold,
            CheckoutQuoteViewModel quote,
            int pendingStatusId,
            int? transactionId = null,
            decimal? reservationFeeOverride = null,
            int? serviceTypeIdOverride = null)
        {
            ArgumentNullException.ThrowIfNull(hold);
            ArgumentNullException.ThrowIfNull(quote);

            var paidDeposit = reservationFeeOverride ?? quote.HoldingFee;

            return new Booking
            {
                UserID = hold.UserID,
                CarID = hold.CarID,
                ServiceTypeID = quote.ServiceTypeID ?? serviceTypeIdOverride,
                CustomerDrivingLicenseID = quote.CustomerDrivingLicenseID,
                ContractTypeCode = "SELF_DRIVE_CONTRACT",
                TransactionID = transactionId,
                StartDate = hold.StartDate,
                EndDate = hold.EndDate,
                PickupLocationID = quote.PickupLocationID,
                ReturnLocationID = quote.ReturnLocationID,
                TotalPrice = quote.TotalPrice,
                TotalHours = quote.TotalHours,
                BookingStatusID = pendingStatusId,
                InsuranceTier = quote.InsuranceTier,
                DepositAmount = paidDeposit,
                ReservationFee = paidDeposit,
                DepositStatus = DepositStatusConstants.Holding,
                PromotionID = quote.PromotionID,
                ContractTypeID = quote.ContractTypeID,
                DiscountAmount = quote.DiscountAmount,
                DeliveryAddress = quote.DeliveryAddress,
                DeliveryMode = quote.DeliveryMode,
                DeliveryLatitude = quote.DeliveryLatitude,
                DeliveryLongitude = quote.DeliveryLongitude,
                DeliveryDistanceKm = quote.DeliveryDistanceKm,
                DeliveryEtaMinutes = quote.DeliveryEtaMinutes,
                DeliveryQuotedAt = quote.DeliveryQuotedAt,
                DeliveryFee = quote.DeliveryFee,
                AddOnFee = quote.AddOnFee,
                TermsDocumentVersionID = quote.TermsDocumentVersionID,
                PrivacyNoticeVersionID = quote.PrivacyNoticeVersionID,
                SelfDriveAgreementAcceptedAt = quote.SelfDriveAgreementAcceptedAt,
                SelfDriveAgreementIpAddress = quote.SelfDriveAgreementIpAddress,
                SelfDriveAgreementUserAgent = quote.SelfDriveAgreementUserAgent
            };
        }
    }
}
