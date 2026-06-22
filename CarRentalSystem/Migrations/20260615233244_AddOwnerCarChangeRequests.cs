using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CarRentalSystem.Migrations
{
    /// <inheritdoc />
    public partial class AddOwnerCarChangeRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsPendingDeletion",
                table: "Cars",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "CarChangeRequests",
                columns: table => new
                {
                    CarChangeRequestID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CarID = table.Column<int>(type: "int", nullable: false),
                    OwnerID = table.Column<int>(type: "int", nullable: false),
                    AreaID = table.Column<int>(type: "int", nullable: true),
                    Operation = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    CurrentStage = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OwnerNote = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RejectionReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CarChangeRequests", x => x.CarChangeRequestID);
                    table.ForeignKey(
                        name: "FK_CarChangeRequests_Areas_AreaID",
                        column: x => x.AreaID,
                        principalTable: "Areas",
                        principalColumn: "AreaID",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_CarChangeRequests_Cars_CarID",
                        column: x => x.CarID,
                        principalTable: "Cars",
                        principalColumn: "CarID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CarChangeRequests_Users_OwnerID",
                        column: x => x.OwnerID,
                        principalTable: "Users",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CarChangeApprovalSteps",
                columns: table => new
                {
                    CarChangeApprovalStepID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CarChangeRequestID = table.Column<int>(type: "int", nullable: false),
                    Stage = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Decision = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ActorUserID = table.Column<int>(type: "int", nullable: true),
                    DecidedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CarChangeApprovalSteps", x => x.CarChangeApprovalStepID);
                    table.ForeignKey(
                        name: "FK_CarChangeApprovalSteps_CarChangeRequests_CarChangeRequestID",
                        column: x => x.CarChangeRequestID,
                        principalTable: "CarChangeRequests",
                        principalColumn: "CarChangeRequestID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CarChangeApprovalSteps_Users_ActorUserID",
                        column: x => x.ActorUserID,
                        principalTable: "Users",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CarChangeApprovalSteps_ActorUserID",
                table: "CarChangeApprovalSteps",
                column: "ActorUserID");

            migrationBuilder.CreateIndex(
                name: "IX_CarChangeApprovalSteps_CarChangeRequestID_Stage",
                table: "CarChangeApprovalSteps",
                columns: new[] { "CarChangeRequestID", "Stage" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CarChangeRequests_AreaID",
                table: "CarChangeRequests",
                column: "AreaID");

            migrationBuilder.CreateIndex(
                name: "IX_CarChangeRequests_CarID_Status",
                table: "CarChangeRequests",
                columns: new[] { "CarID", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_CarChangeRequests_OwnerID",
                table: "CarChangeRequests",
                column: "OwnerID");

            migrationBuilder.CreateIndex(
                name: "IX_CarChangeRequests_Status_CurrentStage_AreaID",
                table: "CarChangeRequests",
                columns: new[] { "Status", "CurrentStage", "AreaID" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CarChangeApprovalSteps");

            migrationBuilder.DropTable(
                name: "CarChangeRequests");

            migrationBuilder.DropColumn(
                name: "IsPendingDeletion",
                table: "Cars");
        }
    }
}
