using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CarRentalSystem.Migrations
{
    /// <inheritdoc />
    public partial class AddPrivacyRequestsWave3 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PrivacyRequests_Users_UserID",
                table: "PrivacyRequests");

            migrationBuilder.DropIndex(
                name: "IX_PrivacyRequests_UserID",
                table: "PrivacyRequests");

            migrationBuilder.CreateIndex(
                name: "IX_PrivacyRequests_UserID_Status_CreatedAt",
                table: "PrivacyRequests",
                columns: new[] { "UserID", "Status", "CreatedAt" });

            migrationBuilder.AddForeignKey(
                name: "FK_PrivacyRequests_Users_UserID",
                table: "PrivacyRequests",
                column: "UserID",
                principalTable: "Users",
                principalColumn: "UserID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PrivacyRequests_Users_UserID",
                table: "PrivacyRequests");

            migrationBuilder.DropIndex(
                name: "IX_PrivacyRequests_UserID_Status_CreatedAt",
                table: "PrivacyRequests");

            migrationBuilder.CreateIndex(
                name: "IX_PrivacyRequests_UserID",
                table: "PrivacyRequests",
                column: "UserID");

            migrationBuilder.AddForeignKey(
                name: "FK_PrivacyRequests_Users_UserID",
                table: "PrivacyRequests",
                column: "UserID",
                principalTable: "Users",
                principalColumn: "UserID",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
