using Microsoft.EntityFrameworkCore.Migrations;
using System;

#nullable disable

namespace FinanceHubFunctions.Migrations
{
    /// <inheritdoc />
    public partial class AddMonzoFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MonzoAccountId",
                table: "BankAccounts",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "MonzoConnected",
                table: "BankAccounts",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "MonzoLastSyncedAt",
                table: "BankAccounts",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MonzoTransactionId",
                table: "BankTransactions",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MonzoMerchantName",
                table: "BankTransactions",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MonzoCategory",
                table: "BankTransactions",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MonzoNotes",
                table: "BankTransactions",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "MonzoAccountId", table: "BankAccounts");
            migrationBuilder.DropColumn(name: "MonzoConnected", table: "BankAccounts");
            migrationBuilder.DropColumn(name: "MonzoLastSyncedAt", table: "BankAccounts");
            migrationBuilder.DropColumn(name: "MonzoTransactionId", table: "BankTransactions");
            migrationBuilder.DropColumn(name: "MonzoMerchantName", table: "BankTransactions");
            migrationBuilder.DropColumn(name: "MonzoCategory", table: "BankTransactions");
            migrationBuilder.DropColumn(name: "MonzoNotes", table: "BankTransactions");
        }
    }
}
