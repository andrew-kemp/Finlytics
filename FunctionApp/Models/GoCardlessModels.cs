using System;

namespace FinanceHubFunctions.Models
{
    /// <summary>
    /// Tracks GoCardless Direct Debit mandates for customers.
    /// A mandate authorises you to collect payments from a customer's bank account.
    /// </summary>
    public class GoCardlessMandate
    {
        public int Id { get; set; }
        public string? CustomerId { get; set; }
        public string? CustomerName { get; set; }
        public string? GoCardlessMandateId { get; set; }  // GC mandate ID e.g. "MD000..."
        public string? GoCardlessCustomerId { get; set; }  // GC customer ID e.g. "CU000..."
        public string? Status { get; set; }  // pending_submission, submitted, active, failed, cancelled, expired
        public string? Scheme { get; set; }  // bacs (UK Direct Debit)
        public string? BankAccountHolder { get; set; }
        public string? BankAccountEndDigits { get; set; }  // Last 4 digits only
        public string? Reference { get; set; }  // Mandate reference shown on bank statement
        public DateTime? CreatedDate { get; set; }
        public DateTime? ActivatedDate { get; set; }
        public DateTime? CancelledDate { get; set; }
    }

    /// <summary>
    /// Tracks individual GoCardless payments (linked to invoices).
    /// </summary>
    public class GoCardlessPayment
    {
        public int Id { get; set; }
        public int? InvoiceId { get; set; }
        public string? InvoiceNumber { get; set; }
        public string? GoCardlessPaymentId { get; set; }  // GC payment ID e.g. "PM000..."
        public string? GoCardlessMandateId { get; set; }  // Which mandate was used
        public decimal Amount { get; set; }  // In GBP
        public string? Currency { get; set; } = "GBP";
        public string? Description { get; set; }
        public string? Status { get; set; }  // pending_submission, submitted, confirmed, paid_out, failed, cancelled
        public DateTime? ChargeDate { get; set; }  // When the payment will be / was collected
        public DateTime? PaidOutDate { get; set; }  // When funds reached your account
        public string? FailureReason { get; set; }
        public DateTime? CreatedDate { get; set; }
    }
}
