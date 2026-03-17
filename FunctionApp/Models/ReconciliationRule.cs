using System;

namespace FinanceHubFunctions.Models
{
    public class ReconciliationRule
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public string? MatchText { get; set; }
        public string? Counterparty { get; set; }
        public decimal? AmountMin { get; set; }
        public decimal? AmountMax { get; set; }
        public string? Category { get; set; }
        public bool AutoMatch { get; set; }
        public bool IsActive { get; set; }
        public DateTime? CreatedDate { get; set; }
        public DateTime? ModifiedDate { get; set; }
    }
}
