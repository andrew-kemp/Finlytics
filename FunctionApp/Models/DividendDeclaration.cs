using System;
using System.Collections.Generic;

namespace FinanceHubFunctions.Models
{
    public class DividendDeclaration
    {
        public int Id { get; set; }

        /// <summary>Auto-generated reference, e.g. "DIV-2026-001"</summary>
        public string DividendRef { get; set; } = "";

        /// <summary>"Interim" or "Final"</summary>
        public string DividendType { get; set; } = "Interim";

        /// <summary>Share class name, e.g. "A Ordinary"</summary>
        public string ShareClass { get; set; } = "";

        public DateTime MeetingDate { get; set; }
        public string? MeetingLocation { get; set; }
        public DateTime RecordDate { get; set; }
        public DateTime PaymentDate { get; set; }

        public decimal AmountPerShare { get; set; }
        public decimal TotalAmount { get; set; }

        /// <summary>"Draft" or "Finalised"</summary>
        public string Status { get; set; } = "Draft";

        public string? DirectorName { get; set; }
        public string? Notes { get; set; }

        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
        public DateTime? FinalisedDate { get; set; }

        // Navigation property — not mapped as FK in DB (loaded explicitly)
        public List<DividendAllocation> Allocations { get; set; } = new();
    }

    public class DividendAllocation
    {
        public int Id { get; set; }
        public int DividendDeclarationId { get; set; }

        /// <summary>FK to Shareholders table — null if manually entered</summary>
        public int? ShareholderId { get; set; }

        public string ShareholderName { get; set; } = "";
        public string ShareClass { get; set; } = "";
        public int NumberOfShares { get; set; }
        public decimal AmountPerShare { get; set; }
        public decimal TotalAmount { get; set; }

        // Banking details for BAC CSV
        public string? BankAccountName { get; set; }
        public string? SortCode { get; set; }
        public string? AccountNumber { get; set; }

        /// <summary>Voucher reference, e.g. "DV-2026-001"</summary>
        public string? VoucherRef { get; set; }

        /// <summary>FK to CompanyLedger — set after finalise</summary>
        public int? LedgerEntryId { get; set; }
    }
}
