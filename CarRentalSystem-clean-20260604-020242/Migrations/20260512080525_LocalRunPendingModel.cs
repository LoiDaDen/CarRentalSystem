using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CarRentalSystem.Migrations
{
    /// <inheritdoc />
    public partial class LocalRunPendingModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF EXISTS (
                    SELECT 1
                    FROM sys.indexes
                    WHERE name = N'IX_PayOSTransactions_OrderCode'
                      AND object_id = OBJECT_ID(N'[dbo].[PayOSTransactions]')
                )
                DROP INDEX [IX_PayOSTransactions_OrderCode] ON [dbo].[PayOSTransactions];");

            migrationBuilder.CreateTable(
                name: "BookingHolds",
                columns: table => new
                {
                    BookingHoldID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserID = table.Column<int>(type: "int", nullable: false),
                    CarID = table.Column<int>(type: "int", nullable: false),
                    BookingID = table.Column<int>(type: "int", nullable: true),
                    TransactionID = table.Column<int>(type: "int", nullable: true),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    QuoteSnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReleasedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ConvertedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BookingHolds", x => x.BookingHoldID);
                    table.ForeignKey(
                        name: "FK_BookingHolds_BookingTransactions_TransactionID",
                        column: x => x.TransactionID,
                        principalTable: "BookingTransactions",
                        principalColumn: "TransactionID",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_BookingHolds_Bookings_BookingID",
                        column: x => x.BookingID,
                        principalTable: "Bookings",
                        principalColumn: "BookingID",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_BookingHolds_Cars_CarID",
                        column: x => x.CarID,
                        principalTable: "Cars",
                        principalColumn: "CarID");
                    table.ForeignKey(
                        name: "FK_BookingHolds_Users_UserID",
                        column: x => x.UserID,
                        principalTable: "Users",
                        principalColumn: "UserID");
                });

            migrationBuilder.CreateTable(
                name: "PaymentLedgerEntries",
                columns: table => new
                {
                    PaymentLedgerEntryID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BookingID = table.Column<int>(type: "int", nullable: true),
                    TransactionID = table.Column<int>(type: "int", nullable: true),
                    BookingTransactionTransactionID = table.Column<int>(type: "int", nullable: true),
                    PaymentID = table.Column<int>(type: "int", nullable: true),
                    Provider = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    OrderCode = table.Column<long>(type: "bigint", nullable: true),
                    EntryType = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PostedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentLedgerEntries", x => x.PaymentLedgerEntryID);
                    table.ForeignKey(
                        name: "FK_PaymentLedgerEntries_BookingTransactions_BookingTransactionTransactionID",
                        column: x => x.BookingTransactionTransactionID,
                        principalTable: "BookingTransactions",
                        principalColumn: "TransactionID");
                    table.ForeignKey(
                        name: "FK_PaymentLedgerEntries_Bookings_BookingID",
                        column: x => x.BookingID,
                        principalTable: "Bookings",
                        principalColumn: "BookingID");
                    table.ForeignKey(
                        name: "FK_PaymentLedgerEntries_Payments_PaymentID",
                        column: x => x.PaymentID,
                        principalTable: "Payments",
                        principalColumn: "PaymentID");
                });

            migrationBuilder.CreateTable(
                name: "PaymentWebhookEvents",
                columns: table => new
                {
                    PaymentWebhookEventID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Provider = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    EventKey = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    PayloadHash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    OrderCode = table.Column<long>(type: "bigint", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    RawPayload = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Error = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ReceivedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ProcessedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentWebhookEvents", x => x.PaymentWebhookEventID);
                });

            migrationBuilder.CreateTable(
                name: "RefundRequests",
                columns: table => new
                {
                    RefundRequestID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BookingID = table.Column<int>(type: "int", nullable: false),
                    PaymentLedgerEntryID = table.Column<int>(type: "int", nullable: true),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ApprovedByUserID = table.Column<int>(type: "int", nullable: true),
                    PaidByUserID = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PaidAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RefundRequests", x => x.RefundRequestID);
                    table.ForeignKey(
                        name: "FK_RefundRequests_Bookings_BookingID",
                        column: x => x.BookingID,
                        principalTable: "Bookings",
                        principalColumn: "BookingID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RefundRequests_PaymentLedgerEntries_PaymentLedgerEntryID",
                        column: x => x.PaymentLedgerEntryID,
                        principalTable: "PaymentLedgerEntries",
                        principalColumn: "PaymentLedgerEntryID");
                    table.ForeignKey(
                        name: "FK_RefundRequests_Users_ApprovedByUserID",
                        column: x => x.ApprovedByUserID,
                        principalTable: "Users",
                        principalColumn: "UserID");
                    table.ForeignKey(
                        name: "FK_RefundRequests_Users_PaidByUserID",
                        column: x => x.PaidByUserID,
                        principalTable: "Users",
                        principalColumn: "UserID");
                });

            migrationBuilder.Sql(@"
                WITH DuplicateOrderCodes AS (
                    SELECT
                        [PayOSTransactionID],
                        ROW_NUMBER() OVER (
                            PARTITION BY [OrderCode]
                            ORDER BY [PayOSTransactionID]
                        ) AS [RowNumber]
                    FROM [dbo].[PayOSTransactions]
                )
                UPDATE [PayOSTransactions]
                SET [OrderCode] = 900000000000000 + [PayOSTransactions].[PayOSTransactionID]
                FROM [dbo].[PayOSTransactions]
                INNER JOIN DuplicateOrderCodes
                    ON [PayOSTransactions].[PayOSTransactionID] = DuplicateOrderCodes.[PayOSTransactionID]
                WHERE DuplicateOrderCodes.[RowNumber] > 1;");

            migrationBuilder.CreateIndex(
                name: "IX_PayOSTransactions_OrderCode",
                table: "PayOSTransactions",
                column: "OrderCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_CarID_StartDate_EndDate_BookingStatusID",
                table: "Bookings",
                columns: new[] { "CarID", "StartDate", "EndDate", "BookingStatusID" });

            migrationBuilder.CreateIndex(
                name: "IX_BookingHolds_BookingID",
                table: "BookingHolds",
                column: "BookingID");

            migrationBuilder.CreateIndex(
                name: "IX_BookingHolds_CarID_StartDate_EndDate_Status",
                table: "BookingHolds",
                columns: new[] { "CarID", "StartDate", "EndDate", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_BookingHolds_ExpiresAt",
                table: "BookingHolds",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_BookingHolds_TransactionID",
                table: "BookingHolds",
                column: "TransactionID");

            migrationBuilder.CreateIndex(
                name: "IX_BookingHolds_UserID",
                table: "BookingHolds",
                column: "UserID");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentLedgerEntries_BookingID_EntryType_Status",
                table: "PaymentLedgerEntries",
                columns: new[] { "BookingID", "EntryType", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentLedgerEntries_BookingTransactionTransactionID",
                table: "PaymentLedgerEntries",
                column: "BookingTransactionTransactionID");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentLedgerEntries_IdempotencyKey",
                table: "PaymentLedgerEntries",
                column: "IdempotencyKey",
                unique: true,
                filter: "[IdempotencyKey] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentLedgerEntries_PaymentID",
                table: "PaymentLedgerEntries",
                column: "PaymentID");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentWebhookEvents_Provider_EventKey",
                table: "PaymentWebhookEvents",
                columns: new[] { "Provider", "EventKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RefundRequests_ApprovedByUserID",
                table: "RefundRequests",
                column: "ApprovedByUserID");

            migrationBuilder.CreateIndex(
                name: "IX_RefundRequests_BookingID_Status",
                table: "RefundRequests",
                columns: new[] { "BookingID", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_RefundRequests_PaidByUserID",
                table: "RefundRequests",
                column: "PaidByUserID");

            migrationBuilder.CreateIndex(
                name: "IX_RefundRequests_PaymentLedgerEntryID",
                table: "RefundRequests",
                column: "PaymentLedgerEntryID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BookingHolds");

            migrationBuilder.DropTable(
                name: "PaymentWebhookEvents");

            migrationBuilder.DropTable(
                name: "RefundRequests");

            migrationBuilder.DropTable(
                name: "PaymentLedgerEntries");

            migrationBuilder.DropIndex(
                name: "IX_PayOSTransactions_OrderCode",
                table: "PayOSTransactions");

            migrationBuilder.DropIndex(
                name: "IX_Bookings_CarID_StartDate_EndDate_BookingStatusID",
                table: "Bookings");

            migrationBuilder.CreateIndex(
                name: "IX_PayOSTransactions_OrderCode",
                table: "PayOSTransactions",
                column: "OrderCode");
        }
    }
}
