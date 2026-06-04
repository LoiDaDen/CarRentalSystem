using System.Globalization;
using System.Text;

namespace CarRentalSystem.Services
{
    public class DeliveryScopeResult
    {
        public bool IsWithinScope { get; set; }
        public string? OriginProvince { get; set; }
        public string? DestinationProvince { get; set; }
        public string MessageVi { get; set; } = "";
        public string MessageEn { get; set; } = "";
    }

    public interface IDeliveryScopeService
    {
        DeliveryScopeResult CheckScope(string? originText, double originLat, double originLng, double destinationLat, double destinationLng);
        DeliveryScopeResult CheckScope(string? originText, double originLat, double originLng, string? destinationText);
        string? ResolveProvince(string? text, double? lat = null, double? lng = null);
    }

    public class DeliveryScopeService : IDeliveryScopeService
    {
        private const string OutOfScopeVi = "Địa điểm giao xe nằm ngoài phạm vi hỗ trợ. Vui lòng chọn địa điểm khác hoặc nhận xe tại vị trí xe.";
        private const string OutOfScopeEn = "This address is outside our delivery range.";

        private static readonly Dictionary<string, string> Aliases = new(StringComparer.OrdinalIgnoreCase)
        {
            ["tp ho chi minh"] = "ho chi minh",
            ["hcm"] = "ho chi minh",
            ["tphcm"] = "ho chi minh",
            ["sai gon"] = "ho chi minh",
            ["ha noi"] = "ha noi",
            ["hanoi"] = "ha noi",
            ["da nang"] = "da nang",
            ["can tho"] = "can tho",
            ["binh duong"] = "binh duong",
            ["dong nai"] = "dong nai",
            ["bien hoa"] = "dong nai",
            ["tran bien"] = "dong nai",
            ["an binh"] = "dong nai",
            ["tam hiep"] = "dong nai",
            ["nha trang"] = "khanh hoa",
            ["khanh hoa"] = "khanh hoa",
            ["hai phong"] = "hai phong",
            ["ba ria vung tau"] = "ba ria vung tau",
            ["vung tau"] = "ba ria vung tau",
            ["ca mau"] = "ca mau",
            ["lam dong"] = "lam dong",
            ["da lat"] = "lam dong",
            ["phu quoc"] = "kien giang",
            ["kien giang"] = "kien giang"
        };

        private static readonly Dictionary<string, (double Lat, double Lng, string Display)> Centers = new(StringComparer.OrdinalIgnoreCase)
        {
            ["ho chi minh"] = (10.7769, 106.7009, "TP. Hồ Chí Minh"),
            ["ha noi"] = (21.0285, 105.8542, "Hà Nội"),
            ["da nang"] = (16.0544, 108.2022, "Đà Nẵng"),
            ["can tho"] = (10.0452, 105.7469, "Cần Thơ"),
            ["binh duong"] = (10.9804, 106.6519, "Bình Dương"),
            ["dong nai"] = (10.9574, 106.8427, "Đồng Nai"),
            ["khanh hoa"] = (12.2388, 109.1967, "Khanh Hoa"),
            ["hai phong"] = (20.8449, 106.6881, "Hai Phong"),
            ["ba ria vung tau"] = (10.5417, 107.2429, "Ba Ria - Vung Tau"),
            ["ca mau"] = (9.1527, 105.1961, "Ca Mau"),
            ["lam dong"] = (11.9404, 108.4583, "Lam Dong"),
            ["kien giang"] = (10.0125, 105.0809, "Kien Giang"),
            ["long an"] = (10.6956, 106.2431, "Long An"),
            ["tay ninh"] = (11.3352, 106.1099, "Tay Ninh"),
            ["binh phuoc"] = (11.7512, 106.7235, "Binh Phuoc"),
            ["binh thuan"] = (10.9333, 108.1000, "Binh Thuan"),
            ["ninh thuan"] = (11.6739, 108.8620, "Ninh Thuan"),
            ["phu yen"] = (13.0882, 109.0929, "Phu Yen"),
            ["dak lak"] = (12.6667, 108.0500, "Dak Lak"),
            ["dak nong"] = (12.2646, 107.6098, "Dak Nong"),
            ["quang nam"] = (15.5736, 108.4740, "Quang Nam"),
            ["thua thien hue"] = (16.4637, 107.5909, "Thua Thien Hue"),
            ["vinh phuc"] = (21.3089, 105.6049, "Vinh Phuc"),
            ["thai nguyen"] = (21.5942, 105.8482, "Thai Nguyen"),
            ["bac ninh"] = (21.1861, 106.0763, "Bac Ninh"),
            ["hung yen"] = (20.6464, 106.0511, "Hung Yen"),
            ["ha nam"] = (20.5835, 105.9220, "Ha Nam"),
            ["hoa binh"] = (20.6861, 105.3131, "Hoa Binh"),
            ["phu tho"] = (21.3227, 105.4013, "Phu Tho"),
            ["bac giang"] = (21.2810, 106.1975, "Bac Giang"),
            ["quang ninh"] = (20.9712, 107.0448, "Quang Ninh"),
            ["hai duong"] = (20.9373, 106.3145, "Hai Duong"),
            ["thai binh"] = (20.4463, 106.3366, "Thai Binh"),
            ["an giang"] = (10.5216, 105.1259, "An Giang"),
            ["dong thap"] = (10.4938, 105.6882, "Dong Thap"),
            ["vinh long"] = (10.2540, 105.9722, "Vinh Long"),
            ["hau giang"] = (9.7845, 105.4701, "Hau Giang"),
            ["bac lieu"] = (9.2940, 105.7244, "Bac Lieu")
        };

        private static readonly Dictionary<string, string[]> Adjacent = new(StringComparer.OrdinalIgnoreCase)
        {
            ["ho chi minh"] = new[] { "binh duong", "dong nai", "long an", "tay ninh", "ba ria vung tau" },
            ["ha noi"] = new[] { "vinh phuc", "thai nguyen", "bac ninh", "hung yen", "ha nam", "hoa binh", "phu tho", "bac giang" },
            ["da nang"] = new[] { "thua thien hue", "quang nam" },
            ["can tho"] = new[] { "an giang", "dong thap", "vinh long", "hau giang", "kien giang" },
            ["binh duong"] = new[] { "ho chi minh", "dong nai", "binh phuoc", "tay ninh" },
            ["dong nai"] = new[] { "ho chi minh", "binh duong", "ba ria vung tau", "binh thuan", "lam dong", "binh phuoc" },
            ["khanh hoa"] = new[] { "phu yen", "dak lak", "lam dong", "ninh thuan" },
            ["hai phong"] = new[] { "quang ninh", "hai duong", "thai binh" },
            ["ba ria vung tau"] = new[] { "ho chi minh", "dong nai", "binh thuan" },
            ["ca mau"] = new[] { "bac lieu", "kien giang" },
            ["lam dong"] = new[] { "dong nai", "binh thuan", "ninh thuan", "khanh hoa", "dak lak", "dak nong", "binh phuoc" },
            ["kien giang"] = new[] { "ca mau", "bac lieu", "hau giang", "can tho", "an giang" }
        };

        public DeliveryScopeResult CheckScope(string? originText, double originLat, double originLng, double destinationLat, double destinationLng)
        {
            var origin = ResolveProvince(originText, originLat, originLng);
            var destination = ResolveProvince(null, destinationLat, destinationLng);
            return BuildResult(origin, destination);
        }

        public DeliveryScopeResult CheckScope(string? originText, double originLat, double originLng, string? destinationText)
        {
            var origin = ResolveProvince(originText, originLat, originLng);
            var destination = ResolveProvince(destinationText);
            return BuildResult(origin, destination);
        }

        private static DeliveryScopeResult BuildResult(string? origin, string? destination)
        {
            var allowed = origin != null && destination != null &&
                (string.Equals(origin, destination, StringComparison.OrdinalIgnoreCase) ||
                 (Adjacent.TryGetValue(origin, out var neighbors) && neighbors.Contains(destination, StringComparer.OrdinalIgnoreCase)));

            return new DeliveryScopeResult
            {
                IsWithinScope = allowed,
                OriginProvince = Display(origin),
                DestinationProvince = Display(destination),
                MessageVi = allowed ? "" : BuildOutOfScopeMessage(origin, destination),
                MessageEn = allowed ? "" : OutOfScopeEn
            };
        }

        public string? ResolveProvince(string? text, double? lat = null, double? lng = null)
        {
            var normalized = Normalize(text);
            if (!string.IsNullOrWhiteSpace(normalized))
            {
                foreach (var alias in Aliases)
                {
                    if (normalized.Contains(alias.Key, StringComparison.OrdinalIgnoreCase))
                        return alias.Value;
                }

                if (Centers.ContainsKey(normalized))
                    return normalized;
            }

            if (lat.HasValue && lng.HasValue)
            {
                return Centers
                    .Select(kvp => new { Province = kvp.Key, Distance = Haversine(lat.Value, lng.Value, kvp.Value.Lat, kvp.Value.Lng) })
                    .OrderBy(x => x.Distance)
                    .FirstOrDefault()?.Province;
            }

            return null;
        }

        private static string? Display(string? province)
        {
            if (province == null) return null;
            return Centers.TryGetValue(province, out var center) ? center.Display : province;
        }

        private static string Normalize(string? input)
        {
            if (string.IsNullOrWhiteSpace(input)) return "";
            var formD = input.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder(formD.Length);
            foreach (var ch in formD)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
                    sb.Append(ch);
            }

            return sb.ToString()
                .Normalize(NormalizationForm.FormC)
                .Replace("đ", "d")
                .Replace("Đ", "d")
                .Replace("đ", "d")
                .Replace("-", " ")
                .Replace(".", " ")
                .Replace(",", " ")
                .Replace("  ", " ")
                .Trim();
        }

        private static string BuildOutOfScopeMessage(string? origin, string? destination)
        {
            if (origin == null || destination == null)
                return "Không thể xác định phạm vi giao xe từ địa chỉ hiện tại. Vui lòng chọn lại vị trí hoặc nhận xe tại vị trí xe.";

            return $"{OutOfScopeVi} Tuyến {Display(origin)} - {Display(destination)} chưa được hỗ trợ.";
        }

        private static double Haversine(double lat1, double lon1, double lat2, double lon2)
        {
            const double r = 6371;
            var dLat = DegreesToRadians(lat2 - lat1);
            var dLon = DegreesToRadians(lon2 - lon1);
            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                    Math.Cos(DegreesToRadians(lat1)) * Math.Cos(DegreesToRadians(lat2)) *
                    Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            return r * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        }

        private static double DegreesToRadians(double degrees) => degrees * Math.PI / 180;
    }
}
