using System;
using System.Collections.Generic;
using System.Linq;

namespace CarRentalSystem.Services
{
    /// <summary>
    /// Cấu hình tăng giá theo loại ngày
    /// </summary>
    public class PricingTierConfig
    {
        public decimal Multiplier { get; set; }
        public string Description { get; set; } = "";
        public int Priority { get; set; } // Priority cao hơn = áp dụng trước
    }

    public interface IHolidayPricingService
    {
        /// <summary>
        /// Lấy multiplier cho một ngày cụ thể
        /// </summary>
        decimal GetMultiplier(DateTime date);

        /// <summary>
        /// Lấy multiplier cho một khoảng thời gian (lấy max)
        /// </summary>
        decimal GetMultiplier(DateTime startDate, DateTime endDate);

        /// <summary>
        /// Tính tổng giá cho một khoảng thời gian (tính theo ngày)
        /// </summary>
        (decimal totalPrice, int totalDays, decimal avgMultiplier) CalculatePrice(decimal pricePerDay, DateTime startDate, DateTime endDate);

        /// <summary>
        /// Kiểm tra ngày có phải là ngày lễ/cuối tuần không
        /// </summary>
        (bool isHoliday, bool isWeekend, string? holidayName) GetDateInfo(DateTime date);

        /// <summary>
        /// Lấy danh sách ngày lễ trong năm
        /// </summary>
        List<DateTime> GetHolidays(int year);

        /// <summary>
        /// Cấu hình multiplier cho cuối tuần (mặc định: 1.3 = +30%)
        /// </summary>
        decimal WeekendMultiplier { get; set; }

        /// <summary>
        /// Cấu hình multiplier cho ngày lễ lớn VN (mặc định: 2.0 = +100%)
        /// </summary>
        decimal MajorHolidayMultiplier { get; set; }

        /// <summary>
        /// Cấu hình multiplier cho Tết Nguyên Đán (mặc định: 2.5 = +150%)
        /// </summary>
        decimal TetHolidayMultiplier { get; set; }
    }

    public class HolidayPricingService : IHolidayPricingService
    {
        // Cấu hình multiplier
        public decimal WeekendMultiplier { get; set; } = 1.30m; // +30%
        public decimal MajorHolidayMultiplier { get; set; } = 2.00m; // +100%
        public decimal TetHolidayMultiplier { get; set; } = 2.50m; // +150%

        /// <summary>
        /// Danh sách ngày lễ cố định Việt Nam (DD/MM)
        /// </summary>
        private static readonly Dictionary<string, (string Name, decimal Multiplier)> FixedHolidays = new()
        {
            // Tết Dương lịch - 1/1
            { "01/01", ("Tết Dương lịch", 2.00m) },
            
            // Giỗ Tổ Hùng Vương - 10/3 âm lịch (ngày mùng 10 tháng 3 âm lịch)
            // Năm 2026: 27/4/2026, Năm 2027: 16/4/2027
            
            // Ngày Giải phóng miền Nam - 30/4
            { "30/04", ("Ngày Giải phóng miền Nam 30/4", 2.00m) },
            
            // Ngày Quốc tế Lao động - 1/5
            { "01/05", ("Ngày Quốc tế Lao động 1/5", 2.00m) },
            
            // Quốc khánh - 2/9
            { "02/09", ("Quốc khánh Việt Nam 2/9", 2.00m) },
            
            // Ngày Học sinh - Sinh viên 20/11
            { "20/11", ("Ngày Nhà giáo Việt Nam 20/11", 1.50m) },
            
            // Ngày Noel 25/12
            { "25/12", ("Giáng Sinh 25/12", 1.50m) },
            
            // Đêm Giao thừa (31/12)
            { "31/12", ("Đêm Giao thừa", 1.50m) },
        };

        /// <summary>
        /// Ngày lễ di chuyển theo năm ( tính theo thứ trong tuần)
        /// </summary>
        private static readonly Dictionary<int, Dictionary<string, (DateTime Date, string Name, decimal Multiplier)>> VariableHolidays = new()
        {
            // 2025
            {
                2025, new Dictionary<string, (DateTime, string, decimal)>
                {
                    // Giỗ Tổ Hùng Vương 2025 - 18/4 (thứ 6)
                    { "LunarNewYear", (new DateTime(2025, 4, 18), "Giỗ Tổ Hùng Vương 2025", 2.00m) },
                }
            },
            // 2026
            {
                2026, new Dictionary<string, (DateTime, string, decimal)>
                {
                    // Giỗ Tổ Hùng Vương 2026 - 27/4 (thứ 2)
                    { "HungKings", (new DateTime(2026, 4, 27), "Giỗ Tổ Hùng Vương 2026", 2.00m) },
                }
            },
            // 2027
            {
                2027, new Dictionary<string, (DateTime, string, decimal)>
                {
                    // Giỗ Tổ Hùng Vương 2027 - 16/4 (thứ 6)
                    { "HungKings", (new DateTime(2027, 4, 16), "Giỗ Tổ Hùng Vương 2027", 2.00m) },
                }
            }
        };

        /// <summary>
        /// Tết Nguyên Đán - Tính theo lịch âm (thường nghỉ 7 ngày)
        /// Lunar New Year dates for upcoming years
        /// </summary>
        private static readonly Dictionary<int, (DateTime StartDate, DateTime EndDate, string YearName)> TetHolidays = new()
        {
            { 2026, (new DateTime(2026, 2, 17), new DateTime(2026, 2, 24), "Tết Nguyên Đán 2026") },
            { 2027, (new DateTime(2027, 2, 9), new DateTime(2027, 2, 16), "Tết Nguyên Đán 2027") },
            { 2028, (new DateTime(2028, 1, 29), new DateTime(2028, 2, 5), "Tết Nguyên Đán 2028") },
        };

        public decimal GetMultiplier(DateTime date)
        {
            var (isHoliday, isWeekend, holidayName) = GetDateInfo(date);
            
            if (isHoliday && holidayName?.StartsWith("Tết") == true)
            {
                return TetHolidayMultiplier;
            }
            else if (isHoliday)
            {
                return MajorHolidayMultiplier;
            }
            else if (isWeekend)
            {
                return WeekendMultiplier;
            }
            
            return 1.0m;
        }

        public decimal GetMultiplier(DateTime startDate, DateTime endDate)
        {
            decimal maxMultiplier = 1.0m;
            
            for (var date = startDate.Date; date <= endDate.Date; date = date.AddDays(1))
            {
                var multiplier = GetMultiplier(date);
                if (multiplier > maxMultiplier)
                {
                    maxMultiplier = multiplier;
                }
            }
            
            return maxMultiplier;
        }

        /// <summary>
        /// Tính tổng giá theo số ngày, mỗi ngày có multiplier riêng
        /// </summary>
        public (decimal totalPrice, int totalDays, decimal avgMultiplier) CalculatePrice(decimal pricePerDay, DateTime startDate, DateTime endDate)
        {
            decimal totalPrice = 0;
            int totalDays = 0;

            for (var date = startDate.Date; date <= endDate.Date; date = date.AddDays(1))
            {
                var multiplier = GetMultiplier(date);
                totalPrice += pricePerDay * multiplier;
                totalDays++;
            }

            var avgMultiplier = totalDays > 0 ? totalPrice / (pricePerDay * totalDays) : 1.0m;
            return (Math.Round(totalPrice), totalDays, Math.Round(avgMultiplier, 2));
        }

        public (bool isHoliday, bool isWeekend, string? holidayName) GetDateInfo(DateTime date)
        {
            // 1. Kiểm tra Tết Nguyên Đán
            var year = date.Year;
            if (TetHolidays.TryGetValue(year, out var tet) || TetHolidays.TryGetValue(year + 1, out tet))
            {
                if (date >= tet.StartDate && date <= tet.EndDate)
                {
                    return (true, false, tet.YearName);
                }
            }

            // 2. Kiểm tra ngày lễ cố định
            var dateKey = $"{date.Day:D2}/{date.Month:D2}";
            if (FixedHolidays.TryGetValue(dateKey, out var holiday))
            {
                return (true, false, holiday.Name);
            }

            // 3. Kiểm tra ngày lễ di chuyển
            if (VariableHolidays.TryGetValue(year, out var yearHolidays))
            {
                foreach (var vh in yearHolidays.Values)
                {
                    if (date.Date == vh.Date.Date)
                    {
                        return (true, false, vh.Name);
                    }
                }
            }

            // 4. Kiểm tra cuối tuần (thứ 7 = Saturday, Chủ nhật = Sunday)
            if (date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday)
            {
                return (false, true, null);
            }

            return (false, false, null);
        }

        public List<DateTime> GetHolidays(int year)
        {
            var holidays = new List<DateTime>();

            // Thêm ngày lễ cố định
            foreach (var kvp in FixedHolidays)
            {
                var parts = kvp.Key.Split('/');
                var day = int.Parse(parts[0]);
                var month = int.Parse(parts[1]);
                
                try
                {
                    holidays.Add(new DateTime(year, month, day));
                }
                catch
                {
                    // Bỏ qua ngày không hợp lệ
                }
            }

            // Thêm ngày lễ di chuyển
            if (VariableHolidays.TryGetValue(year, out var yearHolidays))
            {
                holidays.AddRange(yearHolidays.Values.Select(h => h.Date));
            }

            // Thêm Tết Nguyên Đán
            if (TetHolidays.TryGetValue(year, out var tet))
            {
                for (var date = tet.StartDate; date <= tet.EndDate; date = date.AddDays(1))
                {
                    holidays.Add(date);
                }
            }

            return holidays.OrderBy(h => h).ToList();
        }
    }
}
