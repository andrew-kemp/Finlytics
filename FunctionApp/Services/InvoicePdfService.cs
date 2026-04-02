using System;
using System.IO;
using System.Threading.Tasks;
using FinanceHubFunctions.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace FinanceHubFunctions.Services
{
    public class InvoicePdfService
    {
        private readonly BlobStorageService _blobStorageService;

        public InvoicePdfService(BlobStorageService blobStorageService)
        {
            _blobStorageService = blobStorageService;
        }

        public async Task<byte[]> GenerateInvoicePdfAsync(Invoice invoice, CompanySettings company, Customer customer)
        {
            QuestPDF.Settings.License = LicenseType.Community;

            // Get currency symbol - fallback to £ if not set
            string currencySymbol = !string.IsNullOrEmpty(company.CurrencySymbol) ? company.CurrencySymbol : "£";

            // Get logo directly from blob storage with authentication
            // Prefer DocumentLogoUrl if set, then fall back to blob search
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
            catch
            {
                // Logo failed to load - continue without it
            }

            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(40);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(10));

                    page.Header().Element(c => ComposeHeader(c, invoice, company, logoBytes));
                    page.Content().Element(c => ComposeContent(c, invoice, customer, company, currencySymbol));
                    page.Footer().Element(c => ComposeFooter(c, company));
                });
            });

            return document.GeneratePdf();
        }

        private void ComposeHeader(IContainer container, Invoice invoice, CompanySettings company, byte[] logoBytes)
        {
            container.Row(row =>
            {
                row.RelativeItem().Column(column =>
                {
                    if (logoBytes != null)
                    {
                        column.Item().Height(60).Image(logoBytes);
                        column.Item().PaddingTop(10);
                    }

                    column.Item().Text(company.CompanyName).FontSize(14).Bold();
                    if (!string.IsNullOrEmpty(company.Address))
                    {
                        var addressLines = company.Address.Split(new[] { "\n", "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
                        foreach (var line in addressLines)
                        {
                            column.Item().Text(line).FontSize(9);
                        }
                    }
                    if (!string.IsNullOrEmpty(company.Email))
                        column.Item().Text(company.Email).FontSize(9);
                    if (!string.IsNullOrEmpty(company.PhoneNumber))
                        column.Item().Text(company.PhoneNumber).FontSize(9);
                    if (!string.IsNullOrEmpty(company.VATNumber))
                        column.Item().Text($"VAT: {company.VATNumber}").FontSize(9);
                });

                row.RelativeItem().AlignRight().Column(column =>
                {
                    column.Item().Text("INVOICE").FontSize(20).Bold();
                    column.Item().PaddingTop(5);
                    column.Item().Text($"Invoice #: {invoice.InvoiceNumber}").FontSize(10).Bold();
                    column.Item().Text($"Date: {invoice.DateIssued:dd/MM/yyyy}").FontSize(9);
                    if (invoice.DueDate.HasValue)
                    {
                        column.Item().Text($"Due: {invoice.DueDate.Value:dd/MM/yyyy}").FontSize(9);
                        
                        // Calculate payment days and add payment terms
                        var paymentDays = (invoice.DueDate.Value - invoice.DateIssued).Days;
                        if (paymentDays > 0)
                        {
                            column.Item().Text($"Payment Terms: {paymentDays} days").FontSize(8).Italic();
                        }
                    }
                    if (!string.IsNullOrEmpty(invoice.POReference))
                        column.Item().Text($"PO Ref: {invoice.POReference}").FontSize(9);
                });
            });
        }

        private void ComposeContent(IContainer container, Invoice invoice, Customer customer, CompanySettings company, string currencySymbol)
        {
            container.PaddingVertical(20).Column(column =>
            {
                // Bill To section
                column.Item().Text("Bill To:").FontSize(11).Bold();
                column.Item().PaddingBottom(5);
                var customerName = customer?.CustomerName ?? invoice.CustomerName ?? "";
                if (!string.IsNullOrWhiteSpace(customerName))
                {
                    column.Item().Text(customerName).FontSize(10).Bold();
                }
                if (!string.IsNullOrWhiteSpace(customer?.ContactName))
                {
                    column.Item().Text(customer.ContactName).FontSize(9);
                }
                if (!string.IsNullOrEmpty(customer?.BillingAddress))
                {
                    var addressLines = customer.BillingAddress.Split(new[] { "\n", "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var line in addressLines)
                    {
                        column.Item().Text(line).FontSize(9);
                    }
                }
                var billingEmail = customer?.Email ?? invoice.BillingEmail;
                if (!string.IsNullOrEmpty(billingEmail))
                    column.Item().Text($"Email: {billingEmail}").FontSize(9);

                column.Item().PaddingVertical(20);

                // Line Items Table
                column.Item().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.ConstantColumn(40);  // Line #
                        columns.RelativeColumn(3);   // Description
                        columns.RelativeColumn(1);   // Qty
                        columns.RelativeColumn(1);   // Rate Type
                        columns.RelativeColumn(1);   // Rate
                        columns.RelativeColumn(1);   // VAT %
                        columns.RelativeColumn(1);   // Total
                    });

                    // Header
                    table.Header(header =>
                    {
                        header.Cell().Element(HeaderStyle).Text("#");
                        header.Cell().Element(HeaderStyle).Text("Description");
                        header.Cell().Element(HeaderStyle).AlignRight().Text("Qty");
                        header.Cell().Element(HeaderStyle).Text("Type");
                        header.Cell().Element(HeaderStyle).AlignRight().Text("Rate");
                        header.Cell().Element(HeaderStyle).AlignRight().Text("VAT %");
                        header.Cell().Element(HeaderStyle).AlignRight().Text("Total");
                    });

                    // Line Items
                    foreach (var line in invoice.LineItems ?? new System.Collections.Generic.List<InvoiceLine>())
                    {
                        table.Cell().Element(CellStyle).Text(line.LineNumber.ToString());
                        table.Cell().Element(CellStyle).Text(line.Description);
                        table.Cell().Element(CellStyle).AlignRight().Text(line.Quantity.ToString("F2"));
                        table.Cell().Element(CellStyle).Text(line.RateType == "Day Rate" ? "Day" : "Hour");
                        table.Cell().Element(CellStyle).AlignRight().Text(currencySymbol + line.Rate.ToString("F2"));
                        table.Cell().Element(CellStyle).AlignRight().Text(line.VATRate.ToString("F0") + "%");
                        table.Cell().Element(CellStyle).AlignRight().Text(currencySymbol + line.LineTotal.ToString("F2"));
                    }
                });

                column.Item().PaddingTop(20);

                // Totals
                column.Item().AlignRight().Column(totals =>
                {
                    totals.Item().Row(row =>
                    {
                        row.RelativeItem().Text("Subtotal:").FontSize(10);
                        row.ConstantItem(100).AlignRight().Text(currencySymbol + invoice.AmountNet.ToString("F2")).FontSize(10);
                    });

                    if (invoice.DiscountPercent.HasValue || invoice.DiscountAmount.HasValue)
                    {
                        totals.Item().Row(row =>
                        {
                            var discountText = "Discount";
                            if (invoice.DiscountPercent.HasValue)
                                discountText += $" ({invoice.DiscountPercent.Value}%)";
                            if (!string.IsNullOrEmpty(invoice.DiscountNote))
                                discountText += $" - {invoice.DiscountNote}";
                            
                            row.RelativeItem().Text(discountText + ":").FontSize(10);
                            row.ConstantItem(100).AlignRight().Text("-" + currencySymbol + (invoice.DiscountAmount ?? 0).ToString("F2")).FontSize(10);
                        });
                    }

                    totals.Item().Row(row =>
                    {
                        row.RelativeItem().Text("VAT:").FontSize(10);
                        row.ConstantItem(100).AlignRight().Text(currencySymbol + invoice.VATAmount.ToString("F2")).FontSize(10);
                    });

                    if (invoice.CreditNoteDeduction.HasValue && invoice.CreditNoteDeduction.Value > 0)
                    {
                        totals.Item().Row(row =>
                        {
                            var cnLabel = "Credit Note Applied";
                            if (!string.IsNullOrEmpty(invoice.CreditNoteNumber))
                                cnLabel += $" ({invoice.CreditNoteNumber})";
                            cnLabel += ":";
                            row.RelativeItem().Text(cnLabel).FontSize(10).FontColor("#dc2626");
                            row.ConstantItem(100).AlignRight().Text("-" + currencySymbol + invoice.CreditNoteDeduction.Value.ToString("F2")).FontSize(10).FontColor("#dc2626");
                        });
                    }

                    totals.Item().PaddingTop(5).Row(row =>
                    {
                        row.RelativeItem().Text("Total:").FontSize(12).Bold();
                        row.ConstantItem(100).AlignRight().Text(currencySymbol + invoice.AmountGross.ToString("F2")).FontSize(12).Bold();
                    });
                });

                // Payment terms
                if (!string.IsNullOrEmpty(company.PaymentTerms))
                {
                    column.Item().PaddingTop(20).Text("Payment Terms:").FontSize(10).Bold();
                    column.Item().Text(company.PaymentTerms).FontSize(9);
                }
            });
        }

        private void ComposeFooter(IContainer container, CompanySettings company)
        {
            container.AlignCenter().Column(column =>
            {
                if (!string.IsNullOrEmpty(company.BankName) || !string.IsNullOrEmpty(company.BankAccountNumber) || !string.IsNullOrEmpty(company.AccountNumber))
                {
                    column.Item().BorderTop(1).BorderColor(Colors.Grey.Lighten2).PaddingTop(10);
                    column.Item().Text("Payment Details").FontSize(9).Bold();
                    
                    if (!string.IsNullOrEmpty(company.BankName))
                        column.Item().Text($"Bank: {company.BankName}").FontSize(8);
                    
                    if (!string.IsNullOrEmpty(company.BankAccountNumber) || !string.IsNullOrEmpty(company.AccountNumber))
                        column.Item().Text($"Account: {company.BankAccountNumber ?? company.AccountNumber}").FontSize(8);
                    
                    if (!string.IsNullOrEmpty(company.BankSortCode) || !string.IsNullOrEmpty(company.SortCode))
                        column.Item().Text($"Sort Code: {company.BankSortCode ?? company.SortCode}").FontSize(8);
                    
                    if (!string.IsNullOrEmpty(company.BankIBAN))
                        column.Item().Text($"IBAN: {company.BankIBAN}").FontSize(8);
                    
                    if (!string.IsNullOrEmpty(company.BankSwiftCode))
                        column.Item().Text($"SWIFT/BIC: {company.BankSwiftCode}").FontSize(8);
                    
                    // Payment terms
                    if (!string.IsNullOrEmpty(company.InvoiceTermsDays))
                    {
                        column.Item().PaddingTop(5).Text($"Payment Terms: {company.InvoiceTermsDays} days").FontSize(8).Italic();
                    }
                    else if (!string.IsNullOrEmpty(company.PaymentTerms))
                    {
                        column.Item().PaddingTop(5).Text($"Payment Terms: {company.PaymentTerms}").FontSize(8).Italic();
                    }
                }

                // Payment contact information
                if (!string.IsNullOrEmpty(company.PaymentsEmail))
                {
                    column.Item().PaddingTop(10).Text($"For payment queries, please contact: {company.PaymentsEmail}").FontSize(8).Italic();
                }

                var autoFooterText = BuildCompanyRegistrationFooter(company);
                var footerText = !string.IsNullOrEmpty(autoFooterText)
                    ? autoFooterText
                    : (company.InvoiceFooterText ?? company.FooterText);

                if (!string.IsNullOrEmpty(footerText))
                {
                    column.Item().PaddingTop(10).Text(footerText).FontSize(8).Italic();
                }
            });
        }

        private string BuildCompanyRegistrationFooter(CompanySettings company)
        {
            var parts = new System.Collections.Generic.List<string>();

            var addressValue = company.CompanyAddress ?? company.Address;
            var addressSnippet = addressValue != null ? addressValue.Substring(0, Math.Min(50, addressValue.Length)) : "N/A";
            Console.WriteLine($"Building registration footer for company: {company.CompanyName}, RegNo: {company.CompanyRegistrationNumber}, VAT: {company.VATNumber}, Address: {addressSnippet}");

            // Determine company location based on registration number
            string companyLocation = "England and Wales";
            if (!string.IsNullOrEmpty(company.CompanyRegistrationNumber) && 
                company.CompanyRegistrationNumber.TrimStart().StartsWith("SC", StringComparison.OrdinalIgnoreCase))
            {
                companyLocation = "Scotland";
            }

            // Build footer text
            if (!string.IsNullOrEmpty(company.CompanyName))
            {
                parts.Add($"{company.CompanyName} is a company registered in {companyLocation}");
                
                if (!string.IsNullOrEmpty(company.CompanyRegistrationNumber))
                {
                    parts.Add($"under company number {company.CompanyRegistrationNumber}");
                }
            }

            // Check both VAT fields with fallback
            var vatNumber = company.VatRegistrationNumber ?? company.VATNumber;
            if (!string.IsNullOrEmpty(vatNumber))
            {
                parts.Add($"VAT registration number: {vatNumber}");
            }

            if (!string.IsNullOrEmpty(addressValue))
            {
                // Convert multi-line address to comma-separated
                var address = addressValue
                    .Replace("\r\n", ", ")
                    .Replace("\n", ", ")
                    .Replace("\r", ", ");
                
                // Remove double commas and trim
                while (address.Contains(", ,"))
                    address = address.Replace(", ,", ",");
                
                parts.Add($"Registered office: {address.Trim()}");
            }

            if (parts.Count > 0)
            {
                var footer = string.Join(". ", parts).TrimEnd('.') + ".";
                footer += $"\n\n© {DateTime.Now.Year} {company.CompanyName ?? "Company"}. All rights reserved.";
                Console.WriteLine($"Generated footer: {footer}");
                return footer;
            }

            Console.WriteLine("No footer parts generated");
            return string.Empty;
        }

        private static IContainer HeaderStyle(IContainer container)
        {
            return container.BorderBottom(1).BorderColor(Colors.Grey.Medium).PaddingVertical(5).Background(Colors.Grey.Lighten3);
        }

        private static IContainer CellStyle(IContainer container)
        {
            return container.BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingVertical(5);
        }

        public async Task<byte[]> GenerateQuotePdfAsync(Quote quote, CompanySettings company, Customer customer, string contactEmail = null)
        {
            QuestPDF.Settings.License = LicenseType.Community;

            // Get currency symbol - fallback to £ if not set
            string currencySymbol = !string.IsNullOrEmpty(company.CurrencySymbol) ? company.CurrencySymbol : "£";

            // Get logo directly from blob storage with authentication
            // Prefer DocumentLogoUrl if set, then fall back to blob search
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
            catch
            {
                // Logo failed to load - continue without it
            }

            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(40);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(10));

                    page.Header().Element(c => ComposeQuoteHeader(c, quote, company, logoBytes, contactEmail));
                    page.Content().Element(c => ComposeQuoteContent(c, quote, customer, company, currencySymbol));
                    page.Footer().Element(c => ComposeQuoteFooter(c, company));
                });
            });

            return document.GeneratePdf();
        }

        private void ComposeQuoteHeader(IContainer container, Quote quote, CompanySettings company, byte[] logoBytes, string contactEmail)
        {
            container.Row(row =>
            {
                row.RelativeItem().Column(column =>
                {
                    if (logoBytes != null)
                    {
                        column.Item().Height(60).Image(logoBytes);
                        column.Item().PaddingTop(10);
                    }

                    column.Item().Text(company.CompanyName).FontSize(14).Bold();
                    if (!string.IsNullOrEmpty(company.Address))
                    {
                        var addressLines = company.Address.Split(new[] { "\n", "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
                        foreach (var line in addressLines)
                        {
                            column.Item().Text(line).FontSize(9);
                        }
                    }
                    // Use contactEmail if provided, otherwise fallback to company.Email
                    var emailToDisplay = !string.IsNullOrEmpty(contactEmail) ? contactEmail : company.Email;
                    if (!string.IsNullOrEmpty(emailToDisplay))
                        column.Item().Text(emailToDisplay).FontSize(9);
                    if (!string.IsNullOrEmpty(company.PhoneNumber))
                        column.Item().Text(company.PhoneNumber).FontSize(9);
                    var vatNumber = company.VatRegistrationNumber ?? company.VATNumber;
                    if (!string.IsNullOrEmpty(vatNumber))
                        column.Item().Text($"VAT: {vatNumber}").FontSize(9);
                });

                row.RelativeItem().AlignRight().Column(column =>
                {
                    column.Item().Text("QUOTATION").FontSize(20).Bold();
                    column.Item().PaddingTop(5);
                    column.Item().Text($"Quote #: {quote.QuoteNumber}").FontSize(10).Bold();
                    column.Item().Text($"Date: {quote.DateIssued:dd/MM/yyyy}").FontSize(9);
                    if (quote.ValidUntil.HasValue)
                        column.Item().Text($"Valid Until: {quote.ValidUntil.Value:dd/MM/yyyy}").FontSize(9);
                });
            });
        }

        private void ComposeQuoteContent(IContainer container, Quote quote, Customer customer, CompanySettings company, string currencySymbol)
        {
            container.PaddingVertical(20).Column(column =>
            {
                // Customer section
                column.Item().Text("For:").FontSize(11).Bold();
                column.Item().PaddingBottom(5);
                var customerName = customer?.CustomerName ?? quote.CustomerName ?? "";
                if (!string.IsNullOrWhiteSpace(customerName))
                {
                    column.Item().Text(customerName).FontSize(10).Bold();
                }

                var emailToShow = !string.IsNullOrWhiteSpace(quote.BillingEmail)
                    ? quote.BillingEmail
                    : customer?.Email;

                if (!string.IsNullOrEmpty(emailToShow))
                {
                    column.Item().Text($"Email: {emailToShow}").FontSize(9);
                }

                column.Item().PaddingVertical(20);

                // Line Items Table
                column.Item().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.ConstantColumn(40);  // Line #
                        columns.RelativeColumn(3);   // Description
                        columns.RelativeColumn(1);   // Qty
                        columns.RelativeColumn(1);   // Rate Type
                        columns.RelativeColumn(1);   // Rate
                        columns.RelativeColumn(1);   // VAT %
                        columns.RelativeColumn(1);   // Total
                    });

                    // Header
                    table.Header(header =>
                    {
                        header.Cell().Element(HeaderStyle).Text("#");
                        header.Cell().Element(HeaderStyle).Text("Description");
                        header.Cell().Element(HeaderStyle).AlignRight().Text("Qty");
                        header.Cell().Element(HeaderStyle).Text("Type");
                        header.Cell().Element(HeaderStyle).AlignRight().Text("Rate");
                        header.Cell().Element(HeaderStyle).AlignRight().Text("VAT %");
                        header.Cell().Element(HeaderStyle).AlignRight().Text("Total");
                    });

                    // Line Items
                    foreach (var line in quote.LineItems ?? new System.Collections.Generic.List<QuoteLine>())
                    {
                        table.Cell().Element(CellStyle).Text(line.LineNumber.ToString());
                        table.Cell().Element(CellStyle).Text(line.Description);
                        table.Cell().Element(CellStyle).AlignRight().Text(line.Quantity.ToString("F2"));
                        table.Cell().Element(CellStyle).Text(line.RateType == "Day Rate" ? "Day" : "Hour");
                        table.Cell().Element(CellStyle).AlignRight().Text(currencySymbol + line.Rate.ToString("F2"));
                        table.Cell().Element(CellStyle).AlignRight().Text(line.VATRate.ToString("F0") + "%");
                        table.Cell().Element(CellStyle).AlignRight().Text(currencySymbol + line.LineTotal.ToString("F2"));
                    }
                });

                column.Item().PaddingTop(20);

                // Totals
                column.Item().AlignRight().Column(totals =>
                {
                    totals.Item().Row(row =>
                    {
                        row.RelativeItem().Text("Subtotal:").FontSize(10);
                        row.ConstantItem(100).AlignRight().Text(currencySymbol + quote.AmountNet.ToString("F2")).FontSize(10);
                    });

                    if (quote.DiscountPercent.HasValue || quote.DiscountAmount.HasValue)
                    {
                        totals.Item().Row(row =>
                        {
                            var discountText = "Discount";
                            if (quote.DiscountPercent.HasValue)
                                discountText += $" ({quote.DiscountPercent.Value}%)";
                            if (!string.IsNullOrEmpty(quote.DiscountNote))
                                discountText += $" - {quote.DiscountNote}";
                            
                            row.RelativeItem().Text(discountText + ":").FontSize(10);
                            row.ConstantItem(100).AlignRight().Text("-" + currencySymbol + (quote.DiscountAmount ?? 0).ToString("F2")).FontSize(10);
                        });
                    }

                    totals.Item().Row(row =>
                    {
                        row.RelativeItem().Text("VAT:").FontSize(10);
                        row.ConstantItem(100).AlignRight().Text(currencySymbol + quote.VATAmount.ToString("F2")).FontSize(10);
                    });

                    totals.Item().PaddingTop(5).Row(row =>
                    {
                        row.RelativeItem().Text("Total:").FontSize(12).Bold();
                        row.ConstantItem(100).AlignRight().Text(currencySymbol + quote.AmountGross.ToString("F2")).FontSize(12).Bold();
                    });
                });

                // Quote validity message
                column.Item().PaddingTop(20).Text("This quotation is valid until " + (quote.ValidUntil?.ToString("dd MMMM yyyy") ?? "acceptance") + ".").FontSize(9).Italic();
                
                // Note: Payment terms are not included in quotes as they apply after acceptance/conversion to invoice
            });
        }

        private void ComposeQuoteFooter(IContainer container, CompanySettings company)
        {
            container.AlignCenter().Column(column =>
            {
                // Only show company footer text for quotes, no payment details
                var autoFooterText = BuildCompanyRegistrationFooter(company);
                var footerText = !string.IsNullOrEmpty(autoFooterText)
                    ? autoFooterText
                    : (company.InvoiceFooterText ?? company.FooterText);

                if (!string.IsNullOrEmpty(footerText))
                {
                    column.Item().PaddingTop(10).Text(footerText).FontSize(8).Italic();
                }
            });
        }
    }
}
