using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinanceHubFunctions.Migrations
{
    /// <inheritdoc />
    public partial class AddShareholderBankDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add bank columns to Shareholders (idempotent — skip if column already exists)
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Shareholders') AND name = 'AccountNumber')
                    ALTER TABLE [Shareholders] ADD [AccountNumber] nvarchar(50) NULL;
            ");
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Shareholders') AND name = 'BankAccountName')
                    ALTER TABLE [Shareholders] ADD [BankAccountName] nvarchar(255) NULL;
            ");
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Shareholders') AND name = 'BankSortCode')
                    ALTER TABLE [Shareholders] ADD [BankSortCode] nvarchar(20) NULL;
            ");

            // Create DividendAllocations table only if it doesn't already exist
            // (table may have been created by a prior SQL script deployment)
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'DividendAllocations')
                BEGIN
                    CREATE TABLE [DividendAllocations] (
                        [Id] int NOT NULL IDENTITY(1,1),
                        [DividendDeclarationId] int NOT NULL,
                        [ShareholderId] int NULL,
                        [ShareholderName] nvarchar(255) NOT NULL,
                        [ShareClass] nvarchar(50) NULL,
                        [NumberOfShares] int NOT NULL,
                        [AmountPerShare] decimal(18,4) NOT NULL,
                        [TotalAmount] decimal(18,2) NOT NULL,
                        [BankAccountName] nvarchar(255) NULL,
                        [SortCode] nvarchar(20) NULL,
                        [AccountNumber] nvarchar(50) NULL,
                        [VoucherRef] nvarchar(50) NULL,
                        [LedgerEntryId] int NULL,
                        CONSTRAINT [PK_DividendAllocations] PRIMARY KEY ([Id])
                    );
                    CREATE INDEX [IX_DividendAllocations_DividendDeclarationId]
                        ON [DividendAllocations] ([DividendDeclarationId]);
                END
            ");

            // Create DividendDeclarations table only if it doesn't already exist
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'DividendDeclarations')
                BEGIN
                    CREATE TABLE [DividendDeclarations] (
                        [Id] int NOT NULL IDENTITY(1,1),
                        [DividendRef] nvarchar(50) NOT NULL,
                        [DividendType] nvarchar(50) NULL,
                        [ShareClass] nvarchar(50) NULL,
                        [MeetingDate] datetime2 NOT NULL,
                        [MeetingLocation] nvarchar(255) NULL,
                        [RecordDate] datetime2 NOT NULL,
                        [PaymentDate] datetime2 NOT NULL,
                        [AmountPerShare] decimal(18,4) NOT NULL,
                        [TotalAmount] decimal(18,2) NOT NULL,
                        [Status] nvarchar(50) NULL,
                        [DirectorName] nvarchar(255) NULL,
                        [Notes] nvarchar(2000) NULL,
                        [CreatedDate] datetime2 NOT NULL,
                        [FinalisedDate] datetime2 NULL,
                        CONSTRAINT [PK_DividendDeclarations] PRIMARY KEY ([Id])
                    );
                    CREATE INDEX [IX_DividendDeclarations_DividendRef] ON [DividendDeclarations] ([DividendRef]);
                    CREATE INDEX [IX_DividendDeclarations_Status] ON [DividendDeclarations] ([Status]);
                END
            ");
        }

        // Keep the original EF scaffold methods below so the snapshot stays consistent
        private void CreateTable_DividendAllocations_EFScaffold(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DividendAllocations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DividendDeclarationId = table.Column<int>(type: "int", nullable: false),
                    ShareholderId = table.Column<int>(type: "int", nullable: true),
                    ShareholderName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    ShareClass = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    NumberOfShares = table.Column<int>(type: "int", nullable: false),
                    AmountPerShare = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    BankAccountName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    SortCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    AccountNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    VoucherRef = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    LedgerEntryId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DividendAllocations", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Shareholders') AND name = 'AccountNumber')
                    ALTER TABLE [Shareholders] DROP COLUMN [AccountNumber];
            ");
            migrationBuilder.Sql(@"
                IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Shareholders') AND name = 'BankAccountName')
                    ALTER TABLE [Shareholders] DROP COLUMN [BankAccountName];
            ");
            migrationBuilder.Sql(@"
                IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Shareholders') AND name = 'BankSortCode')
                    ALTER TABLE [Shareholders] DROP COLUMN [BankSortCode];
            ");
        }
    }
}
