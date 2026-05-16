using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CarRentalSystem.Migrations
{
    /// <inheritdoc />
    public partial class EnterprisePhase2OperatingModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AuditLogs_UserId",
                table: "AuditLogs");

            migrationBuilder.AddColumn<int>(
                name: "ContractVersion",
                table: "RentalContracts",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "RenderedContentHash",
                table: "RentalContracts",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SignedAt",
                table: "RentalContracts",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SignedIpAddress",
                table: "RentalContracts",
                type: "nvarchar(45)",
                maxLength: 45,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SignedUserAgent",
                table: "RentalContracts",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SignerIdentitySnapshotJson",
                table: "RentalContracts",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ContractVersion",
                table: "PhysicalContracts",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "FileContentHash",
                table: "PhysicalContracts",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UploadedIpAddress",
                table: "PhysicalContracts",
                type: "nvarchar(45)",
                maxLength: 45,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UploadedUserAgent",
                table: "PhysicalContracts",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CustomerSignatureHash",
                table: "HandoverRecords",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EvidenceHash",
                table: "HandoverRecords",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "GeoLatitude",
                table: "HandoverRecords",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "GeoLongitude",
                table: "HandoverRecords",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RecordedByUserId",
                table: "HandoverRecords",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ContractVersion",
                table: "DigitalContracts",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "RenderedContentHash",
                table: "DigitalContracts",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SignedAt",
                table: "DigitalContracts",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SignedIpAddress",
                table: "DigitalContracts",
                type: "nvarchar(45)",
                maxLength: 45,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SignedUserAgent",
                table: "DigitalContracts",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SignerIdentitySnapshotJson",
                table: "DigitalContracts",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CorrelationId",
                table: "AuditLogs",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "HashVersion",
                table: "AuditLogs",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "PreviousHash",
                table: "AuditLogs",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Reason",
                table: "AuditLogs",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RecordHash",
                table: "AuditLogs",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SettlementReviews",
                columns: table => new
                {
                    SettlementReviewID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BookingID = table.Column<int>(type: "int", nullable: false),
                    HandoverID = table.Column<int>(type: "int", nullable: true),
                    ReviewType = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    RequestedByUserID = table.Column<int>(type: "int", nullable: true),
                    ReviewedByUserID = table.Column<int>(type: "int", nullable: true),
                    PaymentLedgerEntryID = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReviewedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SettlementReviews", x => x.SettlementReviewID);
                    table.ForeignKey(
                        name: "FK_SettlementReviews_Bookings_BookingID",
                        column: x => x.BookingID,
                        principalTable: "Bookings",
                        principalColumn: "BookingID");
                    table.ForeignKey(
                        name: "FK_SettlementReviews_HandoverRecords_HandoverID",
                        column: x => x.HandoverID,
                        principalTable: "HandoverRecords",
                        principalColumn: "HandoverID",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_SettlementReviews_PaymentLedgerEntries_PaymentLedgerEntryID",
                        column: x => x.PaymentLedgerEntryID,
                        principalTable: "PaymentLedgerEntries",
                        principalColumn: "PaymentLedgerEntryID");
                    table.ForeignKey(
                        name: "FK_SettlementReviews_Users_RequestedByUserID",
                        column: x => x.RequestedByUserID,
                        principalTable: "Users",
                        principalColumn: "UserID");
                    table.ForeignKey(
                        name: "FK_SettlementReviews_Users_ReviewedByUserID",
                        column: x => x.ReviewedByUserID,
                        principalTable: "Users",
                        principalColumn: "UserID");
                });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentWebhookEvents_OrderCode_Status",
                table: "PaymentWebhookEvents",
                columns: new[] { "OrderCode", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Payments_BookingID_PaymentDate",
                table: "Payments",
                columns: new[] { "BookingID", "PaymentDate" });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentLedgerEntries_BookingID_EntryType_CreatedAt",
                table: "PaymentLedgerEntries",
                columns: new[] { "BookingID", "EntryType", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_HandoverRecords_RecordedByUserId",
                table: "HandoverRecords",
                column: "RecordedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_CreatedAt_BookingStatusID",
                table: "Bookings",
                columns: new[] { "CreatedAt", "BookingStatusID" });

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_UserID_CreatedAt",
                table: "Bookings",
                columns: new[] { "UserID", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_Timestamp_EntityType_Action",
                table: "AuditLogs",
                columns: new[] { "Timestamp", "EntityType", "Action" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_UserId_Timestamp",
                table: "AuditLogs",
                columns: new[] { "UserId", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_SettlementReviews_BookingID_Status_CreatedAt",
                table: "SettlementReviews",
                columns: new[] { "BookingID", "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SettlementReviews_HandoverID",
                table: "SettlementReviews",
                column: "HandoverID");

            migrationBuilder.CreateIndex(
                name: "IX_SettlementReviews_PaymentLedgerEntryID",
                table: "SettlementReviews",
                column: "PaymentLedgerEntryID");

            migrationBuilder.CreateIndex(
                name: "IX_SettlementReviews_RequestedByUserID",
                table: "SettlementReviews",
                column: "RequestedByUserID");

            migrationBuilder.CreateIndex(
                name: "IX_SettlementReviews_ReviewedByUserID",
                table: "SettlementReviews",
                column: "ReviewedByUserID");

            migrationBuilder.AddForeignKey(
                name: "FK_HandoverRecords_Users_RecordedByUserId",
                table: "HandoverRecords",
                column: "RecordedByUserId",
                principalTable: "Users",
                principalColumn: "UserID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_HandoverRecords_Users_RecordedByUserId",
                table: "HandoverRecords");

            migrationBuilder.DropTable(
                name: "SettlementReviews");

            migrationBuilder.DropIndex(
                name: "IX_PaymentWebhookEvents_OrderCode_Status",
                table: "PaymentWebhookEvents");

            migrationBuilder.DropIndex(
                name: "IX_Payments_BookingID_PaymentDate",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_PaymentLedgerEntries_BookingID_EntryType_CreatedAt",
                table: "PaymentLedgerEntries");

            migrationBuilder.DropIndex(
                name: "IX_HandoverRecords_RecordedByUserId",
                table: "HandoverRecords");

            migrationBuilder.DropIndex(
                name: "IX_Bookings_CreatedAt_BookingStatusID",
                table: "Bookings");

            migrationBuilder.DropIndex(
                name: "IX_Bookings_UserID_CreatedAt",
                table: "Bookings");

            migrationBuilder.DropIndex(
                name: "IX_AuditLogs_Timestamp_EntityType_Action",
                table: "AuditLogs");

            migrationBuilder.DropIndex(
                name: "IX_AuditLogs_UserId_Timestamp",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "ContractVersion",
                table: "RentalContracts");

            migrationBuilder.DropColumn(
                name: "RenderedContentHash",
                table: "RentalContracts");

            migrationBuilder.DropColumn(
                name: "SignedAt",
                table: "RentalContracts");

            migrationBuilder.DropColumn(
                name: "SignedIpAddress",
                table: "RentalContracts");

            migrationBuilder.DropColumn(
                name: "SignedUserAgent",
                table: "RentalContracts");

            migrationBuilder.DropColumn(
                name: "SignerIdentitySnapshotJson",
                table: "RentalContracts");

            migrationBuilder.DropColumn(
                name: "ContractVersion",
                table: "PhysicalContracts");

            migrationBuilder.DropColumn(
                name: "FileContentHash",
                table: "PhysicalContracts");

            migrationBuilder.DropColumn(
                name: "UploadedIpAddress",
                table: "PhysicalContracts");

            migrationBuilder.DropColumn(
                name: "UploadedUserAgent",
                table: "PhysicalContracts");

            migrationBuilder.DropColumn(
                name: "CustomerSignatureHash",
                table: "HandoverRecords");

            migrationBuilder.DropColumn(
                name: "EvidenceHash",
                table: "HandoverRecords");

            migrationBuilder.DropColumn(
                name: "GeoLatitude",
                table: "HandoverRecords");

            migrationBuilder.DropColumn(
                name: "GeoLongitude",
                table: "HandoverRecords");

            migrationBuilder.DropColumn(
                name: "RecordedByUserId",
                table: "HandoverRecords");

            migrationBuilder.DropColumn(
                name: "ContractVersion",
                table: "DigitalContracts");

            migrationBuilder.DropColumn(
                name: "RenderedContentHash",
                table: "DigitalContracts");

            migrationBuilder.DropColumn(
                name: "SignedAt",
                table: "DigitalContracts");

            migrationBuilder.DropColumn(
                name: "SignedIpAddress",
                table: "DigitalContracts");

            migrationBuilder.DropColumn(
                name: "SignedUserAgent",
                table: "DigitalContracts");

            migrationBuilder.DropColumn(
                name: "SignerIdentitySnapshotJson",
                table: "DigitalContracts");

            migrationBuilder.DropColumn(
                name: "CorrelationId",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "HashVersion",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "PreviousHash",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "Reason",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "RecordHash",
                table: "AuditLogs");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_UserId",
                table: "AuditLogs",
                column: "UserId");
        }
    }
}
