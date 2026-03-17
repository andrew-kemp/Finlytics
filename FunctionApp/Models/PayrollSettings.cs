using System;

namespace FinanceHubFunctions.Models
{
    public class PayrollSettings
    {
        public int Id { get; set; }
        public string? EmployerPAYEReference { get; set; }    // e.g. "123/AB12345"
        public string? AccountsOfficeReference { get; set; }  // e.g. "123PA12345678"
        public string? PensionProvider { get; set; }
        public string? DefaultTaxCode { get; set; }           // e.g. "1257L"
        public string? DefaultNiCategory { get; set; }        // e.g. "A"
        public int? PayDayOfMonth { get; set; }               // Day salary is paid each month (default 25)
        public bool EmploymentAllowanceEligible { get; set; } // Sole director companies are NOT eligible
        public bool SmallEmployerRelief { get; set; }         // Recover statutory pay from HMRC

        // ── Email ────────────────────────────────────────────────────────
        public string? PayrollEmail { get; set; }             // From address for payroll emails (payslips, P11D etc.)

        // ── Auto-schedule ────────────────────────────────────────────────
        public bool AutoRunEnabled { get; set; }              // Enable timer-trigger auto-generation
        public int? AutoRunDaysBefore { get; set; }           // Days before pay day to auto-generate (default 7)
        public bool AutoPostImmediately { get; set; }         // true = generate + post automatically; false = draft only
        public DateTime? AutoRunLastTriggered { get; set; }   // UTC timestamp of last auto-run

        public DateTime? CreatedDate { get; set; }
        public DateTime? ModifiedDate { get; set; }
    }
}
