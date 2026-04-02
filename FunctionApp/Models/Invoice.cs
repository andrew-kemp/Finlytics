using System;
using System.Collections.Generic;

namespace FinanceHubFunctions.Models
{
    public class Invoice
    {
        public int Id { get; set; }
        public string InvoiceNumber { get; set; }
        public string? CustomerId { get; set; }
        public string CustomerName { get; set; }
        public string BillingEmail { get; set; }
        public string POReference { get; set; }
        public DateTime DateIssued { get; set; }
        public DateTime? DueDate { get; set; }
        public DateTime? DatePaid { get; set; }
        public string Status { get; set; } // Draft, Issued, Paid, Overdue
        public decimal AmountNet { get; set; }
        public decimal? DiscountPercent { get; set; }
        public decimal? DiscountAmount { get; set; }
        public string DiscountNote { get; set; }
        public decimal VATAmount { get; set; }
        public decimal AmountGross { get; set; }
        public int? TaxYear { get; set; }
        public string FinancialYear { get; set; }
        public string? VatNumber { get; set; }
        public string? PdfUrl { get; set; }
        public List<InvoiceLine> LineItems { get; set; } = new List<InvoiceLine>();
        // Invoice reminder / chaser tracking
        public DateTime? ReminderSentAt { get; set; }
        public int ReminderCount { get; set; } = 0;
        // Credit note applied at invoice creation
        public int? CreditNoteId { get; set; }
        public string? CreditNoteNumber { get; set; }
        public decimal? CreditNoteDeduction { get; set; }
        // GoCardless payment tracking
        public string? GoCardlessPaymentId { get; set; }
        public string? GoCardlessMandateId { get; set; }
        public string? PaymentLink { get; set; }
    }

    public class InvoiceLine
    {
        public int LineNumber { get; set; }
        public string Description { get; set; }
        public string RateType { get; set; } // "Day Rate" or "Hourly Rate"
        public decimal Quantity { get; set; }
        public decimal Rate { get; set; }
        public decimal VATRate { get; set; }
        public decimal LineTotal { get; set; }
    }
}
