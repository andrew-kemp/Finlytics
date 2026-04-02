using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using FinanceHubFunctions.Data;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FinanceHubFunctions.Functions
{
    public class LookupFunctions
    {
        private readonly ILogger _logger;
        private readonly FinanceHubDbContext _context;

        public LookupFunctions(ILoggerFactory loggerFactory, FinanceHubDbContext context)
        {
            _logger = loggerFactory.CreateLogger<LookupFunctions>();
            _context = context;
        }

        [Function("GetLineItemDescriptions")]
        public async Task<HttpResponseData> GetLineItemDescriptions(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "lineitem-descriptions")] HttpRequestData req)
        {
            try
            {
                var descriptions = await _context.Database
                    .SqlQueryRaw<string>(
                        @"SELECT DISTINCT j.[value] AS [Value] FROM (
                            SELECT LineItems FROM Invoices WHERE LineItems IS NOT NULL
                            UNION ALL
                            SELECT LineItems FROM Quotes WHERE LineItems IS NOT NULL
                            UNION ALL
                            SELECT DefaultLineItems AS LineItems FROM RecurringInvoiceTemplates WHERE DefaultLineItems IS NOT NULL
                          ) AS src
                          CROSS APPLY OPENJSON(src.LineItems) WITH ([value] NVARCHAR(500) '$.Description') j
                          WHERE j.[value] IS NOT NULL AND j.[value] <> ''
                          ORDER BY j.[value]")
                    .ToListAsync();

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(descriptions);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching line item descriptions");
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteStringAsync($"Error: {ex.Message}");
                return errorResponse;
            }
        }
    }
}
