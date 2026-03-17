using System;

namespace FinanceHubFunctions.Models
{
    public class CompanyLedgerEntry
    {
        public int Id { get; set; }
        public string? Title { get; set; }
        public string EntryType { get; set; } = string.Empty; // Salary, EmployeeNI, EmployerNI, PAYE, DLA_In, DLA_Out, DLA_Payment, CorpTax_Reserve, CorpTax_Paid, Dividend_Declared, Dividend_Paid, VAT_Paid, VAT_Reclaim
        public decimal Amount { get; set; }
        public DateTime EffectiveDate { get; set; }
        public string? Notes { get; set; }
        
        // DLA Payment tracking
        public string? DlaReference { get; set; } // Links to DlaEntry.DlaId for DLA_Payment entries
        public bool? IsFullPayment { get; set; } // True if paying off DLA in full, false if partial payment
        
        public string PeriodKey { get; set; } = string.Empty; // e.g. "2025-04"
        public int TaxYear { get; set; }
        public string? FinancialYear { get; set; }
    }

    public class CompanyAggregates
    {
        public decimal SalaryGross { get; set; }
        public decimal EmployeeNI { get; set; }
        public decimal EmployerNI { get; set; }
        public decimal PayeRemitted { get; set; }
        public decimal CorpTaxReserved { get; set; }
        public decimal CorpTaxPaid { get; set; }
        public decimal DividendsDeclared { get; set; }
        public decimal DividendsPaid { get; set; }
        public decimal DlaNet { get; set; } // DLA_In - DLA_Out
    }

    public class CompanyOverview
    {
        public decimal TradingProfit { get; set; }
        public decimal SalaryGross { get; set; }
        public decimal EmployerNI { get; set; }
        public decimal ProfitBeforeTax { get; set; }
        public decimal CorporationTaxEstimate { get; set; }
        public decimal AvailableForDividends { get; set; }
        public decimal VatPot { get; set; }
        public decimal CorpTaxPot { get; set; }
        public decimal BankBalance { get; set; }
        public decimal FreeCashAfterPots { get; set; }
        public decimal DlaBalance { get; set; }
    }

    public class TaxConfig
    {
        public decimal SmallProfitsRate { get; set; } = 0.19m;
        public decimal MainRate { get; set; } = 0.25m;
        public decimal LimitLower { get; set; } = 50000m;
        public decimal LimitUpper { get; set; } = 250000m;
    }
}
