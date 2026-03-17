using System;
using System.IO;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using FinanceHubFunctions.Data;
using FinanceHubFunctions.Models;
using FinanceHubFunctions.Services;

namespace FinanceHubFunctions.Functions
{
    public class CreditNoteFunctions
    {
        private readonly ICreditNoteRepository _repo;
        private readonly IInvoiceRepository _invoiceRepo;
        private readonly ICompanySettingsRepository _companyRepo;
        private readonly CreditNotePdfService _pdfService;
        private readonly EmailService _emailService;
        private readonly ILogger<CreditNoteFunctions> _logger;

        private static readonly JsonSerializerOptions _jsonOpts = new()
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
        };

        public CreditNoteFunctions(
            ICreditNoteRepository repo,
            IInvoiceRepository invoiceRepo,
            ICompanySettingsRepository companyRepo,
            CreditNotePdfService pdfService,
            EmailService emailService,
            ILogger<CreditNoteFunctions> logger)
        {
            _repo = repo;
            _invoiceRepo = invoiceRepo;
            _companyRepo = companyRepo;
            _pdfService = pdfService;
            _emailService = emailService;
            _logger = logger;
        }

        // ─── GET /api/creditnotes ───────────────────────────────────────────
        [Function("GetCreditNotes")]
        public async Task<HttpResponseData> GetCreditNotes(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "creditnotes")] HttpRequestData req)
        {
            try
            {
                var all = await _repo.GetAllAsync();
                var resp = req.CreateResponse(HttpStatusCode.OK);
                await resp.WriteAsJsonAsync(all);
                return resp;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting credit notes");
                var err = req.CreateResponse(HttpStatusCode.InternalServerError);
                await err.WriteAsJsonAsync(new { error = ex.Message });
                return err;
            }
        }

        // ─── GET /api/creditnotes/{id} ──────────────────────────────────────
        [Function("GetCreditNoteById")]
        public async Task<HttpResponseData> GetCreditNoteById(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "creditnotes/{id:int}")] HttpRequestData req,
            int id)
        {
            var cn = await _repo.GetByIdAsync(id);
            if (cn == null)
            {
                var nf = req.CreateResponse(HttpStatusCode.NotFound);
                await nf.WriteAsJsonAsync(new { error = "Credit note not found" });
                return nf;
            }
            var resp = req.CreateResponse(HttpStatusCode.OK);
            await resp.WriteAsJsonAsync(cn);
            return resp;
        }

        // ─── GET /api/customers/{customerId}/creditnotes ────────────────────
        [Function("GetCreditNotesByCustomer")]
        public async Task<HttpResponseData> GetCreditNotesByCustomer(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "customers/{customerId}/creditnotes")] HttpRequestData req,
            string customerId)
        {
            var notes = await _repo.GetByCustomerIdAsync(customerId);
            var resp = req.CreateResponse(HttpStatusCode.OK);
            await resp.WriteAsJsonAsync(notes);
            return resp;
        }

        // ─── POST /api/creditnotes ──────────────────────────────────────────
        [Function("CreateCreditNote")]
        public async Task<HttpResponseData> CreateCreditNote(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "creditnotes")] HttpRequestData req)
        {
            try
            {
                var body = await new StreamReader(req.Body).ReadToEndAsync();
                var dto = JsonSerializer.Deserialize<CreateCreditNoteDto>(body, _jsonOpts);

                if (dto == null || string.IsNullOrWhiteSpace(dto.CustomerName))
                {
                    var bad = req.CreateResponse(HttpStatusCode.BadRequest);
                    await bad.WriteAsJsonAsync(new { error = "CustomerName and Reason are required" });
                    return bad;
                }

                var company = await _companyRepo.GetDefaultAsync();
                var fy = GetFinancialYear(dto.DateIssued ?? DateTime.UtcNow, company);

                // Resolve original invoice details
                string? origInvoiceNumber = dto.OriginalInvoiceNumber;
                if (dto.OriginalInvoiceId.HasValue && string.IsNullOrWhiteSpace(origInvoiceNumber))
                {
                    var inv = await _invoiceRepo.GetByIdAsync(dto.OriginalInvoiceId.Value);
                    origInvoiceNumber = inv?.InvoiceNumber;
                }

                var cn = new CreditNote
                {
                    CreditNoteNumber = await _repo.GenerateNextCreditNoteNumberAsync(),
                    CustomerId = dto.CustomerId,
                    CustomerName = dto.CustomerName,
                    CustomerEmail = dto.CustomerEmail ?? "",
                    OriginalInvoiceId = dto.OriginalInvoiceId,
                    OriginalInvoiceNumber = origInvoiceNumber,
                    Reason = dto.Reason ?? "",
                    ReasonCategory = dto.ReasonCategory ?? "Other",
                    AmountNet = dto.AmountNet,
                    VATRate = dto.VATRate,
                    VATAmount = dto.VATAmount,
                    AmountGross = dto.AmountGross,
                    Currency = dto.Currency ?? "GBP",
                    Status = "Draft",
                    DateIssued = dto.DateIssued ?? DateTime.UtcNow,
                    ExpiryDate = dto.ExpiryDate,
                    Notes = dto.Notes,
                    FinancialYear = fy,
                    TaxYear = (dto.DateIssued ?? DateTime.UtcNow).Year,
                    CreatedAt = DateTime.UtcNow
                };

                // Generate PDF immediately and update status to Issued
                if (company != null)
                {
                    try
                    {
                        var blobRef = await _pdfService.GenerateAndUploadAsync(cn, company);
                        cn.PdfUrl = blobRef;
                    }
                    catch (Exception pdfEx)
                    {
                        _logger.LogWarning(pdfEx, "PDF generation failed for credit note {Number}", cn.CreditNoteNumber);
                    }
                }

                cn.Status = "Issued";
                var created = await _repo.CreateAsync(cn);

                var resp = req.CreateResponse(HttpStatusCode.Created);
                await resp.WriteAsJsonAsync(created);
                return resp;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating credit note");
                var err = req.CreateResponse(HttpStatusCode.InternalServerError);
                await err.WriteAsJsonAsync(new { error = ex.Message });
                return err;
            }
        }

        // ─── POST /api/creditnotes/{id}/send ───────────────────────────────
        [Function("SendCreditNoteEmail")]
        public async Task<HttpResponseData> SendCreditNoteEmail(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "creditnotes/{id:int}/send")] HttpRequestData req,
            int id)
        {
            try
            {
                var cn = await _repo.GetByIdAsync(id);
                if (cn == null)
                {
                    var nf = req.CreateResponse(HttpStatusCode.NotFound);
                    await nf.WriteAsJsonAsync(new { error = "Credit note not found" });
                    return nf;
                }

                if (string.IsNullOrWhiteSpace(cn.CustomerEmail))
                {
                    var bad = req.CreateResponse(HttpStatusCode.BadRequest);
                    await bad.WriteAsJsonAsync(new { error = "No customer email address on this credit note" });
                    return bad;
                }

                var company = await _companyRepo.GetDefaultAsync();

                // Regenerate PDF if missing
                byte[] pdfBytes;
                if (company != null)
                {
                    pdfBytes = await _pdfService.GeneratePdfAsync(cn, company);
                    // Refresh blob ref
                    var blobRef = await _pdfService.GenerateAndUploadAsync(cn, company);
                    cn.PdfUrl = blobRef;
                    cn.UpdatedAt = DateTime.UtcNow;
                    await _repo.UpdateAsync(cn);
                }
                else
                {
                    var bad = req.CreateResponse(HttpStatusCode.InternalServerError);
                    await bad.WriteAsJsonAsync(new { error = "Company settings not found" });
                    return bad;
                }

                var result = await _emailService.SendCreditNoteEmailAsync(cn.CustomerEmail, cn, pdfBytes, "");

                if (!result.Success)
                {
                    var fail = req.CreateResponse(HttpStatusCode.InternalServerError);
                    await fail.WriteAsJsonAsync(new { error = result.Error });
                    return fail;
                }

                var resp = req.CreateResponse(HttpStatusCode.OK);
                await resp.WriteAsJsonAsync(new { sent = true, to = cn.CustomerEmail });
                return resp;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending credit note email {Id}", id);
                var err = req.CreateResponse(HttpStatusCode.InternalServerError);
                await err.WriteAsJsonAsync(new { error = ex.Message });
                return err;
            }
        }

        // ─── POST /api/creditnotes/{id}/apply ──────────────────────────────
        [Function("ApplyCreditNote")]
        public async Task<HttpResponseData> ApplyCreditNote(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "creditnotes/{id:int}/apply")] HttpRequestData req,
            int id)
        {
            try
            {
                var body = await new StreamReader(req.Body).ReadToEndAsync();
                var dto = JsonSerializer.Deserialize<ApplyCreditNoteDto>(body, _jsonOpts);

                var cn = await _repo.GetByIdAsync(id);
                if (cn == null)
                {
                    var nf = req.CreateResponse(HttpStatusCode.NotFound);
                    await nf.WriteAsJsonAsync(new { error = "Credit note not found" });
                    return nf;
                }

                if (cn.Status != "Issued")
                {
                    var conflict = req.CreateResponse(HttpStatusCode.Conflict);
                    await conflict.WriteAsJsonAsync(new { error = $"Credit note is already {cn.Status}" });
                    return conflict;
                }

                Invoice? invoice = null;
                if (dto?.InvoiceId.HasValue == true)
                {
                    invoice = await _invoiceRepo.GetByIdAsync(dto.InvoiceId.Value);
                }

                cn.Status = "Applied";
                cn.DateApplied = DateTime.UtcNow;
                cn.AppliedToInvoiceId = invoice?.Id;
                cn.AppliedToInvoiceNumber = invoice?.InvoiceNumber;
                cn.UpdatedAt = DateTime.UtcNow;
                await _repo.UpdateAsync(cn);

                var resp = req.CreateResponse(HttpStatusCode.OK);
                await resp.WriteAsJsonAsync(cn);
                return resp;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error applying credit note {Id}", id);
                var err = req.CreateResponse(HttpStatusCode.InternalServerError);
                await err.WriteAsJsonAsync(new { error = ex.Message });
                return err;
            }
        }

        // ─── POST /api/creditnotes/{id}/void ───────────────────────────────
        [Function("VoidCreditNote")]
        public async Task<HttpResponseData> VoidCreditNote(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "creditnotes/{id:int}/void")] HttpRequestData req,
            int id)
        {
            try
            {
                var cn = await _repo.GetByIdAsync(id);
                if (cn == null)
                {
                    var nf = req.CreateResponse(HttpStatusCode.NotFound);
                    await nf.WriteAsJsonAsync(new { error = "Credit note not found" });
                    return nf;
                }

                if (cn.Status == "Applied")
                {
                    var conflict = req.CreateResponse(HttpStatusCode.Conflict);
                    await conflict.WriteAsJsonAsync(new { error = "Cannot void a credit note that has already been applied" });
                    return conflict;
                }

                cn.Status = "Voided";
                cn.UpdatedAt = DateTime.UtcNow;
                await _repo.UpdateAsync(cn);

                var resp = req.CreateResponse(HttpStatusCode.OK);
                await resp.WriteAsJsonAsync(cn);
                return resp;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error voiding credit note {Id}", id);
                var err = req.CreateResponse(HttpStatusCode.InternalServerError);
                await err.WriteAsJsonAsync(new { error = ex.Message });
                return err;
            }
        }

        // ─── GET /api/creditnotes/{id}/pdf ──────────────────────────────────
        [Function("GetCreditNotePdf")]
        public async Task<HttpResponseData> GetCreditNotePdf(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "creditnotes/{id:int}/pdf")] HttpRequestData req,
            int id)
        {
            try
            {
                var cn = await _repo.GetByIdAsync(id);
                if (cn == null)
                {
                    var nf = req.CreateResponse(HttpStatusCode.NotFound);
                    await nf.WriteAsJsonAsync(new { error = "Credit note not found" });
                    return nf;
                }

                var company = await _companyRepo.GetDefaultAsync();
                var pdfBytes = await _pdfService.GeneratePdfAsync(cn, company!);

                var resp = req.CreateResponse(HttpStatusCode.OK);
                resp.Headers.Add("Content-Type", "application/pdf");
                resp.Headers.Add("Content-Disposition", $"inline; filename=\"CreditNote-{cn.CreditNoteNumber}.pdf\"");
                await resp.WriteBytesAsync(pdfBytes);
                return resp;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating credit note PDF {Id}", id);
                var err = req.CreateResponse(HttpStatusCode.InternalServerError);
                await err.WriteAsJsonAsync(new { error = ex.Message });
                return err;
            }
        }

        // ─── DELETE /api/creditnotes/{id} ───────────────────────────────────
        [Function("DeleteCreditNote")]
        public async Task<HttpResponseData> DeleteCreditNote(
            [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "creditnotes/{id:int}")] HttpRequestData req,
            int id)
        {
            try
            {
                var cn = await _repo.GetByIdAsync(id);
                if (cn == null)
                {
                    var nf = req.CreateResponse(HttpStatusCode.NotFound);
                    await nf.WriteAsJsonAsync(new { error = "Credit note not found" });
                    return nf;
                }

                if (cn.Status == "Applied")
                {
                    var conflict = req.CreateResponse(HttpStatusCode.Conflict);
                    await conflict.WriteAsJsonAsync(new { error = "Cannot delete an applied credit note" });
                    return conflict;
                }

                await _repo.DeleteAsync(id);
                var resp = req.CreateResponse(HttpStatusCode.NoContent);
                return resp;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting credit note {Id}", id);
                var err = req.CreateResponse(HttpStatusCode.InternalServerError);
                await err.WriteAsJsonAsync(new { error = ex.Message });
                return err;
            }
        }

        private static string GetFinancialYear(DateTime date, CompanySettings? company)
        {
            int fyStartMonth = company?.FYStartMonth ?? 4;
            int year = date.Month >= fyStartMonth ? date.Year : date.Year - 1;
            return $"FY{year}/{(year + 1) % 100:D2}";
        }
    }

    public class CreateCreditNoteDto
    {
        public string? CustomerId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string? CustomerEmail { get; set; }
        public int? OriginalInvoiceId { get; set; }
        public string? OriginalInvoiceNumber { get; set; }
        public string? Reason { get; set; }
        public string? ReasonCategory { get; set; }
        public decimal AmountNet { get; set; }
        public decimal VATRate { get; set; }
        public decimal VATAmount { get; set; }
        public decimal AmountGross { get; set; }
        public string? Currency { get; set; }
        public DateTime? DateIssued { get; set; }
        public DateTime? ExpiryDate { get; set; }
        public string? Notes { get; set; }
    }

    public class ApplyCreditNoteDto
    {
        public int? InvoiceId { get; set; }
    }
}
