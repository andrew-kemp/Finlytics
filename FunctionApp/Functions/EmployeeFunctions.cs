using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using FinanceHubFunctions.Models;
using FinanceHubFunctions.Data;

namespace FinanceHubFunctions.Functions
{
    public class EmployeeFunctions
    {
        private readonly ILogger<EmployeeFunctions> _logger;
        private readonly IEmployeeRepository _employeeRepository;

        public EmployeeFunctions(ILogger<EmployeeFunctions> logger, IEmployeeRepository employeeRepository)
        {
            _logger = logger;
            _employeeRepository = employeeRepository;
        }

        [Function("GetEmployees")]
        public async Task<HttpResponseData> GetEmployees(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "employees")] HttpRequestData req)
        {
            _logger.LogInformation("Getting all employees");

            try
            {
                var employees = await _employeeRepository.GetAllAsync();
                
                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(employees);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting employees");
                var response = req.CreateResponse(HttpStatusCode.InternalServerError);
                await response.WriteStringAsync($"Error: {ex.Message}");
                return response;
            }
        }

        [Function("CreateEmployee")]
        public async Task<HttpResponseData> CreateEmployee(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "employees")] HttpRequestData req)
        {
            _logger.LogInformation("Creating new employee");

            try
            {
                var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                _logger.LogInformation($"Request body: {requestBody}");
                
                var employee = JsonSerializer.Deserialize<Employee>(requestBody, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (employee == null)
                {
                    var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badResponse.WriteStringAsync("Invalid employee data");
                    return badResponse;
                }

                if (string.IsNullOrWhiteSpace(employee.EmployeeNumber))
                {
                    employee.EmployeeNumber = await _employeeRepository.GenerateNextEmployeeNumberAsync();
                }

                _logger.LogInformation($"Creating employee: Name={employee.Name}, Email={employee.Email}");

                var createdEmployee = await _employeeRepository.CreateAsync(employee);
                
                _logger.LogInformation($"Employee created successfully with ID: {createdEmployee.Id}");

                var response = req.CreateResponse(HttpStatusCode.Created);
                await response.WriteAsJsonAsync(createdEmployee);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error creating employee: {ex.Message}");
                _logger.LogError($"Stack trace: {ex.StackTrace}");
                var response = req.CreateResponse(HttpStatusCode.InternalServerError);
                await response.WriteStringAsync($"Error: {ex.Message}");
                return response;
            }
        }

        [Function("GetNextEmployeeNumber")]
        public async Task<HttpResponseData> GetNextEmployeeNumber(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "employees/next-number")] HttpRequestData req)
        {
            try
            {
                var nextNumber = await _employeeRepository.GenerateNextEmployeeNumberAsync();
                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(new { nextNumber });
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating next employee number");
                var response = req.CreateResponse(HttpStatusCode.InternalServerError);
                await response.WriteStringAsync($"Error: {ex.Message}");
                return response;
            }
        }

        [Function("UpdateEmployee")]
        public async Task<HttpResponseData> UpdateEmployee(
            [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "employees/{id}")] HttpRequestData req,
            int id)
        {
            _logger.LogInformation($"Updating employee {id}");

            try
            {
                var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                var employee = JsonSerializer.Deserialize<Employee>(requestBody, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (employee == null)
                {
                    var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badResponse.WriteStringAsync("Invalid employee data");
                    return badResponse;
                }

                employee.Id = id;
                var updatedEmployee = await _employeeRepository.UpdateAsync(employee);
                
                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(updatedEmployee);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating employee {id}");
                var response = req.CreateResponse(HttpStatusCode.InternalServerError);
                await response.WriteStringAsync($"Error: {ex.Message}");
                return response;
            }
        }

        [Function("DeleteEmployee")]
        public async Task<HttpResponseData> DeleteEmployee(
            [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "employees/{id}")] HttpRequestData req,
            int id)
        {
            _logger.LogInformation($"Deactivating employee {id}");

            try
            {
                var employee = await _employeeRepository.GetByIdAsync(id);
                if (employee == null)
                {
                    var notFound = req.CreateResponse(HttpStatusCode.NotFound);
                    await notFound.WriteAsJsonAsync(new { error = "Employee not found" });
                    return notFound;
                }

                // Employees are never hard-deleted — employee IDs must never be recycled.
                // Instead we soft-delete by marking the record as inactive.
                employee.IsActive = false;
                await _employeeRepository.UpdateAsync(employee);

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(new { deactivated = true, id });
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deactivating employee {id}");
                var response = req.CreateResponse(HttpStatusCode.InternalServerError);
                await response.WriteStringAsync($"Error: {ex.Message}");
                return response;
            }
        }
    }
}
