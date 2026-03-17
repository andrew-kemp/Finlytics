using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using FinanceHubFunctions.Models;
using FinanceHubFunctions.Services;
using FinanceHubFunctions.Data;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace FinanceHubFunctions.Functions
{
    public class CompanyLedgerFunctions
    {
        private readonly ILogger _logger;
        private readonly ICompanyLedgerRepository _companyLedgerRepository;
        private readonly DeletionGuardService _guard;

        public CompanyLedgerFunctions(ILoggerFactory loggerFactory, ICompanyLedgerRepository companyLedgerRepository, DeletionGuardService guard)
        {
            _logger = loggerFactory.CreateLogger<CompanyLedgerFunctions>();
            _companyLedgerRepository = companyLedgerRepository;
            _guard = guard;
        }

        [Function("GetCompanyLedger")]
        public async Task<HttpResponseData> GetCompanyLedger(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "companyledger/{periodKey}")] HttpRequestData req,
            string periodKey)
        {
            _logger.LogInformation($"GetCompanyLedger function triggered for period: {periodKey}");

            try
            {
                var entries = await _companyLedgerRepository.GetByPeriodAsync(periodKey);
                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(entries);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting company ledger for period {periodKey}");
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteStringAsync($"Error: {ex.Message}");
                return errorResponse;
            }
        }

        [Function("CreateCompanyLedgerEntry")]
        public async Task<HttpResponseData> CreateCompanyLedgerEntry(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "companyledger")] HttpRequestData req)
        {
            _logger.LogInformation("CreateCompanyLedgerEntry function triggered");

            try
            {
                var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                var entry = JsonSerializer.Deserialize<CompanyLedgerEntry>(requestBody, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (entry == null)
                {
                    var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badRequest.WriteStringAsync("Invalid entry data");
                    return badRequest;
                }

                // Validate entry type
                var validTypes = new[] { "Salary", "EmployeeNI", "EmployerNI", "PAYE", "DLA_In", "DLA_Out", 
                                        "CorpTax_Reserve", "CorpTax_Paid", "Dividend_Declared", "Dividend_Paid",
                                        "VAT_Paid", "VAT_Reclaim" };
                if (!validTypes.Contains(entry.EntryType))
                {
                    var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badRequest.WriteStringAsync($"Invalid EntryType. Must be one of: {string.Join(", ", validTypes)}");
                    return badRequest;
                }

                var createdEntry = await _companyLedgerRepository.CreateAsync(entry);
                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(createdEntry);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating company ledger entry");
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteStringAsync($"Error: {ex.Message}");
                return errorResponse;
            }
        }

        [Function("DeleteCompanyLedgerEntry")]
        public async Task<HttpResponseData> DeleteCompanyLedgerEntry(
            [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "companyledger/{id}")] HttpRequestData req,
            int id)
        {
            _logger.LogInformation($"DeleteCompanyLedgerEntry function triggered for ID: {id}");

            var blocked = await _guard.GuardAsync(req, "company ledger entry");
            if (blocked != null) return blocked;

            try
            {
                await _companyLedgerRepository.DeleteAsync(id);
                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(new { success = true, message = "Entry deleted successfully" });
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting company ledger entry {id}");
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteStringAsync($"Error: {ex.Message}");
                return errorResponse;
            }
        }

        [Function("GetYtdAggregates")]
        public async Task<HttpResponseData> GetYtdAggregates(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "ytd-aggregates")] HttpRequestData req)
        {
            // Determine current tax year: UK tax year runs Apr 6 → Apr 5
            // Tax year 2025 = Apr 6 2025 → Apr 5 2026
            var now = DateTime.UtcNow;
            int taxYear = now.Month > 4 || (now.Month == 4 && now.Day >= 6) ? now.Year : now.Year - 1;
            _logger.LogInformation($"GetYtdAggregates function triggered for tax year: {taxYear}");

            try
            {
                var aggregates = await _companyLedgerRepository.GetYtdAggregatesAsync(taxYear);
                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(aggregates);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting YTD aggregates for tax year {taxYear}");
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteStringAsync($"Error: {ex.Message}");
                return errorResponse;
            }
        }

        [Function("GetCompanyAggregates")]
        public async Task<HttpResponseData> GetCompanyAggregates(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "companyledger/{periodKey}/aggregates")] HttpRequestData req,
            string periodKey)
        {
            _logger.LogInformation($"GetCompanyAggregates function triggered for period: {periodKey}");

            try
            {
                var aggregates = await _companyLedgerRepository.GetAggregatesAsync(periodKey);
                
                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(aggregates);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting company aggregates for period {periodKey}");
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteStringAsync($"Error: {ex.Message}");
                return errorResponse;
            }
        }

        [Function("GetCompanyOverview")]
        public async Task<HttpResponseData> GetCompanyOverview(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "companyledger/{periodKey}/overview")] HttpRequestData req,
            string periodKey)
        {
            _logger.LogInformation($"GetCompanyOverview function triggered for period: {periodKey}");

            try
            {
                // Get company aggregates
                var aggregates = await _companyLedgerRepository.GetAggregatesAsync(periodKey);

                // Get trading ledger totals for the period
                // TODO: Implement GetTradingTotalsForPeriod in database
                // For now, use placeholder values - this needs to aggregate Trading Ledger entries
                decimal tradingProfit = 0; // Sum of (Income - Expenses) for period
                decimal outputVat = 0; // Sum of OutputVAT for period
                decimal inputVat = 0; // Sum of InputVAT for period
                decimal bankBalance = 0; // Get from Company Settings or calculate

                // Get tax config from Company Settings (with defaults)
                var taxConfig = new TaxConfig
                {
                    SmallProfitsRate = 0.19m,
                    MainRate = 0.25m,
                    LimitLower = 50000m,
                    LimitUpper = 250000m
                };

                var overview = CompanyLedgerCalculations.GenerateCompanyOverview(
                    tradingProfit,
                    aggregates,
                    outputVat,
                    inputVat,
                    bankBalance,
                    taxConfig
                );

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(overview);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error generating company overview for period {periodKey}");
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteStringAsync($"Error: {ex.Message}");
                return errorResponse;
            }
        }
    }
}
