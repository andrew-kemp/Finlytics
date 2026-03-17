using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using FinanceHubFunctions.Models;

namespace FinanceHubFunctions.Services
{
    public class TemplateBasedCertificateService
    {
        private readonly SharePointService _sharePointService;

        public TemplateBasedCertificateService(SharePointService sharePointService)
        {
            _sharePointService = sharePointService;
        }

        public async Task<string> GenerateShareCertificateHtmlAsync(
            Shareholder shareholder, 
            CompanySettings company, 
            ShareClass shareClass)
        {
            // Determine which template to use based on share class Name
            // ShareClass.Name is "A" or "B", DisplayName is "A Ordinary Shares" or "B Ordinary Shares"
            string templateType = shareClass?.Name == "B"
                ? "Share Certificate - B Ordinary Shares"
                : "Share Certificate - A Ordinary Shares";

            // Get the template from Company Documents
            var documents = await _sharePointService.GetCompanyDocumentsAsync();
            var template = documents.FirstOrDefault(d => 
                d.DocumentType == "Template" && 
                d.RelatedEntity == templateType &&
                d.IsActive);

            if (template == null)
            {
                throw new Exception($"Template not found: {templateType}. Please upload the template to Company Documents.");
            }

            // Download the template content
            var url = new Uri(template.Url);
            var serverRelativeUrl = url.AbsolutePath;
            var templateBytes = await _sharePointService.DownloadCompanyDocumentAsync(serverRelativeUrl);
            var templateHtml = Encoding.UTF8.GetString(templateBytes);

            // Calculate share numbers range
            var allShareholders = await _sharePointService.GetShareholdersAsync();
            var shareNumbersRange = CalculateShareNumbersRange(shareholder, allShareholders);

            // Generate certificate number (e.g., SC-2024-001)
            var certificateNumber = shareholder.ShareCertificateNumber ?? 
                $"SC-{DateTime.Now.Year}-{shareholder.Id:D3}";

            // Format issue date
            var issueDate = shareholder.DateOfIssue ?? DateTime.Now;
            var issueDateFormatted = issueDate.ToString("dd MMM yyyy"); // e.g., "07 Feb 2026"

            // Replace placeholders
            var html = templateHtml
                .Replace("{{COMPANY_NAME}}", company.CompanyName ?? "")
                .Replace("{{COMPANY_NUMBER}}", company.CompanyRegistrationNumber ?? "")
                .Replace("{{REGISTERED_OFFICE_ADDRESS}}", company.CompanyAddress ?? "")
                .Replace("{{COMPANY_LOGO_URL}}", company.LogoUrl ?? "")
                .Replace("{{HOLDER_FULL_NAME}}", shareholder.Name ?? "")
                .Replace("{{SHAREHOLDER_NAME}}", shareholder.Name ?? "")
                .Replace("{{CERTIFICATE_NUMBER}}", certificateNumber)
                .Replace("{{YEAR}}", issueDate.Year.ToString())
                .Replace("{{SEQUENCE}}", shareholder.Id.ToString("D3"))
                .Replace("{{SHARE_CLASS}}", shareClass?.DisplayName ?? shareClass?.Name ?? "Ordinary Shares")
                .Replace("{{NUMBER_OF_SHARES}}", shareholder.SharesOwned.ToString("N0"))
                .Replace("{{SHARE_NUMBERS}}", shareNumbersRange)
                .Replace("{{FROM_TO_NUMBERS}}", shareNumbersRange)
                .Replace("{{DATE_OF_ISSUE}}", issueDate.ToString("dd MMMM yyyy"))
                .Replace("{{ISSUE_DATE_DD_MON_YYYY}}", issueDateFormatted)
                .Replace("{{DIRECTOR_NAME}}", company.DirectorName ?? "Director")
                .Replace("{{SIGN_DATE}}", DateTime.Now.ToString("dd MMM yyyy"))
                .Replace("{{DIRECTOR_SIGNATURE}}", company.DirectorSignature ?? "")
                .Replace("{{AUTHORIZED_OFFICER_SIGNATURE}}", company.AuthorizedOfficerSignature ?? company.DirectorSignature ?? "")
                .Replace("{{AUTHORIZED_OFFICER_NAME}}", company.AuthorizedOfficerName ?? company.DirectorName ?? "");

            return html;
        }

        private string CalculateShareNumbersRange(Shareholder shareholder, List<Shareholder> allShareholders)
        {
            // Get all shareholders with the same share class issued before this one
            var previousShareholdersSameClass = allShareholders
                .Where(s => s.ShareClassId == shareholder.ShareClassId && 
                           s.DateOfIssue < shareholder.DateOfIssue)
                .OrderBy(s => s.DateOfIssue)
                .ToList();

            // Calculate starting share number (previous shares + 1)
            int startNumber = previousShareholdersSameClass.Sum(s => s.SharesOwned) + 1;
            int endNumber = startNumber + shareholder.SharesOwned - 1;

            if (shareholder.SharesOwned == 1)
            {
                return startNumber.ToString();
            }

            return $"{startNumber}-{endNumber}";
        }
    }
}
