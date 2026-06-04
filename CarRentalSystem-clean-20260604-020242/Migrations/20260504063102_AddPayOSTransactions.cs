using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CarRentalSystem.Migrations
{
    /// <inheritdoc />
    public partial class AddPayOSTransactions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PayOSTransactions",
                columns: table => new
                {
                    PayOSTransactionID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BookingID = table.Column<int>(type: "int", nullable: false),
                    PaymentID = table.Column<int>(type: "int", nullable: true),
                    OrderCode = table.Column<long>(type: "bigint", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    CheckoutUrl = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    PayOSCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    PayOSDescription = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    WebhookPayload = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PaidAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CancelledAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FailedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PayOSTransactions", x => x.PayOSTransactionID);
                    table.ForeignKey(
                        name: "FK_PayOSTransactions_Bookings_BookingID",
                        column: x => x.BookingID,
                        principalTable: "Bookings",
                        principalColumn: "BookingID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PayOSTransactions_Payments_PaymentID",
                        column: x => x.PaymentID,
                        principalTable: "Payments",
                        principalColumn: "PaymentID");
                });

            migrationBuilder.CreateIndex(
                name: "IX_PayOSTransactions_BookingID",
                table: "PayOSTransactions",
                column: "BookingID");

            migrationBuilder.CreateIndex(
                name: "IX_PayOSTransactions_OrderCode",
                table: "PayOSTransactions",
                column: "OrderCode");

            migrationBuilder.CreateIndex(
                name: "IX_PayOSTransactions_PaymentID",
                table: "PayOSTransactions",
                column: "PaymentID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PayOSTransactions");
        }
    }
}
