using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FinanceHubFunctions.Data;
using FinanceHubFunctions.Models;
using FinanceHubFunctions.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace FinanceHubFunctions.Functions
{
    /// <summary>
    /// Runs daily at 07:30 UTC (before working hours) and sends email reminders
    /// for upcoming VAT, Payroll, and Corporation Tax deadlines.
    ///
    /// Reminder thresholds:
    ///   VAT Filing/Payment    — 14 days, 7 days, 1 day before, and on the day
    ///   Payroll Pay Day       — 7 days, 3 days before
    ///   PAYE Payment to HMRC  — 7 days, 1 day before, and on the day
    ///   Corporation Tax       — 30 days, 14 days, 7 days before
    /// </summary>
    public class DeadlineReminderFunction
    {
        private readonly ILogger<DeadlineReminderFunction> _logger;
        private readonly ICompanySettingsRepository _companySettingsRepository;
        private readonly IPayrollSettingsRepository _payrollSettingsRepository;
        private readonly IVatReturnRepository _vatReturnRepository;
        private readonly IPayrollRunRepository _payrollRunRepository;
        private readonly EmailService _emailService;

        public DeadlineReminderFunction(
            ILogger<DeadlineReminderFunction> logger,
            ICompanySettingsRepository companySettingsRepository,
            IPayrollSettingsRepository payrollSettingsRepository,
            IVatReturnRepository vatReturnRepository,
            IPayrollRunRepository payrollRunRepository,
            EmailService emailService)
        {
            _logger = logger;
            _companySettingsRepository = companySettingsRepository;
            _payrollSettingsRepository = payrollSettingsRepository;
            _vatReturnRepository = vatReturnRepository;
            _payrollRunRepository = payrollRunRepository;
            _emailService = emailService;
        }

        [Function("DeadlineReminder")]
        public async Task RunAsync([TimerTrigger("0 30 7 * * *")] TimerInfo timer)
        {
            _logger.LogInformation("DeadlineReminder triggered at {Time} UTC", DateTime.UtcNow);

            var settings = await _companySettingsRepository.GetDefaultAsync();
            if (settings == null)
            {
                _logger.LogWarning("No company settings found — skipping deadline reminders");
                return;
            }

            // Determine recipient — prefer the company email, fall back to SMTP from address
            var recipientEmail = settings.CompanyEmail ?? settings.Email ?? settings.SmtpFromAddress;
            if (string.IsNullOrWhiteSpace(recipientEmail))
            {
                _logger.LogWarning("No recipient email configured — skipping deadline reminders");
                return;
            }

            var payrollSettings = await _payrollSettingsRepository.GetAsync();
            var filedReturns = (await _vatReturnRepository.GetAllAsync()).ToList();
            var payrollRuns = (await _payrollRunRepository.GetAllAsync()).ToList();

            var today = DateTime.UtcNow.Date;
            var alerts = new List<DeadlineAlert>();

            // ── VAT deadlines ─────────────────────────────────────────────
            ComputeVatAlerts(settings, filedReturns, today, alerts);

            // ── Payroll deadlines (only PAYE if runs exist) ───────────────
            ComputePayrollAlerts(payrollSettings, payrollRuns.Count > 0, today, alerts);

            // ── Corporation Tax deadline ──────────────────────────────────
            ComputeCtAlert(settings, today, alerts);

            if (alerts.Count == 0)
            {
                _logger.LogInformation("No deadline alerts to send today");
                return;
            }

            _logger.LogInformation("Found {Count} deadline alert(s) to send", alerts.Count);

            var htmlBody = BuildEmailHtml(alerts, settings.CompanyName ?? "Your Company");
            var subject = alerts.Count == 1
                ? $"⏰ Deadline Reminder: {alerts[0].Label}"
                : $"⏰ {alerts.Count} Upcoming Deadline Reminders";

            var (success, error) = await _emailService.SendSystemEmailAsync(
                recipientEmail, subject, htmlBody);

            if (success)
                _logger.LogInformation("Deadline reminder email sent to {Email}", recipientEmail);
            else
                _logger.LogError("Failed to send deadline reminder email: {Error}", error);
        }

        // ──────────────────────────────────────────────────────────────────
        //  VAT
        // ──────────────────────────────────────────────────────────────────
        private void ComputeVatAlerts(
            CompanySettings settings,
            List<VatReturn> filedReturns,
            DateTime today,
            List<DeadlineAlert> alerts)
        {
            var vatStartMonth = settings.VatQuarterStartMonth;
            if (!vatStartMonth.HasValue || string.IsNullOrWhiteSpace(settings.VatRegistrationNumber))
                return;

            // Lower bound: only check quarters starting on/after VAT effective date (or inception)
            var vatLowerBound = settings.VatEffectiveDate
                ?? settings.CompanyInceptionDate
                ?? settings.IncorporationDate;

            // Determine the current quarter start
            int startM = (vatStartMonth.Value - 1 + 12) % 12; // 0-indexed
            int monthsFromLastStart = (today.Month - 1 - startM + 12) % 12;
            int monthsBack = monthsFromLastStart % 3;
            var currentQStart = new DateTime(today.Year, today.Month - monthsBack, 1);
            if (currentQStart.Month != today.Month - monthsBack + 12 * (today.Month - monthsBack < 1 ? 1 : 0))
            {
                // Normalise using AddMonths for safety
                currentQStart = new DateTime(today.Year, today.Month, 1).AddMonths(-monthsBack);
            }

            // Check up to 3 recent quarters for the first unfiled one
            for (int i = 1; i <= 3; i++)
            {
                var qStart = currentQStart.AddMonths(-(i - 1) * 3);
                var qEnd = qStart.AddMonths(3).AddDays(-1); // last day of quarter

                // Skip if quarter hasn't ended yet
                if (qEnd >= today) continue;

                // Skip quarters that started before VAT registration became effective
                if (vatLowerBound.HasValue && qStart < vatLowerBound.Value)
                    continue;

                // Skip if already filed
                bool filed = filedReturns.Any(fr =>
                    Math.Abs((fr.QuarterStartDate - qStart).TotalDays) < 2);
                if (filed) continue;

                // HMRC deadline: 1 month + 7 days after quarter end
                var deadline = new DateTime(qEnd.Year, qEnd.Month, 1).AddMonths(2).AddDays(6);
                // That gives us the 7th of the month after next (1 calendar month + 7 days)
                int daysUntil = (deadline - today).Days;

                string qLabel = $"{qStart:MMM} – {qEnd:MMM yyyy}";

                // Send reminders at: 14 days, 7 days, 1 day before, on the day, and if overdue
                if (daysUntil <= 14)
                {
                    alerts.Add(new DeadlineAlert
                    {
                        Type = "VAT Filing & Payment",
                        Label = $"VAT Return: {qLabel}",
                        Deadline = deadline,
                        DaysUntil = daysUntil,
                        Detail = daysUntil < 0
                            ? $"OVERDUE by {Math.Abs(daysUntil)} day(s)! File and pay immediately."
                            : daysUntil == 0
                                ? "Due TODAY — file and pay your VAT return now."
                                : $"Due in {daysUntil} day(s) on {deadline:dd MMM yyyy}.",
                        Severity = daysUntil < 0 ? "overdue" : daysUntil <= 3 ? "urgent" : "warning"
                    });
                }
                break; // Only alert for the nearest unfiled quarter
            }
        }

        // ──────────────────────────────────────────────────────────────────
        //  Payroll
        // ──────────────────────────────────────────────────────────────────
        private void ComputePayrollAlerts(
            PayrollSettings payrollSettings,
            bool hasPayrollRuns,
            DateTime today,
            List<DeadlineAlert> alerts)
        {
            if (payrollSettings == null) return;
            if (string.IsNullOrWhiteSpace(payrollSettings.EmployerPAYEReference)) return;

            int payDay = payrollSettings.PayDayOfMonth ?? 25;

            // Next pay day — always remind
            int daysInMonth = DateTime.DaysInMonth(today.Year, today.Month);
            int actualPayDay = Math.Min(payDay, daysInMonth);
            var nextPayDate = new DateTime(today.Year, today.Month, actualPayDay);
            if (nextPayDate <= today)
                nextPayDate = nextPayDate.AddMonths(1);

            int daysUntilPay = (nextPayDate - today).Days;

            if (daysUntilPay <= 7)
            {
                alerts.Add(new DeadlineAlert
                {
                    Type = "Payroll",
                    Label = "Pay Day",
                    Deadline = nextPayDate,
                    DaysUntil = daysUntilPay,
                    Detail = daysUntilPay == 0
                        ? "Pay day is TODAY — ensure payroll has been processed."
                        : $"Pay day in {daysUntilPay} day(s) on {nextPayDate:dd MMM yyyy}. Run payroll if not already done.",
                    Severity = daysUntilPay <= 1 ? "urgent" : "warning"
                });
            }

            // PAYE/NI payment deadline — only if there have been actual payroll runs
            if (hasPayrollRuns)
            {
                var payeDeadline = new DateTime(today.Year, today.Month, 22);
                if (payeDeadline <= today)
                    payeDeadline = payeDeadline.AddMonths(1);

                int daysUntilPaye = (payeDeadline - today).Days;

                if (daysUntilPaye <= 7)
                {
                    alerts.Add(new DeadlineAlert
                    {
                        Type = "PAYE/NI Payment",
                        Label = "PAYE/NI Payment to HMRC",
                        Deadline = payeDeadline,
                        DaysUntil = daysUntilPaye,
                        Detail = daysUntilPaye == 0
                            ? "PAYE/NI payment is due TODAY — pay HMRC now."
                            : $"PAYE/NI due in {daysUntilPaye} day(s) on {payeDeadline:dd MMM yyyy}.",
                        Severity = daysUntilPaye <= 1 ? "urgent" : "warning"
                    });
                }
            }
        }

        // ──────────────────────────────────────────────────────────────────
        //  Corporation Tax
        // ──────────────────────────────────────────────────────────────────
        private void ComputeCtAlert(
            CompanySettings settings,
            DateTime today,
            List<DeadlineAlert> alerts)
        {
            var inceptionDate = settings.CompanyInceptionDate ?? settings.IncorporationDate;
            if (!inceptionDate.HasValue) return;

            var inception = inceptionDate.Value;

            // FY end = anniversary of inception minus 1 day
            var fyEnd = new DateTime(inception.Year + 1, inception.Month, inception.Day).AddDays(-1);
            while (fyEnd < today)
                fyEnd = fyEnd.AddYears(1);

            // CT deadline: 9 months + 1 day after FY end
            var ctDeadline = fyEnd.AddMonths(9).AddDays(1);
            int daysUntilCt = (ctDeadline - today).Days;

            // Remind at 30, 14, 7 days
            if (daysUntilCt <= 30)
            {
                alerts.Add(new DeadlineAlert
                {
                    Type = "Corporation Tax",
                    Label = "Corporation Tax Payment",
                    Deadline = ctDeadline,
                    DaysUntil = daysUntilCt,
                    Detail = daysUntilCt < 0
                        ? $"OVERDUE by {Math.Abs(daysUntilCt)} day(s)! Pay HMRC immediately."
                        : daysUntilCt == 0
                            ? "Corporation Tax is due TODAY."
                            : $"FY ends {fyEnd:dd MMM yyyy}. CT due in {daysUntilCt} day(s) on {ctDeadline:dd MMM yyyy}.",
                    Severity = daysUntilCt < 0 ? "overdue" : daysUntilCt <= 7 ? "urgent" : "warning"
                });
            }
        }

        // ──────────────────────────────────────────────────────────────────
        //  Email HTML builder
        // ──────────────────────────────────────────────────────────────────
        private static string BuildEmailHtml(List<DeadlineAlert> alerts, string companyName)
        {
            var rows = string.Join("\n", alerts.OrderBy(a => a.DaysUntil).Select(a =>
            {
                string bgColor = a.Severity == "overdue" ? "#fff5f5"
                    : a.Severity == "urgent" ? "#fff8f0" : "#f8fafc";
                string borderColor = a.Severity == "overdue" ? "#dc3545"
                    : a.Severity == "urgent" ? "#e65100" : "#dee2e6";
                string textColor = a.Severity == "overdue" ? "#dc3545"
                    : a.Severity == "urgent" ? "#e65100" : "#1e293b";
                string badge = a.Severity == "overdue"
                    ? "<span style=\"background:#dc3545;color:#fff;font-size:11px;padding:2px 8px;border-radius:4px;font-weight:700;\">OVERDUE</span>"
                    : a.Severity == "urgent"
                        ? "<span style=\"background:#e65100;color:#fff;font-size:11px;padding:2px 8px;border-radius:4px;font-weight:700;\">URGENT</span>"
                        : "";

                return $@"<tr>
  <td style=""padding:12px 16px;border:1px solid {borderColor};background:{bgColor};"">
    <div style=""font-weight:600;font-size:15px;color:{textColor};margin-bottom:4px;"">{a.Label} {badge}</div>
    <div style=""font-size:13px;color:#64748b;"">{a.Type} &middot; {a.Detail}</div>
  </td>
</tr>";
            }));

            return $@"<!DOCTYPE html>
<html>
<head><meta charset=""utf-8""/></head>
<body style=""font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,sans-serif;background:#f1f5f9;padding:24px;"">
<div style=""max-width:600px;margin:0 auto;background:#fff;border-radius:8px;overflow:hidden;box-shadow:0 2px 8px rgba(0,0,0,0.08);"">
  <div style=""background:#1e3a5f;padding:20px 24px;"">
    <h1 style=""margin:0;color:#fff;font-size:20px;"">&#9200; Deadline Reminders</h1>
    <div style=""color:#94a3b8;font-size:13px;margin-top:4px;"">{companyName} &middot; {DateTime.UtcNow:dd MMM yyyy}</div>
  </div>
  <div style=""padding:20px 24px;"">
    <p style=""margin:0 0 16px;color:#475569;font-size:14px;"">The following deadline(s) need your attention:</p>
    <table style=""width:100%;border-collapse:collapse;"">
      {rows}
    </table>
    <p style=""margin:20px 0 0;font-size:12px;color:#94a3b8;"">This is an automated reminder from FinanceHub.</p>
  </div>
</div>
</body>
</html>";
        }

        private class DeadlineAlert
        {
            public string Type { get; set; }
            public string Label { get; set; }
            public DateTime Deadline { get; set; }
            public int DaysUntil { get; set; }
            public string Detail { get; set; }
            public string Severity { get; set; } // "overdue", "urgent", "warning"
        }
    }
}
