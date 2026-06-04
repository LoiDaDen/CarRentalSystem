using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CarRentalSystem.Migrations
{
    /// <inheritdoc />
    public partial class AddPiiUniquenessHashes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CitizenIdHash",
                table: "Users",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DrivingLicenseHash",
                table: "Users",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PhoneHash",
                table: "Users",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_CitizenIdHash",
                table: "Users",
                column: "CitizenIdHash",
                unique: true,
                filter: "[CitizenIdHash] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Users_DrivingLicenseHash",
                table: "Users",
                column: "DrivingLicenseHash",
                unique: true,
                filter: "[DrivingLicenseHash] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Users_PhoneHash",
                table: "Users",
                column: "PhoneHash",
                unique: true,
                filter: "[PhoneHash] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CitizenIdHash",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "DrivingLicenseHash",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "PhoneHash",
                table: "Users");
        }
    }
}
