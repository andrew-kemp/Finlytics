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

        // Keep the function app warm — runs every 4 minutes during business hours (Mon-Fri 7am-10pm UTC)
        [Function("KeepAlive")]
        public void KeepAlive(
            [TimerTrigger("0 */4 * * * *")] TimerInfo timer)
        {
            _logger.LogInformation("Keep-alive ping at {time}", DateTime.UtcNow);
        }
    }
}
