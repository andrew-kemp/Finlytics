using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinanceHubFunctions.Migrations
{
    /// <inheritdoc />
    public partial class AddPayrollEmail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EmployeeNumber",
                table: "Payslips",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PayrollEmail",
                table: "PayrollSettings",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CcEmail",
                table: "Customers",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContactName",
                table: "Customers",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VatAccountingMethod",
                table: "CompanySettings",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "BikEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TaxYear = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    RecipientName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    RecipientType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true, defaultValue: "Director"),
                    BenefitCategory = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CashEquivalent = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    DateFrom = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DateTo = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsExempt = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    ExemptionReason = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    P11DSection = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: true),
                    Headcount = table.Column<int>(type: "int", nullable: true),
                    TotalEventCost = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BikEntries", x => x.Id);
                });

            migrationBuilder.UpdateData(
                table: "CompanySettings",
                keyColumn: "Id",
                keyValue: 1,
                column: "VatAccountingMethod",
                value: null);

            migrationBuilder.CreateIndex(
                name: "IX_BikEntries_RecipientName",
                table: "BikEntries",
                column: "RecipientName");

            migrationBuilder.CreateIndex(
                name: "IX_BikEntries_TaxYear",
                table: "BikEntries",
                column: "TaxYear");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BikEntries");

            migrationBuilder.DropColumn(
                name: "EmployeeNumber",
                table: "Payslips");

            migrationBuilder.DropColumn(
                name: "PayrollEmail",
                table: "PayrollSettings");

            migrationBuilder.DropColumn(
                name: "CcEmail",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "ContactName",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "VatAccountingMethod",
                table: "CompanySettings");
        }
    }
}
