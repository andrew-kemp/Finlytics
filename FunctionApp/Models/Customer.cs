namespace FinanceHubFunctions.Models
{
    public class Customer
    {
        public string Id { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public string CustomerCode { get; set; } = string.Empty; // From frontend
        public string Name { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty; // Alias for Name
        public string Email { get; set; } = string.Empty;
        public string? BillingEmail { get; set; }
        public string? Phone { get; set; }
        public string? Address { get; set; }
        public string? BillingAddress { get; set; }
        public string? DefaultDayRate { get; set; }
        public string? DefaultHourlyRate { get; set; }
        public bool IsVATRegistered { get; set; }
        public int? DefaultVATRate { get; set; } // Changed to int? to match frontend
        public string? ContactName { get; set; }  // Primary contact name — shown on invoices/quotes
        public string? CcEmail { get; set; }       // CC email — auto-BCC'd on all outbound emails
        // GoCardless Direct Debit mandate
        public string? GoCardlessMandateId { get; set; }
        public string? GoCardlessCustomerId { get; set; }
        public string? GoCardlessMandateStatus { get; set; } // pending_submission, submitted, active, failed, cancelled
    }
}