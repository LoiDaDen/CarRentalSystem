using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CarRentalSystem.Migrations
{
    /// <inheritdoc />
    public partial class AddHandoverInventoryLostFound : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CarInventoryItems",
                columns: table => new
                {
                    CarInventoryItemID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CarID = table.Column<int>(type: "int", nullable: false),
                    ItemName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    UnitReplacementCost = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    IsRequired = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CarInventoryItems", x => x.CarInventoryItemID);
                    table.ForeignKey(
                        name: "FK_CarInventoryItems_Cars_CarID",
                        column: x => x.CarID,
                        principalTable: "Cars",
                        principalColumn: "CarID");
                });

            migrationBuilder.CreateTable(
                name: "LostFoundItems",
                columns: table => new
                {
                    LostFoundItemID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BookingID = table.Column<int>(type: "int", nullable: false),
                    HandoverID = table.Column<int>(type: "int", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    LocationDescription = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    PhotoUrl = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ReportedByUserID = table.Column<int>(type: "int", nullable: true),
                    ResolvedByUserID = table.Column<int>(type: "int", nullable: true),
                    FoundAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CustomerNotifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ResolvedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ResolutionNote = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LostFoundItems", x => x.LostFoundItemID);
                    table.ForeignKey(
                        name: "FK_LostFoundItems_Bookings_BookingID",
                        column: x => x.BookingID,
                        principalTable: "Bookings",
                        principalColumn: "BookingID");
                    table.ForeignKey(
                        name: "FK_LostFoundItems_HandoverRecords_HandoverID",
                        column: x => x.HandoverID,
                        principalTable: "HandoverRecords",
                        principalColumn: "HandoverID",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_LostFoundItems_Users_ReportedByUserID",
                        column: x => x.ReportedByUserID,
                        principalTable: "Users",
                        principalColumn: "UserID");
                    table.ForeignKey(
                        name: "FK_LostFoundItems_Users_ResolvedByUserID",
                        column: x => x.ResolvedByUserID,
                        principalTable: "Users",
                        principalColumn: "UserID");
                });

            migrationBuilder.CreateTable(
                name: "HandoverInventoryItems",
                columns: table => new
                {
                    HandoverInventoryItemID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    HandoverID = table.Column<int>(type: "int", nullable: false),
                    CarInventoryItemID = table.Column<int>(type: "int", nullable: true),
                    ItemName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    ExpectedQuantity = table.Column<int>(type: "int", nullable: false),
                    ActualQuantity = table.Column<int>(type: "int", nullable: false),
                    MissingQuantity = table.Column<int>(type: "int", nullable: false),
                    UnitReplacementCost = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    MissingCharge = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HandoverInventoryItems", x => x.HandoverInventoryItemID);
                    table.ForeignKey(
                        name: "FK_HandoverInventoryItems_CarInventoryItems_CarInventoryItemID",
                        column: x => x.CarInventoryItemID,
                        principalTable: "CarInventoryItems",
                        principalColumn: "CarInventoryItemID",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_HandoverInventoryItems_HandoverRecords_HandoverID",
                        column: x => x.HandoverID,
                        principalTable: "HandoverRecords",
                        principalColumn: "HandoverID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CarInventoryItems_CarID_IsActive_IsDeleted",
                table: "CarInventoryItems",
                columns: new[] { "CarID", "IsActive", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_HandoverInventoryItems_CarInventoryItemID",
                table: "HandoverInventoryItems",
                column: "CarInventoryItemID");

            migrationBuilder.CreateIndex(
                name: "IX_HandoverInventoryItems_HandoverID_MissingQuantity",
                table: "HandoverInventoryItems",
                columns: new[] { "HandoverID", "MissingQuantity" });

            migrationBuilder.CreateIndex(
                name: "IX_LostFoundItems_BookingID_Status_FoundAt",
                table: "LostFoundItems",
                columns: new[] { "BookingID", "Status", "FoundAt" });

            migrationBuilder.CreateIndex(
                name: "IX_LostFoundItems_HandoverID",
                table: "LostFoundItems",
                column: "HandoverID");

            migrationBuilder.CreateIndex(
                name: "IX_LostFoundItems_ReportedByUserID",
                table: "LostFoundItems",
                column: "ReportedByUserID");

            migrationBuilder.CreateIndex(
                name: "IX_LostFoundItems_ResolvedByUserID",
                table: "LostFoundItems",
                column: "ResolvedByUserID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HandoverInventoryItems");

            migrationBuilder.DropTable(
                name: "LostFoundItems");

            migrationBuilder.DropTable(
                name: "CarInventoryItems");
        }
    }
}
