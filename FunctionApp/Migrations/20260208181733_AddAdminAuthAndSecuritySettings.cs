using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinanceHubFunctions.Migrations
{
    /// <inheritdoc />
    public partial class AddAdminAuthAndSecuritySettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AdminSessions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Token = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    AdminUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdminSessions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AdminUsers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Username = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    FullName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsSuperAdmin = table.Column<bool>(type: "bit", nullable: false),
                    MfaSecret = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    PasskeyCredentialId = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    PasskeyPublicKey = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastLoginDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastPasswordChange = table.Column<DateTime>(type: "datetime2", nullable: true),
                    MustChangePassword = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdminUsers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CompanyLedger",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Title = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    EntryType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    EffectiveDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    PeriodKey = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    TaxYear = table.Column<int>(type: "int", nullable: false),
                    FinancialYear = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompanyLedger", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CompanySettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Address = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CompanyAddress = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    PhoneNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    CompanyPhone = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    CompanyEmail = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    CompanyRegistrationNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    TaxRegistrationNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    VatRegistrationNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    VATNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    BankName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    BankAccountNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    AccountNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    BankSortCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    SortCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    BankIBAN = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    BankSwiftCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    DefaultCurrency = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CurrencySymbol = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: true),
                    DefaultVATRate = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    InvoicePrefix = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    QuotePrefix = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    InvoiceTermsDays = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    PaymentTerms = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    InvoiceFooterText = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    FooterText = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    LogoUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    InvoicesEmail = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    QuotesEmail = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    PaymentsEmail = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    CompanyInceptionDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FYStartMonth = table.Column<int>(type: "int", nullable: true),
                    FYStartDay = table.Column<int>(type: "int", nullable: true),
                    NextInvoiceNumber = table.Column<int>(type: "int", nullable: true),
                    NextQuoteNumber = table.Column<int>(type: "int", nullable: true),
                    SmtpServer = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    SmtpPort = table.Column<int>(type: "int", nullable: true),
                    SmtpFromAddress = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    SmtpUsername = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    SmtpPassword = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    DirectorName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    DirectorSignature = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    HasAuthorizedOfficer = table.Column<bool>(type: "bit", nullable: true),
                    AuthorizedOfficerName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    AuthorizedOfficerSignature = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompanySettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Customers",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    CustomerCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    CustomerName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    BillingEmail = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Phone = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Address = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    BillingAddress = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    DefaultDayRate = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    DefaultHourlyRate = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    IsVATRegistered = table.Column<bool>(type: "bit", nullable: false),
                    DefaultVATRate = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Customers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Employees",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmployeeNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    NationalInsuranceNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    TaxCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    AnnualSalary = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    PaymentSchedule = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    StartDate = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    BankAccountName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    BankAccountNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    BankSortCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Address = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    PhoneNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Employees", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Expenses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ExpenseId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Supplier = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    SupplierFreeText = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Reference = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Category = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    VATApplicability = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    VATIncluded = table.Column<bool>(type: "bit", nullable: false),
                    VATRate = table.Column<decimal>(type: "decimal(5,2)", nullable: true),
                    AmountNet = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    VATAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    AmountGross = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    EntryDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DatePaid = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PaymentMethod = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    TaxYear = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    FinancialYear = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ReceiptUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsDLA = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Expenses", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FinanceHubSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SmtpProvider = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SmtpServer = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    SmtpPort = table.Column<int>(type: "int", nullable: true),
                    SmtpUsername = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    SmtpPassword = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    SmtpFromAddress = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    SmtpFromName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    SmtpUseTLS = table.Column<bool>(type: "bit", nullable: true),
                    SmtpUseSSL = table.Column<bool>(type: "bit", nullable: true),
                    AuthenticationType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    SsoEnabled = table.Column<bool>(type: "bit", nullable: true),
                    RequireMfa = table.Column<bool>(type: "bit", nullable: true),
                    AllowPasskeys = table.Column<bool>(type: "bit", nullable: true),
                    MfaProvider = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    PasskeyProvider = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    SamlEnabled = table.Column<bool>(type: "bit", nullable: true),
                    SamlEntityId = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    SamlSsoUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    SamlSloUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    SamlCertificate = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    SamlIssuer = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    AzureAdTenantId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    AzureAdClientId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    AzureAdClientSecret = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    AzureAdRedirectUri = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    PaymentGatewayApiKey = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    PaymentGatewayProvider = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    BlobStorageConnectionString = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    InvoicesContainerName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ReceiptsContainerName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CertificatesContainerName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    DefaultTimeZone = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    DateFormat = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    TimeFormat = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    SessionTimeoutMinutes = table.Column<int>(type: "int", nullable: true),
                    MaintenanceMode = table.Column<bool>(type: "bit", nullable: true),
                    MaintenanceMessage = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FinanceHubSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Invoices",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InvoiceNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CustomerId = table.Column<int>(type: "int", nullable: true),
                    CustomerName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    BillingEmail = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    POReference = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    DateIssued = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DueDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DatePaid = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    AmountNet = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    DiscountPercent = table.Column<decimal>(type: "decimal(5,2)", nullable: true),
                    DiscountAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    DiscountNote = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    VATAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    AmountGross = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TaxYear = table.Column<int>(type: "int", nullable: true),
                    FinancialYear = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Invoices", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Quotes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    QuoteNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CustomerId = table.Column<int>(type: "int", nullable: true),
                    CustomerName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    ContactEmail = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    BillingEmail = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DateIssued = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ValidUntil = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    AmountNet = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    DiscountPercent = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    DiscountAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    DiscountNote = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    VATAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    AmountGross = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TaxYear = table.Column<int>(type: "int", nullable: false),
                    FinancialYear = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LinkedInvoiceId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Quotes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Shareholders",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    ShareholderType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ShareClassId = table.Column<int>(type: "int", nullable: true),
                    ShareClassName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    SharesOwned = table.Column<int>(type: "int", nullable: false),
                    ShareCertificateNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    DateOfIssue = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Email = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Address = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Shareholders", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Suppliers",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    SupplierCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Category = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    DefaultVATRate = table.Column<int>(type: "int", nullable: true),
                    PayeeType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    OnHold = table.Column<bool>(type: "bit", nullable: false),
                    PrimaryContact = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    RemittanceEmail = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Phone = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    PaymentMethod = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    PaymentTerms = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Currency = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    VATRegistration = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    AccountNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    SortCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    IBAN = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Suppliers", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "AdminUsers",
                columns: new[] { "Id", "CreatedDate", "Email", "FullName", "IsActive", "IsSuperAdmin", "LastLoginDate", "LastPasswordChange", "MfaSecret", "MustChangePassword", "PasskeyCredentialId", "PasskeyPublicKey", "PasswordHash", "Username" },
                values: new object[] { 1, new DateTime(2026, 2, 8, 18, 17, 32, 841, DateTimeKind.Utc).AddTicks(1674), "admin@financehub.local", "System Administrator", true, true, null, null, null, true, null, null, null, "admin" });

            migrationBuilder.InsertData(
                table: "CompanySettings",
                columns: new[] { "Id", "AccountNumber", "Address", "AuthorizedOfficerName", "AuthorizedOfficerSignature", "BankAccountNumber", "BankIBAN", "BankName", "BankSortCode", "BankSwiftCode", "CompanyAddress", "CompanyEmail", "CompanyInceptionDate", "CompanyName", "CompanyPhone", "CompanyRegistrationNumber", "CurrencySymbol", "DefaultCurrency", "DefaultVATRate", "DirectorName", "DirectorSignature", "Email", "FYStartDay", "FYStartMonth", "FooterText", "HasAuthorizedOfficer", "InvoiceFooterText", "InvoicePrefix", "InvoiceTermsDays", "InvoicesEmail", "LogoUrl", "NextInvoiceNumber", "NextQuoteNumber", "PaymentTerms", "PaymentsEmail", "PhoneNumber", "QuotePrefix", "QuotesEmail", "SmtpFromAddress", "SmtpPassword", "SmtpPort", "SmtpServer", "SmtpUsername", "SortCode", "TaxRegistrationNumber", "VATNumber", "VatRegistrationNumber" },
                values: new object[] { 1, null, null, null, null, null, null, null, null, null, null, null, null, "Default Company", null, null, "£", "GBP", null, null, null, null, 1, 4, null, null, null, null, null, null, null, 1, 1, null, null, null, null, null, null, null, null, null, null, null, null, null, null });

            migrationBuilder.InsertData(
                table: "FinanceHubSettings",
                columns: new[] { "Id", "AllowPasskeys", "AuthenticationType", "AzureAdClientId", "AzureAdClientSecret", "AzureAdRedirectUri", "AzureAdTenantId", "BlobStorageConnectionString", "CertificatesContainerName", "CreatedDate", "DateFormat", "DefaultTimeZone", "InvoicesContainerName", "LastModifiedBy", "LastModifiedDate", "MaintenanceMessage", "MaintenanceMode", "MfaProvider", "PasskeyProvider", "PaymentGatewayApiKey", "PaymentGatewayProvider", "ReceiptsContainerName", "RequireMfa", "SamlCertificate", "SamlEnabled", "SamlEntityId", "SamlIssuer", "SamlSloUrl", "SamlSsoUrl", "SessionTimeoutMinutes", "SmtpFromAddress", "SmtpFromName", "SmtpPassword", "SmtpPort", "SmtpProvider", "SmtpServer", "SmtpUseSSL", "SmtpUseTLS", "SmtpUsername", "SsoEnabled", "TimeFormat" },
                values: new object[] { 1, true, "Local", "bab79a2b-6427-4f8f-b7e9-ce804e573e9f", null, null, null, null, "certificates", null, "dd/MM/yyyy", "GMT Standard Time", "invoices", null, null, null, false, "TOTP", "WebAuthn", null, null, "receipts", true, null, null, null, null, null, null, 60, null, null, null, null, null, null, null, null, null, false, "HH:mm" });

            migrationBuilder.CreateIndex(
                name: "IX_AdminSessions_AdminUserId",
                table: "AdminSessions",
                column: "AdminUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AdminSessions_Token",
                table: "AdminSessions",
                column: "Token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AdminUsers_Email",
                table: "AdminUsers",
                column: "Email",
                unique: true,
                filter: "[Email] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AdminUsers_Username",
                table: "AdminUsers",
                column: "Username",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CompanyLedger_PeriodKey_EntryType",
                table: "CompanyLedger",
                columns: new[] { "PeriodKey", "EntryType" });

            migrationBuilder.CreateIndex(
                name: "IX_Customers_Code",
                table: "Customers",
                column: "Code");

            migrationBuilder.CreateIndex(
                name: "IX_Employees_EmployeeNumber",
                table: "Employees",
                column: "EmployeeNumber");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_InvoiceNumber",
                table: "Invoices",
                column: "InvoiceNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Quotes_QuoteNumber",
                table: "Quotes",
                column: "QuoteNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Suppliers_Code",
                table: "Suppliers",
                column: "Code");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AdminSessions");

            migrationBuilder.DropTable(
                name: "AdminUsers");

            migrationBuilder.DropTable(
                name: "CompanyLedger");

            migrationBuilder.DropTable(
                name: "CompanySettings");

            migrationBuilder.DropTable(
                name: "Customers");

            migrationBuilder.DropTable(
                name: "Employees");

            migrationBuilder.DropTable(
                name: "Expenses");

            migrationBuilder.DropTable(
                name: "FinanceHubSettings");

            migrationBuilder.DropTable(
                name: "Invoices");

            migrationBuilder.DropTable(
                name: "Quotes");

            migrationBuilder.DropTable(
                name: "Shareholders");

            migrationBuilder.DropTable(
                name: "Suppliers");
        }
    }
}
