using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Globalization;
using FinanceHubFunctions.Models;
using FinanceHubFunctions.Data;
using FinanceHubFunctions.Services;
using FinanceHubFunctions.Helpers;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace FinanceHubFunctions.Functions
{
    public class BikFunctions
    {
        private readonly FinanceHubDbContext _db;
        private readonly EmailService? _emailService;
        private readonly IPayrollSettingsRepository? _payrollSettingsRepo;
        private readonly BlobStorageService? _blobStorage;
        private readonly DeletionGuardService? _guard;

        public BikFunctions(
            FinanceHubDbContext db,
            EmailService? emailService = null,
            IPayrollSettingsRepository? payrollSettingsRepo = null,
            BlobStorageService? blobStorage = null,
            DeletionGuardService? guard = null)
        {
            _db = db;
            _emailService = emailService;
            _payrollSettingsRepo = payrollSettingsRepo;
            _blobStorage = blobStorage;
            _guard = guard;
        }

        // GET /api/bik
        [Function("GetBikEntries")]
        public async Task<HttpResponseData> GetBikEntries(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "bik")] HttpRequestData req)
        {
            try
            {
                var query = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
                var taxYear = query["taxYear"];
                var entries = _db.BikEntries.AsQueryable();
                if (!string.IsNullOrEmpty(taxYear))
                    entries = entries.Where(e => e.TaxYear == taxYear);
                var list = await entries.OrderByDescending(e => e.CreatedDate).ToListAsync();
                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(list);
                return response;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"GetBikEntries ERROR: {ex.Message}");
                var err = req.CreateResponse(HttpStatusCode.InternalServerError);
                await err.WriteAsJsonAsync(new { error = ex.Message });
                return err;
            }
        }

        // POST /api/bik
        [Function("CreateBikEntry")]
        public async Task<HttpResponseData> CreateBikEntry(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "bik")] HttpRequestData req)
        {
            try
            {
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var entry = await JsonSerializer.DeserializeAsync<BikEntry>(req.Body, options);
                if (entry == null)
                {
                    var bad = req.CreateResponse(HttpStatusCode.BadRequest);
                    await bad.WriteStringAsync("Invalid request body.");
                    return bad;
                }
                entry.Id = 0;
                entry.CreatedDate = DateTime.UtcNow;
                entry.ModifiedDate = null;
                if (string.IsNullOrEmpty(entry.P11DSection))
                    entry.P11DSection = MapP11DSection(entry.BenefitCategory);
                _db.BikEntries.Add(entry);
                await _db.SaveChangesAsync();
                var response = req.CreateResponse(HttpStatusCode.Created);
                await response.WriteAsJsonAsync(entry);
                return response;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"CreateBikEntry ERROR: {ex.Message}");
                var err = req.CreateResponse(HttpStatusCode.InternalServerError);
                await err.WriteAsJsonAsync(new { error = ex.Message });
                return err;
            }
        }

        // PUT /api/bik/{id}
        [Function("UpdateBikEntry")]
        public async Task<HttpResponseData> UpdateBikEntry(
            [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "bik/{id:int}")] HttpRequestData req,
            int id)
        {
            try
            {
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var incoming = await JsonSerializer.DeserializeAsync<BikEntry>(req.Body, options);
                if (incoming == null)
                {
                    var bad = req.CreateResponse(HttpStatusCode.BadRequest);
                    await bad.WriteStringAsync("Invalid request body.");
                    return bad;
                }
                var existing = await _db.BikEntries.FindAsync(id);
                if (existing == null)
                {
                    var notFound = req.CreateResponse(HttpStatusCode.NotFound);
                    await notFound.WriteStringAsync($"BIK entry {id} not found.");
                    return notFound;
                }
                existing.TaxYear         = incoming.TaxYear;
                existing.RecipientName   = incoming.RecipientName;
                existing.RecipientType   = incoming.RecipientType;
                existing.BenefitCategory = incoming.BenefitCategory;
                existing.Description     = incoming.Description;
                existing.CashEquivalent  = incoming.CashEquivalent;
                existing.DateFrom        = incoming.DateFrom;
                existing.DateTo          = incoming.DateTo;
                existing.IsExempt        = incoming.IsExempt;
                existing.ExemptionReason = incoming.ExemptionReason;
                existing.P11DSection     = string.IsNullOrEmpty(incoming.P11DSection)
                                             ? MapP11DSection(incoming.BenefitCategory)
                                             : incoming.P11DSection;
                existing.Headcount       = incoming.Headcount;
                existing.TotalEventCost  = incoming.TotalEventCost;
                existing.Notes           = incoming.Notes;
                existing.ModifiedDate    = DateTime.UtcNow;
                await _db.SaveChangesAsync();
                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(existing);
                return response;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"UpdateBikEntry ERROR: {ex.Message}");
                var err = req.CreateResponse(HttpStatusCode.InternalServerError);
                await err.WriteAsJsonAsync(new { error = ex.Message });
                return err;
            }
        }

        // DELETE /api/bik/{id}
        [Function("DeleteBikEntry")]
        public async Task<HttpResponseData> DeleteBikEntry(
            [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "bik/{id:int}")] HttpRequestData req,
            int id)
        {
            try
            {
                if (_guard != null)
                {
                    var blocked = await _guard.GuardAsync(req, "BIK entry");
                    if (blocked != null) return blocked;
                }

                var existing = await _db.BikEntries.FindAsync(id);
                if (existing == null)
                {
                    var notFound = req.CreateResponse(HttpStatusCode.NotFound);
                    await notFound.WriteStringAsync($"BIK entry {id} not found.");
                    return notFound;
                }
                _db.BikEntries.Remove(existing);
                await _db.SaveChangesAsync();
                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(new { message = "Deleted successfully." });
                return response;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"DeleteBikEntry ERROR: {ex.Message}");
                var err = req.CreateResponse(HttpStatusCode.InternalServerError);
                await err.WriteAsJsonAsync(new { error = ex.Message });
                return err;
            }
        }

        // POST /api/bik/email-p11d
        [Function("EmailP11D")]
        public async Task<HttpResponseData> EmailP11D(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "bik/email-p11d")] HttpRequestData req)
        {
            try
            {
                if (_emailService == null)
                {
                    var err = req.CreateResponse(HttpStatusCode.InternalServerError);
                    await err.WriteAsJsonAsync(new { error = "Email service not available" });
                    return err;
                }
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var body = await JsonSerializer.DeserializeAsync<EmailP11DRequest>(req.Body, options);
                if (body == null || string.IsNullOrWhiteSpace(body.RecipientName) || string.IsNullOrWhiteSpace(body.ToEmail))
                {
                    var bad = req.CreateResponse(HttpStatusCode.BadRequest);
                    await bad.WriteStringAsync("recipientName and toEmail are required.");
                    return bad;
                }
                var entries = await _db.BikEntries
                    .Where(e => e.RecipientName == body.RecipientName &&
                                (string.IsNullOrEmpty(body.TaxYear) || e.TaxYear == body.TaxYear))
                    .OrderBy(e => e.BenefitCategory)
                    .ToListAsync();
                if (!entries.Any())
                {
                    var notFound = req.CreateResponse(HttpStatusCode.NotFound);
                    await notFound.WriteAsJsonAsync(new { error = $"No BIK entries found for {body.RecipientName}" });
                    return notFound;
                }
                var company         = await _db.CompanySettings.FirstOrDefaultAsync();
                var payrollSettings = _payrollSettingsRepo != null ? await _payrollSettingsRepo.GetAsync() : null;
                var fromEmail       = payrollSettings?.PayrollEmail;
                var payeRef         = payrollSettings?.EmployerPAYEReference ?? "Not set";
                var companyName     = company?.CompanyName ?? "Your Company";
                var taxYear         = body.TaxYear ?? entries.First().TaxYear;
                var accessToken     = AuthHelper.GetAccessToken(req) ?? "";
                var pdfBytes = await GenerateP11DPdfAsync(entries, body.RecipientName, taxYear, company, payrollSettings);
                var result = await _emailService.SendP11DEmailAsync(
                    body.ToEmail, body.RecipientName, taxYear,
                    entries, companyName, payeRef,
                    accessToken, pdfBytes, fromEmail);
                if (!result.Success)
                {
                    var err = req.CreateResponse(HttpStatusCode.InternalServerError);
                    await err.WriteAsJsonAsync(new { error = result.Error });
                    return err;
                }
                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(new { message = $"P11D emailed to {body.ToEmail}" });
                return response;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"EmailP11D ERROR: {ex.Message}");
                var err = req.CreateResponse(HttpStatusCode.InternalServerError);
                await err.WriteAsJsonAsync(new { error = ex.Message });
                return err;
            }
        }

        // GET /api/bik/p11d-pdf
        [Function("GetP11DPDF")]
        public async Task<HttpResponseData> GetP11DPDF(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "bik/p11d-pdf")] HttpRequestData req)
        {
            try
            {
                var query         = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
                var recipientName = query["recipientName"];
                var taxYear       = query["taxYear"];
                if (string.IsNullOrWhiteSpace(recipientName))
                {
                    var bad = req.CreateResponse(HttpStatusCode.BadRequest);
                    await bad.WriteStringAsync("recipientName is required.");
                    return bad;
                }
                var entries = await _db.BikEntries
                    .Where(e => e.RecipientName == recipientName &&
                                (string.IsNullOrEmpty(taxYear) || e.TaxYear == taxYear))
                    .OrderBy(e => e.BenefitCategory)
                    .ToListAsync();
                var company         = await _db.CompanySettings.FirstOrDefaultAsync();
                var payrollSettings = _payrollSettingsRepo != null ? await _payrollSettingsRepo.GetAsync() : null;
                var effectiveTaxYear = !string.IsNullOrEmpty(taxYear) ? taxYear
                    : (entries.FirstOrDefault()?.TaxYear ?? "Unknown");
                var pdfBytes = await GenerateP11DPdfAsync(entries, recipientName, effectiveTaxYear, company, payrollSettings);
                var safeName = recipientName.Replace(" ", "-");
                var safeYear = effectiveTaxYear.Replace("/", "-");
                var response = req.CreateResponse(HttpStatusCode.OK);
                response.Headers.Add("Content-Type", "application/pdf");
                response.Headers.Add("Content-Disposition", $"attachment; filename=\"P11D-{safeName}-{safeYear}.pdf\"");
                await response.Body.WriteAsync(pdfBytes);
                return response;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"GetP11DPDF ERROR: {ex.Message}");
                var err = req.CreateResponse(HttpStatusCode.InternalServerError);
                await err.WriteAsJsonAsync(new { error = ex.Message });
                return err;
            }
        }

        // Helpers
        private static string MapP11DSection(string category) => category switch
        {
            "PMI"                       => "M",
            "Annual Party"              => "N",
            "Gym Membership"            => "N",
            "Professional Subscription" => "N",
            "Company Car"               => "F",
            "Assets Transferred"        => "C",
            _                           => "N",
        };

        private async Task<byte[]> GenerateP11DPdfAsync(
            List<BikEntry> entries, string recipientName, string taxYear,
            CompanySettings? company, PayrollSettings? payrollSettings)
        {
            QuestPDF.Settings.License = LicenseType.Community;

            byte[]? logoBytes = null;
            if (company != null && _blobStorage != null)
            {
                try
                {
                    if (!string.IsNullOrWhiteSpace(company.DocumentLogoUrl))
                    {
                        var (b, _) = await _blobStorage.GetLogoBytesFromUrlAsync(company.DocumentLogoUrl);
                        logoBytes = b;
                    }
                    if (logoBytes == null)
                    {
                        var (b, _) = await _blobStorage.GetLogoAsync(company.Id);
                        logoBytes = b;
                    }
                }
                catch { /* no logo */ }
            }

            var companyName = company?.CompanyName ?? "Your Company";
            var companyAddr = company?.CompanyAddress ?? company?.Address ?? "";
            var payeRef     = payrollSettings?.EmployerPAYEReference ?? "Not set";
            var genDate     = DateTime.UtcNow.ToString("dd MMM yyyy HH:mm", CultureInfo.InvariantCulture);

            var taxableEntries = entries.Where(e => !e.IsExempt).ToList();
            var totalCashEquiv = taxableEntries.Sum(e => e.CashEquivalent);
            var totalClass1A   = totalCashEquiv * 0.138m;

            // Determine Scotland vs England/Wales/NI via employee tax code (S prefix = Scottish)
            var employee       = await _db.Employees.FirstOrDefaultAsync(e => e.Name != null &&
                                     e.Name.ToLower() == recipientName.ToLower());
            var isScottish     = PayrollCalculations.IsScottishTaxCode(employee?.TaxCode);
            var higherRate     = isScottish ? 0.42m : 0.40m;
            var regionLabel    = isScottish ? "Scotland" : "England / Wales / NI";
            var regionFlag     = isScottish ? "🏴󠁧󠁢󠁳󠁣󠁴󠁿" : "🏴󠁧󠁢󠁥󠁮󠁧󠁿";

            var estTaxBasic    = totalCashEquiv * 0.20m;
            var estTaxHigher   = totalCashEquiv * higherRate;
            var parts          = taxYear.Split('/');
            var deadlineYear   = parts.Length > 1 ? "20" + parts[1].Trim() : "";

            const string Navy    = "#0f2a4a";
            const string Muted   = "#6b7280";
            const string Grey50  = "#f9fafb";
            const string Grey100 = "#f3f4f6";
            const string Grey200 = "#e5e7eb";
            const string Grey300 = "#d1d5db";
            const string Grey400 = "#9ca3af";
            const string Text    = "#374151";
            const string Blue50  = "#eff6ff";
            const string Blue200 = "#bfdbfe";
            const string Blue800 = "#1e40af";

            Func<decimal, string> fmtM = v => "\u00a3" + v.ToString("N2");

            var doc = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(40);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontFamily("Arial").FontSize(9.5f).FontColor(Text));

                    // ── HEADER ───────────────────────────────────────────────
                    page.Header()
                        .BorderBottom(3).BorderColor(Navy)
                        .PaddingBottom(10)
                        .Row(row =>
                        {
                            row.RelativeItem().Column(col =>
                            {
                                if (logoBytes != null)
                                    col.Item().Height(52).Image(logoBytes);
                                col.Item().Text(companyName).Bold().FontSize(13).FontColor(Navy);
                                col.Item().Text("P11D \u2014 Expenses and Benefits").FontSize(8).FontColor(Muted);
                            });
                            row.ConstantItem(190).Column(col =>
                            {
                                col.Item().AlignRight()
                                   .Background(Navy).PaddingHorizontal(8).PaddingVertical(2)
                                   .Text("PRIVATE & CONFIDENTIAL")
                                   .FontSize(7).FontColor(Colors.White).Bold();
                                col.Item().PaddingTop(5).AlignRight()
                                   .DefaultTextStyle(x => x.FontSize(8.5f)).Text(t =>
                                   {
                                       t.Span("Tax Year: ").Bold().FontColor(Navy);
                                       t.Span(taxYear);
                                   });
                                col.Item().AlignRight()
                                   .DefaultTextStyle(x => x.FontSize(8.5f)).Text(t =>
                                   {
                                       t.Span("PAYE Ref: ").Bold().FontColor(Navy);
                                       t.Span(payeRef);
                                   });
                            });
                        });

                    // ── CONTENT ───────────────────────────────────────────────
                    page.Content().PaddingTop(12).Column(main =>
                    {
                        // Employee info bar
                        main.Item()
                            .Background(Grey100).Border(1).BorderColor(Grey200)
                            .Table(tbl =>
                            {
                                tbl.ColumnsDefinition(c =>
                                {
                                    c.RelativeColumn(2);
                                    c.RelativeColumn(1);
                                    c.RelativeColumn(1);
                                });
                                P11DInfoCell(tbl, "EMPLOYEE / DIRECTOR", recipientName);
                                P11DInfoCell(tbl, "TAX YEAR", taxYear);
                                P11DInfoCell(tbl, "TAX REGION", regionLabel);
                            });

                        // Section heading
                        main.Item().PaddingTop(14)
                            .BorderBottom(2).BorderColor(Navy).PaddingBottom(4)
                            .Text("BENEFITS IN KIND").FontSize(7.5f).Bold().FontColor(Navy);

                        // Benefits table
                        main.Item().Table(tbl =>
                        {
                            tbl.ColumnsDefinition(c =>
                            {
                                c.ConstantColumn(22);  // Section ref
                                c.RelativeColumn(2);   // Category
                                c.RelativeColumn(3);   // Description
                                c.ConstantColumn(62);  // Date From
                                c.ConstantColumn(62);  // Date To
                                c.ConstantColumn(68);  // Cash Equiv
                                c.ConstantColumn(68);  // Class 1A NI
                            });

                            // Header row
                            void Hdr(string label, bool right = false)
                            {
                                var cell = tbl.Cell().Background(Navy).Padding(6);
                                if (right) cell.AlignRight().Text(label).FontSize(7.5f).Bold().FontColor(Colors.White);
                                else       cell.Text(label).FontSize(7.5f).Bold().FontColor(Colors.White);
                            }
                            Hdr("\u00a7");
                            Hdr("Benefit Category");
                            Hdr("Description");
                            Hdr("Date From");
                            Hdr("Date To");
                            Hdr("Cash Equiv.", right: true);
                            Hdr("Class 1A NI", right: true);

                            // Data rows
                            bool alt = false;
                            foreach (var e in entries)
                            {
                                var class1A = e.IsExempt ? 0m : e.CashEquivalent * 0.138m;
                                var bg = alt ? Grey50 : "#ffffff";

                                tbl.Cell().Background(bg).BorderBottom(1).BorderColor(Grey200)
                                   .Padding(6).Text(e.P11DSection ?? "N").FontSize(8.5f).FontColor(Muted);
                                tbl.Cell().Background(bg).BorderBottom(1).BorderColor(Grey200)
                                   .Padding(6).Text(e.BenefitCategory).FontSize(8.5f);
                                tbl.Cell().Background(bg).BorderBottom(1).BorderColor(Grey200)
                                   .Padding(6).Text(e.Description ?? "").FontSize(8.5f).FontColor(Muted);
                                tbl.Cell().Background(bg).BorderBottom(1).BorderColor(Grey200)
                                   .Padding(6).Text(e.DateFrom.HasValue ? e.DateFrom.Value.ToString("dd/MM/yy") : "-").FontSize(8f);
                                tbl.Cell().Background(bg).BorderBottom(1).BorderColor(Grey200)
                                   .Padding(6).Text(e.DateTo.HasValue ? e.DateTo.Value.ToString("dd/MM/yy") : "-").FontSize(8f);
                                tbl.Cell().Background(bg).BorderBottom(1).BorderColor(Grey200)
                                   .Padding(6).AlignRight().Text(fmtM(e.CashEquivalent)).FontSize(8.5f);
                                tbl.Cell().Background(bg).BorderBottom(1).BorderColor(Grey200)
                                   .Padding(6).AlignRight()
                                   .Text(e.IsExempt ? "Exempt" : fmtM(class1A))
                                   .FontSize(8.5f).FontColor(e.IsExempt ? Muted : Text);
                                alt = !alt;
                            }

                            // Totals row
                            tbl.Cell().ColumnSpan(5)
                               .Background(Grey100).BorderTop(2).BorderColor(Grey300)
                               .Padding(7).AlignRight()
                               .Text($"{entries.Count} benefit{(entries.Count == 1 ? "" : "s")} \u2014 Cash Equivalent Total:")
                               .FontSize(8.5f).Bold();
                            tbl.Cell().Background(Grey100).BorderTop(2).BorderColor(Grey300)
                               .Padding(7).AlignRight().Text(fmtM(totalCashEquiv)).FontSize(9).Bold();
                            tbl.Cell().Background(Grey100).BorderTop(2).BorderColor(Grey300)
                               .Padding(7).AlignRight().Text(fmtM(totalClass1A)).FontSize(9).Bold().FontColor(Navy);
                        });

                        // Summary boxes — employee income tax is the primary figure
                        const string Amber50  = "#fffbeb";
                        const string Amber200 = "#fde68a";
                        const string Amber800 = "#92400e";

                        // Cash equivalent + employee tax side by side (main focus)
                        main.Item().PaddingTop(12).Row(row =>
                        {
                            row.RelativeItem().PaddingRight(6)
                               .Background(Blue50).Border(1).BorderColor(Blue200)
                               .Padding(10).Column(c =>
                               {
                                   c.Item().Text("Total Taxable Cash Equivalent").FontSize(7.5f).FontColor(Muted);
                                   c.Item().Text(fmtM(totalCashEquiv)).FontSize(16).Bold().FontColor(Navy);
                                   c.Item().PaddingTop(2).Text($"{entries.Count} reportable benefit{(entries.Count == 1 ? "" : "s")}").FontSize(7f).FontColor(Muted);
                               });
                            row.RelativeItem().PaddingLeft(6)
                               .Background(Amber50).Border(1).BorderColor(Amber200)
                               .Padding(10).Column(c =>
                               {
                                   c.Item().Text("Estimated Employee Income Tax").FontSize(7.5f).FontColor(Muted);
                                   c.Item().Text(fmtM(estTaxBasic)).FontSize(16).Bold().FontColor(Amber800);
                                   c.Item().PaddingTop(2).DefaultTextStyle(x => x.FontSize(7f)).Text(t =>
                                   {
                                       t.Span("20% basic rate shown").FontColor(Muted);
                                       t.Span($"  ·  {(int)(higherRate * 100)}% higher: ").FontColor(Muted);
                                       t.Span(fmtM(estTaxHigher)).Bold().FontColor(Amber800);
                                   });
                                   c.Item().Text($"Collected via PAYE coding · {regionLabel}").FontSize(7f).FontColor(Muted);
                               });
                        });

                        // Employer NI — secondary, clearly labelled as employer-only
                        main.Item().PaddingTop(6)
                            .Background("#f8f8f8").Border(1).BorderColor(Grey200)
                            .Padding(8).Row(row =>
                            {
                                row.AutoItem().PaddingRight(6)
                                   .Text("🏛️ FOR EMPLOYER").FontSize(7f).Bold().FontColor(Muted);
                                row.RelativeItem().DefaultTextStyle(x => x.FontSize(7.5f)).Text(t =>
                                {
                                    t.Span("Class 1A NI payable by employer: ").FontColor(Muted);
                                    t.Span(fmtM(totalClass1A)).Bold().FontColor("#6b7280");
                                    t.Span(" (13.8% of cash equivalent · due 22 July · not deducted from employee)").FontColor(Muted);
                                });
                            });

                        // Deadlines
                        if (!string.IsNullOrEmpty(deadlineYear))
                        {
                            main.Item().PaddingTop(10)
                                .Background(Blue50).Border(1).BorderColor(Blue200)
                                .Padding(10).Column(c =>
                                {
                                    c.Item().DefaultTextStyle(x => x.FontSize(8.5f)).Text(t =>
                                    {
                                        t.Span("What happens next: ").Bold();
                                        t.Span($"After HMRC receives this P11D, they will adjust ");
                                        t.Span(recipientName + "\u2019s").Bold();
                                        t.Span(" PAYE tax code to collect the income tax due. ");
                                        t.Span($"{regionLabel} rates apply — no payment is required directly from the employee.");
                                    });
                                    c.Item().PaddingTop(4).DefaultTextStyle(x => x.FontSize(8f).FontColor(Muted)).Text(t =>
                                    {
                                        t.Span("P11D filing deadline: ").Bold();
                                        t.Span($"6 July {deadlineYear}.");
                                        t.Span($" Employer Class 1A NI ({fmtM(totalClass1A)}) due 22 July {deadlineYear} — paid by employer, not deducted from employee.");
                                    });
                                });
                        }
                    });

                    // ── FOOTER ───────────────────────────────────────────────
                    page.Footer()
                        .BorderTop(1).BorderColor(Grey200).PaddingTop(8)
                        .Column(col =>
                        {
                            col.Item().AlignCenter()
                               .DefaultTextStyle(x => x.FontSize(7.5f).FontColor(Grey400)).Text(t =>
                               {
                                   t.Span(companyName);
                                   if (!string.IsNullOrWhiteSpace(companyAddr))
                                   { t.Span("  \u00b7  "); t.Span(companyAddr); }
                               });
                            col.Item().AlignCenter()
                               .DefaultTextStyle(x => x.FontSize(7.5f)).Text(t =>
                               {
                                   t.Span("Employer PAYE Ref: ").FontColor(Grey400);
                                   t.Span(payeRef).Bold().FontColor(Text);
                                   t.Span("  \u00b7  File online: ").FontColor(Grey400);
                                   t.Span("www.gov.uk/paye-online").FontColor(Text);
                                   t.Span("  \u00b7  Generated ").FontColor(Grey400);
                                   t.Span(genDate + " UTC").FontColor(Grey400);
                               });
                        });
                });
            });

            return doc.GeneratePdf();
        }

        private static void P11DInfoCell(TableDescriptor tbl, string label, string value)
        {
            tbl.Cell().Padding(9).Column(c =>
            {
                c.Item().Text(label).FontSize(7.5f).FontColor("#9ca3af");
                c.Item().Text(value).FontSize(9).Bold().FontColor("#111827");
            });
        }

        private record EmailP11DRequest
        {
            public string RecipientName { get; init; } = "";
            public string TaxYear       { get; init; } = "";
            public string ToEmail       { get; init; } = "";
        }
    }
}
