using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using FinanceHubFunctions.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace FinanceHubFunctions.Services
{
    public class MissingReceiptDeclarationPdfService
    {
        private readonly BlobStorageService _blobStorageService;

        public MissingReceiptDeclarationPdfService(BlobStorageService blobStorageService)
        {
            _blobStorageService = blobStorageService;
        }

        /// <summary>
        /// Generates the PDF bytes and computes the SHA-256 hash.
        /// Returns (pdfBytes, hashHex).
        /// </summary>
        public async Task<(byte[] PdfBytes, string HashSha256)> GeneratePdfAsync(
            MissingReceiptDeclaration declaration,
            CompanySettings company)
        {
            QuestPDF.Settings.License = LicenseType.Community;

            byte[] logoBytes = null;
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

            var pdfBytes = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(45);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(10).FontFamily("Arial"));

                    page.Header().Element(c => ComposeHeader(c, declaration, company, logoBytes));
                    page.Content().Element(c => ComposeContent(c, declaration, company));
                    page.Footer().Element(c => ComposeFooter(c, declaration));
                });
            }).GeneratePdf();

            var hash = Convert.ToHexString(SHA256.HashData(pdfBytes)).ToLowerInvariant();
            return (pdfBytes, hash);
        }

        /// <summary>
        /// Generates, uploads to blob, and returns the blob reference and hash.
        /// </summary>
        public async Task<(string BlobRef, string HashSha256)> GenerateAndUploadAsync(
            MissingReceiptDeclaration declaration,
            CompanySettings company)
        {
            var (pdfBytes, hash) = await GeneratePdfAsync(declaration, company);
            var linkedId = declaration.ExpenseId ?? declaration.DlaEntryId ?? 0;
            var blobRef = await _blobStorageService.UploadMissingReceiptDeclarationPdfAsync(
                linkedId,
                declaration.DeclarationId ?? $"MRD-{declaration.Id}",
                pdfBytes);
            return (blobRef, hash);
        }

        /// <summary>Retrieves a previously stored PDF from blob storage.</summary>
        public async Task<byte[]?> GetStoredPdfAsync(string blobRef)
        {
            try
            {
                return await _blobStorageService.DownloadMissingReceiptDeclarationPdfAsync(
                    Path.GetFileNameWithoutExtension(blobRef));
            }
            catch { return null; }
        }

        // ── Header ─────────────────────────────────────────────────────────

        private void ComposeHeader(IContainer container, MissingReceiptDeclaration declaration, CompanySettings company, byte[] logoBytes)
        {
            container.Column(col =>
            {
                col.Item().Row(row =>
                {
                    // Company info
                    row.RelativeItem().Column(column =>
                    {
                        if (logoBytes != null)
                        {
                            column.Item().Height(50).Image(logoBytes);
                            column.Item().PaddingTop(6);
                        }
                        column.Item().Text(company.CompanyName ?? "").FontSize(13).Bold();
                        if (!string.IsNullOrEmpty(company.CompanyRegistrationNumber))
                            column.Item().Text($"Company No: {company.CompanyRegistrationNumber}").FontSize(8);
                        if (!string.IsNullOrEmpty(company.VATNumber))
                            column.Item().Text($"VAT: {company.VATNumber}").FontSize(8);
                    });

                    // Document title block
                    row.RelativeItem().AlignRight().Column(column =>
                    {
                        var title = declaration.DeclarationType == DeclarationType.DirectorExpenseDeclaration
                            ? "DIRECTOR EXPENSE DECLARATION\n(No Receipt)"
                            : "MISSING RECEIPT DECLARATION";

                        column.Item().Text(title)
                            .FontSize(14).Bold()
                            .FontColor(Color.FromHex("#1565C0"))
                            .AlignRight();

                        column.Item().PaddingTop(4);
                        column.Item().Text($"Ref: {declaration.DeclarationId}").FontSize(9).Bold().AlignRight();
                        column.Item().Text($"Created: {declaration.CreatedAt:dd/MM/yyyy HH:mm} UTC").FontSize(8).AlignRight();
                        column.Item().Text($"Status: {declaration.Status}").FontSize(8).AlignRight();
                    });
                });

                col.Item().PaddingTop(8).LineHorizontal(1).LineColor(Color.FromHex("#1565C0"));
                col.Item().PaddingTop(4);
            });
        }

        // ── Content ────────────────────────────────────────────────────────

        private void ComposeContent(IContainer container, MissingReceiptDeclaration declaration, CompanySettings company)
        {
            container.Column(col =>
            {
                col.Spacing(12);

                // Section 1 — Expense Details
                col.Item().Column(section =>
                {
                    section.Item().Text("1. Expense Details")
                        .FontSize(11).Bold().FontColor(Color.FromHex("#1565C0"));
                    section.Item().PaddingTop(4).Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(2);
                            c.RelativeColumn(3);
                        });

                        AddTableRow(table, "Date of Expense", declaration.ExpenseDate?.ToString("dd/MM/yyyy") ?? "—");
                        AddTableRow(table, "Merchant / Payee", declaration.MerchantOrPayee ?? "—");
                        AddTableRow(table, "Category", declaration.ExpenseCategory ?? "—");
                        AddTableRow(table, "Bank Transaction Ref", declaration.BankTransactionRef ?? "—");
                        AddTableRow(table, "Amount (Gross)", $"£{declaration.AmountGross:F2} {declaration.Currency}");
                        AddTableRow(table, "Description", declaration.Description ?? "—");
                    });
                });

                // Section 2 — Reason Receipt Unavailable
                col.Item().Column(section =>
                {
                    section.Item().Text("2. Reason Receipt Unavailable")
                        .FontSize(11).Bold().FontColor(Color.FromHex("#1565C0"));
                    section.Item().PaddingTop(4).Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(2);
                            c.RelativeColumn(3);
                        });

                        var reason = declaration.ReasonReceiptMissing switch
                        {
                            ReceiptMissingReason.Lost => "Lost or misplaced",
                            ReceiptMissingReason.DigitalUnavailable => "Digital receipt not available",
                            ReceiptMissingReason.NotProvided => "Not provided by supplier",
                            ReceiptMissingReason.Other => $"Other: {declaration.OtherReasonText}",
                            _ => declaration.ReasonReceiptMissing.ToString()
                        };

                        AddTableRow(table, "Reason", reason);
                        AddTableRow(table, "VAT Reclaimable", "❌ No — VAT cannot be reclaimed without a valid VAT invoice");
                    });
                });

                // Section 3 — Formal Declaration
                col.Item().Column(section =>
                {
                    section.Item().Text("3. Declaration")
                        .FontSize(11).Bold().FontColor(Color.FromHex("#1565C0"));

                    section.Item().PaddingTop(6).Background(Color.FromHex("#f0f4ff"))
                        .Padding(12).Column(inner =>
                    {
                        inner.Item().Text(
                            "I confirm that this expense was incurred wholly and exclusively for the purposes of the business. " +
                            $"No receipt is available for this transaction. The expense relates to: {declaration.Description ?? "(see above)"}. " +
                            "I understand that VAT cannot be reclaimed without a valid VAT invoice, and that HMRC may disallow this " +
                            "expense if challenged."
                        ).FontSize(9).Italic();
                    });
                });

                // Section 4 — Declarer
                col.Item().Column(section =>
                {
                    section.Item().Text("4. Declarer")
                        .FontSize(11).Bold().FontColor(Color.FromHex("#1565C0"));
                    section.Item().PaddingTop(4).Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(2);
                            c.RelativeColumn(3);
                        });

                        AddTableRow(table, "Name", declaration.DeclarerName ?? "—");
                        AddTableRow(table, "Role", declaration.DeclarerRole ?? "—");
                        AddTableRow(table, "Email", declaration.DeclarerEmail ?? "—");

                        // Signature row — use director image if available, else typed name
                        table.Cell().Background(Color.FromHex("#f8f9fa")).Padding(5)
                            .Text("Signature").FontSize(9).SemiBold();
                        if (!string.IsNullOrEmpty(company.DirectorSignature))
                        {
                            try
                            {
                                var b64 = company.DirectorSignature;
                                if (b64.Contains(',')) b64 = b64[(b64.IndexOf(',') + 1)..];
                                var sigBytes = Convert.FromBase64String(b64);
                                table.Cell().Padding(5).MaxHeight(45).Image(sigBytes).FitHeight();
                            }
                            catch
                            {
                                var fallback = declaration.SignatureType == DeclarationSignatureType.TypedName
                                    ? $"✍ {declaration.TypedSignature} (typed)" : "(none)";
                                table.Cell().Padding(5).Text(fallback).FontSize(9);
                            }
                        }
                        else
                        {
                            var sigText = declaration.SignatureType == DeclarationSignatureType.TypedName
                                ? $"✍ {declaration.TypedSignature} (typed)" : "(none)";
                            table.Cell().Padding(5).Text(sigText).FontSize(9);
                        }

                        AddTableRow(table, "Date Signed", declaration.FinalisedAt?.ToString("dd/MM/yyyy HH:mm") + " UTC" ?? "Draft — not yet finalised");
                    });
                });

                // Section 5 — Acknowledgement
                col.Item().Column(section =>
                {
                    section.Item().Text("5. Acknowledgement")
                        .FontSize(11).Bold().FontColor(Color.FromHex("#1565C0"));
                    section.Item().PaddingTop(4).Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(2);
                            c.RelativeColumn(3);
                        });

                        AddTableRow(table, "CT Disallowable Acknowledged",
                            declaration.AcknowledgementDisallowable
                                ? "✅ Yes — I understand this expense may be disallowed for Corporation Tax purposes"
                                : "⚠️ Not acknowledged");
                    });
                });
            });
        }

        private static void AddTableRow(TableDescriptor table, string label, string value)
        {
            table.Cell().Background(Color.FromHex("#f8f9fa")).Padding(5)
                .Text(label).FontSize(9).SemiBold();
            table.Cell().Padding(5)
                .Text(value ?? "—").FontSize(9);
        }

        // ── Footer ─────────────────────────────────────────────────────────

        private void ComposeFooter(IContainer container, MissingReceiptDeclaration declaration)
        {
            container.Column(col =>
            {
                col.Item().LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten2);
                col.Item().PaddingTop(4).Row(row =>
                {
                    row.RelativeItem().Text($"Document ID: {declaration.DeclarationId}  |  This is an internal audit record")
                        .FontSize(7).FontColor(Colors.Grey.Medium);
                    if (!string.IsNullOrEmpty(declaration.HashSha256))
                    {
                        row.ConstantItem(200).AlignRight()
                            .Text($"SHA-256: {declaration.HashSha256[..16]}…")
                            .FontSize(7).FontColor(Colors.Grey.Medium);
                    }
                });
            });
        }
    }
}
