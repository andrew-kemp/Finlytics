using System;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using FinanceHubFunctions.Data;

namespace FinanceHubFunctions.Functions
{
    /// <summary>
    /// Minimal data cleanup functions with no optional dependencies.
    /// This class only depends on ILoggerFactory and FinanceHubDbContext.
    /// </summary>
    public class DataCleanupFunctions
    {
        private readonly ILogger _logger;
        private readonly FinanceHubDbContext _dbContext;

        public DataCleanupFunctions(
            ILoggerFactory loggerFactory, 
            FinanceHubDbContext dbContext)
        {
            _logger = loggerFactory.CreateLogger<DataCleanupFunctions>();
            _dbContext = dbContext;
        }

        /// <summary>
        /// Delete corrupted records (payees) that have null or empty IDs.
        /// Route: POST /api/cleanup/corrupted-records
        /// </summary>
        [Function("CleanupCorruptedRecords")]
        public async Task<HttpResponseData> CleanupCorruptedRecords(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "cleanup/corrupted-records")] HttpRequestData req)
        {
            try
            {
                _logger.LogInformation("CleanupCorruptedRecords: Starting cleanup of records without IDs");

                // Delete customers without IDs using raw SQL
                int customersDeleted = await _dbContext.Database.ExecuteSqlRawAsync(
                    "DELETE FROM Customers WHERE Id IS NULL OR Id = ''");
                _logger.LogInformation($"Deleted {customersDeleted} corrupted customers");

                // Delete suppliers without IDs using raw SQL
                int suppliersDeleted = await _dbContext.Database.ExecuteSqlRawAsync(
                    "DELETE FROM Suppliers WHERE Id IS NULL OR Id = ''");
                _logger.LogInformation($"Deleted {suppliersDeleted} corrupted suppliers");

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

        /// <summary>
        /// Simple health check to verify the DataCleanupFunctions class can be instantiated.
        /// Route: GET /api/cleanup/health
        /// </summary>
        [Function("CleanupHealth")]
        public async Task<HttpResponseData> CleanupHealth(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "cleanup/health")] HttpRequestData req)
        {
            _logger.LogInformation("CleanupHealth: Health check");
            
            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new { 
                success = true,
                message = "DataCleanupFunctions is operational",
                timestamp = DateTime.UtcNow
            });
            return response;
        }
    }
}
