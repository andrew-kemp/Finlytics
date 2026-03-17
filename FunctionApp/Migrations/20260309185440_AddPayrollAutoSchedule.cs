using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinanceHubFunctions.Migrations
{
    /// <inheritdoc />
    public partial class AddPayrollAutoSchedule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DirectorsNiMethod",
                table: "Payslips",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmployeeName",
                table: "Payslips",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "EmployerNi",
                table: "Payslips",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NiCategory",
                table: "Payslips",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NiNumber",
                table: "Payslips",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StarterDeclaration",
                table: "Payslips",
                type: "nvarchar(5)",
                maxLength: 5,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TaxCode",
                table: "Payslips",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TaxMonth",
                table: "Payslips",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TaxYear",
                table: "Payslips",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "YtdEmployeeNi",
                table: "Payslips",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "YtdEmployerNi",
                table: "Payslips",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "YtdGross",
                table: "Payslips",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "YtdTax",
                table: "Payslips",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "AutoPostImmediately",
                table: "PayrollSettings",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "AutoRunDaysBefore",
                table: "PayrollSettings",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "AutoRunEnabled",
                table: "PayrollSettings",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "AutoRunLastTriggered",
                table: "PayrollSettings",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "EmploymentAllowanceEligible",
                table: "PayrollSettings",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "PayDayOfMonth",
                table: "PayrollSettings",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "SmallEmployerRelief",
                table: "PayrollSettings",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "FpsCorrelationId",
                table: "PayrollRuns",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FpsStatus",
                table: "PayrollRuns",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FpsSubmittedAt",
                table: "PayrollRuns",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TaxMonth",
                table: "PayrollRuns",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TaxYear",
                table: "PayrollRuns",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalEmployeeNi",
                table: "PayrollRuns",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalEmployerNi",
                table: "PayrollRuns",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalGross",
                table: "PayrollRuns",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalNetPay",
                table: "PayrollRuns",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalTax",
                table: "PayrollRuns",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReminderCount",
                table: "Invoices",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReminderSentAt",
                table: "Invoices",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AlterColumn<bool>(
                name: "IsTrivialBenefit",
                table: "Expenses",
                type: "bit",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "bit");

            migrationBuilder.AddColumn<bool>(
                name: "IsRecurring",
                table: "Expenses",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "RecurringFrequency",
                table: "Expenses",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RecurringNextDate",
                table: "Expenses",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "TrivialBenefitType",
                table: "DlaEntries",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AmapRate25p",
                table: "CompanySettings",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AmapRate45p",
                table: "CompanySettings",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AmapThresholdMiles",
                table: "CompanySettings",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Utr",
                table: "CompanySettings",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "VatQuarterStartMonth",
                table: "CompanySettings",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "HmrcTokens",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AccessToken = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RefreshToken = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Scope = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HmrcTokens", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MileageClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ClaimRef = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Director = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PeriodEnd = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TaxYear = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    TotalMiles = table.Column<decimal>(type: "decimal(8,2)", nullable: false),
                    MilesAt45p = table.Column<decimal>(type: "decimal(8,2)", nullable: false),
                    MilesAt25p = table.Column<decimal>(type: "decimal(8,2)", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PostedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PaidAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DlaEntryId = table.Column<int>(type: "int", nullable: true),
                    ReimbursementDlaEntryId = table.Column<int>(type: "int", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MileageClaims", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MileageTrips",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TripId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Director = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    TripDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    StartLocation = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    EndLocation = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Miles = table.Column<decimal>(type: "decimal(8,2)", nullable: false),
                    IsReturn = table.Column<bool>(type: "bit", nullable: false),
                    Purpose = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    TaxYear = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ClaimId = table.Column<int>(type: "int", nullable: true),
                    MilesAt45p = table.Column<decimal>(type: "decimal(8,2)", nullable: false),
                    MilesAt25p = table.Column<decimal>(type: "decimal(8,2)", nullable: false),
                    AmountAt45p = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    AmountAt25p = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    MapLink = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MileageTrips", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "VatReturns",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    QuarterLabel = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    MonthsLabel = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    QuarterStartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    QuarterEndDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    VatIn = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    VatOut = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    VatOwed = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    FiledDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Reference = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VatReturns", x => x.Id);
                });

            migrationBuilder.UpdateData(
                table: "CompanySettings",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "AmapRate25p", "AmapRate45p", "AmapThresholdMiles", "Utr", "VatQuarterStartMonth" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.CreateIndex(
                name: "IX_PayrollRuns_TaxYear_TaxMonth",
                table: "PayrollRuns",
                columns: new[] { "TaxYear", "TaxMonth" });

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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HmrcTokens");

            migrationBuilder.DropTable(
                name: "MileageClaims");

            migrationBuilder.DropTable(
                name: "MileageTrips");

            migrationBuilder.DropTable(
                name: "VatReturns");

            migrationBuilder.DropIndex(
                name: "IX_PayrollRuns_TaxYear_TaxMonth",
                table: "PayrollRuns");

            migrationBuilder.DropColumn(
                name: "DirectorsNiMethod",
                table: "Payslips");

            migrationBuilder.DropColumn(
                name: "EmployeeName",
                table: "Payslips");

            migrationBuilder.DropColumn(
                name: "EmployerNi",
                table: "Payslips");

            migrationBuilder.DropColumn(
                name: "NiCategory",
                table: "Payslips");

            migrationBuilder.DropColumn(
                name: "NiNumber",
                table: "Payslips");

            migrationBuilder.DropColumn(
                name: "StarterDeclaration",
                table: "Payslips");

            migrationBuilder.DropColumn(
                name: "TaxCode",
                table: "Payslips");

            migrationBuilder.DropColumn(
                name: "TaxMonth",
                table: "Payslips");

            migrationBuilder.DropColumn(
                name: "TaxYear",
                table: "Payslips");

            migrationBuilder.DropColumn(
                name: "YtdEmployeeNi",
                table: "Payslips");

            migrationBuilder.DropColumn(
                name: "YtdEmployerNi",
                table: "Payslips");

            migrationBuilder.DropColumn(
                name: "YtdGross",
                table: "Payslips");

            migrationBuilder.DropColumn(
                name: "YtdTax",
                table: "Payslips");

            migrationBuilder.DropColumn(
                name: "AutoPostImmediately",
                table: "PayrollSettings");

            migrationBuilder.DropColumn(
                name: "AutoRunDaysBefore",
                table: "PayrollSettings");

            migrationBuilder.DropColumn(
                name: "AutoRunEnabled",
                table: "PayrollSettings");

            migrationBuilder.DropColumn(
                name: "AutoRunLastTriggered",
                table: "PayrollSettings");

            migrationBuilder.DropColumn(
                name: "EmploymentAllowanceEligible",
                table: "PayrollSettings");

            migrationBuilder.DropColumn(
                name: "PayDayOfMonth",
                table: "PayrollSettings");

            migrationBuilder.DropColumn(
                name: "SmallEmployerRelief",
                table: "PayrollSettings");

            migrationBuilder.DropColumn(
                name: "FpsCorrelationId",
                table: "PayrollRuns");

            migrationBuilder.DropColumn(
                name: "FpsStatus",
                table: "PayrollRuns");

            migrationBuilder.DropColumn(
                name: "FpsSubmittedAt",
                table: "PayrollRuns");

            migrationBuilder.DropColumn(
                name: "TaxMonth",
                table: "PayrollRuns");

            migrationBuilder.DropColumn(
                name: "TaxYear",
                table: "PayrollRuns");

            migrationBuilder.DropColumn(
                name: "TotalEmployeeNi",
                table: "PayrollRuns");

            migrationBuilder.DropColumn(
                name: "TotalEmployerNi",
                table: "PayrollRuns");

            migrationBuilder.DropColumn(
                name: "TotalGross",
                table: "PayrollRuns");

            migrationBuilder.DropColumn(
                name: "TotalNetPay",
                table: "PayrollRuns");

            migrationBuilder.DropColumn(
                name: "TotalTax",
                table: "PayrollRuns");

            migrationBuilder.DropColumn(
                name: "ReminderCount",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "ReminderSentAt",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "IsRecurring",
                table: "Expenses");

            migrationBuilder.DropColumn(
                name: "RecurringFrequency",
                table: "Expenses");

            migrationBuilder.DropColumn(
                name: "RecurringNextDate",
                table: "Expenses");

            migrationBuilder.DropColumn(
                name: "AmapRate25p",
                table: "CompanySettings");

            migrationBuilder.DropColumn(
                name: "AmapRate45p",
                table: "CompanySettings");

            migrationBuilder.DropColumn(
                name: "AmapThresholdMiles",
                table: "CompanySettings");

            migrationBuilder.DropColumn(
                name: "Utr",
                table: "CompanySettings");

            migrationBuilder.DropColumn(
                name: "VatQuarterStartMonth",
                table: "CompanySettings");

            migrationBuilder.AlterColumn<bool>(
                name: "IsTrivialBenefit",
                table: "Expenses",
                type: "bit",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "bit",
                oldDefaultValue: false);

            migrationBuilder.AlterColumn<string>(
                name: "TrivialBenefitType",
                table: "DlaEntries",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);
        }
    }
}
