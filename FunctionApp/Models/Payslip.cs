using System;

namespace FinanceHubFunctions.Models
{
    public class Payslip
    {
        public int Id { get; set; }
        public int PayrollRunId { get; set; }
        public int EmployeeId { get; set; }
        public string? EmployeeNumber { get; set; }          // e.g. "EMP-001"
        public string? EmployeeName { get; set; }            // Denormalised for display
        public string? TaxCode { get; set; }                 // e.g. "1257L"
        public string? NiCategory { get; set; }              // A, B, C…
        public string? NiNumber { get; set; }                // Employee NI number
        public decimal? GrossPay { get; set; }
        public decimal? Tax { get; set; }                    // Employee income tax deducted
        public decimal? NationalInsurance { get; set; }      // Employee NI contribution
        public decimal? EmployerNi { get; set; }             // Employer NI (company cost)
        public decimal? Pension { get; set; }
        public decimal? NetPay { get; set; }
        public string? TaxYear { get; set; }                 // e.g. "2025-26"
        public int? TaxMonth { get; set; }                   // 1–12
        public decimal? YtdGross { get; set; }               // Cumulative gross (incl. this period)
        public decimal? YtdTax { get; set; }                 // Cumulative tax (incl. this period)
        public decimal? YtdEmployeeNi { get; set; }          // Cumulative employee NI
        public decimal? YtdEmployerNi { get; set; }          // Cumulative employer NI
        public string? StarterDeclaration { get; set; }      // A/B/C (new starters only)
        public string? DirectorsNiMethod { get; set; }       // AP = Alternative Per-Period
        public string? PaymentReference { get; set; }
        public string? Notes { get; set; }
        public DateTime? CreatedDate { get; set; }
    }
}
