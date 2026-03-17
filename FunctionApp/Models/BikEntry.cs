using System;

namespace FinanceHubFunctions.Models
{
    /// <summary>
    /// Represents a Benefit in Kind (BIK) entry for P11D reporting.
    /// P11D is filed annually by 6 July for the preceding tax year.
    /// Class 1A NI (13.8% of taxable cash equivalent) is due 22 July.
    /// </summary>
    public class BikEntry
    {
        public int Id { get; set; }

        /// <summary>Tax year, e.g. "2025/26"</summary>
        public string TaxYear { get; set; } = string.Empty;

        /// <summary>Name of the recipient (director or employee)</summary>
        public string RecipientName { get; set; } = string.Empty;

        /// <summary>"Director" or "Employee"</summary>
        public string RecipientType { get; set; } = "Director";

        /// <summary>
        /// Category of benefit: "PMI", "Annual Party", "Gym Membership",
        /// "Professional Subscription", "Company Car", "Assets Transferred", "Other"
        /// </summary>
        public string BenefitCategory { get; set; } = string.Empty;

        /// <summary>Free-text description, e.g. "BUPA private medical insurance 2025/26"</summary>
        public string? Description { get; set; }

        /// <summary>
        /// Cash equivalent (P11D value) — for PMI/Gym this is the employer's cost.
        /// For Annual Party, this is the per-head cost (TotalEventCost / Headcount).
        /// </summary>
        public decimal CashEquivalent { get; set; } = 0m;

        /// <summary>Start date the benefit was provided</summary>
        public DateTime? DateFrom { get; set; }

        /// <summary>End date the benefit was provided (for recurring benefits like PMI)</summary>
        public DateTime? DateTo { get; set; }

        /// <summary>True if covered by an HMRC exemption (e.g. s.264 annual party, trivial benefit)</summary>
        public bool IsExempt { get; set; } = false;

        /// <summary>Reason for exemption, e.g. "Annual party exemption s.264 — within £150/head"</summary>
        public string? ExemptionReason { get; set; }

        /// <summary>
        /// P11D section letter: "C" (assets), "F" (cars), "H" (loans),
        /// "M" (medical), "N" (other). Auto-populated based on BenefitCategory.
        /// </summary>
        public string? P11DSection { get; set; }

        /// <summary>For Annual Party: total number of attendees (including non-employees)</summary>
        public int? Headcount { get; set; }

        /// <summary>For Annual Party: total gross cost of the event (CashEquivalent = this / Headcount)</summary>
        public decimal? TotalEventCost { get; set; }

        public string? Notes { get; set; }

        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
        public DateTime? ModifiedDate { get; set; }
    }
}
