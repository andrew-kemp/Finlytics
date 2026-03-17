#nullable enable
using System;
using System.Collections.Generic;

namespace FinanceHubFunctions.Models
{
    // ═══════════════════════════════════════════════════════════
    //  PROFIT & LOSS REPORT
    // ═══════════════════════════════════════════════════════════

    public class ProfitAndLossReport
    {
        public string FinancialYear { get; set; } = string.Empty;
        public DateTime PeriodStart { get; set; }
        public DateTime PeriodEnd { get; set; }
        public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;

        // Revenue
        public decimal TotalRevenue { get; set; }
        public List<RevenueBreakdown> RevenueByCustomer { get; set; } = new();
        public List<MonthlyAmount> RevenueByMonth { get; set; } = new();

        // Cost of Sales (direct costs)
        public decimal CostOfSales { get; set; }

        // Gross Profit
        public decimal GrossProfit { get; set; }
        public decimal GrossProfitMargin { get; set; } // percentage

        // Operating Expenses
        public decimal TotalOperatingExpenses { get; set; }
        public List<ExpenseCategoryBreakdown> ExpensesByCategory { get; set; } = new();
        public decimal StaffCosts { get; set; }           // Salary + EmployerNI
        public decimal SalaryGross { get; set; }
        public decimal EmployerNI { get; set; }
        public decimal PensionContributions { get; set; }
        public decimal Depreciation { get; set; }
        public decimal MileageClaims { get; set; }
        public decimal SubscriptionCosts { get; set; }

        // Operating Profit
        public decimal OperatingProfit { get; set; }

        // Other Income / Expenses
        public decimal BankInterestReceived { get; set; }
        public decimal BankCharges { get; set; }

        // Profit Before Tax
        public decimal ProfitBeforeTax { get; set; }

        // Corporation Tax
        public decimal CorporationTaxEstimate { get; set; }
        public string CorporationTaxRate { get; set; } = string.Empty;

        // Net Profit
        public decimal NetProfit { get; set; }
        public decimal NetProfitMargin { get; set; } // percentage

        // Dividends
        public decimal DividendsDeclared { get; set; }
        public decimal RetainedProfit { get; set; }
    }

    public class RevenueBreakdown
    {
        public string CustomerName { get; set; } = string.Empty;
        public int InvoiceCount { get; set; }
        public decimal AmountNet { get; set; }
        public decimal Percentage { get; set; }
    }

    public class ExpenseCategoryBreakdown
    {
        public string Category { get; set; } = string.Empty;
        public int Count { get; set; }
        public decimal AmountNet { get; set; }
        public decimal Percentage { get; set; }
    }

    public class MonthlyAmount
    {
        public string Month { get; set; } = string.Empty; // e.g. "2025-04"
        public decimal Amount { get; set; }
    }

    // ═══════════════════════════════════════════════════════════
    //  BALANCE SHEET
    // ═══════════════════════════════════════════════════════════

    public class BalanceSheetReport
    {
        public string AsAtDate { get; set; } = string.Empty;
        public string FinancialYear { get; set; } = string.Empty;
        public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;

        // Fixed Assets
        public decimal FixedAssetsCost { get; set; }
        public decimal FixedAssetsDepreciation { get; set; }
        public decimal FixedAssetsNetBookValue { get; set; }
        public List<AssetCategoryBreakdown> FixedAssetsByCategory { get; set; } = new();

        // Current Assets
        public decimal TradeDebtors { get; set; }         // Unpaid issued invoices
        public decimal BankBalance { get; set; }           // Sum of bank accounts
        public decimal DirectorLoanOwedToCompany { get; set; } // DLA balance where director owes company
        public decimal VatReclaimable { get; set; }        // Input VAT owed back
        public decimal TotalCurrentAssets { get; set; }

        // Current Liabilities
        public decimal TradeCreditors { get; set; }        // Unpaid bills/expenses
        public decimal VatOwed { get; set; }               // Output VAT due to HMRC
        public decimal PayeOwed { get; set; }              // PAYE/NI due to HMRC
        public decimal CorporationTaxOwed { get; set; }    // CT reserved but not paid
        public decimal DividendsDeclaredUnpaid { get; set; }
        public decimal DirectorLoanOwedToDirector { get; set; } // DLA balance where company owes director
        public decimal TotalCurrentLiabilities { get; set; }

        // Net Current Assets
        public decimal NetCurrentAssets { get; set; }

        // Total Net Assets
        public decimal TotalNetAssets { get; set; }

        // Capital & Reserves
        public decimal ShareCapital { get; set; }
        public decimal RetainedEarnings { get; set; }
        public decimal TotalCapitalAndReserves { get; set; }
    }

    public class AssetCategoryBreakdown
    {
        public string Category { get; set; } = string.Empty;
        public int Count { get; set; }
        public decimal Cost { get; set; }
        public decimal Depreciation { get; set; }
        public decimal NetBookValue { get; set; }
    }

    // ═══════════════════════════════════════════════════════════
    //  AGED DEBTORS
    // ═══════════════════════════════════════════════════════════

    public class AgedDebtorsReport
    {
        public DateTime AsAtDate { get; set; }
        public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
        public decimal TotalOutstanding { get; set; }
        public int TotalInvoices { get; set; }

        // Aging buckets
        public decimal Current { get; set; }      // Not yet due
        public decimal Days1To30 { get; set; }
        public decimal Days31To60 { get; set; }
        public decimal Days61To90 { get; set; }
        public decimal Days90Plus { get; set; }

        public List<AgedDebtorCustomer> Customers { get; set; } = new();
    }

    public class AgedDebtorCustomer
    {
        public string CustomerName { get; set; } = string.Empty;
        public string? CustomerId { get; set; }
        public decimal TotalOutstanding { get; set; }
        public decimal Current { get; set; }
        public decimal Days1To30 { get; set; }
        public decimal Days31To60 { get; set; }
        public decimal Days61To90 { get; set; }
        public decimal Days90Plus { get; set; }
        public List<AgedInvoice> Invoices { get; set; } = new();
    }

    public class AgedInvoice
    {
        public string InvoiceNumber { get; set; } = string.Empty;
        public DateTime DateIssued { get; set; }
        public DateTime? DueDate { get; set; }
        public decimal AmountGross { get; set; }
        public int DaysOverdue { get; set; }
        public string AgeBucket { get; set; } = string.Empty;
    }

    // ═══════════════════════════════════════════════════════════
    //  AUDIT TRAIL
    // ═══════════════════════════════════════════════════════════

    public class AuditEntry
    {
        public int Id { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public string UserName { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;   // Created, Updated, Deleted
        public string EntityType { get; set; } = string.Empty; // Invoice, Expense, DLA, etc.
        public string? EntityId { get; set; }
        public string? EntityRef { get; set; }                 // e.g. INV-2026-001
        public string? Summary { get; set; }                   // Human-readable description
        public string? OldValues { get; set; }                 // JSON snapshot of changed fields (before)
        public string? NewValues { get; set; }                 // JSON snapshot of changed fields (after)
    }
}
