using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CarRentalSystem.Migrations
{
    /// <inheritdoc />
    public partial class AddPendingSnapshotAndDiscountToTransaction : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "DiscountAmount",
                table: "BookingTransactions",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MembershipDiscountPercent",
                table: "BookingTransactions",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PendingCartSnapshot",
                table: "BookingTransactions",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DiscountAmount",
                table: "BookingTransactions");

            migrationBuilder.DropColumn(
                name: "MembershipDiscountPercent",
                table: "BookingTransactions");

            migrationBuilder.DropColumn(
                name: "PendingCartSnapshot",
                table: "BookingTransactions");
        }
    }
}
