using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinanceHubFunctions.Migrations
{
    public partial class AddMileageTables : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MileageClaims",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false).Annotation("SqlServer:Identity", "1, 1"),
                    ClaimRef = table.Column<string>(maxLength: 50, nullable: false),
                    Director = table.Column<string>(maxLength: 100, nullable: false),
                    PeriodStart = table.Column<DateTime>(nullable: false),
                    PeriodEnd = table.Column<DateTime>(nullable: false),
                    TaxYear = table.Column<string>(maxLength: 10, nullable: false),
                    TotalMiles = table.Column<decimal>(type: "decimal(8,2)", nullable: false, defaultValue: 0m),
                    MilesAt45p = table.Column<decimal>(type: "decimal(8,2)", nullable: false, defaultValue: 0m),
                    MilesAt25p = table.Column<decimal>(type: "decimal(8,2)", nullable: false, defaultValue: 0m),
                    TotalAmount = table.Column<decimal>(type: "decimal(10,2)", nullable: false, defaultValue: 0m),
                    Status = table.Column<string>(maxLength: 20, nullable: false, defaultValue: "Draft"),
                    SubmittedAt = table.Column<DateTime>(nullable: true),
                    PostedAt = table.Column<DateTime>(nullable: true),
                    PaidAt = table.Column<DateTime>(nullable: true),
                    DlaEntryId = table.Column<int>(nullable: true),
                    ReimbursementDlaEntryId = table.Column<int>(nullable: true),
                    Notes = table.Column<string>(maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UpdatedAt = table.Column<DateTime>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MileageClaims", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MileageTrips",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false).Annotation("SqlServer:Identity", "1, 1"),
                    TripId = table.Column<string>(maxLength: 50, nullable: false),
                    Director = table.Column<string>(maxLength: 100, nullable: false),
                    TripDate = table.Column<DateTime>(nullable: false),
                    StartLocation = table.Column<string>(maxLength: 500, nullable: false),
                    EndLocation = table.Column<string>(maxLength: 500, nullable: false),
                    Miles = table.Column<decimal>(type: "decimal(8,2)", nullable: false),
                    IsReturn = table.Column<bool>(nullable: false, defaultValue: false),
                    Purpose = table.Column<string>(maxLength: 500, nullable: false),
                    Category = table.Column<string>(maxLength: 100, nullable: false, defaultValue: "Consulting"),
                    TaxYear = table.Column<string>(maxLength: 10, nullable: false),
                    Status = table.Column<string>(maxLength: 20, nullable: false, defaultValue: "Draft"),
                    ClaimId = table.Column<int>(nullable: true),
                    MilesAt45p = table.Column<decimal>(type: "decimal(8,2)", nullable: false, defaultValue: 0m),
                    MilesAt25p = table.Column<decimal>(type: "decimal(8,2)", nullable: false, defaultValue: 0m),
                    AmountAt45p = table.Column<decimal>(type: "decimal(10,2)", nullable: false, defaultValue: 0m),
                    AmountAt25p = table.Column<decimal>(type: "decimal(10,2)", nullable: false, defaultValue: 0m),
                    TotalAmount = table.Column<decimal>(type: "decimal(10,2)", nullable: false, defaultValue: 0m),
                    MapLink = table.Column<string>(maxLength: 1000, nullable: true),
                    Notes = table.Column<string>(maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UpdatedAt = table.Column<DateTime>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MileageTrips", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MileageClaims_ClaimRef",
                table: "MileageClaims",
                column: "ClaimRef",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MileageTrips_TripId",
                table: "MileageTrips",
                column: "TripId",
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "MileageTrips");
            migrationBuilder.DropTable(name: "MileageClaims");
        }
    }
}
