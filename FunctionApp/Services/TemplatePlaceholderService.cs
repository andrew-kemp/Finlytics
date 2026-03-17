using System;
using System.Collections.Generic;
using FinanceHubFunctions.Models;

namespace FinanceHubFunctions.Services
{
    public static class TemplatePlaceholderService
    {
        public static string ApplyCompanyPlaceholders(string html, CompanySettings company, string? logoDataUrl = null, DateTime? signDate = null)
        {
            if (string.IsNullOrEmpty(html) || company == null)
            {
                return html;
            }

            var signDateFormatted = (signDate ?? DateTime.Now).ToString("dd MMM yyyy");
            var logoValue = !string.IsNullOrWhiteSpace(logoDataUrl) ? logoDataUrl : company.LogoUrl;

            var replacements = new Dictionary<string, string?>
            {
                ["{{COMPANY_NAME}}"] = company.CompanyName,
                ["{{COMPANY_NUMBER}}"] = company.CompanyRegistrationNumber,
                ["{{REGISTERED_OFFICE_ADDRESS}}"] = company.CompanyAddress ?? company.Address,
                ["{{COMPANY_ADDRESS}}"] = company.CompanyAddress ?? company.Address,
                ["{{COMPANY_PHONE}}"] = company.CompanyPhone ?? company.PhoneNumber,
                ["{{COMPANY_EMAIL}}"] = company.CompanyEmail ?? company.Email,
                ["{{COMPANY_LOGO_URL}}"] = logoValue,
                ["{{DIRECTOR_NAME}}"] = company.DirectorName,
                ["{{DIRECTORS}}"] = company.Directors,
                ["{{DIRECTOR_SIGNATURE}}"] = company.DirectorSignature,
                ["{{AUTHORIZED_OFFICER_NAME}}"] = company.AuthorizedOfficerName ?? company.DirectorName,
                ["{{AUTHORIZED_OFFICER_SIGNATURE}}"] = company.AuthorizedOfficerSignature ?? company.DirectorSignature,
                ["{{VAT_NUMBER}}"] = company.VATNumber ?? company.VatRegistrationNumber,
                ["{{BANK_NAME}}"] = company.BankName,
                ["{{BANK_ACCOUNT_NUMBER}}"] = company.BankAccountNumber ?? company.AccountNumber,
                ["{{BANK_SORT_CODE}}"] = company.BankSortCode ?? company.SortCode,
                ["{{BANK_IBAN}}"] = company.BankIBAN,
                ["{{BANK_SWIFT}}"] = company.BankSwiftCode,
                ["{{INVOICE_FOOTER}}"] = company.InvoiceFooterText ?? company.FooterText,
                ["{{FOOTER_TEXT}}"] = company.FooterText ?? company.InvoiceFooterText,
                ["{{SIGN_DATE}}"] = signDateFormatted,
                ["{{YEAR}}"] = (signDate ?? DateTime.Now).Year.ToString()
            };

            var output = html;
            foreach (var replacement in replacements)
            {
                output = output.Replace(replacement.Key, replacement.Value ?? string.Empty, StringComparison.OrdinalIgnoreCase);
            }

            return output;
        }
    }
}
