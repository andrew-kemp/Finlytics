using System;
using System.IO;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using FinanceHubFunctions.Data;
using FinanceHubFunctions.Helpers;
using FinanceHubFunctions.Models;
using FinanceHubFunctions.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace FinanceHubFunctions.Functions
{
    public class MissingReceiptDeclarationFunctions
    {
        private readonly IMissingReceiptDeclarationRepository _declarationRepo;
        private readonly IExpenseAuditEventRepository _auditRepo;
        private readonly IExpenseRepository _expenseRepo;
        private readonly ICompanySettingsRepository _settingsRepo;
        private readonly MissingReceiptDeclarationPdfService _pdfService;
        private readonly ILogger<MissingReceiptDeclarationFunctions> _logger;

        public MissingReceiptDeclarationFunctions(
            IMissingReceiptDeclarationRepository declarationRepo,
            IExpenseAuditEventRepository auditRepo,
            IExpenseRepository expenseRepo,
            ICompanySettingsRepository settingsRepo,
            MissingReceiptDeclarationPdfService pdfService,
            ILogger<MissingReceiptDeclarationFunctions> logger)
        {
            _declarationRepo = declarationRepo;
            _auditRepo = auditRepo;
            _expenseRepo = expenseRepo;
            _settingsRepo = settingsRepo;
            _pdfService = pdfService;
            _logger = logger;
        }

        // ── GET /expenses/{expenseId}/declaration ─────────────────────────

        [Function("GetMissingReceiptDeclaration")]
        public async Task<HttpResponseData> GetDeclaration(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get",
                Route = "expenses/{expenseId}/declaration")] HttpRequestData req,
            int expenseId)
        {
            try
            {
                var declaration = await _declarationRepo.GetByExpenseIdAsync(expenseId);
                if (declaration == null)
                {
                    var notFound = req.CreateResponse(HttpStatusCode.NotFound);
                    await notFound.WriteAsJsonAsync(new { message = "No declaration found for this expense" });
                    return notFound;
                }

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(declaration);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting declaration for expense {ExpenseId}", expenseId);
                var error = req.CreateResponse(HttpStatusCode.InternalServerError);
                await error.WriteAsJsonAsync(new { error = ex.Message });
                return error;
            }
        }

        // ── POST /expenses/{expenseId}/declaration ────────────────────────

        [Function("CreateMissingReceiptDeclaration")]
        public async Task<HttpResponseData> CreateDeclaration(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post",
                Route = "expenses/{expenseId}/declaration")] HttpRequestData req,
            int expenseId)
        {
            try
            {
                var body = await new StreamReader(req.Body).ReadToEndAsync();
                var dto = JsonSerializer.Deserialize<MissingReceiptDeclarationDto>(body, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
                });

                if (dto == null)
                {
                    var bad = req.CreateResponse(HttpStatusCode.BadRequest);
                    await bad.WriteAsJsonAsync(new { error = "Invalid request body" });
                    return bad;
                }

                if (string.IsNullOrWhiteSpace(dto.Description))
                {
                    var bad = req.CreateResponse(HttpStatusCode.BadRequest);
                    await bad.WriteAsJsonAsync(new { error = "Description is required" });
                    return bad;
                }

                // Verify expense exists
                var expense = await _expenseRepo.GetByIdAsync(expenseId);
                if (expense == null)
                {
                    var notFound = req.CreateResponse(HttpStatusCode.NotFound);
                    await notFound.WriteAsJsonAsync(new { error = "Expense not found" });
                    return notFound;
                }

                // Generate declaration ID
                var declarationId = await _declarationRepo.GenerateNextDeclarationIdAsync();

                var declaration = new MissingReceiptDeclaration
                {
                    DeclarationId = declarationId,
                    ExpenseId = expenseId,
                    CreatedAt = DateTime.UtcNow,
                    DeclarationType = dto.DeclarationType,
                    DeclarerName = dto.DeclarerName,
                    DeclarerRole = dto.DeclarerRole,
                    DeclarerEmail = dto.DeclarerEmail,
                    AmountGross = dto.AmountGross ?? (decimal)(expense.AmountGross ?? 0),
                    Currency = dto.Currency ?? "GBP",
                    ExpenseDate = dto.ExpenseDate ?? expense.DatePaid ?? expense.EntryDate,
                    MerchantOrPayee = dto.MerchantOrPayee ?? expense.Supplier,
                    BankTransactionRef = dto.BankTransactionRef ?? expense.Reference,
                    ExpenseCategory = dto.ExpenseCategory ?? expense.Category,
                    Description = dto.Description,
                    ReasonReceiptMissing = dto.ReasonReceiptMissing,
                    OtherReasonText = dto.OtherReasonText,
                    VatReclaimable = false, // Always false
                    VatAmount = 0m,         // Always zero
                    AcknowledgementDisallowable = dto.AcknowledgementDisallowable,
                    SignatureType = dto.SignatureType,
                    TypedSignature = dto.TypedSignature,
                    Status = DeclarationStatus.Draft
                };

                var created = await _declarationRepo.CreateAsync(declaration);

                // Audit event
                await _auditRepo.CreateAsync(new ExpenseAuditEvent
                {
                    ExpenseId = expenseId,
                    EventType = ExpenseAuditEventType.DeclarationCreated,
                    OccurredAt = DateTime.UtcNow,
                    ActorName = dto.DeclarerName,
                    ActorEmail = dto.DeclarerEmail,
                    Details = $"Draft declaration {declarationId} created. Reason: {declaration.ReasonReceiptMissing}",
                    DeclarationId = created.Id
                });

                var response = req.CreateResponse(HttpStatusCode.Created);
                await response.WriteAsJsonAsync(created);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating declaration for expense {ExpenseId}", expenseId);
                var error = req.CreateResponse(HttpStatusCode.InternalServerError);
                await error.WriteAsJsonAsync(new { error = ex.Message });
                return error;
            }
        }

        // ── POST /expenses/{expenseId}/declaration/finalise ───────────────

        [Function("FinaliseMissingReceiptDeclaration")]
        public async Task<HttpResponseData> FinaliseDeclaration(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post",
                Route = "expenses/{expenseId}/declaration/finalise")] HttpRequestData req,
            int expenseId)
        {
            try
            {
                var declaration = await _declarationRepo.GetByExpenseIdAsync(expenseId);
                if (declaration == null)
                {
                    var notFound = req.CreateResponse(HttpStatusCode.NotFound);
                    await notFound.WriteAsJsonAsync(new { error = "No draft declaration found" });
                    return notFound;
                }

                if (declaration.Status == DeclarationStatus.Finalised)
                {
                    var conflict = req.CreateResponse(HttpStatusCode.Conflict);
                    await conflict.WriteAsJsonAsync(new { error = "Declaration is already finalised" });
                    return conflict;
                }

                if (!declaration.AcknowledgementDisallowable)
                {
                    var bad = req.CreateResponse(HttpStatusCode.BadRequest);
                    await bad.WriteAsJsonAsync(new { error = "Acknowledgement of CT disallowable status is required before finalising" });
                    return bad;
                }

                // Get company settings for PDF
                var company = await _settingsRepo.GetDefaultAsync();

                // Generate and upload PDF
                declaration.FinalisedAt = DateTime.UtcNow;
                declaration.Status = DeclarationStatus.Finalised;
                declaration.VatReclaimable = false;
                declaration.VatAmount = 0m;

                // Fetch as tracked entity for update
                var tracked = await _declarationRepo.GetByIdAsync(declaration.Id);
                tracked!.Status = DeclarationStatus.Finalised;
                tracked.FinalisedAt = DateTime.UtcNow;

                try
                {
                    var (blobRef, hash) = await _pdfService.GenerateAndUploadAsync(tracked, company);
                    tracked.PdfBlobRef = blobRef;
                    tracked.HashSha256 = hash;
                }
                catch (Exception pdfEx)
                {
                    _logger.LogWarning(pdfEx, "PDF generation failed for declaration {DeclarationId} — finalising without PDF", declaration.DeclarationId);
                }

                var finalised = await _declarationRepo.UpdateAsync(tracked);

                // Direct SQL UPDATE — mark declaration on file and zero VAT on the expense
                await _expenseRepo.MarkDeclarationFiledAsync(expenseId, declaration.DeclarationId!);

                var expense = await _expenseRepo.GetByIdAsync(expenseId);
                if (expense != null)
                {
                    await _auditRepo.CreateAsync(new ExpenseAuditEvent
                    {
                        ExpenseId = expenseId,
                        EventType = ExpenseAuditEventType.VatDisabledDueToMissingReceipt,
                        OccurredAt = DateTime.UtcNow,
                        ActorName = declaration.DeclarerName,
                        ActorEmail = declaration.DeclarerEmail,
                        Details = $"VAT set to zero on expense {expense.ExpenseId} because declaration {declaration.DeclarationId} was finalised",
                        DeclarationId = finalised.Id
                    });
                }

                await _auditRepo.CreateAsync(new ExpenseAuditEvent
                {
                    ExpenseId = expenseId,
                    EventType = ExpenseAuditEventType.DeclarationFinalised,
                    OccurredAt = DateTime.UtcNow,
                    ActorName = declaration.DeclarerName,
                    ActorEmail = declaration.DeclarerEmail,
                    Details = $"Declaration {declaration.DeclarationId} finalised. PDF: {finalised.PdfBlobRef ?? "not generated"}",
                    DeclarationId = finalised.Id
                });

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(finalised);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error finalising declaration for expense {ExpenseId}", expenseId);
                var error = req.CreateResponse(HttpStatusCode.InternalServerError);
                await error.WriteAsJsonAsync(new { error = ex.Message });
                return error;
            }
        }

        // ── POST /expenses/{expenseId}/declaration/void ───────────────────

        [Function("VoidMissingReceiptDeclaration")]
        public async Task<HttpResponseData> VoidDeclaration(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post",
                Route = "expenses/{expenseId}/declaration/void")] HttpRequestData req,
            int expenseId)
        {
            try
            {
                var body = await new StreamReader(req.Body).ReadToEndAsync();
                var dto = JsonSerializer.Deserialize<VoidDeclarationDto>(body, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                var declaration = await _declarationRepo.GetByExpenseIdAsync(expenseId);
                if (declaration == null)
                {
                    var notFound = req.CreateResponse(HttpStatusCode.NotFound);
                    await notFound.WriteAsJsonAsync(new { error = "No declaration found" });
                    return notFound;
                }

                var tracked = await _declarationRepo.GetByIdAsync(declaration.Id);
                tracked!.Status = DeclarationStatus.Voided;
                tracked.VoidedAt = DateTime.UtcNow;
                tracked.VoidedReason = dto?.Reason;

                var voided = await _declarationRepo.UpdateAsync(tracked);

                // Update expense — clear declaration flag
                var expense = await _expenseRepo.GetByIdAsync(expenseId);
                if (expense != null)
                {
                    expense.HasMissingReceiptDeclaration = false;
                    expense.MissingReceiptDeclarationRef = null;
                    await _expenseRepo.UpdateAsync(expense);
                }

                await _auditRepo.CreateAsync(new ExpenseAuditEvent
                {
                    ExpenseId = expenseId,
                    EventType = ExpenseAuditEventType.DeclarationVoided,
                    OccurredAt = DateTime.UtcNow,
                    Details = $"Declaration {declaration.DeclarationId} voided. Reason: {dto?.Reason ?? "none"}",
                    DeclarationId = voided.Id
                });

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(voided);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error voiding declaration for expense {ExpenseId}", expenseId);
                var error = req.CreateResponse(HttpStatusCode.InternalServerError);
                await error.WriteAsJsonAsync(new { error = ex.Message });
                return error;
            }
        }

        // ── GET /expenses/{expenseId}/declaration/pdf ─────────────────────

        [Function("DownloadDeclarationPdf")]
        public async Task<HttpResponseData> DownloadDeclarationPdf(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get",
                Route = "expenses/{expenseId}/declaration/pdf")] HttpRequestData req,
            int expenseId)
        {
            try
            {
                var declaration = await _declarationRepo.GetByExpenseIdAsync(expenseId);
                if (declaration == null)
                {
                    var notFound = req.CreateResponse(HttpStatusCode.NotFound);
                    await notFound.WriteAsJsonAsync(new { error = "No declaration found" });
                    return notFound;
                }

                // If already finalised with a stored PDF, serve it from blob
                if (declaration.Status == DeclarationStatus.Finalised && !string.IsNullOrEmpty(declaration.PdfBlobRef))
                {
                    var storedPdf = await _pdfService.GetStoredPdfAsync(declaration.PdfBlobRef);
                    if (storedPdf != null)
                    {
                        var blobResponse = req.CreateResponse(HttpStatusCode.OK);
                        blobResponse.Headers.Add("Content-Type", "application/pdf");
                        blobResponse.Headers.Add("Content-Disposition",
                            $"inline; filename=\"{declaration.DeclarationId}.pdf\"");
                        await blobResponse.Body.WriteAsync(storedPdf);
                        return blobResponse;
                    }
                }

                // Otherwise generate on-the-fly (preview / draft)
                var company = await _settingsRepo.GetDefaultAsync();
                var (pdfBytes, _) = await _pdfService.GeneratePdfAsync(declaration, company);

                var response = req.CreateResponse(HttpStatusCode.OK);
                response.Headers.Add("Content-Type", "application/pdf");
                response.Headers.Add("Content-Disposition",
                    $"inline; filename=\"{declaration.DeclarationId ?? "declaration"}.pdf\"");
                await response.Body.WriteAsync(pdfBytes);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error downloading declaration PDF for expense {ExpenseId}", expenseId);
                var error = req.CreateResponse(HttpStatusCode.InternalServerError);
                await error.WriteAsJsonAsync(new { error = ex.Message });
                return error;
            }
        }

        // ── GET /expenses/{expenseId}/audit ───────────────────────────────

        [Function("GetExpenseAuditEvents")]
        public async Task<HttpResponseData> GetAuditEvents(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get",
                Route = "expenses/{expenseId}/audit")] HttpRequestData req,
            int expenseId)
        {
            try
            {
                var events = await _auditRepo.GetByExpenseIdAsync(expenseId);
                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(events);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting audit events for expense {ExpenseId}", expenseId);
                var error = req.CreateResponse(HttpStatusCode.InternalServerError);
                await error.WriteAsJsonAsync(new { error = ex.Message });
                return error;
            }
        }
    }

    // ── DTOs ────────────────────────────────────────────────────────────────

    public class MissingReceiptDeclarationDto
    {
        public DeclarationType DeclarationType { get; set; } = DeclarationType.MissingReceiptDeclaration;
        public string? DeclarerName { get; set; }
        public string? DeclarerRole { get; set; }
        public string? DeclarerEmail { get; set; }
        public decimal? AmountGross { get; set; }
        public string? Currency { get; set; }
        public DateTime? ExpenseDate { get; set; }
        public string? MerchantOrPayee { get; set; }
        public string? BankTransactionRef { get; set; }
        public string? ExpenseCategory { get; set; }
        public string? Description { get; set; }
        public ReceiptMissingReason ReasonReceiptMissing { get; set; } = ReceiptMissingReason.NotProvided;
        public string? OtherReasonText { get; set; }
        public bool AcknowledgementDisallowable { get; set; }
        public DeclarationSignatureType SignatureType { get; set; } = DeclarationSignatureType.TypedName;
        public string? TypedSignature { get; set; }
    }

    public class VoidDeclarationDto
    {
        public string? Reason { get; set; }
    }
}
