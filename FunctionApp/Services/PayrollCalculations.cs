using System;
using System.Linq;

namespace FinanceHubFunctions.Services
{
    /// <summary>
    /// Shared static helpers for payroll calculations — used by both the HTTP
    /// PayrollFunctions and the PayrollSchedulerFunction timer trigger.
    /// All rates are for UK tax year 2025-26.
    /// </summary>
    internal static class PayrollCalculations
    {
        // ── Tax year / month helpers ──────────────────────────────────────────

        /// <summary>Returns HMRC tax month 1–12 for a given pay date (tax year starts 6 April).</summary>
        public static int GetTaxMonth(DateTime payDate)
        {
            int month = payDate.Month;
            if (payDate.Day < 6) month--;
            if (month <= 0) month += 12;
            return ((month - 4 + 12) % 12) + 1;
        }

        /// <summary>Returns the tax year string e.g. "2025-26" for a given pay date.</summary>
        public static string GetTaxYear(DateTime payDate)
        {
            int start = (payDate.Month > 4 || (payDate.Month == 4 && payDate.Day >= 6))
                ? payDate.Year : payDate.Year - 1;
            return $"{start}-{(start + 1).ToString().Substring(2)}";
        }

        // ── Tax code parsing ─────────────────────────────────────────────────

        /// <summary>Parses annual free-pay allowance from a tax code.
        /// Handles English, Scottish (S prefix), Welsh (C prefix) and K codes.
        /// Returns decimal.MaxValue for NT (no tax). Returns 0 for flat-rate codes.</summary>
        public static decimal ParseTaxCodeAllowance(string? taxCode)
        {
            if (string.IsNullOrEmpty(taxCode)) return 12570m;
            var code = taxCode.Trim().ToUpperInvariant();

            // Fixed-rate / special codes — no personal allowance arithmetic
            if (code is "BR" or "SBR" or "CBR" or "D0" or "D1" or "D2" or "SD0" or "SD1" or "SD2" or "SD3" or "0T") return 0m;
            if (code == "NT") return decimal.MaxValue; // No tax at all

            // Strip regional prefix (S = Scottish, C = Welsh)
            if (code.StartsWith("S") || code.StartsWith("C")) code = code.Substring(1);

            // K code = negative allowance (reduces free pay, adds to taxable income)
            bool isKCode = code.StartsWith("K");
            if (isKCode) code = code.Substring(1);

            // Extract leading digits before any letter suffix (L, T, M, N, W1, M1, X)
            var digits = new string(code.TakeWhile(char.IsDigit).ToArray());
            if (!int.TryParse(digits, out int codeNum)) return 12570m;

            decimal allowance = codeNum * 10m;
            return isKCode ? -allowance : allowance;
        }

        /// <summary>Returns true if the tax code uses Scottish income tax rates (S prefix).</summary>
        public static bool IsScottishTaxCode(string? taxCode)
        {
            if (string.IsNullOrEmpty(taxCode)) return false;
            return taxCode.Trim().ToUpperInvariant().StartsWith("S") && taxCode.Trim().Length > 1;
        }

        // ── National Insurance ────────────────────────────────────────────────

        /// <summary>Calculates employee NI using 2025-26 Category A rates (per-period method).
        /// PT = £1,047.50/mo, rate 8% up to UEL £4,189.17/mo, then 2%.</summary>
        public static decimal CalcEmployeeNI(decimal grossPay, string? niCategory = "A")
        {
            if (niCategory == "C") return 0m; // Over state pension age
            const decimal PT  = 1047.50m;
            const decimal UEL = 4189.17m;
            if (grossPay <= PT) return 0m;
            decimal ni = grossPay <= UEL
                ? (grossPay - PT) * 0.08m
                : (UEL - PT) * 0.08m + (grossPay - UEL) * 0.02m;
            return Math.Round(ni, 2);
        }

        /// <summary>Calculates employer NI using 2025-26 rates.
        /// Secondary threshold = £416.67/mo, rate 15%.</summary>
        public static decimal CalcEmployerNI(decimal grossPay, string? niCategory = "A")
        {
            if (niCategory == "C") return 0m;
            const decimal ST = 416.67m;
            if (grossPay <= ST) return 0m;
            return Math.Round((grossPay - ST) * 0.15m, 2);
        }

        // ── Income tax (cumulative method) ────────────────────────────────────

        /// <summary>Calculates income tax due this period using the cumulative method.
        /// Supports English/Welsh codes and Scottish (S prefix). Also handles flat-rate
        /// emergency codes: BR, D0, D1, SBR, SD0-SD3, NT, 0T.</summary>
        public static decimal CalcTax(
            decimal ytdGrossBefore, decimal ytdTaxBefore,
            decimal grossThisPeriod, string? taxCode, int taxMonth)
        {
            var upper = (taxCode ?? "1257L").Trim().ToUpperInvariant();

            // Flat-rate emergency codes (non-cumulative)
            if (upper is "BR" or "CBR" or "SBR") return Math.Round(grossThisPeriod * 0.20m, 2);
            if (upper == "D0")  return Math.Round(grossThisPeriod * 0.40m, 2);
            if (upper == "D1")  return Math.Round(grossThisPeriod * 0.45m, 2);
            if (upper == "SD0") return Math.Round(grossThisPeriod * 0.21m, 2);
            if (upper == "SD1") return Math.Round(grossThisPeriod * 0.42m, 2);
            if (upper == "SD2") return Math.Round(grossThisPeriod * 0.45m, 2);
            if (upper == "SD3") return Math.Round(grossThisPeriod * 0.48m, 2);
            if (upper == "NT")  return 0m;

            bool    isScottish = IsScottishTaxCode(taxCode);
            decimal allowance  = ParseTaxCodeAllowance(taxCode);
            if (allowance == decimal.MaxValue) return 0m; // NT

            decimal cumulativeFreePay = (allowance / 12m) * taxMonth;
            decimal newYtdGross       = ytdGrossBefore + grossThisPeriod;
            decimal taxable           = Math.Max(0m, newYtdGross - cumulativeFreePay);

            decimal newTotalTax = isScottish
                ? CalcTaxOnTaxableScottish(taxable)
                : CalcTaxOnTaxableEngland(taxable);

            return Math.Round(Math.Max(0m, Math.Round(newTotalTax, 2) - ytdTaxBefore), 2);
        }

        /// <summary>England &amp; Wales 2025-26: tax on cumulative taxable income.</summary>
        public static decimal CalcTaxOnTaxableEngland(decimal taxable)
        {
            if (taxable <= 0)       return 0m;
            if (taxable <= 37700m)  return taxable * 0.20m;
            if (taxable <= 125140m) return 37700m * 0.20m + (taxable - 37700m) * 0.40m;
            return                         37700m * 0.20m + 87440m * 0.40m + (taxable - 125140m) * 0.45m;
        }

        /// <summary>Scotland 2025-26: tax on cumulative taxable income.
        /// Starter 19% / Basic 20% / Intermediate 21% / Higher 42% / Advanced 45% / Top 48%</summary>
        public static decimal CalcTaxOnTaxableScottish(decimal taxable)
        {
            if (taxable <= 0)       return 0m;
            if (taxable <= 2827m)   return taxable * 0.19m;
            if (taxable <= 12726m)  return  2827m * 0.19m + (taxable -  2827m) * 0.20m;
            if (taxable <= 31092m)  return  2827m * 0.19m +  9899m * 0.20m + (taxable - 12726m) * 0.21m;
            if (taxable <= 62430m)  return  2827m * 0.19m +  9899m * 0.20m + 18366m * 0.21m + (taxable - 31092m) * 0.42m;
            if (taxable <= 112570m) return  2827m * 0.19m +  9899m * 0.20m + 18366m * 0.21m + 31338m * 0.42m + (taxable - 62430m) * 0.45m;
            return                           2827m * 0.19m +  9899m * 0.20m + 18366m * 0.21m + 31338m * 0.42m + 50140m * 0.45m + (taxable - 112570m) * 0.48m;
        }
    }
}
