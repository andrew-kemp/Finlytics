using System;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using FinanceHubFunctions.Services;

namespace FinanceHubFunctions.Functions
{
    public class SettingsFunctions
    {
        private readonly ILogger _logger;
        private readonly KeyVaultService _keyVaultService;

        public SettingsFunctions(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<SettingsFunctions>();
            _keyVaultService = new KeyVaultService();
        }

        [Function("SetSmtpPassword")]
        public async Task<HttpResponseData> SetSmtpPassword(
            [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "settings/smtp-password")] HttpRequestData req)
        {
            try
            {
                _logger.LogInformation("Setting SMTP password in Key Vault");

                // Get the password from request body
                var requestBody = await new System.IO.StreamReader(req.Body).ReadToEndAsync();
                var data = JsonSerializer.Deserialize<SmtpPasswordRequest>(requestBody);

                if (string.IsNullOrWhiteSpace(data?.Password))
                {
                    var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badResponse.WriteStringAsync("Password is required");
                    return badResponse;
                }

                // Store password in Key Vault
                await _keyVaultService.SetSmtpPasswordAsync(data.Password);

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteStringAsync("SMTP password updated successfully");
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting SMTP password");
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteStringAsync($"Error: {ex.Message}");
                return errorResponse;
            }
        }

        [Function("GetSmtpPasswordStatus")]
        public async Task<HttpResponseData> GetSmtpPasswordStatus(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "settings/smtp-password/status")] HttpRequestData req)
        {
            try
            {
                _logger.LogInformation("Checking SMTP password status");

                var password = await _keyVaultService.GetSmtpPasswordAsync();
                var hasPassword = !string.IsNullOrWhiteSpace(password);

                var response = req.CreateResponse(HttpStatusCode.OK);
                response.Headers.Add("Content-Type", "application/json");
                await response.WriteStringAsync(JsonSerializer.Serialize(new { hasPassword }));
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking SMTP password status");
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteStringAsync($"Error: {ex.Message}");
                return errorResponse;
            }
        }

        private class SmtpPasswordRequest
        {
            public string? Password { get; set; }
        }
    }
}
