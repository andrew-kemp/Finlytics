using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using FinanceHubFunctions.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace FinanceHubFunctions.Services
{
    public class ShareCertificatePdfService
    {
        private readonly HttpClient _httpClient;
        private readonly SharePointService _sharePointService;

        public ShareCertificatePdfService(SharePointService sharePointService)
        {
            _httpClient = new HttpClient();
            _sharePointService = sharePointService;
        }

        public async Task<byte[]> GenerateShareCertificatePdfAsync(Shareholder shareholder, CompanySettings company, ShareClass shareClass)
        {
            QuestPDF.Settings.License = LicenseType.Community;

            byte[] logoBytes = null;
            // Prefer DocumentLogoUrl, then LogoUrl
            var logoUrlToFetch = !string.IsNullOrWhiteSpace(company.DocumentLogoUrl)
                ? company.DocumentLogoUrl
                : company.LogoUrl;
            if (!string.IsNullOrEmpty(logoUrlToFetch))
            {
                try
                {
                    logoBytes = await _httpClient.GetByteArrayAsync(logoUrlToFetch);
                }
                catch
                {
                    // Logo failed to load - continue without it
                }
            }

            // Calculate share numbers range
            var allShareholders = await _sharePointService.GetShareholdersAsync();
            var shareNumbersRange = await CalculateShareNumbersRange(shareholder, allShareholders);

            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(50);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(11).FontFamily("Segoe UI"));

                    page.Content().Element(c => ComposeContent(c, shareholder, company, shareClass, logoBytes, shareNumbersRange));
                });
            });

            return document.GeneratePdf();
        }

        private Task<string> CalculateShareNumbersRange(Shareholder shareholder, List<Shareholder> allShareholders)
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
                return Task.FromResult(startNumber.ToString());
            }

            return Task.FromResult($"{startNumber}-{endNumber}");
        }

        private void ComposeContent(IContainer container, Shareholder shareholder, CompanySettings company, ShareClass shareClass, byte[] logoBytes, string shareNumbersRange)
        {
            container.Padding(15).Border(2).BorderColor("#0f6cbd").Column(column =>
            {
                column.Spacing(5);

                // Header - simplified without logo to avoid size issues
                column.Item().Column(col =>
                {
                    col.Item().Text(company.CompanyName ?? "").FontSize(12).Bold();
                    if (!string.IsNullOrEmpty(company.CompanyRegistrationNumber))
                        col.Item().Text($"Company Number: {company.CompanyRegistrationNumber}").FontSize(9);
                });

                column.Item().PaddingTop(8).BorderBottom(2).BorderColor("#0f6cbd");

                // Title
                column.Item().PaddingTop(10).AlignCenter().Text("Share Certificate").FontSize(18).Bold().FontColor("#0f6cbd");
                column.Item().PaddingTop(3).AlignCenter().Text($"Certificate Number: {shareholder.ShareCertificateNumber ?? "N/A"}").FontSize(9).FontColor("#666");

                column.Item().PaddingTop(12);

                // Statement - simplified
                column.Item().Text(text =>
                {
                    text.DefaultTextStyle(x => x.FontSize(11));
                    text.Span("This is to certify that ");
                    text.Span(shareholder.Name ?? "").Bold();
                    text.Span(" is the registered holder of the shares described below.");
                });

                // Share details - simpler layout
                column.Item().PaddingTop(12).Background("#fafcfe").Border(1).BorderColor("#cfd6df").Padding(10).Column(box =>
                {
                    box.Item().Text(t =>
                    {
                        t.Span("Share Class: ").FontSize(9).FontColor("#666");
                        t.Span(shareClass?.Name ?? "N/A").FontSize(11).Bold();
                    });
                    
                    box.Item().Text(t =>
                    {
                        t.Span("Shares Owned: ").FontSize(9).FontColor("#666");
                        t.Span(shareholder.SharesOwned.ToString()).FontSize(11).Bold();
                    });
                    
                    box.Item().Text(t =>
                    {
                        t.Span("Issue Date: ").FontSize(9).FontColor("#666");
                        t.Span(shareholder.DateOfIssue?.ToString("dd MMM yyyy") ?? "N/A").FontSize(11).Bold();
                    });
                    
                    box.Item().Text(t =>
                    {
                        t.Span("Share Numbers: ").FontSize(9).FontColor("#666");
                        t.Span(shareNumbersRange).FontSize(11).Bold();
                    });
                });

                // Rights statement
                column.Item().PaddingTop(10).Text("The shares are fully paid and rank in accordance with the rights set out in the Company's Articles of Association.")
                    .FontSize(10);

                // Signatures - simplified
                column.Item().PaddingTop(30).Row(row =>
                {
                    row.RelativeItem().Column(col =>
                    {
                        col.Item().PaddingTop(25).BorderTop(1).PaddingTop(5);
                        col.Item().Text("Director").FontSize(9);
                        col.Item().Text($"Date: {DateTime.Now:dd/MM/yyyy}").FontSize(8);
                    });

                    row.ConstantItem(20);

                    row.RelativeItem().Column(col =>
                    {
                        col.Item().PaddingTop(25).BorderTop(1).PaddingTop(5);
                        col.Item().Text("Authorised Officer").FontSize(9);
                        col.Item().Text($"Date: {DateTime.Now:dd/MM/yyyy}").FontSize(8);
                    });
                });

                // Footer
                column.Item().PaddingTop(15).BorderTop(1).BorderColor("#cfd6df").PaddingTop(5)
                    .AlignCenter().Text("Keep this certificate with the Company's statutory records.")
                    .FontSize(8).FontColor("#666");
            });
        }
    }
}
