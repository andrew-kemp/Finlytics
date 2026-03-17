using System;

namespace FinanceHubFunctions.Models
{
    public class ReconciliationMatch
    {
        public int Id { get; set; }
        public int BankTransactionId { get; set; }
        public string? RelatedType { get; set; } // Invoice, Expense, Transfer, Payroll, Other
        public string? RelatedId { get; set; }
        public string? MatchType { get; set; } // Auto, Manual
        public string? Notes { get; set; }
        public DateTime? CreatedDate { get; set; }
    }
}
