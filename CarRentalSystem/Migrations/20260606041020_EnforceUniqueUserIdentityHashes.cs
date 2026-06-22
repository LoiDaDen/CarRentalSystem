using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CarRentalSystem.Migrations
{
    /// <inheritdoc />
    public partial class EnforceUniqueUserIdentityHashes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Users_CitizenIdHash' AND object_id = OBJECT_ID(N'[Users]'))
                    CREATE UNIQUE INDEX [IX_Users_CitizenIdHash] ON [Users] ([CitizenIdHash]) WHERE [CitizenIdHash] IS NOT NULL;

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Users_DrivingLicenseHash' AND object_id = OBJECT_ID(N'[Users]'))
                    CREATE UNIQUE INDEX [IX_Users_DrivingLicenseHash] ON [Users] ([DrivingLicenseHash]) WHERE [DrivingLicenseHash] IS NOT NULL;

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Users_PhoneHash' AND object_id = OBJECT_ID(N'[Users]'))
                    CREATE UNIQUE INDEX [IX_Users_PhoneHash] ON [Users] ([PhoneHash]) WHERE [PhoneHash] IS NOT NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Users_CitizenIdHash' AND object_id = OBJECT_ID(N'[Users]'))
                    DROP INDEX [IX_Users_CitizenIdHash] ON [Users];

                IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Users_DrivingLicenseHash' AND object_id = OBJECT_ID(N'[Users]'))
                    DROP INDEX [IX_Users_DrivingLicenseHash] ON [Users];

                IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Users_PhoneHash' AND object_id = OBJECT_ID(N'[Users]'))
                    DROP INDEX [IX_Users_PhoneHash] ON [Users];
                """);
        }
    }
}
