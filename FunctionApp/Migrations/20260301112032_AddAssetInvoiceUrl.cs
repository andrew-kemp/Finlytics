using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinanceHubFunctions.Migrations
{
    /// <inheritdoc />
    public partial class AddAssetInvoiceUrl : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CtTag",
                table: "Expenses",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "PsaApproved",
                table: "CompanySettings",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PsaContactName",
                table: "CompanySettings",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InvoiceUrl",
                table: "Assets",
                type: "nvarchar(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.UpdateData(
                table: "CompanySettings",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "PsaApproved", "PsaContactName" },
                values: new object[] { null, null });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CtTag",
                table: "Expenses");

            migrationBuilder.DropColumn(
                name: "PsaApproved",
                table: "CompanySettings");

            migrationBuilder.DropColumn(
                name: "PsaContactName",
                table: "CompanySettings");

            migrationBuilder.DropColumn(
                name: "InvoiceUrl",
                table: "Assets");
        }
    }
}
