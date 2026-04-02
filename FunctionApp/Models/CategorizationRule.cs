using System;

namespace FinanceHubFunctions.Models
{
    public class CategorizationRule
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string? MatchPattern { get; set; } // regex or substring to match
        public string MatchField { get; set; } = "Description"; // Description, MerchantName, Reference
        public string? Direction { get; set; } // In, Out, or null for both
        public decimal? AmountMin { get; set; }
        public decimal? AmountMax { get; set; }
        public string TargetCategory { get; set; } // Category to assign
        public int Priority { get; set; } = 100; // Lower = higher priority
        public bool IsActive { get; set; } = true;
        public DateTime? CreatedDate { get; set; }
        public DateTime? ModifiedDate { get; set; }
    }
}
