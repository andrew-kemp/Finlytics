using System;
using System.Collections.Generic;

namespace FinanceHubFunctions.Models
{
    public class Quote
    {
        public int Id { get; set; }
        public string QuoteNumber { get; set; }
        public string? CustomerId { get; set; }
        public string CustomerName { get; set; }
        public string ContactEmail { get; set; }
        public string BillingEmail { get; set; }
        public DateTime DateIssued { get; set; }
        public DateTime? ValidUntil { get; set; }
        public string Status { get; set; } // Draft, Sent, Accepted, Declined, Expired, ConvertedToInvoice
        public decimal AmountNet { get; set; }
        public decimal? DiscountPercent { get; set; }
        public decimal? DiscountAmount { get; set; }
        public string DiscountNote { get; set; }
        public decimal VATAmount { get; set; }
        public decimal AmountGross { get; set; }
        public int TaxYear { get; set; }
        public string FinancialYear { get; set; }
        public string LinkedInvoiceId { get; set; }
        public string Notes { get; set; }
        public string? PdfUrl { get; set; }
        public List<QuoteLine> LineItems { get; set; } = new List<QuoteLine>();
    }

    public class QuoteLine
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
