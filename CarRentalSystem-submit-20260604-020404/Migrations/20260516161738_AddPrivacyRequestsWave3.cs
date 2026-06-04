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
            migrationBuilder.Sql("""
                IF OBJECT_ID(N'[PrivacyRequests]', N'U') IS NULL
                BEGIN
                    CREATE TABLE [PrivacyRequests] (
                        [PrivacyRequestID] int NOT NULL IDENTITY,
                        [UserID] int NOT NULL,
                        [RequestType] nvarchar(30) NOT NULL,
                        [Status] nvarchar(30) NOT NULL,
                        [Reason] nvarchar(1000) NULL,
                        [ReviewReason] nvarchar(1000) NULL,
                        [ReviewedByUserID] int NULL,
                        [CreatedAt] datetime2 NOT NULL,
                        [ReviewedAt] datetime2 NULL,
                        [CompletedAt] datetime2 NULL,
                        [ExportManifestJson] nvarchar(max) NULL,
                        CONSTRAINT [PK_PrivacyRequests] PRIMARY KEY ([PrivacyRequestID])
                    );
                END

                IF EXISTS (
                    SELECT 1 FROM sys.foreign_keys
                    WHERE name = N'FK_PrivacyRequests_Users_UserID'
                      AND parent_object_id = OBJECT_ID(N'[PrivacyRequests]')
                )
                BEGIN
                    ALTER TABLE [PrivacyRequests] DROP CONSTRAINT [FK_PrivacyRequests_Users_UserID];
                END

                IF EXISTS (
                    SELECT 1 FROM sys.indexes
                    WHERE name = N'IX_PrivacyRequests_UserID'
                      AND object_id = OBJECT_ID(N'[PrivacyRequests]')
                )
                BEGIN
                    DROP INDEX [IX_PrivacyRequests_UserID] ON [PrivacyRequests];
                END

                IF NOT EXISTS (
                    SELECT 1 FROM sys.indexes
                    WHERE name = N'IX_PrivacyRequests_UserID_Status_CreatedAt'
                      AND object_id = OBJECT_ID(N'[PrivacyRequests]')
                )
                BEGIN
                    CREATE INDEX [IX_PrivacyRequests_UserID_Status_CreatedAt]
                    ON [PrivacyRequests] ([UserID], [Status], [CreatedAt]);
                END

                IF NOT EXISTS (
                    SELECT 1 FROM sys.indexes
                    WHERE name = N'IX_PrivacyRequests_ReviewedByUserID'
                      AND object_id = OBJECT_ID(N'[PrivacyRequests]')
                )
                BEGIN
                    CREATE INDEX [IX_PrivacyRequests_ReviewedByUserID]
                    ON [PrivacyRequests] ([ReviewedByUserID]);
                END

                IF NOT EXISTS (
                    SELECT 1 FROM sys.foreign_keys
                    WHERE name = N'FK_PrivacyRequests_Users_UserID'
                      AND parent_object_id = OBJECT_ID(N'[PrivacyRequests]')
                )
                BEGIN
                    ALTER TABLE [PrivacyRequests]
                    ADD CONSTRAINT [FK_PrivacyRequests_Users_UserID]
                    FOREIGN KEY ([UserID]) REFERENCES [Users] ([UserID]);
                END

                IF NOT EXISTS (
                    SELECT 1 FROM sys.foreign_keys
                    WHERE name = N'FK_PrivacyRequests_Users_ReviewedByUserID'
                      AND parent_object_id = OBJECT_ID(N'[PrivacyRequests]')
                )
                BEGIN
                    ALTER TABLE [PrivacyRequests]
                    ADD CONSTRAINT [FK_PrivacyRequests_Users_ReviewedByUserID]
                    FOREIGN KEY ([ReviewedByUserID]) REFERENCES [Users] ([UserID]);
                END
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF OBJECT_ID(N'[PrivacyRequests]', N'U') IS NOT NULL
                BEGIN
                    IF EXISTS (
                        SELECT 1 FROM sys.foreign_keys
                        WHERE name = N'FK_PrivacyRequests_Users_ReviewedByUserID'
                          AND parent_object_id = OBJECT_ID(N'[PrivacyRequests]')
                    )
                    BEGIN
                        ALTER TABLE [PrivacyRequests] DROP CONSTRAINT [FK_PrivacyRequests_Users_ReviewedByUserID];
                    END

                    IF EXISTS (
                        SELECT 1 FROM sys.foreign_keys
                        WHERE name = N'FK_PrivacyRequests_Users_UserID'
                          AND parent_object_id = OBJECT_ID(N'[PrivacyRequests]')
                    )
                    BEGIN
                        ALTER TABLE [PrivacyRequests] DROP CONSTRAINT [FK_PrivacyRequests_Users_UserID];
                    END

                    IF EXISTS (
                        SELECT 1 FROM sys.indexes
                        WHERE name = N'IX_PrivacyRequests_UserID_Status_CreatedAt'
                          AND object_id = OBJECT_ID(N'[PrivacyRequests]')
                    )
                    BEGIN
                        DROP INDEX [IX_PrivacyRequests_UserID_Status_CreatedAt] ON [PrivacyRequests];
                    END

                    IF NOT EXISTS (
                        SELECT 1 FROM sys.indexes
                        WHERE name = N'IX_PrivacyRequests_UserID'
                          AND object_id = OBJECT_ID(N'[PrivacyRequests]')
                    )
                    BEGIN
                        CREATE INDEX [IX_PrivacyRequests_UserID] ON [PrivacyRequests] ([UserID]);
                    END

                    ALTER TABLE [PrivacyRequests]
                    ADD CONSTRAINT [FK_PrivacyRequests_Users_UserID]
                    FOREIGN KEY ([UserID]) REFERENCES [Users] ([UserID]) ON DELETE CASCADE;
                END
                """);
        }
    }
}
