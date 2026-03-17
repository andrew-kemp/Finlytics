using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using FinanceHubFunctions.Models;
using FinanceHubFunctions.Services;
using FinanceHubFunctions.Data;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace FinanceHubFunctions.Functions
{
    public class ReportFunctions
    {
        private readonly ILogger _logger;
        private readonly IInvoiceRepository _invoiceRepo;
        private readonly IExpenseRepository _expenseRepo;
        private readonly IDlaRepository _dlaRepo;
        private readonly IDlaPaymentRepository _dlaPaymentRepo;
        private readonly ICompanyLedgerRepository _ledgerRepo;
        private readonly IAssetRepository _assetRepo;
        private readonly ISubscriptionRepository _subscriptionRepo;
        private readonly IPayrollRunRepository _payrollRunRepo;
        private readonly IPayslipRepository _payslipRepo;
        private readonly ICompanySettingsRepository _settingsRepo;
        private readonly IBankAccountRepository _bankAccountRepo;
        private readonly IShareholderRepository _shareholderRepo;
        private readonly IMileageTripRepository _mileageRepo;
        private readonly IVatReturnRepository _vatReturnRepo;

        public ReportFunctions(
            ILoggerFactory loggerFactory,
            IInvoiceRepository invoiceRepo,
            IExpenseRepository expenseRepo,
            IDlaRepository dlaRepo,
            IDlaPaymentRepository dlaPaymentRepo,
            ICompanyLedgerRepository ledgerRepo,
            IAssetRepository assetRepo,
            ISubscriptionRepository subscriptionRepo,
            IPayrollRunRepository payrollRunRepo,
            IPayslipRepository payslipRepo,
            ICompanySettingsRepository settingsRepo,
            IBankAccountRepository bankAccountRepo,
            IShareholderRepository shareholderRepo,
            IMileageTripRepository mileageRepo,
            IVatReturnRepository vatReturnRepo)
        {
            _logger = loggerFactory.CreateLogger<ReportFunctions>();
            _invoiceRepo = invoiceRepo;
            _expenseRepo = expenseRepo;
            _dlaRepo = dlaRepo;
            _dlaPaymentRepo = dlaPaymentRepo;
            _ledgerRepo = ledgerRepo;
            _assetRepo = assetRepo;
            _subscriptionRepo = subscriptionRepo;
            _payrollRunRepo = payrollRunRepo;
            _payslipRepo = payslipRepo;
            _settingsRepo = settingsRepo;
            _bankAccountRepo = bankAccountRepo;
            _shareholderRepo = shareholderRepo;
            _mileageRepo = mileageRepo;
            _vatReturnRepo = vatReturnRepo;
        }

        // ═══════════════════════════════════════════════════════════
        //  PROFIT & LOSS REPORT
        // ═══════════════════════════════════════════════════════════

        [Function("GetProfitAndLoss")]
        public async Task<HttpResponseData> GetProfitAndLoss(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "reports/profit-and-loss/{startYearStr}/{endYearShort}")] HttpRequestData req,
            string startYearStr, string endYearShort)
        {
            var financialYear = $"{startYearStr}/{endYearShort}";
            _logger.LogInformation("GetProfitAndLoss triggered for FY: {FY}", financialYear);

            try
            {
                var settings = await _settingsRepo.GetDefaultAsync();
                int fyStartMonth = settings?.FYStartMonth ?? 4;
                int fyStartDay = settings?.FYStartDay ?? 1;

                // Parse financial year e.g. "2025/26" → start = 2025
                var startYear = int.Parse(startYearStr);
                var periodStart = new DateTime(startYear, fyStartMonth, fyStartDay);
                var periodEnd = new DateTime(startYear + 1, fyStartMonth, fyStartDay).AddDays(-1);

                // ── Fetch all data sequentially (shared DbContext) ──
                var allInvoices = await _invoiceRepo.GetAllAsync();
                var allExpenses = await _expenseRepo.GetAllAsync();
                var allAssets = await _assetRepo.GetAllAsync();
                var allSubscriptions = await _subscriptionRepo.GetAllAsync();
                var allPayrollRuns = await _payrollRunRepo.GetAllAsync();
                var allMileage = await _mileageRepo.GetAllAsync();
                var allLedger = await _ledgerRepo.GetAllAsync();
                var allDla = await _dlaRepo.GetAllAsync();

                // ── Filter to financial year ──
                var invoices = allInvoices
                    .Where(i => i.FinancialYear == financialYear ||
                               (i.DateIssued >= periodStart && i.DateIssued <= periodEnd))
                    .ToList();

                var paidInvoices = invoices.Where(i => i.Status == "Paid" || i.Status == "Issued").ToList();

                var expenses = allExpenses
                    .Where(e => e.FinancialYear == financialYear ||
                               (e.EntryDate.HasValue && e.EntryDate.Value >= periodStart && e.EntryDate.Value <= periodEnd))
                    .Where(e => !e.IsDLA) // exclude DLA entries from operating expenses
                    .ToList();

                var ledgerEntries = allLedger
                    .Where(l => l.FinancialYear == financialYear ||
                               (l.EffectiveDate >= periodStart && l.EffectiveDate <= periodEnd))
                    .ToList();

                var mileageTrips = allMileage
                    .Where(m => m.TripDate >= periodStart && m.TripDate <= periodEnd)
                    .Where(m => m.Status == "Claimed" || m.Status == "Paid")
                    .ToList();

                // ── Revenue ──
                decimal totalRevenue = paidInvoices.Sum(i => i.AmountNet);

                var revenueByCustomer = paidInvoices
                    .GroupBy(i => i.CustomerName ?? "Unknown")
                    .Select(g => new RevenueBreakdown
                    {
                        CustomerName = g.Key,
                        InvoiceCount = g.Count(),
                        AmountNet = g.Sum(i => i.AmountNet),
                        Percentage = totalRevenue > 0 ? Math.Round(g.Sum(i => i.AmountNet) / totalRevenue * 100, 1) : 0
                    })
                    .OrderByDescending(r => r.AmountNet)
                    .ToList();

                var revenueByMonth = paidInvoices
                    .GroupBy(i => i.DateIssued.ToString("yyyy-MM"))
                    .Select(g => new MonthlyAmount { Month = g.Key, Amount = g.Sum(i => i.AmountNet) })
                    .OrderBy(m => m.Month)
                    .ToList();

                // ── Operating Expenses (from Expenses table, excluding DLA) ──
                var expensesByCategory = expenses
                    .Where(e => e.CtTag != "NonCT") // exclude non-deductible
                    .GroupBy(e => e.Category ?? "Uncategorised")
                    .Select(g => new ExpenseCategoryBreakdown
                    {
                        Category = g.Key,
                        Count = g.Count(),
                        AmountNet = g.Sum(e => e.AmountNet ?? 0),
                    })
                    .OrderByDescending(e => e.AmountNet)
                    .ToList();

                decimal totalOpex = expenses.Where(e => e.CtTag == "Revenue" || e.CtTag == null).Sum(e => e.AmountNet ?? 0);

                // Assign percentages
                foreach (var cat in expensesByCategory)
                    cat.Percentage = totalOpex > 0 ? Math.Round(cat.AmountNet / totalOpex * 100, 1) : 0;

                // ── Staff Costs (from Ledger) ──
                decimal salaryGross = ledgerEntries.Where(l => l.EntryType == "Salary").Sum(l => l.Amount);
                decimal employerNI = ledgerEntries.Where(l => l.EntryType == "EmployerNI").Sum(l => l.Amount);
                decimal staffCosts = salaryGross + employerNI;

                // ── Depreciation (from Assets) ──
                decimal depreciation = 0;
                foreach (var asset in allAssets.Where(a => a.Status == "In Use" && a.DepreciationMethod != "None"))
                {
                    if ((asset.UsefulLifeYears ?? 0) > 0 && (asset.PurchasePrice ?? 0) > 0)
                    {
                        decimal annualDep = ((asset.PurchasePrice ?? 0) - (asset.ResidualValue ?? 0)) / (asset.UsefulLifeYears ?? 1);
                        depreciation += annualDep;
                    }
                }

                // ── Mileage Claims ──
                decimal mileageClaims = mileageTrips.Sum(m => m.TotalAmount);

                // ── Subscription Costs ──
                decimal subscriptionCosts = allSubscriptions
                    .Where(s => s.Status == "Active")
                    .Sum(s =>
                    {
                        var monthlyCost = s.CostPerCycle ?? 0;
                        if (s.BillingCycle == "Annual") monthlyCost /= 12;
                        else if (s.BillingCycle == "One-time") monthlyCost = 0;
                        return monthlyCost * 12; // annualise
                    });

                // ── Dividends ──
                decimal dividendsDeclared = ledgerEntries
                    .Where(l => l.EntryType == "Dividend_Declared")
                    .Sum(l => l.Amount);

                // ── Calculate P&L lines ──
                decimal grossProfit = totalRevenue; // Service company — no cost of sales
                decimal totalOperatingExpenses = totalOpex + staffCosts + depreciation + mileageClaims + subscriptionCosts;
                decimal operatingProfit = grossProfit - totalOperatingExpenses;
                decimal profitBeforeTax = operatingProfit;

                // Corporation Tax
                var taxConfig = new TaxConfig();
                decimal ctEstimate = 0;
                string ctRate = "N/A";
                if (profitBeforeTax > 0)
                {
                    if (profitBeforeTax <= taxConfig.LimitLower)
                    {
                        ctEstimate = profitBeforeTax * taxConfig.SmallProfitsRate;
                        ctRate = "19% (Small Profits)";
                    }
                    else if (profitBeforeTax >= taxConfig.LimitUpper)
                    {
                        ctEstimate = profitBeforeTax * taxConfig.MainRate;
                        ctRate = "25% (Main Rate)";
                    }
                    else
                    {
                        ctEstimate = profitBeforeTax * taxConfig.MainRate
                                     - (3m / 200m) * (taxConfig.LimitUpper - profitBeforeTax);
                        ctRate = $"{Math.Round(ctEstimate / profitBeforeTax * 100, 2)}% (Marginal Relief)";
                    }
                }

                decimal netProfit = profitBeforeTax - ctEstimate;

                var report = new ProfitAndLossReport
                {
                    FinancialYear = financialYear,
                    PeriodStart = periodStart,
                    PeriodEnd = periodEnd,
                    TotalRevenue = totalRevenue,
                    RevenueByCustomer = revenueByCustomer,
                    RevenueByMonth = revenueByMonth,
                    CostOfSales = 0, // Service company
                    GrossProfit = grossProfit,
                    GrossProfitMargin = totalRevenue > 0 ? Math.Round(grossProfit / totalRevenue * 100, 1) : 0,
                    TotalOperatingExpenses = totalOperatingExpenses,
                    ExpensesByCategory = expensesByCategory,
                    StaffCosts = staffCosts,
                    SalaryGross = salaryGross,
                    EmployerNI = employerNI,
                    PensionContributions = 0,
                    Depreciation = depreciation,
                    MileageClaims = mileageClaims,
                    SubscriptionCosts = subscriptionCosts,
                    OperatingProfit = operatingProfit,
                    ProfitBeforeTax = profitBeforeTax,
                    CorporationTaxEstimate = Math.Round(ctEstimate, 2),
                    CorporationTaxRate = ctRate,
                    NetProfit = Math.Round(netProfit, 2),
                    NetProfitMargin = totalRevenue > 0 ? Math.Round(netProfit / totalRevenue * 100, 1) : 0,
                    DividendsDeclared = dividendsDeclared,
                    RetainedProfit = Math.Round(netProfit - dividendsDeclared, 2),
                };

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(report);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating P&L report");
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteStringAsync($"Error: {ex.Message}");
                return errorResponse;
            }
        }

        // ═══════════════════════════════════════════════════════════
        //  BALANCE SHEET
        // ═══════════════════════════════════════════════════════════

        [Function("GetBalanceSheet")]
        public async Task<HttpResponseData> GetBalanceSheet(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "reports/balance-sheet/{startYearStr}/{endYearShort}")] HttpRequestData req,
            string startYearStr, string endYearShort)
        {
            var financialYear = $"{startYearStr}/{endYearShort}";
            _logger.LogInformation("GetBalanceSheet triggered for FY: {FY}", financialYear);

            try
            {
                var settings = await _settingsRepo.GetDefaultAsync();
                int fyStartMonth = settings?.FYStartMonth ?? 4;
                int fyStartDay = settings?.FYStartDay ?? 1;
                var startYear = int.Parse(startYearStr);
                var periodEnd = new DateTime(startYear + 1, fyStartMonth, fyStartDay).AddDays(-1);

                // ── Fetch all data sequentially (shared DbContext) ──
                var allInvoices = await _invoiceRepo.GetAllAsync();
                var allExpenses = await _expenseRepo.GetAllAsync();
                var allAssets = await _assetRepo.GetAllAsync();
                var allBankAccounts = await _bankAccountRepo.GetAllAsync();
                var allDla = await _dlaRepo.GetAllAsync();
                var allLedger = await _ledgerRepo.GetAllAsync();
                var allShareholders = await _shareholderRepo.GetAllAsync();
                var allVatReturns = await _vatReturnRepo.GetAllAsync();

                var ledgerInFY = allLedger
                    .Where(l => l.FinancialYear == financialYear ||
                               (l.EffectiveDate <= periodEnd))
                    .ToList();

                // ── Fixed Assets ──
                var activeAssets = allAssets.Where(a => a.Status != "Disposed" && a.Status != "Lost").ToList();
                decimal fixedAssetsCost = activeAssets.Sum(a => a.PurchasePrice ?? 0);
                decimal fixedAssetsDepreciation = 0;

                foreach (var asset in activeAssets)
                {
                    if ((asset.UsefulLifeYears ?? 0) > 0 && asset.DepreciationMethod != "None" && asset.PurchaseDate.HasValue)
                    {
                        var yearsUsed = Math.Min(
                            (decimal)(periodEnd - asset.PurchaseDate.Value).TotalDays / 365.25m,
                            asset.UsefulLifeYears ?? 1);
                        if (yearsUsed > 0)
                        {
                            decimal annualDep = ((asset.PurchasePrice ?? 0) - (asset.ResidualValue ?? 0)) / (asset.UsefulLifeYears ?? 1);
                            fixedAssetsDepreciation += annualDep * yearsUsed;
                        }
                    }
                }

                var assetsByCategory = activeAssets
                    .GroupBy(a => a.Category ?? "Uncategorised")
                    .Select(g => new AssetCategoryBreakdown
                    {
                        Category = g.Key,
                        Count = g.Count(),
                        Cost = g.Sum(a => a.PurchasePrice ?? 0),
                        NetBookValue = g.Sum(a => a.CurrentValue ?? a.PurchasePrice ?? 0)
                    })
                    .OrderByDescending(a => a.Cost)
                    .ToList();

                // ── Trade Debtors (outstanding invoices) ──
                decimal tradeDebtors = allInvoices
                    .Where(i => (i.Status == "Issued" || i.Status == "Overdue") && i.DateIssued <= periodEnd)
                    .Sum(i => i.AmountGross);

                // ── Bank Balances ──
                decimal bankBalance = allBankAccounts
                    .Where(b => b.IsActive)
                    .Sum(b => b.OpeningBalance ?? 0);

                // ── DLA Balances ──
                decimal dlaOwedToCompany = 0;
                decimal dlaOwedToDirector = 0;
                foreach (var entry in allDla)
                {
                    if (entry.Direction == "OwedToCompany")
                        dlaOwedToCompany += entry.RemainingBalance > 0 ? entry.RemainingBalance : entry.AmountGross;
                    else if (entry.Direction == "OwedToDirector")
                        dlaOwedToDirector += entry.RemainingBalance > 0 ? entry.RemainingBalance : entry.AmountGross;
                }

                // ── Current Liabilities ──
                decimal tradeCreditors = allExpenses
                    .Where(e => e.DatePaid == null && e.EntryDate <= periodEnd)
                    .Sum(e => e.AmountGross ?? 0);

                // PAYE/NI from ledger (accrued but not yet paid)
                decimal payeOwed = ledgerInFY
                    .Where(l => l.EntryType == "Salary" || l.EntryType == "EmployeeNI" || l.EntryType == "EmployerNI" || l.EntryType == "PAYE")
                    .Sum(l => l.EntryType == "PAYE" ? -l.Amount : (l.EntryType == "EmployeeNI" || l.EntryType == "EmployerNI" ? l.Amount : 0));

                decimal corpTaxOwed = ledgerInFY
                    .Where(l => l.EntryType == "CorpTax_Reserve").Sum(l => l.Amount)
                    - ledgerInFY.Where(l => l.EntryType == "CorpTax_Paid").Sum(l => l.Amount);

                decimal vatOwed = 0; // simplified — would need VAT return data

                decimal dividendsUnpaid = ledgerInFY
                    .Where(l => l.EntryType == "Dividend_Declared").Sum(l => l.Amount)
                    - ledgerInFY.Where(l => l.EntryType == "Dividend_Paid").Sum(l => l.Amount);

                // ── Share Capital ──
                decimal shareCapital = allShareholders
                    .Where(s => s.IsActive)
                    .Sum(s => s.SharesOwned) * 1m; // £1 par value assumed

                // ── Build Balance Sheet ──
                decimal fixedAssetsNBV = fixedAssetsCost - fixedAssetsDepreciation;
                decimal totalCurrentAssets = tradeDebtors + bankBalance + dlaOwedToCompany;
                decimal totalCurrentLiabilities = tradeCreditors + Math.Max(0, vatOwed)
                    + Math.Max(0, payeOwed) + Math.Max(0, corpTaxOwed)
                    + Math.Max(0, dividendsUnpaid) + dlaOwedToDirector;
                decimal netCurrentAssets = totalCurrentAssets - totalCurrentLiabilities;
                decimal totalNetAssets = fixedAssetsNBV + netCurrentAssets;
                decimal retainedEarnings = totalNetAssets - shareCapital;

                var report = new BalanceSheetReport
                {
                    AsAtDate = periodEnd.ToString("yyyy-MM-dd"),
                    FinancialYear = financialYear,
                    FixedAssetsCost = fixedAssetsCost,
                    FixedAssetsDepreciation = Math.Round(fixedAssetsDepreciation, 2),
                    FixedAssetsNetBookValue = Math.Round(fixedAssetsNBV, 2),
                    FixedAssetsByCategory = assetsByCategory,
                    TradeDebtors = tradeDebtors,
                    BankBalance = bankBalance,
                    DirectorLoanOwedToCompany = dlaOwedToCompany,
                    TotalCurrentAssets = totalCurrentAssets,
                    TradeCreditors = tradeCreditors,
                    VatOwed = Math.Max(0, vatOwed),
                    PayeOwed = Math.Max(0, payeOwed),
                    CorporationTaxOwed = Math.Max(0, corpTaxOwed),
                    DividendsDeclaredUnpaid = Math.Max(0, dividendsUnpaid),
                    DirectorLoanOwedToDirector = dlaOwedToDirector,
                    TotalCurrentLiabilities = totalCurrentLiabilities,
                    NetCurrentAssets = netCurrentAssets,
                    TotalNetAssets = totalNetAssets,
                    ShareCapital = shareCapital,
                    RetainedEarnings = Math.Round(retainedEarnings, 2),
                    TotalCapitalAndReserves = Math.Round(totalNetAssets, 2),
                };

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(report);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating Balance Sheet");
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteStringAsync($"Error: {ex.Message}");
                return errorResponse;
            }
        }

        // ═══════════════════════════════════════════════════════════
        //  AGED DEBTORS
        // ═══════════════════════════════════════════════════════════

        [Function("GetAgedDebtors")]
        public async Task<HttpResponseData> GetAgedDebtors(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "reports/aged-debtors")] HttpRequestData req)
        {
            _logger.LogInformation("GetAgedDebtors triggered");

            try
            {
                var allInvoices = await _invoiceRepo.GetAllAsync();
                var today = DateTime.UtcNow.Date;

                var unpaid = allInvoices
                    .Where(i => i.Status == "Issued" || i.Status == "Overdue")
                    .ToList();

                var report = new AgedDebtorsReport
                {
                    AsAtDate = today,
                    TotalOutstanding = unpaid.Sum(i => i.AmountGross),
                    TotalInvoices = unpaid.Count,
                };

                foreach (var inv in unpaid)
                {
                    var dueDate = inv.DueDate ?? inv.DateIssued.AddDays(30);
                    int daysOverdue = Math.Max(0, (int)(today - dueDate).TotalDays);
                    string bucket;

                    if (daysOverdue <= 0) { report.Current += inv.AmountGross; bucket = "Current"; }
                    else if (daysOverdue <= 30) { report.Days1To30 += inv.AmountGross; bucket = "1-30 days"; }
                    else if (daysOverdue <= 60) { report.Days31To60 += inv.AmountGross; bucket = "31-60 days"; }
                    else if (daysOverdue <= 90) { report.Days61To90 += inv.AmountGross; bucket = "61-90 days"; }
                    else { report.Days90Plus += inv.AmountGross; bucket = "90+ days"; }

                    var customer = report.Customers.FirstOrDefault(c => c.CustomerName == (inv.CustomerName ?? "Unknown"));
                    if (customer == null)
                    {
                        customer = new AgedDebtorCustomer
                        {
                            CustomerName = inv.CustomerName ?? "Unknown",
                            CustomerId = inv.CustomerId,
                        };
                        report.Customers.Add(customer);
                    }

                    customer.TotalOutstanding += inv.AmountGross;
                    switch (bucket)
                    {
                        case "Current": customer.Current += inv.AmountGross; break;
                        case "1-30 days": customer.Days1To30 += inv.AmountGross; break;
                        case "31-60 days": customer.Days31To60 += inv.AmountGross; break;
                        case "61-90 days": customer.Days61To90 += inv.AmountGross; break;
                        case "90+ days": customer.Days90Plus += inv.AmountGross; break;
                    }

                    customer.Invoices.Add(new AgedInvoice
                    {
                        InvoiceNumber = inv.InvoiceNumber,
                        DateIssued = inv.DateIssued,
                        DueDate = inv.DueDate,
                        AmountGross = inv.AmountGross,
                        DaysOverdue = daysOverdue,
                        AgeBucket = bucket,
                    });
                }

                // Sort customers by total outstanding desc
                report.Customers = report.Customers.OrderByDescending(c => c.TotalOutstanding).ToList();

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(report);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating Aged Debtors report");
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteStringAsync($"Error: {ex.Message}");
                return errorResponse;
            }
        }

        // ═══════════════════════════════════════════════════════════
        //  AUDIT TRAIL (read-only endpoint — entries created by middleware)
        // ═══════════════════════════════════════════════════════════

        [Function("GetAuditTrail")]
        public async Task<HttpResponseData> GetAuditTrail(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "reports/audit-trail")] HttpRequestData req)
        {
            _logger.LogInformation("GetAuditTrail triggered");

            try
            {
                // Parse optional query params
                var query = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
                var entityType = query["entityType"];
                var action = query["action"];
                var fromStr = query["from"];
                var toStr = query["to"];
                var limitStr = query["limit"];

                int limit = 200;
                if (int.TryParse(limitStr, out var parsedLimit)) limit = Math.Min(parsedLimit, 1000);

                DateTime? from = null;
                DateTime? to = null;
                if (DateTime.TryParse(fromStr, out var parsedFrom)) from = parsedFrom;
                if (DateTime.TryParse(toStr, out var parsedTo)) to = parsedTo;

                // For now return empty — audit entries will be populated once we add the AuditService
                // This endpoint structure is ready for the audit trail implementation
                var entries = new List<AuditEntry>();

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(new { entries, total = entries.Count });
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching audit trail");
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteStringAsync($"Error: {ex.Message}");
                return errorResponse;
            }
        }
    }
}
