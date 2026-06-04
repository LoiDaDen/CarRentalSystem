using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CarRentalSystem.Migrations
{
    /// <inheritdoc />
    public partial class AddWave4SupportApiKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ApiKeys",
                columns: table => new
                {
                    ApiKeyID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OwnerUserID = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    KeyPrefix = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    KeyHash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Scopes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RevokedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RevokedByUserID = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApiKeys", x => x.ApiKeyID);
                    table.ForeignKey(
                        name: "FK_ApiKeys_Users_OwnerUserID",
                        column: x => x.OwnerUserID,
                        principalTable: "Users",
                        principalColumn: "UserID");
                    table.ForeignKey(
                        name: "FK_ApiKeys_Users_RevokedByUserID",
                        column: x => x.RevokedByUserID,
                        principalTable: "Users",
                        principalColumn: "UserID");
                });

            migrationBuilder.CreateTable(
                name: "SupportTickets",
                columns: table => new
                {
                    SupportTicketID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserID = table.Column<int>(type: "int", nullable: false),
                    AssignedToUserID = table.Column<int>(type: "int", nullable: true),
                    BookingID = table.Column<int>(type: "int", nullable: true),
                    PrivacyRequestID = table.Column<int>(type: "int", nullable: true),
                    SettlementReviewID = table.Column<int>(type: "int", nullable: true),
                    RefundRequestID = table.Column<int>(type: "int", nullable: true),
                    TicketType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Priority = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Subject = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    Resolution = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FirstResponseDueAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ResolvedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupportTickets", x => x.SupportTicketID);
                    table.ForeignKey(
                        name: "FK_SupportTickets_Bookings_BookingID",
                        column: x => x.BookingID,
                        principalTable: "Bookings",
                        principalColumn: "BookingID");
                    table.ForeignKey(
                        name: "FK_SupportTickets_PrivacyRequests_PrivacyRequestID",
                        column: x => x.PrivacyRequestID,
                        principalTable: "PrivacyRequests",
                        principalColumn: "PrivacyRequestID");
                    table.ForeignKey(
                        name: "FK_SupportTickets_RefundRequests_RefundRequestID",
                        column: x => x.RefundRequestID,
                        principalTable: "RefundRequests",
                        principalColumn: "RefundRequestID");
                    table.ForeignKey(
                        name: "FK_SupportTickets_SettlementReviews_SettlementReviewID",
                        column: x => x.SettlementReviewID,
                        principalTable: "SettlementReviews",
                        principalColumn: "SettlementReviewID");
                    table.ForeignKey(
                        name: "FK_SupportTickets_Users_AssignedToUserID",
                        column: x => x.AssignedToUserID,
                        principalTable: "Users",
                        principalColumn: "UserID");
                    table.ForeignKey(
                        name: "FK_SupportTickets_Users_UserID",
                        column: x => x.UserID,
                        principalTable: "Users",
                        principalColumn: "UserID");
                });

            migrationBuilder.CreateIndex(
                name: "IX_ApiKeys_KeyHash",
                table: "ApiKeys",
                column: "KeyHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ApiKeys_OwnerUserID_Status",
                table: "ApiKeys",
                columns: new[] { "OwnerUserID", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ApiKeys_RevokedByUserID",
                table: "ApiKeys",
                column: "RevokedByUserID");

            migrationBuilder.CreateIndex(
                name: "IX_SupportTickets_AssignedToUserID",
                table: "SupportTickets",
                column: "AssignedToUserID");

            migrationBuilder.CreateIndex(
                name: "IX_SupportTickets_BookingID",
                table: "SupportTickets",
                column: "BookingID");

            migrationBuilder.CreateIndex(
                name: "IX_SupportTickets_Priority_Status_FirstResponseDueAt",
                table: "SupportTickets",
                columns: new[] { "Priority", "Status", "FirstResponseDueAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SupportTickets_PrivacyRequestID",
                table: "SupportTickets",
                column: "PrivacyRequestID");

            migrationBuilder.CreateIndex(
                name: "IX_SupportTickets_RefundRequestID",
                table: "SupportTickets",
                column: "RefundRequestID");

            migrationBuilder.CreateIndex(
                name: "IX_SupportTickets_SettlementReviewID",
                table: "SupportTickets",
                column: "SettlementReviewID");

            migrationBuilder.CreateIndex(
                name: "IX_SupportTickets_UserID_Status_CreatedAt",
                table: "SupportTickets",
                columns: new[] { "UserID", "Status", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApiKeys");

            migrationBuilder.DropTable(
                name: "SupportTickets");
        }
    }
}
