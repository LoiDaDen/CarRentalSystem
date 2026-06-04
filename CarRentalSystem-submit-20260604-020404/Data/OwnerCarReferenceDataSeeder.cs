using CarRentalSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace CarRentalSystem.Data
{
    public static class OwnerCarReferenceDataSeeder
    {
        public static async Task SeedAsync(ApplicationDbContext db)
        {
            await SeedAmenitiesAsync(db);
            await SeedContractTypesAsync(db);
            await SeedCarCatalogAsync(db);
            await BackfillLegacyRolesAsync(db);
        }

        private static async Task SeedAmenitiesAsync(ApplicationDbContext db)
        {
            var amenities = new (string Code, string Name, string Icon)[]
            {
                ("MAP", "Bản đồ", "bi-map"),
                ("BLUETOOTH", "Bluetooth", "bi-bluetooth"),
                ("CAMERA_360", "Camera 360", "bi-camera-video"),
                ("SIDE_CAMERA", "Camera cập lề", "bi-camera"),
                ("DASHCAM", "Camera hành trình", "bi-webcam"),
                ("REAR_CAMERA", "Camera lùi", "bi-camera-fill"),
                ("TPMS", "Cảm biến lốp", "bi-speedometer2"),
                ("COLLISION_WARNING", "Cảnh báo va chạm", "bi-shield-exclamation"),
                ("SPEED_WARNING", "Cảnh báo tốc độ", "bi-exclamation-triangle"),
                ("SUNROOF", "Cửa sổ trời", "bi-sun"),
                ("GPS", "Định vị GPS", "bi-geo-alt"),
                ("CHILD_SEAT", "Ghế trẻ em", "bi-person-arms-up"),
                ("USB", "Khe cắm USB", "bi-usb-drive"),
                ("SPARE_TIRE", "Lốp dự phòng", "bi-record-circle"),
                ("DVD", "Màn hình DVD", "bi-display"),
                ("TONNEAU_COVER", "Nắp thùng xe bán tải", "bi-truck"),
                ("ETC", "ETC", "bi-credit-card"),
                ("AIRBAGS", "Túi khí an toàn", "bi-shield-check"),
                ("CRUISE_CONTROL", "Cruise Control", "bi-speedometer"),
                ("TOUCH_SCREEN", "Màn hình cảm ứng", "bi-hand-index-thumb"),
                ("POWER_TRUNK", "Cốp điện", "bi-lightning-charge"),
                ("ELECTRIC_PARKING", "Phanh tay điện tử", "bi-toggles")
            };

            var order = 1;
            foreach (var item in amenities)
            {
                if (await db.CarAmenities.AnyAsync(a => a.Code == item.Code)) continue;
                db.CarAmenities.Add(new CarAmenity
                {
                    Code = item.Code,
                    Name = item.Name,
                    Icon = item.Icon,
                    DisplayOrder = order++
                });
            }
            await db.SaveChangesAsync();
        }

        private static async Task SeedContractTypesAsync(ApplicationDbContext db)
        {
            var rows = new (string Code, string Name, string Description)[]
            {
                (PhysicalContractTypeCodes.SelfDriveRental, "Hợp đồng thuê xe tự lái", "Hợp đồng giấy ký trực tiếp cho từng đơn thuê xe tự lái."),
                (PhysicalContractTypeCodes.CarOwnerDocument, "Hồ sơ xe/chủ xe", "Giấy tờ xe, đăng kiểm, bảo hiểm hoặc hồ sơ chủ xe."),
                (PhysicalContractTypeCodes.HandoverRecord, "Biên bản bàn giao", "Ảnh/PDF biên bản giao nhận xe."),
                (PhysicalContractTypeCodes.DepositAgreement, "Thỏa thuận đặt cọc", "Chứng từ hoặc thỏa thuận đặt cọc thuê xe."),
                (PhysicalContractTypeCodes.Other, "Khác", "Tài liệu khác cần lưu trữ.")
            };

            foreach (var row in rows)
            {
                if (await db.PhysicalContractTypes.AnyAsync(t => t.Code == row.Code)) continue;
                db.PhysicalContractTypes.Add(new PhysicalContractType
                {
                    Code = row.Code,
                    Name = row.Name,
                    Description = row.Description
                });
            }
            await db.SaveChangesAsync();
        }

        private static async Task SeedCarCatalogAsync(ApplicationDbContext db)
        {
            var catalog = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                ["Toyota"] = new[] { "Vios", "Yaris", "Corolla Altis", "Corolla Cross", "Camry", "Raize", "Yaris Cross", "Innova", "Innova Cross", "Fortuner", "Land Cruiser", "Hilux", "Alphard", "Wigo" },
                ["Honda"] = new[] { "City", "Civic", "Accord", "CR-V", "HR-V", "BR-V", "Jazz", "Pilot" },
                ["Hyundai"] = new[] { "Grand i10", "Accent", "Elantra", "Tucson", "Santa Fe", "Creta", "Stargazer", "Venue", "Palisade", "Kona" },
                ["Kia"] = new[] { "Morning", "Soluto", "K3", "K5", "Seltos", "Sonet", "Sportage", "Sorento", "Carnival", "Carens" },
                ["Mazda"] = new[] { "Mazda2", "Mazda3", "Mazda6", "CX-3", "CX-30", "CX-5", "CX-8", "BT-50" },
                ["Ford"] = new[] { "Ranger", "Ranger Raptor", "Everest", "Territory", "Explorer", "Transit", "EcoSport", "Focus" },
                ["Mitsubishi"] = new[] { "Attrage", "Xpander", "Xpander Cross", "Outlander", "Triton", "Pajero Sport", "Xforce" },
                ["VinFast"] = new[] { "Fadil", "Lux A2.0", "Lux SA2.0", "VF e34", "VF 3", "VF 5", "VF 6", "VF 7", "VF 8", "VF 9" },
                ["Mercedes-Benz"] = new[] { "A-Class", "C-Class", "E-Class", "S-Class", "GLA", "GLB", "GLC", "GLE", "GLS", "V-Class" },
                ["BMW"] = new[] { "3 Series", "5 Series", "7 Series", "X1", "X3", "X4", "X5", "X6", "i4", "iX" },
                ["Audi"] = new[] { "A3", "A4", "A6", "A8", "Q2", "Q3", "Q5", "Q7", "Q8", "e-tron" },
                ["Lexus"] = new[] { "ES", "IS", "LS", "NX", "RX", "GX", "LX", "LM" },
                ["Nissan"] = new[] { "Almera", "Sunny", "Navara", "Kicks", "X-Trail", "Terra", "Patrol" },
                ["Chevrolet"] = new[] { "Spark", "Aveo", "Cruze", "Captiva", "Trailblazer", "Colorado", "Tahoe", "Suburban" },
                ["Peugeot"] = new[] { "2008", "3008", "5008", "408", "Traveller" },
                ["Subaru"] = new[] { "Impreza", "Legacy", "Forester", "Outback", "XV", "BRZ" },
                ["Volkswagen"] = new[] { "Polo", "Golf", "Passat", "Tiguan", "Teramont", "Touareg" },
                ["Volvo"] = new[] { "S60", "S90", "XC40", "XC60", "XC90" },
                ["Porsche"] = new[] { "Macan", "Cayenne", "Panamera", "911", "Taycan" },
                ["Suzuki"] = new[] { "Swift", "Ciaz", "Ertiga", "XL7", "Jimny", "Carry" },
                ["Isuzu"] = new[] { "D-Max", "mu-X", "QKR" },
                ["MG"] = new[] { "MG5", "ZS", "HS", "RX5", "Marvel R" },
                ["BYD"] = new[] { "Dolphin", "Atto 3", "Seal", "Han", "Tang" },
                ["Tesla"] = new[] { "Model 3", "Model Y", "Model S", "Model X", "Cybertruck" }
            };

            foreach (var brandName in catalog.Keys)
            {
                var brand = await db.CarBrands.FirstOrDefaultAsync(b => b.Name == brandName);
                if (brand == null)
                {
                    brand = new CarBrand { Name = brandName };
                    db.CarBrands.Add(brand);
                    await db.SaveChangesAsync();
                }

                foreach (var modelName in catalog[brandName])
                {
                    if (await db.CarModels.AnyAsync(m => m.BrandID == brand.BrandID && m.Name == modelName)) continue;
                    db.CarModels.Add(new CarModel
                    {
                        BrandID = brand.BrandID,
                        Name = modelName,
                        FromYear = 1990,
                        ToYear = null
                    });
                }
                await db.SaveChangesAsync();
            }
        }

        private static async Task BackfillLegacyRolesAsync(ApplicationDbContext db)
        {
            await db.Users
                .Where(u => u.Role == "Partner" || u.Role == "Driver")
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(u => u.Role, UserRoleConstants.Customer)
                    .SetProperty(u => u.OriginalRole, (string?)null)
                    .SetProperty(u => u.IsPartnerApproved, false)
                    .SetProperty(u => u.PartnerApprovedAt, (DateTime?)null));
        }
    }
}
