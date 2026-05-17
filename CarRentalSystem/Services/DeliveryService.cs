using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace CarRentalSystem.Services
{
    public class DeliveryInfo
    {
        public double DistanceKm { get; set; }
        public double DurationMinutes { get; set; }
        public decimal ShippingFee { get; set; }
        public DateTime EarliestPickupTime { get; set; }
        public string FormattedDistance { get; set; } = "";
        public string FormattedDuration { get; set; } = "";
        public string FormattedFee { get; set; } = "";
    }

    public interface IDeliveryService
    {
        Task<DeliveryInfo> CalculateDeliveryAsync(double carLat, double carLng, double customerLat, double customerLng);
        double CalculateHaversineDistance(double lat1, double lon1, double lat2, double lon2);
    }

    public class DeliveryService : IDeliveryService
    {
        private readonly HttpClient _httpClient;
        private const decimal FeePerKm = 3000m;
        private const double PrepTimeHours = 1.0;
        private const double AverageSpeedKmh = 30.0;

        public DeliveryService(HttpClient httpClient)
        {
            _httpClient = httpClient;
            _httpClient.Timeout = TimeSpan.FromSeconds(10);
        }

        public async Task<DeliveryInfo> CalculateDeliveryAsync(double carLat, double carLng, double customerLat, double customerLng)
        {
            var result = new DeliveryInfo
            {
                EarliestPickupTime = DateTime.UtcNow.AddHours(PrepTimeHours)
            };

            try
            {
                // Sử dụng OSRM API để lấy khoảng cách và thời gian thực
                var url = $"https://router.project-osrm.org/route/v1/driving/{carLng},{carLat};{customerLng},{customerLat}?overview=false";
                
                var response = await _httpClient.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;

                    if (root.TryGetProperty("routes", out var routes) && routes.GetArrayLength() > 0)
                    {
                        var route = routes[0];
                        result.DistanceKm = route.GetProperty("distance").GetDouble() / 1000.0;
                        result.DurationMinutes = route.GetProperty("duration").GetDouble() / 60.0;
                    }
                }
            }
            catch (Exception)
            {
                // Fallback: tính khoảng cách đường chim bay
                result.DistanceKm = CalculateHaversineDistance(carLat, carLng, customerLat, customerLng);
                result.DurationMinutes = result.DistanceKm / AverageSpeedKmh * 60.0;
            }

            // Tính thời gian giao xe tối thiểu (thời gian chạy + 1h chuẩn bị)
            if (result.DistanceKm <= 0)
            {
                result.DistanceKm = CalculateHaversineDistance(carLat, carLng, customerLat, customerLng);
                result.DurationMinutes = result.DistanceKm / AverageSpeedKmh * 60.0;
            }

            var totalHoursNeeded = PrepTimeHours + (result.DurationMinutes / 60.0);
            result.EarliestPickupTime = DateTime.UtcNow.AddHours(totalHoursNeeded);

            // Tính phí ship: 3k/km, làm tròn lên
            result.ShippingFee = (decimal)Math.Ceiling(result.DistanceKm) * FeePerKm;

            // Format hiển thị
            result.FormattedDistance = $"{result.DistanceKm:F1} km";
            result.FormattedDuration = totalHoursNeeded >= 1 
                ? $"~{Math.Ceiling(totalHoursNeeded)} tiếng"
                : $"~{Math.Ceiling(result.DurationMinutes)} phút";
            result.FormattedFee = $"{result.ShippingFee:N0} đ";

            return result;
        }

        public double CalculateHaversineDistance(double lat1, double lon1, double lat2, double lon2)
        {
            const double R = 6371; // Bán kính Trái Đất (km)
            var dLat = ToRadians(lat2 - lat1);
            var dLon = ToRadians(lon2 - lon1);
            
            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                    Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                    Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            
            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return R * c;
        }

        private static double ToRadians(double degrees) => degrees * Math.PI / 180.0;
    }
}
