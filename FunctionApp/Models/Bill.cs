using System;
using System.Collections.Generic;

namespace FinanceHubFunctions.Models
{
    public class Bill
    {
        public int Id { get; set; }
        public string BillNumber { get; set; } = string.Empty;
        public string? SupplierId { get; set; }
        public string SupplierName { get; set; } = string.Empty;
        public string? SupplierReference { get; set; } // Supplier's own invoice/ref number
        public DateTime DateReceived { get; set; }
        public DateTime DateIssued { get; set; }
        public DateTime? DueDate { get; set; }
        public DateTime? DatePaid { get; set; }
        public string Status { get; set; } = "Draft"; // Draft, Awaiting Approval, Approved, Paid, Overdue, Cancelled
        public decimal AmountNet { get; set; }
        public decimal VATAmount { get; set; }
        public decimal AmountGross { get; set; }
        public string? VATApplicability { get; set; } // Standard, Reduced, Zero, Exempt
        public string? Currency { get; set; } = "GBP";
        public string? Category { get; set; }
        public string? CtTag { get; set; } // Revenue | Capital | NonCT
        public string? PaymentMethod { get; set; }
        public string? Notes { get; set; }
        public string? TaxYear { get; set; }
        public string? FinancialYear { get; set; }
        // OCR / document
        public string? DocumentUrl { get; set; } // Blob URL to uploaded bill/invoice
        public List<string>? Attachments { get; set; }
        // Line items (JSON-serialised)
        public List<BillLine> LineItems { get; set; } = new List<BillLine>();
        // Payment tracking
        public decimal AmountPaid { get; set; }
        public string? PaymentReference { get; set; }
        // Recurring bill
        public bool IsRecurring { get; set; }
        public string? RecurringFrequency { get; set; } // Monthly | Quarterly | Annual
        public DateTime? RecurringNextDate { get; set; }
        // Approval workflow
        public string? ApprovedBy { get; set; }
        public DateTime? ApprovedAt { get; set; }
        // Linked expense (auto-created when bill is paid)
        public int? LinkedExpenseId { get; set; }
    }

    public class BillLine
    {
        public int LineNumber { get; set; }
        public string Description { get; set; } = string.Empty;
        public decimal Quantity { get; set; } = 1;
        public decimal UnitPrice { get; set; }
        public decimal VATRate { get; set; }
        public decimal VATAmount { get; set; }
        public decimal AmountNet { get; set; }
        public decimal AmountGross { get; set; }
    }
}
