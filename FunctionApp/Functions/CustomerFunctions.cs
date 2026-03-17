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
    public class CustomerFunctions
    {
        private readonly SharePointService _sharePointService;
        private readonly ICustomerRepository? _customerRepository;
        private readonly DeletionGuardService? _guard;

        public CustomerFunctions(
            SharePointService sharePointService,
            ICustomerRepository? customerRepository = null,
            DeletionGuardService? guard = null)
        {
            _sharePointService = sharePointService;
            _customerRepository = customerRepository;
            _guard = guard;
        }



        [Function("GetCustomers")]
        public async Task<HttpResponseData> GetCustomers(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "GetCustomers")] HttpRequestData req)
        {
            try
            {
                List<Customer> customers;
                
                // Use database-first approach with SharePoint fallback
                if (_customerRepository != null)
                {
                    var dbCustomers = await _customerRepository.GetAllAsync();
                    customers = dbCustomers.ToList();
                }
                else
                {
                    // Fallback to SharePoint
                    var accessToken = AuthHelper.GetAccessToken(req);
                    if (string.IsNullOrEmpty(accessToken))
                    {
                        throw new System.Exception("No access token provided");
                    }
                    customers = await _sharePointService.GetCustomers(accessToken);
                }
                
                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(customers);
                return response;
            }
            catch (System.Exception ex)
            {
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteAsJsonAsync(new { error = ex.Message });
                return errorResponse;
            }
        }

        [Function("CreateCustomer")]
        public async Task<HttpResponseData> CreateCustomer(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "CreateCustomer")] HttpRequestData req)
        {
            try
            {
                // Use case-insensitive deserialization
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var customer = await JsonSerializer.DeserializeAsync<Customer>(req.Body, options);
                
                Console.WriteLine($"CreateCustomer: Received customer data: {JsonSerializer.Serialize(customer)}");
                
                // Generate ID if not provided
                if (string.IsNullOrEmpty(customer.Id))
                {
                    customer.Id = Guid.NewGuid().ToString();
                }
                
                Customer createdCustomer;
                
                // Use database-first approach with SharePoint fallback
                if (_customerRepository != null)
                {
                    createdCustomer = await _customerRepository.CreateAsync(customer);
                }
                else
                {
                    // Fallback to SharePoint
                    var accessToken = AuthHelper.GetAccessToken(req);
                    var result = await _sharePointService.CreateCustomer(customer, accessToken);
                    if (!result) throw new Exception("Failed to create customer in SharePoint");
                    createdCustomer = customer; // SharePoint doesn't return the created entity
                }
                
                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(createdCustomer);
                return response;
            }
            catch (System.Exception ex)
            {
                Console.WriteLine($"CreateCustomer: Error - {ex.Message}");
                Console.WriteLine($"CreateCustomer: Stack trace - {ex.StackTrace}");
                
                var errorResponse = req.CreateResponse(HttpStatusCode.OK); // Return 200 so frontend can handle it
                await errorResponse.WriteAsJsonAsync(new 
                { 
                    success = false, 
                    error = ex.Message,
                    stackTrace = ex.StackTrace
                });
                return errorResponse;
            }
        }

        [Function("UpdateCustomer")]
        public async Task<HttpResponseData> UpdateCustomer(
            [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "UpdateCustomer/{id}")] HttpRequestData req,
            string id)
        {
            try
            {
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var customer = await JsonSerializer.DeserializeAsync<Customer>(req.Body, options);
                
                // Ensure the ID from the route is used
                customer.Id = id;
                
                Console.WriteLine($"UpdateCustomer: Updating customer ID {id} with data: {JsonSerializer.Serialize(customer)}");
                
                bool result;
                
                // Use database-first approach with SharePoint fallback
                if (_customerRepository != null)
                {
                    await _customerRepository.UpdateAsync(customer);
                    result = true;
                }
                else
                {
                    // Fallback to SharePoint
                    var accessToken = AuthHelper.GetAccessToken(req);
                    result = await _sharePointService.UpdateCustomer(id, customer, accessToken);
                }
                
                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(new { success = result });
                return response;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"UpdateCustomer: Error - {ex.Message}");
                Console.WriteLine($"UpdateCustomer: Stack trace - {ex.StackTrace}");
                
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

        [Function("DeleteCustomer")]
        public async Task<HttpResponseData> DeleteCustomer(
            [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "DeleteCustomer/{id}")] HttpRequestData req,
            string id)
        {
            try
            {
                Console.WriteLine($"DeleteCustomer: Deleting customer with ID {id}");

                if (_guard != null)
                {
                    var blocked = await _guard.GuardAsync(req, "customer");
                    if (blocked != null) return blocked;
                }

                bool result = false;
                
                // Use database-first approach
                if (_customerRepository != null)
                {
                    await _customerRepository.DeleteAsync(id);
                    result = true;
                }
                else
                {
                    throw new Exception("Customer repository not available");
                }
                
                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(new { success = result });
                return response;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"DeleteCustomer: Error - {ex.Message}");
                
                var errorResponse = req.CreateResponse(HttpStatusCode.OK);
                await errorResponse.WriteAsJsonAsync(new 
                { 
                    success = false, 
                    error = ex.Message
                });
                return errorResponse;
            }
        }

        [Function("DeleteCustomerByName")]
        public async Task<HttpResponseData> DeleteCustomerByName(
            [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "DeleteCustomerByName/{name}")] HttpRequestData req,
            string name)
        {
            try
            {
                Console.WriteLine($"DeleteCustomerByName: Deleting customer with Name {name}");

                if (_guard != null)
                {
                    var blocked = await _guard.GuardAsync(req, "customer");
                    if (blocked != null) return blocked;
                }

                bool result = false;
                
                if (_customerRepository != null)
                {
                    var customers = await _customerRepository.GetAllAsync();
                    var customer = customers.FirstOrDefault(c => c.Name == name);
                    
                    if (customer != null)
                    {
                        // If customer has an ID, delete by ID; otherwise delete by finding in context
                        if (!string.IsNullOrEmpty(customer.Id))
                        {
                            await _customerRepository.DeleteAsync(customer.Id);
                        }
                        else
                        {
                            // For records without ID, we need to use raw SQL or find another way
                            throw new Exception("Customer has no ID. Cannot delete using current method.");
                        }
                        result = true;
                    }
                    else
                    {
                        throw new Exception($"Customer '{name}' not found");
                    }
                }
                else
                {
                    throw new Exception("Customer repository not available");
                }
                
                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(new { success = result });
                return response;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"DeleteCustomerByName: Error - {ex.Message}");
                
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