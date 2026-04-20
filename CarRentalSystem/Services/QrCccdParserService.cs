using System.Text.RegularExpressions;

namespace CarRentalSystem.Services
{
    /// <summary>
    /// Service xử lý dữ liệu QR từ CCCD gắn chip
    /// Hỗ trợ nhiều định dạng QR phổ biến tại Việt Nam
    /// </summary>
    public interface IQrCccdParserService
    {
        /// <summary>
        /// Parse dữ liệu QR CCCD và trả về thông tin đã trích xuất
        /// </summary>
        QrCccdParseResult Parse(string qrData);
        
        /// <summary>
        /// Validate số CCCD có đúng 12 số và checksum
        /// </summary>
        bool ValidateCitizenId(string citizenId);
        
        /// <summary>
        /// Kiểm tra định dạng QR data
        /// </summary>
        QrDataFormat DetectFormat(string qrData);
    }

    public class QrCccdParserService : IQrCccdParserService
    {
        // Các regex patterns cho việc detect format
        private static readonly Regex PipeSeparatedPattern = new(@"^[0-9]{12}\|", RegexOptions.Compiled);
        private static readonly Regex MrzPattern = new(@"^[A-Z]{2}[A-Z<]{2,3}[A-Z0-9<]+\n[A-Z0-9<]+", RegexOptions.Compiled);
        private static readonly Regex CitizenIdPattern = new(@"^[0-9]{12}$", RegexOptions.Compiled);
        private static readonly Regex DatePattern = new(@"^(\d{8}|\d{4}-\d{2}-\d{2}|\d{2}/\d{2}/\d{4})$", RegexOptions.Compiled);
        private static readonly Regex GenderPattern = new(@"^(Nam|Nữ|Nam|NU|Male|Female)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex VietnameseNamePattern = new(@"^[A-ZÀÁÂÃÈÉÊÌÍÒÓÔÕÙÚĂĐĨŨƠàáâãèéêìíòóôõùúăđĩũơẠ-Ỹ\s]+$", RegexOptions.Compiled);

        public QrCccdParseResult Parse(string qrData)
        {
            if (string.IsNullOrWhiteSpace(qrData))
            {
                return new QrCccdParseResult
                {
                    Success = false,
                    ErrorMessage = "Dữ liệu QR trống"
                };
            }

            qrData = qrData.Trim();
            var format = DetectFormat(qrData);

            return format switch
            {
                QrDataFormat.PipeSeparated => ParsePipeSeparated(qrData),
                QrDataFormat.MrzFormat => ParseMrzFormat(qrData),
                QrDataFormat.SimpleFormat => ParseSimpleFormat(qrData),
                _ => new QrCccdParseResult
                {
                    Success = false,
                    ErrorMessage = $"Định dạng QR không được hỗ trợ: {format}"
                }
            };
        }

        public bool ValidateCitizenId(string citizenId)
        {
            if (string.IsNullOrWhiteSpace(citizenId))
                return false;

            // Kiểm tra độ dài
            if (citizenId.Length != 12)
                return false;

            // Kiểm tra toàn số
            if (!CitizenIdPattern.IsMatch(citizenId))
                return false;

            // Kiểm tra checksum (dựa trên thuật toán Luhn-like của Bộ Công An VN)
            return ValidateCitizenIdChecksum(citizenId);
        }

        public QrDataFormat DetectFormat(string qrData)
        {
            if (string.IsNullOrWhiteSpace(qrData))
                return QrDataFormat.Unknown;

            qrData = qrData.Trim();

            if (PipeSeparatedPattern.IsMatch(qrData))
                return QrDataFormat.PipeSeparated;

            if (MrzPattern.IsMatch(qrData))
                return QrDataFormat.MrzFormat;

            if (qrData.Contains('|') || qrData.Contains(','))
                return QrDataFormat.PipeSeparated;

            return QrDataFormat.SimpleFormat;
        }

        /// <summary>
        /// Parse định dạng pipe-separated (phổ biến nhất ở Việt Nam)
        /// Format: SoCCCD|HoTen|NgaySinh|GioiTinh|DiaChi...
        /// </summary>
        private QrCccdParseResult ParsePipeSeparated(string data)
        {
            try
            {
                var parts = data.Split('|').Select(p => p.Trim()).ToArray();

                if (parts.Length < 5)
                {
                    return new QrCccdParseResult
                    {
                        Success = false,
                        ErrorMessage = "Dữ liệu QR không đủ trường thông tin"
                    };
                }

                // Trích xuất số CCCD
                var citizenId = parts.FirstOrDefault(p => CitizenIdPattern.IsMatch(p)) ?? string.Empty;

                if (string.IsNullOrEmpty(citizenId) || citizenId.Length != 12)
                {
                    return new QrCccdParseResult
                    {
                        Success = false,
                        ErrorMessage = "Không tìm thấy số CCCD hợp lệ (12 chữ số)"
                    };
                }

                // Trích xuất họ tên (field dài, có khoảng trắng, chứa chữ cái có dấu)
                var fullName = parts
                    .Where(p => VietnameseNamePattern.IsMatch(p) && p.Length > 5)
                    .FirstOrDefault() ?? string.Empty;

                if (string.IsNullOrWhiteSpace(fullName))
                {
                    return new QrCccdParseResult
                    {
                        Success = false,
                        ErrorMessage = "Không tìm thấy họ tên trong dữ liệu"
                    };
                }

                // Trích xuất ngày sinh
                var dateOfBirthStr = parts.FirstOrDefault(p => DatePattern.IsMatch(p)) ?? string.Empty;
                var dateOfBirth = ParseDate(dateOfBirthStr);

                // Trích xuất giới tính
                var genderRaw = parts.FirstOrDefault(p => GenderPattern.IsMatch(p)) ?? string.Empty;
                var gender = NormalizeGender(genderRaw);

                // Trích xuất địa chỉ (field dài nhất có chứa số)
                var address = parts
                    .Where(p => p.Length > 10 && p.Any(char.IsDigit))
                    .OrderByDescending(p => p.Length)
                    .FirstOrDefault() ?? string.Empty;

                // Trích xuất nơi cấp (nếu có)
                var placeOfIssue = parts.Length > 10 
                    ? parts.Skip(10).FirstOrDefault(p => p.Length > 3 && !p.All(char.IsDigit)) 
                    : string.Empty;

                return new QrCccdParseResult
                {
                    Success = true,
                    Data = new QrCccdInfo
                    {
                        CitizenId = citizenId,
                        FullName = NormalizeName(fullName),
                        DateOfBirth = dateOfBirth,
                        Gender = gender,
                        Address = address,
                        PlaceOfIssue = placeOfIssue,
                        Format = QrDataFormat.PipeSeparated
                    }
                };
            }
            catch (Exception ex)
            {
                return new QrCccdParseResult
                {
                    Success = false,
                    ErrorMessage = $"Lỗi xử lý dữ liệu: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Parse MRZ format (Machine Readable Zone - dùng trên passport và một số CCCD)
        /// </summary>
        private QrCccdParseResult ParseMrzFormat(string data)
        {
            try
            {
                var lines = data.Split('\n').Select(l => l.Trim()).ToArray();
                if (lines.Length < 2)
                {
                    return new QrCccdParseResult
                    {
                        Success = false,
                        ErrorMessage = "Dữ liệu MRZ không hợp lệ"
                    };
                }

                // Tìm dòng chứa ID
                var idLine = lines.FirstOrDefault(l => l.StartsWith("ID") || l.StartsWith("I")) ?? lines[0];
                
                // Trích xuất số CCCD từ dòng ID
                var citizenIdMatch = Regex.Match(idLine, @"(?:ID|IVN|I)[A-Z<]*(\d{12})");
                if (!citizenIdMatch.Success)
                {
                    // Thử pattern khác
                    citizenIdMatch = Regex.Match(idLine, @"(\d{12})");
                }

                var citizenId = citizenIdMatch.Success ? citizenIdMatch.Groups[1].Value : string.Empty;

                // Trích xuất họ tên (sau các ký tự đầu)
                var nameLine = lines.Last();
                var nameClean = nameLine.Replace("<", " ").Trim();
                var nameParts = nameClean.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                var fullName = nameParts.Length > 0 
                    ? string.Join(" ", nameParts.Select(p => char.ToUpper(p[0]) + (p.Length > 1 ? p.Substring(1).ToLower() : "")))
                    : string.Empty;

                return new QrCccdParseResult
                {
                    Success = !string.IsNullOrEmpty(citizenId),
                    ErrorMessage = string.IsNullOrEmpty(citizenId) ? "Không tìm thấy số CCCD" : null,
                    Data = new QrCccdInfo
                    {
                        CitizenId = citizenId,
                        FullName = fullName,
                        Format = QrDataFormat.MrzFormat
                    }
                };
            }
            catch (Exception ex)
            {
                return new QrCccdParseResult
                {
                    Success = false,
                    ErrorMessage = $"Lỗi xử lý MRZ: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Parse format đơn giản (fallback - trích xuất thông tin cơ bản)
        /// </summary>
        private QrCccdParseResult ParseSimpleFormat(string data)
        {
            try
            {
                // Tìm 12 số liên tiếp (số CCCD)
                var citizenIdMatch = Regex.Match(data, @"(\d{12})");
                if (!citizenIdMatch.Success)
                {
                    return new QrCccdParseResult
                    {
                        Success = false,
                        ErrorMessage = "Không tìm thấy số CCCD 12 chữ số trong dữ liệu"
                    };
                }

                // Tìm họ tên (sau dấu | thứ 2 hoặc trong ngoặc)
                var nameMatch = Regex.Match(data, @"\|([^|]{5,50})\|");
                var fullName = nameMatch.Success ? nameMatch.Groups[1].Value.Trim() : string.Empty;

                return new QrCccdParseResult
                {
                    Success = true,
                    Data = new QrCccdInfo
                    {
                        CitizenId = citizenIdMatch.Groups[1].Value,
                        FullName = NormalizeName(fullName),
                        Format = QrDataFormat.SimpleFormat
                    }
                };
            }
            catch (Exception ex)
            {
                return new QrCccdParseResult
                {
                    Success = false,
                    ErrorMessage = $"Lỗi xử lý: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Validate checksum của số CCCD (thuật toán của Bộ Công An VN)
        /// </summary>
        private bool ValidateCitizenIdChecksum(string citizenId)
        {
            if (string.IsNullOrEmpty(citizenId) || citizenId.Length != 12)
                return false;

            if (!citizenId.All(char.IsDigit))
                return false;

            // Lấy 11 số đầu và số cuối (checksum)
            var digits = citizenId.Select(c => int.Parse(c.ToString())).ToArray();
            var checkDigit = digits[11];

            // Các weights theo quy định của Bộ Công An
            var weights = new[] { 0, 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 };

            var sum = 0;
            for (int i = 0; i < 11; i++)
            {
                sum += digits[i] * weights[i];
            }

            var calculatedCheck = (10 - (sum % 11)) % 10;

            return checkDigit == calculatedCheck;
        }

        /// <summary>
        /// Parse chuỗi ngày sinh thành định dạng ISO (yyyy-MM-dd)
        /// </summary>
        private string ParseDate(string dateStr)
        {
            if (string.IsNullOrEmpty(dateStr))
                return string.Empty;

            // Format: ddMMyyyy
            if (Regex.IsMatch(dateStr, @"^\d{8}$"))
            {
                var dd = dateStr.Substring(0, 2);
                var mm = dateStr.Substring(2, 2);
                var yyyy = dateStr.Substring(4, 4);
                return $"{yyyy}-{mm}-{dd}";
            }

            // Format: dd/MM/yyyy
            if (dateStr.Contains('/'))
            {
                var parts = dateStr.Split('/');
                if (parts.Length == 3)
                {
                    return $"{parts[2]}-{parts[1].PadLeft(2, '0')}-{parts[0].PadLeft(2, '0')}";
                }
            }

            // Format: yyyy-MM-dd (đã đúng)
            if (Regex.IsMatch(dateStr, @"^\d{4}-\d{2}-\d{2}$"))
                return dateStr;

            return dateStr;
        }

        /// <summary>
        /// Normalize giới tính về Nam/Nữ
        /// </summary>
        private string NormalizeGender(string gender)
        {
            if (string.IsNullOrEmpty(gender))
                return string.Empty;

            var g = gender.Trim().ToUpper();
            return g switch
            {
                "NAM" or "MALE" or "M" => "Nam",
                "NỮ" or "NU" or "FEMALE" or "F" or "NỮ" => "Nữ",
                _ => gender.Trim()
            };
        }

        /// <summary>
        /// Normalize họ tên (viết hoa chữ cái đầu)
        /// </summary>
        private string NormalizeName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return string.Empty;

            return string.Join(" ", name.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(word => word.Length > 0 
                    ? char.ToUpper(word[0]) + (word.Length > 1 ? word.Substring(1).ToLower() : "")
                    : word))
                .Trim();
        }
    }

    /// <summary>
    /// Kết quả parse QR CCCD
    /// </summary>
    public class QrCccdParseResult
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public QrCccdInfo? Data { get; set; }
    }

    /// <summary>
    /// Thông tin trích xuất từ QR CCCD
    /// </summary>
    public class QrCccdInfo
    {
        public string CitizenId { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string DateOfBirth { get; set; } = string.Empty;
        public string Gender { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string PlaceOfIssue { get; set; } = string.Empty;
        public QrDataFormat Format { get; set; }
    }

    /// <summary>
    /// Các định dạng QR data được hỗ trợ
    /// </summary>
    public enum QrDataFormat
    {
        Unknown = 0,
        PipeSeparated = 1,  // Format phổ biến nhất ở VN: 012345678901|Nguyen Van A|...
        MrzFormat = 2,       // Machine Readable Zone
        SimpleFormat = 3     // Format đơn giản - fallback
    }
}
