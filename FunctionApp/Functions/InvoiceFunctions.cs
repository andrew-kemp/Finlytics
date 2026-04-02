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
    public class InvoiceFunctions
    {
        private readonly ILogger _logger;
        private readonly InvoicePdfService _pdfService;
        private readonly IInvoiceRepository _invoiceRepository;
        private readonly ICustomerRepository _customerRepository;
        private readonly ICompanySettingsRepository _companySettingsRepository;
        private readonly BlobStorageService _blobStorageService;
        private readonly EmailService _emailService;
        private readonly DeletionGuardService _guard;

        public InvoiceFunctions(
            ILoggerFactory loggerFactory,
            IInvoiceRepository invoiceRepository,
            ICustomerRepository customerRepository,
            ICompanySettingsRepository companySettingsRepository,
            BlobStorageService blobStorageService,
            EmailService emailService,
            DeletionGuardService guard)
        {
            _logger = loggerFactory.CreateLogger<InvoiceFunctions>();
            _pdfService = new InvoicePdfService(blobStorageService);
            _invoiceRepository = invoiceRepository;
            _customerRepository = customerRepository;
            _companySettingsRepository = companySettingsRepository;
            _blobStorageService = blobStorageService;
            _emailService = emailService;
            _guard = guard;
        }

        [Function("GetInvoices")]
        public async Task<HttpResponseData> GetInvoices(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "invoices")] HttpRequestData req)
        {
            _logger.LogInformation("GetInvoices function triggered");

            try
            {
                var invoices = await _invoiceRepository.GetAllAsync();
                
                // Populate CustomerName from Customers table for all invoices with CustomerId
                var allCustomers = await _customerRepository.GetAllAsync();
                foreach (var invoice in invoices)
                {
                    _logger.LogInformation($"Invoice {invoice.Id}: CustomerId='{invoice.CustomerId}', CustomerName='{invoice.CustomerName}'");
                    
                    if (!string.IsNullOrWhiteSpace(invoice.CustomerId))
                    {
                        // Always populate/update CustomerName from Customers table if we have a CustomerId
                        var customer = allCustomers.FirstOrDefault(c => c.Id == invoice.CustomerId);
                        if (customer != null && string.IsNullOrWhiteSpace(invoice.CustomerName))
                        {
                            invoice.CustomerName = customer.Name ?? customer.CustomerName;
                            _logger.LogInformation($"Populated CustomerName for invoice {invoice.Id}: {invoice.CustomerName}");
                        }
                    }
                    else
                    {
                        _logger.LogWarning($"Invoice {invoice.Id} has NULL or empty CustomerId - cannot populate CustomerName");
                    }
                }
                
                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(invoices);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting invoices");
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteStringAsync($"Error: {ex.Message}");
                return errorResponse;
            }
        }

        [Function("GetInvoice")]
        public async Task<HttpResponseData> GetInvoice(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "invoices/{id:int}")] HttpRequestData req,
            int id)
        {
            _logger.LogInformation($"GetInvoice function triggered for ID: {id}");

            try
            {
                var invoice = await _invoiceRepository.GetByIdAsync(id);
                
                if (invoice == null)
                {
                    var notFoundResponse = req.CreateResponse(HttpStatusCode.NotFound);
                    await notFoundResponse.WriteStringAsync("Invoice not found");
                    return notFoundResponse;
                }

                // Ensure CustomerName is populated from CustomerId if missing
                if (!string.IsNullOrWhiteSpace(invoice.CustomerId) && string.IsNullOrWhiteSpace(invoice.CustomerName))
                {
                    var customer = await _customerRepository.GetByIdAsync(invoice.CustomerId);
                    if (customer != null)
                    {
                        invoice.CustomerName = customer.CustomerName;
                        _logger.LogInformation($"Populated CustomerName for invoice {id}: {invoice.CustomerName}");
                    }
                }

                _logger.LogInformation($"Returning invoice {id} - CustomerName: {invoice.CustomerName}, LineItems: {invoice.LineItems?.Count ?? 0}");

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(invoice);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting invoice {id}");
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteStringAsync($"Error: {ex.Message}");
                return errorResponse;
            }
        }

        [Function("CreateInvoice")]
        public async Task<HttpResponseData> CreateInvoice(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "invoices")] HttpRequestData req)
        {
            _logger.LogInformation("CreateInvoice function triggered");

            try
            {
                var body = await new StreamReader(req.Body).ReadToEndAsync();
                _logger.LogInformation($"Received invoice data: {body}");
                
                var invoice = JsonSerializer.Deserialize<Invoice>(body, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (invoice == null)
                {
                    _logger.LogError("Invoice deserialization returned null");
                    var badRequestResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badRequestResponse.WriteStringAsync("Invalid invoice data");
                    return badRequestResponse;
                }

                _logger.LogInformation($"Deserialized invoice - Number: {invoice.InvoiceNumber}, CustomerId: {invoice.CustomerId}, CustomerName: '{invoice.CustomerName}', LineItems count: {invoice.LineItems?.Count ?? 0}");
                _logger.LogInformation($"DueDate value received: {(invoice.DueDate.HasValue ? invoice.DueDate.Value.ToString("yyyy-MM-dd") : "NULL")}");
                
                // CRITICAL: Frontend sends customerId as NaN when trying to parse GUID, and customerName as empty
                // We need to parse the raw JSON to extract the customer info that frontend is actually sending
                _logger.LogInformation("Parsing raw JSON to extract customer information...");
                
                // Get all customers for lookup
                var allCustomers = await _customerRepository.GetAllAsync();
                _logger.LogInformation($"Retrieved {allCustomers?.Count() ?? 0} customers from database");
                
                if (allCustomers != null && allCustomers.Any())
                {
                    _logger.LogInformation($"Sample customer: ID={allCustomers.First().Id}, Code={allCustomers.First().Code}, Name={allCustomers.First().Name}");
                }
                
                // Try to parse the customer from the request body JSON
                Customer selectedCustomer = null;
                try
                {
                    using var doc = JsonDocument.Parse(body);
                    var root = doc.RootElement;
                    
                    // Log all properties we receive
                    foreach (var prop in root.EnumerateObject())
                    {
                        _logger.LogInformation($"Request property: {prop.Name} = {prop.Value}");
                    }
                    
                    // Frontend might send customer object or customerCode string
                    if (root.TryGetProperty("customer", out var customerElement))
                    {
                        _logger.LogInformation($"Found 'customer' property in request");
                        string customerId = customerElement.TryGetProperty("id", out var idProp) ? idProp.GetString() : null;
                        string customerCode = customerElement.TryGetProperty("code", out var codeProp) ? codeProp.GetString() : 
                                            customerElement.TryGetProperty("customerCode", out var custCodeProp) ? custCodeProp.GetString() : null;
                        
                        _logger.LogInformation($"Extracted from customer object - ID: '{customerId}', Code: '{customerCode}'");
                        
                        // Try to find customer by ID (GUID) or Code
                        if (!string.IsNullOrEmpty(customerId))
                        {
                            selectedCustomer = allCustomers.FirstOrDefault(c => c.Id == customerId);
                            _logger.LogInformation($"Lookup by ID '{customerId}': {(selectedCustomer != null ? "FOUND" : "NOT FOUND")}");
                        }
                        
                        if (selectedCustomer == null && !string.IsNullOrEmpty(customerCode))
                        {
                            selectedCustomer = allCustomers.FirstOrDefault(c => c.Code == customerCode || c.CustomerCode == customerCode);
                            _logger.LogInformation($"Lookup by Code '{customerCode}': {(selectedCustomer != null ? "FOUND" : "NOT FOUND")}");
                        }
                    }
                    else if (root.TryGetProperty("customerCode", out var custCodeElem))
                    {
                        string customerCode = custCodeElem.GetString();
                        _logger.LogInformation($"Found 'customerCode' property: '{customerCode}'");
                        selectedCustomer = allCustomers.FirstOrDefault(c => c.Code == customerCode || c.CustomerCode == customerCode);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error parsing customer from JSON: {ex.Message}");
                }
                
                // If we found a customer through JSON parsing, use it
                if (selectedCustomer != null)
                {
                    _logger.LogInformation($"Selected customer from JSON: ID={selectedCustomer.Id}, Code={selectedCustomer.Code}, Name={selectedCustomer.Name}");
                    
                    // Store the customer ID as a string to align with Customer.Id (GUID)
                    invoice.CustomerId = selectedCustomer.Id;
                    _logger.LogInformation($"Set CustomerId to: '{invoice.CustomerId}'");
                    
                    invoice.CustomerName = !string.IsNullOrWhiteSpace(selectedCustomer.Name) ? selectedCustomer.Name : selectedCustomer.CustomerName;
                    _logger.LogInformation($"Set CustomerName to: '{invoice.CustomerName}'");
                }
                // Fallback: Try parsing CustomerName field
                else if (!string.IsNullOrWhiteSpace(invoice.CustomerName))
                {
                    _logger.LogInformation($"Attempting to resolve from CustomerName field: '{invoice.CustomerName}'");
                    
                    var customerCode = invoice.CustomerName.Contains(" - ") 
                        ? invoice.CustomerName.Split(" - ")[0].Trim() 
                        : invoice.CustomerName.Trim();
                    
                    selectedCustomer = allCustomers?.FirstOrDefault(c => 
                        (!string.IsNullOrWhiteSpace(c.CustomerCode) && c.CustomerCode.Equals(customerCode, StringComparison.OrdinalIgnoreCase)) ||
                        (!string.IsNullOrWhiteSpace(c.Code) && c.Code.Equals(customerCode, StringComparison.OrdinalIgnoreCase)));
                    
                    if (selectedCustomer != null)
                    {
                        _logger.LogInformation($"Found customer by code: {selectedCustomer.Name}");
                        invoice.CustomerId = selectedCustomer.Id;
                        invoice.CustomerName = selectedCustomer.Name;
                    }
                }
                
                if (selectedCustomer == null)
                {
                    _logger.LogError("CRITICAL: Could not identify customer from request!");
                    _logger.LogError($"Available customers: {string.Join(", ", allCustomers.Select(c => $"{c.Code}={c.Name}"))}");
                }
                
                // Auto-calculate due date if not provided
                if (!invoice.DueDate.HasValue)
                {
                    var company = await _companySettingsRepository.GetDefaultAsync();
                    
                    _logger.LogInformation($"Company InvoiceTermsDays: {company?.InvoiceTermsDays ?? "NULL"}");
                    
                    // Parse payment terms days (default to 30 if not set)
                    int paymentDays = 30;
                    if (company != null && !string.IsNullOrEmpty(company.InvoiceTermsDays) && int.TryParse(company.InvoiceTermsDays, out int parsedDays))
                    {
                        paymentDays = parsedDays;
                    }
                    
                    invoice.DueDate = invoice.DateIssued.AddDays(paymentDays);
                    _logger.LogInformation($"Auto-calculated due date: {invoice.DueDate.Value:yyyy-MM-dd} (invoice date + {paymentDays} days)");
                }
                
                // Ensure CustomerName is populated from Customers table if missing
                if (!string.IsNullOrWhiteSpace(invoice.CustomerId) && string.IsNullOrWhiteSpace(invoice.CustomerName))
                {
                    var customer = allCustomers?.FirstOrDefault(c => c.Id == invoice.CustomerId);
                    if (customer != null)
                    {
                        invoice.CustomerName = customer.Name ?? customer.CustomerName;
                        _logger.LogInformation($"Populated CustomerName from database: {invoice.CustomerName}");
                    }
                    else
                    {
                        _logger.LogWarning($"Could not find customer with ID {invoice.CustomerId} to populate CustomerName");
                    }
                }
                
                var createdInvoice = await _invoiceRepository.CreateAsync(invoice);
                _logger.LogInformation($"Invoice created successfully with ID: {createdInvoice.Id}");

                // Generate and save PDF to blob storage
                try
                {
                    var company = await _companySettingsRepository.GetDefaultAsync();
                    var customer = allCustomers?.FirstOrDefault(c => c.Id == createdInvoice.CustomerId);
                    var pdfBytes = await _pdfService.GenerateInvoicePdfAsync(createdInvoice, company, customer);
                    var pdfUrl = await _blobStorageService.UploadInvoicePdfAsync(
                        createdInvoice.InvoiceNumber, 
                        pdfBytes, 
                        customer?.CustomerCode,
                        customer?.Name,
                        createdInvoice.DateIssued);
                    
                    // Update invoice with PDF URL
                    createdInvoice.PdfUrl = pdfUrl;
                    await _invoiceRepository.UpdateAsync(createdInvoice);
                    
                    _logger.LogInformation($"Invoice PDF saved to blob storage: {pdfUrl}");
                }
                catch (Exception pdfEx)
                {
                    _logger.LogError($"Error generating/saving PDF for invoice {createdInvoice.Id}: {pdfEx.Message}");
                    // Continue - invoice is created, just PDF failed
                }

                var response = req.CreateResponse(HttpStatusCode.Created);
                await response.WriteAsJsonAsync(createdInvoice);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error creating invoice: {ex.Message}");
                _logger.LogError($"Stack trace: {ex.StackTrace}");
                if (ex.InnerException != null)
                {
                    _logger.LogError($"Inner exception: {ex.InnerException.Message}");
                }
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteStringAsync($"Error: {ex.Message}");
                return errorResponse;
            }
        }

        [Function("QuickInvoice")]
        public async Task<HttpResponseData> QuickInvoice(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "invoices/quick")] HttpRequestData req)
        {
            _logger.LogInformation("QuickInvoice function triggered");

            try
            {
                var body = await new StreamReader(req.Body).ReadToEndAsync();
                var payload = JsonSerializer.Deserialize<JsonElement>(body);

                var customerId = payload.GetProperty("customerId").GetString();
                var days = payload.GetProperty("days").GetDecimal();
                var description = payload.TryGetProperty("description", out var descEl) ? descEl.GetString() : null;
                var sendEmail = payload.TryGetProperty("sendEmail", out var sendEl) && sendEl.GetBoolean();
                var rateOverride = payload.TryGetProperty("rate", out var rateEl) ? rateEl.GetDecimal() : (decimal?)null;
                var vatRateOverride = payload.TryGetProperty("vatRate", out var vatEl) ? vatEl.GetInt32() : (int?)null;

                // Lookup customer
                var customer = await _customerRepository.GetByIdAsync(customerId);
                if (customer == null)
                {
                    var notFound = req.CreateResponse(HttpStatusCode.BadRequest);
                    await notFound.WriteStringAsync("Customer not found");
                    return notFound;
                }

                // Determine rate
                decimal dayRate = rateOverride ?? 0;
                if (dayRate == 0 && !string.IsNullOrWhiteSpace(customer.DefaultDayRate))
                    decimal.TryParse(customer.DefaultDayRate, out dayRate);
                if (dayRate == 0)
                {
                    var badRate = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badRate.WriteStringAsync("No day rate specified and customer has no default day rate configured");
                    return badRate;
                }

                int vatRate = vatRateOverride ?? customer.DefaultVATRate ?? 20;
                decimal amountNet = days * dayRate;
                decimal vatAmount = Math.Round(amountNet * vatRate / 100m, 2);
                decimal amountGross = amountNet + vatAmount;

                // Get company settings for terms
                var company = await _companySettingsRepository.GetDefaultAsync();
                int paymentDays = 30;
                if (company != null && !string.IsNullOrEmpty(company.InvoiceTermsDays) && int.TryParse(company.InvoiceTermsDays, out int parsed))
                    paymentDays = parsed;

                var now = DateTime.UtcNow;
                var lineDescription = description ?? $"Consultancy services — {days} day{(days != 1 ? "s" : "")}";

                // Generate next invoice number (same logic as GetNextInvoiceNumber)
                var allInvoices = await _invoiceRepository.GetAllAsync();
                var currentPeriod = now.ToString("yyyyMM");
                var invoicesThisPeriod = allInvoices
                    .Where(i => i.InvoiceNumber != null && i.InvoiceNumber.StartsWith($"INV-{currentPeriod}"))
                    .ToList();
                int nextNum = 1;
                if (invoicesThisPeriod.Any())
                {
                    var lastNumberPart = invoicesThisPeriod.OrderByDescending(i => i.InvoiceNumber).First().InvoiceNumber?.Split('-').LastOrDefault();
                    if (int.TryParse(lastNumberPart, out int lastN)) nextNum = lastN + 1;
                }
                var invoiceNumber = $"INV-{currentPeriod}-{nextNum:D3}";

                var invoice = new Invoice
                {
                    InvoiceNumber = invoiceNumber,
                    CustomerId = customer.Id,
                    CustomerName = customer.Name ?? customer.CustomerName,
                    BillingEmail = customer.BillingEmail ?? customer.Email,
                    POReference = string.Empty,
                    DateIssued = now,
                    DueDate = now.AddDays(paymentDays),
                    Status = "Issued",
                    AmountNet = amountNet,
                    VATAmount = vatAmount,
                    AmountGross = amountGross,
                    VatNumber = company?.VATNumber,
                    LineItems = new List<InvoiceLine>
                    {
                        new InvoiceLine
                        {
                            LineNumber = 1,
                            Description = lineDescription,
                            RateType = "Day Rate",
                            Quantity = days,
                            Rate = dayRate,
                            VATRate = vatRate,
                            LineTotal = amountNet
                        }
                    }
                };

                // Create invoice
                var created = await _invoiceRepository.CreateAsync(invoice);
                _logger.LogInformation($"Quick invoice created: {created.InvoiceNumber} for {customer.Name}");

                // Generate & upload PDF
                string pdfUrl = null;
                byte[] pdfBytes = null;
                try
                {
                    pdfBytes = await _pdfService.GenerateInvoicePdfAsync(created, company, customer);
                    pdfUrl = await _blobStorageService.UploadInvoicePdfAsync(
                        created.InvoiceNumber, pdfBytes,
                        customer.CustomerCode ?? customer.Code,
                        customer.Name, created.DateIssued);
                    created.PdfUrl = pdfUrl;
                    await _invoiceRepository.UpdateAsync(created);
                }
                catch (Exception pdfEx)
                {
                    _logger.LogError($"Quick invoice PDF error: {pdfEx.Message}");
                }

                // Send email if requested
                bool emailSent = false;
                string emailError = null;
                if (sendEmail && pdfBytes != null)
                {
                    var toEmail = customer.BillingEmail ?? customer.Email;
                    if (!string.IsNullOrWhiteSpace(toEmail))
                    {
                        try
                        {
                            var accessToken = AuthHelper.GetAccessToken(req);
                            var result = await _emailService.SendInvoiceEmailAsync(toEmail, created, pdfBytes, accessToken, ccEmail: customer.CcEmail);
                            emailSent = result.Success;
                            emailError = result.Error;
                        }
                        catch (Exception emailEx)
                        {
                            emailError = emailEx.Message;
                            _logger.LogError($"Quick invoice email error: {emailEx.Message}");
                        }
                    }
                    else
                    {
                        emailError = "No email address on customer record";
                    }
                }

                var response = req.CreateResponse(HttpStatusCode.Created);
                await response.WriteAsJsonAsync(new
                {
                    invoice = created,
                    emailSent,
                    emailError,
                    pdfUrl
                });
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError($"QuickInvoice error: {ex.Message}\n{ex.StackTrace}");
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteStringAsync($"Error: {ex.Message}");
                return errorResponse;
            }
        }

        [Function("UpdateInvoice")]
        public async Task<HttpResponseData> UpdateInvoice(
            [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "invoices/{id}")] HttpRequestData req,
            string id)
        {
            _logger.LogInformation($"UpdateInvoice function triggered for ID: {id}");

            try
            {
                var body = await new StreamReader(req.Body).ReadToEndAsync();
                var invoice = JsonSerializer.Deserialize<Invoice>(body, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (invoice == null)
                {
                    var badRequestResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badRequestResponse.WriteStringAsync("Invalid invoice data");
                    return badRequestResponse;
                }

                invoice.Id = int.Parse(id);

                // Get all customers to populate CustomerName from CustomerId
                var allCustomers = await _customerRepository.GetAllAsync();

                // Get existing invoice to preserve CustomerName and LineItems if not provided
                var existingInvoice = await _invoiceRepository.GetByIdAsync(invoice.Id);
                if (existingInvoice != null)
                {
                    // Preserve CustomerName if not in payload (empty or whitespace)
                    if (string.IsNullOrWhiteSpace(invoice.CustomerName))
                    {
                        invoice.CustomerName = existingInvoice.CustomerName;
                    }

                    // Preserve LineItems if not in payload or empty
                    if (invoice.LineItems == null || invoice.LineItems.Count == 0)
                    {
                        invoice.LineItems = existingInvoice.LineItems;
                    }
                }

                // Populate CustomerName from CustomerId if we have a CustomerId but no CustomerName
                if (!string.IsNullOrWhiteSpace(invoice.CustomerId) && string.IsNullOrWhiteSpace(invoice.CustomerName))
                {
                    var customer = allCustomers?.FirstOrDefault(c => c.Id == invoice.CustomerId);
                    if (customer != null)
                    {
                        invoice.CustomerName = customer.Name ?? customer.CustomerName;
                        _logger.LogInformation($"Populated CustomerName from database during update: {invoice.CustomerName}");
                    }
                }

                _logger.LogInformation($"Updating invoice {id} - Status: {invoice.Status}, CustomerName: {invoice.CustomerName}, LineItems: {invoice.LineItems?.Count ?? 0}, DueDate: {(invoice.DueDate.HasValue ? invoice.DueDate.Value.ToString("yyyy-MM-dd") : "NULL")}, DatePaid: {(invoice.DatePaid.HasValue ? invoice.DatePaid.Value.ToString("yyyy-MM-dd") : "NULL")}");
                
                if (invoice.LineItems != null && invoice.LineItems.Count > 0)
                {
                    _logger.LogInformation($"LineItems details: {System.Text.Json.JsonSerializer.Serialize(invoice.LineItems)}");
                }
                
                var updatedInvoice = await _invoiceRepository.UpdateAsync(invoice);
                
                _logger.LogInformation($"After update - Invoice {id} now has {updatedInvoice.LineItems?.Count ?? 0} line items");

                // Generate and save PDF to blob storage
                try
                {
                    var company = await _companySettingsRepository.GetDefaultAsync();
                    var customer = allCustomers?.FirstOrDefault(c => c.Id == updatedInvoice.CustomerId);
                    var pdfBytes = await _pdfService.GenerateInvoicePdfAsync(updatedInvoice, company, customer);
                    var pdfUrl = await _blobStorageService.UploadInvoicePdfAsync(
                        updatedInvoice.InvoiceNumber, 
                        pdfBytes, 
                        customer?.CustomerCode,
                        customer?.Name,
                        updatedInvoice.DateIssued);
                    
                    // Update invoice with PDF URL
                    updatedInvoice.PdfUrl = pdfUrl;
                    await _invoiceRepository.UpdateAsync(updatedInvoice);
                    
                    _logger.LogInformation($"Invoice PDF updated in blob storage: {pdfUrl}");
                }
                catch (Exception pdfEx)
                {
                    _logger.LogError($"Error generating/saving PDF for invoice {updatedInvoice.Id}: {pdfEx.Message}");
                    // Continue - invoice is updated, just PDF failed
                }

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(updatedInvoice);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating invoice {id}");
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteStringAsync($"Error: {ex.Message}");
                return errorResponse;
            }
        }

        [Function("DeleteInvoice")]
        public async Task<HttpResponseData> DeleteInvoice(
            [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "invoices/{id}")] HttpRequestData req,
            string id)
        {
            _logger.LogInformation($"DeleteInvoice function triggered for ID: {id}");

            var blocked = await _guard.GuardAsync(req, "invoice");
            if (blocked != null) return blocked;

            try
            {
                await _invoiceRepository.DeleteAsync(int.Parse(id));

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteStringAsync("Invoice deleted successfully");
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting invoice {id}");
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteStringAsync($"Error: {ex.Message}");
                return errorResponse;
            }
        }

        [Function("GenerateInvoicePdf")]
        public async Task<HttpResponseData> GenerateInvoicePdf(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "invoices/{id}/pdf")] HttpRequestData req,
            string id)
        {
            _logger.LogInformation($"GenerateInvoicePdf function triggered for ID: {id}");

            try
            {
                var invoice = await _invoiceRepository.GetByIdAsync(int.Parse(id));
                if (invoice == null)
                {
                    var notFoundResponse = req.CreateResponse(HttpStatusCode.NotFound);
                    await notFoundResponse.WriteStringAsync("Invoice not found");
                    return notFoundResponse;
                }

                var company = await _companySettingsRepository.GetDefaultAsync();
                var customer = !string.IsNullOrWhiteSpace(invoice.CustomerId)
                    ? await _customerRepository.GetByIdAsync(invoice.CustomerId)
                    : null;
                
                if (company == null || customer == null)
                {
                    var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                    await errorResponse.WriteStringAsync("Unable to fetch company settings or customer data");
                    return errorResponse;
                }
                
                var pdfBytes = await _pdfService.GenerateInvoicePdfAsync(invoice, company, customer);

                var response = req.CreateResponse(HttpStatusCode.OK);
                response.Headers.Add("Content-Type", "application/pdf");
                response.Headers.Add("Content-Disposition", $"attachment; filename={invoice.InvoiceNumber}.pdf");
                await response.Body.WriteAsync(pdfBytes, 0, pdfBytes.Length);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error generating PDF for invoice {id}");
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteStringAsync($"Error: {ex.Message}");
                return errorResponse;
            }
        }

        [Function("GetNextInvoiceNumber")]
        public async Task<HttpResponseData> GetNextInvoiceNumber(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "invoices/next-number")] HttpRequestData req)
        {
            _logger.LogInformation("GetNextInvoiceNumber function triggered");

            try
            {
                // Get all invoices and generate next number
                var invoices = await _invoiceRepository.GetAllAsync();
                var currentPeriod = DateTime.Now.ToString("yyyyMM");
                
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
                
                var nextInvoiceNumber = $"INV-{currentPeriod}-{nextNumber:D3}";
                
                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(new { invoiceNumber = nextInvoiceNumber });
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting next invoice number");
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteStringAsync($"Error: {ex.Message}");
                return errorResponse;
            }
        }

        [Function("SendInvoiceEmail")]
        public async Task<HttpResponseData> SendInvoiceEmail(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "invoices/{id}/email")] HttpRequestData req,
            string id)
        {
            _logger.LogInformation($"SendInvoiceEmail function triggered for invoice ID: {id}");

            try
            {
                var accessToken = AuthHelper.GetAccessToken(req);
                var invoice = await _invoiceRepository.GetByIdAsync(int.Parse(id));
                if (invoice == null)
                {
                    var notFoundResponse = req.CreateResponse(HttpStatusCode.NotFound);
                    await notFoundResponse.WriteStringAsync("Invoice not found");
                    return notFoundResponse;
                }

                var company = await _companySettingsRepository.GetDefaultAsync();
                var customer = !string.IsNullOrWhiteSpace(invoice.CustomerId)
                    ? await _customerRepository.GetByIdAsync(invoice.CustomerId)
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
                    : (invoice.BillingEmail ?? customer?.BillingEmail ?? customer?.Email ?? company.InvoicesEmail ?? company.CompanyEmail);

                if (string.IsNullOrWhiteSpace(toEmail))
                {
                    var errorResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                    await errorResponse.WriteStringAsync("No recipient email available for invoice");
                    return errorResponse;
                }
                
                if (customer == null)
                {
                    customer = new Customer
                    {
                        CustomerName = invoice.CustomerName ?? "Customer",
                        BillingAddress = string.Empty,
                        Email = invoice.BillingEmail ?? string.Empty
                    };
                }

                // Generate PDF
                var pdfBytes = await _pdfService.GenerateInvoicePdfAsync(invoice, company, customer);

                var emailResult = await _emailService.SendInvoiceEmailAsync(toEmail, invoice, pdfBytes, accessToken, ccEmail: customer?.CcEmail);
                if (!emailResult.Success)
                {
                    var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                    await errorResponse.WriteStringAsync(emailResult.Error ?? "Failed to send invoice email");
                    return errorResponse;
                }

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(new { message = "Invoice email sent successfully", toEmail });
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error sending invoice email for invoice {id}: {ex.Message}");
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteStringAsync($"Error: {ex.Message}");
                return errorResponse;
            }
        }

        [Function("SendPaymentReceivedEmail")]
        public async Task<HttpResponseData> SendPaymentReceivedEmail(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "invoices/{id}/payment-received-email")] HttpRequestData req,
            string id)
        {
            _logger.LogInformation($"SendPaymentReceivedEmail triggered for invoice ID: {id}");

            try
            {
                var accessToken = AuthHelper.GetAccessToken(req);
                var invoice = await _invoiceRepository.GetByIdAsync(int.Parse(id));
                if (invoice == null)
                {
                    var notFound = req.CreateResponse(HttpStatusCode.NotFound);
                    await notFound.WriteStringAsync("Invoice not found");
                    return notFound;
                }

                var company = await _companySettingsRepository.GetDefaultAsync();
                if (company == null)
                {
                    var err = req.CreateResponse(HttpStatusCode.InternalServerError);
                    await err.WriteStringAsync("Company settings not found");
                    return err;
                }

                var customer = !string.IsNullOrWhiteSpace(invoice.CustomerId)
                    ? await _customerRepository.GetByIdAsync(invoice.CustomerId)
                    : null;

                // Resolve recipient: body override → invoice billing email → customer email → invoices mailbox
                string? requestedEmail = null;
                try
                {
                    var body = await new StreamReader(req.Body).ReadToEndAsync();
                    if (!string.IsNullOrWhiteSpace(body))
                    {
                        var payload = JsonSerializer.Deserialize<EmailRequest>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                        requestedEmail = payload?.Email;
                    }
                }
                catch { /* ignore */ }

                var toEmail = !string.IsNullOrWhiteSpace(requestedEmail)
                    ? requestedEmail
                    : (invoice.BillingEmail ?? customer?.BillingEmail ?? customer?.Email ?? company.InvoicesEmail ?? company.CompanyEmail);

                if (string.IsNullOrWhiteSpace(toEmail))
                {
                    var err = req.CreateResponse(HttpStatusCode.BadRequest);
                    await err.WriteStringAsync("No recipient email available for invoice");
                    return err;
                }

                var emailResult = await _emailService.SendPaymentReceivedEmailAsync(toEmail, invoice, accessToken, ccEmail: customer?.CcEmail);
                if (!emailResult.Success)
                {
                    var err = req.CreateResponse(HttpStatusCode.InternalServerError);
                    await err.WriteStringAsync(emailResult.Error ?? "Failed to send payment received email");
                    return err;
                }

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(new { message = "Payment confirmation email sent successfully", toEmail });
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error sending payment received email for invoice {id}: {ex.Message}");
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteStringAsync($"Error: {ex.Message}");
                return errorResponse;
            }
        }

        private class EmailRequest
        {
            public string? Email { get; set; }
        }
    }
}
