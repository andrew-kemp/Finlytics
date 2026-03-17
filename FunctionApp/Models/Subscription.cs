using System;

namespace FinanceHubFunctions.Models
{
    public class Subscription
    {
        public int Id { get; set; }
        public string? SubscriptionId { get; set; }  // Auto-generated unique ID like SUB-001
        public string? Name { get; set; }
        public string? Type { get; set; }  // Software License, SaaS, Cloud Platform, Domain/Hosting, Support
        public string? Vendor { get; set; }
        public string? AccountId { get; set; }  // Vendor account/subscription ID
        public string? LicenseKey { get; set; }
        public string? BillingCycle { get; set; }  // Monthly, Annual, Usage-based, One-time
        public decimal? CostPerCycle { get; set; }
        public decimal? MonthlyBudget { get; set; }  // For usage-based services
        public DateTime? StartDate { get; set; }
        public DateTime? RenewalDate { get; set; }
        public DateTime? ExpiryDate { get; set; }
        public bool AutoRenew { get; set; }
        public int? ReminderDaysBefore { get; set; }  // Days before renewal to remind
        public int? Seats { get; set; }  // Number of licenses/seats
        public int? SeatsUsed { get; set; }
        public string? AssignedToEmployeeIds { get; set; }  // Comma-separated employee IDs
        public string? AdminContact { get; set; }  // Who manages this subscription
        public string? AdminEmail { get; set; }
        public string? LoginUrl { get; set; }
        public string? Category { get; set; }  // Productivity, Security, Development, Communication, etc.
        public string? Status { get; set; }  // Active, Expired, Cancelled, Pending, Trial
        public string? Notes { get; set; }
        public DateTime? CreatedDate { get; set; }
        public DateTime? ModifiedDate { get; set; }
    }
}
