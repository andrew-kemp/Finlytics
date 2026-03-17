using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using FinanceHubFunctions.Services;
using FinanceHubFunctions.Models;
using FinanceHubFunctions.Data;
using FinanceHubFunctions.Helpers;

namespace FinanceHubFunctions.Functions
{
    public class SupplierFunctions
    {
        private readonly SharePointService _sharePointService;
        private readonly ISupplierRepository? _supplierRepository;
        private readonly DeletionGuardService? _guard;

        public SupplierFunctions(
            SharePointService sharePointService,
            ISupplierRepository? supplierRepository = null,
            DeletionGuardService? guard = null)
        {
            _sharePointService = sharePointService;
            _supplierRepository = supplierRepository;
            _guard = guard;
        }

        [Function("GetSuppliers")]
        public async Task<HttpResponseData> GetSuppliers(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "GetSuppliers")] HttpRequestData req)
        {
            try
            {
                List<Supplier> suppliers;
                
                // Use database-first approach with SharePoint fallback
                if (_supplierRepository != null)
                {
                    var dbSuppliers = await _supplierRepository.GetAllAsync();
                    suppliers = dbSuppliers.ToList();
                }
                else
                {
                    // Fallback to SharePoint
                    var accessToken = AuthHelper.GetAccessToken(req);
                    if (string.IsNullOrEmpty(accessToken))
                    {
                        throw new System.Exception("No access token provided");
                    }
                    suppliers = await _sharePointService.GetSuppliers(accessToken);
                }
                
                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(suppliers);
                return response;
            }
            catch (System.Exception ex)
            {
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteAsJsonAsync(new { error = ex.Message });
                return errorResponse;
            }
        }

        [Function("CreateSupplier")]
        public async Task<HttpResponseData> CreateSupplier(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "CreateSupplier")] HttpRequestData req)
        {
            try
            {
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var supplier = await JsonSerializer.DeserializeAsync<Supplier>(req.Body, options);
                
                Console.WriteLine($"CreateSupplier: Received supplier data: {JsonSerializer.Serialize(supplier)}");
                
                // Generate ID if not provided
                if (string.IsNullOrEmpty(supplier.Id))
                {
                    supplier.Id = Guid.NewGuid().ToString();
                }
                
                Supplier createdSupplier;
                
                // Use database-first approach with SharePoint fallback
                if (_supplierRepository != null)
                {
                    createdSupplier = await _supplierRepository.CreateAsync(supplier);
                }
                else
                {
                    // Fallback to SharePoint
                    var accessToken = AuthHelper.GetAccessToken(req);
                    var result = await _sharePointService.CreateSupplier(supplier, accessToken);
                    if (!result) throw new Exception("Failed to create supplier in SharePoint");
                    createdSupplier = supplier;
                }
                
                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(createdSupplier);
                return response;
            }
            catch (System.Exception ex)
            {
                Console.WriteLine($"CreateSupplier: Error - {ex.Message}");
                Console.WriteLine($"CreateSupplier: Stack trace - {ex.StackTrace}");
                
                var errorResponse = req.CreateResponse(HttpStatusCode.OK);
                await errorResponse.WriteAsJsonAsync(new 
                { 
                    success = false, 
                    error = ex.Message,
                    stackTrace = ex.StackTrace
                });
                return errorResponse;
            }
        }

        [Function("UpdateSupplier")]
        public async Task<HttpResponseData> UpdateSupplier(
            [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "UpdateSupplier/{id}")] HttpRequestData req,
            string id)
        {
            try
            {
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var supplier = await JsonSerializer.DeserializeAsync<Supplier>(req.Body, options);
                
                // Ensure the ID from the route is used
                supplier.Id = id;
                
                Console.WriteLine($"UpdateSupplier: Updating supplier ID {id} with data: {JsonSerializer.Serialize(supplier)}");
                
                bool result;
                
                // Use database-first approach with SharePoint fallback
                if (_supplierRepository != null)
                {
                    await _supplierRepository.UpdateAsync(supplier);
                    result = true;
                }
                else
                {
                    // Fallback to SharePoint
                    var accessToken = AuthHelper.GetAccessToken(req);
                    result = await _sharePointService.UpdateSupplier(id, supplier, accessToken);
                }
                
                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(new { success = result });
                return response;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"UpdateSupplier: Error - {ex.Message}");
                Console.WriteLine($"UpdateSupplier: Stack trace - {ex.StackTrace}");
                
                var errorResponse = req.CreateResponse(HttpStatusCode.OK);
                await errorResponse.WriteAsJsonAsync(new 
                { 
                    success = false, 
                    error = ex.Message,
                    stackTrace = ex.StackTrace
                });
                return errorResponse;
            }
        }

        [Function("DeleteSupplier")]
        public async Task<HttpResponseData> DeleteSupplier(
            [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "DeleteSupplier/{id}")] HttpRequestData req,
            string id)
        {
            try
            {
                Console.WriteLine($"DeleteSupplier: Deleting supplier with ID {id}");

                if (_guard != null)
                {
                    var blocked = await _guard.GuardAsync(req, "supplier");
                    if (blocked != null) return blocked;
                }

                bool result = false;
                
                // Use database-first approach
                if (_supplierRepository != null)
                {
                    await _supplierRepository.DeleteAsync(id);
                    result = true;
                }
                else
                {
                    throw new Exception("Supplier repository not available");
                }
                
                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(new { success = result });
                return response;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"DeleteSupplier: Error - {ex.Message}");
                
                var errorResponse = req.CreateResponse(HttpStatusCode.OK);
                await errorResponse.WriteAsJsonAsync(new 
                { 
                    success = false, 
                    error = ex.Message
                });
                return errorResponse;
            }
        }
    }
}