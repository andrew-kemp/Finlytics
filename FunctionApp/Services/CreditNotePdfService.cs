using System;
using System.IO;
using System.Threading.Tasks;
using FinanceHubFunctions.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace FinanceHubFunctions.Services
{
    public class CreditNotePdfService
    {
        private readonly BlobStorageService _blobStorageService;

        public CreditNotePdfService(BlobStorageService blobStorageService)
        {
            _blobStorageService = blobStorageService;
        }

        /// <summary>Generates PDF bytes for the credit note.</summary>
        public async Task<byte[]> GeneratePdfAsync(CreditNote creditNote, CompanySettings company)
        {
            QuestPDF.Settings.License = LicenseType.Community;

            byte[]? logoBytes = null;
            try
            {
                if (!string.IsNullOrWhiteSpace(company.DocumentLogoUrl))
                {
                    var (bytes, _) = await _blobStorageService.GetLogoBytesFromUrlAsync(company.DocumentLogoUrl);
                    logoBytes = bytes;
                }
                if (logoBytes == null)
                {
                    var (bytes, _) = await _blobStorageService.GetLogoAsync(company.Id);
                    logoBytes = bytes;
                }
            }
            catch { /* continue without logo */ }

            var currencySymbol = !string.IsNullOrWhiteSpace(company.CurrencySymbol) ? company.CurrencySymbol : "£";

            var pdfBytes = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(45);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(10).FontFamily("Arial"));

                    page.Header().Element(c => ComposeHeader(c, creditNote, company, logoBytes, currencySymbol));
                    page.Content().Element(c => ComposeContent(c, creditNote, company, currencySymbol));
                    page.Footer().AlignCenter().Text(t =>
                    {
                        t.Span($"{company.CompanyName ?? company.CompanyEmail} · Credit Note {creditNote.CreditNoteNumber}").FontSize(8).FontColor(Colors.Grey.Medium);
                        if (!string.IsNullOrWhiteSpace(company.CompanyRegistrationNumber))
                            t.Span($" · Reg: {company.CompanyRegistrationNumber}").FontSize(8).FontColor(Colors.Grey.Medium);
                    });
                });
            }).GeneratePdf();

            return pdfBytes;
        }

        /// <summary>Generates the PDF and uploads to blob storage. Returns the blob reference.</summary>
        public async Task<string> GenerateAndUploadAsync(CreditNote creditNote, CompanySettings company)
        {
            var pdfBytes = await GeneratePdfAsync(creditNote, company);
            var blobRef = await _blobStorageService.UploadCreditNotePdfAsync(
                creditNote.CreditNoteNumber,
                pdfBytes,
                creditNote.CustomerName,
                creditNote.DateIssued);
            return blobRef;
        }

        private void ComposeHeader(IContainer container, CreditNote cn, CompanySettings company, byte[]? logoBytes, string sym)
        {
            container.Row(row =>
            {
                // Left: company details
                row.RelativeItem().Column(col =>
                {
                    if (logoBytes != null && logoBytes.Length > 0)
                        col.Item().Width(120).Image(logoBytes);
                    col.Item().Text(company.CompanyName ?? "").FontSize(14).Bold();
                    if (!string.IsNullOrWhiteSpace(company.CompanyAddress ?? company.Address))
                        col.Item().Text(company.CompanyAddress ?? company.Address ?? "").FontSize(8).FontColor(Colors.Grey.Darken1);
                    if (!string.IsNullOrWhiteSpace(company.CompanyEmail ?? company.Email))
                        col.Item().Text(company.CompanyEmail ?? company.Email ?? "").FontSize(8).FontColor(Colors.Grey.Darken1);
                    if (!string.IsNullOrWhiteSpace(company.VATNumber ?? company.VatRegistrationNumber))
                        col.Item().Text($"VAT No: {company.VATNumber ?? company.VatRegistrationNumber}").FontSize(8).FontColor(Colors.Grey.Darken1);
                });

                // Right: CREDIT NOTE heading + ref
                row.RelativeItem().AlignRight().Column(col =>
                {
                    col.Item().Text("CREDIT NOTE").FontSize(22).Bold().FontColor(Color.FromHex("#dc2626"));
                    col.Item().Text(cn.CreditNoteNumber).FontSize(14).Bold().FontColor(Color.FromHex("#dc2626"));
                    col.Item().PaddingTop(4).Text($"Date: {cn.DateIssued:dd MMM yyyy}").FontSize(9);
                    if (!string.IsNullOrWhiteSpace(cn.OriginalInvoiceNumber))
                        col.Item().Text($"Ref Invoice: {cn.OriginalInvoiceNumber}").FontSize(9);
                    col.Item().PaddingTop(4).Background(Color.FromHex("#fee2e2")).Padding(6)
                        .Text($"Amount: {sym}{cn.AmountGross:N2}").FontSize(13).Bold().FontColor(Color.FromHex("#dc2626"));
                });
            });
        }

        private void ComposeContent(IContainer container, CreditNote cn, CompanySettings company, string sym)
        {
            container.PaddingTop(20).Column(col =>
            {
                // Bill To
                col.Item().Background(Color.FromHex("#fef2f2")).Border(1).BorderColor(Color.FromHex("#fca5a5"))
                    .Padding(10).Column(inner =>
                {
                    inner.Item().Text("CREDIT ISSUED TO").FontSize(8).Bold().FontColor(Colors.Grey.Darken1);
                    inner.Item().Text(cn.CustomerName).FontSize(12).Bold();
                    if (!string.IsNullOrWhiteSpace(cn.CustomerEmail))
                        inner.Item().Text(cn.CustomerEmail).FontSize(9).FontColor(Colors.Grey.Darken1);
                });

                col.Item().PaddingTop(16);

                // Reason section
                col.Item().Background(Colors.Grey.Lighten3).Padding(10).Column(inner =>
                {
                    inner.Item().Text("REASON FOR CREDIT").FontSize(8).Bold().FontColor(Colors.Grey.Darken1);
                    inner.Item().PaddingTop(4).Text(GetReasonCategoryLabel(cn.ReasonCategory)).FontSize(10).Bold();
                    inner.Item().Text(cn.Reason).FontSize(10);
                });

                col.Item().PaddingTop(16);

                // Amounts table
                col.Item().Table(table =>
                {
                    table.ColumnsDefinition(c =>
                    {
                        c.RelativeColumn(3);
                        c.RelativeColumn(1);
                    });

                    // Header row
                    table.Header(header =>
                    {
                        header.Cell().Background(Color.FromHex("#dc2626")).Padding(6)
                            .Text("Description").FontSize(9).Bold().FontColor(Colors.White);
                        header.Cell().Background(Color.FromHex("#dc2626")).Padding(6).AlignRight()
                            .Text("Amount").FontSize(9).Bold().FontColor(Colors.White);
                    });

                    // Net
                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(6)
                        .Text("Net credit amount").FontSize(10);
                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(6).AlignRight()
                        .Text($"{sym}{cn.AmountNet:N2}").FontSize(10);

                    // VAT
                    if (cn.VATAmount > 0)
                    {
                        table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(6)
                            .Text($"VAT ({cn.VATRate:N0}%)").FontSize(10);
                        table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(6).AlignRight()
                            .Text($"{sym}{cn.VATAmount:N2}").FontSize(10);
                    }

                    // Total
                    table.Cell().Background(Color.FromHex("#fef2f2")).Padding(6)
                        .Text("Total Credit").FontSize(11).Bold().FontColor(Color.FromHex("#dc2626"));
                    table.Cell().Background(Color.FromHex("#fef2f2")).Padding(6).AlignRight()
                        .Text($"{sym}{cn.AmountGross:N2}").FontSize(11).Bold().FontColor(Color.FromHex("#dc2626"));
                });

                col.Item().PaddingTop(16);

                // Status / usage info
                if (cn.Status == "Applied" && !string.IsNullOrWhiteSpace(cn.AppliedToInvoiceNumber))
                {
                    col.Item().Background(Color.FromHex("#dcfce7")).Border(1).BorderColor(Color.FromHex("#86efac"))
                        .Padding(10).Text($"✓ This credit was applied to Invoice {cn.AppliedToInvoiceNumber}" +
                            (cn.DateApplied.HasValue ? $" on {cn.DateApplied:dd MMM yyyy}" : ""))
                        .FontSize(9).FontColor(Color.FromHex("#166534"));
                }
                else if (cn.Status == "Issued")
                {
                    col.Item().Background(Color.FromHex("#fff7ed")).Border(1).BorderColor(Color.FromHex("#fdba74"))
                        .Padding(10).Text($"This credit of {sym}{cn.AmountGross:N2} is available to be applied against your next invoice.")
                        .FontSize(9).FontColor(Color.FromHex("#9a3412"));
                }

                if (!string.IsNullOrWhiteSpace(cn.Notes))
                {
                    col.Item().PaddingTop(12).Text("Notes").FontSize(8).Bold().FontColor(Colors.Grey.Darken1);
                    col.Item().Text(cn.Notes).FontSize(9).FontColor(Colors.Grey.Darken1);
                }

                // Footer note
                col.Item().PaddingTop(24).Text(
                    company.InvoiceFooterText ?? company.FooterText ??
                    $"This credit note is issued by {company.CompanyName}. Please retain for your records.")
                    .FontSize(8).FontColor(Colors.Grey.Medium).Italic();
            });
        }

        private static string GetReasonCategoryLabel(string category) => category switch
        {
            "Overpayment" => "Overpayment",
            "Duplicate"   => "Duplicate invoice / duplicate payment",
            "Correction"  => "Invoice correction",
            "Goodwill"    => "Goodwill credit",
            "ServiceIssue"=> "Service issue / partial refund",
            _             => "Other"
        };
    }
}
