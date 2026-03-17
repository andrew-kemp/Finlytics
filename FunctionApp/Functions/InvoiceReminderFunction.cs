using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using FinanceHubFunctions.Data;
using FinanceHubFunctions.Models;
using FinanceHubFunctions.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace FinanceHubFunctions.Functions
{
    public class InvoiceReminderFunction
    {
        private readonly ILogger _logger;
        private readonly IInvoiceRepository _invoiceRepository;
        private readonly ICustomerRepository _customerRepository;
        private readonly ICompanySettingsRepository _companySettingsRepository;
        private readonly BlobStorageService _blobStorageService;
        private readonly EmailService _emailService;

        public InvoiceReminderFunction(
            ILoggerFactory loggerFactory,
            IInvoiceRepository invoiceRepository,
            ICustomerRepository customerRepository,
            ICompanySettingsRepository companySettingsRepository,
            BlobStorageService blobStorageService,
            EmailService emailService)
        {
            _logger = loggerFactory.CreateLogger<InvoiceReminderFunction>();
            _invoiceRepository = invoiceRepository;
            _customerRepository = customerRepository;
            _companySettingsRepository = companySettingsRepository;
            _blobStorageService = blobStorageService;
            _emailService = emailService;
        }

        /// <summary>
        /// Timer-triggered function: runs daily at 08:00 UTC.
        /// Finds all overdue invoices (status=Issued, DueDate in the past, not reminded in last 7 days)
        /// and sends a payment chaser email to each customer.
        /// </summary>
        [Function("SendOverdueInvoiceReminders")]
        public async Task RunTimerAsync(
            [TimerTrigger("0 0 8 * * *")] TimerInfo myTimer)
        {
            _logger.LogInformation($"InvoiceReminderFunction timer fired at {DateTime.UtcNow:o}");

            var overdueInvoices = (await _invoiceRepository.GetOverdueAsync()).ToList();
            _logger.LogInformation($"Found {overdueInvoices.Count} overdue invoice(s) to chase.");

            int sent = 0;
            int skipped = 0;

            foreach (var invoice in overdueInvoices)
            {
                try
                {
                    // Always load customer to resolve billing email and CC address
                    Customer timerCustomer = null;
                    if (!string.IsNullOrWhiteSpace(invoice.CustomerId))
                        timerCustomer = await _customerRepository.GetByIdAsync(invoice.CustomerId);

                    var toEmail = invoice.BillingEmail;
                    if (string.IsNullOrWhiteSpace(toEmail))
                        toEmail = timerCustomer?.Email ?? timerCustomer?.BillingEmail;

                    if (string.IsNullOrWhiteSpace(toEmail))
                    {
                        _logger.LogWarning($"Invoice {invoice.InvoiceNumber} (ID {invoice.Id}): no billing email — skipping reminder.");
                        skipped++;
                        continue;
                    }

                    // Try to load the original PDF from blob storage
                    byte[] pdfBytes = null;
                    if (!string.IsNullOrWhiteSpace(invoice.InvoiceNumber))
                    {
                        try
                        {
                            pdfBytes = await _blobStorageService.DownloadInvoicePdfAsync(invoice.InvoiceNumber);
                        }
                        catch (Exception pdfEx)
                        {
                            _logger.LogWarning($"Could not load PDF for invoice {invoice.InvoiceNumber}: {pdfEx.Message}");
                        }
                    }

                    var result = await _emailService.SendInvoiceReminderEmailAsync(toEmail, invoice, pdfBytes, null, ccEmail: timerCustomer?.CcEmail);

                    if (result.Success)
                    {
                        // Update reminder tracking fields
                        invoice.ReminderSentAt = DateTime.UtcNow;
                        invoice.ReminderCount = (invoice.ReminderCount) + 1;
                        // Advance status to Overdue so it shows in the UI
                        invoice.Status = "Overdue";
                        await _invoiceRepository.UpdateAsync(invoice);
                        _logger.LogInformation($"Reminder #{invoice.ReminderCount} sent for invoice {invoice.InvoiceNumber} to {toEmail}.");
                        sent++;
                    }
                    else
                    {
                        _logger.LogError($"Failed to send reminder for invoice {invoice.InvoiceNumber}: {result.Error}");
                        skipped++;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error processing reminder for invoice {invoice.InvoiceNumber}");
                    skipped++;
                }
            }

            _logger.LogInformation($"Invoice reminder run complete. Sent: {sent}, Skipped/failed: {skipped}.");
        }

        /// <summary>
        /// HTTP POST /api/invoices/{id}/send-reminder — sends a manual chaser for a specific invoice.
        /// </summary>
        [Function("SendInvoiceReminder")]
        public async Task<HttpResponseData> SendManualReminderAsync(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "invoices/{id:int}/send-reminder")] HttpRequestData req,
            int id)
        {
            _logger.LogInformation($"Manual invoice reminder requested for invoice ID {id}");

            try
            {
                var invoice = await _invoiceRepository.GetByIdAsync(id);
                if (invoice == null)
                {
                    var notFound = req.CreateResponse(HttpStatusCode.NotFound);
                    await notFound.WriteStringAsync("Invoice not found");
                    return notFound;
                }

                if (invoice.Status == "Paid" || invoice.Status == "Draft")
                {
                    var bad = req.CreateResponse(HttpStatusCode.BadRequest);
                    await bad.WriteStringAsync($"Cannot send reminder for invoice with status '{invoice.Status}'");
                    return bad;
                }

                // Always load customer to resolve billing email and CC address
                Customer customer = null;
                if (!string.IsNullOrWhiteSpace(invoice.CustomerId))
                    customer = await _customerRepository.GetByIdAsync(invoice.CustomerId);

                var toEmail = invoice.BillingEmail;
                if (string.IsNullOrWhiteSpace(toEmail))
                    toEmail = customer?.Email ?? customer?.BillingEmail;

                if (string.IsNullOrWhiteSpace(toEmail))
                {
                    var bad = req.CreateResponse(HttpStatusCode.BadRequest);
                    await bad.WriteStringAsync("No billing email address found for this invoice");
                    return bad;
                }

                // Try to load the PDF
                byte[] pdfBytes = null;
                if (!string.IsNullOrWhiteSpace(invoice.InvoiceNumber))
                {
                    try
                    {
                        pdfBytes = await _blobStorageService.DownloadInvoicePdfAsync(invoice.InvoiceNumber);
                    }
                    catch (Exception pdfEx)
                    {
                        _logger.LogWarning($"Could not load PDF for invoice {invoice.InvoiceNumber}: {pdfEx.Message}");
                    }
                }

                // Read optional access token from request body (for email SMTP lookup)
                string accessToken = null;
                try
                {
                    var body = await new StreamReader(req.Body).ReadToEndAsync();
                    if (!string.IsNullOrWhiteSpace(body))
                    {
                        using var doc = JsonDocument.Parse(body);
                        if (doc.RootElement.TryGetProperty("accessToken", out var tokenProp))
                            accessToken = tokenProp.GetString();
                    }
                }
                catch { /* ignore — accessToken stays null */ }

                var result = await _emailService.SendInvoiceReminderEmailAsync(toEmail, invoice, pdfBytes, accessToken, ccEmail: customer?.CcEmail);

                if (!result.Success)
                {
                    var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                    await errorResponse.WriteStringAsync($"Failed to send reminder: {result.Error}");
                    return errorResponse;
                }

                // Update reminder tracking
                invoice.ReminderSentAt = DateTime.UtcNow;
                invoice.ReminderCount = invoice.ReminderCount + 1;
                if (invoice.Status == "Issued" && invoice.DueDate.HasValue && invoice.DueDate.Value.Date < DateTime.UtcNow.Date)
                    invoice.Status = "Overdue";
                var updatedInvoice = await _invoiceRepository.UpdateAsync(invoice);

                _logger.LogInformation($"Manual reminder #{invoice.ReminderCount} sent for invoice {invoice.InvoiceNumber} to {toEmail}");

                var okResponse = req.CreateResponse(HttpStatusCode.OK);
                await okResponse.WriteAsJsonAsync(new
                {
                    success = true,
                    message = $"Reminder sent to {toEmail}",
                    reminderCount = updatedInvoice.ReminderCount,
                    reminderSentAt = updatedInvoice.ReminderSentAt
                });
                return okResponse;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error sending manual reminder for invoice {id}");
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteStringAsync($"Error: {ex.Message}");
                return errorResponse;
            }
        }
    }
}
