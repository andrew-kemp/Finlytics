using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinanceHubFunctions.Migrations
{
    /// <inheritdoc />
    public partial class AddMissingReceiptDeclaration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "HasMissingReceiptDeclaration",
                table: "Expenses",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "MissingReceiptDeclarationRef",
                table: "Expenses",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ExpenseAuditEvents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ExpenseId = table.Column<int>(type: "int", nullable: false),
                    EventType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ActorName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ActorEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Details = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    DeclarationId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExpenseAuditEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MissingReceiptDeclarations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DeclarationId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ExpenseId = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DeclarationType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DeclarerName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    DeclarerRole = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    DeclarerEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    AmountGross = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true, defaultValue: "GBP"),
                    ExpenseDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    MerchantOrPayee = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    BankTransactionRef = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ExpenseCategory = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ReasonReceiptMissing = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OtherReasonText = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    VatReclaimable = table.Column<bool>(type: "bit", nullable: false),
                    VatAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    AcknowledgementDisallowable = table.Column<bool>(type: "bit", nullable: false),
                    SignatureType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TypedSignature = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    PdfBlobRef = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    HashSha256 = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FinalisedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    VoidedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    VoidedReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MissingReceiptDeclarations", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ExpenseAuditEvents_ExpenseId",
                table: "ExpenseAuditEvents",
                column: "ExpenseId");

            migrationBuilder.CreateIndex(
                name: "IX_MissingReceiptDeclarations_DeclarationId",
                table: "MissingReceiptDeclarations",
                column: "DeclarationId",
                unique: true,
                filter: "[DeclarationId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_MissingReceiptDeclarations_ExpenseId",
                table: "MissingReceiptDeclarations",
                column: "ExpenseId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ExpenseAuditEvents");

            migrationBuilder.DropTable(
                name: "MissingReceiptDeclarations");

            migrationBuilder.DropColumn(
                name: "HasMissingReceiptDeclaration",
                table: "Expenses");

            migrationBuilder.DropColumn(
                name: "MissingReceiptDeclarationRef",
                table: "Expenses");
        }
    }
}
