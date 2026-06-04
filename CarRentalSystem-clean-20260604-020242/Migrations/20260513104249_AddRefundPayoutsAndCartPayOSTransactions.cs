using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CarRentalSystem.Migrations
{
    /// <inheritdoc />
    public partial class AddRefundPayoutsAndCartPayOSTransactions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TransactionID",
                table: "PayOSTransactions",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "RefundPayouts",
                columns: table => new
                {
                    RefundPayoutID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RefundRequestID = table.Column<int>(type: "int", nullable: false),
                    ReferenceId = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    PayOSPayoutId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    Provider = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    ApprovalState = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    TransactionState = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    AttemptCount = table.Column<int>(type: "int", nullable: false),
                    LastError = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    EncryptedRecipientBin = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EncryptedRecipientAccountNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EncryptedRecipientAccountName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MaskedRecipientAccount = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    RawResponse = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FailedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RefundPayouts", x => x.RefundPayoutID);
                    table.ForeignKey(
                        name: "FK_RefundPayouts_RefundRequests_RefundRequestID",
                        column: x => x.RefundRequestID,
                        principalTable: "RefundRequests",
                        principalColumn: "RefundRequestID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PayOSTransactions_TransactionID",
                table: "PayOSTransactions",
                column: "TransactionID");

            migrationBuilder.CreateIndex(
                name: "IX_RefundPayouts_IdempotencyKey",
                table: "RefundPayouts",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RefundPayouts_ReferenceId",
                table: "RefundPayouts",
                column: "ReferenceId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RefundPayouts_RefundRequestID",
                table: "RefundPayouts",
                column: "RefundRequestID");

            migrationBuilder.AddForeignKey(
                name: "FK_PayOSTransactions_BookingTransactions_TransactionID",
                table: "PayOSTransactions",
                column: "TransactionID",
                principalTable: "BookingTransactions",
                principalColumn: "TransactionID",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PayOSTransactions_BookingTransactions_TransactionID",
                table: "PayOSTransactions");

            migrationBuilder.DropTable(
                name: "RefundPayouts");

            migrationBuilder.DropIndex(
                name: "IX_PayOSTransactions_TransactionID",
                table: "PayOSTransactions");

            migrationBuilder.DropColumn(
                name: "TransactionID",
                table: "PayOSTransactions");
        }
    }
}
