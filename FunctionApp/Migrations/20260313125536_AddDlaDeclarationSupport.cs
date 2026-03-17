using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinanceHubFunctions.Migrations
{
    /// <inheritdoc />
    public partial class AddDlaDeclarationSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "ExpenseId",
                table: "MissingReceiptDeclarations",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<int>(
                name: "DlaEntryId",
                table: "MissingReceiptDeclarations",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "HasMissingReceiptDeclaration",
                table: "DlaEntries",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "MissingReceiptDeclarationRef",
                table: "DlaEntries",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_MissingReceiptDeclarations_DlaEntryId",
                table: "MissingReceiptDeclarations",
                column: "DlaEntryId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MissingReceiptDeclarations_DlaEntryId",
                table: "MissingReceiptDeclarations");

            migrationBuilder.DropColumn(
                name: "DlaEntryId",
                table: "MissingReceiptDeclarations");

            migrationBuilder.DropColumn(
                name: "HasMissingReceiptDeclaration",
                table: "DlaEntries");

            migrationBuilder.DropColumn(
                name: "MissingReceiptDeclarationRef",
                table: "DlaEntries");

            migrationBuilder.AlterColumn<int>(
                name: "ExpenseId",
                table: "MissingReceiptDeclarations",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);
        }
    }
}
