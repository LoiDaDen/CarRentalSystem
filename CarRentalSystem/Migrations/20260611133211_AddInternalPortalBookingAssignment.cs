using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CarRentalSystem.Migrations
{
    /// <inheritdoc />
    public partial class AddInternalPortalBookingAssignment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "AssignedAtUtc",
                table: "Bookings",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AssignedByUserID",
                table: "Bookings",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AssignedStaffUserID",
                table: "Bookings",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_AssignedByUserID",
                table: "Bookings",
                column: "AssignedByUserID");

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_AssignedStaffUserID_StartDate",
                table: "Bookings",
                columns: new[] { "AssignedStaffUserID", "StartDate" });

            migrationBuilder.AddForeignKey(
                name: "FK_Bookings_Users_AssignedByUserID",
                table: "Bookings",
                column: "AssignedByUserID",
                principalTable: "Users",
                principalColumn: "UserID");

            migrationBuilder.AddForeignKey(
                name: "FK_Bookings_Users_AssignedStaffUserID",
                table: "Bookings",
                column: "AssignedStaffUserID",
                principalTable: "Users",
                principalColumn: "UserID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Bookings_Users_AssignedByUserID",
                table: "Bookings");

            migrationBuilder.DropForeignKey(
                name: "FK_Bookings_Users_AssignedStaffUserID",
                table: "Bookings");

            migrationBuilder.DropIndex(
                name: "IX_Bookings_AssignedByUserID",
                table: "Bookings");

            migrationBuilder.DropIndex(
                name: "IX_Bookings_AssignedStaffUserID_StartDate",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "AssignedAtUtc",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "AssignedByUserID",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "AssignedStaffUserID",
                table: "Bookings");
        }
    }
}
