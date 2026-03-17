using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using FinanceHubFunctions.Services;
using FinanceHubFunctions.Data;

namespace FinanceHubFunctions.Functions
{
    public class AdminFunctions
    {
        private readonly ILogger _logger;
        private readonly SharePointService? _sharePointService;
        private readonly KeyVaultService? _keyVaultService;
        private readonly EmailService? _emailService;
        private readonly ICustomerRepository? _customerRepository;
        private readonly ISupplierRepository? _supplierRepository;
        private readonly FinanceHubDbContext? _dbContext;

        public AdminFunctions(
            ILoggerFactory loggerFactory, 
            KeyVaultService? keyVaultService = null, 
            EmailService? emailService = null,
            SharePointService? sharePointService = null,
            ICustomerRepository? customerRepository = null,
            ISupplierRepository? supplierRepository = null,
            FinanceHubDbContext? dbContext = null)
        {
            _logger = loggerFactory.CreateLogger<AdminFunctions>();
            _sharePointService = sharePointService;
            _keyVaultService = keyVaultService;
            _emailService = emailService;
            _customerRepository = customerRepository;
            _supplierRepository = supplierRepository;
            _dbContext = dbContext;
        }

        [Function("TestEmail")]
        public async Task<HttpResponseData> TestEmail(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "admin/test-email")] HttpRequestData req)
        {
            try
            {
                _logger.LogInformation("TestEmail: Sending test email");

                // Get access token from header
                string accessToken = req.Headers.FirstOrDefault(h => h.Key.Equals("Authorization", StringComparison.OrdinalIgnoreCase)).Value.FirstOrDefault()?.Replace("Bearer ", "");
                if (string.IsNullOrEmpty(accessToken))
                {
                    var authResponse = req.CreateResponse(HttpStatusCode.Unauthorized);
                    await authResponse.WriteAsJsonAsync(new { error = "Authorization header missing" });
                    return authResponse;
                }

                // Read request body for email address
                string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                var data = JsonSerializer.Deserialize<TestEmailRequest>(requestBody, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (string.IsNullOrEmpty(data?.Email))
                {
                    var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badResponse.WriteAsJsonAsync(new { error = "Email address is required" });
                    return badResponse;
                }

                if (_emailService == null)
                {
                    var svcResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                    await svcResponse.WriteAsJsonAsync(new { error = "Email service not available" });
                    return svcResponse;
                }

                // Send test email using injected EmailService
                var emailResult = await _emailService.SendTestEmailAsync(data.Email, accessToken);

                if (emailResult.Success)
                {
                    var response = req.CreateResponse(HttpStatusCode.OK);
                    await response.WriteAsJsonAsync(new { 
                        success = true, 
                        message = $"Test email sent successfully to {data.Email}"
                    });
                    return response;
                }
                else
                {
                    var response = req.CreateResponse(HttpStatusCode.InternalServerError);
                    await response.WriteAsJsonAsync(new { 
                        success = false, 
                        error = emailResult.Error ?? "Failed to send test email. Check SMTP settings and Key Vault password."
                    });
                    return response;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending test email");
                var response = req.CreateResponse(HttpStatusCode.InternalServerError);
                await response.WriteAsJsonAsync(new { error = ex.Message });
                return response;
            }
        }

        [Function("EnableLedgerAttachments")]
        public async Task<HttpResponseData> EnableLedgerAttachments(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "admin/enable-attachments")] HttpRequestData req)
        {
            try
            {
                _logger.LogInformation("EnableLedgerAttachments: Starting to enable attachments on Ledger list");

                var result = await _sharePointService.EnableListAttachments("Ledger");
                
                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(new { 
                    success = true, 
                    message = "Attachments enabled on Ledger list",
                    enabled = result
                });
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error enabling attachments");
                var response = req.CreateResponse(HttpStatusCode.InternalServerError);
                await response.WriteAsJsonAsync(new { error = ex.Message });
                return response;
            }
        }

        [Function("DeleteAllCustomersAndSuppliers")]
        public async Task<HttpResponseData> DeleteAllCustomersAndSuppliers(
            [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "admin/delete-all")] HttpRequestData req)
        {
            try
            {
                _logger.LogInformation("DeleteAllCustomersAndSuppliers: Starting deletion");

                // Get access token from header
                string accessToken = req.Headers.FirstOrDefault(h => h.Key.Equals("Authorization", StringComparison.OrdinalIgnoreCase)).Value.FirstOrDefault()?.Replace("Bearer ", "");
                if (string.IsNullOrEmpty(accessToken))
                {
                    var authResponse = req.CreateResponse(HttpStatusCode.Unauthorized);
                    await authResponse.WriteAsJsonAsync(new { error = "Authorization header missing" });
                    return authResponse;
                }

                int customersDeleted = 0;
                int suppliersDeleted = 0;

                // Delete all customers
                if (_customerRepository != null)
                {
                    var customers = await _customerRepository.GetAllAsync();
                    foreach (var customer in customers)
                    {
                        await _customerRepository.DeleteAsync(customer.Id);
                        customersDeleted++;
                    }
                    _logger.LogInformation($"Deleted {customersDeleted} customers");
                }

                // Delete all suppliers
                if (_supplierRepository != null)
                {
                    var suppliers = await _supplierRepository.GetAllAsync();
                    foreach (var supplier in suppliers)
                    {
                        await _supplierRepository.DeleteAsync(supplier.Id);
                        suppliersDeleted++;
                    }
                    _logger.LogInformation($"Deleted {suppliersDeleted} suppliers");
                }

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(new { 
                    success = true,
                    customersDeleted = customersDeleted,
                    suppliersDeleted = suppliersDeleted,
                    message = $"Deleted {customersDeleted} customers and {suppliersDeleted} suppliers"
                });
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting customers and suppliers");
                var response = req.CreateResponse(HttpStatusCode.InternalServerError);
                await response.WriteAsJsonAsync(new { error = ex.Message });
                return response;
            }
        }

        [Function("DeleteCorruptedRecords")]
        public async Task<HttpResponseData> DeleteCorruptedRecords(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "admin/delete-corrupted")] HttpRequestData req)
        {
            try
            {
                _logger.LogInformation("DeleteCorruptedRecords: Starting cleanup of records without IDs");

                int customersDeleted = 0;
                int suppliersDeleted = 0;

                if (_dbContext != null)
                {
                    // Delete customers without IDs using raw SQL
                    customersDeleted = await _dbContext.Database.ExecuteSqlRawAsync(
                        "DELETE FROM Customers WHERE Id IS NULL OR Id = ''");
                    _logger.LogInformation($"Deleted {customersDeleted} corrupted customers");

                    // Delete suppliers without IDs using raw SQL
                    suppliersDeleted = await _dbContext.Database.ExecuteSqlRawAsync(
                        "DELETE FROM Suppliers WHERE Id IS NULL OR Id = ''");
                    _logger.LogInformation($"Deleted {suppliersDeleted} corrupted suppliers");
                }

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(new { 
                    success = true,
                    customersDeleted = customersDeleted,
                    suppliersDeleted = suppliersDeleted,
                    message = $"Deleted {customersDeleted} corrupted customers and {suppliersDeleted} corrupted suppliers (records without IDs)"
                });
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting corrupted records");
                var response = req.CreateResponse(HttpStatusCode.InternalServerError);
                await response.WriteAsJsonAsync(new { error = ex.Message });
                return response;
            }
        }

        private class TestEmailRequest
        {
            public string? Email { get; set; }
        }
    }
}
