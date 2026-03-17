using System;

namespace FinanceHubFunctions.Models
{
    public class MileageClaim
    {
        public int Id { get; set; }
        public string ClaimRef { get; set; } = string.Empty;        // MILCLAIM-2025-001
        public string Director { get; set; } = string.Empty;
        public DateTime PeriodStart { get; set; }
        public DateTime PeriodEnd { get; set; }
        public string TaxYear { get; set; } = string.Empty;
        public decimal TotalMiles { get; set; } = 0m;
        public decimal MilesAt45p { get; set; } = 0m;
        public decimal MilesAt25p { get; set; } = 0m;
        public decimal TotalAmount { get; set; } = 0m;
        public string Status { get; set; } = "Draft";               // Draft | Submitted | Posted | Paid
        public DateTime? SubmittedAt { get; set; }
        public DateTime? PostedAt { get; set; }
        public DateTime? PaidAt { get; set; }
        public int? DlaEntryId { get; set; }                        // FK → DlaEntries when Posted (Dr Mileage / Cr DLA)
        public int? ReimbursementDlaEntryId { get; set; }           // FK → DlaEntries when Paid (Dr DLA / Cr Bank)
        public string? Notes { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
    }
}
