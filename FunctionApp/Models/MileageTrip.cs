using System;

namespace FinanceHubFunctions.Models
{
    public class MileageTrip
    {
        public int Id { get; set; }
        public string TripId { get; set; } = string.Empty;          // MIL-2025-0001
        public string Director { get; set; } = string.Empty;
        public DateTime TripDate { get; set; }
        public string StartLocation { get; set; } = string.Empty;
        public string EndLocation { get; set; } = string.Empty;
        public decimal Miles { get; set; }                          // One-way or total (see IsReturn)
        public bool IsReturn { get; set; } = false;                 // If true miles is already doubled
        public string Purpose { get; set; } = string.Empty;
        public string Category { get; set; } = "Consulting";        // Consulting | Photography | Conference | Other
        public string TaxYear { get; set; } = string.Empty;         // 2025/26
        public string Status { get; set; } = "Draft";               // Draft | Claimed | Paid
        public int? ClaimId { get; set; }
        // Calculated at claim-time
        public decimal MilesAt45p { get; set; } = 0m;
        public decimal MilesAt25p { get; set; } = 0m;
        public decimal AmountAt45p { get; set; } = 0m;
        public decimal AmountAt25p { get; set; } = 0m;
        public decimal TotalAmount { get; set; } = 0m;
        public string? MapLink { get; set; }
        public string? Notes { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        // Employee portal — approval workflow
        public int? SubmittedByTeamMemberId { get; set; }
        public string? ApprovalStatus { get; set; } = "NotRequired"; // NotRequired | Draft | Submitted | Approved | Rejected
        public int? ApprovedByTeamMemberId { get; set; }
        public DateTime? ApprovedAt { get; set; }
        public string? RejectionReason { get; set; }
        public DateTime? SubmittedAt { get; set; }
    }
}
