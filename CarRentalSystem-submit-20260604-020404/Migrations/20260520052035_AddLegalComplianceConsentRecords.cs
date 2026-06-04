using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CarRentalSystem.Migrations
{
    /// <inheritdoc />
    public partial class AddLegalComplianceConsentRecords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PaymentLedgerEntries_BookingID_EntryType_Status",
                table: "PaymentLedgerEntries");

            migrationBuilder.AddColumn<int>(
                name: "CompletedByUserID",
                table: "PrivacyRequests",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DueAt",
                table: "PrivacyRequests",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "DATEADD(day, 3, SYSUTCDATETIME())");

            migrationBuilder.AddColumn<string>(
                name: "IdentityVerificationStatus",
                table: "PrivacyRequests",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "AuthenticatedSession");

            migrationBuilder.AddColumn<string>(
                name: "RequestScope",
                table: "PrivacyRequests",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "Account");

            migrationBuilder.AddColumn<string>(
                name: "SecureExportPath",
                table: "PrivacyRequests",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PrivacyNoticeVersionID",
                table: "Bookings",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SelfDriveAgreementAcceptedAt",
                table: "Bookings",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SelfDriveAgreementIpAddress",
                table: "Bookings",
                type: "nvarchar(45)",
                maxLength: 45,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SelfDriveAgreementUserAgent",
                table: "Bookings",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TermsDocumentVersionID",
                table: "Bookings",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "LegalDocumentVersions",
                columns: table => new
                {
                    LegalDocumentVersionID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Version = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ContentHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    PublishedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EffectiveAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RetiredAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    SourceUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LegalDocumentVersions", x => x.LegalDocumentVersionID);
                });

            migrationBuilder.CreateTable(
                name: "ConsentRecords",
                columns: table => new
                {
                    ConsentRecordID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserID = table.Column<int>(type: "int", nullable: false),
                    LegalDocumentVersionID = table.Column<int>(type: "int", nullable: false),
                    PurposeCode = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    IsGranted = table.Column<bool>(type: "bit", nullable: false),
                    GrantedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    WithdrawnAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Source = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    IpAddress = table.Column<string>(type: "nvarchar(45)", maxLength: 45, nullable: true),
                    UserAgent = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ConsentTextHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    EvidenceJson = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConsentRecords", x => x.ConsentRecordID);
                    table.ForeignKey(
                        name: "FK_ConsentRecords_LegalDocumentVersions_LegalDocumentVersionID",
                        column: x => x.LegalDocumentVersionID,
                        principalTable: "LegalDocumentVersions",
                        principalColumn: "LegalDocumentVersionID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ConsentRecords_Users_UserID",
                        column: x => x.UserID,
                        principalTable: "Users",
                        principalColumn: "UserID");
                });

            migrationBuilder.CreateIndex(
                name: "IX_PrivacyRequests_CompletedByUserID",
                table: "PrivacyRequests",
                column: "CompletedByUserID");

            migrationBuilder.CreateIndex(
                name: "IX_PrivacyRequests_DueAt",
                table: "PrivacyRequests",
                column: "DueAt");

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_PrivacyNoticeVersionID",
                table: "Bookings",
                column: "PrivacyNoticeVersionID");

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_TermsDocumentVersionID",
                table: "Bookings",
                column: "TermsDocumentVersionID");

            migrationBuilder.CreateIndex(
                name: "IX_ConsentRecords_LegalDocumentVersionID",
                table: "ConsentRecords",
                column: "LegalDocumentVersionID");

            migrationBuilder.CreateIndex(
                name: "IX_ConsentRecords_UserID_LegalDocumentVersionID_PurposeCode_IsGranted",
                table: "ConsentRecords",
                columns: new[] { "UserID", "LegalDocumentVersionID", "PurposeCode", "IsGranted" });

            migrationBuilder.CreateIndex(
                name: "IX_ConsentRecords_UserID_PurposeCode_GrantedAt",
                table: "ConsentRecords",
                columns: new[] { "UserID", "PurposeCode", "GrantedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_LegalDocumentVersions_Code_IsActive_EffectiveAt",
                table: "LegalDocumentVersions",
                columns: new[] { "Code", "IsActive", "EffectiveAt" });

            migrationBuilder.CreateIndex(
                name: "IX_LegalDocumentVersions_Code_Version",
                table: "LegalDocumentVersions",
                columns: new[] { "Code", "Version" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Bookings_LegalDocumentVersions_PrivacyNoticeVersionID",
                table: "Bookings",
                column: "PrivacyNoticeVersionID",
                principalTable: "LegalDocumentVersions",
                principalColumn: "LegalDocumentVersionID");

            migrationBuilder.AddForeignKey(
                name: "FK_Bookings_LegalDocumentVersions_TermsDocumentVersionID",
                table: "Bookings",
                column: "TermsDocumentVersionID",
                principalTable: "LegalDocumentVersions",
                principalColumn: "LegalDocumentVersionID");

            migrationBuilder.AddForeignKey(
                name: "FK_PrivacyRequests_Users_CompletedByUserID",
                table: "PrivacyRequests",
                column: "CompletedByUserID",
                principalTable: "Users",
                principalColumn: "UserID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Bookings_LegalDocumentVersions_PrivacyNoticeVersionID",
                table: "Bookings");

            migrationBuilder.DropForeignKey(
                name: "FK_Bookings_LegalDocumentVersions_TermsDocumentVersionID",
                table: "Bookings");

            migrationBuilder.DropForeignKey(
                name: "FK_PrivacyRequests_Users_CompletedByUserID",
                table: "PrivacyRequests");

            migrationBuilder.DropTable(
                name: "ConsentRecords");

            migrationBuilder.DropTable(
                name: "LegalDocumentVersions");

            migrationBuilder.DropIndex(
                name: "IX_PrivacyRequests_CompletedByUserID",
                table: "PrivacyRequests");

            migrationBuilder.DropIndex(
                name: "IX_PrivacyRequests_DueAt",
                table: "PrivacyRequests");

            migrationBuilder.DropIndex(
                name: "IX_Bookings_PrivacyNoticeVersionID",
                table: "Bookings");

            migrationBuilder.DropIndex(
                name: "IX_Bookings_TermsDocumentVersionID",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "CompletedByUserID",
                table: "PrivacyRequests");

            migrationBuilder.DropColumn(
                name: "DueAt",
                table: "PrivacyRequests");

            migrationBuilder.DropColumn(
                name: "IdentityVerificationStatus",
                table: "PrivacyRequests");

            migrationBuilder.DropColumn(
                name: "RequestScope",
                table: "PrivacyRequests");

            migrationBuilder.DropColumn(
                name: "SecureExportPath",
                table: "PrivacyRequests");

            migrationBuilder.DropColumn(
                name: "PrivacyNoticeVersionID",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "SelfDriveAgreementAcceptedAt",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "SelfDriveAgreementIpAddress",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "SelfDriveAgreementUserAgent",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "TermsDocumentVersionID",
                table: "Bookings");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentLedgerEntries_BookingID_EntryType_Status",
                table: "PaymentLedgerEntries",
                columns: new[] { "BookingID", "EntryType", "Status" });
        }
    }
}
