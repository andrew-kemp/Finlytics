using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinanceHubFunctions.Migrations
{
    /// <inheritdoc />
    public partial class AddTrivialBenefitFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsTrivialBenefit",
                table: "Expenses",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "TrivialBenefitType",
                table: "Expenses",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsTrivialBenefit",
                table: "DlaEntries",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "TrivialBenefitType",
                table: "DlaEntries",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsTrivialBenefit",
                table: "Expenses");

            migrationBuilder.DropColumn(
                name: "TrivialBenefitType",
                table: "Expenses");

            migrationBuilder.DropColumn(
                name: "IsTrivialBenefit",
                table: "DlaEntries");

            migrationBuilder.DropColumn(
                name: "TrivialBenefitType",
                table: "DlaEntries");
        }
    }
}
