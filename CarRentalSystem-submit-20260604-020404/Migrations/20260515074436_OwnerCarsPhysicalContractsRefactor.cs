using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CarRentalSystem.Migrations
{
    /// <inheritdoc />
    public partial class OwnerCarsPhysicalContractsRefactor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CarModelID",
                table: "Cars",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DailyMileageLimitKm",
                table: "Cars",
                type: "decimal(10,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "DeliveryEnabled",
                table: "Cars",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "DeliveryFeePerKm",
                table: "Cars",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ExtraMileageFeePerKm",
                table: "Cars",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "FreeDeliveryRadiusKm",
                table: "Cars",
                type: "decimal(10,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "MaxDeliveryDistanceKm",
                table: "Cars",
                type: "decimal(10,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "MileageLimitEnabled",
                table: "Cars",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "MonthlyDiscountPercent",
                table: "Cars",
                type: "decimal(5,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "RentalTerms",
                table: "Cars",
                type: "nvarchar(1200)",
                maxLength: 1200,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "WeeklyDiscountPercent",
                table: "Cars",
                type: "decimal(5,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "CarAmenities",
                columns: table => new
                {
                    CarAmenityID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Icon = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CarAmenities", x => x.CarAmenityID);
                });

            migrationBuilder.CreateTable(
                name: "CarModels",
                columns: table => new
                {
                    CarModelID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BrandID = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    FromYear = table.Column<int>(type: "int", nullable: true),
                    ToYear = table.Column<int>(type: "int", nullable: true),
                    SeatCount = table.Column<int>(type: "int", nullable: true),
                    BodyType = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    FuelType = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    Transmission = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CarModels", x => x.CarModelID);
                    table.ForeignKey(
                        name: "FK_CarModels_CarBrands_BrandID",
                        column: x => x.BrandID,
                        principalTable: "CarBrands",
                        principalColumn: "BrandID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PhysicalContractTypes",
                columns: table => new
                {
                    PhysicalContractTypeID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PhysicalContractTypes", x => x.PhysicalContractTypeID);
                });

            migrationBuilder.CreateTable(
                name: "CarAmenityMappings",
                columns: table => new
                {
                    CarID = table.Column<int>(type: "int", nullable: false),
                    CarAmenityID = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CarAmenityMappings", x => new { x.CarID, x.CarAmenityID });
                    table.ForeignKey(
                        name: "FK_CarAmenityMappings_CarAmenities_CarAmenityID",
                        column: x => x.CarAmenityID,
                        principalTable: "CarAmenities",
                        principalColumn: "CarAmenityID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CarAmenityMappings_Cars_CarID",
                        column: x => x.CarID,
                        principalTable: "Cars",
                        principalColumn: "CarID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PhysicalContracts",
                columns: table => new
                {
                    PhysicalContractID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BookingID = table.Column<int>(type: "int", nullable: true),
                    CarID = table.Column<int>(type: "int", nullable: true),
                    OwnerID = table.Column<int>(type: "int", nullable: false),
                    PhysicalContractTypeID = table.Column<int>(type: "int", nullable: true),
                    Title = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: true),
                    FileUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    FileKind = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    SuggestedContractTypeCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    AiConfidence = table.Column<decimal>(type: "decimal(5,2)", nullable: true),
                    AiReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    UploadedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ConfirmedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PhysicalContracts", x => x.PhysicalContractID);
                    table.ForeignKey(
                        name: "FK_PhysicalContracts_Bookings_BookingID",
                        column: x => x.BookingID,
                        principalTable: "Bookings",
                        principalColumn: "BookingID",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_PhysicalContracts_Cars_CarID",
                        column: x => x.CarID,
                        principalTable: "Cars",
                        principalColumn: "CarID",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_PhysicalContracts_PhysicalContractTypes_PhysicalContractTypeID",
                        column: x => x.PhysicalContractTypeID,
                        principalTable: "PhysicalContractTypes",
                        principalColumn: "PhysicalContractTypeID",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_PhysicalContracts_Users_OwnerID",
                        column: x => x.OwnerID,
                        principalTable: "Users",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Cars_CarModelID",
                table: "Cars",
                column: "CarModelID");

            migrationBuilder.CreateIndex(
                name: "IX_CarAmenities_Code",
                table: "CarAmenities",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CarAmenityMappings_CarAmenityID",
                table: "CarAmenityMappings",
                column: "CarAmenityID");

            migrationBuilder.CreateIndex(
                name: "IX_CarModels_BrandID_Name",
                table: "CarModels",
                columns: new[] { "BrandID", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PhysicalContracts_BookingID",
                table: "PhysicalContracts",
                column: "BookingID");

            migrationBuilder.CreateIndex(
                name: "IX_PhysicalContracts_CarID",
                table: "PhysicalContracts",
                column: "CarID");

            migrationBuilder.CreateIndex(
                name: "IX_PhysicalContracts_OwnerID",
                table: "PhysicalContracts",
                column: "OwnerID");

            migrationBuilder.CreateIndex(
                name: "IX_PhysicalContracts_PhysicalContractTypeID",
                table: "PhysicalContracts",
                column: "PhysicalContractTypeID");

            migrationBuilder.CreateIndex(
                name: "IX_PhysicalContractTypes_Code",
                table: "PhysicalContractTypes",
                column: "Code",
                unique: true);

            migrationBuilder.Sql("""
IF NOT EXISTS (SELECT 1 FROM PhysicalContractTypes WHERE Code = 'SELF_DRIVE_RENTAL')
    INSERT INTO PhysicalContractTypes (Code, Name, Description, IsActive, CreatedAt) VALUES ('SELF_DRIVE_RENTAL', N'Hợp đồng thuê xe tự lái', N'Hợp đồng giấy ký trực tiếp cho từng đơn thuê xe tự lái.', 1, SYSUTCDATETIME());
IF NOT EXISTS (SELECT 1 FROM PhysicalContractTypes WHERE Code = 'CAR_OWNER_DOCUMENT')
    INSERT INTO PhysicalContractTypes (Code, Name, Description, IsActive, CreatedAt) VALUES ('CAR_OWNER_DOCUMENT', N'Hồ sơ xe/chủ xe', N'Giấy tờ xe, đăng kiểm, bảo hiểm hoặc hồ sơ chủ xe.', 1, SYSUTCDATETIME());
IF NOT EXISTS (SELECT 1 FROM PhysicalContractTypes WHERE Code = 'HANDOVER_RECORD')
    INSERT INTO PhysicalContractTypes (Code, Name, Description, IsActive, CreatedAt) VALUES ('HANDOVER_RECORD', N'Biên bản bàn giao', N'Ảnh/PDF biên bản giao nhận xe.', 1, SYSUTCDATETIME());
IF NOT EXISTS (SELECT 1 FROM PhysicalContractTypes WHERE Code = 'DEPOSIT_AGREEMENT')
    INSERT INTO PhysicalContractTypes (Code, Name, Description, IsActive, CreatedAt) VALUES ('DEPOSIT_AGREEMENT', N'Thỏa thuận đặt cọc', N'Chứng từ hoặc thỏa thuận đặt cọc thuê xe.', 1, SYSUTCDATETIME());
IF NOT EXISTS (SELECT 1 FROM PhysicalContractTypes WHERE Code = 'OTHER')
    INSERT INTO PhysicalContractTypes (Code, Name, Description, IsActive, CreatedAt) VALUES ('OTHER', N'Khác', N'Tài liệu khác cần lưu trữ.', 1, SYSUTCDATETIME());

DECLARE @amenities TABLE (Code nvarchar(60), Name nvarchar(120), Icon nvarchar(80), DisplayOrder int);
INSERT INTO @amenities (Code, Name, Icon, DisplayOrder) VALUES
('MAP', N'Bản đồ', 'bi-map', 1),
('BLUETOOTH', N'Bluetooth', 'bi-bluetooth', 2),
('CAMERA_360', N'Camera 360', 'bi-camera-video', 3),
('SIDE_CAMERA', N'Camera cập lề', 'bi-camera', 4),
('DASHCAM', N'Camera hành trình', 'bi-webcam', 5),
('REAR_CAMERA', N'Camera lùi', 'bi-camera-fill', 6),
('TPMS', N'Cảm biến lốp', 'bi-speedometer2', 7),
('COLLISION_WARNING', N'Cảnh báo va chạm', 'bi-shield-exclamation', 8),
('SPEED_WARNING', N'Cảnh báo tốc độ', 'bi-exclamation-triangle', 9),
('SUNROOF', N'Cửa sổ trời', 'bi-sun', 10),
('GPS', N'Định vị GPS', 'bi-geo-alt', 11),
('CHILD_SEAT', N'Ghế trẻ em', 'bi-person-arms-up', 12),
('USB', N'Khe cắm USB', 'bi-usb-drive', 13),
('SPARE_TIRE', N'Lốp dự phòng', 'bi-record-circle', 14),
('DVD', N'Màn hình DVD', 'bi-display', 15),
('TONNEAU_COVER', N'Nắp thùng xe bán tải', 'bi-truck', 16),
('ETC', N'ETC', 'bi-credit-card', 17),
('AIRBAGS', N'Túi khí an toàn', 'bi-shield-check', 18),
('CRUISE_CONTROL', N'Cruise Control', 'bi-speedometer', 19),
('TOUCH_SCREEN', N'Màn hình cảm ứng', 'bi-hand-index-thumb', 20),
('POWER_TRUNK', N'Cốp điện', 'bi-lightning-charge', 21),
('ELECTRIC_PARKING', N'Phanh tay điện tử', 'bi-toggles', 22);

INSERT INTO CarAmenities (Code, Name, Icon, DisplayOrder, IsActive)
SELECT a.Code, a.Name, a.Icon, a.DisplayOrder, 1
FROM @amenities a
WHERE NOT EXISTS (SELECT 1 FROM CarAmenities ca WHERE ca.Code = a.Code);

DECLARE @catalog TABLE (Brand nvarchar(100), Model nvarchar(120));
INSERT INTO @catalog (Brand, Model) VALUES
('Toyota','Vios'),('Toyota','Yaris'),('Toyota','Corolla Altis'),('Toyota','Corolla Cross'),('Toyota','Camry'),('Toyota','Raize'),('Toyota','Yaris Cross'),('Toyota','Innova'),('Toyota','Innova Cross'),('Toyota','Fortuner'),('Toyota','Land Cruiser'),('Toyota','Hilux'),('Toyota','Alphard'),('Toyota','Wigo'),
('Honda','City'),('Honda','Civic'),('Honda','Accord'),('Honda','CR-V'),('Honda','HR-V'),('Honda','BR-V'),('Honda','Jazz'),('Honda','Pilot'),
('Hyundai','Grand i10'),('Hyundai','Accent'),('Hyundai','Elantra'),('Hyundai','Tucson'),('Hyundai','Santa Fe'),('Hyundai','Creta'),('Hyundai','Stargazer'),('Hyundai','Venue'),('Hyundai','Palisade'),('Hyundai','Kona'),
('Kia','Morning'),('Kia','Soluto'),('Kia','K3'),('Kia','K5'),('Kia','Seltos'),('Kia','Sonet'),('Kia','Sportage'),('Kia','Sorento'),('Kia','Carnival'),('Kia','Carens'),
('Mazda','Mazda2'),('Mazda','Mazda3'),('Mazda','Mazda6'),('Mazda','CX-3'),('Mazda','CX-30'),('Mazda','CX-5'),('Mazda','CX-8'),('Mazda','BT-50'),
('Ford','Ranger'),('Ford','Ranger Raptor'),('Ford','Everest'),('Ford','Territory'),('Ford','Explorer'),('Ford','Transit'),('Ford','EcoSport'),('Ford','Focus'),
('Mitsubishi','Attrage'),('Mitsubishi','Xpander'),('Mitsubishi','Xpander Cross'),('Mitsubishi','Outlander'),('Mitsubishi','Triton'),('Mitsubishi','Pajero Sport'),('Mitsubishi','Xforce'),
('VinFast','Fadil'),('VinFast','Lux A2.0'),('VinFast','Lux SA2.0'),('VinFast','VF e34'),('VinFast','VF 3'),('VinFast','VF 5'),('VinFast','VF 6'),('VinFast','VF 7'),('VinFast','VF 8'),('VinFast','VF 9'),
('Mercedes-Benz','A-Class'),('Mercedes-Benz','C-Class'),('Mercedes-Benz','E-Class'),('Mercedes-Benz','S-Class'),('Mercedes-Benz','GLA'),('Mercedes-Benz','GLB'),('Mercedes-Benz','GLC'),('Mercedes-Benz','GLE'),('Mercedes-Benz','GLS'),('Mercedes-Benz','V-Class'),
('BMW','3 Series'),('BMW','5 Series'),('BMW','7 Series'),('BMW','X1'),('BMW','X3'),('BMW','X4'),('BMW','X5'),('BMW','X6'),('BMW','i4'),('BMW','iX'),
('Audi','A3'),('Audi','A4'),('Audi','A6'),('Audi','A8'),('Audi','Q2'),('Audi','Q3'),('Audi','Q5'),('Audi','Q7'),('Audi','Q8'),('Audi','e-tron'),
('Lexus','ES'),('Lexus','IS'),('Lexus','LS'),('Lexus','NX'),('Lexus','RX'),('Lexus','GX'),('Lexus','LX'),('Lexus','LM'),
('Nissan','Almera'),('Nissan','Sunny'),('Nissan','Navara'),('Nissan','Kicks'),('Nissan','X-Trail'),('Nissan','Terra'),('Nissan','Patrol'),
('Chevrolet','Spark'),('Chevrolet','Aveo'),('Chevrolet','Cruze'),('Chevrolet','Captiva'),('Chevrolet','Trailblazer'),('Chevrolet','Colorado'),('Chevrolet','Tahoe'),('Chevrolet','Suburban'),
('Peugeot','2008'),('Peugeot','3008'),('Peugeot','5008'),('Peugeot','408'),('Peugeot','Traveller'),
('Subaru','Impreza'),('Subaru','Legacy'),('Subaru','Forester'),('Subaru','Outback'),('Subaru','XV'),('Subaru','BRZ'),
('Volkswagen','Polo'),('Volkswagen','Golf'),('Volkswagen','Passat'),('Volkswagen','Tiguan'),('Volkswagen','Teramont'),('Volkswagen','Touareg'),
('Volvo','S60'),('Volvo','S90'),('Volvo','XC40'),('Volvo','XC60'),('Volvo','XC90'),
('Porsche','Macan'),('Porsche','Cayenne'),('Porsche','Panamera'),('Porsche','911'),('Porsche','Taycan'),
('Suzuki','Swift'),('Suzuki','Ciaz'),('Suzuki','Ertiga'),('Suzuki','XL7'),('Suzuki','Jimny'),('Suzuki','Carry'),
('Isuzu','D-Max'),('Isuzu','mu-X'),('Isuzu','QKR'),
('MG','MG5'),('MG','ZS'),('MG','HS'),('MG','RX5'),('MG','Marvel R'),
('BYD','Dolphin'),('BYD','Atto 3'),('BYD','Seal'),('BYD','Han'),('BYD','Tang'),
('Tesla','Model 3'),('Tesla','Model Y'),('Tesla','Model S'),('Tesla','Model X'),('Tesla','Cybertruck');

INSERT INTO CarBrands (Name)
SELECT DISTINCT c.Brand
FROM @catalog c
WHERE NOT EXISTS (SELECT 1 FROM CarBrands b WHERE b.Name = c.Brand);

INSERT INTO CarModels (BrandID, Name, FromYear, IsActive, CreatedAt)
SELECT b.BrandID, c.Model, 1990, 1, SYSUTCDATETIME()
FROM @catalog c
JOIN CarBrands b ON b.Name = c.Brand
WHERE NOT EXISTS (SELECT 1 FROM CarModels m WHERE m.BrandID = b.BrandID AND m.Name = c.Model);

UPDATE Users
SET Role = 'Customer',
    OriginalRole = NULL,
    IsPartnerApproved = 0,
    PartnerApprovedAt = NULL
WHERE Role IN ('Partner', 'Driver');

UPDATE Cars
SET WeeklyDiscountPercent = CASE WHEN WeeklyDiscountPercent = 0 THEN 5 ELSE WeeklyDiscountPercent END,
    DailyMileageLimitKm = CASE WHEN DailyMileageLimitKm = 0 THEN 400 ELSE DailyMileageLimitKm END,
    ExtraMileageFeePerKm = CASE WHEN ExtraMileageFeePerKm = 0 THEN 3000 ELSE ExtraMileageFeePerKm END,
    MaxDeliveryDistanceKm = CASE WHEN MaxDeliveryDistanceKm = 0 THEN 20 ELSE MaxDeliveryDistanceKm END,
    DeliveryFeePerKm = CASE WHEN DeliveryFeePerKm = 0 THEN 10000 ELSE DeliveryFeePerKm END;

IF NOT EXISTS (SELECT 1 FROM BookingStatuses WHERE Name = 'NEED_REVIEW')
    INSERT INTO BookingStatuses (Name) VALUES ('NEED_REVIEW');

DECLARE @NeedReviewStatusId int = (SELECT TOP 1 BookingStatusID FROM BookingStatuses WHERE Name = 'NEED_REVIEW');
UPDATE b
SET BookingStatusID = @NeedReviewStatusId,
    DriverID = NULL,
    AdminNotes = CONCAT(COALESCE(NULLIF(b.AdminNotes, ''), ''), CASE WHEN b.AdminNotes IS NULL OR b.AdminNotes = '' THEN '' ELSE CHAR(13) + CHAR(10) END,
        'NEED_REVIEW: Legacy driver/partner data was retired. Review this booking before further processing.')
FROM Bookings b
LEFT JOIN BookingStatuses bs ON bs.BookingStatusID = b.BookingStatusID
WHERE (b.DriverBookingID IS NOT NULL OR b.DriverID IS NOT NULL)
  AND (bs.Name IS NULL OR bs.Name NOT IN ('Completed', 'Cancelled', 'Rejected'));

UPDATE CartItems SET WithDriver = 0, DriverID = NULL WHERE WithDriver = 1 OR DriverID IS NOT NULL;
""");

            migrationBuilder.AddForeignKey(
                name: "FK_Cars_CarModels_CarModelID",
                table: "Cars",
                column: "CarModelID",
                principalTable: "CarModels",
                principalColumn: "CarModelID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Cars_CarModels_CarModelID",
                table: "Cars");

            migrationBuilder.DropTable(
                name: "CarAmenityMappings");

            migrationBuilder.DropTable(
                name: "CarModels");

            migrationBuilder.DropTable(
                name: "PhysicalContracts");

            migrationBuilder.DropTable(
                name: "CarAmenities");

            migrationBuilder.DropTable(
                name: "PhysicalContractTypes");

            migrationBuilder.DropIndex(
                name: "IX_Cars_CarModelID",
                table: "Cars");

            migrationBuilder.DropColumn(
                name: "CarModelID",
                table: "Cars");

            migrationBuilder.DropColumn(
                name: "DailyMileageLimitKm",
                table: "Cars");

            migrationBuilder.DropColumn(
                name: "DeliveryEnabled",
                table: "Cars");

            migrationBuilder.DropColumn(
                name: "DeliveryFeePerKm",
                table: "Cars");

            migrationBuilder.DropColumn(
                name: "ExtraMileageFeePerKm",
                table: "Cars");

            migrationBuilder.DropColumn(
                name: "FreeDeliveryRadiusKm",
                table: "Cars");

            migrationBuilder.DropColumn(
                name: "MaxDeliveryDistanceKm",
                table: "Cars");

            migrationBuilder.DropColumn(
                name: "MileageLimitEnabled",
                table: "Cars");

            migrationBuilder.DropColumn(
                name: "MonthlyDiscountPercent",
                table: "Cars");

            migrationBuilder.DropColumn(
                name: "RentalTerms",
                table: "Cars");

            migrationBuilder.DropColumn(
                name: "WeeklyDiscountPercent",
                table: "Cars");
        }
    }
}
