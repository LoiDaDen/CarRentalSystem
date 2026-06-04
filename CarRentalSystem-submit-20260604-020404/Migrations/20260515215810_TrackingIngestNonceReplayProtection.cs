using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CarRentalSystem.Migrations
{
    /// <inheritdoc />
    public partial class TrackingIngestNonceReplayProtection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TrackingIngestNonces",
                columns: table => new
                {
                    TrackingIngestNonceID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DeviceId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Nonce = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    TimestampUnixSeconds = table.Column<long>(type: "bigint", nullable: false),
                    CarID = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrackingIngestNonces", x => x.TrackingIngestNonceID);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TrackingIngestNonces_CreatedAt",
                table: "TrackingIngestNonces",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_TrackingIngestNonces_DeviceId_Nonce",
                table: "TrackingIngestNonces",
                columns: new[] { "DeviceId", "Nonce" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TrackingIngestNonces");
        }
    }
}
