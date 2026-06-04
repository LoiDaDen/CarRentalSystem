using CarRentalSystem.Models;

namespace CarRentalSystem.Services
{
    public record WithDriverPricingRequest(
        string TripTypeCode,
        decimal RouteBasePrice = 0,
        decimal EstimatedDistanceKm = 0,
        decimal ExtraKmPrice = 0,
        decimal WaitingFee = 0,
        decimal TollFee = 0,
        decimal OvernightFee = 0,
        decimal ExtraKmFee = 0,
        decimal ExtraHourFee = 0,
        decimal SurchargeFee = 0,
        decimal DiscountAmount = 0);

    public record WithDriverPriceResult(decimal TotalAmount);

    public interface IWithDriverPricingService
    {
        WithDriverPriceResult Calculate(WithDriverPricingRequest request);
    }

    public class WithDriverPricingService : IWithDriverPricingService
    {
        public WithDriverPriceResult Calculate(WithDriverPricingRequest request)
        {
            var total = request.TripTypeCode switch
            {
                TripTypeConstants.CityTransfer => request.RouteBasePrice + (request.EstimatedDistanceKm * request.ExtraKmPrice) + request.WaitingFee + request.SurchargeFee,
                TripTypeConstants.AirportTransfer => request.RouteBasePrice + request.WaitingFee + request.SurchargeFee,
                TripTypeConstants.IntercityOneWay => request.RouteBasePrice + request.TollFee + request.SurchargeFee,
                TripTypeConstants.IntercityRoundTrip => request.RouteBasePrice + request.OvernightFee + request.WaitingFee + request.TollFee + request.ExtraKmFee + request.SurchargeFee,
                TripTypeConstants.HourlyRental => request.RouteBasePrice + request.ExtraHourFee + request.ExtraKmFee + request.SurchargeFee,
                TripTypeConstants.DailyRental => request.RouteBasePrice + request.OvernightFee + request.ExtraKmFee + request.SurchargeFee,
                _ => throw new InvalidOperationException("Loại chuyến xe có tài xế không hợp lệ.")
            };

            return new WithDriverPriceResult(Math.Max(0m, total - request.DiscountAmount));
        }
    }
}
