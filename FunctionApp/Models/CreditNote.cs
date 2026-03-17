using System;

namespace FinanceHubFunctions.Models
{
    /// <summary>
    /// Represents a credit note issued to a customer — linked to an original invoice (overpayment,
    /// correction, goodwill, etc.) and optionally redeemed against a future invoice.
    /// </summary>
    public class CreditNote
    {
        public int Id { get; set; }

        /// <summary>Human-readable reference, e.g. CN-2026-001</summary>
        public string CreditNoteNumber { get; set; } = string.Empty;

        /// <summary>Customer this credit note belongs to.</summary>
        public string? CustomerId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string CustomerEmail { get; set; } = string.Empty;

        /// <summary>The invoice that triggered the credit (overpayment, duplicate, etc.). Nullable — can be standalone.</summary>
        public int? OriginalInvoiceId { get; set; }
        public string? OriginalInvoiceNumber { get; set; }

        /// <summary>Invoice the credit was applied against when redeemed. Null = still pending.</summary>
        public int? AppliedToInvoiceId { get; set; }
        public string? AppliedToInvoiceNumber { get; set; }

        /// <summary>Reason for issuing the credit note.</summary>
        public string Reason { get; set; } = string.Empty;

        /// <summary>
        /// Reason category: Overpayment | Duplicate | Correction | Goodwill | ServiceIssue | Other
        /// </summary>
        public string ReasonCategory { get; set; } = "Other";

        public decimal AmountNet { get; set; }
        public decimal VATRate { get; set; }
        public decimal VATAmount { get; set; }
        public decimal AmountGross { get; set; }
        public string Currency { get; set; } = "GBP";

        /// <summary>Draft | Issued | Applied | Voided</summary>
        public string Status { get; set; } = "Draft";

        public DateTime DateIssued { get; set; } = DateTime.UtcNow;
        public DateTime? DateApplied { get; set; }
        public DateTime? ExpiryDate { get; set; }

        public string? PdfUrl { get; set; }
        public string? Notes { get; set; }

        public string? FinancialYear { get; set; }
        public int? TaxYear { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
    }
}
