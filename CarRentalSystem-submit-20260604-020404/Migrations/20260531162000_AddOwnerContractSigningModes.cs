using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CarRentalSystem.Migrations
{
    /// <inheritdoc />
    public partial class AddOwnerContractSigningModes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ContractSigningMode",
                table: "DigitalContracts",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "Digital");

            migrationBuilder.AddColumn<string>(
                name: "OwnerSignatureHash",
                table: "DigitalContracts",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OwnerSignatureProfileSnapshotJson",
                table: "DigitalContracts",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OwnerSignatureUrl",
                table: "DigitalContracts",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OwnerUserIdsJson",
                table: "DigitalContracts",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContractSigningMode",
                table: "Cars",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "PhysicalSelfManaged");

            migrationBuilder.CreateTable(
                name: "OwnerDigitalSignatureProfiles",
                columns: table => new
                {
                    OwnerDigitalSignatureProfileID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserID = table.Column<int>(type: "int", nullable: false),
                    ProviderName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    CertificateSerialNumber = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    CertificateSubject = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    CertificateValidUntil = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    SignatureImageUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    SignatureImageHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    VerifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OwnerDigitalSignatureProfiles", x => x.OwnerDigitalSignatureProfileID);
                    table.ForeignKey(
                        name: "FK_OwnerDigitalSignatureProfiles_Users_UserID",
                        column: x => x.UserID,
                        principalTable: "Users",
                        principalColumn: "UserID");
                });

            migrationBuilder.CreateIndex(
                name: "IX_OwnerDigitalSignatureProfiles_UserID",
                table: "OwnerDigitalSignatureProfiles",
                column: "UserID",
                unique: true,
                filter: "[IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OwnerDigitalSignatureProfiles");

            migrationBuilder.DropColumn(
                name: "ContractSigningMode",
                table: "DigitalContracts");

            migrationBuilder.DropColumn(
                name: "OwnerSignatureHash",
                table: "DigitalContracts");

            migrationBuilder.DropColumn(
                name: "OwnerSignatureProfileSnapshotJson",
                table: "DigitalContracts");

            migrationBuilder.DropColumn(
                name: "OwnerSignatureUrl",
                table: "DigitalContracts");

            migrationBuilder.DropColumn(
                name: "OwnerUserIdsJson",
                table: "DigitalContracts");

            migrationBuilder.DropColumn(
                name: "ContractSigningMode",
                table: "Cars");
        }
    }
}
