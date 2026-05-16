using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CarRentalSystem.Migrations
{
    /// <inheritdoc />
    public partial class SplitSelfDriveAndWithDriverServices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DrivingLicenseExpiryDate",
                table: "Drivers",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PartnerID",
                table: "Drivers",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "Drivers",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "Pending");

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "Drivers",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "BadgeExpiryDate",
                table: "Cars",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BadgeType",
                table: "Cars",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsApproved",
                table: "Cars",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsSelfDriveAllowed",
                table: "Cars",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsWithDriverAllowed",
                table: "Cars",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "ServiceTypeID",
                table: "Cars",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TransportPermitStatus",
                table: "Cars",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "NotRequired");

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "Cars",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContractTypeCode",
                table: "Bookings",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "SELF_DRIVE_CONTRACT");

            migrationBuilder.AddColumn<int>(
                name: "CustomerDrivingLicenseID",
                table: "Bookings",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DriverID",
                table: "Bookings",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ServiceTypeID",
                table: "Bookings",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "Bookings",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CustomerDrivingLicenses",
                columns: table => new
                {
                    CustomerDrivingLicenseID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserID = table.Column<int>(type: "int", nullable: false),
                    LicenseNumber = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    LicenseNumberHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    LicenseClass = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ExpiryDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FrontImageUrl = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    BackImageUrl = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    VerificationStatus = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    IsApproved = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerDrivingLicenses", x => x.CustomerDrivingLicenseID);
                    table.ForeignKey(
                        name: "FK_CustomerDrivingLicenses_Users_UserID",
                        column: x => x.UserID,
                        principalTable: "Users",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ServiceTypes",
                columns: table => new
                {
                    ServiceTypeID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceTypes", x => x.ServiceTypeID);
                });

            migrationBuilder.CreateTable(
                name: "TripTypes",
                columns: table => new
                {
                    TripTypeID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TripTypes", x => x.TripTypeID);
                });

            migrationBuilder.CreateTable(
                name: "RoutePrices",
                columns: table => new
                {
                    RoutePriceID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FromProvince = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    FromDistrict = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ToProvince = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ToDistrict = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    TripTypeID = table.Column<int>(type: "int", nullable: false),
                    CarTypeID = table.Column<int>(type: "int", nullable: false),
                    BasePrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    IncludedKm = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    ExtraKmPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    IncludedHours = table.Column<int>(type: "int", nullable: true),
                    ExtraHourPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    OvernightFee = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    WaitingFeePerHour = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TollFeeIncluded = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoutePrices", x => x.RoutePriceID);
                    table.ForeignKey(
                        name: "FK_RoutePrices_CarTypes_CarTypeID",
                        column: x => x.CarTypeID,
                        principalTable: "CarTypes",
                        principalColumn: "CarTypeID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RoutePrices_TripTypes_TripTypeID",
                        column: x => x.TripTypeID,
                        principalTable: "TripTypes",
                        principalColumn: "TripTypeID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Surcharges",
                columns: table => new
                {
                    SurchargeID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Percent = table.Column<decimal>(type: "decimal(5,2)", nullable: true),
                    ApplyToTripTypeID = table.Column<int>(type: "int", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Surcharges", x => x.SurchargeID);
                    table.ForeignKey(
                        name: "FK_Surcharges_TripTypes_ApplyToTripTypeID",
                        column: x => x.ApplyToTripTypeID,
                        principalTable: "TripTypes",
                        principalColumn: "TripTypeID",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "TripBookings",
                columns: table => new
                {
                    TripBookingID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BookingID = table.Column<int>(type: "int", nullable: false),
                    CustomerID = table.Column<int>(type: "int", nullable: false),
                    CarID = table.Column<int>(type: "int", nullable: true),
                    DriverID = table.Column<int>(type: "int", nullable: true),
                    TripTypeID = table.Column<int>(type: "int", nullable: false),
                    PickupLocation = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    PickupLatitude = table.Column<double>(type: "float", nullable: true),
                    PickupLongitude = table.Column<double>(type: "float", nullable: true),
                    DropoffLocation = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    DropoffLatitude = table.Column<double>(type: "float", nullable: true),
                    DropoffLongitude = table.Column<double>(type: "float", nullable: true),
                    PickupTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReturnTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EstimatedDistanceKm = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    EstimatedDurationMinutes = table.Column<int>(type: "int", nullable: false),
                    BasePrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    DistanceFee = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TimeFee = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    DriverFee = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TollFee = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    WaitingFee = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    OvernightFee = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ExtraKmFee = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    SurchargeFee = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TripBookings", x => x.TripBookingID);
                    table.ForeignKey(
                        name: "FK_TripBookings_Bookings_BookingID",
                        column: x => x.BookingID,
                        principalTable: "Bookings",
                        principalColumn: "BookingID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TripBookings_Cars_CarID",
                        column: x => x.CarID,
                        principalTable: "Cars",
                        principalColumn: "CarID",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_TripBookings_Drivers_DriverID",
                        column: x => x.DriverID,
                        principalTable: "Drivers",
                        principalColumn: "DriverID",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_TripBookings_TripTypes_TripTypeID",
                        column: x => x.TripTypeID,
                        principalTable: "TripTypes",
                        principalColumn: "TripTypeID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TripBookings_Users_CustomerID",
                        column: x => x.CustomerID,
                        principalTable: "Users",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TripStops",
                columns: table => new
                {
                    TripStopID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TripBookingID = table.Column<int>(type: "int", nullable: false),
                    StopOrder = table.Column<int>(type: "int", nullable: false),
                    Address = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Latitude = table.Column<double>(type: "float", nullable: true),
                    Longitude = table.Column<double>(type: "float", nullable: true),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TripStops", x => x.TripStopID);
                    table.ForeignKey(
                        name: "FK_TripStops_TripBookings_TripBookingID",
                        column: x => x.TripBookingID,
                        principalTable: "TripBookings",
                        principalColumn: "TripBookingID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Drivers_PartnerID",
                table: "Drivers",
                column: "PartnerID");

            migrationBuilder.CreateIndex(
                name: "IX_Cars_ServiceTypeID",
                table: "Cars",
                column: "ServiceTypeID");

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_CustomerDrivingLicenseID",
                table: "Bookings",
                column: "CustomerDrivingLicenseID");

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_DriverID",
                table: "Bookings",
                column: "DriverID");

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_ServiceTypeID",
                table: "Bookings",
                column: "ServiceTypeID");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerDrivingLicenses_UserID_LicenseNumberHash",
                table: "CustomerDrivingLicenses",
                columns: new[] { "UserID", "LicenseNumberHash" },
                filter: "[LicenseNumberHash] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_RoutePrices_CarTypeID",
                table: "RoutePrices",
                column: "CarTypeID");

            migrationBuilder.CreateIndex(
                name: "IX_RoutePrices_TripTypeID",
                table: "RoutePrices",
                column: "TripTypeID");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceTypes_Code",
                table: "ServiceTypes",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Surcharges_ApplyToTripTypeID",
                table: "Surcharges",
                column: "ApplyToTripTypeID");

            migrationBuilder.CreateIndex(
                name: "IX_Surcharges_Code",
                table: "Surcharges",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TripBookings_BookingID",
                table: "TripBookings",
                column: "BookingID");

            migrationBuilder.CreateIndex(
                name: "IX_TripBookings_CarID",
                table: "TripBookings",
                column: "CarID");

            migrationBuilder.CreateIndex(
                name: "IX_TripBookings_CustomerID",
                table: "TripBookings",
                column: "CustomerID");

            migrationBuilder.CreateIndex(
                name: "IX_TripBookings_DriverID",
                table: "TripBookings",
                column: "DriverID");

            migrationBuilder.CreateIndex(
                name: "IX_TripBookings_TripTypeID",
                table: "TripBookings",
                column: "TripTypeID");

            migrationBuilder.CreateIndex(
                name: "IX_TripStops_TripBookingID",
                table: "TripStops",
                column: "TripBookingID");

            migrationBuilder.CreateIndex(
                name: "IX_TripTypes_Code",
                table: "TripTypes",
                column: "Code",
                unique: true);

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM ServiceTypes WHERE Code = 'SELF_DRIVE')
    INSERT INTO ServiceTypes (Code, Name, Description, IsActive, CreatedAt)
    VALUES ('SELF_DRIVE', N'Thuê xe tự lái', N'Khách thuê xe và tự điều khiển xe.', 1, SYSUTCDATETIME());
IF NOT EXISTS (SELECT 1 FROM ServiceTypes WHERE Code = 'WITH_DRIVER')
    INSERT INTO ServiceTypes (Code, Name, Description, IsActive, CreatedAt)
    VALUES ('WITH_DRIVER', N'Xe có tài xế', N'Dịch vụ vận chuyển khách theo chuyến/lộ trình, có tài xế phục vụ.', 1, SYSUTCDATETIME());

IF NOT EXISTS (SELECT 1 FROM TripTypes WHERE Code = 'CITY_TRANSFER')
    INSERT INTO TripTypes (Code, Name, IsActive, CreatedAt) VALUES ('CITY_TRANSFER', N'Đưa đón nội thành', 1, SYSUTCDATETIME());
IF NOT EXISTS (SELECT 1 FROM TripTypes WHERE Code = 'AIRPORT_TRANSFER')
    INSERT INTO TripTypes (Code, Name, IsActive, CreatedAt) VALUES ('AIRPORT_TRANSFER', N'Đưa đón sân bay', 1, SYSUTCDATETIME());
IF NOT EXISTS (SELECT 1 FROM TripTypes WHERE Code = 'INTERCITY_ONE_WAY')
    INSERT INTO TripTypes (Code, Name, IsActive, CreatedAt) VALUES ('INTERCITY_ONE_WAY', N'Liên tỉnh một chiều', 1, SYSUTCDATETIME());
IF NOT EXISTS (SELECT 1 FROM TripTypes WHERE Code = 'INTERCITY_ROUND_TRIP')
    INSERT INTO TripTypes (Code, Name, IsActive, CreatedAt) VALUES ('INTERCITY_ROUND_TRIP', N'Liên tỉnh hai chiều', 1, SYSUTCDATETIME());
IF NOT EXISTS (SELECT 1 FROM TripTypes WHERE Code = 'HOURLY_RENTAL')
    INSERT INTO TripTypes (Code, Name, IsActive, CreatedAt) VALUES ('HOURLY_RENTAL', N'Thuê xe theo giờ', 1, SYSUTCDATETIME());
IF NOT EXISTS (SELECT 1 FROM TripTypes WHERE Code = 'DAILY_RENTAL')
    INSERT INTO TripTypes (Code, Name, IsActive, CreatedAt) VALUES ('DAILY_RENTAL', N'Thuê xe theo ngày', 1, SYSUTCDATETIME());

IF NOT EXISTS (SELECT 1 FROM Surcharges WHERE Code = 'NIGHT_FEE') INSERT INTO Surcharges (Code, Name, Amount, IsActive) VALUES ('NIGHT_FEE', N'Phụ phí ban đêm', 0, 1);
IF NOT EXISTS (SELECT 1 FROM Surcharges WHERE Code = 'HOLIDAY_FEE') INSERT INTO Surcharges (Code, Name, Amount, IsActive) VALUES ('HOLIDAY_FEE', N'Phụ phí ngày lễ', 0, 1);
IF NOT EXISTS (SELECT 1 FROM Surcharges WHERE Code = 'WAITING_FEE') INSERT INTO Surcharges (Code, Name, Amount, IsActive) VALUES ('WAITING_FEE', N'Phí chờ', 0, 1);
IF NOT EXISTS (SELECT 1 FROM Surcharges WHERE Code = 'TOLL_FEE') INSERT INTO Surcharges (Code, Name, Amount, IsActive) VALUES ('TOLL_FEE', N'Phí cầu đường/cao tốc', 0, 1);
IF NOT EXISTS (SELECT 1 FROM Surcharges WHERE Code = 'OVERNIGHT_FEE') INSERT INTO Surcharges (Code, Name, Amount, IsActive) VALUES ('OVERNIGHT_FEE', N'Phí lưu đêm tài xế', 0, 1);
IF NOT EXISTS (SELECT 1 FROM Surcharges WHERE Code = 'EXTRA_KM_FEE') INSERT INTO Surcharges (Code, Name, Amount, IsActive) VALUES ('EXTRA_KM_FEE', N'Phí vượt km', 0, 1);
IF NOT EXISTS (SELECT 1 FROM Surcharges WHERE Code = 'EXTRA_HOUR_FEE') INSERT INTO Surcharges (Code, Name, Amount, IsActive) VALUES ('EXTRA_HOUR_FEE', N'Phí vượt giờ', 0, 1);

IF NOT EXISTS (SELECT 1 FROM BookingStatuses WHERE Name = 'NEED_REVIEW')
    INSERT INTO BookingStatuses (Name) VALUES ('NEED_REVIEW');

DECLARE @SelfDriveId int = (SELECT TOP 1 ServiceTypeID FROM ServiceTypes WHERE Code = 'SELF_DRIVE');
DECLARE @NeedReviewStatusId int = (SELECT TOP 1 BookingStatusID FROM BookingStatuses WHERE Name = 'NEED_REVIEW');

UPDATE Cars
SET ServiceTypeID = @SelfDriveId,
    IsSelfDriveAllowed = 1,
    IsWithDriverAllowed = 0,
    TransportPermitStatus = 'NotRequired',
    IsApproved = 1
WHERE ServiceTypeID IS NULL;

INSERT INTO CustomerDrivingLicenses
    (UserID, LicenseNumber, LicenseNumberHash, LicenseClass, FrontImageUrl, BackImageUrl, VerificationStatus, IsApproved, IsActive, CreatedAt)
SELECT u.UserID, u.DrivingLicenseNumber, u.DrivingLicenseHash, 'B2', u.DrivingLicenseUrl, u.DrivingLicenseBackUrl,
       u.VerificationStatus,
       CASE WHEN u.IsVerified = 1 AND u.VerificationStatus = 'Verified' THEN 1 ELSE 0 END,
       1,
       SYSUTCDATETIME()
FROM Users u
WHERE u.DrivingLicenseHash IS NOT NULL
  AND NOT EXISTS (
      SELECT 1 FROM CustomerDrivingLicenses l
      WHERE l.UserID = u.UserID AND l.LicenseNumberHash = u.DrivingLicenseHash
  );

UPDATE b
SET ServiceTypeID = @SelfDriveId,
    ContractTypeCode = 'SELF_DRIVE_CONTRACT',
    CustomerDrivingLicenseID = license.CustomerDrivingLicenseID
FROM Bookings b
OUTER APPLY (
    SELECT TOP 1 l.CustomerDrivingLicenseID
    FROM CustomerDrivingLicenses l
    WHERE l.UserID = b.UserID AND l.IsActive = 1
    ORDER BY ISNULL(l.UpdatedAt, l.CreatedAt) DESC
) license
WHERE b.ServiceTypeID IS NULL;

UPDATE CartItems
SET WithDriver = 0,
    DriverID = NULL
WHERE WithDriver = 1 OR DriverID IS NOT NULL;

UPDATE b
SET BookingStatusID = @NeedReviewStatusId,
    DriverID = NULL,
    AdminNotes = CONCAT(COALESCE(NULLIF(b.AdminNotes, ''), ''), CASE WHEN b.AdminNotes IS NULL OR b.AdminNotes = '' THEN '' ELSE CHAR(13) + CHAR(10) END,
        'NEED_REVIEW: Đơn thuê xe tự lái cũ có dữ liệu tài xế/DriverBooking, cần admin rà soát vì nghiệp vụ mới không cho self-drive kèm tài xế.')
FROM Bookings b
LEFT JOIN BookingStatuses bs ON bs.BookingStatusID = b.BookingStatusID
WHERE (b.DriverBookingID IS NOT NULL OR b.DriverID IS NOT NULL)
  AND (bs.Name IS NULL OR bs.Name NOT IN ('Completed', 'Cancelled', 'Rejected'));
");

            migrationBuilder.AddForeignKey(
                name: "FK_Bookings_CustomerDrivingLicenses_CustomerDrivingLicenseID",
                table: "Bookings",
                column: "CustomerDrivingLicenseID",
                principalTable: "CustomerDrivingLicenses",
                principalColumn: "CustomerDrivingLicenseID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Bookings_Drivers_DriverID",
                table: "Bookings",
                column: "DriverID",
                principalTable: "Drivers",
                principalColumn: "DriverID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Bookings_ServiceTypes_ServiceTypeID",
                table: "Bookings",
                column: "ServiceTypeID",
                principalTable: "ServiceTypes",
                principalColumn: "ServiceTypeID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Cars_ServiceTypes_ServiceTypeID",
                table: "Cars",
                column: "ServiceTypeID",
                principalTable: "ServiceTypes",
                principalColumn: "ServiceTypeID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Drivers_Users_PartnerID",
                table: "Drivers",
                column: "PartnerID",
                principalTable: "Users",
                principalColumn: "UserID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Bookings_CustomerDrivingLicenses_CustomerDrivingLicenseID",
                table: "Bookings");

            migrationBuilder.DropForeignKey(
                name: "FK_Bookings_Drivers_DriverID",
                table: "Bookings");

            migrationBuilder.DropForeignKey(
                name: "FK_Bookings_ServiceTypes_ServiceTypeID",
                table: "Bookings");

            migrationBuilder.DropForeignKey(
                name: "FK_Cars_ServiceTypes_ServiceTypeID",
                table: "Cars");

            migrationBuilder.DropForeignKey(
                name: "FK_Drivers_Users_PartnerID",
                table: "Drivers");

            migrationBuilder.DropTable(
                name: "CustomerDrivingLicenses");

            migrationBuilder.DropTable(
                name: "RoutePrices");

            migrationBuilder.DropTable(
                name: "ServiceTypes");

            migrationBuilder.DropTable(
                name: "Surcharges");

            migrationBuilder.DropTable(
                name: "TripStops");

            migrationBuilder.DropTable(
                name: "TripBookings");

            migrationBuilder.DropTable(
                name: "TripTypes");

            migrationBuilder.DropIndex(
                name: "IX_Drivers_PartnerID",
                table: "Drivers");

            migrationBuilder.DropIndex(
                name: "IX_Cars_ServiceTypeID",
                table: "Cars");

            migrationBuilder.DropIndex(
                name: "IX_Bookings_CustomerDrivingLicenseID",
                table: "Bookings");

            migrationBuilder.DropIndex(
                name: "IX_Bookings_DriverID",
                table: "Bookings");

            migrationBuilder.DropIndex(
                name: "IX_Bookings_ServiceTypeID",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "DrivingLicenseExpiryDate",
                table: "Drivers");

            migrationBuilder.DropColumn(
                name: "PartnerID",
                table: "Drivers");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Drivers");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "Drivers");

            migrationBuilder.DropColumn(
                name: "BadgeExpiryDate",
                table: "Cars");

            migrationBuilder.DropColumn(
                name: "BadgeType",
                table: "Cars");

            migrationBuilder.DropColumn(
                name: "IsApproved",
                table: "Cars");

            migrationBuilder.DropColumn(
                name: "IsSelfDriveAllowed",
                table: "Cars");

            migrationBuilder.DropColumn(
                name: "IsWithDriverAllowed",
                table: "Cars");

            migrationBuilder.DropColumn(
                name: "ServiceTypeID",
                table: "Cars");

            migrationBuilder.DropColumn(
                name: "TransportPermitStatus",
                table: "Cars");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "Cars");

            migrationBuilder.DropColumn(
                name: "ContractTypeCode",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "CustomerDrivingLicenseID",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "DriverID",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "ServiceTypeID",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "Bookings");
        }
    }
}
