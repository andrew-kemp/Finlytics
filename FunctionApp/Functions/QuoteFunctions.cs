using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using FinanceHubFunctions.Models;
using FinanceHubFunctions.Services;
using FinanceHubFunctions.Data;
using FinanceHubFunctions.Helpers;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace FinanceHubFunctions.Functions
{
    public class QuoteFunctions
    {
        private readonly ILogger _logger;
        private readonly InvoicePdfService _pdfService;
        private readonly IQuoteRepository _quoteRepository;
        private readonly ICustomerRepository _customerRepository;
        private readonly ICompanySettingsRepository _companySettingsRepository;
        private readonly BlobStorageService _blobStorageService;
        private readonly EmailService _emailService;
        private readonly DeletionGuardService _guard;

        public QuoteFunctions(
            ILoggerFactory loggerFactory,
            IQuoteRepository quoteRepository,
            ICustomerRepository customerRepository,
            ICompanySettingsRepository companySettingsRepository,
            BlobStorageService blobStorageService,
            EmailService emailService,
            DeletionGuardService guard)
        {
            _logger = loggerFactory.CreateLogger<QuoteFunctions>();
            _pdfService = new InvoicePdfService(blobStorageService);
            _quoteRepository = quoteRepository;
            _customerRepository = customerRepository;
            _companySettingsRepository = companySettingsRepository;
            _blobStorageService = blobStorageService;
            _emailService = emailService;
            _guard = guard;
        }

        [Function("GetQuotes")]
        public async Task<HttpResponseData> GetQuotes(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "quotes")] HttpRequestData req)
        {
            _logger.LogInformation("GetQuotes function triggered");

            try
            {
                var quotes = await _quoteRepository.GetAllAsync();
                
                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(quotes);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting quotes");
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteStringAsync($"Error: {ex.Message}");
                return errorResponse;
            }
        }

        [Function("GetQuote")]
        public async Task<HttpResponseData> GetQuote(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "quotes/{id}")] HttpRequestData req,
            int id)
        {
            _logger.LogInformation($"GetQuote function triggered for ID: {id}");

            try
            {
                var quote = await _quoteRepository.GetByIdAsync(id);
                
                if (quote == null)
                {
                    var notFoundResponse = req.CreateResponse(HttpStatusCode.NotFound);
                    await notFoundResponse.WriteStringAsync($"Quote with ID {id} not found");
                    return notFoundResponse;
                }

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(quote);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting quote {id}");
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteStringAsync($"Error: {ex.Message}");
                return errorResponse;
            }
        }

        [Function("CreateQuote")]
        public async Task<HttpResponseData> CreateQuote(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "quotes")] HttpRequestData req)
        {
            _logger.LogInformation("CreateQuote function triggered");

            try
            {
                var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                _logger.LogInformation($"Request body: {requestBody}");
                
                var quote = JsonSerializer.Deserialize<Quote>(requestBody, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (quote == null)
                {
                    _logger.LogError("Failed to deserialize quote data");
                    var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badRequest.WriteStringAsync("Invalid quote data");
                    return badRequest;
                }

                _logger.LogInformation($"Creating quote {quote.QuoteNumber} for customer {quote.CustomerId}");

                var createdQuote = await _quoteRepository.CreateAsync(quote);

                // Generate and save PDF to blob storage
                try
                {
                    var company = await _companySettingsRepository.GetDefaultAsync();
                    Customer customer = null;
                    if (!string.IsNullOrWhiteSpace(createdQuote.CustomerId))
                    {
                        customer = await _customerRepository.GetByIdAsync(createdQuote.CustomerId);
                    }
                    var pdfBytes = await _pdfService.GenerateQuotePdfAsync(createdQuote, company, customer, company?.QuotesEmail ?? company?.SmtpFromAddress ?? "");
                    var pdfUrl = await _blobStorageService.UploadQuotePdfAsync(
                        createdQuote.QuoteNumber, 
                        pdfBytes, 
                        customer?.CustomerCode,
                        customer?.Name,
                        createdQuote.DateIssued);
                    
                    // Update quote with PDF URL
                    createdQuote.PdfUrl = pdfUrl;
                    await _quoteRepository.UpdateAsync(createdQuote);
                    
                    _logger.LogInformation($"Quote PDF saved to blob storage: {pdfUrl}");
                }
                catch (Exception pdfEx)
                {
                    _logger.LogError($"Error generating/saving PDF for quote {createdQuote.Id}: {pdfEx.Message}");
                    // Continue - quote is created, just PDF failed
                }

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(createdQuote);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error creating quote: {ex.Message}");
                if (ex.InnerException != null)
                {
                    _logger.LogError($"Inner exception: {ex.InnerException.Message}");
                }
                _logger.LogError($"Stack trace: {ex.StackTrace}");
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteStringAsync($"Error: {ex.Message}\nInner: {ex.InnerException?.Message}");
                return errorResponse;
            }
        }

        [Function("UpdateQuote")]
        public async Task<HttpResponseData> UpdateQuote(
            [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "quotes/{id}")] HttpRequestData req,
            int id)
        {
            _logger.LogInformation($"UpdateQuote function triggered for ID: {id}");

            try
            {
                var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                var quote = JsonSerializer.Deserialize<Quote>(requestBody, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (quote == null)
                {
                    var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badRequest.WriteStringAsync("Invalid quote data");
                    return badRequest;
                }

                quote.Id = id;

                var updatedQuote = await _quoteRepository.UpdateAsync(quote);

                // Regenerate and save PDF to blob storage
                try
                {
                    var company = await _companySettingsRepository.GetDefaultAsync();
                    Customer customer = null;
                    if (!string.IsNullOrWhiteSpace(updatedQuote.CustomerId))
                    {
                        customer = await _customerRepository.GetByIdAsync(updatedQuote.CustomerId);
                    }
                    var pdfBytes = await _pdfService.GenerateQuotePdfAsync(updatedQuote, company, customer, company?.QuotesEmail ?? company?.SmtpFromAddress ?? "");
                    var pdfUrl = await _blobStorageService.UploadQuotePdfAsync(
                        updatedQuote.QuoteNumber, 
                        pdfBytes, 
                        customer?.CustomerCode,
                        customer?.Name,
                        updatedQuote.DateIssued);
                    
                    // Update quote with PDF URL
                    updatedQuote.PdfUrl = pdfUrl;
                    await _quoteRepository.UpdateAsync(updatedQuote);
                    
                    _logger.LogInformation($"Quote PDF updated in blob storage: {pdfUrl}");
                }
                catch (Exception pdfEx)
                {
                    _logger.LogError($"Error generating/saving PDF for quote {updatedQuote.Id}: {pdfEx.Message}");
                    // Continue - quote is updated, just PDF failed
                }

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(new { success = true, message = "Quote updated successfully" });
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating quote {id}");
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteStringAsync($"Error: {ex.Message}");
                return errorResponse;
            }
        }

        [Function("DeleteQuote")]
        public async Task<HttpResponseData> DeleteQuote(
            [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "quotes/{id}")] HttpRequestData req,
            int id)
        {
            _logger.LogInformation($"DeleteQuote function triggered for ID: {id}");

            var blocked = await _guard.GuardAsync(req, "quote");
            if (blocked != null) return blocked;

            try
            {
                await _quoteRepository.DeleteAsync(id);
                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(new { success = true, message = "Quote deleted successfully" });
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting quote {id}");
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteStringAsync($"Error: {ex.Message}");
                return errorResponse;
            }
        }

        [Function("GenerateQuotePdf")]
        public async Task<HttpResponseData> GenerateQuotePdf(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "quotes/{id}/pdf")] HttpRequestData req,
            int id)
        {
            _logger.LogInformation($"GenerateQuotePdf function triggered for ID: {id}");

            try
            {
                var quote = await _quoteRepository.GetByIdAsync(id);
                if (quote == null)
                {
                    var notFoundResponse = req.CreateResponse(HttpStatusCode.NotFound);
                    await notFoundResponse.WriteStringAsync($"Quote with ID {id} not found");
                    return notFoundResponse;
                }

                var settings = await _companySettingsRepository.GetDefaultAsync();
                Customer customer = null;
                if (!string.IsNullOrWhiteSpace(quote.CustomerId))
                {
                    customer = await _customerRepository.GetByIdAsync(quote.CustomerId);
                }

                if (settings == null)
                {
                    var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badRequest.WriteStringAsync("Company settings not found");
                    return badRequest;
                }

                var pdfBytes = await _pdfService.GenerateQuotePdfAsync(quote, settings, customer, settings?.QuotesEmail ?? settings?.SmtpFromAddress ?? "");
                
                var response = req.CreateResponse(HttpStatusCode.OK);
                response.Headers.Add("Content-Type", "application/pdf");
                response.Headers.Add("Content-Disposition", $"attachment; filename=\"{quote.QuoteNumber}.pdf\"");
                await response.Body.WriteAsync(pdfBytes, 0, pdfBytes.Length);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error generating PDF for quote {id}");
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteStringAsync($"Error: {ex.Message}");
                return errorResponse;
            }
        }

        [Function("SendQuoteEmail")]
        public async Task<HttpResponseData> SendQuoteEmail(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "quotes/{id}/email")] HttpRequestData req,
            int id)
        {
            _logger.LogInformation($"SendQuoteEmail function triggered for quote ID: {id}");

            try
            {
                var accessToken = AuthHelper.GetAccessToken(req);
                var quote = await _quoteRepository.GetByIdAsync(id);
                if (quote == null)
                {
                    var notFoundResponse = req.CreateResponse(HttpStatusCode.NotFound);
                    await notFoundResponse.WriteStringAsync("Quote not found");
                    return notFoundResponse;
                }

                var company = await _companySettingsRepository.GetDefaultAsync();
                var customer = !string.IsNullOrWhiteSpace(quote.CustomerId)
                    ? await _customerRepository.GetByIdAsync(quote.CustomerId)
                    : null;

                if (company == null)
                {
                    var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                    await errorResponse.WriteStringAsync("Unable to fetch company settings");
                    return errorResponse;
                }

                string? requestedEmail = null;
                try
                {
                    var body = await new StreamReader(req.Body).ReadToEndAsync();
                    if (!string.IsNullOrWhiteSpace(body))
                    {
                        var payload = JsonSerializer.Deserialize<EmailRequest>(body, new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        });
                        requestedEmail = payload?.Email;
                    }
                }
                catch
                {
                    // Ignore body parsing errors
                }

                var toEmail = !string.IsNullOrWhiteSpace(requestedEmail)
                    ? requestedEmail
                    : (quote.BillingEmail ?? customer?.BillingEmail ?? customer?.Email ?? company.QuotesEmail ?? company.CompanyEmail);

                if (string.IsNullOrWhiteSpace(toEmail))
                {
                    var errorResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                    await errorResponse.WriteStringAsync("No recipient email available for quote");
                    return errorResponse;
                }

                // Generate PDF
                var pdfBytes = await _pdfService.GenerateQuotePdfAsync(quote, company, customer, company?.QuotesEmail ?? company?.SmtpFromAddress ?? "");

                var emailResult = await _emailService.SendQuoteEmailAsync(toEmail, quote, pdfBytes, accessToken, ccEmail: customer?.CcEmail);
                if (!emailResult.Success)
                {
                    var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                    await errorResponse.WriteStringAsync(emailResult.Error ?? "Failed to send quote email");
                    return errorResponse;
                }

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(new { message = "Quote email sent successfully", toEmail });
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error sending quote email for quote {id}: {ex.Message}");
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteStringAsync($"Error: {ex.Message}");
                return errorResponse;
            }
        }

        private class EmailRequest
        {
            public string? Email { get; set; }
        }

        [Function("GetNextQuoteNumber")]
        public async Task<HttpResponseData> GetNextQuoteNumber(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "quotes/next-number")] HttpRequestData req)
        {
            _logger.LogInformation("GetNextQuoteNumber function triggered");

            try
            {
                // Get all quotes and generate next number
                var quotes = await _quoteRepository.GetAllAsync();
                var currentPeriod = DateTime.Now.ToString("yyyyMM");
                
                var quotesThisPeriod = quotes
                    .Where(q => q.QuoteNumber != null && q.QuoteNumber.StartsWith($"QUO-{currentPeriod}"))
                    .ToList();
                
                int nextNumber = 1;
                if (quotesThisPeriod.Any())
                {
                    var lastQuote = quotesThisPeriod
                        .OrderByDescending(q => q.QuoteNumber)
                        .First();
                    
                    var lastNumberPart = lastQuote.QuoteNumber?.Split('-').LastOrDefault();
                    if (int.TryParse(lastNumberPart, out int lastNum))
                    {
                        nextNumber = lastNum + 1;
                    }
                }
                
                var nextQuoteNumber = $"QUO-{currentPeriod}-{nextNumber:D3}";
                
                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(new { quoteNumber = nextQuoteNumber });
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting next quote number");
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteStringAsync($"Error: {ex.Message}");
                return errorResponse;
            }
        }
    }
}
