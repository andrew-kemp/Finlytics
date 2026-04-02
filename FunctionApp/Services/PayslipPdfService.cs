using System;
using System.Globalization;
using System.Threading.Tasks;
using FinanceHubFunctions.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace FinanceHubFunctions.Services
{
    public class PayslipPdfService
    {
        private readonly BlobStorageService _blobStorageService;

        public PayslipPdfService(BlobStorageService blobStorageService)
        {
            _blobStorageService = blobStorageService;
        }

        public async Task<byte[]> GeneratePayslipPdfAsync(
            Payslip slip,
            PayrollRun run,
            CompanySettings? company,
            PayrollSettings? settings = null)
        {
            QuestPDF.Settings.License = LicenseType.Community;

            byte[]? logoBytes = null;
            if (company != null)
            {
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
                catch { /* Continue without logo */ }
            }

            var fmt       = (decimal? v) => "£" + (v ?? 0m).ToString("N2");
            var period    = run.PayDate.ToString("MMMM yyyy",   CultureInfo.InvariantCulture);
            var payDate   = run.PayDate.ToString("dd MMM yyyy", CultureInfo.InvariantCulture);
            var compName  = company?.CompanyName ?? "Company";
            var compAddr  = company?.CompanyAddress ?? company?.Address ?? "";
            var genDate   = DateTime.UtcNow.ToString("dd MMM yyyy HH:mm", CultureInfo.InvariantCulture);
            var annSalary = (slip.GrossPay ?? 0m) * 12m;
            var totalDed  = (slip.Tax ?? 0m) + (slip.NationalInsurance ?? 0m) + (slip.Pension ?? 0m);
            var payeRef   = settings?.EmployerPAYEReference ?? settings?.AccountsOfficeReference ?? "—";
            var hasPension= (slip.Pension ?? 0m) != 0m;

            // Colour palette
            const string Navy    = "#0f2a4a";
            const string Muted   = "#6b7280";
            const string Grey50  = "#f9fafb";
            const string Grey100 = "#f3f4f6";
            const string Grey200 = "#e5e7eb";
            const string Grey300 = "#d1d5db";
            const string Grey400 = "#9ca3af";
            const string Text    = "#374151";
            const string Green50  = "#f0fdf4";
            const string Green200 = "#bbf7d0";
            const string Green800 = "#166534";

            var doc = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(40);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontFamily("Arial").FontSize(9.5f).FontColor(Text));

                    // ─────────────────────────────────────────────────────────
                    //  HEADER
                    // ─────────────────────────────────────────────────────────
                    page.Header()
                        .BorderBottom(3).BorderColor(Navy)
                        .PaddingBottom(10)
                        .Row(row =>
                        {
                            // Left: logo + company name
                            row.RelativeItem().Column(col =>
                            {
                                if (logoBytes != null)
                                    col.Item().Height(52).Image(logoBytes);
                                col.Item().Text(compName).Bold().FontSize(13).FontColor(Navy);
                                col.Item().Text("Employee Payslip").FontSize(8).FontColor(Muted);
                            });

                            // Right: period / dates
                            row.ConstantItem(195).Column(col =>
                            {
                                col.Item().AlignRight()
                                   .Background(Navy).PaddingHorizontal(8).PaddingVertical(2)
                                   .Text("PRIVATE & CONFIDENTIAL")
                                   .FontSize(7).FontColor(Colors.White).Bold();

                                col.Item().PaddingTop(5).AlignRight().DefaultTextStyle(x => x.FontSize(8.5f)).Text(t =>
                                {
                                    t.Span("Pay Period: ").Bold().FontColor(Navy);
                                    t.Span(period);
                                });

                                col.Item().AlignRight().DefaultTextStyle(x => x.FontSize(8.5f)).Text(t =>
                                {
                                    t.Span("Pay Date: ").Bold().FontColor(Navy);
                                    t.Span(payDate);
                                });

                                col.Item().AlignRight().DefaultTextStyle(x => x.FontSize(8.5f)).Text(t =>
                                {
                                    t.Span("Tax Year: ").Bold().FontColor(Navy);
                                    t.Span(run.TaxYear ?? "—");
                                    t.Span("  ·  Month: ").Bold().FontColor(Navy);
                                    t.Span((run.TaxMonth ?? 0).ToString());
                                });
                            });
                        });

                    // ─────────────────────────────────────────────────────────
                    //  CONTENT
                    // ─────────────────────────────────────────────────────────
                    page.Content().PaddingTop(12).Column(main =>
                    {
                        // ── Employee info bar ────────────────────────────────
                        main.Item()
                            .Background(Grey100).Border(1).BorderColor(Grey200)
                            .Table(tbl =>
                            {
                                tbl.ColumnsDefinition(c =>
                                {
                                    c.RelativeColumn(2); c.RelativeColumn(1);
                                    c.RelativeColumn(1); c.RelativeColumn(1);
                                    c.RelativeColumn(1);
                                });
                                InfoCell(tbl, "EMPLOYEE",    slip.EmployeeName ?? "—");
                                InfoCell(tbl, "EMPLOYEE ID", slip.EmployeeNumber ?? $"EMP-{slip.EmployeeId:D3}");
                                InfoCell(tbl, "NI NUMBER",   slip.NiNumber     ?? "—");
                                InfoCell(tbl, "TAX CODE",    slip.TaxCode      ?? "—");
                                InfoCell(tbl, "NI CATEGORY", slip.NiCategory   ?? "A");
                            });

                        // ── Earnings / Deductions ────────────────────────────
                        main.Item().PaddingTop(10).Row(row =>
                        {
                            // Earnings column
                            row.RelativeItem().PaddingRight(8).Column(col =>
                            {
                                col.Item()
                                   .BorderBottom(2).BorderColor(Navy).PaddingBottom(4)
                                   .Text("EARNINGS").FontSize(7.5f).Bold().FontColor(Navy);

                                col.Item().Table(tbl =>
                                {
                                    tbl.ColumnsDefinition(c => { c.RelativeColumn(); c.ConstantColumn(70); });

                                    tbl.Cell().BorderBottom(1).BorderColor(Grey100).Padding(5)
                                       .Text("Basic Pay").FontSize(9);
                                    tbl.Cell().BorderBottom(1).BorderColor(Grey100).Padding(5).AlignRight()
                                       .Text(fmt(slip.GrossPay)).FontSize(9);

                                    tbl.Cell().Background(Grey50).BorderTop(2).BorderColor(Grey300).Padding(7)
                                       .Text("Total Earnings").FontSize(9).Bold();
                                    tbl.Cell().Background(Grey50).BorderTop(2).BorderColor(Grey300).Padding(7).AlignRight()
                                       .Text(fmt(slip.GrossPay)).FontSize(9).Bold();
                                });
                            });

                            // Deductions column
                            row.RelativeItem().PaddingLeft(8).Column(col =>
                            {
                                col.Item()
                                   .BorderBottom(2).BorderColor(Navy).PaddingBottom(4)
                                   .Text("DEDUCTIONS").FontSize(7.5f).Bold().FontColor(Navy);

                                col.Item().Table(tbl =>
                                {
                                    tbl.ColumnsDefinition(c => { c.RelativeColumn(); c.ConstantColumn(70); });

                                    tbl.Cell().BorderBottom(1).BorderColor(Grey100).Padding(5)
                                       .Text($"Income Tax (Code {slip.TaxCode ?? "1257L"})").FontSize(9);
                                    tbl.Cell().BorderBottom(1).BorderColor(Grey100).Padding(5).AlignRight()
                                       .Text(fmt(slip.Tax)).FontSize(9);

                                    tbl.Cell().BorderBottom(1).BorderColor(Grey100).Padding(5)
                                       .Text($"National Insurance (Cat {slip.NiCategory ?? "A"})").FontSize(9);
                                    tbl.Cell().BorderBottom(1).BorderColor(Grey100).Padding(5).AlignRight()
                                       .Text(fmt(slip.NationalInsurance)).FontSize(9);

                                    if (hasPension)
                                    {
                                        tbl.Cell().BorderBottom(1).BorderColor(Grey100).Padding(5).Text("Pension").FontSize(9);
                                        tbl.Cell().BorderBottom(1).BorderColor(Grey100).Padding(5).AlignRight().Text(fmt(slip.Pension)).FontSize(9);
                                    }

                                    tbl.Cell().Background(Grey50).BorderTop(2).BorderColor(Grey300).Padding(7)
                                       .Text("Total Deductions").FontSize(9).Bold();
                                    tbl.Cell().Background(Grey50).BorderTop(2).BorderColor(Grey300).Padding(7).AlignRight()
                                       .Text(fmt(totalDed)).FontSize(9).Bold();
                                });
                            });
                        });

                        // ── Net Pay ──────────────────────────────────────────
                        main.Item().PaddingTop(10).Row(row =>
                        {
                            row.RelativeItem().PaddingRight(8)
                               .Background(Green50).Border(1).BorderColor(Green200)
                               .Padding(10).Column(c =>
                               {
                                   c.Item().Text("Net Pay (Amount Paid)").FontSize(7.5f).FontColor(Muted);
                                   c.Item().Text(fmt(slip.NetPay)).FontSize(18).Bold().FontColor(Green800);
                               });

                            row.RelativeItem().PaddingLeft(8)
                               .Background(Grey50).Border(1).BorderColor(Grey200)
                               .Padding(10).Column(c =>
                               {
                                   c.Item().Text("Payment Method").FontSize(7.5f).FontColor(Muted);
                                   c.Item().Text("BACS").FontSize(12).Bold().FontColor(Text);
                                   c.Item().PaddingTop(4).Text(t =>
                                   {
                                       t.Span("Annual Salary: ").FontColor(Muted).FontSize(8);
                                       t.Span("£" + annSalary.ToString("N2")).Bold().FontColor(Text).FontSize(8);
                                   });
                               });
                        });

                        // ── YTD Running Totals ───────────────────────────────
                        main.Item().PaddingTop(12)
                            .BorderBottom(2).BorderColor(Navy).PaddingBottom(4)
                            .Text("TAX YEAR TO DATE — RUNNING TOTALS")
                            .FontSize(7.5f).Bold().FontColor(Navy);

                        main.Item().Table(tbl =>
                        {
                            tbl.ColumnsDefinition(c =>
                            {
                                c.RelativeColumn(); c.RelativeColumn();
                                c.RelativeColumn(); c.RelativeColumn();
                            });

                            tbl.Cell().Background(Grey100).BorderBottom(1).BorderColor(Grey200)
                               .Padding(5).Text("YTD Gross Pay").FontSize(8).FontColor(Muted).SemiBold();
                            tbl.Cell().Background(Grey100).BorderBottom(1).BorderColor(Grey200)
                               .Padding(5).AlignRight().Text("YTD Income Tax").FontSize(8).FontColor(Muted).SemiBold();
                            tbl.Cell().Background(Grey100).BorderBottom(1).BorderColor(Grey200)
                               .Padding(5).AlignRight().Text("YTD Employee NI").FontSize(8).FontColor(Muted).SemiBold();
                            tbl.Cell().Background(Grey100).BorderBottom(1).BorderColor(Grey200)
                               .Padding(5).AlignRight().Text("YTD Employer NI").FontSize(8).FontColor(Muted).SemiBold();

                            tbl.Cell().Padding(6).Text(fmt(slip.YtdGross)).FontSize(9).Bold();
                            tbl.Cell().Padding(6).AlignRight().Text(fmt(slip.YtdTax)).FontSize(9);
                            tbl.Cell().Padding(6).AlignRight().Text(fmt(slip.YtdEmployeeNi)).FontSize(9);
                            tbl.Cell().Padding(6).AlignRight().Text(fmt(slip.YtdEmployerNi)).FontSize(9);
                        });


                    });

                    // ─────────────────────────────────────────────────────────
                    //  FOOTER
                    // ─────────────────────────────────────────────────────────
                    page.Footer()
                        .BorderTop(1).BorderColor(Grey200)
                        .PaddingTop(8)
                        .AlignCenter()
                        .Column(col =>
                        {
                            col.Item().AlignCenter().DefaultTextStyle(x => x.FontSize(7.5f).FontColor(Grey400)).Text(t =>
                            {
                                t.Span(compName);
                                if (!string.IsNullOrWhiteSpace(compAddr))
                                {
                                    t.Span("  ·  ");
                                    t.Span(compAddr);
                                }
                            });

                            col.Item().AlignCenter().DefaultTextStyle(x => x.FontSize(7.5f)).Text(t =>
                            {
                                t.Span("Employer PAYE Ref: ").FontColor(Grey400);
                                t.Span(payeRef).Bold().FontColor(Text);
                                t.Span("  ·  Generated ").FontColor(Grey400);
                                t.Span(genDate + " UTC").FontColor(Grey400);
                            });
                        });
                });
            });

            return doc.GeneratePdf();
        }

        // ── Helper: employee info bar cell ───────────────────────────────────
        private static void InfoCell(TableDescriptor tbl, string label, string value)
        {
            tbl.Cell().Padding(9).Column(c =>
            {
                c.Item().Text(label).FontSize(7.5f).FontColor("#9ca3af");
                c.Item().Text(value).FontSize(9).Bold().FontColor("#111827");
            });
        }
    }
}
