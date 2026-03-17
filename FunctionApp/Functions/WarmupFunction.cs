using System;
using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace FinanceHubFunctions.Functions
{
    public class WarmupFunction
    {
        private readonly ILogger<WarmupFunction> _logger;

        public WarmupFunction(ILogger<WarmupFunction> logger)
        {
            _logger = logger;
        }

        [Function("Health")]
        public HttpResponseData Health(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "health")] HttpRequestData req)
        {
            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json");
            response.WriteString("{\"status\":\"ok\",\"timestamp\":\"" + DateTime.UtcNow.ToString("o") + "\"}");
            return response;
        }
    }
}
