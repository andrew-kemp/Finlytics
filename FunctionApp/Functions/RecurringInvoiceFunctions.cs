using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using FinanceHubFunctions.Data;
using FinanceHubFunctions.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace FinanceHubFunctions.Functions
{
    public class RecurringInvoiceFunctions
    {
        private readonly ILogger _logger;
        private readonly IRecurringInvoiceTemplateRepository _templateRepository;
        private readonly IInvoiceRepository _invoiceRepository;
        private readonly ICompanySettingsRepository _companySettingsRepository;

        public RecurringInvoiceFunctions(
            ILoggerFactory loggerFactory,
            IRecurringInvoiceTemplateRepository templateRepository,
            IInvoiceRepository invoiceRepository,
            ICompanySettingsRepository companySettingsRepository)
        {
            _logger = loggerFactory.CreateLogger<RecurringInvoiceFunctions>();
            _templateRepository = templateRepository;
            _invoiceRepository = invoiceRepository;
            _companySettingsRepository = companySettingsRepository;
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  TIMER: Process recurring invoice templates daily at 07:00 UTC
        // ═══════════════════════════════════════════════════════════════════════

        [Function("ProcessRecurringInvoices")]
        public async Task RunAsync([TimerTrigger("0 0 7 * * *")] TimerInfo myTimer)
        {
            _logger.LogInformation($"RecurringInvoiceFunction timer fired at {DateTime.UtcNow:o}");

            var dueTemplates = await _templateRepository.GetDueTemplatesAsync();
            int created = 0;
            int failed = 0;

            foreach (var template in dueTemplates)
            {
                try
                {
                    var today = DateTime.UtcNow.Date;
                    var invoiceDate = template.NextRunDate?.Date ?? today;

                    // Generate next invoice number
                    var invoiceNumber = await GenerateNextInvoiceNumberAsync(invoiceDate);

                    // Calculate financial year (UK tax year April 6 → April 5)
                    var fy = CalculateFinancialYear(invoiceDate);

                    // Calculate due date from company settings
                    int paymentDays = 30;
                    var company = await _companySettingsRepository.GetDefaultAsync();
                    if (company != null && !string.IsNullOrEmpty(company.InvoiceTermsDays) && int.TryParse(company.InvoiceTermsDays, out int parsedDays))
                    {
                        paymentDays = parsedDays;
                    }

                    // Clone line items from template
                    var lineItems = template.DefaultLineItems?.Select(li => new InvoiceLine
                    {
                        LineNumber = li.LineNumber,
                        Description = li.Description,
                        RateType = li.RateType,
                        Quantity = li.Quantity,
                        Rate = li.Rate,
                        VATRate = li.VATRate,
                        LineTotal = li.LineTotal
                    }).ToList() ?? new List<InvoiceLine>();

                    // Calculate totals from line items
                    var amountNet = lineItems.Sum(li => li.LineTotal);
                    var vatAmount = lineItems.Sum(li => li.LineTotal * li.VATRate / 100m);

                    // Apply discount if set on template
                    var discountAmount = template.DiscountAmount ?? 0m;
                    if (template.DiscountPercent.HasValue && template.DiscountPercent > 0)
                    {
                        discountAmount = amountNet * template.DiscountPercent.Value / 100m;
                    }

                    var amountGross = amountNet - discountAmount + vatAmount;

                    // Create as Draft so user can review/adjust quantities before sending
                    var invoice = new Invoice
                    {
                        InvoiceNumber = invoiceNumber,
                        CustomerId = template.CustomerId,
                        CustomerName = template.CustomerName,
                        BillingEmail = template.BillingEmail,
                        POReference = template.POReference,
                        DateIssued = invoiceDate,
                        DueDate = invoiceDate.AddDays(paymentDays),
                        Status = "Draft",
                        AmountNet = amountNet,
                        DiscountPercent = template.DiscountPercent,
                        DiscountAmount = discountAmount > 0 ? discountAmount : null,
                        DiscountNote = template.DiscountNote,
                        VATAmount = vatAmount,
                        AmountGross = amountGross,
                        FinancialYear = fy,
                        VatNumber = template.VatNumber,
                        LineItems = lineItems
                    };

                    await _invoiceRepository.CreateAsync(invoice);
                    created++;

                    _logger.LogInformation($"Created draft invoice {invoiceNumber} for '{template.CustomerName}' (template ID {template.Id}) dated {invoiceDate:yyyy-MM-dd}.");

                    // Advance the template's next run date
                    template.NextRunDate = AdvanceNextDate(invoiceDate, template.Frequency);
                    template.ModifiedDate = DateTime.UtcNow;
                    await _templateRepository.UpdateAsync(template);

                    _logger.LogInformation($"Template ID {template.Id} next run date advanced to {template.NextRunDate:yyyy-MM-dd}.");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error processing recurring invoice template ID {template.Id}");
                    failed++;
                }
            }

            _logger.LogInformation($"RecurringInvoice run complete. Created: {created}, Failed: {failed}.");
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  GET /api/recurring-invoices — List all templates
        // ═══════════════════════════════════════════════════════════════════════

        [Function("GetRecurringInvoiceTemplates")]
        public async Task<HttpResponseData> GetTemplates(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "recurring-invoices")] HttpRequestData req)
        {
            try
            {
                var templates = await _templateRepository.GetAllAsync();
                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(templates);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting recurring invoice templates");
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteStringAsync($"Error: {ex.Message}");
                return errorResponse;
            }
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  GET /api/recurring-invoices/{id} — Get single template
        // ═══════════════════════════════════════════════════════════════════════

        [Function("GetRecurringInvoiceTemplate")]
        public async Task<HttpResponseData> GetTemplate(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "recurring-invoices/{id}")] HttpRequestData req,
            int id)
        {
            try
            {
                var template = await _templateRepository.GetByIdAsync(id);
                if (template == null)
                {
                    return req.CreateResponse(HttpStatusCode.NotFound);
                }
                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(template);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting recurring invoice template");
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteStringAsync($"Error: {ex.Message}");
                return errorResponse;
            }
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  POST /api/recurring-invoices — Create template
        // ═══════════════════════════════════════════════════════════════════════

        [Function("CreateRecurringInvoiceTemplate")]
        public async Task<HttpResponseData> CreateTemplate(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "recurring-invoices")] HttpRequestData req)
        {
            try
            {
                var body = await new StreamReader(req.Body).ReadToEndAsync();
                var template = JsonSerializer.Deserialize<RecurringInvoiceTemplate>(body, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (template == null)
                {
                    var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badResponse.WriteStringAsync("Invalid template data");
                    return badResponse;
                }

                var created = await _templateRepository.CreateAsync(template);
                var response = req.CreateResponse(HttpStatusCode.Created);
                await response.WriteAsJsonAsync(created);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating recurring invoice template");
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteStringAsync($"Error: {ex.Message}");
                return errorResponse;
            }
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  PUT /api/recurring-invoices/{id} — Update template
        // ═══════════════════════════════════════════════════════════════════════

        [Function("UpdateRecurringInvoiceTemplate")]
        public async Task<HttpResponseData> UpdateTemplate(
            [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "recurring-invoices/{id}")] HttpRequestData req,
            int id)
        {
            try
            {
                var existing = await _templateRepository.GetByIdAsync(id);
                if (existing == null)
                {
                    return req.CreateResponse(HttpStatusCode.NotFound);
                }

                var body = await new StreamReader(req.Body).ReadToEndAsync();
                var template = JsonSerializer.Deserialize<RecurringInvoiceTemplate>(body, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (template == null)
                {
                    var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badResponse.WriteStringAsync("Invalid template data");
                    return badResponse;
                }

                template.Id = id;
                var updated = await _templateRepository.UpdateAsync(template);
                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(updated);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating recurring invoice template");
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteStringAsync($"Error: {ex.Message}");
                return errorResponse;
            }
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  DELETE /api/recurring-invoices/{id} — Delete template
        // ═══════════════════════════════════════════════════════════════════════

        [Function("DeleteRecurringInvoiceTemplate")]
        public async Task<HttpResponseData> DeleteTemplate(
            [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "recurring-invoices/{id}")] HttpRequestData req,
            int id)
        {
            try
            {
                var existing = await _templateRepository.GetByIdAsync(id);
                if (existing == null)
                {
                    return req.CreateResponse(HttpStatusCode.NotFound);
                }

                await _templateRepository.DeleteAsync(id);
                return req.CreateResponse(HttpStatusCode.NoContent);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting recurring invoice template");
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteStringAsync($"Error: {ex.Message}");
                return errorResponse;
            }
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  Helpers
        // ═══════════════════════════════════════════════════════════════════════

        private async Task<string> GenerateNextInvoiceNumberAsync(DateTime date)
        {
            var currentPeriod = date.ToString("yyyyMM");
            var invoices = await _invoiceRepository.GetAllAsync();

            var invoicesThisPeriod = invoices
                .Where(i => i.InvoiceNumber != null && i.InvoiceNumber.StartsWith($"INV-{currentPeriod}"))
                .ToList();

            int nextNumber = 1;
            if (invoicesThisPeriod.Any())
            {
                var lastInvoice = invoicesThisPeriod
                    .OrderByDescending(i => i.InvoiceNumber)
                    .First();

                var lastNumberPart = lastInvoice.InvoiceNumber?.Split('-').LastOrDefault();
                if (int.TryParse(lastNumberPart, out int lastNum))
                {
                    nextNumber = lastNum + 1;
                }
            }

            return $"INV-{currentPeriod}-{nextNumber:D3}";
        }

        private static DateTime AdvanceNextDate(DateTime current, string frequency)
        {
            return frequency?.ToLowerInvariant() switch
            {
                "monthly" => current.AddMonths(1),
                "quarterly" => current.AddMonths(3),
                "annual" => current.AddYears(1),
                _ => current.AddMonths(1)
            };
        }

        private static string CalculateFinancialYear(DateTime date)
        {
            var startYear = date.Month < 4 || (date.Month == 4 && date.Day <= 5)
                ? date.Year - 1
                : date.Year;
            return $"{startYear}/{(startYear + 1) % 100:D2}";
        }
    }
}
