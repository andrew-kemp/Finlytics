using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using System.Text.Json;
using FinanceHubFunctions.Services;
using FinanceHubFunctions.Data;
using FinanceHubFunctions.Helpers;

namespace FinanceHubFunctions.Functions
{
    public class CodeGenerationFunctions
    {
        private readonly SharePointService _sharePointService;
        private readonly ICustomerRepository? _customerRepository;
        private readonly ISupplierRepository? _supplierRepository;

        public CodeGenerationFunctions(
            SharePointService sharePointService,
            ICustomerRepository? customerRepository = null,
            ISupplierRepository? supplierRepository = null)
        {
            _sharePointService = sharePointService;
            _customerRepository = customerRepository;
            _supplierRepository = supplierRepository;
        }

        [Function("GenerateCode")]
        public async Task<HttpResponseData> GenerateCode(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestData req)
        {
            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");

            try
            {
                var accessToken = AuthHelper.GetAccessToken(req);
                var requestBody = await new System.IO.StreamReader(req.Body).ReadToEndAsync();
                var requestData = JsonSerializer.Deserialize<GenerateCodeRequest>(requestBody, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                // Remove spaces and special characters, then take first 3 chars
                var cleanName = new string((requestData?.name ?? "")
                    .Where(c => char.IsLetterOrDigit(c))
                    .ToArray());
                
                var prefix = cleanName.Length >= 3
                    ? cleanName.Substring(0, 3).ToUpper()
                    : "XXX";

                // Get existing codes from database (or SharePoint fallback) based on type
                string code;
                if (requestData?.type == "Customer")
                {
                    // Try database first, fall back to SharePoint
                    if (_customerRepository != null)
                    {
                        var customers = await _customerRepository.GetAllAsync();
                        var existingCodes = customers
                            .Where(c => !string.IsNullOrEmpty(c.CustomerCode) && c.CustomerCode.StartsWith(prefix))
                            .Select(c => c.CustomerCode!)
                            .ToList();

                        code = GenerateNextCode(prefix, existingCodes);
                    }
                    else
                    {
                        // Fallback to SharePoint
                        var customers = await _sharePointService.GetCustomers(accessToken);
                        var existingCodes = customers
                            .Where(c => !string.IsNullOrEmpty(c.CustomerCode) && c.CustomerCode.StartsWith(prefix))
                            .Select(c => c.CustomerCode)
                            .ToList();

                        code = GenerateNextCode(prefix, existingCodes);
                    }
                }
                else if (requestData?.type == "Supplier")
                {
                    // Try database first, fall back to SharePoint
                    if (_supplierRepository != null)
                    {
                        var suppliers = await _supplierRepository.GetAllAsync();
                        var existingCodes = suppliers
                            .Where(s => !string.IsNullOrEmpty(s.SupplierCode) && s.SupplierCode.StartsWith(prefix))
                            .Select(s => s.SupplierCode!)
                            .ToList();

                        code = GenerateNextCode(prefix, existingCodes);
                    }
                    else
                    {
                        // Fallback to SharePoint
                        var suppliers = await _sharePointService.GetSuppliers(accessToken);
                        var existingCodes = suppliers
                            .Where(s => !string.IsNullOrEmpty(s.SupplierCode) && s.SupplierCode.StartsWith(prefix))
                            .Select(s => s.SupplierCode)
                            .ToList();

                        code = GenerateNextCode(prefix, existingCodes);
                    }
                }
                else
                {
                    // Default to 001 for other types (can be extended later)
                    code = $"{prefix}001";
                }

                response.WriteString($@"{{""code"":""{code}""}}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"GenerateCode error: {ex.Message}");
                response.WriteString(@"{""code"":""GEN001""}");
            }

            return response;
        }

        private string GenerateNextCode(string prefix, System.Collections.Generic.List<string> existingCodes)
        {
            if (existingCodes == null || existingCodes.Count == 0)
            {
                return $"{prefix}001";
            }

            // Extract numbers from existing codes and find the highest
            var numbers = existingCodes
                .Select(code => 
                {
                    var numPart = code.Substring(prefix.Length);
                    return int.TryParse(numPart, out int num) ? num : 0;
                })
                .Where(num => num > 0)
                .ToList();

            var nextNumber = numbers.Any() ? numbers.Max() + 1 : 1;
            return $"{prefix}{nextNumber:D3}";
        }
    }

    public class GenerateCodeRequest
    {
        public string type { get; set; }
        public string name { get; set; }
    }
}
