using System;

namespace FinanceHubFunctions.Models
{
    public class PayrollRun
    {
        public int Id { get; set; }
        public DateTime PeriodStart { get; set; }
        public DateTime PeriodEnd { get; set; }
        public DateTime PayDate { get; set; }
        public string? Frequency { get; set; }            // Monthly, Weekly
        public string? Status { get; set; }              // Draft, Posted
        public string? TaxYear { get; set; }             // e.g. "2025-26"
        public int? TaxMonth { get; set; }               // 1–12 (HMRC tax month)
        public string? FpsStatus { get; set; }           // Pending, Submitted, Accepted, Rejected
        public DateTime? FpsSubmittedAt { get; set; }
        public string? FpsCorrelationId { get; set; }
        public decimal? TotalGross { get; set; }
        public decimal? TotalTax { get; set; }
        public decimal? TotalEmployeeNi { get; set; }
        public decimal? TotalEmployerNi { get; set; }
        public decimal? TotalNetPay { get; set; }
        public string? Notes { get; set; }
        public DateTime? CreatedDate { get; set; }
        public DateTime? ModifiedDate { get; set; }
    }
}
