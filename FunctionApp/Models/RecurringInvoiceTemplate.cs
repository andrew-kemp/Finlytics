using System;
using System.Collections.Generic;

namespace FinanceHubFunctions.Models
{
    public class RecurringInvoiceTemplate
    {
        public int Id { get; set; }
        public string? CustomerId { get; set; }
        public string CustomerName { get; set; }
        public string BillingEmail { get; set; }
        public string? POReference { get; set; }
        public string? VatNumber { get; set; }
        public string Frequency { get; set; } = "Monthly"; // Monthly, Quarterly, Annual
        public int DayOfMonth { get; set; } = 1; // Day of month to generate invoice
        public DateTime? NextRunDate { get; set; }
        public bool IsActive { get; set; } = true;
        public string? Notes { get; set; }
        public decimal? DiscountPercent { get; set; }
        public decimal? DiscountAmount { get; set; }
        public string? DiscountNote { get; set; }
        public List<InvoiceLine> DefaultLineItems { get; set; } = new List<InvoiceLine>();
        public DateTime? CreatedDate { get; set; }
        public DateTime? ModifiedDate { get; set; }
    }
}
